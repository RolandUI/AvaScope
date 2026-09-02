using System.IO.Pipes;
using System.Text;
using System.Text.Json;
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
        Assert.Null(BridgeActivationOptions.Default.DisplayName);
        Assert.Equal(SessionKinds.Runtime, BridgeActivationOptions.Default.SessionKind);
        Assert.Null(BridgeActivationOptions.Default.SessionRegistry);
        Assert.False(BridgeActivationOptions.Default.EnableCustomActions);
        Assert.Empty(BridgeActivationOptions.Default.AllowedCustomActions);
        Assert.False(BridgeActivationOptions.Default.AllowDestructiveCustomActions);
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
    public void ActivateCreatesAndDeactivateRemovesLocalSessionManifest()
    {
        var runtime = AvaScopeBridge.Activate(new BridgeActivationOptions("Sample app"));

        Assert.False(string.IsNullOrWhiteSpace(runtime.LocalPipeName));
        Assert.True(runtime.LocalPipeName!.Length <= 36);
        Assert.False(string.IsNullOrWhiteSpace(runtime.SessionManifestPath));
        Assert.True(File.Exists(runtime.SessionManifestPath));

        var manifestJson = File.ReadAllText(runtime.SessionManifestPath);
        var manifest = JsonSerializer.Deserialize<BridgeSessionManifest>(manifestJson);

        Assert.NotNull(manifest);
        Assert.Equal(runtime.SessionId, manifest.SessionId);
        Assert.Equal(Environment.ProcessId, manifest.ProcessId);
        Assert.Equal(runtime.LocalPipeName, manifest.PipeName);
        Assert.Equal(BridgeTransportScopes.LocalOnly, manifest.TransportScope);
        Assert.Equal("Sample app", manifest.DisplayName);

        var manifestPath = runtime.SessionManifestPath;
        var result = AvaScopeBridge.Deactivate();

        Assert.True(result.Success);
        Assert.False(File.Exists(manifestPath));
    }

    [Fact]
    public async Task LocalPipeServerRespondsToHealthRequest()
    {
        var runtime = AvaScopeBridge.Activate(new BridgeActivationOptions("Sample app"));

        var response = await SendBridgeRequestAsync(
            runtime.LocalPipeName!,
            new BridgeIpcRequest("request-1", BridgeIpcMethods.Health));

        Assert.Equal("request-1", response.RequestId);
        Assert.True(response.Success);
        Assert.Null(response.Error);
        Assert.Equal("avascope", response.GetValue<HealthResponse>()!.ServiceName);
    }

    [Fact]
    public async Task LocalPipeCloseSessionRespondsThenRemovesManifest()
    {
        var registry = new SessionRegistry();
        var runtime = AvaScopeBridge.Activate(new BridgeActivationOptions("Sample app", sessionRegistry: registry));
        var manifestPath = runtime.SessionManifestPath!;

        var response = await SendBridgeRequestAsync(
            runtime.LocalPipeName!,
            new BridgeIpcRequest("request-close", BridgeIpcMethods.CloseSession));

        Assert.Equal("request-close", response.RequestId);
        Assert.True(response.Success, response.Error?.Message);
        var close = response.GetValue<CloseSessionResponse>()!;
        Assert.Equal(runtime.SessionId, close.Session.SessionId);
        Assert.Equal(SessionStates.Closed, close.Session.State);
        Assert.Equal(Environment.ProcessId, close.ProcessId);
        Assert.Equal(SessionLifecycleState.Closed, registry.Get(runtime.SessionId).Value!.State);

        await WaitForAsync(() => !AvaScopeBridge.IsActive && !File.Exists(manifestPath));

        Assert.False(AvaScopeBridge.IsActive);
        Assert.False(File.Exists(manifestPath));
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

    private static async Task<BridgeIpcResponse> SendBridgeRequestAsync(
        string pipeName,
        BridgeIpcRequest request)
    {
        await using var pipe = new NamedPipeClientStream(
            ".",
            pipeName,
            PipeDirection.InOut,
            PipeOptions.Asynchronous);

        pipe.Connect(5000);

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var requestBytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(request) + Environment.NewLine);
        await pipe.WriteAsync(requestBytes, timeout.Token);
        await pipe.FlushAsync(timeout.Token);

        var responseBytes = new List<byte>();
        var buffer = new byte[1];

        while (true)
        {
            var read = await pipe.ReadAsync(buffer, timeout.Token);
            if (read == 0)
            {
                break;
            }

            if (buffer[0] == (byte)'\n')
            {
                break;
            }

            if (buffer[0] != (byte)'\r')
            {
                responseBytes.Add(buffer[0]);
            }
        }

        var line = Encoding.UTF8.GetString(responseBytes.ToArray());
        Assert.False(string.IsNullOrWhiteSpace(line));
        return JsonSerializer.Deserialize<BridgeIpcResponse>(line)!
            ?? throw new InvalidOperationException("Bridge IPC response was empty.");
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
