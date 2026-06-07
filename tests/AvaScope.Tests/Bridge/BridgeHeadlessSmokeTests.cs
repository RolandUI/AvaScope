using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Themes.Fluent;
using Avalonia.Threading;
using AvaScope.Bridge;
using AvaScope.Core;
using AvaScope.Mcp;
using AvaScope.Protocol;

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

    [Fact]
    public async Task McpToolsListTopLevelsAndCaptureScreenshotThroughLocalBridgePipe()
    {
        using var session = HeadlessUnitTestSession.StartNew(typeof(BridgeHeadlessTestApplication));

        await session.Dispatch(async () =>
        {
            var runtime = AvaScopeBridge.Activate(new BridgeActivationOptions("Headless pipe sample"));
            var textBlock = new TextBlock
            {
                Name = "PipeText",
                Text = "AvaScope pipe"
            };
            AutomationProperties.SetAutomationId(textBlock, "pipe-text");

            var window = new Window
            {
                Title = "AvaScope Pipe Sample",
                Width = 360,
                Height = 240,
                Content = textBlock
            };

            window.Show();
            using var registration = runtime.RegisterTopLevel(window);
            Dispatcher.UIThread.RunJobs();

            var client = new LocalBridgeClient(Path.GetDirectoryName(runtime.SessionManifestPath)!);
            var topLevels = await AvaScopeMcpTools.ListTopLevels(client, runtime.SessionId.Value);

            Assert.True(topLevels.Success, topLevels.Error?.Message);
            var topLevel = Assert.Single(topLevels.Value!.TopLevels);
            Assert.Equal("window", topLevel.Kind);
            Assert.Equal("AvaScope Pipe Sample", topLevel.Title);

            var visualTree = await AvaScopeMcpTools.VisualTree(
                client,
                runtime.SessionId.Value,
                topLevel.Id,
                maxDepth: 8);

            Assert.True(visualTree.Success, visualTree.Error?.Message);
            Assert.Equal(TreeKinds.Visual, visualTree.Value!.TreeKind);
            Assert.Equal(topLevel.Id, visualTree.Value.TopLevelId);
            Assert.Contains("Window", visualTree.Value.Root.NodeType, StringComparison.Ordinal);
            Assert.NotEmpty(visualTree.Value.Root.Children);
            Assert.NotNull(FindNode(visualTree.Value.Root, node => node.Name == "PipeText"));

            var depthLimitedVisualTree = await AvaScopeMcpTools.VisualTree(
                client,
                runtime.SessionId.Value,
                topLevel.Id,
                maxDepth: 0);

            Assert.True(depthLimitedVisualTree.Success, depthLimitedVisualTree.Error?.Message);
            Assert.Empty(depthLimitedVisualTree.Value!.Root.Children);

            var logicalTree = await AvaScopeMcpTools.LogicalTree(
                client,
                runtime.SessionId.Value,
                topLevel.Id,
                maxDepth: 4);

            Assert.True(logicalTree.Success, logicalTree.Error?.Message);
            Assert.Equal(TreeKinds.Logical, logicalTree.Value!.TreeKind);
            Assert.NotNull(FindNode(logicalTree.Value.Root, node => node.Text == "AvaScope pipe"));

            var byType = await AvaScopeMcpTools.FindNodes(
                client,
                runtime.SessionId.Value,
                topLevel.Id,
                TreeKinds.Visual,
                nodeType: "TextBlock",
                maxDepth: 8);
            Assert.True(byType.Success, byType.Error?.Message);
            Assert.Single(byType.Value!.Matches);

            var byName = await AvaScopeMcpTools.FindNodes(
                client,
                runtime.SessionId.Value,
                topLevel.Id,
                TreeKinds.Visual,
                name: "PipeText",
                maxDepth: 8);
            Assert.True(byName.Success, byName.Error?.Message);
            Assert.Equal("PipeText", Assert.Single(byName.Value!.Matches).Node.Name);

            var byAutomationId = await AvaScopeMcpTools.FindNodes(
                client,
                runtime.SessionId.Value,
                topLevel.Id,
                TreeKinds.Visual,
                automationId: "pipe-text",
                maxDepth: 8);
            Assert.True(byAutomationId.Success, byAutomationId.Error?.Message);
            var automationMatch = Assert.Single(byAutomationId.Value!.Matches);
            Assert.Equal("pipe-text", automationMatch.Node.AutomationId);
            Assert.True(automationMatch.Path.Count >= 2);

            var byText = await AvaScopeMcpTools.FindNodes(
                client,
                runtime.SessionId.Value,
                topLevel.Id,
                TreeKinds.Logical,
                text: "AvaScope pipe",
                maxDepth: 4);
            Assert.True(byText.Success, byText.Error?.Message);
            Assert.Equal("AvaScope pipe", Assert.Single(byText.Value!.Matches).Node.Text);

            var screenshotPath = Path.Combine(
                Path.GetTempPath(),
                "AvaScope.Tests",
                $"{Guid.NewGuid():N}.png");

            try
            {
                var screenshot = await AvaScopeMcpTools.Screenshot(
                    client,
                    runtime.SessionId.Value,
                    topLevel.Id,
                    screenshotPath);

                Assert.True(screenshot.Success, screenshot.Error?.Message);
                Assert.Equal(runtime.SessionId, screenshot.Value!.SessionId);
                Assert.Equal(topLevel.Id, screenshot.Value.TopLevelId);
                Assert.Equal(Path.GetFullPath(screenshotPath), screenshot.Value.FilePath);
                Assert.True(File.Exists(screenshot.Value.FilePath));
                Assert.True(new FileInfo(screenshot.Value.FilePath).Length > 0);
            }
            finally
            {
                if (File.Exists(screenshotPath))
                {
                    File.Delete(screenshotPath);
                }

                window.Close();
            }
        }, CancellationToken.None);
    }

    [Fact]
    public async Task McpInputClicksButtonAndTypesTextThroughLocalBridgePipe()
    {
        using var session = HeadlessUnitTestSession.StartNew(typeof(BridgeHeadlessTestApplication));

        await session.Dispatch(async () =>
        {
            var clicked = 0;
            var pointerMoved = 0;
            var pointerPressed = 0;
            var pointerReleased = 0;
            var button = new Button
            {
                Name = "ClickTarget",
                Content = "Click",
                Width = 120,
                Height = 40
            };
            button.Click += (_, _) => clicked++;
            button.PointerMoved += (_, _) => pointerMoved++;
            button.AddHandler(InputElement.PointerPressedEvent, (_, e) =>
            {
                pointerPressed++;
                Assert.True(e.GetCurrentPoint(button).Properties.IsLeftButtonPressed);
            }, RoutingStrategies.Bubble, handledEventsToo: true);
            button.AddHandler(InputElement.PointerReleasedEvent, (_, e) =>
            {
                pointerReleased++;
                Assert.Equal(MouseButton.Left, e.InitialPressMouseButton);
            }, RoutingStrategies.Bubble, handledEventsToo: true);

            var textBox = new TextBox
            {
                Name = "TextTarget",
                Width = 160
            };

            var window = new Window
            {
                Title = "AvaScope Input Sample",
                Width = 360,
                Height = 240,
                Content = new StackPanel
                {
                    Children =
                    {
                        button,
                        textBox
                    }
                }
            };

            var runtime = AvaScopeBridge.Activate(new BridgeActivationOptions("Headless input sample"));
            window.Show();
            using var registration = runtime.RegisterTopLevel(window);
            Dispatcher.UIThread.RunJobs();

            var client = new LocalBridgeClient(Path.GetDirectoryName(runtime.SessionManifestPath)!);
            var topLevel = Assert.Single(await runtime.ListTopLevelsAsync());
            var buttonCenter = button.TranslatePoint(
                new Point(button.Bounds.Width / 2, button.Bounds.Height / 2),
                window);

            Assert.NotNull(buttonCenter);

            var move = await AvaScopeMcpTools.Input(
                client,
                runtime.SessionId.Value,
                topLevel.Id,
                InputActions.PointerMove,
                buttonCenter.Value.X,
                buttonCenter.Value.Y);

            Assert.True(move.Success, move.Error?.Message);
            Assert.True(move.Value!.Handled);
            Assert.False(string.IsNullOrWhiteSpace(move.Value.TargetNodeId));
            Assert.Equal(1, pointerMoved);

            var down = await AvaScopeMcpTools.Input(
                client,
                runtime.SessionId.Value,
                topLevel.Id,
                InputActions.PointerDown,
                buttonCenter.Value.X,
                buttonCenter.Value.Y);

            Assert.True(down.Success, down.Error?.Message);
            Assert.True(down.Value!.Handled);
            Assert.False(string.IsNullOrWhiteSpace(down.Value.TargetNodeId));
            Assert.Equal(1, pointerPressed);

            var up = await AvaScopeMcpTools.Input(
                client,
                runtime.SessionId.Value,
                topLevel.Id,
                InputActions.PointerUp,
                buttonCenter.Value.X,
                buttonCenter.Value.Y);

            Assert.True(up.Success, up.Error?.Message);
            Assert.True(up.Value!.Handled);
            Assert.False(string.IsNullOrWhiteSpace(up.Value.TargetNodeId));
            Assert.Equal(1, pointerReleased);

            var click = await AvaScopeMcpTools.Input(
                client,
                runtime.SessionId.Value,
                topLevel.Id,
                InputActions.Click,
                buttonCenter.Value.X,
                buttonCenter.Value.Y);

            Assert.True(click.Success, click.Error?.Message);
            Assert.True(click.Value!.Handled);
            Assert.Equal(1, clicked);

            Assert.True(textBox.Focus(NavigationMethod.Pointer));

            var keyText = await AvaScopeMcpTools.Input(
                client,
                runtime.SessionId.Value,
                topLevel.Id,
                InputActions.KeyText,
                inputText: "abc");

            Assert.True(keyText.Success, keyText.Error?.Message);
            Assert.True(keyText.Value!.Handled);
            Assert.Equal("abc", textBox.Text);

            var unsupported = await AvaScopeMcpTools.Input(
                client,
                runtime.SessionId.Value,
                topLevel.Id,
                "drag");

            Assert.False(unsupported.Success);
            Assert.Equal(BridgeErrorCodes.UnsupportedInputAction, unsupported.Error!.Code);

            window.Close();
        }, CancellationToken.None);
    }

    private static TreeNodeSummary? FindNode(
        TreeNodeSummary node,
        Func<TreeNodeSummary, bool> predicate)
    {
        if (predicate(node))
        {
            return node;
        }

        foreach (var child in node.Children)
        {
            var match = FindNode(child, predicate);
            if (match is not null)
            {
                return match;
            }
        }

        return null;
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
