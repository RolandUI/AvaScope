# FEAT-0009: Runtime input expansion

- Status: `Scheduled`
- Implementation Status: `Scheduled for v0.6.0`
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
