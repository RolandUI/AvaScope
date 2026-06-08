# FEAT-0002: Layout warnings

- Status: `Implemented`
- Implementation Status: `Covered by W9`
- Priority: `P1`
- Stored: `2026-06-08`
- Source Order: `3`
- Area: PreviewHost diagnostics, rendered layout analysis

## User Need

AvaScope should automatically identify common UI layout problems during preview rendering so agents can catch visual defects without manually inspecting every screenshot.

## Desired Behavior

Report warnings for:

- clipped or overflowing text
- text truncation
- overlapping elements
- clipped content
- content unreachable without scrolling
- hit targets that are too small

## Acceptance Criteria

- Preview diagnostics include bounded layout warning entries with severity, category, and affected node information where practical.
- Text clipping and truncation detection works for common text controls.
- Overlap detection avoids obvious false positives from intentional overlays where possible.
- Hit target checks use an explicit minimum target size policy documented with the implementation.
- Warnings do not block screenshot generation unless rendering itself fails.

## Current Coverage

- PreviewHost analyzes the rendered visual tree after layout and emits bounded layout diagnostics.
- Implemented warning codes cover text clipping/truncation, clipped content, unreachable content, sibling overlap, and too-small hit targets.
- The hit-target policy is a minimum `24x24` device-independent pixel bounds check for common interactive controls.
- Overlap detection skips obvious overlay-style containers such as canvas, popup, adorner, and overlay hosts where practical.
- Layout warnings remain advisory and do not block screenshot generation.

## Notes

This is the second-priority requested feature and should likely build on visual tree bounds plus rendered output analysis.
