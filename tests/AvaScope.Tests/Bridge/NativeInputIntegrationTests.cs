using System.Diagnostics;
using System.Text.Json;
using AvaScope.Core;
using AvaScope.Protocol;
using ModelContextProtocol.Client;

namespace AvaScope.Tests.Bridge;

public sealed class NativeInputFactAttribute : FactAttribute
{
    public NativeInputFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("AVASCOPE_NATIVE_INPUT_HOST")))
            Skip = "Run eng/test-native-input.ps1 to publish and select the native host/provider fixture.";
    }
}

[Collection(BridgeCollectionDefinition.Name)]
public sealed class NativeInputIntegrationTests
{
    [NativeInputFact]
    public async Task OwnedNativeInputAndDialogsWorkThroughActualCliAndMcp()
    {
        var host = Path.GetFullPath(Environment.GetEnvironmentVariable("AVASCOPE_NATIVE_INPUT_HOST")!);
        var provider = Path.GetFullPath(Environment.GetEnvironmentVariable("AVASCOPE_NATIVE_INPUT_PROVIDER")!);
        var output = Path.GetFullPath(Environment.GetEnvironmentVariable("AVASCOPE_NATIVE_INPUT_OUTPUT")!);
        Directory.CreateDirectory(output);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(110));
        var token = timeout.Token;
        await using var desktop = OperatingSystem.IsLinux() ? new X11TestEnvironment(new X11EnvironmentOptions(WindowManager: true, SessionBus: true), Path.Combine(output, "desktop")) : null;
        if (desktop is not null)
        {
            var started = await desktop.StartAsync(token);
            Assert.True(started.Success, started.Error?.Message);
        }
        var start = new ProcessStartInfo("dotnet") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var argument in new[] { host, "--input-fixture", "--gtk-picker", "--exit-after-ms=120000" }) start.ArgumentList.Add(argument);
        start.Environment["UI_INSPECTION_PROVIDER_PATH"] = provider;
        if (desktop is not null) foreach (var pair in desktop.EnvironmentVariables) start.Environment[pair.Key] = pair.Value;
        using var process = Process.Start(start)!;
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        // A manifest can precede the first cold native window/layout initialization.
        var client = new LocalBridgeClient(null, TimeSpan.FromSeconds(15));
        BridgeSessionManifest? manifest = null;
        try
        {
            for (var attempt = 0; attempt < 200 && manifest is null && !process.HasExited; attempt++)
            {
                manifest = client.ListSessionManifests().SingleOrDefault(item => item.ProcessId == process.Id);
                if (manifest is null) await Task.Delay(50, token);
            }
            Assert.NotNull(manifest);
            var sessionId = manifest.SessionId;
            var levels = await client.ListTopLevelsAsync(sessionId, token);
            Assert.True(levels.Success, levels.Error?.Message);
            var top = Assert.Single(levels.Value!.TopLevels);
            var backend = OperatingSystem.IsWindows() ? "win32" : OperatingSystem.IsLinux() ? "x11" : "macos";
            Assert.Equal(backend, top.Backend!.Backend);
            var route = backend == "win32" ? RuntimeOperationRoutes.Win32WindowMessage : backend == "x11" ? RuntimeOperationRoutes.X11WindowEvent : RuntimeOperationRoutes.AppKitWindowEvent;
            var pad = await Node("NativePad");
            var editor = await Node("NativeEditor");
            var environment = StdioClientTransportOptions.GetDefaultEnvironmentVariables();
            if (Environment.GetEnvironmentVariable("TMPDIR") is { } temporary) environment["TMPDIR"] = temporary;
            await using var mcp = await McpClient.CreateAsync(new StdioClientTransport(new StdioClientTransportOptions
            {
                Name = "Native input validation", Command = "dotnet", Arguments = [Path.Combine(AppContext.BaseDirectory, "AvaScope.Mcp.dll")],
                InheritEnvironmentVariables = false, EnvironmentVariables = environment, ShutdownTimeout = TimeSpan.FromSeconds(3)
            }), cancellationToken: token);

            var clickOptions = new InputExecutionOptions { Strategy = "native", Button = "right", ClickCount = 2, IntervalMs = 75 };
            await CliInput(InputActions.Click, pad.NodeId, clickOptions, "Shift");
            await WaitText("InputState", "RightButtonPressed:2:Shift");
            await WaitText("ReleaseState", "Right");
            var middle = await mcp.CallToolAsync("input", new Dictionary<string, object?>
            {
                ["sessionId"] = sessionId.Value, ["topLevelId"] = top.Id, ["action"] = "click", ["targetNodeId"] = pad.NodeId,
                ["execution"] = JsonSerializer.SerializeToElement(clickOptions with { Button = "middle", ClickCount = 3 })
            }, cancellationToken: token);
            var middleResult = JsonSerializer.Deserialize<ToolResult<InputResponse>>(JsonSerializer.Serialize(middle.StructuredContent))!;
            await File.WriteAllTextAsync(Path.Combine(output, "middle-input-result.json"), JsonSerializer.Serialize(middleResult), token);
            Assert.True(middleResult.Success, middleResult.Error?.Message);
            Assert.Equal(route, middleResult.Value!.Provenance!.Route);
            await WaitText("InputState", "MiddleButtonPressed:3:None");
            await WaitText("ReleaseState", "Middle");
            var dragOptions = new InputExecutionOptions { Strategy = "native", DestinationX = 16 + pad.Bounds!.X + pad.Bounds.Width * .75,
                DestinationY = 16 + pad.Bounds.Y + pad.Bounds.Height * .5, DurationMs = 160, MotionSteps = 4 };
            var drag = await client.InputAsync(sessionId, top.Id, InputActions.Drag, targetNodeId: pad.NodeId, execution: dragOptions, cancellationToken: token);
            if (backend == "macos")
            {
                Assert.False(drag.Success);
                Assert.Contains("held-button motion", drag.Error!.Message);
                Assert.Equal("0", drag.Error.Details!["dispatchedEvents"]);
                drag = await client.InputAsync(sessionId, top.Id, InputActions.Drag, targetNodeId: pad.NodeId,
                    execution: dragOptions with { Strategy = "synthetic" }, cancellationToken: token);
            }
            Assert.True(drag.Success, drag.Error?.Message);
            await WaitText("MotionState", "True:False:False");
            await WaitText("ReleaseState", "Left");
            var key = await client.InputAsync(sessionId, top.Id, InputActions.KeySequence, targetNodeId: editor.NodeId,
                execution: new() { Strategy = "native", Keys = [new("Left", "Shift")] }, cancellationToken: token);
            Assert.True(key.Success, key.Error?.Message);
            Assert.Equal(route, key.Value!.Provenance!.Route);
            await WaitText("KeyState", "Left:Shift");
            var literal = await client.InputAsync(sessionId, top.Id, InputActions.KeyText, targetNodeId: editor.NodeId,
                inputText: "<Ctrl+A> árvíz 😀", execution: new() { Strategy = "native" }, cancellationToken: token);
            if (backend == "x11")
            {
                Assert.False(literal.Success);
                Assert.Contains("literal Unicode", literal.Error!.Message);
                literal = await client.InputAsync(sessionId, top.Id, InputActions.KeyText, targetNodeId: editor.NodeId,
                    inputText: "<Ctrl+A> árvíz 😀", execution: new(), cancellationToken: token);
            }
            Assert.True(literal.Success, literal.Error?.Message);
            await WaitText("NativeEditor", "<Ctrl+A> árvíz 😀");

            // Desired state uses the advertised Avalonia route on the real desktop backend.
            var desired = new RuntimeDesiredStateRequest((await Node("NativeEditor")).Target!, "text",
                JsonSerializer.SerializeToElement("desired-state árvíz 😀"), "native-desired-state");
            var desiredPath = Path.Combine(output, "desired-state-request.json");
            await File.WriteAllTextAsync(desiredPath, JsonSerializer.Serialize(desired), token);
            var desiredStart = new ProcessStartInfo("dotnet") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
            foreach (var argument in new[] { Path.Combine(AppContext.BaseDirectory, "avascope.dll"), "ensure-state", "--request", desiredPath })
                desiredStart.ArgumentList.Add(argument);
            using (var cli = Process.Start(desiredStart)!)
            {
                var resultText = cli.StandardOutput.ReadToEndAsync(token); var errorText = cli.StandardError.ReadToEndAsync(token);
                await cli.WaitForExitAsync(token);
                var result = JsonSerializer.Deserialize<ToolResult<RuntimeDesiredStateResponse>>(await resultText)!;
                await File.WriteAllTextAsync(Path.Combine(output, "cli-desired-state.json"), await resultText, token);
                Assert.True(result.Success, result.Error?.Message + await errorText); Assert.Equal(0, cli.ExitCode);
                Assert.Equal(backend, result.Value!.Provenance.Backend.Backend);
                Assert.Equal(RuntimeOperationRoutes.RoutedEvent, result.Value.Provenance.Route);
            }
            var replay = await mcp.CallToolAsync("ensure_state", new Dictionary<string, object?>
            { ["request"] = JsonSerializer.SerializeToElement(desired) }, cancellationToken: token);
            var desiredReplay = JsonSerializer.Deserialize<ToolResult<RuntimeDesiredStateResponse>>(JsonSerializer.Serialize(replay.StructuredContent))!;
            await File.WriteAllTextAsync(Path.Combine(output, "mcp-desired-state.json"), JsonSerializer.Serialize(desiredReplay), token);
            Assert.True(desiredReplay.Success); Assert.True(desiredReplay.Value!.Replayed);
            var desiredAgain = await client.EnsureStateAsync(new(desired.Target, desired.Property, desired.Desired, "native-desired-no-op"), token);
            Assert.Equal("already_satisfied", desiredAgain.Value!.Status); Assert.Equal(0, desiredAgain.Value.DispatchedOperations);
            await WaitText("NativeEditor", "desired-state árvíz 😀");

            var form = new RuntimeFormInspectionRequest(sessionId, top.Id, new(name: "NativeEditor"));
            var formCall = await mcp.CallToolAsync("inspect_form", new Dictionary<string, object?>
            { ["request"] = JsonSerializer.SerializeToElement(form) }, cancellationToken: token);
            var inventory = JsonSerializer.Deserialize<ToolResult<RuntimeFormInspectionResponse>>(JsonSerializer.Serialize(formCall.StructuredContent))!;
            Assert.True(inventory.Success); Assert.Single(inventory.Value!.Fields);
            var fill = new RuntimeFormFillRequest(form,
                [new("editor", new(name: "NativeEditor"), JsonSerializer.SerializeToElement("form árvíz 😀"))], "native-form-fill");
            var fillPath = Path.Combine(output, "form-fill-request.json");
            await File.WriteAllTextAsync(fillPath, JsonSerializer.Serialize(fill), token);
            var fillStart = new ProcessStartInfo("dotnet") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
            foreach (var argument in new[] { Path.Combine(AppContext.BaseDirectory, "avascope.dll"), "fill-form", "--request", fillPath }) fillStart.ArgumentList.Add(argument);
            using (var cli = Process.Start(fillStart)!)
            {
                var resultText = cli.StandardOutput.ReadToEndAsync(token); var errorText = cli.StandardError.ReadToEndAsync(token);
                await cli.WaitForExitAsync(token);
                var result = JsonSerializer.Deserialize<ToolResult<RuntimeFormFillResponse>>(await resultText)!;
                await File.WriteAllTextAsync(Path.Combine(output, "cli-form-fill.json"), await resultText, token);
                Assert.True(result.Success, result.Error?.Message + await errorText); Assert.Equal(0, cli.ExitCode);
                Assert.Equal(backend, Assert.Single(result.Value!.Fields).Execution!.Provenance.Backend.Backend);
                Assert.False(result.Value.Submitted); Assert.False(result.Value.RolledBack);
            }
            formCall = await mcp.CallToolAsync("fill_form", new Dictionary<string, object?>
            { ["request"] = JsonSerializer.SerializeToElement(fill) }, cancellationToken: token);
            var fillReplay = JsonSerializer.Deserialize<ToolResult<RuntimeFormFillResponse>>(JsonSerializer.Serialize(formCall.StructuredContent))!;
            await File.WriteAllTextAsync(Path.Combine(output, "mcp-form-fill.json"), JsonSerializer.Serialize(fillReplay), token);
            Assert.True(fillReplay.Success); Assert.True(fillReplay.Value!.Replayed);
            await WaitText("NativeEditor", "form árvíz 😀");

            var wrongTarget = new RuntimeTargetContext(new SessionId("unrelated-session"), top.Id, TreeKinds.Visual, pad.NodeId);
            Assert.False((await client.InputAsync(sessionId, top.Id, InputActions.Click, targetNodeId: pad.NodeId,
                inputTarget: wrongTarget, execution: clickOptions, cancellationToken: token)).Success);
            await Invoke("OtherWindow");
            await Task.Delay(150, token);

            var otherLevels = await client.ListTopLevelsAsync(sessionId, token);
            var other = Assert.Single(otherLevels.Value!.TopLevels, item => item.Id != top.Id);
            var lost = await client.InputAsync(sessionId, top.Id, InputActions.KeySequence, targetNodeId: editor.NodeId,
                execution: new() { Strategy = "native", Keys = [new("Enter")] }, cancellationToken: token);
            Assert.False(lost.Success);
            var returnNode = Assert.Single((await client.FindNodesAsync(sessionId, other.Id, TreeKinds.Visual, name: "ReturnToMain", cancellationToken: token)).Value!.Matches).Node;
            Assert.True((await client.InputAsync(sessionId, other.Id, InputActions.Invoke, targetNodeId: returnNode.NodeId, cancellationToken: token)).Success);
            await Task.Delay(150, token);

            await Invoke("ArmInterruptedDrag");
            var interrupted = await client.InputAsync(sessionId, top.Id, backend == "macos" ? InputActions.Click : InputActions.Drag, targetNodeId: pad.NodeId,
                execution: backend == "macos" ? new() { Strategy = "native", ClickCount = 3, IntervalMs = 100 } : dragOptions with { DurationMs = 1000 }, cancellationToken: token);
            Assert.False(interrupted.Success);
            Assert.Equal("released", interrupted.Error!.Details!["cleanup"]);
            otherLevels = await client.ListTopLevelsAsync(sessionId, token);
            other = Assert.Single(otherLevels.Value!.TopLevels, item => item.Id != top.Id);
            returnNode = Assert.Single((await client.FindNodesAsync(sessionId, other.Id, TreeKinds.Visual, name: "ReturnToMain", cancellationToken: token)).Value!.Matches).Node;
            Assert.True((await client.InputAsync(sessionId, other.Id, InputActions.Invoke, targetNodeId: returnNode.NodeId, cancellationToken: token)).Success);
            await Task.Delay(150, token);

            var file = Path.Combine(output, "native-árvíz.txt");
            await File.WriteAllTextAsync(file, "fixture", token);
            // Deterministic app logic consumes a correlated value and opens no native dialog.
            Assert.True(client.NativePicker(sessionId, NativePickerOperations.PredefineResult, file, correlationId: "open-document").Success);
            await Invoke("PreparedPicker");
            await WaitText("PickerState", "predefined:native-árvíz.txt");
            await Invoke("PreparedPicker");
            await WaitText("PickerState", "predefined:not_prepared");

            // A real native cancel is validated independently from the predefined-result path.
            await Invoke("OpenFilePicker");
            var detected = await Picker("detect");
            Assert.True(detected.DialogDetected);
            var wrongOwner = client.NativePicker(sessionId, "cancel", topLevelId: "unrelated-window", timeoutMs: 500);
            Assert.False(wrongOwner.Success);
            Assert.Equal("cancelled", (await Picker("cancel")).Status);
            await WaitText("PickerState", "cancelled");

            // Public AppKit selection supports save panels; GTK3 and Win32 also select an existing open file.
            await Invoke(backend == "macos" ? "SaveFilePicker" : "OpenFilePicker");
            Assert.True((await Picker("detect")).DialogDetected);
            var selectedPath = backend == "macos" ? Path.Combine(output, "native-save.txt") : file;
            Assert.Equal("path_selected", (await Picker("select_path", selectedPath)).Status);
            if (backend == "macos")
            {
                var confirm = client.NativePicker(sessionId, "confirm", topLevelId: top.Id, timeoutMs: 1000);
                Assert.False(confirm.Success);
                Assert.Contains("does not permit programmatic confirmation", confirm.Error!.Message);
                Assert.True((await Picker("detect")).DialogDetected);
                Assert.Equal("cancelled", (await Picker("cancel")).Status);
                await WaitText("PickerState", "cancelled");
            }
            else
            {
                Assert.Equal("confirmed", (await Picker("confirm")).Status);
                await WaitText("PickerState", "selected:" + Path.GetFileName(selectedPath));
            }

            await File.WriteAllTextAsync(Path.Combine(output, "validation.json"), JsonSerializer.Serialize(new
            {
                status = "passed", backend, nativeInputRoute = route, nativeDialog = backend == "x11" ? "gtk3" : backend,
                checks = new[] { "CLI right double modifier click", "MCP middle triple click",
                    backend == "macos" ? "native drag refused before dispatch; explicit synthetic drag" : "bounded native drag",
                    backend == "macos" ? "interrupted native click cleanup" : "interrupted native drag cleanup",
                    "paired native navigation chord", "literal Unicode capability", "CLI desired text; MCP replay; no-op verification", "MCP form inventory; CLI fill; MCP replay", "focus loss", "wrong session", "wrong picker owner", "explicit correlated one-shot host result", "real native cancel", "real native select and confirm" }
            }), token);

            async Task<TreeNodeSummary> Node(string name) => Assert.Single((await client.FindNodesAsync(sessionId, top.Id, TreeKinds.Visual,
                name: name, cancellationToken: token)).Value!.Matches).Node;
            async Task WaitText(string name, string expected)
            {
                string? actual = null;
                for (var attempt = 0; attempt < 100; attempt++)
                {
                    actual = (await Node(name)).Text;
                    if (actual == expected) return;
                    await Task.Delay(30, token);
                }
                Assert.Equal(expected, actual);
            }
            async Task Invoke(string name)
            {
                var node = await Node(name);
                var invoked = await client.InputAsync(sessionId, top.Id, InputActions.Invoke, targetNodeId: node.NodeId, cancellationToken: token);
                Assert.True(invoked.Success, invoked.Error?.Message);
            }
            async Task<NativePickerResponse> Picker(string operation, string? path = null)
            {
                var call = await mcp.CallToolAsync("native_picker", new Dictionary<string, object?>
                {
                    ["sessionId"] = sessionId.Value, ["topLevelId"] = top.Id, ["operation"] = operation, ["path"] = path, ["timeoutMs"] = 3000
                }, cancellationToken: token);
                var result = JsonSerializer.Deserialize<ToolResult<NativePickerResponse>>(JsonSerializer.Serialize(call.StructuredContent))!;
                await File.WriteAllTextAsync(Path.Combine(output, "picker-" + operation + ".json"), JsonSerializer.Serialize(result), token);
                Assert.True(result.Success, result.Error?.Message);
                Assert.NotEqual("app_predefined_result", result.Value!.Route);
                return result.Value;
            }
            async Task CliInput(string action, string node, InputExecutionOptions options, string modifiers)
            {
                var path = Path.Combine(output, "input-options.json");
                await File.WriteAllTextAsync(path, JsonSerializer.Serialize(options), token);
                var command = new ProcessStartInfo("dotnet") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
                foreach (var argument in new[] { Path.Combine(AppContext.BaseDirectory, "avascope.dll"), "input", "--session", sessionId.Value, "--top-level", top.Id,
                    "--action", action, "--target-node", node, "--modifiers", modifiers, "--execution", path }) command.ArgumentList.Add(argument);
                using var cli = Process.Start(command)!;
                var outputTask = cli.StandardOutput.ReadToEndAsync(token);
                var errorTask = cli.StandardError.ReadToEndAsync(token);
                await cli.WaitForExitAsync(token);
                var json = await outputTask;
                await File.WriteAllTextAsync(Path.Combine(output, "cli-input-result.json"), json, token);
                var result = JsonSerializer.Deserialize<ToolResult<InputResponse>>(json)!;
                Assert.True(result.Success, result.Error?.Message + await errorTask);
                Assert.Equal(route, result.Value!.Provenance!.Route);
            }
        }
        finally
        {
            await File.WriteAllTextAsync(Path.Combine(output, "host-state-before-cleanup.log"),
                process.HasExited ? "Exited: " + process.ExitCode : "Still running");
            if (manifest is not null && !process.HasExited)
                await client.CloseSessionAsync(manifest.SessionId);
            if (!process.HasExited) process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync();
            await File.WriteAllTextAsync(Path.Combine(output, "host.stdout.log"), await stdout);
            await File.WriteAllTextAsync(Path.Combine(output, "host.stderr.log"), await stderr);
        }
    }
}
