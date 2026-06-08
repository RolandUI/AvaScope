# AvaScope Development Plan

This document is the primary project-management source for autonomous agents working on AvaScope. Update it whenever meaningful implementation, validation, or planning changes the project state.

## Project Operating Rules

- Work in small vertical slices that produce buildable, testable behavior.
- Keep exactly one `Current Focus` item active at a time.
- Before implementing, compare the requested work with this plan. If the plan is stale, update the plan first.
- Each meaningful implementation change must include relevant tests or an explicit validation note explaining why tests are not applicable.
- Each completed slice must be validated with the listed commands before status moves to `Done`.
- Commit and push each completed vertical slice or coherent milestone part. Record the commit hash in the handoff and, when practical, in this document.
- Keep MCP, CLI, core runtime, bridge, preview host, and protocol concerns separated.
- Do not introduce broad skeletons unless they directly support the active vertical slice.

## Status Legend

- `Not Started`: No implementation work has begun.
- `In Progress`: The active agent is implementing or validating this item.
- `Blocked`: Progress requires external input, credentials, package availability, or a product decision.
- `Review`: Implementation is complete, but validation or handoff is still pending.
- `Done`: Acceptance criteria and validation are complete.

## Current Focus

- `W1 Intake Ledgers`
- Status: `In Progress`
- Owner: autonomous agent
- Started: `2026-06-08`
- Goal: maintain sanitized bug reports and feature request tickets without starting fixes or feature work until explicitly requested.

## Next Action

Store future user-provided bug reports and feature requests as sanitized ledger entries. Do not start fixes or feature implementation unless the user explicitly requests work on a stored ticket.

## Latest Validation

- `2026-06-08`: Documentation-only ownership update; `git diff --check` and intake privacy validation passed before commit/push.
- `2026-06-08`: `powershell -NoProfile -ExecutionPolicy Bypass -File .\eng\create-local-release.ps1` passed after W7 version-bump CI release changes; Release build/test passed with 179 tests, 5 release artifacts verified, packaged Windows sample preview smoke passed.
- `2026-06-08`: Release metadata simulation passed after W7 validation: `Directory.Build.props` version `0.1.0`, derived release tag `v0.1.0`, and remote tag absence detection.
- `2026-06-08`: `powershell -NoProfile -ExecutionPolicy Bypass -File .\eng\publish-github-release.ps1 -Tag v0.1.0 -DryRun` passed after W7 validation against freshly generated release artifacts.
- `2026-06-08`: `powershell -NoProfile -ExecutionPolicy Bypass -File .\eng\publish-nuget.ps1 -DryRun` passed after W7 validation against freshly generated release artifacts.
- `2026-06-08`: `powershell -NoProfile -ExecutionPolicy Bypass -File .\eng\validate-bug-reports.ps1` passed after W7 documentation updates; 13 intake files scanned.
- `2026-06-08`: `powershell -NoProfile -ExecutionPolicy Bypass -File .\eng\create-local-release.ps1` passed after adding GitHub Packages and GitHub Release asset publishing; Release build/test passed with 179 tests, 5 release artifacts verified, packaged Windows sample preview smoke passed.
- `2026-06-08`: `powershell -NoProfile -ExecutionPolicy Bypass -File .\eng\publish-github-release.ps1 -Tag v0.1.0 -DryRun` passed and validated all GitHub Release assets: three `.nupkg` files, win/linux executable ZIPs, and `release-manifest.json`.
- `2026-06-08`: `powershell -NoProfile -ExecutionPolicy Bypass -File .\eng\publish-github-release.ps1 -Tag v0.1.1 -DryRun` failed as expected because the tag did not match `Directory.Build.props` version `0.1.0`.
- `2026-06-08`: `powershell -NoProfile -ExecutionPolicy Bypass -File .\eng\publish-nuget.ps1 -DryRun` passed after GitHub release distribution workflow updates.
- `2026-06-08`: `powershell -NoProfile -ExecutionPolicy Bypass -File .\eng\validate-bug-reports.ps1` passed after GitHub release distribution documentation updates; 13 intake files scanned.
- `2026-06-08`: `git diff --check` passed after GitHub release distribution workflow updates.
- `2026-06-08`: `powershell -NoProfile -ExecutionPolicy Bypass -File .\eng\publish-nuget.ps1 -DryRun` passed after changing NuGet publishing to tag-triggered CI release.
- `2026-06-08`: PowerShell tag/version check passed for `v0.1.0` against `Directory.Build.props` version `0.1.0`.
- `2026-06-08`: `powershell -NoProfile -ExecutionPolicy Bypass -File .\eng\validate-bug-reports.ps1` passed after CI release workflow documentation updates; 13 intake files scanned.
- `2026-06-08`: `git diff --check` passed after CI release workflow updates.
- `2026-06-08`: `powershell -NoProfile -ExecutionPolicy Bypass -File .\eng\create-local-release.ps1` passed after adding the NuGet publish workflow; Release build/test passed with 179 tests, 5 release artifacts verified, packaged Windows sample preview smoke passed.
- `2026-06-08`: `powershell -NoProfile -ExecutionPolicy Bypass -File .\eng\publish-nuget.ps1 -DryRun` passed against `AvaScope.Protocol.0.1.0.nupkg`, `AvaScope.Core.0.1.0.nupkg`, and `AvaScope.Bridge.0.1.0.nupkg`.
- `2026-06-08`: `git check-ignore -v` confirmed regenerated NuGet packages, release manifest, and packaged sample preview output remain ignored under `artifacts/`.
- `2026-06-08`: `powershell -NoProfile -ExecutionPolicy Bypass -File .\eng\validate-bug-reports.ps1` passed after storing `FEAT-0001` through `FEAT-0007`; 13 intake files scanned.
- `2026-06-08`: `powershell -NoProfile -ExecutionPolicy Bypass -File .\eng\create-local-release.ps1` passed; Release build/test passed with 179 tests, 5 release artifacts verified, packaged Windows sample preview smoke passed.
- `2026-06-08`: `dotnet test AvaScope.slnx --no-build` passed with 179 tests after implementing `BUG-0001` and `BUG-0002`.
- `2026-06-08`: `powershell -NoProfile -ExecutionPolicy Bypass -File .\eng\validate-bug-reports.ps1` passed after marking `BUG-0001` and `BUG-0002` fixed; 4 bug report files scanned.
- `2026-06-08`: `dotnet test AvaScope.slnx --no-build --filter Protocol` passed with 30 tests after optional preview dimensions.
- `2026-06-08`: `dotnet test AvaScope.slnx --no-build --filter FullyQualifiedName~Cli` passed with 77 tests after CLI design-dimension fallback.
- `2026-06-08`: `dotnet test AvaScope.slnx --no-build --filter FullyQualifiedName~Mcp` passed with 29 tests after optional preview dimensions.
- `2026-06-08`: `dotnet test AvaScope.slnx --no-build --filter FullyQualifiedName~PreviewHost` passed with 27 tests after Window-root and design-time metadata support.
- `2026-06-08`: `dotnet build AvaScope.slnx` passed with 0 warnings and 0 errors after implementing stored PreviewHost bug fixes.
- `2026-06-08`: `powershell -NoProfile -ExecutionPolicy Bypass -File .\eng\validate-bug-reports.ps1` passed after storing `BUG-0002`; 4 bug report files scanned.
- `2026-06-08`: `powershell -NoProfile -ExecutionPolicy Bypass -File .\eng\validate-bug-reports.ps1` passed after creating the sanitized bug report ledger.
- `2026-06-06`: `dotnet restore AvaScope.slnx` passed.
- `2026-06-06`: `dotnet build AvaScope.slnx` passed with 0 warnings and 0 errors.
- `2026-06-06`: `dotnet test AvaScope.slnx` passed with 29 tests.
- `2026-06-06`: `dotnet test AvaScope.slnx --filter Protocol` passed with 10 tests.
- `2026-06-06`: `dotnet test AvaScope.slnx --filter Core` passed with 9 tests.
- `2026-06-06`: `dotnet test AvaScope.slnx --filter Mcp` passed with 4 tests.
- `2026-06-06`: `dotnet test AvaScope.slnx --filter Bridge` passed with 8 tests.
- `2026-06-06`: `dotnet build AvaScope.slnx` passed with 0 warnings and 0 errors after M5 bridge IPC foundation.
- `2026-06-06`: `dotnet test AvaScope.slnx --no-build --filter Protocol` passed with 13 tests.
- `2026-06-06`: `dotnet test AvaScope.slnx --no-build --filter Bridge` passed with 13 tests.
- `2026-06-06`: `dotnet test AvaScope.slnx --no-build` passed with 34 tests.
- `2026-06-06`: `dotnet build AvaScope.slnx` passed with 0 warnings and 0 errors after M5 attach client/MCP tool slice.
- `2026-06-06`: `dotnet test AvaScope.slnx --no-build --filter Protocol` passed with 15 tests.
- `2026-06-06`: `dotnet test AvaScope.slnx --no-build --filter Core` passed with 13 tests.
- `2026-06-06`: `dotnet test AvaScope.slnx --no-build --filter Mcp` passed with 8 tests.
- `2026-06-06`: `dotnet test AvaScope.slnx --no-build --filter Bridge` passed with 18 tests.
- `2026-06-06`: `dotnet test AvaScope.slnx --no-build` passed with 43 tests.
- `2026-06-06`: `dotnet build AvaScope.slnx` passed with 0 warnings and 0 errors after M5 completion.
- `2026-06-06`: `dotnet test AvaScope.slnx --no-build --filter Bridge` passed with 19 tests after MCP/Core/pipe screenshot validation.
- `2026-06-06`: `dotnet test AvaScope.slnx --no-build --filter Mcp` passed with 9 tests after MCP/Core/pipe screenshot validation.
- `2026-06-06`: `dotnet test AvaScope.slnx --no-build` passed with 44 tests.
- `2026-06-06`: `dotnet build AvaScope.slnx` passed with 0 warnings and 0 errors after M6 tree serialization slice.
- `2026-06-06`: `dotnet test AvaScope.slnx --no-build --filter Protocol` passed with 16 tests.
- `2026-06-06`: `dotnet test AvaScope.slnx --no-build --filter Core` passed with 14 tests.
- `2026-06-06`: `dotnet test AvaScope.slnx --no-build --filter Mcp` passed with 10 tests.
- `2026-06-06`: `dotnet test AvaScope.slnx --no-build --filter Bridge` passed with 20 tests.
- `2026-06-06`: `dotnet test AvaScope.slnx --no-build` passed with 47 tests.
- `2026-06-06`: `dotnet build AvaScope.slnx` passed with 0 warnings and 0 errors after M6 `find_nodes` slice.
- `2026-06-06`: `dotnet test AvaScope.slnx --no-build --filter Protocol` passed with 17 tests.
- `2026-06-06`: `dotnet test AvaScope.slnx --no-build --filter Core` passed with 15 tests.
- `2026-06-06`: `dotnet test AvaScope.slnx --no-build --filter Mcp` passed with 11 tests.
- `2026-06-06`: `dotnet test AvaScope.slnx --no-build --filter Bridge` passed with 21 tests.
- `2026-06-06`: `dotnet test AvaScope.slnx --no-build` passed with 50 tests.
- `2026-06-06`: `dotnet build AvaScope.slnx` passed with 0 warnings and 0 errors after M7 input MVP slice.
- `2026-06-06`: `dotnet test AvaScope.slnx --no-build --filter Protocol` passed with 18 tests.
- `2026-06-06`: `dotnet test AvaScope.slnx --no-build --filter Core` passed with 16 tests.
- `2026-06-06`: `dotnet test AvaScope.slnx --no-build --filter Mcp` passed with 13 tests.
- `2026-06-06`: `dotnet test AvaScope.slnx --no-build --filter Bridge` passed with 23 tests.
- `2026-06-06`: `dotnet test AvaScope.slnx --no-build` passed with 54 tests.
- `2026-06-06`: `AvaScope.Protocol` package list checked; no package references found.
- `2026-06-06`: `AvaScope.Core` package list checked; no package references found.
- `2026-06-06`: `rg "Avalonia|ModelContextProtocol|Mcp|MCP" src\AvaScope.Protocol tests\AvaScope.Tests\Protocol` found no matches.
- `2026-06-06`: `rg "Avalonia|ModelContextProtocol|Mcp|MCP" src\AvaScope.Core tests\AvaScope.Tests\Core` found no matches.
- `2026-06-06`: Markdown tracking fields checked for `Current Focus`, `Next Action`, `Status`, `Acceptance Criteria`, and `Validation`.
- `2026-06-07`: `dotnet build AvaScope.slnx` passed with 0 warnings and 0 errors after M7 routed pointer move completion.
- `2026-06-07`: `dotnet test AvaScope.slnx --no-build --filter Bridge` passed with 23 tests.
- `2026-06-07`: `dotnet test AvaScope.slnx --no-build` passed with 54 tests.
- `2026-06-07`: `dotnet build AvaScope.slnx` passed with 0 warnings and 0 errors after the first M8 preview host slice.
- `2026-06-07`: `dotnet test AvaScope.slnx --no-build --filter Protocol` passed with 20 tests.
- `2026-06-07`: `dotnet test AvaScope.slnx --no-build --filter FullyQualifiedName~PreviewHost` passed with 1 test.
- `2026-06-07`: `dotnet test AvaScope.slnx --no-build` passed with 57 tests.
- `2026-06-07`: `dotnet build AvaScope.slnx` passed with 0 warnings and 0 errors after M8 project-aware path resolution.
- `2026-06-07`: `dotnet test AvaScope.slnx --no-build --filter FullyQualifiedName~PreviewHost` passed with 2 tests.
- `2026-06-07`: `dotnet test AvaScope.slnx --no-build` passed with 58 tests.
- `2026-06-07`: `dotnet build AvaScope.slnx` passed with 0 warnings and 0 errors after M8 project build boundary.
- `2026-06-07`: `dotnet test AvaScope.slnx --no-build --filter FullyQualifiedName~PreviewHost` passed with 3 tests.
- `2026-06-07`: `dotnet test AvaScope.slnx --no-build` passed with 59 tests.
- `2026-06-07`: `dotnet build AvaScope.slnx` passed with 0 warnings and 0 errors after M8 compiled project resource loading.
- `2026-06-07`: `dotnet test AvaScope.slnx --no-build --filter FullyQualifiedName~PreviewHost` passed with 4 tests.
- `2026-06-07`: `dotnet test AvaScope.slnx --no-build` passed with 60 tests.
- `2026-06-07`: `dotnet build AvaScope.slnx` passed with 0 warnings and 0 errors after M9 preview adapter integration.
- `2026-06-07`: `dotnet test AvaScope.slnx --no-build --filter Core` passed with 18 tests.
- `2026-06-07`: `dotnet test AvaScope.slnx --no-build --filter Mcp` passed with 15 tests.
- `2026-06-07`: `dotnet test AvaScope.slnx --no-build` passed with 64 tests.
- `2026-06-07`: `dotnet build AvaScope.slnx` passed with 0 warnings and 0 errors after M10 CLI integration.
- `2026-06-07`: `dotnet test AvaScope.slnx --no-build --filter FullyQualifiedName~Cli` passed with 13 tests.
- `2026-06-07`: `dotnet test AvaScope.slnx --no-build` passed with 67 tests.
- `2026-06-07`: Markdown tracking/status fields checked after M11 documentation update.
- `2026-06-07`: `dotnet build AvaScope.slnx` passed with 0 warnings and 0 errors after M11 documentation update.
- `2026-06-07`: `dotnet test AvaScope.slnx --no-build` passed with 67 tests.
- `2026-06-07`: Post-MVP gap audit recorded in `docs/GAP_AUDIT.md`; selected runtime `close_session` lifecycle support as the next slice.
- `2026-06-07`: `dotnet build AvaScope.slnx` passed with 0 warnings and 0 errors after M12 runtime `close_session` lifecycle implementation.
- `2026-06-07`: `dotnet test AvaScope.slnx --no-build --filter Protocol` passed with 21 tests.
- `2026-06-07`: `dotnet test AvaScope.slnx --no-build --filter Core` passed with 19 tests.
- `2026-06-07`: `dotnet test AvaScope.slnx --no-build --filter Bridge` passed with 26 tests.
- `2026-06-07`: `dotnet test AvaScope.slnx --no-build --filter Mcp` passed with 18 tests.
- `2026-06-07`: `dotnet test AvaScope.slnx --no-build --filter PreviewHost` passed with 8 tests after temp-directory cleanup retry hardening.
- `2026-06-07`: `dotnet test AvaScope.slnx --no-build` passed with 72 tests.
- `2026-06-07`: `dotnet build AvaScope.slnx` passed with 0 warnings and 0 errors after M13 diagnostics implementation.
- `2026-06-07`: `dotnet test AvaScope.slnx --no-build --filter Protocol` passed with 22 tests.
- `2026-06-07`: `dotnet test AvaScope.slnx --no-build --filter Core` passed with 22 tests.
- `2026-06-07`: `dotnet test AvaScope.slnx --no-build --filter Mcp` passed with 21 tests.
- `2026-06-07`: `dotnet test AvaScope.slnx --no-build` passed with 79 tests.
- `2026-06-07`: `dotnet build AvaScope.slnx` passed with 0 warnings and 0 errors after M14 preview App.axaml resource loading.
- `2026-06-07`: `dotnet test AvaScope.slnx --no-build --filter FullyQualifiedName~PreviewHost` passed with 10 tests.
- `2026-06-07`: `dotnet test AvaScope.slnx --no-build` passed with 81 tests.
- `2026-06-07`: `dotnet build AvaScope.slnx` passed with 0 warnings and 0 errors after M15 preview diagnostics expansion and build-server isolation hardening.
- `2026-06-07`: `dotnet test AvaScope.slnx --no-build --filter Protocol` passed with 22 tests.
- `2026-06-07`: `dotnet test AvaScope.slnx --no-build --filter Core` passed with 25 tests.
- `2026-06-07`: `dotnet test AvaScope.slnx --no-build --filter Mcp` passed with 21 tests.
- `2026-06-07`: `dotnet test AvaScope.slnx --no-build --filter FullyQualifiedName~PreviewHost` passed with 13 tests.
- `2026-06-07`: `dotnet test AvaScope.slnx --no-build` passed with 84 tests.
- `2026-06-07`: `dotnet build AvaScope.slnx` passed with 0 warnings and 0 errors after M16 preview reload foundation.
- `2026-06-07`: `dotnet test AvaScope.slnx --no-build --filter Protocol` passed with 23 tests.
- `2026-06-07`: `dotnet test AvaScope.slnx --no-build --filter Core` passed with 28 tests.
- `2026-06-07`: `dotnet test AvaScope.slnx --no-build --filter Mcp` passed with 24 tests.
- `2026-06-07`: `dotnet test AvaScope.slnx --no-build` passed with 91 tests.
- `2026-06-07`: `dotnet build AvaScope.slnx` passed with 0 warnings and 0 errors after M17 preview reload MVP.
- `2026-06-07`: `dotnet test AvaScope.slnx --no-build --filter Protocol` passed with 23 tests.
- `2026-06-07`: `dotnet test AvaScope.slnx --no-build --filter Core` passed with 32 tests.
- `2026-06-07`: `dotnet test AvaScope.slnx --no-build --filter Mcp` passed with 26 tests.
- `2026-06-07`: `dotnet test AvaScope.slnx --no-build` passed with 97 tests.
- `2026-06-07`: `dotnet build AvaScope.slnx` passed with 0 warnings and 0 errors after M18 pointer press/release input.
- `2026-06-07`: `dotnet test AvaScope.slnx --no-build --filter Protocol` passed with 24 tests.
- `2026-06-07`: `dotnet test AvaScope.slnx --no-build --filter Core` passed with 32 tests.
- `2026-06-07`: `dotnet test AvaScope.slnx --no-build --filter Bridge` passed with 33 tests.
- `2026-06-07`: `dotnet test AvaScope.slnx --no-build --filter Mcp` passed with 26 tests.
- `2026-06-07`: `dotnet test AvaScope.slnx --no-build` first hit a transient PreviewHost temp-directory cleanup lock, then passed on immediate rerun with 98 tests.
- `2026-06-07`: `dotnet build AvaScope.slnx` passed with 0 warnings and 0 errors after M19 runtime reload contract implementation.
- `2026-06-07`: `dotnet test AvaScope.slnx --no-build --filter Protocol` passed with 24 tests.
- `2026-06-07`: `dotnet test AvaScope.slnx --no-build --filter Core` passed with 33 tests.
- `2026-06-07`: `dotnet test AvaScope.slnx --no-build --filter Bridge` passed with 36 tests.
- `2026-06-07`: `dotnet test AvaScope.slnx --no-build --filter Mcp` passed with 28 tests.
- `2026-06-07`: `dotnet test AvaScope.slnx --no-build` passed with 101 tests after PreviewHost cleanup lock hardening.
- `2026-06-07`: `dotnet build AvaScope.slnx -c Release` passed with 0 warnings and 0 errors after M20 package metadata.
- `2026-06-07`: `dotnet pack src\AvaScope.Protocol\AvaScope.Protocol.csproj -c Release --no-build --output artifacts\packages` created `AvaScope.Protocol.0.1.0.nupkg`.
- `2026-06-07`: `dotnet pack src\AvaScope.Core\AvaScope.Core.csproj -c Release --no-build --output artifacts\packages` created `AvaScope.Core.0.1.0.nupkg`.
- `2026-06-07`: `dotnet pack src\AvaScope.Bridge\AvaScope.Bridge.csproj -c Release --no-build --output artifacts\packages` created `AvaScope.Bridge.0.1.0.nupkg`.
- `2026-06-07`: Package metadata inspected from `.nuspec`; Bridge declares dependencies on `AvaScope.Core`, `AvaScope.Protocol`, and `Avalonia` 12.0.4, and includes `README.md`.
- `2026-06-07`: `dotnet pack` for `AvaScope.Mcp`, `AvaScope.Cli`, and `AvaScope.PreviewHost` completed as no-op because those projects are explicitly `IsPackable=false`.
- `2026-06-07`: `git check-ignore -v artifacts\packages\AvaScope.Bridge.0.1.0.nupkg` confirmed package artifacts are ignored by `.gitignore`.
- `2026-06-07`: GitHub Actions workflow added with `actions/checkout@v6`, `actions/setup-dotnet@v5`, Windows runner, restore, Release build, Release test, and local pack steps.
- `2026-06-07`: `dotnet restore AvaScope.slnx` passed after M21 CI workflow addition.
- `2026-06-07`: `dotnet build AvaScope.slnx -c Release --no-restore` passed with 0 warnings and 0 errors after M21 CI workflow addition.
- `2026-06-07`: `dotnet test AvaScope.slnx -c Release --no-build` passed with 101 tests.
- `2026-06-07`: CI pack command sequence created `AvaScope.Protocol.0.1.0.nupkg`, `AvaScope.Core.0.1.0.nupkg`, and `AvaScope.Bridge.0.1.0.nupkg`.
- `2026-06-07`: `dotnet build AvaScope.slnx -c Release` passed with 0 warnings and 0 errors after M22 executable packaging script addition.
- `2026-06-07`: `powershell -NoProfile -ExecutionPolicy Bypass -File .\eng\package-executables.ps1 -NoBuild` created `artifacts\executables\avascope-win-framework-dependent.zip`.
- `2026-06-07`: Artifact inspection confirmed the executable ZIP contains `avascope`, `AvaScope.Mcp`, `AvaScope.PreviewHost`, `AvaScope.Core`, and `AvaScope.Protocol` co-located.
- `2026-06-07`: Artifact smoke validation passed: `dotnet artifacts\executables\avascope\avascope.dll` returned structured `invalid_cli_arguments` with exit code 2, and `dotnet artifacts\executables\avascope\avascope.dll mcp --help` started and shut down the co-located MCP server with exit code 0.
- `2026-06-07`: `git check-ignore -v artifacts\executables\avascope-win-framework-dependent.zip` confirmed executable artifacts are ignored by `.gitignore`.
- `2026-06-07`: `dotnet test AvaScope.slnx -c Release --no-build` passed with 101 tests after M22 executable packaging validation.
- `2026-06-07`: `dotnet build AvaScope.slnx -c Release` passed with 0 warnings and 0 errors after M23 artifact verification script addition.
- `2026-06-07`: `dotnet pack` created `AvaScope.Protocol.0.1.0.nupkg`, `AvaScope.Core.0.1.0.nupkg`, and `AvaScope.Bridge.0.1.0.nupkg` in `artifacts\packages`.
- `2026-06-07`: `powershell -NoProfile -ExecutionPolicy Bypass -File .\eng\package-executables.ps1 -NoBuild` created `artifacts\executables\avascope-win-framework-dependent.zip`.
- `2026-06-07`: `powershell -NoProfile -ExecutionPolicy Bypass -File .\eng\verify-artifacts.ps1` verified 4 release artifacts and wrote `artifacts\release-manifest.json`.
- `2026-06-07`: Manifest inspection confirmed artifact names, relative paths, byte sizes, and SHA-256 hashes for the three NuGet packages and executable ZIP.
- `2026-06-07`: `git check-ignore -v artifacts\release-manifest.json artifacts\packages\AvaScope.Protocol.0.1.0.nupkg artifacts\executables\avascope-win-framework-dependent.zip` confirmed verification output and artifacts are ignored.
- `2026-06-07`: `dotnet test AvaScope.slnx -c Release --no-build` passed with 101 tests after M23 artifact verification workflow update.
- `2026-06-07`: `powershell -NoProfile -ExecutionPolicy Bypass -File .\eng\package-executables.ps1` created `avascope-win-x64-framework-dependent.zip` and `avascope-linux-x64-framework-dependent.zip`.
- `2026-06-07`: `powershell -NoProfile -ExecutionPolicy Bypass -File .\eng\verify-artifacts.ps1` verified 5 release artifacts after M24 RID-based executable packaging.
- `2026-06-07`: ZIP inspection confirmed the Windows artifact contains `.exe` apphosts and the Linux artifact contains extensionless apphosts, with `AvaScope.Core.dll` and `AvaScope.Protocol.dll` co-located in both.
- `2026-06-07`: Windows RID artifact smoke validation passed: `dotnet artifacts\executables\avascope-win-x64-framework-dependent\avascope.dll` returned structured `invalid_cli_arguments` with exit code 2, and `dotnet artifacts\executables\avascope-win-x64-framework-dependent\avascope.dll mcp --help` started and shut down the co-located MCP server with exit code 0.
- `2026-06-07`: `dotnet build AvaScope.slnx -c Release` passed with 0 warnings and 0 errors after M24 RID-based executable packaging.
- `2026-06-07`: `git check-ignore -v artifacts\release-manifest.json artifacts\executables\avascope-win-x64-framework-dependent.zip artifacts\executables\avascope-linux-x64-framework-dependent.zip` confirmed RID executable artifacts and manifest are ignored.
- `2026-06-07`: `dotnet test AvaScope.slnx -c Release --no-build` passed with 101 tests after M24 packaging update.
- `2026-06-07`: Official Avalonia 12.0.4 source and API docs checked for `InputElement.Focus(...)`, `InputElement.KeyDownEvent`, `InputElement.KeyUpEvent`, and `KeyEventArgs` init properties before M25 input implementation.
- `2026-06-07`: `dotnet build AvaScope.slnx` passed with 0 warnings and 0 errors after M25 focus/key input implementation.
- `2026-06-07`: `dotnet test AvaScope.slnx --no-build --filter Protocol` passed with 25 tests.
- `2026-06-07`: `dotnet test AvaScope.slnx --no-build --filter Core` passed with 33 tests.
- `2026-06-07`: `dotnet test AvaScope.slnx --no-build --filter Bridge` passed with 37 tests.
- `2026-06-07`: `dotnet test AvaScope.slnx --no-build --filter Mcp` passed with 28 tests.
- `2026-06-07`: `dotnet test AvaScope.slnx --no-build` passed with 102 tests after M25 focus/key input implementation.
- `2026-06-07`: `dotnet build AvaScope.slnx` passed with 0 warnings and 0 errors after M26 `inspect_node` implementation.
- `2026-06-07`: `dotnet test AvaScope.slnx --no-build --filter Protocol` passed with 27 tests.
- `2026-06-07`: `dotnet test AvaScope.slnx --no-build --filter Core` passed with 34 tests.
- `2026-06-07`: `dotnet test AvaScope.slnx --no-build --filter Bridge` passed with 39 tests.
- `2026-06-07`: `dotnet test AvaScope.slnx --no-build --filter Mcp` passed with 29 tests.
- `2026-06-07`: `dotnet test AvaScope.slnx --no-build` passed with 106 tests after M26 `inspect_node` implementation.
- `2026-06-07`: `dotnet build AvaScope.slnx` passed with 0 warnings and 0 errors after M27 CLI `attach` implementation.
- `2026-06-07`: `dotnet test AvaScope.slnx --no-build --filter FullyQualifiedName~Cli` passed with 24 tests.
- `2026-06-07`: `dotnet test AvaScope.slnx --no-build --filter Core` passed with 34 tests.
- `2026-06-07`: `dotnet test AvaScope.slnx --no-build` passed with 108 tests after M27 CLI runtime attach workflow.
- `2026-06-07`: `dotnet build AvaScope.slnx` passed with 0 warnings and 0 errors after M28 CLI top-level/screenshot implementation.
- `2026-06-07`: `dotnet test AvaScope.slnx --no-build --filter FullyQualifiedName~Cli` passed with 30 tests, including fake bridge pipe success paths for `list-top-levels` and `screenshot`.
- `2026-06-07`: `dotnet test AvaScope.slnx --no-build` passed with 114 tests after M28 CLI runtime top-level/screenshot workflow.
- `2026-06-07`: `dotnet build AvaScope.slnx` passed with 0 warnings and 0 errors after M29 CLI visual/logical tree implementation.
- `2026-06-07`: `dotnet test AvaScope.slnx --no-build --filter FullyQualifiedName~Cli` passed with 38 tests, including fake bridge pipe success paths for `visual-tree` and `logical-tree`.
- `2026-06-07`: `dotnet test AvaScope.slnx --no-build` passed with 122 tests after M29 CLI runtime tree workflow.
- `2026-06-07`: `dotnet build AvaScope.slnx` passed with 0 warnings and 0 errors after M30 CLI `inspect-node` implementation.
- `2026-06-07`: `dotnet test AvaScope.slnx --no-build --filter FullyQualifiedName~Cli` passed with 46 tests, including fake bridge pipe success paths for `inspect-node`.
- `2026-06-07`: `dotnet test AvaScope.slnx --no-build` passed with 130 tests after M30 CLI node detail workflow.
- `2026-06-07`: `dotnet build AvaScope.slnx` passed with 0 warnings and 0 errors after M31 CLI `find-nodes` implementation.
- `2026-06-07`: `dotnet test AvaScope.slnx --no-build --filter FullyQualifiedName~Cli` passed with 52 tests, including fake bridge pipe success paths for `find-nodes`.
- `2026-06-07`: `dotnet test AvaScope.slnx --no-build` passed with 136 tests after M31 CLI node search workflow.
- `2026-06-07`: `dotnet build AvaScope.slnx` passed with 0 warnings and 0 errors after M32 CLI `input` implementation.
- `2026-06-07`: `dotnet test AvaScope.slnx --no-build --filter FullyQualifiedName~Cli` passed with 63 tests, including fake bridge pipe success paths for `input`.
- `2026-06-07`: `dotnet test AvaScope.slnx --no-build` passed with 147 tests after M32 CLI runtime input workflow.
- `2026-06-07`: `dotnet build AvaScope.slnx` passed with 0 warnings and 0 errors after M33 CLI `close-session` implementation.
- `2026-06-07`: `dotnet test AvaScope.slnx --no-build --filter FullyQualifiedName~Cli` passed with 66 tests, including fake bridge pipe success paths for `close-session`.
- `2026-06-07`: `dotnet test AvaScope.slnx --no-build` passed with 150 tests after M33 CLI close-session workflow.
- `2026-06-07`: `dotnet build AvaScope.slnx` passed with 0 warnings and 0 errors after M34 CLI `diagnostics` implementation.
- `2026-06-07`: `dotnet test AvaScope.slnx --no-build --filter FullyQualifiedName~Cli` passed with 71 tests, including fake bridge pipe success paths for `diagnostics`.
- `2026-06-07`: `dotnet test AvaScope.slnx --no-build` passed with 155 tests after M34 CLI diagnostics workflow.
- `2026-06-07`: `dotnet build AvaScope.slnx` passed with 0 warnings and 0 errors after M35 CLI `reload` implementation.
- `2026-06-07`: `dotnet test AvaScope.slnx --no-build --filter FullyQualifiedName~Cli` passed with 74 tests, including fake bridge health checks for CLI `reload`.
- `2026-06-07`: `dotnet test AvaScope.slnx --no-build` passed with 158 tests after M35 CLI reload workflow.
- `2026-06-07`: `dotnet build AvaScope.slnx` passed with 0 warnings and 0 errors after M36 durable preview-session store implementation.
- `2026-06-07`: `dotnet test AvaScope.slnx --no-build --filter FullyQualifiedName~PreviewSessionRegistryTests` passed with 8 tests.
- `2026-06-07`: `dotnet test AvaScope.slnx --no-build --filter Core` passed with 36 tests.
- `2026-06-07`: `dotnet test AvaScope.slnx --no-build` passed with 160 tests after M36 reload/hot preview foundation.
- `2026-06-07`: `dotnet build AvaScope.slnx` passed with 0 warnings and 0 errors after M37 App.axaml style loading.
- `2026-06-07`: `dotnet test AvaScope.slnx --no-build --filter FullyQualifiedName~PreviewHostSmokeTests` passed with 7 tests.
- `2026-06-07`: `dotnet test AvaScope.slnx --no-build` passed with 161 tests after M37 preview App.axaml style scope.
- `2026-06-07`: `dotnet build AvaScope.slnx` passed with 0 warnings and 0 errors after M38 App.axaml resource include loading.
- `2026-06-07`: `dotnet test AvaScope.slnx --no-build --filter FullyQualifiedName~PreviewHostSmokeTests` passed with 8 tests.
- `2026-06-07`: `dotnet test AvaScope.slnx --no-build` passed with 162 tests after M38 preview resource include scope.
- `2026-06-07`: `dotnet build AvaScope.slnx` passed with 0 warnings and 0 errors after M39 App.axaml theme dictionary loading.
- `2026-06-07`: `dotnet test AvaScope.slnx --no-build --filter FullyQualifiedName~PreviewHostSmokeTests` passed with 9 tests.
- `2026-06-07`: `dotnet test AvaScope.slnx --no-build` passed with 163 tests after M39 preview theme dictionary variant scope.
- `2026-06-07`: `dotnet build AvaScope.slnx` passed with 0 warnings and 0 errors after M40 app style include coverage.
- `2026-06-07`: `dotnet test AvaScope.slnx --no-build --filter FullyQualifiedName~PreviewHostSmokeTests` passed with 10 tests.
- `2026-06-07`: `dotnet test AvaScope.slnx --no-build` passed with 164 tests after M40 preview style include scope.
- `2026-06-07`: `dotnet build AvaScope.slnx` passed with 0 warnings and 0 errors after M41 preview culture contract.
- `2026-06-07`: `dotnet test AvaScope.slnx --no-build --filter Protocol` passed with 27 tests.
- `2026-06-07`: `dotnet test AvaScope.slnx --no-build --filter FullyQualifiedName~PreviewHostAppliesRequestedCultureBeforeProjectViewLoading` passed with 1 test.
- `2026-06-07`: `dotnet test AvaScope.slnx --no-build --filter FullyQualifiedName~PreviewCommandRendersAxamlThroughPreviewHostClient` passed with 1 test.
- `2026-06-07`: `dotnet test AvaScope.slnx --no-build --filter FullyQualifiedName~PreviewAxamlRendersThroughPreviewHostClient` passed with 1 test.
- `2026-06-07`: `dotnet test AvaScope.slnx --no-build` passed with 165 tests after M41 preview culture contract.
- `2026-06-07`: Markdown tracking/status reviewed after M42 design-data contract audit.
- `2026-06-07`: `dotnet build AvaScope.slnx` passed with 0 warnings and 0 errors after M42 design-data contract audit.
- `2026-06-07`: `dotnet build AvaScope.slnx` passed with 0 warnings and 0 errors after M43 design-data type implementation.
- `2026-06-07`: `dotnet test AvaScope.slnx --no-build --filter Protocol` passed with 27 tests.
- `2026-06-07`: `dotnet test AvaScope.slnx --no-build --filter FullyQualifiedName~PreviewCommandRendersAxamlThroughPreviewHostClient` passed with 1 test.
- `2026-06-07`: `dotnet test AvaScope.slnx --no-build --filter FullyQualifiedName~PreviewAxamlRendersThroughPreviewHostClient` passed with 1 test.
- `2026-06-07`: `dotnet test AvaScope.slnx --no-build --filter FullyQualifiedName~PreviewHostAppliesProjectDesignDataTypeAsRootDataContext` passed with 1 test.
- `2026-06-07`: `dotnet test AvaScope.slnx --no-build --filter FullyQualifiedName~PreviewHostReturnsStructuredErrorWhenDesignDataTypeIsMissing` passed with 1 test.
- `2026-06-07`: `dotnet test AvaScope.slnx --no-build` passed with 167 tests after M43 preview design-data type slice.
- `2026-06-07`: Official Avalonia application lifetime docs checked for `ApplicationLifetime`, design mode null lifetime behavior, and manual lifetime management before M44 startup boundary decision.
- `2026-06-07`: Markdown tracking/status reviewed after M44 App startup boundary audit.
- `2026-06-07`: `dotnet build AvaScope.slnx` passed with 0 warnings and 0 errors after M44 App startup boundary audit.
- `2026-06-07`: Official Avalonia Application/DataTemplates documentation checked before M45 app-level data-template transfer.
- `2026-06-07`: `dotnet build AvaScope.slnx` passed with 0 warnings and 0 errors after M45 app-level data-template implementation.
- `2026-06-07`: `dotnet test AvaScope.slnx --no-build --filter FullyQualifiedName~PreviewHostAppliesCompiledAppDataTemplatesBeforeProjectView` passed with 1 test.
- `2026-06-07`: `dotnet test AvaScope.slnx --no-build --filter FullyQualifiedName~PreviewHost` passed with 22 tests.
- `2026-06-07`: `dotnet test AvaScope.slnx --no-build` passed with 168 tests after M45 app-level data-template implementation.
- `2026-06-07`: `dotnet build AvaScope.slnx` passed with 0 warnings and 0 errors after adding `samples/AvaScope.GettingStartedApp`.
- `2026-06-07`: Documented sample preview command passed and rendered `artifacts\samples\getting-started-preview.png` at 720x420.
- `2026-06-07`: `git check-ignore -v artifacts\samples\getting-started-preview.png` confirmed the generated sample preview artifact is ignored.
- `2026-06-07`: `dotnet test AvaScope.slnx --no-build --filter FullyQualifiedName~PreviewCommandResolvesRelativeProjectAndOutputPathsFromCallerWorkingDirectory` passed with 1 test.
- `2026-06-07`: `dotnet test AvaScope.slnx --no-build --filter FullyQualifiedName~Cli` passed with 75 tests after CLI preview path normalization.
- `2026-06-07`: `dotnet test AvaScope.slnx --no-build` passed with 169 tests after M46 getting-started sample and CLI path normalization.
- `2026-06-07`: `dotnet build AvaScope.slnx -c Release` passed with 0 warnings and 0 errors after M46.
- `2026-06-07`: `dotnet test AvaScope.slnx -c Release --no-build` passed with 169 tests.
- `2026-06-07`: `dotnet pack` created `AvaScope.Protocol.0.1.0.nupkg`, `AvaScope.Core.0.1.0.nupkg`, and `AvaScope.Bridge.0.1.0.nupkg`.
- `2026-06-07`: `powershell -NoProfile -ExecutionPolicy Bypass -File .\eng\package-executables.ps1` created win-x64 and linux-x64 framework-dependent executable ZIPs.
- `2026-06-07`: `powershell -NoProfile -ExecutionPolicy Bypass -File .\eng\verify-artifacts.ps1` verified 5 release artifacts and wrote `artifacts\release-manifest.json`.
- `2026-06-07`: Release artifact list confirmed three NuGet packages and two executable ZIPs; `samples\AvaScope.GettingStartedApp` is not part of the manifest.
- `2026-06-07`: `git check-ignore -v` confirmed release manifest, packages, executable ZIP, and sample preview PNG outputs are ignored under `artifacts/`.
- `2026-06-07`: Packaged win-x64 CLI preview smoke passed against `samples\AvaScope.GettingStartedApp` and rendered `artifacts\samples\getting-started-preview-packaged.png`.
- `2026-06-07`: `dotnet build AvaScope.slnx` passed with 0 warnings and 0 errors after M48 preview failure diagnostics details.
- `2026-06-07`: `dotnet test AvaScope.slnx --no-build --filter FullyQualifiedName~ToolResultFailureSerializesOptionalErrorDetails` passed with 1 test.
- `2026-06-07`: `dotnet test AvaScope.slnx --no-build --filter FullyQualifiedName~PreviewHostReturnsStructuredErrorWhenProjectBuildFails` passed with 1 test.
- `2026-06-07`: `dotnet test AvaScope.slnx --no-build --filter FullyQualifiedName~PreviewCommandPreservesPreviewFailureDetails` passed with 1 test.
- `2026-06-07`: `dotnet test AvaScope.slnx --no-build` passed with 171 tests after M48 preview failure diagnostics details.
- `2026-06-07`: `dotnet build AvaScope.slnx` passed with 0 warnings and 0 errors after M49 runtime safety boundary implementation.
- `2026-06-07`: `dotnet test AvaScope.slnx --no-build --filter "FullyQualifiedName~BridgeSessionManifest|FullyQualifiedName~ActivateCreatesAndDeactivateRemovesLocalSessionManifest|FullyQualifiedName~LocalPipeServerRespondsToHealthRequest|FullyQualifiedName~DiagnosticsReportsInvalidAndStaleManifestsWithoutThrowing"` passed with 5 tests.
- `2026-06-07`: `dotnet test AvaScope.slnx --no-build` passed with 172 tests after M49 runtime safety boundary implementation.
- `2026-06-07`: `dotnet build AvaScope.slnx` passed with 0 warnings and 0 errors during M50 final validation.
- `2026-06-07`: `dotnet test AvaScope.slnx --no-build` passed with 172 tests during M50 final validation.
- `2026-06-07`: `dotnet build AvaScope.slnx -c Release` passed with 0 warnings and 0 errors during M50 final validation.
- `2026-06-07`: `dotnet test AvaScope.slnx -c Release --no-build` passed with 172 tests during M50 final validation.
- `2026-06-07`: `dotnet pack` for `AvaScope.Protocol`, `AvaScope.Core`, and `AvaScope.Bridge` passed; `verify-artifacts.ps1` confirmed all three `0.1.0` NuGet packages.
- `2026-06-07`: `powershell -NoProfile -ExecutionPolicy Bypass -File .\eng\package-executables.ps1` created win-x64 and linux-x64 framework-dependent executable ZIPs during M50 final validation.
- `2026-06-07`: `powershell -NoProfile -ExecutionPolicy Bypass -File .\eng\verify-artifacts.ps1` verified 5 release artifacts and wrote `artifacts\release-manifest.json`.
- `2026-06-07`: Source CLI getting-started preview smoke passed and rendered `artifacts\samples\getting-started-preview.png` at 720x420.
- `2026-06-07`: Packaged win-x64 CLI getting-started preview smoke passed and rendered `artifacts\samples\getting-started-preview-packaged.png` at 720x420.
- `2026-06-07`: `git check-ignore -v` confirmed release manifest, package, executable ZIP, and sample preview artifact outputs are ignored under `artifacts/`.

## Milestones

### M0 Project Foundation

- Status: `Done`
- Goal: create a clean .NET solution foundation for Avalonia 12-oriented development.
- Deliverables: solution structure, source/test folders, shared build settings, test project, local validation command list.
- Acceptance Criteria:
  - Solution builds with `net10.0`.
  - Test project is included in the solution.
  - Repository ignores generated build artifacts.
  - Local validation commands are documented and pass.
- Validation:
  - `dotnet build AvaScope.slnx`
  - `dotnet test AvaScope.slnx`
  - `git status --short`

### M1 Protocol Contracts

- Status: `Done`
- Goal: define stable, transport-neutral request/response contracts.
- Deliverables: session identifiers, protocol version model, core tool result shapes, JSON serialization tests.
- Acceptance Criteria:
  - Protocol models do not depend on Avalonia runtime types or MCP SDK types.
  - Models serialize and deserialize with stable property names.
  - Protocol versioning is explicit.
- Validation:
  - `dotnet test AvaScope.slnx --filter Protocol`

### M2 Core Session Model

- Status: `Done`
- Goal: implement reusable session lifecycle behavior outside MCP.
- Deliverables: session registry, session IDs, lifecycle state, error model, unit tests.
- Acceptance Criteria:
  - Sessions can be created, listed, inspected, and closed through core APIs.
  - Invalid session access returns structured errors.
  - Core APIs remain transport-neutral.
- Validation:
  - `dotnet test AvaScope.slnx --filter Core`

### M3 Minimal MCP Adapter

- Status: `Done`
- Goal: expose the first MCP surface as a thin adapter over protocol/core.
- Deliverables: stdio MCP server, health/version tool, `list_sessions` tool.
- Acceptance Criteria:
  - MCP adapter contains no core session state that belongs in `AvaScope.Core`.
  - Tool results map from protocol/core models.
  - Server starts locally over stdio.
- Validation:
  - `dotnet build AvaScope.slnx`
  - MCP smoke test for health/version and `list_sessions`

### M4 Opt-in Bridge MVP

- Status: `Done`
- Goal: provide an opt-in Avalonia 12 bridge package for runtime inspection.
- Deliverables: bridge activation API, local-only transport boundary, top-level/window discovery.
- Acceptance Criteria:
  - Bridge is disabled unless explicitly activated by the host app.
  - Top-level access runs on `Dispatcher.UIThread`.
  - Bridge does not require private Avalonia APIs.
- Validation:
  - `dotnet test AvaScope.slnx`
  - sample app bridge smoke test

### M5 Runtime Screenshot Slice

- Status: `Done`
- Goal: capture screenshots from a running bridged Avalonia app.
- Deliverables: attach flow, screenshot request/response, generated image file output, sample validation.
- Progress:
  - Done: in-process bridge screenshot capture for registered top-levels with PNG file output and structured success/error results.
  - Done: bridge activation now writes a local session manifest and starts a local-only named-pipe IPC server.
  - Done: bridge IPC protocol models cover request, response, method names, and session manifest JSON shape.
  - Done: bridge named-pipe health request is covered by a smoke test.
  - Done: reusable `LocalBridgeClient` discovers live bridge session manifests and calls the bridge pipe without Avalonia or MCP dependencies.
  - Done: MCP adapter exposes `attach_to_app`, `list_top_levels`, and `screenshot` as thin tool methods over `LocalBridgeClient`.
  - Done: positive attach validation covers manifest discovery plus pipe `health`; negative tool validation covers invalid and missing-session paths.
  - Done: positive `list_top_levels` and `screenshot` validation covers MCP tool -> Core client -> named pipe -> Bridge -> Avalonia UI thread -> PNG output.
- Acceptance Criteria:
  - Screenshot output path is returned as structured data.
  - Failed capture returns a structured diagnostic error.
  - Capture behavior is covered by an integration test or documented manual validation.
- Validation:
  - screenshot smoke test against sample app
  - output file existence and non-empty image validation
  - local IPC health smoke test
  - MCP attach smoke test through local bridge manifest and pipe health
  - MCP/Core/pipe screenshot smoke test against a headless Avalonia window

### M6 Tree Inspection Slice

- Status: `Done`
- Goal: expose visual and logical tree inspection with stable node identity.
- Deliverables: tree serialization, depth limits, node metadata, basic find behavior.
- Progress:
  - Done: protocol DTOs for tree kind, node bounds, node summaries, and tree responses.
  - Done: bridge visual/logical tree serialization using public Avalonia traversal APIs.
  - Done: stable node ids within a session based on runtime object identity.
  - Done: depth limits with default bounded output and explicit max depth input.
  - Done: MCP/Core/named-pipe `visual_tree` and `logical_tree` tool path with headless validation.
  - Done: `find_nodes` filters by type, name, automation id, and text, returning matched nodes with root-to-node path ids.
- Acceptance Criteria:
  - Tree results are bounded by default.
  - Node IDs are stable within a session.
  - Find supports at least type, name, automation id, and text where available.
- Validation:
  - tree serialization unit tests
  - sample app visual/logical tree integration test

### M7 Input Slice

- Status: `Done`
- Goal: send basic local-only input to a running bridged Avalonia app.
- Deliverables: click, pointer move, key text commands, safety checks.
- Progress:
  - Done: protocol `InputResponse`, `InputActions`, and IPC request fields for local input.
  - Done: MCP/Core/named-pipe `input` tool path.
  - Done: Button click MVP via hit-test and routed `Button.ClickEvent`.
  - Done: key text MVP for a focused `TextBox`.
  - Done: pointer move raises a public Avalonia 12 routed `PointerMovedEvent` on the hit-tested input target.
  - Done: unsupported input actions return structured diagnostics through the MCP/Core/named-pipe path.
- Acceptance Criteria:
  - Input targets must resolve to an active local session.
  - Unsupported input returns structured diagnostics.
  - Commands execute on the correct UI/input path for Avalonia 12.
- Validation:
  - input smoke test against sample app
  - negative tests for invalid session and unsupported input

### M8 Preview Host Slice

- Status: `Done`
- Goal: render a `.axaml` view from a project in an isolated preview process.
- Deliverables: preview host process, project/view selection, headless Skia rendering, basic variants.
- Progress:
  - Done: `AvaScope.PreviewHost` console process entrypoint accepts a JSON `PreviewRequest` file and writes a structured `ToolResult<PreviewResponse>` to stdout.
  - Done: `PreviewRequest` and `PreviewResponse` protocol DTOs cover output path, width, height, DPI, theme variant, culture, design-data type, project path, and view path.
  - Done: headless Skia render smoke path loads a standalone `.axaml` control with the official Avalonia runtime XAML loader and writes a PNG file.
  - Done: process-level smoke test validates child process isolation, structured JSON output, PNG existence, dimensions, and non-empty output.
  - Done: project-aware `.csproj` validation resolves relative view paths from the project directory and returns absolute project/view paths in the response.
  - Done: project build boundary runs `dotnet build` inside the preview child process before rendering and returns structured `preview_project_build_failed` diagnostics when the build fails.
  - Done: built project assembly loading resolves compiled Avalonia resource XAML through `avares://` and validates a code-behind-backed `UserControl` smoke render.
- Acceptance Criteria:
  - User application code runs outside the MCP server process.
  - Preview supports width, height, DPI, theme, and culture inputs.
  - Render output is returned as a file path with structured diagnostics.
- Validation:
  - preview smoke test against sample Avalonia 12 project
  - output file existence and non-empty image validation

### M9 Preview Adapter Integration

- Status: `Done`
- Goal: expose preview rendering through adapter surfaces without moving preview execution into MCP.
- Deliverables: reusable Core preview host client, MCP `preview_axaml` tool, structured process diagnostics.
- Progress:
  - Done: Core `PreviewHostClient` writes `PreviewRequest` JSON, launches the preview host child process, parses `ToolResult<PreviewResponse>`, and maps structured errors.
  - Done: MCP `preview_axaml` tool is a thin adapter over `PreviewHostClient`.
  - Done: MCP stdio tool list includes `preview_axaml`.
- Acceptance Criteria:
  - Core can launch `AvaScope.PreviewHost` as a child process and parse structured preview results.
  - MCP exposes preview rendering as a thin adapter over the Core preview client.
  - Preview failures return structured diagnostics without leaking user code into the MCP server process.
- Validation:
  - Core preview client process smoke test
  - MCP `preview_axaml` smoke test with PNG output validation

### M10 CLI Integration

- Status: `Done`
- Goal: provide a local `avascope` command for developer workflows.
- Deliverables: CLI project, preview command, MCP server command handoff or documented invocation path.
- Progress:
  - Done: `AvaScope.Cli` builds as `avascope`.
  - Done: `avascope preview <project.csproj> --view <view.axaml> --out <preview.png> --width <w> --height <h> [--dpi <dpi>] [--theme light|dark] [--culture <culture>] [--design-data-type <type>]` renders through `PreviewHostClient`.
  - Done: `avascope mcp` starts the MCP server assembly colocated with the CLI output.
  - Done: CLI invalid arguments return non-zero exit codes and structured JSON errors.
- Acceptance Criteria:
  - CLI can render a preview through `PreviewHostClient` without loading user project code in the CLI process.
  - CLI returns non-zero exit codes and concise structured errors for invalid requests.
  - CLI project builds with the solution and has focused process-level tests.
- Validation:
  - CLI preview smoke test with PNG output validation
  - CLI invalid argument/error test

### M11 Documentation and Release Readiness

- Status: `Done`
- Goal: document the current usable workflows and harden local validation for handoff.
- Deliverables: README usage guide, architecture/safety summary, validation command checklist, current limitations.
- Progress:
  - Done: README documents project layout, build/test commands, CLI usage, MCP tools, bridge activation, preview behavior, safety boundaries, and current limitations.
  - Done: validation guide includes PreviewHost and CLI targeted test commands.
- Acceptance Criteria:
  - A new agent or developer can run MCP, CLI preview, and bridge workflows from documented commands.
  - Documentation states the current isolation and local-only safety boundaries.
  - Documentation does not overclaim unsupported preview/resource/input behavior.
- Validation:
  - Markdown tracking/status check
  - `dotnet build AvaScope.slnx`
  - `dotnet test AvaScope.slnx --no-build`

### M12 Post-MVP Hardening

- Status: `Done`
- Goal: audit and close the highest-risk gaps in the first usable AvaScope workflow set.
- Deliverables: prioritized gap list, next vertical hardening slice, validation updates.
- Progress:
  - Done: gap audit created with P0/P1/P2 ranking.
  - Done: selected runtime `close_session` lifecycle support as the next vertical slice.
  - Done: safe bridge IPC close handshake returns a structured response before stopping the local bridge server.
  - Done: Core `LocalBridgeClient` and MCP expose `close_session`.
  - Done: stale manifest cleanup is validated through bridge and MCP/Core/pipe tests.
  - Done: PreviewHost temp-directory cleanup is retried to avoid Windows handle-release flakiness in full-suite validation.
- Acceptance Criteria:
  - Gaps are ranked by user impact and architectural risk.
  - The next slice is small enough to validate and commit independently.
  - Any new behavior remains covered by focused tests or explicit validation notes.
- Validation:
  - audit notes in this plan or a dedicated docs file
  - relevant targeted test command for the selected slice

### M13 Diagnostics Surface

- Status: `Done`
- Goal: provide the first aggregate diagnostics surface without coupling MCP schemas to bridge or preview internals.
- Deliverables: diagnostics protocol DTOs, local bridge diagnostics path, MCP `diagnostics` tool, focused tests.
- Progress:
  - Done: transport-neutral diagnostics response DTOs cover service health, manifest directory, bounded bridge session diagnostics, and structured issues.
  - Done: local bridge diagnostics reports manifest path, process id, named-pipe transport, pipe name, protocol health, stale manifests, invalid manifests, and unavailable IPC errors.
  - Done: MCP exposes `diagnostics` as a thin adapter over `LocalBridgeClient.DiagnosticsAsync`.
  - Done: missing/stale/unavailable states return structured diagnostic data instead of throwing.
  - Done: tests cover protocol serialization, Core missing/stale/invalid/limit behavior, MCP unavailable-state behavior, MCP bridge health, and stdio tool listing.
- Acceptance Criteria:
  - Diagnostics are bounded, structured, and transport-neutral.
  - Missing or stale sessions return structured diagnostic data rather than throwing.
  - MCP remains a thin adapter over Core/client behavior.
  - The first slice does not claim binding, layout, or resource diagnostics until those signals exist.
- Validation:
  - `dotnet build AvaScope.slnx`
  - `dotnet test AvaScope.slnx --no-build --filter Protocol`
  - `dotnet test AvaScope.slnx --no-build --filter Core`
  - `dotnet test AvaScope.slnx --no-build --filter Mcp`
  - `dotnet test AvaScope.slnx --no-build`

### M14 Preview App Resource Scope

- Status: `Done`
- Goal: improve project preview fidelity by loading compiled app-level resources before view rendering.
- Deliverables: preview host app resource discovery/loading, resource-backed render smoke test, structured diagnostics for missing/failed app resource loading.
- Progress:
  - Done: inspected Avalonia 12 resource/API docs and current compiled preview loading path.
  - Done: PreviewHost detects project-root `App.axaml`, loads it from the compiled project assembly through `avares://`, and copies top-level `Application.Resources` entries into the active PreviewHost application before loading the view.
  - Done: resource-backed smoke test validates a compiled project view resolving a `StaticResource` brush from `App.axaml`.
  - Done: missing `App.axaml` remains non-breaking for existing standalone and resource-free project previews.
  - Done: invalid/non-Application `App.axaml` returns structured `preview_render_failed` output.
- Acceptance Criteria:
  - App-level resources are loaded inside the isolated preview host process, not MCP or CLI.
  - A project preview can render a compiled view that depends on `App.axaml` resources.
  - Missing app resources do not break standalone or resource-free previews.
  - Failures return structured preview diagnostics without overclaiming full design-time parity.
- Validation:
  - `dotnet build AvaScope.slnx`
  - `dotnet test AvaScope.slnx --no-build --filter FullyQualifiedName~PreviewHost`
  - `dotnet test AvaScope.slnx --no-build`

### M15 Preview Diagnostics Expansion

- Status: `Done`
- Goal: report preview-host readiness in the existing diagnostics tool without launching user project code.
- Deliverables: preview host diagnostic DTO, Core diagnostics population, MCP diagnostics coverage, tests.
- Progress:
  - Done: protocol `PreviewHostDiagnostic` and process-mode constants cover preview host status, host assembly path, isolated child-process mode, service metadata, and structured errors.
  - Done: `PreviewHostClient.GetDiagnostics()` reports available and missing host states without launching the host or loading user projects.
  - Done: `DiagnosticsResponse` includes optional preview-host readiness alongside existing bridge diagnostics.
  - Done: MCP `diagnostics` composes `LocalBridgeClient` and `PreviewHostClient` while remaining a thin Core adapter.
  - Done: PreviewHost project builds now pass `--disable-build-servers` to reduce stale temp-project file locks in repeated smoke runs.
  - Done: tests cover protocol serialization, Core available/missing host readiness, aggregated diagnostics response, MCP diagnostics output, and full-suite stability.
- Acceptance Criteria:
  - Diagnostics can tell an agent whether the preview host executable is present before a preview request.
  - Preview diagnostics do not build or load user projects.
  - The existing `diagnostics` MCP tool remains a thin adapter over Core.
  - Missing preview host state is returned as structured diagnostic data.
- Validation:
  - `dotnet build AvaScope.slnx`
  - `dotnet test AvaScope.slnx --no-build --filter Protocol`
  - `dotnet test AvaScope.slnx --no-build --filter Core`
  - `dotnet test AvaScope.slnx --no-build --filter Mcp`
  - `dotnet test AvaScope.slnx --no-build`

### M16 Preview Reload Foundation

- Status: `Done`
- Goal: create the minimal persistent preview-session model required before implementing `reload`.
- Deliverables: preview session DTOs, Core preview session registry flow, MCP list/close integration for preview sessions where applicable, tests.
- Progress:
  - Done: defined preview sessions as metadata records containing the original request and latest render result, not live user-code processes.
  - Done: added protocol `PreviewSessionSummary` and `ListPreviewSessionsResponse` DTOs.
  - Done: added Core `PreviewSessionRegistry` with create/list/close behavior sharing lifecycle state through `SessionRegistry`.
  - Done: `create_preview_session` performs an initial isolated render through `AvaScope.PreviewHost` and stores success or failure as `lastRender`.
  - Done: `list_preview_sessions` and `close_preview_session` expose preview session lifecycle through MCP.
  - Done: `list_sessions` sees preview session lifecycle through the shared `SessionRegistry`.
  - Done: tests cover protocol serialization, Core successful and failed create flows, close errors, MCP create/list/close behavior, and stdio tool discovery.
- Acceptance Criteria:
  - Preview session state remains outside MCP-specific tool schemas.
  - User project code still only runs in `AvaScope.PreviewHost`.
  - The first foundation slice does not claim hot reload until a reload command is implemented and validated.
  - Existing one-shot `preview_axaml` behavior remains compatible.
- Validation:
  - `dotnet build AvaScope.slnx`
  - `dotnet test AvaScope.slnx --no-build --filter Protocol`
  - `dotnet test AvaScope.slnx --no-build --filter Core`
  - `dotnet test AvaScope.slnx --no-build --filter Mcp`
  - `dotnet test AvaScope.slnx --no-build`

### M17 Preview Reload MVP

- Status: `Done`
- Goal: re-render an existing preview session from its stored request.
- Deliverables: Core reload method, MCP `reload` tool for preview sessions, updated preview session metadata, tests.
- Progress:
  - Done: `PreviewSessionRegistry.ReloadAsync` reuses the stored `PreviewRequest`, re-renders through `AvaScope.PreviewHost`, updates `lastRender`, and preserves the existing session id.
  - Done: successful reload restores a failed preview session to active state through `SessionRegistry.MarkActive`.
  - Done: failed reload stores structured render failure metadata and marks the preview session failed.
  - Done: closed preview sessions return structured `session_closed` errors on reload.
  - Done: MCP exposes `reload` for preview session ids.
  - Done: tests cover successful reload, failed reload, closed-session rejection, MCP reload behavior, and stdio tool discovery.
- Acceptance Criteria:
  - Reload does not keep user code loaded in MCP.
  - Reload updates the existing preview session record rather than creating a new session.
  - Reload returns structured success or failure through the same preview session summary shape.
  - Unsupported runtime-session reload remains explicit and does not silently target bridge sessions.
- Validation:
  - `dotnet build AvaScope.slnx`
  - `dotnet test AvaScope.slnx --no-build --filter Protocol`
  - `dotnet test AvaScope.slnx --no-build --filter Core`
  - `dotnet test AvaScope.slnx --no-build --filter Mcp`
  - `dotnet test AvaScope.slnx --no-build`

### M18 Input Press/Release Slice

- Status: `Done`
- Goal: add one focused runtime input primitive pair beyond move/click/text.
- Deliverables: protocol input action constants, bridge routed pointer press/release handling, Core/MCP path reuse, headless tests.
- Progress:
  - Done: verified Avalonia 12.0.4 public routed pointer press/release event construction against the official API/source shape.
  - Done: added stable `pointer_down` and `pointer_up` input action constants.
  - Done: bridge input raises `PointerPressedEventArgs` and `PointerReleasedEventArgs` on hit-tested `InputElement` targets with explicit coordinate validation.
  - Done: pointer release reuses the active pointer created by pointer down when available.
  - Done: headless MCP/Core/named-pipe validation covers pointer press/release and preserves existing move/click/text behavior.
- Acceptance Criteria:
  - Input remains local-only through an active bridge session.
  - Pointer press/release execute on the UI thread and target hit-tested input elements.
  - Unsupported or invalid input returns structured diagnostics.
  - Existing `click`, `pointer_move`, and `key_text` behavior remains compatible.
- Validation:
  - `dotnet build AvaScope.slnx`
  - `dotnet test AvaScope.slnx --no-build --filter Protocol`
  - `dotnet test AvaScope.slnx --no-build --filter Core`
  - `dotnet test AvaScope.slnx --no-build --filter Bridge`
  - `dotnet test AvaScope.slnx --no-build --filter Mcp`
  - `dotnet test AvaScope.slnx --no-build`

### M19 Runtime Reload Contract Slice

- Status: `Done`
- Goal: make runtime reload semantics explicit and safe now that preview reload has a working MVP.
- Deliverables: reload protocol decision, Core/MCP behavior for runtime sessions, tests, README/tracking updates.
- Progress:
  - Done: audited the current MCP `reload` response shape and kept preview `PreviewSessionSummary` success responses compatible.
  - Done: runtime bridge session ids now fall through to Core `LocalBridgeClient.ReloadRuntimeAsync` only when no preview session exists.
  - Done: active runtime bridge sessions are health-checked locally and return structured `runtime_reload_not_supported` diagnostics.
  - Done: unknown session ids preserve the existing preview `session_not_found` behavior when no runtime bridge matches.
  - Done: PreviewHost smoke-test cleanup no longer fails successful tests on transient Windows temp-directory locks.
- Acceptance Criteria:
  - Runtime reload must not inject code, restart the user app, or claim hot reload.
  - Preview reload behavior remains compatible and covered.
  - Runtime reload checks an active local bridge session and returns structured success or structured unsupported/unavailable diagnostics.
  - MCP remains a thin adapter over Core/protocol behavior.
- Validation:
  - `dotnet build AvaScope.slnx`
  - `dotnet test AvaScope.slnx --no-build --filter Protocol`
  - `dotnet test AvaScope.slnx --no-build --filter Core`
  - `dotnet test AvaScope.slnx --no-build --filter Bridge`
  - `dotnet test AvaScope.slnx --no-build --filter Mcp`
  - `dotnet test AvaScope.slnx --no-build`

### M20 Packaging Metadata Slice

- Status: `Done`
- Goal: prepare local package artifacts without introducing publishing or CI yet.
- Deliverables: package metadata for packable projects, local output folder convention, pack validation commands, docs/tracking update.
- Progress:
  - Done: selected `AvaScope.Protocol`, `AvaScope.Core`, and `AvaScope.Bridge` as the first packable library projects.
  - Done: added shared version/authors/product/repository/readme metadata.
  - Done: added package ids, descriptions, and tags for the three library packages.
  - Done: marked `AvaScope.Mcp`, `AvaScope.Cli`, and `AvaScope.PreviewHost` explicitly non-packable for this slice.
  - Done: added ignored `artifacts/` package output convention.
  - Done: validated Release build, package creation, `.nuspec` metadata, readme inclusion, dependencies, and ignored artifacts.
- Acceptance Criteria:
  - `AvaScope.Bridge` can produce a local NuGet package with explicit package id, version, description, tags, and repository metadata.
  - `AvaScope.Cli` and `AvaScope.Mcp` packaging posture is explicit, even if final tool/executable packaging remains a later slice.
  - Package output goes to a local ignored artifact folder or an explicit temp output path.
  - No publishing credentials, feeds, or release automation are introduced.
- Validation:
  - `dotnet build AvaScope.slnx`
  - `dotnet pack src/AvaScope.Bridge/AvaScope.Bridge.csproj --no-build --output artifacts/packages`
  - packaging metadata inspection
  - `git status --short`

### M21 CI Validation Slice

- Status: `Done`
- Goal: make the documented local validation path run in CI without release publishing.
- Deliverables: GitHub Actions workflow, CI command list, documentation/tracking updates.
- Progress:
  - Done: added `.github/workflows/ci.yml` using official `actions/checkout@v6` and `actions/setup-dotnet@v5`.
  - Done: workflow runs on pushes and pull requests to `main` and `master`.
  - Done: workflow restores, builds Release, runs Release tests, and locally packs Protocol/Core/Bridge.
  - Done: workflow uses no secrets, package feed, release creation, or publishing step.
- Acceptance Criteria:
  - CI runs on pull requests and pushes to the main development branches.
  - CI uses `dotnet restore`, `dotnet build -c Release --no-restore`, `dotnet test -c Release --no-build`, and local `dotnet pack` commands.
  - Generated packages stay CI artifacts or workspace outputs only and are not published to a feed.
  - Workflow does not require secrets.
- Validation:
  - `dotnet build AvaScope.slnx`
  - `dotnet test AvaScope.slnx --no-build`
  - YAML/manual command inspection
  - `git status --short`

### M22 Executable Packaging Slice

- Status: `Done`
- Goal: define repeatable local artifacts for the `avascope` CLI, MCP server, and preview host without publishing.
- Deliverables: executable artifact decision, local publish/package command(s), docs/tracking updates, validation.
- Progress:
  - Done: selected a Windows framework-dependent zipped publish directory as the first executable artifact instead of a `dotnet tool`.
  - Done: added `eng/package-executables.ps1` to publish `AvaScope.Cli` into `artifacts\executables\avascope` and validate co-located CLI/MCP/PreviewHost/Core/Protocol files.
  - Done: added CI executable packaging validation without publishing credentials.
  - Done: documented local executable package validation and artifact shape in `README.md`.
  - Done: validated generated executable artifacts stay under ignored `artifacts/` output.
- Acceptance Criteria:
  - CLI/MCP/PreviewHost release artifact shape is explicit and documented.
  - Local artifact generation works without publishing credentials.
  - Artifact output is ignored and not committed.
  - Existing library package workflow remains compatible.
- Validation:
  - `dotnet build AvaScope.slnx -c Release`
  - `powershell -NoProfile -ExecutionPolicy Bypass -File .\eng\package-executables.ps1 -NoBuild`
  - artifact inspection
  - artifact smoke run for `avascope` and `avascope mcp`
  - `git status --short`

### M23 Release Artifact Hardening Slice

- Status: `Done`
- Goal: make local release artifacts easier for agents to verify, compare, and hand off.
- Deliverables: artifact manifest or checksum output, documented verification command, CI/local validation alignment, next release-distribution decision.
- Progress:
  - Done: added `eng/verify-artifacts.ps1` to validate required NuGet packages and executable ZIP artifacts.
  - Done: manifest output records schema version, product, version, artifact kind, name, relative path, byte size, and SHA-256 hash.
  - Done: CI now runs artifact verification after library pack and executable ZIP creation.
  - Done: README documents local artifact verification and the ignored `artifacts\release-manifest.json` output.
  - Done: selected RID-based framework-dependent ZIPs as the next executable distribution step before self-contained ZIPs or a `dotnet tool`.
- Acceptance Criteria:
  - Local package and executable artifacts have a repeatable verification command.
  - Verification output identifies artifact name, size, and checksum or equivalent integrity data.
  - README and CI describe/run the verification path.
  - Follow-up release distribution choice is recorded in the Decision Log.
- Validation:
  - `dotnet build AvaScope.slnx -c Release`
  - `dotnet pack src\AvaScope.Protocol\AvaScope.Protocol.csproj -c Release --no-build --output artifacts\packages`
  - `dotnet pack src\AvaScope.Core\AvaScope.Core.csproj -c Release --no-build --output artifacts\packages`
  - `dotnet pack src\AvaScope.Bridge\AvaScope.Bridge.csproj -c Release --no-build --output artifacts\packages`
  - `powershell -NoProfile -ExecutionPolicy Bypass -File .\eng\package-executables.ps1 -NoBuild`
  - `powershell -NoProfile -ExecutionPolicy Bypass -File .\eng\verify-artifacts.ps1`
  - manifest JSON inspection
  - `dotnet test AvaScope.slnx -c Release --no-build`
  - `git status --short`

### M24 Cross-platform Framework-dependent Artifact Slice

- Status: `Done`
- Goal: extend executable artifacts beyond the initial Windows-only ZIP while preserving co-located CLI/MCP/PreviewHost behavior.
- Deliverables: explicit RID/artifact strategy, script support for named framework-dependent runtime outputs, CI/local validation update, docs/tracking update.
- Progress:
  - Done: updated `eng/package-executables.ps1` to accept `RuntimeIdentifiers` and default to `win-x64` plus `linux-x64`.
  - Done: executable artifact names now include the RID: `avascope-<rid>-framework-dependent.zip`.
  - Done: packaging validates per-RID apphost names, co-located MCP/PreviewHost/Core/Protocol assemblies, and removes stale AvaScope executable ZIP outputs before producing the current set.
  - Done: updated `eng/verify-artifacts.ps1` to verify RID ZIPs and fail on unexpected AvaScope package/ZIP artifacts not covered by the manifest.
  - Done: CI and README use the RID-aware package and verify commands.
  - Done: validated Windows RID smoke behavior locally and verified Linux RID ZIP structure from the Windows runner environment.
- Acceptance Criteria:
  - Executable artifact naming includes target runtime/platform information.
  - Co-located `avascope`, `AvaScope.Mcp`, `AvaScope.PreviewHost`, `AvaScope.Core`, and `AvaScope.Protocol` validation still runs for each produced executable artifact.
  - Artifact verification manifest includes every produced package/ZIP artifact.
  - README and CI remain aligned with the local command path.
- Validation:
  - `dotnet build AvaScope.slnx -c Release`
  - `powershell -NoProfile -ExecutionPolicy Bypass -File .\eng\package-executables.ps1`
  - `powershell -NoProfile -ExecutionPolicy Bypass -File .\eng\verify-artifacts.ps1`
  - artifact inspection
  - Windows RID artifact smoke run for `avascope` and `avascope mcp`
  - `dotnet test AvaScope.slnx -c Release --no-build`
  - `git status --short`

### M25 Runtime Focus And Keyboard Input Slice

- Status: `Done`
- Goal: add explicit runtime focus targeting and basic keyboard key input support.
- Deliverables: protocol input actions for focus and key press/release or key tap, bridge implementation on `Dispatcher.UIThread`, MCP/Core path, focused tests, docs/tracking update.
- Progress:
  - Done: verified official Avalonia 12.0.4 source/API docs for public focus and routed key event APIs before implementation.
  - Done: added stable `focus`, `key_down`, and `key_up` input action constants.
  - Done: extended input IPC/Core/MCP shape with optional `targetNodeId`, `inputKey`, and `keyModifiers`.
  - Done: bridge focus action resolves visual/logical node ids or hit-tested coordinates and calls public `InputElement.Focus(...)` on the UI thread.
  - Done: bridge key actions raise public routed `KeyEventArgs` for focused input elements or explicit target node ids, with optional modifier parsing.
  - Done: added headless MCP/Core/pipe validation for focus by visual node id and routed key down/up delivery.
- Acceptance Criteria:
  - Runtime bridge can focus a target node by stable node id or hit-tested coordinates through a local-only input request.
  - Basic keyboard key input is exposed through Protocol/Core/MCP without private Avalonia hooks.
  - Unsupported or invalid targets return structured diagnostics.
  - Focus/key behavior is covered by headless bridge tests or an explicit API limitation note.
- Validation:
  - official Avalonia 12.0.4 API/source check for focus and keyboard input APIs
  - `dotnet build AvaScope.slnx`
  - `dotnet test AvaScope.slnx --no-build --filter Protocol`
  - `dotnet test AvaScope.slnx --no-build --filter Core`
  - `dotnet test AvaScope.slnx --no-build --filter Bridge`
  - `dotnet test AvaScope.slnx --no-build --filter Mcp`
  - `dotnet test AvaScope.slnx --no-build`
  - `git status --short`

### M26 Inspect Node Detail Slice

- Status: `Done`
- Goal: add the missing `inspect_node` vertical slice for runtime bridge sessions.
- Deliverables: protocol inspect-node response, bridge node lookup by stable node id, Core client method, MCP `inspect_node` tool, focused tests, docs/tracking update.
- Progress:
  - Done: added transport-neutral `InspectNodeResponse` with bounded node details and `childCount`.
  - Done: added bridge IPC `inspect_node` method and request `nodeId`.
  - Done: added Core `InspectNodeAsync` and MCP `inspect_node` tool.
  - Done: bridge lookup resolves visual/logical node ids using the same session-local stable ids as tree/find results.
  - Done: added structured `invalid_inspect_request` and `node_not_found` diagnostics.
  - Done: added protocol, core, MCP, and headless bridge tests for success and invalid paths.
- Acceptance Criteria:
  - MCP exposes `inspect_node` for runtime bridge sessions.
  - Node lookup works by stable visual/logical node id returned by tree/find responses.
  - Response includes bounded structured details already safe to expose: node id, tree kind, type, name, automation id, text, bounds, classes, and child count.
  - Protocol/Core/Bridge/MCP tests cover success and invalid node id paths.
- Validation:
  - `dotnet build AvaScope.slnx`
  - `dotnet test AvaScope.slnx --no-build --filter Protocol`
  - `dotnet test AvaScope.slnx --no-build --filter Core`
  - `dotnet test AvaScope.slnx --no-build --filter Bridge`
  - `dotnet test AvaScope.slnx --no-build --filter Mcp`
  - `dotnet test AvaScope.slnx --no-build`
  - `git status --short`

### M27 CLI Runtime Bridge Workflow Slice

- Status: `Done`
- Goal: expose the core runtime bridge workflow through the `avascope` CLI beyond `mcp` and `preview`.
- Deliverables: CLI command shape for local runtime bridge sessions, implementation over `LocalBridgeClient`, process/argument smoke tests, docs/tracking update.
- Progress:
  - Done: audited current CLI parser and process smoke test pattern.
  - Done: added `avascope attach [--process <pid>] [--session <session-id>]` over `LocalBridgeClient.AttachToAppAsync`.
  - Done: CLI attach writes structured `ToolResult<AttachToAppResponse>` JSON on success or bridge failure.
  - Done: invalid CLI arguments still return structured `invalid_cli_arguments` failures and exit code 2.
  - Done: added CLI smoke tests for deterministic no-session failure and invalid process id.
  - Done: README documents CLI attach usage.
- Acceptance Criteria:
  - CLI exposes at least one usable runtime bridge workflow over `LocalBridgeClient`.
  - Invalid CLI arguments return structured errors.
  - CLI tests cover success or deterministic no-session failure paths.
  - README documents the command shape and current limitations.
- Validation:
  - `dotnet build AvaScope.slnx`
  - `dotnet test AvaScope.slnx --no-build --filter FullyQualifiedName~Cli`
  - `dotnet test AvaScope.slnx --no-build`
  - `dotnet test AvaScope.slnx --no-build --filter Core`
  - `dotnet test AvaScope.slnx --no-build`
  - `git status --short`

### M28 CLI Runtime Top-level And Screenshot Slice

- Status: `Done`
- Goal: extend CLI runtime support from attach to top-level listing and screenshot capture.
- Deliverables: `list-top-levels` and/or `screenshot` CLI commands over `LocalBridgeClient`, structured JSON output, argument validation tests, README/tracking update.
- Progress:
  - Done: added `avascope list-top-levels --session <session-id>` over `LocalBridgeClient.ListTopLevelsAsync`.
  - Done: added `avascope screenshot --session <session-id> --top-level <top-level-id> --out <screenshot.png>` over `LocalBridgeClient.CaptureScreenshotAsync`.
  - Done: kept commands thin over Core and avoided duplicating bridge IPC behavior in CLI.
  - Done: added no-session and missing-argument tests for both commands.
  - Done: added fake bridge named-pipe success tests for `list-top-levels` and `screenshot`.
  - Done: README documents the new runtime CLI commands.
- Acceptance Criteria:
  - CLI can list top-levels for an attached runtime bridge session id.
  - CLI argument failures are structured and deterministic.
  - Tests cover invalid arguments and no-session failure; success may be covered through headless bridge if reliable from a child CLI process.
  - README documents command shape.
- Validation:
  - `dotnet build AvaScope.slnx`
  - `dotnet test AvaScope.slnx --no-build --filter FullyQualifiedName~Cli`

### M29 CLI Runtime Tree Inspection Slice

- Status: `Done`
- Goal: expose bounded runtime tree inspection from the `avascope` CLI.
- Deliverables: `visual-tree` and `logical-tree` CLI commands over `LocalBridgeClient`, optional `inspect-node` CLI command if the slice remains small, structured JSON output, argument validation tests, README/tracking update.
- Progress:
  - Done: added `visual-tree --session <session-id> --top-level <top-level-id> [--max-depth <n>]`.
  - Done: added `logical-tree --session <session-id> --top-level <top-level-id> [--max-depth <n>]`.
  - Done: added shared non-negative `max-depth` parsing for runtime tree commands.
  - Done: added fake bridge named-pipe success tests for both tree commands.
  - Done: added missing session, missing top-level, invalid `max-depth`, and no-session failure tests.
  - Done: README documents the new runtime tree CLI commands.
  - Done: deferred `inspect-node` to M30 to keep the slice small and independently trackable.
- Acceptance Criteria:
  - CLI can request bounded visual and logical trees for an attached runtime bridge session id.
  - CLI rejects invalid `max-depth`, missing session, and missing top-level arguments deterministically.
  - Tests cover fake bridge success paths and structured failure paths.
  - README documents command shape.
- Validation:
  - `dotnet build AvaScope.slnx`
  - `dotnet test AvaScope.slnx --no-build --filter FullyQualifiedName~Cli`
  - `dotnet test AvaScope.slnx --no-build`

### M30 CLI Runtime Node Detail Slice

- Status: `Done`
- Goal: expose single-node runtime details from the `avascope` CLI.
- Deliverables: `inspect-node` CLI command over `LocalBridgeClient.InspectNodeAsync`, explicit tree-kind option, structured JSON output, argument validation tests, README/tracking update.
- Progress:
  - Done: added `inspect-node --session <session-id> --top-level <top-level-id> --node <node-id> [--tree-kind visual|logical]`.
  - Done: defaulted omitted tree kind to `visual`.
  - Done: validated tree kind values before calling Core.
  - Done: added fake bridge success tests for visual, logical, and default tree-kind paths.
  - Done: added no-session, missing required argument, and invalid tree-kind tests.
  - Done: README documents the command shape.
- Acceptance Criteria:
  - CLI can inspect a single visual or logical node id for an attached runtime bridge session.
  - CLI rejects missing session, top-level, node id, and unsupported tree kind deterministically.
  - Tests cover fake bridge success paths and structured failure paths.
  - README documents command shape.
- Validation:
  - `dotnet build AvaScope.slnx`
  - `dotnet test AvaScope.slnx --no-build --filter FullyQualifiedName~Cli`
  - `dotnet test AvaScope.slnx --no-build`

### M31 CLI Runtime Find Nodes Slice

- Status: `Done`
- Goal: expose runtime node search from the `avascope` CLI.
- Deliverables: `find-nodes` CLI command over `LocalBridgeClient.FindNodesAsync`, filter argument validation, optional depth/result limits, structured JSON output, README/tracking update.
- Progress:
  - Done: added `find-nodes --session <session-id> --top-level <top-level-id> [--tree-kind visual|logical] [--type <type>] [--name <name>] [--automation-id <id>] [--text <text>] [--max-depth <n>] [--max-results <n>]`.
  - Done: required at least one filter before calling Core.
  - Done: added optional non-negative `max-depth` and positive `max-results` validation.
  - Done: added fake bridge named-pipe success test covering type, name, automation id, text, depth, and result limit request fields.
  - Done: added no-session, missing-filter, invalid limit, and invalid tree-kind tests.
  - Done: README documents command shape.
- Acceptance Criteria:
  - CLI can find runtime nodes by at least type, name, automation id, or text.
  - CLI rejects missing session/top-level, unsupported tree kind, invalid limits, and missing filters deterministically.
  - Tests cover fake bridge success paths and structured failure paths.
  - README documents command shape.
- Validation:
  - `dotnet build AvaScope.slnx`
  - `dotnet test AvaScope.slnx --no-build --filter FullyQualifiedName~Cli`
  - `dotnet test AvaScope.slnx --no-build`

### M32 CLI Runtime Input Slice

- Status: `Done`
- Goal: expose local-only runtime input commands from the `avascope` CLI.
- Deliverables: `input` CLI command over `LocalBridgeClient.InputAsync`, action-specific argument validation, structured JSON output, README/tracking update.
- Progress:
  - Done: added `input --session <session-id> --top-level <top-level-id> --action <action>` with optional coordinates, text, node id, key, and modifiers.
  - Done: validated supported action names against existing protocol actions before calling Core.
  - Done: added action-specific validation for coordinates, focus targets, text input, and key input.
  - Done: added fake bridge named-pipe success tests for click, key text, key down, and focus.
  - Done: added no-session, unsupported action, invalid coordinate, and missing action-specific argument tests.
  - Done: README documents command shape and local-only scope.
- Acceptance Criteria:
  - CLI can send at least pointer move/click, focus, key text, key down, and key up actions supported by the bridge.
  - CLI rejects missing session/top-level/action, unsupported actions, invalid numeric coordinates, and missing action-specific parameters deterministically.
  - Tests cover fake bridge success paths and structured failure paths.
  - README documents command shape and local-only scope.
- Validation:
  - `dotnet build AvaScope.slnx`
  - `dotnet test AvaScope.slnx --no-build --filter FullyQualifiedName~Cli`
  - `dotnet test AvaScope.slnx --no-build`

### M33 CLI Runtime Close Session Slice

- Status: `Done`
- Goal: expose runtime bridge session closure from the `avascope` CLI.
- Deliverables: `close-session` CLI command over `LocalBridgeClient.CloseSessionAsync`, structured JSON output, deterministic argument validation, README/tracking update.
- Progress:
  - Done: added `close-session --session <session-id>`.
  - Done: added fake bridge success, no-session failure, and missing-session argument tests.
  - Done: README documents that the command closes an active local bridge session.
- Acceptance Criteria:
  - CLI can request closure of an attached runtime bridge session id.
  - CLI rejects missing or invalid session ids deterministically.
  - Tests cover fake bridge success and structured failure paths.
  - README documents command shape.
- Validation:
  - `dotnet build AvaScope.slnx`
  - `dotnet test AvaScope.slnx --no-build --filter FullyQualifiedName~Cli`
  - `dotnet test AvaScope.slnx --no-build`

### M34 CLI Runtime Diagnostics Slice

- Status: `Done`
- Goal: expose bounded runtime diagnostics from the `avascope` CLI.
- Deliverables: `diagnostics` CLI command over `LocalBridgeClient.DiagnosticsAsync`, process/session filters, max-session validation, structured JSON output, README/tracking update.
- Progress:
  - Done: added `diagnostics [--process <pid>] [--session <session-id>] [--max-sessions <n>]`.
  - Done: included preview host diagnostics from `PreviewHostClient.GetDiagnostics()`.
  - Done: added fake bridge health success test.
  - Done: added no-match issue, invalid process id, and invalid max-session tests.
  - Done: README documents command shape.
- Acceptance Criteria:
  - CLI can return runtime diagnostics with optional process/session filters.
  - CLI rejects invalid process ids, session ids, and max-session values deterministically.
  - Tests cover success and structured issue paths without requiring a live Avalonia bridge.
  - README documents command shape.
- Validation:
  - `dotnet build AvaScope.slnx`
  - `dotnet test AvaScope.slnx --no-build --filter FullyQualifiedName~Cli`
  - `dotnet test AvaScope.slnx --no-build`

### M35 CLI Runtime Reload Slice

- Status: `Done`
- Goal: expose runtime/preview reload checks from the `avascope` CLI.
- Deliverables: `reload` CLI command aligned with MCP reload behavior, runtime unsupported diagnostics, preview session reload path if reusable from CLI, structured JSON output, README/tracking update.
- Progress:
  - Done: inspected reload behavior and kept the CLI slice to runtime bridge checks because CLI preview renders are currently one-shot and not persisted across CLI processes.
  - Done: added `reload --session <session-id>` with deterministic session id validation.
  - Done: added fake bridge health test for explicit `runtime_reload_not_supported`.
  - Done: added no-session and missing-session tests.
  - Done: README documents current CLI reload behavior and limitation.
- Acceptance Criteria:
  - CLI can check an active runtime bridge session and return explicit `runtime_reload_not_supported`.
  - CLI returns structured errors for missing/invalid session ids.
  - Tests cover deterministic runtime or no-session paths.
  - README documents current reload behavior and limitations.
- Validation:
  - `dotnet build AvaScope.slnx`
  - `dotnet test AvaScope.slnx --no-build --filter FullyQualifiedName~Cli`
  - `dotnet test AvaScope.slnx --no-build`

### M36 Reload And Hot Preview Foundation Slice

- Status: `Done`
- Goal: move beyond explicit runtime reload unsupported checks toward a durable reload foundation.
- Deliverables: repository-backed reload gap audit, smallest durable preview-session persistence or reload-state foundation, tests, README/tracking update.
- Progress:
  - Done: inspected `PreviewSessionRegistry`, MCP reload behavior, CLI one-shot preview behavior, and current storage boundaries.
  - Done: added Core `PreviewSessionStore` for per-session JSON records under the local AvaScope temp preview-session store.
  - Done: added `SessionRegistry.Restore` and registry startup restore for persisted preview session records.
  - Done: wired MCP host `PreviewSessionRegistry` to the default persistent store while leaving existing unit-test constructors in-memory unless a store is supplied.
  - Done: persisted create, reload, and close updates; write failures return structured `preview_session_store_failed` errors.
  - Done: added Core tests proving preview sessions and closed state restore across registry instances.
  - Done: README documents the durable MCP preview-session boundary and runtime reload limitation.
- Acceptance Criteria:
  - Non-obvious reload persistence decisions are recorded.
  - Existing runtime reload unsupported behavior remains explicit and tested.
  - Any new preview reload foundation is validated without broadening to full hot reload.
  - README documents the supported reload boundary.
- Validation:
  - `dotnet build AvaScope.slnx`
  - targeted Core/MCP/CLI tests as applicable
  - `dotnet test AvaScope.slnx --no-build`
  - `dotnet test AvaScope.slnx --no-build --filter Core`
  - `git status --short`

### M37 Preview Resource And Style Scope Slice

- Status: `Done`
- Goal: improve preview-host resource/style parity after durable preview-session persistence.
- Deliverables: focused preview-host resource/style loading improvement, tests with a tiny sample project/view, README/tracking update.
- Progress:
  - Done: audited current `AvaScope.PreviewHost` app resource, style, merged dictionary, and theme handling.
  - Done: identified missing `Application.Styles` application as the smallest high-value gap after top-level resources.
  - Done: changed App.axaml loading to instantiate the project `Application` type and call `Initialize()` when available, falling back to URI XAML loading.
  - Done: extracted project application styles and applied them to the preview window style scope.
  - Done: added pixel-validated PreviewHost smoke coverage proving an App.axaml style affects the rendered output.
  - Done: README documents `Application.Styles` support.
- Acceptance Criteria:
  - PreviewHost handles one additional real-world resource/style scenario beyond current top-level `App.axaml` resources.
  - The behavior remains isolated in `AvaScope.PreviewHost`; MCP/CLI do not load user project code.
  - Tests validate rendered output file and structured response for the new scenario.
  - README documents the supported boundary and remaining limitations.
- Validation:
  - `dotnet build AvaScope.slnx`
  - targeted PreviewHost tests
  - `dotnet test AvaScope.slnx --no-build`

### M38 Preview Resource Include Scope Slice

- Status: `Done`
- Goal: improve preview-host support for App.axaml resource/style includes.
- Deliverables: focused support for one compiled `ResourceInclude` or `StyleInclude` scenario, tests with tiny sample project/view, README/tracking update.
- Progress:
  - Done: audited compiled `ResourceInclude` behavior after project `Application.Initialize()`.
  - Done: added preview-host merge of project app resource `MergedDictionaries` into the host app resource scope.
  - Done: added pixel-validated PreviewHost smoke coverage proving a resource included by `ResourceInclude` affects the rendered output.
  - Done: README documents merged resource dictionary support from `App.axaml`.
- Acceptance Criteria:
  - PreviewHost validates at least one app-level include scenario from project `App.axaml`.
  - The behavior remains isolated in `AvaScope.PreviewHost`.
  - Tests validate rendered output file and structured response for the include scenario.
  - README documents supported boundary and remaining limitations.
- Validation:
  - `dotnet build AvaScope.slnx`
  - targeted PreviewHost tests
  - `dotnet test AvaScope.slnx --no-build`

### M39 Preview Theme Dictionary Variant Slice

- Status: `Done`
- Goal: improve preview-host theme-variant resource parity.
- Deliverables: focused support for one `ThemeDictionaries` scenario, tests with light/dark preview variants, README/tracking update.
- Progress:
  - Done: audited `ThemeDictionaries` behavior after project app resource merge and requested theme variant assignment.
  - Done: copied project app resource `ThemeDictionaries` into the host app resource scope.
  - Done: added pixel-validated PreviewHost smoke coverage for light/dark theme dictionary resources.
  - Done: README documents theme dictionary support from `App.axaml`.
- Acceptance Criteria:
  - PreviewHost resolves app-level theme dictionary resources for at least one requested theme variant.
  - The behavior remains isolated in `AvaScope.PreviewHost`.
  - Tests validate rendered output for the theme variant scenario.
  - README documents supported boundary and remaining limitations.
- Validation:
  - `dotnet build AvaScope.slnx`
  - targeted PreviewHost tests
  - `dotnet test AvaScope.slnx --no-build`

### M40 Preview Style Include Scope Slice

- Status: `Done`
- Goal: improve preview-host app style include parity.
- Deliverables: focused support for one compiled `StyleInclude` scenario, tests with tiny sample project/view, README/tracking update.
- Progress:
  - Done: audited current `StyleInclude` behavior after project `Application.Initialize()` and preview-window style transfer.
  - Done: confirmed no PreviewHost code change was required because compiled style includes are already transferred as app styles.
  - Done: added pixel-validated PreviewHost smoke coverage for a style loaded through `StyleInclude`.
  - Done: README and gap audit document style include support from `App.axaml`.
- Acceptance Criteria:
  - PreviewHost validates at least one app-level `StyleInclude` scenario from project `App.axaml`.
  - The behavior remains isolated in `AvaScope.PreviewHost`.
  - Tests validate rendered output for the style include scenario.
  - README documents supported boundary and remaining limitations.
- Validation:
  - `dotnet build AvaScope.slnx`
  - targeted PreviewHost tests
  - `dotnet test AvaScope.slnx --no-build`

### M41 Preview Culture Variant Contract Slice

- Status: `Done`
- Goal: add explicit culture selection to preview rendering.
- Deliverables: preview culture DTO field, CLI/MCP argument propagation, preview-host culture application, tiny culture-sensitive render test, README/tracking update.
- Progress:
  - Done: added nullable transport-neutral `culture` fields to preview request/response contracts.
  - Done: propagated preview culture through CLI `preview`, MCP `preview_axaml`, and MCP `create_preview_session`.
  - Done: applied requested culture inside the isolated preview host render boundary.
  - Done: added protocol, CLI, MCP, and pixel-validated PreviewHost coverage proving culture-sensitive output changes as requested.
  - Done: README and gap audit document culture variant support and remaining design-data limitations.
- Acceptance Criteria:
  - Preview requests can specify a culture without coupling protocol contracts to Avalonia runtime types.
  - PreviewHost applies the culture only inside the child render process.
  - CLI and MCP preview entrypoints propagate the culture field.
  - Tests validate protocol serialization and one culture-sensitive preview render.
- Validation:
  - `dotnet build AvaScope.slnx`
  - targeted Protocol/Core/MCP/CLI/PreviewHost tests as touched
  - `dotnet test AvaScope.slnx --no-build`

### M42 Preview Design Data Contract Audit Slice

- Status: `Done`
- Goal: define the next safe design-data preview boundary.
- Deliverables: design-data gap audit, smallest proposed contract, acceptance criteria for first implementation slice, README/tracking update.
- Progress:
  - Done: audited current preview data-context and design-data behavior.
  - Done: confirmed the current preview path does not set `DataContext` and has no design-data request field.
  - Done: selected project-owned public parameterless design-data type loading as the first implementation boundary.
  - Done: recorded non-goals: no JSON object injection, dependency injection, remote data, or long-lived design-data state in the first slice.
  - Done: README and gap audit describe the selected boundary.
- Acceptance Criteria:
  - The plan documents the selected design-data boundary and non-goals.
  - The decision keeps user code execution isolated in `AvaScope.PreviewHost`.
  - The next implementation slice has concrete tests and validation commands.
  - README/gap audit remain aligned with the selected boundary.
- Validation:
  - Markdown tracking/status review
  - `dotnet build AvaScope.slnx`
  - `git status --short`

### M43 Preview Design Data Type Slice

- Status: `Done`
- Goal: implement the first explicit project-owned design-data boundary.
- Deliverables: `designDataType` DTO field, CLI/MCP argument propagation, PreviewHost type instantiation and root `DataContext` assignment, typed-binding render test, README/tracking update.
- Progress:
  - Done: added nullable transport-neutral `designDataType` fields to preview request/response contracts.
  - Done: propagated `designDataType` through CLI `preview`, MCP `preview_axaml`, and MCP `create_preview_session`.
  - Done: instantiated the named project type inside `AvaScope.PreviewHost` and assigned it to the loaded root control `DataContext`.
  - Done: validated with a tiny project using `x:DataType` and `{CompiledBinding}` against the design-data type.
  - Done: added structured invalid design-data type diagnostics.
  - Done: README and gap audit document support and remaining non-goals.
- Acceptance Criteria:
  - Design data is available only for project-backed previews with a built project assembly.
  - The design-data type must be project-owned, concrete, and constructible with a public parameterless constructor.
  - The design-data object is created only inside the PreviewHost child process and is not persisted in MCP/Core.
  - Tests cover protocol serialization, CLI/MCP propagation, invalid design-data type diagnostics, and a positive typed-binding render.
- Validation:
  - `dotnet build AvaScope.slnx`
  - targeted Protocol/CLI/MCP/PreviewHost tests
  - `dotnet test AvaScope.slnx --no-build`

### M44 Preview App Startup Boundary Audit Slice

- Status: `Done`
- Goal: define the next safe App startup/lifecycle preview boundary.
- Deliverables: audit of current `Application.Initialize()` and lifetime behavior, selected startup/lifecycle boundary or explicit deferral, acceptance criteria for first implementation slice, README/tracking update.
- Progress:
  - Done: audited current PreviewHost app creation, resource merge, style transfer, design-data creation, view loading, and window construction order.
  - Done: checked official Avalonia application lifetime documentation before deciding the boundary.
  - Done: selected explicit deferral for running project app startup/lifecycle hooks such as `OnFrameworkInitializationCompleted`.
  - Done: recorded non-goals: no fake desktop lifetime, no project `MainWindow` creation, no automatic app startup services, no long-lived app process.
  - Done: selected app-level `DataTemplates` transfer as the next safer preview parity slice.
- Acceptance Criteria:
  - The plan documents whether startup/lifecycle orchestration will be implemented or deferred.
  - The decision keeps user app startup effects isolated in `AvaScope.PreviewHost`.
  - The next implementation slice has concrete tests and validation commands, or the gap is explicitly deferred.
  - README/gap audit remain aligned with the selected boundary.
- Validation:
  - Markdown tracking/status review
  - `dotnet build AvaScope.slnx`
  - `git status --short`

### M45 Preview App DataTemplates Scope Slice

- Status: `Done`
- Goal: improve preview-host app-level data-template parity without running app startup hooks.
- Deliverables: transfer project `Application.DataTemplates` into the PreviewHost render scope, tiny compiled data-template sample, README/tracking update.
- Progress:
  - Done: checked official Avalonia `Application.DataTemplates` and data-template resolution documentation.
  - Done: transferred project app-level data templates from `App.axaml` into the preview window `DataTemplates` scope.
  - Done: kept project app lifetime startup hooks deferred; the sample `OnFrameworkInitializationCompleted` would fail if invoked.
  - Done: added pixel render validation for a view whose content relies on an app-level data template.
  - Done: updated README and gap audit with the supported boundary.
- Acceptance Criteria:
  - PreviewHost applies app-level data templates from project `App.axaml` without running app lifetime startup hooks.
  - The behavior remains isolated in `AvaScope.PreviewHost`.
  - Tests validate rendered output for one app-level data-template scenario.
  - README/gap audit document the supported boundary and remaining startup limitations.
- Validation:
  - `dotnet build AvaScope.slnx`
  - targeted PreviewHost tests
  - `dotnet test AvaScope.slnx --no-build`

### M46 Getting Started Sample Slice

- Status: `Done`
- Goal: make the first external-developer workflow runnable from a repository sample.
- Deliverables: tiny Avalonia sample app, documented preview command, documented bridge activation/run command, validation notes.
- Progress:
  - Done: inspected solution/sample conventions and added a `/samples/` solution folder.
  - Done: added `samples/AvaScope.GettingStartedApp`, targeting `net10.0`, Avalonia 12.0.4, and local `AvaScope.Bridge`.
  - Done: added `App.axaml` resources/data templates, preview design data, a previewable `MainView`, and a runtime `MainWindow`.
  - Done: gated sample bridge activation behind `AVASCOPE_SAMPLE_BRIDGE=1` or `true`.
  - Done: documented sample preview and runtime bridge workflows in root README and sample README.
  - Done: fixed CLI preview relative project/output path normalization discovered by the documented sample command.
  - Done: validated the documented sample preview command, generated ignored PNG output, CLI regression test, and full suite.
- Acceptance Criteria:
  - A new developer can run a documented command against the sample and receive a preview PNG.
  - The sample demonstrates explicit opt-in bridge activation without enabling remote inspection.
  - The sample stays outside production packages unless intentionally referenced.
  - Documentation states the sample's purpose and current runtime/preview limitations.
- Validation:
  - `dotnet build AvaScope.slnx`
  - documented sample preview command
  - targeted sample or CLI validation if applicable
  - `dotnet test AvaScope.slnx --no-build`

### M47 Public Alpha Release Validation Refresh

- Status: `Done`
- Goal: revalidate release artifacts after the sample and CLI workflow stabilization changes.
- Deliverables: Release build/test validation, local NuGet package validation, executable ZIP validation, artifact manifest verification, README/tracking update if release commands need adjustment.
- Progress:
  - Done: Release build succeeded for the expanded solution including the sample.
  - Done: Release tests passed after the sample and CLI path normalization changes.
  - Done: local library packages were created for Protocol, Core, and Bridge.
  - Done: win-x64 and linux-x64 framework-dependent executable ZIPs were created.
  - Done: artifact verification wrote a 5-artifact manifest.
  - Done: confirmed sample app remains outside release artifact manifest.
  - Done: packaged win-x64 CLI preview smoke rendered the getting-started sample.
- Acceptance Criteria:
  - Release build succeeds for the full solution including the sample project.
  - Release tests pass.
  - Library packages and executable ZIP artifacts are created and verified.
  - Sample app remains outside package artifacts unless explicitly intended.
- Validation:
  - `dotnet build AvaScope.slnx -c Release`
  - `dotnet test AvaScope.slnx -c Release --no-build`
  - `dotnet pack` for packable libraries
  - `powershell -NoProfile -ExecutionPolicy Bypass -File .\eng\package-executables.ps1`
  - `powershell -NoProfile -ExecutionPolicy Bypass -File .\eng\verify-artifacts.ps1`

### M48 Preview Failure Diagnostics Detail Slice

- Status: `Done`
- Goal: make preview build/render failures easier for agents and users to diagnose.
- Deliverables: audit current PreviewHost/Core error propagation, structured preview failure context, tests for at least one build or render failure path, README/tracking update.
- Progress:
  - Done: audited current `PreviewHost`, `PreviewHostClient`, CLI, MCP, and preview-session error propagation.
  - Done: reused existing optional `ProtocolError.details` as the smallest compatible transport-neutral payload.
  - Done: added `CoreError.Details` and preserved details through Core, CLI, MCP, local bridge diagnostics conversion, and preview-session storage.
  - Done: added bounded PreviewHost build/render context including phase, paths, build exit code, timeout, command, and output tail where applicable.
  - Done: validated protocol serialization, direct PreviewHost build failure details, CLI preservation, and full suite.
- Acceptance Criteria:
  - Preview failure results expose bounded structured context beyond a single opaque message where practical.
  - Existing `ToolResult<PreviewResponse>` compatibility is preserved.
  - Diagnostics remain local and do not include unbounded build output.
  - Tests cover the new failure context.
- Validation:
  - `dotnet build AvaScope.slnx`
  - focused diagnostics/protocol/preview tests
  - `dotnet test AvaScope.slnx --no-build`

### M49 Runtime Safety Boundary Audit Slice

- Status: `Done`
- Goal: harden and document public-alpha runtime bridge safety boundaries.
- Deliverables: bridge activation/local transport audit, missing safety tests or docs, explicit non-goals for runtime control, tracking update.
- Progress:
  - Done: inspected bridge activation defaults, local manifest creation, named-pipe transport, and CLI/MCP runtime control surfaces.
  - Done: added explicit `transportScope: "local_only"` to bridge session manifests with backward-compatible missing-scope deserialization.
  - Done: created local named-pipe servers with `PipeOptions.CurrentUserOnly` in addition to asynchronous byte-mode operation.
  - Done: treated unsupported transport scopes as invalid manifests in diagnostics instead of attachable sessions.
  - Done: documented opt-in activation, local-only manifests, current-user named pipe access, no network listeners, and narrow runtime control in README/sample README.
  - Done: validated protocol, bridge, core diagnostics, and full suite.
- Acceptance Criteria:
  - Bridge remains opt-in and local-only by default.
  - Runtime input/control limitations are documented in public docs.
  - No unauthenticated remote transport is introduced.
  - Any discovered safety gap has either a fix or an explicit tracked deferral.
- Validation:
  - `dotnet build AvaScope.slnx`
  - focused Bridge/Core/CLI safety tests if code changes are needed
  - `dotnet test AvaScope.slnx --no-build`

### M50 Public Alpha Completion Audit Slice

- Status: `Done`
- Goal: verify whether the repository satisfies the full public-alpha objective.
- Deliverables: requirement-by-requirement completion audit, final gap fixes or explicit deferrals, final build/test/pack validation, clean committed worktree.
- Progress:
  - Done: derived public-alpha requirements from the active goal, README, development plan, and gap audit.
  - Done: inspected authoritative evidence for each requirement: code, tests, docs, release artifacts, sample output, and git state.
  - Done: recorded remaining non-blocking deferrals in `docs/PUBLIC_ALPHA_AUDIT.md`; no public-alpha blocker remains.
  - Done: ran final validation commands and updated tracking.
- Acceptance Criteria:
  - Every explicit public-alpha requirement has current evidence or a tracked non-blocking deferral.
  - Final validation includes build, test, release pack/executable verification, and getting-started sample preview smoke.
  - `docs/DEVELOPMENT_PLAN.md` and `docs/GAP_AUDIT.md` reflect the final status.
  - Worktree is clean after commit.
- Validation:
  - `dotnet build AvaScope.slnx`
  - `dotnet test AvaScope.slnx --no-build`
  - `dotnet build AvaScope.slnx -c Release`
  - `dotnet test AvaScope.slnx -c Release --no-build`
  - release pack/executable verify commands
  - documented sample preview command

### W1 Intake Ledgers

- Status: `In Progress`
- Goal: maintain sanitized holding areas for reported bugs and feature requests without treating intake records as authorization to implement fixes or features.
- Deliverables: bug report index, feature request index, per-record Markdown files, privacy validation command, development-plan tracking.
- Progress:
  - Done: created `docs/BUG_REPORTS.md` as the report index and storage rules.
  - Done: created `docs/bug-reports/` for sanitized per-bug records.
  - Done: recorded `BUG-0001` for the PreviewHost `Window` root rendering failure without starting the fix.
  - Done: recorded `BUG-0002` for ignored Avalonia design-time `DataContext` metadata without starting the fix.
  - Done: added `eng/validate-bug-reports.ps1` to reject local absolute paths, home-directory paths, emails, secret assignments, and the current local username in bug report files.
  - Done: expanded intake validation to scan feature request tickets in addition to bug reports.
  - Done: created `docs/FEATURE_REQUESTS.md` and `docs/feature-requests/` for sanitized feature ticket records.
  - Done: stored `FEAT-0001` through `FEAT-0007`, preserving the requested priority order for binding/resource diagnostics, layout warnings, and computed style/resource inspection.
  - Ongoing: store future reports and requests only after sanitizing personal data and paths.
- Acceptance Criteria:
  - Every stored report has a stable `BUG-####` id, status, fix status, reproduction summary, actual result, and expected result where available.
  - Every stored feature request has a stable `FEAT-####` id, priority, status, implementation status, user need, desired behavior, and acceptance criteria where available.
  - Intake files contain no personal local paths, usernames, emails, tokens, or secrets.
  - Intake records remain decoupled from implementation until the user explicitly asks for a fix or feature.
- Validation:
  - `powershell -NoProfile -ExecutionPolicy Bypass -File .\eng\validate-bug-reports.ps1`

### W2 PreviewHost Stored Bug Fixes

- Status: `Done`
- Goal: implement stored `BUG-0001` and `BUG-0002` after explicit user authorization.
- Deliverables: Window/TopLevel-root preview rendering, Avalonia design-time `DataContext` support, regression tests, bug ledger status updates, validation and commit tracking.
- Progress:
  - Done: audited PreviewHost render and AXAML loading flow.
  - Done: changed PreviewHost to render loaded `Window` roots directly while preserving the host-window path for normal `Control` roots.
  - Done: added preview source metadata parsing for `d:DataContext`, `d:DesignWidth`/`d:DesignHeight`, and `Design.Width`/`Design.Height`.
  - Done: applied `Design.DataContext` object elements and `d:DataContext="{x:Static ...}"` values as root `DataContext` when no explicit `designDataType` is supplied.
  - Done: made preview request width and height optional so CLI/MCP requests can use design-time dimension fallback.
  - Done: updated `docs/BUG_REPORTS.md` and both stored report files to `Fixed`.
- Acceptance Criteria:
  - Window-rooted AXAML previews render without being wrapped in another host window.
  - Existing UserControl-rooted previews continue to render.
  - `<Design.DataContext>...</Design.DataContext>` object elements are applied during preview rendering.
  - `d:DataContext` using `x:Static` design data is applied during preview rendering.
  - `d:DesignWidth`/`d:DesignHeight` and `Design.Width`/`Design.Height` are used when preview width and height are omitted.
  - Unsupported or invalid design-time data expressions return structured diagnostics instead of crashing.
  - Stored bug reports and the report index show implementation status.
- Validation:
  - `dotnet build AvaScope.slnx`
  - `dotnet test AvaScope.slnx --no-build --filter FullyQualifiedName~PreviewHost`
  - `dotnet test AvaScope.slnx --no-build`

### W3 Local Release Workflow

- Status: `Done`
- Goal: provide a single repeatable local Release command for testing AvaScope from packaged artifacts.
- Deliverables: local release orchestration script, packaged executable smoke validation, user-facing docs, development-plan tracking, commit.
- Progress:
  - Done: audited the existing release pack and artifact verification scripts.
  - Done: added `eng/create-local-release.ps1` as the single local release entrypoint.
  - Done: the script runs Release restore/build/test, packs libraries, packages executable artifacts, verifies the release manifest, and smoke-tests the packaged Windows CLI.
  - Done: README and validation docs now direct external project testing to the packaged Release executable.
  - Done: validated generated artifacts remain ignored by `.gitignore`.
- Acceptance Criteria:
  - A single command creates current Release artifacts from the repository root.
  - The command runs Release build/test by default before packaging.
  - The command reuses existing package, executable, and manifest verification scripts.
  - The command smoke-tests the packaged Windows `avascope.exe` path.
  - Documentation tells users to test external projects from the packaged Release executable.
- Validation:
  - `powershell -NoProfile -ExecutionPolicy Bypass -File .\eng\create-local-release.ps1`
  - `git status --short`

### W4 NuGet Publish Workflow

- Status: `Done`
- Goal: provide a repeatable manual NuGet publish path for the public AvaScope library packages.
- Deliverables: API-key based publish script, dry-run validation mode, optional manual GitHub Actions workflow, user-facing docs, development-plan tracking, validation, commit.
- Progress:
  - Done: audited existing package metadata and local Release artifact workflow.
  - Done: checked NuGet flat-container endpoints for `AvaScope.Protocol`, `AvaScope.Core`, and `AvaScope.Bridge`; all returned `404` before publishing.
  - Done: added `eng/publish-nuget.ps1` with dry-run validation, dependency-ordered package push, stale artifact rejection, and API-key masking for failures.
  - Done: added the manual `Publish NuGet` GitHub Actions workflow, defaulting to dry-run and using the `NUGET_API_KEY` secret only for actual publish runs.
  - Done: documented local and GitHub-hosted publishing in README and validation docs.
  - Done: validated the full local Release gate and NuGet publish dry-run.
- Acceptance Criteria:
  - Publishing uses existing Release package artifacts from `artifacts\packages`.
  - Publishing requires an API key from a parameter or environment variable and never stores the key in source.
  - A dry-run mode validates the exact package set and source without pushing.
  - The packages publish in dependency order: Protocol, Core, Bridge.
  - Documentation explains local and CI-secret based publish commands.
- Validation:
  - `powershell -NoProfile -ExecutionPolicy Bypass -File .\eng\create-local-release.ps1`
  - `powershell -NoProfile -ExecutionPolicy Bypass -File .\eng\publish-nuget.ps1 -DryRun`
  - `git status --short`

### W5 Tag-Based NuGet CI Release

- Status: `Done`
- Goal: move NuGet publishing responsibility to CI while keeping release execution explicit and version-gated.
- Deliverables: tag-triggered GitHub Actions release workflow, tag/package version check, manual workflow fallback, README/validation/gap/tracking updates, validation, commit.
- Progress:
  - Done: updated `.github/workflows/publish-nuget.yml` into the `Release NuGet` workflow.
  - Done: added `v*.*.*` tag push trigger for automated NuGet release publishing.
  - Done: added a CI check that the pushed tag matches `Directory.Build.props` package version.
  - Done: kept manual workflow dispatch available with `publish=false` by default.
  - Done: documented the required `NUGET_API_KEY` repository secret and `git tag v0.1.0` release command.
- Acceptance Criteria:
  - Normal push/PR CI still does not publish packages.
  - NuGet release publish runs from CI on a version tag.
  - The release job fails before publishing if the tag and package version disagree.
  - The workflow uses the `NUGET_API_KEY` repository secret for nuget.org publishing.
  - Manual workflow dispatch validates by default and only publishes when explicitly requested.
- Validation:
  - `powershell -NoProfile -ExecutionPolicy Bypass -File .\eng\publish-nuget.ps1 -DryRun`
  - PowerShell tag/version check for `v0.1.0`
  - `powershell -NoProfile -ExecutionPolicy Bypass -File .\eng\validate-bug-reports.ps1`
  - `git diff --check`

### W6 GitHub Release And Package Distribution

- Status: `Done`
- Goal: publish release outputs to GitHub as both GitHub Packages and GitHub Release assets while preserving nuget.org publishing.
- Deliverables: GitHub Release asset script, workflow updates for GitHub Packages and Release assets, docs/tracking updates, validation, commit.
- Progress:
  - Done: added `eng/publish-github-release.ps1` to validate and publish release assets for a version tag.
  - Done: updated `Release NuGet` workflow permissions to allow GitHub Packages and GitHub Release writes.
  - Done: added GitHub Packages publishing for `AvaScope.Protocol`, `AvaScope.Core`, and `AvaScope.Bridge` using the workflow `GITHUB_TOKEN`.
  - Done: added GitHub Release asset upload for the three `.nupkg` files, win/linux executable ZIPs, and `release-manifest.json`.
  - Done: kept `-SkipDuplicate` on package pushes so rerunning an existing release can still update GitHub Release assets.
  - Done: documented GitHub Packages and GitHub Release outputs.
- Acceptance Criteria:
  - The tag-triggered release workflow still publishes the three library packages to nuget.org.
  - The same workflow publishes the three library packages to GitHub Packages.
  - The same workflow creates or updates the matching GitHub Release.
  - GitHub Release assets include all local release artifacts needed for CLI/MCP/PreviewHost distribution.
  - Release asset validation is available locally in dry-run mode.
- Validation:
  - `powershell -NoProfile -ExecutionPolicy Bypass -File .\eng\create-local-release.ps1`
  - `powershell -NoProfile -ExecutionPolicy Bypass -File .\eng\publish-github-release.ps1 -Tag v0.1.0 -DryRun`
  - `powershell -NoProfile -ExecutionPolicy Bypass -File .\eng\publish-github-release.ps1 -Tag v0.1.1 -DryRun` expected tag/version mismatch failure
  - `powershell -NoProfile -ExecutionPolicy Bypass -File .\eng\publish-nuget.ps1 -DryRun`
  - `powershell -NoProfile -ExecutionPolicy Bypass -File .\eng\validate-bug-reports.ps1`
  - `git diff --check`

### W7 Version-Bump CI Release

- Status: `Done`
- Goal: release automatically from CI when the repository package version is increased, without requiring a manually pushed release tag.
- Deliverables: branch-push release detection, remote tag existence check, CI-created version tag, no-op behavior for already released versions, README/validation/gap/tracking updates, validation, commit.
- Progress:
  - Done: changed the `Release` workflow from tag-triggered release to `master`/`main` branch push release detection.
  - Done: made `Directory.Build.props` `<Version>` the only release version source.
  - Done: added remote `v<Version>` tag detection so ordinary pushes do not publish when the version is already released.
  - Done: added CI tag creation for newly detected versions after package publishing succeeds.
  - Done: removed the manual `release_tag` workflow input so manual workflow dispatch also uses the repository version.
  - Done: documented the release process as version bump, commit, and push.
- Acceptance Criteria:
  - Increasing `Directory.Build.props` `<Version>` and pushing to `master` or `main` triggers a release.
  - Pushing without increasing to a new unreleased version does not publish packages.
  - The workflow creates the matching `v<Version>` tag automatically for a new release.
  - The workflow still publishes nuget.org packages, GitHub Packages, and GitHub Release assets.
  - Manual workflow dispatch can validate by default and republish the current repository version only when `publish=true`.
- Validation:
  - `powershell -NoProfile -ExecutionPolicy Bypass -File .\eng\create-local-release.ps1`
  - Release metadata simulation for version `0.1.0` and tag `v0.1.0`
  - `powershell -NoProfile -ExecutionPolicy Bypass -File .\eng\publish-github-release.ps1 -Tag v0.1.0 -DryRun`
  - `powershell -NoProfile -ExecutionPolicy Bypass -File .\eng\publish-nuget.ps1 -DryRun`
  - `powershell -NoProfile -ExecutionPolicy Bypass -File .\eng\validate-bug-reports.ps1`
  - `git diff --check`

## Decision Log

- `2026-06-06`: Use `docs/DEVELOPMENT_PLAN.md` as the primary tracking document and keep `AGENTS.md` as the mandatory routing entrypoint.
- `2026-06-06`: Use milestone plus `Current Focus` and `Next Action` tracking instead of a sprint board or task ledger.
- `2026-06-06`: Optimize delivery order for vertical slices: foundation, protocol, core, MCP, bridge, screenshot, tree, input, preview.
- `2026-06-06`: Target Avalonia 12 with `net10.0` by default for Avalonia-facing projects.
- `2026-06-06`: Use xUnit for the initial test foundation because the .NET template is available locally and keeps M0 validation simple.
- `2026-06-06`: Protocol contracts use System.Text.Json attributes and remain independent from Avalonia runtime types and MCP SDK types.
- `2026-06-06`: `AvaScope.Core` references `AvaScope.Protocol` for shared transport-neutral session ids and keeps its own core result/error model for adapter-independent behavior.
- `2026-06-06`: Use official `ModelContextProtocol` 1.4.0 and `Microsoft.Extensions.Hosting` 10.0.8 for the initial stdio MCP adapter; official C# SDK docs recommend `AddMcpServer().WithStdioServerTransport().WithTools<T>()` for local stdio servers.
- `2026-06-06`: Use official Avalonia 12.0.4 packages for bridge work and manual `Avalonia.Headless` sessions for bridge smoke tests to avoid mixing xUnit v2 tests with `Avalonia.Headless.XUnit`'s xUnit v3 dependency.
- `2026-06-06`: Bridge top-level discovery combines Avalonia lifetime discovery with explicit weak `RegisterTopLevel` registration because headless and non-desktop hosts may not populate `IClassicDesktopStyleApplicationLifetime.Windows`.
- `2026-06-06`: Screenshot capture uses public Avalonia `RenderTargetBitmap.Render(Visual)` and stream-based `Bitmap.Save(Stream)` output; headless tests enable Skia-backed drawing so output files are non-empty.
- `2026-06-06`: Bridge-local attach discovery uses temp session manifests plus local named pipes, keeping runtime control opt-in and local-only while allowing MCP/CLI adapters to remain thin clients.
- `2026-06-06`: Bridge IPC uses newline-delimited UTF-8 JSON over named pipes with explicit request ids; the implementation uses byte-level pipe reads/writes for deterministic test behavior.
- `2026-06-06`: The reusable local attach client lives in `AvaScope.Core` so MCP and future CLI can share discovery/pipe behavior without referencing Avalonia bridge assemblies.
- `2026-06-06`: `list_top_levels` and `screenshot` MCP tools were exposed in the attach-client slice, then validated by the MCP/Core/pipe screenshot smoke test before closing M5.
- `2026-06-06`: Positive MCP/Core/pipe screenshot validation uses `HeadlessUnitTestSession.Dispatch(Func<Task>)`; awaiting the tool call from the headless UI dispatch context allows bridge server UI-thread work to complete without a manual pump loop.
- `2026-06-06`: M6 tree inspection uses public Avalonia visual/logical traversal APIs (`GetVisualChildren`, `GetLogicalChildren`) and keeps serialization bounded with an explicit depth limit.
- `2026-06-06`: Tree node ids are stable only within the active runtime session and are based on runtime object identity; no persisted cross-process identity guarantee is introduced in M6.
- `2026-06-06`: `find_nodes` searches the already bounded tree model and requires at least one filter so accidental unbounded discovery is avoided; type/text use case-insensitive contains matching, name/automation id use case-insensitive exact matching.
- `2026-06-06`: M7 input MVP deliberately starts with safe local-only operations: Button click is implemented through hit-test plus routed click event, key text mutates a focused `TextBox`, and pointer move is still tracked separately because generic routed/raw pointer injection needs a more precise platform strategy.
- `2026-06-07`: M7 pointer move uses public Avalonia 12 `PointerEventArgs` plus `InputElement.PointerMovedEvent` on the hit-tested input target instead of raw `IInputManager` injection because `TopLevel.InputRoot` is not public; this keeps the bridge off private runtime hooks.
- `2026-06-07`: The first M8 slice uses the official `Avalonia.Markup.Xaml.Loader` 12.0.4 package for standalone runtime `.axaml` loading; `AvaloniaXamlLoader.Load(Uri, Uri)` was rejected because it expects precompiled/resource XAML.
- `2026-06-07`: Preview rendering starts in an isolated `AvaScope.PreviewHost` child process before adding MCP/CLI adapters, preserving the architecture rule that user preview code cannot run inside the MCP server process.
- `2026-06-07`: M8 project-aware preview path resolution is kept as a separate slice before MSBuild integration; it validates the `.csproj` boundary and resolves relative view paths without yet claiming full project resource/code-behind support.
- `2026-06-07`: M8 build preparation currently uses `dotnet build` inside the preview host child process as the isolation boundary; this validates project compilation and keeps build failures structured, but it does not yet load compiled project assemblies/resources into the render path.
- `2026-06-07`: M8 compiled view loading uses the built project assembly plus `avares://<AssemblyName>/<ViewPath>` first, then falls back to standalone runtime XAML loading; this keeps real project code execution inside `AvaScope.PreviewHost`.
- `2026-06-07`: Added M9 to continue after the initial M0-M8 plan by wiring the completed preview host through Core and MCP adapters.
- `2026-06-07`: MCP references `AvaScope.PreviewHost` only to place the host assembly beside the MCP server output; rendering still goes through `PreviewHostClient` and a child process.
- `2026-06-07`: Added M10 for local CLI workflows after preview host and MCP preview integration.
- `2026-06-07`: `avascope mcp` is a process handoff to the colocated MCP server assembly rather than a second in-process MCP host, keeping one canonical MCP implementation.
- `2026-06-07`: Added M11 because the implemented bridge/MCP/CLI/preview workflows now need repository-level usage documentation before broader hardening.
- `2026-06-07`: README intentionally documents current limitations for input, preview resources, hot reload, and diagnostics so users do not assume full DevTools parity yet.
- `2026-06-07`: Added M12 to continue with explicit post-MVP hardening rather than broad untracked expansion.
- `2026-06-07`: Runtime `close_session` is the next hardening slice because stale local bridge sessions/manifests directly affect repeated agent workflows and the tool name is already part of the target MCP shape.
- `2026-06-07`: Runtime `close_session` uses a two-phase bridge IPC close handshake: the session registry is closed before the structured response is flushed, and the bridge server/manifest are stopped afterward on a background task to avoid pipe teardown before the client receives the result.
- `2026-06-07`: Diagnostics is the next P0 hardening slice because current operations return per-tool errors, but there is no aggregate health/version/session surface for agents to inspect before choosing a workflow.
- `2026-06-07`: The first diagnostics slice reports current health and structured unavailable states only; binding, layout, resource, and historical last-error streams remain future diagnostics work until those signals exist.
- `2026-06-07`: M14 targets preview app resources before reload because persistent/reloadable preview sessions should reuse a preview path that already handles app-level resources predictably.
- `2026-06-07`: M14 copies top-level resource entries from the loaded project `Application.Resources` instead of reparenting the resource dictionary, because Avalonia resource dictionaries are owned by a parent once loaded.
- `2026-06-07`: M15 expands diagnostics to preview-host readiness before reload work so agents can distinguish missing preview infrastructure from project/render failures.
- `2026-06-07`: M15 preview diagnostics deliberately checks only host readiness and does not launch `AvaScope.PreviewHost`, build projects, or load user XAML; render/project diagnostics remain tied to actual preview requests.
- `2026-06-07`: M16 starts with preview-session metadata before implementing `reload`, because reload needs a stable persisted request/result boundary that does not keep user code inside MCP.
- `2026-06-07`: M16 preview sessions are metadata records, not persistent Avalonia preview processes; this keeps user project code execution isolated in one-shot `AvaScope.PreviewHost` child processes while still giving reload a stable request/result target.
- `2026-06-07`: M17 will implement reload only for preview sessions first; runtime bridge reload remains separate because it needs different lifecycle and safety semantics.
- `2026-06-07`: M17 preview reload re-renders stored preview requests through the same isolated preview host path and deliberately does not implement runtime bridge reload.
- `2026-06-07`: M18 targets pointer press/release before packaging/CI because input coverage remains a P1 functional gap in the runtime automation workflow.
- `2026-06-07`: M18 pointer press/release uses public Avalonia 12.0.4 routed pointer event args and `PointerPointProperties` update kinds rather than raw platform input injection.
- `2026-06-07`: M19 targets the runtime reload contract next because `reload` is now implemented for preview sessions but still has preview-specific response semantics.
- `2026-06-07`: M19 keeps preview `reload` success responses compatible and treats runtime bridge reload as an explicit local health check plus unsupported diagnostic, not hot reload.
- `2026-06-07`: PreviewHost smoke-test cleanup is best-effort after assertions pass because Windows can transiently hold built sample project files after child-process exit.
- `2026-06-07`: M20 starts package metadata with `AvaScope.Bridge` first because it is the opt-in user-facing library package; executable/tool packaging for CLI/MCP can stay explicit until a later release workflow slice.
- `2026-06-07`: M20 packages `AvaScope.Protocol` and `AvaScope.Core` alongside `AvaScope.Bridge` so the bridge package has resolvable local package dependencies.
- `2026-06-07`: M21 targets CI before publishing or installer work because local build/test/pack validation is now stable enough to automate without credentials.
- `2026-06-07`: M21 uses Windows CI first because current bridge and PreviewHost smoke coverage has been validated on Windows with named-pipe and headless Avalonia behavior.
- `2026-06-07`: M22 targets executable packaging after CI because library packages are now validated but `avascope` executable distribution is still undefined.
- `2026-06-07`: M22 uses a Windows framework-dependent zipped publish directory first because it preserves `avascope`, `AvaScope.Mcp`, and `AvaScope.PreviewHost` co-location without creating a feed-published tool package or requiring publishing credentials.
- `2026-06-07`: The documented executable package command uses `powershell -NoProfile -ExecutionPolicy Bypass -File` because local Windows execution policy may block direct `.ps1` invocation and the repository should not require a global machine policy change.
- `2026-06-07`: M23 targets artifact hardening next because local packages and executable ZIPs now exist, but agents still need deterministic checksum/manifest output before broader release automation.
- `2026-06-07`: M23 keeps the artifact manifest under ignored `artifacts/` output rather than committing it because package ZIP hashes and sizes are generated validation evidence, not stable source metadata.
- `2026-06-07`: The next executable distribution step is RID-based framework-dependent ZIPs before self-contained ZIPs or a `dotnet tool`; this preserves CLI/MCP/PreviewHost co-location while avoiding runtime bundling size and feed publishing decisions.
- `2026-06-07`: M24 defaults executable packages to `win-x64` and `linux-x64`; macOS artifacts stay deferred until there is an explicit validation surface for macOS apphost behavior, signing, and notarization expectations.
- `2026-06-07`: RID-based executable packaging does not use the CI `-NoBuild` path because runtime-specific publish requires runtime-specific restore/build assets that are not produced by the generic solution build.
- `2026-06-07`: M25 returns to the input coverage gap after packaging hardening because focus targeting and keyboard keys are necessary for useful runtime automation beyond mouse and TextBox text insertion.
- `2026-06-07`: M25 uses public Avalonia 12.0.4 `InputElement.Focus(...)` and routed `KeyEventArgs`/`InputElement.KeyDownEvent`/`KeyUpEvent`; it does not use raw platform input injection or private hooks.
- `2026-06-07`: M25 key input accepts Avalonia `Key` names plus simple modifier text, and returns structured invalid-input diagnostics instead of inventing a custom key naming scheme.
- `2026-06-07`: M26 targets `inspect_node` next because it is part of the intended MCP tool shape and tree/find already provide stable node ids but no detail lookup tool.
- `2026-06-07`: M26 keeps `inspect_node` bounded to one node plus `childCount`; it does not return descendants or arbitrary Avalonia object properties in the first slice.
- `2026-06-07`: M27 targets CLI runtime bridge workflow next because MCP now exposes the primary runtime inspection tools, while the CLI still lacks runtime attach/list/inspect commands from the intended product shape.
- `2026-06-07`: M27 starts CLI runtime support with `attach` because it is the smallest command over existing `LocalBridgeClient` discovery and returns the session id needed by later CLI runtime commands.
- `2026-06-07`: M28 follows with CLI top-level/screenshot commands because `attach` alone verifies bridge discovery but does not yet expose a complete local inspection workflow.
- `2026-06-07`: M28 validates CLI runtime success paths with a fake local bridge named pipe instead of a headless Avalonia child app; this keeps process-level CLI coverage deterministic while existing bridge tests continue to validate real Avalonia screenshot behavior.
- `2026-06-07`: M29 targets CLI visual/logical tree inspection next because attach, top-level discovery, and screenshot now cover the session/image path, while structured tree output is the next core inspection workflow.
- `2026-06-07`: M29 defers `inspect-node` to M30 so tree retrieval and single-node detail remain separately trackable vertical slices with focused tests.
- `2026-06-07`: M30 keeps `inspect-node` tree kind optional with `visual` as the default to match MCP behavior while still allowing explicit logical node inspection.
- `2026-06-07`: M31 targets CLI `find-nodes` next because tree output and single-node detail now exist, and search is the missing workflow that makes stable node ids discoverable from the CLI.
- `2026-06-07`: M31 validates `find-nodes` filter presence in the CLI before calling Core so empty searches fail deterministically as CLI argument errors.
- `2026-06-07`: M32 targets CLI input next because attach, screenshot, tree, inspect, and find now cover read-side runtime inspection, leaving local-only control as the next core runtime workflow.
- `2026-06-07`: M32 validates input actions in the CLI before calling Core so unsupported or under-specified local control requests fail as deterministic CLI argument errors.
- `2026-06-07`: M33 targets CLI close-session next because runtime input introduces local control, and operators need a direct CLI way to close bridge sessions.
- `2026-06-07`: M33 keeps close-session as a separate slice from diagnostics/reload because it is the only runtime CLI command that intentionally changes bridge session lifecycle.
- `2026-06-07`: M34 targets CLI diagnostics next because attach/inspection/control/close paths now exist, and diagnostics makes bridge manifest and preview-host readiness visible from the CLI.
- `2026-06-07`: M34 includes preview-host diagnostics in CLI diagnostics to match MCP diagnostics coverage and expose preview readiness without launching user code.
- `2026-06-07`: M35 targets CLI reload next because diagnostics closes the read-only visibility gap, leaving reload as the remaining intended runtime/preview CLI workflow.
- `2026-06-07`: M35 keeps CLI reload to runtime bridge health checks because preview sessions are currently in-memory server state and CLI preview renders are one-shot; durable preview reload belongs in a separate reload foundation slice.
- `2026-06-07`: M36 targets reload/hot preview foundation next because the CLI runtime command surface now covers attach, top-levels, screenshots, trees, inspect, find, input, close, diagnostics, and runtime reload checks.
- `2026-06-07`: M36 persists MCP-backed preview session records as local per-session JSON under the AvaScope temp preview-session store; CLI preview remains one-shot until a future CLI command explicitly creates durable preview sessions.
- `2026-06-07`: M36 ignores corrupt persisted preview-session records during startup so one bad file cannot prevent the MCP server from starting; write failures are surfaced as structured `preview_session_store_failed` errors.
- `2026-06-07`: M37 targets preview resource/style scope next because durable preview reload now exists and the remaining preview gap is closer design-time resource parity, not another adapter command.
- `2026-06-07`: M37 uses the project's real `Application.Initialize()` path when possible, inside the isolated preview host child process, because compiled App.axaml styles may not be materialized by URI-only loading.
- `2026-06-07`: M37 applies project application styles to the preview window style scope rather than the long-lived host application scope, keeping the imported styles limited to the single render.
- `2026-06-07`: M38 targets app-level include scenarios next because top-level resources and direct application styles are now covered, while include parity remains unverified.
- `2026-06-07`: M38 starts include parity with `ResourceInclude` under `Application.Resources` because resource includes commonly feed static resources used by views and can be validated with a single pixel-stable render.
- `2026-06-07`: M39 targets theme dictionary variants next because resource include support is covered and requested light/dark preview variants should resolve app-level theme dictionaries predictably.
- `2026-06-07`: M39 copies project app `ThemeDictionaries` into the host app resource scope so `DynamicResource` lookups honor requested light/dark preview variants.
- `2026-06-07`: M40 targets `StyleInclude` next because direct app styles, resource includes, and theme dictionaries are covered, while included app styles remain unverified.
- `2026-06-07`: M40 records `StyleInclude` as a coverage slice because the existing project app style transfer already handles compiled style includes; no PreviewHost code change was required.
- `2026-06-07`: M41 targets an explicit culture variant contract next because App.axaml resource/style parity is now covered enough to move to design-time preview variants.
- `2026-06-07`: M41 keeps culture as a transport-neutral string in Protocol and applies it through `CultureInfo` only inside the isolated PreviewHost render process.
- `2026-06-07`: M42 starts with a design-data contract audit because generic design data can imply arbitrary user-code construction and should not be guessed without an explicit boundary.
- `2026-06-07`: M42 selects project-owned public parameterless design-data type loading as the first design-data boundary; JSON object injection, dependency injection, remote data, and long-lived state stay out of scope.
- `2026-06-07`: M43 will assign design data as the loaded root control `DataContext` inside the PreviewHost child process, keeping Core and MCP limited to transport-neutral metadata.
- `2026-06-07`: M43 supports typed-binding design data by assigning a project-owned public parameterless type as root `DataContext`; JSON injection, DI, remote data, and persisted design-data objects remain out of scope.
- `2026-06-07`: M44 starts with an App startup/lifecycle audit because running broader startup hooks can trigger user side effects and should be bounded before implementation.
- `2026-06-07`: M44 explicitly defers running project `OnFrameworkInitializationCompleted` and lifetime startup hooks in preview because Avalonia design-mode lifetimes can be null and normal desktop startup commonly creates windows or services outside the requested preview view.
- `2026-06-07`: M45 targets app-level `DataTemplates` next because they improve preview parity through loaded `App.axaml` composition without invoking broader app startup side effects.
- `2026-06-07`: M45 transfers project `Application.DataTemplates` into the preview `Window.DataTemplates` scope instead of the host application global scope, keeping templates limited to the single isolated render.
- `2026-06-07`: M46 targets a getting-started sample because App.axaml preview parity now covers resources, styles, includes, theme dictionaries, culture, design data, and data templates, while external users still lack a runnable first workflow in the repository.
- `2026-06-07`: M46 keeps the sample project `IsPackable=false` and under `/samples/` so solution builds validate it without changing public package contents.
- `2026-06-07`: M46 gates sample bridge activation behind `AVASCOPE_SAMPLE_BRIDGE` so the sample demonstrates explicit opt-in local inspection rather than always enabling a bridge.
- `2026-06-07`: CLI preview now normalizes project and output paths from the caller working directory before launching `AvaScope.PreviewHost`; view paths remain project-relative unless the user supplies an absolute path.
- `2026-06-07`: M47 targets Release validation next because the expanded solution and CLI path change should be checked against public-alpha packaging workflows before more feature work.
- `2026-06-07`: M47 keeps `samples/AvaScope.GettingStartedApp` out of release artifacts; it validates source workflows but public alpha artifacts remain the three libraries plus the CLI/MCP/PreviewHost executable ZIPs.
- `2026-06-07`: M48 targets bounded preview failure context while preserving existing `ToolResult<PreviewResponse>` compatibility so current MCP/CLI clients do not break.
- `2026-06-07`: M48 uses optional `ProtocolError.details` and `CoreError.Details` rather than changing success/failure result envelopes; preview clients can ignore details and still parse the same `code/message` shape.
- `2026-06-07`: M48 keeps build logs bounded by using the existing trimmed output tail for `error.details.outputTail`; unbounded build logs remain out of the protocol.
- `2026-06-07`: M49 targets runtime safety boundaries next because public-alpha bridge control should be re-audited after preview diagnostics and release validation work.
- `2026-06-07`: M49 records bridge transport scope in session manifests as `local_only`; unsupported manifest scopes are invalid discovery records, not attachable sessions.
- `2026-06-07`: M49 uses `PipeOptions.CurrentUserOnly` for local bridge named-pipe servers where supported by .NET, keeping runtime IPC scoped to the current local user rather than relying only on manifest obscurity.
- `2026-06-07`: M50 is the completion gate for the thread goal; do not mark the goal complete until the audit proves every public-alpha requirement with current evidence.
- `2026-06-07`: M50 completion audit concludes the current repository satisfies the production-ready public-alpha objective; runtime hot reload, richer input, broader preview startup orchestration, publishing automation, and richer binding/layout/resource diagnostics remain explicit non-blocking post-alpha deferrals.
- `2026-06-08`: Bug reports are stored as sanitized documentation under `docs/bug-reports/` and validated by `eng/validate-bug-reports.ps1`; storing a report is not authorization to implement a fix.
- `2026-06-08`: The user explicitly requested implementation for the two stored bug reports, so W1 intake moved to `Done` and W2 became the active implementation workstream.
- `2026-06-08`: W2 keeps explicit `designDataType` as the highest-precedence preview data source; `Design.DataContext` and `d:DataContext="{x:Static ...}"` are fallback design-time metadata only.
- `2026-06-08`: Preview width and height are optional at the protocol/CLI/MCP boundary only when PreviewHost can resolve positive design-time dimensions from the root AXAML.
- `2026-06-08`: After W2 completion, W1 returned as the active `In Progress` intake workstream so future reports remain stored but not implemented without explicit user authorization.
- `2026-06-08`: Local testing should use a packaged Release artifact produced by `eng/create-local-release.ps1`; Debug build paths are only for development diagnostics.
- `2026-06-08`: Feature requests are stored as sanitized tickets under `docs/feature-requests/`; storing a feature ticket is not authorization to implement it.
- `2026-06-08`: W4 keeps NuGet publishing manual and credential-gated: normal CI still validates artifacts without secrets, while actual public publishing requires a nuget.org API key supplied by environment, parameter, or a manually triggered GitHub workflow secret.
- `2026-06-08`: W5 moves NuGet publishing to CI on explicit version tags instead of branch pushes; the release tag must match `Directory.Build.props` so package identity stays deliberate.
- `2026-06-08`: W6 uses GitHub Releases, not NuGet packages, for CLI/MCP/PreviewHost executable distribution because those projects are intentionally non-packable and already validated as co-located framework-dependent ZIP artifacts.
- `2026-06-08`: W7 supersedes the manual tag-push release trigger: `Directory.Build.props` `<Version>` is the release trigger, and CI creates the matching tag only after package publishing succeeds.
- `2026-06-08`: Project ownership now requires agents to push completed committed slices to the configured remote, not only leave local commits.

## Change Log

- `2026-06-06`: Initial development plan created with M0-M8 milestones, tracking rules, acceptance criteria, and validation commands.
- `2026-06-06`: Completed M0 foundation with shared build settings, validation documentation, test project, and Protocol/Core smoke tests; moved active focus to M1.
- `2026-06-06`: Completed M1 protocol contracts with session ids, protocol version metadata, health/list_sessions DTOs, tool result/error shapes, and JSON serialization tests; moved active focus to M2.
- `2026-06-06`: Completed M2 core session model with registry, lifecycle transitions, structured errors, and unit tests; moved active focus to M3.
- `2026-06-06`: Completed M3 minimal MCP adapter with stdio hosting, `health`, `list_sessions`, tool mapping tests, and stdio child-process smoke coverage; moved active focus to M4.
- `2026-06-06`: Completed M4 opt-in bridge MVP with explicit activation/deactivation, local-only runtime scope, UI-thread top-level discovery, explicit top-level registration, and headless bridge smoke coverage; moved active focus to M5.
- `2026-06-06`: Added M5 bridge-local screenshot capture with `ScreenshotResponse`, registered top-level lookup, PNG output, structured missing-top-level errors, and headless file validation; M5 remains in progress pending local attach transport.
- `2026-06-06`: Added M5 bridge IPC foundation with local session manifests, named-pipe server startup/shutdown, IPC DTO JSON tests, manifest lifecycle validation, and pipe health smoke coverage; M5 remains in progress pending MCP/CLI attach client and cross-process screenshot validation.
- `2026-06-06`: Added M5 reusable local attach client and MCP tool adapters for `attach_to_app`, `list_top_levels`, and `screenshot`; M5 remains in progress pending deterministic positive top-level/screenshot validation through the MCP/Core/pipe path.
- `2026-06-06`: Completed M5 runtime screenshot slice with MCP/Core/named-pipe top-level listing and screenshot validation against a headless Avalonia window; moved active focus to M6.
- `2026-06-06`: Added M6 bounded visual/logical tree serialization with protocol DTOs, bridge traversal, MCP/Core/pipe tools, and headless validation; M6 remains in progress pending `find_nodes`.
- `2026-06-06`: Completed M6 tree inspection slice with `find_nodes` filters for type, name, automation id, and text plus path-oriented match results; moved active focus to M7.
- `2026-06-06`: Added M7 input MVP protocol, bridge, Core, and MCP path with headless validation for pointer target lookup, Button click, and focused TextBox key text; M7 remains in progress pending real pointer move injection or explicit limitation handling.
- `2026-06-07`: Completed M7 input slice with routed pointer move, Button click, focused TextBox key text, and unsupported input diagnostics; moved active focus to M8.
- `2026-06-07`: Added the first M8 preview host slice with protocol preview DTOs, isolated child process entrypoint, standalone `.axaml` runtime loading, headless Skia PNG output, and process smoke validation; M8 remains in progress pending project-aware preview loading.
- `2026-06-07`: Added M8 project-aware path resolution for `.csproj` plus relative view paths with process smoke coverage; M8 remains in progress pending MSBuild/design-time build support.
- `2026-06-07`: Added M8 project build boundary in the preview host child process with structured build failure diagnostics; M8 remains in progress pending compiled project assembly/resource loading.
- `2026-06-07`: Completed M8 preview host slice with compiled Avalonia project resource and code-behind smoke rendering; added M9 preview adapter integration as the active focus.
- `2026-06-07`: Completed M9 preview adapter integration with Core `PreviewHostClient`, MCP `preview_axaml`, process smoke coverage, and stdio tool-list validation; added M10 CLI integration as the active focus.
- `2026-06-07`: Completed M10 CLI integration with `avascope preview`, `avascope mcp`, process smoke coverage, and structured invalid-argument errors; added M11 documentation and release readiness as the active focus.
- `2026-06-07`: Completed M11 documentation and release-readiness slice with README usage documentation and validation guide updates; added M12 post-MVP hardening as the active focus.
- `2026-06-07`: Added post-MVP gap audit and selected runtime `close_session` lifecycle support as the next hardening slice.
- `2026-06-07`: Completed M12 close-session hardening with bridge IPC, Core client, MCP tool, manifest cleanup validation, and PreviewHost cleanup retry hardening; added M13 diagnostics surface as the active focus.
- `2026-06-07`: Completed M13 diagnostics surface with protocol DTOs, Core bridge diagnostics, MCP `diagnostics`, unavailable-state handling, and focused tests; added M14 preview app resource scope as the active focus.
- `2026-06-07`: Completed M14 preview app resource scope with compiled `App.axaml` resource loading, resource-backed render validation, structured invalid-app-resource errors, and README updates; added M15 preview diagnostics expansion as the active focus.
- `2026-06-07`: Completed M15 preview diagnostics expansion with preview-host readiness diagnostics, MCP diagnostics composition, build-server isolation hardening, README updates, and full-suite validation; added M16 preview reload foundation as the active focus.
- `2026-06-07`: Completed M16 preview reload foundation with preview session metadata, Core create/list/close lifecycle, MCP preview session tools, README updates, and full-suite validation; added M17 preview reload MVP as the active focus.
- `2026-06-07`: Completed M17 preview reload MVP with Core preview session re-rendering, MCP `reload`, session state recovery/failure handling, README updates, and full-suite validation; added M18 input press/release slice as the active focus.
- `2026-06-07`: Completed M18 input press/release with stable action constants, Avalonia routed pointer press/release events, active pointer reuse, headless bridge validation, README/gap updates, and full-suite validation; added M19 runtime reload contract as the active focus.
- `2026-06-07`: Completed M19 runtime reload contract with preview-compatible reload responses, explicit active-runtime unsupported diagnostics, PreviewHost cleanup hardening, README/gap updates, and full-suite validation; added M20 packaging metadata as the active focus.
- `2026-06-07`: Completed M20 packaging metadata with local library packages for Protocol/Core/Bridge, explicit non-packable executable projects, ignored package artifacts, README updates, and local pack validation; added M21 CI validation as the active focus.
- `2026-06-07`: Completed M21 CI validation with GitHub Actions restore/build/test/pack workflow, README/gap updates, and local command validation; added M22 executable packaging as the active focus.
- `2026-06-07`: Completed M22 executable packaging with a validated Windows framework-dependent ZIP containing CLI, MCP, PreviewHost, Core, and Protocol artifacts; added M23 release artifact hardening as the active focus.
- `2026-06-07`: Completed M23 release artifact hardening with an ignored JSON manifest containing artifact sizes and SHA-256 hashes, CI verification, README updates, and release distribution decision logging; added M24 cross-platform framework-dependent artifacts as the active focus.
- `2026-06-07`: Completed M24 cross-platform framework-dependent artifacts with RID-aware win-x64/linux-x64 ZIP packaging, manifest coverage enforcement, CI/README updates, artifact smoke validation, and full Release test validation; added M25 runtime focus and keyboard input as the active focus.
- `2026-06-07`: Completed M25 runtime focus and keyboard input with stable protocol actions, target-node/key IPC fields, public Avalonia focus/key event dispatch, README updates, and full-suite validation; added M26 inspect node detail as the active focus.
- `2026-06-07`: Completed M26 inspect node detail with Protocol/Core/Bridge/MCP support, bounded single-node details, structured not-found diagnostics, README updates, and full-suite validation; added M27 CLI runtime bridge workflow as the active focus.
- `2026-06-07`: Completed M27 CLI runtime bridge workflow with `avascope attach`, structured attach/no-session errors, README updates, CLI/Core targeted tests, and full-suite validation; added M28 CLI top-level and screenshot workflow as the active focus.
- `2026-06-07`: Completed M28 CLI top-level and screenshot workflow with `list-top-levels`, `screenshot`, fake bridge pipe success tests, no-session and missing-argument coverage, README updates, and targeted validation; added M29 CLI tree inspection as the active focus.
- `2026-06-07`: Completed M29 CLI runtime tree inspection with `visual-tree`, `logical-tree`, shared `max-depth` validation, fake bridge pipe success tests, README updates, and full-suite validation; added M30 CLI node detail as the active focus.
- `2026-06-07`: Completed M30 CLI runtime node detail with `inspect-node`, default visual tree kind, explicit logical tree kind support, fake bridge pipe success tests, README updates, and full-suite validation; added M31 CLI find nodes as the active focus.
- `2026-06-07`: Completed M31 CLI runtime find nodes with filter validation, depth/result limit validation, fake bridge pipe success tests, README updates, and full-suite validation; added M32 CLI runtime input as the active focus.
- `2026-06-07`: Completed M32 CLI runtime input with local-only action validation, click/key/focus fake bridge pipe success tests, README updates, and full-suite validation; added M33 CLI close session as the active focus.
- `2026-06-07`: Completed M33 CLI close session with structured `close-session`, fake bridge pipe success tests, README updates, and full-suite validation; added M34 CLI diagnostics as the active focus.
- `2026-06-07`: Completed M34 CLI diagnostics with process/session filters, max-session validation, preview-host diagnostics, fake bridge health tests, README updates, and full-suite validation; added M35 CLI reload as the active focus.
- `2026-06-07`: Completed M35 CLI reload with runtime bridge health checks, explicit `runtime_reload_not_supported`, structured failure tests, README updates, and full-suite validation; added M36 reload/hot preview foundation as the active focus.
- `2026-06-07`: Completed M36 reload/hot preview foundation with persistent Core preview-session JSON storage, MCP store wiring, restore tests, README updates, and full-suite validation; added M37 preview resource/style scope as the active focus.
- `2026-06-07`: Completed M37 preview resource/style scope with project `Application.Initialize()` loading, preview-window style application, pixel-validated App.axaml style smoke coverage, README updates, and full-suite validation; added M38 preview resource include scope as the active focus.
- `2026-06-07`: Completed M38 preview resource include scope with App.axaml merged resource dictionary transfer, pixel-validated ResourceInclude smoke coverage, README updates, and full-suite validation; added M39 preview theme dictionary variant scope as the active focus.
- `2026-06-07`: Completed M39 preview theme dictionary variant scope with App.axaml theme dictionary transfer, pixel-validated light/dark theme resource smoke coverage, README/gap updates, and full-suite validation; added M40 preview style include scope as the active focus.
- `2026-06-07`: Completed M40 preview style include scope with pixel-validated StyleInclude smoke coverage, README/gap updates, and full-suite validation; added M41 preview culture variant contract as the active focus.
- `2026-06-07`: Completed M41 preview culture variant contract with protocol/CLI/MCP propagation, PreviewHost culture application, pixel-validated culture render coverage, README/gap updates, and full-suite validation; added M42 preview design data contract audit as the active focus.
- `2026-06-07`: Completed M42 preview design data contract audit with project-owned design-data type boundary selection, non-goal documentation, README/gap updates, and build validation; added M43 preview design data type slice as the active focus.
- `2026-06-07`: Completed M43 preview design data type slice with `designDataType` protocol/CLI/MCP propagation, PreviewHost root DataContext assignment, typed-binding render coverage, invalid type diagnostics, README/gap updates, and full-suite validation; added M44 preview App startup boundary audit as the active focus.
- `2026-06-07`: Completed M44 preview App startup boundary audit with explicit lifecycle-hook deferral, startup non-goals, official Avalonia lifetime source review, README/gap updates, and build validation; added M45 preview App DataTemplates scope as the active focus.
- `2026-06-07`: Completed M45 preview App DataTemplates scope with preview-window data-template transfer, pixel-validated compiled App.axaml template coverage, README/gap updates, and full-suite validation; added M46 getting-started sample as the active focus.
- `2026-06-07`: Completed M46 getting-started sample with `samples/AvaScope.GettingStartedApp`, documented preview/runtime bridge commands, CLI relative preview path normalization, ignored PNG validation, README/gap updates, and full-suite validation; added M47 public-alpha Release validation refresh as the active focus.
- `2026-06-07`: Completed M47 public-alpha Release validation refresh with Release build/test, library pack, executable ZIP packaging, artifact manifest verification, packaged CLI sample preview smoke, gap/tracking updates, and sample exclusion from release artifacts; added M48 preview failure diagnostics detail as the active focus.
- `2026-06-07`: Completed M48 preview failure diagnostics detail with bounded `error.details` for preview build/render failures, Core/CLI/MCP/session details preservation, protocol/PreviewHost/CLI tests, README/gap updates, and full-suite validation; added M49 runtime safety boundary audit as the active focus.
- `2026-06-07`: Completed M49 runtime safety boundary audit with explicit local-only manifest scope, current-user local pipe server option, unsupported transport manifest diagnostics, README/sample README safety documentation, focused tests, and full-suite validation; added M50 public-alpha completion audit as the active focus.
- `2026-06-07`: Completed M50 public-alpha completion audit with `docs/PUBLIC_ALPHA_AUDIT.md`, local install and validation documentation updates, final Debug/Release build and test validation, NuGet/executable artifact verification, source and packaged sample preview smoke validation, gap audit closure, and no remaining public-alpha blockers.
- `2026-06-08`: Added the bug report intake ledger with privacy validation and stored `BUG-0001` for the PreviewHost `Window` root rendering failure without starting implementation.
- `2026-06-08`: Stored `BUG-0002` for ignored Avalonia design-time `DataContext` metadata, with local paths and target-specific identifiers sanitized.
- `2026-06-08`: Completed W2 by implementing `BUG-0001` Window-root preview rendering and `BUG-0002` design-time data context/dimension support with PreviewHost, Protocol, CLI, and MCP tests.
- `2026-06-08`: Completed W3 by adding `eng/create-local-release.ps1` as the local Release artifact workflow and documenting packaged-release testing.
- `2026-06-08`: Added the feature request ticket ledger with `FEAT-0001` through `FEAT-0007`, prioritized binding/resource diagnostics, layout warnings, and computed style/resource inspection as the top three backlog items.
- `2026-06-08`: Completed W4 by adding `eng/publish-nuget.ps1`, the manual `Publish NuGet` GitHub workflow, NuGet publishing documentation, and release/dry-run validation.
- `2026-06-08`: Completed W5 by converting NuGet publishing to the tag-triggered `Release NuGet` CI workflow with version-gating and `NUGET_API_KEY` secret usage.
- `2026-06-08`: Completed W6 by adding GitHub Packages publishing and GitHub Release asset publishing for release artifacts.
- `2026-06-08`: Completed W7 by changing CI release activation to version bumps in `Directory.Build.props` with automatic tag creation and no-op behavior for already released versions.
- `2026-06-08`: Updated project agent ownership rules to require pushing committed changes.
