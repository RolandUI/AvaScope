using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using AvaScope.Core;
using AvaScope.Protocol;

namespace AvaScope.Bridge;

// Native clients run off the UI thread: providers may marshal requests back to that thread.
internal static partial class NativeAccessibilityReader
{
    private const int WindowsProviderTimeoutMs = 250;

    internal static NativeAccessibilitySnapshot Read(string backend, nint handle, NodeBounds? bounds, NativeAccessibilityAuditRequest request, CancellationToken token)
    {
        try
        {
            if (OperatingSystem.IsWindows() && backend == "win32") return ReadWindows(handle, request, token);
            token.ThrowIfCancellationRequested();
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
        var started = Stopwatch.GetTimestamp(); var stageStarted = started; var stage = "validate_owner";
        int? failedHResult = null; var initialized = false;
        nint automation = 0, walker = 0; var pending = new Queue<(nint Node, string? Parent, int Depth)>();
        var nodes = new List<NativeAccessibilityNode>(); var truncated = false; var diagnostics = new List<ProtocolError>();
        try
        {
            Stage("validate_owner");
            GetWindowThreadProcessId(handle, out var pid);
            if (pid != Environment.ProcessId) return Unavailable("unavailable", adapter, "native_accessibility_owner_mismatch", "The selected HWND does not belong to this bridge process.");
            Stage("initialize_com"); Check(CoInitializeEx(0, 0)); initialized = true;
            var clsid = new Guid("e22ad333-b25f-460c-83d0-0581107395c9"); var iid = new Guid("34723aff-0c9d-49d0-9896-7ab52df8cd8a");
            Stage("create_automation"); Check(CoCreateInstance(ref clsid, 0, 1, ref iid, out automation));
            Stage("configure_auto_focus");
            Check(Call<PutInt>(automation, 59)(automation, 0)); // IUIAutomation2.AutoSetFocus = false.
            Stage("configure_connection_timeout"); Check(Call<PutInt>(automation, 61)(automation, WindowsProviderTimeoutMs));
            Stage("configure_transaction_timeout"); Check(Call<PutInt>(automation, 63)(automation, WindowsProviderTimeoutMs));
            Stage("get_control_view_walker");
            Check(Call<GetPointer>(automation, 14)(automation, out walker));
            Stage("element_from_handle");
            Check(Call<GetRelative>(automation, 6)(automation, handle, out var root));
            if (root == 0) return Unavailable("unavailable", adapter, "native_accessibility_root_unavailable", "UI Automation did not expose the selected window.");
            pending.Enqueue((root, null, 0));
            while (pending.Count > 0 && nodes.Count < request.MaxNodes)
            {
                Stage("dequeue_node"); var entry = pending.Dequeue();
                try
                {
                    // Never inspect identity/text/state or descendants of a foreign native child.
                    if (Integer(entry.Node, 20, "read_process_id") != Environment.ProcessId)
                    { diagnostics.Add(new("native_accessibility_foreign_child_excluded", "An embedded child belongs to another process and was excluded.")); continue; }
                    var id = "uia-" + nodes.Count;
                    var password = Integer(entry.Node, 35, "read_is_password") != 0;
                    Stage("read_bounding_rectangle");
                    Check(Call<GetRectangle>(entry.Node, 43)(entry.Node, out var rectangle));
                    var role = Integer(entry.Node, 21, "read_control_type");
                    nodes.Add(new(id, entry.Parent, String(entry.Node, 29, "read_automation_id"), password ? "[password control]" : String(entry.Node, 23, "read_name"),
                        role is >= 50000 and <= 50038 ? WindowsRoles[role - 50000] : "uia:" + role,
                        Integer(entry.Node, 28, "read_is_enabled") != 0, Integer(entry.Node, 38, "read_is_offscreen") != 0,
                        new(rectangle.Left, rectangle.Top, rectangle.Right - rectangle.Left, rectangle.Bottom - rectangle.Top)));
                    Stage("get_first_child");
                    Check(Call<GetRelative>(walker, 4)(walker, entry.Node, out var child));
                    if (entry.Depth >= request.MaxDepth || nodes.Count + pending.Count >= request.MaxNodes)
                    { truncated |= child != 0; if (child != 0) Marshal.Release(child); continue; }
                    try
                    {
                        while (child != 0)
                        {
                            Stage("queue_child");
                            if (nodes.Count + pending.Count >= request.MaxNodes) { truncated = true; break; }
                            var transferred = child; child = 0; pending.Enqueue((transferred, id, entry.Depth + 1));
                            Stage("get_next_sibling");
                            Check(Call<GetRelative>(walker, 6)(walker, transferred, out child));
                        }
                    }
                    finally { if (child != 0) Marshal.Release(child); }
                }
                finally { Marshal.Release(entry.Node); }
            }
            truncated |= pending.Count > 0;
        }
        // Native HRESULTs can map to specific managed exceptions, not only COMException.
        catch (Exception e) when (e is not OutOfMemoryException and not AccessViolationException)
        {
            var stopped = Stopwatch.GetTimestamp();
            truncated = true;
            diagnostics.Add(WindowsFailure(e, stage, Stopwatch.GetElapsedTime(started, stopped).TotalMilliseconds,
                Stopwatch.GetElapsedTime(stageStarted, stopped).TotalMilliseconds, failedHResult,
                request, token.IsCancellationRequested, nodes.Count, pending.Count));
        }
        finally
        {
            while (pending.TryDequeue(out var item)) Marshal.Release(item.Node);
            if (walker != 0) Marshal.Release(walker); if (automation != 0) Marshal.Release(automation);
            if (initialized) CoUninitialize();
        }
        return new(nodes.Count == 0 ? "unavailable" : diagnostics.Count > 0 ? "partial" : "observed", adapter, "physical_desktop_pixels", DateTimeOffset.UtcNow, nodes, truncated, diagnostics);

        void Stage(string name) { stage = name; stageStarted = Stopwatch.GetTimestamp(); failedHResult = null; token.ThrowIfCancellationRequested(); }
        void Check(int hr)
        {
            // Keep the actual return value even if thread-local IErrorInfo changes the exception.
            if (hr < 0) failedHResult = hr;
            Marshal.ThrowExceptionForHR(hr);
        }
        int Integer(nint element, int slot, string name) { Stage(name); Check(Call<GetInt>(element, slot)(element, out var value)); return value; }
        string? String(nint element, int slot, string name)
        {
            Stage(name); nint value = 0;
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

    private static ProtocolError WindowsFailure(Exception exception, string stage, double elapsedMs, double stageElapsedMs,
        int? returnedHResult, NativeAccessibilityAuditRequest request, bool cancellationRequested, int observedNodes, int pendingNodes)
        => new("native_accessibility_partial", "A native provider was unavailable or exceeded the bounded query; unobserved nodes are not classified as absent.",
            new Dictionary<string, string>
            {
                ["stage"] = stage,
                ["failureKind"] = returnedHResult.HasValue ? "native_error" : exception is OperationCanceledException ? "canceled" : "managed_error",
                ["hresult"] = "0x" + (returnedHResult ?? exception.HResult).ToString("X8", CultureInfo.InvariantCulture),
                ["hresultSource"] = returnedHResult.HasValue ? "native_return" : "managed_exception",
                ["elapsedMs"] = elapsedMs.ToString("0.###", CultureInfo.InvariantCulture),
                ["stageElapsedMs"] = stageElapsedMs.ToString("0.###", CultureInfo.InvariantCulture),
                ["cancellationRequested"] = cancellationRequested ? "true" : "false",
                ["queryTimeoutMs"] = request.TimeoutMs.ToString(CultureInfo.InvariantCulture),
                ["connectionTimeoutMs"] = WindowsProviderTimeoutMs.ToString(CultureInfo.InvariantCulture),
                ["transactionTimeoutMs"] = WindowsProviderTimeoutMs.ToString(CultureInfo.InvariantCulture),
                ["observedNodeCount"] = observedNodes.ToString(CultureInfo.InvariantCulture),
                ["pendingNodeCount"] = pendingNodes.ToString(CultureInfo.InvariantCulture),
                ["nextAction"] = "Inspect the failing stage, HRESULT and cancellation state before changing provider readiness or budgets; do not classify unobserved controls as absent."
            });

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
