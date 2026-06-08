# FEAT-0005: Multi-size preview

- Status: `Implemented`
- Implementation Status: `Covered by W9`
- Priority: `P3`
- Stored: `2026-06-08`
- Source Order: `5`
- Area: CLI, MCP, PreviewHost orchestration

## User Need

A user should be able to render multiple viewport sizes from one command or request, for example `1440x900`, `1280x720`, and `900x700`.

## Desired Behavior

- Accept multiple viewport sizes in a single CLI or MCP preview request.
- Render either separate screenshots or a combined contact sheet.
- Preserve theme, culture, design data, and project/view settings across all requested sizes.
- Return structured result entries for each rendered size.

## Acceptance Criteria

- Multi-size preview output paths are deterministic and collision-resistant.
- Partial failures are represented per size without losing successful renders.
- Contact sheet generation is optional and does not replace individual screenshots unless explicitly requested.
- Single-size preview remains compatible.

## Current Coverage

- CLI `preview --sizes 1440x900,1280x720` renders multiple isolated preview requests from one command.
- MCP exposes `preview_axaml_multi` over the same Core batch renderer.
- Output file names include the requested index and size, while preserving the caller's base output path.
- Each size returns a `PreviewBatchEntry` with its own structured `ToolResult<PreviewResponse>`, so partial failures do not discard successful renders.
- Optional contact-sheet generation writes a separate combined PNG and leaves individual screenshots in place.

## Notes

This is useful for responsive desktop layout validation and should likely reuse the existing isolated PreviewHost process boundary.
