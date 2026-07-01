using System.Text.Json.Serialization;

namespace AvaScope.Protocol;

public sealed record TreeNodeSummary
{
    [JsonConstructor]
    public TreeNodeSummary(
        string nodeId,
        string nodeType,
        string? name = null,
        string? automationId = null,
        string? text = null,
        NodeBounds? bounds = null,
        IReadOnlyList<string>? classes = null,
        IReadOnlyList<TreeNodeSummary>? children = null,
        RuntimeTargetContext? target = null,
        RuntimeAccessibilityState? accessibilityState = null,
        RuntimeValidationState? validationState = null,
        RuntimeNodeSourceMap? sourceMap = null)
    {
        if (string.IsNullOrWhiteSpace(nodeId))
        {
            throw new ArgumentException("Node id cannot be empty.", nameof(nodeId));
        }

        if (string.IsNullOrWhiteSpace(nodeType))
        {
            throw new ArgumentException("Node type cannot be empty.", nameof(nodeType));
        }

        NodeId = nodeId;
        NodeType = nodeType;
        Name = name;
        AutomationId = automationId;
        Text = text;
        Bounds = bounds;
        Classes = classes ?? Array.Empty<string>();
        Children = children ?? Array.Empty<TreeNodeSummary>();
        Target = target;
        AccessibilityState = accessibilityState;
        ValidationState = validationState;
        SourceMap = sourceMap;
    }

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

    [JsonPropertyName("children")]
    public IReadOnlyList<TreeNodeSummary> Children { get; }

    [JsonPropertyName("target")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public RuntimeTargetContext? Target { get; }

    [JsonPropertyName("accessibilityState")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public RuntimeAccessibilityState? AccessibilityState { get; }

    [JsonPropertyName("validationState")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public RuntimeValidationState? ValidationState { get; }

    [JsonPropertyName("sourceMap")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public RuntimeNodeSourceMap? SourceMap { get; }
}
