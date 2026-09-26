using System.ComponentModel;
using System.Globalization;
using AvaScope.Core;
using AvaScope.Protocol;
using ModelContextProtocol.Server;

namespace AvaScope.Mcp;

[McpServerToolType]
public sealed class AvaScopeMcpTools
{
    [McpServerTool(Name = "audit_native_accessibility", Title = "Compare bridge and OS accessibility evidence", ReadOnly = true, Idempotent = true,
        Destructive = false, OpenWorld = false, UseStructuredContent = true)]
    [Description("Opt-in bounded native accessibility audit of one generation-pinned registered window. Compares public bridge peers with Windows UI Automation or Linux X11 AT-SPI2 data after process ownership checks. Returns both sources, mapping confidence and missing-name/role/state evidence; decorations, grouping, virtualization and native children can legitimately differ. Explicit pinned expectations may require a name/role/exposure. Unavailable services, unsupported backends, ambiguous mappings and partial trees never mean healthy. No native actions, text values, unrelated application trees, service activation or framework adapters. Optional scalar redaction applies before IPC; AutomationId subtree exclusion policies conservatively refuse native audit.")]
    public static async Task<ToolResult<NativeAccessibilityAuditResponse>> AuditNativeAccessibility(LocalBridgeClient bridgeClient,
        NativeAccessibilityAuditRequest request, string? manifestDirectory = null, CancellationToken cancellationToken = default)
        => ToToolResult(await CreateBridgeClient(bridgeClient, manifestDirectory).AuditNativeAccessibilityAsync(request, cancellationToken));

    [McpServerTool(Name = "pick_node", Title = "Pick a current node by point", ReadOnly = true, Idempotent = true,
        Destructive = false, OpenWorld = false, UseStructuredContent = true)]
    [Description("Queries geometry without x/y, then resolves a point with that expectedGeometryRevision inside one pinned top-level. Coordinates: top_level_dip, top_level_pixel or validated desktop units (physical pixels on Windows/X11, Cocoa points on macOS). Returns current generation-pinned hit path, bounded related-window/modal/popup evidence and explicit native occlusion uncertainty. Window movement/scale/size reject stale revisions; old screenshot content is not verified. No pointer motion, application input or global desktop picking.")]
    public static async Task<ToolResult<RuntimePickResponse>> PickNode(LocalBridgeClient bridgeClient,
        RuntimePickRequest request, string? manifestDirectory = null, CancellationToken cancellationToken = default)
        => ToToolResult(await CreateBridgeClient(bridgeClient, manifestDirectory).PickNodeAsync(request, cancellationToken));

    [McpServerTool(Name = "highlight", Title = "Temporarily highlight a selected node", ReadOnly = false, Idempotent = false,
        Destructive = false, OpenWorld = false, UseStructuredContent = true)]
    [Description("Shows, inspects or clears a bounded temporary Avalonia adorner on a pinned visual target. Show/clear respect the control lease; inspect is read-only. Opaque #RRGGBB border, 100..5000 ms lifetime, one per top-level/eight per session. Never focuses, dispatches input, reparents app content or writes target properties. The adorner is input-transparent, follows current transforms and is removed on expiry, detachment, close or any ordinary screenshot capture. Active means registered on the Avalonia adorner layer, not proof of native visibility. Missing layers are unsupported rather than changing host layout.")]
    public static async Task<ToolResult<RuntimeHighlightResponse>> Highlight(LocalBridgeClient bridgeClient,
        RuntimeHighlightRequest request, string? manifestDirectory = null, CancellationToken cancellationToken = default)
        => ToToolResult(await CreateBridgeClient(bridgeClient, manifestDirectory).HighlightAsync(request, cancellationToken));

    [McpServerTool(Name = "capture_screen", Title = "Capture paired render and native screen evidence", ReadOnly = true, Idempotent = false,
        Destructive = false, OpenWorld = false, UseStructuredContent = true)]
    [Description("Captures rendered, native or paired screenshot evidence for an observed session top-level generation. Native pixels require the host-only Bootstrap.SetNativeScreenCaptureScope(\"declared_test_desktop\") opt-in and request desktopScope; evidence policy additionally requires allowNativeScreenCapture. Captures only the client's desktop rectangle, including authorized occlusion; use an isolated test desktop. Masks before IPC/artifacts. Returns separate source/timestamps, desktop units, region transforms, missing off-screen pixels, local PNG paths and descriptive pixel comparison. Sequential samples are not atomic and differences are not defect proof. Native Win32/X11 and macOS 15.2+ ScreenCaptureKit; permission denial/unsupported backends are explicit, never replaced with a rendered image.")]
    public static async Task<ToolResult<RuntimeScreenCaptureResponse>> CaptureScreen(LocalBridgeClient bridgeClient,
        RuntimeScreenCaptureRequest request, string? manifestDirectory = null, CancellationToken cancellationToken = default)
        => ToToolResult(await CreateBridgeClient(bridgeClient, manifestDirectory).CaptureScreenAsync(request, cancellationToken));

    [McpServerTool(Name = "window", Title = "Inspect or manage an owned application window", ReadOnly = false, Idempotent = false,
        Destructive = false, OpenWorld = false, UseStructuredContent = true)]
    [Description("Inspects an explicit session top-level and its monitors, coordinate units, generation and revision. Actions activate, bring_to_front, minimize, maximize, restore, move and resize require that fresh target/revision, policy allowedWindowActions and the session control lease. Returns observed before/after state and verified, refused or partial outcome. Desktop coordinates are physical pixels on Windows/X11 and points on macOS; clientSize uses DIPs. bring_to_front verifies native focus and registered-window order only. Headless/unknown/fullscreen operations are unsupported. Never closes windows or controls unrelated processes; no automatic replay.")]
    public static async Task<ToolResult<RuntimeWindowResponse>> Window(LocalBridgeClient bridgeClient,
        RuntimeWindowRequest request, string? manifestDirectory = null, CancellationToken cancellationToken = default)
        => ToToolResult(await CreateBridgeClient(bridgeClient, manifestDirectory).WindowAsync(request, cancellationToken));

    [McpServerTool(Name = "navigation", Title = "Remember observed navigation routes", ReadOnly = false, Idempotent = false,
        Destructive = false, OpenWorld = false, UseStructuredContent = true)]
    [Description("Starts, records, queries or clears a bounded session/run navigation journal. Captures visits through bridge observations; records explicitly reported action outcomes using previousVisitId without dispatching input. Query visitId, stateKey or fromVisitId/toVisitId for retained evidence and routes; loops may be uncertain sampled matches. Optional identityTarget reads navigation.surface/context/revision from the existing opt-in debug-state provider. Similar trees never merge visits. Runs expire and cannot survive app restart; routes are observations, not guaranteed future plans.")]
    public static async Task<ToolResult<RuntimeNavigationResponse>> Navigation(LocalBridgeClient bridgeClient,
        RuntimeNavigationRequest request, string? manifestDirectory = null, CancellationToken cancellationToken = default)
        => ToToolResult(await CreateBridgeClient(bridgeClient, manifestDirectory).NavigationAsync(request, cancellationToken));

    [McpServerTool(Name = "scene", Title = "Inspect or act on declared canvas objects", ReadOnly = false, Idempotent = false,
        Destructive = false, OpenWorld = false, UseStructuredContent = true)]
    [Description("Inspects an opted-in semantic canvas adapter by exact objectId, objectType or relatedTo identity. Returns bounded declared objects, relationships, selection, transformed DIP bounds, coverage and generation/revision targets. To invoke a registered custom action, send action=invoke with the exact expectedObject, actionName and requestId; the scene is checked again after action availability callbacks. Host activation/allowlists, policy and control leases apply. Never infers hidden objects from pixels or sends coordinate input. Unknown outcomes must be inspected, not replayed.")]
    public static async Task<ToolResult<RuntimeSceneResponse>> Scene(LocalBridgeClient bridgeClient,
        RuntimeSceneRequest request, string? manifestDirectory = null, CancellationToken cancellationToken = default)
        => ToToolResult(await CreateBridgeClient(bridgeClient, manifestDirectory).SceneAsync(request, cancellationToken));

    [McpServerTool(Name = "edit_text", Title = "Read or edit an exact text range", ReadOnly = false, Idempotent = false,
        Destructive = false, OpenWorld = false, UseStructuredContent = true)]
    [Description("Read bounded TextBox text/caret/selection, then select_range, replace_range, replace_selection or insert with the exact observed expectedRevision and a unique requestId. Insert requires explicit start and forbids end; it does not infer the caret. UTF-16 start-inclusive/end-exclusive offsets; surrogate pairs and CRLF cannot be split. Password/protected fields and unsupported rich editors fail closed. Public routed editing preserves validation and undo; verify the returned text and caret. Identical edit ids retrieve retained outcomes without redispatch. Policy must allow inspect and desired state text for changes.")]
    public static async Task<ToolResult<RuntimeTextEditResponse>> EditText(LocalBridgeClient bridgeClient,
        RuntimeTextEditRequest request, string? manifestDirectory = null, CancellationToken cancellationToken = default)
        => ToToolResult(await CreateBridgeClient(bridgeClient, manifestDirectory).EditTextAsync(request, cancellationToken));

    [McpServerTool(Name = "trace", Title = "Collect bounded correlated runtime diagnostics", ReadOnly = false, Idempotent = false,
        Destructive = false, OpenWorld = false, UseStructuredContent = true)]
    [Description("Start/read/stop an explicit-window trace. Start requires an evidence policy applied before retention. Captures bridge request boundaries, app-reported operation transitions and opted-in host validation/binding/event/log adapters; optional validation samples are temporal evidence only. Input correlationId and custom-action requestId link workflow evidence. Query requestId/operationId labels matched, different and uncorrelated events without claiming root cause. Four traces, 128 events/96 KiB each, at most 60 seconds collection. Optional outputDirectory exports bounded sanitized trace.json under the policy-owned root.")]
    public static async Task<ToolResult<RuntimeTraceResponse>> Trace(LocalBridgeClient bridgeClient,
        RuntimeTraceRequest request, string? manifestDirectory = null, CancellationToken cancellationToken = default)
        => ToToolResult(await CreateBridgeClient(bridgeClient, manifestDirectory).TraceAsync(request, cancellationToken));

    [McpServerTool(Name = "operation", Title = "Observe or cancel app-reported operations", ReadOnly = false, Idempotent = false,
        Destructive = true, OpenWorld = false, UseStructuredContent = true)]
    [Description("Observe app-reported long work by operation id from a custom-action response. action=status/wait/cancel; wait is bounded to 30 seconds and waits for any terminal outcome, not necessarily success. Cancellation requires a host-declared capability, the original run's current control lease when leased, and action policy authorization. A cancellation request or timeout does not prove app work stopped. Results are session-scoped and retained for at most ten minutes/128 entries; never redispatch an action because its status is unknown.")]
    public static async Task<ToolResult<RuntimeOperationResponse>> Operation(LocalBridgeClient bridgeClient,
        RuntimeOperationRequest request, string? manifestDirectory = null, CancellationToken cancellationToken = default)
        => ToToolResult(await CreateBridgeClient(bridgeClient, manifestDirectory).OperationAsync(request, cancellationToken));

    [McpServerTool(Name = "evaluate_runtime", Title = "Evaluate typed runtime values and assertions", ReadOnly = true, Idempotent = true,
        Destructive = false, OpenWorld = false, UseStructuredContent = true)]
    [Description("Evaluate a bounded closed expression over explicitly selected UI projections. Returns typed scalars/counts/sums or compound all/any/not/comparison results with every operand, source scope and coverage. requireTrue asserts the same boolean result. Missing, redacted, partial or changing sources remain indeterminate; never loads/scrolls items or evaluates arbitrary code. Workflow waitCondition kind expression polls the same definition.")]
    public static async Task<ToolResult<RuntimeExpressionResponse>> EvaluateRuntime(LocalBridgeClient bridgeClient,
        RuntimeExpressionRequest request, string? manifestDirectory = null, CancellationToken cancellationToken = default)
        => ToToolResult(await CreateBridgeClient(bridgeClient, manifestDirectory).EvaluateRuntimeAsync(request, cancellationToken));

    [McpServerTool(Name = "inspect_focus", Title = "Inspect focus and keyboard navigation", ReadOnly = true, Idempotent = true,
        Destructive = false, OpenWorld = false, UseStructuredContent = true)]
    [Description("Observe framework focus with its actual registered window, native focus independently, scope/tab metadata, disabled candidates and bounded public next/previous predictions. Predictions are not observed key routing. Does not focus, activate or press keys; custom/hidden/native-child uncertainty remains explicit.")]
    public static async Task<ToolResult<RuntimeFocusSnapshot>> InspectFocus(LocalBridgeClient bridgeClient,
        RuntimeFocusInspectionRequest request, string? manifestDirectory = null, CancellationToken cancellationToken = default)
        => ToToolResult(await CreateBridgeClient(bridgeClient, manifestDirectory).InspectFocusAsync(request, cancellationToken));

    [McpServerTool(Name = "probe_focus", Title = "Probe one Tab navigation step", ReadOnly = false, Idempotent = false,
        Destructive = false, OpenWorld = false, UseStructuredContent = true)]
    [Description("STATE-CHANGING: send one Tab or Shift+Tab pair from a pinned currently focused target, then observe focus. May run application validation/commit/navigation handlers. Never activates a window, steals focus, restores state or automatically replays. Explicit synthetic/native strategy; native requires confirmed window focus. Compare before/after with prior predictions; unchanged focus alone does not prove a trap.")]
    public static async Task<ToolResult<RuntimeFocusProbeResponse>> ProbeFocus(LocalBridgeClient bridgeClient,
        RuntimeFocusProbeRequest request, string? manifestDirectory = null, CancellationToken cancellationToken = default)
        => ToToolResult(await CreateBridgeClient(bridgeClient, manifestDirectory).ProbeFocusAsync(request, cancellationToken));

    [McpServerTool(Name = "action_map", Title = "Search available application actions", ReadOnly = true, Idempotent = true,
        Destructive = false, OpenWorld = false, UseStructuredContent = true)]
    [Description("Search bounded menu/button routes, declared hotkeys, display-only gestures, key bindings and allowlisted custom actions in one window. Returns current availability, duplicate labels, target/reveal evidence and explicit unknown lazy/native content. Never opens a menu or invokes a command. Re-observe targets after revealing a route; availability is an observation, not dispatch authorization.")]
    public static async Task<ToolResult<RuntimeActionMapResponse>> ActionMap(LocalBridgeClient bridgeClient,
        RuntimeActionMapRequest request, string? manifestDirectory = null, CancellationToken cancellationToken = default)
        => ToToolResult(await CreateBridgeClient(bridgeClient, manifestDirectory).ActionMapAsync(request, cancellationToken));

    [McpServerTool(Name = "query_table", Title = "Query structured runtime table data", ReadOnly = true, Idempotent = true,
        Destructive = false, OpenWorld = false, UseStructuredContent = true)]
    [Description("Read typed public DataGrid cell values with stable row keys, selected columns, bounded AND filters and paging. Coverage describes the available public collection view, never the full application dataset. Missing values, unsupported bindings, duplicate keys and truncated scans remain explicit. No scrolling, selection or application-store queries are performed.")]
    public static async Task<ToolResult<RuntimeTableQueryResponse>> QueryTable(LocalBridgeClient bridgeClient,
        RuntimeTableQueryRequest request, string? manifestDirectory = null, CancellationToken cancellationToken = default)
        => ToToolResult(await CreateBridgeClient(bridgeClient, manifestDirectory).QueryTableAsync(request, cancellationToken));

    [McpServerTool(Name = "table_action", Title = "Select, edit or sort a runtime table", ReadOnly = false, Idempotent = true,
        Destructive = true, OpenWorld = false, UseStructuredContent = true)]
    [Description("Perform one explicit DataGrid select_row, edit_cell or sort intent by observed row/column identity. Re-resolves stable keys through realization and rejects ambiguity, stale generations, active edits and policy exclusions. Uses public control APIs and supported routed/provider editors, then verifies values/selection/order. Never writes row properties directly or rolls back a failed draft. Preserve the exact requestId and payload when retrieving uncertain results; no automatic write retry.")]
    public static async Task<ToolResult<RuntimeTableActionResponse>> TableAction(LocalBridgeClient bridgeClient,
        RuntimeTableActionRequest request, string? manifestDirectory = null, CancellationToken cancellationToken = default)
        => ToToolResult(await CreateBridgeClient(bridgeClient, manifestDirectory).TableActionAsync(request, cancellationToken));

    [McpServerTool(Name = "inspect_form", Title = "Inspect runtime form fields", ReadOnly = true, Idempotent = true,
        Destructive = false, OpenWorld = false, UseStructuredContent = true)]
    [Description("Read a bounded visual form scope: explicit labels, field values, choices, writable state and observed validation. Unknown required/async validation metadata remains unknown. Password values are always redacted. No input or submit is dispatched.")]
    public static async Task<ToolResult<RuntimeFormInspectionResponse>> InspectForm(LocalBridgeClient bridgeClient,
        RuntimeFormInspectionRequest request, string? manifestDirectory = null, CancellationToken cancellationToken = default)
        => ToToolResult(await CreateBridgeClient(bridgeClient, manifestDirectory).InspectFormAsync(request, cancellationToken));

    [McpServerTool(Name = "fill_form", Title = "Fill and verify runtime form fields", ReadOnly = false, Idempotent = true,
        Destructive = true, OpenWorld = false, UseStructuredContent = true)]
    [Description("Prevalidate up to 16 unique field selectors and typed desired values, fill through supported input/providers, and verify each field after bounded settling. Stops on changed plans or rejection and reports partial effects and appearing/changing fields. Never invokes submit or rollback. Exact requestId/payload replay retrieves the original result without repeating input; use a new id only for a newly observed intent. Sensitive input requires explicit permission and remains redacted.")]
    public static async Task<ToolResult<RuntimeFormFillResponse>> FillForm(LocalBridgeClient bridgeClient,
        RuntimeFormFillRequest request, string? manifestDirectory = null, CancellationToken cancellationToken = default)
        => ToToolResult(await CreateBridgeClient(bridgeClient, manifestDirectory).FillFormAsync(request, cancellationToken));

    [McpServerTool(Name = "ensure_state", Title = "Ensure desired runtime state", ReadOnly = false, Idempotent = true,
        Destructive = true, OpenWorld = false, UseStructuredContent = true)]
    [Description("Read, change only if necessary and verify checked/expanded/text/value/selection state on a fresh explicit target. Preserve requestId and the entire payload when retrieving an uncertain or lost result; no automatic write retry. Text uses routed input for TextBox, other supported states use public automation providers.")]
    public static async Task<ToolResult<RuntimeDesiredStateResponse>> EnsureState(LocalBridgeClient bridgeClient,
        RuntimeDesiredStateRequest request, string? manifestDirectory = null, CancellationToken cancellationToken = default)
        => ToToolResult(await CreateBridgeClient(bridgeClient, manifestDirectory).EnsureStateAsync(request, cancellationToken));

    [McpServerTool(Name = "explain_action", Title = "Explain runtime action blockers", ReadOnly = true, Idempotent = true,
        Destructive = false, OpenWorld = false, UseStructuredContent = true)]
    [Description("Explains bounded observable blockers, related validation and separately labeled app-declared reasons for an explicit live target. Resolves an application-declared local activation point or bounds center and validates current clipping and Avalonia input hit testing. Native OS visibility remains unknown. No input, reveal, focus, mutation or recovery is executed. Reuse the geometryRevision with explicit input execution.expectedGeometryRevision to reject stale geometry before dispatch; validation is still required at dispatch.")]
    public static async Task<ToolResult<RuntimeActionExplanation>> ExplainAction(LocalBridgeClient bridgeClient,
        RuntimeActionExplanationRequest request, string? manifestDirectory = null, CancellationToken cancellationToken = default)
        => ToToolResult(await CreateBridgeClient(bridgeClient, manifestDirectory).ExplainActionAsync(request, cancellationToken));

    [McpServerTool(Name = "export_workflow", Title = "Export recorded AvaScope workflow", ReadOnly = false, Idempotent = false,
        Destructive = false, OpenWorld = false, UseStructuredContent = true)]
    [Description("Exports an original AvaScope workflow request and its matching complete execution response to a parameterized native workflow and review document. Preserves actual assertions, action/redaction policy and idempotency; marks missing verification or unstable targets. Parameter values are never persisted as defaults. No foreign framework import or screenshot baseline acceptance. Does not replay actions.")]
    public static ToolResult<WorkflowExportResponse> ExportWorkflow(WorkflowExportRequest request)
        => ToToolResult(WorkflowExporter.Export(request));

    [McpServerTool(Name = "replay_workflow", Title = "Validate or replay exported workflow", ReadOnly = false, Idempotent = false,
        Destructive = false, OpenWorld = false, UseStructuredContent = true)]
    [Description("Binds an AvaScope export.json to an explicitly selected fresh session, output, named parameters and required policy authorization. Defaults to validateOnly=true through the existing compiler; false executes through the existing workflow runner and reports. Blocking review items require correcting and re-exporting the source; remaining review items require acknowledgeReview=true. Never automatically repeats uncertain actions or accepts baselines.")]
    public static async Task<ToolResult<SemanticWorkflowResponse>> ReplayWorkflow(LocalBridgeClient bridgeClient,
        WorkflowReplayRequest request, string? manifestDirectory = null, CancellationToken cancellationToken = default)
        => ToToolResult(await WorkflowExporter.ReplayAsync(CreateBridgeClient(bridgeClient, manifestDirectory), request, cancellationToken));

    [McpServerTool(Name = "list_agent_runs", Title = "List local agent runs", ReadOnly = true, Idempotent = true,
        Destructive = false, OpenWorld = false, UseStructuredContent = true)]
    [Description("Lists the newest bounded private local scenario recovery records, including active or abandoned runs. Does not discover unrelated processes, return lease tokens, resume actions or delete evidence. Use a returned runId with recover_run.")]
    public static ToolResult<IReadOnlyList<AgentRunRecoveryResponse>> ListAgentRuns(int maxResults = 25, string? storeDirectory = null)
        => ToToolResult(new AgentRunStore(storeDirectory).List(maxResults));

    [McpServerTool(Name = "recover_run", Title = "Inspect or recover agent run", ReadOnly = false, Idempotent = false,
        Destructive = false, OpenWorld = false, UseStructuredContent = true)]
    [Description("Inspects a durable local run, resumes control of its exact live session, or cleans up owned processes/private runtime resources after interruption. Refuses active runs, conflicting leases and changed process identities. Preserves evidence and test data. Resume returns a control token; it never replays an uncertain action or restarts the app. storeDirectory overrides the private local recovery store only when explicitly supplied.")]
    public static async Task<ToolResult<AgentRunRecoveryResponse>> RecoverRun(AgentRunRecoveryRequest request,
        string? storeDirectory = null, CancellationToken cancellationToken = default)
        => ToToolResult(await new AgentRunStore(storeDirectory).RecoverAsync(request, cancellationToken));

    [McpServerTool(Name = "session_control", Title = "Coordinate session control", ReadOnly = false, Idempotent = false,
        Destructive = false, OpenWorld = false, UseStructuredContent = true)]
    [Description("Inspects, acquires, renews or releases exclusive session control for an explicit owner/run. ttlMs is 1000..300000. The current MCP server remembers acquired tokens for subsequent control calls; reconnects use the returned token to renew. Other clients may observe but conflicting control fails before dispatch. Expiry never transfers an operation still executing. Tokens are coordination credentials; keep them out of reports.")]
    public static async Task<ToolResult<SessionControlResponse>> SessionControl(LocalBridgeClient bridgeClient, string sessionId,
        SessionControlRequest request, string? manifestDirectory = null, CancellationToken cancellationToken = default)
        => ToToolResult(await CreateBridgeClient(bridgeClient, manifestDirectory).SessionControlAsync(new(sessionId), request, cancellationToken));

    [McpServerTool(Name = "virtual_item", Title = "Resolve virtualized logical item", ReadOnly = false, Idempotent = false,
        Destructive = false, OpenWorld = false, UseStructuredContent = true)]
    [Description("Finds, reveals or selects one logical ItemsControl item using an explicitly chosen public stable scalar key property. Verifies uniqueness across a bounded complete ItemsView and re-resolves after realization. Duplicate keys, unsupported models, stale collections and limits fail explicitly; container ids and indices are diagnostic evidence, never persisted item identity. Host key getters must be fast and side-effect free; selection handlers may run application code.")]
    public static async Task<ToolResult<RuntimeVirtualItemResponse>> VirtualItem(LocalBridgeClient bridgeClient,
        RuntimeVirtualItemRequest request, string? manifestDirectory = null, CancellationToken cancellationToken = default)
        => ToToolResult(await CreateBridgeClient(bridgeClient, manifestDirectory).VirtualItemAsync(request, cancellationToken));

    [McpServerTool(Name = "observe_changes", Title = "Observe runtime changes", ReadOnly = true, Idempotent = true,
        Destructive = false, OpenWorld = false, UseStructuredContent = true)]
    [Description("Returns ordered bounded changes from an explicit observation cursor, or a baseline for initialization/resynchronization. Cursor scope includes session, filters and policy; expiry, eviction, restart, overflow and invalid scope never claim complete history. Optional bounded polling samples current state; intermediate states may be coalesced. Read a required response artifact before advancing its cursor.")]
    public static async Task<ToolResult<RuntimeObservationChangesResponse>> ObserveChanges(LocalBridgeClient bridgeClient,
        RuntimeObservationChangesRequest request, string? manifestDirectory = null, CancellationToken cancellationToken = default)
        => ToToolResult(await new RuntimeObserver().ObserveChangesAsync(CreateBridgeClient(bridgeClient, manifestDirectory), request, cancellationToken));

    [McpServerTool(Name = "observe", Title = "Observe runtime UI", ReadOnly = true, Idempotent = true,
        Destructive = false, OpenWorld = false, UseStructuredContent = true)]
    [Description("Collects selected windows, focus, bounded flat visual fragments, available actions, public validation diagnostics and optional screenshots in one request. Returns generation/correlation identifiers, per-part availability and sampled change detection; never claims atomic tree/screenshot consistency. Optional local evidence policy redacts/excludes every part before bounded artifact export.")]
    public static async Task<ToolResult<RuntimeObservationResponse>> Observe(LocalBridgeClient bridgeClient,
        RuntimeObservationRequest request, string? manifestDirectory = null, CancellationToken cancellationToken = default)
        => ToToolResult(await new RuntimeObserver().ObserveAsync(CreateBridgeClient(bridgeClient, manifestDirectory), request, cancellationToken));

    [McpServerTool(Name = "doctor_target", Title = "Target application readiness", ReadOnly = true,
        Idempotent = true, Destructive = false, OpenWorld = false, UseStructuredContent = true)]
    [Description("Extends doctor for an explicitly selected application/project/profile/provider and backend. Checks compatibility, dependencies, installation origins, actual X11 access, renderer/fonts and only requested native permissions/services in a bounded isolated probe. Does not load host code, activate a bridge, attach sessions or expose display authentication material.")]
    public static async Task<ToolResult<TargetReadinessResponse>> DoctorTarget(TargetReadinessRequest request, CancellationToken cancellationToken = default)
        => ToToolResult(await new TargetReadinessDoctor().CheckAsync(request, cancellationToken));

    [McpServerTool(Name = "resolve_test_profile", Title = "Resolve agent test profile", ReadOnly = true,
        Idempotent = true, Destructive = false, OpenWorld = false, UseStructuredContent = true)]
    [Description("Validates a named schemaVersion 1 test profile and returns redacted effective scenario settings, platform override precedence, profile-relative paths, provider identity and environment reference names. Does not build, launch or activate. Optional platform is for read-only preview; execution uses the actual platform.")]
    public static ToolResult<AgentTestProfileResponse> ResolveTestProfile(string profileFile, string profileName, string? platform = null)
        => ToToolResult(AgentTestProfiles.Resolve(profileFile, profileName, platform));

    [McpServerTool(Name = "verify_integration", Title = "Verify bridge integration", ReadOnly = false,
        Idempotent = false, Destructive = false, OpenWorld = false, UseStructuredContent = true)]
    [Description("Verifies one explicitly launched local host: optional build/provider checks, owned session discovery, window readiness, tree, screenshot, declared safe focus probe and shutdown cleanup. bootstrapDisabled selects the independent production-output and bounded no-manifest/no-listener lane. Produces a local stage report; never attaches to unrelated processes.")]
    public static async Task<ToolResult<BridgeIntegrationVerificationResponse>> VerifyIntegration(
        BridgeIntegrationVerificationRequest request, CancellationToken cancellationToken = default)
        => ToToolResult(await new BridgeIntegrationVerifier().RunAsync(request, cancellationToken));

    [McpServerTool(Name = "integration_guide", Title = "Bridge integration guidance", ReadOnly = true,
        Idempotent = true, Destructive = false, OpenWorld = false, UseStructuredContent = true)]
    [Description("Read-only, bounded analysis of an explicitly selected .csproj. Returns file/line/hash guidance for guarded package or external-provider integration, existing call sites, and unresolved startup/framework diagnostics. Does not build, evaluate MSBuild or edit source.")]
    public static ToolResult<BridgeIntegrationGuidanceResponse> IntegrationGuide(string projectPath, string? framework = null)
        => ToToolResult(BridgeIntegrationAdvisor.Analyze(projectPath, framework));

    [McpServerTool(Name = "verify_provider", Title = "Verify standalone provider", ReadOnly = true,
        Idempotent = true, Destructive = false, OpenWorld = false, UseStructuredContent = true)]
    [Description("Verifies an explicit external provider directory and optional exact version/manifest SHA-256 pins without loading or activating the bridge. Host Avalonia compatibility is checked at explicit activation.")]
    public static ToolResult<ProviderVerificationResponse> VerifyProvider(
        string directory, string? expectedVersion = null, string? expectedManifestSha256 = null)
        => ToToolResult(ProviderVerifier.Verify(directory, expectedVersion, expectedManifestSha256));

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
        Name = "session_capabilities",
        Title = "Session capabilities",
        ReadOnly = true,
        Idempotent = true,
        Destructive = false,
        OpenWorld = false,
        UseStructuredContent = true)]
    [Description("Returns the effective versioned capabilities negotiated with one active local bridge session.")]
    public static async Task<ToolResult<SessionCapabilitiesResponse>> SessionCapabilities(
        LocalBridgeClient bridgeClient,
        string sessionId,
        string? manifestDirectory = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(bridgeClient);
        if (!TryParseRequiredSessionId(sessionId, out var parsedSessionId, out var error))
        {
            return ToolResult<SessionCapabilitiesResponse>.Fail(error!);
        }

        return ToToolResult(await CreateBridgeClient(bridgeClient, manifestDirectory)
            .SessionCapabilitiesAsync(parsedSessionId!, cancellationToken));
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
    [Description("Captures a screenshot from an attached local AvaScope bridge session to a local output file. Optional captureAfterRender waits for a bounded composition frame and valid layout, returning readiness evidence.")]
    public static async Task<ToolResult<ScreenshotResponse>> Screenshot(
        LocalBridgeClient bridgeClient,
        string sessionId,
        string topLevelId,
        string outputPath,
        string? manifestDirectory = null,
        CancellationToken cancellationToken = default,
        bool captureAfterRender = false)
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
            cancellationToken,
            captureAfterRender));
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
        McpTreeKind treeKind = McpTreeKind.Visual,
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
            treeKind.ToProtocolName(),
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
        McpTreeKind treeKind = McpTreeKind.Visual,
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
            treeKind.ToProtocolName(),
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
    [Description("Finds nodes by identity/state or a bounded relationship selector. Optional attributes project typed values instead of nodes, with explicit partial coverage. Use selector for parent/ancestor/descendant/labeled_by relationships; do not mix it with flat filters. Structured queries accept explicit maxDepth up to 64 and maxNodes up to 2048 for deep templates; verify complete coverage and preserve the entire returned target before acting.")]
    public static async Task<ToolResult<FindNodesResponse>> FindNodes(
        LocalBridgeClient bridgeClient,
        string sessionId,
        string topLevelId,
        McpTreeKind treeKind = McpTreeKind.Visual,
        string? nodeType = null,
        string? name = null,
        string? automationId = null,
        string? text = null,
        bool? visible = null,
        bool? enabled = null,
        bool? rendered = null,
        bool? actionable = null,
        int? maxDepth = null,
        int? maxResults = null,
        bool includeChildren = false,
        bool includeBounds = true,
        bool includeAccessibility = false,
        bool includeBindings = false,
        int? maxResponseDepth = null,
        string? manifestDirectory = null,
        CancellationToken cancellationToken = default,
        SemanticWorkflowSelector? selector = null,
        IReadOnlyList<string>? attributes = null,
        int? maxNodes = null,
        RuntimeEvidencePolicy? policy = null)
    {
        ArgumentNullException.ThrowIfNull(bridgeClient);

        if (!TryParseRequiredSessionId(sessionId, out var parsedSessionId, out var error))
        {
            return ToolResult<FindNodesResponse>.Fail(error!);
        }

        return ToToolResult(await CreateBridgeClient(bridgeClient, manifestDirectory).FindNodesAsync(
            parsedSessionId!,
            topLevelId,
            treeKind.ToProtocolName(),
            nodeType: nodeType,
            name: name,
            automationId: automationId,
            text: text,
            maxDepth: maxDepth,
            maxResults: maxResults,
            cancellationToken: cancellationToken,
            includeChildren: includeChildren,
            includeBounds: includeBounds,
            includeAccessibility: includeAccessibility,
            includeBindings: includeBindings,
            maxResponseDepth: maxResponseDepth,
            visible: visible,
            enabled: enabled,
            rendered: rendered,
            actionable: actionable,
            selector: selector, attributes: attributes, maxNodes: maxNodes, policy: policy));
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
        McpTreeKind treeKind = McpTreeKind.Visual,
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
            McpTreeKind.Visual => await client.VisualTreeAsync(parsedSessionId!, topLevelId, maxDepth, cancellationToken),
            McpTreeKind.Logical => await client.LogicalTreeAsync(parsedSessionId!, topLevelId, maxDepth, cancellationToken),
            _ => throw new ArgumentOutOfRangeException(nameof(treeKind))
        };

        if (!tree.Success)
        {
            return ToolResult<UiAuditResponse>.Fail(new ProtocolError(
                tree.Error!.Code,
                tree.Error.Message,
                tree.Error.Details));
        }

        return ToToolResult(new UiAuditBuilder().Create(ResponseBudgeter.ReadTreeEvidence(tree.Value!), maxIssues, maxInventoryItems));
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
    [Description("Sends local-only input to an attached AvaScope bridge session. Click accepts explicit coordinates or the current target center; gestures derive coordinates from current target bounds and accept a direction or destination target. execution.preconditions accepts a closed typed expected-state expression checked on the UI thread immediately before the first dispatch; false/incomplete state rejects input with checked evidence. Dispatch or response loss can leave an uncertain outcome: inspect or use existing workflow idempotency, never blindly retry. Guards are not transactions across application handlers, subsequent compound events or external state.")]
    public static async Task<ToolResult<InputResponse>> Input(
        LocalBridgeClient bridgeClient,
        string sessionId,
        string topLevelId,
        [Description("Input action name, including invoke, select, toggle, expand, collapse, scroll, drag, swipe, long_press, and press_and_hold.")]
        McpInputAction action,
        double? x = null,
        double? y = null,
        string? inputText = null,
        string? targetNodeId = null,
        string? inputKey = null,
        string? keyModifiers = null,
        [Description("Gesture direction: left, right, up, down, start, or end.")]
        string? gestureDirection = null,
        [Description("Gesture distance percentage, greater than 0 and at most 100.")]
        double? gestureDistancePercentage = null,
        [Description("Bounded gesture duration in milliseconds (50-5000).")]
        int? gestureDurationMs = null,
        [Description("Current visual node id used as a destination for a source-to-target drag or swipe.")]
        string? destinationTargetNodeId = null,
        string? manifestDirectory = null,
        CancellationToken cancellationToken = default,
        InputExecutionOptions? execution = null)
    {
        ArgumentNullException.ThrowIfNull(bridgeClient);

        if (!TryParseRequiredSessionId(sessionId, out var parsedSessionId, out var error))
        {
            return ToolResult<InputResponse>.Fail(error!);
        }

        InputGestureOptions? gesture;
        try
        {
            gesture = CreateGestureOptions(gestureDirection, gestureDistancePercentage, gestureDurationMs, destinationTargetNodeId);
        }
        catch (ArgumentOutOfRangeException exception)
        {
            return ToolResult<InputResponse>.Fail(new ProtocolError(CoreErrorCodes.InvalidBridgeRequest, exception.Message));
        }

        return ToToolResult(await CreateBridgeClient(bridgeClient, manifestDirectory).InputAsync(
            parsedSessionId!,
            topLevelId,
            action.ToProtocolName(),
            x,
            y,
            inputText,
            targetNodeId,
            inputKey,
            keyModifiers,
            gesture,
            cancellationToken, execution: execution));
    }

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
        [Description("Gesture direction: left, right, up, down, start, or end.")]
        string? gestureDirection = null,
        [Description("Gesture distance percentage, greater than 0 and at most 100.")]
        double? gestureDistancePercentage = null,
        [Description("Bounded gesture duration in milliseconds (50-5000).")]
        int? gestureDurationMs = null,
        [Description("Current visual node id used as a destination for a source-to-target drag or swipe.")]
        string? destinationTargetNodeId = null,
        string? manifestDirectory = null,
        CancellationToken cancellationToken = default,
        InputExecutionOptions? execution = null)
    {
        ArgumentNullException.ThrowIfNull(bridgeClient);
        if (!TryParseRequiredSessionId(sessionId, out var parsedSessionId, out var error))
        {
            return ToolResult<InputResponse>.Fail(error!);
        }

        InputGestureOptions? gesture;
        try
        {
            gesture = CreateGestureOptions(gestureDirection, gestureDistancePercentage, gestureDurationMs, destinationTargetNodeId);
        }
        catch (ArgumentOutOfRangeException exception)
        {
            return ToolResult<InputResponse>.Fail(new ProtocolError(CoreErrorCodes.InvalidBridgeRequest, exception.Message));
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
            gesture,
            cancellationToken, execution: execution));
    }

    [McpServerTool(
        Name = "custom_actions",
        Title = "Custom actions",
        ReadOnly = true,
        Idempotent = true,
        Destructive = false,
        OpenWorld = false,
        UseStructuredContent = true)]
    [Description("Discovers app-registered, allowlisted runtime actions and their current executability, required state, parameter schema, and safety classification for one current node target.")]
    public static async Task<ToolResult<RuntimeCustomActionsResponse>> CustomActions(
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
            return ToolResult<RuntimeCustomActionsResponse>.Fail(error!);
        }

        RuntimeTargetContext target;
        try
        {
            target = new RuntimeTargetContext(parsedSessionId!, topLevelId, treeKind, nodeId);
        }
        catch (ArgumentException exception)
        {
            return ToolResult<RuntimeCustomActionsResponse>.Fail(new ProtocolError(
                CoreErrorCodes.InvalidBridgeRequest,
                exception.Message));
        }

        return ToToolResult(await CreateBridgeClient(bridgeClient, manifestDirectory).CustomActionsAsync(
            parsedSessionId!,
            target,
            cancellationToken));
    }

    [McpServerTool(
        Name = "invoke_custom_action",
        Title = "Invoke custom action",
        ReadOnly = false,
        Idempotent = false,
        Destructive = true,
        OpenWorld = false,
        UseStructuredContent = true)]
    [Description("Invokes one app-registered, allowlisted runtime action on a current node target. Destructive actions require explicit authorization from both the app and this request.")]
    public static async Task<ToolResult<RuntimeCustomActionResponse>> InvokeCustomAction(
        LocalBridgeClient bridgeClient,
        string sessionId,
        string topLevelId,
        string nodeId,
        string actionName,
        IReadOnlyDictionary<string, string>? parameters = null,
        string treeKind = TreeKinds.Visual,
        bool allowDestructive = false,
        string? requestId = null,
        string? manifestDirectory = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(bridgeClient);
        if (!TryParseRequiredSessionId(sessionId, out var parsedSessionId, out var error))
        {
            return ToolResult<RuntimeCustomActionResponse>.Fail(error!);
        }

        RuntimeCustomActionRequest request;
        try
        {
            request = new RuntimeCustomActionRequest(
                string.IsNullOrWhiteSpace(requestId) ? Guid.NewGuid().ToString("n") : requestId,
                new RuntimeTargetContext(parsedSessionId!, topLevelId, treeKind, nodeId),
                actionName,
                parameters,
                allowDestructive);
        }
        catch (ArgumentException exception)
        {
            return ToolResult<RuntimeCustomActionResponse>.Fail(new ProtocolError(
                CoreErrorCodes.InvalidBridgeRequest,
                exception.Message));
        }

        return ToToolResult(await CreateBridgeClient(bridgeClient, manifestDirectory).InvokeCustomActionAsync(
            parsedSessionId!,
            request,
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
    [Description("Runs or statically validates a bounded semantic local workflow with typed if/else branches, optional leaf steps, idempotent retry_until, variables, reusable acyclic fragments, workflow-scoped top-level aliases, rendered/command/binding/selection/value/lifecycle waits, and validate_action/validate_mutation dry runs. Readiness waits distinguish bridge, frame and application state; layout_stable/frame_stable sample a bounded optional selector scope. Semantic actions may declare verify to capture pre-state, execute once, and wait for a typed postcondition. Evidence can collect bounded failure context and export aligned JSON, Markdown, and JUnit reports; its optional explicit local policy adds redaction, screenshot masking, owned retention, local action audit, action allowlists, and session/process authorization, with network upload unavailable. validateOnly returns the fully expanded plan and all bounded static diagnostics without bridge dispatch.")]
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
    [Description("Runs a safe local runtime scenario by validating the workflow and optional local evidence/action policy before side effects; optionally building before launch for an executable or project; waiting for bridge readiness, attach, and registered top levels; optionally preparing a declared host test fixture with explicit test-resource identity, bounded readiness and required cleanup; executing observe-act-verify steps across bounded execution paths; preserving policy-redacted logs and failure evidence; and optionally terminating only the exact AvaScope-owned process tree.")]
    public static async Task<ToolResult<RuntimeScenarioResponse>> RunScenario(
        LocalBridgeClient bridgeClient,
        RuntimeScenarioRequest? request = null,
        string? manifestDirectory = null,
        CancellationToken cancellationToken = default,
        string? profileFile = null,
        string? profileName = null)
    {
        ArgumentNullException.ThrowIfNull(bridgeClient);
        if (profileFile is not null || profileName is not null)
        {
            if (request is not null || profileFile is null || profileName is null)
                return ToolResult<RuntimeScenarioResponse>.Fail(new ProtocolError("test_profile_selection_invalid", "Select either request or both profileFile and profileName."));
            return ToToolResult(await AgentTestProfiles.RunAsync(CreateBridgeClient(bridgeClient, manifestDirectory), profileFile, profileName, cancellationToken));
        }
        if (request is null) return ToolResult<RuntimeScenarioResponse>.Fail(new ProtocolError("runtime_scenario_request_required", "Provide request or a named test profile."));

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
        McpMutationOperation operation,
        McpTreeKind treeKind = McpTreeKind.Visual,
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

        if (operation == McpMutationOperation.SetProperty
            && !RuntimeMutationPropertyNames.IsSupported(propertyName))
        {
            return ToolResult<RuntimeMutationResponse>.Fail(new ProtocolError(
                RuntimeMutationErrorCodes.UnsupportedRuntimeMutationProperty,
                $"Runtime property '{propertyName}' is not supported.",
                new Dictionary<string, string>
                {
                    ["supportedProperties"] = string.Join(",", RuntimeMutationPropertyNames.All),
                    ["validationPhase"] = "pre_dispatch"
                }));
        }

        RuntimeMutationRequest request;
        try
        {
            request = new RuntimeMutationRequest(
                string.IsNullOrWhiteSpace(requestId) ? Guid.NewGuid().ToString("n") : requestId,
                new RuntimeTargetContext(parsedSessionId!, topLevelId, treeKind.ToProtocolName(), nodeId),
                new RuntimeMutationOperation(operation.ToProtocolName(), propertyName, value, valueType, className, resourceKey, mutationId),
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
        if (!TryParseRequiredSessionId(sessionId, out var parsedSessionId, out var error))
        {
            return ToolResult<RuntimeMutationResponse>.Fail(error!);
        }

        try
        {
            var request = new RuntimeMutationRequest(
                string.IsNullOrWhiteSpace(requestId) ? Guid.NewGuid().ToString("n") : requestId,
                new RuntimeTargetContext(parsedSessionId!, topLevelId, treeKind, nodeId),
                new RuntimeMutationOperation(operation, propertyName, value, valueType, className, resourceKey, mutationId),
                [RuntimeMutationCapabilityCatalog.RuntimeMutationContract, RuntimeMutationCapabilityCatalog.StyleLayoutMutation]);
            return ToToolResult(await CreateBridgeClient(bridgeClient, manifestDirectory).MutateNodeAsync(
                parsedSessionId!, request, cancellationToken));
        }
        catch (Exception exception) when (exception is ArgumentException or ArgumentOutOfRangeException)
        {
            return ToolResult<RuntimeMutationResponse>.Fail(new ProtocolError(CoreErrorCodes.InvalidBridgeRequest, exception.Message));
        }
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
        McpMutationOperation operation,
        string artifactDirectory,
        McpTreeKind treeKind = McpTreeKind.Visual,
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
                new RuntimeTargetContext(parsedSessionId!, topLevelId, treeKind.ToProtocolName(), nodeId),
                new RuntimeMutationOperation(operation.ToProtocolName(), propertyName, value, valueType, className, resourceKey, mutationId),
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
    [Description("Closes a local AvaScope bridge session. Optionally terminates the process only when it was launched and ownership-recorded by AvaScope.")]
    public static async Task<ToolResult<CloseSessionResponse>> CloseSession(
        LocalBridgeClient bridgeClient,
        string sessionId,
        string? manifestDirectory = null,
        bool terminateLaunchedProcess = false,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(bridgeClient);

        if (!TryParseRequiredSessionId(sessionId, out var parsedSessionId, out var error))
        {
            return ToolResult<CloseSessionResponse>.Fail(error!);
        }

        return ToToolResult(await CreateBridgeClient(bridgeClient, manifestDirectory).CloseSessionAsync(
            parsedSessionId!,
            cancellationToken,
            terminateLaunchedProcess));
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
        McpDiagnosticsMode mode = McpDiagnosticsMode.All,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(bridgeClient);
        ArgumentNullException.ThrowIfNull(previewHostClient);
        previewSessionStore ??= PreviewSessionStore.CreateDefault();

        if (!TryParseOptionalSessionId(sessionId, out var parsedSessionId, out var error))
        {
            return ToolResult<DiagnosticsResponse>.Fail(error!);
        }

        var diagnosticsMode = mode.ToProtocolName();

        var client = CreateBridgeClient(bridgeClient, manifestDirectory);
        var previewHost = previewHostClient.GetDiagnostics();
        var componentOrigins = CreateMcpDiagnosticComponentOrigins(previewHost);
        var result = await client.DiagnosticsAsync(
            processId,
            parsedSessionId,
            maxSessions,
            previewHost,
            previewSessionStore.GetDiagnostics(),
            cancellationToken,
            processName,
            manifestPath,
            componentOrigins);
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
        bool errorsOnly = false,
        McpMinimumSeverity minimumSeverity = McpMinimumSeverity.All,
        string? diagnosticsBaselinePath = null,
        IReadOnlyList<string>? diagnosticsBaselineFingerprints = null,
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
                noBuild: noBuild,
                diagnosticOptions: new PreviewDiagnosticOptions(
                    minimumSeverity.ToProtocolName(),
                    errorsOnly,
                    diagnosticsBaselinePath,
                    diagnosticsBaselineFingerprints));
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
        Name = "native_picker",
        Title = "Native picker",
        ReadOnly = false,
        Idempotent = false,
        Destructive = false,
        OpenWorld = false,
        UseStructuredContent = true)]
    [Description("Controls an owned Windows picker, or with topLevelId an X11 GTK3/macOS AppKit picker. Predefined results are separate app-logic coverage consumed only through the explicit host hook. Portal and unsupported selection paths fail without fallback.")]
    public static ToolResult<NativePickerResponse> NativePicker(
        LocalBridgeClient bridgeClient,
        string sessionId,
        McpNativePickerOperation operation,
        string? path = null,
        McpNativePickerResult? predefinedResult = null,
        string? correlationId = null,
        int ttlMs = 30000,
        int timeoutMs = 1000,
        bool redactPath = true,
        string? manifestDirectory = null,
        string? topLevelId = null)
    {
        if (!TryParseRequiredSessionId(sessionId, out var parsedSessionId, out var error))
        {
            return ToolResult<NativePickerResponse>.Fail(error!);
        }

        return ToToolResult(CreateBridgeClient(bridgeClient, manifestDirectory).NativePicker(
            parsedSessionId!,
            operation.ToProtocolName(),
            path,
            predefinedResult?.ToProtocolName(),
            correlationId,
            ttlMs,
            timeoutMs,
            redactPath, topLevelId));
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
        bool errorsOnly = false,
        McpMinimumSeverity minimumSeverity = McpMinimumSeverity.All,
        string? diagnosticsBaselinePath = null,
        IReadOnlyList<string>? diagnosticsBaselineFingerprints = null,
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
                noBuild: noBuild,
                diagnosticOptions: new PreviewDiagnosticOptions(
                    minimumSeverity.ToProtocolName(),
                    errorsOnly,
                    diagnosticsBaselinePath,
                    diagnosticsBaselineFingerprints));
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
        bool errorsOnly = false,
        McpMinimumSeverity minimumSeverity = McpMinimumSeverity.All,
        string? diagnosticsBaselinePath = null,
        IReadOnlyList<string>? diagnosticsBaselineFingerprints = null,
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
                noBuild: noBuild,
                diagnosticOptions: new PreviewDiagnosticOptions(
                    minimumSeverity.ToProtocolName(),
                    errorsOnly,
                    diagnosticsBaselinePath,
                    diagnosticsBaselineFingerprints));
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
        bool errorsOnly = false,
        McpMinimumSeverity minimumSeverity = McpMinimumSeverity.All,
        string? diagnosticsBaselinePath = null,
        IReadOnlyList<string>? diagnosticsBaselineFingerprints = null,
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
                noBuild: noBuild,
                diagnosticOptions: new PreviewDiagnosticOptions(
                    minimumSeverity.ToProtocolName(),
                    errorsOnly,
                    diagnosticsBaselinePath,
                    diagnosticsBaselineFingerprints));
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

    private static ToolResult<T> ToToolResult<T>(CoreResult<T> result) =>
        OperationResultMapper.ToToolResult(result);

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

    private static InputGestureOptions? CreateGestureOptions(
        string? direction,
        double? distancePercentage,
        int? durationMs,
        string? destinationTargetNodeId)
    {
        return direction is null
            && distancePercentage is null
            && durationMs is null
            && destinationTargetNodeId is null
            ? null
            : new InputGestureOptions(direction, distancePercentage, durationMs, destinationTargetNodeId);
    }

    private static LocalBridgeClient CreateBridgeClient(LocalBridgeClient bridgeClient, string? manifestDirectory)
    {
        return string.IsNullOrWhiteSpace(manifestDirectory)
            ? bridgeClient
            : bridgeClient.WithManifestDirectory(manifestDirectory);
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
            response.Summary,
            response.ComponentOrigins);
    }

    private static IReadOnlyList<DiagnosticComponentOrigin> CreateMcpDiagnosticComponentOrigins(
        PreviewHostDiagnostic previewHost)
    {
        return
        [
            DiagnosticOriginBuilder.Create("mcp", GetMcpAssemblyPath(), AppContext.BaseDirectory),
            DiagnosticOriginBuilder.Create(
                "cli",
                Path.Combine(AppContext.BaseDirectory, "avascope.dll"),
                AppContext.BaseDirectory),
            DiagnosticOriginBuilder.Create("previewHost", previewHost.HostAssemblyPath)
        ];
    }

    private static string GetMcpAssemblyPath()
    {
        var mcpAssemblyPath = typeof(AvaScopeMcpTools).Assembly.Location;
        return string.IsNullOrWhiteSpace(mcpAssemblyPath)
            ? Path.Combine(AppContext.BaseDirectory, "AvaScope.Mcp.dll")
            : mcpAssemblyPath;
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
