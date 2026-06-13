using System.Text;
using System.Text.Json;
using AvaScope.Core;
using AvaScope.Protocol;

namespace AvaScope.Tests.Core;

public sealed class PerformanceStressAuditTests : IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private readonly string _testRoot = Path.Combine(
        Path.GetTempPath(),
        "AvaScope.Tests",
        $"stress-audit-{Guid.NewGuid():N}");

    public void Dispose()
    {
        if (Directory.Exists(_testRoot))
        {
            Directory.Delete(_testRoot, recursive: true);
        }
    }

    [Fact]
    public void UiAuditBuilderCapsLargeTreeIssuesInventoryAndAgentReview()
    {
        var sessionId = new SessionId("stress-ui-audit");
        var topLevelId = "topLevel:stress";
        var children = Enumerable.Range(0, 160)
            .Select(index => CreateActionableNode(sessionId, topLevelId, index))
            .ToArray();
        var root = new TreeNodeSummary(
            "visual:root",
            "Avalonia.Controls.Window",
            "StressRoot",
            children: children,
            target: new RuntimeTargetContext(sessionId, topLevelId, TreeKinds.Visual));
        var tree = new TreeResponse(
            sessionId,
            topLevelId,
            TreeKinds.Visual,
            depthLimit: 64,
            root);

        var result = new UiAuditBuilder(new ManualTimeProvider(DateTimeOffset.UnixEpoch))
            .Create(tree, maxIssues: 12, maxInventoryItems: 5);

        Assert.True(result.Success, result.Error?.Message);
        Assert.Equal(161, result.Value!.Summary.TotalNodes);
        Assert.Equal(160, result.Value.Summary.ActionableNodes);
        Assert.Equal(320, result.Value.Summary.IssueCount);
        Assert.True(result.Value.Summary.InventoryItemCount > result.Value.Inventory.Count);
        Assert.True(result.Value.Summary.Truncated);
        Assert.Equal(12, result.Value.Issues.Count);
        Assert.Equal(5, result.Value.Inventory.Count);
        Assert.Equal(AgentReviewSurface.MaximumFailureSummaries, result.Value.AgentReview.Failures.Count);
        Assert.True(result.Value.AgentReview.Truncated);
        Assert.Contains("nodes: 161", result.Value.AgentReview.Summary, StringComparer.Ordinal);
    }

    [Fact]
    public async Task DiagnosticsCapsLargeManifestAndPreviewSessionIssuePayloads()
    {
        Directory.CreateDirectory(_testRoot);
        for (var index = 0; index < 120; index++)
        {
            await File.WriteAllTextAsync(
                Path.Combine(_testRoot, $"manifest-{index:D3}.json"),
                "{",
                Encoding.UTF8);
        }

        var previewDiagnostics = Enumerable.Range(0, 250)
            .Select(index => new PreviewSessionDiagnostic(
                DiagnosticStatuses.Stale,
                Path.Combine(_testRoot, "preview-sessions", $"preview-{index:D3}.json"),
                new SessionSummary(
                    new SessionId($"preview-stale-{index:D3}"),
                    SessionKinds.Preview,
                    SessionStates.Failed,
                    DateTimeOffset.UnixEpoch,
                    $"Stale preview {index:D3}"),
                error: new ProtocolError(
                    CoreErrorCodes.PreviewSessionStoreFailed,
                    "Preview session record is stale.")))
            .ToArray();
        var client = new LocalBridgeClient(_testRoot, TimeSpan.FromMilliseconds(5));

        var result = await client.DiagnosticsAsync(
            maxSessions: 100,
            previewSessions: previewDiagnostics);

        Assert.True(result.Success, result.Error?.Message);
        Assert.Equal(100, result.Value!.BridgeSessions.Count);
        Assert.Equal(250, result.Value.PreviewSessions.Count);
        Assert.Contains(result.Value.Issues, issue => issue.Code == CoreErrorCodes.DiagnosticsTruncated);
        Assert.Equal(200, result.Value.DiagnosticIssues.Count);
        Assert.Equal(CoreErrorCodes.DiagnosticsTruncated, result.Value.DiagnosticIssues[0].Code);
        Assert.Equal(100, result.Value.DiagnosticIssues.Count(issue => issue.Source == DiagnosticIssueSources.BridgeSession));
        Assert.Equal(99, result.Value.DiagnosticIssues.Count(issue => issue.Source == DiagnosticIssueSources.PreviewSession));
    }

    [Fact]
    public async Task PreviewSessionRegistryHandlesRepeatedOneShotReloadsAndPersistentRestore()
    {
        Directory.CreateDirectory(_testRoot);
        var viewPath = Path.Combine(_testRoot, "RepeatedPreviewView.axaml");
        var outputPath = Path.Combine(_testRoot, "preview.png");
        var store = new PreviewSessionStore(Path.Combine(_testRoot, "preview-store"));
        var timeProvider = new ManualTimeProvider(new DateTimeOffset(2026, 6, 13, 8, 0, 0, TimeSpan.Zero));
        var previewHost = new PreviewHostClient(Path.Combine(AppContext.BaseDirectory, "AvaScope.PreviewHost.dll"));
        var registry = new PreviewSessionRegistry(
            new SessionRegistry(timeProvider),
            previewHost,
            timeProvider,
            store);

        await File.WriteAllTextAsync(viewPath, CreatePreviewMarkup("initial"));
        var created = await registry.CreateAsync(
            new PreviewRequest(
                outputPath,
                width: 180,
                height: 100,
                dpi: 96,
                viewPath: viewPath),
            "Repeated preview stress");

        Assert.True(created.Success, created.Error?.Message);
        Assert.True(created.Value!.LastRender.Success, created.Value.LastRender.Error?.Message);

        var sessionId = created.Value.Session.SessionId;
        PreviewSessionSummary latest = created.Value;
        for (var reload = 1; reload <= 3; reload++)
        {
            await File.WriteAllTextAsync(viewPath, CreatePreviewMarkup($"reload {reload}"));
            timeProvider.UtcNow = timeProvider.UtcNow.AddSeconds(1);

            var reloaded = await registry.ReloadAsync(sessionId);

            Assert.True(reloaded.Success, reloaded.Error?.Message);
            Assert.True(reloaded.Value!.LastRender.Success, reloaded.Value.LastRender.Error?.Message);
            latest = reloaded.Value;
        }

        Assert.True(File.Exists(outputPath));
        Assert.Equal(SessionStates.Active, latest.Session.State);
        Assert.False(latest.Lifecycle.PersistentHostEnabled);
        Assert.Equal("one_shot_isolated_child_process", latest.Lifecycle.HostProcessMode);
        Assert.True(latest.Events.Count >= 4);

        var restoredRegistry = new PreviewSessionRegistry(
            new SessionRegistry(timeProvider),
            previewHost,
            timeProvider,
            store);
        var restored = Assert.Single(restoredRegistry.List());
        Assert.Equal(sessionId, restored.Session.SessionId);
        Assert.Equal(SessionStates.Active, restored.Session.State);
        Assert.True(restored.LastRender.Success, restored.LastRender.Error?.Message);
        Assert.False(restored.Lifecycle.PersistentHostEnabled);
    }

    [Fact]
    public void PreviewSessionStoreCapsLargePersistentSessionDiagnosticsAndCleanup()
    {
        var store = new PreviewSessionStore(Path.Combine(_testRoot, "preview-store"));
        for (var index = 0; index < 130; index++)
        {
            var saved = store.Save(CreateClosedPreviewSession(index));
            Assert.True(saved.Success, saved.Error?.Message);
        }

        Assert.Equal(130, store.Load().Count);

        var diagnostics = store.GetDiagnostics();
        Assert.Equal(100, diagnostics.Count);
        Assert.All(diagnostics, diagnostic => Assert.Equal(DiagnosticStatuses.Stale, diagnostic.Status));

        var cleanup = store.CleanupStale();

        Assert.True(cleanup.Success, cleanup.Error?.Message);
        Assert.Equal(100, cleanup.Value!.DeletedPreviewSessionRecords);
        Assert.Equal(100, cleanup.Value.StalePreviewSessions.Count);
        Assert.Equal(30, store.Load().Count);
    }

    [Fact]
    public void BaselineSuiteExpansionHandlesLargeVariantMatrixWithDeterministicArtifactPaths()
    {
        Directory.CreateDirectory(_testRoot);
        var suitePath = Path.Combine(_testRoot, "stress-suite.json");
        var outputDirectory = Path.Combine(_testRoot, "baseline-images");
        var suite = new PreviewBaselineSuiteManifest(
            PreviewBaselineSuiteManifest.CurrentVersion,
            "Stress Suite",
            [
                new PreviewBaselineSuiteEntry("main", "Sample.csproj", "Views/MainView.axaml"),
                new PreviewBaselineSuiteEntry("dialog", "Sample.csproj", "Views/DialogView.axaml")
            ],
            new PreviewBaselineSuiteDefaults(
                sizes:
                [
                    new PreviewViewport(320, 180),
                    new PreviewViewport(640, 360),
                    new PreviewViewport(1280, 720)
                ],
                dpis: [96, 144],
                themes: ["light", "dark"],
                cultures: ["en-US", "hu-HU"],
                designDataTypes: ["SamplePreviewData", "AlternatePreviewData"],
                animationFramesMs: [0, 100, 250],
                mutationPresetIds: ["wide"],
                comparisonRules: new PreviewComparisonRules(
                    tolerance: 2,
                    maxChangedPercent: 0.5)),
            [
                new PreviewBaselineMutationPreset(
                    "wide",
                    "Metadata-only width mutation preset.",
                    [
                        new RuntimeMutationOperation(
                            RuntimeMutationOperationKinds.SetProperty,
                            propertyName: "Width",
                            value: "640",
                            valueType: "double")
                    ])
            ]);
        File.WriteAllText(suitePath, JsonSerializer.Serialize(suite, JsonOptions), Encoding.UTF8);

        var result = new PreviewBaselineManager(new PreviewHostClient()).ExpandSuiteManifest(
            suitePath,
            outputDirectory);

        Assert.True(result.Success, result.Error?.Message);
        Assert.Equal(288, result.Value!.Count);
        Assert.Equal(Enumerable.Range(0, result.Value.Count), result.Value.Select(static expansion => expansion.Index));
        Assert.Equal(
            result.Value.Count,
            result.Value.Select(static expansion => expansion.ImagePath).Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.All(result.Value, expansion =>
        {
            Assert.StartsWith(Path.GetFullPath(outputDirectory), expansion.ImagePath, StringComparison.OrdinalIgnoreCase);
            Assert.Equal("wide", Assert.Single(expansion.MutationPresetIds));
            Assert.Equal(2, expansion.ComparisonRules!.Tolerance);
            Assert.Equal(0.5, expansion.ComparisonRules.MaxChangedPercent);
        });
        Assert.Equal("main", result.Value[0].EntryId);
        Assert.Equal("dialog", result.Value[^1].EntryId);
        Assert.Equal(250, result.Value[^1].AnimationTimeOffsetMs);
    }

    private static TreeNodeSummary CreateActionableNode(SessionId sessionId, string topLevelId, int index)
    {
        var nodeId = $"visual:button-{index:D3}";
        return new TreeNodeSummary(
            nodeId,
            "Avalonia.Controls.Button",
            classes: ["primary", "stress"],
            target: new RuntimeTargetContext(sessionId, topLevelId, TreeKinds.Visual, nodeId),
            accessibilityState: new RuntimeAccessibilityState(
                "stress_fixture",
                focusable: true,
                isTabStop: true,
                isEnabled: true));
    }

    private PreviewSessionSummary CreateClosedPreviewSession(int index)
    {
        return new PreviewSessionSummary(
            new SessionSummary(
                new SessionId($"preview-closed-{index:D3}"),
                SessionKinds.Preview,
                SessionStates.Closed,
                DateTimeOffset.UnixEpoch.AddSeconds(index),
                $"Closed preview {index:D3}"),
            new PreviewRequest(
                Path.Combine(_testRoot, $"preview-{index:D3}.png"),
                width: 100,
                height: 80,
                dpi: 96),
            ToolResult<PreviewResponse>.Fail(new ProtocolError(
                CoreErrorCodes.PreviewSessionStoreFailed,
                "Preview session was closed.")),
            DateTimeOffset.UnixEpoch.AddSeconds(index));
    }

    private static string CreatePreviewMarkup(string text)
    {
        return $$"""
            <UserControl xmlns="https://github.com/avaloniaui">
              <Border Background="#FFFFFFFF">
                <TextBlock Text="{{text}}" />
              </Border>
            </UserControl>
            """;
    }

    private sealed class ManualTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public DateTimeOffset UtcNow { get; set; } = utcNow;

        public override DateTimeOffset GetUtcNow() => UtcNow;
    }
}
