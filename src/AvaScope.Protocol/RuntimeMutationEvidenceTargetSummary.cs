using System.Text.Json.Serialization;

namespace AvaScope.Protocol;

public sealed record RuntimeMutationEvidenceTargetSummary
{
    [JsonConstructor]
    public RuntimeMutationEvidenceTargetSummary(
        string nodeId,
        string nodeType,
        string? name = null,
        string? text = null,
        NodeBounds? bounds = null,
        IReadOnlyList<string>? classes = null)
    {
        if (string.IsNullOrWhiteSpace(nodeId))
        {
            throw new ArgumentException("Evidence target node id cannot be empty.", nameof(nodeId));
        }

        if (string.IsNullOrWhiteSpace(nodeType))
        {
            throw new ArgumentException("Evidence target node type cannot be empty.", nameof(nodeType));
        }

        NodeId = nodeId.Trim();
        NodeType = nodeType.Trim();
        Name = string.IsNullOrWhiteSpace(name) ? null : name.Trim();
        Text = string.IsNullOrWhiteSpace(text) ? null : text;
        Bounds = bounds;
        Classes = classes ?? [];
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
}
