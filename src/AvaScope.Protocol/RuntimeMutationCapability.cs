using System.Text.Json.Serialization;

namespace AvaScope.Protocol;

public sealed record RuntimeMutationCapability
{
    [JsonConstructor]
    public RuntimeMutationCapability(
        string name,
        bool available,
        IReadOnlyList<string>? supportedOperations = null,
        IReadOnlyList<string>? supportedProperties = null,
        string? reason = null,
        IReadOnlyDictionary<string, string>? metadata = null)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Runtime mutation capability name cannot be empty.", nameof(name));
        }

        Name = name.Trim();
        Available = available;
        SupportedOperations = supportedOperations ?? [];
        SupportedProperties = supportedProperties ?? [];
        Reason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
        Metadata = metadata ?? new Dictionary<string, string>();
    }

    [JsonPropertyName("name")]
    public string Name { get; }

    [JsonPropertyName("available")]
    public bool Available { get; }

    [JsonPropertyName("supportedOperations")]
    public IReadOnlyList<string> SupportedOperations { get; }

    [JsonPropertyName("supportedProperties")]
    public IReadOnlyList<string> SupportedProperties { get; }

    [JsonPropertyName("reason")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Reason { get; }

    [JsonPropertyName("metadata")]
    public IReadOnlyDictionary<string, string> Metadata { get; }
}
