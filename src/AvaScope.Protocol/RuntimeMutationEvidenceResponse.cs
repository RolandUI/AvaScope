using System.Text.Json.Serialization;

namespace AvaScope.Protocol;

public sealed record RuntimeMutationEvidenceResponse
{
    public const int MaximumDiagnostics = 16;

    [JsonConstructor]
    public RuntimeMutationEvidenceResponse(
        string requestId,
        SessionId sessionId,
        string topLevelId,
        RuntimeTargetContext target,
        RuntimeMutationResponse mutation,
        RuntimeMutationEvidenceSummary summary,
        string artifactDirectory,
        string beforeScreenshotPath,
        string afterScreenshotPath,
        string beforeVisualTreePath,
        string afterVisualTreePath,
        DateTimeOffset capturedAt,
        string? diffPath = null,
        PreviewDiffResponse? diff = null,
        RuntimeMutationEvidenceTargetSummary? beforeTarget = null,
        RuntimeMutationEvidenceTargetSummary? afterTarget = null,
        IReadOnlyList<ProtocolError>? diagnostics = null,
        RuntimeMutationReviewArtifact? reviewArtifact = null)
    {
        if (string.IsNullOrWhiteSpace(requestId))
        {
            throw new ArgumentException("Mutation evidence request id cannot be empty.", nameof(requestId));
        }

        if (string.IsNullOrWhiteSpace(topLevelId))
        {
            throw new ArgumentException("Top-level id cannot be empty.", nameof(topLevelId));
        }

        if (string.IsNullOrWhiteSpace(artifactDirectory))
        {
            throw new ArgumentException("Artifact directory cannot be empty.", nameof(artifactDirectory));
        }

        if (string.IsNullOrWhiteSpace(beforeScreenshotPath))
        {
            throw new ArgumentException("Before screenshot path cannot be empty.", nameof(beforeScreenshotPath));
        }

        if (string.IsNullOrWhiteSpace(afterScreenshotPath))
        {
            throw new ArgumentException("After screenshot path cannot be empty.", nameof(afterScreenshotPath));
        }

        if (string.IsNullOrWhiteSpace(beforeVisualTreePath))
        {
            throw new ArgumentException("Before visual tree path cannot be empty.", nameof(beforeVisualTreePath));
        }

        if (string.IsNullOrWhiteSpace(afterVisualTreePath))
        {
            throw new ArgumentException("After visual tree path cannot be empty.", nameof(afterVisualTreePath));
        }

        RequestId = requestId.Trim();
        SessionId = sessionId ?? throw new ArgumentNullException(nameof(sessionId));
        TopLevelId = topLevelId.Trim();
        Target = target ?? throw new ArgumentNullException(nameof(target));
        Mutation = mutation ?? throw new ArgumentNullException(nameof(mutation));
        Summary = summary ?? throw new ArgumentNullException(nameof(summary));
        ArtifactDirectory = artifactDirectory;
        BeforeScreenshotPath = beforeScreenshotPath;
        AfterScreenshotPath = afterScreenshotPath;
        BeforeVisualTreePath = beforeVisualTreePath;
        AfterVisualTreePath = afterVisualTreePath;
        CapturedAt = capturedAt;
        DiffPath = string.IsNullOrWhiteSpace(diffPath) ? null : diffPath;
        Diff = diff;
        BeforeTarget = beforeTarget;
        AfterTarget = afterTarget;
        Diagnostics = (diagnostics ?? []).Take(MaximumDiagnostics).ToArray();
        ReviewArtifact = reviewArtifact;
    }

    [JsonPropertyName("requestId")]
    public string RequestId { get; }

    [JsonPropertyName("sessionId")]
    public SessionId SessionId { get; }

    [JsonPropertyName("topLevelId")]
    public string TopLevelId { get; }

    [JsonPropertyName("target")]
    public RuntimeTargetContext Target { get; }

    [JsonPropertyName("mutation")]
    public RuntimeMutationResponse Mutation { get; }

    [JsonPropertyName("summary")]
    public RuntimeMutationEvidenceSummary Summary { get; }

    [JsonPropertyName("artifactDirectory")]
    public string ArtifactDirectory { get; }

    [JsonPropertyName("beforeScreenshotPath")]
    public string BeforeScreenshotPath { get; }

    [JsonPropertyName("afterScreenshotPath")]
    public string AfterScreenshotPath { get; }

    [JsonPropertyName("beforeVisualTreePath")]
    public string BeforeVisualTreePath { get; }

    [JsonPropertyName("afterVisualTreePath")]
    public string AfterVisualTreePath { get; }

    [JsonPropertyName("capturedAt")]
    public DateTimeOffset CapturedAt { get; }

    [JsonPropertyName("diffPath")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? DiffPath { get; }

    [JsonPropertyName("diff")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public PreviewDiffResponse? Diff { get; }

    [JsonPropertyName("beforeTarget")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public RuntimeMutationEvidenceTargetSummary? BeforeTarget { get; }

    [JsonPropertyName("afterTarget")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public RuntimeMutationEvidenceTargetSummary? AfterTarget { get; }

    [JsonPropertyName("diagnostics")]
    public IReadOnlyList<ProtocolError> Diagnostics { get; }

    [JsonPropertyName("reviewArtifact")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public RuntimeMutationReviewArtifact? ReviewArtifact { get; }

    [JsonPropertyName("agentReview")]
    public AgentReviewSurface AgentReview => CreateAgentReview();

    private AgentReviewSurface CreateAgentReview()
    {
        var failures = Diagnostics
            .Concat(Mutation.Diagnostics)
            .Take(AgentReviewSurface.MaximumFailureSummaries)
            .Select(static diagnostic => new AgentReviewFailure("mutation_evidence", diagnostic.Message, diagnostic.Code))
            .ToArray();
        var artifacts = CreateArtifactPaths().ToArray();
        IReadOnlyList<string> reviewUrls = ReviewArtifact is null ? [] : [ReviewArtifact.ReviewUrl];
        var status = Summary.Status;
        var headline = Mutation.Applied
            ? $"Mutation evidence captured for '{Mutation.Operation.Kind}'."
            : $"Mutation evidence captured with mutation status '{Mutation.Status}'.";

        return new AgentReviewSurface(
            status,
            headline,
            [
                $"request: {RequestId}",
                $"mutation: {Mutation.MutationId}",
                $"mutationStatus: {Mutation.Status}",
                $"diffStatus: {Summary.DiffStatus}"
            ],
            failures,
            [CreateMutationSummary(Mutation, active: Mutation.Applied)],
            artifactPaths: artifacts,
            reviewUrls: reviewUrls,
            truncated: Diagnostics.Count + Mutation.Diagnostics.Count > AgentReviewSurface.MaximumFailureSummaries);
    }

    private IEnumerable<AgentReviewPath> CreateArtifactPaths()
    {
        yield return new AgentReviewPath("directory", ArtifactDirectory, description: "Mutation evidence artifact directory.");
        yield return new AgentReviewPath("before_screenshot", BeforeScreenshotPath, description: "Before mutation screenshot.");
        yield return new AgentReviewPath("after_screenshot", AfterScreenshotPath, description: "After mutation screenshot.");
        yield return new AgentReviewPath("before_visual_tree", BeforeVisualTreePath, description: "Before mutation visual tree snapshot.");
        yield return new AgentReviewPath("after_visual_tree", AfterVisualTreePath, description: "After mutation visual tree snapshot.");

        if (DiffPath is not null)
        {
            yield return new AgentReviewPath("diff", DiffPath, description: "Before/after image diff.");
        }

        if (ReviewArtifact is not null)
        {
            yield return new AgentReviewPath(ReviewArtifact.Format, ReviewArtifact.ArtifactPath, ReviewArtifact.ReviewUrl, "Mutation evidence review artifact.");
        }
    }

    private static AgentReviewMutationSummary CreateMutationSummary(RuntimeMutationResponse mutation, bool active)
    {
        return new AgentReviewMutationSummary(
            mutation.MutationId,
            mutation.Operation.Kind,
            mutation.Status,
            mutation.Applied,
            active,
            mutation.Target.NodeId,
            mutation.Operation.PropertyName);
    }
}
