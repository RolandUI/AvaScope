using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using AvaScope.Core;
using AvaScope.Protocol;
using ModelContextProtocol.Client;

namespace AvaScope.Tests.Bridge;

[Collection(BridgeCollectionDefinition.Name)]
public sealed class NativeInputIntegrationTestsAccessibility
{
    [NativeInputFact]
    public async Task NativeAccessibilityEvidenceDiagnosesTheOwnedSampleThroughCliAndMcp()
    {
        var host = Path.GetFullPath(Environment.GetEnvironmentVariable("AVASCOPE_NATIVE_INPUT_HOST")!);
        var provider = Path.GetFullPath(Environment.GetEnvironmentVariable("AVASCOPE_NATIVE_INPUT_PROVIDER")!);
        var output = Path.Combine(Path.GetFullPath(Environment.GetEnvironmentVariable("AVASCOPE_NATIVE_INPUT_OUTPUT")!), "accessibility");
        var manifests = Path.Combine(output, "sessions"); Directory.CreateDirectory(manifests);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(90)); var token = timeout.Token;
        await using var desktop = OperatingSystem.IsLinux() ? new X11TestEnvironment(new(WindowManager: true, SessionBus: true), Path.Combine(output, "desktop")) : null;
        if (desktop is not null) Assert.True((await desktop.StartAsync(token)).Success);
        var owned = new List<(Process Process, Task<string> Out, Task<string> Error, string Name)>();
        var client = new LocalBridgeClient(manifests, TimeSpan.FromSeconds(10)); BridgeSessionManifest? manifest = null; int? accessibilityGroup = null;
        try
        {
            Process? accessibility = null;
            if (OperatingSystem.IsLinux())
            {
                var launcher = new[] { "/usr/libexec/at-spi-bus-launcher", "/usr/lib/at-spi2-core/at-spi-bus-launcher" }.FirstOrDefault(File.Exists);
                Assert.NotNull(launcher); accessibility = Start("accessibility-service", "setsid", ["--wait", launcher!, "--launch-immediately"]);
                accessibilityGroup = accessibility.Id;
                var ready = false;
                for (var i = 0; i < 40 && !ready; i++)
                {
                    using var probe = Process.Start(Info("dbus-send", ["--session", "--print-reply", "--reply-timeout=250", "--dest=org.a11y.Bus", "/org/a11y/bus", "org.a11y.Bus.GetAddress"]))!;
                    var read = probe.StandardOutput.ReadToEndAsync(token); var error = probe.StandardError.ReadToEndAsync(token);
                    await probe.WaitForExitAsync(token); ready = probe.ExitCode == 0; await read; await error;
                    if (!ready) await Task.Delay(50, token);
                }
                Assert.True(ready, "The owned AT-SPI service did not become ready.");
            }
            var process = Start("host", "dotnet", [host, "--exit-after-ms=100000", "--accessibility-fixture"]);
            for (var i = 0; i < 200 && manifest is null && !process.HasExited; i++)
            { manifest = client.ListSessionManifests().SingleOrDefault(m => m.ProcessId == process.Id); if (manifest is null) await Task.Delay(50, token); }
            Assert.NotNull(manifest);
            var top = Assert.Single((await client.ListTopLevelsAsync(manifest.SessionId, token)).Value!.TopLevels);
            var target = (await client.WindowAsync(new(new(manifest.SessionId, top.Id)), token)).Value!.After!.Target;
            // The test runner is a real different process. Its identity cannot be used as an
            // arbitrary native target inside the selected bridge session.
            Assert.NotEqual(process.Id, Environment.ProcessId);
            var foreign = new RuntimeTargetContext(manifest.SessionId, "process:" + Environment.ProcessId, topLevelGeneration: target.TopLevelGeneration);
            Assert.Equal("native_accessibility_stale", (await client.AuditNativeAccessibilityAsync(new(foreign), token)).Error!.Code);
            var expectations = new List<NativeAccessibilityExpectation>();
            foreach (var name in new[] { "AuditGood", "AuditUnnamed", "AuditWrongRole", "AuditAbsent" })
            {
                var found = Assert.Single((await client.FindNodesAsync(manifest.SessionId, top.Id, TreeKinds.Visual, name: name, cancellationToken: token)).Value!.Matches).Node;
                expectations.Add(new(found.Target!, Name: name == "AuditUnnamed" ? "Continue" : null, Role: "Button"));
            }
            var request = new NativeAccessibilityAuditRequest(target, maxNodes: 128, timeoutMs: 5000, expectations: expectations);
            await Task.Delay(500, token);
            var observed = await client.AuditNativeAccessibilityAsync(request, token);
            await File.WriteAllTextAsync(Path.Combine(output, "initial.json"), JsonSerializer.Serialize(observed), token);
            Assert.True(observed.Success, JsonSerializer.Serialize(observed));
            if (OperatingSystem.IsMacOS())
            { Assert.Equal("unsupported", observed.Value!.Status); Assert.Empty(observed.Value.Native.Nodes); return; }
            Assert.Equal("compared", observed.Value!.Status); Assert.NotEmpty(observed.Value.Native.Nodes);
            Assert.Equal(process.Id, observed.Value.ProcessId);
            var good = observed.Value.Comparisons.Single(c => c.Target.NodeId == expectations[0].Target.NodeId);
            Assert.Equal("high_identity", good.Confidence); Assert.Empty(good.Findings);
            Assert.Contains(OperatingSystem.IsLinux() ? "accessible_name_differs" : "native_name_missing", observed.Value.Comparisons.Single(c => c.Target.NodeId == expectations[1].Target.NodeId).Findings);
            Assert.Contains("accessible_role_differs", observed.Value.Comparisons.Single(c => c.Target.NodeId == expectations[2].Target.NodeId).Findings);
            if (OperatingSystem.IsWindows()) Assert.Contains(observed.Value.Comparisons.Single(c => c.Target.NodeId == expectations[3].Target.NodeId).Findings, f => f.StartsWith("expected_control_not_mapped"));
            var path = Path.Combine(output, "request.json"); await File.WriteAllTextAsync(path, JsonSerializer.Serialize(request), token);
            using (var cli = Process.Start(Info("dotnet", [Path.Combine(AppContext.BaseDirectory, "avascope.dll"), "audit-native-accessibility", "--request", path, "--manifest-dir", manifests]))!)
            {
                var stdout = cli.StandardOutput.ReadToEndAsync(token); var stderr = cli.StandardError.ReadToEndAsync(token);
                await cli.WaitForExitAsync(token); var json = await stdout;
                await File.WriteAllTextAsync(Path.Combine(output, "cli.json"), json, token);
                Assert.Equal(0, cli.ExitCode); Assert.True(JsonSerializer.Deserialize<ToolResult<NativeAccessibilityAuditResponse>>(json)!.Success, json + await stderr);
            }
            await using var mcp = await McpClient.CreateAsync(new StdioClientTransport(new()
            { Command = "dotnet", Arguments = [Path.Combine(AppContext.BaseDirectory, "AvaScope.Mcp.dll")], Name = "Native accessibility audit" }), cancellationToken: token);
            var safeRequest = new NativeAccessibilityAuditRequest(target, maxNodes: 128, timeoutMs: 5000, expectations: expectations,
                policy: new(output, redactedText: ["private-document"]));
            var call = await mcp.CallToolAsync("audit_native_accessibility", new Dictionary<string, object?>
            { ["request"] = JsonSerializer.SerializeToElement(safeRequest), ["manifestDirectory"] = manifests }, cancellationToken: token);
            var safeJson = JsonSerializer.Serialize(call.StructuredContent); Assert.DoesNotContain("private-document", safeJson);
            Assert.True(JsonSerializer.Deserialize<ToolResult<NativeAccessibilityAuditResponse>>(safeJson)!.Success, safeJson);
            await File.WriteAllTextAsync(Path.Combine(output, "mcp-redacted.json"), safeJson, token);
            if (accessibility is not null)
            {
                StopAccessibilityGroup(); await accessibility.WaitForExitAsync(token);
                var missing = await client.AuditNativeAccessibilityAsync(request, token);
                Assert.True(missing.Success, missing.Error?.Message); Assert.Equal("unavailable", missing.Value!.Status);
                Assert.Empty(missing.Value.Native.Nodes); Assert.False(OperationResultMapper.IsSuccessful(missing));
                await File.WriteAllTextAsync(Path.Combine(output, "service-unavailable.json"), JsonSerializer.Serialize(missing), token);
            }
        }
        finally
        {
            if (manifest is not null) await client.CloseSessionAsync(manifest.SessionId);
            StopAccessibilityGroup();
            foreach (var item in owned.AsEnumerable().Reverse())
            {
                if (!item.Process.HasExited) item.Process.Kill(entireProcessTree: true);
                await item.Process.WaitForExitAsync();
                await File.WriteAllTextAsync(Path.Combine(output, item.Name + ".stdout.log"), await item.Out.WaitAsync(TimeSpan.FromSeconds(3)));
                await File.WriteAllTextAsync(Path.Combine(output, item.Name + ".stderr.log"), await item.Error.WaitAsync(TimeSpan.FromSeconds(3)));
                item.Process.Dispose();
            }
        }
        void StopAccessibilityGroup()
        {
            // setsid creates a new process group for this exact owned launcher, including
            // registry daemons reparented after D-Bus activation. Never target the caller's group.
            if (accessibilityGroup is not { } group) return;
            if (getpgid(group) == group) kill(-group, 9);
            accessibilityGroup = null;
        }
        ProcessStartInfo Info(string executable, string[] arguments)
        {
            var info = new ProcessStartInfo(executable) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
            foreach (var arg in arguments) info.ArgumentList.Add(arg);
            info.Environment["UI_INSPECTION_PROVIDER_PATH"] = provider; info.Environment[BridgeSessionManifest.DirectoryEnvironmentVariable] = manifests;
            if (desktop is not null) foreach (var pair in desktop.EnvironmentVariables) info.Environment[pair.Key] = pair.Value;
            return info;
        }
        Process Start(string name, string executable, string[] arguments)
        {
            var process = Process.Start(Info(executable, arguments))!;
            owned.Add((process, process.StandardOutput.ReadToEndAsync(), process.StandardError.ReadToEndAsync(), name)); return process;
        }
    }
    [DllImport("libc", SetLastError = true)] private static extern int getpgid(int pid);
    [DllImport("libc", SetLastError = true)] private static extern int kill(int pid, int signal);
}
