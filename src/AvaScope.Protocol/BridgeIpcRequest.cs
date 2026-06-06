using System.Text.Json.Serialization;

namespace AvaScope.Protocol;

public sealed record BridgeIpcRequest
{
    [JsonConstructor]
    public BridgeIpcRequest(
        string requestId,
        string method,
        string? topLevelId = null,
        string? outputPath = null)
    {
        if (string.IsNullOrWhiteSpace(requestId))
        {
            throw new ArgumentException("Request id cannot be empty.", nameof(requestId));
        }

        if (string.IsNullOrWhiteSpace(method))
        {
            throw new ArgumentException("Method cannot be empty.", nameof(method));
        }

        RequestId = requestId;
        Method = method;
        TopLevelId = topLevelId;
        OutputPath = outputPath;
    }

    [JsonPropertyName("requestId")]
    public string RequestId { get; }

    [JsonPropertyName("method")]
    public string Method { get; }

    [JsonPropertyName("topLevelId")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? TopLevelId { get; }

    [JsonPropertyName("outputPath")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? OutputPath { get; }
}
