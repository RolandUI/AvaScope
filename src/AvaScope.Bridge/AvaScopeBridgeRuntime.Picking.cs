using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using AvaScope.Core;
using AvaScope.Protocol;

namespace AvaScope.Bridge;

public sealed partial class AvaScopeBridgeRuntime
{
    private readonly Dictionary<string, HighlightState> _highlights = new(StringComparer.Ordinal);

    public async Task<CoreResult<RuntimePickResponse>> PickNodeAsync(RuntimePickRequest request, CancellationToken cancellationToken = default)
    {
        if (JsonSerializer.SerializeToUtf8Bytes(request).Length > 65536) return PickFailure("pick_request_limit", "Picking requests are limited to 64 KiB.");
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken); deadline.CancelAfter(3000);
        try { return await Dispatcher.UIThread.InvokeAsync(() => PickNode(request), DispatcherPriority.Background, deadline.Token); }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) { return PickFailure("pick_timeout", "The UI dispatcher did not complete the bounded pick."); }
    }

    private CoreResult<RuntimePickResponse> PickNode(RuntimePickRequest request)
    {
        Dispatcher.UIThread.VerifyAccess();
        if (InspectionPolicyError(request.Target, request.Policy) is { } policyError) return CoreResult<RuntimePickResponse>.Fail(policyError);
        var top = FindTopLevel(request.Target.TopLevelId);
        if (top?.PlatformImpl is null || CreateObjectGeneration(top) != request.Target.TopLevelGeneration) return PickFailure("pick_stale", "The selected registered top-level is absent or changed generation.");
        if (ProtectedInspectionNode(top, request.Policy)) return PickFailure("pick_excluded", "Evidence policy protects the selected top-level.");
        var target = CreateTopLevelTarget(request.Target.TopLevelId, top);
        var geometry = PickGeometry(top);
        var diagnostics = new List<ProtocolError>
        { new("pick_current_sample", "This is the current Avalonia hit test. The geometry revision detects window/scale changes, not stale content in an older screenshot. Native or unrelated-process occlusion is not inferred.") };
        if (request.X is null) return Result("geometry", null, [], [], "not_queried", false);
        if (request.ExpectedGeometryRevision != geometry.Revision) return PickFailure("pick_geometry_changed", "Window position, size, visibility, layout or scale changed; obtain fresh geometry and coordinates.");
        if (!top.IsEffectivelyVisible || !top.IsMeasureValid || !top.IsArrangeValid) return PickFailure("pick_layout_unavailable", "The selected top-level is hidden or has invalid layout.");
        var point = request.CoordinateSpace switch
        {
            "top_level_dip" => new Point(request.X.Value, request.Y!.Value),
            "top_level_pixel" => new Point(request.X.Value / geometry.RenderScaling, request.Y!.Value / geometry.RenderScaling),
            "desktop" when geometry.DesktopBounds is { } bounds => new Point((request.X.Value - bounds.X) / geometry.DesktopScaling, (request.Y!.Value - bounds.Y) / geometry.DesktopScaling),
            _ => (Point?)null
        };
        if (point is null) return PickFailure("pick_coordinates_unsupported", "This backend has no validated desktop coordinate mapping; use top-level DIP/pixel coordinates.");
        if (!new Rect(top.ClientSize).Contains(point.Value)) return Result("outside", point, [], [], "outside_selected_client", false);
        var hit = top.InputHitTest(point.Value, enabledElementsOnly: false) as Visual;
        if (hit is null || hit is HighlightAdorner) return Result("no_hit", point, [], [], "native_occlusion_unverified", false);
        var path = hit.GetSelfAndVisualAncestors().Take(65).ToArray();
        if (path.Length > 64 || !path.Contains(top)) return PickFailure("pick_ancestry_limit", "The hit ancestry cannot be safely bounded within this top-level.");
        if (path.Any(node => ProtectedInspectionNode(node, request.Policy))) return Result("excluded", point, [], [], "policy_excluded", false);
        var safe = request.Policy is null ? null : new RuntimeEvidencePolicyEnforcer(request.Policy);
        string? Text(string? text)
        {
            text = text is null ? null : safe?.SanitizeScalar(text) ?? text;
            return text is { Length: > 256 } ? text[..256] : text;
        }
        var hitPath = path.TakeWhile(node => node != top).Append(top).Take(request.MaxPath).Select(node => new RuntimePickedNode(
            CreateNodeTarget(request.Target.TopLevelId, TreeKinds.Visual, top, node), node.GetType().FullName ?? node.GetType().Name,
            Text(GetName(node)), Text(GetAutomationId(node)), Text(GetText(node)), GetTreeNodeBounds(top, node),
            (node as InputElement)?.IsEffectivelyEnabled, (node as InputElement)?.IsHitTestVisible)).ToArray();
        var related = new List<RuntimeTargetContext>(); var occlusion = "native_occlusion_unverified";
        if (FindModalBlocker(top) is { } modal)
        {
            occlusion = "modal_blocks_selected_owner";
            if (FindTopLevel(InspectableTopLevel.CreateId(modal)) == modal && !ProtectedInspectionNode(modal, request.Policy))
                related.Add(CreateTopLevelTarget(InspectableTopLevel.CreateId(modal), modal));
        }
        if (geometry.DesktopBounds is not null)
        {
            var screenPoint = top.PointToScreen(point.Value);
            var others = DiscoverTopLevels().Select(summary => FindTopLevel(summary.Id)).Where(other => other is not null && other != top && other.IsVisible).Take(33).ToArray();
            if (others.Length > 32) diagnostics.Add(new("pick_related_window_limit", "Only the first 32 registered top-levels were checked for coordinate overlap."));
            foreach (var other in others.Take(32).OfType<TopLevel>())
            {
                if (ProtectedInspectionNode(other, request.Policy)) continue;
                try
                {
                    if (new Rect(other.ClientSize).Contains(other.PointToClient(screenPoint)))
                        related.Add(CreateTopLevelTarget(InspectableTopLevel.CreateId(other), other));
                }
                catch (NotSupportedException) { }
            }
            if (related.Count > 0 && occlusion != "modal_blocks_selected_owner") occlusion = "registered_top_level_overlap; z_order_unverified";
        }
        // Native popups may have their own unregistered root. Do not export a target claiming it is
        // part of the selected window; explicitly selecting a registered popup is the safe route.
        var popupScope = top.GetVisualDescendants().Take(4097).ToArray();
        var popupCount = popupScope.OfType<Popup>().Count(popup => popup.IsOpen && !popup.IsUsingOverlayLayer);
        if (popupScope.Length > 4096) diagnostics.Add(new("pick_popup_scan_limit", "Popup inspection was bounded to 4096 visuals; absence of a popup was not proven."));
        if (popupCount > 0)
        { diagnostics.Add(new("pick_native_popup_present", "This window has an open native popup. Its root is a separate hit-test scope; select/register that top-level explicitly. The owner hit path does not prove desktop delivery.")); occlusion = "native_popup_present; selected_root_only"; }
        if (PickGeometry(top).Revision != geometry.Revision) return PickFailure("pick_geometry_changed", "Geometry changed during hit testing; no current target is claimed.");
        return Result("picked", point, hitPath, related.DistinctBy(item => item.TopLevelId).Take(16).ToArray(), occlusion, path.Length > request.MaxPath || related.Count > 16 || popupScope.Length > 4096);

        CoreResult<RuntimePickResponse> Result(string status, Point? p, IReadOnlyList<RuntimePickedNode> nodes, IReadOnlyList<RuntimeTargetContext> windows, string occlusion, bool truncated)
        {
            var response = new RuntimePickResponse(status, target, geometry, p is { } value ? new(value.X, value.Y) : null, nodes, windows, occlusion, truncated, diagnostics);
            return request.Policy is null ? CoreResult<RuntimePickResponse>.Ok(response) : new RuntimeEvidencePolicyEnforcer(request.Policy).Sanitize(response);
        }
    }

    private RuntimePickGeometry PickGeometry(TopLevel top)
    {
        var backend = RuntimePlatformEvidence.Observe(top); NodeBounds? desktop = null; string? units = null;
        var desktopScale = top is WindowBase window ? window.DesktopScaling : top.RenderScaling;
        if (top is WindowBase && backend.Backend is "win32" or "x11" or "macos")
        {
            var origin = top.PointToScreen(default);
            desktop = new(origin.X, origin.Y, top.ClientSize.Width * desktopScale, top.ClientSize.Height * desktopScale);
            units = backend.Backend == "macos" ? "cocoa_desktop_points" : "physical_desktop_pixels";
        }
        var pixels = GetPixelSize(top);
        var revision = Convert.ToHexStringLower(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new
        { SessionId, Generation = CreateObjectGeneration(top), top.ClientSize, top.RenderScaling, desktopScale, desktop, top.IsVisible, top.IsMeasureValid, top.IsArrangeValid, Handle = top.TryGetPlatformHandle()?.Handle.ToInt64() })));
        return new(revision, new(top.ClientSize.Width, top.ClientSize.Height), new(pixels.Width, pixels.Height), top.RenderScaling, desktopScale, desktop, units, DateTimeOffset.UtcNow);
    }

    public async Task<CoreResult<RuntimeHighlightResponse>> HighlightAsync(RuntimeHighlightRequest request, CancellationToken cancellationToken = default)
    {
        if (JsonSerializer.SerializeToUtf8Bytes(request).Length > 65536) return CoreResult<RuntimeHighlightResponse>.Fail(new("highlight_request_limit", "Highlight requests are limited to 64 KiB."));
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken); deadline.CancelAfter(3000);
        try { return await Dispatcher.UIThread.InvokeAsync(() => Highlight(request), DispatcherPriority.Background, deadline.Token); }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        { return CoreResult<RuntimeHighlightResponse>.Fail(new("highlight_timeout", "The UI dispatcher did not complete the bounded highlight request.")); }
    }

    private CoreResult<RuntimeHighlightResponse> Highlight(RuntimeHighlightRequest request)
    {
        Dispatcher.UIThread.VerifyAccess();
        if (InspectionPolicyError(request.Target, request.Policy) is { } error) return CoreResult<RuntimeHighlightResponse>.Fail(error);
        var top = FindTopLevel(request.Target.TopLevelId);
        if (top?.PlatformImpl is null || CreateObjectGeneration(top) != request.Target.TopLevelGeneration) return Fail("highlight_stale", "The registered top-level is absent or changed generation.");
        if (ProtectedInspectionNode(top, request.Policy)) return Fail("highlight_excluded", "The selected top-level is protected by policy.");
        if (request.Action == "clear") { ClearHighlights(request.Target.TopLevelId); return Result("cleared", null); }
        if (request.Action == "inspect")
        {
            if (_highlights.TryGetValue(request.Target.TopLevelId, out var protectedState) && ProtectedInspectionNode(protectedState.Node, request.Policy))
                return Fail("highlight_excluded", "Evidence policy protects the active highlight target.");
            if (_highlights.TryGetValue(request.Target.TopLevelId, out var current) && !current.Valid()) ClearHighlights(request.Target.TopLevelId);
            return Result(_highlights.ContainsKey(request.Target.TopLevelId) ? "active" : "absent", _highlights.GetValueOrDefault(request.Target.TopLevelId));
        }
        var resolved = ResolveInputGenerationTarget(top, request.Target.TopLevelId, request.Target.NodeId, request.Target);
        if (!resolved.Success) return CoreResult<RuntimeHighlightResponse>.Fail(resolved.Error!);
        if (FindNodeById(top, request.Target.NodeId!) is not Visual node || node is HighlightAdorner) return Fail("highlight_node_unavailable", "The visual target is not available.");
        if (ProtectedInspectionNode(node, request.Policy)) return Fail("highlight_excluded", "The target or its ancestor is protected by policy.");
        if (!node.IsEffectivelyVisible || !top.IsVisible || node is Control { IsArrangeValid: false } || node.Bounds.Width <= 2 || node.Bounds.Height <= 2)
            return Fail("highlight_geometry_unavailable", "The target has no current visible arranged bounds.");
        if (AdornerLayer.GetAdornerLayer(node) is not { } layer) return Fail("highlight_layer_unavailable", "This target has no public Avalonia adorner layer; its content is not reparented or replaced.");
        if (_highlights.Count >= 8 && !_highlights.ContainsKey(request.Target.TopLevelId)) return Fail("highlight_limit", "At most eight top-level highlights may be active.");
        ClearHighlights(request.Target.TopLevelId);
        var overlay = new HighlightAdorner(Color.Parse(request.Color)) { IsHitTestVisible = false, Focusable = false };
        var id = Guid.NewGuid().ToString("N"); var expiry = DateTimeOffset.UtcNow.AddMilliseconds(request.LifetimeMs); var start = Stopwatch.GetTimestamp();
        var identity = QueryIdentity(node, false);
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(50) };
        bool Valid() => Stopwatch.GetElapsedTime(start).TotalMilliseconds < request.LifetimeMs && FindTopLevel(request.Target.TopLevelId) == top
            && top.PlatformImpl is not null && node.IsEffectivelyVisible && TopLevel.GetTopLevel(node) == top
            && QueryIdentity(node, false) == identity && !ProtectedInspectionNode(node, request.Policy);
        EventHandler tick = (_, _) => { if (!Valid()) ClearHighlights(request.Target.TopLevelId); };
        EventHandler<VisualTreeAttachmentEventArgs> detached = (_, _) => ClearHighlights(request.Target.TopLevelId);
        try
        {
            AdornerLayer.SetAdornedElement(overlay, node); AdornerLayer.SetIsClipEnabled(overlay, true);
            layer.Children.Add(overlay);
            var state = new HighlightState(id, request.Target, node, layer, overlay, expiry, timer, Valid,
                () => { timer.Tick -= tick; node.DetachedFromVisualTree -= detached; });
            _highlights.Add(request.Target.TopLevelId, state);
            timer.Tick += tick; node.DetachedFromVisualTree += detached; timer.Start();
            return Result("active", state);
        }
        catch (Exception exception) when (exception is not OutOfMemoryException and not AccessViolationException)
        {
            timer.Stop(); timer.Tick -= tick; node.DetachedFromVisualTree -= detached;
            ClearHighlights(request.Target.TopLevelId); AdornerLayer.SetAdornedElement(overlay, null); layer.Children.Remove(overlay);
            return Fail("highlight_failed", "The temporary adorner could not be installed; all owned highlight resources were removed.");
        }
        CoreResult<RuntimeHighlightResponse> Result(string status, HighlightState? state)
        {
            var response = new RuntimeHighlightResponse(status, state?.Id, state?.Target ?? request.Target,
                state is null ? null : GetTreeNodeBounds(top, state.Node), state?.ExpiresAt, true, true, []);
            var result = request.Policy is null ? CoreResult<RuntimeHighlightResponse>.Ok(response) : new RuntimeEvidencePolicyEnforcer(request.Policy).Sanitize(response);
            if (!result.Success && request.Action == "show") ClearHighlights(request.Target.TopLevelId);
            return result;
        }
        static CoreResult<RuntimeHighlightResponse> Fail(string code, string message) => CoreResult<RuntimeHighlightResponse>.Fail(new(code, message));
    }

    private CoreError? InspectionPolicyError(RuntimeTargetContext target, RuntimeEvidencePolicy? policy)
    {
        if (target.SessionId != SessionId || _sessionRegistry.Get(SessionId).Value?.State is not SessionLifecycleState.Active)
            return new("inspection_session_mismatch", "The target does not address this active bridge session.");
        if (policy is null) return null;
        if (policy.AuthorizedSessionIds.Count > 0 && !policy.AuthorizedSessionIds.Contains(SessionId.Value, StringComparer.Ordinal)
            || policy.AuthorizedProcessIds.Count > 0 && !policy.AuthorizedProcessIds.Contains(Environment.ProcessId))
            return new("inspection_policy_denied", "The session/process is outside the evidence policy.");
        return new RuntimeEvidencePolicyEnforcer(policy).AuthorizeAction(SemanticWorkflowActions.Inspect, null).Error;
    }
    private static bool ProtectedInspectionNode(Visual node, RuntimeEvidencePolicy? policy)
    {
        if (policy is null) return false;
        var ancestors = node.GetSelfAndVisualAncestors().Take(65).ToArray();
        return ancestors.Length > 64 || ancestors.Any(ancestor => policy.ExcludedControlAutomationIds.Contains(GetAutomationId(ancestor), StringComparer.Ordinal)
            || policy.RedactedAutomationIds.Contains(GetAutomationId(ancestor), StringComparer.Ordinal));
    }
    private void ClearHighlights(string? topLevelId = null)
    {
        if (!Dispatcher.UIThread.CheckAccess())
        { Dispatcher.UIThread.InvokeAsync(() => ClearHighlights(topLevelId), DispatcherPriority.Send).GetTask().GetAwaiter().GetResult(); return; }
        foreach (var entry in _highlights.Where(pair => topLevelId is null || pair.Key == topLevelId).ToArray())
        {
            _highlights.Remove(entry.Key); var state = entry.Value;
            state.Timer.Stop(); state.Unsubscribe(); AdornerLayer.SetAdornedElement(state.Overlay, null); state.Layer.Children.Remove(state.Overlay);
        }
    }
    private sealed record HighlightState(string Id, RuntimeTargetContext Target, Visual Node, AdornerLayer Layer, HighlightAdorner Overlay,
        DateTimeOffset ExpiresAt, DispatcherTimer Timer, Func<bool> Valid, Action Unsubscribe);
    private sealed class HighlightAdorner(Color color) : Control
    {
        public override void Render(DrawingContext context)
        {
            base.Render(context);
            if (Bounds.Width > 0 && Bounds.Height > 0)
                context.DrawRectangle(null, new Pen(new SolidColorBrush(color), 2), new Rect(Bounds.Size).Deflate(1));
        }
    }
    private static CoreResult<RuntimePickResponse> PickFailure(string code, string message) => CoreResult<RuntimePickResponse>.Fail(new(code, message));
}
