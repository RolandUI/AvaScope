using System.Text.Json.Serialization;

namespace AvaScope.Protocol;

public sealed record AgentEvidenceReportPackResponse
{
    [JsonConstructor]
    public AgentEvidenceReportPackResponse(
        string reportDirectory,
        string status,
        DateTimeOffset generatedAt,
        int totalEntries,
        int passedEntries,
        int failedEntries,
        IReadOnlyList<AgentEvidenceReportPackAsset>? assets,
        IReadOnlyDictionary<string, string>? environmentMetadata = null,
        IReadOnlyDictionary<string, string>? metadata = null)
    {
        if (string.IsNullOrWhiteSpace(reportDirectory))
        {
            throw new ArgumentException("Report pack directory cannot be empty.", nameof(reportDirectory));
        }

        if (string.IsNullOrWhiteSpace(status))
        {
            throw new ArgumentException("Report pack status cannot be empty.", nameof(status));
        }

        if (totalEntries < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(totalEntries), totalEntries, "Total entries cannot be negative.");
        }

        if (passedEntries < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(passedEntries), passedEntries, "Passed entries cannot be negative.");
        }

        if (failedEntries < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(failedEntries), failedEntries, "Failed entries cannot be negative.");
        }

        if (passedEntries + failedEntries > totalEntries)
        {
            throw new ArgumentException("Passed and failed entries cannot exceed total entries.", nameof(failedEntries));
        }

        ReportDirectory = Path.GetFullPath(reportDirectory);
        Status = status.Trim();
        GeneratedAt = generatedAt;
        TotalEntries = totalEntries;
        PassedEntries = passedEntries;
        FailedEntries = failedEntries;
        Assets = assets ?? [];
        EnvironmentMetadata = environmentMetadata ?? new Dictionary<string, string>();
        Metadata = metadata ?? new Dictionary<string, string>();
    }

    [JsonPropertyName("reportDirectory")]
    public string ReportDirectory { get; }

    [JsonPropertyName("status")]
    public string Status { get; }

    [JsonPropertyName("generatedAt")]
    public DateTimeOffset GeneratedAt { get; }

    [JsonPropertyName("totalEntries")]
    public int TotalEntries { get; }

    [JsonPropertyName("passedEntries")]
    public int PassedEntries { get; }

    [JsonPropertyName("failedEntries")]
    public int FailedEntries { get; }

    [JsonPropertyName("assets")]
    public IReadOnlyList<AgentEvidenceReportPackAsset> Assets { get; }

    [JsonPropertyName("environmentMetadata")]
    public IReadOnlyDictionary<string, string> EnvironmentMetadata { get; }

    [JsonPropertyName("metadata")]
    public IReadOnlyDictionary<string, string> Metadata { get; }
}
