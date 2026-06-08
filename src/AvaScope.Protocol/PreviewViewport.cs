using System.Text.Json.Serialization;

namespace AvaScope.Protocol;

public sealed record PreviewViewport
{
    [JsonConstructor]
    public PreviewViewport(double width, double height, string? label = null)
    {
        if (width < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(width), width, "Viewport width must be positive.");
        }

        if (height < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(height), height, "Viewport height must be positive.");
        }

        Width = width;
        Height = height;
        Label = string.IsNullOrWhiteSpace(label) ? null : label;
    }

    [JsonPropertyName("width")]
    public double Width { get; }

    [JsonPropertyName("height")]
    public double Height { get; }

    [JsonPropertyName("label")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Label { get; }
}
