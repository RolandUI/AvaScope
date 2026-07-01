using System.Text.Json.Serialization;

namespace AvaScope.Protocol;

public static class RuntimeInteractionAnimationActions
{
    public const string Wait = "wait";
}

public static class RuntimeInteractionGeometryAssertionModes
{
    public const string Stable = "stable";
    public const string Equal = "equals";
    public const string WithinRange = "within_range";
    public const string FinalStable = "final_stable";
    public const string NotClipped = "not_clipped";
}

public static class RuntimeInteractionGeometryMetrics
{
    public const string X = "x";
    public const string Y = "y";
    public const string Left = "left";
    public const string Top = "top";
    public const string Right = "right";
    public const string Bottom = "bottom";
    public const string Width = "width";
    public const string Height = "height";
    public const string CenterX = "center_x";
    public const string CenterY = "center_y";
}

public sealed record RuntimeInteractionAnimationRequest
{
    public const int MaximumSteps = 32;
    public const int MaximumAssertions = 32;
    public const int MaximumFrameCount = 24;
    public const int MaximumFrameOffsetMs = 60000;

    public static IReadOnlyList<int> BuiltInFrameOffsetsMs { get; } = [0, 100, 250];

    [JsonConstructor]
    public RuntimeInteractionAnimationRequest(
        SessionId sessionId,
        string topLevelId,
        IReadOnlyList<RuntimeInteractionAnimationStep> steps,
        string? requestId = null,
        string? outputDirectory = null,
        string? frameStripPath = null,
        int maxDepth = 16,
        IReadOnlyList<int>? defaultFrameOffsetsMs = null,
        IReadOnlyList<RuntimeInteractionGeometryAssertion>? assertions = null)
    {
        SessionId = sessionId ?? throw new ArgumentNullException(nameof(sessionId));

        if (string.IsNullOrWhiteSpace(topLevelId))
        {
            throw new ArgumentException("Top-level id cannot be empty.", nameof(topLevelId));
        }

        if (steps is null || steps.Count == 0)
        {
            throw new ArgumentException("Interaction animation requires at least one step.", nameof(steps));
        }

        if (steps.Count > MaximumSteps)
        {
            throw new ArgumentOutOfRangeException(nameof(steps), steps.Count, $"Interaction animation supports at most {MaximumSteps} steps.");
        }

        if (maxDepth < 0 || maxDepth > 64)
        {
            throw new ArgumentOutOfRangeException(nameof(maxDepth), maxDepth, "Max depth must be between 0 and 64.");
        }

        if (assertions is not null && assertions.Count > MaximumAssertions)
        {
            throw new ArgumentOutOfRangeException(nameof(assertions), assertions.Count, $"Interaction animation supports at most {MaximumAssertions} geometry assertions.");
        }

        RequestId = string.IsNullOrWhiteSpace(requestId) ? Guid.NewGuid().ToString("n") : requestId.Trim();
        TopLevelId = topLevelId.Trim();
        Steps = steps;
        OutputDirectory = string.IsNullOrWhiteSpace(outputDirectory) ? null : Path.GetFullPath(outputDirectory);
        FrameStripPath = string.IsNullOrWhiteSpace(frameStripPath) ? null : Path.GetFullPath(frameStripPath);
        MaxDepth = maxDepth;
        DefaultFrameOffsetsMs = NormalizeOffsets(defaultFrameOffsetsMs, nameof(defaultFrameOffsetsMs));
        Assertions = assertions ?? [];
    }

    [JsonPropertyName("requestId")]
    public string RequestId { get; }

    [JsonPropertyName("sessionId")]
    public SessionId SessionId { get; }

    [JsonPropertyName("topLevelId")]
    public string TopLevelId { get; }

    [JsonPropertyName("steps")]
    public IReadOnlyList<RuntimeInteractionAnimationStep> Steps { get; }

    [JsonPropertyName("outputDirectory")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? OutputDirectory { get; }

    [JsonPropertyName("frameStripPath")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? FrameStripPath { get; }

    [JsonPropertyName("maxDepth")]
    public int MaxDepth { get; }

    [JsonPropertyName("defaultFrameOffsetsMs")]
    public IReadOnlyList<int> DefaultFrameOffsetsMs { get; }

    [JsonPropertyName("assertions")]
    public IReadOnlyList<RuntimeInteractionGeometryAssertion> Assertions { get; }

    internal static IReadOnlyList<int> NormalizeOffsets(IReadOnlyList<int>? offsets, string parameterName)
    {
        var normalized = (offsets is null || offsets.Count == 0 ? BuiltInFrameOffsetsMs : offsets)
            .Distinct()
            .Order()
            .ToArray();

        if (normalized.Length == 0)
        {
            throw new ArgumentException("At least one frame offset is required.", parameterName);
        }

        if (normalized.Length > MaximumFrameCount)
        {
            throw new ArgumentOutOfRangeException(parameterName, normalized.Length, $"At most {MaximumFrameCount} frame offsets are supported.");
        }

        if (normalized.Any(static offset => offset < 0 || offset > MaximumFrameOffsetMs))
        {
            throw new ArgumentOutOfRangeException(parameterName, $"Frame offsets must be between 0 and {MaximumFrameOffsetMs} milliseconds.");
        }

        return normalized;
    }
}

public sealed record RuntimeInteractionAnimationStep
{
    [JsonConstructor]
    public RuntimeInteractionAnimationStep(
        string action,
        string? id = null,
        double? x = null,
        double? y = null,
        string? text = null,
        string? targetNodeId = null,
        string? inputKey = null,
        string? keyModifiers = null,
        int? waitMs = null,
        bool captureFrames = true,
        IReadOnlyList<int>? frameOffsetsMs = null)
    {
        if (string.IsNullOrWhiteSpace(action))
        {
            throw new ArgumentException("Interaction step action cannot be empty.", nameof(action));
        }

        if (waitMs is < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(waitMs), waitMs, "Wait duration cannot be negative.");
        }

        Action = NormalizeAction(action);
        Id = string.IsNullOrWhiteSpace(id) ? Action : id.Trim();
        X = x;
        Y = y;
        Text = string.IsNullOrWhiteSpace(text) ? null : text;
        TargetNodeId = string.IsNullOrWhiteSpace(targetNodeId) ? null : targetNodeId.Trim();
        InputKey = string.IsNullOrWhiteSpace(inputKey) ? null : inputKey.Trim();
        KeyModifiers = string.IsNullOrWhiteSpace(keyModifiers) ? null : keyModifiers.Trim();
        WaitMs = waitMs;
        CaptureFrames = captureFrames;
        FrameOffsetsMs = frameOffsetsMs is null || frameOffsetsMs.Count == 0
            ? null
            : RuntimeInteractionAnimationRequest.NormalizeOffsets(frameOffsetsMs, nameof(frameOffsetsMs));
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

    [JsonPropertyName("text")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Text { get; }

    [JsonPropertyName("targetNodeId")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? TargetNodeId { get; }

    [JsonPropertyName("inputKey")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? InputKey { get; }

    [JsonPropertyName("keyModifiers")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? KeyModifiers { get; }

    [JsonPropertyName("waitMs")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? WaitMs { get; }

    [JsonPropertyName("captureFrames")]
    public bool CaptureFrames { get; }

    [JsonPropertyName("frameOffsetsMs")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<int>? FrameOffsetsMs { get; }

    private static string NormalizeAction(string action)
    {
        var normalized = action.Trim().Replace("-", "_", StringComparison.Ordinal).ToLowerInvariant();
        return normalized switch
        {
            "move" => InputActions.PointerMove,
            "pointermove" => InputActions.PointerMove,
            "pointer_down" => InputActions.PointerDown,
            "pointerdown" => InputActions.PointerDown,
            "pointer_up" => InputActions.PointerUp,
            "pointerup" => InputActions.PointerUp,
            "type" => InputActions.KeyText,
            "keytext" => InputActions.KeyText,
            "clear" => InputActions.ClearText,
            "cleartext" => InputActions.ClearText,
            "keydown" => InputActions.KeyDown,
            "keyup" => InputActions.KeyUp,
            RuntimeInteractionAnimationActions.Wait => RuntimeInteractionAnimationActions.Wait,
            _ => normalized
        };
    }
}

public sealed record RuntimeInteractionGeometryAssertion
{
    [JsonConstructor]
    public RuntimeInteractionGeometryAssertion(
        string targetNodeId,
        string metric,
        string mode = RuntimeInteractionGeometryAssertionModes.Stable,
        string? assertionId = null,
        string? stepId = null,
        double? expectedValue = null,
        double? minValue = null,
        double? maxValue = null,
        double tolerance = 1)
    {
        if (string.IsNullOrWhiteSpace(targetNodeId))
        {
            throw new ArgumentException("Geometry assertion target node id cannot be empty.", nameof(targetNodeId));
        }

        if (string.IsNullOrWhiteSpace(metric))
        {
            throw new ArgumentException("Geometry assertion metric cannot be empty.", nameof(metric));
        }

        if (string.IsNullOrWhiteSpace(mode))
        {
            throw new ArgumentException("Geometry assertion mode cannot be empty.", nameof(mode));
        }

        if (tolerance < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(tolerance), tolerance, "Geometry assertion tolerance cannot be negative.");
        }

        TargetNodeId = targetNodeId.Trim();
        Metric = metric.Trim().Replace("-", "_", StringComparison.Ordinal).ToLowerInvariant();
        Mode = mode.Trim().Replace("-", "_", StringComparison.Ordinal).ToLowerInvariant();
        AssertionId = string.IsNullOrWhiteSpace(assertionId) ? $"{TargetNodeId}:{Metric}:{Mode}" : assertionId.Trim();
        StepId = string.IsNullOrWhiteSpace(stepId) ? null : stepId.Trim();
        ExpectedValue = expectedValue;
        MinValue = minValue;
        MaxValue = maxValue;
        Tolerance = tolerance;
    }

    [JsonPropertyName("assertionId")]
    public string AssertionId { get; }

    [JsonPropertyName("targetNodeId")]
    public string TargetNodeId { get; }

    [JsonPropertyName("metric")]
    public string Metric { get; }

    [JsonPropertyName("mode")]
    public string Mode { get; }

    [JsonPropertyName("stepId")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? StepId { get; }

    [JsonPropertyName("expectedValue")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? ExpectedValue { get; }

    [JsonPropertyName("minValue")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? MinValue { get; }

    [JsonPropertyName("maxValue")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? MaxValue { get; }

    [JsonPropertyName("tolerance")]
    public double Tolerance { get; }
}
