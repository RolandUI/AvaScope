using System.Text.Json.Serialization;

namespace AvaScope.Protocol;

public sealed record RuntimeSourceBinding
{
    [JsonConstructor]
    public RuntimeSourceBinding(
        string targetProperty,
        string bindingPath,
        string expression,
        string bindingKind,
        string status = "available",
        string? sourcePath = null,
        int? line = null,
        string? converterResourceKey = null,
        string? dataTypeName = null,
        string? diagnostics = null)
    {
        if (string.IsNullOrWhiteSpace(targetProperty))
        {
            throw new ArgumentException("Target property cannot be empty.", nameof(targetProperty));
        }

        TargetProperty = targetProperty;
        BindingPath = string.IsNullOrWhiteSpace(bindingPath) ? "not_available" : bindingPath;
        Expression = string.IsNullOrWhiteSpace(expression) ? "not_available" : expression;
        BindingKind = string.IsNullOrWhiteSpace(bindingKind) ? "unknown" : bindingKind;
        Status = string.IsNullOrWhiteSpace(status) ? "unknown" : status;
        SourcePath = string.IsNullOrWhiteSpace(sourcePath) ? null : Path.GetFullPath(sourcePath);
        Line = line;
        ConverterResourceKey = string.IsNullOrWhiteSpace(converterResourceKey) ? null : converterResourceKey;
        DataTypeName = string.IsNullOrWhiteSpace(dataTypeName) ? null : dataTypeName;
        Diagnostics = string.IsNullOrWhiteSpace(diagnostics) ? null : diagnostics;
    }

    [JsonPropertyName("targetProperty")]
    public string TargetProperty { get; }

    [JsonPropertyName("bindingPath")]
    public string BindingPath { get; }

    [JsonPropertyName("expression")]
    public string Expression { get; }

    [JsonPropertyName("bindingKind")]
    public string BindingKind { get; }

    [JsonPropertyName("status")]
    public string Status { get; }

    [JsonPropertyName("sourcePath")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? SourcePath { get; }

    [JsonPropertyName("line")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? Line { get; }

    [JsonPropertyName("converterResourceKey")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ConverterResourceKey { get; }

    [JsonPropertyName("dataTypeName")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? DataTypeName { get; }

    [JsonPropertyName("diagnostics")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Diagnostics { get; }
}
