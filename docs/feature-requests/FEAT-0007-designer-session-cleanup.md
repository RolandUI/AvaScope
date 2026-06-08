# FEAT-0007: Designer and session cleanup

- Status: `Implemented`
- Implementation Status: `Covered by W9`
- Priority: `P3`
- Stored: `2026-06-08`
- Source Order: `7`
- Area: PreviewHost lifecycle, CLI, diagnostics

## User Need

Preview workflows should provide better lifecycle control so unnecessary AvaScope, preview, or designer-related `dotnet` processes do not remain after previews.

## Desired Behavior

- Improve automatic cleanup after preview operations.
- Add a focused cleanup command if there are known stale preview/session artifacts.
- Report stale sessions or processes through diagnostics where practical.
- Keep cleanup actions explicit and scoped so AvaScope does not terminate unrelated user processes.

## Acceptance Criteria

- Cleanup never kills arbitrary processes by name alone.
- Any process termination is scoped to AvaScope-owned child process metadata.
- Diagnostics can identify stale AvaScope-owned preview/session artifacts.
- The CLI exposes a clear cleanup workflow if automatic cleanup is insufficient.

## Current Coverage

- CLI `cleanup` and MCP `cleanup` delete only stale or invalid AvaScope-owned preview session JSON records from the local preview-session store.
- `diagnostics` includes preview-session diagnostics for available, stale, and invalid preview-session records.
- Stale records include closed/failed preview sessions and preview records whose latest successful output file is missing.
- Cleanup path checks keep deletion inside the configured AvaScope preview-session store.
- The implementation does not terminate processes because AvaScope does not yet persist reliable owned child-process metadata for post-hoc cleanup.

## Limitations

- No arbitrary process kill-by-name behavior is implemented.
- Future process termination must be based on AvaScope-owned child-process metadata, not `dotnet` process names.

## Notes

Existing session close flows cover some runtime and preview metadata cases; this ticket is specifically about stronger designer/preview lifecycle control and stale process handling.
