using System.Diagnostics;
using System.Text.Json;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using AvaScope.Core;
using AvaScope.Protocol;

namespace AvaScope.Bridge;

public sealed partial class AvaScopeBridgeRuntime
{
    public async Task<CoreResult<RuntimeFocusSnapshot>> InspectFocusAsync(RuntimeFocusInspectionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return await Dispatcher.UIThread.InvokeAsync(() => InspectFocus(request), DispatcherPriority.Background, cancellationToken);
    }

    private CoreResult<RuntimeFocusSnapshot> InspectFocus(RuntimeFocusInspectionRequest request)
    {
        Dispatcher.UIThread.VerifyAccess();
        if (request.SessionId != SessionId) return Fail("focus_session_mismatch", "Select this bridge session explicitly.");
        var policy = request.Policy is null ? null : new RuntimeEvidencePolicyEnforcer(request.Policy);
        if (policy?.AuthorizeAction(SemanticWorkflowActions.Inspect, null) is { Success: false } denied)
            return CoreResult<RuntimeFocusSnapshot>.Fail(denied.Error!);
        var top = FindTopLevel(request.TopLevelId);
        if (top is null) return TopLevelNotFound<RuntimeFocusSnapshot>(request.TopLevelId);
        try
        {
            var reasons = new HashSet<string>(StringComparer.Ordinal);
            var focused = top.FocusManager?.GetFocusedElement() as InputElement;
            InputElement? reference = focused is not null && TopLevel.GetTopLevel(focused) == top ? focused : null;
            if (request.Target is { } target)
            {
                var resolved = ResolveInputGenerationTarget(top, request.TopLevelId, target.NodeId, target);
                if (!resolved.Success) return CoreResult<RuntimeFocusSnapshot>.Fail(resolved.Error!);
                reference = FindNodeById(top, target.NodeId!) as InputElement;
                if (reference is null) return Fail("focus_reference_unavailable", "The selected reference is not an input element.");
                if (Excluded(reference)) return Fail("focus_target_excluded", "The selected focus reference is excluded by policy.");
            }
            var focusStatus = focused is null ? "none" : Excluded(focused) ? "excluded_by_policy"
                : TopLevel.GetTopLevel(focused) == top ? "selected_window" : RegisteredTop(focused) is not null ? "other_registered_window" : "unregistered_scope";
            var focusedEntry = focusStatus is "selected_window" or "other_registered_window" ? Entry(focused!) : null;
            if (reference is not null && Excluded(reference)) reference = null;
            var chain = reference?.GetSelfAndVisualAncestors().Take(33).ToArray() ?? [];
            if (chain.Length > 32) reasons.Add("ancestor_limit");
            var ancestors = chain.Take(32).OfType<InputElement>().Where(node => !Excluded(node)).Select(Entry).Where(node => node is not null).Cast<RuntimeFocusNode>().ToArray();
            var candidates = new List<RuntimeFocusNode>();
            var nodes = new List<Visual>();
            var pending = new Stack<(Visual Node, int Depth)>();
            pending.Push((top, 0));
            var started = Stopwatch.GetTimestamp();
            while (pending.TryPop(out var item))
            {
                if (nodes.Count == request.MaxNodes) { reasons.Add("node_limit"); break; }
                if (Stopwatch.GetElapsedTime(started) > TimeSpan.FromSeconds(2)) { reasons.Add("analysis_budget"); break; }
                nodes.Add(item.Node);
                if (Excluded(item.Node)) { reasons.Add("policy_exclusions"); continue; }
                if (item.Node is InputElement { Focusable: true } input)
                {
                    if (candidates.Count < request.MaxCandidates && Entry(input) is { } entry) candidates.Add(entry);
                    else reasons.Add("candidate_limit");
                }
                var children = item.Node.GetVisualChildren().Take(request.MaxNodes + 1).ToArray();
                if (item.Depth == 32) { if (children.Length > 0) reasons.Add("depth_limit"); continue; }
                if (pending.Count + children.Length > request.MaxNodes) { reasons.Add("node_limit"); break; }
                for (var index = children.Length - 1; index >= 0; index--) pending.Push((children[index], item.Depth + 1));
            }
            var modal = FindModalBlocker(top);
            if (top is Window { OwnedWindows.Count: > 64 }) reasons.Add("modal_limit");
            var modalTarget = modal is not null && FindTopLevel(InspectableTopLevel.CreateId(modal)) == modal && !Excluded(modal)
                ? CreateTopLevelTarget(InspectableTopLevel.CreateId(modal), modal) : null;
            var customNavigation = nodes.Any(node => node is ICustomKeyboardNavigation);
            var predictions = new[] { Predict("next", NavigationDirection.Next), Predict("previous", NavigationDirection.Previous) };
            if (top.FocusManager?.GetFocusedElement() != focused || FindTopLevel(request.TopLevelId) != top)
                return Fail("focus_changed_during_inspection", "Focus or the selected window changed while reading navigation evidence. Observe again.");
            RuntimeFocusSnapshot Response() => new(SessionId, request.TopLevelId, RuntimePlatformEvidence.Observe(top), (top as Window)?.IsActive,
                NativeWindowInput.ObserveFocus(top), focusStatus, focusedEntry, reference is null ? null : Entry(reference), ancestors,
                modalTarget, predictions, candidates.ToArray(), nodes.Count, reasons.Order(StringComparer.Ordinal).ToArray(), DateTimeOffset.UtcNow);
            var response = Response();
            while (candidates.Count > 0 && JsonSerializer.SerializeToUtf8Bytes(response).Length > 65536)
            { candidates.RemoveAt(candidates.Count - 1); reasons.Add("response_byte_limit"); response = Response(); }
            return policy is null ? CoreResult<RuntimeFocusSnapshot>.Ok(response) : policy.Sanitize(response);

            RuntimeFocusPrediction Predict(string direction, NavigationDirection navigation)
            {
                string? unavailable = reference is null ? "no_reference_in_selected_window" : modal is not null ? "owned_modal_window"
                    : reasons.Any(reason => reason != "candidate_limit") ? "incomplete_scope" : customNavigation ? "custom_navigation_requires_probe" : null;
                if (unavailable is not null) return new(direction, "unknown", null, "public_metadata", unavailable);
                try
                {
                    var next = top.FocusManager?.FindNextElement(navigation, new() { FocusedElement = reference });
                    if (next is Visual visual && (TopLevel.GetTopLevel(visual) != top || Excluded(visual)))
                        return new(direction, "unknown", null, "public_focus_manager_prediction", "candidate_outside_selected_or_allowed_scope");
                    if (next is InputElement input && (!input.Focusable || !input.IsEffectivelyEnabled || !input.IsEffectivelyVisible || !KeyboardNavigation.GetIsTabStop(input)))
                        return new(direction, "unavailable_candidate", CreateNodeTarget(request.TopLevelId, TreeKinds.Visual, top, input),
                            "public_focus_manager_prediction", "The public API candidate is not currently focusable/enabled/visible/a tab stop. Use an explicit probe; no alternative route is inferred.");
                    return new(direction, next is Visual ? "predicted" : "no_candidate", next is Visual found
                        ? CreateNodeTarget(request.TopLevelId, TreeKinds.Visual, top, found) : null,
                        "public_focus_manager_prediction", "Not an observed Tab result; event handlers, custom overrides and IME can change routing.");
                }
                catch (Exception exception) when (exception is not OutOfMemoryException and not AccessViolationException)
                { return new(direction, "unknown", null, "public_focus_manager_prediction", "navigation_provider_unavailable"); }
            }
        }
        catch (Exception exception) when (exception is not OutOfMemoryException and not AccessViolationException)
        { return Fail("focus_inspection_unavailable", "A public focus/navigation property could not be observed safely."); }

        bool Excluded(Visual node) => TableExcluded(node, request.Policy) || request.Policy is { } settings
            && node.GetSelfAndVisualAncestors().Take(65).Any(ancestor => settings.RedactedAutomationIds.Contains(GetAutomationId(ancestor), StringComparer.Ordinal));
        TopLevel? RegisteredTop(Visual node) => TopLevel.GetTopLevel(node) is { } owner && FindTopLevel(InspectableTopLevel.CreateId(owner)) == owner ? owner : null;
        RuntimeFocusNode? Entry(InputElement node)
        {
            var owner = RegisteredTop(node);
            if (owner is null || Excluded(node)) return null;
            var label = node is Control control ? AutomationProperties.GetName(control) ?? control.Name : node.Name;
            label = label is null ? null : policy?.SanitizeScalar(label) ?? label;
            var reasons = new List<string>();
            if (!node.Focusable) reasons.Add("not_focusable");
            if (!node.IsEffectivelyEnabled) reasons.Add("disabled");
            if (!node.IsEffectivelyVisible) reasons.Add("hidden");
            if (!KeyboardNavigation.GetIsTabStop(node)) reasons.Add("not_a_tab_stop");
            var mode = KeyboardNavigation.GetTabNavigation(node).ToString();
            if (mode is "Cycle" or "Contained") reasons.Add("tab_navigation_boundary:" + mode);
            if (mode == "None") reasons.Add("descendant_tab_navigation_disabled");
            if (node is ICustomKeyboardNavigation) reasons.Add("custom_navigation_requires_probe");
            return new(CreateNodeTarget(InspectableTopLevel.CreateId(owner), TreeKinds.Visual, owner, node), label is { Length: > 128 } ? label[..128] : label,
                node.GetType().Name is { Length: > 128 } type ? type[..128] : node.GetType().Name,
                node.IsFocused, node.IsKeyboardFocusWithin, node.Focusable, node.IsEffectivelyEnabled, node.IsEffectivelyVisible,
                KeyboardNavigation.GetIsTabStop(node), KeyboardNavigation.GetTabIndex(node), mode, node is IFocusScope, reasons);
        }
        static CoreResult<RuntimeFocusSnapshot> Fail(string code, string message) => CoreResult<RuntimeFocusSnapshot>.Fail(new(code, message));
    }

    public async Task<CoreResult<RuntimeFocusProbeResponse>> ProbeFocusAsync(RuntimeFocusProbeRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var target = request.Target;
        var inspection = new RuntimeFocusInspectionRequest(target.SessionId, target.TopLevelId, target, maxCandidates: 8, policy: request.Policy);
        var policy = request.Policy is null ? null : new RuntimeEvidencePolicyEnforcer(request.Policy);
        if (policy?.AuthorizeAction(InputActions.KeySequence, null) is { Success: false } denied)
            return CoreResult<RuntimeFocusProbeResponse>.Fail(denied.Error!);
        var before = await InspectFocusAsync(inspection, cancellationToken);
        if (!before.Success) return CoreResult<RuntimeFocusProbeResponse>.Fail(before.Error!);
        if (before.Value!.FocusStatus != "selected_window" || before.Value.Focused?.Target.NodeGeneration != target.NodeGeneration)
            return CoreResult<RuntimeFocusProbeResponse>.Fail(new("focus_probe_start_changed", "The pinned target no longer holds focus. Observe again; no focus or input was dispatched."));
        if (request.Strategy == "native" && before.Value.NativeFocus.State != "focused")
            return CoreResult<RuntimeFocusProbeResponse>.Fail(new("focus_probe_native_unconfirmed", "Native focus on the selected window is not confirmed. No activation or input was dispatched."));
        var input = await InputAsync(target.TopLevelId, InputActions.KeySequence, targetNodeId: target.NodeId, inputTarget: target,
            execution: new() { Strategy = request.Strategy, RequireCurrentFocus = true, IntervalMs = 0,
                Keys = [new("Tab", request.Direction == "previous" ? "Shift" : null)] }, cancellationToken: cancellationToken);
        // No automatic rollback: application Tab/focus handlers may validate, commit, or navigate.
        if (request.SettleMs > 0) await Task.Delay(request.SettleMs, cancellationToken);
        var after = await InspectFocusAsync(new(target.SessionId, target.TopLevelId, maxCandidates: 8, policy: request.Policy), cancellationToken);
        var diagnostics = new List<ProtocolError>();
        if (!input.Success) diagnostics.Add(new(input.Error!.Code, input.Error.Message, input.Error.Details));
        if (!after.Success) diagnostics.Add(new(after.Error!.Code, after.Error.Message));
        var response = new RuntimeFocusProbeResponse(diagnostics.Count == 0 ? "observed" : "failed", request.Direction,
            after.Success && (before.Value.Focused?.Target.NodeGeneration != after.Value!.Focused?.Target.NodeGeneration
                || before.Value.FocusStatus != after.Value.FocusStatus), before.Value, after.Value, input.Value, diagnostics);
        return policy is null ? CoreResult<RuntimeFocusProbeResponse>.Ok(response) : policy.Sanitize(response);
    }
}
