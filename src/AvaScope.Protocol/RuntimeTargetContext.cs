using System.Text.Json.Serialization;

namespace AvaScope.Protocol;

public sealed record RuntimeTargetContext
{
    [JsonConstructor]
    public RuntimeTargetContext(
        SessionId sessionId,
        string topLevelId,
        string? treeKind = null,
        string? nodeId = null,
        DateTimeOffset? capturedAt = null,
        string? targetKind = null,
        string? topLevelGeneration = null,
        string? nodeGeneration = null)
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

        var normalizedTargetKind = string.IsNullOrWhiteSpace(targetKind)
            ? normalizedNodeId is null ? "top_level" : "node"
            : targetKind.Trim();
        var normalizedTopLevelGeneration = string.IsNullOrWhiteSpace(topLevelGeneration)
            ? null
            : topLevelGeneration.Trim();
        var normalizedNodeGeneration = string.IsNullOrWhiteSpace(nodeGeneration)
            ? null
            : nodeGeneration.Trim();

        TopLevelId = topLevelId;
        TreeKind = normalizedTreeKind;
        NodeId = normalizedNodeId;
        CapturedAt = capturedAt;
        TargetKind = normalizedTargetKind;
        TopLevelGeneration = normalizedTopLevelGeneration;
        NodeGeneration = normalizedNodeGeneration;
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

    [JsonPropertyName("capturedAt")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public DateTimeOffset? CapturedAt { get; }

    [JsonPropertyName("targetKind")]
    public string TargetKind { get; }

    [JsonPropertyName("topLevelGeneration")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? TopLevelGeneration { get; }

    [JsonPropertyName("nodeGeneration")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? NodeGeneration { get; }
}
