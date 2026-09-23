# Keyboard focus and navigation evidence

MCP `inspect_focus` and CLI `avascope inspect-focus --request focus.json` observe
the selected window without focusing a control, activating a window or pressing
keys. Discover `runtime.focus` before use:

```json
{"sessionId":"<session>","topLevelId":"<window>","maxNodes":1024,"maxCandidates":24}
```

The result separates these facts:

- `focused` is Avalonia's current keyboard focus with its actual registered
  window and generation. `focusStatus` distinguishes the selected window, another
  registered window, absent focus, an unregistered scope and policy exclusion.
- `frameworkWindowActive` is Avalonia's window activation flag. It does not prove
  native keyboard delivery. `nativeFocus` separately compares the Win32 foreground
  HWND, the X11 input-focus XID, or the owned AppKit key window.
- `scopeAncestors` reports public focus scopes, tab modes, indices, enabled/visible
  state and tab-stop flags. Cyclic/contained navigation and disabled descendants
  are evidence about constraints, not proof that a user is trapped.
- `predictions` uses the public focus manager for next/previous candidates from
  the current focus or an optional generation-bearing `target`. Predictions are
  never presented as observed key routing. A custom keyboard-navigation handler,
  modal blocker or incomplete scan yields explicit uncertainty.
  A public API candidate that is disabled, hidden or not a tab stop is marked
  `unavailable_candidate`; the tool does not invent an alternative navigation path.
- `candidates` lists bounded focusable controls, including disabled ones and their
  reasons. Labels use automation names/control names, never editable text values.

Per-scope remembered logical focus has no supported public read API and is
reported as unavailable. Framework keyboard focus and public scope metadata are
still available. Native child controls, platform IME routing, external menus and
handlers which consume a key remain outside the framework observation.

On X11, `focused` means an exact match with the selected owned XID. A different
XID is reported as `different_native_target`; it could be a native child, so
AvaScope does not infer focus on an unrelated process or examine that process.
X11 `None`/`PointerRoot` and a null Win32 foreground handle are `unknown`.
Headless, Wayland and unsupported top-level kinds report unsupported/unknown
native evidence. None of these observations changes global focus.

To measure actual navigation, make a separate **state-changing** call to MCP
`probe_focus` or CLI `avascope probe-focus --request probe.json`:

```json
{
  "target": {"sessionId":"<session>","topLevelId":"<window>",
    "treeKind":"visual","nodeId":"<focused-node>",
    "nodeGeneration":"<observed-generation>","topLevelGeneration":"<observed-generation>"},
  "direction":"next",
  "strategy":"synthetic",
  "settleMs":100
}
```

Use `previous` for Shift+Tab. A probe sends one paired keystroke from the pinned
current focus and captures before/after snapshots. Native probes require native
focus to be confirmed. It never activates a window or refocuses a stale target.
Normal session-control leases, generation validation, input gating and evidence
policy remain in force. Policies must allow both inspection and `key_sequence`.

The result's `status: observed` means dispatch and subsequent observation
completed. `focusChanged` describes the observed targets; unchanged focus alone
does not prove a trap, and a change need not match the static prediction. Application
handlers may validate a field, commit edits or navigate. There is no automatic
restoration or replay, including after a lost transport response. Inspect the
current state before deciding on another operation. Failures preserve available
before/after evidence and map to `success: false` through CLI/MCP.

Existing explicit keyboard input also accepts `requireCurrentFocus: true` to
reject a changed focus before the first dispatch. A navigation keystroke's release
is still delivered after it moves focus; further strokes in the same sequence
stop rather than continuing in the newly focused control.

Inspection caps are 4096 scanned nodes, 64 candidates, 32 ancestor/tree levels,
64 KiB per snapshot and a cooperative two-second scan. Public navigation callbacks
must be fast and read-only; they cannot be preempted. Probe settling is 0..500 ms,
so delayed work after that observation is not declared complete. Redacted/excluded
controls are omitted before projection, including focused controls and predictions.

`RuntimeFocusTests` covers read-only prediction, forward/reverse navigation,
intercepted Tab, stopped multi-key sequences, focus changes, stale nodes, policies,
control leases, nested tab boundaries, modal focus and real CLI/MCP calls. The
native input gate adds platform focus evidence, CLI/MCP Tab probes, focus loss and
an owned native-dialog workflow wait on Windows/X11/macOS.

Public sources: [Avalonia focus manager contract](https://github.com/AvaloniaUI/Avalonia/blob/12.1.0/src/Avalonia.Base/Input/IFocusManager.cs),
[navigation metadata](https://github.com/AvaloniaUI/Avalonia/blob/12.1.3/src/Avalonia.Base/Input/KeyboardNavigation.cs),
[Win32 foreground observation](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-getforegroundwindow),
[X11 focus observation](https://xorg.freedesktop.org/archive/X11R6.8.1/doc/XGetInputFocus.3.html),
[AppKit key-window observation](https://developer.apple.com/documentation/appkit/nswindow/iskeywindow).
