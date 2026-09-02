using System.Text.Json.Serialization;

namespace AvaScope.Protocol;

public sealed record SemanticWorkflowPlan
{
    [JsonConstructor]
    public SemanticWorkflowPlan(
        bool valid,
        int expandedStepCount,
        int estimatedMaximumExecutions,
        int maximumNestingDepth,
        int maximumArtifactCount,
        IReadOnlyList<SemanticWorkflowPlanItem>? steps = null,
        IReadOnlyList<ProtocolError>? diagnostics = null)
    {
        Valid = valid;
        ExpandedStepCount = expandedStepCount;
        EstimatedMaximumExecutions = estimatedMaximumExecutions;
        MaximumNestingDepth = maximumNestingDepth;
        MaximumArtifactCount = maximumArtifactCount;
        Steps = steps ?? Array.Empty<SemanticWorkflowPlanItem>();
        Diagnostics = diagnostics ?? Array.Empty<ProtocolError>();
    }

    [JsonPropertyName("valid")]
    public bool Valid { get; }

    [JsonPropertyName("expandedStepCount")]
    public int ExpandedStepCount { get; }

    [JsonPropertyName("estimatedMaximumExecutions")]
    public int EstimatedMaximumExecutions { get; }

    [JsonPropertyName("maximumNestingDepth")]
    public int MaximumNestingDepth { get; }

    [JsonPropertyName("maximumArtifactCount")]
    public int MaximumArtifactCount { get; }

    [JsonPropertyName("steps")]
    public IReadOnlyList<SemanticWorkflowPlanItem> Steps { get; }

    [JsonPropertyName("diagnostics")]
    public IReadOnlyList<ProtocolError> Diagnostics { get; }
}
