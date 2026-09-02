using System.Text.Json.Serialization;

namespace AvaScope.Protocol;

public sealed record SemanticWaitCondition
{
    [JsonConstructor]
    public SemanticWaitCondition(
        string kind,
        string? expected = null,
        string? comparison = null,
        string? valueType = null,
        string? propertyName = null,
        string? bindingPath = null,
        string? baseline = null,
        string? topLevelId = null,
        string? topLevelTitle = null)
    {
        if (!SemanticWaitConditionKinds.All.Contains(kind, StringComparer.Ordinal))
        {
            throw new ArgumentException($"Wait condition kind '{kind}' is not supported.", nameof(kind));
        }

        var effectiveComparison = string.IsNullOrWhiteSpace(comparison)
            ? kind == SemanticWaitConditionKinds.ChangeFromBaseline
                ? SemanticWaitComparisons.Changed
                : SemanticWaitComparisons.Equal
            : comparison.Trim();
        if (!SemanticWaitComparisons.All.Contains(effectiveComparison, StringComparer.Ordinal))
        {
            throw new ArgumentException($"Wait comparison '{effectiveComparison}' is not supported.", nameof(comparison));
        }

        Kind = kind;
        Expected = expected;
        Comparison = effectiveComparison;
        ValueType = string.IsNullOrWhiteSpace(valueType) ? "auto" : valueType.Trim();
        PropertyName = string.IsNullOrWhiteSpace(propertyName) ? null : propertyName.Trim();
        BindingPath = string.IsNullOrWhiteSpace(bindingPath) ? null : bindingPath.Trim();
        Baseline = baseline;
        TopLevelId = string.IsNullOrWhiteSpace(topLevelId) ? null : topLevelId.Trim();
        TopLevelTitle = string.IsNullOrWhiteSpace(topLevelTitle) ? null : topLevelTitle.Trim();
    }

    [JsonPropertyName("kind")]
    public string Kind { get; }

    [JsonPropertyName("expected")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Expected { get; }

    [JsonPropertyName("comparison")]
    public string Comparison { get; }

    [JsonPropertyName("valueType")]
    public string ValueType { get; }

    [JsonPropertyName("propertyName")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? PropertyName { get; }

    [JsonPropertyName("bindingPath")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? BindingPath { get; }

    [JsonPropertyName("baseline")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Baseline { get; }

    [JsonPropertyName("topLevelId")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? TopLevelId { get; }

    [JsonPropertyName("topLevelTitle")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? TopLevelTitle { get; }
}
