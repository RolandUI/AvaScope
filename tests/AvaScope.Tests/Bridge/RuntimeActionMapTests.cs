using System.Diagnostics;
using System.Text.Json;
using System.Windows.Input;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using AvaScope.Bridge;
using AvaScope.Core;
using AvaScope.Protocol;
using ModelContextProtocol.Client;

namespace AvaScope.Tests.Bridge;

[Collection(BridgeCollectionDefinition.Name)]
public sealed class RuntimeActionMapTests
{
    [Fact]
    public async Task ClosedNestedMenusExposeRoutesAndDuplicateLabelsWithoutOpeningOrExecuting()
    {
        await WithWindow(async (runtime, window, root, top, client) =>
        {
            var command = new Command();
            var csv = new MenuItem { Header = "CSV", Command = command, InputGesture = new(Key.E, KeyModifiers.Control) };
            var export = new MenuItem { Header = "Export", Items = { csv } };
            var file = new MenuItem { Header = "File", Items = { export } };
            var opened = 0;
            file.SubmenuOpened += (_, _) => opened++;
            export.SubmenuOpened += (_, _) => opened++;
            root.Children.Add(new Menu { Items = { file } });
            root.Children.Add(new Button { Content = "CSV", Command = command, HotKey = new(Key.S, KeyModifiers.Control) });
            window.KeyBindings.Add(new() { Gesture = new(Key.F5), Command = command });
            window.UpdateLayout();
            var result = await client.ActionMapAsync(new(runtime.SessionId, top, "csv"));
            Assert.True(result.Success, result.Error?.Message);
            Assert.Equal(2, result.Value!.Actions.Count);
            Assert.All(result.Value.Actions, action => Assert.Equal(2, action.SameLabelCountAtLeast));
            var menu = result.Value.Actions.Single(action => action.Kind == "menu_item");
            Assert.Equal(new[] { "File", "Export", "CSV" }, menu.Route);
            Assert.Equal("observed_public_control", menu.RouteProvenance);
            Assert.Null(menu.Target); Assert.NotNull(menu.RevealTarget);
            Assert.Equal("unrealized", menu.Availability);
            Assert.Contains("lazy", menu.ChildContent!);
            Assert.Equal("display_only_not_a_binding", Assert.Single(menu.Shortcuts).Source);
            var button = result.Value.Actions.Single(action => action.Kind == "button");
            Assert.NotNull(button.Target); Assert.Equal("public_hotkey", Assert.Single(button.Shortcuts).Source);
            var shortcut = await client.ActionMapAsync(new(runtime.SessionId, top, "F5"));
            Assert.Equal("key_binding", Assert.Single(shortcut.Value!.Actions).Kind);
            Assert.Contains("unknown", result.Value.Coverage.UnobservedContent);
            Assert.Equal(0, opened); Assert.Equal(0, command.Executions);
            Assert.False(file.IsSubMenuOpen); Assert.False(export.IsSubMenuOpen);
        });
    }

    [Fact]
    public async Task DisabledCommandsAndAppDeclaredRoutesRespectExistingAllowlists()
    {
        await WithWindow(async (runtime, window, root, top, client) =>
        {
            var command = new Command { Executable = false };
            var button = new Button { Content = "Export", Command = command };
            root.Children.Add(button); window.UpdateLayout();
            var executions = 0;
            using var registration = runtime.RegisterCustomAction(button, new("export", _ =>
            { executions++; return CustomActionOutcome.Succeeded("done"); }, description: "Generate a CSV export", route: ["Tools", "Export", "CSV"]));
            Assert.Throws<InvalidOperationException>(() => runtime.RegisterCustomAction(button, new("not-allowed", _ => CustomActionOutcome.Succeeded("unexpected"))));
            var response = (await client.ActionMapAsync(new(runtime.SessionId, top))).Value!;
            Assert.Contains(response.Actions, action => action.Kind == "button" && action.Availability == "command_disabled");
            var custom = response.Actions.Single(action => action.Kind == "custom_action");
            Assert.Equal("app_declared", custom.RouteProvenance);
            Assert.Equal(new[] { "Tools", "Export", "CSV" }, custom.Route);
            Assert.Equal("export", custom.CustomActionName);
            Assert.Equal("available", custom.Availability);
            var policy = new RuntimeEvidencePolicy(Path.Combine(Path.GetTempPath(), "unused-action-map-policy"),
                allowedActions: [SemanticWorkflowActions.Inspect, SemanticWorkflowActions.CustomAction]);
            var denied = await client.ActionMapAsync(new(runtime.SessionId, top, policy: policy));
            Assert.True(denied.Success, denied.Error?.Message);
            Assert.Contains(denied.Value!.Actions.Single(action => action.Kind == "custom_action").Reasons, reason => reason == "policy_denied");
            command.Throw = true;
            var unavailable = await client.ActionMapAsync(new(runtime.SessionId, top));
            Assert.Contains(unavailable.Value!.Actions, action => action.Kind == "button" && action.Availability == "command_availability_unknown");
            Assert.DoesNotContain("private-callback-error", JsonSerializer.Serialize(unavailable));
            Assert.Equal(0, executions); Assert.Equal(0, command.Executions);
        });
    }

    [Fact]
    public async Task UnmaterializedMenuDataAndLimitsRemainExplicit()
    {
        await WithWindow(async (runtime, window, root, top, client) =>
        {
            var lazy = new MenuItem { Header = "Lazy", ItemsSource = new[] { new object(), new object() } };
            root.Children.Add(new Menu { Items = { lazy } });
            for (var index = 0; index < 150; index++) root.Children.Add(new Button { Content = new string('L', 510) + index });
            window.UpdateLayout();
            var one = (await client.ActionMapAsync(new(runtime.SessionId, top, maxNodes: 1))).Value!;
            Assert.Equal(1, one.Coverage.VisitedNodes); Assert.False(one.Coverage.CompleteObservedScope);
            Assert.Contains("node_limit", one.Coverage.Reasons);
            var result = await client.ActionMapAsync(new(runtime.SessionId, top, maxResults: 128, maxNodes: 4096));
            Assert.True(result.Success, result.Error?.Message);
            Assert.Contains("unrealized_menu_data", result.Value!.Coverage.Reasons);
            Assert.Contains("response_byte_limit", result.Value.Coverage.Reasons);
            Assert.True(JsonSerializer.SerializeToUtf8Bytes(result.Value).Length <= 65536);
            Assert.False(lazy.IsSubMenuOpen);
        });
    }

    [Fact]
    public async Task AppDeclaredActionBlockersAreObservedWithoutDisclosingCallbackErrors()
    {
        await WithWindow(async (runtime, window, root, top, client) =>
        {
            var button = new ContextButton { Content = "Commit" };
            root.Children.Add(button); window.UpdateLayout();
            var blocked = await client.ActionMapAsync(new(runtime.SessionId, top, "Commit"));
            Assert.Equal("blocked", Assert.Single(blocked.Value!.Actions).Availability);
            Assert.Contains("app_declared_action_blocked", blocked.Value.Actions[0].Reasons);
            button.Throw = true;
            var unknown = await client.ActionMapAsync(new(runtime.SessionId, top, "Commit"));
            Assert.Equal("unknown", Assert.Single(unknown.Value!.Actions).Availability);
            Assert.DoesNotContain("private-callback-canary", JsonSerializer.Serialize(unknown));
        });
    }

    private sealed class ContextButton : Button, IAvaScopeActionContextProvider
    {
        protected override Type StyleKeyOverride => typeof(Button);
        public bool Throw { get; set; }
        public AvaScopeActionContext GetActionContext(string action) => Throw
            ? throw new InvalidOperationException("private-callback-canary") : new(CanExecute: false);
    }

    [Fact]
    public async Task SearchUsesRedactedMetadataAndExcludedOwnersHideTheirClosedMenus()
    {
        await WithWindow(async (runtime, window, root, top, client) =>
        {
            var secret = new Button { Content = "private-route-canary" };
            AutomationProperties.SetAutomationId(secret, "sensitive");
            secret.ContextMenu = new ContextMenu { Items = { new MenuItem { Header = "private-context-canary" } } };
            var plain = new Button { Content = "Export private-text-canary" };
            root.Children.Add(secret); root.Children.Add(plain); window.UpdateLayout();
            using var registration = runtime.RegisterCustomAction(secret, new("export", _ => CustomActionOutcome.Succeeded("unexpected"),
                description: "private-custom-canary", route: ["private-route-canary"]));
            var policy = new RuntimeEvidencePolicy(Path.Combine(Path.GetTempPath(), "unused-action-map-policy"),
                redactedAutomationIds: ["sensitive"], redactedText: ["private-text-canary"]);
            var result = await client.ActionMapAsync(new(runtime.SessionId, top, policy: policy));
            Assert.True(result.Success, result.Error?.Message);
            Assert.DoesNotContain("canary", JsonSerializer.Serialize(result));
            Assert.Contains("policy_exclusions", result.Value!.Coverage.Reasons);
            var filtered = await client.ActionMapAsync(new(runtime.SessionId, top, "private-text-canary", policy: policy));
            Assert.Empty(filtered.Value!.Actions);
            var observed = result.Value.Actions.Single(action => action.Kind == "button");
            root.Children.Remove(plain);
            var stale = await client.InputAsync(runtime.SessionId, top, InputActions.Invoke, targetNodeId: observed.Target!.NodeId, inputTarget: observed.Target);
            Assert.False(stale.Success);
        });
    }

    [Fact]
    public async Task ActualCliAndMcpReturnTheSameSearchAndGenerationEvidence()
    {
        await WithWindow(async (runtime, window, root, top, client) =>
        {
            root.Children.Add(new Button { Name = "ExportCsv", Content = "CSV export" }); window.UpdateLayout();
            var request = new RuntimeActionMapRequest(runtime.SessionId, top, "CSV");
            var directory = Path.Combine(Path.GetTempPath(), "AvaScope.Tests", "action-map-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            try
            {
                var path = Path.Combine(directory, "request.json");
                await File.WriteAllTextAsync(path, JsonSerializer.Serialize(request));
                var start = new ProcessStartInfo("dotnet") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
                foreach (var arg in new[] { Path.Combine(AppContext.BaseDirectory, "avascope.dll"), "action-map", "--request", path, "--manifest-dir", client.ManifestDirectory }) start.ArgumentList.Add(arg);
                using var process = Process.Start(start)!;
                var output = process.StandardOutput.ReadToEndAsync(); var errors = process.StandardError.ReadToEndAsync();
                await process.WaitForExitAsync();
                Assert.Equal(0, process.ExitCode); Assert.True(string.IsNullOrWhiteSpace(await errors), await errors);
                var cli = JsonSerializer.Deserialize<ToolResult<RuntimeActionMapResponse>>(await output)!;
                var environment = TestEnvironment.McpEnvironment();
                if (Environment.GetEnvironmentVariable("TMPDIR") is { } temporary) environment["TMPDIR"] = temporary;
                await using var mcp = await McpClient.CreateAsync(new StdioClientTransport(new StdioClientTransportOptions
                {
                    Name = "Action map parity", Command = "dotnet", Arguments = [Path.Combine(AppContext.BaseDirectory, "AvaScope.Mcp.dll")],
                    InheritEnvironmentVariables = false, EnvironmentVariables = environment, ShutdownTimeout = TimeSpan.FromSeconds(3)
                }));
                var call = await mcp.CallToolAsync("action_map", new Dictionary<string, object?>
                    { ["request"] = JsonSerializer.SerializeToElement(request), ["manifestDirectory"] = client.ManifestDirectory });
                var result = JsonSerializer.Deserialize<ToolResult<RuntimeActionMapResponse>>(JsonSerializer.Serialize(call.StructuredContent))!;
                Assert.True(cli.Success); Assert.True(result.Success);
                var first = Assert.Single(cli.Value!.Actions); var second = Assert.Single(result.Value!.Actions);
                Assert.Equal(first.Id, second.Id); Assert.Equal(first.Route, second.Route);
                Assert.Equal(first.Target!.NodeGeneration, second.Target!.NodeGeneration);
                Assert.Equal(first.Availability, second.Availability);
            }
            finally { Directory.Delete(directory, true); }
        });
    }

    private static async Task WithWindow(Func<AvaScopeBridgeRuntime, Window, StackPanel, string, LocalBridgeClient, Task> test)
    {
        var session = HeadlessUnitTestSession.StartNew(typeof(BridgeHeadlessSmokeTests.BridgeHeadlessTestApplication));
        try
        {
            await BridgeHeadlessSmokeTests.DispatchAsync(session, async () =>
            {
                AvaScopeBridge.Deactivate();
                var runtime = AvaScopeBridge.Activate(new(enableCustomActions: true, allowedCustomActions: ["export"]));
                var root = new StackPanel(); var window = new Window { Width = 520, Height = 420, Content = root };
                try
                {
                    window.Show(); using var registration = runtime.RegisterTopLevel(window);
                    var top = Assert.Single(await runtime.ListTopLevelsAsync());
                    await test(runtime, window, root, top.Id, new(Path.GetDirectoryName(runtime.SessionManifestPath)!));
                }
                finally { window.Close(); AvaScopeBridge.Deactivate(); }
            }, CancellationToken.None);
        }
        finally { BridgeHeadlessSmokeTests.DisposeHeadlessSessionAfterExplicitCleanup(session); }
    }

    private sealed class Command : ICommand
    {
        public bool Executable { get; set; } = true;
        public bool Throw { get; set; }
        public int Executions { get; private set; }
        public event EventHandler? CanExecuteChanged { add { } remove { } }
        public bool CanExecute(object? parameter) => Throw ? throw new InvalidOperationException("private-callback-error") : Executable;
        public void Execute(object? parameter) => Executions++;
    }
}
