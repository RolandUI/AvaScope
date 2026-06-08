using System.Text.Json.Serialization;

namespace AvaScope.Protocol;

public sealed record PreviewWatchResponse
{
    [JsonConstructor]
    public PreviewWatchResponse(
        SessionId sessionId,
        IReadOnlyList<string>? watchPaths,
        IReadOnlyList<PreviewWatchEvent>? events,
        bool timedOut,
        int reloadCount,
        DateTimeOffset startedAt,
        DateTimeOffset completedAt,
        PreviewSessionSummary? latestSession = null)
    {
        ArgumentNullException.ThrowIfNull(sessionId);

        if (reloadCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(reloadCount), reloadCount, "Reload count cannot be negative.");
        }

        SessionId = sessionId;
        WatchPaths = (watchPaths ?? [])
            .Where(static path => !string.IsNullOrWhiteSpace(path))
            .Select(Path.GetFullPath)
            .ToArray();
        Events = events ?? [];
        TimedOut = timedOut;
        ReloadCount = reloadCount;
        StartedAt = startedAt;
        CompletedAt = completedAt;
        LatestSession = latestSession;
    }

    [JsonPropertyName("sessionId")]
    public SessionId SessionId { get; }

    [JsonPropertyName("watchPaths")]
    public IReadOnlyList<string> WatchPaths { get; }

    [JsonPropertyName("events")]
    public IReadOnlyList<PreviewWatchEvent> Events { get; }

    [JsonPropertyName("timedOut")]
    public bool TimedOut { get; }

    [JsonPropertyName("reloadCount")]
    public int ReloadCount { get; }

    [JsonPropertyName("startedAt")]
    public DateTimeOffset StartedAt { get; }

    [JsonPropertyName("completedAt")]
    public DateTimeOffset CompletedAt { get; }

    [JsonPropertyName("latestSession")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public PreviewSessionSummary? LatestSession { get; }
}
