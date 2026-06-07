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

Next slice: add richer preview failure diagnostics so build/render/XAML failures expose structured context beyond a trimmed message string.

## P1 Gaps

### Inspect Node Detail

Status: first slice complete.

`inspect_node` is part of the intended MCP tool shape. The first slice now returns a bounded single-node payload by stable visual or logical node id, including node id, tree kind, type, name, automation id, text, bounds, classes, and child count. It deliberately does not return descendants or arbitrary Avalonia object properties yet.

Next slice: richer properties, resources, binding diagnostics, or style diagnostics can be added after the first CLI runtime workflow is available.

### CLI Runtime Workflows

Status: current runtime command surface complete.

The CLI now supports `preview`, `mcp`, `attach`, `list-top-levels`, `screenshot`, `visual-tree`, `logical-tree`, `inspect-node`, `find-nodes`, `input`, `close-session`, `diagnostics`, and `reload`. Runtime CLI commands drive the local bridge through `LocalBridgeClient`, return structured `ToolResult<T>` output, and have deterministic invalid-argument, no-session, and fake bridge named-pipe success tests for the top-level, screenshot, tree, inspect-node, find, input, close, diagnostics, and runtime reload-check paths.

Next slice: move to the broader reload/hot preview foundation gap; CLI preview sessions are still one-shot and not persisted across CLI processes.

### Reload And Hot Preview

Status: preview-session reload MVP, durable MCP preview-session store, and runtime reload contract complete; runtime hot reload and live hot preview remain open.

`reload` is listed in the intended MCP tool shape. Preview sessions now persist the original request plus latest render result as Core metadata and as per-session local JSON records for the MCP host. MCP startup restores those preview records into `PreviewSessionRegistry`, and MCP `reload` re-renders an existing preview session through the isolated preview host. Runtime bridge session ids are checked through the local bridge health path and return a structured `runtime_reload_not_supported` diagnostic. User code still runs only in one-shot preview host child processes. Runtime hot reload and live hot preview sessions are not implemented yet.

Completed slice: runtime reload no longer falls through to a misleading preview `session_not_found` for active bridge sessions, and MCP-backed preview session records now survive MCP server process restarts.

### Preview Resource Scope

Status: first app-resource, app-style, app-data-template, resource-include, theme-dictionary, style-include, culture-variant, and project-owned design-data type slices complete; full design-time parity remains open.

PreviewHost can build a project, load a compiled view resource through `avares://`, copy top-level resource entries, merged resource dictionaries, and theme dictionaries from compiled project-root `App.axaml`, instantiate the project `Application` inside the isolated preview host process, apply direct or included `Application.Styles` and `Application.DataTemplates` to the preview window scope, apply a requested culture inside the child render process, and assign a project-owned design-data type as the root preview `DataContext`. It intentionally does not run project app startup/lifetime hooks such as `OnFrameworkInitializationCompleted`; full `App.axaml` startup orchestration and richer diagnostics are still limited.

Completed slice: project-root compiled `App.axaml` top-level resources, merged resource dictionaries, theme dictionaries, direct app-level styles, app-level `StyleInclude` entries, app-level data templates, culture-sensitive view loading, and typed-binding design-data `DataContext` assignment are validated before rendering the preview view.

Design-data boundary: AvaScope supports a project-owned public parameterless `designDataType`, loaded from the built project assembly and assigned to the root preview control inside `AvaScope.PreviewHost`. JSON object injection, dependency injection, remote data, and long-lived design-data state remain out of scope.

App startup boundary: project `Application.Initialize()` can run in the isolated PreviewHost process so compiled App.axaml composition is available, but project lifetime startup is explicitly deferred. AvaScope will not create a fake desktop lifetime, invoke project `OnFrameworkInitializationCompleted`, create the project's `MainWindow`, start application services, or keep a long-lived user app process for the current public-alpha preview boundary.

Next slice: move from preview parity to public-alpha onboarding with a getting-started sample that exercises preview and runtime bridge workflows.

### Getting Started Sample

Status: first slice complete.

The repository now includes `samples/AvaScope.GettingStartedApp`, a small Avalonia 12 app that an external developer can build, preview, run with the opt-in bridge, and inspect locally. The sample is part of the solution build and is marked `IsPackable=false`.

Completed slice: the documented CLI preview command renders `Views/MainView.axaml` to an ignored PNG artifact using project `App.axaml` resources/data templates and a public design-data type. The sample bridge is disabled unless `AVASCOPE_SAMPLE_BRIDGE` is set to `1` or `true`.

Next slice: refresh Release build/test/pack validation after the sample and CLI path normalization changes.

### Input Coverage

Status: focus targeting and basic key down/up slice complete; drag/drop and richer pointer/key behavior remain open.

Input support is intentionally narrow: routed pointer move, routed pointer press/release, Button click, focus by node id or coordinates, routed key down/up for focused or explicitly targeted input elements, and focused TextBox text. Drag/drop, richer pointer button variants, hardware-like key repeat, and full IME/text editing behavior are not implemented.

Next slice: defer broader input until `inspect_node` completes, then revisit drag/drop or richer keyboard/text behavior.

## P2 Gaps

### Packaging And Release

Status: first library package metadata slice, RID-based executable ZIP packaging slice, artifact verification manifest slice, and post-sample Release validation refresh complete; broader publishing workflow remains open.

`AvaScope.Protocol`, `AvaScope.Core`, and `AvaScope.Bridge` now have package ids, version metadata, descriptions, tags, repository metadata, README inclusion, and local `dotnet pack` validation into ignored `artifacts/packages`. `AvaScope.Mcp`, `AvaScope.Cli`, and `AvaScope.PreviewHost` are explicitly marked not packable in this slice.

Executable distribution now uses `eng/package-executables.ps1` to publish `AvaScope.Cli` into RID-specific framework-dependent output directories and create artifacts such as `artifacts/executables/avascope-win-x64-framework-dependent.zip` and `artifacts/executables/avascope-linux-x64-framework-dependent.zip`. Each artifact keeps `avascope`, `AvaScope.Mcp`, `AvaScope.PreviewHost`, `AvaScope.Core`, and `AvaScope.Protocol` co-located, stays under ignored local output, and does not require publishing credentials.

Artifact verification now uses `eng/verify-artifacts.ps1` to write ignored `artifacts/release-manifest.json` output with artifact kind, name, relative path, byte size, and SHA-256 hash for the three NuGet packages and executable ZIP artifacts. Verification fails when unexpected AvaScope package or executable ZIP artifacts are present outside the manifest-covered set.

Release refresh: after adding the getting-started sample and CLI relative path normalization, Release build/test/pack, executable packaging, artifact verification, and packaged-CLI sample preview smoke validation passed. The sample remains `IsPackable=false` and is not part of the release artifact manifest.

Next slice: defer self-contained packages, macOS artifact policy, and publishing automation until more product gaps are closed.

### CI Workflow

Status: first CI validation slice complete.

GitHub Actions now validates restore, Release build, Release tests, local library package creation, RID-based local executable ZIP package creation, and artifact manifest verification on push and pull request without publishing packages or requiring secrets.

Next slice: CI can later add publish/upload artifacts, self-contained outputs, or more RIDs when release policy is ready.

## Selected Next Slice

Expand preview failure diagnostics next. Release artifacts validate after the sample and CLI workflow updates, so the next public-alpha risk is making preview failures easier for agents and users to diagnose without scraping a single trimmed error string.
