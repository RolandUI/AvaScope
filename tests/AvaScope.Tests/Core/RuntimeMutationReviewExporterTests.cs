using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using AvaScope.Core;
using AvaScope.Protocol;
using SkiaSharp;

namespace AvaScope.Tests.Core;

public sealed class RuntimeMutationReviewExporterTests : IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private readonly string _testRoot = Path.Combine(Path.GetTempPath(), "AvaScope.Tests", Guid.NewGuid().ToString("N"));

    public RuntimeMutationReviewExporterTests()
    {
        Directory.CreateDirectory(_testRoot);
    }

    [Fact]
    public void ExportEvidenceEmbedsClickableScreenshotNodeMapWithProvenance()
    {
        var generatedAt = new DateTimeOffset(2026, 7, 1, 12, 0, 0, TimeSpan.Zero);
        var artifactDirectory = Path.Combine(_testRoot, "evidence");
        var beforeScreenshot = Path.Combine(artifactDirectory, "request-1-before.png");
        var afterScreenshot = Path.Combine(artifactDirectory, "request-1-after.png");
        var diffPath = Path.Combine(artifactDirectory, "request-1-diff.png");
        var beforeTreePath = Path.Combine(artifactDirectory, "request-1-before-visual-tree.json");
        var afterTreePath = Path.Combine(artifactDirectory, "request-1-after-visual-tree.json");
        var reviewPath = Path.Combine(artifactDirectory, "request-1-review.html");
        var sourcePath = Path.Combine(_testRoot, "Views", "MainView.axaml");
        Directory.CreateDirectory(Path.GetDirectoryName(sourcePath)!);
        Directory.CreateDirectory(artifactDirectory);
        WriteImage(beforeScreenshot, 40, 30);
        WriteImage(afterScreenshot, 40, 30, (8, 8, SKColors.SteelBlue));
        WriteImage(diffPath, 40, 30, (8, 8, SKColors.Red));
        WriteTreeSnapshot(beforeTreePath, sourcePath, buttonText: "Before");
        WriteTreeSnapshot(afterTreePath, sourcePath, buttonText: "After");

        var sessionId = new SessionId("session-1");
        var target = new RuntimeTargetContext(sessionId, "topLevel:main", TreeKinds.Visual, "visual:button");
        var operation = new RuntimeMutationOperation(
            RuntimeMutationOperationKinds.SetProperty,
            propertyName: "Background",
            value: "#336699",
            valueType: "brush");
        var mutation = new RuntimeMutationResponse(
            "request-1",
            "mutation:session-1:1",
            sessionId,
            target.TopLevelId,
            target,
            operation,
            RuntimeMutationStatuses.Applied,
            applied: true,
            generatedAt,
            RuntimeMutationCapabilityCatalog.CurrentBridgeCapabilities());
        var summary = new RuntimeMutationEvidenceSummary(
            "captured",
            RuntimeMutationStatuses.Applied,
            mutationApplied: true,
            screenshotsCaptured: true,
            visualTreeSnapshotsCaptured: true,
            diffStatus: "changed",
            beforeVisualTreeNodeCount: 2,
            afterVisualTreeNodeCount: 2,
            beforeTargetFound: true,
            afterTargetFound: true,
            changedPixels: 1,
            changedPixelPercentage: 0.08);
        var evidence = new RuntimeMutationEvidenceResponse(
            "request-1",
            sessionId,
            target.TopLevelId,
            target,
            mutation,
            summary,
            artifactDirectory,
            beforeScreenshot,
            afterScreenshot,
            beforeTreePath,
            afterTreePath,
            generatedAt,
            diffPath,
            new PreviewDiffResponse(
                beforeScreenshot,
                afterScreenshot,
                passed: false,
                pixelWidth: 40,
                pixelHeight: 30,
                tolerance: 0,
                changedPixels: 1,
                totalPixels: 1200,
                changedPercent: 0.08,
                maxDelta: 255,
                diffPath: diffPath),
            new RuntimeMutationEvidenceTargetSummary(
                "visual:button",
                "Avalonia.Controls.Button",
                name: "PrimaryButton",
                text: "Before",
                bounds: new NodeBounds(5, 5, 20, 10),
                classes: ["primary"]),
            new RuntimeMutationEvidenceTargetSummary(
                "visual:button",
                "Avalonia.Controls.Button",
                name: "PrimaryButton",
                text: "After",
                bounds: new NodeBounds(5, 5, 20, 10),
                classes: ["primary"]));

        var result = new RuntimeMutationReviewExporter(new ManualTimeProvider(generatedAt))
            .ExportEvidence(evidence, reviewPath);

        Assert.True(result.Success, result.Error?.Message);
        Assert.True(File.Exists(result.Value!.ArtifactPath), result.Value.ArtifactPath);
        var html = File.ReadAllText(result.Value.ArtifactPath);
        Assert.Contains("id=\"avascope-node-map-data\"", html, StringComparison.Ordinal);
        Assert.Contains("data-node-map-id=\"before\"", html, StringComparison.Ordinal);
        Assert.Contains("data-node-map-id=\"after\"", html, StringComparison.Ordinal);
        Assert.Contains("pickNearestNode", html, StringComparison.Ordinal);
        Assert.Contains("node-map-result", html, StringComparison.Ordinal);

        var nodeMapJson = JsonNode.Parse(ExtractNodeMapJson(html))!;
        Assert.Equal("available", nodeMapJson["before"]!["status"]!.GetValue<string>());
        var beforeNodes = nodeMapJson["before"]!["nodes"]!.AsArray();
        var button = Assert.Single(beforeNodes, node => node!["nodeId"]!.GetValue<string>() == "visual:button")!;
        Assert.Equal("Avalonia.Controls.Button", button["nodeType"]!.GetValue<string>());
        Assert.Equal("PrimaryButton", button["sourceMap"]!["xName"]!.GetValue<string>());
        Assert.Equal(Path.GetFullPath(sourcePath), button["sourceMap"]!["filePath"]!.GetValue<string>());
        Assert.Equal(42, button["sourceMap"]!["line"]!.GetValue<int>());
        Assert.Equal("Background", button["sourceMap"]!["propertyOrigins"]![0]!["propertyName"]!.GetValue<string>());
        Assert.Equal("local_value", button["sourceMap"]!["propertyOrigins"]![0]!["origin"]!.GetValue<string>());
        Assert.Equal("AccentBrush", button["sourceMap"]!["propertyOrigins"]![0]!["resourceKey"]!.GetValue<string>());
        Assert.Equal("Button.primary", button["sourceMap"]!["propertyOrigins"]![0]!["styleSelector"]!.GetValue<string>());
        Assert.Equal("Title", button["sourceMap"]!["bindings"]![0]!["bindingPath"]!.GetValue<string>());
    }

    public void Dispose()
    {
        if (Directory.Exists(_testRoot))
        {
            Directory.Delete(_testRoot, recursive: true);
        }
    }

    private static void WriteTreeSnapshot(string path, string sourcePath, string buttonText)
    {
        var sourceMap = new RuntimeNodeSourceMap(
            "available",
            "xaml_source_metadata",
            sourcePath,
            line: 42,
            column: 9,
            xName: "PrimaryButton",
            elementType: "Avalonia.Controls.Button",
            elementPath: "/Window/Button",
            propertyOrigins:
            [
                new RuntimeSourcePropertyOrigin(
                    "Background",
                    "#336699",
                    "brush",
                    "local_value",
                    "LocalValue",
                    resourceKey: "AccentBrush",
                    styleSelector: "Button.primary",
                    sourcePath: sourcePath,
                    line: 42)
            ],
            bindings:
            [
                new RuntimeSourceBinding(
                    "Content",
                    "Title",
                    "{CompiledBinding Title}",
                    "compiled_binding",
                    "resolved",
                    sourcePath: sourcePath,
                    line: 43,
                    dataTypeName: "MainViewModel")
            ]);
        var tree = new TreeResponse(
            new SessionId("session-1"),
            "topLevel:main",
            TreeKinds.Visual,
            8,
            new TreeNodeSummary(
                "visual:root",
                "Avalonia.Controls.Window",
                bounds: new NodeBounds(0, 0, 40, 30),
                children:
                [
                    new TreeNodeSummary(
                        "visual:button",
                        "Avalonia.Controls.Button",
                        name: "PrimaryButton",
                        automationId: "primary-button",
                        text: buttonText,
                        bounds: new NodeBounds(5, 5, 20, 10),
                        classes: ["primary"],
                        sourceMap: sourceMap)
                ]));

        File.WriteAllText(path, JsonSerializer.Serialize(tree, JsonOptions));
    }

    private static string ExtractNodeMapJson(string html)
    {
        const string startTag = "<script type=\"application/json\" id=\"avascope-node-map-data\">";
        const string endTag = "</script>";
        var start = html.IndexOf(startTag, StringComparison.Ordinal);
        Assert.True(start >= 0, "Expected node map data script tag.");
        start += startTag.Length;
        var end = html.IndexOf(endTag, start, StringComparison.Ordinal);
        Assert.True(end > start, "Expected node map data script closing tag.");
        return WebUtility.HtmlDecode(html[start..end]);
    }

    private static void WriteImage(string path, int width, int height, params (int X, int Y, SKColor Color)[] pixels)
    {
        using var bitmap = new SKBitmap(width, height);
        bitmap.Erase(SKColors.White);
        foreach (var pixel in pixels)
        {
            bitmap.SetPixel(pixel.X, pixel.Y, pixel.Color);
        }

        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        using var stream = File.Create(path);
        data.SaveTo(stream);
    }

    private sealed class ManualTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow()
        {
            return utcNow;
        }
    }
}
