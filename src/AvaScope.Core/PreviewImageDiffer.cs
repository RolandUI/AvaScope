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
        double tolerance = 0)
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
            long changedPixels = 0;
            var maxDelta = 0;
            var totalPixels = (long)baseline.Width * baseline.Height;

            for (var y = 0; y < baseline.Height; y++)
            {
                for (var x = 0; x < baseline.Width; x++)
                {
                    var baselineColor = baseline.GetPixel(x, y);
                    var currentColor = current.GetPixel(x, y);
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
                changedPixels == 0,
                baseline.Width,
                baseline.Height,
                tolerance,
                changedPixels,
                totalPixels,
                totalPixels == 0 ? 0 : (double)changedPixels / totalPixels * 100,
                maxDelta,
                fullDiffPath));
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
}
