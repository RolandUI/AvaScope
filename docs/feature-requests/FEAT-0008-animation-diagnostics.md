# FEAT-0008: Animation diagnostics

- Status: `Measured real-time contract; combined validation pending`
- Implementation Status: `Artifacts shipped in v0.3.0; owner revised timing acceptance in #201 on 2026-09-28`
- Priority: `P4`
- Stored: `2026-06-09`
- Source Order: `8`
- Area: PreviewHost rendering, runtime inspection, protocol, CLI, MCP

## User Need

Agents and developers should be able to evaluate Avalonia animation behavior without relying on a human-only live designer preview.

## Desired Behavior

AvaScope should expose measured animation inspection features such as:

- sampling screenshots at explicit animation time offsets, for example `0ms`, `150ms`, and `300ms`
- returning a bounded frame sequence or visual strip for human review when requested
- reporting structured animation metadata where public Avalonia APIs make it reliable: target node, animated property, duration, easing, start value, current value, and final value
- surfacing animation-related diagnostics such as layout shift, clipping during motion, unstable final state, or nodes that disappear unexpectedly during a transition

The feature should favor agent-readable state and explicit timing windows over a Rider-style continuous live animation preview.

## Acceptance Criteria

- CLI and MCP can request animation sampling for a preview or running bridge session without changing existing screenshot behavior.
- Results include bounded structured metadata plus file paths for generated frames or strips.
- One preview instance plays the real animation. Requested offsets, measured intervals, trigger provenance and tolerance are distinct; late or uncertain timing is inconclusive. Runtime input recording can assert motion, intermediate/final geometry and observed stability in bounded windows.
- Unsupported or unreliable animation metadata is reported as `unknown` or `not_available` rather than guessed.
- PreviewHost isolation and local-only runtime safety boundaries are preserved.

## Non-Goals

- No private Avalonia runtime hooks or designer-specific APIs.
- No requirement to match Rider's full interactive animation designer experience.
- No long-lived preview host process unless lifecycle, close, TTL, crash, and cleanup semantics are separately designed.

## Notes

The artifact and diagnostic workflow shipped in `v0.3.0`: bounded frames, frame strips, file-backed timeline viewers, CLI `preview-animation`, MCP `preview_axaml_animation`, pixel-motion diagnostics, explicit `not_available` metadata, and repeated-offset artifact reuse.

The 1.5 stabilization investigation disproved the original deterministic-timing claim on Avalonia 12.1.3. On 2026-09-28 the owner explicitly replaced virtual seeking with measured real-time validation in [#201](https://github.com/RolandUI/AvaScope/issues/201). Avalonia's [headless render timer](https://github.com/AvaloniaUI/Avalonia/blob/12.1.3/src/Headless/Avalonia.Headless/HeadlessRenderTimer.cs) uses a Stopwatch; the [designer](https://github.com/AvaloniaUI/Avalonia/blob/12.1.3/src/Avalonia.DesignerSupport/Remote/PreviewerWindowingPlatform.cs) also uses a real render loop. AvaScope pumps the public dispatcher while waiting for absolute deadlines, without replacing private clocks or interpreting animation XAML.

The preview origin is window attachment/show or an explicit once-only class addition after warm-up. Runtime observation is armed before input; same-process monotonic spans bound the actual input dispatch, tree and rendered capture, with conservative client fallback. Neither animation-frame callbacks nor these rendered captures establish compositor/native presentation. Geometry assertions select phase windows and distinguish passed, failed and inconclusive. Cached duplicate frames do not count as independent stability samples; legacy exact-offset baselines are refused. The default 100ms time tolerance is caller-adjustable and independent of geometry tolerance.
