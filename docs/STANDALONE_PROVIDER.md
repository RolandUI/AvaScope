# Standalone bridge provider

The host decides whether inspection is allowed. Put its optional provider-loader call behind a host-owned compile-time flag such as `ENABLE_UI_INSPECTION`. Production builds omit the call and the external provider files. Loading `AvaScope.Bridge.dll`, inspecting its metadata or resolving the bootstrap method does not activate inspection.

The stable entry point is the public static, parameterless `AvaScope.Bridge.Bootstrap.Start()` method. Invoke it on `Dispatcher.UIThread` after Avalonia initializes `Application.Current.ApplicationLifetime`, normally at the end of `OnFrameworkInitializationCompleted`. Its BCL `string` return value is the local session id. Repeated calls return the same active session id. Public static `Bootstrap.Stop()` closes that session; a later `Start()` creates a new session. Hosts using reflection need no AvaScope type references.

The initial supported compatibility range is untrimmed .NET 10 and Avalonia 12.1.x on desktop platforms. NativeAOT, trimmed host applications and mixed Avalonia versions are unsupported. Runtime/Avalonia version, UI-thread and lifetime failures have explicit `AVASCOPE_*` diagnostics before activation. The external loader must validate provider files and host-shared assembly identity before invoking this entry point; it must never load a second Avalonia runtime to satisfy the bridge.

The bootstrap uses the existing current-user-only named-pipe transport and manifest discovery. It registers existing classic-desktop lifetime windows or the single-view root, tracks subsequently opened windows through Avalonia's public `WindowOpenedEvent`, and unregisters closed top-levels. Single-view roots are reconciled when their main view loads/unloads. Desktop lifetime `Exit`, explicit stop and remote session close release registrations and transport resources. A process-exit fallback removes transport resources even after the UI dispatcher stops. A forced process kill cannot execute managed cleanup; existing owned-manifest recovery remains necessary for that case.

The bootstrap does not enable custom/destructive application actions, inject into a process, start a TCP listener or search for other applications. Existing package integration through `AvaScopeBridge.Activate()` remains available and unchanged. The bootstrap can enable automatic lifetime registration on that same active bridge without creating a second session.

Implementation sources for the lifecycle hooks: [Avalonia 12.1 Window events](https://github.com/AvaloniaUI/Avalonia/blob/12.1.0/src/Avalonia.Controls/Window.cs) and [classic desktop lifetime](https://github.com/AvaloniaUI/Avalonia/blob/12.1.0/src/Avalonia.Controls/ApplicationLifetimes/ClassicDesktopStyleApplicationLifetime.cs).

## Host loader example

Before editing a host, run `avascope integration-guide --project <absolute.csproj>` or MCP `integration_guide(projectPath, framework?)`. Both use the same bounded read-only analyzer. It reports target frameworks, resolved engine version, existing activation sites and file/line/SHA-256 guidance for **both** package and standalone modes. Choose one mode and review the source hash before applying its snippets. Analysis never evaluates MSBuild, builds the app, activates the bridge or changes source.

Conventional `Application.OnFrameworkInitializationCompleted` startup with a unique base call and a desktop/single-view lifetime receives concrete insertion guidance after window/view assignment. Existing activation receives no duplicate proposal. Unknown startup, unresolved/conditional frameworks, incompatible versions and AOT/trimmed configurations return `needs_review` diagnostics. Literal properties in the nearest `Directory.Build.props`, central package versions and `$(AvaloniaVersion)` are recognized; custom imports/conditions need an explicit review. Source scanning excludes generated/output/hidden directories and symlinks, with limits of 512 directories/files, depth 12, 1 MiB per file and 8 MiB total. Guidance is intentionally conservative and does not claim full C# or MSBuild semantic evaluation.

Required target/version/configuration review takes precedence over the
`already_integrated` status. Existing activation locations remain in the response
and no duplicate snippets are proposed; an activation call does not establish
that the selected target or diagnostics configuration is supported.

The `EnableUiInspection` property is host-owned and unset by default. Package guidance guards both the reference and call; standalone guidance guards the loader call and excludes its copied helper from normal compilation. Existing references require a reviewed migration before claiming dependency-free production output. Run the integration verification after applying a chosen mode, including its independent disabled-build lane.

Run `avascope verify-integration --request integration.json` or MCP `verify_integration(request)` to exercise the selected host. A minimal enabled request is:

```json
{
  "launch": {"command": "dotnet", "argumentList": ["/absolute/host/Host.dll"], "timeoutMs": 20000},
  "providerDirectory": "/absolute/external/provider",
  "outputDirectory": "/absolute/local/evidence"
}
```

Omit `providerDirectory` for package integration. Optional `build` uses the existing scenario build options and runs before launch; always supply the explicit built executable/DLL, including projects with a custom assembly name. The verifier allocates an isolated manifest directory, pins the verified provider for the host, reuses scenario launch/ownership, queries a bounded visual tree, inspects its root, captures the registered window and terminates only its own process. `safeInputTarget` plus `safeInputTargetDeclared: true` permits a focus-only probe on an explicitly declared non-destructive host target. `inspectionTarget` overrides the default visible root selector.

For the independent negative lane, select a separately built production output, set `bootstrapDisabled: true`, `productionOutputDirectory` to that output and optionally `observationMs` (250–30000, default 1500). This flag requests verification; it cannot disable bootstrap inside an incorrectly built application. The host must stay alive for the whole interval. The lane rejects AvaScope DLL/EXE output and observes its private manifest directory and PID-scoped AvaScope named pipes every 50 ms; it does not claim to detect arbitrary third-party listeners or activation after the observation interval. The supplied .NET 10 Unix transport uses the [runtime's CoreFxPipe naming](https://github.com/dotnet/runtime/blob/v10.0.0/src/libraries/System.IO.Pipes/src/System/IO/Pipes/PipeStream.Unix.cs). No TCP listener, attach scan or unrelated-process shutdown is used.

Every run creates an `integration-report.json` under a unique child directory with stage status, failure stage, remediation, logs, tree/screenshot evidence and the underlying scenario response. CLI exits 1 for a failed verification; MCP returns the same report in the usual tool envelope. Loader compatibility diagnostics distinguish activation failures from discovery timeouts. Failed runs retain evidence and recover only stopped resources belonging to their own process. Scenario `captureVisualTree` is an additive opt-in that stores `runtime-tree.json` before workflow dispatch, respecting configured evidence policy redaction and inspection permission.

Copy [OptionalProviderLoader.cs](examples/OptionalProviderLoader.cs) into the host. It uses only the .NET BCL and can be excluded from normal builds. The [StandaloneHost sample](../samples/AvaScope.StandaloneHost) links this source only when `EnableUiInspection=true`; it has no AvaScope package/project reference. Its normal output contains no AvaScope assemblies.

```csharp
#if ENABLE_UI_INSPECTION
var result = OptionalDiagnostics.OptionalProviderLoader.TryStartFromEnvironment(
    environmentVariable: "UI_INSPECTION_PROVIDER_PATH",
    expectedVersion: "1.5.0",
    expectedManifestSha256: "<SHA-256 of the pinned provider-manifest.json>");
// Send result.Code and result.Message to the host's diagnostic sink.
#endif
```

Call this from the UI thread at the end of `OnFrameworkInitializationCompleted`. The environment variable must name the absolute external directory containing `AvaScope.Bridge.dll` and `provider-manifest.json`. Leaving it unset disables this optional provider. Paths with spaces are supported; path comparison respects the host OS. Supplying an invalid path produces a structured failure and does not stop the host application. The sample logs the result to stderr for CI inspection.

The loader verifies every inventoried file before loading, checks exact optional version/manifest-hash pins, shares Avalonia assemblies exclusively through the host's default load context, and resolves provider-private dependencies only from verified files. Missing files, bad assemblies, wrong runtime/Avalonia versions and missing bootstrap members have diagnostic results. The provider is trusted executable code supplied explicitly by the host's operator: checksums establish integrity and pins, not publisher authenticity. Do not mutate a provider directory while its host is running; replacing a loaded provider requires restarting the host.

```powershell
dotnet build samples/AvaScope.StandaloneHost -c Release -p:EnableUiInspection=true
$env:UI_INSPECTION_PROVIDER_PATH = (Resolve-Path artifacts/providers/avascope-bridge-provider).Path
dotnet samples/AvaScope.StandaloneHost/bin/Release/net10.0/StandaloneInspectionHost.dll
```

For noninteractive smoke validation the sample accepts `--headless --exit-after-ms=3000`. Native backend validation omits `--headless`. Both modes use the real Avalonia controls and the same explicit reflection loader. Build without `EnableUiInspection` for the independently disabled lane; an environment variable alone cannot turn that build into an inspectable app.

## Provider distribution and verification

`pwsh -File eng/package-provider.ps1` produces `artifacts/providers/avascope-bridge-provider.zip` and its `.sha256` sidecar. The archive includes the Bridge/Core/Protocol assemblies, their resolver metadata, managed and native private dependencies, the copyable loader, documentation and license notices. `provider-manifest.json` declares the exact provider version, supported runtime/platform range, stable entry point, host-shared assembly identities, and the size/SHA-256 of every private file. Auxiliary Avalonia assemblies can have their own version line; their declared assembly identities are checked separately from the Avalonia engine's product range.

The release publishes this ZIP and checksum in addition to the normal CLI/MCP distributions and NuGet packages. Provider files remain separate from the application's deployment. Choose an explicit release artifact, validate its ZIP hash against `release-manifest.json`, extract it to a version-specific directory, and pin its exact manifest hash for the run. CLI upgrades do not select or replace that directory.

```powershell
avascope verify-provider --directory C:\diagnostics\avascope\1.5.0 --version 1.5.0 --sha256 <manifest-sha256>
```

The equivalent read-only MCP tool is `verify_provider(directory, expectedVersion, expectedManifestSha256)`. Both return the same provider identity and compatibility requirements without loading the bridge. Actual host Avalonia identity is checked by the explicit host loader at activation; successful offline verification does not claim that an unexamined host is compatible.

`pwsh -File eng/verify-provider.ps1` checks the release ZIP inventory, private file hashes, required native libraries, legal files and archive checksum. `pwsh -File eng/test-standalone-provider.ps1` builds separate enabled, disabled and incompatible hosts, then validates real reflection loading through CLI and MCP, existing/new/closed windows, screenshots, normal exit and remote-close cleanup, pins, tampering and load-only startup. Pass `-Native` for actual desktop backend coverage; default headless results are identified explicitly. Evidence and process logs remain under an isolated `artifacts/provider-validation/<run>` directory.

The complete standalone acceptance gate is tracked in #117, #119 and #121.
