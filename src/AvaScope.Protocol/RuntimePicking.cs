using System.Text.Json.Serialization;

namespace AvaScope.Protocol;

public sealed record RuntimePickRequest
{
    [JsonConstructor]
    public RuntimePickRequest(RuntimeTargetContext target, double? x = null, double? y = null, string coordinateSpace = "top_level_pixel",
        string? expectedGeometryRevision = null, int maxPath = 12, RuntimeEvidencePolicy? policy = null)
    {
        Target = target ?? throw new ArgumentNullException(nameof(target));
        if (target.NodeId is not null || target.TargetKind != "top_level" || string.IsNullOrEmpty(target.TopLevelGeneration))
            throw new ArgumentException("Select an observed top-level generation.");
        if ((x is null) != (y is null) || x is { } px && (!double.IsFinite(px) || Math.Abs(px) > 1000000)
            || y is { } py && (!double.IsFinite(py) || Math.Abs(py) > 1000000)) throw new ArgumentException("Supply both finite coordinates within +/-1000000, or neither to inspect geometry.");
        if (coordinateSpace is not ("top_level_dip" or "top_level_pixel" or "desktop")) throw new ArgumentException("Use top_level_dip, top_level_pixel or desktop coordinates.");
        if (x is not null && (expectedGeometryRevision is not { Length: 64 } || expectedGeometryRevision.Any(c => !char.IsAsciiHexDigit(c))))
            throw new ArgumentException("Pick requires the revision from a recent geometry-only pick_node query.");
        if (maxPath is < 1 or > 32) throw new ArgumentOutOfRangeException(nameof(maxPath));
        X = x; Y = y; CoordinateSpace = coordinateSpace; ExpectedGeometryRevision = expectedGeometryRevision; MaxPath = maxPath; Policy = policy;
    }
    [JsonPropertyName("target")] public RuntimeTargetContext Target { get; }
    [JsonPropertyName("x")] public double? X { get; }
    [JsonPropertyName("y")] public double? Y { get; }
    [JsonPropertyName("coordinateSpace")] public string CoordinateSpace { get; }
    [JsonPropertyName("expectedGeometryRevision")] public string? ExpectedGeometryRevision { get; }
    [JsonPropertyName("maxPath")] public int MaxPath { get; }
    [JsonPropertyName("policy")] public RuntimeEvidencePolicy? Policy { get; }
}

public sealed record RuntimePickGeometry(
    [property: JsonPropertyName("revision")] string Revision,
    [property: JsonPropertyName("clientSize")] RuntimeSize ClientSize,
    [property: JsonPropertyName("pixelSize")] RuntimeSize PixelSize,
    [property: JsonPropertyName("renderScaling")] double RenderScaling,
    [property: JsonPropertyName("desktopScaling")] double DesktopScaling,
    [property: JsonPropertyName("desktopBounds")] NodeBounds? DesktopBounds,
    [property: JsonPropertyName("desktopUnits")] string? DesktopUnits,
    [property: JsonPropertyName("observedAt")] DateTimeOffset ObservedAt);

public sealed record RuntimePickedNode(
    [property: JsonPropertyName("target")] RuntimeTargetContext Target,
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("name")] string? Name,
    [property: JsonPropertyName("automationId")] string? AutomationId,
    [property: JsonPropertyName("text")] string? Text,
    [property: JsonPropertyName("bounds")] NodeBounds? Bounds,
    [property: JsonPropertyName("enabled")] bool? Enabled,
    [property: JsonPropertyName("hitTestVisible")] bool? HitTestVisible);

public sealed record RuntimePickResponse(
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("target")] RuntimeTargetContext Target,
    [property: JsonPropertyName("geometry")] RuntimePickGeometry Geometry,
    [property: JsonPropertyName("topLevelPoint")] RuntimePickPoint? TopLevelPoint,
    [property: JsonPropertyName("hitPath")] IReadOnlyList<RuntimePickedNode> HitPath,
    [property: JsonPropertyName("relatedTopLevels")] IReadOnlyList<RuntimeTargetContext> RelatedTopLevels,
    [property: JsonPropertyName("occlusion")] string Occlusion,
    [property: JsonPropertyName("truncated")] bool Truncated,
    [property: JsonPropertyName("diagnostics")] IReadOnlyList<ProtocolError> Diagnostics);

public sealed record RuntimePickPoint([property: JsonPropertyName("x")] double X, [property: JsonPropertyName("y")] double Y);

public sealed record RuntimeHighlightRequest
{
    [JsonConstructor]
    public RuntimeHighlightRequest(RuntimeTargetContext target, string action = "show", int lifetimeMs = 1500,
        string color = "#FFB000", RuntimeEvidencePolicy? policy = null)
    {
        Target = target ?? throw new ArgumentNullException(nameof(target));
        if (action is not ("show" or "clear" or "inspect")) throw new ArgumentException("Use show, clear or inspect.");
        if (string.IsNullOrEmpty(target.TopLevelGeneration) || action == "show" && (target.TreeKind != TreeKinds.Visual || string.IsNullOrEmpty(target.NodeId) || string.IsNullOrEmpty(target.NodeGeneration)))
            throw new ArgumentException("Show requires a pinned visual node; all actions require an observed top-level generation.");
        if (lifetimeMs is < 100 or > 5000) throw new ArgumentOutOfRangeException(nameof(lifetimeMs));
        if (color is not { Length: 7 } || color[0] != '#' || color.Skip(1).Any(c => !char.IsAsciiHexDigit(c))) throw new ArgumentException("Use an opaque #RRGGBB highlight color.");
        Action = action; LifetimeMs = lifetimeMs; Color = color; Policy = policy;
    }
    [JsonPropertyName("target")] public RuntimeTargetContext Target { get; }
    [JsonPropertyName("action")] public string Action { get; }
    [JsonPropertyName("lifetimeMs")] public int LifetimeMs { get; }
    [JsonPropertyName("color")] public string Color { get; }
    [JsonPropertyName("policy")] public RuntimeEvidencePolicy? Policy { get; }
}

public sealed record RuntimeHighlightResponse(
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("highlightId")] string? HighlightId,
    [property: JsonPropertyName("target")] RuntimeTargetContext Target,
    [property: JsonPropertyName("bounds")] NodeBounds? Bounds,
    [property: JsonPropertyName("expiresAt")] DateTimeOffset? ExpiresAt,
    [property: JsonPropertyName("inputTransparent")] bool InputTransparent,
    [property: JsonPropertyName("clearedByScreenshot")] bool ClearedByScreenshot,
    [property: JsonPropertyName("diagnostics")] IReadOnlyList<ProtocolError> Diagnostics);
