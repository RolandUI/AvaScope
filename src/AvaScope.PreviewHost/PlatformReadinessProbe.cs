using System.Diagnostics;
using System.Runtime.InteropServices;
using AvaScope.Protocol;
using SkiaSharp;

namespace AvaScope.PreviewHost;

/// <summary>Executed only in a bounded child process; never loads host application code or the bridge.</summary>
internal static class PlatformReadinessProbe
{
    public static async Task<IReadOnlyList<DoctorCheck>> RunAsync(PlatformReadinessProbeRequest request)
    {
        var checks = new List<DoctorCheck>();
        void Check(string name, bool passed, string code, string message, string remediation, string stage = "platform")
            => checks.Add(new(name, passed ? DiagnosticStatuses.Available : DiagnosticStatuses.Unavailable, message,
                error: passed ? null : new ProtocolError(code, message), stage: stage, remediation: passed ? null : remediation));

        var nativeBackend = OperatingSystem.IsWindows() ? "win32" : OperatingSystem.IsMacOS() ? "macos" : "x11";
        if (request.Backend != "headless" && request.Backend != nativeBackend)
        {
            checks.Add(new("display_backend", "unsupported", "The requested display backend is not validated by this platform probe.",
                error: new("target_backend_unsupported", "Select a supported backend or its dedicated controlled-environment profile."), stage: "display"));
            return checks;
        }

        if (OperatingSystem.IsLinux() && request.Backend == "x11")
        {
            foreach (var library in new[] { "libX11.so.6", "libICE.so.6", "libSM.so.6", "libfontconfig.so.1" })
                Check(library, CanLoad(library), "target_native_dependency_missing", $"Native dependency: {library}.", "Install the corresponding distribution library package.", "dependencies");
            var display = Environment.GetEnvironmentVariable("DISPLAY");
            if (string.IsNullOrWhiteSpace(display))
                Check("x11_display", false, "target_display_missing", "DISPLAY is not configured for the selected launch environment.", "Select an authorized X11 display or an owned Xvfb test profile.", "display");
            else
            {
                var result = await ProbeCommandAsync("xdpyinfo", []);
                Check("x11_display", result == 0, result == -2 ? "target_display_probe_unavailable" : result == -3 ? "target_display_probe_timed_out" : "target_display_inaccessible",
                    result == 0 ? "X11 connectivity and display access were verified with xdpyinfo." : "The configured X11 display could not be verified.",
                    result == -2 ? "Install x11-utils (xdpyinfo)." : "Check the selected DISPLAY and XAUTHORITY permissions without changing global access controls.", "display");
            }
            checks.Add(new("x11_authorization", "information", "Authorization material is never included in probe output.", stage: "display",
                evidence: new Dictionary<string, string> { ["xauthorityConfigured"] = (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("XAUTHORITY"))).ToString().ToLowerInvariant() }));
            if (request.NativeDialogs)
            {
                var gtk = CanLoad("libgtk-3.so.0");
                var portal = await ProbeCommandAsync("gdbus", ["introspect", "--session", "--dest", "org.freedesktop.portal.Desktop", "--object-path", "/org/freedesktop/portal/desktop"]);
                Check("native_dialog_services", gtk || portal == 0, "target_dialog_service_unavailable",
                    gtk ? "GTK3 dialog dependency is available; actual host dialog behavior remains an integration check." : portal == 0 ? "The session desktop portal is reachable." : "Neither GTK3 nor a reachable desktop portal was found.",
                    "Select a supported GTK/portal dialog path with its session service, or use the declared deterministic picker fixture.", "desktop_services");
            }
        }
        else if (OperatingSystem.IsWindows() && request.Backend == "win32")
        {
            var desktop = OpenInputDesktop(0, false, 0x0001); // DESKTOP_READOBJECTS; never switches or mutates the desktop.
            Check("interactive_desktop", desktop != 0, "target_desktop_inaccessible", desktop != 0 ? "The input desktop is readable." : "The current input desktop is unavailable.", "Run the native UI profile in an interactive authorized user session.", "display");
            if (desktop != 0) CloseDesktop(desktop);
            if (request.NativeInput || request.NativeScreenshot)
                checks.Add(new("native_access", desktop != 0 ? "available" : "unavailable", "Windows native operations require an accessible user desktop; integrity-level compatibility is checked against the actual owned target at dispatch.", stage: "permissions"));
        }
        else if (OperatingSystem.IsMacOS() && request.Backend == "macos")
        {
            Check("appkit", CanLoad("/System/Library/Frameworks/AppKit.framework/AppKit"), "target_native_dependency_missing", "AppKit framework availability.", "Use a supported macOS desktop runtime.", "dependencies");
            if (request.NativeInput)
                Check("accessibility_permission", AXIsProcessTrusted(), "target_accessibility_permission_missing", "Accessibility trust was queried without prompting for the probe process.", "Authorize the executable that will perform the requested native automation; a different host process may have a different TCC identity.", "permissions");
            if (request.NativeScreenshot)
                Check("screen_capture_permission", CGPreflightScreenCaptureAccess(), "target_screen_capture_permission_missing", "Screen capture permission was queried without prompting for the probe process.", "Authorize screen recording for the executable performing native capture; Avalonia-rendered screenshots do not need this permission.", "permissions");
        }
        if (request.Backend == "headless" && (request.NativeInput || request.NativeScreenshot || request.NativeDialogs))
            checks.Add(new("native_operations", "unsupported", "The headless backend does not provide OS input, desktop capture or native dialogs.", error: new("target_native_operation_unsupported", "Choose a native desktop profile for the requested operation."), stage: "platform"));

        try
        {
            using var surface = SKSurface.Create(new SKImageInfo(8, 8));
            if (surface is null) throw new InvalidOperationException("Skia did not create a software rendering surface.");
            surface.Canvas.Clear(SKColors.Magenta);
            using var image = surface.Snapshot();
            using var pixels = SKBitmap.FromImage(image);
            Check("skia_renderer", pixels.GetPixel(0, 0) == SKColors.Magenta, "target_renderer_unavailable", "Native Skia software rendering and pixel readback were exercised.", "Check native Skia assets and architecture for the selected runtime.", "renderer");
            var fonts = SKFontManager.Default.FontFamilies.Take(64).Count();
            Check("fonts", fonts > 0, "target_fonts_missing", $"System font discovery returned {fonts} families (bounded at 64).", "Install system fonts and fontconfig on Linux, or restore the platform font service.", "fonts");
        }
        catch (Exception exception) when (exception is DllNotFoundException or EntryPointNotFoundException or TypeInitializationException or InvalidOperationException)
        {
            Check("skia_renderer", false, "target_renderer_unavailable", "The isolated native renderer/font probe failed.", "Verify Skia native dependencies, process architecture and font libraries.", "renderer");
        }
        return checks;
    }

    private static bool CanLoad(string name)
    {
        if (!NativeLibrary.TryLoad(name, out var handle)) return false;
        NativeLibrary.Free(handle);
        return true;
    }

    private static async Task<int> ProbeCommandAsync(string command, IReadOnlyList<string> arguments)
    {
        using var process = new Process { StartInfo = new ProcessStartInfo(command) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true } };
        foreach (var argument in arguments) process.StartInfo.ArgumentList.Add(argument);
        try
        {
            if (!process.Start()) return -2;
        }
        catch (System.ComponentModel.Win32Exception) { return -2; }
        // Drain to a sink: display/server diagnostics may contain authorization addresses or other sensitive data.
        var stdout = process.StandardOutput.BaseStream.CopyToAsync(Stream.Null);
        var stderr = process.StandardError.BaseStream.CopyToAsync(Stream.Null);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        try
        {
            await process.WaitForExitAsync(timeout.Token);
            await Task.WhenAll(stdout, stderr).WaitAsync(timeout.Token);
            return process.ExitCode;
        }
        catch (OperationCanceledException)
        {
            if (!process.HasExited) process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync();
            return -3;
        }
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern nint OpenInputDesktop(uint flags, [MarshalAs(UnmanagedType.Bool)] bool inherit, uint access);
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseDesktop(nint desktop);
    [DllImport("/System/Library/Frameworks/ApplicationServices.framework/ApplicationServices")]
    [return: MarshalAs(UnmanagedType.I1)]
    private static extern bool AXIsProcessTrusted();
    [DllImport("/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics")]
    [return: MarshalAs(UnmanagedType.I1)]
    private static extern bool CGPreflightScreenCaptureAccess();
}
