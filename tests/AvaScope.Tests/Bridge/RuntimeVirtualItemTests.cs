using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Text.Json;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Headless;
using Avalonia.Threading;
using AvaScope.Bridge;
using AvaScope.Core;
using AvaScope.Protocol;
using ModelContextProtocol.Client;

namespace AvaScope.Tests.Bridge;

[Collection(BridgeCollectionDefinition.Name)]
public sealed class RuntimeVirtualItemTests
{
    [Fact]
    public async Task OffscreenLogicalKeysSurviveRecyclingLocalizationAndCollectionReordering()
    {
        await WithList(async (runtime, _, list, rows, target, client) =>
        {
            Assert.Null(list.ContainerFromIndex(190));
            var found = await client.VirtualItemAsync(new RuntimeVirtualItemRequest(target, "Id", "row-190"));
            Assert.True(found.Success, found.Error?.Message);
            Assert.False(found.Value!.Realized);
            var selected = await client.VirtualItemAsync(new RuntimeVirtualItemRequest(target, "Id", "row-190", "select"));
            Assert.True(selected.Success, selected.Error?.Message);
            Assert.Equal("selected", selected.Value!.Status);
            Assert.True(selected.Value.Rendered);
            Assert.Equal(rows[190], list.SelectedItem);
            Assert.NotNull(selected.Value.Container?.NodeGeneration);
            Assert.InRange(selected.Value.ScannedItems, 200, 30000);
            var first = await client.VirtualItemAsync(new RuntimeVirtualItemRequest(target, "Id", "row-0", "reveal"));
            Assert.True(first.Success, first.Error?.Message);
            Assert.Equal(rows[190], list.SelectedItem);
            Assert.Null(list.ContainerFromIndex(190));
            var expected = rows[190];
            expected.Label = "Magyar címke, megegyező felirat";
            rows.Move(190, 70);
            list.Width = 230;
            var moved = await client.VirtualItemAsync(new RuntimeVirtualItemRequest(target, "Id", "row-190", "select"));
            Assert.True(moved.Success, moved.Error?.Message);
            Assert.Equal(70, moved.Value!.Index);
            Assert.Same(expected, list.SelectedItem);
            Assert.Equal("row-190", moved.Value.Key);
            Assert.Equal("Id", moved.Value.KeyProperty);
            Assert.Equal(runtime.SessionId, moved.Value.Collection.SessionId);
        });
    }

    [Fact]
    public async Task AmbiguousUnsupportedStaleAndOverBudgetRequestsCannotSelectAnAlternateRow()
    {
        await WithList(async (runtime, _, list, rows, target, client) =>
        {
            list.SelectedIndex = 0;
            rows[198].Id = rows[199].Id;
            var duplicate = await client.VirtualItemAsync(new RuntimeVirtualItemRequest(target, "Id", "row-199", "select"));
            Assert.Equal("virtual_item_ambiguous", duplicate.Error!.Code);
            Assert.Equal(0, list.SelectedIndex);
            var labels = await client.VirtualItemAsync(new RuntimeVirtualItemRequest(target, "Label", "Same translated label", "select"));
            Assert.Equal("virtual_item_ambiguous", labels.Error!.Code);
            var limited = await client.VirtualItemAsync(new RuntimeVirtualItemRequest(target, "Id", "row-190", "select", maxItems: 100));
            Assert.Equal("virtual_item_search_limit", limited.Error!.Code);
            var unknown = await client.VirtualItemAsync(new RuntimeVirtualItemRequest(target, "Missing", "row-190", "select"));
            Assert.Equal("virtual_item_unsupported_identity", unknown.Error!.Code);
            var missing = await client.VirtualItemAsync(new RuntimeVirtualItemRequest(target, "Id", "does-not-exist"));
            Assert.Equal("virtual_item_not_found", missing.Error!.Code);
            var stale = new RuntimeTargetContext(target.SessionId, target.TopLevelId, target.TreeKind, target.NodeId, nodeGeneration: "stale");
            Assert.Equal("virtual_item_stale_collection", (await client.VirtualItemAsync(new RuntimeVirtualItemRequest(stale, "Id", "row-190", "select"))).Error!.Code);
            Assert.Equal(0, list.SelectedIndex);
            list.ContainerPrepared += (_, args) =>
            {
                if (args.Index == 190) rows[189].Id = "row-190";
            };
            var changedDuringRealization = await client.VirtualItemAsync(new RuntimeVirtualItemRequest(target, "Id", "row-190", "select"));
            Assert.Equal("virtual_item_ambiguous", changedDuringRealization.Error!.Code);
            Assert.Equal(0, list.SelectedIndex);
            rows[189].Id = "row-189";
            list.Height = 0;
            Dispatcher.UIThread.RunJobs();
            var timeout = await runtime.VirtualItemAsync(new RuntimeVirtualItemRequest(target, "Id", "row-50", "reveal", timeoutMs: 100));
            Assert.Equal("virtual_item_timeout", timeout.Error!.Code);
            Assert.InRange(int.Parse(timeout.Error.Details!["scrollRequests"]), 0, 8);
            using var cancellation = new CancellationTokenSource(30);
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => runtime.VirtualItemAsync(
                new RuntimeVirtualItemRequest(target, "Id", "row-50", "reveal"), cancellation.Token));
            Assert.Equal(0, list.SelectedIndex);
        });
    }

    [Fact]
    public async Task ActualCliAndMcpReturnTheSameLogicalIdentityAndAuditRecommendations()
    {
        await WithList(async (runtime, _, _, _, target, client) =>
        {
            var path = Path.Combine(Path.GetTempPath(), $"avascope-item-{Guid.NewGuid():N}.json");
            try
            {
                var request = new RuntimeVirtualItemRequest(target, "Id", "row-190");
                await File.WriteAllTextAsync(path, JsonSerializer.Serialize(request));
                var manifestDirectory = client.ManifestDirectory;
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
                var start = new ProcessStartInfo("dotnet") { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true };
                foreach (var argument in new[] { Path.Combine(AppContext.BaseDirectory, "avascope.dll"), "virtual-item", "--request", path, "--manifest-dir", manifestDirectory }) start.ArgumentList.Add(argument);
                using var process = Process.Start(start)!;
                var output = process.StandardOutput.ReadToEndAsync(timeout.Token);
                var errors = process.StandardError.ReadToEndAsync(timeout.Token);
                await process.WaitForExitAsync(timeout.Token);
                Assert.Equal(0, process.ExitCode);
                Assert.True(string.IsNullOrWhiteSpace(await errors));
                var cli = JsonSerializer.Deserialize<ToolResult<RuntimeVirtualItemResponse>>(await output)!;
                Assert.True(cli.Success, cli.Error?.Message);
                var environment = StdioClientTransportOptions.GetDefaultEnvironmentVariables();
                if (Environment.GetEnvironmentVariable("TMPDIR") is { } temporary) environment["TMPDIR"] = temporary;
                await using var mcp = await McpClient.CreateAsync(new StdioClientTransport(new StdioClientTransportOptions
                {
                    Name = "Virtual item parity", Command = "dotnet", Arguments = [Path.Combine(AppContext.BaseDirectory, "AvaScope.Mcp.dll")],
                    InheritEnvironmentVariables = false, EnvironmentVariables = environment, ShutdownTimeout = TimeSpan.FromSeconds(3)
                }), cancellationToken: timeout.Token);
                var call = await mcp.CallToolAsync("virtual_item", new Dictionary<string, object?>
                {
                    ["request"] = JsonSerializer.SerializeToElement(request), ["manifestDirectory"] = manifestDirectory
                }, cancellationToken: timeout.Token);
                var actual = JsonSerializer.Deserialize<ToolResult<RuntimeVirtualItemResponse>>(JsonSerializer.Serialize(call.StructuredContent))!;
                Assert.True(actual.Success, actual.Error?.Message);
                Assert.Equal(cli.Value!.Key, actual.Value!.Key);
                Assert.Equal(cli.Value.Index, actual.Value.Index);
                Assert.Equal(cli.Value.Collection.NodeId, actual.Value.Collection.NodeId);
                start.ArgumentList.Clear();
                foreach (var argument in new[] { Path.Combine(AppContext.BaseDirectory, "avascope.dll"), "audit-ui", "--session", runtime.SessionId.Value, "--top-level", target.TopLevelId, "--max-depth", "8", "--manifest-dir", manifestDirectory }) start.ArgumentList.Add(argument);
                using var auditProcess = Process.Start(start)!;
                output = auditProcess.StandardOutput.ReadToEndAsync(timeout.Token);
                errors = auditProcess.StandardError.ReadToEndAsync(timeout.Token);
                await auditProcess.WaitForExitAsync(timeout.Token);
                Assert.Equal(0, auditProcess.ExitCode);
                Assert.True(string.IsNullOrWhiteSpace(await errors));
                var audit = JsonSerializer.Deserialize<ToolResult<UiAuditResponse>>(await output)!;
                var auditCall = await mcp.CallToolAsync("audit_ui", new Dictionary<string, object?>
                {
                    ["sessionId"] = runtime.SessionId.Value, ["topLevelId"] = target.TopLevelId, ["maxDepth"] = 8, ["manifestDirectory"] = manifestDirectory
                }, cancellationToken: timeout.Token);
                var auditMcp = JsonSerializer.Deserialize<ToolResult<UiAuditResponse>>(JsonSerializer.Serialize(auditCall.StructuredContent))!;
                Assert.True(audit.Success, audit.Error?.Message);
                Assert.True(auditMcp.Success, auditMcp.Error?.Message);
                Assert.Equal(JsonSerializer.Serialize(audit.Value!.SelectorRecommendations), JsonSerializer.Serialize(auditMcp.Value!.SelectorRecommendations));
                Assert.Contains(audit.Value.SelectorRecommendations, recommendation => recommendation.Selector?.AutomationId == "Rows");
            }
            finally { File.Delete(path); }
        });
    }

    private static async Task WithList(Func<AvaScopeBridgeRuntime, Window, ListBox, ObservableCollection<Row>, RuntimeTargetContext, LocalBridgeClient, Task> test)
    {
        using var session = HeadlessUnitTestSession.StartNew(typeof(BridgeHeadlessSmokeTests.BridgeHeadlessTestApplication));
        await session.Dispatch(async () =>
        {
            AvaScopeBridge.Deactivate();
            var runtime = AvaScopeBridge.Activate(new BridgeActivationOptions("Logical item tests"));
            var rows = new ObservableCollection<Row>(Enumerable.Range(0, 200).Select(index => new Row { Id = $"row-{index}", Label = "Same translated label" }));
            var list = new ListBox
            {
                Name = "Rows", Width = 280, Height = 150, ItemsSource = rows,
                ItemTemplate = new FuncDataTemplate<Row>((row, _) => new Grid
                {
                    Height = 30, ColumnDefinitions = new ColumnDefinitions("*,*"),
                    Children = { new TextBlock { Text = row!.Label }, new TextBlock { Text = row.Id, [Grid.ColumnProperty] = 1 } }
                })
            };
            AutomationProperties.SetAutomationId(list, "Rows");
            var window = new Window { Width = 320, Height = 200, Content = list };
            try
            {
                window.Show();
                using var registration = runtime.RegisterTopLevel(window);
                Dispatcher.UIThread.RunJobs();
                var top = Assert.Single(await runtime.ListTopLevelsAsync());
                var found = await runtime.FindNodesAsync(top.Id, TreeKinds.Visual, automationId: "Rows", maxDepth: 8);
                var target = Assert.Single(found.Value!.Matches).Node.Target!;
                await test(runtime, window, list, rows, target, new LocalBridgeClient(Path.GetDirectoryName(runtime.SessionManifestPath)!));
            }
            finally { window.Close(); AvaScopeBridge.Deactivate(); }
        }, CancellationToken.None);
    }

    private sealed class Row
    {
        public required string Id { get; set; }
        public required string Label { get; set; }
    }
}
