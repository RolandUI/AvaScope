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
        int? waitMs = null)
    {
        if (string.IsNullOrWhiteSpace(action))
        {
            throw new ArgumentException("Workflow step action cannot be empty.", nameof(action));
        }

        if (waitMs is < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(waitMs), waitMs, "Wait duration cannot be negative.");
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
}
