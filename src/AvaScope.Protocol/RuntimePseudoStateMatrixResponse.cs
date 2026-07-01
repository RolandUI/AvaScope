using System.Text.Json.Serialization;

namespace AvaScope.Protocol;

public sealed record RuntimePseudoStateMatrixResponse
{
    public const int MaximumDiagnostics = 48;

    [JsonConstructor]
    public RuntimePseudoStateMatrixResponse(
        string requestId,
        SessionId sessionId,
        string topLevelId,
        RuntimeTargetContext target,
        string status,
        DateTimeOffset startedAt,
        DateTimeOffset completedAt,
        IReadOnlyList<RuntimePseudoStateMatrixEntry>? entries = null,
        string? contactSheetPath = null,
        IReadOnlyList<ProtocolError>? diagnostics = null,
        IReadOnlyDictionary<string, string>? metadata = null)
    {
        if (string.IsNullOrWhiteSpace(requestId))
        {
            throw new ArgumentException("Pseudo-state matrix request id cannot be empty.", nameof(requestId));
        }

        if (string.IsNullOrWhiteSpace(topLevelId))
        {
            throw new ArgumentException("Top-level id cannot be empty.", nameof(topLevelId));
        }

        if (string.IsNullOrWhiteSpace(status))
        {
            throw new ArgumentException("Pseudo-state matrix status cannot be empty.", nameof(status));
        }

        RequestId = requestId.Trim();
        SessionId = sessionId ?? throw new ArgumentNullException(nameof(sessionId));
        TopLevelId = topLevelId.Trim();
        Target = target ?? throw new ArgumentNullException(nameof(target));
        Status = status.Trim();
        StartedAt = startedAt;
        CompletedAt = completedAt;
        Entries = entries ?? [];
        ContactSheetPath = string.IsNullOrWhiteSpace(contactSheetPath) ? null : Path.GetFullPath(contactSheetPath);
        Diagnostics = (diagnostics ?? []).Take(MaximumDiagnostics).ToArray();
        Metadata = metadata ?? new Dictionary<string, string>();
    }

    [JsonPropertyName("requestId")]
    public string RequestId { get; }

    [JsonPropertyName("sessionId")]
    public SessionId SessionId { get; }

    [JsonPropertyName("topLevelId")]
    public string TopLevelId { get; }

    [JsonPropertyName("target")]
    public RuntimeTargetContext Target { get; }

    [JsonPropertyName("status")]
    public string Status { get; }

    [JsonPropertyName("startedAt")]
    public DateTimeOffset StartedAt { get; }

    [JsonPropertyName("completedAt")]
    public DateTimeOffset CompletedAt { get; }

    [JsonPropertyName("entries")]
    public IReadOnlyList<RuntimePseudoStateMatrixEntry> Entries { get; }

    [JsonPropertyName("contactSheetPath")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ContactSheetPath { get; }

    [JsonPropertyName("diagnostics")]
    public IReadOnlyList<ProtocolError> Diagnostics { get; }

    [JsonPropertyName("metadata")]
    public IReadOnlyDictionary<string, string> Metadata { get; }

    [JsonPropertyName("agentReview")]
    public AgentReviewSurface AgentReview => CreateAgentReview();

    private AgentReviewSurface CreateAgentReview()
    {
        var artifacts = Entries
            .SelectMany(static entry => entry.ArtifactPaths())
            .Concat(ContactSheetPath is null ? [] : [new AgentReviewPath("contact_sheet", ContactSheetPath, description: "Labeled pseudo-state matrix contact sheet.")])
            .Take(AgentReviewSurface.MaximumPaths)
            .ToArray();
        var failures = Diagnostics
            .Take(AgentReviewSurface.MaximumFailureSummaries)
            .Select(static diagnostic => new AgentReviewFailure("pseudo_state_matrix", diagnostic.Message, diagnostic.Code))
            .ToArray();

        return new AgentReviewSurface(
            Status,
            $"Pseudo-state matrix '{RequestId}' completed with status '{Status}'.",
            [
                $"states: {Entries.Count}",
                $"captured: {Entries.Count(static entry => entry.Screenshot is not null)}",
                $"target: {Target.NodeId ?? Target.TargetKind}"
            ],
            failures,
            artifactPaths: artifacts,
            truncated: Diagnostics.Count > AgentReviewSurface.MaximumFailureSummaries
                || Entries.Sum(static entry => entry.ArtifactPaths().Count()) + (ContactSheetPath is null ? 0 : 1) > AgentReviewSurface.MaximumPaths);
    }
}

public sealed record RuntimePseudoStateMatrixEntry
{
    public const int MaximumDiagnostics = 16;
    public const int MaximumMutations = 8;

    [JsonConstructor]
    public RuntimePseudoStateMatrixEntry(
        string state,
        string label,
        string status,
        string message,
        DateTimeOffset capturedAt,
        ScreenshotResponse? screenshot = null,
        RuntimePseudoStateTargetSummary? target = null,
        IReadOnlyList<RuntimeMutationResponse>? appliedMutations = null,
        IReadOnlyList<RuntimeMutationResponse>? resetMutations = null,
        IReadOnlyList<InputResponse>? inputs = null,
        IReadOnlyList<ProtocolError>? diagnostics = null,
        IReadOnlyDictionary<string, string>? metadata = null)
    {
        if (string.IsNullOrWhiteSpace(state))
        {
            throw new ArgumentException("Pseudo-state cannot be empty.", nameof(state));
        }

        if (string.IsNullOrWhiteSpace(status))
        {
            throw new ArgumentException("Pseudo-state status cannot be empty.", nameof(status));
        }

        State = state.Trim();
        Label = string.IsNullOrWhiteSpace(label) ? State : label.Trim();
        Status = status.Trim();
        Message = string.IsNullOrWhiteSpace(message) ? Status : message.Trim();
        CapturedAt = capturedAt;
        Screenshot = screenshot;
        Target = target;
        AppliedMutations = (appliedMutations ?? []).Take(MaximumMutations).ToArray();
        ResetMutations = (resetMutations ?? []).Take(MaximumMutations).ToArray();
        Inputs = (inputs ?? []).Take(MaximumMutations).ToArray();
        Diagnostics = (diagnostics ?? []).Take(MaximumDiagnostics).ToArray();
        Metadata = metadata ?? new Dictionary<string, string>();
    }

    [JsonPropertyName("state")]
    public string State { get; }

    [JsonPropertyName("label")]
    public string Label { get; }

    [JsonPropertyName("status")]
    public string Status { get; }

    [JsonPropertyName("message")]
    public string Message { get; }

    [JsonPropertyName("capturedAt")]
    public DateTimeOffset CapturedAt { get; }

    [JsonPropertyName("screenshot")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ScreenshotResponse? Screenshot { get; }

    [JsonPropertyName("target")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public RuntimePseudoStateTargetSummary? Target { get; }

    [JsonPropertyName("appliedMutations")]
    public IReadOnlyList<RuntimeMutationResponse> AppliedMutations { get; }

    [JsonPropertyName("resetMutations")]
    public IReadOnlyList<RuntimeMutationResponse> ResetMutations { get; }

    [JsonPropertyName("inputs")]
    public IReadOnlyList<InputResponse> Inputs { get; }

    [JsonPropertyName("diagnostics")]
    public IReadOnlyList<ProtocolError> Diagnostics { get; }

    [JsonPropertyName("metadata")]
    public IReadOnlyDictionary<string, string> Metadata { get; }

    internal IEnumerable<AgentReviewPath> ArtifactPaths()
    {
        if (Screenshot is not null)
        {
            yield return new AgentReviewPath("pseudo_state_screenshot", Screenshot.FilePath, description: $"Pseudo-state screenshot for '{State}'.");
        }
    }
}

public sealed record RuntimePseudoStateTargetSummary(
    [property: JsonPropertyName("nodeId")] string NodeId,
    [property: JsonPropertyName("nodeType")] string NodeType,
    [property: JsonPropertyName("name")] string? Name,
    [property: JsonPropertyName("automationId")] string? AutomationId,
    [property: JsonPropertyName("text")] string? Text,
    [property: JsonPropertyName("bounds")] NodeBounds? Bounds,
    [property: JsonPropertyName("classes")] IReadOnlyList<string> Classes,
    [property: JsonPropertyName("accessibilityState")] RuntimeAccessibilityState? AccessibilityState);
