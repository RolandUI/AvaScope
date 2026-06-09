using System.Text.Json.Serialization;

namespace AvaScope.Protocol;

public sealed record PreviewAnimationMotionSummary
{
    [JsonConstructor]
    public PreviewAnimationMotionSummary(
        string status,
        int comparedFrameCount,
        long changedPixels,
        long totalPixels,
        double changedPercent,
        int maxDelta,
        IReadOnlyDictionary<string, string>? details = null)
    {
        if (string.IsNullOrWhiteSpace(status))
        {
            throw new ArgumentException("Motion status cannot be empty.", nameof(status));
        }

        if (comparedFrameCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(comparedFrameCount), comparedFrameCount, "Compared frame count cannot be negative.");
        }

        if (changedPixels < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(changedPixels), changedPixels, "Changed pixel count cannot be negative.");
        }

        if (totalPixels < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(totalPixels), totalPixels, "Total pixel count cannot be negative.");
        }

        if (changedPercent < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(changedPercent), changedPercent, "Changed percent cannot be negative.");
        }

        if (maxDelta < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxDelta), maxDelta, "Max delta cannot be negative.");
        }

        Status = status;
        ComparedFrameCount = comparedFrameCount;
        ChangedPixels = changedPixels;
        TotalPixels = totalPixels;
        ChangedPercent = changedPercent;
        MaxDelta = maxDelta;
        Details = details ?? new Dictionary<string, string>();
    }

    [JsonPropertyName("status")]
    public string Status { get; }

    [JsonPropertyName("comparedFrameCount")]
    public int ComparedFrameCount { get; }

    [JsonPropertyName("changedPixels")]
    public long ChangedPixels { get; }

    [JsonPropertyName("totalPixels")]
    public long TotalPixels { get; }

    [JsonPropertyName("changedPercent")]
    public double ChangedPercent { get; }

    [JsonPropertyName("maxDelta")]
    public int MaxDelta { get; }

    [JsonPropertyName("details")]
    public IReadOnlyDictionary<string, string> Details { get; }
}
