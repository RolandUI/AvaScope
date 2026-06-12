using System.Text.Json.Serialization;

namespace AvaScope.Protocol;

public sealed record AgentReviewSurface
{
    public const int MaximumSummaryLines = 8;
    public const int MaximumFailureSummaries = 8;
    public const int MaximumMutationSummaries = 8;
    public const int MaximumPaths = 16;
    public const int MaximumUrls = 16;

    [JsonConstructor]
    public AgentReviewSurface(
        string status,
        string headline,
        IReadOnlyList<string>? summary = null,
        IReadOnlyList<AgentReviewFailure>? failures = null,
        IReadOnlyList<AgentReviewMutationSummary>? mutations = null,
        IReadOnlyList<AgentReviewPath>? reportPaths = null,
        IReadOnlyList<AgentReviewPath>? artifactPaths = null,
        IReadOnlyList<string>? reviewUrls = null,
        IReadOnlyList<string>? previewUrls = null,
        bool truncated = false)
    {
        if (string.IsNullOrWhiteSpace(status))
        {
            throw new ArgumentException("Agent review status cannot be empty.", nameof(status));
        }

        if (string.IsNullOrWhiteSpace(headline))
        {
            throw new ArgumentException("Agent review headline cannot be empty.", nameof(headline));
        }

        Status = status.Trim();
        Headline = headline.Trim();
        Summary = (summary ?? [])
            .Where(static item => !string.IsNullOrWhiteSpace(item))
            .Select(static item => item.Trim())
            .Take(MaximumSummaryLines)
            .ToArray();
        Failures = (failures ?? []).Take(MaximumFailureSummaries).ToArray();
        Mutations = (mutations ?? []).Take(MaximumMutationSummaries).ToArray();
        ReportPaths = (reportPaths ?? []).Take(MaximumPaths).ToArray();
        ArtifactPaths = (artifactPaths ?? []).Take(MaximumPaths).ToArray();
        ReviewUrls = NormalizeUrls(reviewUrls);
        PreviewUrls = NormalizeUrls(previewUrls);
        Truncated = truncated
            || (summary?.Count ?? 0) > MaximumSummaryLines
            || (failures?.Count ?? 0) > MaximumFailureSummaries
            || (mutations?.Count ?? 0) > MaximumMutationSummaries
            || (reportPaths?.Count ?? 0) > MaximumPaths
            || (artifactPaths?.Count ?? 0) > MaximumPaths
            || (reviewUrls?.Count ?? 0) > MaximumUrls
            || (previewUrls?.Count ?? 0) > MaximumUrls;
    }

    [JsonPropertyName("status")]
    public string Status { get; }

    [JsonPropertyName("headline")]
    public string Headline { get; }

    [JsonPropertyName("summary")]
    public IReadOnlyList<string> Summary { get; }

    [JsonPropertyName("failures")]
    public IReadOnlyList<AgentReviewFailure> Failures { get; }

    [JsonPropertyName("mutations")]
    public IReadOnlyList<AgentReviewMutationSummary> Mutations { get; }

    [JsonPropertyName("reportPaths")]
    public IReadOnlyList<AgentReviewPath> ReportPaths { get; }

    [JsonPropertyName("artifactPaths")]
    public IReadOnlyList<AgentReviewPath> ArtifactPaths { get; }

    [JsonPropertyName("reviewUrls")]
    public IReadOnlyList<string> ReviewUrls { get; }

    [JsonPropertyName("previewUrls")]
    public IReadOnlyList<string> PreviewUrls { get; }

    [JsonPropertyName("truncated")]
    public bool Truncated { get; }

    private static IReadOnlyList<string> NormalizeUrls(IReadOnlyList<string>? urls)
    {
        return (urls ?? [])
            .Where(static item => !string.IsNullOrWhiteSpace(item))
            .Select(static item => item.Trim())
            .Distinct(StringComparer.Ordinal)
            .Take(MaximumUrls)
            .ToArray();
    }
}
