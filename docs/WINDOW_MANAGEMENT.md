# Session-owned window management

CLI/MCP `window` exposes `runtime.windows`. It inspects one explicit registered
Avalonia desktop `Window` and can request activation, front placement, minimize,
maximize, restore, move or resize through public Avalonia APIs. It does not
enumerate unrelated processes, inject global input, expose external window titles
or close windows. Closing a bridge session remains a different operation.

Inspect first:

```json
{
  "target": { "sessionId": "copy-the-session-id", "topLevelId": "copy-the-window-id" },
  "action": "inspect"
}
```

The result's `after` snapshot includes the current generation-pinned `target`,
`revision`, framework and native state, activation/native focus, desktop position,
client size, render/desktop scaling, registered owner/modal blocker, monitor work
areas and `availableActions`. Copy that target and revision into a mutation:

```json
{
  "target": { "sessionId": "copy-session", "topLevelId": "copy-window",
    "topLevelGeneration": "copy-observed-generation" },
  "action": "resize",
  "expectedRevision": "copy-the-observed-64-character-revision",
  "clientSize": { "width": 900, "height": 650 },
  "timeoutMs": 3000
}
```

Actions:

- `activate` requests `Window.Activate()` and verifies framework activation plus
  native focus on the exact owned window. OS foreground restrictions can refuse it.
- `bring_to_front` uses activation and additionally verifies that the selected
  window is frontmost among the registered visible application windows. This does
  not prove the absence of unrelated occluders or override another app's topmost
  window. No permanent `Topmost` property is set.
- `minimize`, `maximize`, `restore` set public `WindowState` and compare its result
  with native owned-window state. Restore requests `Normal`; it does not promise
  the previous maximized state. Fullscreen transitions are outside this contract.
- `move` sets public `Window.Position`. It requires a normal window and a position
  within a current monitor work area. Deliberately hiding a window off-screen is
  unsupported. Restore a minimized/maximized window explicitly first.
- `resize` sets current `Width`/`Height` values, preserving bindings, and verifies
  public platform client size within one DIP. It requires a normal, resizable,
  manually sized window; app min/max constraints apply. A width callback which
  changes identity, ownership, modality or resize constraints prevents further
  property dispatch. Partial effects are reported and are not rolled back.

For move, choose either integral `position: { "x": -1200, "y": 80 }` in the
snapshot's desktop units, or monitor-local logical offsets:

```json
{
  "x": 40,
  "y": 45,
  "coordinateSpace": "monitor_dip",
  "monitorId": "copy-an-observed-monitor-id"
}
```

`monitor_dip` is relative to that monitor's **work-area origin**, multiplied by
its desktop scale, then rounded to integral desktop coordinates. Negative desktop
origins and different monitor scales are preserved. Monitor ids represent the
observed layout, not persistent hardware identities. A changed window/monitor
revision requires a fresh inspection before dispatch.

Coordinate meanings are explicit:

| Value | Windows / X11 | macOS |
| --- | --- | --- |
| Desktop position / monitor bounds | Physical desktop pixels | Cocoa desktop points |
| `clientSize` | DIPs | DIPs |
| `desktopScale` / `desktopScaling` | Desktop units per DIP | Avalonia desktop scale, normally 1 |
| `renderScaling` | Render pixels per DIP | Retina render scale, separate from desktop scale |
| Monitor `physicalPixelBounds` | Same physical bounds | Unavailable (`null`) |

The coordinate contract follows public
[Window position](https://github.com/AvaloniaUI/Avalonia/blob/12.1.0/src/Avalonia.Controls/Window.cs),
[WindowBase desktop scaling](https://github.com/AvaloniaUI/Avalonia/blob/12.1.0/src/Avalonia.Controls/WindowBase.cs)
and [screen APIs](https://github.com/AvaloniaUI/Avalonia/blob/12.1.0/src/Avalonia.Controls/Screens.cs).
The native macOS backend publishes NSScreen frame/work-area points and a desktop
scale of one; it must not be treated as physical Retina pixels. The tool does not
invent a global physical origin by multiplying mixed-DPI desktop coordinates.

Native verification observes only the selected process-owned handle: Win32
`IsIconic`/`IsZoomed`, X11 `WM_STATE`/`_NET_WM_STATE`, and AppKit
`isMiniaturized`/`isZoomed`, alongside existing owned native-focus observation.
Headless/unknown backends expose basic inspection and explicit unsupported native
actions. Missing window-manager metadata is unavailable rather than guessed.

Mutations require the existing session control lease when one is held. An
evidence policy must authorize inspection, the selected session/process and the
specific `allowedWindowActions` entry. Window exclusions protect metadata before
capture; title/monitor-name redaction precedes truncation. Modal children block
operations on their owner, including unregistered modal children; select the
authorized dialog explicitly. The bridge rechecks window generation, handle,
data context and modality before each dispatch and while observing completion.

The response preserves `before`, the last available `after`, attempted
`dispatchedOperations`, actual `avalonia_public_window_api` provenance and the
verification basis. `executed` requires two matching observations after dispatch;
`already_satisfied` sends no operation. Refusal, timeout or a lost target is
`rejected`/`partial`, and CLI/MCP report `success: false` while retaining available
evidence. A lost transport response has unknown dispatch; inspect before issuing
a new intent. The client never automatically retries a window mutation.

Requests are limited to 64 KiB, monitor snapshots to 16 entries and owned z-order
inspection to 32 registered windows. Client dimensions accept 32..16384 DIPs.
The observation budget is 100..3000 ms (default 1500); native/public callbacks
remain cooperative and cannot be forcibly interrupted. No retained window-action
ledger, listener, overlay, screenshot or file is created by this tool.

Use `avascope window --request window.json --manifest-dir <dir>` or MCP `window`
with the same request. `eng/test-native-input.ps1` includes the actual
Windows/X11/macOS window-management fixture, while headless tests cover refusals,
modal relationships, stale targets, policy and mixed-DPI coordinate conversion.
