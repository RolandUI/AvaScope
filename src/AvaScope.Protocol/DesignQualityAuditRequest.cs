using System.Text.Json.Serialization;

namespace AvaScope.Protocol;

public sealed record DesignQualityAuditRequest
{
    public const int MaximumFindings = 200;
    public const int MaximumListItems = 128;
    public const int MaximumAuditKinds = 16;

    [JsonConstructor]
    public DesignQualityAuditRequest(
        SessionId sessionId,
        string topLevelId,
        string? requestId = null,
        string treeKind = TreeKinds.Visual,
        int maxDepth = 16,
        int maxFindings = 100,
        string? scopeNodeId = null,
        string? scopeName = null,
        string? scopeAutomationId = null,
        string? scopeSourcePath = null,
        ScreenshotRegion? scopeRegion = null,
        bool onlyChangedNodes = false,
        IReadOnlyList<string>? changedNodeIds = null,
        IReadOnlyList<string>? changedSourcePaths = null,
        IReadOnlyList<string>? excludeNodeIds = null,
        IReadOnlyList<string>? excludeNames = null,
        IReadOnlyList<string>? excludeAutomationIds = null,
        IReadOnlyList<string>? excludeTypes = null,
        IReadOnlyList<string>? excludeSourcePaths = null,
        IReadOnlyList<DesignQualitySuppression>? suppressions = null,
        IReadOnlyList<string>? auditKinds = null)
    {
        SessionId = sessionId ?? throw new ArgumentNullException(nameof(sessionId));

        if (string.IsNullOrWhiteSpace(topLevelId))
        {
            throw new ArgumentException("Top-level id cannot be empty.", nameof(topLevelId));
        }

        if (maxDepth < 0 || maxDepth > 64)
        {
            throw new ArgumentOutOfRangeException(nameof(maxDepth), maxDepth, "Max depth must be between 0 and 64.");
        }

        if (maxFindings < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(maxFindings), maxFindings, "Max findings must be positive.");
        }

        RequestId = string.IsNullOrWhiteSpace(requestId) ? Guid.NewGuid().ToString("n") : requestId.Trim();
        TopLevelId = topLevelId.Trim();
        TreeKind = string.IsNullOrWhiteSpace(treeKind) ? TreeKinds.Visual : treeKind.Trim();
        MaxDepth = maxDepth;
        MaxFindings = Math.Min(maxFindings, MaximumFindings);
        ScopeNodeId = Normalize(scopeNodeId);
        ScopeName = Normalize(scopeName);
        ScopeAutomationId = Normalize(scopeAutomationId);
        ScopeSourcePath = NormalizePath(scopeSourcePath);
        ScopeRegion = scopeRegion;
        OnlyChangedNodes = onlyChangedNodes;
        ChangedNodeIds = NormalizeList(changedNodeIds);
        ChangedSourcePaths = NormalizePathList(changedSourcePaths);
        ExcludeNodeIds = NormalizeList(excludeNodeIds);
        ExcludeNames = NormalizeList(excludeNames);
        ExcludeAutomationIds = NormalizeList(excludeAutomationIds);
        ExcludeTypes = NormalizeList(excludeTypes);
        ExcludeSourcePaths = NormalizePathList(excludeSourcePaths);
        Suppressions = (suppressions ?? []).Take(MaximumListItems).ToArray();
        AuditKinds = NormalizeList(auditKinds, MaximumAuditKinds);
    }

    [JsonPropertyName("requestId")]
    public string RequestId { get; }

    [JsonPropertyName("sessionId")]
    public SessionId SessionId { get; }

    [JsonPropertyName("topLevelId")]
    public string TopLevelId { get; }

    [JsonPropertyName("treeKind")]
    public string TreeKind { get; }

    [JsonPropertyName("maxDepth")]
    public int MaxDepth { get; }

    [JsonPropertyName("maxFindings")]
    public int MaxFindings { get; }

    [JsonPropertyName("scopeNodeId")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ScopeNodeId { get; }

    [JsonPropertyName("scopeName")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ScopeName { get; }

    [JsonPropertyName("scopeAutomationId")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ScopeAutomationId { get; }

    [JsonPropertyName("scopeSourcePath")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ScopeSourcePath { get; }

    [JsonPropertyName("scopeRegion")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ScreenshotRegion? ScopeRegion { get; }

    [JsonPropertyName("onlyChangedNodes")]
    public bool OnlyChangedNodes { get; }

    [JsonPropertyName("changedNodeIds")]
    public IReadOnlyList<string> ChangedNodeIds { get; }

    [JsonPropertyName("changedSourcePaths")]
    public IReadOnlyList<string> ChangedSourcePaths { get; }

    [JsonPropertyName("excludeNodeIds")]
    public IReadOnlyList<string> ExcludeNodeIds { get; }

    [JsonPropertyName("excludeNames")]
    public IReadOnlyList<string> ExcludeNames { get; }

    [JsonPropertyName("excludeAutomationIds")]
    public IReadOnlyList<string> ExcludeAutomationIds { get; }

    [JsonPropertyName("excludeTypes")]
    public IReadOnlyList<string> ExcludeTypes { get; }

    [JsonPropertyName("excludeSourcePaths")]
    public IReadOnlyList<string> ExcludeSourcePaths { get; }

    [JsonPropertyName("suppressions")]
    public IReadOnlyList<DesignQualitySuppression> Suppressions { get; }

    [JsonPropertyName("auditKinds")]
    public IReadOnlyList<string> AuditKinds { get; }

    public bool HasExplicitScope =>
        ScopeNodeId is not null
        || ScopeName is not null
        || ScopeAutomationId is not null
        || ScopeSourcePath is not null
        || ScopeRegion is not null;

    public bool HasChangeFilter => OnlyChangedNodes
        || ChangedNodeIds.Count > 0
        || ChangedSourcePaths.Count > 0;

    private static string? Normalize(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static string? NormalizePath(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : Path.GetFullPath(value);
    }

    private static IReadOnlyList<string> NormalizeList(IReadOnlyList<string>? values, int maximum = MaximumListItems)
    {
        return (values ?? [])
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .Select(static value => value.Trim())
            .Distinct(StringComparer.Ordinal)
            .Take(maximum)
            .ToArray();
    }

    private static IReadOnlyList<string> NormalizePathList(IReadOnlyList<string>? values)
    {
        return (values ?? [])
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .Select(static value => Path.GetFullPath(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(MaximumListItems)
            .ToArray();
    }
}

public sealed record DesignQualitySuppression
{
    [JsonConstructor]
    public DesignQualitySuppression(
        string? code = null,
        string? category = null,
        string? nodeId = null,
        string? nodeType = null,
        string? name = null,
        string? automationId = null,
        string? sourcePath = null,
        string? reason = null)
    {
        Code = Normalize(code);
        Category = Normalize(category);
        NodeId = Normalize(nodeId);
        NodeType = Normalize(nodeType);
        Name = Normalize(name);
        AutomationId = Normalize(automationId);
        SourcePath = string.IsNullOrWhiteSpace(sourcePath) ? null : Path.GetFullPath(sourcePath);
        Reason = string.IsNullOrWhiteSpace(reason) ? "suppressed_by_rule" : reason.Trim();
    }

    [JsonPropertyName("code")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Code { get; }

    [JsonPropertyName("category")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Category { get; }

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

    [JsonPropertyName("reason")]
    public string Reason { get; }

    private static string? Normalize(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
