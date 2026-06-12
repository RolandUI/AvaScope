using System.Text.Json.Serialization;

namespace AvaScope.Protocol;

public sealed record PreviewBaselineSuiteExpansion
{
    [JsonConstructor]
    public PreviewBaselineSuiteExpansion(
        int index,
        string suiteName,
        string entryId,
        string variantName,
        PreviewViewport viewport,
        string imagePath,
        double dpi,
        string projectPath,
        string viewPath,
        string? themeVariant = null,
        string? culture = null,
        string? designDataType = null,
        string? profileName = null,
        string? profileVariant = null,
        string? profileFilePath = null,
        RuntimeTargetContext? runtimeTarget = null,
        IReadOnlyList<string>? mutationPresetIds = null,
        int? animationTimeOffsetMs = null)
    {
        if (index < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(index), index, "Suite expansion index cannot be negative.");
        }

        if (string.IsNullOrWhiteSpace(suiteName))
        {
            throw new ArgumentException("Suite name cannot be empty.", nameof(suiteName));
        }

        if (string.IsNullOrWhiteSpace(entryId))
        {
            throw new ArgumentException("Suite entry id cannot be empty.", nameof(entryId));
        }

        if (string.IsNullOrWhiteSpace(variantName))
        {
            throw new ArgumentException("Suite variant name cannot be empty.", nameof(variantName));
        }

        if (string.IsNullOrWhiteSpace(imagePath))
        {
            throw new ArgumentException("Suite expansion image path cannot be empty.", nameof(imagePath));
        }

        if (dpi <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(dpi), dpi, "DPI must be positive.");
        }

        if (string.IsNullOrWhiteSpace(projectPath))
        {
            throw new ArgumentException("Suite expansion project path cannot be empty.", nameof(projectPath));
        }

        if (string.IsNullOrWhiteSpace(viewPath))
        {
            throw new ArgumentException("Suite expansion view path cannot be empty.", nameof(viewPath));
        }

        if (animationTimeOffsetMs is < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(animationTimeOffsetMs), animationTimeOffsetMs, "Animation time offset must be zero or greater.");
        }

        Index = index;
        SuiteName = suiteName;
        EntryId = entryId;
        VariantName = variantName;
        Viewport = viewport ?? throw new ArgumentNullException(nameof(viewport));
        ImagePath = Path.GetFullPath(imagePath);
        Dpi = dpi;
        ProjectPath = Path.GetFullPath(projectPath);
        ViewPath = viewPath;
        ThemeVariant = string.IsNullOrWhiteSpace(themeVariant) ? null : themeVariant;
        Culture = string.IsNullOrWhiteSpace(culture) ? null : culture;
        DesignDataType = string.IsNullOrWhiteSpace(designDataType) ? null : designDataType;
        ProfileName = string.IsNullOrWhiteSpace(profileName) ? null : profileName;
        ProfileVariant = string.IsNullOrWhiteSpace(profileVariant) ? null : profileVariant;
        ProfileFilePath = string.IsNullOrWhiteSpace(profileFilePath) ? null : Path.GetFullPath(profileFilePath);
        RuntimeTarget = runtimeTarget;
        MutationPresetIds = PreviewBaselineSuiteDefaults.Normalize(mutationPresetIds);
        AnimationTimeOffsetMs = animationTimeOffsetMs;
    }

    [JsonPropertyName("index")]
    public int Index { get; }

    [JsonPropertyName("suiteName")]
    public string SuiteName { get; }

    [JsonPropertyName("entryId")]
    public string EntryId { get; }

    [JsonPropertyName("variantName")]
    public string VariantName { get; }

    [JsonPropertyName("viewport")]
    public PreviewViewport Viewport { get; }

    [JsonPropertyName("imagePath")]
    public string ImagePath { get; }

    [JsonPropertyName("dpi")]
    public double Dpi { get; }

    [JsonPropertyName("projectPath")]
    public string ProjectPath { get; }

    [JsonPropertyName("viewPath")]
    public string ViewPath { get; }

    [JsonPropertyName("themeVariant")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ThemeVariant { get; }

    [JsonPropertyName("culture")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Culture { get; }

    [JsonPropertyName("designDataType")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? DesignDataType { get; }

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
}
