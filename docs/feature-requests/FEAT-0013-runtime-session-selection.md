# FEAT-0013: Runtime session selection ergonomics

- Status: `Scheduled`
- Implementation Status: `Scheduled for v0.6.0`
- Priority: `P3`
- Stored: `2026-06-10`
- Source Order: `5`
- Area: runtime attach, diagnostics, CLI, MCP

## User Need

When multiple local bridge sessions or stale records exist, users should be able to select the intended active session quickly without manually copying session ids.

## Desired Behavior

- Select the latest active session by display name or process id.
- Hide stale sessions by default in ergonomic selection flows while still exposing them in diagnostics.
- Support a concise workflow equivalent to `diagnostics --latest --display-name <name>` or attach-by-latest matching display name.
- Preserve explicit session id and manifest path targeting for deterministic workflows.

## Acceptance Criteria

- Latest-session selection never silently chooses among ambiguous equivalent candidates.
- Stale sessions are excluded from default latest selection unless explicitly requested.
- Diagnostics explain why no latest match or multiple matches were found.
- CLI and MCP attach/diagnostics flows stay consistent.

## Notes

This refines the existing runtime attach and cleanup workflows rather than replacing explicit targeting.
