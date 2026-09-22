# Action blockers and activation points

Call `explain_action` (CLI `explain-action --request action.json`) after finding
or inspecting an explicit target. Capability id: `runtime.action_explanation`.
The bridge uses public Avalonia 12 state on the UI thread and performs no input,
focus, reveal, modal dismissal or property mutation.

```json
{
  "sessionId": "the-selected-session",
  "topLevelId": "the-selected-window",
  "nodeId": "the-current-visual-node",
  "action": "click",
  "strategy": "synthetic",
  "maxReasons": 24
}
```

Supply the previous inspection's `target` to reject stale node generations.
Optional paired finite `x/y` explicitly chooses a point in the top-level's DIPs.
Otherwise the bridge prefers an app-declared local point and falls back to the
current bounds center. Nothing is clicked during analysis.

The response separates these evidence classes:

| certainty | Meaning |
| --- | --- |
| `proven` | Public state establishes the reported fact, such as a disabled ancestor, command denial, modal child, clip or different input hit target. |
| `correlated` | A target, explicitly related field, or bounded same-window field has validation errors. This does not prove why Save is disabled. |
| `app_declared` | The application explicitly supplied availability or a business explanation. AvaScope did not infer or independently prove its business logic. |
| `unknown` | Evidence is missing, unsupported, failed or incomplete. |

Each reason includes `blocksAction`, source, a related runtime target where
available, and a suggested next inspection/recovery action. The tool does not
execute those suggestions. `Command.CanExecute=false` proves denial, not a
business explanation. `no_observed_blocker` is not a guarantee of execution or
task success; full input parameters and the requested route still need validation.
Use existing workflow `validate_action` for full parameter validation and
`inspect_node` / `explain_layout` for additional target detail.

The existing input validator and dispatch reject disabled/hidden targets,
app-declared denial and modal owners, and direct callers to this explanation.
A semantic invoke does not require a clickable point; clipping/obstruction
observations only block pointer actions. Unsupported strategy/action pairs are
explicit. Native keyboard/text parameters and platform restrictions still apply.

## Host evidence

A custom control or its Avalonia automation peer can implement
`IAvaScopeActionContextProvider`:

```csharp
public AvaScopeActionContext GetActionContext(string action) => new(
    ActivationPoint: new Avalonia.Point(8, 12),
    CanExecute: CanSave,
    BusinessReasons: CanSave ? [] : ["Choose a destination before saving."],
    RelatedValidationTargets: [DestinationEditor]);
```

This optional host extension uses an AvaScope compile-time reference. Ordinary
reflection-loaded hosts still work with public runtime state and the explicit
bounds-center fallback; this extension is not required for bridge integration.
The point is relative to the target control in DIPs, including when its peer
implements the interface. Read callbacks must be fast and side-effect free.
Only same-window related controls are accepted. A throwing provider yields
unavailable evidence and blocks dispatch rather than substituting another point.

## Point validation and dispatch

`activationPoint` includes `source`, `coordinateSpace: top_level_dip`, scale,
current hit target and `geometryRevision`. Validation checks finite positive
geometry, current layout, visibility, ancestor bounds/geometric clips and
Avalonia's public input hit test (including `IsHitTestVisible`). It returns
`valid`, `clipped`, `obstructed`, `invalid` or `unavailable`.

For an explicit pointer request, pass the revision from a valid analysis:

```json
{
  "strategy": "synthetic",
  "expectedGeometryRevision": "the-returned-64-character-sha256"
}
```

Keep the same target and coordinate choice. The revision covers target identity,
current transforms/bounds/clips, window position/client size and scale. Dispatch
rechecks the point on the UI thread immediately before the first pointer event
and each subsequent click group. Changed geometry, a new overlay or unavailable
layout rejects remaining dispatch. A partially dispatched operation keeps its
actual dispatch count and releases only its own held input; it is never
automatically replayed. Legacy target-based click also uses the declared point.
Legacy gesture-specific paths retain their documented directional/provider rules.
Workflow export replaces recorded geometry revisions with required parameters;
replay must bind fresh `explain_action` evidence instead of retaining the old
session's revision or discarding the dispatch guard.

`nativeConfirmation: unavailable` explicitly means the point has not been proven
visible to the OS. Avalonia hit testing cannot establish whether another process,
native surface or compositor is covering it. This boundary applies to Windows,
Linux X11, macOS and unvalidated backends. An unknown native fact is not a positive
visibility assertion, and a headless test is not native desktop coverage.

## Bounds and redaction

Requests allow 1–32 reasons (default 24), 32 ancestors, 64 owned child windows, 16 declared related fields,
8 declared business reasons, 4 messages per validation field and a 256-node
same-window validation sample. Text evidence is limited to 512 characters.
`truncated` means omitted/incomplete evidence; it does not claim a complete
causal inventory. Application callbacks cannot be forcibly preempted safely.

The optional existing `policy` enforces explicit session/process authorization,
inspect permission, sensitive-text redaction and excluded-control handling before
CLI/MCP serialization. This read-only tool creates no report directories. A
policy-excluded target fails explicitly, and excluded related controls/ancestors
are omitted. Retain uncertainty when evidence is suppressed.

Tests cover disabled ancestry, command denial, related/unrelated validation,
modality, an irregular clipped button with a declared point, overlays including
non-hit-testable visuals, invalid points, scaling/window movement, stale node and
geometry identity, a partial double click, unavailable native confirmation,
provider failure and redacted CLI/MCP parity.

API sources: Avalonia 12.1.0
[Window modality](https://github.com/AvaloniaUI/Avalonia/blob/12.1.0/src/Avalonia.Controls/Window.cs),
[public input hit testing](https://github.com/AvaloniaUI/Avalonia/blob/12.1.0/src/Avalonia.Base/Input/InputExtensions.cs)
and [visual coordinates](https://github.com/AvaloniaUI/Avalonia/blob/12.1.0/src/Avalonia.Base/VisualTree/VisualExtensions.cs).
No foreign automation framework integration is involved.
