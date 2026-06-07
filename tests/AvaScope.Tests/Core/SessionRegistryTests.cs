using AvaScope.Core;
using AvaScope.Protocol;

namespace AvaScope.Tests.Core;

public sealed class SessionRegistryTests
{
    [Fact]
    public void CreateReturnsActiveSessionSnapshot()
    {
        var createdAt = new DateTimeOffset(2026, 6, 6, 20, 0, 0, TimeSpan.Zero);
        var registry = new SessionRegistry(new ManualTimeProvider(createdAt));

        var session = registry.Create(SessionKinds.Runtime, "Sample app");

        Assert.NotEmpty(session.Id.Value);
        Assert.Equal(SessionKinds.Runtime, session.Kind);
        Assert.Equal(SessionLifecycleState.Active, session.State);
        Assert.Equal(createdAt, session.CreatedAt);
        Assert.Equal("Sample app", session.DisplayName);
        Assert.Null(session.LastError);
    }

    [Fact]
    public void ListReturnsCreatedSessionsInCreationOrder()
    {
        var timeProvider = new ManualTimeProvider(new DateTimeOffset(2026, 6, 6, 20, 0, 0, TimeSpan.Zero));
        var registry = new SessionRegistry(timeProvider);

        var first = registry.Create(SessionKinds.Runtime);
        timeProvider.UtcNow = timeProvider.UtcNow.AddMinutes(1);
        var second = registry.Create(SessionKinds.Preview);

        var sessions = registry.List();

        Assert.Collection(
            sessions,
            session => Assert.Equal(first.Id, session.Id),
            session => Assert.Equal(second.Id, session.Id));
    }

    [Fact]
    public void GetReturnsExistingSession()
    {
        var registry = new SessionRegistry();
        var session = registry.Create(SessionKinds.Runtime);

        var result = registry.Get(session.Id);

        Assert.True(result.Success);
        Assert.Equal(session.Id, result.Value!.Id);
        Assert.Null(result.Error);
    }

    [Fact]
    public void GetUnknownSessionReturnsStructuredError()
    {
        var registry = new SessionRegistry();

        var result = registry.Get(new SessionId("missing"));

        Assert.False(result.Success);
        Assert.Null(result.Value);
        Assert.Equal(CoreErrorCodes.SessionNotFound, result.Error!.Code);
        Assert.Equal("Session 'missing' was not found.", result.Error.Message);
    }

    [Fact]
    public void CloseTransitionsSessionToClosed()
    {
        var registry = new SessionRegistry();
        var session = registry.Create(SessionKinds.Runtime);

        var result = registry.Close(session.Id);

        Assert.True(result.Success);
        Assert.Equal(SessionLifecycleState.Closed, result.Value!.State);
        Assert.Equal(SessionLifecycleState.Closed, registry.Get(session.Id).Value!.State);
    }

    [Fact]
    public void CloseUnknownSessionReturnsStructuredError()
    {
        var registry = new SessionRegistry();

        var result = registry.Close(new SessionId("missing"));

        Assert.False(result.Success);
        Assert.Equal(CoreErrorCodes.SessionNotFound, result.Error!.Code);
    }

    [Fact]
    public void MarkFailedStoresErrorAndFailedState()
    {
        var registry = new SessionRegistry();
        var session = registry.Create(SessionKinds.Runtime);
        var error = new CoreError("bridge_disconnected", "Bridge disconnected.");

        var result = registry.MarkFailed(session.Id, error);

        Assert.True(result.Success);
        Assert.Equal(SessionLifecycleState.Failed, result.Value!.State);
        Assert.Equal(error, result.Value.LastError);
    }

    [Fact]
    public void MarkActiveRestoresFailedSession()
    {
        var registry = new SessionRegistry();
        var session = registry.Create(SessionKinds.Preview);
        registry.MarkFailed(session.Id, new CoreError("preview_failed", "Preview failed."));

        var result = registry.MarkActive(session.Id);

        Assert.True(result.Success);
        Assert.Equal(SessionLifecycleState.Active, result.Value!.State);
        Assert.Null(result.Value.LastError);
    }

    [Fact]
    public void CreateRejectsEmptySessionKind()
    {
        var registry = new SessionRegistry();

        var exception = Assert.Throws<ArgumentException>(() => registry.Create(" "));

        Assert.Equal("kind", exception.ParamName);
    }

    private sealed class ManualTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public DateTimeOffset UtcNow { get; set; } = utcNow;

        public override DateTimeOffset GetUtcNow() => UtcNow;
    }
}
