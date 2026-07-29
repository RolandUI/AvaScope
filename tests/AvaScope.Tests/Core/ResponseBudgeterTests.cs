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
