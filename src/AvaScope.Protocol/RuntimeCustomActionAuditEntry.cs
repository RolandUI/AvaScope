using System.Text.Json.Serialization;

namespace AvaScope.Protocol;

public sealed record RuntimeCustomActionAuditEntry(
    [property: JsonPropertyName("sequence")] long Sequence,
    [property: JsonPropertyName("requestId")] string RequestId,
    [property: JsonPropertyName("actionName")] string ActionName,
    [property: JsonPropertyName("target")] RuntimeTargetContext Target,
    [property: JsonPropertyName("safetyClassification")] string SafetyClassification,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("executed")] bool Executed,
    [property: JsonPropertyName("evaluatedAt")] DateTimeOffset EvaluatedAt);
