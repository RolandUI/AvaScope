using System.Globalization;

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
            RuntimeMutationCapabilityCatalog.CurrentBridgeCapabilities(),
            productVersion: AvaScopeProduct.Version);
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
                ["capabilities"],
                metadata: new Dictionary<string, string> { ["productVersion"] = AvaScopeProduct.Version }),
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
                AvaScopeCapabilityIds.RuntimeEffectiveCapabilities,
                "runtime",
                "Negotiate the effective protocol, bridge methods, input actions, automation patterns, mutation support, and native picker mode of one attached session.",
                ["attach", "attach_to_app", "session-capabilities", "session_capabilities"],
                requires: [AvaScopeCapabilityIds.RuntimeAttach],
                metadata: new Dictionary<string, string>
                {
                    ["revision"] = "sha256",
                    ["fallback"] = "attach_effectiveCapabilities_null_for_older_bridge"
                }),
            Capability(
                AvaScopeCapabilityIds.RuntimeSessionLifecycle,
                "runtime",
                "Launch local bridge-enabled apps and close active local bridge sessions through explicit local lifecycle commands.",
                ["launch-app", "launch_app", "close-session", "close_session"],
                requires: [AvaScopeCapabilityIds.SafetyLocalOnly],
                metadata: new Dictionary<string, string>
                {
                    ["closeSessionDefault"] = "close_session_only",
                    ["optionalProcessTermination"] = "avascope_owned_launches_only",
                    ["closeSessionExample"] = """{"sessionId":"<id>","terminateLaunchedProcess":true}"""
                }),
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
                "Return conservative node-to-source provenance: XAML file/line, x:Name, style/template/resource origins, and binding path hints where public metadata is available.",
                ["inspect-node", "visual-tree", "logical-tree", "inspect_node", "visual_tree", "logical_tree"],
                requires: [AvaScopeCapabilityIds.RuntimeInspect]),
            Capability(
                AvaScopeCapabilityIds.RuntimeBindingInspector,
                "runtime",
                "Expose node-level live binding and DataContext diagnostics: DataContext type/value summary, bound property/expression, resolved value, converter, fallback/null state, and compiled binding issues where public runtime metadata is available.",
                ["inspect-node", "inspect_node"],
                requires: [AvaScopeCapabilityIds.RuntimeInspect]),
            Capability(
                AvaScopeCapabilityIds.RuntimeLayoutExplain,
                "runtime",
                "Explain why a node is 0x0, clipped, or constrained by parent layout, including DesiredSize, Bounds, available constraints, Grid row/column sizing, ScrollViewer viewport, clipping ancestors, and other layout diagnostics where available.",
                ["inspect-node", "explain-layout", "inspect_node", "explain_layout"],
                requires: [AvaScopeCapabilityIds.RuntimeInspect]),
            Capability(
                AvaScopeCapabilityIds.RuntimeFind,
                "runtime",
                "Find runtime nodes by identity plus visible, enabled, rendered, and actionable state with bounded depth/result limits.",
                ["find-nodes", "find_nodes"],
                requires: [AvaScopeCapabilityIds.RuntimeTrees],
                metadata: new Dictionary<string, string>
                {
                    ["identityFilters"] = "nodeType,name,automationId,text",
                    ["stateFilters"] = "visible,enabled,rendered,actionable",
                    ["interactionState"] = "visible,enabled,rendered,actionable,availableActions"
                }),
            Capability(
                AvaScopeCapabilityIds.RuntimeInput,
                "runtime",
                "Send local runtime pointer, keyboard, text, focus, clear, selection, scroll, bounds-derived gesture, and semantic automation input to attached sessions.",
                ["input"],
                requires: [AvaScopeCapabilityIds.RuntimeAttach],
                metadata: new Dictionary<string, string>
                {
                    ["actions"] = string.Join(",", InputActions.All),
                    ["pointer_move"] = """{"required":["x","y"],"targets":["TopLevel"],"example":{"action":"pointer_move","x":100,"y":80}}""",
                    ["pointer_down"] = """{"required":["x","y"],"targets":["TopLevel"],"example":{"action":"pointer_down","x":100,"y":80}}""",
                    ["pointer_up"] = """{"required":["x","y"],"targets":["TopLevel"],"example":{"action":"pointer_up","x":100,"y":80}}""",
                    ["click"] = """{"requiredAny":[["x","y"],["targetNodeId"]],"targets":["Button"],"example":{"action":"click","targetNodeId":"visual:1"}}""",
                    ["key_text"] = """{"required":["inputText"],"optional":["targetNodeId"],"targets":["TextBox"],"example":{"action":"key_text","targetNodeId":"visual:1","inputText":"hello"}}""",
                    ["clear_text"] = """{"required":["targetNodeId"],"targets":["TextBox"],"example":{"action":"clear_text","targetNodeId":"visual:1"}}""",
                    ["focus"] = """{"requiredAny":[["targetNodeId"],["x","y"]],"targets":["Control"],"example":{"action":"focus","targetNodeId":"visual:1"}}""",
                    ["key_down"] = """{"required":["inputKey"],"optional":["targetNodeId","keyModifiers"],"targets":["Control"],"example":{"action":"key_down","inputKey":"Enter"}}""",
                    ["key_up"] = """{"required":["inputKey"],"optional":["targetNodeId","keyModifiers"],"targets":["Control"],"example":{"action":"key_up","inputKey":"Enter"}}""",
                    ["invoke"] = """{"required":["targetNodeId"],"patterns":["Invoke"],"example":{"action":"invoke","targetNodeId":"visual:1"}}""",
                    ["select"] = """{"required":["targetNodeId"],"optional":["inputText"],"patterns":["SelectionItem"],"example":{"action":"select","targetNodeId":"visual:1"}}""",
                    ["toggle"] = """{"required":["targetNodeId"],"patterns":["Toggle"],"example":{"action":"toggle","targetNodeId":"visual:1"}}""",
                    ["expand"] = """{"required":["targetNodeId"],"patterns":["ExpandCollapse"],"example":{"action":"expand","targetNodeId":"visual:1"}}""",
                    ["collapse"] = """{"required":["targetNodeId"],"patterns":["ExpandCollapse"],"example":{"action":"collapse","targetNodeId":"visual:1"}}""",
                    ["scroll"] = """{"required":["targetNodeId"],"optional":["x","y"],"targets":["ScrollViewer"],"example":{"action":"scroll","targetNodeId":"visual:1","y":120}}""",
                    ["drag"] = """{"required":["targetNodeId"],"requiredAny":[["gesture.direction"],["gesture.destinationTargetNodeId"]],"optional":["gesture.distancePercentage","gesture.durationMs"],"providers":["RangeValue","pointer_fallback"],"example":{"action":"drag","targetNodeId":"visual:1","gesture":{"direction":"end","durationMs":300}}}""",
                    ["swipe"] = """{"required":["targetNodeId"],"requiredAny":[["gesture.direction"],["gesture.destinationTargetNodeId"]],"optional":["gesture.distancePercentage","gesture.durationMs"],"providers":["RangeValue","pointer_fallback"],"example":{"action":"swipe","targetNodeId":"visual:1","gesture":{"direction":"left","distancePercentage":75}}}""",
                    ["long_press"] = """{"required":["targetNodeId"],"optional":["gesture.durationMs"],"provider":"pointer_fallback","example":{"action":"long_press","targetNodeId":"visual:1","gesture":{"durationMs":800}}}""",
                    ["press_and_hold"] = """{"required":["targetNodeId"],"optional":["gesture.durationMs"],"provider":"pointer_fallback","example":{"action":"press_and_hold","targetNodeId":"visual:1","gesture":{"durationMs":1000}}}""",
                    ["gestureDirections"] = string.Join(",", GestureDirections.All),
                    ["gestureDurationMs"] = $"{InputGestureOptions.MinimumDurationMs}-{InputGestureOptions.MaximumDurationMs}",
                    ["clickCoordinates"] = "explicit_or_target_center",
                    ["explicitCoordinatePrecedence"] = "true",
                    ["coordinateSpace"] = "top_level_dip"
                }),
            Capability(
                AvaScopeCapabilityIds.RuntimeSemanticAutomation,
                "runtime",
                "Invoke, select, toggle, expand, collapse, or adjust a range gesture on a selected local runtime control through its public Avalonia automation provider.",
                ["input", "run-workflow", "run_workflow"],
                requires: [AvaScopeCapabilityIds.RuntimeInput],
                metadata: new Dictionary<string, string>
                {
                    ["actions"] = "invoke,select,toggle,expand,collapse,drag,swipe",
                    ["providerApi"] = "Avalonia.Automation.Provider",
                    ["unsupportedBehavior"] = "structured_error"
                }),
            Capability(
                AvaScopeCapabilityIds.RuntimeSemanticWorkflow,
                "runtime",
                "Run coordinate-free workflow steps with typed, bounded runtime-state waits, durable idempotency replay protection, and non-mutating action or mutation validation against a local bridge session.",
                ["run-workflow", "run_workflow"],
                requires: [AvaScopeCapabilityIds.RuntimeFind, AvaScopeCapabilityIds.RuntimeInput],
                metadata: new Dictionary<string, string>
                {
                    ["actions"] = string.Join(",", SemanticWorkflowActions.All),
                    ["waitActions"] = "wait_for_node,wait_for_state,wait_for_dialog",
                    ["waitDefaultTimeoutMs"] = "5000",
                    ["waitMaximumTimeoutMs"] = "60000",
                    ["waitDefaultPollIntervalMs"] = "100",
                    ["waitConditions"] = string.Join(",", SemanticWaitConditionKinds.All),
                    ["waitComparisons"] = string.Join(",", SemanticWaitComparisons.All),
                    ["waitSelectorResolution"] = "every_poll",
                    ["waitEvidence"] = "typed_observation,last_candidates,elapsed,next_action",
                    ["topLevelAliases"] = "workflow_scoped,semantic,per_use_resolution,session_scoped",
                    ["topLevelAliasSelectors"] = "title,kind,isActive",
                    ["topLevelAliasEvidence"] = "alias,resolved_top_level_id,bounded_candidates",
                    ["idempotency"] = "optional_step_key,file_backed,session_scoped,ttl_bounded,replay_detected",
                    ["idempotencyDefaultTtlMs"] = "300000",
                    ["dryRunActions"] = "validate_action,validate_mutation",
                    ["dryRunSideEffects"] = "none"
                }),
            Capability(
                AvaScopeCapabilityIds.RuntimeCustomActions,
                "runtime",
                "Discover and invoke app-registered, target-scoped runtime actions through an explicit local-only opt-in, activation allowlist, parameter schema, executability check, safety classification, and audit result.",
                ["custom-actions", "invoke-custom-action", "custom_actions", "invoke_custom_action", "run-workflow", "run_workflow"],
                requires: [AvaScopeCapabilityIds.RuntimeSemanticWorkflow, AvaScopeCapabilityIds.SafetyLocalOnly],
                metadata: new Dictionary<string, string>
                {
                    ["defaultState"] = "disabled",
                    ["targetScope"] = "visual_node",
                    ["safetyClassifications"] = string.Join(",", RuntimeCustomActionSafetyClassifications.All),
                    ["destructiveAuthorization"] = "app_and_request",
                    ["registrationApi"] = "AvaScopeBridgeRuntime.RegisterCustomAction"
                }),
            Capability(
                AvaScopeCapabilityIds.RuntimeScenarioRunner,
                "runtime",
                "Run safe local runtime scenarios by launching or attaching to a bridge session, isolating launched app state, executing semantic workflow steps, and writing timeline artifacts.",
                ["run-scenario", "run_scenario"],
                requires: [AvaScopeCapabilityIds.RuntimeSemanticWorkflow, AvaScopeCapabilityIds.RuntimeSessionLifecycle]),
            Capability(
                AvaScopeCapabilityIds.RuntimeNativePicker,
                "runtime",
                "Control Windows file/folder pickers owned by the selected session process or prepare deterministic isolated-scenario picker outcomes.",
                ["native_picker"],
                requires: [AvaScopeCapabilityIds.RuntimeAttach, AvaScopeCapabilityIds.SafetyLocalOnly],
                metadata: new Dictionary<string, string>
                {
                    ["platform"] = "windows",
                    ["operations"] = string.Join(",", NativePickerOperations.All),
                    ["predefinedResults"] = string.Join(",", NativePickerResultStates.Preparable),
                    ["processScope"] = "selected_session_process_only",
                    ["scenarioSemantics"] = "session_scoped_one_shot_ttl_request_correlated",
                    ["defaultPathRedaction"] = "true",
                    ["maximumTimeoutMs"] = "30000"
                }),
            Capability(
                AvaScopeCapabilityIds.RuntimePointerDiagnostics,
                "runtime",
                "Run pointer-path diagnostics with move/wait/screenshot/assert steps, visual-tree hit-path snapshots, popup-like layer inference, pointer overlays, and bounded transition warnings.",
                ["pointer-diagnostics", "pointer_diagnostics"],
                requires: [AvaScopeCapabilityIds.RuntimeInput, AvaScopeCapabilityIds.RuntimeTrees, AvaScopeCapabilityIds.ArtifactsScreenshot],
                metadata: new Dictionary<string, string> { ["transitionProvenance"] = "bounds_snapshot_inference" }),
            Capability(
                AvaScopeCapabilityIds.RuntimePseudoStateMatrix,
                "runtime",
                "Capture a selected runtime control across pseudo-states with reversible state forcing, labeled contact sheets, and structured diagnostics.",
                ["pseudo-state-matrix", "pseudo_state_matrix"],
                requires: [AvaScopeCapabilityIds.RuntimeInput, AvaScopeCapabilityIds.RuntimeStyleLayoutMutation, AvaScopeCapabilityIds.ArtifactsScreenshot],
                metadata: new Dictionary<string, string>
                {
                    ["defaultStates"] = "normal,pointerover,pressed,disabled,selected,selected+pointerover",
                    ["resetSemantics"] = "per_state_runtime_reset"
                }),
            Capability(
                AvaScopeCapabilityIds.RuntimeInteractionAnimation,
                "runtime",
                "Record frame sequences after scripted pointer, click, keyboard, and wait steps with geometry overlays, frame strips, and geometry assertions across frames.",
                ["record-interaction-animation", "record_interaction_animation"],
                requires: [AvaScopeCapabilityIds.RuntimeInput, AvaScopeCapabilityIds.RuntimeTrees, AvaScopeCapabilityIds.ArtifactsScreenshot],
                metadata: new Dictionary<string, string>
                {
                    ["defaultFrameOffsetsMs"] = "0,100,250",
                    ["maximumFrameOffsets"] = RuntimeInteractionAnimationRequest.MaximumFrameCount.ToString(CultureInfo.InvariantCulture),
                    ["assertionModes"] = "stable,equals,within_range,final_stable,not_clipped"
                }),
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
                AvaScopeCapabilityIds.RuntimeDesignQualityAudit,
                "runtime",
                "Produce task-scoped design-quality audits for alignment, spacing, repeated heights, contrast, seams, radius/layering, and wrapping with explicit exclusions and suppressions.",
                ["design-audit", "design_quality_audit"],
                requires: [AvaScopeCapabilityIds.RuntimeTrees, AvaScopeCapabilityIds.RuntimeSourceMap],
                metadata: new Dictionary<string, string>
                {
                    ["scopeControls"] = "node,name,automationId,sourcePath,region,changedNodes,changedSources",
                    ["suppressionControls"] = "excludeNodeIds,excludeNames,excludeTypes,excludeSourcePaths,suppressions"
                }),
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
                "Render named preview state variants supplied by preview profiles or design-data factories, such as empty, loading, error, long text, many rows, validation errors, or narrow viewport states; AvaScope does not synthesize arbitrary ViewModel state by itself.",
                ["preview", "preview-animation", "create-preview-session", "baseline-create", "preview_axaml", "preview_axaml_multi", "preview_axaml_animation", "create_preview_session"],
                requires: [AvaScopeCapabilityIds.PreviewAxaml]),
            Capability(
                AvaScopeCapabilityIds.PreviewSemanticDiff,
                "preview",
                "Compare a current screenshot against an arbitrary reference image and return bounded raw pixel regions plus heuristic semantic visual-delta findings with annotated crops.",
                ["semantic-diff", "semantic_diff"],
                requires: [AvaScopeCapabilityIds.ArtifactsDiffImage, AvaScopeCapabilityIds.ArtifactsScreenshot],
                metadata: new Dictionary<string, string>
                {
                    ["findingKinds"] = "center_mismatch,edge_mismatch,padding_difference,border_or_seam_difference,wrapping_difference",
                    ["semanticProvenance"] = "pixel_diff_connected_components,content_bounds_heuristics,line_band_heuristics"
                }),
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
                "Report service, bridge, preview host, preview session, bounded diagnostic issue summaries, active-only views, and concise next-command guidance.",
                ["diagnostics", "doctor"],
                metadata: new Dictionary<string, string> { ["modes"] = string.Join(",", DiagnosticsResponseModes.Values) }),
            Capability(
                AvaScopeCapabilityIds.PreviewDiagnosticBaseline,
                "preview",
                "Fingerprint, filter, and compare preview diagnostics against an artifact or explicit fingerprint baseline across one-shot, multi-size, animation, and session renders.",
                ["preview", "preview-animation", "create-preview-session", "preview_axaml", "preview_axaml_multi", "preview_axaml_animation", "create_preview_session"],
                requires: [AvaScopeCapabilityIds.PreviewAxaml, AvaScopeCapabilityIds.ReportsJson],
                metadata: new Dictionary<string, string>
                {
                    ["minimumSeverities"] = string.Join(",", PreviewMinimumSeverities.Values),
                    ["maximumBaselineFingerprints"] = PreviewDiagnosticBaseline.MaximumFingerprints.ToString(System.Globalization.CultureInfo.InvariantCulture)
                }),
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
                AvaScopeCapabilityIds.ArtifactsRunIndex,
                "artifacts",
                "Write per-run JSON/HTML indexes, latest-run pointers, and agent-resolvable artifact navigation metadata.",
                ["preview", "audit-ui", "baseline-check", "latest-run"],
                metadata: new Dictionary<string, string>
                {
                    ["indexFiles"] = "run-index.json,run-index.html,latest-run.json",
                    ["selectorFields"] = "task,runGroup,project,view,profile,variant,stateVariant,command"
                }),
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
            Cli("session-capabilities", AvaScopeCapabilityIds.RuntimeEffectiveCapabilities),
            Mcp("session_capabilities", AvaScopeCapabilityIds.RuntimeEffectiveCapabilities),
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
            Cli("audit-ui", AvaScopeCapabilityIds.RuntimeUiAudit, AvaScopeCapabilityIds.ArtifactsRunIndex),
            Mcp("audit_ui", AvaScopeCapabilityIds.RuntimeUiAudit),
            Cli("design-audit", AvaScopeCapabilityIds.RuntimeDesignQualityAudit),
            Mcp("design_quality_audit", AvaScopeCapabilityIds.RuntimeDesignQualityAudit),
            Cli("input", AvaScopeCapabilityIds.RuntimeInput, AvaScopeCapabilityIds.RuntimeSemanticAutomation),
            Mcp("input", AvaScopeCapabilityIds.RuntimeInput, AvaScopeCapabilityIds.RuntimeSemanticAutomation),
            Cli("custom-actions", AvaScopeCapabilityIds.RuntimeCustomActions),
            Mcp("custom_actions", AvaScopeCapabilityIds.RuntimeCustomActions),
            Cli("invoke-custom-action", AvaScopeCapabilityIds.RuntimeCustomActions),
            Mcp("invoke_custom_action", AvaScopeCapabilityIds.RuntimeCustomActions),
            Cli("run-workflow", AvaScopeCapabilityIds.RuntimeSemanticWorkflow, AvaScopeCapabilityIds.RuntimeSemanticAutomation),
            Mcp("run_workflow", AvaScopeCapabilityIds.RuntimeSemanticWorkflow, AvaScopeCapabilityIds.RuntimeSemanticAutomation),
            Cli("run-scenario", AvaScopeCapabilityIds.RuntimeScenarioRunner, AvaScopeCapabilityIds.SafetyLocalOnly),
            Mcp("run_scenario", AvaScopeCapabilityIds.RuntimeScenarioRunner, AvaScopeCapabilityIds.SafetyLocalOnly),
            Mcp("native_picker", AvaScopeCapabilityIds.RuntimeNativePicker, AvaScopeCapabilityIds.SafetyLocalOnly),
            Cli("native-picker", AvaScopeCapabilityIds.RuntimeNativePicker, AvaScopeCapabilityIds.SafetyLocalOnly),
            Cli("pointer-diagnostics", AvaScopeCapabilityIds.RuntimePointerDiagnostics, AvaScopeCapabilityIds.ArtifactsScreenshot),
            Mcp("pointer_diagnostics", AvaScopeCapabilityIds.RuntimePointerDiagnostics, AvaScopeCapabilityIds.ArtifactsScreenshot),
            Cli("pseudo-state-matrix", AvaScopeCapabilityIds.RuntimePseudoStateMatrix, AvaScopeCapabilityIds.ArtifactsScreenshot),
            Mcp("pseudo_state_matrix", AvaScopeCapabilityIds.RuntimePseudoStateMatrix, AvaScopeCapabilityIds.ArtifactsScreenshot),
            Cli("record-interaction-animation", AvaScopeCapabilityIds.RuntimeInteractionAnimation, AvaScopeCapabilityIds.ArtifactsScreenshot),
            Mcp("record_interaction_animation", AvaScopeCapabilityIds.RuntimeInteractionAnimation, AvaScopeCapabilityIds.ArtifactsScreenshot),
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
            Cli("preview", AvaScopeCapabilityIds.PreviewAxaml, AvaScopeCapabilityIds.PreviewMultiSize, AvaScopeCapabilityIds.PreviewStateVariants, AvaScopeCapabilityIds.PreviewDiagnosticBaseline, AvaScopeCapabilityIds.ArtifactsScreenshot, AvaScopeCapabilityIds.ArtifactsRunIndex),
            Mcp("preview_axaml", AvaScopeCapabilityIds.PreviewAxaml, AvaScopeCapabilityIds.PreviewStateVariants, AvaScopeCapabilityIds.PreviewDiagnosticBaseline, AvaScopeCapabilityIds.ArtifactsScreenshot),
            Mcp("preview_axaml_multi", AvaScopeCapabilityIds.PreviewMultiSize, AvaScopeCapabilityIds.PreviewStateVariants, AvaScopeCapabilityIds.PreviewDiagnosticBaseline, AvaScopeCapabilityIds.ArtifactsScreenshot),
            Cli("preview-animation", AvaScopeCapabilityIds.PreviewAnimation, AvaScopeCapabilityIds.PreviewStateVariants, AvaScopeCapabilityIds.PreviewDiagnosticBaseline, AvaScopeCapabilityIds.ArtifactsHtmlViewer),
            Mcp("preview_axaml_animation", AvaScopeCapabilityIds.PreviewAnimation, AvaScopeCapabilityIds.PreviewStateVariants, AvaScopeCapabilityIds.PreviewDiagnosticBaseline, AvaScopeCapabilityIds.ArtifactsHtmlViewer),
            Cli("create-preview-session", AvaScopeCapabilityIds.PreviewSessions, AvaScopeCapabilityIds.PreviewStateVariants, AvaScopeCapabilityIds.PreviewDiagnosticBaseline),
            Mcp("create_preview_session", AvaScopeCapabilityIds.PreviewSessions, AvaScopeCapabilityIds.PreviewStateVariants, AvaScopeCapabilityIds.PreviewDiagnosticBaseline),
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
            Cli("baseline-check", AvaScopeCapabilityIds.BaselineSingle, AvaScopeCapabilityIds.BaselineSuite, AvaScopeCapabilityIds.BaselineComparisonRules, AvaScopeCapabilityIds.ReportsJson, AvaScopeCapabilityIds.ReportsEvidencePack, AvaScopeCapabilityIds.ArtifactsRunIndex),
            Cli("latest-run", AvaScopeCapabilityIds.ArtifactsRunIndex),
            Mcp("baseline_check", AvaScopeCapabilityIds.BaselineSingle, AvaScopeCapabilityIds.BaselineSuite, AvaScopeCapabilityIds.BaselineComparisonRules, AvaScopeCapabilityIds.ReportsJson, AvaScopeCapabilityIds.ReportsEvidencePack),
            Cli("diff", AvaScopeCapabilityIds.ArtifactsDiffImage),
            Cli("semantic-diff", AvaScopeCapabilityIds.PreviewSemanticDiff, AvaScopeCapabilityIds.ArtifactsDiffImage),
            Mcp("semantic_diff", AvaScopeCapabilityIds.PreviewSemanticDiff, AvaScopeCapabilityIds.ArtifactsDiffImage),
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
