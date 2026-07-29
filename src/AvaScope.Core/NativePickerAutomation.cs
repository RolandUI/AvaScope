using System.Runtime.InteropServices;
using System.Text;
using AvaScope.Protocol;

namespace AvaScope.Core;

internal static class NativePickerAutomation
{
    private const uint WmSetText = 0x000C;
    private const uint WmCommand = 0x0111;
    private const int IdOk = 1;
    private const int IdCancel = 2;

    public static CoreResult<NativePickerResponse> Execute(
        BridgeSessionManifest manifest,
        string operation,
        string? path,
        string? predefinedResult)
    {
        if (operation == NativePickerOperations.PredefineResult)
        {
            var state = predefinedResult ?? NativePickerResultStates.Success;
            var allowed = new[]
            {
                NativePickerResultStates.Success,
                NativePickerResultStates.Cancelled,
                NativePickerResultStates.UnavailablePath,
                NativePickerResultStates.DeletedPath
            };
            return allowed.Contains(state, StringComparer.Ordinal)
                ? CoreResult<NativePickerResponse>.Ok(new NativePickerResponse(
                    manifest.SessionId, manifest.ProcessId, operation, state, false, path,
                    "Deterministic picker result prepared for an isolated runtime scenario."))
                : Fail("Picker result must be success, cancelled, unavailable_path, or deleted_path.");
        }

        if (!OperatingSystem.IsWindows())
        {
            return CoreResult<NativePickerResponse>.Fail(new CoreError(
                CoreErrorCodes.InvalidBridgeRequest,
                "Native picker automation is supported only on Windows.",
                new Dictionary<string, string> { ["platform"] = RuntimeInformation.OSDescription }));
        }

        var dialog = FindDialog(manifest.ProcessId);
        if (operation == NativePickerOperations.Detect)
        {
            return CoreResult<NativePickerResponse>.Ok(new NativePickerResponse(
                manifest.SessionId, manifest.ProcessId, operation,
                dialog == IntPtr.Zero ? "not_found" : "detected",
                dialog != IntPtr.Zero));
        }

        if (dialog == IntPtr.Zero)
        {
            return Fail("No native file or folder picker owned by the selected process is open.");
        }

        if (operation == NativePickerOperations.SelectPath)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return Fail("select_path requires path.");
            }

            var edit = FindChild(dialog, "Edit");
            if (edit == IntPtr.Zero)
            {
                return Fail("The owned picker does not expose a supported path edit control.");
            }

            SendMessage(edit, WmSetText, IntPtr.Zero, path);
            return CoreResult<NativePickerResponse>.Ok(new NativePickerResponse(
                manifest.SessionId, manifest.ProcessId, operation, "path_selected", true, path));
        }

        var command = operation switch
        {
            NativePickerOperations.Confirm => IdOk,
            NativePickerOperations.Cancel => IdCancel,
            _ => 0
        };
        if (command == 0)
        {
            return Fail("Picker operation must be detect, select_path, confirm, cancel, or predefine_result.");
        }

        SendMessage(dialog, WmCommand, new IntPtr(command), IntPtr.Zero);
        return CoreResult<NativePickerResponse>.Ok(new NativePickerResponse(
            manifest.SessionId, manifest.ProcessId, operation,
            operation == NativePickerOperations.Confirm ? "confirmed" : "cancelled",
            true));

        CoreResult<NativePickerResponse> Fail(string message) =>
            CoreResult<NativePickerResponse>.Fail(new CoreError(
                CoreErrorCodes.InvalidBridgeRequest,
                message,
                new Dictionary<string, string>
                {
                    ["processId"] = manifest.ProcessId.ToString(),
                    ["scope"] = "selected_process_only"
                }));
    }

    private static IntPtr FindDialog(int processId)
    {
        var result = IntPtr.Zero;
        EnumWindows((window, _) =>
        {
            GetWindowThreadProcessId(window, out var ownerProcessId);
            if (ownerProcessId != (uint)processId || !IsWindowVisible(window))
            {
                return true;
            }

            var className = GetClassName(window);
            if (className is "#32770" or "CabinetWClass")
            {
                result = window;
                return false;
            }

            return true;
        }, IntPtr.Zero);
        return result;
    }

    private static IntPtr FindChild(IntPtr parent, string expectedClass)
    {
        var result = IntPtr.Zero;
        EnumChildWindows(parent, (window, _) =>
        {
            if (string.Equals(GetClassName(window), expectedClass, StringComparison.Ordinal))
            {
                result = window;
                return false;
            }
            return true;
        }, IntPtr.Zero);
        return result;
    }

    private static string GetClassName(IntPtr window)
    {
        var value = new StringBuilder(256);
        _ = GetClassName(window, value, value.Capacity);
        return value.ToString();
    }

    private delegate bool EnumWindowsProc(IntPtr window, IntPtr parameter);
    [DllImport("user32.dll")] private static extern bool EnumWindows(EnumWindowsProc callback, IntPtr parameter);
    [DllImport("user32.dll")] private static extern bool EnumChildWindows(IntPtr parent, EnumWindowsProc callback, IntPtr parameter);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr window);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetClassName(IntPtr window, StringBuilder className, int maximumCount);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr SendMessage(IntPtr window, uint message, IntPtr wParam, string? lParam);
    [DllImport("user32.dll")] private static extern IntPtr SendMessage(IntPtr window, uint message, IntPtr wParam, IntPtr lParam);
}
