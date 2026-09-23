using System.Collections.Concurrent;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;
using AvaScope.Core;
using AvaScope.Protocol;

namespace AvaScope.Bridge;

public sealed partial class AvaScopeBridgeRuntime
{
    private readonly ConcurrentDictionary<string, RegisteredScene> _scenes = new(StringComparer.Ordinal);
    private bool _scenesClosed;

    public IDisposable RegisterScene(Visual canvas, Func<AvaScopeSceneSnapshot> capture)
    {
        ArgumentNullException.ThrowIfNull(canvas); ArgumentNullException.ThrowIfNull(capture);
        Dispatcher.UIThread.VerifyAccess();
        if (TopLevel.GetTopLevel(canvas) is not { } top || FindTopLevel(InspectableTopLevel.CreateId(top)) != top)
            throw new ArgumentException("Register a scene only on a live canvas in this bridge's registered/lifetime top-levels.");
        var key = CreateObjectGeneration(canvas);
        var entry = new RegisteredScene(new(canvas), capture, Guid.NewGuid().ToString("N"), InspectableTopLevel.CreateId(top));
        if (Volatile.Read(ref _scenesClosed) || _scenes.Count >= 32 || !_scenes.TryAdd(key, entry))
            throw new InvalidOperationException("The scene is already registered, the 32-scene limit is reached, or the bridge is closed.");
        return new TopLevelRegistration(() => _scenes.TryRemove(new KeyValuePair<string, RegisteredScene>(key, entry)));
    }

    private void CloseScenes()
    {
        Volatile.Write(ref _scenesClosed, true);
        _scenes.Clear();
    }

    private void StopTopLevelScenes(string topLevelId)
    {
        foreach (var pair in _scenes.Where(pair => pair.Value.TopLevelId == topLevelId))
            _scenes.TryRemove(pair);
    }

    public Task<CoreResult<RuntimeSceneResponse>> SceneAsync(RuntimeSceneRequest request, CancellationToken cancellationToken = default)
        => SceneAsync(request, owner: null, cancellationToken);

    internal async Task<CoreResult<RuntimeSceneResponse>> SceneAsync(RuntimeSceneRequest request, string? owner, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var invoke = request.Action == "invoke";
        if (invoke && !await _explicitInputGate.WaitAsync(0, cancellationToken))
            return CoreResult<RuntimeSceneResponse>.Fail(new("scene_busy", "Another compound input operation is active; wait for its result."));
        try { return await Dispatcher.UIThread.InvokeAsync(() => Scene(request, owner), DispatcherPriority.Background, cancellationToken); }
        finally { if (invoke) _explicitInputGate.Release(); }
    }

    private CoreResult<RuntimeSceneResponse> Scene(RuntimeSceneRequest request, string? owner)
    {
        Dispatcher.UIThread.VerifyAccess();
        if (request.Canvas.SessionId != SessionId) return Fail("scene_session_mismatch", "Select this bridge session explicitly.");
        if (JsonSerializer.SerializeToUtf8Bytes(request).Length > 65536) return Fail("scene_request_limit", "The scene request exceeds 64 KiB.");
        var policy = request.Policy is null ? null : new RuntimeEvidencePolicyEnforcer(request.Policy);
        if (policy?.AuthorizeAction(SemanticWorkflowActions.Inspect, null) is { Success: false } inspectDenied)
            return CoreResult<RuntimeSceneResponse>.Fail(inspectDenied.Error!);
        if (request.Action == "invoke" && policy?.AuthorizeAction(SemanticWorkflowActions.CustomAction, request.ActionName) is { Success: false } actionDenied)
            return CoreResult<RuntimeSceneResponse>.Fail(actionDenied.Error!);
        RuntimeCustomActionResponse? action = null;
        CoreResult<RuntimeSceneResponse> result;
        try
        {
            var initial = ReadScene(request, policy);
            if (request.Action == "inspect") return Safe(new("observed", initial.Snapshot, null, []));
            var custom = new RuntimeCustomActionRequest(request.RequestId!, request.Canvas, request.ActionName!, request.Parameters, request.AllowDestructive);
            var invoked = InvokeCustomAction(custom, owner, () =>
            {
                try
                {
                    var current = ReadScene(request, policy);
                    var expected = request.ExpectedObject!;
                    if (current.Snapshot.SceneGeneration != expected.SceneGeneration || current.Snapshot.Revision != expected.SceneRevision)
                        return CoreResult<RuntimeSceneObject>.Fail(new("scene_stale", "The scene, camera, canvas geometry or registration changed. Inspect the object again before a new intent."));
                    var item = current.Objects.SingleOrDefault(item => item.Id == expected.ObjectId && item.Generation == expected.ObjectGeneration);
                    if (item is null || !current.VisibleObjectIds.Contains(item.Id))
                        return CoreResult<RuntimeSceneObject>.Fail(new("scene_object_unavailable", "The object was removed, replaced, protected by policy or is outside bounded coverage."));
                    if (item.Actions?.Contains(request.ActionName!, StringComparer.Ordinal) != true)
                        return CoreResult<RuntimeSceneObject>.Fail(new("scene_action_undeclared", "The object does not declare this custom action."));
                    var registration = EnumerateCustomActions().FirstOrDefault(entry => ReferenceEquals(entry.Target, current.Canvas) && entry.Registration.Name == request.ActionName)?.Registration;
                    if (registration?.SafetyClassification == RuntimeCustomActionSafetyClassifications.Destructive
                        && policy is not null && !policy.AllowsDestructiveAction(request.AllowDestructive, false))
                        return CoreResult<RuntimeSceneObject>.Fail(new("scene_action_policy_denied", "The evidence policy did not authorize this destructive action."));
                    return CoreResult<RuntimeSceneObject>.Ok(item);
                }
                catch (Exception exception) when (exception is not OutOfMemoryException and not AccessViolationException)
                { return CoreResult<RuntimeSceneObject>.Fail(SceneError(exception)); }
            });
            if (!invoked.Success) return CoreResult<RuntimeSceneResponse>.Fail(invoked.Error!);
            action = invoked.Value!;
            var observed = ReadScene(request, policy).Snapshot;
            result = Safe(new(action.Status == RuntimeCustomActionStatuses.Executed ? "executed" : action.Executed ? "uncertain" : "rejected",
                observed, action, action.Diagnostics));
        }
        catch (Exception exception) when (exception is not OutOfMemoryException and not AccessViolationException)
        {
            var error = SceneError(exception);
            result = Safe(new(action?.Executed == true ? "partial" : "rejected", null, action, [new(error.Code, error.Message)]));
        }
        return result;

        CoreResult<RuntimeSceneResponse> Safe(RuntimeSceneResponse response)
            => policy is null ? CoreResult<RuntimeSceneResponse>.Ok(response) : policy.Sanitize(response);
        static CoreResult<RuntimeSceneResponse> Fail(string code, string message) => CoreResult<RuntimeSceneResponse>.Fail(new(code, message));
    }

    private SceneRead ReadScene(RuntimeSceneRequest request, RuntimeEvidencePolicyEnforcer? policy)
    {
        var started = Stopwatch.GetTimestamp();
        var target = request.Canvas;
        var top = FindTopLevel(target.TopLevelId) ?? throw new SceneStop("scene_window_unavailable", "The selected window is no longer registered.");
        if (!ResolveInputGenerationTarget(top, target.TopLevelId, target.NodeId, target).Success
            || FindNodeById(top, target.NodeId!) is not Visual canvas)
            throw new SceneStop("scene_canvas_stale", "Resolve the current generation-pinned canvas again.");
        var key = CreateObjectGeneration(canvas);
        if (!_scenes.TryGetValue(key, out var registration) || !registration.Canvas.TryGetTarget(out var declared) || declared != canvas)
            throw new SceneStop("scene_unsupported", "This canvas has no opted-in semantic scene adapter. Hidden objects are not inferred from pixels.");
        var identity = QueryIdentity(canvas, false);
        CheckCanvas();
        var first = Capture();
        var second = Capture();
        if (first.Revision != second.Revision) throw new SceneStop("scene_changed_during_capture", "The adapter or canvas changed between bounded samples; no actionable snapshot was produced.");
        var raw = second.Data.Objects;
        var publicIds = raw.Where(item => !Protected(item)).Select(item => item.Id).ToHashSet(StringComparer.Ordinal);
        var items = new List<RuntimeSceneItem>();
        var unavailable = new HashSet<string>(StringComparer.Ordinal);
        if (!second.Data.IsComplete) unavailable.Add("scene_coverage_incomplete");
        if (publicIds.Count != raw.Count) unavailable.Add("policy_exclusions");
        if (second.Data.UnavailableReason is { } reason) unavailable.Add("host: " + SafeText(reason, 512));
        var sceneMatrix = second.Data.SceneToCanvas;
        var canvasMatrix = second.CanvasToTop;
        var geometryAvailable = !sceneMatrix.ContainsPerspective() && canvasMatrix is { } matrix && !matrix.ContainsPerspective();
        if (!geometryAvailable) unavailable.Add("geometry_unavailable_or_perspective");
        var known = raw.ToDictionary(item => item.Id, StringComparer.Ordinal);
        var matching = 0;
        var bytes = 0;
        foreach (var item in raw.Where(item => publicIds.Contains(item.Id)))
        {
            CheckBudget();
            if (request.Action == "inspect" && (request.ObjectId is not null && request.ObjectId != item.Id
                || request.ObjectType is not null && request.ObjectType != item.Type
                || request.RelatedTo is not null && item.Relationships?.Any(relation => relation.TargetId == request.RelatedTo && publicIds.Contains(relation.TargetId)) != true)) continue;
            matching++;
            if (items.Count == request.MaxObjects) { unavailable.Add("response_object_limit"); continue; }
            var gaps = new List<string>();
            if (item.Label?.Length > 512) gaps.Add("label_truncated");
            var relationships = (item.Relationships ?? []).Where(relation => publicIds.Contains(relation.TargetId)
                && known.TryGetValue(relation.TargetId, out var endpoint) && endpoint.Generation == relation.TargetGeneration).ToArray();
            if (relationships.Length != (item.Relationships?.Count ?? 0)) gaps.Add("relationships_incomplete");
            if (item.Bounds is null || !geometryAvailable) gaps.Add("geometry_unavailable");
            if (item.Selected is null) gaps.Add("selection_unknown");
            var safe = item with
            {
                Label = item.Label is null ? null : SafeText(item.Label, 512),
                Relationships = relationships,
                Metadata = item.Metadata?.ToDictionary(pair => SafeText(pair.Key, 64), pair => SafeText(pair.Value, 256), StringComparer.Ordinal),
                Actions = (item.Actions ?? []).Where(name => policy is null || policy.SanitizeScalar(name) == name).ToArray(),
                PrivacyAutomationId = item.PrivacyAutomationId is null ? null : SafeText(item.PrivacyAutomationId, 128)
            };
            NodeBounds? canvasBounds = null, topBounds = null;
            if (item.Bounds is { } bounds && geometryAvailable)
            {
                var rect = new Rect(bounds.X, bounds.Y, bounds.Width, bounds.Height);
                canvasBounds = Bounds(rect.TransformToAABB(sceneMatrix));
                topBounds = Bounds(rect.TransformToAABB(sceneMatrix * canvasMatrix!.Value));
            }
            var entry = new RuntimeSceneItem(safe, new(registration.Generation, second.Revision, item.Id, item.Generation), canvasBounds, topBounds, gaps);
            var entryBytes = JsonSerializer.SerializeToUtf8Bytes(entry).Length;
            if (bytes + entryBytes > 96 * 1024) { unavailable.Add("response_byte_limit"); continue; }
            bytes += entryBytes; items.Add(entry);
        }
        CheckCanvas();
        var context = new RuntimeTargetContext(target.SessionId, target.TopLevelId, target.TreeKind, target.NodeId,
            DateTimeOffset.UtcNow, target.TargetKind, target.TopLevelGeneration, target.NodeGeneration);
        var snapshot = new RuntimeSceneSnapshot(context, registration.Generation, second.Revision, items,
            unavailable.Count == 0 ? "complete" : "partial", raw.Count, matching,
            sceneMatrix.ContainsPerspective() ? null : Transform(sceneMatrix), canvasMatrix is { } affine && !affine.ContainsPerspective() ? Transform(affine) : null,
            top.RenderScaling, unavailable.ToArray(), DateTimeOffset.UtcNow);
        return new(canvas, raw, publicIds, snapshot);

        void CheckBudget()
        {
            if (Stopwatch.GetElapsedTime(started) > TimeSpan.FromSeconds(2))
                throw new SceneStop("scene_budget", "The cooperative two-second scene inspection budget expired.");
        }

        void CheckCanvas()
        {
            CheckBudget();
            if (Volatile.Read(ref _scenesClosed) || !_scenes.TryGetValue(key, out var current) || current != registration)
                throw new SceneStop("scene_registration_changed", "The scene adapter was disposed or its session closed.");
            if (TopLevel.GetTopLevel(canvas) != top || identity != QueryIdentity(canvas, false)
                || !ResolveInputGenerationTarget(top, target.TopLevelId, target.NodeId, target).Success)
                throw new SceneStop("scene_canvas_stale", "The canvas detached or was recycled during capture.");
            if (request.Policy is { } options)
            {
                var ancestors = canvas.GetSelfAndVisualAncestors().Take(65).ToArray();
                if (ancestors.Length > 64 || ancestors.Any(node => options.ExcludedControlAutomationIds.Contains(GetAutomationId(node), StringComparer.Ordinal)
                    || options.RedactedAutomationIds.Contains(GetAutomationId(node), StringComparer.Ordinal)))
                    throw new SceneStop("scene_excluded", "The canvas or an ancestor is protected by the evidence policy.");
            }
        }

        (AvaScopeSceneSnapshot Data, string Revision, Matrix? CanvasToTop) Capture()
        {
            CheckCanvas();
            var data = registration.Capture() ?? throw new SceneStop("scene_adapter_invalid", "The scene adapter returned no snapshot.");
            RuntimeSceneRequest.ValidateIdentifier(data.Revision);
            if (data.UnavailableReason?.Length > 1024 || data.Objects is null)
                throw new SceneStop("scene_adapter_invalid", "The scene adapter returned invalid bounded metadata.");
            var objects = data.Objects.Take(256).Select(item =>
            {
                if (item is null) throw new SceneStop("scene_adapter_invalid", "Scene objects cannot be null.");
                foreach (var value in new[] { item.Id, item.Generation, item.Type }) RuntimeSceneRequest.ValidateIdentifier(value);
                if (item.PrivacyAutomationId is { } privacyId) RuntimeSceneRequest.ValidateIdentifier(privacyId);
                if (item.Label?.Length > 1024 || item.Relationships?.Count > 16 || item.Actions?.Count > 8 || item.Metadata?.Count > 8)
                    throw new SceneStop("scene_object_limit", "Objects allow a 1024-character label, 16 relationships, eight actions and eight metadata fields.");
                var relations = item.Relationships?.ToArray() ?? [];
                foreach (var relation in relations)
                {
                    if (relation is null) throw new SceneStop("scene_adapter_invalid", "Scene relationships cannot be null.");
                    foreach (var value in new[] { relation.Kind, relation.TargetId, relation.TargetGeneration }) RuntimeSceneRequest.ValidateIdentifier(value);
                }
                var actions = item.Actions?.ToArray() ?? [];
                foreach (var name in actions) RuntimeSceneRequest.ValidateIdentifier(name);
                var metadata = item.Metadata?.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
                if (metadata?.Any(pair => string.IsNullOrWhiteSpace(pair.Key) || pair.Key.Length > 64 || pair.Value is null || pair.Value.Length > 256) == true)
                    throw new SceneStop("scene_object_limit", "Metadata permits 64-character keys and 256-character values.");
                if (item.Bounds is { } bounds) _ = Bounds(new Rect(bounds.X, bounds.Y, bounds.Width, bounds.Height));
                return item with { Relationships = relations, Actions = actions, Metadata = metadata };
            }).ToArray();
            if (objects.Select(item => item.Id).Distinct(StringComparer.Ordinal).Count() != objects.Length)
                throw new SceneStop("scene_duplicate_identity", "Object ids must be unique within a scene snapshot.");
            data = data with { Objects = objects, IsComplete = data.IsComplete && data.Objects.Count <= 256 };
            var transform = canvas.TransformToVisual(top);
            var payload = JsonSerializer.SerializeToUtf8Bytes(new { target.SessionId, target.TopLevelId, target.NodeId, target.NodeGeneration,
                target.TopLevelGeneration, registration.Generation, identity, Data = data, CanvasTransform = transform, canvas.Bounds, top.RenderScaling });
            if (payload.Length > 1024 * 1024) throw new SceneStop("scene_capture_limit", "The bounded captured scene exceeds 1 MiB.");
            CheckCanvas();
            return (data, Convert.ToHexStringLower(SHA256.HashData(payload)), transform);
        }

        bool Protected(RuntimeSceneObject item)
        {
            if (policy is null) return false;
            return new[] { item.Id, item.Generation, item.Type }.Any(value => policy.SanitizeScalar(value) != value)
                || item.PrivacyAutomationId is { } id && (request.Policy!.ExcludedControlAutomationIds.Contains(id, StringComparer.Ordinal)
                    || request.Policy.RedactedAutomationIds.Contains(id, StringComparer.Ordinal));
        }

        string SafeText(string text, int limit)
        {
            var safe = policy?.SanitizeScalar(text) ?? text;
            if (safe.Length <= limit) return safe;
            if (char.IsHighSurrogate(safe[limit - 1])) limit--;
            return safe[..limit];
        }
        static NodeBounds Bounds(Rect rect) => double.IsFinite(rect.X) && double.IsFinite(rect.Y) && double.IsFinite(rect.Width) && double.IsFinite(rect.Height)
            && rect.Width >= 0 && rect.Height >= 0 ? new(rect.X, rect.Y, rect.Width, rect.Height)
            : throw new SceneStop("scene_geometry_invalid", "The adapter or transform produced invalid bounds.");
        static RuntimeSceneTransform Transform(Matrix matrix) => new(matrix.M11, matrix.M12, matrix.M21, matrix.M22, matrix.M31, matrix.M32);
    }

    private static CoreError SceneError(Exception exception) => exception is SceneStop stop
        ? new(stop.Code, stop.Message) : new("scene_adapter_failed", "The host scene adapter failed or returned invalid data; no hidden domain state was inferred.");
    private sealed class SceneStop(string code, string message) : Exception(message) { public string Code { get; } = code; }
    private sealed record RegisteredScene(WeakReference<Visual> Canvas, Func<AvaScopeSceneSnapshot> Capture, string Generation, string TopLevelId);
    private sealed record SceneRead(Visual Canvas, IReadOnlyList<RuntimeSceneObject> Objects, HashSet<string> VisibleObjectIds, RuntimeSceneSnapshot Snapshot);
}
