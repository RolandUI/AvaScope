using System.Text.Json.Serialization;

namespace AvaScope.Protocol;

public sealed record AgentRunRecoveryRequest(
    [property: JsonPropertyName("runId")] string RunId,
    [property: JsonPropertyName("operation")] string Operation = "inspect",
    [property: JsonPropertyName("ttlMs")] int TtlMs = 30000);

public sealed record AgentRunProcess(
    [property: JsonPropertyName("role")] string Role,
    [property: JsonPropertyName("processId")] int ProcessId,
    [property: JsonPropertyName("startedAt")] DateTimeOffset StartedAt,
    [property: JsonPropertyName("startIdentity")] string? StartIdentity = null);

public sealed record AgentRunRecoveryResponse(
    [property: JsonPropertyName("runId")] string RunId,
    [property: JsonPropertyName("state")] string State,
    [property: JsonPropertyName("active")] bool Active,
    [property: JsonPropertyName("sessionId")] SessionId? SessionId,
    [property: JsonPropertyName("processes")] IReadOnlyList<AgentRunProcess> Processes,
    [property: JsonPropertyName("retainedPaths")] IReadOnlyList<string> RetainedPaths,
    [property: JsonPropertyName("providerVersion")] string? ProviderVersion,
    [property: JsonPropertyName("providerManifestSha256")] string? ProviderManifestSha256,
    [property: JsonPropertyName("diagnostics")] IReadOnlyList<ProtocolError> Diagnostics,
    [property: JsonPropertyName("controlToken"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? ControlToken = null,
    [property: JsonPropertyName("outcome")] string? Outcome = null,
    [property: JsonPropertyName("failureStage")] string? FailureStage = null);
