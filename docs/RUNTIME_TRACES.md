# Correlated runtime diagnostics

Discover `runtime.traces`. CLI `avascope trace --request trace.json` and MCP
`trace` share `start`, `read` and `stop`. A trace selects one current top-level;
starting requires an explicit evidence policy and session-control authorization.
Assembly loading or ordinary app startup never starts collection.

```json
{
  "sessionId":"observed-session",
  "action":"start",
  "topLevelId":"observed-window",
  "durationMs":10000,
  "sampleValidation":true,
  "policy":{
    "ownedEvidenceRoot":"/explicit/evidence/root",
    "redactedText":["a-sensitive-value"],
    "excludedControlAutomationIds":["PrivatePanel"]
  }
}
```

Keep the returned `traceId`, execute an action, then read that trace. Input responses
add `correlationId`; custom-action responses already carry `requestId`. Both survive
workflow step evidence and idempotency replay. An [app operation](RUNTIME_OPERATIONS.md)
also carries its originating request id and operation id.

```json
{"sessionId":"observed-session","action":"read","traceId":"returned-id","requestId":"action-request-id","maxEvents":32}
```

The response includes bounded events, source availability, retained/dropped/suppressed
counts and a truncation marker. A requested id labels each returned event as
`explicitly_correlated`, `different_correlation` or `temporal_only`. With no filter,
events are `unfiltered`. Time-window errors remain visible without being blamed on
the selected action. `bridge_request` is evidence of AvaScope request handling;
`operation_request` comes from the explicit operation/action link; `app_declared`
is the host adapter's claim; `uncorrelated` has no explicit link. Correlation does
not establish root cause. Redacted identifiers cannot be reliably matched again.

Bridge dispatch entries contain the method, phase and bounded error code, never
input text, custom-action parameters or arbitrary request payloads. A successful
response does not prove application completion. Operation transitions carry their
known request/operation ids; their messages are sanitized before storage and the
target's captured AutomationId ancestry enforces control exclusions.

Optional UI validation samples use public Avalonia `DataValidationErrors`, at start
and read/stop while collection remains active. They inspect at most 128 visuals,
depth eight and four errors per control with a cooperative 200 ms budget and a
one-second dispatcher deadline. They are current samples, not an event subscription
or a causal binding-error claim. Missing windows, failed diagnostic formatting and
partial traversals appear in source availability. Excluded ancestor subtrees are
skipped before reading their diagnostics; one trace's samples cannot enter another
trace under a weaker policy.

Applications can explicitly adapt approved validation, binding, event and log
sources without replacing a global logger:

```csharp
using var diagnostics = runtime.RegisterDiagnosticSource("import-events", "app_event", topLevelId);
// Inside an allowlisted custom-action handler:
diagnostics.Report("info", "Import accepted", requestId: context.RequestId,
    operationId: operation.OperationId);
// A background error with no known link stays uncorrelated:
diagnostics.Report("error", "Background refresh failed");
```

Allowed kinds are `validation`, `binding`, `app_event` and `app_log`; levels are
`info`, `warning` and `error`. Supply the relevant AutomationId for control-scoped
adapter diagnostics. Its scope/correlation is app-declared; adapters must not label
an unrelated control or omit a known private scope. Dispose adapters when the host
stops using them. No event is retained without an active trace. Missing adapters
are explicitly unavailable; an installed adapter producing no events is not proof
that no error occurred. AvaScope does not infer private binding failures, install
global logging hooks, or automatically correlate arbitrary asynchronous app code.

Every source passes the trace's original policy **before retention and truncation**.
Oversized or invalid events and failed sanitization are suppressed with counts.
Reading without a policy cannot recover original values. Additional read/export
policies can further restrict output. Each trace keeps at most 128 events and
96 KiB of serialized event data, evicting oldest entries. There are at most four
traces and 16 app adapters. Collection lasts at most 60 seconds. Stopped traces
expire ten minutes after their collection deadline and may be evicted earlier for
capacity; active traces are not silently replaced. A registered top-level's removal
stops its traces and removes adapters. Session shutdown clears the store; restart
does not resume old ids.

Supply `outputDirectory` and an export policy to write the same bounded, sanitized
response as `trace.json` under a marker-owned child of `ownedEvidenceRoot`. The
response returns `artifactPath`. Ownership/path checks run before writing; the
existing evidence policy controls retention. `maxEvents` bounds both inline and
exported details, and truncation remains explicit. Use 128 when requesting the
largest retained detail set. No network upload occurs.

Tests cover interleaved actions, unrelated errors, workflow input ids, app operation
correlation, missing/failed sources, volume and byte limits, control exclusions,
cross-trace privacy, disposal/deadline/restart cleanup and real CLI/MCP artifact parity.
