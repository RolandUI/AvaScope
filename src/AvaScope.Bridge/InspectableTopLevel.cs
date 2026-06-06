using System.Runtime.CompilerServices;
using Avalonia.Controls;
using AvaScope.Protocol;

namespace AvaScope.Bridge;

internal static class InspectableTopLevel
{
    internal static TopLevelSummary FromWindow(Window window)
    {
        ArgumentNullException.ThrowIfNull(window);

        return new TopLevelSummary(
            CreateId(window),
            "window",
            window.Title,
            window.ClientSize.Width,
            window.ClientSize.Height,
            window.RenderScaling,
            window.IsActive);
    }

    internal static TopLevelSummary FromTopLevel(TopLevel topLevel, string kind)
    {
        ArgumentNullException.ThrowIfNull(topLevel);

        return new TopLevelSummary(
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
