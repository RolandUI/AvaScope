using System.Text.Json.Serialization;

namespace AvaScope.Protocol;

public sealed record PreviewBaselineCheckResponse
{
    [JsonConstructor]
    public PreviewBaselineCheckResponse(
        string manifestPath,
        bool passed,
        IReadOnlyList<PreviewBaselineCheckEntry>? entries,
        DateTimeOffset checkedAt,
        string? reportPath = null,
        AgentEvidenceReportPackResponse? reportPack = null)
    {
        if (string.IsNullOrWhiteSpace(manifestPath))
        {
            throw new ArgumentException("Baseline manifest path cannot be empty.", nameof(manifestPath));
        }

        ManifestPath = Path.GetFullPath(manifestPath);
        Passed = passed;
        Entries = entries ?? [];
        CheckedAt = checkedAt;
        ReportPath = string.IsNullOrWhiteSpace(reportPath) ? null : Path.GetFullPath(reportPath);
        ReportPack = reportPack;
    }

    [JsonPropertyName("manifestPath")]
    public string ManifestPath { get; }

    [JsonPropertyName("passed")]
    public bool Passed { get; }

    [JsonPropertyName("entries")]
    public IReadOnlyList<PreviewBaselineCheckEntry> Entries { get; }

    [JsonPropertyName("checkedAt")]
    public DateTimeOffset CheckedAt { get; }

    [JsonPropertyName("reportPath")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ReportPath { get; }

    [JsonPropertyName("reportPack")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public AgentEvidenceReportPackResponse? ReportPack { get; }
}
