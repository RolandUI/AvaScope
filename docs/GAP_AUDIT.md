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

`reload` is listed in the intended MCP tool shape, but preview sessions are one-shot child process executions. There is no persistent preview session or reload path yet.

Next slice: add the smallest persistent preview-session foundation before implementing actual reload.

### Preview Resource Scope

Status: first app-resource slice complete; full design-time parity remains open.

PreviewHost can build a project, load a compiled view resource through `avares://`, and copy top-level resource entries from compiled project-root `App.axaml` into the isolated preview host application. Full `App.axaml` orchestration, merged dictionaries, app styles, culture variants, design data, and richer diagnostics are still limited.

Completed slice: project-root compiled `App.axaml` top-level resources are loaded into the isolated preview host before view loading.

Next slice: continue with persistent preview-session foundation before broader resource/style/design-data parity.

### Input Coverage

Input support is intentionally narrow: routed pointer move, Button click, and focused TextBox text. Generic pointer press/release, keyboard key events, focus targeting, and drag/drop are not implemented.

Next slice: add one new input primitive at a time with headless validation.

## P2 Gaps

### Packaging And Release

The solution builds and tests locally, but there is no packaging, version stamping, NuGet package metadata, or release artifact workflow for `AvaScope.Bridge`, `AvaScope.Mcp`, or `AvaScope.Cli`.

Next slice: add package metadata and local pack validation after lifecycle/diagnostics gaps are closed.

### CI Workflow

Validation commands are documented, but there is no GitHub Actions workflow yet.

Next slice: add CI only after the local validation path is stable enough to avoid noisy failures.

## Selected Next Slice

Implement preview reload foundation next. The P0 lifecycle/diagnostics and first preview resource/preview-host readiness slices are complete; `reload` now needs stable preview-session metadata before a real reload command can be validated.
