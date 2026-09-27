# AvaScope Getting Started Sample

This sample is a tiny Avalonia 12 app for validating the first AvaScope agent workflows.

The full packaged-CLI agent runbook is in `docs\AGENT_WORKFLOW.md` from the repository root. It shows the local control-plane loop: readiness checks, previews, runtime inspection, evidence capture, input, and cleanup.

Preview the main view:

```powershell
dotnet .\src\AvaScope.Cli\bin\Debug\net10.0\avascope.dll preview .\samples\AvaScope.GettingStartedApp\AvaScope.GettingStartedApp.csproj --view Views\MainView.axaml --out .\artifacts\samples\getting-started-preview.png --width 720 --height 420 --theme light --design-data-type AvaScope.GettingStartedApp.SamplePreviewData
```

The same settings are available through `avascope.preview.json` in this sample directory:

```powershell
dotnet .\src\AvaScope.Cli\bin\Debug\net10.0\avascope.dll preview .\samples\AvaScope.GettingStartedApp\AvaScope.GettingStartedApp.csproj --profile main
dotnet .\src\AvaScope.Cli\bin\Debug\net10.0\avascope.dll preview .\samples\AvaScope.GettingStartedApp\AvaScope.GettingStartedApp.csproj --profile main --variant dark
dotnet .\src\AvaScope.Cli\bin\Debug\net10.0\avascope.dll create-preview-session .\samples\AvaScope.GettingStartedApp\AvaScope.GettingStartedApp.csproj --profile main
```

The `main` profile declares `dark`, `hu`, and `compact` variants. Variants override the base profile before explicit CLI options, so a command can still pass `--width`, `--height`, or `--out` for one-off runs.

Preview the animation sample with requested time offsets:

```powershell
dotnet .\src\AvaScope.Cli\bin\Debug\net10.0\avascope.dll preview-animation .\samples\AvaScope.GettingStartedApp\AvaScope.GettingStartedApp.csproj --profile animation
```

The `animation` profile requests `Views\AnimationView.axaml` frames at `0,250,900,900ms`, writes per-offset PNGs and a frame strip, and returns a file-backed viewer `previewUrl`. Avalonia 12.1.3 uses wall-clock animation time, so these requested offsets are not controlled or verified; each frame reports an `animation_frame_sampled` warning. Do not use the images to assert exact animation timing ([#201](https://github.com/RolandUI/AvaScope/issues/201)). The repeated final offset reuses the first `900ms` image; this proves cache reuse, not final animation state. The viewer's pixel motion summary describes captured images only. Moving-property metadata remains explicitly `not_available` when public Avalonia APIs do not expose it reliably.

Run the app with the local-only bridge enabled:

```powershell
$env:AVASCOPE_SAMPLE_BRIDGE = "1"
dotnet run --project .\samples\AvaScope.GettingStartedApp\AvaScope.GettingStartedApp.csproj
```

The sample bridge is disabled by default. When enabled, it writes a local-only session manifest and serves AvaScope requests through a current-user local named pipe; it does not open a network listener. The sample also explicitly enables and allowlists `confirm` and `reset` actions on its `MainView` custom control. `confirm` accepts an optional `note` parameter; `reset` is classified as destructive and requires explicit request authorization.

In another terminal, inspect active local bridge sessions:

```powershell
dotnet .\src\AvaScope.Cli\bin\Debug\net10.0\avascope.dll diagnostics --max-sessions 10
```

Use the reported session id with `list-top-levels`, `visual-tree`, `screenshot`, and the other runtime CLI commands documented in the root README.

Use the visual-tree node id for `MainContent` to discover and invoke the sample actions:

```powershell
dotnet .\src\AvaScope.Cli\bin\Debug\net10.0\avascope.dll custom-actions --session <session-id> --top-level <top-level-id> --node <main-content-node-id>
dotnet .\src\AvaScope.Cli\bin\Debug\net10.0\avascope.dll invoke-custom-action --session <session-id> --top-level <top-level-id> --node <main-content-node-id> --action confirm --parameters "note=from-agent"
dotnet .\src\AvaScope.Cli\bin\Debug\net10.0\avascope.dll invoke-custom-action --session <session-id> --top-level <top-level-id> --node <main-content-node-id> --action reset --allow-destructive true
```

If diagnostics show stale sample bridge manifests after stopping the app, run `cleanup-bridge-sessions` from the repository root to remove stale local manifest JSON records without terminating processes.

For the semantic graph demo, also set `AVASCOPE_SAMPLE_SCENE=1`. The additional
window draws two nodes and their connection without child controls. Resolve the
`SemanticGraph` visual target, call `scene` with `objectType: "connection"` and
`relatedTo: "a"`, then invoke `scene.select` using the returned `expectedObject`.
Zoom/pan or remove/recreate the connection with the buttons: an old scene target
must be refreshed before selecting again. See [scene contracts and guards](../../docs/SEMANTIC_SCENES.md).
