using System.Text.Json.Serialization;

namespace AvaScope.Protocol;

public sealed record AgentReviewFailure
{
    [JsonConstructor]
    public AgentReviewFailure(
        string scope,
        string message,
        string? code = null,
        string? path = null)
    {
        if (string.IsNullOrWhiteSpace(scope))
        {
            throw new ArgumentException("Agent review failure scope cannot be empty.", nameof(scope));
        }

        if (string.IsNullOrWhiteSpace(message))
        {
            throw new ArgumentException("Agent review failure message cannot be empty.", nameof(message));
        }

        Scope = scope.Trim();
        Message = message.Trim();
        Code = string.IsNullOrWhiteSpace(code) ? null : code.Trim();
        Path = string.IsNullOrWhiteSpace(path) ? null : System.IO.Path.GetFullPath(path);
    }

    [JsonPropertyName("scope")]
    public string Scope { get; }

    [JsonPropertyName("message")]
    public string Message { get; }

    [JsonPropertyName("code")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Code { get; }

    [JsonPropertyName("path")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Path { get; }
}
