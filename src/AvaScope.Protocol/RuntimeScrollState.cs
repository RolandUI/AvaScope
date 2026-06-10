using System.Text.Json.Serialization;

namespace AvaScope.Protocol;

public sealed record RuntimeScrollState
{
    [JsonConstructor]
    public RuntimeScrollState(
        string status,
        RuntimeVector? offset = null,
        RuntimeSize? extent = null,
        RuntimeSize? viewport = null,
        RuntimeVector? scrollBarMaximum = null,
        string? horizontalScrollBarVisibility = null,
        string? verticalScrollBarVisibility = null,
        RuntimeLayoutMetrics? content = null)
    {
        if (string.IsNullOrWhiteSpace(status))
        {
            throw new ArgumentException("Scroll state status cannot be empty.", nameof(status));
        }

        Status = status;
        Offset = offset;
        Extent = extent;
        Viewport = viewport;
        ScrollBarMaximum = scrollBarMaximum;
        HorizontalScrollBarVisibility = string.IsNullOrWhiteSpace(horizontalScrollBarVisibility)
            ? null
            : horizontalScrollBarVisibility;
        VerticalScrollBarVisibility = string.IsNullOrWhiteSpace(verticalScrollBarVisibility)
            ? null
            : verticalScrollBarVisibility;
        Content = content;
    }

    [JsonPropertyName("status")]
    public string Status { get; }

    [JsonPropertyName("offset")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public RuntimeVector? Offset { get; }

    [JsonPropertyName("extent")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public RuntimeSize? Extent { get; }

    [JsonPropertyName("viewport")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public RuntimeSize? Viewport { get; }

    [JsonPropertyName("scrollBarMaximum")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public RuntimeVector? ScrollBarMaximum { get; }

    [JsonPropertyName("horizontalScrollBarVisibility")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? HorizontalScrollBarVisibility { get; }

    [JsonPropertyName("verticalScrollBarVisibility")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? VerticalScrollBarVisibility { get; }

    [JsonPropertyName("content")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public RuntimeLayoutMetrics? Content { get; }
}
