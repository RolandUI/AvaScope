using AvaScope.Core;
using AvaScope.Protocol;

namespace AvaScope.Tests.Core;

public sealed class DesignQualityAuditBuilderTests
{
    [Fact]
    public void CreateReportsTaskScopedDesignQualityFindings()
    {
        var tree = CreateFlawedDesignTree();
        var request = new DesignQualityAuditRequest(
            tree.SessionId,
            tree.TopLevelId,
            requestId: "design-audit-test",
            scopeName: "DesignRoot");

        var result = new DesignQualityAuditBuilder(new ManualTimeProvider(DateTimeOffset.UnixEpoch)).Create(tree, request);

        Assert.True(result.Success, result.Error?.Message);
        Assert.Equal("issues_found", result.Value!.Summary.Status);
        Assert.Contains(result.Value.Findings, finding => finding.Code == "design.alignment.icon_center_mismatch" && finding.NodeId == "visual:icon");
        Assert.Contains(result.Value.Findings, finding => finding.Code == "design.spacing.repeated_item_height_inconsistent" && finding.NodeId == "visual:rows");
        Assert.Contains(result.Value.Findings, finding => finding.Code == "design.contrast.low_contrast_indicator" && finding.NodeId == "visual:indicator");
        Assert.Contains(result.Value.Findings, finding => finding.Code == "design.surface.unintended_1px_seam" && finding.NodeId == "visual:seam");
        Assert.DoesNotContain(result.Value.Findings, finding => finding.NodeId == "visual:outside-seam");
        Assert.Equal("scoped", result.Value.Summary.ScopeStatus);
        Assert.Equal("issues_found", result.Value.AgentReview.Status);
    }

    [Fact]
    public void CreateReflectsExclusionsAndSuppressionsAsIgnoredFindings()
    {
        var tree = CreateFlawedDesignTree();
        var request = new DesignQualityAuditRequest(
            tree.SessionId,
            tree.TopLevelId,
            requestId: "design-audit-suppressed",
            scopeName: "DesignRoot",
            excludeNames: ["Rows"],
            suppressions:
            [
                new DesignQualitySuppression(
                    "design.surface.unintended_1px_seam",
                    reason: "fixture accepts this seam while testing suppression output")
            ]);

        var result = new DesignQualityAuditBuilder().Create(tree, request);

        Assert.True(result.Success, result.Error?.Message);
        Assert.DoesNotContain(result.Value!.Findings, finding => finding.Code == "design.spacing.repeated_item_height_inconsistent");
        Assert.DoesNotContain(result.Value.Findings, finding => finding.Code == "design.surface.unintended_1px_seam");
        Assert.Contains(result.Value.IgnoredFindings, finding =>
            finding.Code == "design.spacing.repeated_item_height_inconsistent"
            && finding.Ignored
            && finding.IgnoredReason!.StartsWith("excluded:", StringComparison.Ordinal));
        Assert.Contains(result.Value.IgnoredFindings, finding =>
            finding.Code == "design.surface.unintended_1px_seam"
            && finding.Ignored
            && finding.IgnoredReason!.StartsWith("suppressed:", StringComparison.Ordinal));
        Assert.True(result.Value.Summary.IgnoredFindingCount >= 2);
        Assert.True(result.Value.Summary.ExcludedNodeCount >= 1);
        Assert.Equal(1, result.Value.Summary.SuppressionRuleCount);
    }

    [Fact]
    public void CreateLimitsChangedScopeToChangedNodesAndAncestors()
    {
        var tree = CreateFlawedDesignTree();
        var request = new DesignQualityAuditRequest(
            tree.SessionId,
            tree.TopLevelId,
            requestId: "design-audit-changed",
            scopeName: "DesignRoot",
            onlyChangedNodes: true,
            changedNodeIds: ["visual:icon"]);

        var result = new DesignQualityAuditBuilder().Create(tree, request);

        Assert.True(result.Success, result.Error?.Message);
        Assert.Contains(result.Value!.Findings, finding => finding.Code == "design.alignment.icon_center_mismatch");
        Assert.DoesNotContain(result.Value.Findings, finding => finding.Code == "design.spacing.repeated_item_height_inconsistent");
        Assert.Equal("scoped_changed_only", result.Value.Summary.ScopeStatus);
    }

    private static TreeResponse CreateFlawedDesignTree()
    {
        var sessionId = new SessionId("session-design");
        return new TreeResponse(
            sessionId,
            "topLevel:main",
            TreeKinds.Visual,
            8,
            new TreeNodeSummary(
                "visual:root",
                "Avalonia.Controls.Window",
                name: "MainWindow",
                bounds: new NodeBounds(0, 0, 500, 400),
                sourceMap: SourceMap(background: "#FFFFFFFF"),
                children:
                [
                    new TreeNodeSummary(
                        "visual:design-root",
                        "Avalonia.Controls.Grid",
                        name: "DesignRoot",
                        bounds: new NodeBounds(0, 0, 400, 260),
                        sourceMap: SourceMap(background: "#FFFFFFFF"),
                        children:
                        [
                            new TreeNodeSummary(
                                "visual:button",
                                "Avalonia.Controls.Border",
                                name: "IconButton",
                                bounds: new NodeBounds(10, 10, 40, 40),
                                sourceMap: SourceMap(background: "#FFFFFFFF", cornerRadius: "4"),
                                children:
                                [
                                    new TreeNodeSummary(
                                        "visual:icon",
                                        "Avalonia.Controls.PathIcon",
                                        name: "MisalignedIcon",
                                        bounds: new NodeBounds(13, 10, 16, 16),
                                        classes: ["icon"],
                                        sourceMap: SourceMap(foreground: "#FF202020"))
                                ]),
                            new TreeNodeSummary(
                                "visual:indicator",
                                "Avalonia.Controls.Border",
                                name: "LowContrastIndicator",
                                bounds: new NodeBounds(230, 10, 2, 18),
                                classes: ["indicator"],
                                sourceMap: SourceMap(background: "#FFF3F3F3")),
                            new TreeNodeSummary(
                                "visual:rows",
                                "Avalonia.Controls.StackPanel",
                                name: "Rows",
                                bounds: new NodeBounds(10, 60, 200, 100),
                                children:
                                [
                                    Row(sessionId, "visual:row-1", 60, 24),
                                    Row(sessionId, "visual:row-2", 88, 29),
                                    Row(sessionId, "visual:row-3", 122, 24)
                                ]),
                            new TreeNodeSummary(
                                "visual:seam",
                                "Avalonia.Controls.Border",
                                name: "AccidentalSeam",
                                bounds: new NodeBounds(10, 180, 160, 1),
                                classes: ["surface-seam"],
                                sourceMap: SourceMap(background: "#FF202020"))
                        ]),
                    new TreeNodeSummary(
                        "visual:outside-seam",
                        "Avalonia.Controls.Border",
                        name: "OutsideNoise",
                        bounds: new NodeBounds(410, 20, 80, 1),
                        classes: ["outside-noise"],
                        sourceMap: SourceMap(background: "#FF202020"))
                ]));
    }

    private static TreeNodeSummary Row(SessionId sessionId, string nodeId, double y, double height)
    {
        return new TreeNodeSummary(
            nodeId,
            "Avalonia.Controls.Border",
            bounds: new NodeBounds(10, y, 180, height),
            classes: ["row"],
            target: new RuntimeTargetContext(sessionId, "topLevel:main", TreeKinds.Visual, nodeId),
            sourceMap: SourceMap(background: "#FFFFFFFF", cornerRadius: "4"));
    }

    private static RuntimeNodeSourceMap SourceMap(
        string? background = null,
        string? foreground = null,
        string? borderBrush = null,
        string? cornerRadius = null)
    {
        var origins = new List<RuntimeSourcePropertyOrigin>();
        AddOrigin(origins, "Background", background);
        AddOrigin(origins, "Foreground", foreground);
        AddOrigin(origins, "BorderBrush", borderBrush);
        AddOrigin(origins, "CornerRadius", cornerRadius);

        return new RuntimeNodeSourceMap(
            origins.Count == 0 ? "not_available" : "partial",
            origins.Count == 0 ? "not_available" : "test_property_origins",
            propertyOrigins: origins);
    }

    private static void AddOrigin(List<RuntimeSourcePropertyOrigin> origins, string propertyName, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        origins.Add(new RuntimeSourcePropertyOrigin(
            propertyName,
            value,
            "test",
            "local",
            "LocalValue"));
    }

    private sealed class ManualTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow()
        {
            return utcNow;
        }
    }
}
