using System.Diagnostics;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using AvaScope.Core;
using AvaScope.Protocol;

namespace AvaScope.Tests.Core;

public sealed class LocalBridgeClientTests : IDisposable
{
    private static readonly TimeSpan BridgePipeTestTimeout = TimeSpan.FromSeconds(30);

    private readonly string _manifestDirectory = Path.Combine(
        Path.GetTempPath(),
        "AvaScope.Tests",
        $"manifests-{Guid.NewGuid():N}");

    public void Dispose()
    {
        if (Directory.Exists(_manifestDirectory))
        {
            Directory.Delete(_manifestDirectory, recursive: true);
        }
    }

    [Fact]
    public void ListSessionManifestsReturnsOnlyReadableLiveProcesses()
    {
        Directory.CreateDirectory(_manifestDirectory);
        var createdAt = new DateTimeOffset(2026, 6, 6, 23, 30, 0, TimeSpan.Zero);
        var liveManifest = new BridgeSessionManifest(
            new SessionId("session-live"),
            Environment.ProcessId,
            "avascope-live",
            createdAt,
            "Live app");
        var staleManifest = new BridgeSessionManifest(
            new SessionId("session-stale"),
            int.MaxValue,
            "avascope-stale",
            createdAt.AddMinutes(1),
            "Stale app");

        File.WriteAllText(
            Path.Combine(_manifestDirectory, "live.json"),
            JsonSerializer.Serialize(liveManifest),
            Encoding.UTF8);
        File.WriteAllText(
            Path.Combine(_manifestDirectory, "stale.json"),
            JsonSerializer.Serialize(staleManifest),
            Encoding.UTF8);
        File.WriteAllText(
            Path.Combine(_manifestDirectory, "invalid.json"),
            "{",
            Encoding.UTF8);

        var client = new LocalBridgeClient(_manifestDirectory);

        var manifests = client.ListSessionManifests();

        var manifest = Assert.Single(manifests);
        Assert.Equal(liveManifest.SessionId, manifest.SessionId);
        Assert.Equal(Environment.ProcessId, manifest.ProcessId);
        Assert.Equal("Live app", manifest.DisplayName);
    }

    [Fact]
    public async Task AttachToAppCanSelectManifestPathAndProcessName()
    {
        Directory.CreateDirectory(_manifestDirectory);
        var sessionId = SessionId.New();
        var pipeName = $"avascope-core-test-{Guid.NewGuid():N}";
        var processName = Process.GetCurrentProcess().ProcessName;
        var manifestPath = WriteManifest(
            "selected.json",
            new BridgeSessionManifest(
                sessionId,
                Environment.ProcessId,
                pipeName,
                DateTimeOffset.UtcNow,
                "Selected app",
                processName: processName));
        var serverTask = RespondToBridgeRequestAsync(
            pipeName,
            request => BridgeIpcResponse.Ok(
                request.RequestId,
                HealthResponse.Current(SessionCapabilitiesResponse.Current(sessionId, Environment.ProcessId))));
        var client = new LocalBridgeClient(Path.Combine(_manifestDirectory, "unused"), BridgePipeTestTimeout);

        var result = await client.AttachToAppAsync(
            processName: processName + ".exe",
            manifestPath: manifestPath);
        var request = await serverTask;

        Assert.True(result.Success, result.Error?.Message);
        Assert.Equal(BridgeIpcMethods.Health, request.Method);
        Assert.Equal(sessionId, result.Value!.Session.SessionId);
        Assert.Equal(Environment.ProcessId, result.Value.ProcessId);
        Assert.Equal(processName, result.Value.ProcessName);
        Assert.Equal(Path.GetFullPath(manifestPath), result.Value.ManifestPath);
        Assert.NotNull(result.Value.EffectiveCapabilities);
        Assert.Equal(sessionId, result.Value.EffectiveCapabilities!.SessionId);
        Assert.Contains(InputActions.Select, result.Value.EffectiveCapabilities.InputActions);
        Assert.Equal(64, result.Value.EffectiveCapabilities.Revision.Length);
    }

    [Fact]
    public async Task SessionCapabilitiesReturnsEffectiveBridgeHandshake()
    {
        Directory.CreateDirectory(_manifestDirectory);
        var sessionId = SessionId.New();
        var pipeName = $"avascope-capabilities-{Guid.NewGuid():N}";
        WriteManifest(
            "capabilities.json",
            new BridgeSessionManifest(
                sessionId,
                Environment.ProcessId,
                pipeName,
                DateTimeOffset.UtcNow,
                "Capabilities"));
        var expected = SessionCapabilitiesResponse.Current(sessionId, Environment.ProcessId);
        var serverTask = RespondToBridgeRequestAsync(
            pipeName,
            request => BridgeIpcResponse.Ok(request.RequestId, expected));
        var client = new LocalBridgeClient(_manifestDirectory, BridgePipeTestTimeout);

        var result = await client.SessionCapabilitiesAsync(sessionId);
        var request = await serverTask;

        Assert.True(result.Success, result.Error?.Message);
        Assert.Equal(BridgeIpcMethods.Capabilities, request.Method);
        Assert.Equal(expected.Revision, result.Value!.Revision);
        Assert.Equal(BridgeIpcMethods.All, result.Value.SupportedMethods);
        Assert.Equal(InputActions.All, result.Value.InputActions);
        Assert.Equal(RuntimeMutationOperationKinds.All, result.Value.MutationCapabilities
            .SelectMany(static capability => capability.SupportedOperations)
            .Distinct(StringComparer.Ordinal)
            .ToArray());
    }

    [Fact]
    public async Task AttachLatestToAppSelectsNewestActiveMatchingManifest()
    {
        Directory.CreateDirectory(_manifestDirectory);
        var oldSessionId = SessionId.New();
        var newSessionId = SessionId.New();
        var processName = Process.GetCurrentProcess().ProcessName;
        WriteManifest(
            "old.json",
            new BridgeSessionManifest(
                oldSessionId,
                Environment.ProcessId,
                $"avascope-core-old-{Guid.NewGuid():N}",
                DateTimeOffset.UtcNow.AddMinutes(-5),
                "Old app",
                processName: processName));
        var pipeName = $"avascope-core-new-{Guid.NewGuid():N}";
        WriteManifest(
            "new.json",
            new BridgeSessionManifest(
                newSessionId,
                Environment.ProcessId,
                pipeName,
                DateTimeOffset.UtcNow,
                "New app",
                processName: processName));
        var serverTask = RespondToBridgeRequestAsync(
            pipeName,
            request => BridgeIpcResponse.Ok(request.RequestId, HealthResponse.Current()));
        var client = new LocalBridgeClient(_manifestDirectory, BridgePipeTestTimeout);

        var result = await client.AttachLatestToAppAsync(processName: processName);
        var request = await serverTask;

        Assert.True(result.Success, result.Error?.Message);
        Assert.Equal(BridgeIpcMethods.Health, request.Method);
        Assert.Equal(newSessionId, result.Value!.Session.SessionId);
    }

    [Fact]
    public async Task AttachToAppReturnsStructuredErrorWhenNoManifestMatches()
    {
        var client = new LocalBridgeClient(_manifestDirectory);

        var result = await client.AttachToAppAsync(processId: Environment.ProcessId);

        Assert.False(result.Success);
        Assert.Null(result.Value);
        Assert.Equal(CoreErrorCodes.BridgeSessionNotFound, result.Error!.Code);
    }

    [Fact]
    public async Task CaptureScreenshotRejectsEmptyTopLevelIdBeforeIpc()
    {
        var client = new LocalBridgeClient(_manifestDirectory);

        var result = await client.CaptureScreenshotAsync(
            new SessionId("session-1"),
            " ",
            "capture.png");

        Assert.False(result.Success);
        Assert.Equal(CoreErrorCodes.InvalidBridgeRequest, result.Error!.Code);
    }

    [Fact]
    public async Task VisualTreeRejectsEmptyTopLevelIdBeforeIpc()
    {
        var client = new LocalBridgeClient(_manifestDirectory);

        var result = await client.VisualTreeAsync(
            new SessionId("session-1"),
            " ");

        Assert.False(result.Success);
        Assert.Equal(CoreErrorCodes.InvalidBridgeRequest, result.Error!.Code);
    }

    [Fact]
    public async Task FindNodesRejectsMissingFiltersBeforeIpc()
    {
        var client = new LocalBridgeClient(_manifestDirectory);

        var result = await client.FindNodesAsync(
            new SessionId("session-1"),
            "topLevel:abc",
            TreeKinds.Visual);

        Assert.False(result.Success);
        Assert.Equal(CoreErrorCodes.InvalidBridgeRequest, result.Error!.Code);
    }

    [Fact]
    public async Task InspectNodeRejectsEmptyNodeIdBeforeIpc()
    {
        var client = new LocalBridgeClient(_manifestDirectory);

        var result = await client.InspectNodeAsync(
            new SessionId("session-1"),
            "topLevel:abc",
            TreeKinds.Visual,
            " ");

        Assert.False(result.Success);
        Assert.Equal(CoreErrorCodes.InvalidBridgeRequest, result.Error!.Code);
    }

    [Fact]
    public async Task InputRejectsEmptyActionBeforeIpc()
    {
        var client = new LocalBridgeClient(_manifestDirectory);

        var result = await client.InputAsync(
            new SessionId("session-1"),
            "topLevel:abc",
            " ");

        Assert.False(result.Success);
        Assert.Equal(CoreErrorCodes.InvalidBridgeRequest, result.Error!.Code);
    }

    [Fact]
    public async Task MutateNodeRejectsSessionMismatchWithoutIpc()
    {
        var client = new LocalBridgeClient(_manifestDirectory);
        var selectedSessionId = new SessionId("selected-session");
        var request = new RuntimeMutationRequest(
            "mutation-request-1",
            new RuntimeTargetContext(
                new SessionId("target-session"),
                "topLevel:abc",
                TreeKinds.Visual,
                "visual:node"),
            new RuntimeMutationOperation(RuntimeMutationOperationKinds.NoOp));

        var result = await client.MutateNodeAsync(selectedSessionId, request);

        Assert.True(result.Success, result.Error?.Message);
        Assert.False(result.Value!.Applied);
        Assert.Equal(RuntimeMutationStatuses.Unavailable, result.Value.Status);
        Assert.Equal(selectedSessionId, result.Value.SessionId);
        Assert.Equal(RuntimeMutationErrorCodes.RuntimeMutationNonLocalSession, Assert.Single(result.Value.Diagnostics).Code);
        Assert.Equal("target-session", result.Value.Diagnostics[0].Details!["targetSessionId"]);
    }

    [Fact]
    public async Task MutateNodeSendsStructuredMutationThroughBridgePipe()
    {
        Directory.CreateDirectory(_manifestDirectory);
        var sessionId = SessionId.New();
        var pipeName = $"avascope-core-test-{Guid.NewGuid():N}";
        WriteManifest(
            "mutation.json",
            new BridgeSessionManifest(
                sessionId,
                Environment.ProcessId,
                pipeName,
                DateTimeOffset.UtcNow,
                "Mutation app"));
        var request = new RuntimeMutationRequest(
            "mutation-request-2",
            new RuntimeTargetContext(sessionId, "topLevel:abc", TreeKinds.Visual, "visual:node"),
            new RuntimeMutationOperation(
                RuntimeMutationOperationKinds.ResetMutation,
                mutationId: "mutation:session:existing"));
        var serverTask = RespondToBridgeRequestAsync(
            pipeName,
            bridgeRequest =>
            {
                Assert.Equal(BridgeIpcMethods.MutateNode, bridgeRequest.Method);
                Assert.Equal("mutation-request-2", bridgeRequest.RequestId);
                Assert.NotNull(bridgeRequest.Mutation);
                Assert.Equal("topLevel:abc", bridgeRequest.Mutation.Target.TopLevelId);
                Assert.Equal("visual:node", bridgeRequest.Mutation.Target.NodeId);
                Assert.Equal(RuntimeMutationOperationKinds.ResetMutation, bridgeRequest.Mutation.Operation.Kind);
                Assert.Equal("mutation:session:existing", bridgeRequest.Mutation.Operation.MutationId);

                return BridgeIpcResponse.Ok(
                    bridgeRequest.RequestId,
                    new RuntimeMutationResponse(
                        bridgeRequest.Mutation.RequestId,
                        "mutation:session:1",
                        sessionId,
                        bridgeRequest.Mutation.Target.TopLevelId,
                        bridgeRequest.Mutation.Target,
                        bridgeRequest.Mutation.Operation,
                        RuntimeMutationStatuses.Applied,
                        applied: true,
                        DateTimeOffset.UtcNow,
                        RuntimeMutationCapabilityCatalog.CurrentBridgeCapabilities(),
                        metadata: new Dictionary<string, string>
                        {
                            ["resetMutationIds"] = "mutation:session:existing",
                            ["resetCount"] = "1",
                            ["activeMutationCount"] = "0"
                        }));
            });
        var client = new LocalBridgeClient(_manifestDirectory, BridgePipeTestTimeout);

        var result = await client.MutateNodeAsync(sessionId, request);
        var bridgeRequest = await serverTask;

        Assert.True(result.Success, result.Error?.Message);
        Assert.Equal(BridgeIpcMethods.MutateNode, bridgeRequest.Method);
        Assert.Equal("mutation:session:1", result.Value!.MutationId);
        Assert.Equal(RuntimeMutationStatuses.Applied, result.Value.Status);
        Assert.True(result.Value.Applied);
        Assert.Equal("mutation:session:existing", result.Value.Metadata["resetMutationIds"]);
        Assert.Equal("0", result.Value.Metadata["activeMutationCount"]);
        var styleCapability = Assert.Single(result.Value.Capabilities, capability =>
            capability.Name == RuntimeMutationCapabilityCatalog.StyleLayoutMutation);
        Assert.Equal("local_only", styleCapability.Metadata["transport"]);
        Assert.Equal("true", styleCapability.Metadata["temporary"]);
        Assert.Equal("true", styleCapability.Metadata["reversible"]);
        Assert.Empty(result.Value.Diagnostics);
    }

    [Fact]
    public async Task MutationReviewReadsBoundedHistoryThroughBridgePipe()
    {
        Directory.CreateDirectory(_manifestDirectory);
        var sessionId = SessionId.New();
        var pipeName = $"avascope-core-review-{Guid.NewGuid():N}";
        WriteManifest(
            "mutation-review.json",
            new BridgeSessionManifest(
                sessionId,
                Environment.ProcessId,
                pipeName,
                DateTimeOffset.UtcNow,
                "Mutation review app"));
        var target = new RuntimeTargetContext(sessionId, "topLevel:abc", TreeKinds.Visual, "visual:node");
        var operation = new RuntimeMutationOperation(
            RuntimeMutationOperationKinds.SetProperty,
            propertyName: "Width",
            value: "240",
            valueType: "double");
        var entry = new RuntimeMutationReviewEntry(
            1,
            "mutation-review-request-1",
            "mutation:session:1",
            sessionId,
            target.TopLevelId,
            target,
            operation,
            RuntimeMutationStatuses.Applied,
            applied: true,
            active: true,
            DateTimeOffset.UtcNow,
            metadata: new Dictionary<string, string>
            {
                ["propertyName"] = "Width"
            });
        var serverTask = RespondToBridgeRequestAsync(
            pipeName,
            bridgeRequest =>
            {
                Assert.Equal(BridgeIpcMethods.MutationReview, bridgeRequest.Method);
                Assert.Equal(7, bridgeRequest.MaxResults);

                return BridgeIpcResponse.Ok(
                    bridgeRequest.RequestId,
                    new RuntimeMutationReviewResponse(
                        sessionId,
                        DateTimeOffset.UtcNow,
                        historyCount: 1,
                        activeMutationCount: 1,
                        history: [entry],
                        activeMutations: [entry],
                        resetHandoff: new RuntimeMutationResetHandoff(
                            sessionId,
                            activeMutationCount: 1,
                            activeMutationIds: [entry.MutationId],
                            suggestedResetAllTarget: target)));
            });
        var client = new LocalBridgeClient(_manifestDirectory, BridgePipeTestTimeout);

        var result = await client.MutationReviewAsync(sessionId, maxResults: 7);
        var bridgeRequest = await serverTask;

        Assert.True(result.Success, result.Error?.Message);
        Assert.Equal(BridgeIpcMethods.MutationReview, bridgeRequest.Method);
        Assert.Equal(1, result.Value!.HistoryCount);
        Assert.Equal(1, result.Value.ActiveMutationCount);
        Assert.Equal("mutation:session:1", Assert.Single(result.Value.ActiveMutations).MutationId);
        Assert.Equal(RuntimeMutationOperationKinds.ResetMutation, result.Value.ResetHandoff.ResetMutationOperation);
        Assert.Equal(RuntimeMutationOperationKinds.ResetAll, result.Value.ResetHandoff.ResetAllOperation);
        Assert.Equal(target.NodeId, result.Value.ResetHandoff.SuggestedResetAllTarget!.NodeId);
    }

    [Fact]
    public async Task RuntimeMutationEvidenceRunnerCapturesSequencedArtifactsThroughBridgePipe()
    {
        Directory.CreateDirectory(_manifestDirectory);
        var sessionId = SessionId.New();
        var pipeName = $"avascope-core-evidence-{Guid.NewGuid():N}";
        var artifactDirectory = Path.Combine(
            Path.GetTempPath(),
            "AvaScope.Tests",
            $"core-evidence-{Guid.NewGuid():N}");
        WriteManifest(
            "mutation-evidence.json",
            new BridgeSessionManifest(
                sessionId,
                Environment.ProcessId,
                pipeName,
                DateTimeOffset.UtcNow,
                "Mutation evidence app"));
        var request = new RuntimeMutationRequest(
            "core-evidence",
            new RuntimeTargetContext(sessionId, "topLevel:core", TreeKinds.Visual, "visual:target"),
            new RuntimeMutationOperation(
                RuntimeMutationOperationKinds.SetProperty,
                propertyName: "Text",
                value: "After",
                valueType: "string"));
        var serverTask = RespondToBridgeRequestsAsync(
            pipeName,
            expectedCount: 5,
            (index, bridgeRequest) =>
            {
                return index switch
                {
                    0 => CreateEvidenceScreenshotResponse(
                        bridgeRequest,
                        sessionId,
                        "topLevel:core",
                        "core-evidence-before.png"),
                    1 => CreateEvidenceTreeResponse(
                        bridgeRequest,
                        sessionId,
                        "topLevel:core",
                        expectedMaxDepth: 4,
                        "Before"),
                    2 => CreateEvidenceMutationResponse(
                        bridgeRequest,
                        sessionId,
                        "topLevel:core"),
                    3 => CreateEvidenceScreenshotResponse(
                        bridgeRequest,
                        sessionId,
                        "topLevel:core",
                        "core-evidence-after.png"),
                    4 => CreateEvidenceTreeResponse(
                        bridgeRequest,
                        sessionId,
                        "topLevel:core",
                        expectedMaxDepth: 4,
                        "After"),
                    _ => throw new InvalidOperationException("Unexpected bridge request index.")
                };
            });
        var client = new LocalBridgeClient(_manifestDirectory, BridgePipeTestTimeout);

        try
        {
            var result = await new RuntimeMutationEvidenceRunner().CaptureAsync(
                client,
                sessionId,
                request,
                artifactDirectory,
                maxDepth: 4,
                includeDiff: false);
            var bridgeRequests = await serverTask;

            Assert.True(result.Success, result.Error?.Message);
            Assert.Equal(
                [
                    BridgeIpcMethods.Screenshot,
                    BridgeIpcMethods.VisualTree,
                    BridgeIpcMethods.MutateNode,
                    BridgeIpcMethods.Screenshot,
                    BridgeIpcMethods.VisualTree
                ],
                bridgeRequests.Select(static bridgeRequest => bridgeRequest.Method).ToArray());
            Assert.Equal(Path.GetFullPath(artifactDirectory), result.Value!.ArtifactDirectory);
            Assert.EndsWith("core-evidence-before.png", result.Value.BeforeScreenshotPath, StringComparison.Ordinal);
            Assert.EndsWith("core-evidence-after.png", result.Value.AfterScreenshotPath, StringComparison.Ordinal);
            Assert.EndsWith("core-evidence-before-visual-tree.json", result.Value.BeforeVisualTreePath, StringComparison.Ordinal);
            Assert.EndsWith("core-evidence-after-visual-tree.json", result.Value.AfterVisualTreePath, StringComparison.Ordinal);
            Assert.Null(result.Value.Diff);
            Assert.Equal("not_requested", result.Value.Summary.DiffStatus);
            Assert.Equal("captured", result.Value.Summary.Status);
            Assert.Equal(RuntimeMutationStatuses.Applied, result.Value.Summary.MutationStatus);
            Assert.True(result.Value.Summary.MutationApplied);
            Assert.Equal(2, result.Value.Summary.BeforeVisualTreeNodeCount);
            Assert.Equal(2, result.Value.Summary.AfterVisualTreeNodeCount);
            Assert.True(result.Value.Summary.BeforeTargetFound);
            Assert.True(result.Value.Summary.AfterTargetFound);
            Assert.Equal("Before", result.Value.BeforeTarget!.Text);
            Assert.Equal("After", result.Value.AfterTarget!.Text);
            Assert.True(File.Exists(result.Value.BeforeVisualTreePath));
            Assert.True(File.Exists(result.Value.AfterVisualTreePath));
            Assert.NotNull(result.Value.ReviewArtifact);
            Assert.True(File.Exists(result.Value.ReviewArtifact!.ArtifactPath));
            Assert.Equal("html", result.Value.ReviewArtifact.Format);
            Assert.Contains("Before", await File.ReadAllTextAsync(result.Value.BeforeVisualTreePath));
            Assert.Contains("After", await File.ReadAllTextAsync(result.Value.AfterVisualTreePath));
            var reviewHtml = await File.ReadAllTextAsync(result.Value.ReviewArtifact.ArtifactPath);
            Assert.Contains("mutation:core:1", reviewHtml, StringComparison.Ordinal);
            Assert.Contains("Before", reviewHtml, StringComparison.Ordinal);
            Assert.Contains("After", reviewHtml, StringComparison.Ordinal);
        }
        finally
        {
            if (Directory.Exists(artifactDirectory))
            {
                Directory.Delete(artifactDirectory, recursive: true);
            }
        }
    }

    [Fact]
    public async Task CloseSessionReturnsStructuredErrorWhenNoManifestMatches()
    {
        var client = new LocalBridgeClient(_manifestDirectory);

        var result = await client.CloseSessionAsync(new SessionId("missing"));

        Assert.False(result.Success);
        Assert.Equal(CoreErrorCodes.BridgeSessionNotFound, result.Error!.Code);
    }

    [Fact]
    public void NativePickerPredefinedDeletedPathIsDeterministicAndProcessScoped()
    {
        Directory.CreateDirectory(_manifestDirectory);
        var sessionId = new SessionId("picker-session");
        WriteManifest(
            "picker.json",
            new BridgeSessionManifest(
                sessionId,
                Environment.ProcessId,
                "picker-test-pipe",
                DateTimeOffset.UtcNow,
                processName: Process.GetCurrentProcess().ProcessName));
        var client = new LocalBridgeClient(_manifestDirectory);

        var result = client.NativePicker(
            sessionId,
            NativePickerOperations.PredefineResult,
            @"C:\deleted",
            NativePickerResultStates.DeletedPath);

        Assert.True(result.Success, result.Error?.Message);
        Assert.Equal(NativePickerResultStates.DeletedPath, result.Value!.Status);
        Assert.Equal(Environment.ProcessId, result.Value.ProcessId);
        Assert.False(result.Value.DialogDetected);
    }

    [Fact]
    public void NativePickerPredefinedResultIsCorrelatedRedactedAndConsumedOnce()
    {
        Directory.CreateDirectory(_manifestDirectory);
        var sessionId = new SessionId("picker-one-shot");
        WriteManifest(
            "picker-one-shot.json",
            new BridgeSessionManifest(
                sessionId,
                Environment.ProcessId,
                "picker-one-shot-pipe",
                DateTimeOffset.UtcNow,
                processName: Process.GetCurrentProcess().ProcessName));
        var client = new LocalBridgeClient(_manifestDirectory);

        var prepared = client.NativePicker(
            sessionId,
            NativePickerOperations.PredefineResult,
            @"C:\private\exports\logs",
            NativePickerResultStates.Success,
            correlationId: "scenario-42",
            ttlMs: 5000);
        var wrongCorrelation = client.NativePicker(
            sessionId,
            NativePickerOperations.ConsumePredefinedResult,
            correlationId: "scenario-other");
        var consumed = client.NativePicker(
            sessionId,
            NativePickerOperations.ConsumePredefinedResult,
            correlationId: "scenario-42");
        var replay = client.NativePicker(
            sessionId,
            NativePickerOperations.ConsumePredefinedResult,
            correlationId: "scenario-42");

        Assert.True(prepared.Success, prepared.Error?.Message);
        Assert.Equal("scenario-42", prepared.Value!.CorrelationId);
        Assert.True(prepared.Value.PathRedacted);
        Assert.DoesNotContain("private", prepared.Value.SelectedPath!, StringComparison.OrdinalIgnoreCase);
        Assert.NotNull(prepared.Value.ExpiresAt);
        Assert.Equal(NativePickerResultStates.NotPrepared, wrongCorrelation.Value!.Status);
        Assert.Equal(NativePickerResultStates.Success, consumed.Value!.Status);
        Assert.NotNull(consumed.Value.ConsumedAt);
        Assert.Equal(NativePickerResultStates.NotPrepared, replay.Value!.Status);
    }

    [Fact]
    public void NativePickerPredefinedResultExpiresBeforeConsumption()
    {
        Directory.CreateDirectory(_manifestDirectory);
        var sessionId = new SessionId("picker-expiry");
        WriteManifest(
            "picker-expiry.json",
            new BridgeSessionManifest(
                sessionId,
                Environment.ProcessId,
                "picker-expiry-pipe",
                DateTimeOffset.UtcNow,
                processName: Process.GetCurrentProcess().ProcessName));
        var client = new LocalBridgeClient(_manifestDirectory);

        var prepared = client.NativePicker(
            sessionId,
            NativePickerOperations.PredefineResult,
            predefinedResult: NativePickerResultStates.Cancelled,
            correlationId: "expires",
            ttlMs: 100);
        Thread.Sleep(250);
        var consumed = client.NativePicker(
            sessionId,
            NativePickerOperations.ConsumePredefinedResult,
            correlationId: "expires");

        Assert.True(prepared.Success, prepared.Error?.Message);
        Assert.Equal(NativePickerResultStates.Expired, consumed.Value!.Status);
        Assert.NotNull(consumed.Value.ConsumedAt);
    }

    [Fact]
    public async Task SemanticWorkflowConsumesPreparedPickerResultByRequestId()
    {
        Directory.CreateDirectory(_manifestDirectory);
        var sessionId = new SessionId("picker-workflow");
        WriteManifest(
            "picker-workflow.json",
            new BridgeSessionManifest(
                sessionId,
                Environment.ProcessId,
                "picker-workflow-pipe",
                DateTimeOffset.UtcNow,
                processName: Process.GetCurrentProcess().ProcessName));
        var client = new LocalBridgeClient(_manifestDirectory);
        var prepared = client.NativePicker(
            sessionId,
            NativePickerOperations.PredefineResult,
            predefinedResult: NativePickerResultStates.Cancelled,
            correlationId: "workflow-picker");
        var request = new SemanticWorkflowRequest(
            sessionId,
            "topLevel:test",
            [new SemanticWorkflowStep(SemanticWorkflowActions.PickerResult, "consume")],
            requestId: "workflow-picker");

        var result = await new SemanticWorkflowRunner().RunAsync(client, request);

        Assert.True(prepared.Success, prepared.Error?.Message);
        Assert.True(result.Success, result.Error?.Message);
        Assert.Equal("passed", result.Value!.Status);
        var step = Assert.Single(result.Value.Steps);
        Assert.Equal(NativePickerResultStates.Cancelled, step.Picker!.Status);
        Assert.Equal("true", step.Metadata["oneShot"]);
    }

    [Fact]
    public async Task ReloadRuntimeReturnsStructuredErrorWhenNoManifestMatches()
    {
        var client = new LocalBridgeClient(_manifestDirectory);

        var result = await client.ReloadRuntimeAsync(new SessionId("missing"));

        Assert.False(result.Success);
        Assert.Null(result.Value);
        Assert.Equal(CoreErrorCodes.BridgeSessionNotFound, result.Error!.Code);
    }

    [Fact]
    public async Task DiagnosticsReturnsStructuredIssueWhenNoManifestMatches()
    {
        var client = new LocalBridgeClient(_manifestDirectory);

        var result = await client.DiagnosticsAsync(sessionId: new SessionId("missing"));

        Assert.True(result.Success, result.Error?.Message);
        Assert.Equal(Path.GetFullPath(_manifestDirectory), result.Value!.ManifestDirectory);
        Assert.Empty(result.Value.BridgeSessions);
        var issue = Assert.Single(result.Value.Issues);
        Assert.Equal(CoreErrorCodes.BridgeSessionNotFound, issue.Code);
        var diagnosticIssue = Assert.Single(result.Value.DiagnosticIssues);
        Assert.Equal(DiagnosticIssueSources.Diagnostics, diagnosticIssue.Source);
        Assert.Equal(DiagnosticIssueSeverities.Warning, diagnosticIssue.Severity);
        Assert.Equal(DiagnosticStatuses.Unavailable, diagnosticIssue.Status);
        Assert.Equal(CoreErrorCodes.BridgeSessionNotFound, diagnosticIssue.Code);
        Assert.Equal("diagnostics_summary", diagnosticIssue.Provenance);
    }

    [Fact]
    public async Task DiagnosticsReportsInvalidAndStaleManifestsWithoutThrowing()
    {
        Directory.CreateDirectory(_manifestDirectory);
        var createdAt = new DateTimeOffset(2026, 6, 7, 3, 30, 0, TimeSpan.Zero);
        var staleManifest = new BridgeSessionManifest(
            new SessionId("session-stale"),
            int.MaxValue,
            "avascope-stale",
            createdAt,
            "Stale app");

        var staleManifestPath = Path.Combine(_manifestDirectory, "stale.json");
        var invalidManifestPath = Path.Combine(_manifestDirectory, "invalid.json");
        var unsupportedTransportManifestPath = Path.Combine(_manifestDirectory, "unsupported-transport.json");
        File.WriteAllText(staleManifestPath, JsonSerializer.Serialize(staleManifest), Encoding.UTF8);
        File.WriteAllText(invalidManifestPath, "{", Encoding.UTF8);
        File.WriteAllText(
            unsupportedTransportManifestPath,
            $$"""
            {
              "sessionId": "session-unsupported-transport",
              "processId": {{Environment.ProcessId}},
              "pipeName": "avascope-unsupported-transport",
              "createdAt": "2026-06-07T03:30:00+00:00",
              "transportScope": "remote"
            }
            """,
            Encoding.UTF8);

        var client = new LocalBridgeClient(_manifestDirectory, TimeSpan.FromSeconds(5));

        var result = await client.DiagnosticsAsync();

        Assert.True(result.Success, result.Error?.Message);
        Assert.Empty(result.Value!.Issues);
        Assert.Collection(
            result.Value.BridgeSessions,
            stale =>
            {
                Assert.Equal(DiagnosticStatuses.Stale, stale.Status);
                Assert.Equal(Path.GetFullPath(staleManifestPath), stale.ManifestPath);
                Assert.Equal(staleManifest.SessionId, stale.Session!.SessionId);
                Assert.Equal(SessionStates.Failed, stale.Session.State);
                Assert.Equal(int.MaxValue, stale.ProcessId);
                Assert.Equal(DiagnosticTransportKinds.NamedPipe, stale.Transport);
                Assert.Equal(CoreErrorCodes.BridgeIpcUnavailable, stale.Error!.Code);
            },
            invalid =>
            {
                Assert.Equal(DiagnosticStatuses.Invalid, invalid.Status);
                Assert.Equal(Path.GetFullPath(invalidManifestPath), invalid.ManifestPath);
                Assert.Null(invalid.Session);
                Assert.Equal(CoreErrorCodes.BridgeManifestInvalid, invalid.Error!.Code);
            },
            unsupportedTransport =>
            {
                Assert.Equal(DiagnosticStatuses.Invalid, unsupportedTransport.Status);
                Assert.Equal(Path.GetFullPath(unsupportedTransportManifestPath), unsupportedTransport.ManifestPath);
                Assert.Null(unsupportedTransport.Session);
                Assert.Equal(CoreErrorCodes.BridgeManifestInvalid, unsupportedTransport.Error!.Code);
                Assert.Contains("transport scope", unsupportedTransport.Error.Message, StringComparison.OrdinalIgnoreCase);
            });
        Assert.Equal(3, result.Value.Summary.BridgeSessionCount);
        Assert.Equal(0, result.Value.Summary.ActiveBridgeSessionCount);
        Assert.Equal(1, result.Value.Summary.StaleBridgeSessionCount);
        Assert.Equal(2, result.Value.Summary.InvalidBridgeSessionCount);
        Assert.Equal(3, result.Value.Summary.InactiveBridgeSessionCount);
        Assert.Contains("avascope cleanup-bridge-sessions", result.Value.Summary.NextCommands);
        Assert.Collection(
            result.Value.DiagnosticIssues,
            stale =>
            {
                Assert.Equal(DiagnosticIssueSources.BridgeSession, stale.Source);
                Assert.Equal(DiagnosticIssueSeverities.Warning, stale.Severity);
                Assert.Equal(DiagnosticStatuses.Stale, stale.Status);
                Assert.Equal(CoreErrorCodes.BridgeIpcUnavailable, stale.Code);
                Assert.Equal(staleManifest.SessionId.Value, stale.SessionId);
                Assert.Equal(int.MaxValue, stale.ProcessId);
                Assert.Equal(Path.GetFullPath(staleManifestPath), stale.Path);
                Assert.Equal("bridge_session_manifest", stale.Provenance);
            },
            invalid =>
            {
                Assert.Equal(DiagnosticIssueSources.BridgeSession, invalid.Source);
                Assert.Equal(DiagnosticIssueSeverities.Error, invalid.Severity);
                Assert.Equal(DiagnosticStatuses.Invalid, invalid.Status);
                Assert.Equal(CoreErrorCodes.BridgeManifestInvalid, invalid.Code);
                Assert.Equal(Path.GetFullPath(invalidManifestPath), invalid.Path);
            },
            unsupportedTransport =>
            {
                Assert.Equal(DiagnosticIssueSources.BridgeSession, unsupportedTransport.Source);
                Assert.Equal(DiagnosticIssueSeverities.Error, unsupportedTransport.Severity);
                Assert.Equal(DiagnosticStatuses.Invalid, unsupportedTransport.Status);
                Assert.Equal(CoreErrorCodes.BridgeManifestInvalid, unsupportedTransport.Code);
                Assert.Equal(Path.GetFullPath(unsupportedTransportManifestPath), unsupportedTransport.Path);
            });
    }

    [Fact]
    public async Task DiagnosticsReportsDuplicateAndIncompatibleBridgeManifests()
    {
        Directory.CreateDirectory(_manifestDirectory);
        var duplicateSessionId = new SessionId("session-duplicate");
        var incompatibleSessionId = new SessionId("session-incompatible");
        var pipeName = $"avascope-core-test-{Guid.NewGuid():N}";
        var createdAt = new DateTimeOffset(2026, 6, 10, 8, 0, 0, TimeSpan.Zero);
        WriteManifest(
            "duplicate-a.json",
            new BridgeSessionManifest(duplicateSessionId, Environment.ProcessId, "avascope-duplicate-a", createdAt));
        WriteManifest(
            "duplicate-b.json",
            new BridgeSessionManifest(duplicateSessionId, Environment.ProcessId, "avascope-duplicate-b", createdAt.AddSeconds(1)));
        WriteManifest(
            "incompatible.json",
            new BridgeSessionManifest(
                incompatibleSessionId,
                Environment.ProcessId,
                pipeName,
                createdAt.AddSeconds(2),
                processName: Process.GetCurrentProcess().ProcessName));
        var serverTask = RespondToBridgeRequestAsync(
            pipeName,
            request => BridgeIpcResponse.Ok(
                request.RequestId,
                new HealthResponse(AvaScopeProtocol.ServiceName, new ProtocolVersion(2, 0))));
        var client = new LocalBridgeClient(_manifestDirectory, BridgePipeTestTimeout);

        var result = await client.DiagnosticsAsync(sessionId: incompatibleSessionId);
        var request = await serverTask;

        Assert.True(result.Success, result.Error?.Message);
        Assert.Equal(BridgeIpcMethods.Health, request.Method);
        var incompatible = Assert.Single(result.Value!.BridgeSessions);
        Assert.Equal(DiagnosticStatuses.Incompatible, incompatible.Status);
        Assert.Equal(CoreErrorCodes.BridgeProtocolIncompatible, incompatible.Error!.Code);
        Assert.Equal(pipeName, incompatible.PipeName);
        Assert.False(string.IsNullOrWhiteSpace(incompatible.RequestId));

        var duplicateResult = await client.DiagnosticsAsync(maxSessions: 10);

        Assert.True(duplicateResult.Success, duplicateResult.Error?.Message);
        Assert.Contains(
            duplicateResult.Value!.Issues,
            issue => issue.Code == CoreErrorCodes.BridgeManifestDuplicate
                && issue.Details is not null
                && issue.Details["sessionId"] == duplicateSessionId.Value);
    }

    [Fact]
    public async Task CleanupBridgeManifestsDeletesStaleAndInvalidRecordsOnly()
    {
        Directory.CreateDirectory(_manifestDirectory);
        var staleManifestPath = WriteManifest(
            "stale.json",
            new BridgeSessionManifest(
                new SessionId("session-stale"),
                int.MaxValue,
                "avascope-stale",
                DateTimeOffset.UtcNow));
        var invalidManifestPath = Path.Combine(_manifestDirectory, "invalid.json");
        File.WriteAllText(invalidManifestPath, "{", Encoding.UTF8);
        var liveManifestPath = WriteManifest(
            "live.json",
            new BridgeSessionManifest(
                new SessionId("session-live"),
                Environment.ProcessId,
                "avascope-live",
                DateTimeOffset.UtcNow));
        var client = new LocalBridgeClient(_manifestDirectory, TimeSpan.FromMilliseconds(50));

        var result = await client.CleanupBridgeManifestsAsync();

        Assert.True(result.Success, result.Error?.Message);
        Assert.Equal(Path.GetFullPath(_manifestDirectory), result.Value!.ManifestDirectory);
        Assert.Equal(2, result.Value.DeletedBridgeManifestRecords);
        Assert.False(File.Exists(staleManifestPath));
        Assert.False(File.Exists(invalidManifestPath));
        Assert.True(File.Exists(liveManifestPath));
        Assert.Contains(result.Value.CleanupCandidates, candidate => candidate.Status == DiagnosticStatuses.Stale);
        Assert.Contains(result.Value.CleanupCandidates, candidate => candidate.Status == DiagnosticStatuses.Invalid);
        Assert.Empty(result.Value.Issues);
    }

    [Fact]
    public async Task DiagnosticsRejectsInvalidSessionLimit()
    {
        var client = new LocalBridgeClient(_manifestDirectory);

        var result = await client.DiagnosticsAsync(maxSessions: 0);

        Assert.False(result.Success);
        Assert.Equal(CoreErrorCodes.InvalidBridgeRequest, result.Error!.Code);
    }

    [Fact]
    public async Task DiagnosticsIncludesPreviewHostDiagnosticWhenProvided()
    {
        var client = new LocalBridgeClient(_manifestDirectory);
        var previewHost = new PreviewHostDiagnostic(
            DiagnosticStatuses.Available,
            Path.Combine(AppContext.BaseDirectory, "AvaScope.PreviewHost.dll"),
            DiagnosticProcessModes.IsolatedChildProcess,
            HealthResponse.Current());

        var result = await client.DiagnosticsAsync(previewHost: previewHost);

        Assert.True(result.Success, result.Error?.Message);
        Assert.Same(previewHost, result.Value!.PreviewHost);
        Assert.Empty(result.Value.BridgeSessions);
        Assert.Empty(result.Value.Issues);
        Assert.Empty(result.Value.DiagnosticIssues);
    }

    [Fact]
    public async Task DiagnosticsReportsMixedComponentRootsAsWarning()
    {
        var client = new LocalBridgeClient(_manifestDirectory);
        var origins = new[]
        {
            new DiagnosticComponentOrigin(
                "cli",
                "C:\\repo\\.codex\\tools\\avascope\\avascope.dll",
                "C:\\repo\\.codex\\tools\\avascope",
                "C:\\repo",
                "repository"),
            new DiagnosticComponentOrigin(
                "previewHost",
                "C:\\repo\\artifacts\\executables\\avascope-win-x64-framework-dependent\\AvaScope.PreviewHost.dll",
                "C:\\repo\\artifacts\\executables\\avascope-win-x64-framework-dependent",
                "C:\\repo\\artifacts\\executables\\avascope-win-x64-framework-dependent",
                "package_artifact")
        };

        var result = await client.DiagnosticsAsync(componentOrigins: origins);

        Assert.True(result.Success, result.Error?.Message);
        Assert.Equal(origins, result.Value!.ComponentOrigins);
        var issue = Assert.Single(result.Value.Issues);
        Assert.Equal(CoreErrorCodes.DiagnosticsMixedInstallRoots, issue.Code);
        Assert.Contains("C:\\repo", issue.Details!["rootDirectories"], StringComparison.OrdinalIgnoreCase);
        Assert.Contains("previewHost:package_artifact", issue.Details["components"], StringComparison.Ordinal);
        var diagnosticIssue = Assert.Single(result.Value.DiagnosticIssues);
        Assert.Equal(DiagnosticIssueSources.Diagnostics, diagnosticIssue.Source);
        Assert.Equal(DiagnosticIssueSeverities.Warning, diagnosticIssue.Severity);
        Assert.Equal(DiagnosticStatuses.Available, diagnosticIssue.Status);
        Assert.Equal(CoreErrorCodes.DiagnosticsMixedInstallRoots, diagnosticIssue.Code);
    }

    [Fact]
    public async Task DiagnosticsTreatsTrailingDirectorySeparatorsAsTheSameComponentRoot()
    {
        var client = new LocalBridgeClient(_manifestDirectory);
        var rootDirectory = Path.Combine(_manifestDirectory, "install-root");
        var origins = new[]
        {
            new DiagnosticComponentOrigin(
                "cli",
                Path.Combine(rootDirectory, "avascope.dll"),
                rootDirectory,
                rootDirectory,
                "directory"),
            new DiagnosticComponentOrigin(
                "mcp",
                Path.Combine(rootDirectory, "AvaScope.Mcp.dll"),
                rootDirectory,
                rootDirectory + Path.DirectorySeparatorChar,
                "directory"),
            new DiagnosticComponentOrigin(
                "previewHost",
                Path.Combine(rootDirectory, "AvaScope.PreviewHost.dll"),
                rootDirectory,
                rootDirectory + Path.AltDirectorySeparatorChar,
                "directory")
        };

        var result = await client.DiagnosticsAsync(componentOrigins: origins);

        Assert.True(result.Success, result.Error?.Message);
        Assert.Equal(origins, result.Value!.ComponentOrigins);
        Assert.DoesNotContain(result.Value.Issues, issue => issue.Code == CoreErrorCodes.DiagnosticsMixedInstallRoots);
        Assert.DoesNotContain(result.Value.DiagnosticIssues, issue => issue.Code == CoreErrorCodes.DiagnosticsMixedInstallRoots);
    }

    [Fact]
    public void DiagnosticOriginBuilderClassifiesPackageArtifactRootBeforeRepositoryRoot()
    {
        var repositoryRoot = Path.Combine(_manifestDirectory, "repo");
        var packageRoot = Path.Combine(repositoryRoot, "artifacts", "executables", "avascope-win-x64-framework-dependent");
        var assemblyPath = Path.Combine(packageRoot, "AvaScope.PreviewHost.dll");
        Directory.CreateDirectory(packageRoot);
        File.WriteAllText(Path.Combine(repositoryRoot, "AvaScope.slnx"), string.Empty);
        File.WriteAllText(assemblyPath, string.Empty);

        var origin = DiagnosticOriginBuilder.Create("previewHost", assemblyPath);

        Assert.Equal("previewHost", origin.Component);
        Assert.Equal(Path.GetFullPath(packageRoot), origin.RootDirectory);
        Assert.Equal("package_artifact", origin.OriginKind);
        Assert.True(origin.Exists);
    }

    [Fact]
    public async Task DiagnosticsBuildsDiagnosticIssuesForPreviewHostAndPreviewSessions()
    {
        var client = new LocalBridgeClient(_manifestDirectory);
        var previewHostPath = Path.Combine(AppContext.BaseDirectory, "missing-preview-host.dll");
        var previewHost = new PreviewHostDiagnostic(
            DiagnosticStatuses.Unavailable,
            previewHostPath,
            DiagnosticProcessModes.IsolatedChildProcess,
            error: new ProtocolError(CoreErrorCodes.PreviewHostUnavailable, "Preview host is missing."));
        var previewRecordPath = Path.Combine(_manifestDirectory, "preview-session.json");
        var previewSessionId = new SessionId("preview-session-1");
        var previewSession = new PreviewSessionDiagnostic(
            DiagnosticStatuses.Stale,
            previewRecordPath,
            new SessionSummary(
                previewSessionId,
                SessionKinds.Preview,
                SessionStates.Failed,
                new DateTimeOffset(2026, 6, 9, 10, 0, 0, TimeSpan.Zero),
                "Stale preview"),
            error: new ProtocolError(CoreErrorCodes.PreviewSessionStoreFailed, "Preview session is stale."));

        var result = await client.DiagnosticsAsync(
            previewHost: previewHost,
            previewSessions: [previewSession]);

        Assert.True(result.Success, result.Error?.Message);
        Assert.Collection(
            result.Value!.DiagnosticIssues,
            host =>
            {
                Assert.Equal(DiagnosticIssueSources.PreviewHost, host.Source);
                Assert.Equal(DiagnosticIssueSeverities.Error, host.Severity);
                Assert.Equal(DiagnosticStatuses.Unavailable, host.Status);
                Assert.Equal(CoreErrorCodes.PreviewHostUnavailable, host.Code);
                Assert.Equal(Path.GetFullPath(previewHostPath), host.Path);
                Assert.Equal("preview_host_assembly_probe", host.Provenance);
            },
            preview =>
            {
                Assert.Equal(DiagnosticIssueSources.PreviewSession, preview.Source);
                Assert.Equal(DiagnosticIssueSeverities.Warning, preview.Severity);
                Assert.Equal(DiagnosticStatuses.Stale, preview.Status);
                Assert.Equal(CoreErrorCodes.PreviewSessionStoreFailed, preview.Code);
                Assert.Equal(previewSessionId.Value, preview.SessionId);
                Assert.Equal(Path.GetFullPath(previewRecordPath), preview.Path);
                Assert.Equal("preview_session_store_record", preview.Provenance);
            });
    }

    private string WriteManifest(string fileName, BridgeSessionManifest manifest)
    {
        var manifestPath = Path.Combine(_manifestDirectory, fileName);
        File.WriteAllText(manifestPath, JsonSerializer.Serialize(manifest), Encoding.UTF8);
        return manifestPath;
    }

    private static async Task<BridgeIpcRequest> RespondToBridgeRequestAsync(
        string pipeName,
        Func<BridgeIpcRequest, BridgeIpcResponse> responseFactory)
    {
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        try
        {
            while (true)
            {
                await using var pipe = new NamedPipeServerStream(
                    pipeName,
                    PipeDirection.InOut,
                    maxNumberOfServerInstances: 1,
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous);

                await pipe.WaitForConnectionAsync(cancellation.Token);
                var requestLine = await ReadLineAsync(pipe, cancellation.Token);
                if (string.IsNullOrWhiteSpace(requestLine))
                {
                    continue;
                }

                BridgeIpcRequest? request;
                try
                {
                    request = JsonSerializer.Deserialize<BridgeIpcRequest>(requestLine);
                }
                catch (JsonException)
                {
                    continue;
                }

                if (request is null)
                {
                    continue;
                }

                var responseBytes = Encoding.UTF8.GetBytes(
                    JsonSerializer.Serialize(responseFactory(request)) + Environment.NewLine);
                try
                {
                    await pipe.WriteAsync(responseBytes, cancellation.Token);
                    await pipe.FlushAsync(cancellation.Token);
                }
                catch (IOException)
                {
                    return request;
                }

                return request;
            }
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            throw new TimeoutException($"Timed out waiting for a bridge IPC request on pipe '{pipeName}'.");
        }
    }

    private static async Task<IReadOnlyList<BridgeIpcRequest>> RespondToBridgeRequestsAsync(
        string pipeName,
        int expectedCount,
        Func<int, BridgeIpcRequest, BridgeIpcResponse> responseFactory)
    {
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var requests = new List<BridgeIpcRequest>(expectedCount);
        try
        {
            while (requests.Count < expectedCount)
            {
                await using var pipe = new NamedPipeServerStream(
                    pipeName,
                    PipeDirection.InOut,
                    maxNumberOfServerInstances: 1,
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous);

                await pipe.WaitForConnectionAsync(cancellation.Token);
                var requestLine = await ReadLineAsync(pipe, cancellation.Token);
                if (string.IsNullOrWhiteSpace(requestLine))
                {
                    continue;
                }

                BridgeIpcRequest? request;
                try
                {
                    request = JsonSerializer.Deserialize<BridgeIpcRequest>(requestLine);
                }
                catch (JsonException)
                {
                    continue;
                }

                if (request is null)
                {
                    continue;
                }

                var index = requests.Count;
                requests.Add(request);
                var responseBytes = Encoding.UTF8.GetBytes(
                    JsonSerializer.Serialize(responseFactory(index, request)) + Environment.NewLine);
                try
                {
                    await pipe.WriteAsync(responseBytes, cancellation.Token);
                    await pipe.FlushAsync(cancellation.Token);
                }
                catch (IOException) when (requests.Count == expectedCount)
                {
                    return requests;
                }
            }

            return requests;
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            throw new TimeoutException($"Timed out waiting for {expectedCount} bridge IPC requests on pipe '{pipeName}'.");
        }
    }

    private static BridgeIpcResponse CreateEvidenceScreenshotResponse(
        BridgeIpcRequest request,
        SessionId sessionId,
        string topLevelId,
        string expectedFileName)
    {
        Assert.Equal(BridgeIpcMethods.Screenshot, request.Method);
        Assert.Equal(topLevelId, request.TopLevelId);
        Assert.NotNull(request.OutputPath);
        Assert.EndsWith(expectedFileName, request.OutputPath, StringComparison.Ordinal);

        return BridgeIpcResponse.Ok(
            request.RequestId,
            new ScreenshotResponse(
                sessionId,
                topLevelId,
                request.OutputPath!,
                10,
                10,
                DateTimeOffset.UtcNow));
    }

    private static BridgeIpcResponse CreateEvidenceTreeResponse(
        BridgeIpcRequest request,
        SessionId sessionId,
        string topLevelId,
        int expectedMaxDepth,
        string targetText)
    {
        Assert.Equal(BridgeIpcMethods.VisualTree, request.Method);
        Assert.Equal(topLevelId, request.TopLevelId);
        Assert.Equal(expectedMaxDepth, request.MaxDepth);

        return BridgeIpcResponse.Ok(
            request.RequestId,
            CreateEvidenceTree(sessionId, topLevelId, expectedMaxDepth, targetText));
    }

    private static BridgeIpcResponse CreateEvidenceMutationResponse(
        BridgeIpcRequest request,
        SessionId sessionId,
        string topLevelId)
    {
        Assert.Equal(BridgeIpcMethods.MutateNode, request.Method);
        Assert.NotNull(request.Mutation);
        Assert.Equal("core-evidence", request.Mutation.RequestId);
        Assert.Equal(topLevelId, request.Mutation.Target.TopLevelId);
        Assert.Equal("visual:target", request.Mutation.Target.NodeId);
        Assert.Equal(RuntimeMutationOperationKinds.SetProperty, request.Mutation.Operation.Kind);
        Assert.Equal("Text", request.Mutation.Operation.PropertyName);
        Assert.Equal("After", request.Mutation.Operation.Value);

        return BridgeIpcResponse.Ok(
            request.RequestId,
            new RuntimeMutationResponse(
                request.Mutation.RequestId,
                "mutation:core:1",
                sessionId,
                topLevelId,
                request.Mutation.Target,
                request.Mutation.Operation,
                RuntimeMutationStatuses.Applied,
                applied: true,
                DateTimeOffset.UtcNow,
                RuntimeMutationCapabilityCatalog.CurrentBridgeCapabilities()));
    }

    private static TreeResponse CreateEvidenceTree(
        SessionId sessionId,
        string topLevelId,
        int depthLimit,
        string targetText)
    {
        return new TreeResponse(
            sessionId,
            topLevelId,
            TreeKinds.Visual,
            depthLimit,
            new TreeNodeSummary(
                "visual:root",
                "Avalonia.Controls.Window",
                "EvidenceWindow",
                children:
                [
                    new TreeNodeSummary(
                        "visual:target",
                        "Avalonia.Controls.TextBlock",
                        "EvidenceTarget",
                        text: targetText,
                        bounds: new NodeBounds(1, 2, 100, 32),
                        classes: ["evidence-target"],
                        target: new RuntimeTargetContext(
                            sessionId,
                            topLevelId,
                            TreeKinds.Visual,
                            "visual:target"))
                ]));
    }

    private static async Task<string> ReadLineAsync(Stream stream, CancellationToken cancellationToken)
    {
        var bytes = new List<byte>();
        var buffer = new byte[128];

        while (true)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken);
            if (read == 0)
            {
                break;
            }

            for (var index = 0; index < read; index++)
            {
                if (buffer[index] == (byte)'\n')
                {
                    return Encoding.UTF8.GetString(bytes.ToArray());
                }

                if (buffer[index] != (byte)'\r')
                {
                    bytes.Add(buffer[index]);
                }
            }
        }

        return Encoding.UTF8.GetString(bytes.ToArray());
    }
}
