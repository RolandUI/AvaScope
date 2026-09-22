using System.Diagnostics;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Headless;
using AvaScope.Bridge;
using AvaScope.Core;
using AvaScope.Mcp;
using AvaScope.Protocol;

namespace AvaScope.Tests.Bridge;

[Collection(BridgeCollectionDefinition.Name)]
public sealed class SessionControlTests
{
    [Fact]
    public async Task McpLeaseCoordinatesCliAndRawBridgeControlWithoutBlockingObservation()
    {
        var session = HeadlessUnitTestSession.StartNew(typeof(BridgeHeadlessSmokeTests.BridgeHeadlessTestApplication));
        try
        {
            await session.Dispatch(async () =>
            {
                AvaScopeBridge.Deactivate();
                var runtime = AvaScopeBridge.Activate();
                var pressed = 0;
                var button = new Button { Name = "Action", Content = "Action" };
                button.Click += (_, _) => pressed++;
                var window = new Window { Width = 220, Height = 100, Content = button };
                try
                {
                    window.Show();
                    using var registration = runtime.RegisterTopLevel(window);
                    var top = Assert.Single(await runtime.ListTopLevelsAsync()).Id;
                    var node = Assert.Single((await runtime.FindNodesAsync(top, TreeKinds.Visual, name: "Action")).Value!.Matches).Node.NodeId;
                    var client = new LocalBridgeClient(Path.GetDirectoryName(runtime.SessionManifestPath));
                    var acquired = await AvaScopeMcpTools.SessionControl(client, runtime.SessionId.Value, new("acquire", "agent-one"));
                    Assert.True(acquired.Success, acquired.Error?.Message);
                    var token = acquired.Value!.Token!;
                    var outsider = new LocalBridgeClient(client.ManifestDirectory);
                    Assert.True((await outsider.ListTopLevelsAsync(runtime.SessionId)).Success);
                    Assert.Equal("session_control_conflict", (await outsider.CloseSessionAsync(runtime.SessionId)).Error!.Code);
                    var manifest = client.ListSessionManifests().Single(m => m.SessionId == runtime.SessionId);
                    foreach (var method in new[] { BridgeIpcMethods.Input, BridgeIpcMethods.MutateNode, BridgeIpcMethods.InvokeCustomAction,
                        BridgeIpcMethods.VirtualItem, BridgeIpcMethods.NativePicker, BridgeIpcMethods.EnsureState, BridgeIpcMethods.FillForm })
                    {
                        await using var pipe = new NamedPipeClientStream(".", manifest.PipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
                        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                        await pipe.ConnectAsync(timeout.Token);
                        var request = new BridgeIpcRequest(Guid.NewGuid().ToString("N"), method);
                        await pipe.WriteAsync(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(request) + "\n"), timeout.Token);
                        using var reader = new StreamReader(pipe, leaveOpen: true);
                        var refused = JsonSerializer.Deserialize<BridgeIpcResponse>((await reader.ReadLineAsync(timeout.Token))!)!;
                        Assert.Equal("session_control_conflict", refused.Error!.Code);
                    }
                    var selected = client.WithManifestDirectory(client.ManifestDirectory);
                    Assert.True((await selected.InputAsync(runtime.SessionId, top, InputActions.Invoke, targetNodeId: node)).Success);
                    Assert.Equal(1, pressed);
                    var start = new ProcessStartInfo("dotnet") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
                    foreach (var arg in new[] { Path.Combine(AppContext.BaseDirectory, "avascope.dll"), "input", "--session", runtime.SessionId.Value,
                        "--top-level", top, "--action", "invoke", "--target-node", node, "--manifest-dir", client.ManifestDirectory, "--control-token", token }) start.ArgumentList.Add(arg);
                    using var cli = Process.Start(start)!;
                    var output = cli.StandardOutput.ReadToEndAsync();
                    var error = cli.StandardError.ReadToEndAsync();
                    await cli.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(15));
                    var result = JsonSerializer.Deserialize<ToolResult<InputResponse>>(await output)!;
                    Assert.True(result.Success, result.Error?.Message + await error);
                    Assert.Equal(2, pressed);
                    Assert.True((await client.SessionControlAsync(runtime.SessionId, new("release"))).Success);
                    Assert.True((await outsider.InputAsync(runtime.SessionId, top, InputActions.Invoke, targetNodeId: node)).Success);
                    Assert.Equal(3, pressed);
                }
                finally { window.Close(); AvaScopeBridge.Deactivate(); }
            }, CancellationToken.None);
        }
        finally { BridgeHeadlessSmokeTests.DisposeHeadlessSessionAfterExplicitCleanup(session); }
    }
}
