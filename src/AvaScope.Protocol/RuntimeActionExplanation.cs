using System.Text.Json.Serialization;

namespace AvaScope.Protocol;

public sealed record RuntimeActionExplanationRequest
{
    [JsonConstructor]
    public RuntimeActionExplanationRequest(SessionId sessionId, string topLevelId, string nodeId,
        string action = "click", string strategy = "semantic", double? x = null, double? y = null,
        RuntimeTargetContext? target = null, RuntimeEvidencePolicy? policy = null, int maxReasons = 24)
    {
        SessionId = sessionId ?? throw new ArgumentNullException(nameof(sessionId));
        if (string.IsNullOrWhiteSpace(topLevelId) || topLevelId.Length > 256
            || string.IsNullOrWhiteSpace(nodeId) || nodeId.Length > 256)
            throw new ArgumentException("An explicit top-level and node id of at most 256 characters are required.");
        if (!InputActions.All.Contains(action, StringComparer.Ordinal) || strategy is not ("semantic" or "synthetic" or "native"))
            throw new ArgumentException("Use a supported input action and semantic/synthetic/native strategy.");
        if ((x is null) != (y is null) || x is { } px && !double.IsFinite(px) || y is { } py && !double.IsFinite(py))
            throw new ArgumentException("Explicit coordinates require both finite x and y.");
        if (maxReasons is < 1 or > 32) throw new ArgumentOutOfRangeException(nameof(maxReasons));
        TopLevelId = topLevelId; NodeId = nodeId; Action = action; Strategy = strategy;
        X = x; Y = y; Target = target; Policy = policy; MaxReasons = maxReasons;
    }

    [JsonPropertyName("sessionId")] public SessionId SessionId { get; }
    [JsonPropertyName("topLevelId")] public string TopLevelId { get; }
    [JsonPropertyName("nodeId")] public string NodeId { get; }
    [JsonPropertyName("action")] public string Action { get; }
    [JsonPropertyName("strategy")] public string Strategy { get; }
    [JsonPropertyName("x")] public double? X { get; }
    [JsonPropertyName("y")] public double? Y { get; }
    [JsonPropertyName("target")] public RuntimeTargetContext? Target { get; }
    [JsonPropertyName("policy")] public RuntimeEvidencePolicy? Policy { get; }
    [JsonPropertyName("maxReasons")] public int MaxReasons { get; }
}

public sealed record RuntimeActionReason(
    [property: JsonPropertyName("code")] string Code,
    [property: JsonPropertyName("certainty")] string Certainty,
    [property: JsonPropertyName("blocksAction")] bool BlocksAction,
    [property: JsonPropertyName("message")] string Message,
    [property: JsonPropertyName("source")] string Source,
    [property: JsonPropertyName("target")] RuntimeTargetContext? Target,
    [property: JsonPropertyName("automationId")] string? AutomationId,
    [property: JsonPropertyName("nextAction")] string NextAction);

public sealed record RuntimeActivationPoint(
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("source")] string Source,
    [property: JsonPropertyName("coordinateSpace")] string CoordinateSpace,
    [property: JsonPropertyName("x")] double? X,
    [property: JsonPropertyName("y")] double? Y,
    [property: JsonPropertyName("renderScaling")] double RenderScaling,
    [property: JsonPropertyName("geometryRevision")] string? GeometryRevision,
    [property: JsonPropertyName("nativeConfirmation")] string NativeConfirmation,
    [property: JsonPropertyName("hitTarget")] RuntimeTargetContext? HitTarget,
    [property: JsonPropertyName("reason")] string? Reason);

public sealed record RuntimeActionExplanation(
    [property: JsonPropertyName("sessionId")] SessionId SessionId,
    [property: JsonPropertyName("target")] RuntimeTargetContext Target,
    [property: JsonPropertyName("automationId")] string? AutomationId,
    [property: JsonPropertyName("action")] string Action,
    [property: JsonPropertyName("strategy")] string Strategy,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("reasons")] IReadOnlyList<RuntimeActionReason> Reasons,
    [property: JsonPropertyName("activationPoint")] RuntimeActivationPoint ActivationPoint,
    [property: JsonPropertyName("truncated")] bool Truncated,
    [property: JsonPropertyName("nativeConfirmation")] string NativeConfirmation = "unavailable");
