using System.Text.Json.Serialization;

namespace AvaScope.Protocol;

public sealed record PreviewBaselineRegionCheckResult
{
    [JsonConstructor]
    public PreviewBaselineRegionCheckResult(
        int ruleIndex,
        ScreenshotRegion region,
        string assertion,
        ToolResult<ScreenshotRegionAssertionResponse> result)
    {
        if (ruleIndex < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(ruleIndex), ruleIndex, "Region rule index cannot be negative.");
        }

        if (string.IsNullOrWhiteSpace(assertion))
        {
            throw new ArgumentException("Region assertion cannot be empty.", nameof(assertion));
        }

        RuleIndex = ruleIndex;
        Region = region ?? throw new ArgumentNullException(nameof(region));
        Assertion = assertion;
        Result = result ?? throw new ArgumentNullException(nameof(result));
    }

    [JsonPropertyName("ruleIndex")]
    public int RuleIndex { get; }

    [JsonPropertyName("region")]
    public ScreenshotRegion Region { get; }

    [JsonPropertyName("assertion")]
    public string Assertion { get; }

    [JsonPropertyName("result")]
    public ToolResult<ScreenshotRegionAssertionResponse> Result { get; }
}
