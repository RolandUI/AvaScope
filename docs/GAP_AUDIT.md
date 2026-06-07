# AvaScope Gap Audit

Date: 2026-06-07

This audit ranks the highest-risk gaps after the first usable bridge, preview host, MCP, and CLI workflow set.

## P0 Gaps

### Runtime Session Close

`close_session` is listed in the intended MCP tool shape, but the current runtime bridge IPC does not expose a safe close handshake. The bridge can deactivate in-process, but a remote local client cannot yet request closure without risking pipe teardown before a structured response is written.

Next slice: design and implement a close handshake that returns a structured close response, removes the local manifest, and shuts down the bridge server without deadlocking the server task.

### Diagnostics Tool

`diagnostics` is listed in the intended MCP tool shape, but there is no tool yet for bridge, preview, build, binding, layout, or resource diagnostics. Current errors are structured per operation, but there is no aggregate diagnostic surface.

Next slice: start with preview/bridge health diagnostics that report version, process, transport, manifest path, and last structured error where available.

## P1 Gaps

### Reload And Hot Preview

`reload` is listed in the intended MCP tool shape, but preview sessions are one-shot child process executions. There is no persistent preview session or reload path yet.

Next slice: keep one-shot preview stable first; add persistent preview only after diagnostics and close lifecycle are reliable.

### Preview Resource Scope

PreviewHost can build a project and load a compiled view resource through `avares://`, but full `App.axaml` orchestration, app-level resources, culture variants, design data, and richer diagnostics are still limited.

Next slice: load app resources explicitly or document a supported project pattern before claiming full design-time parity.

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

Implement runtime `close_session` lifecycle support first. It is a P0 lifecycle gap, has a clear MCP tool target, and reduces stale bridge sessions/manifests during repeated agent workflows.
