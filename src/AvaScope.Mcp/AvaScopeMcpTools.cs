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
}
