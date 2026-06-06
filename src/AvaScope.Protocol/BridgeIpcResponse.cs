using System.Text.Json;
using System.Text.Json.Serialization;

namespace AvaScope.Protocol;

public sealed record BridgeIpcResponse
{
    [JsonConstructor]
    public BridgeIpcResponse(string requestId, bool success, JsonElement? value, ProtocolError? error)
    {
        if (string.IsNullOrWhiteSpace(requestId))
        {
            throw new ArgumentException("Request id cannot be empty.", nameof(requestId));
        }

        if (success && error is not null)
        {
            throw new ArgumentException("Successful responses cannot include an error.", nameof(error));
        }

        if (!success && error is null)
        {
            throw new ArgumentException("Failed responses must include an error.", nameof(error));
        }

        RequestId = requestId;
        Success = success;
        Value = value;
        Error = error;
    }

    [JsonPropertyName("requestId")]
    public string RequestId { get; }

    [JsonPropertyName("success")]
    public bool Success { get; }

    [JsonPropertyName("value")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public JsonElement? Value { get; }

    [JsonPropertyName("error")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ProtocolError? Error { get; }

    public static BridgeIpcResponse Ok<T>(string requestId, T value)
    {
        return new BridgeIpcResponse(requestId, true, JsonSerializer.SerializeToElement(value), null);
    }

    public static BridgeIpcResponse Fail(string requestId, ProtocolError error)
    {
        return new BridgeIpcResponse(requestId, false, null, error);
    }

    public T? GetValue<T>()
    {
        return Value is { } value
            ? value.Deserialize<T>()
            : default;
    }
}
