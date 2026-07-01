using System.Text.Json.Serialization;

namespace AvaScope.Protocol;

public sealed record ArtifactRunIndexRequest
{
    [JsonConstructor]
    public ArtifactRunIndexRequest(
        string command,
        string status,
        string? taskName = null,
        string? runGroup = null,
        string? projectPath = null,
        string? viewPath = null,
        string? profile = null,
        string? variant = null,
        string? stateVariant = null,
        IReadOnlyList<ArtifactRunIndexArtifact>? artifacts = null,
        IReadOnlyList<ArtifactRunIndexDiagnostic>? diagnostics = null,
        IReadOnlyList<string>? warnings = null,
        IReadOnlyList<ArtifactRunIndexArtifact>? generatedReports = null,
        IReadOnlyDictionary<string, string>? metadata = null,
        DateTimeOffset? startedAt = null,
        DateTimeOffset? completedAt = null)
    {
        if (string.IsNullOrWhiteSpace(command))
        {
            throw new ArgumentException("Run index command cannot be empty.", nameof(command));
        }

        if (string.IsNullOrWhiteSpace(status))
        {
            throw new ArgumentException("Run index status cannot be empty.", nameof(status));
        }

        Command = command.Trim();
        Status = status.Trim();
        TaskName = Normalize(taskName);
        RunGroup = Normalize(runGroup);
        ProjectPath = string.IsNullOrWhiteSpace(projectPath) ? null : Path.GetFullPath(projectPath);
        ViewPath = Normalize(viewPath);
        Profile = Normalize(profile);
        Variant = Normalize(variant);
        StateVariant = Normalize(stateVariant);
        Artifacts = artifacts ?? [];
        Diagnostics = diagnostics ?? [];
        Warnings = (warnings ?? [])
            .Where(static warning => !string.IsNullOrWhiteSpace(warning))
            .Select(static warning => warning.Trim())
            .ToArray();
        GeneratedReports = generatedReports ?? [];
        Metadata = metadata ?? new Dictionary<string, string>();
        StartedAt = startedAt;
        CompletedAt = completedAt;
    }

    [JsonPropertyName("command")]
    public string Command { get; }

    [JsonPropertyName("status")]
    public string Status { get; }

    [JsonPropertyName("taskName")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? TaskName { get; }

    [JsonPropertyName("runGroup")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? RunGroup { get; }

    [JsonPropertyName("projectPath")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ProjectPath { get; }

    [JsonPropertyName("viewPath")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ViewPath { get; }

    [JsonPropertyName("profile")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Profile { get; }

    [JsonPropertyName("variant")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Variant { get; }

    [JsonPropertyName("stateVariant")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? StateVariant { get; }

    [JsonPropertyName("artifacts")]
    public IReadOnlyList<ArtifactRunIndexArtifact> Artifacts { get; }

    [JsonPropertyName("diagnostics")]
    public IReadOnlyList<ArtifactRunIndexDiagnostic> Diagnostics { get; }

    [JsonPropertyName("warnings")]
    public IReadOnlyList<string> Warnings { get; }

    [JsonPropertyName("generatedReports")]
    public IReadOnlyList<ArtifactRunIndexArtifact> GeneratedReports { get; }

    [JsonPropertyName("metadata")]
    public IReadOnlyDictionary<string, string> Metadata { get; }

    [JsonPropertyName("startedAt")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public DateTimeOffset? StartedAt { get; }

    [JsonPropertyName("completedAt")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public DateTimeOffset? CompletedAt { get; }

    private static string? Normalize(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
