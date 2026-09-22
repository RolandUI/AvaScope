using System.Diagnostics;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Raw;
using Avalonia.Threading;
using Avalonia.VisualTree;
using AvaScope.Core;
using AvaScope.Protocol;

namespace AvaScope.Bridge;

public sealed partial class AvaScopeBridgeRuntime
{
    private readonly SemaphoreSlim _explicitInputGate = new(1, 1);

    private async Task<CoreResult<InputResponse>> ExecuteExplicitInputAsync(
        string topLevelId, string action, double? x, double? y, string? text, string? nodeId,
        string? key, string? modifiers, InputGestureOptions? gesture, RuntimeTargetContext? target,
        InputExecutionOptions options, bool validateOnly, CancellationToken cancellationToken)
    {
        if (!await _explicitInputGate.WaitAsync(0, cancellationToken))
            return ExplicitInputFailure("Another compound input request is active; wait for its result.", options, 0, "not_needed");

        ExplicitInputPlan? plan = null;
        NativeWindowInput? native = null;
        Pointer? pointer = null;
        InputElement? pressedTarget = null;
        Key? heldKey = null;
        var heldModifiers = KeyModifiers.None;
        var heldButton = false;
        var dispatched = 0;
        var point = default(Point);
        var cleanup = "not_needed";
        CoreError? error = null;
        InputResponse? response = null;
        var elapsed = Stopwatch.StartNew();
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(5));
        var token = deadline.Token;
        try
        {
            var prepared = await Dispatcher.UIThread.InvokeAsync(() => PrepareExplicitInput(
                topLevelId, action, x, y, text, nodeId, key, modifiers, gesture, target, options),
                DispatcherPriority.Background, token);
            if (!prepared.Success)
            {
                var failure = prepared.Error!;
                var details = new Dictionary<string, string>(failure.Details ?? new Dictionary<string, string>())
                {
                    ["requestedStrategy"] = options.Strategy, ["dispatchedEvents"] = "0", ["cleanup"] = "not_needed"
                };
                return CoreResult<InputResponse>.Fail(new CoreError(failure.Code, failure.Message, details));
            }
            plan = prepared.Value!;
            point = plan.Start;
            if (validateOnly)
                return CoreResult<InputResponse>.Ok(await Dispatcher.UIThread.InvokeAsync(() => MakeResponse(false)));

            if (options.Strategy == "semantic")
            {
                var semantic = await Dispatcher.UIThread.InvokeAsync(() => Input(topLevelId, action, x, y, text,
                    nodeId, key, modifiers, target), DispatcherPriority.Background, token);
                if (!semantic.Success) return semantic;
                var value = semantic.Value!;
                return CoreResult<InputResponse>.Ok(new(value.SessionId, value.TopLevelId, value.Action, value.Handled, value.ExecutedAt,
                    value.TargetNodeId, value.Target, value.InputKey, value.KeyModifiers, value.PointerButton, value.WheelDeltaX, value.WheelDeltaY,
                    new Dictionary<string, string>(value.Metadata) { ["requestedStrategy"] = "semantic" }, value.Gesture, value.Provenance));
            }

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                CheckTarget();
                if (options.Strategy == "native") native = new NativeWindowInput(plan.TopLevel);
                else pointer = new Pointer(Pointer.GetNextFreeId(), PointerType.Mouse, true);
                if (action is InputActions.KeySequence or InputActions.KeyText)
                {
                    if (!plan.Target.IsFocused && !plan.Target.Focus(NavigationMethod.Unspecified))
                        throw new InvalidOperationException("The selected keyboard target did not accept focus.");
                }
            }, DispatcherPriority.Background, token);

            if (action == InputActions.KeyText)
            {
                await OnUi(() =>
                {
                    if (native is not null) native.Text(text!);
                    else plan.Target.RaiseEvent(new TextInputEventArgs { RoutedEvent = InputElement.TextInputEvent, Source = plan.Target, Text = text });
                });
            }
            else if (action == InputActions.KeySequence)
            {
                foreach (var stroke in plan.Keys)
                {
                    await OnUi(() =>
                    {
                        heldKey = stroke.Key;
                        heldModifiers = stroke.Modifiers;
                        DispatchKey(stroke.Key, stroke.Modifiers, true);
                    });
                    await OnUi(() => DispatchKey(stroke.Key, stroke.Modifiers, false));
                    heldKey = null;
                    if (options.IntervalMs > 0) await Task.Delay(options.IntervalMs, token);
                }
            }
            else if (action == InputActions.Click)
            {
                if (plan.ClickGroupDelayMs > 0) await Task.Delay(plan.ClickGroupDelayMs, token);
                for (var click = 1; click <= options.ClickCount; click++)
                {
                    var count = click;
                    await OnUi(() => { heldButton = true; DispatchPointer("down", count); });
                    await OnUi(() => DispatchPointer("up", count));
                    heldButton = false;
                    if (click < options.ClickCount && options.IntervalMs > 0) await Task.Delay(options.IntervalMs, token);
                }
            }
            else
            {
                if (action == InputActions.Drag)
                {
                    await OnUi(() => { heldButton = true; DispatchPointer("down", 1); });
                }
                var motionStart = Stopwatch.GetTimestamp();
                for (var step = 1; step <= options.MotionSteps; step++)
                {
                    var progress = (double)step / options.MotionSteps;
                    var position = options.MotionProfile == "ease_in_out" ? progress * progress * (3 - 2 * progress) : progress;
                    point = plan.Start + (plan.End - plan.Start) * position;
                    var remaining = TimeSpan.FromMilliseconds(options.DurationMs * progress) - Stopwatch.GetElapsedTime(motionStart);
                    if (remaining > TimeSpan.Zero) await Task.Delay(remaining, token);
                    await OnUi(() => DispatchPointer("move", 1));
                }
                if (heldButton)
                {
                    await OnUi(() => DispatchPointer("up", 1));
                    heldButton = false;
                }
            }
            response = await Dispatcher.UIThread.InvokeAsync(() => MakeResponse(true), DispatcherPriority.Background, token);
        }
        catch (Exception exception) when (exception is OperationCanceledException or InvalidOperationException
            or NotSupportedException or System.Runtime.InteropServices.ExternalException or DllNotFoundException or EntryPointNotFoundException)
        {
            error = new CoreError(BridgeErrorCodes.InvalidInputRequest,
                exception is OperationCanceledException ? "Input was cancelled or reached its five-second deadline; inspect state before retrying." : exception.Message);
        }
        finally
        {
            try
            {
                // Release only this request's synthetic device / targeted native events, never global user input.
                var release = Dispatcher.UIThread.InvokeAsync(() =>
                {
                    try
                    {
                        if (heldKey is { } keyToRelease) DispatchKey(keyToRelease, heldModifiers, false);
                        if (heldButton) DispatchPointer("up", 1);
                        if (heldKey is not null || heldButton) cleanup = "released";
                    }
                    finally
                    {
                        pointer?.Capture(null);
                        native?.Dispose();
                    }
                }, DispatcherPriority.Send);
                await release.GetTask().WaitAsync(TimeSpan.FromSeconds(1));
            }
            catch (Exception exception) when (exception is TimeoutException or InvalidOperationException or System.Runtime.InteropServices.ExternalException)
            {
                cleanup = "failed_or_pending";
                error ??= new CoreError(BridgeErrorCodes.InvalidInputRequest, "Input cleanup did not complete; inspect the selected app before continuing.");
            }
            _explicitInputGate.Release();
        }

        return error is null ? CoreResult<InputResponse>.Ok(response!)
            : ExplicitInputFailure(error.Message, options, dispatched, cleanup);

        async Task OnUi(Action dispatch)
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                CheckTarget();
                dispatched++; // A throwing application callback can already have changed state.
                dispatch();
            }, DispatcherPriority.Background, token);
        }

        void CheckTarget()
        {
            if (plan is null || FindTopLevel(topLevelId) != plan.TopLevel || TopLevel.GetTopLevel(plan.Target) != plan.TopLevel
                || !plan.Target.IsEffectivelyVisible || !plan.Target.IsEffectivelyEnabled)
                throw new InvalidOperationException("The selected input target closed, detached or became unavailable.");
            if (options.Strategy == "native") NativeWindowInput.ValidateOwnership(plan.TopLevel, requireFocus: true);
            if (dispatched > 0 && action is InputActions.KeyText or InputActions.KeySequence && !plan.Target.IsFocused)
                throw new InvalidOperationException("Keyboard focus changed during the request; remaining input was not dispatched.");
        }

        void DispatchKey(Key inputKey, KeyModifiers inputModifiers, bool down)
        {
            if (native is not null) native.Key(inputKey, inputModifiers, down);
            else plan!.Target.RaiseEvent(new KeyEventArgs
            {
                RoutedEvent = down ? InputElement.KeyDownEvent : InputElement.KeyUpEvent,
                Source = plan.Target, Key = inputKey, KeyModifiers = inputModifiers, KeyDeviceType = KeyDeviceType.Keyboard
            });
        }

        void DispatchPointer(string phase, int count)
        {
            if (native is not null)
            {
                native.Pointer(point, plan!.Button, phase, plan.Modifiers, count, heldButton);
                return;
            }
            if (pointer is null || plan is null) return;
            var recipient = pointer.Captured as InputElement ?? pressedTarget ?? plan.Target;
            if (phase == "move" && !heldButton && pointer.Captured is null)
            {
                var hit = plan.TopLevel.GetVisualAt(point);
                recipient = hit as InputElement ?? hit?.FindAncestorOfType<InputElement>() ?? plan.TopLevel;
            }
            var rawButton = plan.Button switch
            {
                MouseButton.Right => RawInputModifiers.RightMouseButton,
                MouseButton.Middle => RawInputModifiers.MiddleMouseButton,
                _ => RawInputModifiers.LeftMouseButton
            };
            var update = (plan.Button, phase) switch
            {
                (MouseButton.Right, "down") => PointerUpdateKind.RightButtonPressed,
                (MouseButton.Right, "up") => PointerUpdateKind.RightButtonReleased,
                (MouseButton.Middle, "down") => PointerUpdateKind.MiddleButtonPressed,
                (MouseButton.Middle, "up") => PointerUpdateKind.MiddleButtonReleased,
                (_, "down") => PointerUpdateKind.LeftButtonPressed,
                (_, "up") => PointerUpdateKind.LeftButtonReleased,
                _ => PointerUpdateKind.Other
            };
            var properties = new PointerPointProperties(phase == "up" || !heldButton ? RawInputModifiers.None : rawButton, update);
            var timestamp = (ulong)Environment.TickCount64;
            if (phase == "down")
            {
                pressedTarget = recipient;
                recipient.RaiseEvent(new PointerPressedEventArgs(recipient, pointer, plan.TopLevel, point, timestamp, properties, plan.Modifiers, count));
            }
            else if (phase == "up")
            {
                try { recipient.RaiseEvent(new PointerReleasedEventArgs(recipient, pointer, plan.TopLevel, point, timestamp, properties, plan.Modifiers, plan.Button)); }
                finally { pointer.Capture(null); pressedTarget = null; }
            }
            else recipient.RaiseEvent(new PointerEventArgs(InputElement.PointerMovedEvent, recipient, pointer, plan.TopLevel, point, timestamp, properties, plan.Modifiers));
        }

        InputResponse MakeResponse(bool handled) => new(SessionId, topLevelId, action, handled, DateTimeOffset.UtcNow,
            CreateNodeId(plan!.Target, TreeKinds.Visual), CreateNodeTarget(topLevelId, TreeKinds.Visual, plan.TopLevel, plan.Target),
            pointerButton: action is InputActions.Click or InputActions.Drag ? options.Button : null,
            metadata: new Dictionary<string, string>
            {
                ["requestedStrategy"] = options.Strategy, ["dispatchedEvents"] = dispatched.ToString(CultureInfo.InvariantCulture),
                ["cleanup"] = heldButton || heldKey is not null ? "pending" : "no_owned_input_held",
                ["clickCount"] = options.ClickCount.ToString(CultureInfo.InvariantCulture), ["clickGrouping"] = options.Strategy == "synthetic" ? "explicit_count_per_press" : "native_new_group_after_platform_double_tap_interval",
                ["clickGroupDelayMs"] = plan.ClickGroupDelayMs.ToString(CultureInfo.InvariantCulture),
                ["intervalMs"] = options.IntervalMs.ToString(CultureInfo.InvariantCulture), ["motionProfile"] = options.MotionProfile,
                ["motionSteps"] = options.MotionSteps.ToString(CultureInfo.InvariantCulture), ["elapsedMs"] = elapsed.ElapsedMilliseconds.ToString(CultureInfo.InvariantCulture),
                ["permission"] = options.Strategy == "native" ? "in_process_owned_native_window;no_global_injection" : "activated_bridge",
                ["coverage"] = options.Strategy == "native" ? "native_window_event_dispatch;not_hardware_injection_or_ime_composition" : "avalonia_app_logic"
            }, provenance: RuntimePlatformEvidence.Operation(plan.TopLevel, plan.Route, handled, coordinateSpace: "top_level_dip"));
    }

    private CoreResult<ExplicitInputPlan> PrepareExplicitInput(string topLevelId, string action, double? x, double? y,
        string? text, string? nodeId, string? key, string? modifiers, InputGestureOptions? gesture,
        RuntimeTargetContext? generationTarget, InputExecutionOptions options)
    {
        CoreResult<ExplicitInputPlan> Fail(string message) => CoreResult<ExplicitInputPlan>.Fail(new CoreError(BridgeErrorCodes.InvalidInputRequest, message));
        if (options.GetValidationError(action) is { } validationError) return Fail(validationError);
        if (gesture is not null) return Fail("Explicit execution uses destinationX/Y and durationMs; legacy gesture options cannot be combined with execution.");
        if (action != InputActions.Click && (options.ClickCount != 1 || (options.Button != "left" && action != InputActions.Drag)))
            return Fail("Click grouping/button options require click or drag.");
        if ((action == InputActions.KeySequence) != (options.Keys.Count > 0)) return Fail("key_sequence requires execution.keys; other actions do not accept keys.");
        if (key is not null) return Fail("Explicit keyboard input uses key_sequence with execution.keys containing paired keystrokes.");
        if (modifiers?.Length > 64) return Fail("Modifier strings must be at most 64 characters.");
        var parsedModifiers = ParseKeyModifiers(modifiers);
        if (!parsedModifiers.Success) return CoreResult<ExplicitInputPlan>.Fail(parsedModifiers.Error!);
        var keys = new List<(Key Key, KeyModifiers Modifiers)>();
        foreach (var stroke in options.Keys)
        {
            if (stroke is null) return Fail("Key sequence entries cannot be null.");
            if (string.IsNullOrWhiteSpace(stroke.Key) || stroke.Key.Length > 64 || stroke.Modifiers?.Length > 64)
                return Fail("Key names and modifier strings must be at most 64 characters; key names cannot be empty.");
            var parsed = ParseInputKey(stroke.Key);
            var flags = ParseKeyModifiers(stroke.Modifiers);
            if (!parsed.Success) return CoreResult<ExplicitInputPlan>.Fail(parsed.Error!);
            if (!flags.Success) return CoreResult<ExplicitInputPlan>.Fail(flags.Error!);
            if (parsed.Value is Key.LeftAlt or Key.RightAlt or Key.LeftCtrl or Key.RightCtrl or Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin)
                return Fail("Use each stroke's modifiers field for modifiers; standalone held modifier keys are not supported.");
            keys.Add((parsed.Value, flags.Value));
        }
        var top = FindTopLevel(topLevelId);
        if (top is null) return TopLevelNotFound<ExplicitInputPlan>(topLevelId);
        var current = ResolveInputGenerationTarget(top, topLevelId, nodeId, generationTarget);
        if (!current.Success) return CoreResult<ExplicitInputPlan>.Fail(current.Error!);
        nodeId = current.Value!.NodeId;
        if (options.Strategy == "semantic")
        {
            if (parsedModifiers.Value != KeyModifiers.None) return Fail("Modified input requires explicit synthetic or native events.");
            var validated = ValidateInput(topLevelId, action, x, y, text, nodeId, key, modifiers, null, generationTarget, null);
            if (!validated.Success) return CoreResult<ExplicitInputPlan>.Fail(validated.Error!);
            var route = validated.Value!.Provenance?.PlannedRoute;
            if (route is not (RuntimeOperationRoutes.AutomationProvider or RuntimeOperationRoutes.ControlProperty or RuntimeOperationRoutes.RoutedEvent or RuntimeOperationRoutes.Focus))
                return Fail("The selected operation has no semantic route; explicitly request synthetic or native input.");
            if (options.Button != "left" || options.ClickCount != 1 || options.DestinationX is not null || options.DestinationY is not null)
                return Fail("Semantic input does not accept compound pointer options.");
            return CoreResult<ExplicitInputPlan>.Ok(new(top, top, default, default, MouseButton.Left, parsedModifiers.Value, keys, route));
        }
        if (action is not (InputActions.Click or InputActions.PointerMove or InputActions.Drag or InputActions.KeySequence or InputActions.KeyText))
            return Fail("Explicit synthetic/native input supports click, pointer_move, drag, key_sequence and literal key_text. Use paired compound input instead of held key/button requests.");
        if (action == InputActions.KeyText && (text is null || text.Length > 2048 || text.Contains('\0')))
            return Fail("Literal key_text requires at most 2048 UTF-16 characters without NUL; IME composition is not supported.");
        var keyboard = action is InputActions.KeySequence or InputActions.KeyText;
        if (keyboard && parsedModifiers.Value != KeyModifiers.None) return Fail("Use per-stroke modifiers on key_sequence; literal text does not accept keyboard modifiers.");
        if (action != InputActions.KeyText && text is not null) return Fail("Only key_text accepts inputText with explicit synthetic/native execution.");
        InputElement? recipient = nodeId is null ? null : FindNodeById(top, nodeId) as InputElement;
        if (nodeId is not null && recipient is null) return Fail("The requested visual input node is absent or is not an input element; no focused-node substitution was attempted.");
        if (keyboard) recipient ??= top.FocusManager?.GetFocusedElement() as InputElement;
        if ((x is null) != (y is null)) return Fail("Both x and y must be supplied together.");
        var start = x is not null ? new Point(x.Value, y!.Value) : recipient is null ? default : GetGlobalBounds(recipient, top)?.Center ?? default;
        if (!keyboard)
        {
            if (x is null && recipient is null) return Fail("Pointer input requires a current visual target or explicit x/y.");
            var hit = top.GetVisualAt(start);
            if (recipient is not null && hit != recipient && hit?.GetVisualAncestors().Contains(recipient) != true)
                return Fail("The selected target is covered or its point hits a different control; inspect current bounds and overlays.");
            recipient = hit as InputElement ?? hit?.FindAncestorOfType<InputElement>();
        }
        if (recipient is null || TopLevel.GetTopLevel(recipient) != top || !recipient.IsEffectivelyVisible || !recipient.IsEffectivelyEnabled)
            return Fail("The selected input target is absent, hidden, disabled or detached.");
        if ((options.DestinationX is null) != (options.DestinationY is null)) return Fail("Both destinationX and destinationY are required together.");
        if (action == InputActions.Drag && options.DestinationX is null) return Fail("Explicit drag requires destinationX/Y in the selected top-level's DIP coordinates.");
        if (action is not (InputActions.PointerMove or InputActions.Drag) && options.DestinationX is not null)
            return Fail("Only pointer_move and drag accept a motion destination.");
        var end = options.DestinationX is { } dx ? new Point(dx, options.DestinationY!.Value) : start;
        var bounds = new Rect(top.ClientSize);
        if (!keyboard && (!double.IsFinite(start.X) || !double.IsFinite(start.Y) || !double.IsFinite(end.X) || !double.IsFinite(end.Y)
            || !bounds.Contains(start) || !bounds.Contains(end))) return Fail("Pointer motion must remain within the selected top-level's finite client bounds.");
        var routeName = keyboard ? RuntimeOperationRoutes.SyntheticKey : RuntimeOperationRoutes.SyntheticPointer;
        if (options.Strategy == "native")
        {
            try
            {
                NativeWindowInput.ValidateOwnership(top, requireFocus: true);
                NativeWindowInput.ValidateOperation(top, action, text, keys, parsedModifiers.Value);
                routeName = NativeWindowInput.Route(top);
            }
            catch (Exception exception) when (exception is NotSupportedException or InvalidOperationException or DllNotFoundException or EntryPointNotFoundException)
            { return Fail(exception.Message); }
        }
        var groupDelay = 0;
        if (options.Strategy == "native" && action == InputActions.Click)
        {
            var doubleTap = top.GetPlatformSettings()?.GetDoubleTapTime(PointerType.Mouse).TotalMilliseconds;
            if (doubleTap is null or < 1 or > 3000 || (options.ClickCount > 1 && options.IntervalMs >= doubleTap))
                return Fail("Native click grouping requires a platform double-tap interval of 1..3000 ms and a shorter requested inter-click interval.");
            groupDelay = (int)Math.Ceiling(doubleTap.Value) + 25;
        }
        return CoreResult<ExplicitInputPlan>.Ok(new(top, recipient, start, end,
            options.Button == "right" ? MouseButton.Right : options.Button == "middle" ? MouseButton.Middle : MouseButton.Left,
            parsedModifiers.Value, keys, routeName, groupDelay));
    }

    private static CoreResult<InputResponse> ExplicitInputFailure(string message, InputExecutionOptions options, int dispatched, string cleanup) =>
        CoreResult<InputResponse>.Fail(new CoreError(BridgeErrorCodes.InvalidInputRequest, message, new Dictionary<string, string>
        {
            ["requestedStrategy"] = options.Strategy, ["dispatchedEvents"] = dispatched.ToString(CultureInfo.InvariantCulture),
            ["cleanup"] = cleanup, ["nextAction"] = "Observe current application state before retrying; no route downgrade was attempted."
        }));

    private sealed record ExplicitInputPlan(TopLevel TopLevel, InputElement Target, Point Start, Point End,
        MouseButton Button, KeyModifiers Modifiers, IReadOnlyList<(Key Key, KeyModifiers Modifiers)> Keys, string Route, int ClickGroupDelayMs = 0);
}
