using System.Text.Json.Serialization;

namespace AvaScope.Protocol;

public sealed record RuntimeCustomActionParameterDescriptor
{
    [JsonConstructor]
    public RuntimeCustomActionParameterDescriptor(
        string name,
        string type = RuntimeCustomActionParameterTypes.String,
        bool required = false,
        string? description = null,
        IReadOnlyList<string>? allowedValues = null)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Custom action parameter name cannot be empty.", nameof(name));
        }

        if (!RuntimeCustomActionParameterTypes.All.Contains(type, StringComparer.Ordinal))
        {
            throw new ArgumentException($"Custom action parameter type '{type}' is not supported.", nameof(type));
        }

        Name = name.Trim();
        Type = type;
        Required = required;
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        AllowedValues = (allowedValues ?? [])
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .Select(static value => value.Trim())
            .Distinct(StringComparer.Ordinal)
            .Take(32)
            .ToArray();
    }

    [JsonPropertyName("name")] public string Name { get; }
    [JsonPropertyName("type")] public string Type { get; }
    [JsonPropertyName("required")] public bool Required { get; }
    [JsonPropertyName("description")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Description { get; }
    [JsonPropertyName("allowedValues")] public IReadOnlyList<string> AllowedValues { get; }
}
