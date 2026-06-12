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
}
