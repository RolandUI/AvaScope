using System.Text.Json;
using AvaScope.Core;
using AvaScope.Protocol;
using SkiaSharp;

namespace AvaScope.Tests.Core;

public sealed class RuntimeEvidencePolicyEnforcerTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(),
        "avascope-evidence-policy-tests",
        Guid.NewGuid().ToString("n"));

    [Fact]
    public void SanitizeRemovesConfiguredTextIdsAndExcludedControlContent()
    {
        var policy = CreatePolicy(
            redactedText: ["top-secret"],
            redactedAutomationIds: ["private-id"],
            excludedControlAutomationIds: ["excluded-id"]);
        var enforcer = new RuntimeEvidencePolicyEnforcer(policy);
        var sessionId = new SessionId("session-1");
        var response = new SemanticWorkflowResponse(
            "request-top-secret",
            sessionId,
            "top-level",
            "failed",
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow,
            [
                new SemanticWorkflowStepResult(
                    "inspect-private-id",
                    SemanticWorkflowActions.Inspect,
                    "failed",
                    "Found top-secret.",
                    DateTimeOffset.UtcNow,
                    inspection: new InspectNodeResponse(
                        sessionId,
                        "top-level",
                        TreeKinds.Visual,
                        "node-top-secret",
                        "SecretControl",
                        0,
                        automationId: "excluded-id",
                        text: "top-secret",
                        classes: ["top-secret"]),
                    diagnostics: [new ProtocolError("test_failure", "top-secret")],
                    metadata: new Dictionary<string, string> { ["top-secret-key"] = "public" })
            ]);

        var sanitized = enforcer.Sanitize(response);

        Assert.True(sanitized.Success);
        var json = JsonSerializer.Serialize(sanitized.Value, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.DoesNotContain("top-secret", json, StringComparison.Ordinal);
        Assert.DoesNotContain("private-id", json, StringComparison.Ordinal);
        Assert.DoesNotContain("excluded-id", json, StringComparison.Ordinal);
        Assert.Contains("[REDACTED]", json, StringComparison.Ordinal);
        Assert.Contains("[EXCLUDED]", json, StringComparison.Ordinal);
    }

    [Fact]
    public void SanitizedResponseKeepsJsonMarkdownAndJUnitFreeOfSecrets()
    {
        var run = Path.Combine(_directory, "root", "run");
        var enforcer = new RuntimeEvidencePolicyEnforcer(CreatePolicy(redactedText: ["api-token-123"]));
        Assert.True(enforcer.PrepareRun(run, [], "request").Success);
        var response = new SemanticWorkflowResponse(
            "request",
            new SessionId("session"),
            "top",
            "failed",
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow,
            [
                new SemanticWorkflowStepResult(
                    "step",
                    SemanticWorkflowActions.AssertState,
                    "failed",
                    "api-token-123 was visible",
                    DateTimeOffset.UtcNow,
                    diagnostics: [new ProtocolError("assertion_failed", "api-token-123")])
            ]);
        var sanitized = enforcer.Sanitize(response);
        Assert.True(sanitized.Success);

        var report = new SemanticWorkflowReportPackExporter().Export(
            sanitized.Value!,
            Path.Combine(run, "reports"));

        Assert.True(report.Success);
        foreach (var asset in report.Value!.Assets)
        {
            Assert.DoesNotContain("api-token-123", File.ReadAllText(asset.Path), StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task ScreenshotMaskingBlacksConfiguredRegions()
    {
        var root = Path.Combine(_directory, "root");
        var run = Path.Combine(root, "run");
        var enforcer = new RuntimeEvidencePolicyEnforcer(CreatePolicy(
            screenshotMaskRegions: [new ScreenshotRegion(1, 1, 2, 2)]));
        Assert.True(enforcer.PrepareRun(run, [], "request").Success);
        var path = Path.Combine(run, "capture.png");
        WritePng(path, 4, 4, SKColors.White);
        var request = Request(run, evidence: new SemanticWorkflowEvidenceOptions(
            exportReports: false,
            policy: enforcer.Policy));
        var screenshot = new ScreenshotResponse(request.SessionId, "top", path, 4, 4, DateTimeOffset.UtcNow);

        var masked = await enforcer.MaskScreenshotAsync(
            new LocalBridgeClient(Path.Combine(_directory, "manifests")),
            request,
            screenshot,
            CancellationToken.None);

        Assert.True(masked.Success);
        using var bitmap = SKBitmap.Decode(path);
        Assert.Equal(SKColors.White, bitmap.GetPixel(0, 0));
        Assert.Equal(SKColors.Black, bitmap.GetPixel(1, 1));
        Assert.Equal(SKColors.Black, bitmap.GetPixel(2, 2));
    }

    [Fact]
    public async Task ScreenshotMaskingFailureDeletesUnmaskedArtifact()
    {
        var root = Path.Combine(_directory, "root");
        var run = Path.Combine(root, "run");
        var enforcer = new RuntimeEvidencePolicyEnforcer(CreatePolicy(
            screenshotMaskRegions: [new ScreenshotRegion(0, 0, 1, 1)]));
        Assert.True(enforcer.PrepareRun(run, [], "request").Success);
        var path = Path.Combine(run, "invalid.png");
        File.WriteAllText(path, "not an image");
        var request = Request(run, evidence: new SemanticWorkflowEvidenceOptions(
            exportReports: false,
            policy: enforcer.Policy));

        var masked = await enforcer.MaskScreenshotAsync(
            new LocalBridgeClient(Path.Combine(_directory, "manifests")),
            request,
            new ScreenshotResponse(request.SessionId, "top", path, 1, 1, DateTimeOffset.UtcNow),
            CancellationToken.None);

        Assert.False(masked.Success);
        Assert.Equal(CoreErrorCodes.RuntimeEvidenceMaskFailed, masked.Error!.Code);
        Assert.False(File.Exists(path));
    }

    [Fact]
    public void RetentionDeletesOnlyMarkedOwnedRuns()
    {
        var root = Path.Combine(_directory, "root");
        var oldRun = Path.Combine(root, "old");
        var currentRun = Path.Combine(root, "current");
        var unowned = Path.Combine(root, "unowned");
        var oldEnforcer = new RuntimeEvidencePolicyEnforcer(CreatePolicy());
        Assert.True(oldEnforcer.PrepareRun(oldRun, [], "old").Success);
        Directory.SetLastWriteTimeUtc(oldRun, DateTime.UtcNow.AddDays(-2));
        Directory.CreateDirectory(unowned);
        File.WriteAllText(Path.Combine(unowned, "keep.txt"), "keep");
        File.WriteAllText(
            Path.Combine(unowned, ".avascope-evidence-run.json"),
            "{\"kind\":\"avascope.runtime-evidence-run\",\"version\":1,\"ownershipId\":\"00000000000000000000000000000000\",\"requestFingerprint\":\"0000000000000000000000000000000000000000000000000000000000000000\"}");
        var enforcer = new RuntimeEvidencePolicyEnforcer(CreatePolicy(retentionMaxOwnedRuns: 1));

        var prepared = enforcer.PrepareRun(currentRun, [], "current");

        Assert.True(prepared.Success);
        Assert.False(Directory.Exists(oldRun));
        Assert.True(Directory.Exists(currentRun));
        Assert.True(File.Exists(Path.Combine(unowned, "keep.txt")));
        Assert.Equal("1", prepared.Value!["retentionDeletedRuns"]);
    }

    [Fact]
    public void RetentionAgeDeletesOnlyExpiredOwnedRun()
    {
        var root = Path.Combine(_directory, "root");
        var oldRun = Path.Combine(root, "old");
        var recentRun = Path.Combine(root, "recent");
        var currentRun = Path.Combine(root, "current");
        Assert.True(new RuntimeEvidencePolicyEnforcer(CreatePolicy()).PrepareRun(oldRun, [], "old").Success);
        Assert.True(new RuntimeEvidencePolicyEnforcer(CreatePolicy()).PrepareRun(recentRun, [], "recent").Success);
        Directory.SetLastWriteTimeUtc(oldRun, DateTime.UtcNow.AddMinutes(-10));
        var enforcer = new RuntimeEvidencePolicyEnforcer(CreatePolicy(retentionMaxAgeMinutes: 5));

        var prepared = enforcer.PrepareRun(currentRun, [], "current");

        Assert.True(prepared.Success);
        Assert.False(Directory.Exists(oldRun));
        Assert.True(Directory.Exists(recentRun));
        Assert.True(Directory.Exists(currentRun));
    }

    [Fact]
    public void UnsafeArtifactPathIsRejectedBeforeWriting()
    {
        var root = Path.Combine(_directory, "root");
        var run = Path.Combine(root, "run");
        var outside = Path.Combine(_directory, "outside", "report.json");
        var enforcer = new RuntimeEvidencePolicyEnforcer(CreatePolicy());

        var prepared = enforcer.PrepareRun(run, [outside], "request");

        Assert.False(prepared.Success);
        Assert.Equal(CoreErrorCodes.RuntimeEvidencePolicyInvalid, prepared.Error!.Code);
        Assert.False(Directory.Exists(run));
    }

    [Fact]
    public void FilesystemVolumeCannotBeClaimedAsEvidenceRoot()
    {
        var volumeRoot = Path.GetPathRoot(Path.GetFullPath(_directory))!;
        var enforcer = new RuntimeEvidencePolicyEnforcer(new RuntimeEvidencePolicy(volumeRoot));

        var prepared = enforcer.PrepareRun(Path.Combine(volumeRoot, "avascope-policy-run"), [], "request");

        Assert.False(prepared.Success);
        Assert.Equal(CoreErrorCodes.RuntimeEvidencePolicyInvalid, prepared.Error!.Code);
    }

    [Fact]
    public void InvalidOwnershipMarkerFailsClosedWithoutCreatingRun()
    {
        var root = Path.Combine(_directory, "root");
        var run = Path.Combine(root, "run");
        Directory.CreateDirectory(root);
        File.WriteAllText(Path.Combine(root, ".avascope-evidence-root.json"), "not-valid-json");
        var enforcer = new RuntimeEvidencePolicyEnforcer(CreatePolicy());

        var prepared = enforcer.PrepareRun(run, [], "request");

        Assert.False(prepared.Success);
        Assert.Equal(CoreErrorCodes.RuntimeEvidencePolicyInvalid, prepared.Error!.Code);
        Assert.False(Directory.Exists(run));
    }

    [Fact]
    public void NonEmptyUnownedDirectoryCannotBeClaimedAsEvidenceRun()
    {
        var root = Path.Combine(_directory, "root");
        var run = Path.Combine(root, "run");
        Directory.CreateDirectory(run);
        File.WriteAllText(Path.Combine(run, "keep.txt"), "unowned");
        var enforcer = new RuntimeEvidencePolicyEnforcer(CreatePolicy());

        var prepared = enforcer.PrepareRun(run, [], "request");

        Assert.False(prepared.Success);
        Assert.Equal(CoreErrorCodes.RuntimeEvidencePolicyInvalid, prepared.Error!.Code);
        Assert.Equal("unowned", File.ReadAllText(Path.Combine(run, "keep.txt")));
        Assert.False(File.Exists(Path.Combine(run, ".avascope-evidence-run.json")));
    }

    [Fact]
    public void UnixSymlinkInsideOwnedRootCannotRedirectEvidenceRun()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        var root = Path.Combine(_directory, "root");
        var run = Path.Combine(root, "run");
        var outside = Path.Combine(_directory, "outside");
        Directory.CreateDirectory(root);
        Directory.CreateDirectory(outside);
        Directory.CreateSymbolicLink(run, outside);
        var enforcer = new RuntimeEvidencePolicyEnforcer(CreatePolicy());

        var prepared = enforcer.PrepareRun(run, [], "request");

        Assert.False(prepared.Success);
        Assert.Equal(CoreErrorCodes.RuntimeEvidencePolicyInvalid, prepared.Error!.Code);
        Assert.Empty(Directory.EnumerateFileSystemEntries(outside));
    }

    [Fact]
    public void OwnedRunCannotBeReusedForDifferentRequest()
    {
        var run = Path.Combine(_directory, "root", "run");
        Assert.True(new RuntimeEvidencePolicyEnforcer(CreatePolicy()).PrepareRun(run, [], "first-request").Success);
        var enforcer = new RuntimeEvidencePolicyEnforcer(CreatePolicy());

        var prepared = enforcer.PrepareRun(run, [], "second-request");

        Assert.False(prepared.Success);
        Assert.Equal(CoreErrorCodes.RuntimeEvidencePolicyInvalid, prepared.Error!.Code);
    }

    [Fact]
    public async Task PolicyNeverDeletesArtifactsOutsideOwnedRun()
    {
        var root = Path.Combine(_directory, "root");
        var run = Path.Combine(root, "run");
        var outside = Path.Combine(_directory, "outside.png");
        var enforcer = new RuntimeEvidencePolicyEnforcer(CreatePolicy(
            screenshotMaskRegions: [new ScreenshotRegion(0, 0, 1, 1)]));
        Assert.True(enforcer.PrepareRun(run, [], "request").Success);
        File.WriteAllText(outside, "unowned");
        var request = Request(run);

        var mask = await enforcer.MaskScreenshotAsync(
            new LocalBridgeClient(Path.Combine(_directory, "manifests")),
            request,
            new ScreenshotResponse(request.SessionId, "top", outside, 1, 1, DateTimeOffset.UtcNow),
            CancellationToken.None);
        var redact = enforcer.SanitizeTextFile(outside);

        Assert.False(mask.Success);
        Assert.False(redact.Success);
        Assert.True(File.Exists(outside));
        Assert.Equal("unowned", File.ReadAllText(outside));
    }

    [Fact]
    public void ActionPolicyDeniesUnlistedGesturesAndApplicationActions()
    {
        var defaultEnforcer = new RuntimeEvidencePolicyEnforcer(CreatePolicy());
        Assert.False(defaultEnforcer.AuthorizeAction(SemanticWorkflowActions.Click, null).Success);

        var gestureEnforcer = new RuntimeEvidencePolicyEnforcer(CreatePolicy(
            allowedActions: [SemanticWorkflowActions.Swipe],
            allowGestures: true));
        Assert.False(gestureEnforcer.AuthorizeAction(SemanticWorkflowActions.Swipe, null).Success);

        var customEnforcer = new RuntimeEvidencePolicyEnforcer(CreatePolicy(
            allowedActions: [SemanticWorkflowActions.CustomAction],
            allowedCustomActions: ["safe.refresh"]));
        Assert.True(customEnforcer.AuthorizeAction(SemanticWorkflowActions.CustomAction, "safe.refresh").Success);
        Assert.False(customEnforcer.AuthorizeAction(SemanticWorkflowActions.CustomAction, "danger.delete").Success);
        Assert.False(customEnforcer.AllowsDestructiveAction(requestAuthorization: true, isolatedState: false));
    }

    [Fact]
    public void SessionAndProcessAuthorizationRejectsForeignTargets()
    {
        var manifestDirectory = Path.Combine(_directory, "manifests");
        Directory.CreateDirectory(manifestDirectory);
        var sessionId = new SessionId("session-local");
        var manifest = new BridgeSessionManifest(
            sessionId,
            Environment.ProcessId,
            "avascope-policy-test",
            DateTimeOffset.UtcNow);
        File.WriteAllText(
            Path.Combine(manifestDirectory, "session.json"),
            JsonSerializer.Serialize(manifest, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
        var request = Request(Path.Combine(_directory, "root", "run"), sessionId);
        var plan = new SemanticWorkflowPlan(
            true,
            1,
            1,
            0,
            0,
            [new SemanticWorkflowPlanItem(1, "steps[0]", "inspect", SemanticWorkflowActions.Inspect, 0, false)]);

        var foreignSession = new RuntimeEvidencePolicyEnforcer(CreatePolicy(authorizedSessionIds: ["other-session"]));
        var foreignProcess = new RuntimeEvidencePolicyEnforcer(CreatePolicy(authorizedProcessIds: [int.MaxValue]));

        Assert.Equal(
            CoreErrorCodes.RuntimeEvidenceUnauthorized,
            foreignSession.Authorize(new LocalBridgeClient(manifestDirectory), request, plan).Error!.Code);
        Assert.Equal(
            CoreErrorCodes.RuntimeEvidenceUnauthorized,
            foreignProcess.Authorize(new LocalBridgeClient(manifestDirectory), request, plan).Error!.Code);
    }

    [Fact]
    public void AuditIsRedactedBeforeItIsPersistedAndReportsLocalBoundary()
    {
        var run = Path.Combine(_directory, "root", "run");
        var enforcer = new RuntimeEvidencePolicyEnforcer(CreatePolicy(redactedText: ["secret-value"]));
        Assert.True(enforcer.PrepareRun(run, [], "request").Success);
        var request = Request(run);
        var step = new SemanticWorkflowStepResult(
            "step-secret-value",
            SemanticWorkflowActions.Inspect,
            "failed",
            "secret-value",
            DateTimeOffset.UtcNow);

        var result = enforcer.AppendActionAudit(request, step);

        Assert.True(result.Success);
        var audit = File.ReadAllText(enforcer.ActionAuditPath!);
        Assert.DoesNotContain("secret-value", audit, StringComparison.Ordinal);
        Assert.Contains("[REDACTED]", audit, StringComparison.Ordinal);
        Assert.Contains("\"networkUpload\":\"disabled\"", audit, StringComparison.Ordinal);
        Assert.Contains("\"storage\":\"local_filesystem\"", audit, StringComparison.Ordinal);
    }

    [Fact]
    public async Task WorkflowValidationRejectsDisallowedActionWithoutDispatch()
    {
        var run = Path.Combine(_directory, "root", "run");
        var request = new SemanticWorkflowRequest(
            new SessionId("validation-session"),
            "top",
            [new SemanticWorkflowStep(SemanticWorkflowActions.Click, selector: new SemanticWorkflowSelector(automationId: "button"))],
            outputDirectory: run,
            validateOnly: true,
            evidence: new SemanticWorkflowEvidenceOptions(
                exportReports: false,
                policy: CreatePolicy()));

        var result = await new SemanticWorkflowRunner().RunAsync(
            new LocalBridgeClient(Path.Combine(_directory, "manifests")),
            request);

        Assert.True(result.Success);
        Assert.Equal("validation_failed", result.Value!.Status);
        Assert.Contains(result.Value.Diagnostics, diagnostic => diagnostic.Code == CoreErrorCodes.RuntimeEvidenceActionDisallowed);
        Assert.False(Directory.Exists(run));
    }

    [Fact]
    public async Task ScenarioPolicyRejectsDisallowedActionBeforeAttachOrArtifacts()
    {
        var run = Path.Combine(_directory, "root", "run");
        var request = new RuntimeScenarioRequest(
            [new SemanticWorkflowStep(SemanticWorkflowActions.Click, selector: new SemanticWorkflowSelector(automationId: "button"))],
            sessionId: new SessionId("foreign-session"),
            topLevelId: "top",
            outputDirectory: run,
            evidence: new SemanticWorkflowEvidenceOptions(
                exportReports: false,
                policy: CreatePolicy()));

        var result = await new RuntimeScenarioRunner().RunAsync(
            new LocalBridgeClient(Path.Combine(_directory, "manifests")),
            request);

        Assert.True(result.Success);
        Assert.Equal("failed", result.Value!.Status);
        Assert.Equal(RuntimeScenarioFailureStages.Validation, result.Value.FailureStage);
        Assert.Contains(result.Value.Diagnostics, diagnostic => diagnostic.Code == CoreErrorCodes.RuntimeEvidenceActionDisallowed);
        Assert.Equal("false", result.Value.Metadata["dispatchPerformed"]);
        Assert.False(Directory.Exists(run));
    }

    [Fact]
    public void NetworkUploadCannotBeEnabled()
    {
        var exception = Assert.Throws<ArgumentException>(() => new RuntimeEvidencePolicy(
            Path.Combine(_directory, "root"),
            networkUpload: true));

        Assert.Contains("unavailable", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SerializationFailureReturnsSecretFreeFailClosedDiagnostic()
    {
        var enforcer = new RuntimeEvidencePolicyEnforcer(CreatePolicy(redactedText: ["do-not-echo"]));
        var cyclic = new CyclicEvidence("do-not-echo");
        cyclic.Self = cyclic;

        var result = enforcer.SanitizeJson(cyclic);

        Assert.False(result.Success);
        Assert.Equal(CoreErrorCodes.RuntimeEvidenceRedactionFailed, result.Error!.Code);
        Assert.DoesNotContain("do-not-echo", result.Error.Message, StringComparison.Ordinal);
    }

    private RuntimeEvidencePolicy CreatePolicy(
        IReadOnlyList<string>? redactedText = null,
        IReadOnlyList<string>? redactedAutomationIds = null,
        IReadOnlyList<string>? excludedControlAutomationIds = null,
        IReadOnlyList<ScreenshotRegion>? screenshotMaskRegions = null,
        IReadOnlyList<string>? allowedActions = null,
        IReadOnlyList<string>? allowedCustomActions = null,
        bool allowGestures = false,
        bool allowDestructiveActions = false,
        IReadOnlyList<string>? authorizedSessionIds = null,
        IReadOnlyList<int>? authorizedProcessIds = null,
        int? retentionMaxAgeMinutes = null,
        int? retentionMaxOwnedRuns = null) =>
        new(
            Path.Combine(_directory, "root"),
            redactedText,
            redactedAutomationIds,
            excludedControlAutomationIds,
            screenshotMaskRegions,
            allowedActions,
            allowedCustomActions,
            allowGestures,
            allowDestructiveActions,
            authorizedSessionIds,
            authorizedProcessIds,
            retentionMaxAgeMinutes,
            retentionMaxOwnedRuns: retentionMaxOwnedRuns);

    private static SemanticWorkflowRequest Request(
        string outputDirectory,
        SessionId? sessionId = null,
        SemanticWorkflowEvidenceOptions? evidence = null) =>
        new(
            sessionId ?? new SessionId("session"),
            "top",
            [new SemanticWorkflowStep(SemanticWorkflowActions.Inspect, selector: new SemanticWorkflowSelector(nodeId: "node"))],
            outputDirectory: outputDirectory,
            evidence: evidence);

    private static void WritePng(string path, int width, int height, SKColor color)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using var bitmap = new SKBitmap(width, height);
        bitmap.Erase(color);
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        using var stream = File.Create(path);
        data.SaveTo(stream);
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    private sealed class CyclicEvidence(string value)
    {
        public string Value { get; } = value;

        public CyclicEvidence? Self { get; set; }
    }
}
