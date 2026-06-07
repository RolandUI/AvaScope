# AvaScope Gap Audit

Date: 2026-06-07

This audit ranks the highest-risk gaps after the first usable bridge, preview host, MCP, and CLI workflow set.

## P0 Gaps

### Runtime Session Close

Status: first slice complete.

`close_session` is listed in the intended MCP tool shape. Before M12, runtime bridge IPC did not expose a safe close handshake: the bridge could deactivate in-process, but a remote local client could not request closure without risking pipe teardown before a structured response was written.

Completed slice: bridge IPC now returns a structured close response, removes the local manifest, and shuts down the bridge server after the response is flushed.

### Diagnostics Tool

Status: first bridge diagnostics slice complete; richer preview/build/binding/layout/resource diagnostics remain open.

`diagnostics` is listed in the intended MCP tool shape. The first slice now reports service health, local bridge manifest path, process id, named-pipe transport, protocol health, stale manifests, invalid manifests, and unavailable IPC states. Current errors are still structured per operation; there is no historical last-error stream yet.

Completed slice: preview-host readiness diagnostics now report host assembly path, availability, isolated child-process mode, service metadata, and structured missing-host errors without launching user project code.

## P1 Gaps

### Reload And Hot Preview

Status: preview-session reload MVP complete; runtime bridge reload and live hot preview remain open.

`reload` is listed in the intended MCP tool shape. Preview sessions now persist the original request plus latest render result as Core metadata, and MCP `reload` re-renders an existing preview session through the isolated preview host. User code still runs only in one-shot preview host child processes. Runtime bridge reload and live hot preview sessions are not implemented yet.

Next slice: generalize or explicitly split the reload contract so runtime bridge reload behavior is defined without claiming app hot reload.

### Preview Resource Scope

Status: first app-resource slice complete; full design-time parity remains open.

PreviewHost can build a project, load a compiled view resource through `avares://`, and copy top-level resource entries from compiled project-root `App.axaml` into the isolated preview host application. Full `App.axaml` orchestration, merged dictionaries, app styles, culture variants, design data, and richer diagnostics are still limited.

Completed slice: project-root compiled `App.axaml` top-level resources are loaded into the isolated preview host before view loading.

Next slice: continue with persistent preview-session foundation before broader resource/style/design-data parity.

### Input Coverage

Status: pointer press/release slice complete; broader keyboard and targeting work remains open.

Input support is intentionally narrow: routed pointer move, routed pointer press/release, Button click, and focused TextBox text. Keyboard key events, focus targeting, drag/drop, and richer pointer button variants are not implemented.

Next slice: defer broader input until runtime reload semantics and packaging/CI basics are clarified.

## P2 Gaps

### Packaging And Release

The solution builds and tests locally, but there is no packaging, version stamping, NuGet package metadata, or release artifact workflow for `AvaScope.Bridge`, `AvaScope.Mcp`, or `AvaScope.Cli`.

Next slice: add package metadata and local pack validation after lifecycle/diagnostics gaps are closed.

### CI Workflow

Validation commands are documented, but there is no GitHub Actions workflow yet.

Next slice: add CI only after the local validation path is stable enough to avoid noisy failures.

## Selected Next Slice

Define the runtime `reload` contract next. Preview-session reload and pointer press/release are complete; the remaining `reload` ambiguity is now the highest-priority P1 workflow gap.
