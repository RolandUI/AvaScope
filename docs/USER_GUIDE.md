# AvaScope User Guide

AvaScope provides local CLI/MCP inspection, preview, bounded runtime control and evidence for Avalonia 12 on `net10.0`. Start with the [agent workflow](AGENT_WORKFLOW.md); use [stable surface](STABLE_SURFACE.md) for the command/tool catalog and compatibility, [upgrade guidance](UPGRADE.md) for version alignment, and [troubleshooting](TROUBLESHOOTING.md) for failures. Contributor build, validation and release procedures belong in [CONTRIBUTING.md](../CONTRIBUTING.md) and [VALIDATION.md](VALIDATION.md).

## Install From Release Artifacts

Use the [release installation instructions](../README.md#install-from-a-release) for Windows, Linux and macOS, including runtime requirements, checksums and unsigned-installer handling. Portable ZIPs keep CLI, MCP and PreviewHost together. On macOS, run `bash prepare-macos.sh` after extracting a ZIP to restore apphost execute permissions.

Installers support per-user repair/upgrade and write `avascope.discovery.json` with `schemaVersion`, `version`, `installRoot`, `commandPath`, `executablePath`, `uninstallPath`, `pathEntryManaged` and an `mcp` stdio entry (`serverName`, `commandPath`, `arguments`). Read returned paths rather than constructing them.

Agent discovery order:

1. Use `avascope` from `PATH`.
2. Read the platform's `avascope.discovery.json`: Windows `%LOCALAPPDATA%\AvaScope`; Linux `$XDG_DATA_HOME/avascope` or `~/.local/share/avascope`; macOS `~/Library/Application Support/AvaScope`.
3. Use its `commandPath` or the documented shim: Windows `%LOCALAPPDATA%\AvaScope\bin\avascope.cmd`; Linux/macOS `~/.local/bin/avascope`.
4. Fall back to the selected unpacked release or source build.

Windows uninstall is available through Settings > Apps. Use the discovered `uninstallPath` for other installs. The repository's `eng/install-avascope.ps1` supports Windows development installs from an unpacked directory or ZIP.

## Version Discovery

```powershell
avascope --version
avascope capabilities
```

The product version is also exposed by `health`/`doctor` service metadata, `capabilities.productVersion`, `doctor.productVersion` and MCP `serverInfo.version`. Gate optional behavior by capability IDs; see [upgrade and compatibility](UPGRADE.md).

## Getting Started Sample

The repository includes a tiny Avalonia 12 sample app at `samples\AvaScope.GettingStartedApp`.

Use the installed CLI with the repository sample (or build from source as described in [CONTRIBUTING.md](../CONTRIBUTING.md)):

```powershell
avascope preview .\samples\AvaScope.GettingStartedApp\AvaScope.GettingStartedApp.csproj --view Views\MainView.axaml --out .\artifacts\samples\getting-started-preview.png --width 720 --height 420 --theme light --design-data-type AvaScope.GettingStartedApp.SamplePreviewData
```

Run the sample with the opt-in local bridge enabled:

```powershell
$env:AVASCOPE_SAMPLE_BRIDGE = "1"
dotnet run --project .\samples\AvaScope.GettingStartedApp\AvaScope.GettingStartedApp.csproj
```

In another terminal, inspect local bridge sessions and use the reported session id with runtime commands:

```powershell
avascope diagnostics --max-sessions 10
avascope list-top-levels --session <session-id>
```

The bridge is not enabled unless `AVASCOPE_SAMPLE_BRIDGE` is set to `1` or `true`.

## CLI

Examples use `avascope` from the selected install. For source builds, substitute `dotnet <build-output>/avascope.dll`.

### Preview And Profiles

```powershell
avascope preview path\to\App.csproj --view Views\MainView.axaml --out .\preview.png --width 1440 --height 900 --dpi 96 --theme light --culture ja-JP --design-data-type MyApp.Design.PreviewData --state-variant loading
```

Query supported features before relying on a workflow:

```powershell
avascope capabilities
avascope capabilities --require runtime.ui_audit,reports.evidence_pack
```

`capabilities` writes `ToolResult<AvaScopeCapabilitiesResponse>` with `productVersion`, `capabilities[]`, `tools[]`, `runtimeMutationCapabilities[]`, and `compatibilityPolicy`. The capability descriptions are agent-facing discovery text: clients should use them for planning, but gate behavior by `capabilities[].id`, `tools[]`, and `--require` instead of prose parsing. Unsupported required feature ids fail with `capability_not_supported`, `unsupportedCapabilities`, and a `nextAction` detail so clients can branch by feature id rather than guessing from package versions.

`preview` writes a structured JSON `ToolResult<PreviewResponse>` to stdout. On success, `value.filePath` points to the generated PNG.
`--width` and `--height` can be omitted when the root AXAML declares design-time dimensions with `d:DesignWidth`/`d:DesignHeight` or `Design.Width`/`Design.Height`. Project previews also apply root design-time data from `Design.DataContext` or `d:DataContext="{x:Static ...}"`; an explicit `--design-data-type` still takes precedence.
`--state-variant` selects an explicit design-data state such as `empty`, `loading`, `error`, `long-text`, `many-rows`, `validation-errors`, or `narrow`. PreviewHost applies it by using a public `ForState(string)`, `Create(string)`, string constructor, `StateVariant` property, or `ApplyState(string)` member on the configured design-data type. AvaScope does not invent arbitrary ViewModel states; the project or preview profile supplies the variants. The response echoes `stateVariant` and includes `state_variant_applied` or `state_variant_not_applied` diagnostics.

Use `--run-index <dir>` on `preview` when an agent needs a durable per-run artifact index:

```powershell
avascope preview .\samples\AvaScope.GettingStartedApp\AvaScope.GettingStartedApp.csproj --profile main --out .\artifacts\samples\getting-started-preview.png --run-index .\artifacts\samples\run-indexes --task getting-started-main
avascope latest-run --run-index .\artifacts\samples\run-indexes --task getting-started-main
```

The preview response includes `runIndex` with `run-index.json`, `run-index.html`, `latest-run.json`, screenshot paths, diagnostics, warnings, and generated report paths. `latest-run` resolves the pointer without scanning artifact directories manually. If `--task` is omitted, AvaScope groups the latest pointer by project, view, profile, variant, and state variant.
Project preview builds use an AvaScope-owned isolated output root by default, so a running app that locks its normal `bin` output does not block one-shot previews. Use `--build-output-root <dir>` to choose the isolated build root explicitly. Use `--assembly-path <dll>` to load an already-built project assembly without building, or `--no-build true` to skip the build and probe the known output path. Build failures keep stdout/stderr bounded in the JSON response and write the full process output to `error.details.buildLogPath` when possible.

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
      "stateVariant": "empty",
      "displayName": "Main preview",
      "variants": {
        "dark": {
          "theme": "dark",
          "out": "../../artifacts/samples/main-preview-dark.png"
        },
        "loading": {
          "stateVariant": "loading",
          "out": "../../artifacts/samples/main-preview-loading.png"
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
avascope preview path\to\App.csproj --profile main
avascope preview path\to\App.csproj --profile main --variant dark
avascope create-preview-session path\to\App.csproj --profile main
```

Named variants are applied after the base profile and before explicit CLI options, so `--height 600` still overrides a variant height. Profiles and variants can include `stateVariant`, `buildOutputRoot`, `assemblyPath`, and `noBuild` for repeatable state injection and build isolation. Profile `out`, `contactSheet`, `frameStripPath`, `viewerPath`, `buildOutputRoot`, and `assemblyPath` paths are resolved relative to the profile file; `--profile-file <path>` can point to a non-default profile file.

Render multiple viewport sizes from one preview request:

```powershell
avascope preview path\to\App.csproj --view Views\MainView.axaml --out .\preview.png --sizes 1440x900,1280x720,900x700 --theme light --contact-sheet .\preview-contact-sheet.png
```

The command writes a structured JSON `ToolResult<PreviewBatchResponse>`. Each `entries[]` item has a deterministic per-size output path and an independent `ToolResult<PreviewResponse>`, so one failed size does not discard successful screenshots. If every requested size fails, the top-level error reports the first underlying build/render root cause, a bounded per-viewport failure summary, and the first `buildLogPath` when the failure came from project build output.

### Preview Animation

Render animation samples for requested time offsets:

```powershell
avascope preview-animation path\to\App.csproj --view Views\AnimatedView.axaml --out .\animation.png --time-offsets 0,150,900,900 --width 720 --height 420 --theme light --frame-strip .\animation-strip.png --viewer .\animation.html
```

The command writes per-offset PNG frames, an optional frame strip, and a structured `ToolResult<PreviewAnimationResponse>`. When `--viewer` is supplied, the response includes `viewer.previewUrl`, a `file://` URL for a self-contained HTML timeline viewer that embeds the sampled frames, motion summary, diagnostics, and JSON response.

Animation playback runs in one isolated PreviewHost instance. The real Avalonia dispatcher/render loop advances between absolute elapsed-time deadlines; loading, capture and PNG writing are not added as new delays before each frame. This is measured real time, not a virtual clock or seek API. `--timing-tolerance-ms` (default 100, range 0..5000) sets the allowed interval around each requested offset. Every frame's `animationTiming` contains `requestedOffsetMs`, `earliestElapsedMs`, `latestElapsedMs`, `toleranceMs`, `origin` and `withinTolerance`, even when diagnostic filters hide warnings. The complete response's `timingStatus` is `within_tolerance` only when every frame's capture interval fits; otherwise it is `inconclusive`. A successful tool call means recording completed, not that an animation assertion passed.

By default, the origin is `window_attach_show`, measured just before attachment/show. Constructor work or arbitrary async application work may have started earlier. For an explicit trigger, pass `--trigger-class play --trigger-target-name Mover`: AvaScope warms the view, locates exactly one named control, then adds the previously absent class once and starts the measurement (`class_added`). Without a target name the content root is used. An already active class or missing/ambiguous target is rejected. For example, a style selected by `Border.play` can animate a named Border from width 20 to 120 over 800ms; sample `0,400,1000,1200` to inspect the beginning, middle, end and a later stability sample. MCP exposes the same `triggerClass`, `triggerTargetName` and `timingToleranceMs` arguments.

Repeated offsets reuse the same observation and timing; cached duplicates are excluded from motion/stability analysis. Pixel deltas describe rendered observations, not compositor/native presentation or what happened between samples. Late observations cannot establish a timing assertion. Moving-property metadata remains `not_available` where public Avalonia APIs do not expose it. Exact-offset animation baseline creation/checking is refused because old manifests contain no measured timing; use this recording or runtime assertions instead. Ordinary static previews/baselines are unchanged.

### Preview Sessions

Create and manage durable preview sessions:

```powershell
avascope create-preview-session path\to\App.csproj --view Views\MainView.axaml --out .\preview.png --width 1440 --height 900 --theme light --display-name "Main preview"
avascope list-preview-sessions
avascope reload-preview-session --session <preview-session-id>
avascope preview-viewer --session <preview-session-id> --out .\preview-viewer.html
avascope close-preview-session --session <preview-session-id>
```

Preview-session CLI commands persist metadata in the same local AvaScope preview-session store used by MCP. They store the original request and latest render result, then re-render through `AvaScope.PreviewHost` child processes on reload.

`preview-viewer` writes a self-contained HTML viewer for the latest successful render and returns a structured `ToolResult<PreviewViewerResponse>` with `viewerPath`, `previewUrl`, and `agentReview.previewUrls`. Open the `file://` `previewUrl` in the Codex in-app browser to review the screenshot, metadata, diagnostics, and session JSON beside the thread. The viewer does not start a network listener.

Watch a preview session and reload when project or view files change:

```powershell
avascope watch-preview-session --session <preview-session-id> --timeout-ms 30000 --settle-ms 250 --max-reloads 1
avascope watch-preview-session --session <preview-session-id> --timeout-ms 30000 --watch Views\MainView.axaml
```

Watch mode is bounded by `--timeout-ms`. It emits a structured `ToolResult<PreviewWatchResponse>` with changed/reloaded events and does not keep user project code loaded in the CLI process.

### Runtime Inspection

Attach to an active local bridge session:

```powershell
avascope attach --process 1234
avascope attach --process-name MyApp
avascope attach --latest true --process-name MyApp
avascope attach --session session-id
avascope attach --manifest .\artifacts\sessions\session-id.json
```

Use `--manifest-dir <dir>` on runtime CLI commands when the inspected app writes bridge manifests to a selected local directory. AvaScope never silently picks between multiple matching live manifests; retry with `--session`, `--process`, `--process-name`, or `--manifest` when attach is ambiguous. `--latest true` selects the newest active matching manifest while excluding stale process records and still fails if multiple candidates are equivalently latest.

Launch an explicitly bridge-enabled local app and wait for its bridge session:

```powershell
avascope launch-app --command dotnet --args "run --project path\to\App.csproj" --env AVASCOPE_SAMPLE_BRIDGE=1 --manifest-dir .\artifacts\sessions --out-dir .\artifacts\launch
```

The helper sets `AVASCOPE_BRIDGE_MANIFEST_DIR` for the child process, captures stdout/stderr to deterministic files, waits for a bridge manifest from the launched process, and returns session, top-level when available, process, manifest, stdout, and stderr details. It does not inject into apps; the app must explicitly enable `AvaScopeBridge.Activate`.

Keep activation in development-only host code and behind an explicit local feature flag. A typical application wraps the `AvaScopeBridge.Activate(...)` call in `#if DEBUG` and then checks an app-specific environment/configuration switch; production builds should contain no unconditional activation path. The Bridge itself exposes only the local manifest and named-pipe transport and provides no network activation endpoint.

List top-level windows/views and capture a runtime screenshot from an active bridge session:

```powershell
avascope list-top-levels --session session-id --manifest-dir .\artifacts\sessions
avascope screenshot --session session-id --top-level topLevel:1234 --out screenshot.png --manifest-dir .\artifacts\sessions
```

Read bounded runtime trees from an active bridge session:

```powershell
avascope visual-tree --session session-id --top-level topLevel:1234 --max-depth 4
avascope logical-tree --session session-id --top-level topLevel:1234 --max-depth 4
```

Runtime tree, search, inspect, input, and screenshot responses include a `target` object with the current `sessionId`, `topLevelId`, `targetKind`, `capturedAt`, top-level generation metadata, and, when applicable, `treeKind`, `nodeId`, and node generation metadata. Tree, search, and inspect nodes also expose `interactionState`: effective visibility, enabled state, finite unclipped rendering, semantic actionability, and the currently available built-in or registered actions. Carry a target object only into an immediate follow-up; raw `visual:*` and `logical:*` ids are generation-scoped evidence, not durable workflow identity. Persist stable selectors such as `automationId`, `name`, `nodeType`, `text`, binding, or command identity instead. Missing or stale node references return structured details with the requested target and a `nextAction`.

Inspect a current runtime tree node by its generation-scoped id:

```powershell
avascope inspect-node --session session-id --top-level topLevel:1234 --node visual:5678
avascope inspect-node --session session-id --top-level topLevel:1234 --node logical:5678 --tree-kind logical
avascope explain-layout --session session-id --top-level topLevel:1234 --node visual:5678
```

`inspect-node` includes bounded `computedProperties` for high-value visual, style, text, and layout properties, plus `sourceMap` when Avalonia XAML diagnostics or source snippets can identify file, line, column, `x:Name`, declared bindings, and style/template/resource origins. Provenance uses public Avalonia diagnostic priority where available and reports `unknown` or `not_available` instead of guessing private style/resource origins. For selected runtime nodes it can also include `layoutExplanation` for why a node is `0x0`, clipped, or constrained by parent layout, with desired size, bounds, available constraints, Grid row/column sizing, ScrollViewer viewport, clipping ancestors, and ancestor metrics; `scrollState` for `ScrollViewer` metrics; `bindingState` with `DataContext` type, binding expression/path, resolved-value status, converter/fallback/null status, compiled-binding status, and source mapping; `accessibilityState` from public automation/focus metadata; `validationState` from `DataValidationErrors`; and `debugState` fields from controls that implement the opt-in `IAvaScopeDebugStateProvider` bridge contract. Use `explain-layout` when an agent only needs the bounded measure/arrange explanation for one node.

For controls, `accessibilityState.effectiveAutomationName` is the public Avalonia
peer name, including header/label/custom-peer semantics. The existing
`automationName` retains the declared attached-property value. `automationNameStatus`
distinguishes `available`, genuinely `empty`, and `unavailable` after a peer read
failure. Audits report unavailable names separately; an internal control name
does not prove that assistive technology receives a name. Evidence policy omits
the optional status when that metadata is withheld, preserving name redaction
without inventing an observed state. Older responses without this status use
the legacy metadata fallback; a missing status does not establish an empty name. Compare the actual OS
tree separately with `audit-native-accessibility` when platform evidence matters.

Find runtime tree nodes by identity and optional interaction state. State filters accept `true` or `false` and use the same semantics as workflow selectors and MCP `find_nodes`:

```powershell
avascope find-nodes --session session-id --top-level topLevel:1234 --type TextBlock --max-depth 6
avascope find-nodes --session session-id --top-level topLevel:1234 --tree-kind logical --automation-id save-button --max-results 10
avascope find-nodes --session session-id --top-level topLevel:1234 --automation-id save-button --visible true --enabled true --rendered true --actionable true
```

### Audits

Build a bounded accessibility, validation, and component inventory report:

```powershell
avascope audit-ui --session session-id --top-level topLevel:1234 --tree-kind visual --max-depth 8 --max-issues 100 --max-inventory 100 --run-index .\artifacts\run-indexes --task runtime-audit-main
```

`audit-ui` returns `ToolResult<UiAuditResponse>` with `summary`, bounded `issues`, bounded `inventory`, and `agentReview`. It reports actionable controls missing accessible names or stable automation ids, keyboard focus metadata, runtime validation errors (including noninteractive controls), control/class/component-pattern counts, and explicit `not_available` inventory entries for style/resource/template/theme scopes that the runtime tree cannot prove reliably. When `--run-index <dir>` is supplied, the response includes `runIndex` with the audit command metadata, diagnostics, warnings, and latest pointer for the task.

Check audit coverage before interpreting a clean result. UI audit
`summary.sourceTruncated` distinguishes incomplete source evidence from bounded
issue/inventory output; tree nodes mark omitted descendants with
`childrenTruncated`. Audits can consume the bridge's full tree artifact only after
verifying its content hash, selected session/window/generation, size (16 MiB),
node count (8192) and depth (64). Missing, mismatched or over-budget evidence
remains partial. Requested depth limits are preserved. A missing design scope in
partial evidence returns `design_quality_scope_unavailable`, not proof of absence.
CLI `audit-ui` exits 1 for incomplete source coverage; its complete audit can
still report findings with exit 0. CLI `design-audit` exits 1 for active findings,
partial coverage or failure. MCP keeps available findings in successful partial
results and exposes the coverage/diagnostics. Validation findings include
noninteractive controls. Coverage applies to the captured runtime tree, not all
unrealized model items or every possible application state.

Run a task-scoped design-quality audit when a UI change needs focused visual-quality review rather than a broad accessibility inventory:

```powershell
@{
  sessionId = "session-id"
  topLevelId = "topLevel:1234"
  scopeName = "SettingsToolbar"
  maxDepth = 12
  excludeTypes = @("Popup")
  suppressions = @(
    @{ code = "design.surface.unintended_1px_seam"; reason = "intentional command separator" }
  )
} | ConvertTo-Json -Depth 8 | Set-Content .\design-audit.json

avascope design-audit --request .\design-audit.json
```

`design-audit` returns `ToolResult<DesignQualityAuditResponse>` with active `findings`, separate `ignoredFindings`, scope metadata, and `agentReview`. It checks runtime-tree bounds and source/property metadata for icon center mismatch, inconsistent spacing and repeated item heights, low-contrast indicators, unintended 1px seams, corner-radius/layering mismatch, and wrapping/density problems. Scope can target a node/name/automation id/source path/region or changed node/source filters; exclusions and suppression rules are reflected as ignored findings so agents can distinguish a clean audit from intentionally ignored noise.

### Runtime Input

Send local runtime input to the selected bridge session:

```powershell
avascope input --session session-id --top-level topLevel:1234 --action click --x 120 --y 40
avascope input --session session-id --top-level topLevel:1234 --action click --target-node visual:button
avascope input --session session-id --top-level topLevel:1234 --action focus --target-node visual:5678
avascope input --session session-id --top-level topLevel:1234 --action clear_text --target-node visual:5678
avascope input --session session-id --top-level topLevel:1234 --action key_down --key Enter --modifiers Control+Shift --target-node visual:5678
avascope input --session session-id --top-level topLevel:1234 --action invoke --target-node visual:button
avascope input --session session-id --top-level topLevel:1234 --action select --target-node visual:tabItem
avascope input --session session-id --top-level topLevel:1234 --action toggle --target-node visual:toggleButton
avascope input --session session-id --top-level topLevel:1234 --action expand --target-node visual:expander
avascope input --session session-id --top-level topLevel:1234 --action collapse --target-node visual:expander
avascope input --session session-id --top-level topLevel:1234 --action select --target-node visual:tabControl --text 1
avascope input --session session-id --top-level topLevel:1234 --action scroll --target-node visual:scrollViewer --y 120
avascope input --session session-id --top-level topLevel:1234 --action drag --target-node visual:slider --direction end --duration-ms 300
avascope input --session session-id --top-level topLevel:1234 --action swipe --target-node visual:card --direction left --distance-percent 75 --duration-ms 200
avascope input --session session-id --top-level topLevel:1234 --action drag --target-node visual:card --destination-target-node visual:column --duration-ms 350
avascope input --session session-id --top-level topLevel:1234 --action long_press --target-node visual:menuItem --duration-ms 800
```

Targeted `click` derives top-level DIP coordinates from the center of the selected node's current bounds. If both `x` and `y` are supplied they take precedence; coordinate-only clicks still require both. Stale, invisible, zero-sized, fully clipped, or non-Button targets fail before click dispatch with bounded target/hit-test diagnostics.

`invoke`, target-only `select`, `toggle`, `expand`, and `collapse` use the target control's public Avalonia automation provider. Unsupported target/pattern combinations fail without dispatch and identify the required pattern plus the target's supported semantic actions. The existing `select --text <index-or-item>` form remains available for selecting an item on a `SelectingItemsControl`.

`drag`, `swipe`, `long_press`, and `press_and_hold` always resolve the source node's current arranged bounds immediately before dispatch; callers do not persist or calculate coordinates. Drag and swipe accept exactly one of a direction (`left`, `right`, `up`, `down`, `start`, or `end`) or a destination node. `--distance-percent` applies to directional gestures, and `--duration-ms` is bounded to 50–5000 ms. Pointer-fallback directional gestures start at the safe inset of the opposite edge and measure the percentage against the target's full usable directional span, so 100% travels from one safe edge to the other. Writable range controls prefer Avalonia's public `IRangeValueProvider`; other valid targets use a bounded routed-pointer path. The fallback preserves the initially pressed input element and follows any public Avalonia pointer capture for every move and release instead of re-hit-testing descendants along the path; it clears residual capture after release and on cancellation. This allows a narrow thumb or inner template control to finish a gesture across its wider parent without remaining pressed. Results include the effective path, source/destination bounds, provider or pointer provenance, requested/effective duration, and clipping metadata. Hidden, disabled, zero-sized, clipped, obscured, missing, or stale targets fail before dispatch with structured diagnostics; cancellation releases any pressed pointer. `handled: true` confirms that the gesture was dispatched, not that application-specific completion logic ran; use a workflow `verify` postcondition when the intended state transition must be proven.

Apps can explicitly expose target-scoped actions for custom controls without private Avalonia APIs. Custom actions are disabled by default; activation must enable them and provide an exact allowlist, then the app registers each action on a visual instance:

```csharp
var runtime = AvaScopeBridge.Activate(new BridgeActivationOptions(
    "My app",
    enableCustomActions: true,
    allowedCustomActions: ["confirm", "reset"],
    allowDestructiveCustomActions: true));

runtime.RegisterCustomAction(
    customControl,
    new CustomActionRegistration(
        "confirm",
        context => CustomActionOutcome.Succeeded($"Confirmed {context.Parameters["mode"]}."),
        parameters: [new RuntimeCustomActionParameterDescriptor("mode", required: true)]));
```

Discover the current action descriptors before invoking one. Descriptors include the target, required state, current executability, parameter schema, safety classification, and unavailability reason. Results include bounded audit evidence. A destructive action runs only when activation set `allowDestructiveCustomActions: true` and the invocation sets `--allow-destructive true`; an action name cannot bypass this classification.

```powershell
avascope custom-actions --session session-id --top-level topLevel:1234 --node visual:customControl
avascope invoke-custom-action --session session-id --top-level topLevel:1234 --node visual:customControl --action confirm --parameters "mode=accept"
```

Input responses include `pointerButton` for supported pointer/click actions, `inputKey`/`keyModifiers` for routed key actions, wheel/scroll deltas for scroll actions, and bounded metadata such as the automation peer/pattern, previous/current automation state, selected index/item, or before/after scroll offsets.

### Semantic Workflows

Run semantic workflow steps by stable selectors:

```json
{
  "sessionId": "session-id",
  "topLevelId": "topLevel:1234",
  "outputDirectory": "artifacts/workflows/settings",
  "captureAfterEachStep": true,
  "steps": [
    {
      "id": "open-settings",
      "action": "click",
      "selector": { "automationId": "settings-button" }
    },
    {
      "id": "select-general",
      "action": "select",
      "selector": { "role": "TabItem", "text": "General" }
    },
    {
      "id": "type-server",
      "action": "type_text",
      "selector": { "bindingPath": "ServerUrl" },
      "text": "http://localhost:5000"
    },
    {
      "id": "assert-server",
      "action": "assert_state",
      "selector": { "bindingPath": "ServerUrl" },
      "assertProperty": "Text",
      "expected": "http://localhost:5000"
    },
    {
      "id": "final-shot",
      "action": "screenshot",
      "screenshotPath": "artifacts/workflows/settings/final.png"
    }
  ]
}
```

```powershell
avascope run-workflow --request .\workflow.json
```

Workflow selectors can target `nodeId`, `automationId`, `text`, `name`, `nodeType`, `role`, `bindingPath`, or `commandName`, and can filter `visible`, `enabled`, `rendered`, or `actionable` state. Prefer `"actionable": true` for semantic input targets. Every selector is resolved again immediately before validation or execution, so template recreation and navigation do not make a persisted runtime id authoritative. Generation context is checked atomically on the Bridge UI thread; one retry is allowed only for a stale response that proves `dispatched=false`, while any post-dispatch failure is never repeated. Ambiguous selectors fail with a bounded candidate list containing identity, state, bounds, top-level, and available actions. The semantic action set includes `invoke`, `select`, `toggle`, `expand`, `collapse`, `drag`, `swipe`, `long_press`, `press_and_hold`, `custom_actions`, and `custom_action`. A `custom_action` step supplies `customActionName` and optional `customActionParameters`; AvaScope re-resolves the selector, discovers the descriptor, and enforces its executability and safety classification before dispatch. Gesture steps use `direction`, `distancePercentage`, and `durationMs`; bounds-derived pointer fallback measures directional percentages over the full safe target span, while source-to-target gestures add a `destinationSelector` that is resolved independently and must match exactly one current visual node. Add `verify` to a side-effecting gesture step when successful dispatch must also be followed by a proven application state or command transition. Destructive-looking built-in targets and destructive registered actions are rejected unless the request declares `allowDestructive` or an `isolatedStateDirectory`.

For a multi-window workflow, declare semantic `topLevelAliases` and set `topLevelAlias` on each step. An alias selector accepts exact `title`, `kind`, and optional `isActive`; it is resolved only against `sessionId` and can optionally repeat that session id as a cross-session guard. AvaScope resolves an alias on every step and every wait poll, so a window can close and reopen with a different diagnostic runtime id. `topLevelId` may be omitted when all steps use aliases. Each result reports both `topLevelAlias` and the current `resolvedTopLevelId`; missing or ambiguous aliases include at most eight active top-level candidates and a next action. Screenshot steps and automatic `captureAfterEachStep` evidence preserve the originating step's alias.

```json
{
  "sessionId": "session-id",
  "topLevelAliases": [
    { "alias": "main", "selector": { "title": "My App" } },
    { "alias": "controls", "selector": { "title": "Controls" } }
  ],
  "steps": [
    {
      "id": "apply",
      "action": "invoke",
      "topLevelAlias": "controls",
      "selector": { "automationId": "apply-button" }
    },
    {
      "id": "wait-main",
      "action": "wait_for_state",
      "topLevelAlias": "main",
      "selector": { "automationId": "status" },
      "waitCondition": { "kind": "text", "expected": "Applied" }
    }
  ]
}
```

Compose bounded workflows with `if`, `retry_until`, `optional`, request-level `variables`, and reusable `fragments`. Both `if` and `retry_until` use the same `selector`, `topLevelAlias`, and typed `waitCondition` evaluator as runtime waits. An `if` step contains `then` and/or `else`; a `retry_until` step contains `steps`, requires `maxAttempts`, and may set `retryDelayMs`. Any side-effecting step inside a retry must have an `idempotencyKey`, so later attempts replay the recorded result instead of dispatching the input again. Set `optional: true` only on a leaf step; failure becomes an explicit `skipped` result with the original bounded diagnostics. A `use_fragment` step names a fragment and supplies its declared `arguments`. `${name}` references are resolved from request variables and fragment parameters before execution.

```json
{
  "sessionId": "session-id",
  "topLevelId": "topLevel:1234",
  "timeoutMs": 60000,
  "variables": {
    "statusId": "save-status",
    "readyText": "Saved"
  },
  "fragments": [
    {
      "name": "verify-status",
      "parameters": ["expected"],
      "steps": [
        {
          "id": "verify",
          "action": "assert_state",
          "selector": { "automationId": "${statusId}" },
          "assertProperty": "Text",
          "expected": "${expected}"
        }
      ]
    }
  ],
  "steps": [
    {
      "id": "save-until-ready",
      "action": "retry_until",
      "selector": { "automationId": "${statusId}" },
      "waitCondition": { "kind": "text", "expected": "${readyText}" },
      "maxAttempts": 5,
      "retryDelayMs": 100,
      "steps": [
        {
          "id": "save-once",
          "action": "invoke",
          "selector": { "automationId": "save-button" },
          "idempotencyKey": "save-once"
        }
      ]
    },
    {
      "id": "verify-branch",
      "action": "if",
      "selector": { "automationId": "${statusId}" },
      "waitCondition": { "kind": "text", "expected": "${readyText}" },
      "then": [
        {
          "id": "verify-fragment",
          "action": "use_fragment",
          "fragment": "verify-status",
          "arguments": { "expected": "${readyText}" }
        }
      ],
      "else": [
        {
          "id": "optional-dialog",
          "action": "wait_for_node",
          "selector": { "automationId": "optional-dialog" },
          "timeoutMs": 250,
          "optional": true
        }
      ]
    }
  ]
}
```

Compilation happens before output-directory creation or Bridge dispatch. Set `validateOnly: true` to return status `validated`, an empty runtime step list, and the fully expanded `plan`; static failures return `validation_failed` with every bounded diagnostic found. Execution results remain chronological and add `executionPath`, `parentStepId`, `attempt`, and `sourceFragment`; branch exclusions and optional failures are `skipped`, while a non-final failed retry condition is `retried`. Cycles, missing fragments/arguments/variables, invalid shapes, unbounded retries, retry side effects without idempotency, and limit violations prevent all execution. Fixed limits are: nesting `8`, expanded plan steps `256`, estimated results `512`, fragments `32`, variables `64`, fragment parameters/arguments `16`, retry attempts `10`, total retry iterations `64`, artifacts `64`, and workflow timeout `300000` ms maximum. Existing response budgeting still writes the complete oversized JSON to a hash-addressed local artifact.

### Workflow Evidence And Privacy

Side-effecting semantic actions may add `verify`. AvaScope optionally captures the selected pre-state, executes the action once, then uses the same typed selector/wait evaluator to bound the postcondition. The action step is `passed` only when the postcondition matches; timeout or unavailable state changes the step to `failed` while preserving the action result, last observation, and pre/post evidence. `verify.selector` and `verify.topLevelAlias` override the action target for observation; otherwise they inherit the action selector and alias. `captureBefore` and `captureAfter` default to `true`, while `captureScreenshots` is explicitly opt-in.

```json
{
  "sessionId": "session-id",
  "topLevelId": "topLevel:1234",
  "outputDirectory": "artifacts/workflows/save",
  "evidence": {
    "captureOnFailure": true,
    "includeScreenshot": true,
    "includeVisualTree": true,
    "includeActiveTopLevels": true,
    "includeSelectorCandidates": true,
    "exportReports": true,
    "reportDirectory": "artifacts/workflows/save/reports",
    "treeDepth": 4,
    "maxSelectorCandidates": 8,
    "policy": {
      "ownedEvidenceRoot": "artifacts/workflows",
      "redactedText": ["customer-secret"],
      "redactedAutomationIds": ["private-account-id"],
      "excludedControlAutomationIds": ["credit-card-number"],
      "screenshotMaskRegions": [
        { "x": 20, "y": 20, "width": 240, "height": 48, "name": "account-header" }
      ],
      "allowedActions": ["invoke"],
      "allowedCustomActions": [],
      "allowGestures": false,
      "allowDestructiveActions": false,
      "authorizedSessionIds": ["session-id"],
      "authorizedProcessIds": [1234],
      "retentionMaxAgeMinutes": 10080,
      "retentionMaxOwnedRuns": 20,
      "writeActionAudit": true,
      "networkUpload": false
    }
  },
  "steps": [
    {
      "id": "save",
      "action": "invoke",
      "selector": { "automationId": "save-button" },
      "verify": {
        "selector": { "automationId": "save-status" },
        "condition": { "kind": "text", "expected": "Saved" },
        "timeoutMs": 5000,
        "pollIntervalMs": 100,
        "captureScreenshots": true
      }
    }
  ]
}
```

When `evidence.captureOnFailure` is enabled, the first terminal failed action receives `failureEvidence` with stable paths for every available inspection, screenshot, bounded visual tree, selector-candidate set, active top-level list, and adjacent authored/executed workflow context. Inspection data carries visible/enabled/bounds, available actions, binding state, and validation state; `unavailableEvidence` names every diagnostic class the runtime could not provide. Artifact errors do not erase the action or verification result and produce `partial` or `unavailable` evidence status. With `exportReports`, `reportPack` references `workflow-report.json`, `workflow-report.md`, and `workflow-junit.xml`; all three use the same workflow and step PASS/FAIL state. `agentReview` provides the bounded failure shortlist and report/artifact paths. The same fields are available through `run-workflow`, `run_workflow`, `run-scenario`, and `run_scenario`.

`evidence.policy` is an explicit local opt-in privacy and action boundary. `ownedEvidenceRoot` must contain the workflow/scenario run directory as a strict child; every report, timeline, and explicit screenshot path must stay inside that run. AvaScope marks its own roots/runs and retention deletes only marked direct-child runs after rejecting reparse-point traversal. Count and age limits may be combined. Configured text and AutomationIds are replaced before inline results, failure JSON, action audit, workflow JSON, Markdown, JUnit, scenario timeline, and lifecycle log persistence. Excluded controls are redacted from structured evidence and their live visual bounds are converted to screenshot pixels and masked together with explicit regions. If masking or redaction cannot be completed, AvaScope deletes or omits the affected unredacted artifact and returns a secret-free `runtime_evidence_*` diagnostic.

Policy redaction also follows `responseBudget.artifactPath` references produced while collecting large trees or workflow responses. Referenced JSON inside the marked run is sanitized before it can be returned or persisted; a reference outside the policy-owned run fails closed instead of exposing an unredacted fallback.

When a text or control policy requires screenshot masking, an incomplete tree (depth or response-budget truncation) cannot prove that all protected pixels were found. AvaScope conservatively masks the entire image and reports `screenshotMasking: full_sensitive_mask` on screenshot steps. Complete trees retain selective control masks. Artifact ownership and redaction failures still omit or delete the affected image rather than exporting unmasked pixels.

With a policy present, the default action allowlist contains observation, validation, waits, composition, and custom-action discovery only. Input actions must be named explicitly. `drag`, `swipe`, `long_press`, and `press_and_hold` additionally require both `allowGestures` and `allowDestructiveActions`; destructive-looking built-in actions still require request isolation or `allowDestructive`. Application-defined actions require both the workflow action and exact custom action name in their respective allowlists, while destructive classifications retain the Bridge activation gate and also require the request and policy gates. Optional session/PID allowlists are checked against one live `local_only` manifest before dispatch. `networkUpload` cannot be enabled: policy metadata always reports `storage: local_filesystem`, AvaScope provenance, and `networkUpload: disabled`. When enabled, the redacted local audit is `action-audit.jsonl` under the owned run.

The repository's `AvaScope.ComplexWorkflowApp` sample is the release-shaped reference for this contract. `eng/test-complex-workflow.ps1` launches its two headless windows, resolves both by semantic aliases, uses a range-provider drag and a bounds-derived custom-control drag, follows both optional-UI branches across repeat runs, invokes `workflow.commit`, waits for asynchronous state, verifies the final state, and exercises redacted failure evidence, owned retention, and exact process cleanup. Pass `-Surface Cli` for `run-scenario` or `-Surface Mcp` with the MCP server and test-client assemblies for the same request shape over stdio.

Deterministic workflows can use `wait_for_node`, `wait_for_state`, and `wait_for_dialog`. Each wait accepts `timeoutMs` (default `5000`, maximum `60000`) and `pollIntervalMs` (default `100`, range `25`–`5000`), uses cancellation-aware bounded polling, and resolves its selector again on every poll. `wait_for_node` accepts `exists` and `disappears`; `wait_for_state` accepts `visible`, `hidden`, `enabled`, `disabled`, `checked`, `unchecked`, `selected_value`, `text`, `value`, `rendered`, `command_executable`, `binding_value`, `top_level_opened`, `top_level_closed`, and `change_from_baseline`. Comparisons are typed and support `equals`, `not_equals`, numeric ordering, and `changed`. A successful step exposes `waitObservation`; a failure distinguishes unavailable state (`semantic_workflow_wait_state_unavailable`) from a false condition that timed out (`semantic_workflow_wait_timeout`). Timeout metadata contains the last typed observation, elapsed time, bounded ambiguity candidates when present, and a next action. The compatible `assertProperty`/`expected` form remains supported.

```json
{
  "id": "wait-until-save-is-ready",
  "action": "wait_for_state",
  "selector": { "automationId": "save-button" },
  "timeoutMs": 10000,
  "pollIntervalMs": 100,
  "waitCondition": {
    "kind": "command_executable"
  }
}
```

For a binding, set `kind` to `binding_value`, identify it with `bindingPath` and optionally `propertyName`, then provide `expected` and `valueType`. For disappearance or `top_level_closed`, success deliberately has no surviving target id. Top-level waits use `topLevelId` and/or `topLevelTitle` in `waitCondition`. To wait for any inspected property to change, use `change_from_baseline` with `propertyName`; omit `baseline` to capture the first available observation or provide it explicitly for a known starting state.

Add `idempotencyKey` to a side-effecting step to prevent duplicate dispatch after a client retry. Without an evidence policy, results are persisted under the selected local session manifest directory. With a policy, the sanitized replay record is kept under the owned run directory so retention and privacy rules cover it. Records remain scoped by session and request signature and replay with `idempotencyReplay: true`. `idempotencyTtlMs` defaults to `300000` and accepts `100`–`86400000`; reusing a live key with different step content fails with `semantic_workflow_idempotency_conflict`.

Use `validate_action` with `inputAction`, or `validate_mutation` with a structured `mutation`, to run the same selector, target, provider/property, and value checks without input dispatch or runtime state changes:

```json
{
  "steps": [
    {
      "id": "wait-deploy",
      "action": "wait_for_node",
      "selector": { "automationId": "DeployTab" },
      "timeoutMs": 10000
    },
    {
      "id": "check-select",
      "action": "validate_action",
      "selector": { "automationId": "DeployTab" },
      "inputAction": "select"
    },
    {
      "id": "select-once",
      "action": "select",
      "selector": { "automationId": "DeployTab" },
      "idempotencyKey": "deploy-tab-select"
    },
    {
      "id": "check-width",
      "action": "validate_mutation",
      "selector": { "automationId": "DeployTab" },
      "mutation": {
        "kind": "set_property",
        "propertyName": "Width",
        "value": "240"
      }
    }
  ]
}
```

### Scenario Lifecycle

Use `run-scenario` when the workflow should launch or attach before running steps and produce a human-readable evidence timeline:

```json
{
  "requestId": "settings-scenario",
  "build": {
    "projectPath": "path/to/App.csproj",
    "configuration": "Debug",
    "framework": "net10.0",
    "noRestore": true,
    "timeoutMs": 120000
  },
  "launch": {
    "projectPath": "path/to/App.csproj",
    "configuration": "Debug",
    "framework": "net10.0",
    "noBuild": true,
    "argumentList": ["--automation-mode"],
    "environment": {
      "AVASCOPE_SAMPLE_BRIDGE": "1"
    },
    "timeoutMs": 15000
  },
  "outputDirectory": "artifacts/scenarios/settings",
  "captureAfterEachStep": true,
  "terminateLaunchedProcess": true,
  "timelinePath": "artifacts/scenarios/settings/timeline.md",
  "steps": [
    {
      "id": "open-settings",
      "action": "click",
      "selector": { "automationId": "settings-button" }
    },
    {
      "id": "assert-settings",
      "action": "assert_state",
      "selector": { "text": "Settings" },
      "assertProperty": "text",
      "expected": "Settings"
    }
  ]
}
```

```powershell
avascope run-scenario --request .\scenario.json
```

`run-scenario` returns `ToolResult<RuntimeScenarioResponse>` with `status`, optional `build`, launch and `attach` metadata, bridge `readiness`, all registered `topLevels`, the nested workflow result, optional `cleanup`, `failureStage`, `timelinePath`, diagnostics, and isolated-state metadata. `failureStage` distinguishes `validation`, `build`, `launch`, `bridge_readiness`, `attach`, `top_levels`, `workflow`, and `cleanup`; build and launch stdout/stderr remain in referenced local files even when a later stage fails. Build and launch environment values and tokenized arguments are never echoed in normal response metadata: only environment-variable names and argument counts are reported.

Project launch uses the conventional built `bin/<configuration>/<framework>/<project>.dll` target and launches it directly so manifest process identity can be matched exactly. Set `noBuild: false` to request an automatic build, provide an explicit top-level `build` object for structured build control, or set `noBuild: true` only when the target is already built. Command launch remains compatible through `command`, legacy `arguments`, or the safer tokenized `argumentList`. Launch scenarios isolate app data by default by setting app-data, user-profile, XDG, temp, and `AVASCOPE_SCENARIO_STATE_DIR` environment variables under an AvaScope-owned directory. Attached existing sessions cannot be retroactively isolated; destructive-looking click/select targets still fail unless the scenario launches with isolation or explicitly sets `allowDestructive`.

Set `terminateLaunchedProcess: true` when the scenario owns the app lifecycle. Cleanup closes the bridge and terminates the process tree only when the saved session, process id, and process start time still match; foreign processes, manually attached apps, and PID-reused processes are never terminated. Cancellation and readiness timeout terminate only the directly started process tree before returning their partial logs and readiness evidence. The default remains `false` for compatibility with scenarios that intentionally leave an app running.

### Pointer Diagnostics

Diagnose hover, popup, tooltip and pointer transitions:

```json
{
  "requestId": "hover-popup-diagnostics",
  "sessionId": "session-id",
  "topLevelId": "topLevel:1234",
  "outputDirectory": "artifacts/pointer/hover-popup",
  "parentHoverNodeId": "visual:hoverPanel",
  "includeAllTopLevels": true,
  "steps": [
    { "id": "move-parent", "action": "move", "x": 24, "y": 18 },
    { "id": "move-popup", "action": "move", "x": 260, "y": 32 },
    { "id": "assert-popup", "action": "assert_hit", "expectedLayerKind": "popup" },
    { "id": "capture", "action": "screenshot" }
  ]
}
```

```powershell
avascope pointer-diagnostics --request .\pointer-diagnostics.json
```

`pointer-diagnostics` returns `ToolResult<RuntimePointerDiagnosticsResponse>` with per-step primary top-level DIP coordinates, the sampled layer's local `pointer`, actual Avalonia hit path, nearest node, mismatch diagnostics, screenshot and pointer-overlay paths. Hit paths use generation/geometry-pinned `pick_node` samples in root-to-leaf order; `maxDepth` limits only the separate nearest-node tree. Nearest rectangle distance is not hit evidence. Layer metadata reports complete/partial/unavailable coverage, geometry and occlusion. Cross-window samples require desktop-origin/scale mapping; ambiguous native z-order is reported, and `assert_hit` refuses ambiguous or incomplete evidence. Use `includeAllTopLevels=false` with an explicit target root for a local hit assertion. Native desktop delivery remains unverified. Enter/exit diagnostics use `avalonia_hit_test_snapshot_inference` because they compare samples rather than observe routed events. `parentHoverNodeId` can identify possible parent-hover exits. Pointer overlay coordinates are mapped to the selected root's rendered pixels.

### Pseudo-State Matrix

Capture a selected runtime control across common pseudo-states:

```json
{
  "requestId": "button-state-matrix",
  "sessionId": "session-id",
  "topLevelId": "topLevel:1234",
  "automationId": "system-profile-quick-access",
  "name": "SystemProfileQuickAccessButton",
  "nodeType": "Button",
  "outputDirectory": "artifacts/pseudo-states/button",
  "contactSheetPath": "artifacts/pseudo-states/button/sheet.png",
  "states": ["normal", "pointerover", "pressed", "disabled", "selected", "selected+pointerover"]
}
```

```powershell
avascope pseudo-state-matrix --request .\pseudo-state-matrix.json
```

`pseudo-state-matrix` returns `ToolResult<RuntimePseudoStateMatrixResponse>` with one entry per requested state, state screenshot paths, applied/reset mutation responses, input actions used for pointer states, per-state diagnostics, diff metadata against the normal baseline when available, and a labeled contact sheet path. Runtime forcing is local and reset per state. Prefer selector fields over raw `visual:*` node ids for repeatable requests; raw node ids are generation-scoped and diagnostics report that scope or re-resolve through selector fields when possible. Unsupported states or unsupported target properties are reported in the relevant entry instead of being inferred from screenshots.

Pointer states require the requested `:pointerover` or `:pressed` class on the current target after capture. Dispatch alone cannot pass: an absent class produces a failed entry with `pseudo_state_not_observed`, while retaining its screenshot and observed classes. Pointer cleanup moves/releases outside the top-level, including when the target fills the entire window. Hover uses public synthetic enter/exit events; it does not certify OS cursor position or native desktop delivery. A state may render identically under an application's theme, so pixel differences alone are not required for state success.

When the inline visual tree is truncated, the matrix can inspect its exact target separately without expanding the tree response. A missing or stale target after capture fails that state and omits its target summary; an existing screenshot alone does not establish successful state capture. Applied mutations are still reset, and reset outcomes remain in the entry.

### Interaction Animation

Record frames after input and assert geometry across the transition:

```json
{
  "requestId": "expand-animation",
  "sessionId": "session-id",
  "topLevelId": "topLevel:1234",
  "outputDirectory": "artifacts/interactions/expand",
  "frameStripPath": "artifacts/interactions/expand/expand-frame-strip.png",
  "steps": [
    { "id": "expand", "action": "click", "x": 42, "y": 24, "frameOffsetsMs": [0, 100, 250] }
  ],
  "assertions": [
    { "assertionId": "panel-width", "targetNodeId": "visual:panel", "metric": "width", "mode": "stable", "stepId": "expand", "tolerance": 1 },
    { "assertionId": "panel-left", "targetNodeId": "visual:panel", "metric": "x", "mode": "equals", "stepId": "expand", "expectedValue": 16, "tolerance": 1 }
  ]
}
```

```powershell
avascope record-interaction-animation --request .\interaction-animation.json
```

`record-interaction-animation` returns `ToolResult<RuntimeInteractionAnimationResponse>` with one result per scripted input/wait step, per-frame screenshot paths, geometry overlay PNG paths, a labeled frame strip, and structured geometry assertion samples linked back to the triggering step and frame offset. Assertion modes include `changed`, `increasing`, `decreasing`, `stable`, `equals`, `within_range`, `final_stable`, and `not_clipped`; metrics include `x`, `y`, `width`, `height`, `left`, `top`, `right`, `bottom`, `center_x`, and `center_y`.

Recording is armed before each input dispatch and triggers that input once. Offsets use absolute deadlines, so capture/IPC time does not accumulate into additional requested delays. Each frame has a measured `timing` interval and provenance. New bridges provide monotonic application-side input/tree/capture spans (`application_input_dispatch_interval`); older bridges or truncated-tree fallbacks use a conservative client interval. The interval includes uncertainty about the trigger point inside its handler and the time between geometry inspection and screenshot. It is not a native presentation timestamp. `waitMs`, if present, is added to the requested trigger-relative offset; assertion `fromOffsetMs`/`toOffsetMs` select the original frame offsets after that wait.

Use `timingToleranceMs` separately from an assertion's numeric `tolerance`. `fromOffsetMs` and `toOffsetMs` bound which samples participate: assert `increasing` across the motion, `within_range` at the middle, `equals` in a final window, and `final_stable` across two later distinct samples. Assertions describe observed geometry only, not an arbitrary animated property or continuous stability between frames. If selected measurements miss their timing window, the assertion and recording report `inconclusive` and later scripted inputs are not dispatched. Check `value.status`, not only the outer `success` field. Missing/unreadable target geometry or transport/capture errors remain explicit failures; an empty assertion sample window is inconclusive.


### Runtime Mutations

Apply a reversible runtime mutation and capture an evidence package:

```powershell
avascope mutate-node --session session-id --top-level topLevel:1234 --node visual:5678 --operation set_property --property Width --value 240 --value-type double
avascope mutate-node --session session-id --top-level topLevel:1234 --node visual:5678 --operation reset_mutation --mutation-id mutation-id
avascope mutate-node-evidence --session session-id --top-level topLevel:1234 --node visual:5678 --operation set_property --property Background --value "#0066ff" --value-type brush --out-dir .\artifacts\mutation-evidence --request-id background-check
avascope mutation-review --session session-id --max-results 20 --out .\artifacts\mutation-evidence\review.html --source-project path\to\App.csproj --source-view Views\MainView.axaml --source-app App.axaml --source-profile avascope.preview.json
```

`mutate-node-evidence` runs a fixed local loop: before screenshot, before visual tree, mutation, after screenshot, after visual tree, optional image diff, and local HTML review artifact generation. The response is `ToolResult<RuntimeMutationEvidenceResponse>` with artifact file paths, mutation status, before/after target summaries, bounded diagnostics, changed-pixel metrics when diffing is enabled, `reviewArtifact` with a file URL for human inspection, and `agentReview` with a mutation summary plus bounded artifact/review URL handoff. The generated evidence HTML maps clicks on before/after screenshots to the nearest bounded visual-tree node and shows available source/property/binding provenance from the captured tree snapshots. Use `--diff false` to skip pixel comparison and `--tolerance <0-255>` to allow channel tolerance in the diff.

Logical targets retain their original request identity. Their before/after summaries identify the visual alias captured in each snapshot, using the bridge's node and top-level generations within the same session; node-id text is never rewritten to guess an alias. A target outside the captured depth, lacking sufficient identity or matching more than one visual node remains unavailable with `runtime_mutation_evidence_target_unavailable`, the affected `before`/`after` stage and a reason. This describes capture coverage, not proof that the control is absent from the application. Inspect the current target and increase `--max-depth` where appropriate before relying on that summary.

`mutation-review` returns `ToolResult<RuntimeMutationReviewResponse>` for one local bridge session. It includes bounded mutation history, active override summaries, reset handoff metadata for `reset_mutation` / `reset_all`, optional `sourceContext`, advisory `sourceSuggestions`, an optional HTML review artifact when `--out <review.html>` is supplied, and `agentReview` with the active mutation shortlist, source-suggestion count, and review URL.

When several mutations override the same property, class, or resource, reset the newest one first. An unsafe individual reset returns `runtime_mutation_reset_order_conflict` with a `blockingMutationId` and changes nothing. Reset that newer mutation first, or use `reset_all`, which restores active overrides in reverse order. Independent values and controls can still be reset separately.

Property reset preserves the original value source: a style, theme, inherited or default value resumes after the temporary local override is removed, so later app updates remain effective. Original local values (including null) and `SetCurrentValue` overrides are restored with their original precedence. Mutation metadata distinguishes `originalHadLocalValue` from `originalHadCurrentValueOverride`; a value supplied by a style is not a local value.

Setting a property already controlled by animation is unsupported because its effective value hides the original base source. Both validation and apply return `isAnimating: true` and advise waiting for the application to finish or stop that animation before inspecting and mutating again. If animation begins after a supported mutation, reset removes the known temporary base without replacing the ongoing animation. A prior current-value override invalidated by that active animation is not reapplied over it.

Use `--source-project`, `--source-view`, `--source-app`, and `--source-profile` when an agent wants a source-aware handoff after runtime experiments. The suggestions report likely source target kind, file status, confidence, suggested member/property/class/resource key, limitations, and manual action text. They never modify project files automatically; agents must still inspect the suggested source before making an explicit patch.

Close an active local bridge session:

```powershell
avascope close-session --session session-id
avascope close-session --session session-id --terminate-launched-process true
```

The opt-in termination flag only affects an app started by AvaScope
`launch-app`. AvaScope verifies the recorded PID and process start time before
terminating the owned process tree; otherwise it reports `not_owned`. The
default remains session-only close. Outcomes are `closed_only`, `terminated`,
`already_exited`, `not_owned`, and `termination_failed`.

### Diagnostics

Read local bridge and preview-host diagnostics:

```powershell
avascope doctor
avascope diagnostics
avascope diagnostics --session session-id --max-sessions 10
avascope diagnostics --manifest .\artifacts\sessions\session-id.json
avascope diagnostics --mode active-only
```

`doctor` reports CLI/MCP/PreviewHost co-location, bridge manifest discovery, preview-session store state, preview host readiness, and actionable issues without building or loading user projects. It exits non-zero when required co-located AvaScope assemblies or diagnostic records need attention. `diagnostics` distinguishes active, stale, invalid, unauthorized, unavailable, and incompatible local bridge records, includes health-check request ids, reports duplicate manifest records, and preserves protocol mismatch details. The response includes `summary` counts plus `nextCommands` and `componentOrigins` entries for `cli`, `mcp`, and `previewHost` assembly paths, base directories, resolved roots, source kind, and file existence. If those components resolve from different roots, diagnostics reports `diagnostics_mixed_install_roots` so repo-local and packaged tool mixes are explicit. `--mode active-only` lists only useful active bridge/preview sessions while keeping stale/invalid counts in `summary`; `--mode minimal` and `--mode json-minimal` suppress detailed session lists for concise agent triage.

### Visual Comparison

Compare screenshots with an explicit diff artifact:

```powershell
avascope diff --baseline .\baseline.png --current .\preview.png --out .\preview-diff.png --tolerance 2
```

The command returns a structured `ToolResult<PreviewDiffResponse>`. A changed image exits non-zero while still returning the changed pixel count, changed percentage, max channel delta, and diff path.

Compare a current screenshot against an arbitrary reference and ask AvaScope to produce bounded likely visual deltas:

```powershell
avascope semantic-diff --reference .\reference.png --current .\preview.png --out-dir .\artifacts\semantic-diff --tolerance 2 --max-findings 12 --max-raw-regions 8
```

`semantic-diff` returns `ToolResult<SemanticScreenshotComparisonResponse>` with the raw `PreviewDiffResponse`, separate connected raw pixel regions, heuristic semantic findings, annotated crops, and an annotated overview image. Finding kinds include `center_mismatch`, `edge_mismatch`, `padding_difference`, `border_or_seam_difference`, and `wrapping_difference`. Each semantic finding includes confidence and provenance such as `content_bounds_heuristic`, `edge_band_heuristic`, or `line_band_heuristic`; these are visual-delta hints, not proof of source-level intent.

Check a focused screenshot region without mutating baselines:

```powershell
avascope assert-region --image .\screenshot.png --assert non_empty --x 20 --y 40 --width 200 --height 80 --crop-out .\region.png
avascope assert-region --image .\current.png --baseline .\baseline.png --assert changed --x 0 --y 0 --width 300 --height 160 --min-changed-pixels 5 --tolerance 2
```

Supported assertions are `non_empty`, `mostly_blank`, `changed`, and `unchanged`. The command returns `ToolResult<ScreenshotRegionAssertionResponse>` with bounded pixel metrics and optional crop artifacts.

Create and check a visual regression baseline set:

```powershell
avascope baseline-create path\to\App.csproj --view Views\MainView.axaml --manifest .\baselines\main.json --sizes 1440x900,1280x720 --out-dir .\baselines\main-images --theme light
avascope baseline-check --manifest .\baselines\main.json --out-dir .\artifacts\visual-current --diff-dir .\artifacts\visual-diff --report .\artifacts\visual-report.json --report-pack .\artifacts\visual-report-pack --run-index .\artifacts\run-indexes --task visual-main --tolerance 2
avascope latest-run --run-index .\artifacts\run-indexes --task visual-main
```

`baseline-create` writes explicit baseline screenshots plus a JSON manifest. `baseline-check` re-renders the manifest variants, writes current and diff images to explicit output directories, can write a stable JSON report with `--report`, can write an agent evidence pack with `--report-pack <dir>`, can write a run index with `--run-index <dir>`, returns bounded `agentReview` triage metadata, and exits non-zero when any variant changes. It does not update or replace baseline files.

`--report-pack` writes bounded review assets for agent handoff:

- `baseline-report.json`: machine-readable pack summary, baseline check result, grouped failures, and image paths.
- `baseline-report.html`: local review page with grouped failures, environment metadata, baseline/current/diff image links, and suite/mutation provenance.
- `baseline-junit.xml`: CI-friendly pass/fail summary.
- `baseline.sarif.json`: SARIF-style failure summary for code-scanning review.

The CLI/MCP response returns `agentReview` for first-pass triage, then `reportPack` for status, counts, metadata, and asset paths. It does not inline large images or unbounded report payloads.

For agent repeatability across multiple views, sizes, themes, cultures, and later runtime handoff, put the collection in a named suite manifest:

```json
{
  "version": 1,
  "name": "agent-main-suite",
  "defaults": {
    "sizes": [{ "width": 1440, "height": 900 }, { "width": 1280, "height": 720 }],
    "dpis": [96],
    "themes": ["light", "dark"],
    "cultures": ["en-US"],
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
avascope baseline-create --suite .\baselines\agent-main-suite.json --manifest .\baselines\agent-main.json --out-dir .\baselines\agent-main-images
avascope baseline-check --manifest .\baselines\agent-main.json --out-dir .\artifacts\visual-current --diff-dir .\artifacts\visual-diff --report .\artifacts\visual-report.json --report-pack .\artifacts\visual-report-pack --tolerance 2
```

Suite creation expands to the same baseline manifest shape that `baseline-check` already consumes. `comparisonRules` can be declared in suite defaults, entries, or explicit variants. Scalar values such as `tolerance`, `maxChangedPixels`, and `maxChangedPercent` are overridden by the more specific level, while `ignoredRegions` and `requiredRegions` are combined. If no rules are configured, baseline checks keep the existing strict behavior. `runtimeTarget`, `profileName`, `profileVariant`, `profileFilePath`, and `mutationPresetIds` are recorded as structured provenance and agent handoff metadata; suite creation does not execute runtime mutations.

For CI artifact upload, prefer `baseline-check --report-pack <dir>` and upload that directory plus the configured current/diff directories. The older `eng\collect-baseline-artifacts.ps1` helper remains available for `--report` JSON-only workflows. See [VISUAL_REGRESSION_CI.md](VISUAL_REGRESSION_CI.md).

### Cleanup And Reload

Delete stale AvaScope-owned preview-session metadata:

```powershell
avascope cleanup
```

Cleanup only removes stale or invalid JSON records from the local AvaScope preview-session store. It does not terminate processes by name.

Delete stale or invalid AvaScope-owned bridge manifests:

```powershell
avascope cleanup-bridge-sessions --manifest-dir .\artifacts\sessions
```

Bridge cleanup deletes only stale or invalid local manifest JSON files. It does not kill processes and leaves live but unavailable or incompatible bridge-enabled apps in place for diagnostics.

Check reload support for a runtime bridge session:

```powershell
avascope reload --session session-id
```

Runtime bridge reload currently returns an explicit `runtime_reload_not_supported` result after verifying the local bridge session is active. CLI preview renders are one-shot and are not persisted across CLI processes.
Use `create-preview-session` when a CLI workflow needs persisted preview-session metadata and reload support across CLI processes.

## MCP

Start the stdio server with `avascope mcp` or `dotnet <install-root>/AvaScope.Mcp.dll`. Use the [client configuration example](AGENT_RECIPES.md#connect-any-stdio-mcp-client) and [tool catalog](STABLE_SURFACE.md#mcp-tools). CLI examples above have equivalent structured MCP requests; discover their actual schemas before calling.

Runtime input capability metadata is the canonical action reference. It lists
all pointer, keyboard, text, focus, scroll, and automation-pattern actions
with required parameters and examples. MCP schemas expose input actions,
mutation operations, diagnostics modes, picker operations, and preview
severity filters as closed enums.

`find_nodes` returns compact matching nodes without descendants by default.
Use `includeChildren`, `includeBounds`, `includeAccessibility`,
`includeBindings`, and `maxResponseDepth` for bounded expansion; use
`inspect_node` for complete detail. `includeBindings` returns a runtime binding
summary capped at 16 entries (`status`, data-context type, property/path and
resolution state); it never returns source-map data.

Tree, node-search, diagnostics, workflow, and runtime-scenario results share
inline byte, item, and depth budgets. When a budget is exceeded,
`responseBudget.truncated` is `true`, `reasons` identifies the exhausted
budget, and `artifactPath` points to the complete local JSON payload. The
inline result remains a deterministic bounded summary.

For explicit `semantic`, `synthetic` or `native` input, compound clicks, paired
keyboard sequences, motion profiles, and platform-specific dialog support, see
[Input strategies and native dialogs](INPUT_STRATEGIES.md). CLI `input --execution`
loads a bounded JSON options file; MCP uses the `execution` object. Native picker
requests use `topLevelId` (`--top-level`) for GTK3 X11 and AppKit paths.

`native_picker` is local-only and process-scoped. On Windows it detects and
controls only a picker whose window and owner chain belong to the selected
session process. Detection and commands use bounded timeouts, and selected
paths are redacted by default.

`predefine_result` stores a session-scoped, request-correlated, TTL-bounded
one-shot result. A `run_scenario` request can prepare the result inline with
`pickerResult`; a `picker_result` workflow step consumes it using the scenario
request id (or the step `text` as an explicit correlation id). Supported
deterministic outcomes are `success`, `cancelled`, `unavailable_path`, and
`deleted_path`. Replays return `not_prepared`, and late consumption returns
`expired`.

```json
{
  "requestId": "download-logs",
  "sessionId": "session-1",
  "topLevelId": "topLevel:main",
  "pickerResult": {
    "result": "deleted_path",
    "path": "<absolute-test-path>",
    "ttlMs": 30000
  },
  "steps": [
    {
      "id": "consume-picker",
      "action": "picker_result"
    }
  ]
}
```

Preview results contain a bounded diagnostic list, counts by severity and
category, a short summary, explicit comparison provenance, and a path to the
complete unfiltered JSON diagnostics artifact. Every diagnostic carries a
stable SHA-256 `fingerprint`. One-shot, multi-size, animation, and preview
session renders use identical filtering and comparison semantics.

MCP callers can use `errorsOnly`, the schema-constrained `minimumSeverity`,
`diagnosticsBaselinePath`, or `diagnosticsBaselineFingerprints`. CLI preview,
`preview-animation`, and `create-preview-session` expose the matching
`--errors-only`, `--minimum-severity`, `--diagnostics-baseline`, and
`--diagnostics-fingerprints` options. A prior `.diagnostics.json` artifact can
be used directly as the next baseline. The summary reports `newCount`,
`existingCount`, `resolvedCount`, `baselineCount`, and
`comparisonProvenance`; a missing or malformed baseline leaves the render
successful and returns an actionable `comparisonError`.

`capabilities` returns the same discovery manifest as the CLI command and accepts optional `requiredCapabilities` as comma-separated ids. It is the compatibility gate for clients that need specific runtime, preview, diagnostics, baseline, report, artifact, or mutation surfaces before invoking newer tools.

After attach, use `session_capabilities` (CLI:
`session-capabilities --session <id>`) as the effective compatibility gate.
It reports the connected bridge/product and protocol versions, supported IPC
methods, input actions, automation patterns, mutation operations/properties,
native-picker mode, and a deterministic SHA-256 `revision`. New bridges also
include the same object in `attach_to_app.value.effectiveCapabilities`; older
compatible bridges leave that additive field null, so clients can fall back to
the explicit query or the global catalog.

Every CLI/MCP operation separates transport completion from operation
completion. `transportSuccess` is true when AvaScope handled the request;
`success` is true only when the requested outcome was achieved. A failed
workflow, unsupported/rejected mutation, or requested process termination that
ends as `not_owned` or `termination_failed` therefore returns `success: false`.
Such operation failures can still contain a non-null `value` with bounded
partial evidence: completed steps, diagnostics, timeline/artifact paths, or the
already-closed session. Clients should inspect `error` first and retain `value`
for triage and cleanup.

`diagnostics` reports AvaScope service metadata, local bridge manifest/pipe health, stale, invalid, unauthorized, unavailable, duplicate, and protocol-incompatible bridge records, preview host readiness, stale or invalid preview-session metadata, and `componentOrigins` for CLI/MCP/PreviewHost assembly roots without building or loading user projects. The response keeps the legacy `issues` list and also includes `summary` counts, `nextCommands`, and bounded `diagnosticIssues` entries with source, severity, status, provenance, request ids, and related path/session metadata for agent triage. Mixed repo-local/package roots are reported as `diagnostics_mixed_install_roots`. The optional `mode` parameter accepts `all`, `active-only`, `minimal`, or `json-minimal`.

Preview readiness/build/render failures preserve the stable `error.code` and `error.message` shape and may include bounded `error.details` fields such as `phase`, `requirement`, `projectPath`, `viewPath`, `outputPath`, `exitCode`, `outputTail`, and `nextAction`. Readiness failures cover local prerequisites that can be checked before rendering, such as missing co-located PreviewHost assemblies, missing project files, missing view files, and unavailable `dotnet` process startup.

## Runtime Bridge

Activate the bridge only behind a host-owned development/test flag, then register the intended top-level:

```csharp
using AvaScope.Bridge;

var runtime = AvaScopeBridge.Activate(new BridgeActivationOptions("My app"));
using var registration = runtime.RegisterTopLevel(window);
```

For automatic lifetime registration or an app with no AvaScope package reference, use the [standalone provider](STANDALONE_PROVIDER.md). The local manifest/named-pipe transport handles bounded concurrent requests and correlates each response to its connection/request. Activation, custom actions and desktop capture have separate explicit authorization boundaries; see the [threat model](SECURITY_THREAT_MODEL.md).

Input behavior and provider/fallback limits are documented under [runtime input](#runtime-input) and [input strategies](INPUT_STRATEGIES.md). Mutation reset and history semantics are under [runtime mutations](#runtime-mutations).

## Preview Host

Preview rendering is isolated in `AvaScope.PreviewHost`, launched as a child process by Core, MCP, or CLI callers. The host:

- accepts a JSON `PreviewRequest`;
- optionally runs `dotnet build` for the requested `.csproj` using an isolated build output root by default;
- reports local readiness failures before build/render when project files, view files, host assemblies, or `dotnet` startup are missing;
- loads compiled Avalonia resource XAML through `avares://` when possible;
- loads compiled top-level `Application.Resources`, resource merged dictionaries, theme dictionaries, direct or included `Application.Styles`, `Application.DataTemplates`, and fallback `Application.DataContext` from `App.axaml`/`App.Initialize()` when present;
- falls back to standalone runtime `.axaml` loading;
- applies requested theme and culture variants inside the isolated render process;
- optionally instantiates a project-owned public parameterless design-data type and assigns it as the root control `DataContext`;
- renders through headless Skia;
- adds bounded binding/resource diagnostics, source-backed `x:DataType` binding diagnostics, and advisory layout warnings when public Avalonia APIs and source metadata expose enough signal;
- writes a PNG, structured JSON result, and full build-log artifact paths for build failures when available.

Successful preview responses can include diagnostics for missing `DataContext`, unresolved resource keys, missing or invalid converter resources, conservative binding path failures, `x:DataType` binding path mismatches, missing inherited `x:DataType` on `CompiledBinding`, text clipping/truncation, clipped content, unreachable content, sibling overlap, and too-small hit targets. These diagnostics are advisory and do not fail an otherwise successful screenshot.

Animation preview responses use the same isolated PreviewHost boundary and add explicit `animationTimeOffsetMs` frame sampling. `PreviewAnimationResponse` includes per-frame render results, optional `frameStripPath`, optional file-backed `viewer.previewUrl`, and motion diagnostics derived from sampled pixels.

Preview session tools store the original preview request, latest render result, bounded session events, and lifecycle status as Core metadata. MCP-backed and CLI-created preview session records are also persisted as JSON under the local AvaScope temp preview-session store so they can be restored after the MCP server or CLI process restarts. They do not keep user project code loaded inside MCP or CLI; each render still goes through `AvaScope.PreviewHost`.

See [preview sessions](#preview-sessions) for viewer, reload and bounded watch commands. Watch responses report `lifecycle.hostProcessMode: one_shot_isolated_child_process` and `persistentHostEnabled: false`; unchanged input snapshots produce `skipped` events without another render.

Current preview limitations:

- no runtime hot reload or persistent live preview host process yet; CLI watch reloads still use one-shot PreviewHost child processes;
- no project app startup/lifetime hook execution; `OnFrameworkInitializationCompleted`, project `MainWindow` creation, and app startup services are intentionally deferred;
- no JSON object injection, dependency injection, remote design data, or long-lived design-data state;
- no private Avalonia binding/style/resource hooks; diagnostics and computed provenance stay best-effort and public API based.

## Safety Boundaries

For an agent that needs to distinguish rendered content from visible desktop
presentation, use [paired screen evidence](SCREEN_EVIDENCE.md). Native capture
requires a separate host-only test-desktop grant and applies evidence masks before export.

- Bridge control is local-only and opt-in.
- Preview project build and view loading happen in `AvaScope.PreviewHost`, not inside the MCP server process.
- The MCP server is a thin adapter over Core.
- Tool results use structured JSON and file paths instead of unbounded payloads where practical.
- Runtime mutations remain local-only, reversible, bounded, and auditable. The current safe mutation set is intentionally limited to selected public Avalonia properties, classes, and local resource overrides.


## Task Reference

Use the guide for the selected task rather than loading every tool description:

| Task | Detailed contract and examples |
| --- | --- |
| Integrate or launch an app | [Standalone/package onboarding](STANDALONE_PROVIDER.md), [test profiles](AGENT_TEST_PROFILES.md), [target readiness](TARGET_READINESS.md), [test fixtures](TEST_FIXTURES.md) |
| Observe current state or changes | [Readiness](RUNTIME_READINESS.md), [observations](RUNTIME_OBSERVATIONS.md), [change cursors](RUNTIME_OBSERVATION_CHANGES.md), [provenance](RUNTIME_PROVENANCE.md) |
| Find and validate a target | [Relationships/projections](RELATIONSHIP_QUERIES.md), [virtual items/testability](TESTABILITY_AND_VIRTUAL_ITEMS.md), [picking/highlights](PICKING_AND_HIGHLIGHTS.md), [action maps](ACTION_MAP.md), [action explanations](ACTION_EXPLANATIONS.md) |
| Apply and verify state | [Input strategies](INPUT_STRATEGIES.md), [desired state](DESIRED_STATE_ACTIONS.md), [dispatch preconditions](DISPATCH_PRECONDITIONS.md), [runtime expressions](RUNTIME_EXPRESSIONS.md) |
| Work with fields and records | [Forms](FORM_WORKFLOWS.md), [tables](TABLE_WORKFLOWS.md), [text ranges](TEXT_RANGE_EDITING.md), [focus](FOCUS_NAVIGATION.md) |
| Coordinate windows and app work | [Windows](WINDOW_MANAGEMENT.md), [navigation](NAVIGATION_HISTORY.md), [scenes](SEMANTIC_SCENES.md), [operations](RUNTIME_OPERATIONS.md), [traces](RUNTIME_TRACES.md) |
| Recover or replay | [Run recovery](RUN_RECOVERY.md), [workflow export/replay](WORKFLOW_EXPORT.md) |
| Assess native behavior | [Screen evidence](SCREEN_EVIDENCE.md), [native accessibility](NATIVE_ACCESSIBILITY.md), [platform matrix](NATIVE_PLATFORM_MATRIX.md), [managed X11](MANAGED_X11.md), [managed Wayland](MANAGED_WAYLAND.md) |
