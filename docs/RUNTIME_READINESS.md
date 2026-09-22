# Runtime readiness and bounded stability

An available bridge, a registered window, a rendered frame and application data readiness are separate observations. All remain opt-in and local. Loading the provider never declares or activates readiness.

`wait_for_state` accepts these additional `waitCondition.kind` values through `run-workflow` / `run_workflow` and `run-scenario` / `run_scenario`:

| Condition | Evidence |
| --- | --- |
| `bridge_ready` | A successful local capabilities request; does not require a responsive UI dispatcher. |
| `frame_ready` | A visible, positive-sized registered window, valid sampled layout and a completed public Avalonia composition batch's `Rendered` task. |
| `application_ready` | The host explicitly declared `ready`. |
| `application_busy` | The host explicitly declared `busy`. |
| `layout_stable` | Consecutive identical bounded layout observations after render fences. |
| `frame_stable` | Consecutive identical hashes of a bounded Avalonia bitmap rendering after render fences. |

Existing `top_level_opened` and `top_level_closed` conditions cover window creation/removal. Legacy waits and screenshot calls are unchanged unless new options are selected. An older bridge reports an unavailable method instead of silently claiming readiness.

## Optional host declaration

After explicit `Bootstrap.Start()`, a host can call the public static `AvaScope.Bridge.Bootstrap.SetReadiness(string state, string? reason)` through reflection, on the Avalonia UI thread. Direct integrations can call `AvaScopeBridgeRuntime.SetReadiness` instead. Allowed states are `starting`, `busy`, `ready`, and `failed`; the bounded reason is at most 512 characters. No declaration means **unavailable**, not ready or busy. Calling the hook does not activate a disabled bridge.

The declaration is application-owned. It does not infer completion from arbitrary background tasks, network requests or animations. Reasons are evidence and pass through the configured evidence redaction policy.

## Scope and timing

Readiness waits need a top-level or declared top-level alias; a selector is optional. A selector narrows layout/frame sampling to the freshly resolved visual subtree on every poll. Stable identity must also remain unchanged. This allows a stable editor to be tested while an unrelated progress animation continues.

`stableSamples` defaults to 3 and accepts 2–10. Existing per-step `timeoutMs` (1–60000) and `pollIntervalMs` (25–5000) apply, plus the workflow deadline and caller cancellation. Samples exceeding 512 nodes, depth 8 or 1048576 physical pixels return a reason and require a smaller scope; truncated samples never establish stability. Layout hashes contain identities, visibility and top-level coordinates rounded to 0.001 DIP. Frame hashes identify their source as `avalonia_render_target_bitmap`; they are not native desktop captures or proof that a window is unoccluded. The render fence establishes completion of a composition batch, not global application idleness. A synchronous host rendering callback cannot be forcibly interrupted by AvaScope.

The structured `waitObservation.readiness` records bridge/window/frame/application states, timestamps, sources, reasons, bounds validity, sample counts, truncation, fingerprints, target and observed backend. Missing metadata yields `semantic_workflow_wait_state_unavailable`; available but unmet conditions yield `semantic_workflow_wait_timeout`. Continuous changes reach the configured deadline.

```json
{
  "action": "wait_for_state",
  "selector": { "name": "EditorPanel" },
  "waitCondition": { "kind": "layout_stable", "stableSamples": 3 },
  "timeoutMs": 5000,
  "pollIntervalMs": 100
}
```

## Startup and screenshots

A scenario may add `"startupReadiness": { "waitForFrame": true, "waitForApplication": true, "waitForStableLayout": false, "timeoutMs": 10000 }`. After attach and window discovery, the same typed waits run before inspection or actions. The conditions share the startup deadline, with at most 100 ms cancellation handoff allowance. Failure preserves observations at `readiness.observations`, marks `failureStage: "ui_readiness"`, skips actions and performs normal owned-process cleanup. Policies must permit `wait_for_state`; session and output ownership restrictions still apply. Application readiness remains optional by default.

For screenshots, set `captureAfterRender: true` on a workflow screenshot step or MCP `screenshot`, or use `avascope screenshot ... --capture-after-render true`. A bounded 1000 ms composition fence and usable-layout check precede capture. The result includes the readiness observation; a failed check creates no screenshot. This is a point-in-time prerequisite, not an atomic promise that a continuously changing app will stop changing before capture.

The shared external-provider platform matrix exercises delayed reflected startup, declared readiness, frame readiness, scoped layout stability and capture-after-render through real CLI and MCP processes on Headless, Win32, managed X11 and macOS. Headless tests additionally cover missing hooks, busy/ready/failed transitions, cancellation, bounded sampling, hidden windows and continuous animation outside/inside the requested scope.
