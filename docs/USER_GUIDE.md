# AvaScope User Guide

Detailed usage notes for AvaScope. For the short public project overview, see the [root README](../README.md).

AvaScope is an agent-focused local control plane for Avalonia apps. It gives CLI and MCP clients structured ways to inspect running UI, render previews, drive narrow runtime actions, capture screenshots, collect diagnostics, and hand off evidence artifacts. It targets Avalonia 12 and `net10.0`.

## Current Capabilities

- Agent-oriented inspect, preview, act, evidence, and cleanup workflows through CLI and MCP.
- Opt-in runtime bridge for Avalonia apps.
- Local bridge discovery through session manifests and named pipes.
- Runtime top-level listing, screenshots, bounded visual/logical trees, node search, and basic input.
- Reversible runtime `mutate-node`, `mutate-node-evidence`, and `mutation-review` workflows for selected safe style, layout, text, class, and resource experiments against bridge-enabled apps.
- Isolated preview host process for `.axaml` rendering.
- Preview project/build-output metadata, binding/resource diagnostics, and advisory layout warnings.
- Runtime `inspect_node` computed visual/style/layout property values.
- Multi-size preview, contact-sheet output, screenshot diff, and scoped preview-session cleanup workflows.
- File-backed preview viewer export with `previewUrl` handoff for Codex in-app browser workflows.
- MCP stdio server with structured tools.
- `avascope` CLI with doctor, preview, runtime inspection, diagnostics, and MCP handoff commands.
- Explicit `capabilities` discovery for protocol, CLI/MCP tools, runtime mutation, preview, diagnostics, baselines, reports, and artifact support.
- Getting-started sample app for the first preview and bridge workflow.

## Agent Control Model

AvaScope is designed around small, composable tool calls that an agent can chain safely:

1. Discover or launch a local bridge-enabled app.
2. Inspect top-levels, visual/logical trees, node state, diagnostics, and preview metadata.
3. Act through bounded local commands such as focus, text input, selection, scrolling, screenshots, and baseline checks.
4. Capture evidence as structured JSON plus file paths for screenshots, diffs, reports, or local HTML viewers.
5. Close sessions and clean stale AvaScope-owned metadata explicitly.

The `v0.7.0` release line added the runtime control-plane layer: bounded reversible style/layout/text/class/resource mutations with mutation ids, reset operations, and before/after evidence packages containing screenshots, visual tree snapshots, and optional pixel diffs. The active `v0.8.0` line turns those agent experiments into repeatable validation workflows with baseline suites, comparison rules, and reviewable report packs.

## Project Layout

- `src/AvaScope.Protocol`: transport-neutral DTOs and stable JSON contracts.
- `src/AvaScope.Core`: reusable session registry, local bridge client, and preview host client.
- `src/AvaScope.Bridge`: opt-in package loaded by Avalonia apps for runtime inspection.
- `src/AvaScope.PreviewHost`: child process that builds/loads views and renders previews.
- `src/AvaScope.Mcp`: stdio MCP adapter over Core.
- `src/AvaScope.Cli`: local `avascope` command.
- `samples/AvaScope.GettingStartedApp`: tiny Avalonia app for first preview and bridge workflows.
- `tests/AvaScope.Tests`: protocol, core, MCP, bridge, preview host, and CLI tests.

## Project Management

AvaScope execution is tracked in GitHub Issues, Milestones, and the public [AvaScope Roadmap](https://github.com/users/RolandUI/projects/4) Project board. Use [GITHUB_PROJECT_WORKFLOW.md](GITHUB_PROJECT_WORKFLOW.md) for labels, status flow, milestone rules, and board maintenance. [DEVELOPMENT_PLAN.md](DEVELOPMENT_PLAN.md) is kept as a compact local handoff and validation log.

## Build And Test

```powershell
dotnet restore AvaScope.slnx
dotnet build AvaScope.slnx
dotnet test AvaScope.slnx
```

Targeted checks:

```powershell
dotnet test AvaScope.slnx --no-build --filter Protocol
dotnet test AvaScope.slnx --no-build --filter Core
dotnet test AvaScope.slnx --no-build --filter Mcp
dotnet test AvaScope.slnx --no-build --filter Bridge
dotnet test AvaScope.slnx --no-build --filter FullyQualifiedName~PreviewHost
dotnet test AvaScope.slnx --no-build --filter FullyQualifiedName~Cli
```

One-command local Release build for external project testing:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\eng\create-local-release.ps1
```

The script runs Release restore/build/test, creates local NuGet packages, creates framework-dependent executable ZIPs, verifies `artifacts\release-manifest.json`, and smoke-tests the packaged Windows CLI against the getting-started sample. Use the printed `artifacts\executables\avascope-win-x64-framework-dependent\avascope.exe` path for testing other Avalonia projects.

Local package validation:

```powershell
dotnet build AvaScope.slnx -c Release
dotnet pack .\src\AvaScope.Protocol\AvaScope.Protocol.csproj -c Release --no-build --output .\artifacts\packages
dotnet pack .\src\AvaScope.Core\AvaScope.Core.csproj -c Release --no-build --output .\artifacts\packages
dotnet pack .\src\AvaScope.Bridge\AvaScope.Bridge.csproj -c Release --no-build --output .\artifacts\packages
```

The library package slice produces local NuGet packages for `AvaScope.Protocol`, `AvaScope.Core`, and `AvaScope.Bridge`. `AvaScope.Mcp`, `AvaScope.Cli`, and `AvaScope.PreviewHost` are explicitly not packable; executable distribution uses the local publish/ZIP workflow below.

Local executable package validation:

```powershell
dotnet build AvaScope.slnx -c Release
powershell -NoProfile -ExecutionPolicy Bypass -File .\eng\package-executables.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File .\eng\verify-artifacts.ps1
```

The executable package slice produces framework-dependent publish artifacts by default, such as `artifacts\executables\avascope-win-x64-framework-dependent.zip` and `artifacts\executables\avascope-linux-x64-framework-dependent.zip`. Each ZIP contains the `avascope` CLI, `AvaScope.Mcp`, and `AvaScope.PreviewHost` in one directory so `avascope mcp` and preview rendering can find their co-located assemblies. Framework-dependent packages require a compatible local .NET runtime and do not publish to any feed.

Self-contained executable ZIPs are available as an explicit local/package validation lane:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\eng\create-local-release.ps1 -RuntimeIdentifiers win-x64 -ExecutablePackageKind self-contained -SkipTests -SkipSampleSmoke
```

This produces artifacts such as `artifacts\executables\avascope-win-x64-self-contained.zip`. Self-contained ZIPs are not the default CI or release asset set yet; use the package kind parameter intentionally when validating or publishing that artifact shape.

`eng\verify-artifacts.ps1` writes `artifacts\release-manifest.json`, a local ignored JSON manifest with artifact names, relative paths, byte sizes, SHA-256 hashes, and executable package kind for the three NuGet packages plus executable ZIP artifacts.

The default executable package targets are `win-x64` and `linux-x64`. Pass `-RuntimeIdentifiers win-x64` or `-ExecutableRuntimeIdentifiers win-x64` to the package and verify scripts when validating a narrower local artifact set. Pass `-PackageKind self-contained` to `package-executables.ps1`, or `-ExecutablePackageKind self-contained` to `create-local-release.ps1`, `verify-artifacts.ps1`, or `publish-github-release.ps1` when working with the opt-in self-contained lane.

CI validation runs restore, Release build, Release test, local library pack, local executable package, and artifact verification commands in GitHub Actions on pushes and pull requests. It does not publish packages or require secrets.

## Release

Development is release-based. Define and complete the next target in [RELEASE_PLAN.md](RELEASE_PLAN.md) before increasing the repository version. The version bump is the release commit, not a planning step.

Release publishing is handled by GitHub Actions. Add a repository secret named:

```text
NUGET_API_KEY
```

The release version is the `<Version>` value in `Directory.Build.props`. To release, first move the target in [RELEASE_PLAN.md](RELEASE_PLAN.md) to `Release Candidate` after the release gate passes. Then increase the version, commit, and push to `master`:

```powershell
git add Directory.Build.props docs\RELEASE_PLAN.md docs\DEVELOPMENT_PLAN.md
git commit -m "Release 0.2.0"
git push origin master
```

The `Release` workflow reads `Directory.Build.props`, checks whether `v<Version>` already exists on the remote, and only releases when that tag is missing. Automatic publish on push also requires the commit subject to be exactly `Release <Version>` and [RELEASE_PLAN.md](RELEASE_PLAN.md) to declare the same target version in `Release Candidate` state. If the version was already released, the workflow exits without publishing.

When a new version is detected, the workflow runs the full local release gate, dry-runs the publish set, publishes `AvaScope.Protocol`, `AvaScope.Core`, and `AvaScope.Bridge` to nuget.org and GitHub Packages in dependency order, then creates the `v<Version>` tag on the release commit.

The same workflow creates or updates the GitHub Release for the tag and uploads these release assets:

- `AvaScope.Protocol`, `AvaScope.Core`, and `AvaScope.Bridge` `.nupkg` files.
- `avascope-win-x64-framework-dependent.zip`.
- `avascope-linux-x64-framework-dependent.zip`.
- `release-manifest.json`.

The default workflow publishes framework-dependent executable ZIPs. To manually validate or publish a self-contained GitHub Release asset set, create local artifacts with `-ExecutablePackageKind self-contained` and pass the same package kind to `eng\publish-github-release.ps1`.

Manual local publish is still available when needed. Create and verify the Release artifacts first:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\eng\create-local-release.ps1
```

Dry-run the NuGet publish inputs:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\eng\publish-nuget.ps1 -DryRun
```

Publish the public library packages to nuget.org with an API key from nuget.org:

```powershell
$env:AVASCOPE_NUGET_API_KEY = "<nuget-api-key>"
powershell -NoProfile -ExecutionPolicy Bypass -File .\eng\publish-nuget.ps1
```

The publish script pushes `AvaScope.Protocol`, `AvaScope.Core`, and `AvaScope.Bridge` from `artifacts\packages` in dependency order. It reads the version from `Directory.Build.props`, rejects missing or stale `AvaScope.*.nupkg` artifacts, and never stores the API key in source.

The workflow can also be run manually. Manual runs validate by default; set `publish=true` only when intentionally republishing the current `Directory.Build.props` version. Package pushes use duplicate skipping so a manual run can still create or update the GitHub Release assets for an existing version.

## Public Alpha Local Install

AvaScope public-alpha executable artifacts are built locally for now; no installer publishing is configured. Framework-dependent ZIPs are the default release asset shape; self-contained ZIPs are opt-in local/publish-script artifacts.

Create and verify local Release artifacts:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\eng\create-local-release.ps1
```

Run the packaged Windows CLI/MCP bundle directly from the publish directory:

```powershell
.\artifacts\executables\avascope-win-x64-framework-dependent\avascope.exe doctor
.\artifacts\executables\avascope-win-x64-framework-dependent\avascope.exe mcp
.\artifacts\executables\avascope-win-x64-framework-dependent\avascope.exe preview .\samples\AvaScope.GettingStartedApp\AvaScope.GettingStartedApp.csproj --view Views\MainView.axaml --out .\artifacts\samples\getting-started-preview-packaged.png --width 720 --height 420 --theme light --design-data-type AvaScope.GettingStartedApp.SamplePreviewData
```

For external app checks, use the same packaged Release executable path:

```powershell
.\artifacts\executables\avascope-win-x64-framework-dependent\avascope.exe preview path\to\App.csproj --view Views\MainWindow.axaml --out .\artifacts\samples\external-preview.png --width 1440 --height 900 --theme dark
```

Use the local NuGet packages for an Avalonia app that wants the opt-in bridge:

```powershell
dotnet add path\to\YourApp.csproj package AvaScope.Bridge --version 0.1.0 --source .\artifacts\packages
```

## Getting Started Sample

The repository includes a tiny Avalonia 12 sample app at `samples\AvaScope.GettingStartedApp`.

For a full packaged-CLI runbook covering doctor, preview profiles, preview sessions, runtime bridge inspection, screenshots, input, and diff/baseline workflows, use [AGENT_WORKFLOW.md](AGENT_WORKFLOW.md).

Build AvaScope and render the sample preview:

```powershell
dotnet build AvaScope.slnx
dotnet .\src\AvaScope.Cli\bin\Debug\net10.0\avascope.dll preview .\samples\AvaScope.GettingStartedApp\AvaScope.GettingStartedApp.csproj --view Views\MainView.axaml --out .\artifacts\samples\getting-started-preview.png --width 720 --height 420 --theme light --design-data-type AvaScope.GettingStartedApp.SamplePreviewData
```

Run the sample with the opt-in local bridge enabled:

```powershell
$env:AVASCOPE_SAMPLE_BRIDGE = "1"
dotnet run --project .\samples\AvaScope.GettingStartedApp\AvaScope.GettingStartedApp.csproj
```

In another terminal, inspect local bridge sessions and use the reported session id with runtime commands:

```powershell
dotnet .\src\AvaScope.Cli\bin\Debug\net10.0\avascope.dll diagnostics --max-sessions 10
dotnet .\src\AvaScope.Cli\bin\Debug\net10.0\avascope.dll list-top-levels --session <session-id>
```

The bridge is not enabled unless `AVASCOPE_SAMPLE_BRIDGE` is set to `1` or `true`.

## CLI

Build first, then run the CLI assembly from the build output:

```powershell
dotnet .\src\AvaScope.Cli\bin\Debug\net10.0\avascope.dll preview path\to\App.csproj --view Views\MainView.axaml --out .\preview.png --width 1440 --height 900 --dpi 96 --theme light --culture ja-JP --design-data-type MyApp.Design.PreviewData
```

Query supported protocol and tool features before relying on newer agent workflows:

```powershell
dotnet .\src\AvaScope.Cli\bin\Debug\net10.0\avascope.dll capabilities
dotnet .\src\AvaScope.Cli\bin\Debug\net10.0\avascope.dll capabilities --require runtime.ui_audit,reports.evidence_pack
```

`capabilities` writes `ToolResult<AvaScopeCapabilitiesResponse>` with `capabilities[]`, `tools[]`, `runtimeMutationCapabilities[]`, and `compatibilityPolicy`. Unsupported required feature ids fail with `capability_not_supported`, `unsupportedCapabilities`, and a `nextAction` detail so clients can branch by feature id rather than guessing from package versions.

The command writes a structured JSON `ToolResult<PreviewResponse>` to stdout. On success, `value.filePath` points to the generated PNG.
`--width` and `--height` can be omitted when the root AXAML declares design-time dimensions with `d:DesignWidth`/`d:DesignHeight` or `Design.Width`/`Design.Height`. Project previews also apply root design-time data from `Design.DataContext` or `d:DataContext="{x:Static ...}"`; an explicit `--design-data-type` still takes precedence.

Repeated preview settings can live in `avascope.preview.json` beside the project file:

```json
{
  "profiles": {
    "main": {
      "view": "Views/MainView.axaml",
      "out": "../../artifacts/samples/main-preview.png",
      "width": 720,
      "height": 420,
      "theme": "light",
      "designDataType": "MyApp.Design.PreviewData",
      "displayName": "Main preview",
      "variants": {
        "dark": {
          "theme": "dark",
          "out": "../../artifacts/samples/main-preview-dark.png"
        },
        "hu": {
          "culture": "hu-HU",
          "out": "../../artifacts/samples/main-preview-hu.png"
        }
      }
    }
  }
}
```

Use the profile from preview or durable preview-session commands:

```powershell
dotnet .\src\AvaScope.Cli\bin\Debug\net10.0\avascope.dll preview path\to\App.csproj --profile main
dotnet .\src\AvaScope.Cli\bin\Debug\net10.0\avascope.dll preview path\to\App.csproj --profile main --variant dark
dotnet .\src\AvaScope.Cli\bin\Debug\net10.0\avascope.dll create-preview-session path\to\App.csproj --profile main
```

Named variants are applied after the base profile and before explicit CLI options, so `--height 600` still overrides a variant height. Profile `out`, `contactSheet`, `frameStripPath`, and `viewerPath` paths are resolved relative to the profile file; `--profile-file <path>` can point to a non-default profile file.

Render multiple viewport sizes from one preview request:

```powershell
dotnet .\src\AvaScope.Cli\bin\Debug\net10.0\avascope.dll preview path\to\App.csproj --view Views\MainView.axaml --out .\preview.png --sizes 1440x900,1280x720,900x700 --theme light --contact-sheet .\preview-contact-sheet.png
```

The command writes a structured JSON `ToolResult<PreviewBatchResponse>`. Each `entries[]` item has a deterministic per-size output path and an independent `ToolResult<PreviewResponse>`, so one failed size does not discard successful screenshots.

Render deterministic animation time-offset samples:

```powershell
dotnet .\src\AvaScope.Cli\bin\Debug\net10.0\avascope.dll preview-animation path\to\App.csproj --view Views\AnimatedView.axaml --out .\animation.png --time-offsets 0,150,900,900 --width 720 --height 420 --theme light --frame-strip .\animation-strip.png --viewer .\animation.html
```

The command writes per-offset PNG frames, an optional frame strip, and a structured `ToolResult<PreviewAnimationResponse>`. When `--viewer` is supplied, the response includes `viewer.previewUrl`, a `file://` URL for a self-contained HTML timeline viewer that embeds the sampled frames, motion summary, diagnostics, and JSON response.

Animation sampling advances the public Avalonia headless render timer inside isolated PreviewHost child processes. Repeated offsets inside one request reuse the first successful frame for that offset so agents can produce stable duplicate artifacts. AvaScope reports pixel deltas and final-state stability diagnostics from sampled frames. Moving-node or property metadata is reported with explicit `not_available` provenance when reliable public Avalonia APIs do not expose it.

Create and manage durable preview sessions from the CLI:

```powershell
dotnet .\src\AvaScope.Cli\bin\Debug\net10.0\avascope.dll create-preview-session path\to\App.csproj --view Views\MainView.axaml --out .\preview.png --width 1440 --height 900 --theme light --display-name "Main preview"
dotnet .\src\AvaScope.Cli\bin\Debug\net10.0\avascope.dll list-preview-sessions
dotnet .\src\AvaScope.Cli\bin\Debug\net10.0\avascope.dll reload-preview-session --session <preview-session-id>
dotnet .\src\AvaScope.Cli\bin\Debug\net10.0\avascope.dll preview-viewer --session <preview-session-id> --out .\preview-viewer.html
dotnet .\src\AvaScope.Cli\bin\Debug\net10.0\avascope.dll close-preview-session --session <preview-session-id>
```

Preview-session CLI commands persist metadata in the same local AvaScope preview-session store used by MCP. They store the original request and latest render result, then re-render through `AvaScope.PreviewHost` child processes on reload.

`preview-viewer` writes a self-contained HTML viewer for the latest successful render and returns a structured `ToolResult<PreviewViewerResponse>` with `viewerPath`, `previewUrl`, and `agentReview.previewUrls`. Open the `file://` `previewUrl` in the Codex in-app browser to review the screenshot, metadata, diagnostics, and session JSON beside the thread. The viewer does not start a network listener.

Watch a preview session and reload when project or view files change:

```powershell
dotnet .\src\AvaScope.Cli\bin\Debug\net10.0\avascope.dll watch-preview-session --session <preview-session-id> --timeout-ms 30000 --settle-ms 250 --max-reloads 1
dotnet .\src\AvaScope.Cli\bin\Debug\net10.0\avascope.dll watch-preview-session --session <preview-session-id> --timeout-ms 30000 --watch Views\MainView.axaml
```

Watch mode is bounded by `--timeout-ms`. It emits a structured `ToolResult<PreviewWatchResponse>` with changed/reloaded events and does not keep user project code loaded in the CLI process.

Start the MCP server through the CLI:

```powershell
dotnet .\src\AvaScope.Cli\bin\Debug\net10.0\avascope.dll mcp
```

Attach to an active local bridge session:

```powershell
dotnet .\src\AvaScope.Cli\bin\Debug\net10.0\avascope.dll attach --process 1234
dotnet .\src\AvaScope.Cli\bin\Debug\net10.0\avascope.dll attach --process-name MyApp
dotnet .\src\AvaScope.Cli\bin\Debug\net10.0\avascope.dll attach --latest true --process-name MyApp
dotnet .\src\AvaScope.Cli\bin\Debug\net10.0\avascope.dll attach --session session-id
dotnet .\src\AvaScope.Cli\bin\Debug\net10.0\avascope.dll attach --manifest C:\Temp\AvaScope\sessions\session-id.json
```

Use `--manifest-dir <dir>` on runtime CLI commands when the inspected app writes bridge manifests to a selected local directory. AvaScope never silently picks between multiple matching live manifests; retry with `--session`, `--process`, `--process-name`, or `--manifest` when attach is ambiguous. `--latest true` selects the newest active matching manifest while excluding stale process records and still fails if multiple candidates are equivalently latest.

Launch an explicitly bridge-enabled local app and wait for its bridge session:

```powershell
dotnet .\src\AvaScope.Cli\bin\Debug\net10.0\avascope.dll launch-app --command dotnet --args "run --project path\to\App.csproj" --env AVASCOPE_SAMPLE_BRIDGE=1 --manifest-dir C:\Temp\AvaScope\sessions --out-dir .\artifacts\launch
```

The helper sets `AVASCOPE_BRIDGE_MANIFEST_DIR` for the child process, captures stdout/stderr to deterministic files, waits for a bridge manifest from the launched process, and returns session, top-level when available, process, manifest, stdout, and stderr details. It does not inject into apps; the app must explicitly enable `AvaScopeBridge.Activate`.

List top-level windows/views and capture a runtime screenshot from an active bridge session:

```powershell
dotnet .\src\AvaScope.Cli\bin\Debug\net10.0\avascope.dll list-top-levels --session session-id --manifest-dir C:\Temp\AvaScope\sessions
dotnet .\src\AvaScope.Cli\bin\Debug\net10.0\avascope.dll screenshot --session session-id --top-level topLevel:1234 --out screenshot.png --manifest-dir C:\Temp\AvaScope\sessions
```

Read bounded runtime trees from an active bridge session:

```powershell
dotnet .\src\AvaScope.Cli\bin\Debug\net10.0\avascope.dll visual-tree --session session-id --top-level topLevel:1234 --max-depth 4
dotnet .\src\AvaScope.Cli\bin\Debug\net10.0\avascope.dll logical-tree --session session-id --top-level topLevel:1234 --max-depth 4
```

Runtime tree, search, inspect, input, and screenshot responses include a `target` object with the current `sessionId`, `topLevelId`, `targetKind`, `capturedAt`, top-level generation metadata, and, when applicable, `treeKind`, `nodeId`, and node generation metadata. Carry that object into follow-up commands instead of guessing which visual/logical node id or top-level context belongs together. Missing or stale node references return structured details with the requested `topLevelId`, `treeKind`, `nodeId`, request id when available, and a `nextAction`.

Inspect a single runtime tree node by stable node id:

```powershell
dotnet .\src\AvaScope.Cli\bin\Debug\net10.0\avascope.dll inspect-node --session session-id --top-level topLevel:1234 --node visual:5678
dotnet .\src\AvaScope.Cli\bin\Debug\net10.0\avascope.dll inspect-node --session session-id --top-level topLevel:1234 --node logical:5678 --tree-kind logical
```

`inspect-node` includes bounded `computedProperties` for high-value visual, style, text, and layout properties. Provenance uses public Avalonia diagnostic priority where available and reports `unknown` or `not_available` instead of guessing private style/resource origins. For selected runtime nodes it can also include `scrollState` for `ScrollViewer` metrics, `bindingState` with `DataContext` type and explicit binding metadata availability, `accessibilityState` from public automation/focus metadata, `validationState` from `DataValidationErrors`, and `debugState` fields from controls that implement the opt-in `IAvaScopeDebugStateProvider` bridge contract.

Find runtime tree nodes by type, name, automation id, or text:

```powershell
dotnet .\src\AvaScope.Cli\bin\Debug\net10.0\avascope.dll find-nodes --session session-id --top-level topLevel:1234 --type TextBlock --max-depth 6
dotnet .\src\AvaScope.Cli\bin\Debug\net10.0\avascope.dll find-nodes --session session-id --top-level topLevel:1234 --tree-kind logical --automation-id save-button --max-results 10
```

Build a bounded accessibility, validation, and component inventory report from the runtime tree:

```powershell
dotnet .\src\AvaScope.Cli\bin\Debug\net10.0\avascope.dll audit-ui --session session-id --top-level topLevel:1234 --tree-kind visual --max-depth 8 --max-issues 100 --max-inventory 100
```

`audit-ui` returns `ToolResult<UiAuditResponse>` with `summary`, bounded `issues`, bounded `inventory`, and `agentReview`. It reports actionable controls missing accessible names or stable automation ids, keyboard focus metadata, runtime validation errors, control/class/component-pattern counts, and explicit `not_available` inventory entries for style/resource/template/theme scopes that the runtime tree cannot prove reliably.

Send local-only runtime input to an active bridge session:

```powershell
dotnet .\src\AvaScope.Cli\bin\Debug\net10.0\avascope.dll input --session session-id --top-level topLevel:1234 --action click --x 120 --y 40
dotnet .\src\AvaScope.Cli\bin\Debug\net10.0\avascope.dll input --session session-id --top-level topLevel:1234 --action focus --target-node visual:5678
dotnet .\src\AvaScope.Cli\bin\Debug\net10.0\avascope.dll input --session session-id --top-level topLevel:1234 --action clear_text --target-node visual:5678
dotnet .\src\AvaScope.Cli\bin\Debug\net10.0\avascope.dll input --session session-id --top-level topLevel:1234 --action key_down --key Enter --modifiers Control+Shift --target-node visual:5678
dotnet .\src\AvaScope.Cli\bin\Debug\net10.0\avascope.dll input --session session-id --top-level topLevel:1234 --action select --target-node visual:tabControl --text 1
dotnet .\src\AvaScope.Cli\bin\Debug\net10.0\avascope.dll input --session session-id --top-level topLevel:1234 --action scroll --target-node visual:scrollViewer --y 120
```

Input responses include `pointerButton` for supported pointer/click actions, `inputKey`/`keyModifiers` for routed key actions, wheel/scroll deltas for scroll actions, and bounded metadata such as selected index/item or before/after scroll offsets.

Apply a reversible runtime mutation and capture an agent evidence package:

```powershell
dotnet .\src\AvaScope.Cli\bin\Debug\net10.0\avascope.dll mutate-node --session session-id --top-level topLevel:1234 --node visual:5678 --operation set_property --property Width --value 240 --value-type double
dotnet .\src\AvaScope.Cli\bin\Debug\net10.0\avascope.dll mutate-node --session session-id --top-level topLevel:1234 --node visual:5678 --operation reset_mutation --mutation-id mutation-id
dotnet .\src\AvaScope.Cli\bin\Debug\net10.0\avascope.dll mutate-node-evidence --session session-id --top-level topLevel:1234 --node visual:5678 --operation set_property --property Background --value "#0066ff" --value-type brush --out-dir .\artifacts\mutation-evidence --request-id background-check
dotnet .\src\AvaScope.Cli\bin\Debug\net10.0\avascope.dll mutation-review --session session-id --max-results 20 --out .\artifacts\mutation-evidence\review.html --source-project path\to\App.csproj --source-view Views\MainView.axaml --source-app App.axaml --source-profile avascope.preview.json
```

`mutate-node-evidence` runs a fixed local loop: before screenshot, before visual tree, mutation, after screenshot, after visual tree, optional image diff, and local HTML review artifact generation. The response is `ToolResult<RuntimeMutationEvidenceResponse>` with artifact file paths, mutation status, before/after target summaries, bounded diagnostics, changed-pixel metrics when diffing is enabled, `reviewArtifact` with a file URL for human inspection, and `agentReview` with a mutation summary plus bounded artifact/review URL handoff. Use `--diff false` to skip pixel comparison and `--tolerance <0-255>` to allow channel tolerance in the diff.

`mutation-review` returns `ToolResult<RuntimeMutationReviewResponse>` for one local bridge session. It includes bounded mutation history, active override summaries, reset handoff metadata for `reset_mutation` / `reset_all`, optional `sourceContext`, advisory `sourceSuggestions`, an optional HTML review artifact when `--out <review.html>` is supplied, and `agentReview` with the active mutation shortlist, source-suggestion count, and review URL.

Use `--source-project`, `--source-view`, `--source-app`, and `--source-profile` when an agent wants a source-aware handoff after runtime experiments. The suggestions report likely source target kind, file status, confidence, suggested member/property/class/resource key, limitations, and manual action text. They never modify project files automatically; agents must still inspect the suggested source before making an explicit patch.

Close an active local bridge session:

```powershell
dotnet .\src\AvaScope.Cli\bin\Debug\net10.0\avascope.dll close-session --session session-id
```

Read local bridge and preview-host diagnostics:

```powershell
dotnet .\src\AvaScope.Cli\bin\Debug\net10.0\avascope.dll doctor
dotnet .\src\AvaScope.Cli\bin\Debug\net10.0\avascope.dll diagnostics
dotnet .\src\AvaScope.Cli\bin\Debug\net10.0\avascope.dll diagnostics --session session-id --max-sessions 10
dotnet .\src\AvaScope.Cli\bin\Debug\net10.0\avascope.dll diagnostics --manifest C:\Temp\AvaScope\sessions\session-id.json
```

`doctor` reports CLI/MCP/PreviewHost co-location, bridge manifest discovery, preview-session store state, preview host readiness, and actionable issues without building or loading user projects. It exits non-zero when required co-located AvaScope assemblies or diagnostic records need attention. `diagnostics` distinguishes active, stale, invalid, unauthorized, unavailable, and incompatible local bridge records, includes health-check request ids, reports duplicate manifest records, and preserves protocol mismatch details.

Compare screenshots with an explicit diff artifact:

```powershell
dotnet .\src\AvaScope.Cli\bin\Debug\net10.0\avascope.dll diff --baseline .\baseline.png --current .\preview.png --out .\preview-diff.png --tolerance 2
```

The command returns a structured `ToolResult<PreviewDiffResponse>`. A changed image exits non-zero while still returning the changed pixel count, changed percentage, max channel delta, and diff path.

Check a focused screenshot region without mutating baselines:

```powershell
dotnet .\src\AvaScope.Cli\bin\Debug\net10.0\avascope.dll assert-region --image .\screenshot.png --assert non_empty --x 20 --y 40 --width 200 --height 80 --crop-out .\region.png
dotnet .\src\AvaScope.Cli\bin\Debug\net10.0\avascope.dll assert-region --image .\current.png --baseline .\baseline.png --assert changed --x 0 --y 0 --width 300 --height 160 --min-changed-pixels 5 --tolerance 2
```

Supported assertions are `non_empty`, `mostly_blank`, `changed`, and `unchanged`. The command returns `ToolResult<ScreenshotRegionAssertionResponse>` with bounded pixel metrics and optional crop artifacts.

Create and check a visual regression baseline set:

```powershell
dotnet .\src\AvaScope.Cli\bin\Debug\net10.0\avascope.dll baseline-create path\to\App.csproj --view Views\MainView.axaml --manifest .\baselines\main.json --sizes 1440x900,1280x720 --out-dir .\baselines\main-images --theme light
dotnet .\src\AvaScope.Cli\bin\Debug\net10.0\avascope.dll baseline-check --manifest .\baselines\main.json --out-dir .\artifacts\visual-current --diff-dir .\artifacts\visual-diff --report .\artifacts\visual-report.json --report-pack .\artifacts\visual-report-pack --tolerance 2
```

`baseline-create` writes explicit baseline screenshots plus a JSON manifest. `baseline-check` re-renders the manifest variants, writes current and diff images to explicit output directories, can write a stable JSON report with `--report`, can write an agent evidence pack with `--report-pack <dir>`, returns bounded `agentReview` triage metadata, and exits non-zero when any variant changes. It does not update or replace baseline files.

`--report-pack` writes bounded review assets for agent handoff:

- `baseline-report.json`: machine-readable pack summary, baseline check result, grouped failures, and image paths.
- `baseline-report.html`: local review page with grouped failures, environment metadata, baseline/current/diff image links, and suite/mutation provenance.
- `baseline-junit.xml`: CI-friendly pass/fail summary.
- `baseline.sarif.json`: SARIF-style failure summary for code-scanning or PR review surfaces.

The CLI/MCP response returns `agentReview` for first-pass triage, then `reportPack` for status, counts, metadata, and asset paths. It does not inline large images or unbounded report payloads.

For agent repeatability across multiple views, sizes, themes, cultures, animation frames, and later runtime handoff, put the collection in a named suite manifest:

```json
{
  "version": 1,
  "name": "agent-main-suite",
  "defaults": {
    "sizes": [{ "width": 1440, "height": 900 }, { "width": 1280, "height": 720 }],
    "dpis": [96],
    "themes": ["light", "dark"],
    "cultures": ["en-US"],
    "animationFramesMs": [0],
    "mutationPresetIds": ["wide-layout"],
    "comparisonRules": {
      "tolerance": 2,
      "maxChangedPixels": 25,
      "ignoredRegions": [{ "x": 0, "y": 0, "width": 120, "height": 32, "name": "clock" }],
      "requiredRegions": [
        {
          "region": { "x": 24, "y": 80, "width": 320, "height": 160, "name": "hero" },
          "assertion": "unchanged"
        }
      ]
    }
  },
  "mutationPresets": [
    {
      "id": "wide-layout",
      "description": "Metadata handoff for a runtime width experiment.",
      "operations": [
        { "kind": "set_property", "propertyName": "Width", "value": "1440", "valueType": "double" }
      ]
    }
  ],
  "entries": [
    {
      "id": "main",
      "projectPath": "path/to/App.csproj",
      "viewPath": "Views/MainView.axaml",
      "profileName": "main"
    }
  ]
}
```

```powershell
dotnet .\src\AvaScope.Cli\bin\Debug\net10.0\avascope.dll baseline-create --suite .\baselines\agent-main-suite.json --manifest .\baselines\agent-main.json --out-dir .\baselines\agent-main-images
dotnet .\src\AvaScope.Cli\bin\Debug\net10.0\avascope.dll baseline-check --manifest .\baselines\agent-main.json --out-dir .\artifacts\visual-current --diff-dir .\artifacts\visual-diff --report .\artifacts\visual-report.json --report-pack .\artifacts\visual-report-pack --tolerance 2
```

Suite creation expands to the same baseline manifest shape that `baseline-check` already consumes. `comparisonRules` can be declared in suite defaults, entries, or explicit variants. Scalar values such as `tolerance`, `maxChangedPixels`, and `maxChangedPercent` are overridden by the more specific level, while `ignoredRegions` and `requiredRegions` are combined. If no rules are configured, baseline checks keep the existing strict behavior. In this slice, `runtimeTarget`, `profileName`, `profileVariant`, `profileFilePath`, and `mutationPresetIds` are recorded as structured provenance and agent handoff metadata; suite creation does not execute runtime mutations.

For CI artifact upload, prefer `baseline-check --report-pack <dir>` and upload that directory plus the configured current/diff directories. The older `eng\collect-baseline-artifacts.ps1` helper remains available for `--report` JSON-only workflows. See [VISUAL_REGRESSION_CI.md](VISUAL_REGRESSION_CI.md).

Delete stale AvaScope-owned preview-session metadata:

```powershell
dotnet .\src\AvaScope.Cli\bin\Debug\net10.0\avascope.dll cleanup
```

Cleanup only removes stale or invalid JSON records from the local AvaScope preview-session store. It does not terminate processes by name.

Delete stale or invalid AvaScope-owned bridge manifests:

```powershell
dotnet .\src\AvaScope.Cli\bin\Debug\net10.0\avascope.dll cleanup-bridge-sessions --manifest-dir C:\Temp\AvaScope\sessions
```

Bridge cleanup deletes only stale or invalid local manifest JSON files. It does not kill processes and leaves live but unavailable or incompatible bridge-enabled apps in place for diagnostics.

Check reload support for a runtime bridge session:

```powershell
dotnet .\src\AvaScope.Cli\bin\Debug\net10.0\avascope.dll reload --session session-id
```

Runtime bridge reload currently returns an explicit `runtime_reload_not_supported` result after verifying the local bridge session is active. CLI preview renders are one-shot and are not persisted across CLI processes.
Use `create-preview-session` when a CLI workflow needs persisted preview-session metadata and reload support across CLI processes.

After creating or extracting the local executable package, the same command shape can be run from the artifact directory:

```powershell
.\artifacts\executables\avascope-win-x64-framework-dependent\avascope.exe mcp
dotnet .\artifacts\executables\avascope-win-x64-framework-dependent\avascope.dll mcp
```

## MCP

The MCP server runs over stdio:

```powershell
dotnet .\src\AvaScope.Mcp\bin\Debug\net10.0\AvaScope.Mcp.dll
```

Implemented tools:

- `health`
- `capabilities`
- `list_sessions`
- `attach_to_app`
- `launch_app`
- `list_top_levels`
- `screenshot`
- `assert_region`
- `visual_tree`
- `logical_tree`
- `inspect_node`
- `find_nodes`
- `audit_ui`
- `input`
- `mutate_node`
- `mutate_node_evidence`
- `mutation_review`
- `close_session`
- `diagnostics`
- `preview_axaml`
- `preview_axaml_multi`
- `preview_axaml_animation`
- `cleanup`
- `cleanup_bridge_sessions`
- `create_preview_session`
- `list_preview_sessions`
- `preview_viewer`
- `close_preview_session`
- `reload`

Planned but not implemented yet: runtime hot reload, drag/drop, full preview startup orchestration, installer distribution, macOS release policy, and broader hosted review integrations.

`capabilities` returns the same discovery manifest as the CLI command and accepts optional `requiredCapabilities` as comma-separated ids. It is the compatibility gate for clients that need specific runtime, preview, diagnostics, baseline, report, artifact, or mutation surfaces before invoking newer tools.

`diagnostics` reports AvaScope service metadata, local bridge manifest/pipe health, stale, invalid, unauthorized, unavailable, duplicate, and protocol-incompatible bridge records, preview host readiness, and stale or invalid preview-session metadata without building or loading user projects. The response keeps the legacy `issues` list and also includes bounded `diagnosticIssues` entries with source, severity, status, provenance, request ids, and related path/session metadata for agent triage.

Preview readiness/build/render failures preserve the stable `error.code` and `error.message` shape and may include bounded `error.details` fields such as `phase`, `requirement`, `projectPath`, `viewPath`, `outputPath`, `exitCode`, `outputTail`, and `nextAction`. Readiness failures cover local prerequisites that can be checked before rendering, such as missing co-located PreviewHost assemblies, missing project files, missing view files, and unavailable `dotnet` process startup.

## Runtime Bridge

The bridge is opt-in. A host app must activate it explicitly and register top-levels that should be inspectable:

```csharp
using AvaScope.Bridge;

var runtime = AvaScopeBridge.Activate(new BridgeActivationOptions("My app"));
using var registration = runtime.RegisterTopLevel(window);
```

The bridge currently uses local session manifests and local named pipes. It does not expose unauthenticated remote inspection.

Runtime safety boundary:

- The bridge is inactive until the host app calls `AvaScopeBridge.Activate(...)`.
- Session manifests are local discovery metadata and include `transportScope: "local_only"`.
- Bridge IPC uses local named pipes; the server is created with current-user-only pipe access where the platform supports it.
- CLI and MCP runtime commands only attach to active local manifests and do not open network listeners.
- Runtime control remains intentionally narrow and non-destructive for public alpha.
- Runtime mutations are temporary local overrides. They are not source edits and are not persisted by AvaScope.
- Mutation review history is session-local and bounded; it is intended for current agent handoff, not durable audit storage.
- `reset_mutation`, `reset_all`, top-level unregister, `close-session`, and bridge deactivation attempt to restore active runtime mutations and clear AvaScope's active mutation registry.
- Runtime target handoff uses structured `target` context in command output; it does not add remote control or private Avalonia hooks.

Runtime input support is intentionally narrow:

- `pointer_move` raises a routed Avalonia `PointerMovedEvent` on the hit-tested input target.
- `pointer_down` and `pointer_up` raise routed Avalonia pointer press/release events on the hit-tested input target.
- `click` supports Button targets in the current MVP.
- `focus` focuses an input element by visual/logical node id or hit-tested coordinates.
- `key_down` and `key_up` raise routed Avalonia key events on a focused input element or explicit target node id.
- `key_text` writes to a focused `TextBox` or explicit `targetNodeId`, respects read-only targets, and replaces the current selection when one exists.
- `clear_text` clears a focused or targeted writable `TextBox`, resets caret/selection to 0, and rejects read-only targets.
- `select` sets `SelectedIndex` on targeted `SelectingItemsControl` instances such as `TabControl`, `ListBox`, and `ComboBox` using either an item index or exact item text.
- `scroll` adjusts a targeted `ScrollViewer` offset through public Avalonia state and reports before/after offsets.

## Preview Host

Preview rendering is isolated in `AvaScope.PreviewHost`, launched as a child process by Core, MCP, or CLI callers. The host:

- accepts a JSON `PreviewRequest`;
- optionally runs `dotnet build` for the requested `.csproj`;
- reports local readiness failures before build/render when project files, view files, host assemblies, or `dotnet` startup are missing;
- loads compiled Avalonia resource XAML through `avares://` when possible;
- loads compiled top-level `Application.Resources`, resource merged dictionaries, theme dictionaries, direct or included `Application.Styles`, `Application.DataTemplates`, and fallback `Application.DataContext` from `App.axaml`/`App.Initialize()` when present;
- falls back to standalone runtime `.axaml` loading;
- applies requested theme and culture variants inside the isolated render process;
- optionally instantiates a project-owned public parameterless design-data type and assigns it as the root control `DataContext`;
- renders through headless Skia;
- adds bounded binding/resource diagnostics, source-backed `x:DataType` binding diagnostics, and advisory layout warnings when public Avalonia APIs and source metadata expose enough signal;
- writes a PNG and structured JSON result.

Successful preview responses can include diagnostics for missing `DataContext`, unresolved resource keys, missing or invalid converter resources, conservative binding path failures, `x:DataType` binding path mismatches, missing inherited `x:DataType` on `CompiledBinding`, text clipping/truncation, clipped content, unreachable content, sibling overlap, and too-small hit targets. These diagnostics are advisory and do not fail an otherwise successful screenshot.

Animation preview responses use the same isolated PreviewHost boundary and add explicit `animationTimeOffsetMs` frame sampling. `PreviewAnimationResponse` includes per-frame render results, optional `frameStripPath`, optional file-backed `viewer.previewUrl`, and motion diagnostics derived from sampled pixels.

Preview session tools store the original preview request, latest render result, bounded session events, and lifecycle status as Core metadata. MCP-backed and CLI-created preview session records are also persisted as JSON under the local AvaScope temp preview-session store so they can be restored after the MCP server or CLI process restarts. They do not keep user project code loaded inside MCP or CLI; each render still goes through `AvaScope.PreviewHost`.

`preview_viewer` and CLI `preview-viewer` export a local file-backed HTML viewer for a preview session's latest successful render. The response includes a `previewUrl` that can be opened in the Codex in-app browser. The generated viewer embeds the screenshot and bounded session metadata, so it remains local and does not require a preview server.

`reload` re-runs stored preview-session requests through the isolated preview host and updates the existing session's latest render result. Runtime bridge session ids are health-checked locally and return `runtime_reload_not_supported`; AvaScope does not restart apps, inject code, or claim runtime hot reload. The one-shot CLI `preview` command remains one-shot; CLI preview-session commands provide the durable preview path, and `watch-preview-session` can trigger bounded reloads from file changes. Watch events that leave the watched input snapshot unchanged are reported as `skipped` instead of launching another PreviewHost child process.

`watch-preview-session` responses include a `lifecycle` object. For `v0.2.0`, `lifecycle.hostProcessMode` is `one_shot_isolated_child_process` and `persistentHostEnabled` is `false`. Persistent preview hosts remain deferred until explicit ownership, `close`, TTL, crash recovery, and cleanup semantics are designed and validated; current cleanup is limited to request temp directories and AvaScope preview-session metadata.

Current preview limitations:

- no runtime hot reload or persistent live preview host process yet; CLI watch reloads still use one-shot PreviewHost child processes;
- no project app startup/lifetime hook execution; `OnFrameworkInitializationCompleted`, project `MainWindow` creation, and app startup services are intentionally deferred;
- no JSON object injection, dependency injection, remote design data, or long-lived design-data state;
- no private Avalonia binding/style/resource hooks; diagnostics and computed provenance stay best-effort and public API based;
- build output probing assumes the default `bin\Debug\<tfm>\<ProjectName>.dll` shape.

## Safety Boundaries

- Bridge control is local-only and opt-in.
- Preview project build and view loading happen in `AvaScope.PreviewHost`, not inside the MCP server process.
- The MCP server is a thin adapter over Core.
- Tool results use structured JSON and file paths instead of unbounded payloads where practical.
- Runtime mutations remain local-only, reversible, bounded, and auditable. The current safe mutation set is intentionally limited to selected public Avalonia properties, classes, and local resource overrides.
