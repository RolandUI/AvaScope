using System.Diagnostics;
using System.Text.Json;
using System.Windows.Input;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using AvaScope.Core;
using AvaScope.Protocol;

namespace AvaScope.Bridge;

public sealed partial class AvaScopeBridgeRuntime
{
    public async Task<CoreResult<RuntimeActionMapResponse>> ActionMapAsync(RuntimeActionMapRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return await Dispatcher.UIThread.InvokeAsync(() => ActionMap(request), DispatcherPriority.Background, cancellationToken);
    }

    private CoreResult<RuntimeActionMapResponse> ActionMap(RuntimeActionMapRequest request)
    {
        Dispatcher.UIThread.VerifyAccess();
        if (request.SessionId != SessionId) return Fail("action_map_session_mismatch", "Select this bridge session explicitly.");
        var top = FindTopLevel(request.TopLevelId);
        if (top is null) return TopLevelNotFound<RuntimeActionMapResponse>(request.TopLevelId);
        var policy = request.Policy is null ? null : new RuntimeEvidencePolicyEnforcer(request.Policy);
        if (policy?.AuthorizeAction(SemanticWorkflowActions.Inspect, null) is { Success: false } denied)
            return CoreResult<RuntimeActionMapResponse>.Fail(denied.Error!);
        var started = Stopwatch.GetTimestamp();
        var work = 0;
        var reasons = new HashSet<string>(StringComparer.Ordinal);
        var visited = new HashSet<Visual>(ReferenceEqualityComparer.Instance);
        var entries = new List<RuntimeMappedAction>();
        var identities = new Dictionary<Visual, string>(ReferenceEqualityComparer.Instance);
        try
        {
            Visit(top, [], null, 0, true);
            // The registration allowlist remains authoritative; never reflect arbitrary commands/methods.
            var customCount = 0;
            foreach (var (_, entry) in _customActions)
            {
                if (!Budget() || ++customCount > request.MaxNodes) { reasons.Add("custom_action_limit"); break; }
                if (!entry.Target.TryGetTarget(out var node) || TopLevel.GetTopLevel(node) != top || Excluded(node)) continue;
                if (!visited.Contains(node)) { reasons.Add("custom_target_outside_scan"); continue; }
                var registration = entry.Registration;
                if (Safe(registration.Name) != registration.Name) { reasons.Add("policy_redaction"); continue; }
                var target = Target(node);
                if (target is null) continue;
                var descriptor = CreateCustomActionDescriptor(registration, node, target);
                var available = descriptor.Executable;
                var unavailable = new List<string>();
                if (!available) unavailable.Add("custom_action_unavailable");
                if (policy?.AuthorizeAction(SemanticWorkflowActions.CustomAction, registration.Name) is { Success: false })
                { available = false; unavailable.Add("policy_denied"); }
                var route = registration.Route.Count == 0 ? new[] { Safe(registration.Name)! } : registration.Route.Select(part => Safe(part)!).ToArray();
                Add(new("custom:" + CreateObjectGeneration(node) + ":" + Safe(registration.Name), "custom_action", Safe(registration.Name), route,
                    "app_declared", target, null, ["invoke_custom_action"], available ? "available" : "unavailable", unavailable,
                    Safe(registration.Description), [], 1, CustomActionName: registration.Name));
            }
            foreach (var (node, identity) in identities)
            {
                if (!Budget()) break;
                if (QueryIdentity(node) != identity)
                    return Fail("action_map_changed", "An action target changed during discovery. Search again before deciding on an action.");
            }
            if (!ReferenceEquals(FindTopLevel(request.TopLevelId), top))
                return Fail("action_map_changed", "The selected window closed during discovery.");
            var labelCounts = entries.Where(entry => entry.Label is not null).GroupBy(entry => entry.Label!, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.Count(), StringComparer.OrdinalIgnoreCase);
            var matched = entries.Where(entry => request.Search is null
                || new[] { entry.Label, entry.Description, entry.CustomActionName }.Concat(entry.Route).Concat(entry.Shortcuts.Select(shortcut => shortcut.Gesture))
                    .Any(value => value?.Contains(request.Search, StringComparison.OrdinalIgnoreCase) == true)).ToArray();
            if (matched.Length > request.MaxResults) reasons.Add("result_limit");
            var result = matched.Take(request.MaxResults).Select(entry => entry with
                { SameLabelCountAtLeast = entry.Label is null ? 1 : labelCounts[entry.Label] }).ToList();
            RuntimeActionMapResponse Response() => new(SessionId, request.TopLevelId, result.ToArray(),
                new(visited.Count, entries.Count, reasons.Count == 0, reasons.Order(StringComparer.Ordinal).ToArray()), DateTimeOffset.UtcNow);
            var response = Response();
            while (result.Count > 0 && JsonSerializer.SerializeToUtf8Bytes(response).Length > 65536)
            { result.RemoveAt(result.Count - 1); reasons.Add("response_byte_limit"); response = Response(); }
            return policy is null ? CoreResult<RuntimeActionMapResponse>.Ok(response) : policy.Sanitize(response);
        }
        catch (Exception exception) when (exception is not OutOfMemoryException and not AccessViolationException)
        { return Fail("action_map_unavailable", "A public action property could not be read safely. Discovery did not dispatch input or invoke an action."); }

        void Visit(Visual node, IReadOnlyList<string> route, RuntimeTargetContext? reveal, int depth, bool ancestorsEnabled)
        {
            if (visited.Contains(node) || !Budget()) return;
            if (visited.Count == request.MaxNodes) { reasons.Add("node_limit"); return; }
            visited.Add(node);
            if (Excluded(node)) { reasons.Add("policy_exclusions"); return; }
            identities[node] = QueryIdentity(node);
            var currentRoute = route;
            var nextReveal = reveal;
            var enabled = ancestorsEnabled && (node is not InputElement element || element.IsEffectivelyEnabled);
            if (node is Button or MenuItem)
            {
                var control = (Control)node;
                var label = Label(control);
                currentRoute = node is MenuItem ? route.Append(label ?? "<unlabelled>").ToArray() : new[] { label ?? "<unlabelled>" };
                var target = Target(node);
                var peer = ControlAutomationPeer.CreatePeerForElement(control);
                var actions = peer is null ? Array.Empty<string>() : GetSupportedSemanticAutomationActions(peer).ToArray();
                var unavailable = new List<string>();
                var availability = "available";
                if (!enabled) { availability = "disabled"; unavailable.Add("element_or_route_disabled"); }
                if (!node.IsVisible) { availability = "hidden"; unavailable.Add("element_hidden"); }
                if (node is ICommandSource commandSource && CommandState(commandSource.Command, commandSource.CommandParameter) is { } commandState)
                { availability = commandState; unavailable.Add(commandState); }
                if (target is null) { availability = "unrealized"; unavailable.Add("open_route_then_observe_current_target"); }
                else if (!node.IsEffectivelyVisible) { availability = "hidden"; unavailable.Add("ancestor_hidden"); }
                else if (FindModalBlocker(top) is not null) { availability = "blocked"; unavailable.Add("owned_modal_window"); }
                else if (top is Window { OwnedWindows.Count: > 64 }) { availability = "unknown"; unavailable.Add("modal_scan_limit"); }
                foreach (var action in actions)
                {
                    var (context, failed) = ReadActionContext(node, action);
                    if (failed) { availability = "unknown"; unavailable.Add("app_context_unavailable"); }
                    else if (context?.CanExecute == false) { availability = "blocked"; unavailable.Add("app_declared_action_blocked"); }
                }
                if (actions.Length == 0)
                {
                    if (target is not null) availability = "unsupported";
                    unavailable.Add("no_public_semantic_provider");
                }
                if (policy is not null)
                {
                    actions = actions.Where(action => policy.AuthorizeAction(action, null).Success).ToArray();
                    if (actions.Length == 0) { availability = "policy_denied"; unavailable.Add("policy_denied"); }
                }
                var shortcuts = new List<RuntimeActionShortcut>();
                var hotkey = node is Button button ? button.HotKey : ((MenuItem)node).HotKey;
                if (hotkey is not null) shortcuts.Add(new(hotkey.ToString(), "public_hotkey"));
                if (node is MenuItem { InputGesture: { } display }) shortcuts.Add(new(display.ToString(), "display_only_not_a_binding"));
                Add(new("control:" + CreateObjectGeneration(node), node is MenuItem ? "menu_item" : "button", label, currentRoute,
                    "observed_public_control", target, reveal, actions, availability, unavailable, Safe(AutomationProperties.GetHelpText(control)),
                    shortcuts, 1, node is MenuItem ? "loaded_items_only;future_lazy_population_unknown" : null));
                if (node is MenuItem && target is not null) nextReveal = target;
            }
            if (node is InputElement input)
            {
                var bindings = input.KeyBindings.Take(17).ToArray();
                if (bindings.Length > 16) reasons.Add("shortcut_limit");
                foreach (var binding in bindings.Take(16))
                {
                    if (!Budget()) break;
                    if (binding.Gesture is null) continue;
                    var target = Target(node);
                    var commandState = CommandState(binding.Command, binding.CommandParameter);
                    var availability = commandState ?? (binding.Command is null ? "unbound" : target is null ? "unrealized" : input.IsKeyboardFocusWithin ? "available" : "focus_required");
                    if (!input.IsEffectivelyEnabled || !input.IsEffectivelyVisible) availability = "disabled";
                    if (FindModalBlocker(top) is not null) availability = "blocked";
                    else if (top is Window { OwnedWindows.Count: > 64 }) availability = "unknown";
                    var gesture = binding.Gesture.ToString();
                    Add(new("key:" + CreateObjectGeneration(binding), "key_binding", gesture, [gesture], "observed_public_key_binding", target, reveal,
                        [], availability, ["target_is_routing_scope;key_consumption_requires_explicit_input"],
                        "Public key binding; no command parameter values are exposed. Focus must lie within its routing scope.", [new(gesture, "public_key_binding")], 1));
                }
            }
            if (depth == request.MaxDepth)
            { if (node.GetVisualChildren().Any() || node is MenuItem { ItemCount: > 0 }) reasons.Add("depth_limit"); return; }
            // Items that are already controls can be observed without realizing a template or opening a menu.
            if (node is Menu or MenuItem or ContextMenu)
            {
                var menu = (ItemsControl)node;
                var count = menu.ItemCount;
                for (var index = 0; index < count; index++)
                {
                    if (!Budget() || visited.Count == request.MaxNodes) { reasons.Add("node_limit"); break; }
                    var item = menu.Items[index] as Control ?? menu.ContainerFromIndex(index);
                    if (item is null) { reasons.Add("unrealized_menu_data"); continue; }
                    Visit(item, currentRoute, nextReveal, depth + 1, enabled);
                }
            }
            if (node is Control { ContextMenu: { } contextMenu })
                Visit(contextMenu, [Label((Control)node) ?? "<context>"], Target(node), depth + 1, enabled);
            foreach (var child in node.GetVisualChildren())
            {
                if (!Budget() || visited.Count == request.MaxNodes) { reasons.Add("node_limit"); break; }
                Visit(child, node is MenuItem ? currentRoute : route, nextReveal, depth + 1, enabled);
            }
        }
        bool Budget()
        {
            if (++work <= 32768 && Stopwatch.GetElapsedTime(started) <= TimeSpan.FromSeconds(2)) return true;
            reasons.Add("analysis_budget"); return false;
        }
        bool Excluded(Visual node) => TableExcluded(node, request.Policy) || request.Policy is { } settings
            && node.GetSelfAndVisualAncestors().Take(65).Any(ancestor => settings.RedactedAutomationIds.Contains(GetAutomationId(ancestor), StringComparer.Ordinal));
        RuntimeTargetContext? Target(Visual node) => TopLevel.GetTopLevel(node) == top
            ? CreateNodeTarget(request.TopLevelId, TreeKinds.Visual, top, node) : null;
        string? Safe(string? text)
        {
            if (text is null) return null;
            var safe = policy?.SanitizeScalar(text) ?? text;
            if (safe.Length <= 512) return safe;
            reasons.Add("text_truncated"); return safe[..512];
        }
        string? Label(Control control)
        {
            if (request.Policy?.RedactedAutomationIds.Contains(GetAutomationId(control), StringComparer.Ordinal) == true) return "[REDACTED]";
            var text = AutomationProperties.GetName(control);
            text ??= control is MenuItem menu ? menu.Header as string ?? (menu.Header as TextBlock)?.Text : GetText(control);
            return Safe(text ?? control.Name);
        }
        string? CommandState(ICommand? command, object? parameter)
        {
            if (command is null) return null;
            try { return command.CanExecute(parameter) ? null : "command_disabled"; }
            catch (Exception exception) when (exception is not OutOfMemoryException and not AccessViolationException) { return "command_availability_unknown"; }
        }
        void Add(RuntimeMappedAction entry)
        {
            if (entries.Count >= 4096) { reasons.Add("action_limit"); return; }
            entries.Add(entry);
        }
        static CoreResult<RuntimeActionMapResponse> Fail(string code, string message) => CoreResult<RuntimeActionMapResponse>.Fail(new(code, message));
    }
}
