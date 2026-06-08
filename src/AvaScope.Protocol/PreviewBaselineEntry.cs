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
        string? designDataType = null)
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

        Index = index;
        Viewport = viewport ?? throw new ArgumentNullException(nameof(viewport));
        ImagePath = Path.GetFullPath(imagePath);
        Dpi = dpi;
        ProjectPath = string.IsNullOrWhiteSpace(projectPath) ? null : Path.GetFullPath(projectPath);
        ViewPath = string.IsNullOrWhiteSpace(viewPath) ? null : viewPath;
        ThemeVariant = string.IsNullOrWhiteSpace(themeVariant) ? null : themeVariant;
        Culture = string.IsNullOrWhiteSpace(culture) ? null : culture;
        DesignDataType = string.IsNullOrWhiteSpace(designDataType) ? null : designDataType;
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
}
