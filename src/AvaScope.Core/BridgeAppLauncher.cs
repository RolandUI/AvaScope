using System.Diagnostics;
using System.Globalization;
using System.Text;
using AvaScope.Protocol;

namespace AvaScope.Core;

public sealed class BridgeAppLauncher
{
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(15);

    public async Task<CoreResult<LaunchAppResponse>> LaunchAsync(
        string command,
        string? arguments = null,
        string? workingDirectory = null,
        string? displayName = null,
        string? manifestDirectory = null,
        string? outputDirectory = null,
        IReadOnlyDictionary<string, string>? environment = null,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(command))
        {
            return Fail("Launch command cannot be empty.");
        }

        var effectiveTimeout = timeout ?? DefaultTimeout;
        if (effectiveTimeout <= TimeSpan.Zero)
        {
            return Fail("Launch timeout must be positive.");
        }

        var fullManifestDirectory = Path.GetFullPath(
            string.IsNullOrWhiteSpace(manifestDirectory)
                ? BridgeSessionManifest.GetDefaultDirectory()
                : manifestDirectory);
        var fullOutputDirectory = Path.GetFullPath(
            string.IsNullOrWhiteSpace(outputDirectory)
                ? Path.Combine(Path.GetTempPath(), "AvaScope", "launches", Guid.NewGuid().ToString("n"))
                : outputDirectory);
        Directory.CreateDirectory(fullManifestDirectory);
        Directory.CreateDirectory(fullOutputDirectory);

        var stdoutPath = Path.Combine(fullOutputDirectory, "stdout.log");
        var stderrPath = Path.Combine(fullOutputDirectory, "stderr.log");
        File.WriteAllText(stdoutPath, string.Empty);
        File.WriteAllText(stderrPath, string.Empty);

        var fullWorkingDirectory = string.IsNullOrWhiteSpace(workingDirectory)
            ? Environment.CurrentDirectory
            : Path.GetFullPath(workingDirectory);
        var detachedLauncher = OperatingSystem.IsWindows();
        var process = new Process
        {
            StartInfo = CreateStartInfo(command, arguments, fullWorkingDirectory, detachedLauncher),
            EnableRaisingEvents = true
        };

        if (environment is not null)
        {
            foreach (var pair in environment)
            {
                if (!string.IsNullOrWhiteSpace(pair.Key))
                {
                    process.StartInfo.Environment[pair.Key] = pair.Value;
                }
            }
        }

        process.StartInfo.Environment[BridgeSessionManifest.DirectoryEnvironmentVariable] = fullManifestDirectory;

        var startedAt = DateTimeOffset.UtcNow;
        try
        {
            if (!process.Start())
            {
                process.Dispose();
                return Fail("The configured app process could not be started.");
            }
        }
        catch (Exception exception) when (exception is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            process.Dispose();
            return Fail($"The configured app process could not be started: {exception.Message}");
        }

        var outputCancellation = new CancellationTokenSource();
        _ = CopyToFileUntilExitAsync(process.StandardOutput, stdoutPath, outputCancellation.Token);
        _ = CopyToFileUntilExitAsync(process.StandardError, stderrPath, outputCancellation.Token);

        var client = new LocalBridgeClient(fullManifestDirectory);
        var stopAt = DateTimeOffset.UtcNow + effectiveTimeout;
        while (DateTimeOffset.UtcNow < stopAt)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!detachedLauncher && process.HasExited)
            {
                var details = CreateLaunchDetails(process, stdoutPath, stderrPath, fullManifestDirectory, displayName, process.ExitCode);
                DetachLaunchProcess(process, outputCancellation);

                return Fail(
                    "The launched app process exited before an AvaScope bridge session appeared.",
                    details);
            }

            if (detachedLauncher && process.HasExited && process.ExitCode != 0)
            {
                var details = CreateLaunchDetails(process, stdoutPath, stderrPath, fullManifestDirectory, displayName, process.ExitCode);
                DetachLaunchProcess(process, outputCancellation);

                return Fail(
                    "The launch helper exited before an AvaScope bridge session appeared.",
                    details);
            }

            var manifest = SelectLaunchedManifest(
                client.ListSessionManifests(),
                process.Id,
                startedAt,
                detachedLauncher);
            if (manifest is not null)
            {
                var attach = await client.AttachToAppAsync(
                    manifest.ProcessId,
                    manifest.SessionId,
                    processName: null,
                    cancellationToken: cancellationToken);
                if (!attach.Success)
                {
                    DetachLaunchProcess(process, outputCancellation);
                    return CoreResult<LaunchAppResponse>.Fail(attach.Error!);
                }

                var topLevels = await client.ListTopLevelsAsync(manifest.SessionId, cancellationToken);
                var topLevelId = topLevels.Success
                    ? topLevels.Value!.TopLevels.FirstOrDefault()?.Id
                    : null;

                var response = new LaunchAppResponse(
                    attach.Value!.Session,
                    manifest.ProcessId,
                    attach.Value.ProcessName ?? TryGetProcessName(manifest.ProcessId) ?? manifest.ProcessName ?? Path.GetFileNameWithoutExtension(command),
                    stdoutPath,
                    stderrPath,
                    startedAt,
                    DateTimeOffset.UtcNow,
                    topLevelId,
                    attach.Value.ManifestPath);

                if (!LaunchOwnershipStore.TryGetProcessIdentity(manifest.ProcessId, out var launchedProcess, out var processStartedAt))
                {
                    DetachLaunchProcess(process, outputCancellation);
                    return Fail("The launched app process exited before launch ownership could be recorded.");
                }

                launchedProcess.Dispose();
                try
                {
                    new LaunchOwnershipStore(fullManifestDirectory).Save(new LaunchOwnershipRecord(
                        response.Session,
                        response.ProcessId,
                        response.ProcessName,
                        processStartedAt,
                        startedAt));
                }
                catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
                {
                    DetachLaunchProcess(process, outputCancellation);
                    return Fail($"The launched app was attached, but launch ownership could not be recorded: {exception.Message}");
                }

                DetachLaunchProcess(process, outputCancellation);
                return CoreResult<LaunchAppResponse>.Ok(response);
            }

            await Task.Delay(TimeSpan.FromMilliseconds(100), cancellationToken);
        }

        var timeoutDetails = CreateLaunchDetails(process, stdoutPath, stderrPath, fullManifestDirectory, displayName);
        DetachLaunchProcess(process, outputCancellation);

        return Fail(
            "Timed out waiting for an AvaScope bridge session from the launched app.",
            timeoutDetails);
    }

    private static async Task CopyToFileUntilExitAsync(StreamReader reader, string path, CancellationToken cancellationToken)
    {
        try
        {
            await using var stream = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
            await using var writer = new StreamWriter(stream);
            while (!cancellationToken.IsCancellationRequested)
            {
                var line = await reader.ReadLineAsync(cancellationToken);
                if (line is null)
                {
                    break;
                }

                await writer.WriteLineAsync(line);
                await writer.FlushAsync(cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (ObjectDisposedException)
        {
        }
        catch (IOException)
        {
        }
    }

    private static ProcessStartInfo CreateStartInfo(
        string command,
        string? arguments,
        string workingDirectory,
        bool detachedLauncher)
    {
        if (detachedLauncher)
        {
            return new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-NoProfile -ExecutionPolicy Bypass -EncodedCommand {EncodePowerShellCommand(CreateWindowsLaunchScript(command, arguments, workingDirectory))}",
                WorkingDirectory = workingDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
        }

        return new ProcessStartInfo
        {
            FileName = command,
            Arguments = arguments ?? string.Empty,
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
    }

    private static string CreateWindowsLaunchScript(string command, string? arguments, string workingDirectory)
    {
        var script = new StringBuilder();
        script.Append("$ErrorActionPreference = 'Stop'; ");
        script.Append("Start-Process -FilePath ");
        script.Append(ToPowerShellLiteral(command));

        if (!string.IsNullOrWhiteSpace(arguments))
        {
            script.Append(" -ArgumentList ");
            script.Append(ToPowerShellLiteral(arguments));
        }

        script.Append(" -WorkingDirectory ");
        script.Append(ToPowerShellLiteral(workingDirectory));
        return script.ToString();
    }

    private static string EncodePowerShellCommand(string command)
    {
        return Convert.ToBase64String(Encoding.Unicode.GetBytes(command));
    }

    private static string ToPowerShellLiteral(string value)
    {
        return $"'{value.Replace("'", "''")}'";
    }

    private static BridgeSessionManifest? SelectLaunchedManifest(
        IEnumerable<BridgeSessionManifest> manifests,
        int launcherProcessId,
        DateTimeOffset startedAt,
        bool detachedLauncher)
    {
        var orderedManifests = manifests
            .OrderByDescending(static session => session.CreatedAt)
            .ToArray();

        if (!detachedLauncher)
        {
            var processMatch = orderedManifests.FirstOrDefault(session => session.ProcessId == launcherProcessId);
            if (processMatch is not null)
            {
                return processMatch;
            }
        }

        var earliestManifestTime = startedAt.AddSeconds(-1);
        return orderedManifests.FirstOrDefault(session => session.CreatedAt >= earliestManifestTime);
    }

    private static void DetachLaunchProcess(Process process, CancellationTokenSource outputCancellation)
    {
        outputCancellation.Cancel();
        process.Dispose();
    }

    private static IReadOnlyDictionary<string, string> CreateLaunchDetails(
        Process process,
        string stdoutPath,
        string stderrPath,
        string manifestDirectory,
        string? displayName,
        int? exitCode = null)
    {
        var details = new Dictionary<string, string>
        {
            ["processId"] = process.Id.ToString(CultureInfo.InvariantCulture),
            ["stdoutPath"] = Path.GetFullPath(stdoutPath),
            ["stderrPath"] = Path.GetFullPath(stderrPath),
            ["manifestDirectory"] = Path.GetFullPath(manifestDirectory),
            ["bridgeActivation"] = "explicit_app_opt_in_required",
            ["nextAction"] = $"Ensure the launched app enables AvaScopeBridge.Activate and writes a local manifest under {BridgeSessionManifest.DirectoryEnvironmentVariable}."
        };

        if (!string.IsNullOrWhiteSpace(displayName))
        {
            details["displayName"] = displayName.Trim();
        }

        if (exitCode is not null)
        {
            details["exitCode"] = exitCode.Value.ToString(CultureInfo.InvariantCulture);
        }

        return details;
    }

    private static string? TryGetProcessName(Process process)
    {
        try
        {
            return process.HasExited ? null : process.ProcessName;
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }

    private static string? TryGetProcessName(int processId)
    {
        try
        {
            using var process = Process.GetProcessById(processId);
            return process.HasExited ? null : process.ProcessName;
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            return null;
        }
    }

    private static CoreResult<LaunchAppResponse> Fail(
        string message,
        IReadOnlyDictionary<string, string>? details = null)
    {
        return CoreResult<LaunchAppResponse>.Fail(new CoreError(
            CoreErrorCodes.BridgeSessionNotFound,
            message,
            details));
    }
}
