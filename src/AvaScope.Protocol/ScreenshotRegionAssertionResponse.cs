using System.Text.Json.Serialization;

namespace AvaScope.Protocol;

public sealed record ScreenshotRegionAssertionResponse
{
    [JsonConstructor]
    public ScreenshotRegionAssertionResponse(
        string imagePath,
        ScreenshotRegion region,
        string assertion,
        bool passed,
        int pixelWidth,
        int pixelHeight,
        long totalPixels,
        long nonBlankPixels,
        double nonBlankPercent,
        long changedPixels = 0,
        double changedPercent = 0,
        int maxDelta = 0,
        double tolerance = 0,
        string? baselinePath = null,
        string? cropPath = null)
    {
        if (string.IsNullOrWhiteSpace(imagePath))
        {
            throw new ArgumentException("Image path cannot be empty.", nameof(imagePath));
        }

        if (string.IsNullOrWhiteSpace(assertion))
        {
            throw new ArgumentException("Assertion cannot be empty.", nameof(assertion));
        }

        if (pixelWidth < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(pixelWidth), pixelWidth, "Pixel width must be positive.");
        }

        if (pixelHeight < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(pixelHeight), pixelHeight, "Pixel height must be positive.");
        }

        if (totalPixels < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(totalPixels), totalPixels, "Total pixels must be positive.");
        }

        ImagePath = Path.GetFullPath(imagePath);
        BaselinePath = string.IsNullOrWhiteSpace(baselinePath) ? null : Path.GetFullPath(baselinePath);
        CropPath = string.IsNullOrWhiteSpace(cropPath) ? null : Path.GetFullPath(cropPath);
        Region = region ?? throw new ArgumentNullException(nameof(region));
        Assertion = assertion;
        Passed = passed;
        PixelWidth = pixelWidth;
        PixelHeight = pixelHeight;
        TotalPixels = totalPixels;
        NonBlankPixels = nonBlankPixels;
        NonBlankPercent = nonBlankPercent;
        ChangedPixels = changedPixels;
        ChangedPercent = changedPercent;
        MaxDelta = maxDelta;
        Tolerance = tolerance;
    }

    [JsonPropertyName("imagePath")]
    public string ImagePath { get; }

    [JsonPropertyName("baselinePath")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? BaselinePath { get; }

    [JsonPropertyName("cropPath")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? CropPath { get; }

    [JsonPropertyName("region")]
    public ScreenshotRegion Region { get; }

    [JsonPropertyName("assertion")]
    public string Assertion { get; }

    [JsonPropertyName("passed")]
    public bool Passed { get; }

    [JsonPropertyName("pixelWidth")]
    public int PixelWidth { get; }

    [JsonPropertyName("pixelHeight")]
    public int PixelHeight { get; }

    [JsonPropertyName("totalPixels")]
    public long TotalPixels { get; }

    [JsonPropertyName("nonBlankPixels")]
    public long NonBlankPixels { get; }

    [JsonPropertyName("nonBlankPercent")]
    public double NonBlankPercent { get; }

    [JsonPropertyName("changedPixels")]
    public long ChangedPixels { get; }

    [JsonPropertyName("changedPercent")]
    public double ChangedPercent { get; }

    [JsonPropertyName("maxDelta")]
    public int MaxDelta { get; }

    [JsonPropertyName("tolerance")]
    public double Tolerance { get; }
}
