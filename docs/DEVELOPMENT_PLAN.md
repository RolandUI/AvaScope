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

- `M3 Minimal MCP Adapter`
- Status: `In Progress`
- Owner: autonomous agent
- Started: `2026-06-06`
- Goal: expose the first MCP surface as a thin adapter over protocol/core.

## Next Action

Verify the current official MCP .NET SDK/API shape, then add `AvaScope.Mcp` with stdio hosting, health/version, and `list_sessions` backed by `AvaScope.Core`.

## Latest Validation

- `2026-06-06`: `dotnet restore AvaScope.slnx` passed.
- `2026-06-06`: `dotnet build AvaScope.slnx` passed with 0 warnings and 0 errors.
- `2026-06-06`: `dotnet test AvaScope.slnx` passed with 16 tests.
- `2026-06-06`: `dotnet test AvaScope.slnx --filter Protocol` passed with 7 tests.
- `2026-06-06`: `dotnet test AvaScope.slnx --filter Core` passed with 9 tests.
- `2026-06-06`: `AvaScope.Protocol` package list checked; no package references found.
- `2026-06-06`: `AvaScope.Core` package list checked; no package references found.
- `2026-06-06`: `rg "Avalonia|ModelContextProtocol|Mcp|MCP" src\AvaScope.Protocol tests\AvaScope.Tests\Protocol` found no matches.
- `2026-06-06`: `rg "Avalonia|ModelContextProtocol|Mcp|MCP" src\AvaScope.Core tests\AvaScope.Tests\Core` found no matches.
- `2026-06-06`: Markdown tracking fields checked for `Current Focus`, `Next Action`, `Status`, `Acceptance Criteria`, and `Validation`.

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

- Status: `In Progress`
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

- Status: `Not Started`
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

- Status: `Not Started`
- Goal: capture screenshots from a running bridged Avalonia app.
- Deliverables: attach flow, screenshot request/response, generated image file output, sample validation.
- Acceptance Criteria:
  - Screenshot output path is returned as structured data.
  - Failed capture returns a structured diagnostic error.
  - Capture behavior is covered by an integration test or documented manual validation.
- Validation:
  - screenshot smoke test against sample app
  - output file existence and non-empty image validation

### M6 Tree Inspection Slice

- Status: `Not Started`
- Goal: expose visual and logical tree inspection with stable node identity.
- Deliverables: tree serialization, depth limits, node metadata, basic find behavior.
- Acceptance Criteria:
  - Tree results are bounded by default.
  - Node IDs are stable within a session.
  - Find supports at least type, name, automation id, and text where available.
- Validation:
  - tree serialization unit tests
  - sample app visual/logical tree integration test

### M7 Input Slice

- Status: `Not Started`
- Goal: send basic local-only input to a running bridged Avalonia app.
- Deliverables: click, pointer move, key text commands, safety checks.
- Acceptance Criteria:
  - Input targets must resolve to an active local session.
  - Unsupported input returns structured diagnostics.
  - Commands execute on the correct UI/input path for Avalonia 12.
- Validation:
  - input smoke test against sample app
  - negative tests for invalid session and unsupported input

### M8 Preview Host Slice

- Status: `Not Started`
- Goal: render a `.axaml` view from a project in an isolated preview process.
- Deliverables: preview host process, project/view selection, headless Skia rendering, basic variants.
- Acceptance Criteria:
  - User application code runs outside the MCP server process.
  - Preview supports width, height, DPI, and theme inputs.
  - Render output is returned as a file path with structured diagnostics.
- Validation:
  - preview smoke test against sample Avalonia 12 project
  - output file existence and non-empty image validation

## Decision Log

- `2026-06-06`: Use `docs/DEVELOPMENT_PLAN.md` as the primary tracking document and keep `AGENTS.md` as the mandatory routing entrypoint.
- `2026-06-06`: Use milestone plus `Current Focus` and `Next Action` tracking instead of a sprint board or task ledger.
- `2026-06-06`: Optimize delivery order for vertical slices: foundation, protocol, core, MCP, bridge, screenshot, tree, input, preview.
- `2026-06-06`: Target Avalonia 12 with `net10.0` by default for Avalonia-facing projects.
- `2026-06-06`: Use xUnit for the initial test foundation because the .NET template is available locally and keeps M0 validation simple.
- `2026-06-06`: Protocol contracts use System.Text.Json attributes and remain independent from Avalonia runtime types and MCP SDK types.
- `2026-06-06`: `AvaScope.Core` references `AvaScope.Protocol` for shared transport-neutral session ids and keeps its own core result/error model for adapter-independent behavior.

## Change Log

- `2026-06-06`: Initial development plan created with M0-M8 milestones, tracking rules, acceptance criteria, and validation commands.
- `2026-06-06`: Completed M0 foundation with shared build settings, validation documentation, test project, and Protocol/Core smoke tests; moved active focus to M1.
- `2026-06-06`: Completed M1 protocol contracts with session ids, protocol version metadata, health/list_sessions DTOs, tool result/error shapes, and JSON serialization tests; moved active focus to M2.
- `2026-06-06`: Completed M2 core session model with registry, lifecycle transitions, structured errors, and unit tests; moved active focus to M3.
