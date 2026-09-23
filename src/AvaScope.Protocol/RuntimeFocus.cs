using System.Text.Json.Serialization;

namespace AvaScope.Protocol;

public sealed record RuntimeFocusInspectionRequest
{
    [JsonConstructor]
    public RuntimeFocusInspectionRequest(SessionId sessionId, string topLevelId, RuntimeTargetContext? target = null,
        int maxNodes = 1024, int maxCandidates = 24, RuntimeEvidencePolicy? policy = null)
    {
        SessionId = sessionId ?? throw new ArgumentNullException(nameof(sessionId));
        if (string.IsNullOrWhiteSpace(topLevelId) || topLevelId.Length > 256) throw new ArgumentException("Select one explicit window.");
        if (maxNodes is < 1 or > 4096 || maxCandidates is < 1 or > 64) throw new ArgumentException("Focus inspection allows 1..4096 nodes and 1..64 candidates.");
        if (target is not null && (target.SessionId != sessionId || target.TopLevelId != topLevelId || target.TreeKind != TreeKinds.Visual || target.NodeId is null))
            throw new ArgumentException("The optional reference target must be a visual node in the selected session/window.");
        TopLevelId = topLevelId; Target = target; MaxNodes = maxNodes; MaxCandidates = maxCandidates; Policy = policy;
    }
    [JsonPropertyName("sessionId")] public SessionId SessionId { get; }
    [JsonPropertyName("topLevelId")] public string TopLevelId { get; }
    [JsonPropertyName("target")] public RuntimeTargetContext? Target { get; }
    [JsonPropertyName("maxNodes")] public int MaxNodes { get; }
    [JsonPropertyName("maxCandidates")] public int MaxCandidates { get; }
    [JsonPropertyName("policy")] public RuntimeEvidencePolicy? Policy { get; }
}

public sealed record RuntimeFocusNode(
    [property: JsonPropertyName("target")] RuntimeTargetContext Target,
    [property: JsonPropertyName("label")] string? Label,
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("focused")] bool Focused,
    [property: JsonPropertyName("focusWithin")] bool FocusWithin,
    [property: JsonPropertyName("focusable")] bool Focusable,
    [property: JsonPropertyName("enabled")] bool Enabled,
    [property: JsonPropertyName("visible")] bool Visible,
    [property: JsonPropertyName("isTabStop")] bool IsTabStop,
    [property: JsonPropertyName("tabIndex")] int TabIndex,
    [property: JsonPropertyName("tabNavigation")] string TabNavigation,
    [property: JsonPropertyName("isFocusScope")] bool IsFocusScope,
    [property: JsonPropertyName("reasons")] IReadOnlyList<string> Reasons);

public sealed record RuntimeNativeFocus(
    [property: JsonPropertyName("state")] string State,
    [property: JsonPropertyName("source")] string Source,
    [property: JsonPropertyName("detail")] string Detail);

public sealed record RuntimeFocusPrediction(
    [property: JsonPropertyName("direction")] string Direction,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("target")] RuntimeTargetContext? Target,
    [property: JsonPropertyName("source")] string Source,
    [property: JsonPropertyName("detail")] string Detail);

public sealed record RuntimeFocusSnapshot(
    [property: JsonPropertyName("sessionId")] SessionId SessionId,
    [property: JsonPropertyName("topLevelId")] string TopLevelId,
    [property: JsonPropertyName("backend")] RuntimeBackendInfo Backend,
    [property: JsonPropertyName("frameworkWindowActive")] bool? FrameworkWindowActive,
    [property: JsonPropertyName("nativeFocus")] RuntimeNativeFocus NativeFocus,
    [property: JsonPropertyName("focusStatus")] string FocusStatus,
    [property: JsonPropertyName("focused")] RuntimeFocusNode? Focused,
    [property: JsonPropertyName("reference")] RuntimeFocusNode? Reference,
    [property: JsonPropertyName("scopeAncestors")] IReadOnlyList<RuntimeFocusNode> ScopeAncestors,
    [property: JsonPropertyName("modalBlocker")] RuntimeTargetContext? ModalBlocker,
    [property: JsonPropertyName("predictions")] IReadOnlyList<RuntimeFocusPrediction> Predictions,
    [property: JsonPropertyName("candidates")] IReadOnlyList<RuntimeFocusNode> Candidates,
    [property: JsonPropertyName("visitedNodes")] int VisitedNodes,
    [property: JsonPropertyName("coverageReasons")] IReadOnlyList<string> CoverageReasons,
    [property: JsonPropertyName("observedAt")] DateTimeOffset ObservedAt,
    [property: JsonPropertyName("rememberedScopeFocus")] string RememberedScopeFocus = "unavailable_public_api",
    [property: JsonPropertyName("eventRouting")] string EventRouting = "handlers_ime_and_future_navigation_unknown;use_explicit_probe");

public sealed record RuntimeFocusProbeRequest
{
    [JsonConstructor]
    public RuntimeFocusProbeRequest(RuntimeTargetContext target, string direction = "next", string strategy = "synthetic",
        int settleMs = 100, RuntimeEvidencePolicy? policy = null)
    {
        Target = target ?? throw new ArgumentNullException(nameof(target));
        if (target.TreeKind != TreeKinds.Visual || string.IsNullOrWhiteSpace(target.NodeId)
            || string.IsNullOrWhiteSpace(target.NodeGeneration) || string.IsNullOrWhiteSpace(target.TopLevelGeneration))
            throw new ArgumentException("A probe requires a current generation-bearing visual focus target.");
        if (direction is not ("next" or "previous") || strategy is not ("synthetic" or "native") || settleMs is < 0 or > 500)
            throw new ArgumentException("Use next/previous, synthetic/native and settleMs 0..500. A probe sends one Tab/Shift+Tab pair and can change application state.");
        Direction = direction; Strategy = strategy; SettleMs = settleMs; Policy = policy;
    }
    [JsonPropertyName("target")] public RuntimeTargetContext Target { get; }
    [JsonPropertyName("direction")] public string Direction { get; }
    [JsonPropertyName("strategy")] public string Strategy { get; }
    [JsonPropertyName("settleMs")] public int SettleMs { get; }
    [JsonPropertyName("policy")] public RuntimeEvidencePolicy? Policy { get; }
}

public sealed record RuntimeFocusProbeResponse(
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("direction")] string Direction,
    [property: JsonPropertyName("focusChanged")] bool FocusChanged,
    [property: JsonPropertyName("before")] RuntimeFocusSnapshot Before,
    [property: JsonPropertyName("after")] RuntimeFocusSnapshot? After,
    [property: JsonPropertyName("input")] InputResponse? Input,
    [property: JsonPropertyName("diagnostics")] IReadOnlyList<ProtocolError> Diagnostics,
    [property: JsonPropertyName("stateChanging")] bool StateChanging = true,
    [property: JsonPropertyName("restored")] bool Restored = false);
