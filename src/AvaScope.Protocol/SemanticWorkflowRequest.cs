using System.Text.Json.Serialization;

namespace AvaScope.Protocol;

public sealed record SemanticWorkflowRequest
{
    [JsonConstructor]
    public SemanticWorkflowRequest(
        SessionId sessionId,
        string? topLevelId,
        IReadOnlyList<SemanticWorkflowStep> steps,
        string? requestId = null,
        string? outputDirectory = null,
        bool captureAfterEachStep = false,
        bool allowDestructive = false,
        string? isolatedStateDirectory = null,
        int maxDepth = 16,
        IReadOnlyList<SemanticWorkflowTopLevelAlias>? topLevelAliases = null,
        IReadOnlyDictionary<string, string>? variables = null,
        IReadOnlyList<SemanticWorkflowFragment>? fragments = null,
        bool validateOnly = false,
        int timeoutMs = SemanticWorkflowLimits.DefaultWorkflowTimeoutMs,
        SemanticWorkflowEvidenceOptions? evidence = null)
    {
        SessionId = sessionId ?? throw new ArgumentNullException(nameof(sessionId));

        if (string.IsNullOrWhiteSpace(topLevelId) && (topLevelAliases is null || topLevelAliases.Count == 0))
        {
            throw new ArgumentException("Workflow requires topLevelId or at least one top-level alias.", nameof(topLevelId));
        }

        if (steps is null || steps.Count == 0)
        {
            throw new ArgumentException("Workflow requires at least one step.", nameof(steps));
        }

        if (maxDepth < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxDepth), maxDepth, "Max depth cannot be negative.");
        }

        RequestId = string.IsNullOrWhiteSpace(requestId) ? Guid.NewGuid().ToString("n") : requestId.Trim();
        var aliases = topLevelAliases ?? Array.Empty<SemanticWorkflowTopLevelAlias>();
        var duplicateAlias = aliases
            .GroupBy(static alias => alias.Alias, StringComparer.Ordinal)
            .FirstOrDefault(static group => group.Count() > 1)?.Key;
        if (duplicateAlias is not null)
        {
            throw new ArgumentException($"Top-level alias '{duplicateAlias}' is declared more than once.", nameof(topLevelAliases));
        }

        if (timeoutMs is < 1 or > SemanticWorkflowLimits.MaximumWorkflowTimeoutMs)
        {
            throw new ArgumentOutOfRangeException(nameof(timeoutMs), timeoutMs, $"Workflow timeout must be between 1 and {SemanticWorkflowLimits.MaximumWorkflowTimeoutMs} ms.");
        }

        TopLevelId = string.IsNullOrWhiteSpace(topLevelId) ? null : topLevelId.Trim();
        Steps = steps;
        TopLevelAliases = aliases.ToArray();
        OutputDirectory = string.IsNullOrWhiteSpace(outputDirectory) ? null : Path.GetFullPath(outputDirectory);
        CaptureAfterEachStep = captureAfterEachStep;
        AllowDestructive = allowDestructive;
        IsolatedStateDirectory = string.IsNullOrWhiteSpace(isolatedStateDirectory) ? null : Path.GetFullPath(isolatedStateDirectory);
        MaxDepth = maxDepth;
        Variables = new Dictionary<string, string>(
            variables ?? new Dictionary<string, string>(),
            StringComparer.Ordinal);
        Fragments = fragments ?? Array.Empty<SemanticWorkflowFragment>();
        ValidateOnly = validateOnly;
        TimeoutMs = timeoutMs;
        Evidence = evidence;
    }

    [JsonPropertyName("requestId")]
    public string RequestId { get; }

    [JsonPropertyName("sessionId")]
    public SessionId SessionId { get; }

    [JsonPropertyName("topLevelId")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? TopLevelId { get; }

    [JsonPropertyName("steps")]
    public IReadOnlyList<SemanticWorkflowStep> Steps { get; }

    [JsonPropertyName("topLevelAliases")]
    public IReadOnlyList<SemanticWorkflowTopLevelAlias> TopLevelAliases { get; }

    [JsonPropertyName("outputDirectory")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? OutputDirectory { get; }

    [JsonPropertyName("captureAfterEachStep")]
    public bool CaptureAfterEachStep { get; }

    [JsonPropertyName("allowDestructive")]
    public bool AllowDestructive { get; }

    [JsonPropertyName("isolatedStateDirectory")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? IsolatedStateDirectory { get; }

    [JsonPropertyName("maxDepth")]
    public int MaxDepth { get; }

    [JsonPropertyName("variables")]
    public IReadOnlyDictionary<string, string> Variables { get; }

    [JsonPropertyName("fragments")]
    public IReadOnlyList<SemanticWorkflowFragment> Fragments { get; }

    [JsonPropertyName("validateOnly")]
    public bool ValidateOnly { get; }

    [JsonPropertyName("timeoutMs")]
    public int TimeoutMs { get; }

    [JsonPropertyName("evidence")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public SemanticWorkflowEvidenceOptions? Evidence { get; }
}
