using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using Avalonia.Threading;
using AvaScope.Core;
using AvaScope.Protocol;

namespace AvaScope.Bridge;

public sealed partial class AvaScopeBridgeRuntime
{
    private const int MaximumChangeScopes = 8;
    private const int MaximumRetainedChanges = 128;
    private const int MaximumRetainedChangeBytes = 384 * 1024;
    private readonly object _observationChangesLock = new();
    private readonly Dictionary<string, ObservationChangeState> _observationChanges = new(StringComparer.Ordinal);
    private bool _observationChangesClosed;

    public async Task<CoreResult<RuntimeObservationChangesResponse>> ObserveChangesAsync(
        RuntimeObservationChangesRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Observation.SessionId != SessionId)
            return CoreResult<RuntimeObservationChangesResponse>.Fail(new CoreError("observation_session_mismatch", "The cursor belongs to one explicit bridge session."));
        var elapsed = Stopwatch.StartNew();
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(Math.Min(4000, request.WaitMs + Math.Min(1000, request.Observation.TimeoutMs)));
        ObservationChangeState? state = null;
        var gateHeld = false;
        try
        {
            var scope = ChangeScopeFingerprint(request);
            string? resyncReason = null;
            long after = 0;
            lock (_observationChangesLock)
            {
                if (_observationChangesClosed)
                    return CoreResult<RuntimeObservationChangesResponse>.Fail(new CoreError("observation_session_closed", "The bridge observation journal has been closed."));
                foreach (var expired in _observationChanges.Where(pair => pair.Value.Expired).Select(pair => pair.Key).ToArray())
                    _observationChanges.Remove(expired);
                if (request.Cursor is { } cursor)
                {
                    var parts = cursor.Split(':');
                    if (parts.Length != 2 || !Guid.TryParseExact(parts[0], "N", out _)
                        || !long.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out after))
                        resyncReason = "invalid_cursor";
                    else if (!_observationChanges.TryGetValue(parts[0], out state))
                        resyncReason = "expired_evicted_or_restarted";
                    else if (state.Scope != scope)
                    {
                        state = null;
                        resyncReason = "cursor_scope_mismatch";
                    }
                }
            }
            if (state is null)
            {
                var snapshot = await SampleChangesAsync(request.Observation, deadline.Token);
                if (!snapshot.Success) return CoreResult<RuntimeObservationChangesResponse>.Fail(snapshot.Error!);
                state = new ObservationChangeState(scope, request.CursorTtlMs, snapshot.Value!);
                lock (_observationChangesLock)
                {
                    if (_observationChangesClosed)
                        return CoreResult<RuntimeObservationChangesResponse>.Fail(new CoreError("observation_session_closed", "The bridge observation journal has been closed."));
                    if (_observationChanges.Count >= MaximumChangeScopes)
                        _observationChanges.Remove(_observationChanges.MinBy(pair => pair.Value.LastAccess).Key);
                    _observationChanges[state.Id] = state;
                }
                return CoreResult<RuntimeObservationChangesResponse>.Ok(ChangeResponse(request, state, [],
                    baseline: state.Snapshot, resyncReason, samples: 1));
            }
            await state.Gate.WaitAsync(deadline.Token);
            gateHeld = true;
            var invalidFuture = after > state.Sequence;
            var samples = 0;
            while (true)
            {
                lock (_observationChangesLock)
                    if (_observationChangesClosed)
                        return CoreResult<RuntimeObservationChangesResponse>.Fail(new CoreError("observation_session_closed", "The bridge observation journal has been closed."));
                var snapshot = await SampleChangesAsync(request.Observation, deadline.Token);
                if (!snapshot.Success) return CoreResult<RuntimeObservationChangesResponse>.Fail(snapshot.Error!);
                samples++;
                UpdateChanges(state, snapshot.Value!);
                state.Touch();
                if (invalidFuture)
                    return CoreResult<RuntimeObservationChangesResponse>.Ok(ChangeResponse(request, state, [], state.Snapshot, "invalid_future_cursor", samples));
                if (after < state.OldestSequence - 1)
                    return CoreResult<RuntimeObservationChangesResponse>.Ok(ChangeResponse(request, state, [], state.Snapshot, "buffer_overflow", samples));
                var events = state.Events.Where(item => item.Change.Sequence > after).Take(request.MaxEvents).Select(item => item.Change).ToArray();
                if (events.Length > 0 || elapsed.ElapsedMilliseconds >= request.WaitMs)
                    return CoreResult<RuntimeObservationChangesResponse>.Ok(ChangeResponse(request, state, events, null, null, samples));
                await Task.Delay((int)Math.Max(1, Math.Min(request.PollIntervalMs, request.WaitMs - elapsed.ElapsedMilliseconds)), deadline.Token);
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return CoreResult<RuntimeObservationChangesResponse>.Fail(new CoreError("observation_change_timeout",
                "The UI sampler or another request on this cursor exceeded the bounded polling deadline; no complete observation is claimed."));
        }
        finally
        {
            if (gateHeld) state!.Gate.Release();
        }
    }

    private async Task<CoreResult<RuntimeObservationResponse>> SampleChangesAsync(RuntimeObservationRequest request, CancellationToken cancellationToken)
    {
        var snapshot = await Dispatcher.UIThread.InvokeAsync(() => CollectObservation(request), DispatcherPriority.Background, cancellationToken);
        snapshot = snapshot with { Consistency = "single_dispatcher_sample", ChangedDuringCollection = null };
        return request.Policy is null ? CoreResult<RuntimeObservationResponse>.Ok(snapshot)
            : new RuntimeEvidencePolicyEnforcer(request.Policy).Sanitize(snapshot);
    }

    private static string ChangeScopeFingerprint(RuntimeObservationChangesRequest request) => Convert.ToHexStringLower(SHA256.HashData(
        JsonSerializer.SerializeToUtf8Bytes(new
        {
            request.Observation.SessionId, TopLevelIds = request.Observation.TopLevelIds.Order(StringComparer.Ordinal).ToArray(),
            request.Observation.TopLevelTitle, request.Observation.ActiveOnly, request.Observation.RootNodeId,
            request.Observation.MaxTopLevels, request.Observation.MaxNodes, request.Observation.MaxDepth,
            request.Observation.IncludeDiagnostics, request.Observation.Policy, request.CursorTtlMs
        })));

    private RuntimeObservationChangesResponse ChangeResponse(RuntimeObservationChangesRequest request,
        ObservationChangeState state, IReadOnlyList<RuntimeObservationChange> events, RuntimeObservationResponse? baseline,
        string? resyncReason, int samples)
    {
        var delivered = baseline is not null ? state.Sequence : events.LastOrDefault()?.Sequence ?? state.Sequence;
        return new RuntimeObservationChangesResponse(request.Observation.RequestId, SessionId,
            $"{state.Id}:{delivered.ToString(CultureInfo.InvariantCulture)}", state.ExpiresAt,
            resyncReason is not null ? "resync_required" : baseline is not null ? "baseline" : events.Count == 0 ? "unchanged" : "changed",
            resyncReason is not null, resyncReason, events, baseline, delivered < state.Sequence,
            state.Sequence, state.OldestSequence, state.Dropped, state.Events.Count, state.RetainedBytes, samples);
    }

    private static void UpdateChanges(ObservationChangeState state, RuntimeObservationResponse current)
    {
        var previousWindows = state.Snapshot.Windows.ToDictionary(window => window.TopLevelId, StringComparer.Ordinal);
        var currentWindows = current.Windows.ToDictionary(window => window.TopLevelId, StringComparer.Ordinal);
        foreach (var id in previousWindows.Keys.Union(currentWindows.Keys, StringComparer.Ordinal).Order(StringComparer.Ordinal))
        {
            previousWindows.TryGetValue(id, out var previous);
            currentWindows.TryGetValue(id, out var next);
            if (next is null)
            {
                Add("window_left_scope", id);
                continue;
            }
            var metadata = next with { Nodes = [], Screenshot = null, ScreenshotPng = null };
            if (previous is null)
                Add("window_entered_scope", id, window: metadata);
            else if (!EqualWindowMetadata(previous, next))
                Add(previous.Window is not null && next.Window is null ? "window_closed" : previous.Window is null && next.Window is not null ? "window_opened" : "window_changed",
                    id, window: metadata);
            var oldNodes = (previous?.Nodes ?? []).ToDictionary(node => node.NodeId, StringComparer.Ordinal);
            var newNodes = next.Nodes.ToDictionary(node => node.NodeId, StringComparer.Ordinal);
            foreach (var nodeId in oldNodes.Keys.Union(newNodes.Keys, StringComparer.Ordinal).Order(StringComparer.Ordinal))
            {
                oldNodes.TryGetValue(nodeId, out var oldNode);
                newNodes.TryGetValue(nodeId, out var newNode);
                if (newNode is null) Add("node_left_sample", id, nodeId);
                else if (oldNode is null) Add("node_entered_sample", id, nodeId, newNode);
                else if (!JsonEqual(oldNode, newNode))
                {
                    var fields = new List<string>();
                    if (oldNode.Text != newNode.Text) fields.Add("text");
                    if (!JsonEqual(oldNode.State, newNode.State)) fields.Add("state");
                    if (oldNode.Bounds != newNode.Bounds) fields.Add("bounds");
                    if (!JsonEqual(oldNode.Validation, newNode.Validation)) fields.Add("validation");
                    if (oldNode.Generation != newNode.Generation) fields.Add("generation");
                    if (fields.Count == 0) fields.Add("identity_or_structure");
                    Add("node_changed", id, nodeId, newNode, fields: fields);
                }
            }
        }
        state.Snapshot = current;

        void Add(string kind, string topLevelId, string? nodeId = null, RuntimeObservedNode? node = null,
            RuntimeObservedWindow? window = null, IReadOnlyList<string>? fields = null)
        {
            var change = new RuntimeObservationChange(++state.Sequence, kind, topLevelId, current.CompletedAt, nodeId, node, window, fields);
            var size = JsonSerializer.SerializeToUtf8Bytes(change).Length;
            state.Events.Enqueue((change, size));
            state.RetainedBytes += size;
            while (state.Events.Count > MaximumRetainedChanges || state.RetainedBytes > MaximumRetainedChangeBytes)
            {
                state.RetainedBytes -= state.Events.Dequeue().Bytes;
                state.Dropped++;
            }
        }
    }

    private static bool EqualWindowMetadata(RuntimeObservedWindow left, RuntimeObservedWindow right) => JsonEqual(
        left with { Nodes = [], ObservedAt = default, Screenshot = null, ScreenshotPng = null },
        right with { Nodes = [], ObservedAt = default, Screenshot = null, ScreenshotPng = null });

    private static bool JsonEqual<T>(T left, T right) => JsonSerializer.SerializeToUtf8Bytes(left).AsSpan()
        .SequenceEqual(JsonSerializer.SerializeToUtf8Bytes(right));

    private sealed class ObservationChangeState(string scope, int ttlMs, RuntimeObservationResponse snapshot)
    {
        public string Id { get; } = Guid.NewGuid().ToString("N");
        public string Scope { get; } = scope;
        public SemaphoreSlim Gate { get; } = new(1, 1);
        public long LastAccess { get; private set; } = Stopwatch.GetTimestamp();
        public DateTimeOffset ExpiresAt { get; private set; } = DateTimeOffset.UtcNow.AddMilliseconds(ttlMs);
        public bool Expired => Stopwatch.GetElapsedTime(LastAccess).TotalMilliseconds >= ttlMs;
        public RuntimeObservationResponse Snapshot { get; set; } = snapshot;
        public Queue<(RuntimeObservationChange Change, int Bytes)> Events { get; } = new();
        public long Sequence { get; set; }
        public long Dropped { get; set; }
        public int RetainedBytes { get; set; }
        public long OldestSequence => Events.TryPeek(out var item) ? item.Change.Sequence : Sequence + 1;
        public void Touch()
        {
            LastAccess = Stopwatch.GetTimestamp();
            ExpiresAt = DateTimeOffset.UtcNow.AddMilliseconds(ttlMs);
        }
    }
}
