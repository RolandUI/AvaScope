using System.Globalization;
using System.Text.Json.Serialization;

namespace AvaScope.Protocol;

public sealed record UiAuditResponse
{
    public const int MaximumIssues = 100;
    public const int MaximumInventoryItems = 100;

    [JsonConstructor]
    public UiAuditResponse(
        SessionId sessionId,
        string topLevelId,
        string treeKind,
        int depthLimit,
        DateTimeOffset auditedAt,
        UiAuditSummary summary,
        IReadOnlyList<UiAuditIssue>? issues = null,
        IReadOnlyList<UiInventoryItem>? inventory = null,
        RuntimeTargetContext? target = null,
        ArtifactRunIndexResponse? runIndex = null)
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

        TopLevelId = topLevelId.Trim();
        TreeKind = treeKind.Trim();
        DepthLimit = depthLimit;
        AuditedAt = auditedAt;
        Summary = summary ?? throw new ArgumentNullException(nameof(summary));
        Issues = (issues ?? []).Take(MaximumIssues).ToArray();
        Inventory = (inventory ?? []).Take(MaximumInventoryItems).ToArray();
        Target = target ?? new RuntimeTargetContext(sessionId, topLevelId, treeKind);
        RunIndex = runIndex;
    }

    [JsonPropertyName("sessionId")]
    public SessionId SessionId { get; }

    [JsonPropertyName("topLevelId")]
    public string TopLevelId { get; }

    [JsonPropertyName("treeKind")]
    public string TreeKind { get; }

    [JsonPropertyName("depthLimit")]
    public int DepthLimit { get; }

    [JsonPropertyName("auditedAt")]
    public DateTimeOffset AuditedAt { get; }

    [JsonPropertyName("summary")]
    public UiAuditSummary Summary { get; }

    [JsonPropertyName("issues")]
    public IReadOnlyList<UiAuditIssue> Issues { get; }

    [JsonPropertyName("inventory")]
    public IReadOnlyList<UiInventoryItem> Inventory { get; }

    [JsonPropertyName("target")]
    public RuntimeTargetContext Target { get; }

    [JsonPropertyName("runIndex")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ArtifactRunIndexResponse? RunIndex { get; }

    [JsonPropertyName("agentReview")]
    public AgentReviewSurface AgentReview => CreateAgentReview();

    private AgentReviewSurface CreateAgentReview()
    {
        var status = Issues.Count == 0 ? "clean" : "issues_found";
        var headline = Issues.Count == 0
            ? "No UI audit issues found."
            : $"{Issues.Count.ToString(CultureInfo.InvariantCulture)} UI audit issues found.";
        var failures = Issues
            .Take(AgentReviewSurface.MaximumFailureSummaries)
            .Select(static issue => new AgentReviewFailure(
                issue.Category,
                issue.Message,
                issue.Code))
            .ToArray();

        return new AgentReviewSurface(
            status,
            headline,
            [
                $"nodes: {Summary.TotalNodes.ToString(CultureInfo.InvariantCulture)}",
                $"actionable: {Summary.ActionableNodes.ToString(CultureInfo.InvariantCulture)}",
                $"issues: {Summary.IssueCount.ToString(CultureInfo.InvariantCulture)}",
                $"inventory: {Summary.InventoryItemCount.ToString(CultureInfo.InvariantCulture)}",
                $"accessibility: {Summary.AccessibilityStatus}",
                $"validation: {Summary.ValidationStatus}",
                $"focusOrder: {Summary.FocusOrderStatus}"
            ],
            failures,
            truncated: Summary.Truncated || Issues.Count > AgentReviewSurface.MaximumFailureSummaries);
    }
}
