using System.Text.Json.Serialization;

namespace AvaScope.Protocol;

public sealed record RuntimeGestureResult
{
    [JsonConstructor]
    public RuntimeGestureResult(
        RuntimeGesturePath path,
        string executionMode,
        string provenance,
        int requestedDurationMs,
        int effectiveDurationMs,
        string sourceTargetNodeId,
        string? destinationTargetNodeId = null)
    {
        Path = path ?? throw new ArgumentNullException(nameof(path));
        if (string.IsNullOrWhiteSpace(executionMode)
            || string.IsNullOrWhiteSpace(provenance)
            || string.IsNullOrWhiteSpace(sourceTargetNodeId))
        {
            throw new ArgumentException("Gesture execution mode, provenance, and source target cannot be empty.");
        }

        if (requestedDurationMs < 0 || effectiveDurationMs < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(requestedDurationMs), "Gesture durations cannot be negative.");
        }

        ExecutionMode = executionMode.Trim();
        Provenance = provenance.Trim();
        RequestedDurationMs = requestedDurationMs;
        EffectiveDurationMs = effectiveDurationMs;
        SourceTargetNodeId = sourceTargetNodeId.Trim();
        DestinationTargetNodeId = string.IsNullOrWhiteSpace(destinationTargetNodeId)
            ? null
            : destinationTargetNodeId.Trim();
    }

    [JsonPropertyName("path")]
    public RuntimeGesturePath Path { get; }

    [JsonPropertyName("executionMode")]
    public string ExecutionMode { get; }

    [JsonPropertyName("provenance")]
    public string Provenance { get; }

    [JsonPropertyName("requestedDurationMs")]
    public int RequestedDurationMs { get; }

    [JsonPropertyName("effectiveDurationMs")]
    public int EffectiveDurationMs { get; }

    [JsonPropertyName("sourceTargetNodeId")]
    public string SourceTargetNodeId { get; }

    [JsonPropertyName("destinationTargetNodeId")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? DestinationTargetNodeId { get; }
}
