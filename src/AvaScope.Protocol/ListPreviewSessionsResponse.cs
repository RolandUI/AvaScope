using System.Text.Json.Serialization;

namespace AvaScope.Protocol;

public sealed record ListPreviewSessionsResponse
{
    [JsonConstructor]
    public ListPreviewSessionsResponse(IReadOnlyList<PreviewSessionSummary>? sessions = null)
    {
        Sessions = sessions ?? [];
    }

    [JsonPropertyName("sessions")]
    public IReadOnlyList<PreviewSessionSummary> Sessions { get; }
}
