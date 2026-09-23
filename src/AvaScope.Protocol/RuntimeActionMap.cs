using System.Text.Json.Serialization;

namespace AvaScope.Protocol;

public sealed record RuntimeActionMapRequest
{
    [JsonConstructor]
    public RuntimeActionMapRequest(SessionId sessionId, string topLevelId, string? search = null,
        int maxNodes = 1024, int maxDepth = 16, int maxResults = 40, RuntimeEvidencePolicy? policy = null)
    {
        SessionId = sessionId ?? throw new ArgumentNullException(nameof(sessionId));
        if (string.IsNullOrWhiteSpace(topLevelId) || topLevelId.Length > 256) throw new ArgumentException("Select one explicit top-level.");
        if (search?.Length > 256) throw new ArgumentException("Action search is limited to 256 characters.");
        if (maxNodes is < 1 or > 4096 || maxDepth is < 1 or > 32 || maxResults is < 1 or > 128)
            throw new ArgumentException("Action maps allow 1..4096 nodes, depth 1..32 and 1..128 results.");
        TopLevelId = topLevelId; Search = string.IsNullOrWhiteSpace(search) ? null : search.Trim();
        MaxNodes = maxNodes; MaxDepth = maxDepth; MaxResults = maxResults; Policy = policy;
    }
    [JsonPropertyName("sessionId")] public SessionId SessionId { get; }
    [JsonPropertyName("topLevelId")] public string TopLevelId { get; }
    [JsonPropertyName("search")] public string? Search { get; }
    [JsonPropertyName("maxNodes")] public int MaxNodes { get; }
    [JsonPropertyName("maxDepth")] public int MaxDepth { get; }
    [JsonPropertyName("maxResults")] public int MaxResults { get; }
    [JsonPropertyName("policy")] public RuntimeEvidencePolicy? Policy { get; }
}

public sealed record RuntimeActionShortcut(
    [property: JsonPropertyName("gesture")] string Gesture,
    [property: JsonPropertyName("source")] string Source);

public sealed record RuntimeMappedAction(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("kind")] string Kind,
    [property: JsonPropertyName("label")] string? Label,
    [property: JsonPropertyName("route")] IReadOnlyList<string> Route,
    [property: JsonPropertyName("routeProvenance")] string RouteProvenance,
    [property: JsonPropertyName("target")] RuntimeTargetContext? Target,
    [property: JsonPropertyName("revealTarget")] RuntimeTargetContext? RevealTarget,
    [property: JsonPropertyName("actions")] IReadOnlyList<string> Actions,
    [property: JsonPropertyName("availability")] string Availability,
    [property: JsonPropertyName("reasons")] IReadOnlyList<string> Reasons,
    [property: JsonPropertyName("description")] string? Description,
    [property: JsonPropertyName("shortcuts")] IReadOnlyList<RuntimeActionShortcut> Shortcuts,
    [property: JsonPropertyName("sameLabelCountAtLeast")] int SameLabelCountAtLeast,
    [property: JsonPropertyName("childContent")] string? ChildContent = null,
    [property: JsonPropertyName("customActionName")] string? CustomActionName = null);

public sealed record RuntimeActionMapCoverage(
    [property: JsonPropertyName("visitedNodes")] int VisitedNodes,
    [property: JsonPropertyName("observedActions")] int ObservedActions,
    [property: JsonPropertyName("completeObservedScope")] bool CompleteObservedScope,
    [property: JsonPropertyName("reasons")] IReadOnlyList<string> Reasons,
    [property: JsonPropertyName("unobservedContent")] string UnobservedContent = "lazy_population_and_native_menus_unknown");

public sealed record RuntimeActionMapResponse(
    [property: JsonPropertyName("sessionId")] SessionId SessionId,
    [property: JsonPropertyName("topLevelId")] string TopLevelId,
    [property: JsonPropertyName("actions")] IReadOnlyList<RuntimeMappedAction> Actions,
    [property: JsonPropertyName("coverage")] RuntimeActionMapCoverage Coverage,
    [property: JsonPropertyName("observedAt")] DateTimeOffset ObservedAt);
