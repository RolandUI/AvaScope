using System.Globalization;
using AvaScope.Protocol;
using SkiaSharp;

namespace AvaScope.Core;

public sealed class ScreenshotRegionAsserter
{
    public CoreResult<ScreenshotRegionAssertionResponse> Assert(
        string imagePath,
        ScreenshotRegion region,
        string assertion,
        string? baselinePath = null,
        string? cropPath = null,
        double tolerance = 0,
        long? minChangedPixels = null,
        double mostlyBlankMaxNonBlankPercent = 1)
    {
        if (string.IsNullOrWhiteSpace(imagePath))
        {
            return Fail("Image path is required.");
        }

        ArgumentNullException.ThrowIfNull(region);

        if (string.IsNullOrWhiteSpace(assertion))
        {
            return Fail("Region assertion is required.");
        }

        if (tolerance < 0 || tolerance > 255)
        {
            return Fail("Tolerance must be between 0 and 255.");
        }

        if (mostlyBlankMaxNonBlankPercent is < 0 or > 100)
        {
            return Fail("mostlyBlankMaxNonBlankPercent must be between 0 and 100.");
        }

        var normalizedAssertion = NormalizeAssertion(assertion);
        if (normalizedAssertion is null)
        {
            return Fail($"Region assertion '{assertion}' is not supported.");
        }

        var fullImagePath = Path.GetFullPath(imagePath);
        var fullBaselinePath = string.IsNullOrWhiteSpace(baselinePath) ? null : Path.GetFullPath(baselinePath);
        var fullCropPath = string.IsNullOrWhiteSpace(cropPath) ? null : Path.GetFullPath(cropPath);

        try
        {
            using var image = DecodeBitmap(fullImagePath);
            if (image is null)
            {
                return Fail($"Image '{fullImagePath}' could not be decoded.");
            }

            if (!IsRegionInside(region, image.Width, image.Height))
            {
                return Fail(
                    "Region is outside the image bounds.",
                    new Dictionary<string, string>
                    {
                        ["imageWidth"] = image.Width.ToString(CultureInfo.InvariantCulture),
                        ["imageHeight"] = image.Height.ToString(CultureInfo.InvariantCulture),
                        ["region"] = FormatRegion(region)
                    });
            }

            var totalPixels = (long)region.Width * region.Height;
            var nonBlankPixels = CountNonBlankPixels(image, region, tolerance);
            var nonBlankPercent = nonBlankPixels * 100d / totalPixels;
            var changedPixels = 0L;
            var changedPercent = 0d;
            var maxDelta = 0;

            if (normalizedAssertion is ScreenshotRegionAssertionModes.Changed or ScreenshotRegionAssertionModes.Unchanged)
            {
                if (fullBaselinePath is null)
                {
                    return Fail($"{normalizedAssertion} requires a baseline image path.");
                }

                using var baseline = DecodeBitmap(fullBaselinePath);
                if (baseline is null)
                {
                    return Fail($"Baseline image '{fullBaselinePath}' could not be decoded.");
                }

                if (baseline.Width != image.Width || baseline.Height != image.Height)
                {
                    return CoreResult<ScreenshotRegionAssertionResponse>.Fail(new CoreError(
                        CoreErrorCodes.ImageDiffDimensionMismatch,
                        "Baseline and image dimensions differ.",
                        new Dictionary<string, string>
                        {
                            ["baselineWidth"] = baseline.Width.ToString(CultureInfo.InvariantCulture),
                            ["baselineHeight"] = baseline.Height.ToString(CultureInfo.InvariantCulture),
                            ["imageWidth"] = image.Width.ToString(CultureInfo.InvariantCulture),
                            ["imageHeight"] = image.Height.ToString(CultureInfo.InvariantCulture)
                        }));
                }

                var comparison = CompareRegion(baseline, image, region, tolerance);
                changedPixels = comparison.ChangedPixels;
                changedPercent = changedPixels * 100d / totalPixels;
                maxDelta = comparison.MaxDelta;
            }

            if (fullCropPath is not null)
            {
                SaveCrop(image, region, fullCropPath);
            }

            var passed = normalizedAssertion switch
            {
                ScreenshotRegionAssertionModes.NonEmpty => nonBlankPixels > 0,
                ScreenshotRegionAssertionModes.MostlyBlank => nonBlankPercent <= mostlyBlankMaxNonBlankPercent,
                ScreenshotRegionAssertionModes.Changed => changedPixels >= Math.Max(1, minChangedPixels ?? 1),
                ScreenshotRegionAssertionModes.Unchanged => changedPixels == 0,
                _ => false
            };

            return CoreResult<ScreenshotRegionAssertionResponse>.Ok(new ScreenshotRegionAssertionResponse(
                fullImagePath,
                region,
                normalizedAssertion,
                passed,
                image.Width,
                image.Height,
                totalPixels,
                nonBlankPixels,
                nonBlankPercent,
                changedPixels,
                changedPercent,
                maxDelta,
                tolerance,
                fullBaselinePath,
                fullCropPath));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            return Fail(exception.Message);
        }
    }

    private static string? NormalizeAssertion(string assertion)
    {
        foreach (var supported in SupportedAssertions)
        {
            if (string.Equals(assertion, supported, StringComparison.OrdinalIgnoreCase))
            {
                return supported;
            }
        }

        return null;
    }

    private static bool IsRegionInside(ScreenshotRegion region, int width, int height)
    {
        return region.X < width
            && region.Y < height
            && region.X + region.Width <= width
            && region.Y + region.Height <= height;
    }

    private static long CountNonBlankPixels(SKBitmap image, ScreenshotRegion region, double tolerance)
    {
        long count = 0;
        for (var y = region.Y; y < region.Y + region.Height; y++)
        {
            for (var x = region.X; x < region.X + region.Width; x++)
            {
                if (!IsBlank(image.GetPixel(x, y), tolerance))
                {
                    count++;
                }
            }
        }

        return count;
    }

    private static BitmapComparison CompareRegion(
        SKBitmap baseline,
        SKBitmap image,
        ScreenshotRegion region,
        double tolerance)
    {
        long changedPixels = 0;
        var maxDelta = 0;

        for (var y = region.Y; y < region.Y + region.Height; y++)
        {
            for (var x = region.X; x < region.X + region.Width; x++)
            {
                var delta = MaxChannelDelta(baseline.GetPixel(x, y), image.GetPixel(x, y));
                if (delta <= tolerance)
                {
                    continue;
                }

                changedPixels++;
                maxDelta = Math.Max(maxDelta, delta);
            }
        }

        return new BitmapComparison(changedPixels, maxDelta);
    }

    private static bool IsBlank(SKColor color, double tolerance)
    {
        if (color.Alpha <= tolerance)
        {
            return true;
        }

        return Math.Abs(255 - color.Red) <= tolerance
            && Math.Abs(255 - color.Green) <= tolerance
            && Math.Abs(255 - color.Blue) <= tolerance;
    }

    private static int MaxChannelDelta(SKColor first, SKColor second)
    {
        return Math.Max(
            Math.Max(Math.Abs(first.Red - second.Red), Math.Abs(first.Green - second.Green)),
            Math.Max(Math.Abs(first.Blue - second.Blue), Math.Abs(first.Alpha - second.Alpha)));
    }

    private static SKBitmap? DecodeBitmap(string path)
    {
        using var stream = File.OpenRead(path);
        using var data = SKData.Create(stream);
        return SKBitmap.Decode(data);
    }

    private static void SaveCrop(SKBitmap image, ScreenshotRegion region, string cropPath)
    {
        var directory = Path.GetDirectoryName(cropPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        using var crop = new SKBitmap(region.Width, region.Height);
        using var canvas = new SKCanvas(crop);
        var source = new SKRectI(region.X, region.Y, region.X + region.Width, region.Y + region.Height);
        var destination = new SKRectI(0, 0, region.Width, region.Height);
        canvas.DrawBitmap(image, source, destination);
        using var cropImage = SKImage.FromBitmap(crop);
        using var encoded = cropImage.Encode(SKEncodedImageFormat.Png, 100);
        using var stream = File.Create(cropPath);
        encoded.SaveTo(stream);
    }

    private static string FormatRegion(ScreenshotRegion region)
    {
        return string.Create(
            CultureInfo.InvariantCulture,
            $"{region.X},{region.Y},{region.Width},{region.Height}");
    }

    private static CoreResult<ScreenshotRegionAssertionResponse> Fail(
        string message,
        IReadOnlyDictionary<string, string>? details = null)
    {
        return CoreResult<ScreenshotRegionAssertionResponse>.Fail(new CoreError(
            CoreErrorCodes.ImageRegionAssertionFailed,
            message,
            details));
    }

    private static readonly string[] SupportedAssertions =
    [
        ScreenshotRegionAssertionModes.NonEmpty,
        ScreenshotRegionAssertionModes.MostlyBlank,
        ScreenshotRegionAssertionModes.Changed,
        ScreenshotRegionAssertionModes.Unchanged
    ];

    private readonly record struct BitmapComparison(long ChangedPixels, int MaxDelta);
}
