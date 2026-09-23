using System.Text.Json.Serialization;

namespace AvaScope.Protocol;

public sealed record RuntimeOperationRequest
{
    [JsonConstructor]
    public RuntimeOperationRequest(SessionId sessionId, string operationId, string action = "status", int timeoutMs = 1000,
        bool allowDestructive = false, RuntimeEvidencePolicy? policy = null)
    {
        if (string.IsNullOrWhiteSpace(operationId) || operationId.Length > 192 || action is not ("status" or "wait" or "cancel")
            || timeoutMs is < 1 or > 30000)
            throw new ArgumentException("Use an operation id of at most 192 characters, status/wait/cancel and timeoutMs 1..30000.");
        SessionId = sessionId ?? throw new ArgumentNullException(nameof(sessionId));
        OperationId = operationId; Action = action; TimeoutMs = timeoutMs;
        AllowDestructive = allowDestructive; Policy = policy;
    }
    [JsonPropertyName("sessionId")] public SessionId SessionId { get; }
    [JsonPropertyName("operationId")] public string OperationId { get; }
    [JsonPropertyName("action")] public string Action { get; }
    [JsonPropertyName("timeoutMs")] public int TimeoutMs { get; }
    [JsonPropertyName("allowDestructive")] public bool AllowDestructive { get; }
    [JsonPropertyName("policy")] public RuntimeEvidencePolicy? Policy { get; }
}

public sealed record RuntimeOperationSnapshot(
    [property: JsonPropertyName("sessionId")] SessionId SessionId,
    [property: JsonPropertyName("operationId")] string OperationId,
    [property: JsonPropertyName("requestId")] string RequestId,
    [property: JsonPropertyName("actionName")] string ActionName,
    [property: JsonPropertyName("target")] RuntimeTargetContext Target,
    [property: JsonPropertyName("owner")] string? Owner,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("progress")] double? Progress,
    [property: JsonPropertyName("message")] string? Message,
    [property: JsonPropertyName("canCancel")] bool CanCancel,
    [property: JsonPropertyName("cancellationRequested")] bool CancellationRequested,
    [property: JsonPropertyName("createdAt")] DateTimeOffset CreatedAt,
    [property: JsonPropertyName("updatedAt")] DateTimeOffset UpdatedAt,
    [property: JsonPropertyName("retainUntil")] DateTimeOffset? RetainUntil,
    [property: JsonPropertyName("result")] IReadOnlyDictionary<string, string> Result,
    [property: JsonPropertyName("error")] ProtocolError? Error,
    [property: JsonPropertyName("source")] string Source = "app_reported")
{
    [JsonIgnore] public bool IsTerminal => Status is "completed" or "failed" or "cancelled";
}

public sealed record RuntimeOperationResponse(
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("operation")] RuntimeOperationSnapshot Operation,
    [property: JsonPropertyName("observedAt")] DateTimeOffset ObservedAt);
