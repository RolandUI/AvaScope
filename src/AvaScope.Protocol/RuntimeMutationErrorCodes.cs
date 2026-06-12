namespace AvaScope.Protocol;

public static class RuntimeMutationErrorCodes
{
    public const string InvalidRuntimeMutationRequest = "invalid_runtime_mutation_request";
    public const string InvalidRuntimeMutationValue = "invalid_runtime_mutation_value";
    public const string RuntimeMutationTargetStale = "runtime_mutation_target_stale";
    public const string RuntimeMutationNonLocalSession = "runtime_mutation_non_local_session";
    public const string RuntimeMutationCapabilityUnavailable = "runtime_mutation_capability_unavailable";
    public const string UnsupportedRuntimeMutationOperation = "unsupported_runtime_mutation_operation";
    public const string UnsupportedRuntimeMutationProperty = "unsupported_runtime_mutation_property";
}
