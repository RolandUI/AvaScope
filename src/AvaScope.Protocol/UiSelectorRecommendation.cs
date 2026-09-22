using System.Text.Json.Serialization;

namespace AvaScope.Protocol;

public sealed record UiSelectorRecommendation(
    [property: JsonPropertyName("nodeId")] string NodeId,
    [property: JsonPropertyName("selector")] SemanticWorkflowSelector? Selector,
    [property: JsonPropertyName("matchCount")] int MatchCount,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("stability")] string Stability,
    [property: JsonPropertyName("reason")] string Reason);
