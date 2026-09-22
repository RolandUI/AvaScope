using System.Text.Json.Serialization;

namespace AvaScope.Protocol;

public sealed record RuntimeBackendInfo(
    [property: JsonPropertyName("backend")] string Backend,
    [property: JsonPropertyName("platform")] string Platform,
    [property: JsonPropertyName("evidenceSource")] string EvidenceSource,
    [property: JsonPropertyName("implementationType")] string? ImplementationType,
    [property: JsonPropertyName("handleDescriptor")] string? HandleDescriptor,
    [property: JsonPropertyName("renderMode")] string RenderMode,
    [property: JsonPropertyName("inputRoutes")] IReadOnlyList<string> InputRoutes,
    [property: JsonPropertyName("screenshotRoutes")] IReadOnlyList<string> ScreenshotRoutes,
    [property: JsonPropertyName("restrictions")] IReadOnlyList<string> Restrictions);

public sealed record RuntimeOperationProvenance(
    [property: JsonPropertyName("route")] string Route,
    [property: JsonPropertyName("backend")] RuntimeBackendInfo Backend,
    [property: JsonPropertyName("dispatched")] bool Dispatched,
    [property: JsonPropertyName("fallback")] bool Fallback = false,
    [property: JsonPropertyName("coordinateSpace")] string? CoordinateSpace = null,
    [property: JsonPropertyName("renderScaling")] double? RenderScaling = null,
    [property: JsonPropertyName("plannedRoute")] string? PlannedRoute = null,
    [property: JsonPropertyName("screenOriginX")] double? ScreenOriginX = null,
    [property: JsonPropertyName("screenOriginY")] double? ScreenOriginY = null);

public static class RuntimeOperationRoutes
{
    public const string AutomationProvider = "avalonia_automation_provider";
    public const string SyntheticPointer = "avalonia_synthetic_pointer";
    public const string SyntheticKey = "avalonia_synthetic_key";
    public const string RoutedEvent = "avalonia_routed_event";
    public const string ControlProperty = "avalonia_control_property";
    public const string Focus = "avalonia_focus_api";
    public const string Win32WindowMessage = "win32_owned_window_message";
    public const string X11WindowEvent = "x11_owned_window_event";
    public const string AppKitWindowEvent = "appkit_owned_window_event";
    public const string RenderTargetBitmap = "avalonia_render_target_bitmap";
    public const string NotDispatched = "not_dispatched";
}
