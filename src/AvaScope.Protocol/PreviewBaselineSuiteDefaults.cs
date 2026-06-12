using System.Text.Json.Serialization;

namespace AvaScope.Protocol;

public sealed record PreviewBaselineSuiteDefaults
{
    [JsonConstructor]
    public PreviewBaselineSuiteDefaults(
        IReadOnlyList<PreviewViewport>? sizes = null,
        IReadOnlyList<double>? dpis = null,
        IReadOnlyList<string>? themes = null,
        IReadOnlyList<string>? cultures = null,
        IReadOnlyList<string>? designDataTypes = null,
        IReadOnlyList<int>? animationFramesMs = null,
        IReadOnlyList<string>? mutationPresetIds = null,
        PreviewComparisonRules? comparisonRules = null)
    {
        ValidateDpis(dpis);
        ValidateAnimationFrames(animationFramesMs);

        Sizes = sizes ?? [];
        Dpis = dpis ?? [];
        Themes = Normalize(themes);
        Cultures = Normalize(cultures);
        DesignDataTypes = Normalize(designDataTypes);
        AnimationFramesMs = animationFramesMs ?? [];
        MutationPresetIds = Normalize(mutationPresetIds);
        ComparisonRules = comparisonRules;
    }

    [JsonPropertyName("sizes")]
    public IReadOnlyList<PreviewViewport> Sizes { get; }

    [JsonPropertyName("dpis")]
    public IReadOnlyList<double> Dpis { get; }

    [JsonPropertyName("themes")]
    public IReadOnlyList<string> Themes { get; }

    [JsonPropertyName("cultures")]
    public IReadOnlyList<string> Cultures { get; }

    [JsonPropertyName("designDataTypes")]
    public IReadOnlyList<string> DesignDataTypes { get; }

    [JsonPropertyName("animationFramesMs")]
    public IReadOnlyList<int> AnimationFramesMs { get; }

    [JsonPropertyName("mutationPresetIds")]
    public IReadOnlyList<string> MutationPresetIds { get; }

    [JsonPropertyName("comparisonRules")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public PreviewComparisonRules? ComparisonRules { get; }

    internal static IReadOnlyList<string> Normalize(IReadOnlyList<string>? values)
    {
        return values?
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .ToArray()
            ?? [];
    }

    internal static void ValidateDpis(IReadOnlyList<double>? dpis)
    {
        if (dpis is null)
        {
            return;
        }

        foreach (var dpi in dpis)
        {
            if (dpi <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(dpis), dpi, "DPI values must be positive.");
            }
        }
    }

    internal static void ValidateAnimationFrames(IReadOnlyList<int>? animationFramesMs)
    {
        if (animationFramesMs is null)
        {
            return;
        }

        foreach (var frame in animationFramesMs)
        {
            if (frame < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(animationFramesMs), frame, "Animation frame offsets must be zero or greater.");
            }
        }
    }
}
