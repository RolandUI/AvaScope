# FEAT-0002: Layout warnings

- Status: `Backlog`
- Implementation Status: `Not started`
- Priority: `P1`
- Stored: `2026-06-08`
- Source Order: `3`
- Area: PreviewHost diagnostics, rendered layout analysis

## User Need

AvaScope should automatically identify common UI layout problems during preview rendering so agents can catch visual defects without manually inspecting every screenshot.

## Desired Behavior

Report warnings for:

- clipped or overflowing text
- text truncation
- overlapping elements
- clipped content
- content unreachable without scrolling
- hit targets that are too small

## Acceptance Criteria

- Preview diagnostics include bounded layout warning entries with severity, category, and affected node information where practical.
- Text clipping and truncation detection works for common text controls.
- Overlap detection avoids obvious false positives from intentional overlays where possible.
- Hit target checks use an explicit minimum target size policy documented with the implementation.
- Warnings do not block screenshot generation unless rendering itself fails.

## Notes

This is the second-priority requested feature and should likely build on visual tree bounds plus rendered output analysis.
