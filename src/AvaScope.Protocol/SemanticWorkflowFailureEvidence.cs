using System.Text.Json.Serialization;

namespace AvaScope.Protocol;

public sealed record SemanticWorkflowFailureEvidence
{
    [JsonConstructor]
    public SemanticWorkflowFailureEvidence(
        string status,
        string artifactDirectory,
        string? inspectionPath = null,
        string? screenshotPath = null,
        string? visualTreePath = null,
        string? selectorCandidatesPath = null,
        string? activeTopLevelsPath = null,
        string? workflowContextPath = null,
        IReadOnlyList<string>? unavailableEvidence = null,
        IReadOnlyList<ProtocolError>? diagnostics = null)
    {
        if (string.IsNullOrWhiteSpace(status))
        {
            throw new ArgumentException("Failure evidence status cannot be empty.", nameof(status));
        }

        if (string.IsNullOrWhiteSpace(artifactDirectory))
        {
            throw new ArgumentException("Failure evidence artifact directory cannot be empty.", nameof(artifactDirectory));
        }

        Status = status.Trim();
        ArtifactDirectory = Path.GetFullPath(artifactDirectory);
        InspectionPath = FullPathOrNull(inspectionPath);
        ScreenshotPath = FullPathOrNull(screenshotPath);
        VisualTreePath = FullPathOrNull(visualTreePath);
        SelectorCandidatesPath = FullPathOrNull(selectorCandidatesPath);
        ActiveTopLevelsPath = FullPathOrNull(activeTopLevelsPath);
        WorkflowContextPath = FullPathOrNull(workflowContextPath);
        UnavailableEvidence = unavailableEvidence ?? [];
        Diagnostics = diagnostics ?? [];
    }

    [JsonPropertyName("status")]
    public string Status { get; }

    [JsonPropertyName("artifactDirectory")]
    public string ArtifactDirectory { get; }

    [JsonPropertyName("inspectionPath")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? InspectionPath { get; }

    [JsonPropertyName("screenshotPath")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ScreenshotPath { get; }

    [JsonPropertyName("visualTreePath")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? VisualTreePath { get; }

    [JsonPropertyName("selectorCandidatesPath")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? SelectorCandidatesPath { get; }

    [JsonPropertyName("activeTopLevelsPath")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ActiveTopLevelsPath { get; }

    [JsonPropertyName("workflowContextPath")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? WorkflowContextPath { get; }

    [JsonPropertyName("unavailableEvidence")]
    public IReadOnlyList<string> UnavailableEvidence { get; }

    [JsonPropertyName("diagnostics")]
    public IReadOnlyList<ProtocolError> Diagnostics { get; }

    private static string? FullPathOrNull(string? path) =>
        string.IsNullOrWhiteSpace(path) ? null : Path.GetFullPath(path);
}
