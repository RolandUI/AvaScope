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
        Exception? primaryFailure = null;
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
        catch (Exception exception) { primaryFailure = exception; throw; }
        finally
        {
            await CleanupAsync(owned, output, async () =>
            {
                if (manifest is not null) await client.CloseSessionAsync(manifest.SessionId);
            }, StopAccessibilityGroup, primaryFailure);
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

    private static async Task CleanupAsync(List<(Process Process, Task<string> Out, Task<string> Error, string Name)> owned,
        string output, Func<Task> closeSession, Action stopAccessibilityGroup, Exception? primaryFailure)
    {
        var failures = new List<Exception>(); var phases = new List<object>();
        var processes = new List<Dictionary<string, object?>>();
        await Phase("session", "close_session", closeSession);
        await Phase("session", "stop_accessibility_group", () => { stopAccessibilityGroup(); return Task.CompletedTask; });
        foreach (var item in owned.AsEnumerable().Reverse())
        {
            var state = new Dictionary<string, object?>
            {
                ["name"] = item.Name, ["processId"] = item.Process.Id, ["exited"] = false,
                ["exitCode"] = null, ["killRequested"] = false, ["disposed"] = false
            };
            processes.Add(state);
            await Phase(item.Name, "terminate", () =>
            {
                if (!item.Process.HasExited) { state["killRequested"] = true; item.Process.Kill(entireProcessTree: true); }
                return Task.CompletedTask;
            });
            await Phase(item.Name, "exit", async () =>
            {
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(3));
                await item.Process.WaitForExitAsync(timeout.Token);
                state["exited"] = true; state["exitCode"] = item.Process.ExitCode;
            });
            await Capture("stdout", item.Out);
            await Capture("stderr", item.Error);
            // Process.Dispose does not own synchronous-mode StandardOutput/StandardError readers.
            // Closing them also releases a pending read after its bounded drain failed.
            await Phase(item.Name, "close_stdout", () => { item.Process.StandardOutput.Dispose(); return Task.CompletedTask; });
            await Phase(item.Name, "close_stderr", () => { item.Process.StandardError.Dispose(); return Task.CompletedTask; });
            await Phase(item.Name, "dispose", () => { item.Process.Dispose(); state["disposed"] = true; return Task.CompletedTask; });

            async Task Capture(string stream, Task<string> read)
            {
                string? text = null;
                state[stream] = await Phase(item.Name, stream + "_drain", async () => text = await read.WaitAsync(TimeSpan.FromSeconds(3)));
                await Phase(item.Name, stream + "_log", () => File.WriteAllTextAsync(Path.Combine(output, item.Name + "." + stream + ".log"),
                    text ?? "[capture unavailable; see cleanup.json for the bounded failure phase]"));
                // Observe a read which faults only after its pipe is closed, without waiting again.
                _ = read.ContinueWith(task => { _ = task.Exception; }, CancellationToken.None,
                    TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
            }
        }
        await Phase("session", "write_evidence", () => File.WriteAllTextAsync(Path.Combine(output, "cleanup.json"), JsonSerializer.Serialize(new
        {
            status = failures.Count == 0 ? "completed" : "failed", primaryFailureType = primaryFailure?.GetType().Name,
            streamTimeoutMs = 3000, exitTimeoutMs = 3000, processes, phases
        })));
        if (failures.Count > 0)
            throw new AggregateException("Owned native test cleanup failed; original failure and phase failures are retained.",
                primaryFailure is null ? failures : new[] { primaryFailure }.Concat(failures));

        async Task<string> Phase(string name, string phase, Func<Task> action)
        {
            var timer = Stopwatch.StartNew(); Exception? failure = null;
            try { await action(); }
            catch (Exception exception)
            {
                failure = exception;
                failures.Add(new InvalidOperationException($"{name}/{phase} failed during owned native test cleanup.", exception));
            }
            var status = failure is null ? "completed" : failure is TimeoutException or OperationCanceledException ? "timeout" : "failed";
            // No exception message, stack, user text or environment values in lifecycle evidence.
            phases.Add(new { name, phase, status, elapsedMs = timer.Elapsed.TotalMilliseconds,
                failureType = failure?.GetType().Name, hresult = failure?.HResult });
            return status;
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ExitedProcessWithPendingOutputRetainsCleanupEvidenceAndPrimaryFailure(bool primaryFailed)
    {
        var output = Path.Combine(Path.GetTempPath(), "AvaScope.Tests", "native-cleanup-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(output);
        var owned = new List<(Process Process, Task<string> Out, Task<string> Error, string Name)>();
        var pending = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        try
        {
            foreach (var name in new[] { "healthy", "incomplete" })
            {
                var info = new ProcessStartInfo("dotnet") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
                info.ArgumentList.Add("--version");
                var process = Process.Start(info)!;
                var stdout = process.StandardOutput.ReadToEndAsync(); var stderr = process.StandardError.ReadToEndAsync();
                owned.Add((process, stdout, stderr, name));
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
                await process.WaitForExitAsync(timeout.Token);
                Assert.Equal(0, process.ExitCode);
                Assert.NotEmpty(await stdout.WaitAsync(timeout.Token));
                await stderr.WaitAsync(timeout.Token);
            }
            // Only the capture completion is held: both real owned processes have already exited.
            // This reproduces the cleanup boundary, not the cause of the original delayed EOF.
            owned[1] = (owned[1].Process, pending.Task, Task.FromResult("completed stderr"), "incomplete");
            var primary = primaryFailed ? new InvalidOperationException("private primary failure") : null;
            var error = await Assert.ThrowsAsync<AggregateException>(() => CleanupAsync(owned, output, () => Task.CompletedTask, () => { }, primary));
            Assert.Equal(primaryFailed ? 2 : 1, error.InnerExceptions.Count);
            if (primaryFailed) Assert.Same(primary, error.InnerExceptions[0]);
            Assert.IsType<TimeoutException>(error.InnerExceptions[^1].InnerException);
            Assert.All(owned, item => Assert.Throws<ObjectDisposedException>(() => { _ = item.Process.StandardOutput; }));
            Assert.Contains("unavailable", await File.ReadAllTextAsync(Path.Combine(output, "incomplete.stdout.log")));
            Assert.Equal("completed stderr", await File.ReadAllTextAsync(Path.Combine(output, "incomplete.stderr.log")));
            Assert.NotEmpty(await File.ReadAllTextAsync(Path.Combine(output, "healthy.stdout.log")));
            var json = await File.ReadAllTextAsync(Path.Combine(output, "cleanup.json"));
            Assert.DoesNotContain("private primary failure", json);
            using var evidence = JsonDocument.Parse(json);
            Assert.Equal("failed", evidence.RootElement.GetProperty("status").GetString());
            var processes = evidence.RootElement.GetProperty("processes").EnumerateArray().ToArray();
            Assert.Equal(2, processes.Length);
            Assert.Equal(new[] { "incomplete", "healthy" }, processes.Select(p => p.GetProperty("name").GetString()));
            Assert.True(processes[0].GetProperty("exited").GetBoolean());
            Assert.Equal("timeout", processes[0].GetProperty("stdout").GetString());
            Assert.Equal("completed", processes[0].GetProperty("stderr").GetString());
            Assert.All(processes, p => Assert.True(p.GetProperty("disposed").GetBoolean()));
            Assert.False(pending.Task.IsCompleted);
        }
        finally
        {
            pending.TrySetResult("released after assertion");
            foreach (var item in owned)
            {
                if (Record.Exception(() => { _ = item.Process.StandardOutput; }) is not ObjectDisposedException)
                {
                    if (!item.Process.HasExited) item.Process.Kill(entireProcessTree: true);
                    await item.Process.WaitForExitAsync(); item.Process.StandardOutput.Dispose(); item.Process.StandardError.Dispose(); item.Process.Dispose();
                }
            }
            Directory.Delete(output, true);
        }
    }

    [Theory]
    [InlineData("none")]
    [InlineData("primary")]
    [InlineData("close_session")]
    [InlineData("stop_accessibility_group")]
    [InlineData("stderr_drain")]
    public async Task CleanupCompletesLogsAndDisposalDespiteSessionOrStreamFailure(string failingPhase)
    {
        var output = Path.Combine(Path.GetTempPath(), "AvaScope.Tests", "native-cleanup-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(output);
        var info = new ProcessStartInfo("dotnet") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        info.ArgumentList.Add("--version");
        using var process = Process.Start(info)!;
        var stdout = process.StandardOutput.ReadToEndAsync(); var stderr = process.StandardError.ReadToEndAsync();
        var stopped = false;
        var fault = new IOException("private cleanup failure");
        var primary = new InvalidOperationException("private primary failure");
        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            await process.WaitForExitAsync(timeout.Token);
            var expectedOut = await stdout.WaitAsync(timeout.Token); var expectedError = await stderr.WaitAsync(timeout.Token);
            Assert.Equal(0, process.ExitCode); Assert.NotEmpty(expectedOut);
            var error = await Record.ExceptionAsync(async () =>
            {
                Exception? original = null;
                try { if (failingPhase == "primary") throw primary; }
                catch (Exception exception) { original = exception; throw; }
                finally
                {
                    await CleanupAsync([(process, stdout, failingPhase == "stderr_drain" ? Task.FromException<string>(fault) : stderr, "host")], output,
                        () => failingPhase == "close_session" ? Task.FromException(fault) : Task.CompletedTask,
                        () => { stopped = true; if (failingPhase == "stop_accessibility_group") throw fault; }, original);
                }
            });
            if (failingPhase == "none") Assert.Null(error);
            else if (failingPhase == "primary") Assert.Same(primary, error);
            else Assert.Same(fault, Assert.Single(Assert.IsType<AggregateException>(error).InnerExceptions).InnerException);
            Assert.Throws<ObjectDisposedException>(() => { _ = process.StandardOutput; }); Assert.True(stopped);
            Assert.Equal(expectedOut, await File.ReadAllTextAsync(Path.Combine(output, "host.stdout.log")));
            var savedError = await File.ReadAllTextAsync(Path.Combine(output, "host.stderr.log"));
            if (failingPhase == "stderr_drain") Assert.Contains("capture unavailable", savedError);
            else Assert.Equal(expectedError, savedError);
            var json = await File.ReadAllTextAsync(Path.Combine(output, "cleanup.json"));
            Assert.DoesNotContain("private", json);
            using var evidence = JsonDocument.Parse(json);
            Assert.Equal(failingPhase is "none" or "primary" ? "completed" : "failed", evidence.RootElement.GetProperty("status").GetString());
            Assert.All(evidence.RootElement.GetProperty("processes").EnumerateArray(), item => Assert.True(item.GetProperty("disposed").GetBoolean()));
            if (failingPhase is not "none" and not "primary")
                Assert.Contains(evidence.RootElement.GetProperty("phases").EnumerateArray(), item =>
                    item.GetProperty("phase").GetString() == failingPhase && item.GetProperty("status").GetString() == "failed");
        }
        finally
        {
            if (Record.Exception(() => { _ = process.StandardOutput; }) is not ObjectDisposedException)
            {
                if (!process.HasExited) process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync(); process.StandardOutput.Dispose(); process.StandardError.Dispose();
            }
            Directory.Delete(output, true);
        }
    }
    [DllImport("libc", SetLastError = true)] private static extern int getpgid(int pid);
    [DllImport("libc", SetLastError = true)] private static extern int kill(int pid, int signal);
}
