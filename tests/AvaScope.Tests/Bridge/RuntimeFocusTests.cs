using System.Diagnostics;
using System.Text.Json;
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
public sealed class RuntimeFocusTests
{
    [Fact]
    public async Task ReadOnlyFocusPredictsTabAndProbesObserveBothDirectionsThroughPublicInput()
    {
        await WithWindow(async (runtime, window, root, first, second, top, client) =>
        {
            var disabled = new TextBox { Name = "Disabled", IsEnabled = false };
            root.Children.Insert(1, disabled);
            window.UpdateLayout(); Assert.True(first.Focus());
            var keys = 0;
            first.AddHandler(InputElement.KeyDownEvent, (_, _) => keys++, RoutingStrategies.Tunnel);
            var inspect = await client.InspectFocusAsync(new(runtime.SessionId, top));
            Assert.True(inspect.Success, inspect.Error?.Message);
            var before = inspect.Value!;
            Assert.Equal("selected_window", before.FocusStatus);
            Assert.Equal("First", before.Focused!.Label);
            Assert.Equal("unsupported", before.NativeFocus.State);
            Assert.Contains(before.Candidates, candidate => candidate.Label == "Disabled" && candidate.Reasons.Contains("disabled"));
            Assert.Contains(before.ScopeAncestors, scope => scope.IsFocusScope);
            Assert.Equal(0, keys); Assert.True(first.IsFocused);
            var next = before.Predictions.Single(item => item.Direction == "next");
            Assert.Equal("unavailable_candidate", next.Status);
            Assert.Equal(before.Candidates.Single(candidate => candidate.Label == "Disabled").Target.NodeGeneration, next.Target!.NodeGeneration);
            var moved = await client.ProbeFocusAsync(new(before.Focused.Target, settleMs: 0));
            Assert.True(OperationResultMapper.IsSuccessful(moved), JsonSerializer.Serialize(moved));
            Assert.True(moved.Value!.StateChanging); Assert.False(moved.Value.Restored);
            Assert.True(moved.Value.FocusChanged); Assert.Equal("Second", moved.Value.After!.Focused!.Label);
            Assert.NotEqual(next.Target.NodeGeneration, moved.Value.After.Focused.Target.NodeGeneration);
            Assert.Equal(1, keys); Assert.True(second.IsFocused);
            root.Children.Remove(disabled);
            var reverse = (await client.InspectFocusAsync(new(runtime.SessionId, top))).Value!;
            Assert.Equal(before.Focused.Target.NodeGeneration, reverse.Predictions.Single(p => p.Direction == "previous").Target!.NodeGeneration);
            var previous = await client.ProbeFocusAsync(new(moved.Value.After.Focused.Target, "previous", settleMs: 0));
            Assert.Equal("observed", previous.Value!.Status); Assert.True(previous.Value.FocusChanged);
            Assert.True(first.IsFocused);
        });
    }

    [Fact]
    public async Task InterceptedTabIsObservedAsUnchangedAndMultiKeyInputStopsAfterNavigation()
    {
        await WithWindow(async (runtime, window, root, first, second, top, client) =>
        {
            Assert.True(first.Focus());
            var before = (await client.InspectFocusAsync(new(runtime.SessionId, top))).Value!;
            EventHandler<KeyEventArgs> intercept = (_, e) => { if (e.Key == Key.Tab) e.Handled = true; };
            first.AddHandler(InputElement.KeyDownEvent, intercept, RoutingStrategies.Tunnel);
            var probe = await client.ProbeFocusAsync(new(before.Focused!.Target, settleMs: 0));
            Assert.Equal("observed", probe.Value!.Status); Assert.False(probe.Value.FocusChanged);
            Assert.Equal("predicted", probe.Value.Before.Predictions.Single(p => p.Direction == "next").Status);
            first.RemoveHandler(InputElement.KeyDownEvent, intercept);
            var keys = new List<Key>();
            second.AddHandler(InputElement.KeyDownEvent, (_, e) => keys.Add(e.Key), RoutingStrategies.Tunnel);
            var sequence = await client.InputAsync(runtime.SessionId, top, InputActions.KeySequence,
                targetNodeId: before.Focused.Target.NodeId, inputTarget: before.Focused.Target,
                execution: new() { RequireCurrentFocus = true, IntervalMs = 0, Keys = [new("Tab"), new("Enter")] });
            Assert.False(sequence.Success); Assert.True(second.IsFocused);
            Assert.DoesNotContain(Key.Enter, keys);
            Assert.Equal("not_needed", sequence.Error!.Details!["cleanup"]);
        });
    }

    [Fact]
    public async Task PinnedProbeRejectsChangedFocusStaleNodesPoliciesAndForeignControlLeases()
    {
        await WithWindow(async (runtime, window, root, first, second, top, client) =>
        {
            Assert.True(first.Focus());
            var target = (await client.InspectFocusAsync(new(runtime.SessionId, top))).Value!.Focused!.Target;
            Assert.True(second.Focus());
            var wrongFocus = await client.ProbeFocusAsync(new(target));
            Assert.False(wrongFocus.Success); Assert.Equal("focus_probe_start_changed", wrongFocus.Error!.Code); Assert.True(second.IsFocused);
            var pinnedKeys = new InputExecutionOptions { RequireCurrentFocus = true, IntervalMs = 0, Keys = [new("Tab")] };
            var direct = await client.InputAsync(runtime.SessionId, top, InputActions.KeySequence, targetNodeId: target.NodeId,
                inputTarget: target, execution: pinnedKeys);
            Assert.False(direct.Success); Assert.True(second.IsFocused);
            var validation = await runtime.ValidateInputAsync(top, InputActions.KeySequence, targetNodeId: target.NodeId,
                inputTarget: target, execution: pinnedKeys);
            Assert.False(validation.Success); Assert.True(second.IsFocused);
            Assert.True(first.Focus());
            var policy = new RuntimeEvidencePolicy(Path.Combine(Path.GetTempPath(), "unused-focus-policy"), allowedActions: [SemanticWorkflowActions.Inspect]);
            Assert.False((await client.ProbeFocusAsync(new(target, policy: policy))).Success); Assert.True(first.IsFocused);
            var lease = await client.SessionControlAsync(runtime.SessionId, new("acquire", "focus-owner"));
            Assert.True(lease.Success);
            Assert.True((await client.InspectFocusAsync(new(runtime.SessionId, top))).Success);
            var outsider = new LocalBridgeClient(client.ManifestDirectory);
            Assert.Equal("session_control_conflict", (await outsider.ProbeFocusAsync(new(target))).Error!.Code);
            Assert.True((await client.SessionControlAsync(runtime.SessionId, new("release", Token: lease.Value!.Token))).Success);
            root.Children.Remove(first);
            Assert.False((await client.ProbeFocusAsync(new(target))).Success);
        });
    }

    [Fact]
    public async Task ModalScopesCustomNavigationAndPolicyExclusionsRemainExplicit()
    {
        await WithWindow(async (runtime, window, root, first, second, top, client) =>
        {
            var scope = new StackPanel();
            root.Children.Remove(first); root.Children.Remove(second);
            scope.Children.Add(first); scope.Children.Add(second); root.Children.Add(scope);
            KeyboardNavigation.SetTabNavigation(scope, KeyboardNavigationMode.Cycle);
            window.UpdateLayout(); Assert.True(first.Focus());
            var scoped = (await client.InspectFocusAsync(new(runtime.SessionId, top))).Value!;
            Assert.Contains(scoped.ScopeAncestors, ancestor => ancestor.TabNavigation == "Cycle");
            Assert.Contains(scoped.ScopeAncestors, ancestor => ancestor.IsFocusScope);
            var custom = new CustomPanel(); root.Children.Add(custom);
            var uncertain = (await client.InspectFocusAsync(new(runtime.SessionId, top))).Value!;
            Assert.All(uncertain.Predictions, prediction => Assert.Equal("custom_navigation_requires_probe", prediction.Detail));
            Assert.Equal(0, custom.Calls);
            root.Children.Remove(custom);
            var dialogField = new TextBox { Name = "DialogField" };
            var dialog = new Window { Content = dialogField, Width = 200, Height = 100 };
            var closed = dialog.ShowDialog(window);
            using var registration = runtime.RegisterTopLevel(dialog);
            try
            {
                Assert.True(dialogField.Focus());
                var modal = (await client.InspectFocusAsync(new(runtime.SessionId, top))).Value!;
                Assert.Equal("other_registered_window", modal.FocusStatus);
                Assert.NotNull(modal.ModalBlocker);
                Assert.Equal(modal.ModalBlocker.TopLevelId, modal.Focused!.Target.TopLevelId);
            }
            finally { dialog.Close(); await closed; }
            AutomationProperties.SetAutomationId(first, "private-focus-canary");
            Assert.True(first.Focus());
            var policy = new RuntimeEvidencePolicy(Path.Combine(Path.GetTempPath(), "unused-focus-policy"), redactedAutomationIds: ["private-focus-canary"]);
            var hidden = await client.InspectFocusAsync(new(runtime.SessionId, top, policy: policy));
            Assert.Equal("excluded_by_policy", hidden.Value!.FocusStatus); Assert.Null(hidden.Value.Focused);
            Assert.DoesNotContain("First", JsonSerializer.Serialize(hidden));
            var limited = (await client.InspectFocusAsync(new(runtime.SessionId, top, maxNodes: 1))).Value!;
            Assert.Contains("node_limit", limited.CoverageReasons); Assert.All(limited.Predictions, p => Assert.Equal("unknown", p.Status));
        });
    }

    [Fact]
    public async Task ActualCliInspectionAndMcpProbePreserveFocusEvidenceAndToolSemantics()
    {
        await WithWindow(async (runtime, window, root, first, second, top, client) =>
        {
            Assert.True(first.Focus());
            var directory = Path.Combine(Path.GetTempPath(), "AvaScope.Tests", "focus-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            try
            {
                var path = Path.Combine(directory, "request.json");
                await File.WriteAllTextAsync(path, JsonSerializer.Serialize(new RuntimeFocusInspectionRequest(runtime.SessionId, top)));
                var start = new ProcessStartInfo("dotnet") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
                foreach (var argument in new[] { Path.Combine(AppContext.BaseDirectory, "avascope.dll"), "inspect-focus", "--request", path, "--manifest-dir", client.ManifestDirectory }) start.ArgumentList.Add(argument);
                using var process = Process.Start(start)!;
                var output = process.StandardOutput.ReadToEndAsync(); var errors = process.StandardError.ReadToEndAsync();
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(40));
                try { await process.WaitForExitAsync(timeout.Token); }
                finally { if (!process.HasExited) process.Kill(entireProcessTree: true); }
                Assert.Equal(0, process.ExitCode);
                var cli = JsonSerializer.Deserialize<ToolResult<RuntimeFocusSnapshot>>(await output)!;
                Assert.True(cli.Success, cli.Error?.Message + await errors); Assert.True(first.IsFocused);
                var environment = TestEnvironment.McpEnvironment();
                if (Environment.GetEnvironmentVariable("TMPDIR") is { } temporary) environment["TMPDIR"] = temporary;
                await using var mcp = await McpClient.CreateAsync(new StdioClientTransport(new StdioClientTransportOptions
                {
                    Name = "Focus probe", Command = "dotnet", Arguments = [Path.Combine(AppContext.BaseDirectory, "AvaScope.Mcp.dll")],
                    InheritEnvironmentVariables = false, EnvironmentVariables = environment, ShutdownTimeout = TimeSpan.FromSeconds(3)
                }), cancellationToken: timeout.Token);
                var call = await mcp.CallToolAsync("probe_focus", new Dictionary<string, object?>
                { ["request"] = JsonSerializer.SerializeToElement(new RuntimeFocusProbeRequest(cli.Value!.Focused!.Target)), ["manifestDirectory"] = client.ManifestDirectory }, cancellationToken: timeout.Token);
                var probe = JsonSerializer.Deserialize<ToolResult<RuntimeFocusProbeResponse>>(JsonSerializer.Serialize(call.StructuredContent))!;
                Assert.True(probe.Success, JsonSerializer.Serialize(probe)); Assert.True(probe.Value!.FocusChanged); Assert.True(second.IsFocused);
            }
            finally { Directory.Delete(directory, true); }
        });
    }

    private static async Task WithWindow(Func<AvaScopeBridgeRuntime, Window, StackPanel, TextBox, TextBox, string, LocalBridgeClient, Task> test)
    {
        var session = HeadlessUnitTestSession.StartNew(typeof(BridgeHeadlessSmokeTests.BridgeHeadlessTestApplication));
        try
        {
            await BridgeHeadlessSmokeTests.DispatchAsync(session, async () =>
            {
                AvaScopeBridge.Deactivate(); var runtime = AvaScopeBridge.Activate();
                var first = new TextBox { Name = "First" }; var second = new TextBox { Name = "Second" };
                var root = new StackPanel { Children = { first, second } };
                var window = new Window { Width = 400, Height = 240, Content = root };
                try
                {
                    window.Show(); using var registration = runtime.RegisterTopLevel(window);
                    var top = Assert.Single(await runtime.ListTopLevelsAsync());
                    using var preparedFrame = window.CaptureRenderedFrame();
                    Assert.NotNull(preparedFrame);
                    Assert.Equal(new PixelSize(400, 240), preparedFrame.PixelSize);
                    Assert.True((await runtime.ReadinessAsync(top.Id, options: new(waitForFrame: true))).Success);
                    await test(runtime, window, root, first, second, top.Id, new(Path.GetDirectoryName(runtime.SessionManifestPath)!));
                }
                finally { window.Close(); AvaScopeBridge.Deactivate(); }
            }, CancellationToken.None);
        }
        finally { BridgeHeadlessSmokeTests.DisposeHeadlessSessionAfterExplicitCleanup(session); }
    }

    private sealed class CustomPanel : StackPanel, ICustomKeyboardNavigation
    {
        public int Calls { get; private set; }
        public (bool handled, IInputElement? next) GetNext(IInputElement element, NavigationDirection direction)
        { Calls++; return (false, null); }
    }
}
