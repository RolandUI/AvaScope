using System.Diagnostics;
using System.Text.Json;
using AvaScope.Protocol;

namespace AvaScope.Core;

public sealed class BridgeIntegrationVerifier
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    public async Task<CoreResult<BridgeIntegrationVerificationResponse>> RunAsync(
        BridgeIntegrationVerificationRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Launch?.Command is null || !Path.IsPathFullyQualified(request.OutputDirectory ?? "") ||
            request.Launch.TimeoutMs > 120000 || request.Build?.TimeoutMs > 300000 || request.ObservationMs is < 250 or > 30000 ||
            (request.SafeInputTarget is not null && !request.SafeInputTargetDeclared) ||
            (request.BootstrapDisabled && !Path.IsPathFullyQualified(request.ProductionOutputDirectory ?? "")))
        {
            return CoreResult<BridgeIntegrationVerificationResponse>.Fail(new("integration_verification_invalid",
                "Provide a direct launch.command (for example dotnet plus the explicit built DLL in argumentList), an absolute outputDirectory, launch timeout <=120000 ms, build timeout <=300000 ms and observationMs 250–30000. Disabled verification requires an absolute productionOutputDirectory. A focus probe requires safeInputTargetDeclared=true."));
        }

        var root = Path.Combine(request.OutputDirectory!, $"integration-{Guid.NewGuid():N}");
        var manifests = Path.Combine(root, "sessions");
        var stages = new List<IntegrationVerificationStage>();
        RuntimeScenarioResponse? scenario = null;
        var currentStage = "build";
        var environment = new Dictionary<string, string>(request.Launch.Environment, StringComparer.Ordinal);
        try
        {
            Directory.CreateDirectory(manifests);
            if (request.Build is not null)
            {
                var build = await new RuntimeScenarioBuilder().BuildAsync(request.Build, root, cancellationToken);
                stages.Add(new("build", build.Status, build.Diagnostic?.Message ?? "Selected project built.",
                    build.Status == "passed" ? null : "Read the build logs; fix compilation before verifying activation.", [build.StdoutPath, build.StderrPath]));
                if (build.Status != "passed") return Complete();
            }
            else stages.Add(new("build", "skipped", "Using an explicitly selected existing output."));

            if (request.ProviderDirectory is not null)
            {
                currentStage = "provider";
                var provider = ProviderVerifier.Verify(request.ProviderDirectory, request.ExpectedProviderVersion, request.ExpectedManifestSha256);
                stages.Add(new("provider", provider.Success ? "passed" : "failed", provider.Error?.Message ?? "Provider inventory, compatibility metadata and requested pins verified without loading.",
                    provider.Success ? null : "Supply the complete compatible provider distribution and correct exact pins."));
                if (!provider.Success) return Complete();
                environment["UI_INSPECTION_PROVIDER_PATH"] = request.ProviderDirectory;
                environment["UI_INSPECTION_PROVIDER_VERSION"] = provider.Value!.ProviderVersion;
                environment["UI_INSPECTION_PROVIDER_SHA256"] = provider.Value.ManifestSha256;
            }
            else stages.Add(new("provider", "skipped", "No external provider requested; host integration controls activation."));

            if (request.BootstrapDisabled)
            {
                currentStage = "production_output";
                var assemblies = FindProductionAssemblies(request.ProductionOutputDirectory!);
                stages.Add(new("production_output", assemblies.Length == 0 ? "passed" : "failed",
                    assemblies.Length == 0 ? "Production output contains no AvaScope assemblies." : $"Production output contains: {string.Join(", ", assemblies)}",
                    assemblies.Length == 0 ? null : "Exclude all AvaScope package/project references and provider files from this production output."));
                if (assemblies.Length > 0) return Complete();
                currentStage = "disabled_startup";
                await VerifyDisabledAsync(request, root, manifests, environment, stages, cancellationToken);
                return Complete();
            }

            var launch = new RuntimeScenarioLaunchOptions(request.Launch.Command, request.Launch.Arguments,
                request.Launch.WorkingDirectory, request.Launch.DisplayName, manifests, Path.Combine(root, "launch"), environment,
                request.Launch.TimeoutMs, argumentList: request.Launch.ArgumentList, noBuild: true);
            var steps = new List<SemanticWorkflowStep>
            {
                new(SemanticWorkflowActions.Inspect, "inspection", request.InspectionTarget ?? new SemanticWorkflowSelector(maxDepth: 0, visible: true)),
                new(SemanticWorkflowActions.Screenshot, "screenshot", screenshotPath: Path.Combine(root, "window.png"))
            };
            if (request.SafeInputTarget is not null) steps.Add(new(SemanticWorkflowActions.Focus, "input_probe", request.SafeInputTarget));
            currentStage = "launch";
            var result = await new RuntimeScenarioRunner().RunAsync(new LocalBridgeClient(manifests),
                new RuntimeScenarioRequest(steps, launch: launch, outputDirectory: root, terminateLaunchedProcess: true, workflowTimeoutMs: 30000, captureVisualTree: true), cancellationToken);
            if (!result.Success)
            {
                stages.Add(new("launch", "failed", result.Error!.Message, "Inspect the selected executable and launch environment."));
                return Complete();
            }

            scenario = result.Value!;
            var diagnostic = scenario.Diagnostics.FirstOrDefault();
            var logs = new[] { scenario.Launch?.StdoutPath ?? scenario.Readiness?.StdoutPath, scenario.Launch?.StderrPath ?? scenario.Readiness?.StderrPath }
                .Where(path => path is not null).Select(path => path!).ToArray();
            var hostError = logs.Select(ReadLogTail).FirstOrDefault(log => log.Contains("AVASCOPE_", StringComparison.Ordinal) &&
                (log.Contains("INCOMPATIBLE", StringComparison.Ordinal) || log.Contains("INVALID", StringComparison.Ordinal) ||
                 log.Contains("BOOTSTRAP_MISSING", StringComparison.Ordinal) || log.Contains("ACTIVATION_FAILED", StringComparison.Ordinal)));
            var started = scenario.Launch is not null || scenario.Readiness?.ProcessId is not null;
            var activated = scenario.SessionId is not null;
            stages.Add(new("launch", started ? "passed" : "failed", started ? "The selected direct child process started." : diagnostic?.Message ?? "Process launch failed.",
                started ? null : "Verify launch.command, argumentList and workingDirectory.", logs));
            stages.Add(new("activation", activated ? "passed" : hostError is not null ? "failed" : "unobserved",
                activated ? "The explicit host activation produced a live bridge." : hostError ?? "No activation result was observed; see host logs.",
                activated ? null : "Check the compile-time flag, loader diagnostic, runtime/Avalonia compatibility and UI-thread bootstrap call.", logs));
            stages.Add(new("discovery", activated ? "passed" : started && hostError is null ? "failed" : "skipped",
                activated ? "Session discovered and attached by the exact launched process id in the private manifest directory." : diagnostic?.Message ?? "No owned session appeared.",
                activated ? null : "Ensure the host honors AVASCOPE_BRIDGE_MANIFEST_DIR and explicitly starts the bridge."));
            var controlFailed = scenario.FailureStage == "session_control";
            if (controlFailed) stages.Add(new("session_control", "failed", diagnostic?.Message ?? "The bridge did not grant session control.",
                "Check the selected provider's session-control capability and any existing owner before retrying."));
            stages.Add(new("readiness", scenario.TopLevels.Count > 0 ? "passed" : controlFailed ? "skipped" : activated ? "failed" : "skipped",
                scenario.TopLevels.Count > 0 ? $"{scenario.TopLevels.Count} top-level(s) registered." : controlFailed ? "Session control prevented the window readiness check." : "No registered top-level became ready.",
                scenario.TopLevels.Count > 0 || controlFailed ? null : "Assign/show the application window and enable automatic bootstrap registration."));
            var treePath = Path.Combine(root, "runtime-tree.json");
            stages.Add(new("tree", File.Exists(treePath) ? "passed" : scenario.TopLevels.Count > 0 ? "failed" : "skipped",
                File.Exists(treePath) ? "Bounded visual tree captured." : "Visual tree capture was not completed.",
                scenario.TopLevels.Count > 0 && !File.Exists(treePath) ? scenario.Diagnostics.LastOrDefault()?.Message : null, File.Exists(treePath) ? [treePath] : null));
            foreach (var step in steps)
            {
                var executed = scenario.Workflow?.Steps.FirstOrDefault(item => item.StepId == step.Id);
                stages.Add(new(step.Id, executed?.Status ?? "skipped", executed?.Message ?? "An earlier stage prevented execution.",
                    executed?.Status == "failed" ? $"Inspect {step.Id} diagnostics, target selection and backend support." : null,
                    executed?.Screenshot is null ? null : [executed.Screenshot.FilePath]));
            }

            if (request.SafeInputTarget is null) stages.Add(new("input_probe", "skipped", "Host did not declare a non-destructive focus target."));
            currentStage = "cleanup";
            var processId = scenario.Launch?.ProcessId ?? scenario.Readiness?.ProcessId;
            if (processId.HasValue) RecoverStoppedResources(processId.Value, manifests, environment);
            var remnants = Directory.EnumerateFiles(manifests, "*.json").Any();
            var listeners = processId.HasValue && OwnedListeners(processId.Value, environment).Length > 0;
            stages.Add(new("cleanup", scenario.FailureStage == RuntimeScenarioFailureStages.Cleanup || remnants || listeners ? "failed" : "passed",
                remnants || listeners ? "Owned manifest/listener resources remain after shutdown." : "Owned process cleanup completed; no owned session manifest/listener remains.",
                remnants || listeners ? "Review bootstrap disposal and launch cleanup diagnostics; preserve evidence for recovery." : null));
            if (scenario.Status != "passed" && !stages.Any(stage => stage.Status is "failed" or "cancelled"))
                stages.Add(new(scenario.FailureStage ?? "workflow", "failed", diagnostic?.Message ?? "The scenario failed; inspect its structured diagnostics and timeline."));
            return Complete();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception or OperationCanceledException or JsonException)
        {
            stages.Add(new(currentStage, exception is OperationCanceledException ? "cancelled" : "failed", exception.Message,
                "Inspect this stage's evidence and correct the selected local configuration before retrying."));
            return Complete();
        }

        CoreResult<BridgeIntegrationVerificationResponse> Complete()
        {
            var failed = stages.FirstOrDefault(stage => stage.Status is not ("passed" or "skipped" or "unobserved"));
            var response = new BridgeIntegrationVerificationResponse(failed is null ? "passed" : "failed", failed?.Name,
                request.BootstrapDisabled ? "disabled" : "enabled", Path.Combine(root, "integration-report.json"), stages, scenario);
            try
            {
                Directory.CreateDirectory(root);
                File.WriteAllText(response.ReportPath, JsonSerializer.Serialize(response, JsonOptions));
                return CoreResult<BridgeIntegrationVerificationResponse>.Ok(response);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                return CoreResult<BridgeIntegrationVerificationResponse>.Fail(new("integration_report_failed", exception.Message));
            }
        }
    }

    private static async Task VerifyDisabledAsync(BridgeIntegrationVerificationRequest request, string root, string manifests,
        IReadOnlyDictionary<string, string> environment, List<IntegrationVerificationStage> stages, CancellationToken cancellationToken)
    {
        var info = new ProcessStartInfo(request.Launch.Command!)
        {
            UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true,
            WorkingDirectory = request.Launch.WorkingDirectory ?? Environment.CurrentDirectory
        };
        if (request.Launch.Arguments is not null) info.Arguments = request.Launch.Arguments;
        foreach (var argument in request.Launch.ArgumentList) info.ArgumentList.Add(argument);
        foreach (var pair in environment) info.Environment[pair.Key] = pair.Value;
        info.Environment[BridgeSessionManifest.DirectoryEnvironmentVariable] = manifests;
        using var process = new Process { StartInfo = info };
        var stdoutPath = Path.Combine(root, "disabled-stdout.log");
        var stderrPath = Path.Combine(root, "disabled-stderr.log");
        if (!process.Start()) throw new InvalidOperationException("The disabled host could not start.");
        var stdout = CaptureLogAsync(process.StandardOutput, stdoutPath);
        var stderr = CaptureLogAsync(process.StandardError, stderrPath);
        stages.Add(new("launch", "passed", $"Started owned disabled host PID {process.Id}.", EvidencePaths: [stdoutPath, stderrPath]));
        var watch = Stopwatch.StartNew();
        var leaked = false;
        try
        {
            while (watch.ElapsedMilliseconds < request.ObservationMs)
            {
                cancellationToken.ThrowIfCancellationRequested();
                leaked |= Directory.EnumerateFiles(manifests, "*.json").Any() || OwnedListeners(process.Id, environment).Length > 0;
                if (leaked || process.HasExited) break;
                await Task.Delay(50, cancellationToken);
            }

            var observed = watch.ElapsedMilliseconds >= request.ObservationMs && !process.HasExited;
            stages.Add(new("disabled_startup", !leaked && observed ? "passed" : "failed",
                leaked ? "Disabled startup created an AvaScope manifest or PID-scoped listener." : observed
                    ? $"No manifest or PID-scoped listener observed during {request.ObservationMs} ms (50 ms polling)."
                    : "Host exited before the observation interval completed; disabled activation could not be verified.",
                leaked ? "Remove the bootstrap/loader call from the production compilation." : observed ? null : "Keep the disabled fixture alive for the full observation interval."));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception or OperationCanceledException or JsonException)
        {
            // Record the observation outcome before cleanup, which can fail independently.
            stages.Add(new("disabled_startup", exception is OperationCanceledException && cancellationToken.IsCancellationRequested ? "cancelled" : "failed", exception.Message));
        }
        finally
        {
            await CleanupDisabledHostAsync(process, stdout, stderr, manifests, environment, stages);
        }
    }

    internal static async Task CleanupDisabledHostAsync(Process process, Task stdout, Task stderr, string manifests,
        IReadOnlyDictionary<string, string> environment, List<IntegrationVerificationStage> stages)
    {
        var watch = Stopwatch.StartNew();
        var phase = "terminate";
        var failures = new List<object>();
        string? failureMessage = null;
        var recovered = false;
        var captures = Task.WhenAll(stdout, stderr);
        // A failed exit/drain wait must not leave a later capture fault unobserved.
        _ = captures.ContinueWith(static task => { _ = task.Exception; }, CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
        using var cleanupTimeout = new CancellationTokenSource();
        try
        {
            if (!process.HasExited) process.Kill(entireProcessTree: true);
            // Preserve the existing shared five-second exit/output budget after termination.
            cleanupTimeout.CancelAfter(TimeSpan.FromSeconds(5));
            phase = "process_exit";
            await process.WaitForExitAsync(cleanupTimeout.Token);
            phase = "output_drain";
            await captures.WaitAsync(cleanupTimeout.Token);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception or OperationCanceledException)
        {
            Failed(exception);
        }

        // Output failure does not prevent recovery once this exact owned process has exited.
        phase = "resource_recovery";
        try
        {
            if (!process.HasExited) throw new InvalidOperationException("Owned process exit was not confirmed; resources were preserved.");
            recovered = RecoverStoppedResources(process.Id, manifests, environment) && OwnedListeners(process.Id, environment).Length == 0;
            if (!recovered) throw new IOException("Owned resource recovery could not be confirmed after shutdown.");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception or JsonException)
        {
            Failed(exception);
        }

        var evidencePath = Path.Combine(Path.GetDirectoryName(manifests)!, "disabled-cleanup.json");
        var evidenceWritten = false;
        phase = "evidence";
        try
        {
            File.WriteAllText(evidencePath, JsonSerializer.Serialize(new
            {
                processId = process.Id, observedAt = DateTimeOffset.UtcNow, elapsedMs = watch.Elapsed.TotalMilliseconds,
                cleanupTimeoutMs = 5000, processExited = process.HasExited, exitCode = process.HasExited ? (int?)process.ExitCode : null,
                stdoutStatus = stdout.Status.ToString(), stderrStatus = stderr.Status.ToString(),
                stdoutError = CaptureError(stdout), stderrError = CaptureError(stderr),
                resourceRecovery = recovered ? "passed" : "failed", failures
            }, JsonOptions));
            evidenceWritten = true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            Failed(exception);
        }
        stages.Add(new("cleanup", failureMessage is null ? "passed" : "failed",
            failureMessage ?? "Stopped only the owned disabled host and recovered its isolated resources.",
            failureMessage is null ? null : "Inspect the retained cleanup phase and process/output state before retrying.",
            evidenceWritten ? [evidencePath] : null));

        void Failed(Exception exception)
        {
            var message = exception.Message[..Math.Min(exception.Message.Length, 1024)];
            failures.Add(new { phase, exceptionType = exception.GetType().Name, message,
                timedOut = exception is OperationCanceledException && cleanupTimeout.IsCancellationRequested,
                elapsedMs = watch.Elapsed.TotalMilliseconds });
            failureMessage ??= $"Disabled host cleanup failed during {phase}: {message}";
        }

        static object? CaptureError(Task task)
        {
            var exception = task.Exception?.GetBaseException();
            return exception is null ? null : new { exceptionType = exception.GetType().Name,
                message = exception.Message[..Math.Min(exception.Message.Length, 1024)] };
        }
    }

    private static bool RecoverStoppedResources(int processId, string manifests, IReadOnlyDictionary<string, string> environment)
    {
        if (LaunchOwnershipStore.TryGetProcessIdentity(processId, out var process, out _))
        {
            process.Dispose();
            return false;
        }

        // This directory was allocated for one owned run, and recovery requires the exact PID to have exited.
        foreach (var path in Directory.EnumerateFiles(manifests, "*.json").Take(32))
        {
            if (new FileInfo(path).Length > 1024 * 1024) throw new IOException("Owned manifest exceeds the recovery read limit.");
            var manifest = JsonSerializer.Deserialize<BridgeSessionManifest>(File.ReadAllText(path), JsonOptions);
            if (manifest?.ProcessId == processId) File.Delete(path);
        }
        if (!OperatingSystem.IsWindows())
            foreach (var path in OwnedListeners(processId, environment)) File.Delete(path);
        return true;
    }

    private static string[] FindProductionAssemblies(string directory)
    {
        if (!Directory.Exists(directory)) throw new DirectoryNotFoundException("The selected production output directory does not exist.");
        var pending = new Queue<(DirectoryInfo Directory, int Depth)>();
        pending.Enqueue((new DirectoryInfo(directory), 0));
        var assemblies = new List<string>();
        var entries = 0;
        while (pending.TryDequeue(out var current))
        {
            if (current.Depth > 16 || (current.Directory.Attributes & FileAttributes.ReparsePoint) != 0)
                throw new IOException("Production output cannot be fully verified: linked directory or depth exceeds 16.");
            foreach (var entry in current.Directory.EnumerateFileSystemInfos())
            {
                if (++entries > 10000 || (entry.Attributes & FileAttributes.ReparsePoint) != 0)
                    throw new IOException("Production output cannot be fully verified: linked entry or more than 10000 entries.");
                if (entry is DirectoryInfo child) pending.Enqueue((child, current.Depth + 1));
                else if (entry.Name.StartsWith("AvaScope.", StringComparison.OrdinalIgnoreCase) &&
                    (entry.Extension.Equals(".dll", StringComparison.OrdinalIgnoreCase) || entry.Extension.Equals(".exe", StringComparison.OrdinalIgnoreCase)))
                    assemblies.Add(entry.FullName);
            }
        }
        return assemblies.ToArray();
    }

    private static string[] OwnedListeners(int processId, IReadOnlyDictionary<string, string> environment)
    {
        // .NET 10 named pipes use this public filesystem naming on Unix; no connection or process attach is performed.
        var directory = OperatingSystem.IsWindows() ? @"\\.\pipe\" : environment.GetValueOrDefault("TMPDIR", Path.GetTempPath());
        var pattern = OperatingSystem.IsWindows() ? $"avs-{processId}-*" : $"CoreFxPipe_avs-{processId}-*";
        return Directory.EnumerateFiles(directory, pattern).Take(32).ToArray();
    }

    private static string ReadLogTail(string path)
    {
        if (!File.Exists(path)) return "";
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        stream.Seek(Math.Max(0, stream.Length - 8192), SeekOrigin.Begin);
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    private static async Task CaptureLogAsync(StreamReader reader, string path)
    {
        await using var file = new StreamWriter(path);
        var buffer = new char[4096];
        var written = 0;
        while (await reader.ReadAsync(buffer) is var count && count > 0)
        {
            var remaining = Math.Max(0, 1024 * 1024 - written);
            await file.WriteAsync(buffer.AsMemory(0, Math.Min(count, remaining)));
            written += Math.Min(count, remaining);
        }
    }
}
