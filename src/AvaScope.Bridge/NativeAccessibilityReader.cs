using System.Runtime.InteropServices;
using AvaScope.Core;
using AvaScope.Protocol;

namespace AvaScope.Bridge;

// Native clients run off the UI thread: providers may marshal requests back to that thread.
internal static partial class NativeAccessibilityReader
{
    internal static NativeAccessibilitySnapshot Read(string backend, nint handle, NodeBounds? bounds, NativeAccessibilityAuditRequest request, CancellationToken token)
    {
        try
        {
            token.ThrowIfCancellationRequested();
            if (OperatingSystem.IsWindows() && backend == "win32") return ReadWindows(handle, request, token);
            if (OperatingSystem.IsLinux() && backend == "x11") return ReadLinux(bounds, request, token);
            return Unavailable("unsupported", backend, "native_accessibility_unsupported", "No validated native accessibility adapter is available for this backend; bridge peers are not substituted for OS evidence.");
        }
        catch (Exception e) when (e is not OutOfMemoryException and not AccessViolationException)
        { return Unavailable("unavailable", backend, "native_accessibility_service_unavailable", "The native accessibility service could not be queried. Verify the platform service and application accessibility configuration; no healthy-tree claim is made."); }
    }

    internal static NativeAccessibilitySnapshot Unavailable(string status, string adapter, string code, string message)
        => new(status, adapter, null, DateTimeOffset.UtcNow, [], false, [new(code, message)]);

    private static NativeAccessibilitySnapshot ReadWindows(nint handle, NativeAccessibilityAuditRequest request, CancellationToken token)
    {
        const string adapter = "windows_uia_control_view";
        GetWindowThreadProcessId(handle, out var pid);
        if (pid != Environment.ProcessId) return Unavailable("unavailable", adapter, "native_accessibility_owner_mismatch", "The selected HWND does not belong to this bridge process.");
        var initialized = CoInitializeEx(0, 0); Marshal.ThrowExceptionForHR(initialized);
        nint automation = 0, walker = 0; var pending = new Queue<(nint Node, string? Parent, int Depth)>();
        var nodes = new List<NativeAccessibilityNode>(); var truncated = false; var diagnostics = new List<ProtocolError>();
        try
        {
            var clsid = new Guid("e22ad333-b25f-460c-83d0-0581107395c9"); var iid = new Guid("34723aff-0c9d-49d0-9896-7ab52df8cd8a");
            Marshal.ThrowExceptionForHR(CoCreateInstance(ref clsid, 0, 1, ref iid, out automation));
            Check(Call<PutInt>(automation, 59)(automation, 0)); // IUIAutomation2.AutoSetFocus = false.
            Check(Call<PutInt>(automation, 61)(automation, 250));
            Check(Call<PutInt>(automation, 63)(automation, 250));
            Check(Call<GetPointer>(automation, 14)(automation, out walker));
            Check(Call<GetRelative>(automation, 6)(automation, handle, out var root));
            if (root == 0) return Unavailable("unavailable", adapter, "native_accessibility_root_unavailable", "UI Automation did not expose the selected window.");
            pending.Enqueue((root, null, 0));
            while (pending.Count > 0 && nodes.Count < request.MaxNodes)
            {
                token.ThrowIfCancellationRequested(); var entry = pending.Dequeue();
                try
                {
                    // Never inspect identity/text/state or descendants of a foreign native child.
                    if (Integer(entry.Node, 20) != Environment.ProcessId)
                    { diagnostics.Add(new("native_accessibility_foreign_child_excluded", "An embedded child belongs to another process and was excluded.")); continue; }
                    var id = "uia-" + nodes.Count;
                    var password = Integer(entry.Node, 35) != 0;
                    Check(Call<GetRectangle>(entry.Node, 43)(entry.Node, out var rectangle));
                    var role = Integer(entry.Node, 21);
                    nodes.Add(new(id, entry.Parent, String(entry.Node, 29), password ? "[password control]" : String(entry.Node, 23),
                        role is >= 50000 and <= 50038 ? WindowsRoles[role - 50000] : "uia:" + role,
                        Integer(entry.Node, 28) != 0, Integer(entry.Node, 38) != 0,
                        new(rectangle.Left, rectangle.Top, rectangle.Right - rectangle.Left, rectangle.Bottom - rectangle.Top)));
                    Check(Call<GetRelative>(walker, 4)(walker, entry.Node, out var child));
                    if (entry.Depth >= request.MaxDepth || nodes.Count + pending.Count >= request.MaxNodes)
                    { truncated |= child != 0; if (child != 0) Marshal.Release(child); continue; }
                    try
                    {
                        while (child != 0)
                        {
                            token.ThrowIfCancellationRequested();
                            if (nodes.Count + pending.Count >= request.MaxNodes) { truncated = true; break; }
                            var transferred = child; child = 0; pending.Enqueue((transferred, id, entry.Depth + 1));
                            Check(Call<GetRelative>(walker, 6)(walker, transferred, out child));
                        }
                    }
                    finally { if (child != 0) Marshal.Release(child); }
                }
                finally { Marshal.Release(entry.Node); }
            }
            truncated |= pending.Count > 0;
        }
        catch (Exception e) when (e is COMException or OperationCanceledException)
        { truncated = true; diagnostics.Add(new("native_accessibility_partial", "A native provider was unavailable or exceeded the bounded query; unobserved nodes are not classified as absent.")); }
        finally
        {
            while (pending.TryDequeue(out var item)) Marshal.Release(item.Node);
            if (walker != 0) Marshal.Release(walker); if (automation != 0) Marshal.Release(automation); CoUninitialize();
        }
        return new(nodes.Count == 0 ? "unavailable" : diagnostics.Count > 0 ? "partial" : "observed", adapter, "physical_desktop_pixels", DateTimeOffset.UtcNow, nodes, truncated, diagnostics);

        void Check(int hr) => Marshal.ThrowExceptionForHR(hr);
        int Integer(nint element, int slot) { token.ThrowIfCancellationRequested(); Check(Call<GetInt>(element, slot)(element, out var value)); return value; }
        string? String(nint element, int slot)
        {
            token.ThrowIfCancellationRequested(); nint value = 0;
            try
            {
                Check(Call<GetPointer>(element, slot)(element, out value));
                if (value == 0) return null;
                var length = SysStringLen(value);
                // Never turn a truncated identity into a supposedly exact AutomationId match.
                return slot == 29 && length > 256 ? null : Marshal.PtrToStringUni(value, (int)Math.Min(length, 256));
            }
            finally { if (value != 0) Marshal.FreeBSTR(value); }
        }
    }

    // Public Windows SDK UIAutomationClient.h vtable slots, never Avalonia internals.
    private static T Call<T>(nint instance, int slot) where T : Delegate => Marshal.GetDelegateForFunctionPointer<T>(Marshal.ReadIntPtr(Marshal.ReadIntPtr(instance), slot * IntPtr.Size));
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int GetPointer(nint self, out nint value);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int GetRelative(nint self, nint element, out nint value);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int GetInt(nint self, out int value);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int PutInt(nint self, int value);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int GetRectangle(nint self, out NativeRect value);
    [StructLayout(LayoutKind.Sequential)] private struct NativeRect { public int Left, Top, Right, Bottom; }
    [DllImport("ole32.dll")] private static extern int CoInitializeEx(nint reserved, uint flags);
    [DllImport("ole32.dll")] private static extern void CoUninitialize();
    [DllImport("ole32.dll")] private static extern int CoCreateInstance(ref Guid clsid, nint outer, uint context, ref Guid iid, out nint instance);
    [DllImport("oleaut32.dll")] private static extern uint SysStringLen(nint value);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(nint handle, out uint process);
    private static readonly string[] WindowsRoles = ["Button", "Calendar", "CheckBox", "ComboBox", "Edit", "Hyperlink", "Image", "ListItem", "List", "Menu", "MenuBar", "MenuItem", "ProgressBar", "RadioButton", "ScrollBar", "Slider", "Spinner", "StatusBar", "Tab", "TabItem", "Text", "ToolBar", "ToolTip", "Tree", "TreeItem", "Custom", "Group", "Thumb", "DataGrid", "DataItem", "Document", "SplitButton", "Window", "Pane", "Header", "HeaderItem", "Table", "TitleBar", "Separator"];
}
