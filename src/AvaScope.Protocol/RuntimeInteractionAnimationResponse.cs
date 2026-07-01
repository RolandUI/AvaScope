using System.Text.Json.Serialization;

namespace AvaScope.Protocol;

public sealed record RuntimeInteractionAnimationResponse
{
    public const int MaximumDiagnostics = 64;

    [JsonConstructor]
    public RuntimeInteractionAnimationResponse(
        string requestId,
        SessionId sessionId,
        string topLevelId,
        string status,
        DateTimeOffset startedAt,
        DateTimeOffset completedAt,
        IReadOnlyList<RuntimeInteractionAnimationStepResult>? steps = null,
        IReadOnlyList<RuntimeInteractionGeometryAssertionResult>? assertions = null,
        string? frameStripPath = null,
        IReadOnlyList<ProtocolError>? diagnostics = null,
        IReadOnlyDictionary<string, string>? metadata = null)
    {
        if (string.IsNullOrWhiteSpace(requestId))
        {
            throw new ArgumentException("Interaction animation request id cannot be empty.", nameof(requestId));
        }

        if (string.IsNullOrWhiteSpace(topLevelId))
        {
            throw new ArgumentException("Top-level id cannot be empty.", nameof(topLevelId));
        }

        if (string.IsNullOrWhiteSpace(status))
        {
            throw new ArgumentException("Interaction animation status cannot be empty.", nameof(status));
        }

        RequestId = requestId.Trim();
        SessionId = sessionId ?? throw new ArgumentNullException(nameof(sessionId));
        TopLevelId = topLevelId.Trim();
        Status = status.Trim();
        StartedAt = startedAt;
        CompletedAt = completedAt;
        Steps = steps ?? [];
        Assertions = assertions ?? [];
        FrameStripPath = string.IsNullOrWhiteSpace(frameStripPath) ? null : Path.GetFullPath(frameStripPath);
        Diagnostics = (diagnostics ?? []).Take(MaximumDiagnostics).ToArray();
        Metadata = metadata ?? new Dictionary<string, string>();
    }

    [JsonPropertyName("requestId")]
    public string RequestId { get; }

    [JsonPropertyName("sessionId")]
    public SessionId SessionId { get; }

    [JsonPropertyName("topLevelId")]
    public string TopLevelId { get; }

    [JsonPropertyName("status")]
    public string Status { get; }

    [JsonPropertyName("startedAt")]
    public DateTimeOffset StartedAt { get; }

    [JsonPropertyName("completedAt")]
    public DateTimeOffset CompletedAt { get; }

    [JsonPropertyName("steps")]
    public IReadOnlyList<RuntimeInteractionAnimationStepResult> Steps { get; }

    [JsonPropertyName("assertions")]
    public IReadOnlyList<RuntimeInteractionGeometryAssertionResult> Assertions { get; }

    [JsonPropertyName("frameStripPath")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? FrameStripPath { get; }

    [JsonPropertyName("diagnostics")]
    public IReadOnlyList<ProtocolError> Diagnostics { get; }

    [JsonPropertyName("metadata")]
    public IReadOnlyDictionary<string, string> Metadata { get; }

    [JsonPropertyName("agentReview")]
    public AgentReviewSurface AgentReview => CreateAgentReview();

    private AgentReviewSurface CreateAgentReview()
    {
        var assertionFailures = Assertions
            .Where(static assertion => assertion.Status != "passed")
            .Select(static assertion => new AgentReviewFailure("interaction_animation", assertion.Message, "interaction_geometry_assertion_failed"));
        var diagnosticFailures = Diagnostics
            .Select(static diagnostic => new AgentReviewFailure("interaction_animation", diagnostic.Message, diagnostic.Code));
        var failures = assertionFailures
            .Concat(diagnosticFailures)
            .Take(AgentReviewSurface.MaximumFailureSummaries)
            .ToArray();

        var artifacts = Steps
            .SelectMany(static step => step.ArtifactPaths())
            .Concat(FrameStripPath is null ? [] : [new AgentReviewPath("frame_strip", FrameStripPath, description: "Interaction-triggered animation frame strip.")])
            .Take(AgentReviewSurface.MaximumPaths)
            .ToArray();

        return new AgentReviewSurface(
            Status,
            $"Interaction animation '{RequestId}' completed with status '{Status}'.",
            [
                $"steps: {Steps.Count}",
                $"frames: {Steps.Sum(static step => step.Frames.Count)}",
                $"assertions: {Assertions.Count}"
            ],
            failures,
            artifactPaths: artifacts,
            truncated: Diagnostics.Count + Assertions.Count(static assertion => assertion.Status != "passed") > AgentReviewSurface.MaximumFailureSummaries
                || Steps.Sum(static step => step.ArtifactPaths().Count()) + (FrameStripPath is null ? 0 : 1) > AgentReviewSurface.MaximumPaths);
    }
}

public sealed record RuntimeInteractionAnimationStepResult
{
    public const int MaximumFrames = 24;
    public const int MaximumDiagnostics = 16;

    [JsonConstructor]
    public RuntimeInteractionAnimationStepResult(
        string stepId,
        string action,
        string status,
        string message,
        DateTimeOffset executedAt,
        InputResponse? input = null,
        IReadOnlyList<RuntimeInteractionAnimationFrame>? frames = null,
        IReadOnlyList<ProtocolError>? diagnostics = null,
        IReadOnlyDictionary<string, string>? metadata = null)
    {
        if (string.IsNullOrWhiteSpace(stepId))
        {
            throw new ArgumentException("Step id cannot be empty.", nameof(stepId));
        }

        if (string.IsNullOrWhiteSpace(action))
        {
            throw new ArgumentException("Step action cannot be empty.", nameof(action));
        }

        if (string.IsNullOrWhiteSpace(status))
        {
            throw new ArgumentException("Step status cannot be empty.", nameof(status));
        }

        StepId = stepId.Trim();
        Action = action.Trim();
        Status = status.Trim();
        Message = string.IsNullOrWhiteSpace(message) ? Status : message.Trim();
        ExecutedAt = executedAt;
        Input = input;
        Frames = (frames ?? []).Take(MaximumFrames).ToArray();
        Diagnostics = (diagnostics ?? []).Take(MaximumDiagnostics).ToArray();
        Metadata = metadata ?? new Dictionary<string, string>();
    }

    [JsonPropertyName("stepId")]
    public string StepId { get; }

    [JsonPropertyName("action")]
    public string Action { get; }

    [JsonPropertyName("status")]
    public string Status { get; }

    [JsonPropertyName("message")]
    public string Message { get; }

    [JsonPropertyName("executedAt")]
    public DateTimeOffset ExecutedAt { get; }

    [JsonPropertyName("input")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public InputResponse? Input { get; }

    [JsonPropertyName("frames")]
    public IReadOnlyList<RuntimeInteractionAnimationFrame> Frames { get; }

    [JsonPropertyName("diagnostics")]
    public IReadOnlyList<ProtocolError> Diagnostics { get; }

    [JsonPropertyName("metadata")]
    public IReadOnlyDictionary<string, string> Metadata { get; }

    internal IEnumerable<AgentReviewPath> ArtifactPaths()
    {
        foreach (var frame in Frames)
        {
            if (frame.Screenshot is not null)
            {
                yield return new AgentReviewPath("interaction_frame", frame.Screenshot.FilePath, description: $"Interaction animation frame '{frame.FrameId}'.");
            }

            if (frame.GeometryOverlayPath is not null)
            {
                yield return new AgentReviewPath("geometry_overlay", frame.GeometryOverlayPath, description: $"Geometry overlay for interaction frame '{frame.FrameId}'.");
            }
        }
    }
}

public sealed record RuntimeInteractionAnimationFrame
{
    public const int MaximumSnapshots = 64;

    [JsonConstructor]
    public RuntimeInteractionAnimationFrame(
        string stepId,
        string frameId,
        int frameIndex,
        int offsetMs,
        DateTimeOffset capturedAt,
        ScreenshotResponse? screenshot = null,
        string? geometryOverlayPath = null,
        IReadOnlyList<RuntimeInteractionGeometrySnapshot>? geometry = null,
        IReadOnlyDictionary<string, string>? metadata = null)
    {
        if (string.IsNullOrWhiteSpace(stepId))
        {
            throw new ArgumentException("Step id cannot be empty.", nameof(stepId));
        }

        if (string.IsNullOrWhiteSpace(frameId))
        {
            throw new ArgumentException("Frame id cannot be empty.", nameof(frameId));
        }

        if (frameIndex < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(frameIndex), frameIndex, "Frame index cannot be negative.");
        }

        if (offsetMs < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(offsetMs), offsetMs, "Frame offset cannot be negative.");
        }

        StepId = stepId.Trim();
        FrameId = frameId.Trim();
        FrameIndex = frameIndex;
        OffsetMs = offsetMs;
        CapturedAt = capturedAt;
        Screenshot = screenshot;
        GeometryOverlayPath = string.IsNullOrWhiteSpace(geometryOverlayPath) ? null : Path.GetFullPath(geometryOverlayPath);
        Geometry = (geometry ?? []).Take(MaximumSnapshots).ToArray();
        Metadata = metadata ?? new Dictionary<string, string>();
    }

    [JsonPropertyName("stepId")]
    public string StepId { get; }

    [JsonPropertyName("frameId")]
    public string FrameId { get; }

    [JsonPropertyName("frameIndex")]
    public int FrameIndex { get; }

    [JsonPropertyName("offsetMs")]
    public int OffsetMs { get; }

    [JsonPropertyName("capturedAt")]
    public DateTimeOffset CapturedAt { get; }

    [JsonPropertyName("screenshot")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ScreenshotResponse? Screenshot { get; }

    [JsonPropertyName("geometryOverlayPath")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? GeometryOverlayPath { get; }

    [JsonPropertyName("geometry")]
    public IReadOnlyList<RuntimeInteractionGeometrySnapshot> Geometry { get; }

    [JsonPropertyName("metadata")]
    public IReadOnlyDictionary<string, string> Metadata { get; }
}

public sealed record RuntimeInteractionGeometrySnapshot(
    [property: JsonPropertyName("nodeId")] string NodeId,
    [property: JsonPropertyName("nodeType")] string NodeType,
    [property: JsonPropertyName("name")] string? Name,
    [property: JsonPropertyName("automationId")] string? AutomationId,
    [property: JsonPropertyName("text")] string? Text,
    [property: JsonPropertyName("bounds")] NodeBounds? Bounds,
    [property: JsonPropertyName("parentNodeId")] string? ParentNodeId,
    [property: JsonPropertyName("parentBounds")] NodeBounds? ParentBounds,
    [property: JsonPropertyName("isClippedByParent")] bool IsClippedByParent);

public sealed record RuntimeInteractionGeometryAssertionResult
{
    public const int MaximumSamples = 96;

    [JsonConstructor]
    public RuntimeInteractionGeometryAssertionResult(
        string assertionId,
        string targetNodeId,
        string metric,
        string mode,
        string status,
        string message,
        double tolerance,
        string? stepId = null,
        double? expectedValue = null,
        double? minValue = null,
        double? maxValue = null,
        IReadOnlyList<RuntimeInteractionGeometrySample>? samples = null,
        IReadOnlyDictionary<string, string>? metadata = null)
    {
        if (string.IsNullOrWhiteSpace(assertionId))
        {
            throw new ArgumentException("Assertion id cannot be empty.", nameof(assertionId));
        }

        if (string.IsNullOrWhiteSpace(targetNodeId))
        {
            throw new ArgumentException("Target node id cannot be empty.", nameof(targetNodeId));
        }

        if (string.IsNullOrWhiteSpace(metric))
        {
            throw new ArgumentException("Assertion metric cannot be empty.", nameof(metric));
        }

        if (string.IsNullOrWhiteSpace(mode))
        {
            throw new ArgumentException("Assertion mode cannot be empty.", nameof(mode));
        }

        if (string.IsNullOrWhiteSpace(status))
        {
            throw new ArgumentException("Assertion status cannot be empty.", nameof(status));
        }

        AssertionId = assertionId.Trim();
        TargetNodeId = targetNodeId.Trim();
        Metric = metric.Trim();
        Mode = mode.Trim();
        Status = status.Trim();
        Message = string.IsNullOrWhiteSpace(message) ? Status : message.Trim();
        Tolerance = tolerance;
        StepId = string.IsNullOrWhiteSpace(stepId) ? null : stepId.Trim();
        ExpectedValue = expectedValue;
        MinValue = minValue;
        MaxValue = maxValue;
        Samples = (samples ?? []).Take(MaximumSamples).ToArray();
        Metadata = metadata ?? new Dictionary<string, string>();
    }

    [JsonPropertyName("assertionId")]
    public string AssertionId { get; }

    [JsonPropertyName("targetNodeId")]
    public string TargetNodeId { get; }

    [JsonPropertyName("metric")]
    public string Metric { get; }

    [JsonPropertyName("mode")]
    public string Mode { get; }

    [JsonPropertyName("status")]
    public string Status { get; }

    [JsonPropertyName("message")]
    public string Message { get; }

    [JsonPropertyName("tolerance")]
    public double Tolerance { get; }

    [JsonPropertyName("stepId")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? StepId { get; }

    [JsonPropertyName("expectedValue")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? ExpectedValue { get; }

    [JsonPropertyName("minValue")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? MinValue { get; }

    [JsonPropertyName("maxValue")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? MaxValue { get; }

    [JsonPropertyName("samples")]
    public IReadOnlyList<RuntimeInteractionGeometrySample> Samples { get; }

    [JsonPropertyName("metadata")]
    public IReadOnlyDictionary<string, string> Metadata { get; }
}

public sealed record RuntimeInteractionGeometrySample(
    [property: JsonPropertyName("stepId")] string StepId,
    [property: JsonPropertyName("frameId")] string FrameId,
    [property: JsonPropertyName("offsetMs")] int OffsetMs,
    [property: JsonPropertyName("value")] double? Value,
    [property: JsonPropertyName("bounds")] NodeBounds? Bounds,
    [property: JsonPropertyName("parentBounds")] NodeBounds? ParentBounds,
    [property: JsonPropertyName("isClippedByParent")] bool IsClippedByParent,
    [property: JsonPropertyName("message")] string? Message = null);
