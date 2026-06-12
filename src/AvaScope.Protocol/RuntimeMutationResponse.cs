using System.Text.Json.Serialization;

namespace AvaScope.Protocol;

public sealed record RuntimeMutationResponse
{
    public const int MaximumDiagnostics = 16;
    public const int MaximumMetadataEntries = 48;

    [JsonConstructor]
    public RuntimeMutationResponse(
        string requestId,
        string mutationId,
        SessionId sessionId,
        string topLevelId,
        RuntimeTargetContext target,
        RuntimeMutationOperation operation,
        string status,
        bool applied,
        DateTimeOffset evaluatedAt,
        IReadOnlyList<RuntimeMutationCapability>? capabilities = null,
        IReadOnlyList<ProtocolError>? diagnostics = null,
        IReadOnlyDictionary<string, string>? metadata = null)
    {
        if (string.IsNullOrWhiteSpace(requestId))
        {
            throw new ArgumentException("Runtime mutation request id cannot be empty.", nameof(requestId));
        }

        if (string.IsNullOrWhiteSpace(mutationId))
        {
            throw new ArgumentException("Runtime mutation id cannot be empty.", nameof(mutationId));
        }

        if (string.IsNullOrWhiteSpace(topLevelId))
        {
            throw new ArgumentException("Top-level id cannot be empty.", nameof(topLevelId));
        }

        if (string.IsNullOrWhiteSpace(status))
        {
            throw new ArgumentException("Runtime mutation status cannot be empty.", nameof(status));
        }

        RequestId = requestId.Trim();
        MutationId = mutationId.Trim();
        SessionId = sessionId ?? throw new ArgumentNullException(nameof(sessionId));
        TopLevelId = topLevelId;
        Target = target ?? throw new ArgumentNullException(nameof(target));
        Operation = operation ?? throw new ArgumentNullException(nameof(operation));
        Status = status.Trim();
        Applied = applied;
        EvaluatedAt = evaluatedAt;
        Capabilities = capabilities ?? [];
        Diagnostics = (diagnostics ?? []).Take(MaximumDiagnostics).ToArray();
        Metadata = metadata is null
            ? new Dictionary<string, string>()
            : metadata.Take(MaximumMetadataEntries).ToDictionary(
                static item => item.Key,
                static item => item.Value,
                StringComparer.Ordinal);
    }

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

    [JsonPropertyName("evaluatedAt")]
    public DateTimeOffset EvaluatedAt { get; }

    [JsonPropertyName("capabilities")]
    public IReadOnlyList<RuntimeMutationCapability> Capabilities { get; }

    [JsonPropertyName("diagnostics")]
    public IReadOnlyList<ProtocolError> Diagnostics { get; }

    [JsonPropertyName("metadata")]
    public IReadOnlyDictionary<string, string> Metadata { get; }

    [JsonPropertyName("agentReview")]
    public AgentReviewSurface AgentReview => new(
        Status,
        Applied
            ? $"Runtime mutation '{Operation.Kind}' was applied."
            : $"Runtime mutation '{Operation.Kind}' returned status '{Status}'.",
        [
            $"request: {RequestId}",
            $"mutation: {MutationId}",
            $"topLevel: {TopLevelId}"
        ],
        Diagnostics
            .Select(static diagnostic => new AgentReviewFailure("mutation", diagnostic.Message, diagnostic.Code))
            .ToArray(),
        [
            new AgentReviewMutationSummary(
                MutationId,
                Operation.Kind,
                Status,
                Applied,
                IsActiveMutation(),
                Target.NodeId,
                Operation.PropertyName)
        ],
        truncated: Diagnostics.Count > AgentReviewSurface.MaximumFailureSummaries);

    private bool IsActiveMutation()
    {
        return Applied
            && string.Equals(Status, RuntimeMutationStatuses.Applied, StringComparison.Ordinal)
            && Operation.Kind is not RuntimeMutationOperationKinds.ResetMutation
            && Operation.Kind is not RuntimeMutationOperationKinds.ResetAll;
    }
}
