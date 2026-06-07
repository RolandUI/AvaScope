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

### Inspect Node Detail

Status: first slice complete.

`inspect_node` is part of the intended MCP tool shape. The first slice now returns a bounded single-node payload by stable visual or logical node id, including node id, tree kind, type, name, automation id, text, bounds, classes, and child count. It deliberately does not return descendants or arbitrary Avalonia object properties yet.

Next slice: richer properties, resources, binding diagnostics, or style diagnostics can be added after the first CLI runtime workflow is available.

### CLI Runtime Workflows

Status: attach, top-level listing, screenshot, tree, inspect-node, find, and input slices complete; close, reload, and diagnostics commands remain open.

The CLI now supports `preview`, `mcp`, `attach`, `list-top-levels`, `screenshot`, `visual-tree`, `logical-tree`, `inspect-node`, `find-nodes`, and `input`. Runtime CLI commands drive the local bridge through `LocalBridgeClient`, return structured `ToolResult<T>` output, and have deterministic invalid-argument, no-session, and fake bridge named-pipe success tests for the top-level, screenshot, tree, inspect-node, find, and input paths.

Next slice: add structured JSON `close-session` CLI command over `LocalBridgeClient`, keeping argument errors deterministic and output consistent with existing CLI preview behavior.

### Reload And Hot Preview

Status: preview-session reload MVP and runtime reload contract complete; runtime hot reload and live hot preview remain open.

`reload` is listed in the intended MCP tool shape. Preview sessions now persist the original request plus latest render result as Core metadata, and MCP `reload` re-renders an existing preview session through the isolated preview host. Runtime bridge session ids are now checked through the local bridge health path and return a structured `runtime_reload_not_supported` diagnostic. User code still runs only in one-shot preview host child processes. Runtime hot reload and live hot preview sessions are not implemented yet.

Completed slice: runtime reload no longer falls through to a misleading preview `session_not_found` for active bridge sessions.

### Preview Resource Scope

Status: first app-resource slice complete; full design-time parity remains open.

PreviewHost can build a project, load a compiled view resource through `avares://`, and copy top-level resource entries from compiled project-root `App.axaml` into the isolated preview host application. Full `App.axaml` orchestration, merged dictionaries, app styles, culture variants, design data, and richer diagnostics are still limited.

Completed slice: project-root compiled `App.axaml` top-level resources are loaded into the isolated preview host before view loading.

Next slice: continue with persistent preview-session foundation before broader resource/style/design-data parity.

### Input Coverage

Status: focus targeting and basic key down/up slice complete; drag/drop and richer pointer/key behavior remain open.

Input support is intentionally narrow: routed pointer move, routed pointer press/release, Button click, focus by node id or coordinates, routed key down/up for focused or explicitly targeted input elements, and focused TextBox text. Drag/drop, richer pointer button variants, hardware-like key repeat, and full IME/text editing behavior are not implemented.

Next slice: defer broader input until `inspect_node` completes, then revisit drag/drop or richer keyboard/text behavior.

## P2 Gaps

### Packaging And Release

Status: first library package metadata slice, RID-based executable ZIP packaging slice, and artifact verification manifest slice complete; broader release workflow remains open.

`AvaScope.Protocol`, `AvaScope.Core`, and `AvaScope.Bridge` now have package ids, version metadata, descriptions, tags, repository metadata, README inclusion, and local `dotnet pack` validation into ignored `artifacts/packages`. `AvaScope.Mcp`, `AvaScope.Cli`, and `AvaScope.PreviewHost` are explicitly marked not packable in this slice.

Executable distribution now uses `eng/package-executables.ps1` to publish `AvaScope.Cli` into RID-specific framework-dependent output directories and create artifacts such as `artifacts/executables/avascope-win-x64-framework-dependent.zip` and `artifacts/executables/avascope-linux-x64-framework-dependent.zip`. Each artifact keeps `avascope`, `AvaScope.Mcp`, `AvaScope.PreviewHost`, `AvaScope.Core`, and `AvaScope.Protocol` co-located, stays under ignored local output, and does not require publishing credentials.

Artifact verification now uses `eng/verify-artifacts.ps1` to write ignored `artifacts/release-manifest.json` output with artifact kind, name, relative path, byte size, and SHA-256 hash for the three NuGet packages and executable ZIP artifacts. Verification fails when unexpected AvaScope package or executable ZIP artifacts are present outside the manifest-covered set.

Next slice: defer self-contained packages, macOS artifact policy, and publishing automation until more product gaps are closed.

### CI Workflow

Status: first CI validation slice complete.

GitHub Actions now validates restore, Release build, Release tests, local library package creation, RID-based local executable ZIP package creation, and artifact manifest verification on push and pull request without publishing packages or requiring secrets.

Next slice: CI can later add publish/upload artifacts, self-contained outputs, or more RIDs when release policy is ready.

## Selected Next Slice

Add CLI runtime close-session next. The CLI can now inspect and control runtime sessions, so the next vertical slice should expose a direct local session closure workflow.
