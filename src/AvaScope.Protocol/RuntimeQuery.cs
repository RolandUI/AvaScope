using System.Text.Json;
using System.Text.Json.Serialization;

namespace AvaScope.Protocol;

public sealed record RuntimeSelectorRelationship
{
    [JsonConstructor]
    public RuntimeSelectorRelationship(string kind, SemanticWorkflowSelector selector, int maxDepth = 8)
    {
        if (kind is not ("parent" or "ancestor" or "descendant" or "labeled_by"))
            throw new ArgumentException("Relationship kind must be parent, ancestor, descendant or labeled_by.", nameof(kind));
        if (maxDepth is < 1 or > 32) throw new ArgumentOutOfRangeException(nameof(maxDepth));
        Selector = selector ?? throw new ArgumentNullException(nameof(selector));
        if (!selector.HasSearchCriteria) throw new ArgumentException("A related selector must have explicit criteria.", nameof(selector));
        Kind = kind;
        MaxDepth = maxDepth;
    }
    [JsonPropertyName("kind")] public string Kind { get; }
    [JsonPropertyName("selector")] public SemanticWorkflowSelector Selector { get; }
    [JsonPropertyName("maxDepth")] public int MaxDepth { get; }
}

public sealed record RuntimeQueryRequest
{
    public const int MaximumDepth = 64;
    public static IReadOnlyList<string> SupportedAttributes { get; } =
        ["name", "automationId", "text", "nodeType", "role", "visible", "enabled", "rendered", "actionable", "focused", "checked", "selected", "value"];

    [JsonConstructor]
    public RuntimeQueryRequest(SessionId sessionId, string topLevelId, SemanticWorkflowSelector selector,
        IReadOnlyList<string>? attributes = null, int maxResults = 16, int maxNodes = 512, int maxDepth = 16,
        RuntimeEvidencePolicy? policy = null)
    {
        SessionId = sessionId ?? throw new ArgumentNullException(nameof(sessionId));
        if (string.IsNullOrWhiteSpace(topLevelId) || topLevelId.Length > 256) throw new ArgumentException("Select an explicit top-level id of at most 256 characters.");
        Selector = selector ?? throw new ArgumentNullException(nameof(selector));
        if (!selector.HasSearchCriteria) throw new ArgumentException("A query requires selector criteria.", nameof(selector));
        if (maxResults is < 1 or > 64 || maxNodes is < 1 or > 2048 || maxDepth is < 0 or > MaximumDepth)
            throw new ArgumentException($"Queries allow 1..64 results, 1..2048 visited nodes and depth 0..{MaximumDepth}.");
        var requested = (attributes ?? []).Distinct(StringComparer.Ordinal).ToArray();
        if (requested.Length > 8 || requested.Any(attribute => !SupportedAttributes.Contains(attribute, StringComparer.Ordinal)))
            throw new ArgumentException("Select at most eight supported attributes.", nameof(attributes));
        ValidateSelector(selector, selector.TreeKind);
        TopLevelId = topLevelId; Attributes = requested; MaxResults = maxResults; MaxNodes = maxNodes;
        MaxDepth = Math.Min(maxDepth, selector.MaxDepth ?? maxDepth); Policy = policy;
    }

    private static void ValidateSelector(SemanticWorkflowSelector selector, string treeKind)
    {
        if (selector.TreeKind is not (TreeKinds.Visual or TreeKinds.Logical) || selector.TreeKind != treeKind)
            throw new ArgumentException("A query and its relationships must use the same visual or logical tree.");
        if (new[] { selector.NodeId, selector.AutomationId, selector.Name, selector.Text, selector.NodeType, selector.Role,
            selector.BindingPath, selector.CommandName }.Any(value => value?.Length > 512))
            throw new ArgumentException("Query selector strings are limited to 512 characters.");
        foreach (var relation in selector.Relationships) ValidateSelector(relation.Selector, treeKind);
    }

    [JsonPropertyName("sessionId")] public SessionId SessionId { get; }
    [JsonPropertyName("topLevelId")] public string TopLevelId { get; }
    [JsonPropertyName("selector")] public SemanticWorkflowSelector Selector { get; }
    [JsonPropertyName("attributes")] public IReadOnlyList<string> Attributes { get; }
    [JsonPropertyName("maxResults")] public int MaxResults { get; }
    [JsonPropertyName("maxNodes")] public int MaxNodes { get; }
    [JsonPropertyName("maxDepth")] public int MaxDepth { get; }
    [JsonPropertyName("policy")] public RuntimeEvidencePolicy? Policy { get; }
}

public sealed record RuntimeRelationshipEvidence(
    [property: JsonPropertyName("kind")] string Kind,
    [property: JsonPropertyName("source")] string Source,
    [property: JsonPropertyName("target")] RuntimeTargetContext Target,
    [property: JsonPropertyName("distance")] int Distance);

public sealed record RuntimeQueryValue(
    [property: JsonPropertyName("attribute")] string Attribute,
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("value")] JsonElement? Value,
    [property: JsonPropertyName("source")] string Source);

public sealed record RuntimeQueryProjection(
    [property: JsonPropertyName("target")] RuntimeTargetContext Target,
    [property: JsonPropertyName("attributes")] IReadOnlyList<RuntimeQueryValue> Attributes,
    [property: JsonPropertyName("relationships")] IReadOnlyList<RuntimeRelationshipEvidence> Relationships);

public sealed record RuntimeQueryCoverage(
    [property: JsonPropertyName("complete")] bool Complete,
    [property: JsonPropertyName("visitedNodes")] int VisitedNodes,
    [property: JsonPropertyName("matchedAtLeast")] int MatchedAtLeast,
    [property: JsonPropertyName("reasons")] IReadOnlyList<string> Reasons,
    [property: JsonPropertyName("scope")] string Scope = "realized_nodes_in_selected_top_level");

public sealed record RuntimeQueryCandidate(
    [property: JsonPropertyName("target")] RuntimeTargetContext Target,
    [property: JsonPropertyName("nodeType")] string NodeType,
    [property: JsonPropertyName("name")] string? Name,
    [property: JsonPropertyName("automationId")] string? AutomationId,
    [property: JsonPropertyName("reason")] string Reason);
