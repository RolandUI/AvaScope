# AvaScope Development Plan

GitHub Issues and Milestones are the primary project-management source for autonomous agents working on AvaScope. This document is the compact local handoff and validation log. Update it whenever meaningful implementation, validation, or planning changes the active GitHub issue or release state.

## Project Operating Rules

- Work in small vertical slices that produce buildable, testable behavior.
- Keep exactly one GitHub issue marked `status:in-progress` unless the user explicitly asks for parallel work.
- Before implementing, inspect the selected GitHub issue, its milestone, `docs/GITHUB_PROJECT_WORKFLOW.md`, and this handoff log. If this document is stale, update it from GitHub before implementation.
- Each meaningful implementation change must include relevant tests or an explicit validation note explaining why tests are not applicable.
- Each completed slice must be validated with the listed commands before its issue moves to `status:done`.
- Commit and push each completed vertical slice or coherent milestone part. Record the commit hash and validation in the GitHub issue and, when practical, in this document.
- Keep MCP, CLI, core runtime, bridge, preview host, and protocol concerns separated.
- Do not introduce broad skeletons unless they directly support the active vertical slice.
- From `2026-06-09` onward, development is release-based. Define the next release target in `docs/RELEASE_PLAN.md` before feature implementation and keep this plan aligned with that release scope.
- From `2026-06-10` onward, release execution is GitHub-driven. Each active release must have a GitHub milestone and implementation issues before feature work starts.
- Treat a `Directory.Build.props` version bump as the release commit only. Do not bump the version until the current release target is `Release Candidate` and the release gate has passed.

## Status Legend

- `Not Started`: No implementation work has begun.
- `In Progress`: The active agent is implementing or validating this item.
- `Blocked`: Progress requires external input, credentials, package availability, or a product decision.
- `Review`: Implementation is complete, but validation or handoff is still pending.
- `Done`: Acceptance criteria and validation are complete.

## Current Focus

- `Safe Runtime Scenario Runner With Isolated App State`
- GitHub Issue: https://github.com/RolandUI/AvaScope/issues/45
- GitHub Milestone: none
- Status: `In Progress`
- Owner: unassigned
- Started: `2026-07-01`
- Goal: complete the scenario runner layer over semantic workflow execution with launch/attach orchestration, isolated launched-app state, destructive-target guardrails, and human-readable timeline artifacts.

## Next Action

Finish #45 full validation, commit/push the scenario runner slice, update GitHub with the commit hash and validation results, then close #45 if acceptance remains satisfied. GitHub issue #49 remains open for generated-report click-to-node provenance mapping.

## Latest Validation

- `2026-07-01`: Implemented the remaining GitHub issue #45 scenario-runner slice on top of the semantic workflow runner. Added Protocol `RuntimeScenarioRequest`/`RuntimeScenarioResponse` contracts, Core `RuntimeScenarioRunner`, CLI `run-scenario`, MCP `run_scenario`, capability discovery, launch-mode isolated app-state environment setup, attach/session scenario execution, destructive-target safety preservation for non-isolated sessions, Markdown timeline artifacts, user/stable-surface docs, and focused protocol/CLI/MCP/stable-surface coverage. Validation passed with `dotnet build AvaScope.slnx --no-restore -v:minimal`, scenario/capability/MCP/stable-surface tests (`11` passed), docs/stable-surface guard tests (`18` passed), full Debug tests (`332` passed), and `git diff --check` with only LF/CRLF normalization warnings.
- `2026-07-01`: Implemented GitHub issue #44 isolated preview build-output handling. PreviewHost now builds project previews with an AvaScope-owned isolated `BaseOutputPath` by default, supports explicit `buildOutputRoot`, `assemblyPath`, and `noBuild` request options through Protocol/Core/CLI/MCP/session/baseline paths, reports build mode/output/log metadata in structured project info and errors, preserves full build stdout/stderr in `buildLogPath` artifacts, and reports the first underlying root cause plus per-viewport summary when every contact-sheet preview variant fails. Validation passed with `dotnet build AvaScope.slnx --no-restore -v:minimal`, focused #44 tests (`6` passed), docs/stable-surface guard tests (`18` passed), baseline-suite regression test (`1` passed), full Debug tests (`327` passed), and `git diff --check` with only LF/CRLF normalization warnings.
- `2026-07-01`: Implemented the user-requested runtime node source map, layout explanation diagnostics, semantic workflow runner, live binding/DataContext inspector metadata, and explicit preview state variants. Added additive protocol DTOs/capabilities, Bridge runtime XAML/source/binding/layout extraction, Core semantic workflow execution, CLI `explain-layout` and `run-workflow`, MCP `explain_layout` and `run_workflow`, PreviewHost `stateVariant` activation, baseline/profile state propagation, docs, and focused coverage. Validation passed with `dotnet build AvaScope.slnx --no-restore -v:minimal`, focused feature tests (`9` passed), docs guard tests (`12` passed), full Debug tests (`324` passed), and `git diff --check` with only LF/CRLF normalization warnings.
- `2026-06-14`: Completed GitHub issue #54 in commit `7bd469f` with Windows per-user installer/discovery workflow without release/publish. Added `eng/install-avascope.ps1`, CLI `--version`, `%LOCALAPPDATA%\AvaScope\bin\avascope.cmd`, `%LOCALAPPDATA%\AvaScope\avascope.discovery.json`, installer/MCP stdio smoke coverage, and documentation for agent discovery order. Validation passed with `dotnet build AvaScope.slnx --no-restore -v:minimal`, `dotnet test AvaScope.slnx --no-build --filter FullyQualifiedName~InstallerWorkflowTests` (2 passed, including installed `avascope.cmd mcp` stdio), docs/stable-surface focused tests (9 passed), `eng/create-local-release.ps1 -RuntimeIdentifiers win-x64 -SkipTests`, `eng/install-avascope.ps1 -SourcePath .\artifacts\executables\avascope-win-x64-framework-dependent`, installed PATH command `avascope --version` returning `1.0.2`, installed `doctor` smoke, discovery manifest inspection, full Debug tests (`319` passed), and `git diff --check` with only LF/CRLF normalization warnings. Stale artifact-hosted `avascope.exe`/`dotnet.exe` processes were stopped before local packaging because they held locks under `artifacts\executables`.
- `2026-06-13`: Published `v1.0.2` from commit `15a4af1547376b9beb7a76c3a8c947dcd4bf8187` through Release workflow `27464291295` (`push`, success). The workflow published the three `1.0.2` packages to nuget.org and GitHub Packages through NuGet trusted publishing, created tag `v1.0.2`, uploaded six GitHub Release assets, and `git ls-remote --tags origin refs/tags/v1.0.2` confirmed the tag points at `15a4af1547376b9beb7a76c3a8c947dcd4bf8187`.
- `2026-06-13`: Local `v1.0.2` release gate passed with `eng/create-local-release.ps1` (Release build, 317 Release tests, three `1.0.2` NuGet packages, win/linux framework-dependent ZIPs, manifest verification, packaged doctor smoke, and packaged sample preview smoke), `eng/publish-nuget.ps1 -DryRun`, `eng/publish-github-release.ps1 -Tag v1.0.2 -DryRun`, and packaged TradeR `ChartView`, `LiveTradeView`, and `MainWindow` preview smokes from `artifacts\executables\avascope-win-x64-framework-dependent\avascope.exe`. The first parallel `LiveTradeView` smoke hit a TradeR build output file lock and passed when rerun serially.
- `2026-06-13`: Started release tracker #43 for `v1.0.2` and closed stale release tracker #42 after confirming `v1.0.1` was already published. Patch scope covers PreviewHost app-level style/control-theme fidelity, Inter font preview parity, and NuGet trusted publishing. Initial validation passed with `dotnet build src\AvaScope.PreviewHost\AvaScope.PreviewHost.csproj`, focused implicit-control-theme PreviewHost regression test, nearby app style/resource PreviewHost tests, and TradeR `ChartView`, `LiveTradeView`, and `MainWindow` preview smokes.
- `2026-06-13`: Published `v1.0.1` from commit `8c496f7ea5f22a1933a3950200ce7aa66037367a` through Release workflow `27462977168` (`push`, success). The workflow published the three `1.0.1` packages to nuget.org and GitHub Packages, created tag `v1.0.1`, uploaded six GitHub Release assets, and `git ls-remote --tags origin refs/tags/v1.0.1` confirmed the tag points at `8c496f7ea5f22a1933a3950200ce7aa66037367a`.
- `2026-06-13`: Local `v1.0.1` release gate passed on the release-candidate working tree after stopping stale artifact-hosted `avascope`/`dotnet` processes from previous local release artifacts. Validation passed with `eng/create-local-release.ps1` (Release build, 316 Release tests, three `1.0.1` NuGet packages, win/linux framework-dependent ZIPs, manifest verification, packaged doctor smoke, and packaged sample preview smoke), `eng/publish-nuget.ps1 -DryRun`, `eng/publish-github-release.ps1 -Tag v1.0.1 -DryRun`, `eng/validate-release-commit.ps1 -Version 1.0.1 -CommitSubject "Release 1.0.1" -RequiredState "Release Candidate"`, and `git diff --check` with only LF/CRLF normalization warnings.
- `2026-06-13`: Created GitHub milestone `v1.0.1`, assigned completed issue #41, created release tracker #42, and added the `v1.0.1` current release target to [RELEASE_PLAN.md](RELEASE_PLAN.md). Patch scope is limited to #41 PreviewHost diagnostic noise reduction and guarded release validation.
- `2026-06-13`: Started GitHub issue #41 after a TradeR `MainWindow.axaml` preview smoke showed App.axaml resources/styles and custom controls render correctly, but preview source diagnostics emitted a false `binding_datatype_path_not_found` warning for `{Binding #MinimizedDialogsButton}`. Implementation classifies hash element-name binding paths as explicit sources while preserving normal DataContext and `x:DataType` diagnostics.
- `2026-06-13`: Completed local validation for GitHub issue #41. Preview diagnostics now treat hash element-name bindings as explicit sources and suppress intentional overlay child overlap diagnostics. Validation passed with focused PreviewHost regression tests (2 passed), full `PreviewHostSmokeTests` (31 passed), TradeR `MainWindow.axaml` preview smoke through the source CLI, `dotnet build AvaScope.slnx --no-restore -v:minimal`, and `git diff --check` with only LF/CRLF normalization warnings. The remaining TradeR preview diagnostics are layout warnings for small text bounds and generic overlapping Border layers, not missing App.axaml styles/resources.
- `2026-06-13`: Completed GitHub issue #36 after remote validation. Commit `61a340f713c57b11b3d434fcce2f1fecdfa6ec49` (`Complete v1 documentation readiness`) passed GitHub CI workflow `27457868832` and Release workflow `27457868833` (no-op). The issue moved to `status:done`, project Done/100/Completed, and was closed. Started GitHub issue #37 `R1.0.0-M4 Release Artifact And Package Verification`; the issue and project card moved to `status:in-progress` / In Progress / 25% / Current Slice.
- `2026-06-13`: Due to GitHub Actions quota pressure during #37, normal development CI was intentionally restricted to manual dispatch and local validation became the required slice gate until the final `v1.0.0` publish. The `Release` workflow remains available for the final release commit and is scoped to `Directory.Build.props` version-bump pushes or manual dispatch.
- `2026-06-13`: Completed local #37 release artifact verification with temporary `Directory.Build.props` version `1.0.0`, then restored the committed version to `0.9.0`. Validation passed with `eng/create-local-release.ps1` (Release build, 312 Release tests, three NuGet packages, win/linux framework-dependent ZIPs, manifest verification, packaged doctor and sample preview smoke), `eng/publish-nuget.ps1 -DryRun`, `eng/publish-github-release.ps1 -Tag v1.0.0 -DryRun`, manifest/hash/package/ZIP inspection, packaged `capabilities --require ...`, packaged MCP stdio smoke with 30 tools and `serverInfo.name=avascope`, opt-in win-x64 self-contained local release plus GitHub Release dry-run, and final framework-dependent artifact restoration. Details are in [RELEASE_ARTIFACT_VERIFICATION.md](RELEASE_ARTIFACT_VERIFICATION.md).
- `2026-06-13`: Completed GitHub issue #37 after local-only validation. Commit `b0bbf6d2d8af9f4ae8a7e8d2366bd83b45f9e9ab` (`Verify v1 release artifacts`) was pushed without triggering GitHub Actions by design. The issue moved to `status:done`, project Done/100/Completed, and was closed. Started GitHub issue #38 `R1.0.0-M5 Post-1.0 Backlog And Deferral Audit`; the issue and project card moved to `status:in-progress` / In Progress / 25% / Current Slice.
- `2026-06-13`: Completed local #38 backlog audit. `gh issue list` found no open `priority:p1` issues and only two open `priority:p0` issues: #33 release tracker and #39 final release/publish slice. All feature-request intake files are already marked implemented. Added [POST_1_0_BACKLOG.md](POST_1_0_BACKLOG.md) with non-blocking post-1.0 deferrals, reasons, priorities, and release-blocking status.
- `2026-06-13`: Completed GitHub issue #38 after local-only validation. Commit `a27c41d` (`Record post-1.0 backlog audit`) was pushed without triggering GitHub Actions by design. The issue moved to `status:done`, project Done/100/Completed, and was closed. Started GitHub issue #39 `R1.0.0-M6 Stable Release Commit And Publication`; the issue and project card moved to `status:in-progress` / In Progress / 25% / Current Slice. The `v1.0.0` release target is now `Release Candidate`, and `Directory.Build.props` is bumped to `1.0.0` in the release-candidate working tree.
- `2026-06-13`: Local #39 release gate passed on the `1.0.0` release-candidate working tree: `dotnet build AvaScope.slnx -v:minimal`, full Debug tests (`dotnet test AvaScope.slnx --no-build`, 314 passed), `eng/create-local-release.ps1` (Release build/test 314 passed, three `1.0.0` packages, win-x64/linux-x64 framework-dependent ZIPs, release manifest, packaged doctor smoke, and packaged sample preview smoke), `eng/publish-nuget.ps1 -DryRun`, `eng/publish-github-release.ps1 -Tag v1.0.0 -DryRun`, release commit guard for `Release 1.0.0`, and `git diff --check` with only LF/CRLF normalization warnings.
- `2026-06-13`: The first hosted Release workflow for `Release 1.0.0` commit `fe90eb9` failed before publish/tag creation in `Create release artifacts` because `Avalonia.Headless.HeadlessUnitTestSession.Dispose()` threw a cleanup-only `NullReferenceException` after `BridgeHeadlessSmokeTests.McpExpandedInputAndRuntimeStateInspectionUseBridgeOnly` completed assertions. Stabilized the test with the existing explicit headless cleanup helper. Post-fix local validation passed with Release build, targeted failing Release test, full `eng/create-local-release.ps1` (314 Release tests plus artifacts and packaged smokes), NuGet/GitHub Release dry-runs, release commit guard, and `git diff --check`.
- `2026-06-13`: Published `v1.0.0` from commit `2736b986db8a003680aaa8996d7093e00eb73374` through Release workflow `27459439796` (`workflow_dispatch`, success). The workflow published the three `1.0.0` packages to nuget.org and GitHub Packages, created tag `v1.0.0`, uploaded six GitHub Release assets, and `git ls-remote --tags origin refs/tags/v1.0.0` confirmed the tag points at `2736b986db8a003680aaa8996d7093e00eb73374`.
- `2026-06-13`: Completed local implementation for GitHub issue #36 `R1.0.0-M3 Documentation Completion`. Updated primary docs for stable v1 positioning, added [UPGRADE.md](UPGRADE.md), removed active public-alpha/pre-1.0 wording from README/User Guide/Agent Workflow/Validation/Security docs, refreshed install/release examples, and added `DocumentationCompletionTests`. Validation passed with primary-doc markdown link/path check (9 files), stale wording `rg` check, `dotnet build AvaScope.slnx --no-restore -v:minimal`, focused documentation tests (`DocumentationCompletionTests`, `StableSurfaceDocumentationTests`, `EndToEndValidationDocumentationTests`, `SecurityThreatModelDocumentationTests`, `VisualRegressionWorkflowDocumentationTests`, `PerformanceStressAuditDocumentationTests`, 12 passed), full Debug tests (`dotnet test AvaScope.slnx --no-build`, 311 passed), `powershell -NoProfile -ExecutionPolicy Bypass -File .\eng\create-local-release.ps1 -SkipTests`, packaged `capabilities --require ...`, and `git diff --check` with only LF/CRLF normalization warnings.
- `2026-06-13`: Completed GitHub issue #35 after remote validation. Final commit `f525e11c4d6496445d1a1bfd9f16527192b5353e` (`Stabilize v1 end-to-end CI validation`) passed GitHub CI workflow `27457413804` and Release workflow `27457413805` (no-op). The issue moved to `status:done`, project Done/100/Completed, and was closed. Started GitHub issue #36 `R1.0.0-M3 Documentation Completion`; the issue and project card moved to `status:in-progress` / In Progress / 25% / Current Slice.
- `2026-06-13`: Stabilized the #35 hosted-runner CI failure from GitHub CI workflow `27457002598`. Two CLI bridge-pipe smoke tests timed out waiting for fake bridge IPC under the full Release CI run; the CLI smoke harness now gives implicit fake bridge manifests an async-test-flow isolated manifest directory instead of a shared static directory. Local validation passed with `dotnet test tests/AvaScope.Tests/AvaScope.Tests.csproj -c Release --no-restore --filter "FullyQualifiedName~AvaScope.Tests.Cli.CliSmokeTests"` (91 passed).
- `2026-06-13`: Completed local validation for GitHub issue #35 `R1.0.0-M2 End-To-End Workflow Verification`. Validation passed with `dotnet restore AvaScope.slnx`, `dotnet build AvaScope.slnx --no-restore -v:minimal`, full Debug tests (`dotnet test AvaScope.slnx --no-build`, 307 passed), `powershell -NoProfile -ExecutionPolicy Bypass -File .\eng\create-local-release.ps1` (Release build/test 307 passed, three `0.9.0` packages, win-x64/linux-x64 framework-dependent ZIPs, release manifest, packaged doctor smoke, packaged sample preview smoke), `powershell -NoProfile -ExecutionPolicy Bypass -File .\eng\publish-github-release.ps1 -Tag v0.9.0 -DryRun`, packaged CLI E2E smoke for capabilities/doctor/preview/animation/preview-session/baseline report-pack/diff/assert-region/cleanup, packaged runtime bridge smoke for launch/attach/tree/find/inspect/audit/input/screenshot/mutation evidence/mutation review/reset/close, packaged MCP stdio `initialize` + `tools/list`, and open P0/P1 issue audit with no unexpected blocker. Added `docs/END_TO_END_VALIDATION.md` with results and residual risks.
- `2026-06-13`: Completed GitHub issue #34 after remote validation. Commit `4408ca7fa284f3d84b6470a8b5d6008478258d55` (`Freeze stable public surface`) passed GitHub CI workflow `27456458960` and Release workflow `27456458959` (no-op). The issue moved to `status:done`, project Done/100/Completed, and was closed. Started GitHub issue #35 `R1.0.0-M2 End-To-End Workflow Verification`; the issue and project card moved to `status:in-progress` / In Progress / 25% / Current Slice.
- `2026-06-13`: Completed local implementation for GitHub issue #34 `R1.0.0-M1 Stable Surface Freeze`. Public surface audit found that existing `launch-app`/`launch_app`, `close-session`/`close_session`, and `mcp` adapter commands were implemented or documented but not represented in `AvaScopeCapabilityCatalog`; the slice aligns capability discovery with actual CLI/MCP surfaces and adds `docs/STABLE_SURFACE.md` plus contract tests for CLI/MCP/package/artifact/release surfaces. Validation passed with `dotnet build AvaScope.slnx --no-restore -v:minimal`, focused #34 tests (`dotnet test AvaScope.slnx --no-build --filter "FullyQualifiedName~StableSurfaceContractTests|FullyQualifiedName~StableSurfaceDocumentationTests|FullyQualifiedName~ProtocolContractTests.CapabilitiesResponseSerializesStableDiscoveryShape|FullyQualifiedName~CliSmokeTests.CapabilitiesCommandReportsProtocolAndToolCapabilities|FullyQualifiedName~McpStdioSmokeTests.ServerStartsOverStdioAndListsInitialTools"`, 8 passed), full Debug tests (`dotnet test AvaScope.slnx --no-build`, 307 passed), and `git diff --check` with only LF/CRLF normalization warnings.
- `2026-06-13`: Published `v0.9.0`. Final release commit `f956e48a99304310abf924c3ac7f91ec0abc21c7` (`Release 0.9.0`) passed GitHub CI workflow `27455900556` and Release workflow `27455900554`; `gh release view v0.9.0` confirmed https://github.com/RolandUI/AvaScope/releases/tag/v0.9.0 with six assets, and `git ls-remote --tags origin refs/tags/v0.9.0` confirmed the tag points at the release commit. Closed GitHub issues #32 and #26, moved their project cards to Done/100/Completed, closed milestone `v0.9.0`, and started GitHub issue #34 `R1.0.0-M1 Stable Surface Freeze`; the issue and project card moved to `status:in-progress` / In Progress / 25% / Current Slice.
- `2026-06-13`: Stabilized the `v0.9.0` release commit hosted-runner failures from GitHub CI `27455464470` and Release workflow `27455464485`. The failing bridge input smoke now uses the existing explicit headless-session cleanup helper, and `PreviewImageDifferTests` now retries temp directory deletion to avoid Windows image-file cleanup locks. Local validation passed with `dotnet build AvaScope.slnx -c Release --no-restore -v:minimal`, targeted Release tests (`dotnet test tests/AvaScope.Tests/AvaScope.Tests.csproj -c Release --no-build --filter "FullyQualifiedName~BridgeHeadlessSmokeTests.McpInputClicksButtonAndTypesTextThroughLocalBridgePipe|FullyQualifiedName~PreviewImageDifferTests.CompareAppliesIgnoredRegionsAndThresholds"`, 2 passed), full Release tests (`dotnet test AvaScope.slnx -c Release --no-build`, 302 passed), `powershell -NoProfile -ExecutionPolicy Bypass -File .\eng\create-local-release.ps1`, `powershell -NoProfile -ExecutionPolicy Bypass -File .\eng\publish-github-release.ps1 -Tag v0.9.0 -DryRun`, and release commit guard for `Release 0.9.0`.
- `2026-06-13`: `v0.9.0` release-candidate gate passed for GitHub issue #32. Validation included `dotnet build AvaScope.slnx --no-restore -v:minimal`, full Debug tests (`dotnet test AvaScope.slnx --no-build`, 302 passed), `powershell -NoProfile -ExecutionPolicy Bypass -File .\eng\create-local-release.ps1` (Release build/test 302 passed, three `0.9.0` packages, win-x64/linux-x64 framework-dependent ZIPs, release manifest, packaged doctor smoke, and packaged sample preview smoke), `powershell -NoProfile -ExecutionPolicy Bypass -File .\eng\publish-github-release.ps1 -Tag v0.9.0 -DryRun`, and release commit guard (`powershell -NoProfile -ExecutionPolicy Bypass -File .\eng\validate-release-commit.ps1 -Version 0.9.0 -CommitSubject "Release 0.9.0" -RequiredState "Release Candidate"`). Open P0/P1 check found no unexpected blocker in `v0.9.0`; remaining open P0/P1 issues are planned `v1.0.0` work plus the expected `v0.9.0` release tracker/gate issues.
- `2026-06-13`: Completed GitHub issue #31 after remote validation. Commits `8225751f44b3217fc51d3789c8ac7dab83157c22`, `223b3806b172dcf1e8d0527cf2b8dfee6de731ef`, and `af18f31e037290e1a8b77cc6d88c8c480e06564b` completed the stress audit coverage, troubleshooting docs, and hosted-runner stabilization. GitHub CI workflow `27455089029` passed Build, Test, Pack, executable packaging, and artifact verification; Release workflow `27455089023` passed/no-op. The issue moved to `status:done`, project Done/100/Completed, and was closed. Started GitHub issue #32 `R0.9.0-M6 Release Candidate And Version Bump`; the issue and project card moved to `status:in-progress` / In Progress / 25% / Current Slice.
- `2026-06-13`: Stabilized the remaining #31 hosted-runner CI failure from workflow `27454736940`. `CliSmokeTests.InspectNodeCommandReadsNodeThroughBridgePipe` now uses a per-test `--manifest-dir` directory for fake bridge IPC manifests, and `BridgeHeadlessSmokeTests.RuntimeMutationContractReturnsNoOpUnsupportedAndStaleDiagnostics` now uses the existing explicit headless-session cleanup helper so cleanup-only Avalonia headless disposal failures do not mask dispatch assertions. Local validation passed with `dotnet build AvaScope.slnx -c Release --no-restore -v:minimal`, the failed Release tests (`dotnet test tests/AvaScope.Tests/AvaScope.Tests.csproj -c Release --no-build --filter "FullyQualifiedName~CliSmokeTests.InspectNodeCommandReadsNodeThroughBridgePipe|FullyQualifiedName~BridgeHeadlessSmokeTests.RuntimeMutationContractReturnsNoOpUnsupportedAndStaleDiagnostics"`, 3 passed), full Release tests (`dotnet test AvaScope.slnx -c Release --no-build`, 302 passed), and `git diff --check` with only LF/CRLF normalization warnings.
- `2026-06-13`: Stabilized the #31 hosted-runner CI failure from workflow `27454355662`. Two existing CLI bridge-pipe smoke tests now use explicit per-test `--manifest-dir` directories instead of the shared default test manifest directory, removing parallel full-suite state coupling without changing product behavior. Local validation passed with `dotnet build AvaScope.slnx -c Release --no-restore -v:minimal`, the two failing Release tests (`dotnet test AvaScope.slnx -c Release --no-build --filter "FullyQualifiedName~CliSmokeTests.CloseSessionCommandClosesThroughBridgePipe|FullyQualifiedName~CliSmokeTests.MutationReviewCommandReadsHistoryAndWritesArtifactThroughBridgePipe"`, 2 passed), focused #31 Release tests (8 passed), and full Release tests (`dotnet test AvaScope.slnx -c Release --no-build`, 302 passed).
- `2026-06-13`: Completed local implementation for GitHub issue #31 `R0.9.0-M5 Performance, Stress, Samples, And Troubleshooting Audit`. Added targeted stress coverage for large UI audit trees, large diagnostics payloads, repeated one-shot preview reloads with persistent restore, preview-session store diagnostic/cleanup budgets, large baseline suite expansion, and repeated bridge runtime mutation/reset cycles. Added `docs/PERFORMANCE_STRESS_AUDIT.md` for bounded output budgets and `docs/TROUBLESHOOTING.md` for attach, preview, mutation, report, and package failure triage, linked both from README/user docs, and added documentation coverage tests. Validation passed with `dotnet build AvaScope.slnx --no-restore -v:minimal`, focused #31 tests (`dotnet test AvaScope.slnx --no-build --filter "FullyQualifiedName~PerformanceStressAuditTests|FullyQualifiedName~BridgeHeadlessSmokeTests.RuntimeMutationRepeatedSetPropertyAndResetAllKeepsReviewBounded|FullyQualifiedName~PerformanceStressAuditDocumentationTests"`, 8 passed), full Debug tests (`dotnet test AvaScope.slnx --no-build`, 302 passed), and `git diff --check` with only LF/CRLF normalization warnings.
- `2026-06-13`: Completed GitHub issue #30 after remote validation. Commit `3ec427044ef0da8d3b8d844181c2ba2e6b9fb5f1` added the security threat model, compatibility audit notes, local-only/opt-in bridge validation, and documentation tests. Follow-up commit `6e19a9ff72d08606d0f3330f358484bdcb10b659` stabilized the hosted Release headless cleanup path in the MCP mutation evidence smoke test. GitHub CI workflow `27453875771` passed Build, Test, Pack, executable packaging, and artifact verification; Release workflow `27453875769` passed/no-op. The issue moved to `status:done`, project Done/100/Completed, and was closed. Started GitHub issue #31 `R0.9.0-M5 Performance, Stress, Samples, And Troubleshooting Audit`; the issue and project card moved to `status:in-progress` / In Progress / 25% / Current Slice.
- `2026-06-13`: Completed local implementation for GitHub issue #30 `R0.9.0-M4 Security, Safety, And Compatibility Audit`. Added `docs/SECURITY_THREAT_MODEL.md` covering local-only transports, opt-in bridge activation, runtime mutation permissions, PreviewHost execution, file outputs/logs, package/API/CLI/MCP compatibility, unsafe defaults, and accepted risks/deferrals. Linked it from README and the user guide, added validation guidance, added documentation coverage tests, asserted bridge default inactivity metadata, and added protocol coverage for unsupported transport-scope rejection. Validation passed with `dotnet build AvaScope.slnx --no-restore -v:minimal`, focused #30 tests (`dotnet test AvaScope.slnx --no-build --filter "FullyQualifiedName~SecurityThreatModelDocumentationTests|FullyQualifiedName~AvaScopeBridgeTests.BridgeIsInactiveByDefault|FullyQualifiedName~AvaScopeBridgeTests.ActivateCreatesLocalOnlyRuntimeSession|FullyQualifiedName~ProtocolContractTests.BridgeSessionManifestRejectsUnsupportedTransportScope|FullyQualifiedName~LocalBridgeClientTests.MutateNodeRejectsSessionMismatchWithoutIpc|FullyQualifiedName~LocalBridgeClientTests.DiagnosticsReportsInvalidAndStaleManifestsWithoutThrowing|FullyQualifiedName~CapabilityCompatibilityCheckerTests"`, 11 passed), full Debug tests (`dotnet test AvaScope.slnx --no-build`, 294 passed), and `git diff --check` with only LF/CRLF normalization warnings.
- `2026-06-13`: Completed GitHub issue #29 after remote validation. Commit `b87ec425a1da762dab1eab0f908a9af942b07248` added capability discovery and unsupported requirement diagnostics across Protocol, Core, CLI, and MCP. GitHub CI workflow `27453208153` passed Build, Test, Pack, executable packaging, and artifact verification; Release workflow `27453208143` passed/no-op. The issue moved to `status:done`, project Done/100/Completed, and was closed. Started GitHub issue #30 `R0.9.0-M4 Security, Safety, And Compatibility Audit`; the issue and project card moved to `status:in-progress` / In Progress / 25% / Current Slice.
- `2026-06-13`: Completed local implementation for GitHub issue #29 `R0.9.0-M3 Protocol Capability And Versioning Contract`. Added `AvaScopeCapabilitiesResponse`, generic capability/tool DTOs, protocol capability ids/status/error codes, full capability catalog, Core `CapabilityCompatibilityChecker`, CLI `capabilities [--require ...]`, MCP `capabilities`, and docs for agent/user validation workflows. The manifest covers protocol compatibility policy, CLI/MCP tools, runtime mutation, preview, diagnostics, baseline, report, artifact, and local-only safety capabilities; unsupported requirements return `capability_not_supported` with requested/unsupported/available capability details and next action. Validation passed with `dotnet build AvaScope.slnx --no-restore -v:minimal`, focused #29 tests (`dotnet test AvaScope.slnx --no-build --filter "FullyQualifiedName~ProtocolContractTests.CapabilitiesResponseSerializesStableDiscoveryShape|FullyQualifiedName~CapabilityCompatibilityCheckerTests|FullyQualifiedName~CliSmokeTests.CapabilitiesCommandReportsProtocolAndToolCapabilities|FullyQualifiedName~CliSmokeTests.CapabilitiesCommandRejectsUnsupportedRequiredCapability|FullyQualifiedName~AvaScopeMcpToolsTests.Capabilities|FullyQualifiedName~McpStdioSmokeTests.ServerStartsOverStdioAndListsInitialTools"`, 10 passed), full Debug tests (`dotnet test AvaScope.slnx --no-build`, 291 passed after one cleanup-lock retry for an existing image-diff test), and `git diff --check` with only LF/CRLF normalization warnings.
- `2026-06-13`: Completed GitHub issue #28 after remote validation. Commit `49ea0ebec43521c00fa28beb2bd458c3010541b4` added runtime accessibility/validation snapshots, bounded UI audit protocol contracts, Core `UiAuditBuilder`, CLI `audit-ui`, MCP `audit_ui`, and documentation. Local validation passed with `dotnet build AvaScope.slnx --no-restore -v:minimal`, focused #28 tests (8 passed), full Debug tests (`dotnet test AvaScope.slnx --no-build`, 282 passed), and `git diff --check` with only LF/CRLF normalization warnings. GitHub CI workflow `27452626645` passed Build, Test, Pack, executable packaging, and artifact verification; Release workflow `27452626646` passed/no-op. The issue moved to `status:done`, project Done/100/Completed, and was closed. Started GitHub issue #29 `R0.9.0-M3 Protocol Capability And Versioning Contract`; the issue and project card moved to `status:in-progress` / In Progress / 25% / Current Slice.
- `2026-06-13`: Completed GitHub issue #27 after remote validation. Commit `4b068a1ae71be81676b00bddd80edf29fb65b2a3` added source-aware mutation review suggestions, follow-up commit `3168523955e82bfe3362165cc078c3ea67de711f` stabilized the hosted-runner headless cleanup path, GitHub CI workflow `27451948810` passed, and Release workflow `27451948781` passed with no release needed. The issue moved to `status:done`, project Done/100/Completed, and was closed. Started GitHub issue #28 `R0.9.0-M2 Accessibility, Validation, And Component Inventory`; the issue and project card moved to `status:in-progress` / In Progress / 25% / Current Slice.
- `2026-06-13`: Stabilized the #27 hosted-runner CI failure from workflow `27451538632`. The source-aware MCP mutation smoke now disposes the Avalonia headless session manually so cleanup-only `HeadlessUnitTestSession.Dispose()` null-reference failures after explicit window/bridge cleanup do not mask dispatch-body assertions. Local validation passed with `dotnet build AvaScope.slnx -c Release --no-restore -v:minimal`, the failing Release test (`dotnet test AvaScope.slnx -c Release --no-build --filter FullyQualifiedName~BridgeHeadlessSmokeTests.McpMutateNodeReturnsBoundedMutationContractResultsThroughLocalBridgePipe`, 1 passed), full Release tests (`dotnet test AvaScope.slnx -c Release --no-build`, 277 passed), `dotnet build AvaScope.slnx --no-restore -v:minimal`, and full Debug tests (`dotnet test AvaScope.slnx --no-build`, 277 passed).
- `2026-06-13`: Completed local implementation for GitHub issue #27 `R0.9.0-M1 Source-Aware Change Suggestions`. Added additive `RuntimeSourceSuggestionContext` and `RuntimeSourceSuggestion` protocol contracts, a Core source-suggestion builder, bridge/CLI/MCP mutation-review integration, HTML review source-suggestion output, CLI `--source-project` / `--source-view` / `--source-app` / `--source-profile` options, and agent/user/validation documentation. The feature remains advisory and never mutates source files automatically. Validation passed with `dotnet build AvaScope.slnx --no-restore -v:minimal`, focused #27 tests (`dotnet test AvaScope.slnx --no-build --filter "FullyQualifiedName~ProtocolContractTests.RuntimeMutationReviewResponseSerializesStableShape|FullyQualifiedName~RuntimeSourceSuggestionBuilderTests|FullyQualifiedName~CliSmokeTests.MutationReviewCommandReadsHistoryAndWritesArtifactThroughBridgePipe|FullyQualifiedName~BridgeHeadlessSmokeTests.McpMutateNodeReturnsBoundedMutationContractResultsThroughLocalBridgePipe|FullyQualifiedName~McpStdioSmokeTests.ServerStartsOverStdioAndListsInitialTools"`, 6 passed), full Debug tests (`dotnet test AvaScope.slnx --no-build`, 277 passed), and `git diff --check` with only LF/CRLF normalization warnings.
- `2026-06-13`: Closed GitHub issues #25 and #19, moved their project cards to Done/100/Completed, closed milestone `v0.8.0`, and started GitHub issue #27 `R0.9.0-M1 Source-Aware Change Suggestions`. The issue and project card moved to `status:in-progress` / In Progress / 25% / Current Slice; `docs/RELEASE_PLAN.md` now marks `v0.9.0` as the current in-progress target.
- `2026-06-13`: Published `v0.8.0`. Release commit `d2d4d01652efaf2812acf95a6b60621c93352ada` (`Release 0.8.0`) passed GitHub CI workflow `27450680002` and Release workflow `27450679987`; `gh release view v0.8.0` confirmed https://github.com/RolandUI/AvaScope/releases/tag/v0.8.0 with six assets, and `git ls-remote --tags origin refs/tags/v0.8.0` confirmed the tag points at the release commit.
- `2026-06-13`: `v0.8.0` release-candidate gate passed for GitHub issue #25. Validation included `dotnet build AvaScope.slnx --no-restore -v:minimal`, full Debug tests (`dotnet test AvaScope.slnx --no-build`, 275 passed), `powershell -NoProfile -ExecutionPolicy Bypass -File .\eng\create-local-release.ps1` (Release build/test 275 passed, three `0.8.0` packages, win-x64/linux-x64 framework-dependent ZIPs, release manifest, packaged doctor smoke, and packaged sample preview smoke), `powershell -NoProfile -ExecutionPolicy Bypass -File .\eng\publish-github-release.ps1 -Tag v0.8.0 -DryRun`, packaged CLI suite/report smoke (`baseline-create --suite`, `baseline-check --report --report-pack`, 2 entries passed), report/report-pack JSON validation (`agentReview=passed`, report pack `status=passed`, JSON/HTML/JUnit/SARIF assets present), release commit guard, and `git diff --check` with only LF/CRLF normalization warnings.
- `2026-06-13`: Completed GitHub issue #24 after remote validation. Commit `9de95b0dc542562e2bcf4384f34c22ce7b709eba` passed GitHub CI workflow `27449993825` and Release workflow `27449993838`; the issue was moved to `status:done`, project Done/100/Completed, and closed. Started GitHub issue #25 `R0.8.0-M6 Release Candidate And Version Bump`, moved the issue and project card to `status:in-progress` / `In Progress`, marked `v0.8.0` as `Release Candidate` in `docs/RELEASE_PLAN.md`, bumped `Directory.Build.props` to `0.8.0` in the working tree, and scoped validation to the release gate plus packaged CLI suite/report smoke before committing `Release 0.8.0`.
- `2026-06-12`: Completed local implementation for GitHub issue #24 `R0.8.0-M5 MCP/CLI Agent Review Surface`. Added additive protocol `agentReview` surfaces for baseline checks, preview viewers, runtime mutations, mutation evidence, and mutation review; the surfaces expose bounded headlines, summary lines, failure shortlists, mutation summaries, report paths, artifact paths, and local review/preview URLs without changing existing `ToolResult<T>` compatibility or inlining large payloads. Updated agent/user/CI/validation documentation to direct agents to `agentReview` before full reports. Validation passed with `dotnet build AvaScope.slnx --no-restore -v:minimal`, focused protocol/CLI/MCP/headless tests (`dotnet test AvaScope.slnx --no-build --filter "FullyQualifiedName~ProtocolContractTests.RuntimeMutationRequestAndResponseSerializeStableShapes|FullyQualifiedName~ProtocolContractTests.RuntimeMutationEvidenceResponseSerializesStableShape|FullyQualifiedName~ProtocolContractTests.RuntimeMutationReviewResponseSerializesStableShape|FullyQualifiedName~ProtocolContractTests.PreviewViewerResponseSerializesStableShape|FullyQualifiedName~ProtocolContractTests.PreviewBaselineResponsesSerializeStableShapes|FullyQualifiedName~CliSmokeTests.PreviewSessionCommandsCreateListReloadAndClosePersistedSession|FullyQualifiedName~CliSmokeTests.BaselineCommandsCreateManifestPassCheckAndFailChangedCheck|FullyQualifiedName~CliSmokeTests.MutateNodeEvidenceCommandCapturesSequencedArtifactsThroughBridgePipe|FullyQualifiedName~CliSmokeTests.MutationReviewCommandReadsHistoryAndWritesArtifactThroughBridgePipe|FullyQualifiedName~AvaScopeMcpToolsTests.BaselineCheckWritesReportAndReportPackPathsThroughPreviewHost|FullyQualifiedName~AvaScopeMcpToolsTests.PreviewViewerExportsFileBackedUrlForPreviewSession|FullyQualifiedName~BridgeHeadlessSmokeTests.McpMutateNodeReturnsBoundedMutationContractResultsThroughLocalBridgePipe|FullyQualifiedName~BridgeHeadlessSmokeTests.McpMutateNodeEvidenceCapturesScreenshotsTreesAndDiffThroughLocalBridgePipe"`, 13 passed), full Debug tests (`dotnet test AvaScope.slnx --no-build`, 275 passed), `dotnet build AvaScope.slnx -c Release --no-restore -v:minimal`, full Release tests (`dotnet test AvaScope.slnx -c Release --no-build`, 275 passed), and `git diff --check` with only LF/CRLF normalization warnings.
- `2026-06-12`: Completed GitHub issue #23 after remote validation. Commits `f13dbb2e398e9d2e9fb663b9fa5d12cbc56fb79a`, `3a14f729631c09e21590855b293d80f25ca02126`, and `558e530925f6309b93228617af032075f4531997` passed GitHub CI run `27449167798` (Restore, Build, Test, Pack libraries, Package executables, Verify artifacts) and GitHub Release workflow `27449167784`; the issue was moved to `status:done`, project Done/100/Completed, and closed. Started GitHub issue #24 `R0.8.0-M5 MCP/CLI Agent Review Surface`, moved the issue and project card to `status:in-progress` / `In Progress`, and scoped implementation to additive bounded review-summary metadata for CLI/MCP validation outputs, report/artifact path handoff, mutation summaries, and local review URLs.
- `2026-06-12`: Stabilized the next #23 hosted-runner CI failures from run `27448610779`. The watch-preview CLI smoke now uses a longer settle window so repeated file-change writes finish before the reload render starts, and the MCP mutation headless smoke now performs explicit registration/window/bridge cleanup before disposing the Avalonia headless session. Local validation passed with `dotnet build AvaScope.slnx -c Release --no-restore -v:minimal`, the two failing Release tests (`dotnet test AvaScope.slnx -c Release --no-build --filter "FullyQualifiedName~CliSmokeTests.WatchPreviewSessionCommandReloadsWhenWatchedFileChanges|FullyQualifiedName~BridgeHeadlessSmokeTests.McpMutateNodeReturnsBoundedMutationContractResultsThroughLocalBridgePipe"`, 2 passed), full Release tests (`dotnet test AvaScope.slnx -c Release --no-build`, 275 passed), full Debug tests (`dotnet test AvaScope.slnx`, 275 passed), and `git diff --check` with only LF/CRLF normalization warnings.
- `2026-06-12`: Stabilized the #23 hosted-runner CI failure in `LocalBridgeClientTests.MutationReviewReadsBoundedHistoryThroughBridgePipe`. The fake named-pipe server already used a 30 second timeout, while the fake-pipe client paths still used the production default 5 second IPC timeout; the fake-pipe smoke tests now pass the same bounded test timeout explicitly without changing production defaults. Local validation passed with the failing Release test (`dotnet test AvaScope.slnx -c Release --no-build --filter FullyQualifiedName~LocalBridgeClientTests.MutationReviewReadsBoundedHistoryThroughBridgePipe`, 1 passed), the full `LocalBridgeClientTests` Release class (`dotnet test AvaScope.slnx -c Release --no-build --filter FullyQualifiedName~LocalBridgeClientTests`, 22 passed), `dotnet build AvaScope.slnx -c Release --no-restore -v:minimal`, full Debug tests (`dotnet test AvaScope.slnx --no-build`, 275 passed), and `git diff --check` with only LF/CRLF normalization warnings.
- `2026-06-12`: Completed local implementation for GitHub issue #23 `R0.8.0-M4 GitHub Actions Example And Artifact Upload`. Added a documented non-publishing GitHub Actions visual-regression workflow example under `docs/examples/github-actions/avascope-visual-regression.yml`, updated `docs/VISUAL_REGRESSION_CI.md` with local/CI usage, expected failure semantics, artifact review guidance, and release-workflow separation, and added documentation tests that guard against publish credentials or publish scripts in the sample workflow. Validation passed with `dotnet build AvaScope.slnx --no-restore -v:minimal`, targeted docs/workflow tests (`dotnet test AvaScope.slnx --no-build --filter FullyQualifiedName~VisualRegressionWorkflowDocumentationTests`, 2 passed), a local Visual Regression CI doc/example path check, full Debug tests (`dotnet test AvaScope.slnx --no-build`, 275 passed), and `git diff --check` with only LF/CRLF normalization warnings.
- `2026-06-12`: Completed GitHub issue #22 after remote validation. Commit `0369d0a1e1b1868663892da0c4cdabdac61e0ea5` passed GitHub CI run `27447457369` (Restore, Build, Test, Pack libraries, Package executables, Verify artifacts) and GitHub Release workflow `27447457350`; the issue was moved to `status:done`, project Done/100/Completed, and closed. Started GitHub issue #23 `R0.8.0-M4 GitHub Actions Example And Artifact Upload`, moved the issue and project card to `status:in-progress` / `In Progress`, and scoped implementation to a non-publishing visual-regression GitHub Actions example plus documentation/path validation.
- `2026-06-12`: Completed local implementation for GitHub issue #22 `R0.8.0-M3 Agent Evidence Report Pack`. Added `baseline-check --report-pack <dir>` through Core/CLI/MCP with bounded `reportPack` response metadata, uploadable JSON/HTML/JUnit/SARIF-style assets, grouped failure summaries, environment metadata, baseline/current/diff image path handoff, suite mutation provenance metadata, and compatibility with the existing `--report` JSON file. Validation passed with `dotnet build AvaScope.slnx --no-restore -v:minimal`, focused #22 tests (`dotnet test AvaScope.slnx --no-build --filter "FullyQualifiedName~PreviewBaselineReportPackExporterTests|FullyQualifiedName~ProtocolContractTests.PreviewBaselineResponsesSerializeStableShapes|FullyQualifiedName~PreviewBaselineManagerTests|FullyQualifiedName~CliSmokeTests.BaselineCommandsCreateManifestPassCheckAndFailChangedCheck|FullyQualifiedName~CliSmokeTests.BaselineSuiteCommandCreatesManifestAndCheckPasses|FullyQualifiedName~AvaScopeMcpToolsTests.BaselineCheckWritesReportAndReportPackPathsThroughPreviewHost|FullyQualifiedName~McpStdioSmokeTests.ServerStartsOverStdioAndListsInitialTools"`, 8 passed), full Debug tests (`dotnet test AvaScope.slnx --no-build`, 273 passed), and `git diff --check` with only LF/CRLF normalization warnings.
- `2026-06-12`: Started GitHub issue #22 for `R0.8.0-M3 Agent Evidence Report Pack`. Moved the issue and project card to `status:in-progress` / `In Progress`, recorded intended validation in the issue, and scoped implementation to optional report-pack outputs for baseline suite checks first, bounded JSON/HTML/JUnit/SARIF-style assets, path handoff through CLI responses, and compatibility with existing `baseline-check --report`.
- `2026-06-12`: Completed local implementation for GitHub issue #21 `R0.8.0-M2 Thresholds, Masks, And Region Rules`. Added manifest-backed `comparisonRules` with tolerance, max changed pixel/percent thresholds, ignored regions, required region assertions, deterministic required-region crop artifacts, diff response threshold/mask metadata, suite default/entry/variant rule merging, bounded out-of-bounds mask diagnostics, and user/agent/validation documentation. Validation passed with `dotnet build AvaScope.slnx --no-restore -v:minimal`, focused #21 tests (`dotnet test AvaScope.slnx --no-build --filter "FullyQualifiedName~PreviewImageDifferTests|FullyQualifiedName~PreviewBaselineManagerTests|FullyQualifiedName~ProtocolContractTests.PreviewBaselineResponsesSerializeStableShapes|FullyQualifiedName~ProtocolContractTests.PreviewBaselineSuiteManifestSerializesStableShape|FullyQualifiedName~ProtocolContractTests.PreviewComparisonRulesAndRegionResultsSerializeStableShape|FullyQualifiedName~CliSmokeTests.BaselineCommandsCreateManifestPassCheckAndFailChangedCheck|FullyQualifiedName~CliSmokeTests.BaselineSuiteCommandCreatesManifestAndCheckPasses"`, 9 passed), full Debug tests (`dotnet test AvaScope.slnx --no-build`, 271 passed), and `git diff --check` with only LF/CRLF normalization warnings.
- `2026-06-12`: Started GitHub issue #21 for `R0.8.0-M2 Thresholds, Masks, And Region Rules`. Moved the issue and project card to `status:in-progress` / `In Progress`, recorded intended validation in the issue, and scoped implementation to structured comparison controls for baseline suite entries and variants, deterministic artifacts, default strict compatibility, and reuse of existing diff/region primitives where practical.
- `2026-06-12`: Completed local implementation for GitHub issue #20 `R0.8.0-M1 Baseline Suite Manifest`. Added structured suite manifest protocol DTOs, baseline entry provenance metadata, Core suite parsing and deterministic expansion, CLI `baseline-create --suite`, compatibility with existing `baseline-check --manifest`, focused Core/Protocol/CLI coverage, and agent/user/validation documentation. Validation passed with `dotnet build AvaScope.slnx --no-restore -v:minimal`, focused suite/baseline tests (`dotnet test AvaScope.slnx --no-build --filter "FullyQualifiedName~PreviewBaselineManagerTests|FullyQualifiedName~ProtocolContractTests.PreviewBaselineResponsesSerializeStableShapes|FullyQualifiedName~ProtocolContractTests.PreviewBaselineSuiteManifestSerializesStableShape|FullyQualifiedName~CliSmokeTests.BaselineCommandsCreateManifestPassCheckAndFailChangedCheck|FullyQualifiedName~CliSmokeTests.BaselineSuiteCommandCreatesManifestAndCheckPasses"`, 6 passed), full Debug tests (`dotnet test AvaScope.slnx --no-build`, 268 passed), and `git diff --check` with only LF/CRLF normalization warnings.
- `2026-06-12`: Started GitHub issue #20 for `R0.8.0-M1 Baseline Suite Manifest`. Moved the issue and project card to `status:in-progress` / `In Progress`, recorded intended validation in the issue, and scoped implementation to structured suite manifests, deterministic per-variant expansion, output path generation, mutation preset references, invalid diagnostics, and compatibility with existing single-view baseline commands.
- `2026-06-12`: GitHub Release workflow `27443577851` passed for `Release 0.7.0`; tag `v0.7.0` and six GitHub Release assets were published at `2026-06-12T21:24:17Z`. GitHub CI workflow `27443577826` failed after publish in `LocalBridgeClientTests.DiagnosticsReportsDuplicateAndIncompatibleBridgeManifests` because the fake incompatible-bridge named-pipe test used a 100 ms timeout, and in `BridgeHeadlessSmokeTests.ScreenshotCaptureForMissingTopLevelReturnsStructuredError` because the headless session disposed from a no-window path on the hosted runner. Post-release CI stabilization removed the artificial 100 ms pipe timeout, initialized the missing-top-level screenshot smoke with a minimal headless window, and passed local validation with `dotnet build AvaScope.slnx -c Release --no-restore -v:minimal`, the two targeted failing tests, and `dotnet test AvaScope.slnx -c Release --no-build` with 264 tests.
- `2026-06-12`: Post-release CI stabilization commit `07367e2` passed GitHub CI workflow `27444154586` on `master`; Restore, Build, Test, Pack libraries, Package executables, and Verify artifacts all succeeded. Follow-up Release workflow `27444154583` no-oped successfully because `v0.7.0` already existed.
- `2026-06-12`: Remote GitHub CI `27443036755` and Release `27443036762` failed during Release tests because `CliSmokeTests.WatchPreviewSessionCommandReloadsWhenWatchedFileChanges` reloaded while `MainView.axaml` was transiently locked on the hosted runner. Increased the watcher smoke settle window to `3000ms`, switched its cleanup to the existing retry helper, and validated the watcher plus `launch-app` lifecycle smoke in Release configuration 3 consecutive times.
- `2026-06-12`: `v0.7.0` release-candidate gate passed. Validation included `dotnet build AvaScope.slnx --no-restore -v:minimal`, targeted `launch-app` lifecycle smoke tests (2 passed), full Debug tests (`dotnet test AvaScope.slnx --no-build`, 264 passed), `powershell -NoProfile -ExecutionPolicy Bypass -File .\eng\create-local-release.ps1` (Release build/test 264 passed, three `0.7.0` packages, win-x64/linux-x64 framework-dependent ZIPs, manifest, packaged doctor smoke, packaged sample preview smoke), `powershell -NoProfile -ExecutionPolicy Bypass -File .\eng\publish-github-release.ps1 -Tag v0.7.0 -DryRun`, packaged Windows CLI runtime smoke (`launch-app`, `attach`, `list-top-levels`, `visual-tree`, `find-nodes`, `mutate-node`, `screenshot`, `mutate-node-evidence`, `mutation-review`, `reset_all`, `close-session`), and `git diff --check` with only line-ending normalization warnings.
- `2026-06-12`: Started GitHub issue #14 for the `v0.7.0` release candidate and version bump. Moved the issue and project card to `status:in-progress` / `In Progress`, marked the release plan as `Release Candidate`, bumped the working-tree package version to `0.7.0`, and scoped validation to the release gate plus packaged CLI runtime smoke before committing `Release 0.7.0`.
- `2026-06-12`: Completed GitHub issue #13 for CLI/MCP runtime experiment review. Added session-local bounded runtime mutation history, active override summaries, reset handoff metadata, bridge IPC/Core client `mutation_review`, CLI `mutation-review`, MCP `mutation_review`, HTML review artifact export for evidence and session review responses, and agent/user documentation updates. Validation passed with `dotnet build AvaScope.slnx --no-restore -v:minimal`, focused runtime review tests (`dotnet test AvaScope.slnx --no-build --filter "FullyQualifiedName~ProtocolContractTests.RuntimeMutationReviewResponseSerializesStableShape|FullyQualifiedName~ProtocolContractTests.RuntimeMutationEvidenceResponseSerializesStableShape|FullyQualifiedName~LocalBridgeClientTests.MutationReviewReadsBoundedHistoryThroughBridgePipe|FullyQualifiedName~LocalBridgeClientTests.RuntimeMutationEvidenceRunnerCapturesSequencedArtifactsThroughBridgePipe|FullyQualifiedName~CliSmokeTests.MutationReviewCommandReadsHistoryAndWritesArtifactThroughBridgePipe|FullyQualifiedName~CliSmokeTests.MutateNodeEvidenceCommandCapturesSequencedArtifactsThroughBridgePipe|FullyQualifiedName~BridgeHeadlessSmokeTests.McpMutateNodeReturnsBoundedMutationContractResultsThroughLocalBridgePipe|FullyQualifiedName~BridgeHeadlessSmokeTests.McpMutateNodeEvidenceCapturesScreenshotsTreesAndDiffThroughLocalBridgePipe|FullyQualifiedName~McpStdioSmokeTests.ServerStartsOverStdioAndListsInitialTools"`, 9 passed), full Debug tests (`dotnet test AvaScope.slnx --no-build`, 263 passed), and `git diff --check` (LF/CRLF warnings only).
- `2026-06-12`: Started GitHub issue #13 for CLI/MCP runtime experiment review. Moved the issue and project card to `status:in-progress` / `In Progress`, recorded intended validation in the issue, and scoped implementation to mutation history, active override summaries, reset handoff, and local review artifact paths.
- `2026-06-12`: Completed GitHub issue #12 for agent session safety and reset semantics. Added explicit local-only/temporary/reversible capability metadata, active mutation count and session/target metadata, closed-session mutation rejection, successful-reset-only registry removal, top-level unregister cleanup, and close/deactivate mutation reset cleanup. Updated safety docs to state runtime mutations are temporary local overrides. Validation passed with `dotnet build AvaScope.slnx --no-restore -v:minimal`, focused safety/reset tests (`dotnet test AvaScope.slnx --no-build --filter "FullyQualifiedName~ProtocolContractTests.RuntimeMutationRequestAndResponseSerializeStableShapes|FullyQualifiedName~LocalBridgeClientTests.MutateNodeSendsStructuredMutationThroughBridgePipe|FullyQualifiedName~CliSmokeTests.MutateNodeCommandSendsResetMutationThroughBridgePipe|FullyQualifiedName~BridgeHeadlessSmokeTests.RuntimeMutationDeactivateResetsActiveMutationsAndRejectsFurtherMutation|FullyQualifiedName~BridgeHeadlessSmokeTests.RuntimeMutationTopLevelRegistrationDisposeResetsScopedMutations|FullyQualifiedName~BridgeHeadlessSmokeTests.McpMutateNodeReturnsBoundedMutationContractResultsThroughLocalBridgePipe|FullyQualifiedName~BridgeHeadlessSmokeTests.RuntimeMutationAppliesClassesResourcesTextAndScreenshotObservableBackgroundThenResetAll"`, 7 passed), full Debug tests (`dotnet test AvaScope.slnx --no-build`, 260 passed), and `git diff --check` (LF/CRLF warnings only).
- `2026-06-12`: Started GitHub issue #12 for agent session safety and reset semantics. Moved the issue and project card to `status:in-progress` / `In Progress`, recorded the intended validation in the issue, and scoped implementation to capability discovery, mutation-state scoping, reset/close cleanup, stale diagnostics, and safety documentation.
- `2026-06-12`: Completed GitHub issue #11 for the mutation evidence and screenshot loop. Added `RuntimeMutationEvidence*` protocol DTOs, a reusable `RuntimeMutationEvidenceRunner`, CLI `mutate-node-evidence`, MCP `mutate_node_evidence`, deterministic before/after artifact paths, visual-tree JSON snapshots, optional image diff output, bounded target summaries, and agent-tool documentation updates. Validation passed with `dotnet build AvaScope.slnx --no-restore -v:minimal`, focused evidence tests (`dotnet test AvaScope.slnx --no-build --filter "FullyQualifiedName~ProtocolContractTests.RuntimeMutationEvidenceResponseSerializesStableShape|FullyQualifiedName~LocalBridgeClientTests.RuntimeMutationEvidenceRunnerCapturesSequencedArtifactsThroughBridgePipe|FullyQualifiedName~CliSmokeTests.MutateNodeEvidence|FullyQualifiedName~BridgeHeadlessSmokeTests.McpMutateNodeEvidenceCapturesScreenshotsTreesAndDiffThroughLocalBridgePipe|FullyQualifiedName~McpStdioSmokeTests.ServerStartsOverStdioAndListsInitialTools"`, 6 passed), baseline/screenshot compatibility tests (`dotnet test AvaScope.slnx --no-build --filter "FullyQualifiedName~CliSmokeTests.BaselineCommandsCreateManifestPassCheckAndFailChangedCheck|FullyQualifiedName~CliSmokeTests.ScreenshotCommand"`, 4 passed), full Debug tests (`dotnet test AvaScope.slnx --no-build`, 258 passed), and `git diff --check` (LF/CRLF warnings only).
- `2026-06-12`: Started GitHub issue #11 for mutation evidence and screenshot loop. Moved the issue and project card to `status:in-progress` / `In Progress`, recorded the intended validation in the issue, and scoped implementation to Core/CLI/MCP before/after artifacts plus a representative screenshot/diff smoke path.
- `2026-06-12`: Completed GitHub issue #10 for the runtime style/layout mutation set. Added safe reversible bridge mutations for width, height, min/max size, margin, padding, opacity, text/content, background, foreground, classes, and selected local resource overrides; added mutation response metadata for original/effective values, active mutation ids, and reset results; added `reset_mutation` and `reset_all` operations through Protocol, CLI, MCP, Core, and Bridge. Validation passed with `dotnet build AvaScope.slnx --no-restore -v:minimal`, focused mutation tests (`dotnet test AvaScope.slnx --no-build --filter "FullyQualifiedName~RuntimeMutationRequestAndResponseSerializeStableShapes|FullyQualifiedName~RuntimeMutation|FullyQualifiedName~MutateNode"`, 11 passed), full Debug tests (`dotnet test AvaScope.slnx --no-build`, 253 passed), and `git diff --check` (LF/CRLF warnings only).
- `2026-06-12`: Started GitHub issue #10 for the runtime style/layout mutation set. Moved the issue and project card to `status:in-progress` / `In Progress`, recorded the intended validation in the issue, and scoped implementation to reversible local-only mutations plus reset semantics.
- `2026-06-12`: Completed GitHub issue #9 for the runtime mutation contract. Added Protocol DTOs/constants for mutation requests, operations, capabilities, statuses, diagnostics, and responses; added `mutate_node` bridge IPC, Core client, Bridge runtime validation, CLI `mutate-node`, and MCP `mutate_node` surfaces. The contract validates target session/top-level/node context, returns bounded diagnostics for stale targets, unsupported properties/operations, invalid values, unavailable capabilities, and session mismatch, and preserves existing inspection/input/screenshot/close-session behavior. Validation passed with `dotnet build AvaScope.slnx --no-restore -v:minimal`, focused mutation tests (`dotnet test AvaScope.slnx --no-build --filter "FullyQualifiedName~RuntimeMutationRequestAndResponseSerializeStableShapes|FullyQualifiedName~MutateNode|FullyQualifiedName~McpStdioSmokeTests.ServerStartsOverStdioAndListsInitialTools"`), full Debug tests (`dotnet test AvaScope.slnx --no-build`, 251 passed), and `git diff --check` (LF/CRLF warnings only).
- `2026-06-12`: Completed GitHub issue #40 for the agent-tool documentation positioning refresh. Updated README, `docs/USER_GUIDE.md`, `docs/AGENT_WORKFLOW.md`, `samples/AvaScope.GettingStartedApp/README.md`, and `AGENTS.md` so AvaScope is described as an agent-focused local control plane for Avalonia apps, while planned `v0.7.0` runtime mutations remain clearly marked as future work. Validation passed with `git diff --check` (LF/CRLF warnings only), a stale-wording `rg` check, and a local markdown link/path existence check for changed docs.
- `2026-06-12`: Created the remaining GitHub issue backlog through `v1.0.0`: `v0.8.0` issues #19-#25, `v0.9.0` issues #26-#32, and `v1.0.0` issues #33-#39. Added all new issues to the public `AvaScope Roadmap` Project with `Workflow Status=Backlog`, `Progress=0%`, release tracker/planned-slice phase values, and roadmap ordering. No implementation was started; current focus remains issue #9.
- `2026-06-12`: Replanned `v0.7.0` through `v1.0.0` around the agent-first control-plane direction. Updated `docs/RELEASE_PLAN.md`, the current handoff focus, GitHub milestone descriptions, and v0.7.0 issues #8-#14 so the next release starts with reversible runtime mutation contracts, style/layout mutation, evidence artifacts, session safety, and CLI/MCP experiment review. Validated the docs-only change with `git diff --check` and an `rg` check confirming no external-tool comparison wording remains in the updated planning files.
- `2026-06-10`: Started and completed GitHub issue #18 to make the root README public-facing. Moved the detailed former README content to `docs/USER_GUIDE.md`, replaced the root README with a 53-line overview and documentation index, and validated the documentation-only change with `git diff --check` plus a local Markdown link existence check.
- `2026-06-10`: Improved the `AvaScope Roadmap` GitHub Project for human readability by adding `Progress`, `Release Phase`, and `Roadmap Order` fields, setting values for active v0.7.0 issues #8-#14, archiving completed historical project items #1-#7 and #15-#17, and updating the project readme with the recommended board/table views. Confirmed GitHub's public Projects schema exposes view layouts read-only and has no mutation for saved view layout creation/editing.
- `2026-06-10`: Created and populated the public `AvaScope Roadmap` GitHub Project at https://github.com/users/RolandUI/projects/4. The project is linked to `RolandUI/AvaScope`, contains issues #1-#17, uses a `Workflow Status` field with Ready/Backlog/Done values for the current roadmap, and closed the previous blocker/fallback issues #15 and #17 as completed.
- `2026-06-10`: Migrated project execution to GitHub Issues and Milestones. Closed malformed v0.6.0 issues #1-#7 as completed, closed the `v0.6.0` milestone, created release milestones `v0.7.0` through `v1.0.0`, created v0.7.0 tracking issue #8 and implementation issues #9-#14, and created Project-board setup issue #15 for the then-missing GitHub Projects token scope.
- `2026-06-10`: GitHub CI workflow `27292094908` passed on the docs-only validation commit in 31 seconds; `Setup .NET`, `Restore`, `Build`, `Test`, `Pack libraries`, `Package executables`, and `Verify artifacts` were skipped, and only the documentation-only skip confirmation step ran. Follow-up Release workflow `27292094955` no-oped successfully because `v0.6.0` already existed.
- `2026-06-10`: GitHub CI workflow `27291558979` passed after adding CI change classification; because `.github/workflows/ci.yml` changed, the classifier required full validation and Restore, Build, Test, Pack, executable packaging, and artifact verification all ran. Follow-up Release workflow `27291559430` no-oped successfully because `v0.6.0` already existed.
- `2026-06-10`: `git diff --check` passed after adding the CI documentation-only fast path; local classifier simulation verified docs/Markdown-only changes skip full validation and source/workflow changes require it.
- `2026-06-10`: GitHub CI workflow `27289598895` failed on the `Record v0.6.0 final CI stabilization` documentation commit in `CliSmokeTests.ListTopLevelsCommandReadsTopLevelsThroughBridgePipe` because the fake CLI bridge server timed out waiting for an IPC request on the hosted runner.
- `2026-06-10`: Isolated CLI smoke-test bridge manifests into a per-test-process directory and made the CLI fake bridge helper skip empty/non-JSON probe connections; the failing Release test passed 4 consecutive local runs, and `dotnet test AvaScope.slnx -c Release --no-build` passed with 242 tests.
- `2026-06-10`: GitHub CI workflow `27289102252` passed after the bridge diagnostics fake-pipe helper skipped empty/non-JSON probe connections; follow-up Release workflow `27289102340` no-oped successfully because `v0.6.0` already existed.
- `2026-06-10`: GitHub CI workflow `27288551198` failed on the `Stabilize bridge diagnostics smoke timeout` commit because the fake named-pipe helper still tried to parse an empty/non-JSON probe connection as JSON.
- `2026-06-10`: GitHub CI workflow `27288017355` failed on the `Record v0.6.0 CI stabilization` documentation commit in `LocalBridgeClientTests.DiagnosticsReportsDuplicateAndIncompatibleBridgeManifests` because a 100 ms fake named-pipe timeout was too tight for the hosted runner.
- `2026-06-10`: Increased the incompatible-bridge diagnostics fake-pipe test timeout to 5 seconds and made the fake pipe helper skip empty/non-JSON probe connections; targeted Release diagnostics test passed 4 consecutive local runs, and `dotnet test AvaScope.slnx -c Release --no-build` passed with 242 tests.
- `2026-06-10`: GitHub CI workflow `27287676017` passed after watcher-smoke cleanup retry stabilization; follow-up Release workflow `27287675726` no-oped successfully because `v0.6.0` already existed.
- `2026-06-10`: GitHub CI workflow `27287102689` failed on the `Record v0.6.0 release completion` documentation commit because the watcher smoke temp directory cleanup hit a transient hosted Windows file lock on `MainView.axaml`.
- `2026-06-10`: Added retrying watcher-smoke temp directory cleanup; targeted Release watcher smoke passed locally, and `dotnet test AvaScope.slnx -c Release --no-build` passed with 242 tests.
- `2026-06-10`: GitHub CI workflow `27286438229` passed after post-release watcher stabilization; follow-up Release workflow `27286438001` no-oped successfully because `v0.6.0` already existed.
- `2026-06-10`: `gh release view v0.6.0` confirmed the public release URL and six uploaded assets: three `0.6.0` `.nupkg` files, win-x64 and linux-x64 framework-dependent ZIPs, and `release-manifest.json`.
- `2026-06-10`: `git ls-remote --tags origin refs/tags/v0.6.0` confirmed tag `v0.6.0` at release commit `4af5284`.
- `2026-06-10`: GitHub Release workflow `27285679633` passed for `Release 0.6.0`; tag `v0.6.0` and six GitHub Release assets were published at `2026-06-10T15:11:44Z`.
- `2026-06-10`: GitHub CI workflow `27285678995` failed after the `v0.6.0` release commit in `CliSmokeTests.WatchPreviewSessionCommandReloadsWhenWatchedFileChanges` because the CI runner missed the single watched-file event before the command timed out.
- `2026-06-10`: Post-release watcher stabilization targeted Release smoke passed 4 consecutive runs after generating repeated watched-file writes and increasing the test timeout/settle window.
- `2026-06-10`: `dotnet test AvaScope.slnx -c Release --no-build` passed with 242 tests after the post-release watcher stabilization.
- `2026-06-10`: `powershell -NoProfile -ExecutionPolicy Bypass -File .\eng\validate-release-commit.ps1 -Version 0.6.0 -CommitSubject "Release 0.6.0" -RequiredState "Release Candidate"` passed for the `v0.6.0` release commit guard.
- `2026-06-10`: `git diff --check` passed for the `v0.6.0` release-candidate working tree with only line-ending normalization warnings.
- `2026-06-10`: `dotnet test AvaScope.slnx --no-build` passed again with 242 tests after the final screenshot-region file-handle fix.
- `2026-06-10`: `powershell -NoProfile -ExecutionPolicy Bypass -File .\eng\create-local-release.ps1` passed for `v0.6.0` after stopping stale packaged CLI/MCP processes from the local artifact output; Release build/test passed with 242 tests, three `0.6.0` packages, win-x64 and linux-x64 framework-dependent ZIPs, release manifest, packaged doctor smoke, and packaged sample preview smoke.
- `2026-06-10`: `powershell -NoProfile -ExecutionPolicy Bypass -File .\eng\publish-github-release.ps1 -Tag v0.6.0 -DryRun` passed for `v0.6.0` assets.
- `2026-06-10`: `dotnet test AvaScope.slnx -c Release --filter FullyQualifiedName~ScreenshotRegionAsserterTests` passed with 3 tests after fixing screenshot-region file-handle cleanup by stream-decoding images and disposing crop image handles explicitly.
- `2026-06-10`: `dotnet test AvaScope.slnx --no-build` passed with 242 tests after `v0.6.0` implementation and documentation updates.
- `2026-06-10`: `powershell -NoProfile -ExecutionPolicy Bypass -File .\eng\validate-bug-reports.ps1` passed after marking `FEAT-0009` through `FEAT-0015` implemented; 22 intake files scanned.
- `2026-06-10`: `git diff --check` passed after `v0.6.0` implementation and documentation updates with only line-ending normalization warnings.
- `2026-06-10`: `dotnet build AvaScope.slnx --no-restore -v:minimal` passed with 0 warnings and 0 errors after `v0.6.0` runtime input/state, latest attach, launch helper, screenshot-region assertion, and preview-session lifecycle-event implementation.
- `2026-06-10`: Targeted `v0.6.0` tests passed with 11 tests covering protocol runtime state/region contracts, screenshot region assertions, expanded bridge input/state inspection, CLI select/scroll/region/launch-helper behavior, and latest active bridge manifest selection.
- `2026-06-10`: Targeted preview-session lifecycle tests passed with 3 tests covering preview-session create/reload/close events and lifecycle serialization.
- `2026-06-10`: `powershell -NoProfile -ExecutionPolicy Bypass -File .\eng\validate-bug-reports.ps1` passed after storing `FEAT-0009` through `FEAT-0015` and expanding `v0.6.0`; 22 intake files scanned.
- `2026-06-10`: `git diff --check` passed after the `v0.6.0` ticket scheduling documentation update.
- `2026-06-10`: GitHub Release workflow `27277929158` passed for `Release 0.5.0`; tag `v0.5.0` and six GitHub Release assets were published at `2026-06-10T13:06:09Z`.
- `2026-06-10`: GitHub CI workflow `27277929144` passed for `Release 0.5.0`.
- `2026-06-10`: `gh release view v0.5.0` confirmed the public release URL and six uploaded assets.
- `2026-06-10`: `git ls-remote --tags origin refs/tags/v0.5.0` confirmed tag `v0.5.0` at release commit `e4b6029`.
- `2026-06-10`: `powershell -NoProfile -ExecutionPolicy Bypass -File .\eng\create-local-release.ps1` passed for the `v0.5.0` release-candidate working tree after stopping stale packaged CLI/MCP processes from the local artifact output; Release build/test passed with 231 tests, three local packages, win-x64 and linux-x64 framework-dependent ZIPs, release manifest, packaged doctor smoke, and packaged sample preview smoke.
- `2026-06-10`: `powershell -NoProfile -ExecutionPolicy Bypass -File .\eng\publish-github-release.ps1 -Tag v0.5.0 -DryRun` passed for `v0.5.0` assets.
- `2026-06-10`: `powershell -NoProfile -ExecutionPolicy Bypass -File .\eng\validate-release-commit.ps1 -Version 0.5.0 -CommitSubject "Release 0.5.0" -RequiredState "Release Candidate"` passed for the `v0.5.0` release commit guard.
- `2026-06-10`: `git diff --check` passed for the `v0.5.0` release-candidate working tree with only line-ending normalization warnings.
- `2026-06-10`: `dotnet test AvaScope.slnx --no-build --filter "FullyQualifiedName~ProtocolContractTests.PreviewResponseSerializesProjectInfo|FullyQualifiedName~ProtocolContractTests.PreviewResponseSerializesDiagnostics|FullyQualifiedName~PreviewHostSmokeTests.PreviewHostReturnsProjectInfoAndProjectGraphDiagnostics|FullyQualifiedName~CliSmokeTests.PreviewCommandUsesProjectPreviewProfileVariantAndAllowsExplicitOverrides"` passed with 4 tests after the `v0.5.0` PreviewHost fidelity implementation.
- `2026-06-10`: `dotnet test AvaScope.slnx --no-build` passed with 231 tests after the `v0.5.0` PreviewHost fidelity implementation.
- `2026-06-10`: Source CLI sample previews passed for `samples\AvaScope.GettingStartedApp` profile variants `main --variant dark` and `main --variant hu`, producing themed/culture-specific artifacts with `projectInfo` and `project_graph_resolved` diagnostics.
- `2026-06-10`: `git diff --check` passed after the `v0.5.0` PreviewHost fidelity implementation with only line-ending normalization warnings.
- `2026-06-10`: `dotnet build AvaScope.slnx --no-restore -v:minimal` passed with 0 warnings and 0 errors after adding `v0.5.0` project metadata, diagnostic triage, profile variants, and sample coverage.
- `2026-06-10`: `dotnet build AvaScope.slnx --no-restore -v:minimal` passed with 0 warnings and 0 errors after implementing `v0.4.0` runtime bridge reliability.
- `2026-06-10`: NuGet package check confirmed Avalonia `12.0.4` remains the current stable Avalonia 12 line for the repo's referenced packages; no package-version change is needed for starting `v0.5.0`.
- `2026-06-10`: Runtime reliability targeted tests passed with 61 tests covering Core manifest selection/cleanup/diagnostics, protocol target/input/cleanup shapes, CLI manifest-path/process-name/custom-directory workflows, MCP cleanup and attach selection, MCP stdio tool listing, and headless bridge input metadata.
- `2026-06-10`: `dotnet test AvaScope.slnx --no-build` passed with 228 tests after isolating diagnostics smoke tests from default preview-session temp records.
- `2026-06-10`: `powershell -NoProfile -ExecutionPolicy Bypass -File .\eng\create-local-release.ps1` passed for `v0.4.0` after stopping stale packaged CLI/dotnet processes from the local artifact output; Release build/test passed with 228 tests, three `0.4.0` packages, win-x64 and linux-x64 framework-dependent ZIPs, release manifest, packaged doctor smoke, and packaged sample preview smoke.
- `2026-06-10`: `powershell -NoProfile -ExecutionPolicy Bypass -File .\eng\publish-github-release.ps1 -Tag v0.4.0 -DryRun` passed for `v0.4.0` assets.
- `2026-06-10`: Packaged Windows CLI runtime smoke passed against `samples\AvaScope.GettingStartedApp`: `attach --process-name`, `list-top-levels`, `visual-tree`, `find-nodes`, `inspect-node`, `screenshot`, `input --action pointer_move`, and `close-session`.
- `2026-06-10`: `powershell -NoProfile -ExecutionPolicy Bypass -File .\eng\validate-bug-reports.ps1` passed; 15 intake files scanned.
- `2026-06-10`: `git diff --check` passed with only line-ending normalization warnings.
- `2026-06-10`: `powershell -NoProfile -ExecutionPolicy Bypass -File .\eng\validate-release-commit.ps1 -Version 0.4.0 -CommitSubject "Release 0.4.0" -RequiredState "Release Candidate"` passed for the `v0.4.0` release commit guard.
- `2026-06-10`: GitHub Release workflow `27264946927` passed for `Release 0.4.0`; tag `v0.4.0` and six GitHub Release assets were published at `2026-06-10T09:02:02Z`.
- `2026-06-10`: GitHub CI workflow `27264946899` passed for `Release 0.4.0`.
- `2026-06-10`: `gh release view v0.4.0` confirmed the public release URL and six uploaded assets.
- `2026-06-10`: `git ls-remote --tags origin refs/tags/v0.4.0` confirmed tag `v0.4.0` at release commit `c3cbd16`.
- `2026-06-09`: `git diff --check` passed after revising `v0.8.0` into a product feature release and moving protocol/integration hardening to `v0.9.0`.
- `2026-06-09`: `powershell -NoProfile -ExecutionPolicy Bypass -File .\eng\validate-bug-reports.ps1` passed after the `v0.8.0` roadmap revision; 15 intake files scanned.
- `2026-06-09`: Build/test validation was not run for the `v0.8.0` roadmap-only documentation revision because no source code, project file, or test code changed.
- `2026-06-09`: `git diff --check` passed after planning the `v0.4.0` through `v1.0.0` release roadmap.
- `2026-06-09`: `powershell -NoProfile -ExecutionPolicy Bypass -File .\eng\validate-bug-reports.ps1` passed after roadmap planning and ledger status refresh; 15 intake files scanned.
- `2026-06-09`: Build/test validation was not run for the roadmap-only documentation change because no source code, project file, or test code changed.
- `2026-06-09`: `dotnet build AvaScope.slnx` passed with 0 warnings and 0 errors on the `0.3.0` release-candidate working tree.
- `2026-06-09`: `dotnet test AvaScope.slnx --no-build` passed with 218 tests on the `0.3.0` release-candidate working tree.
- `2026-06-09`: `powershell -NoProfile -ExecutionPolicy Bypass -File .\eng\create-local-release.ps1` passed for `v0.3.0` after stopping stale local artifact-hosted processes; Release build/test passed with 218 tests, three `0.3.0` packages, win-x64 and linux-x64 framework-dependent ZIPs, release manifest, packaged doctor smoke, and packaged sample preview smoke.
- `2026-06-09`: `powershell -NoProfile -ExecutionPolicy Bypass -File .\eng\publish-github-release.ps1 -Tag v0.3.0 -DryRun` passed for `v0.3.0` assets.
- `2026-06-09`: Packaged Windows CLI `preview-animation .\samples\AvaScope.GettingStartedApp\AvaScope.GettingStartedApp.csproj --profile animation` passed with 4 successful frames, a frame strip, `motion.status=changed`, `animation_frame_reused` for the repeated final offset, and a file-backed animation viewer URL.
- `2026-06-09`: `powershell -NoProfile -ExecutionPolicy Bypass -File .\eng\validate-bug-reports.ps1` passed for the `v0.3.0` release-candidate gate; 15 intake files scanned.
- `2026-06-09`: `git diff --check` passed for the `v0.3.0` release-candidate working tree.
- `2026-06-09`: `powershell -NoProfile -ExecutionPolicy Bypass -File .\eng\validate-release-commit.ps1 -Version 0.3.0 -CommitSubject "Release 0.3.0" -RequiredState "Release Candidate"` passed for the `v0.3.0` release commit guard.
- `2026-06-09`: GitHub Release workflow `27217885931` passed for `Release 0.3.0`; tag `v0.3.0` and six GitHub Release assets were published at `2026-06-09T15:50:04Z`.
- `2026-06-09`: `gh release view v0.3.0` confirmed the public release URL and six uploaded assets.
- `2026-06-09`: `git ls-remote --tags origin refs/tags/v0.3.0` confirmed tag `v0.3.0` at release commit `9d6cc3f`.
- `2026-06-09`: Post-release CI stabilization targeted Release watcher skip test passed 3 consecutive runs after increasing the unchanged-input watcher test timeout.
- `2026-06-09`: `dotnet test AvaScope.slnx -c Release --no-build` passed with 218 tests after post-release watcher skip test stabilization.
- `2026-06-09`: GitHub CI workflow `27218629376` passed after post-release watcher skip test stabilization; Release workflow `27218629389` no-oped successfully because `v0.3.0` already existed.
- `2026-06-09`: `dotnet build AvaScope.slnx --no-restore -v:minimal` passed with 0 warnings and 0 errors after implementing `v0.3.0` animation sampling, viewer, sample, and documentation.
- `2026-06-09`: Animation targeted tests passed with 5 tests: `ProtocolContractTests.PreviewAnimationRequestAndResponseSerializeStableShapes`, `PreviewHostClientTests.RenderAnimationAsyncCreatesOffsetFramesStripAndMotionSummary`, `CliSmokeTests.PreviewAnimationCommandRendersOffsetFramesAndStrip`, `McpStdioSmokeTests.ServerStartsOverStdioAndListsInitialTools`, and `AvaScopeMcpToolsTests.PreviewAxamlAnimationRejectsInvalidOffsets`.
- `2026-06-09`: Source CLI `preview-animation .\samples\AvaScope.GettingStartedApp\AvaScope.GettingStartedApp.csproj --profile animation` passed with 4 successful frames, a frame strip, `motion.status=changed`, and a file-backed animation viewer URL.
- `2026-06-09`: `powershell -NoProfile -ExecutionPolicy Bypass -File .\eng\validate-bug-reports.ps1` and `git diff --check` passed after planning `v0.3.0` animation diagnostics release scope; 15 intake files scanned.
- `2026-06-09`: `powershell -NoProfile -ExecutionPolicy Bypass -File .\eng\validate-bug-reports.ps1` passed after storing `FEAT-0008`; 15 intake files scanned.
- `2026-06-09`: GitHub Release workflow `27205089688` passed for `Release 0.2.2`; tag `v0.2.2` and six GitHub Release assets were published at `2026-06-09T12:17:17Z`.
- `2026-06-09`: GitHub CI workflow `27205089675` passed for `Release 0.2.2`.
- `2026-06-09`: `gh release view v0.2.2` confirmed the public release URL and six uploaded assets.
- `2026-06-09`: `git ls-remote --tags origin refs/tags/v0.2.2` confirmed tag `v0.2.2` at release commit `eac2bf1`.
- `2026-06-09`: `powershell -NoProfile -ExecutionPolicy Bypass -File .\eng\validate-release-commit.ps1 -Version 0.2.2 -CommitSubject "Release 0.2.2" -RequiredState "Release Candidate"` passed.
- `2026-06-09`: `git diff --check` passed for `v0.2.2` release-candidate validation.
- `2026-06-09`: `powershell -NoProfile -ExecutionPolicy Bypass -File .\eng\publish-github-release.ps1 -Tag v0.2.2 -DryRun` passed for `v0.2.2` assets.
- `2026-06-09`: `powershell -NoProfile -ExecutionPolicy Bypass -File .\eng\create-local-release.ps1` passed for `v0.2.2`; Release build/test passed with 214 tests, three `0.2.2` packages, win-x64 and linux-x64 framework-dependent ZIPs, release manifest, packaged doctor smoke, and packaged sample preview smoke.
- `2026-06-09`: `dotnet test AvaScope.slnx --no-build` passed with 214 tests for `v0.2.2` release-candidate validation.
- `2026-06-09`: `dotnet build AvaScope.slnx` passed with 0 warnings and 0 errors for `v0.2.2` release-candidate validation.
- `2026-06-09`: `powershell -NoProfile -ExecutionPolicy Bypass -File .\eng\validate-bug-reports.ps1` passed after marking `BUG-0003` fixed; 14 intake files scanned.
- `2026-06-09`: `git diff --check` passed after BUG-0003 diagnostics fix implementation.
- `2026-06-09`: `dotnet test tests/AvaScope.Tests/AvaScope.Tests.csproj --no-build` passed with 214 tests after BUG-0003 diagnostics false positive fixes.
- `2026-06-09`: BUG-0003 targeted PreviewHost diagnostics tests passed with 4 tests: `PreviewHostUsesDataTemplateDataTypeForBindingDiagnostics`, `PreviewHostSuppressesFluentTemplateLayoutNoise`, `PreviewHostReturnsDataTypeBindingPathDiagnostics`, and `PreviewHostReturnsBindingResourceAndLayoutDiagnostics`.
- `2026-06-09`: `dotnet build tests/AvaScope.Tests/AvaScope.Tests.csproj --no-restore -v:minimal` passed with 0 warnings and 0 errors after BUG-0003 implementation.
- `2026-06-09`: Post-BUG-0003 CI stabilization targeted Release watch smoke passed 3 consecutive runs: `dotnet test AvaScope.slnx -c Release --filter FullyQualifiedName~CliSmokeTests.WatchPreviewSessionCommandReloadsWhenWatchedFileChanges`.
- `2026-06-09`: `dotnet test AvaScope.slnx -c Release --no-build` passed with 212 tests after increasing the watch smoke settle window for Windows file watcher timing.
- `2026-06-09`: `powershell -NoProfile -ExecutionPolicy Bypass -File .\eng\validate-bug-reports.ps1` passed after storing `BUG-0003`; 14 intake files scanned.
- `2026-06-09`: Post-v0.2.1 CI stabilization targeted Release tests passed: `dotnet test AvaScope.slnx -c Release --filter "FullyQualifiedName~CliSmokeTests.WatchPreviewSessionCommandReloadsWhenWatchedFileChanges|FullyQualifiedName~AvaScopeMcpBridgeToolsTests.AttachToAppUsesLocalBridgeManifestAndPipeHealth"` passed with 2 tests.
- `2026-06-09`: `dotnet test AvaScope.slnx -c Release --no-build` passed with 212 tests after stabilizing the watch file-write smoke path and explicit MCP bridge attach targeting.
- `2026-06-09`: GitHub Release workflow `27200641766` passed for `Release 0.2.1`; tag `v0.2.1` and six GitHub Release assets were published at `2026-06-09T10:48:21Z`.
- `2026-06-09`: GitHub CI workflow `27200641755` passed for `Release 0.2.1`.
- `2026-06-09`: `gh release view v0.2.1` confirmed the public release URL and six uploaded assets.
- `2026-06-09`: `git ls-remote --tags origin refs/tags/v0.2.1` confirmed tag `v0.2.1` at release commit `d12fe8c`.
- `2026-06-09`: `powershell -NoProfile -ExecutionPolicy Bypass -File .\eng\create-local-release.ps1` passed for `v0.2.1`; Release build/test passed with 212 tests, 0.2.1 packages, win/linux framework-dependent ZIPs, release manifest, packaged doctor smoke, and packaged sample preview smoke.
- `2026-06-09`: `powershell -NoProfile -ExecutionPolicy Bypass -File .\eng\publish-github-release.ps1 -Tag v0.2.1 -DryRun` passed for `v0.2.1` assets.
- `2026-06-09`: `powershell -NoProfile -ExecutionPolicy Bypass -File .\eng\validate-release-commit.ps1 -Version 0.2.1 -CommitSubject "Release 0.2.1" -RequiredState "Release Candidate"` passed.
- `2026-06-09`: Targeted PreviewHost theme-background tests passed: `dotnet test AvaScope.slnx --filter "FullyQualifiedName~PreviewHostSmokeTests.PreviewHostUsesDarkFluentWindowBackgroundForTransparentRootControl|FullyQualifiedName~PreviewHostSmokeTests.PreviewHostKeepsAppWindowBackgroundStyleForTransparentRootControl"` passed with 2 tests.
- `2026-06-09`: `dotnet build AvaScope.slnx` passed with 0 warnings and 0 errors after R0.2.1-M1.
- `2026-06-09`: `dotnet test AvaScope.slnx --no-build` passed with 212 tests after removing one closed AvaScope preview-session temp record that was unrelated stale diagnostics state.
- `2026-06-09`: `git diff --check` passed after R0.2.1-M1.
- `2026-06-09`: External dark preview smoke passed for `SettingsView.axaml` with output `artifacts/validation/v0.2.1-theme-background/SettingsView-dark.png`; the rendered canvas used the dark theme background.
- `2026-06-08`: `dotnet build AvaScope.slnx` passed with 0 warnings and 0 errors after W9 feature-ticket implementation.
- `2026-06-08`: W9 targeted tests passed: protocol contract diagnostics/batch/diff/cleanup coverage, PreviewHost binding/resource/layout diagnostics, CLI multi-size preview/contact sheet, CLI screenshot diff, Bridge computed properties, Core preview-session cleanup, and MCP stdio tool listing.
- `2026-06-08`: `dotnet test AvaScope.slnx --no-build` passed with 186 tests after W9 feature-ticket implementation.
- `2026-06-08`: `powershell -NoProfile -ExecutionPolicy Bypass -File .\eng\validate-bug-reports.ps1` passed after W9 feature-ticket status updates; 13 intake files scanned.
- `2026-06-08`: `git diff --check` passed after W9 feature-ticket implementation and documentation updates.
- `2026-06-08`: `dotnet build AvaScope.slnx` passed with 0 warnings and 0 errors after W10 CLI preview-session workflow.
- `2026-06-08`: `dotnet test AvaScope.slnx --no-build --filter FullyQualifiedName~Cli` passed with 81 tests after W10 CLI preview-session workflow.
- `2026-06-08`: `dotnet test AvaScope.slnx --no-build --filter FullyQualifiedName~PreviewSessionRegistryTests` passed with 9 tests after W10 CLI preview-session workflow.
- `2026-06-08`: `dotnet test AvaScope.slnx --no-build` passed with 188 tests after W10 CLI preview-session workflow.
- `2026-06-08`: `git diff --check` passed after W10 CLI preview-session workflow.
- `2026-06-08`: `dotnet build AvaScope.slnx` passed with 0 warnings and 0 errors after W11 live preview file-watch reload.
- `2026-06-08`: W11 targeted tests passed: CLI watch reload, Core `PreviewSessionWatcher`, and Protocol watch response serialization.
- `2026-06-08`: `dotnet test AvaScope.slnx --no-build --filter FullyQualifiedName~Cli` passed with 82 tests after W11 live preview file-watch reload.
- `2026-06-08`: `dotnet test AvaScope.slnx --no-build --filter FullyQualifiedName~PreviewSessionRegistryTests` passed with 10 tests after W11 live preview file-watch reload.
- `2026-06-08`: `dotnet test AvaScope.slnx --no-build --filter Protocol` passed with 34 tests after W11 live preview file-watch reload.
- `2026-06-08`: `dotnet test AvaScope.slnx --no-build` passed with 191 tests after W11 live preview file-watch reload.
- `2026-06-08`: `git diff --check` passed after W11 live preview file-watch reload.
- `2026-06-08`: `dotnet build AvaScope.slnx` passed with 0 warnings and 0 errors after W12 visual regression workflow.
- `2026-06-08`: `dotnet test AvaScope.slnx --no-build --filter FullyQualifiedName~Cli` passed with 83 tests after W12 visual regression workflow.
- `2026-06-08`: `dotnet test AvaScope.slnx --no-build --filter Protocol` passed with 35 tests after W12 visual regression workflow.
- `2026-06-08`: `dotnet test AvaScope.slnx --no-build` passed with 193 tests after W12 visual regression workflow.
- `2026-06-08`: `git diff --check` passed after W12 visual regression workflow.
- `2026-06-08`: `dotnet build AvaScope.slnx` passed with 0 warnings and 0 errors after W13 deeper diagnostics.
- `2026-06-08`: `dotnet test AvaScope.slnx --no-build --filter FullyQualifiedName~PreviewHostSmokeTests.PreviewHostReturnsDataTypeBindingPathDiagnostics` passed after W13 deeper diagnostics.
- `2026-06-08`: `dotnet test AvaScope.slnx --no-build --filter FullyQualifiedName~PreviewHost` passed with 29 tests after W13 deeper diagnostics.
- `2026-06-08`: `dotnet test AvaScope.slnx --no-build --filter Bridge` passed with 64 tests after W13 deeper diagnostics.
- `2026-06-08`: `dotnet test AvaScope.slnx --no-build` passed with 194 tests after W13 deeper diagnostics.
- `2026-06-08`: `git diff --check` passed after W13 deeper diagnostics.
- `2026-06-08`: `dotnet build AvaScope.slnx` passed with 0 warnings and 0 errors after W14 richer runtime input.
- `2026-06-08`: `dotnet test AvaScope.slnx --no-build --filter Bridge` passed with 64 tests after W14 richer runtime input.
- `2026-06-08`: `dotnet test AvaScope.slnx --no-build --filter FullyQualifiedName~Cli` passed with 83 tests after W14 richer runtime input.
- `2026-06-08`: `dotnet test AvaScope.slnx --no-build` passed with 194 tests after W14 richer runtime input.
- `2026-06-08`: `git diff --check` passed after W14 richer runtime input.
- `2026-06-08`: `dotnet build AvaScope.slnx` passed with 0 warnings and 0 errors after W15 preview startup parity.
- `2026-06-08`: `dotnet test AvaScope.slnx --no-build --filter FullyQualifiedName~PreviewHost` passed with 30 tests after W15 preview startup parity.
- `2026-06-08`: `dotnet test AvaScope.slnx --no-build` passed with 195 tests after W15 preview startup parity.
- `2026-06-08`: `git diff --check` passed after W15 preview startup parity.
- `2026-06-08`: `powershell -NoProfile -ExecutionPolicy Bypass -File .\eng\create-local-release.ps1 -RuntimeIdentifiers win-x64 -ExecutablePackageKind self-contained -SkipTests -SkipSampleSmoke` passed after W16 distribution hardening.
- `2026-06-08`: `powershell -NoProfile -ExecutionPolicy Bypass -File .\eng\publish-github-release.ps1 -Tag v0.1.0 -ExecutableRuntimeIdentifiers win-x64 -ExecutablePackageKind self-contained -DryRun` passed after W16 distribution hardening.
- `2026-06-08`: `powershell -NoProfile -ExecutionPolicy Bypass -File .\eng\create-local-release.ps1` passed after W16 distribution hardening; Release build/test passed with 195 tests, 5 framework-dependent release artifacts verified, and packaged Windows sample preview smoke passed.
- `2026-06-08`: `powershell -NoProfile -ExecutionPolicy Bypass -File .\eng\publish-nuget.ps1 -DryRun` passed after W16 distribution hardening.
- `2026-06-08`: `powershell -NoProfile -ExecutionPolicy Bypass -File .\eng\publish-github-release.ps1 -Tag v0.1.0 -DryRun` passed after W16 distribution hardening.
- `2026-06-08`: `git check-ignore -v` confirmed regenerated W16 release artifacts remain ignored under `artifacts/`.
- `2026-06-09`: `git diff --check` passed after W17 plan refresh and gap-audit alignment.
- `2026-06-09`: `dotnet build AvaScope.slnx` passed with 0 warnings and 0 errors after W18 CLI doctor/self-test.
- `2026-06-09`: `dotnet test AvaScope.slnx --no-build --filter FullyQualifiedName~ProtocolContractTests.DoctorResponseSerializesStableReadinessShape` passed after W18 CLI doctor/self-test.
- `2026-06-09`: `dotnet test AvaScope.slnx --no-build --filter FullyQualifiedName~CliSmokeTests.DoctorCommandReportsLocalReadiness` passed after W18 CLI doctor/self-test.
- `2026-06-09`: `dotnet test AvaScope.slnx --no-build --filter FullyQualifiedName~CliSmokeTests.DoctorCommandRejectsInvalidArguments` passed after W18 CLI doctor/self-test.
- `2026-06-09`: `dotnet test AvaScope.slnx --no-build --filter FullyQualifiedName~Cli` passed with 85 tests after W18 CLI doctor/self-test.
- `2026-06-09`: `dotnet test AvaScope.slnx --no-build --filter Protocol` passed with 36 tests after W18 CLI doctor/self-test.
- `2026-06-09`: source CLI `avascope doctor --manifest-dir <temp> --preview-session-store <temp>` smoke passed after W18 CLI doctor/self-test.
- `2026-06-09`: `powershell -NoProfile -ExecutionPolicy Bypass -File .\eng\create-local-release.ps1` passed after W18 CLI doctor/self-test; Release build/test passed with 198 tests, 5 framework-dependent release artifacts verified, and packaged Windows sample preview smoke passed.
- `2026-06-09`: `powershell -NoProfile -ExecutionPolicy Bypass -File .\eng\create-local-release.ps1 -SkipTests` passed after adding packaged Windows doctor smoke to the release script.
- `2026-06-09`: `git diff --check` passed after W18 CLI doctor/self-test.
- `2026-06-09`: `dotnet build AvaScope.slnx` passed with 0 warnings and 0 errors after W19 preview profiles.
- `2026-06-09`: `dotnet test AvaScope.slnx --no-build --filter FullyQualifiedName~CliSmokeTests.PreviewCommandUsesProjectPreviewProfileAndAllowsExplicitOverrides` passed after W19 preview profiles.
- `2026-06-09`: `dotnet test AvaScope.slnx --no-build --filter FullyQualifiedName~CliSmokeTests.CreatePreviewSessionCommandUsesProjectPreviewProfile` passed after W19 preview profiles.
- `2026-06-09`: `dotnet test AvaScope.slnx --no-build --filter FullyQualifiedName~Cli` passed with 87 tests after W19 preview profiles.
- `2026-06-09`: source CLI `preview .\samples\AvaScope.GettingStartedApp\AvaScope.GettingStartedApp.csproj --profile main` smoke passed after W19 preview profiles.
- `2026-06-09`: `git diff --check` passed after W19 preview profiles.
- `2026-06-09`: `powershell -NoProfile -ExecutionPolicy Bypass -File .\eng\create-local-release.ps1 -SkipTests` passed after W20 agent workflow pack documentation updates.
- `2026-06-09`: packaged CLI `doctor --manifest-dir .\artifacts\samples\agent-workflow\sessions --preview-session-store .\artifacts\samples\agent-workflow\preview-sessions` passed after W20 agent workflow pack.
- `2026-06-09`: packaged CLI `preview .\samples\AvaScope.GettingStartedApp\AvaScope.GettingStartedApp.csproj --profile main --out .\artifacts\samples\agent-workflow\main-preview.png` passed after W20 agent workflow pack.
- `2026-06-09`: `git diff --check` passed after W20 agent workflow pack.
- `2026-06-09`: `dotnet build AvaScope.slnx` passed with 0 warnings and 0 errors after W21 runtime `clear_text` input.
- `2026-06-09`: `dotnet test AvaScope.slnx --no-build --filter Bridge` passed with 65 tests after W21 runtime `clear_text` input.
- `2026-06-09`: `dotnet test AvaScope.slnx --no-build --filter FullyQualifiedName~Cli` passed with 88 tests after W21 runtime `clear_text` input.
- `2026-06-09`: `git diff --check` passed after W21 runtime `clear_text` input.
- `2026-06-09`: `dotnet build AvaScope.slnx` passed with 0 warnings and 0 errors after W22 diagnostics issue provenance.
- `2026-06-09`: `dotnet test AvaScope.slnx --no-build --filter Protocol` passed with 36 tests after W22 diagnostics issue provenance.
- `2026-06-09`: `dotnet test AvaScope.slnx --no-build --filter Core` passed with 39 tests after W22 diagnostics issue provenance.
- `2026-06-09`: `dotnet test AvaScope.slnx --no-build --filter Mcp` passed with 29 tests after W22 diagnostics issue provenance.
- `2026-06-09`: `dotnet test AvaScope.slnx --no-build --filter FullyQualifiedName~Cli` passed with 89 tests after W22 diagnostics issue provenance.
- `2026-06-09`: `git diff --check` passed after W22 diagnostics issue provenance.
- `2026-06-09`: `dotnet build AvaScope.slnx` passed with 0 warnings and 0 errors after W23 unchanged-input watch skip.
- `2026-06-09`: `dotnet test AvaScope.slnx --no-build --filter FullyQualifiedName~ProtocolContractTests.PreviewWatchResponseSerializesEvents` passed after W23 unchanged-input watch skip.
- `2026-06-09`: `dotnet test AvaScope.slnx --no-build --filter FullyQualifiedName~PreviewSessionRegistryTests` passed with 11 tests after W23 unchanged-input watch skip.
- `2026-06-09`: `dotnet test AvaScope.slnx --no-build --filter FullyQualifiedName~PreviewHost` passed with 31 tests after W23 unchanged-input watch skip.
- `2026-06-09`: `dotnet test AvaScope.slnx --no-build --filter FullyQualifiedName~CliSmokeTests.WatchPreviewSessionCommandReloadsWhenWatchedFileChanges` passed after W23 unchanged-input watch skip.
- `2026-06-09`: `git diff --check` passed after W23 unchanged-input watch skip.
- `2026-06-09`: `dotnet build AvaScope.slnx` passed with 0 warnings and 0 errors after W24 baseline-check report output.
- `2026-06-09`: `dotnet test AvaScope.slnx --no-build --filter FullyQualifiedName~ProtocolContractTests.PreviewBaselineResponsesSerializeStableShapes` passed after W24 baseline-check report output.
- `2026-06-09`: `dotnet test AvaScope.slnx --no-build --filter FullyQualifiedName~CliSmokeTests.BaselineCommandsCreateManifestPassCheckAndFailChangedCheck` passed after W24 baseline-check report output.
- `2026-06-09`: `dotnet test AvaScope.slnx --no-build --filter FullyQualifiedName~Cli` passed with 89 tests after W24 baseline-check report output.
- `2026-06-09`: source CLI sample `baseline-create` and `baseline-check --report` smoke passed after W24 baseline-check report output.
- `2026-06-09`: report file parse smoke passed for `.\artifacts\samples\w24-baseline\report.json` after W24 baseline-check report output.
- `2026-06-09`: `git diff --check` passed after W24 baseline-check report output.
- `2026-06-09`: `dotnet build AvaScope.slnx` passed with 0 warnings and 0 errors for W25 release-candidate validation.
- `2026-06-09`: `dotnet test AvaScope.slnx --no-build` passed with 203 tests for W25 release-candidate validation.
- `2026-06-09`: `powershell -NoProfile -ExecutionPolicy Bypass -File .\eng\create-local-release.ps1` passed for W25 release-candidate validation; Release build/test passed with 203 tests, packages and executable ZIPs were generated, 5 artifacts were verified, packaged doctor passed, and packaged sample preview passed.
- `2026-06-09`: `powershell -NoProfile -ExecutionPolicy Bypass -File .\eng\publish-github-release.ps1 -Tag v0.1.0 -DryRun` passed for W25 release-candidate validation.
- `2026-06-09`: packaged CLI doctor, MCP handoff, sample preview, baseline-create, baseline-check `--report`, and report parse smoke passed for W25 release-candidate validation.
- `2026-06-09`: `git check-ignore -v` confirmed W25 generated release and baseline report artifacts remain ignored under `artifacts/`.
- `2026-06-09`: `git diff --check` passed after W25 release-candidate audit updates.
- `2026-06-09`: `powershell -NoProfile -ExecutionPolicy Bypass -File .\eng\validate-release-commit.ps1 -Version 0.2.0 -CommitSubject "Release 0.2.0" -RequiredState "In Progress"` passed after release-based planning updates.
- `2026-06-09`: `git diff --check` passed after release-based planning updates.
- `2026-06-09`: `git diff --check` passed after standalone AvaScope positioning cleanup.
- `2026-06-09`: `git diff --check` passed after formal `v0.2.0` release-goal definition.
- `2026-06-09`: `git diff --check` passed after adding the Codex preview surface goal to `v0.2.0`.
- `2026-06-09`: `dotnet build AvaScope.slnx` passed with 0 warnings and 0 errors after Codex preview viewer implementation.
- `2026-06-09`: `dotnet test AvaScope.slnx --no-build --filter FullyQualifiedName~ProtocolContractTests.PreviewViewerResponseSerializesStableShape` passed after Codex preview viewer implementation.
- `2026-06-09`: `dotnet test AvaScope.slnx --no-build --filter FullyQualifiedName~PreviewSessionRegistryTests.PreviewViewerExporter` passed with 2 tests after Codex preview viewer implementation.
- `2026-06-09`: `dotnet test AvaScope.slnx --no-build --filter FullyQualifiedName~CliSmokeTests.PreviewSessionCommandsCreateListReloadAndClosePersistedSession` passed after Codex preview viewer implementation.
- `2026-06-09`: `dotnet test AvaScope.slnx --no-build --filter FullyQualifiedName~AvaScopeMcpToolsTests.PreviewViewerExportsFileBackedUrlForPreviewSession` passed after Codex preview viewer implementation.
- `2026-06-09`: `dotnet test AvaScope.slnx --no-build --filter FullyQualifiedName~McpStdioSmokeTests.ServerStartsOverStdioAndListsInitialTools` passed after Codex preview viewer implementation.
- `2026-06-09`: `git diff --check` passed after Codex preview viewer implementation.
- `2026-06-09`: `dotnet build AvaScope.slnx` passed with 0 warnings and 0 errors after runtime target handoff implementation.
- `2026-06-09`: runtime target handoff targeted tests passed: protocol contract shape, headless MCP bridge tree/screenshot/inspect/find/input paths, and CLI tree/find/input handoff smoke paths.
- `2026-06-09`: `dotnet test AvaScope.slnx --no-build` passed with 207 tests after runtime target handoff implementation.
- `2026-06-09`: `git diff --check` passed after runtime target handoff implementation.
- `2026-06-09`: `dotnet build AvaScope.slnx` passed with 0 warnings and 0 errors after preview failure triage implementation.
- `2026-06-09`: preview failure triage targeted tests passed: PreviewHostClient readiness details, PreviewHost readiness/build/render diagnostics, and CLI readiness/build detail preservation.
- `2026-06-09`: `dotnet test AvaScope.slnx --no-build` passed with 210 tests after preview failure triage implementation.
- `2026-06-09`: `git diff --check` passed after preview failure triage implementation.
- `2026-06-09`: `dotnet build AvaScope.slnx` passed with 0 warnings and 0 errors after live preview lifecycle decision implementation.
- `2026-06-09`: live preview lifecycle targeted tests passed: preview watch protocol lifecycle shape, Core watcher lifecycle fields, and CLI watch lifecycle output.
- `2026-06-09`: `dotnet test AvaScope.slnx --no-build` passed with 210 tests after live preview lifecycle decision implementation.
- `2026-06-09`: `git diff --check` passed after live preview lifecycle decision implementation.
- `2026-06-09`: `eng\collect-baseline-artifacts.ps1` synthetic report validation passed after visual regression CI artifact handoff implementation.
- `2026-06-09`: `dotnet build AvaScope.slnx` passed with 0 warnings and 0 errors after visual regression CI artifact handoff implementation.
- `2026-06-09`: `dotnet test AvaScope.slnx --no-build` passed with 210 tests after visual regression CI artifact handoff implementation.
- `2026-06-09`: `git diff --check` passed after visual regression CI artifact handoff implementation.
- `2026-06-09`: `dotnet build AvaScope.slnx` passed with 0 warnings and 0 errors for `v0.2.0` release-candidate validation.
- `2026-06-09`: `dotnet test AvaScope.slnx --no-build` passed with 210 tests for `v0.2.0` release-candidate validation.
- `2026-06-09`: `powershell -NoProfile -ExecutionPolicy Bypass -File .\eng\create-local-release.ps1` passed for `v0.2.0`; Release build/test passed with 210 tests, three `0.2.0` packages, win-x64 and linux-x64 framework-dependent ZIPs, release manifest, packaged doctor smoke, and packaged sample preview smoke.
- `2026-06-09`: `powershell -NoProfile -ExecutionPolicy Bypass -File .\eng\publish-github-release.ps1 -Tag v0.2.0 -DryRun` passed for `v0.2.0` release assets.
- `2026-06-09`: `powershell -NoProfile -ExecutionPolicy Bypass -File .\eng\validate-release-commit.ps1 -Version 0.2.0 -CommitSubject "Release 0.2.0" -RequiredState "Release Candidate"` passed before the release commit.
- `2026-06-09`: `git diff --check` passed for `v0.2.0` release-candidate validation.
- `2026-06-09`: GitHub Release run `27194369827` failed before publish in `Create release artifacts` because `CliSmokeTests.InputCommandSendsClickThroughBridgePipe` timed out waiting for the fake bridge named-pipe request on the hosted runner.
- `2026-06-09`: `dotnet build AvaScope.slnx` passed with 0 warnings and 0 errors after stabilizing the CLI fake bridge pipe wait for hosted Release tests.
- `2026-06-09`: targeted CLI bridge input tests passed after the hosted-runner timeout fix: `dotnet test AvaScope.slnx --no-build --filter FullyQualifiedName~CliSmokeTests.InputCommandSendsClickThroughBridgePipe` and `dotnet test AvaScope.slnx -c Release --no-build --filter FullyQualifiedName~CliSmokeTests.InputCommandSendsClickThroughBridgePipe`.
- `2026-06-09`: `powershell -NoProfile -ExecutionPolicy Bypass -File .\eng\create-local-release.ps1` passed again after the hosted-runner timeout fix; Release build/test passed with 210 tests and the `0.2.0` artifacts were regenerated.
- `2026-06-09`: `powershell -NoProfile -ExecutionPolicy Bypass -File .\eng\publish-github-release.ps1 -Tag v0.2.0 -DryRun` and `powershell -NoProfile -ExecutionPolicy Bypass -File .\eng\validate-release-commit.ps1 -Version 0.2.0 -CommitSubject "Release 0.2.0" -RequiredState "Release Candidate"` passed after the hosted-runner timeout fix.
- `2026-06-09`: `git diff --check` passed after the hosted-runner timeout fix.
- `2026-06-09`: GitHub Release run `27195202070` passed for `v0.2.0`; it validated the release commit, created artifacts, published NuGet packages, published GitHub Packages, created tag `v0.2.0`, and uploaded GitHub Release assets.
- `2026-06-09`: CI run `27195202091` passed for release head commit `bb471af`.
- `2026-06-09`: `gh release view v0.2.0` verified GitHub Release `AvaScope 0.2.0` with six uploaded assets: three `0.2.0` `.nupkg` files, win-x64 and linux-x64 framework-dependent ZIPs, and `release-manifest.json`.
- `2026-06-09`: `git ls-remote --tags origin refs/tags/v0.2.0` verified tag `v0.2.0` points to release head commit `bb471af`.
- `2026-06-09`: `git diff --check` passed after recording `v0.2.0` release completion metadata.
- `2026-06-09`: post-release CI run `27195793057` failed in Release tests because the fake bridge server could read a partial named-pipe JSON request under hosted-runner timing.
- `2026-06-09`: `dotnet build AvaScope.slnx` passed with 0 warnings and 0 errors after switching the CLI fake bridge pipe reader from byte-at-a-time reads to chunked reads.
- `2026-06-09`: targeted fake bridge CLI tests passed after chunked pipe reading: `dotnet test AvaScope.slnx --no-build --filter "FullyQualifiedName~CliSmokeTests.InputCommandSendsClickThroughBridgePipe|FullyQualifiedName~CliSmokeTests.InspectNodeCommandReadsNodeThroughBridgePipe"` passed with 3 tests.
- `2026-06-09`: `dotnet build AvaScope.slnx -c Release` passed with 0 warnings and 0 errors after chunked pipe reading.
- `2026-06-09`: `dotnet test AvaScope.slnx -c Release --no-build` passed with 210 tests after chunked pipe reading.
- `2026-06-09`: `git diff --check` passed after chunked fake bridge pipe reader stabilization.
- `2026-06-08`: `dotnet test AvaScope.slnx -c Release --filter FullyQualifiedName~CliSmokeTests.CloseSessionCommandClosesThroughBridgePipe` passed after W8 CI failure hardening.
- `2026-06-08`: `dotnet test AvaScope.slnx -c Release --no-build` passed with 179 tests after W8 CI failure hardening.
- `2026-06-08`: `powershell -NoProfile -ExecutionPolicy Bypass -File .\eng\publish-github-release.ps1 -Tag v0.1.0 -DryRun` passed after W8 GitHub Release creation hardening.
- `2026-06-08`: `powershell -NoProfile -ExecutionPolicy Bypass -File .\eng\validate-bug-reports.ps1` and `git diff --check` passed after W8 CI/Release follow-up fixes.
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

- Status: `Done`
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
  - Done: stored `FEAT-0009` through `FEAT-0015` from the post-`v0.5.0` runtime/debugging wishlist and scheduled them for `v0.6.0`.
  - Done: user explicitly authorized implementation of the original stored `FEAT-0001` through `FEAT-0007` tickets, so that historical work moved to W9.
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

### W8 CI Release Follow-Up

- Status: `Done`
- Goal: fix the first CI and Release workflow failures observed after pushing the version-bump release infrastructure.
- Deliverables: GitHub Release creation hardening, CLI fake bridge pipe test hardening, tracking update, validation, commit, push.
- Progress:
  - Done: made `eng/publish-github-release.ps1` treat a missing GitHub Release as an expected create-path condition instead of a terminating native-command error.
  - Done: hardened the CLI fake bridge named-pipe test helper to ignore empty probe/closed connections and wait for a real JSON request.
  - Done: validated the previously failing close-session smoke test, the full Release test suite, the GitHub Release dry-run, intake validation, and whitespace checks.
- Acceptance Criteria:
  - GitHub Release publishing can create the release when the tag exists but the release record does not.
  - CLI fake bridge tests do not fail when a pipe connection closes before sending a request line.
  - Local validation covers the previously failing CI test and release script path.
- Validation:
  - `dotnet test AvaScope.slnx -c Release --filter FullyQualifiedName~CliSmokeTests.CloseSessionCommandClosesThroughBridgePipe`
  - `dotnet test AvaScope.slnx -c Release --no-build`
  - `powershell -NoProfile -ExecutionPolicy Bypass -File .\eng\publish-github-release.ps1 -Tag v0.1.0 -DryRun`
  - `powershell -NoProfile -ExecutionPolicy Bypass -File .\eng\validate-bug-reports.ps1`
  - `git diff --check`

### W9 Feature Ticket Implementation

- Status: `Done`
- Goal: implement all stored feature tickets `FEAT-0001` through `FEAT-0007`, plus protocol, adapter, validation, and documentation support needed for the complete requested feature set.
- Deliverables: structured preview diagnostics, layout warnings, computed property/style inspection, multi-size preview, screenshot diff/baseline comparison, scoped preview/session cleanup, feature ledger updates, documentation, tests, commit, push.
- Progress:
  - Done: audited feature tickets, current protocol/Core/PreviewHost/Bridge/CLI/MCP paths, local Avalonia 12.0.4 API docs, and NuGet update state.
  - Done: added transport-neutral protocol DTOs for preview diagnostics, computed properties, preview batches, image diffs, preview-session diagnostics, and cleanup responses.
  - Done: added PreviewHost binding/resource diagnostics and advisory layout warnings.
  - Done: added runtime `inspect_node` computed property inspection through public Avalonia diagnostics.
  - Done: added Core/CLI/MCP multi-size preview with deterministic per-size outputs and optional contact sheets.
  - Done: added opt-in CLI screenshot diff comparison with tolerance, structured result output, and explicit diff artifact path.
  - Done: added preview-session diagnostics and scoped cleanup for stale or invalid AvaScope-owned preview-session records.
  - Done: updated feature request ledger, ticket files, README, validation guide, and gap audit.
  - Done: final build, targeted tests, full-suite validation, intake validation, and diff-check passed.
- Acceptance Criteria:
  - `FEAT-0001`: preview results include bounded binding/resource diagnostics when rendering succeeds; missing `DataContext`, unresolved resource keys, invalid converter resources, and resolvable binding path failures are reported where public APIs and source metadata allow.
  - `FEAT-0002`: preview diagnostics include bounded layout warnings for text clipping/truncation, overlap, clipped/overflowing content, unreachable content, and too-small hit targets without blocking screenshot generation.
  - `FEAT-0003`: `inspect_node` returns bounded computed visual/style/layout property values with public Avalonia diagnostic priority where available, and `unknown`/`not_available` instead of guessed provenance.
  - `FEAT-0004`: automatic design-size recognition remains covered by W2 and is re-validated.
  - `FEAT-0005`: CLI and MCP support multi-size preview requests with deterministic per-size output paths, per-size success/failure entries, and optional contact-sheet output while preserving single-size compatibility.
  - `FEAT-0006`: CLI supports opt-in screenshot diff/baseline comparison with deterministic same-size comparison, dimension-mismatch diagnostics, explicit diff output paths, configurable tolerance, and no destructive baseline update.
  - `FEAT-0007`: cleanup workflow is scoped to AvaScope-owned preview/session metadata and never kills arbitrary processes by name alone; diagnostics can report stale preview session records.
  - Protocol/Core remain transport-neutral and MCP stays a thin adapter over reusable behavior.
- Validation:
  - `dotnet build AvaScope.slnx`
  - `dotnet test AvaScope.slnx --no-build --filter Protocol`
  - `dotnet test AvaScope.slnx --no-build --filter FullyQualifiedName~PreviewHost`
  - `dotnet test AvaScope.slnx --no-build --filter FullyQualifiedName~Cli`
  - `dotnet test AvaScope.slnx --no-build --filter Bridge`
  - `dotnet test AvaScope.slnx --no-build --filter Mcp`
  - `dotnet test AvaScope.slnx --no-build`
  - `powershell -NoProfile -ExecutionPolicy Bypass -File .\eng\validate-bug-reports.ps1`
  - `git diff --check`

### W10 CLI Preview Session Workflow

- Status: `Done`
- Goal: make durable preview sessions available from `avascope` without requiring an MCP client.
- Deliverables: CLI `create-preview-session`, `list-preview-sessions`, `reload-preview-session` or compatible reload path, `close-preview-session`, README/validation updates, tests, commit, push.
- Progress:
  - Done: inspected current Core preview-session registry/store and CLI command routing.
  - Done: added CLI `create-preview-session`, `list-preview-sessions`, `reload-preview-session`, and `close-preview-session`.
  - Done: added `AVASCOPE_PREVIEW_SESSION_STORE` override for deterministic local/test store isolation.
  - Done: added CLI smoke coverage for persisted create/list/reload/close and missing preview-session errors.
  - Done: updated README, validation guide, and gap audit.
  - Done: build, targeted CLI/Core tests, full-suite validation, and diff-check passed.
- Acceptance Criteria:
  - CLI can create a preview-session record from project/view/output/theme/culture/design-data arguments and return `ToolResult<PreviewSessionSummary>`.
  - CLI can list persisted preview-session records after a new CLI process starts.
  - CLI can reload an active persisted preview session and update its latest render result.
  - CLI can close a persisted preview session and return structured errors for missing or invalid ids.
  - Existing runtime `reload --session` behavior remains compatible.
  - Preview sessions remain metadata records; CLI does not keep user project code loaded.
- Validation:
  - `dotnet build AvaScope.slnx`
  - `dotnet test AvaScope.slnx --no-build --filter FullyQualifiedName~Cli`
  - `dotnet test AvaScope.slnx --no-build --filter FullyQualifiedName~PreviewSessionRegistryTests`
  - `dotnet test AvaScope.slnx --no-build`
  - `git diff --check`

### W11 Live Preview File Watch Reload

- Status: `Done`
- Goal: add an explicit file-watch workflow for preview sessions that re-renders through isolated PreviewHost child processes when watched project or AXAML files change.
- Deliverables: Core watch orchestration, CLI watch command, bounded event/debounce behavior, tests, documentation, commit, push.
- Progress:
  - Done: added transport-neutral preview watch event/response DTOs.
  - Done: added Core `PreviewSessionWatcher` with explicit timeout, debounce, max-reloads, explicit or derived watch paths, and isolated preview-session reload.
  - Done: added CLI `watch-preview-session --session <id> --timeout-ms <ms>` command.
  - Done: added protocol, Core, and CLI smoke coverage for watch reload.
  - Done: updated README, validation guide, and gap audit.
  - Done: build, targeted tests, full-suite validation, and diff-check passed.
- Acceptance Criteria:
  - Watch mode requires an existing preview-session id and explicit timeout or stop behavior for testability.
  - File changes trigger preview-session reload without keeping user code loaded in the CLI process.
  - Rapid changes are debounced and reported as bounded structured events.
  - Watch output remains local-only and does not alter baselines automatically.
- Validation:
  - `dotnet build AvaScope.slnx`
  - `dotnet test AvaScope.slnx --no-build --filter FullyQualifiedName~Cli`
  - `dotnet test AvaScope.slnx --no-build --filter FullyQualifiedName~Core`
  - `dotnet test AvaScope.slnx --no-build`
  - `git diff --check`

### W12 Visual Regression Workflow

- Status: `Done`
- Goal: layer baseline-set creation and checking over the existing multi-size preview and screenshot diff primitives.
- Deliverables: baseline manifest protocol/Core model, CLI baseline create/check commands, deterministic artifacts, CI-friendly exit codes, tests, documentation, commit, push.
- Progress:
  - Done: added baseline manifest, create response, check response, and per-entry protocol DTOs.
  - Done: added Core `PreviewBaselineManager` over existing multi-size preview and image diff primitives.
  - Done: added CLI `baseline-create` and `baseline-check` commands with explicit manifest/current/diff artifacts.
  - Done: added CLI/protocol coverage for pass and changed visual regression checks.
  - Done: updated README, validation guide, and gap audit.
  - Done: build, targeted tests, full-suite validation, and diff-check passed.
- Acceptance Criteria:
  - Baseline creation records explicit project/view/theme/culture/DPI/size metadata and image paths.
  - Baseline checking re-renders the requested variants, compares images with tolerance, and writes explicit diff artifacts.
  - Changed baselines return non-zero CLI exit codes while preserving structured results.
  - No command mutates or replaces baselines unless explicitly named as a create/update operation.
- Validation:
  - `dotnet build AvaScope.slnx`
  - `dotnet test AvaScope.slnx --no-build --filter FullyQualifiedName~Cli`
  - `dotnet test AvaScope.slnx --no-build --filter FullyQualifiedName~Core`
  - `dotnet test AvaScope.slnx --no-build`
  - `git diff --check`

### W13 Deeper Diagnostics

- Status: `Done`
- Goal: improve preview/runtime diagnostics beyond W9 while staying on public Avalonia APIs or documented logging hooks.
- Deliverables: deeper binding/resource/style diagnostics where reliable, bounded protocol fields, tests, documentation, commit, push.
- Progress:
  - Done: verified current Avalonia 12.0.4 public diagnostic/binding surfaces from local reference XML and kept runtime computed inspection on public `Avalonia.Diagnostics.GetDiagnostic(...)`.
  - Done: added source-backed inherited `x:DataType` metadata collection for preview binding references.
  - Done: added advisory `binding_datatype_path_not_found`, `binding_datatype_not_resolved`, and `compiled_binding_missing_datatype` preview diagnostics without blocking otherwise successful screenshots.
  - Done: added typed-binding PreviewHost smoke coverage and documentation updates.
- Acceptance Criteria:
  - Done: diagnostics do not rely on private Avalonia internals or unsupported reflection over runtime engine state.
  - Done: compiled-binding and typed-binding diagnostics are added only from source metadata plus project assembly type resolution; deeper runtime binding-engine/resource-chain/style provenance remains deferred where public APIs do not expose reliable signals.
  - Done: unsupported or unavailable details return explicit structured diagnostic codes/details instead of inferred provenance.
  - Done: diagnostics remain bounded and advisory unless rendering itself fails.
- Validation:
  - Passed: `dotnet build AvaScope.slnx`
  - Passed: `dotnet test AvaScope.slnx --no-build --filter FullyQualifiedName~PreviewHost`
  - Passed: `dotnet test AvaScope.slnx --no-build --filter Bridge`
  - Passed: `dotnet test AvaScope.slnx --no-build`
  - Passed: `git diff --check`

### W14 Richer Runtime Input

- Status: `Done`
- Goal: expand local runtime input beyond the current pointer/key/focus subset while preserving non-destructive local-only safety.
- Deliverables: richer pointer button state, drag/drop or text-input improvements where public Avalonia APIs allow, tests, documentation, commit, push.
- Progress:
  - Done: selected target-aware TextBox text editing as the smallest deterministic public-API-backed runtime input improvement.
  - Done: `key_text` can write to a focused TextBox or explicit target node id, rejects read-only TextBox targets, and replaces the current selection when one exists.
  - Done: added headless bridge coverage for target-node text input and selection replacement, and CLI pipe coverage for target-node propagation.
- Acceptance Criteria:
  - Done: no new action was needed; the existing `key_text` protocol action uses the existing explicit `targetNodeId` request field.
  - Done: runtime input stays local-only and does not introduce destructive actions.
  - Done: headless bridge tests cover the successful target-aware text path, and existing invalid input paths remain covered.
  - Done: unsupported read-only or non-TextBox text targets return structured errors.
- Validation:
  - Passed: `dotnet build AvaScope.slnx`
  - Passed: `dotnet test AvaScope.slnx --no-build --filter Bridge`
  - Passed: `dotnet test AvaScope.slnx --no-build --filter FullyQualifiedName~Cli`
  - Passed: `dotnet test AvaScope.slnx --no-build`
  - Passed: `git diff --check`

### W15 Preview Startup Parity

- Status: `Done`
- Goal: safely improve PreviewHost startup parity for real Avalonia projects without weakening the isolated child-process boundary.
- Deliverables: audited startup/lifetime expansion, opt-in behavior where needed, tests, documentation, commit, push.
- Progress:
  - Done: selected `Application.DataContext` fallback transfer as the smallest startup-parity improvement that does not require desktop lifetime hooks.
  - Done: PreviewHost now applies App.Initialize-created `Application.DataContext` as the root preview `DataContext` only when no explicit design data, design-time DataContext, or view-owned DataContext exists.
  - Done: added PreviewHost smoke coverage proving App.DataContext render output works while `OnFrameworkInitializationCompleted()` remains unfired.
- Acceptance Criteria:
  - Done: default preview remains isolated and does not unexpectedly run app lifetime hooks that create windows or start services.
  - Done: broader startup behavior is limited to public Application.DataContext fallback after App.Initialize and is documented.
  - Done: PreviewHost keeps user code out of MCP/CLI processes.
  - Done: existing App.axaml resource/style/data-template behavior remains compatible.
- Validation:
  - Passed: `dotnet build AvaScope.slnx`
  - Passed: `dotnet test AvaScope.slnx --no-build --filter FullyQualifiedName~PreviewHost`
  - Passed: `dotnet test AvaScope.slnx --no-build`
  - Passed: `git diff --check`

### W16 Distribution Hardening

- Status: `Done`
- Goal: harden executable distribution beyond current framework-dependent Windows/Linux ZIPs.
- Deliverables: self-contained artifact path or explicit decision, macOS artifact policy, installer decision, CI/release updates, tests/scripts, documentation, commit, push.
- Progress:
  - Done: added opt-in self-contained executable ZIP support to local packaging, release verification, local release creation, and GitHub Release asset validation/publish scripts.
  - Done: kept framework-dependent ZIPs as the default CI/GitHub Release artifact shape.
  - Done: hardened executable packaging cleanup with output-root process detection and retrying deletion of old package artifacts.
  - Done: documented self-contained validation/publish commands, installer deferral, and macOS policy deferral.
- Acceptance Criteria:
  - Done: release scripts can produce and verify opt-in self-contained artifacts as well as the default framework-dependent artifact set.
  - Done: artifact manifest covers all generated release artifacts with hashes, sizes, and executable package kind.
  - Done: CI remains credential-safe for validation-only runs; default CI/release behavior remains framework-dependent unless the package kind is explicitly changed.
  - Done: documentation distinguished framework-dependent defaults, opt-in self-contained artifacts, then-current installer status, and deferred macOS/installer policy.
- Validation:
  - Passed: `powershell -NoProfile -ExecutionPolicy Bypass -File .\eng\create-local-release.ps1 -RuntimeIdentifiers win-x64 -ExecutablePackageKind self-contained -SkipTests -SkipSampleSmoke`
  - Passed: `powershell -NoProfile -ExecutionPolicy Bypass -File .\eng\publish-github-release.ps1 -Tag v0.1.0 -ExecutableRuntimeIdentifiers win-x64 -ExecutablePackageKind self-contained -DryRun`
  - Passed: `powershell -NoProfile -ExecutionPolicy Bypass -File .\eng\create-local-release.ps1`
  - Passed: `powershell -NoProfile -ExecutionPolicy Bypass -File .\eng\publish-github-release.ps1 -Tag v0.1.0 -DryRun`
  - Passed: `powershell -NoProfile -ExecutionPolicy Bypass -File .\eng\publish-nuget.ps1 -DryRun`
  - Passed: `git diff --check`

### W17 Plan Refresh And Alpha Gate

- Status: `Done`
- Goal: close the stale post-W16 planning state and define the next product-aligned development period before new implementation work begins.
- Deliverables: refreshed `Current Focus`, `Next Action`, W17-W25 milestone definitions, audit alignment notes, validation, commit, push.
- Progress:
  - Done: selected CLI doctor/self-test as the next active slice because it improves first-user and agent reliability without broadening runtime safety boundaries.
  - Done: recorded W17-W25 as the next milestone sequence covering diagnostics, profiles, agent workflow documentation, runtime input, preview performance, visual-regression CI, and release-candidate validation.
- Acceptance Criteria:
  - Done: exactly one milestone is marked `In Progress`.
  - Done: W17-W25 have explicit goals, deliverables, acceptance criteria, and validation commands.
  - Done: stale post-W16 handoff wording is replaced by an actionable W18 next action.
- Validation:
  - Passed: `git diff --check`

### W18 CLI Doctor And Self-Test

- Status: `Done`
- Goal: add a first-class local self-test command that reports AvaScope runtime readiness, preview-host readiness, local bridge discovery state, preview-session store state, and packaged-command availability without loading user projects.
- Deliverables: CLI `doctor` command, structured protocol/core or CLI DTOs as needed, tests, README/validation updates, commit, push.
- Progress:
  - Done: added `avascope doctor` with structured `DoctorResponse` and `DoctorCheck` protocol DTOs.
  - Done: doctor reports CLI/MCP/PreviewHost co-location, bridge manifest directory state, bridge session diagnostics, preview-session store state, preview-session diagnostics, and actionable issues without loading user projects.
  - Done: added deterministic source CLI smoke tests with isolated manifest/store paths.
  - Done: added packaged Windows doctor smoke to the local release script with isolated manifest/store paths.
- Acceptance Criteria:
  - Done: doctor does not build or load user projects.
  - Done: doctor reports service identity, CLI path/base directory, preview host availability, MCP assembly availability, bridge manifest directory state, preview-session store state, and actionable issues.
  - Done: doctor can run with no arguments and supports bounded JSON output consistent with other CLI commands.
  - Done: invalid arguments return deterministic CLI errors.
- Validation:
  - Passed: `dotnet build AvaScope.slnx`
  - Passed: `dotnet test AvaScope.slnx --no-build --filter FullyQualifiedName~ProtocolContractTests.DoctorResponseSerializesStableReadinessShape`
  - Passed: `dotnet test AvaScope.slnx --no-build --filter FullyQualifiedName~CliSmokeTests.DoctorCommandReportsLocalReadiness`
  - Passed: `dotnet test AvaScope.slnx --no-build --filter FullyQualifiedName~CliSmokeTests.DoctorCommandRejectsInvalidArguments`
  - Passed: `dotnet test AvaScope.slnx --no-build --filter FullyQualifiedName~Cli`
  - Passed: `dotnet test AvaScope.slnx --no-build --filter Protocol`
  - Passed: source CLI doctor smoke with isolated manifest/store paths
  - Passed: `powershell -NoProfile -ExecutionPolicy Bypass -File .\eng\create-local-release.ps1`
  - Passed: `powershell -NoProfile -ExecutionPolicy Bypass -File .\eng\create-local-release.ps1 -SkipTests`
  - Passed: `git diff --check`

### W19 Preview Profiles

- Status: `Done`
- Goal: make repeated preview commands less brittle by allowing project-local named preview profiles.
- Deliverables: `avascope.preview.json` profile schema, CLI profile loading for preview and preview-session creation, sample profile, tests, documentation, commit, push.
- Progress:
  - Done: added project-local `avascope.preview.json` profile loading for `preview` and `create-preview-session`.
  - Done: profile values support view, output, dimensions, DPI, theme, culture, design data type, sizes, contact sheet, and display name.
  - Done: explicit CLI options override profile values, and profile output/contact-sheet paths resolve relative to the profile file.
  - Done: added a getting-started sample profile and CLI smoke coverage.
- Acceptance Criteria:
  - Done: existing explicit CLI options remain compatible and override profile values.
  - Done: missing or invalid profile files return structured diagnostics.
  - Done: the getting-started sample includes at least one valid profile.
  - Done: profile loading does not execute user code.
- Validation:
  - Passed: `dotnet build AvaScope.slnx`
  - Passed: `dotnet test AvaScope.slnx --no-build --filter FullyQualifiedName~CliSmokeTests.PreviewCommandUsesProjectPreviewProfileAndAllowsExplicitOverrides`
  - Passed: `dotnet test AvaScope.slnx --no-build --filter FullyQualifiedName~CliSmokeTests.CreatePreviewSessionCommandUsesProjectPreviewProfile`
  - Passed: `dotnet test AvaScope.slnx --no-build --filter FullyQualifiedName~Cli`
  - Passed: source CLI sample profile preview smoke
  - Passed: `git diff --check`

### W20 Agent Workflow Pack

- Status: `Done`
- Goal: document and validate an agent-ready workflow that exercises preview, runtime bridge inspection, diagnostics, visual tree, input, screenshot, and diff commands from the packaged CLI.
- Deliverables: workflow documentation, sample walkthrough updates, validation commands, commit, push.
- Progress:
  - Done: added `docs/AGENT_WORKFLOW.md` as a packaged-CLI runbook for doctor, preview profiles, preview sessions, runtime bridge inspection, screenshots, input, diff, baseline, and cleanup.
  - Done: linked the workflow from root and sample README files.
  - Done: added validation commands for packaged doctor and profile preview smoke paths.
- Acceptance Criteria:
  - Done: README or dedicated docs page includes source and packaged CLI examples.
  - Done: the workflow explains safety boundaries and common failure triage through `doctor` and `diagnostics`.
  - Done: the sample README stays aligned with the root workflow.
- Validation:
  - Passed: documentation review
  - Passed: `powershell -NoProfile -ExecutionPolicy Bypass -File .\eng\create-local-release.ps1 -SkipTests`
  - Passed: packaged CLI doctor smoke with isolated manifest/store paths
  - Passed: packaged CLI sample profile preview smoke
  - Passed: `git diff --check`

### W21 Runtime Interaction V2

- Status: `Done`
- Goal: expand runtime input where public Avalonia APIs and deterministic headless tests support it.
- Deliverables: richer non-destructive input behavior, protocol/CLI/MCP propagation if needed, bridge tests, documentation, commit, push.
- Progress:
  - Done: audited current pointer/key support and selected `clear_text` as the smallest deterministic runtime input extension.
  - Done: added protocol and CLI support for `clear_text`.
  - Done: bridge now clears a focused or targeted writable `TextBox`, resets caret/selection, and rejects read-only targets with a structured error.
  - Done: added headless bridge coverage and CLI fake-pipe coverage.
  - Done: documented `clear_text` in README and the packaged agent workflow.
- Acceptance Criteria:
  - Done: runtime bridge remains opt-in and local-only.
  - Done: no destructive runtime actions were introduced; `clear_text` only edits the selected writable `TextBox` target.
  - Done: unsupported or read-only input variants return structured errors instead of inferred behavior.
  - Done: new input behavior is covered by headless bridge and CLI adapter tests.
- Validation:
  - Passed: `dotnet build AvaScope.slnx`
  - Passed: `dotnet test AvaScope.slnx --no-build --filter Bridge`
  - Passed: `dotnet test AvaScope.slnx --no-build --filter FullyQualifiedName~Cli`
  - Passed: `git diff --check`

### W22 Diagnostics V2

- Status: `Done`
- Goal: improve diagnostics with bounded history and clearer severity/provenance without relying on private Avalonia internals.
- Deliverables: diagnostics history/provenance slice, protocol/core/adapter updates, tests, documentation, commit, push.
- Progress:
  - Done: audited diagnostics DTOs and selected a backward-compatible `diagnosticIssues` response list as the smallest useful v2 improvement.
  - Done: added protocol `DiagnosticIssue` plus stable source/severity constants.
  - Done: Core derives bounded diagnostic issues from diagnostics summary errors, bridge session diagnostics, preview-host diagnostics, and preview-session store diagnostics.
  - Done: CLI and MCP expose the same shape through the existing Core diagnostics response.
  - Done: README, validation guidance, and gap audit document the new provenance surface.
- Acceptance Criteria:
  - Done: diagnostics remain bounded and machine-readable; `diagnosticIssues` is capped in Core.
  - Done: unavailable/stale/invalid states are explicit through `status`, `severity`, and `provenance` instead of inferred private details.
  - Done: MCP and CLI expose the same diagnostic data shape through Core.
- Validation:
  - Passed: `dotnet build AvaScope.slnx`
  - Passed: `dotnet test AvaScope.slnx --no-build --filter Protocol`
  - Passed: `dotnet test AvaScope.slnx --no-build --filter Core`
  - Passed: `dotnet test AvaScope.slnx --no-build --filter Mcp`
  - Passed: `dotnet test AvaScope.slnx --no-build --filter FullyQualifiedName~Cli`
  - Passed: `git diff --check`

### W23 Faster Live Preview

- Status: `Done`
- Goal: reduce live-preview reload friction while preserving the isolated child-process boundary for user code.
- Deliverables: measured preview reload improvement or explicit deferral, process/session lifecycle tests, documentation, commit, push.
- Progress:
  - Done: compared the current one-shot reload/watch path with a persistent-host design and kept persistent user-code processes deferred for the public-alpha boundary.
  - Done: added watch-input snapshot detection so duplicate file watcher bursts that leave watched inputs unchanged produce a `skipped` event instead of launching another PreviewHost child process.
  - Done: added protocol and Core watcher coverage plus CLI watch reload regression coverage.
  - Done: documented skipped watch events and the continued one-shot isolated PreviewHost boundary.
- Acceptance Criteria:
  - Done: user project assemblies are still loaded only in isolated PreviewHost child processes, never MCP or CLI.
  - Done: persistent preview host processes remain deferred; no unmanaged persistent process lifecycle was introduced.
  - Done: existing one-shot preview and real-change watch reload behavior remain available.
- Validation:
  - Passed: `dotnet build AvaScope.slnx`
  - Passed: `dotnet test AvaScope.slnx --no-build --filter FullyQualifiedName~ProtocolContractTests.PreviewWatchResponseSerializesEvents`
  - Passed: `dotnet test AvaScope.slnx --no-build --filter FullyQualifiedName~PreviewSessionRegistryTests`
  - Passed: `dotnet test AvaScope.slnx --no-build --filter FullyQualifiedName~PreviewHost`
  - Passed: `dotnet test AvaScope.slnx --no-build --filter FullyQualifiedName~CliSmokeTests.WatchPreviewSessionCommandReloadsWhenWatchedFileChanges`
  - Passed: `git diff --check`

### W24 Visual Regression CI Kit

- Status: `Done`
- Goal: make baseline checking easier to run in CI and easier for agents to summarize.
- Deliverables: CI-friendly baseline report output, stable artifact layout, tests, validation docs, commit, push.
- Progress:
  - Done: added optional `baseline-check --report <report.json>` output.
  - Done: Core writes a stable JSON `PreviewBaselineCheckResponse` report and includes `reportPath` in the response.
  - Done: existing stdout shape, pass/fail behavior, current-image output, and diff-image output remain compatible.
  - Done: protocol and CLI tests cover report serialization and mismatch report creation.
  - Done: README, workflow, validation guide, and gap audit document CI artifact usage.
- Acceptance Criteria:
  - Done: baseline mismatches produce a stable machine-readable report when `--report` is supplied and still exit non-zero.
  - Done: current, baseline, diff, and report artifact paths are explicit in CLI arguments and response JSON.
  - Done: existing baseline commands remain compatible because `--report` is optional.
- Validation:
  - Passed: `dotnet build AvaScope.slnx`
  - Passed: `dotnet test AvaScope.slnx --no-build --filter FullyQualifiedName~ProtocolContractTests.PreviewBaselineResponsesSerializeStableShapes`
  - Passed: `dotnet test AvaScope.slnx --no-build --filter FullyQualifiedName~CliSmokeTests.BaselineCommandsCreateManifestPassCheckAndFailChangedCheck`
  - Passed: `dotnet test AvaScope.slnx --no-build --filter FullyQualifiedName~Cli`
  - Passed: source CLI sample `baseline-create` and `baseline-check --report` smoke
  - Passed: report file parse smoke
  - Passed: `git diff --check`

### W25 Public Alpha Release Candidate

- Status: `Done`
- Goal: validate the post-W17 development period as a coherent public-alpha release-candidate state.
- Deliverables: release-candidate audit refresh, full validation, packaged smoke checks, remaining deferrals, commit, push.
- Progress:
  - Done: refreshed `docs/PUBLIC_ALPHA_AUDIT.md` for the W17-W25 public-alpha release-candidate state.
  - Done: ran full Debug build/test validation.
  - Done: ran full local Release gate through `eng/create-local-release.ps1`.
  - Done: ran GitHub Release asset dry-run validation.
  - Done: ran packaged CLI doctor, MCP handoff, sample preview, baseline report, and artifact-ignore smoke checks.
- Acceptance Criteria:
  - Done: full local release gate passed.
  - Done: packaged CLI `doctor`, preview, MCP handoff, sample preview, and baseline smoke paths passed.
  - Done: README, validation docs, gap audit, and public-alpha audit agree on current capabilities and deferrals.
- Validation:
  - Passed: `dotnet build AvaScope.slnx`
  - Passed: `dotnet test AvaScope.slnx --no-build`
  - Passed: `powershell -NoProfile -ExecutionPolicy Bypass -File .\eng\create-local-release.ps1`
  - Passed: `powershell -NoProfile -ExecutionPolicy Bypass -File .\eng\publish-github-release.ps1 -Tag v0.1.0 -DryRun`
  - Passed: packaged CLI doctor, MCP handoff, sample preview, baseline create/check report, and report parse smoke commands
  - Passed: `git check-ignore -v` for generated release and W25 baseline report artifacts
  - Passed: `git diff --check`

### R0.2.0 Release Planning And Gate

- Status: `Done`
- Goal: move AvaScope development to release-based planning with an explicit `v0.2.0` target and a guarded release commit path.
- Deliverables: release plan, release goal definitions, release commit validation script, CI release guard, README/validation/gap/development-plan updates, commit, push.
- Progress:
  - Done: added `docs/RELEASE_PLAN.md` as the future release-scope source.
  - Done: defined `v0.2.0` scope before starting implementation.
  - Done: formalized `RG-0.2.0-1` through `RG-0.2.0-6` with success signals and milestone mapping.
  - Done: added `eng/validate-release-commit.ps1` so automatic release publishing on push requires commit subject `Release <version>` and a matching release-plan target.
  - Done: wired the release guard into the GitHub `Release` workflow for automatic push-based publishing.
  - Done: documented that `Directory.Build.props` version bumps are release commits, not planning commits.
- Acceptance Criteria:
  - Done: next release scope is visible before feature implementation starts.
  - Done: each `v0.2.0` release goal has a success signal and maps to a release milestone.
  - Done: automatic push-based release publishing is gated by release-plan state and release commit naming.
  - Done: current repository version remains `0.1.0`; no version bump happens until the `v0.2.0` scope is complete.
- Validation:
  - Passed: `powershell -NoProfile -ExecutionPolicy Bypass -File .\eng\validate-release-commit.ps1 -Version 0.2.0 -CommitSubject "Release 0.2.0" -RequiredState "In Progress"`
  - Passed: release-goal search confirmed `RG-0.2.0-1` through `RG-0.2.0-6` in `docs/RELEASE_PLAN.md`
  - Passed: `git diff --check`

### R0.2.0-M1 Runtime Workflow Hardening

- Status: `Done`
- Goal: make runtime node targeting easier and safer for agents using repeated CLI/MCP inspection and input workflows.
- Deliverables: selector/targeting improvements, stale-node diagnostics, CLI/MCP documentation, tests, validation, commit, push.
- Progress:
  - Done: audited current `find-nodes`, tree, `inspect-node`, `screenshot`, and `input` target handoff behavior.
  - Done: added `RuntimeTargetContext` to runtime tree, find, inspect, input, and screenshot response shapes while preserving existing fields.
  - Done: populated tree-node and find-match `target` context so agents can carry `sessionId`, `topLevelId`, `treeKind`, and `nodeId` through follow-up commands.
  - Done: added structured stale/missing target error details with requested target fields and `nextAction`.
  - Done: documented target handoff in README, agent workflow docs, and validation guidance.
- Acceptance Criteria:
  - Done: an agent can find a node and use the returned targeting data in follow-up inspect/input workflows without guessing.
  - Done: stale or invalid node references return structured, actionable errors.
  - Done: no remote control, destructive actions, or private Avalonia hooks are introduced.
- Validation:
  - Passed: `dotnet test AvaScope.slnx --no-build --filter "FullyQualifiedName~ProtocolContractTests|FullyQualifiedName~BridgeHeadlessSmokeTests.McpToolsListTopLevelsAndCaptureScreenshotThroughLocalBridgePipe|FullyQualifiedName~BridgeHeadlessSmokeTests.McpInputClicksButtonAndTypesTextThroughLocalBridgePipe|FullyQualifiedName~CliSmokeTests.TreeCommandReadsTreeThroughBridgePipe|FullyQualifiedName~CliSmokeTests.FindNodesCommandReadsMatchesThroughBridgePipe|FullyQualifiedName~CliSmokeTests.InputCommandSendsClickThroughBridgePipe"`
  - Passed: `dotnet build AvaScope.slnx`
  - Passed: `dotnet test AvaScope.slnx --no-build`
  - Passed: `git diff --check`

### R0.2.0-M2 Preview Diagnostics Readiness

- Status: `Done`
- Goal: make preview failures more actionable before agents retry expensive or impossible render commands.
- Deliverables: project/environment readiness diagnostics, CLI/MCP surface updates, docs, tests, validation, commit, push.
- Progress:
  - Done: audited preview host, Core client, CLI, SDK/build, project path, and view path diagnostics.
  - Done: added `preview_readiness_failed` for local project/view prerequisites that can be checked before build/render.
  - Done: added host-side details for missing co-located PreviewHost assemblies, dotnet process startup, host timeout, malformed host output, and host stderr.
  - Done: preserved project build failures as `preview_project_build_failed` with bounded `outputTail`, `phase=build`, and `nextAction`.
  - Done: documented readiness/build/render `error.details.phase` guidance in README, agent workflow docs, and validation guidance.
- Acceptance Criteria:
  - Done: preview readiness errors distinguish missing local prerequisites from project build/render failures.
  - Done: diagnostics stay bounded and do not execute arbitrary user code outside existing preview request boundaries.
- Validation:
  - Passed: `dotnet test AvaScope.slnx --no-build --filter "FullyQualifiedName~PreviewHostClientTests|FullyQualifiedName~PreviewHostSmokeTests|FullyQualifiedName~CliSmokeTests.PreviewCommandPreservesPreviewReadinessFailureDetails|FullyQualifiedName~CliSmokeTests.PreviewCommandPreservesPreviewFailureDetails"`
  - Passed: `dotnet build AvaScope.slnx`
  - Passed: `dotnet test AvaScope.slnx --no-build`
  - Passed: `git diff --check`

### R0.2.0-M3 Live Preview Lifecycle Decision

- Status: `Done`
- Goal: decide and implement the smallest safe live-preview improvement after unchanged-input skip events.
- Deliverables: lifecycle decision record, implementation or explicit deferral, tests/docs, validation, commit, push.
- Progress:
  - Done: evaluated persistent host process close, TTL, crash, and cleanup semantics against the current one-shot isolated PreviewHost boundary.
  - Done: added `PreviewLifecycleStatus` to `PreviewWatchResponse` so watch output explicitly reports one-shot child-process mode and persistent-host deferral.
  - Done: documented close, TTL, crash, cleanup, and next-step semantics in protocol output, README, agent workflow docs, and validation guidance.
- Acceptance Criteria:
  - Done: live-preview behavior has a concrete documented deferral with a tool-visible lifecycle status.
  - Done: no user project code runs inside MCP or the CLI process.
- Validation:
  - Passed: `dotnet test AvaScope.slnx --no-build --filter "FullyQualifiedName~ProtocolContractTests.PreviewWatchResponseSerializesEventsAndLatestSession|FullyQualifiedName~PreviewSessionRegistryTests.PreviewSessionWatcher|FullyQualifiedName~CliSmokeTests.WatchPreviewSessionCommandReloadsWhenWatchedFileChanges"`
  - Passed: `dotnet build AvaScope.slnx`
  - Passed: `dotnet test AvaScope.slnx --no-build`
  - Passed: `git diff --check`

### R0.2.0-M4 Visual Regression CI Integration

- Status: `Done`
- Goal: make baseline report/current/diff artifacts straightforward to publish from CI without changing local baseline command behavior.
- Deliverables: workflow example or CI helper, docs, tests or script validation, commit, push.
- Progress:
  - Done: added `eng\collect-baseline-artifacts.ps1` to collect a `baseline-check --report` JSON file plus referenced current and diff image artifacts into one upload directory.
  - Done: added an `artifact-manifest.json` handoff file containing the copied report/current/diff paths.
  - Done: documented the CI flow in `docs/VISUAL_REGRESSION_CI.md`, README, agent workflow docs, and validation guidance.
  - Done: kept baseline creation/check commands stable for local users; the helper runs after `baseline-check --report`.
- Acceptance Criteria:
  - Done: CI users can upload JSON report, current image, and diff image artifacts from a documented workflow.
  - Done: existing `baseline-create` and `baseline-check` behavior remains compatible.
- Validation:
  - Passed: `eng\collect-baseline-artifacts.ps1` synthetic report/current/diff copy validation
  - Passed: `dotnet build AvaScope.slnx`
  - Passed: `dotnet test AvaScope.slnx --no-build`
  - Passed: `git diff --check`

### R0.2.0-M5 Codex Preview Surface

- Status: `Done`
- Goal: make AvaScope previews usable from Codex through a local file-backed viewer and explicit MCP/CLI URL handoff.
- Deliverables: local file-backed preview viewer, preview/session `previewUrl` handoff, Codex in-app browser workflow docs, tests, validation, commit, push.
- Progress:
  - Done: added `PreviewViewerResponse` with `viewerPath`, `previewUrl`, generated timestamp, and preview-session metadata.
  - Done: added Core `PreviewViewerExporter` that writes a self-contained file-backed HTML viewer for a preview session's latest successful render.
  - Done: added CLI `preview-viewer --session <id> [--out <viewer.html>]`.
  - Done: added MCP `preview_viewer` for Codex handoff.
  - Done: documented the Codex in-app browser workflow around the returned `file://` `previewUrl`.
- Acceptance Criteria:
  - Done: a Codex user can open an AvaScope preview URL in the in-app browser and review screenshot/diagnostics state.
  - Done: MCP/CLI handoff includes enough information for Codex to present or open the viewer.
  - Done: the viewer is file-backed and local-only; it does not start a network listener or widen the bridge remote-control boundary.
- Validation:
  - Passed: `dotnet build AvaScope.slnx`
  - Passed: `dotnet test AvaScope.slnx --no-build --filter FullyQualifiedName~ProtocolContractTests.PreviewViewerResponseSerializesStableShape`
  - Passed: `dotnet test AvaScope.slnx --no-build --filter FullyQualifiedName~PreviewSessionRegistryTests.PreviewViewerExporter`
  - Passed: `dotnet test AvaScope.slnx --no-build --filter FullyQualifiedName~CliSmokeTests.PreviewSessionCommandsCreateListReloadAndClosePersistedSession`
  - Passed: `dotnet test AvaScope.slnx --no-build --filter FullyQualifiedName~AvaScopeMcpToolsTests.PreviewViewerExportsFileBackedUrlForPreviewSession`
  - Passed: `dotnet test AvaScope.slnx --no-build --filter FullyQualifiedName~McpStdioSmokeTests.ServerStartsOverStdioAndListsInitialTools`
  - Passed: `dotnet test AvaScope.slnx --no-build`
  - Passed: `git diff --check`

### R0.2.0-M6 Release Candidate And Version Bump

- Status: `Done`
- Goal: close `v0.2.0` as a release candidate, bump the version, and publish through the guarded release workflow.
- Deliverables: audit refresh, full release validation, `Directory.Build.props` version bump to `0.2.0`, release commit, push.
- Progress:
  - Done: confirmed R0.2.0-M1 through R0.2.0-M5 are `Done`.
  - Done: moved `docs/RELEASE_PLAN.md` release state to `Release Candidate`.
  - Done: ran full release gate and GitHub Release dry-run for `v0.2.0`.
  - Done: fixed the first remote Release workflow failure, a hosted-runner timeout in the CLI fake bridge input smoke test before publish.
  - Done: committed and pushed release head `bb471af` with subject `Release 0.2.0`.
  - Done: GitHub Release workflow published tag `v0.2.0`, packages, executable ZIPs, and release manifest.
- Acceptance Criteria:
  - Done: `Directory.Build.props` remained unchanged until all in-scope `v0.2.0` work was complete.
  - Done: local release commit guard passed for subject `Release 0.2.0` and `Release Candidate` state.
  - Done: published assets match the release manifest and the `v0.2.0` tag.
- Validation:
  - Passed: `dotnet build AvaScope.slnx`
  - Passed: `dotnet test AvaScope.slnx --no-build`
  - Passed: `powershell -NoProfile -ExecutionPolicy Bypass -File .\eng\create-local-release.ps1`
  - Passed: `powershell -NoProfile -ExecutionPolicy Bypass -File .\eng\publish-github-release.ps1 -Tag v0.2.0 -DryRun`
  - Passed: `powershell -NoProfile -ExecutionPolicy Bypass -File .\eng\validate-release-commit.ps1 -Version 0.2.0 -CommitSubject "Release 0.2.0" -RequiredState "Release Candidate"`
  - Passed: `git diff --check`
  - Passed: hosted-runner timeout fix targeted Debug and Release CLI bridge input tests
  - Passed: GitHub Release run `27195202070`
  - Passed: CI run `27195202091`
  - Passed: `gh release view v0.2.0`
  - Passed: `git ls-remote --tags origin refs/tags/v0.2.0`
  - Passed: post-release hosted-runner fake pipe reader fix targeted Debug tests and full Release test suite

### R0.2.1-M1 Theme-Aware Preview Wrapper Background

- Status: `Done`
- Goal: make PreviewHost wrapper windows use a theme-aware background for root controls that do not paint their own canvas.
- Deliverables: PreviewHost background resolution fix, smoke tests for dark theme wrapper rendering and explicit app window-style precedence, development-plan/release-plan updates, validation, commit, push.
- Progress:
  - Done: defined `v0.2.1` patch release goals in `docs/RELEASE_PLAN.md`.
  - Done: removed the hardcoded white local background from PreviewHost wrapper `Window` instances.
  - Done: applied a render-time fallback that preserves existing `Window.Background`, resolves theme-aware background resources, and falls back by requested theme only when no app/theme resource is available.
  - Done: added smoke coverage for dark Fluent background rendering and app-defined `Window` background style precedence.
  - Done: validated the original `SettingsView.axaml` dark preview scenario against the Debug CLI/PreviewHost build.
- Acceptance Criteria:
  - Done: dark previews for non-`Window` root controls without a root background no longer render a white canvas.
  - Done: project/app `Window` background styles are not overridden by the fallback host background.
  - Done: requested preview theme variant continues to drive theme dictionary/resource lookup.
- Validation:
  - Passed: `dotnet test AvaScope.slnx --filter "FullyQualifiedName~PreviewHostSmokeTests.PreviewHostUsesDarkFluentWindowBackgroundForTransparentRootControl|FullyQualifiedName~PreviewHostSmokeTests.PreviewHostKeepsAppWindowBackgroundStyleForTransparentRootControl"`
  - Passed: `dotnet build AvaScope.slnx`
  - Passed: `dotnet test AvaScope.slnx --no-build`
  - Passed: `git diff --check`
  - Passed: `dotnet .\src\AvaScope.Cli\bin\Debug\net10.0\avascope.dll preview <target-app-root>\TargetApp.csproj --view Views\SettingsView.axaml --out .\artifacts\validation\v0.2.1-theme-background\SettingsView-dark.png --width 1200 --height 900 --theme dark`

### R0.2.1-M2 Release Candidate And Version Bump

- Status: `Done`
- Goal: close `v0.2.1` as a release candidate, bump the version, and publish through the guarded release workflow.
- Deliverables: full release validation, `Directory.Build.props` version bump to `0.2.1`, release commit, push.
- Progress:
  - Done: R0.2.1-M1 reached `Done`.
  - Done: committed and pushed the completed theme-background fix.
  - Done: moved the `v0.2.1` target to `Release Candidate`.
  - Done: ran the local release gate and GitHub Release dry-run for `v0.2.1`.
  - Done: committed and pushed release head `d12fe8c` with subject `Release 0.2.1`.
  - Done: GitHub Release workflow published tag `v0.2.1`, packages, executable ZIPs, and release manifest.
- Acceptance Criteria:
  - Done: `Directory.Build.props` remained unchanged until all in-scope `v0.2.1` work was complete.
  - Done: local release commit guard passed for subject `Release 0.2.1` and `Release Candidate` state.
  - Done: published assets match the release manifest and the `v0.2.1` tag.
- Validation:
  - Passed: `powershell -NoProfile -ExecutionPolicy Bypass -File .\eng\create-local-release.ps1`
  - Passed: `powershell -NoProfile -ExecutionPolicy Bypass -File .\eng\publish-github-release.ps1 -Tag v0.2.1 -DryRun`
  - Passed: `powershell -NoProfile -ExecutionPolicy Bypass -File .\eng\validate-release-commit.ps1 -Version 0.2.1 -CommitSubject "Release 0.2.1" -RequiredState "Release Candidate"`
  - Passed: `git diff --check`
  - Passed: GitHub Release run `27200641766`
  - Passed: CI run `27200641755`
  - Passed: `gh release view v0.2.1`
  - Passed: `git ls-remote --tags origin refs/tags/v0.2.1`

### R0.2.2-M1 DataTemplate Binding Diagnostics

- Status: `Done`
- Goal: fix BUG-0003 DataTemplate binding diagnostic false positives by validating template-contained bindings against their template item context.
- Deliverables: DataTemplate-aware binding diagnostics, regression smoke tests, bug report status update, validation, commit, push.
- Progress:
  - Done: defined `v0.2.2` patch release goals in `docs/RELEASE_PLAN.md`.
  - Done: `x:DataType` binding diagnostics now short-circuit the root preview `DataContext` fallback when a declared data type is available.
  - Done: added `PreviewHostUsesDataTemplateDataTypeForBindingDiagnostics` to cover `ItemsControl.ItemTemplate` bindings with `x:CompileBindings="False"`.
- Acceptance Criteria:
  - Done: `ItemsControl.ItemTemplate` bindings with `x:DataType` do not warn against the root preview `DataContext`.
  - Done: `DataTemplate x:CompileBindings="False"` does not force root-context binding warnings when `x:DataType` is known.
  - Done: unresolved template `x:DataType` cases remain bounded advisory diagnostics instead of being silently misclassified.
- Validation:
  - Passed: `dotnet build tests/AvaScope.Tests/AvaScope.Tests.csproj --no-restore -v:minimal`
  - Passed: targeted BUG-0003 PreviewHost diagnostics tests with 4 tests.
  - Passed: `dotnet test tests/AvaScope.Tests/AvaScope.Tests.csproj --no-build`

### R0.2.2-M2 Template-Aware Layout Diagnostics

- Status: `Done`
- Goal: reduce BUG-0003 layout diagnostic noise for Avalonia layer/template internals and metric-only clipping.
- Deliverables: template-aware overlap filtering, text clipping tolerance, slider template hit-target handling, regression smoke tests, validation, commit, push.
- Progress:
  - Done: layout overlap diagnostics skip framework layer/template scopes and same-template sibling internals.
  - Done: text clipping diagnostics tolerate small font metric height deltas.
  - Done: slider-owned `RepeatButton` template parts no longer report independent hit-target warnings.
  - Done: added `PreviewHostSuppressesFluentTemplateLayoutNoise` for Fluent tab, checkbox, and slider template coverage.
- Acceptance Criteria:
  - Done: full-window root layer/template internals do not produce `elements_overlap` warnings.
  - Done: icon/control-template internal visuals do not produce noisy `elements_overlap` warnings.
  - Done: small tab header font metric deltas do not produce `text_clipped` warnings.
  - Done: slider internal `RepeatButton` parts are ignored through the owning `Slider`.
- Validation:
  - Passed: `dotnet build tests/AvaScope.Tests/AvaScope.Tests.csproj --no-restore -v:minimal`
  - Passed: targeted BUG-0003 PreviewHost diagnostics tests with 4 tests.
  - Passed: `dotnet test tests/AvaScope.Tests/AvaScope.Tests.csproj --no-build`

### R0.2.2-M3 Release Candidate And Version Bump

- Status: `Done`
- Goal: close `v0.2.2` as a release candidate, bump the version, and publish through the guarded release workflow.
- Deliverables: full release validation, `Directory.Build.props` version bump to `0.2.2`, release commit, push.
- Progress:
  - Done: R0.2.2-M1 and R0.2.2-M2 implementation validation passed.
  - Done: moved the `v0.2.2` target to `Release Candidate`.
  - Done: bumped `Directory.Build.props` to `0.2.2` for the release commit.
  - Done: committed and pushed `eac2bf1` (`Release 0.2.2`).
  - Done: verified the guarded GitHub Release workflow published `v0.2.2`.
- Acceptance Criteria:
  - Done: `Directory.Build.props` remained unchanged until all in-scope `v0.2.2` work was complete.
  - Done: local release commit guard passed for subject `Release 0.2.2` and `Release Candidate` state.
  - Done: published assets match the release manifest and the `v0.2.2` tag.
- Validation:
  - Passed: `dotnet build AvaScope.slnx`
  - Passed: `dotnet test AvaScope.slnx --no-build`
  - Passed: `powershell -NoProfile -ExecutionPolicy Bypass -File .\eng\create-local-release.ps1`
  - Passed: `powershell -NoProfile -ExecutionPolicy Bypass -File .\eng\publish-github-release.ps1 -Tag v0.2.2 -DryRun`
  - Passed: `git diff --check`
  - Passed: `powershell -NoProfile -ExecutionPolicy Bypass -File .\eng\validate-release-commit.ps1 -Version 0.2.2 -CommitSubject "Release 0.2.2" -RequiredState "Release Candidate"`
  - Passed: GitHub Release run `27205089688`
  - Passed: CI run `27205089675`
  - Passed: `gh release view v0.2.2`
  - Passed: `git ls-remote --tags origin refs/tags/v0.2.2`

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
- `2026-06-07`: README intentionally documents current limitations for input, preview resources, hot reload, and diagnostics so users do not assume coverage beyond implemented workflows.
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
- `2026-06-08`: W8 keeps GitHub Release creation idempotent by checking release existence without allowing an expected missing release to terminate the publish script.
- `2026-06-08`: W8 treats empty fake bridge pipe connections as test-harness noise and waits for the first non-empty newline-delimited JSON request.
- `2026-06-08`: The user explicitly authorized implementation of all stored feature tickets, so W1 intake is no longer the active workstream and W9 owns feature delivery.
- `2026-06-08`: W9 uses the installed Avalonia 12.0.4 reference XML plus upstream Avalonia source/docs for public API checks; `dotnet list package --outdated` reports no newer stable Avalonia packages from the current NuGet source.
- `2026-06-08`: Computed style/resource inspection will use public `Avalonia.Diagnostics.GetDiagnostic(...)` priority/diagnostic data where available and return explicit `unknown` or `not_available` provenance instead of inferring private style origins.
- `2026-06-08`: Preview layout warnings are advisory diagnostics produced after rendering; they must not make otherwise successful screenshots fail.
- `2026-06-08`: Screenshot diff and cleanup workflows remain explicit opt-in operations; cleanup may delete AvaScope-owned metadata and only terminate processes that can be tied to AvaScope-owned process metadata.
- `2026-06-08`: W9 cleanup does not terminate processes in this slice because AvaScope does not yet persist reliable owned child-process metadata for post-hoc process cleanup.
- `2026-06-08`: W13 deeper diagnostics use source metadata and built project assembly type resolution for `x:DataType` binding checks. They do not inspect private Avalonia binding-engine state, and unresolved type/provenance cases stay advisory structured diagnostics.
- `2026-06-08`: W14 expands runtime text input through the existing `key_text` action and `targetNodeId` request field instead of adding a new protocol action, because this preserves the current tool shape while making TextBox editing behavior more realistic.
- `2026-06-08`: W15 keeps full desktop/single-view lifetime startup deferred; PreviewHost may reuse App.Initialize-created `Application.DataContext` as a fallback preview root DataContext without invoking `OnFrameworkInitializationCompleted()`.
- `2026-06-09`: W17 selects CLI doctor/self-test as the next active slice after W16 because it improves onboarding, packaged-artifact sanity checks, and agent triage before deeper input, diagnostics, or live-preview behavior.
- `2026-06-09`: W18 doctor treats stale or unavailable bridge/preview-session records as actionable issues and exits non-zero, while release-script doctor smoke uses isolated manifest/store paths so package validation is not affected by previous local user sessions.
- `2026-06-09`: W19 preview profiles are project-local JSON files and do not execute user code; explicit CLI arguments override profile values so one-off agent commands can safely specialize a stored profile.
- `2026-06-09`: W20 uses packaged CLI examples as the primary agent workflow because they validate co-located CLI/MCP/PreviewHost behavior and are closer to public-alpha usage than Debug build commands.
- `2026-06-09`: W21 selected `clear_text` instead of drag/drop or richer pointer variants because targeted `TextBox` clearing can be implemented through stable public control state and deterministic headless tests while preserving the narrow non-destructive runtime control boundary.
- `2026-06-09`: W22 keeps legacy diagnostics `issues` intact and adds Core-derived `diagnosticIssues` as a v2 provenance layer so CLI, MCP, and future agents get severity/source/status/path metadata without adapter-specific logic or private Avalonia hooks.
- `2026-06-09`: W23 defers persistent preview host processes until close/TTL/crash semantics are designed; the faster live-preview slice instead skips unchanged-input file watcher bursts while preserving one-shot isolated PreviewHost rendering for real changes.
- `2026-06-09`: W24 keeps baseline-check stdout and exit behavior compatible while adding optional report-file output, so CI can upload current images, diff images, and a stable machine-readable report without changing existing local workflows.
- `2026-06-09`: W25 treats W17-W25 as a public-alpha release-candidate validation period rather than a publishing action; actual NuGet/GitHub Release upload remains gated by credentials and an explicit publish decision.
- `2026-06-09`: AvaScope development is release-based from `v0.2.0` onward: future feature work must belong to a declared release target in `docs/RELEASE_PLAN.md`, and `Directory.Build.props` version changes are release commits only.
- `2026-06-09`: Automatic push-based publishing now requires a release commit subject of `Release <version>` and a matching `docs/RELEASE_PLAN.md` target in `Release Candidate` state, reducing the chance that an accidental version bump publishes unfinished scope.
- `2026-06-09`: `v0.2.0` is scoped around repeated agent usability: runtime workflow hardening, preview diagnostics readiness, live-preview lifecycle decision, visual-regression CI integration, and a guarded release candidate/version bump.
- `2026-06-09`: Project documentation should describe AvaScope as its own Avalonia inspection, preview, and automation project. Do not frame it around external projects, third-party tools, or comparison-based positioning.
- `2026-06-09`: `v0.2.0` release goals use `RG-0.2.0-*` identifiers with success signals so release acceptance is evaluated against outcomes, not only task completion.
- `2026-06-09`: `v0.2.0` includes a Codex preview surface goal implemented through a local file-backed AvaScope viewer and MCP/CLI `previewUrl` handoff rather than depending on an undocumented native Codex sidebar extension.
- `2026-06-09`: Codex preview surface uses a file-backed, self-contained HTML viewer first because Codex supports file-backed previews and this avoids a long-lived preview server or network listener while still returning a `previewUrl`.
- `2026-06-09`: Runtime target handoff is implemented as additive protocol context instead of replacing existing top-level, tree-kind, or node-id fields so older callers remain compatible while agents get an unambiguous object to carry forward.
- `2026-06-09`: Preview failure triage keeps missing project/view/host/dotnet prerequisites in `phase=readiness`, project compilation in `phase=build`, and isolated XAML/render failures in `phase=render` so agents can decide whether retrying is useful.
- `2026-06-09`: Persistent preview hosts remain deferred for `v0.2.0`; the watch response now carries explicit one-shot child-process lifecycle status and documents close, TTL, crash, and cleanup semantics.
- `2026-06-09`: `v0.2.1` targets PreviewHost theme parity by resolving the wrapper `Window` background from applied app/window styles or requested theme resources instead of forcing a white local value on controls that do not paint their own root background.
- `2026-06-09`: `v0.2.2` targets BUG-0003 as an AvaScope PreviewHost diagnostics bug, not a target-app workaround: DataTemplate binding scope and Avalonia template/layer layout noise should be handled inside AvaScope diagnostics.
- `2026-06-09`: BUG-0003 binding diagnostics treat declared source `x:DataType` as authoritative for a binding path before falling back to root preview `DataContext`; layout diagnostics filter Avalonia framework/template internals instead of reporting them as user layout defects.
- `2026-06-09`: Animation support is tracked as a future diagnostics feature; the useful agent surface is deterministic time-offset screenshots, bounded frame artifacts, and structured animation metadata/diagnostics.
- `2026-06-09`: `v0.3.0` is scoped as a minor animation-diagnostics release because deterministic frame sampling, new artifact outputs, and CLI/MCP surfaces are additive product capabilities rather than patch-level fixes.
- `2026-06-09`: `v0.3.0` animation sampling uses public `AvaloniaHeadlessPlatform.ForceRenderTimerTick(count)` in isolated PreviewHost child processes because Avalonia 12 animation clock types are not public; repeated offsets inside one request reuse the first successful frame for that offset to keep duplicate artifacts stable, while moving-property metadata remains explicit `not_available` provenance.
- `2026-06-09`: The roadmap to `v1.0.0` is release-shaped rather than epic-shaped: `v0.4.0` hardens runtime attach/session reliability first, then `v0.5.0` preview fidelity, `v0.6.0` persistent live preview, `v0.7.0` visual regression CI, `v0.8.0` agent-facing UI intelligence features, `v0.9.0` protocol/integration beta hardening, and `v1.0.0` stable-surface verification.
- `2026-06-09`: `v0.4.0` starts with bridge session discovery and cleanup because stale manifests, ambiguous attach, dead processes, and mismatched targets are foundational reliability risks for every later runtime and editor workflow.
- `2026-06-09`: `v0.8.0` remains a product feature release instead of a stabilization-only release: accessibility/validation audit, visual issue overlays, suggested fixes, component/style inventory, and richer animation timeline diagnostics are planned before the `v0.9.0` beta freeze.
- `2026-06-10`: `v0.5.0` preview fidelity uses additive protocol fields (`projectInfo` and diagnostic triage metadata) so existing clients can ignore the new data while agents get project graph, provenance, suggested action, and non-applicable context.
- `2026-06-10`: Preview profile variants are project-local JSON overlays applied after the base profile and before explicit CLI options; variants do not introduce executable behavior or remote design-data loading.
- `2026-06-10`: After `v0.5.0` shipped, the post-release runtime/debugging wishlist was accepted as `v0.6.0` scope: persistent preview remains in scope, and `FEAT-0009` through `FEAT-0015` add runtime input, state inspection, launch/session ergonomics, and focused screenshot assertions.
- `2026-06-10`: `v0.6.0` launch helper scope is explicitly bridge-enabled local app launch only; no-code attach, process injection, arbitrary process termination, and remote inspection remain out of scope.

## Change Log

- `2026-06-10`: Added `FEAT-0009` through `FEAT-0015` for post-`v0.5.0` runtime/debugging wishlist items and expanded the `v0.6.0` release plan to schedule those tickets alongside persistent preview lifecycle work.
- `2026-06-10`: Completed `v0.5.0` release with PreviewHost fidelity improvements, packaged sample smoke, GitHub Release assets, tag verification, and green CI; moved next focus to `R0.6.0-M1 Persistent PreviewHost Lifecycle Contract`.
- `2026-06-10`: Completed `R0.5.0-M1` through `R0.5.0-M5` with project/build-output preview metadata, diagnostic triage fields, resource/layout provenance defaults, CLI profile variants, getting-started sample variants, documentation, and targeted validation; moved active focus to `R0.5.0-M6 Release Candidate And Version Bump`.
- `2026-06-10`: Completed `v0.4.0` release with runtime bridge reliability, packaged CLI runtime smoke, GitHub Release assets, tag verification, and green CI; moved active focus to `R0.5.0-M1 Project Graph And Build Diagnostics`.
- `2026-06-09`: Planned the full release roadmap through `v1.0.0`, promoted `v0.4.0` as the current release target, and moved active focus to `R0.4.0-M1 Bridge Session Discovery And Cleanup`.
- `2026-06-09`: Revised the `v0.8.0` roadmap from protocol-only stabilization into the final pre-1.0 product feature release and moved protocol/integration stabilization under `v0.9.0` beta hardening.
- `2026-06-09`: Completed `v0.3.0` release with GitHub Release assets and tag verification, stabilized the follow-up master CI watcher timing flake, and moved active focus to next release planning.
- `2026-06-09`: Completed `R0.3.0-M1` through `R0.3.0-M5` by adding animation sampling protocol DTOs, PreviewHost time-offset capture, Core frame strip/motion/viewer export, CLI `preview-animation`, MCP `preview_axaml_animation`, getting-started animation sample/profile, documentation, and targeted validation; moved active focus to `R0.3.0-M6`.
- `2026-06-09`: Planned `v0.3.0` with release goals and milestones for animation sampling, PreviewHost time-offset capture, motion diagnostics, CLI/MCP/viewer workflow, sample documentation, and guarded release validation; moved active focus to `R0.3.0-M1`.
- `2026-06-09`: Stored `FEAT-0008` for future animation diagnostics; no implementation was started and next release planning remains the active focus.
- `2026-06-09`: Completed `v0.2.2` release with GitHub Release assets and tag verification; moved active focus to next release planning.
- `2026-06-09`: Implemented BUG-0003 PreviewHost diagnostics fixes for DataTemplate binding scope, Fluent/template layout overlap noise, text metric tolerance, and slider internal hit-target warnings; moved active focus to `R0.2.2-M3`.
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
- `2026-06-08`: Completed W8 by fixing the first pushed CI/Release failures in GitHub Release creation and CLI fake bridge pipe smoke coverage.
- `2026-06-08`: Started W9 for implementation of all stored feature tickets after explicit user authorization.
- `2026-06-08`: Completed W9 by implementing stored feature tickets FEAT-0001 through FEAT-0007 with preview diagnostics, layout warnings, computed inspection, multi-size preview, screenshot diff, scoped cleanup, documentation updates, and full-suite validation.
- `2026-06-08`: Completed W13 by adding source-backed `x:DataType` binding diagnostics in PreviewHost, typed-binding smoke coverage, README/validation/gap updates, and full-suite validation.
- `2026-06-08`: Completed W14 by adding target-aware TextBox `key_text` input, selection replacement, read-only rejection, bridge/CLI tests, README/gap updates, and full-suite validation.
- `2026-06-08`: Completed W15 by adding App.Initialize-created `Application.DataContext` fallback preview startup parity, PreviewHost smoke coverage, README/gap updates, and full-suite validation.
- `2026-06-08`: Completed W16 by adding opt-in self-contained executable ZIP support, executable package kind manifest coverage, packaging cleanup process-lock detection, release script dry-runs, README/validation/gap updates, and full release validation.
- `2026-06-09`: Completed W17 plan refresh with W17-W25 milestone definitions, active W18 focus, stale post-W16 next-action cleanup, gap-audit alignment, validation, commit, and push.
- `2026-06-09`: Completed W18 by adding `avascope doctor`, doctor protocol DTOs, CLI/protocol smoke coverage, packaged release-script doctor smoke with isolated manifest/store paths, README/validation/gap updates, and release validation.
- `2026-06-09`: Completed W19 by adding project-local preview profiles for `preview` and `create-preview-session`, a getting-started sample profile, CLI profile tests, README/sample/validation/gap updates, and sample profile smoke validation.
- `2026-06-09`: Completed W20 by adding `docs/AGENT_WORKFLOW.md`, linking it from README and sample docs, documenting packaged CLI doctor/preview/runtime/diff/baseline workflows, and validating packaged doctor plus profile preview commands.
- `2026-06-09`: Completed W21 by adding runtime `clear_text` input for focused or targeted writable `TextBox` controls, bridge/CLI tests, README/workflow documentation, and targeted validation; moved active focus to W22.
- `2026-06-09`: Completed W22 by adding bounded `diagnosticIssues` provenance to diagnostics responses, Core derivation from bridge/preview-host/preview-session records, protocol/Core/MCP/CLI tests, README/validation/gap updates, and targeted validation; moved active focus to W23.
- `2026-06-09`: Completed W23 by adding unchanged-input skip events to preview-session watching, preserving isolated one-shot PreviewHost rendering, adding protocol/Core/CLI coverage, updating docs/gap tracking, and targeted validation; moved active focus to W24.
- `2026-06-09`: Completed W24 by adding optional baseline-check JSON report output, report path propagation, protocol/CLI tests, sample report smoke validation, README/validation/gap updates, and targeted validation; moved active focus to W25.
- `2026-06-09`: Completed W25 by refreshing the public-alpha audit, running full Debug and Release validation, validating GitHub Release dry-run assets, running packaged CLI doctor/MCP/baseline smokes, updating docs/gap tracking, and confirming generated artifacts remain ignored.
- `2026-06-09`: Adopted release-based development for `v0.2.0`, added the release plan and release commit guard, updated release documentation, and moved active focus to `R0.2.0-M1 Runtime Workflow Hardening`.
- `2026-06-09`: Cleaned project positioning references so docs present AvaScope as a standalone Avalonia project without external-product framing.
- `2026-06-09`: Defined formal `v0.2.0` release goals with success signals and mapped them to R0.2.0-M1 through R0.2.0-M6.
- `2026-06-09`: Added Codex preview surface to the `v0.2.0` release goals and inserted `R0.2.0-M5 Codex Preview Surface` before release-candidate validation.
- `2026-06-09`: Completed R0.2.0-M5 by adding file-backed preview viewer export, CLI `preview-viewer`, MCP `preview_viewer`, Codex in-app browser docs, targeted validation, and full test-suite validation.
- `2026-06-09`: Completed R0.2.0-M1 by adding runtime `target` context to tree/find/inspect/input/screenshot responses, actionable stale target error details, docs, and full validation.
- `2026-06-09`: Completed R0.2.0-M2 by adding preview readiness/build/render failure triage, bounded error details, CLI/Core/PreviewHost tests, docs, and full validation.
- `2026-06-09`: Completed R0.2.0-M3 by adding tool-visible live preview lifecycle status, documenting persistent-host deferral semantics, adding protocol/Core/CLI coverage, and full validation.
- `2026-06-09`: Completed R0.2.1-M1 by making PreviewHost wrapper `Window` backgrounds theme-aware, preserving app `Window` background styles, adding dark preview smoke coverage, and validating the `SettingsView.axaml` dark preview scenario.
- `2026-06-09`: Completed R0.2.1-M2 by validating the release gate, committing `Release 0.2.1`, pushing it to `master`, and confirming GitHub Release `v0.2.1` published with packages, executable ZIPs, and release manifest.
- `2026-06-09`: Stabilized post-release CI by retrying the watch smoke test file rewrite when Windows briefly locks the watched AXAML file and by making the MCP bridge attach smoke target its own session id explicitly.
- `2026-06-09`: Stored `BUG-0003` for preview diagnostic false positives involving DataTemplate binding context, template/layer overlap warnings, text clipping tolerance, and internal slider hit-target noise; no implementation was started.
- `2026-06-09`: Stabilized the watch preview smoke after BUG-0003 intake by increasing the test settle window so Windows file watcher events do not trigger a reload while the changed AXAML file is still locked.
