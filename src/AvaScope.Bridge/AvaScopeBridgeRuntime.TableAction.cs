using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Threading;
using AvaScope.Core;
using AvaScope.Protocol;

namespace AvaScope.Bridge;

public sealed partial class AvaScopeBridgeRuntime
{
    private readonly Dictionary<string, (string Fingerprint, CoreResult<RuntimeTableActionResponse>? Result)> _tableRequests = new(StringComparer.Ordinal);

    public async Task<CoreResult<RuntimeTableActionResponse>> TableActionAsync(RuntimeTableActionRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!await _explicitInputGate.WaitAsync(0, cancellationToken))
            return CoreResult<RuntimeTableActionResponse>.Fail(new("table_action_busy", "Another compound input operation is active; wait for its result."));
        try
        {
            var task = await Dispatcher.UIThread.InvokeAsync<Task<CoreResult<RuntimeTableActionResponse>>>(() => TableAction(request, cancellationToken), DispatcherPriority.Background, cancellationToken);
            return await task;
        }
        finally { _explicitInputGate.Release(); }
    }

    private async Task<CoreResult<RuntimeTableActionResponse>> TableAction(RuntimeTableActionRequest request, CancellationToken cancellationToken)
    {
        Dispatcher.UIThread.VerifyAccess();
        if (request.Query.Table.SessionId != SessionId) return Fail("table_session_mismatch", "Select this bridge session explicitly.");
        if (request.Query.Policy is { } policy && !policy.AllowedTableActions.Contains(request.Action, StringComparer.Ordinal))
            return Fail("table_action_policy_denied", "The policy must explicitly allow this table action.");
        var bytes = JsonSerializer.SerializeToUtf8Bytes(request);
        if (bytes.Length > 65536) return Fail("table_action_request_limit", "Table action requests are limited to 64 KiB.");
        var fingerprint = Convert.ToHexStringLower(SHA256.HashData(bytes));
        if (_tableRequests.TryGetValue(request.RequestId, out var previous))
        {
            if (previous.Fingerprint != fingerprint) return Fail("table_action_conflict", "This request id belongs to a different payload. Inspect the original result before a new intent.");
            if (previous.Result is null) return Fail("table_action_pending", "This request has started and its result is pending. Do not dispatch it with a new id.");
            return previous.Result.Success ? CoreResult<RuntimeTableActionResponse>.Ok(previous.Result.Value! with { Replayed = true }) : previous.Result;
        }
        if (_tableRequests.Count == 128) return Fail("table_action_ledger_full", "This session retains 128 table results and cannot accept a new id. Existing ids remain replayable.");
        var top = FindTopLevel(request.Query.Table.TopLevelId);
        if (top is null) return TopLevelNotFound<RuntimeTableActionResponse>(request.Query.Table.TopLevelId);
        var provenance = RuntimePlatformEvidence.Operation(top, RuntimeOperationRoutes.ControlApi, false);
        _tableRequests.Add(request.RequestId, (fingerprint, null));
        var started = Stopwatch.GetTimestamp();
        var operations = 0;
        var preparation = false;
        var verified = false;
        var intentDispatched = false;
        var status = "rejected";
        TableCapture? before = null;
        TableCapture? current = null;
        TableCapturedRow? row = null;
        DataGridColumnInfo? column = null;
        var diagnostics = new List<ProtocolError>();
        // A revision guards the initial decision. Once this request changes state,
        // subsequent verification must observe the new revision instead.
        var query = new RuntimeTableQueryRequest(request.Query.Table, request.Query.KeyProperty, request.Query.Columns,
            request.Query.Filters, request.Query.Offset, request.Query.Limit, request.Query.MaxRows, policy: request.Query.Policy);
        try
        {
            before = current = Capture(request.Query);
            Resolve(current);
            var grid = current.Grid;
            if (Satisfied(current)) { verified = true; status = "already_satisfied"; return Complete(); }
            if (grid.Editing) throw new TableStop("table_edit_active", "An application edit/add transaction is active. Finish it explicitly before a table action.");
            if (request.Action == "sort")
            {
                if (!column!.Sortable) throw new TableStop("table_sort_unsupported", "The selected column does not allow public user sorting.");
                Dispatch(() => grid.Sort(column, request.Direction!), intent: true);
            }
            else
            {
                if (request.Action == "edit_cell")
                {
                    if (column!.ReadOnly || !column.Supported) throw new TableStop("table_cell_read_only", "The selected cell does not expose a supported writable public binding/editor.");
                    var cell = row!.Response.Cells.SingleOrDefault(cell => cell.ColumnId == column.Id);
                    if (cell is null || cell.Status is not ("present" or "null"))
                        throw new TableStop("table_cell_unavailable", "Include the cell in the query projection and resolve its supported typed value first.");
                    var raw = DataGridTable.Value(row.Item, column);
                    if (request.Desired!.Value.ValueKind != JsonValueKind.Null && (raw.Type == "string" && request.Desired.Value.ValueKind != JsonValueKind.String
                        || raw.Type == "boolean" && request.Desired.Value.ValueKind is not (JsonValueKind.True or JsonValueKind.False)
                        || raw.Type == "number" && request.Desired.Value.ValueKind != JsonValueKind.Number))
                        throw new TableStop("table_cell_type", "The desired value must match the cell's exposed scalar type.");
                }
                var item = row!.Item;
                var observedValue = column is null ? (JsonElement?)null : DataGridTable.Value(item, column).Value;
                var content = column is null ? current.Columns.Select(candidate => grid.Cell(item, candidate)).FirstOrDefault(candidate => candidate is not null) : grid.Cell(item, column);
                if (content is null || CreateInteractionState(top!, content)?.Rendered != true)
                {
                    preparation = true;
                    Dispatch(() => grid.Reveal(item, column));
                    do
                    {
                        await Task.Delay(20, cancellationToken); CheckTime();
                        current = Capture(query); Resolve(current);
                        content = column is null ? current.Columns.Select(candidate => grid.Cell(item, candidate)).FirstOrDefault(candidate => candidate is not null)
                            : grid.Cell(item, column);
                    } while (content is null || CreateInteractionState(top!, content)?.Rendered != true);
                }
                current = Capture(query); Resolve(current);
                if (!ReferenceEquals(row.Item, item)) throw new TableStop("table_row_changed", "The logical row was replaced during realization.");
                if (column is not null && !SameValue(observedValue, DataGridTable.Value(item, column).Value))
                    throw new TableStop("table_cell_changed", "The cell changed during realization; inspect before editing.");
                if (!ReferenceEquals(grid.SelectedItem, item) || grid.SelectedItems.Count != 1)
                {
                    preparation |= request.Action == "edit_cell";
                    Dispatch(() => grid.Select(item), intent: request.Action == "select_row");
                }
                if (request.Action == "edit_cell")
                {
                    preparation = true;
                    current = Capture(query); Resolve(current);
                    if (!SameValue(observedValue, DataGridTable.Value(item, column!).Value))
                        throw new TableStop("table_cell_changed", "Selection callbacks changed the cell; no editor input was dispatched.");
                    Dispatch(() => grid.SetCurrentColumn(column!));
                    var began = false;
                    Dispatch(() => began = grid.BeginEdit());
                    if (!began) throw new TableStop("table_edit_rejected", "The control rejected BeginEdit. Selection or focus preparation may have occurred.");
                    await Task.Delay(20, cancellationToken); CheckTime();
                    current = Capture(query); Resolve(current);
                    if (DataGridTable.Read(grid.View, "CurrentEditItem") is { } editing && !ReferenceEquals(editing, item))
                        throw new TableStop("table_edit_changed", "The table is editing a different row; no cell input was dispatched.");
                    if (!SameValue(observedValue, DataGridTable.Value(item, column!).Value))
                        throw new TableStop("table_cell_changed", "The cell changed while its editor was opening. Its draft remains for explicit inspection.");
                    var editor = grid.Cell(item, column!);
                    var property = editor is TextBox ? "text" : editor is ToggleButton ? "checked" : null;
                    if (property is null || editor is TextBox { PasswordChar: not '\0' } || TableExcluded(editor!, request.Query.Policy))
                        throw new TableStop("table_editor_unsupported", "The public cell editor is unsupported or sensitive/excluded. Inspect its draft; no cell input was dispatched.");
                    var desired = property == "text" ? JsonSerializer.SerializeToElement(EditText(request.Desired!.Value)) : request.Desired!.Value;
                    var editorRequest = new RuntimeDesiredStateRequest(CreateNodeTarget(request.Query.Table.TopLevelId, TreeKinds.Visual, top!, editor!),
                        property, desired, "table-" + fingerprint[..32], request.Query.Policy);
                    var validation = EnsureState(editorRequest, validateOnly: true);
                    if (!validation.Success || validation.Value!.Status is not ("validated" or "already_satisfied"))
                        throw new TableStop("table_editor_rejected", "The cell editor failed input prevalidation. Inspect its current draft explicitly.");
                    CheckTime();
                    // The table ledger owns this compound request; do not create an
                    // independently replayable field intent or require form permissions.
                    intentDispatched = true;
                    var execution = EnsureState(editorRequest);
                    if (!execution.Success) throw new TableStop("table_editor_uncertain", "The editor operation did not return a verified result; inspect the retained draft.");
                    operations += execution.Value!.DispatchedOperations;
                    if (!execution.Value.Verified) throw new TableStop("table_editor_not_verified", "The editor rejected or did not verify the desired value. Earlier preparation remains visible.");
                    CheckTime();
                    current = Capture(query); Resolve(current);
                    if (!ReferenceEquals(grid.SelectedItem, item) || DataGridTable.Read(grid.View, "CurrentEditItem") is { } activeEdit && !ReferenceEquals(activeEdit, item))
                        throw new TableStop("table_edit_changed", "The edit owner changed before commit. No additional commit was dispatched.");
                    var committed = false;
                    Dispatch(() => committed = grid.CommitEdit(), ownedEdit: true);
                    if (!committed || !grid.Valid) throw new TableStop("table_validation_rejected", "The control rejected commit or reports validation errors. Its draft/earlier effects were not rolled back.");
                }
            }
            do
            {
                CheckTime(); current = Capture(query); Resolve(current);
                if (Satisfied(current)) { verified = true; status = "verified"; break; }
                await Task.Delay(25, cancellationToken);
            } while (true);
        }
        catch (Exception exception) when (exception is not OutOfMemoryException and not AccessViolationException)
        {
            status = operations > 0 || preparation ? "uncertain" : "rejected";
            if (exception is TableStop { Code: "table_validation_rejected" or "table_editor_not_verified" }) status = "not_verified";
            var details = exception is TableStop { Details: { } sourceDetails }
                ? new Dictionary<string, string>(sourceDetails, StringComparer.Ordinal) : new Dictionary<string, string>(StringComparer.Ordinal);
            try { details["tableEditing"] = current is null ? "unknown" : current.Grid.Editing ? "true" : "false"; }
            catch (Exception stateError) when (stateError is not OutOfMemoryException and not AccessViolationException) { details["tableEditing"] = "unknown"; }
            details["intentDispatched"] = intentDispatched ? "true" : "false";
            details["tableRecovery"] = "Inspect and explicitly finish or cancel any pending draft before a new intent. Re-query with complete coverage. Reuse the original request id only to retrieve its retained outcome.";
            diagnostics.Add(new(exception is TableStop stop ? stop.Code : exception is OperationCanceledException ? "table_action_cancelled" : "table_action_failed",
                exception is TableStop ? exception.Message : "The public table operation failed or exceeded its deadline. Inspect the current table and any pending draft before a new intent.", details));
        }
        return Complete();

        TableCapture Capture(RuntimeTableQueryRequest options)
        {
            CheckTime();
            var result = CaptureTable(options);
            if (!result.Success) throw new TableStop(result.Error!.Code, result.Error.Message, result.Error.Details);
            return result.Value!;
        }
        void Resolve(TableCapture capture)
        {
            CheckTime();
            if (!capture.Complete || capture.Items.Count != capture.Rows.Count || capture.Rows.Any(item => item.Key is null || item.Response.KeyStatus != "present")
                || capture.Response.Coverage.Reasons.Contains("ambiguous_row_keys"))
                throw new TableStop("table_identity_incomplete", "Actions require a complete bounded available view with unique readable row identities. Narrow the UI data or increase maxRows.");
            if (request.RowKey is not null)
            {
                var matching = capture.Rows.Where(item => item.Key == request.RowKey).ToArray();
                if (matching.Length != 1 || matching[0].Response.Generation != request.RowGeneration)
                    throw new TableStop("table_row_changed", "The row key is absent, ambiguous or belongs to a replacement row. Observe again.");
                row = matching[0];
                if (!intentDispatched && request.Query.Filters.Any(filter => row.Response.Cells.All(cell => cell.ColumnId != filter.ColumnId || TableMatches(cell, filter) is not true)))
                    throw new TableStop("table_row_filter_changed", "The row no longer meets the explicit query filters.");
            }
            if (request.ColumnId is not null)
            {
                column = capture.Columns.SingleOrDefault(item => item.Id == request.ColumnId);
                if (column is null || CreateObjectGeneration(column.Column) != request.ColumnGeneration)
                    throw new TableStop("table_column_changed", "The column identity changed. Observe again.");
                if (request.Action == "edit_cell" && !intentDispatched && column.ReadOnly)
                    throw new TableStop("table_cell_read_only", "The column became read-only before editor input.");
                if (request.Action == "edit_cell" && row?.Response.Cells.SingleOrDefault(cell => cell.ColumnId == column.Id)?.Status is not ("present" or "null"))
                    throw new TableStop("table_cell_unavailable", "A projected readable cell is required; redacted or unavailable values cannot authorize edits or value comparisons.");
            }
        }
        bool Satisfied(TableCapture capture) => request.Action switch
        {
            "sort" => column is not null && capture.Response.Sorts.Count == 1 && capture.Response.Sorts[0].MemberPath == column.SortPath && capture.Response.Sorts[0].Direction == request.Direction,
            "select_row" => row is not null && ReferenceEquals(capture.Grid.SelectedItem, row.Item) && capture.Grid.SelectedItems.Count == 1,
            "edit_cell" => row is not null && column is not null && capture.Grid.Valid && !capture.Grid.Editing
                && SameValue(DataGridTable.Value(row.Item, column).Value, request.Desired),
            _ => false
        };
        void Dispatch(Action action, bool ownedEdit = false, bool intent = false)
        {
            CheckTime();
            current = Capture(query); Resolve(current);
            if (!ownedEdit && current.Grid.Editing) throw new TableStop("table_edit_active", "Another edit/add transaction became active before dispatch.");
            if (InputBlocker(top!, request.Query.Table.TopLevelId, current.Grid.Control, InputActions.Invoke) is not null)
                throw new TableStop("table_action_blocked", "The table is blocked or not actionable; inspect its current state before continuing.");
            CheckTime(); intentDispatched |= intent; operations++; action();
        }
        void CheckTime()
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (Stopwatch.GetElapsedTime(started).TotalMilliseconds > request.TimeoutMs)
                throw new TableStop("table_action_timeout", "The bounded table action deadline expired. Observe current values, ordering and any pending edit before a new intent.");
        }
        CoreResult<RuntimeTableActionResponse> Complete()
        {
            try
            {
                var latest = CaptureTable(query);
                if (latest.Success) current = latest.Value;
                else if (verified) { verified = false; status = "uncertain"; diagnostics.Add(new(latest.Error!.Code, latest.Error.Message, latest.Error.Details)); }
            }
            catch (Exception exception) when (exception is not OutOfMemoryException and not AccessViolationException)
            { verified = false; status = "uncertain"; diagnostics.Add(new("table_final_state_unavailable", "The final public table observation failed. Inspect the current state before another intent.")); }
            var beforeRows = request.RowKey is null ? [] : before?.Rows.Where(item => item.Key == request.RowKey).ToArray() ?? [];
            var beforeRow = beforeRows.Length == 1 ? beforeRows[0].Response : null;
            var afterRows = request.RowKey is null ? [] : current?.Rows.Where(item => item.Key == request.RowKey).ToArray() ?? [];
            var afterRow = afterRows.Length == 1 ? afterRows[0].Response : null;
            if (verified && (afterRows.Length > 1 || request.RowKey is not null && (afterRow is null || afterRow.Generation != request.RowGeneration)))
            { verified = false; status = "uncertain"; }
            if (verified && current is not null)
            {
                try
                {
                    row = afterRows.FirstOrDefault();
                    column = request.ColumnId is null ? null : current.Columns.SingleOrDefault(item => item.Id == request.ColumnId);
                    if (request.ColumnId is not null && (column is null || CreateObjectGeneration(column.Column) != request.ColumnGeneration) || !Satisfied(current))
                    { verified = false; status = "uncertain"; diagnostics.Add(new("table_final_state_changed", "The final observation no longer verifies the requested table state.")); }
                }
                catch (Exception exception) when (exception is not OutOfMemoryException and not AccessViolationException)
                { verified = false; status = "uncertain"; diagnostics.Add(new("table_final_state_unavailable", "The final public state could not be verified.")); }
            }
            var response = new RuntimeTableActionResponse(request.RequestId, request.Query.Table, request.Action, status, verified,
                Compact(beforeRow), Compact(afterRow), before?.Response.Sorts ?? [], current?.Response.Sorts ?? [], operations, preparation,
                provenance with { Dispatched = operations > 0, Route = operations > 0 ? RuntimeOperationRoutes.ControlApi : RuntimeOperationRoutes.NotDispatched,
                    PlannedRoute = operations > 0 ? null : RuntimeOperationRoutes.ControlApi }, diagnostics, DateTimeOffset.UtcNow);
            CoreResult<RuntimeTableActionResponse> result = request.Query.Policy is null ? CoreResult<RuntimeTableActionResponse>.Ok(response)
                : new RuntimeEvidencePolicyEnforcer(request.Query.Policy).Sanitize(response);
            if (result.Success && JsonSerializer.SerializeToUtf8Bytes(result.Value).Length > 65536)
            {
                var bounded = result.Value! with { Before = OmitCells(result.Value.Before), After = OmitCells(result.Value.After) };
                result = JsonSerializer.SerializeToUtf8Bytes(bounded).Length <= 65536 ? CoreResult<RuntimeTableActionResponse>.Ok(bounded)
                    : Fail("table_action_response_limit", "The completed result exceeded 64 KiB even after omitting cell values. Observe current state; this intent will not be dispatched again.");
            }
            _tableRequests[request.RequestId] = (fingerprint, result);
            return result;
        }
        static string EditText(JsonElement value) => value.ValueKind == JsonValueKind.Null ? string.Empty : value.ValueKind == JsonValueKind.String ? value.GetString()!
            : value.ValueKind == JsonValueKind.Number && value.TryGetDecimal(out var number) ? number.ToString(CultureInfo.CurrentCulture) : value.GetRawText();
        static RuntimeTableRow? Compact(RuntimeTableRow? value) => value is null ? null : value with
        {
            Cells = value.Cells.Select(cell => cell.Value is { } json && json.GetRawText().Length > 3072
                ? cell with { Status = "omitted_response_budget", Value = null } : cell).ToArray()
        };
        static RuntimeTableRow? OmitCells(RuntimeTableRow? value) => value is null ? null : value with
        {
            Cells = value.Cells.Select(cell => cell with { Status = "omitted_response_budget", Value = null, Target = null }).ToArray()
        };
        static CoreResult<RuntimeTableActionResponse> Fail(string code, string message) => CoreResult<RuntimeTableActionResponse>.Fail(new(code, message));
    }

    private static bool SameValue(JsonElement? left, JsonElement? right) => left is not null && right is not null && JsonElement.DeepEquals(left.Value, right.Value);
}
