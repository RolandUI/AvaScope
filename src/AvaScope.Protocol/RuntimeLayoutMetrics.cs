using System.Text.Json.Serialization;

namespace AvaScope.Protocol;

public sealed record RuntimeLayoutMetrics
{
    [JsonConstructor]
    public RuntimeLayoutMetrics(
        string status,
        string? nodeId = null,
        string? nodeType = null,
        NodeBounds? bounds = null,
        RuntimeSize? desiredSize = null,
        RuntimeSize? arrangedSize = null,
        RuntimeTargetContext? target = null)
    {
        if (string.IsNullOrWhiteSpace(status))
        {
            throw new ArgumentException("Layout status cannot be empty.", nameof(status));
        }

        Status = status;
        NodeId = string.IsNullOrWhiteSpace(nodeId) ? null : nodeId;
        NodeType = string.IsNullOrWhiteSpace(nodeType) ? null : nodeType;
        Bounds = bounds;
        DesiredSize = desiredSize;
        ArrangedSize = arrangedSize;
        Target = target;
    }

    [JsonPropertyName("status")]
    public string Status { get; }

    [JsonPropertyName("nodeId")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? NodeId { get; }

    [JsonPropertyName("nodeType")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? NodeType { get; }

    [JsonPropertyName("bounds")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NodeBounds? Bounds { get; }

    [JsonPropertyName("desiredSize")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public RuntimeSize? DesiredSize { get; }

    [JsonPropertyName("arrangedSize")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public RuntimeSize? ArrangedSize { get; }

    [JsonPropertyName("target")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public RuntimeTargetContext? Target { get; }
}
