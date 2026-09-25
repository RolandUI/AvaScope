using System.Diagnostics;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using AvaScope.Bridge;
using AvaScope.Core;
using AvaScope.Protocol;
using ModelContextProtocol.Client;

namespace AvaScope.Tests.Bridge;

[Collection(BridgeCollectionDefinition.Name)]
public sealed class RuntimeDesiredStateTests
{
    [Fact]
    public async Task CompleteQueryTargetsWorkAndIncompleteCoverageIsDistinctFromStaleIdentity()
    {
        await WithWindow(async (runtime, root, top, client) =>
        {
            var check = new CheckBox { Name = "Check", Content = "Check" };
            var editor = new TextBox { Name = "Editor", Text = "unchanged" };
            root.Children.Add(check); root.Children.Add(editor);
            var calls = 0; check.Click += (_, _) => calls++;
            ((Window)TopLevel.GetTopLevel(root)!).UpdateLayout();
            Assert.True((await runtime.ReadinessAsync(top, options: new(waitForFrame: true))).Success);
            var found = await client.QueryNodesAsync(new(runtime.SessionId, top,
                new(name: "Check", rendered: true, enabled: true), maxDepth: 32));
            Assert.True(found.Value!.Coverage!.Complete);
            var match = Assert.Single(found.Value.Matches);
            var partial = await client.QueryNodesAsync(new(runtime.SessionId, top,
                new(name: "Check", rendered: true, enabled: true), maxDepth: match.Path.Count - 1));
            Assert.False(partial.Value!.Coverage!.Complete);
            Assert.Contains("depth_limit", partial.Value.Coverage.Reasons);
            var partialTarget = Assert.Single(partial.Value.Matches).Target!;
            var refused = await client.EnsureStateAsync(Request(partialTarget, "checked", true));
            Assert.Equal("runtime_input_selection_incomplete", refused.Error!.Code);
            Assert.Equal("coverage_incomplete", refused.Error.Details!["selectionFailure"]);
            Assert.Contains("depth_limit", refused.Error.Details["coverageReasons"]);
            Assert.Contains("maxDepth", refused.Error.Details["nextAction"]);
            Assert.Equal("false", refused.Error.Details["dispatched"]);
            Assert.False(check.IsChecked); Assert.Equal(0, calls);
            var inspection = await client.InspectNodeAsync(runtime.SessionId, top, TreeKinds.Visual,
                partialTarget.NodeId!, target: partialTarget);
            Assert.Equal("runtime_input_selection_incomplete", inspection.Error!.Code);
            var editorQuery = await client.QueryNodesAsync(new(runtime.SessionId, top, new(name: "Editor"), maxDepth: 32));
            var editorMatch = Assert.Single(editorQuery.Value!.Matches);
            var partialEditor = await client.QueryNodesAsync(new(runtime.SessionId, top, new(name: "Editor"), maxDepth: editorMatch.Path.Count - 1));
            var unreadable = await client.EditTextAsync(new(Assert.Single(partialEditor.Value!.Matches).Target!));
            Assert.Equal("runtime_input_selection_incomplete", unreadable.Error!.Code);
            var readable = await client.EditTextAsync(new(editorMatch.Target!));
            Assert.True(readable.Success, readable.Error?.Message); Assert.Equal("unchanged", readable.Value!.After!.Text);

            var request = Request(match.Target!, "checked", true);
            var changed = await client.EnsureStateAsync(request);
            Assert.True(changed.Value!.Verified); Assert.Equal(1, changed.Value.DispatchedOperations);
            Assert.True(check.IsChecked); Assert.Equal(1, calls);
            var replay = await client.EnsureStateAsync(request);
            Assert.True(replay.Value!.Replayed); Assert.Equal(1, calls);
            var satisfied = await client.EnsureStateAsync(Request(match.Target!, "checked", true));
            Assert.Equal("already_satisfied", satisfied.Value!.Status); Assert.Equal(0, satisfied.Value.DispatchedOperations);
            var denied = await client.EnsureStateAsync(new(match.Target!, "checked", JsonSerializer.SerializeToElement(false),
                "query-policy-denied", new(Path.GetTempPath(), allowedDesiredStates: [])));
            Assert.Equal("desired_state_policy_denied", denied.Error!.Code); Assert.Equal(1, calls);
            var duplicates = new[] { new CheckBox { Name = "Check" }, new CheckBox { Name = "Check" } };
            foreach (var duplicate in duplicates) root.Children.Add(duplicate);
            ((Window)TopLevel.GetTopLevel(root)!).UpdateLayout();
            Assert.True((await runtime.ReadinessAsync(top, options: new(waitForFrame: true))).Success);
            var ambiguous = await client.EnsureStateAsync(Request(match.Target!, "checked", false));
            Assert.Equal(RuntimeInputErrorCodes.TargetStale, ambiguous.Error!.Code); Assert.Equal(1, calls);
            foreach (var duplicate in duplicates) root.Children.Remove(duplicate);
            check.DataContext = new object();
            var stale = await client.EnsureStateAsync(Request(match.Target!, "checked", false));
            Assert.Equal(RuntimeInputErrorCodes.TargetStale, stale.Error!.Code);
            Assert.Equal("false", stale.Error.Details!["dispatched"]); Assert.Equal(1, calls);
            root.Children.Remove(check);
            root.Children.Add(new CheckBox { Name = "Check", Content = "Replacement" });
            var replaced = await client.EnsureStateAsync(Request(match.Target!, "checked", false));
            Assert.Equal(RuntimeInputErrorCodes.TargetStale, replaced.Error!.Code); Assert.Equal(1, calls);
        });
    }

    [Fact]
    public async Task CheckedAndExpandedRequestsSkipSatisfiedStateAndBoundTriStateCycles()
    {
        await WithWindow(async (runtime, root, top, client) =>
        {
            var check = new CheckBox { Name = "Check", IsThreeState = true, IsChecked = false };
            var calls = 0;
            check.Click += (_, _) => calls++;
            var folder = new Expander { Name = "Folder", Header = "Folder", Content = new TextBlock { Text = "Child" } };
            root.Children.Add(check); root.Children.Add(folder);
            Dispatcher.UIThread.RunJobs();
            var target = await Target(runtime, top, "Check");
            var unchanged = await client.EnsureStateAsync(Request(target, "checked", false));
            Assert.Equal("already_satisfied", unchanged.Value!.Status); Assert.Equal(0, calls);
            var changed = await client.EnsureStateAsync(Request(target, "checked", true));
            Assert.True(changed.Value!.Verified); Assert.Equal(1, calls);
            Assert.Equal(RuntimeOperationRoutes.AutomationProvider, changed.Value.Provenance.Route);
            Assert.False(changed.Value.Before.Value!.Value.GetBoolean()); Assert.True(changed.Value.After.Value!.Value.GetBoolean());
            Assert.True((await client.EnsureStateAsync(Request(target, "checked", true))).Value!.Verified); Assert.Equal(1, calls);
            var indeterminate = await client.EnsureStateAsync(Request(target, "checked", null));
            Assert.True(indeterminate.Value!.Verified); Assert.Null(check.IsChecked);
            Assert.Equal("indeterminate", indeterminate.Value.After.Status);
            Assert.True((await client.EnsureStateAsync(Request(target, "checked", false))).Value!.Verified); Assert.False(check.IsChecked);
            check.IsThreeState = false;
            var unsupported = await client.EnsureStateAsync(Request(target, "checked", null));
            Assert.False(unsupported.Value!.Verified); Assert.InRange(unsupported.Value.DispatchedOperations, 1, 3);
            var folderTarget = await Target(runtime, top, "Folder");
            var expansion = await client.EnsureStateAsync(Request(folderTarget, "expanded", true));
            Assert.True(expansion.Value!.Verified, JsonSerializer.Serialize(expansion));
            Assert.True(folder.IsExpanded);
            Assert.Equal(0, (await client.EnsureStateAsync(Request(folderTarget, "expanded", true))).Value!.DispatchedOperations);
            Assert.True((await client.EnsureStateAsync(Request(folderTarget, "expanded", false))).Value!.Verified);
        });
    }

    [Fact]
    public async Task NumericConstraintsAndTextInputValidationRemainObservable()
    {
        await WithWindow(async (runtime, root, top, client) =>
        {
            var slider = new Slider { Name = "Range", Minimum = 0, Maximum = 100, Value = 10 };
            var box = new TextBox { Name = "Text", Text = "before", MaxLength = 6 };
            root.Children.Add(slider); root.Children.Add(box); Dispatcher.UIThread.RunJobs();
            var range = await Target(runtime, top, "Range");
            var changed = await client.EnsureStateAsync(Request(range, "value", 25));
            Assert.True(changed.Value!.Verified); Assert.Equal(25, slider.Value);
            Assert.Equal(0, (await client.EnsureStateAsync(Request(range, "value", 25))).Value!.DispatchedOperations);
            var outside = await client.EnsureStateAsync(Request(range, "value", 500));
            Assert.False(outside.Value!.Verified); Assert.Equal(0, outside.Value.DispatchedOperations); Assert.Equal(25, slider.Value);
            var target = await Target(runtime, top, "Text");
            var inputEvents = 0;
            box.AddHandler(InputElement.TextInputEvent, (_, _) => inputEvents++, Avalonia.Interactivity.RoutingStrategies.Tunnel);
            var text = await client.EnsureStateAsync(Request(target, "text", "after"));
            Assert.True(text.Value!.Verified); Assert.Equal("after", box.Text); Assert.Equal(1, inputEvents);
            Assert.Equal(RuntimeOperationRoutes.RoutedEvent, text.Value.Provenance.Route);
            Assert.Equal(0, (await client.EnsureStateAsync(Request(target, "text", "after"))).Value!.DispatchedOperations);
            Assert.Equal(1, inputEvents);
            var constrained = await client.EnsureStateAsync(Request(target, "text", "longer-than-six"));
            Assert.False(constrained.Value!.Verified); Assert.True(box.Text!.Length <= 6);
            Assert.False(OperationResultMapper.ToToolResult(constrained).Success);
            var empty = await client.EnsureStateAsync(Request(target, "text", ""));
            Assert.True(empty.Value!.Verified, JsonSerializer.Serialize(empty)); Assert.Equal("", box.Text);
            box.AddHandler(InputElement.TextInputEvent, (_, args) => args.Handled = true, Avalonia.Interactivity.RoutingStrategies.Tunnel);
            var rejected = await client.EnsureStateAsync(Request(target, "text", "denied"));
            Assert.False(rejected.Value!.Verified); Assert.Equal("", box.Text);
            DataValidationErrors.SetErrors(box, [new InvalidOperationException("app-validation")]);
            var invalid = await client.EnsureStateAsync(Request(target, "text", ""));
            Assert.False(invalid.Value!.Verified); Assert.Equal(0, invalid.Value.DispatchedOperations);
            Assert.Contains(invalid.Value.Diagnostics, diagnostic => diagnostic.Code == "desired_state_validation_errors");
        });
    }

    [Fact]
    public async Task SelectionChangesOnlyMissingItemsAndReportsConcurrentPartialChanges()
    {
        await WithWindow(async (runtime, root, top, client) =>
        {
            var a = new ListBoxItem { Name = "A", Content = "A" };
            var b = new ListBoxItem { Name = "B", Content = "B" };
            var c = new ListBoxItem { Name = "C", Content = "C" };
            var list = new ListBox { Name = "Items", SelectionMode = SelectionMode.Multiple, Height = 150, Items = { a, b, c } };
            root.Children.Add(list); Dispatcher.UIThread.RunJobs();
            var target = await Target(runtime, top, "Items");
            var at = await Target(runtime, top, "A"); var bt = await Target(runtime, top, "B");
            var changes = 0; list.SelectionChanged += (_, _) => changes++;
            var select = await client.EnsureStateAsync(Request(target, "selection", new[] { at, bt }));
            Assert.True(select.Value!.Verified, JsonSerializer.Serialize(select)); Assert.True(a.IsSelected); Assert.True(b.IsSelected);
            var originalChanges = changes;
            var again = await client.EnsureStateAsync(Request(target, "selection", new[] { bt, at }));
            Assert.Equal("already_satisfied", again.Value!.Status); Assert.Equal(originalChanges, changes);
            var remove = await client.EnsureStateAsync(Request(target, "selection", new[] { bt }));
            Assert.True(remove.Value!.Verified); Assert.Equal(1, remove.Value.DispatchedOperations); Assert.False(a.IsSelected); Assert.True(b.IsSelected);
            Assert.True((await client.EnsureStateAsync(Request(target, "selection", Array.Empty<RuntimeTargetContext>()))).Value!.Verified);
            var interfere = true;
            list.SelectionChanged += (_, _) => { if (interfere && a.IsSelected) { interfere = false; c.IsSelected = true; } };
            var partial = await client.EnsureStateAsync(Request(target, "selection", new[] { at, bt }));
            Assert.False(partial.Value!.Verified); Assert.Equal("partial", partial.Value.Status);
            Assert.Equal(1, partial.Value.DispatchedOperations); Assert.False(b.IsSelected); Assert.True(c.IsSelected);
        });
    }

    [Fact]
    public async Task UnrealizedSelectionAndChangesDuringActionPreparationFailWithoutDispatch()
    {
        await WithWindow(async (runtime, root, top, client) =>
        {
            var list = new ListBox { Name = "Virtual", Height = 80, AutoScrollToSelectedItem = false,
                ItemsSource = Enumerable.Range(0, 200).Select(index => "Row " + index).ToArray(), SelectionMode = SelectionMode.Multiple };
            var check = new ChangingToggle { Name = "Changing" };
            root.Children.Add(list); root.Children.Add(check); Dispatcher.UIThread.RunJobs();
            list.SelectedIndex = 199;
            Assert.Null(list.ContainerFromIndex(199));
            var incomplete = await client.EnsureStateAsync(Request(await Target(runtime, top, "Virtual"), "selection", Array.Empty<RuntimeTargetContext>()));
            Assert.False(incomplete.Value!.Verified); Assert.Equal(0, incomplete.Value.DispatchedOperations); Assert.Equal(199, list.SelectedIndex);
            Assert.Contains(incomplete.Value.Diagnostics, diagnostic => diagnostic.Code == "desired_state_selection_incomplete");
            var target = await Target(runtime, top, "Changing");
            check.ChangeOnRead = true;
            var changed = await client.EnsureStateAsync(Request(target, "checked", true));
            Assert.False(changed.Value!.Verified); Assert.Equal(0, changed.Value.DispatchedOperations); Assert.True(check.IsChecked);
            Assert.Contains(changed.Value.Diagnostics, diagnostic => diagnostic.Code == "desired_state_changed");
        });
    }

    [Fact]
    public async Task FocusRecyclingAndProviderFailuresNeverAuthorizeAutomaticReplay()
    {
        await WithWindow(async (runtime, root, top, client) =>
        {
            var box = new TextBox { Name = "Text", Text = "original", DataContext = new object() };
            var other = new TextBox { Name = "Other" };
            var broken = new ThrowingToggle { Name = "Broken" };
            root.Children.Add(box); root.Children.Add(other); root.Children.Add(broken); Dispatcher.UIThread.RunJobs();
            other.Focus();
            var target = await Target(runtime, top, "Text");
            box.GotFocus += (_, _) => box.DataContext = new object();
            var recycled = await client.EnsureStateAsync(Request(target, "text", "new"));
            Assert.Equal("uncertain", recycled.Value!.Status); Assert.True(recycled.Value.PreparationPerformed);
            Assert.Equal(0, recycled.Value.DispatchedOperations); Assert.Equal("original", box.Text);
            var request = Request(await Target(runtime, top, "Broken"), "checked", true);
            var failed = await client.EnsureStateAsync(request);
            Assert.Equal("uncertain", failed.Value!.Status); Assert.Equal(1, broken.Calls);
            var replayed = await new LocalBridgeClient(client.ManifestDirectory).EnsureStateAsync(request);
            Assert.True(replayed.Value!.Replayed); Assert.Equal(1, broken.Calls); Assert.Equal(failed.Value.ObservedAt, replayed.Value.ObservedAt);
            var conflict = await client.EnsureStateAsync(new(request.Target, "checked", JsonSerializer.SerializeToElement(false), request.RequestId));
            Assert.Equal("desired_state_id_conflict", conflict.Error!.Code); Assert.Equal(1, broken.Calls);
        });
    }

    [Fact]
    public async Task ADiscardedPipeResponseCanBeRetrievedWithoutRepeatingTheToggle()
    {
        await WithWindow(async (runtime, root, top, client) =>
        {
            var check = new CheckBox { Name = "Check" }; root.Children.Add(check); Dispatcher.UIThread.RunJobs();
            var calls = 0;
            var dispatched = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            check.Click += (_, _) => { calls++; dispatched.TrySetResult(); };
            var request = Request(await Target(runtime, top, "Check"), "checked", true);
            await using (var pipe = new NamedPipeClientStream(".", runtime.LocalPipeName!, PipeDirection.InOut, PipeOptions.Asynchronous))
            {
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                await pipe.ConnectAsync(timeout.Token);
                await pipe.WriteAsync(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new BridgeIpcRequest("discard-response", BridgeIpcMethods.EnsureState, desiredState: request)) + "\n"), timeout.Token);
                await pipe.FlushAsync(timeout.Token);
                await dispatched.Task.WaitAsync(timeout.Token);
                // The caller loses/discards the response and reconnects with the exact same intent.
            }
            var recovered = await client.EnsureStateAsync(request);
            Assert.True(recovered.Value!.Replayed); Assert.True(recovered.Value.Verified); Assert.Equal(1, calls);
            var noOp = await client.EnsureStateAsync(Request(request.Target, "checked", true));
            Assert.Equal("already_satisfied", noOp.Value!.Status); Assert.Equal(1, calls);
        });
    }

    [Fact]
    public async Task ClientTimeoutReportsUncertaintyAndKeepsTheOriginalRequestReplayable()
    {
        await WithWindow(async (runtime, root, top, client) =>
        {
            using var release = new ManualResetEventSlim();
            var slow = new BlockingToggle(release) { Name = "Slow" }; root.Children.Add(slow); Dispatcher.UIThread.RunJobs();
            var request = Request(await Target(runtime, top, "Slow"), "checked", true);
            var lost = await Task.Run(async () =>
            {
                try { return await new LocalBridgeClient(client.ManifestDirectory, TimeSpan.FromSeconds(1)).EnsureStateAsync(request); }
                finally { release.Set(); }
            });
            Assert.False(lost.Success); Assert.Equal("unknown", lost.Error!.Details!["dispatched"]);
            Assert.Equal(request.RequestId, lost.Error.Details["desiredStateRequestId"]);
            Assert.Equal(1, slow.Calls);
            var replayed = await client.EnsureStateAsync(request);
            Assert.True(replayed.Value!.Replayed); Assert.Equal(1, slow.Calls);
        });
    }

    [Fact]
    public async Task CooperativeBudgetAfterTextInputRetainsUncertaintyAndNeverRepeatsTheEdit()
    {
        await WithWindow(async (runtime, root, top, client) =>
        {
            var box = new TextBox { Name = "SlowText", Text = "seed" };
            root.Children.Add(box); Dispatcher.UIThread.RunJobs();
            var request = Request(await Target(runtime, top, "SlowText"), "text", "Changed — ő 日本語 😀");
            var calls = 0;
            var changes = 0;
            box.PropertyChanged += (_, change) => { if (change.Property == TextBox.TextProperty) changes++; };
            box.AddHandler(InputElement.TextInputEvent, (_, _) =>
            {
                calls++;
                // Deliberately slow application callback crosses the real cooperative budget.
                // It returns normally after the edit; it is not an IPC timeout or an encoding fault.
                Thread.Sleep(TimeSpan.FromMilliseconds(2100));
            }, Avalonia.Interactivity.RoutingStrategies.Bubble, handledEventsToo: true);

            var result = await runtime.EnsureStateAsync(request);
            Assert.False(OperationResultMapper.ToToolResult(result).Success);
            Assert.Equal("uncertain", result.Value!.Status); Assert.False(result.Value.Verified);
            Assert.Contains(result.Value.Diagnostics, diagnostic => diagnostic.Code == "desired_state_budget");
            Assert.Equal(1, result.Value.DispatchedOperations); Assert.True(result.Value.PreparationPerformed);
            Assert.Equal("unavailable", result.Value.After.Status);
            Assert.Equal(request.Desired.GetString(), box.Text); Assert.Equal(1, calls); Assert.Equal(1, changes);

            var recovered = await new LocalBridgeClient(client.ManifestDirectory).EnsureStateAsync(request);
            Assert.True(recovered.Value!.Replayed); Assert.False(recovered.Value.Verified);
            Assert.Equal(result.Value.ObservedAt, recovered.Value.ObservedAt);
            Assert.Equal("uncertain", recovered.Value.Status);
            Assert.Equal(request.Desired.GetString(), box.Text); Assert.Equal(1, calls); Assert.Equal(1, changes);
            var conflict = await client.EnsureStateAsync(new(request.Target, "text", JsonSerializer.SerializeToElement("different"), request.RequestId));
            Assert.Equal("desired_state_id_conflict", conflict.Error!.Code);
            Assert.Equal(1, calls); Assert.Equal(1, changes);
        });
    }

    [Fact]
    public async Task CliAndMcpSharePolicyRedactionReplayAndOperationFailureSemantics()
    {
        await WithWindow(async (runtime, root, top, client) =>
        {
            var box = new TextBox { Name = "Text" }; root.Children.Add(box); Dispatcher.UIThread.RunJobs();
            var target = await Target(runtime, top, "Text");
            var policy = new RuntimeEvidencePolicy(Path.Combine(Path.GetTempPath(), "avascope-desired-state-tests"),
                redactedText: ["desired-private-value"], authorizedSessionIds: [runtime.SessionId.Value], allowedDesiredStates: ["text"]);
            var request = new RuntimeDesiredStateRequest(target, "text", JsonSerializer.SerializeToElement("desired-private-value"), "cross-adapter-request", policy);
            var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".json");
            try
            {
                await File.WriteAllTextAsync(path, JsonSerializer.Serialize(request));
                var start = new ProcessStartInfo("dotnet") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
                foreach (var arg in new[] { Path.Combine(AppContext.BaseDirectory, "avascope.dll"), "ensure-state", "--request", path, "--manifest-dir", client.ManifestDirectory }) start.ArgumentList.Add(arg);
                using var process = Process.Start(start)!;
                var output = process.StandardOutput.ReadToEndAsync(); var error = process.StandardError.ReadToEndAsync();
                await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(20));
                Assert.Equal(0, process.ExitCode);
                var cli = JsonSerializer.Deserialize<ToolResult<RuntimeDesiredStateResponse>>(await output)!;
                Assert.True(cli.Success, cli.Error?.Message + await error); Assert.DoesNotContain("desired-private-value", await output);
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
                await using var mcp = await McpClient.CreateAsync(new StdioClientTransport(new()
                { Command = "dotnet", Arguments = [Path.Combine(AppContext.BaseDirectory, "AvaScope.Mcp.dll")], Name = "desired-state-test" }), cancellationToken: timeout.Token);
                var call = await mcp.CallToolAsync("ensure_state", new Dictionary<string, object?>
                { ["request"] = JsonSerializer.SerializeToElement(request), ["manifestDirectory"] = client.ManifestDirectory }, cancellationToken: timeout.Token);
                var result = JsonSerializer.Deserialize<ToolResult<RuntimeDesiredStateResponse>>(JsonSerializer.Serialize(call.StructuredContent))!;
                Assert.True(result.Success); Assert.True(result.Value!.Replayed);
                Assert.DoesNotContain("desired-private-value", JsonSerializer.Serialize(result));
                box.IsReadOnly = true;
                var denied = Request(target, "text", "different");
                call = await mcp.CallToolAsync("ensure_state", new Dictionary<string, object?>
                { ["request"] = JsonSerializer.SerializeToElement(denied), ["manifestDirectory"] = client.ManifestDirectory }, cancellationToken: timeout.Token);
                var failed = JsonSerializer.Deserialize<ToolResult<RuntimeDesiredStateResponse>>(JsonSerializer.Serialize(call.StructuredContent))!;
                Assert.False(failed.Success); Assert.NotNull(failed.Value); Assert.Equal(0, failed.Value.DispatchedOperations);
                var readOnlyPolicy = new RuntimeEvidencePolicy(policy.OwnedEvidenceRoot, authorizedSessionIds: [runtime.SessionId.Value]);
                Assert.Equal("desired_state_policy_denied", (await client.EnsureStateAsync(new(target, "text", JsonSerializer.SerializeToElement("denied"), "policy-denied", readOnlyPolicy))).Error!.Code);
            }
            finally { File.Delete(path); }
        });
    }

    [Fact]
    public async Task FullLedgerRejectsNewIntentsWithoutEvictingPreviousResults()
    {
        await WithWindow(async (runtime, root, top, _) =>
        {
            var check = new CheckBox { Name = "Check" }; root.Children.Add(check); Dispatcher.UIThread.RunJobs();
            var target = await Target(runtime, top, "Check");
            var first = Request(target, "checked", false);
            Assert.True((await runtime.EnsureStateAsync(first)).Value!.Verified);
            for (var index = 1; index < 512; index++)
                Assert.True((await runtime.EnsureStateAsync(Request(target, "checked", false))).Value!.Verified);
            Assert.Equal("desired_state_ledger_full", (await runtime.EnsureStateAsync(Request(target, "checked", true))).Error!.Code);
            var replayed = await runtime.EnsureStateAsync(first);
            Assert.True(replayed.Value!.Replayed); Assert.False(check.IsChecked);
        });
    }

    [Fact]
    public void RequestsRequireTypedBoundedValuesAndFreshSameWindowTargets()
    {
        var target = new RuntimeTargetContext(new("s"), "top", "visual", "node", topLevelGeneration: "top-generation", nodeGeneration: "node-generation");
        Assert.Throws<ArgumentException>(() => Request(target, "checked", "true"));
        Assert.Throws<ArgumentException>(() => Request(target, "text", new string('x', 4097)));
        Assert.Throws<ArgumentException>(() => Request(target, "arbitraryProperty", true));
        Assert.Throws<ArgumentException>(() => Request(new(new("s"), "top", "visual", "node"), "checked", true));
        Assert.Throws<ArgumentException>(() => Request(target, "selection", new[] { target, target }));
        var other = new RuntimeTargetContext(new("s"), "other", "visual", "node", topLevelGeneration: "other", nodeGeneration: "node");
        Assert.Throws<ArgumentException>(() => Request(target, "selection", new[] { other }));
        Assert.True(BridgeIpcMethods.RequiresControl(new("request", BridgeIpcMethods.EnsureState)));
    }

    private static RuntimeDesiredStateRequest Request(RuntimeTargetContext target, string property, object? desired) =>
        new(target, property, JsonSerializer.SerializeToElement(desired), Guid.NewGuid().ToString("N"));

    private static async Task<RuntimeTargetContext> Target(AvaScopeBridgeRuntime runtime, string top, string name) =>
        Assert.Single((await runtime.FindNodesAsync(top, TreeKinds.Visual, name: name, maxDepth: 32)).Value!.Matches).Target!;

    private static async Task WithWindow(Func<AvaScopeBridgeRuntime, StackPanel, string, LocalBridgeClient, Task> test)
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
                    window.Show(); using var registration = runtime.RegisterTopLevel(window); Dispatcher.UIThread.RunJobs();
                    await test(runtime, root, Assert.Single(await runtime.ListTopLevelsAsync()).Id, new(Path.GetDirectoryName(runtime.SessionManifestPath)!));
                }
                finally { window.Close(); AvaScopeBridge.Deactivate(); }
            }, CancellationToken.None);
        }
        finally { BridgeHeadlessSmokeTests.DisposeHeadlessSessionAfterExplicitCleanup(session); }
    }

    private sealed class ThrowingToggle : Control
    {
        public int Calls { get; set; }
        protected override AutomationPeer OnCreateAutomationPeer() => new Peer(this);
        private sealed class Peer(ThrowingToggle owner) : ControlAutomationPeer(owner), IToggleProvider
        {
            public ToggleState ToggleState => owner.Calls > 0 ? ToggleState.On : ToggleState.Off;
            public void Toggle() { owner.Calls++; throw new InvalidOperationException("response lost after side effect"); }
        }
    }

    private sealed class ChangingToggle : CheckBox, IAvaScopeActionContextProvider
    {
        public bool ChangeOnRead { get; set; }
        public AvaScopeActionContext GetActionContext(string action)
        {
            if (ChangeOnRead) { ChangeOnRead = false; IsChecked = true; }
            return new();
        }
    }

    private sealed class BlockingToggle(ManualResetEventSlim release) : Control
    {
        public int Calls { get; set; }
        protected override AutomationPeer OnCreateAutomationPeer() => new Peer(this, release);
        private sealed class Peer(BlockingToggle owner, ManualResetEventSlim release) : ControlAutomationPeer(owner), IToggleProvider
        {
            public ToggleState ToggleState => owner.Calls > 0 ? ToggleState.On : ToggleState.Off;
            public void Toggle() { owner.Calls++; if (!release.Wait(TimeSpan.FromSeconds(5))) throw new TimeoutException(); }
        }
    }
}
