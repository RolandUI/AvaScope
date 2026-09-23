# Controlled Wayland testing

Use `waylandEnvironment` in an explicit `run-scenario` / `run_scenario` request or
[named profile](AGENT_TEST_PROFILES.md). It starts **Weston 13.x headless**, with
Pixman software rendering and the kiosk shell. No compositor code is included in
AvaScope. Weston was selected because its maintained headless backend runs without
a display server, DRM device, root, seat daemon, or access to the user's desktop.
Nested Weston would inherit the parent desktop's behavior; wlroots compositors
introduce a different set of backend/input controls. They are not this profile.

```json
{
  "launch": { "command": "dotnet", "argumentList": ["/work/Host.dll", "--wayland"] },
  "terminateLaunchedProcess": true,
  "waylandEnvironment": {
    "lane": "native_wayland", "width": 1280, "height": 900, "scale": 1,
    "keyboardLayout": "us", "keyboardVariant": "", "timeoutMs": 10000
  },
  "steps": [{ "action": "screenshot", "captureAfterRender": true }]
}
```

The host must explicitly use the experimental `Avalonia.Wayland` 12.1.x package
and `AppBuilder.UsePlatformDetect().UseWayland()` (desktop defaults also configure
rendering and text shaping; `UseWayland` replaces the windowing backend). The sample enables this only when built with
`EnableWaylandFixture=true` and started with `--wayland`; its optional provider
remains independently guarded by `EnableUiInspection=true`. It has no AvaScope
PackageReference. Environment variables alone do not switch Avalonia's backend.
The runner rejects a host reporting X11/headless/unknown instead of Wayland.

Ubuntu 24.04 prerequisites: `weston`, `wayland-utils`, `xkb-data`,
`libegl-mesa0`, `libgl1-mesa-dri`, normal Avalonia native libraries and fonts.
Other Weston major versions are explicitly unsupported until validated.
AvaScope reports missing prerequisites without installing packages or modifying
desktop settings. Non-Linux hosts return `wayland_environment_unsupported`.

Each run gets a random 0700 runtime directory and private Unix socket. Only the
launched process receives its environment. DISPLAY is cleared, the session bus
points to a nonexistent owned path, and software EGL is selected. Inherited
`WAYLAND_SOCKET` descriptors are refused. Weston is explicitly headless, never
DRM/nested/RDP/VNC, and its generated config cannot activate XWayland or a desktop
shell client. There is no TCP listener. Concurrent runs never share sockets.

Readiness uses real `wayland-info` output: xdg shell/shm availability, logical
output size, physical pixel size and scale. Width/height are logical; pixels are
width/height times integer scale. Limit: 16 Mi output pixels. Evidence includes
compositor version, observed geometry, selected keyboard configuration, app
backend, helper identity, bounded sanitized logs, and cleanup results. App client
geometry is reported independently: do not infer its size from the output size.
The kiosk shell does not establish arbitrary desktop positioning coverage.

The headless compositor has **no native input seat**. XKB layout/variant are fixed
configuration, not evidence that a physical keyboard or an IME was exercised.
Use public automation providers and explicitly synthetic/routed input; inspect
each operation's provenance. RenderTargetBitmap screenshots validate Avalonia
rendering. Native OS input, desktop capture, window management and native
accessibility audit are explicitly unsupported on this backend. No silent
fallback turns those operations into claimed native coverage.

`lane: xwayland` returns `wayland_xwayland_unsupported` before launch. XWayland is
optional and separate: Weston's lazy X server startup, global X socket lifecycle
and input ownership need their own validated configuration. This release does
not infer XWayland support from Xvfb or native Wayland results. Use managed X11
for the existing native X11 lane; no compatibility with another test framework
is involved.

Cancellation, launch/workflow failure, helper crash and normal completion stop
the exact owned app before Weston, then remove only the verified private runtime
directory. Process/start-time and resource markers enter the existing run recovery
journal. If app termination cannot be confirmed, retain its desktop for explicit
recovery. An unrelated/changed runtime path is retained, never blindly deleted.

Validation: `WaylandTestEnvironmentTests` covers isolation, protocol geometry,
scale, failure/cancellation/crash cleanup and invalid/unsupported profiles.
`pwsh -File eng/test-managed-wayland.ps1` builds the external-provider sample and
runs CLI/MCP at 1x/2x, text interaction, screenshots, intentional workflow
failure, wrong-backend refusal, missing prerequisites and cleanup. Pass
`-CliAssembly` / `-McpAssembly` for packaged binaries.

References: [Weston backends/renderers](https://wayland.pages.freedesktop.org/weston/toc/running-weston.html),
[Avalonia Wayland](https://docs.avaloniaui.net/platform-specific-guides/linux).
Weston 13 command-line options and Wayland globals are checked against the actual
installed executable as part of validation, not assumed from newer documentation.
