using AvaScope.Protocol;
using SkiaSharp;
using System.Globalization;

namespace AvaScope.Core;

public sealed class PreviewImageDiffer
{
    public CoreResult<PreviewDiffResponse> Compare(
        string baselinePath,
        string currentPath,
        string? diffPath = null,
        double tolerance = 0,
        IReadOnlyList<ScreenshotRegion>? ignoredRegions = null,
        long? maxChangedPixels = null,
        double? maxChangedPercent = null)
    {
        if (string.IsNullOrWhiteSpace(baselinePath))
        {
            return CoreResult<PreviewDiffResponse>.Fail(new CoreError(
                CoreErrorCodes.ImageDiffFailed,
                "Baseline image path is required."));
        }

        if (string.IsNullOrWhiteSpace(currentPath))
        {
            return CoreResult<PreviewDiffResponse>.Fail(new CoreError(
                CoreErrorCodes.ImageDiffFailed,
                "Current image path is required."));
        }

        if (tolerance < 0 || tolerance > 255)
        {
            return CoreResult<PreviewDiffResponse>.Fail(new CoreError(
                CoreErrorCodes.ImageDiffFailed,
                "Tolerance must be between 0 and 255."));
        }

        if (maxChangedPixels is < 0)
        {
            return CoreResult<PreviewDiffResponse>.Fail(new CoreError(
                CoreErrorCodes.ImageDiffFailed,
                "Maximum changed pixels cannot be negative."));
        }

        if (maxChangedPercent is < 0 or > 100)
        {
            return CoreResult<PreviewDiffResponse>.Fail(new CoreError(
                CoreErrorCodes.ImageDiffFailed,
                "Maximum changed percent must be between 0 and 100."));
        }

        var fullBaselinePath = Path.GetFullPath(baselinePath);
        var fullCurrentPath = Path.GetFullPath(currentPath);
        var fullDiffPath = string.IsNullOrWhiteSpace(diffPath) ? null : Path.GetFullPath(diffPath);

        try
        {
            using var baseline = SKBitmap.Decode(fullBaselinePath);
            using var current = SKBitmap.Decode(fullCurrentPath);
            if (baseline is null)
            {
                return CoreResult<PreviewDiffResponse>.Fail(new CoreError(
                    CoreErrorCodes.ImageDiffFailed,
                    $"Baseline image '{fullBaselinePath}' could not be decoded."));
            }

            if (current is null)
            {
                return CoreResult<PreviewDiffResponse>.Fail(new CoreError(
                    CoreErrorCodes.ImageDiffFailed,
                    $"Current image '{fullCurrentPath}' could not be decoded."));
            }

            if (baseline.Width != current.Width || baseline.Height != current.Height)
            {
                return CoreResult<PreviewDiffResponse>.Fail(new CoreError(
                    CoreErrorCodes.ImageDiffDimensionMismatch,
                    "Baseline and current image dimensions differ.",
                    new Dictionary<string, string>
                    {
                        ["baselineWidth"] = baseline.Width.ToString(CultureInfo.InvariantCulture),
                        ["baselineHeight"] = baseline.Height.ToString(CultureInfo.InvariantCulture),
                        ["currentWidth"] = current.Width.ToString(CultureInfo.InvariantCulture),
                        ["currentHeight"] = current.Height.ToString(CultureInfo.InvariantCulture)
                    }));
            }

            using var diff = fullDiffPath is null ? null : new SKBitmap(baseline.Width, baseline.Height);
            var maskResult = CreateIgnoredRegionMask(ignoredRegions, baseline.Width, baseline.Height);
            if (!maskResult.Success)
            {
                return CoreResult<PreviewDiffResponse>.Fail(maskResult.Error!);
            }

            var ignoredMask = maskResult.Value!.Mask;
            var ignoredPixelCount = maskResult.Value.IgnoredPixelCount;
            long changedPixels = 0;
            var maxDelta = 0;
            var totalPixels = (long)baseline.Width * baseline.Height - ignoredPixelCount;
            if (totalPixels < 1)
            {
                return CoreResult<PreviewDiffResponse>.Fail(new CoreError(
                    CoreErrorCodes.ImageDiffFailed,
                    "Ignored regions cover the entire image.",
                    new Dictionary<string, string>
                    {
                        ["ignoredPixelCount"] = ignoredPixelCount.ToString(CultureInfo.InvariantCulture),
                        ["pixelWidth"] = baseline.Width.ToString(CultureInfo.InvariantCulture),
                        ["pixelHeight"] = baseline.Height.ToString(CultureInfo.InvariantCulture)
                    }));
            }

            for (var y = 0; y < baseline.Height; y++)
            {
                for (var x = 0; x < baseline.Width; x++)
                {
                    var offset = y * baseline.Width + x;
                    var baselineColor = baseline.GetPixel(x, y);
                    var currentColor = current.GetPixel(x, y);
                    if (ignoredMask is not null && ignoredMask[offset])
                    {
                        diff?.SetPixel(x, y, ToGray(currentColor));
                        continue;
                    }

                    var delta = MaxChannelDelta(baselineColor, currentColor);
                    maxDelta = Math.Max(maxDelta, delta);
                    var changed = delta > tolerance;
                    if (changed)
                    {
                        changedPixels++;
                    }

                    diff?.SetPixel(x, y, changed ? SKColors.Red : ToGray(currentColor));
                }
            }

            var changedPercent = totalPixels == 0 ? 0 : (double)changedPixels / totalPixels * 100;
            var passed = maxChangedPixels is null && maxChangedPercent is null
                ? changedPixels == 0
                : (maxChangedPixels is null || changedPixels <= maxChangedPixels.Value)
                    && (maxChangedPercent is null || changedPercent <= maxChangedPercent.Value);

            if (diff is not null && fullDiffPath is not null)
            {
                var directory = Path.GetDirectoryName(fullDiffPath);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                using var image = SKImage.FromBitmap(diff);
                using var data = image.Encode(SKEncodedImageFormat.Png, 100);
                using var stream = File.Create(fullDiffPath);
                data.SaveTo(stream);
            }

            return CoreResult<PreviewDiffResponse>.Ok(new PreviewDiffResponse(
                fullBaselinePath,
                fullCurrentPath,
                passed,
                baseline.Width,
                baseline.Height,
                tolerance,
                changedPixels,
                totalPixels,
                changedPercent,
                maxDelta,
                fullDiffPath,
                ignoredRegions,
                ignoredPixelCount,
                maxChangedPixels,
                maxChangedPercent));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            return CoreResult<PreviewDiffResponse>.Fail(new CoreError(
                CoreErrorCodes.ImageDiffFailed,
                exception.Message));
        }
    }

    private static int MaxChannelDelta(SKColor first, SKColor second)
    {
        return Math.Max(
            Math.Max(Math.Abs(first.Red - second.Red), Math.Abs(first.Green - second.Green)),
            Math.Max(Math.Abs(first.Blue - second.Blue), Math.Abs(first.Alpha - second.Alpha)));
    }

    private static SKColor ToGray(SKColor color)
    {
        var gray = (byte)((color.Red + color.Green + color.Blue) / 3);
        return new SKColor(gray, gray, gray, color.Alpha);
    }

    private static CoreResult<IgnoredRegionMask> CreateIgnoredRegionMask(
        IReadOnlyList<ScreenshotRegion>? ignoredRegions,
        int width,
        int height)
    {
        if (ignoredRegions is null || ignoredRegions.Count == 0)
        {
            return CoreResult<IgnoredRegionMask>.Ok(new IgnoredRegionMask(null, 0));
        }

        var mask = new bool[width * height];
        long ignoredPixels = 0;
        for (var regionIndex = 0; regionIndex < ignoredRegions.Count; regionIndex++)
        {
            var region = ignoredRegions[regionIndex];
            if (!IsRegionInside(region, width, height))
            {
                return CoreResult<IgnoredRegionMask>.Fail(new CoreError(
                    CoreErrorCodes.ImageDiffFailed,
                    "Ignored region is outside the image bounds.",
                    new Dictionary<string, string>
                    {
                        ["regionIndex"] = regionIndex.ToString(CultureInfo.InvariantCulture),
                        ["region"] = FormatRegion(region),
                        ["imageWidth"] = width.ToString(CultureInfo.InvariantCulture),
                        ["imageHeight"] = height.ToString(CultureInfo.InvariantCulture),
                        ["nextAction"] = "Adjust the ignored region so it fits inside the rendered baseline image."
                    }));
            }

            for (var y = region.Y; y < region.Y + region.Height; y++)
            {
                for (var x = region.X; x < region.X + region.Width; x++)
                {
                    var offset = y * width + x;
                    if (mask[offset])
                    {
                        continue;
                    }

                    mask[offset] = true;
                    ignoredPixels++;
                }
            }
        }

        return CoreResult<IgnoredRegionMask>.Ok(new IgnoredRegionMask(mask, ignoredPixels));
    }

    private static bool IsRegionInside(ScreenshotRegion region, int width, int height)
    {
        return region.X < width
            && region.Y < height
            && region.X + region.Width <= width
            && region.Y + region.Height <= height;
    }

    private static string FormatRegion(ScreenshotRegion region)
    {
        return string.Create(
            CultureInfo.InvariantCulture,
            $"{region.X},{region.Y},{region.Width},{region.Height}");
    }

    private readonly record struct IgnoredRegionMask(bool[]? Mask, long IgnoredPixelCount);
}
