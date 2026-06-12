using System.Text.Json.Serialization;

namespace AvaScope.Protocol;

public sealed record RuntimeMutationRequest
{
    public const int MaximumMetadataEntries = 32;

    [JsonConstructor]
    public RuntimeMutationRequest(
        string requestId,
        RuntimeTargetContext target,
        RuntimeMutationOperation operation,
        IReadOnlyList<string>? requestedCapabilities = null,
        IReadOnlyDictionary<string, string>? metadata = null)
    {
        if (string.IsNullOrWhiteSpace(requestId))
        {
            throw new ArgumentException("Runtime mutation request id cannot be empty.", nameof(requestId));
        }

        RequestId = requestId.Trim();
        Target = target ?? throw new ArgumentNullException(nameof(target));
        Operation = operation ?? throw new ArgumentNullException(nameof(operation));
        RequestedCapabilities = requestedCapabilities ?? [];
        Metadata = metadata is null
            ? new Dictionary<string, string>()
            : metadata.Take(MaximumMetadataEntries).ToDictionary(
                static item => item.Key,
                static item => item.Value,
                StringComparer.Ordinal);
    }

    [JsonPropertyName("requestId")]
    public string RequestId { get; }

    [JsonPropertyName("target")]
    public RuntimeTargetContext Target { get; }

    [JsonPropertyName("operation")]
    public RuntimeMutationOperation Operation { get; }

    [JsonPropertyName("requestedCapabilities")]
    public IReadOnlyList<string> RequestedCapabilities { get; }

    [JsonPropertyName("metadata")]
    public IReadOnlyDictionary<string, string> Metadata { get; }
}
