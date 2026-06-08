using System.Text.Json.Serialization;

namespace AvaScope.Protocol;

public sealed record ComputedPropertyValue
{
    [JsonConstructor]
    public ComputedPropertyValue(
        string name,
        string value,
        string valueType,
        string priority = "unknown",
        string source = "unknown",
        string? diagnostic = null)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Property name cannot be empty.", nameof(name));
        }

        if (string.IsNullOrWhiteSpace(valueType))
        {
            throw new ArgumentException("Property value type cannot be empty.", nameof(valueType));
        }

        if (string.IsNullOrWhiteSpace(priority))
        {
            throw new ArgumentException("Property priority cannot be empty.", nameof(priority));
        }

        if (string.IsNullOrWhiteSpace(source))
        {
            throw new ArgumentException("Property source cannot be empty.", nameof(source));
        }

        Name = name;
        Value = value;
        ValueType = valueType;
        Priority = priority;
        Source = source;
        Diagnostic = string.IsNullOrWhiteSpace(diagnostic) ? null : diagnostic;
    }

    [JsonPropertyName("name")]
    public string Name { get; }

    [JsonPropertyName("value")]
    public string Value { get; }

    [JsonPropertyName("valueType")]
    public string ValueType { get; }

    [JsonPropertyName("priority")]
    public string Priority { get; }

    [JsonPropertyName("source")]
    public string Source { get; }

    [JsonPropertyName("diagnostic")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Diagnostic { get; }
}
