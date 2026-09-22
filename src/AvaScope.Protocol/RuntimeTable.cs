using System.Text.Json;
using System.Text.Json.Serialization;

namespace AvaScope.Protocol;

public sealed record RuntimeTableFilter
{
    [JsonConstructor]
    public RuntimeTableFilter(string columnId, string operation, JsonElement value)
    {
        if (string.IsNullOrWhiteSpace(columnId) || columnId.Length > 256) throw new ArgumentException("A bounded column identity is required.");
        if (operation is not ("equals" or "not_equals" or "contains" or "greater_than" or "less_than")) throw new ArgumentException("Unsupported table filter operation.");
        ValidateValue(value);
        ColumnId = columnId; Operation = operation; Value = value.Clone();
    }
    [JsonPropertyName("columnId")] public string ColumnId { get; }
    [JsonPropertyName("operation")] public string Operation { get; }
    [JsonPropertyName("value")] public JsonElement Value { get; }

    internal static void ValidateValue(JsonElement value)
    {
        if (value.ValueKind is JsonValueKind.Undefined or JsonValueKind.Array or JsonValueKind.Object || value.GetRawText().Length > 16384
            || value.ValueKind == JsonValueKind.String && value.GetString()!.Length > 4096
            || value.ValueKind == JsonValueKind.Number && (!value.TryGetDouble(out var number) || !double.IsFinite(number)))
            throw new ArgumentException("Table values must be bounded scalar JSON values; numbers must be finite.");
    }
}

public sealed record RuntimeTableQueryRequest
{
    [JsonConstructor]
    public RuntimeTableQueryRequest(RuntimeTargetContext table, string keyProperty, IReadOnlyList<string>? columns = null,
        IReadOnlyList<RuntimeTableFilter>? filters = null, int offset = 0, int limit = 25, int maxRows = 512,
        string? expectedRevision = null, RuntimeEvidencePolicy? policy = null)
    {
        Table = table ?? throw new ArgumentNullException(nameof(table));
        if (table.TreeKind != TreeKinds.Visual || table.NodeId is null || table.NodeGeneration is null || table.TopLevelGeneration is null)
            throw new ArgumentException("Select a fresh generation-bearing visual table target.");
        if (string.IsNullOrWhiteSpace(keyProperty) || keyProperty.Length > 128 || !char.IsLetter(keyProperty[0]) && keyProperty[0] != '_'
            || keyProperty.Any(character => !char.IsLetterOrDigit(character) && character != '_'))
            throw new ArgumentException("Row identity requires one explicit public scalar key property; paths and indexers are not accepted.");
        Columns = columns?.ToArray() ?? [];
        Filters = filters?.ToArray() ?? [];
        if (Columns.Count > 16 || Columns.Any(column => string.IsNullOrWhiteSpace(column) || column.Length > 256)
            || Columns.Distinct(StringComparer.Ordinal).Count() != Columns.Count || Filters.Count > 4 || Filters.Any(filter => filter is null))
            throw new ArgumentException("Select up to 16 distinct columns and four non-null AND filters.");
        if (offset is < 0 or > 4096 || limit is < 1 or > 64 || maxRows is < 1 or > 4096)
            throw new ArgumentException("Table queries allow offset 0..4096, limit 1..64 and maxRows 1..4096.");
        if (expectedRevision is not null && (expectedRevision.Length != 64 || expectedRevision.Any(character => !char.IsAsciiHexDigit(character))))
            throw new ArgumentException("A table revision must be the returned 64-character digest.");
        KeyProperty = keyProperty; Offset = offset; Limit = limit; MaxRows = maxRows; ExpectedRevision = expectedRevision; Policy = policy;
    }
    [JsonPropertyName("table")] public RuntimeTargetContext Table { get; }
    [JsonPropertyName("keyProperty")] public string KeyProperty { get; }
    [JsonPropertyName("columns")] public IReadOnlyList<string> Columns { get; }
    [JsonPropertyName("filters")] public IReadOnlyList<RuntimeTableFilter> Filters { get; }
    [JsonPropertyName("offset")] public int Offset { get; }
    [JsonPropertyName("limit")] public int Limit { get; }
    [JsonPropertyName("maxRows")] public int MaxRows { get; }
    [JsonPropertyName("expectedRevision")] public string? ExpectedRevision { get; }
    [JsonPropertyName("policy")] public RuntimeEvidencePolicy? Policy { get; }
}

public sealed record RuntimeTableActionRequest
{
    [JsonConstructor]
    public RuntimeTableActionRequest(RuntimeTableQueryRequest query, string action, string requestId, string? rowKey = null,
        string? rowGeneration = null, string? columnId = null, string? columnGeneration = null, JsonElement? desired = null,
        string? direction = null, int timeoutMs = 2000)
    {
        Query = query ?? throw new ArgumentNullException(nameof(query));
        if (action is not ("select_row" or "edit_cell" or "sort")) throw new ArgumentException("Unsupported table action.");
        if (string.IsNullOrWhiteSpace(requestId) || requestId.Length > 128) throw new ArgumentException("A table action request id of 1..128 characters is required.");
        if (action is "select_row" or "edit_cell" && (string.IsNullOrWhiteSpace(rowKey) || rowKey.Length > 256 || string.IsNullOrWhiteSpace(rowGeneration) || rowGeneration.Length > 256))
            throw new ArgumentException("Row actions require the observed stable row key and row generation.");
        if (action is "edit_cell" or "sort" && (string.IsNullOrWhiteSpace(columnId) || columnId.Length > 256 || string.IsNullOrWhiteSpace(columnGeneration) || columnGeneration.Length > 256))
            throw new ArgumentException("Column actions require the observed column id and column generation.");
        if (action == "edit_cell")
        {
            if (desired is null) throw new ArgumentException("Cell edits require an explicit desired value.");
            RuntimeTableFilter.ValidateValue(desired.Value);
        }
        else if (desired is not null) throw new ArgumentException("Only cell editing accepts a desired value.");
        if (action == "sort" ? direction is not ("ascending" or "descending") : direction is not null)
            throw new ArgumentException("Sorting requires ascending or descending; other actions do not accept a sort direction.");
        if (timeoutMs is < 100 or > 3000) throw new ArgumentOutOfRangeException(nameof(timeoutMs));
        Action = action; RequestId = requestId; RowKey = rowKey; RowGeneration = rowGeneration;
        ColumnId = columnId; ColumnGeneration = columnGeneration; Desired = desired?.Clone(); Direction = direction; TimeoutMs = timeoutMs;
    }
    [JsonPropertyName("query")] public RuntimeTableQueryRequest Query { get; }
    [JsonPropertyName("action")] public string Action { get; }
    [JsonPropertyName("requestId")] public string RequestId { get; }
    [JsonPropertyName("rowKey")] public string? RowKey { get; }
    [JsonPropertyName("rowGeneration")] public string? RowGeneration { get; }
    [JsonPropertyName("columnId")] public string? ColumnId { get; }
    [JsonPropertyName("columnGeneration")] public string? ColumnGeneration { get; }
    [JsonPropertyName("desired")] public JsonElement? Desired { get; }
    [JsonPropertyName("direction")] public string? Direction { get; }
    [JsonPropertyName("timeoutMs")] public int TimeoutMs { get; }
}

public sealed record RuntimeTableColumn(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("generation")] string Generation,
    [property: JsonPropertyName("header")] string? Header,
    [property: JsonPropertyName("displayIndex")] int DisplayIndex,
    [property: JsonPropertyName("valueType")] string ValueType,
    [property: JsonPropertyName("readOnly")] bool ReadOnly,
    [property: JsonPropertyName("sortable")] bool Sortable);

public sealed record RuntimeTableCell(
    [property: JsonPropertyName("columnId")] string ColumnId,
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("value")] JsonElement? Value,
    [property: JsonPropertyName("provenance")] string Provenance,
    [property: JsonPropertyName("target")] RuntimeTargetContext? Target,
    [property: JsonPropertyName("validation")] RuntimeValidationState? Validation);

public sealed record RuntimeTableRow(
    [property: JsonPropertyName("key")] string? Key,
    [property: JsonPropertyName("keyStatus")] string KeyStatus,
    [property: JsonPropertyName("generation")] string Generation,
    [property: JsonPropertyName("viewIndex")] int ViewIndex,
    [property: JsonPropertyName("realized")] bool Realized,
    [property: JsonPropertyName("selected")] bool Selected,
    [property: JsonPropertyName("cells")] IReadOnlyList<RuntimeTableCell> Cells);

public sealed record RuntimeTableSort(
    [property: JsonPropertyName("memberPath")] string? MemberPath,
    [property: JsonPropertyName("direction")] string Direction);

public sealed record RuntimeTableCoverage(
    [property: JsonPropertyName("scope")] string Scope,
    [property: JsonPropertyName("datasetCompleteness")] string DatasetCompleteness,
    [property: JsonPropertyName("completeAvailableView")] bool CompleteAvailableView,
    [property: JsonPropertyName("scannedRows")] int ScannedRows,
    [property: JsonPropertyName("matchedRowsAtLeast")] int MatchedRowsAtLeast,
    [property: JsonPropertyName("totalAvailableRows")] int? TotalAvailableRows,
    [property: JsonPropertyName("reasons")] IReadOnlyList<string> Reasons);

public sealed record RuntimeTableQueryResponse(
    [property: JsonPropertyName("table")] RuntimeTargetContext Table,
    [property: JsonPropertyName("columns")] IReadOnlyList<RuntimeTableColumn> Columns,
    [property: JsonPropertyName("rows")] IReadOnlyList<RuntimeTableRow> Rows,
    [property: JsonPropertyName("sorts")] IReadOnlyList<RuntimeTableSort> Sorts,
    [property: JsonPropertyName("coverage")] RuntimeTableCoverage Coverage,
    [property: JsonPropertyName("revision")] string Revision,
    [property: JsonPropertyName("nextOffset")] int? NextOffset,
    [property: JsonPropertyName("observedAt")] DateTimeOffset ObservedAt);

public sealed record RuntimeTableActionResponse(
    [property: JsonPropertyName("requestId")] string RequestId,
    [property: JsonPropertyName("table")] RuntimeTargetContext Table,
    [property: JsonPropertyName("action")] string Action,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("verified")] bool Verified,
    [property: JsonPropertyName("before")] RuntimeTableRow? Before,
    [property: JsonPropertyName("after")] RuntimeTableRow? After,
    [property: JsonPropertyName("sortsBefore")] IReadOnlyList<RuntimeTableSort> SortsBefore,
    [property: JsonPropertyName("sortsAfter")] IReadOnlyList<RuntimeTableSort> SortsAfter,
    [property: JsonPropertyName("dispatchedOperations")] int DispatchedOperations,
    [property: JsonPropertyName("preparationPerformed")] bool PreparationPerformed,
    [property: JsonPropertyName("provenance")] RuntimeOperationProvenance Provenance,
    [property: JsonPropertyName("diagnostics")] IReadOnlyList<ProtocolError> Diagnostics,
    [property: JsonPropertyName("observedAt")] DateTimeOffset ObservedAt,
    [property: JsonPropertyName("replayed")] bool Replayed = false);
