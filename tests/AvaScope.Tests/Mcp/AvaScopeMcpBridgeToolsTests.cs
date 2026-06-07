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

    [Fact]
    public async Task CloseSessionUsesLocalBridgeManifestAndPipeHandshake()
    {
        var runtime = AvaScopeBridge.Activate(new BridgeActivationOptions("Sample app"));
        var manifestPath = runtime.SessionManifestPath!;
        var client = new LocalBridgeClient(Path.GetDirectoryName(manifestPath)!);

        var result = await AvaScopeMcpTools.CloseSession(client, runtime.SessionId.Value);

        Assert.True(result.Success, result.Error?.Message);
        Assert.Equal(runtime.SessionId, result.Value!.Session.SessionId);
        Assert.Equal(SessionStates.Closed, result.Value.Session.State);
        Assert.Equal(Environment.ProcessId, result.Value.ProcessId);

        await WaitForAsync(() => !AvaScopeBridge.IsActive && !File.Exists(manifestPath));

        Assert.False(AvaScopeBridge.IsActive);
        Assert.False(File.Exists(manifestPath));
    }

    private static async Task WaitForAsync(Func<bool> predicate)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        while (!predicate())
        {
            if (timeout.IsCancellationRequested)
            {
                Assert.True(predicate(), "Timed out waiting for bridge close to complete.");
            }

            await Task.Delay(20, CancellationToken.None);
        }
    }
}
