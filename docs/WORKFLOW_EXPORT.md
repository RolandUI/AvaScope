# Export and replay AvaScope workflows

`export-workflow` / `export_workflow` exports an executed AvaScope semantic
workflow into the existing workflow format. It consumes the original
`SemanticWorkflowRequest` and its complete `SemanticWorkflowResponse`. Keep the
unmodified request and the response's `value`, or read the complete response
artifact when the tool reports truncation. No other framework format, adapter,
workflow importer or compatibility layer is supported.

The execution response includes `metadata.requestDefinitionSha256`, a canonical
request fingerprint. This correlates the source definition; it is not a signature
or a trust boundary. Responses from older versions, validate-only responses,
truncated responses and changed source definitions cannot establish execution
evidence and are rejected. An unsuccessful recording may be exported for review,
but it is never labeled a verified successful flow.

## Export

Save a request shaped as follows, with the actual original request and response
objects in `source` and `recording`:

```json
{
  "source": { "...": "original SemanticWorkflowRequest" },
  "recording": { "...": "complete SemanticWorkflowResponse" },
  "outputDirectory": "/explicit/evidence/root",
  "parameters": { "customer": "recorded-sensitive-value" },
  "defaultTopLevelAlias": {
    "alias": "main",
    "selector": { "title": "My application" }
  }
}
```

```sh
avascope export-workflow --request export-request.json
```

MCP takes the same DTO as `request` in `export_workflow`. `defaultTopLevelAlias`
is optional for a source already using aliases. The exporter writes a fresh
`avascope-export-<id>/export.json` and `workflow.json`; it never overwrites a
previous export. `workflow.json` uses the existing semantic workflow shape with
binding placeholders. Use **export.json** with the replay tool.

- Original verified assertions, typed waits and successful postconditions are
  retained. `verifiedStepIds` identifies the recorded verified steps, not a
  promise about the next run. Dispatch alone is never a successful assertion.
- Node ids are removed. A matching inspection may supply a candidate automation
  id or name, with a review item; replay always requires unique resolution.
  Missing stable selectors and window aliases block replay.
- Fixed sleeps, unobserved branches, repeated source ids and actions without a
  verified postcondition are review items. Held keys/buttons block replay;
  convert them to paired actions. Coordinates require layout review.
- Literal typed text, workflow-variable values and configured redaction values
  become required parameters. Explicit `parameters` maps names to recorded
  sensitive or environment-specific values, which become `export_<name>`.
  Equal values share a parameter. No recorded value is stored as a default.
  Identify any additional application-specific secrets with this map or the
  existing evidence policy; arbitrary strings cannot be classified reliably.
- Idempotency keys, bounded branches/retries, action restrictions, screenshot
  masks, exclusions and redactions remain in the workflow. Transient session,
  process authorization, output and isolated-state locations are rebound.
- The exporter never inserts assertions from screenshots, changes expected
  values to match a failure, or creates/accepts visual baselines.

`status=needs_review` is a successfully written draft, not a passed test.
`validated` means the template compiled when rebound to its original inputs.
Blocking review items require correcting the source and exporting again.
Non-blocking review items require explicit `acknowledgeReview=true` before
validation or execution. Do not infer assertions for unobserved paths.

## Validate and replay

```json
{
  "exportPath": "/explicit/evidence/root/avascope-export-<id>/export.json",
  "sessionId": "fresh-explicit-session",
  "outputDirectory": "/explicit/evidence/root/fresh-run",
  "parameters": { "export_customer": "fresh-sensitive-value" },
  "validateOnly": true,
  "acknowledgeReview": false,
  "evidenceRoot": "/explicit/evidence/root"
}
```

```sh
avascope replay-workflow --request replay-request.json --manifest-dir /explicit/manifests
```

MCP `replay_workflow` accepts the same request and optional `manifestDirectory`.
All listed parameters must be provided, with no extra names. If the source
required isolated state or a process allowlist, supply `isolatedStateDirectory`
or `authorizedProcessId` explicitly. The original policy is enforced with the
new bindings, including redaction in returned evidence and reports.

Validation defaults to `true` and invokes no bridge actions. After reviewing
the expanded plan, set `validateOnly=false` to execute via the existing runner.
Destructive authorization is never broadened: `allowDestructive=true` must be
explicit on replay and permitted by the exported source and policy; the existing
isolated-state rules still apply. Use a fresh session/test fixture for a fresh
regression. Reusing a live session retains normal idempotency behavior.

Execution returns existing workflow observations and JSON/Markdown/JUnit report
paths when requested by the source evidence options. A deliberate application
regression fails its original condition. CLI replay exits nonzero for validation
or execution failure. MCP clients must inspect the workflow status as with
`run_workflow`. Export inputs and files are limited to 1 MiB; at most 128
parameters of 8192 characters are supported. Input/replay request files contain
the values supplied by the caller: store them privately, outside committed test
definitions. The exported definition contains no parameter defaults.

The shared compiler/runner works across supported Avalonia platforms. This
format does not convert synthetic input into native input or increase a backend's
capabilities; retain [input provenance and limitations](INPUT_STRATEGIES.md).
