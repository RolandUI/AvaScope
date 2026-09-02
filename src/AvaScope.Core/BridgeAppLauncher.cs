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
        CancellationToken cancellationToken = default,
        IReadOnlyList<string>? argumentList = null,
        bool directProcess = false,
        bool terminateOnFailure = false,
        bool captureOutputUntilExit = false)
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
        var detachedLauncher = OperatingSystem.IsWindows() && !directProcess;
        var process = new Process
        {
            StartInfo = CreateStartInfo(command, arguments, argumentList, fullWorkingDirectory, detachedLauncher),
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
                return Fail(
                    "The configured app process could not be started.",
                    new Dictionary<string, string>
                    {
                        ["stdoutPath"] = Path.GetFullPath(stdoutPath),
                        ["stderrPath"] = Path.GetFullPath(stderrPath),
                        ["manifestDirectory"] = Path.GetFullPath(fullManifestDirectory),
                        ["failureStage"] = RuntimeScenarioFailureStages.Launch
                    });
            }
        }
        catch (Exception exception) when (exception is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            process.Dispose();
            return Fail(
                $"The configured app process could not be started: {exception.Message}",
                new Dictionary<string, string>
                {
                    ["stdoutPath"] = Path.GetFullPath(stdoutPath),
                    ["stderrPath"] = Path.GetFullPath(stderrPath),
                    ["manifestDirectory"] = Path.GetFullPath(fullManifestDirectory),
                    ["failureStage"] = RuntimeScenarioFailureStages.Launch
                });
        }

        var outputCancellation = new CancellationTokenSource();
        var stdoutTask = CopyToFileUntilExitAsync(process.StandardOutput, stdoutPath, outputCancellation.Token);
        var stderrTask = CopyToFileUntilExitAsync(process.StandardError, stderrPath, outputCancellation.Token);

        var client = new LocalBridgeClient(
            fullManifestDirectory,
            terminateOnFailure ? effectiveTimeout : null);
        using var readinessTimeout = new CancellationTokenSource(effectiveTimeout);
        using var readinessCancellation = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            readinessTimeout.Token);
        var readinessCancellationToken = terminateOnFailure
            ? readinessCancellation.Token
            : cancellationToken;
        var stopAt = DateTimeOffset.UtcNow + effectiveTimeout;
        var readinessChecks = 0;
        while (DateTimeOffset.UtcNow < stopAt)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                if (!terminateOnFailure)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                }

                var details = CreateLaunchDetails(
                    process,
                    stdoutPath,
                    stderrPath,
                    fullManifestDirectory,
                    displayName,
                    failureStage: RuntimeScenarioFailureStages.BridgeReadiness,
                    readinessChecks: readinessChecks);
                details["cancelled"] = "true";
                TerminateAndDetachLaunchProcess(process, outputCancellation, stdoutTask, stderrTask);
                return Fail("The app launch was cancelled while waiting for bridge readiness.", details);
            }

            readinessChecks++;

            if (!detachedLauncher && process.HasExited)
            {
                var details = CreateLaunchDetails(
                    process,
                    stdoutPath,
                    stderrPath,
                    fullManifestDirectory,
                    displayName,
                    process.ExitCode,
                    RuntimeScenarioFailureStages.Launch,
                    readinessChecks);
                DetachLaunchProcess(process, outputCancellation, stdoutTask, stderrTask);

                return Fail(
                    "The launched app process exited before an AvaScope bridge session appeared.",
                    details);
            }

            if (detachedLauncher && process.HasExited && process.ExitCode != 0)
            {
                var details = CreateLaunchDetails(
                    process,
                    stdoutPath,
                    stderrPath,
                    fullManifestDirectory,
                    displayName,
                    process.ExitCode,
                    RuntimeScenarioFailureStages.Launch,
                    readinessChecks);
                DetachLaunchProcess(process, outputCancellation, stdoutTask, stderrTask);

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
                CoreResult<AttachToAppResponse> attach;
                try
                {
                    attach = await client.AttachToAppAsync(
                        manifest.ProcessId,
                        manifest.SessionId,
                        processName: null,
                        cancellationToken: readinessCancellationToken);
                }
                catch (OperationCanceledException) when (terminateOnFailure && readinessCancellationToken.IsCancellationRequested)
                {
                    var details = CreateLaunchDetails(
                        process,
                        stdoutPath,
                        stderrPath,
                        fullManifestDirectory,
                        displayName,
                        failureStage: RuntimeScenarioFailureStages.Attach,
                        readinessChecks: readinessChecks);
                    var cancelled = cancellationToken.IsCancellationRequested;
                    details[cancelled ? "cancelled" : "timedOut"] = "true";
                    TerminateAndDetachLaunchProcess(process, outputCancellation, stdoutTask, stderrTask);
                    return Fail(
                        cancelled
                            ? "The app launch was cancelled while attaching to its bridge session."
                            : "Timed out while attaching to the launched app's bridge session.",
                        details);
                }

                if (!attach.Success)
                {
                    var details = CreateLaunchDetails(
                        process,
                        stdoutPath,
                        stderrPath,
                        fullManifestDirectory,
                        displayName,
                        failureStage: RuntimeScenarioFailureStages.Attach,
                        readinessChecks: readinessChecks);
                    foreach (var pair in attach.Error!.Details ?? new Dictionary<string, string>())
                    {
                        details.TryAdd(pair.Key, pair.Value);
                    }

                    if (terminateOnFailure)
                    {
                        TerminateAndDetachLaunchProcess(process, outputCancellation, stdoutTask, stderrTask);
                    }
                    else
                    {
                        DetachLaunchProcess(process, outputCancellation, stdoutTask, stderrTask);
                    }

                    return CoreResult<LaunchAppResponse>.Fail(new CoreError(
                        attach.Error.Code,
                        attach.Error.Message,
                        details));
                }

                CoreResult<ListTopLevelsResponse> topLevels;
                try
                {
                    topLevels = await client.ListTopLevelsAsync(manifest.SessionId, readinessCancellationToken);
                }
                catch (OperationCanceledException) when (terminateOnFailure && readinessCancellationToken.IsCancellationRequested)
                {
                    var details = CreateLaunchDetails(
                        process,
                        stdoutPath,
                        stderrPath,
                        fullManifestDirectory,
                        displayName,
                        failureStage: RuntimeScenarioFailureStages.TopLevels,
                        readinessChecks: readinessChecks);
                    var cancelled = cancellationToken.IsCancellationRequested;
                    details[cancelled ? "cancelled" : "timedOut"] = "true";
                    TerminateAndDetachLaunchProcess(process, outputCancellation, stdoutTask, stderrTask);
                    return Fail(
                        cancelled
                            ? "The app launch was cancelled while reading registered top levels."
                            : "Timed out while reading registered top levels from the launched app.",
                        details);
                }

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
                    var details = CreateLaunchDetails(
                        process,
                        stdoutPath,
                        stderrPath,
                        fullManifestDirectory,
                        displayName,
                        failureStage: RuntimeScenarioFailureStages.Launch,
                        readinessChecks: readinessChecks);
                    DetachLaunchProcess(process, outputCancellation, stdoutTask, stderrTask);
                    return Fail("The launched app process exited before launch ownership could be recorded.", details);
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
                    var details = CreateLaunchDetails(
                        process,
                        stdoutPath,
                        stderrPath,
                        fullManifestDirectory,
                        displayName,
                        failureStage: RuntimeScenarioFailureStages.Launch,
                        readinessChecks: readinessChecks);
                    if (terminateOnFailure)
                    {
                        TerminateAndDetachLaunchProcess(process, outputCancellation, stdoutTask, stderrTask);
                    }
                    else
                    {
                        DetachLaunchProcess(process, outputCancellation, stdoutTask, stderrTask);
                    }

                    return Fail($"The launched app was attached, but launch ownership could not be recorded: {exception.Message}", details);
                }

                if (captureOutputUntilExit && !detachedLauncher)
                {
                    _ = CompleteOutputCaptureAsync(process, outputCancellation, stdoutTask, stderrTask);
                }
                else
                {
                    DetachLaunchProcess(process, outputCancellation, stdoutTask, stderrTask);
                }

                return CoreResult<LaunchAppResponse>.Ok(response);
            }

            try
            {
                await Task.Delay(TimeSpan.FromMilliseconds(100), readinessCancellationToken);
            }
            catch (OperationCanceledException) when (terminateOnFailure && readinessCancellationToken.IsCancellationRequested)
            {
                var details = CreateLaunchDetails(
                    process,
                    stdoutPath,
                    stderrPath,
                    fullManifestDirectory,
                    displayName,
                    failureStage: RuntimeScenarioFailureStages.BridgeReadiness,
                    readinessChecks: readinessChecks);
                var cancelled = cancellationToken.IsCancellationRequested;
                details[cancelled ? "cancelled" : "timedOut"] = "true";
                TerminateAndDetachLaunchProcess(process, outputCancellation, stdoutTask, stderrTask);
                return Fail(
                    cancelled
                        ? "The app launch was cancelled while waiting for bridge readiness."
                        : "Timed out waiting for bridge readiness from the launched app.",
                    details);
            }
        }

        var timeoutDetails = CreateLaunchDetails(
            process,
            stdoutPath,
            stderrPath,
            fullManifestDirectory,
            displayName,
            failureStage: RuntimeScenarioFailureStages.BridgeReadiness,
            readinessChecks: readinessChecks);
        timeoutDetails["timedOut"] = "true";
        if (terminateOnFailure)
        {
            TerminateAndDetachLaunchProcess(process, outputCancellation, stdoutTask, stderrTask);
        }
        else
        {
            DetachLaunchProcess(process, outputCancellation, stdoutTask, stderrTask);
        }

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
        IReadOnlyList<string>? argumentList,
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

        var startInfo = new ProcessStartInfo
        {
            FileName = command,
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        if (argumentList is { Count: > 0 })
        {
            foreach (var argument in argumentList)
            {
                startInfo.ArgumentList.Add(argument);
            }
        }
        else
        {
            startInfo.Arguments = arguments ?? string.Empty;
        }

        return startInfo;
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
            return orderedManifests.FirstOrDefault(session => session.ProcessId == launcherProcessId);
        }

        var earliestManifestTime = startedAt.AddSeconds(-1);
        return orderedManifests.FirstOrDefault(session => session.CreatedAt >= earliestManifestTime);
    }

    private static void DetachLaunchProcess(
        Process process,
        CancellationTokenSource outputCancellation,
        Task stdoutTask,
        Task stderrTask)
    {
        outputCancellation.Cancel();
        _ = ObserveOutputTasksAsync(stdoutTask, stderrTask, outputCancellation);
        process.Dispose();
    }

    private static void TerminateAndDetachLaunchProcess(
        Process process,
        CancellationTokenSource outputCancellation,
        Task stdoutTask,
        Task stderrTask)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                process.WaitForExit(5000);
            }
        }
        catch (Exception exception) when (exception is InvalidOperationException or System.ComponentModel.Win32Exception or NotSupportedException)
        {
        }

        DetachLaunchProcess(process, outputCancellation, stdoutTask, stderrTask);
    }

    private static async Task CompleteOutputCaptureAsync(
        Process process,
        CancellationTokenSource outputCancellation,
        Task stdoutTask,
        Task stderrTask)
    {
        try
        {
            await Task.WhenAll(stdoutTask, stderrTask);
        }
        finally
        {
            outputCancellation.Dispose();
            process.Dispose();
        }
    }

    private static async Task ObserveOutputTasksAsync(
        Task stdoutTask,
        Task stderrTask,
        CancellationTokenSource outputCancellation)
    {
        try
        {
            await Task.WhenAll(stdoutTask, stderrTask);
        }
        catch (Exception exception) when (exception is OperationCanceledException or ObjectDisposedException or IOException)
        {
        }
        finally
        {
            outputCancellation.Dispose();
        }
    }

    private static Dictionary<string, string> CreateLaunchDetails(
        Process process,
        string stdoutPath,
        string stderrPath,
        string manifestDirectory,
        string? displayName,
        int? exitCode = null,
        string? failureStage = null,
        int? readinessChecks = null)
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

        if (!string.IsNullOrWhiteSpace(failureStage))
        {
            details["failureStage"] = failureStage;
        }

        if (readinessChecks is not null)
        {
            details["readinessChecks"] = readinessChecks.Value.ToString(CultureInfo.InvariantCulture);
        }

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
