using System.Diagnostics;
using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using AvaScope.Bridge;
using AvaScope.Core;
using AvaScope.Protocol;
using ModelContextProtocol.Client;

namespace AvaScope.Tests.Bridge;

[Collection(BridgeCollectionDefinition.Name)]
public sealed class ExplicitInputTests
{
    [Fact]
    public async Task CompoundClicksChordsAndLiteralTextUseTheRequestedRouteAcrossCliAndMcp()
    {
        await WithWindow(async (runtime, pad, editor, top, client) =>
        {
            var presses = new List<(MouseButton, int, KeyModifiers)>();
            pad.PointerPressed += (_, args) => presses.Add((args.GetCurrentPoint(pad).Properties.PointerUpdateKind == PointerUpdateKind.RightButtonPressed
                ? MouseButton.Right : args.GetCurrentPoint(pad).Properties.PointerUpdateKind == PointerUpdateKind.MiddleButtonPressed ? MouseButton.Middle : MouseButton.Left, args.ClickCount, args.KeyModifiers));
            var padId = await Node("Pad");
            var editorId = await Node("Editor");
            var options = new InputExecutionOptions { Button = "right", ClickCount = 3, IntervalMs = 10 };
            var dry = await client.ValidateInputAsync(runtime.SessionId, top, InputActions.Click, targetNodeId: padId, keyModifiers: "Shift", execution: options);
            Assert.True(dry.Success, dry.Error?.Message);
            Assert.False(dry.Value!.Provenance!.Dispatched);
            Assert.Empty(presses);
            var click = await client.InputAsync(runtime.SessionId, top, InputActions.Click, targetNodeId: padId, keyModifiers: "Shift", execution: options);
            Assert.True(click.Success, click.Error?.Message);
            Assert.Equal(new[] { (MouseButton.Right, 1, KeyModifiers.Shift), (MouseButton.Right, 2, KeyModifiers.Shift), (MouseButton.Right, 3, KeyModifiers.Shift) }, presses);
            Assert.Equal(RuntimeOperationRoutes.SyntheticPointer, click.Value!.Provenance!.Route);
            Assert.Equal("no_owned_input_held", click.Value.Metadata["cleanup"]);
            var keys = new List<(Key, KeyModifiers)>();
            editor.AddHandler(InputElement.KeyDownEvent, (_, args) => keys.Add((args.Key, args.KeyModifiers)), RoutingStrategies.Tunnel);
            var keyOptions = new InputExecutionOptions { Keys = [new("A", "Control"), new("Left", "Shift")], IntervalMs = 0 };
            var sequence = await client.InputAsync(runtime.SessionId, top, InputActions.KeySequence, targetNodeId: editorId, execution: keyOptions);
            Assert.True(sequence.Success, sequence.Error?.Message);
            Assert.Equal(new[] { (Key.A, KeyModifiers.Control), (Key.Left, KeyModifiers.Shift) }, keys);
            editor.Text = "";
            const string literal = "<Ctrl+A> árvíz 😀";
            var typed = await client.InputAsync(runtime.SessionId, top, InputActions.KeyText, targetNodeId: editorId, inputText: literal, execution: new());
            Assert.True(typed.Success, typed.Error?.Message);
            Assert.Equal(literal, editor.Text);
            Assert.Equal(RuntimeOperationRoutes.SyntheticKey, typed.Value!.Provenance!.Route);

            var file = Path.Combine(Path.GetTempPath(), $"avascope-input-{Guid.NewGuid():N}.json");
            try
            {
                await File.WriteAllTextAsync(file, JsonSerializer.Serialize(options with { Button = "middle", ClickCount = 2 }));
                var start = new ProcessStartInfo("dotnet") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
                foreach (var argument in new[] { Path.Combine(AppContext.BaseDirectory, "avascope.dll"), "input", "--session", runtime.SessionId.Value, "--top-level", top, "--action", "click", "--target-node", padId, "--execution", file, "--manifest-dir", client.ManifestDirectory }) start.ArgumentList.Add(argument);
                using var process = Process.Start(start)!;
                var stdout = process.StandardOutput.ReadToEndAsync();
                var stderr = process.StandardError.ReadToEndAsync();
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
                await process.WaitForExitAsync(timeout.Token);
                var cli = JsonSerializer.Deserialize<ToolResult<InputResponse>>(await stdout)!;
                Assert.True(cli.Success, cli.Error?.Message + await stderr);
                Assert.Equal("middle", cli.Value!.PointerButton);
                var environment = StdioClientTransportOptions.GetDefaultEnvironmentVariables();
                if (Environment.GetEnvironmentVariable("TMPDIR") is { } temporary) environment["TMPDIR"] = temporary;
                await using var mcp = await McpClient.CreateAsync(new StdioClientTransport(new StdioClientTransportOptions
                {
                    Name = "Explicit input parity", Command = "dotnet", Arguments = [Path.Combine(AppContext.BaseDirectory, "AvaScope.Mcp.dll")],
                    InheritEnvironmentVariables = false, EnvironmentVariables = environment, ShutdownTimeout = TimeSpan.FromSeconds(3)
                }), cancellationToken: timeout.Token);
                var call = await mcp.CallToolAsync("input", new Dictionary<string, object?>
                {
                    ["sessionId"] = runtime.SessionId.Value, ["topLevelId"] = top, ["action"] = "key_sequence", ["targetNodeId"] = editorId,
                    ["execution"] = JsonSerializer.SerializeToElement(keyOptions), ["manifestDirectory"] = client.ManifestDirectory
                }, cancellationToken: timeout.Token);
                var actual = JsonSerializer.Deserialize<ToolResult<InputResponse>>(JsonSerializer.Serialize(call.StructuredContent))!;
                Assert.True(actual.Success, actual.Error?.Message);
                Assert.Equal(sequence.Value!.Provenance!.Route, actual.Value!.Provenance!.Route);
                var workflow = await new SemanticWorkflowRunner().RunAsync(client, new SemanticWorkflowRequest(runtime.SessionId, top,
                    [new SemanticWorkflowStep(SemanticWorkflowActions.KeySequence, selector: new(name: "Editor"), inputExecution: keyOptions)]), timeout.Token);
                Assert.True(workflow.Success, workflow.Error?.Message);
                Assert.Equal("passed", workflow.Value!.Status);
                Assert.Equal(RuntimeOperationRoutes.SyntheticKey, workflow.Value.Steps[0].Input!.Provenance!.Route);
            }
            finally { File.Delete(file); }

            async Task<string> Node(string name) => Assert.Single((await runtime.FindNodesAsync(top, TreeKinds.Visual, name: name)).Value!.Matches).Node.NodeId;
        });
    }

    [Fact]
    public async Task InterruptedDragReleasesItsOwnPointerAndMotionIsReproducible()
    {
        await WithWindow(async (runtime, pad, _, top, _) =>
        {
            var path = new List<(double X, double Y)>();
            IPointer? owned = null;
            var released = 0;
            pad.PointerPressed += (_, args) => { owned = args.Pointer; owned.Capture(pad); };
            pad.PointerMoved += (_, args) => { var p = args.GetPosition(pad); path.Add((p.X, p.Y)); };
            pad.PointerReleased += (_, _) => released++;
            var options = new InputExecutionOptions { DestinationX = 180, DestinationY = 50, MotionSteps = 4, DurationMs = 80, MotionProfile = "ease_in_out" };
            for (var index = 0; index < 2; index++)
            {
                var result = await runtime.InputAsync(top, InputActions.Drag, x: 20, y: 50, execution: options);
                Assert.True(result.Success, result.Error?.Message);
                Assert.Null(owned!.Captured);
            }
            Assert.Equal(path.Take(4), path.Skip(4));
            Assert.Equal(2, released);
            using var cancellation = new CancellationTokenSource();
            pad.PointerPressed += (_, _) => cancellation.Cancel();
            var cancelled = await runtime.InputAsync(top, InputActions.Drag, x: 20, y: 50, execution: options, cancellationToken: cancellation.Token);
            Assert.False(cancelled.Success);
            Assert.Equal("released", cancelled.Error!.Details!["cleanup"]);
            Assert.Equal(3, released);
            Assert.Null(owned!.Captured);
        });
    }

    [Fact]
    public async Task UnsupportedRoutesInvalidSequencesAndDetachedTargetsFailBeforeEffects()
    {
        await WithWindow(async (runtime, pad, editor, top, _) =>
        {
            var events = 0;
            pad.PointerPressed += (_, _) => events++;
            editor.KeyDown += (_, _) => events++;
            var id = Assert.Single((await runtime.FindNodesAsync(top, TreeKinds.Visual, name: "Pad")).Value!.Matches).Node.NodeId;
            foreach (var options in new[] { new InputExecutionOptions { Strategy = "native" }, new() { ClickCount = 4 }, new() { MotionSteps = 121 }, new() { Strategy = "semantic", Button = "right" } })
            {
                var refused = await runtime.InputAsync(top, InputActions.Click, targetNodeId: id, execution: options);
                Assert.False(refused.Success);
                Assert.Equal("0", refused.Error!.Details!["dispatchedEvents"]);
                Assert.Equal("not_needed", refused.Error.Details["cleanup"]);
            }
            var editorId = Assert.Single((await runtime.FindNodesAsync(top, TreeKinds.Visual, name: "Editor")).Value!.Matches).Node.NodeId;
            var badSequence = await runtime.InputAsync(top, InputActions.KeySequence, targetNodeId: editorId,
                execution: new() { Keys = [new("A"), new("DefinitelyNotAKey")] });
            Assert.False(badSequence.Success);
            editor.Focus();
            Assert.False((await runtime.InputAsync(top, InputActions.KeySequence, targetNodeId: "visual:missing",
                execution: new() { Keys = [new("Enter")] })).Success);
            pad.IsEnabled = false;
            Assert.False((await runtime.InputAsync(top, InputActions.Click, targetNodeId: id, execution: new())).Success);
            Assert.Equal(0, events);
            var picker = await runtime.NativePickerAsync(new(top, "detect", TimeoutMs: 0));
            Assert.False(picker.Success);
            Assert.Contains("no supported native dialog backend", picker.Error!.Message);
        });
    }

    [Fact]
    public async Task ExplicitHostPickerHookConsumesOnlyItsOwnCorrelationOnce()
    {
        await WithWindow(async (runtime, _, _, _, client) =>
        {
            var path = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "literal-árvíz.txt"));
            var prepared = client.NativePicker(runtime.SessionId, NativePickerOperations.PredefineResult, path,
                correlationId: "open-document");
            Assert.True(prepared.Success, prepared.Error?.Message);
            Assert.Equal("app_predefined_result", prepared.Value!.Route);
            Assert.True(prepared.Value.PathRedacted);
            var wrong = JsonSerializer.Deserialize<NativePickerResponse>(Bootstrap.TakePreparedPickerResult("other"))!;
            Assert.Equal(NativePickerResultStates.NotPrepared, wrong.Status);
            var first = JsonSerializer.Deserialize<NativePickerResponse>(Bootstrap.TakePreparedPickerResult("open-document"))!;
            Assert.Equal(path, first.SelectedPath);
            Assert.Equal("app_predefined_result", first.Route);
            Assert.Equal(NativePickerResultStates.NotPrepared, JsonSerializer.Deserialize<NativePickerResponse>(Bootstrap.TakePreparedPickerResult("open-document"))!.Status);
            await Task.CompletedTask;
        });
        Assert.Throws<InvalidOperationException>(() => Bootstrap.TakePreparedPickerResult("open-document"));
    }

    private static async Task WithWindow(Func<AvaScopeBridgeRuntime, Border, TextBox, string, LocalBridgeClient, Task> test)
    {
        using var session = HeadlessUnitTestSession.StartNew(typeof(BridgeHeadlessSmokeTests.BridgeHeadlessTestApplication));
        await BridgeHeadlessSmokeTests.DispatchAsync(session, async () =>
        {
            AvaScopeBridge.Deactivate();
            var runtime = AvaScopeBridge.Activate();
            var pad = new Border { Name = "Pad", Background = Brushes.White, Height = 100 };
            var editor = new TextBox { Name = "Editor", Text = "start" };
            var window = new Window { Width = 300, Height = 220, Content = new StackPanel { Children = { pad, editor } } };
            try
            {
                window.Show();
                using var registration = runtime.RegisterTopLevel(window);
                Dispatcher.UIThread.RunJobs();
                var top = Assert.Single(await runtime.ListTopLevelsAsync());
                Assert.True((await runtime.ReadinessAsync(top.Id, options: new(waitForFrame: true))).Success);
                await test(runtime, pad, editor, top.Id, new LocalBridgeClient(Path.GetDirectoryName(runtime.SessionManifestPath)!));
            }
            finally { window.Close(); AvaScopeBridge.Deactivate(); }
        }, CancellationToken.None);
    }
}
