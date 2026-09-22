# Desired runtime states

Use MCP `ensure_state` or CLI `ensure-state --request state.json` to request
`checked`, `expanded`, `text`, `value` or an exact `selection` set. Discover
`runtime.desired_state` and the selected bridge's `ensure_state` method first.
Supply the complete fresh visual `target` from find/inspection, including both
generation tokens. Relationship selection context is preserved and rechecked.

```json
{
  "target": {
    "sessionId": "current-session",
    "topLevelId": "current-window",
    "treeKind": "visual",
    "nodeId": "current-node",
    "topLevelGeneration": "observed-window-generation",
    "nodeGeneration": "observed-node-generation"
  },
  "property": "checked",
  "desired": true,
  "requestId": "unique-intent-id"
}
```

MCP accepts this as `request`; both adapters accept an optional manifest
directory. A session control lease applies exactly as it does to other input.
The host must explicitly activate its local bridge. No foreign automation
framework, language or workflow integration is involved.

## State and execution

| Property | Desired JSON | Execution and verification |
| --- | --- | --- |
| `checked` | Boolean or null for indeterminate | Public toggle provider; stop at the desired state, repeated state or three transitions. No blind retry of an unchanged/asynchronous toggle. |
| `expanded` | Boolean | Public expand/collapse provider; leaf or unsupported states are explicit. |
| `value` | Finite number | Public range provider; reject read-only and out-of-range values before dispatch, then compare the actual value. |
| `text` | String, up to 4096 characters | TextBox: focus, select all, routed text input (Delete for empty text), preserving input handling and length constraints. Other controls require a public value provider; no arbitrary setter fallback. |
| `selection` | Array of up to 32 complete fresh item targets | Public selection/item providers in the same window/container; add missing and remove extra items. Single selection uses the provider's select operation. |

Already satisfied state causes no focus, input or provider mutation. A multi-
selection request is a set, independent of item order. Required-selection and
single-selection constraints are enforced. All selected items must be available
through the public provider; standard selecting controls additionally compare
the realized provider count with the actual selected-index count. Unrealized or
over-limit selections cannot be presented as a complete verified set. Reveal
the intended items explicitly before retrying with a new observed intent.

Each dispatch checks visibility/enabled/modal/app-declared blockers, generation,
data-context identity and the last observed state. Focus/provider callbacks that
recycle the target or change state stop the old plan. Selection callbacks that
change additional items stop further changes and report the partial set. No
temporary style/property mutation is used to force input success.

Results contain typed `before`/`after` values, `status`, `verified`, dispatch count,
preparation effects, backend/route provenance, observation time and diagnostics.
Selection values are sorted live node IDs within the response's target window.
Statuses include `already_satisfied`, `verified`, `unsupported`, `not_verified`,
`partial` and `uncertain`. Missing state is explicit. Target validation errors
prevent a verified result even when the requested value is visible. CLI exit 1
and MCP `success:false` retain the partial response for all unverified outcomes.

Verification is immediate public UI state. It does not establish application
persistence, backend acceptance or eventual asynchronous state. Use existing
bounded state waits and application assertions for those postconditions.
Automation-provider and routed-event provenance do not claim native OS input.
These paths use the same public Avalonia runtime on Windows, Linux X11, macOS
and headless; native platform integration still requires separate validation.

## Lost responses and policies

Retain the exact payload and `requestId` until the outcome is known. Repeating it
retrieves the session's saved result with `replayed:true`, including its original
observation time; it does not re-observe or repeat input. A different payload
with the same ID fails. Cached results may describe historical state. A new ID
means a new intent and requires fresh observation after uncertainty.

The bridge reserves the ID before provider execution, retains up to 512 requests
for its session lifetime, and never evicts old IDs to make room for potentially
duplicate writes. At capacity new requests fail safely. The ledger is not durable
across application/session restart; an old target cannot address a new session.
Core never automatically retries this write operation. Client cancellation or a
lost transport response is not proof that nothing happened: retrieve the exact
same request or observe the application before continuing.

Bounds: 64 KiB request, 4096 text characters, 32 selected items, 64 provider/input
operations, and a cooperative two-second UI-thread budget. Public app callbacks
cannot be safely preempted and must remain fast. Unsupported, indeterminate and
uncertain outcomes never authorize automatic action replay.

When `policy` is supplied, authorize the session as usual and explicitly list
the properties in `allowedDesiredStates`; its default is empty. Existing read-
only policies do not gain control permissions. Redaction applies before results
are cached or sent, and excluded targets/ancestors are rejected. The operation
creates no evidence file, temporary mutation or external listener.

Public Avalonia 12.1.0 provider contracts:
[toggle](https://github.com/AvaloniaUI/Avalonia/blob/12.1.0/src/Avalonia.Controls/Automation/Provider/IToggleProvider.cs),
[selection](https://github.com/AvaloniaUI/Avalonia/blob/12.1.0/src/Avalonia.Controls/Automation/Provider/ISelectionProvider.cs),
[range value](https://github.com/AvaloniaUI/Avalonia/blob/12.1.0/src/Avalonia.Controls/Automation/Provider/IRangeValueProvider.cs),
[text value](https://github.com/AvaloniaUI/Avalonia/blob/12.1.0/src/Avalonia.Controls/Automation/Provider/IValueProvider.cs).
