namespace AvaScope.Protocol;

public static class RuntimeCustomActionErrorCodes
{
    public const string Disabled = "runtime_custom_actions_disabled";
    public const string InvalidRequest = "invalid_runtime_custom_action_request";
    public const string TargetStale = "runtime_custom_action_target_stale";
    public const string UnknownAction = "runtime_custom_action_unknown";
    public const string Unavailable = "runtime_custom_action_unavailable";
    public const string NonExecutable = "runtime_custom_action_non_executable";
    public const string Disallowed = "runtime_custom_action_disallowed";
    public const string InvalidParameters = "runtime_custom_action_invalid_parameters";
    public const string ExecutionFailed = "runtime_custom_action_execution_failed";
}
