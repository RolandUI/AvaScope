using System.Text.Json.Serialization;

namespace AvaScope.Protocol;

public sealed record CloseSessionResponse
{
    [JsonConstructor]
    public CloseSessionResponse(
        SessionSummary session,
        int processId,
        DateTimeOffset closedAt)
    {
        ArgumentNullException.ThrowIfNull(session);

        if (processId < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(processId), processId, "Process id must be positive.");
        }

        Session = session;
        ProcessId = processId;
        ClosedAt = closedAt;
    }

    [JsonPropertyName("session")]
    public SessionSummary Session { get; }

    [JsonPropertyName("processId")]
    public int ProcessId { get; }

    [JsonPropertyName("closedAt")]
    public DateTimeOffset ClosedAt { get; }
}
