# FEAT-0014: Screenshot assertions and region checks

- Status: `Scheduled`
- Implementation Status: `Scheduled for v0.6.0`
- Priority: `P3`
- Stored: `2026-06-10`
- Source Order: `6`
- Area: screenshot, diff, visual assertions, CLI, MCP

## User Need

AvaScope should support focused screenshot checks for agent validation, not only whole-image diffing.

## Desired Behavior

- Crop or evaluate named/coordinate regions from a screenshot.
- Assert simple pixel conditions such as non-empty region, mostly blank region, changed region bounds, and minimum changed-pixel count.
- Return structured pass/fail output suitable for CLI and MCP workflows.
- Build on existing screenshot diff and baseline primitives without mutating baselines automatically.

## Acceptance Criteria

- Region crop/check commands produce deterministic artifact paths when requested.
- Pixel assertions handle empty, blank, changed, and unchanged regions with bounded metrics.
- Invalid regions and dimension mismatches return structured diagnostics.
- Existing full-image `diff`, `baseline-create`, and `baseline-check` behavior remains compatible.

## Notes

This is a v0.6 targeted extension of the existing visual comparison foundation; broader CI report productization remains in the v0.7 roadmap.
