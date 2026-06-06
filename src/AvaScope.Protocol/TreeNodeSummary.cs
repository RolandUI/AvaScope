using System.Text.Json.Serialization;

namespace AvaScope.Protocol;

public sealed record TreeNodeSummary
{
    [JsonConstructor]
    public TreeNodeSummary(
        string nodeId,
        string nodeType,
        string? name = null,
        string? text = null,
        NodeBounds? bounds = null,
        IReadOnlyList<string>? classes = null,
        IReadOnlyList<TreeNodeSummary>? children = null)
    {
        if (string.IsNullOrWhiteSpace(nodeId))
        {
            throw new ArgumentException("Node id cannot be empty.", nameof(nodeId));
        }

        if (string.IsNullOrWhiteSpace(nodeType))
        {
            throw new ArgumentException("Node type cannot be empty.", nameof(nodeType));
        }

        NodeId = nodeId;
        NodeType = nodeType;
        Name = name;
        Text = text;
        Bounds = bounds;
        Classes = classes ?? Array.Empty<string>();
        Children = children ?? Array.Empty<TreeNodeSummary>();
    }

    [JsonPropertyName("nodeId")]
    public string NodeId { get; }

    [JsonPropertyName("nodeType")]
    public string NodeType { get; }

    [JsonPropertyName("name")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Name { get; }

    [JsonPropertyName("text")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Text { get; }

    [JsonPropertyName("bounds")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NodeBounds? Bounds { get; }

    [JsonPropertyName("classes")]
    public IReadOnlyList<string> Classes { get; }

    [JsonPropertyName("children")]
    public IReadOnlyList<TreeNodeSummary> Children { get; }
}
