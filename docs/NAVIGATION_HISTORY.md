# Observed navigation history

CLI/MCP `navigation` and capability `runtime.navigation` keep a bounded in-memory
history of surfaces visited during one explicit bridge session/run. The bridge
collects evidence through the existing observation engine. The tool never sends
input, invokes custom actions, changes a page or replays a route. Observers may
maintain their own journal while another client holds the application's control
lease.

Start with `sessionId` and `action: "start"`. Optional `topLevelIds` selects up to
four windows; omitting it samples up to four currently registered windows, so new
dialogs can enter the next observation. `maxNodes` (default 24, maximum 64) and
`maxDepth` (default 4, maximum 8) are fixed for that run. The response returns an
opaque `runId`, `currentVisitId`, expiry and the first visit. No screenshots or
filesystem artifacts are created by this tool.

After performing an action through the usual tools, record the new observation:

```json
{
  "sessionId": "copy-the-session-id",
  "action": "record",
  "runId": "copy-the-run-id",
  "previousVisitId": "copy-the-current-visit-id",
  "visitLabel": "Settings opened",
  "transition": {
    "description": "Opened Settings using its observed button",
    "outcome": "succeeded",
    "requestId": "optional-action-correlation-id"
  }
}
```

The transition is a caller-reported outcome (`succeeded`, `failed`, `unknown`),
correlated with real bridge observations before and after it. It does not prove
that the action caused the change. Do not put passwords, typed text or other
action arguments in its description. Omitting `transition` records a visit with
an explicit gap in the route. `previousVisitId` must still be the current tip:
concurrent or accidentally repeated appends fail rather than inventing a path.
After a lost response, query the run before recording again. A lost start response
can leave one bounded run until its fixed expiry; start is not retried internally.

Query with `action: "query"` (the default), `sessionId` and `runId`:

- No filter returns recent visits, newest first.
- `visitId` retrieves one exact visit. `includeEvidence: true` includes its retained
  observation with window generations, node samples, timestamps and coverage.
- `stateKey` retrieves visits sharing an observed host identity and content sample.
- `fromVisitId` and `toVisitId` retrieve the actual chronological route between two
  retained visits. A route is complete only if every intervening transition was
  recorded and the whole path fits `maxVisits`. `reported_success` means every
  recorded action was reported successful. A route is historical evidence, not a
  plan guaranteed to work in the current application.

`loops` reports returns to a prior observed state, with the intervening path length
and the number of occurrences of that retained pattern. Repeated observations
without explicit actions are not navigation loops. An identity-free match is
marked `possible_sampled_revisit; equivalence_uncertain`; it cannot prove the same
document, hidden state or route. Failed and unknown action outcomes remain visible.
Only the newest 32 retained loops are returned, with an explicit truncation marker.

Every visit has its own immutable id and evidence; similar trees never merge
visits. Without a host identity, each visit also receives a unique state key.
Identical bounded content samples may suggest uncertain equivalence, but are not
used to manufacture routes between different visits. Content, validation state,
window/node generations and sampled window sets contribute to the observation
revision. Geometry, focus and timestamps are omitted from candidate equivalence.
Anything outside the selected sample, redacted content and unsampled intermediate
states remains unknown. Two samples detect changes during collection; this is not
an atomic domain transaction.

Applications that already implement `IAvaScopeDebugStateProvider` can expose three
explicit fields on a view:

```csharp
public IReadOnlyDictionary<string, string?> GetAvaScopeDebugState() =>
    new Dictionary<string, string?>
    {
        ["navigation.surface"] = "document-editor",
        ["navigation.context"] = document.StableId,
        ["navigation.revision"] = document.StateRevision.ToString()
    };
```

Pass a fresh `identityTarget` from `find_nodes` when starting or recording a visit.
The target must belong to a sampled window and retain its generation/data context
through collection. Each field must contain 1..128 characters. `context` must
distinguish documents/workspaces; `revision` must change when domain state changes.
The bridge combines this host declaration with the provider's object/data-context
identity, window/node generations and observed content. It still stores every
visit separately and labels hidden domain state as unverified. Missing, failed,
truncated, changing or policy-redacted identity falls back to a unique visit;
stale targets are rejected. No view-model reflection or domain inference is used.

The same evidence policy must accompany every request for a run. Inspection and
explicit session/process authorization apply before capture or history access.
Text is redacted before truncation and retention; excluded controls are omitted.
Protected identity providers are not queried. Changing or omitting the original
policy cannot reveal already retained evidence. Use a new run for a new policy.

Limits: eight runs per bridge; 128 visits and 512 KiB per run; 64 KiB per visit;
1..32 returned visits (default 16), with an 80 KiB combined visit response budget.
Oldest visits are evicted first and `droppedVisits`/diagnostics expose the gap.
Query a specific visit to retrieve evidence omitted from a bounded response.
Missing/evicted routes are not proof that no route was observed. The default fixed
TTL is two minutes, configurable at start from 100 ms to 30 minutes. Queries do
not renew it. An expiry timer releases retained memory, and `action: "clear"`
discards the selected run immediately. App restart/shutdown discards all history;
old session ids, generations and run ids never authorize a new app instance.

Use `avascope navigation --request navigation.json --manifest-dir <dir>` or MCP
`navigation` with the same structured request. This public-Avalonia observation
path is shared across Windows, Linux, macOS and headless backends; it adds no
native accessibility, remote transport or foreign test-framework integration.
