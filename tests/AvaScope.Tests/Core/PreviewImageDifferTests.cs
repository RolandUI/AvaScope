using AvaScope.Core;
using AvaScope.Protocol;
using SkiaSharp;

namespace AvaScope.Tests.Core;

public sealed class PreviewImageDifferTests : IDisposable
{
    private readonly string _testRoot = Path.Combine(Path.GetTempPath(), "AvaScope.Tests", Guid.NewGuid().ToString("N"));

    public PreviewImageDifferTests()
    {
        Directory.CreateDirectory(_testRoot);
    }

    [Fact]
    public void CompareAppliesIgnoredRegionsAndThresholds()
    {
        var baselinePath = Path.Combine(_testRoot, "baseline.png");
        var currentPath = Path.Combine(_testRoot, "current.png");
        var diffPath = Path.Combine(_testRoot, "diff.png");
        WriteImage(baselinePath, [(0, 0, SKColors.White), (3, 3, SKColors.White)]);
        WriteImage(currentPath, [(0, 0, SKColors.Black), (3, 3, SKColors.Black)]);

        var strict = new PreviewImageDiffer().Compare(baselinePath, currentPath);
        Assert.True(strict.Success, strict.Error?.Message);
        Assert.False(strict.Value!.Passed);
        Assert.Equal(2, strict.Value.ChangedPixels);

        var withRules = new PreviewImageDiffer().Compare(
            baselinePath,
            currentPath,
            diffPath,
            ignoredRegions: [new ScreenshotRegion(0, 0, 1, 1, "clock")],
            maxChangedPixels: 1,
            maxChangedPercent: 10);

        Assert.True(withRules.Success, withRules.Error?.Message);
        Assert.True(withRules.Value!.Passed);
        Assert.Equal(1, withRules.Value.ChangedPixels);
        Assert.Equal(1, withRules.Value.IgnoredPixelCount);
        Assert.Equal(15, withRules.Value.TotalPixels);
        Assert.Equal(1, Assert.Single(withRules.Value.IgnoredRegions).Width);
        Assert.Equal(1, withRules.Value.MaxChangedPixels);
        Assert.Equal(10, withRules.Value.MaxChangedPercent);
        Assert.True(File.Exists(diffPath));
    }

    [Fact]
    public void CompareReportsOutOfBoundsIgnoredRegion()
    {
        var baselinePath = Path.Combine(_testRoot, "baseline.png");
        var currentPath = Path.Combine(_testRoot, "current.png");
        WriteImage(baselinePath);
        WriteImage(currentPath);

        var result = new PreviewImageDiffer().Compare(
            baselinePath,
            currentPath,
            ignoredRegions: [new ScreenshotRegion(3, 3, 2, 2, "outside")]);

        Assert.False(result.Success);
        Assert.Equal(CoreErrorCodes.ImageDiffFailed, result.Error!.Code);
        Assert.Contains("outside the image bounds", result.Error.Message, StringComparison.Ordinal);
        Assert.Equal("0", result.Error.Details!["regionIndex"]);
        Assert.Equal("3,3,2,2", result.Error.Details["region"]);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testRoot))
        {
            Directory.Delete(_testRoot, recursive: true);
        }
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
}
