using System.Globalization;
using System.Text;
using AvaScope.Protocol;

namespace AvaScope.Core;

public sealed class RuntimeScenarioRunner
{
    private const string Passed = "passed";
    private const string Failed = "failed";

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
                }));
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
        NativePickerResponse? preparedPickerResult = null;
        LocalBridgeClient workflowClient = bridgeClient;
        var scenarioMode = "session";

        if (request.Launch is not null)
        {
            scenarioMode = "launch";
            var launchResult = await LaunchAsync(
                bridgeClient,
                request,
                outputDirectory,
                isolation,
                cancellationToken);
            if (!launchResult.Success)
            {
                diagnostics.Add(ToProtocolError(launchResult.Error!));
                return CoreResult<RuntimeScenarioResponse>.Ok(CreateResponse(
                    request,
                    Failed,
                    startedAt,
                    sessionId,
                    topLevelId,
                    launch,
                    attach,
                    workflow: null,
                    isolation,
                    timelinePath,
                    diagnostics,
                    outputDirectory,
                    scenarioMode));
            }

            launch = launchResult.Value!;
            sessionId = launch.Session.SessionId;
            topLevelId ??= launch.TopLevelId;
            workflowClient = new LocalBridgeClient(request.Launch.ManifestDirectory ?? bridgeClient.ManifestDirectory);
        }
        else if (request.Attach is not null)
        {
            scenarioMode = "attach";
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
                return CoreResult<RuntimeScenarioResponse>.Ok(CreateResponse(
                    request,
                    Failed,
                    startedAt,
                    sessionId,
                    topLevelId,
                    launch,
                    attach,
                    workflow: null,
                    isolation,
                    timelinePath,
                    diagnostics,
                    outputDirectory,
                    scenarioMode));
            }

            attach = attachResult.Value!;
            sessionId = attach.Session.SessionId;
        }
        else
        {
            var attachResult = await bridgeClient.AttachToAppAsync(
                sessionId: request.SessionId,
                cancellationToken: cancellationToken);
            if (!attachResult.Success)
            {
                diagnostics.Add(ToProtocolError(attachResult.Error!));
                return CoreResult<RuntimeScenarioResponse>.Ok(CreateResponse(
                    request,
                    Failed,
                    startedAt,
                    sessionId,
                    topLevelId,
                    launch,
                    attach,
                    workflow: null,
                    isolation,
                    timelinePath,
                    diagnostics,
                    outputDirectory,
                    scenarioMode));
            }

            attach = attachResult.Value!;
            sessionId = attach.Session.SessionId;
        }

        if (sessionId is null)
        {
            diagnostics.Add(new ProtocolError(
                "runtime_scenario_session_not_resolved",
                "Scenario could not resolve an active bridge session."));
            return CoreResult<RuntimeScenarioResponse>.Ok(CreateResponse(
                request,
                Failed,
                startedAt,
                sessionId,
                topLevelId,
                launch,
                attach,
                workflow: null,
                isolation,
                timelinePath,
                diagnostics,
                outputDirectory,
                scenarioMode));
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
                return CoreResult<RuntimeScenarioResponse>.Ok(CreateResponse(
                    request,
                    Failed,
                    startedAt,
                    sessionId,
                    topLevelId,
                    launch,
                    attach,
                    workflow: null,
                    isolation,
                    timelinePath,
                    diagnostics,
                    outputDirectory,
                    scenarioMode));
            }

            preparedPickerResult = prepareResult.Value;
        }

        topLevelId ??= await ResolveFirstTopLevelIdAsync(workflowClient, sessionId, cancellationToken);
        if (string.IsNullOrWhiteSpace(topLevelId))
        {
            diagnostics.Add(new ProtocolError(
                "runtime_scenario_top_level_not_resolved",
                "Scenario could not resolve a top-level id for the selected bridge session.",
                new Dictionary<string, string>
                {
                    ["sessionId"] = sessionId.Value
                }));
            return CoreResult<RuntimeScenarioResponse>.Ok(CreateResponse(
                request,
                Failed,
                startedAt,
                sessionId,
                topLevelId,
                launch,
                attach,
                workflow: null,
                isolation,
                timelinePath,
                diagnostics,
                outputDirectory,
                scenarioMode,
                preparedPickerResult));
        }

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
            return CoreResult<RuntimeScenarioResponse>.Ok(CreateResponse(
                request,
                Failed,
                startedAt,
                sessionId,
                topLevelId,
                launch,
                attach,
                workflow: null,
                isolation,
                timelinePath,
                diagnostics,
                outputDirectory,
                scenarioMode,
                preparedPickerResult));
        }

        diagnostics.AddRange(workflow.Value!.Diagnostics);
        var status = string.Equals(workflow.Value.Status, Passed, StringComparison.Ordinal)
            ? Passed
            : Failed;
        return CoreResult<RuntimeScenarioResponse>.Ok(CreateResponse(
            request,
            status,
            startedAt,
            sessionId,
            topLevelId,
            launch,
            attach,
            workflow.Value,
            isolation,
            timelinePath,
            diagnostics,
            outputDirectory,
            scenarioMode,
            preparedPickerResult));
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

        return await new BridgeAppLauncher().LaunchAsync(
            launch.Command,
            launch.Arguments,
            launch.WorkingDirectory,
            launch.DisplayName,
            launch.ManifestDirectory ?? bridgeClient.ManifestDirectory,
            launch.OutputDirectory ?? Path.Combine(outputDirectory, "launch"),
            environment,
            TimeSpan.FromMilliseconds(launch.TimeoutMs),
            cancellationToken);
    }

    private static async Task<string?> ResolveFirstTopLevelIdAsync(
        LocalBridgeClient client,
        SessionId sessionId,
        CancellationToken cancellationToken)
    {
        var topLevels = await client.ListTopLevelsAsync(sessionId, cancellationToken);
        return topLevels.Success
            ? topLevels.Value!.TopLevels.FirstOrDefault()?.Id
            : null;
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
        NativePickerResponse? preparedPickerResult = null)
    {
        var completedAt = DateTimeOffset.UtcNow;
        var metadata = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["scenarioMode"] = scenarioMode,
            ["outputDirectory"] = outputDirectory,
            ["timelineFormat"] = "markdown",
            ["requestedSteps"] = request.Steps.Count.ToString(CultureInfo.InvariantCulture)
        };

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
            preparedPickerResult);

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
        AppendOptional(builder, "State directory", response.IsolatedStateDirectory);
        AppendOptional(builder, "Session", response.SessionId?.Value);
        AppendOptional(builder, "Top level", response.TopLevelId);
        AppendOptional(builder, "Launch stdout", response.Launch?.StdoutPath);
        AppendOptional(builder, "Launch stderr", response.Launch?.StderrPath);

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
