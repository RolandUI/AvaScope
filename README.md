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

The first package slice produces local NuGet packages for `AvaScope.Protocol`, `AvaScope.Core`, and `AvaScope.Bridge`. `AvaScope.Mcp`, `AvaScope.Cli`, and `AvaScope.PreviewHost` are explicitly not packable yet; executable/tool packaging remains a later release workflow.

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
- `find_nodes`
- `input`
- `close_session`
- `diagnostics`
- `preview_axaml`
- `create_preview_session`
- `list_preview_sessions`
- `close_preview_session`
- `reload`

Planned but not implemented yet: runtime hot reload, keyboard key events, focus targeting, drag/drop, executable/tool packaging, and CI.

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
- `key_text` requires a focused `TextBox`.

## Preview Host

Preview rendering is isolated in `AvaScope.PreviewHost`, launched as a child process by Core, MCP, or CLI callers. The host:

- accepts a JSON `PreviewRequest`;
- optionally runs `dotnet build` for the requested `.csproj`;
- loads compiled Avalonia resource XAML through `avares://` when possible;
- loads compiled top-level `Application.Resources` entries from `App.axaml` when present;
- falls back to standalone runtime `.axaml` loading;
- renders through headless Skia;
- writes a PNG and structured JSON result.

Preview session tools store the original preview request plus the latest render result as Core metadata. They do not keep user project code loaded inside MCP; each render still goes through `AvaScope.PreviewHost`.

`reload` re-runs stored preview-session requests through the isolated preview host and updates the existing session's latest render result. Runtime bridge session ids are health-checked locally and return `runtime_reload_not_supported`; AvaScope does not restart apps, inject code, or claim runtime hot reload.

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
