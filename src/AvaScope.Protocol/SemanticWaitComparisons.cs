namespace AvaScope.Protocol;

public static class SemanticWaitComparisons
{
    public const string Equal = "equals";
    public const string NotEquals = "not_equals";
    public const string GreaterThan = "greater_than";
    public const string GreaterThanOrEqual = "greater_than_or_equal";
    public const string LessThan = "less_than";
    public const string LessThanOrEqual = "less_than_or_equal";
    public const string Changed = "changed";

    public static IReadOnlyList<string> All { get; } =
    [
        Equal, NotEquals, GreaterThan, GreaterThanOrEqual,
        LessThan, LessThanOrEqual, Changed
    ];
}
