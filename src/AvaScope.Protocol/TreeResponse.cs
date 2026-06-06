using System.Text.Json.Serialization;

namespace AvaScope.Protocol;

public sealed record TreeResponse
{
    [JsonConstructor]
    public TreeResponse(
        SessionId sessionId,
        string topLevelId,
        string treeKind,
        int depthLimit,
        TreeNodeSummary root)
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
        Root = root ?? throw new ArgumentNullException(nameof(root));
    }

    [JsonPropertyName("sessionId")]
    public SessionId SessionId { get; }

    [JsonPropertyName("topLevelId")]
    public string TopLevelId { get; }

    [JsonPropertyName("treeKind")]
    public string TreeKind { get; }

    [JsonPropertyName("depthLimit")]
    public int DepthLimit { get; }

    [JsonPropertyName("root")]
    public TreeNodeSummary Root { get; }
}
