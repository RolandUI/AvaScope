# Bounded runtime observation changes

Use `avascope observe-changes --request changes.json` or MCP `observe_changes(request)` after selecting one explicitly activated local session. The bridge retains cursors, so a later CLI process or MCP connection can continue the same cursor without a client cache or another listener.

```json
{
  "observation": {
    "sessionId": "selected-session-id",
    "topLevelIds": ["selected-top-level-id"],
    "maxNodes": 40,
    "maxDepth": 4
  },
  "cursor": null,
  "waitMs": 0,
  "maxEvents": 32
}
```

The first result has `status: baseline`, a bounded `baseline` observation and a cursor. Send the returned cursor with the same observation scope to get ordered changes. Unchanged UI returns `status: unchanged`, no baseline and no events. Follow `hasMore` using the last returned cursor to drain a page; events have strictly increasing sequence numbers within that cursor's scope.

Events describe window metadata, sampled node membership, text, state/actions, bounds, public validation diagnostics, generation and identity/structure changes. `node_entered_sample` and `node_left_sample` describe the bounded sample, not necessarily object creation/destruction. Window filter changes similarly produce scope membership events. A missing explicitly selected window reports `window_closed`; returning availability reports `window_opened`. Generations identify object lifetimes, not state-change counters.

## Sampling and waiting

Collection compares current bounded observations. It does not retain an exhaustive event stream: intermediate states between samples may coalesce, including a value changing and returning to its original value. Every result reports this limitation in `coverage`. No background collector, timer or UI subscription runs between calls.

`waitMs` is 0–3000; `pollIntervalMs` is 25–1000 (default 100). A pending call samples until a change or its deadline, with at most 1000 additional milliseconds for UI/gate access and an overall 4000 ms cap. The observation timeout can narrow that allowance. `samplesTaken` makes collection overhead visible. Cancellation stops client waiting; an already accepted bridge request remains bounded by its deadline. Direct bridge cancellation also cancels polling. Bridge shutdown clears retained state and prevents further collection.

## Cursor scope and limits

Cursors belong to one bridge activation, session, window/root filters, node/depth limits, diagnostics setting, policy and cursor TTL. A changed scope requires resynchronization. Request ids, output directories, inline budgets, polling and page sizes are delivery settings and do not change the scope. A cursor is not an authorization token: normal local session and policy checks still apply.

The bridge keeps at most eight scopes, evicting the least recently used scope. Each scope retains at most 128 events and 384 KiB of encoded event JSON, plus its latest bounded observation. It retains sanitized DTOs, not Avalonia controls or screenshots. Idle cursor TTL defaults to 120000 ms and accepts 100–300000 ms. Activity refreshes it; expiry uses monotonic elapsed time, while `cursorExpiresAt` is informational UTC.

Malformed/future cursors, expiry, eviction, restart, changed scope and dropped history return `resyncRequired: true`, an explicit reason, a fresh bounded baseline and its cursor. Overflow never masquerades as complete history. `droppedEvents`, retained count/bytes and oldest/latest sequence describe the retained scope; no lost-event count is inferred after a restart or eviction.

## Privacy and response budgets

Observation exclusions and text redaction run before baseline or event retention, and Core sanitizes again before response/artifact output. A changed policy cannot read an earlier scope. For repeated requests writing into the same policy-owned output directory, supply a stable explicit `observation.requestId`; alternatively use a new output directory.

If the bounded response exceeds the observation's inline budget, Core saves the complete sanitized response to `responseBudget.artifactPath`, omits inline events/baseline and sets `requiresArtifactRead: true`. Read that artifact before advancing the cursor. If artifact creation fails, keep the previous cursor and retry with a larger budget or establish a narrower scope. Screenshot requests are rejected here; use an explicit [`observe`](RUNTIME_OBSERVATIONS.md) request for image evidence.

Regression coverage includes ordered pagination, compact unchanged UI, coalesced intermediate states, node/window membership, validation/state changes, bounded polling and cancellation, rapid updates/overflow, expiry/eviction, invalid/scoped cursors, activation restart, redaction before retention, sanitized artifact fallback and a real CLI baseline continued through MCP.
