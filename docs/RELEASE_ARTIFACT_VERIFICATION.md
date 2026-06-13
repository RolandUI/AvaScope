# AvaScope v1.0.0 Release Artifact Verification

This ledger records the `R1.0.0-M4 Release Artifact And Package Verification` slice for GitHub issue #37.

## Scope

- Validation date: `2026-06-13`.
- Target release version: `1.0.0`.
- Repository committed version during the slice: `0.9.0`.
- Validation method: temporarily set `Directory.Build.props` `<Version>` to `1.0.0`, run local release and publish dry-runs, then restore `Directory.Build.props` to `0.9.0` before commit.
- Remote CI policy: normal development CI was intentionally manual-only for this slice because of GitHub Actions quota pressure. All #37 gates below are local validations. The final publish remains the GitHub `Release` workflow.

## Default Artifact Set

The default `v1.0.0` release asset set is framework-dependent:

- `AvaScope.Protocol.1.0.0.nupkg`
- `AvaScope.Core.1.0.0.nupkg`
- `AvaScope.Bridge.1.0.0.nupkg`
- `avascope-win-x64-framework-dependent.zip`
- `avascope-linux-x64-framework-dependent.zip`
- `release-manifest.json`

The framework-dependent ZIPs are the default because they keep the public release smaller, preserve the co-located CLI/MCP/PreviewHost layout, and rely on the user's installed compatible .NET runtime. Self-contained ZIPs remain an explicit opt-in lane for environments that need bundled runtime bits.

## Local Release Gate

Command:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\eng\create-local-release.ps1
```

Result:

- Release restore passed.
- Release build passed with 0 warnings and 0 errors.
- Release tests passed: 312 passed, 0 failed.
- NuGet package creation passed for `AvaScope.Protocol`, `AvaScope.Core`, and `AvaScope.Bridge`.
- Executable packaging passed for `win-x64` and `linux-x64` framework-dependent ZIPs.
- `eng\verify-artifacts.ps1` verified 5 release artifacts and wrote `artifacts\release-manifest.json`.
- Packaged Windows `doctor` smoke passed.
- Packaged Windows sample preview smoke passed and wrote `artifacts\samples\getting-started-preview-release.png`.

After the MCP server-info fix, the full release gate was rerun and passed. A final `-SkipTests` release artifact regeneration restored the default framework-dependent artifact set after the opt-in self-contained validation lane.

## Publish Dry-Runs

NuGet dry-run:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\eng\publish-nuget.ps1 -DryRun
```

Result:

- Validated `Version: 1.0.0`.
- Validated package order: Protocol, Core, Bridge.
- Did not push packages.

GitHub Release dry-run:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\eng\publish-github-release.ps1 -Tag v1.0.0 -DryRun
```

Result:

- Validated tag/version alignment: `v1.0.0` and `1.0.0`.
- Validated required GitHub Release assets: three `.nupkg` packages, two framework-dependent executable ZIPs, and `release-manifest.json`.
- Did not create or update a GitHub Release.

## Artifact Inspection

Local inspection verified:

- Manifest `schemaVersion` is `1`.
- Manifest `product` is `AvaScope`.
- Manifest `version` is `1.0.0`.
- Manifest `executablePackageKind` is `framework-dependent`.
- Manifest artifact set exactly matches the default release asset set.
- Every manifest artifact exists, has the recorded byte size, and has a lowercase 64-character SHA-256 hash.
- Every recorded SHA-256 hash matches a freshly computed file hash.
- Every NuGet package contains a `.nuspec` with the expected id and version `1.0.0`, non-empty description, non-empty tags, and a `lib/net10.0` assembly.
- Both executable ZIPs contain `avascope.dll`, `AvaScope.Mcp.dll`, `AvaScope.PreviewHost.dll`, `AvaScope.Core.dll`, `AvaScope.Protocol.dll`, `avascope.runtimeconfig.json`, and `avascope.deps.json`.
- The Windows ZIP contains `avascope.exe`.
- The Linux ZIP does not contain `avascope.exe`.

## Packaged CLI And MCP Smoke

Packaged CLI capability gate:

```powershell
.\artifacts\executables\avascope-win-x64-framework-dependent\avascope.exe capabilities --require protocol.capability_discovery,preview.axaml,preview.sessions,runtime.attach,runtime.session_lifecycle,baseline.single,reports.evidence_pack
```

Result:

- Returned `success: true`.
- Reported `serviceName: avascope`.
- Reported protocol version `1.0`.

Packaged MCP stdio smoke:

- Started `artifacts\executables\avascope-win-x64-framework-dependent\AvaScope.Mcp.dll` over stdio.
- Sent `initialize`.
- Verified `serverInfo.name` is `avascope`.
- Sent `notifications/initialized`.
- Sent `tools/list`.
- Verified 30 MCP tools, including `health`, `capabilities`, `list_sessions`, `preview_axaml`, `baseline_check`, `mutate_node`, and `cleanup_bridge_sessions`.

## Self-Contained Lane

Self-contained executable artifacts are not the default `v1.0.0` release asset set, but the opt-in lane was validated locally for `win-x64`.

Commands:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\eng\create-local-release.ps1 -RuntimeIdentifiers win-x64 -ExecutablePackageKind self-contained -SkipTests -SkipSampleSmoke
powershell -NoProfile -ExecutionPolicy Bypass -File .\eng\publish-github-release.ps1 -Tag v1.0.0 -ExecutableRuntimeIdentifiers win-x64 -ExecutablePackageKind self-contained -DryRun
```

Result:

- Created `avascope-win-x64-self-contained.zip`.
- Manifest verification covered 4 artifacts: three NuGet packages plus the self-contained Windows ZIP.
- Packaged self-contained `doctor` smoke passed.
- GitHub Release dry-run validated the self-contained asset set without publishing.

The final local artifact state was restored to framework-dependent with:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\eng\create-local-release.ps1 -SkipTests
powershell -NoProfile -ExecutionPolicy Bypass -File .\eng\publish-github-release.ps1 -Tag v1.0.0 -DryRun
```

## Non-Obvious Decision

The MCP SDK default `serverInfo.name` was `AvaScope.Mcp`. The slice changed the MCP server to explicitly publish `serverInfo.name = "avascope"` through `McpServerOptions.ServerInfo`, aligning the packaged MCP handshake with the product and protocol service name before the stable release.
