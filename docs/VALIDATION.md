# AvaScope Validation

Run these commands from the repository root before marking a development slice complete:

```powershell
dotnet restore AvaScope.slnx
dotnet build AvaScope.slnx
dotnet test AvaScope.slnx
git status --short
```

For the `v1.0.0` end-to-end release-readiness ledger, keep [END_TO_END_VALIDATION.md](END_TO_END_VALIDATION.md) updated with source, packaged CLI, packaged MCP, runtime bridge, report-pack, release artifact, and blocker-audit results. Record package, ZIP, manifest, hash, publish dry-run, packaged CLI, and packaged MCP release artifact checks in [RELEASE_ARTIFACT_VERIFICATION.md](RELEASE_ARTIFACT_VERIFICATION.md). Record final non-blocking post-1.0 deferrals in [POST_1_0_BACKLOG.md](POST_1_0_BACKLOG.md).

Run build and test commands sequentially. Parallel build/test invocations can contend for the same `bin/` and `obj/` outputs.

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

Record observed budgets in [PERFORMANCE_STRESS_AUDIT.md](PERFORMANCE_STRESS_AUDIT.md) and keep attach, preview, mutation, report, and package failure triage in [TROUBLESHOOTING.md](TROUBLESHOOTING.md).

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

Before marking a stable release-readiness or release-workflow slice complete, run:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\eng\create-local-release.ps1
```

For release-based development, do not bump `Directory.Build.props` until the current target in `docs\RELEASE_PLAN.md` is ready to move to `Release Candidate`. The automatic publish path validates that the release commit subject is `Release <version>` and the release plan targets the same version:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\eng\validate-release-commit.ps1 -Version 1.0.0 -CommitSubject "Release 1.0.0" -RequiredState "Release Candidate"
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

The repository `CI` workflow runs for pull requests targeting `master` and by manual dispatch. Pull-request code receives read-only repository permissions; the workflow does not use `pull_request_target` or publishing credentials. Development slices must still pass the relevant local commands above before push. NuGet publishing in CI requires a repository secret named `NUGET_API_KEY`. The separate `Release` workflow publishes from `master` or `main` when a push changes `Directory.Build.props` and the `<Version>` value has no matching remote `v<Version>` tag yet.

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
