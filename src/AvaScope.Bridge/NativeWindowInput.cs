using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using AvaScope.Protocol;

namespace AvaScope.Bridge;

/// <summary>Native events addressed to a live in-process window. Never injects global desktop input.</summary>
internal sealed class NativeWindowInput : IDisposable
{
    private readonly TopLevel _top;
    private readonly nint _handle;
    private readonly string _route;
    private readonly nint _display;

    internal NativeWindowInput(TopLevel top)
    {
        ValidateOwnership(top, true);
        _top = top;
        _handle = top.TryGetPlatformHandle()!.Handle;
        _route = Route(top);
        if (_route == RuntimeOperationRoutes.X11WindowEvent)
        {
            _display = XOpenDisplay(null);
            if (_display == 0) throw new InvalidOperationException("Cannot open the application's X11 display; verify DISPLAY and XAUTHORITY.");
        }
    }

    internal static string Route(TopLevel top) => top.PlatformImpl?.GetType().Assembly.GetName().Name switch
    {
        "Avalonia.Win32" when OperatingSystem.IsWindows() => RuntimeOperationRoutes.Win32WindowMessage,
        "Avalonia.X11" when OperatingSystem.IsLinux() && IntPtr.Size == 8 => RuntimeOperationRoutes.X11WindowEvent,
        "Avalonia.Native" when OperatingSystem.IsMacOS() => RuntimeOperationRoutes.AppKitWindowEvent,
        _ => throw new NotSupportedException("Native input requires a live Win32, Linux X11 (64-bit), or macOS AppKit window. Headless/Wayland/unknown backends do not have this route.")
    };

    internal static void ValidateOwnership(TopLevel top, bool requireFocus)
    {
        Dispatcher.UIThread.VerifyAccess();
        var route = Route(top);
        var handle = top.TryGetPlatformHandle();
        if (handle is null || handle.Handle == 0 || !top.IsVisible)
            throw new InvalidOperationException("The selected native window is closed or hidden.");
        if (top is not Window window) throw new NotSupportedException("Native input currently requires a registered Window, not a popup or embedded top-level.");
        if (requireFocus && !window.IsActive) throw new InvalidOperationException("The selected native window is not active. Activate it explicitly and retry; input will not steal focus.");
        if (route == RuntimeOperationRoutes.Win32WindowMessage)
        {
            var thread = GetWindowThreadProcessId(handle.Handle, out var process);
            if (handle.HandleDescriptor != "HWND" || process != Environment.ProcessId || thread != GetCurrentThreadId())
                throw new InvalidOperationException("The HWND is not owned by the current application UI thread.");
            // Noninteractive desktops can have no foreground HWND. Addressed in-process
            // messages remain scoped to the active owned window and never use global input.
            var foreground = GetForegroundWindow();
            if (requireFocus && foreground != 0 && foreground != handle.Handle)
                throw new InvalidOperationException("The selected HWND lost foreground ownership; no global input was sent.");
        }
        else if (route == RuntimeOperationRoutes.AppKitWindowEvent)
        {
            if (handle.HandleDescriptor != "NSWindow") throw new NotSupportedException("AppKit input requires an NSWindow handle.");
            var app = Mac.Send(Mac.Class("NSApplication"), "sharedApplication");
            if (Mac.SendArg(Mac.Send(app, "windows"), Mac.Selector("containsObject:"), handle.Handle) == 0)
                throw new InvalidOperationException("The NSWindow is not in this application's owned windows.");
            if (requireFocus && Mac.Send(handle.Handle, "isKeyWindow") == 0)
                throw new InvalidOperationException("The selected NSWindow lost keyboard focus.");
        }
        else if (handle.HandleDescriptor != "XID") throw new InvalidOperationException("The selected top-level does not expose an owned X11 XID.");
    }

    internal static void ValidateOperation(TopLevel top, string action, string? text,
        IReadOnlyList<(Key Key, KeyModifiers Modifiers)> keys, KeyModifiers modifiers)
    {
        var route = Route(top);
        if (route == RuntimeOperationRoutes.AppKitWindowEvent && action == InputActions.Drag)
            throw new NotSupportedException("AppKit owned-window events cannot preserve Avalonia's native held-button motion state without global device injection. Choose synthetic drag explicitly; no input was dispatched.");
        if (route == RuntimeOperationRoutes.X11WindowEvent && action == InputActions.KeyText)
            throw new NotSupportedException("X11 targeted native events cannot guarantee literal Unicode/IME text. Use synthetic key_text for literal text, or native key_sequence for mapped keys; no clipboard or keyboard-map mutation is attempted.");
        if (action == InputActions.KeyText && text!.Any(char.IsControl))
            throw new NotSupportedException("Native literal text excludes control characters. Use key_sequence for Enter/Tab, or synthetic key_text for literal control characters.");
        if (route == RuntimeOperationRoutes.Win32WindowMessage && ((modifiers & KeyModifiers.Meta) != 0 || keys.Any(k => (k.Modifiers & KeyModifiers.Meta) != 0)))
            throw new NotSupportedException("Win32 targeted messages do not emulate Windows-logo shell shortcuts; no global shortcut will be sent.");
        using var input = new NativeWindowInput(top);
        foreach (var stroke in keys) input.ValidateKey(stroke.Key, stroke.Modifiers);
        if (route == RuntimeOperationRoutes.X11WindowEvent) input.XModifiers(modifiers);
    }

    private void Check()
    {
        ValidateOwnership(_top, false); // Cleanup remains targeted even after foreground focus changes.
        if (_top.TryGetPlatformHandle()?.Handle != _handle) throw new InvalidOperationException("The original native window handle changed; input was stopped.");
    }

    internal void Pointer(Point point, MouseButton button, string phase, KeyModifiers modifiers, int count, bool held)
    {
        Check();
        if (_route == RuntimeOperationRoutes.Win32WindowMessage)
        {
            var pixelX = checked((int)Math.Round(point.X * _top.RenderScaling));
            var pixelY = checked((int)Math.Round(point.Y * _top.RenderScaling));
            if (pixelX is < short.MinValue or > short.MaxValue || pixelY is < short.MinValue or > short.MaxValue)
                throw new NotSupportedException("Win32 window-message input coordinates exceed signed 16-bit client coordinates.");
            uint message = phase == "move" ? 0x0200u : (button, phase) switch
            {
                (MouseButton.Right, "down") => 0x0204, (MouseButton.Right, _) => 0x0205,
                (MouseButton.Middle, "down") => 0x0207, (MouseButton.Middle, _) => 0x0208,
                (_, "down") => 0x0201, _ => 0x0202
            };
            var flags = ((modifiers & KeyModifiers.Control) != 0 ? 8 : 0) | ((modifiers & KeyModifiers.Shift) != 0 ? 4 : 0);
            if (held && phase != "up") flags |= button == MouseButton.Right ? 2 : button == MouseButton.Middle ? 16 : 1;
            WithKeyboardState(modifiers, () => Send(message, flags, (pixelY << 16) | (pixelX & 0xffff)));
        }
        else if (_route == RuntimeOperationRoutes.X11WindowEvent)
        {
            var code = button == MouseButton.Right ? 3u : button == MouseButton.Middle ? 2u : 1u;
            var state = XModifiers(modifiers);
            // Avalonia 12.1's core-event modifier conversion maps Button2Mask to
            // Right and Button3Mask to Middle. Transitions infer the changed button
            // from the event kind; send the post-release state to avoid a phantom
            // second held button. Motion uses the receiver's documented core route.
            if (held && phase == "move") state |= button == MouseButton.Right ? 1u << 9 : button == MouseButton.Middle ? 1u << 10 : 1u << 8;
            var ev = NewXEvent(phase == "move" ? 6 : phase == "down" ? 4 : 5, point, state, code);
            SendX(ref ev);
        }
        else
        {
            long type = (button, phase) switch
            {
                (MouseButton.Right, "down") => 3, (MouseButton.Right, "up") => 4,
                (MouseButton.Middle, "down") => 25, (MouseButton.Middle, "up") => 26,
                (_, "down") => 1, (_, "up") => 2,
                (MouseButton.Right, _) when held => 7, (MouseButton.Middle, _) when held => 27,
                _ when held => 6, _ => 5
            };
            var value = Mac.MouseEvent(Mac.Class("NSEvent"), Mac.Selector("mouseEventWithType:location:modifierFlags:timestamp:windowNumber:context:eventNumber:clickCount:pressure:"),
                type, new Mac.Point(point.X, _top.ClientSize.Height - point.Y), Mac.Flags(modifiers), Environment.TickCount64 / 1000d,
                Mac.Send(_handle, "windowNumber"), 0, 0, count, phase == "up" ? 0 : 1);
            if (value == 0) throw new InvalidOperationException("AppKit rejected the native mouse event.");
            if (button == MouseButton.Middle && Mac.Send(value, "buttonNumber") != 2)
            {
                // NSEvent's mouse factory has no button-number argument and can label
                // otherMouseDown/Up as button zero. Preserve its window/location while
                // setting the public CGEvent button field, then rebuild the NSEvent.
                var cgEvent = Mac.Send(value, "CGEvent");
                if (cgEvent == 0) throw new NotSupportedException("AppKit did not expose the native middle-button event representation.");
                Mac.CGEventSetIntegerValueField(cgEvent, 3, 2); // kCGMouseEventButtonNumber
                value = Mac.SendArg(Mac.Class("NSEvent"), Mac.Selector("eventWithCGEvent:"), cgEvent);
                if (value == 0 || Mac.Send(value, "buttonNumber") != 2 || Mac.Send(value, "windowNumber") != Mac.Send(_handle, "windowNumber"))
                    throw new NotSupportedException("AppKit could not preserve the owned window and middle-button identity; no event was dispatched.");
            }
            Mac.SendArg(_handle, Mac.Selector("sendEvent:"), value);
        }
    }

    internal void Key(Key key, KeyModifiers modifiers, bool down)
    {
        Check();
        if (_route == RuntimeOperationRoutes.Win32WindowMessage)
        {
            var virtualKey = WinKey(key);
            var scan = MapVirtualKey(virtualKey, 0);
            var flags = 1L | ((long)scan << 16) | (down ? 0 : 0xc0000000L);
            if (key is Avalonia.Input.Key.Left or Avalonia.Input.Key.Right or Avalonia.Input.Key.Up or Avalonia.Input.Key.Down
                or Avalonia.Input.Key.Home or Avalonia.Input.Key.End or Avalonia.Input.Key.Insert or Avalonia.Input.Key.Delete) flags |= 1L << 24;
            WithKeyboardState(modifiers, () => Send(down ? 0x0100u : 0x0101u, (nint)virtualKey, (nint)flags));
        }
        else if (_route == RuntimeOperationRoutes.X11WindowEvent)
        {
            var code = XKey(key);
            var ev = NewXEvent(down ? 2 : 3, default, XModifiers(modifiers), code);
            SendX(ref ev);
        }
        else
        {
            var (code, chars) = Mac.KeyCode(key);
            var characters = Mac.String(chars);
            var value = Mac.KeyEvent(Mac.Class("NSEvent"), Mac.Selector("keyEventWithType:location:modifierFlags:timestamp:windowNumber:context:characters:charactersIgnoringModifiers:isARepeat:keyCode:"),
                down ? 10 : 11, default, Mac.Flags(modifiers), Environment.TickCount64 / 1000d, Mac.Send(_handle, "windowNumber"), 0,
                characters, characters, false, code);
            if (value == 0) throw new InvalidOperationException("AppKit rejected the native key event.");
            Mac.SendArg(_handle, Mac.Selector("sendEvent:"), value);
        }
    }

    internal void Text(string text)
    {
        Check();
        if (_route == RuntimeOperationRoutes.Win32WindowMessage)
            foreach (var character in text) Send(0x0102, character, 1);
        else if (_route == RuntimeOperationRoutes.AppKitWindowEvent)
        {
            var responder = Mac.Send(_handle, "firstResponder");
            var selector = Mac.Selector("insertText:replacementRange:");
            if (Mac.SendArg(responder, Mac.Selector("respondsToSelector:"), selector) == 0)
                throw new NotSupportedException("The owned AppKit first responder does not implement NSTextInputClient literal insertion.");
            Mac.InsertText(responder, selector, Mac.String(text), new Mac.Range(nuint.MaxValue, 0));
        }
        else throw new NotSupportedException("Native literal text is unavailable for this backend.");
    }

    private void ValidateKey(Key key, KeyModifiers modifiers)
    {
        if (_route == RuntimeOperationRoutes.Win32WindowMessage) _ = WinKey(key);
        else if (_route == RuntimeOperationRoutes.X11WindowEvent) { _ = XKey(key); _ = XModifiers(modifiers); }
        else _ = Mac.KeyCode(key);
    }

    private void Send(uint message, nint wParam, nint lParam)
    {
        if (SendMessageTimeout(_handle, message, wParam, lParam, 2, 200, out _) == 0)
            throw new InvalidOperationException("The owned HWND did not acknowledge its native message.");
    }

    private static void WithKeyboardState(KeyModifiers modifiers, Action action)
    {
        var original = new byte[256];
        if (!GetKeyboardState(original)) throw new InvalidOperationException("Cannot read this UI thread's keyboard state.");
        var state = (byte[])original.Clone();
        foreach (var index in new[] { 0x10, 0x11, 0x12, 0xa0, 0xa1, 0xa2, 0xa3, 0xa4, 0xa5 }) state[index] = 0;
        if ((modifiers & KeyModifiers.Shift) != 0) state[0x10] = state[0xa0] = 0x80;
        if ((modifiers & KeyModifiers.Control) != 0) state[0x11] = state[0xa2] = 0x80;
        if ((modifiers & KeyModifiers.Alt) != 0) state[0x12] = state[0xa4] = 0x80;
        if (!SetKeyboardState(state)) throw new InvalidOperationException("Cannot set this UI thread's request-local modifier state.");
        try { action(); }
        finally { if (!SetKeyboardState(original)) throw new InvalidOperationException("Failed to restore the application's UI-thread keyboard state."); }
    }

    private static uint WinKey(Key key)
    {
        if (key is >= Avalonia.Input.Key.A and <= Avalonia.Input.Key.Z) return (uint)(0x41 + key - Avalonia.Input.Key.A);
        if (key is >= Avalonia.Input.Key.D0 and <= Avalonia.Input.Key.D9) return (uint)(0x30 + key - Avalonia.Input.Key.D0);
        if (key is >= Avalonia.Input.Key.F1 and <= Avalonia.Input.Key.F12) return (uint)(0x70 + key - Avalonia.Input.Key.F1);
        return key switch
        {
            Avalonia.Input.Key.Back => 8, Avalonia.Input.Key.Tab => 9, Avalonia.Input.Key.Enter => 13,
            Avalonia.Input.Key.Escape => 27, Avalonia.Input.Key.Space => 32, Avalonia.Input.Key.PageUp => 33,
            Avalonia.Input.Key.PageDown => 34, Avalonia.Input.Key.End => 35, Avalonia.Input.Key.Home => 36,
            Avalonia.Input.Key.Left => 37, Avalonia.Input.Key.Up => 38, Avalonia.Input.Key.Right => 39,
            Avalonia.Input.Key.Down => 40, Avalonia.Input.Key.Insert => 45, Avalonia.Input.Key.Delete => 46,
            _ => throw new NotSupportedException($"Native Win32 key '{key}' is not supported; use logical A-Z, D0-D9, F1-F12 or navigation keys.")
        };
    }

    private byte XKey(Key key)
    {
        var name = key switch
        {
            >= Avalonia.Input.Key.A and <= Avalonia.Input.Key.Z => key.ToString().ToLowerInvariant(),
            >= Avalonia.Input.Key.D0 and <= Avalonia.Input.Key.D9 => ((int)(key - Avalonia.Input.Key.D0)).ToString(System.Globalization.CultureInfo.InvariantCulture),
            Avalonia.Input.Key.Enter => "Return", Avalonia.Input.Key.Back => "BackSpace", Avalonia.Input.Key.Space => "space",
            Avalonia.Input.Key.PageUp => "Prior", Avalonia.Input.Key.PageDown => "Next", _ => key.ToString()
        };
        var symbol = XStringToKeysym(name);
        var code = symbol == 0 ? (byte)0 : XKeysymToKeycode(_display, symbol);
        if (code == 0 || XkbKeycodeToKeysym(_display, code, 0, 0) != symbol)
            throw new NotSupportedException($"Key '{key}' is not a base-group key in the current X11 map; no keyboard layout mutation is allowed.");
        return code;
    }

    private uint XModifiers(KeyModifiers modifiers)
    {
        uint result = 0;
        var mapPointer = XGetModifierMapping(_display);
        if (mapPointer == 0) throw new InvalidOperationException("Cannot read the X11 modifier mapping.");
        try
        {
            var map = Marshal.PtrToStructure<XModifierMap>(mapPointer);
            foreach (var (flag, name, expectedGroup) in new[] { (KeyModifiers.Shift, "Shift_L", 0), (KeyModifiers.Control, "Control_L", 2), (KeyModifiers.Alt, "Alt_L", 3), (KeyModifiers.Meta, "Super_L", 6) })
            {
                if ((modifiers & flag) == 0) continue;
                var code = XKeysymToKeycode(_display, XStringToKeysym(name));
                var found = false;
                for (var group = 0; group < 8; group++)
                    for (var index = 0; index < map.MaxKeysPerModifier; index++)
                        if (code != 0 && Marshal.ReadByte(map.Keys, group * map.MaxKeysPerModifier + index) == code)
                        {
                            if (group != expectedGroup)
                                throw new NotSupportedException($"X11 modifier '{name}' uses a mapping not supported by Avalonia's core-event route; use explicit synthetic input.");
                            result |= 1u << group;
                            found = true;
                        }
                if (!found) throw new NotSupportedException($"The current X11 map has no validated '{name}' modifier.");
            }
        }
        finally { XFreeModifiermap(mapPointer); }
        return result;
    }

    private XEvent NewXEvent(int type, Point point, uint state, uint detail) => new()
    {
        Type = type, SendEvent = 1, Display = _display, Window = _handle, Root = XDefaultRootWindow(_display),
        Time = (nuint)(Environment.TickCount64 & uint.MaxValue), X = (int)Math.Round(point.X * _top.RenderScaling),
        Y = (int)Math.Round(point.Y * _top.RenderScaling), State = state, Detail = detail, SameScreen = 1
    };

    private void SendX(ref XEvent value)
    {
        // NoEventMask addresses the window's creating client regardless of XI2 subscriptions. No propagation.
        if (XSendEvent(_display, _handle, false, 0, ref value) == 0) throw new InvalidOperationException("X11 rejected the owned-window event.");
        XFlush(_display);
    }

    public void Dispose() { if (_display != 0) XCloseDisplay(_display); }

    [StructLayout(LayoutKind.Sequential, Size = 192)]
    private struct XEvent
    {
        public int Type; public nuint Serial; public int SendEvent; public nint Display; public nint Window;
        public nint Root; public nint Subwindow; public nuint Time; public int X; public int Y; public int RootX; public int RootY;
        public uint State; public uint Detail; public int SameScreen;
    }
    [StructLayout(LayoutKind.Sequential)] private struct XModifierMap { public int MaxKeysPerModifier; public nint Keys; }
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(nint handle, out uint process);
    [DllImport("kernel32.dll")] private static extern uint GetCurrentThreadId();
    [DllImport("user32.dll")] private static extern nint GetForegroundWindow();
    [DllImport("user32.dll", EntryPoint = "SendMessageTimeoutW")] private static extern nint SendMessageTimeout(nint handle, uint message, nint wParam, nint lParam, uint flags, uint timeout, out nint result);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool GetKeyboardState(byte[] state);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool SetKeyboardState(byte[] state);
    [DllImport("user32.dll", EntryPoint = "MapVirtualKeyW")] private static extern uint MapVirtualKey(uint code, uint mapType);
    [DllImport("libX11.so.6")] private static extern nint XOpenDisplay(string? display);
    [DllImport("libX11.so.6")] private static extern int XCloseDisplay(nint display);
    [DllImport("libX11.so.6")] private static extern nint XDefaultRootWindow(nint display);
    [DllImport("libX11.so.6")] private static extern int XSendEvent(nint display, nint window, [MarshalAs(UnmanagedType.Bool)] bool propagate, nint mask, ref XEvent value);
    [DllImport("libX11.so.6")] private static extern int XFlush(nint display);
    [DllImport("libX11.so.6")] private static extern nuint XStringToKeysym(string name);
    [DllImport("libX11.so.6")] private static extern byte XKeysymToKeycode(nint display, nuint symbol);
    [DllImport("libX11.so.6")] private static extern nuint XkbKeycodeToKeysym(nint display, byte code, int group, int level);
    [DllImport("libX11.so.6")] private static extern nint XGetModifierMapping(nint display);
    [DllImport("libX11.so.6")] private static extern int XFreeModifiermap(nint map);

    internal static class Mac
    {
        private const string ObjC = "/usr/lib/libobjc.A.dylib";
        [StructLayout(LayoutKind.Sequential)] internal readonly record struct Point(double X, double Y);
        [StructLayout(LayoutKind.Sequential)] internal readonly record struct Range(nuint Location, nuint Length);
        [DllImport(ObjC, EntryPoint = "objc_getClass")] internal static extern nint Class(string name);
        [DllImport(ObjC, EntryPoint = "sel_registerName")] internal static extern nint Selector(string name);
        [DllImport(ObjC, EntryPoint = "objc_msgSend")] private static extern nint Send0(nint receiver, nint selector);
        [DllImport(ObjC, EntryPoint = "objc_msgSend")] internal static extern nint SendArg(nint receiver, nint selector, nint argument);
        [DllImport(ObjC, EntryPoint = "objc_msgSend")] private static extern nint StringUtf8(nint receiver, nint selector, [MarshalAs(UnmanagedType.LPUTF8Str)] string value);
        [DllImport(ObjC, EntryPoint = "objc_msgSend")] internal static extern nint MouseEvent(nint receiver, nint selector, long type, Point location, nuint flags, double timestamp, nint window, nint context, nint eventNumber, nint clickCount, float pressure);
        [DllImport(ObjC, EntryPoint = "objc_msgSend")] internal static extern nint KeyEvent(nint receiver, nint selector, long type, Point location, nuint flags, double timestamp, nint window, nint context, nint characters, nint ignoringModifiers, [MarshalAs(UnmanagedType.I1)] bool repeat, ushort code);
        [DllImport(ObjC, EntryPoint = "objc_msgSend")] internal static extern void InsertText(nint receiver, nint selector, nint text, Range replacement);
        [DllImport("/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics")]
        internal static extern void CGEventSetIntegerValueField(nint value, int field, long number);
        internal static nint Send(nint receiver, string selector) => Send0(receiver, Selector(selector));
        internal static nint String(string value) => StringUtf8(Class("NSString"), Selector("stringWithUTF8String:"), value);
        internal static nuint Flags(KeyModifiers modifiers) =>
            ((modifiers & KeyModifiers.Shift) != 0 ? 1u << 17 : 0) | ((modifiers & KeyModifiers.Control) != 0 ? 1u << 18 : 0)
            | ((modifiers & KeyModifiers.Alt) != 0 ? 1u << 19 : 0) | ((modifiers & KeyModifiers.Meta) != 0 ? 1u << 20 : 0);

        internal static (ushort Code, string Characters) KeyCode(Key key) => key switch
        {
            // Navigation is independent of the active layout. Letter shortcuts require a future validated layout mapper.
            Avalonia.Input.Key.Enter => (36, "\r"), Avalonia.Input.Key.Tab => (48, "\t"), Avalonia.Input.Key.Space => (49, " "),
            Avalonia.Input.Key.Back => (51, "\b"), Avalonia.Input.Key.Escape => (53, "\u001b"),
            Avalonia.Input.Key.Home => (115, "\uf729"), Avalonia.Input.Key.End => (119, "\uf72b"),
            Avalonia.Input.Key.PageUp => (116, "\uf72c"), Avalonia.Input.Key.PageDown => (121, "\uf72d"),
            Avalonia.Input.Key.Delete => (117, "\uf728"), Avalonia.Input.Key.Left => (123, "\uf702"),
            Avalonia.Input.Key.Right => (124, "\uf703"), Avalonia.Input.Key.Down => (125, "\uf701"), Avalonia.Input.Key.Up => (126, "\uf700"),
            _ => throw new NotSupportedException($"Native AppKit key '{key}' has no validated layout mapping. Use navigation keys, native literal text, or explicitly synthetic key_sequence for application shortcuts.")
        };
    }
}
