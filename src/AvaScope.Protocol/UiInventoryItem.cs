using System.Text.Json.Serialization;

namespace AvaScope.Protocol;

public sealed record UiInventoryItem
{
    public const int MaximumSampleTargets = 8;
    public const int MaximumDetails = 16;

    [JsonConstructor]
    public UiInventoryItem(
        string itemId,
        string category,
        string name,
        int count,
        string provenance,
        string status = "available",
        IReadOnlyList<RuntimeTargetContext>? sampleTargets = null,
        IReadOnlyDictionary<string, string>? details = null)
    {
        if (string.IsNullOrWhiteSpace(itemId))
        {
            throw new ArgumentException("Inventory item id cannot be empty.", nameof(itemId));
        }

        if (string.IsNullOrWhiteSpace(category))
        {
            throw new ArgumentException("Inventory item category cannot be empty.", nameof(category));
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Inventory item name cannot be empty.", nameof(name));
        }

        if (count < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(count), count, "Inventory item count cannot be negative.");
        }

        if (string.IsNullOrWhiteSpace(provenance))
        {
            throw new ArgumentException("Inventory item provenance cannot be empty.", nameof(provenance));
        }

        if (string.IsNullOrWhiteSpace(status))
        {
            throw new ArgumentException("Inventory item status cannot be empty.", nameof(status));
        }

        ItemId = itemId.Trim();
        Category = category.Trim();
        Name = name.Trim();
        Count = count;
        Provenance = provenance.Trim();
        Status = status.Trim();
        SampleTargets = (sampleTargets ?? []).Take(MaximumSampleTargets).ToArray();
        Details = details is null
            ? new Dictionary<string, string>()
            : details
                .Where(static item => !string.IsNullOrWhiteSpace(item.Key))
                .Take(MaximumDetails)
                .ToDictionary(static item => item.Key, static item => item.Value, StringComparer.Ordinal);
    }

    [JsonPropertyName("itemId")]
    public string ItemId { get; }

    [JsonPropertyName("category")]
    public string Category { get; }

    [JsonPropertyName("name")]
    public string Name { get; }

    [JsonPropertyName("count")]
    public int Count { get; }

    [JsonPropertyName("provenance")]
    public string Provenance { get; }

    [JsonPropertyName("status")]
    public string Status { get; }

    [JsonPropertyName("sampleTargets")]
    public IReadOnlyList<RuntimeTargetContext> SampleTargets { get; }

    [JsonPropertyName("details")]
    public IReadOnlyDictionary<string, string> Details { get; }
}
