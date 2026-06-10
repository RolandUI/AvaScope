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

        var result = await AvaScopeMcpTools.AttachToApp(client, sessionId: runtime.SessionId.Value);

        Assert.True(result.Success, result.Error?.Message);
        Assert.Equal(runtime.SessionId, result.Value!.Session.SessionId);
        Assert.Equal(SessionKinds.Runtime, result.Value.Session.Kind);
        Assert.Equal(SessionStates.Active, result.Value.Session.State);
        Assert.Equal("Sample app", result.Value.Session.DisplayName);
        Assert.Equal(Environment.ProcessId, result.Value.ProcessId);
        Assert.Null(result.Error);
    }

    [Fact]
    public async Task AttachToAppUsesProcessNameAndManifestDirectory()
    {
        var runtime = AvaScopeBridge.Activate(new BridgeActivationOptions("Sample app"));
        var manifestDirectory = Path.GetDirectoryName(runtime.SessionManifestPath)!;
        var client = new LocalBridgeClient(Path.Combine(manifestDirectory, "missing"));

        var result = await AvaScopeMcpTools.AttachToApp(
            client,
            processName: Environment.ProcessPath is { } processPath
                ? Path.GetFileNameWithoutExtension(processPath)
                : null,
            manifestDirectory: manifestDirectory);

        Assert.True(result.Success, result.Error?.Message);
        Assert.Equal(runtime.SessionId, result.Value!.Session.SessionId);
        Assert.Equal(Environment.ProcessId, result.Value.ProcessId);
        Assert.Equal(Path.GetFullPath(runtime.SessionManifestPath!), result.Value.ManifestPath);
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

    [Fact]
    public async Task DiagnosticsUsesLocalBridgeManifestAndPipeHealth()
    {
        var runtime = AvaScopeBridge.Activate(new BridgeActivationOptions("Sample app"));
        var manifestPath = runtime.SessionManifestPath!;
        var client = new LocalBridgeClient(Path.GetDirectoryName(manifestPath)!);
        var previewHostClient = new PreviewHostClient(Path.Combine(AppContext.BaseDirectory, "AvaScope.PreviewHost.dll"));
        var previewSessionStore = new PreviewSessionStore(Path.Combine(
            Path.GetTempPath(),
            "AvaScope.Tests",
            $"mcp-bridge-preview-sessions-{Guid.NewGuid():N}"));

        var result = await AvaScopeMcpTools.Diagnostics(
            client,
            previewHostClient,
            previewSessionStore,
            sessionId: runtime.SessionId.Value);

        Assert.True(result.Success, result.Error?.Message);
        Assert.Null(result.Error);
        Assert.Equal("avascope", result.Value!.Service.ServiceName);
        Assert.Equal(DiagnosticStatuses.Available, result.Value.PreviewHost!.Status);
        Assert.Empty(result.Value.Issues);
        Assert.Empty(result.Value.DiagnosticIssues);
        var bridge = Assert.Single(result.Value.BridgeSessions);
        Assert.Equal(DiagnosticStatuses.Available, bridge.Status);
        Assert.Equal(Path.GetFullPath(manifestPath), bridge.ManifestPath);
        Assert.Equal(runtime.SessionId, bridge.Session!.SessionId);
        Assert.Equal(SessionStates.Active, bridge.Session.State);
        Assert.Equal("Sample app", bridge.Session.DisplayName);
        Assert.Equal(Environment.ProcessId, bridge.ProcessId);
        Assert.Equal(DiagnosticTransportKinds.NamedPipe, bridge.Transport);
        Assert.Equal(runtime.LocalPipeName, bridge.PipeName);
        Assert.Equal("avascope", bridge.Health!.ServiceName);
        Assert.Null(bridge.Error);
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
