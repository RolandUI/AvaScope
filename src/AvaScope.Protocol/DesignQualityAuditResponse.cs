using System.Globalization;
using System.Text.Json.Serialization;

namespace AvaScope.Protocol;

public sealed record DesignQualityAuditResponse
{
    public const int MaximumFindings = 200;
    public const int MaximumDiagnostics = 48;

    [JsonConstructor]
    public DesignQualityAuditResponse(
        string requestId,
        SessionId sessionId,
        string topLevelId,
        string treeKind,
        DateTimeOffset auditedAt,
        DesignQualityAuditSummary summary,
        RuntimeTargetContext scopeTarget,
        IReadOnlyList<DesignQualityFinding>? findings = null,
        IReadOnlyList<DesignQualityFinding>? ignoredFindings = null,
        IReadOnlyList<ProtocolError>? diagnostics = null,
        IReadOnlyDictionary<string, string>? metadata = null)
    {
        if (string.IsNullOrWhiteSpace(requestId))
        {
            throw new ArgumentException("Design-quality audit request id cannot be empty.", nameof(requestId));
        }

        if (string.IsNullOrWhiteSpace(topLevelId))
        {
            throw new ArgumentException("Top-level id cannot be empty.", nameof(topLevelId));
        }

        if (string.IsNullOrWhiteSpace(treeKind))
        {
            throw new ArgumentException("Tree kind cannot be empty.", nameof(treeKind));
        }

        RequestId = requestId.Trim();
        SessionId = sessionId ?? throw new ArgumentNullException(nameof(sessionId));
        TopLevelId = topLevelId.Trim();
        TreeKind = treeKind.Trim();
        AuditedAt = auditedAt;
        Summary = summary ?? throw new ArgumentNullException(nameof(summary));
        ScopeTarget = scopeTarget ?? throw new ArgumentNullException(nameof(scopeTarget));
        Findings = (findings ?? []).Take(MaximumFindings).ToArray();
        IgnoredFindings = (ignoredFindings ?? []).Take(MaximumFindings).ToArray();
        Diagnostics = (diagnostics ?? []).Take(MaximumDiagnostics).ToArray();
        Metadata = metadata ?? new Dictionary<string, string>();
    }

    [JsonPropertyName("requestId")]
    public string RequestId { get; }

    [JsonPropertyName("sessionId")]
    public SessionId SessionId { get; }

    [JsonPropertyName("topLevelId")]
    public string TopLevelId { get; }

    [JsonPropertyName("treeKind")]
    public string TreeKind { get; }

    [JsonPropertyName("auditedAt")]
    public DateTimeOffset AuditedAt { get; }

    [JsonPropertyName("summary")]
    public DesignQualityAuditSummary Summary { get; }

    [JsonPropertyName("scopeTarget")]
    public RuntimeTargetContext ScopeTarget { get; }

    [JsonPropertyName("findings")]
    public IReadOnlyList<DesignQualityFinding> Findings { get; }

    [JsonPropertyName("ignoredFindings")]
    public IReadOnlyList<DesignQualityFinding> IgnoredFindings { get; }

    [JsonPropertyName("diagnostics")]
    public IReadOnlyList<ProtocolError> Diagnostics { get; }

    [JsonPropertyName("metadata")]
    public IReadOnlyDictionary<string, string> Metadata { get; }

    [JsonPropertyName("agentReview")]
    public AgentReviewSurface AgentReview => CreateAgentReview();

    private AgentReviewSurface CreateAgentReview()
    {
        var failures = Findings
            .Take(AgentReviewSurface.MaximumFailureSummaries)
            .Select(static finding => new AgentReviewFailure(
                finding.Category,
                finding.Message,
                finding.Code))
            .ToArray();

        return new AgentReviewSurface(
            Summary.Status,
            Findings.Count == 0
                ? $"Design-quality audit '{RequestId}' found no active findings."
                : $"Design-quality audit '{RequestId}' found {Findings.Count.ToString(CultureInfo.InvariantCulture)} active findings.",
            [
                $"scopedNodes: {Summary.ScopedNodes.ToString(CultureInfo.InvariantCulture)}",
                $"evaluatedNodes: {Summary.EvaluatedNodes.ToString(CultureInfo.InvariantCulture)}",
                $"findings: {Summary.FindingCount.ToString(CultureInfo.InvariantCulture)}",
                $"ignored: {Summary.IgnoredFindingCount.ToString(CultureInfo.InvariantCulture)}",
                $"excludedNodes: {Summary.ExcludedNodeCount.ToString(CultureInfo.InvariantCulture)}",
                $"scope: {Summary.ScopeStatus}"
            ],
            failures,
            truncated: Summary.Truncated || Findings.Count > AgentReviewSurface.MaximumFailureSummaries);
    }
}

public sealed record DesignQualityAuditSummary
{
    [JsonConstructor]
    public DesignQualityAuditSummary(
        int totalNodes,
        int scopedNodes,
        int evaluatedNodes,
        int outOfScopeNodes,
        int excludedNodeCount,
        int findingCount,
        int ignoredFindingCount,
        int suppressionRuleCount,
        string status,
        string scopeStatus,
        bool truncated = false,
        IReadOnlyDictionary<string, int>? categoryCounts = null)
    {
        if (totalNodes < 0
            || scopedNodes < 0
            || evaluatedNodes < 0
            || outOfScopeNodes < 0
            || excludedNodeCount < 0
            || findingCount < 0
            || ignoredFindingCount < 0
            || suppressionRuleCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(totalNodes), "Design-quality audit counts cannot be negative.");
        }

        if (string.IsNullOrWhiteSpace(status))
        {
            throw new ArgumentException("Audit status cannot be empty.", nameof(status));
        }

        if (string.IsNullOrWhiteSpace(scopeStatus))
        {
            throw new ArgumentException("Audit scope status cannot be empty.", nameof(scopeStatus));
        }

        TotalNodes = totalNodes;
        ScopedNodes = scopedNodes;
        EvaluatedNodes = evaluatedNodes;
        OutOfScopeNodes = outOfScopeNodes;
        ExcludedNodeCount = excludedNodeCount;
        FindingCount = findingCount;
        IgnoredFindingCount = ignoredFindingCount;
        SuppressionRuleCount = suppressionRuleCount;
        Status = status.Trim();
        ScopeStatus = scopeStatus.Trim();
        Truncated = truncated;
        CategoryCounts = categoryCounts ?? new Dictionary<string, int>();
    }

    [JsonPropertyName("totalNodes")]
    public int TotalNodes { get; }

    [JsonPropertyName("scopedNodes")]
    public int ScopedNodes { get; }

    [JsonPropertyName("evaluatedNodes")]
    public int EvaluatedNodes { get; }

    [JsonPropertyName("outOfScopeNodes")]
    public int OutOfScopeNodes { get; }

    [JsonPropertyName("excludedNodeCount")]
    public int ExcludedNodeCount { get; }

    [JsonPropertyName("findingCount")]
    public int FindingCount { get; }

    [JsonPropertyName("ignoredFindingCount")]
    public int IgnoredFindingCount { get; }

    [JsonPropertyName("suppressionRuleCount")]
    public int SuppressionRuleCount { get; }

    [JsonPropertyName("status")]
    public string Status { get; }

    [JsonPropertyName("scopeStatus")]
    public string ScopeStatus { get; }

    [JsonPropertyName("truncated")]
    public bool Truncated { get; }

    [JsonPropertyName("categoryCounts")]
    public IReadOnlyDictionary<string, int> CategoryCounts { get; }
}

public sealed record DesignQualityFinding
{
    public const int MaximumDetails = 24;
    public const int MaximumRelatedNodes = 16;

    [JsonConstructor]
    public DesignQualityFinding(
        string findingId,
        string category,
        string severity,
        string code,
        string message,
        string provenance,
        RuntimeTargetContext target,
        string suggestedAction,
        string? nodeId = null,
        string? nodeType = null,
        string? name = null,
        string? automationId = null,
        string? sourcePath = null,
        NodeBounds? bounds = null,
        IReadOnlyList<string>? relatedNodeIds = null,
        IReadOnlyDictionary<string, string>? details = null,
        bool ignored = false,
        string? ignoredReason = null)
    {
        if (string.IsNullOrWhiteSpace(findingId))
        {
            throw new ArgumentException("Design-quality finding id cannot be empty.", nameof(findingId));
        }

        if (string.IsNullOrWhiteSpace(category))
        {
            throw new ArgumentException("Design-quality finding category cannot be empty.", nameof(category));
        }

        if (string.IsNullOrWhiteSpace(severity))
        {
            throw new ArgumentException("Design-quality finding severity cannot be empty.", nameof(severity));
        }

        if (string.IsNullOrWhiteSpace(code))
        {
            throw new ArgumentException("Design-quality finding code cannot be empty.", nameof(code));
        }

        if (string.IsNullOrWhiteSpace(message))
        {
            throw new ArgumentException("Design-quality finding message cannot be empty.", nameof(message));
        }

        if (string.IsNullOrWhiteSpace(provenance))
        {
            throw new ArgumentException("Design-quality finding provenance cannot be empty.", nameof(provenance));
        }

        if (string.IsNullOrWhiteSpace(suggestedAction))
        {
            throw new ArgumentException("Design-quality finding suggested action cannot be empty.", nameof(suggestedAction));
        }

        FindingId = findingId.Trim();
        Category = category.Trim();
        Severity = severity.Trim();
        Code = code.Trim();
        Message = message.Trim();
        Provenance = provenance.Trim();
        Target = target ?? throw new ArgumentNullException(nameof(target));
        SuggestedAction = suggestedAction.Trim();
        NodeId = Normalize(nodeId);
        NodeType = Normalize(nodeType);
        Name = Normalize(name);
        AutomationId = Normalize(automationId);
        SourcePath = string.IsNullOrWhiteSpace(sourcePath) ? null : Path.GetFullPath(sourcePath);
        Bounds = bounds;
        RelatedNodeIds = (relatedNodeIds ?? [])
            .Where(static nodeId => !string.IsNullOrWhiteSpace(nodeId))
            .Select(static nodeId => nodeId.Trim())
            .Distinct(StringComparer.Ordinal)
            .Take(MaximumRelatedNodes)
            .ToArray();
        Details = details is null
            ? new Dictionary<string, string>()
            : details
                .Where(static item => !string.IsNullOrWhiteSpace(item.Key))
                .Take(MaximumDetails)
                .ToDictionary(static item => item.Key, static item => item.Value, StringComparer.Ordinal);
        Ignored = ignored;
        IgnoredReason = Normalize(ignoredReason);
    }

    [JsonPropertyName("findingId")]
    public string FindingId { get; }

    [JsonPropertyName("category")]
    public string Category { get; }

    [JsonPropertyName("severity")]
    public string Severity { get; }

    [JsonPropertyName("code")]
    public string Code { get; }

    [JsonPropertyName("message")]
    public string Message { get; }

    [JsonPropertyName("provenance")]
    public string Provenance { get; }

    [JsonPropertyName("target")]
    public RuntimeTargetContext Target { get; }

    [JsonPropertyName("suggestedAction")]
    public string SuggestedAction { get; }

    [JsonPropertyName("nodeId")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? NodeId { get; }

    [JsonPropertyName("nodeType")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? NodeType { get; }

    [JsonPropertyName("name")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Name { get; }

    [JsonPropertyName("automationId")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? AutomationId { get; }

    [JsonPropertyName("sourcePath")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? SourcePath { get; }

    [JsonPropertyName("bounds")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NodeBounds? Bounds { get; }

    [JsonPropertyName("relatedNodeIds")]
    public IReadOnlyList<string> RelatedNodeIds { get; }

    [JsonPropertyName("details")]
    public IReadOnlyDictionary<string, string> Details { get; }

    [JsonPropertyName("ignored")]
    public bool Ignored { get; }

    [JsonPropertyName("ignoredReason")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? IgnoredReason { get; }

    public DesignQualityFinding MarkIgnored(string reason)
    {
        return new DesignQualityFinding(
            FindingId,
            Category,
            Severity,
            Code,
            Message,
            Provenance,
            Target,
            SuggestedAction,
            NodeId,
            NodeType,
            Name,
            AutomationId,
            SourcePath,
            Bounds,
            RelatedNodeIds,
            Details,
            ignored: true,
            string.IsNullOrWhiteSpace(reason) ? "ignored" : reason.Trim());
    }

    private static string? Normalize(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
