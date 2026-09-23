using System.Diagnostics;
using System.Text.Json;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using AvaScope.Bridge;
using AvaScope.Core;
using AvaScope.Mcp;
using AvaScope.Protocol;
using ModelContextProtocol.Client;

namespace AvaScope.Tests.Bridge;

[Collection(BridgeCollectionDefinition.Name)]
public sealed class RuntimeTestFixtureTests
{
    [Fact]
    public async Task ActualCliAndMcpScenariosApplyTheFixtureLifecycle()
    {
        await WithHost(async host =>
        {
            Directory.CreateDirectory(host.Output);
            var path = Path.Combine(host.Output, "request.json");
            await File.WriteAllTextAsync(path, JsonSerializer.Serialize(host.Request("empty")));
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            var start = new ProcessStartInfo("dotnet") { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true };
            foreach (var argument in new[] { Path.Combine(AppContext.BaseDirectory, "avascope.dll"), "run-scenario", "--request", path, "--manifest-dir", host.Client.ManifestDirectory }) start.ArgumentList.Add(argument);
            using var process = Process.Start(start)!;
            var output = process.StandardOutput.ReadToEndAsync(timeout.Token);
            var errors = process.StandardError.ReadToEndAsync(timeout.Token);
            await process.WaitForExitAsync(timeout.Token);
            Assert.Equal(0, process.ExitCode);
            Assert.True(string.IsNullOrWhiteSpace(await errors));
            var cli = JsonSerializer.Deserialize<ToolResult<RuntimeScenarioResponse>>(await output)!;
            Assert.Equal("passed", cli.Value!.Status);
            Assert.Equal("cleaned", cli.Value.TestFixture!.CleanupStatus);
            var environment = TestEnvironment.McpEnvironment();
            environment[AgentRunStore.DirectoryEnvironmentVariable] = Environment.GetEnvironmentVariable(AgentRunStore.DirectoryEnvironmentVariable)!;
            if (Environment.GetEnvironmentVariable("TMPDIR") is { } temporary) environment["TMPDIR"] = temporary;
            await using var client = await McpClient.CreateAsync(new StdioClientTransport(new StdioClientTransportOptions
            {
                Name = "Fixture parity", Command = "dotnet", Arguments = [Path.Combine(AppContext.BaseDirectory, "AvaScope.Mcp.dll")],
                InheritEnvironmentVariables = false, EnvironmentVariables = environment, ShutdownTimeout = TimeSpan.FromSeconds(3)
            }), cancellationToken: timeout.Token);
            var call = await client.CallToolAsync("run_scenario", new Dictionary<string, object?>
            {
                ["request"] = JsonSerializer.SerializeToElement(host.Request("empty")), ["manifestDirectory"] = host.Client.ManifestDirectory
            }, cancellationToken: timeout.Token);
            var mcp = JsonSerializer.Deserialize<ToolResult<RuntimeScenarioResponse>>(JsonSerializer.Serialize(call.StructuredContent))!;
            Assert.Equal("passed", mcp.Value!.Status);
            Assert.Equal(cli.Value.TestFixture.Name, mcp.Value.TestFixture!.Name);
            Assert.Equal(cli.Value.TestFixture.Version, mcp.Value.TestFixture.Version);
            Assert.Equal(cli.Value.TestFixture.CleanupStatus, mcp.Value.TestFixture.CleanupStatus);
            Assert.Equal(2, host.Prepared);
            Assert.Equal(2, host.Cleaned);
        });
    }

    [Fact]
    public async Task SeededEmptyAndOfflineFixturesRepeatThroughScenariosAndAlwaysCleanUp()
    {
        await WithHost(async host =>
        {
            foreach (var name in new[] { "seeded", "empty", "offline" })
            {
                for (var repeat = 0; repeat < 2; repeat++)
                {
                    var request = host.Request(name, parameters: name == "seeded" ? new Dictionary<string, string> { ["seed"] = "812771" } : null);
                    var result = await AvaScopeMcpTools.RunScenario(host.Client, request);
                    Assert.True(result.Success, result.Error?.Message);
                    Assert.Equal("passed", result.Value!.Status);
                    Assert.Equal("prepared", result.Value.TestFixture!.PreparationStatus);
                    Assert.Equal("ready", result.Value.TestFixture.ReadinessStatus);
                    Assert.Equal("cleaned", result.Value.TestFixture.CleanupStatus);
                    Assert.Equal("1", result.Value.TestFixture.Version);
                    Assert.Equal("test-memory", result.Value.TestFixture.ResourceId);
                    Assert.NotNull(result.Value.TestFixture.ReadinessObservation);
                    Assert.DoesNotContain("812771", JsonSerializer.Serialize(result.Value), StringComparison.Ordinal);
                    Assert.Contains("Fixture cleanup", await File.ReadAllTextAsync(result.Value.TimelinePath!), StringComparison.Ordinal);
                    Assert.Empty(host.Rows);
                    Assert.False(host.Offline);
                }
            }
            Assert.Equal(6, host.Prepared);
            Assert.Equal(6, host.Cleaned);
            Assert.Equal(host.SeededRuns[0], host.SeededRuns[1]);
            var policy = new RuntimeEvidencePolicy(host.Output, redactedText: ["812771"],
                allowedActions: [SemanticWorkflowActions.Inspect, SemanticWorkflowActions.CustomActions, SemanticWorkflowActions.CustomAction, SemanticWorkflowActions.WaitForState],
                allowedCustomActions: ["fixture.prepare.seeded", "fixture.cleanup.seeded"]);
            var privateRun = host.Request("seeded", parameters: new Dictionary<string, string> { ["seed"] = "812771" }, policy: policy);
            var guarded = await new RuntimeScenarioRunner().RunAsync(host.Client, privateRun);
            Assert.Equal("passed", guarded.Value!.Status);
            var audit = await File.ReadAllTextAsync(guarded.Value.Metadata["actionAuditPath"]);
            Assert.Contains("fixture.prepare.seeded", audit, StringComparison.Ordinal);
            Assert.Contains("fixture.cleanup.seeded", audit, StringComparison.Ordinal);
            Assert.DoesNotContain("812771", audit, StringComparison.Ordinal);
            Assert.DoesNotContain("812771", JsonSerializer.Serialize(guarded.Value), StringComparison.Ordinal);
        });
    }

    [Fact]
    public async Task UnknownResourcesInvalidParametersAndPolicyDenialsNeverReachFixtureHandlers()
    {
        await WithHost(async host =>
        {
            foreach (var request in new[]
            {
                host.Request("unknown"), host.Request("seeded", resource: "production"),
                host.Request("seeded", parameters: new Dictionary<string, string> { ["seed"] = "invalid" })
            })
            {
                var result = await new RuntimeScenarioRunner().RunAsync(host.Client, request);
                Assert.True(result.Success, result.Error?.Message);
                Assert.Equal("failed", result.Value!.Status);
                Assert.Equal("fixture_preparation", result.Value.FailureStage);
                Assert.Equal("rejected", result.Value.TestFixture!.PreparationStatus);
                Assert.Equal("not_needed", result.Value.TestFixture.CleanupStatus);
                Assert.Null(result.Value.Workflow);
            }
            var denied = await new RuntimeScenarioRunner().RunAsync(host.Client, host.Request("empty",
                policy: new RuntimeEvidencePolicy(host.Output, allowedActions: [SemanticWorkflowActions.Inspect])));
            Assert.Equal("validation", denied.Value!.FailureStage);
            var cleanupDenied = await new RuntimeScenarioRunner().RunAsync(host.Client, host.Request("empty",
                policy: new RuntimeEvidencePolicy(host.Output,
                    allowedActions: [SemanticWorkflowActions.Inspect, SemanticWorkflowActions.CustomActions, SemanticWorkflowActions.CustomAction, SemanticWorkflowActions.WaitForState],
                    allowedCustomActions: ["fixture.prepare.empty"])));
            Assert.Equal("fixture_preparation", cleanupDenied.Value!.FailureStage);
            Assert.Equal("rejected", cleanupDenied.Value.TestFixture!.PreparationStatus);
            Assert.Equal(0, host.Prepared);
            Assert.Equal(0, host.Cleaned);
            var target = (await host.Runtime.FindNodesAsync(host.TopLevelId, TreeKinds.Visual, automationId: "Fixtures", maxDepth: 8)).Value!.Matches[0].Node.Target!;
            var direct = await host.Client.InvokeCustomActionAsync(host.Runtime.SessionId,
                new RuntimeCustomActionRequest("bad-resource", target, "fixture.prepare.empty", new Dictionary<string, string> { ["testResource"] = "production" }));
            Assert.False(direct.Value!.Executed);
            Assert.Contains(direct.Value.Diagnostics, diagnostic => diagnostic.Code == RuntimeCustomActionErrorCodes.InvalidParameters);
            Assert.Equal(0, host.Prepared);
            Assert.Throws<ArgumentException>(() => new RuntimeScenarioFixtureOptions("empty", "/production/database", "Fixtures"));
            var changedVersion = await host.Client.InvokeCustomActionAsync(host.Runtime.SessionId,
                new RuntimeCustomActionRequest("old-version", target, "fixture.prepare.empty",
                    new Dictionary<string, string> { ["testResource"] = "test-memory" }, expectedFixtureVersion: "older"));
            Assert.False(changedVersion.Value!.Executed);
            Assert.Contains(changedVersion.Value.Diagnostics, diagnostic => diagnostic.Code == "runtime_fixture_version_changed");
            Assert.Equal(0, host.Prepared);
            Assert.Throws<ArgumentException>(() => new RuntimeScenarioFixtureOptions("empty", "test-memory", "Fixtures", new Dictionary<string, string> { ["testResource"] = "override" }));
        });
    }

    [Fact]
    public async Task ReadinessFailureCancellationAndCleanupFailureRemainStructured()
    {
        await WithHost(async host =>
        {
            host.ReadinessDelay = 3000;
            var timedOut = await new RuntimeScenarioRunner().RunAsync(host.Client, host.Request("empty", timeoutMs: 1500));
            Assert.Equal("failed", timedOut.Value!.Status);
            Assert.Equal("fixture_preparation", timedOut.Value.FailureStage);
            Assert.Equal("cleaned", timedOut.Value.TestFixture!.CleanupStatus);
            Assert.NotEqual("ready", timedOut.Value.TestFixture.ReadinessStatus);
            using var cancelled = new CancellationTokenSource();
            var cancellationTask = new RuntimeScenarioRunner().RunAsync(host.Client, host.Request("empty"), cancelled.Token);
            for (var attempt = 0; host.Prepared < 2 && attempt < 200; attempt++) await Task.Delay(10);
            Assert.Equal(2, host.Prepared);
            cancelled.Cancel();
            var cancellation = await cancellationTask;
            Assert.Equal("cancelled", cancellation.Value!.Status);
            Assert.Equal("cleaned", cancellation.Value.TestFixture!.CleanupStatus);
            await Task.Delay(3100);
            Assert.Equal(0, host.ReadySignals);
            Assert.Equal(2, host.Cleaned);
            host.ReadinessDelay = 25;
            host.FailPreparation = true;
            var preparationFailure = await new RuntimeScenarioRunner().RunAsync(host.Client, host.Request("empty"));
            Assert.Equal("failed", preparationFailure.Value!.TestFixture!.PreparationStatus);
            Assert.Equal("cleaned", preparationFailure.Value.TestFixture.CleanupStatus);
            host.FailPreparation = false;
            host.FailCleanup = true;
            var cleanupFailure = await new RuntimeScenarioRunner().RunAsync(host.Client, host.Request("offline"));
            Assert.Equal("failed", cleanupFailure.Value!.Status);
            Assert.Equal("fixture_cleanup", cleanupFailure.Value.FailureStage);
            Assert.Equal("failed", cleanupFailure.Value.TestFixture!.CleanupStatus);
        });
    }

    [Fact]
    public async Task FixtureRegistrationIsDisabledByDefaultAndRequiresDeclaredTestResources()
    {
        using var session = HeadlessUnitTestSession.StartNew(typeof(BridgeHeadlessSmokeTests.BridgeHeadlessTestApplication));
        await BridgeHeadlessSmokeTests.DispatchAsync(session, async () =>
        {
            var fixture = new RuntimeTestFixtureDescriptor("empty", "1", ["test-memory"], new SemanticWaitCondition(SemanticWaitConditionKinds.ApplicationReady));
            var target = new Border();
            try
            {
                foreach (var options in new[]
                {
                    new BridgeActivationOptions(enableCustomActions: true, allowedCustomActions: [fixture.PrepareAction, fixture.CleanupAction!]),
                    new BridgeActivationOptions(enableCustomActions: true, allowedCustomActions: [fixture.PrepareAction, fixture.CleanupAction!], enableTestFixtures: true, allowedTestResources: ["another-test-resource"]),
                    new BridgeActivationOptions(enableCustomActions: true, allowedCustomActions: [fixture.PrepareAction], enableTestFixtures: true, allowedTestResources: ["test-memory"])
                })
                {
                    AvaScopeBridge.Deactivate();
                    var runtime = AvaScopeBridge.Activate(options);
                    Assert.Throws<InvalidOperationException>(() => runtime.RegisterTestFixture(target, fixture,
                        _ => throw new InvalidOperationException("Must not execute"), _ => throw new InvalidOperationException("Must not execute")));
                }
                await Task.CompletedTask;
            }
            finally { AvaScopeBridge.Deactivate(); }
        }, CancellationToken.None);
    }

    private static async Task WithHost(Func<FixtureHost, Task> test)
    {
        using var session = HeadlessUnitTestSession.StartNew(typeof(BridgeHeadlessSmokeTests.BridgeHeadlessTestApplication));
        var output = Path.Combine(Path.GetTempPath(), "AvaScope.Tests", Guid.NewGuid().ToString("N"));
        try
        {
            await BridgeHeadlessSmokeTests.DispatchAsync(session, async () =>
            {
                AvaScopeBridge.Deactivate();
                var names = new[] { "seeded", "empty", "offline" };
                var runtime = AvaScopeBridge.Activate(new BridgeActivationOptions("Fixture host", enableCustomActions: true,
                    allowedCustomActions: names.SelectMany(name => new[] { "fixture.prepare." + name, "fixture.cleanup." + name }).ToArray(),
                    enableTestFixtures: true, allowedTestResources: ["test-memory"]));
                var target = new Border { Width = 100, Height = 50, Child = new TextBlock { Text = "Fixture target" } };
                AutomationProperties.SetAutomationId(target, "Fixtures");
                var window = new Window { Width = 240, Height = 140, Content = target };
                using var host = new FixtureHost(runtime, output);
                try
                {
                    window.Show();
                    using var top = runtime.RegisterTopLevel(window);
                    host.TopLevelId = Assert.Single(await runtime.ListTopLevelsAsync()).Id;
                    foreach (var name in names)
                        host.Registrations.Add(runtime.RegisterTestFixture(target, new RuntimeTestFixtureDescriptor(name, "1", ["test-memory"],
                            new SemanticWaitCondition(SemanticWaitConditionKinds.ApplicationReady)), context => host.Prepare(name, context), host.Cleanup,
                            name == "seeded" ? [new RuntimeCustomActionParameterDescriptor("seed", RuntimeCustomActionParameterTypes.Integer, required: true)] : []));
                    await test(host);
                }
                finally { window.Close(); AvaScopeBridge.Deactivate(); }
            }, CancellationToken.None);
        }
        finally { if (Directory.Exists(output)) Directory.Delete(output, recursive: true); }
    }

    private sealed class FixtureHost(AvaScopeBridgeRuntime runtime, string output) : IDisposable
    {
        public AvaScopeBridgeRuntime Runtime { get; } = runtime;
        public string TopLevelId { get; set; } = "";
        public string Output { get; } = output;
        public LocalBridgeClient Client { get; } = new(Path.GetDirectoryName(runtime.SessionManifestPath)!);
        public List<IDisposable> Registrations { get; } = [];
        public List<int> Rows { get; } = [];
        public List<int[]> SeededRuns { get; } = [];
        public bool Offline { get; set; }
        public int Prepared { get; private set; }
        public int Cleaned { get; private set; }
        public int ReadySignals { get; private set; }
        public int ReadinessDelay { get; set; } = 25;
        public bool FailPreparation { get; set; }
        public bool FailCleanup { get; set; }
        private IDisposable? _pending;

        public RuntimeScenarioRequest Request(string name, string resource = "test-memory", IReadOnlyDictionary<string, string>? parameters = null,
            int timeoutMs = 2000, RuntimeEvidencePolicy? policy = null) => new(
                [new SemanticWorkflowStep(SemanticWorkflowActions.Inspect, selector: new SemanticWorkflowSelector(automationId: "Fixtures"))],
                sessionId: Runtime.SessionId, topLevelId: TopLevelId, outputDirectory: Path.Combine(Output, Guid.NewGuid().ToString("N")),
                isolateState: false, testFixture: new RuntimeScenarioFixtureOptions(name, resource, "Fixtures", parameters, timeoutMs),
                evidence: new SemanticWorkflowEvidenceOptions(captureOnFailure: false, exportReports: false, policy: policy));

        public CustomActionOutcome Prepare(string name, CustomActionContext context)
        {
            Assert.Equal("test-memory", context.Parameters["testResource"]);
            Prepared++;
            Runtime.SetReadiness("busy", "Preparing host-owned test data");
            Rows.Clear();
            Offline = name == "offline";
            if (name == "seeded")
            {
                var random = new Random(int.Parse(context.Parameters["seed"]));
                Rows.AddRange(Enumerable.Range(0, 5).Select(_ => random.Next(1000)));
                SeededRuns.Add(Rows.ToArray());
            }
            if (FailPreparation) return CustomActionOutcome.Failed("Host preparation failed after changing owned state");
            _pending = DispatcherTimer.RunOnce(() => { ReadySignals++; Runtime.SetReadiness("ready", "Test data ready"); }, TimeSpan.FromMilliseconds(ReadinessDelay));
            return CustomActionOutcome.Succeeded("Fixture accepted");
        }

        public CustomActionOutcome Cleanup(CustomActionContext context)
        {
            Assert.Equal("test-memory", context.Parameters["testResource"]);
            _pending?.Dispose();
            _pending = null;
            Rows.Clear();
            Offline = false;
            Cleaned++;
            return FailCleanup ? CustomActionOutcome.Failed("Host cleanup failed") : CustomActionOutcome.Succeeded("Test state cleaned");
        }

        public void Dispose()
        {
            _pending?.Dispose();
            foreach (var registration in Registrations) registration.Dispose();
        }
    }
}
