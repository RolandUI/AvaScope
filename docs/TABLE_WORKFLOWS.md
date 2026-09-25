# Runtime table workflows

`runtime.tables` provides MCP `query_table` / `table_action` and CLI
`avascope query-table --request query.json` / `avascope table-action --request action.json`.
CLI accepts `--manifest-dir`; MCP accepts `manifestDirectory`. Table actions use the
existing session control lease. Activation remains an explicit host decision.

## Supported data boundary

The initial adapter uses the host's optional Avalonia DataGrid 12.1.x assembly through
public APIs. AvaScope.Bridge has no DataGrid package dependency. The native fixture
uses DataGrid 12.1.2 with Avalonia 12.1.3; the bridge retains its Avalonia 12.1.0
compilation baseline and standalone compatibility range `[12.1.0,12.2.0)`.

Supported cells are standard `DataGridTextColumn` and `DataGridCheckBoxColumn` with
one public scalar property on a reference-type row, through a reflection or compiled
row-context binding. No private store, arbitrary path/indexer, method or data-source
write is used. Custom templates, explicit source/ancestor bindings, format strings
and custom converters return `unsupported_binding`. Scalar types include strings,
booleans, finite numbers, named enums, GUIDs and dates. Values identify their public
binding-source provenance; they are not reconstructed from displayed pixels.

The row key is an explicitly named public string, GUID or integer property. Column
identity uses a bounded string `Tag`, then a simple binding path, then string header:
`tag:...`, `binding:...`, `header:...`. Duplicate column identities reject the read.
Duplicate row keys are reported as ambiguous and cannot authorize an action.

## Read, filter and page

Find the table through `find_nodes` or `query_nodes` and preserve its returned visual
target, including session, top-level, node and both generations:

```json
{
  "table": "<replace with the returned target object>",
  "keyProperty": "Id",
  "columns": ["binding:Status", "binding:Amount"],
  "filters": [{"columnId": "binding:Status", "operation": "equals", "value": "failed"}],
  "offset": 0,
  "limit": 25,
  "maxRows": 512
}
```

Filters use AND semantics and typed scalar equality, inequality, ordinal string
`contains`, or numeric `greater_than` / `less_than`. Missing, unsupported or redacted
values cannot prove a match; `unavailable_filter_value` makes this explicit. Policy
redaction is applied before matching, so filtering cannot disclose a protected value.
Control exclusions require realized cell scope; offscreen rows cannot establish that
scope and remain unavailable under such a policy. Password editors are never read.

The result contains typed cells, optional realized cell targets, selection, column
metadata, current public sort descriptions and coverage. `scope` is
`available_public_collection_view`; `datasetCompleteness` is always `unknown`.
`completeAvailableView` means the bounded public view was exhausted without hidden
filter/policy coverage. It does not claim that a server loaded every record.
`realized` describes individual rows. Reading never scrolls or fetches more data.

Limits are 4096 scanned rows (default 512), 64 page rows (default 25), 16 projected
columns, four filters, 64 column identities, eight sort descriptors, 64 selected
rows and 64 KiB per response. Strings are limited to 4096 characters. Reads have a
cooperative two-second / 32768-work-item budget; application getters cannot be
preempted. `reasons` explains row/page/column/byte limits or unavailable coverage.
An oversized row can produce an empty page; reduce the projection in that case.

`nextOffset` addresses matching rows within the bounded scanned prefix, never an
unseen server page. Send the previous `revision` as `expectedRevision` when paging.
Changes to projected/filter values, row identities/order, selection or columns
invalidate it. Structural changes during a read return `table_changed`.

## Verified actions

An action wraps the same query and adds one of `select_row`, `edit_cell`, or `sort`:

```json
{
  "query": "<replace with the query object>",
  "action": "edit_cell",
  "requestId": "repair-row-42-status",
  "rowKey": "row-42",
  "rowGeneration": "<observed row generation>",
  "columnId": "binding:Status",
  "columnGeneration": "<observed column generation>",
  "desired": "passed",
  "timeoutMs": 3000
}
```

Selection requires row identity; sorting requires column identity and `direction`
(`ascending` or `descending`). Editing requires both and a typed desired value.
Actions require the entire bounded available view to have unique readable keys,
re-resolve targets before dispatch and reject replacements or changed filters.
The optional revision guards the initial decision; verification observes the new
state after the action. A satisfied state performs no new input.

Realization uses public `ScrollIntoView`; selection uses the control's public
selection API. Editing opens the real public editor, uses the existing routed text
or toggle-provider path, then calls public `CommitEdit`. Sorting uses the column's
public `Sort`. No row property is assigned directly. Formatting/custom editors are
unsupported rather than guessed. An existing edit/add transaction blocks actions;
rejected drafts and earlier preparation remain visible for explicit handling.

Deep control templates can grow further when a cell editor opens. For the shared
QA DataGrid, discover the table with an explicit structured `find_nodes` selector,
`maxDepth: 64` and `maxNodes: 2048`, verify complete coverage, then pass the entire
returned target. The default query bounds and relationship-hop limits are unchanged;
smaller explicit bounds remain enforced and are never silently widened during an action.
The global query node/work/time limits still apply. A depth-32 target can become
incomplete during editor preparation; `coverageReasons`, query bounds, `tableEditing`
and `intentDispatched` diagnostics distinguish that state from stale identity.
Explicitly inspect and finish/cancel any pending draft before a new edit intent.
The original request remains replayable as its original uncertain outcome.

Cell cancellation is not necessarily row-transaction completion. In the native QA
fixture, Escape clears a rejected fractional Score draft and its validation error,
but subsequent edits still report `table_edit_active`. After confirming the original
valid source value, explicitly finishing the row with Enter allows a newly observed
edit. Inspect the application's actual editing behavior before choosing finish or
cancel; do not repeat an unverified write. An already-satisfied action performs no
input and does not prove that a separate edit transaction has ended.

The deadline is 100..3000 ms (default 2000), with no automatic rollback or save.
Results include before/after rows, sort state, dispatched operation count,
preparation, diagnostics and `avalonia_public_control_api` provenance with the actual
backend. `verified` establishes the observed public state, not durable persistence or
completion of future asynchronous validation. Unverified results remain available
with `success:false` and CLI exit 1. Large action cell values can be omitted with
`omitted_response_budget` while retaining the operation outcome.

Preserve the exact payload and request ID after a lost response. The bridge retains
128 results per session without eviction and returns the original result with
`replayed:true`; it never silently redispatches. Different payloads conflict. A full
ledger rejects new IDs. Replay is historical evidence, not a fresh assertion. Check
current state before starting a different intent. Policies must explicitly include
the action in `allowedTableActions`; normal inspection authorization/redaction applies.

## Sources and validation

The adapter uses the public [DataGrid](https://github.com/AvaloniaUI/Avalonia.Controls.DataGrid/blob/12.1.2/src/Avalonia.Controls.DataGrid/DataGrid.cs),
[column](https://github.com/AvaloniaUI/Avalonia.Controls.DataGrid/blob/12.1.2/src/Avalonia.Controls.DataGrid/DataGridColumn.cs)
and [binding](https://github.com/AvaloniaUI/Avalonia.Controls.DataGrid/blob/12.1.2/src/Avalonia.Controls.DataGrid/DataGridBoundColumn.cs)
contracts. No foreign automation framework is required.

`RuntimeTableTests` covers typed filters, paging/revisions, virtualized/replaced rows,
duplicate keys/columns, unsupported bindings, validation drafts, sorting during
reads, redaction, byte budgets, interrupted responses and CLI/MCP replay.
`eng/test-native-input.ps1` enables the optional host-side table fixture and validates
MCP reads plus CLI editing of an offscreen row on the actual desktop backend. Normal
standalone host builds retain their dependency-free AvaScope integration and omit
the optional DataGrid fixture package.
