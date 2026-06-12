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
        IReadOnlyList<ProtocolError>? diagnostics = null)
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
}
