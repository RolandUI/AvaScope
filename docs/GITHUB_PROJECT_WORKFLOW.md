# AvaScope Project Workflow

GitHub Issues, Milestones and the [AvaScope Roadmap](https://github.com/users/RolandUI/projects/4) board are the only development tracking system. This document defines the process; it contains no current task list or release status.

## Information Ownership

| Information | Authoritative location |
| --- | --- |
| Problem, scope, acceptance, decisions and concise validation result | The relevant GitHub issue |
| Work status and priority | Issue labels and project fields |
| Release scope and remaining release work | GitHub milestone and release tracking issue |
| Published version, assets and user-facing changes | GitHub Release; version-specific release notes are its source text |
| Product behavior, contracts, architecture and security boundaries | Relevant technical documentation and executable tests |
| Validation procedure and selection | [VALIDATION.md](VALIDATION.md) |
| QA fixture operation, scenarios and retention | [AGENT_QA_LAB.md](AGENT_QA_LAB.md) |
| Temporary execution details and machine-specific configuration | Chat or verified ignored local storage |

Do not create or maintain Markdown backlogs, current-work checkpoints, campaign diaries, issue-status inventories or release histories. Do not copy issue results into another document. Before retiring a record, retain any still-relevant technical knowledge in its existing reference document and move unresolved work into the appropriate issue. Git history retains historical records; do not create an archive copy.

Update technical documentation when its behavior, contract or procedure changes, not after every execution. Keep a rule in its owning document and link to it elsewhere. Release notes explain user-visible changes and compatibility; they are not an agent activity log. Follow the [publication policy](../AGENTS.md#github-information-policy) for all text and attachments.

## Development Loop

1. Inspect the relevant issue, milestone and board. Select one issue and its current acceptance criteria. Search existing issues before creating another; keep at most one implementation issue in progress.
2. Set `status:in-progress` and the matching board state. Update missing or changed acceptance/validation details in the issue body; no routine start comment is needed.
3. Work directly on `master`. Do not create task branches, worktrees or pull requests. Preserve existing work; never force-push or rewrite history.
4. Make a small coherent change and run the [risk-appropriate checks](VALIDATION.md#choose-validation-by-risk). Review the diff and outgoing information, then commit and push directly to `master`.
5. Record one concise result in the issue with commit, validation outcome and necessary limitations. Comment earlier only for a material scope change or actionable blocker; no per-command, per-commit or unchanged-status reports.
6. Close only when the issue's acceptance criteria pass, with `status:done` and board Done. Use `status:review` for pending required validation and `status:blocked` for an external dependency. A commit may be pushed before acceptance is complete; do not claim missing checks passed.

For requested product work outside a milestone, create or update its issue first; assign a milestone only when release scope is agreed. Small explicitly requested documentation/policy corrections without product behavior changes need only a commit and validation summary; reuse a relevant issue if present, without creating bookkeeping tickets.

## Labels And Board

Use one status label and keep the board's `Workflow Status` aligned:

| Label | Workflow Status |
| --- | --- |
| `status:backlog` | Backlog |
| `status:ready` | Ready |
| `status:in-progress` | In Progress |
| `status:review` | Review |
| `status:done` | Done |
| `status:blocked` | Blocked |

Use one primary type: `type:feature`, `type:bug`, `type:release`, `type:ci` or `type:docs`. Add relevant `area:runtime`, `area:preview`, `area:visual-regression`, `area:cli`, `area:mcp` or `area:infra` labels. Priorities range from `priority:p0` (release/core blocker) through `priority:p1` (high), `priority:p2` (normal) and `priority:p3` (polish).

The existing board also has `Progress`, `Release Phase` and `Roadmap Order`; maintain coarse values only when work state changes. Keep the default `Status` field aligned with Todo/In Progress/Done. Completed items may be archived from the board while their issues remain available. Do not maintain another board-state table in the repository.

## Releases

Create one `type:release` tracker in the numeric release milestone `v<major>.<minor>.<patch>`. Keep scope, acceptance decisions and relevant evidence there. Prerelease candidates use that same milestone; record any specifically authorized acceptance gaps in the tracker. Follow [stable release validation](VALIDATION.md#stable-release-validation) for readiness, commit and publication checks.

Release authorization is separate from implementation completion. Close the tracker and milestone only after publication and verification, with remaining work explicitly moved or deferred in GitHub. A release does not resolve unrelated investigations or prove untested coverage.
