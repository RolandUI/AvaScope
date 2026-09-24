# Paired render and native screen evidence

CLI `capture-screen --request capture.json` and MCP `capture_screen` expose
`runtime.screen_capture`. Obtain a current `target` with `topLevelGeneration`
from `window` inspection, then choose `mode: paired`, `rendered` or `native`.
Existing `screenshot` keeps its Avalonia render-to-bitmap behavior.

```json
{
  "target": { "sessionId": "observed-session", "topLevelId": "observed-window", "topLevelGeneration": "observed-generation" },
  "outputDirectory": "/absolute/test-evidence",
  "mode": "paired",
  "desktopScope": "declared_test_desktop",
  "timeoutMs": 3000,
  "policy": { "ownedEvidenceRoot": "/absolute/test-evidence", "allowNativeScreenCapture": true }
}
```

## Host authorization and privacy

Native desktop pixels need an additional **host-only** opt-in, on the UI thread:

```csharp
#if ENABLE_UI_INSPECTION
// After explicit bridge activation, and only in an authorized test desktop:
AvaScope.Bridge.Bootstrap.SetNativeScreenCaptureScope("declared_test_desktop");
// Revoke with SetNativeScreenCaptureScope(null).
#endif
```

The optional standalone loader can invoke this BCL-only string method through
reflection; no package reference is needed. `Start()` remains unchanged and does
not grant desktop capture. No environment variable in the bridge enables it.
The method is absent from IPC, CLI and MCP. Repeated `Start()` does not widen scope;
closing/restarting the session discards the grant.

The host explicitly authorizes all desktop content presented **inside the selected
window's client rectangle**, including overlapping windows, native surfaces and
transparent backgrounds. It must own the test environment and authorize that
content. This is a declared trust boundary, **not** an OS-verified claim that every
pixel belongs to the target process. Use an owned Xvfb desktop or an otherwise
isolated CI desktop. Shared personal desktops with unrelated/private windows
should leave this grant disabled. AvaScope conservatively refuses native capture
without both the host grant and matching request scope; it does not attempt to
infer unrelated-window ownership from pixel colors or a racy foreground check.
It never captures the rest of the desktop or enumerates/attaches foreign processes.

An optional evidence policy additionally requires `allowNativeScreenCapture`.
Session/PID and screenshot action authorization apply before capture. A protected
top-level is rejected. Text/AutomationId/exclusion rules conservatively mask the
whole image because bounded tree sampling cannot prove all sensitive pixels were
found. Explicit mask regions use the output render-pixel grid and apply to both
images. Masks run **before IPC, files or inline results**; no unmasked temporary
file is created. Core rechecks masks and marker-owned paths before writing.
Successful results contain local artifact paths, never inline image byte arrays.

## Meaning and coordinates

Each image reports its source, start/end timestamps, size, masking and availability.
The response includes the exact session/window generation and backend. Captures
are sequential and never claim an atomic app/desktop snapshot. Changes in window
identity, position, scale or geometry discard native evidence. Revoking the grant
while an asynchronous capture is pending discards its pixels.

The Avalonia image is a `RenderTargetBitmap` of the presentation root. Native
capture reads the screen region as presented, so overlapping content can replace
rendered controls. It does not use a window's off-screen backing store.

Both output images use the target's render-pixel grid. `desktopBounds` is the
client rectangle in physical desktop pixels on Windows/X11 and Cocoa desktop
points on macOS. `renderScaling` and `desktopScaling` remain separate. Each
`nativeRegions` entry records the clipped monitor rectangle, its original native
pixel dimensions and destination `imageBounds`; mixed DPI captures may resample.
`nativePixelSize` on the frame is populated only for a single native region.
Off-screen areas and monitor gaps are transparent **missing evidence**. A fully
off-screen/hidden/minimized target has no native image. The cursor, protected video,
HDR/color profiles and display-server overlays are not guaranteed by these routes.

The descriptive comparison counts opaque, unmasked aligned pixels with a
16-per-channel tolerance. Privacy masks and missing pixels are excluded. Full
sensitive masking reports `privacy_masked`, not a passing comparison. Differences
can arise from occlusion, native surfaces, timing, resampling or color management;
they do not automatically diagnose a defect. Inspect the paired images and the
observed geometry before deciding.

Core saves valid masked frames before the optional comparison, using the same
bounded processing deadline. An exhausted comparison reports `timeout` with its
partial pixel counts; it does not discard files already saved. `captured` describes
the images, so inspect the comparison status separately. Save failures include
stage, elapsed time, exception type and cancellation source without exception text
or image contents.

Comparison uses SkiaSharp's public bulk color conversion and cached dimensions,
avoiding repeated native calls for every pixel. The two color arrays add at most
64 MiB under the existing 8-megapixel limit. All opaque, unmasked pixels, including
the last row and column, still participate; channel tolerance is unchanged.

## Native backends and failure behavior

| Backend | Native route | Conditions |
| --- | --- | --- |
| Windows | Desktop GDI `BitBlt` with layered-window capture | Interactive authorized test desktop; physical client rectangle |
| Linux X11 | Root-framebuffer `XGetImage` | Authorized display; bounded 24/32-bit little-endian RGB format; owned Xvfb is preferred |
| macOS 15.2+ | ScreenCaptureKit `captureImageInRect:completionHandler:` | Existing Screen Recording permission; Cocoa point rectangle, explicit original pixel size |
| Headless, Wayland/unknown, older macOS | Unavailable | No silent render substitution |

macOS permission is preflighted without prompting, changing TCC or bypassing the
OS decision. Hosted macOS runners without permission validate the explicit denied
path; a denied run is not evidence of successful native pixel capture. The modern
ScreenCaptureKit route avoids the removed/deprecated CoreGraphics screenshot APIs.
One process-wide macOS callback slot remains occupied until its completion, even
after caller timeout, so late images cannot be mistaken for a later request.

Windows access-denied results from locked, secure or noninteractive desktops also
produce `native_screen_permission_denied`. Their native gate records the denied
path explicitly; it does not unlock/switch desktops or count rendered pixels as
successful native capture.

Partial capture/save failure preserves the other requested source, with operation
`success: false`, `status: partial` and per-frame diagnostics. No automatic retry,
window activation, movement or permission request occurs. The capture budget is
250–5000 ms per bridge capture and client processing stage (cooperative across
native/codec calls), one in-flight request per bridge, 16 monitors, 8,388,608
output/native-region pixels, 16,384 per dimension and 256 KiB per masked PNG.
Thus 1920×1080 DIP at 2× retains its full 3840×2160 render-pixel grid without
resizing the application or downsampling the output. The existing per-region
original native sizes and image transforms still describe any mixed-display
resampling; output size alone is not proof of original native resolution.
Each exported capture gets a unique directory; existing files are never overwritten.

Dimensions are checked before render/native pixel allocations and client decode,
including finite values, rounded dimensions and overflow-sized inputs. macOS
region requests are checked in Cocoa points times the largest observed display
scale before entering ScreenCaptureKit, then the actual callback image is checked
again before copying. A concurrent OS display change may still produce an
oversized OS-owned image; it is rejected rather than copied or silently reduced.

The memory bound is on capture data, not total app/GPU/OS process memory. One
8 Mi-pixel BGRA plane is 32 MiB. Render and native stages run sequentially, retaining
only the first masked PNG while capturing the second. At native capture's largest
explicit pixel-buffer stage, output + OS/source + normalization uses at most four
such planes (128 MiB, plus at most 512 KiB X11 row padding); region buffers are
disposed before the next region. Client comparison holds two decoded planes and
one byte-per-pixel mask (72 MiB), then disposes them before saving/masking frames
sequentially. Codec/render backend scratch storage is additional and constrained
by the same image dimensions; these figures are not an RSS guarantee.
The two accepted encoded PNGs total at most 512 KiB; the existing 1 MiB bridge
response limit remains unchanged. High-entropy images can still exceed the
encoded limit and return an explicit byte-limit/partial result. No lossy evidence
is substituted to make them fit. The processing deadline checks comparison rows
and artifact writes; a synchronous native/codec call itself is not preempted.

Native regression fixtures exercise paired provenance, CLI occlusion, popups,
MCP masking, off-screen transparency and policy/scope refusals. Headless tests
exercise pre-IPC masking, unknown backends, stale/excluded targets, lease-safe
observation and failed artifact publication.

API references: [Windows BitBlt](https://learn.microsoft.com/en-us/windows/win32/api/wingdi/nf-wingdi-bitblt),
[Xlib image operations](https://www.x.org/releases/X11R7.5/doc/libX11/libX11.html),
[ScreenCaptureKit region capture](https://developer.apple.com/documentation/screencapturekit/scscreenshotmanager/captureimage(in:completionhandler:)),
[Apple Block ABI](https://clang.llvm.org/docs/Block-ABI-Apple.html).
