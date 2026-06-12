using System.Text.Json;
using System.Text.Json.Nodes;
using System.Xml.Linq;
using AvaScope.Core;
using AvaScope.Protocol;
using SkiaSharp;

namespace AvaScope.Tests.Core;

public sealed class PreviewBaselineReportPackExporterTests : IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly string _testRoot = Path.Combine(Path.GetTempPath(), "AvaScope.Tests", Guid.NewGuid().ToString("N"));

    public PreviewBaselineReportPackExporterTests()
    {
        Directory.CreateDirectory(_testRoot);
    }

    [Fact]
    public void ExportWritesJsonHtmlJUnitAndSarifAssetsForPassingAndFailingEntries()
    {
        var generatedAt = new DateTimeOffset(2026, 6, 12, 8, 0, 0, TimeSpan.Zero);
        var checkedAt = new DateTimeOffset(2026, 6, 12, 7, 59, 0, TimeSpan.Zero);
        var reportDirectory = Path.Combine(_testRoot, "report-pack");
        var baselinePass = Path.Combine(_testRoot, "baseline-pass.png");
        var currentPass = Path.Combine(_testRoot, "current-pass.png");
        var diffPass = Path.Combine(_testRoot, "diff-pass.png");
        var baselineFail = Path.Combine(_testRoot, "baseline-fail.png");
        var currentFail = Path.Combine(_testRoot, "current-fail.png");
        var diffFail = Path.Combine(_testRoot, "diff-fail.png");
        WriteImage(baselinePass);
        WriteImage(currentPass);
        WriteImage(diffPass);
        WriteImage(baselineFail);
        WriteImage(currentFail, (0, 0, SKColors.Black));
        WriteImage(diffFail, (0, 0, SKColors.Red));

        var response = new PreviewBaselineCheckResponse(
            Path.Combine(_testRoot, "baseline.json"),
            passed: false,
            [
                CreateEntry(
                    0,
                    baselinePass,
                    currentPass,
                    diffPass,
                    passed: true,
                    changedPixels: 0,
                    checkedAt),
                CreateEntry(
                    1,
                    baselineFail,
                    currentFail,
                    diffFail,
                    passed: false,
                    changedPixels: 1,
                    checkedAt)
            ],
            checkedAt);

        var result = new PreviewBaselineReportPackExporter(new ManualTimeProvider(generatedAt)).Export(
            response,
            reportDirectory);

        Assert.True(result.Success, result.Error?.Message);
        Assert.Equal("failed", result.Value!.Status);
        Assert.Equal(2, result.Value.TotalEntries);
        Assert.Equal(1, result.Value.PassedEntries);
        Assert.Equal(1, result.Value.FailedEntries);
        Assert.Equal(4, result.Value.Assets.Count);
        Assert.All(result.Value.Assets, asset => Assert.True(File.Exists(asset.Path), asset.Path));

        var jsonAsset = Assert.Single(result.Value.Assets, asset => asset.Kind == "json");
        var json = JsonNode.Parse(File.ReadAllText(jsonAsset.Path))!;
        Assert.Equal("failed", json["reportPack"]!["status"]!.GetValue<string>());
        Assert.Equal("Agent Suite", json["failures"]![0]!["suite"]!.GetValue<string>());
        Assert.Contains("Image changed", json["failures"]![0]!["message"]!.GetValue<string>(), StringComparison.Ordinal);

        var htmlAsset = Assert.Single(result.Value.Assets, asset => asset.Kind == "html");
        var html = File.ReadAllText(htmlAsset.Path);
        Assert.Contains("Grouped Failures", html, StringComparison.Ordinal);
        Assert.Contains(currentFail, html, StringComparison.Ordinal);
        Assert.Contains("preset_metadata_available", html, StringComparison.Ordinal);

        var junitAsset = Assert.Single(result.Value.Assets, asset => asset.Kind == "junit");
        var junit = XDocument.Load(junitAsset.Path);
        Assert.Equal("2", junit.Root!.Attribute("tests")!.Value);
        Assert.Equal("1", junit.Root.Attribute("failures")!.Value);
        Assert.Equal(2, junit.Root.Elements("testcase").Count());

        var sarifAsset = Assert.Single(result.Value.Assets, asset => asset.Kind == "sarif");
        var sarif = JsonNode.Parse(File.ReadAllText(sarifAsset.Path))!;
        Assert.Equal("2.1.0", sarif["version"]!.GetValue<string>());
        Assert.Single(sarif["runs"]![0]!["results"]!.AsArray());
    }

    public void Dispose()
    {
        if (Directory.Exists(_testRoot))
        {
            Directory.Delete(_testRoot, recursive: true);
        }
    }

    private static PreviewBaselineCheckEntry CreateEntry(
        int index,
        string baselinePath,
        string currentPath,
        string diffPath,
        bool passed,
        long changedPixels,
        DateTimeOffset timestamp)
    {
        var baseline = new PreviewBaselineEntry(
            index,
            new PreviewViewport(4, 4),
            baselinePath,
            96,
            suiteName: "Agent Suite",
            suiteEntryId: "main",
            suiteVariantName: passed ? "light" : "dark",
            mutationPresetIds: ["wide"],
            animationTimeOffsetMs: 0);
        return new PreviewBaselineCheckEntry(
            baseline,
            currentPath,
            diffPath,
            ToolResult<PreviewResponse>.Ok(new PreviewResponse(
                currentPath,
                4,
                4,
                96,
                timestamp)),
            ToolResult<PreviewDiffResponse>.Ok(new PreviewDiffResponse(
                baselinePath,
                currentPath,
                passed,
                4,
                4,
                0,
                changedPixels,
                16,
                changedPixels / 16d * 100d,
                changedPixels == 0 ? 0 : 255,
                diffPath)));
    }

    private static void WriteImage(string path, params (int X, int Y, SKColor Color)[] pixels)
    {
        using var bitmap = new SKBitmap(4, 4);
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
