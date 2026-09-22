using System.Diagnostics;
using System.Net.NetworkInformation;
using AvaScope.Core;
using AvaScope.Protocol;

namespace AvaScope.Tests.Core;

public sealed class X11TestEnvironmentTests : IDisposable
{
    private readonly string _root = Path.Combine(Environment.GetEnvironmentVariable("AVASCOPE_VALIDATION_EVIDENCE_ROOT") ?? Path.GetTempPath(), "AvaScope.Tests", "x11-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task InvalidOwnershipAndRemoteDisplaysAreRejectedBeforeDispatch()
    {
        Assert.NotNull(X11TestEnvironment.Validate(new(Mode: "existing", Display: "example.org:0")));
        Assert.NotNull(X11TestEnvironment.Validate(new(Mode: "existing", Display: ":1", WindowManager: true)));
        Assert.NotNull(X11TestEnvironment.Validate(new(Display: ":1")));
        Assert.NotNull(X11TestEnvironment.Validate(new(Width: 999999)));
        Assert.NotNull(X11TestEnvironment.Validate(new(TimeoutMs: 0)));
        var result = await new RuntimeScenarioRunner().RunAsync(new LocalBridgeClient(Path.Combine(_root, "sessions")),
            new([new(SemanticWorkflowActions.Wait, "wait", waitMs: 1)], sessionId: new("external"), x11Environment: new()));
        Assert.Equal("x11_environment_ownership_required", result.Error!.Code);
        Assert.False(Directory.Exists(_root));
    }

    [Fact]
    public async Task UnsupportedPlatformsNeverStartHelpers()
    {
        if (OperatingSystem.IsLinux()) return;
        await using var environment = new X11TestEnvironment(new(), _root);
        var result = await environment.StartAsync();
        Assert.Equal("x11_environment_unsupported", result.Error!.Code);
        Assert.Empty(environment.Evidence.Helpers);
        Assert.False(Directory.Exists(_root));
    }

    [Fact]
    public async Task LinuxConcurrentDisplaysUsePrivateCookiesAndPreserveExistingDesktop()
    {
        if (!OperatingSystem.IsLinux()) return;
        await using var first = new X11TestEnvironment(new(WindowManager: true, SessionBus: true), Path.Combine(_root, "first"));
        await using var second = new X11TestEnvironment(new(), Path.Combine(_root, "second"));
        var results = await Task.WhenAll(first.StartAsync(), second.StartAsync());
        Assert.All(results, result => Assert.True(result.Success, result.Error?.Message));
        Assert.NotEqual(first.Evidence.Display, second.Evidence.Display);
        Assert.NotEqual(first.Evidence.RuntimeDirectory, second.Evidence.RuntimeDirectory);
        Assert.Equal(3, first.Evidence.Helpers.Count);
        foreach (var environment in new[] { first, second })
        {
            Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(environment.EnvironmentVariables["XAUTHORITY"]));
            var port = 6000 + int.Parse(environment.Evidence.Display![1..]);
            Assert.DoesNotContain(IPGlobalProperties.GetIPGlobalProperties().GetActiveTcpListeners(), endpoint => endpoint.Port == port);
        }
        var noAuthority = Path.Combine(_root, "empty-authority");
        File.WriteAllText(noAuthority, string.Empty);
        using (var denied = new Process { StartInfo = new("xdpyinfo") { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true } })
        {
            denied.StartInfo.Environment["DISPLAY"] = first.Evidence.Display;
            denied.StartInfo.Environment["XAUTHORITY"] = noAuthority;
            denied.Start();
            var stdout = denied.StandardOutput.ReadToEndAsync();
            var stderr = denied.StandardError.ReadToEndAsync();
            await denied.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5));
            await Task.WhenAll(stdout, stderr);
            Assert.NotEqual(0, denied.ExitCode);
        }
        await using (var existing = new X11TestEnvironment(new(Mode: "existing", Display: first.Evidence.Display,
            Xauthority: first.EnvironmentVariables["XAUTHORITY"]), Path.Combine(_root, "existing")))
        {
            Assert.True((await existing.StartAsync()).Success);
            Assert.Empty(existing.Evidence.Helpers);
        }
        Assert.All(first.Evidence.Helpers, helper => Assert.False(helper.Exited));
        await first.DisposeAsync();
        await second.DisposeAsync();
        foreach (var environment in new[] { first, second })
        {
            Assert.Equal("closed", environment.Evidence.Status);
            Assert.True(environment.Evidence.RuntimeDirectoryRemoved);
            Assert.All(environment.Evidence.Helpers, helper => Assert.True(helper.Exited));
            Assert.All(environment.Evidence.Helpers, helper => Assert.True(new FileInfo(helper.StderrPath).Length <= 262144));
        }
    }

    [Fact]
    public async Task LinuxStartupCancellationAndHelperCrashCleanOnlyOwnedResources()
    {
        if (!OperatingSystem.IsLinux()) return;
        await using var cancelled = new X11TestEnvironment(new(), Path.Combine(_root, "cancelled"));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        Assert.False((await cancelled.StartAsync(cancellation.Token)).Success);
        Assert.All(cancelled.Evidence.Helpers, helper => Assert.True(helper.Exited));
        Assert.True(cancelled.Evidence.RuntimeDirectoryRemoved);

        await using var crashed = new X11TestEnvironment(new(), Path.Combine(_root, "crashed"));
        Assert.True((await crashed.StartAsync()).Success);
        var token = crashed.FailureToken;
        using (var helper = Process.GetProcessById(Assert.Single(crashed.Evidence.Helpers).ProcessId))
        {
            helper.Kill();
            await helper.WaitForExitAsync();
        }
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Task.Delay(5000, token));
        Assert.Equal("x11_helper_exited", crashed.UnexpectedExit!.Code);
        await crashed.DisposeAsync();
        Assert.True(crashed.Evidence.RuntimeDirectoryRemoved);
        Assert.All(crashed.Evidence.Helpers, helper => Assert.True(helper.Exited));
        Assert.False(File.Exists("/tmp/.X" + crashed.Evidence.Display![1..] + "-lock"));
    }

    [Fact]
    public async Task LinuxScenarioLaunchFailureAndCancellationAlwaysCloseDesktop()
    {
        if (!OperatingSystem.IsLinux()) return;
        var client = new LocalBridgeClient(Path.Combine(_root, "sessions"));
        var failed = await new RuntimeScenarioRunner().RunAsync(client,
            new([new(SemanticWorkflowActions.Wait, "wait", waitMs: 1)], launch: new(command: "/nonexistent/avascope-host"),
                outputDirectory: Path.Combine(_root, "failed"), terminateLaunchedProcess: true, x11Environment: new()));
        Assert.Equal("failed", failed.Value!.Status);
        Assert.Equal("closed", failed.Value.Environment!.Status);
        Assert.True(failed.Value.Environment.RuntimeDirectoryRemoved);
        Assert.All(failed.Value.Environment.Helpers, helper => Assert.True(helper.Exited));

        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var output = Path.Combine(_root, "interrupted");
        var running = new RuntimeScenarioRunner().RunAsync(client,
            new([new(SemanticWorkflowActions.Wait, "wait", waitMs: 1)], launch: new(command: "/bin/sleep", argumentList: ["60"]),
                outputDirectory: output, terminateLaunchedProcess: true, x11Environment: new()), cancellation.Token);
        while (!Directory.Exists(Path.Combine(output, "launch")) && !running.IsCompleted)
            await Task.Delay(30, cancellation.Token);
        cancellation.Cancel();
        var result = await running;
        Assert.Equal("cancelled", result.Value!.Status);
        Assert.Equal("closed", result.Value.Environment!.Status);
        Assert.True(result.Value.Environment.RuntimeDirectoryRemoved);
        Assert.All(result.Value.Environment.Helpers, helper => Assert.True(helper.Exited));
    }

    public void Dispose()
    {
        if (Environment.GetEnvironmentVariable("AVASCOPE_VALIDATION_EVIDENCE_ROOT") is null && Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }
}
