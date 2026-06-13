# AvaScope Upgrade And Compatibility

Use this guide when moving an Avalonia project, CLI workflow, or MCP client between AvaScope versions.

## Version Alignment

- Keep `AvaScope.Protocol`, `AvaScope.Core`, and `AvaScope.Bridge` on the same major version.
- Use the CLI and MCP executable ZIP from the same release as the bridge package whenever possible.
- Rebuild or re-extract executable ZIPs after upgrading packages; do not mix old `AvaScope.Mcp` or `AvaScope.PreviewHost` binaries with a newer CLI.
- Run `avascope doctor` after extracting a ZIP to verify co-located CLI, MCP, PreviewHost, and local store readiness.

## Client Compatibility Rules

- Major version `1` keeps the stable CLI command names, MCP tool names, protocol result shapes, exit-code classes, and artifact names documented in [STABLE_SURFACE.md](STABLE_SURFACE.md).
- Minor releases may add optional JSON fields, diagnostics, capability ids, CLI flags, MCP parameters, and report assets.
- Clients must ignore unknown JSON properties and unknown capability ids.
- Clients should call `capabilities` and gate optional workflows by capability id instead of guessing from package versions.
- Unsupported required capability ids fail with `capability_not_supported` and include `requestedCapabilities`, `unsupportedCapabilities`, `availableCapabilities`, `protocolVersion`, and `nextAction` details.

## Upgrading A Bridge-Enabled App

1. Update the app's `AvaScope.Bridge` package to the intended release version.
2. Rebuild the application.
3. Run the app with bridge activation explicitly enabled for the environment being tested.
4. Run `avascope diagnostics`, then attach with an explicit `--session`, `--process`, or `--manifest`.
5. Validate `list-top-levels`, `visual-tree`, `inspect-node`, `screenshot`, and `close-session` before using runtime mutation or evidence workflows.

Runtime mutations are temporary local overrides. Upgrading AvaScope does not migrate active mutation history; close sessions and reset active mutations before replacing bridge or CLI binaries.

## Upgrading CLI Or MCP Workflows

- Replace the entire extracted executable directory with the new release ZIP.
- Run `avascope capabilities` and verify every workflow-required capability before issuing newer commands.
- For MCP clients, reconnect the stdio server after replacing binaries; do not keep a long-running old MCP process alive during an upgrade.
- If a workflow reads report packs, prefer stable filenames such as `baseline-report.json`, `baseline-report.html`, `baseline-junit.xml`, and `baseline.sarif.json`; do not rely on generated absolute paths or timestamps.

## Validation Checklist

```powershell
avascope doctor
avascope capabilities --require protocol.capability_discovery,preview.axaml,preview.sessions,runtime.attach,runtime.session_lifecycle,baseline.single,reports.evidence_pack
avascope diagnostics --max-sessions 10
```

For source-tree validation after an upgrade:

```powershell
dotnet restore AvaScope.slnx
dotnet build AvaScope.slnx --no-restore -v:minimal
dotnet test AvaScope.slnx --no-build
powershell -NoProfile -ExecutionPolicy Bypass -File .\eng\create-local-release.ps1
```

If a bridge-enabled app reports `bridge_protocol_incompatible`, align the app package and CLI/MCP release to the same major version, then restart the app so it writes a fresh local bridge manifest.
