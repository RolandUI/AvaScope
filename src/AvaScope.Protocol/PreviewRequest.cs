using System.Text.Json.Serialization;

namespace AvaScope.Protocol;

public sealed record PreviewRequest
{
    [JsonConstructor]
    public PreviewRequest(
        string outputPath,
        double? width = null,
        double? height = null,
        double dpi = 96,
        string? projectPath = null,
        string? viewPath = null,
        string? themeVariant = null,
        string? culture = null,
        string? designDataType = null,
        int? animationTimeOffsetMs = null,
        string? stateVariant = null)
    {
        if (string.IsNullOrWhiteSpace(outputPath))
        {
            throw new ArgumentException("Output path cannot be empty.", nameof(outputPath));
        }

        if (width is < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(width), width, "Width must be positive.");
        }

        if (height is < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(height), height, "Height must be positive.");
        }

        if (dpi <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(dpi), dpi, "DPI must be positive.");
        }

        if (animationTimeOffsetMs is < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(animationTimeOffsetMs), animationTimeOffsetMs, "Animation time offset must be zero or greater.");
        }

        OutputPath = outputPath;
        Width = width;
        Height = height;
        Dpi = dpi;
        ProjectPath = string.IsNullOrWhiteSpace(projectPath) ? null : projectPath;
        ViewPath = string.IsNullOrWhiteSpace(viewPath) ? null : viewPath;
        ThemeVariant = string.IsNullOrWhiteSpace(themeVariant) ? null : themeVariant;
        Culture = string.IsNullOrWhiteSpace(culture) ? null : culture;
        DesignDataType = string.IsNullOrWhiteSpace(designDataType) ? null : designDataType;
        AnimationTimeOffsetMs = animationTimeOffsetMs;
        StateVariant = string.IsNullOrWhiteSpace(stateVariant) ? null : stateVariant;
    }

    [JsonPropertyName("outputPath")]
    public string OutputPath { get; }

    [JsonPropertyName("width")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? Width { get; }

    [JsonPropertyName("height")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? Height { get; }

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

    [JsonPropertyName("animationTimeOffsetMs")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? AnimationTimeOffsetMs { get; }

    [JsonPropertyName("stateVariant")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? StateVariant { get; }
}
