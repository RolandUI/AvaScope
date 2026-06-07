using System.ComponentModel;
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
        string? sessionId = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(bridgeClient);

        if (!TryParseOptionalSessionId(sessionId, out var parsedSessionId, out var error))
        {
            return ToolResult<AttachToAppResponse>.Fail(error!);
        }

        return ToToolResult(await bridgeClient.AttachToAppAsync(processId, parsedSessionId, cancellationToken));
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
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(bridgeClient);

        if (!TryParseRequiredSessionId(sessionId, out var parsedSessionId, out var error))
        {
            return ToolResult<ListTopLevelsResponse>.Fail(error!);
        }

        return ToToolResult(await bridgeClient.ListTopLevelsAsync(parsedSessionId!, cancellationToken));
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
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(bridgeClient);

        if (!TryParseRequiredSessionId(sessionId, out var parsedSessionId, out var error))
        {
            return ToolResult<ScreenshotResponse>.Fail(error!);
        }

        return ToToolResult(await bridgeClient.CaptureScreenshotAsync(
            parsedSessionId!,
            topLevelId,
            outputPath,
            cancellationToken));
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
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(bridgeClient);

        if (!TryParseRequiredSessionId(sessionId, out var parsedSessionId, out var error))
        {
            return ToolResult<TreeResponse>.Fail(error!);
        }

        return ToToolResult(await bridgeClient.VisualTreeAsync(
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
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(bridgeClient);

        if (!TryParseRequiredSessionId(sessionId, out var parsedSessionId, out var error))
        {
            return ToolResult<TreeResponse>.Fail(error!);
        }

        return ToToolResult(await bridgeClient.LogicalTreeAsync(
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
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(bridgeClient);

        if (!TryParseRequiredSessionId(sessionId, out var parsedSessionId, out var error))
        {
            return ToolResult<InspectNodeResponse>.Fail(error!);
        }

        return ToToolResult(await bridgeClient.InspectNodeAsync(
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
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(bridgeClient);

        if (!TryParseRequiredSessionId(sessionId, out var parsedSessionId, out var error))
        {
            return ToolResult<FindNodesResponse>.Fail(error!);
        }

        return ToToolResult(await bridgeClient.FindNodesAsync(
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
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(bridgeClient);

        if (!TryParseRequiredSessionId(sessionId, out var parsedSessionId, out var error))
        {
            return ToolResult<InputResponse>.Fail(error!);
        }

        return ToToolResult(await bridgeClient.InputAsync(
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
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(bridgeClient);

        if (!TryParseRequiredSessionId(sessionId, out var parsedSessionId, out var error))
        {
            return ToolResult<CloseSessionResponse>.Fail(error!);
        }

        return ToToolResult(await bridgeClient.CloseSessionAsync(parsedSessionId!, cancellationToken));
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
        int? processId = null,
        string? sessionId = null,
        int maxSessions = 50,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(bridgeClient);
        ArgumentNullException.ThrowIfNull(previewHostClient);

        if (!TryParseOptionalSessionId(sessionId, out var parsedSessionId, out var error))
        {
            return ToolResult<DiagnosticsResponse>.Fail(error!);
        }

        return ToToolResult(await bridgeClient.DiagnosticsAsync(
            processId,
            parsedSessionId,
            maxSessions,
            previewHostClient.GetDiagnostics(),
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
        double width,
        double height,
        double dpi = 96,
        string? projectPath = null,
        string? viewPath = null,
        string? themeVariant = null,
        string? culture = null,
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
                culture);
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
        double width,
        double height,
        double dpi = 96,
        string? projectPath = null,
        string? viewPath = null,
        string? themeVariant = null,
        string? culture = null,
        string? displayName = null,
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
                culture);
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

        var runtimeReload = await bridgeClient.ReloadRuntimeAsync(parsedSessionId!, cancellationToken);
        return runtimeReload.Error!.Code == CoreErrorCodes.BridgeSessionNotFound
            ? ToToolResult(previewReload)
            : ToolResult<PreviewSessionSummary>.Fail(new ProtocolError(runtimeReload.Error.Code, runtimeReload.Error.Message));
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
            : ToolResult<T>.Fail(new ProtocolError(result.Error!.Code, result.Error.Message));
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
}
