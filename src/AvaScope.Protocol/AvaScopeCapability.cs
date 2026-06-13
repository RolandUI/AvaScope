using System.Text.Json.Serialization;

namespace AvaScope.Protocol;

public sealed record AvaScopeCapability
{
    [JsonConstructor]
    public AvaScopeCapability(
        string id,
        string category,
        string status,
        string description,
        ProtocolVersion? sinceProtocolVersion = null,
        IReadOnlyList<string>? tools = null,
        IReadOnlyList<string>? requires = null,
        string? reason = null,
        IReadOnlyDictionary<string, string>? metadata = null)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            throw new ArgumentException("Capability id cannot be empty.", nameof(id));
        }

        if (string.IsNullOrWhiteSpace(category))
        {
            throw new ArgumentException("Capability category cannot be empty.", nameof(category));
        }

        if (string.IsNullOrWhiteSpace(status))
        {
            throw new ArgumentException("Capability status cannot be empty.", nameof(status));
        }

        if (string.IsNullOrWhiteSpace(description))
        {
            throw new ArgumentException("Capability description cannot be empty.", nameof(description));
        }

        Id = id.Trim();
        Category = category.Trim();
        Status = status.Trim();
        Description = description.Trim();
        SinceProtocolVersion = sinceProtocolVersion ?? AvaScopeProtocol.CurrentVersion;
        Tools = NormalizeValues(tools);
        Requires = NormalizeValues(requires);
        Reason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
        Metadata = metadata ?? new Dictionary<string, string>();
    }

    [JsonPropertyName("id")]
    public string Id { get; }

    [JsonPropertyName("category")]
    public string Category { get; }

    [JsonPropertyName("status")]
    public string Status { get; }

    [JsonPropertyName("description")]
    public string Description { get; }

    [JsonPropertyName("sinceProtocolVersion")]
    public ProtocolVersion SinceProtocolVersion { get; }

    [JsonPropertyName("tools")]
    public IReadOnlyList<string> Tools { get; }

    [JsonPropertyName("requires")]
    public IReadOnlyList<string> Requires { get; }

    [JsonPropertyName("reason")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Reason { get; }

    [JsonPropertyName("metadata")]
    public IReadOnlyDictionary<string, string> Metadata { get; }

    private static IReadOnlyList<string> NormalizeValues(IReadOnlyList<string>? values)
    {
        return (values ?? [])
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .Select(static value => value.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }
}
