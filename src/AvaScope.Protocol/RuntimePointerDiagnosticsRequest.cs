using System.Text.Json.Serialization;

namespace AvaScope.Protocol;

public static class RuntimePointerPathActions
{
    public const string Move = "move";
    public const string Wait = "wait";
    public const string Screenshot = "screenshot";
    public const string AssertHit = "assert_hit";
}

public sealed record RuntimePointerDiagnosticsRequest
{
    [JsonConstructor]
    public RuntimePointerDiagnosticsRequest(
        SessionId sessionId,
        string topLevelId,
        IReadOnlyList<RuntimePointerPathStep> steps,
        string? requestId = null,
        string? outputDirectory = null,
        int maxDepth = 16,
        bool includeAllTopLevels = true,
        bool captureScreenshots = false,
        string? parentHoverNodeId = null)
    {
        SessionId = sessionId ?? throw new ArgumentNullException(nameof(sessionId));

        if (string.IsNullOrWhiteSpace(topLevelId))
        {
            throw new ArgumentException("Top-level id cannot be empty.", nameof(topLevelId));
        }

        if (steps is null || steps.Count == 0)
        {
            throw new ArgumentException("Pointer diagnostics requires at least one step.", nameof(steps));
        }

        if (maxDepth < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxDepth), maxDepth, "Max depth cannot be negative.");
        }

        RequestId = string.IsNullOrWhiteSpace(requestId) ? Guid.NewGuid().ToString("n") : requestId.Trim();
        TopLevelId = topLevelId.Trim();
        Steps = steps;
        OutputDirectory = string.IsNullOrWhiteSpace(outputDirectory) ? null : Path.GetFullPath(outputDirectory);
        MaxDepth = maxDepth;
        IncludeAllTopLevels = includeAllTopLevels;
        CaptureScreenshots = captureScreenshots;
        ParentHoverNodeId = string.IsNullOrWhiteSpace(parentHoverNodeId) ? null : parentHoverNodeId.Trim();
    }

    [JsonPropertyName("requestId")]
    public string RequestId { get; }

    [JsonPropertyName("sessionId")]
    public SessionId SessionId { get; }

    [JsonPropertyName("topLevelId")]
    public string TopLevelId { get; }

    [JsonPropertyName("steps")]
    public IReadOnlyList<RuntimePointerPathStep> Steps { get; }

    [JsonPropertyName("outputDirectory")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? OutputDirectory { get; }

    [JsonPropertyName("maxDepth")]
    public int MaxDepth { get; }

    [JsonPropertyName("includeAllTopLevels")]
    public bool IncludeAllTopLevels { get; }

    [JsonPropertyName("captureScreenshots")]
    public bool CaptureScreenshots { get; }

    [JsonPropertyName("parentHoverNodeId")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ParentHoverNodeId { get; }
}

public sealed record RuntimePointerPathStep
{
    [JsonConstructor]
    public RuntimePointerPathStep(
        string action,
        string? id = null,
        double? x = null,
        double? y = null,
        int? waitMs = null,
        string? screenshotPath = null,
        string? expectedNodeId = null,
        string? expectedLayerKind = null)
    {
        if (string.IsNullOrWhiteSpace(action))
        {
            throw new ArgumentException("Pointer path step action cannot be empty.", nameof(action));
        }

        if (waitMs is < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(waitMs), waitMs, "Wait duration cannot be negative.");
        }

        Action = action.Trim();
        Id = string.IsNullOrWhiteSpace(id) ? Action : id.Trim();
        X = x;
        Y = y;
        WaitMs = waitMs;
        ScreenshotPath = string.IsNullOrWhiteSpace(screenshotPath) ? null : Path.GetFullPath(screenshotPath);
        ExpectedNodeId = string.IsNullOrWhiteSpace(expectedNodeId) ? null : expectedNodeId.Trim();
        ExpectedLayerKind = string.IsNullOrWhiteSpace(expectedLayerKind) ? null : expectedLayerKind.Trim();
    }

    [JsonPropertyName("id")]
    public string Id { get; }

    [JsonPropertyName("action")]
    public string Action { get; }

    [JsonPropertyName("x")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? X { get; }

    [JsonPropertyName("y")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? Y { get; }

    [JsonPropertyName("waitMs")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? WaitMs { get; }

    [JsonPropertyName("screenshotPath")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ScreenshotPath { get; }

    [JsonPropertyName("expectedNodeId")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ExpectedNodeId { get; }

    [JsonPropertyName("expectedLayerKind")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ExpectedLayerKind { get; }
}
