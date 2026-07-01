using System.Text.Json.Serialization;

namespace AvaScope.Protocol;

public sealed record RuntimeLayoutExplanation
{
    [JsonConstructor]
    public RuntimeLayoutExplanation(
        string status,
        string summary,
        RuntimeLayoutMetrics? node = null,
        string? constraintStatus = null,
        RuntimeSize? inferredParentConstraint = null,
        IReadOnlyList<RuntimeLayoutAncestor>? ancestors = null,
        IReadOnlyList<RuntimeLayoutReason>? reasons = null)
    {
        if (string.IsNullOrWhiteSpace(status))
        {
            throw new ArgumentException("Layout explanation status cannot be empty.", nameof(status));
        }

        if (string.IsNullOrWhiteSpace(summary))
        {
            throw new ArgumentException("Layout explanation summary cannot be empty.", nameof(summary));
        }

        Status = status;
        Summary = summary;
        Node = node;
        ConstraintStatus = string.IsNullOrWhiteSpace(constraintStatus) ? "inferred_from_parent_bounds" : constraintStatus;
        InferredParentConstraint = inferredParentConstraint;
        Ancestors = ancestors ?? Array.Empty<RuntimeLayoutAncestor>();
        Reasons = reasons ?? Array.Empty<RuntimeLayoutReason>();
    }

    [JsonPropertyName("status")]
    public string Status { get; }

    [JsonPropertyName("summary")]
    public string Summary { get; }

    [JsonPropertyName("node")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public RuntimeLayoutMetrics? Node { get; }

    [JsonPropertyName("constraintStatus")]
    public string ConstraintStatus { get; }

    [JsonPropertyName("inferredParentConstraint")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public RuntimeSize? InferredParentConstraint { get; }

    [JsonPropertyName("ancestors")]
    public IReadOnlyList<RuntimeLayoutAncestor> Ancestors { get; }

    [JsonPropertyName("reasons")]
    public IReadOnlyList<RuntimeLayoutReason> Reasons { get; }
}
