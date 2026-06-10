using System.Text.Json.Serialization;

namespace AvaScope.Protocol;

public sealed record LaunchAppResponse
{
    [JsonConstructor]
    public LaunchAppResponse(
        SessionSummary session,
        int processId,
        string processName,
        string stdoutPath,
        string stderrPath,
        DateTimeOffset startedAt,
        DateTimeOffset attachedAt,
        string? topLevelId = null,
        string? manifestPath = null)
    {
        Session = session ?? throw new ArgumentNullException(nameof(session));

        if (processId < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(processId), processId, "Process id must be positive.");
        }

        if (string.IsNullOrWhiteSpace(processName))
        {
            throw new ArgumentException("Process name cannot be empty.", nameof(processName));
        }

        if (string.IsNullOrWhiteSpace(stdoutPath))
        {
            throw new ArgumentException("Stdout path cannot be empty.", nameof(stdoutPath));
        }

        if (string.IsNullOrWhiteSpace(stderrPath))
        {
            throw new ArgumentException("Stderr path cannot be empty.", nameof(stderrPath));
        }

        ProcessId = processId;
        ProcessName = processName;
        StdoutPath = Path.GetFullPath(stdoutPath);
        StderrPath = Path.GetFullPath(stderrPath);
        StartedAt = startedAt;
        AttachedAt = attachedAt;
        TopLevelId = string.IsNullOrWhiteSpace(topLevelId) ? null : topLevelId;
        ManifestPath = string.IsNullOrWhiteSpace(manifestPath) ? null : Path.GetFullPath(manifestPath);
    }

    [JsonPropertyName("session")]
    public SessionSummary Session { get; }

    [JsonPropertyName("processId")]
    public int ProcessId { get; }

    [JsonPropertyName("processName")]
    public string ProcessName { get; }

    [JsonPropertyName("topLevelId")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? TopLevelId { get; }

    [JsonPropertyName("manifestPath")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ManifestPath { get; }

    [JsonPropertyName("stdoutPath")]
    public string StdoutPath { get; }

    [JsonPropertyName("stderrPath")]
    public string StderrPath { get; }

    [JsonPropertyName("startedAt")]
    public DateTimeOffset StartedAt { get; }

    [JsonPropertyName("attachedAt")]
    public DateTimeOffset AttachedAt { get; }
}
