# FEAT-0003: Computed style and resource inspector

- Status: `Backlog`
- Implementation Status: `Not started`
- Priority: `P2`
- Stored: `2026-06-08`
- Source Order: `4`
- Area: runtime inspection, preview inspection, protocol

## User Need

A user should be able to inspect a UI element and see the final computed visual/style values, including where values came from.

## Desired Behavior

For a selected node, AvaScope should expose final values such as:

- `Foreground`
- `Background`
- `BorderBrush`
- font family, size, style, and weight
- `Padding`
- `Margin`
- other high-value layout or visual properties

Where practical, the inspector should also report whether each value came from a local value, style setter, theme, dynamic resource, static resource, inherited value, or default.

## Acceptance Criteria

- `inspect_node` or a new focused tool can return bounded computed style/resource information for one node.
- Results avoid dumping arbitrary Avalonia internals.
- Resource provenance is reported where public Avalonia APIs make it reliable.
- Unsupported provenance cases return explicit `unknown` or `not_available` fields instead of guessing.

## Notes

This is the third-priority requested feature and should preserve AvaScope's transport-neutral protocol boundary.
