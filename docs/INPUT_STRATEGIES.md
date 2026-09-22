# Input strategies and native dialogs

Omitting input `execution` preserves existing routing. Explicit `execution.strategy`
selects `semantic`, `synthetic`, or `native`; an unsupported route fails before
dispatch and never silently falls back. Use returned `provenance`, then observe the
application postcondition. `handled` means dispatch, not a verified business result.

CLI passes `--execution options.json` (maximum 32 KiB); MCP `input` accepts the same
`execution` object. Workflow steps use `inputExecution`. Structured targets retain
the existing session/node generation checks. No new bridge activation mechanism or
foreign automation framework integration is involved.

```json
{
  "strategy": "synthetic",
  "button": "right",
  "clickCount": 2,
  "intervalMs": 75
}
```

For `key_sequence`, use `keys: [{"key":"A","modifiers":"Control"},
{"key":"Left","modifiers":"Shift"}]`. Each entry dispatches one down/up pair.
Modifiers are per-event state, not globally held keys. At most 32 entries and
3000 ms total inter-key delay are allowed. Literal `key_text` never parses command
notation: `<Ctrl+A>` remains text. Synthetic text raises Avalonia text input;
semantic text uses the existing control-value route. Native text has the platform
limits below. Text is bounded to 2048 UTF-16 characters; native literal input rejects
control characters (use explicit Enter/Tab strokes instead).

`click` supports left/right/middle and 1–3 presses. Synthetic presses carry counts
1, 2, 3 directly. Native clicks start a new group after waiting the backend double-tap interval
plus 25 ms (reported as clickGroupDelayMs), then send the requested presses.
A requested interval at least as long as the platform threshold is rejected.
Concurrent real user input can still interfere, so observe the resulting count. `intervalMs` defaults to 75,
range 0–250. Chords use `keyModifiers` on pointer requests and `modifiers` on each
key stroke. Raw explicit held-button/key operations are deliberately unavailable:
use a paired request, or the documented legacy synthetic API.

`pointer_move` and `drag` accept `destinationX/Y` in the selected top-level's DIP
coordinates. Drag requires a destination. Start with explicit x/y or the current
visual target center. All points stay within that window. `durationMs` defaults to
250 (0–3000), `motionSteps` to 10 (1–120), and `motionProfile` to `linear` or
`ease_in_out` (smoothstep). A fixed request produces the same points; scheduling
latency can lengthen actual timing. Only one compound request runs at a time.
The operation has a five-second cooperative deadline, followed by a bounded
cleanup wait. Application callbacks cannot be forcibly preempted safely.

On cancellation/failure, cleanup sends the outstanding release only to this
request's original target/device. It never releases global user input. Errors
report dispatch count and cleanup state; `failed_or_pending` means stop and inspect
the app. Unknown outcomes are not automatically retried. Synthetic capture is
released in finally. Native events target owned windows, with no global keyboard
or pointer injection. A closed, detached, disabled, hidden or changed-focus target
stops further dispatch. Cleanup may still address the original owned window after
focus loss. A native handler may open a nested native event loop; modifiers are
restored when that handler returns.

| Capability | Windows Win32 | Linux X11 | macOS AppKit | Headless / unknown |
| --- | --- | --- | --- | --- |
| Semantic / synthetic input | Yes | Yes | Yes | Synthetic on Avalonia Headless; target-dependent semantic providers |
| Native route | `win32_owned_window_message` | `x11_owned_window_event` | `appkit_owned_window_event` | Explicit unsupported error |
| Native pointer | Owned HWND messages; client pixels derived from DIP | XSendEvent directly to owned XID, NoEventMask, no propagation | NSEvent to owned NSWindow | Unavailable |
| Native keys | Logical A–Z, D0–D9, F1–F12, navigation; thread-local modifiers | Base-group mapped keys and verified standard modifier groups; no remapping | Layout-independent navigation keys and modifiers | Unavailable |
| Native literal Unicode | WM_CHAR UTF-16 | Unsupported; explicitly choose synthetic text | Owned NSTextInputClient insertion | Unavailable |
| IME / dead-key composition | Unavailable | Unavailable | Unavailable | Unavailable |
| Clipboard read/write | Unavailable; input does not touch clipboard | Unavailable | Unavailable | Unavailable |
| Drag/drop | In-window pointer drag; no native data-transfer promise | In-window pointer drag; no XDND payload or unrelated window | Native drag unsupported; explicitly choose synthetic drag | Synthetic pointer drag |

These native routes exercise native window input handling. They do not emulate
hardware, move the global cursor, prove global hotkey behavior, or exercise OS
input permissions required by global injection tools. Win32 requires the owning
UI thread and active HWND; a nonzero desktop foreground HWND must match.
A noninteractive desktop with no foreground HWND can still receive these owned
window messages, since no global device injection is used. AppKit requires an
application-owned key window. X11 requires a live registered 64-bit XID and its
authorized display connection. All window access uses Dispatcher.UIThread. New
backend identities remain unsupported until separately validated.

Avalonia.Native obtains held mouse buttons from AppKit's global device state.
Addressed NSEvents cannot supply that state during motion, so native macOS drag
fails before dispatch. The explicit synthetic route preserves request-owned
button state and is tested separately. Clicks and paired navigation keys still
use the owned AppKit route.

Avalonia 12.1 maps the core X11 event's Button2/3 modifier bits in reversed order.
The targeted provider supplies post-release button state and normalizes held
button state on motion for this receiving route; otherwise right/middle releases
can be treated as moves with a phantom second button. This compatibility behavior
is covered by the native gate and must be rechecked when updating Avalonia.

## Native and predefined picker paths

`native_picker` / `native-picker` retains `detect`, `select_path`, `confirm`,
`cancel`, `predefine_result` and `consume_predefined_result`.
Pass `topLevelId` (`--top-level`) for the bridge native route, with a 0–3000 ms
timeout. Native success reports `route`; selected paths are redacted by default.
No native operation substitutes a prepared result.

| Path | Supported operations and ownership |
| --- | --- |
| Windows common picker | Existing process/owner-chain check, bounded select/confirm/cancel. Legacy request without topLevelId remains available. |
| X11 GTK3 chooser | In-process GTK list, matching X11 transient owner equals the explicitly selected window; all GTK calls execute on GLib. Existing file/folder and save paths, confirm/cancel. |
| macOS AppKit sheet | Native NSSavePanel/NSOpenPanel sheet attached to the explicitly selected NSWindow. Detect/cancel; select_path on save panels. Open/folder programmatic selection and all programmatic confirmation explicitly unsupported. |
| Portal-hosted / unrelated dialog | Unsupported without validated request ownership/correlation; no process search or desktop-wide automation. |
| Prepared result | App logic only. Session/correlation scoped, one shot, TTL bounded. Works on every platform, independently of native dialogs. |

On Linux, Avalonia may prefer a portal. A host that deliberately tests the GTK
path can configure `X11PlatformOptions.UseDBusFilePicker = false` before startup.
The bridge never silently reconfigures the host or initializes GTK to replace a
missing picker. Ambiguous owned GTK dialogs fail. Requested paths are absolute,
bounded, and must exist for open/folder selection. Save parent directories must
exist. Selecting/confirming is a real UI side effect; a timeout may mean an effect
occurred without confirmed closure, so inspect before retrying.

The host can explicitly integrate prepared results without referencing an AvaScope
assembly at compile time. After its authorized `Bootstrap.Start`, reflect
`AvaScope.Bridge.Bootstrap.TakePreparedPickerResult(string correlationId)`. It returns
a JSON string with the existing NativePickerResponse fields. The host decides how
to use `success`, `cancelled`, `unavailable_path`, `deleted_path`, `expired` or
`not_prepared`; for `not_prepared`, it can open its real StorageProvider picker.
This hook throws if the bridge was not explicitly started. Merely loading an
assembly or preparing a result does not intercept any application picker.
The sample's `--input-fixture` demonstrates the reflection-only call under its
existing compile-time diagnostics flag.

## Validation

`eng/test-native-input.ps1` publishes the host without AvaScope assemblies and uses
the external provider. It runs real CLI/MCP pointer and keyboard requests, literal
text coverage/unsupported cases, focus loss, mismatched session and dialog owner,
one-shot host results, and real native picker cancel/select/confirm. Linux owns an
authenticated Xvfb/Openbox/D-Bus environment and cleans up those exact helpers.
The gated native test is explicitly skipped in ordinary headless test runs.
`ExplicitInputTests` separately verifies no-effect validation, deterministic motion,
request cancellation cleanup and CLI/MCP headless parity. Native coverage is never
inferred from a headless pass. Hosted CI runs the native gate on each desktop OS.

Public API references: [Avalonia 12.1 pointer events](https://github.com/AvaloniaUI/Avalonia/blob/12.1.0/src/Avalonia.Base/Input/PointerEventArgs.cs),
[Avalonia X11 dispatch](https://github.com/AvaloniaUI/Avalonia/blob/12.1.0/src/Avalonia.X11/X11Window.cs),
[Xlib event delivery](https://xorg.freedesktop.org/archive/current/doc/libX11/libX11/libX11.html),
[Win32 thread keyboard state](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setkeyboardstate),
[GTK3 chooser selection](https://docs.gtk.org/gtk3/method.FileChooser.set_filename.html),
[AppKit NSSavePanel](https://developer.apple.com/documentation/appkit/nssavepanel).
The macOS motion limitation follows [Avalonia.Native modifier conversion](https://github.com/AvaloniaUI/Avalonia/blob/12.1.0/native/Avalonia.Native/src/OSX/AvnView.mm).

macOS file panels are hosted out of process. Apple explicitly excludes calling
`ok:` to confirm them; AvaScope reports that capability as unsupported before
sending any confirmation. The native gate validates path selection, cancellation
and this negative case; only the explicit predefined-result host hook covers
successful app-level selection. It never substitutes a native-confirm success.
See [Apple WWDC19: Advances in macOS Security](https://developer.apple.com/videos/play/wwdc2019/701/)
and [NSSavePanel](https://developer.apple.com/documentation/appkit/nssavepanel).
