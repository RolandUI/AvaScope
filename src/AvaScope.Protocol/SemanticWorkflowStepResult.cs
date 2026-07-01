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
}
