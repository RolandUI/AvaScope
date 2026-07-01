namespace AvaScope.Protocol;

public static class AvaScopeCapabilityCatalog
{
    public static AvaScopeCapabilitiesResponse Current(DateTimeOffset? generatedAt = null)
    {
        var capabilities = CreateCapabilities();
        var tools = CreateTools();

        return new AvaScopeCapabilitiesResponse(
            AvaScopeProtocol.ServiceName,
            AvaScopeProtocol.CurrentVersion,
            generatedAt ?? DateTimeOffset.UtcNow,
            CreateCompatibilityPolicy(),
            capabilities,
            tools,
            RuntimeMutationCapabilityCatalog.CurrentBridgeCapabilities());
    }

    private static IReadOnlyDictionary<string, string> CreateCompatibilityPolicy()
    {
        return new Dictionary<string, string>
        {
            ["breakingContract"] = "protocolVersion.major",
            ["featureDiscovery"] = "capabilities[].id",
            ["additiveFields"] = "Clients must ignore unknown JSON properties and use capability ids for feature support.",
            ["unsupportedCapabilities"] = "Request required capabilities before use; unsupported requirements fail with capability_not_supported.",
            ["toolResultShape"] = "ToolResult preserves success, value, and error fields for old and new clients."
        };
    }

    private static IReadOnlyList<AvaScopeCapability> CreateCapabilities()
    {
        return
        [
            Capability(
                AvaScopeCapabilityIds.ProtocolToolResultV1,
                "protocol",
                "Stable ToolResult<T> JSON shape with success, value, and error fields.",
                ["capabilities", "health"],
                metadata: new Dictionary<string, string> { ["toolResult"] = "success,value,error" }),
            Capability(
                AvaScopeCapabilityIds.ProtocolAdditiveJsonFields,
                "protocol",
                "Additive response fields are compatible when clients ignore unknown JSON properties.",
                ["capabilities"],
                metadata: new Dictionary<string, string> { ["clientRule"] = "ignore_unknown_json_properties" }),
            Capability(
                AvaScopeCapabilityIds.ProtocolCapabilityDiscovery,
                "protocol",
                "Clients can query this manifest and require named feature ids before using newer surfaces.",
                ["capabilities"]),
            Capability(
                AvaScopeCapabilityIds.ProtocolMcpStdioServer,
                "protocol",
                "The avascope CLI can start the stdio MCP server adapter for local agent clients.",
                ["mcp"],
                requires: [AvaScopeCapabilityIds.ProtocolCapabilityDiscovery]),
            Capability(
                AvaScopeCapabilityIds.SafetyLocalOnly,
                "safety",
                "Runtime bridge and generated artifacts are local-only by default.",
                ["doctor", "diagnostics", "attach_to_app", "launch_app", "cleanup_bridge_sessions"],
                metadata: new Dictionary<string, string> { ["remoteInspection"] = "unsupported_by_default" }),
            Capability(
                AvaScopeCapabilityIds.RuntimeAttach,
                "runtime",
                "Attach to opt-in local bridge sessions by process, process name, session id, manifest, or latest local session.",
                ["attach", "attach_to_app"],
                requires: [AvaScopeCapabilityIds.SafetyLocalOnly]),
            Capability(
                AvaScopeCapabilityIds.RuntimeSessionLifecycle,
                "runtime",
                "Launch local bridge-enabled apps and close active local bridge sessions through explicit local lifecycle commands.",
                ["launch-app", "launch_app", "close-session", "close_session"],
                requires: [AvaScopeCapabilityIds.SafetyLocalOnly]),
            Capability(
                AvaScopeCapabilityIds.RuntimeTrees,
                "runtime",
                "Read bounded visual and logical tree snapshots from an attached local bridge session.",
                ["visual-tree", "logical-tree", "visual_tree", "logical_tree"],
                requires: [AvaScopeCapabilityIds.RuntimeAttach]),
            Capability(
                AvaScopeCapabilityIds.RuntimeInspect,
                "runtime",
                "Inspect a single runtime node with bounds, classes, resources, binding, accessibility, validation, and target context where available.",
                ["inspect-node", "inspect_node"],
                requires: [AvaScopeCapabilityIds.RuntimeTrees]),
            Capability(
                AvaScopeCapabilityIds.RuntimeSourceMap,
                "runtime",
                "Return conservative runtime node source provenance including XAML file, line, x:Name, style, template, resource, and binding path hints where available.",
                ["inspect-node", "visual-tree", "logical-tree", "inspect_node", "visual_tree", "logical_tree"],
                requires: [AvaScopeCapabilityIds.RuntimeInspect]),
            Capability(
                AvaScopeCapabilityIds.RuntimeBindingInspector,
                "runtime",
                "Return live DataContext, bound property, binding expression, resolved value, converter, fallback, null, and compiled binding diagnostics where public runtime metadata is available.",
                ["inspect-node", "inspect_node"],
                requires: [AvaScopeCapabilityIds.RuntimeInspect]),
            Capability(
                AvaScopeCapabilityIds.RuntimeLayoutExplain,
                "runtime",
                "Explain node layout state with DesiredSize, Bounds, clipping, Grid, ScrollViewer, ancestor, and constraint diagnostics.",
                ["inspect-node", "explain-layout", "inspect_node", "explain_layout"],
                requires: [AvaScopeCapabilityIds.RuntimeInspect]),
            Capability(
                AvaScopeCapabilityIds.RuntimeFind,
                "runtime",
                "Find runtime nodes by type, name, automation id, text, and bounded depth/result limits.",
                ["find-nodes", "find_nodes"],
                requires: [AvaScopeCapabilityIds.RuntimeTrees]),
            Capability(
                AvaScopeCapabilityIds.RuntimeInput,
                "runtime",
                "Send local runtime pointer, keyboard, text, focus, clear, and selection input to attached sessions.",
                ["input"],
                requires: [AvaScopeCapabilityIds.RuntimeAttach]),
            Capability(
                AvaScopeCapabilityIds.RuntimeSemanticWorkflow,
                "runtime",
                "Run semantic workflow steps against a local bridge session using AutomationId, text, role, binding path, command, or stable node selectors.",
                ["run-workflow", "run_workflow"],
                requires: [AvaScopeCapabilityIds.RuntimeFind, AvaScopeCapabilityIds.RuntimeInput]),
            Capability(
                AvaScopeCapabilityIds.RuntimeScenarioRunner,
                "runtime",
                "Run safe local runtime scenarios by launching or attaching to a bridge session, isolating launched app state, executing semantic workflow steps, and writing timeline artifacts.",
                ["run-scenario", "run_scenario"],
                requires: [AvaScopeCapabilityIds.RuntimeSemanticWorkflow, AvaScopeCapabilityIds.RuntimeSessionLifecycle]),
            Capability(
                AvaScopeCapabilityIds.RuntimePointerDiagnostics,
                "runtime",
                "Run pointer-path diagnostics with move/wait/screenshot/assert steps, visual-tree hit-path snapshots, popup-like layer inference, pointer overlays, and bounded transition warnings.",
                ["pointer-diagnostics", "pointer_diagnostics"],
                requires: [AvaScopeCapabilityIds.RuntimeInput, AvaScopeCapabilityIds.RuntimeTrees, AvaScopeCapabilityIds.ArtifactsScreenshot],
                metadata: new Dictionary<string, string> { ["transitionProvenance"] = "bounds_snapshot_inference" }),
            Capability(
                AvaScopeCapabilityIds.RuntimeMutationContract,
                "runtime",
                "Apply and report local-only runtime mutation requests with explicit capability and diagnostic metadata.",
                ["mutate-node", "mutate_node"],
                requires: [AvaScopeCapabilityIds.RuntimeAttach],
                metadata: new Dictionary<string, string>
                {
                    ["runtimeMutationCapability"] = RuntimeMutationCapabilityCatalog.RuntimeMutationContract,
                    ["temporary"] = "true"
                }),
            Capability(
                AvaScopeCapabilityIds.RuntimeStyleLayoutMutation,
                "runtime",
                "Apply reversible local style, layout, class, resource, text, and content runtime overrides through the bridge.",
                ["mutate-node", "mutate_node"],
                requires: [AvaScopeCapabilityIds.RuntimeMutationContract],
                metadata: new Dictionary<string, string>
                {
                    ["runtimeMutationCapability"] = RuntimeMutationCapabilityCatalog.StyleLayoutMutation,
                    ["resetOperations"] = "reset_mutation,reset_all"
                }),
            Capability(
                AvaScopeCapabilityIds.RuntimeMutationEvidence,
                "runtime",
                "Capture before/after screenshots, tree snapshots, diffs, and review artifacts around runtime mutations.",
                ["mutate-node-evidence", "mutate_node_evidence"],
                requires: [AvaScopeCapabilityIds.RuntimeStyleLayoutMutation, AvaScopeCapabilityIds.ArtifactsScreenshot]),
            Capability(
                AvaScopeCapabilityIds.RuntimeMutationReview,
                "runtime",
                "Read bounded mutation history, active overrides, reset handoff, and review artifact metadata.",
                ["mutation-review", "mutation_review"],
                requires: [AvaScopeCapabilityIds.RuntimeMutationContract]),
            Capability(
                AvaScopeCapabilityIds.RuntimeSourceSuggestions,
                "runtime",
                "Return conservative source-aware suggestions for runtime mutation review without editing source files.",
                ["mutation-review", "mutation_review"],
                requires: [AvaScopeCapabilityIds.RuntimeMutationReview]),
            Capability(
                AvaScopeCapabilityIds.RuntimeUiAudit,
                "runtime",
                "Produce bounded accessibility, validation, control, class, and component-pattern audit inventory reports.",
                ["audit-ui", "audit_ui"],
                requires: [AvaScopeCapabilityIds.RuntimeTrees]),
            Capability(
                AvaScopeCapabilityIds.PreviewAxaml,
                "preview",
                "Render Avalonia AXAML views through the isolated PreviewHost using the real Avalonia runtime.",
                ["preview", "preview_axaml"]),
            Capability(
                AvaScopeCapabilityIds.PreviewMultiSize,
                "preview",
                "Render multiple preview viewport sizes and optional contact sheets.",
                ["preview", "preview_axaml_multi"],
                requires: [AvaScopeCapabilityIds.PreviewAxaml]),
            Capability(
                AvaScopeCapabilityIds.PreviewAnimation,
                "preview",
                "Render selected animation time offsets with optional frame strip and HTML viewer artifacts.",
                ["preview-animation", "preview_axaml_animation"],
                requires: [AvaScopeCapabilityIds.PreviewAxaml, AvaScopeCapabilityIds.ArtifactsHtmlViewer]),
            Capability(
                AvaScopeCapabilityIds.PreviewStateVariants,
                "preview",
                "Render explicit design data state variants such as empty, loading, error, long text, validation, or dense data states.",
                ["preview", "preview-animation", "create-preview-session", "baseline-create", "preview_axaml", "preview_axaml_multi", "preview_axaml_animation", "create_preview_session"],
                requires: [AvaScopeCapabilityIds.PreviewAxaml]),
            Capability(
                AvaScopeCapabilityIds.PreviewSessions,
                "preview",
                "Create, list, reload, close, watch, and persist local preview sessions.",
                ["create-preview-session", "list-preview-sessions", "reload-preview-session", "close-preview-session", "watch-preview-session", "create_preview_session", "list_preview_sessions", "reload", "close_preview_session"],
                requires: [AvaScopeCapabilityIds.PreviewAxaml]),
            Capability(
                AvaScopeCapabilityIds.PreviewReload,
                "preview",
                "Reload preview sessions and return explicit unsupported diagnostics for runtime sessions.",
                ["reload", "reload-preview-session"],
                requires: [AvaScopeCapabilityIds.PreviewSessions]),
            Capability(
                AvaScopeCapabilityIds.PreviewViewer,
                "preview",
                "Export file-backed HTML preview viewer artifacts for local browser review.",
                ["preview-viewer", "preview_viewer"],
                requires: [AvaScopeCapabilityIds.PreviewSessions, AvaScopeCapabilityIds.ArtifactsHtmlViewer]),
            Capability(
                AvaScopeCapabilityIds.DiagnosticsSummary,
                "diagnostics",
                "Report service, bridge, preview host, preview session, and bounded diagnostic issue summaries.",
                ["diagnostics", "doctor"]),
            Capability(
                AvaScopeCapabilityIds.BaselineSingle,
                "baseline",
                "Create and check single-view preview baselines with screenshot diff outputs.",
                ["baseline-create", "baseline-check", "baseline_create", "baseline_check"],
                requires: [AvaScopeCapabilityIds.PreviewAxaml, AvaScopeCapabilityIds.ArtifactsDiffImage]),
            Capability(
                AvaScopeCapabilityIds.BaselineSuite,
                "baseline",
                "Create and check structured baseline suite manifests with profile and variant expansion.",
                ["baseline-create", "baseline-check", "baseline_create", "baseline_check"],
                requires: [AvaScopeCapabilityIds.BaselineSingle]),
            Capability(
                AvaScopeCapabilityIds.BaselineComparisonRules,
                "baseline",
                "Apply tolerance, changed-pixel thresholds, ignored regions, and required region assertions during baseline checks.",
                ["baseline-check", "baseline_check"],
                requires: [AvaScopeCapabilityIds.BaselineSingle]),
            Capability(
                AvaScopeCapabilityIds.ReportsJson,
                "reports",
                "Write bounded JSON reports for baseline and agent-facing workflows.",
                ["baseline-check", "baseline_check"]),
            Capability(
                AvaScopeCapabilityIds.ReportsAgentReview,
                "reports",
                "Return bounded agentReview summaries with report paths, artifact paths, review URLs, and next actions.",
                ["preview-viewer", "baseline-check", "mutate-node-evidence", "mutation-review", "preview_viewer", "baseline_check", "mutate_node_evidence", "mutation_review"]),
            Capability(
                AvaScopeCapabilityIds.ReportsEvidencePack,
                "reports",
                "Write uploadable JSON, HTML, JUnit, SARIF, and artifact index report packs for baseline checks.",
                ["baseline-check", "baseline_check"],
                requires: [AvaScopeCapabilityIds.ReportsJson, AvaScopeCapabilityIds.ArtifactsJunitSarif]),
            Capability(
                AvaScopeCapabilityIds.ArtifactsScreenshot,
                "artifacts",
                "Write local screenshot PNG artifacts from runtime and preview workflows.",
                ["screenshot", "preview", "mutate-node-evidence", "preview_axaml", "mutate_node_evidence"]),
            Capability(
                AvaScopeCapabilityIds.ArtifactsDiffImage,
                "artifacts",
                "Write local image diff artifacts for screenshot and baseline comparison workflows.",
                ["diff", "baseline-check", "mutate-node-evidence", "baseline_check", "mutate_node_evidence"]),
            Capability(
                AvaScopeCapabilityIds.ArtifactsHtmlViewer,
                "artifacts",
                "Write local HTML review and viewer artifacts with file-backed URLs.",
                ["preview-viewer", "preview-animation", "mutation-review", "mutate-node-evidence", "preview_viewer", "mutation_review", "mutate_node_evidence"]),
            Capability(
                AvaScopeCapabilityIds.ArtifactsJunitSarif,
                "artifacts",
                "Write JUnit and SARIF-style report assets for CI and agent evidence review.",
                ["baseline-check", "baseline_check"])
        ];
    }

    private static IReadOnlyList<AvaScopeToolCapability> CreateTools()
    {
        return
        [
            Cli("capabilities", AvaScopeCapabilityIds.ProtocolCapabilityDiscovery, AvaScopeCapabilityIds.ProtocolToolResultV1),
            Cli("mcp", AvaScopeCapabilityIds.ProtocolMcpStdioServer),
            Mcp("capabilities", AvaScopeCapabilityIds.ProtocolCapabilityDiscovery, AvaScopeCapabilityIds.ProtocolToolResultV1),
            Mcp("health", AvaScopeCapabilityIds.ProtocolToolResultV1),
            Cli("doctor", AvaScopeCapabilityIds.DiagnosticsSummary, AvaScopeCapabilityIds.SafetyLocalOnly),
            Cli("diagnostics", AvaScopeCapabilityIds.DiagnosticsSummary, AvaScopeCapabilityIds.SafetyLocalOnly),
            Mcp("diagnostics", AvaScopeCapabilityIds.DiagnosticsSummary, AvaScopeCapabilityIds.SafetyLocalOnly),
            Cli("attach", AvaScopeCapabilityIds.RuntimeAttach),
            Mcp("attach_to_app", AvaScopeCapabilityIds.RuntimeAttach),
            Cli("launch-app", AvaScopeCapabilityIds.RuntimeSessionLifecycle, AvaScopeCapabilityIds.SafetyLocalOnly),
            Mcp("launch_app", AvaScopeCapabilityIds.RuntimeSessionLifecycle, AvaScopeCapabilityIds.SafetyLocalOnly),
            Cli("list-top-levels", AvaScopeCapabilityIds.RuntimeAttach),
            Mcp("list_top_levels", AvaScopeCapabilityIds.RuntimeAttach),
            Cli("visual-tree", AvaScopeCapabilityIds.RuntimeTrees, AvaScopeCapabilityIds.RuntimeSourceMap),
            Cli("logical-tree", AvaScopeCapabilityIds.RuntimeTrees, AvaScopeCapabilityIds.RuntimeSourceMap),
            Mcp("visual_tree", AvaScopeCapabilityIds.RuntimeTrees, AvaScopeCapabilityIds.RuntimeSourceMap),
            Mcp("logical_tree", AvaScopeCapabilityIds.RuntimeTrees, AvaScopeCapabilityIds.RuntimeSourceMap),
            Cli("inspect-node", AvaScopeCapabilityIds.RuntimeInspect, AvaScopeCapabilityIds.RuntimeSourceMap, AvaScopeCapabilityIds.RuntimeBindingInspector, AvaScopeCapabilityIds.RuntimeLayoutExplain),
            Mcp("inspect_node", AvaScopeCapabilityIds.RuntimeInspect, AvaScopeCapabilityIds.RuntimeSourceMap, AvaScopeCapabilityIds.RuntimeBindingInspector, AvaScopeCapabilityIds.RuntimeLayoutExplain),
            Cli("explain-layout", AvaScopeCapabilityIds.RuntimeLayoutExplain),
            Mcp("explain_layout", AvaScopeCapabilityIds.RuntimeLayoutExplain),
            Cli("find-nodes", AvaScopeCapabilityIds.RuntimeFind),
            Mcp("find_nodes", AvaScopeCapabilityIds.RuntimeFind),
            Cli("audit-ui", AvaScopeCapabilityIds.RuntimeUiAudit),
            Mcp("audit_ui", AvaScopeCapabilityIds.RuntimeUiAudit),
            Cli("input", AvaScopeCapabilityIds.RuntimeInput),
            Mcp("input", AvaScopeCapabilityIds.RuntimeInput),
            Cli("run-workflow", AvaScopeCapabilityIds.RuntimeSemanticWorkflow),
            Mcp("run_workflow", AvaScopeCapabilityIds.RuntimeSemanticWorkflow),
            Cli("run-scenario", AvaScopeCapabilityIds.RuntimeScenarioRunner, AvaScopeCapabilityIds.SafetyLocalOnly),
            Mcp("run_scenario", AvaScopeCapabilityIds.RuntimeScenarioRunner, AvaScopeCapabilityIds.SafetyLocalOnly),
            Cli("pointer-diagnostics", AvaScopeCapabilityIds.RuntimePointerDiagnostics, AvaScopeCapabilityIds.ArtifactsScreenshot),
            Mcp("pointer_diagnostics", AvaScopeCapabilityIds.RuntimePointerDiagnostics, AvaScopeCapabilityIds.ArtifactsScreenshot),
            Cli("mutate-node", AvaScopeCapabilityIds.RuntimeMutationContract, AvaScopeCapabilityIds.RuntimeStyleLayoutMutation),
            Mcp("mutate_node", AvaScopeCapabilityIds.RuntimeMutationContract, AvaScopeCapabilityIds.RuntimeStyleLayoutMutation),
            Cli("mutate-node-evidence", AvaScopeCapabilityIds.RuntimeMutationEvidence, AvaScopeCapabilityIds.ArtifactsScreenshot, AvaScopeCapabilityIds.ArtifactsDiffImage),
            Mcp("mutate_node_evidence", AvaScopeCapabilityIds.RuntimeMutationEvidence, AvaScopeCapabilityIds.ArtifactsScreenshot, AvaScopeCapabilityIds.ArtifactsDiffImage),
            Cli("mutation-review", AvaScopeCapabilityIds.RuntimeMutationReview, AvaScopeCapabilityIds.RuntimeSourceSuggestions, AvaScopeCapabilityIds.ArtifactsHtmlViewer),
            Mcp("mutation_review", AvaScopeCapabilityIds.RuntimeMutationReview, AvaScopeCapabilityIds.RuntimeSourceSuggestions, AvaScopeCapabilityIds.ArtifactsHtmlViewer),
            Cli("close-session", AvaScopeCapabilityIds.RuntimeSessionLifecycle),
            Mcp("close_session", AvaScopeCapabilityIds.RuntimeSessionLifecycle),
            Cli("screenshot", AvaScopeCapabilityIds.ArtifactsScreenshot),
            Mcp("screenshot", AvaScopeCapabilityIds.ArtifactsScreenshot),
            Cli("preview", AvaScopeCapabilityIds.PreviewAxaml, AvaScopeCapabilityIds.PreviewMultiSize, AvaScopeCapabilityIds.PreviewStateVariants, AvaScopeCapabilityIds.ArtifactsScreenshot),
            Mcp("preview_axaml", AvaScopeCapabilityIds.PreviewAxaml, AvaScopeCapabilityIds.PreviewStateVariants, AvaScopeCapabilityIds.ArtifactsScreenshot),
            Mcp("preview_axaml_multi", AvaScopeCapabilityIds.PreviewMultiSize, AvaScopeCapabilityIds.PreviewStateVariants, AvaScopeCapabilityIds.ArtifactsScreenshot),
            Cli("preview-animation", AvaScopeCapabilityIds.PreviewAnimation, AvaScopeCapabilityIds.PreviewStateVariants, AvaScopeCapabilityIds.ArtifactsHtmlViewer),
            Mcp("preview_axaml_animation", AvaScopeCapabilityIds.PreviewAnimation, AvaScopeCapabilityIds.PreviewStateVariants, AvaScopeCapabilityIds.ArtifactsHtmlViewer),
            Cli("create-preview-session", AvaScopeCapabilityIds.PreviewSessions, AvaScopeCapabilityIds.PreviewStateVariants),
            Mcp("create_preview_session", AvaScopeCapabilityIds.PreviewSessions, AvaScopeCapabilityIds.PreviewStateVariants),
            Cli("list-preview-sessions", AvaScopeCapabilityIds.PreviewSessions),
            Mcp("list_preview_sessions", AvaScopeCapabilityIds.PreviewSessions),
            Cli("reload-preview-session", AvaScopeCapabilityIds.PreviewReload),
            Cli("reload", AvaScopeCapabilityIds.PreviewReload),
            Mcp("reload", AvaScopeCapabilityIds.PreviewReload),
            Cli("close-preview-session", AvaScopeCapabilityIds.PreviewSessions),
            Mcp("close_preview_session", AvaScopeCapabilityIds.PreviewSessions),
            Cli("watch-preview-session", AvaScopeCapabilityIds.PreviewSessions, AvaScopeCapabilityIds.PreviewReload),
            Cli("preview-viewer", AvaScopeCapabilityIds.PreviewViewer, AvaScopeCapabilityIds.ArtifactsHtmlViewer),
            Mcp("preview_viewer", AvaScopeCapabilityIds.PreviewViewer, AvaScopeCapabilityIds.ArtifactsHtmlViewer),
            Cli("baseline-create", AvaScopeCapabilityIds.BaselineSingle, AvaScopeCapabilityIds.BaselineSuite, AvaScopeCapabilityIds.PreviewStateVariants),
            Cli("baseline-check", AvaScopeCapabilityIds.BaselineSingle, AvaScopeCapabilityIds.BaselineSuite, AvaScopeCapabilityIds.BaselineComparisonRules, AvaScopeCapabilityIds.ReportsJson, AvaScopeCapabilityIds.ReportsEvidencePack),
            Mcp("baseline_check", AvaScopeCapabilityIds.BaselineSingle, AvaScopeCapabilityIds.BaselineSuite, AvaScopeCapabilityIds.BaselineComparisonRules, AvaScopeCapabilityIds.ReportsJson, AvaScopeCapabilityIds.ReportsEvidencePack),
            Cli("diff", AvaScopeCapabilityIds.ArtifactsDiffImage),
            Mcp("assert_region", AvaScopeCapabilityIds.BaselineComparisonRules),
            Cli("assert-region", AvaScopeCapabilityIds.BaselineComparisonRules),
            Cli("cleanup", AvaScopeCapabilityIds.PreviewSessions),
            Mcp("cleanup", AvaScopeCapabilityIds.PreviewSessions),
            Cli("cleanup-bridge-sessions", AvaScopeCapabilityIds.SafetyLocalOnly),
            Mcp("cleanup_bridge_sessions", AvaScopeCapabilityIds.SafetyLocalOnly),
            Mcp("list_sessions", AvaScopeCapabilityIds.ProtocolToolResultV1)
        ];
    }

    private static AvaScopeCapability Capability(
        string id,
        string category,
        string description,
        IReadOnlyList<string> tools,
        IReadOnlyList<string>? requires = null,
        IReadOnlyDictionary<string, string>? metadata = null)
    {
        return new AvaScopeCapability(
            id,
            category,
            AvaScopeCapabilityStatuses.Available,
            description,
            AvaScopeProtocol.CurrentVersion,
            tools,
            requires,
            metadata: metadata);
    }

    private static AvaScopeToolCapability Cli(string name, params string[] capabilityIds)
    {
        return new AvaScopeToolCapability("cli", name, capabilityIds);
    }

    private static AvaScopeToolCapability Mcp(string name, params string[] capabilityIds)
    {
        return new AvaScopeToolCapability("mcp", name, capabilityIds);
    }
}
