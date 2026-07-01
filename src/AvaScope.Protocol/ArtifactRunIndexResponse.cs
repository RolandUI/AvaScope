using System.Text.Json.Serialization;

namespace AvaScope.Protocol;

public sealed record ArtifactRunIndexResponse
{
    [JsonConstructor]
    public ArtifactRunIndexResponse(
        string runId,
        string taskKey,
        string command,
        string status,
        DateTimeOffset generatedAt,
        string indexJsonPath,
        string indexHtmlPath,
        string latestPointerPath,
        string? taskName = null,
        string? runGroup = null,
        string? projectPath = null,
        string? viewPath = null,
        string? profile = null,
        string? variant = null,
        string? stateVariant = null,
        IReadOnlyList<string>? screenshotPaths = null,
        IReadOnlyList<ArtifactRunIndexArtifact>? artifacts = null,
        IReadOnlyList<ArtifactRunIndexDiagnostic>? diagnostics = null,
        IReadOnlyList<string>? warnings = null,
        IReadOnlyList<ArtifactRunIndexArtifact>? generatedReports = null,
        IReadOnlyDictionary<string, string>? metadata = null,
        DateTimeOffset? startedAt = null,
        DateTimeOffset? completedAt = null)
    {
        if (string.IsNullOrWhiteSpace(runId))
        {
            throw new ArgumentException("Run id cannot be empty.", nameof(runId));
        }

        if (string.IsNullOrWhiteSpace(taskKey))
        {
            throw new ArgumentException("Run index task key cannot be empty.", nameof(taskKey));
        }

        if (string.IsNullOrWhiteSpace(command))
        {
            throw new ArgumentException("Run index command cannot be empty.", nameof(command));
        }

        if (string.IsNullOrWhiteSpace(status))
        {
            throw new ArgumentException("Run index status cannot be empty.", nameof(status));
        }

        if (string.IsNullOrWhiteSpace(indexJsonPath))
        {
            throw new ArgumentException("Run index JSON path cannot be empty.", nameof(indexJsonPath));
        }

        if (string.IsNullOrWhiteSpace(indexHtmlPath))
        {
            throw new ArgumentException("Run index HTML path cannot be empty.", nameof(indexHtmlPath));
        }

        if (string.IsNullOrWhiteSpace(latestPointerPath))
        {
            throw new ArgumentException("Latest run pointer path cannot be empty.", nameof(latestPointerPath));
        }

        RunId = runId.Trim();
        TaskKey = taskKey.Trim();
        Command = command.Trim();
        Status = status.Trim();
        GeneratedAt = generatedAt;
        IndexJsonPath = Path.GetFullPath(indexJsonPath);
        IndexHtmlPath = Path.GetFullPath(indexHtmlPath);
        LatestPointerPath = Path.GetFullPath(latestPointerPath);
        TaskName = Normalize(taskName);
        RunGroup = Normalize(runGroup);
        ProjectPath = string.IsNullOrWhiteSpace(projectPath) ? null : Path.GetFullPath(projectPath);
        ViewPath = Normalize(viewPath);
        Profile = Normalize(profile);
        Variant = Normalize(variant);
        StateVariant = Normalize(stateVariant);
        ScreenshotPaths = (screenshotPaths ?? [])
            .Where(static path => !string.IsNullOrWhiteSpace(path))
            .Select(Path.GetFullPath)
            .ToArray();
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

    [JsonPropertyName("runId")]
    public string RunId { get; }

    [JsonPropertyName("taskKey")]
    public string TaskKey { get; }

    [JsonPropertyName("command")]
    public string Command { get; }

    [JsonPropertyName("status")]
    public string Status { get; }

    [JsonPropertyName("generatedAt")]
    public DateTimeOffset GeneratedAt { get; }

    [JsonPropertyName("indexJsonPath")]
    public string IndexJsonPath { get; }

    [JsonPropertyName("indexJsonUrl")]
    public string IndexJsonUrl => new Uri(IndexJsonPath).AbsoluteUri;

    [JsonPropertyName("indexHtmlPath")]
    public string IndexHtmlPath { get; }

    [JsonPropertyName("indexHtmlUrl")]
    public string IndexHtmlUrl => new Uri(IndexHtmlPath).AbsoluteUri;

    [JsonPropertyName("latestPointerPath")]
    public string LatestPointerPath { get; }

    [JsonPropertyName("latestPointerUrl")]
    public string LatestPointerUrl => new Uri(LatestPointerPath).AbsoluteUri;

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

    [JsonPropertyName("screenshotPaths")]
    public IReadOnlyList<string> ScreenshotPaths { get; }

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
