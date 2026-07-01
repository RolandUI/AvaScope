using AvaScope.Core;
using AvaScope.Protocol;
using SkiaSharp;

namespace AvaScope.Tests.Core;

public sealed class SemanticScreenshotComparerTests : IDisposable
{
    private readonly string _testRoot = Path.Combine(Path.GetTempPath(), "AvaScope.Tests", Guid.NewGuid().ToString("N"));

    public SemanticScreenshotComparerTests()
    {
        Directory.CreateDirectory(_testRoot);
    }

    [Fact]
    public void CompareFindsPaddingCenterBorderAndWritesAnnotatedArtifacts()
    {
        var referencePath = Path.Combine(_testRoot, "reference.png");
        var currentPath = Path.Combine(_testRoot, "current.png");
        var outputDirectory = Path.Combine(_testRoot, "semantic");
        WriteFixture(referencePath, shifted: false, border: false);
        WriteFixture(currentPath, shifted: true, border: true);

        var result = new SemanticScreenshotComparer().Compare(new SemanticScreenshotComparisonRequest(
            referencePath,
            currentPath,
            requestId: "semantic-core",
            outputDirectory: outputDirectory,
            tolerance: 0,
            maxFindings: 8,
            maxRawRegions: 8,
            minChangedPixels: 4));

        Assert.True(result.Success, result.Error?.Message);
        Assert.Equal("differences_found", result.Value!.Status);
        Assert.False(result.Value.RawDiff.Passed);
        Assert.True(File.Exists(result.Value.RawDiff.DiffPath), result.Value.RawDiff.DiffPath);
        Assert.True(File.Exists(result.Value.AnnotatedPath), result.Value.AnnotatedPath);
        Assert.Contains(result.Value.Findings, finding => finding.Kind == SemanticScreenshotFindingKinds.CenterMismatch);
        Assert.Contains(result.Value.Findings, finding => finding.Kind == SemanticScreenshotFindingKinds.PaddingDifference);
        Assert.Contains(result.Value.Findings, finding => finding.Kind == SemanticScreenshotFindingKinds.BorderOrSeamDifference);
        Assert.All(result.Value.Findings, finding =>
        {
            Assert.InRange(finding.Confidence, 0, 1);
            Assert.NotEqual("semantic_truth", finding.Provenance);
            Assert.True(File.Exists(finding.CropPath), finding.CropPath);
            Assert.True(File.Exists(finding.AnnotatedCropPath), finding.AnnotatedCropPath);
        });
        Assert.NotEmpty(result.Value.RawRegions);
        Assert.All(result.Value.RawRegions, region =>
        {
            Assert.Equal("pixel_diff_connected_component", region.Metadata["provenance"]);
            Assert.True(File.Exists(region.CropPath), region.CropPath);
            Assert.True(File.Exists(region.AnnotatedCropPath), region.AnnotatedCropPath);
        });
        Assert.Contains(result.Value.AgentReview.ArtifactPaths, artifact => artifact.Kind == "semantic_annotation");
        Assert.Contains(result.Value.AgentReview.ArtifactPaths, artifact => artifact.Kind == "raw_diff");
    }

    public void Dispose()
    {
        DeleteDirectoryWithRetry(_testRoot);
    }

    private static void DeleteDirectoryWithRetry(string path)
    {
        for (var attempt = 0; attempt < 30; attempt++)
        {
            if (!Directory.Exists(path))
            {
                return;
            }

            try
            {
                Directory.Delete(path, recursive: true);
                return;
            }
            catch (IOException)
            {
                Thread.Sleep(100);
            }
            catch (UnauthorizedAccessException)
            {
                Thread.Sleep(100);
            }
        }
    }

    private static void WriteFixture(string path, bool shifted, bool border)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using var bitmap = new SKBitmap(120, 80);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.White);
        using var contentPaint = new SKPaint { Color = SKColors.Black, Style = SKPaintStyle.Fill };
        canvas.DrawRect(new SKRect(shifted ? 36 : 30, 25, shifted ? 76 : 70, 45), contentPaint);

        if (border)
        {
            using var borderPaint = new SKPaint { Color = SKColors.Red, StrokeWidth = 1, Style = SKPaintStyle.Stroke };
            canvas.DrawLine(100, 10, 100, 70, borderPaint);
        }

        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        using var stream = File.Create(path);
        data.SaveTo(stream);
    }
}
