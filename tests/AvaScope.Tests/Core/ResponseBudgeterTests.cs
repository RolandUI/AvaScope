using System.Text.Json;
using AvaScope.Core;
using AvaScope.Protocol;

namespace AvaScope.Tests.Core;

public sealed class ResponseBudgeterTests
{
    [Fact]
    public void TreeBudgetTruncatesInlineShapeAndPreservesCompleteArtifact()
    {
        var root = Node(
            "root",
            [
                Node("child-1", [Node("grandchild-1")]),
                Node("child-2"),
                Node("child-3")
            ]);
        var response = new TreeResponse(
            new SessionId("budget-session"),
            "topLevel:main",
            TreeKinds.Visual,
            8,
            root);

        var bounded = ResponseBudgeter.Apply(
            response,
            maxInlineBytes: int.MaxValue,
            maxItems: 2,
            maxDepth: 1);

        Assert.NotNull(bounded.ResponseBudget);
        Assert.True(bounded.ResponseBudget!.Truncated);
        Assert.Contains("item_budget", bounded.ResponseBudget.Reasons);
        Assert.Contains("depth_budget", bounded.ResponseBudget.Reasons);
        Assert.Equal(2, bounded.ResponseBudget.ReturnedItems);
        Assert.Single(bounded.Root.Children);
        Assert.True(File.Exists(bounded.ResponseBudget.ArtifactPath));

        var artifact = JsonSerializer.Deserialize<TreeResponse>(
            File.ReadAllText(bounded.ResponseBudget.ArtifactPath!));
        Assert.Equal(4, artifact!.Root.Children.Count + artifact.Root.Children[0].Children.Count);
    }

    [Fact]
    public void FindNodesBudgetCapsMatchesAndWritesCompleteArtifact()
    {
        var matches = Enumerable.Range(0, 5)
            .Select(index => new FindNodeMatch(Node($"node-{index}")))
            .ToArray();
        var response = new FindNodesResponse(
            new SessionId("budget-session"),
            "topLevel:main",
            TreeKinds.Visual,
            8,
            matches);

        var bounded = ResponseBudgeter.Apply(
            response,
            maxInlineBytes: int.MaxValue,
            maxItems: 2,
            maxDepth: 8);

        Assert.Equal(2, bounded.Matches.Count);
        Assert.Equal(5, bounded.ResponseBudget!.TotalItems);
        Assert.True(File.Exists(bounded.ResponseBudget.ArtifactPath));
    }

    [Fact]
    public void WorkflowByteBudgetReturnsArtifactAndExplicitMetadata()
    {
        var now = DateTimeOffset.UtcNow;
        var response = new SemanticWorkflowResponse(
            "budget-workflow",
            new SessionId("budget-session"),
            "topLevel:main",
            "passed",
            now,
            now,
            [
                new SemanticWorkflowStepResult(
                    "step-1",
                    SemanticWorkflowActions.Screenshot,
                    "passed",
                    new string('x', 2048),
                    now)
            ]);

        var bounded = ResponseBudgeter.Apply(
            response,
            maxInlineBytes: 256,
            maxItems: 10,
            maxDepth: 8);

        Assert.Contains("byte_budget", bounded.ResponseBudget!.Reasons);
        Assert.True(bounded.ResponseBudget.EstimatedBytes > 256);
        Assert.Empty(bounded.Steps);
        Assert.True(File.Exists(bounded.ResponseBudget.ArtifactPath));
        Assert.Contains(new string('x', 128), File.ReadAllText(bounded.ResponseBudget.ArtifactPath!));
    }

    [Fact]
    public void WorkflowPlanUsesSharedItemBudgetAndPreservesCompleteArtifact()
    {
        var now = DateTimeOffset.UtcNow;
        var planSteps = Enumerable.Range(0, 8)
            .Select(index => new SemanticWorkflowPlanItem(
                index + 1,
                $"{index + 1}:step-{index}",
                $"step-{index}",
                SemanticWorkflowActions.Wait,
                0,
                false))
            .ToArray();
        var response = new SemanticWorkflowResponse(
            "budget-workflow-plan",
            new SessionId("budget-session"),
            "topLevel:main",
            "validated",
            now,
            now,
            [],
            plan: new SemanticWorkflowPlan(true, 8, 8, 0, 0, planSteps));

        var bounded = ResponseBudgeter.Apply(
            response,
            maxInlineBytes: int.MaxValue,
            maxItems: 3,
            maxDepth: 8);

        Assert.Equal(3, bounded.Plan!.Steps.Count);
        Assert.Equal(8, bounded.ResponseBudget!.TotalItems);
        Assert.Equal(3, bounded.ResponseBudget.ReturnedItems);
        Assert.True(File.Exists(bounded.ResponseBudget.ArtifactPath));
        var artifact = JsonSerializer.Deserialize<SemanticWorkflowResponse>(
            File.ReadAllText(bounded.ResponseBudget.ArtifactPath!));
        Assert.Equal(8, artifact!.Plan!.Steps.Count);
    }

    [Fact]
    public void WorkflowEvidenceUsesSharedBudgetAndPreservesReportReferences()
    {
        var now = DateTimeOffset.UtcNow;
        var root = Path.Combine(Path.GetTempPath(), "AvaScope.Tests", $"budget-evidence-{Guid.NewGuid():N}");
        var evidence = new SemanticWorkflowFailureEvidence(
            "partial",
            Path.Combine(root, "failure"),
            unavailableEvidence: ["screenshot", "binding_diagnostics"],
            diagnostics: [new ProtocolError("artifact_unavailable", new string('x', 1024))]);
        var reportPack = new AgentEvidenceReportPackResponse(
            Path.Combine(root, "reports"),
            "failed",
            now,
            1,
            0,
            1,
            [
                new AgentEvidenceReportPackAsset(
                    "json",
                    Path.Combine(root, "reports", "workflow-report.json"),
                    "application/json")
            ]);
        var response = new SemanticWorkflowResponse(
            "budget-workflow-evidence",
            new SessionId("budget-session"),
            "topLevel:main",
            "failed",
            now,
            now,
            [
                new SemanticWorkflowStepResult(
                    "save",
                    SemanticWorkflowActions.Invoke,
                    "failed",
                    "Verification failed.",
                    now,
                    failureEvidence: evidence)
            ],
            reportPack: reportPack);

        var bounded = ResponseBudgeter.Apply(
            response,
            maxInlineBytes: 256,
            maxItems: 2,
            maxDepth: 8);

        Assert.Empty(bounded.Steps);
        Assert.Same(reportPack, bounded.ReportPack);
        Assert.True(File.Exists(bounded.ResponseBudget!.ArtifactPath));
        var artifact = JsonSerializer.Deserialize<SemanticWorkflowResponse>(
            File.ReadAllText(bounded.ResponseBudget.ArtifactPath!));
        Assert.Equal("partial", Assert.Single(artifact!.Steps).FailureEvidence!.Status);
        Assert.Equal("failed", artifact.ReportPack!.Status);
    }

    [Fact]
    public void ScenarioLifecycleEvidenceUsesSharedBudgetAndPreservesStageReferences()
    {
        var now = DateTimeOffset.UtcNow;
        var sessionId = new SessionId("budget-lifecycle");
        var root = Path.Combine(Path.GetTempPath(), "AvaScope.Tests", "budget-lifecycle");
        var topLevels = Enumerable.Range(0, 3)
            .Select(index => new TopLevelSummary($"topLevel:{index}", "window", $"Window {index}", 800, 600, 1, index == 0))
            .ToArray();
        var build = new RuntimeScenarioBuildResult(
            RuntimeScenarioLifecycleStatuses.Passed,
            Path.Combine(root, "App.csproj"),
            "Release",
            now,
            now,
            Path.Combine(root, "build", "stdout.log"),
            Path.Combine(root, "build", "stderr.log"),
            0);
        var readiness = new RuntimeScenarioReadinessEvidence(
            RuntimeScenarioLifecycleStatuses.Ready,
            now,
            now,
            2,
            Environment.ProcessId,
            sessionId,
            topLevels: topLevels);
        var cleanup = new CloseSessionResponse(
            new SessionSummary(sessionId, SessionKinds.Runtime, SessionStates.Closed, now),
            Environment.ProcessId,
            now,
            terminateLaunchedProcessRequested: true,
            outcome: CloseSessionOutcomes.AlreadyExited,
            launchedProcessOwned: true);
        var response = new RuntimeScenarioResponse(
            "budget-lifecycle",
            "failed",
            now,
            now,
            sessionId,
            build: build,
            readiness: readiness,
            topLevels: topLevels,
            cleanup: cleanup,
            failureStage: RuntimeScenarioFailureStages.Workflow);

        var bounded = ResponseBudgeter.Apply(
            response,
            maxInlineBytes: int.MaxValue,
            maxItems: 1,
            maxDepth: 8);

        Assert.Single(bounded.TopLevels);
        Assert.Same(build, bounded.Build);
        Assert.Same(readiness, bounded.Readiness);
        Assert.Same(cleanup, bounded.Cleanup);
        Assert.Equal(RuntimeScenarioFailureStages.Workflow, bounded.FailureStage);
        Assert.True(File.Exists(bounded.ResponseBudget!.ArtifactPath));
        var artifact = JsonSerializer.Deserialize<RuntimeScenarioResponse>(
            File.ReadAllText(bounded.ResponseBudget.ArtifactPath!));
        Assert.Equal(3, artifact!.TopLevels.Count);
    }

    [Fact]
    public void DiagnosticsBudgetUsesSharedItemPolicy()
    {
        var issues = Enumerable.Range(0, 4)
            .Select(index => new ProtocolError($"issue-{index}", "Diagnostic issue."))
            .ToArray();
        var response = new DiagnosticsResponse(
            HealthResponse.Current(),
            DateTimeOffset.UtcNow,
            Path.GetTempPath(),
            issues: issues);

        var bounded = ResponseBudgeter.Apply(
            response,
            maxInlineBytes: int.MaxValue,
            maxItems: 2,
            maxDepth: 8);

        Assert.Equal(2, bounded.Issues.Count);
        Assert.Equal(4, bounded.ResponseBudget!.TotalItems);
        Assert.Contains("item_budget", bounded.ResponseBudget.Reasons);
    }

    private static TreeNodeSummary Node(string id, IReadOnlyList<TreeNodeSummary>? children = null) =>
        new(id, "Avalonia.Controls.Control", children: children);
}
