using System.Diagnostics;
using System.Text.Json;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using AvaScope.Bridge;
using AvaScope.Core;
using AvaScope.Protocol;
using ModelContextProtocol.Client;
using SkiaSharp;

namespace AvaScope.Tests.Bridge;

[Collection(BridgeCollectionDefinition.Name)]
public sealed class RuntimePickingTests
{
    [Theory]
    [InlineData("normal")]
    [InlineData("translation")]
    [InlineData("scale")]
    [InlineData("rotation")]
    [InlineData("ancestor_scale")]
    [InlineData("ancestor_rotation")]
    [InlineData("scaled_clipping_control")]
    public async Task VisualBoundsMatchRenderedExtentsAcrossTransforms(string scenario)
    {
        await WithWindow(async (runtime, window, button, top, node, client, output) =>
        {
            var target = new Border { Name = "TransformedBounds", Width = 80, Height = 40, Background = Brushes.Lime };
            AutomationProperties.SetAutomationId(target, "transformed-bounds");
            var localX = scenario == "scaled_clipping_control" ? 70 : 40;
            var localY = scenario == "scaled_clipping_control" ? 20 : 40;
            Canvas.SetLeft(target, localX); Canvas.SetTop(target, localY);
            var parent = new Canvas { Width = scenario == "scaled_clipping_control" ? 140 : 180,
                Height = scenario == "scaled_clipping_control" ? 100 : 120, ClipToBounds = true,
                Children = { target } };
            Canvas.SetLeft(parent, 50); Canvas.SetTop(parent, 50);
            var expected = scenario switch
            {
                "translation" => new NodeBounds(95, 80, 80, 40),
                "scale" => new NodeBounds(50, 100, 160, 20),
                "rotation" => new NodeBounds(110, 70, 40, 80),
                "ancestor_scale" => new NodeBounds(70, 100, 40, 50),
                "ancestor_rotation" => new NodeBounds(120, 60, 40, 80),
                "scaled_clipping_control" => new NodeBounds(140, 80, 40, 20),
                _ => new NodeBounds(90, 90, 80, 40)
            };
            if (scenario == "translation") target.RenderTransform = new TranslateTransform(5, -10);
            if (scenario == "scale") target.RenderTransform = new ScaleTransform(2, .5);
            if (scenario == "rotation") target.RenderTransform = new RotateTransform(90);
            if (scenario == "scaled_clipping_control") target.RenderTransform = new ScaleTransform(.5, .5);
            if (scenario == "ancestor_scale")
            {
                parent.RenderTransformOrigin = new RelativePoint(0, 0, RelativeUnit.Relative);
                parent.RenderTransform = new ScaleTransform(.5, 1.25);
            }
            if (scenario == "ancestor_rotation") parent.RenderTransform = new RotateTransform(90);
            window.Content = new Canvas { Background = Brushes.White, Children = { parent } };
            window.UpdateLayout(); using var prepared = window.CaptureRenderedFrame(); Assert.NotNull(prepared);
            var found = Assert.Single((await client.FindNodesAsync(runtime.SessionId, top.TopLevelId,
                TreeKinds.Visual, name: target.Name, maxDepth: 32)).Value!.Matches).Node;
            var query = await client.FindNodesAsync(runtime.SessionId, top.TopLevelId,
                TreeKinds.Visual, maxDepth: 32, selector: new(treeKind: TreeKinds.Visual,
                    automationId: "transformed-bounds"));
            Assert.True(query.Success && query.Value!.Matches.Count == 1, JsonSerializer.Serialize(query));
            var queried = Assert.Single(query.Value!.Matches).Node;
            var inspected = (await client.InspectNodeAsync(runtime.SessionId, top.TopLevelId,
                TreeKinds.Visual, found.NodeId)).Value!;
            var recorded = await new RuntimeInteractionAnimationRunner().RunAsync(client, new(
                runtime.SessionId, top.TopLevelId,
                [new(RuntimeInteractionAnimationActions.Wait, targetNodeId: found.NodeId, frameOffsetsMs: [0])],
                outputDirectory: output, maxDepth: 32,
                assertions: [new(found.NodeId, "width", "not_clipped")]));
            Assert.True(recorded.Success && recorded.Value!.Status == "passed", JsonSerializer.Serialize(recorded));
            var frame = Assert.Single(Assert.Single(recorded.Value!.Steps).Frames);
            using var pixels = SKBitmap.Decode(frame.Screenshot!.FilePath);
            var minX = pixels.Width; var minY = pixels.Height; var maxX = -1; var maxY = -1;
            for (var y = 0; y < pixels.Height; y++)
                for (var x = 0; x < pixels.Width; x++)
                    if (pixels.GetPixel(x, y) == SKColors.Lime)
                    { minX = Math.Min(minX, x); minY = Math.Min(minY, y); maxX = Math.Max(maxX, x); maxY = Math.Max(maxY, y); }
            Assert.Equal((int)expected.X, minX); Assert.Equal((int)expected.Y, minY);
            Assert.Equal((int)expected.Width, maxX - minX + 1); Assert.Equal((int)expected.Height, maxY - minY + 1);

            void AssertBounds(NodeBounds? actual)
            {
                Assert.NotNull(actual);
                Assert.Equal(expected.X, actual.X, 6); Assert.Equal(expected.Y, actual.Y, 6);
                Assert.Equal(expected.Width, actual.Width, 6); Assert.Equal(expected.Height, actual.Height, 6);
            }
            AssertBounds(found.Bounds); AssertBounds(queried.Bounds); AssertBounds(inspected.Bounds);
            AssertBounds(Assert.Single(frame.Geometry).Bounds);
            Assert.Equal("passed", recorded.Value.Status);
            Assert.Equal(new NodeBounds(localX, localY, 80, 40), inspected.LayoutExplanation!.Node!.Bounds);
            var tree = (await client.VisualTreeAsync(runtime.SessionId, top.TopLevelId, 32)).Value!;
            var pending = new Stack<TreeNodeSummary>(); pending.Push(tree.Root);
            TreeNodeSummary? treeNode = null;
            while (pending.TryPop(out var current))
            {
                if (current.NodeId == found.NodeId) { treeNode = current; break; }
                foreach (var child in current.Children) pending.Push(child);
            }
            Assert.NotNull(treeNode); AssertBounds(treeNode.Bounds);
            var geometry = Pick(await client.PickNodeAsync(new(top))).Geometry;
            var picked = Pick(await client.PickNodeAsync(new(top, expected.X + expected.Width / 2,
                expected.Y + expected.Height / 2, "top_level_dip", geometry.Revision)));
            Assert.Equal(found.NodeId, picked.HitPath[0].Target.NodeId);
            AssertBounds(picked.HitPath[0].Bounds);
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task InteractionRecordingCapturesDeepGeometryAndParentCoordinates(bool clipped)
    {
        await WithWindow(async (runtime, window, button, top, node, client, output) =>
        {
            var target = new Border { Name = "RecordingTarget", Width = 80, Height = 30, Background = Brushes.Lime };
            Canvas.SetLeft(target, clipped ? 110 : 20); Canvas.SetTop(target, 10);
            var parent = new Canvas { Name = "RecordingParent", Width = 140, Height = 80,
                ClipToBounds = true, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top, Children = { target } };
            Control nested = parent;
            for (var depth = 0; depth < 12; depth++) nested = new Border { Child = nested };
            Canvas.SetLeft(nested, 30); Canvas.SetTop(nested, 40);
            window.Content = new Canvas { Background = Brushes.White, Children = { nested } };
            window.UpdateLayout(); using var prepared = window.CaptureRenderedFrame(); Assert.NotNull(prepared);
            var found = Assert.Single((await client.FindNodesAsync(runtime.SessionId, top.TopLevelId,
                TreeKinds.Visual, name: target.Name, maxDepth: 32)).Value!.Matches).Node;
            var parentNode = Assert.Single((await client.FindNodesAsync(runtime.SessionId, top.TopLevelId,
                TreeKinds.Visual, name: parent.Name, maxDepth: 32)).Value!.Matches).Node;
            var tree = (await client.VisualTreeAsync(runtime.SessionId, top.TopLevelId, 32)).Value!;
            Assert.True(tree.ResponseBudget?.Truncated);
            Assert.DoesNotContain(found.NodeId, JsonSerializer.Serialize(tree.Root));
            var result = await new RuntimeInteractionAnimationRunner().RunAsync(client, new(
                runtime.SessionId, top.TopLevelId,
                [new(RuntimeInteractionAnimationActions.Wait, targetNodeId: found.NodeId, frameOffsetsMs: [0, 1])],
                outputDirectory: output, maxDepth: 32, assertions:
                [new(found.NodeId, "width", "equals", expectedValue: 80, tolerance: 0),
                 new(found.NodeId, "width", "not_clipped")]));
            Assert.True(result.Success, JsonSerializer.Serialize(result.Error));
            Assert.Equal(clipped ? "failed" : "passed", result.Value!.Status);
            Assert.Equal("passed", result.Value.Assertions[0].Status);
            Assert.Equal(clipped ? "failed" : "passed", result.Value.Assertions[1].Status);
            Assert.DoesNotContain(result.Value.Diagnostics, d => d.Code == "interaction_geometry_target_not_found");
            Assert.Equal(2, result.Value.Steps[0].Frames.Count);
            foreach (var frame in result.Value.Steps[0].Frames)
            {
                var geometry = Assert.Single(frame.Geometry);
                Assert.Equal(found.NodeId, geometry.NodeId);
                Assert.Equal(parentNode.NodeId, geometry.ParentNodeId);
                Assert.Equal(found.Bounds, geometry.Bounds);
                Assert.Equal(parentNode.Bounds, geometry.ParentBounds);
                Assert.Equal(new NodeBounds(clipped ? 140 : 50, 50, 80, 30), geometry.Bounds);
                Assert.Equal(new NodeBounds(30, 40, 140, 80), geometry.ParentBounds);
                Assert.Equal(clipped, geometry.IsClippedByParent);
                using var pixels = SKBitmap.Decode(frame.Screenshot!.FilePath);
                Assert.Equal(SKColors.Lime, pixels.GetPixel(clipped ? 150 : 60, 65));
                Assert.Equal(SKColors.White, pixels.GetPixel(175, 65));
                Assert.True(File.Exists(frame.GeometryOverlayPath));
            }
            var limited = await new RuntimeInteractionAnimationRunner().RunAsync(client, new(
                runtime.SessionId, top.TopLevelId,
                [new(RuntimeInteractionAnimationActions.Wait, targetNodeId: found.NodeId, frameOffsetsMs: [0])],
                outputDirectory: Path.Combine(output, "depth-limited"), maxDepth: 2));
            Assert.Equal("failed", limited.Value!.Status);
            Assert.Empty(Assert.Single(limited.Value.Steps[0].Frames).Geometry);
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task InteractionRecordingDoesNotSubstituteMissingOrReplacedTarget(bool replaced)
    {
        await WithWindow(async (runtime, window, button, top, node, client, output) =>
        {
            var parent = (Border)button.Parent!;
            window.Content = null;
            Control nested = parent;
            for (var depth = 0; depth < 12; depth++) nested = new Border { Child = nested };
            window.Content = nested;
            parent.Child = replaced ? new Button { Name = button.Name, Content = "Replacement" } : null;
            window.UpdateLayout(); using var frame = window.CaptureRenderedFrame();
            Assert.True((await client.VisualTreeAsync(runtime.SessionId, top.TopLevelId, 32)).Value!.ResponseBudget?.Truncated);
            var result = await new RuntimeInteractionAnimationRunner().RunAsync(client, new(
                runtime.SessionId, top.TopLevelId,
                [new(RuntimeInteractionAnimationActions.Wait, targetNodeId: node.NodeId, frameOffsetsMs: [0])],
                outputDirectory: output, maxDepth: 32));
            Assert.True(result.Success, JsonSerializer.Serialize(result.Error));
            Assert.Equal("failed", result.Value!.Status);
            Assert.Contains(result.Value.Diagnostics, d => d.Code == "interaction_geometry_target_not_found");
            Assert.Empty(Assert.Single(result.Value.Steps[0].Frames).Geometry);
        });
    }

    [Fact]
    public async Task SyntheticHoverCleanupPreservesPreexistingNativeHover()
    {
        await WithWindow(async (runtime, window, button, top, node, client, output) =>
        {
            var native = new Border { Width = 100, Height = 100, Background = Brushes.Blue };
            var synthetic = new Border { Width = 100, Height = 100, Background = Brushes.Red };
            var parent = new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal, Children = { native, synthetic } };
            window.Content = parent; window.UpdateLayout();
            using var frame = window.CaptureRenderedFrame();
            var nativePoint = native.TranslatePoint(new Point(50, 50), window)!.Value;
            var syntheticPoint = synthetic.TranslatePoint(new Point(50, 50), window)!.Value;
            window.MouseMove(nativePoint);
            Assert.True(native.IsPointerOver); Assert.True(parent.IsPointerOver);
            var moved = await client.InputAsync(runtime.SessionId, top.TopLevelId, InputActions.PointerMove, syntheticPoint.X, syntheticPoint.Y);
            Assert.True(moved.Success); Assert.True(synthetic.IsPointerOver);
            var cleared = await client.InputAsync(runtime.SessionId, top.TopLevelId, InputActions.PointerMove, -1, -1);
            Assert.True(cleared.Success); Assert.False(synthetic.IsPointerOver);
            Assert.True(native.IsPointerOver); Assert.True(parent.IsPointerOver);
            window.MouseMove(new Point(-1, -1));
            Assert.False(native.IsPointerOver); Assert.False(parent.IsPointerOver);
        });
    }

    [Fact]
    public async Task SyntheticHoverRetainsPressedPointerCaptureUntilRelease()
    {
        await WithWindow(async (runtime, window, button, top, node, client, output) =>
        {
            var pad = new Border { Background = Brushes.Blue };
            window.Content = pad; window.UpdateLayout();
            using var frame = window.CaptureRenderedFrame();
            IPointer? hovered = null; IPointer? pressed = null; IPointer? released = null;
            var heldMoves = 0;
            pad.PointerEntered += (_, e) => hovered = e.Pointer;
            pad.PointerPressed += (_, e) => { pressed = e.Pointer; e.Pointer.Capture(pad); };
            pad.PointerMoved += (_, e) =>
            {
                if (e.GetCurrentPoint(pad).Properties.IsLeftButtonPressed)
                { Assert.Same(pressed, e.Pointer); heldMoves++; }
            };
            pad.PointerReleased += (_, e) => released = e.Pointer;
            foreach (var action in new[] { InputActions.PointerMove, InputActions.PointerDown })
                Assert.True((await client.InputAsync(runtime.SessionId, top.TopLevelId, action, 50, 50)).Success);
            Assert.Same(hovered, pressed); Assert.NotNull(pressed); Assert.Same(pad, pressed.Captured);
            Assert.True((await client.InputAsync(runtime.SessionId, top.TopLevelId, InputActions.PointerMove, -1, -1)).Success);
            Assert.Equal(1, heldMoves); Assert.False(pad.IsPointerOver);
            Assert.True((await client.InputAsync(runtime.SessionId, top.TopLevelId, InputActions.PointerUp, -1, -1)).Success);
            Assert.Same(pressed, released); Assert.Null(pressed.Captured);
        });
    }

    [Fact]
    public async Task PseudoStateHoverPressedAndDisabledRestoreWithoutClickingFullWindowButton()
    {
        await WithWindow(async (runtime, window, button, top, node, client, output) =>
        {
            ((Border)button.Parent!).Child = null;
            button.Width = double.NaN; button.Height = double.NaN;
            button.HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch;
            button.VerticalAlignment = Avalonia.Layout.VerticalAlignment.Stretch;
            window.Content = button; window.UpdateLayout();
            using var frame = window.CaptureRenderedFrame();
            var clicks = 0; button.Click += (_, _) => clicks++;
            var result = await new RuntimePseudoStateMatrixRunner().RunAsync(client, new(
                runtime.SessionId, top.TopLevelId, states: [RuntimePseudoStates.Normal, RuntimePseudoStates.PointerOver,
                    RuntimePseudoStates.Pressed, RuntimePseudoStates.Disabled], name: button.Name, outputDirectory: output));
            Assert.True(result.Success, JsonSerializer.Serialize(result.Error));
            Assert.Equal("passed", result.Value!.Status);
            Assert.Contains(":pointerover", result.Value.Entries[1].Target!.Classes);
            Assert.Contains(":pressed", result.Value.Entries[2].Target!.Classes);
            Assert.Contains(":disabled", result.Value.Entries[3].Target!.Classes);
            Assert.Equal(0, clicks); Assert.False(button.IsPointerOver); Assert.False(button.IsPressed);
            Assert.True(button.IsEnabled); Assert.False(window.IsPointerOver);
        });
    }

    [Theory]
    [InlineData("outside")]
    [InlineData("unregister")]
    [InlineData("deactivate")]
    public async Task SyntheticHoverTracksAncestorsAndClearsDetachedTargets(string cleanup)
    {
        await WithWindow(async (runtime, window, button, top, node, client, output) =>
        {
            var first = new Border { Width = 100, Height = 100, Background = Brushes.Blue };
            var second = new Border { Width = 100, Height = 100, Background = Brushes.Red };
            var parent = new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal, Children = { first, second } };
            var hoverWindow = new Window { Width = 240, Height = 160, Content = parent };
            hoverWindow.Show();
            using var registration = runtime.RegisterTopLevel(hoverWindow);
            try
            {
                using var frame = hoverWindow.CaptureRenderedFrame();
                var hoverTop = (await runtime.ListTopLevelsAsync()).Single(item => item.Id != top.TopLevelId).Id;
                var entered = 0; var exited = 0; var parentEntered = 0; var parentExited = 0;
                var exitOrder = new List<string>();
                first.PointerEntered += (_, _) => entered++;
                first.PointerExited += (_, _) => exited++;
                parent.PointerEntered += (_, _) => parentEntered++;
                second.PointerExited += (_, _) => exitOrder.Add("second");
                parent.PointerExited += (_, _) => { parentExited++; exitOrder.Add("parent"); };
                hoverWindow.PointerExited += (_, _) => exitOrder.Add("window");
                async Task Move(double x, double y)
                {
                    var moved = await client.InputAsync(runtime.SessionId, hoverTop, InputActions.PointerMove, x, y);
                    Assert.True(moved.Success, JsonSerializer.Serialize(moved.Error));
                }
                await Move(50, 50);
                Assert.True(first.IsPointerOver);
                Assert.True(parent.IsPointerOver);
                Assert.True(hoverWindow.IsPointerOver);
                Assert.Contains(":pointerover", first.Classes);
                await Move(55, 55);
                Assert.Equal(1, entered); Assert.Equal(1, parentEntered);
                await Move(150, 50);
                Assert.False(first.IsPointerOver); Assert.True(second.IsPointerOver);
                Assert.Equal(1, exited); Assert.Equal(0, parentExited);
                Assert.True(parent.IsPointerOver);
                // Exit must still reach an owned hovered element after it leaves the visual tree.
                parent.Children.Remove(second);
                if (cleanup == "outside") await Move(-1, -1);
                else if (cleanup == "unregister") registration.Dispose();
                else AvaScopeBridge.Deactivate();
                Assert.False(second.IsPointerOver);
                Assert.False(parent.IsPointerOver);
                Assert.False(hoverWindow.IsPointerOver);
                Assert.Equal(1, parentExited);
                Assert.Equal(new[] { "second", "parent", "window" }, exitOrder);
            }
            finally { hoverWindow.Close(); }
        });
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task PseudoStateCausalFailureAndAdvisorySuccessAgreeThroughCliAndMcp(bool useMcp, bool occluded)
    {
        await WithWindow(async (runtime, window, button, top, node, client, output) =>
        {
            var target = new Border { Name = "DiagnosticHoverTarget", Background = Brushes.Blue };
            var panel = new Grid { Children = { target } };
            if (occluded) panel.Children.Add(new Border { Background = Brushes.Red });
            window.Content = panel; window.UpdateLayout();
            using var frame = window.CaptureRenderedFrame();
            var found = Assert.Single((await client.FindNodesAsync(runtime.SessionId, top.TopLevelId, TreeKinds.Visual, name: target.Name)).Value!.Matches);
            var request = new RuntimePseudoStateMatrixRequest(runtime.SessionId, top.TopLevelId, found.Node.Target!,
                [RuntimePseudoStates.Normal, RuntimePseudoStates.PointerOver], outputDirectory: output);
            ToolResult<RuntimePseudoStateMatrixResponse> result;
            if (useMcp)
            {
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
                await using var mcp = await McpClient.CreateAsync(new StdioClientTransport(new()
                { Command = "dotnet", Arguments = [Path.Combine(AppContext.BaseDirectory, "AvaScope.Mcp.dll")], Name = "matrix-diagnostics-test" }), cancellationToken: timeout.Token);
                var call = await mcp.CallToolAsync("pseudo_state_matrix", new Dictionary<string, object?>
                { ["request"] = JsonSerializer.SerializeToElement(request), ["manifestDirectory"] = client.ManifestDirectory }, cancellationToken: timeout.Token);
                result = JsonSerializer.Deserialize<ToolResult<RuntimePseudoStateMatrixResponse>>(JsonSerializer.Serialize(call.StructuredContent))!;
            }
            else
            {
                Directory.CreateDirectory(output);
                var requestPath = Path.Combine(output, "request.json");
                await File.WriteAllTextAsync(requestPath, JsonSerializer.Serialize(request));
                var start = new ProcessStartInfo("dotnet") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
                foreach (var argument in new[] { Path.Combine(AppContext.BaseDirectory, "avascope.dll"), "pseudo-state-matrix", "--request", requestPath, "--manifest-dir", client.ManifestDirectory }) start.ArgumentList.Add(argument);
                using var process = Process.Start(start)!;
                var stdout = process.StandardOutput.ReadToEndAsync(); var stderr = process.StandardError.ReadToEndAsync();
                try
                {
                    await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(30));
                    result = JsonSerializer.Deserialize<ToolResult<RuntimePseudoStateMatrixResponse>>(await stdout)!;
                    Assert.True(string.IsNullOrWhiteSpace(await stderr), await stderr);
                    Assert.Equal(occluded ? 1 : 0, process.ExitCode);
                }
                finally
                {
                    if (!process.HasExited) { process.Kill(entireProcessTree: true); await process.WaitForExitAsync(); }
                }
            }

            Assert.True(result.TransportSuccess);
            Assert.Equal(!occluded, result.Success);
            Assert.Contains(result.Value!.Diagnostics, diagnostic => diagnostic.Code == "pseudo_state_raw_node_id_generation_scoped");
            var hover = result.Value.Entries[1];
            Assert.Equal(occluded ? "failed" : "passed", hover.Status);
            Assert.Equal(!occluded, hover.Target!.Classes.Contains(":pointerover"));
            using var pixels = SKBitmap.Decode(hover.Screenshot!.FilePath);
            Assert.Equal(occluded ? SKColors.Red : SKColors.Blue, pixels.GetPixel(150, 110));
            if (occluded)
            {
                Assert.Equal("pseudo_state_not_observed", result.Error!.Code);
                Assert.Equal(":pointerover", result.Error.Details!["expectedClass"]);
                Assert.Equal("true", result.Error.Details["partialValueAvailable"]);
            }
            else Assert.Null(result.Error);
            Assert.False(target.IsPointerOver);
            Assert.False(window.IsPointerOver);
            Assert.All(panel.Children, child => Assert.False(child.IsPointerOver));
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PseudoStateHoverRequiresRealStateAndResetsFullWindowTarget(bool occluded)
    {
        await WithWindow(async (runtime, window, button, top, node, client, output) =>
        {
            var target = new Border { Name = "HoverTarget" };
            target.Styles.Add(new Style(selector => selector.OfType<Border>())
                { Setters = { new Setter(Border.BackgroundProperty, Brushes.Blue) } });
            target.Styles.Add(new Style(selector => selector.OfType<Border>().Class(":pointerover"))
                { Setters = { new Setter(Border.BackgroundProperty, Brushes.Lime) } });
            var panel = new Grid { Children = { target } };
            if (occluded) panel.Children.Add(new Border { Background = Brushes.Red });
            window.Content = panel; window.UpdateLayout();
            using var frame = window.CaptureRenderedFrame();
            var result = await new RuntimePseudoStateMatrixRunner().RunAsync(client, new(
                runtime.SessionId, top.TopLevelId, states: [RuntimePseudoStates.Normal, RuntimePseudoStates.PointerOver],
                name: target.Name, outputDirectory: output));
            Assert.True(result.Success, JsonSerializer.Serialize(result.Error));
            var hover = result.Value!.Entries[1];
            if (occluded)
            {
                Assert.Equal("failed", hover.Status);
                Assert.Contains(hover.Diagnostics, diagnostic => diagnostic.Code == "pseudo_state_not_observed");
                Assert.DoesNotContain(":pointerover", hover.Target!.Classes);
            }
            else
            {
                Assert.Equal("passed", result.Value.Status);
                Assert.Contains(":pointerover", hover.Target!.Classes);
                using var normalPixels = SKBitmap.Decode(result.Value.Entries[0].Screenshot!.FilePath);
                using var hoverPixels = SKBitmap.Decode(hover.Screenshot!.FilePath);
                Assert.Equal(SKColors.Blue, normalPixels.GetPixel(150, 110));
                Assert.Equal(SKColors.Lime, hoverPixels.GetPixel(150, 110));
            }
            Assert.False(target.IsPointerOver);
            Assert.False(window.IsPointerOver);
            Assert.All(panel.Children, child => Assert.False(child.IsPointerOver));
        });
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task PseudoStateMatrixCapturesDeepTargetWithRealResponseBudget(bool pinnedTarget, bool replaceTarget)
    {
        await WithWindow(async (runtime, window, button, top, node, client, output) =>
        {
            window.Content = null;
            Control nested = button;
            // Detach from the original fixture parent before placing it below the inline depth budget.
            ((Border)button.Parent!).Child = null;
            for (var depth = 0; depth < 12; depth++) nested = new Border { Child = nested };
            window.Content = nested;
            window.UpdateLayout();
            using var frame = window.CaptureRenderedFrame();
            Assert.NotNull(frame);
            var found = await client.FindNodesAsync(runtime.SessionId, top.TopLevelId, TreeKinds.Visual,
                name: button.Name, maxDepth: 32, includeAccessibility: true);
            var current = Assert.Single(found.Value!.Matches).Node;
            var tree = await client.VisualTreeAsync(runtime.SessionId, top.TopLevelId, 32);
            Assert.True(tree.Value!.ResponseBudget?.Truncated);
            Assert.DoesNotContain(current.NodeId, JsonSerializer.Serialize(tree.Value.Root));
            var requestedTarget = current.Target;
            if (replaceTarget)
            {
                var parent = (Border)button.Parent!;
                button = new Button { Name = button.Name, Content = "Replacement", Width = 160, Height = 60 };
                parent.Child = button;
                window.UpdateLayout();
                using var replacementFrame = window.CaptureRenderedFrame();
                Assert.NotNull(replacementFrame);
                current = Assert.Single((await client.FindNodesAsync(runtime.SessionId, top.TopLevelId,
                    TreeKinds.Visual, name: button.Name, maxDepth: 32)).Value!.Matches).Node;
                Assert.NotEqual(requestedTarget!.NodeId, current.NodeId);
            }

            var result = await new RuntimePseudoStateMatrixRunner().RunAsync(client, new(
                runtime.SessionId, top.TopLevelId, pinnedTarget ? requestedTarget : null,
                [RuntimePseudoStates.Normal, RuntimePseudoStates.Disabled], outputDirectory: output,
                maxDepth: 32, name: !pinnedTarget || replaceTarget ? button.Name : null));
            Assert.True(result.Success && result.Value!.Status == "passed", JsonSerializer.Serialize(result));
            Assert.Equal(2, result.Value.Entries.Count);
            Assert.All(result.Value.Entries, entry =>
            {
                Assert.Equal("passed", entry.Status);
                Assert.Equal(current.NodeId, entry.Target!.NodeId);
                Assert.True(File.Exists(entry.Screenshot!.FilePath));
            });
            Assert.True(result.Value.Entries[0].Target!.AccessibilityState!.IsEnabled);
            Assert.False(result.Value.Entries[1].Target!.AccessibilityState!.IsEnabled);
            Assert.Single(result.Value.Entries[1].AppliedMutations);
            Assert.Single(result.Value.Entries[1].ResetMutations);
            Assert.True(button.IsEnabled);
            if (replaceTarget)
                Assert.Contains(result.Value.Diagnostics, diagnostic => diagnostic.Code == "pseudo_state_target_reresolved");
            if (pinnedTarget)
            {
                var stale = new RuntimeTargetContext(runtime.SessionId, top.TopLevelId, TreeKinds.Visual,
                    current.NodeId, topLevelGeneration: "wrong-generation", nodeGeneration: current.Target!.NodeGeneration);
                var refused = await new RuntimePseudoStateMatrixRunner().RunAsync(client, new(
                    runtime.SessionId, top.TopLevelId, stale, [RuntimePseudoStates.Disabled],
                    outputDirectory: Path.Combine(output, "stale"), maxDepth: 32));
                Assert.Equal("failed", refused.Value!.Status);
                var entry = Assert.Single(refused.Value.Entries);
                Assert.Empty(entry.AppliedMutations);
                Assert.Empty(entry.Inputs);
                Assert.Contains(entry.Diagnostics, diagnostic => diagnostic.Code == RuntimeInputErrorCodes.TargetStale);
                Assert.True(button.IsEnabled);
            }
        });
    }

    [Theory]
    [InlineData(false, InputActions.PointerMove)]
    [InlineData(true, InputActions.PointerMove)]
    [InlineData(false, InputActions.PointerDown)]
    [InlineData(true, InputActions.PointerDown)]
    [InlineData(false, InputActions.PointerUp)]
    [InlineData(true, InputActions.PointerUp)]
    [InlineData(false, InputActions.Drag)]
    [InlineData(true, InputActions.Drag)]
    [InlineData(false, InputActions.Focus)]
    [InlineData(true, InputActions.Focus)]
    public async Task LegacyInputSkipsHitTestInvisibleChildOrOverlay(bool overlay, string action)
    {
        await WithWindow(async (runtime, window, button, top, node, client, output) =>
        {
            var ignored = new Border { Name = "IgnoredHit", Width = 160, Height = 80,
                Background = Brushes.Red, IsHitTestVisible = false };
            var pad = new Border { Name = "InputPad", Width = 160, Height = 80,
                Background = Brushes.Blue, Focusable = true, RenderTransform = new TranslateTransform(12, 9) };
            Canvas.SetLeft(pad, 40); Canvas.SetTop(pad, 30);
            var canvas = new Canvas { Children = { pad }, ClipToBounds = true };
            if (overlay)
            {
                Canvas.SetLeft(ignored, 52); Canvas.SetTop(ignored, 39); canvas.Children.Add(ignored);
            }
            else pad.Child = ignored;
            window.Content = canvas; window.UpdateLayout();
            Assert.True((await runtime.ReadinessAsync(top.TopLevelId, options: new(waitForFrame: true))).Success);
            var padNode = Assert.Single((await client.FindNodesAsync(runtime.SessionId, top.TopLevelId, TreeKinds.Visual, name: pad.Name, maxDepth: 32)).Value!.Matches).Node;
            var point = pad.TranslatePoint(new(80, 40), window)!.Value;
            var excludedEvents = 0; var presses = 0; var releases = 0; var moves = 0; var captureLost = 0;
            IPointer? ownedPointer = null;
            ignored.PointerMoved += (_, _) => excludedEvents++;
            ignored.PointerPressed += (_, _) => excludedEvents++;
            ignored.PointerReleased += (_, _) => excludedEvents++;
            pad.PointerMoved += (_, _) => moves++;
            pad.PointerPressed += (_, e) => { presses++; ownedPointer = e.Pointer; e.Pointer.Capture(pad); };
            pad.PointerReleased += (_, _) => releases++;
            pad.PointerCaptureLost += (_, _) => captureLost++;

            var result = action == InputActions.Drag
                ? await client.InputAsync(runtime.SessionId, top.TopLevelId, action, targetNodeId: padNode.NodeId,
                    gesture: new(GestureDirections.Right, 50, durationMs: 50))
                : await client.InputAsync(runtime.SessionId, top.TopLevelId, action, x: point.X, y: point.Y);
            CoreResult<InputResponse>? pairedRelease = null;
            if (action == InputActions.PointerDown)
                pairedRelease = await client.InputAsync(runtime.SessionId, top.TopLevelId, InputActions.PointerUp, x: 5, y: 5);
            Assert.True(result.Success, JsonSerializer.Serialize(result.Error));
            Assert.True(excludedEvents == 0,
                $"{action}, overlay={overlay}: ignored visual received {excludedEvents} events; pad presses={presses}, releases={releases}, moves={moves}; response={JsonSerializer.Serialize(result.Value)}");
            Assert.Equal(padNode.NodeId, result.Value!.TargetNodeId);
            if (action == InputActions.PointerMove) Assert.Equal(1, moves);
            if (action == InputActions.PointerUp) Assert.Equal(1, releases);
            if (action is InputActions.PointerDown or InputActions.Drag)
            {
                if (pairedRelease is not null) Assert.True(pairedRelease.Success, JsonSerializer.Serialize(pairedRelease.Error));
                Assert.Equal(1, presses); Assert.Equal(1, releases); Assert.Equal(1, captureLost);
                Assert.NotNull(ownedPointer); Assert.Null(ownedPointer.Captured);
            }
            if (action == InputActions.Focus) Assert.Same(pad, window.FocusManager!.GetFocusedElement());
            if (action is InputActions.PointerMove or InputActions.PointerDown or InputActions.PointerUp)
                Assert.Equal("TopLevel.InputHitTest", result.Value.Metadata["hitTestSource"]);

            // A real input overlay remains the target; ignoring decorations must not bypass it.
            ignored.IsHitTestVisible = true;
            Assert.True((await runtime.ReadinessAsync(top.TopLevelId, options: new(waitForFrame: true))).Success);
            var intercepted = await client.InputAsync(runtime.SessionId, top.TopLevelId, InputActions.PointerMove, x: point.X, y: point.Y);
            Assert.True(intercepted.Success, JsonSerializer.Serialize(intercepted.Error));
            Assert.NotEqual(padNode.NodeId, intercepted.Value!.TargetNodeId); Assert.Equal(1, excludedEvents);
            ignored.IsHitTestVisible = false; pad.IsEnabled = false;
            var before = (presses, releases, moves, excludedEvents);
            var disabled = await client.InputAsync(runtime.SessionId, top.TopLevelId, InputActions.PointerDown,
                x: point.X, y: point.Y, targetNodeId: padNode.NodeId);
            Assert.False(disabled.Success); Assert.Equal(before, (presses, releases, moves, excludedEvents));
        });
    }

    [Fact]
    public async Task InputTransparentOverlayDoesNotContradictClickActionability()
    {
        await WithWindow(async (runtime, window, button, top, node, client, output) =>
        {
            button = new Button { Name = "ActionButton", Width = 160, Height = 60, Content = "Run" };
            var ignored = new Border { Width = button.Width, Height = button.Height,
                Background = Brushes.Red, IsHitTestVisible = false };
            window.Content = new Grid { Children = { button, ignored } };
            var clicks = 0; button.Click += (_, _) => clicks++;
            window.UpdateLayout();
            Assert.True((await runtime.ReadinessAsync(top.TopLevelId, options: new(waitForFrame: true))).Success);
            var point = button.TranslatePoint(new(button.Bounds.Width / 2, button.Bounds.Height / 2), window)!.Value;
            var click = await client.InputAsync(runtime.SessionId, top.TopLevelId, InputActions.Click, x: point.X, y: point.Y);
            Assert.True(click.Success, JsonSerializer.Serialize(click.Error)); Assert.Equal(1, clicks);
            var observed = Assert.Single((await client.FindNodesAsync(runtime.SessionId, top.TopLevelId, TreeKinds.Visual, name: button.Name, maxDepth: 32)).Value!.Matches).Node;
            Assert.True(observed.InteractionState!.Actionable, "A successful actual click contradicts the input-transparent overlay's actionable=false snapshot.");
            Assert.Equal("TopLevel.InputHitTest", click.Value!.Metadata["hitTestSource"]);

            ignored.IsHitTestVisible = true;
            Assert.True((await runtime.ReadinessAsync(top.TopLevelId, options: new(waitForFrame: true))).Success);
            var blocked = await client.InputAsync(runtime.SessionId, top.TopLevelId, InputActions.Click, x: point.X, y: point.Y);
            Assert.False(blocked.Success); Assert.Equal(1, clicks);
            observed = Assert.Single((await client.FindNodesAsync(runtime.SessionId, top.TopLevelId, TreeKinds.Visual, name: button.Name, maxDepth: 32)).Value!.Matches).Node;
            Assert.False(observed.InteractionState!.Actionable);
        });
    }

    [Fact]
    public async Task PointerDiagnosticsUsesActualHitPathAcrossTransformsOverlaysAndClipping()
    {
        await WithWindow(async (runtime, window, button, top, node, client, output) =>
        {
            var pad = new Border { Name = "PointerPad", Width = 120, Height = 60, Background = Brushes.Blue,
                RenderTransform = new TranslateTransform(20, 10) };
            Canvas.SetLeft(pad, 40); Canvas.SetTop(pad, 30);
            var overlay = new Border { Name = "PointerOverlay", Width = 120, Height = 60, Background = Brushes.Red, IsVisible = false };
            Canvas.SetLeft(overlay, 60); Canvas.SetTop(overlay, 40);
            var canvas = new Canvas { Width = 200, Height = 120, ClipToBounds = true, Children = { pad, overlay } };
            window.Content = new Grid { Children = { new Border { Background = Brushes.White, Name = "Background" }, canvas } };
            window.UpdateLayout(); await runtime.ReadinessAsync(top.TopLevelId, options: new(waitForFrame: true));
            var padNode = Assert.Single((await client.FindNodesAsync(runtime.SessionId, top.TopLevelId, TreeKinds.Visual, name: "PointerPad", maxDepth: 32)).Value!.Matches).Node;
            var point = pad.TranslatePoint(new(60, 30), window)!.Value;
            var geometry = Pick(await client.PickNodeAsync(new(top))).Geometry;
            var actual = Pick(await client.PickNodeAsync(new(top, point.X, point.Y, "top_level_dip", geometry.Revision, maxPath: 32)));
            Assert.Equal(padNode.NodeId, actual.HitPath[0].Target.NodeId);
            var presses = 0; pad.PointerPressed += (_, _) => presses++;

            async Task<RuntimePointerDiagnosticsResponse> Probe(string expected, int depth = 32)
            {
                var result = await new RuntimePointerDiagnosticsRunner().RunAsync(client, new(runtime.SessionId, top.TopLevelId,
                    [new(RuntimePointerPathActions.Move, x: point.X, y: point.Y), new(RuntimePointerPathActions.AssertHit, expectedNodeId: expected)],
                    maxDepth: depth, includeAllTopLevels: false));
                Assert.True(result.Success, JsonSerializer.Serialize(result.Error));
                return result.Value!;
            }

            var direct = await Probe(padNode.NodeId);
            Assert.Equal("passed", direct.Status);
            Assert.Equal(padNode.NodeId, direct.Steps.Last().ActiveLayer!.HitTestPath.Last().NodeId);
            Assert.DoesNotContain(direct.Diagnostics, error => error.Code == "runtime_pointer_input_hit_path_mismatch");
            // A shallow nearest-node tree must not truncate the independent runtime hit test.
            Assert.Equal("passed", (await Probe(padNode.NodeId, depth: 1)).Status);

            overlay.IsVisible = true; window.UpdateLayout(); await runtime.ReadinessAsync(top.TopLevelId, options: new(waitForFrame: true));
            var overlayNode = Assert.Single((await client.FindNodesAsync(runtime.SessionId, top.TopLevelId, TreeKinds.Visual, name: "PointerOverlay", maxDepth: 32)).Value!.Matches).Node;
            Assert.Equal("passed", (await Probe(overlayNode.NodeId)).Status);
            Assert.Equal("failed", (await Probe(padNode.NodeId)).Status);
            overlay.IsHitTestVisible = false;
            await runtime.ReadinessAsync(top.TopLevelId, options: new(waitForFrame: true));
            Assert.Equal("passed", (await Probe(padNode.NodeId)).Status);

            // This point stays in the child's transformed bounds but falls outside its clipping parent.
            pad.RenderTransform = new TranslateTransform(120, 10);
            window.UpdateLayout(); await runtime.ReadinessAsync(top.TopLevelId, options: new(waitForFrame: true));
            point = pad.TranslatePoint(new(60, 30), window)!.Value;
            Assert.Equal("failed", (await Probe(padNode.NodeId)).Status);
            Assert.Equal(0, presses);
        });
    }

    [Fact]
    public async Task CurrentHitPathUsesExplicitGeometryWithoutDispatchingInputOrFocus()
    {
        await WithWindow(async (runtime, window, button, top, node, client, output) =>
        {
            var clicks = 0; var moves = 0; button.Click += (_, _) => clicks++; window.PointerMoved += (_, _) => moves++;
            var focused = window.FocusManager?.GetFocusedElement();
            var geometry = Pick(await client.PickNodeAsync(new(top))).Geometry;
            var point = button.TranslatePoint(new(button.Bounds.Width / 2, button.Bounds.Height / 2), window)!.Value;
            foreach (var space in new[] { "top_level_dip", "top_level_pixel" })
            {
                var scale = space == "top_level_pixel" ? geometry.RenderScaling : 1;
                var result = Pick(await client.PickNodeAsync(new(top, point.X * scale, point.Y * scale, space, geometry.Revision)));
                Assert.Equal("picked", result.Status); Assert.Contains(result.HitPath, item => item.Target.NodeId == node.NodeId);
                Assert.Equal(point.X, result.TopLevelPoint!.X, 6); Assert.Equal(point.Y, result.TopLevelPoint.Y, 6);
                Assert.All(result.HitPath, item => Assert.NotNull(item.Target.NodeGeneration));
                Assert.Contains("unverified", result.Occlusion);
            }
            var outside = Pick(await client.PickNodeAsync(new(top, -2, -2, expectedGeometryRevision: geometry.Revision)));
            Assert.Equal("outside", outside.Status); Assert.Empty(outside.HitPath);
            Assert.False(OperationResultMapper.IsSuccessful(CoreResult<RuntimePickResponse>.Ok(outside)));
            var desktop = await client.PickNodeAsync(new(top, 50, 50, "desktop", geometry.Revision));
            Assert.Equal("pick_coordinates_unsupported", desktop.Error!.Code);
            Assert.Equal(focused, window.FocusManager?.GetFocusedElement()); Assert.Equal(0, clicks); Assert.Equal(0, moves);
        });
    }

    [Fact]
    public async Task ChangedGeometryGenerationAndProtectedAncestorsCannotSelectStaleContent()
    {
        await WithWindow(async (runtime, window, button, top, node, client, output) =>
        {
            var old = Pick(await client.PickNodeAsync(new(top))).Geometry;
            window.Width += 20; window.UpdateLayout();
            Assert.Equal("pick_geometry_changed", (await client.PickNodeAsync(new(top, 20, 20, expectedGeometryRevision: old.Revision))).Error!.Code);
            var stale = new RuntimeTargetContext(top.SessionId, top.TopLevelId, topLevelGeneration: "old");
            Assert.Equal("pick_stale", (await client.PickNodeAsync(new(stale))).Error!.Code);
            await runtime.ReadinessAsync(top.TopLevelId, options: new(waitForFrame: true));
            var geometry = Pick(await client.PickNodeAsync(new(top))).Geometry;
            var center = button.TranslatePoint(new(button.Bounds.Width / 2, button.Bounds.Height / 2), window)!.Value;
            var policy = new RuntimeEvidencePolicy(output, excludedControlAutomationIds: ["protected-group"]);
            var excluded = Pick(await client.PickNodeAsync(new(top, center.X, center.Y, "top_level_dip", geometry.Revision, policy: policy)));
            Assert.Equal("excluded", excluded.Status); Assert.Empty(excluded.HitPath); Assert.DoesNotContain("Sensitive button", JsonSerializer.Serialize(excluded));
            Assert.Equal("highlight_excluded", (await client.HighlightAsync(new(node, policy: policy))).Error!.Code);
            var truncated = Pick(await client.PickNodeAsync(new(top, center.X, center.Y, "top_level_dip", geometry.Revision, maxPath: 1)));
            Assert.True(truncated.Truncated); Assert.Single(truncated.HitPath);
            Assert.Equal("inspection_policy_denied", (await runtime.PickNodeAsync(new(top, policy: new(output, authorizedProcessIds: [int.MaxValue])))).Error!.Code);
        });
    }

    [Fact]
    public async Task HighlightIsInputTransparentFollowsBoundsAndScreenshotsClearIt()
    {
        await WithWindow(async (runtime, window, button, top, node, client, output) =>
        {
            var layer = AdornerLayer.GetAdornerLayer(button)!; Assert.NotNull(layer); var before = layer.Children.Count;
            var baseline = await client.CaptureScreenshotAsync(runtime.SessionId, top.TopLevelId, Path.Combine(output, "before.png")); Assert.True(baseline.Success);
            var focused = window.FocusManager?.GetFocusedElement();
            var show = Highlight(await client.HighlightAsync(new(node, lifetimeMs: 5000)));
            Assert.Equal("active", show.Status); Assert.True(show.InputTransparent); Assert.True(show.ClearedByScreenshot);
            Assert.Equal(before + 1, layer.Children.Count);
            var overlay = layer.Children.Last(); Assert.False(overlay.IsHitTestVisible); Assert.False(overlay.Focusable);
            window.UpdateLayout(); await runtime.ReadinessAsync(top.TopLevelId, options: new(waitForFrame: true));
            var center = button.TranslatePoint(new(button.Bounds.Width / 2, button.Bounds.Height / 2), window)!.Value;
            Assert.NotEqual(overlay, window.InputHitTest(center)); Assert.Equal(focused, window.FocusManager?.GetFocusedElement());
            button.Margin = new Thickness(20, 10, 0, 0); window.UpdateLayout();
            var moved = Highlight(await client.HighlightAsync(new(top, "inspect")));
            Assert.NotEqual(show.Bounds, moved.Bounds);
            button.Margin = default; window.UpdateLayout();
            var after = await client.CaptureScreenshotAsync(runtime.SessionId, top.TopLevelId, Path.Combine(output, "after.png")); Assert.True(after.Success);
            Assert.Equal("absent", Highlight(await client.HighlightAsync(new(top, "inspect"))).Status); Assert.Equal(before, layer.Children.Count);
            Assert.Equal(File.ReadAllBytes(baseline.Value!.FilePath), File.ReadAllBytes(after.Value!.FilePath));
            Highlight(await client.HighlightAsync(new(node, lifetimeMs: 5000)));
            var observation = await new RuntimeObserver().ObserveAsync(client, new(runtime.SessionId, [top.TopLevelId], includeScreenshot: true, outputDirectory: output));
            Assert.True(observation.Success); Assert.Equal(before, layer.Children.Count);
            Highlight(await client.HighlightAsync(new(node, lifetimeMs: 5000)));
            var pair = await client.CaptureScreenAsync(new(top, output, "rendered")); Assert.True(pair.Success); Assert.Equal(before, layer.Children.Count);
            var otherButton = new Button { Name = "OtherButton", Content = "Other", Width = 100, Height = 40 };
            var other = new Window { Width = 200, Height = 120, Content = otherButton }; other.Show();
            try
            {
                using var registration = runtime.RegisterTopLevel(other); Dispatcher.UIThread.RunJobs();
                var otherId = (await runtime.ListTopLevelsAsync()).Single(item => item.Id != top.TopLevelId).Id;
                var otherNode = Assert.Single((await client.FindNodesAsync(runtime.SessionId, otherId, TreeKinds.Visual, name: "OtherButton")).Value!.Matches).Node.Target!;
                var otherLayer = AdornerLayer.GetAdornerLayer(otherButton)!; var otherBefore = otherLayer.Children.Count;
                Highlight(await client.HighlightAsync(new(otherNode, lifetimeMs: 5000)));
                Highlight(await client.HighlightAsync(new(node, lifetimeMs: 5000)));
                var paired = await client.CaptureScreenAsync(new(top, output)); Assert.True(paired.Success);
                Assert.Equal("captured", paired.Value!.Rendered!.Status);
                Assert.Equal("native_screen_scope_denied", Assert.Single(paired.Value.Native!.Diagnostics).Code);
                Assert.Equal(before, layer.Children.Count); Assert.Equal(otherBefore, otherLayer.Children.Count);
            }
            finally { other.Close(); }
        });
    }

    [Fact]
    public async Task OverlayPopupPickingUsesTheOwnersActualHitRoot()
    {
        await WithWindow(async (runtime, window, button, top, node, client, output) =>
        {
            var content = (Border)window.Content!; content.Child = null;
            var popupButton = new Button { Name = "PopupButton", Content = "Popup target", Width = 100, Height = 30 };
            var popup = new Popup { Child = popupButton, PlacementTarget = button, Placement = PlacementMode.Center, ShouldUseOverlayLayer = true };
            content.Child = new Grid { Children = { button, popup } };
            popup.IsOpen = true; Dispatcher.UIThread.RunJobs();
            try
            {
                Assert.True(popup.IsUsingOverlayLayer);
                var root = TopLevel.GetTopLevel(popupButton)!; Assert.NotNull(root);
                Assert.Same(window, root);
                var id = top.TopLevelId;
                await runtime.ReadinessAsync(id, options: new(waitForFrame: true));
                var found = Assert.Single((await client.FindNodesAsync(runtime.SessionId, id, TreeKinds.Visual, name: "PopupButton")).Value!.Matches).Node;
                var selected = new RuntimeTargetContext(runtime.SessionId, id, topLevelGeneration: found.Target!.TopLevelGeneration);
                var geometry = Pick(await client.PickNodeAsync(new(selected))).Geometry;
                var point = popupButton.TranslatePoint(new(popupButton.Bounds.Width / 2, popupButton.Bounds.Height / 2), root)!.Value;
                var picked = Pick(await client.PickNodeAsync(new(selected, point.X, point.Y, "top_level_dip", geometry.Revision)));
                Assert.Equal("picked", picked.Status); Assert.Contains(picked.HitPath, item => item.Target.NodeId == found.NodeId);
                Assert.All(picked.HitPath, item => Assert.Equal(id, item.Target.TopLevelId));
            }
            finally { popup.IsOpen = false; }
        });
    }

    [Fact]
    public async Task ExpiryClearDetachAndSessionCloseReleaseOwnedAdorners()
    {
        await WithWindow(async (runtime, window, button, top, node, client, output) =>
        {
            var layer = AdornerLayer.GetAdornerLayer(button)!; var original = layer.Children.Count;
            var first = Highlight(await client.HighlightAsync(new(node, lifetimeMs: 100)));
            await Task.Delay(180); Dispatcher.UIThread.RunJobs();
            Assert.Equal("absent", Highlight(await client.HighlightAsync(new(top, "inspect"))).Status); Assert.Equal(original, layer.Children.Count);
            var second = Highlight(await client.HighlightAsync(new(node, lifetimeMs: 5000))); Assert.NotEqual(first.HighlightId, second.HighlightId);
            Highlight(await client.HighlightAsync(new(top, "clear"))); Assert.Equal(original, layer.Children.Count);
            Highlight(await client.HighlightAsync(new(node, lifetimeMs: 5000)));
            var originalContent = window.Content;
            window.Content = new Border(); Dispatcher.UIThread.RunJobs(); Assert.Equal(original, layer.Children.Count);
            Assert.False((await client.HighlightAsync(new(node))).Success);
            window.Content = originalContent; window.UpdateLayout(); await runtime.ReadinessAsync(top.TopLevelId, options: new(waitForFrame: true));
            var found = await client.FindNodesAsync(runtime.SessionId, top.TopLevelId, TreeKinds.Visual, name: "PickButton");
            var current = Assert.Single(found.Value!.Matches).Node.Target!;
            Highlight(await client.HighlightAsync(new(current, lifetimeMs: 5000)));
            AvaScopeBridge.Deactivate(); Assert.Equal(original, layer.Children.Count);
        });
    }

    [Fact]
    public async Task CliPicksAndMcpHighlightsWithLeaseAndModalEvidence()
    {
        await WithWindow(async (runtime, window, button, top, node, client, output) =>
        {
            var geometry = Pick(await client.PickNodeAsync(new(top))).Geometry;
            var center = button.TranslatePoint(new(button.Bounds.Width / 2, button.Bounds.Height / 2), window)!.Value;
            var request = new RuntimePickRequest(top, center.X, center.Y, "top_level_dip", geometry.Revision);
            Directory.CreateDirectory(output); var path = Path.Combine(output, "pick.json"); File.WriteAllText(path, JsonSerializer.Serialize(request));
            var start = new ProcessStartInfo("dotnet") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
            foreach (var arg in new[] { Path.Combine(AppContext.BaseDirectory, "avascope.dll"), "pick-node", "--request", path, "--manifest-dir", client.ManifestDirectory }) start.ArgumentList.Add(arg);
            using var process = Process.Start(start)!; var stdout = process.StandardOutput.ReadToEndAsync(); var stderr = process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(30));
            var response = JsonSerializer.Deserialize<ToolResult<RuntimePickResponse>>(await stdout)!;
            Assert.True(response.Success, await stdout + await stderr); Assert.Equal(0, process.ExitCode);
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            await using var mcp = await McpClient.CreateAsync(new StdioClientTransport(new()
            { Command = "dotnet", Arguments = [Path.Combine(AppContext.BaseDirectory, "AvaScope.Mcp.dll")], Name = "pick-highlight-test" }), cancellationToken: timeout.Token);
            var call = await mcp.CallToolAsync("highlight", new Dictionary<string, object?>
            { ["request"] = JsonSerializer.SerializeToElement(new RuntimeHighlightRequest(node, lifetimeMs: 5000)), ["manifestDirectory"] = client.ManifestDirectory }, cancellationToken: timeout.Token);
            var highlighted = JsonSerializer.Deserialize<ToolResult<RuntimeHighlightResponse>>(JsonSerializer.Serialize(call.StructuredContent))!;
            Assert.True(highlighted.Success); Assert.Equal("active", highlighted.Value!.Status);
            Assert.True((await client.SessionControlAsync(runtime.SessionId, new("acquire", "highlight-owner"))).Success);
            var observer = new LocalBridgeClient(client.ManifestDirectory);
            Assert.Equal("session_control_conflict", (await observer.HighlightAsync(new(top, "clear"))).Error!.Code);
            Assert.True((await observer.HighlightAsync(new(top, "inspect"))).Success);
            Assert.True((await observer.PickNodeAsync(new(top))).Success);
            Highlight(await client.HighlightAsync(new(top, "clear")));
            var modal = new Window { Width = 100, Height = 80 }; var modalTask = modal.ShowDialog(window);
            try
            {
                using var registered = runtime.RegisterTopLevel(modal); Dispatcher.UIThread.RunJobs();
                await runtime.ReadinessAsync(top.TopLevelId, options: new(waitForFrame: true));
                geometry = Pick(await client.PickNodeAsync(new(top))).Geometry;
                var picked = Pick(await client.PickNodeAsync(new(top, center.X, center.Y, "top_level_dip", geometry.Revision)));
                Assert.Equal("modal_blocks_selected_owner", picked.Occlusion); Assert.Single(picked.RelatedTopLevels);
            }
            finally { modal.Close(); await modalTask; }
        });
    }

    [Fact]
    public async Task EmptyHighlightCleanupDoesNotRequireAUiDispatcherTurn()
    {
        await WithWindow(async (runtime, window, button, top, node, client, output) =>
        {
            window.Close();
            var closed = Task.Run(AvaScopeBridge.Deactivate);
            var completedWithoutDispatcher = closed.Wait(TimeSpan.FromSeconds(1));
            // If this regresses, yielding lets the pending cleanup finish before reporting failure.
            await closed;
            Assert.True(completedWithoutDispatcher, "An empty highlight registry waited for UI work during shutdown.");
        });
    }

    private static RuntimePickResponse Pick(CoreResult<RuntimePickResponse> result)
    { Assert.True(result.Success, JsonSerializer.Serialize(result.Error)); return result.Value!; }
    private static RuntimeHighlightResponse Highlight(CoreResult<RuntimeHighlightResponse> result)
    { Assert.True(result.Success, JsonSerializer.Serialize(result.Error)); return result.Value!; }
    private static async Task WithWindow(Func<AvaScopeBridgeRuntime, Window, Button, RuntimeTargetContext, RuntimeTargetContext, LocalBridgeClient, string, Task> test)
    {
        var session = HeadlessUnitTestSession.StartNew(typeof(BridgeHeadlessSmokeTests.BridgeHeadlessTestApplication));
        var output = Path.Combine(Path.GetTempPath(), "avascope-picking-" + Guid.NewGuid().ToString("N"));
        try
        {
            await BridgeHeadlessSmokeTests.DispatchAsync(session, async () =>
            {
                AvaScopeBridge.Deactivate(); var runtime = AvaScopeBridge.Activate();
                var button = new Button { Name = "PickButton", Content = "Sensitive button", Width = 160, Height = 60 };
                var border = new Border { Name = "Group", Padding = new Thickness(25), Child = button }; AutomationProperties.SetAutomationId(border, "protected-group");
                var window = new Window { Width = 300, Height = 220, Content = border };
                try
                {
                    window.Show(); using var registration = runtime.RegisterTopLevel(window); Dispatcher.UIThread.RunJobs();
                    var client = new LocalBridgeClient(Path.GetDirectoryName(runtime.SessionManifestPath)!);
                    var top = Assert.Single(await runtime.ListTopLevelsAsync()).Id;
                    using var preparedFrame = window.CaptureRenderedFrame();
                    Assert.NotNull(preparedFrame);
                    Assert.Equal(new PixelSize(300, 220), preparedFrame.PixelSize);
                    var readiness = await runtime.ReadinessAsync(top, options: new(waitForFrame: true));
                    Assert.True(readiness.Success, JsonSerializer.Serialize(readiness));
                    var pinned = (await client.WindowAsync(new(new(runtime.SessionId, top)))).Value!.After!.Target;
                    var found = await client.FindNodesAsync(runtime.SessionId, top, TreeKinds.Visual, name: "PickButton");
                    await test(runtime, window, button, pinned, Assert.Single(found.Value!.Matches).Node.Target!, client, output);
                }
                finally { window.Close(); AvaScopeBridge.Deactivate(); }
            }, CancellationToken.None);
        }
        finally
        {
            // Failed dispatch continuations can run on the worker that Dispose joins.
            await Task.Run(() => BridgeHeadlessSmokeTests.DisposeHeadlessSessionAfterExplicitCleanup(session));
            if (Directory.Exists(output)) Directory.Delete(output, true);
        }
    }
}
