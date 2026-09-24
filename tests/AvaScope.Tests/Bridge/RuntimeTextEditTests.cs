using System.Diagnostics;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using AvaScope.Bridge;
using AvaScope.Core;
using AvaScope.Protocol;
using ModelContextProtocol.Client;

namespace AvaScope.Tests.Bridge;

[Collection(BridgeCollectionDefinition.Name)]
public sealed class RuntimeTextEditTests
{
    [Fact]
    public async Task MalformedMcpArgumentsReturnSafeStructuredErrorsBeforeDispatchAndLeaveValidReplayIntact()
    {
        await WithWindow(async (runtime, root, top, client) =>
        {
            var box = new TextBox { Name = "Editor", Text = "Ada" };
            root.Children.Add(box); Dispatcher.UIThread.RunJobs();
            var target = await Target(runtime, top, "Editor");
            var state = await Read(client, target);
            var dispatched = 0;
            box.AddHandler(InputElement.TextInputEvent, (_, _) => dispatched++, RoutingStrategies.Tunnel);
            const string privateText = "private-mcp-validation-canary";
            var policy = new RuntimeEvidencePolicy(Path.Combine(Path.GetTempPath(), "avascope-text-validation"),
                redactedText: [privateText], allowedDesiredStates: ["text"]);
            var request = Edit(target, state, "insert", 1, text: "X", policy: policy);
            var stderr = new System.Collections.Concurrent.ConcurrentQueue<string>();
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(90));
            await using var mcp = await McpClient.CreateAsync(new StdioClientTransport(new()
            {
                Command = "dotnet", Arguments = [Path.Combine(AppContext.BaseDirectory, "AvaScope.Mcp.dll")],
                Name = "text-validation-test", StandardErrorLines = stderr.Enqueue
            }), cancellationToken: timeout.Token);

            Action<JsonObject>[] invalidChanges =
            [
                json => json.Remove("start"),
                json => json["start"] = -1,
                json => json["end"] = 1,
                json => json["start"] = privateText,
                json => json["action"] = privateText,
                json => json["target"] = null,
                json => { json["target"]!["targetKind"] = privateText; json["target"]!["treeKind"] = null; },
                json => json["policy"]!["retentionMaxAgeMinutes"] = -1,
                json => json["text"] = new string('x', RuntimeTextEditRequest.MaximumReplacementLength + 1)
            ];
            for (var index = 0; index < invalidChanges.Length + 2; index++)
            {
                var json = JsonSerializer.SerializeToNode(request)!.AsObject();
                if (index < invalidChanges.Length) invalidChanges[index](json);
                var arguments = new Dictionary<string, object?> { ["manifestDirectory"] = client.ManifestDirectory };
                if (index <= invalidChanges.Length) arguments["request"] = index == invalidChanges.Length ? null : json;
                var call = await mcp.CallToolAsync("edit_text", arguments, cancellationToken: timeout.Token);
                Assert.True(call.IsError, $"Malformed case {index}: {JsonSerializer.Serialize(call)}");
                Assert.NotNull(call.StructuredContent);
                var failed = JsonSerializer.Deserialize<ToolResult<RuntimeTextEditResponse>>(JsonSerializer.Serialize(call.StructuredContent))!;
                Assert.False(failed.Success); Assert.Equal("invalid_mcp_arguments", failed.Error!.Code);
                Assert.Equal("false", failed.Error.Details!["dispatched"]);
                Assert.Equal("request", failed.Error.Details["argument"]);
                Assert.Contains("schema", failed.Error.Details["nextAction"]);
                if (index == 0) Assert.Contains("start offset", failed.Error.Message);
                Assert.DoesNotContain(privateText, JsonSerializer.Serialize(call));
                Assert.Equal("Ada", box.Text); Assert.Equal(0, dispatched);
            }

            var readCall = await mcp.CallToolAsync("edit_text", new Dictionary<string, object?>
            { ["request"] = JsonSerializer.SerializeToElement(new RuntimeTextEditRequest(target)), ["manifestDirectory"] = client.ManifestDirectory }, cancellationToken: timeout.Token);
            var read = JsonSerializer.Deserialize<ToolResult<RuntimeTextEditResponse>>(JsonSerializer.Serialize(readCall.StructuredContent))!;
            Assert.True(read.Success); Assert.Equal(state, read.Value!.After);
            for (var replay = 0; replay < 2; replay++)
            {
                var call = await mcp.CallToolAsync("edit_text", new Dictionary<string, object?>
                { ["request"] = JsonSerializer.SerializeToElement(request), ["manifestDirectory"] = client.ManifestDirectory }, cancellationToken: timeout.Token);
                var edited = JsonSerializer.Deserialize<ToolResult<RuntimeTextEditResponse>>(JsonSerializer.Serialize(call.StructuredContent))!;
                Assert.True(edited.Success, JsonSerializer.Serialize(call)); Assert.Equal(replay == 1, edited.Value!.Replayed);
                Assert.Equal("AXda", edited.Value.After!.Text); Assert.Equal(1, dispatched);
            }
            var pick = await mcp.CallToolAsync("pick_node", new Dictionary<string, object?>
            { ["request"] = new { target = new { sessionId = runtime.SessionId.Value, topLevelId = top } } }, cancellationToken: timeout.Token);
            Assert.True(pick.IsError); Assert.NotNull(pick.StructuredContent);
            Assert.Equal("invalid_mcp_arguments", JsonSerializer.Deserialize<ToolResult<object>>(JsonSerializer.Serialize(pick.StructuredContent))!.Error!.Code);
            Assert.DoesNotContain(privateText, string.Join('\n', stderr));
        });
    }

    [Fact]
    public async Task MultilineUnicodeRangeSelectionInsertionAndUndoPreserveSurroundingText()
    {
        await WithWindow(async (runtime, root, top, client) =>
        {
            const string initial = "first 😀\r\npath=/old/út\nlast e\u0301";
            var box = new TextBox { Name = "Editor", Text = initial, AcceptsReturn = true }; root.Children.Add(box); Dispatcher.UIThread.RunJobs();
            var target = await Target(runtime, top, "Editor");
            var state = await Read(client, target);
            var start = initial.IndexOf("/old/", StringComparison.Ordinal);
            var request = Edit(target, state, "replace_range", start, start + "/old/út".Length, "/new/東京");
            var started = Stopwatch.GetTimestamp();
            var response = await client.EditTextAsync(request);
            // Preserve the observed state on a hosted timeout without retrying an uncertain edit.
            Assert.True(OperationResultMapper.ToToolResult(response).Success, JsonSerializer.Serialize(new
            {
                Response = response, ElapsedMs = Stopwatch.GetElapsedTime(started).TotalMilliseconds,
                TimeoutMs = client.OperationTimeout.TotalMilliseconds,
                InitialTextUnchanged = box.Text == initial,
                ExpectedTextObserved = box.Text == "first 😀\r\npath=/new/東京\nlast e\u0301",
                box.IsFocused, box.CaretIndex, box.SelectionStart, box.SelectionEnd
            }));
            var result = response.Value!;
            Assert.Equal("first 😀\r\npath=/new/東京\nlast e\u0301", result.After!.Text);
            Assert.Equal(start + "/new/東京".Length, result.After.Caret);
            Assert.Equal(result.After.Caret, result.After.SelectionStart); Assert.Equal(result.After.Caret, result.After.SelectionEnd);
            Assert.Equal(1, result.DispatchedOperations);
            box.Undo(); Assert.Equal(initial, box.Text);

            state = await Read(client, target);
            var selected = Verified(await client.EditTextAsync(Edit(target, state, "select_range", 0, 5)));
            Assert.Equal(initial, box.Text); Assert.Equal(0, selected.DispatchedOperations);
            Assert.Equal(0, selected.After!.SelectionStart); Assert.Equal(5, selected.After.SelectionEnd);
            var replaced = Verified(await client.EditTextAsync(Edit(target, selected.After, "replace_selection", text: "start")));
            Assert.StartsWith("start 😀\r\n", replaced.After!.Text);
            var inserted = Verified(await client.EditTextAsync(Edit(target, replaced.After, "insert", 0, text: "→")));
            Assert.StartsWith("→start", inserted.After!.Text);
            var deleted = Verified(await client.EditTextAsync(Edit(target, inserted.After, "replace_range", 0, 1, "")));
            Assert.StartsWith("start", deleted.After!.Text); Assert.Equal(2, deleted.DispatchedOperations);
            var empty = Verified(await client.EditTextAsync(Edit(target, deleted.After, "replace_selection", text: "")));
            Assert.Equal(deleted.After.Text, empty.After!.Text); Assert.Equal(0, empty.DispatchedOperations);
            box.Text = "";
            var emptyInsert = Verified(await client.EditTextAsync(Edit(target, await Read(client, target), "insert", 0, text: "a\r\nb\nc")));
            Assert.Equal("a\r\nb\nc", emptyInsert.After!.Text);
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SlowDispatchedCallbackRetainsSafeFinalStateWithoutRepeatingInput(bool protectAfterDispatch)
    {
        await WithWindow(async (runtime, root, top, client) =>
        {
            var box = new TextBox { Name = "Editor", Text = "before" }; root.Children.Add(box); Dispatcher.UIThread.RunJobs();
            var target = await Target(runtime, top, "Editor");
            var request = Edit(target, await Read(client, target), "replace_range", 0, 6, "after");
            var calls = 0;
            box.AddHandler(InputElement.TextInputEvent, (_, _) =>
            {
                calls++;
                Thread.Sleep(2100); // A synchronous application callback can exceed the preparation deadline.
                if (protectAfterDispatch) box.PasswordChar = '*';
            }, RoutingStrategies.Tunnel);
            var response = await client.EditTextAsync(request);
            var result = protectAfterDispatch ? Rejected(response, "text_edit_sensitive", 1) : Verified(response);
            if (protectAfterDispatch) { Assert.Null(result.Before); Assert.Null(result.After); }
            else { Assert.Equal("after", result.After!.Text); Assert.Equal(5, result.After.Caret); Assert.Equal(1, result.DispatchedOperations); }
            var replay = (await client.EditTextAsync(request)).Value!;
            Assert.True(replay.Replayed); Assert.Equal(result.After, replay.After); Assert.Equal(1, calls);
        });
    }

    [Fact]
    public async Task InvalidBoundariesStaleStateAndReentrantPreparationNeverDispatchOldRanges()
    {
        await WithWindow(async (runtime, root, top, client) =>
        {
            var box = new ChangingTextBox { Name = "Editor", Text = "a😀\r\nb", AcceptsReturn = true };
            root.Children.Add(box); Dispatcher.UIThread.RunJobs();
            var target = await Target(runtime, top, "Editor");
            var state = await Read(client, target);
            foreach (var index in new[] { 2, 4, 99 })
                Rejected(await client.EditTextAsync(Edit(target, state, "insert", index, text: "x")), "text_edit_range", 0);
            box.Text = "new text";
            Rejected(await client.EditTextAsync(Edit(target, state, "replace_range", 0, 1, "x")), "text_edit_stale", 0);
            state = await Read(client, target); box.CaretIndex = 1;
            Rejected(await client.EditTextAsync(Edit(target, state, "insert", 0, text: "x")), "text_edit_stale", 0);
            state = await Read(client, target); box.ChangeOnContext = true;
            Rejected(await client.EditTextAsync(Edit(target, state, "insert", 0, text: "x")), "text_edit_stale", 0);
            Assert.Equal("changed by app", box.Text);
            box.PropertyChanged += (_, args) => { if (args.Property == TextBox.SelectionStartProperty) box.Text = "selection callback"; };
            state = await Read(client, target);
            Rejected(await client.EditTextAsync(Edit(target, state, "replace_range", 2, 4, "x")), "text_edit_stale", 0);
            Assert.Equal("selection callback", box.Text);
            var validationBox = new TextBox { Name = "Validation", Text = "hello" };
            root.Children.Add(validationBox); Dispatcher.UIThread.RunJobs();
            var validationTarget = await Target(runtime, top, "Validation");
            validationBox.PropertyChanged += (_, args) =>
            {
                if (args.Property == TextBox.SelectionEndProperty && validationBox.SelectionEnd == 3)
                    DataValidationErrors.SetErrors(validationBox, ["changed during selection"]);
            };
            Rejected(await client.EditTextAsync(Edit(validationTarget, await Read(client, validationTarget), "replace_range", 1, 3, "x")), "text_edit_stale", 0);
            Assert.Equal("hello", validationBox.Text);
            root.Children.Remove(box);
            Assert.False((await client.EditTextAsync(new(target))).Success);
        });
    }

    [Fact]
    public async Task SensitiveReadOnlyUnsupportedAndOversizedFieldsFailWithoutLeakingTextOrRevisions()
    {
        await WithWindow(async (runtime, root, top, client) =>
        {
            var box = new TextBox { Name = "Editor", Text = "safe", IsReadOnly = true };
            root.Children.Add(box); root.Children.Add(new Control { Name = "Rich" }); Dispatcher.UIThread.RunJobs();
            var target = await Target(runtime, top, "Editor");
            Rejected(await client.EditTextAsync(Edit(target, await Read(client, target), "insert", 0, text: "x")), "text_edit_read_only", 0);
            Assert.Equal("text_edit_unsupported", (await client.EditTextAsync(new(await Target(runtime, top, "Rich")))).Error!.Code);
            box.IsReadOnly = false; box.PasswordChar = '*'; box.Text = "hidden-password"; box.RevealPassword = true;
            var password = Rejected(await client.EditTextAsync(new(target)), "text_edit_sensitive", 0);
            Assert.Null(password.Before); Assert.Null(password.After); Assert.DoesNotContain("hidden-password", JsonSerializer.Serialize(password));
            box.PasswordChar = '\0'; box.Text = "safe secret suffix";
            var policy = new RuntimeEvidencePolicy(Path.Combine(Path.GetTempPath(), "avascope-text-tests"), redactedText: ["secret"],
                authorizedSessionIds: [runtime.SessionId.Value], allowedDesiredStates: ["text"]);
            var sensitive = Rejected(await client.EditTextAsync(new(target, policy: policy)), "text_edit_sensitive", 0);
            Assert.Null(sensitive.Before); Assert.Null(sensitive.After); Assert.DoesNotContain("safe secret suffix", JsonSerializer.Serialize(sensitive));
            box.Text = "safe";
            Rejected(await client.EditTextAsync(Edit(target, await Read(client, target), "insert", 0, text: "secret", policy: policy)), "text_edit_sensitive", 0);
            Assert.Equal("safe", box.Text);
            AutomationProperties.SetAutomationId(root, "PrivateGroup");
            foreach (var exclude in new[] { false, true })
            {
                var ancestorPolicy = new RuntimeEvidencePolicy(policy.OwnedEvidenceRoot,
                    excludedControlAutomationIds: exclude ? ["PrivateGroup"] : [], redactedAutomationIds: exclude ? [] : ["PrivateGroup"],
                    authorizedSessionIds: [runtime.SessionId.Value]);
                var hidden = Rejected(await client.EditTextAsync(new(target, policy: ancestorPolicy)), "text_edit_sensitive", 0);
                Assert.Null(hidden.Before); Assert.Null(hidden.After);
            }
            AutomationProperties.SetAutomationId(root, null);
            box.Text = new string('x', 8193);
            Assert.Null(Rejected(await client.EditTextAsync(new(target)), "text_edit_state_unavailable", 0).After);
            box.Text = "\ud800";
            Assert.Null(Rejected(await client.EditTextAsync(new(target)), "text_edit_state_unavailable", 0).After);
        });
    }

    [Fact]
    public async Task AppValidationRejectionAndPostDispatchFailureRemainObservableAndAreNeverReplayed()
    {
        await WithWindow(async (runtime, root, top, client) =>
        {
            var box = new TextBox { Name = "Editor", Text = "before", MaxLength = 6 }; root.Children.Add(box); Dispatcher.UIThread.RunJobs();
            var target = await Target(runtime, top, "Editor");
            Rejected(await client.EditTextAsync(Edit(target, await Read(client, target), "insert", 0, text: "too long")), "text_edit_length", 0);
            box.MaxLength = 0;
            EventHandler<TextInputEventArgs> reject = (_, args) => args.Handled = true;
            box.AddHandler(InputElement.TextInputEvent, reject, RoutingStrategies.Tunnel);
            Rejected(await client.EditTextAsync(Edit(target, await Read(client, target), "replace_range", 0, 6, "after")), "text_edit_not_verified", 1);
            Assert.Equal("before", box.Text); box.RemoveHandler(InputElement.TextInputEvent, reject);
            DataValidationErrors.SetErrors(box, [new InvalidOperationException("private validation detail")]);
            var invalid = Rejected(await client.EditTextAsync(Edit(target, await Read(client, target), "replace_range", 0, 6, "after")), "text_edit_validation_errors", 1);
            Assert.Equal("after", invalid.After!.Text); Assert.True(invalid.After.HasValidationErrors);
            Assert.DoesNotContain("private validation detail", JsonSerializer.Serialize(invalid));
            DataValidationErrors.ClearErrors(box);
            var calls = 0;
            box.AddHandler(InputElement.TextInputEvent, (_, _) => { calls++; box.Text = "committed"; throw new InvalidOperationException("private callback detail"); }, RoutingStrategies.Tunnel);
            var request = Edit(target, await Read(client, target), "insert", 0, text: "x");
            var failure = Rejected(await client.EditTextAsync(request), "text_edit_callback_failed", 1);
            Assert.Equal("uncertain", failure.Status); Assert.Equal("committed", failure.After!.Text);
            var replayed = Rejected(await new LocalBridgeClient(client.ManifestDirectory).EditTextAsync(request), "text_edit_callback_failed", 1);
            Assert.True(replayed.Replayed); Assert.Equal(1, calls);
            Assert.DoesNotContain("private callback detail", JsonSerializer.Serialize(replayed));
            var conflict = new RuntimeTextEditRequest(target, "insert", request.RequestId, request.ExpectedRevision, start: 0, text: "different");
            Assert.Equal("text_edit_id_conflict", (await client.EditTextAsync(conflict)).Error!.Code);
        });
    }

    [Fact]
    public async Task CliMcpPolicyAndControlLeaseShareReadEditReplayAndFailureContracts()
    {
        await WithWindow(async (runtime, root, top, client) =>
        {
            var box = new TextBox { Name = "Editor", Text = "path=old" }; root.Children.Add(box); Dispatcher.UIThread.RunJobs();
            var target = await Target(runtime, top, "Editor");
            var policy = new RuntimeEvidencePolicy(Path.Combine(Path.GetTempPath(), "avascope-text-tests"),
                authorizedSessionIds: [runtime.SessionId.Value], allowedDesiredStates: ["text"]);
            var state = await Read(client, target);
            var request = Edit(target, state, "replace_range", 5, 8, "new", policy);
            var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".json");
            try
            {
                await File.WriteAllTextAsync(path, JsonSerializer.Serialize(request));
                var start = new ProcessStartInfo("dotnet") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
                foreach (var arg in new[] { Path.Combine(AppContext.BaseDirectory, "avascope.dll"), "edit-text", "--request", path, "--manifest-dir", client.ManifestDirectory }) start.ArgumentList.Add(arg);
                using var process = Process.Start(start)!;
                var stdout = process.StandardOutput.ReadToEndAsync(); var stderr = process.StandardError.ReadToEndAsync();
                await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(30));
                var cli = JsonSerializer.Deserialize<ToolResult<RuntimeTextEditResponse>>(await stdout)!;
                Assert.True(cli.Success, await stdout + await stderr); Assert.Equal(0, process.ExitCode); Assert.Equal("path=new", cli.Value!.After!.Text);
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
                await using var mcp = await McpClient.CreateAsync(new StdioClientTransport(new()
                { Command = "dotnet", Arguments = [Path.Combine(AppContext.BaseDirectory, "AvaScope.Mcp.dll")], Name = "text-edit-test" }), cancellationToken: timeout.Token);
                var call = await mcp.CallToolAsync("edit_text", new Dictionary<string, object?>
                { ["request"] = JsonSerializer.SerializeToElement(request), ["manifestDirectory"] = client.ManifestDirectory }, cancellationToken: timeout.Token);
                var replayed = JsonSerializer.Deserialize<ToolResult<RuntimeTextEditResponse>>(JsonSerializer.Serialize(call.StructuredContent))!;
                Assert.True(replayed.Success); Assert.True(replayed.Value!.Replayed); Assert.Equal(cli.Value.After, replayed.Value.After);
                box.IsReadOnly = true;
                var denied = Edit(target, await Read(client, target), "insert", 0, text: "x");
                call = await mcp.CallToolAsync("edit_text", new Dictionary<string, object?>
                { ["request"] = JsonSerializer.SerializeToElement(denied), ["manifestDirectory"] = client.ManifestDirectory }, cancellationToken: timeout.Token);
                var failed = JsonSerializer.Deserialize<ToolResult<RuntimeTextEditResponse>>(JsonSerializer.Serialize(call.StructuredContent))!;
                Assert.False(failed.Success); Assert.Equal("text_edit_read_only", failed.Error!.Code); Assert.NotNull(failed.Value);
                var readPolicy = new RuntimeEvidencePolicy(policy.OwnedEvidenceRoot, authorizedSessionIds: [runtime.SessionId.Value]);
                Assert.Equal("text_edit_policy_denied", (await client.EditTextAsync(Edit(target, await Read(client, target), "insert", 0, text: "x", policy: readPolicy))).Error!.Code);
                Assert.True((await client.SessionControlAsync(runtime.SessionId, new("acquire", "editor"))).Success);
                var outsider = new LocalBridgeClient(client.ManifestDirectory);
                Assert.NotNull(await Read(outsider, target));
                Assert.Equal("session_control_conflict", (await outsider.EditTextAsync(denied)).Error!.Code);
            }
            finally { File.Delete(path); }
        });
    }

    [Fact]
    public async Task DisconnectAfterDispatchRecoversTheRetainedResultWithoutSecondInput()
    {
        await WithWindow(async (runtime, root, top, client) =>
        {
            var box = new TextBox { Name = "Editor", Text = "a" }; root.Children.Add(box); Dispatcher.UIThread.RunJobs();
            var target = await Target(runtime, top, "Editor");
            var request = Edit(target, await Read(client, target), "insert", 1, text: "b");
            var received = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var calls = 0;
            box.AddHandler(InputElement.TextInputEvent, (_, _) => { calls++; received.TrySetResult(); }, RoutingStrategies.Tunnel);
            var manifest = client.ListSessionManifests().Single(item => item.SessionId == runtime.SessionId);
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            await using (var pipe = new NamedPipeClientStream(".", manifest.PipeName, PipeDirection.InOut, PipeOptions.Asynchronous))
            {
                await pipe.ConnectAsync(timeout.Token);
                await pipe.WriteAsync(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new BridgeIpcRequest("discard-text-response", BridgeIpcMethods.EditText, textEdit: request)) + "\n"), timeout.Token);
                await pipe.FlushAsync(timeout.Token);
                await received.Task.WaitAsync(timeout.Token);
                // Drop this connection without reading the response after the app received the edit.
            }
            var recovered = Verified(await client.EditTextAsync(request));
            Assert.True(recovered.Replayed); Assert.Equal("ab", recovered.After!.Text); Assert.Equal("ab", box.Text); Assert.Equal(1, calls);
        });
    }

    [Fact]
    public async Task IpcTimeoutDuringDispatchedCallbackPreservesOneEditAndItsRecoverableOutcome()
    {
        await WithWindow(async (runtime, root, top, client) =>
        {
            var box = new TextBox { Name = "Editor", Text = "a" }; root.Children.Add(box); Dispatcher.UIThread.RunJobs();
            var target = await Target(runtime, top, "Editor");
            var request = Edit(target, await Read(client, target), "insert", 1, text: "b");
            var received = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            using var releaseCallback = new ManualResetEventSlim();
            var calls = 0;
            box.AddHandler(InputElement.TextInputEvent, (_, _) =>
            {
                calls++;
                received.TrySetResult();
                Assert.True(releaseCallback.Wait(TimeSpan.FromSeconds(20)), "The test must release the application callback after observing the client timeout.");
            }, RoutingStrategies.Tunnel);

            // Observe the client deadline off the blocked UI thread; always release the callback.
            var uncertain = await Task.Run(async () =>
            {
                var edit = client.EditTextAsync(request);
                try
                {
                    await received.Task.WaitAsync(TimeSpan.FromSeconds(10));
                    return await edit.WaitAsync(TimeSpan.FromSeconds(10));
                }
                finally { releaseCallback.Set(); }
            });
            Assert.False(uncertain.Success);
            Assert.Equal(CoreErrorCodes.BridgeIpcUnavailable, uncertain.Error!.Code);
            Assert.Equal("Bridge IPC request timed out.", uncertain.Error.Message);
            Assert.Equal("unknown", uncertain.Error.Details!["dispatched"]);
            Assert.Equal(request.RequestId, uncertain.Error.Details["textEditRequestId"]);
            Assert.Equal("ab", box.Text); Assert.Equal(1, calls);
            Assert.Equal("ab", (await Read(client, target)).Text);

            var recovered = Verified(await client.EditTextAsync(request));
            Assert.True(recovered.Replayed); Assert.Equal("ab", recovered.After!.Text);
            Assert.Equal(1, recovered.DispatchedOperations); Assert.Equal(1, calls);
        });
    }

    [Fact]
    public async Task LedgerLimitPreservesOldResultsAndShutdownPreventsReuse()
    {
        await WithWindow(async (runtime, root, top, client) =>
        {
            var box = new TextBox { Name = "Editor", Text = "a" }; root.Children.Add(box); Dispatcher.UIThread.RunJobs();
            var target = await Target(runtime, top, "Editor");
            var first = Edit(target, await Read(client, target), "select_range", 0, 0);
            Verified(await runtime.EditTextAsync(first));
            for (var i = 1; i < 128; i++) Verified(await runtime.EditTextAsync(Edit(target, await Read(client, target), "select_range", 0, 0)));
            Assert.Equal("text_edit_ledger_full", (await runtime.EditTextAsync(Edit(target, await Read(client, target), "insert", 0, text: "x"))).Error!.Code);
            Assert.True(Verified(await runtime.EditTextAsync(first)).Replayed);
            AvaScopeBridge.Deactivate();
            Assert.Equal("text_edit_session_closed", (await runtime.EditTextAsync(first)).Error!.Code);
            var replacement = AvaScopeBridge.Activate();
            Assert.Equal("text_edit_session_mismatch", (await replacement.EditTextAsync(first)).Error!.Code);
        });
    }

    [Fact]
    public void ProtocolRejectsAmbiguousIndicesInvalidUnicodeAndUnpinnedChanges()
    {
        var target = new RuntimeTargetContext(new("session"), "top", "visual", "node", topLevelGeneration: "t", nodeGeneration: "n");
        var revision = new string('a', 64);
        Assert.Throws<ArgumentException>(() => new RuntimeTextEditRequest(target, "insert", "id", start: 0, text: "x"));
        Assert.Throws<ArgumentException>(() => new RuntimeTextEditRequest(target, "replace_range", "id", revision, 2, 1, "x"));
        Assert.Throws<ArgumentException>(() => new RuntimeTextEditRequest(target, "insert", "id", revision, 0, text: "\ud800"));
        Assert.Throws<ArgumentException>(() => new RuntimeTextEditRequest(target, "insert", "id", revision, 0, text: new string('x', 4097)));
        Assert.Throws<ArgumentException>(() => new RuntimeTextEditRequest(target, "read", text: "hidden-write"));
        Assert.Throws<ArgumentException>(() => new RuntimeTextEditRequest(target, "replace_selection", "id", revision, 0, text: "x"));
        Assert.False(BridgeIpcMethods.RequiresControl(new("read", BridgeIpcMethods.EditText, textEdit: new(target))));
        Assert.True(BridgeIpcMethods.RequiresControl(new("edit", BridgeIpcMethods.EditText, textEdit: new(target, "select_range", "id", revision, 0, 0))));
    }

    private static RuntimeTextEditRequest Edit(RuntimeTargetContext target, RuntimeTextState state, string action,
        int? start = null, int? end = null, string? text = null, RuntimeEvidencePolicy? policy = null)
        => new(target, action, Guid.NewGuid().ToString("N"), state.Revision, start, end, text, policy);

    private static async Task<RuntimeTextState> Read(LocalBridgeClient client, RuntimeTargetContext target) => Verified(await client.EditTextAsync(new(target))).After!;

    private static RuntimeTextEditResponse Verified(CoreResult<RuntimeTextEditResponse> result)
    {
        Assert.True(OperationResultMapper.ToToolResult(result).Success, JsonSerializer.Serialize(result));
        return result.Value!;
    }

    private static RuntimeTextEditResponse Rejected(CoreResult<RuntimeTextEditResponse> result, string code, int operations)
    {
        var tool = OperationResultMapper.ToToolResult(result);
        Assert.False(tool.Success); Assert.Equal(code, tool.Error!.Code); Assert.NotNull(tool.Value);
        Assert.Equal(operations, tool.Value.DispatchedOperations); return tool.Value;
    }

    private static async Task<RuntimeTargetContext> Target(AvaScopeBridgeRuntime runtime, string top, string name)
        => Assert.Single((await runtime.FindNodesAsync(top, TreeKinds.Visual, name: name, maxDepth: 32)).Value!.Matches).Target!;

    private static async Task WithWindow(Func<AvaScopeBridgeRuntime, StackPanel, string, LocalBridgeClient, Task> test)
    {
        var session = HeadlessUnitTestSession.StartNew(typeof(BridgeHeadlessSmokeTests.BridgeHeadlessTestApplication));
        try
        {
            await BridgeHeadlessSmokeTests.DispatchAsync(session, async () =>
            {
                AvaScopeBridge.Deactivate(); var runtime = AvaScopeBridge.Activate();
                var root = new StackPanel(); var window = new Window { Width = 500, Height = 600, Content = root };
                try
                {
                    window.Show(); using var registration = runtime.RegisterTopLevel(window); Dispatcher.UIThread.RunJobs();
                    await test(runtime, root, Assert.Single(await runtime.ListTopLevelsAsync()).Id, new(Path.GetDirectoryName(runtime.SessionManifestPath)!));
                }
                finally { window.Close(); AvaScopeBridge.Deactivate(); }
            }, CancellationToken.None);
        }
        finally { BridgeHeadlessSmokeTests.DisposeHeadlessSessionAfterExplicitCleanup(session); }
    }

    private sealed class ChangingTextBox : TextBox, IAvaScopeActionContextProvider
    {
        public bool ChangeOnContext { get; set; }
        public AvaScopeActionContext GetActionContext(string action)
        {
            if (ChangeOnContext) { ChangeOnContext = false; Text = "changed by app"; }
            return new();
        }
    }
}
