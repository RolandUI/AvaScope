# FEAT-0006: Screenshot diff and baseline mode

- Status: `Backlog`
- Implementation Status: `Not started`
- Priority: `P3`
- Stored: `2026-06-08`
- Source Order: `6`
- Area: CLI, future CI workflow, image comparison

## User Need

A user should be able to compare the current preview screenshot with a previous preview or baseline image so UI regressions are visible quickly.

## Desired Behavior

- Accept a baseline screenshot path or previous preview record.
- Compare current and baseline images with configurable tolerance.
- Emit a diff image or structured summary of changed regions.
- Return pass/fail status suitable for local validation and future CI workflows.

## Acceptance Criteria

- Diff mode handles same-size images deterministically.
- Dimension mismatches return structured diagnostics.
- Diff artifacts are written to explicit output paths.
- The first implementation avoids destructive baseline updates unless explicitly requested.

## Notes

This should remain opt-in because visual diffs can generate artifacts and may become CI-sensitive later.
