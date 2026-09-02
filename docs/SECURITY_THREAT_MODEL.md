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

Application-defined actions are disabled by default. A host must set `enableCustomActions: true`, provide an exact activation allowlist, and register each action against a live visual instance. Discovery reports required state, current executability, parameter schema, and safety classification. Invocation validates the current generation-scoped target and schema on the UI thread and returns bounded audit evidence. Destructive classifications require two independent gates: `allowDestructiveCustomActions` at activation and `allowDestructive` on the request (or an isolated workflow state directory). The registered name never overrides the safety classification.

## Preview Execution

Preview rendering can build and load user project code. That code runs inside `AvaScope.PreviewHost`, not inside the CLI or MCP server process. Preview workflows use explicit project/view/profile inputs and write explicit local outputs. Dependency-injection startup, remote design-data loading, JSON object injection, and long-lived design-data services are deferred until they have a separate security model.

## File Outputs And Logs

AvaScope writes screenshots, diffs, JSON reports, HTML viewers, JUnit/SARIF-style report assets, launch stdout/stderr, and release artifacts only to explicit local paths or AvaScope-owned local temp directories. Generated files can contain UI text, paths, diagnostics, and screenshots from the user's app; agents should treat them as local sensitive artifacts and upload them only when the user or CI workflow explicitly chooses to.

Runtime scenario build and launch requests may contain sensitive environment values or application arguments. Normal lifecycle responses expose only environment-variable names and argument counts; raw values are passed directly to the owned child process and are not copied into metadata, diagnostics, or timelines. Captured stdout/stderr remain local artifacts and may still contain values printed by the application itself. Scenario cleanup records the launched session, PID, and process start time and terminates the process tree only when all identity checks still match; foreign, manually launched, already-replaced, or PID-reused processes are not killed.

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
| Preview user code loads inside MCP/CLI | Core launches the isolated PreviewHost process for preview rendering. |
| CI example publishes packages or releases | Visual regression example uses `permissions: contents: read` and no publish scripts or secrets. |

## Accepted Risks And Deferrals

- PreviewHost still executes user project code locally. This is inherent to realistic Avalonia preview rendering and is mitigated by child-process isolation, explicit inputs, and local artifacts.
- Runtime mutation coverage is intentionally narrow. Broader arbitrary-property editing requires separate conversion, validation, rollback, and security validation.
- Remote inspection/control, no-code attach, process injection, CLR profiling, and private Avalonia hooks remain post-1.0 unless a separate threat model is designed.
- Generated screenshots and reports may contain sensitive UI data. Upload and retention policy belongs to the user or CI workflow that handles the local artifacts.

The final non-blocking post-1.0 backlog and release-blocking audit is recorded in [POST_1_0_BACKLOG.md](POST_1_0_BACKLOG.md).

No release-blocking security risk is accepted for v1.0.0. A new P0/P1 security issue blocks the stable release until fixed, moved out of scope with explicit rationale, or accepted by a separate release decision.
