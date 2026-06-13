using System.Text.Json.Serialization;

namespace AvaScope.Protocol;

public sealed record RuntimeSourceSuggestionContext
{
    [JsonConstructor]
    public RuntimeSourceSuggestionContext(
        string? projectPath = null,
        string? viewPath = null,
        string? appXamlPath = null,
        string? profileFilePath = null,
        string? source = null)
    {
        ProjectPath = NormalizePath(projectPath);
        ViewPath = NormalizePath(viewPath);
        AppXamlPath = NormalizePath(appXamlPath);
        ProfileFilePath = NormalizePath(profileFilePath);
        Source = string.IsNullOrWhiteSpace(source) ? "unknown" : source.Trim();
    }

    [JsonPropertyName("projectPath")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ProjectPath { get; }

    [JsonPropertyName("viewPath")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ViewPath { get; }

    [JsonPropertyName("appXamlPath")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? AppXamlPath { get; }

    [JsonPropertyName("profileFilePath")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ProfileFilePath { get; }

    [JsonPropertyName("source")]
    public string Source { get; }

    [JsonIgnore]
    public bool HasAnyPath => ProjectPath is not null
        || ViewPath is not null
        || AppXamlPath is not null
        || ProfileFilePath is not null;

    private static string? NormalizePath(string? path)
    {
        return string.IsNullOrWhiteSpace(path)
            ? null
            : Path.GetFullPath(path.Trim());
    }
}
