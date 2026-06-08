# FEAT-0001: Binding and resource diagnostics

- Status: `Backlog`
- Implementation Status: `Not started`
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

## Notes

This is the highest-priority requested feature because it directly affects UI validation usefulness for complex Avalonia views.
