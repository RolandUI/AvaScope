using System.Text.Json.Serialization;

namespace AvaScope.Protocol;

public sealed record AttachToAppResponse
{
    [JsonConstructor]
    public AttachToAppResponse(SessionSummary session, int processId)
    {
        Session = session ?? throw new ArgumentNullException(nameof(session));

        if (processId < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(processId), processId, "Process id must be positive.");
        }

        ProcessId = processId;
    }

    [JsonPropertyName("session")]
    public SessionSummary Session { get; }

    [JsonPropertyName("processId")]
    public int ProcessId { get; }
}
