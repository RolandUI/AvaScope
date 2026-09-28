# AvaScope AGENTS.md

AvaScope is an agent-focused local control plane for Avalonia apps.

Keep this file as the agent entry point and durable product context. Work state belongs in GitHub.

Target Avalonia line: `Avalonia 12`. Do not default implementation work to Avalonia 11.x guidance. Avalonia-facing projects should target `net10.0` by default and use the latest stable Avalonia 12.x patch unless a narrower compatibility target is explicitly required. When package versions, APIs, or breaking changes matter, verify against current Avalonia 12 sources or official documentation before implementing.

## Agent Ownership

Development in this repository is expected to be performed 100% by autonomous coding agents.

- Agents own implementation, test writing, build validation, test validation, documentation updates, commits, pushes, and handoff notes end-to-end.
- Do not leave routine coding, validation, formatting, commit, or push work for the user.
- If a task requires a decision that cannot be derived from this file or the codebase, make the smallest reasonable product-aligned choice and record non-obvious decisions in docs.
- If external credentials, account access, publishing permissions, or product decisions block completion, state the blocker precisely and stop at the nearest validated state.
- Each meaningful change should include relevant tests or an explicit validation note explaining why tests are not applicable.

## Development Entry Point

Read [the project workflow](docs/GITHUB_PROJECT_WORKFLOW.md) before implementation and follow the selected GitHub issue and milestone. It owns branch/commit rules, issue and board transitions, and documentation ownership. [Validation](docs/VALIDATION.md) owns risk-based check selection and release acceptance. Keep at most one implementation issue in progress.

For local QA, follow [the lab operating and retention policy](docs/AGENT_QA_LAB.md#local-testing-and-retention-policy). Before QA or cleanup, read any existing machine-specific handoff at the path returned by `git rev-parse --git-path info/avascope-local.md`. This optional file stays in Git metadata; never commit or upload it. Report release status in chat and do not recreate cancelled status automations.

## GitHub Information Policy

GitHub records the development rationale and verified result, not the agent's local work diary. This policy applies to tracked files (including this file), commit messages, issues, comments, pull requests, project updates, release notes and uploaded evidence. It supersedes older reporting instructions that request more detail.

- Publish only information needed to understand the problem, scope, solution, important decisions, validation outcome and remaining limitations. Include minimal reproduction steps when useful, with relevant commit, source or CI links.
- Never publish workstation-specific absolute paths, local usernames, hostnames, private URLs, local session/process identifiers, temporary QA locations, unrelated machine inventory or personal notification/account details. Use repository-relative paths or neutral placeholders such as `<repo>` and `<qa-root>`; retain OS/runtime/version details only when they explain behavior or validation coverage.
- Keep raw tool responses, full logs, repeated screenshots, command transcripts, cleanup histories and step-by-step agent activity local. If evidence is necessary, publish only the relevant sanitized excerpt or image. Inspect attachments, generated reports and staged diffs as well as prose before publishing; a file being generated or ignored locally does not make it safe to upload.
- Keep machine-specific instructions and transient handoff state in chat or verified untracked/ignored local storage. A tracked file is published documentation even when named a plan, report or handoff. Do not create another permanent work diary.
- Report meaningful outcomes, scope changes or actionable blockers. Use labels/project fields for ordinary state transitions; no mandatory start comments, per-commit reports, unchanged status updates or duplicated results across issues and docs.
- Keep validation failures, uncertainty and missing coverage explicit when they affect acceptance. Concision must not turn an unverified result into a passing claim.
- These are publication rules, not changes to AvaScope's local tool response or evidence contracts. Historical cleanup is a separately scoped task; adding this policy does not remove prior GitHub content or Git history.

## Product And Architecture

AvaScope helps agents inspect, preview, control and validate local Avalonia applications through structured CLI and MCP calls. Keep the product name `AvaScope`, CLI command and MCP server name `avascope`.

- `AvaScope.Protocol` owns transport-neutral DTOs and versioned JSON contracts.
- `AvaScope.Core` owns reusable runtime/preview clients, sessions, workflows and evidence handling. CLI and MCP remain thin adapters over it.
- `AvaScope.Bridge` is explicitly activated in the host, through package integration or the external provider.
- `AvaScope.PreviewHost` builds/loads project views and renders through the real Avalonia runtime in an isolated child process.
- `AvaScope.Installer` and `eng/installer` own distribution; `samples` and `tests` exercise public behavior.

Use the [user guide](docs/USER_GUIDE.md) for usage and [stable surface](docs/STABLE_SURFACE.md) for supported commands, tools and compatibility. Future scope belongs in issues, not a second roadmap here.

## Technical Boundaries

- Prefer the real Avalonia runtime and public APIs over custom XAML interpretation or private hooks. Keep UI access on `Dispatcher.UIThread`.
- Keep Core reusable outside MCP; do not couple public schemas to Avalonia internals. Bound tree depth, response size and operation lifetimes; return explicit unavailable evidence rather than guesses.
- Keep protocols versioned, with regression coverage for contracts, process ownership and session lifecycle. Verify relevant current Avalonia/MCP SDK APIs before implementing against them.
- Keep bridge activation explicit, local-only and disabled by default in production. Select projects/sessions explicitly; never add unauthenticated remote inspection or implicit process injection.
- Loading project code is an execution boundary. Preserve PreviewHost isolation and the [security threat model](docs/SECURITY_THREAT_MODEL.md), including action authorization, evidence privacy and owned cleanup.
- Runtime mutations are bounded reversible experiments. Source suggestions do not authorize automatic source edits.
- Do not add Robot Framework/PlatynUI interoperability, foreign workflow import or a Python/Robot adapter distribution. Implement useful capabilities through AvaScope's own contracts.
- Prefer small working changes, existing patterns and explicit names. Do not generalize assumptions from one fixture to all Avalonia projects.
