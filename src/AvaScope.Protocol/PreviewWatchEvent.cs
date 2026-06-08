using System.Text.Json.Serialization;

namespace AvaScope.Protocol;

public sealed record PreviewWatchEvent
{
    [JsonConstructor]
    public PreviewWatchEvent(
        string eventType,
        DateTimeOffset timestamp,
        string? path = null,
        string? changeKind = null,
        ToolResult<PreviewSessionSummary>? reload = null)
    {
        if (string.IsNullOrWhiteSpace(eventType))
        {
            throw new ArgumentException("Watch event type cannot be empty.", nameof(eventType));
        }

        EventType = eventType;
        Timestamp = timestamp;
        Path = string.IsNullOrWhiteSpace(path) ? null : System.IO.Path.GetFullPath(path);
        ChangeKind = string.IsNullOrWhiteSpace(changeKind) ? null : changeKind;
        Reload = reload;
    }

    [JsonPropertyName("eventType")]
    public string EventType { get; }

    [JsonPropertyName("timestamp")]
    public DateTimeOffset Timestamp { get; }

    [JsonPropertyName("path")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Path { get; }

    [JsonPropertyName("changeKind")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ChangeKind { get; }

    [JsonPropertyName("reload")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ToolResult<PreviewSessionSummary>? Reload { get; }
}
