using System.ComponentModel;
using System.Globalization;
using AvaScope.Core;
using AvaScope.Protocol;
using ModelContextProtocol.Server;

namespace AvaScope.Mcp;

[McpServerToolType]
public sealed class AvaScopeMcpTools
{
    [McpServerTool(
        Name = "health",
        Title = "Health",
        ReadOnly = true,
        Idempotent = true,
        Destructive = false,
        OpenWorld = false,
        UseStructuredContent = true)]
    [Description("Returns AvaScope server health and protocol version metadata.")]
    public static ToolResult<HealthResponse> Health()
    {
        return ToolResult<HealthResponse>.Ok(HealthResponse.Current());
    }

    [McpServerTool(
        Name = "capabilities",
        Title = "Capabilities",
        ReadOnly = true,
        Idempotent = true,
        Destructive = false,
        OpenWorld = false,
        UseStructuredContent = true)]
    [Description("Returns AvaScope protocol, CLI/MCP tool, runtime, preview, diagnostics, baseline, report, and artifact capability metadata.")]
    public static ToolResult<AvaScopeCapabilitiesResponse> Capabilities(string? requiredCapabilities = null)
    {
        return ToToolResult(new CapabilityCompatibilityChecker().CreateResponse(requiredCapabilities));
    }

    [McpServerTool(
        Name = "list_sessions",
        Title = "List sessions",
        ReadOnly = true,
        Idempotent = true,
        Destructive = false,
        OpenWorld = false,
        UseStructuredContent = true)]
    [Description("Lists active AvaScope inspection and preview sessions.")]
    public static ToolResult<ListSessionsResponse> ListSessions(SessionRegistry sessionRegistry)
    {
        ArgumentNullException.ThrowIfNull(sessionRegistry);

        var sessions = sessionRegistry.List()
            .Select(ToProtocolSummary)
            .ToArray();

        return ToolResult<ListSessionsResponse>.Ok(new ListSessionsResponse(sessions));
    }

    [McpServerTool(
        Name = "attach_to_app",
        Title = "Attach to app",
        ReadOnly = true,
        Idempotent = true,
        Destructive = false,
        OpenWorld = false,
        UseStructuredContent = true)]
    [Description("Attaches to one active local AvaScope bridge session by process id or session id.")]
    public static async Task<ToolResult<AttachToAppResponse>> AttachToApp(
        LocalBridgeClient bridgeClient,
        int? processId = null,
        string? processName = null,
        string? sessionId = null,
        string? manifestPath = null,
        string? manifestDirectory = null,
        bool latest = false,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(bridgeClient);

        if (!TryParseOptionalSessionId(sessionId, out var parsedSessionId, out var error))
        {
            return ToolResult<AttachToAppResponse>.Fail(error!);
        }

        var client = CreateBridgeClient(bridgeClient, manifestDirectory);
        if (latest)
        {
            return ToToolResult(await client.AttachLatestToAppAsync(
                processId,
                processName,
                cancellationToken));
        }

        return ToToolResult(await client.AttachToAppAsync(
            processId,
            parsedSessionId,
            processName,
            manifestPath,
            cancellationToken));
    }

    [McpServerTool(
        Name = "list_top_levels",
        Title = "List top levels",
        ReadOnly = true,
        Idempotent = true,
        Destructive = false,
        OpenWorld = false,
        UseStructuredContent = true)]
    [Description("Lists top-level windows/views for an attached local AvaScope bridge session.")]
    public static async Task<ToolResult<ListTopLevelsResponse>> ListTopLevels(
        LocalBridgeClient bridgeClient,
        string sessionId,
        string? manifestDirectory = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(bridgeClient);

        if (!TryParseRequiredSessionId(sessionId, out var parsedSessionId, out var error))
        {
            return ToolResult<ListTopLevelsResponse>.Fail(error!);
        }

        return ToToolResult(await CreateBridgeClient(bridgeClient, manifestDirectory).ListTopLevelsAsync(
            parsedSessionId!,
            cancellationToken));
    }

    [McpServerTool(
        Name = "screenshot",
        Title = "Screenshot",
        ReadOnly = true,
        Idempotent = false,
        Destructive = false,
        OpenWorld = false,
        UseStructuredContent = true)]
    [Description("Captures a screenshot from an attached local AvaScope bridge session to a local output file.")]
    public static async Task<ToolResult<ScreenshotResponse>> Screenshot(
        LocalBridgeClient bridgeClient,
        string sessionId,
        string topLevelId,
        string outputPath,
        string? manifestDirectory = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(bridgeClient);

        if (!TryParseRequiredSessionId(sessionId, out var parsedSessionId, out var error))
        {
            return ToolResult<ScreenshotResponse>.Fail(error!);
        }

        return ToToolResult(await CreateBridgeClient(bridgeClient, manifestDirectory).CaptureScreenshotAsync(
            parsedSessionId!,
            topLevelId,
            outputPath,
            cancellationToken));
    }

    [McpServerTool(
        Name = "assert_region",
        Title = "Assert region",
        ReadOnly = true,
        Idempotent = true,
        Destructive = false,
        OpenWorld = false,
        UseStructuredContent = true)]
    [Description("Checks a coordinate region in a local screenshot for non-empty, mostly blank, changed, or unchanged pixels.")]
    public static ToolResult<ScreenshotRegionAssertionResponse> AssertRegion(
        string imagePath,
        string assertion,
        int x,
        int y,
        int width,
        int height,
        string? baselinePath = null,
        string? cropPath = null,
        double tolerance = 0,
        long? minChangedPixels = null,
        double mostlyBlankMaxNonBlankPercent = 1)
    {
        try
        {
            var region = new ScreenshotRegion(x, y, width, height);
            return ToToolResult(new ScreenshotRegionAsserter().Assert(
                imagePath,
                region,
                assertion,
                baselinePath,
                cropPath,
                tolerance,
                minChangedPixels,
                mostlyBlankMaxNonBlankPercent));
        }
        catch (ArgumentOutOfRangeException exception)
        {
            return ToolResult<ScreenshotRegionAssertionResponse>.Fail(new ProtocolError(
                CoreErrorCodes.ImageRegionAssertionFailed,
                exception.Message));
        }
    }

    [McpServerTool(
        Name = "visual_tree",
        Title = "Visual tree",
        ReadOnly = true,
        Idempotent = true,
        Destructive = false,
        OpenWorld = false,
        UseStructuredContent = true)]
    [Description("Returns a bounded visual tree for an attached local AvaScope bridge session top-level.")]
    public static async Task<ToolResult<TreeResponse>> VisualTree(
        LocalBridgeClient bridgeClient,
        string sessionId,
        string topLevelId,
        int? maxDepth = null,
        string? manifestDirectory = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(bridgeClient);

        if (!TryParseRequiredSessionId(sessionId, out var parsedSessionId, out var error))
        {
            return ToolResult<TreeResponse>.Fail(error!);
        }

        return ToToolResult(await CreateBridgeClient(bridgeClient, manifestDirectory).VisualTreeAsync(
            parsedSessionId!,
            topLevelId,
            maxDepth,
            cancellationToken));
    }

    [McpServerTool(
        Name = "logical_tree",
        Title = "Logical tree",
        ReadOnly = true,
        Idempotent = true,
        Destructive = false,
        OpenWorld = false,
        UseStructuredContent = true)]
    [Description("Returns a bounded logical tree for an attached local AvaScope bridge session top-level.")]
    public static async Task<ToolResult<TreeResponse>> LogicalTree(
        LocalBridgeClient bridgeClient,
        string sessionId,
        string topLevelId,
        int? maxDepth = null,
        string? manifestDirectory = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(bridgeClient);

        if (!TryParseRequiredSessionId(sessionId, out var parsedSessionId, out var error))
        {
            return ToolResult<TreeResponse>.Fail(error!);
        }

        return ToToolResult(await CreateBridgeClient(bridgeClient, manifestDirectory).LogicalTreeAsync(
            parsedSessionId!,
            topLevelId,
            maxDepth,
            cancellationToken));
    }

    [McpServerTool(
        Name = "inspect_node",
        Title = "Inspect node",
        ReadOnly = true,
        Idempotent = true,
        Destructive = false,
        OpenWorld = false,
        UseStructuredContent = true)]
    [Description("Inspects one runtime node by stable visual or logical tree node id.")]
    public static async Task<ToolResult<InspectNodeResponse>> InspectNode(
        LocalBridgeClient bridgeClient,
        string sessionId,
        string topLevelId,
        string nodeId,
        string treeKind = TreeKinds.Visual,
        string? manifestDirectory = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(bridgeClient);

        if (!TryParseRequiredSessionId(sessionId, out var parsedSessionId, out var error))
        {
            return ToolResult<InspectNodeResponse>.Fail(error!);
        }

        return ToToolResult(await CreateBridgeClient(bridgeClient, manifestDirectory).InspectNodeAsync(
            parsedSessionId!,
            topLevelId,
            treeKind,
            nodeId,
            cancellationToken));
    }

    [McpServerTool(
        Name = "explain_layout",
        Title = "Explain layout",
        ReadOnly = true,
        Idempotent = true,
        Destructive = false,
        OpenWorld = false,
        UseStructuredContent = true)]
    [Description("Explains runtime layout state for one node, including DesiredSize, Bounds, clipping, Grid, ScrollViewer, and ancestor constraints where available.")]
    public static async Task<ToolResult<LayoutExplainResponse>> ExplainLayout(
        LocalBridgeClient bridgeClient,
        string sessionId,
        string topLevelId,
        string nodeId,
        string treeKind = TreeKinds.Visual,
        string? manifestDirectory = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(bridgeClient);

        if (!TryParseRequiredSessionId(sessionId, out var parsedSessionId, out var error))
        {
            return ToolResult<LayoutExplainResponse>.Fail(error!);
        }

        return ToToolResult(await CreateBridgeClient(bridgeClient, manifestDirectory).ExplainLayoutAsync(
            parsedSessionId!,
            topLevelId,
            treeKind,
            nodeId,
            cancellationToken));
    }

    [McpServerTool(
        Name = "find_nodes",
        Title = "Find nodes",
        ReadOnly = true,
        Idempotent = true,
        Destructive = false,
        OpenWorld = false,
        UseStructuredContent = true)]
    [Description("Finds nodes in a bounded visual or logical tree by type, name, automation id, or text.")]
    public static async Task<ToolResult<FindNodesResponse>> FindNodes(
        LocalBridgeClient bridgeClient,
        string sessionId,
        string topLevelId,
        string treeKind = TreeKinds.Visual,
        string? nodeType = null,
        string? name = null,
        string? automationId = null,
        string? text = null,
        int? maxDepth = null,
        int? maxResults = null,
        string? manifestDirectory = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(bridgeClient);

        if (!TryParseRequiredSessionId(sessionId, out var parsedSessionId, out var error))
        {
            return ToolResult<FindNodesResponse>.Fail(error!);
        }

        return ToToolResult(await CreateBridgeClient(bridgeClient, manifestDirectory).FindNodesAsync(
            parsedSessionId!,
            topLevelId,
            treeKind,
            nodeType,
            name,
            automationId,
            text,
            maxDepth,
            maxResults,
            cancellationToken));
    }

    [McpServerTool(
        Name = "audit_ui",
        Title = "Audit UI",
        ReadOnly = true,
        Idempotent = true,
        Destructive = false,
        OpenWorld = false,
        UseStructuredContent = true)]
    [Description("Returns a bounded accessibility, validation, and component inventory audit from a runtime visual or logical tree.")]
    public static async Task<ToolResult<UiAuditResponse>> AuditUi(
        LocalBridgeClient bridgeClient,
        string sessionId,
        string topLevelId,
        string treeKind = TreeKinds.Visual,
        int? maxDepth = null,
        int? maxIssues = null,
        int? maxInventoryItems = null,
        string? manifestDirectory = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(bridgeClient);

        if (!TryParseRequiredSessionId(sessionId, out var parsedSessionId, out var error))
        {
            return ToolResult<UiAuditResponse>.Fail(error!);
        }

        var client = CreateBridgeClient(bridgeClient, manifestDirectory);
        CoreResult<TreeResponse> tree = treeKind switch
        {
            TreeKinds.Visual => await client.VisualTreeAsync(parsedSessionId!, topLevelId, maxDepth, cancellationToken),
            TreeKinds.Logical => await client.LogicalTreeAsync(parsedSessionId!, topLevelId, maxDepth, cancellationToken),
            _ => CoreResult<TreeResponse>.Fail(new CoreError(
                CoreErrorCodes.InvalidBridgeRequest,
                $"Tree kind '{treeKind}' is not supported.",
                new Dictionary<string, string>
                {
                    ["supportedTreeKinds"] = $"{TreeKinds.Visual},{TreeKinds.Logical}"
                }))
        };

        if (!tree.Success)
        {
            return ToolResult<UiAuditResponse>.Fail(new ProtocolError(
                tree.Error!.Code,
                tree.Error.Message,
                tree.Error.Details));
        }

        return ToToolResult(new UiAuditBuilder().Create(tree.Value!, maxIssues, maxInventoryItems));
    }

    [McpServerTool(
        Name = "design_quality_audit",
        Title = "Design quality audit",
        ReadOnly = true,
        Idempotent = true,
        Destructive = false,
        OpenWorld = false,
        UseStructuredContent = true)]
    [Description("Runs a task-scoped design-quality audit over a runtime tree, reporting alignment, spacing, repeated height, contrast, seam, radius/layering, and wrapping findings with explicit exclusions and suppressions.")]
    public static async Task<ToolResult<DesignQualityAuditResponse>> DesignQualityAudit(
        LocalBridgeClient bridgeClient,
        DesignQualityAuditRequest request,
        string? manifestDirectory = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(bridgeClient);
        ArgumentNullException.ThrowIfNull(request);

        return ToToolResult(await new DesignQualityAuditRunner().RunAsync(
            CreateBridgeClient(bridgeClient, manifestDirectory),
            request,
            cancellationToken));
    }

    [McpServerTool(
        Name = "input",
        Title = "Input",
        ReadOnly = false,
        Idempotent = false,
        Destructive = false,
        OpenWorld = false,
        UseStructuredContent = true)]
    [Description("Sends a local-only input command to an attached AvaScope bridge session.")]
    public static async Task<ToolResult<InputResponse>> Input(
        LocalBridgeClient bridgeClient,
        string sessionId,
        string topLevelId,
        string action,
        double? x = null,
        double? y = null,
        string? inputText = null,
        string? targetNodeId = null,
        string? inputKey = null,
        string? keyModifiers = null,
        string? manifestDirectory = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(bridgeClient);

        if (!TryParseRequiredSessionId(sessionId, out var parsedSessionId, out var error))
        {
            return ToolResult<InputResponse>.Fail(error!);
        }

        return ToToolResult(await CreateBridgeClient(bridgeClient, manifestDirectory).InputAsync(
            parsedSessionId!,
            topLevelId,
            action,
            x,
            y,
            inputText,
            targetNodeId,
            inputKey,
            keyModifiers,
            cancellationToken));
    }

    [McpServerTool(
        Name = "run_workflow",
        Title = "Run workflow",
        ReadOnly = false,
        Idempotent = false,
        Destructive = false,
        OpenWorld = false,
        UseStructuredContent = true)]
    [Description("Runs a semantic local workflow against an attached AvaScope bridge session using node selectors such as AutomationId, text, role, binding path, command, or stable node id.")]
    public static async Task<ToolResult<SemanticWorkflowResponse>> RunWorkflow(
        LocalBridgeClient bridgeClient,
        SemanticWorkflowRequest request,
        string? manifestDirectory = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(bridgeClient);
        ArgumentNullException.ThrowIfNull(request);

        return ToToolResult(await new SemanticWorkflowRunner().RunAsync(
            CreateBridgeClient(bridgeClient, manifestDirectory),
            request,
            cancellationToken));
    }

    [McpServerTool(
        Name = "run_scenario",
        Title = "Run scenario",
        ReadOnly = false,
        Idempotent = false,
        Destructive = false,
        OpenWorld = false,
        UseStructuredContent = true)]
    [Description("Runs a safe local runtime scenario by launching or attaching to a bridge session, applying isolated app-state environment when launching, executing semantic workflow steps, and writing a human-readable timeline artifact.")]
    public static async Task<ToolResult<RuntimeScenarioResponse>> RunScenario(
        LocalBridgeClient bridgeClient,
        RuntimeScenarioRequest request,
        string? manifestDirectory = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(bridgeClient);
        ArgumentNullException.ThrowIfNull(request);

        return ToToolResult(await new RuntimeScenarioRunner().RunAsync(
            CreateBridgeClient(bridgeClient, manifestDirectory),
            request,
            cancellationToken));
    }

    [McpServerTool(
        Name = "pointer_diagnostics",
        Title = "Pointer diagnostics",
        ReadOnly = false,
        Idempotent = false,
        Destructive = false,
        OpenWorld = false,
        UseStructuredContent = true)]
    [Description("Runs a bounded pointer path against an attached bridge session and returns hit-path, popup-like layer, transition, screenshot, and pointer overlay diagnostics.")]
    public static async Task<ToolResult<RuntimePointerDiagnosticsResponse>> PointerDiagnostics(
        LocalBridgeClient bridgeClient,
        RuntimePointerDiagnosticsRequest request,
        string? manifestDirectory = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(bridgeClient);
        ArgumentNullException.ThrowIfNull(request);

        return ToToolResult(await new RuntimePointerDiagnosticsRunner().RunAsync(
            CreateBridgeClient(bridgeClient, manifestDirectory),
            request,
            cancellationToken));
    }

    [McpServerTool(
        Name = "pseudo_state_matrix",
        Title = "Pseudo-state matrix",
        ReadOnly = false,
        Idempotent = false,
        Destructive = false,
        OpenWorld = false,
        UseStructuredContent = true)]
    [Description("Captures a runtime control across common pseudo-states such as normal, pointerover, pressed, disabled, selected, and selected+pointerover, then writes a labeled contact sheet and structured diagnostics with reset results.")]
    public static async Task<ToolResult<RuntimePseudoStateMatrixResponse>> PseudoStateMatrix(
        LocalBridgeClient bridgeClient,
        RuntimePseudoStateMatrixRequest request,
        string? manifestDirectory = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(bridgeClient);
        ArgumentNullException.ThrowIfNull(request);

        return ToToolResult(await new RuntimePseudoStateMatrixRunner().RunAsync(
            CreateBridgeClient(bridgeClient, manifestDirectory),
            request,
            cancellationToken));
    }

    [McpServerTool(
        Name = "record_interaction_animation",
        Title = "Record interaction animation",
        ReadOnly = false,
        Idempotent = false,
        Destructive = false,
        OpenWorld = false,
        UseStructuredContent = true)]
    [Description("Runs scripted local runtime input, records frame sequences after selected steps, writes geometry overlays and a frame strip, and returns per-frame geometry assertion results.")]
    public static async Task<ToolResult<RuntimeInteractionAnimationResponse>> RecordInteractionAnimation(
        LocalBridgeClient bridgeClient,
        RuntimeInteractionAnimationRequest request,
        string? manifestDirectory = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(bridgeClient);
        ArgumentNullException.ThrowIfNull(request);

        return ToToolResult(await new RuntimeInteractionAnimationRunner().RunAsync(
            CreateBridgeClient(bridgeClient, manifestDirectory),
            request,
            cancellationToken));
    }

    [McpServerTool(
        Name = "mutate_node",
        Title = "Mutate node",
        ReadOnly = false,
        Idempotent = false,
        Destructive = false,
        OpenWorld = false,
        UseStructuredContent = true)]
    [Description("Evaluates a local-only runtime UI mutation request against a selected node and returns capability-aware diagnostics.")]
    public static async Task<ToolResult<RuntimeMutationResponse>> MutateNode(
        LocalBridgeClient bridgeClient,
        string sessionId,
        string topLevelId,
        string nodeId,
        string operation,
        string treeKind = TreeKinds.Visual,
        string? propertyName = null,
        string? value = null,
        string? valueType = null,
        string? className = null,
        string? resourceKey = null,
        string? mutationId = null,
        string? requestId = null,
        string? manifestDirectory = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(bridgeClient);

        if (!TryParseRequiredSessionId(sessionId, out var parsedSessionId, out var error))
        {
            return ToolResult<RuntimeMutationResponse>.Fail(error!);
        }

        RuntimeMutationRequest request;
        try
        {
            request = new RuntimeMutationRequest(
                string.IsNullOrWhiteSpace(requestId) ? Guid.NewGuid().ToString("n") : requestId,
                new RuntimeTargetContext(parsedSessionId!, topLevelId, treeKind, nodeId),
                new RuntimeMutationOperation(operation, propertyName, value, valueType, className, resourceKey, mutationId),
                [
                    RuntimeMutationCapabilityCatalog.RuntimeMutationContract,
                    RuntimeMutationCapabilityCatalog.StyleLayoutMutation
                ]);
        }
        catch (Exception exception) when (exception is ArgumentException or ArgumentOutOfRangeException)
        {
            return ToolResult<RuntimeMutationResponse>.Fail(new ProtocolError(
                CoreErrorCodes.InvalidBridgeRequest,
                exception.Message));
        }

        return ToToolResult(await CreateBridgeClient(bridgeClient, manifestDirectory).MutateNodeAsync(
            parsedSessionId!,
            request,
            cancellationToken));
    }

    [McpServerTool(
        Name = "mutate_node_evidence",
        Title = "Mutate node evidence",
        ReadOnly = false,
        Idempotent = false,
        Destructive = false,
        OpenWorld = false,
        UseStructuredContent = true)]
    [Description("Applies a local-only runtime UI mutation and captures before/after screenshots, visual tree snapshots, and optional diff artifacts.")]
    public static async Task<ToolResult<RuntimeMutationEvidenceResponse>> MutateNodeEvidence(
        LocalBridgeClient bridgeClient,
        string sessionId,
        string topLevelId,
        string nodeId,
        string operation,
        string artifactDirectory,
        string treeKind = TreeKinds.Visual,
        string? propertyName = null,
        string? value = null,
        string? valueType = null,
        string? className = null,
        string? resourceKey = null,
        string? mutationId = null,
        string? requestId = null,
        int maxDepth = 8,
        bool includeDiff = true,
        double tolerance = 0,
        string? manifestDirectory = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(bridgeClient);

        if (!TryParseRequiredSessionId(sessionId, out var parsedSessionId, out var error))
        {
            return ToolResult<RuntimeMutationEvidenceResponse>.Fail(error!);
        }

        RuntimeMutationRequest request;
        try
        {
            request = new RuntimeMutationRequest(
                string.IsNullOrWhiteSpace(requestId) ? Guid.NewGuid().ToString("n") : requestId,
                new RuntimeTargetContext(parsedSessionId!, topLevelId, treeKind, nodeId),
                new RuntimeMutationOperation(operation, propertyName, value, valueType, className, resourceKey, mutationId),
                [
                    RuntimeMutationCapabilityCatalog.RuntimeMutationContract,
                    RuntimeMutationCapabilityCatalog.StyleLayoutMutation
                ]);
        }
        catch (Exception exception) when (exception is ArgumentException or ArgumentOutOfRangeException)
        {
            return ToolResult<RuntimeMutationEvidenceResponse>.Fail(new ProtocolError(
                CoreErrorCodes.InvalidBridgeRequest,
                exception.Message));
        }

        return ToToolResult(await new RuntimeMutationEvidenceRunner().CaptureAsync(
            CreateBridgeClient(bridgeClient, manifestDirectory),
            parsedSessionId!,
            request,
            artifactDirectory,
            maxDepth,
            includeDiff,
            tolerance,
            cancellationToken));
    }

    [McpServerTool(
        Name = "mutation_review",
        Title = "Mutation review",
        ReadOnly = false,
        Idempotent = false,
        Destructive = false,
        OpenWorld = false,
        UseStructuredContent = true)]
    [Description("Returns a bounded local runtime mutation history, active override summary, and reset handoff for one bridge session.")]
    public static async Task<ToolResult<RuntimeMutationReviewResponse>> MutationReview(
        LocalBridgeClient bridgeClient,
        string sessionId,
        int? maxResults = null,
        string? artifactPath = null,
        string? sourceProject = null,
        string? sourceView = null,
        string? sourceApp = null,
        string? sourceProfile = null,
        string? manifestDirectory = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(bridgeClient);

        if (!TryParseRequiredSessionId(sessionId, out var parsedSessionId, out var error))
        {
            return ToolResult<RuntimeMutationReviewResponse>.Fail(error!);
        }

        if (maxResults is < 1 or > RuntimeMutationReviewResponse.MaximumEntries)
        {
            return ToolResult<RuntimeMutationReviewResponse>.Fail(new ProtocolError(
                CoreErrorCodes.InvalidBridgeRequest,
                $"maxResults must be between 1 and {RuntimeMutationReviewResponse.MaximumEntries.ToString(CultureInfo.InvariantCulture)}."));
        }

        var result = await CreateBridgeClient(bridgeClient, manifestDirectory).MutationReviewAsync(
            parsedSessionId!,
            maxResults,
            cancellationToken);
        if (!result.Success)
        {
            return ToToolResult(result);
        }

        var response = result.Value!;
        response = RuntimeSourceSuggestionBuilder.WithSourceContext(
            response,
            CreateSourceSuggestionContext(
                sourceProject,
                sourceView,
                sourceApp,
                sourceProfile,
                "mcp"));
        if (!string.IsNullOrWhiteSpace(artifactPath))
        {
            var artifact = new RuntimeMutationReviewExporter().ExportReview(response, artifactPath);
            if (!artifact.Success)
            {
                return ToolResult<RuntimeMutationReviewResponse>.Fail(new ProtocolError(
                    artifact.Error!.Code,
                    artifact.Error.Message,
                    artifact.Error.Details));
            }

            response = WithReviewArtifact(response, artifact.Value!);
        }

        return ToolResult<RuntimeMutationReviewResponse>.Ok(response);
    }

    [McpServerTool(
        Name = "close_session",
        Title = "Close session",
        ReadOnly = false,
        Idempotent = true,
        Destructive = false,
        OpenWorld = false,
        UseStructuredContent = true)]
    [Description("Closes a local AvaScope bridge session and removes its local manifest.")]
    public static async Task<ToolResult<CloseSessionResponse>> CloseSession(
        LocalBridgeClient bridgeClient,
        string sessionId,
        string? manifestDirectory = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(bridgeClient);

        if (!TryParseRequiredSessionId(sessionId, out var parsedSessionId, out var error))
        {
            return ToolResult<CloseSessionResponse>.Fail(error!);
        }

        return ToToolResult(await CreateBridgeClient(bridgeClient, manifestDirectory).CloseSessionAsync(
            parsedSessionId!,
            cancellationToken));
    }

    [McpServerTool(
        Name = "diagnostics",
        Title = "Diagnostics",
        ReadOnly = true,
        Idempotent = true,
        Destructive = false,
        OpenWorld = false,
        UseStructuredContent = true)]
    [Description("Returns bounded local AvaScope diagnostics for bridge sessions, manifests, transport, and protocol health.")]
    public static async Task<ToolResult<DiagnosticsResponse>> Diagnostics(
        LocalBridgeClient bridgeClient,
        PreviewHostClient previewHostClient,
        PreviewSessionStore? previewSessionStore = null,
        int? processId = null,
        string? processName = null,
        string? sessionId = null,
        string? manifestPath = null,
        string? manifestDirectory = null,
        int maxSessions = 50,
        string? mode = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(bridgeClient);
        ArgumentNullException.ThrowIfNull(previewHostClient);
        previewSessionStore ??= PreviewSessionStore.CreateDefault();

        if (!TryParseOptionalSessionId(sessionId, out var parsedSessionId, out var error))
        {
            return ToolResult<DiagnosticsResponse>.Fail(error!);
        }

        if (!DiagnosticsResponseModes.TryNormalize(mode, out var diagnosticsMode))
        {
            return ToolResult<DiagnosticsResponse>.Fail(new ProtocolError(
                CoreErrorCodes.InvalidBridgeRequest,
                "Diagnostics mode must be all, active-only, minimal, or json-minimal."));
        }

        var client = CreateBridgeClient(bridgeClient, manifestDirectory);
        var result = await client.DiagnosticsAsync(
            processId,
            parsedSessionId,
            maxSessions,
            previewHostClient.GetDiagnostics(),
            previewSessionStore.GetDiagnostics(),
            cancellationToken,
            processName,
            manifestPath);
        if (!result.Success)
        {
            return ToToolResult(result);
        }

        return ToolResult<DiagnosticsResponse>.Ok(ApplyDiagnosticsMode(result.Value!, diagnosticsMode));
    }

    [McpServerTool(
        Name = "launch_app",
        Title = "Launch app",
        ReadOnly = false,
        Idempotent = false,
        Destructive = false,
        OpenWorld = false,
        UseStructuredContent = true)]
    [Description("Launches a local bridge-enabled app, captures stdout/stderr, waits for an AvaScope bridge session, and returns follow-up identifiers.")]
    public static async Task<ToolResult<LaunchAppResponse>> LaunchApp(
        string command,
        string? arguments = null,
        string? workingDirectory = null,
        string? displayName = null,
        string? manifestDirectory = null,
        string? outputDirectory = null,
        string? environment = null,
        int timeoutMs = 15000,
        CancellationToken cancellationToken = default)
    {
        if (timeoutMs < 1)
        {
            return ToolResult<LaunchAppResponse>.Fail(new ProtocolError(
                CoreErrorCodes.InvalidBridgeRequest,
                "timeoutMs must be positive."));
        }

        if (!TryParseEnvironment(environment, out var environmentVariables, out var error))
        {
            return ToolResult<LaunchAppResponse>.Fail(error!);
        }

        return ToToolResult(await new BridgeAppLauncher().LaunchAsync(
            command,
            arguments,
            workingDirectory,
            displayName,
            manifestDirectory,
            outputDirectory,
            environmentVariables,
            TimeSpan.FromMilliseconds(timeoutMs),
            cancellationToken));
    }

    [McpServerTool(
        Name = "preview_axaml",
        Title = "Preview AXAML",
        ReadOnly = false,
        Idempotent = false,
        Destructive = false,
        OpenWorld = false,
        UseStructuredContent = true)]
    [Description("Renders an Avalonia .axaml preview through the isolated AvaScope preview host child process.")]
    public static async Task<ToolResult<PreviewResponse>> PreviewAxaml(
        PreviewHostClient previewHostClient,
        string outputPath,
        double? width = null,
        double? height = null,
        double dpi = 96,
        string? projectPath = null,
        string? viewPath = null,
        string? themeVariant = null,
        string? culture = null,
        string? designDataType = null,
        string? stateVariant = null,
        string? buildOutputRoot = null,
        string? assemblyPath = null,
        bool noBuild = false,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(previewHostClient);

        PreviewRequest request;
        try
        {
            request = new PreviewRequest(
                outputPath,
                width,
                height,
                dpi,
                projectPath,
                viewPath,
                themeVariant,
                culture,
                designDataType,
                stateVariant: stateVariant,
                buildOutputRoot: buildOutputRoot,
                assemblyPath: assemblyPath,
                noBuild: noBuild);
        }
        catch (Exception exception) when (exception is ArgumentException or ArgumentOutOfRangeException)
        {
            return ToolResult<PreviewResponse>.Fail(new ProtocolError(
                CoreErrorCodes.InvalidPreviewRequest,
                exception.Message));
        }

        return ToToolResult(await previewHostClient.RenderAsync(request, cancellationToken));
    }

    [McpServerTool(
        Name = "baseline_check",
        Title = "Baseline check",
        ReadOnly = false,
        Idempotent = false,
        Destructive = false,
        OpenWorld = false,
        UseStructuredContent = true)]
    [Description("Checks an AvaScope baseline manifest through isolated preview host renders and can write JSON and agent evidence report-pack artifacts.")]
    public static async Task<ToolResult<PreviewBaselineCheckResponse>> BaselineCheck(
        PreviewHostClient previewHostClient,
        string manifestPath,
        string? outputDirectory = null,
        string? diffDirectory = null,
        double tolerance = 0,
        string? reportPath = null,
        string? reportPackDirectory = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(previewHostClient);

        if (string.IsNullOrWhiteSpace(manifestPath))
        {
            return ToolResult<PreviewBaselineCheckResponse>.Fail(new ProtocolError(
                CoreErrorCodes.InvalidPreviewRequest,
                "Baseline manifest path is required."));
        }

        string fullManifestPath;
        string fullOutputDirectory;
        string fullDiffDirectory;
        try
        {
            fullManifestPath = Path.GetFullPath(manifestPath);
            var manifestDirectory = Path.GetDirectoryName(fullManifestPath) ?? Environment.CurrentDirectory;
            fullOutputDirectory = string.IsNullOrWhiteSpace(outputDirectory)
                ? Path.Combine(manifestDirectory, "current-images")
                : Path.GetFullPath(outputDirectory);
            fullDiffDirectory = string.IsNullOrWhiteSpace(diffDirectory)
                ? Path.Combine(manifestDirectory, "diff-images")
                : Path.GetFullPath(diffDirectory);
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return ToolResult<PreviewBaselineCheckResponse>.Fail(new ProtocolError(
                CoreErrorCodes.InvalidPreviewRequest,
                exception.Message));
        }

        return ToToolResult(await new PreviewBaselineManager(previewHostClient).CheckAsync(
            fullManifestPath,
            fullOutputDirectory,
            fullDiffDirectory,
            tolerance,
            reportPath,
            reportPackDirectory,
            cancellationToken));
    }

    [McpServerTool(
        Name = "semantic_diff",
        Title = "Semantic screenshot diff",
        ReadOnly = false,
        Idempotent = false,
        Destructive = false,
        OpenWorld = false,
        UseStructuredContent = true)]
    [Description("Compares a current screenshot against an arbitrary reference image and returns bounded raw changed regions plus heuristic semantic visual-delta findings with annotated crop artifacts.")]
    public static ToolResult<SemanticScreenshotComparisonResponse> SemanticDiff(
        string referencePath,
        string currentPath,
        string outputDirectory,
        string? diffPath = null,
        string? annotatedPath = null,
        double tolerance = 0,
        string? requestId = null,
        int maxFindings = 12,
        int maxRawRegions = 8,
        int minChangedPixels = 4)
    {
        SemanticScreenshotComparisonRequest request;
        try
        {
            request = new SemanticScreenshotComparisonRequest(
                referencePath,
                currentPath,
                requestId,
                outputDirectory,
                diffPath,
                annotatedPath,
                tolerance,
                maxFindings,
                maxRawRegions,
                minChangedPixels);
        }
        catch (Exception exception) when (exception is ArgumentException or ArgumentOutOfRangeException or NotSupportedException)
        {
            return ToolResult<SemanticScreenshotComparisonResponse>.Fail(new ProtocolError(
                CoreErrorCodes.ImageDiffFailed,
                exception.Message));
        }

        return ToToolResult(new SemanticScreenshotComparer().Compare(request));
    }

    [McpServerTool(
        Name = "preview_axaml_multi",
        Title = "Preview AXAML multiple sizes",
        ReadOnly = false,
        Idempotent = false,
        Destructive = false,
        OpenWorld = false,
        UseStructuredContent = true)]
    [Description("Renders an Avalonia .axaml preview at multiple viewport sizes through isolated preview host child processes.")]
    public static async Task<ToolResult<PreviewBatchResponse>> PreviewAxamlMulti(
        PreviewHostClient previewHostClient,
        string outputPath,
        string sizes,
        double dpi = 96,
        string? projectPath = null,
        string? viewPath = null,
        string? themeVariant = null,
        string? culture = null,
        string? designDataType = null,
        string? stateVariant = null,
        string? contactSheetPath = null,
        string? buildOutputRoot = null,
        string? assemblyPath = null,
        bool noBuild = false,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(previewHostClient);

        if (!TryParsePreviewViewports(sizes, out var viewports))
        {
            return ToolResult<PreviewBatchResponse>.Fail(new ProtocolError(
                CoreErrorCodes.InvalidPreviewRequest,
                "sizes must be a comma-separated list like 1440x900,1280x720."));
        }

        PreviewRequest request;
        try
        {
            request = new PreviewRequest(
                outputPath,
                dpi: dpi,
                projectPath: projectPath,
                viewPath: viewPath,
                themeVariant: themeVariant,
                culture: culture,
                designDataType: designDataType,
                stateVariant: stateVariant,
                buildOutputRoot: buildOutputRoot,
                assemblyPath: assemblyPath,
                noBuild: noBuild);
        }
        catch (Exception exception) when (exception is ArgumentException or ArgumentOutOfRangeException)
        {
            return ToolResult<PreviewBatchResponse>.Fail(new ProtocolError(
                CoreErrorCodes.InvalidPreviewRequest,
                exception.Message));
        }

        return ToToolResult(await previewHostClient.RenderBatchAsync(
            request,
            viewports!,
            contactSheetPath,
            cancellationToken));
    }

    [McpServerTool(
        Name = "preview_axaml_animation",
        Title = "Preview AXAML animation",
        ReadOnly = false,
        Idempotent = false,
        Destructive = false,
        OpenWorld = false,
        UseStructuredContent = true)]
    [Description("Renders deterministic time-offset animation samples for an Avalonia .axaml preview through isolated preview host child processes.")]
    public static async Task<ToolResult<PreviewAnimationResponse>> PreviewAxamlAnimation(
        PreviewHostClient previewHostClient,
        string outputPath,
        string timeOffsetsMs,
        double? width = null,
        double? height = null,
        double dpi = 96,
        string? projectPath = null,
        string? viewPath = null,
        string? themeVariant = null,
        string? culture = null,
        string? designDataType = null,
        string? stateVariant = null,
        string? frameStripPath = null,
        string? viewerPath = null,
        string? buildOutputRoot = null,
        string? assemblyPath = null,
        bool noBuild = false,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(previewHostClient);

        if (!TryParseAnimationTimeOffsets(timeOffsetsMs, out var offsets))
        {
            return ToolResult<PreviewAnimationResponse>.Fail(new ProtocolError(
                CoreErrorCodes.InvalidPreviewRequest,
                $"timeOffsetsMs must be a comma-separated list of 0..{PreviewAnimationRequest.MaximumTimeOffsetMs} millisecond offsets."));
        }

        PreviewAnimationRequest request;
        try
        {
            request = new PreviewAnimationRequest(
                outputPath,
                offsets!,
                width,
                height,
                dpi,
                projectPath,
                viewPath,
                themeVariant,
                culture,
                designDataType,
                frameStripPath: frameStripPath,
                viewerPath: viewerPath,
                stateVariant: stateVariant,
                buildOutputRoot: buildOutputRoot,
                assemblyPath: assemblyPath,
                noBuild: noBuild);
        }
        catch (Exception exception) when (exception is ArgumentException or ArgumentOutOfRangeException)
        {
            return ToolResult<PreviewAnimationResponse>.Fail(new ProtocolError(
                CoreErrorCodes.InvalidPreviewRequest,
                exception.Message));
        }

        return ToToolResult(await previewHostClient.RenderAnimationAsync(
            request,
            cancellationToken));
    }

    [McpServerTool(
        Name = "cleanup",
        Title = "Cleanup",
        ReadOnly = false,
        Idempotent = true,
        Destructive = false,
        OpenWorld = false,
        UseStructuredContent = true)]
    [Description("Deletes stale AvaScope-owned preview session records from the local preview-session store.")]
    public static ToolResult<PreviewCleanupResponse> Cleanup(PreviewSessionStore previewSessionStore)
    {
        ArgumentNullException.ThrowIfNull(previewSessionStore);

        return ToToolResult(previewSessionStore.CleanupStale());
    }

    [McpServerTool(
        Name = "cleanup_bridge_sessions",
        Title = "Cleanup bridge sessions",
        ReadOnly = false,
        Idempotent = true,
        Destructive = false,
        OpenWorld = false,
        UseStructuredContent = true)]
    [Description("Deletes stale or invalid AvaScope-owned local bridge manifests without killing application processes.")]
    public static async Task<ToolResult<BridgeCleanupResponse>> CleanupBridgeSessions(
        LocalBridgeClient bridgeClient,
        string? manifestDirectory = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(bridgeClient);

        var client = CreateBridgeClient(bridgeClient, manifestDirectory);
        return ToToolResult(await client.CleanupBridgeManifestsAsync(cancellationToken));
    }

    [McpServerTool(
        Name = "create_preview_session",
        Title = "Create preview session",
        ReadOnly = false,
        Idempotent = false,
        Destructive = false,
        OpenWorld = false,
        UseStructuredContent = true)]
    [Description("Creates a persistent AvaScope preview session record and stores the initial isolated preview render result.")]
    public static async Task<ToolResult<PreviewSessionSummary>> CreatePreviewSession(
        PreviewSessionRegistry previewSessions,
        string outputPath,
        double? width = null,
        double? height = null,
        double dpi = 96,
        string? projectPath = null,
        string? viewPath = null,
        string? themeVariant = null,
        string? culture = null,
        string? designDataType = null,
        string? stateVariant = null,
        string? displayName = null,
        string? buildOutputRoot = null,
        string? assemblyPath = null,
        bool noBuild = false,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(previewSessions);

        PreviewRequest request;
        try
        {
            request = new PreviewRequest(
                outputPath,
                width,
                height,
                dpi,
                projectPath,
                viewPath,
                themeVariant,
                culture,
                designDataType,
                stateVariant: stateVariant,
                buildOutputRoot: buildOutputRoot,
                assemblyPath: assemblyPath,
                noBuild: noBuild);
        }
        catch (Exception exception) when (exception is ArgumentException or ArgumentOutOfRangeException)
        {
            return ToolResult<PreviewSessionSummary>.Fail(new ProtocolError(
                CoreErrorCodes.InvalidPreviewRequest,
                exception.Message));
        }

        return ToToolResult(await previewSessions.CreateAsync(
            request,
            displayName,
            cancellationToken));
    }

    [McpServerTool(
        Name = "list_preview_sessions",
        Title = "List preview sessions",
        ReadOnly = true,
        Idempotent = true,
        Destructive = false,
        OpenWorld = false,
        UseStructuredContent = true)]
    [Description("Lists AvaScope preview session records with their original request and latest render result.")]
    public static ToolResult<ListPreviewSessionsResponse> ListPreviewSessions(
        PreviewSessionRegistry previewSessions)
    {
        ArgumentNullException.ThrowIfNull(previewSessions);

        return ToolResult<ListPreviewSessionsResponse>.Ok(new ListPreviewSessionsResponse(previewSessions.List()));
    }

    [McpServerTool(
        Name = "preview_viewer",
        Title = "Preview viewer",
        ReadOnly = false,
        Idempotent = false,
        Destructive = false,
        OpenWorld = false,
        UseStructuredContent = true)]
    [Description("Exports a local file-backed AvaScope preview viewer for a preview session and returns a previewUrl suitable for the Codex in-app browser.")]
    public static ToolResult<PreviewViewerResponse> PreviewViewer(
        PreviewSessionRegistry previewSessions,
        string sessionId,
        string? outputPath = null)
    {
        ArgumentNullException.ThrowIfNull(previewSessions);

        if (!TryParseRequiredSessionId(sessionId, out var parsedSessionId, out var error))
        {
            return ToolResult<PreviewViewerResponse>.Fail(error!);
        }

        var session = previewSessions.Get(parsedSessionId!);
        if (!session.Success)
        {
            return ToolResult<PreviewViewerResponse>.Fail(new ProtocolError(
                session.Error!.Code,
                session.Error.Message,
                session.Error.Details));
        }

        return ToToolResult(new PreviewViewerExporter().Export(session.Value!, outputPath));
    }

    [McpServerTool(
        Name = "close_preview_session",
        Title = "Close preview session",
        ReadOnly = false,
        Idempotent = true,
        Destructive = false,
        OpenWorld = false,
        UseStructuredContent = true)]
    [Description("Closes a persistent AvaScope preview session record without affecting runtime bridge sessions.")]
    public static ToolResult<PreviewSessionSummary> ClosePreviewSession(
        PreviewSessionRegistry previewSessions,
        string sessionId)
    {
        ArgumentNullException.ThrowIfNull(previewSessions);

        if (!TryParseRequiredSessionId(sessionId, out var parsedSessionId, out var error))
        {
            return ToolResult<PreviewSessionSummary>.Fail(error!);
        }

        return ToToolResult(previewSessions.Close(parsedSessionId!));
    }

    [McpServerTool(
        Name = "reload",
        Title = "Reload",
        ReadOnly = false,
        Idempotent = false,
        Destructive = false,
        OpenWorld = false,
        UseStructuredContent = true)]
    [Description("Reloads a preview session, or checks a runtime bridge session and returns explicit unsupported diagnostics.")]
    public static async Task<ToolResult<PreviewSessionSummary>> Reload(
        PreviewSessionRegistry previewSessions,
        LocalBridgeClient bridgeClient,
        string sessionId,
        string? manifestDirectory = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(previewSessions);
        ArgumentNullException.ThrowIfNull(bridgeClient);

        if (!TryParseRequiredSessionId(sessionId, out var parsedSessionId, out var error))
        {
            return ToolResult<PreviewSessionSummary>.Fail(error!);
        }

        var previewReload = await previewSessions.ReloadAsync(parsedSessionId!, cancellationToken);
        if (previewReload.Success || previewReload.Error!.Code != CoreErrorCodes.SessionNotFound)
        {
            return ToToolResult(previewReload);
        }

        var runtimeReload = await CreateBridgeClient(bridgeClient, manifestDirectory).ReloadRuntimeAsync(
            parsedSessionId!,
            cancellationToken);
        return runtimeReload.Error!.Code == CoreErrorCodes.BridgeSessionNotFound
            ? ToToolResult(previewReload)
            : ToolResult<PreviewSessionSummary>.Fail(new ProtocolError(
                runtimeReload.Error.Code,
                runtimeReload.Error.Message,
                runtimeReload.Error.Details));
    }

    private static SessionSummary ToProtocolSummary(SessionSnapshot session)
    {
        return new SessionSummary(
            session.Id,
            session.Kind,
            ToProtocolState(session.State),
            session.CreatedAt,
            session.DisplayName);
    }

    private static string ToProtocolState(SessionLifecycleState state)
    {
        return state switch
        {
            SessionLifecycleState.Active => SessionStates.Active,
            SessionLifecycleState.Closing => SessionStates.Closing,
            SessionLifecycleState.Closed => SessionStates.Closed,
            SessionLifecycleState.Failed => SessionStates.Failed,
            _ => throw new ArgumentOutOfRangeException(nameof(state), state, "Unknown session state.")
        };
    }

    private static ToolResult<T> ToToolResult<T>(CoreResult<T> result)
    {
        return result.Success
            ? ToolResult<T>.Ok(result.Value!)
            : ToolResult<T>.Fail(new ProtocolError(
                result.Error!.Code,
                result.Error.Message,
                result.Error.Details));
    }

    private static RuntimeMutationReviewResponse WithReviewArtifact(
        RuntimeMutationReviewResponse response,
        RuntimeMutationReviewArtifact artifact)
    {
        return new RuntimeMutationReviewResponse(
            response.SessionId,
            response.ReviewedAt,
            response.HistoryCount,
            response.ActiveMutationCount,
            response.History,
            response.ActiveMutations,
            response.ResetHandoff,
            response.Metadata,
            artifact,
            response.SourceContext,
            response.SourceSuggestions);
    }

    private static RuntimeSourceSuggestionContext? CreateSourceSuggestionContext(
        string? sourceProject,
        string? sourceView,
        string? sourceApp,
        string? sourceProfile,
        string source)
    {
        var context = new RuntimeSourceSuggestionContext(
            sourceProject,
            sourceView,
            sourceApp,
            sourceProfile,
            source);
        return context.HasAnyPath ? context : null;
    }

    private static LocalBridgeClient CreateBridgeClient(LocalBridgeClient bridgeClient, string? manifestDirectory)
    {
        return string.IsNullOrWhiteSpace(manifestDirectory)
            ? bridgeClient
            : new LocalBridgeClient(manifestDirectory, bridgeClient.OperationTimeout);
    }

    private static DiagnosticsResponse ApplyDiagnosticsMode(
        DiagnosticsResponse response,
        string mode)
    {
        if (mode == DiagnosticsResponseModes.All)
        {
            return response;
        }

        var activeOnly = mode == DiagnosticsResponseModes.ActiveOnly;
        var bridgeSessions = activeOnly
            ? response.BridgeSessions
                .Where(static session => session.Status == DiagnosticStatuses.Available)
                .ToArray()
            : Array.Empty<BridgeSessionDiagnostic>();
        var previewSessions = activeOnly
            ? response.PreviewSessions
                .Where(static session => session.Status == DiagnosticStatuses.Available)
                .ToArray()
            : Array.Empty<PreviewSessionDiagnostic>();
        var issues = activeOnly ? response.Issues : Array.Empty<ProtocolError>();
        var diagnosticIssues = activeOnly
            ? response.DiagnosticIssues
                .Where(static issue => issue.Source is DiagnosticIssueSources.Diagnostics or DiagnosticIssueSources.PreviewHost)
                .ToArray()
            : Array.Empty<DiagnosticIssue>();

        return new DiagnosticsResponse(
            response.Service,
            response.GeneratedAt,
            response.ManifestDirectory,
            bridgeSessions,
            issues,
            response.PreviewHost,
            previewSessions,
            diagnosticIssues,
            response.Summary);
    }

    private static bool TryParseOptionalSessionId(
        string? sessionId,
        out SessionId? parsedSessionId,
        out ProtocolError? error)
    {
        parsedSessionId = null;
        error = null;

        if (string.IsNullOrWhiteSpace(sessionId))
        {
            return true;
        }

        return TryCreateSessionId(sessionId, out parsedSessionId, out error);
    }

    private static bool TryParseRequiredSessionId(
        string sessionId,
        out SessionId? parsedSessionId,
        out ProtocolError? error)
    {
        parsedSessionId = null;
        error = null;

        return string.IsNullOrWhiteSpace(sessionId)
            ? FailSessionId("Session id is required.", out error)
            : TryCreateSessionId(sessionId, out parsedSessionId, out error);
    }

    private static bool TryCreateSessionId(
        string sessionId,
        out SessionId? parsedSessionId,
        out ProtocolError? error)
    {
        try
        {
            parsedSessionId = new SessionId(sessionId);
            error = null;
            return true;
        }
        catch (ArgumentException exception)
        {
            parsedSessionId = null;
            error = new ProtocolError(CoreErrorCodes.InvalidBridgeRequest, exception.Message);
            return false;
        }
    }

    private static bool FailSessionId(string message, out ProtocolError error)
    {
        error = new ProtocolError(CoreErrorCodes.InvalidBridgeRequest, message);
        return false;
    }

    private static bool TryParsePreviewViewports(string text, out IReadOnlyList<PreviewViewport>? viewports)
    {
        viewports = null;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var parsed = new List<PreviewViewport>();
        foreach (var token in text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var parts = token.Split(['x', 'X'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (parts.Length != 2
                || !double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var width)
                || !double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var height)
                || width < 1
                || height < 1)
            {
                return false;
            }

            parsed.Add(new PreviewViewport(width, height));
        }

        if (parsed.Count == 0)
        {
            return false;
        }

        viewports = parsed;
        return true;
    }

    private static bool TryParseEnvironment(
        string? text,
        out IReadOnlyDictionary<string, string> environment,
        out ProtocolError? error)
    {
        environment = new Dictionary<string, string>(StringComparer.Ordinal);
        error = null;
        if (string.IsNullOrWhiteSpace(text))
        {
            return true;
        }

        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var token in text.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var separator = token.IndexOf('=');
            if (separator <= 0)
            {
                error = new ProtocolError(
                    CoreErrorCodes.InvalidBridgeRequest,
                    "environment must be a semicolon-separated list of KEY=VALUE entries.");
                return false;
            }

            values[token[..separator]] = token[(separator + 1)..];
        }

        environment = values;
        return true;
    }

    private static bool TryParseAnimationTimeOffsets(string text, out IReadOnlyList<int>? timeOffsetsMs)
    {
        timeOffsetsMs = null;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var parsed = new List<int>();
        foreach (var token in text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (!int.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out var offset)
                || offset < 0
                || offset > PreviewAnimationRequest.MaximumTimeOffsetMs)
            {
                return false;
            }

            parsed.Add(offset);
        }

        if (parsed.Count == 0 || parsed.Count > PreviewAnimationRequest.MaximumFrameCount)
        {
            return false;
        }

        timeOffsetsMs = parsed;
        return true;
    }
}
