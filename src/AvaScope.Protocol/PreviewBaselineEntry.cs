using System.Text.Json.Serialization;

namespace AvaScope.Protocol;

public sealed record PreviewBaselineEntry
{
    [JsonConstructor]
    public PreviewBaselineEntry(
        int index,
        PreviewViewport viewport,
        string imagePath,
        double dpi,
        string? projectPath = null,
        string? viewPath = null,
        string? themeVariant = null,
        string? culture = null,
        string? designDataType = null,
        string? suiteName = null,
        string? suiteEntryId = null,
        string? suiteVariantName = null,
        string? profileName = null,
        string? profileVariant = null,
        string? profileFilePath = null,
        RuntimeTargetContext? runtimeTarget = null,
        IReadOnlyList<string>? mutationPresetIds = null,
        int? animationTimeOffsetMs = null,
        PreviewComparisonRules? comparisonRules = null)
    {
        if (index < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(index), index, "Baseline entry index cannot be negative.");
        }

        if (string.IsNullOrWhiteSpace(imagePath))
        {
            throw new ArgumentException("Baseline image path cannot be empty.", nameof(imagePath));
        }

        if (dpi <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(dpi), dpi, "DPI must be positive.");
        }

        if (animationTimeOffsetMs is < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(animationTimeOffsetMs), animationTimeOffsetMs, "Animation time offset must be zero or greater.");
        }

        Index = index;
        Viewport = viewport ?? throw new ArgumentNullException(nameof(viewport));
        ImagePath = Path.GetFullPath(imagePath);
        Dpi = dpi;
        ProjectPath = string.IsNullOrWhiteSpace(projectPath) ? null : Path.GetFullPath(projectPath);
        ViewPath = string.IsNullOrWhiteSpace(viewPath) ? null : viewPath;
        ThemeVariant = string.IsNullOrWhiteSpace(themeVariant) ? null : themeVariant;
        Culture = string.IsNullOrWhiteSpace(culture) ? null : culture;
        DesignDataType = string.IsNullOrWhiteSpace(designDataType) ? null : designDataType;
        SuiteName = string.IsNullOrWhiteSpace(suiteName) ? null : suiteName;
        SuiteEntryId = string.IsNullOrWhiteSpace(suiteEntryId) ? null : suiteEntryId;
        SuiteVariantName = string.IsNullOrWhiteSpace(suiteVariantName) ? null : suiteVariantName;
        ProfileName = string.IsNullOrWhiteSpace(profileName) ? null : profileName;
        ProfileVariant = string.IsNullOrWhiteSpace(profileVariant) ? null : profileVariant;
        ProfileFilePath = string.IsNullOrWhiteSpace(profileFilePath) ? null : Path.GetFullPath(profileFilePath);
        RuntimeTarget = runtimeTarget;
        MutationPresetIds = PreviewBaselineSuiteDefaults.Normalize(mutationPresetIds);
        AnimationTimeOffsetMs = animationTimeOffsetMs;
        ComparisonRules = comparisonRules;
    }

    [JsonPropertyName("index")]
    public int Index { get; }

    [JsonPropertyName("viewport")]
    public PreviewViewport Viewport { get; }

    [JsonPropertyName("imagePath")]
    public string ImagePath { get; }

    [JsonPropertyName("dpi")]
    public double Dpi { get; }

    [JsonPropertyName("projectPath")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ProjectPath { get; }

    [JsonPropertyName("viewPath")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ViewPath { get; }

    [JsonPropertyName("themeVariant")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ThemeVariant { get; }

    [JsonPropertyName("culture")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Culture { get; }

    [JsonPropertyName("designDataType")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? DesignDataType { get; }

    [JsonPropertyName("suiteName")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? SuiteName { get; }

    [JsonPropertyName("suiteEntryId")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? SuiteEntryId { get; }

    [JsonPropertyName("suiteVariantName")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? SuiteVariantName { get; }

    [JsonPropertyName("profileName")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ProfileName { get; }

    [JsonPropertyName("profileVariant")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ProfileVariant { get; }

    [JsonPropertyName("profileFilePath")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ProfileFilePath { get; }

    [JsonPropertyName("runtimeTarget")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public RuntimeTargetContext? RuntimeTarget { get; }

    [JsonPropertyName("mutationPresetIds")]
    public IReadOnlyList<string> MutationPresetIds { get; }

    [JsonPropertyName("animationTimeOffsetMs")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? AnimationTimeOffsetMs { get; }

    [JsonPropertyName("comparisonRules")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public PreviewComparisonRules? ComparisonRules { get; }
}
