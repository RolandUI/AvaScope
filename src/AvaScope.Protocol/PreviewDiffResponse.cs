using System.Text.Json.Serialization;

namespace AvaScope.Protocol;

public sealed record PreviewDiffResponse
{
    [JsonConstructor]
    public PreviewDiffResponse(
        string baselinePath,
        string currentPath,
        bool passed,
        int pixelWidth,
        int pixelHeight,
        double tolerance,
        long changedPixels,
        long totalPixels,
        double changedPercent,
        int maxDelta,
        string? diffPath = null,
        IReadOnlyList<ScreenshotRegion>? ignoredRegions = null,
        long ignoredPixelCount = 0,
        long? maxChangedPixels = null,
        double? maxChangedPercent = null)
    {
        if (string.IsNullOrWhiteSpace(baselinePath))
        {
            throw new ArgumentException("Baseline path cannot be empty.", nameof(baselinePath));
        }

        if (string.IsNullOrWhiteSpace(currentPath))
        {
            throw new ArgumentException("Current path cannot be empty.", nameof(currentPath));
        }

        if (pixelWidth < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(pixelWidth), pixelWidth, "Pixel width must be positive.");
        }

        if (pixelHeight < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(pixelHeight), pixelHeight, "Pixel height must be positive.");
        }

        if (tolerance < 0 || tolerance > 255)
        {
            throw new ArgumentOutOfRangeException(nameof(tolerance), tolerance, "Tolerance must be between 0 and 255.");
        }

        if (changedPixels < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(changedPixels), changedPixels, "Changed pixels cannot be negative.");
        }

        if (totalPixels < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(totalPixels), totalPixels, "Total pixels must be positive.");
        }

        if (maxDelta < 0 || maxDelta > 255)
        {
            throw new ArgumentOutOfRangeException(nameof(maxDelta), maxDelta, "Max delta must be between 0 and 255.");
        }

        if (ignoredPixelCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(ignoredPixelCount), ignoredPixelCount, "Ignored pixel count cannot be negative.");
        }

        if (maxChangedPixels is < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxChangedPixels), maxChangedPixels, "Maximum changed pixels cannot be negative.");
        }

        if (maxChangedPercent is < 0 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(maxChangedPercent), maxChangedPercent, "Maximum changed percent must be between 0 and 100.");
        }

        BaselinePath = Path.GetFullPath(baselinePath);
        CurrentPath = Path.GetFullPath(currentPath);
        DiffPath = string.IsNullOrWhiteSpace(diffPath) ? null : Path.GetFullPath(diffPath);
        Passed = passed;
        PixelWidth = pixelWidth;
        PixelHeight = pixelHeight;
        Tolerance = tolerance;
        ChangedPixels = changedPixels;
        TotalPixels = totalPixels;
        ChangedPercent = changedPercent;
        MaxDelta = maxDelta;
        IgnoredRegions = ignoredRegions ?? [];
        IgnoredPixelCount = ignoredPixelCount;
        MaxChangedPixels = maxChangedPixels;
        MaxChangedPercent = maxChangedPercent;
    }

    [JsonPropertyName("baselinePath")]
    public string BaselinePath { get; }

    [JsonPropertyName("currentPath")]
    public string CurrentPath { get; }

    [JsonPropertyName("diffPath")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? DiffPath { get; }

    [JsonPropertyName("passed")]
    public bool Passed { get; }

    [JsonPropertyName("pixelWidth")]
    public int PixelWidth { get; }

    [JsonPropertyName("pixelHeight")]
    public int PixelHeight { get; }

    [JsonPropertyName("tolerance")]
    public double Tolerance { get; }

    [JsonPropertyName("changedPixels")]
    public long ChangedPixels { get; }

    [JsonPropertyName("totalPixels")]
    public long TotalPixels { get; }

    [JsonPropertyName("changedPercent")]
    public double ChangedPercent { get; }

    [JsonPropertyName("maxDelta")]
    public int MaxDelta { get; }

    [JsonPropertyName("ignoredRegions")]
    public IReadOnlyList<ScreenshotRegion> IgnoredRegions { get; }

    [JsonPropertyName("ignoredPixelCount")]
    public long IgnoredPixelCount { get; }

    [JsonPropertyName("maxChangedPixels")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public long? MaxChangedPixels { get; }

    [JsonPropertyName("maxChangedPercent")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? MaxChangedPercent { get; }
}
