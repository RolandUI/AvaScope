using System.Text.Json.Serialization;

namespace AvaScope.Protocol;

public sealed record RuntimeNodeSourceMap
{
    [JsonConstructor]
    public RuntimeNodeSourceMap(
        string status,
        string provenance,
        string? filePath = null,
        int? line = null,
        int? column = null,
        string? xName = null,
        string? elementType = null,
        string? elementPath = null,
        IReadOnlyList<RuntimeSourcePropertyOrigin>? propertyOrigins = null,
        IReadOnlyList<RuntimeSourceBinding>? bindings = null,
        IReadOnlyList<ProtocolError>? diagnostics = null)
    {
        if (string.IsNullOrWhiteSpace(status))
        {
            throw new ArgumentException("Source map status cannot be empty.", nameof(status));
        }

        if (string.IsNullOrWhiteSpace(provenance))
        {
            throw new ArgumentException("Source map provenance cannot be empty.", nameof(provenance));
        }

        Status = status;
        Provenance = provenance;
        FilePath = string.IsNullOrWhiteSpace(filePath) ? null : Path.GetFullPath(filePath);
        Line = line;
        Column = column;
        XName = string.IsNullOrWhiteSpace(xName) ? null : xName;
        ElementType = string.IsNullOrWhiteSpace(elementType) ? null : elementType;
        ElementPath = string.IsNullOrWhiteSpace(elementPath) ? null : elementPath;
        PropertyOrigins = propertyOrigins ?? Array.Empty<RuntimeSourcePropertyOrigin>();
        Bindings = bindings ?? Array.Empty<RuntimeSourceBinding>();
        Diagnostics = diagnostics ?? Array.Empty<ProtocolError>();
    }

    [JsonPropertyName("status")]
    public string Status { get; }

    [JsonPropertyName("provenance")]
    public string Provenance { get; }

    [JsonPropertyName("filePath")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? FilePath { get; }

    [JsonPropertyName("line")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? Line { get; }

    [JsonPropertyName("column")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? Column { get; }

    [JsonPropertyName("xName")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? XName { get; }

    [JsonPropertyName("elementType")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ElementType { get; }

    [JsonPropertyName("elementPath")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ElementPath { get; }

    [JsonPropertyName("propertyOrigins")]
    public IReadOnlyList<RuntimeSourcePropertyOrigin> PropertyOrigins { get; }

    [JsonPropertyName("bindings")]
    public IReadOnlyList<RuntimeSourceBinding> Bindings { get; }

    [JsonPropertyName("diagnostics")]
    public IReadOnlyList<ProtocolError> Diagnostics { get; }
}
