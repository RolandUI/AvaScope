using System.Text.Json.Serialization;

namespace AvaScope.Protocol;

public sealed record RuntimeWindowRequest
{
    public static IReadOnlyList<string> Actions { get; } = ["inspect", "activate", "bring_to_front", "minimize", "maximize", "restore", "move", "resize"];

    [JsonConstructor]
    public RuntimeWindowRequest(RuntimeTargetContext target, string action = "inspect", string? expectedRevision = null,
        RuntimeWindowPosition? position = null, RuntimeSize? clientSize = null, int timeoutMs = 1500, RuntimeEvidencePolicy? policy = null)
    {
        Target = target ?? throw new ArgumentNullException(nameof(target));
        if (target.NodeId is not null || target.TargetKind != "top_level") throw new ArgumentException("Select an explicit top-level window, not a control node.");
        if (!Actions.Contains(action, StringComparer.Ordinal)) throw new ArgumentException("Unknown window action; close is deliberately separate and unavailable here.");
        if (action != "inspect" && (string.IsNullOrEmpty(target.TopLevelGeneration) || expectedRevision is null))
            throw new ArgumentException("Window changes require the generation and revision from a fresh inspection.");
        if (expectedRevision is not null && (expectedRevision.Length != 64 || expectedRevision.Any(c => !char.IsAsciiHexDigit(c))))
            throw new ArgumentException("Use the exact observed window revision.");
        if ((action == "move") != (position is not null) || (action == "resize") != (clientSize is not null))
            throw new ArgumentException("Only move requires position; only resize requires clientSize in DIPs.");
        if (clientSize is { } size && (!double.IsFinite(size.Width) || !double.IsFinite(size.Height) || size.Width is < 32 or > 16384 || size.Height is < 32 or > 16384))
            throw new ArgumentException("Client size must contain finite dimensions in 32..16384 DIPs.");
        if (timeoutMs is < 100 or > 3000) throw new ArgumentOutOfRangeException(nameof(timeoutMs));
        Action = action; ExpectedRevision = expectedRevision; Position = position; ClientSize = clientSize; TimeoutMs = timeoutMs; Policy = policy;
    }
    [JsonPropertyName("target")] public RuntimeTargetContext Target { get; }
    [JsonPropertyName("action")] public string Action { get; }
    [JsonPropertyName("expectedRevision")] public string? ExpectedRevision { get; }
    [JsonPropertyName("position")] public RuntimeWindowPosition? Position { get; }
    [JsonPropertyName("clientSize")] public RuntimeSize? ClientSize { get; }
    [JsonPropertyName("timeoutMs")] public int TimeoutMs { get; }
    [JsonPropertyName("policy")] public RuntimeEvidencePolicy? Policy { get; }
}

public sealed record RuntimeWindowPosition
{
    [JsonConstructor]
    public RuntimeWindowPosition(double x, double y, string coordinateSpace = "desktop", string? monitorId = null)
    {
        if (!double.IsFinite(x) || !double.IsFinite(y) || Math.Abs(x) > 1000000 || Math.Abs(y) > 1000000)
            throw new ArgumentException("Position must contain finite desktop coordinates within +/-1000000.");
        if (coordinateSpace is not ("desktop" or "monitor_dip")
            || coordinateSpace == "monitor_dip" && (string.IsNullOrWhiteSpace(monitorId) || monitorId.Length > 128)
            || coordinateSpace == "desktop" && (monitorId is not null || x != Math.Truncate(x) || y != Math.Truncate(y)))
            throw new ArgumentException("Use integral desktop coordinates or DIP offsets from a selected monitor's work-area origin.");
        X = x; Y = y; CoordinateSpace = coordinateSpace; MonitorId = monitorId;
    }
    [JsonPropertyName("x")] public double X { get; }
    [JsonPropertyName("y")] public double Y { get; }
    [JsonPropertyName("coordinateSpace")] public string CoordinateSpace { get; }
    [JsonPropertyName("monitorId")] public string? MonitorId { get; }
}

public sealed record RuntimeMonitorInfo(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("name")] string? Name,
    [property: JsonPropertyName("desktopBounds")] NodeBounds DesktopBounds,
    [property: JsonPropertyName("workArea")] NodeBounds WorkArea,
    [property: JsonPropertyName("desktopUnits")] string DesktopUnits,
    [property: JsonPropertyName("desktopScale")] double DesktopScale,
    [property: JsonPropertyName("logicalSize")] RuntimeSize LogicalSize,
    [property: JsonPropertyName("physicalPixelBounds")] NodeBounds? PhysicalPixelBounds,
    [property: JsonPropertyName("primary")] bool Primary);

public sealed record RuntimeWindowSnapshot(
    [property: JsonPropertyName("target")] RuntimeTargetContext Target,
    [property: JsonPropertyName("revision")] string Revision,
    [property: JsonPropertyName("backend")] RuntimeBackendInfo Backend,
    [property: JsonPropertyName("title")] string? Title,
    [property: JsonPropertyName("state")] string State,
    [property: JsonPropertyName("nativeState")] RuntimeNativeWindowState NativeState,
    [property: JsonPropertyName("visible")] bool Visible,
    [property: JsonPropertyName("active")] bool Active,
    [property: JsonPropertyName("nativeFocus")] RuntimeNativeFocus NativeFocus,
    [property: JsonPropertyName("position")] RuntimeWindowPosition Position,
    [property: JsonPropertyName("desktopUnits")] string DesktopUnits,
    [property: JsonPropertyName("clientSize")] RuntimeSize ClientSize,
    [property: JsonPropertyName("renderScaling")] double RenderScaling,
    [property: JsonPropertyName("desktopScaling")] double DesktopScaling,
    [property: JsonPropertyName("isDialog")] bool IsDialog,
    [property: JsonPropertyName("owner")] RuntimeTargetContext? Owner,
    [property: JsonPropertyName("modalBlocker")] RuntimeTargetContext? ModalBlocker,
    [property: JsonPropertyName("frontmostRegisteredWindow")] bool? FrontmostRegisteredWindow,
    [property: JsonPropertyName("monitors")] IReadOnlyList<RuntimeMonitorInfo> Monitors,
    [property: JsonPropertyName("availableActions")] IReadOnlyList<string> AvailableActions,
    [property: JsonPropertyName("unavailable")] IReadOnlyList<string> Unavailable,
    [property: JsonPropertyName("observedAt")] DateTimeOffset ObservedAt);

public sealed record RuntimeNativeWindowState(
    [property: JsonPropertyName("state")] string State,
    [property: JsonPropertyName("source")] string Source);

public sealed record RuntimeWindowResponse(
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("action")] string Action,
    [property: JsonPropertyName("before")] RuntimeWindowSnapshot? Before,
    [property: JsonPropertyName("after")] RuntimeWindowSnapshot? After,
    [property: JsonPropertyName("dispatchedOperations")] int DispatchedOperations,
    [property: JsonPropertyName("provenance")] RuntimeOperationProvenance? Provenance,
    [property: JsonPropertyName("verification")] string Verification,
    [property: JsonPropertyName("diagnostics")] IReadOnlyList<ProtocolError> Diagnostics);
