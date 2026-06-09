using System.Text.Json.Serialization;

namespace AvaScope.Protocol;

public sealed record DiagnosticIssue
{
    [JsonConstructor]
    public DiagnosticIssue(
        string source,
        string severity,
        string status,
        string code,
        string message,
        string provenance,
        DateTimeOffset observedAt,
        string? sessionId = null,
        int? processId = null,
        string? path = null,
        IReadOnlyDictionary<string, string>? details = null)
    {
        if (string.IsNullOrWhiteSpace(source))
        {
            throw new ArgumentException("Diagnostic issue source cannot be empty.", nameof(source));
        }

        if (string.IsNullOrWhiteSpace(severity))
        {
            throw new ArgumentException("Diagnostic issue severity cannot be empty.", nameof(severity));
        }

        if (string.IsNullOrWhiteSpace(status))
        {
            throw new ArgumentException("Diagnostic issue status cannot be empty.", nameof(status));
        }

        if (string.IsNullOrWhiteSpace(code))
        {
            throw new ArgumentException("Diagnostic issue code cannot be empty.", nameof(code));
        }

        if (string.IsNullOrWhiteSpace(message))
        {
            throw new ArgumentException("Diagnostic issue message cannot be empty.", nameof(message));
        }

        if (string.IsNullOrWhiteSpace(provenance))
        {
            throw new ArgumentException("Diagnostic issue provenance cannot be empty.", nameof(provenance));
        }

        if (processId is < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(processId), processId, "Process id must be positive.");
        }

        Source = source;
        Severity = severity;
        Status = status;
        Code = code;
        Message = message;
        Provenance = provenance;
        ObservedAt = observedAt;
        SessionId = string.IsNullOrWhiteSpace(sessionId) ? null : sessionId.Trim();
        ProcessId = processId;
        Path = string.IsNullOrWhiteSpace(path) ? null : System.IO.Path.GetFullPath(path);
        Details = details;
    }

    [JsonPropertyName("source")]
    public string Source { get; }

    [JsonPropertyName("severity")]
    public string Severity { get; }

    [JsonPropertyName("status")]
    public string Status { get; }

    [JsonPropertyName("code")]
    public string Code { get; }

    [JsonPropertyName("message")]
    public string Message { get; }

    [JsonPropertyName("provenance")]
    public string Provenance { get; }

    [JsonPropertyName("observedAt")]
    public DateTimeOffset ObservedAt { get; }

    [JsonPropertyName("sessionId")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? SessionId { get; }

    [JsonPropertyName("processId")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? ProcessId { get; }

    [JsonPropertyName("path")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Path { get; }

    [JsonPropertyName("details")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyDictionary<string, string>? Details { get; }
}
