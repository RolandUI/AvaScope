using System.Text.Json.Serialization;

namespace AvaScope.Protocol;

public sealed record SemanticWorkflowVerification
{
    [JsonConstructor]
    public SemanticWorkflowVerification(
        SemanticWaitCondition condition,
        SemanticWorkflowSelector? selector = null,
        string? topLevelAlias = null,
        int timeoutMs = 5000,
        int pollIntervalMs = 100,
        bool captureBefore = true,
        bool captureAfter = true,
        bool captureScreenshots = false)
    {
        Condition = condition ?? throw new ArgumentNullException(nameof(condition));
        if (timeoutMs is < 1 or > 60000)
        {
            throw new ArgumentOutOfRangeException(nameof(timeoutMs), timeoutMs, "Verification timeout must be between 1 and 60000 ms.");
        }

        if (pollIntervalMs is < 25 or > 5000)
        {
            throw new ArgumentOutOfRangeException(nameof(pollIntervalMs), pollIntervalMs, "Verification poll interval must be between 25 and 5000 ms.");
        }

        Selector = selector;
        TopLevelAlias = string.IsNullOrWhiteSpace(topLevelAlias) ? null : topLevelAlias.Trim();
        TimeoutMs = timeoutMs;
        PollIntervalMs = pollIntervalMs;
        CaptureBefore = captureBefore;
        CaptureAfter = captureAfter;
        CaptureScreenshots = captureScreenshots;
    }

    [JsonPropertyName("condition")]
    public SemanticWaitCondition Condition { get; }

    [JsonPropertyName("selector")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public SemanticWorkflowSelector? Selector { get; }

    [JsonPropertyName("topLevelAlias")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? TopLevelAlias { get; }

    [JsonPropertyName("timeoutMs")]
    public int TimeoutMs { get; }

    [JsonPropertyName("pollIntervalMs")]
    public int PollIntervalMs { get; }

    [JsonPropertyName("captureBefore")]
    public bool CaptureBefore { get; }

    [JsonPropertyName("captureAfter")]
    public bool CaptureAfter { get; }

    [JsonPropertyName("captureScreenshots")]
    public bool CaptureScreenshots { get; }
}
