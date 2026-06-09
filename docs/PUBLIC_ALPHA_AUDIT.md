# AvaScope Public Alpha Audit

Date: 2026-06-09

This audit is the release-candidate gate for the current public-alpha goal. It records the requirements, current evidence, and non-blocking deferrals that define the repository's public-alpha state after the W17-W25 development period.

## Requirements And Evidence

| Requirement | Evidence | Status |
| --- | --- | --- |
| Avalonia 12 and `net10.0` target | Project files target `net10.0`; Avalonia-facing packages reference Avalonia `12.0.4`; `AGENTS.md` and README state Avalonia 12. | Met |
| Agent-owned project management | `AGENTS.md` defines agent ownership and `docs/DEVELOPMENT_PLAN.md` workflow; development plan contains milestone tracking, validation history, decision log, and change log. | Met |
| Stable protocol/core/session foundation | `AvaScope.Protocol` contains transport-neutral DTOs; `AvaScope.Core` owns session registry, local bridge client, preview client, preview-session store; protocol/core tests cover JSON and lifecycle behavior. | Met |
| MCP/CLI adapter workflows | MCP exposes health, sessions, runtime inspection/control, diagnostics, preview sessions, and reload; CLI exposes matching local workflows plus `doctor`, preview profiles, packaged workflow docs, and baseline reports. Both stay thin over Core. | Met |
| Runtime bridge workflows | Opt-in bridge activation, local manifests, current-user local named-pipe IPC, top-level list, screenshot, tree, inspect, find, input, close, diagnostics, and runtime reload unsupported diagnostics are implemented and tested. W21 added targeted writable `TextBox` `clear_text`. | Met |
| Preview workflows | Isolated PreviewHost builds/loads projects, renders `.axaml`, applies app resources/styles/includes/theme dictionaries/data templates, culture and design-data variants, and returns structured failures. Durable preview-session watching now skips unchanged input bursts without keeping user code loaded. | Met |
| Startup/lifecycle boundary | Preview runs project `Application.Initialize()` for App.axaml composition but explicitly defers `OnFrameworkInitializationCompleted`, project `MainWindow`, startup services, and long-lived preview host processes. | Met |
| Diagnostics | Bridge diagnostics, preview-host readiness, runtime unsupported reload diagnostics, bounded preview failure `error.details`, preview advisory diagnostics, and W22 `diagnosticIssues` source/severity/provenance entries are implemented and documented. | Met |
| Runtime safety | Bridge is opt-in, local-only, manifest scope is explicit, current-user local named pipe is used where supported, and unsupported transport manifests are invalid. No network listener is introduced. | Met |
| Getting-started workflow | `samples/AvaScope.GettingStartedApp` is in the solution, documented in README and sample README, previewable from CLI, and bridge activation is gated by `AVASCOPE_SAMPLE_BRIDGE`. | Met |
| Visual regression workflow | `baseline-create` and `baseline-check` produce explicit baseline/current/diff artifacts, non-zero mismatch exits, and optional W24 JSON reports for CI upload and agent summarization. | Met |
| Release/package readiness | Local NuGet packages for Protocol/Core/Bridge, RID-specific framework-dependent executable ZIPs, artifact verification manifest, local release script, and GitHub Release dry-run script exist. Publishing credentials/feed upload are deferred. | Met |
| Validation coverage | Unit/integration/smoke tests cover protocol, core, MCP, bridge, preview host, CLI, sample preview, baseline reports, release artifacts, and safety boundaries; W25 release-candidate validation passed. | Met |
| Clean committed worktree | W25 documentation/tracking changes are the final source change for this goal and are committed after validation; generated artifacts remain ignored under `artifacts/`. | Met |

## Public-Alpha Non-Blocking Deferrals

- Runtime hot reload remains explicitly unsupported and returns `runtime_reload_not_supported`.
- Drag/drop, richer pointer/key behavior, IME-level text input, and destructive runtime actions are out of the public-alpha tool set. Targeted `TextBox` `key_text` and `clear_text` are supported for writable controls.
- Full project startup/lifetime orchestration for preview is deferred for safety.
- JSON object design-data injection, dependency injection, remote design data, and long-lived design-data state are deferred.
- Self-contained executable ZIPs are available through an opt-in local script lane after W16; default GitHub Release assets remain framework-dependent. macOS artifacts, installer publishing, and broader upload automation are deferred.
- Preview diagnostics now include first slices for binding/resource/layout warnings, source-backed `x:DataType` checks, computed inspection, and diagnostics issue provenance; deeper private binding-engine telemetry and full resource-chain provenance remain deferred unless public Avalonia APIs expose reliable sources.

## Final W25 Validation Gates

The W17-W25 public-alpha release-candidate goal can be marked complete because these commands passed from the repository root on 2026-06-09.

Final W25 run results:

- `dotnet build AvaScope.slnx`: passed with 0 warnings and 0 errors.
- `dotnet test AvaScope.slnx --no-build`: passed with 203 tests.
- `powershell -NoProfile -ExecutionPolicy Bypass -File .\eng\create-local-release.ps1`: passed; Release build/test passed with 203 tests, three NuGet packages were created, win-x64 and linux-x64 framework-dependent executable ZIPs were created, 5 release artifacts were verified, packaged doctor passed, and packaged sample preview passed.
- `powershell -NoProfile -ExecutionPolicy Bypass -File .\eng\publish-github-release.ps1 -Tag v0.1.0 -DryRun`: passed against the generated packages, executable ZIPs, and release manifest.
- Packaged CLI `doctor --manifest-dir .\artifacts\samples\w25-doctor\sessions --preview-session-store .\artifacts\samples\w25-doctor\preview-sessions`: passed.
- Packaged CLI MCP handoff smoke: passed by starting `avascope.exe mcp`, confirming it stayed running briefly, and stopping it.
- Packaged CLI sample `baseline-create` and `baseline-check --report`: passed; `artifacts\samples\w25-baseline\report.json` exists and parses with `passed=true` and 1 entry.
- `git check-ignore -v` confirmed release artifacts and generated W25 baseline report remain ignored under `artifacts/`.
- `git diff --check`: passed after W25 documentation updates.

```powershell
dotnet build AvaScope.slnx
dotnet test AvaScope.slnx --no-build
powershell -NoProfile -ExecutionPolicy Bypass -File .\eng\create-local-release.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File .\eng\publish-github-release.ps1 -Tag v0.1.0 -DryRun
.\artifacts\executables\avascope-win-x64-framework-dependent\avascope.exe doctor --manifest-dir .\artifacts\samples\w25-doctor\sessions --preview-session-store .\artifacts\samples\w25-doctor\preview-sessions
.\artifacts\executables\avascope-win-x64-framework-dependent\avascope.exe baseline-create .\samples\AvaScope.GettingStartedApp\AvaScope.GettingStartedApp.csproj --view Views\MainView.axaml --manifest .\artifacts\samples\w25-baseline\baseline.json --sizes 360x240 --out-dir .\artifacts\samples\w25-baseline\baseline --theme light --design-data-type AvaScope.GettingStartedApp.SamplePreviewData
.\artifacts\executables\avascope-win-x64-framework-dependent\avascope.exe baseline-check --manifest .\artifacts\samples\w25-baseline\baseline.json --out-dir .\artifacts\samples\w25-baseline\current --diff-dir .\artifacts\samples\w25-baseline\diff --report .\artifacts\samples\w25-baseline\report.json --tolerance 0
git check-ignore -v artifacts\release-manifest.json artifacts\packages\AvaScope.Protocol.0.1.0.nupkg artifacts\executables\avascope-win-x64-framework-dependent.zip artifacts\samples\getting-started-preview-release.png artifacts\samples\w25-baseline\report.json
git status --short
```
