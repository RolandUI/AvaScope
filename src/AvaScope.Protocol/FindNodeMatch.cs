using System.Text.Json.Serialization;

namespace AvaScope.Protocol;

public sealed record FindNodeMatch
{
    [JsonConstructor]
    public FindNodeMatch(TreeNodeSummary node, IReadOnlyList<string>? path = null)
    {
        Node = node ?? throw new ArgumentNullException(nameof(node));
        Path = path ?? Array.Empty<string>();
    }

    [JsonPropertyName("node")]
    public TreeNodeSummary Node { get; }

    [JsonPropertyName("path")]
    public IReadOnlyList<string> Path { get; }
}
