using System.Text.Json.Serialization;

namespace AvaScope.Protocol;

public sealed record RuntimeCustomActionsResponse
{
    [JsonConstructor]
    public RuntimeCustomActionsResponse(
        SessionId sessionId,
        string topLevelId,
        RuntimeTargetContext target,
        bool enabled,
        IReadOnlyList<RuntimeCustomActionDescriptor>? actions,
        DateTimeOffset evaluatedAt,
        IReadOnlyList<ProtocolError>? diagnostics = null)
    {
        SessionId = sessionId ?? throw new ArgumentNullException(nameof(sessionId));
        TopLevelId = string.IsNullOrWhiteSpace(topLevelId)
            ? throw new ArgumentException("Top-level id cannot be empty.", nameof(topLevelId))
            : topLevelId;
        Target = target ?? throw new ArgumentNullException(nameof(target));
        Enabled = enabled;
        Actions = (actions ?? []).Take(64).ToArray();
        EvaluatedAt = evaluatedAt;
        Diagnostics = (diagnostics ?? []).Take(16).ToArray();
    }

    [JsonPropertyName("sessionId")] public SessionId SessionId { get; }
    [JsonPropertyName("topLevelId")] public string TopLevelId { get; }
    [JsonPropertyName("target")] public RuntimeTargetContext Target { get; }
    [JsonPropertyName("enabled")] public bool Enabled { get; }
    [JsonPropertyName("actions")] public IReadOnlyList<RuntimeCustomActionDescriptor> Actions { get; }
    [JsonPropertyName("evaluatedAt")] public DateTimeOffset EvaluatedAt { get; }
    [JsonPropertyName("diagnostics")] public IReadOnlyList<ProtocolError> Diagnostics { get; }
}
