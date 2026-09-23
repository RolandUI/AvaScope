using System.Diagnostics;
using System.Text.Json;
using AvaScope.Core;
using AvaScope.Protocol;
using ModelContextProtocol.Client;

namespace AvaScope.Tests.Bridge;

[Collection(BridgeCollectionDefinition.Name)]
public sealed class NativeInputIntegrationTestsWindowManagement
{
    [NativeInputFact]
    public async Task OwnedNativeWindowsRestoreMoveResizeAndExposeModalAndLeaseRefusals()
    {
        var host = Path.GetFullPath(Environment.GetEnvironmentVariable("AVASCOPE_NATIVE_INPUT_HOST")!);
        var provider = Path.GetFullPath(Environment.GetEnvironmentVariable("AVASCOPE_NATIVE_INPUT_PROVIDER")!);
        var output = Path.Combine(Path.GetFullPath(Environment.GetEnvironmentVariable("AVASCOPE_NATIVE_INPUT_OUTPUT")!), "windows");
        var manifests = Path.Combine(output, "sessions"); Directory.CreateDirectory(manifests);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(90)); var token = timeout.Token;
        await using var desktop = OperatingSystem.IsLinux() ? new X11TestEnvironment(new(WindowManager: true, SessionBus: true), Path.Combine(output, "desktop")) : null;
        if (desktop is not null) { var ready = await desktop.StartAsync(token); Assert.True(ready.Success, ready.Error?.Message); }
        var start = new ProcessStartInfo("dotnet") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var argument in new[] { host, "--exit-after-ms=100000" }) start.ArgumentList.Add(argument);
        start.Environment["UI_INSPECTION_PROVIDER_PATH"] = provider; start.Environment[BridgeSessionManifest.DirectoryEnvironmentVariable] = manifests;
        start.Environment["AVASCOPE_NATIVE_STARTUP_TRACE"] = "1";
        if (desktop is not null) foreach (var pair in desktop.EnvironmentVariables) start.Environment[pair.Key] = pair.Value;
        using var process = Process.Start(start)!;
        var stdout = process.StandardOutput.ReadToEndAsync(); var stderr = process.StandardError.ReadToEndAsync();
        var client = new LocalBridgeClient(manifests, TimeSpan.FromSeconds(15)); BridgeSessionManifest? manifest = null;
        var evidence = new List<RuntimeWindowResponse>();
        try
        {
            var startup = Stopwatch.StartNew();
            // The first native Avalonia process on a cold CI runner can outlive the warm 10 s budget.
            while (startup.Elapsed < TimeSpan.FromSeconds(30) && manifest is null && !process.HasExited)
            { manifest = client.ListSessionManifests().SingleOrDefault(item => item.ProcessId == process.Id); if (manifest is null) await Task.Delay(50, token); }
            await File.WriteAllTextAsync(Path.Combine(output, "startup.json"), JsonSerializer.Serialize(new
            {
                elapsedMs = startup.ElapsedMilliseconds, processId = process.Id,
                manifestCreated = manifest is not null, processExited = process.HasExited
            }), token);
            Assert.True(manifest is not null, "Native host did not publish a manifest within 30 seconds; inspect startup.json and host.stderr.log.");
            var levels = await client.ListTopLevelsAsync(manifest.SessionId, token); Assert.True(levels.Success, levels.Error?.Message);
            var top = Assert.Single(levels.Value!.TopLevels); var target = new RuntimeTargetContext(manifest.SessionId, top.Id);
            var initial = await Inspect();
            for (var attempt = 0; attempt < 100 && !initial.AvailableActions.Contains("minimize"); attempt++)
            { await Task.Delay(50, token); initial = await Inspect(); }
            Assert.Contains("minimize", initial.AvailableActions);
            Assert.NotEmpty(initial.Monitors);
            Assert.Equal(OperatingSystem.IsMacOS() ? "cocoa_desktop_points" : "physical_desktop_pixels", initial.DesktopUnits);
            if (OperatingSystem.IsMacOS()) Assert.All(initial.Monitors, monitor => Assert.Null(monitor.PhysicalPixelBounds));
            await Change("minimize"); await Change("restore"); await Change("maximize"); await Change("restore");

            var current = await Inspect();
            var monitor = current.Monitors.FirstOrDefault(item => item.Primary) ?? current.Monitors[0];
            var move = new RuntimeWindowRequest(current.Target, "move", current.Revision, position: new(40, 45, "monitor_dip", monitor.Id), timeoutMs: 3000);
            var requestPath = Path.Combine(output, "window-request.json");
            await File.WriteAllTextAsync(requestPath, JsonSerializer.Serialize(move), token);
            var command = new ProcessStartInfo("dotnet") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
            foreach (var arg in new[] { Path.Combine(AppContext.BaseDirectory, "avascope.dll"), "window", "--request", requestPath, "--manifest-dir", manifests }) command.ArgumentList.Add(arg);
            using (var cli = Process.Start(command)!)
            {
                var outputTask = cli.StandardOutput.ReadToEndAsync(token); var errorTask = cli.StandardError.ReadToEndAsync(token);
                await cli.WaitForExitAsync(token); var json = await outputTask;
                await File.WriteAllTextAsync(Path.Combine(output, "cli-move.json"), json, token);
                var response = JsonSerializer.Deserialize<ToolResult<RuntimeWindowResponse>>(json)!;
                Assert.True(response.Success, json + await errorTask); Assert.Equal(0, cli.ExitCode); evidence.Add(response.Value!);
            }
            current = await Inspect();
            var resolved = RuntimeWindowGeometry.ResolvePosition(move.Position!, current.Monitors).Value!;
            Assert.Equal(resolved.X, current.Position.X); Assert.Equal(resolved.Y, current.Position.Y);

            var environment = TestEnvironment.McpEnvironment();
            if (Environment.GetEnvironmentVariable("TMPDIR") is { } temporary) environment["TMPDIR"] = temporary;
            await using var mcp = await McpClient.CreateAsync(new StdioClientTransport(new()
            {
                Command = "dotnet", Arguments = [Path.Combine(AppContext.BaseDirectory, "AvaScope.Mcp.dll")], Name = "Owned window validation",
                InheritEnvironmentVariables = false, EnvironmentVariables = environment, ShutdownTimeout = TimeSpan.FromSeconds(3)
            }), cancellationToken: token);
            var resize = new RuntimeWindowRequest(current.Target, "resize", current.Revision, clientSize: new(540, 460), timeoutMs: 3000);
            var call = await mcp.CallToolAsync("window", new Dictionary<string, object?>
            { ["request"] = JsonSerializer.SerializeToElement(resize), ["manifestDirectory"] = manifests }, cancellationToken: token);
            var resized = JsonSerializer.Deserialize<ToolResult<RuntimeWindowResponse>>(JsonSerializer.Serialize(call.StructuredContent))!;
            await File.WriteAllTextAsync(Path.Combine(output, "mcp-resize.json"), JsonSerializer.Serialize(resized), token);
            Assert.True(resized.Success, JsonSerializer.Serialize(resized)); evidence.Add(resized.Value!);
            Assert.InRange(Math.Abs(resized.Value!.After!.ClientSize.Width - 540), 0, 1);
            Assert.Equal(RuntimeOperationRoutes.WindowApi, resized.Value.Provenance!.Route);
            var stale = OperationResultMapper.ToToolResult(await client.WindowAsync(resize, token));
            Assert.False(stale.Success); Assert.Equal("window_stale", stale.Error!.Code); Assert.Equal(0, stale.Value!.DispatchedOperations);

            var geometry = await client.PickNodeAsync(new(current.Target), token);
            // Use the current window target for picking; its geometry is independent of action revisions/focus.
            Assert.True(geometry.Success, geometry.Error?.Message);
            var pickTarget = geometry.Value!.Target; var pickGeometry = geometry.Value.Geometry;
            var openModal = Assert.Single((await client.FindNodesAsync(manifest.SessionId, top.Id, TreeKinds.Visual, name: "OpenModal", cancellationToken: token)).Value!.Matches).Node;
            var bounds = openModal.Bounds!; var centerX = bounds.X + bounds.Width / 2; var centerY = bounds.Y + bounds.Height / 2;
            var pickRequest = new RuntimePickRequest(pickTarget, pickGeometry.DesktopBounds!.X + centerX * pickGeometry.DesktopScaling,
                pickGeometry.DesktopBounds.Y + centerY * pickGeometry.DesktopScaling, "desktop", pickGeometry.Revision);
            var pickCall = await mcp.CallToolAsync("pick_node", new Dictionary<string, object?>
            { ["request"] = JsonSerializer.SerializeToElement(pickRequest), ["manifestDirectory"] = manifests }, cancellationToken: token);
            var picked = JsonSerializer.Deserialize<ToolResult<RuntimePickResponse>>(JsonSerializer.Serialize(pickCall.StructuredContent))!;
            Assert.True(picked.Success, JsonSerializer.Serialize(picked)); Assert.Contains(picked.Value!.HitPath, item => item.Target.NodeId == openModal.NodeId);
            var highlight = await client.HighlightAsync(new(openModal.Target!, lifetimeMs: 5000), token);
            Assert.True(highlight.Success, highlight.Error?.Message); Assert.True(highlight.Value!.InputTransparent);
            var cleanImage = await client.CaptureScreenshotAsync(manifest.SessionId, top.Id, Path.Combine(output, "without-highlight.png"), token);
            Assert.True(cleanImage.Success, cleanImage.Error?.Message);
            Assert.Equal("absent", (await client.HighlightAsync(new(pickTarget, "inspect"), token)).Value!.Status);
            current = await Inspect();
            var moveAgain = await client.WindowAsync(new(current.Target, "move", current.Revision,
                position: new(current.Position.X + 20, current.Position.Y), timeoutMs: 3000), token);
            Assert.True(OperationResultMapper.IsSuccessful(moveAgain), JsonSerializer.Serialize(moveAgain));
            Assert.Equal("pick_geometry_changed", (await client.PickNodeAsync(pickRequest, token)).Error!.Code);
            await Invoke(top.Id, "OpenPopup");
            var popupGeometry = (await client.PickNodeAsync(new(pickTarget), token)).Value!.Geometry;
            var popupPick = await client.PickNodeAsync(new(pickTarget, centerX, centerY, "top_level_dip", popupGeometry.Revision), token);
            Assert.True(popupPick.Success, popupPick.Error?.Message);
            Assert.Contains(popupPick.Value!.Diagnostics, item => item.Code == "pick_native_popup_present");
            Assert.Contains("selected_root_only", popupPick.Value.Occlusion);
            await Invoke(top.Id, "ClosePopup");
            await File.WriteAllTextAsync(Path.Combine(output, "picking.json"), JsonSerializer.Serialize(new { picked, highlight, cleanImage, moveAgain, popupPick }), token);

            // OS foreground policy may deny activation on a noninteractive CI desktop.
            current = await Inspect();
            var front = await client.WindowAsync(new(current.Target, "bring_to_front", current.Revision, timeoutMs: 3000), token);
            Assert.True(front.Success, front.Error?.Message); evidence.Add(front.Value!);
            if (OperationResultMapper.IsSuccessful(front))
            { Assert.True(front.Value!.After!.FrontmostRegisteredWindow); Assert.Equal("focused", front.Value.After.NativeFocus.State); }
            else { Assert.Equal("partial", front.Value!.Status); Assert.NotEmpty(front.Value.Diagnostics); }

            await Invoke(top.Id, "OpenModal");
            current = await Inspect(); Assert.NotNull(current.ModalBlocker);
            var blocked = OperationResultMapper.ToToolResult(await client.WindowAsync(new(current.Target, "activate", current.Revision), token));
            Assert.False(blocked.Success); Assert.Equal("window_modal_blocked", blocked.Error!.Code); Assert.Equal(0, blocked.Value!.DispatchedOperations);
            var modal = Snapshot(await client.WindowAsync(new(current.ModalBlocker!), token)); Assert.True(modal.IsDialog); Assert.Equal(top.Id, modal.Owner!.TopLevelId);
            await Invoke(modal.Target.TopLevelId, "CloseModal");
            var closed = OperationResultMapper.ToToolResult(await client.WindowAsync(new(modal.Target, "restore", modal.Revision), token));
            Assert.False(closed.Success); Assert.Equal("window_unavailable", closed.Error!.Code);
            Assert.True((await client.SessionControlAsync(manifest.SessionId, new("acquire", "window-owner"), token)).Success);
            var observer = new LocalBridgeClient(manifests);
            var observation = Snapshot(await observer.WindowAsync(new(target), token));
            var conflict = await observer.WindowAsync(new(observation.Target, "restore", observation.Revision), token);
            Assert.Equal("session_control_conflict", conflict.Error!.Code);
            await File.WriteAllTextAsync(Path.Combine(output, "validation.json"), JsonSerializer.Serialize(new { backend = initial.Backend.Backend, evidence, modal, blocked, conflict }), token);

            async Task<RuntimeWindowSnapshot> Inspect() => Snapshot(await client.WindowAsync(new(target), token));
            async Task Change(string action)
            {
                var before = await Inspect();
                var result = await client.WindowAsync(new(before.Target, action, before.Revision, timeoutMs: 3000), token);
                var after = Snapshot(result); evidence.Add(result.Value!);
                Assert.Equal(action == "restore" ? "normal" : action == "minimize" ? "minimized" : "maximized", after.NativeState.State);
            }
            async Task Invoke(string topId, string name)
            {
                var found = await client.FindNodesAsync(manifest.SessionId, topId, TreeKinds.Visual, name: name, cancellationToken: token);
                var node = Assert.Single(found.Value!.Matches).Node;
                Assert.True(OperationResultMapper.IsSuccessful(await client.InputAsync(manifest.SessionId, topId, InputActions.Invoke, targetNodeId: node.NodeId, cancellationToken: token)));
            }
        }
        finally
        {
            await File.WriteAllTextAsync(Path.Combine(output, "completed-operations.json"), JsonSerializer.Serialize(evidence));
            if (manifest is not null && !process.HasExited) await client.CloseSessionAsync(manifest.SessionId);
            if (!process.HasExited) process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync();
            await File.WriteAllTextAsync(Path.Combine(output, "host.stdout.log"), await stdout);
            await File.WriteAllTextAsync(Path.Combine(output, "host.stderr.log"), await stderr);
        }
    }

    private static RuntimeWindowSnapshot Snapshot(CoreResult<RuntimeWindowResponse> result)
    { Assert.True(OperationResultMapper.IsSuccessful(result), JsonSerializer.Serialize(result)); return result.Value!.After!; }
}
