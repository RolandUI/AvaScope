using System.Diagnostics;
using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Headless;
using AvaScope.Bridge;
using AvaScope.Core;
using AvaScope.Mcp;
using AvaScope.Protocol;
using ModelContextProtocol.Client;

namespace AvaScope.Tests.Bridge;

[Collection(BridgeCollectionDefinition.Name)]
public sealed class RuntimeOperationTests
{
    [Fact]
    public async Task AppReportsImmediateAndDelayedOutcomesAndWaitTimeoutDoesNotStopWork()
    {
        await WithWindow(async (runtime, target, button, client) =>
        {
            RuntimeOperationHandle? handle = null;
            using var registered = runtime.RegisterCustomAction(button, new("import", context =>
            {
                handle = context.BeginOperation();
                Assert.Same(handle, context.BeginOperation());
                return CustomActionOutcome.Succeeded("Accepted");
            }, supportsOperations: true, supportsCancellation: true));
            var descriptor = Assert.Single((await client.CustomActionsAsync(runtime.SessionId, target)).Value!.Actions);
            Assert.True(descriptor.SupportsOperations); Assert.True(descriptor.SupportsCancellation);
            var action = await Start(client, runtime, target);
            Assert.Equal("accepted", action.Operation!.Status);
            Assert.Equal(action.RequestId, action.Operation.RequestId);
            var id = action.Operation.OperationId;
            Assert.True(handle!.ReportProgress(.4, "Four records"));
            var status = await client.OperationAsync(new(runtime.SessionId, id));
            Assert.Equal("running", status.Value!.Operation.Status); Assert.Equal(.4, status.Value.Operation.Progress);
            var timedOut = await AvaScopeMcpTools.Operation(client, new(runtime.SessionId, id, "wait", 30));
            Assert.False(timedOut.Success); Assert.Equal("runtime_operation_wait_timeout", timedOut.Error!.Code);
            Assert.False(handle.CancellationToken.IsCancellationRequested);
            using var stopObserving = new CancellationTokenSource(30);
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => client.OperationAsync(new(runtime.SessionId, id, "wait", 500), stopObserving.Token));
            Assert.False(handle.CancellationToken.IsCancellationRequested);
            var reconnected = new LocalBridgeClient(client.ManifestDirectory);
            var waiting = reconnected.OperationAsync(new(runtime.SessionId, id, "wait", 3000));
            await Task.Run(() => handle.Complete(new Dictionary<string, string> { ["records"] = "10" }));
            var completed = (await waiting).Value!.Operation;
            Assert.Equal("completed", completed.Status); Assert.Equal("10", completed.Result["records"]);
            Assert.Equal("app_reported", completed.Source);
            Assert.Equal(TimeSpan.FromMinutes(10), completed.RetainUntil - completed.UpdatedAt);
            Assert.False(handle.Fail("late", "A terminal result cannot be overwritten."));
            registered.Dispose();
            using var immediate = runtime.RegisterCustomAction(button, new("import", context =>
            {
                context.BeginOperation().Complete();
                return CustomActionOutcome.Succeeded("Completed immediately");
            }, supportsOperations: true));
            Assert.Equal("completed", (await Start(client, runtime, target)).Operation!.Status);
        });
    }

    [Fact]
    public async Task CancellationNeedsDeclaredSupportAndOriginatingRunAndDoesNotFabricateCompletion()
    {
        await WithWindow(async (runtime, target, button, client) =>
        {
            RuntimeOperationHandle? handle = null;
            using var registration = runtime.RegisterCustomAction(button, new("import", context =>
            { handle = context.BeginOperation(); handle.ReportProgress(.1); return CustomActionOutcome.Succeeded("Accepted"); },
                supportsOperations: true, supportsCancellation: true));
            Assert.True((await client.SessionControlAsync(runtime.SessionId, new("acquire", "operation-run"))).Success);
            var started = (await Start(client, runtime, target)).Operation!;
            Assert.Equal("operation-run", started.Owner);
            var other = new LocalBridgeClient(client.ManifestDirectory);
            Assert.Equal("session_control_conflict", (await other.OperationAsync(new(runtime.SessionId, started.OperationId, "cancel"))).Error!.Code);
            Assert.True((await client.SessionControlAsync(runtime.SessionId, new("release"))).Success);
            Assert.True((await other.SessionControlAsync(runtime.SessionId, new("acquire", "another-run"))).Success);
            Assert.Equal("runtime_operation_owner_mismatch", (await other.OperationAsync(new(runtime.SessionId, started.OperationId, "cancel"))).Error!.Code);
            await other.SessionControlAsync(runtime.SessionId, new("release"));
            await client.SessionControlAsync(runtime.SessionId, new("acquire", "operation-run"));
            var notified = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var notifications = 0;
            using var callback = handle!.CancellationToken.Register(() => { Interlocked.Increment(ref notifications); notified.TrySetResult(); });
            var requested = await client.OperationAsync(new(runtime.SessionId, started.OperationId, "cancel"));
            Assert.True(requested.Success, requested.Error?.Message);
            Assert.True(requested.Value!.Operation.CancellationRequested); Assert.Equal("running", requested.Value.Operation.Status);
            await notified.Task.WaitAsync(TimeSpan.FromSeconds(3));
            Assert.True((await client.OperationAsync(new(runtime.SessionId, started.OperationId, "cancel"))).Success);
            Assert.Equal(1, notifications);
            Assert.True(handle.Complete()); Assert.False(handle.ConfirmCancelled());
            Assert.Equal("completed", (await client.OperationAsync(new(runtime.SessionId, started.OperationId))).Value!.Operation.Status);

            var second = (await Start(client, runtime, target)).Operation!;
            var active = handle!;
            using var confirm = active.CancellationToken.Register(() => active.ConfirmCancelled());
            await client.OperationAsync(new(runtime.SessionId, second.OperationId, "cancel"));
            Assert.Equal("cancelled", (await client.OperationAsync(new(runtime.SessionId, second.OperationId, "wait", 3000))).Value!.Operation.Status);
            registration.Dispose();
            using var unsupported = runtime.RegisterCustomAction(button, new("import", context =>
            { handle = context.BeginOperation(); return CustomActionOutcome.Succeeded("Accepted"); }, supportsOperations: true));
            var third = (await Start(client, runtime, target)).Operation!;
            Assert.False(third.CanCancel);
            Assert.Equal("runtime_operation_cancel_unsupported", (await client.OperationAsync(new(runtime.SessionId, third.OperationId, "cancel"))).Error!.Code);
            Assert.False(handle!.CancellationToken.IsCancellationRequested);
        });
    }

    [Fact]
    public async Task FailedHandlerPreservesStartedOperationAndAppFailureRemainsStructured()
    {
        await WithWindow(async (runtime, target, button, client) =>
        {
            RuntimeOperationHandle? handle = null;
            CustomActionContext? saved = null;
            using var registration = runtime.RegisterCustomAction(button, new("import", context =>
            {
                saved = context; handle = context.BeginOperation();
                throw new InvalidOperationException("Handler failed after starting work");
            }, supportsOperations: true));
            var failedDispatch = await Start(client, runtime, target);
            Assert.Equal("failed", failedDispatch.Status); Assert.True(failedDispatch.Executed);
            Assert.Equal("accepted", failedDispatch.Operation!.Status);
            Assert.Throws<InvalidOperationException>(() => saved!.BeginOperation());
            handle!.ReportProgress(.2);
            Assert.True(handle.Fail("invalid_record", "Row 5 could not be imported."));
            var failed = (await client.OperationAsync(new(runtime.SessionId, handle.OperationId, "wait"))).Value!.Operation;
            Assert.Equal("failed", failed.Status); Assert.Equal("invalid_record", failed.Error!.Code);
            Assert.False(handle.ConfirmCancelled());
            Assert.Throws<ArgumentException>(() => handle.ReportProgress(double.NaN));
            Assert.Throws<ArgumentException>(() => handle.Complete(new Dictionary<string, string> { ["result"] = new('x', 513) }));
            Assert.Throws<ArgumentException>(() => new RuntimeOperationRequest(runtime.SessionId, handle.OperationId, timeoutMs: 30001));
            registration.Dispose();
            using var legacy = runtime.RegisterCustomAction(button, new("import", context =>
            { Assert.Throws<InvalidOperationException>(() => context.BeginOperation()); return CustomActionOutcome.Succeeded("Synchronous action"); }));
            Assert.Null((await Start(client, runtime, target)).Operation);
            Assert.False(Assert.Single((await client.CustomActionsAsync(runtime.SessionId, target)).Value!.Actions).SupportsOperations);
        });
    }

    [Fact]
    public async Task RetentionIsBoundedAndSessionShutdownInvalidatesObserversWithoutCancellingHostWork()
    {
        await WithWindow(async (runtime, target, button, client) =>
        {
            RuntimeOperationHandle? handle = null;
            var immediate = true;
            using var registration = runtime.RegisterCustomAction(button, new("import", context =>
            { handle = context.BeginOperation(); if (immediate) handle.Complete(); return CustomActionOutcome.Succeeded("Accepted"); }, supportsOperations: true));
            var first = (await runtime.InvokeCustomActionAsync(new("first", target, "import"))).Value!.Operation!.OperationId;
            for (var index = 0; index < 128; index++)
                Assert.True((await runtime.InvokeCustomActionAsync(new("retention-" + index, target, "import"))).Success);
            Assert.Equal("runtime_operation_unknown", (await client.OperationAsync(new(runtime.SessionId, first))).Error!.Code);
            immediate = false;
            for (var index = 0; index < 32; index++)
                Assert.NotNull((await runtime.InvokeCustomActionAsync(new("active-" + index, target, "import"))).Value!.Operation);
            var refused = (await runtime.InvokeCustomActionAsync(new("over-capacity", target, "import"))).Value!;
            Assert.Equal("failed", refused.Status); Assert.Null(refused.Operation);
            var active = handle!;
            var observing = runtime.OperationAsync(new(runtime.SessionId, active.OperationId, "wait", 30000));
            AvaScopeBridge.Deactivate();
            Assert.Equal("runtime_operation_session_closed", (await observing).Error!.Code);
            Assert.False(active.CancellationToken.IsCancellationRequested); Assert.False(active.Complete());
            var replacement = AvaScopeBridge.Activate();
            Assert.NotEqual(runtime.SessionId, replacement.SessionId);
            Assert.Equal("runtime_operation_session_mismatch", (await replacement.OperationAsync(new(replacement.SessionId, active.OperationId))).Error!.Code);
        });
    }

    [Fact]
    public async Task DestructiveCancellationEnforcesPolicyAndCallbackFailureDoesNotReportAppCompletion()
    {
        await WithWindow(async (runtime, target, button, client) =>
        {
            RuntimeOperationHandle? handle = null;
            using var registration = runtime.RegisterCustomAction(button, new("import", context =>
            { handle = context.BeginOperation(); handle.ReportProgress(.1); return CustomActionOutcome.Succeeded("Accepted"); },
                safetyClassification: RuntimeCustomActionSafetyClassifications.Destructive, supportsOperations: true, supportsCancellation: true));
            Assert.Null((await Start(client, runtime, target)).Operation);
            Assert.Null(handle);
            var started = (await client.InvokeCustomActionAsync(runtime.SessionId, new("destructive", target, "import", allowDestructive: true))).Value!.Operation!;
            Assert.NotNull(started);
            Assert.Equal("runtime_operation_cancel_disallowed", (await client.OperationAsync(new(runtime.SessionId, started.OperationId, "cancel"))).Error!.Code);
            var deniedPolicy = new RuntimeEvidencePolicy(Path.GetTempPath(), allowedActions: [SemanticWorkflowActions.CustomAction], allowDestructiveActions: true);
            Assert.False((await client.OperationAsync(new(runtime.SessionId, started.OperationId, "cancel", allowDestructive: true, policy: deniedPolicy))).Success);
            Assert.False(handle!.CancellationToken.IsCancellationRequested);
            var allowedPolicy = new RuntimeEvidencePolicy(Path.GetTempPath(), allowedActions: [SemanticWorkflowActions.CustomAction],
                allowedCustomActions: ["import"], allowDestructiveActions: true);
            using var callback = handle.CancellationToken.Register(() => throw new InvalidOperationException("callback-private-canary"));
            Assert.True((await client.OperationAsync(new(runtime.SessionId, started.OperationId, "cancel", allowDestructive: true, policy: allowedPolicy))).Success);
            RuntimeOperationSnapshot observed;
            var deadline = Stopwatch.StartNew();
            do
            {
                observed = (await client.OperationAsync(new(runtime.SessionId, started.OperationId))).Value!.Operation;
                if (observed.Error is not null) break;
                await Task.Delay(10);
            } while (deadline.Elapsed < TimeSpan.FromSeconds(3));
            Assert.Equal("runtime_operation_cancel_callback_failed", observed.Error!.Code);
            Assert.Equal("running", observed.Status); Assert.DoesNotContain("callback-private-canary", JsonSerializer.Serialize(observed));
            Assert.True(handle.ConfirmCancelled());
        });
    }

    [Fact]
    public async Task WorkflowIdempotencyRetainsOriginalOperationWithoutStartingDuplicateWork()
    {
        await WithWindow(async (runtime, target, button, client) =>
        {
            var calls = 0; RuntimeOperationHandle? handle = null;
            using var registration = runtime.RegisterCustomAction(button, new("import", context =>
            { calls++; handle = context.BeginOperation(); return CustomActionOutcome.Succeeded("Accepted"); }, supportsOperations: true));
            var runner = new SemanticWorkflowRunner();
            var request = new SemanticWorkflowRequest(runtime.SessionId, target.TopLevelId,
                [new(SemanticWorkflowActions.CustomAction, selector: new(name: "Import"), customActionName: "import", idempotencyKey: "one-import")]);
            var first = await runner.RunAsync(client, request);
            Assert.True(OperationResultMapper.IsSuccessful(first), JsonSerializer.Serialize(first));
            var original = first.Value!.Steps[0].CustomAction!.Operation!;
            Assert.Equal("accepted", original.Status); handle!.Complete();
            var replay = await runner.RunAsync(client, request);
            Assert.True(OperationResultMapper.IsSuccessful(replay), JsonSerializer.Serialize(replay)); Assert.Equal(1, calls);
            Assert.Equal(original.OperationId, replay.Value!.Steps[0].CustomAction!.Operation!.OperationId);
            Assert.Equal("accepted", replay.Value.Steps[0].CustomAction!.Operation!.Status);
            Assert.Equal("completed", (await client.OperationAsync(new(runtime.SessionId, original.OperationId))).Value!.Operation.Status);
        });
    }

    [Fact]
    public async Task RealCliAndMcpReturnProgressTerminalResultsAndSanitizedEvidence()
    {
        await WithWindow(async (runtime, target, button, client) =>
        {
            RuntimeOperationHandle? handle = null;
            using var registration = runtime.RegisterCustomAction(button, new("import", context =>
            { handle = context.BeginOperation(); handle.ReportProgress(.5, "operation-private-canary"); return CustomActionOutcome.Succeeded("Accepted"); },
                supportsOperations: true, supportsCancellation: true));
            var started = (await Start(client, runtime, target)).Operation!;
            var path = Path.Combine(Path.GetTempPath(), "operation-" + Guid.NewGuid().ToString("N") + ".json");
            try
            {
                var policy = new RuntimeEvidencePolicy(Path.GetTempPath(), redactedText: ["operation-private-canary"], allowedActions: [SemanticWorkflowActions.Inspect]);
                var request = new RuntimeOperationRequest(runtime.SessionId, started.OperationId, policy: policy);
                await File.WriteAllTextAsync(path, JsonSerializer.Serialize(request));
                var start = new ProcessStartInfo("dotnet") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
                foreach (var arg in new[] { Path.Combine(AppContext.BaseDirectory, "avascope.dll"), "operation", "--request", path,
                    "--manifest-dir", client.ManifestDirectory }) start.ArgumentList.Add(arg);
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(40));
                using var process = Process.Start(start)!;
                var output = process.StandardOutput.ReadToEndAsync(); var error = process.StandardError.ReadToEndAsync();
                try { await process.WaitForExitAsync(timeout.Token); }
                finally { if (!process.HasExited) process.Kill(entireProcessTree: true); }
                var cli = JsonSerializer.Deserialize<ToolResult<RuntimeOperationResponse>>(await output)!;
                Assert.True(cli.Success, cli.Error?.Message + await error); Assert.Equal(0, process.ExitCode);
                Assert.Equal(.5, cli.Value!.Operation.Progress); Assert.DoesNotContain("operation-private-canary", await output);
                var environment = TestEnvironment.McpEnvironment();
                if (Environment.GetEnvironmentVariable("TMPDIR") is { } temporary) environment["TMPDIR"] = temporary;
                await using var mcp = await McpClient.CreateAsync(new StdioClientTransport(new StdioClientTransportOptions
                { Name = "Application operations", Command = "dotnet", Arguments = [Path.Combine(AppContext.BaseDirectory, "AvaScope.Mcp.dll")],
                    InheritEnvironmentVariables = false, EnvironmentVariables = environment, ShutdownTimeout = TimeSpan.FromSeconds(3) }), cancellationToken: timeout.Token);
                handle!.Complete(new Dictionary<string, string> { ["rows"] = "10", ["note"] = "operation-private-canary" });
                var call = await mcp.CallToolAsync("operation", new Dictionary<string, object?>
                { ["request"] = JsonSerializer.SerializeToElement(new RuntimeOperationRequest(runtime.SessionId, started.OperationId, "wait", policy: policy)),
                    ["manifestDirectory"] = client.ManifestDirectory }, cancellationToken: timeout.Token);
                var result = JsonSerializer.Deserialize<ToolResult<RuntimeOperationResponse>>(JsonSerializer.Serialize(call.StructuredContent))!;
                Assert.True(result.Success, result.Error?.Message); Assert.Equal("completed", result.Value!.Operation.Status);
                Assert.Equal("10", result.Value.Operation.Result["rows"]); Assert.DoesNotContain("operation-private-canary", JsonSerializer.Serialize(result));
                var denied = await client.OperationAsync(new(runtime.SessionId, started.OperationId, "cancel", policy: policy));
                Assert.False(denied.Success);
            }
            finally { File.Delete(path); }
        });
    }

    private static async Task<RuntimeCustomActionResponse> Start(LocalBridgeClient client, AvaScopeBridgeRuntime runtime, RuntimeTargetContext target)
    {
        var result = await client.InvokeCustomActionAsync(runtime.SessionId, new(Guid.NewGuid().ToString("N"), target, "import"));
        Assert.True(result.Success, result.Error?.Message); return result.Value!;
    }

    private static async Task WithWindow(Func<AvaScopeBridgeRuntime, RuntimeTargetContext, Button, LocalBridgeClient, Task> test)
    {
        var session = HeadlessUnitTestSession.StartNew(typeof(BridgeHeadlessSmokeTests.BridgeHeadlessTestApplication));
        try
        {
            await BridgeHeadlessSmokeTests.DispatchAsync(session, async () =>
            {
                AvaScopeBridge.Deactivate(); var runtime = AvaScopeBridge.Activate(new(enableCustomActions: true, allowedCustomActions: ["import"], allowDestructiveCustomActions: true));
                var button = new Button { Name = "Import", Content = "Import" };
                var window = new Window { Width = 300, Height = 180, Content = button };
                try
                {
                    window.Show(); using var registration = runtime.RegisterTopLevel(window);
                    var top = Assert.Single(await runtime.ListTopLevelsAsync());
                    var client = new LocalBridgeClient(Path.GetDirectoryName(runtime.SessionManifestPath)!);
                    var node = Assert.Single((await client.FindNodesAsync(runtime.SessionId, top.Id, TreeKinds.Visual, name: "Import")).Value!.Matches).Node;
                    await test(runtime, new(runtime.SessionId, top.Id, TreeKinds.Visual, node.NodeId), button, client);
                }
                finally { window.Close(); AvaScopeBridge.Deactivate(); }
            }, CancellationToken.None);
        }
        finally { BridgeHeadlessSmokeTests.DisposeHeadlessSessionAfterExplicitCleanup(session); }
    }
}
