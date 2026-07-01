using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using AvaScope.Protocol;

namespace AvaScope.Tests.Protocol;

public sealed class ProtocolContractTests
{
    [Fact]
    public void ProtocolVersionSerializesWithStablePropertyNames()
    {
        var json = JsonSerializer.Serialize(AvaScopeProtocol.CurrentVersion);
        var node = JsonNode.Parse(json)!;

        Assert.Equal(1, node["major"]!.GetValue<int>());
        Assert.Equal(0, node["minor"]!.GetValue<int>());
        Assert.Equal("1.0", AvaScopeProtocol.CurrentVersion.ToString());
    }

    [Fact]
    public void SessionIdSerializesAsStringValue()
    {
        var result = ToolResult<SessionId>.Ok(new SessionId("session-1"));
        var json = JsonSerializer.Serialize(result);
        var node = JsonNode.Parse(json)!;

        Assert.True(node["success"]!.GetValue<bool>());
        Assert.Equal("session-1", node["value"]!.GetValue<string>());
        Assert.Null(node["error"]);
    }

    [Fact]
    public void ToolResultFailureSerializesStructuredError()
    {
        var result = ToolResult<HealthResponse>.Fail(new ProtocolError("session_not_found", "Session was not found."));
        var json = JsonSerializer.Serialize(result);
        var node = JsonNode.Parse(json)!;

        Assert.False(node["success"]!.GetValue<bool>());
        Assert.Null(node["value"]);
        Assert.Equal("session_not_found", node["error"]!["code"]!.GetValue<string>());
        Assert.Equal("Session was not found.", node["error"]!["message"]!.GetValue<string>());
    }

    [Fact]
    public void ToolResultFailureSerializesOptionalErrorDetails()
    {
        var result = ToolResult<HealthResponse>.Fail(new ProtocolError(
            "preview_project_build_failed",
            "Build failed.",
            new Dictionary<string, string>
            {
                ["phase"] = "build",
                ["exitCode"] = "1"
            }));
        var json = JsonSerializer.Serialize(result);
        var node = JsonNode.Parse(json)!;

        Assert.False(node["success"]!.GetValue<bool>());
        Assert.Equal("preview_project_build_failed", node["error"]!["code"]!.GetValue<string>());
        Assert.Equal("build", node["error"]!["details"]!["phase"]!.GetValue<string>());
        Assert.Equal("1", node["error"]!["details"]!["exitCode"]!.GetValue<string>());
    }

    [Fact]
    public void HealthResponseUsesCurrentProtocolMetadata()
    {
        var result = ToolResult<HealthResponse>.Ok(HealthResponse.Current());
        var json = JsonSerializer.Serialize(result);
        var node = JsonNode.Parse(json)!;

        Assert.True(node["success"]!.GetValue<bool>());
        Assert.Equal("avascope", node["value"]!["serviceName"]!.GetValue<string>());
        Assert.Equal(1, node["value"]!["protocolVersion"]!["major"]!.GetValue<int>());
        Assert.Equal(0, node["value"]!["protocolVersion"]!["minor"]!.GetValue<int>());
    }

    [Fact]
    public void CapabilitiesResponseSerializesStableDiscoveryShape()
    {
        var response = AvaScopeCapabilityCatalog.Current(new DateTimeOffset(2026, 6, 13, 1, 0, 0, TimeSpan.Zero));
        var result = ToolResult<AvaScopeCapabilitiesResponse>.Ok(response);
        var json = JsonSerializer.Serialize(result);
        var node = JsonNode.Parse(json)!;

        Assert.True(node["success"]!.GetValue<bool>());
        Assert.Equal("avascope", node["value"]!["serviceName"]!.GetValue<string>());
        Assert.Equal(1, node["value"]!["protocolVersion"]!["major"]!.GetValue<int>());
        Assert.Equal("capabilities[].id", node["value"]!["compatibilityPolicy"]!["featureDiscovery"]!.GetValue<string>());
        Assert.Equal("protocol.capability_discovery", node["value"]!["capabilities"]![2]!["id"]!.GetValue<string>());
        Assert.Equal("available", node["value"]!["capabilities"]![2]!["status"]!.GetValue<string>());
        Assert.Equal("capabilities", node["value"]!["capabilities"]![2]!["tools"]![0]!.GetValue<string>());
        Assert.Contains("ignore unknown JSON", node["value"]!["capabilities"]![1]!["description"]!.GetValue<string>(), StringComparison.OrdinalIgnoreCase);
        Assert.Equal("cli", node["value"]!["tools"]![0]!["adapter"]!.GetValue<string>());
        Assert.Equal("capabilities", node["value"]!["tools"]![0]!["name"]!.GetValue<string>());
        Assert.Equal(AvaScopeCapabilityIds.ProtocolCapabilityDiscovery, node["value"]!["tools"]![0]!["capabilityIds"]![0]!.GetValue<string>());
        Assert.Equal(RuntimeMutationCapabilityCatalog.RuntimeMutationContract, node["value"]!["runtimeMutationCapabilities"]![0]!["name"]!.GetValue<string>());
        Assert.Contains(response.Capabilities, capability => capability.Id == AvaScopeCapabilityIds.RuntimeSourceMap);
        Assert.Contains(response.Capabilities, capability => capability.Id == AvaScopeCapabilityIds.RuntimeLayoutExplain);
        Assert.Contains(response.Capabilities, capability => capability.Id == AvaScopeCapabilityIds.RuntimeSemanticWorkflow);
        Assert.Contains(response.Capabilities, capability => capability.Id == AvaScopeCapabilityIds.RuntimeScenarioRunner);
        Assert.Contains(response.Capabilities, capability => capability.Id == AvaScopeCapabilityIds.RuntimePointerDiagnostics);
        Assert.Contains(response.Capabilities, capability => capability.Id == AvaScopeCapabilityIds.RuntimePseudoStateMatrix);
        Assert.Contains(response.Capabilities, capability => capability.Id == AvaScopeCapabilityIds.RuntimeInteractionAnimation);
        Assert.Contains(response.Capabilities, capability => capability.Id == AvaScopeCapabilityIds.PreviewStateVariants);
        Assert.Contains(response.Tools, tool => tool.Adapter == "mcp" && tool.Name == "run_workflow");
        Assert.Contains(response.Tools, tool => tool.Adapter == "mcp" && tool.Name == "run_scenario");
        Assert.Contains(response.Tools, tool => tool.Adapter == "mcp" && tool.Name == "pointer_diagnostics");
        Assert.Contains(response.Tools, tool => tool.Adapter == "mcp" && tool.Name == "pseudo_state_matrix");
        Assert.Contains(response.Tools, tool => tool.Adapter == "mcp" && tool.Name == "record_interaction_animation");
        Assert.Empty(node["value"]!["diagnostics"]!.AsArray());
    }

    [Fact]
    public void RuntimeSourceLayoutBindingAndWorkflowResponsesSerializeStableShapes()
    {
        var completedAt = new DateTimeOffset(2026, 7, 1, 10, 0, 0, TimeSpan.Zero);
        var sessionId = new SessionId("session-source");
        var target = new RuntimeTargetContext(sessionId, "topLevel:main", TreeKinds.Visual, "visual:source");
        var binding = new RuntimeSourceBinding(
            "Text",
            "Title",
            "{Binding Title}",
            "runtime",
            sourcePath: "C:\\apps\\Sample\\Views\\MainView.axaml",
            line: 12,
            converterResourceKey: "TitleConverter",
            dataTypeName: "Sample.MainViewModel");
        var sourceMap = new RuntimeNodeSourceMap(
            "available",
            "avalonia_xaml_source_info",
            "C:\\apps\\Sample\\Views\\MainView.axaml",
            line: 12,
            column: 8,
            xName: "TitleText",
            elementType: "TextBlock",
            elementPath: "TextBlock#TitleText",
            propertyOrigins:
            [
                new RuntimeSourcePropertyOrigin(
                    "Foreground",
                    "#ff336699",
                    "Avalonia.Media.IBrush",
                    "style",
                    "Style",
                    resourceKey: "AccentBrush",
                    styleSelector: "TextBlock.title",
                    sourcePath: "C:\\apps\\Sample\\App.axaml",
                    line: 25)
            ],
            bindings: [binding]);
        var layoutExplanation = new RuntimeLayoutExplanation(
            "warning",
            "The node was arranged to zero width or height.",
            new RuntimeLayoutMetrics(
                "available",
                "visual:source",
                "Avalonia.Controls.TextBlock",
                new NodeBounds(0, 0, 0, 24),
                new RuntimeSize(80, 24),
                new RuntimeSize(0, 24),
                target),
            "inferred_from_parent_bounds",
            new RuntimeSize(0, 24),
            reasons:
            [
                new RuntimeLayoutReason(
                    "arranged_zero_size",
                    "The node was arranged to zero width or height.",
                    "warning",
                    "visual:source",
                    "Avalonia.Controls.TextBlock",
                    new Dictionary<string, string> { ["boundsWidth"] = "0" })
            ]);
        var bindingState = new RuntimeBindingState(
            "available",
            "Sample.MainViewModel",
            "available",
            [
                new RuntimeBoundProperty(
                    "Text",
                    "Title",
                    "Hello",
                    "System.String",
                    "binding",
                    "active",
                    "{Binding Title}",
                    "Avalonia.Data.BindingExpression",
                    "available",
                    "declared",
                    "not_available",
                    "not_null",
                    "runtime",
                    binding)
            ],
            sourceMap: sourceMap);
        var inspect = new InspectNodeResponse(
            sessionId,
            "topLevel:main",
            TreeKinds.Visual,
            "visual:source",
            "Avalonia.Controls.TextBlock",
            0,
            "TitleText",
            "title-text",
            "Hello",
            new NodeBounds(0, 0, 0, 24),
            computedProperties: [new ComputedPropertyValue("Text", "Hello", "System.String", "LocalValue", "local")],
            target: target,
            bindingState: bindingState,
            sourceMap: sourceMap,
            layoutExplanation: layoutExplanation);
        var treeNode = new TreeNodeSummary(
            "visual:source",
            "Avalonia.Controls.TextBlock",
            "TitleText",
            "title-text",
            "Hello",
            sourceMap: sourceMap,
            target: target);
        var workflow = new SemanticWorkflowResponse(
            "workflow-1",
            sessionId,
            "topLevel:main",
            "passed",
            completedAt,
            completedAt,
            [
                new SemanticWorkflowStepResult(
                    "assert-title",
                    SemanticWorkflowActions.AssertState,
                    "passed",
                    "Assertion passed.",
                    completedAt,
                    target,
                    inspection: inspect)
            ],
            isolatedStateStatus: "configured",
            metadata: new Dictionary<string, string> { ["stepCount"] = "1" });

        var inspectNode = JsonNode.Parse(JsonSerializer.Serialize(inspect))!;
        var layoutNode = JsonNode.Parse(JsonSerializer.Serialize(new LayoutExplainResponse(sessionId, "topLevel:main", TreeKinds.Visual, "visual:source", layoutExplanation, target)))!;
        var treeNodeJson = JsonNode.Parse(JsonSerializer.Serialize(treeNode))!;
        var workflowNode = JsonNode.Parse(JsonSerializer.Serialize(workflow))!;

        Assert.Equal("TitleText", inspectNode["sourceMap"]!["xName"]!.GetValue<string>());
        Assert.Equal("Title", inspectNode["sourceMap"]!["bindings"]![0]!["bindingPath"]!.GetValue<string>());
        Assert.Equal("available", inspectNode["bindingState"]!["boundProperties"]![0]!["resolvedValueStatus"]!.GetValue<string>());
        Assert.Equal("arranged_zero_size", inspectNode["layoutExplanation"]!["reasons"]![0]!["code"]!.GetValue<string>());
        Assert.Equal("visual:source", layoutNode["target"]!["nodeId"]!.GetValue<string>());
        Assert.Equal("TitleText", treeNodeJson["sourceMap"]!["xName"]!.GetValue<string>());
        Assert.Equal("workflow-1", workflowNode["requestId"]!.GetValue<string>());
        Assert.Equal(SemanticWorkflowActions.AssertState, workflowNode["steps"]![0]!["action"]!.GetValue<string>());
    }

    [Fact]
    public void RuntimeScenarioRequestAndResponseSerializeStableShapes()
    {
        var at = new DateTimeOffset(2026, 7, 1, 12, 0, 0, TimeSpan.Zero);
        var sessionId = new SessionId("session-scenario");
        var step = new SemanticWorkflowStep(
            SemanticWorkflowActions.Click,
            "click-delete",
            new SemanticWorkflowSelector(automationId: "delete-button"));
        var request = new RuntimeScenarioRequest(
            [step],
            requestId: "scenario-1",
            launch: new RuntimeScenarioLaunchOptions(
                "dotnet",
                arguments: "Sample.dll",
                workingDirectory: "C:\\apps\\Sample",
                displayName: "Sample",
                manifestDirectory: "C:\\state\\manifests",
                outputDirectory: "C:\\state\\launch",
                environment: new Dictionary<string, string> { ["APP_ENV"] = "test" },
                timeoutMs: 2500),
            topLevelId: "topLevel:main",
            outputDirectory: "C:\\state\\artifacts",
            captureAfterEachStep: true,
            isolatedStateDirectory: "C:\\state\\isolated",
            timelinePath: "C:\\state\\timeline.md");
        var workflow = new SemanticWorkflowResponse(
            "scenario-1",
            sessionId,
            "topLevel:main",
            "passed",
            at,
            at,
            [
                new SemanticWorkflowStepResult(
                    "click-delete",
                    SemanticWorkflowActions.Click,
                    "passed",
                    "Clicked.",
                    at,
                    new RuntimeTargetContext(sessionId, "topLevel:main", TreeKinds.Visual, "visual:delete"))
            ],
            isolatedStateStatus: "applied_environment");
        var response = new RuntimeScenarioResponse(
            "scenario-1",
            "passed",
            at,
            at,
            sessionId,
            "topLevel:main",
            workflow: workflow,
            isolatedStateStatus: "applied_environment",
            isolatedStateDirectory: "C:\\state\\isolated",
            timelinePath: "C:\\state\\timeline.md",
            metadata: new Dictionary<string, string> { ["scenarioMode"] = "launch" });

        var requestNode = JsonNode.Parse(JsonSerializer.Serialize(request))!;
        var responseNode = JsonNode.Parse(JsonSerializer.Serialize(response))!;

        Assert.Equal("scenario-1", requestNode["requestId"]!.GetValue<string>());
        Assert.Equal("dotnet", requestNode["launch"]!["command"]!.GetValue<string>());
        Assert.Equal("Sample.dll", requestNode["launch"]!["arguments"]!.GetValue<string>());
        Assert.Equal("test", requestNode["launch"]!["environment"]!["APP_ENV"]!.GetValue<string>());
        Assert.Equal("delete-button", requestNode["steps"]![0]!["selector"]!["automationId"]!.GetValue<string>());
        Assert.True(requestNode["captureAfterEachStep"]!.GetValue<bool>());
        Assert.Equal("passed", responseNode["status"]!.GetValue<string>());
        Assert.Equal("session-scenario", responseNode["sessionId"]!.GetValue<string>());
        Assert.Equal("applied_environment", responseNode["isolatedStateStatus"]!.GetValue<string>());
        Assert.Equal("launch", responseNode["metadata"]!["scenarioMode"]!.GetValue<string>());
        Assert.Equal("click-delete", responseNode["workflow"]!["steps"]![0]!["stepId"]!.GetValue<string>());
    }

    [Fact]
    public void RuntimePointerDiagnosticsRequestAndResponseSerializeStableShapes()
    {
        var at = new DateTimeOffset(2026, 7, 1, 13, 0, 0, TimeSpan.Zero);
        var sessionId = new SessionId("session-pointer");
        var request = new RuntimePointerDiagnosticsRequest(
            sessionId,
            "topLevel:main",
            [
                new RuntimePointerPathStep(RuntimePointerPathActions.Move, "move-parent", x: 10, y: 12),
                new RuntimePointerPathStep(RuntimePointerPathActions.AssertHit, "assert-popup", expectedLayerKind: "popup")
            ],
            requestId: "pointer-1",
            outputDirectory: "C:\\state\\pointer",
            captureScreenshots: true,
            parentHoverNodeId: "visual:hover");
        var hitNode = new RuntimePointerHitNode(
            "visual:popupItem",
            "Avalonia.Controls.Button",
            "PopupItem",
            "popup-item",
            "Open",
            new NodeBounds(4, 4, 80, 24),
            true,
            0);
        var activeLayer = new RuntimePointerLayerSnapshot(
            "topLevel:popup",
            "topLevel",
            "popup",
            isPrimary: false,
            hitTestPath: [hitNode],
            nearestNode: hitNode);
        var transition = new RuntimePointerTransitionDiagnostic(
            "warning",
            "pointer_parent_hover_exited_into_popup_layer",
            "Pointer moved into popup.",
            "bounds_snapshot_inference",
            fromTopLevelId: "topLevel:main",
            fromNodeId: "visual:hover",
            toTopLevelId: "topLevel:popup",
            toNodeId: "visual:popupItem",
            parentHoverRegionExited: true);
        var response = new RuntimePointerDiagnosticsResponse(
            "pointer-1",
            sessionId,
            "topLevel:main",
            "passed",
            at,
            at,
            [
                new RuntimePointerStepResult(
                    "move-popup",
                    RuntimePointerPathActions.Move,
                    "passed",
                    "Moved.",
                    at,
                    new RuntimePointerLocation(10, 12),
                    screenshot: new ScreenshotResponse(
                        sessionId,
                        "topLevel:popup",
                        "C:\\state\\pointer\\move-popup.png",
                        120,
                        80,
                        at),
                    pointerOverlayPath: "C:\\state\\pointer\\move-popup-pointer-overlay.png",
                    activeLayer: activeLayer,
                    transitions: [transition],
                    metadata: new Dictionary<string, string> { ["transitionProvenance"] = "bounds_snapshot_inference" })
            ]);

        var requestNode = JsonNode.Parse(JsonSerializer.Serialize(request))!;
        var responseNode = JsonNode.Parse(JsonSerializer.Serialize(response))!;

        Assert.Equal("pointer-1", requestNode["requestId"]!.GetValue<string>());
        Assert.Equal("move", requestNode["steps"]![0]!["action"]!.GetValue<string>());
        Assert.Equal(10, requestNode["steps"]![0]!["x"]!.GetValue<double>());
        Assert.Equal("visual:hover", requestNode["parentHoverNodeId"]!.GetValue<string>());
        Assert.Equal("passed", responseNode["status"]!.GetValue<string>());
        Assert.Equal("popup", responseNode["steps"]![0]!["activeLayer"]!["layerKind"]!.GetValue<string>());
        Assert.Equal("visual:popupItem", responseNode["steps"]![0]!["activeLayer"]!["hitTestPath"]![0]!["nodeId"]!.GetValue<string>());
        Assert.True(responseNode["steps"]![0]!["transitions"]![0]!["parentHoverRegionExited"]!.GetValue<bool>());
        Assert.Equal("bounds_snapshot_inference", responseNode["steps"]![0]!["metadata"]!["transitionProvenance"]!.GetValue<string>());
        Assert.Equal("pointer_overlay", responseNode["agentReview"]!["artifactPaths"]![1]!["kind"]!.GetValue<string>());
    }

    [Fact]
    public void RuntimePseudoStateMatrixRequestAndResponseSerializeStableShapes()
    {
        var at = new DateTimeOffset(2026, 7, 1, 14, 0, 0, TimeSpan.Zero);
        var sessionId = new SessionId("session-matrix");
        var target = new RuntimeTargetContext(sessionId, "topLevel:main", TreeKinds.Visual, "visual:item");
        var request = new RuntimePseudoStateMatrixRequest(
            sessionId,
            "topLevel:main",
            target,
            [RuntimePseudoStates.Normal, RuntimePseudoStates.Disabled, RuntimePseudoStates.SelectedPointerOver],
            requestId: "matrix-1",
            outputDirectory: "C:\\state\\matrix",
            contactSheetPath: "C:\\state\\matrix\\sheet.png",
            automationId: "state-target");
        var mutation = new RuntimeMutationResponse(
            "matrix-1:disabled:IsEnabled:false",
            "mutation:session-matrix:1",
            sessionId,
            "topLevel:main",
            target,
            new RuntimeMutationOperation(
                RuntimeMutationOperationKinds.SetProperty,
                propertyName: "IsEnabled",
                value: "false",
                valueType: "bool"),
            RuntimeMutationStatuses.Applied,
            applied: true,
            at);
        var response = new RuntimePseudoStateMatrixResponse(
            "matrix-1",
            sessionId,
            "topLevel:main",
            target,
            "passed",
            at,
            at,
            [
                new RuntimePseudoStateMatrixEntry(
                    RuntimePseudoStates.Disabled,
                    "disabled",
                    "passed",
                    "Captured.",
                    at,
                    new ScreenshotResponse(
                        sessionId,
                        "topLevel:main",
                        "C:\\state\\matrix\\disabled.png",
                        320,
                        180,
                        at),
                    new RuntimePseudoStateTargetSummary(
                        "visual:item",
                        "Avalonia.Controls.ListBoxItem",
                        "Item",
                        "state-target",
                        "Selected item",
                        new NodeBounds(10, 20, 100, 32),
                        ["selected"],
                        new RuntimeAccessibilityState("State target", null, null, null, isEnabled: false)),
                    appliedMutations: [mutation],
                    resetMutations: [mutation],
                    metadata: new Dictionary<string, string> { ["diffStatus"] = "changed" })
            ],
            "C:\\state\\matrix\\sheet.png");

        var requestNode = JsonNode.Parse(JsonSerializer.Serialize(request))!;
        var responseNode = JsonNode.Parse(JsonSerializer.Serialize(response))!;

        Assert.Equal("matrix-1", requestNode["requestId"]!.GetValue<string>());
        Assert.Equal("disabled", requestNode["states"]![1]!.GetValue<string>());
        Assert.Equal("C:\\state\\matrix\\sheet.png", requestNode["contactSheetPath"]!.GetValue<string>());
        Assert.Equal("state-target", requestNode["automationId"]!.GetValue<string>());
        Assert.Equal("passed", responseNode["status"]!.GetValue<string>());
        Assert.Equal("visual:item", responseNode["entries"]![0]!["target"]!["nodeId"]!.GetValue<string>());
        Assert.Equal("IsEnabled", responseNode["entries"]![0]!["appliedMutations"]![0]!["operation"]!["propertyName"]!.GetValue<string>());
        Assert.Equal("contact_sheet", responseNode["agentReview"]!["artifactPaths"]![1]!["kind"]!.GetValue<string>());
    }

    [Fact]
    public void RuntimeInteractionAnimationRequestAndResponseSerializeStableShapes()
    {
        var at = new DateTimeOffset(2026, 7, 1, 15, 0, 0, TimeSpan.Zero);
        var sessionId = new SessionId("session-animation");
        var request = new RuntimeInteractionAnimationRequest(
            sessionId,
            "topLevel:main",
            [
                new RuntimeInteractionAnimationStep(
                    InputActions.Click,
                    "expand-panel",
                    x: 12,
                    y: 8,
                    frameOffsetsMs: [0, 120, 240])
            ],
            requestId: "interaction-1",
            outputDirectory: "C:\\state\\interaction",
            frameStripPath: "C:\\state\\interaction\\strip.png",
            assertions:
            [
                new RuntimeInteractionGeometryAssertion(
                    "visual:panel",
                    RuntimeInteractionGeometryMetrics.Width,
                    RuntimeInteractionGeometryAssertionModes.Stable,
                    "panel-width",
                    stepId: "expand-panel",
                    tolerance: 1)
            ]);
        var screenshot = new ScreenshotResponse(
            sessionId,
            "topLevel:main",
            "C:\\state\\interaction\\expand-panel-000ms.png",
            320,
            180,
            at);
        var sample = new RuntimeInteractionGeometrySample(
            "expand-panel",
            "expand-panel-00-0ms",
            0,
            120,
            new NodeBounds(10, 20, 120, 40),
            new NodeBounds(0, 0, 320, 180),
            IsClippedByParent: false);
        var response = new RuntimeInteractionAnimationResponse(
            "interaction-1",
            sessionId,
            "topLevel:main",
            "passed",
            at,
            at,
            [
                new RuntimeInteractionAnimationStepResult(
                    "expand-panel",
                    InputActions.Click,
                    "passed",
                    "Captured.",
                    at,
                    new InputResponse(sessionId, "topLevel:main", InputActions.Click, handled: true, at, "visual:button"),
                    [
                        new RuntimeInteractionAnimationFrame(
                            "expand-panel",
                            "expand-panel-00-0ms",
                            0,
                            0,
                            at,
                            screenshot,
                            "C:\\state\\interaction\\expand-panel-000ms-geometry.png",
                            [
                                new RuntimeInteractionGeometrySnapshot(
                                    "visual:panel",
                                    "Avalonia.Controls.Border",
                                    "AnimatedPanel",
                                    "animated-panel",
                                    null,
                                    new NodeBounds(10, 20, 120, 40),
                                    "visual:root",
                                    new NodeBounds(0, 0, 320, 180),
                                    IsClippedByParent: false)
                            ])
                    ])
            ],
            [
                new RuntimeInteractionGeometryAssertionResult(
                    "panel-width",
                    "visual:panel",
                    RuntimeInteractionGeometryMetrics.Width,
                    RuntimeInteractionGeometryAssertionModes.Stable,
                    "passed",
                    "Stable.",
                    tolerance: 1,
                    stepId: "expand-panel",
                    samples: [sample])
            ],
            "C:\\state\\interaction\\strip.png");

        var requestNode = JsonNode.Parse(JsonSerializer.Serialize(request))!;
        var responseNode = JsonNode.Parse(JsonSerializer.Serialize(response))!;

        Assert.Equal("interaction-1", requestNode["requestId"]!.GetValue<string>());
        Assert.Equal("click", requestNode["steps"]![0]!["action"]!.GetValue<string>());
        Assert.Equal(120, requestNode["steps"]![0]!["frameOffsetsMs"]![1]!.GetValue<int>());
        Assert.Equal("panel-width", requestNode["assertions"]![0]!["assertionId"]!.GetValue<string>());
        Assert.Equal("passed", responseNode["status"]!.GetValue<string>());
        Assert.Equal("expand-panel", responseNode["steps"]![0]!["frames"]![0]!["stepId"]!.GetValue<string>());
        Assert.Equal("visual:panel", responseNode["steps"]![0]!["frames"]![0]!["geometry"]![0]!["nodeId"]!.GetValue<string>());
        Assert.Equal(120, responseNode["assertions"]![0]!["samples"]![0]!["value"]!.GetValue<double>());
        Assert.Equal("frame_strip", responseNode["agentReview"]!["artifactPaths"]![2]!["kind"]!.GetValue<string>());
    }

    [Fact]
    public void DiagnosticsResponseSerializesBridgeSessionsAndIssues()
    {
        var generatedAt = new DateTimeOffset(2026, 6, 7, 3, 0, 0, TimeSpan.Zero);
        var createdAt = generatedAt.AddMinutes(-10);
        var manifestPath = Path.Combine(Path.GetTempPath(), "AvaScope.Tests", "session-1.json");
        var response = new DiagnosticsResponse(
            HealthResponse.Current(),
            generatedAt,
            Path.GetDirectoryName(manifestPath)!,
            [
                new BridgeSessionDiagnostic(
                    DiagnosticStatuses.Available,
                    manifestPath,
                    new SessionSummary(
                        new SessionId("session-1"),
                        SessionKinds.Runtime,
                        SessionStates.Active,
                        createdAt,
                        "Sample app"),
                    1234,
                    DiagnosticTransportKinds.NamedPipe,
                    "avascope-1234-session-1",
                    HealthResponse.Current())
            ],
            [
                new ProtocolError("bridge_session_not_found", "No bridge session matched.")
            ],
            new PreviewHostDiagnostic(
                DiagnosticStatuses.Available,
                "C:\\avascope\\AvaScope.PreviewHost.dll",
                DiagnosticProcessModes.IsolatedChildProcess,
                HealthResponse.Current()),
            diagnosticIssues:
            [
                new DiagnosticIssue(
                    DiagnosticIssueSources.Diagnostics,
                    DiagnosticIssueSeverities.Warning,
                    DiagnosticStatuses.Unavailable,
                    "bridge_session_not_found",
                    "No bridge session matched.",
                    "diagnostics_summary",
                    generatedAt)
            ]);

        var json = JsonSerializer.Serialize(response);
        var node = JsonNode.Parse(json)!;

        Assert.Equal("avascope", node["service"]!["serviceName"]!.GetValue<string>());
        Assert.Equal(1, node["service"]!["protocolVersion"]!["major"]!.GetValue<int>());
        Assert.Equal(Path.GetFullPath(Path.GetDirectoryName(manifestPath)!), node["manifestDirectory"]!.GetValue<string>());
        Assert.Equal(generatedAt, DateTimeOffset.Parse(node["generatedAt"]!.GetValue<string>(), CultureInfo.InvariantCulture));
        Assert.Equal(DiagnosticStatuses.Available, node["bridgeSessions"]![0]!["status"]!.GetValue<string>());
        Assert.Equal(Path.GetFullPath(manifestPath), node["bridgeSessions"]![0]!["manifestPath"]!.GetValue<string>());
        Assert.Equal("session-1", node["bridgeSessions"]![0]!["session"]!["sessionId"]!.GetValue<string>());
        Assert.Equal(1234, node["bridgeSessions"]![0]!["processId"]!.GetValue<int>());
        Assert.Equal(DiagnosticTransportKinds.NamedPipe, node["bridgeSessions"]![0]!["transport"]!.GetValue<string>());
        Assert.Equal("avascope-1234-session-1", node["bridgeSessions"]![0]!["pipeName"]!.GetValue<string>());
        Assert.Equal("avascope", node["bridgeSessions"]![0]!["health"]!["serviceName"]!.GetValue<string>());
        Assert.Equal(DiagnosticStatuses.Available, node["previewHost"]!["status"]!.GetValue<string>());
        Assert.Equal("C:\\avascope\\AvaScope.PreviewHost.dll", node["previewHost"]!["hostAssemblyPath"]!.GetValue<string>());
        Assert.Equal(DiagnosticProcessModes.IsolatedChildProcess, node["previewHost"]!["processMode"]!.GetValue<string>());
        Assert.Equal("avascope", node["previewHost"]!["service"]!["serviceName"]!.GetValue<string>());
        Assert.Equal("bridge_session_not_found", node["issues"]![0]!["code"]!.GetValue<string>());
        Assert.Equal(DiagnosticIssueSources.Diagnostics, node["diagnosticIssues"]![0]!["source"]!.GetValue<string>());
        Assert.Equal(DiagnosticIssueSeverities.Warning, node["diagnosticIssues"]![0]!["severity"]!.GetValue<string>());
        Assert.Equal(DiagnosticStatuses.Unavailable, node["diagnosticIssues"]![0]!["status"]!.GetValue<string>());
        Assert.Equal("diagnostics_summary", node["diagnosticIssues"]![0]!["provenance"]!.GetValue<string>());
    }

    [Fact]
    public void DoctorResponseSerializesStableReadinessShape()
    {
        var generatedAt = new DateTimeOffset(2026, 6, 9, 9, 0, 0, TimeSpan.Zero);
        var response = new DoctorResponse(
            HealthResponse.Current(),
            generatedAt,
            DiagnosticStatuses.Available,
            "C:\\avascope\\avascope.dll",
            "C:\\avascope",
            "C:\\avascope\\sessions",
            "C:\\avascope\\preview-sessions",
            [
                new DoctorCheck(
                    "mcp_assembly",
                    DiagnosticStatuses.Available,
                    "MCP server assembly is available.",
                    "C:\\avascope\\AvaScope.Mcp.dll")
            ],
            [],
            new PreviewHostDiagnostic(
                DiagnosticStatuses.Available,
                "C:\\avascope\\AvaScope.PreviewHost.dll",
                DiagnosticProcessModes.IsolatedChildProcess,
                HealthResponse.Current()));

        var json = JsonSerializer.Serialize(response);
        var node = JsonNode.Parse(json)!;

        Assert.Equal("avascope", node["service"]!["serviceName"]!.GetValue<string>());
        Assert.Equal(generatedAt, DateTimeOffset.Parse(node["generatedAt"]!.GetValue<string>(), CultureInfo.InvariantCulture));
        Assert.Equal(DiagnosticStatuses.Available, node["status"]!.GetValue<string>());
        Assert.Equal("C:\\avascope\\avascope.dll", node["cliAssemblyPath"]!.GetValue<string>());
        Assert.Equal("C:\\avascope", node["baseDirectory"]!.GetValue<string>());
        Assert.Equal("C:\\avascope\\sessions", node["manifestDirectory"]!.GetValue<string>());
        Assert.Equal("C:\\avascope\\preview-sessions", node["previewSessionStoreDirectory"]!.GetValue<string>());
        Assert.Equal("mcp_assembly", node["checks"]![0]!["name"]!.GetValue<string>());
        Assert.Equal(DiagnosticStatuses.Available, node["checks"]![0]!["status"]!.GetValue<string>());
        Assert.Equal("C:\\avascope\\AvaScope.Mcp.dll", node["checks"]![0]!["path"]!.GetValue<string>());
        Assert.Equal(DiagnosticStatuses.Available, node["previewHost"]!["status"]!.GetValue<string>());
        Assert.Empty(node["issues"]!.AsArray());
    }

    [Fact]
    public void ListSessionsResponseSerializesBoundedSummaryShape()
    {
        var createdAt = new DateTimeOffset(2026, 6, 6, 18, 30, 0, TimeSpan.Zero);
        var response = new ListSessionsResponse(
        [
            new SessionSummary(
                new SessionId("session-1"),
                SessionKinds.Runtime,
                SessionStates.Active,
                createdAt,
                "Sample app")
        ]);

        var json = JsonSerializer.Serialize(response);
        var node = JsonNode.Parse(json)!;

        Assert.Equal("session-1", node["sessions"]![0]!["sessionId"]!.GetValue<string>());
        Assert.Equal("runtime", node["sessions"]![0]!["kind"]!.GetValue<string>());
        Assert.Equal("active", node["sessions"]![0]!["state"]!.GetValue<string>());
        Assert.Equal("Sample app", node["sessions"]![0]!["displayName"]!.GetValue<string>());
        var createdAtText = node["sessions"]![0]!["createdAt"]!.GetValue<string>();
        var parsedCreatedAt = DateTimeOffset.Parse(createdAtText, CultureInfo.InvariantCulture);

        Assert.Equal(createdAt, parsedCreatedAt);
    }

    [Fact]
    public void ToolResultRoundTripsThroughJson()
    {
        var original = ToolResult<ListSessionsResponse>.Ok(new ListSessionsResponse());
        var json = JsonSerializer.Serialize(original);
        var deserialized = JsonSerializer.Deserialize<ToolResult<ListSessionsResponse>>(json);

        Assert.NotNull(deserialized);
        Assert.True(deserialized.Success);
        Assert.NotNull(deserialized.Value);
        Assert.Empty(deserialized.Value.Sessions);
        Assert.Null(deserialized.Error);
    }

    [Fact]
    public void ScreenshotResponseSerializesStableOutputShape()
    {
        var capturedAt = new DateTimeOffset(2026, 6, 6, 21, 0, 0, TimeSpan.Zero);
        var response = new ScreenshotResponse(
            new SessionId("session-1"),
            "topLevel:abc",
            "C:\\screenshots\\capture.png",
            320,
            200,
            capturedAt);

        var json = JsonSerializer.Serialize(response);
        var node = JsonNode.Parse(json)!;

        Assert.Equal("session-1", node["sessionId"]!.GetValue<string>());
        Assert.Equal("topLevel:abc", node["topLevelId"]!.GetValue<string>());
        Assert.Equal("C:\\screenshots\\capture.png", node["filePath"]!.GetValue<string>());
        Assert.Equal(320, node["pixelWidth"]!.GetValue<int>());
        Assert.Equal(200, node["pixelHeight"]!.GetValue<int>());
        Assert.Equal(capturedAt, DateTimeOffset.Parse(node["capturedAt"]!.GetValue<string>(), CultureInfo.InvariantCulture));
        Assert.Equal("session-1", node["target"]!["sessionId"]!.GetValue<string>());
        Assert.Equal("topLevel:abc", node["target"]!["topLevelId"]!.GetValue<string>());
        Assert.Null(node["target"]!["treeKind"]);
        Assert.Null(node["target"]!["nodeId"]);
    }

    [Fact]
    public void BridgeIpcRequestSerializesStableAttachShape()
    {
        var request = new BridgeIpcRequest(
            "request-1",
            BridgeIpcMethods.Screenshot,
            "topLevel:abc",
            "C:\\screenshots\\capture.png");

        var json = JsonSerializer.Serialize(request);
        var node = JsonNode.Parse(json)!;

        Assert.Equal("request-1", node["requestId"]!.GetValue<string>());
        Assert.Equal("screenshot", node["method"]!.GetValue<string>());
        Assert.Equal("topLevel:abc", node["topLevelId"]!.GetValue<string>());
        Assert.Equal("C:\\screenshots\\capture.png", node["outputPath"]!.GetValue<string>());
    }

    [Fact]
    public void RuntimeMutationRequestAndResponseSerializeStableShapes()
    {
        var evaluatedAt = new DateTimeOffset(2026, 6, 12, 12, 0, 0, TimeSpan.Zero);
        var target = new RuntimeTargetContext(
            new SessionId("session-1"),
            "topLevel:abc",
            TreeKinds.Visual,
            "visual:button",
            evaluatedAt.AddSeconds(-1),
            nodeGeneration: "node-generation-1");
        var operation = new RuntimeMutationOperation(
            RuntimeMutationOperationKinds.SetProperty,
            propertyName: "Width",
            value: "240",
            valueType: "double");
        var mutationRequest = new RuntimeMutationRequest(
            "mutation-request-1",
            target,
            operation,
            [
                RuntimeMutationCapabilityCatalog.RuntimeMutationContract,
                RuntimeMutationCapabilityCatalog.StyleLayoutMutation
            ],
            new Dictionary<string, string>
            {
                ["source"] = "cli"
            });
        var ipcRequest = new BridgeIpcRequest(
            mutationRequest.RequestId,
            BridgeIpcMethods.MutateNode,
            mutation: mutationRequest);
        var response = new RuntimeMutationResponse(
            mutationRequest.RequestId,
            "mutation:session-1:1",
            target.SessionId,
            target.TopLevelId,
            target,
            operation,
            RuntimeMutationStatuses.Applied,
            applied: true,
            evaluatedAt,
            RuntimeMutationCapabilityCatalog.CurrentBridgeCapabilities(),
            metadata: new Dictionary<string, string>
            {
                ["propertyName"] = "Width",
                ["originalValue"] = "120",
                ["effectiveValue"] = "240",
                ["resetSupported"] = "true"
            });

        var requestNode = JsonNode.Parse(JsonSerializer.Serialize(mutationRequest))!;
        var ipcNode = JsonNode.Parse(JsonSerializer.Serialize(ipcRequest))!;
        var responseNode = JsonNode.Parse(JsonSerializer.Serialize(response))!;

        Assert.Equal("mutation-request-1", requestNode["requestId"]!.GetValue<string>());
        Assert.Equal("session-1", requestNode["target"]!["sessionId"]!.GetValue<string>());
        Assert.Equal("topLevel:abc", requestNode["target"]!["topLevelId"]!.GetValue<string>());
        Assert.Equal(TreeKinds.Visual, requestNode["target"]!["treeKind"]!.GetValue<string>());
        Assert.Equal("visual:button", requestNode["target"]!["nodeId"]!.GetValue<string>());
        Assert.Equal(RuntimeMutationOperationKinds.SetProperty, requestNode["operation"]!["kind"]!.GetValue<string>());
        Assert.Equal("Width", requestNode["operation"]!["propertyName"]!.GetValue<string>());
        Assert.Equal("240", requestNode["operation"]!["value"]!.GetValue<string>());
        Assert.Equal(RuntimeMutationCapabilityCatalog.RuntimeMutationContract, requestNode["requestedCapabilities"]![0]!.GetValue<string>());
        Assert.Equal(RuntimeMutationCapabilityCatalog.StyleLayoutMutation, requestNode["requestedCapabilities"]![1]!.GetValue<string>());
        Assert.Equal("cli", requestNode["metadata"]!["source"]!.GetValue<string>());

        Assert.Equal(BridgeIpcMethods.MutateNode, ipcNode["method"]!.GetValue<string>());
        Assert.Equal("mutation-request-1", ipcNode["mutation"]!["requestId"]!.GetValue<string>());
        Assert.Equal("Width", ipcNode["mutation"]!["operation"]!["propertyName"]!.GetValue<string>());

        Assert.Equal("mutation:session-1:1", responseNode["mutationId"]!.GetValue<string>());
        Assert.Equal(RuntimeMutationStatuses.Applied, responseNode["status"]!.GetValue<string>());
        Assert.True(responseNode["applied"]!.GetValue<bool>());
        Assert.Equal(RuntimeMutationCapabilityCatalog.RuntimeMutationContract, responseNode["capabilities"]![0]!["name"]!.GetValue<string>());
        Assert.True(responseNode["capabilities"]![0]!["available"]!.GetValue<bool>());
        Assert.Equal("local_only", responseNode["capabilities"]![0]!["metadata"]!["transport"]!.GetValue<string>());
        Assert.Equal("true", responseNode["capabilities"]![0]!["metadata"]!["temporary"]!.GetValue<string>());
        Assert.Equal(RuntimeMutationCapabilityCatalog.StyleLayoutMutation, responseNode["capabilities"]![1]!["name"]!.GetValue<string>());
        Assert.True(responseNode["capabilities"]![1]!["available"]!.GetValue<bool>());
        Assert.Equal("width", responseNode["capabilities"]![1]!["supportedProperties"]![0]!.GetValue<string>());
        Assert.Equal("true", responseNode["capabilities"]![1]!["metadata"]!["reversible"]!.GetValue<string>());
        Assert.Equal("reset_mutation,reset_all", responseNode["capabilities"]![1]!["metadata"]!["resetOperations"]!.GetValue<string>());
        Assert.Equal("Width", responseNode["metadata"]!["propertyName"]!.GetValue<string>());
        Assert.Equal("120", responseNode["metadata"]!["originalValue"]!.GetValue<string>());
        Assert.Equal("240", responseNode["metadata"]!["effectiveValue"]!.GetValue<string>());
        Assert.Equal("true", responseNode["metadata"]!["resetSupported"]!.GetValue<string>());
        Assert.Empty(responseNode["diagnostics"]!.AsArray());
        Assert.Equal(RuntimeMutationStatuses.Applied, responseNode["agentReview"]!["status"]!.GetValue<string>());
        Assert.Equal("mutation:session-1:1", responseNode["agentReview"]!["mutations"]![0]!["mutationId"]!.GetValue<string>());
        Assert.True(responseNode["agentReview"]!["mutations"]![0]!["active"]!.GetValue<bool>());
        Assert.Equal(evaluatedAt, DateTimeOffset.Parse(responseNode["evaluatedAt"]!.GetValue<string>(), CultureInfo.InvariantCulture));
    }

    [Fact]
    public void RuntimeMutationEvidenceResponseSerializesStableShape()
    {
        var capturedAt = new DateTimeOffset(2026, 6, 12, 13, 0, 0, TimeSpan.Zero);
        var sessionId = new SessionId("session-1");
        var target = new RuntimeTargetContext(
            sessionId,
            "topLevel:abc",
            TreeKinds.Visual,
            "visual:button");
        var operation = new RuntimeMutationOperation(
            RuntimeMutationOperationKinds.SetProperty,
            propertyName: "Background",
            value: "#0000ff",
            valueType: "brush");
        var mutation = new RuntimeMutationResponse(
            "evidence-request-1",
            "mutation:session-1:1",
            sessionId,
            target.TopLevelId,
            target,
            operation,
            RuntimeMutationStatuses.Applied,
            applied: true,
            capturedAt.AddSeconds(-1),
            RuntimeMutationCapabilityCatalog.CurrentBridgeCapabilities());
        var summary = new RuntimeMutationEvidenceSummary(
            "captured",
            RuntimeMutationStatuses.Applied,
            mutationApplied: true,
            screenshotsCaptured: true,
            visualTreeSnapshotsCaptured: true,
            diffStatus: "changed",
            beforeVisualTreeNodeCount: 4,
            afterVisualTreeNodeCount: 4,
            beforeTargetFound: true,
            afterTargetFound: true,
            changedPixels: 42,
            changedPixelPercentage: 12.5);
        var response = new RuntimeMutationEvidenceResponse(
            "evidence-request-1",
            sessionId,
            target.TopLevelId,
            target,
            mutation,
            summary,
            "C:\\artifacts\\evidence",
            "C:\\artifacts\\evidence\\evidence-request-1-before.png",
            "C:\\artifacts\\evidence\\evidence-request-1-after.png",
            "C:\\artifacts\\evidence\\evidence-request-1-before-visual-tree.json",
            "C:\\artifacts\\evidence\\evidence-request-1-after-visual-tree.json",
            capturedAt,
            "C:\\artifacts\\evidence\\evidence-request-1-diff.png",
            new PreviewDiffResponse(
                "C:\\artifacts\\evidence\\evidence-request-1-before.png",
                "C:\\artifacts\\evidence\\evidence-request-1-after.png",
                passed: false,
                pixelWidth: 20,
                pixelHeight: 10,
                tolerance: 0,
                changedPixels: 42,
                totalPixels: 200,
                changedPercent: 21,
                maxDelta: 255,
                diffPath: "C:\\artifacts\\evidence\\evidence-request-1-diff.png"),
            new RuntimeMutationEvidenceTargetSummary(
                "visual:button",
                "Avalonia.Controls.Button",
                name: "PrimaryButton",
                text: "Before",
                bounds: new NodeBounds(1, 2, 100, 32),
                classes: ["primary"]),
            new RuntimeMutationEvidenceTargetSummary(
                "visual:button",
                "Avalonia.Controls.Button",
                name: "PrimaryButton",
                text: "After",
                bounds: new NodeBounds(1, 2, 100, 32),
                classes: ["primary", "agent-mutated"]),
            [
                new ProtocolError(
                    "diff_notice",
                    "Diff captured.",
                    new Dictionary<string, string>
                    {
                        ["artifact"] = "diff"
                    })
            ],
            new RuntimeMutationReviewArtifact(
                "C:\\artifacts\\evidence\\evidence-request-1-review.html",
                "file:///C:/artifacts/evidence/evidence-request-1-review.html",
                "html",
                capturedAt.AddSeconds(1)));

        var json = JsonSerializer.Serialize(response);
        var node = JsonNode.Parse(json)!;

        Assert.Equal("evidence-request-1", node["requestId"]!.GetValue<string>());
        Assert.Equal("session-1", node["sessionId"]!.GetValue<string>());
        Assert.Equal("topLevel:abc", node["topLevelId"]!.GetValue<string>());
        Assert.Equal("visual:button", node["target"]!["nodeId"]!.GetValue<string>());
        Assert.Equal("mutation:session-1:1", node["mutation"]!["mutationId"]!.GetValue<string>());
        Assert.Equal("captured", node["summary"]!["status"]!.GetValue<string>());
        Assert.True(node["summary"]!["mutationApplied"]!.GetValue<bool>());
        Assert.True(node["summary"]!["screenshotsCaptured"]!.GetValue<bool>());
        Assert.True(node["summary"]!["visualTreeSnapshotsCaptured"]!.GetValue<bool>());
        Assert.Equal("changed", node["summary"]!["diffStatus"]!.GetValue<string>());
        Assert.Equal(4, node["summary"]!["beforeVisualTreeNodeCount"]!.GetValue<int>());
        Assert.Equal(42, node["summary"]!["changedPixels"]!.GetValue<long>());
        Assert.Equal(12.5, node["summary"]!["changedPixelPercentage"]!.GetValue<double>());
        Assert.Equal("C:\\artifacts\\evidence", node["artifactDirectory"]!.GetValue<string>());
        Assert.EndsWith("evidence-request-1-before.png", node["beforeScreenshotPath"]!.GetValue<string>());
        Assert.EndsWith("evidence-request-1-after.png", node["afterScreenshotPath"]!.GetValue<string>());
        Assert.EndsWith("evidence-request-1-before-visual-tree.json", node["beforeVisualTreePath"]!.GetValue<string>());
        Assert.EndsWith("evidence-request-1-after-visual-tree.json", node["afterVisualTreePath"]!.GetValue<string>());
        Assert.EndsWith("evidence-request-1-diff.png", node["diffPath"]!.GetValue<string>());
        Assert.Equal(42, node["diff"]!["changedPixels"]!.GetValue<long>());
        Assert.Equal("Before", node["beforeTarget"]!["text"]!.GetValue<string>());
        Assert.Equal("After", node["afterTarget"]!["text"]!.GetValue<string>());
        Assert.Equal("agent-mutated", node["afterTarget"]!["classes"]![1]!.GetValue<string>());
        Assert.Equal("diff_notice", node["diagnostics"]![0]!["code"]!.GetValue<string>());
        Assert.EndsWith("evidence-request-1-review.html", node["reviewArtifact"]!["artifactPath"]!.GetValue<string>());
        Assert.Equal("file:///C:/artifacts/evidence/evidence-request-1-review.html", node["reviewArtifact"]!["reviewUrl"]!.GetValue<string>());
        Assert.Equal("html", node["reviewArtifact"]!["format"]!.GetValue<string>());
        Assert.Equal("captured", node["agentReview"]!["status"]!.GetValue<string>());
        Assert.Equal("mutation:session-1:1", node["agentReview"]!["mutations"]![0]!["mutationId"]!.GetValue<string>());
        Assert.Equal("diff_notice", node["agentReview"]!["failures"]![0]!["code"]!.GetValue<string>());
        Assert.Contains("evidence-request-1-review.html", node["agentReview"]!["reviewUrls"]![0]!.GetValue<string>(), StringComparison.Ordinal);
        Assert.Contains("evidence-request-1-diff.png", node["agentReview"]!["artifactPaths"]![5]!["path"]!.GetValue<string>(), StringComparison.Ordinal);
        Assert.Equal(capturedAt, DateTimeOffset.Parse(node["capturedAt"]!.GetValue<string>(), CultureInfo.InvariantCulture));
    }

    [Fact]
    public void RuntimeMutationReviewResponseSerializesStableShape()
    {
        var reviewedAt = new DateTimeOffset(2026, 6, 12, 14, 0, 0, TimeSpan.Zero);
        var sessionId = new SessionId("session-1");
        var target = new RuntimeTargetContext(sessionId, "topLevel:abc", TreeKinds.Visual, "visual:button");
        var operation = new RuntimeMutationOperation(
            RuntimeMutationOperationKinds.SetProperty,
            propertyName: "Width",
            value: "240",
            valueType: "double");
        var entry = new RuntimeMutationReviewEntry(
            1,
            "mutation-request-1",
            "mutation:session-1:1",
            sessionId,
            target.TopLevelId,
            target,
            operation,
            RuntimeMutationStatuses.Applied,
            applied: true,
            active: true,
            reviewedAt.AddSeconds(-2),
            [
                new ProtocolError("notice", "Mutation captured.")
            ],
            new Dictionary<string, string>
            {
                ["propertyName"] = "Width",
                ["originalValue"] = "120",
                ["effectiveValue"] = "240"
            });
        var response = new RuntimeMutationReviewResponse(
            sessionId,
            reviewedAt,
            historyCount: 1,
            activeMutationCount: 1,
            history: [entry],
            activeMutations: [entry],
            resetHandoff: new RuntimeMutationResetHandoff(
                sessionId,
                activeMutationCount: 1,
                activeMutationIds: [entry.MutationId],
                suggestedResetAllTarget: target,
                nextAction: "Reset active runtime overrides."),
            metadata: new Dictionary<string, string>
            {
                ["scope"] = "local_session",
                ["maxResults"] = "50"
            },
            reviewArtifact: new RuntimeMutationReviewArtifact(
                "C:\\artifacts\\review.html",
                "file:///C:/artifacts/review.html",
                "html",
                reviewedAt.AddSeconds(1)),
            sourceContext: new RuntimeSourceSuggestionContext(
                "C:\\apps\\Sample\\Sample.csproj",
                "C:\\apps\\Sample\\Views\\MainView.axaml",
                "C:\\apps\\Sample\\App.axaml",
                "C:\\apps\\Sample\\avascope.preview.json",
                "contract-test"),
            sourceSuggestions:
            [
                new RuntimeSourceSuggestion(
                    "source-suggestion:mutation:session-1:1:1",
                    entry.MutationId,
                    entry.Sequence,
                    entry.Operation.Kind,
                    entry.Target,
                    "medium",
                    "runtime_mutation_metadata+source_context",
                    "xaml_property_or_style_setter",
                    "provided",
                    "Review the owning XAML property or style setter before applying this manually.",
                    "C:\\apps\\Sample\\Views\\MainView.axaml",
                    "Width",
                    suggestedProperty: "Width",
                    limitations:
                    [
                        "Runtime mutations are temporary local overrides."
                    ],
                    metadata: new Dictionary<string, string>
                    {
                        ["effectiveValue"] = "240"
                    })
            ]);

        var json = JsonSerializer.Serialize(response);
        var node = JsonNode.Parse(json)!;

        Assert.Equal("session-1", node["sessionId"]!.GetValue<string>());
        Assert.Equal(1, node["historyCount"]!.GetValue<int>());
        Assert.Equal(1, node["activeMutationCount"]!.GetValue<int>());
        Assert.Equal("mutation:session-1:1", node["history"]![0]!["mutationId"]!.GetValue<string>());
        Assert.True(node["history"]![0]!["active"]!.GetValue<bool>());
        Assert.Equal("Width", node["activeMutations"]![0]!["operation"]!["propertyName"]!.GetValue<string>());
        Assert.Equal(RuntimeMutationOperationKinds.ResetMutation, node["resetHandoff"]!["resetMutationOperation"]!.GetValue<string>());
        Assert.Equal(RuntimeMutationOperationKinds.ResetAll, node["resetHandoff"]!["resetAllOperation"]!.GetValue<string>());
        Assert.Equal("mutation:session-1:1", node["resetHandoff"]!["activeMutationIds"]![0]!.GetValue<string>());
        Assert.Equal("visual:button", node["resetHandoff"]!["suggestedResetAllTarget"]!["nodeId"]!.GetValue<string>());
        Assert.Equal("local_session", node["metadata"]!["scope"]!.GetValue<string>());
        Assert.EndsWith("review.html", node["reviewArtifact"]!["artifactPath"]!.GetValue<string>());
        Assert.EndsWith("Sample.csproj", node["sourceContext"]!["projectPath"]!.GetValue<string>());
        Assert.EndsWith("MainView.axaml", node["sourceContext"]!["viewPath"]!.GetValue<string>());
        Assert.Equal("contract-test", node["sourceContext"]!["source"]!.GetValue<string>());
        Assert.Equal("source-suggestion:mutation:session-1:1:1", node["sourceSuggestions"]![0]!["suggestionId"]!.GetValue<string>());
        Assert.Equal("medium", node["sourceSuggestions"]![0]!["confidence"]!.GetValue<string>());
        Assert.Equal("runtime_mutation_metadata+source_context", node["sourceSuggestions"]![0]!["provenance"]!.GetValue<string>());
        Assert.Equal("xaml_property_or_style_setter", node["sourceSuggestions"]![0]!["suggestedTargetKind"]!.GetValue<string>());
        Assert.Equal("provided", node["sourceSuggestions"]![0]!["sourceFileStatus"]!.GetValue<string>());
        Assert.Equal("Width", node["sourceSuggestions"]![0]!["suggestedProperty"]!.GetValue<string>());
        Assert.Equal("active_mutations", node["agentReview"]!["status"]!.GetValue<string>());
        Assert.Contains("sourceSuggestions: 1", node["agentReview"]!["summary"]![2]!.GetValue<string>(), StringComparison.Ordinal);
        Assert.Equal("mutation:session-1:1", node["agentReview"]!["mutations"]![0]!["mutationId"]!.GetValue<string>());
        Assert.Equal("notice", node["agentReview"]!["failures"]![0]!["code"]!.GetValue<string>());
        Assert.Equal("file:///C:/artifacts/review.html", node["agentReview"]!["reviewUrls"]![0]!.GetValue<string>());
        Assert.Equal(reviewedAt, DateTimeOffset.Parse(node["reviewedAt"]!.GetValue<string>(), CultureInfo.InvariantCulture));
    }

    [Fact]
    public void BridgeIpcResponseRoundTripsStructuredValue()
    {
        var response = BridgeIpcResponse.Ok("request-1", HealthResponse.Current());

        var json = JsonSerializer.Serialize(response);
        var node = JsonNode.Parse(json)!;
        var roundTripped = JsonSerializer.Deserialize<BridgeIpcResponse>(json);

        Assert.Equal("request-1", node["requestId"]!.GetValue<string>());
        Assert.True(node["success"]!.GetValue<bool>());
        Assert.Equal("avascope", node["value"]!["serviceName"]!.GetValue<string>());

        Assert.NotNull(roundTripped);
        Assert.True(roundTripped.Success);
        Assert.Null(roundTripped.Error);
        Assert.Equal("avascope", roundTripped.GetValue<HealthResponse>()!.ServiceName);
    }

    [Fact]
    public void PreviewViewerResponseSerializesStableShape()
    {
        var createdAt = new DateTimeOffset(2026, 6, 9, 13, 0, 0, TimeSpan.Zero);
        var renderedAt = createdAt.AddSeconds(5);
        var generatedAt = createdAt.AddSeconds(10);
        var session = new PreviewSessionSummary(
            new SessionSummary(
                new SessionId("preview-session-1"),
                SessionKinds.Preview,
                SessionStates.Active,
                createdAt,
                "Main preview"),
            new PreviewRequest(
                "C:\\preview\\main.png",
                width: 320,
                height: 200,
                dpi: 96,
                projectPath: "C:\\app\\App.csproj",
                viewPath: "Views\\MainView.axaml"),
            ToolResult<PreviewResponse>.Ok(new PreviewResponse(
                "C:\\preview\\main.png",
                320,
                200,
                96,
                renderedAt,
                "C:\\app\\App.csproj",
                "Views\\MainView.axaml")),
            renderedAt);
        var response = new PreviewViewerResponse(
            session,
            "C:\\preview\\main.avascope-preview.html",
            "file:///C:/preview/main.avascope-preview.html",
            generatedAt);

        var json = JsonSerializer.Serialize(response);
        var node = JsonNode.Parse(json)!;

        Assert.Equal("preview-session-1", node["session"]!["session"]!["sessionId"]!.GetValue<string>());
        Assert.Equal("C:\\preview\\main.avascope-preview.html", node["viewerPath"]!.GetValue<string>());
        Assert.Equal("file:///C:/preview/main.avascope-preview.html", node["previewUrl"]!.GetValue<string>());
        Assert.Equal("available", node["agentReview"]!["status"]!.GetValue<string>());
        Assert.Equal("file:///C:/preview/main.avascope-preview.html", node["agentReview"]!["previewUrls"]![0]!.GetValue<string>());
        Assert.Equal("html", node["agentReview"]!["reportPaths"]![0]!["kind"]!.GetValue<string>());
        Assert.Equal(generatedAt, DateTimeOffset.Parse(node["generatedAt"]!.GetValue<string>(), CultureInfo.InvariantCulture));
    }

    [Fact]
    public void BridgeSessionManifestSerializesStableAttachMetadata()
    {
        var createdAt = new DateTimeOffset(2026, 6, 6, 22, 0, 0, TimeSpan.Zero);
        var manifest = new BridgeSessionManifest(
            new SessionId("session-1"),
            1234,
            "avascope-1234-session-1",
            createdAt,
            "Sample app");

        var json = JsonSerializer.Serialize(manifest);
        var node = JsonNode.Parse(json)!;

        Assert.Equal("session-1", node["sessionId"]!.GetValue<string>());
        Assert.Equal(1234, node["processId"]!.GetValue<int>());
        Assert.Equal("avascope-1234-session-1", node["pipeName"]!.GetValue<string>());
        Assert.Equal(BridgeTransportScopes.LocalOnly, node["transportScope"]!.GetValue<string>());
        Assert.Equal("Sample app", node["displayName"]!.GetValue<string>());
        Assert.Equal(createdAt, DateTimeOffset.Parse(node["createdAt"]!.GetValue<string>(), CultureInfo.InvariantCulture));
    }

    [Fact]
    public void BridgeSessionManifestDefaultsMissingTransportScopeToLocalOnly()
    {
        var json = """
            {
              "sessionId": "session-1",
              "processId": 1234,
              "pipeName": "avascope-1234-session-1",
              "createdAt": "2026-06-06T22:00:00+00:00",
              "displayName": "Legacy app"
            }
            """;

        var manifest = JsonSerializer.Deserialize<BridgeSessionManifest>(json);

        Assert.NotNull(manifest);
        Assert.Equal(BridgeTransportScopes.LocalOnly, manifest.TransportScope);
    }

    [Fact]
    public void BridgeSessionManifestRejectsUnsupportedTransportScope()
    {
        var exception = Assert.Throws<ArgumentException>(() => new BridgeSessionManifest(
            new SessionId("session-remote"),
            1234,
            "avascope-remote",
            new DateTimeOffset(2026, 6, 13, 2, 0, 0, TimeSpan.Zero),
            transportScope: "remote"));

        Assert.Equal("transportScope", exception.ParamName);
        Assert.Contains("not supported", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TopLevelListResponseSerializesStableShape()
    {
        var response = new ListTopLevelsResponse(
        [
            new TopLevelSummary(
                "topLevel:abc",
                "window",
                "Main",
                1440,
                900,
                1.25,
                true)
        ]);

        var json = JsonSerializer.Serialize(response);
        var node = JsonNode.Parse(json)!;

        Assert.Equal("topLevel:abc", node["topLevels"]![0]!["id"]!.GetValue<string>());
        Assert.Equal("window", node["topLevels"]![0]!["kind"]!.GetValue<string>());
        Assert.Equal("Main", node["topLevels"]![0]!["title"]!.GetValue<string>());
        Assert.Equal(1440, node["topLevels"]![0]!["width"]!.GetValue<double>());
        Assert.Equal(900, node["topLevels"]![0]!["height"]!.GetValue<double>());
        Assert.Equal(1.25, node["topLevels"]![0]!["renderScaling"]!.GetValue<double>());
        Assert.True(node["topLevels"]![0]!["isActive"]!.GetValue<bool>());
    }

    [Fact]
    public void AttachToAppResponseSerializesSessionSummary()
    {
        var createdAt = new DateTimeOffset(2026, 6, 6, 23, 0, 0, TimeSpan.Zero);
        var response = new AttachToAppResponse(
            new SessionSummary(
                new SessionId("session-1"),
                SessionKinds.Runtime,
                SessionStates.Active,
                createdAt,
                "Sample app"),
            1234);

        var json = JsonSerializer.Serialize(response);
        var node = JsonNode.Parse(json)!;

        Assert.Equal(1234, node["processId"]!.GetValue<int>());
        Assert.Equal("session-1", node["session"]!["sessionId"]!.GetValue<string>());
        Assert.Equal("runtime", node["session"]!["kind"]!.GetValue<string>());
        Assert.Equal("active", node["session"]!["state"]!.GetValue<string>());
        Assert.Equal("Sample app", node["session"]!["displayName"]!.GetValue<string>());
        Assert.Equal(createdAt, DateTimeOffset.Parse(node["session"]!["createdAt"]!.GetValue<string>(), CultureInfo.InvariantCulture));
    }

    [Fact]
    public void TreeResponseSerializesBoundedNodeShape()
    {
        var response = new TreeResponse(
            new SessionId("session-1"),
            "topLevel:abc",
            TreeKinds.Visual,
            2,
            new TreeNodeSummary(
                "visual:root",
                "Avalonia.Controls.Window",
                "MainWindow",
                bounds: new NodeBounds(0, 0, 320, 200),
                classes: ["root"],
                children:
                [
                    new TreeNodeSummary(
                        "visual:text",
                        "Avalonia.Controls.TextBlock",
                        name: "TitleText",
                        automationId: "title-text",
                        text: "AvaScope",
                        target: new RuntimeTargetContext(
                            new SessionId("session-1"),
                            "topLevel:abc",
                            TreeKinds.Visual,
                            "visual:text"),
                        accessibilityState: new RuntimeAccessibilityState(
                            "avalonia_public_automation_properties",
                            automationName: "Title",
                            focusable: false,
                            isTabStop: false),
                        validationState: new RuntimeValidationState(
                            "clean",
                            "avalonia_public_data_validation_errors",
                            hasErrors: false,
                            errorCount: 0))
                ]));

        var json = JsonSerializer.Serialize(response);
        var node = JsonNode.Parse(json)!;

        Assert.Equal("session-1", node["sessionId"]!.GetValue<string>());
        Assert.Equal("topLevel:abc", node["topLevelId"]!.GetValue<string>());
        Assert.Equal("visual", node["treeKind"]!.GetValue<string>());
        Assert.Equal(2, node["depthLimit"]!.GetValue<int>());
        Assert.Equal("visual:root", node["root"]!["nodeId"]!.GetValue<string>());
        Assert.Equal("Avalonia.Controls.Window", node["root"]!["nodeType"]!.GetValue<string>());
        Assert.Equal("MainWindow", node["root"]!["name"]!.GetValue<string>());
        Assert.Equal(320, node["root"]!["bounds"]!["width"]!.GetValue<double>());
        Assert.Equal("root", node["root"]!["classes"]![0]!.GetValue<string>());
        Assert.Equal("title-text", node["root"]!["children"]![0]!["automationId"]!.GetValue<string>());
        Assert.Equal("AvaScope", node["root"]!["children"]![0]!["text"]!.GetValue<string>());
        Assert.Equal("Title", node["root"]!["children"]![0]!["accessibilityState"]!["automationName"]!.GetValue<string>());
        Assert.False(node["root"]!["children"]![0]!["accessibilityState"]!["focusable"]!.GetValue<bool>());
        Assert.Equal("clean", node["root"]!["children"]![0]!["validationState"]!["status"]!.GetValue<string>());
        Assert.False(node["root"]!["children"]![0]!["validationState"]!["hasErrors"]!.GetValue<bool>());
        Assert.Equal("session-1", node["target"]!["sessionId"]!.GetValue<string>());
        Assert.Equal("topLevel:abc", node["target"]!["topLevelId"]!.GetValue<string>());
        Assert.Equal("visual", node["target"]!["treeKind"]!.GetValue<string>());
        Assert.Equal("visual:text", node["root"]!["children"]![0]!["target"]!["nodeId"]!.GetValue<string>());
    }

    [Fact]
    public void FindNodesResponseSerializesMatchesAndPaths()
    {
        var response = new FindNodesResponse(
            new SessionId("session-1"),
            "topLevel:abc",
            TreeKinds.Visual,
            4,
            [
                new FindNodeMatch(
                    new TreeNodeSummary(
                        "visual:text",
                        "Avalonia.Controls.TextBlock",
                        name: "TitleText",
                        automationId: "title-text",
                        text: "AvaScope",
                        target: new RuntimeTargetContext(
                            new SessionId("session-1"),
                            "topLevel:abc",
                            TreeKinds.Visual,
                            "visual:text")),
                    ["visual:root", "visual:text"])
            ]);

        var json = JsonSerializer.Serialize(response);
        var node = JsonNode.Parse(json)!;

        Assert.Equal("session-1", node["sessionId"]!.GetValue<string>());
        Assert.Equal("topLevel:abc", node["topLevelId"]!.GetValue<string>());
        Assert.Equal("visual", node["treeKind"]!.GetValue<string>());
        Assert.Equal(4, node["depthLimit"]!.GetValue<int>());
        Assert.Equal("visual:text", node["matches"]![0]!["node"]!["nodeId"]!.GetValue<string>());
        Assert.Equal("TitleText", node["matches"]![0]!["node"]!["name"]!.GetValue<string>());
        Assert.Equal("title-text", node["matches"]![0]!["node"]!["automationId"]!.GetValue<string>());
        Assert.Equal("AvaScope", node["matches"]![0]!["node"]!["text"]!.GetValue<string>());
        Assert.Equal("visual:root", node["matches"]![0]!["path"]![0]!.GetValue<string>());
        Assert.Equal("visual:text", node["matches"]![0]!["path"]![1]!.GetValue<string>());
        Assert.Equal("session-1", node["target"]!["sessionId"]!.GetValue<string>());
        Assert.Equal("visual", node["target"]!["treeKind"]!.GetValue<string>());
        Assert.Equal("visual:text", node["matches"]![0]!["target"]!["nodeId"]!.GetValue<string>());
    }

    [Fact]
    public void InspectNodeResponseSerializesBoundedDetailShape()
    {
        var response = new InspectNodeResponse(
            new SessionId("session-1"),
            "topLevel:abc",
            TreeKinds.Visual,
            "visual:button",
            "Avalonia.Controls.Button",
            childCount: 1,
            name: "SaveButton",
            automationId: "save-button",
            text: "Save",
            bounds: new NodeBounds(10, 20, 80, 30),
            classes: ["primary"]);

        var json = JsonSerializer.Serialize(response);
        var node = JsonNode.Parse(json)!;

        Assert.Equal("session-1", node["sessionId"]!.GetValue<string>());
        Assert.Equal("topLevel:abc", node["topLevelId"]!.GetValue<string>());
        Assert.Equal("visual", node["treeKind"]!.GetValue<string>());
        Assert.Equal("visual:button", node["nodeId"]!.GetValue<string>());
        Assert.Equal("Avalonia.Controls.Button", node["nodeType"]!.GetValue<string>());
        Assert.Equal("SaveButton", node["name"]!.GetValue<string>());
        Assert.Equal("save-button", node["automationId"]!.GetValue<string>());
        Assert.Equal("Save", node["text"]!.GetValue<string>());
        Assert.Equal(80, node["bounds"]!["width"]!.GetValue<double>());
        Assert.Equal("primary", node["classes"]![0]!.GetValue<string>());
        Assert.Equal(1, node["childCount"]!.GetValue<int>());
        Assert.Equal("session-1", node["target"]!["sessionId"]!.GetValue<string>());
        Assert.Equal("topLevel:abc", node["target"]!["topLevelId"]!.GetValue<string>());
        Assert.Equal("visual", node["target"]!["treeKind"]!.GetValue<string>());
        Assert.Equal("visual:button", node["target"]!["nodeId"]!.GetValue<string>());
    }

    [Fact]
    public void InputResponseSerializesStableShape()
    {
        var executedAt = new DateTimeOffset(2026, 6, 7, 0, 0, 0, TimeSpan.Zero);
        var capturedAt = executedAt.AddMilliseconds(-25);
        var response = new InputResponse(
            new SessionId("session-1"),
            "topLevel:abc",
            InputActions.Click,
            handled: true,
            executedAt,
            "visual:button",
            new RuntimeTargetContext(
                new SessionId("session-1"),
                "topLevel:abc",
                TreeKinds.Visual,
                "visual:button",
                capturedAt,
                topLevelGeneration: "top-gen",
                nodeGeneration: "node-gen"),
            pointerButton: "left");

        var json = JsonSerializer.Serialize(response);
        var node = JsonNode.Parse(json)!;

        Assert.Equal("session-1", node["sessionId"]!.GetValue<string>());
        Assert.Equal("topLevel:abc", node["topLevelId"]!.GetValue<string>());
        Assert.Equal("click", node["action"]!.GetValue<string>());
        Assert.True(node["handled"]!.GetValue<bool>());
        Assert.Equal("visual:button", node["targetNodeId"]!.GetValue<string>());
        Assert.Equal(executedAt, DateTimeOffset.Parse(node["executedAt"]!.GetValue<string>(), CultureInfo.InvariantCulture));
        Assert.Equal("session-1", node["target"]!["sessionId"]!.GetValue<string>());
        Assert.Equal("topLevel:abc", node["target"]!["topLevelId"]!.GetValue<string>());
        Assert.Equal("visual", node["target"]!["treeKind"]!.GetValue<string>());
        Assert.Equal("visual:button", node["target"]!["nodeId"]!.GetValue<string>());
        Assert.Equal("node", node["target"]!["targetKind"]!.GetValue<string>());
        Assert.Equal(capturedAt, DateTimeOffset.Parse(node["target"]!["capturedAt"]!.GetValue<string>(), CultureInfo.InvariantCulture));
        Assert.Equal("top-gen", node["target"]!["topLevelGeneration"]!.GetValue<string>());
        Assert.Equal("node-gen", node["target"]!["nodeGeneration"]!.GetValue<string>());
        Assert.Equal("left", node["pointerButton"]!.GetValue<string>());
    }

    [Fact]
    public void InputResponseSerializesKeyMetadataWhenProvided()
    {
        var response = new InputResponse(
            new SessionId("session-1"),
            "topLevel:abc",
            InputActions.KeyDown,
            handled: true,
            DateTimeOffset.UnixEpoch,
            "visual:textbox",
            inputKey: "Enter",
            keyModifiers: "Control, Shift");

        var node = JsonNode.Parse(JsonSerializer.Serialize(response))!;

        Assert.Equal("Enter", node["inputKey"]!.GetValue<string>());
        Assert.Equal("Control, Shift", node["keyModifiers"]!.GetValue<string>());
    }

    [Fact]
    public void InputActionConstantsRemainStable()
    {
        Assert.Equal("pointer_move", InputActions.PointerMove);
        Assert.Equal("pointer_down", InputActions.PointerDown);
        Assert.Equal("pointer_up", InputActions.PointerUp);
        Assert.Equal("click", InputActions.Click);
        Assert.Equal("key_text", InputActions.KeyText);
        Assert.Equal("focus", InputActions.Focus);
        Assert.Equal("key_down", InputActions.KeyDown);
        Assert.Equal("key_up", InputActions.KeyUp);
        Assert.Equal("select", InputActions.Select);
        Assert.Equal("scroll", InputActions.Scroll);
    }

    [Fact]
    public void InspectNodeResponseSerializesRuntimeStateShape()
    {
        var response = new InspectNodeResponse(
            new SessionId("session-1"),
            "topLevel:abc",
            TreeKinds.Visual,
            "visual:scroll",
            "Avalonia.Controls.ScrollViewer",
            1,
            scrollState: new RuntimeScrollState(
                "available",
                new RuntimeVector(10, 20),
                new RuntimeSize(300, 400),
                new RuntimeSize(100, 120),
                new RuntimeVector(200, 280),
                "Auto",
                "Visible"),
            bindingState: new RuntimeBindingState(
                "available",
                "Sample.ViewModel",
                diagnostics:
                [
                    new ProtocolError("runtime_binding_path_metadata_not_available", "not available")
                ]),
            debugState: new RuntimeDebugState(
                "available",
                new Dictionary<string, string>
                {
                    ["visibleRange"] = "10..20"
                },
                "Sample.Control",
                fieldCount: 1,
                maximumFieldCount: 32,
                maximumValueLength: 500));

        var node = JsonNode.Parse(JsonSerializer.Serialize(response))!;

        Assert.Equal("available", node["scrollState"]!["status"]!.GetValue<string>());
        Assert.Equal(10, node["scrollState"]!["offset"]!["x"]!.GetValue<double>());
        Assert.Equal(300, node["scrollState"]!["extent"]!["width"]!.GetValue<double>());
        Assert.Equal("Sample.ViewModel", node["bindingState"]!["dataContextType"]!.GetValue<string>());
        Assert.Equal("runtime_binding_path_metadata_not_available", node["bindingState"]!["diagnostics"]![0]!["code"]!.GetValue<string>());
        Assert.Equal("available", node["debugState"]!["status"]!.GetValue<string>());
        Assert.Equal("10..20", node["debugState"]!["fields"]!["visibleRange"]!.GetValue<string>());
    }

    [Fact]
    public void UiAuditResponseSerializesStableShape()
    {
        var sessionId = new SessionId("session-1");
        var auditedAt = new DateTimeOffset(2026, 6, 13, 1, 0, 0, TimeSpan.Zero);
        var target = new RuntimeTargetContext(sessionId, "topLevel:abc", TreeKinds.Visual, "visual:button");
        var response = new UiAuditResponse(
            sessionId,
            "topLevel:abc",
            TreeKinds.Visual,
            8,
            auditedAt,
            new UiAuditSummary(
                totalNodes: 4,
                actionableNodes: 1,
                nodesWithAutomationId: 0,
                nodesWithAccessibilityName: 0,
                nodesWithValidationMetadata: 1,
                nodesWithValidationErrors: 1,
                distinctControlTypes: 3,
                distinctClasses: 1,
                repeatedPatternCount: 1,
                issueCount: 1,
                inventoryItemCount: 2,
                accessibilityStatus: "issues_found",
                validationStatus: "errors_found",
                focusOrderStatus: "available"),
            issues:
            [
                new UiAuditIssue(
                    "ui-audit:1",
                    "accessibility",
                    "warning",
                    "accessibility.missing_automation_id",
                    "Missing automation id.",
                    "runtime_tree+public_avalonia_metadata",
                    target,
                    "Add AutomationProperties.AutomationId.",
                    "visual:button",
                    "Avalonia.Controls.Button",
                    "SaveButton")
            ],
            inventory:
            [
                new UiInventoryItem(
                    "inventory:control:button",
                    "control",
                    "Button",
                    1,
                    "runtime_tree",
                    sampleTargets: [target]),
                new UiInventoryItem(
                    "inventory:resource:resource-dictionaries",
                    "resource",
                    "resource_dictionaries",
                    0,
                    "not_available",
                    "not_available")
            ],
            target: new RuntimeTargetContext(sessionId, "topLevel:abc", TreeKinds.Visual));

        var node = JsonNode.Parse(JsonSerializer.Serialize(response))!;

        Assert.Equal("session-1", node["sessionId"]!.GetValue<string>());
        Assert.Equal("topLevel:abc", node["topLevelId"]!.GetValue<string>());
        Assert.Equal(TreeKinds.Visual, node["treeKind"]!.GetValue<string>());
        Assert.Equal(4, node["summary"]!["totalNodes"]!.GetValue<int>());
        Assert.Equal("issues_found", node["summary"]!["accessibilityStatus"]!.GetValue<string>());
        Assert.Equal("accessibility.missing_automation_id", node["issues"]![0]!["code"]!.GetValue<string>());
        Assert.Equal("visual:button", node["issues"]![0]!["target"]!["nodeId"]!.GetValue<string>());
        Assert.Equal("Button", node["inventory"]![0]!["name"]!.GetValue<string>());
        Assert.Equal("not_available", node["inventory"]![1]!["status"]!.GetValue<string>());
        Assert.Equal("issues_found", node["agentReview"]!["status"]!.GetValue<string>());
        Assert.Equal("issues: 1", node["agentReview"]!["summary"]![2]!.GetValue<string>());
    }

    [Fact]
    public void DesignQualityAuditRequestAndResponseSerializeStableShapes()
    {
        var sessionId = new SessionId("session-1");
        var target = new RuntimeTargetContext(sessionId, "topLevel:abc", TreeKinds.Visual, "visual:icon");
        var request = new DesignQualityAuditRequest(
            sessionId,
            "topLevel:abc",
            requestId: "design-request",
            scopeName: "Toolbar",
            onlyChangedNodes: true,
            changedNodeIds: ["visual:icon"],
            excludeTypes: ["Popup"],
            suppressions:
            [
                new DesignQualitySuppression(
                    "design.surface.unintended_1px_seam",
                    reason: "intentional separator")
            ]);
        var requestNode = JsonNode.Parse(JsonSerializer.Serialize(request))!;

        Assert.Equal("design-request", requestNode["requestId"]!.GetValue<string>());
        Assert.Equal("Toolbar", requestNode["scopeName"]!.GetValue<string>());
        Assert.True(requestNode["onlyChangedNodes"]!.GetValue<bool>());
        Assert.Equal("visual:icon", requestNode["changedNodeIds"]![0]!.GetValue<string>());
        Assert.Equal("Popup", requestNode["excludeTypes"]![0]!.GetValue<string>());
        Assert.Equal("design.surface.unintended_1px_seam", requestNode["suppressions"]![0]!["code"]!.GetValue<string>());

        var response = new DesignQualityAuditResponse(
            "design-request",
            sessionId,
            "topLevel:abc",
            TreeKinds.Visual,
            new DateTimeOffset(2026, 7, 1, 12, 0, 0, TimeSpan.Zero),
            new DesignQualityAuditSummary(
                totalNodes: 8,
                scopedNodes: 4,
                evaluatedNodes: 3,
                outOfScopeNodes: 4,
                excludedNodeCount: 1,
                findingCount: 1,
                ignoredFindingCount: 1,
                suppressionRuleCount: 1,
                status: "issues_found",
                scopeStatus: "scoped",
                categoryCounts: new Dictionary<string, int> { ["alignment"] = 1 }),
            target,
            findings:
            [
                new DesignQualityFinding(
                    "design-quality:1",
                    "alignment",
                    "warning",
                    "design.alignment.icon_center_mismatch",
                    "Icon center mismatch.",
                    "runtime_tree_bounds_metadata",
                    target,
                    "Align icon.",
                    "visual:icon",
                    "Avalonia.Controls.PathIcon",
                    "Icon",
                    bounds: new NodeBounds(1, 2, 16, 16))
            ],
            ignoredFindings:
            [
                new DesignQualityFinding(
                    "design-quality:2",
                    "surface",
                    "warning",
                    "design.surface.unintended_1px_seam",
                    "Seam.",
                    "runtime_tree_bounds_metadata",
                    target,
                    "Suppress if intentional.",
                    "visual:seam",
                    ignored: true,
                    ignoredReason: "suppressed:intentional separator")
            ]);

        var responseNode = JsonNode.Parse(JsonSerializer.Serialize(response))!;

        Assert.Equal("design-request", responseNode["requestId"]!.GetValue<string>());
        Assert.Equal("issues_found", responseNode["summary"]!["status"]!.GetValue<string>());
        Assert.Equal(1, responseNode["summary"]!["categoryCounts"]!["alignment"]!.GetValue<int>());
        Assert.Equal("design.alignment.icon_center_mismatch", responseNode["findings"]![0]!["code"]!.GetValue<string>());
        Assert.Equal("visual:icon", responseNode["findings"]![0]!["target"]!["nodeId"]!.GetValue<string>());
        Assert.True(responseNode["ignoredFindings"]![0]!["ignored"]!.GetValue<bool>());
        Assert.Equal("suppressed:intentional separator", responseNode["ignoredFindings"]![0]!["ignoredReason"]!.GetValue<string>());
        Assert.Equal("issues_found", responseNode["agentReview"]!["status"]!.GetValue<string>());
        Assert.Equal("ignored: 1", responseNode["agentReview"]!["summary"]![3]!.GetValue<string>());
    }

    [Fact]
    public void ScreenshotRegionAssertionResponseSerializesStableShape()
    {
        var response = new ScreenshotRegionAssertionResponse(
            "C:\\shots\\current.png",
            new ScreenshotRegion(4, 8, 20, 10, "toolbar"),
            ScreenshotRegionAssertionModes.Changed,
            passed: true,
            pixelWidth: 320,
            pixelHeight: 200,
            totalPixels: 200,
            nonBlankPixels: 50,
            nonBlankPercent: 25,
            changedPixels: 12,
            changedPercent: 6,
            maxDelta: 255,
            tolerance: 1,
            baselinePath: "C:\\shots\\baseline.png",
            cropPath: "C:\\shots\\crop.png");

        var node = JsonNode.Parse(JsonSerializer.Serialize(response))!;

        Assert.Equal("changed", node["assertion"]!.GetValue<string>());
        Assert.True(node["passed"]!.GetValue<bool>());
        Assert.Equal("toolbar", node["region"]!["name"]!.GetValue<string>());
        Assert.Equal(12, node["changedPixels"]!.GetValue<long>());
        Assert.Equal(255, node["maxDelta"]!.GetValue<int>());
        Assert.Equal(Path.GetFullPath("C:\\shots\\crop.png"), node["cropPath"]!.GetValue<string>());
    }

    [Fact]
    public void BridgeIpcRequestSerializesStableInputTargetAndKeyShape()
    {
        var request = new BridgeIpcRequest(
            "request-input",
            BridgeIpcMethods.Input,
            "topLevel:abc",
            action: InputActions.KeyDown,
            targetNodeId: "visual:button",
            inputKey: "Enter",
            keyModifiers: "Control+Shift");

        var json = JsonSerializer.Serialize(request);
        var node = JsonNode.Parse(json)!;

        Assert.Equal("request-input", node["requestId"]!.GetValue<string>());
        Assert.Equal("input", node["method"]!.GetValue<string>());
        Assert.Equal("topLevel:abc", node["topLevelId"]!.GetValue<string>());
        Assert.Equal("key_down", node["action"]!.GetValue<string>());
        Assert.Equal("visual:button", node["targetNodeId"]!.GetValue<string>());
        Assert.Equal("Enter", node["inputKey"]!.GetValue<string>());
        Assert.Equal("Control+Shift", node["keyModifiers"]!.GetValue<string>());
    }

    [Fact]
    public void BridgeIpcRequestSerializesInspectNodeShape()
    {
        var request = new BridgeIpcRequest(
            "request-inspect",
            BridgeIpcMethods.InspectNode,
            "topLevel:abc",
            treeKind: TreeKinds.Visual,
            nodeId: "visual:button");

        var json = JsonSerializer.Serialize(request);
        var node = JsonNode.Parse(json)!;

        Assert.Equal("request-inspect", node["requestId"]!.GetValue<string>());
        Assert.Equal("inspect_node", node["method"]!.GetValue<string>());
        Assert.Equal("topLevel:abc", node["topLevelId"]!.GetValue<string>());
        Assert.Equal("visual", node["treeKind"]!.GetValue<string>());
        Assert.Equal("visual:button", node["nodeId"]!.GetValue<string>());
    }

    [Fact]
    public void PreviewRequestSerializesStableShape()
    {
        var request = new PreviewRequest(
            "C:\\previews\\main.png",
            1440,
            900,
            120,
            "C:\\apps\\Sample\\Sample.csproj",
            "Views\\MainView.axaml",
            "dark",
            "ja-JP",
            "Sample.PreviewDesignData");

        var json = JsonSerializer.Serialize(request);
        var node = JsonNode.Parse(json)!;

        Assert.Equal("C:\\previews\\main.png", node["outputPath"]!.GetValue<string>());
        Assert.Equal(1440, node["width"]!.GetValue<double>());
        Assert.Equal(900, node["height"]!.GetValue<double>());
        Assert.Equal(120, node["dpi"]!.GetValue<double>());
        Assert.Equal("C:\\apps\\Sample\\Sample.csproj", node["projectPath"]!.GetValue<string>());
        Assert.Equal("Views\\MainView.axaml", node["viewPath"]!.GetValue<string>());
        Assert.Equal("dark", node["themeVariant"]!.GetValue<string>());
        Assert.Equal("ja-JP", node["culture"]!.GetValue<string>());
        Assert.Equal("Sample.PreviewDesignData", node["designDataType"]!.GetValue<string>());
    }

    [Fact]
    public void PreviewRequestOmitsOptionalDimensionsWhenUnset()
    {
        var request = new PreviewRequest(
            "C:\\previews\\main.png",
            dpi: 96,
            projectPath: "C:\\apps\\Sample\\Sample.csproj",
            viewPath: "Views\\MainView.axaml",
            stateVariant: "loading");

        var json = JsonSerializer.Serialize(request);
        var node = JsonNode.Parse(json)!;

        Assert.Null(node["width"]);
        Assert.Null(node["height"]);
        Assert.Equal(96, node["dpi"]!.GetValue<double>());
        Assert.Equal("loading", node["stateVariant"]!.GetValue<string>());
    }

    [Fact]
    public void PreviewRequestSerializesBuildIsolationOptions()
    {
        var request = new PreviewRequest(
            "C:\\previews\\main.png",
            projectPath: "C:\\apps\\Sample\\Sample.csproj",
            viewPath: "Views\\MainView.axaml",
            buildOutputRoot: "C:\\isolated\\bin",
            assemblyPath: "C:\\isolated\\bin\\Debug\\net10.0\\Sample.dll",
            noBuild: true);

        var json = JsonSerializer.Serialize(request);
        var node = JsonNode.Parse(json)!;

        Assert.Equal("C:\\isolated\\bin", node["buildOutputRoot"]!.GetValue<string>());
        Assert.Equal("C:\\isolated\\bin\\Debug\\net10.0\\Sample.dll", node["assemblyPath"]!.GetValue<string>());
        Assert.True(node["noBuild"]!.GetValue<bool>());
    }

    [Fact]
    public void PreviewResponseSerializesStableShape()
    {
        var renderedAt = new DateTimeOffset(2026, 6, 7, 1, 0, 0, TimeSpan.Zero);
        var response = new PreviewResponse(
            "C:\\previews\\main.png",
            1440,
            900,
            96,
            renderedAt,
            "C:\\apps\\Sample\\Sample.csproj",
            "Views\\MainView.axaml",
            "light",
            "ja-JP",
            "Sample.PreviewDesignData",
            stateVariant: "loading");

        var json = JsonSerializer.Serialize(response);
        var node = JsonNode.Parse(json)!;

        Assert.Equal("C:\\previews\\main.png", node["filePath"]!.GetValue<string>());
        Assert.Equal(1440, node["pixelWidth"]!.GetValue<int>());
        Assert.Equal(900, node["pixelHeight"]!.GetValue<int>());
        Assert.Equal(96, node["dpi"]!.GetValue<double>());
        Assert.Equal(renderedAt, DateTimeOffset.Parse(node["renderedAt"]!.GetValue<string>(), CultureInfo.InvariantCulture));
        Assert.Equal("C:\\apps\\Sample\\Sample.csproj", node["projectPath"]!.GetValue<string>());
        Assert.Equal("Views\\MainView.axaml", node["viewPath"]!.GetValue<string>());
        Assert.Equal("light", node["themeVariant"]!.GetValue<string>());
        Assert.Equal("ja-JP", node["culture"]!.GetValue<string>());
        Assert.Equal("Sample.PreviewDesignData", node["designDataType"]!.GetValue<string>());
        Assert.Equal("loading", node["stateVariant"]!.GetValue<string>());
    }

    [Fact]
    public void PreviewResponseSerializesProjectInfo()
    {
        var renderedAt = new DateTimeOffset(2026, 6, 10, 11, 0, 0, TimeSpan.Zero);
        var response = new PreviewResponse(
            "C:\\previews\\main.png",
            720,
            420,
            96,
            renderedAt,
            projectInfo: new PreviewProjectInfo(
                "C:\\apps\\Sample\\Sample.csproj",
                "C:\\apps\\Sample",
                "Sample.Designer",
                targetFrameworks: ["net10.0"],
                selectedTargetFramework: "net10.0",
                buildConfiguration: "Debug",
                outputAssemblyPath: "C:\\apps\\Sample\\bin\\Debug\\net10.0\\Sample.Designer.dll",
                appXamlPath: "C:\\apps\\Sample\\App.axaml",
                buildOutputRoot: "C:\\isolated\\bin",
                buildIntermediateOutputRoot: "C:\\isolated\\obj",
                buildLogPath: "C:\\previews\\.avascope\\logs\\main-build.log",
                buildMode: "isolated_default_build"));

        var json = JsonSerializer.Serialize(response);
        var node = JsonNode.Parse(json)!;

        Assert.Equal("C:\\apps\\Sample\\Sample.csproj", node["projectInfo"]!["projectPath"]!.GetValue<string>());
        Assert.Equal("Sample.Designer", node["projectInfo"]!["assemblyName"]!.GetValue<string>());
        Assert.Equal("net10.0", node["projectInfo"]!["targetFrameworks"]![0]!.GetValue<string>());
        Assert.Equal("net10.0", node["projectInfo"]!["selectedTargetFramework"]!.GetValue<string>());
        Assert.Equal("C:\\apps\\Sample\\bin\\Debug\\net10.0\\Sample.Designer.dll", node["projectInfo"]!["outputAssemblyPath"]!.GetValue<string>());
        Assert.Equal("C:\\isolated\\bin", node["projectInfo"]!["buildOutputRoot"]!.GetValue<string>());
        Assert.Equal("C:\\isolated\\obj", node["projectInfo"]!["buildIntermediateOutputRoot"]!.GetValue<string>());
        Assert.Equal("C:\\previews\\.avascope\\logs\\main-build.log", node["projectInfo"]!["buildLogPath"]!.GetValue<string>());
        Assert.Equal("isolated_default_build", node["projectInfo"]!["buildMode"]!.GetValue<string>());
    }

    [Fact]
    public void PreviewResponseSerializesDiagnostics()
    {
        var renderedAt = new DateTimeOffset(2026, 6, 8, 8, 0, 0, TimeSpan.Zero);
        var response = new PreviewResponse(
            "C:\\previews\\main.png",
            320,
            200,
            96,
            renderedAt,
            diagnostics:
            [
                new PreviewDiagnostic(
                    PreviewDiagnosticSeverities.Warning,
                    PreviewDiagnosticCategories.Binding,
                    "binding_missing_datacontext",
                    "Binding has no DataContext.",
                    "visual:text",
                    "Avalonia.Controls.TextBlock",
                    "Text",
                    "Views\\MainView.axaml",
                    new NodeBounds(0, 0, 80, 24),
                    new Dictionary<string, string>
                    {
                        ["elementPath"] = "UserControl/TextBlock[1]"
                    },
                    phase: "source_analysis",
                    provenance: "xaml_source_metadata",
                    suggestedAction: "Assign design-time data or fix the binding path.")
            ]);

        var json = JsonSerializer.Serialize(response);
        var node = JsonNode.Parse(json)!;

        Assert.Equal("binding", node["diagnostics"]![0]!["category"]!.GetValue<string>());
        Assert.Equal("warning", node["diagnostics"]![0]!["severity"]!.GetValue<string>());
        Assert.Equal("binding_missing_datacontext", node["diagnostics"]![0]!["code"]!.GetValue<string>());
        Assert.Equal("Text", node["diagnostics"]![0]!["propertyName"]!.GetValue<string>());
        Assert.Equal("source_analysis", node["diagnostics"]![0]!["phase"]!.GetValue<string>());
        Assert.Equal("xaml_source_metadata", node["diagnostics"]![0]!["provenance"]!.GetValue<string>());
        Assert.Equal("Assign design-time data or fix the binding path.", node["diagnostics"]![0]!["suggestedAction"]!.GetValue<string>());
        Assert.Equal("UserControl/TextBlock[1]", node["diagnostics"]![0]!["details"]!["elementPath"]!.GetValue<string>());
    }

    [Fact]
    public void PreviewBatchResponseSerializesPerSizeResults()
    {
        var renderedAt = new DateTimeOffset(2026, 6, 8, 8, 15, 0, TimeSpan.Zero);
        var response = new PreviewBatchResponse(
        [
            new PreviewBatchEntry(
                new PreviewViewport(1440, 900),
                "C:\\previews\\main-01-1440x900.png",
                ToolResult<PreviewResponse>.Ok(new PreviewResponse(
                    "C:\\previews\\main-01-1440x900.png",
                    1440,
                    900,
                    96,
                    renderedAt))),
            new PreviewBatchEntry(
                new PreviewViewport(1280, 720),
                "C:\\previews\\main-02-1280x720.png",
                ToolResult<PreviewResponse>.Fail(new ProtocolError("preview_render_failed", "Render failed.")))
        ],
        "C:\\previews\\sheet.png",
        renderedAt);

        var json = JsonSerializer.Serialize(response);
        var node = JsonNode.Parse(json)!;

        Assert.Equal(1440, node["entries"]![0]!["viewport"]!["width"]!.GetValue<double>());
        Assert.True(node["entries"]![0]!["render"]!["success"]!.GetValue<bool>());
        Assert.False(node["entries"]![1]!["render"]!["success"]!.GetValue<bool>());
        Assert.Equal("C:\\previews\\sheet.png", node["contactSheetPath"]!.GetValue<string>());
    }

    [Fact]
    public void PreviewAnimationRequestAndResponseSerializeStableShapes()
    {
        var sampledAt = new DateTimeOffset(2026, 6, 9, 14, 0, 0, TimeSpan.Zero);
        var request = new PreviewAnimationRequest(
            "C:\\previews\\animation.png",
            [0, 150, 300],
            320,
            200,
            96,
            "C:\\apps\\Sample\\Sample.csproj",
            "Views\\AnimatedView.axaml",
            "dark",
            "en-US",
            "Sample.AnimationDesignData",
            "C:\\previews\\animation-strip.png",
            "C:\\previews\\animation.html");
        var response = new PreviewAnimationResponse(
            [
                new PreviewAnimationFrame(
                    150,
                    "C:\\previews\\animation-02-150ms.png",
                    ToolResult<PreviewResponse>.Ok(new PreviewResponse(
                        "C:\\previews\\animation-02-150ms.png",
                        320,
                        200,
                        96,
                        sampledAt,
                        animationTimeOffsetMs: 150)))
            ],
            "C:\\previews\\animation-strip.png",
            new PreviewAnimationMotionSummary(
                "changed",
                3,
                42,
                64000,
                0.065625,
                128,
                new Dictionary<string, string>
                {
                    ["metadataProvenance"] = "not_available"
                }),
            [
                new PreviewDiagnostic(
                    PreviewDiagnosticSeverities.Info,
                    PreviewDiagnosticCategories.Animation,
                    "animation_pixels_changed",
                    "Frames changed.")
            ],
            sampledAt,
            new PreviewAnimationViewerResponse(
                "C:\\previews\\animation.html",
                "file:///C:/previews/animation.html",
                sampledAt.AddSeconds(1)));

        var requestNode = JsonNode.Parse(JsonSerializer.Serialize(request))!;
        var responseNode = JsonNode.Parse(JsonSerializer.Serialize(response))!;

        Assert.Equal("C:\\previews\\animation.png", requestNode["outputPath"]!.GetValue<string>());
        Assert.Equal(150, requestNode["timeOffsetsMs"]![1]!.GetValue<int>());
        Assert.Equal(320, requestNode["width"]!.GetValue<double>());
        Assert.Equal("Views\\AnimatedView.axaml", requestNode["viewPath"]!.GetValue<string>());
        Assert.Equal("C:\\previews\\animation-strip.png", requestNode["frameStripPath"]!.GetValue<string>());
        Assert.Equal("C:\\previews\\animation.html", requestNode["viewerPath"]!.GetValue<string>());
        Assert.Equal(150, responseNode["frames"]![0]!["timeOffsetMs"]!.GetValue<int>());
        Assert.True(responseNode["frames"]![0]!["render"]!["success"]!.GetValue<bool>());
        Assert.Equal(150, responseNode["frames"]![0]!["render"]!["value"]!["animationTimeOffsetMs"]!.GetValue<int>());
        Assert.Equal("changed", responseNode["motion"]!["status"]!.GetValue<string>());
        Assert.Equal(42, responseNode["motion"]!["changedPixels"]!.GetValue<long>());
        Assert.Equal("animation", responseNode["diagnostics"]![0]!["category"]!.GetValue<string>());
        Assert.Equal("C:\\previews\\animation-strip.png", responseNode["frameStripPath"]!.GetValue<string>());
        Assert.Equal("file:///C:/previews/animation.html", responseNode["viewer"]!["previewUrl"]!.GetValue<string>());
    }

    [Fact]
    public void PreviewDiffAndCleanupResponsesSerializeStableShapes()
    {
        var diff = new PreviewDiffResponse(
            "C:\\baseline.png",
            "C:\\current.png",
            passed: false,
            pixelWidth: 4,
            pixelHeight: 4,
            tolerance: 0,
            changedPixels: 1,
            totalPixels: 16,
            changedPercent: 6.25,
            maxDelta: 255,
            "C:\\diff.png");
        var cleanup = new PreviewCleanupResponse(
            "C:\\avascope\\preview-sessions",
            1,
            [
                new PreviewSessionDiagnostic(
                    DiagnosticStatuses.Stale,
                    "C:\\avascope\\preview-sessions\\session.json",
                    new SessionSummary(
                        new SessionId("preview-1"),
                        SessionKinds.Preview,
                        SessionStates.Failed,
                        DateTimeOffset.UnixEpoch))
            ],
            ["C:\\avascope\\preview-sessions\\session.json"],
            DateTimeOffset.UnixEpoch);

        var diffNode = JsonNode.Parse(JsonSerializer.Serialize(diff))!;
        var cleanupNode = JsonNode.Parse(JsonSerializer.Serialize(cleanup))!;

        Assert.False(diffNode["passed"]!.GetValue<bool>());
        Assert.Equal(1, diffNode["changedPixels"]!.GetValue<long>());
        Assert.Equal("C:\\diff.png", diffNode["diffPath"]!.GetValue<string>());
        Assert.Equal(1, cleanupNode["deletedPreviewSessionRecords"]!.GetValue<int>());
        Assert.Equal(DiagnosticStatuses.Stale, cleanupNode["stalePreviewSessions"]![0]!["status"]!.GetValue<string>());
    }

    [Fact]
    public void BridgeCleanupResponseSerializesStableShape()
    {
        var cleanedAt = new DateTimeOffset(2026, 6, 10, 8, 0, 0, TimeSpan.Zero);
        var response = new BridgeCleanupResponse(
            "C:\\avascope\\sessions",
            1,
            [
                new BridgeSessionDiagnostic(
                    DiagnosticStatuses.Stale,
                    "C:\\avascope\\sessions\\session.json",
                    new SessionSummary(
                        new SessionId("runtime-1"),
                        SessionKinds.Runtime,
                        SessionStates.Failed,
                        DateTimeOffset.UnixEpoch),
                    1234,
                    DiagnosticTransportKinds.NamedPipe,
                    "avascope-1234-runtime-1",
                    error: new ProtocolError("bridge_ipc_unavailable", "Process is gone."),
                    processName: "SampleApp",
                    checkedAt: cleanedAt,
                    cleanupCandidate: true)
            ],
            ["C:\\avascope\\sessions\\session.json"],
            [],
            cleanedAt);

        var node = JsonNode.Parse(JsonSerializer.Serialize(response))!;

        Assert.Equal("C:\\avascope\\sessions", node["manifestDirectory"]!.GetValue<string>());
        Assert.Equal(1, node["deletedBridgeManifestRecords"]!.GetValue<int>());
        Assert.Equal(DiagnosticStatuses.Stale, node["cleanupCandidates"]![0]!["status"]!.GetValue<string>());
        Assert.Equal("SampleApp", node["cleanupCandidates"]![0]!["processName"]!.GetValue<string>());
        Assert.True(node["cleanupCandidates"]![0]!["cleanupCandidate"]!.GetValue<bool>());
        Assert.Equal("C:\\avascope\\sessions\\session.json", node["deletedPaths"]![0]!.GetValue<string>());
        Assert.Equal(cleanedAt, DateTimeOffset.Parse(node["cleanedAt"]!.GetValue<string>(), CultureInfo.InvariantCulture));
    }

    [Fact]
    public void PreviewWatchResponseSerializesEventsAndLatestSession()
    {
        var startedAt = new DateTimeOffset(2026, 6, 8, 9, 0, 0, TimeSpan.Zero);
        var completedAt = startedAt.AddSeconds(2);
        var session = new PreviewSessionSummary(
            new SessionSummary(
                new SessionId("preview-1"),
                SessionKinds.Preview,
                SessionStates.Active,
                startedAt,
                "Watched preview"),
            new PreviewRequest(
                "C:\\previews\\main.png",
                100,
                100,
                96,
                "C:\\apps\\Sample\\Sample.csproj",
                "Views\\MainView.axaml"),
            ToolResult<PreviewResponse>.Ok(new PreviewResponse(
                "C:\\previews\\main.png",
                100,
                100,
                96,
                completedAt)),
            completedAt);
        var response = new PreviewWatchResponse(
            new SessionId("preview-1"),
            ["C:\\apps\\Sample\\Views\\MainView.axaml"],
            [
                new PreviewWatchEvent(
                    PreviewWatchEventTypes.Changed,
                    startedAt.AddSeconds(1),
                    "C:\\apps\\Sample\\Views\\MainView.axaml",
                    "Changed"),
                new PreviewWatchEvent(
                    PreviewWatchEventTypes.Reloaded,
                    completedAt,
                    reload: ToolResult<PreviewSessionSummary>.Ok(session)),
                new PreviewWatchEvent(
                    PreviewWatchEventTypes.Skipped,
                    completedAt.AddMilliseconds(10),
                    "C:\\apps\\Sample\\Views\\MainView.axaml",
                    "unchanged_input_snapshot")
            ],
            timedOut: false,
            reloadCount: 1,
            startedAt,
            completedAt,
            session);

        var node = JsonNode.Parse(JsonSerializer.Serialize(response))!;

        Assert.Equal("preview-1", node["sessionId"]!.GetValue<string>());
        Assert.False(node["timedOut"]!.GetValue<bool>());
        Assert.Equal(1, node["reloadCount"]!.GetValue<int>());
        Assert.Equal(PreviewWatchEventTypes.Changed, node["events"]![0]!["eventType"]!.GetValue<string>());
        Assert.Equal("Changed", node["events"]![0]!["changeKind"]!.GetValue<string>());
        Assert.True(node["events"]![1]!["reload"]!["success"]!.GetValue<bool>());
        Assert.Equal(PreviewWatchEventTypes.Skipped, node["events"]![2]!["eventType"]!.GetValue<string>());
        Assert.Equal("unchanged_input_snapshot", node["events"]![2]!["changeKind"]!.GetValue<string>());
        Assert.Equal("Watched preview", node["latestSession"]!["session"]!["displayName"]!.GetValue<string>());
        Assert.Equal("one_shot_isolated_child_process", node["lifecycle"]!["hostProcessMode"]!.GetValue<string>());
        Assert.False(node["lifecycle"]!["persistentHostEnabled"]!.GetValue<bool>());
        Assert.Contains("close-preview-session", node["lifecycle"]!["closeSemantics"]!.GetValue<string>(), StringComparison.Ordinal);
        Assert.Contains("TTL", node["lifecycle"]!["ttlSemantics"]!.GetValue<string>(), StringComparison.Ordinal);
        Assert.Contains("failed child process", node["lifecycle"]!["crashSemantics"]!.GetValue<string>(), StringComparison.Ordinal);
        Assert.Contains("cleanup", node["lifecycle"]!["cleanupSemantics"]!.GetValue<string>(), StringComparison.Ordinal);
        Assert.Contains("persistent-host", node["lifecycle"]!["nextStep"]!.GetValue<string>(), StringComparison.Ordinal);
    }

    [Fact]
    public void PreviewBaselineResponsesSerializeStableShapes()
    {
        var createdAt = new DateTimeOffset(2026, 6, 8, 10, 0, 0, TimeSpan.Zero);
        var baseline = new PreviewBaselineEntry(
            0,
            new PreviewViewport(1440, 900),
            "C:\\baselines\\baseline-01-1440x900.png",
            96,
            "C:\\apps\\Sample\\Sample.csproj",
            "Views\\MainView.axaml",
            "light",
            "en-US");
        var manifest = new PreviewBaselineManifest(
            PreviewBaselineManifest.CurrentVersion,
            createdAt,
            [baseline]);
        var create = new PreviewBaselineCreateResponse(
            "C:\\baselines\\baseline.json",
            manifest,
            new PreviewBatchResponse(
            [
                new PreviewBatchEntry(
                    baseline.Viewport,
                    baseline.ImagePath,
                    ToolResult<PreviewResponse>.Ok(new PreviewResponse(
                        baseline.ImagePath,
                        1440,
                        900,
                        96,
                        createdAt)))
            ],
            null,
            createdAt));
        var reportPack = new AgentEvidenceReportPackResponse(
            "C:\\reports\\pack",
            "failed",
            createdAt,
            totalEntries: 1,
            passedEntries: 0,
            failedEntries: 1,
            [
                new AgentEvidenceReportPackAsset(
                    "html",
                    "C:\\reports\\pack\\baseline-report.html",
                    "text/html",
                    "Review report.")
            ],
            new Dictionary<string, string>
            {
                ["os"] = "Windows"
            },
            new Dictionary<string, string>
            {
                ["kind"] = "preview-baseline-check",
                ["mutationHistoryStatus"] = "preset_metadata_available"
            });
        var check = new PreviewBaselineCheckResponse(
            "C:\\baselines\\baseline.json",
            passed: false,
            [
                new PreviewBaselineCheckEntry(
                    baseline,
                    "C:\\current\\current-01-1440x900.png",
                    "C:\\diff\\diff-01-1440x900.png",
                    ToolResult<PreviewResponse>.Ok(new PreviewResponse(
                        "C:\\current\\current-01-1440x900.png",
                        1440,
                        900,
                        96,
                        createdAt)),
                    ToolResult<PreviewDiffResponse>.Ok(new PreviewDiffResponse(
                        baseline.ImagePath,
                        "C:\\current\\current-01-1440x900.png",
                        passed: false,
                        pixelWidth: 1440,
                        pixelHeight: 900,
                        tolerance: 0,
                        changedPixels: 10,
                        totalPixels: 1296000,
                        changedPercent: 0.001,
                        maxDelta: 255,
                        "C:\\diff\\diff-01-1440x900.png")))
            ],
            createdAt,
            "C:\\reports\\baseline-check.json",
            reportPack);

        var createNode = JsonNode.Parse(JsonSerializer.Serialize(create))!;
        var checkNode = JsonNode.Parse(JsonSerializer.Serialize(check))!;

        Assert.Equal("C:\\baselines\\baseline.json", createNode["manifestPath"]!.GetValue<string>());
        Assert.Equal(PreviewBaselineManifest.CurrentVersion, createNode["manifest"]!["version"]!.GetValue<int>());
        Assert.Equal("C:\\baselines\\baseline-01-1440x900.png", createNode["manifest"]!["entries"]![0]!["imagePath"]!.GetValue<string>());
        Assert.False(checkNode["passed"]!.GetValue<bool>());
        Assert.Equal("C:\\reports\\baseline-check.json", checkNode["reportPath"]!.GetValue<string>());
        Assert.Equal("failed", checkNode["reportPack"]!["status"]!.GetValue<string>());
        Assert.Equal("html", checkNode["reportPack"]!["assets"]![0]!["kind"]!.GetValue<string>());
        Assert.Equal("preset_metadata_available", checkNode["reportPack"]!["metadata"]!["mutationHistoryStatus"]!.GetValue<string>());
        Assert.Equal("C:\\diff\\diff-01-1440x900.png", checkNode["entries"]![0]!["diffPath"]!.GetValue<string>());
        Assert.False(checkNode["entries"]![0]!["diff"]!["value"]!["passed"]!.GetValue<bool>());
        Assert.Equal("failed", checkNode["agentReview"]!["status"]!.GetValue<string>());
        Assert.Equal("visual_diff_changed", checkNode["agentReview"]!["failures"]![0]!["code"]!.GetValue<string>());
        Assert.Equal("html", checkNode["agentReview"]!["reportPaths"]![2]!["kind"]!.GetValue<string>());
        Assert.Equal("file:///C:/reports/pack/baseline-report.html", checkNode["agentReview"]!["reviewUrls"]![0]!.GetValue<string>());
        Assert.Equal("diff", checkNode["agentReview"]!["artifactPaths"]![2]!["kind"]!.GetValue<string>());
    }

    [Fact]
    public void PreviewBaselineSuiteManifestSerializesStableShape()
    {
        var target = new RuntimeTargetContext(
            new SessionId("session-suite"),
            "topLevel:main",
            TreeKinds.Visual,
            "visual:root");
        var operation = new RuntimeMutationOperation(
            RuntimeMutationOperationKinds.SetProperty,
            propertyName: "Width",
            value: "320",
            valueType: "double");
        var suite = new PreviewBaselineSuiteManifest(
            PreviewBaselineSuiteManifest.CurrentVersion,
            "agent-suite",
            [
                new PreviewBaselineSuiteEntry(
                    "main",
                    "C:\\apps\\Sample\\Sample.csproj",
                    "Views\\MainView.axaml",
                    "main",
                    "dark-wide",
                    "C:\\apps\\Sample\\avascope.preview.json",
                    sizes: [new PreviewViewport(320, 200)],
                    themes: ["dark"],
                    runtimeTarget: target,
                    mutationPresetIds: ["wide"],
                    variants:
                    [
                        new PreviewBaselineSuiteVariant(
                            "dark-wide",
                            new PreviewViewport(360, 240),
                            144,
                            "dark",
                            "en-US",
                            "Sample.DesignData",
                            150,
                            target,
                            ["wide"],
                            stateVariant: "validation-errors")
                    ])
            ],
            new PreviewBaselineSuiteDefaults(
                sizes: [new PreviewViewport(320, 200)],
                dpis: [96],
                themes: ["light"],
                cultures: ["en-US"],
                animationFramesMs: [0],
                mutationPresetIds: ["wide"]),
            [
                new PreviewBaselineMutationPreset(
                    "wide",
                    "Wider layout state.",
                    [operation])
            ]);
        var baseline = new PreviewBaselineEntry(
            0,
            new PreviewViewport(360, 240),
            "C:\\baselines\\suite\\main.png",
            144,
            "C:\\apps\\Sample\\Sample.csproj",
            "Views\\MainView.axaml",
            "dark",
            "en-US",
            "Sample.DesignData",
            "agent-suite",
            "main",
            "dark-wide",
            "main",
            "dark-wide",
            "C:\\apps\\Sample\\avascope.preview.json",
            target,
            ["wide"],
            150,
            stateVariant: "validation-errors");

        var suiteNode = JsonNode.Parse(JsonSerializer.Serialize(suite))!;
        var baselineNode = JsonNode.Parse(JsonSerializer.Serialize(baseline))!;

        Assert.Equal(PreviewBaselineSuiteManifest.CurrentVersion, suiteNode["version"]!.GetValue<int>());
        Assert.Equal("validation-errors", suiteNode["entries"]![0]!["variants"]![0]!["stateVariant"]!.GetValue<string>());
        Assert.Equal("validation-errors", baselineNode["stateVariant"]!.GetValue<string>());
        Assert.Equal("agent-suite", suiteNode["name"]!.GetValue<string>());
        Assert.Equal(320, suiteNode["defaults"]!["sizes"]![0]!["width"]!.GetValue<double>());
        Assert.Equal("wide", suiteNode["defaults"]!["mutationPresetIds"]![0]!.GetValue<string>());
        Assert.Equal("main", suiteNode["entries"]![0]!["id"]!.GetValue<string>());
        Assert.Equal("C:\\apps\\Sample\\Sample.csproj", suiteNode["entries"]![0]!["projectPath"]!.GetValue<string>());
        Assert.Equal("C:\\apps\\Sample\\avascope.preview.json", suiteNode["entries"]![0]!["profileFilePath"]!.GetValue<string>());
        Assert.Equal("session-suite", suiteNode["entries"]![0]!["runtimeTarget"]!["sessionId"]!.GetValue<string>());
        Assert.Equal("dark-wide", suiteNode["entries"]![0]!["variants"]![0]!["name"]!.GetValue<string>());
        Assert.Equal(150, suiteNode["entries"]![0]!["variants"]![0]!["animationTimeOffsetMs"]!.GetValue<int>());
        Assert.Equal("wide", suiteNode["mutationPresets"]![0]!["id"]!.GetValue<string>());
        Assert.Equal(RuntimeMutationOperationKinds.SetProperty, suiteNode["mutationPresets"]![0]!["operations"]![0]!["kind"]!.GetValue<string>());

        Assert.Equal("agent-suite", baselineNode["suiteName"]!.GetValue<string>());
        Assert.Equal("main", baselineNode["suiteEntryId"]!.GetValue<string>());
        Assert.Equal("dark-wide", baselineNode["suiteVariantName"]!.GetValue<string>());
        Assert.Equal("main", baselineNode["profileName"]!.GetValue<string>());
        Assert.Equal("dark-wide", baselineNode["profileVariant"]!.GetValue<string>());
        Assert.Equal("C:\\apps\\Sample\\avascope.preview.json", baselineNode["profileFilePath"]!.GetValue<string>());
        Assert.Equal("session-suite", baselineNode["runtimeTarget"]!["sessionId"]!.GetValue<string>());
        Assert.Equal("wide", baselineNode["mutationPresetIds"]![0]!.GetValue<string>());
        Assert.Equal(150, baselineNode["animationTimeOffsetMs"]!.GetValue<int>());
    }

    [Fact]
    public void PreviewComparisonRulesAndRegionResultsSerializeStableShape()
    {
        var capturedAt = new DateTimeOffset(2026, 6, 12, 22, 30, 0, TimeSpan.Zero);
        var rules = new PreviewComparisonRules(
            tolerance: 2,
            maxChangedPixels: 10,
            maxChangedPercent: 1.5,
            ignoredRegions:
            [
                new ScreenshotRegion(1, 2, 3, 4, "clock")
            ],
            requiredRegions:
            [
                new PreviewRequiredRegion(
                    new ScreenshotRegion(5, 6, 7, 8, "hero"),
                    ScreenshotRegionAssertionModes.Unchanged)
            ]);
        var baseline = new PreviewBaselineEntry(
            0,
            new PreviewViewport(10, 10),
            "C:\\baselines\\main.png",
            96,
            projectPath: "C:\\apps\\Sample\\Sample.csproj",
            viewPath: "Views\\MainView.axaml",
            comparisonRules: rules);
        var regionResponse = new ScreenshotRegionAssertionResponse(
            "C:\\current\\main.png",
            rules.RequiredRegions[0].Region,
            rules.RequiredRegions[0].Assertion,
            passed: true,
            pixelWidth: 10,
            pixelHeight: 10,
            totalPixels: 56,
            nonBlankPixels: 56,
            nonBlankPercent: 100,
            tolerance: 2,
            baselinePath: baseline.ImagePath,
            cropPath: "C:\\diff\\required-region-main.png");
        var checkEntry = new PreviewBaselineCheckEntry(
            baseline,
            "C:\\current\\main.png",
            "C:\\diff\\main.png",
            ToolResult<PreviewResponse>.Ok(new PreviewResponse(
                "C:\\current\\main.png",
                10,
                10,
                96,
                capturedAt)),
            ToolResult<PreviewDiffResponse>.Ok(new PreviewDiffResponse(
                baseline.ImagePath,
                "C:\\current\\main.png",
                passed: true,
                pixelWidth: 10,
                pixelHeight: 10,
                tolerance: 2,
                changedPixels: 1,
                totalPixels: 88,
                changedPercent: 1.136,
                maxDelta: 255,
                "C:\\diff\\main.png",
                rules.IgnoredRegions,
                ignoredPixelCount: 12,
                maxChangedPixels: 10,
                maxChangedPercent: 1.5)),
            rules,
            [
                new PreviewBaselineRegionCheckResult(
                    0,
                    rules.RequiredRegions[0].Region,
                    rules.RequiredRegions[0].Assertion,
                    ToolResult<ScreenshotRegionAssertionResponse>.Ok(regionResponse))
            ]);

        var node = JsonNode.Parse(JsonSerializer.Serialize(checkEntry))!;

        Assert.Equal(2, node["baseline"]!["comparisonRules"]!["tolerance"]!.GetValue<double>());
        Assert.Equal(10, node["baseline"]!["comparisonRules"]!["maxChangedPixels"]!.GetValue<long>());
        Assert.Equal(1.5, node["baseline"]!["comparisonRules"]!["maxChangedPercent"]!.GetValue<double>());
        Assert.Equal("clock", node["baseline"]!["comparisonRules"]!["ignoredRegions"]![0]!["name"]!.GetValue<string>());
        Assert.Equal("hero", node["baseline"]!["comparisonRules"]!["requiredRegions"]![0]!["region"]!["name"]!.GetValue<string>());
        Assert.Equal(12, node["diff"]!["value"]!["ignoredPixelCount"]!.GetValue<long>());
        Assert.Equal(10, node["diff"]!["value"]!["maxChangedPixels"]!.GetValue<long>());
        Assert.Equal(1.5, node["diff"]!["value"]!["maxChangedPercent"]!.GetValue<double>());
        Assert.Equal("clock", node["diff"]!["value"]!["ignoredRegions"]![0]!["name"]!.GetValue<string>());
        Assert.Equal(0, node["requiredRegionResults"]![0]!["ruleIndex"]!.GetValue<int>());
        Assert.Equal(ScreenshotRegionAssertionModes.Unchanged, node["requiredRegionResults"]![0]!["assertion"]!.GetValue<string>());
        Assert.True(node["requiredRegionResults"]![0]!["result"]!["success"]!.GetValue<bool>());
        Assert.Equal("C:\\diff\\required-region-main.png", node["requiredRegionResults"]![0]!["result"]!["value"]!["cropPath"]!.GetValue<string>());
    }

    [Fact]
    public void PreviewSessionSummarySerializesRequestAndLastRender()
    {
        var createdAt = new DateTimeOffset(2026, 6, 7, 4, 0, 0, TimeSpan.Zero);
        var updatedAt = createdAt.AddSeconds(5);
        var response = new ListPreviewSessionsResponse(
        [
            new PreviewSessionSummary(
                new SessionSummary(
                    new SessionId("preview-1"),
                    SessionKinds.Preview,
                    SessionStates.Active,
                    createdAt,
                    "Views\\MainView.axaml"),
                new PreviewRequest(
                    "C:\\previews\\main.png",
                    1440,
                    900,
                    96,
                    "C:\\apps\\Sample\\Sample.csproj",
                    "Views\\MainView.axaml",
                    "light",
                    "ja-JP",
                    "Sample.PreviewDesignData"),
                ToolResult<PreviewResponse>.Ok(new PreviewResponse(
                    "C:\\previews\\main.png",
                    1440,
                    900,
                    96,
                    updatedAt,
                    "C:\\apps\\Sample\\Sample.csproj",
                    "Views\\MainView.axaml",
                    "light",
                    "ja-JP",
                    "Sample.PreviewDesignData")),
                updatedAt,
                [
                    new PreviewWatchEvent(
                        PreviewWatchEventTypes.SessionCreated,
                        updatedAt,
                        changeKind: "initial_render_succeeded")
                ])
        ]);

        var json = JsonSerializer.Serialize(response);
        var node = JsonNode.Parse(json)!;
        var session = node["sessions"]![0]!;

        Assert.Equal("preview-1", session["session"]!["sessionId"]!.GetValue<string>());
        Assert.Equal("preview", session["session"]!["kind"]!.GetValue<string>());
        Assert.Equal("active", session["session"]!["state"]!.GetValue<string>());
        Assert.Equal("C:\\previews\\main.png", session["request"]!["outputPath"]!.GetValue<string>());
        Assert.Equal("Views\\MainView.axaml", session["request"]!["viewPath"]!.GetValue<string>());
        Assert.Equal("ja-JP", session["request"]!["culture"]!.GetValue<string>());
        Assert.Equal("Sample.PreviewDesignData", session["request"]!["designDataType"]!.GetValue<string>());
        Assert.True(session["lastRender"]!["success"]!.GetValue<bool>());
        Assert.Equal("C:\\previews\\main.png", session["lastRender"]!["value"]!["filePath"]!.GetValue<string>());
        Assert.Equal("ja-JP", session["lastRender"]!["value"]!["culture"]!.GetValue<string>());
        Assert.Equal("Sample.PreviewDesignData", session["lastRender"]!["value"]!["designDataType"]!.GetValue<string>());
        Assert.Equal(updatedAt, DateTimeOffset.Parse(session["updatedAt"]!.GetValue<string>(), CultureInfo.InvariantCulture));
        Assert.Equal("session_created", session["events"]![0]!["eventType"]!.GetValue<string>());
        Assert.Equal("one_shot_isolated_child_process", session["lifecycle"]!["hostProcessMode"]!.GetValue<string>());
    }

    [Fact]
    public void CloseSessionResponseSerializesSessionSummary()
    {
        var createdAt = new DateTimeOffset(2026, 6, 7, 2, 0, 0, TimeSpan.Zero);
        var closedAt = createdAt.AddMinutes(5);
        var response = new CloseSessionResponse(
            new SessionSummary(
                new SessionId("session-1"),
                SessionKinds.Runtime,
                SessionStates.Closed,
                createdAt,
                "Sample app"),
            1234,
            closedAt);

        var json = JsonSerializer.Serialize(response);
        var node = JsonNode.Parse(json)!;

        Assert.Equal(1234, node["processId"]!.GetValue<int>());
        Assert.Equal("session-1", node["session"]!["sessionId"]!.GetValue<string>());
        Assert.Equal("runtime", node["session"]!["kind"]!.GetValue<string>());
        Assert.Equal("closed", node["session"]!["state"]!.GetValue<string>());
        Assert.Equal("Sample app", node["session"]!["displayName"]!.GetValue<string>());
        Assert.Equal(closedAt, DateTimeOffset.Parse(node["closedAt"]!.GetValue<string>(), CultureInfo.InvariantCulture));
    }
}
