using System.Text.Json.Serialization;

namespace AvaScope.Protocol;

public sealed record AvaScopeToolCapability
{
    [JsonConstructor]
    public AvaScopeToolCapability(
        string adapter,
        string name,
        IReadOnlyList<string> capabilityIds,
        string status = AvaScopeCapabilityStatuses.Available,
        string? description = null,
        IReadOnlyDictionary<string, string>? metadata = null)
    {
        if (string.IsNullOrWhiteSpace(adapter))
        {
            throw new ArgumentException("Tool adapter cannot be empty.", nameof(adapter));
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Tool name cannot be empty.", nameof(name));
        }

        if (capabilityIds.Count == 0)
        {
            throw new ArgumentException("Tool capability ids cannot be empty.", nameof(capabilityIds));
        }

        Adapter = adapter.Trim();
        Name = name.Trim();
        CapabilityIds = capabilityIds
            .Where(static id => !string.IsNullOrWhiteSpace(id))
            .Select(static id => id.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        Status = string.IsNullOrWhiteSpace(status)
            ? AvaScopeCapabilityStatuses.Available
            : status.Trim();
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        Metadata = metadata ?? new Dictionary<string, string>();
    }

    [JsonPropertyName("adapter")]
    public string Adapter { get; }

    [JsonPropertyName("name")]
    public string Name { get; }

    [JsonPropertyName("capabilityIds")]
    public IReadOnlyList<string> CapabilityIds { get; }

    [JsonPropertyName("status")]
    public string Status { get; }

    [JsonPropertyName("description")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Description { get; }

    [JsonPropertyName("metadata")]
    public IReadOnlyDictionary<string, string> Metadata { get; }
}
