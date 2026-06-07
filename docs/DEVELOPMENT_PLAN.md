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

- `M12 Post-MVP Hardening`
- Status: `In Progress`
- Owner: autonomous agent
- Started: `2026-06-07`
- Goal: close the highest-risk gaps after the first usable bridge, preview, MCP, and CLI workflow set.

## Next Action

Run a focused gap audit against the product goals and choose the next vertical slice; prioritize implemented-but-undocumented limits, missing structured diagnostics, `close_session`, `reload`, or package/release readiness.

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

- Status: `In Progress`
- Goal: audit and close the highest-risk gaps in the first usable AvaScope workflow set.
- Deliverables: prioritized gap list, next vertical hardening slice, validation updates.
- Acceptance Criteria:
  - Gaps are ranked by user impact and architectural risk.
  - The next slice is small enough to validate and commit independently.
  - Any new behavior remains covered by focused tests or explicit validation notes.
- Validation:
  - audit notes in this plan or a dedicated docs file
  - relevant targeted test command for the selected slice

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
