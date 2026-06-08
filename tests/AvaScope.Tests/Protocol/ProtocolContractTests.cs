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
                HealthResponse.Current()));

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
                        text: "AvaScope")
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
                        text: "AvaScope"),
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
    }

    [Fact]
    public void InputResponseSerializesStableShape()
    {
        var executedAt = new DateTimeOffset(2026, 6, 7, 0, 0, 0, TimeSpan.Zero);
        var response = new InputResponse(
            new SessionId("session-1"),
            "topLevel:abc",
            InputActions.Click,
            handled: true,
            executedAt,
            "visual:button");

        var json = JsonSerializer.Serialize(response);
        var node = JsonNode.Parse(json)!;

        Assert.Equal("session-1", node["sessionId"]!.GetValue<string>());
        Assert.Equal("topLevel:abc", node["topLevelId"]!.GetValue<string>());
        Assert.Equal("click", node["action"]!.GetValue<string>());
        Assert.True(node["handled"]!.GetValue<bool>());
        Assert.Equal("visual:button", node["targetNodeId"]!.GetValue<string>());
        Assert.Equal(executedAt, DateTimeOffset.Parse(node["executedAt"]!.GetValue<string>(), CultureInfo.InvariantCulture));
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
            viewPath: "Views\\MainView.axaml");

        var json = JsonSerializer.Serialize(request);
        var node = JsonNode.Parse(json)!;

        Assert.Null(node["width"]);
        Assert.Null(node["height"]);
        Assert.Equal(96, node["dpi"]!.GetValue<double>());
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
            "Sample.PreviewDesignData");

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
                updatedAt)
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
