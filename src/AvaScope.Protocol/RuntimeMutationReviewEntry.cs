using System.Text.Json.Serialization;

namespace AvaScope.Protocol;

public sealed record RuntimeMutationReviewEntry
{
    public const int MaximumDiagnostics = 8;
    public const int MaximumMetadataEntries = 32;

    [JsonConstructor]
    public RuntimeMutationReviewEntry(
        long sequence,
        string requestId,
        string mutationId,
        SessionId sessionId,
        string topLevelId,
        RuntimeTargetContext target,
        RuntimeMutationOperation operation,
        string status,
        bool applied,
        bool active,
        DateTimeOffset evaluatedAt,
        IReadOnlyList<ProtocolError>? diagnostics = null,
        IReadOnlyDictionary<string, string>? metadata = null)
    {
        if (sequence < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(sequence), sequence, "Review entry sequence must be positive.");
        }

        if (string.IsNullOrWhiteSpace(requestId))
        {
            throw new ArgumentException("Review entry request id cannot be empty.", nameof(requestId));
        }

        if (string.IsNullOrWhiteSpace(mutationId))
        {
            throw new ArgumentException("Review entry mutation id cannot be empty.", nameof(mutationId));
        }

        if (string.IsNullOrWhiteSpace(topLevelId))
        {
            throw new ArgumentException("Review entry top-level id cannot be empty.", nameof(topLevelId));
        }

        if (string.IsNullOrWhiteSpace(status))
        {
            throw new ArgumentException("Review entry status cannot be empty.", nameof(status));
        }

        Sequence = sequence;
        RequestId = requestId.Trim();
        MutationId = mutationId.Trim();
        SessionId = sessionId ?? throw new ArgumentNullException(nameof(sessionId));
        TopLevelId = topLevelId.Trim();
        Target = target ?? throw new ArgumentNullException(nameof(target));
        Operation = operation ?? throw new ArgumentNullException(nameof(operation));
        Status = status.Trim();
        Applied = applied;
        Active = active;
        EvaluatedAt = evaluatedAt;
        Diagnostics = (diagnostics ?? []).Take(MaximumDiagnostics).ToArray();
        Metadata = metadata is null
            ? new Dictionary<string, string>()
            : metadata.Take(MaximumMetadataEntries).ToDictionary(
                static item => item.Key,
                static item => item.Value,
                StringComparer.Ordinal);
    }

    [JsonPropertyName("sequence")]
    public long Sequence { get; }

    [JsonPropertyName("requestId")]
    public string RequestId { get; }

    [JsonPropertyName("mutationId")]
    public string MutationId { get; }

    [JsonPropertyName("sessionId")]
    public SessionId SessionId { get; }

    [JsonPropertyName("topLevelId")]
    public string TopLevelId { get; }

    [JsonPropertyName("target")]
    public RuntimeTargetContext Target { get; }

    [JsonPropertyName("operation")]
    public RuntimeMutationOperation Operation { get; }

    [JsonPropertyName("status")]
    public string Status { get; }

    [JsonPropertyName("applied")]
    public bool Applied { get; }

    [JsonPropertyName("active")]
    public bool Active { get; }

    [JsonPropertyName("evaluatedAt")]
    public DateTimeOffset EvaluatedAt { get; }

    [JsonPropertyName("diagnostics")]
    public IReadOnlyList<ProtocolError> Diagnostics { get; }

    [JsonPropertyName("metadata")]
    public IReadOnlyDictionary<string, string> Metadata { get; }
}
