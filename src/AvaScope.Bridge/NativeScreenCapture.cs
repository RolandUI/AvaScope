using System.Runtime.InteropServices;
using AvaScope.Protocol;

namespace AvaScope.Bridge;

// These APIs capture presented desktop pixels, never an off-screen window backing store.
// The caller must authorize the declared test desktop before reaching this class.
internal static class NativeScreenCapture
{
    internal sealed record Pixels(byte[] Bgra, int Width, int Height);
    internal sealed class CaptureException(string code, string message) : Exception(message)
    { internal string Code { get; } = code; }

    internal static string Route(string backend) => backend switch
    {
        "win32" => "win32_desktop_bitblt", "x11" => "x11_root_get_image",
        "macos" => "macos_screencapturekit_region", _ => "native_screen_unsupported"
    };

    internal static Task<Pixels> CaptureAsync(string backend, NodeBounds bounds) => backend switch
    {
        "win32" when OperatingSystem.IsWindows() => Task.FromResult(Windows.Capture(bounds)),
        "x11" when OperatingSystem.IsLinux() => Task.FromResult(X11.Capture(bounds)),
        "macos" when OperatingSystem.IsMacOS() => Mac.Capture(bounds),
        _ => throw new CaptureException("native_screen_unsupported", "This backend has no supported native desktop capture route.")
    };

    private static CaptureException Failed() => new("native_screen_failed", "The native capture API could not return the requested visible desktop region.");
    private static void ValidateSize(int width, int height)
    {
        if (width < 1 || height < 1 || (long)width * height > 4194304)
            throw new CaptureException("native_screen_pixel_limit", "A native capture is limited to 4194304 pixels.");
    }

    private static class Windows
    {
        internal static Pixels Capture(NodeBounds rect)
        {
            var width = checked((int)rect.Width); var height = checked((int)rect.Height); ValidateSize(width, height);
            var screen = GetDC(0); if (screen == 0) throw Failure("GetDC");
            nint memory = 0, bitmap = 0, previous = 0;
            try
            {
                memory = CreateCompatibleDC(screen); if (memory == 0) throw Failure("CreateCompatibleDC");
                var info = new BitmapInfo { Size = 40, Width = width, Height = -height, Planes = 1, BitCount = 32 };
                bitmap = CreateDIBSection(screen, ref info, 0, out var bits, 0, 0);
                if (bitmap == 0 || bits == 0) throw Failure("CreateDIBSection");
                previous = SelectObject(memory, bitmap); if (previous is 0 or -1) throw Failure("SelectObject");
                if (!BitBlt(memory, 0, 0, width, height, screen, checked((int)rect.X), checked((int)rect.Y), 0x40CC0020)) throw Failure("BitBlt");
                var bytes = new byte[checked(width * height * 4)]; Marshal.Copy(bits, bytes, 0, bytes.Length);
                for (var i = 3; i < bytes.Length; i += 4) bytes[i] = 255;
                return new(bytes, width, height);
            }
            finally
            {
                if (previous is not (0 or -1)) SelectObject(memory, previous);
                if (bitmap != 0) DeleteObject(bitmap);
                if (memory != 0) DeleteDC(memory);
                ReleaseDC(0, screen);
            }
        }
        private static CaptureException Failure(string stage)
        {
            var error = Marshal.GetLastPInvokeError();
            return new(error == 5 ? "native_screen_permission_denied" : "native_screen_failed",
                $"Win32 {stage} could not capture the declared desktop region (OS error {error}). Locked, secure or noninteractive desktops may deny screen access; AvaScope does not bypass that decision.");
        }
        [StructLayout(LayoutKind.Sequential)] private struct BitmapInfo
        { public uint Size; public int Width, Height; public ushort Planes, BitCount; public uint Compression, ImageSize; public int XPels, YPels; public uint Colors, Important; public uint Palette; }
        [DllImport("user32.dll", SetLastError = true)] private static extern nint GetDC(nint window);
        [DllImport("user32.dll")] private static extern int ReleaseDC(nint window, nint dc);
        [DllImport("gdi32.dll", SetLastError = true)] private static extern nint CreateCompatibleDC(nint dc);
        [DllImport("gdi32.dll", SetLastError = true)] private static extern nint CreateDIBSection(nint dc, ref BitmapInfo info, uint usage, out nint bits, nint section, uint offset);
        [DllImport("gdi32.dll", SetLastError = true)] private static extern nint SelectObject(nint dc, nint value);
        [DllImport("gdi32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool BitBlt(nint target, int x, int y, int w, int h, nint source, int sx, int sy, uint operation);
        [DllImport("gdi32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool DeleteObject(nint value);
        [DllImport("gdi32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool DeleteDC(nint dc);
    }

    private static class X11
    {
        internal static Pixels Capture(NodeBounds rect)
        {
            var width = checked((int)rect.Width); var height = checked((int)rect.Height); ValidateSize(width, height);
            var display = XOpenDisplay(null); if (display == 0) throw Failed();
            nint image = 0;
            try
            {
                var screen = XDefaultScreen(display);
                if (rect.X < 0 || rect.Y < 0 || rect.X + width > XDisplayWidth(display, screen) || rect.Y + height > XDisplayHeight(display, screen))
                    throw new CaptureException("native_screen_geometry_changed", "The requested rectangle is outside the current X11 root framebuffer.");
                image = XGetImage(display, XDefaultRootWindow(display), (int)rect.X, (int)rect.Y, (uint)width, (uint)height, nuint.MaxValue, 2);
                if (image == 0) throw Failed();
                var data = Marshal.PtrToStructure<XImage>(image);
                if (data.Width != width || data.Height != height || data.ByteOrder != 0 || data.BitsPerPixel is not (24 or 32)
                    || data.RedMask != 0xff0000 || data.GreenMask != 0xff00 || data.BlueMask != 0xff || data.Data == 0
                    || data.BytesPerLine < width * (data.BitsPerPixel / 8) || data.BytesPerLine > width * 4 + 16)
                    throw new CaptureException("native_screen_format_unsupported", "The X11 root image format is unsupported; no substituted render is returned.");
                var source = new byte[checked(data.BytesPerLine * height)]; Marshal.Copy(data.Data, source, 0, source.Length);
                var bytes = new byte[checked(width * height * 4)];
                for (var y = 0; y < height; y++)
                    for (var x = 0; x < width; x++)
                    {
                        var input = y * data.BytesPerLine + x * (data.BitsPerPixel / 8); var output = (y * width + x) * 4;
                        bytes[output] = source[input]; bytes[output + 1] = source[input + 1]; bytes[output + 2] = source[input + 2]; bytes[output + 3] = 255;
                    }
                return new(bytes, width, height);
            }
            finally { if (image != 0) XDestroyImage(image); XCloseDisplay(display); }
        }
        [StructLayout(LayoutKind.Sequential)] private struct XImage
        {
            public int Width, Height, XOffset, Format; public nint Data;
            public int ByteOrder, BitmapUnit, BitmapBitOrder, BitmapPad, Depth, BytesPerLine, BitsPerPixel;
            public nuint RedMask, GreenMask, BlueMask; public nint ObData;
        }
        [DllImport("libX11.so.6")] private static extern nint XOpenDisplay(string? name);
        [DllImport("libX11.so.6")] private static extern int XCloseDisplay(nint display);
        [DllImport("libX11.so.6")] private static extern int XDefaultScreen(nint display);
        [DllImport("libX11.so.6")] private static extern int XDisplayWidth(nint display, int screen);
        [DllImport("libX11.so.6")] private static extern int XDisplayHeight(nint display, int screen);
        [DllImport("libX11.so.6")] private static extern nuint XDefaultRootWindow(nint display);
        [DllImport("libX11.so.6")] private static extern nint XGetImage(nint display, nuint drawable, int x, int y, uint width, uint height, nuint planes, int format);
        [DllImport("libX11.so.6")] private static extern int XDestroyImage(nint image);
    }

    private static class Mac
    {
        private const string Graphics = "/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics";
        private const string ObjC = "/usr/lib/libobjc.A.dylib";
        private static readonly object Gate = new();
        private static TaskCompletionSource<Pixels>? _pending;
        private static readonly CaptureCallback Callback = Completed;
        private static nint _block;

        internal static Task<Pixels> Capture(NodeBounds rect)
        {
            if (!OperatingSystem.IsMacOSVersionAtLeast(15, 2))
                throw new CaptureException("native_screen_unsupported", "ScreenCaptureKit region capture requires macOS 15.2 or later.");
            if (!CGPreflightScreenCaptureAccess())
                throw new CaptureException("native_screen_permission_denied", "Screen Recording permission is absent. AvaScope does not request permission or bypass the OS decision.");
            lock (Gate)
            {
                if (_pending is not null) throw new CaptureException("native_screen_busy", "A previous OS screen capture callback is still pending.");
                if (_block == 0)
                {
                    // One process-lifetime global Block, not a per-request native allocation. An OS timeout
                    // leaves the one slot occupied until its callback, so late results cannot reach another request.
                    NativeLibrary.Load("/System/Library/Frameworks/ScreenCaptureKit.framework/ScreenCaptureKit");
                    var system = NativeLibrary.Load("/usr/lib/libSystem.B.dylib");
                    var descriptor = Marshal.AllocHGlobal(16); Marshal.WriteInt64(descriptor, 0, 0); Marshal.WriteInt64(descriptor, 8, 32);
                    _block = Marshal.AllocHGlobal(32);
                    Marshal.WriteIntPtr(_block, 0, NativeLibrary.GetExport(system, "_NSConcreteGlobalBlock"));
                    Marshal.WriteInt32(_block, 8, 1 << 28); Marshal.WriteInt32(_block, 12, 0);
                    Marshal.WriteIntPtr(_block, 16, Marshal.GetFunctionPointerForDelegate(Callback)); Marshal.WriteIntPtr(_block, 24, descriptor);
                }
                var type = objc_getClass("SCScreenshotManager"); var selector = sel_registerName("captureImageInRect:completionHandler:");
                if (type == 0 || !Responds(type, sel_registerName("respondsToSelector:"), selector))
                    throw new CaptureException("native_screen_unsupported", "The ScreenCaptureKit region API is unavailable.");
                _pending = new(TaskCreationOptions.RunContinuationsAsynchronously); var task = _pending.Task;
                CaptureImage(type, selector, new(rect.X, rect.Y, rect.Width, rect.Height), _block);
                return task;
            }
        }
        private static void Completed(nint block, nint image, nint error)
        {
            TaskCompletionSource<Pixels>? completion;
            lock (Gate) { completion = _pending; _pending = null; }
            if (completion is null) return;
            try
            {
                if (image == 0 || error != 0) throw Failed();
                var width = checked((int)CGImageGetWidth(image)); var height = checked((int)CGImageGetHeight(image)); ValidateSize(width, height);
                var bytes = new byte[checked(width * height * 4)];
                var pinned = GCHandle.Alloc(bytes, GCHandleType.Pinned); var colorSpace = CGColorSpaceCreateDeviceRGB(); nint context = 0;
                try
                {
                    // Little-endian premultiplied first -> BGRA; Quartz bitmap rows are copied top down.
                    context = CGBitmapContextCreate(pinned.AddrOfPinnedObject(), (nuint)width, (nuint)height, 8, (nuint)(width * 4), colorSpace, 0x2002);
                    if (context == 0) throw Failed();
                    CGContextDrawImage(context, new(0, 0, width, height), image);
                    completion.TrySetResult(new(bytes, width, height));
                }
                finally { if (context != 0) CGContextRelease(context); if (colorSpace != 0) CGColorSpaceRelease(colorSpace); pinned.Free(); }
            }
            catch (Exception exception) { completion.TrySetException(exception is CaptureException ? exception : Failed()); }
        }
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate void CaptureCallback(nint block, nint image, nint error);
        [StructLayout(LayoutKind.Sequential)] private readonly struct Rect(double x, double y, double width, double height)
        { public readonly double X = x, Y = y, Width = width, Height = height; }
        [DllImport(ObjC)] private static extern nint objc_getClass(string name);
        [DllImport(ObjC)] private static extern nint sel_registerName(string name);
        [DllImport(ObjC, EntryPoint = "objc_msgSend")] [return: MarshalAs(UnmanagedType.I1)] private static extern bool Responds(nint receiver, nint selector, nint argument);
        [DllImport(ObjC, EntryPoint = "objc_msgSend")] private static extern void CaptureImage(nint receiver, nint selector, Rect rect, nint block);
        [DllImport(Graphics)] [return: MarshalAs(UnmanagedType.I1)] private static extern bool CGPreflightScreenCaptureAccess();
        [DllImport(Graphics)] private static extern nuint CGImageGetWidth(nint image);
        [DllImport(Graphics)] private static extern nuint CGImageGetHeight(nint image);
        [DllImport(Graphics)] private static extern nint CGColorSpaceCreateDeviceRGB();
        [DllImport(Graphics)] private static extern void CGColorSpaceRelease(nint colorSpace);
        [DllImport(Graphics)] private static extern nint CGBitmapContextCreate(nint data, nuint width, nuint height, nuint bits, nuint stride, nint colorSpace, uint info);
        [DllImport(Graphics)] private static extern void CGContextDrawImage(nint context, Rect rect, nint image);
        [DllImport(Graphics)] private static extern void CGContextRelease(nint context);
    }
}
