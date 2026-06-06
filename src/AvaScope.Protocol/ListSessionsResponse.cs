using System.Text.Json.Serialization;

namespace AvaScope.Protocol;

public sealed record ListSessionsResponse
{
    [JsonConstructor]
    public ListSessionsResponse(IReadOnlyList<SessionSummary>? sessions = null)
    {
        Sessions = sessions ?? Array.Empty<SessionSummary>();
    }

    [JsonPropertyName("sessions")]
    public IReadOnlyList<SessionSummary> Sessions { get; }
}
