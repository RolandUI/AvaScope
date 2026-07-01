using System.Text.Json.Serialization;

namespace AvaScope.Protocol;

public sealed record RuntimeBindingState
{
    [JsonConstructor]
    public RuntimeBindingState(
        string dataContextStatus,
        string? dataContextType = null,
        string bindingMetadataStatus = "not_available",
        IReadOnlyList<RuntimeBoundProperty>? boundProperties = null,
        IReadOnlyList<ProtocolError>? diagnostics = null,
        RuntimeNodeSourceMap? sourceMap = null)
    {
        if (string.IsNullOrWhiteSpace(dataContextStatus))
        {
            throw new ArgumentException("DataContext status cannot be empty.", nameof(dataContextStatus));
        }

        DataContextStatus = dataContextStatus;
        DataContextType = string.IsNullOrWhiteSpace(dataContextType) ? null : dataContextType;
        BindingMetadataStatus = string.IsNullOrWhiteSpace(bindingMetadataStatus)
            ? "not_available"
            : bindingMetadataStatus;
        BoundProperties = boundProperties ?? Array.Empty<RuntimeBoundProperty>();
        Diagnostics = diagnostics ?? Array.Empty<ProtocolError>();
        SourceMap = sourceMap;
    }

    [JsonPropertyName("dataContextStatus")]
    public string DataContextStatus { get; }

    [JsonPropertyName("dataContextType")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? DataContextType { get; }

    [JsonPropertyName("bindingMetadataStatus")]
    public string BindingMetadataStatus { get; }

    [JsonPropertyName("boundProperties")]
    public IReadOnlyList<RuntimeBoundProperty> BoundProperties { get; }

    [JsonPropertyName("diagnostics")]
    public IReadOnlyList<ProtocolError> Diagnostics { get; }

    [JsonPropertyName("sourceMap")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public RuntimeNodeSourceMap? SourceMap { get; }
}
