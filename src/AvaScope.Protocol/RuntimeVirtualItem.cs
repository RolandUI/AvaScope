using System.Text.Json.Serialization;

namespace AvaScope.Protocol;

public sealed record RuntimeVirtualItemRequest
{
    [JsonConstructor]
    public RuntimeVirtualItemRequest(RuntimeTargetContext collection, string keyProperty, string key,
        string action = "find", int maxItems = 2000, int timeoutMs = 1000)
    {
        Collection = collection ?? throw new ArgumentNullException(nameof(collection));
        if (collection.TreeKind != TreeKinds.Visual || string.IsNullOrWhiteSpace(collection.NodeId))
            throw new ArgumentException("Virtual items require an explicit visual collection node target.", nameof(collection));
        if (string.IsNullOrWhiteSpace(keyProperty) || keyProperty.Length > 128
            || !keyProperty.All(character => char.IsLetterOrDigit(character) || character == '_'))
            throw new ArgumentException("Choose one public scalar item key property; paths and expressions are unsupported.", nameof(keyProperty));
        if (string.IsNullOrWhiteSpace(key) || key.Length > 256) throw new ArgumentException("Item keys require 1–256 characters.", nameof(key));
        if (action is not ("find" or "reveal" or "select")) throw new ArgumentException("Action must be find, reveal or select.", nameof(action));
        if (maxItems is < 1 or > 10000) throw new ArgumentOutOfRangeException(nameof(maxItems));
        if (timeoutMs is < 50 or > 3000) throw new ArgumentOutOfRangeException(nameof(timeoutMs));
        KeyProperty = keyProperty;
        Key = key;
        Action = action;
        MaxItems = maxItems;
        TimeoutMs = timeoutMs;
    }

    [JsonPropertyName("collection")] public RuntimeTargetContext Collection { get; }
    [JsonPropertyName("keyProperty")] public string KeyProperty { get; }
    [JsonPropertyName("key")] public string Key { get; }
    [JsonPropertyName("action")] public string Action { get; }
    [JsonPropertyName("maxItems")] public int MaxItems { get; }
    [JsonPropertyName("timeoutMs")] public int TimeoutMs { get; }
}

public sealed record RuntimeVirtualItemResponse(
    [property: JsonPropertyName("collection")] RuntimeTargetContext Collection,
    [property: JsonPropertyName("keyProperty")] string KeyProperty,
    [property: JsonPropertyName("key")] string Key,
    [property: JsonPropertyName("action")] string Action,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("index")] int Index,
    [property: JsonPropertyName("itemCount")] int ItemCount,
    [property: JsonPropertyName("realized")] bool Realized,
    [property: JsonPropertyName("rendered")] bool Rendered,
    [property: JsonPropertyName("selected")] bool? Selected,
    [property: JsonPropertyName("container")] RuntimeTargetContext? Container,
    [property: JsonPropertyName("elapsedMs")] long ElapsedMs,
    [property: JsonPropertyName("scannedItems")] int ScannedItems,
    [property: JsonPropertyName("scrollRequests")] int ScrollRequests,
    [property: JsonPropertyName("provenance")] string Provenance = "public_avalonia_items_view_scalar_key_and_scroll_into_view");
