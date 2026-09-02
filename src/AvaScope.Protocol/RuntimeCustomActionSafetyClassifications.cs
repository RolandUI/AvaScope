namespace AvaScope.Protocol;

public static class RuntimeCustomActionSafetyClassifications
{
    public const string ReadOnly = "read_only";
    public const string NonDestructive = "non_destructive";
    public const string Destructive = "destructive";

    public static IReadOnlyList<string> All { get; } = [ReadOnly, NonDestructive, Destructive];
}
