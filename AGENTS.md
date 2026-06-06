# AvaScope AGENTS.md

AvaScope is intended to become an open, generic Avalonia inspection and preview toolkit for AI agents and developers. The long-term target is a DevTools-like experience for Avalonia apps without requiring Avalonia Pro/Plus licensing.

This repository starts from an empty project. Preserve this file as the project context for future agents.

Target Avalonia line: `Avalonia 12`. Do not default implementation work to Avalonia 11.x guidance. Avalonia-facing projects should target `net10.0` by default and use the latest stable Avalonia 12.x patch unless a narrower compatibility target is explicitly required. When package versions, APIs, or breaking changes matter, verify against current Avalonia 12 sources or official documentation before implementing.

## Agent Ownership

Development in this repository is expected to be performed 100% by autonomous coding agents.

- Agents own implementation, test writing, build validation, test validation, documentation updates, commits, and handoff notes end-to-end.
- Do not leave routine coding, validation, formatting, or commit work for the user.
- If a task requires a decision that cannot be derived from this file or the codebase, make the smallest reasonable product-aligned choice and record non-obvious decisions in docs.
- If external credentials, account access, publishing permissions, or product decisions block completion, state the blocker precisely and stop at the nearest validated state.
- Each meaningful change should include relevant tests or an explicit validation note explaining why tests are not applicable.

## Development Plan Workflow

`docs/DEVELOPMENT_PLAN.md` is the primary project-management and progress-tracking source for this repository.

- Every agent must inspect `docs/DEVELOPMENT_PLAN.md` before starting meaningful implementation work.
- Development must follow the active `Current Focus`, `Next Action`, milestone status, acceptance criteria, and validation commands unless the requested task explicitly changes the plan.
- Keep exactly one milestone or workstream marked as `In Progress`.
- After each meaningful change, update the development plan with the new status, next action, validation result, and commit hash when practical.
- If the development plan is stale or conflicts with the repository state, update the plan first, record the reason in its `Decision Log` or `Change Log`, then continue implementation.
- Do not mark a milestone `Done` until its acceptance criteria and validation commands have passed.
- Commit each completed vertical slice or coherent milestone part; do not leave commit, test, or validation work for the user.

## Product Goal

Build a generic Avalonia UI inspection, preview, and automation stack that can be used by:

- MCP clients such as Codex, Claude, Cursor, Rider, VS Code, and Visual Studio.
- A CLI for local developer workflows.
- Future editor integrations.
- Future visual regression or CI workflows.

The project must not be TradeR-specific. TradeR is only the motivating use case.

## Name and Positioning

- Product/repo name: `AvaScope`.
- CLI command target: `avascope`.
- MCP server name target: `avascope`.
- Suggested tagline: `Open inspection and preview tools for Avalonia.`
- Avoid names that imply official Avalonia ownership, such as `Avalonia DevTools`, `Avalonia MCP DevTools`, or `AvaloniaUI.Diagnostics`.

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
- `AvaScope.Core`: shared inspection model, session management, node identity, serialization.
- `AvaScope.Bridge`: opt-in package loaded by Avalonia applications for runtime inspection.
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

Design-time target:

- Preview a `.axaml` file from a `.csproj`.
- Build or design-time-build the project.
- Load app resources, themes, styles, custom controls, and code-behind through the real Avalonia runtime.
- Render through headless Skia.
- Support variants: size, theme, DPI, culture, and optional design data.

Long-term target:

- Hot reload or reload a changed `.axaml` into a preview session.
- Show binding errors, layout warnings, missing resource diagnostics, and style resolution details.
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
- Do not make assumptions from one sample app that break generic Avalonia projects.

## Security and Safety

AvaScope can execute or load user application code. Treat that as a security boundary.

- Default transports should bind to stdio, localhost, or named pipes only.
- Do not expose unauthenticated remote inspection.
- Never enable production remote control by default.
- Prefer explicit project/session selection.
- Make bridge activation obvious and opt-in.
- Keep destructive actions out of the first tool set.

## MCP Tool Shape

Initial MCP tools should be small and composable:

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

Tool results should favor structured JSON plus file paths for generated screenshots. Avoid returning huge unbounded trees by default; support depth limits and node filters.

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

The user wants a generic tool for every Avalonia app, not only TradeR. Development time is not the main constraint; quality and a DevTools-like direction matter more. The best approach chosen was an MSBuild-integrated opt-in bridge plus a preview host, with MCP as one adapter over the engine.
