using System.Text.Json.Serialization;

namespace AvaScope.Protocol;

public sealed record RuntimeMutationReviewResponse
{
    public const int MaximumEntries = 100;
    public const int MaximumMetadataEntries = 32;

    [JsonConstructor]
    public RuntimeMutationReviewResponse(
        SessionId sessionId,
        DateTimeOffset reviewedAt,
        int historyCount,
        int activeMutationCount,
        IReadOnlyList<RuntimeMutationReviewEntry>? history = null,
        IReadOnlyList<RuntimeMutationReviewEntry>? activeMutations = null,
        RuntimeMutationResetHandoff? resetHandoff = null,
        IReadOnlyDictionary<string, string>? metadata = null,
        RuntimeMutationReviewArtifact? reviewArtifact = null)
    {
        if (historyCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(historyCount), historyCount, "History count cannot be negative.");
        }

        if (activeMutationCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(activeMutationCount), activeMutationCount, "Active mutation count cannot be negative.");
        }

        SessionId = sessionId ?? throw new ArgumentNullException(nameof(sessionId));
        ReviewedAt = reviewedAt;
        HistoryCount = historyCount;
        ActiveMutationCount = activeMutationCount;
        History = (history ?? []).Take(MaximumEntries).ToArray();
        ActiveMutations = (activeMutations ?? []).Take(MaximumEntries).ToArray();
        ResetHandoff = resetHandoff ?? new RuntimeMutationResetHandoff(SessionId, activeMutationCount);
        Metadata = metadata is null
            ? new Dictionary<string, string>()
            : metadata.Take(MaximumMetadataEntries).ToDictionary(
                static item => item.Key,
                static item => item.Value,
                StringComparer.Ordinal);
        ReviewArtifact = reviewArtifact;
    }

    [JsonPropertyName("sessionId")]
    public SessionId SessionId { get; }

    [JsonPropertyName("reviewedAt")]
    public DateTimeOffset ReviewedAt { get; }

    [JsonPropertyName("historyCount")]
    public int HistoryCount { get; }

    [JsonPropertyName("activeMutationCount")]
    public int ActiveMutationCount { get; }

    [JsonPropertyName("history")]
    public IReadOnlyList<RuntimeMutationReviewEntry> History { get; }

    [JsonPropertyName("activeMutations")]
    public IReadOnlyList<RuntimeMutationReviewEntry> ActiveMutations { get; }

    [JsonPropertyName("resetHandoff")]
    public RuntimeMutationResetHandoff ResetHandoff { get; }

    [JsonPropertyName("metadata")]
    public IReadOnlyDictionary<string, string> Metadata { get; }

    [JsonPropertyName("reviewArtifact")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public RuntimeMutationReviewArtifact? ReviewArtifact { get; }

    [JsonPropertyName("agentReview")]
    public AgentReviewSurface AgentReview => CreateAgentReview();

    private AgentReviewSurface CreateAgentReview()
    {
        var mutationSummaries = ActiveMutations
            .Take(AgentReviewSurface.MaximumMutationSummaries)
            .Select(static entry => new AgentReviewMutationSummary(
                entry.MutationId,
                entry.Operation.Kind,
                entry.Status,
                entry.Applied,
                entry.Active,
                entry.Target.NodeId,
                entry.Operation.PropertyName))
            .ToArray();
        var failures = History
            .SelectMany(static entry => entry.Diagnostics.Select(diagnostic => new AgentReviewFailure(
                $"mutation:{entry.Sequence.ToString(System.Globalization.CultureInfo.InvariantCulture)}",
                diagnostic.Message,
                diagnostic.Code)))
            .Take(AgentReviewSurface.MaximumFailureSummaries)
            .ToArray();
        IReadOnlyList<AgentReviewPath> artifactPaths = ReviewArtifact is null
            ? []
            : [new AgentReviewPath(ReviewArtifact.Format, ReviewArtifact.ArtifactPath, ReviewArtifact.ReviewUrl, "Runtime mutation review artifact.")];
        var status = ActiveMutationCount == 0 ? "clean" : "active_mutations";
        var headline = ActiveMutationCount == 0
            ? "No active runtime mutations."
            : $"{ActiveMutationCount.ToString(System.Globalization.CultureInfo.InvariantCulture)} active runtime mutations.";

        return new AgentReviewSurface(
            status,
            headline,
            [
                $"history: {HistoryCount.ToString(System.Globalization.CultureInfo.InvariantCulture)}",
                $"active: {ActiveMutationCount.ToString(System.Globalization.CultureInfo.InvariantCulture)}",
                $"resetOperation: {ResetHandoff.ResetAllOperation}"
            ],
            failures,
            mutationSummaries,
            artifactPaths: artifactPaths,
            reviewUrls: ReviewArtifact is null ? [] : [ReviewArtifact.ReviewUrl],
            truncated: ActiveMutations.Count > AgentReviewSurface.MaximumMutationSummaries
                || History.Sum(static entry => entry.Diagnostics.Count) > AgentReviewSurface.MaximumFailureSummaries);
    }
}
