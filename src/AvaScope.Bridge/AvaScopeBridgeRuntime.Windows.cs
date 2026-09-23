using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using AvaScope.Core;
using AvaScope.Protocol;

namespace AvaScope.Bridge;

public sealed partial class AvaScopeBridgeRuntime
{
    public async Task<CoreResult<RuntimeWindowResponse>> WindowAsync(RuntimeWindowRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (JsonSerializer.SerializeToUtf8Bytes(request).Length > 65536) return Fail("window_request_limit", "Window requests are limited to 64 KiB.");
        if (request.Target.SessionId != SessionId) return Fail("window_session_mismatch", "Select a window in this exact bridge session.");
        var policy = request.Policy is null ? null : new RuntimeEvidencePolicyEnforcer(request.Policy);
        if (policy?.AuthorizeAction(SemanticWorkflowActions.Inspect, null) is { Success: false } denied)
            return CoreResult<RuntimeWindowResponse>.Fail(denied.Error!);
        if (request.Policy is { } authorization && (authorization.AuthorizedSessionIds.Count > 0 && !authorization.AuthorizedSessionIds.Contains(SessionId.Value, StringComparer.Ordinal)
            || authorization.AuthorizedProcessIds.Count > 0 && !authorization.AuthorizedProcessIds.Contains(Environment.ProcessId)))
            return Fail("window_policy_denied", "The current session/process is outside the evidence policy.");
        if (request.Action != "inspect" && request.Policy is { } actionPolicy && !actionPolicy.AllowedWindowActions.Contains(request.Action, StringComparer.Ordinal))
            return Fail("window_policy_denied", "Explicitly allow this window action in allowedWindowActions.");
        var mutate = request.Action != "inspect";
        if (mutate && !await _explicitInputGate.WaitAsync(0, cancellationToken)) return Fail("window_busy", "Another compound input operation is active.");
        RuntimeWindowSnapshot? before = null, after = null;
        RuntimeOperationProvenance? provenance = null;
        Window? window = null;
        RuntimeWindowPosition? position = null;
        nint handle = 0;
        string? identity = null;
        var dispatched = 0;
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(request.TimeoutMs + 700);
        var elapsed = Stopwatch.StartNew();
        try
        {
            var immediate = await Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (FindTopLevel(request.Target.TopLevelId) is not Window { PlatformImpl: not null } selected)
                    throw new WindowStop("window_unavailable", "The selected registered top-level is absent or is not a desktop Window.");
                window = selected;
                before = CaptureWindow(window, request.Policy);
                after = before;
                provenance = RuntimePlatformEvidence.Operation(window, RuntimeOperationRoutes.WindowApi, dispatched: false, coordinateSpace: before.DesktopUnits + ";client_DIP");
                if (request.Target.TopLevelGeneration is { } expectedGeneration && before.Target.TopLevelGeneration != expectedGeneration
                    || request.ExpectedRevision is { } expectedRevision && before.Revision != expectedRevision)
                    throw new WindowStop("window_stale", "Window identity, state, geometry, constraints or monitor layout changed; inspect it again.");
                if (!mutate) return "observed";
                if (FindModalBlocker(window) is not null || window.OwnedWindows.Count > 64)
                    throw new WindowStop("window_modal_blocked", "A modal child or incomplete ownership scope blocks changing this owner. Select the dialog explicitly.");
                if (!before.AvailableActions.Contains(request.Action, StringComparer.Ordinal))
                    throw new WindowStop("window_action_unsupported", "This window state, backend or app constraints do not support the requested action.");
                NativeWindowInput.ValidateOwnership(window, false);
                handle = window.TryGetPlatformHandle()!.Handle;
                identity = QueryIdentity(window, false);
                if (request.Position is { } requestedPosition)
                {
                    var resolved = RuntimeWindowGeometry.ResolvePosition(requestedPosition, before.Monitors);
                    if (!resolved.Success) throw new WindowStop(resolved.Error!.Code, resolved.Error.Message);
                    position = resolved.Value;
                }
                if (request.ClientSize is { } size && (size.Width < window.MinWidth || size.Width > window.MaxWidth || size.Height < window.MinHeight || size.Height > window.MaxHeight))
                    throw new WindowStop("window_size_constraints", "The requested client size violates the application's current min/max dimensions.");
                if (Satisfied(before)) return "already_satisfied";
                Check();
                switch (request.Action)
                {
                    case "activate": case "bring_to_front": Dispatch(window.Activate); break;
                    case "minimize": Dispatch(() => window.WindowState = WindowState.Minimized); break;
                    case "maximize": Dispatch(() => window.WindowState = WindowState.Maximized); break;
                    case "restore": Dispatch(() => window.WindowState = WindowState.Normal); break;
                    case "move": Dispatch(() => window.Position = new PixelPoint(checked((int)position!.X), checked((int)position.Y))); break;
                    case "resize":
                        Dispatch(() => window.SetCurrentValue(Window.WidthProperty, request.ClientSize!.Width));
                        Dispatch(() => window.SetCurrentValue(Window.HeightProperty, request.ClientSize!.Height));
                        break;
                }
                after = CaptureWindow(window, request.Policy);
                return (string?)null;
            }, DispatcherPriority.Background, deadline.Token);
            if (immediate is not null) return Result(immediate, "observed_owned_window_state", []);
            var consecutive = 0;
            while (elapsed.ElapsedMilliseconds < request.TimeoutMs)
            {
                await Task.Delay(50, deadline.Token);
                var matched = await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    Check(); after = CaptureWindow(window!, request.Policy); return Satisfied(after);
                }, DispatcherPriority.Background, deadline.Token);
                consecutive = matched ? consecutive + 1 : 0;
                if (consecutive >= 2) return Result("executed", Verification(), []);
            }
            return Result("partial", "requested_postcondition_not_confirmed", [new("window_manager_refused_or_unconfirmed", "The bounded observations did not confirm the requested result; window-manager policy or app handlers may have refused it. Inspect the actual after state.")]);
        }
        catch (WindowStop exception) { return Result(dispatched > 0 ? "partial" : "rejected", "not_confirmed", [new(exception.Code, exception.Message)]); }
        catch (OperationCanceledException)
        {
            if (dispatched == 0 && cancellationToken.IsCancellationRequested) throw;
            return Result(dispatched > 0 ? "partial" : "rejected", "observation_cancelled_or_timed_out", [new("window_timeout", "The bounded operation ended without confirmed final state. Reinspect before a new request; no rollback or replay was attempted.")]);
        }
        catch (Exception exception) when (exception is not OutOfMemoryException and not AccessViolationException)
        { return Result(dispatched > 0 ? "partial" : "rejected", "not_confirmed", [new("window_operation_failed", "The public window/backend operation failed. No unrelated window or private exception text was exposed.")]); }
        finally { if (mutate) _explicitInputGate.Release(); }

        void Check()
        {
            if (window is null || FindTopLevel(request.Target.TopLevelId) != window || window.TryGetPlatformHandle()?.Handle != handle
                || CreateObjectGeneration(window) != request.Target.TopLevelGeneration || QueryIdentity(window, false) != identity)
                throw new WindowStop("window_changed_during_operation", "The original window closed, unregistered or changed identity; no further operation was dispatched.");
            if (FindModalBlocker(window) is not null || window.OwnedWindows.Count > 64)
                throw new WindowStop("window_modal_blocked", "A modal child appeared during the operation; no further action was dispatched.");
            if (request.Action == "resize" && (!window.CanResize || window.SizeToContent != SizeToContent.Manual
                || request.ClientSize!.Width < window.MinWidth || request.ClientSize.Width > window.MaxWidth
                || request.ClientSize.Height < window.MinHeight || request.ClientSize.Height > window.MaxHeight))
                throw new WindowStop("window_constraints_changed", "Application resize constraints changed; no further size property was dispatched.");
            NativeWindowInput.ValidateOwnership(window, false);
        }
        void Dispatch(Action operation)
        {
            Check(); deadline.Token.ThrowIfCancellationRequested(); dispatched++;
            after = null;
            provenance = RuntimePlatformEvidence.Operation(window!, RuntimeOperationRoutes.WindowApi, coordinateSpace: before!.DesktopUnits + ";client_DIP");
            operation();
        }
        bool Satisfied(RuntimeWindowSnapshot state) => request.Action switch
        {
            "activate" => state.Active && state.NativeFocus.State == "focused",
            "bring_to_front" => state.Active && state.NativeFocus.State == "focused" && state.FrontmostRegisteredWindow == true,
            "minimize" => state.State == "minimized" && state.NativeState.State == "minimized",
            "maximize" => state.State == "maximized" && state.NativeState.State == "maximized",
            "restore" => state.State == "normal" && state.NativeState.State == "normal",
            "move" => state.State == "normal" && state.NativeState.State == "normal" && state.Position.X == position?.X && state.Position.Y == position?.Y,
            "resize" => state.State == "normal" && state.NativeState.State == "normal" && Math.Abs(state.ClientSize.Width - request.ClientSize!.Width) <= 1 && Math.Abs(state.ClientSize.Height - request.ClientSize.Height) <= 1,
            _ => false
        };
        string Verification() => request.Action switch
        {
            "activate" => "framework_active_and_owned_native_focus",
            "bring_to_front" => "native_focus_and_frontmost_registered_window; unrelated_window_occlusion_unverified",
            "minimize" or "maximize" or "restore" => "framework_and_owned_native_window_state",
            _ => "public_platform_geometry_observed_twice; client_size_tolerance_1_DIP"
        };
        CoreResult<RuntimeWindowResponse> Result(string status, string verification, IReadOnlyList<ProtocolError> diagnostics)
        {
            var response = new RuntimeWindowResponse(status, request.Action, before, after, dispatched, provenance, verification, diagnostics);
            return policy is null ? CoreResult<RuntimeWindowResponse>.Ok(response) : policy.Sanitize(response);
        }
        static CoreResult<RuntimeWindowResponse> Fail(string code, string message) => CoreResult<RuntimeWindowResponse>.Fail(new(code, message));
    }

    private RuntimeWindowSnapshot CaptureWindow(Window window, RuntimeEvidencePolicy? options)
    {
        Dispatcher.UIThread.VerifyAccess();
        if (options is not null && (options.ExcludedControlAutomationIds.Contains(GetAutomationId(window), StringComparer.Ordinal)
            || options.RedactedAutomationIds.Contains(GetAutomationId(window), StringComparer.Ordinal)))
            throw new WindowStop("window_excluded", "The selected window is protected by evidence policy.");
        var policy = options is null ? null : new RuntimeEvidencePolicyEnforcer(options);
        var backend = RuntimePlatformEvidence.Observe(window);
        var supported = backend.Backend is "win32" or "x11" or "macos";
        var units = backend.Backend == "macos" ? "cocoa_desktop_points" : supported ? "physical_desktop_pixels" : "backend_desktop_units_unverified";
        var gaps = new List<string> { "unrelated_window_occlusion_not_observed", "fullscreen_transitions_unsupported" };
        var monitors = new List<RuntimeMonitorInfo>();
        try
        {
            var screens = window.Screens.All.Take(17).ToArray();
            if (screens.Length > 16) gaps.Add("monitor_limit");
            foreach (var screen in screens.Take(16))
            {
                if (!double.IsFinite(screen.Scaling) || screen.Scaling <= 0) { gaps.Add("monitor_scale_unavailable"); continue; }
                var bounds = new NodeBounds(screen.Bounds.X, screen.Bounds.Y, screen.Bounds.Width, screen.Bounds.Height);
                var work = new NodeBounds(screen.WorkingArea.X, screen.WorkingArea.Y, screen.WorkingArea.Width, screen.WorkingArea.Height);
                var id = WindowHash(new { screen.DisplayName, screen.Bounds, screen.WorkingArea, screen.Scaling, Index = monitors.Count });
                monitors.Add(new(id, Clip(screen.DisplayName), bounds, work, units, screen.Scaling,
                    new(bounds.Width / screen.Scaling, bounds.Height / screen.Scaling), units == "physical_desktop_pixels" ? bounds : null, screen.IsPrimary));
            }
        }
        catch (Exception exception) when (exception is NotSupportedException or InvalidOperationException) { gaps.Add("monitors_unavailable"); }
        if (backend.Backend == "macos") gaps.Add("monitor_physical_pixel_bounds_unavailable; desktop_points_differ_from_render_pixels");
        bool? front = null;
        if (supported)
        {
            var owned = false;
            try
            {
                NativeWindowInput.ValidateOwnership(window, false);
                owned = true;
                var windows = DiscoverTopLevels().Select(summary => FindTopLevel(summary.Id)).OfType<Window>().Where(top => top.IsVisible && top.WindowState != WindowState.Minimized).Take(33).ToArray();
                if (windows.Length > 32) gaps.Add("registered_z_order_limit");
                else if (windows.Contains(window)) { Window.SortWindowsByZOrder(windows); front = windows.LastOrDefault() == window; }
            }
            catch (Exception exception) when (exception is NotSupportedException or InvalidOperationException or DllNotFoundException or EntryPointNotFoundException)
            { gaps.Add("native_ownership_or_z_order_unavailable"); supported = owned; }
        }
        else gaps.Add("native_window_management_unsupported");
        var modal = FindModalBlocker(window);
        var nativeState = NativeWindowInput.ObserveWindowState(window);
        var nativeFocus = NativeWindowInput.ObserveFocus(window);
        var actions = new List<string> { "inspect" };
        var normal = window.WindowState == WindowState.Normal && nativeState.State == "normal";
        if (supported && window.IsVisible && modal is null && window.OwnedWindows.Count <= 64 && window.WindowState != WindowState.FullScreen)
        {
            actions.Add("activate");
            if (front is not null || window.WindowState == WindowState.Minimized) actions.Add("bring_to_front");
            if (nativeState.State is "normal" or "minimized" or "maximized")
            {
                actions.Add("restore");
                if (window.CanMinimize) actions.Add("minimize");
                if (window.CanMaximize) actions.Add("maximize");
            }
            else gaps.Add("native_window_state_unavailable");
            if (normal && monitors.Count > 0) actions.Add("move");
            if (normal && window.CanResize && window.SizeToContent == SizeToContent.Manual) actions.Add("resize");
        }
        var target = WindowTarget(window);
        var owner = window.Owner is Window parent && FindTopLevel(InspectableTopLevel.CreateId(parent)) == parent ? WindowTarget(parent) : null;
        var blocker = modal is not null && FindTopLevel(InspectableTopLevel.CreateId(modal)) == modal ? WindowTarget(modal) : null;
        if (modal is not null && blocker is null) gaps.Add("unregistered_modal_child");
        if (options is not null) actions.RemoveAll(action => action != "inspect" && !options.AllowedWindowActions.Contains(action, StringComparer.Ordinal));
        var revision = WindowHash(new { target.TopLevelId, target.TopLevelGeneration, Identity = QueryIdentity(window, false), Handle = window.TryGetPlatformHandle()?.Handle.ToInt64(),
            window.Title, window.Position, window.ClientSize, window.WindowState, window.IsActive, window.IsVisible, window.RenderScaling, window.DesktopScaling,
            window.CanMinimize, window.CanMaximize, window.CanResize, window.MinWidth, window.MinHeight,
            MaxWidth = double.IsFinite(window.MaxWidth) ? window.MaxWidth : (double?)null, MaxHeight = double.IsFinite(window.MaxHeight) ? window.MaxHeight : (double?)null,
            window.SizeToContent, NativeState = nativeState, NativeFocus = nativeFocus.State, Front = front,
            Owner = owner?.TopLevelGeneration, Modal = modal is null ? null : CreateObjectGeneration(modal), Monitors = monitors });
        return new(target, revision, backend, Clip(window.Title), window.WindowState.ToString().ToLowerInvariant(), nativeState, window.IsVisible, window.IsActive,
            nativeFocus, new(window.Position.X, window.Position.Y), units, new(window.ClientSize.Width, window.ClientSize.Height),
            window.RenderScaling, window.DesktopScaling, window.IsDialog, owner, blocker, front, monitors, actions, gaps, DateTimeOffset.UtcNow);

        string? Clip(string? value)
        {
            if (value is null) return null;
            var safe = policy?.SanitizeScalar(value) ?? value;
            return safe.Length <= 128 ? safe : safe[..(char.IsHighSurrogate(safe[127]) ? 127 : 128)];
        }
    }

    private RuntimeTargetContext WindowTarget(Window window) => new(SessionId, InspectableTopLevel.CreateId(window), capturedAt: DateTimeOffset.UtcNow,
        topLevelGeneration: CreateObjectGeneration(window));
    private static string WindowHash(object value) => Convert.ToHexStringLower(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(value)));
    private sealed class WindowStop(string code, string message) : Exception(message) { public string Code { get; } = code; }
}
