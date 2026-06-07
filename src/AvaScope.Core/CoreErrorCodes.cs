namespace AvaScope.Core;

public static class CoreErrorCodes
{
    public const string SessionNotFound = "session_not_found";
    public const string BridgeSessionNotFound = "bridge_session_not_found";
    public const string MultipleBridgeSessions = "multiple_bridge_sessions";
    public const string BridgeIpcFailed = "bridge_ipc_failed";
    public const string BridgeIpcUnavailable = "bridge_ipc_unavailable";
    public const string BridgeManifestInvalid = "bridge_manifest_invalid";
    public const string DiagnosticsTruncated = "diagnostics_truncated";
    public const string InvalidBridgeRequest = "invalid_bridge_request";
    public const string InvalidPreviewRequest = "invalid_preview_request";
    public const string PreviewHostFailed = "preview_host_failed";
    public const string PreviewHostUnavailable = "preview_host_unavailable";
}
