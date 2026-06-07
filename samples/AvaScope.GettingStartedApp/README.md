# AvaScope Getting Started Sample

This sample is a tiny Avalonia 12 app for validating the first AvaScope workflows.

Preview the main view:

```powershell
dotnet .\src\AvaScope.Cli\bin\Debug\net10.0\avascope.dll preview .\samples\AvaScope.GettingStartedApp\AvaScope.GettingStartedApp.csproj --view Views\MainView.axaml --out .\artifacts\samples\getting-started-preview.png --width 720 --height 420 --theme light --design-data-type AvaScope.GettingStartedApp.SamplePreviewData
```

Run the app with the local-only bridge enabled:

```powershell
$env:AVASCOPE_SAMPLE_BRIDGE = "1"
dotnet run --project .\samples\AvaScope.GettingStartedApp\AvaScope.GettingStartedApp.csproj
```

The sample bridge is disabled by default. When enabled, it writes a local-only session manifest and serves AvaScope requests through a current-user local named pipe; it does not open a network listener.

In another terminal, inspect active local bridge sessions:

```powershell
dotnet .\src\AvaScope.Cli\bin\Debug\net10.0\avascope.dll diagnostics --max-sessions 10
```

Use the reported session id with `list-top-levels`, `visual-tree`, `screenshot`, and the other runtime CLI commands documented in the root README.
