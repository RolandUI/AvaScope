# FEAT-0012: Opt-in custom control debug state

- Status: `Implemented`
- Implementation Status: `Covered by v0.6.0`
- Priority: `P2`
- Stored: `2026-06-10`
- Source Order: `4`
- Area: bridge package, runtime inspection, protocol

## User Need

Custom controls should be able to expose app-owned debug values to AvaScope without AvaScope knowing the control's domain model.

## Desired Behavior

- Provide an opt-in contract or registration hook for controls or view models to expose bounded debug state.
- Return custom debug fields during node inspection or a focused debug-state query.
- Keep debug state local-only and explicitly enabled by the app.
- Avoid reflection over arbitrary private state unless the app opts into a safe public contract.

## Acceptance Criteria

- A sample custom control can expose structured debug values through the bridge.
- Debug fields are bounded by count and serialized size.
- The protocol distinguishes app-provided debug state from AvaScope-derived diagnostics.
- No debug state is exposed unless the app explicitly opts in.

## Notes

Motivating examples include plot bounds, scaled ranges, visible item ranges, render counters, and aggregate statistics, but the ticket should remain generic.

## Implementation Notes

`v0.6.0` adds `IAvaScopeDebugStateProvider` in the bridge package and returns bounded app-provided `debugState` fields during runtime node inspection only for controls that explicitly implement the contract.
