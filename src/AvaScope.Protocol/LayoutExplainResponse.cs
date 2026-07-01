using System.Text.Json.Serialization;

namespace AvaScope.Protocol;

public sealed record LayoutExplainResponse
{
    [JsonConstructor]
    public LayoutExplainResponse(
        SessionId sessionId,
        string topLevelId,
        string treeKind,
        string nodeId,
        RuntimeLayoutExplanation explanation,
        RuntimeTargetContext? target = null)
    {
        SessionId = sessionId ?? throw new ArgumentNullException(nameof(sessionId));

        if (string.IsNullOrWhiteSpace(topLevelId))
        {
            throw new ArgumentException("Top-level id cannot be empty.", nameof(topLevelId));
        }

        if (string.IsNullOrWhiteSpace(treeKind))
        {
            throw new ArgumentException("Tree kind cannot be empty.", nameof(treeKind));
        }

        if (string.IsNullOrWhiteSpace(nodeId))
        {
            throw new ArgumentException("Node id cannot be empty.", nameof(nodeId));
        }

        TopLevelId = topLevelId;
        TreeKind = treeKind;
        NodeId = nodeId;
        Explanation = explanation ?? throw new ArgumentNullException(nameof(explanation));
        Target = target ?? new RuntimeTargetContext(sessionId, topLevelId, treeKind, nodeId);
    }

    [JsonPropertyName("sessionId")]
    public SessionId SessionId { get; }

    [JsonPropertyName("topLevelId")]
    public string TopLevelId { get; }

    [JsonPropertyName("treeKind")]
    public string TreeKind { get; }

    [JsonPropertyName("nodeId")]
    public string NodeId { get; }

    [JsonPropertyName("explanation")]
    public RuntimeLayoutExplanation Explanation { get; }

    [JsonPropertyName("target")]
    public RuntimeTargetContext Target { get; }
}
