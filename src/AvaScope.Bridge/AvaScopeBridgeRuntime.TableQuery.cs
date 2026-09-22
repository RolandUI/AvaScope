using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;
using AvaScope.Core;
using AvaScope.Protocol;

namespace AvaScope.Bridge;

public sealed partial class AvaScopeBridgeRuntime
{
    public async Task<CoreResult<RuntimeTableQueryResponse>> QueryTableAsync(RuntimeTableQueryRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var result = await Dispatcher.UIThread.InvokeAsync(() => CaptureTable(request), DispatcherPriority.Background, cancellationToken);
        if (!result.Success) return CoreResult<RuntimeTableQueryResponse>.Fail(result.Error!);
        return request.Policy is null ? CoreResult<RuntimeTableQueryResponse>.Ok(result.Value!.Response)
            : new RuntimeEvidencePolicyEnforcer(request.Policy).Sanitize(result.Value!.Response);
    }

    private CoreResult<TableCapture> CaptureTable(RuntimeTableQueryRequest request)
    {
        Dispatcher.UIThread.VerifyAccess();
        if (request.Table.SessionId != SessionId) return Fail("table_session_mismatch", "Select this bridge session explicitly.");
        var top = FindTopLevel(request.Table.TopLevelId);
        if (top is null) return TopLevelNotFound<TableCapture>(request.Table.TopLevelId);
        var resolved = ResolveMutationTarget(top, request.Table);
        if (!resolved.Success) return CoreResult<TableCapture>.Fail(resolved.Error!);
        if (resolved.Value!.Node is not Control control) return Fail("table_control_required", "Select a supported visual table control.");
        var policy = request.Policy is null ? null : new RuntimeEvidencePolicyEnforcer(request.Policy);
        if (policy is not null)
        {
            var allowed = policy.AuthorizeAction(SemanticWorkflowActions.Inspect, null);
            if (!allowed.Success) return CoreResult<TableCapture>.Fail(allowed.Error!);
        }
        if (TableExcluded(control, request.Policy)) return Fail("table_excluded", "The table is excluded by the evidence policy.");
        var started = Stopwatch.GetTimestamp();
        var reads = 0;
        var reasons = new HashSet<string>(StringComparer.Ordinal);
        try
        {
            var grid = new DataGridTable(control);
            var identity = QueryIdentity(control, false);
            var columns = grid.Columns();
            var columnsRevision = TableColumnRevision(columns);
            var selected = request.Columns.Count == 0 ? columns.Take(16).ToArray()
                : request.Columns.Select(id => columns.SingleOrDefault(column => column.Id == id)
                    ?? throw new TableStop("table_column_not_found", "A requested column is unavailable or hidden.")).ToArray();
            if (request.Columns.Count == 0 && columns.Count > 16) reasons.Add("column_limit");
            foreach (var filter in request.Filters)
                if (columns.All(column => column.Id != filter.ColumnId)) throw new TableStop("table_column_not_found", "A filter column is unavailable or hidden.");
            var required = selected.Concat(request.Filters.Select(filter => columns.Single(column => column.Id == filter.ColumnId))).DistinctBy(column => column.Id).ToArray();
            var snapshot = grid.Rows(request.MaxRows, Budget);
            var sorts = grid.Sorts();
            var selectedItems = grid.SelectedItems.ToHashSet(ReferenceEqualityComparer.Instance);
            if (!snapshot.Complete) reasons.Add("row_scan_limit");
            var captured = new List<TableCapturedRow>();
            var rows = new List<RuntimeTableRow>();
            foreach (var (item, index) in snapshot.Rows.Select((item, index) => (item, index)))
            {
                Budget();
                var contents = required.ToDictionary(column => column.Id, column => grid.Cell(item, column), StringComparer.Ordinal);
                var realized = contents.Values.Any(content => content is not null && TopLevel.GetTopLevel(content) == top);
                if (request.Policy is { ExcludedControlAutomationIds.Count: > 0 } && !realized)
                { reasons.Add("unrealized_policy_scope"); continue; }
                var rowControl = contents.Values.FirstOrDefault(content => content is not null)?.GetSelfAndVisualAncestors()
                    .OfType<Control>().FirstOrDefault(ancestor => ancestor.GetType().FullName == "Avalonia.Controls.DataGridRow");
                if (rowControl is not null && TableExcluded(rowControl, request.Policy)) { reasons.Add("policy_exclusions"); continue; }
                var (key, keyStatus) = DataGridTable.Key(item, request.KeyProperty);
                if (keyStatus != "present") reasons.Add("unavailable_row_key");
                var safeKey = key is null ? null : policy?.SanitizeScalar(key) ?? key;
                if (safeKey != key) { safeKey = null; keyStatus = "redacted"; }
                var cells = new List<RuntimeTableCell>();
                foreach (var column in required)
                {
                    Budget();
                    var content = contents[column.Id];
                    var target = content is not null && TopLevel.GetTopLevel(content) == top
                        ? CreateNodeTarget(request.Table.TopLevelId, TreeKinds.Visual, top, content) : null;
                    if (content is not null && TableExcluded(content, request.Policy))
                    { reasons.Add("policy_exclusions"); cells.Add(new(column.Id, "unknown", "excluded", null, "policy", null, null)); continue; }
                    if (request.Policy is { ExcludedControlAutomationIds.Count: > 0 } && target is null)
                    { reasons.Add("unrealized_policy_scope"); cells.Add(new(column.Id, "unknown", "unavailable", null, "policy_requires_realized_cell", null, null)); continue; }
                    if (content is TextBox { PasswordChar: not '\0' } || content is not null && request.Policy is { } settings
                        && content.GetSelfAndVisualAncestors().Take(65).Any(ancestor => settings.RedactedAutomationIds.Contains(GetAutomationId(ancestor), StringComparer.Ordinal)))
                    { cells.Add(new(column.Id, "unknown", "redacted", null, "sensitive_control", target, null)); continue; }
                    var (type, state, value) = DataGridTable.Value(item, column);
                    if (value is { ValueKind: JsonValueKind.String } text && policy is not null && policy.SanitizeScalar(text.GetString()!) != text.GetString())
                    { state = "redacted"; value = null; }
                    RuntimeValidationState? validation = content is null ? null : new(DataValidationErrors.GetHasErrors(content) ? "has_errors" : "no_errors_observed",
                        "avalonia_public_data_validation_errors;async_pending_unknown", DataValidationErrors.GetHasErrors(content));
                    cells.Add(new(column.Id, type, state, value, "public_simple_column_binding_source", target, validation));
                }
                var row = new RuntimeTableRow(safeKey, keyStatus, CreateObjectGeneration(item), index, realized,
                    selectedItems.Contains(item), cells);
                captured.Add(new(item, key, row));
                var matches = true;
                foreach (var filter in request.Filters)
                {
                    var cell = cells.Single(cell => cell.ColumnId == filter.ColumnId);
                    var match = TableMatches(cell, filter);
                    if (match is null) reasons.Add("unavailable_filter_value");
                    matches &= match is true;
                }
                if (matches) rows.Add(row with { Cells = selected.Select(column => cells.Single(cell => cell.ColumnId == column.Id)).ToArray() });
            }
            var duplicates = captured.Where(row => row.Key is not null).GroupBy(row => row.Key, StringComparer.Ordinal)
                .Where(group => group.Count() > 1).Select(group => group.Key).ToHashSet(StringComparer.Ordinal);
            if (duplicates.Count > 0)
            {
                reasons.Add("ambiguous_row_keys");
                rows = rows.Select(row => row.Key is not null && duplicates.Contains(row.Key) ? row with { KeyStatus = "ambiguous" } : row).ToList();
            }
            var after = grid.Rows(request.MaxRows, Budget);
            if (!ReferenceEquals(snapshot.View, after.View) || snapshot.Complete != after.Complete || snapshot.Total != after.Total
                || !snapshot.Rows.SequenceEqual(after.Rows, ReferenceEqualityComparer.Instance) || columnsRevision != TableColumnRevision(grid.Columns())
                || JsonSerializer.Serialize(sorts) != JsonSerializer.Serialize(grid.Sorts()) || identity != QueryIdentity(control, false)
                || !selectedItems.SetEquals(grid.SelectedItems)
                || TopLevel.GetTopLevel(control) != top || !ResolveMutationTarget(top, request.Table).Success)
                throw new TableStop("table_changed", "The table, row ordering or column identities changed during the bounded read. Query again before acting.");
            foreach (var row in captured)
            {
                Budget();
                if (DataGridTable.Key(row.Item, request.KeyProperty).Key != row.Key)
                    throw new TableStop("table_changed", "A row key changed during the bounded read. Query again before acting.");
                foreach (var cell in row.Response.Cells.Where(cell => cell.Status is "present" or "null"))
                {
                    Budget();
                    var current = DataGridTable.Value(row.Item, required.Single(column => column.Id == cell.ColumnId));
                    if (current.Status != cell.Status || current.Value is null || cell.Value is null || !JsonElement.DeepEquals(current.Value.Value, cell.Value.Value))
                        throw new TableStop("table_changed", "A projected or filtered cell changed during the bounded read. Query again before acting.");
                }
            }
            // Public getters may invoke application callbacks. Check structural
            // identity again after the value verification pass as well.
            var finalRows = grid.Rows(request.MaxRows, Budget);
            if (!ReferenceEquals(snapshot.View, finalRows.View) || snapshot.Complete != finalRows.Complete || snapshot.Total != finalRows.Total
                || !snapshot.Rows.SequenceEqual(finalRows.Rows, ReferenceEqualityComparer.Instance)
                || columnsRevision != TableColumnRevision(grid.Columns()) || !selectedItems.SetEquals(grid.SelectedItems)
                || JsonSerializer.Serialize(sorts) != JsonSerializer.Serialize(grid.Sorts()) || identity != QueryIdentity(control, false)
                || TopLevel.GetTopLevel(control) != top || !ResolveMutationTarget(top, request.Table).Success)
                throw new TableStop("table_changed", "The table changed during value verification. Query again before acting.");
            var metadata = selected.Select(column => new RuntimeTableColumn(column.Id, CreateObjectGeneration(column.Column), Safe(column.Header),
                column.DisplayIndex, captured.SelectMany(row => row.Response.Cells).FirstOrDefault(cell => cell.ColumnId == column.Id)?.Type ?? "unknown",
                column.ReadOnly || !column.Supported, column.Sortable)).ToArray();
            // Hash sanitized semantic values and ordering, never timestamps, geometry or raw redacted values.
            var revision = Convert.ToHexStringLower(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new
            {
                table = request.Table.NodeGeneration, columns = metadata, sorts,
                rows = captured.Select(row => new { row.Response.Key, row.Response.KeyStatus, row.Response.Generation, row.Response.ViewIndex, row.Response.Selected,
                    cells = row.Response.Cells.Select(cell => new { cell.ColumnId, cell.Type, cell.Status, cell.Value }) })
            })));
            if (request.ExpectedRevision is not null && !string.Equals(request.ExpectedRevision, revision, StringComparison.OrdinalIgnoreCase))
                return Fail("table_revision_changed", "The bounded table revision changed. Restart paging or observe before a new action.");
            var page = rows.Skip(request.Offset).Take(request.Limit).ToList();
            if (request.Offset + page.Count < rows.Count) reasons.Add("page_limit");
            RuntimeTableQueryResponse Response() => new(resolved.Value.Target, metadata, page.ToArray(), sorts,
                new("available_public_collection_view", "unknown", snapshot.Complete && !reasons.Any(reason => reason is "unavailable_filter_value" or "policy_exclusions" or "unrealized_policy_scope"),
                    snapshot.Rows.Length, rows.Count, snapshot.Total, reasons.Order(StringComparer.Ordinal).ToArray()), revision,
                request.Offset + page.Count < rows.Count && page.Count > 0 ? request.Offset + page.Count : null, DateTimeOffset.UtcNow);
            var response = Response();
            while (page.Count > 0 && JsonSerializer.SerializeToUtf8Bytes(response).Length > 65536)
            { page.RemoveAt(page.Count - 1); reasons.Add("response_byte_limit"); response = Response(); }
            if (JsonSerializer.SerializeToUtf8Bytes(response).Length > 65536)
                return Fail("table_response_limit", "Column metadata exceeds the 64 KiB response budget. Select fewer columns.");
            return CoreResult<TableCapture>.Ok(new(grid, columns, captured, snapshot.Rows, snapshot.Complete, response));
        }
        catch (TableStop exception) { return Fail(exception.Code, exception.Message); }
        catch (NotSupportedException exception) { return Fail("table_unsupported", exception.Message); }
        catch (Exception exception) when (exception is not OutOfMemoryException and not AccessViolationException)
        { return Fail("table_read_failed", "A public table, column or scalar property could not be read safely. No input was dispatched."); }

        void Budget()
        {
            if (++reads > 32768 || Stopwatch.GetElapsedTime(started) > TimeSpan.FromSeconds(2))
                throw new TableStop("table_analysis_budget", "The cooperative two-second/32768-work-item table analysis budget expired. Narrow the query.");
        }
        string? Safe(string? value)
        {
            var safe = value is null ? null : policy?.SanitizeScalar(value) ?? value;
            if (safe?.Length > 512) { reasons.Add("text_truncated"); return safe[..512]; }
            return safe;
        }
        static CoreResult<TableCapture> Fail(string code, string message) => CoreResult<TableCapture>.Fail(new(code, message));
    }

    private static bool TableExcluded(Visual node, RuntimeEvidencePolicy? policy)
    {
        if (policy is not { ExcludedControlAutomationIds.Count: > 0 }) return false;
        var ancestors = node.GetSelfAndVisualAncestors().Take(65).ToArray();
        return ancestors.Length > 64 || ancestors.Any(ancestor => policy.ExcludedControlAutomationIds.Contains(GetAutomationId(ancestor), StringComparer.Ordinal));
    }

    private static bool? TableMatches(RuntimeTableCell cell, RuntimeTableFilter filter)
    {
        if (cell.Status is not ("present" or "null") || cell.Value is null) return null;
        var value = cell.Value.Value;
        if (filter.Operation == "equals") return JsonElement.DeepEquals(value, filter.Value);
        if (filter.Operation == "not_equals") return !JsonElement.DeepEquals(value, filter.Value);
        if (filter.Operation == "contains") return value.ValueKind == JsonValueKind.String && filter.Value.ValueKind == JsonValueKind.String
            ? value.GetString()!.Contains(filter.Value.GetString()!, StringComparison.Ordinal) : null;
        if (value.ValueKind != JsonValueKind.Number || filter.Value.ValueKind != JsonValueKind.Number) return null;
        var comparison = value.TryGetDecimal(out var left) && filter.Value.TryGetDecimal(out var right)
            ? left.CompareTo(right) : value.GetDouble().CompareTo(filter.Value.GetDouble());
        return filter.Operation == "greater_than" ? comparison > 0 : comparison < 0;
    }

    private static string TableColumnRevision(IReadOnlyList<DataGridColumnInfo> columns) => JsonSerializer.Serialize(columns.Select(column => new
    {
        generation = CreateObjectGeneration(column.Column), column.Id, column.Path, header = column.Header is { Length: > 512 } text ? text[..512] : column.Header,
        column.DisplayIndex, column.Supported, column.ReadOnly, column.Sortable, sortPath = column.SortPath is { Length: > 512 } ? null : column.SortPath
    }));
    private sealed record TableCapturedRow(object Item, string? Key, RuntimeTableRow Response);
    private sealed record TableCapture(DataGridTable Grid, IReadOnlyList<DataGridColumnInfo> Columns, IReadOnlyList<TableCapturedRow> Rows,
        IReadOnlyList<object> Items, bool Complete, RuntimeTableQueryResponse Response);
    private sealed class TableStop(string code, string message) : Exception(message) { public string Code { get; } = code; }
}
