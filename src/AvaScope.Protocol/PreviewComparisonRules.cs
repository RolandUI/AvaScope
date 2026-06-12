using System.Text.Json.Serialization;

namespace AvaScope.Protocol;

public sealed record PreviewComparisonRules
{
    [JsonConstructor]
    public PreviewComparisonRules(
        double? tolerance = null,
        long? maxChangedPixels = null,
        double? maxChangedPercent = null,
        IReadOnlyList<ScreenshotRegion>? ignoredRegions = null,
        IReadOnlyList<PreviewRequiredRegion>? requiredRegions = null)
    {
        if (tolerance is < 0 or > 255)
        {
            throw new ArgumentOutOfRangeException(nameof(tolerance), tolerance, "Tolerance must be between 0 and 255.");
        }

        if (maxChangedPixels is < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxChangedPixels), maxChangedPixels, "Maximum changed pixels cannot be negative.");
        }

        if (maxChangedPercent is < 0 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(maxChangedPercent), maxChangedPercent, "Maximum changed percent must be between 0 and 100.");
        }

        Tolerance = tolerance;
        MaxChangedPixels = maxChangedPixels;
        MaxChangedPercent = maxChangedPercent;
        IgnoredRegions = ignoredRegions ?? [];
        RequiredRegions = requiredRegions ?? [];
    }

    [JsonPropertyName("tolerance")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? Tolerance { get; }

    [JsonPropertyName("maxChangedPixels")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public long? MaxChangedPixels { get; }

    [JsonPropertyName("maxChangedPercent")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? MaxChangedPercent { get; }

    [JsonPropertyName("ignoredRegions")]
    public IReadOnlyList<ScreenshotRegion> IgnoredRegions { get; }

    [JsonPropertyName("requiredRegions")]
    public IReadOnlyList<PreviewRequiredRegion> RequiredRegions { get; }
}
