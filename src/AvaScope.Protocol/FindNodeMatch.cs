using System.Text.Json.Serialization;

namespace AvaScope.Protocol;

public sealed record FindNodeMatch
{
    [JsonConstructor]
    public FindNodeMatch(
        TreeNodeSummary node,
        IReadOnlyList<string>? path = null,
        RuntimeTargetContext? target = null,
        IReadOnlyList<RuntimeRelationshipEvidence>? relationships = null)
    {
        Node = node ?? throw new ArgumentNullException(nameof(node));
        Path = path ?? Array.Empty<string>();
        Target = target ?? node.Target;
        Relationships = relationships ?? [];
    }

    [JsonPropertyName("node")]
    public TreeNodeSummary Node { get; }

    [JsonPropertyName("path")]
    public IReadOnlyList<string> Path { get; }

    [JsonPropertyName("target")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public RuntimeTargetContext? Target { get; }

    [JsonPropertyName("relationships")]
    public IReadOnlyList<RuntimeRelationshipEvidence> Relationships { get; }
}
