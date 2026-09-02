using System.Text.Json.Serialization;

namespace AvaScope.Protocol;

public sealed record RuntimeWaitObservation
{
    [JsonConstructor]
    public RuntimeWaitObservation(
        string condition,
        string availability,
        bool matched,
        DateTimeOffset capturedAt,
        string? value = null,
        string valueType = "not_available",
        string comparison = SemanticWaitComparisons.Equal,
        string? expected = null,
        string? baseline = null,
        string? source = null,
        string? message = null)
    {
        if (string.IsNullOrWhiteSpace(condition))
        {
            throw new ArgumentException("Wait condition cannot be empty.", nameof(condition));
        }

        if (string.IsNullOrWhiteSpace(availability))
        {
            throw new ArgumentException("Wait observation availability cannot be empty.", nameof(availability));
        }

        Condition = condition;
        Availability = availability;
        Matched = matched;
        CapturedAt = capturedAt;
        Value = value;
        ValueType = string.IsNullOrWhiteSpace(valueType) ? "not_available" : valueType;
        Comparison = string.IsNullOrWhiteSpace(comparison) ? SemanticWaitComparisons.Equal : comparison;
        Expected = expected;
        Baseline = baseline;
        Source = string.IsNullOrWhiteSpace(source) ? null : source;
        Message = string.IsNullOrWhiteSpace(message) ? null : message;
    }

    [JsonPropertyName("condition")]
    public string Condition { get; }

    [JsonPropertyName("availability")]
    public string Availability { get; }

    [JsonPropertyName("matched")]
    public bool Matched { get; }

    [JsonPropertyName("capturedAt")]
    public DateTimeOffset CapturedAt { get; }

    [JsonPropertyName("value")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Value { get; }

    [JsonPropertyName("valueType")]
    public string ValueType { get; }

    [JsonPropertyName("comparison")]
    public string Comparison { get; }

    [JsonPropertyName("expected")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Expected { get; }

    [JsonPropertyName("baseline")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Baseline { get; }

    [JsonPropertyName("source")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Source { get; }

    [JsonPropertyName("message")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Message { get; }
}
