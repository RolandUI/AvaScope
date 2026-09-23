using System.Text.Json.Serialization;

namespace AvaScope.Protocol;

public sealed record RuntimeTraceRequest
{
    [JsonConstructor]
    public RuntimeTraceRequest(SessionId sessionId, string action, string? traceId = null, string? topLevelId = null,
        RuntimeEvidencePolicy? policy = null, string? requestId = null, string? operationId = null, int durationMs = 10000,
        int maxEvents = 32, bool sampleValidation = false, string? outputDirectory = null)
    {
        if (action is not ("start" or "read" or "stop") || durationMs is < 1 or > 60000 || maxEvents is < 1 or > 128
            || traceId?.Length > 128 || requestId?.Length > 256 || operationId?.Length > 192 || topLevelId?.Length > 256
            || action == "start" && (policy is null || string.IsNullOrWhiteSpace(topLevelId))
            || action != "start" && string.IsNullOrWhiteSpace(traceId)
            || outputDirectory is not null && policy is null)
            throw new ArgumentException("Trace start requires an explicit window and evidence policy; read/stop require traceId. Duration 1..60000 ms, maxEvents 1..128; export requires a policy-owned output directory.");
        SessionId = sessionId ?? throw new ArgumentNullException(nameof(sessionId)); Action = action; TraceId = traceId;
        TopLevelId = topLevelId; Policy = policy; RequestId = requestId; OperationId = operationId;
        DurationMs = durationMs; MaxEvents = maxEvents; SampleValidation = sampleValidation; OutputDirectory = outputDirectory;
    }
    [JsonPropertyName("sessionId")] public SessionId SessionId { get; }
    [JsonPropertyName("action")] public string Action { get; }
    [JsonPropertyName("traceId")] public string? TraceId { get; }
    [JsonPropertyName("topLevelId")] public string? TopLevelId { get; }
    [JsonPropertyName("policy")] public RuntimeEvidencePolicy? Policy { get; }
    [JsonPropertyName("requestId")] public string? RequestId { get; }
    [JsonPropertyName("operationId")] public string? OperationId { get; }
    [JsonPropertyName("durationMs")] public int DurationMs { get; }
    [JsonPropertyName("maxEvents")] public int MaxEvents { get; }
    [JsonPropertyName("sampleValidation")] public bool SampleValidation { get; }
    [JsonPropertyName("outputDirectory")] public string? OutputDirectory { get; }
}

public sealed record RuntimeTraceEvent(
    [property: JsonPropertyName("sequence")] long Sequence,
    [property: JsonPropertyName("timestamp")] DateTimeOffset Timestamp,
    [property: JsonPropertyName("source")] string Source,
    [property: JsonPropertyName("kind")] string Kind,
    [property: JsonPropertyName("level")] string Level,
    [property: JsonPropertyName("message")] string Message,
    [property: JsonPropertyName("requestId")] string? RequestId,
    [property: JsonPropertyName("operationId")] string? OperationId,
    [property: JsonPropertyName("automationId")] string? AutomationId,
    [property: JsonPropertyName("correlation")] string Correlation,
    [property: JsonPropertyName("relation")] string Relation = "unfiltered");

public sealed record RuntimeTraceSource(
    [property: JsonPropertyName("kind")] string Kind,
    [property: JsonPropertyName("availability")] string Availability,
    [property: JsonPropertyName("reason")] string Reason);

public sealed record RuntimeTraceResponse(
    [property: JsonPropertyName("sessionId")] SessionId SessionId,
    [property: JsonPropertyName("traceId")] string TraceId,
    [property: JsonPropertyName("topLevelId")] string TopLevelId,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("events")] IReadOnlyList<RuntimeTraceEvent> Events,
    [property: JsonPropertyName("sources")] IReadOnlyList<RuntimeTraceSource> Sources,
    [property: JsonPropertyName("retainedEvents")] int RetainedEvents,
    [property: JsonPropertyName("droppedEvents")] long DroppedEvents,
    [property: JsonPropertyName("suppressedEvents")] long SuppressedEvents,
    [property: JsonPropertyName("truncated")] bool Truncated,
    [property: JsonPropertyName("observedAt")] DateTimeOffset ObservedAt,
    [property: JsonPropertyName("artifactPath")] string? ArtifactPath = null,
    [property: JsonPropertyName("correlationNote")] string CorrelationNote = "Explicit correlation associates evidence; neither correlation nor temporal proximity alone proves root cause.");
