using System.Text.Json.Serialization;

namespace AvaScope.Protocol;

public sealed record ArtifactRunIndexDiagnostic
{
    [JsonConstructor]
    public ArtifactRunIndexDiagnostic(
        string severity,
        string category,
        string code,
        string message,
        string? sourcePath = null,
        string? nodeId = null,
        IReadOnlyDictionary<string, string>? details = null)
    {
        if (string.IsNullOrWhiteSpace(severity))
        {
            throw new ArgumentException("Run index diagnostic severity cannot be empty.", nameof(severity));
        }

        if (string.IsNullOrWhiteSpace(category))
        {
            throw new ArgumentException("Run index diagnostic category cannot be empty.", nameof(category));
        }

        if (string.IsNullOrWhiteSpace(code))
        {
            throw new ArgumentException("Run index diagnostic code cannot be empty.", nameof(code));
        }

        if (string.IsNullOrWhiteSpace(message))
        {
            throw new ArgumentException("Run index diagnostic message cannot be empty.", nameof(message));
        }

        Severity = severity.Trim();
        Category = category.Trim();
        Code = code.Trim();
        Message = message.Trim();
        SourcePath = string.IsNullOrWhiteSpace(sourcePath) ? null : sourcePath.Trim();
        NodeId = string.IsNullOrWhiteSpace(nodeId) ? null : nodeId.Trim();
        Details = details ?? new Dictionary<string, string>();
    }

    [JsonPropertyName("severity")]
    public string Severity { get; }

    [JsonPropertyName("category")]
    public string Category { get; }

    [JsonPropertyName("code")]
    public string Code { get; }

    [JsonPropertyName("message")]
    public string Message { get; }

    [JsonPropertyName("sourcePath")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? SourcePath { get; }

    [JsonPropertyName("nodeId")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? NodeId { get; }

    [JsonPropertyName("details")]
    public IReadOnlyDictionary<string, string> Details { get; }
}
