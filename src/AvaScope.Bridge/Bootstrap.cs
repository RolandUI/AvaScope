using System.Reflection;
using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;

namespace AvaScope.Bridge;

/// <summary>The stable, explicit entry point for externally supplied bridge providers.</summary>
public static class Bootstrap
{
    /// <summary>
    /// Starts the local bridge on the application's UI thread and returns its session id.
    /// Call after Avalonia initializes its application lifetime. Repeated calls reuse the session.
    /// Loading this assembly or resolving this method never activates the bridge.
    /// </summary>
    public static string Start()
    {
        if (!RuntimeFeature.IsDynamicCodeSupported || Environment.Version.Major != 10)
        {
            throw new NotSupportedException(
                "AVASCOPE_RUNTIME_INCOMPATIBLE: The standalone provider requires an untrimmed .NET 10 desktop application with dynamic assembly loading; NativeAOT is unsupported.");
        }

        return StartAvalonia();
    }

    /// <summary>Closes the active bridge and removes its owned registrations and local resources.</summary>
    public static void Stop() => AvaScopeBridge.Deactivate();

    /// <summary>
    /// Host-only opt-in for pixels on a declared test desktop. Pass null to revoke.
    /// The host authorizes all content within its window's screen rectangle, including occlusion.
    /// Prefer an isolated test display; this hook is deliberately absent from IPC/MCP.
    /// </summary>
    public static void SetNativeScreenCaptureScope(string? scope) =>
        (AvaScopeBridge.Current ?? throw new InvalidOperationException("AVASCOPE_NOT_ACTIVE: Start explicitly before authorizing desktop pixels."))
            .SetNativeScreenCaptureScope(scope);

    /// <summary>Optionally declares host-owned starting, busy, ready or failed state on the UI thread.</summary>
    public static void SetReadiness(string state, string? reason) =>
        (AvaScopeBridge.Current ?? throw new InvalidOperationException("AVASCOPE_NOT_ACTIVE: Start the bridge explicitly before declaring readiness."))
            .SetReadiness(state, reason);

    /// <summary>
    /// Explicit host opt-in for a one-shot, correlated picker result. Returns JSON using only BCL types.
    /// The host decides how to apply the result or open its real picker when status is not_prepared.
    /// </summary>
    public static string TakePreparedPickerResult(string correlationId)
    {
        if (string.IsNullOrWhiteSpace(correlationId) || correlationId.Length > 128)
            throw new ArgumentException("A nonempty picker correlation id of at most 128 characters is required.", nameof(correlationId));
        var runtime = AvaScopeBridge.Current ?? throw new InvalidOperationException("AVASCOPE_NOT_ACTIVE: Start the bridge explicitly before consuming a picker result.");
        var result = new AvaScope.Core.LocalBridgeClient(Path.GetDirectoryName(runtime.SessionManifestPath)!)
            .NativePicker(runtime.SessionId, AvaScope.Protocol.NativePickerOperations.ConsumePredefinedResult,
                correlationId: correlationId, redactPath: false);
        if (!result.Success) throw new InvalidOperationException(result.Error!.Message);
        return System.Text.Json.JsonSerializer.Serialize(result.Value);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static string StartAvalonia()
    {
        var versionText = typeof(Application).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        if (!Version.TryParse(versionText?.Split('+', '-')[0], out var version)
            || version.Major != 12 || version.Minor != 1)
        {
            throw new NotSupportedException(
                $"AVASCOPE_AVALONIA_INCOMPATIBLE: This provider supports Avalonia 12.1.x; the host reports '{versionText ?? "unknown"}'. Use a matching provider without loading a second Avalonia runtime.");
        }

        if (!Dispatcher.UIThread.CheckAccess())
        {
            throw new InvalidOperationException(
                "AVASCOPE_UI_THREAD_REQUIRED: Invoke Bootstrap.Start() on Dispatcher.UIThread after application initialization.");
        }

        var lifetime = Application.Current?.ApplicationLifetime;
        if (lifetime is not IClassicDesktopStyleApplicationLifetime and not ISingleViewApplicationLifetime)
        {
            throw new NotSupportedException(
                "AVASCOPE_LIFETIME_UNAVAILABLE: Initialize a classic desktop or single-view application lifetime before invoking Bootstrap.Start().");
        }

        var alreadyActive = AvaScopeBridge.IsActive;
        var runtime = AvaScopeBridge.Activate();
        try
        {
            runtime.EnableAutomaticTopLevels(lifetime);
            return runtime.SessionId.Value;
        }
        catch
        {
            if (!alreadyActive)
            {
                AvaScopeBridge.Deactivate();
            }

            throw;
        }
    }
}
