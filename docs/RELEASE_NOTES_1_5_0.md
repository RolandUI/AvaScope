# AvaScope 1.5.0

This release adds external bridge loading, reproducible agent test environments,
and richer observation/action tools for already connected Avalonia applications.
It targets .NET 10 and Avalonia 12.1.x. The complete scope is #117–#153 and #155;
the external-framework interoperability proposal #154 was cancelled.

## Connect a host without an AvaScope dependency

The new standalone provider ZIP includes its dependency inventory and hashes.
A host-owned diagnostics build flag can guard a BCL-only loader that verifies an
explicit provider directory, then calls `AvaScope.Bridge.Bootstrap.Start()` through
reflection. Existing/new/closed windows are registered automatically. Loading an
assembly alone never activates inspection, and normal host output can contain no
AvaScope assemblies. Existing direct package integration remains supported.

[Provider compatibility and loader](STANDALONE_PROVIDER.md),
[integration profiles](AGENT_TEST_PROFILES.md),
[onboarding recipes](AGENT_RECIPES.md).

## Observe, decide and act with stronger evidence

- [Coordinated observations](RUNTIME_OBSERVATIONS.md) and
  [bounded change journals](RUNTIME_OBSERVATION_CHANGES.md) reduce repeated tree reads.
  [Readiness](RUNTIME_READINESS.md), [target guards](TARGET_READINESS.md), and
  [dispatch preconditions](DISPATCH_PRECONDITIONS.md) distinguish stale, blocked,
  busy and actionable targets.
- [Relationship queries](RELATIONSHIP_QUERIES.md),
  [action explanations](ACTION_EXPLANATIONS.md), [action maps](ACTION_MAP.md),
  [focus navigation](FOCUS_NAVIGATION.md), and
  [typed compound assertions](RUNTIME_EXPRESSIONS.md) expose evidence for decisions.
- [Desired-state actions](DESIRED_STATE_ACTIONS.md), [forms](FORM_WORKFLOWS.md),
  [tables](TABLE_WORKFLOWS.md), [virtual items](TESTABILITY_AND_VIRTUAL_ITEMS.md),
  and [text-range editing](TEXT_RANGE_EDITING.md) provide bounded semantic controls.
- [App-reported operations](RUNTIME_OPERATIONS.md),
  [bounded traces](RUNTIME_TRACES.md), [semantic scenes](SEMANTIC_SCENES.md), and
  [observed navigation history](NAVIGATION_HISTORY.md) add optional application context.
- [Explicit input strategies](INPUT_STRATEGIES.md),
  [window management](WINDOW_MANAGEMENT.md), [point picking/highlights](PICKING_AND_HIGHLIGHTS.md),
  [paired screen evidence](SCREEN_EVIDENCE.md), and
  [native accessibility comparison](NATIVE_ACCESSIBILITY.md) retain their actual
  operation route, platform limits and mapping confidence.

## Reproduce and recover agent runs

[Named profiles](AGENT_TEST_PROFILES.md) use the same Core engine through CLI/MCP.
[Host-declared fixtures](TEST_FIXTURES.md), [owned run recovery](RUN_RECOVERY.md),
and [workflow export/replay](WORKFLOW_EXPORT.md) support repeatable tests and
failure investigation without importing another framework's workflow language.

[Managed X11](MANAGED_X11.md) provides private authenticated Xvfb, optional
Openbox and an isolated session bus. [Controlled Wayland](MANAGED_WAYLAND.md)
uses Weston 13 headless/Pixman with verified output geometry and explicit native
Avalonia Wayland selection. XWayland remains a separately unsupported optional
lane. The [platform matrix](NATIVE_PLATFORM_MATRIX.md) distinguishes headless,
Windows, X11, macOS and experimental native Wayland evidence.

## Compatibility and limits

Use matching release versions for CLI/MCP and the external provider; replace the
entire provider directory and verify the manifest rather than copying individual
DLLs. The external provider supports untrimmed .NET 10 / Avalonia 12.1.x hosts;
NativeAOT, trimmed hosts and mixed Avalonia versions are unsupported. Gate optional
operations with `capabilities` and current `session-capabilities`.

Native functionality is platform-specific. Accessibility comparison reads Windows
UIA or Linux X11 AT-SPI; other backends return unsupported. Native screen capture
requires an explicit host-declared test desktop, request authorization and OS
permission, and may report unavailable. Experimental Wayland supports the
documented semantic/synthetic operations and rendered screenshots; it does not
claim native desktop input, screen capture, window management or accessibility
audit. A configured keyboard layout without an input seat is not keyboard evidence.

Activation stays explicit and local-only. There is no process injection, CLR
profiling, remote listener or unrelated-process control. Session-owned resources,
evidence redaction, bounded results and truthful unsupported outcomes apply to
the new tools. See [upgrade guidance](UPGRADE.md) before replacing running binaries.
