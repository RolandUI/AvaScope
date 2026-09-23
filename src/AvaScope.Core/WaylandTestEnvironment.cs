using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using AvaScope.Protocol;

namespace AvaScope.Core;

/// <summary>One private Weston headless compositor; never connects to an existing desktop.</summary>
public sealed class WaylandTestEnvironment : IAsyncDisposable, IRuntimeTestEnvironment
{
    private readonly WaylandEnvironmentOptions _options;
    private readonly string _output;
    private readonly Func<string, string> _sanitize;
    private readonly Dictionary<string, string> _environment = new(StringComparer.Ordinal);
    private readonly List<ProtocolError> _diagnostics = [];
    private readonly CancellationTokenSource _failure = new();
    private readonly string _identity = Guid.NewGuid().ToString("N");
    private Process? _compositor;
    private DateTimeOffset _startedAt;
    private Task? _stdout, _stderr;
    private int _pid;
    private string? _runtime, _version;
    private string _status = "not_started", _startingCommand = "weston";
    private bool _observed, _exited, _disposed;
    private volatile bool _stopping;
    internal Action<string, Process>? ProcessStarted { get; init; }
    internal Action<string>? RuntimeDirectoryCreated { get; init; }
    internal Action<RuntimeEnvironmentEvidence>? EvidenceChanged { get; init; }

    public WaylandTestEnvironment(WaylandEnvironmentOptions options, string outputDirectory, Func<string, string>? sanitize = null)
    { _options = options; _output = Path.GetFullPath(outputDirectory); _sanitize = sanitize ?? (value => value); }
    public IReadOnlyDictionary<string, string> EnvironmentVariables => _environment;
    public CancellationToken FailureToken => _failure.Token;
    public ProtocolError? UnexpectedExit { get; private set; }
    public RuntimeEnvironmentEvidence Evidence => new("wayland", "managed", _status,
        _runtime is null ? null : Path.Combine(_runtime, "display"),
        _observed ? $"{_options.Width}x{_options.Height} logical @{_options.Scale}x" : null, "disabled", _runtime,
        _runtime is not null && !Directory.Exists(_runtime),
        _pid == 0 ? [] : [new("weston", _pid, _startedAt, _exited || _compositor?.HasExited == true, Log("stdout"), Log("stderr"))],
        _diagnostics.ToArray(), "private_unix", new(_options.Lane, _version, "pixman", _options.Width, _options.Height, _options.Scale,
            _observed ? _options.Width * _options.Scale : null, _observed ? _options.Height * _options.Scale : null,
            _options.KeyboardLayout, _options.KeyboardVariant, "configured_only_no_native_keyboard_seat",
            ["experimental_avalonia_wayland_requires_explicit_host_opt_in", "headless_kiosk_shell;app_client_geometry_reported_separately",
             "no_native_input_seat;semantic_and_explicit_synthetic_input_only", "rendered_capture_only;native_screen_capture_unsupported",
             "native_window_management_and_accessibility_audit_unsupported", "xwayland_separate_unsupported_lane"]));

    public static ProtocolError? Validate(WaylandEnvironmentOptions options)
    {
        if (options.Lane == "xwayland") return new("wayland_xwayland_unsupported", "XWayland is a separate, unvalidated lane. Select native_wayland with an explicit Avalonia UseWayland host, or use the managed X11 profile.");
        if (options.Lane != "native_wayland" || options.Width is < 64 or > 8192 || options.Height is < 64 or > 8192
            || options.Scale is < 1 or > 4 || (long)options.Width * options.Height * options.Scale * options.Scale > 16777216
            || options.TimeoutMs is < 100 or > 30000 || !Token(options.KeyboardLayout, false) || !Token(options.KeyboardVariant, true))
            return new("wayland_environment_invalid", "Use native_wayland, logical dimensions 64..8192, scale 1..4, at most 16 Mi output pixels, 100..30000 ms and bounded XKB layout/variant tokens.");
        return null;
        static bool Token(string? value, bool empty) => value is not null && value.Length <= 32 && (empty || value.Length > 0)
            && value.All(c => char.IsAsciiLetterOrDigit(c) || c is '_' or '-');
    }

    public async Task<CoreResult<RuntimeEnvironmentEvidence>> StartAsync(CancellationToken cancellationToken = default)
    {
        if (_status != "not_started" || _disposed) return Fail("wayland_environment_already_started", "An environment instance can start only once.");
        if (Validate(_options) is { } invalid) return Fail(invalid.Code, invalid.Message);
        if (!OperatingSystem.IsLinux()) return Fail("wayland_environment_unsupported", "Controlled Weston environments require Linux. Choose this platform's native profile.");
        if (Environment.GetEnvironmentVariable("WAYLAND_SOCKET") is not null)
            return Fail("wayland_inherited_socket_unsupported", "Unset WAYLAND_SOCKET for this agent process; the managed profile must use its own named socket.");
        Directory.CreateDirectory(_output); _status = "starting";
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _failure.Token);
        deadline.CancelAfter(_options.TimeoutMs);
        try
        {
            _runtime = Path.Combine(Path.GetTempPath(), "avs-wayland-" + _identity);
            Directory.CreateDirectory(_runtime, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            File.WriteAllText(Path.Combine(_runtime, ".avascope-wayland"), _identity);
            RuntimeDirectoryCreated?.Invoke(_runtime);
            _environment["XDG_RUNTIME_DIR"] = _runtime;
            _environment["WAYLAND_DISPLAY"] = "display";
            _environment["DISPLAY"] = "";
            _environment["XAUTHORITY"] = Path.Combine(_runtime, "no-x11-authority");
            _environment["DBUS_SESSION_BUS_ADDRESS"] = "unix:path=" + Path.Combine(_runtime, "no-session-bus");
            _environment["XDG_SESSION_TYPE"] = "wayland";
            _environment["LIBGL_ALWAYS_SOFTWARE"] = "1";
            _environment["GALLIUM_DRIVER"] = "llvmpipe";
            var version = await ProbeAsync("weston", ["--version"], deadline.Token);
            _version = version.Output.Trim();
            if (version.ExitCode != 0 || !Regex.IsMatch(_version, @"^weston 13\.[0-9]+\.[0-9]+$", RegexOptions.CultureInvariant))
                throw new EnvironmentStop("wayland_compositor_version_unsupported", "This profile validates Weston 13.x. Install Weston 13 and wayland-utils; other versions require separate validation.");
            if (!File.Exists("/usr/share/X11/xkb/symbols/" + _options.KeyboardLayout))
                throw new EnvironmentStop("wayland_keyboard_layout_missing", "Install xkb-data providing the requested keyboard layout. No desktop settings were changed.");
            var config = Path.Combine(_runtime, "weston.ini");
            File.WriteAllText(config, "[core]\nxwayland=false\n[keyboard]\nkeymap_layout=" + _options.KeyboardLayout + "\nkeymap_variant=" + _options.KeyboardVariant + "\n");
            _startingCommand = "weston";
            _compositor = new Process { StartInfo = Info("weston", ["--backend=headless", "--renderer=pixman", "--shell=kiosk-shell.so",
                "--socket=display", "--width=" + _options.Width.ToString(CultureInfo.InvariantCulture), "--height=" + _options.Height.ToString(CultureInfo.InvariantCulture),
                "--scale=" + _options.Scale.ToString(CultureInfo.InvariantCulture), "--idle-time=0", "--config=" + config]), EnableRaisingEvents = true };
            deadline.Token.ThrowIfCancellationRequested();
            if (!_compositor.Start()) throw new InvalidOperationException("Compositor startup failed.");
            _pid = _compositor.Id; _startedAt = _compositor.StartTime.ToUniversalTime();
            _stdout = DrainAsync(_compositor.StandardOutput, Log("stdout")); _stderr = DrainAsync(_compositor.StandardError, Log("stderr"));
            ProcessStarted?.Invoke("weston", _compositor);
            _compositor.Exited += (_, _) => Exited();
            if (_compositor.HasExited) Exited();
            WriteEvidence();
            while (true)
            {
                deadline.Token.ThrowIfCancellationRequested();
                var probe = await ProbeAsync("wayland-info", [], deadline.Token);
                if (probe.ExitCode == 0)
                {
                    File.WriteAllText(Path.Combine(_output, "wayland-info.log"), _sanitize(probe.Output));
                    // These values come from wl_output/xdg_output, not the requested arguments.
                    if (!probe.Output.Contains("interface: 'xdg_wm_base'", StringComparison.Ordinal)
                        || !probe.Output.Contains("interface: 'wl_shm'", StringComparison.Ordinal)
                        || !Regex.IsMatch(probe.Output, $@"logical_width: {_options.Width}, logical_height: {_options.Height}\b")
                        || !Regex.IsMatch(probe.Output, $@"\bscale: {_options.Scale},")
                        || !Regex.IsMatch(probe.Output, $@"\bwidth: {_options.Width * _options.Scale} px, height: {_options.Height * _options.Scale} px,")
                        || probe.Output.Contains("interface: 'wl_seat'", StringComparison.Ordinal))
                        throw new EnvironmentStop("wayland_environment_geometry_mismatch", "Weston did not expose the requested fixed headless output/no-seat profile. Review wayland-info and compositor logs.");
                    _observed = true; break;
                }
                await Task.Delay(50, deadline.Token);
            }
            deadline.Token.ThrowIfCancellationRequested(); _status = "ready"; WriteEvidence();
            return CoreResult<RuntimeEnvironmentEvidence>.Ok(Evidence);
        }
        catch (Exception e) when (e is EnvironmentStop or OperationCanceledException or TimeoutException or IOException or InvalidOperationException or UnauthorizedAccessException or System.ComponentModel.Win32Exception)
        {
            var error = e is EnvironmentStop stop ? new ProtocolError(stop.Code, stop.Message) : cancellationToken.IsCancellationRequested
                ? new("wayland_environment_cancelled", "The controlled environment was cancelled; only its owned resources are cleaned.") : UnexpectedExit
                ?? (e is System.ComponentModel.Win32Exception ? new("wayland_dependency_missing", $"Install '{_startingCommand}' (Weston 13 / wayland-utils), xkb-data and Mesa EGL software libraries in this Linux environment.")
                    : new ProtocolError(e is OperationCanceledException ? "wayland_environment_timed_out" : "wayland_environment_start_failed", "Controlled compositor readiness failed. Inspect its bounded logs; the existing desktop was not used."));
            _diagnostics.Add(error); await DisposeAsync(); return Fail(error.Code, error.Message);
        }
    }

    private string Log(string stream) => Path.Combine(_output, "weston." + stream + ".log");
    private void Exited()
    {
        if (_stopping) return;
        UnexpectedExit = new("wayland_helper_exited", "The owned compositor exited before scenario cleanup.");
        _failure.Cancel();
    }
    private ProcessStartInfo Info(string command, IReadOnlyList<string> args)
    {
        var info = new ProcessStartInfo(command) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true,
            RedirectStandardError = true, WorkingDirectory = _runtime ?? _output };
        foreach (var arg in args) info.ArgumentList.Add(arg);
        foreach (var pair in _environment) info.Environment[pair.Key] = pair.Value;
        info.Environment.Remove("WESTON_MODULE_MAP"); info.Environment.Remove("WAYLAND_SOCKET");
        return info;
    }
    private async Task<(int ExitCode, string Output)> ProbeAsync(string command, IReadOnlyList<string> args, CancellationToken token)
    {
        _startingCommand = command;
        using var process = Process.Start(Info(command, args)) ?? throw new InvalidOperationException("Readiness probe did not start.");
        var stdout = ReadBoundedAsync(process.StandardOutput); var stderr = process.StandardError.BaseStream.CopyToAsync(Stream.Null);
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token); deadline.CancelAfter(1000);
        try
        {
            await process.WaitForExitAsync(deadline.Token);
            return (process.ExitCode, await stdout.WaitAsync(deadline.Token));
        }
        catch (OperationCanceledException) when (!token.IsCancellationRequested) { return (-1, ""); }
        finally
        {
            if (!process.HasExited) process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(3));
            await Task.WhenAll(stdout, stderr).WaitAsync(TimeSpan.FromSeconds(3));
        }
    }
    private async Task DrainAsync(StreamReader reader, string path) => await File.WriteAllTextAsync(path, _sanitize(await ReadBoundedAsync(reader)));
    private static async Task<string> ReadBoundedAsync(StreamReader reader)
    {
        var result = new StringBuilder(); var buffer = new char[2048]; int count;
        while ((count = await reader.ReadAsync(buffer)) > 0)
            if (result.Length < 65536) result.Append(buffer, 0, Math.Min(count, 65536 - result.Length));
        return result.ToString();
    }
    private void WriteEvidence()
    {
        var evidence = Evidence; EvidenceChanged?.Invoke(evidence);
        File.WriteAllText(Path.Combine(_output, "environment.json"), _sanitize(JsonSerializer.Serialize(evidence)));
    }
    void IRuntimeTestEnvironment.RetainForRecovery() { _stopping = true; _status = "retained"; WriteEvidence(); }
    public async ValueTask DisposeAsync()
    {
        if (_disposed) return; _stopping = true;
        if (_pid != 0 && _compositor is not null)
        {
            try
            {
                if (!_compositor.HasExited && _compositor.StartTime.ToUniversalTime() == _startedAt.UtcDateTime)
                {
                    Kill(_pid, 15);
                    try { await _compositor.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(2)); }
                    catch (TimeoutException) { _compositor.Kill(entireProcessTree: true); }
                }
                await _compositor.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(3));
                await Task.WhenAll(_stdout!, _stderr!).WaitAsync(TimeSpan.FromSeconds(3));
                _exited = true; _compositor.Dispose(); _compositor = null;
            }
            catch (Exception e) when (e is InvalidOperationException or System.ComponentModel.Win32Exception or IOException or TimeoutException)
            { _diagnostics.Add(new("wayland_helper_cleanup_failed", "The owned compositor could not be safely cleaned; its identity is retained.")); }
        }
        if ((_pid == 0 || _exited) && _runtime is not null && Directory.Exists(_runtime))
        {
            try
            {
                if (Path.GetFileName(_runtime) != "avs-wayland-" + _identity || Path.GetDirectoryName(_runtime) != Path.TrimEndingDirectorySeparator(Path.GetTempPath())
                    || new DirectoryInfo(_runtime).LinkTarget is not null || File.ReadAllText(Path.Combine(_runtime, ".avascope-wayland")) != _identity)
                    throw new IOException("Owned runtime identity changed.");
                var entries = Directory.EnumerateFileSystemEntries(_runtime).Take(1025).ToArray();
                if (entries.Length > 1024 || entries.Any(p => (File.GetAttributes(p) & (FileAttributes.ReparsePoint | FileAttributes.Directory)) != 0))
                    throw new IOException("Unexpected runtime resource; retained for inspection.");
                Directory.Delete(_runtime, recursive: true);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            { _diagnostics.Add(new("wayland_resources_cleanup_failed", "The exact private runtime directory could not be safely removed.")); }
        }
        if (UnexpectedExit is not null && !_diagnostics.Contains(UnexpectedExit)) _diagnostics.Add(UnexpectedExit);
        _status = _diagnostics.Any(d => d.Code.Contains("cleanup", StringComparison.Ordinal)) ? "cleanup_failed" : "closed";
        if (Directory.Exists(_output)) WriteEvidence();
        _disposed = true; _failure.Dispose();
    }
    private static CoreResult<RuntimeEnvironmentEvidence> Fail(string code, string message) => CoreResult<RuntimeEnvironmentEvidence>.Fail(new(code, message));
    private sealed class EnvironmentStop(string code, string message) : Exception(message) { public string Code { get; } = code; }
    [DllImport("libc", EntryPoint = "kill", SetLastError = true)] private static extern int Kill(int pid, int signal);
}
