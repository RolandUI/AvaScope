# AvaScope 1.5.1-rc.1

Release candidate for native macOS Retina verification on .NET 10 / Avalonia
12.1.x. This is a prerelease; the latest stable version remains 1.5.0.

## Changes since 1.5.0

- Stabilization fixes for target identity, desired-state actions, text editing,
  mutations, diagnostics, capture and agent workflow reliability, with expanded
  real-application fixtures and regression coverage.
- Larger bounded paired capture supports the requested 1920×1080 DIP viewport at
  2× scale (#161). Screenshot/text-fidelity mitigations for #157 require the
  owner's original complex Retina view to establish acceptance.
- Animation inspection now triggers and measures real playback in one view, with
  actual capture intervals, timing tolerances and motion/state/stability checks.
  Late or uncertain measurements are inconclusive (#201). This is not virtual
  seeking; legacy exact-offset animation baselines are refused.
- Standalone provider validation accepts RC/build suffixes while retaining exact
  version/hash pins and numeric assembly checks (#232).

## Install the matching Mac components

Download assets specifically from **v1.5.1-rc.1**, not the `latest` URL:

- Apple Silicon: `avascope-osx-arm64-installer` or
  `avascope-osx-arm64-framework-dependent.zip`.
- Intel: `avascope-osx-x64-installer` or
  `avascope-osx-x64-framework-dependent.zip`.
- External/standalone bridge: `avascope-bridge-provider.zip` and its `.sha256`.
- Package integration: `AvaScope.Bridge` **1.5.1-rc.1**; rebuild the application.

Verify downloaded files against `release-manifest.json`. Installers are unsigned
and unnotarized; follow the existing [macOS installation instructions](https://github.com/RolandUI/AvaScope#install-from-a-release)
and [upgrade guide](https://github.com/RolandUI/AvaScope/blob/v1.5.1-rc.1/docs/UPGRADE.md).

Stop the existing diagnostic app/MCP processes before upgrading. Replace the whole
CLI/MCP directory and, for standalone loading, the whole provider directory.
Update any provider version/hash pins and restart the host. Updating only the CLI
does not update the bridge running inside the application.

If your host copied or linked `OptionalProviderLoader.cs` from 1.5.0, update that
file from this RC's provider ZIP and rebuild the host before loading the RC.
The old loader rejects prerelease version identifiers even with a new provider.

```bash
chmod +x avascope-osx-arm64-installer
./avascope-osx-arm64-installer
~/.local/bin/avascope --version
~/.local/bin/avascope doctor
```

Use `osx-x64` for Intel. The version command must print `1.5.1-rc.1`; the external
provider manifest must report the same version. Reconnect the MCP client.

## Mac acceptance checklist

1. Use the original complex view on a real Retina desktop; record macOS,
   architecture, Avalonia patch, actual render scale and top-level DIP dimensions.
2. **[#157](https://github.com/RolandUI/AvaScope/issues/157):** compare the normal
   rendered screenshot with the actual visible app, focusing on text size,
   wrapping and layout. Use a native screenshot/paired capture where available;
   report the actual capture route and any OS permission limitation.
3. **[#161](https://github.com/RolandUI/AvaScope/issues/161):** at an actual
   1920×1080 DIP viewport and scale 2, request paired capture without shrinking
   the window. Check the expected 3840×2160 rendered image and that native capture
   succeeds when the desktop, host authorization and OS permissions permit it.
4. Attach the request, response, expected/actual image and component versions to
   the relevant issue. Neither Retina issue is closed until this verification.

## Validation and remaining gaps

The stabilization source passed all six jobs of
[CI 36386091375](https://github.com/RolandUI/AvaScope/actions/runs/36386091375):
Windows 1177 passed/eight skipped, macOS 1174/ten, Linux 33/zero. Nine scripted
native lifecycle cases passed at scale 1; they do not establish Retina fidelity.
The RC release workflow separately rebuilds and validates its exact versioned
packages, provider, executable ZIPs and installers before publication.

Remaining stabilization gaps include the two Retina acceptance reports above and
eleven unresolved original timing/build/IPC/lock causes
(#171, #181, #182, #191, #203, #204, #208, #213, #218, #226, #231). Later passing
tests do not prove those original causes fixed. The broader #174 campaign and
stable 1.5.1 release acceptance remain incomplete.
