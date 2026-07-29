using System.Text.Json.Serialization;

namespace AvaScope.Protocol;

public sealed record SemanticWorkflowStep
{
    [JsonConstructor]
    public SemanticWorkflowStep(
        string action,
        string? id = null,
        SemanticWorkflowSelector? selector = null,
        string? text = null,
        string? key = null,
        string? modifiers = null,
        string? assertProperty = null,
        string? expected = null,
        string? screenshotPath = null,
        int? waitMs = null,
        int? timeoutMs = null,
        int? pollIntervalMs = null,
        string? inputAction = null,
        RuntimeMutationOperation? mutation = null,
        string? idempotencyKey = null,
        int? idempotencyTtlMs = null)
    {
        if (string.IsNullOrWhiteSpace(action))
        {
            throw new ArgumentException("Workflow step action cannot be empty.", nameof(action));
        }

        if (waitMs is < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(waitMs), waitMs, "Wait duration cannot be negative.");
        }

        if (timeoutMs is < 1 or > 60000)
        {
            throw new ArgumentOutOfRangeException(nameof(timeoutMs), timeoutMs, "Timeout must be between 1 and 60000 ms.");
        }

        if (pollIntervalMs is < 25 or > 5000)
        {
            throw new ArgumentOutOfRangeException(nameof(pollIntervalMs), pollIntervalMs, "Poll interval must be between 25 and 5000 ms.");
        }

        if (idempotencyTtlMs is < 100 or > 86400000)
        {
            throw new ArgumentOutOfRangeException(nameof(idempotencyTtlMs), idempotencyTtlMs, "Idempotency TTL must be between 100 and 86400000 ms.");
        }

        Action = action.Trim();
        Id = string.IsNullOrWhiteSpace(id) ? Action : id.Trim();
        Selector = selector;
        Text = string.IsNullOrWhiteSpace(text) ? null : text;
        Key = string.IsNullOrWhiteSpace(key) ? null : key;
        Modifiers = string.IsNullOrWhiteSpace(modifiers) ? null : modifiers;
        AssertProperty = string.IsNullOrWhiteSpace(assertProperty) ? null : assertProperty;
        Expected = expected;
        ScreenshotPath = string.IsNullOrWhiteSpace(screenshotPath) ? null : screenshotPath;
        WaitMs = waitMs;
        TimeoutMs = timeoutMs;
        PollIntervalMs = pollIntervalMs;
        InputAction = string.IsNullOrWhiteSpace(inputAction) ? null : inputAction.Trim();
        Mutation = mutation;
        IdempotencyKey = string.IsNullOrWhiteSpace(idempotencyKey) ? null : idempotencyKey.Trim();
        IdempotencyTtlMs = idempotencyTtlMs;
    }

    [JsonPropertyName("id")]
    public string Id { get; }

    [JsonPropertyName("action")]
    public string Action { get; }

    [JsonPropertyName("selector")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public SemanticWorkflowSelector? Selector { get; }

    [JsonPropertyName("text")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Text { get; }

    [JsonPropertyName("key")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Key { get; }

    [JsonPropertyName("modifiers")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Modifiers { get; }

    [JsonPropertyName("assertProperty")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? AssertProperty { get; }

    [JsonPropertyName("expected")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Expected { get; }

    [JsonPropertyName("screenshotPath")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ScreenshotPath { get; }

    [JsonPropertyName("waitMs")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? WaitMs { get; }

    [JsonPropertyName("timeoutMs")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? TimeoutMs { get; }

    [JsonPropertyName("pollIntervalMs")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? PollIntervalMs { get; }

    [JsonPropertyName("inputAction")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? InputAction { get; }

    [JsonPropertyName("mutation")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public RuntimeMutationOperation? Mutation { get; }

    [JsonPropertyName("idempotencyKey")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? IdempotencyKey { get; }

    [JsonPropertyName("idempotencyTtlMs")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? IdempotencyTtlMs { get; }
}
