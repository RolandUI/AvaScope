using System.Text.Json.Serialization;

namespace AvaScope.Protocol;

public sealed record TopLevelSummary
{
    [JsonConstructor]
    public TopLevelSummary(
        string id,
        string kind,
        string? title,
        double width,
        double height,
        double renderScaling,
        bool isActive)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            throw new ArgumentException("Top-level id cannot be empty.", nameof(id));
        }

        if (string.IsNullOrWhiteSpace(kind))
        {
            throw new ArgumentException("Top-level kind cannot be empty.", nameof(kind));
        }

        Id = id;
        Kind = kind;
        Title = title;
        Width = width;
        Height = height;
        RenderScaling = renderScaling;
        IsActive = isActive;
    }

    [JsonPropertyName("id")]
    public string Id { get; }

    [JsonPropertyName("kind")]
    public string Kind { get; }

    [JsonPropertyName("title")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Title { get; }

    [JsonPropertyName("width")]
    public double Width { get; }

    [JsonPropertyName("height")]
    public double Height { get; }

    [JsonPropertyName("renderScaling")]
    public double RenderScaling { get; }

    [JsonPropertyName("isActive")]
    public bool IsActive { get; }
}
