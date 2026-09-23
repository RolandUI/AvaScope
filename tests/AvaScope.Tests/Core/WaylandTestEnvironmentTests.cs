using System.Diagnostics;
using System.Text.Json;
using AvaScope.Core;
using AvaScope.Protocol;

namespace AvaScope.Tests.Core;

public sealed class WaylandTestEnvironmentTests : IDisposable
{
    private readonly string _root = Path.Combine(Environment.GetEnvironmentVariable("AVASCOPE_VALIDATION_EVIDENCE_ROOT") ?? Path.GetTempPath(), "AvaScope.Tests", "wayland-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task InvalidProfilesAndUnownedEnvironmentsFailBeforeLaunch()
    {
        Assert.Equal("wayland_xwayland_unsupported", WaylandTestEnvironment.Validate(new(Lane: "xwayland"))!.Code);
        Assert.NotNull(WaylandTestEnvironment.Validate(new(KeyboardLayout: "us\n[core]")));
        Assert.NotNull(WaylandTestEnvironment.Validate(new(Width: 8192, Height: 8192, Scale: 4)));
        Assert.NotNull(WaylandTestEnvironment.Validate(new(Scale: 0)));
        var client = new LocalBridgeClient(Path.Combine(_root, "sessions"));
        var runner = new RuntimeScenarioRunner();
        var steps = new SemanticWorkflowStep[] { new("wait", waitMs: 1) };
        Assert.Equal("wayland_environment_ownership_required", (await runner.RunAsync(client, new(steps, sessionId: new("unowned"), waylandEnvironment: new()))).Error!.Code);
        Assert.Equal("wayland_environment_ownership_required", (await runner.RunAsync(client, new(steps, launch: new(command: "not-launched"), terminateLaunchedProcess: true, x11Environment: new(), waylandEnvironment: new()))).Error!.Code);
        Assert.False(Directory.Exists(_root));
        Directory.CreateDirectory(_root);
        var file = Path.Combine(_root, "profile.json");
        File.WriteAllText(file, """{"schemaVersion":1,"profiles":{"wayland":{"scenario":{"launch":{"command":"dotnet"},"waylandEnvironment":{"scale":2,"keyboardLayout":"hu"},"steps":[{"action":"wait","waitMs":1}]}}}}""");
        var profile = AgentTestProfiles.Resolve(file, "wayland", "linux");
        Assert.True(profile.Success, profile.Error?.Message);
        Assert.Equal(2, profile.Value!.Scenario.GetProperty("waylandEnvironment").GetProperty("scale").GetInt32());
        Assert.True(profile.Value.Scenario.GetProperty("terminateLaunchedProcess").GetBoolean());
    }

    [Fact]
    public async Task UnsupportedPlatformsDoNotCreateResources()
    {
        if (OperatingSystem.IsLinux()) return;
        await using var environment = new WaylandTestEnvironment(new(), _root);
        Assert.Equal("wayland_environment_unsupported", (await environment.StartAsync()).Error!.Code);
        Assert.Empty(environment.Evidence.Helpers); Assert.False(Directory.Exists(_root));
    }

    [Fact]
    public async Task LinuxConcurrentCompositorsObserveGeometryAndCleanOnlyOwnedSockets()
    {
        if (!OperatingSystem.IsLinux()) return;
        var original = Environment.GetEnvironmentVariable("WAYLAND_DISPLAY");
        await using var first = new WaylandTestEnvironment(new(Width: 800, Height: 600), Path.Combine(_root, "first"));
        await using var second = new WaylandTestEnvironment(new(Width: 640, Height: 480, Scale: 2, KeyboardLayout: "hu"), Path.Combine(_root, "second"));
        var results = await Task.WhenAll(first.StartAsync(), second.StartAsync());
        Assert.All(results, r => Assert.True(r.Success, r.Error?.Message));
        Assert.NotEqual(first.Evidence.Display, second.Evidence.Display);
        Assert.Equal(original, Environment.GetEnvironmentVariable("WAYLAND_DISPLAY"));
        Assert.Equal(1280, second.Evidence.Wayland!.OutputPixelWidth);
        Assert.Equal(960, second.Evidence.Wayland.OutputPixelHeight);
        Assert.Equal("hu", second.Evidence.Wayland.KeyboardLayout);
        Assert.Equal("configured_only_no_native_keyboard_seat", second.Evidence.Wayland.KeyboardEvidence);
        foreach (var environment in new[] { first, second })
        {
            Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute, File.GetUnixFileMode(environment.Evidence.RuntimeDirectory!));
            Assert.Equal("disabled", environment.Evidence.TcpListening);
            Assert.Equal("", environment.EnvironmentVariables["DISPLAY"]);
            Assert.StartsWith("unix:path=" + environment.Evidence.RuntimeDirectory, environment.EnvironmentVariables["DBUS_SESSION_BUS_ADDRESS"]);
            var pid = Assert.Single(environment.Evidence.Helpers).ProcessId;
            var sockets = Directory.EnumerateFileSystemEntries($"/proc/{pid}/fd").Select(p => new FileInfo(p).LinkTarget)
                .Where(p => p?.StartsWith("socket:[", StringComparison.Ordinal) == true).ToHashSet();
            foreach (var table in new[] { "/proc/net/tcp", "/proc/net/tcp6" }.Where(File.Exists))
                Assert.DoesNotContain(File.ReadLines(table).Skip(1).Select(l => l.Split(' ', StringSplitOptions.RemoveEmptyEntries)),
                    fields => fields.Length > 9 && fields[3] == "0A" && sockets.Contains("socket:[" + fields[9] + "]"));
        }
        await first.DisposeAsync();
        Assert.False(Assert.Single(second.Evidence.Helpers).Exited);
        Assert.True(File.Exists(second.Evidence.Display));
        await second.DisposeAsync();
        foreach (var environment in new[] { first, second }) AssertClean(environment.Evidence);
    }

    [Fact]
    public async Task LinuxStartupCancellationCrashAndLaunchFailureCleanOwnedResources()
    {
        if (!OperatingSystem.IsLinux()) return;
        await using var cancelled = new WaylandTestEnvironment(new(), Path.Combine(_root, "cancelled"));
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        Assert.Equal("wayland_environment_cancelled", (await cancelled.StartAsync(cancellation.Token)).Error!.Code);
        AssertClean(cancelled.Evidence);
        await using var crashed = new WaylandTestEnvironment(new(), Path.Combine(_root, "crashed"));
        Assert.True((await crashed.StartAsync()).Success);
        var token = crashed.FailureToken;
        using (var compositor = Process.GetProcessById(Assert.Single(crashed.Evidence.Helpers).ProcessId))
        { compositor.Kill(); await compositor.WaitForExitAsync(); }
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Task.Delay(5000, token));
        Assert.Equal("wayland_helper_exited", crashed.UnexpectedExit!.Code);
        await crashed.DisposeAsync(); AssertClean(crashed.Evidence);
        var failed = await new RuntimeScenarioRunner().RunAsync(new LocalBridgeClient(Path.Combine(_root, "sessions")),
            new([new("wait", waitMs: 1)], launch: new(command: "/nonexistent/avascope-host"), outputDirectory: Path.Combine(_root, "failed"),
                terminateLaunchedProcess: true, waylandEnvironment: new()));
        Assert.Equal("failed", failed.Value!.Status); AssertClean(failed.Value.Environment!);
        using var cancel = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var output = Path.Combine(_root, "interrupted");
        var running = new RuntimeScenarioRunner().RunAsync(new LocalBridgeClient(Path.Combine(_root, "sessions2")),
            new([new("wait", waitMs: 1)], launch: new(command: "/bin/sleep", argumentList: ["60"]), outputDirectory: output,
                terminateLaunchedProcess: true, waylandEnvironment: new()), cancel.Token);
        while (!Directory.Exists(Path.Combine(output, "launch")) && !running.IsCompleted) await Task.Delay(30, cancel.Token);
        cancel.Cancel(); var result = await running;
        Assert.Equal("cancelled", result.Value!.Status); AssertClean(result.Value.Environment!);
    }

    private static void AssertClean(RuntimeEnvironmentEvidence evidence)
    {
        Assert.Equal("closed", evidence.Status); Assert.True(evidence.RuntimeDirectoryRemoved, JsonSerializer.Serialize(evidence));
        Assert.All(evidence.Helpers, h => Assert.True(h.Exited));
        Assert.DoesNotContain(evidence.Diagnostics, d => d.Code.Contains("cleanup"));
    }

    [Fact]
    public async Task LinuxAbandonedRunRecoveryUsesTheRecordedCompositorAndDirectory()
    {
        if (!OperatingSystem.IsLinux()) return;
        var store = new AgentRunStore(Path.Combine(_root, "store"));
        using var registration = store.Begin(Path.Combine(_root, "run"), null, null, Path.Combine(_root, "sessions"), null).Value!;
        await using var environment = new WaylandTestEnvironment(new(), Path.Combine(_root, "run", "environment"))
        { ProcessStarted = registration.ProcessStarted, RuntimeDirectoryCreated = registration.OwnRuntimeDirectory, EvidenceChanged = registration.EnvironmentChanged };
        Assert.True((await environment.StartAsync()).Success);
        ((IRuntimeTestEnvironment)environment).RetainForRecovery();
        registration.Dispose();
        var recovered = await store.RecoverAsync(new(registration.RunId, "cleanup"));
        Assert.True(recovered.Success, recovered.Error?.Message);
        Assert.Equal("cleaned", recovered.Value!.State); Assert.Empty(recovered.Value.Diagnostics);
        Assert.False(Directory.Exists(environment.Evidence.RuntimeDirectory));
        Assert.All(environment.Evidence.Helpers, h => Assert.True(h.Exited));
        await environment.DisposeAsync(); AssertClean(environment.Evidence);
    }
    public void Dispose()
    { if (Environment.GetEnvironmentVariable("AVASCOPE_VALIDATION_EVIDENCE_ROOT") is null && Directory.Exists(_root)) Directory.Delete(_root, recursive: true); }
}
