using System.Text.Json.Serialization;

namespace AvaScope.Protocol;

public sealed record ToolResult<T>
{
    [JsonConstructor]
    public ToolResult(bool success, T? value, ProtocolError? error)
    {
        if (success && error is not null)
        {
            throw new ArgumentException("Successful results cannot include an error.", nameof(error));
        }

        if (!success && error is null)
        {
            throw new ArgumentException("Failed results must include an error.", nameof(error));
        }

        Success = success;
        Value = value;
        Error = error;
    }

    [JsonPropertyName("success")]
    public bool Success { get; }

    [JsonPropertyName("value")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public T? Value { get; }

    [JsonPropertyName("error")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ProtocolError? Error { get; }

    public static ToolResult<T> Ok(T value) => new(true, value, null);

    public static ToolResult<T> Fail(ProtocolError error) => new(false, default, error);
}
