# Searchable application actions

Use MCP `action_map` or CLI `avascope action-map --request actions.json` to search
one explicitly selected window before choosing an operation:

```json
{
  "sessionId": "<session>",
  "topLevelId": "<window>",
  "search": "CSV",
  "maxResults": 40,
  "maxNodes": 1024,
  "maxDepth": 16
}
```

The `runtime.action_map` capability reads public Avalonia buttons, menu controls,
already-loaded menu items, attached context menus, hotkeys, key bindings and
existing allowlisted custom actions. Matching is a case-insensitive substring of
the **sanitized** label, route, description, custom action name or shortcut.
There is no source-code search or foreign automation-framework integration.

Each result carries a session-local identity, route, provenance, current
availability and reasons. A visible control has a generation-bearing `target`.
Closed menu items can supply a route such as `File > Export > CSV` while their
target remains null; `revealTarget` identifies the nearest observed route owner.
Explicitly open the route through supported existing input, list any new top-level,
then search again for its current target. A context-menu owner is evidence about
where to open the menu, not a request to invoke the owner's default action.

Routes from menu controls are `observed_public_control`. Optional custom-action
routes are `app_declared` and need not describe a visible menu:

```csharp
runtime.RegisterCustomAction(control, new CustomActionRegistration(
    "export", ExportHandler, description: "Export the current table as CSV",
    route: ["Tools", "Export", "CSV"]));
```

Custom action activation and registration allowlists remain authoritative. The
map returns the custom action name and target; use existing `custom_actions` for
parameter requirements and `invoke_custom_action` for an explicit invocation.
Discovery never calls command `Execute`, custom-action handlers, menu opening or
input APIs. Public `CanExecute` and declared availability callbacks run on the UI
thread and are expected to be fast, read-only application code.

Shortcut provenance distinguishes `public_hotkey`, `public_key_binding`, and
`display_only_not_a_binding`. Avalonia `MenuItem.InputGesture` only displays text;
it does not install a key binding. A standalone key-binding target denotes its
routing scope. Focus must be inside that scope; earlier event handlers, IME and
platform routing may still consume the key. An observation does not prove that a
future key will execute the command. No command-parameter value is exposed.

`sameLabelCountAtLeast` counts duplicate sanitized labels in the bounded observed
scope, including actions outside the search's result limit. Always preserve target
identity and route instead of choosing the first matching label. Availability is
current evidence, not a dispatch authorization or a promise about future state;
normal input/custom-action validation still applies.

Limits are 4096 visited nodes, depth 32, 128 returned actions, 16 key bindings per
node, 512 characters per text field, a 64 KiB response and a cooperative two-second
/ 32768-work-item budget. Callbacks cannot be preempted. Coverage reasons identify
node, depth, shortcut, action, text, result and byte limits, exclusions and
unrealized data templates. Discovery never constructs a menu data template to
learn a label. Redacted or excluded control owners and their context menus are
omitted; protected text is sanitized before searching.

`completeObservedScope` applies only to the scanned public scope. Future lazy
population, menu-open callbacks, native menu bars, unmaterialized menu data and
other windows remain unknown. The map does not claim to enumerate the entire
application. No dependency on a particular desktop backend is required.

`RuntimeActionMapTests` covers closed nested menus, duplicates, disabled or failing
commands, real/display-only shortcuts, app declarations and allowlists, lazy data,
redaction, response limits, stale targets, and actual CLI/MCP search parity.
Public API references: [MenuItem](https://github.com/AvaloniaUI/Avalonia/blob/12.1.3/src/Avalonia.Controls/MenuItem.cs),
[Button](https://github.com/AvaloniaUI/Avalonia/blob/12.1.3/src/Avalonia.Controls/Button.cs),
[keyboard routing](https://docs.avaloniaui.net/docs/input-interaction/keyboard-and-hotkeys).
