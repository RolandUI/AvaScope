# Runtime form workflows

`runtime.forms` exposes `inspect_form` and `fill_form` through MCP, with matching
`avascope inspect-form --request form.json` and `avascope fill-form --request fill.json`
commands. Both accept `--manifest-dir`; MCP accepts `manifestDirectory` separately.
The host must explicitly activate the local bridge. Filling requires the same
session-control permission as other input. No external automation framework is involved.

## Inspect and select

An inspection request selects one session and top-level, optionally a unique visual
subtree via a [relationship selector](RELATIONSHIP_QUERIES.md):

```json
{
  "sessionId": "<session>",
  "topLevelId": "<top-level>",
  "scope": { "name": "SettingsForm" },
  "maxFields": 24,
  "maxNodes": 512
}
```

Supported field families are public Avalonia `TextBox`, `ToggleButton` (including
check/radio buttons), `RangeBase`, and `SelectingItemsControl`. Template children
inside another supported field are excluded from the field inventory. This is a
bounded inventory of visible supported controls, not a claim to understand arbitrary
custom forms. Unsupported custom controls continue to use explicit inspection,
input or declared host actions.

Each field returns a generation-bearing target, name/automation id, control type,
desired-state property, typed state, writable flag, choices and observed validation.
Labels come from `AutomationProperties.LabeledBy`, the public automation peer,
`Label.Target`, or explicitly declared `AutomationProperties.Name`. Their source is
included. Proximity and asterisks do not imply labels or requiredness. Avalonia 12.1's
public control/automation APIs used here expose no universal required-field property;
`required` stays null and `requiredSource` is `unavailable`.

Validation uses `DataValidationErrors`: `no_errors_observed` describes the observation,
not completed asynchronous validation or persisted application state. Its provenance
explicitly says asynchronous pending state is unknown. Error counts become lower
bounds when `validation_truncated` is reported.

Selection fields return at most eight choices each and 64 overall, with public
string labels where available. Unrealized choices have null targets; no scrolling
or opening a dropdown occurs during inspection. Filling a selection requires fresh
realized item targets in the selected container, using the desired-state selection
contract. Incomplete current selection is explicit. A missing label stays null.

## Fill and verify

```json
{
  "form": { "sessionId": "<session>", "topLevelId": "<top-level>" },
  "fields": [
    { "id": "email", "selector": { "automationId": "Email" }, "desired": "dev@example.test" },
    { "id": "remember", "selector": { "name": "Remember" }, "desired": true }
  ],
  "requestId": "settings-intent-1",
  "settleMs": 100,
  "allowSensitiveInput": false
}
```

Every mapping has its own unique id and selector. A selector must identify exactly
one supported field inside the inspected scope. Duplicate labels require a name,
automation id or explicit container relationship. The entire plan is checked before
any input: ambiguity, unsupported values, read-only targets, declared text limits,
provider constraints, policy and available replay-ledger capacity. A rejected plan
leaves the other mappings `not_executed`.

The bridge uses [desired-state actions](DESIRED_STATE_ACTIONS.md): routed text input
for TextBox and public automation providers for checks, values and selections. Each
field is checked again against the prevalidated identity and state before dispatch.
Changes to a remaining field stop the old plan; an already executed field is never
automatically rolled back. The default 100 ms settling interval observes delayed
validation; it is configurable from 0 to 1000 ms per field. Each completed field is
verified again at the final observation, so a later change can invalidate an earlier
result. Validation occurring after the observation remains unknown.

`before`, `after`, `appearedFields`, `disappearedFields` and `changedFields` report
observable dependency effects, including changed choices, writable state and
validation. These are changes during the plan, not an inferred causal/business-rule
graph. Inspect them before deciding on a new plan for newly available fields.

`passed` requires every requested field to verify. `invalid_plan`, `partial`,
`failed`, `cancelled` and `uncertain` preserve field-level outcomes. CLI returns 1 and
MCP returns `success: false` with the response retained for those outcomes. Transport
failure reports unknown dispatch and the original form request id. No write is
automatically retried. Repeat the **identical request id and payload** in the same
live bridge to retrieve its original result; replay does not re-observe the app.
After observing current state, a new intent requires a new id.

The tool never invokes submit or Enter and always reports `submitted: false` and
`rolledBack: false`. Application callbacks may autosave or perform other side effects;
these flags describe AvaScope's actions, not a transaction guarantee. Submission
must be an explicit separate input/custom action selected by the caller.

## Bounds, privacy and platforms

- Inspection: 1..32 fields, 1..2048 nodes, depth 32, two-second cooperative analysis
  budget, 64 KiB result. `coverage` describes exclusions, truncation and partial data.
  Incomplete inventory/identity prevents filling; bounded choices can still be reported.
- Fill: 1..16 mappings, 64 KiB request, 30-second cooperative budget between operations.
  Each provider operation retains the desired-state limits. Application callbacks
  cannot be forcibly interrupted. The client's normal IPC timeout is extended by the
  requested total settling interval; timeout still means unknown dispatch.
- The bridge retains 64 complete fill results and shares the 512-entry desired-state
  ledger. No entries are evicted. A full ledger rejects new intents and keeps existing
  results replayable. Restarting the bridge ends this replay scope.
- Password TextBoxes always redact observed values and validation messages; writing
  them requires `allowSensitiveInput: true`. Responses do not echo desired values or
  field selectors. Request files still contain caller-supplied input; callers own
  their secure storage and deletion.
- Optional `RuntimeEvidencePolicy` applies session/inspect authorization, exclusions,
  redaction and explicit `allowedDesiredStates` permissions. Excluded subtrees are not
  read or filled. The tools write no automatic evidence artifacts. Values omitted
  from bounded execution evidence carry `omitted_response_budget`; verification was
  performed against the full supported value before output compaction.
- Public Avalonia 12 APIs and the UI dispatcher provide the same contract on
  Headless, Windows, Linux X11 and macOS. Evidence reports the actual provider/routed
  path and backend; these operations do not claim native OS keyboard dispatch.
