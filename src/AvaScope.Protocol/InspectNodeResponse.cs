using System.Text.Json.Serialization;

namespace AvaScope.Protocol;

public sealed record InspectNodeResponse
{
    [JsonConstructor]
    public InspectNodeResponse(
        SessionId sessionId,
        string topLevelId,
        string treeKind,
        string nodeId,
        string nodeType,
        int childCount,
        string? name = null,
        string? automationId = null,
        string? text = null,
        NodeBounds? bounds = null,
        IReadOnlyList<string>? classes = null,
        IReadOnlyList<ComputedPropertyValue>? computedProperties = null,
        RuntimeTargetContext? target = null,
        RuntimeScrollState? scrollState = null,
        RuntimeBindingState? bindingState = null,
        RuntimeDebugState? debugState = null,
        RuntimeAccessibilityState? accessibilityState = null,
        RuntimeValidationState? validationState = null,
        RuntimeNodeSourceMap? sourceMap = null,
        RuntimeLayoutExplanation? layoutExplanation = null,
        RuntimeNodeInteractionState? interactionState = null)
    {
        SessionId = sessionId ?? throw new ArgumentNullException(nameof(sessionId));

        if (string.IsNullOrWhiteSpace(topLevelId))
        {
            throw new ArgumentException("Top-level id cannot be empty.", nameof(topLevelId));
        }

        if (string.IsNullOrWhiteSpace(treeKind))
        {
            throw new ArgumentException("Tree kind cannot be empty.", nameof(treeKind));
        }

        if (string.IsNullOrWhiteSpace(nodeId))
        {
            throw new ArgumentException("Node id cannot be empty.", nameof(nodeId));
        }

        if (string.IsNullOrWhiteSpace(nodeType))
        {
            throw new ArgumentException("Node type cannot be empty.", nameof(nodeType));
        }

        if (childCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(childCount), childCount, "Child count cannot be negative.");
        }

        SessionId = sessionId;
        TopLevelId = topLevelId;
        TreeKind = treeKind;
        NodeId = nodeId;
        NodeType = nodeType;
        Name = name;
        AutomationId = automationId;
        Text = text;
        Bounds = bounds;
        Classes = classes ?? Array.Empty<string>();
        ComputedProperties = computedProperties ?? Array.Empty<ComputedPropertyValue>();
        ChildCount = childCount;
        Target = target ?? new RuntimeTargetContext(sessionId, topLevelId, treeKind, nodeId);
        ScrollState = scrollState;
        BindingState = bindingState;
        DebugState = debugState;
        AccessibilityState = accessibilityState;
        ValidationState = validationState;
        SourceMap = sourceMap;
        LayoutExplanation = layoutExplanation;
        InteractionState = interactionState;
    }

    [JsonPropertyName("sessionId")]
    public SessionId SessionId { get; }

    [JsonPropertyName("topLevelId")]
    public string TopLevelId { get; }

    [JsonPropertyName("treeKind")]
    public string TreeKind { get; }

    [JsonPropertyName("nodeId")]
    public string NodeId { get; }

    [JsonPropertyName("nodeType")]
    public string NodeType { get; }

    [JsonPropertyName("name")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Name { get; }

    [JsonPropertyName("automationId")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? AutomationId { get; }

    [JsonPropertyName("text")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Text { get; }

    [JsonPropertyName("bounds")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NodeBounds? Bounds { get; }

    [JsonPropertyName("classes")]
    public IReadOnlyList<string> Classes { get; }

    [JsonPropertyName("childCount")]
    public int ChildCount { get; }

    [JsonPropertyName("computedProperties")]
    public IReadOnlyList<ComputedPropertyValue> ComputedProperties { get; }

    [JsonPropertyName("target")]
    public RuntimeTargetContext Target { get; }

    [JsonPropertyName("scrollState")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public RuntimeScrollState? ScrollState { get; }

    [JsonPropertyName("bindingState")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public RuntimeBindingState? BindingState { get; }

    [JsonPropertyName("debugState")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public RuntimeDebugState? DebugState { get; }

    [JsonPropertyName("accessibilityState")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public RuntimeAccessibilityState? AccessibilityState { get; }

    [JsonPropertyName("validationState")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public RuntimeValidationState? ValidationState { get; }

    [JsonPropertyName("sourceMap")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public RuntimeNodeSourceMap? SourceMap { get; }

    [JsonPropertyName("layoutExplanation")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public RuntimeLayoutExplanation? LayoutExplanation { get; }

    [JsonPropertyName("interactionState")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public RuntimeNodeInteractionState? InteractionState { get; }
}
