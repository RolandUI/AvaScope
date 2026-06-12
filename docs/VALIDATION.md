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

For CLI doctor/self-test work, include:

```powershell
dotnet test AvaScope.slnx --filter FullyQualifiedName~CliSmokeTests.DoctorCommandReportsLocalReadiness
dotnet test AvaScope.slnx --filter FullyQualifiedName~ProtocolContractTests.DoctorResponseSerializesStableReadinessShape
```

For diagnostics shape work, include:

```powershell
dotnet test AvaScope.slnx --filter Protocol
dotnet test AvaScope.slnx --filter Core
dotnet test AvaScope.slnx --filter Mcp
dotnet test AvaScope.slnx --filter FullyQualifiedName~Cli
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

For `v0.7.0` runtime experiment review work, include:

```powershell
dotnet test AvaScope.slnx --no-build --filter "FullyQualifiedName~ProtocolContractTests.RuntimeMutationReviewResponseSerializesStableShape|FullyQualifiedName~ProtocolContractTests.RuntimeMutationEvidenceResponseSerializesStableShape|FullyQualifiedName~LocalBridgeClientTests.MutationReviewReadsBoundedHistoryThroughBridgePipe|FullyQualifiedName~LocalBridgeClientTests.RuntimeMutationEvidenceRunnerCapturesSequencedArtifactsThroughBridgePipe|FullyQualifiedName~CliSmokeTests.MutationReviewCommandReadsHistoryAndWritesArtifactThroughBridgePipe|FullyQualifiedName~CliSmokeTests.MutateNodeEvidenceCommandCapturesSequencedArtifactsThroughBridgePipe|FullyQualifiedName~BridgeHeadlessSmokeTests.McpMutateNodeReturnsBoundedMutationContractResultsThroughLocalBridgePipe|FullyQualifiedName~BridgeHeadlessSmokeTests.McpMutateNodeEvidenceCapturesScreenshotsTreesAndDiffThroughLocalBridgePipe|FullyQualifiedName~McpStdioSmokeTests.ServerStartsOverStdioAndListsInitialTools"
```

For visual regression workflow work, include:

```powershell
dotnet test AvaScope.slnx --filter FullyQualifiedName~CliSmokeTests.BaselineCommandsCreateManifestPassCheckAndFailChangedCheck
dotnet test AvaScope.slnx --filter FullyQualifiedName~CliSmokeTests.BaselineSuiteCommandCreatesManifestAndCheckPasses
dotnet test AvaScope.slnx --filter FullyQualifiedName~PreviewImageDifferTests
dotnet test AvaScope.slnx --filter FullyQualifiedName~PreviewBaselineManagerTests
dotnet test AvaScope.slnx --filter FullyQualifiedName~ProtocolContractTests.PreviewBaselineResponsesSerializeStableShapes
dotnet test AvaScope.slnx --filter FullyQualifiedName~ProtocolContractTests.PreviewBaselineSuiteManifestSerializesStableShape
dotnet test AvaScope.slnx --filter FullyQualifiedName~ProtocolContractTests.PreviewComparisonRulesAndRegionResultsSerializeStableShape
```

For CI report validation, run a sample baseline check with `--report <report.json>`, verify the report exists and contains the same `passed` and `entries` shape as stdout, then collect upload artifacts:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\eng\collect-baseline-artifacts.ps1 -Report <report.json> -OutDir .\artifacts\visual-regression\upload
```

For agent workflow documentation work, validate the packaged CLI examples that do not require a live runtime bridge:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\eng\create-local-release.ps1 -SkipTests
.\artifacts\executables\avascope-win-x64-framework-dependent\avascope.exe doctor --manifest-dir .\artifacts\samples\agent-workflow\sessions --preview-session-store .\artifacts\samples\agent-workflow\preview-sessions
.\artifacts\executables\avascope-win-x64-framework-dependent\avascope.exe preview .\samples\AvaScope.GettingStartedApp\AvaScope.GettingStartedApp.csproj --profile main --out .\artifacts\samples\agent-workflow\main-preview.png
```

## Public Alpha Release Validation

Before marking a public-alpha readiness or release-workflow slice complete, run:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\eng\create-local-release.ps1
```

For release-based development, do not bump `Directory.Build.props` until the current target in `docs\RELEASE_PLAN.md` is ready to move to `Release Candidate`. The automatic publish path validates that the release commit subject is `Release <version>` and the release plan targets the same version:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\eng\validate-release-commit.ps1 -Version 0.2.0 -CommitSubject "Release 0.2.0" -RequiredState "Release Candidate"
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
