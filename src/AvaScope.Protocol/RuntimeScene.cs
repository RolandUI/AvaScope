using System.Text.Json.Serialization;

namespace AvaScope.Protocol;

public sealed record RuntimeSceneRequest
{
    [JsonConstructor]
    public RuntimeSceneRequest(RuntimeTargetContext canvas, string action = "inspect", string? objectId = null,
        string? objectType = null, string? relatedTo = null, int maxObjects = 64, RuntimeSceneObjectTarget? expectedObject = null,
        string? actionName = null, string? requestId = null, IReadOnlyDictionary<string, string>? parameters = null,
        bool allowDestructive = false, RuntimeEvidencePolicy? policy = null)
    {
        Canvas = canvas ?? throw new ArgumentNullException(nameof(canvas));
        if (canvas.TreeKind != TreeKinds.Visual || string.IsNullOrEmpty(canvas.NodeId)
            || string.IsNullOrEmpty(canvas.NodeGeneration) || string.IsNullOrEmpty(canvas.TopLevelGeneration))
            throw new ArgumentException("Scene operations require a fresh visual canvas target with both generation tokens.");
        if (action is not ("inspect" or "invoke")) throw new ArgumentException("Scene action must be inspect or invoke.");
        if (maxObjects is < 1 or > 128) throw new ArgumentOutOfRangeException(nameof(maxObjects), "Return at most 1..128 scene objects.");
        foreach (var value in new[] { objectId, objectType, relatedTo, actionName, requestId })
            if (value is not null) ValidateIdentifier(value);
        if (action == "inspect")
        {
            if (expectedObject is not null || actionName is not null || requestId is not null || parameters is not null || allowDestructive)
                throw new ArgumentException("Scene inspection cannot include action options.");
        }
        else
        {
            if (expectedObject is null || actionName is null || requestId is null)
                throw new ArgumentException("Scene invocation requires an observed object target, registered action name and request id.");
            if (objectId is not null || objectType is not null || relatedTo is not null)
                throw new ArgumentException("Scene invocation uses the exact observed object, not query filters.");
        }
        if (parameters is { Count: > 16 } || parameters?.Any(pair => string.IsNullOrWhiteSpace(pair.Key)
            || pair.Key.Length > 64 || pair.Value is null || pair.Value.Length > 1024) == true)
            throw new ArgumentException("Scene action parameters allow 16 fields, 64-character names and 1024-character values.");
        Action = action; ObjectId = objectId; ObjectType = objectType; RelatedTo = relatedTo; MaxObjects = maxObjects;
        ExpectedObject = expectedObject; ActionName = actionName; RequestId = requestId;
        Parameters = parameters?.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
        AllowDestructive = allowDestructive; Policy = policy;
    }

    public static void ValidateIdentifier(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 128)
            throw new ArgumentException("Scene identifiers must contain 1..128 characters.");
    }

    [JsonPropertyName("canvas")] public RuntimeTargetContext Canvas { get; }
    [JsonPropertyName("action")] public string Action { get; }
    [JsonPropertyName("objectId")] public string? ObjectId { get; }
    [JsonPropertyName("objectType")] public string? ObjectType { get; }
    [JsonPropertyName("relatedTo")] public string? RelatedTo { get; }
    [JsonPropertyName("maxObjects")] public int MaxObjects { get; }
    [JsonPropertyName("expectedObject")] public RuntimeSceneObjectTarget? ExpectedObject { get; }
    [JsonPropertyName("actionName")] public string? ActionName { get; }
    [JsonPropertyName("requestId")] public string? RequestId { get; }
    [JsonPropertyName("parameters")] public IReadOnlyDictionary<string, string>? Parameters { get; }
    [JsonPropertyName("allowDestructive")] public bool AllowDestructive { get; }
    [JsonPropertyName("policy")] public RuntimeEvidencePolicy? Policy { get; }
}

public sealed record RuntimeSceneObjectTarget
{
    [JsonConstructor]
    public RuntimeSceneObjectTarget(string sceneGeneration, string sceneRevision, string objectId, string objectGeneration)
    {
        foreach (var value in new[] { sceneGeneration, objectId, objectGeneration }) RuntimeSceneRequest.ValidateIdentifier(value);
        if (sceneRevision is null || sceneRevision.Length != 64 || sceneRevision.Any(c => !char.IsAsciiHexDigit(c)))
            throw new ArgumentException("Use the exact scene revision returned by inspection.");
        SceneGeneration = sceneGeneration; SceneRevision = sceneRevision; ObjectId = objectId; ObjectGeneration = objectGeneration;
    }
    [JsonPropertyName("sceneGeneration")] public string SceneGeneration { get; }
    [JsonPropertyName("sceneRevision")] public string SceneRevision { get; }
    [JsonPropertyName("objectId")] public string ObjectId { get; }
    [JsonPropertyName("objectGeneration")] public string ObjectGeneration { get; }
}

/// <summary>Host-declared meaning and geometry; never inferred from pixels or arbitrary view models.</summary>
public sealed record RuntimeSceneObject(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("generation")] string Generation,
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("label")] string? Label = null,
    [property: JsonPropertyName("bounds")] NodeBounds? Bounds = null,
    [property: JsonPropertyName("selected")] bool? Selected = null,
    [property: JsonPropertyName("relationships")] IReadOnlyList<RuntimeSceneRelationship>? Relationships = null,
    [property: JsonPropertyName("actions")] IReadOnlyList<string>? Actions = null,
    [property: JsonPropertyName("privacyAutomationId")] string? PrivacyAutomationId = null,
    [property: JsonPropertyName("metadata")] IReadOnlyDictionary<string, string>? Metadata = null);

public sealed record RuntimeSceneRelationship(
    [property: JsonPropertyName("kind")] string Kind,
    [property: JsonPropertyName("targetId")] string TargetId,
    [property: JsonPropertyName("targetGeneration")] string TargetGeneration);

public sealed record RuntimeSceneTransform(
    [property: JsonPropertyName("m11")] double M11,
    [property: JsonPropertyName("m12")] double M12,
    [property: JsonPropertyName("m21")] double M21,
    [property: JsonPropertyName("m22")] double M22,
    [property: JsonPropertyName("m31")] double M31,
    [property: JsonPropertyName("m32")] double M32);

public sealed record RuntimeSceneItem(
    [property: JsonPropertyName("object")] RuntimeSceneObject Object,
    [property: JsonPropertyName("target")] RuntimeSceneObjectTarget? Target,
    [property: JsonPropertyName("canvasBounds")] NodeBounds? CanvasBounds,
    [property: JsonPropertyName("topLevelBounds")] NodeBounds? TopLevelBounds,
    [property: JsonPropertyName("unavailable")] IReadOnlyList<string> Unavailable);

public sealed record RuntimeSceneSnapshot(
    [property: JsonPropertyName("canvas")] RuntimeTargetContext Canvas,
    [property: JsonPropertyName("sceneGeneration")] string SceneGeneration,
    [property: JsonPropertyName("revision")] string Revision,
    [property: JsonPropertyName("objects")] IReadOnlyList<RuntimeSceneItem> Objects,
    [property: JsonPropertyName("coverage")] string Coverage,
    [property: JsonPropertyName("examinedObjects")] int ExaminedObjects,
    [property: JsonPropertyName("matchingObjects")] int MatchingObjects,
    [property: JsonPropertyName("sceneToCanvas")] RuntimeSceneTransform? SceneToCanvas,
    [property: JsonPropertyName("canvasToTopLevel")] RuntimeSceneTransform? CanvasToTopLevel,
    [property: JsonPropertyName("renderScaling")] double RenderScaling,
    [property: JsonPropertyName("unavailable")] IReadOnlyList<string> Unavailable,
    [property: JsonPropertyName("observedAt")] DateTimeOffset ObservedAt,
    [property: JsonPropertyName("provenance")] string Provenance = "host_declared_semantics; transformed_axis_aligned_bounds_in_DIPs; not_native_hit_test");

public sealed record RuntimeSceneResponse(
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("snapshot")] RuntimeSceneSnapshot? Snapshot,
    [property: JsonPropertyName("action")] RuntimeCustomActionResponse? Action,
    [property: JsonPropertyName("diagnostics")] IReadOnlyList<ProtocolError> Diagnostics);
