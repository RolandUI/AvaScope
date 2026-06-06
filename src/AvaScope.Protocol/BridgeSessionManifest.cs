using System.Text.Json.Serialization;

namespace AvaScope.Protocol;

public sealed record BridgeSessionManifest
{
    [JsonConstructor]
    public BridgeSessionManifest(
        SessionId sessionId,
        int processId,
        string pipeName,
        DateTimeOffset createdAt,
        string? displayName = null)
    {
        SessionId = sessionId ?? throw new ArgumentNullException(nameof(sessionId));

        if (processId < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(processId), processId, "Process id must be positive.");
        }

        if (string.IsNullOrWhiteSpace(pipeName))
        {
            throw new ArgumentException("Pipe name cannot be empty.", nameof(pipeName));
        }

        ProcessId = processId;
        PipeName = pipeName;
        CreatedAt = createdAt;
        DisplayName = displayName;
    }

    [JsonPropertyName("sessionId")]
    public SessionId SessionId { get; }

    [JsonPropertyName("processId")]
    public int ProcessId { get; }

    [JsonPropertyName("pipeName")]
    public string PipeName { get; }

    [JsonPropertyName("createdAt")]
    public DateTimeOffset CreatedAt { get; }

    [JsonPropertyName("displayName")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? DisplayName { get; }
}
