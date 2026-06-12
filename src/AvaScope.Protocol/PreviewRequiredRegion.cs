using System.Text.Json.Serialization;

namespace AvaScope.Protocol;

public sealed record PreviewRequiredRegion
{
    [JsonConstructor]
    public PreviewRequiredRegion(
        ScreenshotRegion region,
        string assertion = ScreenshotRegionAssertionModes.Unchanged,
        long? minChangedPixels = null,
        double? mostlyBlankMaxNonBlankPercent = null)
    {
        if (string.IsNullOrWhiteSpace(assertion))
        {
            throw new ArgumentException("Required region assertion cannot be empty.", nameof(assertion));
        }

        if (minChangedPixels is <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(minChangedPixels), minChangedPixels, "Minimum changed pixels must be positive.");
        }

        if (mostlyBlankMaxNonBlankPercent is < 0 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(mostlyBlankMaxNonBlankPercent), mostlyBlankMaxNonBlankPercent, "Mostly-blank threshold must be between 0 and 100.");
        }

        Region = region ?? throw new ArgumentNullException(nameof(region));
        Assertion = assertion;
        MinChangedPixels = minChangedPixels;
        MostlyBlankMaxNonBlankPercent = mostlyBlankMaxNonBlankPercent;
    }

    [JsonPropertyName("region")]
    public ScreenshotRegion Region { get; }

    [JsonPropertyName("assertion")]
    public string Assertion { get; }

    [JsonPropertyName("minChangedPixels")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public long? MinChangedPixels { get; }

    [JsonPropertyName("mostlyBlankMaxNonBlankPercent")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? MostlyBlankMaxNonBlankPercent { get; }
}
