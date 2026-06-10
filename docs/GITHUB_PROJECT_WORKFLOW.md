# AvaScope GitHub Project Workflow

GitHub is the source of truth for AvaScope project execution.

Use this document for issue, milestone, label, and project-board conventions. Keep `docs/DEVELOPMENT_PLAN.md` as a compact local handoff and validation log, not as the primary backlog.

## Source Of Truth

- Backlog and implementation work: GitHub Issues.
- Release grouping: GitHub Milestones named `v<version>`, for example `v0.7.0`.
- Active work state: `status:*` labels and the GitHub Project board.
- Roadmap intent and release scope: `docs/RELEASE_PLAN.md`.
- Local handoff and validation log: `docs/DEVELOPMENT_PLAN.md`.

## Required Agent Startup

Before starting meaningful implementation work:

1. Inspect the current milestone and ready issues:

   ```powershell
   gh issue list --repo RolandUI/AvaScope --milestone v0.7.0 --state open --json number,title,labels,milestone,url
   ```

2. Pick exactly one issue to work on.
3. Move it from `status:ready` or `status:backlog` to `status:in-progress`.
4. Add a short issue comment stating the implementation start and intended validation.
5. Mirror only the active issue and latest validation in `docs/DEVELOPMENT_PLAN.md`.

If the user explicitly asks for work outside the active milestone, create or update the GitHub issue first, then implement.

## Status Labels

Use exactly one status label on active backlog issues:

- `status:backlog`: accepted but not ready for active implementation.
- `status:ready`: ready for an agent to start.
- `status:in-progress`: currently being implemented.
- `status:review`: implementation is complete, validation or review is pending.
- `status:done`: completed and validated.
- `status:blocked`: blocked on external access, credentials, or a product decision.

Keep at most one issue marked `status:in-progress` unless the user explicitly asks for parallel work.

## Type, Area, And Priority Labels

Use one primary type label:

- `type:feature`
- `type:bug`
- `type:release`
- `type:ci`
- `type:docs`

Use one or more area labels:

- `area:runtime`
- `area:preview`
- `area:visual-regression`
- `area:cli`
- `area:mcp`
- `area:infra`

Use one priority label:

- `priority:p0`: blocks release or a core workflow.
- `priority:p1`: high priority release work.
- `priority:p2`: normal priority release work.
- `priority:p3`: lower priority or polish work.

## Milestones

Release milestones use `v<major>.<minor>.<patch>` names.

- A release tracking issue should be created for each active release.
- Each release milestone should contain vertical-slice issues such as `R0.7.0-M1 Baseline Suite Manifest`.
- Close the milestone only after the release is published and all milestone issues are closed or explicitly moved.

## Project Board

Use the public `AvaScope Roadmap` GitHub Project for human-readable roadmap state:

- https://github.com/users/RolandUI/projects/4

The project includes a `Workflow Status` single-select field with these values:

- Backlog
- Ready
- In Progress
- Review
- Done
- Blocked

Keep `status:*` labels aligned with `Workflow Status` when moving work. The default GitHub `Status` field is also populated so the built-in board view separates open Todo work from Done items.

## Completion Rules

When a slice is complete:

1. Run the issue-specific validation plus the repo validation appropriate for the changed files.
2. Commit and push the slice.
3. Add an issue comment with commit hash, validation commands, and outcome.
4. Replace `status:in-progress` or `status:review` with `status:done`.
5. Close the issue with reason `completed`.
6. Update `docs/DEVELOPMENT_PLAN.md` with the latest validation and next GitHub issue.

Release issues are closed only after the GitHub Release tag and assets exist.
