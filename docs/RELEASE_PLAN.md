# AvaScope Release Plan

AvaScope development is release-based from `2026-06-09` onward and GitHub-driven from `2026-06-10` onward. Each new version must have an explicit release target, GitHub milestone, and implementation issues before implementation starts. The version bump is the final release commit after the target scope is complete.

## Release Workflow

1. Define the next release target in this file and create the matching GitHub milestone.
2. Create a release tracking issue plus vertical-slice implementation issues for the milestone.
3. Lock the intended release scope before implementation. Scope changes are allowed only when they are recorded in the GitHub issue and, when release-level, here before the release commit.
4. Complete each release issue as a vertical slice with tests or an explicit validation note.
5. Move the release target to `Release Candidate` only after every in-scope GitHub issue is closed as completed and the release gate passes.
6. Make the release commit by increasing `Directory.Build.props` `<Version>` to the target version and committing with subject `Release <version>`.
7. Push the release commit to `master`. The GitHub `Release` workflow validates, publishes packages/assets when credentials are available, and creates the matching `v<version>` tag.

The release commit must not include unfinished feature work. It should contain only the version bump and release-readiness metadata required to publish the already validated scope.

## Release States

- `Planned`: scope is defined, implementation has not started.
- `In Progress`: at least one release milestone is actively being implemented.
- `Release Candidate`: all release milestones are complete and the local release gate passed; version bump may be committed.
- `Released`: the matching GitHub tag and release assets exist.
- `Deferred`: target was intentionally stopped or moved to a later version.

## Release Gate

Before a target can move to `Release Candidate`, run the release validation commands from `docs/VALIDATION.md`, including:

```powershell
dotnet build AvaScope.slnx
dotnet test AvaScope.slnx --no-build
powershell -NoProfile -ExecutionPolicy Bypass -File .\eng\create-local-release.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File .\eng\publish-github-release.ps1 -Tag v<version> -DryRun
git diff --check
```

If the release includes public workflow or packaging changes, also validate the packaged CLI paths documented in `docs/AGENT_WORKFLOW.md`.

## Roadmap Planning Rules

The roadmap below is the working plan to `v1.0.0`. It is intentionally release-shaped so every implementation slice remains shippable, validated, and reversible.

- `v0.6.0` is released.
- `v0.7.0` is released.
- `v0.7.0` starts the agent-first product direction: AvaScope becomes an agent control plane for inspecting, changing, validating, and explaining Avalonia UI behavior through structured CLI/MCP workflows.
- `v0.8.0` is the active release target through GitHub milestone `v0.8.0`, release issue #19, and implementation issue #22 currently in progress.
- `v0.9.0` through `v1.0.0` are planned targets. Their scope may be refined before they become the current release target, but changes must be recorded here before implementation starts.
- Each release must preserve the current product boundaries: MCP and CLI stay adapters over Core, runtime bridge activation stays opt-in and local-only, PreviewHost stays isolated from the MCP server, and private Avalonia/runtime hooks remain out of the default path.
- Every release must include targeted tests, full build/test validation, release dry-run validation, documentation updates, and explicit deferrals.
- A release may be split into a patch release if a P0/P1 regression blocks users or CI, but patch scope must remain defect-focused.

## v1.0 Readiness Definition

AvaScope reaches `v1.0.0` when it is a stable local Avalonia inspection, preview, automation, and visual-regression toolkit that agents and developers can rely on without project-specific handholding.

Required `v1.0.0` properties:

- Stable public package identities for `AvaScope.Protocol`, `AvaScope.Core`, and `AvaScope.Bridge`, with SemVer compatibility rules documented.
- Stable CLI command names for runtime inspection, runtime mutation, preview, preview sessions, diagnostics, animation sampling, and visual regression.
- Stable MCP tool names and schemas, with version/capability negotiation for additive future changes.
- Runtime bridge workflows are safe, local-only, opt-in, observable, and resilient to stale manifests, closed processes, mismatched targets, and session cleanup.
- Runtime mutation workflows are reversible, bounded, observable, and able to produce screenshot/diff evidence for every applied change.
- Preview workflows handle normal Avalonia 12 project shapes with reliable resource/style/template/design-data behavior, bounded diagnostics, and isolated failure handling.
- Live preview workflows have explicit close, TTL, crash, cleanup, cancellation, and performance behavior.
- Visual regression workflows are CI-ready with structured reports, uploadable artifacts, threshold/mask support, and documented GitHub Actions usage.
- Release artifacts are reproducible and verified for the supported platforms, and installation/upgrade documentation is complete.
- No known P0/P1 bugs remain open; any deferred areas are documented as post-1.0 non-goals or future work.

## Released Target: v0.4.0

- Release: `v0.4.0`
- Target Version: `0.4.0`
- Release State: `Released`
- Scope Lock: `2026-06-09`
- Release Commit: `c3cbd16` (`Release 0.4.0`)
- Local Release Gate: passed on `2026-06-10`
- Published At: `2026-06-10T09:02:02Z`
- GitHub Release: https://github.com/RolandUI/AvaScope/releases/tag/v0.4.0
- Previous Release: `v0.3.0`

### v0.4.0 Release Goals

The `v0.4.0` release target is focused on runtime bridge reliability, attach ergonomics, and target stability. The goal is to make runtime inspection/control dependable enough for repeated agent workflows against real local applications before deeper preview-session work begins.

1. `RG-0.4.0-1 Bridge Session Discovery And Cleanup`: make local bridge discovery resilient to stale manifests, dead processes, duplicate records, and user-selected manifest directories.
   Success signal: diagnostics and attach flows can distinguish active, stale, invalid, incompatible, and unauthorized local bridge sessions without hanging or guessing.
2. `RG-0.4.0-2 Attach Target Selection`: support explicit attach selection by session id, process id, process name, and manifest path where safe.
   Success signal: CLI/MCP users can target the intended local app deterministically when multiple bridge-enabled apps are running.
3. `RG-0.4.0-3 Runtime Target Stability`: strengthen node/top-level target references so tree/search results remain usable across follow-up inspect, screenshot, and input commands, or fail with actionable stale-target diagnostics.
   Success signal: target handoff includes generation/timestamp/context metadata and returns bounded mismatch details when the target moved, disappeared, or belongs to a different tree/top-level.
4. `RG-0.4.0-4 Runtime Input Reliability`: harden non-destructive runtime input for common agent workflows without broadening into destructive actions.
   Success signal: click, pointer move/press/release, focus, key text, key down/up, and `clear_text` have clearer target validation, button/modifier metadata where supported, and deterministic bridge/CLI tests.
5. `RG-0.4.0-5 Runtime Diagnostics And Observability`: add enough request/session diagnostics for agents to decide whether to retry, reattach, or stop.
   Success signal: diagnostics can report recent bridge/session issues, protocol/capability mismatches, stale target causes, request ids, and cleanup outcomes in bounded structured data.
6. `RG-0.4.0-6 Guarded Release`: ship only after the runtime attach and stability work passes targeted tests, full validation, packaged CLI smoke checks, and release dry-runs.

### v0.4.0 Milestone Map

- `R0.4.0-M1 Bridge Session Discovery And Cleanup` delivers `RG-0.4.0-1`; Status: `Done`.
- `R0.4.0-M2 Attach Target Selection` delivers `RG-0.4.0-2`; Status: `Done`.
- `R0.4.0-M3 Runtime Target Stability` delivers `RG-0.4.0-3`; Status: `Done`.
- `R0.4.0-M4 Runtime Input Reliability` delivers `RG-0.4.0-4`; Status: `Done`.
- `R0.4.0-M5 Runtime Diagnostics And Documentation` delivers `RG-0.4.0-5`; Status: `Done`.
- `R0.4.0-M6 Release Candidate And Version Bump` delivers `RG-0.4.0-6`; Status: `Done`.

### v0.4.0 Acceptance Criteria

- Runtime attach commands never silently select an ambiguous session when multiple viable sessions exist.
- Stale bridge manifests and dead processes are reported as diagnostics and can be cleaned through documented workflows.
- Runtime target references include enough context for follow-up commands to reject mismatched top-levels, tree kinds, closed sessions, and stale node ids.
- CLI and MCP runtime workflows preserve structured `ToolResult<T>` compatibility.
- The getting-started sample validates at least one attach/list/tree/find/inspect/input/close workflow through the packaged CLI.
- Runtime safety remains local-only and opt-in; no network listener, injection, production remote control, or destructive input action is introduced.

### v0.4.0 Implementation Validation

- `2026-06-10`: `dotnet build AvaScope.slnx --no-restore -v:minimal` passed with 0 warnings and 0 errors after runtime bridge reliability implementation.
- `2026-06-10`: Runtime reliability targeted tests passed with 61 tests covering Core manifest selection/cleanup/diagnostics, protocol target/input/cleanup shapes, CLI manifest-path/process-name/custom-directory workflows, MCP cleanup and attach selection, MCP stdio tool listing, and headless bridge input metadata.
- `2026-06-10`: `dotnet test AvaScope.slnx --no-build` passed with 228 tests after isolating diagnostics smoke tests from default preview-session temp records.
- `2026-06-10`: `powershell -NoProfile -ExecutionPolicy Bypass -File .\eng\create-local-release.ps1` passed for `v0.4.0` after stopping stale packaged CLI/dotnet processes from the local artifact output; Release build/test passed with 228 tests, three `0.4.0` packages, win-x64 and linux-x64 framework-dependent ZIPs, release manifest, packaged doctor smoke, and packaged sample preview smoke.
- `2026-06-10`: `powershell -NoProfile -ExecutionPolicy Bypass -File .\eng\publish-github-release.ps1 -Tag v0.4.0 -DryRun` passed for `v0.4.0` assets.
- `2026-06-10`: Packaged Windows CLI runtime smoke passed against `samples\AvaScope.GettingStartedApp`: `attach --process-name`, `list-top-levels`, `visual-tree`, `find-nodes`, `inspect-node`, `screenshot`, `input --action pointer_move`, and `close-session`.
- `2026-06-10`: `powershell -NoProfile -ExecutionPolicy Bypass -File .\eng\validate-bug-reports.ps1` passed; 15 intake files scanned.
- `2026-06-10`: `git diff --check` passed with only line-ending normalization warnings.
- `2026-06-10`: `powershell -NoProfile -ExecutionPolicy Bypass -File .\eng\validate-release-commit.ps1 -Version 0.4.0 -CommitSubject "Release 0.4.0" -RequiredState "Release Candidate"` passed for the `v0.4.0` release commit guard.
- `2026-06-10`: GitHub Release workflow `27264946927` passed for `Release 0.4.0`; tag `v0.4.0` and six GitHub Release assets were published at `2026-06-10T09:02:02Z`.
- `2026-06-10`: GitHub CI workflow `27264946899` passed for `Release 0.4.0`.
- `2026-06-10`: `gh release view v0.4.0` confirmed the public release URL and six uploaded assets: three `0.4.0` `.nupkg` files, win-x64 and linux-x64 framework-dependent ZIPs, and `release-manifest.json`.
- `2026-06-10`: `git ls-remote --tags origin refs/tags/v0.4.0` confirmed tag `v0.4.0` at release commit `c3cbd16`.

### v0.4.0 Explicit Deferrals

- Runtime hot reload remains out of scope.
- Drag/drop, IME-level typing, hardware-like keyboard repeat, and destructive runtime actions remain out of scope.
- No-code attach, injection, CLR profiling, or private runtime hooks remain out of scope.
- Remote inspection remains out of scope.

## Completed Target: v0.5.0

- Release: `v0.5.0`
- Target Version: `0.5.0`
- Release State: `Released`
- Scope Lock: `2026-06-10`
- Release Commit: `e4b6029`
- Local Release Gate: passed `2026-06-10`
- Published At: `2026-06-10T13:06:09Z`
- GitHub Release: https://github.com/RolandUI/AvaScope/releases/tag/v0.5.0
- Previous Release: `v0.4.0`

### v0.5.0 Release Goals

The `v0.5.0` release target is focused on PreviewHost fidelity for normal Avalonia 12 project shapes. The goal is to reduce the gap between a successful `.axaml` render and a trustworthy design-time preview.

1. `RG-0.5.0-1 Project Loading Robustness`: handle multi-project solutions, project references, multi-targeting selection, generated files, Avalonia resources, and build-output discovery more reliably.
   Success signal: preview failures identify readiness, project graph, target framework, build, resource lookup, and render phases with actionable details.
2. `RG-0.5.0-2 Resource And Style Provenance`: improve resource/style diagnostics through public Avalonia APIs and source metadata.
   Success signal: missing resources, dynamic resources, style selectors, theme variants, and computed values report source/provenance when reliable, otherwise explicit `unknown`/`not_available`.
3. `RG-0.5.0-3 Design Data Profiles`: expand project-local preview profiles for safe design-data scenarios without executing arbitrary remote services.
   Success signal: profiles can declare design-data type, culture, theme, size, DPI, output paths, and optional named variants with deterministic CLI/MCP behavior.
4. `RG-0.5.0-4 Preview Diagnostics Triage`: make preview advisory diagnostics easier for agents to prioritize.
   Success signal: diagnostics include severity, category, provenance, affected node/path, suggested next action, and suppression/non-applicable reasons where relevant.
5. `RG-0.5.0-5 Preview Fidelity Samples`: add sample scenarios covering resources, styles, templates, design data, culture, and theme variants.
   Success signal: sample commands validate expected rendered artifacts and diagnostics through source and packaged CLI.
6. `RG-0.5.0-6 Guarded Release`: ship only after preview fidelity tests, sample smokes, full validation, and release dry-runs pass.

### v0.5.0 Milestone Map

- `R0.5.0-M1 Project Graph And Build Diagnostics`; Status: `Done`.
- `R0.5.0-M2 Resource And Style Provenance`; Status: `Done`.
- `R0.5.0-M3 Design Data Profiles And Variants`; Status: `Done`.
- `R0.5.0-M4 Preview Diagnostic Prioritization`; Status: `Done`.
- `R0.5.0-M5 Preview Fidelity Samples And Docs`; Status: `Done`.
- `R0.5.0-M6 Release Candidate And Version Bump`; Status: `Done`.

### v0.5.0 Implementation Validation

- `2026-06-10`: NuGet package check confirmed Avalonia `12.0.4` remains the current stable Avalonia 12 line for the repo's referenced packages; no package-version change was needed.
- `2026-06-10`: `dotnet build AvaScope.slnx --no-restore -v:minimal` passed with 0 warnings and 0 errors after adding `v0.5.0` project metadata, diagnostic triage, profile variants, and sample coverage.
- `2026-06-10`: Targeted protocol, PreviewHost, and CLI profile-variant tests passed with 4 tests.
- `2026-06-10`: `dotnet test AvaScope.slnx --no-build` passed with 231 tests.
- `2026-06-10`: Source CLI sample previews passed for `samples\AvaScope.GettingStartedApp` profile variants `main --variant dark` and `main --variant hu`.
- `2026-06-10`: `powershell -NoProfile -ExecutionPolicy Bypass -File .\eng\create-local-release.ps1` passed for `v0.5.0`; Release build/test passed with 231 tests, three `0.5.0` packages, win-x64 and linux-x64 framework-dependent ZIPs, release manifest, packaged doctor smoke, and packaged sample preview smoke.
- `2026-06-10`: `powershell -NoProfile -ExecutionPolicy Bypass -File .\eng\publish-github-release.ps1 -Tag v0.5.0 -DryRun` passed for `v0.5.0` assets.
- `2026-06-10`: `git diff --check` passed with only line-ending normalization warnings.
- `2026-06-10`: `powershell -NoProfile -ExecutionPolicy Bypass -File .\eng\validate-release-commit.ps1 -Version 0.5.0 -CommitSubject "Release 0.5.0" -RequiredState "Release Candidate"` passed for the `v0.5.0` release commit guard.
- `2026-06-10`: GitHub Release workflow `27277929158` passed for `Release 0.5.0`; tag `v0.5.0` and six GitHub Release assets were published at `2026-06-10T13:06:09Z`.
- `2026-06-10`: GitHub CI workflow `27277929144` passed for `Release 0.5.0`.
- `2026-06-10`: `gh release view v0.5.0` confirmed the public release URL and six uploaded assets: three `0.5.0` `.nupkg` files, win-x64 and linux-x64 framework-dependent ZIPs, and `release-manifest.json`.
- `2026-06-10`: `git ls-remote --tags origin refs/tags/v0.5.0` confirmed tag `v0.5.0` at release commit `e4b6029`.

### v0.5.0 Explicit Deferrals

- Full application startup/lifetime execution remains deferred unless a safe isolated model is designed.
- Dependency-injection service startup, remote data loading, and long-lived design-data state remain out of scope.
- Private Avalonia designer APIs remain out of scope.

## Released Target: v0.6.0

- Release: `v0.6.0`
- Target Version: `0.6.0`
- Release State: `Released`
- Scope Lock: `2026-06-10`
- Release Commit: `4af5284` (`Release 0.6.0`)
- Local Release Gate: passed `2026-06-10`
- Published At: `2026-06-10T15:11:44Z`
- GitHub Release: https://github.com/RolandUI/AvaScope/releases/tag/v0.6.0
- Previous Release: `v0.5.0`

### v0.6.0 Release Goals

The `v0.6.0` release target is focused on runtime debugging and agent validation ergonomics requested after the `v0.5.0` release, plus preview-session lifecycle observability that preserves the existing isolated one-shot PreviewHost process boundary. A fully persistent long-lived PreviewHost process remains deferred until its process-management, TTL, crash-recovery, and cleanup model can be validated without weakening isolation.

1. `RG-0.6.0-1 Preview Session Lifecycle Observability`: expose bounded lifecycle state and session events for existing preview sessions without moving user code into the MCP server process.
   Success signal: session creation, reload, reload failure, and close events are visible through session summaries, and unsupported long-lived host semantics are explicit.
2. `RG-0.6.0-2 Incremental Reload Boundary`: keep existing watcher/reload behavior deterministic and document the hot AXAML boundary for unsupported persistent-host reuse.
   Success signal: existing reloads and unchanged-input skips remain compatible, unsupported persistent reuse returns explicit lifecycle status rather than implying stale previews are live.
3. `RG-0.6.0-3 Runtime Input Expansion` (`FEAT-0009`): broaden non-destructive runtime input beyond simple button clicks.
   Success signal: CLI/MCP/bridge workflows can switch tab/selectable controls, send common navigation keys and modifiers, and exercise wheel/drag/pan/scrollbar gestures where public Avalonia APIs make behavior deterministic.
4. `RG-0.6.0-4 Runtime State Inspection` (`FEAT-0010`, `FEAT-0011`, `FEAT-0012`): expose scroll, binding/context, and opt-in custom control debug state.
   Success signal: selected nodes can report ScrollViewer metrics, DataContext type, bounded binding path/value metadata where reliable, and app-provided debug fields through an explicit opt-in contract.
5. `RG-0.6.0-5 Runtime Session And Launch Ergonomics` (`FEAT-0013`, `FEAT-0015`): reduce manual attach/setup friction for repeated agent workflows.
   Success signal: users can select the latest active matching session safely, stale sessions stay out of default selection, and a bridge-enabled launch helper returns session/top-level/process/stdout/stderr details.
6. `RG-0.6.0-6 Screenshot Assertions And Region Checks` (`FEAT-0014`): add focused pixel/region assertions on top of existing screenshot diff and baseline primitives.
   Success signal: CLI/MCP workflows can crop or check regions for non-empty, mostly blank, changed, and unchanged conditions with structured pass/fail output and deterministic artifacts.
7. `RG-0.6.0-7 Session Event Stream And Lifecycle Budget`: expose bounded preview-session events while keeping process count and artifact behavior bounded by the existing one-shot isolated PreviewHost model.
   Success signal: CLI/MCP can report session created, reloaded, reload failed, and closed events, and validation confirms the release gate still cleans and packages deterministic artifacts.
8. `RG-0.6.0-8 Guarded Release`: ship only after lifecycle, runtime input/state, launch/session, region assertion, cleanup, performance, and release validation pass.

### v0.6.0 Milestone Map

- `R0.6.0-M1 Preview Session Lifecycle Observability`; Status: `Done`.
- `R0.6.0-M2 Incremental Reload Boundary Documentation`; Status: `Done`.
- `R0.6.0-M3 Runtime Input Expansion`; Status: `Done`.
- `R0.6.0-M4 Runtime State Inspection`; Status: `Done`.
- `R0.6.0-M5 Runtime Session Selection And Launch Helper`; Status: `Done`.
- `R0.6.0-M6 Screenshot Region Assertions`; Status: `Done`.
- `R0.6.0-M7 Session Events And Cleanup Validation`; Status: `Done`.
- `R0.6.0-M8 Release Documentation And Ticket Closure`; Status: `Done`.
- `R0.6.0-M9 Release Candidate And Version Bump`; Status: `Done`.

### v0.6.0 Acceptance Criteria

- Runtime input supports targeted selectable-control selection and deterministic `ScrollViewer` offset adjustment through CLI, MCP, and bridge contracts.
- Runtime `inspect_node` can report bounded scroll metrics, binding/DataContext state, and app-provided debug state from the explicit bridge opt-in contract.
- Latest-session attach excludes stale manifests, fails on equivalent newest candidates, and preserves explicit session, process, process-name, and manifest targeting.
- The launch helper starts an explicitly bridge-enabled local process, captures stdout/stderr, waits for the matching session manifest, and returns session/top-level/process details or structured timeout errors.
- Screenshot region assertions support non-empty, mostly blank, changed, and unchanged checks with optional crop artifacts.
- Preview-session summaries expose bounded lifecycle events without claiming that one-shot PreviewHost renders are long-lived persistent processes.
- Public CLI/MCP/protocol changes remain additive and local-only, with targeted tests plus the full release gate passing before the version bump.

### v0.6.0 Implementation Validation

- `2026-06-10`: `dotnet build AvaScope.slnx --no-restore -v:minimal` passed with 0 warnings and 0 errors after runtime input/state, latest attach, launch helper, screenshot-region assertion, and preview-session event implementation.
- `2026-06-10`: Targeted `v0.6.0` tests passed with 11 tests covering protocol runtime state/region contracts, screenshot region assertions, expanded bridge input/state inspection, CLI select/scroll/region/launch-helper behavior, and latest active bridge manifest selection.
- `2026-06-10`: Targeted preview-session lifecycle tests passed with 3 tests covering preview-session create/reload/close events and lifecycle serialization.
- `2026-06-10`: `dotnet test AvaScope.slnx --no-build` passed with 242 tests on the Debug build.
- `2026-06-10`: `powershell -NoProfile -ExecutionPolicy Bypass -File .\eng\validate-bug-reports.ps1` passed; 22 intake files scanned.
- `2026-06-10`: `git diff --check` passed with only line-ending normalization warnings.
- `2026-06-10`: Initial Release gate validation found a Windows file-lock in the new screenshot-region test cleanup; `ScreenshotRegionAsserter` was updated to decode images from streams and dispose crop images explicitly, and the cleanup retry was strengthened.
- `2026-06-10`: `dotnet test AvaScope.slnx -c Release --filter FullyQualifiedName~ScreenshotRegionAsserterTests` passed with 3 tests after the file-handle fix.
- `2026-06-10`: `dotnet test AvaScope.slnx --no-build` passed again with 242 tests after the final screenshot-region file-handle fix.
- `2026-06-10`: `powershell -NoProfile -ExecutionPolicy Bypass -File .\eng\create-local-release.ps1` passed for `v0.6.0` after stopping stale packaged CLI/MCP processes from the local artifact output; Release build/test passed with 242 tests, three `0.6.0` packages, win-x64 and linux-x64 framework-dependent ZIPs, release manifest, packaged doctor smoke, and packaged sample preview smoke.
- `2026-06-10`: `powershell -NoProfile -ExecutionPolicy Bypass -File .\eng\publish-github-release.ps1 -Tag v0.6.0 -DryRun` passed for `v0.6.0` assets.
- `2026-06-10`: `powershell -NoProfile -ExecutionPolicy Bypass -File .\eng\validate-release-commit.ps1 -Version 0.6.0 -CommitSubject "Release 0.6.0" -RequiredState "Release Candidate"` passed for the `v0.6.0` release commit guard.
- `2026-06-10`: GitHub Release workflow `27285679633` passed for `Release 0.6.0`; tag `v0.6.0` and six GitHub Release assets were published at `2026-06-10T15:11:44Z`.
- `2026-06-10`: `gh release view v0.6.0` confirmed the public release URL and six uploaded assets: three `0.6.0` `.nupkg` files, win-x64 and linux-x64 framework-dependent ZIPs, and `release-manifest.json`.
- `2026-06-10`: `git ls-remote --tags origin refs/tags/v0.6.0` confirmed tag `v0.6.0` at release commit `4af5284`.
- `2026-06-10`: GitHub CI workflow `27285678995` failed after publish in `CliSmokeTests.WatchPreviewSessionCommandReloadsWhenWatchedFileChanges` because the hosted runner missed the single watched-file event before the command timed out.
- `2026-06-10`: Post-release CI stabilization commit `434f9dd` generated repeated watched-file writes and increased the watch smoke timeout/settle window; targeted Release smoke passed 4 consecutive local runs, local `dotnet test AvaScope.slnx -c Release --no-build` passed with 242 tests, GitHub CI workflow `27286438229` passed, and follow-up Release workflow `27286438001` no-oped successfully because `v0.6.0` already existed.
- `2026-06-10`: GitHub CI workflow `27287102689` failed on the `Record v0.6.0 release completion` documentation commit because watcher-smoke temp directory cleanup hit a transient hosted Windows file lock on `MainView.axaml`.
- `2026-06-10`: Post-release CI cleanup stabilization commit `b35def7` added retrying watcher-smoke temp directory cleanup; targeted Release watcher smoke passed locally, local `dotnet test AvaScope.slnx -c Release --no-build` passed with 242 tests, GitHub CI workflow `27287676017` passed, and follow-up Release workflow `27287675726` no-oped successfully because `v0.6.0` already existed.
- `2026-06-10`: GitHub CI workflow `27288017355` failed on the `Record v0.6.0 CI stabilization` documentation commit in `LocalBridgeClientTests.DiagnosticsReportsDuplicateAndIncompatibleBridgeManifests` because the fake incompatible-bridge named-pipe helper used a 100 ms timeout that was too tight for the hosted runner.
- `2026-06-10`: GitHub CI workflow `27288551198` failed on the `Stabilize bridge diagnostics smoke timeout` commit because the fake named-pipe helper still treated an empty/non-JSON probe connection as a JSON bridge request.
- `2026-06-10`: Post-release fake-pipe stabilization commit `bdf5f8a` made the helper skip empty/non-JSON probe connections; targeted Release diagnostics test passed 4 consecutive local runs, local `dotnet test AvaScope.slnx -c Release --no-build` passed with 242 tests, GitHub CI workflow `27289102252` passed, and follow-up Release workflow `27289102340` no-oped successfully because `v0.6.0` already existed.
- `2026-06-10`: GitHub CI workflow `27289598895` failed on the `Record v0.6.0 final CI stabilization` documentation commit in `CliSmokeTests.ListTopLevelsCommandReadsTopLevelsThroughBridgePipe` because the fake CLI bridge server timed out waiting for an IPC request on the hosted runner.
- `2026-06-10`: Post-release CLI bridge smoke stabilization isolated CLI test bridge manifests into a per-test-process directory and skipped empty/non-JSON pipe probe connections; the failing Release test passed 4 consecutive local runs, and local `dotnet test AvaScope.slnx -c Release --no-build` passed with 242 tests.

### v0.6.0 Explicit Deferrals

- Runtime app hot reload remains separate from PreviewHost hot preview.
- Process injection and no-code attach remain out of scope; the launch helper is limited to explicitly bridge-enabled local apps.
- Full long-lived persistent PreviewHost worker processes remain deferred. `v0.6.0` ships bounded lifecycle/event observability over the existing isolated one-shot PreviewHost child-process model.
- Persistent hosts must stay child processes when implemented later; MCP server in-process user-code loading remains out of scope.
- Destructive runtime actions, arbitrary process termination, and remote inspection remain out of scope.
- Full visual-regression suite/report productization remains in `v0.8.0`; `v0.6.0` only adds focused screenshot region assertions.

## Released Target: v0.7.0

- Release: `v0.7.0`
- Target Version: `0.7.0`
- Release State: `Released`
- Scope Lock: locked
- Release Commit: `d944e1e` (`Release 0.7.0`)
- Local Release Gate: passed `2026-06-12`
- Published At: `2026-06-12T21:24:17Z`
- GitHub Release: https://github.com/RolandUI/AvaScope/releases/tag/v0.7.0
- Previous Release: `v0.6.0`

### v0.7.0 Release Goals

The `v0.7.0` release target starts the agent-first product direction. The goal is to let an agent attach to a local Avalonia app, inspect the UI, apply reversible runtime changes, capture evidence, and hand off a bounded change log without relying on unstructured screen reading.

1. `RG-0.7.0-1 Runtime Mutation Contract`: define structured protocol, CLI, and MCP shapes for temporary runtime UI changes on selected nodes.
   Success signal: agents can request bounded property/class/resource changes with stable target context, validation diagnostics, mutation ids, and explicit unsupported-property results.
2. `RG-0.7.0-2 Reversible Style And Layout Changes`: implement the first safe mutation set for common UI iteration.
   Success signal: width, height, min/max size, margin, padding, opacity, text, background, foreground, classes, and selected resource overrides can be applied and reset without persisting source changes.
3. `RG-0.7.0-3 Mutation Evidence Loop`: make every runtime change observable through screenshots, visual tree snapshots, and optional baseline/diff checks.
   Success signal: CLI/MCP responses can return before/after artifact paths, changed-node summaries, diagnostics, and failure reasons in bounded structured data.
4. `RG-0.7.0-4 Agent Session Safety`: keep runtime mutation opt-in, local-only, reversible, and auditable.
   Success signal: bridge activation remains explicit, mutation capabilities are discoverable, mutations are tracked per session/top-level/node, and reset/close cleanup is deterministic.
5. `RG-0.7.0-5 Runtime Experiment Review Surface`: give agents a concise way to review what changed and decide the next action.
   Success signal: CLI/MCP can list mutation history, inspect the active runtime overrides, reset one mutation or all mutations, and produce a local review artifact with screenshots and structured details.
6. `RG-0.7.0-6 Guarded Release`: ship only after runtime mutation tests, bridge safety validation, evidence artifact validation, documentation updates, and release dry-runs pass.

### v0.7.0 Milestone Map

- `R0.7.0-M1 Runtime Mutation Contract`; Status: `Done`.
- `R0.7.0-M2 Style And Layout Mutation Set`; Status: `Done`.
- `R0.7.0-M3 Mutation Evidence And Screenshot Loop`; Status: `Done`.
- `R0.7.0-M4 Agent Session Safety And Reset Semantics`; Status: `Done`.
- `R0.7.0-M5 CLI/MCP Runtime Experiment Review`; Status: `Done`.
- `R0.7.0-M6 Release Candidate And Version Bump`; Status: `Done`.

### v0.7.0 Implementation Validation

- `2026-06-12`: Release-candidate validation passed for `v0.7.0` with `dotnet build AvaScope.slnx --no-restore -v:minimal`, targeted `launch-app` lifecycle smoke tests, and full Debug tests (`dotnet test AvaScope.slnx --no-build`, 264 passed).
- `2026-06-12`: `powershell -NoProfile -ExecutionPolicy Bypass -File .\eng\create-local-release.ps1` passed for `v0.7.0`; Release build/test passed with 264 tests, three `0.7.0` packages, win-x64 and linux-x64 framework-dependent ZIPs, release manifest, packaged doctor smoke, and packaged sample preview smoke.
- `2026-06-12`: `powershell -NoProfile -ExecutionPolicy Bypass -File .\eng\publish-github-release.ps1 -Tag v0.7.0 -DryRun` passed for the generated `0.7.0` assets.
- `2026-06-12`: Packaged Windows CLI runtime smoke passed against `samples\AvaScope.GettingStartedApp` using `launch-app`, `attach`, `list-top-levels`, `visual-tree`, `find-nodes`, `mutate-node`, `screenshot`, `mutate-node-evidence`, `mutation-review`, `reset_all`, and `close-session`.
- `2026-06-12`: Hosted-runner watcher smoke stabilization passed in Release configuration 3 consecutive times after increasing the watcher settle window to avoid reloading while the changed AXAML file is transiently locked.
- `2026-06-12`: `git diff --check` passed with only line-ending normalization warnings.
- `2026-06-12`: GitHub Release workflow `27443577851` passed for `Release 0.7.0`; tag `v0.7.0` and six GitHub Release assets were published at `2026-06-12T21:24:17Z`.
- `2026-06-12`: `gh release view v0.7.0` confirmed the public release URL and six uploaded assets: three `0.7.0` `.nupkg` files, win-x64 and linux-x64 framework-dependent ZIPs, and `release-manifest.json`.
- `2026-06-12`: `git ls-remote --tags origin refs/tags/v0.7.0` confirmed tag `v0.7.0` at release commit `d944e1e`.
- `2026-06-12`: GitHub CI workflow `27443577826` failed after publish in `LocalBridgeClientTests.DiagnosticsReportsDuplicateAndIncompatibleBridgeManifests` because the fake incompatible-bridge named-pipe test used a 100 ms timeout, and in `BridgeHeadlessSmokeTests.ScreenshotCaptureForMissingTopLevelReturnsStructuredError` because the headless session disposed from a no-window path on the hosted runner.
- `2026-06-12`: Post-release CI stabilization removed the artificial 100 ms pipe timeout from the incompatible-bridge diagnostics test and initialized the missing-top-level screenshot smoke with a minimal headless window. Local validation passed with `dotnet build AvaScope.slnx -c Release --no-restore -v:minimal`, the two targeted failing tests, and `dotnet test AvaScope.slnx -c Release --no-build` with 264 tests.
- `2026-06-12`: Post-release CI stabilization commit `07367e2` passed GitHub CI workflow `27444154586`; Restore, Build, Test, Pack libraries, Package executables, and Verify artifacts all succeeded. Follow-up Release workflow `27444154583` no-oped successfully because `v0.7.0` already existed.

### v0.7.0 Explicit Deferrals

- Persisting runtime changes back to source files remains out of scope; this release records what changed and may produce advisory handoff text only.
- Destructive runtime actions, remote control, process injection, and private runtime hooks remain out of scope.
- Broad arbitrary-property editing remains out of scope until property conversion, validation, rollback, and security behavior are proven for the first mutation set.
- Cloud dashboard hosting remains out of scope.

## In Progress Target: v0.8.0

- Release: `v0.8.0`
- Target Version: `0.8.0`
- Release State: `In Progress`
- Scope Lock: `2026-06-12`
- Release Commit: pending
- Local Release Gate: pending
- Published At: pending
- GitHub Release: pending
- Previous Release: `v0.7.0`

### v0.8.0 Release Goals

The `v0.8.0` release target turns agent experiments into repeatable validation workflows. The goal is to let agents run preview/runtime checks, compare visual outcomes, and produce reviewable artifacts for local and pull-request validation.

1. `RG-0.8.0-1 Baseline Collections`: support named baseline suites with multiple projects, views, profiles, sizes, themes, cultures, runtime mutation presets, and animation frames.
   Success signal: one command can create/check a suite and produce stable per-variant artifacts.
2. `RG-0.8.0-2 Thresholds, Masks, And Regions`: add practical comparison controls for real UI variance.
   Success signal: users can configure tolerance, ignored regions, required regions, and per-variant thresholds without editing generated reports.
3. `RG-0.8.0-3 Agent Evidence Reports`: produce uploadable HTML, JSON, JUnit, and optional SARIF-style summaries for preview, runtime, mutation, and baseline checks.
   Success signal: a local or CI run can expose current images, diffs, mutation history, grouped failures, and machine-readable status from a single artifact directory.
4. `RG-0.8.0-4 GitHub Actions Workflow`: provide a documented and validated GitHub Actions example for agent validation checks.
   Success signal: the repo sample can run baseline validation and upload artifacts in CI without publishing credentials.
5. `RG-0.8.0-5 MCP/CLI Agent Review Surface`: make validation output easy for MCP clients and CLI users to summarize.
   Success signal: responses include bounded failure summaries, mutation summaries, report paths, and preview URLs for local review.
6. `RG-0.8.0-6 Guarded Feature Release`: ship only after suite tests, report rendering validation, CI example validation, review-surface validation, and release dry-runs pass.

### v0.8.0 Milestone Map

- `R0.8.0-M1 Baseline Suite Manifest`; Status: `Done`.
- `R0.8.0-M2 Thresholds, Masks, And Region Rules`; Status: `Done`.
- `R0.8.0-M3 Agent Evidence Report Pack`; Status: `Done`.
- `R0.8.0-M4 GitHub Actions Example And Artifact Upload`; Status: `Done`.
- `R0.8.0-M5 MCP/CLI Agent Review Surface`; Status: `Review`.
- `R0.8.0-M6 Release Candidate And Version Bump`; Status: `Planned`.

### v0.8.0 Explicit Deferrals

- Automatic baseline approval or mutation from CI remains out of scope unless explicitly gated by user action.
- Pixel-perfect guarantees across every OS/font stack remain out of scope; reports must expose environment metadata instead.
- Native editor plugins remain optional; this release improves artifact/viewer handoff but does not require shipped IDE extensions.
- Remote multi-user inspection remains out of scope.

## Planned Target: v0.9.0

- Release: `v0.9.0`
- Target Version: `0.9.0`
- Release State: `Planned`
- Scope Lock: pending
- Release Commit: pending
- Local Release Gate: pending
- Published At: pending
- GitHub Release: pending
- Previous Release: `v0.8.0`

### v0.9.0 Release Goals

The `v0.9.0` release target is beta hardening for the agent control plane. The goal is to make runtime changes, validation artifacts, and source-level guidance dependable enough for the `v1.0.0` API and workflow freeze.

1. `RG-0.9.0-1 Source-Aware Change Suggestions`: derive conservative source-level guidance from runtime mutations, diagnostics, and project metadata.
   Success signal: reports can suggest likely XAML/style/resource locations, confidence, and manual patch guidance without mutating source files automatically.
2. `RG-0.9.0-2 Accessibility, Validation, And Component Inventory`: inspect accessible names, automation ids, focus order, validation states, controls, styles, classes, resources, templates, theme variants, and repeated patterns where public Avalonia APIs make them reliable.
   Success signal: CLI/MCP can produce bounded audit and inventory reports with affected node context, severity, provenance, and suggested next action.
3. `RG-0.9.0-3 Protocol Versioning And Capability Negotiation`: make schema compatibility explicit across Protocol, Core, CLI, MCP, bridge, and PreviewHost.
   Success signal: clients can query protocol/tool capabilities and handle additive fields without guessing package versions.
4. `RG-0.9.0-4 Security, Safety, And Compatibility Audit`: review runtime bridge activation, mutation permissions, local IPC, file outputs, project-code execution, logs, package surfaces, command names, SemVer behavior, and old/new client compatibility.
   Success signal: threat model docs exist, local-only guarantees are tested, unsafe defaults are rejected, production bridge activation remains explicit, and compatibility risk is recorded.
5. `RG-0.9.0-5 Performance, Stress, And Sample Audit`: run larger app/tree/preview/runtime-mutation/baseline scenarios with explicit budgets and finalize sample coverage.
   Success signal: tests or validation scripts cover large visual trees, large diagnostics, repeated previews, repeated mutation/reset cycles, persistent sessions, and baseline suites.
6. `RG-0.9.0-6 Guarded Beta Release`: ship only after beta audit validation, full release dry-runs, and all P0/P1 issues are fixed or explicitly accepted as non-blocking.

### v0.9.0 Milestone Map

- `R0.9.0-M1 Source-Aware Change Suggestions`; Status: `Planned`.
- `R0.9.0-M2 Accessibility, Validation, And Component Inventory`; Status: `Planned`.
- `R0.9.0-M3 Protocol Capability And Versioning Contract`; Status: `Planned`.
- `R0.9.0-M4 Security, Safety, And Compatibility Audit`; Status: `Planned`.
- `R0.9.0-M5 Performance, Stress, Samples, And Troubleshooting Audit`; Status: `Planned`.
- `R0.9.0-M6 Release Candidate And Version Bump`; Status: `Planned`.

### v0.9.0 Explicit Deferrals

- Automatic source editing remains out of scope; suggested fixes are advisory unless a later release adds an explicit guarded patch workflow.
- Any new product capability not required for 1.0 stability should move to post-1.0 unless it blocks the readiness definition.
- Broad native IDE plugin implementation remains post-1.0 unless already validated through contracts and small adapters.
- Remote/network inspection remains out of scope.

## Planned Target: v1.0.0

- Release: `v1.0.0`
- Target Version: `1.0.0`
- Release State: `Planned`
- Scope Lock: pending
- Release Commit: pending
- Local Release Gate: pending
- Published At: pending
- GitHub Release: pending
- Previous Release: `v0.9.0`

### v1.0.0 Release Goals

The `v1.0.0` release target is the stable public release. The goal is not to add broad new features; it is to freeze and verify the workflows that make AvaScope dependable.

1. `RG-1.0.0-1 Stable Surface Freeze`: freeze public packages, protocol DTOs, CLI commands, MCP tools, exit codes, artifact naming, and release workflow behavior.
   Success signal: compatibility rules and migration guidance exist, and stable surfaces are covered by contract tests.
2. `RG-1.0.0-2 End-To-End Workflow Verification`: validate runtime, preview, animation, live preview, diagnostics, baseline, CLI, MCP, and packaged workflows end-to-end.
   Success signal: source and packaged validation commands pass on the supported platform matrix with no P0/P1 failures.
3. `RG-1.0.0-3 Documentation Complete`: publish complete installation, getting-started, CLI, MCP, bridge activation, preview, visual-regression, troubleshooting, security, and release documentation.
   Success signal: a new user can install AvaScope, preview a sample, attach to a bridge-enabled app, run diagnostics, and configure baseline checks from docs alone.
4. `RG-1.0.0-4 Release Artifact Verification`: finalize reproducible package and executable outputs.
   Success signal: release artifacts have manifest hashes, package metadata, version alignment, NuGet/GitHub Release publication, and packaged smoke validation.
5. `RG-1.0.0-5 Post-1.0 Backlog Definition`: explicitly separate stable scope from future work.
   Success signal: deferred features are recorded as post-1.0 backlog with reason, not as hidden blockers.
6. `RG-1.0.0-6 Stable Release Publication`: publish `v1.0.0` only after all readiness gates pass and release metadata is recorded.

### v1.0.0 Milestone Map

- `R1.0.0-M1 Stable Surface Freeze`; Status: `Planned`.
- `R1.0.0-M2 End-To-End Workflow Verification`; Status: `Planned`.
- `R1.0.0-M3 Documentation Completion`; Status: `Planned`.
- `R1.0.0-M4 Release Artifact And Package Verification`; Status: `Planned`.
- `R1.0.0-M5 Post-1.0 Backlog And Deferral Audit`; Status: `Planned`.
- `R1.0.0-M6 Stable Release Commit And Publication`; Status: `Planned`.

### v1.0.0 Explicit Deferrals

- Remote inspection/control, no-code attach, process injection, CLR profiling, and private Avalonia designer APIs remain post-1.0 unless a separate security model is designed.
- Native IDE extensions can build on the stable CLI/MCP contracts after 1.0; they are not required for the stable release.
- Cloud-hosted visual regression dashboards remain post-1.0.
- Destructive runtime actions remain out of scope for the stable tool set.

## Released Target: v0.3.0

- Release: `v0.3.0`
- Target Version: `0.3.0`
- Release State: `Released`
- Scope Lock: `2026-06-09`
- Release Commit: `9d6cc3f` (`Release 0.3.0`)
- Local Release Gate: passed on `2026-06-09`
- Published At: `2026-06-09T15:50:04Z`
- GitHub Release: https://github.com/RolandUI/AvaScope/releases/tag/v0.3.0
- Previous Release: `v0.2.2`

### v0.3.0 Release Goals

The `v0.3.0` release target is a minor release focused on deterministic animation diagnostics for agents and developers. AvaScope should expose time-sampled frames, bounded artifacts, and structured diagnostics that can be consumed through CLI and MCP.

1. `RG-0.3.0-1 Animation Sampling Contract`: define additive protocol models and tool shapes for explicit animation time-offset sampling.
   Success signal: CLI/MCP/Core can represent a request such as `0ms`, `150ms`, `300ms`, output frame paths, optional strip/contact-sheet paths, and bounded diagnostics without changing existing screenshot or preview response compatibility.
2. `RG-0.3.0-2 PreviewHost Time-Offset Frame Capture`: PreviewHost can render a view at requested animation offsets in isolated child-process mode.
   Success signal: a sample animated view produces deterministic per-offset PNG frames while preserving size, theme, DPI, culture, profile, design-data, and one-shot isolation semantics.
3. `RG-0.3.0-3 Motion Diagnostics`: AvaScope reports agent-readable motion summaries and advisory issues derived from sampled frames and public Avalonia state where reliable.
   Success signal: results can report moving nodes/properties where known, pixel/bounds deltas, final-state stability, clipping during motion, disappearing content, and explicit `unknown`/`not_available` provenance when metadata cannot be trusted.
4. `RG-0.3.0-4 Agent Workflow Surface`: CLI, MCP, and file-backed viewer workflows make animation sampling usable from Codex and other MCP clients.
   Success signal: users can request animation samples from the CLI and MCP, receive structured JSON plus artifact paths, and open a local viewer showing the sampled timeline or strip.
5. `RG-0.3.0-5 Sample And Documentation`: the getting-started sample and docs include a small animation scenario and validated commands.
   Success signal: sample docs show preview animation sampling, diagnostics interpretation, generated artifacts, and explicit limitations.
6. `RG-0.3.0-6 Guarded Release`: `v0.3.0` ships only after the declared goals are complete or explicitly deferred.
   Success signal: targeted tests, full build/test validation, release dry-run validation, packaged workflow smoke checks, and a `Release 0.3.0` commit complete before publishing.

### v0.3.0 Milestone Map

- `R0.3.0-M1 Animation Sampling Contract` delivers `RG-0.3.0-1`; Status: `Done`.
- `R0.3.0-M2 PreviewHost Time-Offset Frame Capture` delivers `RG-0.3.0-2`; Status: `Done`.
- `R0.3.0-M3 Motion Diagnostics` delivers `RG-0.3.0-3`; Status: `Done`.
- `R0.3.0-M4 CLI, MCP, And Viewer Workflow` delivers `RG-0.3.0-4`; Status: `Done`.
- `R0.3.0-M5 Sample And Documentation` delivers `RG-0.3.0-5`; Status: `Done`.
- `R0.3.0-M6 Release Candidate And Version Bump` delivers `RG-0.3.0-6`; Status: `Done`.

### v0.3.0 Implementation Validation

- `2026-06-09`: `dotnet build AvaScope.slnx --no-restore -v:minimal` passed with 0 warnings and 0 errors after animation sampling, viewer, sample, and documentation implementation.
- `2026-06-09`: Animation targeted tests passed with 5 tests covering protocol serialization, Core frame/strip/viewer output, CLI `preview-animation`, MCP tool listing, and MCP invalid-offset validation.
- `2026-06-09`: Source CLI `preview-animation .\samples\AvaScope.GettingStartedApp\AvaScope.GettingStartedApp.csproj --profile animation` passed with 4 successful frames, a frame strip, `motion.status=changed`, and a file-backed animation viewer URL.
- `2026-06-09`: `dotnet build AvaScope.slnx` passed with 0 warnings and 0 errors on the `0.3.0` release-candidate working tree.
- `2026-06-09`: `dotnet test AvaScope.slnx --no-build` passed with 218 tests on the `0.3.0` release-candidate working tree.
- `2026-06-09`: `powershell -NoProfile -ExecutionPolicy Bypass -File .\eng\create-local-release.ps1` passed for `v0.3.0` after stopping stale local artifact-hosted processes; Release build/test passed with 218 tests, three `0.3.0` packages, win-x64 and linux-x64 framework-dependent ZIPs, release manifest, packaged doctor smoke, and packaged sample preview smoke.
- `2026-06-09`: `powershell -NoProfile -ExecutionPolicy Bypass -File .\eng\publish-github-release.ps1 -Tag v0.3.0 -DryRun` passed for `v0.3.0` assets.
- `2026-06-09`: Packaged Windows CLI `preview-animation .\samples\AvaScope.GettingStartedApp\AvaScope.GettingStartedApp.csproj --profile animation` passed with 4 successful frames, a frame strip, `motion.status=changed`, `animation_frame_reused` for the repeated final offset, and a file-backed animation viewer URL.
- `2026-06-09`: `powershell -NoProfile -ExecutionPolicy Bypass -File .\eng\validate-bug-reports.ps1` passed for the `v0.3.0` release-candidate gate; 15 intake files scanned.
- `2026-06-09`: `git diff --check` passed for the `v0.3.0` release-candidate working tree.
- `2026-06-09`: `powershell -NoProfile -ExecutionPolicy Bypass -File .\eng\validate-release-commit.ps1 -Version 0.3.0 -CommitSubject "Release 0.3.0" -RequiredState "Release Candidate"` passed for the `v0.3.0` release commit guard.
- `2026-06-09`: GitHub Release workflow `27217885931` passed for `Release 0.3.0`; tag `v0.3.0` and six GitHub Release assets were published at `2026-06-09T15:50:04Z`.
- `2026-06-09`: `gh release view v0.3.0` confirmed the public release URL and six uploaded assets: three `0.3.0` `.nupkg` files, win-x64 and linux-x64 framework-dependent ZIPs, and `release-manifest.json`.
- `2026-06-09`: `git ls-remote --tags origin refs/tags/v0.3.0` confirmed tag `v0.3.0` at release commit `9d6cc3f`.
- `2026-06-09`: GitHub CI workflow `27217886778` failed after publish in `PreviewSessionWatcherSkipsReloadWhenWatchedInputsAreUnchanged` because the hosted runner timed out during the watcher settle delay after receiving a transient file event.
- `2026-06-09`: Post-release CI stabilization commit `9d475d2` increased the unchanged-input watcher test timeout; targeted Release test passed 3 consecutive runs, local `dotnet test AvaScope.slnx -c Release --no-build` passed with 218 tests, GitHub CI workflow `27218629376` passed, and the follow-up Release workflow `27218629389` no-oped successfully because `v0.3.0` already existed.

### Explicit Deferrals

- Continuous live animation designer playback remains out of scope.
- Persistent preview host processes remain out of scope unless separately required and designed with close, TTL, crash, and cleanup semantics.
- Private Avalonia runtime hooks, CLR injection, and designer-private APIs remain out of scope.
- Remote runtime inspection remains out of scope; bridge transport stays opt-in and local-only.
- Animation metadata that cannot be obtained through reliable public APIs must be reported as `unknown` or `not_available`.
- Strict manual animation clock injection remains out of scope because Avalonia 12 `IClock`/`Clock` are not public API; `v0.3.0` uses public headless render timer ticks and stable repeated-offset artifact reuse.

## Released Target: v0.2.2

- Release: `v0.2.2`
- Target Version: `0.2.2`
- Release State: `Released`
- Scope Lock: `2026-06-09`
- Release Commit: `eac2bf1` (`Release 0.2.2`)
- Local Release Gate: passed on `2026-06-09`
- Published At: `2026-06-09T12:17:17Z`
- GitHub Release: https://github.com/RolandUI/AvaScope/releases/tag/v0.2.2
- Previous Release: `v0.2.1`

### v0.2.2 Release Goals

The `v0.2.2` release target is a patch release focused on reducing PreviewHost diagnostic false positives reported in `BUG-0003`.

1. `RG-0.2.2-1 DataTemplate Binding Diagnostic Scope`: binding diagnostics under `DataTemplate` use the template item context when `x:DataType` is available instead of warning against the root preview `DataContext`.
   Success signal: `ItemsControl.ItemTemplate` bindings to item properties no longer emit root-context `binding_path_not_found` warnings, including templates with `x:CompileBindings="False"`.
2. `RG-0.2.2-2 Template-Aware Layout Diagnostic Noise Reduction`: layout diagnostics avoid noisy warnings for Avalonia layer/template internals and small font metric differences.
   Success signal: PreviewHost does not report full-window root layer overlaps, icon/control-template internal overlaps, tab-header metric-only clipping, or slider internal `RepeatButton` hit-target warnings in targeted smoke coverage.
3. `RG-0.2.2-3 Guarded Patch Release`: `v0.2.2` ships only after BUG-0003 is fixed and the release gate passes.
   Success signal: targeted PreviewHost tests, full build/test validation, release dry-run validation, and a `Release 0.2.2` commit complete before publishing.

### v0.2.2 Milestone Map

- `R0.2.2-M1 DataTemplate Binding Diagnostics` delivers `RG-0.2.2-1`; Status: `Done`.
- `R0.2.2-M2 Template-Aware Layout Diagnostics` delivers `RG-0.2.2-2`; Status: `Done`.
- `R0.2.2-M3 Release Candidate And Version Bump` delivers `RG-0.2.2-3`; Status: `Done`.

### v0.2.2 Implementation Validation

- `2026-06-09`: `dotnet build tests/AvaScope.Tests/AvaScope.Tests.csproj --no-restore -v:minimal` passed with 0 warnings and 0 errors after BUG-0003 implementation.
- `2026-06-09`: Targeted PreviewHost diagnostic tests passed with 4 tests: `PreviewHostUsesDataTemplateDataTypeForBindingDiagnostics`, `PreviewHostSuppressesFluentTemplateLayoutNoise`, `PreviewHostReturnsDataTypeBindingPathDiagnostics`, and `PreviewHostReturnsBindingResourceAndLayoutDiagnostics`.
- `2026-06-09`: `dotnet test tests/AvaScope.Tests/AvaScope.Tests.csproj --no-build` passed with 214 tests.
- `2026-06-09`: `dotnet build AvaScope.slnx` passed with 0 warnings and 0 errors for `v0.2.2` release-candidate validation.
- `2026-06-09`: `dotnet test AvaScope.slnx --no-build` passed with 214 tests for `v0.2.2` release-candidate validation.
- `2026-06-09`: `powershell -NoProfile -ExecutionPolicy Bypass -File .\eng\create-local-release.ps1` passed for `v0.2.2`; Release build/test passed with 214 tests, three `0.2.2` packages, win-x64 and linux-x64 framework-dependent ZIPs, release manifest, packaged doctor smoke, and packaged sample preview smoke.
- `2026-06-09`: `powershell -NoProfile -ExecutionPolicy Bypass -File .\eng\publish-github-release.ps1 -Tag v0.2.2 -DryRun` passed for `v0.2.2` assets.
- `2026-06-09`: `powershell -NoProfile -ExecutionPolicy Bypass -File .\eng\validate-release-commit.ps1 -Version 0.2.2 -CommitSubject "Release 0.2.2" -RequiredState "Release Candidate"` passed.
- `2026-06-09`: `git diff --check` passed for `v0.2.2` release-candidate validation.
- `2026-06-09`: GitHub Release workflow `27205089688` passed for `Release 0.2.2`; tag `v0.2.2` and six GitHub Release assets were published at `2026-06-09T12:17:17Z`.
- `2026-06-09`: GitHub CI workflow `27205089675` passed for `Release 0.2.2`.
- `2026-06-09`: `gh release view v0.2.2` confirmed the public release URL and six uploaded assets.
- `2026-06-09`: `git ls-remote --tags origin refs/tags/v0.2.2` confirmed tag `v0.2.2` at release commit `eac2bf1`.

### Explicit Deferrals

- Pixel-perfect visual clipping analysis remains out of scope; this patch uses bounded tolerances and template-aware filtering.
- Broad Avalonia private runtime hooks remain out of scope.
- No new remote inspection or control surface is introduced.

## Released Target: v0.2.1

- Release: `v0.2.1`
- Target Version: `0.2.1`
- Release State: `Released`
- Scope Lock: `2026-06-09`
- Release Commit: `d12fe8c` (`Release 0.2.1`)
- Local Release Gate: passed on `2026-06-09`
- Published At: `2026-06-09T10:48:21Z`
- GitHub Release: https://github.com/RolandUI/AvaScope/releases/tag/v0.2.1
- Previous Release: `v0.2.0`

### v0.2.1 Release Goals

The `v0.2.1` release target is a patch release focused on preview theme parity for controls rendered inside the isolated PreviewHost wrapper window.

1. `RG-0.2.1-1 Theme-Aware Preview Wrapper Background`: non-`Window` previews inherit a theme-appropriate host background instead of forcing a white canvas.
   Success signal: a dark preview request for a root control without its own background renders against a dark/theme-derived background, while explicit project window styles still win.
2. `RG-0.2.1-2 Guarded Patch Release`: `v0.2.1` ships only after the targeted fix is complete and the release gate passes.
   Success signal: targeted preview-host tests, full build/test validation, release dry-run validation, and a `Release 0.2.1` commit complete before publishing.

### v0.2.1 Milestone Map

- `R0.2.1-M1 Theme-Aware Preview Wrapper Background` delivers `RG-0.2.1-1`; Status: `Done`.
- `R0.2.1-M2 Release Candidate And Version Bump` delivers `RG-0.2.1-2`; Status: `Done`.

### Explicit Deferrals

- Persistent preview host processes remain out of scope for this patch release.
- Broader design-time startup/lifetime execution remains out of scope.
- No new remote inspection or control surface is introduced.

## Released Target: v0.2.0

- Release: `v0.2.0`
- Target Version: `0.2.0`
- Release State: `Released`
- Scope Lock: `2026-06-09`
- Release Commit: `bb471af` (`Release 0.2.0`)
- Local Release Gate: passed on `2026-06-09`
- Published At: `2026-06-09T09:04:15Z`
- GitHub Release: https://github.com/RolandUI/AvaScope/releases/tag/v0.2.0
- Previous Release: `v0.1.0`

### v0.2.0 Release Goals

The `v0.2.0` release target is focused on making AvaScope more reliable for repeated agent workflows against local Avalonia projects while preserving the current local-only safety boundary.

1. `RG-0.2.0-1 Runtime Target Handoff`: a node found through tree or search output can be carried into follow-up runtime commands without guessing which id, tree kind, or top-level context is required.
   Success signal: `find-nodes`, tree, `inspect-node`, `screenshot`, and `input` workflows expose or accept consistent target context, and stale or mismatched references return structured actionable errors.
2. `RG-0.2.0-2 Preview Failure Triage`: preview failures distinguish local environment readiness, project build failures, and render/runtime failures before agents retry commands.
   Success signal: CLI/MCP diagnostics and preview responses report bounded readiness issues for missing SDK/build/host prerequisites where reliable signals are available.
3. `RG-0.2.0-3 Live Preview Lifecycle`: live preview behavior has a concrete next step after unchanged-input skipping.
   Success signal: either a small validated lifecycle improvement ships, or persistent preview host processes are explicitly deferred with close, TTL, crash, and cleanup semantics documented.
4. `RG-0.2.0-4 Visual Regression CI Handoff`: visual baseline checks are ready to be consumed by CI without changing local baseline command behavior.
   Success signal: a documented workflow or helper shows how to upload the JSON report, current image, and diff image artifacts produced by `baseline-check --report`.
   Implementation: `eng/collect-baseline-artifacts.ps1` collects the report plus referenced current and diff images into one upload directory, and `docs/VISUAL_REGRESSION_CI.md` documents the CI upload flow.
5. `RG-0.2.0-5 Codex Preview Surface`: Codex can hand off an AvaScope preview to a local file-backed viewer that works with the Codex in-app browser instead of relying on a native custom sidebar surface.
   Success signal: AvaScope can produce a local file-backed `previewUrl` for a preview/session viewer, MCP/CLI handoff returns that URL, and docs describe the Codex in-app browser workflow.
6. `RG-0.2.0-6 Guarded Release`: `v0.2.0` ships only after the declared goals are complete or explicitly deferred.
   Success signal: audits are refreshed, the full release gate passes, `Directory.Build.props` is bumped to `0.2.0` in a `Release 0.2.0` commit, and the guarded release workflow publishes the matching version.

### v0.2.0 Milestone Map

- `R0.2.0-M1 Runtime Workflow Hardening` delivers `RG-0.2.0-1`.
- `R0.2.0-M2 Preview Diagnostics Readiness` delivers `RG-0.2.0-2`.
- `R0.2.0-M3 Live Preview Lifecycle Decision` delivers `RG-0.2.0-3`.
- `R0.2.0-M4 Visual Regression CI Integration` delivers `RG-0.2.0-4`.
- `R0.2.0-M5 Codex Preview Surface` delivers `RG-0.2.0-5`.
- `R0.2.0-M6 Release Candidate And Version Bump` delivers `RG-0.2.0-6`.

### Explicit Deferrals

- macOS release assets, signing, notarization, and installers remain deferred until a validation surface exists.
- Remote runtime inspection remains out of scope; bridge transport stays opt-in and local-only.
- Private Avalonia runtime hooks, CLR injection, and production remote control remain out of scope.
- Persistent preview host processes are not guaranteed for `v0.2.0` unless their lifecycle and safety semantics are validated first.
