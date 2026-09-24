using System.Runtime.InteropServices;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using AvaScope.Core;
using AvaScope.Protocol;
using SkiaSharp;

namespace AvaScope.Bridge;

public sealed partial class AvaScopeBridgeRuntime
{
    private string? _nativeScreenScope;
    private long _nativeScreenScopeRevision;
    private readonly SemaphoreSlim _screenCaptureGate = new(1, 1);
    public string? NativeScreenCaptureScope => Volatile.Read(ref _nativeScreenScope);

    public void SetNativeScreenCaptureScope(string? scope)
    {
        Dispatcher.UIThread.VerifyAccess();
        if (scope is not null and not "declared_test_desktop") throw new ArgumentException("Use declared_test_desktop or null to revoke.", nameof(scope));
        if (_sessionRegistry.Get(SessionId).Value?.State is not SessionLifecycleState.Active)
            throw new InvalidOperationException("Desktop capture cannot be enabled on a closed session.");
        Volatile.Write(ref _nativeScreenScope, scope); Interlocked.Increment(ref _nativeScreenScopeRevision);
        foreach (var top in DiscoverTopLevels())
            if (FindTopLevel(top.Id) is { } window) _observedBackends[top.Id] = RuntimePlatformEvidence.Observe(window);
    }

    public async Task<CoreResult<RuntimeScreenCaptureResponse>> CaptureScreenAsync(RuntimeScreenCaptureRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (JsonSerializer.SerializeToUtf8Bytes(request).Length > 65536) return Fail("screen_capture_request_limit", "Capture requests are limited to 64 KiB.");
        if (request.Target.SessionId != SessionId) return Fail("screen_capture_session_mismatch", "Select this exact session.");
        var policy = request.Policy is null ? null : new RuntimeEvidencePolicyEnforcer(request.Policy);
        if (policy?.AuthorizeAction(SemanticWorkflowActions.Screenshot, null) is { Success: false } denied)
            return CoreResult<RuntimeScreenCaptureResponse>.Fail(denied.Error!);
        if (request.Policy is { } authorization && (authorization.AuthorizedSessionIds.Count > 0 && !authorization.AuthorizedSessionIds.Contains(SessionId.Value, StringComparer.Ordinal)
            || authorization.AuthorizedProcessIds.Count > 0 && !authorization.AuthorizedProcessIds.Contains(Environment.ProcessId)))
            return Fail("screen_capture_policy_denied", "The selected session/process is outside the evidence policy.");
        if (!await _screenCaptureGate.WaitAsync(0, cancellationToken)) return Fail("screen_capture_busy", "Another paired capture is active.");
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken); deadline.CancelAfter(request.TimeoutMs);
        RuntimeScreenFrame? rendered = null, native = null;
        var limitations = new List<string> { "sequential_samples_not_atomic", "pixel_differences_do_not_prove_a_defect_or_occlusion", "native_surfaces_and_popups_may_differ_from_rendered_tree", "cursor_and_protected_video_capture_not_guaranteed", "native_output_resampled_to_top_level_render_pixel_grid", "offscreen_or_monitor_gaps_are_transparent_missing_evidence" };
        Window? window = null; RuntimeBackendInfo? backend = null; RuntimeTargetContext? target = null;
        PixelSize pixels = default; NodeBounds? desktop = null; NodeBounds[] regions = []; string? identity = null, units = null;
        double scale = 1, desktopScale = 1, maximumNativeScale = 1; long scopeRevision = 0; nint handle = 0; string[] clearedHighlightTopLevels = []; var clearFrameConfirmed = false;
        try
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                window = FindTopLevel(request.Target.TopLevelId) as Window;
                if (window?.PlatformImpl is null) throw Stop("screen_capture_target_unavailable", "The selected registered desktop window is absent or closed.");
                if (CreateObjectGeneration(window) != request.Target.TopLevelGeneration) throw Stop("screen_capture_stale", "The selected window generation changed.");
                if (request.Policy is { } p && (p.ExcludedControlAutomationIds.Contains(GetAutomationId(window), StringComparer.Ordinal)
                    || p.RedactedAutomationIds.Contains(GetAutomationId(window), StringComparer.Ordinal))) throw Stop("screen_capture_excluded", "Evidence policy excludes the selected window.");
                clearedHighlightTopLevels = _highlights.Keys.ToArray();
                ClearHighlights();
                backend = RuntimePlatformEvidence.Observe(window); target = CreateTopLevelTarget(request.Target.TopLevelId, window);
                scale = window.RenderScaling; desktopScale = window.DesktopScaling;
                var width = Math.Ceiling(window.ClientSize.Width * scale); var height = Math.Ceiling(window.ClientSize.Height * scale);
                if (!RuntimeScreenCaptureLimits.AllowsDimensions(width, height))
                    throw Stop("screen_capture_pixel_limit", "Each image is limited to 8388608 pixels and 16384 pixels per dimension.");
                pixels = new((int)width, (int)height);
                identity = QueryIdentity(window, false); handle = window.TryGetPlatformHandle()?.Handle ?? 0;
                scopeRevision = Interlocked.Read(ref _nativeScreenScopeRevision);
                if (backend.Backend is "win32" or "x11" or "macos")
                {
                    var origin = window.PointToScreen(default); var end = window.PointToScreen(new(window.ClientSize.Width, window.ClientSize.Height));
                    desktop = new(origin.X, origin.Y, end.X - origin.X, end.Y - origin.Y);
                    if (desktop.Width <= 0 || desktop.Height <= 0) throw Stop("screen_capture_geometry_unavailable", "The client rectangle has no usable desktop mapping.");
                    units = backend.Backend == "macos" ? "cocoa_desktop_points" : "physical_desktop_pixels";
                    var screens = window.Screens.All.Take(17).ToArray();
                    if (screens.Length > 16) throw Stop("screen_capture_monitor_limit", "At most 16 monitors can be safely mapped.");
                    // Cocoa capture rectangles are points. Bound the possible backing image before asking the OS;
                    // keep the returned-dimension check too, because display configuration can change asynchronously.
                    if (backend.Backend == "macos") maximumNativeScale = screens.Select(screen => screen.Scaling).DefaultIfEmpty(double.NaN).Max();
                    regions = screens.Select(screen => IntersectScreen(desktop, new(screen.Bounds.X, screen.Bounds.Y, screen.Bounds.Width, screen.Bounds.Height)))
                        .Where(region => region.Width > 0 && region.Height > 0).Distinct().ToArray();
                }
            }, DispatcherPriority.Background, deadline.Token);

            if (request.Mode != "native")
            {
                var started = DateTimeOffset.UtcNow;
                try
                {
                    var ready = await ReadinessAsync(request.Target.TopLevelId, options: new(waitForFrame: true), cancellationToken: deadline.Token);
                    if (!ready.Success || ready.Value!.Frame.Status != "rendered" || !ready.Value.LayoutValid)
                        throw Stop("screen_render_not_ready", "No usable completed Avalonia frame was observed.");
                    clearFrameConfirmed = true;
                    rendered = await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        Check();
                        using var bitmap = new RenderTargetBitmap(pixels, new Vector(96 * scale, 96 * scale));
                        RenderRuntimeVisual(bitmap, window!.GetPresentationSource()?.RootVisual ?? window!);
                        using var stream = new MemoryStream(); bitmap.Save(stream, PngBitmapEncoderOptions.Default);
                        return Frame(RuntimeOperationRoutes.RenderTargetBitmap, stream.ToArray(), started, null, [new(0, 0, pixels.Width, pixels.Height)]);
                    }, DispatcherPriority.Background, deadline.Token);
                }
                catch (Exception exception) when (Recoverable(exception)) { rendered = Unavailable(RuntimeOperationRoutes.RenderTargetBitmap, started, exception); }
            }

            if (request.Mode != "rendered")
            {
                var started = DateTimeOffset.UtcNow; var route = NativeScreenCapture.Route(backend!.Backend);
                try
                {
                    foreach (var clearedTopLevel in clearedHighlightTopLevels)
                    {
                        if (clearedTopLevel == request.Target.TopLevelId && clearFrameConfirmed) continue;
                        var ready = await ReadinessAsync(clearedTopLevel, options: new(waitForFrame: true), cancellationToken: deadline.Token);
                        if (!ready.Success || ready.Value!.Frame.Status != "rendered") throw Stop("native_screen_highlight_clear_unconfirmed", "A frame after clearing debug highlights was not confirmed.");
                    }
                    await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        Check(); CheckScope();
                        if (backend.Backend is not ("win32" or "x11" or "macos")) throw Stop("native_screen_unsupported", "Headless/unknown backends have no native screen pixels.");
                        if (!window!.IsVisible || window.WindowState == WindowState.Minimized) throw Stop("native_screen_not_presented", "The selected window is hidden or minimized.");
                        NativeWindowInput.ValidateOwnership(window, false);
                        if (regions.Length == 0) throw Stop("native_screen_offscreen", "The client rectangle is entirely outside all observed displays.");
                    }, DispatcherPriority.Background, deadline.Token);
                    using var output = new SKBitmap(pixels.Width, pixels.Height, SKColorType.Bgra8888, SKAlphaType.Premul);
                    output.Erase(SKColors.Transparent); using var canvas = new SKCanvas(output);
                    var visible = new List<NodeBounds>(); var capturedRegions = new List<RuntimeScreenRegion>();
                    foreach (var region in regions)
                    {
                        // The macOS API is asynchronous. The UI dispatcher stays free while its callback is pending.
                        var operation = await Dispatcher.UIThread.InvokeAsync<Task<NativeScreenCapture.Pixels>>(() =>
                        { Check(); CheckScope(); return NativeScreenCapture.CaptureAsync(backend.Backend, region, maximumNativeScale); }, DispatcherPriority.Background, deadline.Token);
                        var captured = await operation.WaitAsync(deadline.Token);
                        using var bitmap = new SKBitmap(captured.Width, captured.Height, SKColorType.Bgra8888, SKAlphaType.Premul);
                        Marshal.Copy(captured.Bgra, 0, bitmap.GetPixels(), captured.Bgra.Length);
                        var imageRect = new NodeBounds((region.X - desktop!.X) * pixels.Width / desktop.Width, (region.Y - desktop.Y) * pixels.Height / desktop.Height,
                            region.Width * pixels.Width / desktop.Width, region.Height * pixels.Height / desktop.Height);
                        visible.Add(imageRect);
                        capturedRegions.Add(new(region, new(captured.Width, captured.Height), imageRect));
                        canvas.DrawBitmap(bitmap, new SKRect((float)imageRect.X, (float)imageRect.Y, (float)(imageRect.X + imageRect.Width), (float)(imageRect.Y + imageRect.Height)));
                    }
                    await Dispatcher.UIThread.InvokeAsync(() => { Check(); CheckScope(); }, DispatcherPriority.Background, deadline.Token);
                    canvas.Flush(); using var image = SKImage.FromBitmap(output); using var png = image.Encode(SKEncodedImageFormat.Png, 100);
                    native = Frame(route, png.ToArray(), started, capturedRegions.Count == 1 ? capturedRegions[0].NativePixelSize : null, visible)
                        with { NativeRegions = capturedRegions };
                    limitations.Add("desktop_content_within_client_rectangle_explicitly_authorized_by_host;isolation_not_inferred");
                }
                catch (Exception exception) when (Recoverable(exception)) { native = Unavailable(route, started, exception); }
            }
            var consistency = "no_sampled_geometry_change; content_and_presentation_not_atomic";
            try { await Dispatcher.UIThread.InvokeAsync(() => { Check(); if (native?.Status == "captured") CheckScope(); }, DispatcherPriority.Background, deadline.Token); }
            catch (Exception exception) when (Recoverable(exception))
            {
                consistency = "target_or_geometry_changed; comparison_unavailable";
                // A revoked scope or changed target cannot expose a capture completed under the old permission/identity.
                if (native is not null) native = Unavailable(native.Source, native.StartedAt, exception);
            }
            var response = new RuntimeScreenCaptureResponse(
                (rendered is null || rendered.Status == "captured") && (native is null || native.Status == "captured") ? "captured" : "partial",
                target!, backend!, scale, desktopScale, consistency, rendered, native, null, limitations);
            // PNGs are already masked; generic JSON text redaction must never rewrite their base64 strings.
            if (policy is not null)
            {
                var sanitized = policy.Sanitize(response with { Rendered = rendered is null ? null : rendered with { Png = null }, Native = native is null ? null : native with { Png = null } });
                if (!sanitized.Success) return sanitized;
                response = sanitized.Value! with { Rendered = sanitized.Value.Rendered is null ? null : sanitized.Value.Rendered with { Png = rendered?.Png },
                    Native = sanitized.Value.Native is null ? null : sanitized.Value.Native with { Png = native?.Png } };
            }
            return CoreResult<RuntimeScreenCaptureResponse>.Ok(response);
        }
        catch (Exception exception) when (Recoverable(exception))
        { var error = ScreenError(exception); return Fail(error.Code, error.Message); }
        finally { _screenCaptureGate.Release(); }

        void Check()
        {
            deadline.Token.ThrowIfCancellationRequested();
            if (_sessionRegistry.Get(SessionId).Value?.State is not SessionLifecycleState.Active
                || window is null || FindTopLevel(request.Target.TopLevelId) != window || window.PlatformImpl is null
                || CreateObjectGeneration(window) != request.Target.TopLevelGeneration || QueryIdentity(window, false) != identity
                || (window.TryGetPlatformHandle()?.Handle ?? 0) != handle || GetPixelSize(window) != pixels
                || window.RenderScaling != scale || window.DesktopScaling != desktopScale)
                throw Stop("screen_capture_stale", "The session, target identity or capture geometry changed; no native image is exported.");
            if (desktop is not null)
            {
                var origin = window.PointToScreen(default); var end = window.PointToScreen(new(window.ClientSize.Width, window.ClientSize.Height));
                if (origin.X != desktop.X || origin.Y != desktop.Y || end.X - origin.X != desktop.Width || end.Y - origin.Y != desktop.Height)
                    throw Stop("screen_capture_geometry_changed", "The window moved or resized during the samples.");
            }
        }
        void CheckScope()
        {
            if (request.DesktopScope != "declared_test_desktop" || NativeScreenCaptureScope != request.DesktopScope
                || Interlocked.Read(ref _nativeScreenScopeRevision) != scopeRevision)
                throw Stop("native_screen_scope_denied", "The host and this request must explicitly authorize declared_test_desktop. Use an isolated test display; unrelated desktop pixels are otherwise refused.");
            if (request.Policy is { AllowNativeScreenCapture: false }) throw Stop("native_screen_policy_denied", "The evidence policy does not allow native desktop pixels.");
        }
        RuntimeScreenFrame Frame(string source, byte[] png, DateTimeOffset started, RuntimeSize? nativeSize, IReadOnlyList<NodeBounds> visible)
        {
            deadline.Token.ThrowIfCancellationRequested();
            var masking = "not_required";
            if (policy is not null) (png, masking) = policy.MaskScreenshotPng(png, new(SessionId, request.Target.TopLevelId, Path.Combine(request.OutputDirectory, "in-memory.png"), pixels.Width, pixels.Height, DateTimeOffset.UtcNow));
            deadline.Token.ThrowIfCancellationRequested();
            if (png.Length > RuntimeScreenCaptureLimits.MaximumPngBytes) throw Stop("screen_capture_byte_limit", "Each masked PNG is limited to 256 KiB; use the rendered screenshot tool for larger encoded evidence.");
            return new(source, "captured", pixels.Width, pixels.Height, started, DateTimeOffset.UtcNow, desktop, units, nativeSize, visible, masking, null, [], png);
        }
        RuntimeScreenFrame Unavailable(string source, DateTimeOffset started, Exception exception) =>
            new(source, "unavailable", pixels.Width, pixels.Height, started, DateTimeOffset.UtcNow, desktop, units, null, [], "not_exposed", null, [ScreenError(exception)]);
        static CoreResult<RuntimeScreenCaptureResponse> Fail(string code, string message) => CoreResult<RuntimeScreenCaptureResponse>.Fail(new(code, message));
    }

    private static NativeScreenCapture.CaptureException Stop(string code, string message) => new(code, message);
    private static bool Recoverable(Exception exception) => exception is not OutOfMemoryException and not AccessViolationException;
    private static ProtocolError ScreenError(Exception exception) => exception is NativeScreenCapture.CaptureException known ? new(known.Code, known.Message)
        : exception is OperationCanceledException ? new("screen_capture_timeout", "The bounded capture ended before all requested evidence was available.")
        : exception is NotSupportedException or DllNotFoundException or EntryPointNotFoundException ? new("native_screen_unsupported", "The selected native capture backend or API is unavailable.")
        : new("screen_capture_failed", "Capture failed; no unmasked image or private backend error is exposed.");
    private static NodeBounds IntersectScreen(NodeBounds a, NodeBounds b)
    {
        var x = Math.Max(a.X, b.X); var y = Math.Max(a.Y, b.Y);
        return new(x, y, Math.Max(0, Math.Min(a.X + a.Width, b.X + b.Width) - x), Math.Max(0, Math.Min(a.Y + a.Height, b.Y + b.Height) - y));
    }
}
