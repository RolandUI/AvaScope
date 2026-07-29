namespace AvaScope.Protocol;

public static class RuntimeMutationStatuses
{
    public const string Applied = "applied";
    public const string NoOp = "no_op";
    public const string Validated = "validated";
    public const string Rejected = "rejected";
    public const string Unsupported = "unsupported";
    public const string StaleTarget = "stale_target";
    public const string Unavailable = "unavailable";
}
