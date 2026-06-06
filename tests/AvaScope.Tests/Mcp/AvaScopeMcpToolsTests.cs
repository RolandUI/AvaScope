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

    private static string CreateMissingManifestDirectory()
    {
        return Path.Combine(
            Path.GetTempPath(),
            "AvaScope.Tests",
            $"missing-manifests-{Guid.NewGuid():N}");
    }
}
