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
        string status,
        string? expression = null,
        string? expressionType = null,
        string? resolvedValueStatus = null,
        string? converterStatus = null,
        string? fallbackStatus = null,
        string? nullStatus = null,
        string? compiledBindingStatus = null,
        RuntimeSourceBinding? sourceMap = null)
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
        Expression = string.IsNullOrWhiteSpace(expression) ? null : expression;
        ExpressionType = string.IsNullOrWhiteSpace(expressionType) ? null : expressionType;
        ResolvedValueStatus = string.IsNullOrWhiteSpace(resolvedValueStatus) ? "available" : resolvedValueStatus;
        ConverterStatus = string.IsNullOrWhiteSpace(converterStatus) ? "not_available" : converterStatus;
        FallbackStatus = string.IsNullOrWhiteSpace(fallbackStatus) ? "not_available" : fallbackStatus;
        NullStatus = string.IsNullOrWhiteSpace(nullStatus) ? (string.Equals(Value, "null", StringComparison.Ordinal) ? "null" : "not_null") : nullStatus;
        CompiledBindingStatus = string.IsNullOrWhiteSpace(compiledBindingStatus) ? "not_available" : compiledBindingStatus;
        SourceMap = sourceMap;
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

    [JsonPropertyName("expression")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Expression { get; }

    [JsonPropertyName("expressionType")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ExpressionType { get; }

    [JsonPropertyName("resolvedValueStatus")]
    public string ResolvedValueStatus { get; }

    [JsonPropertyName("converterStatus")]
    public string ConverterStatus { get; }

    [JsonPropertyName("fallbackStatus")]
    public string FallbackStatus { get; }

    [JsonPropertyName("nullStatus")]
    public string NullStatus { get; }

    [JsonPropertyName("compiledBindingStatus")]
    public string CompiledBindingStatus { get; }

    [JsonPropertyName("sourceMap")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public RuntimeSourceBinding? SourceMap { get; }
}
