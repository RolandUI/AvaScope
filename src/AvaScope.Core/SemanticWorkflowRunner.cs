using System.Globalization;
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
        var isolatedStateStatus = string.IsNullOrWhiteSpace(request.IsolatedStateDirectory)
            ? "not_configured"
            : "declared_by_request";

        if (!string.IsNullOrWhiteSpace(request.OutputDirectory))
        {
            Directory.CreateDirectory(request.OutputDirectory);
        }

        foreach (var step in request.Steps)
        {
            var result = await ExecuteStepAsync(bridgeClient, request, step, results.Count, cancellationToken);
            results.Add(result);

            if (request.CaptureAfterEachStep
                && result.Status == "passed"
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
                ["selectorMode"] = "automation_text_name_type_binding_or_node_id"
            }));
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
                SemanticWorkflowActions.Screenshot => await ScreenshotAsync(bridgeClient, request, step, stepIndex, cancellationToken),
                SemanticWorkflowActions.Inspect => await InspectAsync(bridgeClient, request, step, cancellationToken),
                SemanticWorkflowActions.AssertState => await AssertStateAsync(bridgeClient, request, step, cancellationToken),
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
        var selector = step.Selector;
        if (selector is null || !selector.HasSearchCriteria)
        {
            return CoreResult<ResolvedWorkflowTarget>.Fail(new CoreError(
                CoreErrorCodes.InvalidBridgeRequest,
                "Workflow step requires a selector."));
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
                "Workflow selector did not match any node.",
                CreateSelectorDetails(selector)));
        }

        if (result.Value.Matches.Count > 1)
        {
            return CoreResult<ResolvedWorkflowTarget>.Fail(new CoreError(
                CoreErrorCodes.InvalidBridgeRequest,
                "Workflow selector matched multiple nodes; make the selector more specific.",
                CreateSelectorDetails(selector)));
        }

        var match = result.Value.Matches[0];
        return CoreResult<ResolvedWorkflowTarget>.Ok(CreateResolvedTarget(match.Node));
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
        if (step.Action is not SemanticWorkflowActions.Click
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
        IReadOnlyDictionary<string, string>? metadata = null)
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
            metadata: metadata);
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
        RuntimeTargetContext? target = null)
    {
        return new SemanticWorkflowStepResult(
            step.Id,
            step.Action,
            "failed",
            error.Message,
            DateTimeOffset.UtcNow,
            target,
            diagnostics: [error]);
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
