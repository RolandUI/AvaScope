using System.Text.Json.Serialization;

namespace AvaScope.Protocol;

public sealed record RuntimeLayoutReason
{
    [JsonConstructor]
    public RuntimeLayoutReason(
        string code,
        string message,
        string severity = "info",
        string? sourceNodeId = null,
        string? sourceNodeType = null,
        IReadOnlyDictionary<string, string>? details = null)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            throw new ArgumentException("Layout reason code cannot be empty.", nameof(code));
        }

        if (string.IsNullOrWhiteSpace(message))
        {
            throw new ArgumentException("Layout reason message cannot be empty.", nameof(message));
        }

        Code = code;
        Message = message;
        Severity = string.IsNullOrWhiteSpace(severity) ? "info" : severity;
        SourceNodeId = string.IsNullOrWhiteSpace(sourceNodeId) ? null : sourceNodeId;
        SourceNodeType = string.IsNullOrWhiteSpace(sourceNodeType) ? null : sourceNodeType;
        Details = details ?? new Dictionary<string, string>();
    }

    [JsonPropertyName("code")]
    public string Code { get; }

    [JsonPropertyName("message")]
    public string Message { get; }

    [JsonPropertyName("severity")]
    public string Severity { get; }

    [JsonPropertyName("sourceNodeId")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? SourceNodeId { get; }

    [JsonPropertyName("sourceNodeType")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? SourceNodeType { get; }

    [JsonPropertyName("details")]
    public IReadOnlyDictionary<string, string> Details { get; }
}
