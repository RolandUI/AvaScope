namespace AvaScope.Protocol;

public static class AvaScopeCapabilityIds
{
    public const string ProtocolToolResultV1 = "protocol.tool_result_v1";
    public const string ProtocolAdditiveJsonFields = "protocol.additive_json_fields";
    public const string ProtocolCapabilityDiscovery = "protocol.capability_discovery";
    public const string ProtocolMcpStdioServer = "protocol.mcp_stdio_server";

    public const string SafetyLocalOnly = "safety.local_only";

    public const string RuntimeAttach = "runtime.attach";
    public const string RuntimeSessionLifecycle = "runtime.session_lifecycle";
    public const string RuntimeTrees = "runtime.trees";
    public const string RuntimeInspect = "runtime.inspect";
    public const string RuntimeFind = "runtime.find";
    public const string RuntimeInput = "runtime.input";
    public const string RuntimeMutationContract = "runtime.mutation_contract";
    public const string RuntimeStyleLayoutMutation = "runtime.style_layout_mutation";
    public const string RuntimeMutationEvidence = "runtime.mutation_evidence";
    public const string RuntimeMutationReview = "runtime.mutation_review";
    public const string RuntimeSourceSuggestions = "runtime.source_suggestions";
    public const string RuntimeUiAudit = "runtime.ui_audit";

    public const string PreviewAxaml = "preview.axaml";
    public const string PreviewSessions = "preview.sessions";
    public const string PreviewReload = "preview.reload";
    public const string PreviewViewer = "preview.viewer";
    public const string PreviewMultiSize = "preview.multi_size";
    public const string PreviewAnimation = "preview.animation";

    public const string DiagnosticsSummary = "diagnostics.summary";

    public const string BaselineSingle = "baseline.single";
    public const string BaselineSuite = "baseline.suite";
    public const string BaselineComparisonRules = "baseline.comparison_rules";

    public const string ReportsJson = "reports.json";
    public const string ReportsAgentReview = "reports.agent_review";
    public const string ReportsEvidencePack = "reports.evidence_pack";

    public const string ArtifactsScreenshot = "artifacts.screenshot";
    public const string ArtifactsDiffImage = "artifacts.diff_image";
    public const string ArtifactsHtmlViewer = "artifacts.html_viewer";
    public const string ArtifactsJunitSarif = "artifacts.junit_sarif";
}
