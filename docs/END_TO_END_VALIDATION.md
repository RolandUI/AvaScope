# AvaScope End-To-End Validation

This document records the `v1.0.0` end-to-end workflow verification for GitHub issue #35. It is a release-readiness ledger, not a replacement for the automated unit, integration, CI, and release workflows.

## Scope

The `v1.0.0` E2E gate covers:

- Source build and test validation.
- Release artifact creation and manifest verification.
- Packaged CLI readiness, preview, animation, preview-session, baseline, report-pack, diff, region assertion, cleanup, and MCP stdio smoke workflows.
- Packaged runtime bridge launch, attach, inspect, input, screenshot, mutation evidence, mutation review, reset, close, and cleanup workflows against the getting-started sample.
- Open P0/P1 blocker audit for the active `v1.0.0` milestone.

## Validation Summary

Validation date: `2026-06-13`

Commit under local validation: `a42f030` plus local #35 validation artifacts.

Outcome: passed.

Residual release risks:

- Linux executable artifacts are packaged and manifest-verified in this Windows validation lane, but Linux runtime execution is not separately smoke-tested in this repository workflow.
- Native IDE extensions, remote/cloud dashboards, no-code attach, process injection, private Avalonia runtime hooks, and destructive runtime actions remain post-1.0 deferrals tracked outside this #35 gate.

## Source Validation

Commands:

```powershell
dotnet restore AvaScope.slnx
dotnet build AvaScope.slnx --no-restore -v:minimal
dotnet test AvaScope.slnx --no-build
```

Result:

- Restore passed.
- Debug build passed with 0 warnings and 0 errors.
- Full Debug test suite passed: 307 passed, 0 failed.

The full test suite includes protocol contracts, Core bridge/preview clients, CLI smoke tests, MCP stdio and tool tests, bridge headless runtime tests, preview host tests, visual regression tests, report-pack tests, documentation tests, and stable-surface tests.

## Release Artifact Validation

Command:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\eng\create-local-release.ps1
```

Result:

- Release build passed with 0 warnings and 0 errors.
- Full Release test suite passed: 307 passed, 0 failed.
- NuGet packages were created:
  - `AvaScope.Protocol.0.9.0.nupkg`
  - `AvaScope.Core.0.9.0.nupkg`
  - `AvaScope.Bridge.0.9.0.nupkg`
- Executable ZIPs were created:
  - `avascope-win-x64-framework-dependent.zip`
  - `avascope-linux-x64-framework-dependent.zip`
- `artifacts/release-manifest.json` was verified with 5 artifacts.
- Packaged `doctor` smoke passed.
- Packaged getting-started sample `preview` smoke passed.

GitHub Release asset dry-run:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\eng\publish-github-release.ps1 -Tag v0.9.0 -DryRun
```

Result: passed. The dry-run validated the three `.nupkg` files, both framework-dependent executable ZIPs, and `release-manifest.json` without publishing.

## Packaged CLI Workflow Validation

Packaged CLI:

```powershell
.\artifacts\executables\avascope-win-x64-framework-dependent\avascope.exe
```

Output root:

```text
artifacts\e2e\v1-20260613064151
```

Validated packaged CLI commands:

- `capabilities --require protocol.capability_discovery,preview.axaml,preview.sessions,baseline.single,reports.evidence_pack,artifacts.html_viewer`
- `doctor --manifest-dir <isolated-dir> --preview-session-store <isolated-dir>`
- `preview <sample.csproj> --profile main --out <png>`
- `preview-animation <sample.csproj> --profile animation --out <png> --frame-strip <png> --viewer <html>`
- `create-preview-session <sample.csproj> --profile main --out <png> --display-name v1-e2e`
- `list-preview-sessions`
- `reload-preview-session --session <session-id>`
- `preview-viewer --session <session-id> --out <html>`
- `close-preview-session --session <session-id>`
- `baseline-create <sample.csproj> --view Views\MainView.axaml --manifest <json> --sizes 720x420,360x240 --out-dir <dir> --theme light --design-data-type AvaScope.GettingStartedApp.SamplePreviewData`
- `baseline-check --manifest <json> --out-dir <dir> --diff-dir <dir> --report <json> --report-pack <dir> --tolerance 0`
- `diff --baseline <png> --current <png> --out <png> --tolerance 0`
- `assert-region --image <png> --assert non_empty --x 0 --y 0 --width 120 --height 120 --crop-out <png>`
- `cleanup`
- `cleanup-bridge-sessions --manifest-dir <isolated-dir>`

Result:

- `capabilities` passed with all required capability ids.
- `doctor` returned `available`.
- `preview` wrote `artifacts\e2e\v1-20260613064151\preview\main.png`.
- `preview-animation` produced 4 frames, a frame strip, and an HTML viewer.
- Preview-session create/list/reload/viewer/close passed for session `44c9083a6607414c8e67246aec755a38`.
- Baseline check passed with 2 entries.
- Report pack status was `passed`.
- Report pack assets existed:
  - `baseline-report.json`
  - `baseline-report.html`
  - `baseline-junit.xml`
  - `baseline.sarif.json`
- Same-image diff passed.
- Non-empty region assertion passed and wrote a crop artifact.
- Cleanup commands passed against isolated stores.

## Packaged Runtime Bridge Validation

Packaged CLI:

```powershell
.\artifacts\executables\avascope-win-x64-framework-dependent\avascope.exe
```

Output root:

```text
artifacts\e2e\runtime-v1-20260613064428
```

Validated runtime commands against `samples\AvaScope.GettingStartedApp` with `AVASCOPE_SAMPLE_BRIDGE=1`:

- `launch-app --command dotnet --args "run --project <sample.csproj>" --manifest-dir <isolated-dir> --out-dir <dir> --env AVASCOPE_SAMPLE_BRIDGE=1 --timeout-ms 20000`
- `attach --session <runtime-session-id> --manifest-dir <isolated-dir>`
- `list-top-levels --session <runtime-session-id> --manifest-dir <isolated-dir>`
- `visual-tree --session <runtime-session-id> --top-level <top-level-id> --max-depth 8 --manifest-dir <isolated-dir>`
- `find-nodes --session <runtime-session-id> --top-level <top-level-id> --type TextBlock --max-depth 8 --max-results 5 --manifest-dir <isolated-dir>`
- `inspect-node --session <runtime-session-id> --top-level <top-level-id> --node <node-id> --manifest-dir <isolated-dir>`
- `audit-ui --session <runtime-session-id> --top-level <top-level-id> --max-depth 8 --max-issues 100 --max-inventory 100 --manifest-dir <isolated-dir>`
- `input --session <runtime-session-id> --top-level <top-level-id> --action pointer_move --x 12 --y 12 --manifest-dir <isolated-dir>`
- `screenshot --session <runtime-session-id> --top-level <top-level-id> --out <png> --manifest-dir <isolated-dir>`
- `mutate-node-evidence --session <runtime-session-id> --top-level <top-level-id> --node <node-id> --operation set_property --property Width --value 260 --value-type double --out-dir <dir> --request-id v1-runtime-width --manifest-dir <isolated-dir>`
- `mutation-review --session <runtime-session-id> --max-results 20 --out <html> --source-project <sample.csproj> --source-view Views\MainView.axaml --source-app App.axaml --source-profile avascope.preview.json --manifest-dir <isolated-dir>`
- `mutate-node --session <runtime-session-id> --top-level <top-level-id> --node <node-id> --operation reset_all --manifest-dir <isolated-dir>`
- `close-session --session <runtime-session-id> --manifest-dir <isolated-dir>`

Result:

- Launched sample process id: `15468`.
- Runtime session id: `76f3b1db60eb4213be63862978f24416`.
- Top-level id: `topLevel:2c59f4e`.
- TextBlock node id: `visual:7625d`.
- Visual tree root: `AvaScope.GettingStartedApp.Views.MainWindow`.
- UI audit issue count: `0`.
- Runtime screenshot existed at `artifacts\e2e\runtime-v1-20260613064428\runtime-screenshot.png`.
- Mutation evidence status: `captured`.
- Runtime mutation status: `applied`.
- Mutation review reported 1 active mutation before reset.
- `reset_all` returned `applied`.
- `close-session` returned session state `closed`.
- The launched sample process was stopped after validation; `Get-Process -Id 15468` returned no process.

Mutation evidence artifacts existed:

- `v1-runtime-width-before.png`
- `v1-runtime-width-after.png`
- `v1-runtime-width-before-visual-tree.json`
- `v1-runtime-width-after-visual-tree.json`
- `v1-runtime-width-diff.png`
- `v1-runtime-width-review.html`

## Packaged MCP Validation

Packaged MCP assembly:

```powershell
.\artifacts\executables\avascope-win-x64-framework-dependent\AvaScope.Mcp.dll
```

Validation:

- Started the packaged MCP server with `dotnet`.
- Sent MCP `initialize` over stdio.
- Sent `notifications/initialized`.
- Sent `tools/list`.

Result:

- Initialize response protocol version: `2025-06-18`.
- Tool count: `30`.
- Required packaged MCP tools were present:
  - `capabilities`
  - `health`
  - `preview_axaml`
  - `mutate_node`
  - `baseline_check`
  - `close_session`

## Open P0/P1 Audit

Command:

```powershell
gh issue list --repo RolandUI/AvaScope --state open --search "milestone:v1.0.0 label:priority:p0,priority:p1" --json number,title,labels,url --limit 50
```

Result:

- No unexpected P0/P1 blocker was found.
- Remaining open P0/P1 issues are planned `v1.0.0` release work:
  - #33 `Release v1.0.0`
  - #35 `R1.0.0-M2 End-To-End Workflow Verification`
  - #36 `R1.0.0-M3 Documentation Completion`
  - #37 `R1.0.0-M4 Release Artifact And Package Verification`
  - #39 `R1.0.0-M6 Stable Release Commit And Publication`

## Remote Validation Baseline

The #35 start documentation commit `a42f030` passed:

- GitHub CI workflow `27456669605`
- GitHub Release workflow `27456669606`

The final #35 implementation commit must also pass GitHub CI and Release no-op validation before #35 is closed.
