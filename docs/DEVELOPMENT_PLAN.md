# AvaScope Development Plan

This document is the primary project-management source for autonomous agents working on AvaScope. Update it whenever meaningful implementation, validation, or planning changes the project state.

## Project Operating Rules

- Work in small vertical slices that produce buildable, testable behavior.
- Keep exactly one `Current Focus` item active at a time.
- Before implementing, compare the requested work with this plan. If the plan is stale, update the plan first.
- Each meaningful implementation change must include relevant tests or an explicit validation note explaining why tests are not applicable.
- Each completed slice must be validated with the listed commands before status moves to `Done`.
- Commit each completed vertical slice or coherent milestone part. Record the commit hash in the handoff and, when practical, in this document.
- Keep MCP, CLI, core runtime, bridge, preview host, and protocol concerns separated.
- Do not introduce broad skeletons unless they directly support the active vertical slice.

## Status Legend

- `Not Started`: No implementation work has begun.
- `In Progress`: The active agent is implementing or validating this item.
- `Blocked`: Progress requires external input, credentials, package availability, or a product decision.
- `Review`: Implementation is complete, but validation or handoff is still pending.
- `Done`: Acceptance criteria and validation are complete.

## Current Focus

- `M21 CI Validation Slice`
- Status: `In Progress`
- Owner: autonomous agent
- Started: `2026-06-07`
- Goal: add repository CI validation for build, tests, and local package creation without publishing.

## Next Action

Add a GitHub Actions workflow that installs the required .NET SDK, restores, builds, tests, and locally packs library artifacts without pushing or publishing anything.

## Latest Validation

- `2026-06-06`: `dotnet restore AvaScope.slnx` passed.
- `2026-06-06`: `dotnet build AvaScope.slnx` passed with 0 warnings and 0 errors.
- `2026-06-06`: `dotnet test AvaScope.slnx` passed with 29 tests.
- `2026-06-06`: `dotnet test AvaScope.slnx --filter Protocol` passed with 10 tests.
- `2026-06-06`: `dotnet test AvaScope.slnx --filter Core` passed with 9 tests.
- `2026-06-06`: `dotnet test AvaScope.slnx --filter Mcp` passed with 4 tests.
- `2026-06-06`: `dotnet test AvaScope.slnx --filter Bridge` passed with 8 tests.
- `2026-06-06`: `dotnet build AvaScope.slnx` passed with 0 warnings and 0 errors after M5 bridge IPC foundation.
- `2026-06-06`: `dotnet test AvaScope.slnx --no-build --filter Protocol` passed with 13 tests.
- `2026-06-06`: `dotnet test AvaScope.slnx --no-build --filter Bridge` passed with 13 tests.
- `2026-06-06`: `dotnet test AvaScope.slnx --no-build` passed with 34 tests.
- `2026-06-06`: `dotnet build AvaScope.slnx` passed with 0 warnings and 0 errors after M5 attach client/MCP tool slice.
- `2026-06-06`: `dotnet test AvaScope.slnx --no-build --filter Protocol` passed with 15 tests.
- `2026-06-06`: `dotnet test AvaScope.slnx --no-build --filter Core` passed with 13 tests.
- `2026-06-06`: `dotnet test AvaScope.slnx --no-build --filter Mcp` passed with 8 tests.
- `2026-06-06`: `dotnet test AvaScope.slnx --no-build --filter Bridge` passed with 18 tests.
- `2026-06-06`: `dotnet test AvaScope.slnx --no-build` passed with 43 tests.
- `2026-06-06`: `dotnet build AvaScope.slnx` passed with 0 warnings and 0 errors after M5 completion.
- `2026-06-06`: `dotnet test AvaScope.slnx --no-build --filter Bridge` passed with 19 tests after MCP/Core/pipe screenshot validation.
- `2026-06-06`: `dotnet test AvaScope.slnx --no-build --filter Mcp` passed with 9 tests after MCP/Core/pipe screenshot validation.
- `2026-06-06`: `dotnet test AvaScope.slnx --no-build` passed with 44 tests.
- `2026-06-06`: `dotnet build AvaScope.slnx` passed with 0 warnings and 0 errors after M6 tree serialization slice.
- `2026-06-06`: `dotnet test AvaScope.slnx --no-build --filter Protocol` passed with 16 tests.
- `2026-06-06`: `dotnet test AvaScope.slnx --no-build --filter Core` passed with 14 tests.
- `2026-06-06`: `dotnet test AvaScope.slnx --no-build --filter Mcp` passed with 10 tests.
- `2026-06-06`: `dotnet test AvaScope.slnx --no-build --filter Bridge` passed with 20 tests.
- `2026-06-06`: `dotnet test AvaScope.slnx --no-build` passed with 47 tests.
- `2026-06-06`: `dotnet build AvaScope.slnx` passed with 0 warnings and 0 errors after M6 `find_nodes` slice.
- `2026-06-06`: `dotnet test AvaScope.slnx --no-build --filter Protocol` passed with 17 tests.
- `2026-06-06`: `dotnet test AvaScope.slnx --no-build --filter Core` passed with 15 tests.
- `2026-06-06`: `dotnet test AvaScope.slnx --no-build --filter Mcp` passed with 11 tests.
- `2026-06-06`: `dotnet test AvaScope.slnx --no-build --filter Bridge` passed with 21 tests.
- `2026-06-06`: `dotnet test AvaScope.slnx --no-build` passed with 50 tests.
- `2026-06-06`: `dotnet build AvaScope.slnx` passed with 0 warnings and 0 errors after M7 input MVP slice.
- `2026-06-06`: `dotnet test AvaScope.slnx --no-build --filter Protocol` passed with 18 tests.
- `2026-06-06`: `dotnet test AvaScope.slnx --no-build --filter Core` passed with 16 tests.
- `2026-06-06`: `dotnet test AvaScope.slnx --no-build --filter Mcp` passed with 13 tests.
- `2026-06-06`: `dotnet test AvaScope.slnx --no-build --filter Bridge` passed with 23 tests.
- `2026-06-06`: `dotnet test AvaScope.slnx --no-build` passed with 54 tests.
- `2026-06-06`: `AvaScope.Protocol` package list checked; no package references found.
- `2026-06-06`: `AvaScope.Core` package list checked; no package references found.
- `2026-06-06`: `rg "Avalonia|ModelContextProtocol|Mcp|MCP" src\AvaScope.Protocol tests\AvaScope.Tests\Protocol` found no matches.
- `2026-06-06`: `rg "Avalonia|ModelContextProtocol|Mcp|MCP" src\AvaScope.Core tests\AvaScope.Tests\Core` found no matches.
- `2026-06-06`: Markdown tracking fields checked for `Current Focus`, `Next Action`, `Status`, `Acceptance Criteria`, and `Validation`.
- `2026-06-07`: `dotnet build AvaScope.slnx` passed with 0 warnings and 0 errors after M7 routed pointer move completion.
- `2026-06-07`: `dotnet test AvaScope.slnx --no-build --filter Bridge` passed with 23 tests.
- `2026-06-07`: `dotnet test AvaScope.slnx --no-build` passed with 54 tests.
- `2026-06-07`: `dotnet build AvaScope.slnx` passed with 0 warnings and 0 errors after the first M8 preview host slice.
- `2026-06-07`: `dotnet test AvaScope.slnx --no-build --filter Protocol` passed with 20 tests.
- `2026-06-07`: `dotnet test AvaScope.slnx --no-build --filter FullyQualifiedName~PreviewHost` passed with 1 test.
- `2026-06-07`: `dotnet test AvaScope.slnx --no-build` passed with 57 tests.
- `2026-06-07`: `dotnet build AvaScope.slnx` passed with 0 warnings and 0 errors after M8 project-aware path resolution.
- `2026-06-07`: `dotnet test AvaScope.slnx --no-build --filter FullyQualifiedName~PreviewHost` passed with 2 tests.
- `2026-06-07`: `dotnet test AvaScope.slnx --no-build` passed with 58 tests.
- `2026-06-07`: `dotnet build AvaScope.slnx` passed with 0 warnings and 0 errors after M8 project build boundary.
- `2026-06-07`: `dotnet test AvaScope.slnx --no-build --filter FullyQualifiedName~PreviewHost` passed with 3 tests.
- `2026-06-07`: `dotnet test AvaScope.slnx --no-build` passed with 59 tests.
- `2026-06-07`: `dotnet build AvaScope.slnx` passed with 0 warnings and 0 errors after M8 compiled project resource loading.
- `2026-06-07`: `dotnet test AvaScope.slnx --no-build --filter FullyQualifiedName~PreviewHost` passed with 4 tests.
- `2026-06-07`: `dotnet test AvaScope.slnx --no-build` passed with 60 tests.
- `2026-06-07`: `dotnet build AvaScope.slnx` passed with 0 warnings and 0 errors after M9 preview adapter integration.
- `2026-06-07`: `dotnet test AvaScope.slnx --no-build --filter Core` passed with 18 tests.
- `2026-06-07`: `dotnet test AvaScope.slnx --no-build --filter Mcp` passed with 15 tests.
- `2026-06-07`: `dotnet test AvaScope.slnx --no-build` passed with 64 tests.
- `2026-06-07`: `dotnet build AvaScope.slnx` passed with 0 warnings and 0 errors after M10 CLI integration.
- `2026-06-07`: `dotnet test AvaScope.slnx --no-build --filter FullyQualifiedName~Cli` passed with 13 tests.
- `2026-06-07`: `dotnet test AvaScope.slnx --no-build` passed with 67 tests.
- `2026-06-07`: Markdown tracking/status fields checked after M11 documentation update.
- `2026-06-07`: `dotnet build AvaScope.slnx` passed with 0 warnings and 0 errors after M11 documentation update.
- `2026-06-07`: `dotnet test AvaScope.slnx --no-build` passed with 67 tests.
- `2026-06-07`: Post-MVP gap audit recorded in `docs/GAP_AUDIT.md`; selected runtime `close_session` lifecycle support as the next slice.
- `2026-06-07`: `dotnet build AvaScope.slnx` passed with 0 warnings and 0 errors after M12 runtime `close_session` lifecycle implementation.
- `2026-06-07`: `dotnet test AvaScope.slnx --no-build --filter Protocol` passed with 21 tests.
- `2026-06-07`: `dotnet test AvaScope.slnx --no-build --filter Core` passed with 19 tests.
- `2026-06-07`: `dotnet test AvaScope.slnx --no-build --filter Bridge` passed with 26 tests.
- `2026-06-07`: `dotnet test AvaScope.slnx --no-build --filter Mcp` passed with 18 tests.
- `2026-06-07`: `dotnet test AvaScope.slnx --no-build --filter PreviewHost` passed with 8 tests after temp-directory cleanup retry hardening.
- `2026-06-07`: `dotnet test AvaScope.slnx --no-build` passed with 72 tests.
- `2026-06-07`: `dotnet build AvaScope.slnx` passed with 0 warnings and 0 errors after M13 diagnostics implementation.
- `2026-06-07`: `dotnet test AvaScope.slnx --no-build --filter Protocol` passed with 22 tests.
- `2026-06-07`: `dotnet test AvaScope.slnx --no-build --filter Core` passed with 22 tests.
- `2026-06-07`: `dotnet test AvaScope.slnx --no-build --filter Mcp` passed with 21 tests.
- `2026-06-07`: `dotnet test AvaScope.slnx --no-build` passed with 79 tests.
- `2026-06-07`: `dotnet build AvaScope.slnx` passed with 0 warnings and 0 errors after M14 preview App.axaml resource loading.
- `2026-06-07`: `dotnet test AvaScope.slnx --no-build --filter FullyQualifiedName~PreviewHost` passed with 10 tests.
- `2026-06-07`: `dotnet test AvaScope.slnx --no-build` passed with 81 tests.
- `2026-06-07`: `dotnet build AvaScope.slnx` passed with 0 warnings and 0 errors after M15 preview diagnostics expansion and build-server isolation hardening.
- `2026-06-07`: `dotnet test AvaScope.slnx --no-build --filter Protocol` passed with 22 tests.
- `2026-06-07`: `dotnet test AvaScope.slnx --no-build --filter Core` passed with 25 tests.
- `2026-06-07`: `dotnet test AvaScope.slnx --no-build --filter Mcp` passed with 21 tests.
- `2026-06-07`: `dotnet test AvaScope.slnx --no-build --filter FullyQualifiedName~PreviewHost` passed with 13 tests.
- `2026-06-07`: `dotnet test AvaScope.slnx --no-build` passed with 84 tests.
- `2026-06-07`: `dotnet build AvaScope.slnx` passed with 0 warnings and 0 errors after M16 preview reload foundation.
- `2026-06-07`: `dotnet test AvaScope.slnx --no-build --filter Protocol` passed with 23 tests.
- `2026-06-07`: `dotnet test AvaScope.slnx --no-build --filter Core` passed with 28 tests.
- `2026-06-07`: `dotnet test AvaScope.slnx --no-build --filter Mcp` passed with 24 tests.
- `2026-06-07`: `dotnet test AvaScope.slnx --no-build` passed with 91 tests.
- `2026-06-07`: `dotnet build AvaScope.slnx` passed with 0 warnings and 0 errors after M17 preview reload MVP.
- `2026-06-07`: `dotnet test AvaScope.slnx --no-build --filter Protocol` passed with 23 tests.
- `2026-06-07`: `dotnet test AvaScope.slnx --no-build --filter Core` passed with 32 tests.
- `2026-06-07`: `dotnet test AvaScope.slnx --no-build --filter Mcp` passed with 26 tests.
- `2026-06-07`: `dotnet test AvaScope.slnx --no-build` passed with 97 tests.
- `2026-06-07`: `dotnet build AvaScope.slnx` passed with 0 warnings and 0 errors after M18 pointer press/release input.
- `2026-06-07`: `dotnet test AvaScope.slnx --no-build --filter Protocol` passed with 24 tests.
- `2026-06-07`: `dotnet test AvaScope.slnx --no-build --filter Core` passed with 32 tests.
- `2026-06-07`: `dotnet test AvaScope.slnx --no-build --filter Bridge` passed with 33 tests.
- `2026-06-07`: `dotnet test AvaScope.slnx --no-build --filter Mcp` passed with 26 tests.
- `2026-06-07`: `dotnet test AvaScope.slnx --no-build` first hit a transient PreviewHost temp-directory cleanup lock, then passed on immediate rerun with 98 tests.
- `2026-06-07`: `dotnet build AvaScope.slnx` passed with 0 warnings and 0 errors after M19 runtime reload contract implementation.
- `2026-06-07`: `dotnet test AvaScope.slnx --no-build --filter Protocol` passed with 24 tests.
- `2026-06-07`: `dotnet test AvaScope.slnx --no-build --filter Core` passed with 33 tests.
- `2026-06-07`: `dotnet test AvaScope.slnx --no-build --filter Bridge` passed with 36 tests.
- `2026-06-07`: `dotnet test AvaScope.slnx --no-build --filter Mcp` passed with 28 tests.
- `2026-06-07`: `dotnet test AvaScope.slnx --no-build` passed with 101 tests after PreviewHost cleanup lock hardening.
- `2026-06-07`: `dotnet build AvaScope.slnx -c Release` passed with 0 warnings and 0 errors after M20 package metadata.
- `2026-06-07`: `dotnet pack src\AvaScope.Protocol\AvaScope.Protocol.csproj -c Release --no-build --output artifacts\packages` created `AvaScope.Protocol.0.1.0.nupkg`.
- `2026-06-07`: `dotnet pack src\AvaScope.Core\AvaScope.Core.csproj -c Release --no-build --output artifacts\packages` created `AvaScope.Core.0.1.0.nupkg`.
- `2026-06-07`: `dotnet pack src\AvaScope.Bridge\AvaScope.Bridge.csproj -c Release --no-build --output artifacts\packages` created `AvaScope.Bridge.0.1.0.nupkg`.
- `2026-06-07`: Package metadata inspected from `.nuspec`; Bridge declares dependencies on `AvaScope.Core`, `AvaScope.Protocol`, and `Avalonia` 12.0.4, and includes `README.md`.
- `2026-06-07`: `dotnet pack` for `AvaScope.Mcp`, `AvaScope.Cli`, and `AvaScope.PreviewHost` completed as no-op because those projects are explicitly `IsPackable=false`.
- `2026-06-07`: `git check-ignore -v artifacts\packages\AvaScope.Bridge.0.1.0.nupkg` confirmed package artifacts are ignored by `.gitignore`.

## Milestones

### M0 Project Foundation

- Status: `Done`
- Goal: create a clean .NET solution foundation for Avalonia 12-oriented development.
- Deliverables: solution structure, source/test folders, shared build settings, test project, local validation command list.
- Acceptance Criteria:
  - Solution builds with `net10.0`.
  - Test project is included in the solution.
  - Repository ignores generated build artifacts.
  - Local validation commands are documented and pass.
- Validation:
  - `dotnet build AvaScope.slnx`
  - `dotnet test AvaScope.slnx`
  - `git status --short`

### M1 Protocol Contracts

- Status: `Done`
- Goal: define stable, transport-neutral request/response contracts.
- Deliverables: session identifiers, protocol version model, core tool result shapes, JSON serialization tests.
- Acceptance Criteria:
  - Protocol models do not depend on Avalonia runtime types or MCP SDK types.
  - Models serialize and deserialize with stable property names.
  - Protocol versioning is explicit.
- Validation:
  - `dotnet test AvaScope.slnx --filter Protocol`

### M2 Core Session Model

- Status: `Done`
- Goal: implement reusable session lifecycle behavior outside MCP.
- Deliverables: session registry, session IDs, lifecycle state, error model, unit tests.
- Acceptance Criteria:
  - Sessions can be created, listed, inspected, and closed through core APIs.
  - Invalid session access returns structured errors.
  - Core APIs remain transport-neutral.
- Validation:
  - `dotnet test AvaScope.slnx --filter Core`

### M3 Minimal MCP Adapter

- Status: `Done`
- Goal: expose the first MCP surface as a thin adapter over protocol/core.
- Deliverables: stdio MCP server, health/version tool, `list_sessions` tool.
- Acceptance Criteria:
  - MCP adapter contains no core session state that belongs in `AvaScope.Core`.
  - Tool results map from protocol/core models.
  - Server starts locally over stdio.
- Validation:
  - `dotnet build AvaScope.slnx`
  - MCP smoke test for health/version and `list_sessions`

### M4 Opt-in Bridge MVP

- Status: `Done`
- Goal: provide an opt-in Avalonia 12 bridge package for runtime inspection.
- Deliverables: bridge activation API, local-only transport boundary, top-level/window discovery.
- Acceptance Criteria:
  - Bridge is disabled unless explicitly activated by the host app.
  - Top-level access runs on `Dispatcher.UIThread`.
  - Bridge does not require private Avalonia APIs.
- Validation:
  - `dotnet test AvaScope.slnx`
  - sample app bridge smoke test

### M5 Runtime Screenshot Slice

- Status: `Done`
- Goal: capture screenshots from a running bridged Avalonia app.
- Deliverables: attach flow, screenshot request/response, generated image file output, sample validation.
- Progress:
  - Done: in-process bridge screenshot capture for registered top-levels with PNG file output and structured success/error results.
  - Done: bridge activation now writes a local session manifest and starts a local-only named-pipe IPC server.
  - Done: bridge IPC protocol models cover request, response, method names, and session manifest JSON shape.
  - Done: bridge named-pipe health request is covered by a smoke test.
  - Done: reusable `LocalBridgeClient` discovers live bridge session manifests and calls the bridge pipe without Avalonia or MCP dependencies.
  - Done: MCP adapter exposes `attach_to_app`, `list_top_levels`, and `screenshot` as thin tool methods over `LocalBridgeClient`.
  - Done: positive attach validation covers manifest discovery plus pipe `health`; negative tool validation covers invalid and missing-session paths.
  - Done: positive `list_top_levels` and `screenshot` validation covers MCP tool -> Core client -> named pipe -> Bridge -> Avalonia UI thread -> PNG output.
- Acceptance Criteria:
  - Screenshot output path is returned as structured data.
  - Failed capture returns a structured diagnostic error.
  - Capture behavior is covered by an integration test or documented manual validation.
- Validation:
  - screenshot smoke test against sample app
  - output file existence and non-empty image validation
  - local IPC health smoke test
  - MCP attach smoke test through local bridge manifest and pipe health
  - MCP/Core/pipe screenshot smoke test against a headless Avalonia window

### M6 Tree Inspection Slice

- Status: `Done`
- Goal: expose visual and logical tree inspection with stable node identity.
- Deliverables: tree serialization, depth limits, node metadata, basic find behavior.
- Progress:
  - Done: protocol DTOs for tree kind, node bounds, node summaries, and tree responses.
  - Done: bridge visual/logical tree serialization using public Avalonia traversal APIs.
  - Done: stable node ids within a session based on runtime object identity.
  - Done: depth limits with default bounded output and explicit max depth input.
  - Done: MCP/Core/named-pipe `visual_tree` and `logical_tree` tool path with headless validation.
  - Done: `find_nodes` filters by type, name, automation id, and text, returning matched nodes with root-to-node path ids.
- Acceptance Criteria:
  - Tree results are bounded by default.
  - Node IDs are stable within a session.
  - Find supports at least type, name, automation id, and text where available.
- Validation:
  - tree serialization unit tests
  - sample app visual/logical tree integration test

### M7 Input Slice

- Status: `Done`
- Goal: send basic local-only input to a running bridged Avalonia app.
- Deliverables: click, pointer move, key text commands, safety checks.
- Progress:
  - Done: protocol `InputResponse`, `InputActions`, and IPC request fields for local input.
  - Done: MCP/Core/named-pipe `input` tool path.
  - Done: Button click MVP via hit-test and routed `Button.ClickEvent`.
  - Done: key text MVP for a focused `TextBox`.
  - Done: pointer move raises a public Avalonia 12 routed `PointerMovedEvent` on the hit-tested input target.
  - Done: unsupported input actions return structured diagnostics through the MCP/Core/named-pipe path.
- Acceptance Criteria:
  - Input targets must resolve to an active local session.
  - Unsupported input returns structured diagnostics.
  - Commands execute on the correct UI/input path for Avalonia 12.
- Validation:
  - input smoke test against sample app
  - negative tests for invalid session and unsupported input

### M8 Preview Host Slice

- Status: `Done`
- Goal: render a `.axaml` view from a project in an isolated preview process.
- Deliverables: preview host process, project/view selection, headless Skia rendering, basic variants.
- Progress:
  - Done: `AvaScope.PreviewHost` console process entrypoint accepts a JSON `PreviewRequest` file and writes a structured `ToolResult<PreviewResponse>` to stdout.
  - Done: `PreviewRequest` and `PreviewResponse` protocol DTOs cover output path, width, height, DPI, theme variant, project path, and view path.
  - Done: headless Skia render smoke path loads a standalone `.axaml` control with the official Avalonia runtime XAML loader and writes a PNG file.
  - Done: process-level smoke test validates child process isolation, structured JSON output, PNG existence, dimensions, and non-empty output.
  - Done: project-aware `.csproj` validation resolves relative view paths from the project directory and returns absolute project/view paths in the response.
  - Done: project build boundary runs `dotnet build` inside the preview child process before rendering and returns structured `preview_project_build_failed` diagnostics when the build fails.
  - Done: built project assembly loading resolves compiled Avalonia resource XAML through `avares://` and validates a code-behind-backed `UserControl` smoke render.
- Acceptance Criteria:
  - User application code runs outside the MCP server process.
  - Preview supports width, height, DPI, and theme inputs.
  - Render output is returned as a file path with structured diagnostics.
- Validation:
  - preview smoke test against sample Avalonia 12 project
  - output file existence and non-empty image validation

### M9 Preview Adapter Integration

- Status: `Done`
- Goal: expose preview rendering through adapter surfaces without moving preview execution into MCP.
- Deliverables: reusable Core preview host client, MCP `preview_axaml` tool, structured process diagnostics.
- Progress:
  - Done: Core `PreviewHostClient` writes `PreviewRequest` JSON, launches the preview host child process, parses `ToolResult<PreviewResponse>`, and maps structured errors.
  - Done: MCP `preview_axaml` tool is a thin adapter over `PreviewHostClient`.
  - Done: MCP stdio tool list includes `preview_axaml`.
- Acceptance Criteria:
  - Core can launch `AvaScope.PreviewHost` as a child process and parse structured preview results.
  - MCP exposes preview rendering as a thin adapter over the Core preview client.
  - Preview failures return structured diagnostics without leaking user code into the MCP server process.
- Validation:
  - Core preview client process smoke test
  - MCP `preview_axaml` smoke test with PNG output validation

### M10 CLI Integration

- Status: `Done`
- Goal: provide a local `avascope` command for developer workflows.
- Deliverables: CLI project, preview command, MCP server command handoff or documented invocation path.
- Progress:
  - Done: `AvaScope.Cli` builds as `avascope`.
  - Done: `avascope preview <project.csproj> --view <view.axaml> --out <preview.png> --width <w> --height <h> [--dpi <dpi>] [--theme light|dark]` renders through `PreviewHostClient`.
  - Done: `avascope mcp` starts the MCP server assembly colocated with the CLI output.
  - Done: CLI invalid arguments return non-zero exit codes and structured JSON errors.
- Acceptance Criteria:
  - CLI can render a preview through `PreviewHostClient` without loading user project code in the CLI process.
  - CLI returns non-zero exit codes and concise structured errors for invalid requests.
  - CLI project builds with the solution and has focused process-level tests.
- Validation:
  - CLI preview smoke test with PNG output validation
  - CLI invalid argument/error test

### M11 Documentation and Release Readiness

- Status: `Done`
- Goal: document the current usable workflows and harden local validation for handoff.
- Deliverables: README usage guide, architecture/safety summary, validation command checklist, current limitations.
- Progress:
  - Done: README documents project layout, build/test commands, CLI usage, MCP tools, bridge activation, preview behavior, safety boundaries, and current limitations.
  - Done: validation guide includes PreviewHost and CLI targeted test commands.
- Acceptance Criteria:
  - A new agent or developer can run MCP, CLI preview, and bridge workflows from documented commands.
  - Documentation states the current isolation and local-only safety boundaries.
  - Documentation does not overclaim unsupported preview/resource/input behavior.
- Validation:
  - Markdown tracking/status check
  - `dotnet build AvaScope.slnx`
  - `dotnet test AvaScope.slnx --no-build`

### M12 Post-MVP Hardening

- Status: `Done`
- Goal: audit and close the highest-risk gaps in the first usable AvaScope workflow set.
- Deliverables: prioritized gap list, next vertical hardening slice, validation updates.
- Progress:
  - Done: gap audit created with P0/P1/P2 ranking.
  - Done: selected runtime `close_session` lifecycle support as the next vertical slice.
  - Done: safe bridge IPC close handshake returns a structured response before stopping the local bridge server.
  - Done: Core `LocalBridgeClient` and MCP expose `close_session`.
  - Done: stale manifest cleanup is validated through bridge and MCP/Core/pipe tests.
  - Done: PreviewHost temp-directory cleanup is retried to avoid Windows handle-release flakiness in full-suite validation.
- Acceptance Criteria:
  - Gaps are ranked by user impact and architectural risk.
  - The next slice is small enough to validate and commit independently.
  - Any new behavior remains covered by focused tests or explicit validation notes.
- Validation:
  - audit notes in this plan or a dedicated docs file
  - relevant targeted test command for the selected slice

### M13 Diagnostics Surface

- Status: `Done`
- Goal: provide the first aggregate diagnostics surface without coupling MCP schemas to bridge or preview internals.
- Deliverables: diagnostics protocol DTOs, local bridge diagnostics path, MCP `diagnostics` tool, focused tests.
- Progress:
  - Done: transport-neutral diagnostics response DTOs cover service health, manifest directory, bounded bridge session diagnostics, and structured issues.
  - Done: local bridge diagnostics reports manifest path, process id, named-pipe transport, pipe name, protocol health, stale manifests, invalid manifests, and unavailable IPC errors.
  - Done: MCP exposes `diagnostics` as a thin adapter over `LocalBridgeClient.DiagnosticsAsync`.
  - Done: missing/stale/unavailable states return structured diagnostic data instead of throwing.
  - Done: tests cover protocol serialization, Core missing/stale/invalid/limit behavior, MCP unavailable-state behavior, MCP bridge health, and stdio tool listing.
- Acceptance Criteria:
  - Diagnostics are bounded, structured, and transport-neutral.
  - Missing or stale sessions return structured diagnostic data rather than throwing.
  - MCP remains a thin adapter over Core/client behavior.
  - The first slice does not claim binding, layout, or resource diagnostics until those signals exist.
- Validation:
  - `dotnet build AvaScope.slnx`
  - `dotnet test AvaScope.slnx --no-build --filter Protocol`
  - `dotnet test AvaScope.slnx --no-build --filter Core`
  - `dotnet test AvaScope.slnx --no-build --filter Mcp`
  - `dotnet test AvaScope.slnx --no-build`

### M14 Preview App Resource Scope

- Status: `Done`
- Goal: improve project preview fidelity by loading compiled app-level resources before view rendering.
- Deliverables: preview host app resource discovery/loading, resource-backed render smoke test, structured diagnostics for missing/failed app resource loading.
- Progress:
  - Done: inspected Avalonia 12 resource/API docs and current compiled preview loading path.
  - Done: PreviewHost detects project-root `App.axaml`, loads it from the compiled project assembly through `avares://`, and copies top-level `Application.Resources` entries into the active PreviewHost application before loading the view.
  - Done: resource-backed smoke test validates a compiled project view resolving a `StaticResource` brush from `App.axaml`.
  - Done: missing `App.axaml` remains non-breaking for existing standalone and resource-free project previews.
  - Done: invalid/non-Application `App.axaml` returns structured `preview_render_failed` output.
- Acceptance Criteria:
  - App-level resources are loaded inside the isolated preview host process, not MCP or CLI.
  - A project preview can render a compiled view that depends on `App.axaml` resources.
  - Missing app resources do not break standalone or resource-free previews.
  - Failures return structured preview diagnostics without overclaiming full design-time parity.
- Validation:
  - `dotnet build AvaScope.slnx`
  - `dotnet test AvaScope.slnx --no-build --filter FullyQualifiedName~PreviewHost`
  - `dotnet test AvaScope.slnx --no-build`

### M15 Preview Diagnostics Expansion

- Status: `Done`
- Goal: report preview-host readiness in the existing diagnostics tool without launching user project code.
- Deliverables: preview host diagnostic DTO, Core diagnostics population, MCP diagnostics coverage, tests.
- Progress:
  - Done: protocol `PreviewHostDiagnostic` and process-mode constants cover preview host status, host assembly path, isolated child-process mode, service metadata, and structured errors.
  - Done: `PreviewHostClient.GetDiagnostics()` reports available and missing host states without launching the host or loading user projects.
  - Done: `DiagnosticsResponse` includes optional preview-host readiness alongside existing bridge diagnostics.
  - Done: MCP `diagnostics` composes `LocalBridgeClient` and `PreviewHostClient` while remaining a thin Core adapter.
  - Done: PreviewHost project builds now pass `--disable-build-servers` to reduce stale temp-project file locks in repeated smoke runs.
  - Done: tests cover protocol serialization, Core available/missing host readiness, aggregated diagnostics response, MCP diagnostics output, and full-suite stability.
- Acceptance Criteria:
  - Diagnostics can tell an agent whether the preview host executable is present before a preview request.
  - Preview diagnostics do not build or load user projects.
  - The existing `diagnostics` MCP tool remains a thin adapter over Core.
  - Missing preview host state is returned as structured diagnostic data.
- Validation:
  - `dotnet build AvaScope.slnx`
  - `dotnet test AvaScope.slnx --no-build --filter Protocol`
  - `dotnet test AvaScope.slnx --no-build --filter Core`
  - `dotnet test AvaScope.slnx --no-build --filter Mcp`
  - `dotnet test AvaScope.slnx --no-build`

### M16 Preview Reload Foundation

- Status: `Done`
- Goal: create the minimal persistent preview-session model required before implementing `reload`.
- Deliverables: preview session DTOs, Core preview session registry flow, MCP list/close integration for preview sessions where applicable, tests.
- Progress:
  - Done: defined preview sessions as metadata records containing the original request and latest render result, not live user-code processes.
  - Done: added protocol `PreviewSessionSummary` and `ListPreviewSessionsResponse` DTOs.
  - Done: added Core `PreviewSessionRegistry` with create/list/close behavior sharing lifecycle state through `SessionRegistry`.
  - Done: `create_preview_session` performs an initial isolated render through `AvaScope.PreviewHost` and stores success or failure as `lastRender`.
  - Done: `list_preview_sessions` and `close_preview_session` expose preview session lifecycle through MCP.
  - Done: `list_sessions` sees preview session lifecycle through the shared `SessionRegistry`.
  - Done: tests cover protocol serialization, Core successful and failed create flows, close errors, MCP create/list/close behavior, and stdio tool discovery.
- Acceptance Criteria:
  - Preview session state remains outside MCP-specific tool schemas.
  - User project code still only runs in `AvaScope.PreviewHost`.
  - The first foundation slice does not claim hot reload until a reload command is implemented and validated.
  - Existing one-shot `preview_axaml` behavior remains compatible.
- Validation:
  - `dotnet build AvaScope.slnx`
  - `dotnet test AvaScope.slnx --no-build --filter Protocol`
  - `dotnet test AvaScope.slnx --no-build --filter Core`
  - `dotnet test AvaScope.slnx --no-build --filter Mcp`
  - `dotnet test AvaScope.slnx --no-build`

### M17 Preview Reload MVP

- Status: `Done`
- Goal: re-render an existing preview session from its stored request.
- Deliverables: Core reload method, MCP `reload` tool for preview sessions, updated preview session metadata, tests.
- Progress:
  - Done: `PreviewSessionRegistry.ReloadAsync` reuses the stored `PreviewRequest`, re-renders through `AvaScope.PreviewHost`, updates `lastRender`, and preserves the existing session id.
  - Done: successful reload restores a failed preview session to active state through `SessionRegistry.MarkActive`.
  - Done: failed reload stores structured render failure metadata and marks the preview session failed.
  - Done: closed preview sessions return structured `session_closed` errors on reload.
  - Done: MCP exposes `reload` for preview session ids.
  - Done: tests cover successful reload, failed reload, closed-session rejection, MCP reload behavior, and stdio tool discovery.
- Acceptance Criteria:
  - Reload does not keep user code loaded in MCP.
  - Reload updates the existing preview session record rather than creating a new session.
  - Reload returns structured success or failure through the same preview session summary shape.
  - Unsupported runtime-session reload remains explicit and does not silently target bridge sessions.
- Validation:
  - `dotnet build AvaScope.slnx`
  - `dotnet test AvaScope.slnx --no-build --filter Protocol`
  - `dotnet test AvaScope.slnx --no-build --filter Core`
  - `dotnet test AvaScope.slnx --no-build --filter Mcp`
  - `dotnet test AvaScope.slnx --no-build`

### M18 Input Press/Release Slice

- Status: `Done`
- Goal: add one focused runtime input primitive pair beyond move/click/text.
- Deliverables: protocol input action constants, bridge routed pointer press/release handling, Core/MCP path reuse, headless tests.
- Progress:
  - Done: verified Avalonia 12.0.4 public routed pointer press/release event construction against the official API/source shape.
  - Done: added stable `pointer_down` and `pointer_up` input action constants.
  - Done: bridge input raises `PointerPressedEventArgs` and `PointerReleasedEventArgs` on hit-tested `InputElement` targets with explicit coordinate validation.
  - Done: pointer release reuses the active pointer created by pointer down when available.
  - Done: headless MCP/Core/named-pipe validation covers pointer press/release and preserves existing move/click/text behavior.
- Acceptance Criteria:
  - Input remains local-only through an active bridge session.
  - Pointer press/release execute on the UI thread and target hit-tested input elements.
  - Unsupported or invalid input returns structured diagnostics.
  - Existing `click`, `pointer_move`, and `key_text` behavior remains compatible.
- Validation:
  - `dotnet build AvaScope.slnx`
  - `dotnet test AvaScope.slnx --no-build --filter Protocol`
  - `dotnet test AvaScope.slnx --no-build --filter Core`
  - `dotnet test AvaScope.slnx --no-build --filter Bridge`
  - `dotnet test AvaScope.slnx --no-build --filter Mcp`
  - `dotnet test AvaScope.slnx --no-build`

### M19 Runtime Reload Contract Slice

- Status: `Done`
- Goal: make runtime reload semantics explicit and safe now that preview reload has a working MVP.
- Deliverables: reload protocol decision, Core/MCP behavior for runtime sessions, tests, README/tracking updates.
- Progress:
  - Done: audited the current MCP `reload` response shape and kept preview `PreviewSessionSummary` success responses compatible.
  - Done: runtime bridge session ids now fall through to Core `LocalBridgeClient.ReloadRuntimeAsync` only when no preview session exists.
  - Done: active runtime bridge sessions are health-checked locally and return structured `runtime_reload_not_supported` diagnostics.
  - Done: unknown session ids preserve the existing preview `session_not_found` behavior when no runtime bridge matches.
  - Done: PreviewHost smoke-test cleanup no longer fails successful tests on transient Windows temp-directory locks.
- Acceptance Criteria:
  - Runtime reload must not inject code, restart the user app, or claim hot reload.
  - Preview reload behavior remains compatible and covered.
  - Runtime reload checks an active local bridge session and returns structured success or structured unsupported/unavailable diagnostics.
  - MCP remains a thin adapter over Core/protocol behavior.
- Validation:
  - `dotnet build AvaScope.slnx`
  - `dotnet test AvaScope.slnx --no-build --filter Protocol`
  - `dotnet test AvaScope.slnx --no-build --filter Core`
  - `dotnet test AvaScope.slnx --no-build --filter Bridge`
  - `dotnet test AvaScope.slnx --no-build --filter Mcp`
  - `dotnet test AvaScope.slnx --no-build`

### M20 Packaging Metadata Slice

- Status: `Done`
- Goal: prepare local package artifacts without introducing publishing or CI yet.
- Deliverables: package metadata for packable projects, local output folder convention, pack validation commands, docs/tracking update.
- Progress:
  - Done: selected `AvaScope.Protocol`, `AvaScope.Core`, and `AvaScope.Bridge` as the first packable library projects.
  - Done: added shared version/authors/product/repository/readme metadata.
  - Done: added package ids, descriptions, and tags for the three library packages.
  - Done: marked `AvaScope.Mcp`, `AvaScope.Cli`, and `AvaScope.PreviewHost` explicitly non-packable for this slice.
  - Done: added ignored `artifacts/` package output convention.
  - Done: validated Release build, package creation, `.nuspec` metadata, readme inclusion, dependencies, and ignored artifacts.
- Acceptance Criteria:
  - `AvaScope.Bridge` can produce a local NuGet package with explicit package id, version, description, tags, and repository metadata.
  - `AvaScope.Cli` and `AvaScope.Mcp` packaging posture is explicit, even if final tool/executable packaging remains a later slice.
  - Package output goes to a local ignored artifact folder or an explicit temp output path.
  - No publishing credentials, feeds, or release automation are introduced.
- Validation:
  - `dotnet build AvaScope.slnx`
  - `dotnet pack src/AvaScope.Bridge/AvaScope.Bridge.csproj --no-build --output artifacts/packages`
  - packaging metadata inspection
  - `git status --short`

### M21 CI Validation Slice

- Status: `In Progress`
- Goal: make the documented local validation path run in CI without release publishing.
- Deliverables: GitHub Actions workflow, CI command list, documentation/tracking updates.
- Progress:
  - Pending: add a workflow using .NET 10 SDK setup.
  - Pending: run restore, Release build, full tests, and local pack validation for library packages.
  - Pending: keep workflow local-validation only, with no package push, release creation, or credentials.
- Acceptance Criteria:
  - CI runs on pull requests and pushes to the main development branches.
  - CI uses `dotnet restore`, `dotnet build -c Release --no-restore`, `dotnet test -c Release --no-build`, and local `dotnet pack` commands.
  - Generated packages stay CI artifacts or workspace outputs only and are not published to a feed.
  - Workflow does not require secrets.
- Validation:
  - `dotnet build AvaScope.slnx`
  - `dotnet test AvaScope.slnx --no-build`
  - YAML/manual command inspection
  - `git status --short`

## Decision Log

- `2026-06-06`: Use `docs/DEVELOPMENT_PLAN.md` as the primary tracking document and keep `AGENTS.md` as the mandatory routing entrypoint.
- `2026-06-06`: Use milestone plus `Current Focus` and `Next Action` tracking instead of a sprint board or task ledger.
- `2026-06-06`: Optimize delivery order for vertical slices: foundation, protocol, core, MCP, bridge, screenshot, tree, input, preview.
- `2026-06-06`: Target Avalonia 12 with `net10.0` by default for Avalonia-facing projects.
- `2026-06-06`: Use xUnit for the initial test foundation because the .NET template is available locally and keeps M0 validation simple.
- `2026-06-06`: Protocol contracts use System.Text.Json attributes and remain independent from Avalonia runtime types and MCP SDK types.
- `2026-06-06`: `AvaScope.Core` references `AvaScope.Protocol` for shared transport-neutral session ids and keeps its own core result/error model for adapter-independent behavior.
- `2026-06-06`: Use official `ModelContextProtocol` 1.4.0 and `Microsoft.Extensions.Hosting` 10.0.8 for the initial stdio MCP adapter; official C# SDK docs recommend `AddMcpServer().WithStdioServerTransport().WithTools<T>()` for local stdio servers.
- `2026-06-06`: Use official Avalonia 12.0.4 packages for bridge work and manual `Avalonia.Headless` sessions for bridge smoke tests to avoid mixing xUnit v2 tests with `Avalonia.Headless.XUnit`'s xUnit v3 dependency.
- `2026-06-06`: Bridge top-level discovery combines Avalonia lifetime discovery with explicit weak `RegisterTopLevel` registration because headless and non-desktop hosts may not populate `IClassicDesktopStyleApplicationLifetime.Windows`.
- `2026-06-06`: Screenshot capture uses public Avalonia `RenderTargetBitmap.Render(Visual)` and stream-based `Bitmap.Save(Stream)` output; headless tests enable Skia-backed drawing so output files are non-empty.
- `2026-06-06`: Bridge-local attach discovery uses temp session manifests plus local named pipes, keeping runtime control opt-in and local-only while allowing MCP/CLI adapters to remain thin clients.
- `2026-06-06`: Bridge IPC uses newline-delimited UTF-8 JSON over named pipes with explicit request ids; the implementation uses byte-level pipe reads/writes for deterministic test behavior.
- `2026-06-06`: The reusable local attach client lives in `AvaScope.Core` so MCP and future CLI can share discovery/pipe behavior without referencing Avalonia bridge assemblies.
- `2026-06-06`: `list_top_levels` and `screenshot` MCP tools were exposed in the attach-client slice, then validated by the MCP/Core/pipe screenshot smoke test before closing M5.
- `2026-06-06`: Positive MCP/Core/pipe screenshot validation uses `HeadlessUnitTestSession.Dispatch(Func<Task>)`; awaiting the tool call from the headless UI dispatch context allows bridge server UI-thread work to complete without a manual pump loop.
- `2026-06-06`: M6 tree inspection uses public Avalonia visual/logical traversal APIs (`GetVisualChildren`, `GetLogicalChildren`) and keeps serialization bounded with an explicit depth limit.
- `2026-06-06`: Tree node ids are stable only within the active runtime session and are based on runtime object identity; no persisted cross-process identity guarantee is introduced in M6.
- `2026-06-06`: `find_nodes` searches the already bounded tree model and requires at least one filter so accidental unbounded discovery is avoided; type/text use case-insensitive contains matching, name/automation id use case-insensitive exact matching.
- `2026-06-06`: M7 input MVP deliberately starts with safe local-only operations: Button click is implemented through hit-test plus routed click event, key text mutates a focused `TextBox`, and pointer move is still tracked separately because generic routed/raw pointer injection needs a more precise platform strategy.
- `2026-06-07`: M7 pointer move uses public Avalonia 12 `PointerEventArgs` plus `InputElement.PointerMovedEvent` on the hit-tested input target instead of raw `IInputManager` injection because `TopLevel.InputRoot` is not public; this keeps the bridge off private runtime hooks.
- `2026-06-07`: The first M8 slice uses the official `Avalonia.Markup.Xaml.Loader` 12.0.4 package for standalone runtime `.axaml` loading; `AvaloniaXamlLoader.Load(Uri, Uri)` was rejected because it expects precompiled/resource XAML.
- `2026-06-07`: Preview rendering starts in an isolated `AvaScope.PreviewHost` child process before adding MCP/CLI adapters, preserving the architecture rule that user preview code cannot run inside the MCP server process.
- `2026-06-07`: M8 project-aware preview path resolution is kept as a separate slice before MSBuild integration; it validates the `.csproj` boundary and resolves relative view paths without yet claiming full project resource/code-behind support.
- `2026-06-07`: M8 build preparation currently uses `dotnet build` inside the preview host child process as the isolation boundary; this validates project compilation and keeps build failures structured, but it does not yet load compiled project assemblies/resources into the render path.
- `2026-06-07`: M8 compiled view loading uses the built project assembly plus `avares://<AssemblyName>/<ViewPath>` first, then falls back to standalone runtime XAML loading; this keeps real project code execution inside `AvaScope.PreviewHost`.
- `2026-06-07`: Added M9 to continue after the initial M0-M8 plan by wiring the completed preview host through Core and MCP adapters.
- `2026-06-07`: MCP references `AvaScope.PreviewHost` only to place the host assembly beside the MCP server output; rendering still goes through `PreviewHostClient` and a child process.
- `2026-06-07`: Added M10 for local CLI workflows after preview host and MCP preview integration.
- `2026-06-07`: `avascope mcp` is a process handoff to the colocated MCP server assembly rather than a second in-process MCP host, keeping one canonical MCP implementation.
- `2026-06-07`: Added M11 because the implemented bridge/MCP/CLI/preview workflows now need repository-level usage documentation before broader hardening.
- `2026-06-07`: README intentionally documents current limitations for input, preview resources, hot reload, and diagnostics so users do not assume full DevTools parity yet.
- `2026-06-07`: Added M12 to continue with explicit post-MVP hardening rather than broad untracked expansion.
- `2026-06-07`: Runtime `close_session` is the next hardening slice because stale local bridge sessions/manifests directly affect repeated agent workflows and the tool name is already part of the target MCP shape.
- `2026-06-07`: Runtime `close_session` uses a two-phase bridge IPC close handshake: the session registry is closed before the structured response is flushed, and the bridge server/manifest are stopped afterward on a background task to avoid pipe teardown before the client receives the result.
- `2026-06-07`: Diagnostics is the next P0 hardening slice because current operations return per-tool errors, but there is no aggregate health/version/session surface for agents to inspect before choosing a workflow.
- `2026-06-07`: The first diagnostics slice reports current health and structured unavailable states only; binding, layout, resource, and historical last-error streams remain future diagnostics work until those signals exist.
- `2026-06-07`: M14 targets preview app resources before reload because persistent/reloadable preview sessions should reuse a preview path that already handles app-level resources predictably.
- `2026-06-07`: M14 copies top-level resource entries from the loaded project `Application.Resources` instead of reparenting the resource dictionary, because Avalonia resource dictionaries are owned by a parent once loaded.
- `2026-06-07`: M15 expands diagnostics to preview-host readiness before reload work so agents can distinguish missing preview infrastructure from project/render failures.
- `2026-06-07`: M15 preview diagnostics deliberately checks only host readiness and does not launch `AvaScope.PreviewHost`, build projects, or load user XAML; render/project diagnostics remain tied to actual preview requests.
- `2026-06-07`: M16 starts with preview-session metadata before implementing `reload`, because reload needs a stable persisted request/result boundary that does not keep user code inside MCP.
- `2026-06-07`: M16 preview sessions are metadata records, not persistent Avalonia preview processes; this keeps user project code execution isolated in one-shot `AvaScope.PreviewHost` child processes while still giving reload a stable request/result target.
- `2026-06-07`: M17 will implement reload only for preview sessions first; runtime bridge reload remains separate because it needs different lifecycle and safety semantics.
- `2026-06-07`: M17 preview reload re-renders stored preview requests through the same isolated preview host path and deliberately does not implement runtime bridge reload.
- `2026-06-07`: M18 targets pointer press/release before packaging/CI because input coverage remains a P1 functional gap in the runtime automation workflow.
- `2026-06-07`: M18 pointer press/release uses public Avalonia 12.0.4 routed pointer event args and `PointerPointProperties` update kinds rather than raw platform input injection.
- `2026-06-07`: M19 targets the runtime reload contract next because `reload` is now implemented for preview sessions but still has preview-specific response semantics.
- `2026-06-07`: M19 keeps preview `reload` success responses compatible and treats runtime bridge reload as an explicit local health check plus unsupported diagnostic, not hot reload.
- `2026-06-07`: PreviewHost smoke-test cleanup is best-effort after assertions pass because Windows can transiently hold built sample project files after child-process exit.
- `2026-06-07`: M20 starts package metadata with `AvaScope.Bridge` first because it is the opt-in user-facing library package; executable/tool packaging for CLI/MCP can stay explicit until a later release workflow slice.
- `2026-06-07`: M20 packages `AvaScope.Protocol` and `AvaScope.Core` alongside `AvaScope.Bridge` so the bridge package has resolvable local package dependencies.
- `2026-06-07`: M21 targets CI before publishing or installer work because local build/test/pack validation is now stable enough to automate without credentials.

## Change Log

- `2026-06-06`: Initial development plan created with M0-M8 milestones, tracking rules, acceptance criteria, and validation commands.
- `2026-06-06`: Completed M0 foundation with shared build settings, validation documentation, test project, and Protocol/Core smoke tests; moved active focus to M1.
- `2026-06-06`: Completed M1 protocol contracts with session ids, protocol version metadata, health/list_sessions DTOs, tool result/error shapes, and JSON serialization tests; moved active focus to M2.
- `2026-06-06`: Completed M2 core session model with registry, lifecycle transitions, structured errors, and unit tests; moved active focus to M3.
- `2026-06-06`: Completed M3 minimal MCP adapter with stdio hosting, `health`, `list_sessions`, tool mapping tests, and stdio child-process smoke coverage; moved active focus to M4.
- `2026-06-06`: Completed M4 opt-in bridge MVP with explicit activation/deactivation, local-only runtime scope, UI-thread top-level discovery, explicit top-level registration, and headless bridge smoke coverage; moved active focus to M5.
- `2026-06-06`: Added M5 bridge-local screenshot capture with `ScreenshotResponse`, registered top-level lookup, PNG output, structured missing-top-level errors, and headless file validation; M5 remains in progress pending local attach transport.
- `2026-06-06`: Added M5 bridge IPC foundation with local session manifests, named-pipe server startup/shutdown, IPC DTO JSON tests, manifest lifecycle validation, and pipe health smoke coverage; M5 remains in progress pending MCP/CLI attach client and cross-process screenshot validation.
- `2026-06-06`: Added M5 reusable local attach client and MCP tool adapters for `attach_to_app`, `list_top_levels`, and `screenshot`; M5 remains in progress pending deterministic positive top-level/screenshot validation through the MCP/Core/pipe path.
- `2026-06-06`: Completed M5 runtime screenshot slice with MCP/Core/named-pipe top-level listing and screenshot validation against a headless Avalonia window; moved active focus to M6.
- `2026-06-06`: Added M6 bounded visual/logical tree serialization with protocol DTOs, bridge traversal, MCP/Core/pipe tools, and headless validation; M6 remains in progress pending `find_nodes`.
- `2026-06-06`: Completed M6 tree inspection slice with `find_nodes` filters for type, name, automation id, and text plus path-oriented match results; moved active focus to M7.
- `2026-06-06`: Added M7 input MVP protocol, bridge, Core, and MCP path with headless validation for pointer target lookup, Button click, and focused TextBox key text; M7 remains in progress pending real pointer move injection or explicit limitation handling.
- `2026-06-07`: Completed M7 input slice with routed pointer move, Button click, focused TextBox key text, and unsupported input diagnostics; moved active focus to M8.
- `2026-06-07`: Added the first M8 preview host slice with protocol preview DTOs, isolated child process entrypoint, standalone `.axaml` runtime loading, headless Skia PNG output, and process smoke validation; M8 remains in progress pending project-aware preview loading.
- `2026-06-07`: Added M8 project-aware path resolution for `.csproj` plus relative view paths with process smoke coverage; M8 remains in progress pending MSBuild/design-time build support.
- `2026-06-07`: Added M8 project build boundary in the preview host child process with structured build failure diagnostics; M8 remains in progress pending compiled project assembly/resource loading.
- `2026-06-07`: Completed M8 preview host slice with compiled Avalonia project resource and code-behind smoke rendering; added M9 preview adapter integration as the active focus.
- `2026-06-07`: Completed M9 preview adapter integration with Core `PreviewHostClient`, MCP `preview_axaml`, process smoke coverage, and stdio tool-list validation; added M10 CLI integration as the active focus.
- `2026-06-07`: Completed M10 CLI integration with `avascope preview`, `avascope mcp`, process smoke coverage, and structured invalid-argument errors; added M11 documentation and release readiness as the active focus.
- `2026-06-07`: Completed M11 documentation and release-readiness slice with README usage documentation and validation guide updates; added M12 post-MVP hardening as the active focus.
- `2026-06-07`: Added post-MVP gap audit and selected runtime `close_session` lifecycle support as the next hardening slice.
- `2026-06-07`: Completed M12 close-session hardening with bridge IPC, Core client, MCP tool, manifest cleanup validation, and PreviewHost cleanup retry hardening; added M13 diagnostics surface as the active focus.
- `2026-06-07`: Completed M13 diagnostics surface with protocol DTOs, Core bridge diagnostics, MCP `diagnostics`, unavailable-state handling, and focused tests; added M14 preview app resource scope as the active focus.
- `2026-06-07`: Completed M14 preview app resource scope with compiled `App.axaml` resource loading, resource-backed render validation, structured invalid-app-resource errors, and README updates; added M15 preview diagnostics expansion as the active focus.
- `2026-06-07`: Completed M15 preview diagnostics expansion with preview-host readiness diagnostics, MCP diagnostics composition, build-server isolation hardening, README updates, and full-suite validation; added M16 preview reload foundation as the active focus.
- `2026-06-07`: Completed M16 preview reload foundation with preview session metadata, Core create/list/close lifecycle, MCP preview session tools, README updates, and full-suite validation; added M17 preview reload MVP as the active focus.
- `2026-06-07`: Completed M17 preview reload MVP with Core preview session re-rendering, MCP `reload`, session state recovery/failure handling, README updates, and full-suite validation; added M18 input press/release slice as the active focus.
- `2026-06-07`: Completed M18 input press/release with stable action constants, Avalonia routed pointer press/release events, active pointer reuse, headless bridge validation, README/gap updates, and full-suite validation; added M19 runtime reload contract as the active focus.
- `2026-06-07`: Completed M19 runtime reload contract with preview-compatible reload responses, explicit active-runtime unsupported diagnostics, PreviewHost cleanup hardening, README/gap updates, and full-suite validation; added M20 packaging metadata as the active focus.
- `2026-06-07`: Completed M20 packaging metadata with local library packages for Protocol/Core/Bridge, explicit non-packable executable projects, ignored package artifacts, README updates, and local pack validation; added M21 CI validation as the active focus.
