# AvaScope Security Threat Model

This document records the security boundary for the AvaScope v1 stable local control plane. AvaScope is a local-first agent tool. It can execute or load user project code, inspect bridge-enabled applications, and write local artifacts, so the default posture is explicit opt-in, local-only access, bounded output, and no unauthenticated remote control.

## Assets

- User project source, build output, resources, preview profiles, and generated artifacts.
- Running Avalonia app state exposed through an opt-in bridge.
- Local bridge session manifests and named-pipe identifiers.
- Screenshots, visual/logical tree JSON, mutation evidence, report packs, HTML viewers, logs, and release artifacts.
- Public package, CLI, MCP, and protocol compatibility contracts.

## Trust Boundaries

| Boundary | Trusted Side | Untrusted Or Higher-Risk Side | Default Rule |
| --- | --- | --- | --- |
| CLI/MCP process to PreviewHost | AvaScope adapter process | User project code loaded by PreviewHost | PreviewHost runs as an isolated child process. |
| CLI/MCP/Core to runtime app | Local AvaScope client | App process that explicitly activates `AvaScope.Bridge` | Only local manifests and local named pipes are supported. |
| Agent to filesystem artifacts | Agent and user reviewing paths | Generated screenshots/reports/logs | AvaScope writes explicit local paths and does not upload by default. |
| Public client compatibility | Existing CLI/MCP/protocol clients | Newer additive fields and tools | Clients must use capability ids and ignore unknown JSON properties. |

## Local-Only Transport

Runtime inspection and control use local session manifests plus local named pipes. `BridgeSessionManifest` defaults missing `transportScope` to `local_only` for legacy compatibility and rejects any non-local value. `LocalBridgeClient` treats unsupported transport manifests as invalid diagnostics and does not attach or mutate through them.

Remote inspection, unauthenticated network listeners, no-code attach, process injection, CLR profiling, and private runtime hooks are out of scope for the v1.0.0 stable release.

## Opt-In Bridge Activation

Referencing `AvaScope.Bridge` does not activate inspection. A host application must explicitly call `AvaScopeBridge.Activate(...)`, and the getting-started sample only does this when `AVASCOPE_SAMPLE_BRIDGE` is set to `1` or `true`.

Production bridge activation remains disabled by default. AvaScope does not provide a production autostart hook, global runtime injection, or remote activation mechanism.

## Runtime Mutation Permissions

Runtime mutations are local-only, temporary, bounded, and reversible. Mutation requests must target the selected local bridge session and include the same structured `RuntimeTargetContext` returned by runtime inspection commands. Session mismatch is rejected before IPC with `runtime_mutation_non_local_session`.

The safe mutation set is limited to selected public Avalonia style, layout, class, resource, text, and content overrides. Destructive actions, arbitrary process termination, persistent source edits, broad arbitrary-property editing, and private runtime hooks remain out of scope.

## Runtime Custom Action Permissions

Application-defined actions are disabled by default. A host must set `enableCustomActions: true`, provide an exact activation allowlist, and register each action against a live visual instance. Discovery reports required state, current executability, parameter schema, and safety classification. Invocation validates the current generation-scoped target and schema on the UI thread and returns bounded audit evidence. Destructive classifications always require the independent `allowDestructiveCustomActions` activation gate and request authorization (`allowDestructive` or an isolated workflow state directory); a configured runtime evidence policy adds its own explicit destructive-action gate. The registered name never overrides the safety classification.

## Runtime Evidence Privacy And Action Policy

Workflow and scenario `evidence.policy` is an explicit opt-in boundary over runtime evidence and automation. The configured `ownedEvidenceRoot` must contain the run directory as a strict child; report, timeline, screenshot, lifecycle-log, audit, and policy-scoped idempotency paths cannot escape it. AvaScope creates ownership markers and retention considers only marked direct-child run directories. It rejects volume roots, traversal outside the run, and reparse points before recursive deletion, so unrelated or linked content is never treated as owned evidence.

Configured text and AutomationIds are redacted before persisted JSON, Markdown, JUnit, scenario timelines, lifecycle logs, and local JSONL action audits. Excluded controls are structurally redacted; their current visual-tree bounds and explicit pixel regions are black-masked in every policy-managed screenshot. A redaction, control-bound resolution, decode, encode, or path failure fails closed for the affected artifact: unmasked screenshots are deleted and unserializable evidence is omitted behind a generic `runtime_evidence_redaction_failed` or `runtime_evidence_mask_failed` diagnostic that does not repeat the sensitive value.

Large runtime responses can create complete hash-addressed JSON fallbacks while the inline result remains bounded. Under a runtime evidence policy, AvaScope recursively sanitizes each referenced `responseBudget.artifactPath` fallback only when it is inside the marked run and rejects an external path; screenshot masking applies the same rule to the temporary tree it uses for control-bound discovery. This prevents an otherwise clean report or screenshot from leaving an unredacted complete tree in the policy-owned isolated temp directory.

The policy's safe default allowlist contains observation, bounded waits, validation, composition, and custom-action discovery. Interactive actions require explicit allowlisting. Gestures require separate gesture and destructive-action authorization. Application-defined action names require a second exact allowlist and keep the Bridge's independent activation and destructive gates. Optional session and process allowlists are matched against exactly one live `local_only` manifest before dispatch. Network upload is not implemented and `networkUpload: true` is rejected; results report local filesystem storage and AvaScope provenance.

## Preview Execution

Preview rendering can build and load user project code. That code runs inside `AvaScope.PreviewHost`, not inside the CLI or MCP server process. Preview workflows use explicit project/view/profile inputs and write explicit local outputs. Dependency-injection startup, remote design-data loading, JSON object injection, and long-lived design-data services are deferred until they have a separate security model.

## File Outputs And Logs

AvaScope writes screenshots, diffs, JSON reports, HTML viewers, JUnit/SARIF-style report assets, launch stdout/stderr, and release artifacts only to explicit local paths or AvaScope-owned local temp directories. Generated files can contain UI text, paths, diagnostics, and screenshots from the user's app; agents should treat them as local sensitive artifacts and upload them only when the user or CI workflow explicitly chooses to.

Runtime scenario build and launch requests may contain sensitive environment values or application arguments. Normal lifecycle responses expose only environment-variable names and argument counts; raw values are passed directly to the owned child process and are not copied into metadata, diagnostics, or timelines. Captured stdout/stderr remain local artifacts and may still contain values printed by the application itself. Scenario cleanup records the launched session, PID, and process start time and terminates the process tree only when all identity checks still match; foreign, manually launched, already-replaced, or PID-reused processes are not killed.

When `evidence.policy` is configured, scenario stdout/stderr are redacted in place before the response and timeline are returned. Without that explicit policy, the compatibility behavior remains local-only but unredacted, so callers must continue treating the files as sensitive.

The visual-regression GitHub Actions example uses read-only repository permissions and artifact upload only. Publishing workflows require separate release gates and credentials.

## Package, API, CLI, And MCP Compatibility

For v1.0.0, compatibility risk is tracked through GitHub issues, [STABLE_SURFACE.md](STABLE_SURFACE.md), and `docs/RELEASE_PLAN.md`. Public protocol changes should be additive within major version `1`. `ToolResult<T>` keeps the stable `success`, `value`, and `error` JSON shape.

Clients should call `capabilities` and gate workflows by capability id rather than guessing from package versions. Unknown JSON fields must be ignored by clients. Unsupported required capability ids fail with `capability_not_supported` and details that include `requestedCapabilities`, `unsupportedCapabilities`, `availableCapabilities`, `protocolVersion`, and `nextAction`.

## Unsafe Defaults Rejected

| Risk | Current Enforcement |
| --- | --- |
| Bridge active after package reference only | `AvaScopeBridge.IsActive` is false until explicit activation. |
| Non-local bridge transport | `BridgeSessionManifest` rejects non-`local_only` transport scopes; diagnostics mark unsupported manifests invalid. |
| Mutation targets another session | `LocalBridgeClient.MutateNodeAsync` rejects mismatched target sessions before IPC. |
| Runtime mutations become permanent source edits | Mutation review source suggestions are advisory; no automatic source editing is implemented. |
| App-defined action becomes implicitly callable | Custom actions default off and require activation, exact allowlisting, per-target registration, current-state validation, and dual authorization for destructive classifications. |
| Workflow action bypasses an evidence policy | The compiled plan and every leaf action are checked; gestures and destructive/custom actions require their additional independent gates. |
| Evidence retention deletes unrelated data | Only marked direct-child runs under a validated, non-reparse-point owned root are eligible. |
| Redaction or screenshot masking fails | The affected evidence fails closed and the diagnostic omits configured secrets. |
| Evidence is uploaded unexpectedly | Network upload is unavailable; policy construction rejects `networkUpload: true` and reports local storage provenance. |
| Preview user code loads inside MCP/CLI | Core launches the isolated PreviewHost process for preview rendering. |
| CI example publishes packages or releases | Visual regression example uses `permissions: contents: read` and no publish scripts or secrets. |

## Accepted Risks And Deferrals

- PreviewHost still executes user project code locally. This is inherent to realistic Avalonia preview rendering and is mitigated by child-process isolation, explicit inputs, and local artifacts.
- Runtime mutation coverage is intentionally narrow. Broader arbitrary-property editing requires separate conversion, validation, rollback, and security validation.
- Remote inspection/control, no-code attach, process injection, CLR profiling, and private Avalonia hooks remain post-1.0 unless a separate threat model is designed.
- Generated screenshots and reports may contain sensitive UI data when the optional evidence policy is not configured. Upload remains an explicit external user/CI decision; AvaScope itself has no network-upload path.

The final non-blocking post-1.0 backlog and release-blocking audit is recorded in [POST_1_0_BACKLOG.md](POST_1_0_BACKLOG.md).

No release-blocking security risk is accepted for v1.0.0. A new P0/P1 security issue blocks the stable release until fixed, moved out of scope with explicit rationale, or accepted by a separate release decision.
