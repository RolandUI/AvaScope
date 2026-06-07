using System.Text.Json.Serialization;

namespace AvaScope.Protocol;

public sealed record PreviewResponse
{
    [JsonConstructor]
    public PreviewResponse(
        string filePath,
        int pixelWidth,
        int pixelHeight,
        double dpi,
        DateTimeOffset renderedAt,
        string? projectPath = null,
        string? viewPath = null,
        string? themeVariant = null,
        string? culture = null,
        string? designDataType = null)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentException("File path cannot be empty.", nameof(filePath));
        }

        if (pixelWidth < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(pixelWidth), pixelWidth, "Pixel width must be positive.");
        }

        if (pixelHeight < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(pixelHeight), pixelHeight, "Pixel height must be positive.");
        }

        if (dpi <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(dpi), dpi, "DPI must be positive.");
        }

        FilePath = filePath;
        PixelWidth = pixelWidth;
        PixelHeight = pixelHeight;
        Dpi = dpi;
        RenderedAt = renderedAt;
        ProjectPath = string.IsNullOrWhiteSpace(projectPath) ? null : projectPath;
        ViewPath = string.IsNullOrWhiteSpace(viewPath) ? null : viewPath;
        ThemeVariant = string.IsNullOrWhiteSpace(themeVariant) ? null : themeVariant;
        Culture = string.IsNullOrWhiteSpace(culture) ? null : culture;
        DesignDataType = string.IsNullOrWhiteSpace(designDataType) ? null : designDataType;
    }

    [JsonPropertyName("filePath")]
    public string FilePath { get; }

    [JsonPropertyName("pixelWidth")]
    public int PixelWidth { get; }

    [JsonPropertyName("pixelHeight")]
    public int PixelHeight { get; }

    [JsonPropertyName("dpi")]
    public double Dpi { get; }

    [JsonPropertyName("renderedAt")]
    public DateTimeOffset RenderedAt { get; }

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
