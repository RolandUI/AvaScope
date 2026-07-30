using System.Globalization;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AvaScope.Protocol;

namespace AvaScope.Core;

internal static class NativePickerAutomation
{
    private const uint WmSetText = 0x000C;
    private const uint WmCommand = 0x0111;
    private const uint SmtoAbortIfHung = 0x0002;
    private const uint GwOwner = 4;
    private const int IdOk = 1;
    private const int IdCancel = 2;
    private const int FileNameEditId = 0x0480;
    private const int FileNameComboId = 0x047C;
    private const int DefaultTimeoutMs = 1000;
    private const int MaximumTimeoutMs = 30000;
    private const int DefaultTtlMs = 30000;
    private const int MaximumTtlMs = 300000;
    private static readonly JsonSerializerOptions StoreJsonOptions = new(JsonSerializerDefaults.Web);

    public static CoreResult<NativePickerResponse> Execute(
        BridgeSessionManifest manifest,
        string manifestDirectory,
        string operation,
        string? path,
        string? predefinedResult,
        string? correlationId,
        int ttlMs,
        int timeoutMs,
        bool redactPath)
    {
        ArgumentNullException.ThrowIfNull(manifest);

        var normalizedCorrelationId = string.IsNullOrWhiteSpace(correlationId)
            ? "default"
            : correlationId.Trim();
        if (operation == NativePickerOperations.PredefineResult)
        {
            return PrepareResult(
                manifest,
                manifestDirectory,
                path,
                predefinedResult,
                normalizedCorrelationId,
                ttlMs,
                redactPath);
        }

        if (operation == NativePickerOperations.ConsumePredefinedResult)
        {
            return ConsumeResult(
                manifest,
                manifestDirectory,
                normalizedCorrelationId,
                redactPath);
        }

        if (!OperatingSystem.IsWindows())
        {
            return CoreResult<NativePickerResponse>.Fail(new CoreError(
                CoreErrorCodes.InvalidBridgeRequest,
                "Native picker automation is supported only on Windows.",
                new Dictionary<string, string> { ["platform"] = RuntimeInformation.OSDescription }));
        }

        var boundedTimeoutMs = Math.Clamp(timeoutMs < 0 ? DefaultTimeoutMs : timeoutMs, 0, MaximumTimeoutMs);
        var dialog = WaitForDialog(manifest.ProcessId, boundedTimeoutMs);
        if (operation == NativePickerOperations.Detect)
        {
            return CoreResult<NativePickerResponse>.Ok(new NativePickerResponse(
                manifest.SessionId,
                manifest.ProcessId,
                operation,
                dialog.Handle == IntPtr.Zero ? "not_found" : "detected",
                dialog.Handle != IntPtr.Zero,
                message: dialog.Handle == IntPtr.Zero
                    ? $"No owned picker was detected within {boundedTimeoutMs.ToString(CultureInfo.InvariantCulture)} ms."
                    : $"Detected owned {dialog.ClassName} picker."));
        }

        if (dialog.Handle == IntPtr.Zero)
        {
            return Fail("No native file or folder picker owned by the selected process is open.");
        }

        if (!IsOwnedDialog(dialog.Handle, manifest.ProcessId))
        {
            return Fail("The detected picker no longer belongs to the selected session process.");
        }

        if (operation == NativePickerOperations.SelectPath)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return Fail("select_path requires path.");
            }

            var edit = FindPathEdit(dialog.Handle);
            if (edit == IntPtr.Zero)
            {
                return Fail("The owned picker does not expose a supported path edit control.");
            }

            if (!TrySendText(edit, path, boundedTimeoutMs))
            {
                return Fail("The owned picker did not accept the path before the timeout.");
            }

            return CoreResult<NativePickerResponse>.Ok(new NativePickerResponse(
                manifest.SessionId,
                manifest.ProcessId,
                operation,
                "path_selected",
                true,
                DisplayPath(path, redactPath),
                pathRedacted: redactPath));
        }

        var command = operation switch
        {
            NativePickerOperations.Confirm => IdOk,
            NativePickerOperations.Cancel => IdCancel,
            _ => 0
        };
        if (command == 0)
        {
            return Fail("Picker operation must be detect, select_path, confirm, cancel, predefine_result, or consume_predefined_result.");
        }

        if (!PostMessage(dialog.Handle, WmCommand, new IntPtr(command), IntPtr.Zero))
        {
            return Fail($"The owned picker did not accept the {operation} command.");
        }

        var closed = WaitForDialogClose(dialog.Handle, boundedTimeoutMs);
        if (!closed)
        {
            return Fail($"The {operation} command was delivered, but the owned picker remained open at the timeout.");
        }

        return CoreResult<NativePickerResponse>.Ok(new NativePickerResponse(
            manifest.SessionId,
            manifest.ProcessId,
            operation,
            operation == NativePickerOperations.Confirm ? "confirmed" : "cancelled",
            false,
            message: "The owned picker closed after the command."));

        CoreResult<NativePickerResponse> Fail(string message) =>
            CoreResult<NativePickerResponse>.Fail(new CoreError(
                CoreErrorCodes.InvalidBridgeRequest,
                message,
                new Dictionary<string, string>
                {
                    ["processId"] = manifest.ProcessId.ToString(CultureInfo.InvariantCulture),
                    ["scope"] = "selected_process_only",
                    ["timeoutMs"] = boundedTimeoutMs.ToString(CultureInfo.InvariantCulture)
                }));
    }

    private static CoreResult<NativePickerResponse> PrepareResult(
        BridgeSessionManifest manifest,
        string manifestDirectory,
        string? path,
        string? predefinedResult,
        string correlationId,
        int ttlMs,
        bool redactPath)
    {
        var state = predefinedResult ?? NativePickerResultStates.Success;
        var allowed = NativePickerResultStates.Preparable;
        if (!allowed.Contains(state, StringComparer.Ordinal))
        {
            return FailPrepared("Picker result must be success, cancelled, unavailable_path, or deleted_path.");
        }

        var boundedTtlMs = ttlMs <= 0 ? DefaultTtlMs : ttlMs;
        if (boundedTtlMs is < 100 or > MaximumTtlMs)
        {
            return FailPrepared($"Picker result TTL must be between 100 and {MaximumTtlMs.ToString(CultureInfo.InvariantCulture)} ms.");
        }

        try
        {
            var now = DateTimeOffset.UtcNow;
            var entry = new PreparedPickerResult(
                manifest.SessionId.Value,
                manifest.ProcessId,
                correlationId,
                state,
                path,
                now,
                now.AddMilliseconds(boundedTtlMs));
            WritePreparedResult(manifestDirectory, entry);
            return CoreResult<NativePickerResponse>.Ok(new NativePickerResponse(
                manifest.SessionId,
                manifest.ProcessId,
                NativePickerOperations.PredefineResult,
                state,
                false,
                DisplayPath(path, redactPath),
                "Stored a session-scoped one-shot picker result for an isolated runtime scenario.",
                correlationId,
                entry.ExpiresAt,
                pathRedacted: redactPath && !string.IsNullOrWhiteSpace(path)));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            return FailPrepared($"Picker result could not be stored: {exception.Message}");
        }

        CoreResult<NativePickerResponse> FailPrepared(string message) =>
            CoreResult<NativePickerResponse>.Fail(new CoreError(
                CoreErrorCodes.InvalidBridgeRequest,
                message,
                new Dictionary<string, string>
                {
                    ["sessionId"] = manifest.SessionId.Value,
                    ["correlationId"] = correlationId
                }));
    }

    private static CoreResult<NativePickerResponse> ConsumeResult(
        BridgeSessionManifest manifest,
        string manifestDirectory,
        string correlationId,
        bool redactPath)
    {
        try
        {
            var entry = TakePreparedResult(manifestDirectory, manifest.SessionId, correlationId);
            if (entry is null)
            {
                return CoreResult<NativePickerResponse>.Ok(new NativePickerResponse(
                    manifest.SessionId,
                    manifest.ProcessId,
                    NativePickerOperations.ConsumePredefinedResult,
                    NativePickerResultStates.NotPrepared,
                    false,
                    correlationId: correlationId,
                    message: "No unconsumed picker result is prepared for this session and correlation id."));
            }

            var consumedAt = DateTimeOffset.UtcNow;
            if (entry.ExpiresAt <= consumedAt)
            {
                return CoreResult<NativePickerResponse>.Ok(new NativePickerResponse(
                    manifest.SessionId,
                    manifest.ProcessId,
                    NativePickerOperations.ConsumePredefinedResult,
                    NativePickerResultStates.Expired,
                    false,
                    correlationId: correlationId,
                    expiresAt: entry.ExpiresAt,
                    consumedAt: consumedAt,
                    message: "The prepared picker result expired before it was consumed."));
            }

            return CoreResult<NativePickerResponse>.Ok(new NativePickerResponse(
                manifest.SessionId,
                manifest.ProcessId,
                NativePickerOperations.ConsumePredefinedResult,
                entry.Result,
                false,
                DisplayPath(entry.Path, redactPath),
                "Consumed the prepared picker result; it cannot be replayed.",
                correlationId,
                entry.ExpiresAt,
                consumedAt,
                redactPath && !string.IsNullOrWhiteSpace(entry.Path)));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            return CoreResult<NativePickerResponse>.Fail(new CoreError(
                CoreErrorCodes.InvalidBridgeRequest,
                $"Picker result could not be consumed: {exception.Message}",
                new Dictionary<string, string>
                {
                    ["sessionId"] = manifest.SessionId.Value,
                    ["correlationId"] = correlationId
                }));
        }
    }

    private static void WritePreparedResult(string manifestDirectory, PreparedPickerResult entry)
    {
        var directory = GetStoreDirectory(manifestDirectory);
        Directory.CreateDirectory(directory);
        var destination = GetStorePath(directory, new SessionId(entry.SessionId), entry.CorrelationId);
        var temporary = destination + "." + Guid.NewGuid().ToString("n") + ".tmp";
        try
        {
            File.WriteAllText(temporary, JsonSerializer.Serialize(entry, StoreJsonOptions));
            File.Move(temporary, destination, true);
        }
        finally
        {
            if (File.Exists(temporary))
            {
                File.Delete(temporary);
            }
        }
    }

    private static PreparedPickerResult? TakePreparedResult(
        string manifestDirectory,
        SessionId sessionId,
        string correlationId)
    {
        var directory = GetStoreDirectory(manifestDirectory);
        var source = GetStorePath(directory, sessionId, correlationId);
        if (!File.Exists(source))
        {
            return null;
        }

        var claimed = source + "." + Guid.NewGuid().ToString("n") + ".claimed";
        try
        {
            File.Move(source, claimed);
        }
        catch (FileNotFoundException)
        {
            return null;
        }

        try
        {
            var entry = JsonSerializer.Deserialize<PreparedPickerResult>(
                File.ReadAllText(claimed),
                StoreJsonOptions);
            if (entry is null
                || !string.Equals(entry.SessionId, sessionId.Value, StringComparison.Ordinal)
                || !string.Equals(entry.CorrelationId, correlationId, StringComparison.Ordinal))
            {
                throw new JsonException("Stored picker result identity does not match the request.");
            }

            return entry;
        }
        finally
        {
            if (File.Exists(claimed))
            {
                File.Delete(claimed);
            }
        }
    }

    private static string GetStoreDirectory(string manifestDirectory) =>
        Path.Combine(Path.GetFullPath(manifestDirectory), ".picker-results");

    private static string GetStorePath(string directory, SessionId sessionId, string correlationId)
    {
        var identity = Encoding.UTF8.GetBytes(sessionId.Value + "\n" + correlationId);
        return Path.Combine(directory, Convert.ToHexString(SHA256.HashData(identity)).ToLowerInvariant() + ".json");
    }

    private static string? DisplayPath(string? path, bool redactPath)
    {
        if (string.IsNullOrWhiteSpace(path) || !redactPath)
        {
            return path;
        }

        var trimmed = path.TrimEnd('/', '\\');
        var separatorIndex = Math.Max(trimmed.LastIndexOf('/'), trimmed.LastIndexOf('\\'));
        var leaf = separatorIndex < 0 ? trimmed : trimmed[(separatorIndex + 1)..];
        return string.IsNullOrWhiteSpace(leaf) ? "<redacted>" : $"<redacted>{Path.DirectorySeparatorChar}{leaf}";
    }

    private static PickerDialog WaitForDialog(int processId, int timeoutMs)
    {
        var deadline = Environment.TickCount64 + timeoutMs;
        do
        {
            var dialog = FindDialog(processId);
            if (dialog.Handle != IntPtr.Zero || Environment.TickCount64 >= deadline)
            {
                return dialog;
            }

            Thread.Sleep(50);
        }
        while (true);
    }

    private static bool WaitForDialogClose(IntPtr dialog, int timeoutMs)
    {
        var deadline = Environment.TickCount64 + timeoutMs;
        do
        {
            if (!IsWindow(dialog))
            {
                return true;
            }

            if (Environment.TickCount64 >= deadline)
            {
                return false;
            }

            Thread.Sleep(50);
        }
        while (true);
    }

    private static PickerDialog FindDialog(int processId)
    {
        var result = default(PickerDialog);
        EnumWindows((window, _) =>
        {
            if (!IsWindowVisible(window) || !IsOwnedDialog(window, processId))
            {
                return true;
            }

            var className = GetWindowClassName(window);
            if (className is "#32770" or "CabinetWClass")
            {
                result = new PickerDialog(window, className);
                return false;
            }

            return true;
        }, IntPtr.Zero);
        return result;
    }

    private static bool IsOwnedDialog(IntPtr window, int processId)
    {
        GetWindowThreadProcessId(window, out var windowProcessId);
        if (windowProcessId != (uint)processId)
        {
            return false;
        }

        var owner = GetWindow(window, GwOwner);
        while (owner != IntPtr.Zero)
        {
            GetWindowThreadProcessId(owner, out var ownerProcessId);
            if (ownerProcessId != 0 && ownerProcessId != (uint)processId)
            {
                return false;
            }

            owner = GetWindow(owner, GwOwner);
        }

        return true;
    }

    private static IntPtr FindPathEdit(IntPtr dialog)
    {
        var directEdit = GetDlgItem(dialog, FileNameEditId);
        if (directEdit != IntPtr.Zero)
        {
            return directEdit;
        }

        var combo = GetDlgItem(dialog, FileNameComboId);
        if (combo != IntPtr.Zero)
        {
            var comboEdit = FindChild(combo, "Edit");
            if (comboEdit != IntPtr.Zero)
            {
                return comboEdit;
            }
        }

        return FindChild(dialog, "Edit");
    }

    private static IntPtr FindChild(IntPtr parent, string expectedClass)
    {
        var result = IntPtr.Zero;
        EnumChildWindows(parent, (window, _) =>
        {
            if (string.Equals(GetWindowClassName(window), expectedClass, StringComparison.Ordinal))
            {
                result = window;
                return false;
            }
            return true;
        }, IntPtr.Zero);
        return result;
    }

    private static bool TrySendText(IntPtr window, string text, int timeoutMs)
    {
        return SendMessageTimeout(
            window,
            WmSetText,
            IntPtr.Zero,
            text,
            SmtoAbortIfHung,
            (uint)Math.Max(timeoutMs, 1),
            out _) != IntPtr.Zero;
    }

    private static string GetWindowClassName(IntPtr window)
    {
        var value = new StringBuilder(256);
        _ = GetClassName(window, value, value.Capacity);
        return value.ToString();
    }

    private sealed record PreparedPickerResult(
        string SessionId,
        int ProcessId,
        string CorrelationId,
        string Result,
        string? Path,
        DateTimeOffset PreparedAt,
        DateTimeOffset ExpiresAt);

    private readonly record struct PickerDialog(IntPtr Handle, string ClassName);

    private delegate bool EnumWindowsProc(IntPtr window, IntPtr parameter);
    [DllImport("user32.dll")] private static extern bool EnumWindows(EnumWindowsProc callback, IntPtr parameter);
    [DllImport("user32.dll")] private static extern bool EnumChildWindows(IntPtr parent, EnumWindowsProc callback, IntPtr parameter);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr window);
    [DllImport("user32.dll")] private static extern bool IsWindow(IntPtr window);
    [DllImport("user32.dll")] private static extern IntPtr GetWindow(IntPtr window, uint command);
    [DllImport("user32.dll")] private static extern IntPtr GetDlgItem(IntPtr dialog, int controlId);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetClassName(IntPtr window, StringBuilder className, int maximumCount);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr SendMessageTimeout(
        IntPtr window,
        uint message,
        IntPtr wParam,
        string lParam,
        uint flags,
        uint timeout,
        out IntPtr result);
    [DllImport("user32.dll")] private static extern bool PostMessage(IntPtr window, uint message, IntPtr wParam, IntPtr lParam);
}
