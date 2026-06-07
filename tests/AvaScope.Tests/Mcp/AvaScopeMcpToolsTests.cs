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
    public async Task InspectNodeRejectsEmptySessionId()
    {
        var client = new LocalBridgeClient(CreateMissingManifestDirectory());

        var result = await AvaScopeMcpTools.InspectNode(
            client,
            " ",
            "topLevel:abc",
            "visual:button");

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
        var previewHostClient = CreatePreviewHostClient();

        var result = await AvaScopeMcpTools.Diagnostics(client, previewHostClient, sessionId: "session-missing");

        Assert.True(result.Success, result.Error?.Message);
        Assert.Null(result.Error);
        Assert.Equal("avascope", result.Value!.Service.ServiceName);
        Assert.Equal(DiagnosticStatuses.Available, result.Value.PreviewHost!.Status);
        Assert.Equal(DiagnosticProcessModes.IsolatedChildProcess, result.Value.PreviewHost.ProcessMode);
        Assert.Empty(result.Value.BridgeSessions);
        var issue = Assert.Single(result.Value.Issues);
        Assert.Equal(CoreErrorCodes.BridgeSessionNotFound, issue.Code);
    }

    [Fact]
    public async Task DiagnosticsRejectsInvalidSessionLimit()
    {
        var client = new LocalBridgeClient(CreateMissingManifestDirectory());
        var previewHostClient = CreatePreviewHostClient();

        var result = await AvaScopeMcpTools.Diagnostics(client, previewHostClient, maxSessions: 0);

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
    public async Task CreatePreviewSessionRejectsInvalidDimensions()
    {
        var previewSessions = CreatePreviewSessionRegistryWithMissingHost();

        var result = await AvaScopeMcpTools.CreatePreviewSession(
            previewSessions,
            "preview.png",
            width: 0,
            height: 100);

        Assert.False(result.Success);
        Assert.Null(result.Value);
        Assert.Equal(CoreErrorCodes.InvalidPreviewRequest, result.Error!.Code);
    }

    [Fact]
    public async Task PreviewSessionToolsCreateListAndCloseSessionRecord()
    {
        var sessionRegistry = new SessionRegistry();
        var previewSessions = CreatePreviewSessionRegistryWithMissingHost(sessionRegistry);

        var created = await AvaScopeMcpTools.CreatePreviewSession(
            previewSessions,
            Path.Combine(Path.GetTempPath(), "AvaScope.Tests", "missing-preview.png"),
            width: 120,
            height: 80,
            displayName: "Broken preview");

        Assert.True(created.Success, created.Error?.Message);
        Assert.Equal(SessionKinds.Preview, created.Value!.Session.Kind);
        Assert.Equal(SessionStates.Failed, created.Value.Session.State);
        Assert.Equal("Broken preview", created.Value.Session.DisplayName);
        Assert.False(created.Value.LastRender.Success);
        Assert.Equal(CoreErrorCodes.PreviewHostUnavailable, created.Value.LastRender.Error!.Code);

        var reloaded = await AvaScopeMcpTools.Reload(
            previewSessions,
            new LocalBridgeClient(CreateMissingManifestDirectory()),
            created.Value.Session.SessionId.Value);

        Assert.True(reloaded.Success, reloaded.Error?.Message);
        Assert.Equal(created.Value.Session.SessionId, reloaded.Value!.Session.SessionId);
        Assert.Equal(SessionStates.Failed, reloaded.Value.Session.State);
        Assert.False(reloaded.Value.LastRender.Success);
        Assert.Equal(CoreErrorCodes.PreviewHostUnavailable, reloaded.Value.LastRender.Error!.Code);

        var listed = AvaScopeMcpTools.ListPreviewSessions(previewSessions);

        Assert.True(listed.Success);
        var preview = Assert.Single(listed.Value!.Sessions);
        Assert.Equal(created.Value.Session.SessionId, preview.Session.SessionId);

        var allSessions = AvaScopeMcpTools.ListSessions(sessionRegistry);

        Assert.Contains(
            allSessions.Value!.Sessions,
            session => session.SessionId == created.Value.Session.SessionId
                && session.Kind == SessionKinds.Preview
                && session.State == SessionStates.Failed);

        var closed = AvaScopeMcpTools.ClosePreviewSession(previewSessions, created.Value.Session.SessionId.Value);

        Assert.True(closed.Success, closed.Error?.Message);
        Assert.Equal(SessionStates.Closed, closed.Value!.Session.State);
    }

    [Fact]
    public void ClosePreviewSessionRejectsEmptySessionId()
    {
        var previewSessions = CreatePreviewSessionRegistryWithMissingHost();

        var result = AvaScopeMcpTools.ClosePreviewSession(previewSessions, " ");

        Assert.False(result.Success);
        Assert.Null(result.Value);
        Assert.Equal(CoreErrorCodes.InvalidBridgeRequest, result.Error!.Code);
    }

    [Fact]
    public async Task ReloadRejectsClosedPreviewSession()
    {
        var previewSessions = CreatePreviewSessionRegistryWithMissingHost();
        var created = await AvaScopeMcpTools.CreatePreviewSession(
            previewSessions,
            Path.Combine(Path.GetTempPath(), "AvaScope.Tests", "missing-preview.png"),
            width: 120,
            height: 80);
        AvaScopeMcpTools.ClosePreviewSession(previewSessions, created.Value!.Session.SessionId.Value);

        var result = await AvaScopeMcpTools.Reload(
            previewSessions,
            new LocalBridgeClient(CreateMissingManifestDirectory()),
            created.Value.Session.SessionId.Value);

        Assert.False(result.Success);
        Assert.Null(result.Value);
        Assert.Equal(CoreErrorCodes.SessionClosed, result.Error!.Code);
    }

    [Fact]
    public async Task ReloadRejectsEmptySessionId()
    {
        var previewSessions = CreatePreviewSessionRegistryWithMissingHost();

        var result = await AvaScopeMcpTools.Reload(
            previewSessions,
            new LocalBridgeClient(CreateMissingManifestDirectory()),
            " ");

        Assert.False(result.Success);
        Assert.Null(result.Value);
        Assert.Equal(CoreErrorCodes.InvalidBridgeRequest, result.Error!.Code);
    }

    [Fact]
    public async Task ReloadPreservesPreviewSessionNotFoundWhenNoRuntimeBridgeMatches()
    {
        var previewSessions = CreatePreviewSessionRegistryWithMissingHost();
        var client = new LocalBridgeClient(CreateMissingManifestDirectory());

        var result = await AvaScopeMcpTools.Reload(previewSessions, client, "session-missing");

        Assert.False(result.Success);
        Assert.Null(result.Value);
        Assert.Equal(CoreErrorCodes.SessionNotFound, result.Error!.Code);
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
                themeVariant: "light",
                culture: "ja-JP");

            Assert.True(result.Success, result.Error?.Message);
            Assert.Equal(Path.GetFullPath(outputPath), result.Value!.FilePath);
            Assert.Equal(320, result.Value.PixelWidth);
            Assert.Equal(180, result.Value.PixelHeight);
            Assert.Equal("ja-JP", result.Value.Culture);
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

    private static PreviewHostClient CreatePreviewHostClient()
    {
        return new PreviewHostClient(Path.Combine(AppContext.BaseDirectory, "AvaScope.PreviewHost.dll"));
    }

    private static PreviewSessionRegistry CreatePreviewSessionRegistryWithMissingHost(
        SessionRegistry? sessionRegistry = null)
    {
        return new PreviewSessionRegistry(
            sessionRegistry ?? new SessionRegistry(),
            new PreviewHostClient(Path.Combine(
                Path.GetTempPath(),
                "AvaScope.Tests",
                $"missing-preview-host-{Guid.NewGuid():N}.dll")));
    }
}
