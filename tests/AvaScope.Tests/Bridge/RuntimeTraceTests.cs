using System.Diagnostics;
using System.Text.Json;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using AvaScope.Bridge;
using AvaScope.Core;
using AvaScope.Protocol;
using ModelContextProtocol.Client;

namespace AvaScope.Tests.Bridge;

[Collection(BridgeCollectionDefinition.Name)]
public sealed class RuntimeTraceTests
{
    [Fact]
    public async Task InterleavedActionsKeepExplicitOperationCorrelationAndUnrelatedFailuresSeparate()
    {
        await WithWindow(async (runtime, top, button, _, client, root) =>
        {
            var handles = new List<RuntimeOperationHandle>();
            using var events = runtime.RegisterDiagnosticSource("domain", "app_event", top);
            using var logs = runtime.RegisterDiagnosticSource("logs", "app_log", top);
            using var registration = runtime.RegisterCustomAction(button, new("import", context =>
            {
                var operation = context.BeginOperation(); handles.Add(operation);
                events.Report("info", "Import accepted", context.RequestId, operation.OperationId);
                return CustomActionOutcome.Succeeded("Accepted");
            }, supportsOperations: true));
            var policy = new RuntimeEvidencePolicy(root);
            var started = await client.TraceAsync(new(runtime.SessionId, "start", topLevelId: top, policy: policy, durationMs: 60000));
            Assert.True(started.Success, started.Error?.Message);
            Assert.Equal("unavailable", started.Value!.Sources.Single(source => source.Kind == "binding").Availability);
            var node = Assert.Single((await client.FindNodesAsync(runtime.SessionId, top, TreeKinds.Visual, name: "Action")).Value!.Matches).Node.NodeId;
            var target = new RuntimeTargetContext(runtime.SessionId, top, TreeKinds.Visual, node);
            await client.InvokeCustomActionAsync(runtime.SessionId, new("action-a", target, "import"));
            await client.InvokeCustomActionAsync(runtime.SessionId, new("action-b", target, "import"));
            handles[0].ReportProgress(.5, "Halfway through A"); handles[1].Fail("other_job", "B failed");
            logs.Report("error", "Unrelated timer error"); handles[0].Complete();
            var input = await new SemanticWorkflowRunner().RunAsync(client, new(runtime.SessionId, top,
                [new(SemanticWorkflowActions.Invoke, selector: new(name: "Action"))]));
            Assert.True(OperationResultMapper.IsSuccessful(input), JsonSerializer.Serialize(input));
            var correlation = input.Value!.Steps[0].Input!.CorrelationId;
            Assert.False(string.IsNullOrWhiteSpace(correlation));
            var read = (await client.TraceAsync(new(runtime.SessionId, "read", started.Value.TraceId, requestId: "action-a", maxEvents: 128))).Value!;
            Assert.Contains(read.Events, item => item.Kind == "operation" && item.Message.StartsWith("completed", StringComparison.Ordinal) && item.Relation == "explicitly_correlated"
                && item.Correlation == "operation_request" && item.OperationId == handles[0].OperationId);
            Assert.Contains(read.Events, item => item.RequestId == "action-b" && item.Relation == "different_correlation" && item.Level == "error");
            Assert.Contains(read.Events, item => item.Message == "Unrelated timer error" && item.Correlation == "uncorrelated" && item.Relation == "temporal_only");
            Assert.Contains(read.Events, item => item.RequestId == correlation && item.Correlation == "bridge_request");
            Assert.Equal(read.Events.Count, read.Events.Select(item => item.Sequence).Distinct().Count());
            Assert.Contains("neither correlation nor temporal proximity", read.CorrelationNote);
            var byOperation = (await client.TraceAsync(new(runtime.SessionId, "read", started.Value.TraceId, operationId: handles[0].OperationId, maxEvents: 128))).Value!;
            Assert.Contains(byOperation.Events, item => item.Correlation == "app_declared" && item.Relation == "explicitly_correlated");
        });
    }

    [Fact]
    public async Task EverySourceIsSanitizedBeforeRetentionAndExcludedAncestorsDoNotLeakBetweenTraces()
    {
        await WithWindow(async (runtime, top, button, panel, client, root) =>
        {
            var secret = new TextBox { Name = "Secret" }; AutomationProperties.SetAutomationId(secret, "child-control");
            var group = new StackPanel { Children = { secret } }; AutomationProperties.SetAutomationId(group, "private-group");
            panel.Children.Add(group); DataValidationErrors.SetErrors(secret, [new Exception("ancestor-secret-canary")]);
            DataValidationErrors.SetErrors(button, ["redacted-event-canary"]);
            var strict = new RuntimeEvidencePolicy(root, redactedText: ["redacted-event-canary"], excludedControlAutomationIds: ["private-group", "excluded-log"]);
            var selected = await client.TraceAsync(new(runtime.SessionId, "start", topLevelId: top, policy: strict, sampleValidation: true, durationMs: 60000));
            Assert.True(selected.Success, selected.Error?.Message);
            var broad = await client.TraceAsync(new(runtime.SessionId, "start", topLevelId: top, policy: new(root), sampleValidation: true, durationMs: 60000));
            Assert.True(broad.Success, broad.Error?.Message);
            using var privateAction = runtime.RegisterCustomAction(secret, new("import", context =>
            { var work = context.BeginOperation(); work.ReportProgress(.5, "operation-exclusion-canary"); work.Complete(); return CustomActionOutcome.Succeeded("Accepted"); }, supportsOperations: true));
            var secretNode = Assert.Single((await client.FindNodesAsync(runtime.SessionId, top, TreeKinds.Visual, name: "Secret")).Value!.Matches).Node.NodeId;
            await client.InvokeCustomActionAsync(runtime.SessionId, new("private-operation", new(runtime.SessionId, top, TreeKinds.Visual, secretNode), "import"));
            using var logs = runtime.RegisterDiagnosticSource("app-log", "app_log", top);
            Assert.True(logs.Report("error", "redacted-event-canary"));
            logs.Report("error", "excluded-source-canary", automationId: "excluded-log");
            var read = (await client.TraceAsync(new(runtime.SessionId, "read", selected.Value!.TraceId, maxEvents: 128))).Value!;
            var json = JsonSerializer.Serialize(read);
            Assert.DoesNotContain("ancestor-secret-canary", json);
            Assert.DoesNotContain("excluded-source-canary", json);
            Assert.DoesNotContain("redacted-event-canary", json);
            Assert.DoesNotContain("operation-exclusion-canary", json);
            Assert.Contains(read.Events, item => item.Source == "avalonia_validation_sample" && item.Correlation == "uncorrelated");
            Assert.True(read.SuppressedEvents > 0);
            // Reading without a policy must not recover values hidden before retention.
            Assert.Contains("ancestor-secret-canary", JsonSerializer.Serialize((await client.TraceAsync(new(runtime.SessionId, "read", broad.Value!.TraceId, maxEvents: 128))).Value));
            using var binding = runtime.RegisterDiagnosticSource("bindings", "binding", top);
            binding.Report("warning", "redacted-event-canary", requestId: "declared-request");
            DataValidationErrors.SetErrors(button, [new ThrowingDiagnostic()]);
            var partial = (await client.TraceAsync(new(runtime.SessionId, "read", selected.Value.TraceId))).Value!;
            Assert.Equal("partial", partial.Sources.Single(source => source.Kind == "validation").Availability);
            Assert.Equal("adapter_registered", partial.Sources.Single(source => source.Kind == "binding").Availability);
            Assert.Contains(partial.Events, item => item.Kind == "binding" && item.Correlation == "app_declared");
            Assert.DoesNotContain("redacted-event-canary", JsonSerializer.Serialize(partial));
        });
    }

    [Fact]
    public async Task TraceLimitsExpirySourceDisposalAndShutdownRemainBounded()
    {
        await WithWindow(async (runtime, top, _, _, client, root) =>
        {
            Assert.Throws<ArgumentException>(() => new RuntimeTraceRequest(runtime.SessionId, "start", topLevelId: top));
            using var log = runtime.RegisterDiagnosticSource("volume", "app_log", top);
            Assert.False(log.Report("info", "Before explicit trace start"));
            var trace = (await client.TraceAsync(new(runtime.SessionId, "start", topLevelId: top, policy: new(root), durationMs: 60000))).Value!;
            for (var index = 0; index < 500; index++) Assert.True(log.Report("info", index + new string('x', 800)));
            Assert.False(log.Report("info", new string('s', 4097)));
            var read = (await client.TraceAsync(new(runtime.SessionId, "read", trace.TraceId, maxEvents: 128))).Value!;
            Assert.InRange(read.RetainedEvents, 1, 128); Assert.True(read.DroppedEvents > 0); Assert.True(read.SuppressedEvents > 0); Assert.True(read.Truncated);
            Assert.True(JsonSerializer.SerializeToUtf8Bytes(read.Events).Length < 98304);
            Assert.DoesNotContain(read.Events, item => item.Message.StartsWith("0xxx", StringComparison.Ordinal));
            var stopped = await client.TraceAsync(new(runtime.SessionId, "stop", trace.TraceId)); Assert.Equal("stopped", stopped.Value!.Status);
            Assert.False(log.Report("info", "After stop"));
            var expiring = (await client.TraceAsync(new(runtime.SessionId, "start", topLevelId: top, policy: new(root), durationMs: 50))).Value!;
            await Task.Delay(80);
            Assert.False(log.Report("info", "After collection deadline"));
            Assert.Equal("stopped", (await client.TraceAsync(new(runtime.SessionId, "read", expiring.TraceId))).Value!.Status);
            var active = new List<string>();
            for (var index = 0; index < 4; index++) active.Add((await client.TraceAsync(new(runtime.SessionId, "start", topLevelId: top, policy: new(root), durationMs: 60000))).Value!.TraceId);
            Assert.Equal("trace_limit", (await client.TraceAsync(new(runtime.SessionId, "start", topLevelId: top, policy: new(root)))).Error!.Code);
            log.Dispose(); Assert.False(log.Report("error", "Disposed adapter"));
            using var renewed = runtime.RegisterDiagnosticSource("volume", "app_log", top);
            Assert.True(renewed.Report("info", "New adapter generation"));
            AvaScopeBridge.Deactivate(); Assert.False(renewed.Report("info", "Closed bridge"));
            Assert.Equal("trace_session_closed", (await runtime.TraceAsync(new(runtime.SessionId, "read", active[0]))).Error!.Code);
            var replacement = AvaScopeBridge.Activate();
            Assert.Equal("trace_unknown", (await replacement.TraceAsync(new(replacement.SessionId, "read", active[0]))).Error!.Code);
        });
    }

    [Fact]
    public async Task CliAndMcpExportOnlySanitizedArtifactsInsideOwnedEvidenceDirectories()
    {
        await WithWindow(async (runtime, top, _, _, client, root) =>
        {
            var policy = new RuntimeEvidencePolicy(root, redactedText: ["trace-export-canary"]);
            using var events = runtime.RegisterDiagnosticSource("events", "app_event", top);
            var trace = (await client.TraceAsync(new(runtime.SessionId, "start", topLevelId: top, policy: policy, durationMs: 60000))).Value!;
            events.Report("info", "trace-export-canary", requestId: "app-request");
            var path = Path.Combine(root, "request.json");
            var request = new RuntimeTraceRequest(runtime.SessionId, "read", trace.TraceId, policy: policy, requestId: "app-request", outputDirectory: Path.Combine(root, "export"));
            await File.WriteAllTextAsync(path, JsonSerializer.Serialize(request));
            var start = new ProcessStartInfo("dotnet") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
            foreach (var arg in new[] { Path.Combine(AppContext.BaseDirectory, "avascope.dll"), "trace", "--request", path, "--manifest-dir", client.ManifestDirectory }) start.ArgumentList.Add(arg);
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(40));
            using var process = Process.Start(start)!;
            var output = process.StandardOutput.ReadToEndAsync(); var error = process.StandardError.ReadToEndAsync();
            try { await process.WaitForExitAsync(timeout.Token); }
            finally { if (!process.HasExited) process.Kill(entireProcessTree: true); }
            var cli = JsonSerializer.Deserialize<ToolResult<RuntimeTraceResponse>>(await output)!;
            Assert.True(cli.Success, cli.Error?.Message + await error); Assert.Equal(0, process.ExitCode);
            Assert.True(File.Exists(cli.Value!.ArtifactPath));
            Assert.DoesNotContain("trace-export-canary", await File.ReadAllTextAsync(cli.Value.ArtifactPath!));
            var artifact = JsonSerializer.Deserialize<RuntimeTraceResponse>(await File.ReadAllTextAsync(cli.Value.ArtifactPath!))!;
            Assert.Equal(cli.Value.Events, artifact.Events);
            var environment = TestEnvironment.McpEnvironment();
            if (Environment.GetEnvironmentVariable("TMPDIR") is { } temporary) environment["TMPDIR"] = temporary;
            await using var mcp = await McpClient.CreateAsync(new StdioClientTransport(new StdioClientTransportOptions
            { Name = "Runtime traces", Command = "dotnet", Arguments = [Path.Combine(AppContext.BaseDirectory, "AvaScope.Mcp.dll")],
                InheritEnvironmentVariables = false, EnvironmentVariables = environment, ShutdownTimeout = TimeSpan.FromSeconds(3) }), cancellationToken: timeout.Token);
            var call = await mcp.CallToolAsync("trace", new Dictionary<string, object?>
            { ["request"] = JsonSerializer.SerializeToElement(request), ["manifestDirectory"] = client.ManifestDirectory }, cancellationToken: timeout.Token);
            var response = JsonSerializer.Deserialize<ToolResult<RuntimeTraceResponse>>(JsonSerializer.Serialize(call.StructuredContent))!;
            Assert.True(response.Success, response.Error?.Message); Assert.Equal(cli.Value.Events, response.Value!.Events);
            Assert.DoesNotContain("trace-export-canary", JsonSerializer.Serialize(response));
            var outside = await client.TraceAsync(new(runtime.SessionId, "read", trace.TraceId, policy: policy, outputDirectory: Path.GetDirectoryName(root)));
            Assert.False(outside.Success);
        });
    }

    private sealed class ThrowingDiagnostic
    { public override string ToString() => throw new InvalidOperationException("Diagnostic adapter failure"); }

    private static async Task WithWindow(Func<AvaScopeBridgeRuntime, string, Button, StackPanel, LocalBridgeClient, string, Task> test)
    {
        var session = HeadlessUnitTestSession.StartNew(typeof(BridgeHeadlessSmokeTests.BridgeHeadlessTestApplication));
        var root = Path.Combine(Path.GetTempPath(), "AvaScope.Tests", "trace-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
        try
        {
            await BridgeHeadlessSmokeTests.DispatchAsync(session, async () =>
            {
                AvaScopeBridge.Deactivate(); var runtime = AvaScopeBridge.Activate(new(enableCustomActions: true, allowedCustomActions: ["import"]));
                var button = new Button { Name = "Action", Content = "Action" };
                var panel = new StackPanel { Children = { button } };
                var window = new Window { Width = 400, Height = 250, Content = panel };
                try
                {
                    window.Show(); using var registration = runtime.RegisterTopLevel(window);
                    var top = Assert.Single(await runtime.ListTopLevelsAsync());
                    await test(runtime, top.Id, button, panel, new LocalBridgeClient(Path.GetDirectoryName(runtime.SessionManifestPath)!), root);
                }
                finally { window.Close(); AvaScopeBridge.Deactivate(); }
            }, CancellationToken.None);
        }
        finally
        {
            BridgeHeadlessSmokeTests.DisposeHeadlessSessionAfterExplicitCleanup(session);
            Directory.Delete(root, recursive: true);
        }
    }
}
