using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Media;
using AvaScope.Bridge;
using AvaScope.Core;
using AvaScope.Protocol;
using SkiaSharp;

namespace AvaScope.Tests.Bridge;

[Collection(BridgeCollectionDefinition.Name)]
public sealed class RuntimeAnimationTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task RealInputPlaybackDistinguishesMotionFailureAndMeasurementDelay(bool stopped, bool delayed)
    {
        using var session = HeadlessUnitTestSession.StartNew(typeof(BridgeHeadlessSmokeTests.BridgeHeadlessTestApplication));
        var output = Path.Combine(Path.GetTempPath(), "AvaScope.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(output);
        try
        {
            await BridgeHeadlessSmokeTests.DispatchAsync(session, async () =>
            {
                AvaScopeBridge.Deactivate();
                var runtime = AvaScopeBridge.Activate(new BridgeActivationOptions("Measured animation regression"));
                var mover = new Border { Name = "Mover", Width = 20, Height = 20, Background = Brushes.Red };
                mover.Transitions = new Transitions { new DoubleTransition { Property = Control.WidthProperty, Duration = TimeSpan.FromMilliseconds(800) } };
                var button = new Button { Name = "Start", Content = "Start", Width = 80, Height = 25 };
                Canvas.SetTop(button, 40);
                var starts = 0;
                button.Click += (_, _) =>
                {
                    starts++;
                    if (!stopped) mover.Width = 120;
                    if (delayed) Thread.Sleep(400); // Deliberately block the trigger handler: timing must be inconclusive.
                };
                var window = new Window { Width = 160, Height = 100, Content = new Canvas { Children = { mover, button } } };
                try
                {
                    window.Show();
                    using var registration = runtime.RegisterTopLevel(window);
                    using var initial = window.CaptureRenderedFrame();
                    Assert.NotNull(initial);
                    var top = Assert.Single(await runtime.ListTopLevelsAsync());
                    var buttonMatch = await runtime.FindNodesAsync(top.Id, TreeKinds.Visual, name: "Start");
                    var moverMatch = await runtime.FindNodesAsync(top.Id, TreeKinds.Visual, name: "Mover");
                    var buttonId = Assert.Single(buttonMatch.Value!.Matches).Node.NodeId;
                    var moverId = Assert.Single(moverMatch.Value!.Matches).Node.NodeId;
                    var result = await new RuntimeInteractionAnimationRunner().RunAsync(
                        new LocalBridgeClient(Path.GetDirectoryName(runtime.SessionManifestPath)!, TimeSpan.FromSeconds(30)),
                        new(runtime.SessionId, top.Id,
                            [new(InputActions.Click, "start", targetNodeId: buttonId, frameOffsetsMs: [0, 300, 1000, 1200]),
                                .. (delayed ? new[] { new RuntimeInteractionAnimationStep(InputActions.Click, "must-not-run", targetNodeId: buttonId) } : [])],
                            outputDirectory: output, timingToleranceMs: 150,
                            assertions:
                            [
                                new(moverId, "width", "increasing", assertionId: "motion", toOffsetMs: 1000),
                                new(moverId, "width", "within_range", assertionId: "middle", minValue: 35, maxValue: 90, fromOffsetMs: 300, toOffsetMs: 300),
                                new(moverId, "width", "equals", assertionId: "final", expectedValue: 120, fromOffsetMs: 1000),
                                new(moverId, "width", "final_stable", assertionId: "settled", fromOffsetMs: 1000),
                                .. (delayed ? new[] { new RuntimeInteractionGeometryAssertion(moverId, "width", "equals",
                                    assertionId: "unobserved", stepId: "must-not-run", expectedValue: 120) } : [])
                            ]));
                    Assert.True(result.Success, result.Error?.Message);
                    Assert.Equal(1, starts);
                    Assert.Equal(delayed ? "inconclusive" : stopped ? "failed" : "passed", result.Value!.Status);
                    var step = Assert.Single(result.Value.Steps);
                    Assert.Equal(4, step.Frames.Count);
                    Assert.All(step.Frames, frame =>
                    {
                        var timing = Assert.IsType<AnimationSampleTiming>(frame.Timing);
                        Assert.Equal("application_input_dispatch_interval", timing.Origin);
                        Assert.Equal(!delayed, timing.WithinTolerance);
                    });
                    Assert.All(result.Value.Assertions, assertion =>
                    {
                        var expected = delayed ? "inconclusive" : stopped && assertion.AssertionId != "settled" ? "failed" : "passed";
                        Assert.Equal(expected, assertion.Status);
                    });
                    using var finalPixels = SKBitmap.Decode(step.Frames[^1].Screenshot!.FilePath);
                    var redWidth = Enumerable.Range(0, finalPixels.Width).Count(x => finalPixels.GetPixel(x, 10) == SKColors.Red);
                    Assert.Equal(stopped ? 20 : 120, redWidth);
                    Assert.Equal(stopped ? 20 : 120, mover.Bounds.Width);
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
            Directory.Delete(output, recursive: true);
        }
    }
}
