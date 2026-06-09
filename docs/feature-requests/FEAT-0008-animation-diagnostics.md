# FEAT-0008: Animation diagnostics

- Status: `Implemented`
- Implementation Status: `Covered by v0.3.0`
- Priority: `P4`
- Stored: `2026-06-09`
- Source Order: `8`
- Area: PreviewHost rendering, runtime inspection, protocol, CLI, MCP

## User Need

Agents and developers should be able to evaluate Avalonia animation behavior without relying on a human-only live designer preview.

## Desired Behavior

AvaScope should expose deterministic animation inspection features such as:

- sampling screenshots at explicit animation time offsets, for example `0ms`, `150ms`, and `300ms`
- returning a bounded frame sequence or visual strip for human review when requested
- reporting structured animation metadata where public Avalonia APIs make it reliable: target node, animated property, duration, easing, start value, current value, and final value
- surfacing animation-related diagnostics such as layout shift, clipping during motion, unstable final state, or nodes that disappear unexpectedly during a transition

The feature should favor agent-readable state and deterministic frame comparison over a Rider-style continuous live animation preview.

## Acceptance Criteria

- CLI and MCP can request animation sampling for a preview or running bridge session without changing existing screenshot behavior.
- Results include bounded structured metadata plus file paths for generated frames or strips.
- Sampling is deterministic enough for tests by using explicit time offsets instead of wall-clock observation.
- Unsupported or unreliable animation metadata is reported as `unknown` or `not_available` rather than guessed.
- PreviewHost isolation and local-only runtime safety boundaries are preserved.

## Non-Goals

- No private Avalonia runtime hooks or designer-specific APIs.
- No requirement to match Rider's full interactive animation designer experience.
- No long-lived preview host process unless lifecycle, close, TTL, crash, and cleanup semantics are separately designed.

## Notes

This diagnostic capability was implemented and released in `v0.3.0`. The shipped scope covers deterministic PreviewHost animation sampling at explicit time offsets, bounded frame artifacts, frame strips, file-backed timeline viewers, CLI `preview-animation`, MCP `preview_axaml_animation`, pixel-motion diagnostics, explicit `not_available` provenance for unavailable animation metadata, and stable repeated-offset artifact reuse.
