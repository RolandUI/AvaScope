# Point picking and temporary highlighting

`runtime.picking` exposes CLI `pick-node` / MCP `pick_node`, plus CLI/MCP
`highlight`. Both require the selected bridge session and observed top-level
generation. These tools never move the mouse, dispatch input or focus a control.

First call `pick_node` with only `target` to obtain `geometry`. Then supply `x`,
`y`, `coordinateSpace` and its `expectedGeometryRevision`:

```json
{
  "target": { "sessionId": "observed-session", "topLevelId": "observed-window", "topLevelGeneration": "observed-generation" },
  "x": 120,
  "y": 80,
  "coordinateSpace": "top_level_pixel",
  "expectedGeometryRevision": "64-character-revision-from-geometry-query",
  "maxPath": 12
}
```

`top_level_pixel` addresses the selected window's rendered screenshot grid;
`top_level_dip` addresses Avalonia client coordinates. `desktop` uses physical
desktop pixels on Windows/X11 and Cocoa desktop points on macOS. Headless and
unknown backends do not claim a desktop transform. The geometry response includes
client/pixel size, both scales, desktop origin/bounds and timestamp. Movement,
resize, scale, visibility or layout changes reject stale geometry. Changes inside
an old screenshot are not detected by this geometry revision: refresh the image
when its content may have changed. The hit result describes the **current** tree.

The first `hitPath` entry is the Avalonia input hit, followed by its visual
ancestors. Each carries a current generation-pinned target, public identity/text,
bounds and input state. A template leaf may be the hit while its Button ancestor
is the useful action target; no coordinate guess is converted into a click.
Clipped/non-hit-testable elements are excluded by Avalonia's public hit testing.
Outside/no-hit/policy-excluded results contain no invented target.

Modal and overlapping **registered** top-levels are reported separately. Their
overlap is not proof of native z-order. An open native popup has a separate hit
root; an owner-window result never claims to hit that popup. Register/select a
popup root explicitly to query it. Overlay popups participate in the selected
root's hit test. Unregistered/native/foreign-window occlusion remains unverified;
use separately authorized [paired screen evidence](SCREEN_EVIDENCE.md) when needed.
No global desktop selector, unrelated process discovery or input hook is installed.

## Highlighting

Pass a pinned **visual node** target from inspection/search/picking:

```json
{
  "target": { "sessionId": "observed-session", "topLevelId": "observed-window", "treeKind": "visual", "nodeId": "observed-node", "topLevelGeneration": "observed-generation", "nodeGeneration": "observed-node-generation" },
  "action": "show",
  "lifetimeMs": 1500,
  "color": "#FFB000"
}
```

`show` installs an AvaScope-owned border in the existing public Avalonia adorner
layer. It does not replace the app's focus adorner, set target properties or reparent
application content. The border is unfocusable and not hit-testable; public adorner
tracking follows the target's transform/clipping. `active` means the debug adorner
is registered, not that the OS has presented or left the window unobscured.
Missing adorner layers return an explicit unsupported result.

`inspect` reports the active highlight and current bounds. `clear` removes it.
One highlight per selected top-level is supported; another `show` replaces it.
There are at most eight per session. Lifetime is 100–5000 ms and expiry is checked
on the UI dispatcher; a blocked UI cannot repaint/remove anything until it resumes.
Detach, changed target context, unregister, close, session shutdown and failed
installation also remove subscriptions/timers/owned visuals.

**Ordinary screenshots clear debug highlights**, including observation screenshots
and paired/native captures. They are not persistent mutations or test evidence.
Native capture waits for a frame after removal before reading desktop pixels.
The response explicitly reports `clearedByScreenshot: true`; show it again if a
human still needs the overlay after a capture. This avoids racing hide/restore
cycles that could reintroduce an overlay into later frames.

`show`/`clear` respect the session control lease; geometry/picking/inspect remain
read-only. Evidence policy authorizes inspection and the current session/PID,
redacts scalar text before output, and refuses protected nodes/ancestors. No
highlight or hit path is returned for an excluded target.

Bounds: 64 KiB request, 3-second cooperative dispatcher deadline, up to 32 returned
ancestors/64 checked ancestors, 32 related top-levels/16 returned references, 4096
visuals for popup inspection and 256 characters per text field. Truncation and
unverified desktop facts remain explicit.

Implementation uses Avalonia 12 public [AdornerLayer](https://github.com/AvaloniaUI/Avalonia/blob/12.1.0/src/Avalonia.Controls/Primitives/AdornerLayer.cs)
and input hit testing; no custom GUI inspector or foreign framework adapter is required.
