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
}
