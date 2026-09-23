using System.Diagnostics;
using System.Text.Json;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using AvaScope.Bridge;
using AvaScope.Core;
using AvaScope.Protocol;
using ModelContextProtocol.Client;
using static AvaScope.Tests.Core.RuntimeExpressionEvaluatorTests;

namespace AvaScope.Tests.Bridge;

[Collection(BridgeCollectionDefinition.Name)]
public sealed class RuntimeExpressionTests
{
    [Fact]
    public async Task ScalarsSelectionCountsNumericRelationshipsAndCompoundAssertionsShareObservations()
    {
        await WithWindow(async (runtime, window, root, top, client) =>
        {
            root.Children.Add(new ListBoxItem { Name = "Row", IsSelected = true });
            root.Children.Add(new ListBoxItem { Name = "Row", IsSelected = true });
            root.Children.Add(new Slider { Name = "Amount", Value = 1.25 });
            root.Children.Add(new Slider { Name = "Amount", Value = 2.5 });
            root.Children.Add(new TextBlock { Name = "Total", Text = "3.75" });
            root.Children.Add(new TextBlock { Name = "Status", Text = "saved" });
            window.UpdateLayout();
            var sources = new RuntimeExpressionSource[] { new("rows", new(name: "Row"), "selected"), new("amounts", new(name: "Amount"), "value"),
                new("total", new(name: "Total"), "text"), new("status", new(name: "Status"), "text") };
            var scalar = await client.EvaluateRuntimeAsync(new(runtime.SessionId, top, new(new("count_true", "rows"), [sources[0]])));
            Assert.True(scalar.Success); Assert.Equal(2, scalar.Value!.Result.Value!.Value.GetInt32());
            var definition = new RuntimeExpressionDefinition(Op("all",
                Op("eq", new("count_true", "rows"), Literal(2)),
                Op("eq", new("sum", "amounts"), Op("number", new RuntimeExpression("value", "total"))),
                Op("eq", new("value", "status"), Literal("saved"))), sources);
            var asserted = await client.EvaluateRuntimeAsync(new(runtime.SessionId, top, definition, requireTrue: true));
            Assert.True(OperationResultMapper.IsSuccessful(asserted), JsonSerializer.Serialize(asserted));
            Assert.Equal("passed", asserted.Value!.Status);
            Assert.All(asserted.Value.Sources, source => Assert.True(source.Coverage.Complete));
            Assert.Equal(scalar.Value.Result.Value!.Value.GetInt32(), asserted.Value.Result.Operands[0].Operands[0].Value!.Value.GetInt32());
            ((ListBoxItem)root.Children[0]).IsSelected = false;
            var failed = await client.EvaluateRuntimeAsync(new(runtime.SessionId, top, definition, requireTrue: true));
            Assert.False(OperationResultMapper.IsSuccessful(failed)); Assert.Equal("failed", failed.Value!.Status);
            Assert.False(failed.Value.Result.Operands[0].Value!.Value.GetBoolean());
            Assert.Equal("$.operands[0]", failed.Value.Result.Operands[0].Path);
        });
    }

    [Fact]
    public async Task EmptyPartialVirtualizedRedactedAndTruncatedSourcesCannotProduceFalsePasses()
    {
        await WithWindow(async (runtime, window, root, top, client) =>
        {
            var count = new RuntimeExpressionDefinition(new("count", "rows"), [new("rows", new(name: "Row"))]);
            Assert.Equal(0, (await client.EvaluateRuntimeAsync(new(runtime.SessionId, top, count))).Value!.Result.Value!.Value.GetInt32());
            root.Children.Add(new TextBox { Name = "Row", Text = "secret-expression-canary" });
            root.Children.Add(new TextBox { Name = "Row", Text = new string('x', 600) });
            foreach (var limited in new[] { new RuntimeExpressionDefinition(count.Expression, count.Sources, maxResults: 1),
                new RuntimeExpressionDefinition(count.Expression, count.Sources, maxNodes: 1) })
                Assert.Equal("indeterminate", (await client.EvaluateRuntimeAsync(new(runtime.SessionId, top, limited))).Value!.Status);
            var text = new RuntimeExpressionDefinition(new("value", "rows"), [new("rows", new(name: "Row"), "text")]);
            var redacted = await client.EvaluateRuntimeAsync(new(runtime.SessionId, top, text,
                policy: new(Path.GetTempPath(), redactedText: ["secret-expression-canary"])));
            Assert.Equal("indeterminate", redacted.Value!.Status); Assert.DoesNotContain("secret-expression-canary", JsonSerializer.Serialize(redacted));
            Assert.Contains(redacted.Value.Sources[0].Values, value => value.Observation.Status == "redacted");
            Assert.Contains(redacted.Value.Sources[0].Values, value => value.Observation.Status == "truncated");
            AutomationProperties.SetAutomationId(root.Children[0], "hidden-row");
            var excluded = await client.EvaluateRuntimeAsync(new(runtime.SessionId, top, count,
                policy: new(Path.GetTempPath(), redactedAutomationIds: ["hidden-row"])));
            Assert.Equal("indeterminate", excluded.Value!.Status); Assert.False(excluded.Value.Sources[0].Coverage.Complete);
            root.Children.Clear();
            var list = new ListBox { Height = 100, ItemsSource = Enumerable.Range(0, 200).ToArray() };
            root.Children.Add(list); window.UpdateLayout();
            var selected = new RuntimeExpressionDefinition(new("count_true", "rows"), [new("rows", new(nodeType: "ListBoxItem"), "selected")]);
            var partial = await client.EvaluateRuntimeAsync(new(runtime.SessionId, top, selected));
            Assert.Equal("indeterminate", partial.Value!.Status);
            Assert.Contains("unrealized_collection_items", partial.Value.Sources[0].Coverage.Reasons);
            root.Children.Add(new TextBox { Name = "Pinned", Text = "observed" });
            var pinned = Assert.Single((await client.QueryNodesAsync(new(runtime.SessionId, top, new(name: "Pinned")))).Value!.Matches).Target!;
            var exact = new RuntimeExpressionDefinition(new("value", "pinned"), [new("pinned", new(nodeId: pinned.NodeId), "text")]);
            Assert.Equal("observed", (await client.EvaluateRuntimeAsync(new(runtime.SessionId, top, exact))).Value!.Result.Value!.Value.GetString());
            root.Children.Clear(); root.Children.Add(new DerivedGrid());
            var grid = await client.EvaluateRuntimeAsync(new(runtime.SessionId, top, count));
            Assert.Equal("indeterminate", grid.Value!.Status);
            Assert.Contains("table_requires_structured_query", grid.Value.Sources[0].Coverage.Reasons);
        });
    }

    [Fact]
    public async Task ChangingPublicProviderGenerationIsIndeterminateAndNeverAutomaticallyRetriedToPass()
    {
        await WithWindow(async (runtime, _, root, top, client) =>
        {
            var label = new TextBlock { Text = "Label" };
            var changing = new ChangingLabel { Name = "Changing", Label = label };
            root.Children.Add(label); root.Children.Add(changing);
            var definition = new RuntimeExpressionDefinition(new("count", "control"),
                [new("control", new(name: "Changing", relationships: [new("labeled_by", new(text: "Label"))]))]);
            var actual = await client.EvaluateRuntimeAsync(new(runtime.SessionId, top, definition));
            Assert.Equal("indeterminate", actual.Value!.Status);
            Assert.Contains("generation_changed", actual.Value.Sources[0].Coverage.Reasons);
        });
    }

    [Fact]
    public async Task WorkflowWaitsReuseExpressionsResolveVariablesAndRetainTimeoutOperands()
    {
        await WithWindow(async (runtime, _, root, top, client) =>
        {
            var status = new TextBlock { Name = "Status", Text = "busy" }; root.Children.Add(status);
            var definition = new RuntimeExpressionDefinition(Op("eq", new("value", "status"), Literal("${expected}")),
                [new("status", new(name: "${field}"), "text")]);
            var runner = new SemanticWorkflowRunner();
            var request = new SemanticWorkflowRequest(runtime.SessionId, top,
                [new(SemanticWorkflowActions.WaitForState, waitCondition: new("expression", expression: definition), timeoutMs: 3000, pollIntervalMs: 25)],
                variables: new Dictionary<string, string> { ["expected"] = "saved", ["field"] = "Status" });
            var compiled = await runner.RunAsync(client, new(runtime.SessionId, top, request.Steps, variables: request.Variables, validateOnly: true));
            Assert.Equal("validated", compiled.Value!.Status);
            var waiting = runner.RunAsync(client, request);
            await Task.Delay(100); status.Text = "saved";
            var completed = await waiting;
            Assert.True(OperationResultMapper.IsSuccessful(completed), JsonSerializer.Serialize(completed));
            Assert.Equal("passed", completed.Value!.Steps[0].WaitObservation!.Expression!.Status);
            status.Text = "busy";
            var timeout = await runner.RunAsync(client, new(runtime.SessionId, top,
                [new(SemanticWorkflowActions.WaitForState, waitCondition: new("expression", expression: definition), timeoutMs: 500, pollIntervalMs: 25)],
                variables: request.Variables));
            Assert.False(OperationResultMapper.IsSuccessful(timeout));
            Assert.Equal("failed", timeout.Value!.Steps[0].WaitObservation!.Expression!.Status);
            Assert.Contains("false", timeout.Value.Steps[0].Metadata!["expressionFindings"]);
        });
    }

    [Fact]
    public async Task RealCliAndMcpReturnMatchingTypedScalarsAndFailedAssertionEvidence()
    {
        await WithWindow(async (runtime, _, root, top, client) =>
        {
            root.Children.Add(new Slider { Name = "Amount", Value = 2.5 });
            var source = new RuntimeExpressionSource("amount", new(name: "Amount"), "value");
            var request = new RuntimeExpressionRequest(runtime.SessionId, top, new(new("sum", "amount"), [source]));
            var path = Path.Combine(Path.GetTempPath(), "expression-" + Guid.NewGuid().ToString("N") + ".json");
            try
            {
                await File.WriteAllTextAsync(path, JsonSerializer.Serialize(request));
                var start = new ProcessStartInfo("dotnet") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
                foreach (var arg in new[] { Path.Combine(AppContext.BaseDirectory, "avascope.dll"), "evaluate-runtime", "--request", path, "--manifest-dir", client.ManifestDirectory }) start.ArgumentList.Add(arg);
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(40));
                using var process = Process.Start(start)!;
                var output = process.StandardOutput.ReadToEndAsync(); var errors = process.StandardError.ReadToEndAsync();
                try { await process.WaitForExitAsync(timeout.Token); }
                finally { if (!process.HasExited) process.Kill(entireProcessTree: true); }
                Assert.Equal(0, process.ExitCode);
                var cli = JsonSerializer.Deserialize<ToolResult<RuntimeExpressionResponse>>(await output)!;
                Assert.True(cli.Success, cli.Error?.Message + await errors);
                var environment = TestEnvironment.McpEnvironment();
                if (Environment.GetEnvironmentVariable("TMPDIR") is { } temporary) environment["TMPDIR"] = temporary;
                await using var mcp = await McpClient.CreateAsync(new StdioClientTransport(new StdioClientTransportOptions
                { Name = "Expressions", Command = "dotnet", Arguments = [Path.Combine(AppContext.BaseDirectory, "AvaScope.Mcp.dll")],
                    InheritEnvironmentVariables = false, EnvironmentVariables = environment, ShutdownTimeout = TimeSpan.FromSeconds(3) }), cancellationToken: timeout.Token);
                var result = await Call(request);
                Assert.True(result.Success, JsonSerializer.Serialize(result));
                Assert.Equal(cli.Value!.Result.Value!.Value.GetDecimal(), result.Value!.Result.Value!.Value.GetDecimal());
                var failed = await Call(new(runtime.SessionId, top, new(Op("eq", new("sum", "amount"), Literal(9)), [source]), requireTrue: true));
                Assert.False(failed.Success); Assert.Equal("failed", failed.Value!.Status); Assert.Equal(2.5m, failed.Value.Result.Operands[0].Value!.Value.GetDecimal());
                var wait = new SemanticWorkflowRequest(runtime.SessionId, top,
                    [new(SemanticWorkflowActions.WaitForState, waitCondition: new("expression",
                        expression: new(Op("eq", new("sum", "amount"), Literal(9)), [source])), timeoutMs: 700, pollIntervalMs: 25)]);
                var waitCall = await mcp.CallToolAsync("run_workflow", new Dictionary<string, object?>
                { ["request"] = JsonSerializer.SerializeToElement(wait), ["manifestDirectory"] = client.ManifestDirectory }, cancellationToken: timeout.Token);
                var mcpWait = JsonSerializer.Deserialize<ToolResult<SemanticWorkflowResponse>>(JsonSerializer.Serialize(waitCall.StructuredContent))!;
                Assert.False(mcpWait.Success); Assert.Equal("failed", mcpWait.Value!.Steps[0].WaitObservation!.Expression!.Status);
                await File.WriteAllTextAsync(path, JsonSerializer.Serialize(wait));
                start.ArgumentList[1] = "run-workflow";
                using var cliWaitProcess = Process.Start(start)!;
                var waitOutput = cliWaitProcess.StandardOutput.ReadToEndAsync(); var waitErrors = cliWaitProcess.StandardError.ReadToEndAsync();
                try { await cliWaitProcess.WaitForExitAsync(timeout.Token); }
                finally { if (!cliWaitProcess.HasExited) cliWaitProcess.Kill(entireProcessTree: true); }
                Assert.Equal(1, cliWaitProcess.ExitCode);
                var cliWait = JsonSerializer.Deserialize<ToolResult<SemanticWorkflowResponse>>(await waitOutput)!;
                Assert.False(cliWait.Success, await waitErrors);
                Assert.Equal(mcpWait.Value.Steps[0].WaitObservation!.Expression!.Result.Value!.Value.GetBoolean(),
                    cliWait.Value!.Steps[0].WaitObservation!.Expression!.Result.Value!.Value.GetBoolean());
                async Task<ToolResult<RuntimeExpressionResponse>> Call(RuntimeExpressionRequest input)
                {
                    var call = await mcp.CallToolAsync("evaluate_runtime", new Dictionary<string, object?>
                    { ["request"] = JsonSerializer.SerializeToElement(input), ["manifestDirectory"] = client.ManifestDirectory }, cancellationToken: timeout.Token);
                    return JsonSerializer.Deserialize<ToolResult<RuntimeExpressionResponse>>(JsonSerializer.Serialize(call.StructuredContent))!;
                }
            }
            finally { File.Delete(path); }
        });
    }

    private static async Task WithWindow(Func<AvaScopeBridgeRuntime, Window, StackPanel, string, LocalBridgeClient, Task> test)
    {
        var session = HeadlessUnitTestSession.StartNew(typeof(BridgeHeadlessSmokeTests.BridgeHeadlessTestApplication));
        try
        {
            await BridgeHeadlessSmokeTests.DispatchAsync(session, async () =>
            {
                AvaScopeBridge.Deactivate(); var runtime = AvaScopeBridge.Activate();
                var root = new StackPanel(); var window = new Window { Width = 500, Height = 700, Content = root };
                try
                {
                    window.Show(); using var registration = runtime.RegisterTopLevel(window);
                    var top = Assert.Single(await runtime.ListTopLevelsAsync());
                    Assert.True((await runtime.ReadinessAsync(top.Id, options: new(waitForFrame: true))).Success);
                    await test(runtime, window, root, top.Id, new(Path.GetDirectoryName(runtime.SessionManifestPath)!));
                }
                finally { window.Close(); AvaScopeBridge.Deactivate(); }
            }, CancellationToken.None);
        }
        finally { BridgeHeadlessSmokeTests.DisposeHeadlessSessionAfterExplicitCleanup(session); }
    }

    private sealed class DerivedGrid : DataGrid;

    private sealed class ChangingLabel : Control
    {
        public Control? Label { get; set; }
        protected override AutomationPeer OnCreateAutomationPeer() => new Peer(this);
        private sealed class Peer(ChangingLabel owner) : ControlAutomationPeer(owner)
        {
            protected override AutomationPeer? GetLabeledByCore()
            {
                owner.DataContext = new object();
                return owner.Label is null ? null : CreatePeerForElement(owner.Label);
            }
        }
    }
}
