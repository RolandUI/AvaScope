# AvaScope Public Alpha Audit

Date: 2026-06-07

This audit is the completion gate for the current public-alpha goal. It records the requirements, current evidence, and non-blocking deferrals that define the repository's public-alpha state.

## Requirements And Evidence

| Requirement | Evidence | Status |
| --- | --- | --- |
| Avalonia 12 and `net10.0` target | Project files target `net10.0`; Avalonia-facing packages reference Avalonia `12.0.4`; `AGENTS.md` and README state Avalonia 12. | Met |
| Agent-owned project management | `AGENTS.md` defines agent ownership and `docs/DEVELOPMENT_PLAN.md` workflow; development plan contains milestone tracking, validation history, decision log, and change log. | Met |
| Stable protocol/core/session foundation | `AvaScope.Protocol` contains transport-neutral DTOs; `AvaScope.Core` owns session registry, local bridge client, preview client, preview-session store; protocol/core tests cover JSON and lifecycle behavior. | Met |
| MCP/CLI adapter workflows | MCP exposes health, sessions, runtime inspection/control, diagnostics, preview sessions, and reload; CLI exposes matching local workflows. Both stay thin over Core. | Met |
| Runtime bridge workflows | Opt-in bridge activation, local manifests, current-user local named-pipe IPC, top-level list, screenshot, tree, inspect, find, input, close, diagnostics, and runtime reload unsupported diagnostics are implemented and tested. | Met |
| Preview workflows | Isolated PreviewHost builds/loads projects, renders `.axaml`, applies app resources/styles/includes/theme dictionaries/data templates, culture and design-data variants, and returns structured failures. | Met |
| Startup/lifecycle boundary | Preview runs project `Application.Initialize()` for App.axaml composition but explicitly defers `OnFrameworkInitializationCompleted`, project `MainWindow`, startup services, and long-lived preview host processes. | Met |
| Diagnostics | Bridge diagnostics, preview-host readiness, runtime unsupported reload diagnostics, and bounded preview failure `error.details` are implemented and documented. | Met |
| Runtime safety | Bridge is opt-in, local-only, manifest scope is explicit, current-user local named pipe is used where supported, and unsupported transport manifests are invalid. No network listener is introduced. | Met |
| Getting-started workflow | `samples/AvaScope.GettingStartedApp` is in the solution, documented in README and sample README, previewable from CLI, and bridge activation is gated by `AVASCOPE_SAMPLE_BRIDGE`. | Met |
| Release/package readiness | Local NuGet packages for Protocol/Core/Bridge, RID-specific framework-dependent executable ZIPs, artifact verification manifest, and CI workflow exist. Publishing credentials/feed upload are deferred. | Met |
| Validation coverage | Unit/integration/smoke tests cover protocol, core, MCP, bridge, preview host, CLI, sample preview, release artifacts, and safety boundaries; final M50 validation passed. | Met |
| Clean committed worktree | M50 documentation/tracking changes are committed as the final source change; post-commit `git status --short` is checked empty in the final handoff. | Met |

## Public-Alpha Non-Blocking Deferrals

- Runtime hot reload remains explicitly unsupported and returns `runtime_reload_not_supported`.
- Drag/drop, richer pointer/key behavior, IME-level text input, and destructive runtime actions are out of the public-alpha tool set.
- Full project startup/lifetime orchestration for preview is deferred for safety.
- JSON object design-data injection, dependency injection, remote design data, and long-lived design-data state are deferred.
- Self-contained executable ZIPs are available through an opt-in local script lane after W16; default GitHub Release assets remain framework-dependent. macOS artifacts, installer publishing, and broader upload automation are deferred.
- Rich binding/layout/resource diagnostics beyond bounded preview failure context were deferred at M50; W9 later added first slices for preview binding/resource diagnostics, layout warnings, and computed property inspection while deeper private provenance remains deferred.

## Final M50 Validation Gates

The public-alpha goal can be marked complete only after these commands pass from the repository root after this audit is updated.

Final M50 run results:

- `dotnet build AvaScope.slnx`: passed with 0 warnings and 0 errors.
- `dotnet test AvaScope.slnx --no-build`: passed with 172 tests.
- `dotnet build AvaScope.slnx -c Release`: passed with 0 warnings and 0 errors.
- `dotnet test AvaScope.slnx -c Release --no-build`: passed with 172 tests.
- `dotnet pack` for `AvaScope.Protocol`, `AvaScope.Core`, and `AvaScope.Bridge`: passed; verifier confirmed all three `0.1.0` NuGet packages.
- `powershell -NoProfile -ExecutionPolicy Bypass -File .\eng\package-executables.ps1`: passed; created win-x64 and linux-x64 framework-dependent executable ZIPs.
- `powershell -NoProfile -ExecutionPolicy Bypass -File .\eng\verify-artifacts.ps1`: passed; verified 5 release artifacts and wrote `artifacts\release-manifest.json`.
- Source CLI getting-started preview smoke: passed; rendered `artifacts\samples\getting-started-preview.png` at 720x420.
- Packaged win-x64 CLI getting-started preview smoke: passed; rendered `artifacts\samples\getting-started-preview-packaged.png` at 720x420.
- `git check-ignore -v` for release manifest, package, executable ZIP, and sample preview artifact: passed.
- Pre-commit `git status --short`: only M50 documentation/tracking changes remained.

```powershell
dotnet build AvaScope.slnx
dotnet test AvaScope.slnx --no-build
dotnet build AvaScope.slnx -c Release
dotnet test AvaScope.slnx -c Release --no-build
dotnet pack .\src\AvaScope.Protocol\AvaScope.Protocol.csproj -c Release --no-build --output .\artifacts\packages
dotnet pack .\src\AvaScope.Core\AvaScope.Core.csproj -c Release --no-build --output .\artifacts\packages
dotnet pack .\src\AvaScope.Bridge\AvaScope.Bridge.csproj -c Release --no-build --output .\artifacts\packages
powershell -NoProfile -ExecutionPolicy Bypass -File .\eng\package-executables.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File .\eng\verify-artifacts.ps1
dotnet .\src\AvaScope.Cli\bin\Debug\net10.0\avascope.dll preview .\samples\AvaScope.GettingStartedApp\AvaScope.GettingStartedApp.csproj --view Views\MainView.axaml --out .\artifacts\samples\getting-started-preview.png --width 720 --height 420 --theme light --design-data-type AvaScope.GettingStartedApp.SamplePreviewData
.\artifacts\executables\avascope-win-x64-framework-dependent\avascope.exe preview .\samples\AvaScope.GettingStartedApp\AvaScope.GettingStartedApp.csproj --view Views\MainView.axaml --out .\artifacts\samples\getting-started-preview-packaged.png --width 720 --height 420 --theme light --design-data-type AvaScope.GettingStartedApp.SamplePreviewData
git check-ignore -v artifacts\release-manifest.json artifacts\packages\AvaScope.Protocol.0.1.0.nupkg artifacts\executables\avascope-win-x64-framework-dependent.zip artifacts\samples\getting-started-preview.png
git status --short
```
