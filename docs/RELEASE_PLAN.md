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

The roadmap below records the release-shaped plan through `v1.0.0`. It is intentionally release-shaped so every implementation slice remains shippable, validated, and reversible.

- `v0.6.0` is released.
- `v0.7.0` is released.
- `v0.8.0` is released.
- `v0.7.0` starts the agent-first product direction: AvaScope becomes an agent control plane for inspecting, changing, validating, and explaining Avalonia UI behavior through structured CLI/MCP workflows.
- `v0.9.0` is released.
- `v1.0.0` is released.
- Each release must preserve the current product boundaries: MCP and CLI stay adapters over Core, runtime bridge activation stays opt-in and local-only, PreviewHost stays isolated from the MCP server, and private Avalonia/runtime hooks remain out of the default path.
- Every release must include targeted tests, full build/test validation, release dry-run validation, documentation updates, and explicit deferrals.
- A release may be split into a patch release if a P0/P1 regression blocks users or CI, but patch scope must remain defect-focused.

## Active Target: v1.1.4

- Release: `v1.1.4`
- Target Version: `1.1.4`
- Release State: `In Progress`
- Scope Lock: `2026-07-29`
- GitHub Milestone: `v1.1.4`
- GitHub Issues: #74, #75
- Previous Release: `v1.1.3`

### v1.1.4 Release Goals

The v1.1.4 patch release restores MCP interoperability with Rider Copilot and other clients that validate structured tool responses strictly against the advertised output schema.

1. `RG-1.1.4-1 Strict MCP Output Compatibility`: serialize the inactive `ToolResult<T>` branch as an explicit null so successful and failed responses both contain every field required by the published schema.
2. `RG-1.1.4-2 Regression Coverage`: validate representative success and failure results against the output schemas returned by the real stdio MCP server.
3. `RG-1.1.4-3 Guarded Patch Release`: publish only after the full local release gate passes and the Release workflow creates `v1.1.4`, packages, portable ZIPs, installers, manifest, and GitHub Release assets.

### v1.1.4 Milestone Map

- #75 `Rider Copilot rejects MCP responses that omit required null result fields`; Status: `Done`.
- #74 `Release v1.1.4`; Status: `In Progress`.

### v1.1.4 Implementation Validation

- `2026-07-29`: Completed #75 in commit `fb15795` after the focused and full Debug validation passed, then started guarded release tracker #74. The release gate covers Debug/Release builds and tests, complete packages/ZIPs/installers and manifest, packaged MCP and sample smoke, Windows/WSL installer validation, publish dry-runs, release-commit guard, and remote publication verification.
- `2026-07-29`: Implemented explicit null serialization for both inactive `ToolResult<T>` branches, including an override of the MCP SDK's global null-omission behavior. Focused protocol and real stdio MCP schema/response tests passed (`59`); representative successful and failed tool calls contained all advertised required fields. The full Debug build passed with `0` warnings/errors and all `373` tests passed.
- `2026-07-29`: Started #75 from Rider Copilot feedback against AvaScope 1.1.3. The defect is scoped to the mismatch between required `value`/`error` output-schema properties and null-omitting JSON serialization; the patch preserves the stable three-field result contract and adds real stdio MCP regression coverage.

## Released Target: v1.1.3

- Release: `v1.1.3`
- Target Version: `1.1.3`
- Release State: `Released`
- Scope Lock: `2026-07-28`
- Release Commit: `3fed6b8b422e8f33efdda35665a2d4f9bebf35d1` (`Release 1.1.3`)
- Local Release Gate: passed on `2026-07-28`
- Published At: `2026-07-28T18:36:23Z`
- GitHub Release: https://github.com/RolandUI/AvaScope/releases/tag/v1.1.3
- GitHub Milestone: `v1.1.3`
- GitHub Issues: #65, #66, #70
- Previous Release: `v1.1.2`

### v1.1.3 Release Goals

The `v1.1.3` patch release resolves the missing public license grant and package provenance metadata discovered before opening the source repository, and adds user-local Windows and Linux installers before publication.

Release execution began on `2026-07-28` after the remaining planned modifications were completed and publication was explicitly approved.

1. `RG-1.1.3-1 Apache-2.0 License Grant`: license AvaScope-authored source and official release artifacts, including versions `0.1.0` and later, under Apache-2.0 while preserving third-party licenses.
2. `RG-1.1.3-2 Verifiable Distribution Metadata`: embed license, repository, project, copyright, scope, and third-party notice data in NuGet and executable artifacts and validate the packed outputs.
3. `RG-1.1.3-3 Windows And Linux Installers`: add non-admin, per-user install, upgrade, and uninstall workflows for Windows and Linux while retaining portable ZIPs; macOS and system-wide Linux packages remain out of scope.
4. `RG-1.1.3-4 Guarded Patch Release`: publish only after the local release gate passes and the Release workflow creates `v1.1.3`, packages, portable executable ZIPs, installer artifacts, manifest, and GitHub Release assets.

### v1.1.3 Milestone Map

- #65 `Add Apache-2.0 licensing and package provenance metadata`; Status: `Done`.
- #70 `Add non-admin Windows and Linux installers`; Status: `Done`.
- #66 `Release v1.1.3`; Status: `Done`.

### v1.1.3 Implementation Validation

- `2026-07-28`: Published v1.1.3 from exact release commit `3fed6b8` through successful Release workflow `30387366119`. The tag resolves to the release commit; nuget.org and GitHub Packages received Protocol, Core, and Bridge `1.1.3`; the GitHub Release exposes three NuGet packages, Windows/Linux portable ZIPs, branded Windows and Linux installers, and the release manifest. The downloaded manifest matched all seven packaged asset SHA-256 digests and sizes, and all three public nuget.org indexes expose `1.1.3`.
- `2026-07-28`: The complete local release gate passed and v1.1.3 moved to `Release Candidate`. Debug and Release builds completed with 0 warnings/errors and `371` passing tests in each configuration; the Release suite passed twice more while diagnosing stale artifact-hosted MCP locks. Exact `1.1.3` NuGet packages, Windows/Linux portable ZIPs, branded `AvaScopeSetup.exe`, the Linux installer, and the SHA-256 manifest passed metadata/legal/provenance verification, packaged doctor, sample preview, Windows installer install/version/doctor/MCP/repair/uninstall, and equivalent WSL Ubuntu Linux installer smoke using a temporary user-local .NET 10 runtime. NuGet and GitHub Release dry-runs passed without publication. The release is ready for the exact `Release 1.1.3` commit and guarded workflow.
- `2026-07-28`: Started the guarded v1.1.3 release after explicit publication approval. The release tracker moved to `In Progress / 25%`; the version remains `1.1.2` until the complete local gate passes.
- `2026-07-28`: Integrated the seven validated installer and branding commits directly into `master` through `c0025cd`. GitHub automatically marked the existing draft PR #71 as merged after its full commit history became reachable from `master`; no PR merge action, version bump, tag, or publication occurred. Issue #70 is complete and release tracker #66 remains ready pending explicit release approval.
- `2026-07-28`: Completed the `Atomic A` AvaScope identity integration with deterministic light/dark repository exports, a theme-aware README lockup, and consistent setup/wizard/uninstall/application-list branding. The GitHub description was set to the product tagline. Release build, Windows/Linux installer packaging, seven-artifact verification, seven focused artifact-backed tests, PE icon inspection, and `git diff --check` passed. No version bump, tag, or publication occurred.
- `2026-07-28`: Started integration of the selected `Atomic A` AvaScope identity into repository documentation and the Windows installer. Validation covers theme-aware README artwork, setup/uninstall/application-list icons, the installed icon file, artifact verification, and focused tests. No version bump, tag, or publication was started.
- `2026-07-28`: Completed terminal color polish: only the `SUCCESS`/`FAILED` marker is green/red and all other text stays white. Success/failure ANSI assertions, setup rebuild, seven-artifact verification, Release build, and seven focused tests passed. No version bump, tag, or publication occurred.
- `2026-07-28`: Reopened #70 for final terminal color polish: retain white output and color only the `SUCCESS`/`FAILED` marker. No version bump, tag, or publication was started.
- `2026-07-28`: Completed the clarified verification UX: the Finish-page checkbox now launches a persistent ASCII terminal with installed version, green success or red failure state, actionable guidance, and keypress-to-close behavior. Windows artifact tests assert both success and simulated failure output; seven-artifact verification, Release build, and seven focused tests passed. No version bump, tag, or publication occurred.
- `2026-07-28`: Reopened #70 after product clarification: retain the Finish-page verification checkbox and replace its disappearing raw terminal with a persistent ASCII success/failure status screen. No version bump, tag, or publication was started.
- `2026-07-28`: Completed the Finish-page UX correction. `AvaScopeSetup.exe` now verifies the installed CLI and exact product version with captured hidden output, shows explicit success guidance, and reports an actionable .NET 10 runtime error instead of flashing a terminal. Windows installer smoke, seven-artifact verification, Release build, and seven focused tests passed. No version bump, tag, or publication occurred.
- `2026-07-28`: Reopened #70 for a Finish-page UX correction: remove the terminal-flashing verification action and replace it with hidden automatic command verification plus explicit wizard feedback. No version bump, tag, or publication was started.
- `2026-07-28`: Completed the corrective Windows installer slice with an Inno Setup `modern dynamic` wizard and stable `AvaScopeSetup.exe` artifact. The full Release gate passed with `371` tests, seven verified artifacts, Windows installed CLI/doctor/MCP/repair/uninstall smoke, WSL Ubuntu Linux installer smoke, and NuGet/GitHub Release dry-runs without publication. Version remains `1.1.2`; no tag or release was created.
- `2026-07-28`: Reopened #70 after interactive review showed that the first Windows artifact was a console bootstrapper. The Windows distribution is being replaced by a real non-admin Inno Setup wizard with modern light/dark styling, destination and PATH choices, repair/upgrade behavior, discovery metadata, and Apps & Features uninstall. The validated Linux terminal installer remains unchanged; no version bump, tag, or publication was started.
- `2026-07-28`: Completed local implementation for #70 in draft PR #71 with single-file `win-x64` and `linux-x64` installer artifacts embedding the existing framework-dependent payloads. The installers provide non-admin per-user install, repair/full-payload replacement, owned-path-safe uninstall, Windows current-user PATH and Apps & Features registration, Linux `~/.local/bin` command setup, discovery metadata, legal files, and a Windows signing hook. Debug and Release builds passed with 0 warnings/errors; the full Debug and Release suites each passed `371` tests; Windows artifact-backed install/doctor/MCP/repair/uninstall passed; WSL Ubuntu Linux install/doctor/MCP startup/repair/uninstall passed; the local release workflow produced and hashed seven artifacts; NuGet and GitHub Release dry-runs passed without publication. Version remained `1.1.2`; no tag or release was created.
- `2026-07-28`: Added #70 as the next release-preparation slice: non-admin Windows and Linux installers with install/upgrade/uninstall validation, installed CLI/MCP smoke tests, release-manifest integration, and portable ZIP fallback. macOS, `.deb`, `.rpm`, system-wide installation, and mandatory production signing are explicitly deferred. No implementation, version bump, tag, or publication was started.
- `2026-07-10`: #65 added the canonical Apache-2.0 license and an explicit scope grant for AvaScope-authored official releases from `0.1.0` onward; packaged legal/provenance metadata is now verified for all NuGet packages and Windows/Linux executable ZIPs. Debug build passed with 0 warnings and 0 errors, focused stable-surface tests passed (`4`), the full Debug suite passed (`370`), Release package/ZIP generation with tests and sample smoke skipped passed the new artifact checks, NuGet and GitHub Release dry-runs passed, and `git diff --check` reported only line-ending normalization warnings. PR #67 merged the slice to `master` as `7c76ce9`; #65 closed as completed. Release tracker #66 remains ready and unpublished pending the remaining planned modifications.

## Released Target: v1.1.2

- Release: `v1.1.2`
- Target Version: `1.1.2`
- Release State: `Released`
- Scope Lock: `2026-07-10`
- Release Commit: `ca3e49fd6e5c30709b7cf53cc5f1b13ae557e744` (`Release 1.1.2`)
- Local Release Gate: passed on `2026-07-10`
- Published At: `2026-07-09T22:50:50Z`
- GitHub Release: https://github.com/RolandUI/AvaScope/releases/tag/v1.1.2
- GitHub Milestone: `v1.1.2`
- GitHub Issues: #63, #64
- Previous Release: `v1.1.1`

### v1.1.2 Release Goals

The `v1.1.2` release target is a patch release for diagnostics root normalization and the latest stable Avalonia release.

1. `RG-1.1.2-1 Diagnostics Root Normalization`: complete #63 so equivalent component roots with and without trailing directory separators do not emit `diagnostics_mixed_install_roots`.
2. `RG-1.1.2-2 Avalonia 12.1.0`: upgrade every repository-owned Avalonia package reference from `12.0.4` to the latest stable `12.1.0` release and revalidate runtime, preview, bridge, sample, and headless paths.
3. `RG-1.1.2-3 Guarded Patch Release`: publish only after the local release gate passes and the Release workflow creates `v1.1.2`, NuGet/GitHub packages, executable ZIPs, manifest, and GitHub Release assets.

### v1.1.2 Milestone Map

- #63 `Diagnostics mixed-install warning treats trailing slash variants as different roots`; Status: `Done`.
- #64 `Release v1.1.2`; Status: `Done`.

### v1.1.2 Implementation Validation

- `2026-07-10`: Upgraded all repository-owned and dynamically generated test-project Avalonia references from `12.0.4` to `12.1.0`. Migrated PreviewHost and Bridge PNG screenshot saves to `PngBitmapEncoderOptions.Default` for the Avalonia 12.1 encoder API. Restore passed; Debug build passed with 0 warnings and 0 errors; the full Debug suite passed with 370 tests; 45 resolved Avalonia 12 package entries were verified at `12.1.0`; and no tracked active source/package reference remained on `12.0.4`.
- `2026-07-10`: Full `v1.1.2` local release gate passed. Release build passed with 0 warnings and 0 errors; 370 tests passed; three `1.1.2` NuGet packages, win-x64 and linux-x64 framework-dependent ZIPs, and the verified release manifest were created; packaged `doctor` and sample preview smoke passed; NuGet and GitHub Release dry-runs passed; bug-report privacy validation scanned 22 files; packaged version/capability smoke and artifact-ignore checks passed; the prospective release-commit guard passed; and remote tag `v1.1.2` remained absent before publication.
- `2026-07-10`: GitHub Release workflow `29055197705` passed for release commit `ca3e49f`; trusted publishing completed for nuget.org and GitHub Packages, tag `v1.1.2` was created at the release commit, and six GitHub Release assets were uploaded. The downloaded remote manifest reported version `1.1.2` and hashes matching the five package/ZIP asset digests returned by GitHub. The public nuget.org flat-container index subsequently exposed `1.1.2` for `AvaScope.Protocol`, `AvaScope.Core`, and `AvaScope.Bridge`.

### v1.1.2 Published Assets

| Asset | URL | SHA-256 |
| --- | --- | --- |
| `AvaScope.Protocol.1.1.2.nupkg` | https://github.com/RolandUI/AvaScope/releases/download/v1.1.2/AvaScope.Protocol.1.1.2.nupkg | `86e54c52e26706ce63ad00c5f0fe9e5c0c520a25f7e562a302720622af84bebb` |
| `AvaScope.Core.1.1.2.nupkg` | https://github.com/RolandUI/AvaScope/releases/download/v1.1.2/AvaScope.Core.1.1.2.nupkg | `3da4081474ab63766813f323086abc35c864cdf45f7502f669c8b9cd616ae748` |
| `AvaScope.Bridge.1.1.2.nupkg` | https://github.com/RolandUI/AvaScope/releases/download/v1.1.2/AvaScope.Bridge.1.1.2.nupkg | `75e68996008a6f9420a42e0f704339a8865750968a5a4aa1642c9306a135349e` |
| `avascope-win-x64-framework-dependent.zip` | https://github.com/RolandUI/AvaScope/releases/download/v1.1.2/avascope-win-x64-framework-dependent.zip | `18688438729ffa6cbbe40c1c9dbe36ab4e3a59cdbb8377e84ac62dc1b9ee40b7` |
| `avascope-linux-x64-framework-dependent.zip` | https://github.com/RolandUI/AvaScope/releases/download/v1.1.2/avascope-linux-x64-framework-dependent.zip | `c4bd9a949b8a423841a05e307c2885b1180772168a84507c3483fb49bd3bf42f` |
| `release-manifest.json` | https://github.com/RolandUI/AvaScope/releases/download/v1.1.2/release-manifest.json | `ba0f410cc9b655f6cb1277d5f94829e58f6e1bc526f598daaf67eab6a4828e93` |

## Released Target: v1.1.1

- Release: `v1.1.1`
- Target Version: `1.1.1`
- Release State: `Released`
- Scope Lock: `2026-07-02`
- Release Commit: `6e710509aef727d4d74bf3580f5ee6ec0564c445` (`Release 1.1.1`)
- Local Release Gate: passed on `2026-07-02`
- Published At: `2026-07-02T12:54:41Z`
- GitHub Release: https://github.com/RolandUI/AvaScope/releases/tag/v1.1.1
- GitHub Milestone: `v1.1.1`
- GitHub Issues: #56, #57, #58, #59, #60, #61, #62
- Previous Release: `v1.1.0`

### v1.1.1 Release Goals

The `v1.1.1` release target is a defect-focused patch for the AvaScope 1.1.0 TradeR smoke-test report. Scope is limited to the completed bug issues and guarded release publication.

1. `RG-1.1.1-1 Runtime MCP Manifest Store Resolution`: ship #56 so runtime MCP follow-up tools consistently honor `manifestDirectory`.
2. `RG-1.1.1-2 Serialized Preview Project Builds`: ship #57 so parallel previews do not collide on project intermediate output.
3. `RG-1.1.1-3 Selector-First Runtime Targeting`: ship #58 so stale/raw runtime node ids get explicit diagnostics and selector-based recovery where possible.
4. `RG-1.1.1-4 Pointer Diagnostics Target Evidence`: ship #59 so pointer workflows report requested/effective coordinates and hit-path mismatches.
5. `RG-1.1.1-5 Run-Index Help Accuracy`: ship #60 so `baseline-create` rejects unsupported run-index flags with command-specific guidance.
6. `RG-1.1.1-6 Component Origin Diagnostics`: ship #61 so diagnostics identify CLI, MCP, and PreviewHost component origins and mixed install roots.
7. `RG-1.1.1-7 Guarded Patch Release`: publish only after the local release gate passes and the Release workflow creates `v1.1.1`, NuGet/GitHub packages, executable ZIPs, manifest, and GitHub Release assets.

### v1.1.1 Milestone Map

- #56 `Runtime MCP tools cannot resolve custom manifestDirectory bridge sessions`; Status: `Done`.
- #57 `Parallel previews collide on project obj intermediate output`; Status: `Done`.
- #58 `Runtime raw node ids are too fragile for multi-step workflows`; Status: `Done`.
- #59 `Pointer diagnostics and pseudo-state input miss selected control`; Status: `Done`.
- #60 `Clarify baseline-create run-index capability/help mismatch`; Status: `Done`.
- #61 `Diagnostics can report PreviewHost from a different install root than repo-local CLI`; Status: `Done`.
- #62 `Release v1.1.1`; Status: `Done`.

### v1.1.1 Implementation Validation

- `2026-07-02`: Started release tracker #62 after #56-#61 were completed and closed. The patch scope is locked to the AvaScope 1.1.0 TradeR smoke-test bug set plus guarded release publication.
- `2026-07-02`: Local `v1.1.1` release gate passed after stopping three stale artifact-hosted `dotnet ...\AvaScope.Mcp.dll` processes that held the previous packaged output. Validation passed with `dotnet build AvaScope.slnx --no-restore -v:minimal`, `dotnet test AvaScope.slnx --no-build` (`369` passed), focused bridge/headless reproduction tests (`18` passed), `eng/create-local-release.ps1` (Release build/test `369` passed, three `1.1.1` NuGet packages, win/linux framework-dependent ZIPs, manifest verification, packaged doctor smoke, and packaged sample preview smoke), `eng/publish-nuget.ps1 -DryRun`, `eng/publish-github-release.ps1 -Tag v1.1.1 -DryRun`, packaged `avascope.exe --version` (`1.1.1`), and packaged capability gate for the runtime/preview/artifact ids.
- `2026-07-02`: Published `v1.1.1` from commit `6e710509aef727d4d74bf3580f5ee6ec0564c445` through Release workflow `28591077660` (`push`, success). The workflow published `AvaScope.Protocol`, `AvaScope.Core`, and `AvaScope.Bridge` `1.1.1` to nuget.org and GitHub Packages through NuGet trusted publishing, created tag `v1.1.1`, and uploaded the GitHub Release assets. `git ls-remote --tags origin refs/tags/v1.1.1` confirmed the tag points at `6e710509aef727d4d74bf3580f5ee6ec0564c445`.

### v1.1.1 Published Assets

| Asset | URL | SHA-256 |
| --- | --- | --- |
| `AvaScope.Protocol.1.1.1.nupkg` | https://github.com/RolandUI/AvaScope/releases/download/v1.1.1/AvaScope.Protocol.1.1.1.nupkg | `d9f505b182a6f6c8bf3c4a8ec01b71f047aad645c936fea59ad186608fcca717` |
| `AvaScope.Core.1.1.1.nupkg` | https://github.com/RolandUI/AvaScope/releases/download/v1.1.1/AvaScope.Core.1.1.1.nupkg | `576b20e4933a60a271d79b8cf7ac2e96f090c02114023896abd6dbe257003a40` |
| `AvaScope.Bridge.1.1.1.nupkg` | https://github.com/RolandUI/AvaScope/releases/download/v1.1.1/AvaScope.Bridge.1.1.1.nupkg | `493c95e229765fa4bb9977532b4e47e8f094ca7b5acf02fee6d0183790b3be3d` |
| `avascope-win-x64-framework-dependent.zip` | https://github.com/RolandUI/AvaScope/releases/download/v1.1.1/avascope-win-x64-framework-dependent.zip | `6f2f0618373559add7fa8a9040dd3cca647c4da78c532e859a4bc55519dd68f6` |
| `avascope-linux-x64-framework-dependent.zip` | https://github.com/RolandUI/AvaScope/releases/download/v1.1.1/avascope-linux-x64-framework-dependent.zip | `3eab66333bec1f39845fd2b116476db36cced46155055054c90babc9f041de34` |
| `release-manifest.json` | https://github.com/RolandUI/AvaScope/releases/download/v1.1.1/release-manifest.json | `dfa02805e64f8bd74c81b4a8aa599c54422a8aea19d6f49e7ae4592be092c0b6` |

## Released Target: v1.1.0

- Release: `v1.1.0`
- Target Version: `1.1.0`
- Release State: `Released`
- Scope Lock: `2026-07-02`
- Release Commit: `88bc782cfe8d9ede259a3f542a3b3ab7e7bd449f` (`Release 1.1.0`)
- Local Release Gate: passed on `2026-07-02`
- Published At: `2026-07-02T05:08:51Z`
- GitHub Release: https://github.com/RolandUI/AvaScope/releases/tag/v1.1.0
- GitHub Milestone: `v1.1.0`
- GitHub Issues: #55
- Previous Release: `v1.0.2`

### v1.1.0 Release Goals

The `v1.1.0` release target is an additive minor release for agent-facing runtime diagnostics, semantic workflows, visual evidence workflows, artifact navigation, and discovery/versioning surfaces completed after `v1.0.2`.

1. `RG-1.1.0-1 Runtime Source And Binding Diagnostics`: ship runtime node source maps, live binding/DataContext inspection, and layout explanation diagnostics with conservative public-metadata provenance.
   Success signal: agents can inspect node source, binding, DataContext, layout, clipping, Grid, ScrollViewer, and constraint data without manually searching AXAML first.
2. `RG-1.1.0-2 Semantic Runtime Workflows`: ship coordinate-free workflow/scenario execution, pointer diagnostics, pseudo-state matrices, and interaction-triggered animation frame capture.
   Success signal: agents can act through AutomationId/text/role/binding/command selectors and capture structured evidence for hover, popup, state, and animation failures.
3. `RG-1.1.0-3 Preview And Visual Evidence Workflows`: ship named preview state variants, isolated preview builds, semantic screenshot diff, design-quality audits, and clickable provenance maps in review artifacts.
   Success signal: agents can preview explicit UI states, compare visual output against references, and navigate from artifacts back to runtime/source metadata.
4. `RG-1.1.0-4 Diagnostics And Discovery Ergonomics`: ship concise diagnostics modes, run-index/latest-run artifact navigation, standard product-version surfaces, and improved capability descriptions.
   Success signal: agents can discover capabilities through `capabilities`, gate required workflows by capability id, and resolve recent artifacts without directory scanning.
5. `RG-1.1.0-5 Guarded Minor Release`: publish only after the local release gate passes and the Release workflow creates `v1.1.0`, NuGet/GitHub packages, executable ZIPs, manifest, and GitHub Release assets.

### v1.1.0 Milestone Map

- #55 `Release v1.1.0`; Status: `In Progress`.

### v1.1.0 Implementation Validation

- `2026-07-02`: Started release tracker #55 and selected `v1.1.0` because `master` contains additive feature commits after the published `v1.0.2` tag. Capability descriptions were tightened for agent discovery, especially source mapping, binding inspection, layout explanation, semantic workflows, and preview state variants.
- `2026-07-02`: Local `v1.1.0` release gate passed after stopping a stale artifact-hosted MCP process that held the previous packaged output. Validation passed with `dotnet build AvaScope.slnx --no-restore -v:minimal`, `dotnet test AvaScope.slnx --no-build` (`356` passed), `eng/create-local-release.ps1` (Release build/test `356` passed, three `1.1.0` NuGet packages, win/linux framework-dependent ZIPs, manifest verification, packaged doctor smoke, and packaged sample preview smoke), `eng/publish-nuget.ps1 -DryRun`, `eng/publish-github-release.ps1 -Tag v1.1.0 -DryRun`, packaged `avascope.exe --version`, packaged `capabilities --require runtime.source_map,runtime.layout_explain,runtime.binding_inspector,runtime.semantic_workflow,preview.state_variants,artifacts.run_index`, and `git diff --check` with only LF/CRLF normalization warnings.
- `2026-07-02`: Published `v1.1.0` from commit `88bc782cfe8d9ede259a3f542a3b3ab7e7bd449f` through Release workflow `28566542499` (`push`, success). The workflow published `AvaScope.Protocol`, `AvaScope.Core`, and `AvaScope.Bridge` `1.1.0` to nuget.org and GitHub Packages through NuGet trusted publishing, created tag `v1.1.0`, and uploaded the GitHub Release assets. `git ls-remote --tags origin refs/tags/v1.1.0` confirmed the tag points at `88bc782cfe8d9ede259a3f542a3b3ab7e7bd449f`.

### v1.1.0 Published Assets

| Asset | URL | SHA-256 |
| --- | --- | --- |
| `AvaScope.Protocol.1.1.0.nupkg` | https://github.com/RolandUI/AvaScope/releases/download/v1.1.0/AvaScope.Protocol.1.1.0.nupkg | `df25905b013f39d29afe8394fe55df0f534d4781c1db29b603ca7314750dbab1` |
| `AvaScope.Core.1.1.0.nupkg` | https://github.com/RolandUI/AvaScope/releases/download/v1.1.0/AvaScope.Core.1.1.0.nupkg | `2c1b0667622ce29a6dbd522ce56482a2d5c91fdfafd8d2fe7f0ecfd402b62544` |
| `AvaScope.Bridge.1.1.0.nupkg` | https://github.com/RolandUI/AvaScope/releases/download/v1.1.0/AvaScope.Bridge.1.1.0.nupkg | `64c937eefaf17d83c2a921bf2ae0a93fe0c26c20208ecdc37964fa481c51728e` |
| `avascope-win-x64-framework-dependent.zip` | https://github.com/RolandUI/AvaScope/releases/download/v1.1.0/avascope-win-x64-framework-dependent.zip | `7c7bb09e59f59703644e3dbb55e617919a37b8526e4f0f5321ea7838480c92a9` |
| `avascope-linux-x64-framework-dependent.zip` | https://github.com/RolandUI/AvaScope/releases/download/v1.1.0/avascope-linux-x64-framework-dependent.zip | `dceb6b12e5a6a3a51f43f5e23807c072e3e75fa746939d362a76f3b0653f7e22` |
| `release-manifest.json` | https://github.com/RolandUI/AvaScope/releases/download/v1.1.0/release-manifest.json | `1e925a09c4fd6d06c40e04c1037112f7457e15f1bc409210bcfc5e9ff046212c` |

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

## Released Target: v1.0.2

- Release: `v1.0.2`
- Target Version: `1.0.2`
- Release State: `Released`
- Scope Lock: `2026-06-13`
- Release Commit: `15a4af1547376b9beb7a76c3a8c947dcd4bf8187` (`Release 1.0.2`)
- Local Release Gate: passed on `2026-06-13`
- Published At: `2026-06-13T10:42:20Z`
- GitHub Release: https://github.com/RolandUI/AvaScope/releases/tag/v1.0.2
- GitHub Milestone: `v1.0.2`
- GitHub Issues: #43
- Previous Release: `v1.0.1`

### v1.0.2 Release Goals

The `v1.0.2` release target is a patch release for PreviewHost fidelity and package publishing reliability after the `v1.0.1` patch. Scope is limited to defect-focused preview parity and NuGet trusted publishing migration.

1. `RG-1.0.2-1 App-Level Style And ControlTheme Fidelity`: apply compiled project `Application.Styles` at application scope in PreviewHost so App.axaml resources, custom control templates, and implicit `{x:Type ...}` control themes resolve like the running app.
   Success signal: TradeR `MainWindow.axaml` renders custom Button, ComboBox, DatePicker, and TimePicker templates instead of fallback Fluent controls.
2. `RG-1.0.2-2 Inter Font Preview Parity`: detect projects that reference `Avalonia.Fonts.Inter` or call `.WithInterFont()` and configure the PreviewHost app builder with Inter font support.
   Success signal: TradeR `ChartView`, `LiveTradeView`, and `MainWindow` preview text metrics match the live app more closely.
3. `RG-1.0.2-3 NuGet Trusted Publishing`: migrate the Release workflow from a stored NuGet API key secret to NuGet trusted publishing through GitHub Actions OIDC.
   Success signal: the Release workflow can publish to nuget.org with `NuGet/login@v1` and `id-token: write`.
4. `RG-1.0.2-4 Guarded Patch Release`: publish only after the local release gate passes and the Release workflow creates `v1.0.2`, NuGet/GitHub packages, executable ZIPs, manifest, and GitHub Release assets.

### v1.0.2 Milestone Map

- #43 `Release v1.0.2`; Status: `Done`.

### v1.0.2 Implementation Validation

- `2026-06-13`: PreviewHost now moves compiled project `Application.Styles` into the host application style collection when App.axaml is loaded, clearing the fallback host theme only when project styles exist. Added a regression smoke for implicit `{x:Type Button}` `ControlTheme` resolution. Validation passed with the focused implicit-control-theme test, four nearby app style/resource preview tests, and TradeR `MainWindow.axaml` preview smoke.
- `2026-06-13`: PreviewHost now references `Avalonia.Fonts.Inter` and enables `.WithInterFont()` when the previewed project references `Avalonia.Fonts.Inter` or calls `.WithInterFont()` in `Program.cs`. Validation passed with `dotnet build src\AvaScope.PreviewHost\AvaScope.PreviewHost.csproj`, the implicit-control-theme regression test, and TradeR `ChartView`, `LiveTradeView`, and `MainWindow` preview smokes.
- `2026-06-13`: Release workflow now grants `id-token: write`, logs in with `NuGet/login@v1`, and passes the trusted-publishing API key output to `eng/publish-nuget.ps1`.
- `2026-06-13`: Local `v1.0.2` release gate passed with `eng/create-local-release.ps1` (Release build, 317 Release tests, three `1.0.2` NuGet packages, win/linux framework-dependent ZIPs, manifest verification, packaged doctor smoke, and packaged sample preview smoke), `eng/publish-nuget.ps1 -DryRun`, `eng/publish-github-release.ps1 -Tag v1.0.2 -DryRun`, and packaged TradeR `ChartView`, `LiveTradeView`, and `MainWindow` preview smokes from `artifacts\executables\avascope-win-x64-framework-dependent\avascope.exe`. The first parallel `LiveTradeView` smoke hit a TradeR build output file lock and passed when rerun serially.
- `2026-06-13`: Published `v1.0.2` from commit `15a4af1547376b9beb7a76c3a8c947dcd4bf8187` through Release workflow `27464291295` (`push`, success). The workflow created tag `v1.0.2`, published `AvaScope.Protocol`, `AvaScope.Core`, and `AvaScope.Bridge` `1.0.2` to nuget.org and GitHub Packages through NuGet trusted publishing, and uploaded the GitHub Release assets. `git ls-remote --tags origin refs/tags/v1.0.2` confirmed the tag points at `15a4af1547376b9beb7a76c3a8c947dcd4bf8187`.

### v1.0.2 Published Assets

| Asset | URL | SHA-256 |
| --- | --- | --- |
| `AvaScope.Protocol.1.0.2.nupkg` | https://github.com/RolandUI/AvaScope/releases/download/v1.0.2/AvaScope.Protocol.1.0.2.nupkg | `3763e6eff8e34061fffbd5b986091d242af55d781b6c61fd75073a5c482980e6` |
| `AvaScope.Core.1.0.2.nupkg` | https://github.com/RolandUI/AvaScope/releases/download/v1.0.2/AvaScope.Core.1.0.2.nupkg | `fea3920e8c7280fad5a7ff44bbbaeff202cc375e4040a30927dfbff404644fa7` |
| `AvaScope.Bridge.1.0.2.nupkg` | https://github.com/RolandUI/AvaScope/releases/download/v1.0.2/AvaScope.Bridge.1.0.2.nupkg | `8aecd587d1bea48a42a3b07ded0b6e5669593381856fafae51d6f8d17a0ac2b4` |
| `avascope-win-x64-framework-dependent.zip` | https://github.com/RolandUI/AvaScope/releases/download/v1.0.2/avascope-win-x64-framework-dependent.zip | `56c3752959313ed97167dafaadb70c87398a22f03d596548d11215c7903acb01` |
| `avascope-linux-x64-framework-dependent.zip` | https://github.com/RolandUI/AvaScope/releases/download/v1.0.2/avascope-linux-x64-framework-dependent.zip | `ad6058a2df9a773fb205fa080999798b1c724d20710b343b62e4e56e1a21a5e4` |
| `release-manifest.json` | https://github.com/RolandUI/AvaScope/releases/download/v1.0.2/release-manifest.json | `19292feb814c54de403c3acdb56572042d6021e485af456cc039e538bfa7535d` |

### v1.0.2 Explicit Deferrals

- Full target-app `Program.BuildAvaloniaApp()` execution remains out of scope; this patch only mirrors known safe font configuration needed for preview fidelity.
- Design-time demo/live workspace state remains controlled by target project design data, not PreviewHost mutation.

## Released Target: v1.0.1

- Release: `v1.0.1`
- Target Version: `1.0.1`
- Release State: `Released`
- Scope Lock: `2026-06-13`
- Release Commit: `8c496f7ea5f22a1933a3950200ce7aa66037367a` (`Release 1.0.1`)
- Local Release Gate: passed on `2026-06-13`
- Published At: `2026-06-13T09:41:34Z`
- GitHub Release: https://github.com/RolandUI/AvaScope/releases/tag/v1.0.1
- GitHub Milestone: `v1.0.1`
- GitHub Issues: #41, #42
- Previous Release: `v1.0.0`

### v1.0.1 Release Goals

The `v1.0.1` release target is a patch release for PreviewHost diagnostic fidelity after the stable `v1.0.0` publication. Scope is limited to defect-focused diagnostic noise reduction and release validation.

1. `RG-1.0.1-1 Hash Element Binding Diagnostics`: treat Avalonia hash element-name bindings such as `{Binding #TargetButton}` as explicit binding sources, not DataContext or `x:DataType` paths.
   Success signal: PreviewHost no longer emits false binding path diagnostics for valid hash element-name bindings.
2. `RG-1.0.1-2 Intentional Overlay Diagnostics`: suppress overlap diagnostics when an intentional overlay, popup, or adorner child overlaps sibling content.
   Success signal: chart and overlay-heavy views keep useful diagnostics without warning on expected overlay layers.
3. `RG-1.0.1-3 Guarded Patch Release`: publish only after the stable release gate passes and the release workflow creates the `v1.0.1` tag, packages, executable ZIPs, manifest, and GitHub Release assets.

### v1.0.1 Milestone Map

- #41 `Preview diagnostics should treat hash element-name bindings as explicit sources`; Status: `Done`.
- #42 `Release v1.0.1`; Status: `In Progress`.

### v1.0.1 Implementation Validation

- `2026-06-13`: #41 completed in commit `16bf95614826934a4b06afa48230b50d2dbc55a5`. Local validation passed with focused PreviewHost regression tests (2 passed), full `PreviewHostSmokeTests` (31 passed), TradeR `MainWindow.axaml` preview smoke through the source CLI, `dotnet build AvaScope.slnx --no-restore -v:minimal`, and `git diff --check` with only LF/CRLF normalization warnings. The TradeR smoke confirmed App.axaml styles/resources load and that the hash binding and custom overlay warnings were removed.
- `2026-06-13`: Moved `v1.0.1` to `Release Candidate` and bumped `Directory.Build.props` to `1.0.1` in the release-candidate working tree for the local release gate.
- `2026-06-13`: Local `v1.0.1` release gate passed on the release-candidate working tree after stopping stale artifact-hosted `avascope`/`dotnet` processes from previous local release artifacts. Validation passed with `eng/create-local-release.ps1` (Release build, 316 Release tests, three `1.0.1` NuGet packages, win/linux framework-dependent ZIPs, manifest verification, packaged doctor smoke, and packaged sample preview smoke), `eng/publish-nuget.ps1 -DryRun`, `eng/publish-github-release.ps1 -Tag v1.0.1 -DryRun`, `eng/validate-release-commit.ps1 -Version 1.0.1 -CommitSubject "Release 1.0.1" -RequiredState "Release Candidate"`, and `git diff --check` with only LF/CRLF normalization warnings.
- `2026-06-13`: Published `v1.0.1` from commit `8c496f7ea5f22a1933a3950200ce7aa66037367a` through Release workflow `27462977168` (`push`, success). The workflow created tag `v1.0.1`, published `AvaScope.Protocol`, `AvaScope.Core`, and `AvaScope.Bridge` `1.0.1` to nuget.org and GitHub Packages, and uploaded the GitHub Release assets. `git ls-remote --tags origin refs/tags/v1.0.1` confirmed the tag points at `8c496f7ea5f22a1933a3950200ce7aa66037367a`.

### v1.0.1 Published Assets

| Asset | URL | SHA-256 |
| --- | --- | --- |
| `AvaScope.Protocol.1.0.1.nupkg` | https://github.com/RolandUI/AvaScope/releases/download/v1.0.1/AvaScope.Protocol.1.0.1.nupkg | `d1d1a29b25bb08e133f0c78663bc5a7055f92e016699941c2b8e2a86815a39c8` |
| `AvaScope.Core.1.0.1.nupkg` | https://github.com/RolandUI/AvaScope/releases/download/v1.0.1/AvaScope.Core.1.0.1.nupkg | `ca9303e781d3bc4d4cdfbaeb4f129803f489e1ceec1b8da94bb4fb03c1dd3891` |
| `AvaScope.Bridge.1.0.1.nupkg` | https://github.com/RolandUI/AvaScope/releases/download/v1.0.1/AvaScope.Bridge.1.0.1.nupkg | `09f6851f2727aadf1762e9b38e1b62b6176d1544609f585aa717590d1d3dda15` |
| `avascope-win-x64-framework-dependent.zip` | https://github.com/RolandUI/AvaScope/releases/download/v1.0.1/avascope-win-x64-framework-dependent.zip | `a6a0fc38a8b8895b66064854f827e8cdd937d6c8b24b34ee18fff414e2c0b4c6` |
| `avascope-linux-x64-framework-dependent.zip` | https://github.com/RolandUI/AvaScope/releases/download/v1.0.1/avascope-linux-x64-framework-dependent.zip | `23d7508daa29a0f00222b0852050afcad84368f850c408ff691d00c7e0c35f0a` |
| `release-manifest.json` | https://github.com/RolandUI/AvaScope/releases/download/v1.0.1/release-manifest.json | `094fa5458623cf88508cf4dac103089eada0e570c3bf7943554726783a35c450` |

### v1.0.1 Explicit Deferrals

- Broader layout heuristic tuning remains post-`v1.0.1` unless backed by concrete false-positive reproductions.
- No public protocol, package identity, CLI, or MCP schema changes are included in this patch.

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

## Released Target: v0.8.0

- Release: `v0.8.0`
- Target Version: `0.8.0`
- Release State: `Released`
- Scope Lock: locked
- Release Commit: `d2d4d01` (`Release 0.8.0`)
- Local Release Gate: passed `2026-06-13`
- Published At: `2026-06-13T00:29:08Z`
- GitHub Release: https://github.com/RolandUI/AvaScope/releases/tag/v0.8.0
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
- `R0.8.0-M5 MCP/CLI Agent Review Surface`; Status: `Done`.
- `R0.8.0-M6 Release Candidate And Version Bump`; Status: `Done`.

### v0.8.0 Implementation Validation

- `2026-06-12`: GitHub issues #20 through #23 completed with remote CI and Release workflow validation for suite manifests, threshold/mask rules, report packs, and the GitHub Actions artifact example.
- `2026-06-13`: GitHub issue #24 completed after commit `9de95b0dc542562e2bcf4384f34c22ce7b709eba` passed GitHub CI workflow `27449993825` and Release workflow `27449993838`; the issue was moved to `status:done`, project Done/100/Completed, and closed.
- `2026-06-13`: GitHub issue #25 release-candidate gate passed for `v0.8.0`. Validation included `dotnet build AvaScope.slnx --no-restore -v:minimal`, full Debug tests (`dotnet test AvaScope.slnx --no-build`, 275 passed), `powershell -NoProfile -ExecutionPolicy Bypass -File .\eng\create-local-release.ps1` (Release build/test 275 passed, three `0.8.0` packages, win-x64/linux-x64 framework-dependent ZIPs, release manifest, packaged doctor smoke, and packaged sample preview smoke), `powershell -NoProfile -ExecutionPolicy Bypass -File .\eng\publish-github-release.ps1 -Tag v0.8.0 -DryRun`, packaged CLI suite/report smoke (`baseline-create --suite`, `baseline-check --report --report-pack`, 2 entries passed), report/report-pack JSON validation, `powershell -NoProfile -ExecutionPolicy Bypass -File .\eng\validate-release-commit.ps1 -Version 0.8.0 -CommitSubject "Release 0.8.0" -RequiredState "Release Candidate"`, and `git diff --check` with only LF/CRLF normalization warnings.
- `2026-06-13`: GitHub CI workflow `27450680002` and Release workflow `27450679987` passed for `Release 0.8.0`; tag `v0.8.0` and six GitHub Release assets were published at `2026-06-13T00:29:08Z`.
- `2026-06-13`: `gh release view v0.8.0` confirmed the public release URL and six uploaded assets: three `0.8.0` `.nupkg` files, win-x64 and linux-x64 framework-dependent ZIPs, and `release-manifest.json`.
- `2026-06-13`: `git ls-remote --tags origin refs/tags/v0.8.0` confirmed tag `v0.8.0` at release commit `d2d4d01652efaf2812acf95a6b60621c93352ada`.

### v0.8.0 Explicit Deferrals

- Automatic baseline approval or mutation from CI remains out of scope unless explicitly gated by user action.
- Pixel-perfect guarantees across every OS/font stack remain out of scope; reports must expose environment metadata instead.
- Native editor plugins remain optional; this release improves artifact/viewer handoff but does not require shipped IDE extensions.
- Remote multi-user inspection remains out of scope.

## Released Target: v0.9.0

- Release: `v0.9.0`
- Target Version: `0.9.0`
- Release State: `Released`
- Scope Lock: `2026-06-13`
- Release Commit: `f956e48` (`Release 0.9.0`)
- Local Release Gate: passed `2026-06-13`
- Published At: `2026-06-13T04:07:36Z`
- GitHub Release: https://github.com/RolandUI/AvaScope/releases/tag/v0.9.0
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

- `R0.9.0-M1 Source-Aware Change Suggestions`; Status: `Done`.
- `R0.9.0-M2 Accessibility, Validation, And Component Inventory`; Status: `Done`.
- `R0.9.0-M3 Protocol Capability And Versioning Contract`; Status: `Done`.
- `R0.9.0-M4 Security, Safety, And Compatibility Audit`; Status: `Done`.
- `R0.9.0-M5 Performance, Stress, Samples, And Troubleshooting Audit`; Status: `Done`.
- `R0.9.0-M6 Release Candidate And Version Bump`; Status: `Done`.

### v0.9.0 Explicit Deferrals

- Automatic source editing remains out of scope; suggested fixes are advisory unless a later release adds an explicit guarded patch workflow.
- Any new product capability not required for 1.0 stability should move to post-1.0 unless it blocks the readiness definition.
- Broad native IDE plugin implementation remains post-1.0 unless already validated through contracts and small adapters.
- Remote/network inspection remains out of scope.

## Released Target: v1.0.0

- Release: `v1.0.0`
- Target Version: `1.0.0`
- Release State: `Released`
- Scope Lock: `2026-06-13`
- Release Commit: `2736b98` (`Release 1.0.0`)
- Local Release Gate: passed `2026-06-13`
- Published At: `2026-06-13T06:54:38Z`
- GitHub Release: https://github.com/RolandUI/AvaScope/releases/tag/v1.0.0
- Previous Release: `v0.9.0`

### v1.0.0 Release Goals

The `v1.0.0` release target is the stable public release. The goal is not to add broad new features; it is to freeze and verify the workflows that make AvaScope dependable.

1. `RG-1.0.0-1 Stable Surface Freeze`: freeze public packages, protocol DTOs, CLI commands, MCP tools, exit codes, artifact naming, and release workflow behavior.
   Success signal: compatibility rules and migration guidance exist in [STABLE_SURFACE.md](STABLE_SURFACE.md), and stable surfaces are covered by contract tests.
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

- `R1.0.0-M1 Stable Surface Freeze`; Status: `Done`.
- `R1.0.0-M2 End-To-End Workflow Verification`; Status: `Done`.
- `R1.0.0-M3 Documentation Completion`; Status: `Done`.
- `R1.0.0-M4 Release Artifact And Package Verification`; Status: `Done`.
- `R1.0.0-M5 Post-1.0 Backlog And Deferral Audit`; Status: `Done`.
- `R1.0.0-M6 Stable Release Commit And Publication`; Status: `Done`.

### v1.0.0 Implementation Validation

- `2026-06-13`: `R1.0.0-M1 Stable Surface Freeze` completed after remote validation. Commit `4408ca7` passed GitHub CI workflow `27456458960` and Release workflow `27456458959` (no-op). The slice adds `docs/STABLE_SURFACE.md`, aligns capability discovery with implemented `mcp`, `launch-app`/`launch_app`, and `close-session`/`close_session` surfaces, and adds contract tests for CLI/MCP/package/artifact/release stability.
- `2026-06-13`: Started `R1.0.0-M2 End-To-End Workflow Verification` with source and packaged runtime, preview, diagnostics, mutation, visual-regression, report, CLI, and MCP validation as the active release gate.
- `2026-06-13`: Local `R1.0.0-M2` validation passed and is recorded in [END_TO_END_VALIDATION.md](END_TO_END_VALIDATION.md): source restore/build/test, local release artifact creation, GitHub Release dry-run, packaged CLI workflows, packaged runtime bridge workflows, packaged MCP `tools/list`, and open P0/P1 blocker audit.
- `2026-06-13`: Stabilized the `R1.0.0-M2` hosted CI follow-up after GitHub CI workflow `27457002598` exposed CLI fake bridge manifest coupling under the full Release test run. The fix is test-harness only: implicit CLI fake bridge manifests now use an async-test-flow isolated manifest directory. Targeted Release CLI smoke validation passed with 91 tests.
- `2026-06-13`: Completed `R1.0.0-M2` after remote validation. Commit `f525e11` passed GitHub CI workflow `27457413804` and Release workflow `27457413805` (no-op), and #35 was closed. Started `R1.0.0-M3 Documentation Completion` as the active release slice.
- `2026-06-13`: Completed local `R1.0.0-M3` documentation implementation. Primary docs now use stable v1 positioning, upgrade guidance is documented in [UPGRADE.md](UPGRADE.md), stale active public-alpha/pre-1.0 wording is removed from primary docs, and `DocumentationCompletionTests` covers stable install, upgrade, CLI, MCP, bridge, preview, mutation, baseline, safety, and stale-wording regressions. Local validation passed with markdown link/path check, stale wording check, build, focused docs tests, full Debug tests, packaged local release creation with sample smoke, packaged capability gate, and `git diff --check`.
- `2026-06-13`: Completed `R1.0.0-M3` after remote validation. Commit `61a340f` passed GitHub CI workflow `27457868832` and Release workflow `27457868833` (no-op), and #36 was closed. Started `R1.0.0-M4 Release Artifact And Package Verification` as the active release slice.
- `2026-06-13`: For `R1.0.0-M4`, GitHub Actions quota pressure changed the validation policy until final publish: development CI is manual-only, intermediate slices are locally validated, and the `Release` workflow is limited to `Directory.Build.props` version-bump pushes or manual dispatch so only the final release publish consumes CI.
- `2026-06-13`: Completed local `R1.0.0-M4` artifact verification and recorded it in [RELEASE_ARTIFACT_VERIFICATION.md](RELEASE_ARTIFACT_VERIFICATION.md). The slice validated temporary `1.0.0` local release creation, Release tests (312 passed), NuGet and GitHub Release dry-runs, manifest/hash/package metadata, framework-dependent ZIP contents, packaged CLI capability gate, packaged MCP stdio `tools/list` with `serverInfo.name=avascope`, the opt-in win-x64 self-contained lane, and restoration of the default framework-dependent asset set. `Directory.Build.props` was restored to `0.9.0`; the actual `1.0.0` bump remains reserved for the final release commit.
- `2026-06-13`: Completed `R1.0.0-M4` after local validation only. Commit `b0bbf6d` did not trigger GitHub Actions by design, #37 was closed, and `R1.0.0-M5 Post-1.0 Backlog And Deferral Audit` started as the active release slice.
- `2026-06-13`: Completed local `R1.0.0-M5` backlog audit and recorded it in [POST_1_0_BACKLOG.md](POST_1_0_BACKLOG.md). The audit found no open P1 issues and no hidden product P0 issue; the only open P0 issues are #33 release tracker and #39 final release/publish slice.
- `2026-06-13`: Completed `R1.0.0-M5` after local validation only. Commit `a27c41d` did not trigger GitHub Actions by design, #38 was closed, and `R1.0.0-M6 Stable Release Commit And Publication` started as the active release slice.
- `2026-06-13`: Moved `v1.0.0` to `Release Candidate` for the final local release gate and bumped `Directory.Build.props` to `1.0.0` in the release-candidate working tree. Development CI remains manual-only; the final `Release` workflow is the only expected GitHub Actions run for publication.
- `2026-06-13`: Local `v1.0.0` release gate passed on the release-candidate working tree: `dotnet build AvaScope.slnx -v:minimal`, `dotnet test AvaScope.slnx --no-build` (314 passed), `eng/create-local-release.ps1` (Release build, 314 Release tests, three `1.0.0` NuGet packages, win/linux framework-dependent ZIPs, manifest verification, packaged doctor and sample preview smoke), `eng/publish-nuget.ps1 -DryRun`, `eng/publish-github-release.ps1 -Tag v1.0.0 -DryRun`, `eng/validate-release-commit.ps1 -Version 1.0.0 -CommitSubject "Release 1.0.0" -RequiredState "Release Candidate"`, and `git diff --check` with only LF/CRLF normalization warnings.
- `2026-06-13`: The first hosted Release workflow for commit `fe90eb9` (`27459143938`) failed before publishing or tagging in `Create release artifacts`. Root cause was a hosted-runner-only cleanup `NullReferenceException` from `Avalonia.Headless.HeadlessUnitTestSession.Dispose()` after `McpExpandedInputAndRuntimeStateInspectionUseBridgeOnly` had already completed its assertions. The test now uses the repository's existing explicit headless cleanup helper. Post-fix local validation passed with Release build, the targeted failing Release test, full `eng/create-local-release.ps1` (314 Release tests plus package/ZIP/manifest/smoke checks), both publish dry-runs, release commit guard, and `git diff --check`.
- `2026-06-13`: Published `v1.0.0` from commit `2736b986db8a003680aaa8996d7093e00eb73374` through Release workflow `27459439796` (`workflow_dispatch`, success). The workflow created tag `v1.0.0`, published `AvaScope.Protocol`, `AvaScope.Core`, and `AvaScope.Bridge` `1.0.0` to nuget.org and GitHub Packages, and uploaded the GitHub Release assets. `git ls-remote --tags origin refs/tags/v1.0.0` confirmed the tag points at `2736b986db8a003680aaa8996d7093e00eb73374`.

### v1.0.0 Published Assets

| Asset | URL | SHA-256 |
| --- | --- | --- |
| `AvaScope.Protocol.1.0.0.nupkg` | https://github.com/RolandUI/AvaScope/releases/download/v1.0.0/AvaScope.Protocol.1.0.0.nupkg | `b2329e58cd2647dfb4c523a4dbc2d549394ebe9d307cf9024bc819f0aff9f6ed` |
| `AvaScope.Core.1.0.0.nupkg` | https://github.com/RolandUI/AvaScope/releases/download/v1.0.0/AvaScope.Core.1.0.0.nupkg | `54c1fe768c11b77c080841c7ef957d1ad2ee3e39f9e4f030f386b95064149a32` |
| `AvaScope.Bridge.1.0.0.nupkg` | https://github.com/RolandUI/AvaScope/releases/download/v1.0.0/AvaScope.Bridge.1.0.0.nupkg | `40db5f48f4853eb0c511b3eb424a375703132699fb0661e54f5a7b5f6cdc3aed` |
| `avascope-win-x64-framework-dependent.zip` | https://github.com/RolandUI/AvaScope/releases/download/v1.0.0/avascope-win-x64-framework-dependent.zip | `7e8770aa775d841df908cfebc8d6b4e01a3713615717f7eb91262dc502b6f60b` |
| `avascope-linux-x64-framework-dependent.zip` | https://github.com/RolandUI/AvaScope/releases/download/v1.0.0/avascope-linux-x64-framework-dependent.zip | `0d88212c2d98ddf9a911726c32c88daba2ec6b40dd88dbd98e3c6e3dbe88f72f` |
| `release-manifest.json` | https://github.com/RolandUI/AvaScope/releases/download/v1.0.0/release-manifest.json | `7bc760f7bac31ba7ec2ed008565fe25ba955782388481d1724e27d15d020935c` |

### v1.0.0 Explicit Deferrals

The final v1.0.0 non-blocking deferral list is recorded in [POST_1_0_BACKLOG.md](POST_1_0_BACKLOG.md).

- Remote inspection/control, no-code attach, process injection, CLR profiling, and private Avalonia designer APIs remain post-1.0 unless a separate security model is designed.
- Native IDE extensions can build on the stable CLI/MCP contracts after 1.0; they are not required for the stable release.
- Cloud-hosted visual regression dashboards remain post-1.0.
- Destructive runtime actions remain out of scope for the stable tool set.
- Automatic source editing remains post-1.0; source suggestions are advisory in v1.

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

- macOS release assets, signing, notarization, and native signed installers remain deferred until a validation surface exists.
- Remote runtime inspection remains out of scope; bridge transport stays opt-in and local-only.
- Private Avalonia runtime hooks, CLR injection, and production remote control remain out of scope.
- Persistent preview host processes are not guaranteed for `v0.2.0` unless their lifecycle and safety semantics are validated first.
