using System.Globalization;
using System.Diagnostics;
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
        if (step.Selector is null || !step.Selector.HasSearchCriteria)
        {
            return Fail(step, "semantic_workflow_selector_required", "wait_for_node requires a selector.");
        }

        var timeoutMs = step.TimeoutMs ?? 5_000;
        var pollMs = step.PollIntervalMs ?? 100;
        var stopwatch = Stopwatch.StartNew();
        var attempts = 0;
        CoreError? lastError = null;
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            attempts++;
            using var attemptCancellation = CreateWaitAttemptCancellation(
                stopwatch,
                timeoutMs,
                cancellationToken);
            CoreResult<ResolvedWorkflowTarget> target;
            try
            {
                target = await ResolveTargetAsync(
                    bridgeClient,
                    request,
                    step,
                    attemptCancellation.Token);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                return WaitTimeout(
                    step,
                    timeoutMs,
                    pollMs,
                    attempts,
                    stopwatch.ElapsedMilliseconds,
                    lastError);
            }
            if (target.Success)
            {
                return Pass(
                    step,
                    $"Node became available after {attempts.ToString(CultureInfo.InvariantCulture)} attempt(s).",
                    target.Value!.Target,
                    metadata: CreateWaitMetadata(timeoutMs, pollMs, attempts, stopwatch.ElapsedMilliseconds));
            }

            lastError = target.Error;
            if (stopwatch.ElapsedMilliseconds >= timeoutMs)
            {
                return WaitTimeout(step, timeoutMs, pollMs, attempts, stopwatch.ElapsedMilliseconds, lastError);
            }

            await DelayUntilNextPollAsync(stopwatch, timeoutMs, pollMs, cancellationToken);
        }
    }

    private static async Task<SemanticWorkflowStepResult> WaitForStateAsync(
        LocalBridgeClient bridgeClient,
        SemanticWorkflowRequest request,
        SemanticWorkflowStep step,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(step.AssertProperty))
        {
            return Fail(step, "semantic_workflow_assert_property_required", "wait_for_state requires assertProperty.");
        }

        if (step.Selector is null || !step.Selector.HasSearchCriteria)
        {
            return Fail(step, "semantic_workflow_selector_required", "wait_for_state requires a selector.");
        }

        var timeoutMs = step.TimeoutMs ?? 5_000;
        var pollMs = step.PollIntervalMs ?? 100;
        var stopwatch = Stopwatch.StartNew();
        var attempts = 0;
        CoreError? lastError = null;
        InspectNodeResponse? lastInspection = null;
        RuntimeTargetContext? lastTarget = null;
        string? lastActual = null;

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            attempts++;
            using var attemptCancellation = CreateWaitAttemptCancellation(
                stopwatch,
                timeoutMs,
                cancellationToken);
            CoreResult<ResolvedWorkflowTarget> target;
            try
            {
                target = await ResolveTargetAsync(
                    bridgeClient,
                    request,
                    step,
                    attemptCancellation.Token);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                var timeoutMetadata = CreateWaitMetadata(
                    timeoutMs,
                    pollMs,
                    attempts,
                    stopwatch.ElapsedMilliseconds);
                timeoutMetadata["assertProperty"] = step.AssertProperty;
                timeoutMetadata["actual"] = lastActual ?? "unavailable";
                timeoutMetadata["expected"] = step.Expected ?? "null";
                return Fail(
                    step,
                    "semantic_workflow_wait_timeout",
                    $"Timed out waiting for '{step.AssertProperty}'.",
                    lastTarget,
                    lastInspection,
                    timeoutMetadata);
            }
            if (target.Success)
            {
                lastTarget = target.Value!.Target;
                CoreResult<InspectNodeResponse> inspect;
                try
                {
                    inspect = await bridgeClient.InspectNodeAsync(
                        request.SessionId,
                        request.TopLevelId,
                        lastTarget.TreeKind ?? TreeKinds.Visual,
                        lastTarget.NodeId!,
                        attemptCancellation.Token);
                }
                catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                {
                    var timeoutMetadata = CreateWaitMetadata(
                        timeoutMs,
                        pollMs,
                        attempts,
                        stopwatch.ElapsedMilliseconds);
                    timeoutMetadata["assertProperty"] = step.AssertProperty;
                    timeoutMetadata["actual"] = lastActual ?? "unavailable";
                    timeoutMetadata["expected"] = step.Expected ?? "null";
                    return Fail(
                        step,
                        "semantic_workflow_wait_timeout",
                        $"Timed out waiting for '{step.AssertProperty}'.",
                        lastTarget,
                        lastInspection,
                        timeoutMetadata);
                }
                if (inspect.Success)
                {
                    lastInspection = inspect.Value;
                    lastActual = ReadInspectableValue(inspect.Value!, step.AssertProperty);
                    if (string.Equals(lastActual, step.Expected, StringComparison.Ordinal))
                    {
                        var metadata = CreateWaitMetadata(
                            timeoutMs,
                            pollMs,
                            attempts,
                            stopwatch.ElapsedMilliseconds);
                        metadata["assertProperty"] = step.AssertProperty;
                        metadata["actual"] = lastActual ?? "null";
                        metadata["expected"] = step.Expected ?? "null";
                        return Pass(
                            step,
                            $"State '{step.AssertProperty}' reached the expected value.",
                            lastTarget,
                            inspection: lastInspection,
                            metadata: metadata);
                    }
                }
                else
                {
                    lastError = inspect.Error;
                }
            }
            else
            {
                lastError = target.Error;
            }

            if (stopwatch.ElapsedMilliseconds >= timeoutMs)
            {
                var metadata = CreateWaitMetadata(
                    timeoutMs,
                    pollMs,
                    attempts,
                    stopwatch.ElapsedMilliseconds);
                metadata["assertProperty"] = step.AssertProperty;
                metadata["actual"] = lastActual ?? "unavailable";
                metadata["expected"] = step.Expected ?? "null";
                CopyLastError(metadata, lastError);
                return Fail(
                    step,
                    "semantic_workflow_wait_timeout",
                    $"Timed out waiting for '{step.AssertProperty}'.",
                    lastTarget,
                    lastInspection,
                    metadata);
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
            cancellationToken: cancellationToken);
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

    private static SemanticWorkflowStepResult WaitTimeout(
        SemanticWorkflowStep step,
        int timeoutMs,
        int pollMs,
        int attempts,
        long elapsedMs,
        CoreError? lastError)
    {
        var metadata = CreateWaitMetadata(timeoutMs, pollMs, attempts, elapsedMs);
        CopyLastError(metadata, lastError);
        return Fail(
            step,
            "semantic_workflow_wait_timeout",
            "Timed out waiting for a matching node.",
            metadata: metadata);
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
            cancellationToken: cancellationToken);

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

            return inspect.Success
                ? CoreResult<ResolvedWorkflowTarget>.Ok(CreateResolvedTarget(inspect.Value!))
                : CoreResult<ResolvedWorkflowTarget>.Fail(inspect.Error!);
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
            nodeType,
            selector.Name,
            selector.AutomationId,
            selector.Text,
            selector.MaxDepth ?? request.MaxDepth,
            maxResults: 2,
            cancellationToken);

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
                CreateSelectorDetails(selector)));
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
            .Take(2)
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
                CreateSelectorDetails(selector)));
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

        return true;
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
        RuntimeMutationResponse? mutation = null)
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
            mutation: mutation);
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
        IReadOnlyDictionary<string, string>? metadata = null)
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
            metadata: metadata);
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

        return details;
    }

    private static void CopyDetail(IDictionary<string, string> details, string key, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            details[key] = value;
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
}
