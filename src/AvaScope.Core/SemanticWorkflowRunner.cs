using System.Globalization;
using System.Diagnostics;
using System.Text.Json;
using AvaScope.Protocol;

namespace AvaScope.Core;

public sealed class SemanticWorkflowRunner
{
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
        var results = new List<SemanticWorkflowStepResult>();
        var diagnostics = new List<ProtocolError>();
        var idempotencyStore = new WorkflowIdempotencyStore(bridgeClient.ManifestDirectory);
        var replayCount = 0;
        var isolatedStateStatus = string.IsNullOrWhiteSpace(request.IsolatedStateDirectory)
            ? "not_configured"
            : "declared_by_request";

        if (!string.IsNullOrWhiteSpace(request.OutputDirectory))
        {
            Directory.CreateDirectory(request.OutputDirectory);
        }

        foreach (var step in request.Steps)
        {
            var result = await ExecuteStepWithIdempotencyAsync(
                bridgeClient,
                idempotencyStore,
                request,
                step,
                results.Count,
                cancellationToken);
            results.Add(result);
            var replayed = result.Metadata.TryGetValue("idempotencyReplay", out var replay)
                && string.Equals(replay, "true", StringComparison.Ordinal);
            if (replayed)
            {
                replayCount++;
            }

            if (request.CaptureAfterEachStep
                && result.Status == "passed"
                && !replayed
                && result.Screenshot is null
                && !string.IsNullOrWhiteSpace(request.OutputDirectory)
                && step.Action != SemanticWorkflowActions.Wait)
            {
                var screenshot = await CaptureStepScreenshotAsync(bridgeClient, request, step, results.Count, cancellationToken);
                results.Add(screenshot);
            }

            if (result.Status == "failed")
            {
                diagnostics.AddRange(result.Diagnostics);
                break;
            }
        }

        var status = results.All(static result => result.Status == "passed")
            ? "passed"
            : "failed";

        return CoreResult<SemanticWorkflowResponse>.Ok(new SemanticWorkflowResponse(
            request.RequestId,
            request.SessionId,
            request.TopLevelId,
            status,
            startedAt,
            DateTimeOffset.UtcNow,
            results,
            isolatedStateStatus,
            diagnostics,
            new Dictionary<string, string>
            {
                ["requestedSteps"] = request.Steps.Count.ToString(CultureInfo.InvariantCulture),
                ["executedSteps"] = results.Count.ToString(CultureInfo.InvariantCulture),
                ["idempotencyReplayCount"] = replayCount.ToString(CultureInfo.InvariantCulture),
                ["selectorMode"] = "automation_text_name_type_binding_or_node_id"
            }));
    }

    private static async Task<SemanticWorkflowStepResult> ExecuteStepWithIdempotencyAsync(
        LocalBridgeClient bridgeClient,
        WorkflowIdempotencyStore store,
        SemanticWorkflowRequest request,
        SemanticWorkflowStep step,
        int stepIndex,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(step.IdempotencyKey))
        {
            return await ExecuteStepAsync(
                bridgeClient,
                request,
                step,
                stepIndex,
                cancellationToken);
        }

        var lease = await store.AcquireAsync(
            request.SessionId,
            step.IdempotencyKey,
            cancellationToken);
        if (!lease.Success)
        {
            return Fail(step, lease.Error!);
        }

        using var idempotencyLease = lease.Value!;
        var signature = WorkflowIdempotencyStore.CreateSignature(request, step);
        var replay = store.TryReplay(request.SessionId, step.IdempotencyKey, signature);
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
            cancellationToken);
        var save = store.Save(
            request.SessionId,
            step.IdempotencyKey,
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
        CancellationToken cancellationToken)
    {
        try
        {
            return step.Action switch
            {
                SemanticWorkflowActions.Wait => await WaitAsync(step, cancellationToken),
                SemanticWorkflowActions.WaitForNode => await WaitForNodeAsync(bridgeClient, request, step, cancellationToken),
                SemanticWorkflowActions.WaitForState => await WaitForStateAsync(bridgeClient, request, step, cancellationToken),
                SemanticWorkflowActions.WaitForDialog => await WaitForDialogAsync(bridgeClient, request, step, cancellationToken),
                SemanticWorkflowActions.ValidateAction => await ValidateActionAsync(bridgeClient, request, step, cancellationToken),
                SemanticWorkflowActions.ValidateMutation => await ValidateMutationAsync(bridgeClient, request, step, cancellationToken),
                SemanticWorkflowActions.Screenshot => await ScreenshotAsync(bridgeClient, request, step, stepIndex, cancellationToken),
                SemanticWorkflowActions.Inspect => await InspectAsync(bridgeClient, request, step, cancellationToken),
                SemanticWorkflowActions.AssertState => await AssertStateAsync(bridgeClient, request, step, cancellationToken),
                SemanticWorkflowActions.PickerResult => ConsumePickerResult(bridgeClient, request, step),
                SemanticWorkflowActions.Click => await InputAsync(bridgeClient, request, step, InputActions.Click, cancellationToken),
                SemanticWorkflowActions.TypeText => await InputAsync(bridgeClient, request, step, InputActions.KeyText, cancellationToken),
                SemanticWorkflowActions.ClearText => await InputAsync(bridgeClient, request, step, InputActions.ClearText, cancellationToken),
                SemanticWorkflowActions.Focus => await InputAsync(bridgeClient, request, step, InputActions.Focus, cancellationToken),
                SemanticWorkflowActions.Invoke => await InputAsync(bridgeClient, request, step, InputActions.Invoke, cancellationToken),
                SemanticWorkflowActions.Select => await InputAsync(bridgeClient, request, step, InputActions.Select, cancellationToken),
                SemanticWorkflowActions.Toggle => await InputAsync(bridgeClient, request, step, InputActions.Toggle, cancellationToken),
                SemanticWorkflowActions.Expand => await InputAsync(bridgeClient, request, step, InputActions.Expand, cancellationToken),
                SemanticWorkflowActions.Collapse => await InputAsync(bridgeClient, request, step, InputActions.Collapse, cancellationToken),
                SemanticWorkflowActions.KeyDown => await InputAsync(bridgeClient, request, step, InputActions.KeyDown, cancellationToken),
                SemanticWorkflowActions.KeyUp => await InputAsync(bridgeClient, request, step, InputActions.KeyUp, cancellationToken),
                SemanticWorkflowActions.Drag => await InputAsync(bridgeClient, request, step, InputActions.Drag, cancellationToken),
                SemanticWorkflowActions.Swipe => await InputAsync(bridgeClient, request, step, InputActions.Swipe, cancellationToken),
                SemanticWorkflowActions.LongPress => await InputAsync(bridgeClient, request, step, InputActions.LongPress, cancellationToken),
                SemanticWorkflowActions.PressAndHold => await InputAsync(bridgeClient, request, step, InputActions.PressAndHold, cancellationToken),
                SemanticWorkflowActions.CustomActions => await CustomActionsAsync(bridgeClient, request, step, cancellationToken),
                SemanticWorkflowActions.CustomAction => await CustomActionAsync(bridgeClient, request, step, cancellationToken),
                _ => Fail(step, "semantic_workflow_action_not_supported", $"Workflow action '{step.Action}' is not supported.")
            };
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
        CancellationToken cancellationToken)
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
                if (condition.Kind is SemanticWaitConditionKinds.TopLevelOpened
                    or SemanticWaitConditionKinds.TopLevelClosed)
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
                else
                {
                    var target = await ResolveTargetAsync(
                        bridgeClient,
                        request,
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
                            request.SessionId,
                            request.TopLevelId,
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
                return WaitConditionFailure(
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
                    sawAvailable);
            }

            if (lastObservation?.Matched == true)
            {
                var metadata = CreateWaitMetadata(
                    timeoutMs,
                    pollMs,
                    attempts,
                    stopwatch.ElapsedMilliseconds);
                CopyObservation(metadata, lastObservation);
                return Pass(
                    step,
                    $"Wait condition '{condition.Kind}' matched after {attempts.ToString(CultureInfo.InvariantCulture)} attempt(s).",
                    lastTarget,
                    inspection: lastInspection,
                    metadata: metadata,
                    waitObservation: lastObservation);
            }

            if (stopwatch.ElapsedMilliseconds >= timeoutMs)
            {
                return WaitConditionFailure(
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
                    sawAvailable);
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
            request.TopLevelId,
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
                request.TopLevelId,
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

        var authorizedDestructive = request.AllowDestructive || !string.IsNullOrWhiteSpace(request.IsolatedStateDirectory);
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
            && !request.AllowDestructive
            && string.IsNullOrWhiteSpace(request.IsolatedStateDirectory))
        {
            return Fail(
                step,
                "semantic_workflow_destructive_target_requires_isolation",
                "The selected target looks destructive; provide isolatedStateDirectory or set allowDestructive explicitly.",
                resolvedTarget.Target);
        }

        var result = await bridgeClient.InputAsync(
            request.SessionId,
            request.TopLevelId,
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
                && !request.AllowDestructive
                && string.IsNullOrWhiteSpace(request.IsolatedStateDirectory))
            {
                return Fail(
                    step,
                    "semantic_workflow_destructive_target_requires_isolation",
                    "The re-resolved target looks destructive; provide isolatedStateDirectory or set allowDestructive explicitly.",
                    resolvedTarget.Target);
            }

            result = await bridgeClient.InputAsync(
                request.SessionId,
                request.TopLevelId,
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
            request.TopLevelId,
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
            request.TopLevelId,
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
        CancellationToken cancellationToken)
    {
        var path = ResolveScreenshotPath(request, step, stepIndex);
        if (string.IsNullOrWhiteSpace(path))
        {
            return Fail(step, "semantic_workflow_screenshot_path_required", "Screenshot steps require screenshotPath or workflow outputDirectory.");
        }

        var result = await bridgeClient.CaptureScreenshotAsync(
            request.SessionId,
            request.TopLevelId,
            path,
            cancellationToken);

        return result.Success
            ? Pass(step, "Screenshot captured.", result.Value!.Target, screenshot: result.Value)
            : Fail(step, ToProtocolError(result.Error!));
    }

    private static Task<SemanticWorkflowStepResult> CaptureStepScreenshotAsync(
        LocalBridgeClient bridgeClient,
        SemanticWorkflowRequest request,
        SemanticWorkflowStep step,
        int stepIndex,
        CancellationToken cancellationToken)
    {
        var screenshotStep = new SemanticWorkflowStep(
            SemanticWorkflowActions.Screenshot,
            $"{step.Id}:screenshot",
            screenshotPath: ResolveScreenshotPath(request, step, stepIndex));
        return ScreenshotAsync(bridgeClient, request, screenshotStep, stepIndex, cancellationToken);
    }

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
                request.TopLevelId,
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
            request.TopLevelId,
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
                    request.TopLevelId)));
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
            request.TopLevelId,
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
                CreateAmbiguityDetails(selector, matches, request.TopLevelId)));
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
