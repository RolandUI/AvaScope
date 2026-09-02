using System.Text.Json.Serialization;

namespace AvaScope.Protocol;

public sealed record SemanticWorkflowVerificationResult
{
    [JsonConstructor]
    public SemanticWorkflowVerificationResult(
        string status,
        SemanticWaitCondition condition,
        DateTimeOffset startedAt,
        DateTimeOffset completedAt,
        InspectNodeResponse? beforeInspection = null,
        InspectNodeResponse? afterInspection = null,
        ScreenshotResponse? beforeScreenshot = null,
        ScreenshotResponse? afterScreenshot = null,
        RuntimeWaitObservation? observation = null,
        IReadOnlyList<ProtocolError>? diagnostics = null,
        IReadOnlyDictionary<string, string>? metadata = null)
    {
        if (string.IsNullOrWhiteSpace(status))
        {
            throw new ArgumentException("Verification status cannot be empty.", nameof(status));
        }

        Status = status.Trim();
        Condition = condition ?? throw new ArgumentNullException(nameof(condition));
        StartedAt = startedAt;
        CompletedAt = completedAt;
        BeforeInspection = beforeInspection;
        AfterInspection = afterInspection;
        BeforeScreenshot = beforeScreenshot;
        AfterScreenshot = afterScreenshot;
        Observation = observation;
        Diagnostics = diagnostics ?? [];
        Metadata = metadata ?? new Dictionary<string, string>();
    }

    [JsonPropertyName("status")]
    public string Status { get; }

    [JsonPropertyName("condition")]
    public SemanticWaitCondition Condition { get; }

    [JsonPropertyName("startedAt")]
    public DateTimeOffset StartedAt { get; }

    [JsonPropertyName("completedAt")]
    public DateTimeOffset CompletedAt { get; }

    [JsonPropertyName("beforeInspection")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public InspectNodeResponse? BeforeInspection { get; }

    [JsonPropertyName("afterInspection")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public InspectNodeResponse? AfterInspection { get; }

    [JsonPropertyName("beforeScreenshot")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ScreenshotResponse? BeforeScreenshot { get; }

    [JsonPropertyName("afterScreenshot")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ScreenshotResponse? AfterScreenshot { get; }

    [JsonPropertyName("observation")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public RuntimeWaitObservation? Observation { get; }

    [JsonPropertyName("diagnostics")]
    public IReadOnlyList<ProtocolError> Diagnostics { get; }

    [JsonPropertyName("metadata")]
    public IReadOnlyDictionary<string, string> Metadata { get; }
}
