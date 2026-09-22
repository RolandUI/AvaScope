# Owned Linux X11 test environments

Add `x11Environment` to an existing `run-scenario` request or a named
[agent test profile](AGENT_TEST_PROFILES.md). It prepares the environment before
the explicit host launch and releases it after the owned application exits.
CLI and MCP use the same scenario engine.

```json
{
  "launch": { "command": "dotnet", "argumentList": ["/work/MyApp.dll"] },
  "terminateLaunchedProcess": true,
  "x11Environment": {
    "mode": "managed",
    "width": 1280,
    "height": 900,
    "dpi": 96,
    "windowManager": true,
    "sessionBus": true,
    "timeoutMs": 10000
  },
  "steps": [{ "action": "screenshot" }]
}
```

On Ubuntu the prerequisites are `xvfb`, `x11-utils`, and optionally `openbox`
and `dbus`. AvaScope reports missing commands; it does not install packages or
change the user's desktop. The application still needs its normal Avalonia
libraries/fonts and explicit bridge activation.

Managed mode allocates a display with Xvfb's `-displayfd`, forces `-nolisten tcp`
and uses a random per-run MIT-MAGIC-COOKIE-1 authorization file with mode 0600
inside a private 0700 runtime directory. Linux clients connect through the abstract
Unix socket (`-nolisten unix -listen local`), so a read-only WSLg socket directory
does not need to be changed. Evidence records `socketTransport: abstract_unix`.
It verifies real `xdpyinfo` access.
Optional Openbox uses a generated minimal configuration and waits for its EWMH
root property. Optional D-Bus uses an owned Unix socket and verifies ListNames;
its configuration does not import host service directories or automatically
start unrelated desktop services. A portal/dialog service is a separate explicit
prerequisite, not implied by having a session bus.

The selected host receives the exact DISPLAY, XAUTHORITY and XDG_RUNTIME_DIR,
with WAYLAND_DISPLAY cleared. Managed runs never inherit the user's session bus.
No global environment variable is modified. Concurrent runs have distinct display
allocations, authorization cookies and runtime directories. Named profiles also
allocate separate evidence directories automatically; explicit scenario requests
must select distinct output directories for concurrent runs.

To reuse an authorized display, select `{"mode":"existing","display":":0",
"xauthority":"/absolute/authorization-file"}` instead. This mode probes access
without owning the display or starting a window manager/session bus. Disposal
does not stop that desktop. Only local `:number` displays are accepted.

Environment evidence records helper PIDs/start times, requested geometry/DPI,
actual display allocation, logs, local transport configuration and cleanup status.
Helper logs retain at most 64 KiB of text each and pass through the scenario's
evidence redaction before persistence. Cookies are never included in evidence.
Managed resources are disposed on startup failure, launch interruption, workflow
failure, cancellation and normal completion. Unexpected helper exit cancels the
owned run. Cleanup first requests graceful termination of the exact helper
identity, then uses a bounded forced stop if needed. Stale Xvfb socket/lock removal
requires the lock still to identify that exited owned helper. Abstract sockets
are released by the kernel; filesystem sockets from existing desktops are never
removed.

Xvfb exercises native X11 windowing with a virtual framebuffer. It does not by
itself prove native OS input was used: operation and screenshot route evidence
must be checked independently. Windows/macOS requests return
`x11_environment_unsupported`; select their own native profiles.

Validation commands on Linux:

```sh
dotnet test AvaScope.slnx -c Release --no-build --filter FullyQualifiedName~X11TestEnvironmentTests
pwsh -NoProfile -File eng/test-managed-x11.ps1
```

The tests cover parallel displays, authorization denial, no TCP listener,
existing-display preservation, startup/launch cancellation and helper crashes.
The script runs a bridge-free host project with the external provider through
CLI/MCP, then verifies dependency failure and owned cleanup.

References: [Xserver display allocation and transports](https://www.x.org/releases/current/doc/man/man1/Xserver.1.xhtml),
[Xvfb options](https://www.x.org/releases/current/doc/man/man1/Xvfb.1.xhtml),
[D-Bus daemon configuration](https://dbus.freedesktop.org/doc/dbus-daemon.1.html).
