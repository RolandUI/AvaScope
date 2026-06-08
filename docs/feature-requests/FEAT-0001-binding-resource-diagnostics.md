# FEAT-0001: Binding and resource diagnostics

- Status: `Implemented`
- Implementation Status: `Covered by W9`
- Priority: `P0`
- Stored: `2026-06-08`
- Source Order: `2`
- Area: PreviewHost diagnostics, CLI, MCP

## User Need

During preview rendering, AvaScope should separately report binding, compiled binding, converter, `DataContext`, and resource-key failures. A user should not only see a generic render failure; they should be able to identify which binding or resource did not resolve.

## Desired Behavior

- Surface binding diagnostics independently from render exceptions.
- Include compiled binding failures where Avalonia exposes enough signal.
- Identify missing or invalid converters.
- Identify missing or unexpected `DataContext` cases when they affect bindings.
- Identify unresolved resource keys and the scope where resolution failed.
- Return structured diagnostics through reusable protocol data, with CLI and MCP adapters preserving the detail.

## Acceptance Criteria

- Preview results can include structured binding/resource diagnostic entries without failing the whole render when the view still renders.
- Failing binding/resource entries include the affected target node or path where practical.
- Diagnostics are bounded so large views do not return unbounded logs.
- The implementation stays inside PreviewHost/core protocol boundaries and does not make MCP Avalonia-specific.

## Current Coverage

- `PreviewResponse.Diagnostics` carries bounded structured diagnostics with severity, category, code, message, optional node/path, and detail fields.
- PreviewHost source metadata scanning reports missing root `DataContext`, missing/invalid binding converter resources, unresolved resource keys, and conservative binding path failures.
- Diagnostics are advisory when rendering succeeds; they do not turn a successful screenshot into a failed render.
- CLI and MCP preserve diagnostics through the same transport-neutral protocol response.

## Limitations

- Compiled binding and runtime binding-engine internals are reported only where public Avalonia APIs or source metadata expose enough signal.
- Resource and binding locations are best-effort source paths or node paths; AvaScope does not use private Avalonia hooks to infer hidden provenance.

## Notes

This is the highest-priority requested feature because it directly affects UI validation usefulness for complex Avalonia views.
