using System.Text.Json.Serialization;

namespace AvaScope.Protocol;

public sealed record RuntimeMutationOperation
{
    [JsonConstructor]
    public RuntimeMutationOperation(
        string kind,
        string? propertyName = null,
        string? value = null,
        string? valueType = null,
        string? className = null,
        string? resourceKey = null,
        string? mutationId = null)
    {
        if (string.IsNullOrWhiteSpace(kind))
        {
            throw new ArgumentException("Runtime mutation operation kind cannot be empty.", nameof(kind));
        }

        Kind = kind.Trim();
        PropertyName = string.IsNullOrWhiteSpace(propertyName) ? null : propertyName.Trim();
        Value = value;
        ValueType = string.IsNullOrWhiteSpace(valueType) ? null : valueType.Trim();
        ClassName = string.IsNullOrWhiteSpace(className) ? null : className.Trim();
        ResourceKey = string.IsNullOrWhiteSpace(resourceKey) ? null : resourceKey.Trim();
        MutationId = string.IsNullOrWhiteSpace(mutationId) ? null : mutationId.Trim();
    }

    [JsonPropertyName("kind")]
    public string Kind { get; }

    [JsonPropertyName("propertyName")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? PropertyName { get; }

    [JsonPropertyName("value")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Value { get; }

    [JsonPropertyName("valueType")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ValueType { get; }

    [JsonPropertyName("className")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ClassName { get; }

    [JsonPropertyName("resourceKey")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ResourceKey { get; }

    [JsonPropertyName("mutationId")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? MutationId { get; }
}
