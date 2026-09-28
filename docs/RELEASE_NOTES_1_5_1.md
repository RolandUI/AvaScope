# AvaScope 1.5.1

Stable release of the implementation tested in **1.5.1-rc.1**, targeting .NET 10
and Avalonia 12.1.x. The owner tested the RC on their Mac, reported improvement
of the earlier Mac bugs, and approved stable publication. No additional runtime
changes were introduced after that RC.

## Changes since 1.5.0

- Corrected target identity and exact AutomationID matching, desired-state
  operations, revision-safe text editing and structured MCP failures.
- Improved runtime capture/text rendering and bounded paired capture for a
  1920×1080 DIP viewport at 2×. The Mac reports
  [#157](https://github.com/RolandUI/AvaScope/issues/157) and
  [#161](https://github.com/RolandUI/AvaScope/issues/161) are accepted by the owner
  after their RC test.
- Stabilized diagnostics, mutations, input, workflow recording/recovery and
  agent control paths, with expanded Direct/standalone QA scenes and regressions.
- Animation validation can trigger real playback and measure capture intervals,
  motion, state and stability with explicit timing tolerances. Uncertain timing
  is inconclusive; this does not provide virtual-clock seeking. Legacy
  exact-offset animation baselines are refused.
- Standalone provider validation supports prerelease/build suffixes while
  preserving exact version/hash pins and numeric assembly identity checks.

## Upgrade

Use matching **1.5.1** CLI/MCP and bridge components. Stop diagnostic app/MCP
processes before replacing complete installation/provider directories, update
any provider version/hash pins, and restart the host and MCP client. Updating
only the CLI does not replace the bridge already running inside an application.

- Package integration: update `AvaScope.Bridge` to **1.5.1** and rebuild the app.
- Standalone integration: install `avascope-bridge-provider.zip`. When upgrading
  from 1.5.0, also update the copied `OptionalProviderLoader.cs` and rebuild the
  host. Hosts already rebuilt with the RC loader do not need another loader fix.
- Mac: use `avascope-osx-arm64-installer` for Apple Silicon or
  `avascope-osx-x64-installer` for Intel. Corresponding framework-dependent ZIPs
  are also available. Installers remain unsigned and unnotarized; see the
  [installation instructions](https://github.com/RolandUI/AvaScope#install-from-a-release).
- Verify downloads against `release-manifest.json`, then check
  `avascope --version` returns `1.5.1` and run `avascope doctor`.

See the [upgrade guide](https://github.com/RolandUI/AvaScope/blob/v1.5.1/docs/UPGRADE.md).

## Validation and known follow-up

The stabilization baseline passed all six jobs of
[CI 36386091375](https://github.com/RolandUI/AvaScope/actions/runs/36386091375).
The exact RC passed
[Release 36397875663](https://github.com/RolandUI/AvaScope/actions/runs/36397875663):
1189 tests passed, eight skipped, zero failed, with successful installer,
package/provider/manifest, preview and publication checks. Stable 1.5.1 is rebuilt
and passes the version-specific Release gate before publication.

Mac acceptance is the owner's report. Detailed native captures, geometry and
environment versions were not supplied; no additional agent-run Retina coverage
or proof of the original rendering cause is claimed.

Eleven intermittent timing/build/IPC/file-lock investigations remain open:
#171, #181, #182, #191, #203, #204, #208, #213, #218, #226, #231. Later passing
tests do not establish their original causes as fixed. Further reusable native
evidence and comprehensive coverage work remains tracked in #164/#166/#174.
These are explicit follow-up items outside the owner-approved stable scope.
