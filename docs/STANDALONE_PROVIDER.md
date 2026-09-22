# Standalone bridge provider

The host decides whether inspection is allowed. Put its optional provider-loader call behind a host-owned compile-time flag such as `ENABLE_UI_INSPECTION`. Production builds omit the call and the external provider files. Loading `AvaScope.Bridge.dll`, inspecting its metadata or resolving the bootstrap method does not activate inspection.

The stable entry point is the public static, parameterless `AvaScope.Bridge.Bootstrap.Start()` method. Invoke it on `Dispatcher.UIThread` after Avalonia initializes `Application.Current.ApplicationLifetime`, normally at the end of `OnFrameworkInitializationCompleted`. Its BCL `string` return value is the local session id. Repeated calls return the same active session id. Public static `Bootstrap.Stop()` closes that session; a later `Start()` creates a new session. Hosts using reflection need no AvaScope type references.

The initial supported compatibility range is untrimmed .NET 10 and Avalonia 12.1.x on desktop platforms. NativeAOT, trimmed host applications and mixed Avalonia versions are unsupported. Runtime/Avalonia version, UI-thread and lifetime failures have explicit `AVASCOPE_*` diagnostics before activation. The external loader must validate provider files and host-shared assembly identity before invoking this entry point; it must never load a second Avalonia runtime to satisfy the bridge.

The bootstrap uses the existing current-user-only named-pipe transport and manifest discovery. It registers existing classic-desktop lifetime windows or the single-view root, tracks subsequently opened windows through Avalonia's public `WindowOpenedEvent`, and unregisters closed top-levels. Single-view roots are reconciled when their main view loads/unloads. Desktop lifetime `Exit`, explicit stop and remote session close release registrations and transport resources. A process-exit fallback removes transport resources even after the UI dispatcher stops. A forced process kill cannot execute managed cleanup; existing owned-manifest recovery remains necessary for that case.

The bootstrap does not enable custom/destructive application actions, inject into a process, start a TCP listener or search for other applications. Existing package integration through `AvaScopeBridge.Activate()` remains available and unchanged. The bootstrap can enable automatic lifetime registration on that same active bridge without creating a second session.

Implementation sources for the lifecycle hooks: [Avalonia 12.1 Window events](https://github.com/AvaloniaUI/Avalonia/blob/12.1.0/src/Avalonia.Controls/Window.cs) and [classic desktop lifetime](https://github.com/AvaloniaUI/Avalonia/blob/12.1.0/src/Avalonia.Controls/ApplicationLifetimes/ClassicDesktopStyleApplicationLifetime.cs).

Distribution, loader sample and compatibility-manifest validation are coordinated by #119 and #121; the complete standalone acceptance gate is tracked in #117.
