using System.Text.Json.Serialization;

namespace AvaScope.Protocol;

public sealed record SemanticWorkflowSelector
{
    [JsonConstructor]
    public SemanticWorkflowSelector(
        string? nodeId = null,
        string? treeKind = null,
        string? automationId = null,
        string? text = null,
        string? name = null,
        string? nodeType = null,
        string? role = null,
        string? bindingPath = null,
        string? commandName = null,
        int? maxDepth = null)
    {
        if (maxDepth is < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxDepth), maxDepth, "Max depth cannot be negative.");
        }

        NodeId = string.IsNullOrWhiteSpace(nodeId) ? null : nodeId;
        TreeKind = string.IsNullOrWhiteSpace(treeKind) ? TreeKinds.Visual : treeKind;
        AutomationId = string.IsNullOrWhiteSpace(automationId) ? null : automationId;
        Text = string.IsNullOrWhiteSpace(text) ? null : text;
        Name = string.IsNullOrWhiteSpace(name) ? null : name;
        NodeType = string.IsNullOrWhiteSpace(nodeType) ? null : nodeType;
        Role = string.IsNullOrWhiteSpace(role) ? null : role;
        BindingPath = string.IsNullOrWhiteSpace(bindingPath) ? null : bindingPath;
        CommandName = string.IsNullOrWhiteSpace(commandName) ? null : commandName;
        MaxDepth = maxDepth;
    }

    [JsonPropertyName("nodeId")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? NodeId { get; }

    [JsonPropertyName("treeKind")]
    public string TreeKind { get; }

    [JsonPropertyName("automationId")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? AutomationId { get; }

    [JsonPropertyName("text")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Text { get; }

    [JsonPropertyName("name")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Name { get; }

    [JsonPropertyName("nodeType")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? NodeType { get; }

    [JsonPropertyName("role")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Role { get; }

    [JsonPropertyName("bindingPath")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? BindingPath { get; }

    [JsonPropertyName("commandName")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? CommandName { get; }

    [JsonPropertyName("maxDepth")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? MaxDepth { get; }

    public bool HasSearchCriteria =>
        !string.IsNullOrWhiteSpace(NodeId)
        || !string.IsNullOrWhiteSpace(AutomationId)
        || !string.IsNullOrWhiteSpace(Text)
        || !string.IsNullOrWhiteSpace(Name)
        || !string.IsNullOrWhiteSpace(NodeType)
        || !string.IsNullOrWhiteSpace(Role)
        || !string.IsNullOrWhiteSpace(BindingPath)
        || !string.IsNullOrWhiteSpace(CommandName);
}
