# Typed runtime queries and compound assertions

Use MCP `evaluate_runtime` or CLI `avascope evaluate-runtime --request expression.json`
when multiple observed values must agree. Discover `runtime.expressions` first.
The read-only operation uses the existing selector/relationship/projection model,
without scrolling, realizing items, invoking commands or evaluating application code.

For example, assert that exactly two currently observed rows are selected:

```json
{
  "sessionId": "<session>", "topLevelId": "<window>", "requireTrue": true,
  "definition": {
    "sources": [{"id":"rows", "selector":{"nodeType":"ListBoxItem"}, "attribute":"selected"}],
    "expression": {"kind":"eq", "operands":[
      {"kind":"count_true", "source":"rows"},
      {"kind":"literal", "literal":2}
    ]}
  }
}
```

For the direct typed count, replace `expression` with
`{"kind":"count_true","source":"rows"}` and omit `requireTrue`. Both forms
use precisely the same captured values and evaluator. Sources select one of the
attributes supported by `find_nodes`; the default `nodeType` is useful for counting.
Every declared source must be used and source ids are unique ASCII identifiers.

| Operator | Inputs and result |
| --- | --- |
| `literal` | Boolean, string, or decimal JSON number in `literal` |
| `value` | Exactly one present source value; zero/multiple matches are indeterminate |
| `count` | Number of fully observed values from `source` |
| `count_true` | Number of true boolean values from `source` |
| `sum`, `minimum`, `maximum` | Numeric source aggregate |
| `number` | One operand; explicit invariant decimal conversion of a string or number |
| `all`, `any` | One to eight boolean operands |
| `not` | One boolean operand |
| `eq`, `ne`, `gt`, `ge`, `lt`, `le` | Two same-type operands; boolean operands support equality only |
| `add`, `subtract` | Two numeric operands |

Numbers use checked .NET decimal arithmetic, with no implicit conversion or
tolerance. `number` accepts a sign, decimal point and exponent using invariant
culture; it rejects localized commas, grouping separators and whitespace. Thus
`"1.25"` converts consistently under `hu-HU`, `fr-FR` and `en-US`; `"1,25"` does
not. Overflow and incompatible types are indeterminate. String comparisons are
ordinal and case-sensitive. Empty complete sources have count/sum zero; minimum,
maximum and scalar `value` are indeterminate. A missing projected attribute is
different from an empty complete selection, and cannot contribute to an aggregate.

The result includes every operand's exact expression path, typed value,
availability and reason, plus each source's selector, scope, coverage, observed
values and target generations. `requireTrue` yields `passed`, `failed` or
`indeterminate`; a scalar request yields `observed` or `indeterminate`. CLI/MCP
report unsuccessful assertions and indeterminate observations as `success: false`
while preserving the complete bounded evidence. CLI exit codes are 0 for success,
1 for an unsuccessful operation and 2 for invalid input.

No missing operand is ignored: even `any(true, unavailable)` is indeterminate.
The same rule applies to excluded/redacted controls, truncated text, query depth,
node/result limits and response projection limits. This conservative behavior
prevents partial observations from producing a complete or passing aggregate.

Collection scope is the **currently loaded UI in the selected window**, not a
server/database dataset or future lazy-loaded data. Public `ItemsControl.ItemCount`
is compared with the bounded realized-container count. An unrealized collection
anywhere in a broad source's scan makes its coverage incomplete; a DataGrid requires
the separate structured table query. An exact `nodeId` source identifies an
already-realized target and avoids unrelated collection realization uncertainty.
All normal traversal, redaction and generation checks still apply. AvaScope never
claims that an application's loaded collection is the complete remote dataset.

Sources are collected and re-read in one UI dispatcher turn. Changed generations,
values, membership or coverage invalidate the combined result. This is explicitly
sampled consistency, not an atomic application transaction; app callbacks may run
during public inspection and cannot be preempted. Two equal samples cannot prove
that no unobserved intermediate state occurred.

Use the same definition in workflow waits and action postconditions:

```json
{
  "action":"wait_for_state", "timeoutMs":3000, "pollIntervalMs":100,
  "waitCondition": {
    "kind":"expression",
    "expression": {
      "sources":[{"id":"status","selector":{"name":"Status"},"attribute":"text"}],
      "expression":{"kind":"eq","operands":[
        {"kind":"value","source":"status"},
        {"kind":"literal","literal":"saved"}
      ]}
    }
  }
}
```

The existing workflow timeout, cancellation, top-level aliases, variables and
observe-act-verify infrastructure apply. Comparisons belong inside the expression;
the condition waits for its boolean true result. Each poll reselects and reobserves
sources. Timeout reports retain the last complete evaluation, failed paths and
source values in `waitObservation.expression`; metadata summarizes failed paths
for human-readable reports. Templates substitute source selector strings and string
literals, never operator names or executable code.

Limits: eight sources, 48 expression operators, expression depth eight, 2048
scanned nodes/64 matches/tree depth 32 per source, 64 KiB CLI request, 128 KiB
response, and a cooperative two-second collection budget. Oversized evidence
returns an explicit limit error rather than a passing partial result. Evidence
policies authorize inspection and apply before evaluation and response serialization.

Validation covers numeric cultures, arithmetic/overflow, strict all/any/not,
empty/partial/redacted/virtualized sources, generation changes, scalar/assertion
parity, bounded workflow waits and real CLI/MCP calls. The collection APIs are
public Avalonia 12 APIs: [ItemsControl source](https://github.com/AvaloniaUI/Avalonia/blob/12.1.0/src/Avalonia.Controls/ItemsControl.cs).
