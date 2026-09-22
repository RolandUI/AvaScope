using System.Text.Json.Serialization;

namespace AvaScope.Protocol;

public sealed record RuntimeNativePickerRequest(
    [property: JsonPropertyName("topLevelId")] string? TopLevelId,
    [property: JsonPropertyName("operation")] string Operation,
    [property: JsonPropertyName("path")] string? Path = null,
    [property: JsonPropertyName("timeoutMs")] int TimeoutMs = 1000,
    [property: JsonPropertyName("redactPath")] bool RedactPath = true,
    [property: JsonPropertyName("predefinedResult")] string? PredefinedResult = null,
    [property: JsonPropertyName("correlationId")] string? CorrelationId = null,
    [property: JsonPropertyName("ttlMs")] int TtlMs = 30000);
