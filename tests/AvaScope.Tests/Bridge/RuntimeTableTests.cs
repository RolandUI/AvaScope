using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO.Pipes;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Headless;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Threading;
using AvaScope.Bridge;
using AvaScope.Core;
using AvaScope.Protocol;
using ModelContextProtocol.Client;

namespace AvaScope.Tests.Bridge;

[Collection(BridgeCollectionDefinition.Name)]
public sealed class RuntimeTableTests
{
    [Fact]
    public async Task TestSessionPropagatesAnExceptionAfterAnAsynchronousBoundary()
    {
        var failure = await Assert.ThrowsAsync<InvalidOperationException>(() => WithTable(async (_, _, _, _, _) =>
        {
            await Task.Delay(30);
            throw new InvalidOperationException("asynchronous test body sentinel");
        }));
        Assert.Equal("asynchronous test body sentinel", failure.Message);
    }

    [Fact]
    public async Task QueryReturnsTypedFailedRowsProjectionsStablePagingAndExplicitCoverage()
    {
        await WithTable(async (runtime, grid, data, target, client) =>
        {
            var request = new RuntimeTableQueryRequest(target, "Id", ["binding:Status", "binding:Amount", "binding:Active"],
                [new("binding:Status", "equals", JsonSerializer.SerializeToElement("failed"))], limit: 1);
            var first = await client.QueryTableAsync(request);
            Assert.True(first.Success, first.Error?.Message);
            var page = first.Value!;
            Assert.Equal("r0", Assert.Single(page.Rows).Key); Assert.Equal(3, page.Columns.Count);
            Assert.Equal(10m, page.Rows[0].Cells.Single(cell => cell.ColumnId == "binding:Amount").Value!.Value.GetDecimal());
            Assert.False(page.Rows[0].Cells.Single(cell => cell.ColumnId == "binding:Active").Value!.Value.GetBoolean());
            Assert.Equal("unknown", page.Coverage.DatasetCompleteness); Assert.True(page.Coverage.CompleteAvailableView);
            Assert.Equal(3, page.Coverage.TotalAvailableRows); Assert.Equal(2, page.Coverage.MatchedRowsAtLeast);
            Assert.Equal(1, page.NextOffset); Assert.Contains("page_limit", page.Coverage.Reasons);
            var second = await client.QueryTableAsync(new(target, "Id", request.Columns, request.Filters, offset: 1, limit: 1, expectedRevision: page.Revision));
            Assert.Equal("r2", Assert.Single(second.Value!.Rows).Key); Assert.Null(second.Value.NextOffset);
            data[0].Status = "passed";
            var stale = await client.QueryTableAsync(new(target, "Id", request.Columns, request.Filters, offset: 1, limit: 1, expectedRevision: page.Revision));
            Assert.Equal("table_revision_changed", stale.Error!.Code);
            var bounded = (await client.QueryTableAsync(new(target, "Id", maxRows: 1))).Value!;
            Assert.False(bounded.Coverage.CompleteAvailableView); Assert.Contains("row_scan_limit", bounded.Coverage.Reasons);
            Assert.Null(grid.SelectedItem);
        });
    }

    [Fact]
    public async Task StableKeysSelectOffscreenRowsAndRejectReplacedObjects()
    {
        await WithTable(async (runtime, grid, data, target, client) =>
        {
            for (var index = 3; index < 120; index++) data.Add(new("r" + index, "passed", index));
            Dispatcher.UIThread.RunJobs();
            var query = new RuntimeTableQueryRequest(target, "Id", filters: [new("binding:Id", "equals", JsonSerializer.SerializeToElement("r100"))], maxRows: 128);
            var initial = (await client.QueryTableAsync(query)).Value!;
            var row = Assert.Single(initial.Rows); Assert.False(row.Realized);
            var request = new RuntimeTableActionRequest(query, "select_row", "select-offscreen", row.Key, row.Generation, timeoutMs: 3000);
            var selected = await client.TableActionAsync(request);
            Assert.True(selected.Value!.Verified, JsonSerializer.Serialize(selected));
            Assert.Same(data[100], grid.SelectedItem); Assert.True(selected.Value.After!.Selected);
            Assert.Equal(row.Generation, selected.Value.After.Generation);
            grid.SelectedItem = data[1];
            Assert.True((await client.TableActionAsync(request)).Value!.Replayed); Assert.Same(data[1], grid.SelectedItem);
            data[100] = new("r100", "passed", 100);
            var replaced = await client.TableActionAsync(new(query, "select_row", "stale-row", row.Key, row.Generation));
            Assert.False(replaced.Value!.Verified); Assert.Equal(0, replaced.Value.DispatchedOperations);
            Assert.Contains(replaced.Value.Diagnostics, error => error.Code == "table_row_changed");
        });
    }

    [Fact]
    public async Task PublicSortAndRoutedCellEditsAreVerifiedAndReplayedWithoutRepeatingWrites()
    {
        await WithTable(async (runtime, grid, data, target, client) =>
        {
            var query = new RuntimeTableQueryRequest(target, "Id");
            var initial = (await client.QueryTableAsync(query)).Value!;
            var amount = initial.Columns.Single(column => column.Id == "binding:Amount");
            var sortCount = 0; grid.Sorting += (_, _) => sortCount++;
            var sort = new RuntimeTableActionRequest(query, "sort", "sort-amount", columnId: amount.Id, columnGeneration: amount.Generation, direction: "descending");
            var sorted = await client.TableActionAsync(sort);
            Assert.True(sorted.Value!.Verified, JsonSerializer.Serialize(sorted));
            Assert.Equal("descending", Assert.Single(sorted.Value.SortsAfter).Direction); Assert.Equal(1, sortCount);
            Assert.True((await client.TableActionAsync(sort)).Value!.Replayed); Assert.Equal(1, sortCount);
            var sortedQuery = (await client.QueryTableAsync(query)).Value!;
            Assert.Equal("r2", sortedQuery.Rows[0].Key);
            var row = sortedQuery.Rows.Single(row => row.Key == "r0");
            var status = sortedQuery.Columns.Single(column => column.Id == "binding:Status");
            var filtered = new RuntimeTableQueryRequest(target, "Id", filters: [new("binding:Status", "equals", JsonSerializer.SerializeToElement("failed"))]);
            var edit = new RuntimeTableActionRequest(filtered, "edit_cell", "fix-status", row.Key, row.Generation, status.Id, status.Generation,
                JsonSerializer.SerializeToElement("passed"), timeoutMs: 3000);
            var edited = await client.TableActionAsync(edit);
            Assert.True(edited.Value!.Verified, JsonSerializer.Serialize(edited)); Assert.Equal("passed", data[0].Status);
            Assert.Equal(RuntimeOperationRoutes.ControlApi, edited.Value.Provenance.Route);
            var writes = data[0].StatusWrites;
            Assert.True((await client.TableActionAsync(edit)).Value!.Replayed); Assert.Equal(writes, data[0].StatusWrites);
            var numeric = new RuntimeTableActionRequest(query, "edit_cell", "numeric", row.Key, row.Generation, amount.Id, amount.Generation,
                JsonSerializer.SerializeToElement(12.5m), timeoutMs: 3000);
            var number = await client.TableActionAsync(numeric);
            Assert.True(number.Value!.Verified, JsonSerializer.Serialize(number)); Assert.Equal(12.5m, data[0].Amount);
        });
    }

    [Fact]
    public async Task DuplicateKeysAndColumnsFailWithoutFirstMatchDispatch()
    {
        await WithTable(async (runtime, grid, data, target, client) =>
        {
            data.Add(new("r0", "failed", 100)); Dispatcher.UIThread.RunJobs();
            var query = new RuntimeTableQueryRequest(target, "Id");
            var observed = (await client.QueryTableAsync(query)).Value!;
            Assert.All(observed.Rows.Where(row => row.Key == "r0"), row => Assert.Equal("ambiguous", row.KeyStatus));
            var row = observed.Rows.First(row => row.Key == "r0");
            var result = await client.TableActionAsync(new(query, "select_row", "duplicate", row.Key, row.Generation));
            Assert.False(result.Value!.Verified); Assert.Equal(0, result.Value.DispatchedOperations);
            grid.Columns.Add(new DataGridTextColumn { Header = "Status again", Binding = new ReflectionBinding("Status") });
            var columns = await client.QueryTableAsync(query);
            Assert.Equal("table_unsupported", columns.Error!.Code); Assert.Contains("ambiguous", columns.Error.Message);
        });
    }

    [Fact]
    public async Task UnsupportedBindingsAndReadonlyColumnsRemainExplicitAndDoNotWriteModels()
    {
        await WithTable(async (runtime, grid, data, target, client) =>
        {
            grid.Columns[2].IsReadOnly = true;
            grid.Columns.Add(new DataGridTemplateColumn { Header = "Custom",
                CellTemplate = new Avalonia.Controls.Templates.FuncDataTemplate<Row>((_, _) => new TextBlock { Text = "custom" }) });
            grid.Columns.Add(new DataGridTextColumn { Header = "OtherSource", Tag = "other", Binding = new ReflectionBinding("Status") { Source = data[1] } });
            grid.Columns.Add(new DataGridTextColumn { Header = "Formatted", Tag = "formatted", Binding = new ReflectionBinding("Amount") { StringFormat = "{0:C}" } });
            grid.Columns.Add(new DataGridTextColumn { Header = "Converted", Tag = "converted", Binding = new ReflectionBinding("Status")
                { Converter = new Avalonia.Data.Converters.FuncValueConverter<string, string>(value => "display:" + value) } });
            Dispatcher.UIThread.RunJobs();
            var query = new RuntimeTableQueryRequest(target, "Id");
            var result = (await client.QueryTableAsync(query)).Value!;
            Assert.All(result.Rows, row =>
            {
                Assert.Equal("unsupported_binding", row.Cells.Single(cell => cell.ColumnId == "header:Custom").Status);
                Assert.Equal("unsupported_binding", row.Cells.Single(cell => cell.ColumnId == "tag:other").Status);
                Assert.Equal("unsupported_binding", row.Cells.Single(cell => cell.ColumnId == "tag:formatted").Status);
                Assert.Equal("unsupported_binding", row.Cells.Single(cell => cell.ColumnId == "tag:converted").Status);
            });
            var amount = result.Columns.Single(column => column.Id == "binding:Amount");
            Assert.True(amount.ReadOnly);
            var row = result.Rows[0];
            var denied = await client.TableActionAsync(new(query, "edit_cell", "readonly", row.Key, row.Generation, amount.Id, amount.Generation, JsonSerializer.SerializeToElement(50)));
            Assert.False(denied.Value!.Verified); Assert.Equal(0, denied.Value.DispatchedOperations); Assert.Equal(10m, data[0].Amount);
        });
    }

    [Fact]
    public async Task LostResponsesFullLedgerAndClosedWindowsKeepOriginalResultsWithoutRepeatingSelection()
    {
        await WithTable(async (runtime, grid, data, target, client) =>
        {
            var query = new RuntimeTableQueryRequest(target, "Id");
            var row = (await client.QueryTableAsync(query)).Value!.Rows[1];
            var request = new RuntimeTableActionRequest(query, "select_row", "lost", row.Key, row.Generation);
            var manifest = client.ListSessionManifests().Single(item => item.SessionId == runtime.SessionId);
            using (var pipe = new NamedPipeClientStream(".", manifest.PipeName, PipeDirection.InOut, PipeOptions.Asynchronous))
            {
                await pipe.ConnectAsync(5000);
                await pipe.WriteAsync(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new BridgeIpcRequest("discard", BridgeIpcMethods.TableAction, tableAction: request)) + "\n"));
                await pipe.FlushAsync();
                for (var attempt = 0; !ReferenceEquals(grid.SelectedItem, data[1]) && attempt < 100; attempt++) await Task.Delay(10);
                Assert.Same(data[1], grid.SelectedItem);
            }
            Assert.True((await client.TableActionAsync(request)).Value!.Replayed);
            for (var index = 1; index < 128; index++)
                Assert.Equal("already_satisfied", (await runtime.TableActionAsync(new(query, "select_row", "ledger-" + index, row.Key, row.Generation))).Value!.Status);
            Assert.Equal("table_action_ledger_full", (await runtime.TableActionAsync(new(query, "select_row", "overflow", row.Key, row.Generation))).Error!.Code);
            var conflict = await runtime.TableActionAsync(new(query, "select_row", request.RequestId, "other", row.Generation));
            Assert.Equal("table_action_conflict", conflict.Error!.Code);
            ((Window)TopLevel.GetTopLevel(grid)!).Close();
            Assert.True((await client.TableActionAsync(request)).Value!.Replayed);
        });
    }

    [Fact]
    public async Task EscapedLargeValuesRespectByteBudgetsAndPolicyPermissions()
    {
        await WithTable(async (runtime, grid, data, target, client) =>
        {
            foreach (var item in data) item.Status = new string('Á', 4000);
            for (var index = 0; index < 12; index++)
                grid.Columns.Add(new DataGridTextColumn { Header = "Repeated " + index, Tag = "large-" + index, Binding = new ReflectionBinding("Status") });
            Dispatcher.UIThread.RunJobs();
            var query = new RuntimeTableQueryRequest(target, "Id");
            var response = (await client.QueryTableAsync(query)).Value!;
            Assert.True(JsonSerializer.SerializeToUtf8Bytes(response).Length <= 65536);
            Assert.Contains("response_byte_limit", response.Coverage.Reasons);
            // A narrow projection provides a usable row identity even when a full row exceeds the page budget.
            var narrow = (await client.QueryTableAsync(new(target, "Id", ["binding:Id"]))).Value!;
            var row = narrow.Rows[1];
            var selected = (await client.TableActionAsync(new(query, "select_row", "large-result", row.Key, row.Generation))).Value!;
            Assert.True(selected.Verified); Assert.True(JsonSerializer.SerializeToUtf8Bytes(selected).Length <= 65536);
            var policy = new RuntimeEvidencePolicy(Path.Combine(Path.GetTempPath(), "avascope-table-policy"), authorizedSessionIds: [runtime.SessionId.Value]);
            var denied = await client.TableActionAsync(new(new(target, "Id", policy: policy), "select_row", "denied", row.Key, row.Generation));
            Assert.Equal("table_action_policy_denied", denied.Error!.Code);
        });
    }

    [Fact]
    public async Task CheckboxEditsUseTheirPublicProviderAndColumnReplacementInvalidatesIntent()
    {
        await WithTable(async (runtime, grid, data, target, client) =>
        {
            var query = new RuntimeTableQueryRequest(target, "Id");
            var snapshot = (await client.QueryTableAsync(query)).Value!;
            var row = snapshot.Rows[0]; var column = snapshot.Columns.Single(item => item.Id == "binding:Active");
            var edit = await client.TableActionAsync(new(query, "edit_cell", "check", row.Key, row.Generation, column.Id, column.Generation, JsonSerializer.SerializeToElement(true)));
            Assert.True(edit.Value!.Verified, JsonSerializer.Serialize(edit)); Assert.True(data[0].Active);
            grid.Columns[3] = new DataGridCheckBoxColumn { Header = "Active", Binding = new ReflectionBinding("Active") };
            var stale = await client.TableActionAsync(new(query, "edit_cell", "replaced-column", row.Key, row.Generation, column.Id, column.Generation, JsonSerializer.SerializeToElement(false)));
            Assert.False(stale.Value!.Verified); Assert.Equal(0, stale.Value.DispatchedOperations); Assert.True(data[0].Active);
            Assert.Contains(stale.Value.Diagnostics, error => error.Code == "table_column_changed");
        });
    }

    [Fact]
    public async Task ValidationRejectionAndExistingDraftsAreNotSilentlyCommittedOrRolledBack()
    {
        await WithTable(async (runtime, grid, data, target, client) =>
        {
            var query = new RuntimeTableQueryRequest(target, "Id");
            var observed = (await client.QueryTableAsync(query)).Value!;
            var amount = observed.Columns.Single(column => column.Id == "binding:Amount"); var row = observed.Rows[0];
            var result = await client.TableActionAsync(new(query, "edit_cell", "invalid-amount", row.Key, row.Generation, amount.Id, amount.Generation,
                JsonSerializer.SerializeToElement(-1), timeoutMs: 3000));
            Assert.False(result.Value!.Verified); Assert.NotEqual(-1m, data[0].Amount); Assert.True(result.Value.PreparationPerformed);
            Assert.Contains(result.Value.Diagnostics, error => error.Code is "table_editor_not_verified" or "table_validation_rejected");
            // Test fixture cleanup is explicit; the tool leaves the rejected draft visible.
            grid.CancelEdit(); Dispatcher.UIThread.RunJobs();
            grid.SelectedItem = data[1]; grid.CurrentColumn = grid.Columns[1]; Assert.True(grid.BeginEdit());
            var selected = await client.TableActionAsync(new(query, "select_row", "active-draft", row.Key, row.Generation));
            Assert.False(selected.Value!.Verified); Assert.Equal(0, selected.Value.DispatchedOperations);
            Assert.Contains(selected.Value.Diagnostics, error => error.Code == "table_edit_active");
            grid.CancelEdit();
        });
    }

    [Fact]
    public async Task SortingDuringQueryInvalidatesTheSnapshotAndBoundedScansCannotAuthorizeWrites()
    {
        await WithTable(async (runtime, grid, data, target, client) =>
        {
            for (var index = 3; index < 80; index++) data.Add(new("r" + index, "passed", index));
            Dispatcher.UIThread.RunJobs();
            // An unrealized row has no display binding that could invoke the getter before the query.
            data[70].StatusRead = () =>
            {
                data[70].StatusRead = null;
                grid.CollectionView.SortDescriptions.Add(Avalonia.Collections.DataGridSortDescription.FromPath("Amount", ListSortDirection.Descending));
            };
            var changed = await client.QueryTableAsync(new(target, "Id"));
            Assert.False(changed.Success, "A synchronous public collection-view sort during a getter must invalidate the read.");
            Assert.Equal("table_changed", changed.Error!.Code);
            var query = new RuntimeTableQueryRequest(target, "Id", maxRows: 1);
            var bounded = (await client.QueryTableAsync(query)).Value!; var row = Assert.Single(bounded.Rows);
            var action = await client.TableActionAsync(new(query, "select_row", "bounded", row.Key, row.Generation));
            Assert.False(action.Value!.Verified); Assert.Equal(0, action.Value.DispatchedOperations);
            Assert.Contains(action.Value.Diagnostics, error => error.Code == "table_identity_incomplete");
        });
    }

    [Fact]
    public async Task RedactionDoesNotBecomeASecretFilterOracleAndExclusionsRejectReads()
    {
        await WithTable(async (runtime, grid, data, target, client) =>
        {
            data[0].Status = "private-value"; Dispatcher.UIThread.RunJobs();
            var policy = new RuntimeEvidencePolicy(Path.Combine(Path.GetTempPath(), "avascope-table-policy"), redactedText: ["private-value"],
                authorizedSessionIds: [runtime.SessionId.Value], allowedTableActions: ["edit_cell"]);
            var result = await client.QueryTableAsync(new(target, "Id", policy: policy));
            Assert.DoesNotContain("private-value", JsonSerializer.Serialize(result));
            var filtered = (await client.QueryTableAsync(new(target, "Id", filters: [new("binding:Status", "equals", JsonSerializer.SerializeToElement("private-value"))], policy: policy))).Value!;
            Assert.Empty(filtered.Rows); Assert.Contains("unavailable_filter_value", filtered.Coverage.Reasons);
            Assert.False(filtered.Coverage.CompleteAvailableView);
            var row = result.Value!.Rows[0]; var column = result.Value.Columns.Single(item => item.Id == "binding:Status");
            var compareSecret = await client.TableActionAsync(new(new(target, "Id", policy: policy), "edit_cell", "no-secret-oracle", row.Key, row.Generation,
                column.Id, column.Generation, JsonSerializer.SerializeToElement("private-value")));
            Assert.False(compareSecret.Value!.Verified); Assert.Equal(0, compareSecret.Value.DispatchedOperations);
            Assert.Contains(compareSecret.Value.Diagnostics, error => error.Code == "table_cell_unavailable");
            AutomationProperties.SetAutomationId(grid, "private-table");
            var excluded = new RuntimeEvidencePolicy(policy.OwnedEvidenceRoot, excludedControlAutomationIds: ["private-table"], authorizedSessionIds: [runtime.SessionId.Value]);
            Assert.Equal("table_excluded", (await client.QueryTableAsync(new(target, "Id", policy: excluded))).Error!.Code);
        });
    }

    [Fact]
    public async Task CliAndMcpShareTypedQueriesSelectionReplayAndOperationFailures()
    {
        await WithTable(async (runtime, grid, data, target, client) =>
        {
            var query = new RuntimeTableQueryRequest(target, "Id", filters: [new("binding:Status", "equals", JsonSerializer.SerializeToElement("failed"))]);
            var observed = await Cli<RuntimeTableQueryResponse>("query-table", query, client.ManifestDirectory, 0);
            Assert.True(observed.Success); Assert.Equal(2, observed.Value!.Rows.Count);
            var row = observed.Value.Rows[1];
            var request = new RuntimeTableActionRequest(query, "select_row", "adapter-selection", row.Key, row.Generation);
            var selected = await Cli<RuntimeTableActionResponse>("table-action", request, client.ManifestDirectory, 0);
            Assert.True(selected.Success); Assert.Same(data[2], grid.SelectedItem);
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            await using var mcp = await McpClient.CreateAsync(new StdioClientTransport(new()
            { Command = "dotnet", Arguments = [Path.Combine(AppContext.BaseDirectory, "AvaScope.Mcp.dll")], Name = "table-test" }), cancellationToken: timeout.Token);
            var call = await mcp.CallToolAsync("table_action", new Dictionary<string, object?>
            { ["request"] = JsonSerializer.SerializeToElement(request), ["manifestDirectory"] = client.ManifestDirectory }, cancellationToken: timeout.Token);
            var replay = JsonSerializer.Deserialize<ToolResult<RuntimeTableActionResponse>>(JsonSerializer.Serialize(call.StructuredContent))!;
            Assert.True(replay.Success); Assert.True(replay.Value!.Replayed);
            call = await mcp.CallToolAsync("query_table", new Dictionary<string, object?>
            { ["request"] = JsonSerializer.SerializeToElement(query), ["manifestDirectory"] = client.ManifestDirectory }, cancellationToken: timeout.Token);
            var read = JsonSerializer.Deserialize<ToolResult<RuntimeTableQueryResponse>>(JsonSerializer.Serialize(call.StructuredContent))!;
            Assert.True(read.Success); Assert.True(read.Value!.Rows.Single(item => item.Key == row.Key).Selected);
            var missing = new RuntimeTableActionRequest(query, "select_row", "absent", "missing", row.Generation);
            var failed = await Cli<RuntimeTableActionResponse>("table-action", missing, client.ManifestDirectory, 1);
            Assert.False(failed.Success); Assert.Equal(0, failed.Value!.DispatchedOperations);
        });
    }

    private static async Task<ToolResult<T>> Cli<T>(string command, object request, string manifestDirectory, int exit)
    {
        var path = Path.Combine(Path.GetTempPath(), "avascope-table-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            await File.WriteAllTextAsync(path, JsonSerializer.Serialize(request));
            var start = new ProcessStartInfo("dotnet") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
            foreach (var arg in new[] { Path.Combine(AppContext.BaseDirectory, "avascope.dll"), command, "--request", path, "--manifest-dir", manifestDirectory }) start.ArgumentList.Add(arg);
            using var process = Process.Start(start)!;
            var output = process.StandardOutput.ReadToEndAsync(); var error = process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(25)); Assert.True(exit == process.ExitCode, await output + await error);
            return JsonSerializer.Deserialize<ToolResult<T>>(await output)!;
        }
        finally { File.Delete(path); }
    }

    private static async Task WithTable(Func<AvaScopeBridgeRuntime, DataGrid, ObservableCollection<Row>, RuntimeTargetContext, LocalBridgeClient, Task> test)
    {
        var session = HeadlessUnitTestSession.StartNew(typeof(BridgeHeadlessSmokeTests.BridgeHeadlessTestApplication));
        try
        {
            await BridgeHeadlessSmokeTests.DispatchAsync(session, async () =>
            {
                AvaScopeBridge.Deactivate(); var runtime = AvaScopeBridge.Activate();
                Avalonia.Application.Current!.Styles.Add(new StyleInclude(new Uri("avares://Avalonia.Controls.DataGrid/"))
                    { Source = new Uri("avares://Avalonia.Controls.DataGrid/Themes/Fluent.xaml") });
                var data = new ObservableCollection<Row> { new("r0", "failed", 10), new("r1", "passed", 20), new("r2", "failed", 30) };
                var grid = new DataGrid { Name = "Table", AutoGenerateColumns = false, ItemsSource = data, Height = 240 };
                grid.Columns.Add(new DataGridTextColumn { Header = "Identifier", Binding = new ReflectionBinding("Id"), IsReadOnly = true });
                grid.Columns.Add(new DataGridTextColumn { Header = "Status", Binding = new ReflectionBinding("Status") });
                grid.Columns.Add(new DataGridTextColumn { Header = "Amount", Binding = new ReflectionBinding("Amount") });
                grid.Columns.Add(new DataGridCheckBoxColumn { Header = "Active", Binding = new ReflectionBinding("Active") });
                var window = new Window { Width = 700, Height = 300, Content = grid };
                try
                {
                    window.Show(); using var registration = runtime.RegisterTopLevel(window); Dispatcher.UIThread.RunJobs();
                    window.UpdateLayout();
                    Assert.NotNull(grid.Template);
                    Assert.NotNull(grid.Columns[0].GetCellContent(data[0]));
                    var top = Assert.Single(await runtime.ListTopLevelsAsync()).Id;
                    var target = Assert.Single((await runtime.FindNodesAsync(top, TreeKinds.Visual, name: "Table")).Value!.Matches).Target!;
                    await test(runtime, grid, data, target, new(Path.GetDirectoryName(runtime.SessionManifestPath)!));
                }
                finally { window.Close(); AvaScopeBridge.Deactivate(); }
            }, CancellationToken.None);
        }
        finally { BridgeHeadlessSmokeTests.DisposeHeadlessSessionAfterExplicitCleanup(session); }
    }

    private sealed class Row(string id, string status, decimal amount) : INotifyPropertyChanged
    {
        private string _status = status;
        private decimal _amount = amount;
        private bool _active;
        public string Id { get; } = id;
        public Action? StatusRead { get; set; }
        public int StatusWrites { get; private set; }
        public string Status { get { StatusRead?.Invoke(); return _status; } set { _status = value; StatusWrites++; Changed(); } }
        public decimal Amount { get => _amount; set { if (value < 0) throw new ArgumentException("Amount must be nonnegative."); _amount = value; Changed(); } }
        public bool Active { get => _active; set { _active = value; Changed(); } }
        public event PropertyChangedEventHandler? PropertyChanged;
        private void Changed([CallerMemberName] string? property = null) => PropertyChanged?.Invoke(this, new(property));
    }
}
