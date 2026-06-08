using System.Text.Json.Serialization;

namespace AvaScope.Protocol;

public sealed record PreviewDiagnostic
{
    [JsonConstructor]
    public PreviewDiagnostic(
        string severity,
        string category,
        string code,
        string message,
        string? nodeId = null,
        string? nodeType = null,
        string? propertyName = null,
        string? sourcePath = null,
        NodeBounds? bounds = null,
        IReadOnlyDictionary<string, string>? details = null)
    {
        if (string.IsNullOrWhiteSpace(severity))
        {
            throw new ArgumentException("Diagnostic severity cannot be empty.", nameof(severity));
        }

        if (string.IsNullOrWhiteSpace(category))
        {
            throw new ArgumentException("Diagnostic category cannot be empty.", nameof(category));
        }

        if (string.IsNullOrWhiteSpace(code))
        {
            throw new ArgumentException("Diagnostic code cannot be empty.", nameof(code));
        }

        if (string.IsNullOrWhiteSpace(message))
        {
            throw new ArgumentException("Diagnostic message cannot be empty.", nameof(message));
        }

        Severity = severity;
        Category = category;
        Code = code;
        Message = message;
        NodeId = string.IsNullOrWhiteSpace(nodeId) ? null : nodeId;
        NodeType = string.IsNullOrWhiteSpace(nodeType) ? null : nodeType;
        PropertyName = string.IsNullOrWhiteSpace(propertyName) ? null : propertyName;
        SourcePath = string.IsNullOrWhiteSpace(sourcePath) ? null : sourcePath;
        Bounds = bounds;
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

    [JsonPropertyName("nodeId")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? NodeId { get; }

    [JsonPropertyName("nodeType")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? NodeType { get; }

    [JsonPropertyName("propertyName")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? PropertyName { get; }

    [JsonPropertyName("sourcePath")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? SourcePath { get; }

    [JsonPropertyName("bounds")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NodeBounds? Bounds { get; }

    [JsonPropertyName("details")]
    public IReadOnlyDictionary<string, string> Details { get; }
}
