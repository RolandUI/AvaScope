using System.Text.Json.Serialization;

namespace AvaScope.Protocol;

public sealed record BridgeSessionDiagnostic
{
    [JsonConstructor]
    public BridgeSessionDiagnostic(
        string status,
        string manifestPath,
        SessionSummary? session = null,
        int? processId = null,
        string? transport = null,
        string? pipeName = null,
        HealthResponse? health = null,
        ProtocolError? error = null,
        string? processName = null,
        DateTimeOffset? checkedAt = null,
        string? requestId = null,
        bool cleanupCandidate = false)
    {
        if (string.IsNullOrWhiteSpace(status))
        {
            throw new ArgumentException("Diagnostic status cannot be empty.", nameof(status));
        }

        if (string.IsNullOrWhiteSpace(manifestPath))
        {
            throw new ArgumentException("Manifest path cannot be empty.", nameof(manifestPath));
        }

        if (processId is < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(processId), processId, "Process id must be positive.");
        }

        if (transport is not null && string.IsNullOrWhiteSpace(transport))
        {
            throw new ArgumentException("Transport cannot be empty.", nameof(transport));
        }

        if (pipeName is not null && string.IsNullOrWhiteSpace(pipeName))
        {
            throw new ArgumentException("Pipe name cannot be empty.", nameof(pipeName));
        }

        Status = status;
        ManifestPath = Path.GetFullPath(manifestPath);
        Session = session;
        ProcessId = processId;
        Transport = transport;
        PipeName = pipeName;
        Health = health;
        Error = error;
        ProcessName = string.IsNullOrWhiteSpace(processName) ? null : processName.Trim();
        CheckedAt = checkedAt;
        RequestId = string.IsNullOrWhiteSpace(requestId) ? null : requestId.Trim();
        CleanupCandidate = cleanupCandidate;
    }

    [JsonPropertyName("status")]
    public string Status { get; }

    [JsonPropertyName("manifestPath")]
    public string ManifestPath { get; }

    [JsonPropertyName("session")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public SessionSummary? Session { get; }

    [JsonPropertyName("processId")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? ProcessId { get; }

    [JsonPropertyName("transport")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Transport { get; }

    [JsonPropertyName("pipeName")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? PipeName { get; }

    [JsonPropertyName("health")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public HealthResponse? Health { get; }

    [JsonPropertyName("error")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ProtocolError? Error { get; }

    [JsonPropertyName("processName")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ProcessName { get; }

    [JsonPropertyName("checkedAt")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public DateTimeOffset? CheckedAt { get; }

    [JsonPropertyName("requestId")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? RequestId { get; }

    [JsonPropertyName("cleanupCandidate")]
    public bool CleanupCandidate { get; }
}
