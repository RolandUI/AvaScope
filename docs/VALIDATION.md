# AvaScope Validation

For documentation-only policy or prose changes, review the diff for correctness and consistency, check affected relative links, scan outgoing content for machine-specific information, and run `git diff --check`. Record that validation without rebuilding the product or dispatching a full CI gate. Executable examples, generated contracts, scripts or product behavior changes still require their relevant tests. The [GitHub information policy](../AGENTS.md#github-information-policy) applies to all published validation summaries and evidence.

## Choose Validation By Risk

Development commits and pushes go directly to `master`, without task branches, worktrees or pull requests. Choose the smallest set of checks that establishes the changed behavior and protects its affected dependents. The commands later in this document are a scope-specific reference, not a checklist to run in full for every change.

| Level | When required | Scope |
| --- | --- | --- |
| Local | Default for changes that can be adequately verified locally | Build affected components; run the relevant regression/contract tests and affected public CLI/MCP or native journey. Documentation-only edits use the checks above. |
| Targeted CI | Required acceptance evidence depends on a platform or environment unavailable locally | Run only the relevant platform and test/job group. A Windows-only result does not establish macOS/Linux or native display coverage. |
| Full CI | Before release, or when cross-cutting risk cannot be covered by narrower checks | Validate the required platform matrix and integration boundaries; for release, verify the exact candidate's packaged CLI/MCP/provider/installers and publication inputs. |

Assess shared protocol/Core behavior, transport and process lifecycle, security boundaries, Avalonia/.NET/dependency upgrades, and build or packaging changes for wider impact. These are reasons to examine dependencies and expand coverage where needed, not automatic full-CI triggers for every edit in those areas. Account for the accumulated relevant changes since the last applicable validation, not just the latest commit. If the impact cannot be bounded confidently, broaden validation before claiming acceptance.

A completed commit, issue or batch, elapsed time, or a desire for a fresh green badge does not justify a full gate. Full solution build/test, all-platform QA, installers and release packaging are not mandatory for each development slice. Specific issue acceptance criteria remain binding; keep missing evidence pending instead of calling it passed.

## Local Development Loop

Reproduce a defect through the affected public behavior where applicable, add a focused regression, implement the smallest correction, then build and test the affected components and dependents. Verify the actual application journey when unit tests cannot establish the behavior, especially for native or visual changes. Inspect the diff and run `git diff --check`, then commit and push the coherent, locally validated change to `master`. Resolve failures attributable to the change before push; report unrelated failures accurately.

Reuse the current QA binaries and build outputs. Restore when dependencies or restore inputs change or assets are missing; rebuild before using `--no-build` after relevant source/test/configuration changes. Do not reuse an old binary merely because a filtered test passed. Reuse a successful result only while its relevant source, tests, dependencies, configuration and environment remain applicable. Keep provenance compact and local under the existing QA policy.

For a required full local solution check, run from the repository root:

```powershell
dotnet restore AvaScope.slnx
dotnet build AvaScope.slnx
dotnet test AvaScope.slnx
git status --short
```

## CI And Release Decisions

CI is deliberately dispatched from `master` to answer an identified validation gap. Briefly identify the selected commit, required platforms/tests and reason in the existing issue or chat context; no extra start/status comment is required. Verify the run's source revision. Independent local work can continue while it runs, but its result does not cover later commits automatically.

Prefer the existing targeted native-input workflow when it covers the required case. Selective platform/job controls for the main CI remain pending; do not claim an unavailable narrow mode was run. If only the current broad workflow can supply required evidence, its use needs the same concrete risk justification. Diagnose failures and rerun the affected checks/jobs first; rerun the complete gate only when the fix invalidates broader coverage or the remaining risk requires it.

Commit/push, issue closure and release publication are separate decisions. Close an issue after its specific acceptance criteria pass; full CI is not a universal closure requirement. Keep an issue in review when required validation is pending, or blocked for an external dependency. Release publication requires complete applicable validation for the exact candidate and its artifacts, plus publication authorization. A Windows Release workflow alone does not establish the full native platform matrix.

The target workflow design uses explicit dispatch, selectable platform/job groups and build reuse within each compatible platform/configuration. Coordinate CI and Release validation so identical checks are not repeated without cause; verify the actual release artifacts even when source-level results can be reused. Parallelize independent jobs to reduce waiting, and omit unnecessary work to reduce cost. Workflow optimization is tracked in [#234](https://github.com/RolandUI/AvaScope/issues/234).

## Validation Reference

Record each necessary validation result once in its GitHub issue. This document contains repeatable procedures, not execution reports.

Run build and test commands sequentially. Parallel build/test invocations can contend for the same `bin/` and `obj/` outputs.

The [native agent QA lab](AGENT_QA_LAB.md) complements these tests with actual agent-operated desktop tasks and independent native image review. `pwsh -File eng/test-agent-qa-lab.ps1 -SkipBuild -TestExpiry` checks its two-host lifecycle and failure-evidence plumbing on an available desktop; that scripted check is not an exploratory test result. CI records the observed native backend/scale separately on Windows, X11 and macOS.

Headless asynchronous test bodies must use `BridgeHeadlessSmokeTests.DispatchAsync`
or the explicit `HeadlessUnitTestSession.Dispatch<T>(Func<Task<T>>, CancellationToken)`
overload. Avalonia has no `Func<Task>` overload: `Dispatch(async () => { ... })`
without a return value can select the synchronous generic overload and produce
`Task<Task>`, leaving assertions after the first suspension unobserved. The
`TestSessionPropagatesAnExceptionAfterAnAsynchronousBoundary` regression verifies
that the shared helper propagates such failures. A green run with unobserved test
bodies is not release evidence.

The test process and its CLI/MCP children use isolated recovery and bridge session
registries under the test temporary directory. Explicit `AVASCOPE_RUN_STORE_DIR`
and `AVASCOPE_BRIDGE_MANIFEST_DIR` overrides are honored. Validation must not depend on or add records to a developer's normal run
store; accumulated real records can otherwise introduce path conflicts and lock
contention unrelated to the fixture.

For the standalone-provider foundation (#117/#119/#121), batch the bootstrap/provider regressions with the real external-host gate:

```powershell
dotnet test tests/AvaScope.Tests/AvaScope.Tests.csproj -c Release --filter "FullyQualifiedName~Bridge|FullyQualifiedName~ProviderVerifierTests"
pwsh -File eng/test-standalone-provider.ps1
pwsh -File eng/test-standalone-provider.ps1 -Native -SkipBuild
```

The gate records the actual headless/native mode and exercises CLI plus MCP stdio against the no-PackageReference sample, isolated enabled/disabled/incompatible builds, window lifecycle, screenshot, malformed/missing/tampered providers, pins, normal shutdown and remote transport cleanup. On Linux, run the native lane inside an owned X11 display. Hosted Windows/Linux/macOS evidence is required before closing cross-platform acceptance. See [STANDALONE_PROVIDER.md](STANDALONE_PROVIDER.md) for artifact pinning and compatibility boundaries.

For project guidance, integration verification and shared profiles (#118/#120/#122), batch `BridgeIntegration*`, `AgentTestProfileTests`, `RuntimeScenarioLifecycleTests` and `StableSurface*` tests, then run `pwsh -File eng/test-integration-onboarding.ps1` (or `-Native -SkipBuild` after packaging). The gate applies the returned file-specific snippets only to disposable clean fixtures, packs local bridge dependencies into an isolated NuGet feed/cache, builds both package/standalone integration modes with the flag enabled and disabled, repeats analysis to reject duplicate guidance, and verifies all four outputs through CLI and MCP. It also runs the same named profile through both adapters for each integration mode and checks actual host log redaction. Its eight lifecycle reports, four profile reports and final `validation.json` remain under `artifacts/onboarding-validation`. These local fixture packages never overwrite the user's NuGet cache or publish a package. Native platform CI runs this gate alongside the standalone-provider gate.

For controlled native Wayland (#155), Linux Ubuntu 24.04 CI installs Weston 13,
wayland-utils, xkb-data and Mesa EGL. Batch `WaylandTestEnvironmentTests` with
X11/profile/run-recovery tests, then run `eng/test-managed-wayland.ps1` after the
Release solution build and provider packaging. The gate checks real CLI/MCP
text/capture at 1x/2x, wrong-backend and dependency failures, and cleanup. Native
Wayland is separate from X11/XWayland and does not imply native input coverage.
See [controlled Wayland](MANAGED_WAYLAND.md).

For protocol-only work, also run:

```powershell
dotnet test AvaScope.slnx --filter Protocol
```

For protocol capability/versioning work, include:

```powershell
dotnet test AvaScope.slnx --no-build --filter "FullyQualifiedName~ProtocolContractTests.CapabilitiesResponseSerializesStableDiscoveryShape|FullyQualifiedName~CapabilityCompatibilityCheckerTests|FullyQualifiedName~CliSmokeTests.CapabilitiesCommandReportsProtocolAndToolCapabilities|FullyQualifiedName~CliSmokeTests.CapabilitiesCommandRejectsUnsupportedRequiredCapability|FullyQualifiedName~AvaScopeMcpToolsTests.Capabilities|FullyQualifiedName~McpStdioSmokeTests.ServerStartsOverStdioAndListsInitialTools"
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

For runtime input work, include the bridge path and CLI adapter path:

```powershell
dotnet test AvaScope.slnx --filter Bridge
dotnet test AvaScope.slnx --filter FullyQualifiedName~Cli
```

For preview host work, also run:

```powershell
dotnet test AvaScope.slnx --filter FullyQualifiedName~PreviewHost
```

For source-backed preview diagnostics work, include the typed-binding smoke path:

```powershell
dotnet test AvaScope.slnx --filter FullyQualifiedName~PreviewHostSmokeTests.PreviewHostReturnsDataTypeBindingPathDiagnostics
```

For preview failure triage/readiness work, include:

```powershell
dotnet test AvaScope.slnx --filter FullyQualifiedName~PreviewHostClientTests
dotnet test AvaScope.slnx --filter FullyQualifiedName~PreviewHostSmokeTests
dotnet test AvaScope.slnx --filter "FullyQualifiedName~CliSmokeTests.PreviewCommandPreservesPreviewReadinessFailureDetails|FullyQualifiedName~CliSmokeTests.PreviewCommandPreservesPreviewFailureDetails"
```

For CLI work, also run:

```powershell
dotnet test AvaScope.slnx --filter FullyQualifiedName~Cli
```

For installer and CLI discovery work, include:

```powershell
dotnet test AvaScope.slnx --filter FullyQualifiedName~InstallerWorkflowTests
powershell -NoProfile -ExecutionPolicy Bypass -File .\eng\create-local-release.ps1 -RuntimeIdentifiers win-x64 -SkipTests -SkipSampleSmoke
$env:AVASCOPE_INSTALLER_ARTIFACT = ".\artifacts\executables\AvaScopeSetup.exe"
dotnet test AvaScope.slnx -c Release --no-build --filter FullyQualifiedName~PackagedInstallerSupportsInstallRepairDoctorMcpAndUninstall
```

On Linux, package `linux-x64`, then run `bash ./eng/test-linux-installer.sh ./artifacts/executables/avascope-linux-x64-installer <version>` and the same artifact-backed .NET test with `AVASCOPE_INSTALLER_ARTIFACT` set to that path. These checks cover embedded payload/legal verification, clean install, `--version`, `doctor`, MCP stdio startup, repair/upgrade replacement, unsafe-uninstall rejection, and uninstall.

For CLI doctor/self-test work, include:

```powershell
dotnet test AvaScope.slnx --filter FullyQualifiedName~CliSmokeTests.DoctorCommandReportsLocalReadiness
dotnet test AvaScope.slnx --filter FullyQualifiedName~ProtocolContractTests.DoctorResponseSerializesStableReadinessShape
```

For product version discovery surface work, include:

```powershell
dotnet test AvaScope.slnx --no-build --filter "FullyQualifiedName~CliSmokeTests.VersionCommandReportsProductVersion|FullyQualifiedName~CliSmokeTests.CapabilitiesCommandReportsProtocolAndToolCapabilities|FullyQualifiedName~CliSmokeTests.DoctorCommandReportsLocalReadiness|FullyQualifiedName~ProtocolContractTests.HealthResponseUsesCurrentProtocolMetadata|FullyQualifiedName~ProtocolContractTests.CapabilitiesResponseSerializesStableDiscoveryShape|FullyQualifiedName~ProtocolContractTests.DoctorResponseSerializesStableReadinessShape|FullyQualifiedName~AvaScopeMcpToolsTests.HealthReturnsCurrentProtocolMetadata|FullyQualifiedName~AvaScopeMcpToolsTests.CapabilitiesReturnsCurrentCapabilityManifest|FullyQualifiedName~McpStdioSmokeTests.ServerStartsOverStdioAndListsInitialTools"
```

For diagnostics shape work, include:

```powershell
dotnet test AvaScope.slnx --filter Protocol
dotnet test AvaScope.slnx --filter Core
dotnet test AvaScope.slnx --filter Mcp
dotnet test AvaScope.slnx --filter FullyQualifiedName~Cli
```

For diagnostics and artifact run-index ergonomics work, include:

```powershell
dotnet test AvaScope.slnx --no-build --filter "FullyQualifiedName~ArtifactRunIndexStoreTests|FullyQualifiedName~ProtocolContractTests|FullyQualifiedName~LocalBridgeClientTests.Diagnostics|FullyQualifiedName~CliSmokeTests.PreviewCommandRendersAxamlThroughPreviewHostClient|FullyQualifiedName~AvaScopeMcpToolsTests.CapabilitiesReturnsCurrentCapabilityManifest"
dotnet test AvaScope.slnx --no-build --filter FullyQualifiedName~StableSurfaceContractTests
```

For runtime target handoff work, include:

```powershell
dotnet test AvaScope.slnx --filter FullyQualifiedName~ProtocolContractTests
dotnet test AvaScope.slnx --filter FullyQualifiedName~BridgeHeadlessSmokeTests.McpToolsListTopLevelsAndCaptureScreenshotThroughLocalBridgePipe
dotnet test AvaScope.slnx --filter FullyQualifiedName~BridgeHeadlessSmokeTests.McpInputClicksButtonAndTypesTextThroughLocalBridgePipe
dotnet test AvaScope.slnx --filter "FullyQualifiedName~CliSmokeTests.TreeCommandReadsTreeThroughBridgePipe|FullyQualifiedName~CliSmokeTests.FindNodesCommandReadsMatchesThroughBridgePipe|FullyQualifiedName~CliSmokeTests.InputCommandSendsClickThroughBridgePipe"
```

For runtime bridge reliability release work, include:

```powershell
dotnet test AvaScope.slnx --filter "FullyQualifiedName~LocalBridgeClientTests|FullyQualifiedName~ProtocolContractTests|FullyQualifiedName~CliSmokeTests.AttachCommandSelectsManifestPathAndProcessName|FullyQualifiedName~CliSmokeTests.ListTopLevelsCommandUsesCustomManifestDirectory|FullyQualifiedName~CliSmokeTests.CleanupBridgeSessionsCommandDeletesStaleAndInvalidCustomManifestRecords|FullyQualifiedName~AvaScopeMcpToolsTests.CleanupBridgeSessionsDeletesStaleManifestFromSelectedDirectory|FullyQualifiedName~AvaScopeMcpBridgeToolsTests.AttachToAppUsesProcessNameAndManifestDirectory|FullyQualifiedName~McpStdioSmokeTests.ServerStartsOverStdioAndListsInitialTools|FullyQualifiedName~BridgeHeadlessSmokeTests.McpInputClicksButtonAndTypesTextThroughLocalBridgePipe"
```

For `v0.6.0` runtime input/state, session ergonomics, launch helper, screenshot region assertion, and preview-session lifecycle-event work, include:

```powershell
dotnet test AvaScope.slnx --no-build --filter "FullyQualifiedName~ProtocolContractTests.InspectNodeResponseSerializesRuntimeStateShape|FullyQualifiedName~ProtocolContractTests.ScreenshotRegionAssertionResponseSerializesStableShape|FullyQualifiedName~ScreenshotRegionAsserterTests|FullyQualifiedName~BridgeHeadlessSmokeTests.McpExpandedInputAndRuntimeStateInspectionUseBridgeOnly|FullyQualifiedName~CliSmokeTests.InputCommandSendsExpandedInputThroughBridgePipe|FullyQualifiedName~CliSmokeTests.AssertRegionCommandChecksNonEmptyRegion|FullyQualifiedName~CliSmokeTests.LaunchAppCommandReturnsStructuredErrorWhenNoBridgeSessionAppears|FullyQualifiedName~LocalBridgeClientTests.AttachLatestToAppSelectsNewestActiveMatchingManifest"
dotnet test AvaScope.slnx --no-build --filter "FullyQualifiedName~PreviewSessionRegistryTests.CreateAsyncRendersAndRegistersPreviewSession|FullyQualifiedName~PreviewSessionRegistryTests.ReloadAsyncRerendersExistingPreviewSession|FullyQualifiedName~ProtocolContractTests.PreviewSessionSummarySerializesRequestAndLastRender"
```

For CLI preview-session work, include the persistent-session smoke path:

```powershell
dotnet test AvaScope.slnx --filter FullyQualifiedName~CliSmokeTests.PreviewSessionCommandsCreateListReloadAndClosePersistedSession
dotnet test AvaScope.slnx --filter FullyQualifiedName~CliSmokeTests.ReloadPreviewSessionCommandReturnsStructuredErrorWhenNoPreviewSessionMatches
dotnet test AvaScope.slnx --filter FullyQualifiedName~CliSmokeTests.WatchPreviewSessionCommandReloadsWhenWatchedFileChanges
```

For Codex preview-viewer handoff work, include:

```powershell
dotnet test AvaScope.slnx --filter FullyQualifiedName~ProtocolContractTests.PreviewViewerResponseSerializesStableShape
dotnet test AvaScope.slnx --filter FullyQualifiedName~PreviewSessionRegistryTests.PreviewViewerExporter
dotnet test AvaScope.slnx --filter FullyQualifiedName~CliSmokeTests.PreviewSessionCommandsCreateListReloadAndClosePersistedSession
dotnet test AvaScope.slnx --filter FullyQualifiedName~AvaScopeMcpToolsTests.PreviewViewerExportsFileBackedUrlForPreviewSession
dotnet test AvaScope.slnx --filter FullyQualifiedName~McpStdioSmokeTests.ServerStartsOverStdioAndListsInitialTools
```

For animation preview work, include:

```powershell
dotnet test AvaScope.slnx --filter FullyQualifiedName~ProtocolContractTests.PreviewAnimationRequestAndResponseSerializeStableShapes
dotnet test AvaScope.slnx --filter FullyQualifiedName~PreviewHostClientTests.RenderAnimationAsyncCreatesOffsetFramesStripAndMotionSummary
dotnet test AvaScope.slnx --filter FullyQualifiedName~CliSmokeTests.PreviewAnimationCommandRendersOffsetFramesAndStrip
dotnet test AvaScope.slnx --filter "FullyQualifiedName~McpStdioSmokeTests.ServerStartsOverStdioAndListsInitialTools|FullyQualifiedName~AvaScopeMcpToolsTests.PreviewAxamlAnimationRejectsInvalidOffsets"
dotnet .\src\AvaScope.Cli\bin\Debug\net10.0\avascope.dll preview-animation .\samples\AvaScope.GettingStartedApp\AvaScope.GettingStartedApp.csproj --profile animation
```

For live preview watcher changes, include:

```powershell
dotnet test AvaScope.slnx --filter FullyQualifiedName~ProtocolContractTests.PreviewWatchResponseSerializesEvents
dotnet test AvaScope.slnx --filter FullyQualifiedName~PreviewSessionRegistryTests
dotnet test AvaScope.slnx --filter FullyQualifiedName~PreviewHost
dotnet test AvaScope.slnx --filter FullyQualifiedName~CliSmokeTests.WatchPreviewSessionCommandReloadsWhenWatchedFileChanges
```

For live preview lifecycle decision work, also confirm the watch response lifecycle shape:

```powershell
dotnet test AvaScope.slnx --filter FullyQualifiedName~ProtocolContractTests.PreviewWatchResponseSerializesEventsAndLatestSession
dotnet test AvaScope.slnx --filter FullyQualifiedName~PreviewSessionRegistryTests.PreviewSessionWatcher
dotnet test AvaScope.slnx --filter FullyQualifiedName~CliSmokeTests.WatchPreviewSessionCommandReloadsWhenWatchedFileChanges
```

For CLI preview profile work, include:

```powershell
dotnet test AvaScope.slnx --filter FullyQualifiedName~CliSmokeTests.PreviewCommandUsesProjectPreviewProfileAndAllowsExplicitOverrides
dotnet test AvaScope.slnx --filter FullyQualifiedName~CliSmokeTests.PreviewCommandUsesProjectPreviewProfileVariantAndAllowsExplicitOverrides
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

For `v0.7.0` runtime mutation evidence work, include:

```powershell
dotnet test AvaScope.slnx --no-build --filter "FullyQualifiedName~ProtocolContractTests.RuntimeMutationEvidenceResponseSerializesStableShape|FullyQualifiedName~LocalBridgeClientTests.RuntimeMutationEvidenceRunnerCapturesSequencedArtifactsThroughBridgePipe|FullyQualifiedName~CliSmokeTests.MutateNodeEvidenceCommandCapturesSequencedArtifactsThroughBridgePipe|FullyQualifiedName~BridgeHeadlessSmokeTests.McpMutateNodeEvidenceCapturesScreenshotsTreesAndDiffThroughLocalBridgePipe|FullyQualifiedName~McpStdioSmokeTests.ServerStartsOverStdioAndListsInitialTools"
```

For `v0.7.0` runtime mutation safety and reset semantics work, include:

```powershell
dotnet test AvaScope.slnx --no-build --filter "FullyQualifiedName~ProtocolContractTests.RuntimeMutationRequestAndResponseSerializeStableShapes|FullyQualifiedName~LocalBridgeClientTests.MutateNodeSendsStructuredMutationThroughBridgePipe|FullyQualifiedName~CliSmokeTests.MutateNodeCommandSendsResetMutationThroughBridgePipe|FullyQualifiedName~BridgeHeadlessSmokeTests.RuntimeMutationDeactivateResetsActiveMutationsAndRejectsFurtherMutation|FullyQualifiedName~BridgeHeadlessSmokeTests.RuntimeMutationTopLevelRegistrationDisposeResetsScopedMutations|FullyQualifiedName~BridgeHeadlessSmokeTests.McpMutateNodeReturnsBoundedMutationContractResultsThroughLocalBridgePipe|FullyQualifiedName~BridgeHeadlessSmokeTests.RuntimeMutationAppliesClassesResourcesTextAndScreenshotObservableBackgroundThenResetAll"
```

For security, safety, and compatibility audit work, include:

```powershell
dotnet test AvaScope.slnx --no-build --filter "FullyQualifiedName~SecurityThreatModelDocumentationTests|FullyQualifiedName~AvaScopeBridgeTests.BridgeIsInactiveByDefault|FullyQualifiedName~AvaScopeBridgeTests.ActivateCreatesLocalOnlyRuntimeSession|FullyQualifiedName~ProtocolContractTests.BridgeSessionManifestRejectsUnsupportedTransportScope|FullyQualifiedName~LocalBridgeClientTests.MutateNodeRejectsSessionMismatchWithoutIpc|FullyQualifiedName~LocalBridgeClientTests.DiagnosticsReportsInvalidAndStaleManifestsWithoutThrowing|FullyQualifiedName~CapabilityCompatibilityCheckerTests"
```

For performance, stress, samples, and troubleshooting audit work, include:

```powershell
dotnet test AvaScope.slnx --no-build --filter "FullyQualifiedName~PerformanceStressAuditTests|FullyQualifiedName~BridgeHeadlessSmokeTests.RuntimeMutationRepeatedSetPropertyAndResetAllKeepsReviewBounded|FullyQualifiedName~PerformanceStressAuditDocumentationTests"
```

Use [performance budgets and focused stress checks](PERFORMANCE_STRESS_AUDIT.md) and [failure triage](TROUBLESHOOTING.md). Update those references only when the contract or procedure changes; record observed run results in the relevant issue.

For `v0.7.0` runtime experiment review work, include:

```powershell
dotnet test AvaScope.slnx --no-build --filter "FullyQualifiedName~ProtocolContractTests.RuntimeMutationReviewResponseSerializesStableShape|FullyQualifiedName~ProtocolContractTests.RuntimeMutationEvidenceResponseSerializesStableShape|FullyQualifiedName~LocalBridgeClientTests.MutationReviewReadsBoundedHistoryThroughBridgePipe|FullyQualifiedName~LocalBridgeClientTests.RuntimeMutationEvidenceRunnerCapturesSequencedArtifactsThroughBridgePipe|FullyQualifiedName~CliSmokeTests.MutationReviewCommandReadsHistoryAndWritesArtifactThroughBridgePipe|FullyQualifiedName~CliSmokeTests.MutateNodeEvidenceCommandCapturesSequencedArtifactsThroughBridgePipe|FullyQualifiedName~BridgeHeadlessSmokeTests.McpMutateNodeReturnsBoundedMutationContractResultsThroughLocalBridgePipe|FullyQualifiedName~BridgeHeadlessSmokeTests.McpMutateNodeEvidenceCapturesScreenshotsTreesAndDiffThroughLocalBridgePipe|FullyQualifiedName~McpStdioSmokeTests.ServerStartsOverStdioAndListsInitialTools"
```

For runtime pseudo-state matrix work, include:

```powershell
dotnet test AvaScope.slnx --no-build --filter "FullyQualifiedName~ProtocolContractTests.RuntimePseudoStateMatrixRequestAndResponseSerializeStableShapes|FullyQualifiedName~RuntimePseudoStateMatrixRunnerTests|FullyQualifiedName~CliSmokeTests.PseudoStateMatrixCommandCapturesContactSheetThroughBridgePipe|FullyQualifiedName~BridgeHeadlessSmokeTests.PseudoStateMatrixCapturesCommonStatesAndResetsRuntimeForcing|FullyQualifiedName~McpStdioSmokeTests.ServerStartsOverStdioAndListsInitialTools|FullyQualifiedName~StableSurfaceContractTests"
```

For interaction-triggered runtime animation recording work, include:

```powershell
dotnet test AvaScope.slnx --no-build --filter "FullyQualifiedName~ProtocolContractTests.RuntimeInteractionAnimationRequestAndResponseSerializeStableShapes|FullyQualifiedName~RuntimeInteractionAnimationRunnerTests|FullyQualifiedName~CliSmokeTests.RecordInteractionAnimationCommandCapturesFrameStripAndAssertionsThroughBridgePipe|FullyQualifiedName~CliSmokeTests.CapabilitiesCommandReportsProtocolAndToolCapabilities|FullyQualifiedName~AvaScopeMcpToolsTests.CapabilitiesReturnsCurrentCapabilityManifest|FullyQualifiedName~McpStdioSmokeTests.ServerStartsOverStdioAndListsInitialTools|FullyQualifiedName~StableSurfaceContractTests"
```

For semantic screenshot comparison work, include:

```powershell
dotnet test AvaScope.slnx --no-build --filter "FullyQualifiedName~ProtocolContractTests.SemanticScreenshotComparisonRequestAndResponseSerializeStableShapes|FullyQualifiedName~SemanticScreenshotComparerTests|FullyQualifiedName~CliSmokeTests.SemanticDiffCommandWritesAnnotatedArtifactsAndBoundedFindings|FullyQualifiedName~CliSmokeTests.CapabilitiesCommandReportsProtocolAndToolCapabilities|FullyQualifiedName~AvaScopeMcpToolsTests.SemanticDiffWritesAnnotatedArtifactsAndFindings|FullyQualifiedName~AvaScopeMcpToolsTests.CapabilitiesReturnsCurrentCapabilityManifest|FullyQualifiedName~McpStdioSmokeTests.ServerStartsOverStdioAndListsInitialTools|FullyQualifiedName~StableSurfaceContractTests"
```

For `v0.9.0` source-aware runtime suggestion work, include:

```powershell
dotnet test AvaScope.slnx --no-build --filter "FullyQualifiedName~ProtocolContractTests.RuntimeMutationReviewResponseSerializesStableShape|FullyQualifiedName~RuntimeSourceSuggestionBuilderTests|FullyQualifiedName~CliSmokeTests.MutationReviewCommandReadsHistoryAndWritesArtifactThroughBridgePipe|FullyQualifiedName~BridgeHeadlessSmokeTests.McpMutateNodeReturnsBoundedMutationContractResultsThroughLocalBridgePipe|FullyQualifiedName~McpStdioSmokeTests.ServerStartsOverStdioAndListsInitialTools"
```

For `v0.9.0` accessibility, validation, and component inventory work, include:

```powershell
dotnet test AvaScope.slnx --no-build --filter "FullyQualifiedName~ProtocolContractTests.TreeResponseSerializesBoundedNodeShape|FullyQualifiedName~ProtocolContractTests.UiAuditResponseSerializesStableShape|FullyQualifiedName~UiAuditBuilderTests|FullyQualifiedName~CliSmokeTests.AuditUiCommandBuildsBoundedReportFromVisualTreeThroughBridgePipe|FullyQualifiedName~AvaScopeMcpToolsTests.AuditUiRejectsEmptySessionId|FullyQualifiedName~BridgeHeadlessSmokeTests.McpToolsListTopLevelsAndCaptureScreenshotThroughLocalBridgePipe|FullyQualifiedName~McpStdioSmokeTests.ServerStartsOverStdioAndListsInitialTools"
```

For task-scoped design-quality audit work, include:

```powershell
dotnet test AvaScope.slnx --no-build --filter "FullyQualifiedName~ProtocolContractTests.DesignQualityAuditRequestAndResponseSerializeStableShapes|FullyQualifiedName~DesignQualityAuditBuilderTests|FullyQualifiedName~CliSmokeTests.DesignAuditCommandBuildsScopedReportFromVisualTreeThroughBridgePipe|FullyQualifiedName~AvaScopeMcpToolsTests.CapabilitiesReturnsCurrentCapabilityManifest|FullyQualifiedName~McpStdioSmokeTests.ServerStartsOverStdioAndListsInitialTools|FullyQualifiedName~StableSurfaceContractTests"
```

For visual regression workflow work, include:

```powershell
dotnet test AvaScope.slnx --filter FullyQualifiedName~CliSmokeTests.BaselineCommandsCreateManifestPassCheckAndFailChangedCheck
dotnet test AvaScope.slnx --filter FullyQualifiedName~CliSmokeTests.BaselineSuiteCommandCreatesManifestAndCheckPasses
dotnet test AvaScope.slnx --filter FullyQualifiedName~PreviewImageDifferTests
dotnet test AvaScope.slnx --filter FullyQualifiedName~PreviewBaselineManagerTests
dotnet test AvaScope.slnx --filter FullyQualifiedName~PreviewBaselineReportPackExporterTests
dotnet test AvaScope.slnx --filter FullyQualifiedName~ProtocolContractTests.PreviewBaselineResponsesSerializeStableShapes
dotnet test AvaScope.slnx --filter FullyQualifiedName~ProtocolContractTests.PreviewBaselineSuiteManifestSerializesStableShape
dotnet test AvaScope.slnx --filter FullyQualifiedName~ProtocolContractTests.PreviewComparisonRulesAndRegionResultsSerializeStableShape
dotnet test AvaScope.slnx --filter FullyQualifiedName~AvaScopeMcpToolsTests.BaselineCheckWritesReportAndReportPackPathsThroughPreviewHost
dotnet test AvaScope.slnx --filter FullyQualifiedName~McpStdioSmokeTests.ServerStartsOverStdioAndListsInitialTools
```

For CI report validation, run a sample baseline check with `--report <report.json> --report-pack <dir>`, verify the JSON report exists and contains the same `passed` and `entries` shape as stdout, then verify the report pack contains `baseline-report.json`, `baseline-report.html`, `baseline-junit.xml`, and `baseline.sarif.json`. The CLI response should include `agentReview` bounded failure/report/artifact handoff plus `reportPack.status`, pass/fail counts, metadata, and asset paths without inlining image payloads.

For the packaged lifecycle gate, run `pwsh -NoProfile -File ./eng/test-packaged-lifecycle.ps1 -CliAssembly <framework-dependent-package>/avascope.dll -Configuration Release` on Windows, Linux, and macOS. The gate must complete explicit build, direct project launch, bridge readiness and attach, top-level discovery, workflow execution, local evidence, and exact owned-process cleanup while proving launch environment values are absent from normal JSON output.

For the v1.4 complex workflow gate, run the source and packaged CLI/MCP surfaces. Each invocation performs at least two successful runs with alternating optional UI plus one intentional failure:

```powershell
pwsh -NoProfile -File ./eng/test-complex-workflow.ps1 -CliAssembly <cli>/avascope.dll -Surface Cli -Configuration Release
pwsh -NoProfile -File ./eng/test-complex-workflow.ps1 -CliAssembly <cli>/avascope.dll -Surface Mcp -McpAssembly <mcp>/AvaScope.Mcp.dll -McpScenarioClientAssembly ./tests/AvaScope.McpScenarioClient/bin/Release/net10.0/AvaScope.McpScenarioClient.dll -Configuration Release
```

The gate must prove two distinct aliases resolve without persisted runtime ids; provider and bounds-derived drag modes both execute without caller coordinates; the custom action, bounded retry, conditional branches, fragments, typed waits, automatic screenshots, and cross-window verification pass; JSON/Markdown/JUnit and the intentional failure screenshot are redacted; response-budget fallback JSON contains no configured secret; count retention removes only marked prior runs; the launched process is terminated; and unrelated files and the calling process remain. CI runs the source gate on Windows and the packaged gate on Windows, Linux, and macOS; the existing native macOS runtime gate remains responsible for Retina scaling coverage.

For the v1.4 runtime evidence privacy and action policy, run the focused policy and real Bridge evidence tests. They must cover inline/tree/JSON/Markdown/JUnit/audit redaction, explicit and control-derived screenshot masks, fail-closed removal, safe retention ownership, path traversal, action/gesture/custom-action gates, foreign session/PID authorization, and the unavailable network-upload boundary:

```powershell
dotnet test tests/AvaScope.Tests/AvaScope.Tests.csproj --filter "FullyQualifiedName~RuntimeEvidencePolicyEnforcerTests|FullyQualifiedName~RuntimeEvidencePolicyRedactsAndMasksWorkflowEvidenceEndToEnd"
dotnet test tests/AvaScope.Tests/AvaScope.Tests.csproj --filter "FullyQualifiedName~SecurityThreatModelDocumentationTests|FullyQualifiedName~StableSurfaceDocumentationTests"
```

For visual-regression GitHub Actions example work, include:

```powershell
dotnet test AvaScope.slnx --no-build --filter FullyQualifiedName~VisualRegressionWorkflowDocumentationTests
```

For legacy JSON-only artifact collection, run:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\eng\collect-baseline-artifacts.ps1 -Report <report.json> -OutDir .\artifacts\visual-regression\upload
```

For agent workflow documentation work, validate the packaged CLI examples that do not require a live runtime bridge:

```powershell
dotnet test AvaScope.slnx --no-build --filter FullyQualifiedName~DocumentationCompletionTests
powershell -NoProfile -ExecutionPolicy Bypass -File .\eng\create-local-release.ps1 -SkipTests
.\artifacts\executables\avascope-win-x64-framework-dependent\avascope.exe doctor --manifest-dir .\artifacts\samples\agent-workflow\sessions --preview-session-store .\artifacts\samples\agent-workflow\preview-sessions
.\artifacts\executables\avascope-win-x64-framework-dependent\avascope.exe preview .\samples\AvaScope.GettingStartedApp\AvaScope.GettingStartedApp.csproj --profile main --out .\artifacts\samples\agent-workflow\main-preview.png
```

## Stable Release Validation

For actual stable release readiness, run the complete candidate checks, including:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\eng\create-local-release.ps1
```

Release scope and acceptance live in the GitHub milestone and its `type:release` tracker. After required checks and publication authorization, set the tracker to `status:review`; stable publication requires all other milestone issues closed or explicitly moved. An explicitly authorized prerelease can retain documented acceptance gaps in the same numeric milestone; it must not promote Latest or close unresolved stable acceptance.

The version source is `Directory.Build.props`; the release commit subject must be `Release <version>`. The guard uses GitHub's open milestone issues, not a local status document. It requires exactly one release tracker in review, rejects incomplete stable acceptance and fails closed when GitHub cannot be verified. The GitHub CLI needs read access to issues. Read the [stable surface](STABLE_SURFACE.md) for package/protocol compatibility boundaries.

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\eng\validate-release-commit.ps1 -Version <version> -CommitSubject "Release <version>"
```

Run `eng/test-release-commit.ps1 -OutputDirectory <qa-root>` for isolated guard regression checks; it mocks GitHub responses and never publishes or changes issues. Version-specific release notes remain the source consumed by `eng/publish-github-release.ps1`, not a second release-status record.

`eng/create-local-release.ps1` wraps the release gate:

```powershell
dotnet restore AvaScope.slnx
dotnet build AvaScope.slnx -c Release
dotnet test AvaScope.slnx -c Release --no-build
dotnet pack .\src\AvaScope.Protocol\AvaScope.Protocol.csproj -c Release --no-build --output .\artifacts\packages
dotnet pack .\src\AvaScope.Core\AvaScope.Core.csproj -c Release --no-build --output .\artifacts\packages
dotnet pack .\src\AvaScope.Bridge\AvaScope.Bridge.csproj -c Release --no-build --output .\artifacts\packages
powershell -NoProfile -ExecutionPolicy Bypass -File .\eng\package-executables.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File .\eng\package-installers.ps1
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
powershell -NoProfile -ExecutionPolicy Bypass -File .\eng\publish-github-release.ps1 -Tag v1.0.0 -ExecutableRuntimeIdentifiers win-x64 -ExecutablePackageKind self-contained -DryRun
```

Current YAML has not yet been aligned with the explicit-dispatch policy: `CI` still declares a pull-request trigger alongside manual dispatch, and `Release` still reacts to `Directory.Build.props` pushes on `master`/`main`. Do not create a PR to trigger checks. Ordinary `master` source pushes do not start `CI`. A version change without an existing remote `v<Version>` tag can still start release validation and publication; make such changes only as part of authorized release work. The target removes automatic validation/publication triggers and preserves explicit publication intent. Until that change is implemented, inspect the existing triggers before touching release inputs. Hosted NuGet publication currently uses trusted publishing; manual API-key publishing is described below.

The release workflow publishes library packages to nuget.org and GitHub Packages, creates the `v<Version>` tag, creates or updates the matching GitHub Release, and uploads the three `.nupkg` files, `avascope-win-x64-framework-dependent.zip`, `avascope-linux-x64-framework-dependent.zip`, `avascope-osx-arm64-framework-dependent.zip`, `avascope-osx-x64-framework-dependent.zip`, Windows/Linux installer artifacts, and `artifacts\release-manifest.json`.

The macOS ZIPs and `avascope-osx-arm64-installer` / `avascope-osx-x64-installer` terminal installers are framework-dependent, unsigned, and unnotarized. They are not App Store, `.app`, or DMG distributions and do not require paid Apple Developer Program membership. After extracting a ZIP, run `bash prepare-macos.sh` from the artifact directory to deterministically restore execute permission on the CLI, MCP server, and PreviewHost apphosts. Before running a downloaded installer, compare its SHA-256 with `release-manifest.json`, run `chmod +x avascope-osx-<architecture>-installer`, and only if Gatekeeper reports quarantine after checksum verification use `xattr -d com.apple.quarantine avascope-osx-<architecture>-installer`. The installer writes only below the user profile (by default `~/Library/Application Support/AvaScope` and `~/.local/bin`), never invokes `sudo`, and never edits shell profiles.

The hosted macOS lane runs `eng/test-macos-packaged-workflow.sh` after manifest verification and installer lifecycle validation. On the native Apple Silicon runner it installs the release-shaped artifact, attaches to the bridged sample, captures visual-tree JSON plus runtime screenshot evidence, renders preview evidence through the installed PreviewHost, and uninstalls. The Intel artifact is cross-packaged and covered by the same payload, manifest, hash, and stable-surface checks; execution requires a compatible Intel macOS runner.

Before publishing library packages manually, validate the exact publish set without pushing:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\eng\publish-nuget.ps1 -DryRun
powershell -NoProfile -ExecutionPolicy Bypass -File .\eng\publish-github-release.ps1 -Tag v1.0.0 -DryRun
```

Manual NuGet publishing requires a nuget.org API key supplied by `AVASCOPE_NUGET_API_KEY`, `NUGET_API_KEY`, or the `-ApiKey` parameter.

Then verify generated artifacts are ignored:

```powershell
git check-ignore -v artifacts\release-manifest.json artifacts\packages\AvaScope.Protocol.1.0.0.nupkg artifacts\executables\avascope-win-x64-framework-dependent.zip artifacts\executables\AvaScopeSetup.exe artifacts\samples\getting-started-preview-release.png
```
