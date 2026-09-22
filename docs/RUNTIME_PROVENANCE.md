# Runtime backend and operation evidence

`session-capabilities` / `session_capabilities` and attach's effective capabilities include `backends`. These are observations of registered/discovered top levels, taken on the UI thread from public Avalonia `TopLevel.PlatformImpl` and `TryGetPlatformHandle()`. The host OS alone does not establish the backend: a Headless app on Windows reports `headless`, not `win32`. Supported identities are `headless`, `x11`, `win32`, and `macos`; custom/wrapped/unavailable implementations report `unknown`. Loading a platform assembly without using it does not establish coverage.

Health reads cached observations without waiting on a blocked UI thread. Registration/discovery refreshes them; unregister/shutdown removes them. Before any top level is observed, `backends` is empty. Refresh `list-top-levels` after lifecycle changes in manually registered legacy hosts. Each returned top level has its own `backend` for sessions with multiple implementations. Capability revision includes the observed route support and restrictions, independent of session id, process id, or window count. Native Windows picker support requires an observed Win32 backend.

Actual compositor/GPU/software rendering mode is `unknown`: public platform identity does not prove which rendering backend was selected. No private renderer reflection is used.

Every successful input response carries `provenance`, also preserved in workflow steps, scenario results, redacted evidence JSON and response-budget artifacts:

| Route | What was executed |
| --- | --- |
| `avalonia_automation_provider` | An Avalonia automation provider, including range gestures. This does not prove OS accessibility integration. |
| `avalonia_synthetic_pointer` | Routed Avalonia pointer events. No OS mouse input. |
| `avalonia_synthetic_key` | Routed Avalonia key events. No OS keyboard input. |
| `avalonia_routed_event` | The existing Button click event path, with focus. |
| `avalonia_control_property` | Direct control changes: text/selection or scroll offset. No IME/native typing claim. |
| `avalonia_focus_api` | Avalonia focus request. |
| `not_dispatched` | Dry-run validation or no hit target; `plannedRoute` identifies the route that was considered. |

`dispatched` records execution, not application postcondition success. `fallback` is true only when a gesture actually used its pointer fallback. Failed requests retain their existing structured error; they must not be interpreted as successful execution or automatically replayed. A dry run never reports a dispatched fallback.

Screenshot `provenance.route` is `avalonia_render_target_bitmap`: it renders the Avalonia presentation root, excluding native window chrome, unrelated windows and external native surfaces. It is not desktop/screen capture. `coordinateSpace` is `top_level_pixel` for images and `top_level_dip` for pointer paths; `renderScaling` is the observed DIP-to-pixel scale. Where the bound native backend exposes public screen conversion, `screenOriginX/Y` locate client (0,0) in Avalonia screen pixel coordinates. They are null for Headless or unavailable conversion. Render scale and origin are operation-time evidence and may change between operations.

`inputRoutes`, `screenshotRoutes` and `restrictions` describe implemented routes; provider availability still depends on the target control. Native OS input and native screen capture are explicitly unavailable in this slice. Unknown/absent provenance from older bridges cannot be treated as native evidence. No new tool names or mandatory request fields are needed.

Avalonia 12.1 public API references: [TopLevel](https://github.com/AvaloniaUI/Avalonia/blob/12.1.0/src/Avalonia.Controls/TopLevel.cs), [X11 implementation](https://github.com/AvaloniaUI/Avalonia/blob/12.1.0/src/Avalonia.X11/X11Window.cs), [Headless implementation](https://github.com/AvaloniaUI/Avalonia/blob/12.1.0/src/Headless/Avalonia.Headless/HeadlessWindowImpl.cs). The native provider validation asserts actual Win32/X11/macOS identity, input route and screenshot route/scale; managed-X11 scenarios also assert that provenance survives both CLI and MCP workflow execution.
