using System.Diagnostics;
using System.Runtime.InteropServices;
using Avalonia.Controls;
using Avalonia.Threading;
using AvaScope.Core;
using AvaScope.Protocol;

namespace AvaScope.Bridge;

public sealed partial class AvaScopeBridgeRuntime
{
    public async Task<CoreResult<NativePickerResponse>> NativePickerAsync(RuntimeNativePickerRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.TopLevelId is null || request.Operation is NativePickerOperations.PredefineResult or NativePickerOperations.ConsumePredefinedResult)
            return await Task.Run(() => new LocalBridgeClient(Path.GetDirectoryName(SessionManifestPath)!).ExecuteHostedPicker(SessionId, request), cancellationToken);
        if (request.TimeoutMs is < 0 or > 3000 || string.IsNullOrWhiteSpace(request.TopLevelId)
            || request.Operation is not (NativePickerOperations.Detect or NativePickerOperations.SelectPath or NativePickerOperations.Confirm or NativePickerOperations.Cancel))
            return Fail("Native picker requests require an explicit topLevelId, detect/select_path/confirm/cancel and timeoutMs 0..3000.");
        if (request.Operation == NativePickerOperations.SelectPath && (string.IsNullOrWhiteSpace(request.Path)
            || request.Path.Length > 4096 || request.Path.Contains('\0') || !Path.IsPathFullyQualified(request.Path)))
            return Fail("Native picker select_path requires an absolute path of at most 4096 characters without NUL.");

        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromMilliseconds(Math.Max(request.TimeoutMs, 100)));
        var token = deadline.Token;
        Window? owner = null;
        nint handle = 0;
        string? backend = null;
        EventHandler closed = (_, _) => deadline.Cancel();
        try
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                owner = FindTopLevel(request.TopLevelId) as Window ?? throw new InvalidOperationException("The selected registered native Window was not found.");
                backend = RuntimePlatformEvidence.Observe(owner).Backend;
                handle = owner.TryGetPlatformHandle()?.Handle ?? 0;
                if (handle == 0 || backend is not ("win32" or "x11" or "macos"))
                    throw new NotSupportedException("This top-level has no supported native dialog backend. Predefined results remain available through the explicit host hook.");
                owner.Closed += closed;
            }, DispatcherPriority.Background, token);
            if (backend == "win32")
                return await Task.Run(() => new LocalBridgeClient(Path.GetDirectoryName(SessionManifestPath)!)
                    .NativePicker(SessionId, request.Operation, request.Path, timeoutMs: request.TimeoutMs, redactPath: request.RedactPath), token);

            NativeDialogState current;
            var timer = Stopwatch.StartNew();
            do
            {
                current = await ReadOrAct("detect");
                if (current.Found || request.TimeoutMs == 0) break;
                await Task.Delay(25, token);
            } while (timer.ElapsedMilliseconds < request.TimeoutMs);
            if (!current.Found)
            {
                if (request.Operation != NativePickerOperations.Detect)
                    return Fail("No supported dialog belongs to the selected window. X11 supports in-process GTK3 choosers; portal dialogs require request ownership integration and are never discovered in unrelated processes.");
                return Ok("not_found", false);
            }
            if (request.Operation == NativePickerOperations.Detect) return Ok("detected", true);
            current = await ReadOrAct(request.Operation);
            if (request.Operation == NativePickerOperations.SelectPath)
            {
                while (current.Found && !PathsEqual(current.Path, request.Path))
                {
                    await Task.Delay(25, token);
                    current = await ReadOrAct("detect");
                }
                if (!current.Found) return Fail("The owned picker closed while its path was being selected.");
                return Ok("path_selected", true, request.RedactPath ? "[redacted]" : request.Path);
            }
            while (current.Found)
            {
                await Task.Delay(25, token);
                current = await ReadOrAct("detect");
            }
            return Ok(request.Operation == NativePickerOperations.Cancel ? "cancelled" : "confirmed", false);
        }
        catch (Exception exception) when (exception is InvalidOperationException or NotSupportedException or OperationCanceledException
            or DllNotFoundException or EntryPointNotFoundException or IOException or UnauthorizedAccessException)
        {
            return Fail(exception is OperationCanceledException
                ? "Native picker operation timed out, was cancelled, or its owner closed. An already dispatched selection/confirmation may have taken effect; observe before retrying."
                : exception.Message);
        }
        finally
        {
            if (owner is not null)
                await Dispatcher.UIThread.InvokeAsync(() => owner.Closed -= closed, DispatcherPriority.Send);
        }

        Task<NativeDialogState> ReadOrAct(string operation) => backend == "x11"
            ? GtkPicker.InvokeAsync(() => GtkPicker.Execute(handle, operation, request.Path, token), token)
            : Dispatcher.UIThread.InvokeAsync(() =>
            {
                token.ThrowIfCancellationRequested();
                if (FindTopLevel(request.TopLevelId) != owner || owner!.TryGetPlatformHandle()?.Handle != handle)
                    throw new InvalidOperationException("The picker owner is no longer registered with its original native handle.");
                return MacPicker(handle, operation, request.Path);
            }, DispatcherPriority.Background, token).GetTask();

        CoreResult<NativePickerResponse> Ok(string status, bool found, string? path = null) => CoreResult<NativePickerResponse>.Ok(new(
            SessionId, Environment.ProcessId, request.Operation, status, found, path,
            "Native dialog belongs to the explicitly selected window; no predefined result or unrelated process was used.",
            pathRedacted: path is not null && request.RedactPath, route: backend == "x11" ? "gtk3_owned_native_dialog" : "appkit_owned_native_dialog"));
        CoreResult<NativePickerResponse> Fail(string message) => CoreResult<NativePickerResponse>.Fail(new CoreError(
            CoreErrorCodes.InvalidBridgeRequest, message, new Dictionary<string, string>
            { ["scope"] = "selected_application_window", ["topLevelId"] = request.TopLevelId, ["requestedStrategy"] = "native_dialog", ["fallback"] = "none" }));
    }

    private static bool PathsEqual(string? actual, string? expected) => actual is not null && expected is not null
        && string.Equals(Path.GetFullPath(actual), Path.GetFullPath(expected), OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);

    private sealed record NativeDialogState(bool Found, string? Path = null);

    private static NativeDialogState MacPicker(nint owner, string operation, string? path)
    {
        var sheet = NativeWindowInput.Mac.Send(owner, "attachedSheet");
        var panelClass = NativeWindowInput.Mac.Class("NSSavePanel");
        if (sheet == 0 || NativeWindowInput.Mac.SendArg(sheet, NativeWindowInput.Mac.Selector("isKindOfClass:"), panelClass) == 0)
            return new(false);
        var isOpen = NativeWindowInput.Mac.SendArg(sheet, NativeWindowInput.Mac.Selector("isKindOfClass:"), NativeWindowInput.Mac.Class("NSOpenPanel")) != 0;
        if (operation == NativePickerOperations.Cancel) NativeWindowInput.Mac.SendArg(sheet, NativeWindowInput.Mac.Selector("cancel:"), 0);
        if (operation == NativePickerOperations.Confirm)
            throw new NotSupportedException("macOS hosts file panels out of process and does not permit programmatic confirmation through NSSavePanel.ok:. Cancel the native panel and use the explicit host-authorized predefined-result hook for deterministic app-logic coverage. No confirmation or global input was dispatched.");
        if (operation == NativePickerOperations.SelectPath)
        {
            if (isOpen)
                throw new NotSupportedException("AppKit open/folder panels support detect/cancel. Programmatic select_path is supported for save panels only; use the explicit predefined-result hook for deterministic open/folder selection.");
            var directory = Path.GetDirectoryName(path!);
            if (!Directory.Exists(directory)) throw new InvalidOperationException("The save panel's requested parent directory does not exist.");
            var url = NativeWindowInput.Mac.SendArg(NativeWindowInput.Mac.Class("NSURL"), NativeWindowInput.Mac.Selector("fileURLWithPath:"), NativeWindowInput.Mac.String(directory!));
            NativeWindowInput.Mac.SendArg(sheet, NativeWindowInput.Mac.Selector("setDirectoryURL:"), url);
            NativeWindowInput.Mac.SendArg(sheet, NativeWindowInput.Mac.Selector("setNameFieldStringValue:"), NativeWindowInput.Mac.String(Path.GetFileName(path!)));
        }
        var selected = NativeWindowInput.Mac.Send(NativeWindowInput.Mac.Send(sheet, "URL"), "path");
        if (!isOpen)
        {
            // Remote panels may not publish URL until user confirmation. Verify
            // the public editable directory/name configuration before that point.
            var directory = ReadMacString(NativeWindowInput.Mac.Send(NativeWindowInput.Mac.Send(sheet, "directoryURL"), "path"));
            var name = ReadMacString(NativeWindowInput.Mac.Send(sheet, "nameFieldStringValue"));
            return new(true, directory is not null && name is not null ? Path.Combine(directory, name) : null);
        }
        return new(true, ReadMacString(selected));
    }

    private static string? ReadMacString(nint selected)
    {
        var utf8 = selected == 0 ? 0 : NativeWindowInput.Mac.Send(selected, "UTF8String");
        return utf8 == 0 ? null : Marshal.PtrToStringUTF8(utf8);
    }

    private static class GtkPicker
    {
        private const string Gtk = "libgtk-3.so.0";
        private const string Gdk = "libgdk-3.so.0";
        private const string Glib = "libglib-2.0.so.0";
        private static readonly SourceCallback Callback = Dispatch;
        private static readonly DestroyCallback Destroy = Free;

        internal static async Task<NativeDialogState> InvokeAsync(Func<NativeDialogState> action, CancellationToken token)
        {
            var work = new GtkWork(action, token);
            var source = g_idle_source_new();
            var state = GCHandle.Alloc(work);
            try
            {
                g_source_set_callback(source, Callback, GCHandle.ToIntPtr(state), Destroy);
                g_source_attach(source, 0);
                return await work.Completion.Task.WaitAsync(token);
            }
            finally { g_source_destroy(source); g_source_unref(source); }
        }

        private static int Dispatch(nint data)
        {
            var work = (GtkWork)GCHandle.FromIntPtr(data).Target!;
            try { work.Token.ThrowIfCancellationRequested(); work.Completion.TrySetResult(work.Action()); }
            catch (Exception exception) { work.Completion.TrySetException(exception); }
            return 0;
        }
        private static void Free(nint data) => GCHandle.FromIntPtr(data).Free();
        private sealed record GtkWork(Func<NativeDialogState> Action, CancellationToken Token)
        { public TaskCompletionSource<NativeDialogState> Completion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously); }

        internal static NativeDialogState Execute(nint owner, string operation, string? path, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            var list = gtk_window_list_toplevels();
            nint selected = 0;
            try
            {
                var cursor = list;
                for (var count = 0; cursor != 0; count++)
                {
                    if (count >= 256) throw new InvalidOperationException("The process GTK window list exceeds the bounded native dialog scan.");
                    var value = Marshal.ReadIntPtr(cursor);
                    cursor = Marshal.ReadIntPtr(cursor, IntPtr.Size);
                    if (gtk_widget_get_visible(value) == 0 || g_type_check_instance_is_a(value, gtk_file_chooser_get_type()) == 0
                        || g_type_check_instance_is_a(value, gtk_dialog_get_type()) == 0) continue;
                    var window = gtk_widget_get_window(value);
                    var display = gdk_x11_display_get_xdisplay(gdk_window_get_display(window));
                    if (display == 0 || XGetTransientForHint(display, gdk_x11_window_get_xid(window), out var parent) == 0 || parent != owner) continue;
                    if (selected != 0) throw new InvalidOperationException("More than one GTK chooser belongs to the selected window; close the ambiguous dialog first.");
                    selected = value;
                }
            }
            finally { g_list_free(list); }
            if (selected == 0) return new(false);
            token.ThrowIfCancellationRequested();
            if (operation == NativePickerOperations.SelectPath)
            {
                if (gtk_file_chooser_get_action(selected) == 1)
                {
                    var directory = Path.GetDirectoryName(path!);
                    if (!Directory.Exists(directory)) throw new InvalidOperationException("The save picker's requested parent directory does not exist.");
                    gtk_file_chooser_set_current_folder(selected, directory!);
                    gtk_file_chooser_set_current_name(selected, Path.GetFileName(path!));
                }
                else
                {
                    if (!File.Exists(path) && !Directory.Exists(path)) throw new InvalidOperationException("The picker selection path does not exist.");
                    gtk_file_chooser_set_filename(selected, path!);
                }
            }
            if (operation is NativePickerOperations.Cancel or NativePickerOperations.Confirm)
            {
                gtk_dialog_response(selected, operation == NativePickerOperations.Cancel ? -6 : -3);
                return new(true); // The caller separately observes closure on the GLib thread.
            }
            var file = gtk_file_chooser_get_filename(selected);
            try { return new(true, file == 0 ? null : Marshal.PtrToStringUTF8(file)); }
            finally { if (file != 0) g_free(file); }
        }

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int SourceCallback(nint data);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate void DestroyCallback(nint data);
        [DllImport(Glib)] private static extern nint g_idle_source_new();
        [DllImport(Glib)] private static extern void g_source_set_callback(nint source, SourceCallback callback, nint data, DestroyCallback destroy);
        [DllImport(Glib)] private static extern uint g_source_attach(nint source, nint context);
        [DllImport(Glib)] private static extern void g_source_destroy(nint source);
        [DllImport(Glib)] private static extern void g_source_unref(nint source);
        [DllImport(Glib)] private static extern void g_list_free(nint list);
        [DllImport(Glib)] private static extern void g_free(nint value);
        [DllImport(Gtk)] private static extern nint gtk_window_list_toplevels();
        [DllImport(Gtk)] private static extern int gtk_widget_get_visible(nint widget);
        [DllImport(Gtk)] private static extern nint gtk_widget_get_window(nint widget);
        [DllImport(Gtk)] private static extern nuint gtk_file_chooser_get_type();
        [DllImport(Gtk)] private static extern nuint gtk_dialog_get_type();
        [DllImport("libgobject-2.0.so.0")] private static extern int g_type_check_instance_is_a(nint instance, nuint type);
        [DllImport(Gdk)] private static extern nint gdk_window_get_display(nint window);
        [DllImport(Gdk)] private static extern nint gdk_x11_display_get_xdisplay(nint display);
        [DllImport(Gdk)] private static extern nint gdk_x11_window_get_xid(nint window);
        [DllImport("libX11.so.6")] private static extern int XGetTransientForHint(nint display, nint window, out nint owner);
        [DllImport(Gtk)] private static extern int gtk_file_chooser_get_action(nint chooser);
        [DllImport(Gtk)] private static extern int gtk_file_chooser_set_filename(nint chooser, [MarshalAs(UnmanagedType.LPUTF8Str)] string path);
        [DllImport(Gtk)] private static extern int gtk_file_chooser_set_current_folder(nint chooser, [MarshalAs(UnmanagedType.LPUTF8Str)] string path);
        [DllImport(Gtk)] private static extern void gtk_file_chooser_set_current_name(nint chooser, [MarshalAs(UnmanagedType.LPUTF8Str)] string path);
        [DllImport(Gtk)] private static extern nint gtk_file_chooser_get_filename(nint chooser);
        [DllImport(Gtk)] private static extern void gtk_dialog_response(nint dialog, int response);
    }
}
