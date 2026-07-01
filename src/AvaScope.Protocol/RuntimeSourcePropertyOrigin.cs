using System.Text.Json.Serialization;

namespace AvaScope.Protocol;

public sealed record RuntimeSourcePropertyOrigin
{
    [JsonConstructor]
    public RuntimeSourcePropertyOrigin(
        string propertyName,
        string value,
        string valueType,
        string origin,
        string priority,
        string? diagnostic = null,
        string? resourceKey = null,
        string? styleSelector = null,
        string? templateOrigin = null,
        string? sourcePath = null,
        int? line = null)
    {
        if (string.IsNullOrWhiteSpace(propertyName))
        {
            throw new ArgumentException("Property name cannot be empty.", nameof(propertyName));
        }

        PropertyName = propertyName;
        Value = string.IsNullOrWhiteSpace(value) ? "not_available" : value;
        ValueType = string.IsNullOrWhiteSpace(valueType) ? "not_available" : valueType;
        Origin = string.IsNullOrWhiteSpace(origin) ? "unknown" : origin;
        Priority = string.IsNullOrWhiteSpace(priority) ? "unknown" : priority;
        Diagnostic = string.IsNullOrWhiteSpace(diagnostic) ? null : diagnostic;
        ResourceKey = string.IsNullOrWhiteSpace(resourceKey) ? null : resourceKey;
        StyleSelector = string.IsNullOrWhiteSpace(styleSelector) ? null : styleSelector;
        TemplateOrigin = string.IsNullOrWhiteSpace(templateOrigin) ? null : templateOrigin;
        SourcePath = string.IsNullOrWhiteSpace(sourcePath) ? null : Path.GetFullPath(sourcePath);
        Line = line;
    }

    [JsonPropertyName("propertyName")]
    public string PropertyName { get; }

    [JsonPropertyName("value")]
    public string Value { get; }

    [JsonPropertyName("valueType")]
    public string ValueType { get; }

    [JsonPropertyName("origin")]
    public string Origin { get; }

    [JsonPropertyName("priority")]
    public string Priority { get; }

    [JsonPropertyName("diagnostic")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Diagnostic { get; }

    [JsonPropertyName("resourceKey")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ResourceKey { get; }

    [JsonPropertyName("styleSelector")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? StyleSelector { get; }

    [JsonPropertyName("templateOrigin")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? TemplateOrigin { get; }

    [JsonPropertyName("sourcePath")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? SourcePath { get; }

    [JsonPropertyName("line")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? Line { get; }
}
