# Relationship selectors and typed projections

`find_nodes` accepts an optional `selector`, `attributes`, `maxNodes` and `policy`.
The CLI accepts the same bounded query as `find-nodes --request query.json`.
Existing flat find flags retain their behavior; do not combine a `selector` with
flat identity/state filters or tree/binding expansion options.

```json
{
  "sessionId": "selected-session",
  "topLevelId": "selected-window",
  "selector": {
    "name": "City",
    "relationships": [
      { "kind": "ancestor", "selector": { "name": "Shipping" } }
    ]
  },
  "attributes": ["name", "text", "enabled"],
  "maxDepth": 24,
  "maxNodes": 512,
  "maxResults": 16
}
```

For MCP, pass these properties directly to `find_nodes`. With no `attributes`,
the response contains flat `matches`, each with related target evidence. With
attributes, `matches` is empty and `projections` contains one typed attribute set
per matching node, with the same source target and relationship evidence. No
whole tree or arbitrary application properties are returned.

## Relationships

All ordinary criteria are conjunctive and use existing find semantics: names and
automation IDs compare without case, text/type filters are case-insensitive
substrings, binding paths and command paths compare exactly. `role` retains its
existing node-type alias meaning; the projected `role` is public accessibility
control-type metadata. `nodeId` may further constrain a current identity.

| Kind | Evidence |
| --- | --- |
| `parent` | The immediate parent in the selected visual/logical tree. |
| `ancestor` | A containing ancestor, within `maxDepth` hops (default 8, maximum 32). Use a group's name or a stable row automation ID to distinguish repeated fields/actions. |
| `descendant` | A realized descendant within the specified hop limit. |
| `labeled_by` | An explicit `AutomationProperties.LabeledBy`, a public control automation peer's `GetLabeledBy`, or a `Label.Target` link. |

For Peter's Edit button, combine `name: Edit` with an `ancestor` whose
`automationId` is the application's stable Peter-row ID. Relationships may nest;
each selector permits four relationships, with at most four nested levels and
sixteen relationships total. All nested selectors must use the same `treeKind`.
Their optional `maxDepth` further limits absolute depth within the selected tree.
The first proven witness satisfies an existential relationship; this does not
choose among multiple matching action targets.

The bridge never treats spatial proximity or a similar label as proof of an
association. Missing label metadata yields no labeled match and up to eight
identity/state candidates explaining the unproven relationship. Label links
outside the selected tree/window are marked as incomplete coverage. Localized
label text works when explicitly supplied; stable IDs avoid translation coupling.

The same selector object works in workflow actions, `validate_action`, waits,
assertions, destinations, verification and fragment variables. Workflows require
exactly one match and complete query coverage; zero/ambiguous/partial results
return bounded candidates and recovery details before input. Each step and wait
poll resolves again. Relationship workflow queries use at most 512 realized nodes
and the requested workflow/selector depth, capped at 32.

## Values and coverage

Supported attributes are `name`, `automationId`, `text`, `nodeType`, `role`,
`visible`, `enabled`, `rendered`, `actionable`, `focused`, `checked`, `selected`
and `value`. Values carry `type`, `status`, `source` and a typed JSON `value`.
Names/text/identity/role are strings; state attributes are booleans. `checked`
uses `ToggleButton`; `selected` uses realized list/tree item controls. `value`
is numeric for `RangeBase` and string for `TextBox`. Other control families
report missing values instead of invoking arbitrary getters.

Statuses distinguish `present`, `missing`, `indeterminate` (tri-state check),
`unavailable` (such as non-finite numeric values), `redacted` and `truncated`.
Missing/redacted values are JSON null, not false, zero or empty text. Text is
redacted before its 512-character limit; redacted projected values contain no
partial sensitive text. Excluded controls and their selected-tree descendants
produce no values or candidate identity and mark coverage as incomplete.

`coverage` reports visited nodes, a lower bound on matches and reasons for
partial output. Default bounds are 16 results, 512 nodes and depth 16; maximums
are 64 results, 2048 nodes, depth 32 and eight projected attributes. The analysis
also has 32,768 work checks and a two-second cooperative budget. Public app/peer
callbacks must be fast and read-only and cannot be forcibly preempted safely.
Output is limited to 64 KiB with explicit coverage markers, without spill files.

Depth/node/result/byte/time limits, failed label providers, excluded controls,
truncated text and generation changes must not be interpreted as a complete
inventory or proof of absence. The scope is realized nodes in one selected
top-level, never every logical item in a virtualized collection. Use
`virtual_item` to find/reveal a logical item before querying its realized row.
No scrolling, focus change or input is performed by query analysis.

## Freshness and dispatch

Returned targets include `selection` and `selectionRevision`. Preserve the
entire target when using `inputTarget`, mutation targets or virtual collections.
The revision records live object/data-context identity for the target, its
ancestor chain and relationship witnesses; it is not a durable item ID.
The bridge re-evaluates the selector and requires a unique current target and
matching revision before acting. Recycled containers, changed relationships,
detachment and new ambiguity fail safely. Public callbacks that change query
membership/generation during a query produce no stale result rows.
Targets from a broad, multi-match query retain that selector. Refine it with an
explicit identity or current node ID before input; picking one returned row does
not silently waive the uniqueness check.

Input rechecks after focus/preparation callbacks and before new compound input
groups. A failure after preparation does not authorize automatic replay because
application callbacks may already have run; owned release cleanup still occurs.
The existing proven pre-dispatch stale-target retry rule remains in force.
Workflow inspections, assertions and wait-state reads preserve and revalidate
the same selection context. A disappearance wait requires complete coverage;
an empty partial query never proves that the target disappeared.
Geometry/hit-testing remains the separate `explain_action`/input contract.

Queries use the existing opt-in local bridge on all supported platforms. Public
Avalonia tree/label evidence is not OS-level accessibility or native occlusion
proof. No foreign query language or automation-framework interoperability is
introduced.

Public Avalonia 12.1.0 sources:
[label properties](https://github.com/AvaloniaUI/Avalonia/blob/12.1.0/src/Avalonia.Controls/Automation/AutomationProperties.cs),
[control peers](https://github.com/AvaloniaUI/Avalonia/blob/12.1.0/src/Avalonia.Controls/Automation/Peers/ControlAutomationPeer.cs),
[Label.Target](https://github.com/AvaloniaUI/Avalonia/blob/12.1.0/src/Avalonia.Controls/Label.cs).
