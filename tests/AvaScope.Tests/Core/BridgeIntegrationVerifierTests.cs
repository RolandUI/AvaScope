using System.Diagnostics;
using System.Text.Json;
using AvaScope.Core;
using AvaScope.Mcp;
using AvaScope.Protocol;

namespace AvaScope.Tests.Core;

public sealed class BridgeIntegrationVerifierTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "AvaScope.Tests", $"integration-verify-{Guid.NewGuid():N}");
    public BridgeIntegrationVerifierTests() => Directory.CreateDirectory(_root);

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
