using System.Diagnostics;
using System.Text.Json;
using AvaScope.Core;
using AvaScope.Protocol;
using ModelContextProtocol.Client;
using SkiaSharp;

namespace AvaScope.Tests.Bridge;

[Collection(BridgeCollectionDefinition.Name)]
public sealed class NativeInputIntegrationTestsScreenCapture
{
    [NativeInputFact]
    public async Task PairedEvidenceShowsOcclusionOffscreenPixelsAndPolicyBeforeExportThroughCliAndMcp()
    {
        var host = Path.GetFullPath(Environment.GetEnvironmentVariable("AVASCOPE_NATIVE_INPUT_HOST")!);
        var provider = Path.GetFullPath(Environment.GetEnvironmentVariable("AVASCOPE_NATIVE_INPUT_PROVIDER")!);
        var output = Path.Combine(Path.GetFullPath(Environment.GetEnvironmentVariable("AVASCOPE_NATIVE_INPUT_OUTPUT")!), "screen");
        var manifests = Path.Combine(output, "sessions"); Directory.CreateDirectory(manifests);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(90)); var token = timeout.Token;
        await using var desktop = OperatingSystem.IsLinux() ? new X11TestEnvironment(new(Width: 4096, Height: 2400, WindowManager: true, SessionBus: true), Path.Combine(output, "desktop")) : null;
        if (desktop is not null) { var ready = await desktop.StartAsync(token); Assert.True(ready.Success, ready.Error?.Message); }
        var start = new ProcessStartInfo("dotnet") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var argument in new[] { host, "--exit-after-ms=100000", "--screen-fixture", "--authorize-screen-capture" }) start.ArgumentList.Add(argument);
        start.Environment["UI_INSPECTION_PROVIDER_PATH"] = provider; start.Environment[BridgeSessionManifest.DirectoryEnvironmentVariable] = manifests;
        if (desktop is not null) foreach (var pair in desktop.EnvironmentVariables) start.Environment[pair.Key] = pair.Value;
        using var process = Process.Start(start)!;
        var stdout = process.StandardOutput.ReadToEndAsync(); var stderr = process.StandardError.ReadToEndAsync();
        var client = new LocalBridgeClient(manifests, TimeSpan.FromSeconds(12)); BridgeSessionManifest? manifest = null;
        var evidence = new List<RuntimeScreenCaptureResponse>();
        try
        {
            for (var attempt = 0; attempt < 200 && manifest is null && !process.HasExited; attempt++)
            { manifest = client.ListSessionManifests().SingleOrDefault(item => item.ProcessId == process.Id); if (manifest is null) await Task.Delay(50, token); }
            Assert.NotNull(manifest);
            var levels = await client.ListTopLevelsAsync(manifest.SessionId, token); Assert.True(levels.Success, levels.Error?.Message);
            var top = Assert.Single(levels.Value!.TopLevels);
            var inspection = await client.WindowAsync(new(new(manifest.SessionId, top.Id)), token); Assert.True(inspection.Success, inspection.Error?.Message);
            var target = inspection.Value!.After!.Target;
            var root = Path.Combine(output, "captures");
            var policy = new RuntimeEvidencePolicy(root, allowNativeScreenCapture: true);
            var request = new RuntimeScreenCaptureRequest(target, root, desktopScope: "declared_test_desktop", timeoutMs: 5000, policy: policy);
            var withoutScope = Value(await client.CaptureScreenAsync(new(target, root, policy: policy), token));
            Assert.Equal("native_screen_scope_denied", Assert.Single(withoutScope.Native!.Diagnostics).Code); Assert.Null(withoutScope.Native.FilePath);
            await Task.Delay(250, token);
            var first = Value(await client.CaptureScreenAsync(request, token)); evidence.Add(first);
            if ((OperatingSystem.IsMacOS() || OperatingSystem.IsWindows()) && first.Native!.Status == "unavailable"
                && first.Native.Diagnostics.Any(error => error.Code == "native_screen_permission_denied"
                    || OperatingSystem.IsMacOS() && error.Code == "native_screen_unsupported"))
            {
                // A locked Windows desktop or hosted macOS may deny screen access. Preserve actual denial evidence,
                // never count a render-to-bitmap result as a native capture or grant TCC permission silently.
                Assert.Contains(Assert.Single(first.Native.Diagnostics).Code, new[] { "native_screen_permission_denied", "native_screen_unsupported" });
                Assert.Equal("captured", first.Rendered!.Status); Assert.Equal("partial", first.Status); return;
            }
            Assert.True(first.Status == "captured", JsonSerializer.Serialize(first)); Assert.NotEqual(first.Rendered!.Source, first.Native!.Source);
            Assert.True(first.Rendered.CompletedAt <= first.Native.CompletedAt); Assert.NotEmpty(first.Native.NativeRegions!);
            Assert.Equal(first.Rendered.PixelWidth, first.Native.PixelWidth);
            Assert.True(CountColor(first.Native.FilePath!, "blue") > 10000);
            Assert.Equal(OperatingSystem.IsMacOS() ? "cocoa_desktop_points" : "physical_desktop_pixels", first.Native.DesktopUnits);

            await Invoke("ScreenOcclude"); await Task.Delay(250, token);
            var requestPath = Path.Combine(output, "capture-request.json"); await File.WriteAllTextAsync(requestPath, JsonSerializer.Serialize(request), token);
            var cliStart = new ProcessStartInfo("dotnet") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
            foreach (var arg in new[] { Path.Combine(AppContext.BaseDirectory, "avascope.dll"), "capture-screen", "--request", requestPath, "--manifest-dir", manifests }) cliStart.ArgumentList.Add(arg);
            using (var cli = Process.Start(cliStart)!)
            {
                var jsonTask = cli.StandardOutput.ReadToEndAsync(token); var errorTask = cli.StandardError.ReadToEndAsync(token);
                await cli.WaitForExitAsync(token); var json = await jsonTask; await File.WriteAllTextAsync(Path.Combine(output, "cli-occluded.json"), json, token);
                var call = JsonSerializer.Deserialize<ToolResult<RuntimeScreenCaptureResponse>>(json)!;
                Assert.True(call.Success, json + await errorTask); Assert.Equal(0, cli.ExitCode);
                var occluded = call.Value!; evidence.Add(occluded);
                Assert.Equal(0, CountColor(occluded.Rendered!.FilePath!, "red"));
                Assert.True(CountColor(occluded.Native!.FilePath!, "red") > 5000);
                using (var nativeImage = SKBitmap.Decode(occluded.Native.FilePath))
                    Assert.Equal(SKColors.Red, nativeImage.GetPixel((int)(200 * occluded.RenderScaling), (int)(50 * occluded.RenderScaling)));
                Assert.True(occluded.Comparison!.DifferentPixels > 5000); Assert.Equal("compared", occluded.Comparison.Status);
            }
            await Invoke("ScreenUncover"); await Invoke("ScreenPopup"); await Task.Delay(150, token);
            var popup = Value(await client.CaptureScreenAsync(request, token)); evidence.Add(popup);
            Assert.Equal("captured", popup.Status); Assert.True(CountColor(popup.Native!.FilePath!, "green") > 1000);
            await Invoke("ScreenPopup");

            var environment = TestEnvironment.McpEnvironment();
            if (Environment.GetEnvironmentVariable("TMPDIR") is { } temporary) environment["TMPDIR"] = temporary;
            await using var mcp = await McpClient.CreateAsync(new StdioClientTransport(new()
            { Command = "dotnet", Arguments = [Path.Combine(AppContext.BaseDirectory, "AvaScope.Mcp.dll")], Name = "Screen evidence validation",
                InheritEnvironmentVariables = false, EnvironmentVariables = environment, ShutdownTimeout = TimeSpan.FromSeconds(3) }), cancellationToken: token);
            var protectedRequest = new RuntimeScreenCaptureRequest(target, root, desktopScope: "declared_test_desktop", timeoutMs: 5000,
                policy: new(root, redactedText: ["sensitive-pixels"], allowNativeScreenCapture: true));
            var result = await mcp.CallToolAsync("capture_screen", new Dictionary<string, object?>
            { ["request"] = JsonSerializer.SerializeToElement(protectedRequest), ["manifestDirectory"] = manifests }, cancellationToken: token);
            var protectedResult = JsonSerializer.Deserialize<ToolResult<RuntimeScreenCaptureResponse>>(JsonSerializer.Serialize(result.StructuredContent))!;
            Assert.True(protectedResult.Success, JsonSerializer.Serialize(protectedResult)); evidence.Add(protectedResult.Value!);
            Assert.Equal("privacy_masked", protectedResult.Value!.Comparison!.Status);
            foreach (var frame in new[] { protectedResult.Value.Rendered!, protectedResult.Value.Native! })
            {
                Assert.Null(frame.Png); Assert.Equal("full_sensitive_mask", frame.Masking);
                using var bitmap = SKBitmap.Decode(frame.FilePath);
                Assert.All(bitmap.Pixels, pixel => Assert.Equal(SKColors.Black, pixel));
            }
            await Invoke("ScreenOffscreen"); await Task.Delay(200, token);
            var offscreen = Value(await client.CaptureScreenAsync(request, token)); evidence.Add(offscreen);
            Assert.Equal("captured", offscreen.Status);
            Assert.True(offscreen.Native!.VisibleImageRegions.Sum(r => r.Width * r.Height) < offscreen.Native.PixelWidth * offscreen.Native.PixelHeight * 0.8);
            using (var bitmap = SKBitmap.Decode(offscreen.Native.FilePath)) Assert.Contains(bitmap.Pixels, pixel => pixel.Alpha == 0);
            using (var bitmap = SKBitmap.Decode(offscreen.Rendered!.FilePath)) Assert.DoesNotContain(bitmap.Pixels, pixel => pixel.Alpha == 0);
            if (desktop is not null)
            {
                // This validates native 4K allocation/transport, not a claim of macOS Retina text fidelity.
                var current = (await client.WindowAsync(new(target), token)).Value!.After!;
                var moved = await client.WindowAsync(new(current.Target, "move", current.Revision, position: new(20, 40)), token);
                Assert.Equal("executed", moved.Value!.Status);
                current = (await client.WindowAsync(new(target), token)).Value!.After!;
                var requestedSize = new RuntimeSize(3840 / current.RenderScaling, 2160 / current.RenderScaling);
                var resized = await client.WindowAsync(new(current.Target, "resize", current.Revision, clientSize: requestedSize), token);
                Assert.Equal("executed", resized.Value!.Status); Assert.Equal(requestedSize, resized.Value.After!.ClientSize);
                await File.WriteAllTextAsync(Path.Combine(output, "full-resolution-window.json"), JsonSerializer.Serialize(resized.Value.After), token);
                await File.WriteAllTextAsync(requestPath, JsonSerializer.Serialize(request), token);
                using (var cli = Process.Start(cliStart)!)
                {
                    var jsonTask = cli.StandardOutput.ReadToEndAsync(token); var errorTask = cli.StandardError.ReadToEndAsync(token);
                    await cli.WaitForExitAsync(token); var json = await jsonTask;
                    await File.WriteAllTextAsync(Path.Combine(output, "cli-full-resolution.json"), json, token);
                    var call = JsonSerializer.Deserialize<ToolResult<RuntimeScreenCaptureResponse>>(json)!;
                    Assert.True(call.Success, json + await errorTask); Assert.Equal(0, cli.ExitCode);
                    CheckFullResolution(call.Value!);
                }
                var largeMcp = await mcp.CallToolAsync("capture_screen", new Dictionary<string, object?>
                { ["request"] = JsonSerializer.SerializeToElement(request), ["manifestDirectory"] = manifests }, cancellationToken: token);
                var largeResult = JsonSerializer.Deserialize<ToolResult<RuntimeScreenCaptureResponse>>(JsonSerializer.Serialize(largeMcp.StructuredContent))!;
                Assert.True(largeResult.Success, JsonSerializer.Serialize(largeResult)); CheckFullResolution(largeResult.Value!);
                Assert.Equal(requestedSize, (await client.WindowAsync(new(target), token)).Value!.After!.ClientSize);
                void CheckFullResolution(RuntimeScreenCaptureResponse captured)
                {
                    evidence.Add(captured); Assert.Equal("captured", captured.Status);
                    foreach (var frame in new[] { captured.Rendered!, captured.Native! })
                    {
                        Assert.Equal(3840, frame.PixelWidth); Assert.Equal(2160, frame.PixelHeight);
                        Assert.Null(frame.Png); Assert.True(File.Exists(frame.FilePath));
                    }
                    Assert.Equal("compared", captured.Comparison!.Status);
                    Assert.Equal(3840L * 2160, captured.Comparison.ComparedPixels);
                }
            }
            async Task Invoke(string name)
            {
                var found = await client.FindNodesAsync(manifest.SessionId, top.Id, TreeKinds.Visual, name: name, cancellationToken: token);
                var node = Assert.Single(found.Value!.Matches).Node;
                var invoked = await client.InputAsync(manifest.SessionId, top.Id, InputActions.Invoke, targetNodeId: node.NodeId, cancellationToken: token);
                Assert.True(OperationResultMapper.IsSuccessful(invoked), JsonSerializer.Serialize(invoked));
            }
        }
        finally
        {
            await File.WriteAllTextAsync(Path.Combine(output, "validation.json"), JsonSerializer.Serialize(evidence));
            if (manifest is not null && !process.HasExited) await client.CloseSessionAsync(manifest.SessionId);
            if (!process.HasExited) process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync();
            await File.WriteAllTextAsync(Path.Combine(output, "host.stdout.log"), await stdout);
            await File.WriteAllTextAsync(Path.Combine(output, "host.stderr.log"), await stderr);
        }
    }
    private static RuntimeScreenCaptureResponse Value(CoreResult<RuntimeScreenCaptureResponse> result)
    { Assert.True(result.Success, JsonSerializer.Serialize(result)); return result.Value!; }
    private static int CountColor(string path, string color)
    {
        using var bitmap = SKBitmap.Decode(path);
        return bitmap.Pixels.Count(pixel => pixel.Alpha == 255 && (color switch
        { "red" => pixel.Red > 220 && pixel.Green < 30 && pixel.Blue < 30, "green" => pixel.Green > 220 && pixel.Red < 30 && pixel.Blue < 30,
            _ => pixel.Blue > 220 && pixel.Red < 30 && pixel.Green < 30 }));
    }
}
