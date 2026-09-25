using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using AvaScope.Bridge;
using AvaScope.Core;
using AvaScope.Mcp;
using AvaScope.Protocol;

namespace AvaScope.Tests.Bridge;

[Collection(BridgeCollectionDefinition.Name)]
public sealed class RuntimeProvenanceTests
{
    [Fact]
    public async Task ObservedBackendAndActualRoutesSurviveIpcAndWorkflowEvidence()
    {
        using var session = HeadlessUnitTestSession.StartNew(typeof(BridgeHeadlessSmokeTests.BridgeHeadlessTestApplication));
        var output = Path.Combine(Path.GetTempPath(), "AvaScope.Tests", Guid.NewGuid().ToString("N"));
        try
        {
            await BridgeHeadlessSmokeTests.DispatchAsync(session, async () =>
            {
                AvaScopeBridge.Deactivate();
                var runtime = AvaScopeBridge.Activate(new BridgeActivationOptions("Route evidence"));
                var clicked = 0;
                var button = new Button { Name = "Action", Content = "Apply", Width = 100, Height = 40 };
                button.Click += (_, _) => clicked++;
                var textBox = new TextBox { Name = "Editor", Text = "start" };
                var window = new Window
                {
                    Width = 320, Height = 220,
                    Content = new StackPanel { Children = { button, textBox } }
                };
                try
                {
                    Assert.Empty(runtime.GetCapabilities().Backends);
                    window.Show();
                    using var registration = runtime.RegisterTopLevel(window);
                    Dispatcher.UIThread.RunJobs();
                    var top = Assert.Single(await runtime.ListTopLevelsAsync());
                    using var preparedFrame = window.CaptureRenderedFrame();
                    Assert.NotNull(preparedFrame);
                    Assert.Equal(new PixelSize(320, 220), preparedFrame.PixelSize);
                    Assert.True((await runtime.ReadinessAsync(top.Id, options: new(waitForFrame: true))).Success);
                    Assert.Equal("headless", top.Backend!.Backend);
                    Assert.Equal("STUB", top.Backend.HandleDescriptor);
                    Assert.Equal("unknown", top.Backend.RenderMode);
                    Assert.Contains("native_os_input_unavailable", top.Backend.Restrictions);
                    var client = new LocalBridgeClient(Path.GetDirectoryName(runtime.SessionManifestPath)!);
                    var capabilities = await client.SessionCapabilitiesAsync(runtime.SessionId);
                    Assert.True(capabilities.Success, capabilities.Error?.Message);
                    Assert.Equal("headless", Assert.Single(capabilities.Value!.Backends).Backend);
                    Assert.False(capabilities.Value.NativePickerSupported);

                    async Task<string> Node(string name)
                    {
                        var result = await runtime.FindNodesAsync(top.Id, TreeKinds.Visual, name: name);
                        Assert.True(result.Success, result.Error?.Message);
                        return Assert.Single(result.Value!.Matches).Node.NodeId;
                    }
                    var buttonId = await Node("Action");
                    var textId = await Node("Editor");
                    AssertRoute(await runtime.InputAsync(top.Id, InputActions.Invoke, targetNodeId: buttonId), RuntimeOperationRoutes.AutomationProvider);
                    AssertRoute(await runtime.InputAsync(top.Id, InputActions.Click, targetNodeId: buttonId), RuntimeOperationRoutes.RoutedEvent);
                    Assert.Equal(2, clicked);
                    AssertRoute(await runtime.InputAsync(top.Id, InputActions.Focus, targetNodeId: textId), RuntimeOperationRoutes.Focus);
                    AssertRoute(await runtime.InputAsync(top.Id, InputActions.KeyText, targetNodeId: textId, inputText: "new"), RuntimeOperationRoutes.ControlProperty);
                    AssertRoute(await runtime.InputAsync(top.Id, InputActions.KeyDown, targetNodeId: textId, inputKey: "Left"), RuntimeOperationRoutes.SyntheticKey);
                    var entered = 0;
                    var exited = 0;
                    window.PointerEntered += (_, _) => entered++;
                    window.PointerExited += (_, _) => exited++;
                    AssertRoute(await runtime.InputAsync(top.Id, InputActions.PointerMove, x: 10, y: 10), RuntimeOperationRoutes.SyntheticPointer);
                    Assert.True(window.IsPointerOver);
                    Assert.Equal(1, entered);
                    var exit = await runtime.InputAsync(top.Id, InputActions.PointerMove, x: -100, y: -100);
                    AssertRoute(exit, RuntimeOperationRoutes.SyntheticPointer);
                    Assert.True(exit.Value!.Handled);
                    Assert.False(window.IsPointerOver);
                    Assert.Equal(1, exited);
                    // Once owned hover is cleared, another outside move really dispatches nothing.
                    var miss = await runtime.InputAsync(top.Id, InputActions.PointerMove, x: -100, y: -100);
                    AssertRoute(miss, RuntimeOperationRoutes.NotDispatched, dispatched: false);
                    Assert.False(miss.Value!.Handled);
                    Assert.Equal(1, exited);
                    Assert.Equal(RuntimeOperationRoutes.SyntheticPointer, miss.Value!.Provenance!.PlannedRoute);
                    var dry = await runtime.ValidateInputAsync(top.Id, InputActions.Invoke, targetNodeId: buttonId);
                    AssertRoute(dry, RuntimeOperationRoutes.NotDispatched, dispatched: false);
                    Assert.Equal(RuntimeOperationRoutes.AutomationProvider, dry.Value!.Provenance!.PlannedRoute);
                    Assert.Equal(2, clicked);

                    var workflow = await AvaScopeMcpTools.RunWorkflow(client, new SemanticWorkflowRequest(
                        runtime.SessionId, top.Id,
                        [new SemanticWorkflowStep(SemanticWorkflowActions.Invoke, selector: new SemanticWorkflowSelector(name: "Action")),
                            new SemanticWorkflowStep(SemanticWorkflowActions.Screenshot)], outputDirectory: output,
                        evidence: new SemanticWorkflowEvidenceOptions(exportReports: true)));
                    Assert.True(workflow.Success, workflow.Error?.Message);
                    Assert.Equal("passed", workflow.Value!.Status);
                    Assert.Equal(RuntimeOperationRoutes.AutomationProvider, workflow.Value.Steps[0].Input!.Provenance!.Route);
                    var screenshot = workflow.Value.Steps[1].Screenshot!;
                    Assert.Equal(RuntimeOperationRoutes.RenderTargetBitmap, screenshot.Provenance!.Route);
                    Assert.Equal("top_level_pixel", screenshot.Provenance.CoordinateSpace);
                    Assert.Equal(window.RenderScaling, screenshot.Provenance.RenderScaling);
                    Assert.Null(screenshot.Provenance.ScreenOriginX);
                    var json = JsonSerializer.Serialize(workflow.Value);
                    var roundTrip = JsonSerializer.Deserialize<SemanticWorkflowResponse>(json)!;
                    Assert.Equal(json, JsonSerializer.Serialize(roundTrip));
                    Assert.Contains(RuntimeOperationRoutes.RenderTargetBitmap,
                        string.Join("\n", Directory.GetFiles(output, "*.json", SearchOption.AllDirectories).Select(File.ReadAllText)), StringComparison.Ordinal);

                    registration.Dispose();
                    Assert.Empty(runtime.GetCapabilities().Backends);
                    window.Close();
                    using var closedRegistration = runtime.RegisterTopLevel(window);
                    var unknown = Assert.Single(runtime.GetCapabilities().Backends);
                    Assert.Equal("unknown", unknown.Backend);
                    Assert.Null(unknown.ImplementationType);
                    Assert.Contains("platform_implementation_unrecognized_or_unavailable", unknown.Restrictions);
                }
                finally
                {
                    window.Close();
                    AvaScopeBridge.Deactivate();
                }
            }, CancellationToken.None);
        }
        finally
        {
            if (Directory.Exists(output)) Directory.Delete(output, recursive: true);
        }
    }

    [Fact]
    public void LegacyPayloadsStayReadableAndRevisionTracksCoverage()
    {
        const string input = """{"sessionId":"session-a","topLevelId":"top","action":"invoke","handled":true,"executedAt":"2026-09-22T00:00:00Z"}""";
        Assert.Null(JsonSerializer.Deserialize<InputResponse>(input)!.Provenance);
        var id = new SessionId("session-a");
        var unknown = SessionCapabilitiesResponse.Current(id, 1);
        Assert.Empty(unknown.Backends);
        Assert.False(unknown.NativePickerSupported);
        var backend = new RuntimeBackendInfo("headless", "linux", "top_level_platform_implementation", null, "STUB",
            "unknown", [RuntimeOperationRoutes.SyntheticPointer], [RuntimeOperationRoutes.RenderTargetBitmap], ["native_os_input_unavailable"]);
        var first = SessionCapabilitiesResponse.Current(id, 1, backends: [backend]);
        var second = SessionCapabilitiesResponse.Current(new SessionId("other"), 2, backends: [backend, backend]);
        Assert.Equal(first.Revision, second.Revision);
        Assert.NotEqual(unknown.Revision, first.Revision);
        Assert.NotEqual(first.Revision, SessionCapabilitiesResponse.Current(id, 1, backends: [backend with { Backend = "x11" }]).Revision);
        var json = JsonSerializer.Serialize(first);
        Assert.Equal(json, JsonSerializer.Serialize(JsonSerializer.Deserialize<SessionCapabilitiesResponse>(json)));
    }

    private static void AssertRoute(CoreResult<InputResponse> result, string route, bool dispatched = true)
    {
        Assert.True(result.Success, result.Error?.Message);
        var provenance = Assert.IsType<RuntimeOperationProvenance>(result.Value!.Provenance);
        Assert.Equal(route, provenance.Route);
        Assert.Equal("headless", provenance.Backend.Backend);
        Assert.Equal(dispatched, provenance.Dispatched);
        Assert.False(provenance.Fallback);
    }
}
