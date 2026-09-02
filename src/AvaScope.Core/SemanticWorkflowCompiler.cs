using System.Text.RegularExpressions;
using AvaScope.Protocol;

namespace AvaScope.Core;

internal static partial class SemanticWorkflowCompiler
{
    private const int MaximumDiagnostics = 128;

    public static CompiledSemanticWorkflow Compile(SemanticWorkflowRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var context = new CompilationContext(request);
        context.ValidateDefinitions();
        var roots = context.CompileSteps(
            request.Steps,
            parentPath: null,
            parentStepId: null,
            branch: null,
            sourceFragment: null,
            context.GlobalVariables,
            fragmentStack: [],
            depth: 0,
            retryMultiplier: 1,
            insideRetry: false);

        var estimatedExecutions = roots.Sum(node => EstimateMaximumExecutions(node, request.CaptureAfterEachStep));
        var maximumArtifacts = roots.Sum(node => EstimateMaximumArtifacts(node, request.CaptureAfterEachStep));
        if (context.TotalSteps > SemanticWorkflowLimits.MaximumExpandedSteps)
        {
            context.AddDiagnostic(
                "semantic_workflow_expanded_step_limit",
                $"Expanded workflow contains {context.TotalSteps} steps; the limit is {SemanticWorkflowLimits.MaximumExpandedSteps}.",
                "workflow",
                SemanticWorkflowLimits.MaximumExpandedSteps);
        }

        if (estimatedExecutions > SemanticWorkflowLimits.MaximumEstimatedExecutions)
        {
            context.AddDiagnostic(
                "semantic_workflow_execution_limit",
                $"Workflow may produce {estimatedExecutions} executions; the limit is {SemanticWorkflowLimits.MaximumEstimatedExecutions}.",
                "workflow",
                SemanticWorkflowLimits.MaximumEstimatedExecutions);
        }

        if (context.TotalRetryIterations > SemanticWorkflowLimits.MaximumTotalRetryIterations)
        {
            context.AddDiagnostic(
                "semantic_workflow_retry_iteration_limit",
                $"Workflow may perform {context.TotalRetryIterations} retry iterations; the limit is {SemanticWorkflowLimits.MaximumTotalRetryIterations}.",
                "workflow",
                SemanticWorkflowLimits.MaximumTotalRetryIterations);
        }

        if (maximumArtifacts > SemanticWorkflowLimits.MaximumArtifacts)
        {
            context.AddDiagnostic(
                "semantic_workflow_artifact_limit",
                $"Workflow may create {maximumArtifacts} screenshot artifacts; the limit is {SemanticWorkflowLimits.MaximumArtifacts}.",
                "workflow",
                SemanticWorkflowLimits.MaximumArtifacts);
        }

        var diagnostics = context.Diagnostics.ToArray();
        var plan = new SemanticWorkflowPlan(
            diagnostics.Length == 0,
            context.TotalSteps,
            estimatedExecutions,
            context.MaximumDepth,
            maximumArtifacts,
            context.PlanItems.ToArray(),
            diagnostics);
        return new CompiledSemanticWorkflow(plan, roots);
    }

    private static int EstimateMaximumExecutions(CompiledWorkflowNode node, bool captureAfterEachStep)
    {
        var nested = node.Primary.Sum(child => EstimateMaximumExecutions(child, captureAfterEachStep))
            + node.Alternate.Sum(child => EstimateMaximumExecutions(child, captureAfterEachStep));
        var self = 1 + (captureAfterEachStep
            && IsExecutableLeaf(node.Step.Action)
            && node.Step.Action is not (SemanticWorkflowActions.Wait or SemanticWorkflowActions.Screenshot)
                ? 1
                : 0);
        return node.Step.Action == SemanticWorkflowActions.RetryUntil
            ? checked(Math.Clamp(
                node.Step.MaxAttempts ?? 1,
                1,
                SemanticWorkflowLimits.MaximumRetryAttempts) * (nested + 1))
            : checked(self + nested);
    }

    private static int EstimateMaximumArtifacts(CompiledWorkflowNode node, bool captureAfterEachStep)
    {
        var self = node.Step.Action == SemanticWorkflowActions.Screenshot
            ? 1
            : captureAfterEachStep && IsExecutableLeaf(node.Step.Action)
                && node.Step.Action != SemanticWorkflowActions.Wait
                ? 1
                : 0;
        var primary = node.Primary.Sum(child => EstimateMaximumArtifacts(child, captureAfterEachStep));
        var alternate = node.Alternate.Sum(child => EstimateMaximumArtifacts(child, captureAfterEachStep));
        var nested = node.Step.Action == SemanticWorkflowActions.If
            ? Math.Max(primary, alternate)
            : primary + alternate;
        return node.Step.Action == SemanticWorkflowActions.RetryUntil
            ? checked(Math.Clamp(
                node.Step.MaxAttempts ?? 1,
                1,
                SemanticWorkflowLimits.MaximumRetryAttempts) * (self + nested))
            : checked(self + nested);
    }

    private static bool IsExecutableLeaf(string action) => action is not (
        SemanticWorkflowActions.If
        or SemanticWorkflowActions.RetryUntil
        or SemanticWorkflowActions.UseFragment);

    private static bool IsSideEffecting(string action) => action is
        SemanticWorkflowActions.Click
        or SemanticWorkflowActions.TypeText
        or SemanticWorkflowActions.ClearText
        or SemanticWorkflowActions.Focus
        or SemanticWorkflowActions.Invoke
        or SemanticWorkflowActions.Select
        or SemanticWorkflowActions.Toggle
        or SemanticWorkflowActions.Expand
        or SemanticWorkflowActions.Collapse
        or SemanticWorkflowActions.KeyDown
        or SemanticWorkflowActions.KeyUp
        or SemanticWorkflowActions.Drag
        or SemanticWorkflowActions.Swipe
        or SemanticWorkflowActions.LongPress
        or SemanticWorkflowActions.PressAndHold
        or SemanticWorkflowActions.CustomAction
        or SemanticWorkflowActions.PickerResult;

    private static bool RequiresSelector(string action) => action is
        SemanticWorkflowActions.Click
        or SemanticWorkflowActions.TypeText
        or SemanticWorkflowActions.ClearText
        or SemanticWorkflowActions.Focus
        or SemanticWorkflowActions.Invoke
        or SemanticWorkflowActions.Select
        or SemanticWorkflowActions.Toggle
        or SemanticWorkflowActions.Expand
        or SemanticWorkflowActions.Collapse
        or SemanticWorkflowActions.KeyDown
        or SemanticWorkflowActions.KeyUp
        or SemanticWorkflowActions.Drag
        or SemanticWorkflowActions.Swipe
        or SemanticWorkflowActions.LongPress
        or SemanticWorkflowActions.PressAndHold
        or SemanticWorkflowActions.CustomActions
        or SemanticWorkflowActions.CustomAction
        or SemanticWorkflowActions.AssertState
        or SemanticWorkflowActions.Inspect
        or SemanticWorkflowActions.ValidateAction
        or SemanticWorkflowActions.ValidateMutation;

    [GeneratedRegex(@"\$\{(?<name>[A-Za-z_][A-Za-z0-9_.-]*)\}", RegexOptions.CultureInvariant)]
    private static partial Regex VariablePattern();

    private sealed class CompilationContext
    {
        private readonly SemanticWorkflowRequest _request;
        private readonly Dictionary<string, SemanticWorkflowFragment> _fragments = new(StringComparer.Ordinal);
        private readonly HashSet<string> _diagnosticKeys = new(StringComparer.Ordinal);
        private bool _planTruncated;

        public CompilationContext(SemanticWorkflowRequest request)
        {
            _request = request;
            GlobalVariables = new Dictionary<string, string>(request.Variables, StringComparer.Ordinal);
        }

        public Dictionary<string, string> GlobalVariables { get; }

        public List<ProtocolError> Diagnostics { get; } = [];

        public List<SemanticWorkflowPlanItem> PlanItems { get; } = [];

        public int TotalSteps { get; private set; }

        public int TotalRetryIterations { get; private set; }

        public int MaximumDepth { get; private set; }

        public void ValidateDefinitions()
        {
            if (_request.Variables.Count > SemanticWorkflowLimits.MaximumVariables)
            {
                AddDiagnostic(
                    "semantic_workflow_variable_limit",
                    $"Workflow declares {_request.Variables.Count} variables; the limit is {SemanticWorkflowLimits.MaximumVariables}.",
                    "variables",
                    SemanticWorkflowLimits.MaximumVariables);
            }

            foreach (var pair in _request.Variables)
            {
                if (string.IsNullOrWhiteSpace(pair.Key))
                {
                    AddDiagnostic("semantic_workflow_variable_invalid", "Workflow variable names cannot be empty.", "variables");
                    continue;
                }

                if (pair.Value is null)
                {
                    AddDiagnostic(
                        "semantic_workflow_variable_invalid",
                        $"Workflow variable '{pair.Key}' cannot have a null value.",
                        $"variables.{pair.Key}");
                    continue;
                }

                _ = ResolveVariable(pair.Key, GlobalVariables, [], $"variables.{pair.Key}");
            }

            if (_request.Fragments.Count > SemanticWorkflowLimits.MaximumFragments)
            {
                AddDiagnostic(
                    "semantic_workflow_fragment_limit",
                    $"Workflow declares {_request.Fragments.Count} fragments; the limit is {SemanticWorkflowLimits.MaximumFragments}.",
                    "fragments",
                    SemanticWorkflowLimits.MaximumFragments);
            }

            foreach (var fragment in _request.Fragments)
            {
                if (!_fragments.TryAdd(fragment.Name, fragment))
                {
                    AddDiagnostic(
                        "semantic_workflow_fragment_duplicate",
                        $"Workflow fragment '{fragment.Name}' is declared more than once.",
                        $"fragments.{fragment.Name}");
                }

                if (fragment.Parameters.Count > SemanticWorkflowLimits.MaximumFragmentParameters)
                {
                    AddDiagnostic(
                        "semantic_workflow_fragment_parameter_limit",
                        $"Fragment '{fragment.Name}' declares {fragment.Parameters.Count} parameters; the limit is {SemanticWorkflowLimits.MaximumFragmentParameters}.",
                        $"fragments.{fragment.Name}",
                        SemanticWorkflowLimits.MaximumFragmentParameters);
                }

                var duplicates = fragment.Parameters
                    .Where(static parameter => !string.IsNullOrWhiteSpace(parameter))
                    .GroupBy(static parameter => parameter, StringComparer.Ordinal)
                    .Where(static group => group.Count() > 1)
                    .Select(static group => group.Key);
                foreach (var duplicate in duplicates)
                {
                    AddDiagnostic(
                        "semantic_workflow_fragment_parameter_duplicate",
                        $"Fragment '{fragment.Name}' declares parameter '{duplicate}' more than once.",
                        $"fragments.{fragment.Name}");
                }

                if (fragment.Parameters.Any(static parameter => string.IsNullOrWhiteSpace(parameter)))
                {
                    AddDiagnostic(
                        "semantic_workflow_fragment_parameter_invalid",
                        $"Fragment '{fragment.Name}' contains an empty parameter name.",
                        $"fragments.{fragment.Name}");
                }
            }

            foreach (var fragment in _fragments.Values)
            {
                ValidateFragmentGraph(fragment, []);
                var allowedVariables = new HashSet<string>(GlobalVariables.Keys, StringComparer.Ordinal);
                allowedVariables.UnionWith(fragment.Parameters);
                ValidateTemplateVariables(fragment.Steps, allowedVariables, $"fragments.{fragment.Name}");
            }
        }

        public IReadOnlyList<CompiledWorkflowNode> CompileSteps(
            IReadOnlyList<SemanticWorkflowStep> steps,
            string? parentPath,
            string? parentStepId,
            string? branch,
            string? sourceFragment,
            IReadOnlyDictionary<string, string> variables,
            IReadOnlyList<string> fragmentStack,
            int depth,
            int retryMultiplier,
            bool insideRetry)
        {
            var compiled = new List<CompiledWorkflowNode>();
            MaximumDepth = Math.Max(MaximumDepth, depth);
            if (depth > SemanticWorkflowLimits.MaximumNestingDepth)
            {
                AddDiagnostic(
                    "semantic_workflow_nesting_limit",
                    $"Workflow nesting depth exceeds {SemanticWorkflowLimits.MaximumNestingDepth}.",
                    parentPath ?? "workflow",
                    SemanticWorkflowLimits.MaximumNestingDepth);
                return compiled;
            }

            for (var index = 0; index < steps.Count; index++)
            {
                if (TotalSteps > SemanticWorkflowLimits.MaximumExpandedSteps)
                {
                    break;
                }

                var rawStep = steps[index];
                var provisionalPath = string.IsNullOrWhiteSpace(parentPath)
                    ? $"{index + 1}"
                    : $"{parentPath}/{branch ?? "steps"}/{index + 1}";
                var step = ResolveStep(rawStep, variables, provisionalPath);
                var path = $"{provisionalPath}:{step.Id}";
                TotalSteps++;
                if (PlanItems.Count < SemanticWorkflowLimits.MaximumExpandedSteps)
                {
                    PlanItems.Add(new SemanticWorkflowPlanItem(
                        PlanItems.Count + 1,
                        path,
                        step.Id,
                        step.Action,
                        depth,
                        step.Optional,
                        parentStepId,
                        branch,
                        sourceFragment,
                        step.MaxAttempts,
                        step.TopLevelAlias));
                }
                else if (!_planTruncated)
                {
                    _planTruncated = true;
                    AddDiagnostic(
                        "semantic_workflow_plan_truncated",
                        $"Expanded plan output is limited to {SemanticWorkflowLimits.MaximumExpandedSteps} entries.",
                        path,
                        SemanticWorkflowLimits.MaximumExpandedSteps);
                }

                ValidateStepShape(step, path, insideRetry);
                IReadOnlyList<CompiledWorkflowNode> primary = [];
                IReadOnlyList<CompiledWorkflowNode> alternate = [];
                switch (step.Action)
                {
                    case SemanticWorkflowActions.If:
                        primary = CompileSteps(
                            rawStep.Then, path, step.Id, "then", sourceFragment, variables,
                            fragmentStack, depth + 1, retryMultiplier, insideRetry);
                        alternate = CompileSteps(
                            rawStep.Else, path, step.Id, "else", sourceFragment, variables,
                            fragmentStack, depth + 1, retryMultiplier, insideRetry);
                        break;
                    case SemanticWorkflowActions.RetryUntil:
                        var attempts = Math.Clamp(
                            step.MaxAttempts ?? 1,
                            1,
                            SemanticWorkflowLimits.MaximumRetryAttempts);
                        TotalRetryIterations = checked(TotalRetryIterations + (retryMultiplier * attempts));
                        primary = CompileSteps(
                            rawStep.Steps, path, step.Id, "retry", sourceFragment, variables,
                            fragmentStack, depth + 1, checked(retryMultiplier * attempts), insideRetry: true);
                        break;
                    case SemanticWorkflowActions.UseFragment:
                        primary = CompileFragment(step, path, variables, fragmentStack, depth, retryMultiplier, insideRetry);
                        break;
                }

                compiled.Add(new CompiledWorkflowNode(
                    step,
                    path,
                    parentStepId,
                    branch,
                    sourceFragment,
                    primary,
                    alternate));
            }

            return compiled;
        }

        private IReadOnlyList<CompiledWorkflowNode> CompileFragment(
            SemanticWorkflowStep step,
            string path,
            IReadOnlyDictionary<string, string> variables,
            IReadOnlyList<string> fragmentStack,
            int depth,
            int retryMultiplier,
            bool insideRetry)
        {
            if (step.Fragment is null || !_fragments.TryGetValue(step.Fragment, out var fragment))
            {
                return [];
            }

            if (fragmentStack.Contains(fragment.Name, StringComparer.Ordinal))
            {
                AddDiagnostic(
                    "semantic_workflow_fragment_cycle",
                    $"Workflow fragment cycle detected: {string.Join(" -> ", fragmentStack.Append(fragment.Name))}.",
                    path);
                return [];
            }

            var scope = new Dictionary<string, string>(variables, StringComparer.Ordinal);
            foreach (var parameter in fragment.Parameters)
            {
                if (!step.Arguments.TryGetValue(parameter, out var value))
                {
                    AddDiagnostic(
                        "semantic_workflow_fragment_argument_missing",
                        $"Fragment '{fragment.Name}' requires argument '{parameter}'.",
                        path);
                    continue;
                }

                scope[parameter] = ResolveText(value, variables, $"{path}.arguments.{parameter}") ?? value;
            }

            foreach (var argument in step.Arguments.Keys.Except(fragment.Parameters, StringComparer.Ordinal))
            {
                AddDiagnostic(
                    "semantic_workflow_fragment_argument_unknown",
                    $"Fragment '{fragment.Name}' does not declare argument '{argument}'.",
                    path);
            }

            return CompileSteps(
                fragment.Steps,
                path,
                step.Id,
                "fragment",
                fragment.Name,
                scope,
                fragmentStack.Append(fragment.Name).ToArray(),
                depth + 1,
                retryMultiplier,
                insideRetry);
        }

        private void ValidateStepShape(SemanticWorkflowStep step, string path, bool insideRetry)
        {
            if (!SemanticWorkflowActions.All.Contains(step.Action, StringComparer.Ordinal))
            {
                AddDiagnostic(
                    "semantic_workflow_action_not_supported",
                    $"Workflow action '{step.Action}' is not supported.",
                    path);
                return;
            }

            if (step.TopLevelAlias is not null
                && !_request.TopLevelAliases.Any(alias => string.Equals(alias.Alias, step.TopLevelAlias, StringComparison.Ordinal)))
            {
                AddDiagnostic(
                    "semantic_workflow_top_level_alias_unknown",
                    $"Top-level alias '{step.TopLevelAlias}' is not declared by the workflow.",
                    path);
            }

            if (string.IsNullOrWhiteSpace(_request.TopLevelId)
                && step.TopLevelAlias is null
                && step.Action != SemanticWorkflowActions.UseFragment)
            {
                AddDiagnostic(
                    "semantic_workflow_top_level_required",
                    "Workflow steps without topLevelAlias require the request topLevelId.",
                    path);
            }

            if (step.Arguments.Count > SemanticWorkflowLimits.MaximumFragmentParameters)
            {
                AddDiagnostic(
                    "semantic_workflow_fragment_argument_limit",
                    $"Step declares {step.Arguments.Count} fragment arguments; the limit is {SemanticWorkflowLimits.MaximumFragmentParameters}.",
                    path,
                    SemanticWorkflowLimits.MaximumFragmentParameters);
            }

            if (step.Arguments.Any(static pair => string.IsNullOrWhiteSpace(pair.Key) || pair.Value is null))
            {
                AddDiagnostic(
                    "semantic_workflow_fragment_argument_invalid",
                    "Fragment argument names cannot be empty and values cannot be null.",
                    path);
            }

            var isControl = step.Action is SemanticWorkflowActions.If
                or SemanticWorkflowActions.RetryUntil
                or SemanticWorkflowActions.UseFragment;
            if (step.Optional && isControl)
            {
                AddDiagnostic(
                    "semantic_workflow_optional_control_not_supported",
                    "Optional applies to executable leaf steps, not composition controls.",
                    path);
            }

            if (insideRetry && IsSideEffecting(step.Action) && string.IsNullOrWhiteSpace(step.IdempotencyKey))
            {
                AddDiagnostic(
                    "semantic_workflow_retry_idempotency_required",
                    $"Retry body side-effect action '{step.Action}' requires idempotencyKey to prevent duplicate dispatch.",
                    path);
            }

            if (RequiresSelector(step.Action)
                && (step.Selector is null || !step.Selector.HasSearchCriteria))
            {
                AddDiagnostic(
                    "semantic_workflow_selector_required",
                    $"Workflow action '{step.Action}' requires a selector.",
                    path);
            }

            if (step.Action == SemanticWorkflowActions.AssertState
                && string.IsNullOrWhiteSpace(step.AssertProperty))
            {
                AddDiagnostic(
                    "semantic_workflow_assert_property_required",
                    "assert_state requires assertProperty.",
                    path);
            }

            if (step.Action == SemanticWorkflowActions.ValidateAction
                && string.IsNullOrWhiteSpace(step.InputAction))
            {
                AddDiagnostic(
                    "semantic_workflow_input_action_required",
                    "validate_action requires inputAction.",
                    path);
            }

            if (step.Action == SemanticWorkflowActions.ValidateMutation && step.Mutation is null)
            {
                AddDiagnostic(
                    "semantic_workflow_mutation_required",
                    "validate_mutation requires mutation.",
                    path);
            }

            if (step.Action == SemanticWorkflowActions.CustomAction
                && string.IsNullOrWhiteSpace(step.CustomActionName))
            {
                AddDiagnostic(
                    "semantic_workflow_custom_action_name_required",
                    "custom_action requires customActionName.",
                    path);
            }

            if (step.Action == SemanticWorkflowActions.Screenshot
                && string.IsNullOrWhiteSpace(step.ScreenshotPath)
                && string.IsNullOrWhiteSpace(_request.OutputDirectory))
            {
                AddDiagnostic(
                    "semantic_workflow_screenshot_path_required",
                    "screenshot requires screenshotPath or workflow outputDirectory.",
                    path);
            }

            if (step.Action == SemanticWorkflowActions.WaitForNode
                && (step.Selector is null || !step.Selector.HasSearchCriteria))
            {
                AddDiagnostic(
                    "semantic_workflow_selector_required",
                    "wait_for_node requires a selector.",
                    path);
            }

            if (step.Action == SemanticWorkflowActions.WaitForState)
            {
                var condition = step.WaitCondition;
                if (condition is null && string.IsNullOrWhiteSpace(step.AssertProperty))
                {
                    AddDiagnostic(
                        "semantic_workflow_wait_condition_required",
                        "wait_for_state requires waitCondition or assertProperty.",
                        path);
                }

                var topLevelCondition = condition?.Kind is SemanticWaitConditionKinds.TopLevelOpened
                    or SemanticWaitConditionKinds.TopLevelClosed;
                if (!topLevelCondition && (step.Selector is null || !step.Selector.HasSearchCriteria))
                {
                    AddDiagnostic(
                        "semantic_workflow_selector_required",
                        "wait_for_state requires a selector unless it observes top-level lifetime.",
                        path);
                }
            }

            switch (step.Action)
            {
                case SemanticWorkflowActions.If:
                    if (step.Then.Count == 0 && step.Else.Count == 0)
                    {
                        AddDiagnostic("semantic_workflow_branch_empty", "if requires then or else steps.", path);
                    }

                    ValidateCondition(step, path);
                    if (step.Steps.Count > 0 || step.Fragment is not null || step.MaxAttempts.HasValue)
                    {
                        AddDiagnostic("semantic_workflow_branch_shape_invalid", "if accepts then/else but not steps, fragment, or maxAttempts.", path);
                    }

                    break;
                case SemanticWorkflowActions.RetryUntil:
                    if (step.Steps.Count == 0)
                    {
                        AddDiagnostic("semantic_workflow_retry_steps_required", "retry_until requires steps.", path);
                    }

                    if (!step.MaxAttempts.HasValue)
                    {
                        AddDiagnostic("semantic_workflow_retry_bound_required", "retry_until requires maxAttempts.", path);
                    }
                    else if (step.MaxAttempts is < 1 or > SemanticWorkflowLimits.MaximumRetryAttempts)
                    {
                        AddDiagnostic(
                            "semantic_workflow_retry_attempt_limit",
                            $"retry_until maxAttempts must be between 1 and {SemanticWorkflowLimits.MaximumRetryAttempts}.",
                            path,
                            SemanticWorkflowLimits.MaximumRetryAttempts);
                    }

                    if (step.RetryDelayMs is < 0 or > 5000)
                    {
                        AddDiagnostic(
                            "semantic_workflow_retry_delay_limit",
                            "retry_until retryDelayMs must be between 0 and 5000 ms.",
                            path,
                            5000);
                    }

                    ValidateCondition(step, path);
                    if (step.Then.Count > 0 || step.Else.Count > 0 || step.Fragment is not null)
                    {
                        AddDiagnostic("semantic_workflow_retry_shape_invalid", "retry_until accepts steps but not then, else, or fragment.", path);
                    }

                    break;
                case SemanticWorkflowActions.UseFragment:
                    if (step.Fragment is null)
                    {
                        AddDiagnostic("semantic_workflow_fragment_required", "use_fragment requires fragment.", path);
                    }
                    else if (!_fragments.ContainsKey(step.Fragment))
                    {
                        AddDiagnostic(
                            "semantic_workflow_fragment_unresolved",
                            $"Workflow fragment '{step.Fragment}' is not declared.",
                            path);
                    }

                    if (step.Then.Count > 0 || step.Else.Count > 0 || step.Steps.Count > 0 || step.MaxAttempts.HasValue)
                    {
                        AddDiagnostic("semantic_workflow_fragment_shape_invalid", "use_fragment accepts fragment/arguments but not nested steps or maxAttempts.", path);
                    }

                    break;
                default:
                    if (step.Then.Count > 0 || step.Else.Count > 0 || step.Steps.Count > 0 || step.Fragment is not null
                        || step.MaxAttempts.HasValue || step.RetryDelayMs.HasValue || step.Arguments.Count > 0)
                    {
                        AddDiagnostic(
                            "semantic_workflow_leaf_shape_invalid",
                            $"Leaf action '{step.Action}' cannot declare composition fields.",
                            path);
                    }

                    break;
            }
        }

        private void ValidateCondition(SemanticWorkflowStep step, string path)
        {
            if (step.WaitCondition is null)
            {
                AddDiagnostic(
                    "semantic_workflow_condition_required",
                    $"{step.Action} requires waitCondition.",
                    path);
                return;
            }

            var topLevelCondition = step.WaitCondition.Kind is SemanticWaitConditionKinds.TopLevelOpened
                or SemanticWaitConditionKinds.TopLevelClosed;
            if (!topLevelCondition && (step.Selector is null || !step.Selector.HasSearchCriteria))
            {
                AddDiagnostic(
                    "semantic_workflow_condition_selector_required",
                    $"{step.Action} requires a selector for condition '{step.WaitCondition.Kind}'.",
                    path);
            }
        }

        private SemanticWorkflowStep ResolveStep(
            SemanticWorkflowStep step,
            IReadOnlyDictionary<string, string> variables,
            string path)
        {
            return new SemanticWorkflowStep(
                ResolveText(step.Action, variables, $"{path}.action") ?? step.Action,
                ResolveText(step.Id, variables, $"{path}.id") ?? step.Id,
                ResolveSelector(step.Selector, variables, path),
                ResolveText(step.Text, variables, $"{path}.text"),
                ResolveText(step.Key, variables, $"{path}.key"),
                ResolveText(step.Modifiers, variables, $"{path}.modifiers"),
                ResolveText(step.AssertProperty, variables, $"{path}.assertProperty"),
                ResolveText(step.Expected, variables, $"{path}.expected"),
                ResolveText(step.ScreenshotPath, variables, $"{path}.screenshotPath"),
                step.WaitMs,
                step.TimeoutMs,
                step.PollIntervalMs,
                ResolveText(step.InputAction, variables, $"{path}.inputAction"),
                ResolveMutation(step.Mutation, variables, path),
                ResolveText(step.IdempotencyKey, variables, $"{path}.idempotencyKey"),
                step.IdempotencyTtlMs,
                ResolveSelector(step.DestinationSelector, variables, path),
                ResolveText(step.Direction, variables, $"{path}.direction"),
                step.DistancePercentage,
                step.DurationMs,
                ResolveText(step.CustomActionName, variables, $"{path}.customActionName"),
                step.CustomActionParameters.ToDictionary(
                    static pair => pair.Key,
                    pair => ResolveText(pair.Value, variables, $"{path}.customActionParameters.{pair.Key}") ?? pair.Value,
                    StringComparer.Ordinal),
                ResolveCondition(step.WaitCondition, variables, path),
                ResolveText(step.TopLevelAlias, variables, $"{path}.topLevelAlias"),
                step.Optional,
                step.Then,
                step.Else,
                step.Steps,
                step.MaxAttempts,
                step.RetryDelayMs,
                ResolveText(step.Fragment, variables, $"{path}.fragment"),
                step.Arguments.ToDictionary(
                    static pair => pair.Key,
                    pair => ResolveText(pair.Value, variables, $"{path}.arguments.{pair.Key}") ?? pair.Value,
                    StringComparer.Ordinal));
        }

        private SemanticWorkflowSelector? ResolveSelector(
            SemanticWorkflowSelector? selector,
            IReadOnlyDictionary<string, string> variables,
            string path)
        {
            return selector is null
                ? null
                : new SemanticWorkflowSelector(
                    ResolveText(selector.NodeId, variables, $"{path}.selector.nodeId"),
                    ResolveText(selector.TreeKind, variables, $"{path}.selector.treeKind"),
                    ResolveText(selector.AutomationId, variables, $"{path}.selector.automationId"),
                    ResolveText(selector.Text, variables, $"{path}.selector.text"),
                    ResolveText(selector.Name, variables, $"{path}.selector.name"),
                    ResolveText(selector.NodeType, variables, $"{path}.selector.nodeType"),
                    ResolveText(selector.Role, variables, $"{path}.selector.role"),
                    ResolveText(selector.BindingPath, variables, $"{path}.selector.bindingPath"),
                    ResolveText(selector.CommandName, variables, $"{path}.selector.commandName"),
                    selector.MaxDepth,
                    selector.Visible,
                    selector.Enabled,
                    selector.Rendered,
                    selector.Actionable);
        }

        private SemanticWaitCondition? ResolveCondition(
            SemanticWaitCondition? condition,
            IReadOnlyDictionary<string, string> variables,
            string path)
        {
            return condition is null
                ? null
                : new SemanticWaitCondition(
                    condition.Kind,
                    ResolveText(condition.Expected, variables, $"{path}.waitCondition.expected"),
                    condition.Comparison,
                    condition.ValueType,
                    ResolveText(condition.PropertyName, variables, $"{path}.waitCondition.propertyName"),
                    ResolveText(condition.BindingPath, variables, $"{path}.waitCondition.bindingPath"),
                    ResolveText(condition.Baseline, variables, $"{path}.waitCondition.baseline"),
                    ResolveText(condition.TopLevelId, variables, $"{path}.waitCondition.topLevelId"),
                    ResolveText(condition.TopLevelTitle, variables, $"{path}.waitCondition.topLevelTitle"));
        }

        private RuntimeMutationOperation? ResolveMutation(
            RuntimeMutationOperation? mutation,
            IReadOnlyDictionary<string, string> variables,
            string path)
        {
            return mutation is null
                ? null
                : new RuntimeMutationOperation(
                    ResolveText(mutation.Kind, variables, $"{path}.mutation.kind") ?? mutation.Kind,
                    ResolveText(mutation.PropertyName, variables, $"{path}.mutation.propertyName"),
                    ResolveText(mutation.Value, variables, $"{path}.mutation.value"),
                    ResolveText(mutation.ValueType, variables, $"{path}.mutation.valueType"),
                    ResolveText(mutation.ClassName, variables, $"{path}.mutation.className"),
                    ResolveText(mutation.ResourceKey, variables, $"{path}.mutation.resourceKey"),
                    ResolveText(mutation.MutationId, variables, $"{path}.mutation.mutationId"));
        }

        private string? ResolveText(
            string? value,
            IReadOnlyDictionary<string, string> variables,
            string path)
        {
            if (value is null || !value.Contains("${", StringComparison.Ordinal))
            {
                return value;
            }

            return ResolveTextCore(value, variables, [], path);
        }

        private string ResolveTextCore(
            string value,
            IReadOnlyDictionary<string, string> variables,
            IReadOnlyList<string> stack,
            string path)
        {
            return VariablePattern().Replace(value, match =>
            {
                var name = match.Groups["name"].Value;
                return ResolveVariable(name, variables, stack, path) ?? match.Value;
            });
        }

        private string? ResolveVariable(
            string name,
            IReadOnlyDictionary<string, string> variables,
            IReadOnlyList<string> stack,
            string path)
        {
            if (!variables.TryGetValue(name, out var value))
            {
                AddDiagnostic(
                    "semantic_workflow_variable_unresolved",
                    $"Workflow variable '{name}' is not defined.",
                    path);
                return null;
            }

            if (value is null)
            {
                AddDiagnostic(
                    "semantic_workflow_variable_invalid",
                    $"Workflow variable '{name}' cannot have a null value.",
                    path);
                return null;
            }

            if (stack.Contains(name, StringComparer.Ordinal))
            {
                AddDiagnostic(
                    "semantic_workflow_variable_cycle",
                    $"Workflow variable cycle detected: {string.Join(" -> ", stack.Append(name))}.",
                    path);
                return null;
            }

            return ResolveTextCore(value, variables, stack.Append(name).ToArray(), path);
        }

        private void ValidateFragmentGraph(SemanticWorkflowFragment fragment, IReadOnlyList<string> stack)
        {
            if (stack.Contains(fragment.Name, StringComparer.Ordinal))
            {
                AddDiagnostic(
                    "semantic_workflow_fragment_cycle",
                    $"Workflow fragment cycle detected: {string.Join(" -> ", stack.Append(fragment.Name))}.",
                    $"fragments.{fragment.Name}");
                return;
            }

            foreach (var step in EnumerateSteps(fragment.Steps))
            {
                if (step.Action != SemanticWorkflowActions.UseFragment || string.IsNullOrWhiteSpace(step.Fragment))
                {
                    continue;
                }

                if (!_fragments.TryGetValue(step.Fragment, out var referenced))
                {
                    AddDiagnostic(
                        "semantic_workflow_fragment_unresolved",
                        $"Workflow fragment '{step.Fragment}' is not declared.",
                        $"fragments.{fragment.Name}");
                    continue;
                }

                ValidateFragmentGraph(referenced, stack.Append(fragment.Name).ToArray());
            }
        }

        private void ValidateTemplateVariables(
            IReadOnlyList<SemanticWorkflowStep> steps,
            IReadOnlySet<string> allowedVariables,
            string path)
        {
            foreach (var step in EnumerateSteps(steps))
            {
                foreach (var value in EnumerateVariableStrings(step))
                {
                    foreach (Match match in VariablePattern().Matches(value))
                    {
                        var name = match.Groups["name"].Value;
                        if (!allowedVariables.Contains(name))
                        {
                            AddDiagnostic(
                                "semantic_workflow_variable_unresolved",
                                $"Workflow variable '{name}' is not defined for fragment template.",
                                path);
                        }
                    }
                }
            }
        }

        private static IEnumerable<SemanticWorkflowStep> EnumerateSteps(IReadOnlyList<SemanticWorkflowStep> steps)
        {
            foreach (var step in steps)
            {
                yield return step;
                foreach (var nested in EnumerateSteps(step.Then))
                {
                    yield return nested;
                }

                foreach (var nested in EnumerateSteps(step.Else))
                {
                    yield return nested;
                }

                foreach (var nested in EnumerateSteps(step.Steps))
                {
                    yield return nested;
                }
            }
        }

        private static IEnumerable<string> EnumerateVariableStrings(SemanticWorkflowStep step)
        {
            var values = new[]
            {
                step.Id, step.Text, step.Key, step.Modifiers, step.AssertProperty, step.Expected,
                step.ScreenshotPath, step.InputAction, step.IdempotencyKey, step.Direction,
                step.CustomActionName, step.TopLevelAlias, step.Fragment,
                step.Selector?.NodeId, step.Selector?.AutomationId, step.Selector?.Text,
                step.Selector?.Name, step.Selector?.NodeType, step.Selector?.Role,
                step.Selector?.BindingPath, step.Selector?.CommandName,
                step.WaitCondition?.Expected, step.WaitCondition?.PropertyName,
                step.WaitCondition?.BindingPath, step.WaitCondition?.Baseline,
                step.WaitCondition?.TopLevelId, step.WaitCondition?.TopLevelTitle,
                step.Mutation?.PropertyName, step.Mutation?.Value, step.Mutation?.ClassName,
                step.Mutation?.ResourceKey, step.Mutation?.MutationId
            };
            foreach (var value in values)
            {
                if (value is not null)
                {
                    yield return value;
                }
            }

            foreach (var value in step.CustomActionParameters.Values.Concat(step.Arguments.Values))
            {
                yield return value;
            }
        }

        public void AddDiagnostic(string code, string message, string path, int? limit = null)
        {
            var key = $"{code}\n{path}\n{message}";
            if (!_diagnosticKeys.Add(key) || Diagnostics.Count >= MaximumDiagnostics)
            {
                return;
            }

            var details = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["executionPath"] = path,
                ["nextAction"] = "Correct the workflow definition and run validateOnly again before execution."
            };
            if (limit.HasValue)
            {
                details["limit"] = limit.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
            }

            Diagnostics.Add(new ProtocolError(code, message, details));
        }
    }
}

internal sealed record CompiledSemanticWorkflow(
    SemanticWorkflowPlan Plan,
    IReadOnlyList<CompiledWorkflowNode> Roots);

internal sealed record CompiledWorkflowNode(
    SemanticWorkflowStep Step,
    string ExecutionPath,
    string? ParentStepId,
    string? Branch,
    string? SourceFragment,
    IReadOnlyList<CompiledWorkflowNode> Primary,
    IReadOnlyList<CompiledWorkflowNode> Alternate);
