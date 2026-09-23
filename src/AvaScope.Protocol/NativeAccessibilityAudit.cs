using System.Text.Json.Serialization;

namespace AvaScope.Protocol;

public sealed record NativeAccessibilityAuditRequest
{
    [JsonConstructor]
    public NativeAccessibilityAuditRequest(RuntimeTargetContext target, int maxNodes = 64, int maxDepth = 16, int timeoutMs = 3000,
        IReadOnlyList<NativeAccessibilityExpectation>? expectations = null, RuntimeEvidencePolicy? policy = null)
    {
        Target = target ?? throw new ArgumentNullException(nameof(target));
        if (target.TargetKind != "top_level" || target.NodeId is not null || string.IsNullOrEmpty(target.TopLevelGeneration))
            throw new ArgumentException("Select a registered top-level with its observed generation.");
        if (maxNodes is < 1 or > 256 || maxDepth is < 1 or > 32 || timeoutMs is < 250 or > 5000)
            throw new ArgumentOutOfRangeException(nameof(maxNodes), "Use 1..256 nodes, 1..32 levels and 250..5000 ms.");
        Expectations = expectations?.ToArray() ?? [];
        if (Expectations.Count > 32 || Expectations.Any(e => e is null || e.Target is null || e.Target.SessionId != target.SessionId || e.Target.TopLevelId != target.TopLevelId
            || e.Target.TopLevelGeneration != target.TopLevelGeneration || e.Target.TreeKind != TreeKinds.Visual || string.IsNullOrEmpty(e.Target.NodeGeneration)
            || string.IsNullOrEmpty(e.Target.NodeId) || e.Name?.Length > 256 || e.Role?.Length > 64))
            throw new ArgumentException("At most 32 expectations may name current visual nodes within the selected top-level.");
        if (Expectations.Select(e => e.Target.NodeId).Distinct(StringComparer.Ordinal).Count() != Expectations.Count)
            throw new ArgumentException("Each expected control may occur only once.");
        MaxNodes = maxNodes; MaxDepth = maxDepth; TimeoutMs = timeoutMs; Policy = policy;
    }
    [JsonPropertyName("target")] public RuntimeTargetContext Target { get; }
    [JsonPropertyName("maxNodes")] public int MaxNodes { get; }
    [JsonPropertyName("maxDepth")] public int MaxDepth { get; }
    [JsonPropertyName("timeoutMs")] public int TimeoutMs { get; }
    [JsonPropertyName("expectations")] public IReadOnlyList<NativeAccessibilityExpectation> Expectations { get; }
    [JsonPropertyName("policy")] public RuntimeEvidencePolicy? Policy { get; }
}

public sealed record NativeAccessibilityExpectation(
    [property: JsonPropertyName("target")] RuntimeTargetContext Target,
    [property: JsonPropertyName("name")] string? Name = null,
    [property: JsonPropertyName("role")] string? Role = null);

public sealed record BridgeAccessibilityEvidence(
    [property: JsonPropertyName("target")] RuntimeTargetContext Target,
    [property: JsonPropertyName("visualType")] string VisualType,
    [property: JsonPropertyName("automationId")] string? AutomationId,
    [property: JsonPropertyName("name")] string? Name,
    [property: JsonPropertyName("role")] string? Role,
    [property: JsonPropertyName("isControlElement")] bool? IsControlElement,
    [property: JsonPropertyName("visible")] bool Visible,
    [property: JsonPropertyName("enabled")] bool Enabled,
    [property: JsonPropertyName("desktopBounds")] NodeBounds? DesktopBounds,
    [property: JsonPropertyName("expectation")] NativeAccessibilityExpectation? Expectation);

public sealed record NativeAccessibilityNode(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("parentId")] string? ParentId,
    [property: JsonPropertyName("automationId")] string? AutomationId,
    [property: JsonPropertyName("name")] string? Name,
    [property: JsonPropertyName("role")] string? Role,
    [property: JsonPropertyName("enabled")] bool? Enabled,
    [property: JsonPropertyName("offscreen")] bool? Offscreen,
    [property: JsonPropertyName("desktopBounds")] NodeBounds? DesktopBounds);

public sealed record NativeAccessibilitySnapshot(
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("adapter")] string Adapter,
    [property: JsonPropertyName("coordinateUnits")] string? CoordinateUnits,
    [property: JsonPropertyName("observedAt")] DateTimeOffset ObservedAt,
    [property: JsonPropertyName("nodes")] IReadOnlyList<NativeAccessibilityNode> Nodes,
    [property: JsonPropertyName("truncated")] bool Truncated,
    [property: JsonPropertyName("diagnostics")] IReadOnlyList<ProtocolError> Diagnostics);

public sealed record NativeAccessibilityComparison(
    [property: JsonPropertyName("target")] RuntimeTargetContext Target,
    [property: JsonPropertyName("mapping")] string Mapping,
    [property: JsonPropertyName("confidence")] string Confidence,
    [property: JsonPropertyName("nativeIds")] IReadOnlyList<string> NativeIds,
    [property: JsonPropertyName("findings")] IReadOnlyList<string> Findings);

public sealed record NativeAccessibilityAuditResponse(
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("target")] RuntimeTargetContext Target,
    [property: JsonPropertyName("processId")] int ProcessId,
    [property: JsonPropertyName("bridgeObservedAt")] DateTimeOffset BridgeObservedAt,
    [property: JsonPropertyName("bridgeNodes")] IReadOnlyList<BridgeAccessibilityEvidence> BridgeNodes,
    [property: JsonPropertyName("bridgeTruncated")] bool BridgeTruncated,
    [property: JsonPropertyName("native")] NativeAccessibilitySnapshot Native,
    [property: JsonPropertyName("comparisons")] IReadOnlyList<NativeAccessibilityComparison> Comparisons,
    [property: JsonPropertyName("limitations")] IReadOnlyList<string> Limitations);
