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
        Assert.Equal("Sample app", node["displayName"]!.GetValue<string>());
        Assert.Equal(createdAt, DateTimeOffset.Parse(node["createdAt"]!.GetValue<string>(), CultureInfo.InvariantCulture));
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
}
