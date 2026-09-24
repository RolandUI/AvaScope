using System.Security.Cryptography;
using System.Text;
using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using AvaScope.Core;
using AvaScope.Protocol;

namespace AvaScope.Bridge;

public sealed partial class AvaScopeBridgeRuntime
{
    public async Task<CoreResult<RuntimeActionExplanation>> ExplainActionAsync(RuntimeActionExplanationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.SessionId != SessionId)
            return CoreResult<RuntimeActionExplanation>.Fail(new("action_session_mismatch", "Select this bridge session explicitly."));
        var result = await Dispatcher.UIThread.InvokeAsync(() => ExplainAction(request), DispatcherPriority.Background, cancellationToken);
        if (request.Policy is null) return result;
        var policy = new RuntimeEvidencePolicyEnforcer(request.Policy);
        if (result.Success) return policy.Sanitize(result.Value!);
        var error = policy.Sanitize(result.Error!);
        return CoreResult<RuntimeActionExplanation>.Fail(error.Success ? error.Value! : error.Error!);
    }

    private CoreResult<RuntimeActionExplanation> ExplainAction(RuntimeActionExplanationRequest request)
    {
        Dispatcher.UIThread.VerifyAccess();
        var top = FindTopLevel(request.TopLevelId);
        if (top is null) return TopLevelNotFound<RuntimeActionExplanation>(request.TopLevelId);
        var resolved = ResolveInputGenerationTarget(top, request.TopLevelId, request.NodeId, request.Target);
        if (!resolved.Success) return CoreResult<RuntimeActionExplanation>.Fail(resolved.Error!);
        if (FindNodeById(top, resolved.Value!.NodeId!) is not Visual node)
            return CoreResult<RuntimeActionExplanation>.Fail(new(BridgeErrorCodes.NodeNotFound, "Action explanation requires a live visual node."));
        bool Excluded(Visual visual)
        {
            if (request.Policy is not { ExcludedControlAutomationIds.Count: > 0 } policy) return false;
            var chain = visual.GetSelfAndVisualAncestors().Take(34).ToArray();
            return chain.Length > 33 || chain.Any(ancestor => policy.ExcludedControlAutomationIds.Contains(GetAutomationId(ancestor), StringComparer.Ordinal));
        }
        if (Excluded(node))
            return CoreResult<RuntimeActionExplanation>.Fail(new("action_target_excluded", "The selected target is excluded by the evidence policy."));
        var reasons = new List<RuntimeActionReason>();
        var blocked = false;
        var truncated = false;
        var evidencePolicy = request.Policy is null ? null : new RuntimeEvidencePolicyEnforcer(request.Policy);
        string? Limit(string? value)
        {
            var safe = value is null ? null : evidencePolicy?.SanitizeScalar(value) ?? value;
            if (safe is not { Length: > 512 }) return safe;
            truncated = true;
            return safe[..512];
        }
        void Add(string code, string certainty, bool blocks, string message, string source, Visual? related, string next)
        {
            blocked |= blocks;
            if (related is not null && Excluded(related)) { truncated = true; return; }
            if (reasons.Count == request.MaxReasons) { truncated = true; return; }
            var relatedTop = related is null ? null : TopLevel.GetTopLevel(related);
            reasons.Add(new(code, certainty, blocks, Limit(message)!, source,
                related is not null && relatedTop is not null ? CreateNodeTarget(InspectableTopLevel.CreateId(relatedTop), TreeKinds.Visual, relatedTop, related) : null,
                related is null ? null : Limit(GetAutomationId(related)), next));
        }
        var pointer = request.Action is InputActions.Click or InputActions.PointerMove or InputActions.PointerDown or InputActions.PointerUp
            or InputActions.Drag or InputActions.Swipe or InputActions.LongPress or InputActions.PressAndHold;
        var ancestors = node.GetSelfAndVisualAncestors().Take(33).ToArray();
        if (ancestors.Length > 32)
        {
            truncated = true;
            Add("ancestry_limit", "unknown", true, "Ancestry exceeds the 32-node analysis limit.", "bounded_visual_tree", node, "Select a shallower target or inspect its ancestry.");
        }
        foreach (var ancestor in ancestors.Take(32))
        {
            if (!ancestor.IsVisible)
                Add("hidden_ancestor", "proven", true, "This target or ancestor is not visible.", "avalonia_public_visibility", ancestor, "Inspect the hidden ancestor and the application's intended reveal action.");
            if (ancestor is InputElement input && !input.IsEnabled)
                Add("disabled_ancestor", "proven", true, "This target or ancestor has IsEnabled=false.", "avalonia_public_enabled", ancestor, "Inspect the disabled element and its command or declared reason.");
            if (pointer && ancestor is InputElement { IsHitTestVisible: false })
                Add("hit_test_disabled", "proven", true, "This target or ancestor disables hit testing.", "avalonia_public_hit_test", ancestor, "Choose an intended interactive target; do not force hit testing.");
        }
        if (node is InputElement { IsEffectivelyEnabled: false } && !reasons.Any(reason => reason.Code == "disabled_ancestor"))
            Add("effectively_disabled", "proven", true, "The target is effectively disabled; a public local IsEnabled value alone does not explain why.", "avalonia_public_enabled", node, "Inspect command executability and app-declared reasons.");
        if (FindModalBlocker(top) is { } dialog)
            Add("modal_owner_blocked", "proven", true, "A visible modal child blocks this owner window.", "avalonia_window_owner_and_is_dialog", dialog, "Inspect and complete the modal dialog before returning to its owner.");
        if (top is Window { OwnedWindows.Count: > 64 })
        {
            truncated = true;
            Add("owned_window_limit", "unknown", true, "The owner has more than 64 children; modal analysis is incomplete.", "bounded_owned_windows", top, "Select the intended child window explicitly before acting.");
        }

        if (request.Action is InputActions.Click or InputActions.Invoke)
        {
            ComputedPropertyValue? command;
            try { command = GetCommandState(node); }
            catch (Exception exception) when (exception is not OutOfMemoryException and not AccessViolationException) { command = null; }
            if (command?.Value == "false")
                Add("command_denied", "proven", true, "Command.CanExecute returned false. Its business reason is not exposed by ICommand.", "avalonia_public_command", node, "Inspect related validation and any app-declared reason; do not infer a business cause.");
            else if (command is null || command.Value == "not_available")
                Add("command_reason_unavailable", "unknown", false, "No command executability reason is available for this target.", "avalonia_public_command", node, "Use observable state or an explicit application evidence provider.");
        }

        var (context, providerFailed) = ReadActionContext(node, request.Action);
        if (providerFailed)
            Add("app_context_unavailable", "unknown", true, "The application evidence provider failed; no reason or activation point was substituted.", "app_context_provider", node, "Fix the read-only application evidence provider before retrying.");
        if (context?.CanExecute == false)
            Add("app_declared_blocker", "app_declared", true, "The application explicitly declares this action unavailable.", "app_context_provider", node, "Inspect the app-declared reason and its linked fields.");
        var businessReasons = context?.BusinessReasons ?? [];
        if (businessReasons.Count > 8) truncated = true;
        foreach (var reason in businessReasons.Take(8))
            if (!string.IsNullOrWhiteSpace(reason))
                Add("app_business_reason", "app_declared", false, reason, "app_context_provider", node, "Follow the application's stated precondition; AvaScope has not proven its causal logic.");

        var related = (context?.RelatedValidationTargets ?? []).Take(17).ToArray();
        if (related.Length > 16) truncated = true;
        var fields = related.Take(16).Where(field => field is not null && TopLevel.GetTopLevel(field) == top).ToHashSet();
        if (node is Control self) fields.Add(self);
        foreach (var field in fields) Validation(field, field == node ? "target" : "app_declared_relation");
        // Nearby errors are observations only. Never turn an unrelated field error into a Save business rule.
        var sampled = top.GetVisualDescendants().Take(257).ToArray();
        if (sampled.Length > 256) truncated = true;
        foreach (var field in sampled.Take(256).OfType<Control>().Where(field => !fields.Contains(field))) Validation(field, "same_window_unproven");

        void Validation(Control field, string relation)
        {
            if (!DataValidationErrors.GetHasErrors(field)) return;
            var errors = (DataValidationErrors.GetErrors(field) ?? []).Cast<object?>().Take(5).ToArray();
            if (errors.Length > 4) truncated = true;
            var messages = errors.Take(4).Select(error => Limit(FormatValidationError(error))).Where(text => !string.IsNullOrWhiteSpace(text));
            Add("validation_error", "correlated", false, relation + ": " + string.Join("; ", messages),
                "avalonia_public_data_validation_errors", field, "Inspect this field; its validation error alone does not prove why the action is blocked.");
        }

        var point = ResolveActivationPoint(top, request.TopLevelId, node, request.X, request.Y, context, providerFailed);
        if (point.HitTarget is { NodeId: { } hitId } && FindNodeById(top, hitId) is Visual hitVisual && Excluded(hitVisual))
            point = point with { HitTarget = null, Reason = "The hit target is excluded by the evidence policy." };
        if (point.Status != "valid")
            Add("activation_point_" + point.Status, point.Status == "unavailable" ? "unknown" : "proven", pointer,
                point.Reason ?? "The proposed activation point is not usable.", point.Source, node, "Reobserve geometry, clipping and the hit target before choosing another point.");
        if (point.Status == "obstructed" && point.HitTarget is { } obstruction)
        {
            var hit = FindNodeById(top, obstruction.NodeId!);
            Add("hit_test_obstruction", "proven", pointer, "The proposed point hits a different input control.", "avalonia_input_hit_test", hit as Visual, "Inspect the covering control or overlay; analysis does not dismiss it.");
        }
        if (request.Strategy == "semantic" && request.Action is InputActions.Invoke or InputActions.Toggle or InputActions.Select or InputActions.Expand or InputActions.Collapse)
        {
            var peer = node is Control control ? ControlAutomationPeer.CreatePeerForElement(control) : null;
            if (peer is null || !GetSupportedSemanticAutomationActions(peer).Contains(request.Action, StringComparer.Ordinal))
                Add("automation_pattern_unavailable", "proven", true, "The target does not expose the requested automation pattern.", "avalonia_automation_peer", node, "Read the target's available actions and choose a supported route.");
        }
        if (request.Strategy == "native")
        {
            try
            {
                NativeWindowInput.ValidateOwnership(top, requireFocus: true);
                if (pointer) NativeWindowInput.ValidateOperation(top, request.Action, null, [], KeyModifiers.None);
            }
            catch (Exception exception) when (exception is NotSupportedException or InvalidOperationException or DllNotFoundException or EntryPointNotFoundException)
            { Add("native_route_unavailable", "proven", true, exception.GetType().Name + ": " + exception.Message, "owned_native_window", node, "Read session backend restrictions and native permissions before requesting native input."); }
        }
        if (request.Strategy != "semantic" && request.Action is not (InputActions.Click or InputActions.PointerMove or InputActions.Drag or InputActions.KeySequence or InputActions.KeyText))
            Add("strategy_action_unavailable", "proven", true, "This action has no explicit synthetic/native route.", "avascope_input_contract", node, "Use a supported semantic action or explicit compound pointer/keyboard operation.");
        Add("native_confirmation_unavailable", "unknown", false, "Avalonia hit testing does not prove that another process or native surface is not covering this window.", "in_process_evidence_boundary", node, "Use native evidence when available; do not treat DIP geometry as proof of OS visibility.");
        return CoreResult<RuntimeActionExplanation>.Ok(new(SessionId, CreateNodeTarget(request.TopLevelId, TreeKinds.Visual, top, node),
            Limit(GetAutomationId(node)), request.Action, request.Strategy, blocked ? "blocked" : "no_observed_blocker", reasons, point, truncated));
    }

    private static (AvaScopeActionContext? Context, bool Failed) ReadActionContext(Visual node, string action)
    {
        try
        {
            var provider = node as IAvaScopeActionContextProvider
                ?? (node is Control control ? ControlAutomationPeer.CreatePeerForElement(control) as IAvaScopeActionContextProvider : null);
            return provider is null ? (null, false) : (provider.GetActionContext(action) ?? throw new InvalidOperationException(), false);
        }
        catch (Exception exception) when (exception is not OutOfMemoryException and not AccessViolationException) { return (null, true); }
    }

    private static Window? FindModalBlocker(TopLevel top)
    {
        while (top is PopupRoot popup) top = popup.ParentTopLevel;
        return top is Window owner
            ? owner.OwnedWindows.Take(64).FirstOrDefault(window => window.IsDialog && window.IsVisible) : null;
    }

    private RuntimeActivationPoint ResolveActivationPoint(TopLevel top, string topId, Visual node, double? x, double? y,
        AvaScopeActionContext? context, bool providerFailed)
    {
        var source = x is not null ? "explicit_top_level_dip" : context?.ActivationPoint is not null ? "app_declared_local_dip" : "bounds_center";
        RuntimeActivationPoint Result(string status, Point? point, string? revision, Visual? hit, string? reason) =>
            new(status, source, "top_level_dip", point?.X, point?.Y, top.RenderScaling, revision, "unavailable",
                hit is null ? null : CreateNodeTarget(topId, TreeKinds.Visual, top, hit), reason);
        if (providerFailed) return Result("unavailable", null, null, null, "Application point evidence failed.");
        var chain = node.GetSelfAndVisualAncestors().Take(33).ToArray();
        if (chain.Length > 32 || chain.OfType<Control>().Any(control => !control.IsMeasureValid || !control.IsArrangeValid))
            return Result("unavailable", null, null, null, "Layout is invalid or the bounded ancestor chain is incomplete.");
        var bounds = GetGlobalBounds(node, top);
        if (bounds is not { } rect || rect.Width <= 0 || rect.Height <= 0
            || !double.IsFinite(rect.X) || !double.IsFinite(rect.Y) || !double.IsFinite(rect.Width) || !double.IsFinite(rect.Height))
            return Result("invalid", null, null, null, "The target has no finite positive arranged geometry.");
        var point = x is { } px && y is { } py ? new Point(px, py)
            : context?.ActivationPoint is { } declaredPoint ? node.TranslatePoint(declaredPoint, top) : rect.Center;
        if (point is not { } p || !double.IsFinite(p.X) || !double.IsFinite(p.Y))
            return Result("invalid", null, null, null, "The declared point or its coordinate transform is invalid.");
        if (!node.IsEffectivelyVisible || !new Rect(top.ClientSize).Contains(p))
            return Result("clipped", p, null, null, "The point is hidden or outside the top-level client area.");
        foreach (var ancestor in chain)
        {
            var local = top.TranslatePoint(p, ancestor);
            if (local is null || ancestor.ClipToBounds && !new Rect(ancestor.Bounds.Size).Contains(local.Value)
                || ancestor.Clip is { } clip && !clip.FillContains(local.Value))
                return Result("clipped", p, null, null, "The point falls outside a current ancestor clip.");
        }
        var hit = top.InputHitTest(p, enabledElementsOnly: false) as Visual;
        var valid = hit == node || hit?.GetVisualAncestors().Take(32).Contains(node) == true;
        var geometry = new StringBuilder();
        geometry.Append(SessionId.Value).Append('|').Append(topId).Append('|').Append(CreateNodeId(node, TreeKinds.Visual));
        geometry.Append(FormattableString.Invariant($"|{p.X:R},{p.Y:R}|{top.RenderScaling:R}|{top.ClientSize.Width:R},{top.ClientSize.Height:R}"));
        if (top is Window window) geometry.Append(FormattableString.Invariant($"|{window.Position.X},{window.Position.Y}"));
        foreach (var ancestor in chain)
        {
            var b = GetGlobalBounds(ancestor, top);
            var c = ancestor.Clip?.Bounds;
            geometry.Append(FormattableString.Invariant($"|{b?.X:R},{b?.Y:R},{b?.Width:R},{b?.Height:R}|")).Append('|')
                .Append(ancestor.TransformToVisual(top)?.ToString()).Append('|').Append(ancestor.ClipToBounds)
                .Append(FormattableString.Invariant($"|{c?.X:R},{c?.Y:R},{c?.Width:R},{c?.Height:R}"));
        }
        var revision = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(geometry.ToString())));
        return Result(valid ? "valid" : "obstructed", p, revision, hit,
            valid ? null : "The current Avalonia input hit test does not resolve to this target or its descendants.");
    }

    private CoreError? InputBlocker(TopLevel top, string topId, Visual node, string action)
    {
        while (top is PopupRoot popup) top = popup.ParentTopLevel;
        var blocker = FindModalBlocker(top);
        if (blocker is null && top is not Window { OwnedWindows.Count: > 64 }
            && node is InputElement { IsEffectivelyEnabled: true } && node.IsEffectivelyVisible)
        {
            var (context, failed) = ReadActionContext(node, action);
            if (!failed && context?.CanExecute != false) return null;
        }
        return new CoreError(BridgeErrorCodes.UnsupportedInputAction, "The selected target is blocked; request explain_action for bounded evidence.",
            new Dictionary<string, string>
            {
                ["targetNodeId"] = CreateNodeId(node, TreeKinds.Visual), ["topLevelId"] = topId,
                ["blockerNodeId"] = blocker is null ? CreateNodeId(node, TreeKinds.Visual) : CreateNodeId(blocker, TreeKinds.Visual),
                ["dispatchedEvents"] = "0", ["nextAction"] = "Call explain_action with the same session, window and target; do not force enabled/visible state."
            });
    }
}
