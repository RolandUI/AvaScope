using System.Text.Json.Serialization;

namespace AvaScope.Protocol;

public sealed record ArtifactLatestRunPointer
{
    [JsonConstructor]
    public ArtifactLatestRunPointer(
        string taskKey,
        string runId,
        string indexJsonPath,
        string indexHtmlPath,
        DateTimeOffset generatedAt,
        string command,
        string? taskName = null,
        string? runGroup = null,
        string? projectPath = null,
        string? viewPath = null,
        string? profile = null,
        string? variant = null,
        string? stateVariant = null)
    {
        if (string.IsNullOrWhiteSpace(taskKey))
        {
            throw new ArgumentException("Latest run task key cannot be empty.", nameof(taskKey));
        }

        if (string.IsNullOrWhiteSpace(runId))
        {
            throw new ArgumentException("Latest run id cannot be empty.", nameof(runId));
        }

        if (string.IsNullOrWhiteSpace(indexJsonPath))
        {
            throw new ArgumentException("Latest run JSON path cannot be empty.", nameof(indexJsonPath));
        }

        if (string.IsNullOrWhiteSpace(indexHtmlPath))
        {
            throw new ArgumentException("Latest run HTML path cannot be empty.", nameof(indexHtmlPath));
        }

        if (string.IsNullOrWhiteSpace(command))
        {
            throw new ArgumentException("Latest run command cannot be empty.", nameof(command));
        }

        TaskKey = taskKey.Trim();
        RunId = runId.Trim();
        IndexJsonPath = Path.GetFullPath(indexJsonPath);
        IndexHtmlPath = Path.GetFullPath(indexHtmlPath);
        GeneratedAt = generatedAt;
        Command = command.Trim();
        TaskName = Normalize(taskName);
        RunGroup = Normalize(runGroup);
        ProjectPath = string.IsNullOrWhiteSpace(projectPath) ? null : Path.GetFullPath(projectPath);
        ViewPath = Normalize(viewPath);
        Profile = Normalize(profile);
        Variant = Normalize(variant);
        StateVariant = Normalize(stateVariant);
    }

    [JsonPropertyName("taskKey")]
    public string TaskKey { get; }

    [JsonPropertyName("runId")]
    public string RunId { get; }

    [JsonPropertyName("indexJsonPath")]
    public string IndexJsonPath { get; }

    [JsonPropertyName("indexJsonUrl")]
    public string IndexJsonUrl => new Uri(IndexJsonPath).AbsoluteUri;

    [JsonPropertyName("indexHtmlPath")]
    public string IndexHtmlPath { get; }

    [JsonPropertyName("indexHtmlUrl")]
    public string IndexHtmlUrl => new Uri(IndexHtmlPath).AbsoluteUri;

    [JsonPropertyName("generatedAt")]
    public DateTimeOffset GeneratedAt { get; }

    [JsonPropertyName("command")]
    public string Command { get; }

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

    private static string? Normalize(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
