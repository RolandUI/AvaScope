using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
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
    // Never evict a dispatched request: reusing its id must not repeat an uncertain operation.
    private readonly Dictionary<string, (string Fingerprint, CoreResult<RuntimeDesiredStateResponse>? Result)> _desiredStateRequests = new(StringComparer.Ordinal);

    public async Task<CoreResult<RuntimeDesiredStateResponse>> EnsureStateAsync(RuntimeDesiredStateRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!await _explicitInputGate.WaitAsync(0, cancellationToken))
            return DesiredStateFailure("desired_state_busy", "Another compound input operation is active; wait for its result.");
        try
        {
            return await Dispatcher.UIThread.InvokeAsync(() => RememberDesiredState(request), DispatcherPriority.Background, cancellationToken);
        }
        finally { _explicitInputGate.Release(); }
    }

    private CoreResult<RuntimeDesiredStateResponse> RememberDesiredState(RuntimeDesiredStateRequest request)
    {
        Dispatcher.UIThread.VerifyAccess();
        if (request.Target.SessionId != SessionId) return DesiredStateFailure("desired_state_session_mismatch", "Select this bridge session explicitly.");
        if (request.Policy is { } policy && !policy.AllowedDesiredStates.Contains(request.Property, StringComparer.Ordinal))
            return DesiredStateFailure("desired_state_policy_denied", "The evidence policy must explicitly allow this desired-state property.");
        var bytes = JsonSerializer.SerializeToUtf8Bytes(request);
        if (bytes.Length > 65536) return DesiredStateFailure("desired_state_request_limit", "The request exceeds 64 KiB.");
        var fingerprint = Convert.ToHexStringLower(SHA256.HashData(bytes));
        if (_desiredStateRequests.TryGetValue(request.RequestId, out var existing))
        {
            if (existing.Fingerprint != fingerprint) return DesiredStateFailure("desired_state_id_conflict", "This request id belongs to a different payload. Inspect the previous result before making a new request.");
            if (existing.Result is null) return DesiredStateFailure("desired_state_pending", "This request has started; its outcome is not available. Do not replay with a new id.");
            return existing.Result.Success ? CoreResult<RuntimeDesiredStateResponse>.Ok(existing.Result.Value! with { Replayed = true }) : existing.Result;
        }
        if (_desiredStateRequests.Count == 512) return DesiredStateFailure("desired_state_ledger_full", "This session has reached its 512 desired-state request limit. Existing ids remain replayable; observe before starting another authorized session.");
        _desiredStateRequests.Add(request.RequestId, (fingerprint, null));
        CoreResult<RuntimeDesiredStateResponse> result;
        try { result = EnsureState(request); }
        catch (Exception exception) when (exception is not OutOfMemoryException and not AccessViolationException)
        { result = DesiredStateFailure("desired_state_uncertain", "The provider failed outside state verification. Observe current state; this request will not be dispatched again."); }
        if (request.Policy is { } evidencePolicy)
        {
            var enforcer = new RuntimeEvidencePolicyEnforcer(evidencePolicy);
            if (result.Success) result = enforcer.Sanitize(result.Value!);
            else
            {
                var safe = enforcer.Sanitize(result.Error!);
                result = CoreResult<RuntimeDesiredStateResponse>.Fail(safe.Success ? safe.Value! : safe.Error!);
            }
        }
        _desiredStateRequests[request.RequestId] = (fingerprint, result);
        return result;
    }

    private CoreResult<RuntimeDesiredStateResponse> EnsureState(RuntimeDesiredStateRequest request, bool validateOnly = false)
    {
        var target = request.Target;
        var top = FindTopLevel(target.TopLevelId);
        if (top is null) return TopLevelNotFound<RuntimeDesiredStateResponse>(target.TopLevelId);
        var resolved = ResolveInputGenerationTarget(top, target.TopLevelId, target.NodeId, target);
        if (!resolved.Success) return CoreResult<RuntimeDesiredStateResponse>.Fail(resolved.Error!);
        if (FindNodeById(top, target.NodeId!) is not Control control)
            return DesiredStateFailure("desired_state_control_required", "Select a live control with a supported public input/provider path.");

        var started = Stopwatch.GetTimestamp();
        var tracked = new List<(Control Control, RuntimeTargetContext Target, string Identity)> { (control, target, QueryIdentity(control, false)) };
        var operations = 0;
        var preparation = false;
        var route = request.Property == "text" && control is TextBox ? RuntimeOperationRoutes.RoutedEvent : RuntimeOperationRoutes.AutomationProvider;
        var before = new RuntimeDesiredStateValue("unavailable", request.Property, null);
        var after = before;
        var diagnostics = new List<ProtocolError>();
        AutomationPeer? peer = null;
        IToggleProvider? toggle = null;
        IExpandCollapseProvider? expand = null;
        IRangeValueProvider? range = null;
        IValueProvider? text = null;
        ISelectionProvider? selection = null;
        var wantedItems = new List<(Control Control, ISelectionItemProvider Provider)>();
        try
        {
            Fresh();
            peer = request.Property == "text" && control is TextBox ? null : ControlAutomationPeer.CreatePeerForElement(control);
            toggle = request.Property == "checked" ? peer?.GetProvider<IToggleProvider>() : null;
            expand = request.Property == "expanded" ? peer?.GetProvider<IExpandCollapseProvider>() : null;
            range = request.Property == "value" ? peer?.GetProvider<IRangeValueProvider>() : null;
            text = request.Property == "text" ? peer?.GetProvider<IValueProvider>() : null;
            selection = request.Property == "selection" ? peer?.GetProvider<ISelectionProvider>() : null;
            before = after = Read();
            Fresh();
            if (before.Status is "unavailable" or "missing") throw new DesiredStateStop("desired_state_unsupported", "The selected control cannot expose this bounded state through a supported public provider.");
            if (request.Property == "selection")
            {
                foreach (var item in request.Desired.Deserialize<RuntimeTargetContext[]>()!)
                {
                    var checkedItem = ResolveInputGenerationTarget(top, target.TopLevelId, item.NodeId, item);
                    if (!checkedItem.Success || FindNodeById(top, item.NodeId!) is not Control child)
                        throw new DesiredStateStop("desired_state_item_stale", "A selected item target is stale; resolve the intended items again.");
                    tracked.Add((child, item, QueryIdentity(child, false)));
                    var provider = ControlAutomationPeer.CreatePeerForElement(child)?.GetProvider<ISelectionItemProvider>();
                    if (provider is null || !ReferenceEquals(provider.SelectionContainer, selection))
                        throw new DesiredStateStop("desired_state_selection_container", "Every requested item must expose a selection provider for the selected container.");
                    wantedItems.Add((child, provider));
                }
            }
            Fresh();
            if (Satisfied(after)) return Finish("already_satisfied", true);
            switch (request.Property)
            {
                case "checked":
                    if (validateOnly) { CheckAction(InputActions.Toggle); return Finish("validated", false); }
                    var seen = new HashSet<string>(StringComparer.Ordinal);
                    for (var attempt = 0; attempt < 3 && !Satisfied(after); attempt++)
                    {
                        if (!seen.Add(after.Value!.Value.GetRawText())) break;
                        Dispatch(toggle!.Toggle, InputActions.Toggle);
                        after = Read(); Fresh();
                    }
                    break;
                case "expanded":
                    if (expand!.ExpandCollapseState == ExpandCollapseState.LeafNode)
                        throw new DesiredStateStop("desired_state_leaf", "A leaf node has no expanded/collapsed state.");
                    if (validateOnly) { CheckAction(request.Desired.GetBoolean() ? InputActions.Expand : InputActions.Collapse); return Finish("validated", false); }
                    Dispatch(request.Desired.GetBoolean() ? expand.Expand : expand.Collapse,
                        request.Desired.GetBoolean() ? InputActions.Expand : InputActions.Collapse);
                    break;
                case "value":
                    var value = request.Desired.GetDouble();
                    if (range!.IsReadOnly || !double.IsFinite(range.Minimum) || !double.IsFinite(range.Maximum)
                        || value < range.Minimum || value > range.Maximum)
                        throw new DesiredStateStop("desired_state_range", "The requested value is outside the public provider range or the control is read-only.");
                    if (validateOnly) { CheckAction("set_value"); return Finish("validated", false); }
                    Dispatch(() => range.SetValue(value), "set_value");
                    break;
                case "text":
                    var desiredText = request.Desired.GetString()!;
                    if (control is TextBox box)
                    {
                        if (box.IsReadOnly) throw new DesiredStateStop("desired_state_read_only", "The text control is read-only.");
                        CheckAction(InputActions.KeyText);
                        if (validateOnly) return Finish("validated", false);
                        preparation = true;
                        if (!box.IsFocused && !box.Focus(NavigationMethod.Unspecified))
                            throw new DesiredStateStop("desired_state_focus", "The selected text control did not accept focus.");
                        Fresh();
                        box.SelectAll();
                        Fresh();
                        if (desiredText.Length > 0)
                            Dispatch(() => box.RaiseEvent(new TextInputEventArgs { RoutedEvent = InputElement.TextInputEvent, Source = box, Text = desiredText }), InputActions.KeyText);
                        else
                        {
                            try { Dispatch(() => box.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Source = box, Key = Key.Delete }), InputActions.KeyDown); }
                            finally
                            {
                                // Complete only this request's routed key sequence, even after a callback failure.
                                if (operations > 0) { operations++; box.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyUpEvent, Source = box, Key = Key.Delete }); }
                            }
                        }
                    }
                    else
                    {
                        if (text!.IsReadOnly) throw new DesiredStateStop("desired_state_read_only", "The public value provider is read-only.");
                        if (validateOnly) { CheckAction(InputActions.KeyText); return Finish("validated", false); }
                        Dispatch(() => text.SetValue(desiredText), InputActions.KeyText);
                    }
                    break;
                case "selection":
                    if (!selection!.CanSelectMultiple && wantedItems.Count > 1 || selection.IsSelectionRequired && wantedItems.Count == 0)
                        throw new DesiredStateStop("desired_state_selection_constraints", "The requested set violates the provider's multiple/required-selection constraints.");
                    var selected = ReadSelection();
                    if (validateOnly)
                    {
                        foreach (var item in selected.Concat(wantedItems)) CheckAction(InputActions.Select, item.Control);
                        return Finish("validated", false);
                    }
                    if (!selection.CanSelectMultiple && wantedItems.Count == 1)
                        Dispatch(wantedItems[0].Provider.Select, InputActions.Select, wantedItems[0].Control);
                    else
                    {
                        var expected = selected.Select(item => item.Control).ToHashSet();
                        foreach (var item in wantedItems.Where(item => !expected.Contains(item.Control)))
                        {
                            Dispatch(item.Provider.AddToSelection, InputActions.Select, item.Control);
                            expected.Add(item.Control);
                            if (!expected.SetEquals(ReadSelection().Select(item => item.Control)))
                                throw new DesiredStateStop("desired_state_selection_changed", "Selection changed unexpectedly during an addition; inspect the partial set before continuing.");
                        }
                        foreach (var item in selected.Where(item => wantedItems.All(wanted => wanted.Control != item.Control)))
                        {
                            Dispatch(item.Provider.RemoveFromSelection, InputActions.Select, item.Control);
                            expected.Remove(item.Control);
                            if (!expected.SetEquals(ReadSelection().Select(item => item.Control)))
                                throw new DesiredStateStop("desired_state_selection_changed", "Selection changed unexpectedly during a removal; inspect the partial set before continuing.");
                        }
                    }
                    break;
            }
            after = Read(); Fresh();
            return Satisfied(after) ? Finish("verified", true) : Finish(operations > 1 ? "partial" : "not_verified", false,
                "desired_state_not_verified", "The desired state was not observed after dispatch. Validation or asynchronous application behavior may be involved; observe before issuing a new request.");
        }
        catch (Exception exception) when (exception is not OutOfMemoryException and not AccessViolationException)
        {
            try { after = Read(); Fresh(); }
            catch (Exception readException) when (readException is not OutOfMemoryException and not AccessViolationException)
            { after = new("unavailable", request.Property, null); }
            var uncertain = operations > 0 || preparation;
            return Finish(uncertain ? exception is DesiredStateStop { Code: "desired_state_selection_changed" } && after.Status == "present" ? "partial" : "uncertain" : "unsupported", false,
                exception is DesiredStateStop stop ? stop.Code : "desired_state_provider_failed",
                exception is DesiredStateStop ? exception.Message : "A public provider failed. Observe current state before making another request.");
        }

        void Fresh()
        {
            if (Stopwatch.GetElapsedTime(started) > TimeSpan.FromSeconds(2)) throw new DesiredStateStop("desired_state_budget", "The cooperative two-second operation budget expired.");
            foreach (var item in tracked)
            {
                var current = ResolveInputGenerationTarget(top, target.TopLevelId, item.Target.NodeId, item.Target);
                if (!current.Success || TopLevel.GetTopLevel(item.Control) != top || item.Identity != QueryIdentity(item.Control, false))
                    throw new DesiredStateStop("desired_state_target_changed", "A target was detached, recycled or changed during preparation/execution.");
                if (request.Policy is { ExcludedControlAutomationIds.Count: > 0 } policy)
                {
                    var ancestors = item.Control.GetSelfAndVisualAncestors().Take(65).ToArray();
                    if (ancestors.Length > 64 || ancestors.Any(node => policy.ExcludedControlAutomationIds.Contains(GetAutomationId(node), StringComparer.Ordinal)))
                        throw new DesiredStateStop("desired_state_target_excluded", "The selected target is excluded by the evidence policy.");
                }
            }
        }

        void CheckAction(string action, Control? recipient = null)
        {
            if (InputBlocker(top, target.TopLevelId, recipient ?? control, action) is not null)
                throw new DesiredStateStop("desired_state_blocked", "The target is blocked; use explain_action and inspect current state.");
            Fresh();
        }

        void Dispatch(Action action, string kind, Control? recipient = null)
        {
            CheckAction(kind, recipient);
            var current = Read();
            Fresh();
            if (current.Status != after.Status || !JsonElement.DeepEquals(current.Value ?? JsonSerializer.SerializeToElement<object?>(null),
                after.Value ?? JsonSerializer.SerializeToElement<object?>(null)))
                throw new DesiredStateStop("desired_state_changed", "The state changed during preparation. Observe again instead of applying the previous action plan.");
            if (operations == 64) throw new DesiredStateStop("desired_state_operation_limit", "The 64-operation limit was reached.");
            operations++;
            action();
            Fresh();
            after = Read();
            Fresh();
        }

        List<(Control Control, ISelectionItemProvider Provider)> ReadSelection()
        {
            if (selection is null) throw new DesiredStateStop("desired_state_selection_unsupported", "The target has no public selection provider.");
            var items = selection.GetSelection();
            if (items.Count > 32 || control is SelectingItemsControl selecting
                && selecting.GetValue(ListBox.SelectionProperty).SelectedIndexes.Count != items.Count)
                throw new DesiredStateStop("desired_state_selection_incomplete", "The selection exceeds 32 items or includes unrealized items. No complete set can be verified.");
            var result = new List<(Control, ISelectionItemProvider)>();
            foreach (var item in items)
            {
                if (item is not ControlAutomationPeer child || TopLevel.GetTopLevel(child.Owner) != top
                    || item.GetProvider<ISelectionItemProvider>() is not { } provider || !ReferenceEquals(provider.SelectionContainer, selection))
                    throw new DesiredStateStop("desired_state_selection_incomplete", "A selected item has no live provider in the selected window/container.");
                if (tracked.All(entry => entry.Control != child.Owner))
                    tracked.Add((child.Owner, CreateNodeTarget(target.TopLevelId, TreeKinds.Visual, top, child.Owner), QueryIdentity(child.Owner, false)));
                result.Add((child.Owner, provider));
            }
            return result;
        }

        RuntimeDesiredStateValue Read()
        {
            object? value = request.Property switch
            {
                "checked" when toggle is not null => toggle.ToggleState switch { ToggleState.On => true, ToggleState.Off => false, _ => (bool?)null },
                "expanded" when expand is not null => expand.ExpandCollapseState switch { ExpandCollapseState.Expanded => true, ExpandCollapseState.Collapsed => false, _ => (bool?)null },
                "value" when range is not null => range.Value,
                "text" when control is TextBox box => box.Text ?? string.Empty,
                "text" when text is not null => text.Value ?? string.Empty,
                "selection" when selection is not null => ReadSelection().Select(item => CreateNodeId(item.Control, TreeKinds.Visual)).Order(StringComparer.Ordinal).ToArray(),
                _ => null
            };
            var type = request.Property switch { "checked" or "expanded" => "boolean", "value" => "number", "selection" => "node_ids", _ => "string" };
            if (value is double number && !double.IsFinite(number) || value is string content && content.Length > 4096)
                return new("unavailable", type, null);
            if (value is null)
                return new(request.Property == "checked" && toggle is not null ? "indeterminate" : "unavailable", type, JsonSerializer.SerializeToElement<object?>(null));
            return new("present", type, JsonSerializer.SerializeToElement(value));
        }

        bool Satisfied(RuntimeDesiredStateValue state) => state.Status is "present" or "indeterminate" && (request.Property == "selection"
            ? state.Value!.Value.Deserialize<string[]>()!.ToHashSet(StringComparer.Ordinal).SetEquals(wantedItems.Select(item => CreateNodeId(item.Control, TreeKinds.Visual)))
            : request.Property == "value" ? state.Value!.Value.GetDouble() == request.Desired.GetDouble()
            : JsonElement.DeepEquals(state.Value!.Value, request.Desired));

        CoreResult<RuntimeDesiredStateResponse> Finish(string status, bool verified, string? code = null, string? message = null)
        {
            if (verified && DataValidationErrors.GetHasErrors(control))
            { verified = false; status = "not_verified"; code = "desired_state_validation_errors"; message = "The requested state is visible, but the target reports validation errors."; }
            if (code is not null) diagnostics.Add(new(code, message!, new Dictionary<string, string>
            { ["nextAction"] = "Inspect before/after state and diagnostics. Replay the identical request id to retrieve this result; use a new id only for a newly observed intent." }));
            if (control is TextBox { PasswordChar: not '\0' })
            { before = before with { Status = "redacted", Value = null }; after = after with { Status = "redacted", Value = null }; }
            var evidenceTarget = control is TextBox { PasswordChar: not '\0' }
                ? new RuntimeTargetContext(target.SessionId, target.TopLevelId, target.TreeKind, target.NodeId, target.CapturedAt,
                    target.TargetKind, target.TopLevelGeneration, target.NodeGeneration) : target;
            return CoreResult<RuntimeDesiredStateResponse>.Ok(new(request.RequestId, evidenceTarget, request.Property, status, verified, before, after,
                operations, preparation, RuntimePlatformEvidence.Operation(top, route, operations > 0), diagnostics, DateTimeOffset.UtcNow));
        }
    }

    private static CoreResult<RuntimeDesiredStateResponse> DesiredStateFailure(string code, string message) => CoreResult<RuntimeDesiredStateResponse>.Fail(new(code, message));
    private sealed class DesiredStateStop(string code, string message) : Exception(message) { public string Code { get; } = code; }
}
