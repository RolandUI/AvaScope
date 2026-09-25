# Native accessibility comparison

CLI `audit-native-accessibility --request audit.json` and MCP
`audit_native_accessibility` expose `runtime.native_accessibility`. Invocation is
explicit and requires an already activated bridge, selected registered top-level
and its current generation. Loading the provider never starts an audit.

```json
{
  "target": { "sessionId": "observed-session", "topLevelId": "observed-window", "topLevelGeneration": "observed-generation" },
  "maxNodes": 64,
  "maxDepth": 16,
  "timeoutMs": 3000
}
```

The response keeps **bridgeNodes** (public Avalonia peers and current visual
geometry) separate from **native.nodes** (actual OS accessibility API results).
It includes timestamps, desktop units, truncation, mapping provenance/confidence
and findings. It is an audit observation, not proof of accessibility conformance.
The samples are sequential; asynchronous UI changes can explain state differences.
Window movement, generation changes and detached sampled controls invalidate the
comparison rather than exporting stale targets.

Unique matching AutomationIds produce `high_identity` mappings. Without one,
geometry within two desktop pixels plus compatible role or name can produce a
`probable_geometry` mapping. Duplicate ids/multiple candidates are ambiguous.
If either sample is partial/truncated, identity confidence is reduced to
`candidate_identity_in_partial_sample`: unseen duplicates cannot be ruled out.
Canonical role equivalences account for platform vocabulary, such as Button vs
AT-SPI push button, Edit vs entry and Window vs frame. Missing names, differing
expected names/roles and enabled states are review findings. Native nodes without
bridge counterparts remain evidence; they may be native children or grouping.
For example, Avalonia's AT-SPI provider can publish a class-name fallback such as
`Button` where Windows UIA exposes an empty name. The native value is preserved;
an explicit expected name identifies that mismatch without inventing an empty
AT-SPI name. UIA control view and AT-SPI grouping/exposure can also differ.

By default an unmatched visual is **not a defect**. Decorations, templates,
grouping, unrealized virtualized items and native children make the trees differ.
For a known requirement, add up to 32 `expectations`, each containing a pinned
visual `target` and optional `name` and `role` (Avalonia or native vocabulary).
Expectations require exposure even for a custom control without a normal peer.
An expected visible control not mapped in a complete native sample is a review
finding, not a definitive absence claim. Partial/truncated/unavailable samples
never make that finding. Explicit expectations are collected before the general
visual inventory and must fit its bounds.

## Platform boundaries

| Backend | Native evidence | Requirements and limitations |
| --- | --- | --- |
| Windows Win32 | UI Automation control view | Current owned HWND; native PID verified before reading each element. Foreign native child subtrees are excluded. No desktop-root enumeration. |
| Linux X11 | AT-SPI2 Accessible/Component over D-Bus | Avalonia 12.1.x with a running local accessibility service and GIO/GLib. Native window is selected by unique client geometry within this process's application roots. |
| macOS, headless, unknown/other backends | Explicit `unsupported` | Bridge evidence is retained; no bridge peer is presented as native OS evidence. |

Linux uses the host's existing **Unix** session bus and `org.a11y.Bus.GetAddress`.
The bus daemon's bounded connection-name/PID credential metadata identifies this
process's own accessibility connection. It never queries another application's
accessible objects or accepts a caller-selected PID/bus endpoint. Every followed
child reference must retain that same authenticated unique owner. A missing or
ambiguous window root returns unavailable. Native geometry cannot prove identity
when two owned windows have identical client bounds, so that case is refused.
No TCP bus, remote server, AT-SPI service activation, desktop setting changes or
unrelated-process control is performed. The native test gate explicitly starts
and cleans up its own AT-SPI service inside the managed X11 session.

`unavailable`, `unsupported` and `partial` are unsuccessful tool outcomes with
available evidence preserved. They do not mean an empty or healthy tree.
`compared` means the query completed, not that every accessibility expectation
passed. Review the per-control findings and truncation flags.

## Bounds, privacy and cleanup

Requests are at most 64 KiB; each source has 1–256 returned nodes, up to 32 levels,
and 250–5000 ms total cooperative deadline. Bridge traversal also stops at 2048
visuals; Linux connection metadata is capped at 1024 names/16 owned candidates
and 32 window roots. Text fields are capped at 256 characters; native text values,
password values and document content are never read. Native providers are queried
off the UI dispatcher; all Avalonia reads remain on it.

One native worker may run per session. Windows UIA connections/transactions use
250 ms native timeouts and disable automatic focus. Linux GIO calls use 250 ms
timeouts plus native cancellation. If a native call outlasts cancellation, the
request returns without launching another worker; its slot remains occupied until
native references are released. Session shutdown cancels the owned query. No
subscriptions, listeners, app mutations, global input or persistent OS resources
are created by an audit.

Windows `native_accessibility_partial` diagnostics retain the failing operation
stage, HRESULT and its source (`native_return` or `managed_exception`), native
reader elapsed time, current-stage elapsed time, cancellation state, configured
query/connection/transaction budgets and observed/pending node counts. Timings are
in invariant-culture milliseconds. The reader timer starts after bridge capture;
it is not the entire audit duration. A requested cancellation and a native error
may coexist, so neither elapsed time nor cancellation alone proves the cause of
a provider failure. Unobserved nodes remain unavailable, with already observed
native nodes retained as partial evidence.

Inspect these fields before changing provider readiness or timeouts. The adapter
keeps the actual failed HRESULT even if thread-local COM error information maps
it to a different managed exception. It never exports exception messages, inner
exceptions, stack traces or exception data. COM initialization failure is reported
at its own stage, and only successful initialization is balanced with uninitialization.

Session/PID inspection authorization and scalar redaction are enforced before
IPC output. An evidence policy with redacted or excluded AutomationId subtrees
**refuses the native audit**: native grouping can conceal ancestry, so selectively
removing names would not prove exclusion. Bridge-only audits remain available.
Password controls expose only a fixed control label, never their value.

Public API references:
[Windows UI Automation interfaces](https://learn.microsoft.com/en-us/windows/win32/api/uiautomationclient/nn-uiautomationclient-iuiautomation2),
[Windows SDK declarations](https://github.com/microsoft/win32metadata/blob/main/generation/WinSDK/RecompiledIdlHeaders/um/UIAutomationClient.h),
[UIA connection timeout](https://learn.microsoft.com/en-us/windows/win32/api/uiautomationclient/nf-uiautomationclient-iuiautomation2-put_connectiontimeout),
[UIA transaction timeout](https://learn.microsoft.com/en-us/windows/win32/api/uiautomationclient/nf-uiautomationclient-iuiautomation2-put_transactiontimeout),
[COM initialization](https://learn.microsoft.com/en-us/windows/win32/api/combaseapi/nf-combaseapi-coinitializeex),
[HRESULT mapping and thread-local error information](https://learn.microsoft.com/en-us/dotnet/api/system.runtime.interopservices.marshal.throwexceptionforhr?view=net-10.0),
[GIO D-Bus calls](https://docs.gtk.org/gio/method.DBusConnection.call_sync.html),
[Avalonia 12.1 AT-SPI interface implementation](https://github.com/AvaloniaUI/Avalonia/blob/12.1.3/src/Avalonia.FreeDesktop.AtSpi/Handlers/AtSpiAccessibleHandler.cs).
The adapter calls public native interfaces; it does not inspect Avalonia's private
AT-SPI server state or integrate with another test framework.

### Native integration test lifecycle evidence

The owned native accessibility fixture retains `cleanup.json` separately from
`initial.json`, `cli.json` and `mcp-redacted.json`. A compared audit is not evidence
that cleanup succeeded. Session close, service stop, process termination/exit,
both output captures, reader closure and disposal each retain their outcome and
elapsed time. Stream capture keeps its three-second bound; process exit has an
independent three-second cancellation bound. A failed capture produces an explicit
unavailable marker, while the other stream and remaining owned processes still
receive cleanup. Primary and cleanup exceptions are retained together; the safe
JSON contains exception types/HRESULTs, not messages, stacks or environment data.

The test explicitly disposes readers obtained through `Process.StandardOutput`
and `StandardError`, following the [.NET 10 Process ownership implementation](https://github.com/dotnet/runtime/blob/v10.0.0/src/libraries/System.Diagnostics.Process/src/System/Diagnostics/Process.cs).
Exit and stream EOF are separate observations. A later pass or short drain does
not establish the cause of an earlier delayed capture.
