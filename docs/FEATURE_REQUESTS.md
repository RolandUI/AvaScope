# AvaScope Feature Requests

This ledger stores product feature requests before they are selected for implementation.
Tickets in this area are backlog records only; do not start implementation unless the user explicitly asks for a ticket to be built.

## Storage Rules

- Store each ticket under `docs/feature-requests/FEAT-####-short-title.md`.
- Remove local usernames, machine names, personal directories, absolute paths, emails, tokens, and secrets before commit.
- Replace sensitive app-specific details with generic names when they are not required to preserve the product need.
- Preserve the user goal, affected AvaScope surface, expected behavior, priority, and suggested validation.
- Keep `Implementation Status` explicit. New backlog tickets default to `Not started`.
- Run `eng/validate-bug-reports.ps1` before committing intake changes.

## Priority Order

| Rank | Ticket | Priority | Status | Implementation Status |
| --- | --- | --- | --- | --- |
| 1 | [FEAT-0016](feature-requests/FEAT-0016-complex-agent-workflows.md) | P1 | Planned | Planned for v1.4.0 |
| 2 | [FEAT-0001](feature-requests/FEAT-0001-binding-resource-diagnostics.md) | P0 | Implemented | Covered by W9 |
| 3 | [FEAT-0002](feature-requests/FEAT-0002-layout-warnings.md) | P1 | Implemented | Covered by W9 |
| 4 | [FEAT-0003](feature-requests/FEAT-0003-computed-style-resource-inspector.md) | P2 | Implemented | Covered by W9 |
| 5 | [FEAT-0009](feature-requests/FEAT-0009-runtime-input-expansion.md) | P0 | Implemented | Covered by v0.6.0 |
| 6 | [FEAT-0010](feature-requests/FEAT-0010-scrollviewer-state-inspection.md) | P1 | Implemented | Covered by v0.6.0 |
| 7 | [FEAT-0011](feature-requests/FEAT-0011-runtime-binding-viewmodel-inspection.md) | P1 | Implemented | Covered by v0.6.0 |
| 8 | [FEAT-0012](feature-requests/FEAT-0012-opt-in-control-debug-state.md) | P2 | Implemented | Covered by v0.6.0 |
| 9 | [FEAT-0015](feature-requests/FEAT-0015-app-launch-helper.md) | P2 | Implemented | Covered by v0.6.0 |
| 10 | [FEAT-0013](feature-requests/FEAT-0013-runtime-session-selection.md) | P3 | Implemented | Covered by v0.6.0 |
| 11 | [FEAT-0014](feature-requests/FEAT-0014-screenshot-assertions-region-checks.md) | P3 | Implemented | Covered by v0.6.0 |
| 12 | [FEAT-0008](feature-requests/FEAT-0008-animation-diagnostics.md) | P4 | Implemented | Covered by v0.3.0 |

## Tickets

| ID | Title | Priority | Status | Implementation Status | Stored |
| --- | --- | --- | --- | --- | --- |
| [FEAT-0001](feature-requests/FEAT-0001-binding-resource-diagnostics.md) | Binding and resource diagnostics | P0 | Implemented | Covered by W9 | 2026-06-08 |
| [FEAT-0002](feature-requests/FEAT-0002-layout-warnings.md) | Layout warnings | P1 | Implemented | Covered by W9 | 2026-06-08 |
| [FEAT-0003](feature-requests/FEAT-0003-computed-style-resource-inspector.md) | Computed style and resource inspector | P2 | Implemented | Covered by W9 | 2026-06-08 |
| [FEAT-0004](feature-requests/FEAT-0004-automatic-design-size.md) | Automatic design-size recognition | P3 | Implemented | Covered by W2; revalidated by W9 | 2026-06-08 |
| [FEAT-0005](feature-requests/FEAT-0005-multi-size-preview.md) | Multi-size preview | P3 | Implemented | Covered by W9 | 2026-06-08 |
| [FEAT-0006](feature-requests/FEAT-0006-screenshot-diff-baseline.md) | Screenshot diff and baseline mode | P3 | Implemented | Covered by W9 | 2026-06-08 |
| [FEAT-0007](feature-requests/FEAT-0007-designer-session-cleanup.md) | Designer and session cleanup | P3 | Implemented | Covered by W9 | 2026-06-08 |
| [FEAT-0008](feature-requests/FEAT-0008-animation-diagnostics.md) | Animation diagnostics | P4 | Implemented | Covered by v0.3.0 | 2026-06-09 |
| [FEAT-0009](feature-requests/FEAT-0009-runtime-input-expansion.md) | Runtime input expansion | P0 | Implemented | Covered by v0.6.0 | 2026-06-10 |
| [FEAT-0010](feature-requests/FEAT-0010-scrollviewer-state-inspection.md) | ScrollViewer state inspection | P1 | Implemented | Covered by v0.6.0 | 2026-06-10 |
| [FEAT-0011](feature-requests/FEAT-0011-runtime-binding-viewmodel-inspection.md) | Runtime binding and ViewModel inspection | P1 | Implemented | Covered by v0.6.0 | 2026-06-10 |
| [FEAT-0012](feature-requests/FEAT-0012-opt-in-control-debug-state.md) | Opt-in custom control debug state | P2 | Implemented | Covered by v0.6.0 | 2026-06-10 |
| [FEAT-0013](feature-requests/FEAT-0013-runtime-session-selection.md) | Runtime session selection ergonomics | P3 | Implemented | Covered by v0.6.0 | 2026-06-10 |
| [FEAT-0014](feature-requests/FEAT-0014-screenshot-assertions-region-checks.md) | Screenshot assertions and region checks | P3 | Implemented | Covered by v0.6.0 | 2026-06-10 |
| [FEAT-0015](feature-requests/FEAT-0015-app-launch-helper.md) | App launch helper | P2 | Implemented | Covered by v0.6.0 | 2026-06-10 |
| [FEAT-0016](feature-requests/FEAT-0016-complex-agent-workflows.md) | Robust agent-driven workflows for complex Avalonia applications | P1 | Planned | Planned for v1.4.0 | 2026-09-02 |
