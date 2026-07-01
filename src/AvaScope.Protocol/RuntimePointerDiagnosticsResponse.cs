using System.Text.Json.Serialization;

namespace AvaScope.Protocol;

public sealed record RuntimePointerDiagnosticsResponse
{
    public const int MaximumDiagnostics = 32;

    [JsonConstructor]
    public RuntimePointerDiagnosticsResponse(
        string requestId,
        SessionId sessionId,
        string topLevelId,
        string status,
        DateTimeOffset startedAt,
        DateTimeOffset completedAt,
        IReadOnlyList<RuntimePointerStepResult>? steps = null,
        IReadOnlyList<ProtocolError>? diagnostics = null,
        IReadOnlyDictionary<string, string>? metadata = null)
    {
        if (string.IsNullOrWhiteSpace(requestId))
        {
            throw new ArgumentException("Pointer diagnostics request id cannot be empty.", nameof(requestId));
        }

        if (string.IsNullOrWhiteSpace(topLevelId))
        {
            throw new ArgumentException("Top-level id cannot be empty.", nameof(topLevelId));
        }

        if (string.IsNullOrWhiteSpace(status))
        {
            throw new ArgumentException("Pointer diagnostics status cannot be empty.", nameof(status));
        }

        RequestId = requestId.Trim();
        SessionId = sessionId ?? throw new ArgumentNullException(nameof(sessionId));
        TopLevelId = topLevelId.Trim();
        Status = status.Trim();
        StartedAt = startedAt;
        CompletedAt = completedAt;
        Steps = steps ?? [];
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
    public IReadOnlyList<RuntimePointerStepResult> Steps { get; }

    [JsonPropertyName("diagnostics")]
    public IReadOnlyList<ProtocolError> Diagnostics { get; }

    [JsonPropertyName("metadata")]
    public IReadOnlyDictionary<string, string> Metadata { get; }

    [JsonPropertyName("agentReview")]
    public AgentReviewSurface AgentReview => CreateAgentReview();

    private AgentReviewSurface CreateAgentReview()
    {
        var failures = Diagnostics
            .Take(AgentReviewSurface.MaximumFailureSummaries)
            .Select(static diagnostic => new AgentReviewFailure("pointer_diagnostics", diagnostic.Message, diagnostic.Code))
            .ToArray();
        var artifacts = Steps
            .SelectMany(static step => step.ArtifactPaths())
            .Take(AgentReviewSurface.MaximumPaths)
            .ToArray();
        var transitionCount = Steps.Sum(static step => step.Transitions.Count);

        return new AgentReviewSurface(
            Status,
            $"Pointer diagnostics '{RequestId}' completed with status '{Status}'.",
            [
                $"steps: {Steps.Count}",
                $"transitions: {transitionCount}",
                $"topLevel: {TopLevelId}"
            ],
            failures,
            artifactPaths: artifacts,
            truncated: Diagnostics.Count > AgentReviewSurface.MaximumFailureSummaries
                || Steps.Sum(static step => step.ArtifactPaths().Count()) > AgentReviewSurface.MaximumPaths);
    }
}

public sealed record RuntimePointerStepResult
{
    public const int MaximumDiagnostics = 16;
    public const int MaximumTransitions = 16;

    [JsonConstructor]
    public RuntimePointerStepResult(
        string stepId,
        string action,
        string status,
        string message,
        DateTimeOffset executedAt,
        RuntimePointerLocation? pointer = null,
        InputResponse? input = null,
        ScreenshotResponse? screenshot = null,
        string? pointerOverlayPath = null,
        RuntimePointerLayerSnapshot? activeLayer = null,
        IReadOnlyList<RuntimePointerTransitionDiagnostic>? transitions = null,
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
        Message = string.IsNullOrWhiteSpace(message) ? Status : message;
        ExecutedAt = executedAt;
        Pointer = pointer;
        Input = input;
        Screenshot = screenshot;
        PointerOverlayPath = string.IsNullOrWhiteSpace(pointerOverlayPath) ? null : Path.GetFullPath(pointerOverlayPath);
        ActiveLayer = activeLayer;
        Transitions = (transitions ?? []).Take(MaximumTransitions).ToArray();
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

    [JsonPropertyName("pointer")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public RuntimePointerLocation? Pointer { get; }

    [JsonPropertyName("input")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public InputResponse? Input { get; }

    [JsonPropertyName("screenshot")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ScreenshotResponse? Screenshot { get; }

    [JsonPropertyName("pointerOverlayPath")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? PointerOverlayPath { get; }

    [JsonPropertyName("activeLayer")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public RuntimePointerLayerSnapshot? ActiveLayer { get; }

    [JsonPropertyName("transitions")]
    public IReadOnlyList<RuntimePointerTransitionDiagnostic> Transitions { get; }

    [JsonPropertyName("diagnostics")]
    public IReadOnlyList<ProtocolError> Diagnostics { get; }

    [JsonPropertyName("metadata")]
    public IReadOnlyDictionary<string, string> Metadata { get; }

    internal IEnumerable<AgentReviewPath> ArtifactPaths()
    {
        if (Screenshot is not null)
        {
            yield return new AgentReviewPath("screenshot", Screenshot.FilePath, description: $"Pointer diagnostics screenshot for step '{StepId}'.");
        }

        if (PointerOverlayPath is not null)
        {
            yield return new AgentReviewPath("pointer_overlay", PointerOverlayPath, description: $"Pointer marker overlay for step '{StepId}'.");
        }
    }
}

public sealed record RuntimePointerLocation(
    [property: JsonPropertyName("x")] double X,
    [property: JsonPropertyName("y")] double Y);

public sealed record RuntimePointerLayerSnapshot
{
    [JsonConstructor]
    public RuntimePointerLayerSnapshot(
        string topLevelId,
        string topLevelKind,
        string layerKind,
        bool isPrimary,
        IReadOnlyList<RuntimePointerHitNode>? hitTestPath = null,
        RuntimePointerHitNode? nearestNode = null)
    {
        TopLevelId = string.IsNullOrWhiteSpace(topLevelId) ? "unknown" : topLevelId.Trim();
        TopLevelKind = string.IsNullOrWhiteSpace(topLevelKind) ? "unknown" : topLevelKind.Trim();
        LayerKind = string.IsNullOrWhiteSpace(layerKind) ? "unknown" : layerKind.Trim();
        IsPrimary = isPrimary;
        HitTestPath = hitTestPath ?? [];
        NearestNode = nearestNode;
    }

    [JsonPropertyName("topLevelId")]
    public string TopLevelId { get; }

    [JsonPropertyName("topLevelKind")]
    public string TopLevelKind { get; }

    [JsonPropertyName("layerKind")]
    public string LayerKind { get; }

    [JsonPropertyName("isPrimary")]
    public bool IsPrimary { get; }

    [JsonPropertyName("hitTestPath")]
    public IReadOnlyList<RuntimePointerHitNode> HitTestPath { get; }

    [JsonPropertyName("nearestNode")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public RuntimePointerHitNode? NearestNode { get; }
}

public sealed record RuntimePointerHitNode(
    [property: JsonPropertyName("nodeId")] string NodeId,
    [property: JsonPropertyName("nodeType")] string NodeType,
    [property: JsonPropertyName("name")] string? Name,
    [property: JsonPropertyName("automationId")] string? AutomationId,
    [property: JsonPropertyName("text")] string? Text,
    [property: JsonPropertyName("bounds")] NodeBounds? Bounds,
    [property: JsonPropertyName("containsPointer")] bool ContainsPointer,
    [property: JsonPropertyName("distance")] double Distance);

public sealed record RuntimePointerTransitionDiagnostic
{
    [JsonConstructor]
    public RuntimePointerTransitionDiagnostic(
        string severity,
        string code,
        string message,
        string provenance,
        string? fromTopLevelId = null,
        string? fromNodeId = null,
        string? toTopLevelId = null,
        string? toNodeId = null,
        bool parentHoverRegionExited = false,
        IReadOnlyDictionary<string, string>? metadata = null)
    {
        Severity = string.IsNullOrWhiteSpace(severity) ? "info" : severity.Trim();
        Code = string.IsNullOrWhiteSpace(code) ? "pointer_transition" : code.Trim();
        Message = string.IsNullOrWhiteSpace(message) ? Code : message;
        Provenance = string.IsNullOrWhiteSpace(provenance) ? "unknown" : provenance.Trim();
        FromTopLevelId = string.IsNullOrWhiteSpace(fromTopLevelId) ? null : fromTopLevelId.Trim();
        FromNodeId = string.IsNullOrWhiteSpace(fromNodeId) ? null : fromNodeId.Trim();
        ToTopLevelId = string.IsNullOrWhiteSpace(toTopLevelId) ? null : toTopLevelId.Trim();
        ToNodeId = string.IsNullOrWhiteSpace(toNodeId) ? null : toNodeId.Trim();
        ParentHoverRegionExited = parentHoverRegionExited;
        Metadata = metadata ?? new Dictionary<string, string>();
    }

    [JsonPropertyName("severity")]
    public string Severity { get; }

    [JsonPropertyName("code")]
    public string Code { get; }

    [JsonPropertyName("message")]
    public string Message { get; }

    [JsonPropertyName("provenance")]
    public string Provenance { get; }

    [JsonPropertyName("fromTopLevelId")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? FromTopLevelId { get; }

    [JsonPropertyName("fromNodeId")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? FromNodeId { get; }

    [JsonPropertyName("toTopLevelId")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ToTopLevelId { get; }

    [JsonPropertyName("toNodeId")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ToNodeId { get; }

    [JsonPropertyName("parentHoverRegionExited")]
    public bool ParentHoverRegionExited { get; }

    [JsonPropertyName("metadata")]
    public IReadOnlyDictionary<string, string> Metadata { get; }
}
