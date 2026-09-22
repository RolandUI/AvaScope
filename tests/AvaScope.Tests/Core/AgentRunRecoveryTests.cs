using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using AvaScope.Core;
using AvaScope.Protocol;

namespace AvaScope.Tests.Core;

public sealed class AgentRunRecoveryTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "AvaScope.Tests", "recovery-" + Guid.NewGuid().ToString("N"));
    private string StorePath => Path.Combine(_root, "store");
    private string Output => Path.Combine(_root, "evidence");
    private string Manifests => Path.Combine(_root, "manifests");

    [Fact]
    public void LeaseExpiryDoesNotTransferAnExecutingOperationOrAcceptOldTokens()
    {
        var clock = new Clock();
        var control = new SessionControlCoordinator(new("lease-test"), clock);
        var alice = control.Execute(new("acquire", "alice", TtlMs: 1000));
        Assert.True(alice.Success);
        var token = alice.Value!.Token!;
        Assert.Null(control.Execute(new()).Value!.Token);
        Assert.False(control.Execute(new("acquire", "bob")).Success);
        Assert.False(control.Enter(null).Success);
        Assert.False(control.Enter("wrong").Success);
        using (var operation = control.Enter(token).Value!)
        {
            clock.Advance(2000);
            Assert.False(control.Execute(new("acquire", "bob")).Success);
            Assert.False(control.Execute(new("release", Token: token)).Success);
            Assert.True(control.Execute(new()).Value!.Busy);
        }
        Assert.Equal("expired", control.Execute(new()).Value!.State);
        Assert.False(control.Enter(token).Success);
        Assert.False(control.Enter(null).Success);
        var bob = control.Execute(new("acquire", "bob"));
        Assert.True(bob.Success);
        Assert.NotEqual(token, bob.Value!.Token);
        Assert.False(control.Execute(new("release", Token: token)).Success);
        Assert.True(control.Execute(new("renew", Token: bob.Value.Token)).Success);
        Assert.True(control.Execute(new("release", Token: bob.Value.Token)).Success);
        using var legacy = control.Enter(null).Value;
        Assert.NotNull(legacy);
        Assert.False(control.Enter(null).Success);
    }

    [Fact]
    public async Task ConcurrentPathsAndActiveRunsConflictWhileAbandonedEvidenceIsPreserved()
    {
        var store = new AgentRunStore(StorePath);
        var first = store.Begin(Output, Path.Combine(Output, "state"), null, Manifests, null);
        Assert.True(first.Success, first.Error?.Message);
        var run = first.Value!;
        Directory.CreateDirectory(Output);
        var evidence = Path.Combine(Output, "evidence.txt");
        File.WriteAllText(evidence, "retained");
        using var independent = store.Begin(Path.Combine(_root, "other"), null, null, Manifests, null).Value!;
        Assert.NotEqual(run.RunId, independent.RunId);
        Assert.Equal("run_path_conflict", store.Begin(Path.Combine(Output, "child"), null, null, Manifests, null).Error!.Code);
        Assert.True((await store.RecoverAsync(new(run.RunId))).Value!.Active);
        Assert.Equal("run_active", (await store.RecoverAsync(new(run.RunId, "cleanup"))).Error!.Code);
        run.Dispose();
        Assert.Equal("abandoned", (await store.RecoverAsync(new(run.RunId))).Value!.State);
        Assert.Equal("run_path_conflict", store.Begin(Output, null, null, Manifests, null).Error!.Code);
        Assert.Equal("cleaned", (await store.RecoverAsync(new(run.RunId, "cleanup"))).Value!.State);
        Assert.Equal("retained", File.ReadAllText(evidence));
        using var next = store.Begin(Output, null, null, Manifests, null).Value!;
        Assert.NotNull(next);
    }

    [Fact]
    public async Task IndependentConcurrentRunsWaitForRegistryWithoutSharingOwnership()
    {
        var store = new AgentRunStore(StorePath);
        var results = await Task.WhenAll(Enumerable.Range(0, 8).Select(index => Task.Run(() =>
            store.Begin(Path.Combine(_root, "run-" + index), null, null, Manifests, null))));
        try
        {
            Assert.All(results, result => Assert.True(result.Success, result.Error?.Message));
            Assert.Equal(8, results.Select(result => result.Value!.RunId).Distinct().Count());
            Assert.All(store.List().Value!, record => Assert.True(record.Active));
        }
        finally { foreach (var result in results) result.Value?.Dispose(); }
    }

    [Fact]
    public async Task StalePidAndMismatchedRuntimeMarkerCannotAuthorizeCleanup()
    {
        var store = new AgentRunStore(StorePath);
        var run = store.Begin(Output, null, null, Manifests, null).Value!;
        using var current = Process.GetCurrentProcess();
        run.ProcessStarted("app", current);
        run.Dispose();
        var path = Path.Combine(StorePath, run.RunId, "record.json");
        var record = JsonNode.Parse(File.ReadAllText(path))!;
        record["processes"]![0]!["startedAt"] = current.StartTime.ToUniversalTime().AddDays(-1);
        record["processes"]![0]!["startIdentity"] = "stale-kernel-start-identity";
        File.WriteAllText(path, record.ToJsonString());
        Assert.Equal("run_process_identity_changed", (await store.RecoverAsync(new(run.RunId, "cleanup"))).Error!.Code);
        Assert.False(current.HasExited);

        record["processes"] = new JsonArray();
        var runtime = Path.Combine(Path.GetTempPath(), "avs-x11-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(runtime);
        try
        {
            File.WriteAllText(Path.Combine(runtime, ".avascope-run"), "different-run");
            File.WriteAllText(Path.Combine(runtime, "authority"), "must-not-delete");
            record["runtimeDirectories"] = new JsonArray(runtime);
            File.WriteAllText(path, record.ToJsonString());
            var partial = await store.RecoverAsync(new(run.RunId, "cleanup"));
            Assert.True(partial.Success);
            Assert.Equal("partial_cleanup", partial.Value!.State);
            Assert.Contains(partial.Value.Diagnostics, d => d.Code == "run_resource_cleanup_failed");
            Assert.Equal("must-not-delete", File.ReadAllText(Path.Combine(runtime, "authority")));
            File.WriteAllText(Path.Combine(runtime, ".avascope-run"), run.RunId);
            Assert.Equal("cleaned", (await store.RecoverAsync(new(run.RunId, "cleanup"))).Value!.State);
            Assert.False(Directory.Exists(runtime));
        }
        finally { if (Directory.Exists(runtime)) Directory.Delete(runtime, recursive: true); }
    }

    [Fact]
    public async Task ConflictingOwnerRetainsTheAppAndDesktopUntilExplicitRecovery()
    {
        Directory.CreateDirectory(_root);
        var leaseFile = Path.Combine(_root, "fixture-control-token");
        var managed = OperatingSystem.IsLinux() && Environment.GetEnvironmentVariable("AVASCOPE_RECOVERY_MANAGED_X11") == "1";
        var request = new RuntimeScenarioRequest([new(SemanticWorkflowActions.Wait, "unused", waitMs: 1)],
            launch: new(command: "dotnet", argumentList: [FixturePath, "--control-token-file", leaseFile], manifestDirectory: Manifests),
            outputDirectory: Output, terminateLaunchedProcess: true,
            x11Environment: managed ? new(WindowManager: true, SessionBus: true) : null);
        var result = await new RuntimeScenarioRunner().RunAsync(new LocalBridgeClient(Manifests), request);
        var value = Assert.IsType<RuntimeScenarioResponse>(result.Value);
        try
        {
            Assert.Equal("failed", value.Status);
            Assert.Equal("cleanup", value.FailureStage);
            Assert.Contains(value.Diagnostics, error => error.Code == "session_control_conflict");
            using var app = Process.GetProcessById(value.Launch!.ProcessId);
            Assert.False(app.HasExited);
            if (managed)
            {
                Assert.Equal("retained", value.Environment!.Status);
                Assert.All(value.Environment.Helpers, helper => Assert.False(helper.Exited));
            }
            var record = (await new AgentRunStore().RecoverAsync(new(value.RunId!))).Value!;
            Assert.Equal("partial_cleanup", record.State);
            Assert.Equal("failed", record.Outcome);
            Assert.Equal("session_control_conflict", (await new AgentRunStore().RecoverAsync(new(value.RunId!, "cleanup"))).Error!.Code);
        }
        finally
        {
            if (value.SessionId is not null && File.Exists(leaseFile))
                await new LocalBridgeClient(Manifests).SessionControlAsync(value.SessionId, new("release", Token: File.ReadAllText(leaseFile)));
            if (value.RunId is not null)
            {
                var cleanup = await new AgentRunStore().RecoverAsync(new(value.RunId, "cleanup"));
                Assert.True(cleanup.Success, cleanup.Error?.Message);
                Assert.Equal("cleaned", cleanup.Value!.State);
            }
        }
    }

    [Fact]
    public async Task KillingTheScenarioClientLeavesAResumableLeaseAndOnlyItsOwnedAppIsCleaned()
    {
        Directory.CreateDirectory(_root);
        var fixture = FixturePath;
        Assert.True(File.Exists(fixture), "Build the solution before this process-recovery gate.");
        var request = new RuntimeScenarioRequest([new(SemanticWorkflowActions.Wait, "hold", waitMs: 60000)],
            launch: new(command: "dotnet", argumentList: [fixture], manifestDirectory: Manifests),
            outputDirectory: Output, terminateLaunchedProcess: true, workflowTimeoutMs: 90000);
        var requestPath = Path.Combine(_root, "request.json");
        File.WriteAllText(requestPath, JsonSerializer.Serialize(request));
        var start = new ProcessStartInfo("dotnet") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var arg in new[] { Path.Combine(AppContext.BaseDirectory, "avascope.dll"), "run-scenario", "--request", requestPath }) start.ArgumentList.Add(arg);
        start.Environment[AgentRunStore.DirectoryEnvironmentVariable] = StorePath;
        using var owner = Process.Start(start)!;
        var stdout = owner.StandardOutput.ReadToEndAsync();
        var stderr = owner.StandardError.ReadToEndAsync();
        AgentRunStore.Record? record = null;
        var store = new AgentRunStore(StorePath);
        try
        {
            for (var i = 0; i < 200 && record?.ControlToken is null && !owner.HasExited; i++)
            {
                if (Directory.Exists(StorePath))
                {
                    var file = Directory.EnumerateFiles(StorePath, "record.json", SearchOption.AllDirectories).SingleOrDefault();
                    if (file is not null) record = JsonSerializer.Deserialize<AgentRunStore.Record>(File.ReadAllText(file), new JsonSerializerOptions(JsonSerializerDefaults.Web));
                }
                if (record?.ControlToken is null) await Task.Delay(50);
            }
            Assert.NotNull(record?.SessionId);
            Assert.NotNull(record.ControlToken);
            Assert.True((await store.RecoverAsync(new(record.RunId))).Value!.Active);
            var outsider = new LocalBridgeClient(Manifests);
            Assert.Equal("session_control_conflict", (await outsider.SessionControlAsync(record.SessionId!, new("acquire", "other-agent"))).Error!.Code);
            owner.Kill(); // Simulates only the agent dying; deliberately preserve its app for recovery.
            await owner.WaitForExitAsync();
            var inspect = await store.RecoverAsync(new(record.RunId));
            for (var i = 0; i < 100 && inspect.Value?.Active == true; i++)
            {
                await Task.Delay(20);
                inspect = await store.RecoverAsync(new(record.RunId));
            }
            Assert.Equal("abandoned", inspect.Value!.State);
            Assert.DoesNotContain(record.ControlToken!, JsonSerializer.Serialize(inspect.Value));
            var resume = await store.RecoverAsync(new(record.RunId, "resume"));
            Assert.True(resume.Success, resume.Error?.Message);
            Assert.Equal("resumed", resume.Value!.State);
            Assert.NotNull(resume.Value.ControlToken);
            var cleanup = await store.RecoverAsync(new(record.RunId, "cleanup"));
            Assert.True(cleanup.Success, cleanup.Error?.Message);
            Assert.Equal("cleaned", cleanup.Value!.State);
            Assert.Empty(cleanup.Value.Diagnostics);
            Assert.DoesNotContain(new LocalBridgeClient(Manifests).ListSessionManifests(), item => item.SessionId == record.SessionId);
            Assert.True(Directory.Exists(Output));
            Assert.True(Directory.Exists(Path.Combine(Output, "isolated-state")));
        }
        finally
        {
            if (!owner.HasExited) { owner.Kill(); await owner.WaitForExitAsync(); }
            if (record is not null) await store.RecoverAsync(new(record.RunId, "cleanup"));
            await File.WriteAllTextAsync(Path.Combine(_root, "owner.stdout.log"), await stdout);
            await File.WriteAllTextAsync(Path.Combine(_root, "owner.stderr.log"), await stderr);
        }
    }

    private static string FixturePath => Path.Combine(FindRepository(), "tests", "AvaScope.LifecycleTestApp", "bin",
        new DirectoryInfo(AppContext.BaseDirectory).Parent!.Name, "net10.0", "AvaScope.LifecycleTestApp.dll");

    private static string FindRepository()
    {
        for (var path = new DirectoryInfo(AppContext.BaseDirectory); path is not null; path = path.Parent)
            if (File.Exists(Path.Combine(path.FullName, "AvaScope.slnx"))) return path.FullName;
        throw new DirectoryNotFoundException("Repository root was not found.");
    }

    public void Dispose() { if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true); }

    private sealed class Clock : TimeProvider
    {
        private long _ticks;
        public override long TimestampFrequency => TimeSpan.TicksPerSecond;
        public override long GetTimestamp() => _ticks;
        public override DateTimeOffset GetUtcNow() => DateTimeOffset.UnixEpoch.AddTicks(_ticks);
        public void Advance(int ms) => _ticks += TimeSpan.FromMilliseconds(ms).Ticks;
    }
}
