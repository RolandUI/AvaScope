# BUG-0004: Runtime screenshots misplace nested content on macOS Retina displays

- Status: `Fixed`
- Fix Status: `Implemented for v1.4.0`
- Stored: `2026-09-02`
- Privacy Review: no personal paths, identities, credentials, or machine-specific identifiers were included.

## Summary

AvaScope `1.3.0` runtime screenshots captured from an Avalonia `12.1` application on an Apple Silicon macOS Retina display do not match the native window. Top-level backgrounds are positioned correctly, while nested templated controls, text, buttons, and vector drawings are displaced toward the bottom-right and clipped.

The affected nodes still exist in the visual tree with valid, non-zero bounds. Delayed captures reproduce the same output, which points to the runtime screenshot rendering path rather than application layout, data binding, animation, or capture timing.

GitHub tracking: [#98](https://github.com/RolandUI/AvaScope/issues/98).

## Environment

- OS: macOS on Apple Silicon
- Display: Retina/HiDPI
- Target framework: .NET 10
- Avalonia: 12.1
- AvaScope and AvaScope.Bridge: 1.3.0
- Top-level logical size: `1920x1080`
- `RenderScaling`: `2`
- Captured screenshot size: `3840x2160`

## Reproduction

1. Launch a bridge-enabled Avalonia desktop application on a macOS Retina display.
2. Show a screen containing nested templated controls and a `DrawingImage` backed by nested `DrawingGroup` transforms.
3. Attach AvaScope and capture the top-level with the runtime screenshot operation.
4. Capture the same window with the native macOS screenshot function.
5. Compare nested child positions and clipping.

## Actual Result

- Parent backgrounds and other top-level elements remain near their expected positions.
- Nested content is displaced toward the bottom-right and may be clipped outside its parent.
- Text and buttons may appear near or beyond the lower edge of their container.
- `DrawingImage` and `DrawingGroup` content may be missing, displaced, or partially clipped.
- Content near logical X=445 appears near X=890, and content near X=610 appears near X=1220.

## Expected Result

- The runtime screenshot matches the native Avalonia window.
- Device scaling is applied exactly once.
- Every physical coordinate equals its logical coordinate multiplied by `RenderScaling` once.
- Nested templates and vector drawings retain their displayed position, scale, and clipping.

## Root Cause

Avalonia 12 does not guarantee that a `TopLevel` is the root visual presented by its render target. AvaScope passed the registered `TopLevel` directly to `RenderTargetBitmap.Render`, so platform presentation-root transforms were omitted while the output bitmap still used the top-level's scaled pixel size and DPI. On macOS Retina this made nested template and drawing transforms resolve in the wrong coordinate context.

## Resolution

Runtime capture now renders `topLevel.GetPresentationSource()?.RootVisual` and falls back to the registered top-level only when no presentation source exists. Pixel dimensions and DPI continue to derive once from `ClientSize` and `RenderScaling`.

Regression coverage applies a presentation-root transform and verifies nested control and vector-drawing pixels at scale factors 1 and 2. The native macOS smoke also resolves a nested DataTemplate control from the live visual tree and verifies its expected screenshot pixel using the reported runtime scaling.

## Regression Coverage

- Scale-factor-1 and scale-factor-2 runtime screenshot cases.
- Known parent and child coordinates in a nested template.
- Nested `DrawingImage`/`DrawingGroup` transforms.
- Native Apple Silicon Retina comparison.
- Existing Windows and Linux runtime screenshot behavior.

## Priority

P0 for v1.4.0 because the defect makes runtime screenshots unreliable as visual regression, UI verification, and audit evidence on supported macOS Retina environments.
