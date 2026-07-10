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
- Explicit capability discovery so agents can gate newer CLI/MCP workflows by feature id instead of package-version guessing.
- A command-line tool, a Windows per-user install workflow, an MCP stdio server, and reusable protocol/core libraries.

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
- [Stable surface](docs/STABLE_SURFACE.md): v1 package, protocol, CLI, MCP, exit-code, artifact, and release compatibility rules.
- [Upgrade and compatibility](docs/UPGRADE.md): version alignment, bridge-package upgrades, CLI/MCP replacement, and capability-gated client behavior.
- [End-to-end validation](docs/END_TO_END_VALIDATION.md): v1 source, packaged CLI, packaged MCP, runtime bridge, report, and release-readiness validation record.
- [Release artifact verification](docs/RELEASE_ARTIFACT_VERIFICATION.md): v1 package, ZIP, manifest, hash, publish dry-run, packaged CLI, and packaged MCP verification ledger.
- [Post-1.0 backlog](docs/POST_1_0_BACKLOG.md): explicit non-blocking deferrals and release-blocking audit.
- [Security threat model](docs/SECURITY_THREAT_MODEL.md): local-only transport, bridge activation, mutation, preview, artifact, and compatibility boundaries.
- [Performance and stress audit](docs/PERFORMANCE_STRESS_AUDIT.md): bounded output budgets and stress validation coverage for agent workflows.
- [Troubleshooting](docs/TROUBLESHOOTING.md): attach, preview, mutation, report, and package failure triage.
- [Validation](docs/VALIDATION.md): local validation commands and release checks.
- [Visual regression CI](docs/VISUAL_REGRESSION_CI.md): baseline-check artifact collection for GitHub Actions.
- [Release plan](docs/RELEASE_PLAN.md): release goals, milestones, non-goals, and roadmap.
- [Project workflow](docs/GITHUB_PROJECT_WORKFLOW.md): GitHub Issues, Milestones, labels, and Project board conventions.

## Safety Model

- Runtime inspection is opt-in; host apps must explicitly activate `AvaScope.Bridge`.
- Bridge discovery and control are local-only through session manifests and local named pipes.
- Preview rendering runs user project code only inside `AvaScope.PreviewHost`, not inside MCP or the CLI process.
- MCP is a thin adapter over reusable local libraries and uses structured results instead of unbounded UI payloads.
- Runtime control is intentionally narrow, local-only, and non-destructive in the stable v1 surface. Bridge-enabled apps support bounded reversible temporary UI mutations for selected style, layout, text, class, and resource experiments, plus before/after evidence capture, session-local mutation review, and reset/close cleanup for agent review loops.

## License

AvaScope-authored source code and official AvaScope release artifacts published by RolandUI, including previously published official releases, are licensed under the [Apache License 2.0](LICENSE). See [LICENSE-SCOPE.md](LICENSE-SCOPE.md) for the exact scope of the grant and [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md) for separately licensed dependencies.

## Project Status

AvaScope `v1.0.0` is the stable agent control-plane release. Package identities, protocol DTOs, CLI commands, MCP tools, exit codes, artifact names, and release workflow behavior are documented in [docs/STABLE_SURFACE.md](docs/STABLE_SURFACE.md).

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
