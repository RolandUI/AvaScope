namespace AvaScope.Core;

public static class CoreErrorCodes
{
    public const string SessionNotFound = "session_not_found";
    public const string SessionClosed = "session_closed";
    public const string BridgeSessionNotFound = "bridge_session_not_found";
    public const string MultipleBridgeSessions = "multiple_bridge_sessions";
    public const string BridgeIpcFailed = "bridge_ipc_failed";
    public const string BridgeIpcUnavailable = "bridge_ipc_unavailable";
    public const string BridgeManifestInvalid = "bridge_manifest_invalid";
    public const string BridgeManifestUnauthorized = "bridge_manifest_unauthorized";
    public const string BridgeManifestDuplicate = "bridge_manifest_duplicate";
    public const string BridgeProtocolIncompatible = "bridge_protocol_incompatible";
    public const string BridgeManifestCleanupFailed = "bridge_manifest_cleanup_failed";
    public const string DiagnosticsTruncated = "diagnostics_truncated";
    public const string DiagnosticsMixedInstallRoots = "diagnostics_mixed_install_roots";
    public const string InvalidBridgeRequest = "invalid_bridge_request";
    public const string InvalidPreviewRequest = "invalid_preview_request";
    public const string PreviewBaselineFailed = "preview_baseline_failed";
    public const string PreviewBaselineManifestInvalid = "preview_baseline_manifest_invalid";
    public const string PreviewDiagnosticsBaselineInvalid = "preview_diagnostics_baseline_invalid";
    public const string ImageDiffDimensionMismatch = "image_diff_dimension_mismatch";
    public const string ImageDiffFailed = "image_diff_failed";
    public const string ImageRegionAssertionFailed = "image_region_assertion_failed";
    public const string PreviewHostFailed = "preview_host_failed";
    public const string PreviewHostUnavailable = "preview_host_unavailable";
    public const string PreviewSessionStoreFailed = "preview_session_store_failed";
    public const string PreviewViewerUnavailable = "preview_viewer_unavailable";
    public const string AgentEvidenceReportPackUnavailable = "agent_evidence_report_pack_unavailable";
    public const string ArtifactRunIndexUnavailable = "artifact_run_index_unavailable";
    public const string RuntimeMutationReviewUnavailable = "runtime_mutation_review_unavailable";
    public const string RuntimeReloadNotSupported = "runtime_reload_not_supported";
    public const string RuntimeEvidencePolicyInvalid = "runtime_evidence_policy_invalid";
    public const string RuntimeEvidenceUnauthorized = "runtime_evidence_unauthorized";
    public const string RuntimeEvidenceActionDisallowed = "runtime_evidence_action_disallowed";
    public const string RuntimeEvidenceRedactionFailed = "runtime_evidence_redaction_failed";
    public const string RuntimeEvidenceMaskFailed = "runtime_evidence_mask_failed";
    public const string RuntimeEvidenceAuditFailed = "runtime_evidence_audit_failed";
}
