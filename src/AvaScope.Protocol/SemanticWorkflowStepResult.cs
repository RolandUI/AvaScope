using System.Text.Json.Serialization;

namespace AvaScope.Protocol;

public sealed record SemanticWorkflowStepResult
{
    [JsonConstructor]
    public SemanticWorkflowStepResult(
        string stepId,
        string action,
        string status,
        string message,
        DateTimeOffset executedAt,
        RuntimeTargetContext? target = null,
        InputResponse? input = null,
        InspectNodeResponse? inspection = null,
        ScreenshotResponse? screenshot = null,
        IReadOnlyList<ProtocolError>? diagnostics = null,
        IReadOnlyDictionary<string, string>? metadata = null,
        NativePickerResponse? picker = null,
        RuntimeMutationResponse? mutation = null,
        RuntimeCustomActionsResponse? customActions = null,
        RuntimeCustomActionResponse? customAction = null,
        RuntimeWaitObservation? waitObservation = null,
        string? topLevelAlias = null,
        string? resolvedTopLevelId = null,
        string? executionPath = null,
        string? parentStepId = null,
        int? attempt = null,
        string? sourceFragment = null,
        SemanticWorkflowVerificationResult? verification = null,
        SemanticWorkflowFailureEvidence? failureEvidence = null)
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

        if (string.IsNullOrWhiteSpace(message))
        {
            throw new ArgumentException("Step message cannot be empty.", nameof(message));
        }

        StepId = stepId;
        Action = action;
        Status = status;
        Message = message;
        ExecutedAt = executedAt;
        Target = target;
        Input = input;
        Inspection = inspection;
        Screenshot = screenshot;
        Diagnostics = diagnostics ?? Array.Empty<ProtocolError>();
        Metadata = metadata ?? new Dictionary<string, string>();
        Picker = picker;
        Mutation = mutation;
        CustomActions = customActions;
        CustomAction = customAction;
        WaitObservation = waitObservation;
        TopLevelAlias = string.IsNullOrWhiteSpace(topLevelAlias) ? null : topLevelAlias;
        ResolvedTopLevelId = string.IsNullOrWhiteSpace(resolvedTopLevelId) ? null : resolvedTopLevelId;
        ExecutionPath = string.IsNullOrWhiteSpace(executionPath) ? null : executionPath;
        ParentStepId = string.IsNullOrWhiteSpace(parentStepId) ? null : parentStepId;
        Attempt = attempt;
        SourceFragment = string.IsNullOrWhiteSpace(sourceFragment) ? null : sourceFragment;
        Verification = verification;
        FailureEvidence = failureEvidence;
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

    [JsonPropertyName("target")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public RuntimeTargetContext? Target { get; }

    [JsonPropertyName("input")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public InputResponse? Input { get; }

    [JsonPropertyName("inspection")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public InspectNodeResponse? Inspection { get; }

    [JsonPropertyName("screenshot")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ScreenshotResponse? Screenshot { get; }

    [JsonPropertyName("diagnostics")]
    public IReadOnlyList<ProtocolError> Diagnostics { get; }

    [JsonPropertyName("metadata")]
    public IReadOnlyDictionary<string, string> Metadata { get; }

    [JsonPropertyName("picker")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativePickerResponse? Picker { get; }

    [JsonPropertyName("mutation")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public RuntimeMutationResponse? Mutation { get; }

    [JsonPropertyName("customActions")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public RuntimeCustomActionsResponse? CustomActions { get; }

    [JsonPropertyName("customAction")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public RuntimeCustomActionResponse? CustomAction { get; }

    [JsonPropertyName("waitObservation")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public RuntimeWaitObservation? WaitObservation { get; }

    [JsonPropertyName("topLevelAlias")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? TopLevelAlias { get; }

    [JsonPropertyName("resolvedTopLevelId")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ResolvedTopLevelId { get; }

    [JsonPropertyName("executionPath")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ExecutionPath { get; }

    [JsonPropertyName("parentStepId")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ParentStepId { get; }

    [JsonPropertyName("attempt")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? Attempt { get; }

    [JsonPropertyName("sourceFragment")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? SourceFragment { get; }

    [JsonPropertyName("verification")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public SemanticWorkflowVerificationResult? Verification { get; }

    [JsonPropertyName("failureEvidence")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public SemanticWorkflowFailureEvidence? FailureEvidence { get; }
}
