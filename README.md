# AvaScope

Agent control plane for Avalonia apps.

AvaScope is a local-first agent tool for understanding, validating, and controlling Avalonia UI through structured CLI and MCP workflows. It helps an agent inspect running UI trees, render `.axaml` previews in an isolated process, capture screenshots, send narrow non-destructive input, collect diagnostics, and produce evidence artifacts without relying on unstructured screen reading.

AvaScope targets Avalonia 12 and `net10.0`.

## What It Provides

- Agent-oriented control loops: inspect UI state, preview variants, act through local runtime commands, capture evidence, and decide the next step from structured results.
- Runtime inspection and narrow runtime control for bridge-enabled Avalonia apps.
- Headless preview rendering for project-backed `.axaml` views.
- Screenshot capture, focused region assertions, image diffs, and baseline checks for local evidence and CI handoff.
- Structured diagnostics for preview readiness, build failures, bindings, resources, layout, local bridge sessions, and agent triage.
- A command-line tool, an MCP stdio server, and reusable protocol/core libraries.

## Tools

- `avascope` CLI: local commands for previewing, diagnostics, runtime attach, tree inspection, screenshots, input, diffs, baselines, and agent evidence workflows.
- `AvaScope.Mcp`: a stdio MCP server for agent clients such as Codex, Claude, Cursor, Rider, VS Code, and Visual Studio.
- `AvaScope.Bridge`: an opt-in package that a local Avalonia app can load to expose inspectable top-levels and local runtime control.
- `AvaScope.PreviewHost`: an isolated child process that builds/loads project views and renders previews without loading user code into the CLI or MCP server.
- `AvaScope.Protocol` and `AvaScope.Core`: shared contracts and reusable runtime/preview plumbing.

## Quick Start From Source

```powershell
dotnet restore AvaScope.slnx
dotnet build AvaScope.slnx
dotnet test AvaScope.slnx
```

Run a local health check:

```powershell
dotnet .\src\AvaScope.Cli\bin\Debug\net10.0\avascope.dll doctor
```

For the first sample preview and runtime bridge workflow, see the [getting started sample guide](docs/USER_GUIDE.md#getting-started-sample).

Executable ZIPs and package artifacts are published from GitHub Releases when a release is cut.

## Documentation

- [User guide](docs/USER_GUIDE.md): detailed CLI, MCP, runtime bridge, preview, screenshot, diff, baseline, packaging, and release commands.
- [Agent workflow](docs/AGENT_WORKFLOW.md): packaged CLI runbooks for agent-driven local workflows.
- [Validation](docs/VALIDATION.md): local validation commands and release checks.
- [Visual regression CI](docs/VISUAL_REGRESSION_CI.md): baseline-check artifact collection for GitHub Actions.
- [Release plan](docs/RELEASE_PLAN.md): release goals, milestones, non-goals, and roadmap.
- [Project workflow](docs/GITHUB_PROJECT_WORKFLOW.md): GitHub Issues, Milestones, labels, and Project board conventions.

## Safety Model

- Runtime inspection is opt-in; host apps must explicitly activate `AvaScope.Bridge`.
- Bridge discovery and control are local-only through session manifests and local named pipes.
- Preview rendering runs user project code only inside `AvaScope.PreviewHost`, not inside MCP or the CLI process.
- MCP is a thin adapter over reusable local libraries and uses structured results instead of unbounded UI payloads.
- Runtime control is intentionally narrow and non-destructive in the current pre-1.0 line. Bridge-enabled apps support bounded reversible temporary UI mutations for selected style, layout, text, class, and resource experiments, plus before/after evidence capture, session-local mutation review, and reset/close cleanup for agent review loops.

## Project Status

AvaScope is pre-1.0 and actively evolving. The current focus is the `v0.8.0` agent validation release: baseline suites, comparison rules, reviewable evidence packs, and CI-ready artifact handoff on top of the runtime control surfaces shipped in `v0.7.0`. Public APIs and artifact shapes may still change before `v1.0.0`.

Development is tracked in GitHub Issues, Milestones, and the public [AvaScope Roadmap](https://github.com/users/RolandUI/projects/4) Project board.

## Repository Layout

- `src/AvaScope.Protocol`: transport-neutral DTOs and JSON contracts.
- `src/AvaScope.Core`: local bridge and preview host clients.
- `src/AvaScope.Bridge`: opt-in runtime inspection package.
- `src/AvaScope.PreviewHost`: isolated preview renderer.
- `src/AvaScope.Mcp`: MCP stdio adapter.
- `src/AvaScope.Cli`: local command-line interface.
- `samples/AvaScope.GettingStartedApp`: small Avalonia sample app.
- `tests/AvaScope.Tests`: protocol, core, MCP, bridge, preview host, and CLI tests.
