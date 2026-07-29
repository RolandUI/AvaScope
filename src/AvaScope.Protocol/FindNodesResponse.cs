using System.Text.Json.Serialization;

namespace AvaScope.Protocol;

public sealed record FindNodesResponse
{
    [JsonConstructor]
    public FindNodesResponse(
        SessionId sessionId,
        string topLevelId,
        string treeKind,
        int depthLimit,
        IReadOnlyList<FindNodeMatch>? matches = null,
        RuntimeTargetContext? target = null,
        ResponseBudgetInfo? responseBudget = null)
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

        if (depthLimit < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(depthLimit), depthLimit, "Depth limit cannot be negative.");
        }

        TopLevelId = topLevelId;
        TreeKind = treeKind;
        DepthLimit = depthLimit;
        Matches = matches ?? Array.Empty<FindNodeMatch>();
        Target = target ?? new RuntimeTargetContext(sessionId, topLevelId, treeKind);
        ResponseBudget = responseBudget;
    }

    [JsonPropertyName("sessionId")]
    public SessionId SessionId { get; }

    [JsonPropertyName("topLevelId")]
    public string TopLevelId { get; }

    [JsonPropertyName("treeKind")]
    public string TreeKind { get; }

    [JsonPropertyName("depthLimit")]
    public int DepthLimit { get; }

    [JsonPropertyName("matches")]
    public IReadOnlyList<FindNodeMatch> Matches { get; }

    [JsonPropertyName("target")]
    public RuntimeTargetContext Target { get; }

    [JsonPropertyName("responseBudget")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ResponseBudgetInfo? ResponseBudget { get; }
}
