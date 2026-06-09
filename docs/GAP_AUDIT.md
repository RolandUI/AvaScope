# AvaScope Gap Audit

Date: 2026-06-07

This audit ranks the highest-risk gaps after the first usable bridge, preview host, MCP, and CLI workflow set.

## P0 Gaps

### Runtime Session Close

Status: first slice complete.

`close_session` is listed in the intended MCP tool shape. Before M12, runtime bridge IPC did not expose a safe close handshake: the bridge could deactivate in-process, but a remote local client could not request closure without risking pipe teardown before a structured response was written.

Completed slice: bridge IPC now returns a structured close response, removes the local manifest, and shuts down the bridge server after the response is flushed.

### Runtime Safety Boundary

Status: public-alpha safety boundary slice complete.

Runtime bridge activation remains explicit and opt-in through `AvaScopeBridge.Activate(...)`. Active bridge manifests now include `transportScope: "local_only"` so discovery metadata records the intended boundary directly. The bridge IPC server uses local named pipes with current-user-only pipe access where the platform supports it, and unsupported manifest transport scopes are treated as invalid diagnostics rather than attachable sessions.

Completed slice: protocol, bridge, and core diagnostics tests cover local-only manifest scope, legacy manifest compatibility, current local pipe health, and invalid unsupported transport manifests. README and sample README document that the bridge does not open network listeners and that runtime control remains narrow for public alpha.

### Diagnostics Tool

Status: bridge diagnostics, preview failure details, preview binding/resource diagnostics, source-backed `x:DataType` binding diagnostics, preview layout warnings, stale preview-session diagnostics, and bounded diagnostic issue provenance slices complete.

`diagnostics` is listed in the intended MCP tool shape. The first slice now reports service health, local bridge manifest path, process id, named-pipe transport, protocol health, stale manifests, invalid manifests, and unavailable IPC states. Current errors are still structured per operation; there is no historical last-error stream yet.

Completed slice: preview-host readiness diagnostics now report host assembly path, availability, isolated child-process mode, service metadata, and structured missing-host errors without launching user project code.

Completed slice: preview build/render failures can now include bounded `error.details` fields such as `phase`, paths, build exit code, and build output tail while preserving the existing `code/message` result shape through Core, CLI, MCP, and preview-session storage.

Completed slice: successful preview responses can now include bounded advisory diagnostics for missing `DataContext`, missing or invalid binding converter resources, unresolved resource keys, conservative binding path failures, source-backed `x:DataType` binding path mismatches, missing inherited `x:DataType` on `CompiledBinding`, text clipping/truncation, clipped or unreachable content, sibling overlap, and too-small hit targets.

Completed slice: `diagnostics` now includes preview-session store records so stale and invalid AvaScope-owned preview metadata can be identified without loading user projects.

Completed slice: `diagnostics` now also returns bounded `diagnosticIssues` entries derived in Core from diagnostics summary issues, bridge manifests, preview-host readiness, and preview-session store records. Each issue includes source, severity, status, provenance, observed timestamp, and related path/session/process metadata where available while preserving the legacy `issues` list.

Next slice: deeper binding-engine runtime telemetry and style/resource provenance parity remains limited to public Avalonia API availability.

## P1 Gaps

### Inspect Node Detail

Status: first node detail and computed property inspection slices complete.

`inspect_node` is part of the intended MCP tool shape. The first slice now returns a bounded single-node payload by stable visual or logical node id, including node id, tree kind, type, name, automation id, text, bounds, classes, and child count. It deliberately does not return descendants or arbitrary Avalonia object properties yet.

Completed slice: `inspect_node` now returns bounded computed visual/style/layout values for high-value properties, with public Avalonia diagnostic priority where available and explicit `unknown`/`not_available` provenance when source details are not reliable.

Next slice: richer resource-chain explanations can be added when public Avalonia APIs expose reliable provenance beyond priority/source diagnostics.

### CLI Runtime Workflows

Status: current runtime command surface plus CLI self-test complete.

The CLI now supports `preview`, `mcp`, `doctor`, `attach`, `list-top-levels`, `screenshot`, `visual-tree`, `logical-tree`, `inspect-node`, `find-nodes`, `input`, `close-session`, `diagnostics`, `reload`, `diff`, `cleanup`, `create-preview-session`, `list-preview-sessions`, `reload-preview-session`, `close-preview-session`, and `watch-preview-session`. Runtime CLI commands drive the local bridge through `LocalBridgeClient`, return structured `ToolResult<T>` output, and have deterministic invalid-argument, no-session, and fake bridge named-pipe success tests for the top-level, screenshot, tree, inspect-node, find, input, close, diagnostics, and runtime reload-check paths.

Completed slice: `preview --sizes` adds deterministic multi-size preview output and optional contact-sheet generation, while `diff` adds explicit same-size screenshot comparison with tolerance and structured pass/fail output.

Completed slice: CLI preview-session commands can create, list, reload, and close persisted preview-session metadata across CLI processes using the same local preview-session store as MCP.

Completed slice: `watch-preview-session` can watch the stored preview request's project/view files or an explicit watch path list, debounce changes, and reload through isolated PreviewHost child processes with bounded structured events.

Completed slice: `baseline-create` and `baseline-check` provide manifest-backed visual regression workflows over multi-size preview and screenshot diff primitives, with explicit current/diff artifact directories and non-zero check exits for changed variants.

Completed slice: `doctor` reports CLI/MCP/PreviewHost co-location, bridge manifest diagnostics, preview-session store diagnostics, and actionable readiness issues without loading user projects. Packaged release validation now runs doctor with isolated manifest/store paths before sample preview smoke.

Completed slice: project-local `avascope.preview.json` profiles can drive `preview` and `create-preview-session`, with explicit CLI options overriding profile values and profile output paths resolving relative to the profile file.

Next slice: agent workflow documentation and validation; the one-shot `preview` command remains intentionally one-shot.

### Reload And Hot Preview

Status: preview-session reload MVP, durable MCP/CLI preview-session store, CLI file-watch reload, duplicate-burst skip, and runtime reload contract complete; runtime hot reload and persistent live preview host processes remain open.

`reload` is listed in the intended MCP tool shape. Preview sessions now persist the original request plus latest render result as Core metadata and as per-session local JSON records for MCP and CLI hosts. MCP startup and CLI preview-session commands restore those preview records into `PreviewSessionRegistry`, and reload re-renders an existing preview session through the isolated preview host. Runtime bridge session ids are checked through the local bridge health path and return a structured `runtime_reload_not_supported` diagnostic. User code still runs only in one-shot preview host child processes. Runtime hot reload and persistent live preview host sessions are not implemented yet.

Completed slice: runtime reload no longer falls through to a misleading preview `session_not_found` for active bridge sessions, and MCP-backed preview session records now survive MCP server process restarts.

Completed slice: CLI file-watch reload adds bounded `watch-preview-session` events for changed files and preview-session reloads without keeping user project code loaded.

Completed slice: `watch-preview-session` now snapshots watched inputs and reports unchanged duplicate file watcher bursts as `skipped`, avoiding unnecessary PreviewHost child-process launches while preserving one-shot isolated rendering for real input changes.

### Preview Resource Scope

Status: first app-resource, app-style, app-data-template, app-data-context, resource-include, theme-dictionary, style-include, culture-variant, and project-owned design-data type slices complete; full design-time parity remains open.

PreviewHost can build a project, load a compiled view resource through `avares://`, copy top-level resource entries, merged resource dictionaries, and theme dictionaries from compiled project-root `App.axaml`, instantiate the project `Application` inside the isolated preview host process, apply direct or included `Application.Styles` and `Application.DataTemplates` to the preview window scope, apply `Application.DataContext` as a fallback root preview `DataContext`, apply a requested culture inside the child render process, and assign a project-owned design-data type as the root preview `DataContext`. It intentionally does not run project app startup/lifetime hooks such as `OnFrameworkInitializationCompleted`; full `App.axaml` startup orchestration and richer diagnostics are still limited.

Completed slice: project-root compiled `App.axaml` top-level resources, merged resource dictionaries, theme dictionaries, direct app-level styles, app-level `StyleInclude` entries, app-level data templates, fallback app-level `DataContext`, culture-sensitive view loading, and typed-binding design-data `DataContext` assignment are validated before rendering the preview view.

Design-data boundary: AvaScope supports a project-owned public parameterless `designDataType`, loaded from the built project assembly and assigned to the root preview control inside `AvaScope.PreviewHost`. JSON object injection, dependency injection, remote data, and long-lived design-data state remain out of scope.

App startup boundary: project `Application.Initialize()` can run in the isolated PreviewHost process so compiled App.axaml composition is available, but project lifetime startup is explicitly deferred. AvaScope will not create a fake desktop lifetime, invoke project `OnFrameworkInitializationCompleted`, create the project's `MainWindow`, start application services, or keep a long-lived user app process for the current public-alpha preview boundary.

Next slice: move from preview parity to public-alpha onboarding with a getting-started sample that exercises preview and runtime bridge workflows.

### Getting Started Sample

Status: first slice complete.

The repository now includes `samples/AvaScope.GettingStartedApp`, a small Avalonia 12 app that an external developer can build, preview, run with the opt-in bridge, and inspect locally. The sample is part of the solution build and is marked `IsPackable=false`.

Completed slice: the documented CLI preview command renders `Views/MainView.axaml` to an ignored PNG artifact using project `App.axaml` resources/data templates and a public design-data type. The sample bridge is disabled unless `AVASCOPE_SAMPLE_BRIDGE` is set to `1` or `true`.

Next slice: refresh Release build/test/pack validation after the sample and CLI path normalization changes.

### Input Coverage

Status: focus targeting, basic key down/up, target-aware TextBox text editing, and targeted TextBox clearing slices complete; drag/drop and richer pointer/key behavior remain open.

Input support is intentionally narrow: routed pointer move, routed pointer press/release, Button click, focus by node id or coordinates, routed key down/up for focused or explicitly targeted input elements, TextBox text input for focused or explicitly targeted TextBox controls, and `clear_text` for focused or explicitly targeted writable TextBox controls. TextBox text input respects read-only targets and replaces a current selection when one exists; `clear_text` also rejects read-only targets and resets caret/selection to 0. Drag/drop, richer pointer button variants, hardware-like key repeat, and full IME/text editing behavior are not implemented.

Next slice: defer broader input until a drag/drop, pointer-button, or hardware-like keyboard path can be covered by deterministic public Avalonia APIs.

## P2 Gaps

### Packaging And Release

Status: first library package metadata slice, RID-based executable ZIP packaging slice, opt-in self-contained ZIP lane, artifact verification manifest slice, post-sample Release validation refresh, and manual NuGet publish workflow complete; broader installer and macOS distribution policy remain open.

`AvaScope.Protocol`, `AvaScope.Core`, and `AvaScope.Bridge` now have package ids, version metadata, descriptions, tags, repository metadata, README inclusion, and local `dotnet pack` validation into ignored `artifacts/packages`. `AvaScope.Mcp`, `AvaScope.Cli`, and `AvaScope.PreviewHost` are explicitly marked not packable in this slice.

Executable distribution now uses `eng/package-executables.ps1` to publish `AvaScope.Cli` into RID-specific output directories and create artifacts such as `artifacts/executables/avascope-win-x64-framework-dependent.zip` and `artifacts/executables/avascope-linux-x64-framework-dependent.zip`. Each artifact keeps `avascope`, `AvaScope.Mcp`, `AvaScope.PreviewHost`, `AvaScope.Core`, and `AvaScope.Protocol` co-located, stays under ignored local output, and does not require publishing credentials. Framework-dependent remains the default package kind, and self-contained ZIPs are available through explicit script parameters.

Artifact verification now uses `eng/verify-artifacts.ps1` to write ignored `artifacts/release-manifest.json` output with artifact kind, executable package kind, name, relative path, byte size, and SHA-256 hash for the three NuGet packages and executable ZIP artifacts. Verification fails when unexpected AvaScope package or executable ZIP artifacts are present outside the manifest-covered set.

Release refresh: after adding the getting-started sample and CLI relative path normalization, Release build/test/pack, executable packaging, artifact verification, and packaged-CLI sample preview smoke validation passed. The sample remains `IsPackable=false` and is not part of the release artifact manifest.

NuGet publishing now uses `eng/publish-nuget.ps1` to push the three library packages from `artifacts/packages` in dependency order, with `-DryRun` validation and an API key supplied out of band. GitHub Actions publishes from `master` or `main` when `Directory.Build.props` contains a version with no matching remote `v<Version>` tag. The release workflow creates that tag, publishes those packages to GitHub Packages, and uploads `.nupkg` files, RID executable ZIPs, and the release manifest to the matching GitHub Release.

Next slice: keep default GitHub Release assets framework-dependent until release policy changes; defer macOS artifact policy and installer publishing until there is a signing/notarization/installer validation surface.

### CI Workflow

Status: first CI validation slice complete.

GitHub Actions now validates restore, Release build, Release tests, local library package creation, RID-based local executable ZIP package creation, and artifact manifest verification on push and pull request without publishing packages or requiring secrets. A separate `Release` workflow publishes only when the repository version has not been released yet, using the `NUGET_API_KEY` repository secret plus the workflow `GITHUB_TOKEN` for GitHub Packages and GitHub Release assets, and can also be run manually with publish disabled by default.

Next slice: CI can later add upload artifacts, self-contained outputs, or more RIDs when release policy is ready.

## Selected Next Slice

No public-alpha blocking slice remains after M50, and W9-W16 completed the stored feature-request first slices plus CLI preview sessions, file-watch reload, manifest-backed visual regression, source-backed typed binding diagnostics, target-aware TextBox input, preview startup parity, and opt-in self-contained distribution hardening.

Selected next slice: W24 visual regression CI kit. W18 completed CLI doctor/self-test, W19 completed preview profiles, W20 added a validated packaged-CLI agent workflow, W21 added deterministic runtime `clear_text` input, W22 added bounded diagnostics issue provenance, and W23 reduced duplicate live-preview reloads with unchanged-input skip events. The next product improvement is making baseline-check output easier to upload and summarize in CI. W25 should finish with a release-candidate audit.
