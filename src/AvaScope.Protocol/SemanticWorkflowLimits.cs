namespace AvaScope.Protocol;

public static class SemanticWorkflowLimits
{
    public const int MaximumNestingDepth = 8;
    public const int MaximumExpandedSteps = 256;
    public const int MaximumEstimatedExecutions = 512;
    public const int MaximumFragments = 32;
    public const int MaximumVariables = 64;
    public const int MaximumFragmentParameters = 16;
    public const int MaximumRetryAttempts = 10;
    public const int MaximumTotalRetryIterations = 64;
    public const int MaximumArtifacts = 64;
    public const int DefaultWorkflowTimeoutMs = 60000;
    public const int MaximumWorkflowTimeoutMs = 300000;
}
