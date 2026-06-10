# FEAT-0015: App launch helper

- Status: `Scheduled`
- Implementation Status: `Scheduled for v0.6.0`
- Priority: `P2`
- Stored: `2026-06-10`
- Source Order: `7`
- Area: CLI, runtime bridge, agent workflow

## User Need

AvaScope should provide a helper that launches a local app with the AvaScope bridge enabled, waits for a runtime session, and returns the identifiers needed for follow-up inspection.

## Desired Behavior

- Launch a configured project or executable with bridge activation settings.
- Wait for the active runtime session to appear.
- Return session id, top-level id when available, process id, stdout path, and stderr path.
- Time out with structured diagnostics if the app fails to start or no bridge session appears.

## Acceptance Criteria

- The helper does not require remote inspection or process injection.
- Bridge activation remains explicit and opt-in.
- Output capture paths are deterministic and under user-specified or AvaScope-owned directories.
- Follow-up CLI and MCP workflows can consume the returned session/top-level identifiers.

## Notes

This is intended for repeatable local agent workflows against bridge-enabled apps and should not weaken AvaScope's local-only security model.
