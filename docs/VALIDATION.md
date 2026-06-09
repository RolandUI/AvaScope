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

For source-backed preview diagnostics work, include the typed-binding smoke path:

```powershell
dotnet test AvaScope.slnx --filter FullyQualifiedName~PreviewHostSmokeTests.PreviewHostReturnsDataTypeBindingPathDiagnostics
```

For CLI work, also run:

```powershell
dotnet test AvaScope.slnx --filter FullyQualifiedName~Cli
```

For CLI doctor/self-test work, include:

```powershell
dotnet test AvaScope.slnx --filter FullyQualifiedName~CliSmokeTests.DoctorCommandReportsLocalReadiness
dotnet test AvaScope.slnx --filter FullyQualifiedName~ProtocolContractTests.DoctorResponseSerializesStableReadinessShape
```

For CLI preview-session work, include the persistent-session smoke path:

```powershell
dotnet test AvaScope.slnx --filter FullyQualifiedName~CliSmokeTests.PreviewSessionCommandsCreateListReloadAndClosePersistedSession
dotnet test AvaScope.slnx --filter FullyQualifiedName~CliSmokeTests.ReloadPreviewSessionCommandReturnsStructuredErrorWhenNoPreviewSessionMatches
dotnet test AvaScope.slnx --filter FullyQualifiedName~CliSmokeTests.WatchPreviewSessionCommandReloadsWhenWatchedFileChanges
```

For CLI preview profile work, include:

```powershell
dotnet test AvaScope.slnx --filter FullyQualifiedName~CliSmokeTests.PreviewCommandUsesProjectPreviewProfileAndAllowsExplicitOverrides
dotnet test AvaScope.slnx --filter FullyQualifiedName~CliSmokeTests.CreatePreviewSessionCommandUsesProjectPreviewProfile
```

For feature-ticket work covering preview diagnostics, computed inspection, multi-size preview, diff, or cleanup, run the targeted smoke checks first and then the full suite:

```powershell
dotnet test AvaScope.slnx --no-build --filter Protocol
dotnet test AvaScope.slnx --no-build --filter FullyQualifiedName~PreviewHost
dotnet test AvaScope.slnx --no-build --filter FullyQualifiedName~Cli
dotnet test AvaScope.slnx --no-build --filter Bridge
dotnet test AvaScope.slnx --no-build --filter Mcp
dotnet test AvaScope.slnx --no-build
```

For visual regression workflow work, include:

```powershell
dotnet test AvaScope.slnx --filter FullyQualifiedName~CliSmokeTests.BaselineCommandsCreateManifestPassCheckAndFailChangedCheck
dotnet test AvaScope.slnx --filter FullyQualifiedName~ProtocolContractTests.PreviewBaselineResponsesSerializeStableShapes
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
.\artifacts\executables\avascope-win-x64-framework-dependent\avascope.exe doctor
.\artifacts\executables\avascope-win-x64-framework-dependent\avascope.exe preview .\samples\AvaScope.GettingStartedApp\AvaScope.GettingStartedApp.csproj --view Views\MainView.axaml --out .\artifacts\samples\getting-started-preview-release.png --width 720 --height 420 --theme light --design-data-type AvaScope.GettingStartedApp.SamplePreviewData
```

Use `.\artifacts\executables\avascope-win-x64-framework-dependent\avascope.exe` for external project testing after the script completes.

For the opt-in self-contained executable lane, validate a narrow local artifact set with:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\eng\create-local-release.ps1 -RuntimeIdentifiers win-x64 -ExecutablePackageKind self-contained -SkipTests -SkipSampleSmoke
powershell -NoProfile -ExecutionPolicy Bypass -File .\eng\publish-github-release.ps1 -Tag v0.1.0 -ExecutableRuntimeIdentifiers win-x64 -ExecutablePackageKind self-contained -DryRun
```

NuGet publishing in CI requires a repository secret named `NUGET_API_KEY`. The `Release` workflow publishes from `master` or `main` when the `Directory.Build.props` `<Version>` value has no matching remote `v<Version>` tag yet.

The release workflow publishes library packages to nuget.org and GitHub Packages, creates the `v<Version>` tag, creates or updates the matching GitHub Release, and uploads the three `.nupkg` files, RID-specific framework-dependent executable ZIPs, and `artifacts\release-manifest.json`.

Before publishing library packages manually, validate the exact publish set without pushing:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\eng\publish-nuget.ps1 -DryRun
powershell -NoProfile -ExecutionPolicy Bypass -File .\eng\publish-github-release.ps1 -Tag v0.1.0 -DryRun
```

Manual NuGet publishing requires a nuget.org API key supplied by `AVASCOPE_NUGET_API_KEY`, `NUGET_API_KEY`, or the `-ApiKey` parameter.

Then verify generated artifacts are ignored:

```powershell
git check-ignore -v artifacts\release-manifest.json artifacts\packages\AvaScope.Protocol.0.1.0.nupkg artifacts\executables\avascope-win-x64-framework-dependent.zip artifacts\samples\getting-started-preview-release.png
```
