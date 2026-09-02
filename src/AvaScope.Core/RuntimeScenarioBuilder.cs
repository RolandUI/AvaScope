using System.Diagnostics;
using System.Globalization;
using AvaScope.Protocol;

namespace AvaScope.Core;

internal sealed class RuntimeScenarioBuilder
{
    public async Task<RuntimeScenarioBuildResult> BuildAsync(
        RuntimeScenarioBuildOptions options,
        string outputDirectory,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(options);

        var startedAt = DateTimeOffset.UtcNow;
        var buildDirectory = Path.Combine(Path.GetFullPath(outputDirectory), "build");
        Directory.CreateDirectory(buildDirectory);
        var stdoutPath = Path.Combine(buildDirectory, "stdout.log");
        var stderrPath = Path.Combine(buildDirectory, "stderr.log");
        File.WriteAllText(stdoutPath, string.Empty);
        File.WriteAllText(stderrPath, string.Empty);

        var metadata = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["environmentVariableNames"] = string.Join(',', options.Environment.Keys.Order(StringComparer.Ordinal)),
            ["argumentCount"] = options.Arguments.Count.ToString(CultureInfo.InvariantCulture),
            ["noRestore"] = options.NoRestore.ToString().ToLowerInvariant()
        };

        if (!File.Exists(options.ProjectPath))
        {
            return CreateResult(
                RuntimeScenarioLifecycleStatuses.Failed,
                options,
                startedAt,
                stdoutPath,
                stderrPath,
                diagnostic: CreateDiagnostic(
                    "runtime_scenario_build_project_not_found",
                    "The configured scenario build project does not exist.",
                    options,
                    stdoutPath,
                    stderrPath),
                metadata: metadata);
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            WorkingDirectory = options.WorkingDirectory ?? Path.GetDirectoryName(options.ProjectPath) ?? Environment.CurrentDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        startInfo.ArgumentList.Add("build");
        startInfo.ArgumentList.Add(options.ProjectPath);
        startInfo.ArgumentList.Add("--configuration");
        startInfo.ArgumentList.Add(options.Configuration);
        if (!string.IsNullOrWhiteSpace(options.Framework))
        {
            startInfo.ArgumentList.Add("--framework");
            startInfo.ArgumentList.Add(options.Framework);
        }

        if (!string.IsNullOrWhiteSpace(options.RuntimeIdentifier))
        {
            startInfo.ArgumentList.Add("--runtime");
            startInfo.ArgumentList.Add(options.RuntimeIdentifier);
        }

        if (options.NoRestore)
        {
            startInfo.ArgumentList.Add("--no-restore");
        }

        foreach (var argument in options.Arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        foreach (var pair in options.Environment)
        {
            if (!string.IsNullOrWhiteSpace(pair.Key))
            {
                startInfo.Environment[pair.Key] = pair.Value;
            }
        }

        using var process = new Process { StartInfo = startInfo };
        try
        {
            if (!process.Start())
            {
                return CreateResult(
                    RuntimeScenarioLifecycleStatuses.Failed,
                    options,
                    startedAt,
                    stdoutPath,
                    stderrPath,
                    diagnostic: CreateDiagnostic(
                        "runtime_scenario_build_start_failed",
                        "The scenario build process could not be started.",
                        options,
                        stdoutPath,
                        stderrPath),
                    metadata: metadata);
            }
        }
        catch (Exception exception) when (exception is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            return CreateResult(
                RuntimeScenarioLifecycleStatuses.Failed,
                options,
                startedAt,
                stdoutPath,
                stderrPath,
                diagnostic: CreateDiagnostic(
                    "runtime_scenario_build_start_failed",
                    $"The scenario build process could not be started: {exception.Message}",
                    options,
                    stdoutPath,
                    stderrPath),
                metadata: metadata);
        }

        var stdoutTask = process.StandardOutput.ReadToEndAsync();
        var stderrTask = process.StandardError.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromMilliseconds(options.TimeoutMs));
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);

        try
        {
            await process.WaitForExitAsync(linked.Token);
        }
        catch (OperationCanceledException)
        {
            var killRequested = TryKillOwnedProcess(process);
            var exitedAfterTermination = await WaitForExitBoundedAsync(process, TimeSpan.FromSeconds(5));
            var cancelled = cancellationToken.IsCancellationRequested;
            await WriteLogsBoundedAsync(
                stdoutPath,
                stderrPath,
                stdoutTask,
                stderrTask,
                TimeSpan.FromSeconds(1));
            metadata["ownedProcessKillRequested"] = killRequested.ToString().ToLowerInvariant();
            metadata["ownedProcessExited"] = exitedAfterTermination.ToString().ToLowerInvariant();
            var status = cancelled
                ? RuntimeScenarioLifecycleStatuses.Cancelled
                : RuntimeScenarioLifecycleStatuses.TimedOut;
            var code = cancelled
                ? "runtime_scenario_build_cancelled"
                : "runtime_scenario_build_timed_out";
            var message = cancelled
                ? "The scenario build was cancelled and its owned process tree was terminated."
                : $"The scenario build exceeded its {options.TimeoutMs.ToString(CultureInfo.InvariantCulture)} ms timeout and its owned process tree was terminated.";
            return CreateResult(
                status,
                options,
                startedAt,
                stdoutPath,
                stderrPath,
                process.HasExited ? process.ExitCode : null,
                CreateDiagnostic(code, message, options, stdoutPath, stderrPath),
                metadata);
        }

        await WriteLogsAsync(stdoutPath, stderrPath, stdoutTask, stderrTask);
        if (process.ExitCode == 0)
        {
            return CreateResult(
                RuntimeScenarioLifecycleStatuses.Passed,
                options,
                startedAt,
                stdoutPath,
                stderrPath,
                process.ExitCode,
                metadata: metadata);
        }

        return CreateResult(
            RuntimeScenarioLifecycleStatuses.Failed,
            options,
            startedAt,
            stdoutPath,
            stderrPath,
            process.ExitCode,
            CreateDiagnostic(
                "runtime_scenario_build_failed",
                $"The scenario build exited with code {process.ExitCode.ToString(CultureInfo.InvariantCulture)}. Inspect the captured build logs.",
                options,
                stdoutPath,
                stderrPath,
                process.ExitCode),
            metadata);
    }

    private static async Task WriteLogsAsync(
        string stdoutPath,
        string stderrPath,
        Task<string> stdoutTask,
        Task<string> stderrTask)
    {
        await File.WriteAllTextAsync(stdoutPath, await stdoutTask);
        await File.WriteAllTextAsync(stderrPath, await stderrTask);
    }

    private static async Task WriteLogsBoundedAsync(
        string stdoutPath,
        string stderrPath,
        Task<string> stdoutTask,
        Task<string> stderrTask,
        TimeSpan timeout)
    {
        var captureTask = Task.WhenAll(stdoutTask, stderrTask);
        await Task.WhenAny(captureTask, Task.Delay(timeout));
        await File.WriteAllTextAsync(
            stdoutPath,
            stdoutTask.IsCompletedSuccessfully
                ? stdoutTask.Result
                : "Output capture did not complete before the bounded termination deadline.");
        await File.WriteAllTextAsync(
            stderrPath,
            stderrTask.IsCompletedSuccessfully
                ? stderrTask.Result
                : "Error output capture did not complete before the bounded termination deadline.");
    }

    private static async Task<bool> WaitForExitBoundedAsync(Process process, TimeSpan timeout)
    {
        if (process.HasExited)
        {
            return true;
        }

        using var cancellation = new CancellationTokenSource(timeout);
        try
        {
            await process.WaitForExitAsync(cancellation.Token);
            return true;
        }
        catch (OperationCanceledException)
        {
            return process.HasExited;
        }
    }

    private static bool TryKillOwnedProcess(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                return true;
            }

            return false;
        }
        catch (Exception exception) when (exception is InvalidOperationException or System.ComponentModel.Win32Exception or NotSupportedException)
        {
            return false;
        }
    }

    private static RuntimeScenarioBuildResult CreateResult(
        string status,
        RuntimeScenarioBuildOptions options,
        DateTimeOffset startedAt,
        string stdoutPath,
        string stderrPath,
        int? exitCode = null,
        ProtocolError? diagnostic = null,
        IReadOnlyDictionary<string, string>? metadata = null)
    {
        return new RuntimeScenarioBuildResult(
            status,
            options.ProjectPath,
            options.Configuration,
            startedAt,
            DateTimeOffset.UtcNow,
            stdoutPath,
            stderrPath,
            exitCode,
            diagnostic,
            metadata);
    }

    private static ProtocolError CreateDiagnostic(
        string code,
        string message,
        RuntimeScenarioBuildOptions options,
        string stdoutPath,
        string stderrPath,
        int? exitCode = null)
    {
        var details = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["projectPath"] = options.ProjectPath,
            ["stdoutPath"] = Path.GetFullPath(stdoutPath),
            ["stderrPath"] = Path.GetFullPath(stderrPath),
            ["failureStage"] = RuntimeScenarioFailureStages.Build,
            ["nextAction"] = "Inspect the captured build logs, correct the project build, and retry the same bounded scenario."
        };
        if (exitCode is not null)
        {
            details["exitCode"] = exitCode.Value.ToString(CultureInfo.InvariantCulture);
        }

        return new ProtocolError(code, message, details);
    }
}
