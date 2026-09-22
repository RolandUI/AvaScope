# Target and platform readiness

Use `avascope doctor --target-request /absolute/target.json` or MCP
`doctor_target(request)` before launching a selected application. The CLI returns
the shared result in `value.target`; MCP returns it in `value`. CLI exit code 1
means one or more checks are unavailable, incompatible or unsupported.

```json
{
  "assemblyPath": "/work/MyApp/bin/Release/net10.0/MyApp.dll",
  "providerDirectory": "/work/providers/avascope-bridge-provider",
  "backend": "x11",
  "timeoutMs": 10000
}
```

Alternatively select `projectPath` and optionally `framework`, or select both
`profileFile` and `profileName` from an [agent test profile](AGENT_TEST_PROFILES.md).
Profile resolution reads configuration without building or launching its commands.
All selected paths are absolute. `expectedProviderVersion`,
`expectedManifestSha256` and `expectedInstallationRoot` can pin the intended
provider and installation. The backend is `auto`, `headless`, `x11`, `win32` or
`macos`; an unvalidated backend returns `target_backend_unsupported`.

The result contains at most 64 checks with stable error codes, stage, remediation
and bounded evidence. Component origins identify the Core, Protocol and isolated
probe installation. Metadata inspection does not load application assemblies.
Project inspection is conservative and static: conditional or computed MSBuild
values may require a built assembly or an explicit target framework.

The platform probe runs in a child process with a deadline of 100–30000 ms and
cleans up its own process and native handles. It never activates the bridge or
changes desktop configuration. It verifies:

- .NET 10, Avalonia 12.1.x, declared provider compatibility and managed dependency
  files in complete framework-dependent output.
- X11 libraries, actual `xdpyinfo` connectivity using the selected display and
  authorization environment, font discovery, and native Skia software rendering.
- A readable Windows input desktop, or macOS AppKit availability.
- Requested `nativeInput`, `nativeScreenshot` and `nativeDialogs` prerequisites.
  Permission probes do not display authorization prompts.

Native input and screenshot permissions are checked only when requested. macOS
trust belongs to the probing executable's TCC identity; the actual automation
host can have a different identity. A successful software Skia probe does not
certify the application's GPU configuration. GTK/portal availability is a
prerequisite check; opening and operating a real native dialog remains an
integration test. Unsupported headless native operations are explicit.

Typical failures are `target_display_missing`, `target_display_inaccessible`,
`target_native_dependency_missing`, `target_dependency_missing`,
`target_avalonia_incompatible`, provider verification codes and
`target_installation_root_conflict`. Native command stderr and environment values
are discarded; X11 authorization evidence records only whether XAUTHORITY is set.

When an MCP client sanitizes its server environment on Unix, preserve `TMPDIR`
from the application launch environment as well as the required display variables.
.NET named-pipe clients and servers use that directory to locate their local
socket. Different directories can cause a connection timeout despite a readable
session manifest.

Validation: `eng/test-target-readiness.ps1` exercises CLI/MCP native readiness,
incomplete output, and Linux missing/inaccessible-display failure fixtures. Unit
coverage also checks incompatible metadata, conflicting roots, bounded output,
unsupported headless native operations and child-process cleanup.
