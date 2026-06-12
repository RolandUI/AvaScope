using System.Text.Json.Serialization;

namespace AvaScope.Protocol;

public sealed record AgentReviewMutationSummary
{
    [JsonConstructor]
    public AgentReviewMutationSummary(
        string mutationId,
        string operation,
        string status,
        bool applied,
        bool active,
        string? targetNodeId = null,
        string? propertyName = null)
    {
        if (string.IsNullOrWhiteSpace(mutationId))
        {
            throw new ArgumentException("Agent review mutation id cannot be empty.", nameof(mutationId));
        }

        if (string.IsNullOrWhiteSpace(operation))
        {
            throw new ArgumentException("Agent review mutation operation cannot be empty.", nameof(operation));
        }

        if (string.IsNullOrWhiteSpace(status))
        {
            throw new ArgumentException("Agent review mutation status cannot be empty.", nameof(status));
        }

        MutationId = mutationId.Trim();
        Operation = operation.Trim();
        Status = status.Trim();
        Applied = applied;
        Active = active;
        TargetNodeId = string.IsNullOrWhiteSpace(targetNodeId) ? null : targetNodeId.Trim();
        PropertyName = string.IsNullOrWhiteSpace(propertyName) ? null : propertyName.Trim();
    }

    [JsonPropertyName("mutationId")]
    public string MutationId { get; }

    [JsonPropertyName("operation")]
    public string Operation { get; }

    [JsonPropertyName("status")]
    public string Status { get; }

    [JsonPropertyName("applied")]
    public bool Applied { get; }

    [JsonPropertyName("active")]
    public bool Active { get; }

    [JsonPropertyName("targetNodeId")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? TargetNodeId { get; }

    [JsonPropertyName("propertyName")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? PropertyName { get; }
}
