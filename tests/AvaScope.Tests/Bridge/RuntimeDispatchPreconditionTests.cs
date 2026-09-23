using System.Diagnostics;
using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Headless;
using AvaScope.Bridge;
using AvaScope.Core;
using AvaScope.Protocol;
using ModelContextProtocol.Client;
using static AvaScope.Tests.Core.RuntimeExpressionEvaluatorTests;

namespace AvaScope.Tests.Bridge;

[Collection(BridgeCollectionDefinition.Name)]
public sealed class RuntimeDispatchPreconditionTests
{
    [Fact]
    public async Task ChangedDocumentAndDryRunRejectBeforeDispatchAndAcceptedResultsPreserveCheckedValues()
    {
        await WithWindow(async (runtime, _, _, document, button, top, target, client) =>
        {
            var calls = 0; button.Click += (_, _) => calls++;
            var options = Options("document-a");
            document.Text = "document-b";
            var rejected = await client.InputAsync(runtime.SessionId, top, InputActions.Invoke, targetNodeId: target.NodeId, inputTarget: target, execution: options);
            Assert.False(rejected.Success); Assert.Equal(0, calls);
            Assert.Equal("input_precondition_rejected", rejected.Error!.Code);
            Assert.Equal("false", rejected.Error.Details!["dispatched"]);
            var observed = JsonSerializer.Deserialize<RuntimeExpressionResponse>(rejected.Error.Details["preconditions"])!;
            Assert.Equal("document-b", observed.Result.Operands[0].Value!.Value.GetString());
            Assert.Equal("failed", observed.Status);
            Assert.False((await runtime.ValidateInputAsync(top, InputActions.Invoke, targetNodeId: target.NodeId, inputTarget: target, execution: options)).Success);
            document.Text = "document-a";
            var validation = await client.ValidateInputAsync(runtime.SessionId, top, InputActions.Invoke, targetNodeId: target.NodeId, inputTarget: target, execution: options);
            Assert.True(validation.Success, validation.Error?.Message); Assert.Equal("false", validation.Value!.Metadata["dispatched"]);
            Assert.Equal("passed", validation.Value.Preconditions!.Status); Assert.Equal(0, calls);
            var executed = await client.InputAsync(runtime.SessionId, top, InputActions.Invoke, targetNodeId: target.NodeId, inputTarget: target, execution: options);
            Assert.True(executed.Success, executed.Error?.Message); Assert.Equal(1, calls);
            Assert.Equal("pre_dispatch", executed.Value!.Metadata["preconditionPhase"]);
            Assert.Equal("true", executed.Value.Metadata["dispatched"]);
            Assert.Equal("document-a", executed.Value.Preconditions!.Result.Operands[0].Value!.Value.GetString());
        });
    }

    [Fact]
    public async Task FocusPreparationChangesAreRecheckedAndReplacedTargetsCannotDispatch()
    {
        await WithWindow(async (runtime, _, root, document, button, top, target, client) =>
        {
            var clicks = 0; button.Click += (_, _) => clicks++;
            Assert.True(document.Focus());
            button.GotFocus += (_, _) => document.Text = "different-document";
            var changed = await client.InputAsync(runtime.SessionId, top, InputActions.Click, targetNodeId: target.NodeId, inputTarget: target, execution: Options("document-a"));
            Assert.False(changed.Success); Assert.Equal(0, clicks);
            Assert.Equal("pre_dispatch", changed.Error!.Details!["preconditionPhase"]);
            Assert.Equal("false", changed.Error.Details["dispatched"]);
            Assert.Contains("different-document", changed.Error.Details["preconditions"]);
            root.Children.Remove(button); root.Children.Add(new Button { Name = "Action", Content = "Replacement" });
            var stale = await client.InputAsync(runtime.SessionId, top, InputActions.Invoke, targetNodeId: target.NodeId, inputTarget: target, execution: Options("different-document"));
            Assert.False(stale.Success); Assert.Equal("false", stale.Error!.Details!["dispatched"]);
            Assert.Equal(0, clicks);
        });
    }

    [Fact]
    public async Task PostDispatchExceptionsRemainUncertainAndSyntheticInputChecksAfterFocusPreparation()
    {
        await WithWindow(async (runtime, _, _, document, button, top, target, client) =>
        {
            var count = 0;
            button.Click += (_, _) => { count++; document.Text = "committed"; throw new InvalidOperationException("application callback failed after commit"); };
            var failed = await client.InputAsync(runtime.SessionId, top, InputActions.Invoke, targetNodeId: target.NodeId, inputTarget: target, execution: Options("document-a"));
            Assert.False(failed.Success); Assert.Equal(1, count); Assert.Equal("committed", document.Text);
            Assert.Equal("true", failed.Error!.Details!["dispatched"]);
            Assert.Equal("unknown_after_dispatch", failed.Error.Details["dispatchOutcome"]);
            document.Text = "document-a";
            var editor = Assert.Single((await client.QueryNodesAsync(new(runtime.SessionId, top, new(name: "Document"), maxDepth: 32))).Value!.Matches).Target!;
            var blocked = await client.InputAsync(runtime.SessionId, top, InputActions.KeyText, inputText: "never", targetNodeId: editor.NodeId, inputTarget: editor,
                execution: Options("different-document") with { Strategy = "synthetic" });
            Assert.False(blocked.Success); Assert.Equal("document-a", document.Text); Assert.Equal("false", blocked.Error!.Details!["dispatched"]);
            document.SelectionStart = 0; document.SelectionEnd = document.Text!.Length;
            var accepted = await client.InputAsync(runtime.SessionId, top, InputActions.KeyText, inputText: "document-b", targetNodeId: editor.NodeId,
                inputTarget: editor, execution: Options("document-a"));
            Assert.True(accepted.Success, accepted.Error?.Message); Assert.Equal("document-b", document.Text);
            Assert.Equal("passed", accepted.Value!.Preconditions!.Status); Assert.Equal("true", accepted.Value.Metadata["dispatched"]);
        });
    }

    [Fact]
    public async Task WorkflowReplayPreservesOriginalGuardAndAsyncCompletionRequiresSeparateVerification()
    {
        await WithWindow(async (runtime, _, _, document, button, top, _, client) =>
        {
            var count = 0;
            var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            button.Click += async (_, _) => { count++; document.Text = "processing"; await Task.Delay(40); document.Text = "saved"; completion.TrySetResult(); };
            var options = Options("${document}");
            var step = new SemanticWorkflowStep(SemanticWorkflowActions.Invoke, id: "guarded-apply", selector: new(name: "Action"),
                idempotencyKey: "guarded-once", inputExecution: options,
                verify: new(new("expression", expression: Condition("saved")), timeoutMs: 3000, pollIntervalMs: 25));
            var workflow = new SemanticWorkflowRequest(runtime.SessionId, top, [step], variables: new Dictionary<string, string> { ["document"] = "document-a" });
            var runner = new SemanticWorkflowRunner();
            var first = await runner.RunAsync(client, workflow);
            Assert.True(OperationResultMapper.IsSuccessful(first), JsonSerializer.Serialize(first));
            await completion.Task.WaitAsync(TimeSpan.FromSeconds(3)); Assert.Equal(1, count);
            var replay = await runner.RunAsync(client, workflow);
            Assert.True(OperationResultMapper.IsSuccessful(replay), JsonSerializer.Serialize(replay)); Assert.Equal(1, count);
            Assert.Equal("true", replay.Value!.Steps[0].Metadata["idempotencyReplay"]);
            var newAttempt = await client.InputAsync(runtime.SessionId, top, InputActions.Invoke,
                targetNodeId: (await client.QueryNodesAsync(new(runtime.SessionId, top, new(name: "Action")))).Value!.Matches.Single().Target!.NodeId,
                execution: Options("document-a"));
            Assert.False(newAttempt.Success); Assert.Equal(1, count);
        });
    }

    [Fact]
    public async Task RealCliAndMcpEnforcePolicyAndReturnGuardEvidenceWithoutLeakingProtectedValues()
    {
        await WithWindow(async (runtime, _, _, document, button, top, target, client) =>
        {
            var calls = 0; button.Click += (_, _) => calls++;
            var path = Path.Combine(Path.GetTempPath(), "guard-" + Guid.NewGuid().ToString("N") + ".json");
            try
            {
                var options = Options("document-a");
                await File.WriteAllTextAsync(path, JsonSerializer.Serialize(options));
                var start = new ProcessStartInfo("dotnet") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
                foreach (var arg in new[] { Path.Combine(AppContext.BaseDirectory, "avascope.dll"), "input", "--session", runtime.SessionId.Value,
                    "--top-level", top, "--action", "invoke", "--target-node", target.NodeId!, "--execution", path, "--manifest-dir", client.ManifestDirectory }) start.ArgumentList.Add(arg);
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(40));
                using var process = Process.Start(start)!;
                var output = process.StandardOutput.ReadToEndAsync(); var error = process.StandardError.ReadToEndAsync();
                try { await process.WaitForExitAsync(timeout.Token); }
                finally { if (!process.HasExited) process.Kill(entireProcessTree: true); }
                var cli = JsonSerializer.Deserialize<ToolResult<InputResponse>>(await output)!;
                Assert.True(cli.Success, cli.Error?.Message + await error); Assert.Equal(0, process.ExitCode);
                Assert.Equal("passed", cli.Value!.Preconditions!.Status); Assert.Equal(1, calls);
                document.Text = "private-guard-canary";
                var policy = new RuntimeEvidencePolicy(Path.GetTempPath(), redactedText: ["private-guard-canary"],
                    allowedActions: [SemanticWorkflowActions.Inspect, SemanticWorkflowActions.Invoke]);
                var environment = TestEnvironment.McpEnvironment();
                if (Environment.GetEnvironmentVariable("TMPDIR") is { } temporary) environment["TMPDIR"] = temporary;
                await using var mcp = await McpClient.CreateAsync(new StdioClientTransport(new StdioClientTransportOptions
                { Name = "Guarded input", Command = "dotnet", Arguments = [Path.Combine(AppContext.BaseDirectory, "AvaScope.Mcp.dll")],
                    InheritEnvironmentVariables = false, EnvironmentVariables = environment, ShutdownTimeout = TimeSpan.FromSeconds(3) }), cancellationToken: timeout.Token);
                var call = await mcp.CallToolAsync("input", new Dictionary<string, object?>
                { ["sessionId"] = runtime.SessionId.Value, ["topLevelId"] = top, ["action"] = "invoke", ["targetNodeId"] = target.NodeId,
                    ["execution"] = JsonSerializer.SerializeToElement(options with { PreconditionPolicy = policy }), ["manifestDirectory"] = client.ManifestDirectory }, cancellationToken: timeout.Token);
                var result = JsonSerializer.Deserialize<ToolResult<InputResponse>>(JsonSerializer.Serialize(call.StructuredContent))!;
                Assert.False(result.Success); Assert.Equal(1, calls);
                Assert.Equal("false", result.Error!.Details!["dispatched"]);
                Assert.DoesNotContain("private-guard-canary", JsonSerializer.Serialize(result));
                Assert.Equal("indeterminate", JsonSerializer.Deserialize<RuntimeExpressionResponse>(result.Error.Details["preconditions"])!.Status);
            }
            finally { File.Delete(path); }
        });
    }

    private static InputExecutionOptions Options(string expected) => new() { Strategy = "semantic", Preconditions = Condition(expected) };
    private static RuntimeExpressionDefinition Condition(string expected) => new(Op("eq", new("value", "document"), Literal(expected)),
        [new("document", new(name: "Document"), "text")]);

    private static async Task WithWindow(Func<AvaScopeBridgeRuntime, Window, StackPanel, TextBox, Button, string, RuntimeTargetContext, LocalBridgeClient, Task> test)
    {
        var session = HeadlessUnitTestSession.StartNew(typeof(BridgeHeadlessSmokeTests.BridgeHeadlessTestApplication));
        try
        {
            await BridgeHeadlessSmokeTests.DispatchAsync(session, async () =>
            {
                AvaScopeBridge.Deactivate(); var runtime = AvaScopeBridge.Activate();
                var document = new TextBox { Name = "Document", Text = "document-a" };
                var button = new Button { Name = "Action", Content = "Apply" };
                var root = new StackPanel { Children = { document, button } };
                var window = new Window { Width = 500, Height = 300, Content = root };
                try
                {
                    window.Show(); using var registration = runtime.RegisterTopLevel(window);
                    var top = Assert.Single(await runtime.ListTopLevelsAsync());
                    Assert.True((await runtime.ReadinessAsync(top.Id, options: new(waitForFrame: true))).Success);
                    var client = new LocalBridgeClient(Path.GetDirectoryName(runtime.SessionManifestPath)!);
                    var query = await client.QueryNodesAsync(new(runtime.SessionId, top.Id, new(name: "Action"), maxDepth: 32));
                    Assert.True(query.Value!.Coverage!.Complete, JsonSerializer.Serialize(query));
                    var target = Assert.Single(query.Value.Matches).Target!;
                    await test(runtime, window, root, document, button, top.Id, target, client);
                }
                finally { window.Close(); AvaScopeBridge.Deactivate(); }
            }, CancellationToken.None);
        }
        finally { BridgeHeadlessSmokeTests.DisposeHeadlessSessionAfterExplicitCleanup(session); }
    }
}
