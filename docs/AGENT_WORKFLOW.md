# AvaScope Agent Workflow

This workflow is for agents using AvaScope as a local control plane for an Avalonia project. It uses the packaged CLI path when available because that is closest to the stable public release shape.

The intended agent loop is: check readiness, preview the UI, inspect a running app, act through bounded local commands, capture evidence, and clean up explicit local state. AvaScope returns structured JSON and file paths so an agent can make follow-up decisions without parsing screenshots or terminal text as the source of truth.

## 1. Create And Install A Local Release

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\eng\create-local-release.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File .\eng\install-avascope.ps1 -SourcePath .\artifacts\executables\avascope-win-x64-framework-dependent
```

Use the installed command when it is available:

```powershell
$avascope = "avascope"
& $avascope --version
```

If the current shell has not picked up the user `PATH` change yet, read `%LOCALAPPDATA%\AvaScope\avascope.discovery.json` and use `commandPath`, or fall back to the packaged Windows CLI printed by the release script:

```powershell
$avascope = ".\artifacts\executables\avascope-win-x64-framework-dependent\avascope.exe"
```

Agent discovery order is `PATH` command first, `%LOCALAPPDATA%\AvaScope\avascope.discovery.json` second, `%LOCALAPPDATA%\AvaScope\bin` and `%LOCALAPPDATA%\AvaScope\current` third, and repository or unpacked artifact paths last.

## 2. Run Readiness Checks

Use isolated paths when validating package health so old local sessions do not affect the result:

```powershell
& $avascope doctor --manifest-dir .\artifacts\samples\agent-workflow\sessions --preview-session-store .\artifacts\samples\agent-workflow\preview-sessions
```

Use default paths when diagnosing the user's current machine state:

```powershell
& $avascope capabilities
& $avascope --version
& $avascope doctor
& $avascope diagnostics --max-sessions 10 --mode active-only
```

`--version`, `capabilities.productVersion`, `doctor.productVersion`, and MCP `serverInfo.version` report the same product version for bug reports and artifact provenance. `capabilities` returns the current protocol/tool manifest, including runtime mutation, preview, diagnostics, baseline, report, and artifact feature ids. Treat capability descriptions as planning hints and use `capabilities[].id`, `tools[]`, and `--require` for compatibility decisions. Use `capabilities --require <id>[,<id>...]` before newer workflows when an agent needs an explicit compatibility gate; unsupported requirements return `capability_not_supported` with actionable details instead of relying on package-version guessing. `doctor` exits non-zero when co-located AvaScope assemblies are missing or stale diagnostic records need attention. `diagnostics --mode active-only` returns useful active bridge/preview sessions while summarizing stale/invalid counts in `summary` and `nextCommands`; use `--mode all` when detailed stale records are needed.

## 3. Preview A View

The getting-started sample includes `avascope.preview.json` with a `main` profile and named variants:

```powershell
& $avascope preview .\samples\AvaScope.GettingStartedApp\AvaScope.GettingStartedApp.csproj --profile main --out .\artifacts\samples\agent-workflow\main-preview.png --run-index .\artifacts\samples\agent-workflow\run-indexes --task main-preview
& $avascope preview .\samples\AvaScope.GettingStartedApp\AvaScope.GettingStartedApp.csproj --profile main --variant dark
& $avascope latest-run --run-index .\artifacts\samples\agent-workflow\run-indexes --task main-preview
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

Variants are applied after the base profile and before explicit CLI options. Preview responses include `projectInfo` for project path, assembly name, target framework selection, build configuration, output assembly path, and App.axaml path when available. When `--run-index <dir>` is supplied, responses also include `runIndex` pointing at `run-index.json`, `run-index.html`, and `latest-run.json`; agents can call `latest-run` by task or project/view selector instead of scanning artifact directories.

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

`preview-viewer` returns a `previewUrl` pointing at a generated file-backed HTML viewer. Open that URL in the Codex in-app browser to review the rendered screenshot, preview metadata, diagnostics, and session JSON beside the thread without starting a server. Agents should read `agentReview.previewUrls` and `agentReview.reportPaths` first, then load the full session payload only when deeper context is needed.

Preview failures include bounded `error.details.phase` values. Treat `readiness` as a local prerequisite problem, `build` as user project build output, and `render` as isolated view loading or rendering failure.

`watch-preview-session` also returns `lifecycle`. In the stable v1 surface, persistent preview hosts are disabled; the lifecycle status documents one-shot child-process rendering plus the deferred close, TTL, crash, and cleanup requirements.

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
& $avascope audit-ui --session <runtime-session-id> --top-level <topLevel:id> --max-depth 8 --max-issues 100 --max-inventory 100 --run-index .\artifacts\samples\agent-workflow\run-indexes --task runtime-audit
```

Use `--manifest-dir` on follow-up runtime commands when the inspected app writes bridge manifests outside the default temp location. `attach` also accepts `--process`, `--process-name`, `--session`, and `--manifest` so agents can avoid ambiguous selection when multiple bridge-enabled apps are running.

Use the `target` object returned by `visual-tree`, `logical-tree`, `find-nodes`, `inspect-node`, `audit-ui`, `screenshot`, and `input` as the handoff source for immediate follow-up commands. It contains the current `sessionId`, `topLevelId`, `targetKind`, `capturedAt`, generation metadata, and node `treeKind`/`nodeId` when a node is involved; stale nodes return structured details and a `nextAction`. Raw `visual:*` and `logical:*` node ids are runtime-generation scoped, so multi-step requests should also carry selector fields such as `automationId`, `name`, `nodeType`, or `text` when those fields are available.

`audit-ui` builds a bounded accessibility, validation, and component inventory report from the runtime tree. It reports missing accessible names, missing stable automation ids, keyboard focus metadata, runtime validation errors, control/class/component-pattern inventory, and explicit `not_available` entries for style/resource/template/theme scopes that the runtime tree cannot prove reliably. With `--run-index`, the audit response writes a task latest pointer containing diagnostics and warnings for later agent handoff.

Use `design-audit` for task-scoped visual quality review after a UI change:

```powershell
@{
  sessionId = "<runtime-session-id>"
  topLevelId = "<topLevel:id>"
  scopeName = "ChangedSurface"
  onlyChangedNodes = $false
  excludeTypes = @("Popup")
  suppressions = @(
    @{ code = "design.surface.unintended_1px_seam"; reason = "intentional separator" }
  )
} | ConvertTo-Json -Depth 8 | Set-Content .\artifacts\samples\design-audit.json

& $avascope design-audit --request .\artifacts\samples\design-audit.json
```

The response separates active `findings` from `ignoredFindings`. Findings cover alignment, spacing, repeated heights, low-contrast indicators, unintended thin seams, radius/layering mismatch, and wrapping/density issues using runtime bounds plus available source/property metadata. Scope filters by node id, name, automation id, source path, region, or changed node/source lists; exclusions and suppressions are echoed in ignored findings instead of disappearing silently.

Runtime bridge activation is always explicit and local-only. AvaScope does not open a network listener.

## 7. Capture And Compare

```powershell
& $avascope screenshot --session <runtime-session-id> --top-level <topLevel:id> --out .\artifacts\samples\runtime-screenshot.png
& $avascope diff --baseline .\artifacts\samples\main-preview.png --current .\artifacts\samples\runtime-screenshot.png --out .\artifacts\samples\runtime-diff.png --tolerance 2
& $avascope semantic-diff --reference .\artifacts\samples\main-preview.png --current .\artifacts\samples\runtime-screenshot.png --out-dir .\artifacts\samples\semantic-diff --tolerance 2
```

Use `semantic-diff` when a user asks what looks wrong compared with a supplied reference screenshot. It keeps raw connected pixel regions separate from heuristic findings such as center mismatch, padding difference, border or seam difference, and wrapping difference. Treat finding `provenance` and `confidence` as visual evidence, not source-level certainty.

For preview-only visual regression:

```powershell
& $avascope baseline-create .\samples\AvaScope.GettingStartedApp\AvaScope.GettingStartedApp.csproj --view Views\MainView.axaml --manifest .\artifacts\samples\baselines\getting-started.json --sizes 720x420,360x240 --theme light --design-data-type AvaScope.GettingStartedApp.SamplePreviewData
& $avascope baseline-check --manifest .\artifacts\samples\baselines\getting-started.json --out-dir .\artifacts\samples\baselines\current --diff-dir .\artifacts\samples\baselines\diff --report .\artifacts\samples\baselines\report.json --report-pack .\artifacts\samples\baselines\report-pack --run-index .\artifacts\samples\agent-workflow\run-indexes --task getting-started-baseline --tolerance 0
& $avascope latest-run --run-index .\artifacts\samples\agent-workflow\run-indexes --task getting-started-baseline
```

For repeatable agent validation suites, use `baseline-create --suite <suite.json> --manifest <baseline.json>`. The suite manifest can name multiple entries, variant defaults, explicit variants, profiles, runtime target metadata, mutation preset references, animation frame offsets, and `comparisonRules`. Rules support `tolerance`, `maxChangedPixels`, `maxChangedPercent`, `ignoredRegions`, and `requiredRegions`; defaults remain strict when no rules are configured. The generated baseline manifest remains compatible with `baseline-check --manifest`; runtime target and mutation preset fields are structured handoff metadata in this slice.

For agent or CI review, prefer `--report-pack <dir>` and `--run-index <dir>`. The response includes `reportPack.status`, pass/fail counts, environment metadata, and asset paths for JSON, HTML, JUnit XML, and SARIF-style summaries. It also includes `agentReview` with a bounded failure shortlist, report paths, failed-entry current/diff artifact paths, local review URLs, and `runIndex` with the latest pointer. Upload the report-pack directory with the current/diff image directories and run-index directory; agents should inspect `agentReview` and `runIndex` first and then read the JSON/HTML paths instead of relying on terminal output.

For older JSON-only report workflows, collect the report/current/diff outputs into a single artifact directory:

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

For hover, tooltip, popup, or flyout failures, use a pointer diagnostics request instead of trying to infer everything from one screenshot:

```powershell
& $avascope pointer-diagnostics --request .\artifacts\samples\pointer-diagnostics.json
```

`pointer-diagnostics` accepts move, wait, screenshot, and assert-hit steps. Results include per-step pointer coordinates, active top-level or popup-like layer, bounded visual-tree hit path, inferred enter/exit transition diagnostics with `bounds_snapshot_inference` provenance, screenshot paths, and pointer overlay PNG paths. Set `parentHoverNodeId` in the request when validating whether moving into a popup/flyout/tooltip may exit the parent hover region.

For style regressions that only appear in specific control states, run a pseudo-state matrix instead of manually driving each state:

```powershell
& $avascope pseudo-state-matrix --request .\artifacts\samples\pseudo-state-matrix.json
```

`pseudo-state-matrix` targets a runtime node by supported find filters first, or by `target`/`nodeId` for immediate follow-ups. Prefer selector-first request fields (`automationId`, `name`, `nodeType`, `text`) for repeatable multi-step workflows; if a raw generation-scoped node id disappears, diagnostics report the scope and the selector fields needed to retry. The tool captures states such as `normal`, `pointerover`, `pressed`, `disabled`, `selected`, and `selected+pointerover`, and writes one screenshot per state plus a labeled contact sheet. Results include applied mutation ids, reset mutation responses, pointer input evidence, per-state diagnostics, and explicit `unsupported` entries when a state cannot be safely forced on the selected control.

For animation bugs that only happen after real input, record frames after the scripted interaction instead of relying on a static preview time offset:

```powershell
& $avascope record-interaction-animation --request .\artifacts\samples\interaction-animation.json
```

`record-interaction-animation` runs input or wait steps, captures requested frame offsets after each selected step, writes per-frame screenshots, geometry overlays, and a labeled frame strip, and evaluates geometry assertions such as stable width, fixed x/y alignment, final stability, range checks, or not-clipped checks. Each frame and assertion sample includes the triggering `stepId` and `offsetMs`.

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
& $avascope mutation-review --session <runtime-session-id> --max-results 20 --out .\artifacts\samples\mutation-evidence\runtime-review.html --source-project path\to\App.csproj --source-view Views\MainView.axaml --source-app App.axaml --source-profile avascope.preview.json
```

Applied mutation responses include mutation ids, original/effective metadata, diagnostics, active mutation count, explicit reset metadata, and `agentReview.mutations` for quick handoff. Evidence responses add before/after screenshots, before/after visual-tree JSON snapshots, optional diff PNGs, changed-pixel metrics, target summaries, and a local HTML review artifact so an agent can explain what changed without relying on terminal text or manual screenshot reading. The evidence HTML lets an agent click before/after screenshots to select the nearest bounded visual-tree node and inspect available source/property/binding provenance from the captured snapshots. `mutation-review` returns a bounded session-local history, active override list, reset handoff, optional HTML artifact, `sourceSuggestions` for conservative source-level handoff, and `agentReview.reviewUrls` for local review.

`sourceSuggestions` are advisory. They use runtime mutation metadata plus optional source context to suggest likely XAML, style, class, or resource follow-up locations with confidence and limitations. AvaScope does not automatically edit source files from runtime mutations.

Runtime mutations are temporary local overrides. Prefer `reset_mutation` or `reset_all` when keeping a session open; `close-session`, bridge deactivation, and top-level unregister also clear AvaScope's active mutation registry and attempt to restore active overrides.

## 9. Close And Clean Up

```powershell
& $avascope close-session --session <runtime-session-id>
& $avascope cleanup
& $avascope cleanup-bridge-sessions --manifest-dir <manifest-dir>
```

`cleanup` removes stale or invalid AvaScope-owned preview-session metadata. `cleanup-bridge-sessions` removes stale or invalid local bridge manifest JSON files. Neither command terminates processes by name.
