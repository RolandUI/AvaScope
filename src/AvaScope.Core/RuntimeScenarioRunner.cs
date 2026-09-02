using System.Globalization;
using System.Text;
using AvaScope.Protocol;

namespace AvaScope.Core;

public sealed class RuntimeScenarioRunner
{
    private const string Passed = "passed";
    private const string Failed = "failed";
    private const string Cancelled = "cancelled";

    public async Task<CoreResult<RuntimeScenarioResponse>> RunAsync(
        LocalBridgeClient bridgeClient,
        RuntimeScenarioRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(bridgeClient);
        ArgumentNullException.ThrowIfNull(request);

        var startedAt = DateTimeOffset.UtcNow;
        var validationSessionId = request.SessionId ?? new SessionId($"scenario-validation-{request.RequestId}");
        var validationTopLevelId = request.TopLevelId
            ?? (request.TopLevelAliases.Count == 0 ? "topLevel:scenario-validation" : null);
        var validationRequest = new SemanticWorkflowRequest(
            validationSessionId,
            validationTopLevelId,
            request.Steps,
            request.RequestId,
            outputDirectory: request.OutputDirectory ?? Path.Combine(Path.GetTempPath(), "AvaScope", "scenario-validation", request.RequestId),
            captureAfterEachStep: request.CaptureAfterEachStep,
            allowDestructive: request.AllowDestructive,
            maxDepth: request.MaxDepth,
            topLevelAliases: request.TopLevelAliases,
            variables: request.Variables,
            fragments: request.Fragments,
            validateOnly: true,
            timeoutMs: request.WorkflowTimeoutMs,
            evidence: request.Evidence);
        var validation = SemanticWorkflowCompiler.Compile(validationRequest);
        if (!validation.Plan.Valid)
        {
            var completedAt = DateTimeOffset.UtcNow;
            var validationWorkflow = new SemanticWorkflowResponse(
                request.RequestId,
                validationSessionId,
                validationTopLevelId,
                "validation_failed",
                startedAt,
                completedAt,
                [],
                diagnostics: validation.Plan.Diagnostics,
                metadata: new Dictionary<string, string> { ["validationOnly"] = "true" },
                plan: validation.Plan);
            return CoreResult<RuntimeScenarioResponse>.Ok(new RuntimeScenarioResponse(
                request.RequestId,
                Failed,
                startedAt,
                completedAt,
                workflow: validationWorkflow,
                diagnostics: validation.Plan.Diagnostics,
                metadata: new Dictionary<string, string>
                {
                    ["scenarioMode"] = "validation",
                    ["dispatchPerformed"] = "false"
                },
                failureStage: RuntimeScenarioFailureStages.Validation));
        }

        var diagnostics = new List<ProtocolError>();
        var outputDirectory = ResolveOutputDirectory(request);
        Directory.CreateDirectory(outputDirectory);

        var isolation = PrepareIsolation(request, outputDirectory);
        var timelinePath = request.TimelinePath ?? Path.Combine(outputDirectory, "scenario-timeline.md");
        SessionId? sessionId = null;
        string? topLevelId = request.TopLevelId;
        LaunchAppResponse? launch = null;
        AttachToAppResponse? attach = null;
        RuntimeScenarioBuildResult? build = null;
        RuntimeScenarioReadinessEvidence? readiness = null;
        IReadOnlyList<TopLevelSummary> topLevels = [];
        CloseSessionResponse? cleanup = null;
        NativePickerResponse? preparedPickerResult = null;
        LocalBridgeClient workflowClient = bridgeClient;
        var scenarioMode = request.Launch is not null
            ? "launch"
            : request.Attach is not null ? "attach" : "session";
        var effectiveBuild = request.Build
            ?? (request.Launch?.ProjectPath is not null && !request.Launch.NoBuild
                ? new RuntimeScenarioBuildOptions(
                    request.Launch.ProjectPath,
                    request.Launch.Configuration,
                    request.Launch.Framework)
                : null);
        var currentStage = effectiveBuild is null
            ? request.Launch is not null
                ? RuntimeScenarioFailureStages.Launch
                : RuntimeScenarioFailureStages.Attach
            : RuntimeScenarioFailureStages.Build;
        DateTimeOffset? launchReadinessStartedAt = null;

        async Task<CoreResult<RuntimeScenarioResponse>> CompleteAsync(
            string status,
            string? failureStage,
            SemanticWorkflowResponse? workflow = null)
        {
            if (launch is not null && request.TerminateLaunchedProcess && sessionId is not null)
            {
                var cleanupResult = await workflowClient.CloseSessionAsync(
                    sessionId,
                    CancellationToken.None,
                    terminateLaunchedProcess: true);
                if (!cleanupResult.Success)
                {
                    diagnostics.Insert(0, ToProtocolError(cleanupResult.Error!));
                    status = Failed;
                    failureStage = RuntimeScenarioFailureStages.Cleanup;
                }
                else
                {
                    var cleanupValue = cleanupResult.Value!;
                    cleanup = cleanupValue;
                    if (cleanupValue.Outcome is not (CloseSessionOutcomes.Terminated or CloseSessionOutcomes.AlreadyExited))
                    {
                        diagnostics.Insert(0, new ProtocolError(
                            "runtime_scenario_cleanup_failed",
                            cleanupValue.TerminationMessage ?? "The owned scenario process could not be terminated.",
                            new Dictionary<string, string>
                            {
                                ["failureStage"] = RuntimeScenarioFailureStages.Cleanup,
                                ["outcome"] = cleanupValue.Outcome,
                                ["processId"] = cleanupValue.ProcessId.ToString(CultureInfo.InvariantCulture)
                            }));
                        status = Failed;
                        failureStage = RuntimeScenarioFailureStages.Cleanup;
                    }
                }
            }

            return CoreResult<RuntimeScenarioResponse>.Ok(CreateResponse(
                request,
                status,
                startedAt,
                sessionId,
                topLevelId,
                launch,
                attach,
                workflow,
                isolation,
                timelinePath,
                diagnostics,
                outputDirectory,
                scenarioMode,
                preparedPickerResult,
                build,
                readiness,
                topLevels,
                cleanup,
                failureStage));
        }

        if (effectiveBuild is not null)
        {
            build = await new RuntimeScenarioBuilder().BuildAsync(
                effectiveBuild,
                outputDirectory,
                cancellationToken);
            if (build.Diagnostic is not null)
            {
                diagnostics.Add(build.Diagnostic);
            }

            if (!string.Equals(build.Status, RuntimeScenarioLifecycleStatuses.Passed, StringComparison.Ordinal))
            {
                return await CompleteAsync(
                    string.Equals(build.Status, RuntimeScenarioLifecycleStatuses.Cancelled, StringComparison.Ordinal)
                        ? Cancelled
                        : Failed,
                    RuntimeScenarioFailureStages.Build);
            }
        }

        try
        {
            if (request.Launch is not null)
            {
                scenarioMode = "launch";
                currentStage = RuntimeScenarioFailureStages.Launch;
                launchReadinessStartedAt = DateTimeOffset.UtcNow;
                var launchResult = await LaunchAsync(
                    bridgeClient,
                    request,
                    outputDirectory,
                    isolation,
                    cancellationToken);
                if (!launchResult.Success)
                {
                    diagnostics.Add(ToProtocolError(launchResult.Error!));
                    var failureStage = launchResult.Error!.Details?.GetValueOrDefault("failureStage")
                        ?? RuntimeScenarioFailureStages.Launch;
                    readiness = CreateReadinessFailure(
                        launchResult.Error,
                        launchReadinessStartedAt.Value,
                        failureStage);
                    var cancelled = string.Equals(
                        launchResult.Error.Details?.GetValueOrDefault("cancelled"),
                        "true",
                        StringComparison.OrdinalIgnoreCase);
                    return await CompleteAsync(cancelled ? Cancelled : Failed, failureStage);
                }

                launch = launchResult.Value!;
                sessionId = launch.Session.SessionId;
                topLevelId ??= launch.TopLevelId;
                workflowClient = new LocalBridgeClient(request.Launch.ManifestDirectory ?? bridgeClient.ManifestDirectory);
                attach = new AttachToAppResponse(
                    launch.Session,
                    launch.ProcessId,
                    launch.ProcessName,
                    launch.ManifestPath);
            }
            else if (request.Attach is not null)
            {
                scenarioMode = "attach";
                currentStage = RuntimeScenarioFailureStages.Attach;
                var attachResult = request.Attach.Latest
                    ? await bridgeClient.AttachLatestToAppAsync(
                        processId: request.Attach.ProcessId,
                        processName: request.Attach.ProcessName,
                        cancellationToken: cancellationToken)
                    : await bridgeClient.AttachToAppAsync(
                        request.Attach.ProcessId,
                        request.Attach.SessionId,
                        request.Attach.ProcessName,
                        request.Attach.ManifestPath,
                        cancellationToken);
                if (!attachResult.Success)
                {
                    diagnostics.Add(ToProtocolError(attachResult.Error!));
                    return await CompleteAsync(Failed, RuntimeScenarioFailureStages.Attach);
                }

                attach = attachResult.Value!;
                sessionId = attach.Session.SessionId;
            }
            else
            {
                currentStage = RuntimeScenarioFailureStages.Attach;
                var attachResult = await bridgeClient.AttachToAppAsync(
                    sessionId: request.SessionId,
                    cancellationToken: cancellationToken);
                if (!attachResult.Success)
                {
                    diagnostics.Add(ToProtocolError(attachResult.Error!));
                    return await CompleteAsync(Failed, RuntimeScenarioFailureStages.Attach);
                }

                attach = attachResult.Value!;
                sessionId = attach.Session.SessionId;
            }

            if (sessionId is null)
            {
                diagnostics.Add(new ProtocolError(
                    "runtime_scenario_session_not_resolved",
                    "Scenario could not resolve an active bridge session."));
                return await CompleteAsync(Failed, RuntimeScenarioFailureStages.Attach);
            }

            if (request.PickerResult is not null)
            {
                var picker = request.PickerResult;
                var correlationId = picker.CorrelationId ?? request.RequestId;
                var prepareResult = workflowClient.NativePicker(
                    sessionId,
                    NativePickerOperations.PredefineResult,
                    picker.Path,
                    picker.Result,
                    correlationId,
                    picker.TtlMs);
                if (!prepareResult.Success)
                {
                    diagnostics.Add(ToProtocolError(prepareResult.Error!));
                    return await CompleteAsync(Failed, RuntimeScenarioFailureStages.Workflow);
                }

                preparedPickerResult = prepareResult.Value;
            }

            currentStage = RuntimeScenarioFailureStages.TopLevels;
            var topLevelCheckCount = 0;
            if (launch is not null || string.IsNullOrWhiteSpace(topLevelId))
            {
                var topLevelResult = await ResolveTopLevelsAsync(
                    workflowClient,
                    sessionId,
                    request.Launch is null ? TimeSpan.Zero : TimeSpan.FromMilliseconds(request.Launch.TimeoutMs),
                    cancellationToken);
                topLevelCheckCount = topLevelResult.CheckCount;
                if (!topLevelResult.Result.Success)
                {
                    diagnostics.Add(ToProtocolError(topLevelResult.Result.Error!));
                    return await CompleteAsync(Failed, RuntimeScenarioFailureStages.TopLevels);
                }

                topLevels = topLevelResult.Result.Value!.TopLevels;
                topLevelId ??= topLevels.FirstOrDefault()?.Id;
            }

            if (launch is not null)
            {
                readiness = new RuntimeScenarioReadinessEvidence(
                    RuntimeScenarioLifecycleStatuses.Ready,
                    launchReadinessStartedAt ?? launch.StartedAt,
                    DateTimeOffset.UtcNow,
                    topLevelCheckCount,
                    launch.ProcessId,
                    launch.Session.SessionId,
                    launch.ManifestPath,
                    launch.StdoutPath,
                    launch.StderrPath,
                    topLevels,
                    metadata: new Dictionary<string, string>
                    {
                        ["bridgeHealth"] = "passed",
                        ["registeredTopLevelCount"] = topLevels.Count.ToString(CultureInfo.InvariantCulture)
                    });
            }

            if (string.IsNullOrWhiteSpace(topLevelId))
            {
                diagnostics.Add(new ProtocolError(
                    "runtime_scenario_top_level_not_resolved",
                    "Scenario could not resolve a top-level id for the selected bridge session.",
                    new Dictionary<string, string>
                    {
                        ["sessionId"] = sessionId.Value,
                        ["failureStage"] = RuntimeScenarioFailureStages.TopLevels,
                        ["readinessChecks"] = topLevelCheckCount.ToString(CultureInfo.InvariantCulture)
                    }));
                return await CompleteAsync(Failed, RuntimeScenarioFailureStages.TopLevels);
            }

            currentStage = RuntimeScenarioFailureStages.Workflow;
            var workflowRequest = new SemanticWorkflowRequest(
                sessionId,
                topLevelId,
                request.Steps,
                requestId: request.RequestId,
                outputDirectory: outputDirectory,
                captureAfterEachStep: request.CaptureAfterEachStep,
                allowDestructive: request.AllowDestructive,
                isolatedStateDirectory: isolation.Applied ? isolation.Directory : null,
                maxDepth: request.MaxDepth,
                topLevelAliases: request.TopLevelAliases,
                variables: request.Variables,
                fragments: request.Fragments,
                timeoutMs: request.WorkflowTimeoutMs,
                evidence: request.Evidence);
            var workflow = await new SemanticWorkflowRunner().RunAsync(workflowClient, workflowRequest, cancellationToken);
            if (!workflow.Success)
            {
                diagnostics.Add(ToProtocolError(workflow.Error!));
                return await CompleteAsync(Failed, RuntimeScenarioFailureStages.Workflow);
            }

            diagnostics.AddRange(workflow.Value!.Diagnostics);
            var status = string.Equals(workflow.Value.Status, Passed, StringComparison.Ordinal)
                ? Passed
                : Failed;
            return await CompleteAsync(
                status,
                string.Equals(status, Passed, StringComparison.Ordinal)
                    ? null
                    : RuntimeScenarioFailureStages.Workflow,
                workflow.Value);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            diagnostics.Add(new ProtocolError(
                "runtime_scenario_cancelled",
                $"The scenario was cancelled during the {currentStage} stage.",
                new Dictionary<string, string>
                {
                    ["failureStage"] = currentStage
                }));
            return await CompleteAsync(Cancelled, currentStage);
        }
    }

    private async Task<CoreResult<LaunchAppResponse>> LaunchAsync(
        LocalBridgeClient bridgeClient,
        RuntimeScenarioRequest request,
        string outputDirectory,
        ScenarioIsolation isolation,
        CancellationToken cancellationToken)
    {
        var launch = request.Launch!;
        var environment = new Dictionary<string, string>(launch.Environment, StringComparer.Ordinal);
        if (isolation.Applied)
        {
            foreach (var pair in isolation.Environment)
            {
                environment[pair.Key] = pair.Value;
            }
        }

        environment["AVASCOPE_SCENARIO_ID"] = request.RequestId;

        var command = launch.Command ?? "dotnet";
        var workingDirectory = launch.WorkingDirectory;
        IReadOnlyList<string>? argumentList = launch.ArgumentList.Count == 0
            ? null
            : launch.ArgumentList;
        if (launch.ProjectPath is not null)
        {
            var framework = launch.Framework ?? request.Build?.Framework ?? "net10.0";
            var runtimeIdentifier = request.Build?.RuntimeIdentifier;
            var targetDirectory = Path.Combine(
                Path.GetDirectoryName(launch.ProjectPath)!,
                "bin",
                launch.Configuration,
                framework);
            if (!string.IsNullOrWhiteSpace(runtimeIdentifier))
            {
                targetDirectory = Path.Combine(targetDirectory, runtimeIdentifier);
            }

            var targetPath = Path.Combine(
                targetDirectory,
                $"{Path.GetFileNameWithoutExtension(launch.ProjectPath)}.dll");
            var projectArguments = new List<string>
            {
                targetPath
            };
            if (launch.ArgumentList.Count > 0)
            {
                projectArguments.AddRange(launch.ArgumentList);
            }

            argumentList = projectArguments;
            workingDirectory ??= Path.GetDirectoryName(launch.ProjectPath);
        }

        return await new BridgeAppLauncher().LaunchAsync(
            command,
            launch.Arguments,
            workingDirectory,
            launch.DisplayName,
            launch.ManifestDirectory ?? bridgeClient.ManifestDirectory,
            launch.OutputDirectory ?? Path.Combine(outputDirectory, "launch"),
            environment,
            TimeSpan.FromMilliseconds(launch.TimeoutMs),
            cancellationToken,
            argumentList,
            directProcess: true,
            terminateOnFailure: true,
            captureOutputUntilExit: true);
    }

    private static async Task<(CoreResult<ListTopLevelsResponse> Result, int CheckCount)> ResolveTopLevelsAsync(
        LocalBridgeClient client,
        SessionId sessionId,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        var stopAt = DateTimeOffset.UtcNow + timeout;
        var checkCount = 0;
        while (true)
        {
            checkCount++;
            var result = await client.ListTopLevelsAsync(sessionId, cancellationToken);
            if (!result.Success || result.Value!.TopLevels.Count > 0 || DateTimeOffset.UtcNow >= stopAt)
            {
                return (result, checkCount);
            }

            await Task.Delay(TimeSpan.FromMilliseconds(100), cancellationToken);
        }
    }

    private static RuntimeScenarioReadinessEvidence CreateReadinessFailure(
        CoreError error,
        DateTimeOffset startedAt,
        string failureStage)
    {
        var details = error.Details ?? new Dictionary<string, string>();
        _ = int.TryParse(
            details.GetValueOrDefault("readinessChecks"),
            NumberStyles.Integer,
            CultureInfo.InvariantCulture,
            out var checkCount);
        _ = int.TryParse(
            details.GetValueOrDefault("processId"),
            NumberStyles.Integer,
            CultureInfo.InvariantCulture,
            out var processId);
        var status = string.Equals(details.GetValueOrDefault("cancelled"), "true", StringComparison.OrdinalIgnoreCase)
            ? RuntimeScenarioLifecycleStatuses.Cancelled
            : string.Equals(details.GetValueOrDefault("timedOut"), "true", StringComparison.OrdinalIgnoreCase)
                ? RuntimeScenarioLifecycleStatuses.TimedOut
                : details.ContainsKey("exitCode")
                    ? RuntimeScenarioLifecycleStatuses.ProcessExited
                    : RuntimeScenarioLifecycleStatuses.Failed;
        return new RuntimeScenarioReadinessEvidence(
            status,
            startedAt,
            DateTimeOffset.UtcNow,
            checkCount,
            processId > 0 ? processId : null,
            manifestPath: details.GetValueOrDefault("manifestPath"),
            stdoutPath: details.GetValueOrDefault("stdoutPath"),
            stderrPath: details.GetValueOrDefault("stderrPath"),
            diagnostic: ToProtocolError(error),
            metadata: new Dictionary<string, string>
            {
                ["failureStage"] = failureStage,
                ["bridgeHealth"] = "not_ready"
            });
    }

    private static RuntimeScenarioResponse CreateResponse(
        RuntimeScenarioRequest request,
        string status,
        DateTimeOffset startedAt,
        SessionId? sessionId,
        string? topLevelId,
        LaunchAppResponse? launch,
        AttachToAppResponse? attach,
        SemanticWorkflowResponse? workflow,
        ScenarioIsolation isolation,
        string timelinePath,
        IReadOnlyList<ProtocolError> diagnostics,
        string outputDirectory,
        string scenarioMode,
        NativePickerResponse? preparedPickerResult = null,
        RuntimeScenarioBuildResult? build = null,
        RuntimeScenarioReadinessEvidence? readiness = null,
        IReadOnlyList<TopLevelSummary>? topLevels = null,
        CloseSessionResponse? cleanup = null,
        string? failureStage = null)
    {
        var completedAt = DateTimeOffset.UtcNow;
        var metadata = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["scenarioMode"] = scenarioMode,
            ["outputDirectory"] = outputDirectory,
            ["timelineFormat"] = "markdown",
            ["requestedSteps"] = request.Steps.Count.ToString(CultureInfo.InvariantCulture),
            ["terminateLaunchedProcessRequested"] = request.TerminateLaunchedProcess.ToString().ToLowerInvariant()
        };

        if (!string.IsNullOrWhiteSpace(failureStage))
        {
            metadata["failureStage"] = failureStage;
        }

        if (request.Launch is not null)
        {
            metadata["launchEnvironmentVariableNames"] = string.Join(',', request.Launch.Environment.Keys.Order(StringComparer.Ordinal));
            metadata["launchArgumentCount"] = (request.Launch.ArgumentList.Count > 0
                ? request.Launch.ArgumentList.Count
                : string.IsNullOrWhiteSpace(request.Launch.Arguments) ? 0 : 1).ToString(CultureInfo.InvariantCulture);
        }

        if (isolation.Applied)
        {
            metadata["isolatedEnvironmentVariables"] = string.Join(',', isolation.Environment.Keys.Order(StringComparer.Ordinal));
        }

        var response = new RuntimeScenarioResponse(
            request.RequestId,
            status,
            startedAt,
            completedAt,
            sessionId,
            topLevelId,
            launch,
            attach,
            workflow,
            isolation.Status,
            isolation.Directory,
            timelinePath,
            diagnostics,
            metadata,
            preparedPickerResult,
            build: build,
            readiness: readiness,
            topLevels: topLevels,
            cleanup: cleanup,
            failureStage: failureStage);

        WriteTimeline(response);
        return response;
    }

    private static string ResolveOutputDirectory(RuntimeScenarioRequest request)
    {
        return request.OutputDirectory
            ?? Path.Combine(Path.GetTempPath(), "AvaScope", "scenarios", request.RequestId);
    }

    private static ScenarioIsolation PrepareIsolation(RuntimeScenarioRequest request, string outputDirectory)
    {
        if (request.Launch is null)
        {
            return new ScenarioIsolation(
                string.IsNullOrWhiteSpace(request.IsolatedStateDirectory)
                    ? "not_applicable_existing_session"
                    : "not_applied_existing_session",
                request.IsolatedStateDirectory,
                Applied: false,
                Environment: new Dictionary<string, string>());
        }

        if (!request.IsolateState && string.IsNullOrWhiteSpace(request.IsolatedStateDirectory))
        {
            return new ScenarioIsolation(
                "disabled",
                null,
                Applied: false,
                Environment: new Dictionary<string, string>());
        }

        var stateDirectory = request.IsolatedStateDirectory
            ?? Path.Combine(outputDirectory, "isolated-state");
        var environment = CreateIsolatedStateEnvironment(stateDirectory);
        foreach (var path in environment.Values.Where(static value => value.Contains(Path.DirectorySeparatorChar, StringComparison.Ordinal)))
        {
            Directory.CreateDirectory(path);
        }

        return new ScenarioIsolation(
            "applied_environment",
            stateDirectory,
            Applied: true,
            environment);
    }

    private static IReadOnlyDictionary<string, string> CreateIsolatedStateEnvironment(string stateDirectory)
    {
        var fullStateDirectory = Path.GetFullPath(stateDirectory);
        var roaming = Path.Combine(fullStateDirectory, "appdata", "roaming");
        var local = Path.Combine(fullStateDirectory, "appdata", "local");
        var profile = Path.Combine(fullStateDirectory, "user-profile");
        var config = Path.Combine(fullStateDirectory, "xdg", "config");
        var data = Path.Combine(fullStateDirectory, "xdg", "data");
        var cache = Path.Combine(fullStateDirectory, "xdg", "cache");
        var temp = Path.Combine(fullStateDirectory, "temp");

        return new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["AVASCOPE_SCENARIO_STATE_DIR"] = fullStateDirectory,
            ["AVASCOPE_ISOLATED_STATE_DIR"] = fullStateDirectory,
            ["APPDATA"] = roaming,
            ["LOCALAPPDATA"] = local,
            ["USERPROFILE"] = profile,
            ["HOME"] = profile,
            ["XDG_CONFIG_HOME"] = config,
            ["XDG_DATA_HOME"] = data,
            ["XDG_CACHE_HOME"] = cache,
            ["TEMP"] = temp,
            ["TMP"] = temp
        };
    }

    private static void WriteTimeline(RuntimeScenarioResponse response)
    {
        if (string.IsNullOrWhiteSpace(response.TimelinePath))
        {
            return;
        }

        var directory = Path.GetDirectoryName(response.TimelinePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var builder = new StringBuilder();
        builder.AppendLine("# AvaScope Scenario Timeline");
        builder.AppendLine();
        builder.AppendLine($"- Request: `{response.RequestId}`");
        builder.AppendLine($"- Status: `{response.Status}`");
        builder.AppendLine($"- Started: `{response.StartedAt:O}`");
        builder.AppendLine($"- Completed: `{response.CompletedAt:O}`");
        builder.AppendLine($"- Isolation: `{response.IsolatedStateStatus}`");
        AppendOptional(builder, "Failure stage", response.FailureStage);
        AppendOptional(builder, "State directory", response.IsolatedStateDirectory);
        AppendOptional(builder, "Session", response.SessionId?.Value);
        AppendOptional(builder, "Top level", response.TopLevelId);
        AppendOptional(builder, "Build status", response.Build?.Status);
        AppendOptional(builder, "Build stdout", response.Build?.StdoutPath);
        AppendOptional(builder, "Build stderr", response.Build?.StderrPath);
        AppendOptional(builder, "Launch stdout", response.Launch?.StdoutPath);
        AppendOptional(builder, "Launch stderr", response.Launch?.StderrPath);
        AppendOptional(builder, "Bridge readiness", response.Readiness?.Status);
        AppendOptional(builder, "Cleanup outcome", response.Cleanup?.Outcome);
        builder.AppendLine($"- Registered top levels: `{response.TopLevels.Count.ToString(CultureInfo.InvariantCulture)}`");

        if (response.Diagnostics.Count > 0)
        {
            builder.AppendLine();
            builder.AppendLine("## Diagnostics");
            foreach (var diagnostic in response.Diagnostics)
            {
                builder.AppendLine($"- `{diagnostic.Code}` {diagnostic.Message}");
            }
        }

        builder.AppendLine();
        builder.AppendLine("## Steps");
        builder.AppendLine();
        builder.AppendLine("| # | Execution path | Step | Action | Status | Verify | Attempt | Fragment | Target | Evidence | Message |");
        builder.AppendLine("| - | -------------- | ---- | ------ | ------ | ------ | ------- | -------- | ------ | -------- | ------- |");

        var steps = response.Workflow?.Steps ?? [];
        for (var index = 0; index < steps.Count; index++)
        {
            var step = steps[index];
            var target = step.TopLevelAlias is not null
                ? $"{step.TopLevelAlias}->{step.ResolvedTopLevelId ?? "missing"}/{step.Target?.NodeId ?? step.Target?.TargetKind ?? "top-level"}"
                : step.Target is null
                    ? string.Empty
                    : $"{step.Target.TopLevelId}/{step.Target.NodeId ?? step.Target.TargetKind}";
            var evidence = step.Screenshot?.FilePath
                ?? step.Verification?.AfterScreenshot?.FilePath
                ?? step.FailureEvidence?.ArtifactDirectory
                ?? (step.Mutation is null ? null : $"mutation:{step.Mutation.Status}")
                ?? (step.Metadata.TryGetValue("idempotencyReplay", out var replay)
                    && string.Equals(replay, "true", StringComparison.Ordinal)
                        ? "idempotency:replay"
                        : null)
                ?? step.Metadata.FirstOrDefault(static item => item.Key.EndsWith("Path", StringComparison.OrdinalIgnoreCase)).Value
                ?? string.Empty;
            builder.AppendLine(
                $"| {(index + 1).ToString(CultureInfo.InvariantCulture)} | {EscapeTable(step.ExecutionPath ?? step.StepId)} | {EscapeTable(step.StepId)} | {EscapeTable(step.Action)} | {EscapeTable(step.Status)} | {EscapeTable(step.Verification?.Status)} | {EscapeTable(step.Attempt?.ToString(CultureInfo.InvariantCulture))} | {EscapeTable(step.SourceFragment)} | {EscapeTable(target)} | {EscapeTable(evidence)} | {EscapeTable(step.Message)} |");
        }

        File.WriteAllText(response.TimelinePath, builder.ToString());
    }

    private static void AppendOptional(StringBuilder builder, string label, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            builder.AppendLine($"- {label}: `{value}`");
        }
    }

    private static string EscapeTable(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Replace("|", "\\|", StringComparison.Ordinal).Replace(Environment.NewLine, " ", StringComparison.Ordinal);
    }

    private static ProtocolError ToProtocolError(CoreError error)
    {
        return new ProtocolError(error.Code, error.Message, error.Details);
    }

    private sealed record ScenarioIsolation(
        string Status,
        string? Directory,
        bool Applied,
        IReadOnlyDictionary<string, string> Environment);
}
