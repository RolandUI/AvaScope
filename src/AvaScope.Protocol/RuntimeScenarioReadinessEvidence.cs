using System.Text.Json.Serialization;

namespace AvaScope.Protocol;

public sealed record RuntimeScenarioReadinessEvidence
{
    [JsonConstructor]
    public RuntimeScenarioReadinessEvidence(
        string status,
        DateTimeOffset startedAt,
        DateTimeOffset completedAt,
        int checkCount,
        int? processId = null,
        SessionId? sessionId = null,
        string? manifestPath = null,
        string? stdoutPath = null,
        string? stderrPath = null,
        IReadOnlyList<TopLevelSummary>? topLevels = null,
        ProtocolError? diagnostic = null,
        IReadOnlyDictionary<string, string>? metadata = null)
    {
        if (string.IsNullOrWhiteSpace(status))
        {
            throw new ArgumentException("Readiness status cannot be empty.", nameof(status));
        }

        if (checkCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(checkCount), checkCount, "Readiness check count cannot be negative.");
        }

        Status = status.Trim();
        StartedAt = startedAt;
        CompletedAt = completedAt;
        CheckCount = checkCount;
        ProcessId = processId;
        SessionId = sessionId;
        ManifestPath = string.IsNullOrWhiteSpace(manifestPath) ? null : Path.GetFullPath(manifestPath);
        StdoutPath = string.IsNullOrWhiteSpace(stdoutPath) ? null : Path.GetFullPath(stdoutPath);
        StderrPath = string.IsNullOrWhiteSpace(stderrPath) ? null : Path.GetFullPath(stderrPath);
        TopLevels = topLevels ?? [];
        Diagnostic = diagnostic;
        Metadata = metadata ?? new Dictionary<string, string>();
    }

    [JsonPropertyName("status")]
    public string Status { get; }

    [JsonPropertyName("startedAt")]
    public DateTimeOffset StartedAt { get; }

    [JsonPropertyName("completedAt")]
    public DateTimeOffset CompletedAt { get; }

    [JsonPropertyName("checkCount")]
    public int CheckCount { get; }

    [JsonPropertyName("processId")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? ProcessId { get; }

    [JsonPropertyName("sessionId")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public SessionId? SessionId { get; }

    [JsonPropertyName("manifestPath")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ManifestPath { get; }

    [JsonPropertyName("stdoutPath")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? StdoutPath { get; }

    [JsonPropertyName("stderrPath")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? StderrPath { get; }

    [JsonPropertyName("topLevels")]
    public IReadOnlyList<TopLevelSummary> TopLevels { get; }

    [JsonPropertyName("diagnostic")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ProtocolError? Diagnostic { get; }

    [JsonPropertyName("metadata")]
    public IReadOnlyDictionary<string, string> Metadata { get; }
}
