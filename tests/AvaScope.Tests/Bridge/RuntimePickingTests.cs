using System.Diagnostics;
using System.Text.Json;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using AvaScope.Bridge;
using AvaScope.Core;
using AvaScope.Protocol;
using ModelContextProtocol.Client;

namespace AvaScope.Tests.Bridge;

[Collection(BridgeCollectionDefinition.Name)]
public sealed class RuntimePickingTests
{
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
