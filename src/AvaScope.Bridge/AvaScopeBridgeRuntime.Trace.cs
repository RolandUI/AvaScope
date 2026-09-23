using System.Diagnostics;
using System.Text.Json;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;
using AvaScope.Core;
using AvaScope.Protocol;

namespace AvaScope.Bridge;

public sealed partial class AvaScopeBridgeRuntime
{
    private readonly object _traceLock = new();
    private readonly Dictionary<string, TraceState> _traces = new(StringComparer.Ordinal);
    private readonly Dictionary<string, (string Kind, string TopLevel, Guid Generation)> _diagnosticSources = new(StringComparer.Ordinal);
    private bool _tracesClosed;

    public RuntimeDiagnosticSource RegisterDiagnosticSource(string name, string kind, string topLevelId)
    {
        Dispatcher.UIThread.VerifyAccess();
        if (string.IsNullOrWhiteSpace(name) || name.Length > 64 || kind is not ("validation" or "binding" or "app_event" or "app_log")
            || FindTopLevel(topLevelId) is null)
            throw new ArgumentException("Declare a named validation/binding/app_event/app_log adapter for an existing registered top-level.");
        var generation = Guid.NewGuid();
        lock (_traceLock)
        {
            if (_tracesClosed || _diagnosticSources.Count >= 16 || _diagnosticSources.ContainsKey(name))
                throw new InvalidOperationException("The diagnostic source is duplicate, the 16-source limit was reached, or the bridge is closed.");
            _diagnosticSources.Add(name, (kind, topLevelId, generation));
        }
        return new RuntimeDiagnosticSource((level, message, requestId, operationId, automationId) =>
        {
            lock (_traceLock)
            {
                if (!_diagnosticSources.TryGetValue(name, out var current) || current.Generation != generation || _tracesClosed) return false;
                return RecordTrace(topLevelId, new(0, DateTimeOffset.UtcNow, "app:" + name, kind, level, message,
                    requestId, operationId, automationId, requestId is null && operationId is null ? "uncorrelated" : "app_declared"));
            }
        }, () => { lock (_traceLock) if (_diagnosticSources.TryGetValue(name, out var current) && current.Generation == generation) _diagnosticSources.Remove(name); });
    }

    internal void TraceDispatch(BridgeIpcRequest request, string stage, ProtocolError? error = null)
    {
        if (request.Method == BridgeIpcMethods.Trace || !(BridgeIpcMethods.RequiresControl(request) || request.Method == BridgeIpcMethods.ValidateInput)) return;
        var top = request.TopLevelId ?? request.CustomAction?.Target.TopLevelId ?? request.Mutation?.Target.TopLevelId
            ?? request.DesiredState?.Target.TopLevelId ?? request.FormFill?.Form.TopLevelId ?? request.TableAction?.Query.Table.TopLevelId
            ?? request.FocusProbe?.Target.TopLevelId ?? request.NativePicker?.TopLevelId ?? request.VirtualItem?.Collection.TopLevelId;
        if (top is null) return;
        RecordTrace(top, new(0, DateTimeOffset.UtcNow, "bridge", "dispatch", error is null ? "info" : "error",
            request.Method + ": " + stage + (error is null ? "" : " (" + error.Code + ")"), request.RequestId,
            null, null, "bridge_request"));
    }

    private void TraceOperation(RuntimeOperationSnapshot operation, IReadOnlyList<string> automationIds, bool scopeComplete)
        => RecordTrace(operation.Target.TopLevelId, new(0, operation.UpdatedAt, "bridge", "operation",
            operation.Status == "failed" ? "error" : "info", operation.Status + (operation.CancellationRequested ? "; cancellation requested" : "")
                + (operation.Error is null ? "" : "; " + operation.Error.Code) + (operation.Message is null ? "" : ": " + operation.Message),
            operation.RequestId, operation.OperationId, null, "operation_request"), controlAutomationIds: automationIds, controlScopeComplete: scopeComplete);

    private bool RecordTrace(string topLevelId, RuntimeTraceEvent entry, string? onlyTraceId = null,
        IReadOnlyList<string>? controlAutomationIds = null, bool controlScopeComplete = true)
    {
        lock (_traceLock)
        {
            if (_tracesClosed) return false;
            var stored = false;
            foreach (var trace in _traces.Values.Where(trace => trace.TopLevelId == topLevelId && trace.IsActive && (onlyTraceId is null || trace.Id == onlyTraceId)))
            {
                if (entry.Level is not ("info" or "warning" or "error") || entry.Message is null || entry.Message.Length > 4096
                    || entry.RequestId?.Length > 256 || entry.OperationId?.Length > 192 || entry.AutomationId?.Length > 256
                    || trace.Policy.Policy.ExcludedControlAutomationIds.Count > 0 && (!controlScopeComplete
                        || controlAutomationIds?.Any(id => trace.Policy.Policy.ExcludedControlAutomationIds.Contains(id, StringComparer.Ordinal)) == true)
                    || entry.AutomationId is { } id && trace.Policy.Policy.ExcludedControlAutomationIds.Contains(id, StringComparer.Ordinal))
                { trace.Suppressed++; continue; }
                // Sanitization precedes truncation and retention, including internal dispatch/operation sources.
                var safe = trace.Policy.Sanitize(entry);
                if (!safe.Success || safe.Value?.Message is null) { trace.Suppressed++; continue; }
                var value = safe.Value! with { Sequence = ++trace.Sequence };
                if (value.Message.Length > 1024) value = value with { Message = value.Message[..1024] };
                var bytes = JsonSerializer.SerializeToUtf8Bytes(value).Length;
                while (trace.Events.Count > 0 && (trace.Events.Count >= 128 || trace.Bytes + bytes > 98304))
                { var removed = trace.Events.Dequeue(); trace.Bytes -= removed.Bytes; trace.Dropped++; }
                if (bytes > 98304) { trace.Suppressed++; continue; }
                trace.Events.Enqueue((value, bytes)); trace.Bytes += bytes; stored = true;
            }
            return stored;
        }
    }

    public Task<CoreResult<RuntimeTraceResponse>> TraceAsync(RuntimeTraceRequest request, CancellationToken cancellationToken = default)
        => TraceAsync(request, owner: null, cancellationToken);

    internal async Task<CoreResult<RuntimeTraceResponse>> TraceAsync(RuntimeTraceRequest request, string? owner, CancellationToken cancellationToken)
    {
        if (request.SessionId != SessionId) return Fail("trace_session_mismatch", "Select the trace's exact bridge session.");
        var requestedPolicy = request.Policy is null ? null : new RuntimeEvidencePolicyEnforcer(request.Policy);
        if (requestedPolicy?.AuthorizeAction(SemanticWorkflowActions.Inspect, null) is { Success: false } denied)
            return CoreResult<RuntimeTraceResponse>.Fail(denied.Error!);
        if (request.Action == "start")
        {
            using var startup = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken); startup.CancelAfter(1000);
            try
            {
                if (!await Dispatcher.UIThread.InvokeAsync(() => FindTopLevel(request.TopLevelId!) is not null, DispatcherPriority.Background, startup.Token))
                    return Fail("trace_window_unavailable", "The selected top-level is no longer registered.");
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            { return Fail("trace_window_unavailable", "The selected UI dispatcher did not respond within one second."); }
        }
        TraceState state;
        lock (_traceLock)
        {
            if (_tracesClosed) return Fail("trace_session_closed", "The trace store is closed.");
            foreach (var expired in _traces.Where(pair => pair.Value.Clock.ElapsedMilliseconds > pair.Value.DurationMs + 600000).Select(pair => pair.Key).ToArray())
                _traces.Remove(expired);
            if (request.Action == "start")
            {
                if (_traces.Count >= 4)
                {
                    var old = _traces.Values.Where(trace => !trace.IsActive).MaxBy(trace => trace.Clock.Elapsed);
                    if (old is null) return Fail("trace_limit", "Four traces are already collecting. Stop a trace before starting another.");
                    _traces.Remove(old.Id);
                }
                state = new(Guid.NewGuid().ToString("N"), request.TopLevelId!, request.DurationMs, request.SampleValidation, requestedPolicy!, owner);
                _traces.Add(state.Id, state);
            }
            else if (!_traces.TryGetValue(request.TraceId!, out state!))
                return Fail("trace_unknown", "The trace is unknown, expired, evicted or belongs to an earlier app instance.");
            if (request.Action == "stop" && state.Owner is not null && state.Owner != owner)
                return Fail("trace_owner_mismatch", "Stop requires the originating run's current session control lease.");
        }
        if (state.SampleValidation && state.IsActive)
        {
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            deadline.CancelAfter(1000);
            try { await Dispatcher.UIThread.InvokeAsync(() => SampleTraceValidation(state), DispatcherPriority.Background, deadline.Token); }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
            catch (Exception exception) when (exception is not OutOfMemoryException and not AccessViolationException)
            { lock (_traceLock) state.ValidationAvailability = "partial"; }
        }
        lock (_traceLock)
        {
            if (_tracesClosed) return Fail("trace_session_closed", "The trace store closed during observation.");
            if (request.Action == "stop") state.Stopped = true;
            var matching = state.Events.Select(item => item.Event).Select(entry => entry with
            {
                Relation = request.RequestId is null && request.OperationId is null ? "unfiltered"
                    : request.RequestId is not null && entry.RequestId == request.RequestId || request.OperationId is not null && entry.OperationId == request.OperationId
                        ? "explicitly_correlated" : entry.RequestId is null && entry.OperationId is null ? "temporal_only" : "different_correlation"
            }).ToArray();
            var sources = new List<RuntimeTraceSource>
            {
                new("dispatch", "available", "Bridge request boundaries; response success does not prove application completion."),
                new("operation", "available", "Only explicitly started, app-reported operation transitions.")
            };
            foreach (var kind in new[] { "validation", "binding", "app_event", "app_log" })
            {
                var available = _diagnosticSources.Values.Any(source => source.TopLevel == state.TopLevelId && source.Kind == kind);
                sources.Add(new(kind, available ? "adapter_registered" : kind == "validation" && state.SampleValidation ? state.ValidationAvailability : "unavailable",
                    available ? "Explicit host adapter; no event is not proof of absence." : kind == "validation" && state.SampleValidation
                        ? "Bounded current UI samples, not an event subscription; temporal association only." : "No opted-in adapter; no global logger replacement or inferred diagnostics."));
            }
            var response = new RuntimeTraceResponse(SessionId, state.Id, state.TopLevelId, state.IsActive ? "collecting" : "stopped",
                matching.TakeLast(request.MaxEvents).ToArray(), sources, state.Events.Count, state.Dropped, state.Suppressed,
                matching.Length > request.MaxEvents || state.Dropped > 0 || state.Suppressed > 0, DateTimeOffset.UtcNow);
            return state.Policy.Sanitize(response);
        }
        static CoreResult<RuntimeTraceResponse> Fail(string code, string message) => CoreResult<RuntimeTraceResponse>.Fail(new(code, message));
    }

    private void SampleTraceValidation(TraceState state)
    {
        Dispatcher.UIThread.VerifyAccess();
        var top = FindTopLevel(state.TopLevelId);
        if (top is null) { lock (_traceLock) state.ValidationAvailability = "unavailable"; return; }
        var pending = new Queue<(Avalonia.Visual Node, int Depth, bool Excluded)>(); pending.Enqueue((top, 0, false));
        var count = 0; var budget = Stopwatch.StartNew(); var partial = false;
        while (pending.Count > 0 && count++ < 128 && budget.ElapsedMilliseconds < 200)
        {
            var (node, depth, ancestorExcluded) = pending.Dequeue();
            var id = AutomationProperties.GetAutomationId(node);
            var excluded = ancestorExcluded || id is not null && state.Policy.Policy.ExcludedControlAutomationIds.Contains(id, StringComparer.Ordinal);
            if (excluded) continue;
            if (node is Control control)
                foreach (var error in (DataValidationErrors.GetErrors(control) ?? []).Take(4))
                    RecordTrace(state.TopLevelId, new(0, DateTimeOffset.UtcNow, "avalonia_validation_sample", "validation", "error",
                        error is Exception exception ? exception.Message : error?.ToString() ?? "Validation error", null, null, id, "uncorrelated"), state.Id);
            if (depth >= 8) { if (node.GetVisualChildren().Any()) partial = true; continue; }
            foreach (var child in node.GetVisualChildren())
            {
                if (pending.Count + count >= 128) { partial = true; break; }
                pending.Enqueue((child, depth + 1, excluded));
            }
        }
        lock (_traceLock) state.ValidationAvailability = partial || pending.Count > 0 ? "partial" : "sampled";
    }

    private void CloseTraces()
    { lock (_traceLock) { _tracesClosed = true; _traces.Clear(); _diagnosticSources.Clear(); } }

    private void StopTopLevelTraces(string topLevelId)
    {
        lock (_traceLock)
        {
            foreach (var trace in _traces.Values.Where(trace => trace.TopLevelId == topLevelId)) trace.Stopped = true;
            foreach (var source in _diagnosticSources.Where(pair => pair.Value.TopLevel == topLevelId).Select(pair => pair.Key).ToArray())
                _diagnosticSources.Remove(source);
        }
    }

    private sealed class TraceState(string id, string topLevelId, int durationMs, bool sampleValidation, RuntimeEvidencePolicyEnforcer policy, string? owner)
    {
        public string Id { get; } = id;
        public string TopLevelId { get; } = topLevelId;
        public int DurationMs { get; } = durationMs;
        public bool SampleValidation { get; } = sampleValidation;
        public RuntimeEvidencePolicyEnforcer Policy { get; } = policy;
        public string? Owner { get; } = owner;
        public Stopwatch Clock { get; } = Stopwatch.StartNew();
        public bool Stopped { get; set; }
        public bool IsActive => !Stopped && Clock.ElapsedMilliseconds < DurationMs;
        public string ValidationAvailability { get; set; } = "not_sampled";
        public Queue<(RuntimeTraceEvent Event, int Bytes)> Events { get; } = new();
        public int Bytes { get; set; }
        public long Sequence { get; set; }
        public long Dropped { get; set; }
        public long Suppressed { get; set; }
    }
}
