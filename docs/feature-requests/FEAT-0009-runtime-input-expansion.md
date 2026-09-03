# FEAT-0009: Runtime input expansion

- Status: `Implemented`
- Implementation Status: `Covered by v0.6.0`
- Priority: `P0`
- Stored: `2026-06-10`
- Source Order: `1`
- Area: runtime bridge, CLI, MCP input

## User Need

AvaScope runtime input should cover common agent validation workflows beyond simple `Button` clicks and basic key events, so users do not need native OS clicking to verify tabs, selectable items, combo options, scrollbars, and keyboard shortcuts.

## Desired Behavior

- Click and coordinate input work against common selectable controls such as tab items, list items, combo box items, custom controls, and explicit coordinates.
- Keyboard input supports common keys and modifiers such as `Ctrl+Tab`, `Shift+Ctrl+Tab`, `Tab`, `Enter`, arrow keys, and `Escape`.
- Pointer input supports mouse wheel, drag, pan, and scrollbar thumb dragging where public Avalonia APIs make behavior deterministic.
- Input responses preserve target, key, modifier, pointer button, wheel, drag, and resulting focus/selection metadata where practical.

## Acceptance Criteria

- Runtime input commands can switch tabs or selected items in the sample app without relying on native OS automation.
- Unsupported input paths return structured diagnostics instead of silently doing nothing.
- The implementation stays local-only and avoids destructive actions.
- Tests cover bridge behavior plus CLI and MCP request validation for the supported action set.

## Notes

This is the highest-priority new runtime validation gap because tab/workspace switching and keyboard shortcuts are common agent verification steps.

## Implementation Notes

`v0.6.0` added targeted `select` input for `SelectingItemsControl` instances and deterministic `scroll` input for `ScrollViewer` offsets. The `v1.4.0` #99 slice adds bounds-derived `drag`, `swipe`, `long_press`, and `press_and_hold` across protocol, Bridge, Core workflows, CLI, and MCP. The `v1.4.1` #110 correction makes directional pointer-fallback percentages span the full safe target range instead of starting at its center. The `v1.4.2` #113 correction preserves the initially pressed/currently captured input element through move and release and clears residual capture, including nested template controls whose bounds are smaller than the parent gesture path. Writable range controls use the public `IRangeValueProvider`; custom controls use a bounded routed-pointer fallback with structured path/provenance/timing results and cancellation-safe release. Native Avalonia drag-and-drop payload exchange and full IME behavior remain deferred.
