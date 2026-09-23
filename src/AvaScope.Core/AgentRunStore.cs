using System.Diagnostics;
using System.Globalization;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text.Json;
using AvaScope.Protocol;

namespace AvaScope.Core;

/// <summary>Private local recovery records; never replays interrupted actions or deletes retained evidence/data.</summary>
public sealed class AgentRunStore
{
    public const string DirectoryEnvironmentVariable = "AVASCOPE_RUN_STORE_DIR";
    private readonly string _root;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public AgentRunStore(string? directory = null) => _root = Path.GetFullPath(directory ?? Environment.GetEnvironmentVariable(DirectoryEnvironmentVariable)
        ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AvaScope", "agent-runs"));

    internal CoreResult<Registration> Begin(string output, string? state, string? launchOutput, string manifestDirectory,
        IReadOnlyDictionary<string, string>? environment, IReadOnlyList<string?>? additionalPaths = null)
    {
        try
        {
            PrivateDirectory(_root);
            using var registry = Lock(Path.Combine(_root, "registry.lock"), wait: true);
            var paths = new[] { output, state, launchOutput }.Concat(additionalPaths ?? []).Where(p => p is not null).Select(p => CanonicalPath(p!)).Distinct(PathComparer).ToArray();
            if (paths.Length > 256) return Fail<Registration>("run_path_limit", "A run may reserve at most 256 distinct artifact/test-data paths.");
            var directories = Directory.EnumerateDirectories(_root).Take(2049).ToArray();
            if (directories.Length > 2048) return Fail<Registration>("run_store_limit", "Recovery store exceeds 2048 runs. Archive completed record directories explicitly before starting another run.");
            foreach (var directory in directories)
            {
                if (!Guid.TryParseExact(Path.GetFileName(directory), "N", out _)) continue;
                var previous = Read(Path.GetFileName(directory));
                if (previous is null) return Fail<Registration>("run_record_invalid", $"Recovery record {Path.GetFileName(directory)} is invalid; path ownership cannot be established. Inspect/archive that record explicitly.");
                if (!previous.Paths.Any(left => paths.Any(right => Overlaps(left, right)))) continue;
                var active = IsLocked(directory);
                if (active || previous.State is "running" or "resumed" or "partial_cleanup" || previous.Processes.Any(IsAlive))
                    return Fail<Registration>("run_path_conflict", $"Run {previous.RunId} still owns an overlapping artifact/test-data path. Inspect or recover that run before reusing the path.");
            }
            var id = Guid.NewGuid().ToString("N");
            var runDirectory = Path.Combine(_root, id);
            PrivateDirectory(runDirectory);
            var activeLock = Lock(Path.Combine(runDirectory, "active.lock"));
            try
            {
                var record = new Record(id, "running", DateTimeOffset.UtcNow, Path.GetFullPath(manifestDirectory), paths,
                    environment?.GetValueOrDefault("UI_INSPECTION_PROVIDER_VERSION"), environment?.GetValueOrDefault("UI_INSPECTION_PROVIDER_SHA256"));
                Save(record);
                return CoreResult<Registration>.Ok(new(this, record, activeLock));
            }
            catch { activeLock.Dispose(); throw; }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
        { return Fail<Registration>("run_store_unavailable", "Cannot reserve private run paths/records: " + exception.Message); }
    }

    public CoreResult<IReadOnlyList<AgentRunRecoveryResponse>> List(int maxResults = 25)
    {
        if (maxResults is < 1 or > 100) return Fail<IReadOnlyList<AgentRunRecoveryResponse>>("run_list_invalid", "maxResults must be 1..100.");
        if (!Directory.Exists(_root)) return CoreResult<IReadOnlyList<AgentRunRecoveryResponse>>.Ok([]);
        try
        {
            RejectLink(_root);
            var directories = Directory.EnumerateDirectories(_root).Take(2049).ToArray();
            if (directories.Length > 2048) return Fail<IReadOnlyList<AgentRunRecoveryResponse>>("run_store_limit", "Recovery store exceeds 2048 records; archive completed records explicitly.");
            var records = directories.Where(d => Guid.TryParseExact(Path.GetFileName(d), "N", out _))
                .Select(d => Read(Path.GetFileName(d))).Where(r => r is not null).Cast<Record>()
                .OrderByDescending(r => r.StartedAt).Take(maxResults)
                .Select(r => Report(r, IsLocked(Path.Combine(_root, r.RunId)), [])).ToArray();
            return CoreResult<IReadOnlyList<AgentRunRecoveryResponse>>.Ok(records);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        { return Fail<IReadOnlyList<AgentRunRecoveryResponse>>("run_store_unavailable", exception.Message); }
    }

    public async Task<CoreResult<AgentRunRecoveryResponse>> RecoverAsync(AgentRunRecoveryRequest request, CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParseExact(request.RunId, "N", out _) || request.Operation is not ("inspect" or "resume" or "cleanup") || request.TtlMs is < 1000 or > 300000)
            return Fail<AgentRunRecoveryResponse>("run_recovery_invalid", "Use an exact runId, inspect/resume/cleanup and ttlMs 1000..300000.");
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var record = Read(request.RunId);
            if (record is null) return Fail<AgentRunRecoveryResponse>("run_record_invalid", "The private run record is missing, invalid, oversized or replaced by a link.");
            if (record.Machine != Environment.MachineName)
                return Fail<AgentRunRecoveryResponse>("run_host_mismatch", "Recovery records belong to their original machine; imported records cannot authorize local control or cleanup.");
            var directory = Path.Combine(_root, request.RunId);
            var active = IsLocked(directory);
            if (request.Operation == "inspect") return CoreResult<AgentRunRecoveryResponse>.Ok(Report(record, active, []));
            if (active) return Fail<AgentRunRecoveryResponse>("run_active", "The run is still executing. Cancel its owner before attempting recovery.");
            using var ownership = Lock(Path.Combine(directory, "active.lock"));
            record = Read(request.RunId) ?? throw new IOException("The run record changed during recovery.");
            if (record.State == "cleaned") return request.Operation == "cleanup"
                ? CoreResult<AgentRunRecoveryResponse>.Ok(Report(record, false, []))
                : Fail<AgentRunRecoveryResponse>("run_already_cleaned", "This run was cleaned up; start a new run explicitly.");
            if (record.Processes.Any(p => IsAlive(p) && !SameProcess(p)))
                return Fail<AgentRunRecoveryResponse>("run_process_identity_changed", "An owned PID no longer identifies its original process. No control or cleanup was dispatched.");
            var diagnostics = new List<ProtocolError>();
            var client = new LocalBridgeClient(record.ManifestDirectory, controlToken: record.ControlToken);
            if (record.SessionId is { } session)
            {
                var current = client.ListSessionManifests().SingleOrDefault(m => m.SessionId == session);
                if (current is not null)
                {
                    if (record.SessionProcess is null || current.ProcessId != record.SessionProcess.ProcessId || !SameProcess(record.SessionProcess))
                        return Fail<AgentRunRecoveryResponse>("run_process_identity_changed", "The recorded session process is no longer the exact original process; recovery cannot control it.");
                    var lease = await client.SessionControlAsync(session, new("renew", Token: record.ControlToken, TtlMs: request.TtlMs), cancellationToken);
                    if (!lease.Success)
                    {
                        // A new acquire succeeds only if no other owner holds a lease and no operation is executing.
                        lease = await client.SessionControlAsync(session, new("acquire", "run:" + record.RunId, TtlMs: request.TtlMs), cancellationToken);
                    }
                    if (!lease.Success) return CoreResult<AgentRunRecoveryResponse>.Fail(lease.Error!);
                    record = record with { ControlToken = lease.Value!.Token };
                    Save(record);
                }
                else if (request.Operation == "resume") return Fail<AgentRunRecoveryResponse>("run_session_unavailable", "The original session is no longer live; inspect retained evidence or clean up. Restart explicitly as a new run.");
            }
            if (request.Operation == "resume")
            {
                if (record.SessionId is null) return Fail<AgentRunRecoveryResponse>("run_session_unavailable", "The run did not reach session readiness; launch a new run explicitly.");
                record = record with { State = "resumed" };
                Save(record);
                return CoreResult<AgentRunRecoveryResponse>.Ok(Report(record, false, [], record.ControlToken));
            }

            // Close only a session belonging to an app launched by this run; attached apps are retained.
            if (record.SessionId is { } ownedSession && record.Processes.Any(p => p.Role == "app" && IsAlive(p)))
            {
                var closed = await client.CloseSessionAsync(ownedSession, cancellationToken);
                if (!closed.Success) diagnostics.Add(new(closed.Error!.Code, "Owned app session could not be closed; its process and helpers were retained."));
            }
            if (diagnostics.Count == 0)
            {
                foreach (var process in record.Processes.OrderBy(p => p.Role == "app" ? 0 : 1).ThenByDescending(p => p.StartedAt))
                {
                    var error = await StopProcess(process, cancellationToken);
                    if (error is not null) { diagnostics.Add(error); break; }
                }
            }
            if (record.SessionId is { } retainedSession && !record.Processes.Any(p => p.Role == "app") && record.ControlToken is not null)
            {
                var released = await client.SessionControlAsync(retainedSession, new("release", Token: record.ControlToken), CancellationToken.None);
                if (!released.Success) diagnostics.Add(new(released.Error!.Code, "Attached application retained; control lease release was not confirmed."));
            }
            if (diagnostics.Count == 0)
            {
                if (record.PipePath is not null && record.Processes.Any(p => p.Role == "app"))
                {
                    var socketError = await RemoveOwnedSocket(record, cancellationToken);
                    if (socketError is not null) diagnostics.Add(socketError);
                }
                if (OperatingSystem.IsLinux() && record.X11Display is { } display && display.StartsWith(':')
                    && int.TryParse(display.AsSpan(1), out var number) && number is >= 0 and <= 65535
                    && record.Processes.FirstOrDefault(p => p.Role == "xvfb") is { } server && !IsAlive(server))
                {
                    var lockPath = "/tmp/.X" + number + "-lock";
                    RejectLink(lockPath);
                    if (File.Exists(lockPath) && int.TryParse(File.ReadAllText(lockPath).Trim(), out var pid) && pid == server.ProcessId)
                        File.Delete(lockPath);
                }
                foreach (var runtime in record.RuntimeDirectories)
                {
                    try
                    {
                        if (!Directory.Exists(runtime)) continue;
                        ValidateRuntimeDirectory(runtime, record.RunId);
                        Directory.Delete(runtime, recursive: true);
                    }
                    catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
                    { diagnostics.Add(new("run_resource_cleanup_failed", "Private runtime directory retained: " + exception.Message)); }
                }
                if (record.Processes.Any(p => p.Role == "app") && record.SessionId is { } closedSession && record.SessionProcess is { } sessionProcess && !IsAlive(sessionProcess)
                    && record.ManifestPath is { } manifestPath && File.Exists(manifestPath))
                {
                    if (!PathComparer.Equals(Path.GetFullPath(manifestPath), Path.Combine(record.ManifestDirectory, closedSession.Value + ".json")))
                        throw new IOException("The recorded manifest is outside its exact session path.");
                    RejectLink(manifestPath);
                    var manifest = JsonSerializer.Deserialize<BridgeSessionManifest>(File.ReadAllText(manifestPath));
                    if (manifest?.SessionId == closedSession && manifest.ProcessId == sessionProcess.ProcessId)
                        File.Delete(manifestPath);
                    else diagnostics.Add(new("run_manifest_changed", "The manifest no longer matches the recorded session; it was retained."));
                }
            }
            record = record with { State = diagnostics.Count == 0 ? "cleaned" : "partial_cleanup", ControlToken = diagnostics.Count == 0 ? null : record.ControlToken };
            Save(record);
            return CoreResult<AgentRunRecoveryResponse>.Ok(Report(record, false, diagnostics));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException or InvalidOperationException)
        { return Fail<AgentRunRecoveryResponse>("run_recovery_failed", "Recovery stopped without assuming resource ownership: " + exception.Message); }
    }

    private static AgentRunRecoveryResponse Report(Record record, bool active, IReadOnlyList<ProtocolError> diagnostics, string? token = null) => new(
        record.RunId, record.State == "running" && !active ? "abandoned" : record.State, active, record.SessionId,
        record.Processes, record.Paths, record.ProviderVersion, record.ProviderManifestSha256, diagnostics, token, record.Outcome, record.FailureStage);

    private Record? Read(string id)
    {
        var directory = Path.Combine(_root, id);
        var path = Path.Combine(directory, "record.json");
        if (!File.Exists(path)) return null;
        RejectLink(_root); RejectLink(directory); RejectLink(path);
        try
        {
            // Writers replace an immutable snapshot atomically. A reader must not block
            // that rename on Windows while a scenario records its process or lease.
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete);
            if (stream.Length > 65536) return null;
            var record = JsonSerializer.Deserialize<Record>(stream, JsonOptions);
            return record?.RunId == id && record.Paths is { Count: > 0 and <= 256 } && record.Paths.All(Path.IsPathFullyQualified)
                && record.Processes is { Count: <= 16 } && record.Processes.All(p => p is not null && p.ProcessId > 0 && p.StartedAt > DateTimeOffset.UnixEpoch)
                && record.RuntimeDirectories is { Count: <= 4 } && record.RuntimeDirectories.All(Path.IsPathFullyQualified)
                && !string.IsNullOrWhiteSpace(record.ManifestDirectory) && Path.IsPathFullyQualified(record.ManifestDirectory) ? record : null;
        }
        catch (JsonException) { return null; }
    }

    private void Save(Record record)
    {
        var path = Path.Combine(_root, record.RunId, "record.json");
        RejectLink(_root); RejectLink(Path.GetDirectoryName(path)!); RejectLink(path);
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        var bytes = JsonSerializer.SerializeToUtf8Bytes(record, JsonOptions);
        if (bytes.Length > 65536) throw new IOException("Recovery record exceeds 64 KiB; no additional resource may be started.");
        var options = new FileStreamOptions { Mode = FileMode.CreateNew, Access = FileAccess.Write };
        if (!OperatingSystem.IsWindows()) options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
        using (var stream = new FileStream(temporary, options))
        { stream.Write(bytes); stream.Flush(flushToDisk: true); }
        File.Move(temporary, path, overwrite: true);
    }

    private static bool IsLocked(string directory)
    {
        if (!File.Exists(Path.Combine(directory, "active.lock"))) return false;
        try { using var file = Lock(Path.Combine(directory, "active.lock")); return false; }
        catch (IOException) { return true; }
    }
    private static FileStream Lock(string path, bool wait = false)
    {
        var started = Stopwatch.GetTimestamp();
        while (true)
        {
            RejectLink(path);
            try { return new(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None); }
            catch (IOException) when (wait && Stopwatch.GetElapsedTime(started) < TimeSpan.FromSeconds(5))
            { Thread.Sleep(20); }
        }
    }
    private static void PrivateDirectory(string path)
    {
        RejectLink(path);
        if (OperatingSystem.IsWindows()) Directory.CreateDirectory(path);
        else Directory.CreateDirectory(path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
    }
    private static void RejectLink(string path)
    {
        if (File.Exists(path) || Directory.Exists(path))
            if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0) throw new IOException("Linked recovery paths are not supported.");
    }
    private static StringComparer PathComparer => OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
    private static string CanonicalPath(string path)
    {
        var absolute = Path.GetFullPath(path);
        var root = Path.GetPathRoot(absolute)!;
        var current = root;
        foreach (var segment in absolute[root.Length..].Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries))
        {
            current = Path.Combine(current, segment);
            var info = new FileInfo(current);
            if (info.LinkTarget is not null) current = info.ResolveLinkTarget(returnFinalTarget: true)?.FullName
                ?? throw new IOException("A run path contains an unresolved link.");
        }
        return Path.TrimEndingDirectorySeparator(current);
    }
    private static bool Overlaps(string left, string right) => PathComparer.Equals(left, right)
        || left.StartsWith(right + Path.DirectorySeparatorChar, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal)
        || right.StartsWith(left + Path.DirectorySeparatorChar, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
    private static bool IsAlive(AgentRunProcess identity)
    {
        try { using var process = Process.GetProcessById(identity.ProcessId); return !process.HasExited; }
        catch (ArgumentException) { return false; }
        catch (InvalidOperationException) { return false; }
    }
    private static bool SameProcess(AgentRunProcess identity)
    {
        try
        {
            using var process = Process.GetProcessById(identity.ProcessId);
            return !process.HasExited && (OperatingSystem.IsLinux()
                ? identity.StartIdentity is not null && identity.StartIdentity == LinuxStartIdentity(process.Id)
                : process.StartTime.ToUniversalTime() == identity.StartedAt.UtcDateTime);
        }
        catch (ArgumentException) { return false; }
        catch (InvalidOperationException) { return false; }
        catch (System.ComponentModel.Win32Exception) { return false; }
        catch (IOException) { return false; }
        catch (UnauthorizedAccessException) { return false; }
    }
    private static AgentRunProcess ProcessIdentity(string role, Process process) => new(role, process.Id,
        process.StartTime.ToUniversalTime(), OperatingSystem.IsLinux() ? LinuxStartIdentity(process.Id) : null);
    private static string LinuxStartIdentity(int pid)
    {
        // Linux Process.StartTime converts uptime to wall-clock time separately in
        // each observer. Persist the kernel's exact boot/start ticks instead.
        var stat = File.ReadAllText("/proc/" + pid.ToString(CultureInfo.InvariantCulture) + "/stat");
        var close = stat.LastIndexOf(')'); // comm may contain spaces and parentheses.
        var fields = close < 0 ? [] : stat[(close + 1)..].Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var boot = File.ReadAllText("/proc/sys/kernel/random/boot_id").Trim();
        if (fields.Length <= 19 || !ulong.TryParse(fields[19], NumberStyles.None, CultureInfo.InvariantCulture, out var ticks)
            || !Guid.TryParseExact(boot, "D", out _)) throw new IOException("Linux process start identity is unavailable.");
        return boot + ":" + ticks.ToString(CultureInfo.InvariantCulture);
    }
    private static async Task<ProtocolError?> StopProcess(AgentRunProcess identity, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if (!IsAlive(identity)) return null;
        try
        {
            using var process = Process.GetProcessById(identity.ProcessId);
            if (!SameProcess(identity) || identity.ProcessId == Environment.ProcessId)
                return new("run_process_identity_changed", "A PID was reused or names the recovery process; no termination was authorized.");
            if (OperatingSystem.IsLinux() && identity.Role != "app")
            {
                _ = Kill(identity.ProcessId, 15);
                try { await process.WaitForExitAsync(token).WaitAsync(TimeSpan.FromSeconds(2), token); }
                catch (TimeoutException) { }
            }
            if (!process.HasExited) process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync(token).WaitAsync(TimeSpan.FromSeconds(5), token);
            return null;
        }
        catch (Exception exception) when (exception is InvalidOperationException or System.ComponentModel.Win32Exception or TimeoutException)
        { return new("run_process_cleanup_failed", "An owned process could not be terminated: " + exception.Message); }
    }
    [DllImport("libc", EntryPoint = "kill", SetLastError = true)]
    private static extern int Kill(int pid, int signal);

    private static async Task<ProtocolError?> RemoveOwnedSocket(Record record, CancellationToken token)
    {
        if (OperatingSystem.IsWindows() || !File.Exists(record.PipePath)) return null;
        var path = record.PipePath!;
        if (record.SessionProcess is null || IsAlive(record.SessionProcess)
            || !Path.GetFileName(path).StartsWith("CoreFxPipe_avs-" + record.SessionProcess.ProcessId + "-", StringComparison.Ordinal)
            || !PathComparer.Equals(Path.GetDirectoryName(path), Path.TrimEndingDirectorySeparator(Path.GetTempPath())))
            return new("run_socket_ownership_changed", "Transport path or process ownership no longer matches; the socket was retained.");
        try
        {
            RejectLink(path);
            // Use the platform's bounded, read-only file-type query; a replaced regular file must never be unlinked.
            var start = new ProcessStartInfo("stat") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
            foreach (var argument in OperatingSystem.IsMacOS() ? new[] { "-f", "%HT", path } : new[] { "-c", "%F", "--", path }) start.ArgumentList.Add(argument);
            using var stat = Process.Start(start)!;
            var output = stat.StandardOutput.ReadToEndAsync(token);
            var error = stat.StandardError.ReadToEndAsync(token);
            try { await stat.WaitForExitAsync(token).WaitAsync(TimeSpan.FromSeconds(1), token); }
            finally { if (!stat.HasExited) stat.Kill(); }
            if (stat.ExitCode != 0 || !string.Equals((await output).Trim(), "socket", StringComparison.OrdinalIgnoreCase))
                return new("run_socket_type_changed", "The recorded transport is no longer a verified Unix socket; it was retained.");
            await error;
            using var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
            try
            {
                await socket.ConnectAsync(new UnixDomainSocketEndPoint(path), token).AsTask().WaitAsync(TimeSpan.FromMilliseconds(250), token);
                return new("run_socket_still_listening", "The old transport path still accepts connections; it was retained.");
            }
            catch (SocketException exception) when (exception.SocketErrorCode == SocketError.ConnectionRefused)
            { RejectLink(path); File.Delete(path); }
            catch (SocketException) when (!File.Exists(path)) { }
            return null;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception or TimeoutException or SocketException)
        { return new("run_socket_cleanup_failed", "The recorded Unix socket could not be verified or removed: " + exception.Message); }
    }
    private static void ValidateRuntimeDirectory(string path, string runId)
    {
        if (!PathComparer.Equals(Path.GetDirectoryName(path), Path.TrimEndingDirectorySeparator(Path.GetTempPath()))
            || !Path.GetFileName(path).StartsWith("avs-x11-", StringComparison.Ordinal)) throw new IOException("Runtime path is outside its fixed temporary parent.");
        RejectLink(path);
        var marker = Path.Combine(path, ".avascope-run");
        RejectLink(marker);
        if (File.ReadAllText(marker) != runId) throw new IOException("Runtime ownership marker does not match.");
        var pending = new Stack<string>();
        pending.Push(path);
        var count = 0;
        while (pending.TryPop(out var directory))
            foreach (var entry in Directory.EnumerateFileSystemEntries(directory))
            {
                if (++count > 1024) throw new IOException("Runtime cleanup exceeds 1024 entries.");
                RejectLink(entry);
                if (Directory.Exists(entry)) pending.Push(entry);
            }
    }
    private static CoreResult<T> Fail<T>(string code, string message) => CoreResult<T>.Fail(new(code, message));

    internal sealed record Record(string RunId, string State, DateTimeOffset StartedAt, string ManifestDirectory,
        IReadOnlyList<string> Paths, string? ProviderVersion, string? ProviderManifestSha256)
    {
        public string Machine { get; init; } = Environment.MachineName;
        public SessionId? SessionId { get; init; }
        public AgentRunProcess? SessionProcess { get; init; }
        public string? ManifestPath { get; init; }
        public string? PipePath { get; init; }
        public string? X11Display { get; init; }
        public string? Outcome { get; init; }
        public string? FailureStage { get; init; }
        public string? ControlToken { get; init; }
        public IReadOnlyList<AgentRunProcess> Processes { get; init; } = [];
        public IReadOnlyList<string> RuntimeDirectories { get; init; } = [];
    }

    internal sealed class Registration(AgentRunStore store, Record record, FileStream activeLock) : IDisposable
    {
        private readonly object _sync = new();
        public string RunId => record.RunId;
        public void ProcessStarted(string role, Process process)
        {
            lock (_sync)
            {
                record = record with { Processes = [.. record.Processes, ProcessIdentity(role, process)] };
                store.Save(record);
            }
        }
        public void OwnRuntimeDirectory(string path)
        {
            RejectLink(path);
            File.WriteAllText(Path.Combine(path, ".avascope-run"), RunId);
            lock (_sync) { record = record with { RuntimeDirectories = [.. record.RuntimeDirectories, path] }; store.Save(record); }
        }
        public void Session(AttachToAppResponse session, string? token)
        {
            using var process = Process.GetProcessById(session.ProcessId);
            var manifest = session.ManifestPath is { } manifestPath
                ? JsonSerializer.Deserialize<BridgeSessionManifest>(File.ReadAllText(manifestPath)) : null;
            lock (_sync)
            {
                record = record with { SessionId = session.Session.SessionId, SessionProcess = ProcessIdentity("session", process),
                    ManifestPath = session.ManifestPath, ControlToken = token,
                    PipePath = !OperatingSystem.IsWindows() && manifest?.PipeName.StartsWith("avs-" + session.ProcessId + "-", StringComparison.Ordinal) == true
                        && Path.GetFileName(manifest.PipeName) == manifest.PipeName ? Path.Combine(Path.GetTempPath(), "CoreFxPipe_" + manifest.PipeName) : null };
                store.Save(record);
            }
        }
        public void EnvironmentChanged(RuntimeEnvironmentEvidence evidence)
        { lock (_sync) { record = record with { X11Display = evidence.Mode == "managed" ? evidence.Display : null }; store.Save(record); } }
        public void Complete(string state, string? outcome = null, string? failureStage = null)
        { lock (_sync) { record = record with { State = state, Outcome = outcome, FailureStage = failureStage,
            ControlToken = state == "partial_cleanup" ? record.ControlToken : null }; store.Save(record); } }
        public void Dispose() => activeLock.Dispose();
    }
}
