# Native platform validation matrix

Run the same external-provider fixture and assertions on each backend. The host has no AvaScope package dependency and performs no manual top-level registration. `eng/test-native-platform-matrix.ps1` uses the existing scenario runner through both CLI and real MCP stdio, with one successful and one deliberate failure case per adapter.

| Lane | Environment | What it establishes |
| --- | --- | --- |
| `headless` | Avalonia Headless + Skia | Fast runtime/control behavior and overlay popup, no desktop integration |
| `win32` | Interactive Windows worker | Actual Win32 top levels and native popup creation |
| `x11` | Linux, owned authenticated Xvfb + Openbox + private D-Bus | Actual X11 top levels/window manager lifecycle, with local Unix transport and no TCP |
| `macos` | Native macOS worker | Avalonia.Native top levels and native popup creation, observed screen scale |
| XWayland | Separate future lane | Explicitly unsupported until separately validated; Xvfb success is not XWayland coverage |
| Native Wayland | Separate future lane | Explicitly unsupported until a controlled compositor/backend gate exists |

Every implemented lane checks existing/new/closed windows; modal ownership through the fixture's public `Window.Owner`; focus and routed key input; popup `Opened`/`Closed` and `IsUsingOverlayLayer` (native window on desktop lanes, overlay on Headless); resize from 500×460 to 580×520 DIP; and screenshot pixel dimensions at the observed `RenderScaling`. Popups are verified by public fixture state, not claimed as independently inspectable registered windows. Cross-window actions use title aliases instead of saved runtime ids. Conditions are polled with bounded deadlines, without fixed sleeps.

The fixture requests Light theme and records the default font family resolved by Avalonia. Linux CI installs DejaVu fonts. Reports retain actual top-level backend/type, restrictions, renderer (`unknown` when not publicly observable), input routes, screenshot route/scale and platform/runtime identity. Existing macOS Retina and headless 1×/2× checks remain in CI. Moving between monitors or changing a user's global DPI setting is not part of this gate. Images from different backend/font/scale/theme combinations are separate baseline contexts; the matrix tests behavioral assertions and dimensions, not cross-platform pixel equality.

Provider invocation, routed keyboard events and Avalonia focus operations are labeled by their actual route. They do not establish native mouse/keyboard, OS accessibility, IME, or desktop-capture coverage. Screenshots use RenderTargetBitmap; native capture remains a separate capability.

The separate `eng/test-native-input.ps1` gate validates owned native window input and real native pickers through CLI/MCP on Windows, Linux X11 and macOS. See [input strategies](INPUT_STRATEGIES.md) for exact platform capabilities and negative cases, including unsupported native AppKit drag and X11 literal Unicode. Its results are separate from this matrix's provider/routed-event coverage.

Local commands after a Release solution build and `pwsh -File eng/package-provider.ps1`:

```powershell
pwsh -File eng/test-native-platform-matrix.ps1 -Backend headless
pwsh -File eng/test-native-platform-matrix.ps1 -Backend win32
# Linux (requires Xvfb, xdpyinfo, openbox, xprop, dbus-daemon and dbus-send):
pwsh -File eng/test-native-platform-matrix.ps1 -Backend x11
# macOS:
pwsh -File eng/test-native-platform-matrix.ps1 -Backend macos
```

Pass `-CliAssembly <packaged/avascope.dll> -McpAssembly <packaged/AvaScope.Mcp.dll>` to use a distribution. CI runs Headless and Win32 with packaged Windows binaries, X11 with packaged Linux binaries, and macOS with its native packaged binaries. The existing standalone-provider gate separately verifies normal host shutdown, bootstrap-disabled startup and incompatible providers.

Evidence is stored under `artifacts/platform-matrix/<backend>-<run>/`: `validation.json`, sanitized `*-result.json`, and policy-owned `evidence/` trees with bounded reports, failure screenshots, trees and helper/launch logs. A synthetic sensitive text field exercises redaction and screenshot masking. Each adapter's deliberate assertion failure must preserve evidence while removing the owned app, session manifests, and managed X11 helpers/resources. CI retains these diagnostics on failure for seven days. Raw request files, private Xauthority cookies, app/provider binaries and build intermediates are not uploaded.

The matrix exposed a redaction round-trip bug for excluded controls with typed target timestamps. These timestamps now become `DateTimeOffset.MinValue` (excluded/unknown time), preserving neither the original value nor an invalid textual placeholder. The dedicated regression and native failure cases guard this behavior.
