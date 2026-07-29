using System.Text.Json.Serialization;

namespace AvaScope.Protocol;

public sealed record SemanticWorkflowResponse
{
    [JsonConstructor]
    public SemanticWorkflowResponse(
        string requestId,
        SessionId sessionId,
        string topLevelId,
        string status,
        DateTimeOffset startedAt,
        DateTimeOffset completedAt,
        IReadOnlyList<SemanticWorkflowStepResult> steps,
        string isolatedStateStatus = "not_configured",
        IReadOnlyList<ProtocolError>? diagnostics = null,
        IReadOnlyDictionary<string, string>? metadata = null,
        ResponseBudgetInfo? responseBudget = null)
    {
        if (string.IsNullOrWhiteSpace(requestId))
        {
            throw new ArgumentException("Workflow request id cannot be empty.", nameof(requestId));
        }

        SessionId = sessionId ?? throw new ArgumentNullException(nameof(sessionId));

        if (string.IsNullOrWhiteSpace(topLevelId))
        {
            throw new ArgumentException("Top-level id cannot be empty.", nameof(topLevelId));
        }

        if (string.IsNullOrWhiteSpace(status))
        {
            throw new ArgumentException("Workflow status cannot be empty.", nameof(status));
        }

        RequestId = requestId;
        TopLevelId = topLevelId;
        Status = status;
        StartedAt = startedAt;
        CompletedAt = completedAt;
        Steps = steps ?? Array.Empty<SemanticWorkflowStepResult>();
        IsolatedStateStatus = string.IsNullOrWhiteSpace(isolatedStateStatus) ? "not_configured" : isolatedStateStatus;
        Diagnostics = diagnostics ?? Array.Empty<ProtocolError>();
        Metadata = metadata ?? new Dictionary<string, string>();
        ResponseBudget = responseBudget;
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
}
