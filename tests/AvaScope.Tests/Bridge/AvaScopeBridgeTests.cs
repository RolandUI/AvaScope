using AvaScope.Bridge;
using AvaScope.Core;
using AvaScope.Protocol;

namespace AvaScope.Tests.Bridge;

[Collection(BridgeCollectionDefinition.Name)]
public sealed class AvaScopeBridgeTests : IDisposable
{
    public AvaScopeBridgeTests()
    {
        AvaScopeBridge.Deactivate();
    }

    public void Dispose()
    {
        AvaScopeBridge.Deactivate();
    }

    [Fact]
    public void BridgeIsInactiveByDefault()
    {
        Assert.False(AvaScopeBridge.IsActive);
        Assert.Null(AvaScopeBridge.Current);
    }

    [Fact]
    public void ActivateCreatesLocalOnlyRuntimeSession()
    {
        var registry = new SessionRegistry();

        var runtime = AvaScopeBridge.Activate(new BridgeActivationOptions("Sample app", sessionRegistry: registry));

        Assert.True(AvaScopeBridge.IsActive);
        Assert.Same(runtime, AvaScopeBridge.Current);
        Assert.Equal(BridgeTransportScope.LocalOnly, runtime.TransportScope);
        Assert.Equal(SessionKinds.Runtime, runtime.Session.Kind);
        Assert.Equal("Sample app", runtime.Session.DisplayName);
        Assert.Equal(SessionLifecycleState.Active, registry.Get(runtime.SessionId).Value!.State);
    }

    [Fact]
    public void ActivateIsIdempotentWhileBridgeIsActive()
    {
        var first = AvaScopeBridge.Activate(new BridgeActivationOptions("First"));
        var second = AvaScopeBridge.Activate(new BridgeActivationOptions("Second"));

        Assert.Same(first, second);
        Assert.Equal("First", second.Session.DisplayName);
    }

    [Fact]
    public void DeactivateClosesActiveRuntimeSession()
    {
        var registry = new SessionRegistry();
        var runtime = AvaScopeBridge.Activate(new BridgeActivationOptions("Sample app", sessionRegistry: registry));

        var result = AvaScopeBridge.Deactivate();

        Assert.True(result.Success);
        Assert.Equal(runtime.SessionId, result.Value!.Id);
        Assert.Equal(SessionLifecycleState.Closed, result.Value.State);
        Assert.False(AvaScopeBridge.IsActive);
        Assert.Equal(SessionLifecycleState.Closed, registry.Get(runtime.SessionId).Value!.State);
    }

    [Fact]
    public void DeactivateInactiveBridgeReturnsStructuredError()
    {
        var result = AvaScopeBridge.Deactivate();

        Assert.False(result.Success);
        Assert.Null(result.Value);
        Assert.Equal(BridgeErrorCodes.BridgeNotActive, result.Error!.Code);
    }

    [Fact]
    public void ActivationRejectsEmptySessionKind()
    {
        var exception = Assert.Throws<ArgumentException>(
            () => new BridgeActivationOptions(sessionKind: " "));

        Assert.Equal("sessionKind", exception.ParamName);
    }
}
