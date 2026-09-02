using System.Text.Json.Serialization;

namespace AvaScope.Protocol;

public sealed record SemanticTopLevelSelector
{
    [JsonConstructor]
    public SemanticTopLevelSelector(
        string? title = null,
        string? kind = null,
        bool? isActive = null,
        SessionId? sessionId = null)
    {
        Title = string.IsNullOrWhiteSpace(title) ? null : title.Trim();
        Kind = string.IsNullOrWhiteSpace(kind) ? null : kind.Trim();
        IsActive = isActive;
        SessionId = sessionId;

        if (!HasSearchCriteria)
        {
            throw new ArgumentException("Top-level selector requires title, kind, or isActive.");
        }
    }

    [JsonPropertyName("title")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Title { get; }

    [JsonPropertyName("kind")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Kind { get; }

    [JsonPropertyName("isActive")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? IsActive { get; }

    [JsonPropertyName("sessionId")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public SessionId? SessionId { get; }

    [JsonIgnore]
    public bool HasSearchCriteria =>
        !string.IsNullOrWhiteSpace(Title)
        || !string.IsNullOrWhiteSpace(Kind)
        || IsActive.HasValue;
}
