using System.Text.Json.Serialization;

namespace AvaScope.Protocol;

public sealed record PreviewSessionSummary
{
    [JsonConstructor]
    public PreviewSessionSummary(
        SessionSummary session,
        PreviewRequest request,
        ToolResult<PreviewResponse> lastRender,
        DateTimeOffset updatedAt,
        IReadOnlyList<PreviewWatchEvent>? events = null,
        PreviewLifecycleStatus? lifecycle = null)
    {
        Session = session ?? throw new ArgumentNullException(nameof(session));
        Request = request ?? throw new ArgumentNullException(nameof(request));
        LastRender = lastRender ?? throw new ArgumentNullException(nameof(lastRender));
        UpdatedAt = updatedAt;
        Events = events ?? Array.Empty<PreviewWatchEvent>();
        Lifecycle = lifecycle ?? PreviewLifecycleStatus.OneShotIsolated;
    }

    [JsonPropertyName("session")]
    public SessionSummary Session { get; }

    [JsonPropertyName("request")]
    public PreviewRequest Request { get; }

    [JsonPropertyName("lastRender")]
    public ToolResult<PreviewResponse> LastRender { get; }

    [JsonPropertyName("updatedAt")]
    public DateTimeOffset UpdatedAt { get; }

    [JsonPropertyName("events")]
    public IReadOnlyList<PreviewWatchEvent> Events { get; }

    [JsonPropertyName("lifecycle")]
    public PreviewLifecycleStatus Lifecycle { get; }
}
