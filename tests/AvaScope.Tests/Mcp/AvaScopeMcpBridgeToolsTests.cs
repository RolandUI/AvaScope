using AvaScope.Bridge;
using AvaScope.Core;
using AvaScope.Mcp;
using AvaScope.Protocol;
using AvaScope.Tests.Bridge;

namespace AvaScope.Tests.Mcp;

[Collection(BridgeCollectionDefinition.Name)]
public sealed class AvaScopeMcpBridgeToolsTests : IDisposable
{
    public AvaScopeMcpBridgeToolsTests()
    {
        AvaScopeBridge.Deactivate();
    }

    public void Dispose()
    {
        AvaScopeBridge.Deactivate();
    }

    [Fact]
    public async Task AttachToAppUsesLocalBridgeManifestAndPipeHealth()
    {
        var runtime = AvaScopeBridge.Activate(new BridgeActivationOptions("Sample app"));
        var client = new LocalBridgeClient(Path.GetDirectoryName(runtime.SessionManifestPath)!);

        var result = await AvaScopeMcpTools.AttachToApp(client, processId: Environment.ProcessId);

        Assert.True(result.Success, result.Error?.Message);
        Assert.Equal(runtime.SessionId, result.Value!.Session.SessionId);
        Assert.Equal(SessionKinds.Runtime, result.Value.Session.Kind);
        Assert.Equal(SessionStates.Active, result.Value.Session.State);
        Assert.Equal("Sample app", result.Value.Session.DisplayName);
        Assert.Equal(Environment.ProcessId, result.Value.ProcessId);
        Assert.Null(result.Error);
    }
}
