using System.Globalization;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using AvaScope.Protocol;

namespace AvaScope.Core;

public sealed class SemanticWorkflowRunner
{
    private static readonly JsonSerializerOptions EvidenceJsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private static readonly string[] DestructiveTokens =
    [
        "delete",
        "remove",
        "close",
        "destroy",
        "drop",
        "clear-all"
    ];

    public async Task<CoreResult<SemanticWorkflowResponse>> RunAsync(
        LocalBridgeClient bridgeClient,
        SemanticWorkflowRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(bridgeClient);
        ArgumentNullException.ThrowIfNull(request);

        var startedAt = DateTimeOffset.UtcNow;
        var compiled = SemanticWorkflowCompiler.Compile(request);
        var policy = request.Evidence?.Policy is null
            ? null
            : new RuntimeEvidencePolicyEnforcer(request.Evidence.Policy);
        var policyDiagnostics = new List<ProtocolError>();
        if (policy is not null)
        {
            foreach (var item in compiled.Plan.Steps)
            {
                var authorization = policy.AuthorizeAction(item.Action, customActionName: null);
                if (!authorization.Success)
                {
                    policyDiagnostics.Add(ToProtocolError(authorization.Error!));
                    break;
                }
            }
        }

        var isolatedStateStatus = string.IsNullOrWhiteSpace(request.IsolatedStateDirectory)
            ? "not_configured"
            : "declared_by_request";
        if (!compiled.Plan.Valid || policyDiagnostics.Count > 0 || request.ValidateOnly)
        {
            var validationStatus = compiled.Plan.Valid && policyDiagnostics.Count == 0 ? "validated" : "validation_failed";
            var validationDiagnostics = compiled.Plan.Diagnostics.Concat(policyDiagnostics).ToArray();
            var validationResponse = new SemanticWorkflowResponse(
                request.RequestId,
                request.SessionId,
                request.TopLevelId,
                validationStatus,
                startedAt,
                DateTimeOffset.UtcNow,
                [],
                isolatedStateStatus,
                validationDiagnostics,
                CreateWorkflowMetadata(request, compiled.Plan, replayCount: 0, executedSteps: 0),
                plan: compiled.Plan);
            if (policy is null)
            {
                return CoreResult<SemanticWorkflowResponse>.Ok(validationResponse);
            }

            var sanitized = policy.Sanitize(validationResponse);
            return CoreResult<SemanticWorkflowResponse>.Ok(sanitized.Success
                ? sanitized.Value!
                : RedactionFailureResponse(validationResponse, sanitized.Error!));
        }

        IReadOnlyDictionary<string, string>? policyMetadata = null;
        if (policy is not null)
        {
            var artifactRoot = ResolveArtifactRoot(request);
            var policyReportDirectory = request.Evidence!.ReportDirectory
                ?? Path.Combine(artifactRoot, "reports");
            var workflowSteps = EnumerateWorkflowSteps(request).ToArray();
            if (workflowSteps.Any(step => !string.Equals(policy.SanitizeScalar(step.Id), step.Id, StringComparison.Ordinal)))
            {
                return PolicyFailure(new CoreError(
                    CoreErrorCodes.RuntimeEvidencePolicyInvalid,
                    "Workflow step identifiers cannot contain configured sensitive values because they contribute to local artifact paths."));
            }

            var artifactPaths = workflowSteps
                .Select(static step => step.ScreenshotPath)
                .Append(policyReportDirectory);
            var prepared = policy.PrepareRun(artifactRoot, artifactPaths, request.RequestId);
            if (!prepared.Success)
            {
                return PolicyFailure(prepared.Error!);
            }

            var authorization = policy.Authorize(bridgeClient, request, compiled.Plan);
            if (!authorization.Success)
            {
                return PolicyFailure(authorization.Error!);
            }

            policyMetadata = prepared.Value!
                .Concat(authorization.Value!)
                .ToDictionary(static pair => pair.Key, static pair => pair.Value, StringComparer.Ordinal);
        }
        else if (!string.IsNullOrWhiteSpace(request.OutputDirectory))
        {
            Directory.CreateDirectory(request.OutputDirectory);
        }

        using var workflowCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        workflowCancellation.CancelAfter(request.TimeoutMs);
        var context = new CompositionExecutionContext(
            request,
            bridgeClient,
            new WorkflowIdempotencyStore(policy?.RunDirectory ?? bridgeClient.ManifestDirectory),
            compiled.Plan,
            policy,
            policyMetadata);
        try
        {
            await ExecuteNodesAsync(context, compiled.Roots, attempt: null, workflowCancellation.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            context.Diagnostics.Add(new ProtocolError(
                "semantic_workflow_timeout",
                $"Workflow exceeded its bounded {request.TimeoutMs.ToString(CultureInfo.InvariantCulture)} ms timeout.",
                new Dictionary<string, string>
                {
                    ["timeoutMs"] = request.TimeoutMs.ToString(CultureInfo.InvariantCulture),
                    ["executedSteps"] = context.Results.Count.ToString(CultureInfo.InvariantCulture),
                    ["nextAction"] = "Inspect the execution timeline, then reduce work or increase timeoutMs within the advertised limit."
                }));
        }

        var status = context.Results.All(static result => result.Status is "passed" or "skipped" or "retried")
            && context.Diagnostics.Count == 0
            ? "passed"
            : "failed";

        var response = new SemanticWorkflowResponse(
            request.RequestId,
            request.SessionId,
            request.TopLevelId,
            status,
            startedAt,
            DateTimeOffset.UtcNow,
            context.Results,
            isolatedStateStatus,
            context.Diagnostics,
            CreateWorkflowMetadata(
                request,
                compiled.Plan,
                context.ReplayCount,
                context.Results.Count,
                context.PolicyMetadata),
            plan: compiled.Plan);
        if (policy is not null)
        {
            var sanitized = policy.Sanitize(response);
            if (!sanitized.Success)
            {
                response = RedactionFailureResponse(response, sanitized.Error!);
            }
            else
            {
                response = sanitized.Value!;
            }
        }

        if (request.Evidence is not { ExportReports: true })
        {
            return CoreResult<SemanticWorkflowResponse>.Ok(response);
        }

        var reportDirectory = request.Evidence.ReportDirectory
            ?? Path.Combine(ResolveArtifactRoot(request), "reports");
        var report = new SemanticWorkflowReportPackExporter().Export(response, reportDirectory);
        if (!report.Success)
        {
            var diagnostics = response.Diagnostics
                .Append(ToProtocolError(report.Error!))
                .ToArray();
            response = new SemanticWorkflowResponse(
                response.RequestId,
                response.SessionId,
                response.TopLevelId,
                response.Status,
                response.StartedAt,
                response.CompletedAt,
                response.Steps,
                response.IsolatedStateStatus,
                diagnostics,
                response.Metadata,
                plan: response.Plan);
            if (policy is not null)
            {
                var sanitized = policy.Sanitize(response);
                response = sanitized.Success
                    ? sanitized.Value!
                    : RedactionFailureResponse(response, sanitized.Error!);
            }

            return CoreResult<SemanticWorkflowResponse>.Ok(response);
        }

        if (string.Equals(report.Value!.Status, "partial", StringComparison.Ordinal))
        {
            var diagnostics = response.Diagnostics
                .Append(new ProtocolError(
                    "semantic_workflow_report_partial",
                    "One or more requested workflow report assets could not be written.",
                    report.Value.Metadata))
                .ToArray();
            response = new SemanticWorkflowResponse(
                response.RequestId,
                response.SessionId,
                response.TopLevelId,
                response.Status,
                response.StartedAt,
                response.CompletedAt,
                response.Steps,
                response.IsolatedStateStatus,
                diagnostics,
                response.Metadata,
                plan: response.Plan,
                reportPack: report.Value);
        }
        else
        {
            response = new SemanticWorkflowResponse(
                response.RequestId,
                response.SessionId,
                response.TopLevelId,
                response.Status,
                response.StartedAt,
                response.CompletedAt,
                response.Steps,
                response.IsolatedStateStatus,
                response.Diagnostics,
                response.Metadata,
                plan: response.Plan,
                reportPack: report.Value);
        }

        if (policy is not null)
        {
            var sanitized = policy.Sanitize(response);
            response = sanitized.Success
                ? sanitized.Value!
                : RedactionFailureResponse(response, sanitized.Error!);
        }

        return CoreResult<SemanticWorkflowResponse>.Ok(response);

        CoreResult<SemanticWorkflowResponse> PolicyFailure(CoreError error)
        {
            var diagnostic = ToProtocolError(error);
            var failure = new SemanticWorkflowResponse(
                request.RequestId,
                request.SessionId,
                request.TopLevelId,
                "validation_failed",
                startedAt,
                DateTimeOffset.UtcNow,
                [],
                isolatedStateStatus,
                [diagnostic],
                CreateWorkflowMetadata(request, compiled.Plan, replayCount: 0, executedSteps: 0),
                plan: compiled.Plan);
            if (policy is null)
            {
                return CoreResult<SemanticWorkflowResponse>.Ok(failure);
            }

            var sanitized = policy.Sanitize(failure);
            return CoreResult<SemanticWorkflowResponse>.Ok(sanitized.Success
                ? sanitized.Value!
                : RedactionFailureResponse(failure, sanitized.Error!));
        }
    }

    private static IReadOnlyDictionary<string, string> CreateWorkflowMetadata(
        SemanticWorkflowRequest request,
        SemanticWorkflowPlan plan,
        int replayCount,
        int executedSteps,
        IReadOnlyDictionary<string, string>? policyMetadata = null)
    {
        var metadata = new Dictionary<string, string>
        {
            ["requestedSteps"] = request.Steps.Count.ToString(CultureInfo.InvariantCulture),
            ["expandedSteps"] = plan.ExpandedStepCount.ToString(CultureInfo.InvariantCulture),
            ["executedSteps"] = executedSteps.ToString(CultureInfo.InvariantCulture),
            ["idempotencyReplayCount"] = replayCount.ToString(CultureInfo.InvariantCulture),
            ["selectorMode"] = "automation_text_name_type_binding_or_node_id",
            ["topLevelAliasCount"] = request.TopLevelAliases.Count.ToString(CultureInfo.InvariantCulture),
            ["topLevelResolution"] = request.TopLevelAliases.Count == 0 ? "root_runtime_id" : "semantic_alias_per_use",
            ["composition"] = "bounded_if_else_optional_retry_variables_fragments",
            ["validationOnly"] = request.ValidateOnly.ToString().ToLowerInvariant(),
            ["workflowTimeoutMs"] = request.TimeoutMs.ToString(CultureInfo.InvariantCulture),
            ["verification"] = "observe_act_typed_wait_verify",
            ["failureEvidence"] = request.Evidence is null ? "not_requested" : "bounded_request_policy",
            ["reportExport"] = request.Evidence?.ExportReports == true ? "json_markdown_junit" : "not_requested",
            ["evidencePolicy"] = request.Evidence?.Policy is null ? "not_configured" : "explicit_local_opt_in",
            ["storage"] = "local_filesystem",
            ["provenance"] = "avascope_runtime_evidence",
            ["networkUpload"] = "disabled"
        };
        if (policyMetadata is not null)
        {
            foreach (var pair in policyMetadata)
            {
                metadata[pair.Key] = pair.Value;
            }
        }

        return metadata;
    }

    private static async Task<bool> ExecuteNodesAsync(
        CompositionExecutionContext context,
        IReadOnlyList<CompiledWorkflowNode> nodes,
        int? attempt,
        CancellationToken cancellationToken)
    {
        foreach (var node in nodes)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (context.Results.Count >= SemanticWorkflowLimits.MaximumEstimatedExecutions)
            {
                context.Diagnostics.Add(new ProtocolError(
                    "semantic_workflow_runtime_result_limit",
                    $"Workflow reached the runtime result limit of {SemanticWorkflowLimits.MaximumEstimatedExecutions}.",
                    new Dictionary<string, string>
                    {
                        ["executionPath"] = node.ExecutionPath,
                        ["nextAction"] = "Reduce branch, retry, fragment, or automatic evidence expansion."
                    }));
                return false;
            }

            var succeeded = node.Step.Action switch
            {
                SemanticWorkflowActions.If => await ExecuteIfAsync(context, node, attempt, cancellationToken),
                SemanticWorkflowActions.RetryUntil => await ExecuteRetryUntilAsync(context, node, cancellationToken),
                SemanticWorkflowActions.UseFragment => await ExecuteFragmentAsync(context, node, attempt, cancellationToken),
                _ => await ExecuteLeafAsync(context, node, attempt, cancellationToken)
            };
            if (!succeeded)
            {
                return false;
            }
        }

        return true;
    }

    private static async Task<bool> ExecuteLeafAsync(
        CompositionExecutionContext context,
        CompiledWorkflowNode node,
        int? attempt,
        CancellationToken cancellationToken)
    {
        if (context.Policy is not null)
        {
            var authorization = context.Policy.AuthorizeAction(node.Step.Action, node.Step.CustomActionName);
            if (!authorization.Success)
            {
                var rejected = WithCompositionEvidence(Fail(node.Step, authorization.Error!), node, attempt);
                context.Results.Add(rejected);
                context.Diagnostics.AddRange(rejected.Diagnostics);
                AppendAudit(context, rejected);
                return false;
            }
        }

        var verificationStartedAt = DateTimeOffset.UtcNow;
        var before = node.Step.Verify is { CaptureBefore: true }
            ? await CaptureVerificationSnapshotAsync(
                context,
                node,
                "before",
                node.Step.Verify.CaptureScreenshots,
                cancellationToken)
            : VerificationSnapshot.Empty;
        var result = await ExecuteStepWithIdempotencyAsync(
            context.BridgeClient,
            context.IdempotencyStore,
            context.Request,
            node.Step,
            context.Results.Count,
            context.Policy,
            cancellationToken,
            node.ParentStepId is null ? null : node.ExecutionPath);
        if (node.Step.Verify is not null)
        {
            result = string.Equals(result.Status, "passed", StringComparison.Ordinal)
                ? await VerifyActionAsync(
                    context,
                    node,
                    result,
                    before,
                    verificationStartedAt,
                    cancellationToken)
                : WithVerification(
                    result,
                    new SemanticWorkflowVerificationResult(
                        "not_run",
                        node.Step.Verify.Condition,
                        verificationStartedAt,
                        DateTimeOffset.UtcNow,
                        before.Inspection,
                        beforeScreenshot: before.Screenshot,
                        diagnostics: before.Diagnostics,
                        metadata: new Dictionary<string, string>
                        {
                            ["reason"] = "action_failed",
                            ["postconditionDispatched"] = "false"
                        }));
        }

        result = WithCompositionEvidence(result, node, attempt);
        var replayed = result.Metadata.TryGetValue("idempotencyReplay", out var replay)
            && string.Equals(replay, "true", StringComparison.Ordinal);
        if (replayed)
        {
            context.ReplayCount++;
        }

        if (result.Status == "failed" && node.Step.Optional)
        {
            result = AsOptionalSkipped(result);
        }

        if (result.Status == "failed" && context.Request.Evidence is { CaptureOnFailure: true })
        {
            var failureEvidence = await CaptureFailureEvidenceAsync(
                context,
                node,
                result,
                cancellationToken);
            result = WithFailureEvidence(result, failureEvidence);
        }

        context.Results.Add(result);
        if (!AppendAudit(context, result))
        {
            return false;
        }

        if (result.Status == "failed")
        {
            context.Diagnostics.AddRange(result.Diagnostics);
            return false;
        }

        if (context.Request.CaptureAfterEachStep
            && result.Status == "passed"
            && !replayed
            && result.Screenshot is null
            && !string.IsNullOrWhiteSpace(context.Request.OutputDirectory)
            && node.Step.Action != SemanticWorkflowActions.Wait)
        {
            var screenshot = await CaptureStepScreenshotAsync(
                context.BridgeClient,
                context.Request,
                node.Step,
                context.Results.Count,
                context.Policy,
                cancellationToken);
            screenshot = WithCompositionEvidence(
                screenshot,
                node with
                {
                    ExecutionPath = $"{node.ExecutionPath}/evidence",
                    ParentStepId = node.Step.Id
                },
                attempt);
            context.Results.Add(screenshot);
            if (!AppendAudit(context, screenshot))
            {
                return false;
            }

            if (screenshot.Status == "failed")
            {
                context.Diagnostics.AddRange(screenshot.Diagnostics);
                return false;
            }
        }

        return true;
    }

    private static async Task<SemanticWorkflowStepResult> VerifyActionAsync(
        CompositionExecutionContext context,
        CompiledWorkflowNode node,
        SemanticWorkflowStepResult actionResult,
        VerificationSnapshot before,
        DateTimeOffset startedAt,
        CancellationToken cancellationToken)
    {
        var verification = node.Step.Verify!;
        var probe = new SemanticWorkflowStep(
            SemanticWorkflowActions.WaitForState,
            $"{node.Step.Id}:verify",
            verification.Selector ?? node.Step.Selector,
            timeoutMs: verification.TimeoutMs,
            pollIntervalMs: verification.PollIntervalMs,
            waitCondition: verification.Condition,
            topLevelAlias: verification.TopLevelAlias ?? node.Step.TopLevelAlias);
        var wait = await WaitForConditionAsync(
            context.BridgeClient,
            context.Request,
            probe,
            verification.Condition,
            cancellationToken);
        var after = verification.CaptureAfter
            ? await CaptureVerificationSnapshotAsync(
                context,
                node,
                "after",
                verification.CaptureScreenshots,
                cancellationToken)
            : VerificationSnapshot.Empty;
        var passed = string.Equals(wait.Status, "passed", StringComparison.Ordinal);
        var diagnostics = before.Diagnostics
            .Concat(wait.Diagnostics)
            .Concat(after.Diagnostics)
            .ToArray();
        var metadata = new Dictionary<string, string>(wait.Metadata, StringComparer.Ordinal)
        {
            ["postconditionDispatched"] = "true",
            ["condition"] = verification.Condition.Kind,
            ["timeoutMs"] = verification.TimeoutMs.ToString(CultureInfo.InvariantCulture),
            ["pollIntervalMs"] = verification.PollIntervalMs.ToString(CultureInfo.InvariantCulture),
            ["captureBefore"] = verification.CaptureBefore.ToString().ToLowerInvariant(),
            ["captureAfter"] = verification.CaptureAfter.ToString().ToLowerInvariant(),
            ["captureScreenshots"] = verification.CaptureScreenshots.ToString().ToLowerInvariant(),
            ["artifactStatus"] = before.Diagnostics.Count + after.Diagnostics.Count == 0 ? "complete" : "partial"
        };
        var result = new SemanticWorkflowVerificationResult(
            passed ? "passed" : "failed",
            verification.Condition,
            startedAt,
            DateTimeOffset.UtcNow,
            before.Inspection,
            verification.CaptureAfter ? after.Inspection ?? wait.Inspection : null,
            before.Screenshot,
            after.Screenshot,
            wait.WaitObservation,
            diagnostics,
            metadata);
        return WithVerification(actionResult, result);
    }

    private static async Task<VerificationSnapshot> CaptureVerificationSnapshotAsync(
        CompositionExecutionContext context,
        CompiledWorkflowNode node,
        string phase,
        bool captureScreenshot,
        CancellationToken cancellationToken)
    {
        var verification = node.Step.Verify!;
        var diagnostics = new List<ProtocolError>();
        var request = context.Request;
        var alias = verification.TopLevelAlias ?? node.Step.TopLevelAlias;
        if (alias is not null)
        {
            var topLevel = await ResolveTopLevelAliasAsync(
                context.BridgeClient,
                request,
                alias,
                cancellationToken);
            if (topLevel.Success)
            {
                request = WithTopLevelId(request, topLevel.Value!.Summary.Id);
            }
            else
            {
                diagnostics.Add(ToProtocolError(topLevel.Error!));
            }
        }

        InspectNodeResponse? inspection = null;
        var selector = verification.Selector ?? node.Step.Selector;
        if (!string.IsNullOrWhiteSpace(request.TopLevelId) && selector is { HasSearchCriteria: true })
        {
            var target = await ResolveSelectorAsync(
                context.BridgeClient,
                request,
                selector,
                $"Verification {phase} snapshot",
                cancellationToken);
            if (target.Success)
            {
                var inspect = await context.BridgeClient.InspectNodeAsync(
                    request.SessionId,
                    request.TopLevelId!,
                    target.Value!.Target.TreeKind ?? TreeKinds.Visual,
                    target.Value.Target.NodeId!,
                    cancellationToken);
                if (inspect.Success)
                {
                    inspection = inspect.Value;
                }
                else
                {
                    diagnostics.Add(ToProtocolError(inspect.Error!));
                }
            }
            else
            {
                diagnostics.Add(ToProtocolError(target.Error!));
            }
        }

        ScreenshotResponse? screenshot = null;
        if (captureScreenshot && !string.IsNullOrWhiteSpace(request.TopLevelId))
        {
            try
            {
                var directory = Path.Combine(
                    ResolveArtifactRoot(context.Request),
                    "verification",
                    SafeArtifactName(node.ExecutionPath));
                Directory.CreateDirectory(directory);
                var capture = await context.BridgeClient.CaptureScreenshotAsync(
                    request.SessionId,
                    request.TopLevelId!,
                    Path.Combine(directory, $"{phase}.png"),
                    cancellationToken);
                if (capture.Success)
                {
                    if (context.Policy is null)
                    {
                        screenshot = capture.Value;
                    }
                    else
                    {
                        var masked = await context.Policy.MaskScreenshotAsync(
                            context.BridgeClient,
                            request,
                            capture.Value!,
                            cancellationToken);
                        if (masked.Success)
                        {
                            screenshot = capture.Value;
                        }
                        else
                        {
                            diagnostics.Add(ToProtocolError(masked.Error!));
                        }
                    }
                }
                else
                {
                    diagnostics.Add(ToProtocolError(capture.Error!));
                }
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
            {
                diagnostics.Add(new ProtocolError(
                    "semantic_workflow_verification_artifact_unavailable",
                    $"Verification {phase} screenshot could not be created: {exception.Message}"));
            }
        }

        return new VerificationSnapshot(inspection, screenshot, diagnostics);
    }

    private static async Task<SemanticWorkflowFailureEvidence> CaptureFailureEvidenceAsync(
        CompositionExecutionContext context,
        CompiledWorkflowNode node,
        SemanticWorkflowStepResult failedResult,
        CancellationToken cancellationToken)
    {
        var options = context.Request.Evidence!;
        var directory = Path.Combine(
            ResolveArtifactRoot(context.Request),
            "failures",
            SafeArtifactName(node.ExecutionPath));
        var unavailable = new List<string>();
        var diagnostics = new List<ProtocolError>();
        try
        {
            Directory.CreateDirectory(directory);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            var diagnostic = new ProtocolError(
                "semantic_workflow_failure_evidence_unavailable",
                $"Failure evidence directory could not be created: {exception.Message}");
            return new SemanticWorkflowFailureEvidence(
                "unavailable",
                directory,
                unavailableEvidence: ["artifact_directory"],
                diagnostics: [diagnostic]);
        }

        var request = context.Request;
        var alias = node.Step.Verify?.TopLevelAlias ?? node.Step.TopLevelAlias;
        var verificationResolvedTopLevelId = failedResult.Verification?.Metadata.TryGetValue(
            "resolvedTopLevelId",
            out var verificationTopLevelId) == true
                ? verificationTopLevelId
                : null;
        var resolvedTopLevelId = verificationResolvedTopLevelId
            ?? failedResult.Verification?.AfterInspection?.TopLevelId
            ?? failedResult.Target?.TopLevelId
            ?? failedResult.ResolvedTopLevelId;
        if (resolvedTopLevelId is null && alias is not null)
        {
            var topLevel = await ResolveTopLevelAliasAsync(
                context.BridgeClient,
                request,
                alias,
                cancellationToken);
            if (topLevel.Success)
            {
                resolvedTopLevelId = topLevel.Value!.Summary.Id;
            }
            else
            {
                AddUnavailable("resolved_top_level", topLevel.Error!);
            }
        }

        resolvedTopLevelId ??= request.TopLevelId;
        if (resolvedTopLevelId is not null)
        {
            request = WithTopLevelId(request, resolvedTopLevelId);
        }

        var selector = node.Step.Verify?.Selector ?? node.Step.Selector;
        var inspection = failedResult.Verification?.AfterInspection
            ?? failedResult.Inspection;
        if (inspection is null
            && resolvedTopLevelId is not null
            && selector is { HasSearchCriteria: true })
        {
            var target = await ResolveSelectorAsync(
                context.BridgeClient,
                request,
                selector,
                "Failure evidence",
                cancellationToken);
            if (target.Success)
            {
                var inspect = await context.BridgeClient.InspectNodeAsync(
                    request.SessionId,
                    resolvedTopLevelId,
                    target.Value!.Target.TreeKind ?? TreeKinds.Visual,
                    target.Value.Target.NodeId!,
                    cancellationToken);
                if (inspect.Success)
                {
                    inspection = inspect.Value;
                }
                else
                {
                    AddUnavailable("inspection", inspect.Error!);
                }
            }
            else
            {
                AddUnavailable("inspection", target.Error!);
            }
        }

        string? inspectionPath = null;
        if (inspection is not null)
        {
            inspectionPath = WriteJson("inspection", "inspection.json", inspection);
            if (inspection.InteractionState is null)
            {
                MarkUnavailable("interaction_state");
            }

            if (inspection.BindingState is null)
            {
                MarkUnavailable("binding_diagnostics");
            }

            if (inspection.ValidationState is null)
            {
                MarkUnavailable("validation_diagnostics");
            }
        }
        else
        {
            MarkUnavailable("inspection");
        }

        string? screenshotPath = null;
        if (options.IncludeScreenshot)
        {
            if (resolvedTopLevelId is null)
            {
                MarkUnavailable("screenshot");
            }
            else
            {
                var path = Path.Combine(directory, "failure-screenshot.png");
                var capture = await context.BridgeClient.CaptureScreenshotAsync(
                    request.SessionId,
                    resolvedTopLevelId,
                    path,
                    cancellationToken);
                if (capture.Success)
                {
                    if (context.Policy is null)
                    {
                        screenshotPath = capture.Value!.FilePath;
                    }
                    else
                    {
                        var masked = await context.Policy.MaskScreenshotAsync(
                            context.BridgeClient,
                            request,
                            capture.Value!,
                            cancellationToken);
                        if (masked.Success)
                        {
                            screenshotPath = capture.Value!.FilePath;
                        }
                        else
                        {
                            AddUnavailable("screenshot", masked.Error!);
                        }
                    }
                }
                else
                {
                    AddUnavailable("screenshot", capture.Error!);
                }
            }
        }

        TreeResponse? visualTree = null;
        string? visualTreePath = null;
        if (options.IncludeVisualTree && resolvedTopLevelId is not null)
        {
            var tree = await context.BridgeClient.VisualTreeAsync(
                request.SessionId,
                resolvedTopLevelId,
                options.TreeDepth,
                cancellationToken);
            if (tree.Success)
            {
                visualTree = tree.Value;
                visualTreePath = WriteJson("visual_tree", "visual-tree.json", tree.Value!);
            }
            else
            {
                AddUnavailable("visual_tree", tree.Error!);
            }
        }
        else if (options.IncludeVisualTree)
        {
            MarkUnavailable("visual_tree");
        }

        string? candidatesPath = null;
        if (options.IncludeSelectorCandidates)
        {
            if (resolvedTopLevelId is null || selector is not { HasSearchCriteria: true })
            {
                MarkUnavailable("selector_candidates");
            }
            else if (!string.IsNullOrWhiteSpace(selector.BindingPath) || !string.IsNullOrWhiteSpace(selector.CommandName))
            {
                if (visualTree is null)
                {
                    var tree = await context.BridgeClient.VisualTreeAsync(
                        request.SessionId,
                        resolvedTopLevelId,
                        options.TreeDepth,
                        cancellationToken);
                    if (tree.Success)
                    {
                        visualTree = tree.Value;
                    }
                    else
                    {
                        AddUnavailable("selector_candidates", tree.Error!);
                    }
                }

                if (visualTree is not null)
                {
                    var candidates = EnumerateNodes(visualTree.Root)
                        .Where(candidate => MatchesSourceMappedSelector(candidate, selector))
                        .Take(options.MaxSelectorCandidates)
                        .ToArray();
                    candidatesPath = WriteJson("selector_candidates", "selector-candidates.json", new
                    {
                        selector,
                        candidates,
                        truncated = candidates.Length == options.MaxSelectorCandidates
                    });
                }
            }
            else if (!string.IsNullOrWhiteSpace(selector.NodeId))
            {
                candidatesPath = WriteJson("selector_candidates", "selector-candidates.json", new
                {
                    selector,
                    candidates = inspection is null ? [] : new[] { inspection },
                    truncated = false
                });
            }
            else
            {
                var candidates = await context.BridgeClient.FindNodesAsync(
                    request.SessionId,
                    resolvedTopLevelId,
                    selector.TreeKind,
                    selector.NodeType ?? selector.Role,
                    selector.Name,
                    selector.AutomationId,
                    selector.Text,
                    selector.MaxDepth ?? options.TreeDepth,
                    options.MaxSelectorCandidates,
                    cancellationToken,
                    visible: selector.Visible,
                    enabled: selector.Enabled,
                    rendered: selector.Rendered,
                    actionable: selector.Actionable);
                if (candidates.Success)
                {
                    candidatesPath = WriteJson("selector_candidates", "selector-candidates.json", candidates.Value!);
                }
                else
                {
                    AddUnavailable("selector_candidates", candidates.Error!);
                }
            }
        }

        string? topLevelsPath = null;
        if (options.IncludeActiveTopLevels)
        {
            var topLevels = await context.BridgeClient.ListTopLevelsAsync(request.SessionId, cancellationToken);
            if (topLevels.Success)
            {
                topLevelsPath = WriteJson("active_top_levels", "active-top-levels.json", topLevels.Value!);
            }
            else
            {
                AddUnavailable("active_top_levels", topLevels.Error!);
            }
        }

        var planIndex = context.Plan.Steps
            .Select((item, index) => (item, index))
            .FirstOrDefault(item => string.Equals(item.item.ExecutionPath, node.ExecutionPath, StringComparison.Ordinal))
            .index;
        var adjacentPlan = context.Plan.Steps
            .Skip(Math.Max(0, planIndex - 2))
            .Take(5)
            .ToArray();
        var contextPath = WriteJson("workflow_context", "workflow-context.json", new
        {
            requestId = request.RequestId,
            failedStep = failedResult,
            previousSteps = context.Results.TakeLast(2).ToArray(),
            adjacentPlan,
            diagnostics = failedResult.Diagnostics
        });

        var status = unavailable.Count == 0
            ? "captured"
            : unavailable.Count >= 5
                ? "unavailable"
                : "partial";
        return new SemanticWorkflowFailureEvidence(
            status,
            directory,
            inspectionPath,
            screenshotPath,
            visualTreePath,
            candidatesPath,
            topLevelsPath,
            contextPath,
            unavailable.Distinct(StringComparer.Ordinal).ToArray(),
            diagnostics);

        string? WriteJson<T>(string evidenceKind, string fileName, T value)
        {
            try
            {
                var path = Path.Combine(directory, fileName);
                var json = context.Policy is null
                    ? CoreResult<string>.Ok(JsonSerializer.Serialize(value, EvidenceJsonOptions))
                    : context.Policy.SanitizeJson(value);
                if (!json.Success)
                {
                    diagnostics.Add(ToProtocolError(json.Error!));
                    MarkUnavailable(evidenceKind);
                    return null;
                }

                File.WriteAllText(path, json.Value!, Encoding.UTF8);
                return path;
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException or NotSupportedException)
            {
                diagnostics.Add(new ProtocolError(
                    "semantic_workflow_failure_evidence_artifact_unavailable",
                    $"Failure evidence '{evidenceKind}' could not be written: {exception.Message}"));
                MarkUnavailable(evidenceKind);
                return null;
            }
        }

        void AddUnavailable(string evidenceKind, CoreError error)
        {
            diagnostics.Add(ToProtocolError(error));
            MarkUnavailable(evidenceKind);
        }

        void MarkUnavailable(string evidenceKind)
        {
            unavailable.Add(evidenceKind);
        }
    }

    private static SemanticWorkflowStepResult WithVerification(
        SemanticWorkflowStepResult result,
        SemanticWorkflowVerificationResult verification)
    {
        var failed = string.Equals(verification.Status, "failed", StringComparison.Ordinal);
        return new SemanticWorkflowStepResult(
            result.StepId,
            result.Action,
            failed ? "failed" : result.Status,
            failed ? $"Action completed but verification failed: {verification.Diagnostics.FirstOrDefault()?.Message ?? "postcondition did not match"}" : result.Message,
            result.ExecutedAt,
            result.Target,
            result.Input,
            result.Inspection,
            result.Screenshot,
            failed ? result.Diagnostics.Concat(verification.Diagnostics).ToArray() : result.Diagnostics,
            result.Metadata,
            result.Picker,
            result.Mutation,
            result.CustomActions,
            result.CustomAction,
            result.WaitObservation,
            result.TopLevelAlias,
            result.ResolvedTopLevelId,
            result.ExecutionPath,
            result.ParentStepId,
            result.Attempt,
            result.SourceFragment,
            verification,
            result.FailureEvidence);
    }

    private static SemanticWorkflowStepResult WithFailureEvidence(
        SemanticWorkflowStepResult result,
        SemanticWorkflowFailureEvidence failureEvidence)
    {
        return new SemanticWorkflowStepResult(
            result.StepId,
            result.Action,
            result.Status,
            result.Message,
            result.ExecutedAt,
            result.Target,
            result.Input,
            result.Inspection,
            result.Screenshot,
            result.Diagnostics.Concat(failureEvidence.Diagnostics).ToArray(),
            result.Metadata,
            result.Picker,
            result.Mutation,
            result.CustomActions,
            result.CustomAction,
            result.WaitObservation,
            result.TopLevelAlias,
            result.ResolvedTopLevelId,
            result.ExecutionPath,
            result.ParentStepId,
            result.Attempt,
            result.SourceFragment,
            result.Verification,
            failureEvidence);
    }

    private static string ResolveArtifactRoot(SemanticWorkflowRequest request) =>
        request.OutputDirectory
        ?? request.Evidence?.ReportDirectory
        ?? Path.Combine(Path.GetTempPath(), "AvaScope", "workflows", request.RequestId);

    private static IEnumerable<SemanticWorkflowStep> EnumerateWorkflowSteps(SemanticWorkflowRequest request)
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

    private static bool AppendAudit(CompositionExecutionContext context, SemanticWorkflowStepResult result)
    {
        if (context.Policy is null)
        {
            return true;
        }

        var audit = context.Policy.AppendActionAudit(context.Request, result);
        if (audit.Success)
        {
            return true;
        }

        context.Diagnostics.Add(ToProtocolError(audit.Error!));
        if (context.PolicyMetadata is IDictionary<string, string> metadata)
        {
            metadata.Remove("actionAuditPath");
            metadata["actionAudit"] = "failed_closed";
        }

        return false;
    }

    private static SemanticWorkflowResponse RedactionFailureResponse(
        SemanticWorkflowResponse response,
        CoreError error)
    {
        return new SemanticWorkflowResponse(
            "redacted-request",
            new SessionId("redacted-session"),
            null,
            "failed",
            response.StartedAt,
            response.CompletedAt,
            [],
            response.IsolatedStateStatus,
            [ToProtocolError(error)],
            new Dictionary<string, string>
            {
                ["evidencePolicy"] = "failed_closed",
                ["storage"] = "local_filesystem",
                ["networkUpload"] = "disabled"
            });
    }

    private static string SafeArtifactName(string value)
    {
        var safe = new string(value
            .Select(static character => char.IsLetterOrDigit(character) || character is '.' or '_' or '-'
                ? character
                : '-')
            .ToArray())
            .Trim('-', '.');
        return string.IsNullOrWhiteSpace(safe) ? "workflow-step" : safe;
    }

    private static async Task<bool> ExecuteIfAsync(
        CompositionExecutionContext context,
        CompiledWorkflowNode node,
        int? attempt,
        CancellationToken cancellationToken)
    {
        var evaluation = await EvaluateCompositionConditionAsync(
            context.BridgeClient,
            context.Request,
            node.Step,
            cancellationToken);
        if (!evaluation.Success)
        {
            var failed = WithCompositionEvidence(evaluation.Result, node, attempt);
            context.Results.Add(failed);
            context.Diagnostics.AddRange(failed.Diagnostics);
            return false;
        }

        var branch = evaluation.Matched ? "then" : "else";
        var decision = CreateCompositionControlResult(
            node.Step,
            "passed",
            $"Condition evaluated to {evaluation.Matched.ToString().ToLowerInvariant()}; selected '{branch}'.",
            evaluation.Result,
            new Dictionary<string, string>
            {
                ["branch"] = branch,
                ["conditionMatched"] = evaluation.Matched.ToString().ToLowerInvariant()
            });
        context.Results.Add(WithCompositionEvidence(decision, node, attempt));

        if (evaluation.Matched)
        {
            if (!await ExecuteNodesAsync(context, node.Primary, attempt, cancellationToken))
            {
                return false;
            }

            AppendSkipped(context, node.Alternate, "if_else_not_selected", attempt);
        }
        else
        {
            AppendSkipped(context, node.Primary, "if_then_not_selected", attempt);
            if (!await ExecuteNodesAsync(context, node.Alternate, attempt, cancellationToken))
            {
                return false;
            }
        }

        return true;
    }

    private static async Task<bool> ExecuteRetryUntilAsync(
        CompositionExecutionContext context,
        CompiledWorkflowNode node,
        CancellationToken cancellationToken)
    {
        var maximumAttempts = node.Step.MaxAttempts!.Value;
        for (var attempt = 1; attempt <= maximumAttempts; attempt++)
        {
            if (!await ExecuteNodesAsync(context, node.Primary, attempt, cancellationToken))
            {
                return false;
            }

            var evaluation = await EvaluateCompositionConditionAsync(
                context.BridgeClient,
                context.Request,
                node.Step,
                cancellationToken);
            if (!evaluation.Success)
            {
                var failed = WithCompositionEvidence(evaluation.Result, node, attempt);
                context.Results.Add(failed);
                context.Diagnostics.AddRange(failed.Diagnostics);
                return false;
            }

            var finalAttempt = attempt == maximumAttempts;
            var status = evaluation.Matched ? "passed" : finalAttempt ? "failed" : "retried";
            var metadata = new Dictionary<string, string>
            {
                ["attempt"] = attempt.ToString(CultureInfo.InvariantCulture),
                ["maxAttempts"] = maximumAttempts.ToString(CultureInfo.InvariantCulture),
                ["conditionMatched"] = evaluation.Matched.ToString().ToLowerInvariant(),
                ["retryDelayMs"] = (node.Step.RetryDelayMs ?? 0).ToString(CultureInfo.InvariantCulture)
            };
            var message = evaluation.Matched
                ? $"Retry condition matched on attempt {attempt.ToString(CultureInfo.InvariantCulture)}."
                : finalAttempt
                    ? $"Retry condition did not match within {maximumAttempts.ToString(CultureInfo.InvariantCulture)} attempts."
                    : $"Retry condition did not match on attempt {attempt.ToString(CultureInfo.InvariantCulture)}; retrying.";
            IReadOnlyList<ProtocolError>? diagnostics = null;
            if (status == "failed")
            {
                diagnostics =
                [
                    new ProtocolError(
                        "semantic_workflow_retry_exhausted",
                        message,
                        new Dictionary<string, string>(metadata, StringComparer.Ordinal)
                        {
                            ["executionPath"] = node.ExecutionPath,
                            ["nextAction"] = "Inspect the final typed observation and adjust the operation, condition, or maxAttempts."
                        })
                ];
            }

            var retryResult = CreateCompositionControlResult(
                node.Step,
                status,
                message,
                evaluation.Result,
                metadata,
                diagnostics);
            retryResult = WithCompositionEvidence(retryResult, node, attempt);
            context.Results.Add(retryResult);
            if (evaluation.Matched)
            {
                return true;
            }

            if (finalAttempt)
            {
                context.Diagnostics.AddRange(retryResult.Diagnostics);
                return false;
            }

            var retryDelayMs = node.Step.RetryDelayMs ?? 0;
            if (retryDelayMs > 0)
            {
                await Task.Delay(retryDelayMs, cancellationToken);
            }
        }

        return false;
    }

    private static async Task<bool> ExecuteFragmentAsync(
        CompositionExecutionContext context,
        CompiledWorkflowNode node,
        int? attempt,
        CancellationToken cancellationToken)
    {
        var result = new SemanticWorkflowStepResult(
            node.Step.Id,
            node.Step.Action,
            "passed",
            $"Expanded workflow fragment '{node.Step.Fragment}'.",
            DateTimeOffset.UtcNow,
            metadata: new Dictionary<string, string>
            {
                ["fragment"] = node.Step.Fragment!,
                ["expandedSteps"] = node.Primary.Count.ToString(CultureInfo.InvariantCulture)
            });
        context.Results.Add(WithCompositionEvidence(result, node, attempt));
        return await ExecuteNodesAsync(context, node.Primary, attempt, cancellationToken);
    }

    private static async Task<CompositionConditionEvaluation> EvaluateCompositionConditionAsync(
        LocalBridgeClient bridgeClient,
        SemanticWorkflowRequest request,
        SemanticWorkflowStep step,
        CancellationToken cancellationToken)
    {
        var probe = new SemanticWorkflowStep(
            step.Action,
            step.Id,
            step.Selector,
            timeoutMs: step.TimeoutMs ?? 1000,
            pollIntervalMs: step.PollIntervalMs ?? 25,
            waitCondition: step.WaitCondition,
            topLevelAlias: step.TopLevelAlias);
        var result = await WaitForConditionAsync(
            bridgeClient,
            request,
            probe,
            step.WaitCondition!,
            cancellationToken,
            singleObservation: true);
        var observation = result.WaitObservation;
        var lastErrorCode = result.Metadata.TryGetValue("lastErrorCode", out var errorCode)
            ? errorCode
            : null;
        var benignMissing = lastErrorCode is null
            || lastErrorCode is "node_not_found" or "top_level_not_found" or "semantic_workflow_top_level_alias_missing";
        var success = observation is not null
            && !string.Equals(observation.Availability, "unavailable", StringComparison.Ordinal)
            && benignMissing;
        if (success)
        {
            return new CompositionConditionEvaluation(true, observation!.Matched, result);
        }

        var diagnostic = result.Diagnostics.FirstOrDefault()
            ?? new ProtocolError(
                "semantic_workflow_condition_unavailable",
                "Workflow composition condition could not be evaluated from typed runtime state.");
        var failed = new SemanticWorkflowStepResult(
            step.Id,
            step.Action,
            "failed",
            "Workflow composition condition could not be evaluated.",
            DateTimeOffset.UtcNow,
            result.Target,
            inspection: result.Inspection,
            diagnostics: [diagnostic],
            metadata: result.Metadata,
            waitObservation: observation,
            topLevelAlias: result.TopLevelAlias,
            resolvedTopLevelId: result.ResolvedTopLevelId);
        return new CompositionConditionEvaluation(false, false, failed);
    }

    private static SemanticWorkflowStepResult CreateCompositionControlResult(
        SemanticWorkflowStep step,
        string status,
        string message,
        SemanticWorkflowStepResult evaluation,
        IReadOnlyDictionary<string, string> metadata,
        IReadOnlyList<ProtocolError>? diagnostics = null)
    {
        var combinedMetadata = new Dictionary<string, string>(evaluation.Metadata, StringComparer.Ordinal);
        foreach (var pair in metadata)
        {
            combinedMetadata[pair.Key] = pair.Value;
        }

        return new SemanticWorkflowStepResult(
            step.Id,
            step.Action,
            status,
            message,
            DateTimeOffset.UtcNow,
            evaluation.Target,
            inspection: evaluation.Inspection,
            diagnostics: diagnostics,
            metadata: combinedMetadata,
            waitObservation: evaluation.WaitObservation,
            topLevelAlias: evaluation.TopLevelAlias,
            resolvedTopLevelId: evaluation.ResolvedTopLevelId);
    }

    private static void AppendSkipped(
        CompositionExecutionContext context,
        IReadOnlyList<CompiledWorkflowNode> nodes,
        string reason,
        int? attempt)
    {
        foreach (var node in nodes)
        {
            var result = new SemanticWorkflowStepResult(
                node.Step.Id,
                node.Step.Action,
                "skipped",
                "Workflow branch was not selected.",
                DateTimeOffset.UtcNow,
                metadata: new Dictionary<string, string> { ["skipReason"] = reason });
            context.Results.Add(WithCompositionEvidence(result, node, attempt));
            AppendSkipped(context, node.Primary, reason, attempt);
            AppendSkipped(context, node.Alternate, reason, attempt);
        }
    }

    private static SemanticWorkflowStepResult AsOptionalSkipped(SemanticWorkflowStepResult result)
    {
        var metadata = new Dictionary<string, string>(result.Metadata, StringComparer.Ordinal)
        {
            ["optional"] = "true",
            ["originalStatus"] = result.Status,
            ["skipReason"] = "optional_step_failed"
        };
        return new SemanticWorkflowStepResult(
            result.StepId,
            result.Action,
            "skipped",
            $"Optional step skipped after failure: {result.Message}",
            result.ExecutedAt,
            result.Target,
            result.Input,
            result.Inspection,
            result.Screenshot,
            result.Diagnostics,
            metadata,
            result.Picker,
            result.Mutation,
            result.CustomActions,
            result.CustomAction,
            result.WaitObservation,
            result.TopLevelAlias,
            result.ResolvedTopLevelId,
            result.ExecutionPath,
            result.ParentStepId,
            result.Attempt,
            result.SourceFragment,
            result.Verification,
            result.FailureEvidence);
    }

    private static SemanticWorkflowStepResult WithCompositionEvidence(
        SemanticWorkflowStepResult result,
        CompiledWorkflowNode node,
        int? attempt)
    {
        var metadata = new Dictionary<string, string>(result.Metadata, StringComparer.Ordinal)
        {
            ["executionPath"] = node.ExecutionPath,
            ["nestingDepth"] = CountNestingDepth(node.ExecutionPath).ToString(CultureInfo.InvariantCulture)
        };
        if (node.ParentStepId is not null)
        {
            metadata["parentStepId"] = node.ParentStepId;
        }

        if (node.Branch is not null)
        {
            metadata["branch"] = node.Branch;
        }

        if (attempt.HasValue)
        {
            metadata["attempt"] = attempt.Value.ToString(CultureInfo.InvariantCulture);
        }

        if (node.SourceFragment is not null)
        {
            metadata["sourceFragment"] = node.SourceFragment;
        }

        return new SemanticWorkflowStepResult(
            result.StepId,
            result.Action,
            result.Status,
            result.Message,
            result.ExecutedAt,
            result.Target,
            result.Input,
            result.Inspection,
            result.Screenshot,
            result.Diagnostics,
            metadata,
            result.Picker,
            result.Mutation,
            result.CustomActions,
            result.CustomAction,
            result.WaitObservation,
            result.TopLevelAlias,
            result.ResolvedTopLevelId,
            node.ExecutionPath,
            node.ParentStepId,
            attempt,
            node.SourceFragment,
            result.Verification,
            result.FailureEvidence);
    }

    private static int CountNestingDepth(string executionPath) =>
        executionPath.Count(static character => character == '/');

    private static async Task<SemanticWorkflowStepResult> ExecuteStepWithIdempotencyAsync(
        LocalBridgeClient bridgeClient,
        WorkflowIdempotencyStore store,
        SemanticWorkflowRequest request,
        SemanticWorkflowStep step,
        int stepIndex,
        RuntimeEvidencePolicyEnforcer? policy,
        CancellationToken cancellationToken,
        string? idempotencyScope = null)
    {
        if (string.IsNullOrWhiteSpace(step.IdempotencyKey))
        {
            return await ExecuteStepAsync(
                bridgeClient,
                request,
                step,
                stepIndex,
                policy,
                cancellationToken);
        }

        var effectiveIdempotencyKey = string.IsNullOrWhiteSpace(idempotencyScope)
            ? step.IdempotencyKey
            : $"{step.IdempotencyKey}@{Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(idempotencyScope)))[..16].ToLowerInvariant()}";
        var lease = await store.AcquireAsync(
            request.SessionId,
            effectiveIdempotencyKey,
            cancellationToken);
        if (!lease.Success)
        {
            return Fail(step, lease.Error!);
        }

        using var idempotencyLease = lease.Value!;
        var signature = WorkflowIdempotencyStore.CreateSignature(request, step);
        var replay = store.TryReplay(request.SessionId, effectiveIdempotencyKey, signature);
        if (!replay.Success)
        {
            return Fail(step, replay.Error!);
        }

        if (replay.Value is not null)
        {
            return replay.Value;
        }

        var result = await ExecuteStepAsync(
            bridgeClient,
            request,
            step,
            stepIndex,
            policy,
            cancellationToken);
        if (policy is not null)
        {
            var sanitized = policy.Sanitize(result);
            if (!sanitized.Success)
            {
                return Fail(step, sanitized.Error!, result.Target);
            }

            result = sanitized.Value!;
        }

        var save = store.Save(
            request.SessionId,
            effectiveIdempotencyKey,
            signature,
            TimeSpan.FromMilliseconds(step.IdempotencyTtlMs ?? 300_000),
            result);
        return save.Success ? result : Fail(step, save.Error!, result.Target);
    }

    private static async Task<SemanticWorkflowStepResult> ExecuteStepAsync(
        LocalBridgeClient bridgeClient,
        SemanticWorkflowRequest request,
        SemanticWorkflowStep step,
        int stepIndex,
        RuntimeEvidencePolicyEnforcer? policy,
        CancellationToken cancellationToken)
    {
        try
        {
            var effectiveRequest = request;
            string? resolvedTopLevelId = null;
            var deferredAliasResolution = step.TopLevelAlias is not null
                && step.Action is SemanticWorkflowActions.WaitForNode or SemanticWorkflowActions.WaitForState;
            if (step.TopLevelAlias is not null && !deferredAliasResolution)
            {
                var topLevel = await ResolveTopLevelAliasAsync(
                    bridgeClient,
                    request,
                    step.TopLevelAlias,
                    cancellationToken);
                if (!topLevel.Success)
                {
                    return WithTopLevelEvidence(
                        Fail(step, topLevel.Error!),
                        step.TopLevelAlias,
                        resolvedTopLevelId);
                }

                resolvedTopLevelId = topLevel.Value!.Summary.Id;
                effectiveRequest = WithTopLevelId(request, resolvedTopLevelId);
            }
            else if (step.TopLevelAlias is null && string.IsNullOrWhiteSpace(request.TopLevelId))
            {
                return Fail(
                    step,
                    "semantic_workflow_top_level_required",
                    "Workflow steps without topLevelAlias require the request topLevelId.");
            }

            var result = step.Action switch
            {
                SemanticWorkflowActions.Wait => await WaitAsync(step, cancellationToken),
                SemanticWorkflowActions.WaitForNode => await WaitForNodeAsync(bridgeClient, effectiveRequest, step, cancellationToken),
                SemanticWorkflowActions.WaitForState => await WaitForStateAsync(bridgeClient, effectiveRequest, step, cancellationToken),
                SemanticWorkflowActions.WaitForDialog => await WaitForDialogAsync(bridgeClient, effectiveRequest, step, cancellationToken),
                SemanticWorkflowActions.ValidateAction => await ValidateActionAsync(bridgeClient, effectiveRequest, step, cancellationToken),
                SemanticWorkflowActions.ValidateMutation => await ValidateMutationAsync(bridgeClient, effectiveRequest, step, cancellationToken),
                SemanticWorkflowActions.Screenshot => await ScreenshotAsync(bridgeClient, effectiveRequest, step, stepIndex, policy, cancellationToken),
                SemanticWorkflowActions.Inspect => await InspectAsync(bridgeClient, effectiveRequest, step, cancellationToken),
                SemanticWorkflowActions.AssertState => await AssertStateAsync(bridgeClient, effectiveRequest, step, cancellationToken),
                SemanticWorkflowActions.PickerResult => ConsumePickerResult(bridgeClient, effectiveRequest, step),
                SemanticWorkflowActions.Click => await InputAsync(bridgeClient, effectiveRequest, step, InputActions.Click, policy, cancellationToken),
                SemanticWorkflowActions.TypeText => await InputAsync(bridgeClient, effectiveRequest, step, InputActions.KeyText, policy, cancellationToken),
                SemanticWorkflowActions.ClearText => await InputAsync(bridgeClient, effectiveRequest, step, InputActions.ClearText, policy, cancellationToken),
                SemanticWorkflowActions.Focus => await InputAsync(bridgeClient, effectiveRequest, step, InputActions.Focus, policy, cancellationToken),
                SemanticWorkflowActions.Invoke => await InputAsync(bridgeClient, effectiveRequest, step, InputActions.Invoke, policy, cancellationToken),
                SemanticWorkflowActions.Select => await InputAsync(bridgeClient, effectiveRequest, step, InputActions.Select, policy, cancellationToken),
                SemanticWorkflowActions.Toggle => await InputAsync(bridgeClient, effectiveRequest, step, InputActions.Toggle, policy, cancellationToken),
                SemanticWorkflowActions.Expand => await InputAsync(bridgeClient, effectiveRequest, step, InputActions.Expand, policy, cancellationToken),
                SemanticWorkflowActions.Collapse => await InputAsync(bridgeClient, effectiveRequest, step, InputActions.Collapse, policy, cancellationToken),
                SemanticWorkflowActions.KeyDown => await InputAsync(bridgeClient, effectiveRequest, step, InputActions.KeyDown, policy, cancellationToken),
                SemanticWorkflowActions.KeyUp => await InputAsync(bridgeClient, effectiveRequest, step, InputActions.KeyUp, policy, cancellationToken),
                SemanticWorkflowActions.Drag => await InputAsync(bridgeClient, effectiveRequest, step, InputActions.Drag, policy, cancellationToken),
                SemanticWorkflowActions.Swipe => await InputAsync(bridgeClient, effectiveRequest, step, InputActions.Swipe, policy, cancellationToken),
                SemanticWorkflowActions.LongPress => await InputAsync(bridgeClient, effectiveRequest, step, InputActions.LongPress, policy, cancellationToken),
                SemanticWorkflowActions.PressAndHold => await InputAsync(bridgeClient, effectiveRequest, step, InputActions.PressAndHold, policy, cancellationToken),
                SemanticWorkflowActions.CustomActions => await CustomActionsAsync(bridgeClient, effectiveRequest, step, cancellationToken),
                SemanticWorkflowActions.CustomAction => await CustomActionAsync(bridgeClient, effectiveRequest, step, policy, cancellationToken),
                _ => Fail(step, "semantic_workflow_action_not_supported", $"Workflow action '{step.Action}' is not supported.")
            };
            return step.TopLevelAlias is null
                ? result
                : WithTopLevelEvidence(
                    result,
                    step.TopLevelAlias,
                    result.ResolvedTopLevelId ?? resolvedTopLevelId);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException)
        {
            return Fail(step, "semantic_workflow_step_failed", exception.Message);
        }
    }

    private static async Task<SemanticWorkflowStepResult> WaitAsync(SemanticWorkflowStep step, CancellationToken cancellationToken)
    {
        var waitMs = step.WaitMs ?? 0;
        if (waitMs > 0)
        {
            await Task.Delay(waitMs, cancellationToken);
        }

        return Pass(
            step,
            $"Waited {waitMs.ToString(CultureInfo.InvariantCulture)} ms.",
            metadata: new Dictionary<string, string>
            {
                ["waitMs"] = waitMs.ToString(CultureInfo.InvariantCulture)
            });
    }

    private static async Task<SemanticWorkflowStepResult> WaitForNodeAsync(
        LocalBridgeClient bridgeClient,
        SemanticWorkflowRequest request,
        SemanticWorkflowStep step,
        CancellationToken cancellationToken)
    {
        var condition = step.WaitCondition
            ?? new SemanticWaitCondition(SemanticWaitConditionKinds.Exists);
        if (condition.Kind is not SemanticWaitConditionKinds.Exists
            and not SemanticWaitConditionKinds.Disappears)
        {
            return Fail(
                step,
                "semantic_workflow_wait_condition_not_supported",
                "wait_for_node supports only exists or disappears conditions.");
        }

        if (step.Selector is null || !step.Selector.HasSearchCriteria)
        {
            return Fail(step, "semantic_workflow_selector_required", "wait_for_node requires a selector.");
        }

        return await WaitForConditionAsync(bridgeClient, request, step, condition, cancellationToken);
    }

    private static async Task<SemanticWorkflowStepResult> WaitForStateAsync(
        LocalBridgeClient bridgeClient,
        SemanticWorkflowRequest request,
        SemanticWorkflowStep step,
        CancellationToken cancellationToken)
    {
        var condition = step.WaitCondition;
        if (condition is null && string.IsNullOrWhiteSpace(step.AssertProperty))
        {
            return Fail(
                step,
                "semantic_workflow_wait_condition_required",
                "wait_for_state requires waitCondition or the compatible assertProperty field.");
        }

        condition ??= new SemanticWaitCondition(
            SemanticWaitConditionKinds.Value,
            step.Expected,
            propertyName: step.AssertProperty);
        var topLevelCondition = condition.Kind is SemanticWaitConditionKinds.TopLevelOpened
            or SemanticWaitConditionKinds.TopLevelClosed;
        if (!topLevelCondition && (step.Selector is null || !step.Selector.HasSearchCriteria))
        {
            return Fail(step, "semantic_workflow_selector_required", "wait_for_state requires a selector.");
        }

        return await WaitForConditionAsync(bridgeClient, request, step, condition, cancellationToken);
    }

    private static async Task<SemanticWorkflowStepResult> WaitForConditionAsync(
        LocalBridgeClient bridgeClient,
        SemanticWorkflowRequest request,
        SemanticWorkflowStep step,
        SemanticWaitCondition condition,
        CancellationToken cancellationToken,
        bool singleObservation = false)
    {
        var timeoutMs = step.TimeoutMs ?? 5_000;
        var pollMs = step.PollIntervalMs ?? 100;
        var stopwatch = Stopwatch.StartNew();
        var attempts = 0;
        CoreError? lastError = null;
        InspectNodeResponse? lastInspection = null;
        RuntimeTargetContext? lastTarget = null;
        RuntimeWaitObservation? lastObservation = null;
        var baseline = condition.Baseline;
        var baselineCaptured = condition.Baseline is not null;
        var sawAvailable = false;
        string? lastResolvedTopLevelId = null;

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            attempts++;
            using var attemptCancellation = CreateWaitAttemptCancellation(
                stopwatch,
                timeoutMs,
                cancellationToken);
            try
            {
                var pollRequest = request;
                CoreResult<ResolvedWorkflowTopLevel>? aliasResolution = null;
                if (step.TopLevelAlias is not null)
                {
                    aliasResolution = await ResolveTopLevelAliasAsync(
                        bridgeClient,
                        request,
                        step.TopLevelAlias,
                        attemptCancellation.Token);
                    if (aliasResolution.Success)
                    {
                        lastResolvedTopLevelId = aliasResolution.Value!.Summary.Id;
                        pollRequest = WithTopLevelId(request, lastResolvedTopLevelId);
                    }
                }

                if (condition.Kind is SemanticWaitConditionKinds.TopLevelOpened
                    or SemanticWaitConditionKinds.TopLevelClosed)
                {
                    if (aliasResolution is not null)
                    {
                        var opened = aliasResolution.Success;
                        var missing = !opened && IsMissingTopLevelAlias(aliasResolution.Error);
                        var shouldBeOpen = condition.Kind == SemanticWaitConditionKinds.TopLevelOpened;
                        lastTarget = !opened
                            ? null
                            : new RuntimeTargetContext(
                                request.SessionId,
                                aliasResolution.Value!.Summary.Id,
                                capturedAt: DateTimeOffset.UtcNow);
                        lastError = aliasResolution.Error;
                        lastObservation = new RuntimeWaitObservation(
                            condition.Kind,
                            opened ? "available" : missing ? "missing" : "unavailable",
                            opened == shouldBeOpen && (opened || missing),
                            DateTimeOffset.UtcNow,
                            opened.ToString().ToLowerInvariant(),
                            typeof(bool).FullName!,
                            condition.Comparison,
                            shouldBeOpen.ToString().ToLowerInvariant(),
                            source: "top_level_alias",
                            message: aliasResolution.Error?.Message);
                        sawAvailable |= opened || missing;
                    }
                    else
                    {
                        var topLevels = await bridgeClient.ListTopLevelsAsync(
                            request.SessionId,
                            attemptCancellation.Token);
                        if (topLevels.Success)
                        {
                            var topLevelId = condition.TopLevelId
                                ?? (string.IsNullOrWhiteSpace(condition.TopLevelTitle) ? request.TopLevelId : null);
                            var match = topLevels.Value!.TopLevels.FirstOrDefault(topLevel =>
                                (string.IsNullOrWhiteSpace(topLevelId)
                                    || string.Equals(topLevel.Id, topLevelId, StringComparison.Ordinal))
                                && (string.IsNullOrWhiteSpace(condition.TopLevelTitle)
                                    || string.Equals(topLevel.Title, condition.TopLevelTitle, StringComparison.Ordinal)));
                            var opened = match is not null;
                            var shouldBeOpen = condition.Kind == SemanticWaitConditionKinds.TopLevelOpened;
                            lastTarget = match is null
                                ? null
                                : new RuntimeTargetContext(request.SessionId, match.Id, capturedAt: DateTimeOffset.UtcNow);
                            lastObservation = new RuntimeWaitObservation(
                                condition.Kind,
                                "available",
                                opened == shouldBeOpen,
                                DateTimeOffset.UtcNow,
                                opened.ToString().ToLowerInvariant(),
                                typeof(bool).FullName!,
                                condition.Comparison,
                                shouldBeOpen.ToString().ToLowerInvariant(),
                                source: "list_top_levels");
                            sawAvailable = true;
                            lastError = null;
                        }
                        else
                        {
                            lastError = topLevels.Error;
                        }
                    }
                }
                else if (aliasResolution is { Success: false })
                {
                    lastError = aliasResolution.Error;
                    lastTarget = null;
                    lastObservation = new RuntimeWaitObservation(
                        condition.Kind,
                        IsMissingTopLevelAlias(aliasResolution.Error) ? "missing" : "unavailable",
                        matched: false,
                        DateTimeOffset.UtcNow,
                        comparison: condition.Comparison,
                        expected: GetExpectedValue(condition),
                        baseline: baseline,
                        source: "top_level_alias",
                        message: aliasResolution.Error?.Message);
                }
                else
                {
                    var target = await ResolveTargetAsync(
                        bridgeClient,
                        pollRequest,
                        step,
                        attemptCancellation.Token);
                    if (condition.Kind is SemanticWaitConditionKinds.Exists
                        or SemanticWaitConditionKinds.Disappears)
                    {
                        var missing = !target.Success && IsMissingTarget(target.Error);
                        var exists = target.Success;
                        var matched = condition.Kind == SemanticWaitConditionKinds.Exists
                            ? exists
                            : missing;
                        lastTarget = target.Value?.Target;
                        lastError = target.Error;
                        lastObservation = new RuntimeWaitObservation(
                            condition.Kind,
                            exists ? "available" : missing ? "missing" : "unavailable",
                            matched,
                            DateTimeOffset.UtcNow,
                            exists.ToString().ToLowerInvariant(),
                            typeof(bool).FullName!,
                            condition.Comparison,
                            (condition.Kind == SemanticWaitConditionKinds.Exists).ToString().ToLowerInvariant(),
                            source: "selector_resolution",
                            message: target.Error?.Message);
                        sawAvailable |= exists || missing;
                    }
                    else if (target.Success)
                    {
                        lastError = null;
                        lastTarget = target.Value!.Target;
                        var inspect = await bridgeClient.InspectNodeAsync(
                            pollRequest.SessionId,
                            pollRequest.TopLevelId!,
                            lastTarget.TreeKind ?? TreeKinds.Visual,
                            lastTarget.NodeId!,
                            attemptCancellation.Token);
                        if (inspect.Success)
                        {
                            lastError = null;
                            lastInspection = inspect.Value;
                            var observed = ObserveNodeCondition(inspect.Value!, condition);
                            if (observed.Available
                                && condition.Comparison == SemanticWaitComparisons.Changed
                                && !baselineCaptured)
                            {
                                baseline = observed.Value;
                                baselineCaptured = true;
                            }

                            var compared = CompareObservation(observed, condition, baseline, baselineCaptured);
                            lastObservation = new RuntimeWaitObservation(
                                condition.Kind,
                                compared.Available ? "available" : "unavailable",
                                compared.Matched,
                                DateTimeOffset.UtcNow,
                                observed.Value,
                                observed.ValueType,
                                condition.Comparison,
                                GetExpectedValue(condition),
                                baseline,
                                observed.Source,
                                compared.Message ?? observed.Message);
                            sawAvailable |= compared.Available;
                        }
                        else
                        {
                            lastError = inspect.Error;
                            lastObservation = MissingObservation(condition, inspect.Error?.Message);
                        }
                    }
                    else
                    {
                        lastError = target.Error;
                        lastObservation = MissingObservation(condition, target.Error?.Message);
                    }
                }
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                return WithTopLevelEvidence(WaitConditionFailure(
                    step,
                    condition,
                    timeoutMs,
                    pollMs,
                    attempts,
                    stopwatch.ElapsedMilliseconds,
                    lastTarget,
                    lastInspection,
                    lastObservation,
                    lastError,
                    sawAvailable), step.TopLevelAlias, lastResolvedTopLevelId);
            }

            if (lastObservation?.Matched == true)
            {
                var metadata = CreateWaitMetadata(
                    timeoutMs,
                    pollMs,
                    attempts,
                    stopwatch.ElapsedMilliseconds);
                CopyObservation(metadata, lastObservation);
                return WithTopLevelEvidence(Pass(
                    step,
                    $"Wait condition '{condition.Kind}' matched after {attempts.ToString(CultureInfo.InvariantCulture)} attempt(s).",
                    lastTarget,
                    inspection: lastInspection,
                    metadata: metadata,
                    waitObservation: lastObservation), step.TopLevelAlias, lastResolvedTopLevelId);
            }

            if (singleObservation)
            {
                return WithTopLevelEvidence(WaitConditionFailure(
                    step,
                    condition,
                    timeoutMs,
                    pollMs,
                    attempts,
                    stopwatch.ElapsedMilliseconds,
                    lastTarget,
                    lastInspection,
                    lastObservation,
                    lastError,
                    sawAvailable), step.TopLevelAlias, lastResolvedTopLevelId);
            }

            if (stopwatch.ElapsedMilliseconds >= timeoutMs)
            {
                return WithTopLevelEvidence(WaitConditionFailure(
                    step,
                    condition,
                    timeoutMs,
                    pollMs,
                    attempts,
                    stopwatch.ElapsedMilliseconds,
                    lastTarget,
                    lastInspection,
                    lastObservation,
                    lastError,
                    sawAvailable), step.TopLevelAlias, lastResolvedTopLevelId);
            }

            await DelayUntilNextPollAsync(stopwatch, timeoutMs, pollMs, cancellationToken);
        }
    }

    private static async Task<SemanticWorkflowStepResult> WaitForDialogAsync(
        LocalBridgeClient bridgeClient,
        SemanticWorkflowRequest request,
        SemanticWorkflowStep step,
        CancellationToken cancellationToken)
    {
        var timeoutMs = step.TimeoutMs ?? 5_000;
        var pollMs = step.PollIntervalMs ?? 100;
        var stopwatch = Stopwatch.StartNew();
        var attempts = 0;
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            attempts++;
            var picker = bridgeClient.NativePicker(
                request.SessionId,
                NativePickerOperations.Detect,
                timeoutMs: 0);
            if (!picker.Success)
            {
                return Fail(step, picker.Error!);
            }

            if (picker.Value!.DialogDetected)
            {
                return Pass(
                    step,
                    $"Native picker detected after {attempts.ToString(CultureInfo.InvariantCulture)} attempt(s).",
                    metadata: CreateWaitMetadata(timeoutMs, pollMs, attempts, stopwatch.ElapsedMilliseconds),
                    picker: picker.Value);
            }

            if (stopwatch.ElapsedMilliseconds >= timeoutMs)
            {
                var metadata = CreateWaitMetadata(
                    timeoutMs,
                    pollMs,
                    attempts,
                    stopwatch.ElapsedMilliseconds);
                metadata["pickerStatus"] = picker.Value.Status;
                return new SemanticWorkflowStepResult(
                    step.Id,
                    step.Action,
                    "failed",
                    "Timed out waiting for a native file or folder picker.",
                    DateTimeOffset.UtcNow,
                    diagnostics:
                    [
                        new ProtocolError(
                            "semantic_workflow_wait_timeout",
                            "Timed out waiting for a native file or folder picker.",
                            metadata)
                    ],
                    metadata: metadata,
                    picker: picker.Value);
            }

            await DelayUntilNextPollAsync(stopwatch, timeoutMs, pollMs, cancellationToken);
        }
    }

    private static async Task<SemanticWorkflowStepResult> ValidateActionAsync(
        LocalBridgeClient bridgeClient,
        SemanticWorkflowRequest request,
        SemanticWorkflowStep step,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(step.InputAction))
        {
            return Fail(step, "semantic_workflow_input_action_required", "validate_action requires inputAction.");
        }

        var target = await ResolveTargetAsync(bridgeClient, request, step, cancellationToken);
        if (!target.Success)
        {
            return Fail(step, target.Error!);
        }

        var destination = await ResolveDestinationAsync(bridgeClient, request, step, cancellationToken);
        if (!destination.Success)
        {
            return Fail(step, destination.Error!, target.Value!.Target);
        }

        if (LooksDestructive(step, target.Value!)
            && !request.AllowDestructive
            && string.IsNullOrWhiteSpace(request.IsolatedStateDirectory))
        {
            return Fail(
                step,
                "semantic_workflow_destructive_target_requires_isolation",
                "The selected target looks destructive; provide isolatedStateDirectory or set allowDestructive explicitly.",
                target.Value!.Target);
        }

        var validation = await bridgeClient.ValidateInputAsync(
            request.SessionId,
            request.TopLevelId!,
            step.InputAction,
            inputText: step.Text,
            targetNodeId: target.Value!.Target.NodeId,
            inputKey: step.Key,
            keyModifiers: step.Modifiers,
            gesture: CreateGestureOptions(step, destination.Value),
            cancellationToken: cancellationToken,
            inputTarget: target.Value.Target,
            gestureDestinationTarget: destination.Value?.Target);
        if (!validation.Success && IsPreDispatchStale(validation.Error!))
        {
            target = await ResolveTargetAsync(bridgeClient, request, step, cancellationToken);
            if (!target.Success)
            {
                return Fail(step, target.Error!);
            }

            destination = await ResolveDestinationAsync(bridgeClient, request, step, cancellationToken);
            if (!destination.Success)
            {
                return Fail(step, destination.Error!, target.Value!.Target);
            }

            validation = await bridgeClient.ValidateInputAsync(
                request.SessionId,
                request.TopLevelId!,
                step.InputAction,
                inputText: step.Text,
                targetNodeId: target.Value!.Target.NodeId,
                inputKey: step.Key,
                keyModifiers: step.Modifiers,
                gesture: CreateGestureOptions(step, destination.Value),
                cancellationToken: cancellationToken,
                inputTarget: target.Value.Target,
                gestureDestinationTarget: destination.Value?.Target);
        }
        return validation.Success
            ? Pass(
                step,
                $"Input action '{step.InputAction}' validated without execution.",
                target.Value.Target,
                validation.Value,
                metadata: validation.Value!.Metadata)
            : Fail(step, validation.Error!, target.Value.Target);
    }

    private static async Task<SemanticWorkflowStepResult> ValidateMutationAsync(
        LocalBridgeClient bridgeClient,
        SemanticWorkflowRequest request,
        SemanticWorkflowStep step,
        CancellationToken cancellationToken)
    {
        if (step.Mutation is null)
        {
            return Fail(step, "semantic_workflow_mutation_required", "validate_mutation requires mutation.");
        }

        var target = await ResolveTargetAsync(bridgeClient, request, step, cancellationToken);
        if (!target.Success)
        {
            return Fail(step, target.Error!);
        }

        var mutationRequest = new RuntimeMutationRequest(
            $"{request.RequestId}:{step.Id}:validation",
            target.Value!.Target,
            step.Mutation);
        var validation = await bridgeClient.ValidateMutationAsync(
            request.SessionId,
            mutationRequest,
            cancellationToken);
        if (!validation.Success)
        {
            return Fail(step, validation.Error!, target.Value.Target);
        }

        var response = validation.Value!;
        if (string.Equals(response.Status, RuntimeMutationStatuses.StaleTarget, StringComparison.Ordinal))
        {
            target = await ResolveTargetAsync(bridgeClient, request, step, cancellationToken);
            if (!target.Success)
            {
                return Fail(step, target.Error!);
            }

            mutationRequest = new RuntimeMutationRequest(
                mutationRequest.RequestId,
                target.Value!.Target,
                step.Mutation);
            validation = await bridgeClient.ValidateMutationAsync(
                request.SessionId,
                mutationRequest,
                cancellationToken);
            if (!validation.Success)
            {
                return Fail(step, validation.Error!, target.Value.Target);
            }

            response = validation.Value!;
        }

        if (!string.Equals(response.Status, RuntimeMutationStatuses.Validated, StringComparison.Ordinal))
        {
            var diagnostic = response.Diagnostics.FirstOrDefault()
                ?? new ProtocolError(
                    "semantic_workflow_mutation_validation_failed",
                    $"Mutation validation returned status '{response.Status}'.");
            return Fail(step, diagnostic, target.Value.Target, mutation: response);
        }

        return Pass(
            step,
            "Runtime mutation validated without applying it.",
            target.Value.Target,
            metadata: response.Metadata,
            mutation: response);
    }

    private static async Task DelayUntilNextPollAsync(
        Stopwatch stopwatch,
        int timeoutMs,
        int pollMs,
        CancellationToken cancellationToken)
    {
        var remaining = timeoutMs - stopwatch.ElapsedMilliseconds;
        if (remaining > 0)
        {
            await Task.Delay(
                (int)Math.Min(pollMs, remaining),
                cancellationToken);
        }
    }

    private static CancellationTokenSource CreateWaitAttemptCancellation(
        Stopwatch stopwatch,
        int timeoutMs,
        CancellationToken cancellationToken)
    {
        var source = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        source.CancelAfter((int)Math.Max(1, timeoutMs - stopwatch.ElapsedMilliseconds));
        return source;
    }

    private static SemanticWorkflowStepResult WaitConditionFailure(
        SemanticWorkflowStep step,
        SemanticWaitCondition condition,
        int timeoutMs,
        int pollMs,
        int attempts,
        long elapsedMs,
        RuntimeTargetContext? target,
        InspectNodeResponse? inspection,
        RuntimeWaitObservation? observation,
        CoreError? lastError,
        bool sawAvailable)
    {
        var unavailable = !sawAvailable
            && string.Equals(observation?.Availability, "unavailable", StringComparison.Ordinal)
            && !string.Equals(observation?.Source, "selector_resolution", StringComparison.Ordinal);
        var code = unavailable
            ? "semantic_workflow_wait_state_unavailable"
            : "semantic_workflow_wait_timeout";
        var message = unavailable
            ? $"Wait condition '{condition.Kind}' is unavailable for the selected runtime target."
            : $"Timed out waiting for condition '{condition.Kind}'.";
        var metadata = CreateWaitMetadata(timeoutMs, pollMs, attempts, elapsedMs);
        metadata["condition"] = condition.Kind;
        metadata["nextAction"] = unavailable
            ? "Choose a state exposed by this control or inspect its computed and binding properties."
            : "Inspect the last observation and candidate evidence, then adjust the selector, expected value, or timeout.";
        if (observation is not null)
        {
            CopyObservation(metadata, observation);
        }

        CopyLastError(metadata, lastError);
        return Fail(step, code, message, target, inspection, metadata, observation);
    }

    private static RuntimeWaitObservation MissingObservation(
        SemanticWaitCondition condition,
        string? message)
    {
        return new RuntimeWaitObservation(
            condition.Kind,
            "missing",
            matched: false,
            DateTimeOffset.UtcNow,
            comparison: condition.Comparison,
            expected: GetExpectedValue(condition),
            baseline: condition.Baseline,
            source: "selector_resolution",
            message: message);
    }

    private static bool IsMissingTarget(CoreError? error)
    {
        return error is not null
            && (string.Equals(error.Code, "node_not_found", StringComparison.Ordinal)
                || string.Equals(error.Code, "top_level_not_found", StringComparison.Ordinal)
                || error.Message.Contains("did not match any node", StringComparison.Ordinal));
    }

    private static ObservedWaitValue ObserveNodeCondition(
        InspectNodeResponse response,
        SemanticWaitCondition condition)
    {
        if (condition.Kind == SemanticWaitConditionKinds.BindingValue)
        {
            var bound = response.BindingState?.BoundProperties.FirstOrDefault(property =>
                (string.IsNullOrWhiteSpace(condition.BindingPath)
                    || string.Equals(property.BindingPath, condition.BindingPath, StringComparison.Ordinal))
                && (string.IsNullOrWhiteSpace(condition.PropertyName)
                    || string.Equals(property.PropertyName, condition.PropertyName, StringComparison.OrdinalIgnoreCase)));
            return bound is null || string.Equals(bound.ResolvedValueStatus, "not_available", StringComparison.Ordinal)
                ? ObservedWaitValue.Unavailable(
                    "binding",
                    "The requested binding value is not available on the selected node.")
                : ObservedWaitValue.FromProtocolValue(
                    bound.Value,
                    bound.ValueType,
                    $"binding:{bound.BindingPath}");
        }

        if (condition.Kind is SemanticWaitConditionKinds.Visible or SemanticWaitConditionKinds.Hidden)
        {
            return ObservedWaitValue.FromBoolean(response.InteractionState?.Visible, "interaction_state.visible");
        }

        if (condition.Kind is SemanticWaitConditionKinds.Enabled or SemanticWaitConditionKinds.Disabled)
        {
            return ObservedWaitValue.FromBoolean(response.InteractionState?.Enabled, "interaction_state.enabled");
        }

        if (condition.Kind == SemanticWaitConditionKinds.Rendered)
        {
            return ObservedWaitValue.FromBoolean(response.InteractionState?.Rendered, "interaction_state.rendered");
        }

        var propertyName = condition.PropertyName ?? condition.Kind switch
        {
            SemanticWaitConditionKinds.Checked or SemanticWaitConditionKinds.Unchecked => "IsChecked",
            SemanticWaitConditionKinds.SelectedValue => "SelectedValue",
            SemanticWaitConditionKinds.Text => "Text",
            SemanticWaitConditionKinds.Value => "Value",
            SemanticWaitConditionKinds.CommandExecutable => "CommandExecutable",
            _ => null
        };
        if (string.IsNullOrWhiteSpace(propertyName))
        {
            return ObservedWaitValue.Unavailable(
                "inspect_node",
                "The wait condition requires propertyName.");
        }

        if (string.Equals(propertyName, "text", StringComparison.OrdinalIgnoreCase)
            && !response.ComputedProperties.Any(property =>
                string.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase)))
        {
            return new ObservedWaitValue(
                true,
                response.Text,
                typeof(string).FullName!,
                "inspect_node.text");
        }

        var computed = response.ComputedProperties.FirstOrDefault(property =>
            string.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase));
        return computed is null
            ? ObservedWaitValue.Unavailable(
                "computed_property",
                $"Property '{propertyName}' is not exposed for the selected node.")
            : ObservedWaitValue.FromProtocolValue(
                computed.Value,
                computed.ValueType,
                computed.Source,
                computed.Diagnostic);
    }

    private static ComparedWaitValue CompareObservation(
        ObservedWaitValue observed,
        SemanticWaitCondition condition,
        string? baseline,
        bool baselineCaptured)
    {
        if (!observed.Available)
        {
            return new ComparedWaitValue(false, false, observed.Message);
        }

        var expected = condition.Comparison == SemanticWaitComparisons.Changed
            ? baseline
            : GetExpectedValue(condition);
        if (condition.Comparison == SemanticWaitComparisons.Changed && !baselineCaptured)
        {
            return new ComparedWaitValue(true, false, "Waiting for a baseline observation.");
        }

        var valueType = string.Equals(condition.ValueType, "auto", StringComparison.OrdinalIgnoreCase)
            ? observed.ValueType
            : condition.ValueType;
        var equality = TryCompareEqual(observed.Value, expected, valueType, out var equal, out var error);
        if (!equality)
        {
            return new ComparedWaitValue(false, false, error);
        }

        if (condition.Comparison == SemanticWaitComparisons.Equal)
        {
            return new ComparedWaitValue(true, equal);
        }

        if (condition.Comparison is SemanticWaitComparisons.NotEquals or SemanticWaitComparisons.Changed)
        {
            return new ComparedWaitValue(true, !equal);
        }

        if (!decimal.TryParse(observed.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var actualNumber)
            || !decimal.TryParse(expected, NumberStyles.Float, CultureInfo.InvariantCulture, out var expectedNumber))
        {
            return new ComparedWaitValue(
                false,
                false,
                "Ordered comparisons require invariant numeric observed and expected values.");
        }

        var matched = condition.Comparison switch
        {
            SemanticWaitComparisons.GreaterThan => actualNumber > expectedNumber,
            SemanticWaitComparisons.GreaterThanOrEqual => actualNumber >= expectedNumber,
            SemanticWaitComparisons.LessThan => actualNumber < expectedNumber,
            SemanticWaitComparisons.LessThanOrEqual => actualNumber <= expectedNumber,
            _ => false
        };
        return new ComparedWaitValue(true, matched);
    }

    private static string? GetExpectedValue(SemanticWaitCondition condition)
    {
        return condition.Kind switch
        {
            SemanticWaitConditionKinds.Visible
                or SemanticWaitConditionKinds.Enabled
                or SemanticWaitConditionKinds.Checked
                or SemanticWaitConditionKinds.Rendered
                or SemanticWaitConditionKinds.CommandExecutable => "true",
            SemanticWaitConditionKinds.Hidden
                or SemanticWaitConditionKinds.Disabled
                or SemanticWaitConditionKinds.Unchecked => "false",
            _ => condition.Expected
        };
    }

    private static bool TryCompareEqual(
        string? actual,
        string? expected,
        string valueType,
        out bool equal,
        out string? error)
    {
        error = null;
        if (string.Equals(valueType, "null", StringComparison.OrdinalIgnoreCase))
        {
            equal = actual is null && (expected is null || string.Equals(expected, "null", StringComparison.OrdinalIgnoreCase));
            return true;
        }

        if (valueType.Contains("Boolean", StringComparison.OrdinalIgnoreCase)
            || string.Equals(valueType, "bool", StringComparison.OrdinalIgnoreCase))
        {
            if (bool.TryParse(actual, out var actualBoolean)
                && bool.TryParse(expected, out var expectedBoolean))
            {
                equal = actualBoolean == expectedBoolean;
                return true;
            }

            equal = false;
            error = "Boolean comparison requires true or false observed and expected values.";
            return false;
        }

        if (IsNumericValueType(valueType))
        {
            if (decimal.TryParse(actual, NumberStyles.Float, CultureInfo.InvariantCulture, out var actualNumber)
                && decimal.TryParse(expected, NumberStyles.Float, CultureInfo.InvariantCulture, out var expectedNumber))
            {
                equal = actualNumber == expectedNumber;
                return true;
            }

            equal = false;
            error = "Numeric comparison requires invariant numeric observed and expected values.";
            return false;
        }

        equal = string.Equals(actual, expected, StringComparison.Ordinal);
        return true;
    }

    private static bool IsNumericValueType(string valueType)
    {
        return valueType.Contains("Byte", StringComparison.OrdinalIgnoreCase)
            || valueType.Contains("Int", StringComparison.OrdinalIgnoreCase)
            || valueType.Contains("Single", StringComparison.OrdinalIgnoreCase)
            || valueType.Contains("Double", StringComparison.OrdinalIgnoreCase)
            || valueType.Contains("Decimal", StringComparison.OrdinalIgnoreCase)
            || string.Equals(valueType, "number", StringComparison.OrdinalIgnoreCase);
    }

    private static void CopyObservation(
        IDictionary<string, string> metadata,
        RuntimeWaitObservation observation)
    {
        metadata["condition"] = observation.Condition;
        metadata["availability"] = observation.Availability;
        metadata["actual"] = observation.Value ?? "null";
        metadata["valueType"] = observation.ValueType;
        metadata["comparison"] = observation.Comparison;
        metadata["expected"] = observation.Expected ?? "null";
        if (observation.Baseline is not null)
        {
            metadata["baseline"] = observation.Baseline;
        }

        if (observation.Message is not null)
        {
            metadata["observationMessage"] = observation.Message;
        }
    }

    private static Dictionary<string, string> CreateWaitMetadata(
        int timeoutMs,
        int pollMs,
        int attempts,
        long elapsedMs) =>
        new(StringComparer.Ordinal)
        {
            ["timeoutMs"] = timeoutMs.ToString(CultureInfo.InvariantCulture),
            ["pollIntervalMs"] = pollMs.ToString(CultureInfo.InvariantCulture),
            ["attempts"] = attempts.ToString(CultureInfo.InvariantCulture),
            ["elapsedMs"] = elapsedMs.ToString(CultureInfo.InvariantCulture)
        };

    private static void CopyLastError(
        IDictionary<string, string> metadata,
        CoreError? error)
    {
        if (error is not null)
        {
            metadata["lastErrorCode"] = error.Code;
            metadata["lastErrorMessage"] = error.Message;
            if (error.Details is not null)
            {
                foreach (var key in new[] { "candidateCount", "candidatesTruncated", "candidates", "nextAction" })
                {
                    if (error.Details.TryGetValue(key, out var value))
                    {
                        metadata[$"lastError{char.ToUpperInvariant(key[0])}{key[1..]}"] = value;
                    }
                }
            }
        }
    }

    private static SemanticWorkflowStepResult ConsumePickerResult(
        LocalBridgeClient bridgeClient,
        SemanticWorkflowRequest request,
        SemanticWorkflowStep step)
    {
        var correlationId = step.Text ?? request.RequestId;
        var result = bridgeClient.NativePicker(
            request.SessionId,
            NativePickerOperations.ConsumePredefinedResult,
            correlationId: correlationId);
        if (!result.Success)
        {
            return Fail(step, result.Error!);
        }

        var picker = result.Value!;
        if (picker.Status is NativePickerResultStates.NotPrepared or NativePickerResultStates.Expired)
        {
            return new SemanticWorkflowStepResult(
                step.Id,
                step.Action,
                "failed",
                picker.Message ?? $"Picker result is {picker.Status}.",
                DateTimeOffset.UtcNow,
                diagnostics:
                [
                    new ProtocolError(
                        $"native_picker_{picker.Status}",
                        picker.Message ?? $"Picker result is {picker.Status}.",
                        new Dictionary<string, string>
                        {
                            ["correlationId"] = correlationId
                        })
                ],
                picker: picker);
        }

        return Pass(
            step,
            $"Consumed deterministic picker result '{picker.Status}'.",
            metadata: new Dictionary<string, string>
            {
                ["correlationId"] = correlationId,
                ["pickerStatus"] = picker.Status,
                ["oneShot"] = "true"
            },
            picker: picker);
    }

    private static async Task<SemanticWorkflowStepResult> CustomActionsAsync(
        LocalBridgeClient bridgeClient,
        SemanticWorkflowRequest request,
        SemanticWorkflowStep step,
        CancellationToken cancellationToken)
    {
        var target = await ResolveTargetAsync(bridgeClient, request, step, cancellationToken);
        if (!target.Success)
        {
            return Fail(step, target.Error!);
        }

        var result = await bridgeClient.CustomActionsAsync(
            request.SessionId,
            target.Value!.Target,
            cancellationToken);
        return result.Success
            ? Pass(
                step,
                $"Discovered {result.Value!.Actions.Count.ToString(CultureInfo.InvariantCulture)} custom action(s).",
                target.Value.Target,
                customActions: result.Value)
            : Fail(step, result.Error!, target.Value.Target);
    }

    private static async Task<SemanticWorkflowStepResult> CustomActionAsync(
        LocalBridgeClient bridgeClient,
        SemanticWorkflowRequest request,
        SemanticWorkflowStep step,
        RuntimeEvidencePolicyEnforcer? policy,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(step.CustomActionName))
        {
            return Fail(step, RuntimeCustomActionErrorCodes.InvalidRequest, "custom_action requires customActionName.");
        }

        var target = await ResolveTargetAsync(bridgeClient, request, step, cancellationToken);
        if (!target.Success)
        {
            return Fail(step, target.Error!);
        }

        var discovery = await bridgeClient.CustomActionsAsync(
            request.SessionId,
            target.Value!.Target,
            cancellationToken);
        if (!discovery.Success)
        {
            return Fail(step, discovery.Error!, target.Value.Target);
        }

        var descriptor = discovery.Value!.Actions.FirstOrDefault(action =>
            string.Equals(action.Name, step.CustomActionName, StringComparison.Ordinal));
        if (descriptor is null)
        {
            return Fail(
                step,
                RuntimeCustomActionErrorCodes.UnknownAction,
                $"Custom action '{step.CustomActionName}' is not available for the resolved selector target.",
                target.Value.Target,
                metadata: new Dictionary<string, string>
                {
                    ["availableActionNames"] = string.Join(",", discovery.Value.Actions.Select(static action => action.Name))
                });
        }

        var requestAuthorizedDestructive = request.AllowDestructive || !string.IsNullOrWhiteSpace(request.IsolatedStateDirectory);
        var authorizedDestructive = policy is null
            ? requestAuthorizedDestructive
            : policy.AllowsDestructiveAction(request.AllowDestructive, !string.IsNullOrWhiteSpace(request.IsolatedStateDirectory));
        if (string.Equals(descriptor.SafetyClassification, RuntimeCustomActionSafetyClassifications.Destructive, StringComparison.Ordinal)
            && !authorizedDestructive)
        {
            return Fail(
                step,
                RuntimeCustomActionErrorCodes.Disallowed,
                "The selected custom action is destructive; provide isolatedStateDirectory or set allowDestructive explicitly.",
                target.Value.Target,
                metadata: new Dictionary<string, string>
                {
                    ["actionName"] = descriptor.Name,
                    ["safetyClassification"] = descriptor.SafetyClassification
                });
        }

        if (!descriptor.Executable)
        {
            return Fail(
                step,
                RuntimeCustomActionErrorCodes.NonExecutable,
                descriptor.UnavailableReason ?? $"Custom action '{descriptor.Name}' is not executable in the current state.",
                target.Value.Target);
        }

        var customRequest = new RuntimeCustomActionRequest(
            $"{request.RequestId}:{step.Id}",
            target.Value.Target,
            descriptor.Name,
            step.CustomActionParameters,
            authorizedDestructive);
        var invocation = await bridgeClient.InvokeCustomActionAsync(
            request.SessionId,
            customRequest,
            cancellationToken);
        if (!invocation.Success)
        {
            return Fail(step, invocation.Error!, target.Value.Target);
        }

        var response = invocation.Value!;
        if (!response.Executed
            && response.Diagnostics.Any(static diagnostic =>
                string.Equals(diagnostic.Code, RuntimeCustomActionErrorCodes.TargetStale, StringComparison.Ordinal)))
        {
            target = await ResolveTargetAsync(bridgeClient, request, step, cancellationToken);
            if (!target.Success)
            {
                return Fail(step, target.Error!);
            }

            customRequest = new RuntimeCustomActionRequest(
                customRequest.RequestId,
                target.Value!.Target,
                descriptor.Name,
                step.CustomActionParameters,
                authorizedDestructive);
            invocation = await bridgeClient.InvokeCustomActionAsync(
                request.SessionId,
                customRequest,
                cancellationToken);
            if (!invocation.Success)
            {
                return Fail(step, invocation.Error!, target.Value.Target);
            }

            response = invocation.Value!;
        }

        return string.Equals(response.Status, RuntimeCustomActionStatuses.Executed, StringComparison.Ordinal)
            ? Pass(
                step,
                response.Message,
                target.Value.Target,
                metadata: response.Metadata,
                customAction: response)
            : new SemanticWorkflowStepResult(
                step.Id,
                step.Action,
                "failed",
                response.Message,
                DateTimeOffset.UtcNow,
                target.Value.Target,
                diagnostics: response.Diagnostics,
                metadata: response.Metadata,
                customAction: response);
    }

    private static async Task<SemanticWorkflowStepResult> InputAsync(
        LocalBridgeClient bridgeClient,
        SemanticWorkflowRequest request,
        SemanticWorkflowStep step,
        string inputAction,
        RuntimeEvidencePolicyEnforcer? policy,
        CancellationToken cancellationToken)
    {
        var target = await ResolveTargetAsync(bridgeClient, request, step, cancellationToken);
        if (!target.Success)
        {
            return Fail(step, target.Error!);
        }

        var resolvedTarget = target.Value!;
        var destination = await ResolveDestinationAsync(bridgeClient, request, step, cancellationToken);
        if (!destination.Success)
        {
            return Fail(step, destination.Error!, resolvedTarget.Target);
        }

        if (LooksDestructive(step, resolvedTarget)
            && !(policy is null
                ? request.AllowDestructive || !string.IsNullOrWhiteSpace(request.IsolatedStateDirectory)
                : policy.AllowsDestructiveAction(request.AllowDestructive, !string.IsNullOrWhiteSpace(request.IsolatedStateDirectory))))
        {
            return Fail(
                step,
                "semantic_workflow_destructive_target_requires_isolation",
                "The selected target looks destructive; provide isolatedStateDirectory or set allowDestructive explicitly.",
                resolvedTarget.Target);
        }

        var result = await bridgeClient.InputAsync(
            request.SessionId,
            request.TopLevelId!,
            inputAction,
            inputText: step.Text,
            targetNodeId: resolvedTarget.Target.NodeId,
            inputKey: step.Key,
            keyModifiers: step.Modifiers,
            gesture: CreateGestureOptions(step, destination.Value),
            cancellationToken: cancellationToken,
            inputTarget: resolvedTarget.Target,
            gestureDestinationTarget: destination.Value?.Target);

        if (!result.Success && IsPreDispatchStale(result.Error!))
        {
            target = await ResolveTargetAsync(bridgeClient, request, step, cancellationToken);
            if (!target.Success)
            {
                return Fail(step, target.Error!);
            }

            resolvedTarget = target.Value!;
            destination = await ResolveDestinationAsync(bridgeClient, request, step, cancellationToken);
            if (!destination.Success)
            {
                return Fail(step, destination.Error!, resolvedTarget.Target);
            }

            if (LooksDestructive(step, resolvedTarget)
                && !(policy is null
                    ? request.AllowDestructive || !string.IsNullOrWhiteSpace(request.IsolatedStateDirectory)
                    : policy.AllowsDestructiveAction(request.AllowDestructive, !string.IsNullOrWhiteSpace(request.IsolatedStateDirectory))))
            {
                return Fail(
                    step,
                    "semantic_workflow_destructive_target_requires_isolation",
                    "The re-resolved target looks destructive; provide isolatedStateDirectory or set allowDestructive explicitly.",
                    resolvedTarget.Target);
            }

            result = await bridgeClient.InputAsync(
                request.SessionId,
                request.TopLevelId!,
                inputAction,
                inputText: step.Text,
                targetNodeId: resolvedTarget.Target.NodeId,
                inputKey: step.Key,
                keyModifiers: step.Modifiers,
                gesture: CreateGestureOptions(step, destination.Value),
                cancellationToken: cancellationToken,
                inputTarget: resolvedTarget.Target,
                gestureDestinationTarget: destination.Value?.Target);
        }

        return result.Success
            ? Pass(step, $"Input action '{inputAction}' executed.", resolvedTarget.Target, result.Value)
            : Fail(step, ToProtocolError(result.Error!), resolvedTarget.Target);
    }

    private static async Task<SemanticWorkflowStepResult> InspectAsync(
        LocalBridgeClient bridgeClient,
        SemanticWorkflowRequest request,
        SemanticWorkflowStep step,
        CancellationToken cancellationToken)
    {
        var target = await ResolveTargetAsync(bridgeClient, request, step, cancellationToken);
        if (!target.Success)
        {
            return Fail(step, target.Error!);
        }

        var result = await bridgeClient.InspectNodeAsync(
            request.SessionId,
            request.TopLevelId!,
            target.Value!.Target.TreeKind ?? TreeKinds.Visual,
            target.Value.Target.NodeId!,
            cancellationToken);

        return result.Success
            ? Pass(step, "Node inspection captured.", target.Value.Target, inspection: result.Value)
            : Fail(step, ToProtocolError(result.Error!), target.Value.Target);
    }

    private static async Task<SemanticWorkflowStepResult> AssertStateAsync(
        LocalBridgeClient bridgeClient,
        SemanticWorkflowRequest request,
        SemanticWorkflowStep step,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(step.AssertProperty))
        {
            return Fail(step, "semantic_workflow_assert_property_required", "assert_state requires assertProperty.");
        }

        var target = await ResolveTargetAsync(bridgeClient, request, step, cancellationToken);
        if (!target.Success)
        {
            return Fail(step, target.Error!);
        }

        var inspect = await bridgeClient.InspectNodeAsync(
            request.SessionId,
            request.TopLevelId!,
            target.Value!.Target.TreeKind ?? TreeKinds.Visual,
            target.Value.Target.NodeId!,
            cancellationToken);

        if (!inspect.Success)
        {
            return Fail(step, ToProtocolError(inspect.Error!), target.Value.Target);
        }

        var actual = ReadInspectableValue(inspect.Value!, step.AssertProperty);
        if (string.Equals(actual, step.Expected, StringComparison.Ordinal))
        {
            return Pass(
                step,
                $"Assertion passed for '{step.AssertProperty}'.",
                target.Value.Target,
                inspection: inspect.Value,
                metadata: new Dictionary<string, string>
                {
                    ["assertProperty"] = step.AssertProperty,
                    ["actual"] = actual ?? "null",
                    ["expected"] = step.Expected ?? "null"
                });
        }

        return Fail(
            step,
            "semantic_workflow_assertion_failed",
            $"Assertion failed for '{step.AssertProperty}'.",
            target.Value.Target,
            inspect.Value,
            new Dictionary<string, string>
            {
                ["assertProperty"] = step.AssertProperty,
                ["actual"] = actual ?? "null",
                ["expected"] = step.Expected ?? "null"
            });
    }

    private static async Task<SemanticWorkflowStepResult> ScreenshotAsync(
        LocalBridgeClient bridgeClient,
        SemanticWorkflowRequest request,
        SemanticWorkflowStep step,
        int stepIndex,
        RuntimeEvidencePolicyEnforcer? policy,
        CancellationToken cancellationToken)
    {
        var path = ResolveScreenshotPath(request, step, stepIndex);
        if (string.IsNullOrWhiteSpace(path))
        {
            return Fail(step, "semantic_workflow_screenshot_path_required", "Screenshot steps require screenshotPath or workflow outputDirectory.");
        }

        var result = await bridgeClient.CaptureScreenshotAsync(
            request.SessionId,
            request.TopLevelId!,
            path,
            cancellationToken);

        if (!result.Success)
        {
            return Fail(step, ToProtocolError(result.Error!));
        }

        if (policy is not null)
        {
            var masked = await policy.MaskScreenshotAsync(bridgeClient, request, result.Value!, cancellationToken);
            if (!masked.Success)
            {
                return Fail(step, masked.Error!);
            }

            return Pass(step, "Screenshot captured and policy-masked.", result.Value!.Target, screenshot: result.Value, metadata: masked.Value);
        }

        return Pass(step, "Screenshot captured.", result.Value!.Target, screenshot: result.Value);
    }

    private static Task<SemanticWorkflowStepResult> CaptureStepScreenshotAsync(
        LocalBridgeClient bridgeClient,
        SemanticWorkflowRequest request,
        SemanticWorkflowStep step,
        int stepIndex,
        RuntimeEvidencePolicyEnforcer? policy,
        CancellationToken cancellationToken)
    {
        var screenshotStep = new SemanticWorkflowStep(
            SemanticWorkflowActions.Screenshot,
            $"{step.Id}:screenshot",
            screenshotPath: ResolveScreenshotPath(request, step, stepIndex),
            topLevelAlias: step.TopLevelAlias);
        return ExecuteStepAsync(bridgeClient, request, screenshotStep, stepIndex, policy, cancellationToken);
    }

    private static SemanticWorkflowRequest WithTopLevelId(
        SemanticWorkflowRequest request,
        string topLevelId)
    {
        return new SemanticWorkflowRequest(
            request.SessionId,
            topLevelId,
            request.Steps,
            request.RequestId,
            request.OutputDirectory,
            request.CaptureAfterEachStep,
            request.AllowDestructive,
            request.IsolatedStateDirectory,
            request.MaxDepth,
            request.TopLevelAliases,
            request.Variables,
            request.Fragments,
            request.ValidateOnly,
            request.TimeoutMs,
            request.Evidence);
    }

    private static async Task<CoreResult<ResolvedWorkflowTopLevel>> ResolveTopLevelAliasAsync(
        LocalBridgeClient bridgeClient,
        SemanticWorkflowRequest request,
        string alias,
        CancellationToken cancellationToken)
    {
        var definition = request.TopLevelAliases.FirstOrDefault(item =>
            string.Equals(item.Alias, alias, StringComparison.Ordinal));
        if (definition is null)
        {
            return CoreResult<ResolvedWorkflowTopLevel>.Fail(new CoreError(
                "semantic_workflow_top_level_alias_unknown",
                $"Top-level alias '{alias}' is not declared by the workflow.",
                new Dictionary<string, string>
                {
                    ["topLevelAlias"] = alias,
                    ["declaredAliases"] = string.Join(",", request.TopLevelAliases.Select(static item => item.Alias)),
                    ["nextAction"] = "Declare the alias in topLevelAliases or select an existing workflow alias."
                }));
        }

        if (definition.Selector.SessionId is { } selectorSession
            && !string.Equals(selectorSession.Value, request.SessionId.Value, StringComparison.Ordinal))
        {
            return CoreResult<ResolvedWorkflowTopLevel>.Fail(new CoreError(
                "semantic_workflow_top_level_alias_session_mismatch",
                $"Top-level alias '{alias}' is scoped to another session.",
                new Dictionary<string, string>
                {
                    ["topLevelAlias"] = alias,
                    ["workflowSessionId"] = request.SessionId.Value,
                    ["selectorSessionId"] = selectorSession.Value,
                    ["nextAction"] = "Use a selector scoped to the workflow session; aliases never cross session boundaries."
                }));
        }

        var topLevels = await bridgeClient.ListTopLevelsAsync(request.SessionId, cancellationToken);
        if (!topLevels.Success)
        {
            return CoreResult<ResolvedWorkflowTopLevel>.Fail(topLevels.Error!);
        }

        var matches = topLevels.Value!.TopLevels
            .Where(topLevel => MatchesTopLevelSelector(topLevel, definition.Selector))
            .Take(9)
            .ToArray();
        if (matches.Length == 0)
        {
            return CoreResult<ResolvedWorkflowTopLevel>.Fail(new CoreError(
                "semantic_workflow_top_level_alias_missing",
                $"Top-level alias '{alias}' did not match an active top-level.",
                CreateTopLevelAliasDetails(alias, definition.Selector, topLevels.Value.TopLevels)));
        }

        if (matches.Length > 1)
        {
            return CoreResult<ResolvedWorkflowTopLevel>.Fail(new CoreError(
                "semantic_workflow_top_level_alias_ambiguous",
                $"Top-level alias '{alias}' matched multiple active top-levels.",
                CreateTopLevelAliasDetails(alias, definition.Selector, matches)));
        }

        return CoreResult<ResolvedWorkflowTopLevel>.Ok(new ResolvedWorkflowTopLevel(
            alias,
            matches[0]));
    }

    private static bool MatchesTopLevelSelector(
        TopLevelSummary topLevel,
        SemanticTopLevelSelector selector)
    {
        return (string.IsNullOrWhiteSpace(selector.Title)
                || string.Equals(topLevel.Title, selector.Title, StringComparison.Ordinal))
            && (string.IsNullOrWhiteSpace(selector.Kind)
                || string.Equals(topLevel.Kind, selector.Kind, StringComparison.OrdinalIgnoreCase))
            && (!selector.IsActive.HasValue || topLevel.IsActive == selector.IsActive.Value);
    }

    private static IReadOnlyDictionary<string, string> CreateTopLevelAliasDetails(
        string alias,
        SemanticTopLevelSelector selector,
        IEnumerable<TopLevelSummary> candidates)
    {
        var values = candidates.Take(9).ToArray();
        var details = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["topLevelAlias"] = alias,
            ["candidateCount"] = values.Length.ToString(CultureInfo.InvariantCulture),
            ["candidatesTruncated"] = (values.Length > 8).ToString().ToLowerInvariant(),
            ["candidates"] = JsonSerializer.Serialize(values.Take(8).Select(static topLevel => new
            {
                topLevel.Id,
                topLevel.Kind,
                topLevel.Title,
                topLevel.Width,
                topLevel.Height,
                topLevel.RenderScaling,
                topLevel.IsActive
            })),
            ["nextAction"] = "Refine the top-level alias selector so it matches exactly one active top-level."
        };
        CopyDetail(details, "title", selector.Title);
        CopyDetail(details, "kind", selector.Kind);
        CopyDetail(details, "isActive", selector.IsActive);
        return details;
    }

    private static bool IsMissingTopLevelAlias(CoreError? error) =>
        string.Equals(
            error?.Code,
            "semantic_workflow_top_level_alias_missing",
            StringComparison.Ordinal);

    private static async Task<CoreResult<ResolvedWorkflowTarget>> ResolveTargetAsync(
        LocalBridgeClient bridgeClient,
        SemanticWorkflowRequest request,
        SemanticWorkflowStep step,
        CancellationToken cancellationToken)
    {
        return await ResolveSelectorAsync(
            bridgeClient,
            request,
            step.Selector,
            "Workflow step",
            cancellationToken);
    }

    private static async Task<CoreResult<ResolvedWorkflowTarget?>> ResolveDestinationAsync(
        LocalBridgeClient bridgeClient,
        SemanticWorkflowRequest request,
        SemanticWorkflowStep step,
        CancellationToken cancellationToken)
    {
        if (step.DestinationSelector is null)
        {
            return CoreResult<ResolvedWorkflowTarget?>.Ok(null);
        }

        var resolved = await ResolveSelectorAsync(
            bridgeClient,
            request,
            step.DestinationSelector,
            "Workflow destination",
            cancellationToken);
        return resolved.Success
            ? CoreResult<ResolvedWorkflowTarget?>.Ok(resolved.Value)
            : CoreResult<ResolvedWorkflowTarget?>.Fail(resolved.Error!);
    }

    private static async Task<CoreResult<ResolvedWorkflowTarget>> ResolveSelectorAsync(
        LocalBridgeClient bridgeClient,
        SemanticWorkflowRequest request,
        SemanticWorkflowSelector? selector,
        string selectorRole,
        CancellationToken cancellationToken)
    {
        if (selector is null || !selector.HasSearchCriteria)
        {
            return CoreResult<ResolvedWorkflowTarget>.Fail(new CoreError(
                CoreErrorCodes.InvalidBridgeRequest,
                $"{selectorRole} requires a selector."));
        }

        if (!string.IsNullOrWhiteSpace(selector.NodeId))
        {
            var treeKind = selector.TreeKind;
            var inspect = await bridgeClient.InspectNodeAsync(
                request.SessionId,
                request.TopLevelId!,
                treeKind,
                selector.NodeId,
                cancellationToken);

            if (!inspect.Success)
            {
                return CoreResult<ResolvedWorkflowTarget>.Fail(inspect.Error!);
            }

            return MatchesInteractionState(inspect.Value!.InteractionState, selector)
                ? CoreResult<ResolvedWorkflowTarget>.Ok(CreateResolvedTarget(inspect.Value))
                : CoreResult<ResolvedWorkflowTarget>.Fail(new CoreError(
                    CoreErrorCodes.InvalidBridgeRequest,
                    $"{selectorRole} selector matched the node identity but not its requested interaction state.",
                    CreateSelectorDetails(selector)));
        }

        if (!string.IsNullOrWhiteSpace(selector.BindingPath) || !string.IsNullOrWhiteSpace(selector.CommandName))
        {
            return await ResolveSourceMappedTargetAsync(bridgeClient, request, selector, cancellationToken);
        }

        var nodeType = selector.NodeType ?? selector.Role;
        var result = await bridgeClient.FindNodesAsync(
            request.SessionId,
            request.TopLevelId!,
            selector.TreeKind,
            nodeType: nodeType,
            name: selector.Name,
            automationId: selector.AutomationId,
            text: selector.Text,
            maxDepth: selector.MaxDepth ?? request.MaxDepth,
            maxResults: 9,
            cancellationToken: cancellationToken,
            visible: selector.Visible,
            enabled: selector.Enabled,
            rendered: selector.Rendered,
            actionable: selector.Actionable);

        if (!result.Success)
        {
            return CoreResult<ResolvedWorkflowTarget>.Fail(result.Error!);
        }

        if (result.Value!.Matches.Count == 0)
        {
            return CoreResult<ResolvedWorkflowTarget>.Fail(new CoreError(
                CoreErrorCodes.InvalidBridgeRequest,
                $"{selectorRole} selector did not match any node.",
                CreateSelectorDetails(selector)));
        }

        if (result.Value.Matches.Count > 1)
        {
            return CoreResult<ResolvedWorkflowTarget>.Fail(new CoreError(
                CoreErrorCodes.InvalidBridgeRequest,
                $"{selectorRole} selector matched multiple nodes; make the selector more specific.",
                CreateAmbiguityDetails(
                    selector,
                    result.Value.Matches.Select(static match => match.Node),
                    request.TopLevelId!)));
        }

        var match = result.Value.Matches[0];
        return CoreResult<ResolvedWorkflowTarget>.Ok(CreateResolvedTarget(match.Node));
    }

    private static InputGestureOptions? CreateGestureOptions(
        SemanticWorkflowStep step,
        ResolvedWorkflowTarget? destination)
    {
        if (step.Direction is null
            && step.DistancePercentage is null
            && step.DurationMs is null
            && destination is null)
        {
            return null;
        }

        return new InputGestureOptions(
            step.Direction,
            step.DistancePercentage,
            step.DurationMs,
            destination?.Target.NodeId);
    }

    private static async Task<CoreResult<ResolvedWorkflowTarget>> ResolveSourceMappedTargetAsync(
        LocalBridgeClient bridgeClient,
        SemanticWorkflowRequest request,
        SemanticWorkflowSelector selector,
        CancellationToken cancellationToken)
    {
        var tree = await bridgeClient.VisualTreeAsync(
            request.SessionId,
            request.TopLevelId!,
            selector.MaxDepth ?? request.MaxDepth,
            cancellationToken);
        if (!tree.Success)
        {
            return CoreResult<ResolvedWorkflowTarget>.Fail(tree.Error!);
        }

        var matches = EnumerateNodes(tree.Value!.Root)
            .Where(node => MatchesSourceMappedSelector(node, selector))
            .Take(9)
            .ToArray();

        if (matches.Length == 0)
        {
            return CoreResult<ResolvedWorkflowTarget>.Fail(new CoreError(
                CoreErrorCodes.InvalidBridgeRequest,
                "Workflow source-mapped selector did not match any node.",
                CreateSelectorDetails(selector)));
        }

        if (matches.Length > 1)
        {
            return CoreResult<ResolvedWorkflowTarget>.Fail(new CoreError(
                CoreErrorCodes.InvalidBridgeRequest,
                "Workflow source-mapped selector matched multiple nodes; add automationId, name, or text.",
                CreateAmbiguityDetails(selector, matches, request.TopLevelId!)));
        }

        return CoreResult<ResolvedWorkflowTarget>.Ok(CreateResolvedTarget(matches[0]));
    }

    private static IEnumerable<TreeNodeSummary> EnumerateNodes(TreeNodeSummary node)
    {
        yield return node;
        foreach (var child in node.Children)
        {
            foreach (var descendant in EnumerateNodes(child))
            {
                yield return descendant;
            }
        }
    }

    private static bool MatchesSourceMappedSelector(TreeNodeSummary node, SemanticWorkflowSelector selector)
    {
        if (!string.IsNullOrWhiteSpace(selector.AutomationId)
            && !string.Equals(node.AutomationId, selector.AutomationId, StringComparison.Ordinal))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(selector.Name)
            && !string.Equals(node.Name, selector.Name, StringComparison.Ordinal))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(selector.Text)
            && !string.Equals(node.Text, selector.Text, StringComparison.Ordinal))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(selector.NodeType)
            && !node.NodeType.Contains(selector.NodeType, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(selector.BindingPath)
            && node.SourceMap?.Bindings.Any(binding => string.Equals(binding.BindingPath, selector.BindingPath, StringComparison.Ordinal)) != true)
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(selector.CommandName)
            && node.SourceMap?.Bindings.Any(binding =>
                binding.TargetProperty.Contains("Command", StringComparison.OrdinalIgnoreCase)
                && string.Equals(binding.BindingPath, selector.CommandName, StringComparison.Ordinal)) != true)
        {
            return false;
        }

        return MatchesInteractionState(node.InteractionState, selector);
    }

    private static bool MatchesInteractionState(
        RuntimeNodeInteractionState? state,
        SemanticWorkflowSelector selector)
    {
        return (!selector.Visible.HasValue || state?.Visible == selector.Visible)
            && (!selector.Enabled.HasValue || state?.Enabled == selector.Enabled)
            && (!selector.Rendered.HasValue || state?.Rendered == selector.Rendered)
            && (!selector.Actionable.HasValue || state?.Actionable == selector.Actionable);
    }

    private static string? ReadInspectableValue(InspectNodeResponse response, string propertyName)
    {
        return propertyName switch
        {
            "text" => response.Text,
            "name" => response.Name,
            "automationId" => response.AutomationId,
            "nodeType" => response.NodeType,
            "classes" => string.Join(",", response.Classes),
            _ => response.ComputedProperties
                .FirstOrDefault(property => string.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase))
                ?.Value
        };
    }

    private static bool LooksDestructive(SemanticWorkflowStep step, ResolvedWorkflowTarget target)
    {
        var effectiveAction = step.Action == SemanticWorkflowActions.ValidateAction
            ? step.InputAction
            : step.Action;
        if (effectiveAction is not SemanticWorkflowActions.Click
            and not SemanticWorkflowActions.Invoke
            and not SemanticWorkflowActions.Select
            and not SemanticWorkflowActions.Toggle
            and not SemanticWorkflowActions.Expand
            and not SemanticWorkflowActions.Collapse)
        {
            return false;
        }

        var haystack = string.Join(
            " ",
            step.Text,
            step.Selector?.Text,
            step.Selector?.AutomationId,
            step.Selector?.Name,
            target.Text,
            target.AutomationId,
            target.Name);

        return DestructiveTokens.Any(token => haystack.Contains(token, StringComparison.OrdinalIgnoreCase));
    }

    private static string? ResolveScreenshotPath(SemanticWorkflowRequest request, SemanticWorkflowStep step, int stepIndex)
    {
        if (!string.IsNullOrWhiteSpace(step.ScreenshotPath))
        {
            return Path.GetFullPath(step.ScreenshotPath);
        }

        if (string.IsNullOrWhiteSpace(request.OutputDirectory))
        {
            return null;
        }

        var safeId = string.Join(
            "-",
            step.Id.Split(Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries));
        if (string.IsNullOrWhiteSpace(safeId))
        {
            safeId = step.Action;
        }

        return Path.Combine(
            request.OutputDirectory,
            $"{(stepIndex + 1).ToString("00", CultureInfo.InvariantCulture)}-{safeId}.png");
    }

    private static SemanticWorkflowStepResult Pass(
        SemanticWorkflowStep step,
        string message,
        RuntimeTargetContext? target = null,
        InputResponse? input = null,
        InspectNodeResponse? inspection = null,
        ScreenshotResponse? screenshot = null,
        IReadOnlyDictionary<string, string>? metadata = null,
        NativePickerResponse? picker = null,
        RuntimeMutationResponse? mutation = null,
        RuntimeCustomActionsResponse? customActions = null,
        RuntimeCustomActionResponse? customAction = null,
        RuntimeWaitObservation? waitObservation = null)
    {
        return new SemanticWorkflowStepResult(
            step.Id,
            step.Action,
            "passed",
            message,
            DateTimeOffset.UtcNow,
            target,
            input,
            inspection,
            screenshot,
            metadata: metadata,
            picker: picker,
            mutation: mutation,
            customActions: customActions,
            customAction: customAction,
            waitObservation: waitObservation);
    }

    private static SemanticWorkflowStepResult WithTopLevelEvidence(
        SemanticWorkflowStepResult result,
        string? topLevelAlias,
        string? resolvedTopLevelId)
    {
        if (string.IsNullOrWhiteSpace(topLevelAlias))
        {
            return result;
        }

        var metadata = new Dictionary<string, string>(result.Metadata, StringComparer.Ordinal)
        {
            ["topLevelAlias"] = topLevelAlias
        };
        if (!string.IsNullOrWhiteSpace(resolvedTopLevelId))
        {
            metadata["resolvedTopLevelId"] = resolvedTopLevelId;
        }

        return new SemanticWorkflowStepResult(
            result.StepId,
            result.Action,
            result.Status,
            result.Message,
            result.ExecutedAt,
            result.Target,
            result.Input,
            result.Inspection,
            result.Screenshot,
            result.Diagnostics,
            metadata,
            result.Picker,
            result.Mutation,
            result.CustomActions,
            result.CustomAction,
            result.WaitObservation,
            topLevelAlias,
            resolvedTopLevelId,
            result.ExecutionPath,
            result.ParentStepId,
            result.Attempt,
            result.SourceFragment,
            result.Verification,
            result.FailureEvidence);
    }

    private static SemanticWorkflowStepResult Fail(
        SemanticWorkflowStep step,
        CoreError error,
        RuntimeTargetContext? target = null)
    {
        return Fail(step, ToProtocolError(error), target);
    }

    private static SemanticWorkflowStepResult Fail(
        SemanticWorkflowStep step,
        ProtocolError error,
        RuntimeTargetContext? target = null,
        RuntimeMutationResponse? mutation = null)
    {
        return new SemanticWorkflowStepResult(
            step.Id,
            step.Action,
            "failed",
            error.Message,
            DateTimeOffset.UtcNow,
            target,
            diagnostics: [error],
            mutation: mutation);
    }

    private static SemanticWorkflowStepResult Fail(
        SemanticWorkflowStep step,
        string code,
        string message,
        RuntimeTargetContext? target = null,
        InspectNodeResponse? inspection = null,
        IReadOnlyDictionary<string, string>? metadata = null,
        RuntimeWaitObservation? waitObservation = null)
    {
        return new SemanticWorkflowStepResult(
            step.Id,
            step.Action,
            "failed",
            message,
            DateTimeOffset.UtcNow,
            target,
            inspection: inspection,
            diagnostics: [new ProtocolError(code, message, metadata)],
            metadata: metadata,
            waitObservation: waitObservation);
    }

    private static ProtocolError ToProtocolError(CoreError error)
    {
        return new ProtocolError(error.Code, error.Message, error.Details);
    }

    private static IReadOnlyDictionary<string, string> CreateSelectorDetails(SemanticWorkflowSelector selector)
    {
        var details = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["treeKind"] = selector.TreeKind
        };

        CopyDetail(details, "nodeId", selector.NodeId);
        CopyDetail(details, "automationId", selector.AutomationId);
        CopyDetail(details, "text", selector.Text);
        CopyDetail(details, "name", selector.Name);
        CopyDetail(details, "nodeType", selector.NodeType);
        CopyDetail(details, "role", selector.Role);
        CopyDetail(details, "bindingPath", selector.BindingPath);
        CopyDetail(details, "commandName", selector.CommandName);
        CopyDetail(details, "visible", selector.Visible);
        CopyDetail(details, "enabled", selector.Enabled);
        CopyDetail(details, "rendered", selector.Rendered);
        CopyDetail(details, "actionable", selector.Actionable);

        return details;
    }

    private static bool IsPreDispatchStale(CoreError error)
    {
        return string.Equals(error.Code, RuntimeInputErrorCodes.TargetStale, StringComparison.Ordinal)
            && error.Details?.TryGetValue("dispatched", out var dispatched) == true
            && string.Equals(dispatched, bool.FalseString, StringComparison.OrdinalIgnoreCase);
    }

    private static IReadOnlyDictionary<string, string> CreateAmbiguityDetails(
        SemanticWorkflowSelector selector,
        IEnumerable<TreeNodeSummary> matches,
        string topLevelId)
    {
        var nodes = matches.Take(9).ToArray();
        var details = new Dictionary<string, string>(CreateSelectorDetails(selector), StringComparer.Ordinal)
        {
            ["candidateCount"] = nodes.Length.ToString(CultureInfo.InvariantCulture),
            ["candidatesTruncated"] = (nodes.Length > 8).ToString().ToLowerInvariant(),
            ["candidates"] = JsonSerializer.Serialize(nodes.Take(8).Select(node => new
            {
                node.NodeId,
                node.NodeType,
                node.Name,
                node.AutomationId,
                node.Text,
                node.Bounds,
                TopLevelId = node.Target?.TopLevelId ?? topLevelId,
                Visible = node.InteractionState?.Visible,
                Enabled = node.InteractionState?.Enabled,
                Rendered = node.InteractionState?.Rendered,
                Actionable = node.InteractionState?.Actionable,
                AvailableActions = node.InteractionState?.AvailableActions ?? []
            })),
            ["nextAction"] = "Add a stable identity field or an interaction-state filter, then resolve the selector again."
        };
        return details;
    }

    private static void CopyDetail(IDictionary<string, string> details, string key, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            details[key] = value;
        }
    }

    private static void CopyDetail(IDictionary<string, string> details, string key, bool? value)
    {
        if (value.HasValue)
        {
            details[key] = value.Value.ToString().ToLowerInvariant();
        }
    }

    private static ResolvedWorkflowTarget CreateResolvedTarget(TreeNodeSummary node)
    {
        return new ResolvedWorkflowTarget(
            node.Target!,
            node.NodeType,
            node.Name,
            node.AutomationId,
            node.Text,
            node.SourceMap);
    }

    private static ResolvedWorkflowTarget CreateResolvedTarget(InspectNodeResponse response)
    {
        return new ResolvedWorkflowTarget(
            response.Target,
            response.NodeType,
            response.Name,
            response.AutomationId,
            response.Text,
            response.SourceMap);
    }

    private sealed record ResolvedWorkflowTarget(
        RuntimeTargetContext Target,
        string NodeType,
        string? Name,
        string? AutomationId,
        string? Text,
        RuntimeNodeSourceMap? SourceMap);

    private sealed record ResolvedWorkflowTopLevel(string Alias, TopLevelSummary Summary);

    private sealed record CompositionConditionEvaluation(
        bool Success,
        bool Matched,
        SemanticWorkflowStepResult Result);

    private sealed class CompositionExecutionContext
    {
        public CompositionExecutionContext(
            SemanticWorkflowRequest request,
            LocalBridgeClient bridgeClient,
            WorkflowIdempotencyStore idempotencyStore,
            SemanticWorkflowPlan plan,
            RuntimeEvidencePolicyEnforcer? policy,
            IReadOnlyDictionary<string, string>? policyMetadata)
        {
            Request = request;
            BridgeClient = bridgeClient;
            IdempotencyStore = idempotencyStore;
            Plan = plan;
            Policy = policy;
            PolicyMetadata = policyMetadata;
        }

        public SemanticWorkflowRequest Request { get; }

        public LocalBridgeClient BridgeClient { get; }

        public WorkflowIdempotencyStore IdempotencyStore { get; }

        public SemanticWorkflowPlan Plan { get; }

        public RuntimeEvidencePolicyEnforcer? Policy { get; }

        public IReadOnlyDictionary<string, string>? PolicyMetadata { get; }

        public List<SemanticWorkflowStepResult> Results { get; } = [];

        public List<ProtocolError> Diagnostics { get; } = [];

        public int ReplayCount { get; set; }
    }

    private sealed record VerificationSnapshot(
        InspectNodeResponse? Inspection,
        ScreenshotResponse? Screenshot,
        IReadOnlyList<ProtocolError> Diagnostics)
    {
        public static VerificationSnapshot Empty { get; } = new(null, null, []);
    }

    private sealed record ObservedWaitValue(
        bool Available,
        string? Value,
        string ValueType,
        string Source,
        string? Message = null)
    {
        public static ObservedWaitValue Unavailable(string source, string message) =>
            new(false, null, "not_available", source, message);

        public static ObservedWaitValue FromBoolean(bool? value, string source) =>
            value.HasValue
                ? new(true, value.Value.ToString().ToLowerInvariant(), typeof(bool).FullName!, source)
                : Unavailable(source, "The requested interaction state is not available for the selected node.");

        public static ObservedWaitValue FromProtocolValue(
            string value,
            string valueType,
            string source,
            string? message = null)
        {
            if (string.Equals(value, "not_available", StringComparison.Ordinal)
                && string.Equals(valueType, "not_available", StringComparison.Ordinal))
            {
                return Unavailable(source, message ?? "The requested runtime value is not available.");
            }

            return new(
                true,
                string.Equals(valueType, "null", StringComparison.OrdinalIgnoreCase)
                    && string.Equals(value, "null", StringComparison.Ordinal)
                        ? null
                        : value,
                valueType,
                source,
                message);
        }
    }

    private sealed record ComparedWaitValue(bool Available, bool Matched, string? Message = null);
}
