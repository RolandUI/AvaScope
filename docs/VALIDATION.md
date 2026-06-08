# AvaScope Validation

Run these commands from the repository root before marking a development slice complete:

```powershell
dotnet restore AvaScope.slnx
dotnet build AvaScope.slnx
dotnet test AvaScope.slnx
git status --short
```

Run build and test commands sequentially. Parallel build/test invocations can contend for the same `bin/` and `obj/` outputs.

For protocol-only work, also run:

```powershell
dotnet test AvaScope.slnx --filter Protocol
```

For core-only work, also run:

```powershell
dotnet test AvaScope.slnx --filter Core
```

For MCP adapter work, also run:

```powershell
dotnet test AvaScope.slnx --filter Mcp
```

For Avalonia bridge work, also run:

```powershell
dotnet test AvaScope.slnx --filter Bridge
```

For preview host work, also run:

```powershell
dotnet test AvaScope.slnx --filter FullyQualifiedName~PreviewHost
```

For CLI work, also run:

```powershell
dotnet test AvaScope.slnx --filter FullyQualifiedName~Cli
```

## Public Alpha Release Validation

Before marking a public-alpha readiness or release-workflow slice complete, run:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\eng\create-local-release.ps1
```

The script wraps the release gate:

```powershell
dotnet restore AvaScope.slnx
dotnet build AvaScope.slnx -c Release
dotnet test AvaScope.slnx -c Release --no-build
dotnet pack .\src\AvaScope.Protocol\AvaScope.Protocol.csproj -c Release --no-build --output .\artifacts\packages
dotnet pack .\src\AvaScope.Core\AvaScope.Core.csproj -c Release --no-build --output .\artifacts\packages
dotnet pack .\src\AvaScope.Bridge\AvaScope.Bridge.csproj -c Release --no-build --output .\artifacts\packages
powershell -NoProfile -ExecutionPolicy Bypass -File .\eng\package-executables.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File .\eng\verify-artifacts.ps1
```

It also validates the getting-started preview path from the packaged Windows CLI:

```powershell
.\artifacts\executables\avascope-win-x64-framework-dependent\avascope.exe preview .\samples\AvaScope.GettingStartedApp\AvaScope.GettingStartedApp.csproj --view Views\MainView.axaml --out .\artifacts\samples\getting-started-preview-release.png --width 720 --height 420 --theme light --design-data-type AvaScope.GettingStartedApp.SamplePreviewData
```

Use `.\artifacts\executables\avascope-win-x64-framework-dependent\avascope.exe` for external project testing after the script completes.

Then verify generated artifacts are ignored:

```powershell
git check-ignore -v artifacts\release-manifest.json artifacts\packages\AvaScope.Protocol.0.1.0.nupkg artifacts\executables\avascope-win-x64-framework-dependent.zip artifacts\samples\getting-started-preview-release.png
```
