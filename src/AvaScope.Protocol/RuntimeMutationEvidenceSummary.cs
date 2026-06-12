using System.Text.Json.Serialization;

namespace AvaScope.Protocol;

public sealed record RuntimeMutationEvidenceSummary
{
    [JsonConstructor]
    public RuntimeMutationEvidenceSummary(
        string status,
        string mutationStatus,
        bool mutationApplied,
        bool screenshotsCaptured,
        bool visualTreeSnapshotsCaptured,
        string diffStatus,
        int beforeVisualTreeNodeCount,
        int afterVisualTreeNodeCount,
        bool beforeTargetFound,
        bool afterTargetFound,
        long? changedPixels = null,
        double? changedPixelPercentage = null)
    {
        if (string.IsNullOrWhiteSpace(status))
        {
            throw new ArgumentException("Mutation evidence status cannot be empty.", nameof(status));
        }

        if (string.IsNullOrWhiteSpace(mutationStatus))
        {
            throw new ArgumentException("Mutation status cannot be empty.", nameof(mutationStatus));
        }

        if (string.IsNullOrWhiteSpace(diffStatus))
        {
            throw new ArgumentException("Mutation evidence diff status cannot be empty.", nameof(diffStatus));
        }

        Status = status.Trim();
        MutationStatus = mutationStatus.Trim();
        MutationApplied = mutationApplied;
        ScreenshotsCaptured = screenshotsCaptured;
        VisualTreeSnapshotsCaptured = visualTreeSnapshotsCaptured;
        DiffStatus = diffStatus.Trim();
        BeforeVisualTreeNodeCount = beforeVisualTreeNodeCount;
        AfterVisualTreeNodeCount = afterVisualTreeNodeCount;
        BeforeTargetFound = beforeTargetFound;
        AfterTargetFound = afterTargetFound;
        ChangedPixels = changedPixels;
        ChangedPixelPercentage = changedPixelPercentage;
    }

    [JsonPropertyName("status")]
    public string Status { get; }

    [JsonPropertyName("mutationStatus")]
    public string MutationStatus { get; }

    [JsonPropertyName("mutationApplied")]
    public bool MutationApplied { get; }

    [JsonPropertyName("screenshotsCaptured")]
    public bool ScreenshotsCaptured { get; }

    [JsonPropertyName("visualTreeSnapshotsCaptured")]
    public bool VisualTreeSnapshotsCaptured { get; }

    [JsonPropertyName("diffStatus")]
    public string DiffStatus { get; }

    [JsonPropertyName("beforeVisualTreeNodeCount")]
    public int BeforeVisualTreeNodeCount { get; }

    [JsonPropertyName("afterVisualTreeNodeCount")]
    public int AfterVisualTreeNodeCount { get; }

    [JsonPropertyName("beforeTargetFound")]
    public bool BeforeTargetFound { get; }

    [JsonPropertyName("afterTargetFound")]
    public bool AfterTargetFound { get; }

    [JsonPropertyName("changedPixels")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public long? ChangedPixels { get; }

    [JsonPropertyName("changedPixelPercentage")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? ChangedPixelPercentage { get; }
}
