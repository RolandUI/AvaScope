using System.Diagnostics;
using System.Text.Json;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Threading;
using Avalonia.VisualTree;
using AvaScope.Core;
using AvaScope.Protocol;

namespace AvaScope.Bridge;

public sealed partial class AvaScopeBridgeRuntime
{
    public async Task<CoreResult<RuntimeFormInspectionResponse>> InspectFormAsync(RuntimeFormInspectionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var result = await Dispatcher.UIThread.InvokeAsync(() => InspectForm(request), DispatcherPriority.Background, cancellationToken);
        if (request.Policy is null) return result;
        var policy = new RuntimeEvidencePolicyEnforcer(request.Policy);
        if (result.Success) return policy.Sanitize(result.Value!);
        var error = policy.Sanitize(result.Error!);
        return CoreResult<RuntimeFormInspectionResponse>.Fail(error.Success ? error.Value! : error.Error!);
    }

    private CoreResult<RuntimeFormInspectionResponse> InspectForm(RuntimeFormInspectionRequest request)
    {
        Dispatcher.UIThread.VerifyAccess();
        if (request.SessionId != SessionId) return CoreResult<RuntimeFormInspectionResponse>.Fail(new("form_session_mismatch", "Select this bridge session explicitly."));
        if (request.Policy is { } requestedPolicy)
        {
            var allowed = new RuntimeEvidencePolicyEnforcer(requestedPolicy).AuthorizeAction(SemanticWorkflowActions.Inspect, null);
            if (!allowed.Success) return CoreResult<RuntimeFormInspectionResponse>.Fail(allowed.Error!);
        }
        var top = FindTopLevel(request.TopLevelId);
        if (top is null) return TopLevelNotFound<RuntimeFormInspectionResponse>(request.TopLevelId);
        Visual scope = top;
        if (request.Scope is { } scopeSelector)
        {
            var query = QueryNodes(new(SessionId, request.TopLevelId, scopeSelector, maxResults: 2, maxNodes: request.MaxNodes, maxDepth: 32, policy: request.Policy));
            if (!query.Success || query.Value!.Matches.Count != 1 || query.Value.Coverage!.Reasons.Any(reason => reason != "policy_exclusions")
                || FindNodeById(top, query.Value.Matches[0].Node.NodeId) is not Visual selected)
                return CoreResult<RuntimeFormInspectionResponse>.Fail(new("form_scope_unresolved", "Resolve one complete, unambiguous visual form scope before inspecting or filling it."));
            scope = selected;
        }
        var scopeTarget = CreateNodeTarget(request.TopLevelId, TreeKinds.Visual, top, scope);
        var policy = request.Policy is null ? null : new RuntimeEvidencePolicyEnforcer(request.Policy);
        var started = Stopwatch.GetTimestamp();
        var reasons = new HashSet<string>(StringComparer.Ordinal);
        var nodes = new List<(Visual Node, string Identity, Visual[] Children)>();
        var fields = new List<RuntimeFormField>();
        var candidates = new List<Control>();
        var choicesLeft = 64;
        try
        {
            Visit(scope, 0, false);
            foreach (var control in candidates)
            {
                if (!Budget()) break;
                if (fields.Count == request.MaxFields) { reasons.Add("field_limit"); break; }
                fields.Add(ReadField(control));
            }
            foreach (var entry in nodes)
            {
                if (!Budget()) break;
                if (entry.Identity != QueryIdentity(entry.Node) || !entry.Children.SequenceEqual(entry.Node.GetVisualChildren().Take(request.MaxNodes + 1)))
                { reasons.Add("generation_changed"); fields.Clear(); break; }
            }
            if (TopLevel.GetTopLevel(scope) != top) { reasons.Add("generation_changed"); fields.Clear(); }
            RuntimeFormInspectionResponse Response() => new(SessionId, request.TopLevelId, scopeTarget, fields.ToArray(),
                new(reasons.Count == 0, nodes.Count, candidates.Count, reasons.Order(StringComparer.Ordinal).ToArray(), "supported_fields_in_selected_visual_subtree"), DateTimeOffset.UtcNow);
            var response = Response();
            while (fields.Count > 0 && JsonSerializer.SerializeToUtf8Bytes(response).Length > 65536)
            { fields.RemoveAt(fields.Count - 1); reasons.Add("response_byte_limit"); response = Response(); }
            return CoreResult<RuntimeFormInspectionResponse>.Ok(response);
        }
        catch (Exception exception) when (exception is not OutOfMemoryException and not AccessViolationException)
        {
            return CoreResult<RuntimeFormInspectionResponse>.Fail(new("form_inspection_unavailable", "A public field/label/validation provider could not be read safely. No fill was attempted."));
        }

        bool Budget()
        {
            if (Stopwatch.GetElapsedTime(started) <= TimeSpan.FromSeconds(2)) return true;
            reasons.Add("analysis_budget"); return false;
        }
        bool Excluded(Visual node)
        {
            if (request.Policy is not { ExcludedControlAutomationIds.Count: > 0 } settings) return false;
            var ancestors = node.GetSelfAndVisualAncestors().Take(65).ToArray();
            return ancestors.Length > 64 || ancestors.Any(ancestor => settings.ExcludedControlAutomationIds.Contains(GetAutomationId(ancestor), StringComparer.Ordinal));
        }
        void Visit(Visual node, int depth, bool insideField)
        {
            if (!Budget()) return;
            if (nodes.Count == request.MaxNodes) { reasons.Add("node_limit"); return; }
            if (Excluded(node)) { reasons.Add("policy_exclusions"); return; }
            var children = node.GetVisualChildren().Take(request.MaxNodes + 1).ToArray();
            nodes.Add((node, QueryIdentity(node), children));
            var field = node is TextBox or ToggleButton or RangeBase or SelectingItemsControl;
            if (field && !insideField && node.IsEffectivelyVisible) candidates.Add((Control)node);
            if (depth == 32) { if (children.Length > 0) reasons.Add("depth_limit"); return; }
            foreach (var child in children)
            {
                if (nodes.Count == request.MaxNodes) { reasons.Add("node_limit"); break; }
                Visit(child, depth + 1, insideField || field);
            }
        }
        string? Safe(string? value, int limit = 512)
        {
            if (value is null) return null;
            var safe = policy?.SanitizeScalar(value) ?? value;
            if (safe.Length <= limit) return safe;
            reasons.Add("text_truncated"); return safe[..limit];
        }
        RuntimeDesiredStateValue State(object? value, string type, bool sensitive)
        {
            if (sensitive) return new("redacted", type, null);
            if (value is string content)
            {
                var safe = Safe(content)!;
                if (policy is not null && policy.SanitizeScalar(content) != content) return new("redacted", type, null);
                return new(content.Length > 512 ? "truncated" : "present", type, JsonSerializer.SerializeToElement(safe));
            }
            if (value is double number && !double.IsFinite(number)) return new("unavailable", type, null);
            return new(value is null ? "indeterminate" : "present", type, JsonSerializer.SerializeToElement(value));
        }
        (string? Label, string Source) Label(Control control)
        {
            var label = AutomationProperties.GetLabeledBy(control);
            var source = "automation_labeled_by";
            if (label is null)
            {
                try { label = (ControlAutomationPeer.CreatePeerForElement(control)?.GetLabeledBy() as ControlAutomationPeer)?.Owner; source = "automation_peer_labeled_by"; }
                catch (Exception exception) when (exception is not OutOfMemoryException and not AccessViolationException) { reasons.Add("label_provider_unavailable"); }
            }
            if (label is null)
            {
                label = nodes.Select(entry => entry.Node).OfType<Label>().FirstOrDefault(candidate => ReferenceEquals(candidate.Target, control));
                source = "label_target";
            }
            if (label is not null)
            {
                if (label is TextBox { PasswordChar: not '\0' }) return (null, "sensitive");
                if (Excluded(label)) return (null, "excluded");
                if (!nodes.Any(entry => entry.Node == label)) { reasons.Add("label_outside_scope"); return (null, "outside_scope"); }
                return (Safe(GetText(label) ?? GetName(label)), source);
            }
            var name = AutomationProperties.GetName(control);
            return string.IsNullOrWhiteSpace(name) ? (null, "unavailable") : (Safe(name), "automation_name");
        }
        RuntimeFormField ReadField(Control control)
        {
            var (label, labelSource) = Label(control);
            var property = control is TextBox ? "text" : control is ToggleButton ? "checked" : control is RangeBase ? "value" : "selection";
            var sensitive = control is TextBox { PasswordChar: not '\0' };
            var writable = control.IsEffectivelyEnabled && control.IsEffectivelyVisible;
            var choices = new List<RuntimeFormChoice>();
            int? choiceCount = null;
            var completeChoices = true;
            RuntimeDesiredStateValue state;
            if (control is TextBox text)
            { state = State(text.Text ?? string.Empty, "string", sensitive); writable &= !text.IsReadOnly; }
            else if (control is ToggleButton toggle)
            { state = State(toggle.IsChecked, "boolean", false); writable &= ControlAutomationPeer.CreatePeerForElement(control)?.GetProvider<IToggleProvider>() is not null; }
            else if (control is RangeBase range)
            {
                state = State(range.Value, "number", false);
                writable &= ControlAutomationPeer.CreatePeerForElement(control)?.GetProvider<IRangeValueProvider>() is { IsReadOnly: false };
            }
            else
            {
                var selecting = (SelectingItemsControl)control;
                choiceCount = selecting.ItemCount;
                var selected = selecting.GetValue(ListBox.SelectionProperty).SelectedIndexes.Take(33).ToArray();
                var selectedIds = new List<string>();
                foreach (var index in selected.Take(32))
                    if (selecting.ContainerFromIndex(index) is Control container && TopLevel.GetTopLevel(container) == top && !Excluded(container))
                        selectedIds.Add(CreateNodeId(container, TreeKinds.Visual));
                state = new(selected.Length > 32 || selectedIds.Count != selected.Length ? "partial" : "present", "node_ids",
                    JsonSerializer.SerializeToElement(selectedIds.Order(StringComparer.Ordinal).ToArray()));
                if (state.Status == "partial") reasons.Add("selection_incomplete");
                writable &= ControlAutomationPeer.CreatePeerForElement(control)?.GetProvider<ISelectionProvider>() is not null;
                for (var index = 0; index < Math.Min(8, selecting.ItemCount) && choicesLeft > 0; index++, choicesLeft--)
                {
                    var container = selecting.ContainerFromIndex(index);
                    if (container is not null && Excluded(container)) { reasons.Add("policy_exclusions"); continue; }
                    var item = selecting.ItemsView[index];
                    var choiceLabel = container is not null ? GetText(container) : item as string;
                    choices.Add(new(index, Safe(choiceLabel), container is not null && TopLevel.GetTopLevel(container) == top
                        ? CreateNodeTarget(request.TopLevelId, TreeKinds.Visual, top, container) : null, selected.Contains(index)));
                }
                completeChoices = choices.Count == selecting.ItemCount;
                if (!completeChoices) reasons.Add("choices_limit");
            }
            var rawErrors = (DataValidationErrors.GetErrors(control) ?? []).Cast<object?>().Take(9).ToArray();
            if (rawErrors.Length > 8) reasons.Add("validation_truncated");
            var validation = new RuntimeValidationState(DataValidationErrors.GetHasErrors(control) ? "has_errors" : "no_errors_observed",
                "avalonia_public_data_validation_errors;async_pending_unknown", DataValidationErrors.GetHasErrors(control), rawErrors.Length,
                sensitive ? [] : rawErrors.Take(8).Select(error => Safe(error is Exception exception ? exception.Message : error as string ?? "validation_error")!).ToArray());
            return new(CreateNodeTarget(request.TopLevelId, TreeKinds.Visual, top, control), Safe(GetName(control)), Safe(GetAutomationId(control)),
                label, labelSource, control.GetType().FullName!, property, state, writable, null, "unavailable", sensitive,
                choices, choiceCount, completeChoices, validation);
        }
    }
}
