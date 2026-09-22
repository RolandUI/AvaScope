using System.Text.Json.Serialization;

namespace AvaScope.Protocol;

public sealed record SessionControlRequest(
    [property: JsonPropertyName("operation")] string Operation = "status",
    [property: JsonPropertyName("owner")] string? Owner = null,
    [property: JsonPropertyName("token")] string? Token = null,
    [property: JsonPropertyName("ttlMs")] int TtlMs = 30000);

public sealed record SessionControlResponse(
    [property: JsonPropertyName("sessionId")] SessionId SessionId,
    [property: JsonPropertyName("state")] string State,
    [property: JsonPropertyName("owner")] string? Owner,
    [property: JsonPropertyName("expiresAt")] DateTimeOffset? ExpiresAt,
    [property: JsonPropertyName("busy")] bool Busy,
    [property: JsonPropertyName("token"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Token = null);
