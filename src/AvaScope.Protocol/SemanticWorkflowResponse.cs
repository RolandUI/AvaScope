using System.Text.Json.Serialization;

namespace AvaScope.Protocol;

public sealed record SemanticWorkflowResponse
{
    [JsonConstructor]
    public SemanticWorkflowResponse(
        string requestId,
        SessionId sessionId,
        string? topLevelId,
        string status,
        DateTimeOffset startedAt,
        DateTimeOffset completedAt,
        IReadOnlyList<SemanticWorkflowStepResult> steps,
        string isolatedStateStatus = "not_configured",
        IReadOnlyList<ProtocolError>? diagnostics = null,
        IReadOnlyDictionary<string, string>? metadata = null,
        ResponseBudgetInfo? responseBudget = null,
        SemanticWorkflowPlan? plan = null,
        AgentEvidenceReportPackResponse? reportPack = null)
    {
        if (string.IsNullOrWhiteSpace(requestId))
        {
            throw new ArgumentException("Workflow request id cannot be empty.", nameof(requestId));
        }

        SessionId = sessionId ?? throw new ArgumentNullException(nameof(sessionId));

        if (string.IsNullOrWhiteSpace(status))
        {
            throw new ArgumentException("Workflow status cannot be empty.", nameof(status));
        }

        RequestId = requestId;
        TopLevelId = string.IsNullOrWhiteSpace(topLevelId) ? null : topLevelId;
        Status = status;
        StartedAt = startedAt;
        CompletedAt = completedAt;
        Steps = steps ?? Array.Empty<SemanticWorkflowStepResult>();
        IsolatedStateStatus = string.IsNullOrWhiteSpace(isolatedStateStatus) ? "not_configured" : isolatedStateStatus;
        Diagnostics = diagnostics ?? Array.Empty<ProtocolError>();
        Metadata = metadata ?? new Dictionary<string, string>();
        ResponseBudget = responseBudget;
        Plan = plan;
        ReportPack = reportPack;
    }

    [JsonPropertyName("requestId")]
    public string RequestId { get; }

    [JsonPropertyName("sessionId")]
    public SessionId SessionId { get; }

    [JsonPropertyName("topLevelId")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? TopLevelId { get; }

    [JsonPropertyName("status")]
    public string Status { get; }

    [JsonPropertyName("startedAt")]
    public DateTimeOffset StartedAt { get; }

    [JsonPropertyName("completedAt")]
    public DateTimeOffset CompletedAt { get; }

    [JsonPropertyName("steps")]
    public IReadOnlyList<SemanticWorkflowStepResult> Steps { get; }

    [JsonPropertyName("isolatedStateStatus")]
    public string IsolatedStateStatus { get; }

    [JsonPropertyName("diagnostics")]
    public IReadOnlyList<ProtocolError> Diagnostics { get; }

    [JsonPropertyName("metadata")]
    public IReadOnlyDictionary<string, string> Metadata { get; }

    [JsonPropertyName("responseBudget")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ResponseBudgetInfo? ResponseBudget { get; }

    [JsonPropertyName("plan")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public SemanticWorkflowPlan? Plan { get; }

    [JsonPropertyName("reportPack")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public AgentEvidenceReportPackResponse? ReportPack { get; }

    [JsonPropertyName("agentReview")]
    public AgentReviewSurface AgentReview => CreateAgentReview();

    private AgentReviewSurface CreateAgentReview()
    {
        var failedSteps = Steps
            .Where(static step => string.Equals(step.Status, "failed", StringComparison.Ordinal))
            .ToArray();
        var allFailures = failedSteps
            .Select(step => new AgentReviewFailure(
                "semantic_workflow",
                step.Message,
                step.Diagnostics.FirstOrDefault()?.Code,
                step.FailureEvidence?.WorkflowContextPath))
            .Concat(Diagnostics.Select(static diagnostic => new AgentReviewFailure(
                "semantic_workflow",
                diagnostic.Message,
                diagnostic.Code)))
            .Concat(Steps.SelectMany(static step =>
                (step.Verification?.Diagnostics ?? [])
                    .Concat(step.FailureEvidence?.Diagnostics ?? [])
                    .Select(diagnostic => new AgentReviewFailure(
                        "semantic_workflow_evidence",
                        diagnostic.Message,
                        diagnostic.Code,
                        step.FailureEvidence?.WorkflowContextPath))))
            .DistinctBy(static failure => (failure.Scope, failure.Code, failure.Message, failure.Path))
            .ToArray();
        var failures = allFailures.Take(AgentReviewSurface.MaximumFailureSummaries).ToArray();
        var reports = ReportPack?.Assets
            .Select(static asset => new AgentReviewPath(asset.Kind, asset.Path, asset.Url, asset.Description))
            .Take(AgentReviewSurface.MaximumPaths)
            .ToArray() ?? [];
        var allArtifacts = Steps
            .SelectMany(static step => EvidencePaths(step))
            .ToArray();
        var artifacts = allArtifacts.Take(AgentReviewSurface.MaximumPaths).ToArray();
        var reviewStatus = string.Equals(Status, "passed", StringComparison.Ordinal)
            && allFailures.Length > 0
                ? "partial"
                : Status;
        return new AgentReviewSurface(
            reviewStatus,
            string.Equals(reviewStatus, "passed", StringComparison.Ordinal)
                ? "Semantic workflow passed."
                : string.Equals(reviewStatus, "validated", StringComparison.Ordinal)
                    ? "Semantic workflow definition is valid."
                    : string.Equals(reviewStatus, "partial", StringComparison.Ordinal)
                        ? "Semantic workflow passed with partial evidence."
                    : "Semantic workflow requires review.",
            [
                $"steps: {Steps.Count}",
                $"failedSteps: {failedSteps.Length}",
                $"verificationSteps: {Steps.Count(static step => step.Verification is not null)}",
                $"failureEvidencePacks: {Steps.Count(static step => step.FailureEvidence is not null)}"
            ],
            failures,
            reportPaths: reports,
            artifactPaths: artifacts,
            reviewUrls: reports.Select(static report => report.Url).Where(static url => url is not null).Cast<string>().ToArray(),
            truncated: allFailures.Length > AgentReviewSurface.MaximumFailureSummaries
                || allArtifacts.Length > AgentReviewSurface.MaximumPaths);
    }

    private static IEnumerable<AgentReviewPath> EvidencePaths(SemanticWorkflowStepResult step)
    {
        if (step.Verification?.BeforeScreenshot is not null)
        {
            yield return new AgentReviewPath("verification_before_screenshot", step.Verification.BeforeScreenshot.FilePath);
        }

        if (step.Verification?.AfterScreenshot is not null)
        {
            yield return new AgentReviewPath("verification_after_screenshot", step.Verification.AfterScreenshot.FilePath);
        }

        if (step.FailureEvidence is not { } evidence)
        {
            yield break;
        }

        yield return new AgentReviewPath("failure_evidence_directory", evidence.ArtifactDirectory);
        foreach (var path in new[]
        {
            ("failure_inspection", evidence.InspectionPath),
            ("failure_screenshot", evidence.ScreenshotPath),
            ("failure_visual_tree", evidence.VisualTreePath),
            ("failure_selector_candidates", evidence.SelectorCandidatesPath),
            ("failure_active_top_levels", evidence.ActiveTopLevelsPath),
            ("failure_workflow_context", evidence.WorkflowContextPath)
        })
        {
            if (path.Item2 is not null)
            {
                yield return new AgentReviewPath(path.Item1, path.Item2);
            }
        }
    }
}
