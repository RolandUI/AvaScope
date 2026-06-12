using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
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
                Assert.Equal(runtime.SessionId, screenshot.Value.Target.SessionId);
                Assert.Equal(topLevel.Id, screenshot.Value.Target.TopLevelId);
                Assert.Null(screenshot.Value.Target.TreeKind);
                Assert.Null(screenshot.Value.Target.NodeId);
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
            var window = new Window
            {
                Title = "AvaScope Missing TopLevel Sample",
                Width = 160,
                Height = 120,
                Content = new TextBlock { Text = "Missing top-level target" }
            };
            var screenshotPath = Path.Combine(
                Path.GetTempPath(),
                "AvaScope.Tests",
                $"{Guid.NewGuid():N}.png");

            try
            {
                window.Show();
                using var registration = runtime.RegisterTopLevel(window);
                Dispatcher.UIThread.RunJobs();

                var result = runtime.CaptureScreenshotAsync("topLevel:missing", screenshotPath).GetAwaiter().GetResult();

                Assert.False(result.Success);
                Assert.Null(result.Value);
                Assert.Equal(BridgeErrorCodes.TopLevelNotFound, result.Error!.Code);
                Assert.False(File.Exists(screenshotPath));
            }
            finally
            {
                window.Close();
            }
        }, CancellationToken.None);
    }

    [Fact]
    public async Task RuntimeMutationContractReturnsNoOpUnsupportedAndStaleDiagnostics()
    {
        using var session = HeadlessUnitTestSession.StartNew(typeof(BridgeHeadlessTestApplication));

        await session.Dispatch(() =>
        {
            var runtime = AvaScopeBridge.Activate(new BridgeActivationOptions("Headless mutation sample"));
            var targetText = new TextBlock
            {
                Name = "MutationTarget",
                Text = "Mutation target",
                Width = 120
            };
            var window = new Window
            {
                Title = "AvaScope Mutation Sample",
                Width = 360,
                Height = 240,
                Content = targetText
            };

            window.Show();
            using var registration = runtime.RegisterTopLevel(window);
            Dispatcher.UIThread.RunJobs();

            var topLevel = Assert.Single(runtime.ListTopLevelsAsync().GetAwaiter().GetResult());
            var tree = runtime.GetVisualTreeAsync(topLevel.Id, maxDepth: 8).GetAwaiter().GetResult();
            Assert.True(tree.Success, tree.Error?.Message);
            var targetNode = FindNode(tree.Value!.Root, node => node.Name == "MutationTarget");
            Assert.NotNull(targetNode);
            Assert.NotNull(targetNode.Target);

            var noop = runtime.MutateNodeAsync(new RuntimeMutationRequest(
                "mutation-request-1",
                targetNode.Target!,
                new RuntimeMutationOperation(RuntimeMutationOperationKinds.NoOp))).GetAwaiter().GetResult();

            Assert.True(noop.Success, noop.Error?.Message);
            Assert.Equal(RuntimeMutationStatuses.NoOp, noop.Value!.Status);
            Assert.False(noop.Value.Applied);
            Assert.Empty(noop.Value.Diagnostics);
            Assert.EndsWith(":1", noop.Value.MutationId, StringComparison.Ordinal);
            Assert.Contains(noop.Value.Capabilities, capability =>
                capability.Name == RuntimeMutationCapabilityCatalog.RuntimeMutationContract
                && capability.Available);

            var secondNoop = runtime.MutateNodeAsync(new RuntimeMutationRequest(
                "mutation-request-2",
                targetNode.Target!,
                new RuntimeMutationOperation(RuntimeMutationOperationKinds.NoOp))).GetAwaiter().GetResult();

            Assert.True(secondNoop.Success, secondNoop.Error?.Message);
            Assert.NotEqual(noop.Value.MutationId, secondNoop.Value!.MutationId);
            Assert.EndsWith(":2", secondNoop.Value.MutationId, StringComparison.Ordinal);

            var widthMutation = runtime.MutateNodeAsync(new RuntimeMutationRequest(
                "mutation-request-3",
                targetNode.Target!,
                new RuntimeMutationOperation(
                    RuntimeMutationOperationKinds.SetProperty,
                    propertyName: "Width",
                    value: "240",
                    valueType: "double"))).GetAwaiter().GetResult();

            Assert.True(widthMutation.Success, widthMutation.Error?.Message);
            Assert.Equal(RuntimeMutationStatuses.Applied, widthMutation.Value!.Status);
            Assert.True(widthMutation.Value.Applied);
            Assert.Equal(240, targetText.Width);
            Assert.Equal("Width", widthMutation.Value.Metadata["propertyName"]);
            Assert.Equal("120", widthMutation.Value.Metadata["originalValue"]);
            Assert.Equal("240", widthMutation.Value.Metadata["effectiveValue"]);
            Assert.Contains(widthMutation.Value.Capabilities, capability =>
                capability.Name == RuntimeMutationCapabilityCatalog.StyleLayoutMutation
                && capability.Available);

            var resetWidth = runtime.MutateNodeAsync(new RuntimeMutationRequest(
                "mutation-request-4",
                targetNode.Target!,
                new RuntimeMutationOperation(
                    RuntimeMutationOperationKinds.ResetMutation,
                    mutationId: widthMutation.Value.MutationId))).GetAwaiter().GetResult();

            Assert.True(resetWidth.Success, resetWidth.Error?.Message);
            Assert.Equal(RuntimeMutationStatuses.Applied, resetWidth.Value!.Status);
            Assert.True(resetWidth.Value.Applied);
            Assert.Equal(120, targetText.Width);
            Assert.Contains(widthMutation.Value.MutationId, resetWidth.Value.Metadata["resetMutationIds"], StringComparison.Ordinal);

            var unsupported = runtime.MutateNodeAsync(new RuntimeMutationRequest(
                "mutation-request-5",
                targetNode.Target!,
                new RuntimeMutationOperation(
                    RuntimeMutationOperationKinds.SetProperty,
                    propertyName: "UnsupportedProperty",
                    value: "240",
                    valueType: "double"))).GetAwaiter().GetResult();

            Assert.True(unsupported.Success, unsupported.Error?.Message);
            Assert.Equal(RuntimeMutationStatuses.Unsupported, unsupported.Value!.Status);
            Assert.False(unsupported.Value.Applied);
            var unsupportedDiagnostic = Assert.Single(unsupported.Value.Diagnostics);
            Assert.Equal(RuntimeMutationErrorCodes.UnsupportedRuntimeMutationProperty, unsupportedDiagnostic.Code);
            Assert.Equal("UnsupportedProperty", unsupportedDiagnostic.Details!["propertyName"]);

            var invalidValue = runtime.MutateNodeAsync(new RuntimeMutationRequest(
                "mutation-request-6",
                targetNode.Target!,
                new RuntimeMutationOperation(
                    RuntimeMutationOperationKinds.SetProperty,
                    propertyName: "Width"))).GetAwaiter().GetResult();

            Assert.True(invalidValue.Success, invalidValue.Error?.Message);
            Assert.Equal(RuntimeMutationStatuses.Rejected, invalidValue.Value!.Status);
            Assert.Equal(RuntimeMutationErrorCodes.InvalidRuntimeMutationValue, Assert.Single(invalidValue.Value.Diagnostics).Code);

            var stale = runtime.MutateNodeAsync(new RuntimeMutationRequest(
                "mutation-request-7",
                new RuntimeTargetContext(runtime.SessionId, "topLevel:missing", TreeKinds.Visual, targetNode.NodeId),
                new RuntimeMutationOperation(RuntimeMutationOperationKinds.NoOp))).GetAwaiter().GetResult();

            Assert.True(stale.Success, stale.Error?.Message);
            Assert.Equal(RuntimeMutationStatuses.StaleTarget, stale.Value!.Status);
            Assert.Equal(RuntimeMutationErrorCodes.RuntimeMutationTargetStale, Assert.Single(stale.Value.Diagnostics).Code);

            window.Close();
        }, CancellationToken.None);
    }

    [Fact]
    public async Task RuntimeMutationAppliesClassesResourcesTextAndScreenshotObservableBackgroundThenResetAll()
    {
        using var session = HeadlessUnitTestSession.StartNew(typeof(BridgeHeadlessTestApplication));

        await session.Dispatch(() =>
        {
            var runtime = AvaScopeBridge.Activate(new BridgeActivationOptions("Headless mutation apply sample"));
            var originalResourceBrush = Brushes.Green;
            var targetText = new TextBlock
            {
                Name = "MutationText",
                Text = "Before",
                Foreground = Brushes.White
            };
            var targetSurface = new Border
            {
                Name = "MutationSurface",
                Width = 180,
                Height = 120,
                Background = Brushes.Red,
                Child = targetText
            };
            targetSurface.Resources["AccentBrush"] = originalResourceBrush;
            var window = new Window
            {
                Title = "AvaScope Mutation Apply Sample",
                Width = 360,
                Height = 240,
                Content = targetSurface
            };

            var beforePath = Path.Combine(Path.GetTempPath(), "AvaScope.Tests", $"{Guid.NewGuid():N}-before.png");
            var afterPath = Path.Combine(Path.GetTempPath(), "AvaScope.Tests", $"{Guid.NewGuid():N}-after.png");
            var diffPath = Path.Combine(Path.GetTempPath(), "AvaScope.Tests", $"{Guid.NewGuid():N}-diff.png");

            try
            {
                window.Show();
                using var registration = runtime.RegisterTopLevel(window);
                Dispatcher.UIThread.RunJobs();

                var topLevel = Assert.Single(runtime.ListTopLevelsAsync().GetAwaiter().GetResult());
                var tree = runtime.GetVisualTreeAsync(topLevel.Id, maxDepth: 8).GetAwaiter().GetResult();
                Assert.True(tree.Success, tree.Error?.Message);
                var surfaceNode = FindNode(tree.Value!.Root, node => node.Name == "MutationSurface");
                var textNode = FindNode(tree.Value.Root, node => node.Name == "MutationText");
                Assert.NotNull(surfaceNode);
                Assert.NotNull(surfaceNode.Target);
                Assert.NotNull(textNode);
                Assert.NotNull(textNode.Target);

                var before = runtime.CaptureScreenshotAsync(topLevel.Id, beforePath).GetAwaiter().GetResult();
                Assert.True(before.Success, before.Error?.Message);

                var background = runtime.MutateNodeAsync(new RuntimeMutationRequest(
                    "apply-background",
                    surfaceNode.Target!,
                    new RuntimeMutationOperation(
                        RuntimeMutationOperationKinds.SetProperty,
                        propertyName: "Background",
                        value: "#0000ff",
                        valueType: "brush"))).GetAwaiter().GetResult();
                var text = runtime.MutateNodeAsync(new RuntimeMutationRequest(
                    "apply-text",
                    textNode.Target!,
                    new RuntimeMutationOperation(
                        RuntimeMutationOperationKinds.SetProperty,
                        propertyName: "Text",
                        value: "After",
                        valueType: "string"))).GetAwaiter().GetResult();
                var addClass = runtime.MutateNodeAsync(new RuntimeMutationRequest(
                    "apply-class",
                    surfaceNode.Target!,
                    new RuntimeMutationOperation(
                        RuntimeMutationOperationKinds.AddClass,
                        className: "agent-selected"))).GetAwaiter().GetResult();
                var resource = runtime.MutateNodeAsync(new RuntimeMutationRequest(
                    "apply-resource",
                    surfaceNode.Target!,
                    new RuntimeMutationOperation(
                        RuntimeMutationOperationKinds.SetResource,
                        value: "#00ff00",
                        valueType: "brush",
                        resourceKey: "AccentBrush"))).GetAwaiter().GetResult();

                Assert.True(background.Success, background.Error?.Message);
                Assert.True(background.Value!.Applied);
                Assert.True(text.Success, text.Error?.Message);
                Assert.True(text.Value!.Applied);
                Assert.True(addClass.Success, addClass.Error?.Message);
                Assert.True(addClass.Value!.Applied);
                Assert.True(resource.Success, resource.Error?.Message);
                Assert.True(resource.Value!.Applied);
                Assert.Equal("After", targetText.Text);
                Assert.Contains("agent-selected", targetSurface.Classes);
                Assert.True(targetSurface.Resources.TryGetValue("AccentBrush", out var mutatedResource));
                Assert.NotSame(originalResourceBrush, mutatedResource);

                Dispatcher.UIThread.RunJobs();
                var after = runtime.CaptureScreenshotAsync(topLevel.Id, afterPath).GetAwaiter().GetResult();
                Assert.True(after.Success, after.Error?.Message);
                var diff = new PreviewImageDiffer().Compare(beforePath, afterPath, diffPath);
                Assert.True(diff.Success, diff.Error?.Message);
                Assert.False(diff.Value!.Passed);
                Assert.True(diff.Value.ChangedPixels > 0);

                var resetAll = runtime.MutateNodeAsync(new RuntimeMutationRequest(
                    "reset-all",
                    surfaceNode.Target!,
                    new RuntimeMutationOperation(RuntimeMutationOperationKinds.ResetAll))).GetAwaiter().GetResult();

                Assert.True(resetAll.Success, resetAll.Error?.Message);
                Assert.Equal(RuntimeMutationStatuses.Applied, resetAll.Value!.Status);
                Assert.True(resetAll.Value.Applied);
                Assert.Equal("4", resetAll.Value.Metadata["resetCount"]);
                Assert.Equal("0", resetAll.Value.Metadata["activeMutationCount"]);
                Assert.Equal("Before", targetText.Text);
                Assert.DoesNotContain("agent-selected", targetSurface.Classes);
                Assert.Equal(Brushes.Red.ToString(), targetSurface.Background?.ToString());
                Assert.True(targetSurface.Resources.TryGetValue("AccentBrush", out var resetResource));
                Assert.Same(originalResourceBrush, resetResource);
            }
            finally
            {
                window.Close();
                DeleteIfExists(beforePath);
                DeleteIfExists(afterPath);
                DeleteIfExists(diffPath);
            }
        }, CancellationToken.None);
    }

    [Fact]
    public async Task RuntimeMutationDeactivateResetsActiveMutationsAndRejectsFurtherMutation()
    {
        using var session = HeadlessUnitTestSession.StartNew(typeof(BridgeHeadlessTestApplication));

        await session.Dispatch(() =>
        {
            var runtime = AvaScopeBridge.Activate(new BridgeActivationOptions("Headless mutation close cleanup sample"));
            var targetSurface = new Border
            {
                Name = "CloseCleanupSurface",
                Width = 180,
                Height = 120,
                Background = Brushes.Red
            };
            var window = new Window
            {
                Title = "AvaScope Mutation Close Cleanup Sample",
                Width = 360,
                Height = 240,
                Content = targetSurface
            };

            try
            {
                window.Show();
                using var registration = runtime.RegisterTopLevel(window);
                Dispatcher.UIThread.RunJobs();

                var topLevel = Assert.Single(runtime.ListTopLevelsAsync().GetAwaiter().GetResult());
                var tree = runtime.GetVisualTreeAsync(topLevel.Id, maxDepth: 8).GetAwaiter().GetResult();
                Assert.True(tree.Success, tree.Error?.Message);
                var targetNode = FindNode(tree.Value!.Root, node => node.Name == "CloseCleanupSurface");
                Assert.NotNull(targetNode);
                Assert.NotNull(targetNode.Target);

                var background = runtime.MutateNodeAsync(new RuntimeMutationRequest(
                    "close-cleanup-background",
                    targetNode.Target!,
                    new RuntimeMutationOperation(
                        RuntimeMutationOperationKinds.SetProperty,
                        propertyName: "Background",
                        value: "#0000ff",
                        valueType: "brush"))).GetAwaiter().GetResult();
                Assert.True(background.Success, background.Error?.Message);
                Assert.True(background.Value!.Applied);
                Assert.Equal("1", background.Value.Metadata["activeMutationCount"]);
                Assert.Equal(Brushes.Blue.ToString(), targetSurface.Background?.ToString());

                var close = AvaScopeBridge.Deactivate();

                Assert.True(close.Success, close.Error?.Message);
                Assert.Equal(SessionLifecycleState.Closed, close.Value!.State);
                Assert.False(AvaScopeBridge.IsActive);
                Assert.Equal(Brushes.Red.ToString(), targetSurface.Background?.ToString());

                var afterClose = runtime.MutateNodeAsync(new RuntimeMutationRequest(
                    "after-close-noop",
                    targetNode.Target!,
                    new RuntimeMutationOperation(RuntimeMutationOperationKinds.NoOp))).GetAwaiter().GetResult();

                Assert.True(afterClose.Success, afterClose.Error?.Message);
                Assert.Equal(RuntimeMutationStatuses.Unavailable, afterClose.Value!.Status);
                Assert.False(afterClose.Value.Applied);
                Assert.Equal(CoreErrorCodes.SessionClosed, Assert.Single(afterClose.Value.Diagnostics).Code);
            }
            finally
            {
                window.Close();
            }
        }, CancellationToken.None);
    }

    [Fact]
    public async Task RuntimeMutationTopLevelRegistrationDisposeResetsScopedMutations()
    {
        using var session = HeadlessUnitTestSession.StartNew(typeof(BridgeHeadlessTestApplication));

        await session.Dispatch(() =>
        {
            var runtime = AvaScopeBridge.Activate(new BridgeActivationOptions("Headless mutation top-level cleanup sample"));
            IDisposable? registration = null;
            IDisposable? secondRegistration = null;
            var targetText = new TextBlock
            {
                Name = "TopLevelCleanupText",
                Text = "Cleanup",
                Width = 120
            };
            var window = new Window
            {
                Title = "AvaScope Mutation TopLevel Cleanup Sample",
                Width = 360,
                Height = 240,
                Content = targetText
            };

            try
            {
                window.Show();
                registration = runtime.RegisterTopLevel(window);
                Dispatcher.UIThread.RunJobs();

                var topLevel = Assert.Single(runtime.ListTopLevelsAsync().GetAwaiter().GetResult());
                var tree = runtime.GetVisualTreeAsync(topLevel.Id, maxDepth: 8).GetAwaiter().GetResult();
                Assert.True(tree.Success, tree.Error?.Message);
                var targetNode = FindNode(tree.Value!.Root, node => node.Name == "TopLevelCleanupText");
                Assert.NotNull(targetNode);
                Assert.NotNull(targetNode.Target);

                var width = runtime.MutateNodeAsync(new RuntimeMutationRequest(
                    "top-level-cleanup-width",
                    targetNode.Target!,
                    new RuntimeMutationOperation(
                        RuntimeMutationOperationKinds.SetProperty,
                        propertyName: "Width",
                        value: "240",
                        valueType: "double"))).GetAwaiter().GetResult();
                Assert.True(width.Success, width.Error?.Message);
                Assert.True(width.Value!.Applied);
                Assert.Equal("1", width.Value.Metadata["activeMutationCount"]);
                Assert.Equal(240, targetText.Width);

                registration.Dispose();
                registration = null;

                Assert.Equal(120, targetText.Width);

                secondRegistration = runtime.RegisterTopLevel(window);
                var resetAll = runtime.MutateNodeAsync(new RuntimeMutationRequest(
                    "top-level-cleanup-reset-all",
                    targetNode.Target!,
                    new RuntimeMutationOperation(RuntimeMutationOperationKinds.ResetAll))).GetAwaiter().GetResult();

                Assert.True(resetAll.Success, resetAll.Error?.Message);
                Assert.Equal(RuntimeMutationStatuses.NoOp, resetAll.Value!.Status);
                Assert.False(resetAll.Value.Applied);
                Assert.Equal("0", resetAll.Value.Metadata["resetCount"]);
                Assert.Equal("0", resetAll.Value.Metadata["activeMutationCount"]);
            }
            finally
            {
                secondRegistration?.Dispose();
                registration?.Dispose();
                window.Close();
            }
        }, CancellationToken.None);
    }

    [Fact]
    public async Task McpMutateNodeReturnsBoundedMutationContractResultsThroughLocalBridgePipe()
    {
        using var session = HeadlessUnitTestSession.StartNew(typeof(BridgeHeadlessTestApplication));

        await session.Dispatch(async () =>
        {
            var runtime = AvaScopeBridge.Activate(new BridgeActivationOptions("Headless MCP mutation sample"));
            var targetText = new TextBlock
            {
                Name = "McpMutationTarget",
                Text = "MCP mutation target",
                Width = 120
            };
            var window = new Window
            {
                Title = "AvaScope MCP Mutation Sample",
                Width = 360,
                Height = 240,
                Content = targetText
            };
            var reviewPath = Path.Combine(
                Path.GetTempPath(),
                "AvaScope.Tests",
                $"{Guid.NewGuid():N}-mutation-review.html");

            window.Show();
            using var registration = runtime.RegisterTopLevel(window);
            Dispatcher.UIThread.RunJobs();

            var client = new LocalBridgeClient(Path.GetDirectoryName(runtime.SessionManifestPath)!);
            var topLevel = Assert.Single(await runtime.ListTopLevelsAsync());
            var tree = await AvaScopeMcpTools.VisualTree(
                client,
                runtime.SessionId.Value,
                topLevel.Id,
                maxDepth: 8);
            Assert.True(tree.Success, tree.Error?.Message);
            var targetNode = FindNode(tree.Value!.Root, node => node.Name == "McpMutationTarget");
            Assert.NotNull(targetNode);

            var noop = await AvaScopeMcpTools.MutateNode(
                client,
                runtime.SessionId.Value,
                topLevel.Id,
                targetNode.NodeId,
                RuntimeMutationOperationKinds.NoOp,
                TreeKinds.Visual,
                requestId: "mcp-mutation-request-1");

            Assert.True(noop.Success, noop.Error?.Message);
            Assert.Equal("mcp-mutation-request-1", noop.Value!.RequestId);
            Assert.Equal(RuntimeMutationStatuses.NoOp, noop.Value.Status);
            Assert.False(noop.Value.Applied);
            Assert.Empty(noop.Value.Diagnostics);

            var widthMutation = await AvaScopeMcpTools.MutateNode(
                client,
                runtime.SessionId.Value,
                topLevel.Id,
                targetNode.NodeId,
                RuntimeMutationOperationKinds.SetProperty,
                TreeKinds.Visual,
                propertyName: "Width",
                value: "240",
                valueType: "double",
                requestId: "mcp-mutation-request-2");

            Assert.True(widthMutation.Success, widthMutation.Error?.Message);
            Assert.Equal(RuntimeMutationStatuses.Applied, widthMutation.Value!.Status);
            Assert.True(widthMutation.Value.Applied);
            Assert.Equal(240, targetText.Width);
            Assert.Equal("Width", widthMutation.Value.Metadata["propertyName"]);
            Assert.Equal("1", widthMutation.Value.Metadata["activeMutationCount"]);
            var styleCapability = Assert.Single(widthMutation.Value.Capabilities, capability =>
                capability.Name == RuntimeMutationCapabilityCatalog.StyleLayoutMutation);
            Assert.True(styleCapability.Available);
            Assert.Equal("local_only", styleCapability.Metadata["transport"]);
            Assert.Equal("true", styleCapability.Metadata["temporary"]);
            Assert.Equal("true", styleCapability.Metadata["reversible"]);

            var review = await AvaScopeMcpTools.MutationReview(
                client,
                runtime.SessionId.Value,
                maxResults: 10,
                artifactPath: reviewPath);

            Assert.True(review.Success, review.Error?.Message);
            Assert.Equal(1, review.Value!.ActiveMutationCount);
            Assert.True(review.Value.HistoryCount >= 2);
            var activeMutation = Assert.Single(review.Value.ActiveMutations);
            Assert.Equal(widthMutation.Value.MutationId, activeMutation.MutationId);
            Assert.Equal("Width", activeMutation.Metadata["propertyName"]);
            Assert.Equal(widthMutation.Value.MutationId, Assert.Single(review.Value.ResetHandoff.ActiveMutationIds));
            Assert.Equal(RuntimeMutationOperationKinds.ResetMutation, review.Value.ResetHandoff.ResetMutationOperation);
            Assert.Equal(RuntimeMutationOperationKinds.ResetAll, review.Value.ResetHandoff.ResetAllOperation);
            Assert.NotNull(review.Value.ResetHandoff.SuggestedResetAllTarget);
            Assert.NotNull(review.Value.ReviewArtifact);
            Assert.True(File.Exists(review.Value.ReviewArtifact!.ArtifactPath));
            Assert.Contains(widthMutation.Value.MutationId, File.ReadAllText(review.Value.ReviewArtifact.ArtifactPath), StringComparison.Ordinal);

            var resetWidth = await AvaScopeMcpTools.MutateNode(
                client,
                runtime.SessionId.Value,
                topLevel.Id,
                targetNode.NodeId,
                RuntimeMutationOperationKinds.ResetMutation,
                TreeKinds.Visual,
                mutationId: widthMutation.Value.MutationId,
                requestId: "mcp-mutation-request-3");

            Assert.True(resetWidth.Success, resetWidth.Error?.Message);
            Assert.Equal(RuntimeMutationStatuses.Applied, resetWidth.Value!.Status);
            Assert.True(resetWidth.Value.Applied);
            Assert.Equal(120, targetText.Width);
            Assert.Contains(widthMutation.Value.MutationId, resetWidth.Value.Metadata["resetMutationIds"], StringComparison.Ordinal);
            Assert.Equal("0", resetWidth.Value.Metadata["activeMutationCount"]);

            var reviewAfterReset = await AvaScopeMcpTools.MutationReview(
                client,
                runtime.SessionId.Value,
                maxResults: 10);
            Assert.True(reviewAfterReset.Success, reviewAfterReset.Error?.Message);
            Assert.Equal(0, reviewAfterReset.Value!.ActiveMutationCount);
            Assert.Empty(reviewAfterReset.Value.ActiveMutations);
            Assert.Contains(reviewAfterReset.Value.History, entry => entry.MutationId == resetWidth.Value.MutationId);

            DeleteIfExists(reviewPath);
            window.Close();
        }, CancellationToken.None);
    }

    [Fact]
    public async Task McpMutateNodeEvidenceCapturesScreenshotsTreesAndDiffThroughLocalBridgePipe()
    {
        using var session = HeadlessUnitTestSession.StartNew(typeof(BridgeHeadlessTestApplication));

        await session.Dispatch(async () =>
        {
            var runtime = AvaScopeBridge.Activate(new BridgeActivationOptions("Headless MCP evidence sample"));
            var targetSurface = new Border
            {
                Name = "McpEvidenceSurface",
                Width = 180,
                Height = 120,
                Background = Brushes.Red
            };
            var window = new Window
            {
                Title = "AvaScope MCP Evidence Sample",
                Width = 360,
                Height = 240,
                Content = targetSurface
            };
            var artifactDirectory = Path.Combine(
                Path.GetTempPath(),
                "AvaScope.Tests",
                $"mcp-evidence-{Guid.NewGuid():N}");

            try
            {
                window.Show();
                using var registration = runtime.RegisterTopLevel(window);
                Dispatcher.UIThread.RunJobs();

                var client = new LocalBridgeClient(Path.GetDirectoryName(runtime.SessionManifestPath)!);
                var topLevel = Assert.Single(await runtime.ListTopLevelsAsync());
                var tree = await AvaScopeMcpTools.VisualTree(
                    client,
                    runtime.SessionId.Value,
                    topLevel.Id,
                    maxDepth: 8);
                Assert.True(tree.Success, tree.Error?.Message);
                var targetNode = FindNode(tree.Value!.Root, node => node.Name == "McpEvidenceSurface");
                Assert.NotNull(targetNode);

                var evidence = await AvaScopeMcpTools.MutateNodeEvidence(
                    client,
                    runtime.SessionId.Value,
                    topLevel.Id,
                    targetNode.NodeId,
                    RuntimeMutationOperationKinds.SetProperty,
                    artifactDirectory,
                    TreeKinds.Visual,
                    propertyName: "Background",
                    value: "#0000ff",
                    valueType: "brush",
                    requestId: "mcp-evidence-request",
                    maxDepth: 8,
                    includeDiff: true,
                    tolerance: 0);

                Assert.True(evidence.Success, evidence.Error?.Message);
                Assert.Equal("mcp-evidence-request", evidence.Value!.RequestId);
                Assert.Equal(RuntimeMutationStatuses.Applied, evidence.Value.Mutation.Status);
                Assert.True(evidence.Value.Mutation.Applied);
                Assert.Equal("captured", evidence.Value.Summary.Status);
                Assert.Equal("changed", evidence.Value.Summary.DiffStatus);
                Assert.True(evidence.Value.Summary.ScreenshotsCaptured);
                Assert.True(evidence.Value.Summary.VisualTreeSnapshotsCaptured);
                Assert.True(evidence.Value.Summary.BeforeTargetFound);
                Assert.True(evidence.Value.Summary.AfterTargetFound);
                Assert.NotNull(evidence.Value.Diff);
                Assert.False(evidence.Value.Diff.Passed);
                Assert.True(evidence.Value.Diff.ChangedPixels > 0);
                Assert.True(evidence.Value.Summary.ChangedPixels > 0);
                Assert.True(File.Exists(evidence.Value.BeforeScreenshotPath));
                Assert.True(File.Exists(evidence.Value.AfterScreenshotPath));
                Assert.True(File.Exists(evidence.Value.BeforeVisualTreePath));
                Assert.True(File.Exists(evidence.Value.AfterVisualTreePath));
                Assert.NotNull(evidence.Value.DiffPath);
                Assert.True(File.Exists(evidence.Value.DiffPath));
                Assert.NotNull(evidence.Value.ReviewArtifact);
                Assert.True(File.Exists(evidence.Value.ReviewArtifact!.ArtifactPath));
                var reviewHtml = File.ReadAllText(evidence.Value.ReviewArtifact.ArtifactPath);
                Assert.Contains(evidence.Value.Mutation.MutationId, reviewHtml, StringComparison.Ordinal);
                Assert.Contains("Before", reviewHtml, StringComparison.Ordinal);
                Assert.Contains("After", reviewHtml, StringComparison.Ordinal);
                Assert.Equal(Brushes.Blue.ToString(), targetSurface.Background?.ToString());
            }
            finally
            {
                window.Close();
                DeleteDirectoryIfExists(artifactDirectory);
            }
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
                Text = "AvaScope pipe",
                FontSize = 18,
                Foreground = Brushes.Red
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
            Assert.Equal(runtime.SessionId, visualTree.Value.Target.SessionId);
            Assert.Equal(topLevel.Id, visualTree.Value.Target.TopLevelId);
            Assert.Equal(TreeKinds.Visual, visualTree.Value.Target.TreeKind);
            Assert.Contains("Window", visualTree.Value.Root.NodeType, StringComparison.Ordinal);
            Assert.Equal(runtime.SessionId, visualTree.Value.Root.Target!.SessionId);
            Assert.Equal(topLevel.Id, visualTree.Value.Root.Target.TopLevelId);
            Assert.Equal(TreeKinds.Visual, visualTree.Value.Root.Target.TreeKind);
            Assert.Equal(visualTree.Value.Root.NodeId, visualTree.Value.Root.Target.NodeId);
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
            Assert.Equal(TreeKinds.Logical, logicalTree.Value.Target.TreeKind);
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
            var pipeTextMatch = Assert.Single(byName.Value!.Matches);
            Assert.Equal("PipeText", pipeTextMatch.Node.Name);
            Assert.Equal(runtime.SessionId, pipeTextMatch.Target!.SessionId);
            Assert.Equal(topLevel.Id, pipeTextMatch.Target.TopLevelId);
            Assert.Equal(TreeKinds.Visual, pipeTextMatch.Target.TreeKind);
            Assert.Equal(pipeTextMatch.Node.NodeId, pipeTextMatch.Target.NodeId);

            var pipeTextNode = pipeTextMatch.Node;
            var inspected = await AvaScopeMcpTools.InspectNode(
                client,
                runtime.SessionId.Value,
                topLevel.Id,
                pipeTextNode.NodeId,
                TreeKinds.Visual);

            Assert.True(inspected.Success, inspected.Error?.Message);
            Assert.Equal(runtime.SessionId, inspected.Value!.SessionId);
            Assert.Equal(topLevel.Id, inspected.Value.TopLevelId);
            Assert.Equal(TreeKinds.Visual, inspected.Value.TreeKind);
            Assert.Equal(pipeTextNode.NodeId, inspected.Value.NodeId);
            Assert.Equal(runtime.SessionId, inspected.Value.Target.SessionId);
            Assert.Equal(topLevel.Id, inspected.Value.Target.TopLevelId);
            Assert.Equal(TreeKinds.Visual, inspected.Value.Target.TreeKind);
            Assert.Equal(pipeTextNode.NodeId, inspected.Value.Target.NodeId);
            Assert.Equal("PipeText", inspected.Value.Name);
            Assert.Equal("pipe-text", inspected.Value.AutomationId);
            Assert.Contains("TextBlock", inspected.Value.NodeType, StringComparison.Ordinal);
            Assert.True(inspected.Value.Bounds is { Width: >= 0, Height: >= 0 });
            Assert.True(inspected.Value.ChildCount >= 0);
            Assert.Contains(inspected.Value.ComputedProperties, property => property.Name == "FontSize" && property.Value == "18");
            Assert.Contains(inspected.Value.ComputedProperties, property => property.Name == "Foreground" && property.Source == "local");

            var missingInspect = await AvaScopeMcpTools.InspectNode(
                client,
                runtime.SessionId.Value,
                topLevel.Id,
                "visual:missing",
                TreeKinds.Visual);

            Assert.False(missingInspect.Success);
            Assert.Equal(BridgeErrorCodes.NodeNotFound, missingInspect.Error!.Code);
            Assert.Equal(topLevel.Id, missingInspect.Error.Details!["topLevelId"]);
            Assert.Equal(TreeKinds.Visual, missingInspect.Error.Details["treeKind"]);
            Assert.Equal("visual:missing", missingInspect.Error.Details["nodeId"]);
            Assert.Contains("Refresh", missingInspect.Error.Details["nextAction"], StringComparison.Ordinal);

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
                Assert.Equal(runtime.SessionId, screenshot.Value.Target.SessionId);
                Assert.Equal(topLevel.Id, screenshot.Value.Target.TopLevelId);
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
            var keyDown = 0;
            var keyUp = 0;
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
            textBox.AddHandler(InputElement.KeyDownEvent, (_, e) =>
            {
                keyDown++;
                Assert.Equal(Key.Enter, e.Key);
                Assert.Equal(KeyModifiers.Control | KeyModifiers.Shift, e.KeyModifiers);
            }, RoutingStrategies.Bubble, handledEventsToo: true);
            textBox.AddHandler(InputElement.KeyUpEvent, (_, e) =>
            {
                keyUp++;
                Assert.Equal(Key.Enter, e.Key);
            }, RoutingStrategies.Bubble, handledEventsToo: true);

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
            Assert.Equal(runtime.SessionId, move.Value.Target.SessionId);
            Assert.Equal(topLevel.Id, move.Value.Target.TopLevelId);
            Assert.Equal(TreeKinds.Visual, move.Value.Target.TreeKind);
            Assert.Equal(move.Value.TargetNodeId, move.Value.Target.NodeId);
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
            Assert.Equal("left", down.Value.PointerButton);
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
            Assert.Equal("left", up.Value.PointerButton);
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
            Assert.Equal("left", click.Value.PointerButton);
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

            var inputTree = await AvaScopeMcpTools.VisualTree(
                client,
                runtime.SessionId.Value,
                topLevel.Id,
                maxDepth: 8);
            Assert.True(inputTree.Success, inputTree.Error?.Message);
            var textTargetNode = FindNode(inputTree.Value!.Root, node => node.Name == "TextTarget");
            Assert.NotNull(textTargetNode);

            textBox.Text = "abcdef";
            textBox.SelectionStart = 1;
            textBox.SelectionEnd = 4;
            textBox.CaretIndex = 4;
            Assert.True(button.Focus(NavigationMethod.Pointer));
            Assert.False(textBox.IsFocused);

            var targetedKeyText = await AvaScopeMcpTools.Input(
                client,
                runtime.SessionId.Value,
                topLevel.Id,
                InputActions.KeyText,
                inputText: "X",
                targetNodeId: textTargetNode.NodeId);

            Assert.True(targetedKeyText.Success, targetedKeyText.Error?.Message);
            Assert.True(targetedKeyText.Value!.Handled);
            Assert.Equal(textTargetNode.NodeId, targetedKeyText.Value.TargetNodeId);
            Assert.Equal(runtime.SessionId, targetedKeyText.Value.Target.SessionId);
            Assert.Equal(topLevel.Id, targetedKeyText.Value.Target.TopLevelId);
            Assert.Equal(TreeKinds.Visual, targetedKeyText.Value.Target.TreeKind);
            Assert.Equal(textTargetNode.NodeId, targetedKeyText.Value.Target.NodeId);
            Assert.True(textBox.IsFocused);
            Assert.Equal("aXef", textBox.Text);
            Assert.Equal(2, textBox.CaretIndex);
            Assert.Equal(textBox.CaretIndex, textBox.SelectionStart);
            Assert.Equal(textBox.CaretIndex, textBox.SelectionEnd);

            textBox.Text = "clear-me";
            textBox.SelectionStart = 2;
            textBox.SelectionEnd = 7;
            textBox.CaretIndex = 7;
            Assert.True(button.Focus(NavigationMethod.Pointer));
            Assert.False(textBox.IsFocused);

            var clearText = await AvaScopeMcpTools.Input(
                client,
                runtime.SessionId.Value,
                topLevel.Id,
                InputActions.ClearText,
                targetNodeId: textTargetNode.NodeId);

            Assert.True(clearText.Success, clearText.Error?.Message);
            Assert.True(clearText.Value!.Handled);
            Assert.Equal(InputActions.ClearText, clearText.Value.Action);
            Assert.Equal(textTargetNode.NodeId, clearText.Value.TargetNodeId);
            Assert.True(textBox.IsFocused);
            Assert.Equal(string.Empty, textBox.Text);
            Assert.Equal(0, textBox.CaretIndex);
            Assert.Equal(0, textBox.SelectionStart);
            Assert.Equal(0, textBox.SelectionEnd);

            textBox.Text = "blocked";
            textBox.IsReadOnly = true;
            var readOnlyClear = await AvaScopeMcpTools.Input(
                client,
                runtime.SessionId.Value,
                topLevel.Id,
                InputActions.ClearText,
                targetNodeId: textTargetNode.NodeId);

            Assert.False(readOnlyClear.Success);
            Assert.Equal(BridgeErrorCodes.UnsupportedInputAction, readOnlyClear.Error!.Code);
            Assert.Equal("blocked", textBox.Text);
            textBox.IsReadOnly = false;

            Assert.True(button.Focus(NavigationMethod.Pointer));
            Assert.False(textBox.IsFocused);

            var focus = await AvaScopeMcpTools.Input(
                client,
                runtime.SessionId.Value,
                topLevel.Id,
                InputActions.Focus,
                targetNodeId: textTargetNode.NodeId);

            Assert.True(focus.Success, focus.Error?.Message);
            Assert.True(focus.Value!.Handled);
            Assert.Equal(textTargetNode.NodeId, focus.Value.TargetNodeId);
            Assert.True(textBox.IsFocused);

            var keyDownResult = await AvaScopeMcpTools.Input(
                client,
                runtime.SessionId.Value,
                topLevel.Id,
                InputActions.KeyDown,
                inputKey: "Enter",
                keyModifiers: "Ctrl+Shift");

            Assert.True(keyDownResult.Success, keyDownResult.Error?.Message);
            Assert.True(keyDownResult.Value!.Handled);
            Assert.Equal("Enter", keyDownResult.Value.InputKey);
            Assert.Contains("Control", keyDownResult.Value.KeyModifiers, StringComparison.Ordinal);
            Assert.Contains("Shift", keyDownResult.Value.KeyModifiers, StringComparison.Ordinal);
            Assert.Equal(1, keyDown);

            var keyUpResult = await AvaScopeMcpTools.Input(
                client,
                runtime.SessionId.Value,
                topLevel.Id,
                InputActions.KeyUp,
                inputKey: "Enter");

            Assert.True(keyUpResult.Success, keyUpResult.Error?.Message);
            Assert.True(keyUpResult.Value!.Handled);
            Assert.Equal("Enter", keyUpResult.Value.InputKey);
            Assert.Equal(1, keyUp);

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

    [Fact]
    public async Task McpExpandedInputAndRuntimeStateInspectionUseBridgeOnly()
    {
        using var session = HeadlessUnitTestSession.StartNew(typeof(BridgeHeadlessTestApplication));

        await session.Dispatch(async () =>
        {
            var tabControl = new TabControl
            {
                Name = "TabTarget",
                Items =
                {
                    new TabItem { Header = "First", Content = "First content" },
                    new TabItem { Header = "Second", Content = "Second content" }
                }
            };
            var debugPanel = new DebugStatePanel
            {
                Name = "DebugPanel",
                Width = 120,
                Height = 260,
                DataContext = new RuntimeStateViewModel()
            };
            var scrollViewer = new ScrollViewer
            {
                Name = "ScrollTarget",
                Width = 120,
                Height = 80,
                Content = debugPanel
            };

            var window = new Window
            {
                Title = "AvaScope Runtime State Sample",
                Width = 360,
                Height = 260,
                Content = new StackPanel
                {
                    Children =
                    {
                        tabControl,
                        scrollViewer
                    }
                }
            };

            var runtime = AvaScopeBridge.Activate(new BridgeActivationOptions("Headless runtime state sample"));
            window.Show();
            using var registration = runtime.RegisterTopLevel(window);
            Dispatcher.UIThread.RunJobs();

            var client = new LocalBridgeClient(Path.GetDirectoryName(runtime.SessionManifestPath)!);
            var topLevel = Assert.Single(await runtime.ListTopLevelsAsync());
            var tree = await AvaScopeMcpTools.VisualTree(
                client,
                runtime.SessionId.Value,
                topLevel.Id,
                maxDepth: 12);
            Assert.True(tree.Success, tree.Error?.Message);
            var tabNode = FindNode(tree.Value!.Root, node => node.Name == "TabTarget");
            var scrollNode = FindNode(tree.Value.Root, node => node.Name == "ScrollTarget");
            var debugNode = FindNode(tree.Value.Root, node => node.Name == "DebugPanel");
            Assert.NotNull(tabNode);
            Assert.NotNull(scrollNode);
            Assert.NotNull(debugNode);

            var select = await AvaScopeMcpTools.Input(
                client,
                runtime.SessionId.Value,
                topLevel.Id,
                InputActions.Select,
                inputText: "1",
                targetNodeId: tabNode.NodeId);

            Assert.True(select.Success, select.Error?.Message);
            Assert.True(select.Value!.Handled);
            Assert.Equal(1, tabControl.SelectedIndex);
            Assert.Equal("1", select.Value.Metadata["selectedIndex"]);

            var scroll = await AvaScopeMcpTools.Input(
                client,
                runtime.SessionId.Value,
                topLevel.Id,
                InputActions.Scroll,
                x: 0,
                y: 40,
                targetNodeId: scrollNode.NodeId);

            Assert.True(scroll.Success, scroll.Error?.Message);
            Assert.True(scroll.Value!.Handled);
            Assert.True(scrollViewer.Offset.Y > 0);
            Assert.Equal(InputActions.Scroll, scroll.Value.Action);
            Assert.Equal("0", scroll.Value.Metadata["previousOffsetY"]);

            var scrollInspect = await AvaScopeMcpTools.InspectNode(
                client,
                runtime.SessionId.Value,
                topLevel.Id,
                scrollNode.NodeId,
                TreeKinds.Visual);

            Assert.True(scrollInspect.Success, scrollInspect.Error?.Message);
            Assert.Equal("available", scrollInspect.Value!.ScrollState!.Status);
            Assert.True(scrollInspect.Value.ScrollState.Offset!.Y > 0);
            Assert.Equal("available", scrollInspect.Value.ScrollState.Content!.Status);

            var debugInspect = await AvaScopeMcpTools.InspectNode(
                client,
                runtime.SessionId.Value,
                topLevel.Id,
                debugNode.NodeId,
                TreeKinds.Visual);

            Assert.True(debugInspect.Success, debugInspect.Error?.Message);
            Assert.Equal("available", debugInspect.Value!.BindingState!.DataContextStatus);
            Assert.Contains(nameof(RuntimeStateViewModel), debugInspect.Value.BindingState.DataContextType);
            Assert.Equal("available", debugInspect.Value.DebugState!.Status);
            Assert.Equal("10..20", debugInspect.Value.DebugState.Fields["visibleRange"]);

            window.Close();
        }, CancellationToken.None);
    }

    [Fact]
    public async Task McpReloadRejectsActiveRuntimeBridgeSessionWithExplicitUnsupportedError()
    {
        using var session = HeadlessUnitTestSession.StartNew(typeof(BridgeHeadlessTestApplication));

        await session.Dispatch(async () =>
        {
            var runtime = AvaScopeBridge.Activate(new BridgeActivationOptions("Headless runtime reload sample"));
            var window = new Window
            {
                Title = "AvaScope Runtime Reload Sample",
                Width = 320,
                Height = 200,
                Content = new TextBlock { Text = "Runtime reload" }
            };

            window.Show();
            using var registration = runtime.RegisterTopLevel(window);
            Dispatcher.UIThread.RunJobs();

            var client = new LocalBridgeClient(Path.GetDirectoryName(runtime.SessionManifestPath)!);
            var previewSessions = CreatePreviewSessionRegistryWithMissingHost();

            var result = await AvaScopeMcpTools.Reload(
                previewSessions,
                client,
                runtime.SessionId.Value);

            Assert.False(result.Success);
            Assert.Null(result.Value);
            Assert.Equal(CoreErrorCodes.RuntimeReloadNotSupported, result.Error!.Code);
            Assert.Contains("verified the local bridge session is active", result.Error.Message, StringComparison.Ordinal);

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

    private static void DeleteIfExists(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    private static void DeleteDirectoryIfExists(string path)
    {
        if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
        }
    }

    private static PreviewSessionRegistry CreatePreviewSessionRegistryWithMissingHost()
    {
        return new PreviewSessionRegistry(
            new SessionRegistry(),
            new PreviewHostClient(Path.Combine(
                Path.GetTempPath(),
                "AvaScope.Tests",
                $"missing-preview-host-{Guid.NewGuid():N}.dll")));
    }

    private sealed class DebugStatePanel : StackPanel, IAvaScopeDebugStateProvider
    {
        public IReadOnlyDictionary<string, string?> GetAvaScopeDebugState()
        {
            return new Dictionary<string, string?>
            {
                ["visibleRange"] = "10..20",
                ["renderCount"] = "3"
            };
        }
    }

    private sealed class RuntimeStateViewModel;

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
