# AvaScope

Open inspection and preview tools for Avalonia.

AvaScope is a generic Avalonia inspection, preview, and automation stack for agents and local developer workflows. It targets Avalonia 12 and `net10.0`. It is not TradeR-specific.

## Current Capabilities

- Opt-in runtime bridge for Avalonia apps.
- Local bridge discovery through session manifests and named pipes.
- Runtime top-level listing, screenshots, bounded visual/logical trees, node search, and basic input.
- Isolated preview host process for `.axaml` rendering.
- MCP stdio server with structured tools.
- `avascope` CLI with preview and MCP handoff commands.

## Project Layout

- `src/AvaScope.Protocol`: transport-neutral DTOs and stable JSON contracts.
- `src/AvaScope.Core`: reusable session registry, local bridge client, and preview host client.
- `src/AvaScope.Bridge`: opt-in package loaded by Avalonia apps for runtime inspection.
- `src/AvaScope.PreviewHost`: child process that builds/loads views and renders previews.
- `src/AvaScope.Mcp`: stdio MCP adapter over Core.
- `src/AvaScope.Cli`: local `avascope` command.
- `tests/AvaScope.Tests`: protocol, core, MCP, bridge, preview host, and CLI tests.

## Build And Test

```powershell
dotnet restore AvaScope.slnx
dotnet build AvaScope.slnx
dotnet test AvaScope.slnx
```

Targeted checks:

```powershell
dotnet test AvaScope.slnx --no-build --filter Protocol
dotnet test AvaScope.slnx --no-build --filter Core
dotnet test AvaScope.slnx --no-build --filter Mcp
dotnet test AvaScope.slnx --no-build --filter Bridge
dotnet test AvaScope.slnx --no-build --filter FullyQualifiedName~PreviewHost
dotnet test AvaScope.slnx --no-build --filter FullyQualifiedName~Cli
```

Local package validation:

```powershell
dotnet build AvaScope.slnx -c Release
dotnet pack .\src\AvaScope.Protocol\AvaScope.Protocol.csproj -c Release --no-build --output .\artifacts\packages
dotnet pack .\src\AvaScope.Core\AvaScope.Core.csproj -c Release --no-build --output .\artifacts\packages
dotnet pack .\src\AvaScope.Bridge\AvaScope.Bridge.csproj -c Release --no-build --output .\artifacts\packages
```

The library package slice produces local NuGet packages for `AvaScope.Protocol`, `AvaScope.Core`, and `AvaScope.Bridge`. `AvaScope.Mcp`, `AvaScope.Cli`, and `AvaScope.PreviewHost` are explicitly not packable; executable distribution uses the local publish/ZIP workflow below.

Local executable package validation:

```powershell
dotnet build AvaScope.slnx -c Release
powershell -NoProfile -ExecutionPolicy Bypass -File .\eng\package-executables.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File .\eng\verify-artifacts.ps1
```

The executable package slice produces framework-dependent publish artifacts such as `artifacts\executables\avascope-win-x64-framework-dependent.zip` and `artifacts\executables\avascope-linux-x64-framework-dependent.zip`. Each ZIP contains the `avascope` CLI, `AvaScope.Mcp`, and `AvaScope.PreviewHost` in one directory so `avascope mcp` and preview rendering can find their co-located assemblies. The packages require a compatible local .NET runtime and do not publish to any feed.

`eng\verify-artifacts.ps1` writes `artifacts\release-manifest.json`, a local ignored JSON manifest with artifact names, relative paths, byte sizes, and SHA-256 hashes for the three NuGet packages plus executable ZIP artifacts.

The default executable package targets are `win-x64` and `linux-x64`. Pass `-RuntimeIdentifiers win-x64` or `-ExecutableRuntimeIdentifiers win-x64` to the package and verify scripts when validating a narrower local artifact set.

CI validation runs restore, Release build, Release test, local library pack, local executable package, and artifact verification commands in GitHub Actions on pushes and pull requests. It does not publish packages or require secrets.

## CLI

Build first, then run the CLI assembly from the build output:

```powershell
dotnet .\src\AvaScope.Cli\bin\Debug\net10.0\avascope.dll preview path\to\App.csproj --view Views\MainView.axaml --out .\preview.png --width 1440 --height 900 --dpi 96 --theme light
```

The command writes a structured JSON `ToolResult<PreviewResponse>` to stdout. On success, `value.filePath` points to the generated PNG.

Start the MCP server through the CLI:

```powershell
dotnet .\src\AvaScope.Cli\bin\Debug\net10.0\avascope.dll mcp
```

Attach to an active local bridge session:

```powershell
dotnet .\src\AvaScope.Cli\bin\Debug\net10.0\avascope.dll attach --process 1234
dotnet .\src\AvaScope.Cli\bin\Debug\net10.0\avascope.dll attach --session session-id
```

List top-level windows/views and capture a runtime screenshot from an active bridge session:

```powershell
dotnet .\src\AvaScope.Cli\bin\Debug\net10.0\avascope.dll list-top-levels --session session-id
dotnet .\src\AvaScope.Cli\bin\Debug\net10.0\avascope.dll screenshot --session session-id --top-level topLevel:1234 --out screenshot.png
```

Read bounded runtime trees from an active bridge session:

```powershell
dotnet .\src\AvaScope.Cli\bin\Debug\net10.0\avascope.dll visual-tree --session session-id --top-level topLevel:1234 --max-depth 4
dotnet .\src\AvaScope.Cli\bin\Debug\net10.0\avascope.dll logical-tree --session session-id --top-level topLevel:1234 --max-depth 4
```

Inspect a single runtime tree node by stable node id:

```powershell
dotnet .\src\AvaScope.Cli\bin\Debug\net10.0\avascope.dll inspect-node --session session-id --top-level topLevel:1234 --node visual:5678
dotnet .\src\AvaScope.Cli\bin\Debug\net10.0\avascope.dll inspect-node --session session-id --top-level topLevel:1234 --node logical:5678 --tree-kind logical
```

Find runtime tree nodes by type, name, automation id, or text:

```powershell
dotnet .\src\AvaScope.Cli\bin\Debug\net10.0\avascope.dll find-nodes --session session-id --top-level topLevel:1234 --type TextBlock --max-depth 6
dotnet .\src\AvaScope.Cli\bin\Debug\net10.0\avascope.dll find-nodes --session session-id --top-level topLevel:1234 --tree-kind logical --automation-id save-button --max-results 10
```

Send local-only runtime input to an active bridge session:

```powershell
dotnet .\src\AvaScope.Cli\bin\Debug\net10.0\avascope.dll input --session session-id --top-level topLevel:1234 --action click --x 120 --y 40
dotnet .\src\AvaScope.Cli\bin\Debug\net10.0\avascope.dll input --session session-id --top-level topLevel:1234 --action focus --target-node visual:5678
dotnet .\src\AvaScope.Cli\bin\Debug\net10.0\avascope.dll input --session session-id --top-level topLevel:1234 --action key_down --key Enter --modifiers Control+Shift --target-node visual:5678
```

Close an active local bridge session:

```powershell
dotnet .\src\AvaScope.Cli\bin\Debug\net10.0\avascope.dll close-session --session session-id
```

Read local bridge and preview-host diagnostics:

```powershell
dotnet .\src\AvaScope.Cli\bin\Debug\net10.0\avascope.dll diagnostics
dotnet .\src\AvaScope.Cli\bin\Debug\net10.0\avascope.dll diagnostics --session session-id --max-sessions 10
```

Check reload support for a runtime bridge session:

```powershell
dotnet .\src\AvaScope.Cli\bin\Debug\net10.0\avascope.dll reload --session session-id
```

Runtime bridge reload currently returns an explicit `runtime_reload_not_supported` result after verifying the local bridge session is active. CLI preview renders are one-shot and are not persisted across CLI processes.

After creating or extracting the local executable package, the same command shape can be run from the artifact directory:

```powershell
.\artifacts\executables\avascope\avascope.exe mcp
dotnet .\artifacts\executables\avascope\avascope.dll mcp
```

## MCP

The MCP server runs over stdio:

```powershell
dotnet .\src\AvaScope.Mcp\bin\Debug\net10.0\AvaScope.Mcp.dll
```

Implemented tools:

- `health`
- `list_sessions`
- `attach_to_app`
- `list_top_levels`
- `screenshot`
- `visual_tree`
- `logical_tree`
- `inspect_node`
- `find_nodes`
- `input`
- `close_session`
- `diagnostics`
- `preview_axaml`
- `create_preview_session`
- `list_preview_sessions`
- `close_preview_session`
- `reload`

Planned but not implemented yet: runtime hot reload, keyboard key events, focus targeting, drag/drop, cross-platform executable artifacts, self-contained artifacts, and publishing automation.

`diagnostics` reports AvaScope service metadata, local bridge manifest/pipe health, stale or invalid bridge manifests, and preview host readiness without building or loading user projects.

## Runtime Bridge

The bridge is opt-in. A host app must activate it explicitly and register top-levels that should be inspectable:

```csharp
using AvaScope.Bridge;

var runtime = AvaScopeBridge.Activate(new BridgeActivationOptions("My app"));
using var registration = runtime.RegisterTopLevel(window);
```

The bridge currently uses local session manifests and local named pipes. It does not expose unauthenticated remote inspection.

Runtime input support is intentionally narrow:

- `pointer_move` raises a routed Avalonia `PointerMovedEvent` on the hit-tested input target.
- `pointer_down` and `pointer_up` raise routed Avalonia pointer press/release events on the hit-tested input target.
- `click` supports Button targets in the current MVP.
- `focus` focuses an input element by visual/logical node id or hit-tested coordinates.
- `key_down` and `key_up` raise routed Avalonia key events on a focused input element or explicit target node id.
- `key_text` requires a focused `TextBox`.

## Preview Host

Preview rendering is isolated in `AvaScope.PreviewHost`, launched as a child process by Core, MCP, or CLI callers. The host:

- accepts a JSON `PreviewRequest`;
- optionally runs `dotnet build` for the requested `.csproj`;
- loads compiled Avalonia resource XAML through `avares://` when possible;
- loads compiled top-level `Application.Resources`, resource merged dictionaries, and `Application.Styles` from `App.axaml` when present;
- falls back to standalone runtime `.axaml` loading;
- renders through headless Skia;
- writes a PNG and structured JSON result.

Preview session tools store the original preview request plus the latest render result as Core metadata. MCP-backed preview session records are also persisted as JSON under the local AvaScope temp preview-session store so they can be restored after the MCP server process restarts. They do not keep user project code loaded inside MCP; each render still goes through `AvaScope.PreviewHost`.

`reload` re-runs stored preview-session requests through the isolated preview host and updates the existing session's latest render result. Runtime bridge session ids are health-checked locally and return `runtime_reload_not_supported`; AvaScope does not restart apps, inject code, or claim runtime hot reload. CLI preview renders remain one-shot unless a future CLI command creates durable preview session records explicitly.

Current preview limitations:

- no hot reload or persistent live preview host process yet;
- no full `App.axaml` orchestration yet; merged dictionaries, app styles, design data, and startup logic remain limited;
- no culture/design-data variants yet;
- build output probing assumes the default `bin\Debug\<tfm>\<ProjectName>.dll` shape.

## Safety Boundaries

- Bridge control is local-only and opt-in.
- Preview project build and view loading happen in `AvaScope.PreviewHost`, not inside the MCP server process.
- The MCP server is a thin adapter over Core.
- Tool results use structured JSON and file paths instead of unbounded payloads where practical.
