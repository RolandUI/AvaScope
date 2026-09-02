# FEAT-0016: Robust agent-driven workflows for complex Avalonia applications

- Status: `Planned`
- Implementation Status: `Planned for v1.4.0`
- Priority: `P1`
- Stored: `2026-09-02`
- Area: runtime bridge, Core workflows, CLI, MCP, evidence, security

## User Need

Complex Avalonia applications need deterministic end-to-end automation that can operate custom controls and multiple windows without caller-calculated coordinates, persisted runtime ids, fixed delays, or manual evidence collection.

AvaScope already provides semantic actions, bounded waits, launch/attach scenarios, idempotency, and partial evidence. The requested release extends those foundations instead of replacing them.

## Requested Capabilities

- Bounds-derived drag, swipe, long-press, and press-and-hold gestures.
- Discoverable application-defined actions for custom controls.
- Actionable selector filters and per-step selector re-resolution with safe stale-node recovery.
- Typed waits for visibility, enabled state, checked/selected/value/text changes, rendered bounds, command executability, binding values, and top-level lifecycle.
- Multi-window workflows with semantic top-level aliases.
- Conditional, retryable, optional, reusable, and variable-driven workflow composition.
- Optional observe-act-verify execution with structured PASS/FAIL results.
- Build-launch-attach-owned-cleanup lifecycle orchestration with captured logs.
- Automatic failure evidence exported as JSON, Markdown, and JUnit-compatible reports.
- Text/id redaction, screenshot masking, exclusions, retention, local storage, allowlists, and audit logs.

## Acceptance Criteria

- Standard and custom draggable controls can be operated semantically.
- One workflow can coordinate multiple application windows.
- Workflows wait on real application state and do not require fixed sleeps.
- Hidden, disabled, unrendered, ambiguous, or stale template nodes are not executed accidentally.
- Runtime node and top-level ids remain diagnostic rather than persisted workflow identities.
- Conditional and retryable steps preserve bounded execution and idempotency.
- Failures produce redacted screenshot and structured diagnostics when available.
- AvaScope can build or launch, attach to, and cleanly terminate only an owned application.
- The complete scenario runs repeatedly without human intervention.

## Safety Boundary

- Runtime inspection and application-defined actions remain explicitly opt-in and local-only.
- Actions are allowlistable, with destructive behavior disabled by default.
- Bridge integration can remain limited to development builds.
- Sessions cannot attach to unrelated processes without authorization.
- Process termination remains restricted to exact AvaScope-owned processes.
- Evidence is not uploaded over the network by default.

## GitHub Scope

The v1.4.0 vertical slices are tracked by [#99](https://github.com/RolandUI/AvaScope/issues/99) through [#108](https://github.com/RolandUI/AvaScope/issues/108), with release tracking in [#109](https://github.com/RolandUI/AvaScope/issues/109).
