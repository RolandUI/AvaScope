using System.Security.Cryptography;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using AvaScope.Bridge;
using AvaScope.ComplexWorkflowApp;
using AvaScope.Core;
using AvaScope.Protocol;
using SkiaSharp;

namespace AvaScope.Tests.Bridge;

[Collection(BridgeCollectionDefinition.Name)]
public sealed class RuntimeRenderingTests
{
    [Theory]
    [InlineData(1.0, "screenshot")]
    [InlineData(1.5, "screenshot")]
    [InlineData(2.0, "screenshot")]
    [InlineData(1.0, "observe")]
    [InlineData(1.5, "observe")]
    [InlineData(2.0, "observe")]
    [InlineData(1.0, "capture_screen")]
    [InlineData(1.5, "capture_screen")]
    [InlineData(2.0, "capture_screen")]
    public async Task RepeatedOpacityTransformsKeepTextScaleAndLiveFrame(double scaling, string route)
    {
        using var session = HeadlessUnitTestSession.StartNew(typeof(BridgeHeadlessSmokeTests.BridgeHeadlessTestApplication));
        var output = Path.Combine(Path.GetTempPath(), "AvaScope.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(output);
        try
        {
            await BridgeHeadlessSmokeTests.DispatchAsync(session, async () =>
            {
                AvaScopeBridge.Deactivate();
                var runtime = AvaScopeBridge.Activate(new BridgeActivationOptions("Opacity transform regression"));
                var window = new Window { Width = 300, Height = 300, Background = Brushes.White, WindowDecorations = WindowDecorations.None };
                window.SetRenderScaling(scaling);
                try
                {
                    window.Show();
                    using var registration = runtime.RegisterTopLevel(window);
                    var top = Assert.Single(await runtime.ListTopLevelsAsync());
                    var client = new LocalBridgeClient(Path.GetDirectoryName(runtime.SessionManifestPath)!);
                    using var preparedFrame = window.CaptureRenderedFrame();
                    Assert.NotNull(preparedFrame);
                    Assert.Equal(new PixelSize((int)(300 * scaling), (int)(300 * scaling)), preparedFrame.PixelSize);
                    byte[]? firstSimple = null, firstComplex = null;
                    foreach (var complex in new[] { false, true, true, false })
                    {
                        var panel = new StackPanel { Background = Brushes.White };
                        for (var row = 0; row < 5; row++)
                            panel.Children.Add(new TextBlock { Text = "MM", FontSize = 18, Height = 45, Margin = new Thickness(15, 0, 0, 0) });
                        window.Content = complex ? new QaOpacityTransformControl() : panel;
                        Dispatcher.UIThread.RunJobs();
                        Assert.True((await runtime.ReadinessAsync(top.Id, options: new(waitForFrame: true, timeoutMs: 5000))).Success);
                        var size = window.ClientSize;
                        var contentBounds = ((Control)window.Content).Bounds;
                        using var live = window.CaptureRenderedFrame();
                        Assert.NotNull(live);
                        using var liveStream = new MemoryStream();
                        live.Save(liveStream, PngBitmapEncoderOptions.Default);
                        using var reference = SKBitmap.Decode(liveStream.ToArray());
                        var expectedBands = TextBands(reference);
                        Assert.Equal(5, expectedBands.Count);

                        string path;
                        if (route == "observe")
                        {
                            var observed = Value(await new RuntimeObserver().ObserveAsync(client,
                                new(runtime.SessionId, [top.Id], includeScreenshot: true, outputDirectory: output, timeoutMs: 5000)));
                            var frame = Assert.Single(observed.Windows);
                            Assert.Equal("available", frame.Parts["screenshot"]);
                            path = frame.Screenshot!.FilePath;
                        }
                        else if (route == "capture_screen")
                        {
                            var target = Value(await client.WindowAsync(new(new(runtime.SessionId, top.Id)))).After!.Target;
                            var captured = Value(await client.CaptureScreenAsync(new(target, output, "rendered", timeoutMs: 5000)));
                            Assert.Equal("captured", captured.Status);
                            path = captured.Rendered!.FilePath!;
                        }
                        else
                            path = Value(await client.CaptureScreenshotAsync(runtime.SessionId, top.Id, Path.Combine(output, "screenshot.png"))).FilePath;

                        var png = File.ReadAllBytes(path);
                        using var actual = SKBitmap.Decode(png);
                        Assert.Equal((int)(300 * scaling), actual.Width);
                        Assert.Equal((int)(300 * scaling), actual.Height);
                        var actualBands = TextBands(actual);
                        Assert.Equal(expectedBands.Count, actualBands.Count);
                        for (var row = 0; row < expectedBands.Count; row++)
                        {
                            // Compositor and offscreen text may differ at antialiased edges, not in layout or scale.
                            Assert.InRange(Math.Abs(actualBands[row].Left - expectedBands[row].Left), 0, 2);
                            Assert.InRange(Math.Abs(actualBands[row].Top - expectedBands[row].Top), 0, 2);
                            Assert.InRange(Math.Abs(actualBands[row].Width - expectedBands[row].Width), 0, 2);
                            Assert.InRange(Math.Abs(actualBands[row].Height - expectedBands[row].Height), 0, 2);
                        }
                        if (complex)
                        {
                            var overlap = actual.GetPixel((int)(135 * scaling), (int)(25 * scaling));
                            Assert.InRange((int)overlap.Red, 126, 128);
                            Assert.InRange((int)overlap.Green, 126, 128);
                            Assert.Equal(255, overlap.Blue);
                            Assert.Equal(SKColors.Lime, actual.GetPixel((int)(125 * scaling), (int)(65 * scaling)));
                            Assert.Equal(SKColors.White, actual.GetPixel((int)(115 * scaling), (int)(55 * scaling)));
                            if (firstComplex is not null) Assert.Equal(firstComplex, png);
                            firstComplex = png;
                        }
                        else
                        {
                            if (firstSimple is not null) Assert.Equal(firstSimple, png);
                            firstSimple = png;
                        }
                        var ready = Value(await runtime.ReadinessAsync(top.Id, options: new(includeFrameHash: true, timeoutMs: 5000)));
                        Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(png)), ready.FrameFingerprint);
                        Assert.Equal(size, window.ClientSize);
                        Assert.Equal(contentBounds, ((Control)window.Content).Bounds);
                        using var after = window.CaptureRenderedFrame();
                        using var afterStream = new MemoryStream();
                        after!.Save(afterStream, PngBitmapEncoderOptions.Default);
                        Assert.Equal(liveStream.ToArray(), afterStream.ToArray());
                    }
                }
                finally { window.Close(); AvaScopeBridge.Deactivate(); }
            }, CancellationToken.None);
        }
        finally { Directory.Delete(output, recursive: true); }
    }

    private static List<SKRectI> TextBands(SKBitmap bitmap)
    {
        var bands = new List<SKRectI>();
        int start = -1, left = bitmap.Width, right = -1;
        for (var y = 0; y <= bitmap.Height; y++)
        {
            var occupied = false;
            if (y < bitmap.Height)
                for (var x = 0; x < bitmap.Width; x++)
                {
                    var pixel = bitmap.GetPixel(x, y);
                    if (pixel.Alpha > 180 && pixel.Red < 180 && pixel.Green < 180 && pixel.Blue < 180)
                    { occupied = true; left = Math.Min(left, x); right = Math.Max(right, x); }
                }
            if (occupied && start == -1) start = y;
            if (!occupied && start != -1)
            { bands.Add(new(left, start, right + 1, y)); start = -1; left = bitmap.Width; right = -1; }
        }
        return bands;
    }

    private static T Value<T>(CoreResult<T> result)
    {
        Assert.True(result.Success, result.Error?.Message);
        return result.Value!;
    }
}
