using AvaScope.Core;
using AvaScope.Mcp;
using AvaScope.Protocol;

namespace AvaScope.Tests.Mcp;

public sealed class AvaScopeMcpToolsTests
{
    [Fact]
    public void HealthReturnsCurrentProtocolMetadata()
    {
        var result = AvaScopeMcpTools.Health();

        Assert.True(result.Success);
        Assert.Equal("avascope", result.Value!.ServiceName);
        Assert.Equal(1, result.Value.ProtocolVersion.Major);
        Assert.Equal(0, result.Value.ProtocolVersion.Minor);
        Assert.Null(result.Error);
    }

    [Fact]
    public void ListSessionsReturnsEmptySessionList()
    {
        var registry = new SessionRegistry();

        var result = AvaScopeMcpTools.ListSessions(registry);

        Assert.True(result.Success);
        Assert.Empty(result.Value!.Sessions);
        Assert.Null(result.Error);
    }

    [Fact]
    public void ListSessionsMapsCoreSnapshotsToProtocolSummaries()
    {
        var registry = new SessionRegistry();
        var activeSession = registry.Create(SessionKinds.Runtime, "Sample app");
        var closedSession = registry.Create(SessionKinds.Preview, "Preview");
        registry.Close(closedSession.Id);

        var result = AvaScopeMcpTools.ListSessions(registry);

        Assert.True(result.Success);
        Assert.Collection(
            result.Value!.Sessions,
            session =>
            {
                Assert.Equal(activeSession.Id, session.SessionId);
                Assert.Equal(SessionKinds.Runtime, session.Kind);
                Assert.Equal(SessionStates.Active, session.State);
                Assert.Equal("Sample app", session.DisplayName);
            },
            session =>
            {
                Assert.Equal(closedSession.Id, session.SessionId);
                Assert.Equal(SessionKinds.Preview, session.Kind);
                Assert.Equal(SessionStates.Closed, session.State);
                Assert.Equal("Preview", session.DisplayName);
            });
    }

    [Fact]
    public async Task AttachToAppReturnsStructuredErrorWhenNoBridgeSessionExists()
    {
        var client = new LocalBridgeClient(CreateMissingManifestDirectory());

        var result = await AvaScopeMcpTools.AttachToApp(client, processId: Environment.ProcessId);

        Assert.False(result.Success);
        Assert.Null(result.Value);
        Assert.Equal(CoreErrorCodes.BridgeSessionNotFound, result.Error!.Code);
    }

    [Fact]
    public async Task ListTopLevelsRejectsEmptySessionId()
    {
        var client = new LocalBridgeClient(CreateMissingManifestDirectory());

        var result = await AvaScopeMcpTools.ListTopLevels(client, " ");

        Assert.False(result.Success);
        Assert.Null(result.Value);
        Assert.Equal(CoreErrorCodes.InvalidBridgeRequest, result.Error!.Code);
    }

    [Fact]
    public async Task ScreenshotRejectsEmptySessionId()
    {
        var client = new LocalBridgeClient(CreateMissingManifestDirectory());

        var result = await AvaScopeMcpTools.Screenshot(
            client,
            " ",
            "topLevel:abc",
            "capture.png");

        Assert.False(result.Success);
        Assert.Null(result.Value);
        Assert.Equal(CoreErrorCodes.InvalidBridgeRequest, result.Error!.Code);
    }

    [Fact]
    public async Task VisualTreeRejectsEmptySessionId()
    {
        var client = new LocalBridgeClient(CreateMissingManifestDirectory());

        var result = await AvaScopeMcpTools.VisualTree(
            client,
            " ",
            "topLevel:abc");

        Assert.False(result.Success);
        Assert.Null(result.Value);
        Assert.Equal(CoreErrorCodes.InvalidBridgeRequest, result.Error!.Code);
    }

    [Fact]
    public async Task FindNodesRejectsMissingFilters()
    {
        var client = new LocalBridgeClient(CreateMissingManifestDirectory());

        var result = await AvaScopeMcpTools.FindNodes(
            client,
            "session-1",
            "topLevel:abc");

        Assert.False(result.Success);
        Assert.Null(result.Value);
        Assert.Equal(CoreErrorCodes.InvalidBridgeRequest, result.Error!.Code);
    }

    [Fact]
    public async Task InputRejectsEmptySessionId()
    {
        var client = new LocalBridgeClient(CreateMissingManifestDirectory());

        var result = await AvaScopeMcpTools.Input(
            client,
            " ",
            "topLevel:abc",
            InputActions.Click,
            x: 1,
            y: 1);

        Assert.False(result.Success);
        Assert.Null(result.Value);
        Assert.Equal(CoreErrorCodes.InvalidBridgeRequest, result.Error!.Code);
    }

    [Fact]
    public async Task CloseSessionRejectsEmptySessionId()
    {
        var client = new LocalBridgeClient(CreateMissingManifestDirectory());

        var result = await AvaScopeMcpTools.CloseSession(client, " ");

        Assert.False(result.Success);
        Assert.Null(result.Value);
        Assert.Equal(CoreErrorCodes.InvalidBridgeRequest, result.Error!.Code);
    }

    [Fact]
    public async Task DiagnosticsReturnsStructuredIssueWhenNoBridgeSessionExists()
    {
        var client = new LocalBridgeClient(CreateMissingManifestDirectory());

        var result = await AvaScopeMcpTools.Diagnostics(client, sessionId: "session-missing");

        Assert.True(result.Success, result.Error?.Message);
        Assert.Null(result.Error);
        Assert.Equal("avascope", result.Value!.Service.ServiceName);
        Assert.Empty(result.Value.BridgeSessions);
        var issue = Assert.Single(result.Value.Issues);
        Assert.Equal(CoreErrorCodes.BridgeSessionNotFound, issue.Code);
    }

    [Fact]
    public async Task DiagnosticsRejectsInvalidSessionLimit()
    {
        var client = new LocalBridgeClient(CreateMissingManifestDirectory());

        var result = await AvaScopeMcpTools.Diagnostics(client, maxSessions: 0);

        Assert.False(result.Success);
        Assert.Null(result.Value);
        Assert.Equal(CoreErrorCodes.InvalidBridgeRequest, result.Error!.Code);
    }

    [Fact]
    public async Task PreviewAxamlRejectsInvalidDimensions()
    {
        var client = new PreviewHostClient(Path.Combine(AppContext.BaseDirectory, "AvaScope.PreviewHost.dll"));

        var result = await AvaScopeMcpTools.PreviewAxaml(
            client,
            "preview.png",
            width: 0,
            height: 100);

        Assert.False(result.Success);
        Assert.Null(result.Value);
        Assert.Equal(CoreErrorCodes.InvalidPreviewRequest, result.Error!.Code);
    }

    [Fact]
    public async Task PreviewAxamlRendersThroughPreviewHostClient()
    {
        var testRoot = Path.Combine(Path.GetTempPath(), "AvaScope.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(testRoot);

        var hostAssembly = Path.Combine(AppContext.BaseDirectory, "AvaScope.PreviewHost.dll");
        var viewPath = Path.Combine(testRoot, "McpPreview.axaml");
        var outputPath = Path.Combine(testRoot, "preview.png");

        await File.WriteAllTextAsync(viewPath, """
            <UserControl xmlns="https://github.com/avaloniaui">
              <Border Background="#FFFFFFFF">
                <TextBlock Text="MCP preview smoke" />
              </Border>
            </UserControl>
            """);

        try
        {
            var client = new PreviewHostClient(hostAssembly);

            var result = await AvaScopeMcpTools.PreviewAxaml(
                client,
                outputPath,
                width: 320,
                height: 180,
                dpi: 96,
                viewPath: viewPath,
                themeVariant: "light");

            Assert.True(result.Success, result.Error?.Message);
            Assert.Equal(Path.GetFullPath(outputPath), result.Value!.FilePath);
            Assert.Equal(320, result.Value.PixelWidth);
            Assert.Equal(180, result.Value.PixelHeight);
            Assert.True(File.Exists(result.Value.FilePath));
            Assert.True(new FileInfo(result.Value.FilePath).Length > 0);
        }
        finally
        {
            if (Directory.Exists(testRoot))
            {
                Directory.Delete(testRoot, recursive: true);
            }
        }
    }

    private static string CreateMissingManifestDirectory()
    {
        return Path.Combine(
            Path.GetTempPath(),
            "AvaScope.Tests",
            $"missing-manifests-{Guid.NewGuid():N}");
    }
}
