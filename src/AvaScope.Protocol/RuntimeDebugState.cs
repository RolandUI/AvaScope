using System.Text.Json.Serialization;

namespace AvaScope.Protocol;

public sealed record RuntimeDebugState
{
    [JsonConstructor]
    public RuntimeDebugState(
        string status,
        IReadOnlyDictionary<string, string>? fields = null,
        string? sourceType = null,
        bool truncated = false,
        int fieldCount = 0,
        int maximumFieldCount = 0,
        int maximumValueLength = 0)
    {
        if (string.IsNullOrWhiteSpace(status))
        {
            throw new ArgumentException("Debug state status cannot be empty.", nameof(status));
        }

        Status = status;
        Fields = fields ?? new Dictionary<string, string>();
        SourceType = string.IsNullOrWhiteSpace(sourceType) ? null : sourceType;
        Truncated = truncated;
        FieldCount = fieldCount < 0 ? throw new ArgumentOutOfRangeException(nameof(fieldCount)) : fieldCount;
        MaximumFieldCount = maximumFieldCount < 0 ? throw new ArgumentOutOfRangeException(nameof(maximumFieldCount)) : maximumFieldCount;
        MaximumValueLength = maximumValueLength < 0 ? throw new ArgumentOutOfRangeException(nameof(maximumValueLength)) : maximumValueLength;
    }

    [JsonPropertyName("status")]
    public string Status { get; }

    [JsonPropertyName("fields")]
    public IReadOnlyDictionary<string, string> Fields { get; }

    [JsonPropertyName("sourceType")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? SourceType { get; }

    [JsonPropertyName("truncated")]
    public bool Truncated { get; }

    [JsonPropertyName("fieldCount")]
    public int FieldCount { get; }

    [JsonPropertyName("maximumFieldCount")]
    public int MaximumFieldCount { get; }

    [JsonPropertyName("maximumValueLength")]
    public int MaximumValueLength { get; }
}
