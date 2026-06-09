# AvaScope Agent Workflow

This workflow is for agents and developers validating AvaScope against a local Avalonia project. It uses the packaged CLI path when available because that is closest to public-alpha usage.

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

`doctor` exits non-zero when co-located AvaScope assemblies are missing or stale diagnostic records need attention. `diagnostics` returns the lower-level bridge and preview-host records.

## 3. Preview A View

The getting-started sample includes `avascope.preview.json` with a `main` profile:

```powershell
& $avascope preview .\samples\AvaScope.GettingStartedApp\AvaScope.GettingStartedApp.csproj --profile main --out .\artifacts\samples\agent-workflow\main-preview.png
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
      "designDataType": "MyApp.Design.PreviewData"
    }
  }
}
```

Explicit CLI options override profile values.

## 4. Use Durable Preview Sessions

```powershell
& $avascope create-preview-session .\samples\AvaScope.GettingStartedApp\AvaScope.GettingStartedApp.csproj --profile main
& $avascope list-preview-sessions
& $avascope reload-preview-session --session <preview-session-id>
& $avascope watch-preview-session --session <preview-session-id> --timeout-ms 30000 --settle-ms 250 --max-reloads 1
& $avascope close-preview-session --session <preview-session-id>
```

Preview sessions persist request metadata only. Each render still runs through an isolated `AvaScope.PreviewHost` child process.

## 5. Inspect A Running App

Start the sample with the opt-in bridge:

```powershell
$env:AVASCOPE_SAMPLE_BRIDGE = "1"
dotnet run --project .\samples\AvaScope.GettingStartedApp\AvaScope.GettingStartedApp.csproj
```

In another terminal:

```powershell
& $avascope diagnostics --max-sessions 10
& $avascope attach --session <runtime-session-id>
& $avascope list-top-levels --session <runtime-session-id>
& $avascope visual-tree --session <runtime-session-id> --top-level <topLevel:id> --max-depth 4
& $avascope find-nodes --session <runtime-session-id> --top-level <topLevel:id> --type TextBlock --max-depth 6
& $avascope inspect-node --session <runtime-session-id> --top-level <topLevel:id> --node <node-id>
```

Runtime bridge activation is always explicit and local-only. AvaScope does not open a network listener.

## 6. Capture And Compare

```powershell
& $avascope screenshot --session <runtime-session-id> --top-level <topLevel:id> --out .\artifacts\samples\runtime-screenshot.png
& $avascope diff --baseline .\artifacts\samples\main-preview.png --current .\artifacts\samples\runtime-screenshot.png --out .\artifacts\samples\runtime-diff.png --tolerance 2
```

For preview-only visual regression:

```powershell
& $avascope baseline-create .\samples\AvaScope.GettingStartedApp\AvaScope.GettingStartedApp.csproj --view Views\MainView.axaml --manifest .\artifacts\samples\baselines\getting-started.json --sizes 720x420,360x240 --theme light --design-data-type AvaScope.GettingStartedApp.SamplePreviewData
& $avascope baseline-check --manifest .\artifacts\samples\baselines\getting-started.json --out-dir .\artifacts\samples\baselines\current --diff-dir .\artifacts\samples\baselines\diff --tolerance 0
```

## 7. Send Narrow Runtime Input

```powershell
& $avascope input --session <runtime-session-id> --top-level <topLevel:id> --action focus --target-node <node-id>
& $avascope input --session <runtime-session-id> --top-level <topLevel:id> --action key_text --target-node <textBox-node-id> --text "hello"
& $avascope input --session <runtime-session-id> --top-level <topLevel:id> --action clear_text --target-node <textBox-node-id>
& $avascope input --session <runtime-session-id> --top-level <topLevel:id> --action click --x 120 --y 40
```

Runtime input is intentionally narrow and non-destructive. Unsupported actions return structured errors.

## 8. Close And Clean Up

```powershell
& $avascope close-session --session <runtime-session-id>
& $avascope cleanup
```

`cleanup` removes stale or invalid AvaScope-owned preview-session metadata. It does not terminate processes by name.
