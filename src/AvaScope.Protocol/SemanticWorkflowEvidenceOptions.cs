using System.Text.Json.Serialization;

namespace AvaScope.Protocol;

public sealed record SemanticWorkflowEvidenceOptions
{
    public const int MaximumTreeDepth = 8;
    public const int MaximumSelectorCandidates = 8;

    [JsonConstructor]
    public SemanticWorkflowEvidenceOptions(
        bool captureOnFailure = true,
        bool includeScreenshot = true,
        bool includeVisualTree = true,
        bool includeActiveTopLevels = true,
        bool includeSelectorCandidates = true,
        bool exportReports = true,
        string? reportDirectory = null,
        int treeDepth = 4,
        int maxSelectorCandidates = MaximumSelectorCandidates)
    {
        if (treeDepth is < 0 or > MaximumTreeDepth)
        {
            throw new ArgumentOutOfRangeException(nameof(treeDepth), treeDepth, $"Evidence tree depth must be between 0 and {MaximumTreeDepth}.");
        }

        if (maxSelectorCandidates is < 1 or > MaximumSelectorCandidates)
        {
            throw new ArgumentOutOfRangeException(nameof(maxSelectorCandidates), maxSelectorCandidates, $"Evidence selector candidates must be between 1 and {MaximumSelectorCandidates}.");
        }

        CaptureOnFailure = captureOnFailure;
        IncludeScreenshot = includeScreenshot;
        IncludeVisualTree = includeVisualTree;
        IncludeActiveTopLevels = includeActiveTopLevels;
        IncludeSelectorCandidates = includeSelectorCandidates;
        ExportReports = exportReports;
        ReportDirectory = string.IsNullOrWhiteSpace(reportDirectory) ? null : Path.GetFullPath(reportDirectory);
        TreeDepth = treeDepth;
        MaxSelectorCandidates = maxSelectorCandidates;
    }

    [JsonPropertyName("captureOnFailure")]
    public bool CaptureOnFailure { get; }

    [JsonPropertyName("includeScreenshot")]
    public bool IncludeScreenshot { get; }

    [JsonPropertyName("includeVisualTree")]
    public bool IncludeVisualTree { get; }

    [JsonPropertyName("includeActiveTopLevels")]
    public bool IncludeActiveTopLevels { get; }

    [JsonPropertyName("includeSelectorCandidates")]
    public bool IncludeSelectorCandidates { get; }

    [JsonPropertyName("exportReports")]
    public bool ExportReports { get; }

    [JsonPropertyName("reportDirectory")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ReportDirectory { get; }

    [JsonPropertyName("treeDepth")]
    public int TreeDepth { get; }

    [JsonPropertyName("maxSelectorCandidates")]
    public int MaxSelectorCandidates { get; }
}
