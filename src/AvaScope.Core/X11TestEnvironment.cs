using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using AvaScope.Protocol;

namespace AvaScope.Core;

/// <summary>Owns only helpers explicitly created for one Linux X11 scenario.</summary>
public sealed class X11TestEnvironment : IAsyncDisposable, IRuntimeTestEnvironment
{
    private readonly X11EnvironmentOptions _options;
    private readonly string _outputDirectory;
    private readonly Func<string, string> _sanitize;
    private readonly List<Helper> _helpers = [];
    private readonly Dictionary<string, string> _environment = new(StringComparer.Ordinal);
    private readonly ConcurrentQueue<ProtocolError> _diagnostics = new();
    private readonly CancellationTokenSource _failure = new();
    private string? _runtimeDirectory;
    private string? _display;
    private string _status = "not_started";
    private string _startingCommand = "Xvfb";
    private volatile bool _stopping;
    private ProtocolError? _unexpectedExit;
    private bool _disposed;
    internal Action<string, Process>? ProcessStarted { get; init; }
    internal Action<string>? RuntimeDirectoryCreated { get; init; }
    internal Action<RuntimeEnvironmentEvidence>? EvidenceChanged { get; init; }

    public X11TestEnvironment(X11EnvironmentOptions options, string outputDirectory, Func<string, string>? sanitize = null)
    {
        _options = options;
        _outputDirectory = Path.GetFullPath(outputDirectory);
        _sanitize = sanitize ?? (value => value);
    }

    public IReadOnlyDictionary<string, string> EnvironmentVariables => _environment;
    public CancellationToken FailureToken => _failure.Token;
    public ProtocolError? UnexpectedExit => _unexpectedExit;

    public RuntimeEnvironmentEvidence Evidence => new("x11", _options.Mode, _status, _display,
        _options.Mode == "managed" ? $"{_options.Width}x{_options.Height}x24@{_options.Dpi}dpi" : null,
        _options.Mode == "managed" ? "disabled" : "not_managed", _runtimeDirectory,
        _runtimeDirectory is not null && !Directory.Exists(_runtimeDirectory),
        _helpers.Select(helper => new RuntimeHelperEvidence(helper.Kind, helper.Id, helper.StartedAt,
            helper.Disposed || helper.Process.HasExited, helper.StdoutPath, helper.StderrPath)).ToArray(), _diagnostics.ToArray(),
        _options.Mode == "managed" ? "abstract_unix" : "not_managed");

    public static ProtocolError? Validate(X11EnvironmentOptions options)
    {
        if (options.Mode is not ("managed" or "existing") || options.Width is < 64 or > 8192 || options.Height is < 64 or > 8192 ||
            options.Dpi is < 48 or > 384 || options.TimeoutMs is < 100 or > 30000)
            return new("x11_environment_invalid", "Use managed/existing mode, dimensions 64–8192, DPI 48–384 and timeoutMs 100–30000.");
        if (options.Mode == "managed" && (options.Display is not null || options.Xauthority is not null))
            return new("x11_environment_invalid", "Managed mode allocates its own display and authorization; do not supply display/xauthority.");
        if (options.Mode == "existing" && (options.Display is null || !Regex.IsMatch(options.Display, @"^:[0-9]{1,5}(\.[0-9]{1,2})?$", RegexOptions.CultureInvariant) ||
            options.WindowManager || options.SessionBus || options.Xauthority is not null && !Path.IsPathFullyQualified(options.Xauthority)))
            return new("x11_environment_invalid", "Existing mode requires an explicit local :number display, optional absolute xauthority, and no owned window manager/session bus.");
        return null;
    }

    public async Task<CoreResult<RuntimeEnvironmentEvidence>> StartAsync(CancellationToken cancellationToken = default)
    {
        if (_status != "not_started") return CoreResult<RuntimeEnvironmentEvidence>.Fail(new("x11_environment_already_started", "An environment instance can start only once."));
        if (Validate(_options) is { } invalid) return CoreResult<RuntimeEnvironmentEvidence>.Fail(new(invalid.Code, invalid.Message));
        if (!OperatingSystem.IsLinux()) return CoreResult<RuntimeEnvironmentEvidence>.Fail(new("x11_environment_unsupported", "Managed/existing X11 environments require Linux; select the current platform's test profile."));
        Directory.CreateDirectory(_outputDirectory);
        _status = "starting";
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _failure.Token);
        timeout.CancelAfter(_options.TimeoutMs);
        try
        {
            if (_options.Mode == "managed")
            {
                // Short private socket paths also work when the evidence directory is deeply nested.
                _runtimeDirectory = Path.Combine(Path.GetTempPath(), "avs-x11-" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(_runtimeDirectory, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
                RuntimeDirectoryCreated?.Invoke(_runtimeDirectory);
                _environment["XDG_RUNTIME_DIR"] = _runtimeDirectory;
                _environment["XAUTHORITY"] = Path.Combine(_runtimeDirectory, "authority");
                _environment["WAYLAND_DISPLAY"] = string.Empty;
                // A managed run must not auto-connect to the user's unrelated session bus.
                _environment["DBUS_SESSION_BUS_ADDRESS"] = "unix:path=" + Path.Combine(_runtimeDirectory, "no-session-bus");
                WriteAuthority(_environment["XAUTHORITY"]);
                var server = StartHelper("xvfb", "Xvfb", ["-displayfd", "1", "-screen", "0", $"{_options.Width}x{_options.Height}x24",
                    "-dpi", _options.Dpi.ToString(CultureInfo.InvariantCulture), "-nolisten", "tcp", "-nolisten", "unix", "-listen", "local",
                    "-auth", _environment["XAUTHORITY"], "-noreset"], firstLine: true);
                var line = await server.Process.StandardOutput.ReadLineAsync(timeout.Token);
                server.Stdout = DrainAsync(server.Process.StandardOutput, server.StdoutPath);
                if (!int.TryParse(line, NumberStyles.None, CultureInfo.InvariantCulture, out var number) || number is < 0 or > 65535)
                    throw new InvalidOperationException("Xvfb did not return an allocated display number.");
                _display = ":" + number.ToString(CultureInfo.InvariantCulture);
                _environment["DISPLAY"] = _display;
            }
            else
            {
                _display = _options.Display;
                _environment["DISPLAY"] = _display!;
                _environment["WAYLAND_DISPLAY"] = string.Empty;
                if (_options.Xauthority is not null) _environment["XAUTHORITY"] = _options.Xauthority;
            }
            await WaitForAsync(async () => (await ProbeAsync("xdpyinfo", [], timeout.Token)).ExitCode == 0, timeout.Token);
            if (_options.SessionBus)
            {
                var busPath = Path.Combine(_runtimeDirectory!, "bus");
                var configPath = Path.Combine(_runtimeDirectory!, "bus.conf");
                // No service directories or system configuration imports: no unowned activated daemons.
                File.WriteAllText(configPath, "<busconfig><type>session</type><listen>unix:path=" + busPath +
                    "</listen><auth>EXTERNAL</auth><policy context=\"default\"><allow own=\"*\"/><allow send_destination=\"*\"/><allow receive_sender=\"*\"/></policy></busconfig>");
                var bus = StartHelper("dbus", "dbus-daemon", ["--nofork", "--nopidfile", "--nosyslog", "--config-file=" + configPath, "--print-address=1"], firstLine: true);
                var address = await bus.Process.StandardOutput.ReadLineAsync(timeout.Token);
                bus.Stdout = DrainAsync(bus.Process.StandardOutput, bus.StdoutPath);
                if (address is null || !address.StartsWith("unix:path=" + busPath, StringComparison.Ordinal))
                    throw new InvalidOperationException("The owned D-Bus did not return its local socket address.");
                _environment["DBUS_SESSION_BUS_ADDRESS"] = address;
                await WaitForAsync(async () => (await ProbeAsync("dbus-send", ["--session", "--print-reply", "--dest=org.freedesktop.DBus", "/org/freedesktop/DBus", "org.freedesktop.DBus.ListNames"], timeout.Token)).ExitCode == 0, timeout.Token);
            }
            if (_options.WindowManager)
            {
                var configPath = Path.Combine(_runtimeDirectory!, "openbox.xml");
                File.WriteAllText(configPath, "<openbox_config xmlns=\"http://openbox.org/3.4/rc\"><focus><focusNew>yes</focusNew></focus></openbox_config>");
                StartHelper("window_manager", "openbox", ["--sm-disable", "--config-file", configPath]);
                await WaitForAsync(async () =>
                {
                    var result = await ProbeAsync("xprop", ["-root", "_NET_SUPPORTING_WM_CHECK"], timeout.Token);
                    return result.ExitCode == 0 && result.Output.Contains("window id # 0x", StringComparison.Ordinal) && !result.Output.TrimEnd().EndsWith("0x0", StringComparison.Ordinal);
                }, timeout.Token);
            }
            timeout.Token.ThrowIfCancellationRequested();
            _status = "ready";
            WriteEvidence();
            return CoreResult<RuntimeEnvironmentEvidence>.Ok(Evidence);
        }
        catch (Exception exception) when (exception is OperationCanceledException or IOException or InvalidOperationException or UnauthorizedAccessException or System.ComponentModel.Win32Exception)
        {
            var error = _unexpectedExit ?? new ProtocolError(
                exception is System.ComponentModel.Win32Exception ? "x11_dependency_missing" :
                cancellationToken.IsCancellationRequested ? "x11_environment_cancelled" :
                exception is OperationCanceledException ? "x11_environment_timed_out" : "x11_environment_start_failed",
                exception is System.ComponentModel.Win32Exception ? $"Install the required '{_startingCommand}' executable in the selected Linux environment." :
                "X11 environment readiness failed or was interrupted. Check the bounded helper logs and display authorization.");
            _diagnostics.Enqueue(error);
            await DisposeAsync();
            return CoreResult<RuntimeEnvironmentEvidence>.Fail(new(error.Code, error.Message));
        }
    }

    private Helper StartHelper(string kind, string command, IReadOnlyList<string> arguments, bool firstLine = false)
    {
        _startingCommand = command;
        var process = new Process { StartInfo = CreateStartInfo(command, arguments), EnableRaisingEvents = true };
        if (!process.Start()) throw new InvalidOperationException("The owned helper did not start.");
        try { ProcessStarted?.Invoke(kind, process); }
        catch
        {
            if (!process.HasExited) process.Kill(entireProcessTree: true);
            process.Dispose();
            throw;
        }
        var helper = new Helper(kind, process, Path.Combine(_outputDirectory, kind + ".stdout.log"), Path.Combine(_outputDirectory, kind + ".stderr.log"));
        _helpers.Add(helper);
        helper.Stderr = DrainAsync(process.StandardError, helper.StderrPath);
        if (!firstLine) helper.Stdout = DrainAsync(process.StandardOutput, helper.StdoutPath);
        process.Exited += (_, _) =>
        {
            if (_stopping) return;
            _unexpectedExit = new("x11_helper_exited", $"The owned {kind} helper exited before scenario cleanup.");
            _failure.Cancel();
        };
        if (process.HasExited)
        {
            _unexpectedExit = new("x11_helper_exited", $"The owned {kind} helper exited during startup.");
            _failure.Cancel();
        }
        WriteEvidence();
        return helper;
    }

    private ProcessStartInfo CreateStartInfo(string command, IReadOnlyList<string> arguments)
    {
        var info = new ProcessStartInfo(command) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true,
            WorkingDirectory = _runtimeDirectory ?? _outputDirectory };
        foreach (var argument in arguments) info.ArgumentList.Add(argument);
        foreach (var pair in _environment) info.Environment[pair.Key] = pair.Value;
        return info;
    }

    private async Task<(int ExitCode, string Output)> ProbeAsync(string command, IReadOnlyList<string> arguments, CancellationToken cancellationToken)
    {
        _startingCommand = command;
        using var probe = new Process { StartInfo = CreateStartInfo(command, arguments) };
        if (!probe.Start()) throw new InvalidOperationException("The readiness probe did not start.");
        var stdout = ReadBoundedAsync(probe.StandardOutput, 4096);
        var stderr = probe.StandardError.BaseStream.CopyToAsync(Stream.Null);
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(1000);
        try
        {
            await probe.WaitForExitAsync(deadline.Token);
            var output = await stdout.WaitAsync(deadline.Token);
            await stderr.WaitAsync(deadline.Token);
            return (probe.ExitCode, output);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) { return (-1, string.Empty); }
        finally
        {
            if (!probe.HasExited) probe.Kill(entireProcessTree: true);
            await probe.WaitForExitAsync(CancellationToken.None);
            await Task.WhenAll(stdout, stderr);
        }
    }

    private static async Task WaitForAsync(Func<Task<bool>> ready, CancellationToken cancellationToken)
    {
        while (!await ready()) await Task.Delay(50, cancellationToken);
    }

    private async Task DrainAsync(StreamReader reader, string path)
    {
        var text = await ReadBoundedAsync(reader, 65536);
        File.WriteAllText(path, _sanitize(text));
    }

    private static async Task<string> ReadBoundedAsync(StreamReader reader, int limit)
    {
        var output = new StringBuilder();
        var buffer = new char[2048];
        int count;
        while ((count = await reader.ReadAsync(buffer)) != 0)
            if (output.Length < limit) output.Append(buffer, 0, Math.Min(count, limit - output.Length));
        return output.ToString();
    }

    [System.Runtime.Versioning.SupportedOSPlatform("linux")]
    private static void WriteAuthority(string path)
    {
        using var stream = new FileStream(path, new FileStreamOptions { Mode = FileMode.CreateNew, Access = FileAccess.Write,
            UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite });
        // Xauthority uses network-order uint16 fields. FamilyWild + empty display
        // selects this one private cookie before Xvfb chooses its free display.
        stream.Write([255, 255, 0, 0, 0, 0]);
        var name = Encoding.ASCII.GetBytes("MIT-MAGIC-COOKIE-1");
        stream.Write([0, (byte)name.Length]);
        stream.Write(name);
        stream.Write([0, 16]);
        stream.Write(RandomNumberGenerator.GetBytes(16));
    }

    private void WriteEvidence()
    {
        var evidence = Evidence;
        EvidenceChanged?.Invoke(evidence);
        File.WriteAllText(Path.Combine(_outputDirectory, "environment.json"),
            _sanitize(JsonSerializer.Serialize(evidence, new JsonSerializerOptions(JsonSerializerDefaults.Web))));
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _stopping = true;
        foreach (var helper in _helpers.AsEnumerable().Reverse())
        {
            try
            {
                if (!helper.Process.HasExited && helper.Process.StartTime.ToUniversalTime() == helper.StartedAt.UtcDateTime)
                {
                    // Graceful shutdown lets Xvfb release its own display lock.
                    _ = Kill(helper.Id, 15);
                    using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(2));
                    try { await helper.Process.WaitForExitAsync(deadline.Token); }
                    catch (OperationCanceledException) { helper.Process.Kill(entireProcessTree: true); }
                }
                await helper.Process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(3));
                if (helper.Stdout is null) helper.Stdout = DrainAsync(helper.Process.StandardOutput, helper.StdoutPath);
                await Task.WhenAll(helper.Stdout, helper.Stderr!).WaitAsync(TimeSpan.FromSeconds(3));
                helper.Process.Dispose();
                helper.Disposed = true;
            }
            catch (Exception exception) when (exception is InvalidOperationException or System.ComponentModel.Win32Exception or IOException or TimeoutException)
            {
                _diagnostics.Enqueue(new("x11_helper_cleanup_failed", $"Cleanup of owned {helper.Kind} helper failed; its identity and logs are retained."));
            }
        }
        if (_helpers.All(helper => helper.Disposed) && _runtimeDirectory is not null)
        {
            try
            {
                RemoveOwnedXvfbLock();
                Directory.Delete(_runtimeDirectory, recursive: true);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                _diagnostics.Enqueue(new("x11_resources_cleanup_failed", "Owned X11 runtime resources could not be removed."));
            }
        }
        _status = _diagnostics.Any(error => error.Code.Contains("cleanup", StringComparison.Ordinal)) ? "cleanup_failed" : "closed";
        if (_unexpectedExit is not null && !_diagnostics.Contains(_unexpectedExit)) _diagnostics.Enqueue(_unexpectedExit);
        if (Directory.Exists(_outputDirectory)) WriteEvidence();
        _disposed = true;
        _failure.Dispose();
    }

    internal void RetainForRecovery()
    {
        _stopping = true;
        _status = "retained";
        WriteEvidence();
    }

    void IRuntimeTestEnvironment.RetainForRecovery() => RetainForRecovery();

    private void RemoveOwnedXvfbLock()
    {
        if (_options.Mode != "managed" || _display is null || _helpers.FirstOrDefault(helper => helper.Kind == "xvfb") is not { Disposed: true } server) return;
        var number = _display[1..];
        var lockPath = "/tmp/.X" + number + "-lock";
        // A crashed X server can leave files. Delete only when its lock still names
        // the exact exited helper we started; never touch an external server's lock.
        if (File.Exists(lockPath) && int.TryParse(File.ReadAllText(lockPath).Trim(), out var owner) && owner == server.Id)
        {
            // Our server uses an abstract Unix socket; never remove filesystem
            // sockets belonging to another desktop (including WSLg).
            File.Delete(lockPath);
        }
    }

    private sealed class Helper(string kind, Process process, string stdoutPath, string stderrPath)
    {
        public string Kind { get; } = kind;
        public Process Process { get; } = process;
        public int Id { get; } = process.Id;
        public DateTimeOffset StartedAt { get; } = process.StartTime.ToUniversalTime();
        public string StdoutPath { get; } = stdoutPath;
        public string StderrPath { get; } = stderrPath;
        public Task? Stdout { get; set; }
        public Task? Stderr { get; set; }
        public bool Disposed { get; set; }
    }

    [DllImport("libc", EntryPoint = "kill", SetLastError = true)]
    private static extern int Kill(int pid, int signal);
}
