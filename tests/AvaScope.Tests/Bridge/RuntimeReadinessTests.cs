using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Media;
using Avalonia.Threading;
using AvaScope.Bridge;
using AvaScope.Core;
using AvaScope.Mcp;
using AvaScope.Protocol;

namespace AvaScope.Tests.Bridge;

[Collection(BridgeCollectionDefinition.Name)]
public sealed class RuntimeReadinessTests
{
    [Fact]
    public async Task MissingHookIsDistinctFromBusyAndAsyncStartupThroughIpcAndMcp()
    {
        using var session = HeadlessUnitTestSession.StartNew(typeof(BridgeHeadlessSmokeTests.BridgeHeadlessTestApplication));
        await BridgeHeadlessSmokeTests.DispatchAsync(session, async () =>
        {
            AvaScopeBridge.Deactivate();
            var runtime = AvaScopeBridge.Activate(new BridgeActivationOptions("Readiness"));
            var window = new Window { Width = 220, Height = 160, Content = new Border() };
            try
            {
                window.Show();
                using var registration = runtime.RegisterTopLevel(window);
                var top = Assert.Single(await runtime.ListTopLevelsAsync());
                var client = new LocalBridgeClient(Path.GetDirectoryName(runtime.SessionManifestPath)!);
                var initial = await client.ReadinessAsync(runtime.SessionId, top.Id);
                Assert.True(initial.Success, initial.Error?.Message);
                Assert.Equal("available", initial.Value!.Bridge.Status);
                Assert.Equal("created", initial.Value.Window.Status);
                Assert.Equal("not_sampled", initial.Value.Frame.Status);
                Assert.Equal("unavailable", initial.Value.Application.Status);
                var absent = await Wait(client, runtime.SessionId, top.Id, SemanticWaitConditionKinds.ApplicationReady, timeoutMs: 120);
                Assert.Equal("semantic_workflow_wait_state_unavailable", Assert.Single(absent.Diagnostics).Code);
                Assert.Equal("unavailable", absent.WaitObservation!.Availability);

                runtime.SetReadiness("busy", "Loading local fixture");
                var busy = await Wait(client, runtime.SessionId, top.Id, SemanticWaitConditionKinds.ApplicationBusy);
                Assert.Equal("passed", busy.Status);
                Assert.Equal("Loading local fixture", busy.WaitObservation!.Readiness!.Application.Reason);
                var falseCondition = await Wait(client, runtime.SessionId, top.Id, SemanticWaitConditionKinds.ApplicationReady, timeoutMs: 120);
                Assert.Equal("semantic_workflow_wait_timeout", Assert.Single(falseCondition.Diagnostics).Code);
                Assert.Equal("available", falseCondition.WaitObservation!.Availability);

                runtime.SetReadiness("starting");
                var loading = Wait(client, runtime.SessionId, top.Id, SemanticWaitConditionKinds.ApplicationReady);
                runtime.SetReadiness("ready", "Fixture data loaded");
                Assert.Equal("passed", (await loading).Status);
                runtime.SetReadiness("failed", "Fixture rejected");
                Assert.Equal("failed", (await client.ReadinessAsync(runtime.SessionId, top.Id)).Value!.Application.Status);
                Assert.Throws<ArgumentException>(() => runtime.SetReadiness("guess"));
                using var cancelled = new CancellationTokenSource();
                cancelled.Cancel();
                await Assert.ThrowsAnyAsync<OperationCanceledException>(() => runtime.ReadinessAsync(top.Id, cancellationToken: cancelled.Token));
                using var stopWait = new CancellationTokenSource(TimeSpan.FromMilliseconds(80));
                await Assert.ThrowsAnyAsync<OperationCanceledException>(() => AvaScopeMcpTools.RunWorkflow(client,
                    new SemanticWorkflowRequest(runtime.SessionId, top.Id,
                        [new SemanticWorkflowStep(SemanticWorkflowActions.WaitForState,
                            waitCondition: new SemanticWaitCondition(SemanticWaitConditionKinds.ApplicationReady))]),
                    cancellationToken: stopWait.Token));
            }
            finally
            {
                window.Close();
                AvaScopeBridge.Deactivate();
            }
        }, CancellationToken.None);
    }

    [Fact]
    public async Task RenderFenceAndScopedStabilityDoNotRequireGlobalIdle()
    {
        using var session = HeadlessUnitTestSession.StartNew(typeof(BridgeHeadlessSmokeTests.BridgeHeadlessTestApplication));
        var output = Path.Combine(Path.GetTempPath(), "AvaScope.Tests", Guid.NewGuid().ToString("N"));
        try
        {
            await BridgeHeadlessSmokeTests.DispatchAsync(session, async () =>
            {
                AvaScopeBridge.Deactivate();
                var runtime = AvaScopeBridge.Activate(new BridgeActivationOptions("Frame readiness"));
                var stable = new Border { Name = "Stable", Width = 60, Height = 40, Background = Brushes.Green };
                var animated = new Border { Name = "Animated", Width = 60, Height = 40, Background = Brushes.Red };
                var window = new Window { Width = 240, Height = 180, Content = new StackPanel { Children = { stable, animated } } };
                var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(12) };
                var ticks = 0;
                timer.Tick += (_, _) => { animated.Background = new SolidColorBrush(Color.FromRgb((byte)++ticks, 50, 80)); };
                try
                {
                    window.Show();
                    using var registration = runtime.RegisterTopLevel(window);
                    var top = Assert.Single(await runtime.ListTopLevelsAsync());
                    var client = new LocalBridgeClient(Path.GetDirectoryName(runtime.SessionManifestPath)!);
                    timer.Start();
                    Assert.Equal("passed", (await Wait(client, runtime.SessionId, top.Id, SemanticWaitConditionKinds.FrameReady)).Status);
                    var scoped = await Wait(client, runtime.SessionId, top.Id, SemanticWaitConditionKinds.FrameStable, "Stable");
                    Assert.True(scoped.Status == "passed", JsonSerializer.Serialize(scoped));
                    Assert.NotNull(scoped.WaitObservation!.Readiness!.FrameFingerprint);
                    Assert.True(ticks > 0);
                    var moving = await Wait(client, runtime.SessionId, top.Id, SemanticWaitConditionKinds.FrameStable, "Animated", timeoutMs: 180);
                    Assert.Equal("semantic_workflow_wait_timeout", Assert.Single(moving.Diagnostics).Code);
                    stable.Width = 90;
                    var layout = await Wait(client, runtime.SessionId, top.Id, SemanticWaitConditionKinds.LayoutStable, "Stable");
                    Assert.Equal("passed", layout.Status);
                    Assert.True(layout.WaitObservation!.Readiness!.LayoutValid);
                    Assert.True(int.Parse(layout.Metadata["attempts"]) >= 3);
                    var screenshot = await AvaScopeMcpTools.Screenshot(client, runtime.SessionId.Value, top.Id,
                        Path.Combine(output, "after-render.png"), captureAfterRender: true);
                    Assert.True(screenshot.Success, screenshot.Error?.Message);
                    Assert.Equal("rendered", screenshot.Value!.Readiness!.Frame.Status);
                    Assert.Equal("composition_batch_rendered", screenshot.Value.Readiness.Frame.Source);
                    Assert.True(File.Exists(screenshot.Value.FilePath));
                    timer.Stop();
                    window.Hide();
                    var hidden = await Wait(client, runtime.SessionId, top.Id, SemanticWaitConditionKinds.FrameReady, timeoutMs: 120);
                    Assert.Equal("semantic_workflow_wait_timeout", Assert.Single(hidden.Diagnostics).Code);
                    var noFrame = await client.CaptureScreenshotAsync(runtime.SessionId, top.Id,
                        Path.Combine(output, "hidden.png"), captureAfterRender: true);
                    Assert.False(noFrame.Success);
                    Assert.False(File.Exists(Path.Combine(output, "hidden.png")));
                }
                finally
                {
                    timer.Stop();
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
    public async Task ScenarioReadinessFailurePreservesObservationAndDoesNotDispatchActions()
    {
        using var session = HeadlessUnitTestSession.StartNew(typeof(BridgeHeadlessSmokeTests.BridgeHeadlessTestApplication));
        var output = Path.Combine(Path.GetTempPath(), "AvaScope.Tests", Guid.NewGuid().ToString("N"));
        try
        {
            await BridgeHeadlessSmokeTests.DispatchAsync(session, async () =>
            {
                AvaScopeBridge.Deactivate();
                var runtime = AvaScopeBridge.Activate(new BridgeActivationOptions("Startup gate"));
                var invoked = 0;
                var button = new Button { Name = "Action", Content = "Run" };
                button.Click += (_, _) => invoked++;
                var window = new Window { Width = 200, Height = 140, Content = button };
                try
                {
                    window.Show();
                    using var registration = runtime.RegisterTopLevel(window);
                    var top = Assert.Single(await runtime.ListTopLevelsAsync());
                    var client = new LocalBridgeClient(Path.GetDirectoryName(runtime.SessionManifestPath)!);
                    RuntimeScenarioRequest Request() => new(
                        [new SemanticWorkflowStep(SemanticWorkflowActions.Invoke, selector: new SemanticWorkflowSelector(name: "Action"))],
                        sessionId: runtime.SessionId, topLevelId: top.Id, outputDirectory: Path.Combine(output, Guid.NewGuid().ToString("N")),
                        startupReadiness: new RuntimeStartupReadinessOptions(waitForFrame: false, waitForApplication: true, timeoutMs: 200));
                    var absent = await AvaScopeMcpTools.RunScenario(client, Request());
                    Assert.False(absent.Success);
                    Assert.NotNull(absent.Value);
                    Assert.Equal("ui_readiness", absent.Value!.FailureStage);
                    Assert.Null(absent.Value.Workflow);
                    Assert.Equal("unavailable", absent.Value.Readiness!.Observations.Last().Availability);
                    Assert.Equal(0, invoked);
                    runtime.SetReadiness("busy");
                    var busy = await AvaScopeMcpTools.RunScenario(client, Request());
                    Assert.Equal("available", busy.Value!.Readiness!.Observations.Last().Availability);
                    Assert.False(busy.Value.Readiness.Observations.Last().Matched);
                    Assert.Equal(0, invoked);
                    runtime.SetReadiness("ready");
                    var ready = await AvaScopeMcpTools.RunScenario(client, Request());
                    Assert.Equal("passed", ready.Value!.Status);
                    Assert.All(ready.Value.Readiness!.Observations, value => Assert.True(value.Matched));
                    Assert.Equal(1, invoked);

                    var panel = new StackPanel();
                    for (var index = 0; index < 520; index++) panel.Children.Add(new Border { Width = 10, Height = 1 });
                    window.Content = panel;
                    var large = await runtime.ReadinessAsync(top.Id);
                    Assert.True(large.Value!.Truncated);
                    Assert.Null(large.Value.LayoutFingerprint);
                    Assert.Equal(512, large.Value.SampledNodes);
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
    public void ReadinessOptionsRemainOptionalAndRoundTripThroughWorkflowContracts()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var legacy = JsonSerializer.Deserialize<RuntimeScenarioRequest>(
            """{"sessionId":"session-test","topLevelId":"top","steps":[{"action":"screenshot"}]}""", options)!;
        Assert.Null(legacy.StartupReadiness);
        Assert.False(legacy.Steps[0].CaptureAfterRender);
        var request = new RuntimeScenarioRequest(legacy.Steps, sessionId: legacy.SessionId, topLevelId: legacy.TopLevelId,
            startupReadiness: new RuntimeStartupReadinessOptions(waitForApplication: true, waitForStableLayout: true));
        var json = JsonSerializer.Serialize(request, options);
        Assert.Equal(json, JsonSerializer.Serialize(JsonSerializer.Deserialize<RuntimeScenarioRequest>(json, options), options));
        Assert.Throws<ArgumentOutOfRangeException>(() => new RuntimeReadinessProbeOptions(timeoutMs: 5001));
        Assert.Throws<ArgumentOutOfRangeException>(() => new SemanticWaitCondition(SemanticWaitConditionKinds.FrameStable, stableSamples: 1));
        Assert.Contains(BridgeIpcMethods.Readiness, BridgeIpcMethods.All);
    }

    private static async Task<SemanticWorkflowStepResult> Wait(LocalBridgeClient client, SessionId sessionId,
        string topLevelId, string kind, string? name = null, int timeoutMs = 2500)
    {
        var result = await AvaScopeMcpTools.RunWorkflow(client, new SemanticWorkflowRequest(sessionId, topLevelId,
            [new SemanticWorkflowStep(SemanticWorkflowActions.WaitForState,
                selector: name is null ? null : new SemanticWorkflowSelector(name: name),
                waitCondition: new SemanticWaitCondition(kind), timeoutMs: timeoutMs, pollIntervalMs: 25)]));
        Assert.NotNull(result.Value);
        return Assert.Single(result.Value!.Steps);
    }
}
