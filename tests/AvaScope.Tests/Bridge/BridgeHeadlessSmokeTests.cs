using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Themes.Fluent;
using Avalonia.Threading;
using AvaScope.Bridge;

namespace AvaScope.Tests.Bridge;

[Collection(BridgeCollectionDefinition.Name)]
public sealed class BridgeHeadlessSmokeTests : IDisposable
{
    public BridgeHeadlessSmokeTests()
    {
        AvaScopeBridge.Deactivate();
    }

    public void Dispose()
    {
        AvaScopeBridge.Deactivate();
    }

    [Fact]
    public async Task BridgeDiscoversOpenHeadlessWindowOnUiThread()
    {
        using var session = HeadlessUnitTestSession.StartNew(typeof(BridgeHeadlessTestApplication));

        await session.Dispatch(() =>
        {
            var runtime = AvaScopeBridge.Activate(new BridgeActivationOptions("Headless sample"));
            var window = new Window
            {
                Title = "AvaScope Headless Sample",
                Width = 320,
                Height = 200,
                Content = new TextBlock { Text = "AvaScope" }
            };

            window.Show();
            using var registration = runtime.RegisterTopLevel(window);
            Dispatcher.UIThread.RunJobs();

            var topLevels = runtime.ListTopLevelsAsync().GetAwaiter().GetResult();

            var topLevel = Assert.Single(topLevels);
            Assert.Equal("window", topLevel.Kind);
            Assert.Equal("AvaScope Headless Sample", topLevel.Title);
            Assert.True(topLevel.Width > 0);
            Assert.True(topLevel.Height > 0);
            Assert.True(topLevel.RenderScaling > 0);
            Assert.StartsWith("topLevel:", topLevel.Id, StringComparison.Ordinal);

            var screenshotPath = Path.Combine(
                Path.GetTempPath(),
                "AvaScope.Tests",
                $"{Guid.NewGuid():N}.png");

            try
            {
                var screenshot = runtime.CaptureScreenshotAsync(topLevel.Id, screenshotPath).GetAwaiter().GetResult();

                Assert.True(screenshot.Success, screenshot.Error?.Message);
                Assert.Equal(runtime.SessionId, screenshot.Value!.SessionId);
                Assert.Equal(topLevel.Id, screenshot.Value.TopLevelId);
                Assert.Equal(Path.GetFullPath(screenshotPath), screenshot.Value.FilePath);
                Assert.True(screenshot.Value.PixelWidth > 0);
                Assert.True(screenshot.Value.PixelHeight > 0);
                Assert.True(File.Exists(screenshot.Value.FilePath));
                Assert.True(new FileInfo(screenshot.Value.FilePath).Length > 0);
            }
            finally
            {
                if (File.Exists(screenshotPath))
                {
                    File.Delete(screenshotPath);
                }
            }

            window.Close();
        }, CancellationToken.None);
    }

    [Fact]
    public async Task ScreenshotCaptureForMissingTopLevelReturnsStructuredError()
    {
        using var session = HeadlessUnitTestSession.StartNew(typeof(BridgeHeadlessTestApplication));

        await session.Dispatch(() =>
        {
            var runtime = AvaScopeBridge.Activate(new BridgeActivationOptions("Headless sample"));
            var screenshotPath = Path.Combine(
                Path.GetTempPath(),
                "AvaScope.Tests",
                $"{Guid.NewGuid():N}.png");

            var result = runtime.CaptureScreenshotAsync("topLevel:missing", screenshotPath).GetAwaiter().GetResult();

            Assert.False(result.Success);
            Assert.Null(result.Value);
            Assert.Equal(BridgeErrorCodes.TopLevelNotFound, result.Error!.Code);
            Assert.False(File.Exists(screenshotPath));
        }, CancellationToken.None);
    }

    private sealed class BridgeHeadlessTestApplication : Application
    {
        public static AppBuilder BuildAvaloniaApp()
        {
            return AppBuilder.Configure<BridgeHeadlessTestApplication>()
                .UseSkia()
                .UseHeadless(new AvaloniaHeadlessPlatformOptions
                {
                    UseHeadlessDrawing = false
                });
        }

        public override void Initialize()
        {
            Styles.Add(new FluentTheme());
        }
    }
}
