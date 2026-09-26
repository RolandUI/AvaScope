using System.Diagnostics;
using System.Text.Json;
using AvaScope.Core;
using AvaScope.Mcp;
using AvaScope.Protocol;
using Xunit.Abstractions;

namespace AvaScope.Tests.Core;

public sealed class BridgeIntegrationVerifierTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "AvaScope.Tests", $"integration-verify-{Guid.NewGuid():N}");
    private readonly ITestOutputHelper _output;
    public BridgeIntegrationVerifierTests(ITestOutputHelper output)
    {
        _output = output;
        Directory.CreateDirectory(_root);
    }

    [Theory]
    [InlineData("none", "passed")]
    [InlineData("stdout", "passed")]
    [InlineData("stderr", "passed")]
    [InlineData("both", "passed")]
    [InlineData("timeout", "passed")]
    [InlineData("both", "cancelled")]
    [InlineData("timeout", "failed")]
    public async Task DisabledCleanupReportsCaptureFailureAndRecoversOnlyItsOwnManifest(string failure, string observationStatus)
    {
        var launch = SleepingLaunch();
        var info = new ProcessStartInfo(launch.Command!)
        {
            UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardOutput = true, RedirectStandardError = true
        };
        foreach (var argument in launch.ArgumentList) info.ArgumentList.Add(argument);
        using var process = Process.Start(info)!;
        var actualStdout = process.StandardOutput.ReadToEndAsync();
        var actualStderr = process.StandardError.ReadToEndAsync();
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var stdout = failure is "stdout" or "both" ? Task.WhenAll(actualStdout, Task.FromException(new IOException("controlled_stdout_failure"))) : actualStdout;
        var stderr = failure is "stderr" or "both" ? Task.WhenAll(actualStderr, Task.FromException(new IOException("controlled_stderr_failure"))) : actualStderr;
        if (failure == "timeout") stdout = Task.WhenAll(actualStdout, release.Task);
        var manifests = Directory.CreateDirectory(Path.Combine(_root, "sessions")).FullName;
        var ownedPath = Path.Combine(manifests, "owned.json");
        var foreignPath = Path.Combine(manifests, "foreign.json");
        File.WriteAllText(ownedPath, JsonSerializer.Serialize(new BridgeSessionManifest(SessionId.New(), process.Id, $"avs-{process.Id}-fixture", DateTimeOffset.UtcNow)));
        var foreign = JsonSerializer.Serialize(new BridgeSessionManifest(SessionId.New(), Environment.ProcessId, "foreign-fixture", DateTimeOffset.UtcNow));
        File.WriteAllText(foreignPath, foreign);
        var stages = new List<IntegrationVerificationStage> { new("disabled_startup", observationStatus, "Controlled observation completed.") };
        try
        {
            var error = await Record.ExceptionAsync(() => BridgeIntegrationVerifier.CleanupDisabledHostAsync(
                process, stdout, stderr, manifests, new Dictionary<string, string>(), stages));
            _output.WriteLine(JsonSerializer.Serialize(new { failure, observationStatus, errorType = error?.GetType().Name,
                processId = process.Id, exited = process.HasExited, ownedManifestExists = File.Exists(ownedPath),
                foreignUnchanged = File.ReadAllText(foreignPath) == foreign, stages }));
            Assert.Null(error);
            AssertExited(process.Id);
            Assert.False(File.Exists(ownedPath));
            Assert.Equal(foreign, File.ReadAllText(foreignPath));
            Assert.Equal(observationStatus, stages.Single(stage => stage.Name == "disabled_startup").Status);
            var cleanup = stages.Single(stage => stage.Name == "cleanup");
            Assert.Equal(failure == "none" ? "passed" : "failed", cleanup.Status);
            var evidencePath = Assert.Single(cleanup.EvidencePaths!);
            Assert.True(new FileInfo(evidencePath).Length < 8192);
            using var evidence = JsonDocument.Parse(File.ReadAllText(evidencePath));
            var data = evidence.RootElement;
            Assert.True(data.GetProperty("processExited").GetBoolean());
            Assert.Equal(process.Id, data.GetProperty("processId").GetInt32());
            Assert.Equal(5000, data.GetProperty("cleanupTimeoutMs").GetInt32());
            Assert.Equal("passed", data.GetProperty("resourceRecovery").GetString());
            if (failure == "none")
            {
                Assert.Empty(data.GetProperty("failures").EnumerateArray());
                Assert.Equal("RanToCompletion", data.GetProperty("stdoutStatus").GetString());
                Assert.Equal("RanToCompletion", data.GetProperty("stderrStatus").GetString());
            }
            else
            {
                var detail = Assert.Single(data.GetProperty("failures").EnumerateArray());
                Assert.Equal("output_drain", detail.GetProperty("phase").GetString());
                Assert.Equal(failure == "timeout", detail.GetProperty("timedOut").GetBoolean());
                if (failure is "stdout" or "both") Assert.Equal("controlled_stdout_failure", data.GetProperty("stdoutError").GetProperty("message").GetString());
                if (failure is "stderr" or "both") Assert.Equal("controlled_stderr_failure", data.GetProperty("stderrError").GetProperty("message").GetString());
            }
            _output.WriteLine(evidence.RootElement.GetRawText());
        }
        finally
        {
            release.TrySetResult();
            if (!process.HasExited) process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5));
            await Task.WhenAll(actualStdout, actualStderr).WaitAsync(TimeSpan.FromSeconds(5));
        }
    }

    [Fact]
    public async Task CallerCancellationRetainsObservationOutcomeAndCleansUpTheDisabledHost()
    {
        var production = Directory.CreateDirectory(Path.Combine(_root, "production")).FullName;
        using var cancellation = new CancellationTokenSource();
        var operation = new BridgeIntegrationVerifier().RunAsync(new(SleepingLaunch(), _root,
            BootstrapDisabled: true, ProductionOutputDirectory: production, ObservationMs: 30000), cancellation.Token);
        cancellation.Cancel();
        var result = await operation;
        Assert.True(result.Success, result.Error?.Message);
        var response = result.Value!;
        Assert.Equal("disabled_startup", response.FailureStage);
        Assert.Equal("cancelled", response.Stages.Single(stage => stage.Name == "disabled_startup").Status);
        Assert.Equal("passed", response.Stages.Single(stage => stage.Name == "cleanup").Status);
        var processId = int.Parse(response.Stages.Single(stage => stage.Name == "launch").Message.Split(' ')[^1].TrimEnd('.'));
        AssertExited(processId);
        _output.WriteLine(JsonSerializer.Serialize(response));
    }

    [Fact]
    public async Task InvalidProbeIsRejectedBeforeCreatingOutputOrLaunching()
    {
        var request = new BridgeIntegrationVerificationRequest(SleepingLaunch(), Path.Combine(_root, "run"), SafeInputTarget: new(name: "Save"));
        var result = await new BridgeIntegrationVerifier().RunAsync(request);
        Assert.False(result.Success);
        Assert.Equal("integration_verification_invalid", result.Error!.Code);
        Assert.False(Directory.Exists(request.OutputDirectory));
    }

    [Fact]
    public async Task BuildProviderAndLaunchFailuresHaveDistinctStagesAndReports()
    {
        var verifier = new BridgeIntegrationVerifier();
        var request = new BridgeIntegrationVerificationRequest(new("avascope-executable-does-not-exist"), _root);
        var build = await verifier.RunAsync(request with { Build = new(Path.Combine(_root, "missing.csproj")) });
        Assert.Equal("build", build.Value!.FailureStage);
        var provider = await verifier.RunAsync(request with { ProviderDirectory = Path.Combine(_root, "missing-provider") });
        Assert.Equal("provider", provider.Value!.FailureStage);
        var launch = await verifier.RunAsync(request);
        Assert.Equal("launch", launch.Value!.FailureStage);
        Assert.True(File.Exists(launch.Value.ReportPath));
        Assert.NotEqual(build.Value.ReportPath, provider.Value.ReportPath);
        var mcp = await AvaScopeMcpTools.VerifyIntegration(request with { Build = request.Build ?? new(Path.Combine(_root, "missing.csproj")) });
        Assert.Equal("build", mcp.Value!.FailureStage);
    }

    [Fact]
    public async Task DiscoveryTimeoutIsNotReportedAsLaunchFailureAndOwnedProcessStops()
    {
        var result = await new BridgeIntegrationVerifier().RunAsync(new(SleepingLaunch(), _root));
        Assert.Equal("discovery", result.Value!.FailureStage);
        Assert.Equal("passed", result.Value.Stages.Single(stage => stage.Name == "launch").Status);
        AssertExited(result.Value.Scenario!.Readiness!.ProcessId!.Value);
        Assert.Equal("passed", result.Value.Stages.Single(stage => stage.Name == "cleanup").Status);
    }

    [Fact]
    public async Task DisabledLaneChecksOutputAndStartupIndependentlyAndCleansUp()
    {
        var production = Path.Combine(_root, "production");
        Directory.CreateDirectory(production);
        File.WriteAllText(Path.Combine(production, "Host.dll"), "fixture");
        var request = new BridgeIntegrationVerificationRequest(SleepingLaunch(), _root, BootstrapDisabled: true, ProductionOutputDirectory: production, ObservationMs: 250);
        var passed = await new BridgeIntegrationVerifier().RunAsync(request);
        Assert.True(passed.Success, passed.Error?.Message);
        Assert.Equal("passed", passed.Value!.Status);
        Assert.Contains(passed.Value.Stages, stage => stage.Name == "disabled_startup" && stage.Status == "passed");
        var pid = int.Parse(passed.Value.Stages.Single(stage => stage.Name == "launch").Message.Split(' ')[^1].TrimEnd('.'));
        AssertExited(pid);
        File.WriteAllText(Path.Combine(production, "AvaScope.Bridge.dll"), "forbidden");
        var rejected = await new BridgeIntegrationVerifier().RunAsync(request);
        Assert.Equal("production_output", rejected.Value!.FailureStage);
        Assert.DoesNotContain(rejected.Value.Stages, stage => stage.Name == "launch");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task InspectionAndCloseFailuresStillRecoverOwnedProcessesAndManifests(bool failClose)
    {
        var launch = FixtureLaunch(failClose ? ["--fail-method", BridgeIpcMethods.CloseSession] : []);
        var result = await new BridgeIntegrationVerifier().RunAsync(new(launch, _root));
        Assert.True(result.Success, result.Error?.Message);
        var response = result.Value!;
        Assert.Equal("failed", response.Status);
        Assert.Equal("tree", response.FailureStage);
        AssertExited(response.Scenario!.Launch!.ProcessId);
        Assert.Empty(Directory.GetFiles(Path.Combine(Path.GetDirectoryName(response.ReportPath)!, "sessions"), "*.json"));
        Assert.Equal(failClose ? "failed" : "passed", response.Stages.Single(stage => stage.Name == "cleanup").Status);
    }

    [Fact]
    public async Task DisabledFixtureThatActivatesIsRejectedAndRecovered()
    {
        var production = Directory.CreateDirectory(Path.Combine(_root, "production")).FullName;
        var result = await new BridgeIntegrationVerifier().RunAsync(new(FixtureLaunch([]), _root,
            BootstrapDisabled: true, ProductionOutputDirectory: production, ObservationMs: 3000));
        Assert.True(result.Success, result.Error?.Message);
        Assert.Equal("disabled_startup", result.Value!.FailureStage);
        Assert.Equal("passed", result.Value.Stages.Single(stage => stage.Name == "cleanup").Status);
        Assert.Empty(Directory.GetFiles(Path.Combine(Path.GetDirectoryName(result.Value.ReportPath)!, "sessions"), "*.json"));
    }

    [Fact]
    public async Task WindowReadinessFailureIdentifiesItsStage()
    {
        var result = await new BridgeIntegrationVerifier().RunAsync(new(FixtureLaunch(["--empty-windows"], 2000), _root));
        Assert.Equal("readiness", result.Value!.FailureStage);
        AssertExited(result.Value.Scenario!.Launch!.ProcessId);
    }

    [Fact]
    public async Task SessionControlFailureIsNotMisreportedAsMissingApplicationWindows()
    {
        var result = await new BridgeIntegrationVerifier().RunAsync(new(FixtureLaunch(["--fail-method", BridgeIpcMethods.SessionControl]), _root));
        Assert.True(result.Success, result.Error?.Message);
        Assert.Equal("session_control", result.Value!.FailureStage);
        Assert.Equal("skipped", result.Value.Stages.Single(stage => stage.Name == "readiness").Status);
        Assert.Equal("passed", result.Value.Stages.Single(stage => stage.Name == "cleanup").Status);
        AssertExited(result.Value.Scenario!.Launch!.ProcessId);
    }

    private static RuntimeScenarioLaunchOptions SleepingLaunch() => OperatingSystem.IsWindows()
        ? new("powershell.exe", argumentList: ["-NoProfile", "-Command", "Start-Sleep -Seconds 30"], timeoutMs: 250)
        : new("/bin/sh", argumentList: ["-c", "sleep 30"], timeoutMs: 250);

    private static RuntimeScenarioLaunchOptions FixtureLaunch(string[] arguments, int timeoutMs = 15000)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "AvaScope.slnx"))) directory = directory.Parent;
        var configuration = new DirectoryInfo(AppContext.BaseDirectory).Parent!.Name;
        var assembly = Path.Combine(directory!.FullName, "tests", "AvaScope.LifecycleTestApp", "bin", configuration, "net10.0", "AvaScope.LifecycleTestApp.dll");
        return new("dotnet", argumentList: [assembly, .. arguments], timeoutMs: timeoutMs);
    }

    private static void AssertExited(int processId)
    {
        try { using var process = Process.GetProcessById(processId); Assert.True(process.HasExited); }
        catch (ArgumentException) { }
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);
}
