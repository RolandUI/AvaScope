# FEAT-0004: Automatic design-size recognition

- Status: `Implemented`
- Implementation Status: `Covered by W2; revalidated by W9`
- Priority: `P3`
- Stored: `2026-06-08`
- Source Order: `1`
- Area: PreviewHost, CLI, MCP

## User Need

The preview command should reliably read `d:DesignWidth` and `d:DesignHeight` from a view so users do not need to pass explicit `--width` and `--height` for every preview.

## Current Coverage

The development plan records this as covered by W2:

- preview request width and height are optional at the protocol, CLI, and MCP boundary
- PreviewHost falls back to `d:DesignWidth` and `d:DesignHeight`
- PreviewHost also considers `Design.Width` and `Design.Height`

W9 revalidated this behavior while adding multi-size preview: single-size preview compatibility remains intact, and design-size fallback remains the default when explicit dimensions are omitted.

## Acceptance Criteria

- Preview requests can omit explicit dimensions when the root AXAML provides positive design-time dimensions.
- CLI and MCP return structured diagnostics if dimensions are omitted and no valid design size can be resolved.
- Existing explicit width/height request behavior remains higher precedence than design metadata.

## Notes

This ticket is kept in the feature ledger for product tracking even though current repository tracking marks the core behavior implemented.
