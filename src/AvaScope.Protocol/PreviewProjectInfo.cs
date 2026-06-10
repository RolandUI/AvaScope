using System.Text.Json.Serialization;

namespace AvaScope.Protocol;

public sealed record PreviewProjectInfo
{
    [JsonConstructor]
    public PreviewProjectInfo(
        string projectPath,
        string projectDirectory,
        string assemblyName,
        string? targetFramework = null,
        IReadOnlyList<string>? targetFrameworks = null,
        string? selectedTargetFramework = null,
        string? buildConfiguration = null,
        string? outputAssemblyPath = null,
        string? appXamlPath = null)
    {
        if (string.IsNullOrWhiteSpace(projectPath))
        {
            throw new ArgumentException("Project path cannot be empty.", nameof(projectPath));
        }

        if (string.IsNullOrWhiteSpace(projectDirectory))
        {
            throw new ArgumentException("Project directory cannot be empty.", nameof(projectDirectory));
        }

        if (string.IsNullOrWhiteSpace(assemblyName))
        {
            throw new ArgumentException("Assembly name cannot be empty.", nameof(assemblyName));
        }

        ProjectPath = Path.GetFullPath(projectPath);
        ProjectDirectory = Path.GetFullPath(projectDirectory);
        AssemblyName = assemblyName;
        TargetFramework = string.IsNullOrWhiteSpace(targetFramework) ? null : targetFramework;
        TargetFrameworks = targetFrameworks ?? [];
        SelectedTargetFramework = string.IsNullOrWhiteSpace(selectedTargetFramework) ? null : selectedTargetFramework;
        BuildConfiguration = string.IsNullOrWhiteSpace(buildConfiguration) ? null : buildConfiguration;
        OutputAssemblyPath = string.IsNullOrWhiteSpace(outputAssemblyPath) ? null : Path.GetFullPath(outputAssemblyPath);
        AppXamlPath = string.IsNullOrWhiteSpace(appXamlPath) ? null : Path.GetFullPath(appXamlPath);
    }

    [JsonPropertyName("projectPath")]
    public string ProjectPath { get; init; }

    [JsonPropertyName("projectDirectory")]
    public string ProjectDirectory { get; init; }

    [JsonPropertyName("assemblyName")]
    public string AssemblyName { get; init; }

    [JsonPropertyName("targetFramework")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? TargetFramework { get; init; }

    [JsonPropertyName("targetFrameworks")]
    public IReadOnlyList<string> TargetFrameworks { get; init; }

    [JsonPropertyName("selectedTargetFramework")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? SelectedTargetFramework { get; init; }

    [JsonPropertyName("buildConfiguration")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? BuildConfiguration { get; init; }

    [JsonPropertyName("outputAssemblyPath")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? OutputAssemblyPath { get; init; }

    [JsonPropertyName("appXamlPath")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? AppXamlPath { get; init; }
}
