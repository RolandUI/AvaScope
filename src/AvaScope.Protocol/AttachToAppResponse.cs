using System.Text.Json.Serialization;

namespace AvaScope.Protocol;

public sealed record AttachToAppResponse
{
    [JsonConstructor]
    public AttachToAppResponse(
        SessionSummary session,
        int processId,
        string? processName = null,
        string? manifestPath = null)
    {
        Session = session ?? throw new ArgumentNullException(nameof(session));

        if (processId < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(processId), processId, "Process id must be positive.");
        }

        ProcessId = processId;
        ProcessName = string.IsNullOrWhiteSpace(processName) ? null : processName.Trim();
        ManifestPath = string.IsNullOrWhiteSpace(manifestPath) ? null : Path.GetFullPath(manifestPath);
    }

    [JsonPropertyName("session")]
    public SessionSummary Session { get; }

    [JsonPropertyName("processId")]
    public int ProcessId { get; }

    [JsonPropertyName("processName")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ProcessName { get; }

    [JsonPropertyName("manifestPath")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ManifestPath { get; }
}
