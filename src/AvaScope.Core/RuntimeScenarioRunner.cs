using System.Globalization;
using System.Text;
using System.Text.Json;
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

        if (request.X11Environment is { } environmentOptions)
        {
            if (X11TestEnvironment.Validate(environmentOptions) is { } invalid)
                return CoreResult<RuntimeScenarioResponse>.Fail(new(invalid.Code, invalid.Message));
            if (request.Launch is null || environmentOptions.Mode == "managed" && !request.TerminateLaunchedProcess)
                return CoreResult<RuntimeScenarioResponse>.Fail(new("x11_environment_ownership_required", "X11 environments require an explicit launch; managed mode also requires terminateLaunchedProcess so the app exits before its owned desktop."));
        }

        var startedAt = DateTimeOffset.UtcNow;
        var evidencePolicy = request.Evidence?.Policy is null
            ? null
            : new RuntimeEvidencePolicyEnforcer(request.Evidence.Policy);
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
        var policyValidationDiagnostics = new List<ProtocolError>();
        if (evidencePolicy is not null)
        {
            if (request.TestFixture is { } fixtureOptions)
            {
                foreach (var (action, customName) in new[]
                {
                    (SemanticWorkflowActions.CustomActions, (string?)null),
                    (SemanticWorkflowActions.WaitForState, (string?)null),
                    (SemanticWorkflowActions.CustomAction, "fixture.prepare." + fixtureOptions.Name)
                })
                {
                    var authorization = evidencePolicy.AuthorizeAction(action, customName);
                    if (!authorization.Success) policyValidationDiagnostics.Add(ToProtocolError(authorization.Error!));
                }
            }

            if (request.StartupReadiness is not null)
            {
                var authorization = evidencePolicy.AuthorizeAction(SemanticWorkflowActions.WaitForState, customActionName: null);
                if (!authorization.Success) policyValidationDiagnostics.Add(ToProtocolError(authorization.Error!));
            }

            if (request.CaptureVisualTree)
            {
                var authorization = evidencePolicy.AuthorizeAction(SemanticWorkflowActions.Inspect, customActionName: null);
                if (!authorization.Success) policyValidationDiagnostics.Add(ToProtocolError(authorization.Error!));
            }

            foreach (var item in validation.Plan.Steps)
            {
                var authorization = evidencePolicy.AuthorizeAction(item.Action, customActionName: null);
                if (!authorization.Success)
                {
                    policyValidationDiagnostics.Add(ToProtocolError(authorization.Error!));
                    break;
                }
            }
        }

        if (!validation.Plan.Valid || policyValidationDiagnostics.Count > 0)
        {
            var completedAt = DateTimeOffset.UtcNow;
            var validationDiagnostics = validation.Plan.Diagnostics.Concat(policyValidationDiagnostics).ToArray();
            var validationWorkflow = new SemanticWorkflowResponse(
                request.RequestId,
                validationSessionId,
                validationTopLevelId,
                "validation_failed",
                startedAt,
                completedAt,
                [],
                diagnostics: validationDiagnostics,
                metadata: new Dictionary<string, string> { ["validationOnly"] = "true" },
                plan: validation.Plan);
            var invalidResponse = new RuntimeScenarioResponse(
                request.RequestId,
                Failed,
                startedAt,
                completedAt,
                workflow: validationWorkflow,
                diagnostics: validationDiagnostics,
                metadata: new Dictionary<string, string>
                {
                    ["scenarioMode"] = "validation",
                    ["dispatchPerformed"] = "false",
                    ["evidencePolicy"] = evidencePolicy is null ? "not_configured" : "explicit_local_opt_in",
                    ["storage"] = "local_filesystem",
                    ["provenance"] = "avascope_runtime_evidence",
                    ["networkUpload"] = "disabled"
                },
                failureStage: RuntimeScenarioFailureStages.Validation);
            if (evidencePolicy is null)
            {
                return CoreResult<RuntimeScenarioResponse>.Ok(invalidResponse);
            }

            var sanitized = evidencePolicy.Sanitize(invalidResponse);
            return CoreResult<RuntimeScenarioResponse>.Ok(sanitized.Success
                ? sanitized.Value!
                : CreateRedactionFailure(startedAt, sanitized.Error!));
        }

        var diagnostics = new List<ProtocolError>();
        var outputDirectory = ResolveOutputDirectory(request);
        var runStarted = new AgentRunStore().Begin(outputDirectory,
            request.Launch is null ? null : request.IsolatedStateDirectory ?? (request.IsolateState ? Path.Combine(outputDirectory, "isolated-state") : null),
            request.Launch?.OutputDirectory, request.Launch?.ManifestDirectory ?? bridgeClient.ManifestDirectory, request.Launch?.Environment,
            EnumerateWorkflowSteps(request).Select(step => step.ScreenshotPath).Append(request.TimelinePath).Append(request.Evidence?.ReportDirectory).ToArray());
        if (!runStarted.Success) return CoreResult<RuntimeScenarioResponse>.Fail(runStarted.Error!);
        using var runRegistration = runStarted.Value!;
        var timelinePath = request.TimelinePath ?? Path.Combine(outputDirectory, "scenario-timeline.md");
        IReadOnlyDictionary<string, string>? evidencePolicyMetadata = null;
        if (evidencePolicy is not null)
        {
            var policyReportDirectory = request.Evidence!.ReportDirectory
                ?? Path.Combine(outputDirectory, "reports");
            var workflowSteps = EnumerateWorkflowSteps(request).ToArray();
            if (workflowSteps.Any(step => !string.Equals(evidencePolicy.SanitizeScalar(step.Id), step.Id, StringComparison.Ordinal)))
            {
                runRegistration.Complete("completed", Failed, "policy");
                return CoreResult<RuntimeScenarioResponse>.Ok(CreatePolicyFailure(
                    startedAt,
                    new CoreError(
                        CoreErrorCodes.RuntimeEvidencePolicyInvalid,
                        "Workflow step identifiers cannot contain configured sensitive values because they contribute to local artifact paths.")) with { RunId = runRegistration.RunId });
            }

            var artifactPaths = workflowSteps
                .Select(static step => step.ScreenshotPath)
                .Append(policyReportDirectory)
                .Append(timelinePath)
                .Append(request.Launch?.OutputDirectory);
            var prepared = evidencePolicy.PrepareRun(outputDirectory, artifactPaths, request.RequestId);
            if (!prepared.Success)
            {
                runRegistration.Complete("completed", Failed, "policy");
                return CoreResult<RuntimeScenarioResponse>.Ok(CreatePolicyFailure(startedAt, prepared.Error!) with { RunId = runRegistration.RunId });
            }

            evidencePolicyMetadata = prepared.Value;
        }
        else
        {
            Directory.CreateDirectory(outputDirectory);
        }

        var isolation = PrepareIsolation(request, outputDirectory);
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
        X11TestEnvironment? desktop = null;
        var retainDesktop = false;
        CancellationTokenSource? environmentCancellation = null;
        var executionToken = cancellationToken;
        RuntimeTestFixtureRun? fixtureRun = null;
        var leaseAcquired = false;
        using var stopHeartbeat = new CancellationTokenSource();
        using var leaseFailure = new CancellationTokenSource();
        CancellationTokenSource? leaseCancellation = null;
        Task heartbeat = Task.CompletedTask;
        CoreError? heartbeatError = null;

        async Task<CoreResult<RuntimeScenarioResponse>> CompleteAsync(
            string status,
            string? failureStage,
            SemanticWorkflowResponse? workflow = null)
        {
            if (fixtureRun is not null)
            {
                var fixtureCleanup = await fixtureRun.CleanupAsync();
                if (!fixtureCleanup.Success)
                {
                    diagnostics.Add(ToProtocolError(fixtureCleanup.Error!));
                    status = Failed;
                    failureStage = "fixture_cleanup";
                }
            }
            if (launch is not null && request.TerminateLaunchedProcess && sessionId is not null)
            {
                await stopHeartbeat.CancelAsync();
                await heartbeat;
                var cleanupResult = await workflowClient.CloseSessionAsync(
                    sessionId,
                    CancellationToken.None,
                    terminateLaunchedProcess: true);
                if (!cleanupResult.Success)
                {
                    diagnostics.Insert(0, ToProtocolError(cleanupResult.Error!));
                    var ownership = new LaunchOwnershipStore(workflowClient.ManifestDirectory).TryRead(sessionId);
                    if (ownership is not null && cleanupResult.Error!.Code != "session_control_conflict")
                        cleanup = LocalBridgeClient.TerminateOwnedProcess(ownership, DateTimeOffset.UtcNow);
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
                if (desktop is not null && cleanup?.Outcome is not (CloseSessionOutcomes.Terminated or CloseSessionOutcomes.AlreadyExited))
                {
                    retainDesktop = true;
                    desktop.RetainForRecovery();
                    diagnostics.Add(new("run_desktop_retained", "The app could not be safely terminated; its owned desktop remains available for explicit run recovery."));
                }
            }

            await stopHeartbeat.CancelAsync();
            await heartbeat;
            if (heartbeatError is not null)
            {
                diagnostics.Add(ToProtocolError(heartbeatError));
                status = Failed;
                if (failureStage != RuntimeScenarioFailureStages.Cleanup) failureStage = "session_control";
            }
            if (leaseAcquired && sessionId is not null && cleanup is null)
            {
                var release = await workflowClient.SessionControlAsync(sessionId, new("release"), CancellationToken.None);
                if (!release.Success && workflowClient.ListSessionManifests().Any(m => m.SessionId == sessionId))
                {
                    diagnostics.Add(ToProtocolError(release.Error!));
                    status = Failed;
                    failureStage = RuntimeScenarioFailureStages.Cleanup;
                }
            }

            if (desktop is not null && !retainDesktop)
            {
                await desktop.DisposeAsync();
                if (desktop.UnexpectedExit is { } helperFailure)
                {
                    diagnostics.Add(helperFailure);
                    status = Failed;
                    failureStage = "environment";
                }
                if (desktop.Evidence.Status == "cleanup_failed")
                {
                    diagnostics.AddRange(desktop.Evidence.Diagnostics);
                    status = Failed;
                    failureStage = RuntimeScenarioFailureStages.Cleanup;
                }
            }

            runRegistration.Complete(failureStage == RuntimeScenarioFailureStages.Cleanup ? "partial_cleanup" : "completed", status, failureStage);
            var response = CreateResponse(
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
                failureStage,
                evidencePolicyMetadata,
                desktop?.Evidence,
                fixtureRun?.Evidence);
            response = response with { RunId = runRegistration.RunId };
            if (evidencePolicy is not null)
            {
                var sanitized = evidencePolicy.Sanitize(response);
                response = sanitized.Success
                    ? sanitized.Value!
                    : CreateRedactionFailure(startedAt, sanitized.Error!);
            }

            WriteTimeline(response);
            return CoreResult<RuntimeScenarioResponse>.Ok(response);
        }

        if (effectiveBuild is not null)
        {
            build = await new RuntimeScenarioBuilder().BuildAsync(
                effectiveBuild,
                outputDirectory,
                cancellationToken,
                evidencePolicy is null ? null : value => evidencePolicy.SanitizeScalar(value));
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
            if (request.X11Environment is not null)
            {
                currentStage = "environment";
                desktop = new X11TestEnvironment(request.X11Environment, Path.Combine(outputDirectory, "environment"),
                    evidencePolicy is null ? null : value => evidencePolicy.SanitizeScalar(value))
                { ProcessStarted = runRegistration.ProcessStarted, RuntimeDirectoryCreated = runRegistration.OwnRuntimeDirectory,
                    EvidenceChanged = runRegistration.EnvironmentChanged };
                var preparedEnvironment = await desktop.StartAsync(cancellationToken);
                if (!preparedEnvironment.Success)
                {
                    diagnostics.Add(ToProtocolError(preparedEnvironment.Error!));
                    return await CompleteAsync(cancellationToken.IsCancellationRequested ? Cancelled : Failed, currentStage);
                }
                environmentCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, desktop.FailureToken);
                executionToken = environmentCancellation.Token;
            }
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
                    evidencePolicy,
                    desktop?.EnvironmentVariables,
                    executionToken,
                    runRegistration);
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
                        cancellationToken: executionToken)
                    : await bridgeClient.AttachToAppAsync(
                        request.Attach.ProcessId,
                        request.Attach.SessionId,
                        request.Attach.ProcessName,
                        request.Attach.ManifestPath,
                        executionToken);
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
                    cancellationToken: executionToken);
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

            runRegistration.Session(attach!, token: null);
            var acquired = await workflowClient.SessionControlAsync(sessionId, new("acquire", "run:" + runRegistration.RunId), executionToken);
            if (!acquired.Success)
            {
                diagnostics.Add(ToProtocolError(acquired.Error!));
                return await CompleteAsync(Failed, "session_control");
            }
            leaseAcquired = true;
            runRegistration.Session(attach!, acquired.Value!.Token!);
            leaseCancellation = CancellationTokenSource.CreateLinkedTokenSource(executionToken, leaseFailure.Token);
            executionToken = leaseCancellation.Token;
            heartbeat = Task.Run(async () =>
            {
                using var timer = new PeriodicTimer(TimeSpan.FromSeconds(10));
                try
                {
                    while (await timer.WaitForNextTickAsync(stopHeartbeat.Token))
                    {
                        var renewed = await workflowClient.SessionControlAsync(sessionId, new("renew"), stopHeartbeat.Token);
                        if (!renewed.Success)
                        {
                            heartbeatError = renewed.Error;
                            await leaseFailure.CancelAsync();
                            break;
                        }
                    }
                }
                catch (OperationCanceledException) when (stopHeartbeat.IsCancellationRequested) { }
            }, CancellationToken.None);

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
                    executionToken);
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

            if (request.TestFixture is not null)
            {
                currentStage = "fixture_preparation";
                fixtureRun = new RuntimeTestFixtureRun(workflowClient, request, sessionId, topLevelId, outputDirectory, evidencePolicy);
                var prepared = await fixtureRun.PrepareAsync(executionToken);
                if (!prepared.Success)
                {
                    diagnostics.Add(ToProtocolError(prepared.Error!));
                    return await CompleteAsync(Failed, currentStage);
                }
            }

            if (request.StartupReadiness is { } startup)
            {
                currentStage = "ui_readiness";
                var checks = new List<SemanticWorkflowStep>();
                foreach (var (enabled, kind) in new[]
                {
                    (true, SemanticWaitConditionKinds.BridgeReady),
                    (startup.WaitForApplication, SemanticWaitConditionKinds.ApplicationReady),
                    (startup.WaitForFrame, SemanticWaitConditionKinds.FrameReady),
                    (startup.WaitForStableLayout, SemanticWaitConditionKinds.LayoutStable)
                })
                {
                    if (enabled) checks.Add(new SemanticWorkflowStep(SemanticWorkflowActions.WaitForState,
                        id: $"startup-{kind}", waitCondition: new SemanticWaitCondition(kind), timeoutMs: startup.TimeoutMs));
                }
                var probeStarted = DateTimeOffset.UtcNow;
                var probeWatch = System.Diagnostics.Stopwatch.StartNew();
                var observations = new List<RuntimeWaitObservation>();
                var probeDiagnostics = new List<ProtocolError>();
                foreach (var check in checks)
                {
                    var remaining = startup.TimeoutMs - (int)probeWatch.ElapsedMilliseconds;
                    if (remaining <= 0)
                    {
                        probeDiagnostics.Add(new ProtocolError("runtime_startup_readiness_timeout", "The shared startup readiness deadline expired."));
                        break;
                    }
                    var probe = await new SemanticWorkflowRunner().RunAsync(workflowClient,
                        new SemanticWorkflowRequest(sessionId, topLevelId,
                            [new SemanticWorkflowStep(check.Action, check.Id, waitCondition: check.WaitCondition, timeoutMs: remaining)],
                            request.RequestId, outputDirectory: outputDirectory, timeoutMs: remaining + 100,
                            evidence: new SemanticWorkflowEvidenceOptions(captureOnFailure: false, exportReports: false,
                                policy: request.Evidence?.Policy)), executionToken);
                    if (!probe.Success)
                    {
                        probeDiagnostics.Add(ToProtocolError(probe.Error!));
                        break;
                    }
                    observations.AddRange(probe.Value!.Steps.Where(static step => step.WaitObservation is not null)
                        .Select(static step => step.WaitObservation!));
                    probeDiagnostics.AddRange(probe.Value.Diagnostics);
                    if (probe.Value.Status != Passed) break;
                }
                var passed = probeDiagnostics.Count == 0 && observations.Count == checks.Count && observations.All(static value => value.Matched);
                readiness = new RuntimeScenarioReadinessEvidence(passed ? "ready" : "failed",
                    readiness?.StartedAt ?? probeStarted, DateTimeOffset.UtcNow,
                    (readiness?.CheckCount ?? 0) + observations.Count,
                    launch?.ProcessId ?? attach?.ProcessId, sessionId, launch?.ManifestPath ?? attach?.ManifestPath,
                    launch?.StdoutPath, launch?.StderrPath, topLevels,
                    diagnostic: probeDiagnostics.FirstOrDefault(), metadata: readiness?.Metadata,
                    observations: observations);
                if (!passed)
                {
                    diagnostics.AddRange(probeDiagnostics);
                    return await CompleteAsync(Failed, currentStage);
                }
            }

            if (request.CaptureVisualTree)
            {
                currentStage = "inspection";
                var tree = await workflowClient.VisualTreeAsync(sessionId, topLevelId, Math.Min(request.MaxDepth, 32), executionToken);
                if (!tree.Success)
                {
                    diagnostics.Add(ToProtocolError(tree.Error!));
                    return await CompleteAsync(Failed, currentStage);
                }

                if (evidencePolicy is not null)
                {
                    tree = evidencePolicy.Sanitize(tree.Value!);
                    if (!tree.Success)
                    {
                        diagnostics.Add(ToProtocolError(tree.Error!));
                        return await CompleteAsync(Failed, currentStage);
                    }
                }
                File.WriteAllText(Path.Combine(outputDirectory, "runtime-tree.json"), JsonSerializer.Serialize(tree.Value, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
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
            var workflow = await new SemanticWorkflowRunner().RunAsync(workflowClient, workflowRequest, executionToken);
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
        catch (OperationCanceledException) when (executionToken.IsCancellationRequested)
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
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException or ArgumentException)
        {
            diagnostics.Add(new ProtocolError("runtime_scenario_stage_failed", exception.Message,
                new Dictionary<string, string> { ["failureStage"] = currentStage }));
            return await CompleteAsync(Failed, currentStage);
        }
        finally
        {
            await stopHeartbeat.CancelAsync();
            await heartbeat;
            leaseCancellation?.Dispose();
            environmentCancellation?.Dispose();
            if (desktop is not null && !retainDesktop) await desktop.DisposeAsync();
        }
    }

    private async Task<CoreResult<LaunchAppResponse>> LaunchAsync(
        LocalBridgeClient bridgeClient,
        RuntimeScenarioRequest request,
        string outputDirectory,
        ScenarioIsolation isolation,
        RuntimeEvidencePolicyEnforcer? evidencePolicy,
        IReadOnlyDictionary<string, string>? desktopEnvironment,
        CancellationToken cancellationToken,
        AgentRunStore.Registration runRegistration)
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

        if (desktopEnvironment is not null)
            foreach (var pair in desktopEnvironment) environment[pair.Key] = pair.Value;

        environment["AVASCOPE_SCENARIO_ID"] = request.RequestId;
        environment["AVASCOPE_RUN_ID"] = runRegistration.RunId;

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
            captureOutputUntilExit: true,
            outputSanitizer: evidencePolicy is null ? null : value => evidencePolicy.SanitizeScalar(value),
            processStarted: process => runRegistration.ProcessStarted("app", process));
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
        string? failureStage = null,
        IReadOnlyDictionary<string, string>? evidencePolicyMetadata = null,
        RuntimeEnvironmentEvidence? environment = null,
        RuntimeTestFixtureEvidence? testFixture = null)
    {
        var completedAt = DateTimeOffset.UtcNow;
        var metadata = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["scenarioMode"] = scenarioMode,
            ["outputDirectory"] = outputDirectory,
            ["timelineFormat"] = "markdown",
            ["requestedSteps"] = request.Steps.Count.ToString(CultureInfo.InvariantCulture),
            ["terminateLaunchedProcessRequested"] = request.TerminateLaunchedProcess.ToString().ToLowerInvariant(),
            ["storage"] = "local_filesystem",
            ["provenance"] = "avascope_runtime_evidence",
            ["networkUpload"] = "disabled"
        };

        if (evidencePolicyMetadata is not null)
        {
            foreach (var pair in evidencePolicyMetadata)
            {
                metadata[pair.Key] = pair.Value;
            }
        }

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
            failureStage: failureStage,
            environment: environment,
            testFixture: testFixture);

        return response;
    }

    private static string ResolveOutputDirectory(RuntimeScenarioRequest request)
    {
        return request.OutputDirectory
            ?? Path.Combine(Path.GetTempPath(), "AvaScope", "scenarios", Guid.NewGuid().ToString("N"));
    }

    private static IEnumerable<SemanticWorkflowStep> EnumerateWorkflowSteps(RuntimeScenarioRequest request)
    {
        foreach (var step in request.Steps.Concat(request.Fragments.SelectMany(static fragment => fragment.Steps)))
        {
            foreach (var item in EnumerateWorkflowSteps(step))
            {
                yield return item;
            }
        }
    }

    private static IEnumerable<SemanticWorkflowStep> EnumerateWorkflowSteps(SemanticWorkflowStep step)
    {
        yield return step;
        foreach (var child in step.Then.Concat(step.Else).Concat(step.Steps))
        {
            foreach (var item in EnumerateWorkflowSteps(child))
            {
                yield return item;
            }
        }
    }

    private static RuntimeScenarioResponse CreatePolicyFailure(DateTimeOffset startedAt, CoreError error) =>
        new(
            "redacted-request",
            Failed,
            startedAt,
            DateTimeOffset.UtcNow,
            diagnostics: [ToProtocolError(error)],
            metadata: new Dictionary<string, string>
            {
                ["dispatchPerformed"] = "false",
                ["evidencePolicy"] = "rejected",
                ["storage"] = "local_filesystem",
                ["provenance"] = "avascope_runtime_evidence",
                ["networkUpload"] = "disabled"
            },
            failureStage: RuntimeScenarioFailureStages.Validation);

    private static RuntimeScenarioResponse CreateRedactionFailure(DateTimeOffset startedAt, CoreError error) =>
        new(
            "redacted-request",
            Failed,
            startedAt,
            DateTimeOffset.UtcNow,
            diagnostics: [ToProtocolError(error)],
            metadata: new Dictionary<string, string>
            {
                ["evidencePolicy"] = "failed_closed",
                ["storage"] = "local_filesystem",
                ["provenance"] = "avascope_runtime_evidence",
                ["networkUpload"] = "disabled"
            },
            failureStage: RuntimeScenarioFailureStages.Validation);

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
        var environment = CreateIsolatedStateEnvironment(stateDirectory, outputDirectory);
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

    private static IReadOnlyDictionary<string, string> CreateIsolatedStateEnvironment(
        string stateDirectory,
        string outputDirectory)
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
            [ResponseBudgeter.ArtifactDirectoryEnvironmentVariable] = Path.Combine(Path.GetFullPath(outputDirectory), "response-artifacts"),
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
        if (response.TestFixture is { } fixture)
        {
            AppendOptional(builder, "Fixture", fixture.Name);
            AppendOptional(builder, "Fixture version", fixture.Version);
            AppendOptional(builder, "Fixture resource", fixture.ResourceId);
            AppendOptional(builder, "Fixture preparation", fixture.PreparationStatus);
            AppendOptional(builder, "Fixture readiness", fixture.ReadinessStatus);
            AppendOptional(builder, "Fixture cleanup", fixture.CleanupStatus);
        }
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
