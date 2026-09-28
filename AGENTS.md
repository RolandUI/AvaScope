# AvaScope AGENTS.md

AvaScope is an agent-focused local control plane for Avalonia apps.

This repository starts from an empty project. Preserve this file as the project context for future agents.

Target Avalonia line: `Avalonia 12`. Do not default implementation work to Avalonia 11.x guidance. Avalonia-facing projects should target `net10.0` by default and use the latest stable Avalonia 12.x patch unless a narrower compatibility target is explicitly required. When package versions, APIs, or breaking changes matter, verify against current Avalonia 12 sources or official documentation before implementing.

## Agent Ownership

Development in this repository is expected to be performed 100% by autonomous coding agents.

- Agents own implementation, test writing, build validation, test validation, documentation updates, commits, pushes, and handoff notes end-to-end.
- Do not leave routine coding, validation, formatting, commit, or push work for the user.
- If a task requires a decision that cannot be derived from this file or the codebase, make the smallest reasonable product-aligned choice and record non-obvious decisions in docs.
- If external credentials, account access, publishing permissions, or product decisions block completion, state the blocker precisely and stop at the nearest validated state.
- Each meaningful change should include relevant tests or an explicit validation note explaining why tests are not applicable.

## Current Working And QA Policy

Owner policy updated on 2026-09-28:

- Commit and push directly to `master`. Do not create task branches, worktrees or pull requests. Preserve existing work; never force-push or rewrite history.
- Develop in small changes: reproduce the defect where applicable, add a relevant regression, build affected components and run focused tests plus the affected CLI/MCP or native journey. Include dependent behavior in the scope; reuse valid builds and rebuild only affected components.
- Select validation by risk using `docs/VALIDATION.md`: local checks by default, targeted CI for necessary coverage unavailable locally, full CI before release or when cross-cutting risk cannot be covered by narrower checks. Completing a commit, issue or batch does not itself require full CI or a full local solution test run.
- Dispatch CI deliberately from `master` only for an identified validation gap. State the source revision, relevant platforms/tests and reason briefly in the existing work context; do not add a separate reporting ritual. Do not introduce automatic push, PR or scheduled validation. Workflow alignment is tracked in `docs/DEVELOPMENT_PLAN.md`; existing YAML triggers are not authority to create PRs or run unnecessary gates.
- Separate commit/push, issue acceptance and release readiness. Push locally validated coherent changes; close an issue when its specific acceptance criteria pass, without a blanket full-CI requirement. Keep missing required platform evidence pending and never imply that a later commit was covered by an earlier run.
- Reuse successful checks only while their relevant source, tests, dependencies, configuration and environment remain applicable. On failure, diagnose and rerun the affected checks first; repeat a full gate only when the remaining risk requires it. Continue independent local work during a necessary CI run.
- Report release status in chat; do not recreate previously cancelled status automations.
- Local testing uses one reusable binary set and one current output directory. Keep its absolute location in local context, never in tracked documentation. Do not accumulate run directories or move the same accumulation to another drive.
- Before local QA or cleanup, read any existing machine-specific handoff at the path returned by `git rev-parse --git-path info/avascope-local.md`. This optional file stays in Git metadata and must never be committed or uploaded; preserve local ownership and cleanup restrictions there when needed.
- Reuse the selected CLI/MCP/client, Direct host, standalone host and provider across cases. Record their source revision once and rebuild only affected components when source changes. Do not copy or hash whole build/runtime trees for every case. Stop owned apps before replacing binaries.
- After checking a case, discard successful raw responses, repeated screenshots and temporary render/build outputs. After a defect is fixed and validated, delete its local historical artifacts. GitHub issues, commits and regression tests are the lasting record; no local archive is required.
- While an issue or batch is still open, keep only the minimal failing request/response, relevant log excerpt and necessary image or TRX. Do not keep runtime dependencies, PDB trees, package copies or all prior successful runs as evidence. Delete pending batch artifacts when that batch closes.
- Keep the reusable local QA area below 8 GiB. Check usage before builds/downloads, clear completed outputs first, and do not start another large run when that budget would be exceeded. Download CI artifacts individually for inspection, then delete each unpacked artifact after recording the result.
- Before recursive cleanup, verify exact absolute targets belong to the selected AvaScope QA area and that no live process uses them. Do not clean other projects, global caches, installed AvaScope or user data.

This policy supersedes older blanket requirements for CI per batch or full local tests per slice, as well as campaign instructions to preserve every attempt or create a fresh binary snapshot for each charter. Specific acceptance and release evidence requirements still apply. Historical local artifact links may intentionally be unavailable after cleanup; do not recreate them merely for archival completeness.

## GitHub Information Policy

GitHub records the development rationale and verified result, not the agent's local work diary. This policy applies to tracked files (including this file and `docs/DEVELOPMENT_PLAN.md`), commit messages, issues, comments, pull requests, project updates, release notes and uploaded evidence. It supersedes older reporting instructions that request more detail.

- Publish only information needed to understand the problem, scope, solution, important decisions, validation outcome and remaining limitations. Include minimal reproduction steps when useful, with relevant commit, source or CI links.
- Never publish workstation-specific absolute paths, local usernames, hostnames, private URLs, local session/process identifiers, temporary QA locations, unrelated machine inventory or personal notification/account details. Use repository-relative paths or neutral placeholders such as `<repo>` and `<qa-root>`; retain OS/runtime/version details only when they explain behavior or validation coverage.
- Keep raw tool responses, full logs, repeated screenshots, command transcripts, cleanup histories and step-by-step agent activity local. If evidence is necessary, publish only the relevant sanitized excerpt or image. Inspect attachments, generated reports and staged diffs as well as prose before publishing; a file being generated or ignored locally does not make it safe to upload.
- Keep machine-specific instructions and transient handoff state in chat or verified untracked/ignored local storage. A tracked file is published documentation even when named a plan, report or handoff. Do not create another permanent work diary.
- Report meaningful outcomes, scope changes or actionable blockers. Use labels/project fields for ordinary state transitions; no mandatory start comments, per-commit reports, unchanged status updates or duplicated results across issues and docs.
- Keep validation failures, uncertainty and missing coverage explicit when they affect acceptance. Concision must not turn an unverified result into a passing claim.
- These are publication rules, not changes to AvaScope's local tool response or evidence contracts. Historical cleanup is a separately scoped task; adding this policy does not remove prior GitHub content or Git history.

## GitHub Project Workflow

GitHub Issues, Milestones, and the `AvaScope Roadmap` Project board are the primary project-management and progress-tracking source for this repository. `docs/DEVELOPMENT_PLAN.md` is a compact, publishable checkpoint with current work and links to authoritative results, not a local log or duplicate backlog.

- Every agent must inspect the relevant GitHub issue, GitHub milestone, `docs/GITHUB_PROJECT_WORKFLOW.md`, and `docs/DEVELOPMENT_PLAN.md` before starting meaningful implementation work.
- Development must follow the selected GitHub issue's scope, acceptance criteria, milestone, labels, and validation notes unless the requested task explicitly changes that scope.
- Keep at most one issue marked `status:in-progress` unless the user explicitly asks for parallel work.
- Before implementation, move the selected issue to `status:in-progress`. Record intended validation in the issue's existing scope/acceptance description only if it is missing or changed; do not add a routine start comment.
- At a completed coherent slice, record one concise issue result with commit, validation outcome and any necessary decision or limitation. Comment earlier only for a material scope change or actionable blocker. Update `docs/DEVELOPMENT_PLAN.md` only when its current-work, release or next-step checkpoint changes; link to the result instead of copying it.
- For a small, explicitly requested documentation or policy correction with no product behavior change, a commit and validation summary are sufficient. Do not create an issue, milestone, start/completion comment or plan entry solely to log the agent's activity; use a relevant existing issue if there is one.
- If GitHub issues or milestones conflict with tracked docs, treat GitHub as current and correct the stale summary. Record a necessary explanation once in the authoritative issue, without duplicating the work history.
- Do not close an issue until its acceptance criteria and validation commands have passed.
- Commit and push each completed vertical slice or coherent milestone part; do not leave commit, push, test, validation, issue updates, or release handoff for the user.

## Product Goal

Build an Avalonia UI control-plane stack that helps agents inspect, preview, safely control, validate, and explain local Avalonia applications through structured tool calls.

Primary users and surfaces:

- MCP clients such as Codex, Claude, Cursor, Rider, VS Code, and Visual Studio.
- A CLI for local agent and developer workflows.
- Future editor integrations.
- Future visual regression or CI workflows.

## Name and Positioning

- Product/repo name: `AvaScope`.
- CLI command target: `avascope`.
- MCP server name target: `avascope`.
- Suggested tagline: `Agent control plane for Avalonia apps.`
- Keep naming AvaScope-specific. Do not introduce alternate product names.

## Core Architecture

Do not make the MCP server the core engine. Keep MCP as a thin adapter over reusable libraries.

Preferred architecture:

```text
Agent / IDE / CLI
  -> AvaScope.Mcp or AvaScope.Cli
    -> AvaScope.Protocol
      -> AvaScope runtime/preview engine
        -> Running app bridge
        -> Preview host process
        -> Headless Skia renderer
        -> MSBuild/project loader
```

Suggested projects:

- `AvaScope.Protocol`: shared request/response DTOs and transport-neutral contracts.
- `AvaScope.Core`: shared inspection/control model, session management, node identity, serialization, and evidence plumbing.
- `AvaScope.Bridge`: opt-in package loaded by Avalonia applications for runtime inspection and local control.
- `AvaScope.PreviewHost`: isolated process that loads projects/views and renders previews.
- `AvaScope.Headless`: headless Avalonia rendering and screenshot helpers.
- `AvaScope.Mcp`: stdio MCP server exposing the reusable engine to AI clients.
- `AvaScope.Cli`: local command line interface.
- `AvaScope.Tests`: unit and integration tests.

## Main Capabilities

Near-term target:

- Attach to a running Avalonia app that includes the AvaScope bridge.
- List inspectable windows and top-levels.
- Capture screenshots.
- Read visual tree and logical tree.
- Inspect node properties, classes, bounds, resources, and binding diagnostics where possible.
- Find nodes by type, name, automation id, text, or path.
- Send basic input: click, pointer move, key text.
- Apply reversible runtime UI mutations for selected safe style/layout/text properties, with mutation history, reset semantics, and before/after evidence artifacts.

Design-time target:

- Preview a `.axaml` file from a `.csproj`.
- Build or design-time-build the project.
- Load app resources, themes, styles, custom controls, and code-behind through the real Avalonia runtime.
- Render through headless Skia.
- Support variants: size, theme, DPI, culture, and optional design data.

Long-term target:

- Hot reload or reload a changed `.axaml` into a preview session.
- Show binding errors, layout warnings, missing resource diagnostics, and style resolution details.
- Derive source-aware suggestions from runtime mutations, diagnostics, preview metadata, and visual evidence without automatically editing source files unless a later guarded workflow explicitly allows it.
- Optional no-code attach mode may be explored later, but it is not the default foundation.

## Important Technical Principles

- Always prefer the real Avalonia runtime over custom XAML interpretation.
- Use public Avalonia APIs first.
- Keep process injection, CLR profiling, or private runtime hooks out of the MVP.
- Keep all UI access on `Dispatcher.UIThread`.
- Isolate preview sessions in child processes so failed user code cannot kill the MCP server.
- Keep protocols stable and versioned.
- Keep the bridge opt-in, local-only, and disabled by default in production builds.
- Do not couple MCP schemas directly to Avalonia internals.
- Do not add Robot Framework/PlatynUI interoperability, foreign workflow import or a Python/Robot compatibility adapter distribution. Useful individual features may inspire AvaScope capabilities through its own Core/CLI/MCP contracts. Issue #154 was explicitly cancelled for this reason.
- Do not make assumptions from one sample app that break normal Avalonia project usage.

## Security and Safety

AvaScope can execute or load user application code. Treat that as a security boundary.

- Default transports should bind to stdio, localhost, or named pipes only.
- Do not expose unauthenticated remote inspection.
- Never enable production remote control by default.
- Prefer explicit project/session selection.
- Make bridge activation obvious and opt-in.
- Keep destructive actions out of the first tool set.

## MCP Tool Shape

Agent-facing MCP tools should be small and composable:

- `list_sessions`
- `attach_to_app`
- `preview_axaml`
- `close_session`
- `screenshot`
- `visual_tree`
- `logical_tree`
- `inspect_node`
- `find_nodes`
- `input`
- `reload`
- `diagnostics`

Tool results should favor structured JSON plus file paths for generated screenshots, reports, and review artifacts. Avoid returning huge unbounded trees by default; support depth limits and node filters.

Runtime mutation tool names and schemas are finalized through the `v0.7.0` issues, but the shape must stay aligned with the agent control-plane model: explicit targets, bounded operations, mutation ids, validation diagnostics, reset semantics, and before/after evidence.

## CLI Shape

Target examples:

```bash
avascope mcp
avascope preview path/to/App.csproj --view Views/MainWindow.axaml --width 1440 --height 900
avascope inspect --process <pid>
avascope screenshot --session <id> --out screenshot.png
```

## Initial Milestones

1. Scaffold a .NET solution with the project layout above.
2. Implement shared protocol models and session IDs.
3. Implement a minimal MCP server with `list_sessions` and health/version info.
4. Implement a minimal Avalonia bridge package that can expose open top-levels.
5. Add screenshot capture for a running app.
6. Add visual tree serialization with stable node IDs.
7. Add preview host for a simple `.axaml` view in an isolated process.
8. Add integration tests with a tiny sample Avalonia app.

## Development Rules

- Prefer small, working vertical slices over broad skeletons.
- Keep the core reusable outside MCP.
- Keep naming explicit and boring.
- Add tests for protocol contracts and process/session behavior.
- When behavior depends on current Avalonia APIs or MCP SDK APIs, verify against official sources before implementing.
- Record non-obvious design decisions in docs, not only in chat.

## Context From Project Creation

AvaScope is a standalone project for Avalonia inspection, preview, and automation workflows. The chosen architecture is an MSBuild-integrated opt-in bridge plus a preview host, with MCP as one adapter over the engine.
