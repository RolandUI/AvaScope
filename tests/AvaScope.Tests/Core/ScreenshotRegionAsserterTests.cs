using AvaScope.Core;
using AvaScope.Protocol;
using SkiaSharp;

namespace AvaScope.Tests.Core;

public sealed class ScreenshotRegionAsserterTests : IDisposable
{
    private readonly string _testRoot = Path.Combine(
        Path.GetTempPath(),
        "AvaScope.Tests",
        $"regions-{Guid.NewGuid():N}");

    public void Dispose()
    {
        for (var attempt = 0; attempt < 10; attempt++)
        {
            try
            {
                if (Directory.Exists(_testRoot))
                {
                    Directory.Delete(_testRoot, recursive: true);
                }

                return;
            }
            catch (IOException) when (attempt < 9)
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
                Thread.Sleep(100);
            }
        }
    }

    [Fact]
    public void AssertDetectsNonEmptyRegionAndWritesCrop()
    {
        Directory.CreateDirectory(_testRoot);
        var imagePath = Path.Combine(_testRoot, "current.png");
        var cropPath = Path.Combine(_testRoot, "crop.png");
        WriteBitmap(imagePath, SKColors.White, (2, 2, SKColors.Black));

        var result = new ScreenshotRegionAsserter().Assert(
            imagePath,
            new ScreenshotRegion(0, 0, 4, 4),
            ScreenshotRegionAssertionModes.NonEmpty,
            cropPath: cropPath);

        Assert.True(result.Success, result.Error?.Message);
        Assert.True(result.Value!.Passed);
        Assert.Equal(1, result.Value.NonBlankPixels);
        Assert.Equal(Path.GetFullPath(cropPath), result.Value.CropPath);
        Assert.True(File.Exists(cropPath));
    }

    [Fact]
    public void AssertChangedComparesOnlyRequestedRegion()
    {
        Directory.CreateDirectory(_testRoot);
        var baselinePath = Path.Combine(_testRoot, "baseline.png");
        var currentPath = Path.Combine(_testRoot, "current.png");
        WriteBitmap(baselinePath, SKColors.White);
        WriteBitmap(currentPath, SKColors.White, (3, 3, SKColors.Black));

        var changed = new ScreenshotRegionAsserter().Assert(
            currentPath,
            new ScreenshotRegion(0, 0, 5, 5),
            ScreenshotRegionAssertionModes.Changed,
            baselinePath,
            minChangedPixels: 1);
        var unchanged = new ScreenshotRegionAsserter().Assert(
            currentPath,
            new ScreenshotRegion(0, 0, 2, 2),
            ScreenshotRegionAssertionModes.Unchanged,
            baselinePath);

        Assert.True(changed.Success, changed.Error?.Message);
        Assert.True(changed.Value!.Passed);
        Assert.Equal(1, changed.Value.ChangedPixels);
        Assert.True(unchanged.Success, unchanged.Error?.Message);
        Assert.True(unchanged.Value!.Passed);
        Assert.Equal(0, unchanged.Value.ChangedPixels);
    }

    [Fact]
    public void AssertRejectsRegionOutsideImage()
    {
        Directory.CreateDirectory(_testRoot);
        var imagePath = Path.Combine(_testRoot, "current.png");
        WriteBitmap(imagePath, SKColors.White);

        var result = new ScreenshotRegionAsserter().Assert(
            imagePath,
            new ScreenshotRegion(9, 0, 2, 2),
            ScreenshotRegionAssertionModes.NonEmpty);

        Assert.False(result.Success);
        Assert.Equal(CoreErrorCodes.ImageRegionAssertionFailed, result.Error!.Code);
        Assert.Equal("10", result.Error.Details!["imageWidth"]);
    }

    private static void WriteBitmap(string path, SKColor background, params (int X, int Y, SKColor Color)[] pixels)
    {
        using var bitmap = new SKBitmap(10, 10);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(background);
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
