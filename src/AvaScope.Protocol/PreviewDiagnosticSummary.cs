using System.Text.Json.Serialization;

namespace AvaScope.Protocol;

public sealed record PreviewDiagnosticSummary
{
    [JsonConstructor]
    public PreviewDiagnosticSummary(
        int totalCount,
        IReadOnlyDictionary<string, int> severityCounts,
        IReadOnlyDictionary<string, int> categoryCounts,
        string summary,
        string comparisonProvenance = "unavailable",
        int? newCount = null,
        int? existingCount = null,
        int? resolvedCount = null,
        bool truncated = false,
        int inlineCount = 0,
        int? baselineCount = null,
        ProtocolError? comparisonError = null)
    {
        TotalCount = totalCount;
        SeverityCounts = severityCounts ?? new Dictionary<string, int>();
        CategoryCounts = categoryCounts ?? new Dictionary<string, int>();
        Summary = summary;
        ComparisonProvenance = comparisonProvenance;
        NewCount = newCount;
        ExistingCount = existingCount;
        ResolvedCount = resolvedCount;
        Truncated = truncated;
        InlineCount = inlineCount;
        BaselineCount = baselineCount;
        ComparisonError = comparisonError;
    }

    [JsonPropertyName("totalCount")] public int TotalCount { get; }
    [JsonPropertyName("severityCounts")] public IReadOnlyDictionary<string, int> SeverityCounts { get; }
    [JsonPropertyName("categoryCounts")] public IReadOnlyDictionary<string, int> CategoryCounts { get; }
    [JsonPropertyName("summary")] public string Summary { get; }
    [JsonPropertyName("comparisonProvenance")] public string ComparisonProvenance { get; }
    [JsonPropertyName("newCount")] public int? NewCount { get; }
    [JsonPropertyName("existingCount")] public int? ExistingCount { get; }
    [JsonPropertyName("resolvedCount")] public int? ResolvedCount { get; }
    [JsonPropertyName("truncated")] public bool Truncated { get; }
    [JsonPropertyName("inlineCount")] public int InlineCount { get; }
    [JsonPropertyName("baselineCount")] public int? BaselineCount { get; }

    [JsonPropertyName("comparisonError")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ProtocolError? ComparisonError { get; }
}
