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
            Capability(AvaScopeCapabilityIds.RuntimeDesiredState, "runtime", "Read, conditionally dispatch and verify bounded desired states through public semantic/input paths.",
                ["ensure-state", "ensure_state"], requires: [AvaScopeCapabilityIds.RuntimeInput],
                metadata: new Dictionary<string, string> { ["properties"] = "checked,expanded,text,value,selection", ["requestId"] = "same-session exact-payload replay; 512 retained requests; no eviction or automatic retry", ["selection"] = "at most 32 fresh targets; complete realized selected set required", ["text"] = "4096 characters; TextBox routed input; no temporary property mutation", ["verification"] = "immediate public state plus target validation; not application persistence" }),
            Capability(AvaScopeCapabilityIds.RuntimeForms, "runtime", "Bounded form inventory and prevalidated fill with per-field verification and observable dependency changes.",
                ["inspect-form", "inspect_form", "fill-form", "fill_form"], requires: [AvaScopeCapabilityIds.RuntimeDesiredState],
                metadata: new Dictionary<string, string> { ["fields"] = "32 inspected; 16 filled", ["settleMs"] = "0..1000 per field; async validation beyond observation remains unknown", ["requestId"] = "64 same-session exact-payload fill results; no eviction", ["submit"] = "separate explicit operation; no automatic rollback", ["sensitive"] = "password values always redacted; explicit allowSensitiveInput required" }),
            Capability(AvaScopeCapabilityIds.RuntimeTables, "runtime", "Bounded structured public DataGrid data and verified row selection, cell editing and sorting.",
                ["query-table", "query_table", "table-action", "table_action"], requires: [AvaScopeCapabilityIds.RuntimeDesiredState],
                metadata: new Dictionary<string, string> { ["control"] = "optional host DataGrid 12.1.x; public APIs only", ["coverage"] = "available collection view; dataset completeness unknown", ["limits"] = "4096 scanned rows; 64 returned rows; 16 columns; 4 filters; 64 KiB query result", ["binding"] = "simple public row-property bindings on standard text/check columns; unsupported templates/paths are explicit", ["write"] = "stable unique key plus row/column generations; public control/edit APIs; no direct row writes", ["replay"] = "128 same-session exact-request results without eviction" }),
            Capability(AvaScopeCapabilityIds.RuntimeActionMap, "runtime", "Read-only search of observed menu/button routes, shortcuts and app-declared custom actions.",
                ["action-map", "action_map"], requires: [AvaScopeCapabilityIds.RuntimeInspect],
                metadata: new Dictionary<string, string> { ["scope"] = "one explicit window; visual controls and already-loaded menu items", ["limits"] = "4096 nodes; depth 32; 128 results; 64 KiB", ["lazyContent"] = "unknown; discovery never opens menus", ["shortcuts"] = "public hotkeys/key bindings distinguished from display-only gestures" }),
            Capability(AvaScopeCapabilityIds.RuntimeDispatchPreconditions, "runtime", "Explicit typed expected-state checks at the supported input dispatch boundary.",
                ["input", "run-workflow", "run_workflow"], requires: [AvaScopeCapabilityIds.RuntimeExpressions, AvaScopeCapabilityIds.RuntimeInput],
                metadata: new Dictionary<string, string> { ["request"] = "execution.preconditions; workflow inputExecution.preconditions", ["boundary"] = "UI-thread check after preparation and immediately before first event/provider/property invocation", ["scope"] = "explicit semantic/synthetic/native input; not custom actions/mutations or a transaction across async/external state", ["outcome"] = "checked operands, dispatched true/false/unknown; no automatic replay after response loss", ["policy"] = "optional preconditionPolicy; workflow evidence policy takes precedence" }),
            Capability(AvaScopeCapabilityIds.RuntimeOperations, "runtime", "Explicit app-reported long-operation progress, results and authorized cancellation.",
                ["operation", "custom-actions", "custom_actions", "invoke-custom-action", "invoke_custom_action"], requires: [AvaScopeCapabilityIds.RuntimeCustomActions],
                metadata: new Dictionary<string, string> { ["activation"] = "host allowlisted custom action declares supportsOperations and optional supportsCancellation", ["limits"] = "32 active; 128 total; terminal retention at most 10 minutes; wait 1..30000 ms", ["ownership"] = "exact bridge session and originating run when leased; closing observation does not cancel app work", ["completion"] = "app_reported; UI dispatch and cancellation request do not imply completion", ["reconnect"] = "query retained operation id; never redispatch because status is unknown" }),
            Capability(AvaScopeCapabilityIds.RuntimeNativeAccessibility, "runtime", "Opt-in comparison of bridge controls with native OS accessibility evidence.",
                ["audit-native-accessibility", "audit_native_accessibility"], requires: [AvaScopeCapabilityIds.RuntimeInspect],
                metadata: new Dictionary<string, string> { ["adapters"] = "Windows UI Automation; Linux X11 AT-SPI2 over local Unix D-Bus; other backends explicitly unsupported", ["limits"] = "256 nodes per source; 32 levels; 5000 ms; one native worker per session", ["mapping"] = "unique AutomationId or probable geometry/semantics; ambiguity and missing services are explicit", ["privacy"] = "no text values; AutomationId subtree exclusion policies conservatively refuse native audit; scalar redaction before IPC" }),
            Capability(AvaScopeCapabilityIds.RuntimePicking, "runtime", "Scoped current point-to-node picking and temporary input-transparent adorners.",
                ["pick_node", "highlight"], requires: [AvaScopeCapabilityIds.RuntimeInspect],
                metadata: new Dictionary<string, string> { ["coordinates"] = "top_level_dip,top_level_pixel,desktop; current geometry revision required", ["limits"] = "32 hit ancestors returned; 64 ancestry scan; 32 related top-levels; eight highlights; 100..5000 ms expiry", ["highlight"] = "public input-transparent Avalonia adorner; no focus/input/model mutation; cleared by ordinary screenshots, detach, expiry or close", ["occlusion"] = "current Avalonia hit path; modal/registered overlap and popup evidence; native/unrelated-window occlusion unverified" }),
            Capability(AvaScopeCapabilityIds.RuntimeScreenCapture, "runtime", "Paired rendered and host-authorized native desktop-region evidence.",
                ["capture_screen"], requires: [AvaScopeCapabilityIds.RuntimeInspect],
                metadata: new Dictionary<string, string> { ["modes"] = "paired,rendered,native", ["privacy"] = "host-only declared_test_desktop grant; matching request scope; allowNativeScreenCapture policy; masking before IPC/artifacts", ["coordinates"] = "render pixel output grid; physical desktop pixels on Win32/X11; Cocoa points on macOS; per-region native pixel sizes", ["backends"] = "Win32 BitBlt; X11 root XGetImage; macOS 15.2+ ScreenCaptureKit with existing Screen Recording permission", ["limits"] = "4 Mi pixels and 256 KiB masked PNG per image; 16 monitors; 250..5000 ms; one in-flight capture", ["interpretation"] = "sequential samples, explicit offscreen/missing pixels; differences are descriptive, not defect proof; no fallback" }),
            Capability(AvaScopeCapabilityIds.RuntimeWindows, "runtime", "Owned-window state, monitors and guarded public native window management.",
                ["window"], requires: [AvaScopeCapabilityIds.RuntimeInspect],
                metadata: new Dictionary<string, string> { ["actions"] = string.Join(',', RuntimeWindowRequest.Actions), ["scope"] = "registered session Window only; no close/global input/foreign process control", ["guards"] = "fresh top-level generation and window/monitor revision; explicit allowedWindowActions policy; control lease", ["coordinates"] = "Win32/X11 physical desktop pixels; macOS desktop points; monitor_dip offsets use monitor desktop scale; clientSize in DIPs", ["verification"] = "bounded before/after public geometry plus owned native focus/state; refusal or timeout is partial", ["limits"] = "16 monitors; 32 registered windows for z-order; 64 KiB request; 100..3000 ms observation", ["unsupported"] = "headless/unknown window operations; fullscreen transitions; unrelated occlusion" }),
            Capability(AvaScopeCapabilityIds.RuntimeNavigation, "runtime", "Bounded observed visits, explicitly reported routes and navigation loops without replay.",
                ["navigation"], requires: [AvaScopeCapabilityIds.RuntimeInspect],
                metadata: new Dictionary<string, string> { ["actions"] = "start,record,query,clear; no app input or navigation", ["limits"] = "8 runs; 128 visits/512 KiB per run; 64 KiB per visit; 1..32 returned visits/80 KiB; TTL 100..1800000 ms", ["identity"] = "existing debug-state navigation.surface/context/revision plus generation and sampled content; otherwise unique visits with uncertain matches", ["routes"] = "explicit caller-reported outcomes between retained bridge observations; no future-execution guarantee", ["retention"] = "memory only; fixed expiry; same session and exact policy; shutdown clears history" }),
            Capability(AvaScopeCapabilityIds.RuntimeScenes, "runtime", "Opted-in semantic canvas objects, relationships and guarded custom actions.",
                ["scene"], requires: [AvaScopeCapabilityIds.RuntimeInspect],
                metadata: new Dictionary<string, string> { ["scope"] = "host-declared objects and relationships only; no pixel inference", ["limits"] = "32 adapters; scan 256 objects/1 MiB; return 1..128 objects/96 KiB items; cooperative 2 seconds", ["geometry"] = "declared scene coordinates to canvas/top-level DIP axis-aligned bounds; unsupported perspective explicit", ["guards"] = "canvas, adapter, object generations plus scene/camera revision; recheck after action availability", ["actions"] = "existing authorized custom actions; requiresSceneObject prevents generic unguarded invocation" }),
            Capability(AvaScopeCapabilityIds.RuntimeTextEditing, "runtime", "Generation-pinned TextBox text, caret, selection and verified range editing.",
                ["edit-text", "edit_text"], requires: [AvaScopeCapabilityIds.RuntimeInput],
                metadata: new Dictionary<string, string> { ["actions"] = "read,select_range,replace_range,replace_selection,insert", ["limits"] = "8192 UTF-16 text units; 4096 replacement units; 128 retained edit ids", ["ranges"] = "start-inclusive/end-exclusive; reject split surrogate/CRLF", ["concurrency"] = "expectedRevision fingerprints text/caret/selection/constraints and target generations; no automatic replay", ["privacy"] = "password and policy-protected fields withhold whole state", ["support"] = "public Avalonia TextBox editing across backends; rich/custom editors unsupported" }),
            Capability(AvaScopeCapabilityIds.RuntimeTraces, "runtime", "Bounded pre-redacted request/operation traces and opted-in diagnostic adapters.",
                ["trace"], requires: [AvaScopeCapabilityIds.RuntimeInspect],
                metadata: new Dictionary<string, string> { ["limits"] = "4 traces; 128 events/96 KiB each; collection 1..60000 ms; expiry ten minutes after collection deadline", ["privacy"] = "start requires evidence policy; redaction/exclusions before retention/export", ["correlation"] = "bridge_request,operation_request,app_declared,uncorrelated; never root-cause inference", ["sources"] = "dispatch,operation; optional UI validation samples and host validation/binding/app_event/app_log adapters", ["artifacts"] = "optional policy-owned trace.json" }),
            Capability(AvaScopeCapabilityIds.RuntimeExpressions, "runtime", "Closed typed scalar/aggregate queries and compound assertions with complete operand evidence.",
                ["evaluate-runtime", "evaluate_runtime"], requires: [AvaScopeCapabilityIds.RuntimeFind],
                metadata: new Dictionary<string, string> { ["operators"] = "literal,value,count,count_true,sum,minimum,maximum,number,all,any,not,eq,ne,gt,ge,lt,le,add,subtract", ["limits"] = "8 sources; 48 expressions; expression depth 8; 2048 nodes/64 results per source; 128 KiB response; cooperative 2 seconds", ["consistency"] = "two samples in one dispatcher turn; partial/redacted/changing sources are indeterminate", ["numbers"] = "checked decimal; explicit invariant number conversion; ordinal strings", ["wait"] = "waitCondition.kind expression reuses the exact definition" }),
            Capability(AvaScopeCapabilityIds.RuntimeFocus, "runtime", "Observed framework/native focus, bounded navigation predictions and explicit state-changing Tab probes.",
                ["inspect-focus", "inspect_focus", "probe-focus", "probe_focus"], requires: [AvaScopeCapabilityIds.RuntimeInspect],
                metadata: new Dictionary<string, string> { ["scope"] = "selected window plus actual focused registered window", ["limits"] = "4096 nodes; 64 candidates; depth 32; 64 KiB snapshot", ["prediction"] = "public focus manager; custom navigation unknown; not observed key routing", ["probe"] = "one paired Tab/Shift+Tab from pinned current focus; no activation/restoration/replay", ["native"] = "Win32 foreground HWND, X11 exact input-focus XID, AppKit key window; headless/Wayland unknown or unsupported" }),
            Capability(AvaScopeCapabilityIds.RuntimeActionExplanation, "runtime", "Bounded action blockers, declared business evidence and validated activation points without UI side effects.",
                ["explain-action", "explain_action"], requires: [AvaScopeCapabilityIds.RuntimeInspect],
                metadata: new Dictionary<string, string> { ["maxReasons"] = "1..32", ["certainty"] = "proven,correlated,app_declared,unknown", ["activationPoint"] = "explicit,app_declared_local_dip,bounds_center; current Avalonia input hit test", ["nativeConfirmation"] = "unavailable", ["dispatchGuard"] = "execution.expectedGeometryRevision; revalidation before pointer dispatch" }),
            Capability(AvaScopeCapabilityIds.RuntimeSessionControl, "runtime", "Exclusive bounded session control with explicit ownership, monotonic expiry and no takeover of executing operations.",
                ["session-control", "session_control"], requires: [AvaScopeCapabilityIds.RuntimeAttach],
                metadata: new Dictionary<string, string> { ["ttlMs"] = "1000..300000", ["operations"] = "status,acquire,renew,release", ["readOnlyObservation"] = "unblocked", ["controlToken"] = "private bearer coordination token; never report or log it" }),
            Capability(AvaScopeCapabilityIds.RuntimeRunRecovery, "runtime", "Durable scenario identity, path reservation, process identity and evidence-preserving recovery.",
                ["run-scenario", "run_scenario", "recover-run", "recover_run", "list-agent-runs", "list_agent_runs"], requires: [AvaScopeCapabilityIds.RuntimeSessionControl],
                metadata: new Dictionary<string, string> { ["operations"] = "inspect,resume,cleanup", ["resume"] = "live original session only; uncertain actions are never replayed", ["cleanup"] = "exact owned process start time and private runtime markers; attached apps and evidence/data retained" }),
            Capability(AvaScopeCapabilityIds.RuntimeWorkflowExport, "runtime", "Export recorded AvaScope workflows with stable selectors, explicit verification and fresh replay bindings.",
                ["export-workflow", "export_workflow", "replay-workflow", "replay_workflow"], requires: [AvaScopeCapabilityIds.RuntimeSemanticWorkflow],
                metadata: new Dictionary<string, string> { ["defaultReplay"] = "validate_only", ["format"] = "AvaScope semantic workflow only", ["parameters"] = "explicit; no recorded defaults", ["review"] = "unverified assertions, unstable selectors and non-replayable inputs" }),
            Capability(
                AvaScopeCapabilityIds.RuntimeStandaloneProvider,
                "runtime",
                "Verify and pin an external reflection-loadable bridge provider; only the host's explicit bootstrap activates it.",
                ["verify-provider", "verify_provider"],
                requires: [AvaScopeCapabilityIds.SafetyLocalOnly],
                metadata: new Dictionary<string, string>
                {
                    ["bootstrap"] = "AvaScope.Bridge.Bootstrap.Start",
                    ["runtime"] = "net10.0",
                    ["avalonia"] = "[12.1.0,12.2.0)",
                    ["activation"] = "explicit_host_call_only"
                }),
            Capability(
                AvaScopeCapabilityIds.RuntimeIntegrationGuidance,
                "runtime",
                "Read-only project-aware guidance for host-owned compile-time activation with package or external provider integration.",
                ["integration-guide", "integration_guide"],
                requires: [AvaScopeCapabilityIds.RuntimeStandaloneProvider]),
            Capability(
                AvaScopeCapabilityIds.RuntimeIntegrationVerification,
                "runtime",
                "Verify the complete owned-host bridge lifecycle or independent disabled production output/startup with structured stage evidence.",
                ["verify-integration", "verify_integration"],
                requires: [AvaScopeCapabilityIds.RuntimeStandaloneProvider]),
            Capability(
                AvaScopeCapabilityIds.RuntimeAgentTestProfiles,
                "runtime",
                "Resolve versioned named test profiles with platform overrides, explicit providers and redacted environment references into existing scenarios.",
                ["resolve-test-profile", "resolve_test_profile", "run-scenario", "run_scenario"],
                requires: [AvaScopeCapabilityIds.RuntimeStandaloneProvider]),
            Capability(
                AvaScopeCapabilityIds.DiagnosticsTargetReadiness,
                "diagnostics",
                "Read-only target/profile/provider compatibility and native platform readiness with bounded isolated probes and explicit remediation.",
                ["doctor", "doctor_target"],
                requires: [AvaScopeCapabilityIds.RuntimeAgentTestProfiles]),
            Capability(AvaScopeCapabilityIds.RuntimeReadiness, "runtime",
                "Separate bridge, frame and host-declared readiness, bounded scoped stability and capture-after-render.",
                ["run-workflow", "run_workflow", "run-scenario", "run_scenario", "screenshot"],
                requires: [AvaScopeCapabilityIds.RuntimeSemanticWorkflow]),
            Capability(AvaScopeCapabilityIds.RuntimeObservation, "runtime",
                "Coordinated bounded windows, focus, nodes/actions, public validation and optional screenshot with sampled consistency and policy-redacted artifacts.",
                ["observe"], requires: [AvaScopeCapabilityIds.RuntimeInspect]),
            Capability(AvaScopeCapabilityIds.RuntimeObservationChanges, "runtime",
                "Session/filter/policy-scoped bounded sampled change journals with expiry, overflow resynchronization, cancellation and local long-poll.",
                ["observe-changes", "observe_changes"], requires: [AvaScopeCapabilityIds.RuntimeObservation]),
            Capability(AvaScopeCapabilityIds.RuntimeTestFixtures, "runtime",
                "Explicit host-owned fixture metadata and allowlisted test-resource preparation, typed parameters, bounded readiness and cleanup in scenarios.",
                ["custom-actions", "custom_actions", "run-scenario", "run_scenario"], requires: [AvaScopeCapabilityIds.RuntimeCustomActions],
                metadata: new Dictionary<string, string> { ["enabledByDefault"] = "false", ["resources"] = "explicit_host_declared_test_identities", ["arbitraryReset"] = "unsupported" }),
            Capability(AvaScopeCapabilityIds.RuntimeVirtualItems, "runtime",
                "Bounded find/reveal/select by explicit stable scalar item key, uniqueness checks and generation-aware re-resolution after virtualization.",
                ["virtual-item", "virtual_item"], requires: [AvaScopeCapabilityIds.RuntimeFind],
                metadata: new Dictionary<string, string> { ["collectionContract"] = "Avalonia.ItemsControl", ["maxItems"] = "10000", ["maxTimeoutMs"] = "3000" }),
            Capability(
                AvaScopeCapabilityIds.RuntimeManagedX11,
                "runtime",
                "Explicit Linux X11 scenarios with owned authenticated Xvfb, optional window manager/session bus, readiness and cleanup; existing displays remain external.",
                ["run-scenario", "run_scenario"],
                requires: [AvaScopeCapabilityIds.RuntimeScenarioRunner],
                metadata: new Dictionary<string, string> { ["platform"] = "linux", ["transport"] = "abstract_unix", ["tcp"] = "disabled", ["activation"] = "explicit_scenario_option" }),
            Capability(AvaScopeCapabilityIds.RuntimeManagedWayland, "runtime",
                "Explicit isolated Weston headless scenarios with observed output geometry, owned cleanup and native Avalonia Wayland backend verification.",
                ["run-scenario", "run_scenario"], requires: [AvaScopeCapabilityIds.RuntimeScenarioRunner],
                metadata: new Dictionary<string, string> { ["compositor"] = "Weston 13.x; headless pixman kiosk shell", ["transport"] = "private Unix socket; no TCP", ["keyboard"] = "fixed XKB configuration; no native input seat", ["scope"] = "native_wayland experimental; XWayland separately unsupported", ["operations"] = "semantic and explicit synthetic input; rendered screenshots; no native input/capture/window management" }),
            Capability(
                AvaScopeCapabilityIds.RuntimeEffectiveCapabilities,
                "runtime",
                "Negotiate the effective protocol, bridge methods, input actions, automation patterns, mutation support, and native picker mode of one attached session.",
                ["attach", "attach_to_app", "session-capabilities", "session_capabilities"],
                requires: [AvaScopeCapabilityIds.RuntimeAttach],
                metadata: new Dictionary<string, string>
                {
                    ["revision"] = "sha256",
                    ["fallback"] = "attach_effectiveCapabilities_null_for_older_bridge",
                    ["backendEvidence"] = "registered_top_level_platform_implementation",
                    ["operationEvidence"] = "input.provenance,screenshot.provenance",
                    ["unknownEvidence"] = "no_observed_backend_or_render_mode_is_not_native_coverage"
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
                    ["interactionState"] = "visible,enabled,rendered,actionable,availableActions",
                    ["relationships"] = "selector.relationships: parent,ancestor,descendant,labeled_by; explicit public relationships only",
                    ["queryAttributes"] = string.Join(",", RuntimeQueryRequest.SupportedAttributes),
                    ["queryLimits"] = "64 results;2048 realized nodes;32 tree levels;4 relationship levels;16 relationships;8 attributes;64 KiB response",
                    ["queryCoverage"] = "explicit missing,redacted,truncated,partial; selected top-level only; generation/data-context revalidation before action",
                    ["queryCli"] = "find-nodes --request query.json"
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
                    ["key_sequence"] = """{"required":["execution.keys"],"optional":["targetNodeId"],"example":{"action":"key_sequence","execution":{"strategy":"synthetic","keys":[{"key":"A","modifiers":"Control"}]}}}""",
                    ["inputStrategies"] = "semantic,synthetic,native; explicit execution never silently downgrades; omission preserves legacy routing",
                    ["nativeInputCoverage"] = "owned native window messages/events only; no global device injection, clipboard, IME composition, external drag/drop or unrelated process control",
                    ["nativeInputLimits"] = "macOS: navigation keys only, native drag unavailable; X11: base-group keys and standard modifier groups, literal Unicode unavailable; unsupported routes require an explicit strategy change",
                    ["compoundInput"] = "execution: left/right/middle, clickCount 1..3, keys <=32, intervalMs 0..250, durationMs 0..3000, motionSteps 1..120, linear/ease_in_out",
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
                    ["composition"] = "if_else,optional,retry_until,variables,use_fragment",
                    ["compositionValidation"] = "pre_dispatch,expanded_plan,all_static_errors,validate_only",
                    ["compositionTimeline"] = "execution_path,parent_step_id,attempt,source_fragment,skipped,retried",
                    ["compositionLimits"] = $"nesting={SemanticWorkflowLimits.MaximumNestingDepth},expanded_steps={SemanticWorkflowLimits.MaximumExpandedSteps},executions={SemanticWorkflowLimits.MaximumEstimatedExecutions},retry_attempts={SemanticWorkflowLimits.MaximumRetryAttempts},retry_iterations={SemanticWorkflowLimits.MaximumTotalRetryIterations},artifacts={SemanticWorkflowLimits.MaximumArtifacts},timeout_ms={SemanticWorkflowLimits.MaximumWorkflowTimeoutMs}",
                    ["idempotency"] = "optional_step_key,file_backed,session_scoped,ttl_bounded,replay_detected",
                    ["idempotencyDefaultTtlMs"] = "300000",
                    ["dryRunActions"] = "validate_action,validate_mutation",
                    ["dryRunSideEffects"] = "none"
                }),
            Capability(
                AvaScopeCapabilityIds.RuntimeWorkflowEvidence,
                "runtime",
                "Observe action pre-state, execute once, verify a bounded typed postcondition, and write deterministic local runtime evidence with optional redaction, masking, retention, authorization, and action safety policy.",
                ["run-workflow", "run-scenario", "run_workflow", "run_scenario"],
                requires: [AvaScopeCapabilityIds.RuntimeSemanticWorkflow, AvaScopeCapabilityIds.ReportsJson],
                metadata: new Dictionary<string, string>
                {
                    ["verification"] = "optional_per_action,typed_wait_model,bounded_timeout",
                    ["verificationCapture"] = "pre_inspection,post_inspection,optional_pre_post_screenshots",
                    ["failureEvidence"] = "inspection,visual_tree,selector_candidates,interaction_state,actions,binding_validation,top_levels,adjacent_steps,timeline",
                    ["unavailableEvidence"] = "explicit",
                    ["reports"] = "json,markdown,junit",
                    ["privacyPolicy"] = "text_and_automation_id_redaction,excluded_controls,screenshot_regions,fail_closed",
                    ["retention"] = "validated_owned_root,marker_owned_runs,age_or_count_bounded",
                    ["actionSafety"] = "explicit_allowlist,gestures_and_destructive_actions_denied_by_default,custom_action_allowlist",
                    ["authorization"] = "local_session_and_process_allowlists",
                    ["storage"] = "local_filesystem_only,network_upload_unavailable,redacted_action_audit",
                    ["treeDepthMaximum"] = SemanticWorkflowEvidenceOptions.MaximumTreeDepth.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    ["selectorCandidatesMaximum"] = SemanticWorkflowEvidenceOptions.MaximumSelectorCandidates.ToString(System.Globalization.CultureInfo.InvariantCulture)
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
                "Run safe local runtime scenarios with optional project builds, executable or project launch, bounded bridge readiness and attach, semantic workflows, durable logs and top-level evidence, and exact owned-process cleanup.",
                ["run-scenario", "run_scenario"],
                requires: [AvaScopeCapabilityIds.RuntimeSemanticWorkflow, AvaScopeCapabilityIds.RuntimeSessionLifecycle],
                metadata: new Dictionary<string, string>
                {
                    ["workflowComposition"] = "variables,fragments,if_else,optional,retry_until",
                    ["compositionValidation"] = "before_launch_attach_or_artifact_creation",
                    ["timelineIdentity"] = "execution_path,parent_step_id,attempt,source_fragment",
                    ["lifecycleStages"] = "build,launch,bridge_readiness,attach,top_levels,workflow,cleanup",
                    ["launchTargets"] = "command,project",
                    ["launchInputDisclosure"] = "environment_names_and_argument_count_only",
                    ["ownedCleanup"] = "session_process_id_and_start_time_verified_process_tree"
                }),
            Capability(
                AvaScopeCapabilityIds.RuntimeNativePicker,
                "runtime",
                "Control owned Windows, GTK3 X11 and AppKit dialogs with explicit route evidence; consume predefined results only through an explicit host hook.",
                ["native_picker"],
                requires: [AvaScopeCapabilityIds.RuntimeAttach, AvaScopeCapabilityIds.SafetyLocalOnly],
                metadata: new Dictionary<string, string>
                {
                    ["platform"] = "windows,x11_gtk3,macos_appkit;topLevelId_required_for_bridge_native_routes",
                    ["operations"] = string.Join(",", NativePickerOperations.All),
                    ["predefinedResults"] = string.Join(",", NativePickerResultStates.Preparable),
                    ["processScope"] = "selected_session_process_only",
                    ["scenarioSemantics"] = "session_scoped_one_shot_ttl_request_correlated",
                    ["defaultPathRedaction"] = "true",
                    ["maximumTimeoutMs"] = "3000_bridge;30000_legacy_windows",
                    ["unsupported"] = "uncorrelated_portals;appkit_open_panel_path_selection",
                    ["hostHook"] = "AvaScope.Bridge.Bootstrap.TakePreparedPickerResult(correlationId)"
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
                "Write bounded JSON reports for baseline and runtime workflow evidence.",
                ["baseline-check", "run-workflow", "run-scenario", "baseline_check", "run_workflow", "run_scenario"]),
            Capability(
                AvaScopeCapabilityIds.ReportsAgentReview,
                "reports",
                "Return bounded agentReview summaries with report paths, artifact paths, review URLs, and next actions.",
                ["preview-viewer", "baseline-check", "mutate-node-evidence", "mutation-review", "preview_viewer", "baseline_check", "mutate_node_evidence", "mutation_review"]),
            Capability(
                AvaScopeCapabilityIds.ReportsEvidencePack,
                "reports",
                "Write uploadable baseline JSON/HTML/JUnit/SARIF packs and runtime workflow JSON/Markdown/JUnit evidence packs.",
                ["baseline-check", "run-workflow", "run-scenario", "baseline_check", "run_workflow", "run_scenario"],
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
                ["baseline-check", "run-workflow", "run-scenario", "baseline_check", "run_workflow", "run_scenario"])
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
            Cli("session-control", AvaScopeCapabilityIds.RuntimeSessionControl),
            Mcp("session_control", AvaScopeCapabilityIds.RuntimeSessionControl),
            Cli("recover-run", AvaScopeCapabilityIds.RuntimeRunRecovery),
            Mcp("recover_run", AvaScopeCapabilityIds.RuntimeRunRecovery),
            Cli("list-agent-runs", AvaScopeCapabilityIds.RuntimeRunRecovery),
            Mcp("list_agent_runs", AvaScopeCapabilityIds.RuntimeRunRecovery),
            Cli("export-workflow", AvaScopeCapabilityIds.RuntimeWorkflowExport),
            Mcp("export_workflow", AvaScopeCapabilityIds.RuntimeWorkflowExport),
            Cli("replay-workflow", AvaScopeCapabilityIds.RuntimeWorkflowExport),
            Mcp("replay_workflow", AvaScopeCapabilityIds.RuntimeWorkflowExport),
            Cli("doctor", AvaScopeCapabilityIds.DiagnosticsSummary, AvaScopeCapabilityIds.SafetyLocalOnly, AvaScopeCapabilityIds.DiagnosticsTargetReadiness),
            Cli("verify-provider", AvaScopeCapabilityIds.RuntimeStandaloneProvider),
            Mcp("verify_provider", AvaScopeCapabilityIds.RuntimeStandaloneProvider),
            Cli("integration-guide", AvaScopeCapabilityIds.RuntimeIntegrationGuidance),
            Mcp("integration_guide", AvaScopeCapabilityIds.RuntimeIntegrationGuidance),
            Cli("verify-integration", AvaScopeCapabilityIds.RuntimeIntegrationVerification),
            Mcp("verify_integration", AvaScopeCapabilityIds.RuntimeIntegrationVerification),
            Cli("resolve-test-profile", AvaScopeCapabilityIds.RuntimeAgentTestProfiles),
            Mcp("resolve_test_profile", AvaScopeCapabilityIds.RuntimeAgentTestProfiles),
            Mcp("doctor_target", AvaScopeCapabilityIds.DiagnosticsTargetReadiness),
            Cli("observe", AvaScopeCapabilityIds.RuntimeObservation, AvaScopeCapabilityIds.SafetyLocalOnly),
            Mcp("observe", AvaScopeCapabilityIds.RuntimeObservation, AvaScopeCapabilityIds.SafetyLocalOnly),
            Cli("explain-action", AvaScopeCapabilityIds.RuntimeActionExplanation),
            Mcp("explain_action", AvaScopeCapabilityIds.RuntimeActionExplanation),
            Cli("ensure-state", AvaScopeCapabilityIds.RuntimeDesiredState),
            Mcp("ensure_state", AvaScopeCapabilityIds.RuntimeDesiredState),
            Cli("inspect-form", AvaScopeCapabilityIds.RuntimeForms),
            Mcp("inspect_form", AvaScopeCapabilityIds.RuntimeForms),
            Cli("fill-form", AvaScopeCapabilityIds.RuntimeForms),
            Mcp("fill_form", AvaScopeCapabilityIds.RuntimeForms),
            Cli("query-table", AvaScopeCapabilityIds.RuntimeTables),
            Mcp("query_table", AvaScopeCapabilityIds.RuntimeTables),
            Cli("action-map", AvaScopeCapabilityIds.RuntimeActionMap),
            Mcp("action_map", AvaScopeCapabilityIds.RuntimeActionMap),
            Cli("inspect-focus", AvaScopeCapabilityIds.RuntimeFocus),
            Mcp("inspect_focus", AvaScopeCapabilityIds.RuntimeFocus),
            Cli("probe-focus", AvaScopeCapabilityIds.RuntimeFocus),
            Mcp("probe_focus", AvaScopeCapabilityIds.RuntimeFocus),
            Cli("evaluate-runtime", AvaScopeCapabilityIds.RuntimeExpressions),
            Mcp("evaluate_runtime", AvaScopeCapabilityIds.RuntimeExpressions),
            Cli("operation", AvaScopeCapabilityIds.RuntimeOperations),
            Mcp("operation", AvaScopeCapabilityIds.RuntimeOperations),
            Cli("trace", AvaScopeCapabilityIds.RuntimeTraces),
            Mcp("trace", AvaScopeCapabilityIds.RuntimeTraces),
            Cli("edit-text", AvaScopeCapabilityIds.RuntimeTextEditing),
            Mcp("edit_text", AvaScopeCapabilityIds.RuntimeTextEditing),
            Cli("scene", AvaScopeCapabilityIds.RuntimeScenes),
            Mcp("scene", AvaScopeCapabilityIds.RuntimeScenes),
            Cli("navigation", AvaScopeCapabilityIds.RuntimeNavigation),
            Mcp("navigation", AvaScopeCapabilityIds.RuntimeNavigation),
            Cli("window", AvaScopeCapabilityIds.RuntimeWindows),
            Mcp("window", AvaScopeCapabilityIds.RuntimeWindows),
            Cli("capture-screen", AvaScopeCapabilityIds.RuntimeScreenCapture),
            Mcp("capture_screen", AvaScopeCapabilityIds.RuntimeScreenCapture),
            Cli("pick-node", AvaScopeCapabilityIds.RuntimePicking),
            Cli("audit-native-accessibility", AvaScopeCapabilityIds.RuntimeNativeAccessibility),
            Mcp("pick_node", AvaScopeCapabilityIds.RuntimePicking),
            Mcp("audit_native_accessibility", AvaScopeCapabilityIds.RuntimeNativeAccessibility),
            Cli("highlight", AvaScopeCapabilityIds.RuntimePicking),
            Mcp("highlight", AvaScopeCapabilityIds.RuntimePicking),
            Cli("table-action", AvaScopeCapabilityIds.RuntimeTables),
            Mcp("table_action", AvaScopeCapabilityIds.RuntimeTables),
            Cli("observe-changes", AvaScopeCapabilityIds.RuntimeObservationChanges, AvaScopeCapabilityIds.SafetyLocalOnly),
            Mcp("observe_changes", AvaScopeCapabilityIds.RuntimeObservationChanges, AvaScopeCapabilityIds.SafetyLocalOnly),
            Cli("virtual-item", AvaScopeCapabilityIds.RuntimeVirtualItems, AvaScopeCapabilityIds.SafetyLocalOnly),
            Mcp("virtual_item", AvaScopeCapabilityIds.RuntimeVirtualItems, AvaScopeCapabilityIds.SafetyLocalOnly),
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
            Cli("input", AvaScopeCapabilityIds.RuntimeInput, AvaScopeCapabilityIds.RuntimeSemanticAutomation, AvaScopeCapabilityIds.RuntimeDispatchPreconditions),
            Mcp("input", AvaScopeCapabilityIds.RuntimeInput, AvaScopeCapabilityIds.RuntimeSemanticAutomation, AvaScopeCapabilityIds.RuntimeDispatchPreconditions),
            Cli("custom-actions", AvaScopeCapabilityIds.RuntimeCustomActions, AvaScopeCapabilityIds.RuntimeTestFixtures),
            Mcp("custom_actions", AvaScopeCapabilityIds.RuntimeCustomActions, AvaScopeCapabilityIds.RuntimeTestFixtures),
            Cli("invoke-custom-action", AvaScopeCapabilityIds.RuntimeCustomActions),
            Mcp("invoke_custom_action", AvaScopeCapabilityIds.RuntimeCustomActions),
            Cli("run-workflow", AvaScopeCapabilityIds.RuntimeSemanticWorkflow, AvaScopeCapabilityIds.RuntimeSemanticAutomation, AvaScopeCapabilityIds.RuntimeDispatchPreconditions, AvaScopeCapabilityIds.RuntimeWorkflowEvidence, AvaScopeCapabilityIds.ReportsJson, AvaScopeCapabilityIds.ReportsEvidencePack, AvaScopeCapabilityIds.ArtifactsJunitSarif),
            Mcp("run_workflow", AvaScopeCapabilityIds.RuntimeSemanticWorkflow, AvaScopeCapabilityIds.RuntimeSemanticAutomation, AvaScopeCapabilityIds.RuntimeDispatchPreconditions, AvaScopeCapabilityIds.RuntimeWorkflowEvidence, AvaScopeCapabilityIds.ReportsJson, AvaScopeCapabilityIds.ReportsEvidencePack, AvaScopeCapabilityIds.ArtifactsJunitSarif),
            Cli("run-scenario", AvaScopeCapabilityIds.RuntimeScenarioRunner, AvaScopeCapabilityIds.RuntimeWorkflowEvidence, AvaScopeCapabilityIds.RuntimeManagedX11, AvaScopeCapabilityIds.RuntimeManagedWayland, AvaScopeCapabilityIds.RuntimeTestFixtures, AvaScopeCapabilityIds.SafetyLocalOnly),
            Mcp("run_scenario", AvaScopeCapabilityIds.RuntimeScenarioRunner, AvaScopeCapabilityIds.RuntimeWorkflowEvidence, AvaScopeCapabilityIds.RuntimeManagedX11, AvaScopeCapabilityIds.RuntimeManagedWayland, AvaScopeCapabilityIds.RuntimeTestFixtures, AvaScopeCapabilityIds.SafetyLocalOnly),
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
