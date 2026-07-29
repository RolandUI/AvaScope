namespace AvaScope.Protocol;

public static class RuntimeMutationPropertyNames
{
    public static IReadOnlyList<string> All { get; } =
    [
        "width", "height", "minWidth", "minHeight", "maxWidth", "maxHeight",
        "margin", "padding", "opacity", "text", "content", "background",
        "foreground", "isEnabled", "isSelected", "isExpanded"
    ];

    public static bool IsSupported(string? value) =>
        !string.IsNullOrWhiteSpace(value)
        && All.Contains(value, StringComparer.OrdinalIgnoreCase);
}
