using System.Text.Json.Serialization;

namespace AvaScope.Protocol;

public sealed record SessionSummary
{
    [JsonConstructor]
    public SessionSummary(
        SessionId sessionId,
        string kind,
        string state,
        DateTimeOffset createdAt,
        string? displayName = null)
    {
        SessionId = sessionId ?? throw new ArgumentNullException(nameof(sessionId));

        if (string.IsNullOrWhiteSpace(kind))
        {
            throw new ArgumentException("Session kind cannot be empty.", nameof(kind));
        }

        if (string.IsNullOrWhiteSpace(state))
        {
            throw new ArgumentException("Session state cannot be empty.", nameof(state));
        }

        Kind = kind;
        State = state;
        CreatedAt = createdAt;
        DisplayName = displayName;
    }

    [JsonPropertyName("sessionId")]
    public SessionId SessionId { get; }

    [JsonPropertyName("kind")]
    public string Kind { get; }

    [JsonPropertyName("state")]
    public string State { get; }

    [JsonPropertyName("createdAt")]
    public DateTimeOffset CreatedAt { get; }

    [JsonPropertyName("displayName")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? DisplayName { get; }
}
