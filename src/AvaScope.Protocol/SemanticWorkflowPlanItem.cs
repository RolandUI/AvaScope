using System.Text.Json.Serialization;

namespace AvaScope.Protocol;

public sealed record SemanticWorkflowPlanItem
{
    [JsonConstructor]
    public SemanticWorkflowPlanItem(
        int sequence,
        string executionPath,
        string stepId,
        string action,
        int nestingDepth,
        bool optional,
        string? parentStepId = null,
        string? branch = null,
        string? sourceFragment = null,
        int? maximumAttempts = null,
        string? topLevelAlias = null)
    {
        Sequence = sequence;
        ExecutionPath = executionPath;
        StepId = stepId;
        Action = action;
        NestingDepth = nestingDepth;
        Optional = optional;
        ParentStepId = parentStepId;
        Branch = branch;
        SourceFragment = sourceFragment;
        MaximumAttempts = maximumAttempts;
        TopLevelAlias = topLevelAlias;
    }

    [JsonPropertyName("sequence")]
    public int Sequence { get; }

    [JsonPropertyName("executionPath")]
    public string ExecutionPath { get; }

    [JsonPropertyName("stepId")]
    public string StepId { get; }

    [JsonPropertyName("action")]
    public string Action { get; }

    [JsonPropertyName("nestingDepth")]
    public int NestingDepth { get; }

    [JsonPropertyName("optional")]
    public bool Optional { get; }

    [JsonPropertyName("parentStepId")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ParentStepId { get; }

    [JsonPropertyName("branch")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Branch { get; }

    [JsonPropertyName("sourceFragment")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? SourceFragment { get; }

    [JsonPropertyName("maximumAttempts")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? MaximumAttempts { get; }

    [JsonPropertyName("topLevelAlias")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? TopLevelAlias { get; }
}
