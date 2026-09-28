# AvaScope GitHub Project Workflow

GitHub is the source of truth for AvaScope project execution.

Use this document for issue, milestone, label, and project-board conventions. Follow the [GitHub information policy](../AGENTS.md#github-information-policy) for every published surface, including tracked documentation and attachments. Keep `docs/DEVELOPMENT_PLAN.md` as a compact public checkpoint, not a local work diary or the primary backlog.

## Source Of Truth

- Backlog and implementation work: GitHub Issues.
- Release grouping: GitHub Milestones named `v<version>`, for example `v0.7.0`.
- Active work state: `status:*` labels and the GitHub Project board.
- Roadmap intent and release scope: `docs/RELEASE_PLAN.md`.
- Current release, active work and next-step links: `docs/DEVELOPMENT_PLAN.md`.
- Machine-specific configuration and transient working notes: chat or verified untracked/ignored local storage, never tracked documentation.

## What To Publish

An issue or completion update should explain the problem and impact, the resulting change and necessary rationale, the validation outcome, and any remaining acceptance limitation. Link the relevant commit/test/CI result; include only the reproduction steps or sanitized evidence needed to assess it.

Use repository-relative source paths and portable commands. Replace machine-specific locations with neutral placeholders such as `<repo>` or `<qa-root>`. Do not publish workstation paths, usernames, hostnames, private links, local session/process identifiers, temporary directories, raw tool payloads, full logs, notification/account details or cleanup transcripts. Keep environment details only when needed to reproduce or interpret the result. Inspect screenshots and attachments for the same information before uploading; do not attach a whole local evidence bundle by default.

The same rules apply to commit messages, PRs, release notes, project fields and versioned reports. A local artifact path is not durable evidence for another reader. Prefer a regression test or relevant CI link; use a minimal sanitized excerpt/image when that is needed to explain the defect. Do not hide failing checks or uncertain coverage when summarizing.

## Update Frequency

- Labels and project fields record ordinary work-state changes. A start or unchanged-status comment is unnecessary.
- Add one result per completed coherent slice, with its commit and concise validation outcome. Do not report every edit, command, test attempt or commit separately.
- Before completion, comment only when scope/acceptance materially changes or an actionable blocker needs attention. Edit the existing description for corrections and current acceptance requirements instead of appending repetitive updates.
- Give each result one authoritative home. The development plan and release trackers should link to it, not repeat the narrative. Replace stale checkpoint content rather than append a growing history.
- Small documentation/policy corrections explicitly requested by the owner, without product behavior changes, can be recorded by their commit and validation summary. Reuse a relevant issue if one exists; do not create a ticket, milestone or plan entry only for bookkeeping.

## Required Agent Startup

All implementation commits and pushes go directly to `master`. Do not create task branches, worktrees or pull requests. Review the local diff before committing; preserve existing work and never force-push or rewrite history.

Before starting issue-backed implementation work:

1. Inspect the current milestone and ready issues:

   ```powershell
   gh issue list --repo RolandUI/AvaScope --state open --json number,title,labels,milestone,url
   ```

2. Pick exactly one issue to work on.
3. Move it from `status:ready` or `status:backlog` to `status:in-progress`.
4. Select local, targeted CI or full CI using the [risk-based validation rules](VALIDATION.md#choose-validation-by-risk). Check the issue's acceptance description and update it only if missing or changed. Do not add a routine start comment.
5. Update `docs/DEVELOPMENT_PLAN.md` only if its current-work checkpoint changes, using issue/result links and a short next step.

For product work outside the active milestone, create or update the relevant issue first. Do not assign a release milestone without release scope. The small documentation/policy exception above does not require an issue.

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

The active board should stay focused on open release work. Completed historical items may be archived from the project after their GitHub issues and milestones remain closed.

The project includes these board-support fields:

- `Workflow Status`: kanban column/status field.
- `Progress`: coarse completion marker for cards.
- `Release Phase`: release tracker, current slice, planned slice, or completed work.
- `Roadmap Order`: numeric ordering inside a release.

`Workflow Status` values:

- Backlog
- Ready
- In Progress
- Review
- Done
- Blocked

Keep `status:*` labels aligned with `Workflow Status` when moving work. The default GitHub `Status` field is also populated so the built-in board view separates open Todo work from Done items.

GitHub's public Projects API currently supports project fields and items, but not saved view layout creation or saved view layout edits. If a board view is missing in the GitHub UI, create it manually with:

- Layout: `Board`
- Column/group field: `Workflow Status`
- Sort: `Roadmap Order`
- Visible card fields: `Progress`, `Release Phase`, `Milestone`, `Labels`

## Completion Rules

When a slice is complete:

1. Run the issue-specific validation at the level justified by the change and its dependents. Neither completion of a slice/batch nor issue closure requires full CI by default. Documentation-only edits use the focused checks in [validation](VALIDATION.md).
2. Review the staged diff, commit message and any outgoing text/attachments against the information policy, then commit and push the locally validated slice directly to `master` without a branch or PR.
3. Record one concise issue result with commit, validation outcome and any remaining limitation. Include portable validation commands only when needed for reproduction; do not duplicate an existing result comment.
4. If required acceptance evidence is still pending, keep `status:review` (or `status:blocked` for an external dependency) and leave the issue open. A commit may be pushed before a required hosted check completes; do not present it as fully accepted.
5. Once the issue's acceptance criteria pass, set `status:done` and close with reason `completed`. Local validation is sufficient when it establishes all required acceptance; a full CI run is not a universal prerequisite.
6. Update `docs/DEVELOPMENT_PLAN.md` only if the active work, release or next step changed. Keep a current checkpoint with links, not the execution history.

Release issues are closed only after the exact release candidate has complete applicable validation and the GitHub Release tag and assets exist. Release authorization and readiness are separate from committing or closing an implementation issue.
