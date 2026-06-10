# FEAT-0010: ScrollViewer state inspection

- Status: `Scheduled`
- Implementation Status: `Scheduled for v0.6.0`
- Priority: `P1`
- Stored: `2026-06-10`
- Source Order: `2`
- Area: runtime inspection, diagnostics, protocol

## User Need

AvaScope should expose enough `ScrollViewer` state to diagnose scroll, scaling, and clipped-content bugs without inferring everything from screenshots.

## Desired Behavior

- Inspect `Offset`, `Extent`, `Viewport`, and scrollbar visibility for a selected `ScrollViewer`.
- Expose relevant child bounds, desired size, and arranged size for selected child content.
- Include this state through bounded structured output from node inspection or a focused scroll inspection tool.

## Acceptance Criteria

- A selected scroll container reports current horizontal and vertical scroll metrics where available.
- Child layout metrics are bounded and tied to stable node references where practical.
- Missing or unsupported metrics return explicit `not_available` values instead of guesses.
- CLI and MCP adapters preserve the structured output.

## Notes

This ticket is generic even though the motivating case is chart scroll and scaling diagnostics.
