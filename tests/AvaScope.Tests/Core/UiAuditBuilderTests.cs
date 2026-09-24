using AvaScope.Core;
using AvaScope.Protocol;

namespace AvaScope.Tests.Core;

public sealed class UiAuditBuilderTests
{
    [Fact]
    public void CreateReportsAccessibilityValidationAndInventory()
    {
        var sessionId = new SessionId("session-1");
        var buttonTarget = new RuntimeTargetContext(sessionId, "topLevel:main", TreeKinds.Visual, "visual:button");
        var textBoxTarget = new RuntimeTargetContext(sessionId, "topLevel:main", TreeKinds.Visual, "visual:textbox");
        var tree = new TreeResponse(
            sessionId,
            "topLevel:main",
            TreeKinds.Visual,
            4,
            new TreeNodeSummary(
                "visual:root",
                "Avalonia.Controls.Window",
                children:
                [
                    new TreeNodeSummary(
                        "visual:button",
                        "Avalonia.Controls.Button",
                        classes: ["primary"],
                        target: buttonTarget,
                        accessibilityState: new RuntimeAccessibilityState(
                            "test",
                            focusable: true,
                            isTabStop: true,
                            tabIndex: 0)),
                    new TreeNodeSummary(
                        "visual:textbox",
                        "Avalonia.Controls.TextBox",
                        automationId: "EmailInput",
                        target: textBoxTarget,
                        accessibilityState: new RuntimeAccessibilityState(
                            "test",
                            automationName: "Email",
                            focusable: true,
                            isTabStop: true,
                            tabIndex: 1),
                        validationState: new RuntimeValidationState(
                            "has_errors",
                            "test",
                            hasErrors: true,
                            errorCount: 1,
                            errors: ["Email is required"]))
                ]),
            new RuntimeTargetContext(sessionId, "topLevel:main", TreeKinds.Visual));

        var result = new UiAuditBuilder(TimeProvider.System).Create(tree);

        Assert.True(result.Success, result.Error?.Message);
        Assert.Equal(3, result.Value!.Summary.TotalNodes);
        Assert.Equal(2, result.Value.Summary.ActionableNodes);
        Assert.Equal(1, result.Value.Summary.NodesWithAutomationId);
        Assert.Equal(1, result.Value.Summary.NodesWithValidationErrors);
        Assert.Equal("issues_found", result.Value.Summary.AccessibilityStatus);
        Assert.Equal("errors_found", result.Value.Summary.ValidationStatus);
        Assert.Equal("available", result.Value.Summary.FocusOrderStatus);
        Assert.Contains(result.Value.Issues, issue => issue.Code == "accessibility.missing_accessible_name" && issue.NodeId == "visual:button");
        Assert.Contains(result.Value.Issues, issue => issue.Code == "accessibility.missing_automation_id" && issue.NodeId == "visual:button");
        Assert.Contains(result.Value.Issues, issue => issue.Code == "validation.errors_present" && issue.NodeId == "visual:textbox");
        Assert.Contains(result.Value.Inventory, item => item.Category == "control" && item.Name == "Button" && item.Count == 1);
        Assert.Contains(result.Value.Inventory, item => item.Category == "class" && item.Name == "primary" && item.Count == 1);
        Assert.Contains(result.Value.Inventory, item => item.Category == "resource" && item.Status == "not_available");
        Assert.Equal("issues_found", result.Value.AgentReview.Status);
        Assert.Contains("issues: 3", result.Value.AgentReview.Summary);
    }

    [Fact]
    public void CreateLimitsIssueAndInventoryOutputButKeepsSummaryCounts()
    {
        var sessionId = new SessionId("session-1");
        var tree = new TreeResponse(
            sessionId,
            "topLevel:main",
            TreeKinds.Visual,
            2,
            new TreeNodeSummary(
                "visual:root",
                "Avalonia.Controls.Window",
                children:
                [
                    CreateButton(sessionId, "visual:button1"),
                    CreateButton(sessionId, "visual:button2")
                ]));

        var result = new UiAuditBuilder().Create(tree, maxIssues: 1, maxInventoryItems: 2);

        Assert.True(result.Success, result.Error?.Message);
        Assert.Single(result.Value!.Issues);
        Assert.Equal(2, result.Value.Inventory.Count);
        Assert.True(result.Value.Summary.Truncated);
        Assert.True(result.Value.Summary.IssueCount > result.Value.Issues.Count);
        Assert.True(result.Value.Summary.InventoryItemCount > result.Value.Inventory.Count);
    }

    [Fact]
    public void SelectorRecommendationsVerifyUniquenessUsingActualSearchSemanticsAndAvoidLocalizedText()
    {
        var tree = new TreeResponse(new SessionId("audit"), "window", TreeKinds.Visual, 4,
            new TreeNodeSummary("root", "Window", children:
            [
                new("one", "Avalonia.Controls.Button", automationId: "Save", text: "Save"),
                new("two", "Avalonia.Controls.Button", name: "Localized", automationId: "save", text: "Mentés"),
                new("three", "Avalonia.Controls.Button", automationId: "Cancel", text: "Mégse"),
                new("four", "Avalonia.Controls.Button", text: "Save"),
                new("five", "Avalonia.Controls.Button", name: "Shared"),
                new("six", "App.Avalonia.Controls.ButtonVariant", name: "shared"),
                new("seven", "Avalonia.Controls.Button", automationId: "Save")
            ]));
        var result = new UiAuditBuilder().Create(tree).Value!;
        var duplicate = Assert.Single(result.Issues, item => item.Code == "testability.duplicate_automation_id");
        Assert.Equal("2", duplicate.Details["matchCount"]);
        Assert.Contains("returned_tree_snapshot", result.SelectorVerificationScope, StringComparison.Ordinal);
        var ambiguous = Assert.Single(result.SelectorRecommendations, item => item.NodeId == "one");
        Assert.Equal("ambiguous", ambiguous.Status);
        Assert.Null(ambiguous.Selector);
        var localized = Assert.Single(result.SelectorRecommendations, item => item.NodeId == "two");
        Assert.Equal("save", localized.Selector!.AutomationId);
        Assert.Equal(1, localized.MatchCount);
        Assert.Null(localized.Selector.Text);
        var unique = Assert.Single(result.SelectorRecommendations, item => item.NodeId == "three");
        Assert.Equal("Cancel", unique.Selector!.AutomationId);
        Assert.Equal(tree.DepthLimit, unique.Selector.MaxDepth);
        Assert.Null(Assert.Single(result.SelectorRecommendations, item => item.NodeId == "four").Selector);
        Assert.Null(Assert.Single(result.SelectorRecommendations, item => item.NodeId == "five").Selector);
        Assert.All(result.SelectorRecommendations, item => Assert.Null(item.Selector?.NodeId));
        Assert.Single(new UiAuditBuilder().Create(tree, maxInventoryItems: 1).Value!.SelectorRecommendations);
    }

    private static TreeNodeSummary CreateButton(SessionId sessionId, string nodeId)
    {
        return new TreeNodeSummary(
            nodeId,
            "Avalonia.Controls.Button",
            target: new RuntimeTargetContext(sessionId, "topLevel:main", TreeKinds.Visual, nodeId),
            accessibilityState: new RuntimeAccessibilityState("test", focusable: true, isTabStop: true));
    }
}
