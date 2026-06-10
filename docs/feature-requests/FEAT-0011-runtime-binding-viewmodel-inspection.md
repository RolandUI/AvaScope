# FEAT-0011: Runtime binding and ViewModel inspection

- Status: `Scheduled`
- Implementation Status: `Scheduled for v0.6.0`
- Priority: `P1`
- Stored: `2026-06-10`
- Source Order: `3`
- Area: runtime inspection, binding diagnostics, protocol

## User Need

AvaScope should let users inspect runtime binding context and current bound values for a selected node so state-dependent UI bugs can be diagnosed directly.

## Desired Behavior

- Show the selected node's `DataContext` type.
- Show bound property paths for high-value inspectable properties where public APIs expose reliable binding metadata.
- Show current binding values where they can be read safely.
- Surface runtime binding errors and unavailable binding metadata explicitly.

## Acceptance Criteria

- Runtime `inspect_node` or a focused tool reports `DataContext` type for a selected node.
- Bound property path/value output is bounded and avoids dumping arbitrary object graphs.
- Sensitive values are not exposed by default beyond explicit selected-node inspection.
- Unsupported binding metadata returns `unknown` or `not_available` instead of inferred data.

## Notes

This complements preview binding diagnostics. It is runtime-focused and must preserve the local-only bridge boundary.
