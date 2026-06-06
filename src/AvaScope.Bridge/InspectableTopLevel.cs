using System.Runtime.CompilerServices;
using Avalonia.Controls;

namespace AvaScope.Bridge;

public sealed record InspectableTopLevel
{
    private InspectableTopLevel(
        string id,
        string kind,
        string? title,
        double width,
        double height,
        double renderScaling,
        bool isActive)
    {
        Id = id;
        Kind = kind;
        Title = title;
        Width = width;
        Height = height;
        RenderScaling = renderScaling;
        IsActive = isActive;
    }

    public string Id { get; }

    public string Kind { get; }

    public string? Title { get; }

    public double Width { get; }

    public double Height { get; }

    public double RenderScaling { get; }

    public bool IsActive { get; }

    internal static InspectableTopLevel FromWindow(Window window)
    {
        ArgumentNullException.ThrowIfNull(window);

        return new InspectableTopLevel(
            CreateId(window),
            "window",
            window.Title,
            window.ClientSize.Width,
            window.ClientSize.Height,
            window.RenderScaling,
            window.IsActive);
    }

    internal static InspectableTopLevel FromTopLevel(TopLevel topLevel, string kind)
    {
        ArgumentNullException.ThrowIfNull(topLevel);

        return new InspectableTopLevel(
            CreateId(topLevel),
            kind,
            null,
            topLevel.ClientSize.Width,
            topLevel.ClientSize.Height,
            topLevel.RenderScaling,
            false);
    }

    internal static string CreateId(TopLevel topLevel)
    {
        return $"topLevel:{GetRuntimeId(topLevel):x}";
    }

    internal static int GetRuntimeId(TopLevel topLevel)
    {
        return RuntimeHelpers.GetHashCode(topLevel);
    }
}
