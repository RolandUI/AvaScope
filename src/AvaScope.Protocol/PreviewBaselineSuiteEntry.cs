using System.Text.Json.Serialization;

namespace AvaScope.Protocol;

public sealed record PreviewBaselineSuiteEntry
{
    [JsonConstructor]
    public PreviewBaselineSuiteEntry(
        string id,
        string projectPath,
        string? viewPath = null,
        string? profileName = null,
        string? profileVariant = null,
        string? profileFilePath = null,
        IReadOnlyList<PreviewViewport>? sizes = null,
        IReadOnlyList<double>? dpis = null,
        IReadOnlyList<string>? themes = null,
        IReadOnlyList<string>? cultures = null,
        IReadOnlyList<string>? designDataTypes = null,
        IReadOnlyList<int>? animationFramesMs = null,
        RuntimeTargetContext? runtimeTarget = null,
        IReadOnlyList<string>? mutationPresetIds = null,
        IReadOnlyList<PreviewBaselineSuiteVariant>? variants = null)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            throw new ArgumentException("Suite entry id cannot be empty.", nameof(id));
        }

        if (string.IsNullOrWhiteSpace(projectPath))
        {
            throw new ArgumentException("Suite entry project path cannot be empty.", nameof(projectPath));
        }

        PreviewBaselineSuiteDefaults.ValidateDpis(dpis);
        PreviewBaselineSuiteDefaults.ValidateAnimationFrames(animationFramesMs);

        Id = id;
        ProjectPath = projectPath;
        ViewPath = string.IsNullOrWhiteSpace(viewPath) ? null : viewPath;
        ProfileName = string.IsNullOrWhiteSpace(profileName) ? null : profileName;
        ProfileVariant = string.IsNullOrWhiteSpace(profileVariant) ? null : profileVariant;
        ProfileFilePath = string.IsNullOrWhiteSpace(profileFilePath) ? null : profileFilePath;
        Sizes = sizes ?? [];
        Dpis = dpis ?? [];
        Themes = PreviewBaselineSuiteDefaults.Normalize(themes);
        Cultures = PreviewBaselineSuiteDefaults.Normalize(cultures);
        DesignDataTypes = PreviewBaselineSuiteDefaults.Normalize(designDataTypes);
        AnimationFramesMs = animationFramesMs ?? [];
        RuntimeTarget = runtimeTarget;
        MutationPresetIds = PreviewBaselineSuiteDefaults.Normalize(mutationPresetIds);
        Variants = variants ?? [];
    }

    [JsonPropertyName("id")]
    public string Id { get; }

    [JsonPropertyName("projectPath")]
    public string ProjectPath { get; }

    [JsonPropertyName("viewPath")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ViewPath { get; }

    [JsonPropertyName("profileName")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ProfileName { get; }

    [JsonPropertyName("profileVariant")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ProfileVariant { get; }

    [JsonPropertyName("profileFilePath")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ProfileFilePath { get; }

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

    [JsonPropertyName("runtimeTarget")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public RuntimeTargetContext? RuntimeTarget { get; }

    [JsonPropertyName("mutationPresetIds")]
    public IReadOnlyList<string> MutationPresetIds { get; }

    [JsonPropertyName("variants")]
    public IReadOnlyList<PreviewBaselineSuiteVariant> Variants { get; }
}
