using System.Text.Json.Serialization;

namespace AvaScope.Protocol;

public sealed record RuntimeBoundProperty
{
    [JsonConstructor]
    public RuntimeBoundProperty(
        string propertyName,
        string bindingPath,
        string value,
        string valueType,
        string source,
        string status)
    {
        if (string.IsNullOrWhiteSpace(propertyName))
        {
            throw new ArgumentException("Property name cannot be empty.", nameof(propertyName));
        }

        PropertyName = propertyName;
        BindingPath = string.IsNullOrWhiteSpace(bindingPath) ? "unknown" : bindingPath;
        Value = string.IsNullOrWhiteSpace(value) ? "not_available" : value;
        ValueType = string.IsNullOrWhiteSpace(valueType) ? "not_available" : valueType;
        Source = string.IsNullOrWhiteSpace(source) ? "unknown" : source;
        Status = string.IsNullOrWhiteSpace(status) ? "unknown" : status;
    }

    [JsonPropertyName("propertyName")]
    public string PropertyName { get; }

    [JsonPropertyName("bindingPath")]
    public string BindingPath { get; }

    [JsonPropertyName("value")]
    public string Value { get; }

    [JsonPropertyName("valueType")]
    public string ValueType { get; }

    [JsonPropertyName("source")]
    public string Source { get; }

    [JsonPropertyName("status")]
    public string Status { get; }
}
