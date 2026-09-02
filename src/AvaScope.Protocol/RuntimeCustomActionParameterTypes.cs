namespace AvaScope.Protocol;

public static class RuntimeCustomActionParameterTypes
{
    public const string String = "string";
    public const string Boolean = "boolean";
    public const string Integer = "integer";
    public const string Number = "number";

    public static IReadOnlyList<string> All { get; } = [String, Boolean, Integer, Number];
}
