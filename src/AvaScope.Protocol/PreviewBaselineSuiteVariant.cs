using System.Text.Json.Serialization;

namespace AvaScope.Protocol;

public sealed record PreviewBaselineSuiteVariant
{
    [JsonConstructor]
    public PreviewBaselineSuiteVariant(
        string? name = null,
        PreviewViewport? size = null,
        double? dpi = null,
        string? themeVariant = null,
        string? culture = null,
        string? designDataType = null,
        int? animationTimeOffsetMs = null,
        RuntimeTargetContext? runtimeTarget = null,
        IReadOnlyList<string>? mutationPresetIds = null,
        PreviewComparisonRules? comparisonRules = null)
    {
        if (dpi is <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(dpi), dpi, "DPI must be positive.");
        }

        if (animationTimeOffsetMs is < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(animationTimeOffsetMs), animationTimeOffsetMs, "Animation time offset must be zero or greater.");
        }

        Name = string.IsNullOrWhiteSpace(name) ? null : name;
        Size = size;
        Dpi = dpi;
        ThemeVariant = string.IsNullOrWhiteSpace(themeVariant) ? null : themeVariant;
        Culture = string.IsNullOrWhiteSpace(culture) ? null : culture;
        DesignDataType = string.IsNullOrWhiteSpace(designDataType) ? null : designDataType;
        AnimationTimeOffsetMs = animationTimeOffsetMs;
        RuntimeTarget = runtimeTarget;
        MutationPresetIds = PreviewBaselineSuiteDefaults.Normalize(mutationPresetIds);
        ComparisonRules = comparisonRules;
    }

    [JsonPropertyName("name")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Name { get; }

    [JsonPropertyName("size")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public PreviewViewport? Size { get; }

    [JsonPropertyName("dpi")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? Dpi { get; }

    [JsonPropertyName("themeVariant")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ThemeVariant { get; }

    [JsonPropertyName("culture")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Culture { get; }

    [JsonPropertyName("designDataType")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? DesignDataType { get; }

    [JsonPropertyName("animationTimeOffsetMs")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? AnimationTimeOffsetMs { get; }

    [JsonPropertyName("runtimeTarget")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public RuntimeTargetContext? RuntimeTarget { get; }

    [JsonPropertyName("mutationPresetIds")]
    public IReadOnlyList<string> MutationPresetIds { get; }

    [JsonPropertyName("comparisonRules")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public PreviewComparisonRules? ComparisonRules { get; }
}
