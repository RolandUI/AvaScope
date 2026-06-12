# AvaScope Agent Workflow

This workflow is for agents using AvaScope as a local control plane for an Avalonia project. It uses the packaged CLI path when available because that is closest to public-alpha usage.

The intended agent loop is: check readiness, preview the UI, inspect a running app, act through bounded local commands, capture evidence, and clean up explicit local state. AvaScope returns structured JSON and file paths so an agent can make follow-up decisions without parsing screenshots or terminal text as the source of truth.

## 1. Create A Local Release

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\eng\create-local-release.ps1
```

Use the packaged Windows CLI printed by the script:

```powershell
$avascope = ".\artifacts\executables\avascope-win-x64-framework-dependent\avascope.exe"
```

## 2. Run Readiness Checks

Use isolated paths when validating package health so old local sessions do not affect the result:

```powershell
& $avascope doctor --manifest-dir .\artifacts\samples\agent-workflow\sessions --preview-session-store .\artifacts\samples\agent-workflow\preview-sessions
```

Use default paths when diagnosing the user's current machine state:

```powershell
& $avascope doctor
& $avascope diagnostics --max-sessions 10
```

`doctor` exits non-zero when co-located AvaScope assemblies are missing or stale diagnostic records need attention. `diagnostics` returns the lower-level bridge and preview-host records plus bounded `diagnosticIssues` with source, severity, status, and provenance.

## 3. Preview A View

The getting-started sample includes `avascope.preview.json` with a `main` profile and named variants:

```powershell
& $avascope preview .\samples\AvaScope.GettingStartedApp\AvaScope.GettingStartedApp.csproj --profile main --out .\artifacts\samples\agent-workflow\main-preview.png
& $avascope preview .\samples\AvaScope.GettingStartedApp\AvaScope.GettingStartedApp.csproj --profile main --variant dark
```

For another app, either pass explicit options:

```powershell
& $avascope preview path\to\App.csproj --view Views\MainView.axaml --out .\artifacts\samples\app-preview.png --width 1440 --height 900 --theme light
```

or add `avascope.preview.json` beside the project:

```json
{
  "profiles": {
    "main": {
      "view": "Views/MainView.axaml",
      "out": "../../artifacts/samples/main-preview.png",
      "width": 1440,
      "height": 900,
      "theme": "light",
      "designDataType": "MyApp.Design.PreviewData",
      "variants": {
        "dark": {
          "theme": "dark",
          "out": "../../artifacts/samples/main-preview-dark.png"
        }
      }
    }
  }
}
```

Variants are applied after the base profile and before explicit CLI options. Preview responses include `projectInfo` for project path, assembly name, target framework selection, build configuration, output assembly path, and App.axaml path when available.

## 4. Sample An Animation

The getting-started sample also includes an `animation` profile:

```powershell
& $avascope preview-animation .\samples\AvaScope.GettingStartedApp\AvaScope.GettingStartedApp.csproj --profile animation
```

The command returns `ToolResult<PreviewAnimationResponse>` with per-offset frame paths, an optional frame strip, motion diagnostics, and an optional `viewer.previewUrl`. Open the returned `file://` URL in the Codex in-app browser to review the sampled timeline without starting a server.

For another app, pass explicit offsets and viewer paths:

```powershell
& $avascope preview-animation path\to\App.csproj --view Views\AnimatedView.axaml --out .\artifacts\samples\animation.png --time-offsets 0,150,900,900 --width 720 --height 420 --theme light --frame-strip .\artifacts\samples\animation-strip.png --viewer .\artifacts\samples\animation.html
```

Animation sampling advances Avalonia headless render timer ticks inside isolated PreviewHost child processes. Repeated offsets inside one request reuse the first successful frame for that offset so duplicate final artifacts are stable. It reports pixel deltas from sampled frames and uses `not_available` provenance where reliable public animation metadata is unavailable.

## 5. Use Durable Preview Sessions

```powershell
& $avascope create-preview-session .\samples\AvaScope.GettingStartedApp\AvaScope.GettingStartedApp.csproj --profile main
& $avascope list-preview-sessions
& $avascope reload-preview-session --session <preview-session-id>
& $avascope preview-viewer --session <preview-session-id> --out .\artifacts\samples\main-preview-viewer.html
& $avascope watch-preview-session --session <preview-session-id> --timeout-ms 30000 --settle-ms 250 --max-reloads 1
& $avascope close-preview-session --session <preview-session-id>
```

Preview sessions persist request metadata only. Each render still runs through an isolated `AvaScope.PreviewHost` child process. Duplicate watcher bursts that leave the watched input snapshot unchanged are reported as `skipped` instead of launching another host process.

`preview-viewer` returns a `previewUrl` pointing at a generated file-backed HTML viewer. Open that URL in the Codex in-app browser to review the rendered screenshot, preview metadata, diagnostics, and session JSON beside the thread without starting a server.

Preview failures include bounded `error.details.phase` values. Treat `readiness` as a local prerequisite problem, `build` as user project build output, and `render` as isolated view loading or rendering failure.

`watch-preview-session` also returns `lifecycle`. In `v0.2.0`, persistent preview hosts are disabled; the lifecycle status documents one-shot child-process rendering plus the deferred close, TTL, crash, and cleanup requirements.

## 6. Inspect A Running App

Start the sample with the opt-in bridge:

```powershell
$env:AVASCOPE_SAMPLE_BRIDGE = "1"
dotnet run --project .\samples\AvaScope.GettingStartedApp\AvaScope.GettingStartedApp.csproj
```

In another terminal:

```powershell
& $avascope diagnostics --max-sessions 10
& $avascope attach --session <runtime-session-id>
& $avascope attach --process-name AvaScope.GettingStartedApp
& $avascope list-top-levels --session <runtime-session-id> --manifest-dir <manifest-dir>
& $avascope visual-tree --session <runtime-session-id> --top-level <topLevel:id> --max-depth 4
& $avascope find-nodes --session <runtime-session-id> --top-level <topLevel:id> --type TextBlock --max-depth 6
& $avascope inspect-node --session <runtime-session-id> --top-level <topLevel:id> --node <node-id>
```

Use `--manifest-dir` on follow-up runtime commands when the inspected app writes bridge manifests outside the default temp location. `attach` also accepts `--process`, `--process-name`, `--session`, and `--manifest` so agents can avoid ambiguous selection when multiple bridge-enabled apps are running.

Use the `target` object returned by `visual-tree`, `logical-tree`, `find-nodes`, `inspect-node`, `screenshot`, and `input` as the handoff source for follow-up commands. It contains the current `sessionId`, `topLevelId`, `targetKind`, `capturedAt`, generation metadata, and node `treeKind`/`nodeId` when a node is involved; stale nodes return structured details and a `nextAction`.

Runtime bridge activation is always explicit and local-only. AvaScope does not open a network listener.

## 7. Capture And Compare

```powershell
& $avascope screenshot --session <runtime-session-id> --top-level <topLevel:id> --out .\artifacts\samples\runtime-screenshot.png
& $avascope diff --baseline .\artifacts\samples\main-preview.png --current .\artifacts\samples\runtime-screenshot.png --out .\artifacts\samples\runtime-diff.png --tolerance 2
```

For preview-only visual regression:

```powershell
& $avascope baseline-create .\samples\AvaScope.GettingStartedApp\AvaScope.GettingStartedApp.csproj --view Views\MainView.axaml --manifest .\artifacts\samples\baselines\getting-started.json --sizes 720x420,360x240 --theme light --design-data-type AvaScope.GettingStartedApp.SamplePreviewData
& $avascope baseline-check --manifest .\artifacts\samples\baselines\getting-started.json --out-dir .\artifacts\samples\baselines\current --diff-dir .\artifacts\samples\baselines\diff --report .\artifacts\samples\baselines\report.json --tolerance 0
```

For CI upload, collect the report/current/diff outputs into a single artifact directory:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\eng\collect-baseline-artifacts.ps1 -Report .\artifacts\samples\baselines\report.json -OutDir .\artifacts\samples\baselines\upload
```

## 8. Send Narrow Runtime Input

```powershell
& $avascope input --session <runtime-session-id> --top-level <topLevel:id> --action focus --target-node <node-id>
& $avascope input --session <runtime-session-id> --top-level <topLevel:id> --action key_text --target-node <textBox-node-id> --text "hello"
& $avascope input --session <runtime-session-id> --top-level <topLevel:id> --action clear_text --target-node <textBox-node-id>
& $avascope input --session <runtime-session-id> --top-level <topLevel:id> --action click --x 120 --y 40
```

Runtime input is intentionally narrow and non-destructive. Unsupported actions return structured errors.

Runtime mutations use the same local bridge boundary and are reversible UI experiments, not implicit source edits:

```powershell
& $avascope mutate-node --session <runtime-session-id> --top-level <topLevel:id> --node <node-id> --operation set_property --property Width --value 240 --value-type double
& $avascope mutate-node --session <runtime-session-id> --top-level <topLevel:id> --node <node-id> --operation add_class --class agent-selected
& $avascope mutate-node --session <runtime-session-id> --top-level <topLevel:id> --node <node-id> --operation set_resource --resource-key AccentBrush --value "#0066ff" --value-type brush
& $avascope mutate-node --session <runtime-session-id> --top-level <topLevel:id> --node <node-id> --operation reset_mutation --mutation-id <mutation-id>
& $avascope mutate-node --session <runtime-session-id> --top-level <topLevel:id> --node <node-id> --operation reset_all
```

For agent review, prefer the evidence wrapper when the result should be auditable:

```powershell
& $avascope mutate-node-evidence --session <runtime-session-id> --top-level <topLevel:id> --node <node-id> --operation set_property --property Background --value "#0066ff" --value-type brush --out-dir .\artifacts\samples\mutation-evidence --request-id runtime-background-check
```

Applied mutation responses include mutation ids, original/effective metadata, diagnostics, and explicit reset metadata. Evidence responses add before/after screenshots, before/after visual-tree JSON snapshots, optional diff PNGs, changed-pixel metrics, and target summaries so an agent can explain what changed without relying on terminal text or manual screenshot reading. Mutation history review artifacts remain a later `v0.7.0` slice.

Runtime mutations are temporary local overrides. Prefer `reset_mutation` or `reset_all` when keeping a session open; `close-session`, bridge deactivation, and top-level unregister also clear AvaScope's active mutation registry and attempt to restore active overrides.

## 9. Close And Clean Up

```powershell
& $avascope close-session --session <runtime-session-id>
& $avascope cleanup
& $avascope cleanup-bridge-sessions --manifest-dir <manifest-dir>
```

`cleanup` removes stale or invalid AvaScope-owned preview-session metadata. `cleanup-bridge-sessions` removes stale or invalid local bridge manifest JSON files. Neither command terminates processes by name.
