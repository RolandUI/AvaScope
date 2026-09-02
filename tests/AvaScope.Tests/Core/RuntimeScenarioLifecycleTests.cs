using System.Diagnostics;
using System.Text.Json;
using AvaScope.Core;
using AvaScope.Protocol;

namespace AvaScope.Tests.Core;

public sealed class RuntimeScenarioLifecycleTests : IDisposable
{
    private readonly string _testRoot = Path.Combine(
        Path.GetTempPath(),
        "AvaScope.Tests",
        $"scenario-lifecycle-{Guid.NewGuid():N}");

    public RuntimeScenarioLifecycleTests()
    {
        Directory.CreateDirectory(_testRoot);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testRoot))
        {
            Directory.Delete(_testRoot, recursive: true);
        }
    }

    [Fact]
    public async Task BuildStagePassesBeforeAttachAndRedactsEnvironmentValues()
    {
        var projectPath = WriteProject("build-pass", failBuild: false);
        const string secret = "scenario-secret-value";
        var request = CreateRequest(
            "build-pass",
            build: new RuntimeScenarioBuildOptions(
                projectPath,
                noRestore: false,
                arguments: ["--nologo"],
                environment: new Dictionary<string, string> { ["SCENARIO_SECRET"] = secret }));

        var result = await new RuntimeScenarioRunner().RunAsync(
            new LocalBridgeClient(Path.Combine(_testRoot, "missing-manifests")),
            request);

        Assert.True(result.Success, result.Error?.Message);
        Assert.Equal("failed", result.Value!.Status);
        Assert.Equal(RuntimeScenarioFailureStages.Attach, result.Value.FailureStage);
        Assert.NotNull(result.Value.Build);
        Assert.Equal(RuntimeScenarioLifecycleStatuses.Passed, result.Value.Build!.Status);
        Assert.Equal(0, result.Value.Build.ExitCode);
        Assert.True(File.Exists(result.Value.Build.StdoutPath));
        Assert.True(File.Exists(result.Value.Build.StderrPath));
        Assert.Equal("SCENARIO_SECRET", result.Value.Build.Metadata["environmentVariableNames"]);
        Assert.DoesNotContain(secret, JsonSerializer.Serialize(result.Value), StringComparison.Ordinal);
    }

    [Fact]
    public async Task BuildFailureStopsBeforeAttachAndPreservesStructuredLogs()
    {
        var projectPath = WriteProject("build-fail", failBuild: true);
        var request = CreateRequest(
            "build-fail",
            build: new RuntimeScenarioBuildOptions(projectPath, arguments: ["--nologo"]));

        var result = await new RuntimeScenarioRunner().RunAsync(
            new LocalBridgeClient(Path.Combine(_testRoot, "missing-manifests")),
            request);

        Assert.True(result.Success, result.Error?.Message);
        Assert.Equal("failed", result.Value!.Status);
        Assert.Equal(RuntimeScenarioFailureStages.Build, result.Value.FailureStage);
        Assert.Equal(RuntimeScenarioLifecycleStatuses.Failed, result.Value.Build!.Status);
        Assert.NotEqual(0, result.Value.Build.ExitCode);
        Assert.Equal("runtime_scenario_build_failed", result.Value.Build.Diagnostic!.Code);
        Assert.Contains("Expected lifecycle build failure", await File.ReadAllTextAsync(result.Value.Build.StdoutPath), StringComparison.Ordinal);
        Assert.Null(result.Value.Attach);
        Assert.Null(result.Value.Readiness);
    }

    [Fact]
    public async Task BuildTimeoutTerminatesOwnedBuildProcessAndPreservesLogs()
    {
        var projectPath = WriteProject("build-timeout", failBuild: false);
        var request = CreateRequest(
            "build-timeout",
            build: new RuntimeScenarioBuildOptions(projectPath, timeoutMs: 1));

        var result = await new RuntimeScenarioRunner().RunAsync(
            new LocalBridgeClient(Path.Combine(_testRoot, "missing-manifests")),
            request);

        Assert.True(result.Success, result.Error?.Message);
        Assert.Equal(RuntimeScenarioFailureStages.Build, result.Value!.FailureStage);
        Assert.Equal(RuntimeScenarioLifecycleStatuses.TimedOut, result.Value.Build!.Status);
        Assert.Equal("runtime_scenario_build_timed_out", result.Value.Build.Diagnostic!.Code);
        Assert.True(File.Exists(result.Value.Build.StdoutPath));
        Assert.True(File.Exists(result.Value.Build.StderrPath));
    }

    [Fact]
    public async Task BuildCancellationReturnsCancelledEvidence()
    {
        var projectPath = WriteProject("build-cancel", failBuild: false);
        var request = CreateRequest(
            "build-cancel",
            build: new RuntimeScenarioBuildOptions(projectPath));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        var result = await new RuntimeScenarioRunner().RunAsync(
            new LocalBridgeClient(Path.Combine(_testRoot, "missing-manifests")),
            request,
            cancellation.Token);

        Assert.True(result.Success, result.Error?.Message);
        Assert.Equal("cancelled", result.Value!.Status);
        Assert.Equal(RuntimeScenarioFailureStages.Build, result.Value.FailureStage);
        Assert.Equal(RuntimeScenarioLifecycleStatuses.Cancelled, result.Value.Build!.Status);
        Assert.Equal("runtime_scenario_build_cancelled", result.Value.Build.Diagnostic!.Code);
    }

    [Fact]
    public async Task LaunchStartFailureIsDistinctAndPreservesArtifactPaths()
    {
        var request = CreateRequest(
            "launch-start-failure",
            launch: new RuntimeScenarioLaunchOptions(
                $"missing-command-{Guid.NewGuid():N}",
                outputDirectory: Path.Combine(_testRoot, "launch-start")));

        var result = await new RuntimeScenarioRunner().RunAsync(
            new LocalBridgeClient(Path.Combine(_testRoot, "launch-start-manifests")),
            request);

        Assert.True(result.Success, result.Error?.Message);
        Assert.Equal(RuntimeScenarioFailureStages.Launch, result.Value!.FailureStage);
        Assert.Equal(RuntimeScenarioLifecycleStatuses.Failed, result.Value.Readiness!.Status);
        Assert.True(File.Exists(result.Value.Readiness.StdoutPath));
        Assert.True(File.Exists(result.Value.Readiness.StderrPath));
    }

    [Fact]
    public async Task ChildExitBeforeBridgeIsDistinctFromReadinessTimeout()
    {
        var request = CreateRequest(
            "launch-exit",
            launch: new RuntimeScenarioLaunchOptions(
                "dotnet",
                argumentList: ["--info"],
                outputDirectory: Path.Combine(_testRoot, "launch-exit"),
                timeoutMs: 5000));

        var result = await new RuntimeScenarioRunner().RunAsync(
            new LocalBridgeClient(Path.Combine(_testRoot, "launch-exit-manifests")),
            request);

        Assert.True(result.Success, result.Error?.Message);
        Assert.Equal(RuntimeScenarioFailureStages.Launch, result.Value!.FailureStage);
        Assert.Equal(RuntimeScenarioLifecycleStatuses.ProcessExited, result.Value.Readiness!.Status);
        Assert.Contains("exited before", result.Value.Readiness.Diagnostic!.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ReadinessTimeoutTerminatesOnlyTheDirectlyOwnedProcessTree()
    {
        var launch = CreateSleepingLaunch("launch-timeout", timeoutMs: 150);
        var request = CreateRequest("launch-timeout", launch: launch);

        var result = await new RuntimeScenarioRunner().RunAsync(
            new LocalBridgeClient(Path.Combine(_testRoot, "launch-timeout-manifests")),
            request);

        Assert.True(result.Success, result.Error?.Message);
        Assert.Equal(RuntimeScenarioFailureStages.BridgeReadiness, result.Value!.FailureStage);
        Assert.Equal(RuntimeScenarioLifecycleStatuses.TimedOut, result.Value.Readiness!.Status);
        Assert.True(result.Value.Readiness.ProcessId > 0);
        AssertProcessExited(result.Value.Readiness.ProcessId!.Value);
    }

    [Fact]
    public async Task LaunchCancellationTerminatesOnlyTheDirectlyOwnedProcessTree()
    {
        var launch = CreateSleepingLaunch("launch-cancel", timeoutMs: 30000);
        var request = CreateRequest("launch-cancel", launch: launch);
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(250));

        var result = await new RuntimeScenarioRunner().RunAsync(
            new LocalBridgeClient(Path.Combine(_testRoot, "launch-cancel-manifests")),
            request,
            cancellation.Token);

        Assert.True(result.Success, result.Error?.Message);
        Assert.Equal("cancelled", result.Value!.Status);
        Assert.Equal(RuntimeScenarioFailureStages.BridgeReadiness, result.Value.FailureStage);
        Assert.Equal(RuntimeScenarioLifecycleStatuses.Cancelled, result.Value.Readiness!.Status);
        AssertProcessExited(result.Value.Readiness.ProcessId!.Value);
    }

    [Fact]
    public async Task BuildLaunchAttachWorkflowAndOwnedCleanupCompleteInOneScenario()
    {
        var repositoryRoot = FindRepositoryRoot();
        var projectPath = Path.Combine(
            repositoryRoot,
            "tests",
            "AvaScope.LifecycleTestApp",
            "AvaScope.LifecycleTestApp.csproj");
        var scenarioDirectory = Path.Combine(_testRoot, "complete-lifecycle");
        var manifestDirectory = Path.Combine(scenarioDirectory, "manifests");
        var markerPath = Path.Combine(scenarioDirectory, "argument-marker.txt");
        var timelinePath = Path.Combine(scenarioDirectory, "timeline.md");
        const string secret = "lifecycle-secret-value";
        var request = new RuntimeScenarioRequest(
            [new SemanticWorkflowStep(SemanticWorkflowActions.Wait, "workflow-wait", waitMs: 1)],
            requestId: "complete-lifecycle",
            launch: new RuntimeScenarioLaunchOptions(
                projectPath: projectPath,
                argumentList: ["--marker", markerPath],
                configuration: "Debug",
                framework: "net10.0",
                noBuild: true,
                manifestDirectory: manifestDirectory,
                outputDirectory: Path.Combine(scenarioDirectory, "launch"),
                environment: new Dictionary<string, string>
                {
                    ["AVASCOPE_LIFECYCLE_TEST_SECRET"] = secret
                },
                timeoutMs: 15000),
            outputDirectory: scenarioDirectory,
            timelinePath: timelinePath,
            build: new RuntimeScenarioBuildOptions(
                projectPath,
                configuration: "Debug",
                framework: "net10.0",
                noRestore: true,
                arguments: ["--nologo"]),
            terminateLaunchedProcess: true);

        var result = await new RuntimeScenarioRunner().RunAsync(
            new LocalBridgeClient(manifestDirectory),
            request);

        Assert.True(result.Success, result.Error?.Message);
        Assert.True(
            string.Equals("passed", result.Value!.Status, StringComparison.Ordinal),
            JsonSerializer.Serialize(result.Value));
        Assert.Null(result.Value.FailureStage);
        Assert.Equal(RuntimeScenarioLifecycleStatuses.Passed, result.Value.Build!.Status);
        Assert.NotNull(result.Value.Launch);
        Assert.NotNull(result.Value.Attach);
        Assert.Equal(result.Value.Launch!.Session.SessionId, result.Value.Attach!.Session.SessionId);
        Assert.Equal(RuntimeScenarioLifecycleStatuses.Ready, result.Value.Readiness!.Status);
        Assert.Equal("topLevel:lifecycle", result.Value.TopLevelId);
        Assert.Equal("topLevel:lifecycle", Assert.Single(result.Value.TopLevels).Id);
        Assert.Equal("passed", result.Value.Workflow!.Status);
        Assert.Equal(CloseSessionOutcomes.Terminated, result.Value.Cleanup!.Outcome);
        Assert.True(result.Value.Cleanup.ProcessTerminated);
        Assert.Equal(secret, await File.ReadAllTextAsync(markerPath));
        Assert.DoesNotContain(secret, JsonSerializer.Serialize(result.Value), StringComparison.Ordinal);
        Assert.Contains("Lifecycle test bridge ready.", await File.ReadAllTextAsync(result.Value.Launch.StdoutPath), StringComparison.Ordinal);
        Assert.True(File.Exists(timelinePath));
        var timeline = await File.ReadAllTextAsync(timelinePath);
        Assert.Contains("Build status: `passed`", timeline, StringComparison.Ordinal);
        Assert.Contains("Bridge readiness: `ready`", timeline, StringComparison.Ordinal);
        Assert.Contains("Cleanup outcome: `terminated`", timeline, StringComparison.Ordinal);
        AssertProcessExited(result.Value.Launch.ProcessId);
    }

    private RuntimeScenarioRequest CreateRequest(
        string requestId,
        RuntimeScenarioBuildOptions? build = null,
        RuntimeScenarioLaunchOptions? launch = null)
    {
        return new RuntimeScenarioRequest(
            [new SemanticWorkflowStep(SemanticWorkflowActions.Wait, "wait", waitMs: 1)],
            requestId,
            launch,
            sessionId: launch is null ? new SessionId($"missing-{requestId}") : null,
            topLevelId: "topLevel:missing",
            outputDirectory: Path.Combine(_testRoot, requestId),
            build: build,
            terminateLaunchedProcess: true);
    }

    private RuntimeScenarioLaunchOptions CreateSleepingLaunch(string name, int timeoutMs)
    {
        return OperatingSystem.IsWindows()
            ? new RuntimeScenarioLaunchOptions(
                "powershell.exe",
                argumentList: ["-NoProfile", "-Command", "Start-Sleep -Seconds 30"],
                manifestDirectory: Path.Combine(_testRoot, $"{name}-manifests"),
                outputDirectory: Path.Combine(_testRoot, name),
                timeoutMs: timeoutMs)
            : new RuntimeScenarioLaunchOptions(
                "/bin/sh",
                argumentList: ["-c", "sleep 30"],
                manifestDirectory: Path.Combine(_testRoot, $"{name}-manifests"),
                outputDirectory: Path.Combine(_testRoot, name),
                timeoutMs: timeoutMs);
    }

    private string WriteProject(string name, bool failBuild)
    {
        var projectDirectory = Path.Combine(_testRoot, name, "project");
        Directory.CreateDirectory(projectDirectory);
        var projectPath = Path.Combine(projectDirectory, $"{name}.csproj");
        var failureTarget = failBuild
            ? "<Target Name=\"FailExpectedly\" BeforeTargets=\"Build\"><Error Text=\"Expected lifecycle build failure\" /></Target>"
            : string.Empty;
        File.WriteAllText(
            projectPath,
            $"<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup>{failureTarget}</Project>");
        return projectPath;
    }

    private static void AssertProcessExited(int processId)
    {
        try
        {
            using var process = Process.GetProcessById(processId);
            Assert.True(process.HasExited, $"Expected owned process {processId} to have exited.");
        }
        catch (ArgumentException)
        {
        }
    }

    private static string FindRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "AvaScope.slnx")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Could not find the AvaScope repository root.");
    }
}
