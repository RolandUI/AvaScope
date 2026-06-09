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
| 1 | [FEAT-0001](feature-requests/FEAT-0001-binding-resource-diagnostics.md) | P0 | Implemented | Covered by W9 |
| 2 | [FEAT-0002](feature-requests/FEAT-0002-layout-warnings.md) | P1 | Implemented | Covered by W9 |
| 3 | [FEAT-0003](feature-requests/FEAT-0003-computed-style-resource-inspector.md) | P2 | Implemented | Covered by W9 |

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
| [FEAT-0008](feature-requests/FEAT-0008-animation-diagnostics.md) | Animation diagnostics | P4 | Backlog | Not started | 2026-06-09 |
