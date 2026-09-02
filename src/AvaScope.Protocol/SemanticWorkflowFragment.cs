using System.Text.Json.Serialization;

namespace AvaScope.Protocol;

public sealed record SemanticWorkflowFragment
{
    [JsonConstructor]
    public SemanticWorkflowFragment(
        string name,
        IReadOnlyList<SemanticWorkflowStep> steps,
        IReadOnlyList<string>? parameters = null)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Workflow fragment name cannot be empty.", nameof(name));
        }

        if (steps is null || steps.Count == 0)
        {
            throw new ArgumentException("Workflow fragment requires at least one step.", nameof(steps));
        }

        Name = name.Trim();
        Steps = steps;
        Parameters = (parameters ?? Array.Empty<string>())
            .Select(static parameter => parameter?.Trim() ?? string.Empty)
            .ToArray();
    }

    [JsonPropertyName("name")]
    public string Name { get; }

    [JsonPropertyName("parameters")]
    public IReadOnlyList<string> Parameters { get; }

    [JsonPropertyName("steps")]
    public IReadOnlyList<SemanticWorkflowStep> Steps { get; }
}
