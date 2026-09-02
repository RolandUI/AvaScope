using System.Text.Json.Serialization;

namespace AvaScope.Protocol;

public sealed record RuntimeCustomActionResponse
{
    [JsonConstructor]
    public RuntimeCustomActionResponse(
        string requestId,
        string actionName,
        RuntimeTargetContext target,
        string safetyClassification,
        string status,
        bool executed,
        string message,
        DateTimeOffset evaluatedAt,
        RuntimeCustomActionAuditEntry audit,
        IReadOnlyDictionary<string, string>? metadata = null,
        IReadOnlyList<ProtocolError>? diagnostics = null)
    {
        RequestId = requestId;
        ActionName = actionName;
        Target = target;
        SafetyClassification = safetyClassification;
        Status = status;
        Executed = executed;
        Message = message;
        EvaluatedAt = evaluatedAt;
        Audit = audit;
        Metadata = (metadata ?? new Dictionary<string, string>()).Take(32)
            .ToDictionary(static pair => pair.Key, static pair => pair.Value, StringComparer.Ordinal);
        Diagnostics = (diagnostics ?? []).Take(16).ToArray();
    }

    [JsonPropertyName("requestId")] public string RequestId { get; }
    [JsonPropertyName("actionName")] public string ActionName { get; }
    [JsonPropertyName("target")] public RuntimeTargetContext Target { get; }
    [JsonPropertyName("safetyClassification")] public string SafetyClassification { get; }
    [JsonPropertyName("status")] public string Status { get; }
    [JsonPropertyName("executed")] public bool Executed { get; }
    [JsonPropertyName("message")] public string Message { get; }
    [JsonPropertyName("evaluatedAt")] public DateTimeOffset EvaluatedAt { get; }
    [JsonPropertyName("audit")] public RuntimeCustomActionAuditEntry Audit { get; }
    [JsonPropertyName("metadata")] public IReadOnlyDictionary<string, string> Metadata { get; }
    [JsonPropertyName("diagnostics")] public IReadOnlyList<ProtocolError> Diagnostics { get; }
}
