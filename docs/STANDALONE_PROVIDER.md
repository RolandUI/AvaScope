# Standalone bridge provider

The host decides whether inspection is allowed. Put its optional provider-loader call behind a host-owned compile-time flag such as `ENABLE_UI_INSPECTION`. Production builds omit the call and the external provider files. Loading `AvaScope.Bridge.dll`, inspecting its metadata or resolving the bootstrap method does not activate inspection.

The stable entry point is the public static, parameterless `AvaScope.Bridge.Bootstrap.Start()` method. Invoke it on `Dispatcher.UIThread` after Avalonia initializes `Application.Current.ApplicationLifetime`, normally at the end of `OnFrameworkInitializationCompleted`. Its BCL `string` return value is the local session id. Repeated calls return the same active session id. Public static `Bootstrap.Stop()` closes that session; a later `Start()` creates a new session. Hosts using reflection need no AvaScope type references.

The initial supported compatibility range is untrimmed .NET 10 and Avalonia 12.1.x on desktop platforms. NativeAOT, trimmed host applications and mixed Avalonia versions are unsupported. Runtime/Avalonia version, UI-thread and lifetime failures have explicit `AVASCOPE_*` diagnostics before activation. The external loader must validate provider files and host-shared assembly identity before invoking this entry point; it must never load a second Avalonia runtime to satisfy the bridge.

The bootstrap uses the existing current-user-only named-pipe transport and manifest discovery. It registers existing classic-desktop lifetime windows or the single-view root, tracks subsequently opened windows through Avalonia's public `WindowOpenedEvent`, and unregisters closed top-levels. Single-view roots are reconciled when their main view loads/unloads. Desktop lifetime `Exit`, explicit stop and remote session close release registrations and transport resources. A process-exit fallback removes transport resources even after the UI dispatcher stops. A forced process kill cannot execute managed cleanup; existing owned-manifest recovery remains necessary for that case.

The bootstrap does not enable custom/destructive application actions, inject into a process, start a TCP listener or search for other applications. Existing package integration through `AvaScopeBridge.Activate()` remains available and unchanged. The bootstrap can enable automatic lifetime registration on that same active bridge without creating a second session.

Implementation sources for the lifecycle hooks: [Avalonia 12.1 Window events](https://github.com/AvaloniaUI/Avalonia/blob/12.1.0/src/Avalonia.Controls/Window.cs) and [classic desktop lifetime](https://github.com/AvaloniaUI/Avalonia/blob/12.1.0/src/Avalonia.Controls/ApplicationLifetimes/ClassicDesktopStyleApplicationLifetime.cs).

## Host loader example

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
