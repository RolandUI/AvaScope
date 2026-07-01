using System.Text.Json.Serialization;

namespace AvaScope.Protocol;

public sealed record ArtifactRunIndexSelector
{
    [JsonConstructor]
    public ArtifactRunIndexSelector(
        string? taskName = null,
        string? runGroup = null,
        string? projectPath = null,
        string? viewPath = null,
        string? profile = null,
        string? variant = null,
        string? stateVariant = null,
        string? command = null)
    {
        TaskName = Normalize(taskName);
        RunGroup = Normalize(runGroup);
        ProjectPath = string.IsNullOrWhiteSpace(projectPath) ? null : Path.GetFullPath(projectPath);
        ViewPath = Normalize(viewPath);
        Profile = Normalize(profile);
        Variant = Normalize(variant);
        StateVariant = Normalize(stateVariant);
        Command = Normalize(command);

        if (TaskName is null
            && ProjectPath is null
            && ViewPath is null
            && Command is null)
        {
            throw new ArgumentException("Latest-run resolution requires taskName, projectPath, viewPath, or command.", nameof(taskName));
        }
    }

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

    [JsonPropertyName("command")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Command { get; }

    private static string? Normalize(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
