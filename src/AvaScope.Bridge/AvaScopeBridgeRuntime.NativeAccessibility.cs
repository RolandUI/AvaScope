using System.Diagnostics;
using System.Text.Json;
using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;
using AvaScope.Core;
using AvaScope.Protocol;

namespace AvaScope.Bridge;

public sealed partial class AvaScopeBridgeRuntime
{
    private readonly SemaphoreSlim _nativeAccessibilityGate = new(1);
    private readonly CancellationTokenSource _nativeAccessibilityClosed = new();

    public async Task<CoreResult<NativeAccessibilityAuditResponse>> AuditNativeAccessibilityAsync(NativeAccessibilityAuditRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (JsonSerializer.SerializeToUtf8Bytes(request).Length > 65536) return Fail("native_accessibility_request_limit", "Audit requests are limited to 64 KiB.");
        if (!await _nativeAccessibilityGate.WaitAsync(0, cancellationToken)) return Fail("native_accessibility_busy", "A native query is still finishing. No additional native worker was started.");
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _nativeAccessibilityClosed.Token);
        deadline.CancelAfter(request.TimeoutMs);
        var transferred = false;
        try
        {
            var snapshot = await Dispatcher.UIThread.InvokeAsync(() => Capture(), DispatcherPriority.Background, deadline.Token);
            var nativeToken = deadline.Token;
            var worker = Task.Run(() => NativeAccessibilityReader.Read(snapshot.Backend, snapshot.Handle, snapshot.Bounds, request, nativeToken), CancellationToken.None);
            // A platform call may finish after caller cancellation. Keep one in-flight worker and
            // release its gate only after native references have actually been disposed.
            transferred = true;
            _ = worker.ContinueWith(_ => _nativeAccessibilityGate.Release(), CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
            var native = await worker.WaitAsync(deadline.Token);
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (InspectionPolicyError(request.Target, request.Policy) is not null || FindTopLevel(request.Target.TopLevelId) is not { } current
                    || current != snapshot.Top || CreateObjectGeneration(current) != request.Target.TopLevelGeneration || PickGeometry(current).Revision != snapshot.GeometryRevision)
                    throw new AccessibilityStop("native_accessibility_target_changed", "The selected window/session/geometry changed during native observation; repeat with a current target.");
                foreach (var item in snapshot.Nodes)
                    if (!ResolveInputGenerationTarget(current, item.Target.TopLevelId, item.Target.NodeId, item.Target).Success)
                        throw new AccessibilityStop("native_accessibility_target_changed", "A sampled control was detached or recycled; no stale comparison is returned.");
            }, DispatcherPriority.Background, deadline.Token);
            var response = new NativeAccessibilityAuditResponse(native.Status == "observed" ? "compared" : native.Status, request.Target,
                Environment.ProcessId, snapshot.Time, snapshot.Nodes, snapshot.Truncated, native, NativeAccessibilityComparer.Compare(snapshot.Nodes, native, snapshot.Truncated),
                ["sequential_not_atomic", "only_realized_controls", "decorative_grouped_virtualized_and_native_children_may_differ",
                 "geometry_mapping_is_probable_not_identity", "names_bounded_to_256_characters", "no_text_value_password_or_document_content_read",
                 "findings_are_review_evidence_not_accessibility_conformance_certification"]);
            return request.Policy is null ? CoreResult<NativeAccessibilityAuditResponse>.Ok(response) : new RuntimeEvidencePolicyEnforcer(request.Policy).Sanitize(response);
        }
        catch (AccessibilityStop stop) { return Fail(stop.Code, stop.Message); }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        { return Fail("native_accessibility_timeout", "The bounded query expired or the session closed; no empty/healthy native-tree claim is made. A finishing native call is not duplicated."); }
        catch (Exception e) when (e is not OperationCanceledException and not OutOfMemoryException and not AccessViolationException)
        { return Fail("native_accessibility_unavailable", "The audit could not safely finish. Verify native accessibility availability and target identity; no application exception text is exported."); }
        finally { if (!transferred) _nativeAccessibilityGate.Release(); }

        (TopLevel Top, string Backend, nint Handle, NodeBounds? Bounds, string GeometryRevision, DateTimeOffset Time, IReadOnlyList<BridgeAccessibilityEvidence> Nodes, bool Truncated) Capture()
        {
            if (InspectionPolicyError(request.Target, request.Policy) is { } denied) throw new AccessibilityStop(denied.Code, denied.Message);
            if (FindTopLevel(request.Target.TopLevelId) is not { PlatformImpl: not null } top || CreateObjectGeneration(top) != request.Target.TopLevelGeneration)
                throw new AccessibilityStop("native_accessibility_stale", "Select the current registered top-level generation.");
            // Native grouping can hide ancestry/ids. An exclusion policy must never expose an
            // unmapped native name: conservatively refuse until that boundary can be proven.
            if (request.Policy is { } policy && (policy.ExcludedControlAutomationIds.Count > 0 || policy.RedactedAutomationIds.Count > 0))
                throw new AccessibilityStop("native_accessibility_policy_unavailable", "Native grouping cannot guarantee AutomationId subtree exclusion; this audit is unavailable under that policy. Bridge-only audits remain available.");
            var geometry = PickGeometry(top); var result = new List<BridgeAccessibilityEvidence>();
            var pending = new Stack<(Visual Node, int Depth)>(); var visited = new HashSet<Visual>(); var truncated = false; var scanned = 0;
            pending.Push((top, 0));
            foreach (var expected in request.Expectations.Reverse())
            {
                if (!ResolveInputGenerationTarget(top, expected.Target.TopLevelId, expected.Target.NodeId, expected.Target).Success
                    || FindNodeById(top, expected.Target.NodeId!) is not Visual visual)
                    throw new AccessibilityStop("native_accessibility_stale", "An expected control no longer matches its observed generation.");
                pending.Push((visual, -1)); // Sample every explicit expectation before traversing templates.
            }
            while (pending.TryPop(out var entry) && result.Count < request.MaxNodes && scanned++ < 2048)
            {
                deadline.Token.ThrowIfCancellationRequested();
                if (entry.Node is HighlightAdorner) continue;
                if (visited.Add(entry.Node) && entry.Node is Control control)
                {
                    var peer = ControlAutomationPeer.CreatePeerForElement(control); string? role = null, name = null, automationId = null; bool? isControl = null;
                    if (peer is not null)
                    {
                        role = peer.GetAutomationControlType().ToString(); isControl = peer.IsControlElement();
                        automationId = peer.GetAutomationId(); name = control is TextBox { PasswordChar: not '\0' } ? "[password control]" : peer.GetName();
                    }
                    var nodeTarget = CreateNodeTarget(request.Target.TopLevelId, TreeKinds.Visual, top, control);
                    var expected = request.Expectations.SingleOrDefault(e => e.Target.NodeId == nodeTarget.NodeId);
                    NodeBounds? desktop = null;
                    if (geometry.DesktopBounds is { } origin && GetTreeNodeBounds(top, control) is { } local)
                        desktop = new(origin.X + local.X * geometry.DesktopScaling, origin.Y + local.Y * geometry.DesktopScaling,
                            local.Width * geometry.DesktopScaling, local.Height * geometry.DesktopScaling);
                    result.Add(new(nodeTarget, control.GetType().FullName ?? control.GetType().Name, automationId is { Length: > 256 } ? null : automationId,
                        name is { Length: > 256 } ? name[..256] : name, role, isControl, control.IsEffectivelyVisible, control.IsEffectivelyEnabled, desktop, expected));
                }
                if (entry.Depth < 0) continue;
                var remaining = Math.Max(0, 2048 - scanned - pending.Count);
                var children = entry.Node.GetVisualChildren().Take(remaining + 1).ToArray();
                if (entry.Depth >= request.MaxDepth) { truncated |= children.Length > 0; continue; }
                truncated |= children.Length > remaining;
                foreach (var child in children.Take(remaining).Reverse()) pending.Push((child, entry.Depth + 1));
            }
            truncated |= pending.Count > 0 || scanned >= 2048;
            if (request.Expectations.Any(e => result.All(n => n.Target.NodeId != e.Target.NodeId)))
                throw new AccessibilityStop("native_accessibility_expected_node_limit", "The bounds excluded an explicit expectation; increase maxNodes or reduce expectations.");
            return (top, RuntimePlatformEvidence.Observe(top).Backend, top.TryGetPlatformHandle()?.Handle ?? 0, geometry.DesktopBounds, geometry.Revision, DateTimeOffset.UtcNow, result, truncated);
        }
        static CoreResult<NativeAccessibilityAuditResponse> Fail(string code, string message) => CoreResult<NativeAccessibilityAuditResponse>.Fail(new(code, message));
    }
    private sealed class AccessibilityStop(string code, string message) : Exception(message) { public string Code { get; } = code; }
}
