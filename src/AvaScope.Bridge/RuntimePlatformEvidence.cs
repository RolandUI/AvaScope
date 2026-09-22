using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using AvaScope.Protocol;

namespace AvaScope.Bridge;

internal static class RuntimePlatformEvidence
{
    internal static RuntimeBackendInfo Observe(TopLevel topLevel)
    {
        Dispatcher.UIThread.VerifyAccess();
        var implementation = topLevel.PlatformImpl?.GetType();
        var assembly = implementation?.Assembly.GetName().Name;
        var descriptor = topLevel.TryGetPlatformHandle()?.HandleDescriptor;
        var backend = assembly switch
        {
            "Avalonia.Headless" => "headless",
            "Avalonia.X11" => "x11",
            "Avalonia.Win32" => "win32",
            "Avalonia.Native" when OperatingSystem.IsMacOS() => "macos",
            _ => "unknown"
        };
        var restrictions = new List<string>
        {
            "native_os_input_unavailable",
            "native_screen_capture_unavailable",
            "render_mode_unknown",
            "automation_provider_support_depends_on_target",
            "render_target_bitmap_excludes_native_chrome_and_external_surfaces"
        };
        if (backend == "headless")
        {
            restrictions.Add("headless_does_not_validate_native_desktop_integration");
        }
        if (backend == "unknown")
        {
            restrictions.Add("platform_implementation_unrecognized_or_unavailable");
        }

        return new RuntimeBackendInfo(
            backend,
            OperatingSystem.IsWindows() ? "windows" : OperatingSystem.IsLinux() ? "linux"
                : OperatingSystem.IsMacOS() ? "macos" : "unknown",
            "top_level_platform_implementation",
            implementation?.FullName,
            descriptor,
            "unknown",
            [RuntimeOperationRoutes.AutomationProvider, RuntimeOperationRoutes.SyntheticPointer,
                RuntimeOperationRoutes.SyntheticKey, RuntimeOperationRoutes.RoutedEvent,
                RuntimeOperationRoutes.ControlProperty, RuntimeOperationRoutes.Focus],
            [RuntimeOperationRoutes.RenderTargetBitmap],
            restrictions);
    }

    internal static RuntimeOperationProvenance Operation(
        TopLevel topLevel,
        string route,
        bool dispatched = true,
        bool fallback = false,
        string? coordinateSpace = null)
    {
        var backend = Observe(topLevel);
        PixelPoint? origin = null;
        if (backend.Backend is "x11" or "win32" or "macos"
            && topLevel.TryGetPlatformHandle()?.Handle is { } handle && handle != IntPtr.Zero)
        {
            try
            {
                origin = topLevel.PointToScreen(default);
            }
            catch (Exception exception) when (exception is NotSupportedException or InvalidOperationException)
            {
                // Missing public screen-coordinate support must not break the original operation.
            }
        }

        var scaling = topLevel.RenderScaling;
        return new RuntimeOperationProvenance(
            dispatched ? route : RuntimeOperationRoutes.NotDispatched,
            backend,
            dispatched,
            dispatched && fallback,
            coordinateSpace,
            double.IsFinite(scaling) && scaling > 0 ? scaling : null,
            dispatched ? null : route,
            origin?.X,
            origin?.Y);
    }
}
