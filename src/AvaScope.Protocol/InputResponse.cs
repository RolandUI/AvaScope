using System.Text.Json.Serialization;

namespace AvaScope.Protocol;

public sealed record InputResponse
{
    [JsonConstructor]
    public InputResponse(
        SessionId sessionId,
        string topLevelId,
        string action,
        bool handled,
        DateTimeOffset executedAt,
        string? targetNodeId = null,
        RuntimeTargetContext? target = null)
    {
        SessionId = sessionId ?? throw new ArgumentNullException(nameof(sessionId));

        if (string.IsNullOrWhiteSpace(topLevelId))
        {
            throw new ArgumentException("Top-level id cannot be empty.", nameof(topLevelId));
        }

        if (string.IsNullOrWhiteSpace(action))
        {
            throw new ArgumentException("Input action cannot be empty.", nameof(action));
        }

        TopLevelId = topLevelId;
        Action = action;
        Handled = handled;
        ExecutedAt = executedAt;
        TargetNodeId = targetNodeId;
        Target = target ?? CreateTarget(sessionId, topLevelId, targetNodeId);
    }

    [JsonPropertyName("sessionId")]
    public SessionId SessionId { get; }

    [JsonPropertyName("topLevelId")]
    public string TopLevelId { get; }

    [JsonPropertyName("action")]
    public string Action { get; }

    [JsonPropertyName("handled")]
    public bool Handled { get; }

    [JsonPropertyName("executedAt")]
    public DateTimeOffset ExecutedAt { get; }

    [JsonPropertyName("targetNodeId")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? TargetNodeId { get; }

    [JsonPropertyName("target")]
    public RuntimeTargetContext Target { get; }

    private static RuntimeTargetContext CreateTarget(
        SessionId sessionId,
        string topLevelId,
        string? targetNodeId)
    {
        if (string.IsNullOrWhiteSpace(targetNodeId))
        {
            return new RuntimeTargetContext(sessionId, topLevelId);
        }

        var treeKind = InferTreeKind(targetNodeId);
        return treeKind is null
            ? new RuntimeTargetContext(sessionId, topLevelId)
            : new RuntimeTargetContext(sessionId, topLevelId, treeKind, targetNodeId);
    }

    private static string? InferTreeKind(string nodeId)
    {
        if (nodeId.StartsWith($"{TreeKinds.Visual}:", StringComparison.Ordinal))
        {
            return TreeKinds.Visual;
        }

        return nodeId.StartsWith($"{TreeKinds.Logical}:", StringComparison.Ordinal)
            ? TreeKinds.Logical
            : null;
    }
}
