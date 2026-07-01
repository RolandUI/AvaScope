using System.Text.Json.Serialization;

namespace AvaScope.Protocol;

public sealed record SemanticWorkflowRequest
{
    [JsonConstructor]
    public SemanticWorkflowRequest(
        SessionId sessionId,
        string topLevelId,
        IReadOnlyList<SemanticWorkflowStep> steps,
        string? requestId = null,
        string? outputDirectory = null,
        bool captureAfterEachStep = false,
        bool allowDestructive = false,
        string? isolatedStateDirectory = null,
        int maxDepth = 16)
    {
        SessionId = sessionId ?? throw new ArgumentNullException(nameof(sessionId));

        if (string.IsNullOrWhiteSpace(topLevelId))
        {
            throw new ArgumentException("Top-level id cannot be empty.", nameof(topLevelId));
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
        TopLevelId = topLevelId;
        Steps = steps;
        OutputDirectory = string.IsNullOrWhiteSpace(outputDirectory) ? null : Path.GetFullPath(outputDirectory);
        CaptureAfterEachStep = captureAfterEachStep;
        AllowDestructive = allowDestructive;
        IsolatedStateDirectory = string.IsNullOrWhiteSpace(isolatedStateDirectory) ? null : Path.GetFullPath(isolatedStateDirectory);
        MaxDepth = maxDepth;
    }

    [JsonPropertyName("requestId")]
    public string RequestId { get; }

    [JsonPropertyName("sessionId")]
    public SessionId SessionId { get; }

    [JsonPropertyName("topLevelId")]
    public string TopLevelId { get; }

    [JsonPropertyName("steps")]
    public IReadOnlyList<SemanticWorkflowStep> Steps { get; }

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
}
