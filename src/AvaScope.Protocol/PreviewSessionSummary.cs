using System.Text.Json.Serialization;

namespace AvaScope.Protocol;

public sealed record PreviewSessionSummary
{
    [JsonConstructor]
    public PreviewSessionSummary(
        SessionSummary session,
        PreviewRequest request,
        ToolResult<PreviewResponse> lastRender,
        DateTimeOffset updatedAt)
    {
        Session = session ?? throw new ArgumentNullException(nameof(session));
        Request = request ?? throw new ArgumentNullException(nameof(request));
        LastRender = lastRender ?? throw new ArgumentNullException(nameof(lastRender));
        UpdatedAt = updatedAt;
    }

    [JsonPropertyName("session")]
    public SessionSummary Session { get; }

    [JsonPropertyName("request")]
    public PreviewRequest Request { get; }

    [JsonPropertyName("lastRender")]
    public ToolResult<PreviewResponse> LastRender { get; }

    [JsonPropertyName("updatedAt")]
    public DateTimeOffset UpdatedAt { get; }
}
