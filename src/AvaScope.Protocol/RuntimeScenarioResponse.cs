using System.Text.Json.Serialization;

namespace AvaScope.Protocol;

public sealed record RuntimeScenarioResponse
{
    [JsonConstructor]
    public RuntimeScenarioResponse(
        string requestId,
        string status,
        DateTimeOffset startedAt,
        DateTimeOffset completedAt,
        SessionId? sessionId = null,
        string? topLevelId = null,
        LaunchAppResponse? launch = null,
        AttachToAppResponse? attach = null,
        SemanticWorkflowResponse? workflow = null,
        string isolatedStateStatus = "not_configured",
        string? isolatedStateDirectory = null,
        string? timelinePath = null,
        IReadOnlyList<ProtocolError>? diagnostics = null,
        IReadOnlyDictionary<string, string>? metadata = null,
        NativePickerResponse? preparedPickerResult = null,
        ResponseBudgetInfo? responseBudget = null)
    {
        if (string.IsNullOrWhiteSpace(requestId))
        {
            throw new ArgumentException("Scenario request id cannot be empty.", nameof(requestId));
        }

        if (string.IsNullOrWhiteSpace(status))
        {
            throw new ArgumentException("Scenario status cannot be empty.", nameof(status));
        }

        RequestId = requestId;
        Status = status;
        StartedAt = startedAt;
        CompletedAt = completedAt;
        SessionId = sessionId;
        TopLevelId = string.IsNullOrWhiteSpace(topLevelId) ? null : topLevelId;
        Launch = launch;
        Attach = attach;
        Workflow = workflow;
        IsolatedStateStatus = string.IsNullOrWhiteSpace(isolatedStateStatus) ? "not_configured" : isolatedStateStatus;
        IsolatedStateDirectory = string.IsNullOrWhiteSpace(isolatedStateDirectory) ? null : Path.GetFullPath(isolatedStateDirectory);
        TimelinePath = string.IsNullOrWhiteSpace(timelinePath) ? null : Path.GetFullPath(timelinePath);
        Diagnostics = diagnostics ?? [];
        Metadata = metadata ?? new Dictionary<string, string>();
        PreparedPickerResult = preparedPickerResult;
        ResponseBudget = responseBudget;
    }

    [JsonPropertyName("requestId")]
    public string RequestId { get; }

    [JsonPropertyName("status")]
    public string Status { get; }

    [JsonPropertyName("startedAt")]
    public DateTimeOffset StartedAt { get; }

    [JsonPropertyName("completedAt")]
    public DateTimeOffset CompletedAt { get; }

    [JsonPropertyName("sessionId")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public SessionId? SessionId { get; }

    [JsonPropertyName("topLevelId")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? TopLevelId { get; }

    [JsonPropertyName("launch")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public LaunchAppResponse? Launch { get; }

    [JsonPropertyName("attach")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public AttachToAppResponse? Attach { get; }

    [JsonPropertyName("workflow")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public SemanticWorkflowResponse? Workflow { get; }

    [JsonPropertyName("isolatedStateStatus")]
    public string IsolatedStateStatus { get; }

    [JsonPropertyName("isolatedStateDirectory")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? IsolatedStateDirectory { get; }

    [JsonPropertyName("timelinePath")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? TimelinePath { get; }

    [JsonPropertyName("diagnostics")]
    public IReadOnlyList<ProtocolError> Diagnostics { get; }

    [JsonPropertyName("metadata")]
    public IReadOnlyDictionary<string, string> Metadata { get; }

    [JsonPropertyName("preparedPickerResult")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativePickerResponse? PreparedPickerResult { get; }

    [JsonPropertyName("responseBudget")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ResponseBudgetInfo? ResponseBudget { get; }
}
