using System.Text.Json.Serialization;

namespace AvaScope.Protocol;

public sealed record UiAuditIssue
{
    public const int MaximumDetails = 16;

    [JsonConstructor]
    public UiAuditIssue(
        string issueId,
        string category,
        string severity,
        string code,
        string message,
        string provenance,
        RuntimeTargetContext target,
        string suggestedAction,
        string? nodeId = null,
        string? nodeType = null,
        string? name = null,
        string? automationId = null,
        IReadOnlyDictionary<string, string>? details = null)
    {
        if (string.IsNullOrWhiteSpace(issueId))
        {
            throw new ArgumentException("Audit issue id cannot be empty.", nameof(issueId));
        }

        if (string.IsNullOrWhiteSpace(category))
        {
            throw new ArgumentException("Audit issue category cannot be empty.", nameof(category));
        }

        if (string.IsNullOrWhiteSpace(severity))
        {
            throw new ArgumentException("Audit issue severity cannot be empty.", nameof(severity));
        }

        if (string.IsNullOrWhiteSpace(code))
        {
            throw new ArgumentException("Audit issue code cannot be empty.", nameof(code));
        }

        if (string.IsNullOrWhiteSpace(message))
        {
            throw new ArgumentException("Audit issue message cannot be empty.", nameof(message));
        }

        if (string.IsNullOrWhiteSpace(provenance))
        {
            throw new ArgumentException("Audit issue provenance cannot be empty.", nameof(provenance));
        }

        if (string.IsNullOrWhiteSpace(suggestedAction))
        {
            throw new ArgumentException("Audit issue suggested action cannot be empty.", nameof(suggestedAction));
        }

        IssueId = issueId.Trim();
        Category = category.Trim();
        Severity = severity.Trim();
        Code = code.Trim();
        Message = message.Trim();
        Provenance = provenance.Trim();
        Target = target ?? throw new ArgumentNullException(nameof(target));
        SuggestedAction = suggestedAction.Trim();
        NodeId = Normalize(nodeId);
        NodeType = Normalize(nodeType);
        Name = Normalize(name);
        AutomationId = Normalize(automationId);
        Details = details is null
            ? new Dictionary<string, string>()
            : details
                .Where(static item => !string.IsNullOrWhiteSpace(item.Key))
                .Take(MaximumDetails)
                .ToDictionary(static item => item.Key, static item => item.Value, StringComparer.Ordinal);
    }

    [JsonPropertyName("issueId")]
    public string IssueId { get; }

    [JsonPropertyName("category")]
    public string Category { get; }

    [JsonPropertyName("severity")]
    public string Severity { get; }

    [JsonPropertyName("code")]
    public string Code { get; }

    [JsonPropertyName("message")]
    public string Message { get; }

    [JsonPropertyName("provenance")]
    public string Provenance { get; }

    [JsonPropertyName("target")]
    public RuntimeTargetContext Target { get; }

    [JsonPropertyName("suggestedAction")]
    public string SuggestedAction { get; }

    [JsonPropertyName("nodeId")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? NodeId { get; }

    [JsonPropertyName("nodeType")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? NodeType { get; }

    [JsonPropertyName("name")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Name { get; }

    [JsonPropertyName("automationId")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? AutomationId { get; }

    [JsonPropertyName("details")]
    public IReadOnlyDictionary<string, string> Details { get; }

    private static string? Normalize(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
