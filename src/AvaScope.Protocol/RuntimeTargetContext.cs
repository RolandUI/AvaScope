using System.Text.Json.Serialization;

namespace AvaScope.Protocol;

public sealed record RuntimeTargetContext
{
    [JsonConstructor]
    public RuntimeTargetContext(
        SessionId sessionId,
        string topLevelId,
        string? treeKind = null,
        string? nodeId = null)
    {
        SessionId = sessionId ?? throw new ArgumentNullException(nameof(sessionId));

        if (string.IsNullOrWhiteSpace(topLevelId))
        {
            throw new ArgumentException("Top-level id cannot be empty.", nameof(topLevelId));
        }

        var normalizedTreeKind = string.IsNullOrWhiteSpace(treeKind) ? null : treeKind;
        var normalizedNodeId = string.IsNullOrWhiteSpace(nodeId) ? null : nodeId;
        if (normalizedNodeId is not null && normalizedTreeKind is null)
        {
            throw new ArgumentException("Tree kind is required when node id is provided.", nameof(treeKind));
        }

        TopLevelId = topLevelId;
        TreeKind = normalizedTreeKind;
        NodeId = normalizedNodeId;
    }

    [JsonPropertyName("sessionId")]
    public SessionId SessionId { get; }

    [JsonPropertyName("topLevelId")]
    public string TopLevelId { get; }

    [JsonPropertyName("treeKind")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? TreeKind { get; }

    [JsonPropertyName("nodeId")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? NodeId { get; }
}
