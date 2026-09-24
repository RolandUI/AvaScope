# Agent QA plan for v1.5.1 stabilization

Status: planned on 2026-09-24; not an implemented lab or validation result. [Milestone v1.5.1](https://github.com/RolandUI/AvaScope/milestone/20) and [release tracker #165](https://github.com/RolandUI/AvaScope/issues/165) own the scope. Issue acceptance criteria are authoritative; this document records the design and rationale.

The user subsequently authorized a first phase: implement #162/#163, execute the detailed exploratory campaign [#166](https://github.com/RolandUI/AvaScope/issues/166), and record bugs. Product bug fixes and release execution require the later phase. The broader candidate/release guidance below is future context, not authorization to publish during this work.

## Observed coverage gaps

The 1.5.0 report describes five failures in a native macOS 2× standalone-provider app. They are recorded in #157–#161 with reproduction steps, expected/actual behavior and validation requirements. The report does not include the exact Avalonia patch or the complex view's source, fonts and templates; establish those while reducing the reproduction. Do not infer a single Retina root cause for all five defects.

Existing infrastructure is substantial: [agent test profiles](AGENT_TEST_PROFILES.md), [host fixtures](TEST_FIXTURES.md), [run recovery](RUN_RECOVERY.md), [native platform gates](NATIVE_PLATFORM_MATRIX.md), [workflow export](WORKFLOW_EXPORT.md), `eng/test-agent-recipes.ps1`, `eng/test-complex-workflow.ps1` and standalone-provider/packaged tests. Extend these pieces.

Two concrete gaps were found during intake:

- `samples/AvaScope.ComplexWorkflowApp/Program.cs` always calls `UseHeadless`, even when its scripts execute on a native OS worker. Its success does not establish native complex-screen fidelity.
- `eng/test-macos-runtime.sh` validates nested screenshot placement by sampling a colored region. That assertion and pixel dimensions do not verify the reported text sizes against native pixels.

Avalonia's [headless documentation](https://docs.avaloniaui.net/docs/testing/setting-up-the-headless-platform) explicitly distinguishes its in-memory platform from native windowing. Headless + Skia remains useful for fast deterministic regressions. A native backend defect also needs native execution and an independent observable result.

## App corpus: two existing hosts

Implement [#162](https://github.com/RolandUI/AvaScope/issues/162) by extending the existing samples:

| Host | Purpose | Minimum additions |
| --- | --- | --- |
| `AvaScope.ComplexWorkflowApp` | Realistic package-integrated agent target | Explicit native/headless modes; XAML/template-rich screens; resettable scenarios and app-owned state evidence. |
| `AvaScope.StandaloneHost` | Minimal external-provider deployment target | Relevant bug scenes through the shipped reflection loader; keep no AvaScope package/project reference. |

Use one coherent realistic app with scenario pages, rather than one executable per defect. Keep the minimal host separate because it exercises a genuinely different integration boundary. Add further hosts only for a concrete unsupported test topology. Preserve current sample workflows and avoid an unrelated refactor.

Initial scenes cover nested localized text/presenters/templates, inherited transforms and clipping; stable checked-state controls; writable/read-only text; case-distinct IDs; and a 1920×1080 DIP client area. Exercise simple → complex → simple navigation, repeated capture and a bounded asynchronous state change. Keep existing popup/multi-window/drag scenarios. Theme and locale variants should use representative fixed values, not an unbounded Cartesian product.

Each scene declares its initial state, fixture version/seed, readiness, selectors and expected postconditions. An app-owned read-only state/event journal supplies independent values such as final text and toggle count. Preparation/reset may establish initial state; it must never perform the action being tested. Use the existing opt-in fixture contracts and test data only.

## Reproducible agent workspace

Implement [#163](https://github.com/RolandUI/AvaScope/issues/163) as a small orchestration extension over current profiles and run ownership. Provide a documented one-command entry point; its command name is an implementation decision, not a currently available API.

The lifecycle is: validate environment → launch owned app → prepare deterministic scene → wait for readiness → return session and evidence location → inspect/act/verify → reset/replay → clean owned resources. Offer a bounded scripted mode and a retained interactive mode with explicit cleanup and expiry/recovery. Store manifest/run/temp data per run and pin the exact provider/package identity.

The agent needs the application session, selected top-level, scene/reset instructions, actual environment metadata, viewable images and logs. It should be able to reproduce a failed scripted scenario in the retained session without editing customer code or reconstructing setup by hand. Native screen/input permissions are preflight requirements, never silently bypassed.

| Lane | Required environment | Interpretation |
| --- | --- | --- |
| Fast regression | Headless + Skia, deterministic 1×/2× fixtures | UI logic, contracts and rendering regressions; does not prove native desktop behavior. |
| Windows native | Owned interactive desktop, observed 1×/2× as supported | Win32 UI, input and capture; never change the user's global display settings. |
| Linux native | Existing managed X11; current controlled Wayland lane | Preserve the documented capability boundaries; record backend and actual scale. |
| macOS Retina | Logged-in native desktop, actual `RenderScaling=2`, room for a 1920×1080 DIP client area, required Screen Recording/Accessibility access | Mandatory reproduction and full-size native/rendered text evidence for this release. |

A `macos-latest` job name is not proof of Retina, usable desktop geometry or capture permission. Reuse a hosted worker only when it reports the required capabilities; otherwise a suitably configured Mac worker is a recorded dependency. This plan does not assert such a host is already available, require a purchase or expose AvaScope remotely. Agents/scripts execute locally on each selected host and retain artifacts through the existing workflow.

## Public workflow and independent checks

Implement [#164](https://github.com/RolandUI/AvaScope/issues/164) using the existing real MCP client and recipe/scenario infrastructure. Exercise individual public MCP tools through the packaged stdio server, not only a `run_scenario` wrapper or direct Bridge methods. Repeat the same cases through CLI and both integration modes.

The core regression sequence is exact selector → fresh-target `ensure_state` → revision-safe `edit_text` → exact idempotent replay → `observe` screenshot → full-size paired capture. Preserve and reuse the actual returned target and revision. Assert app state and dispatch counts; include already-satisfied, stale, rejected-policy, readonly and wrong-case negative cases. Falling back to semantic toggle or `key_text` records a defect in the original operation even if the user's broader task can continue.

Use three complementary observations:

1. Structured operation/diagnostic responses establish the advertised contract.
2. App-owned state/journal establishes whether the requested change occurred exactly once.
3. Native pixels establish what the user actually sees; compare against the AvaScope-rendered client area.

For images, wait for a stable scene, record capture times/skew, align client areas, and check text bounds, clipping and selected regions with documented antialiasing tolerance. Keep baselines separate by OS/backend/scale/font/theme. A native reference must not come from the same RenderTargetBitmap route being tested. Keep an independent test-owned OS capture path available when investigating the product's capture path itself. Do not treat native/rendered images as pixel-identical across different platform/font contexts.

Preserve full-resolution text regions even when a report preview is downsampled. Do not resize the application to fit the evidence limit. New baseline images require an explicit recorded review; never automatically accept the candidate's output. Prove the gate catches deliberately oversized text, wrong app state and opaque MCP failure before relying on it.

## Development and release cadence

| Point | Required work |
| --- | --- |
| Start a defect | Reproduce the exact public journey; retain failing inputs/evidence; reduce to a focused regression before fixing. If not reproducible, retain the uncertainty. |
| During development | Run the focused contract/headless tests and affected MCP journey. Open the native sample for visual/native changes; inspect images and app state. |
| Coherent implementation checkpoint | Run affected native lanes and clean-reset replay at least twice, including failure/cleanup. Capture useful new exploration as a deterministic regression. |
| Before Release Candidate | One consolidated Debug/Release suite; existing Windows/Linux/macOS and Wayland boundaries; exact-version packaged CLI/MCP/provider/installer/manifest checks and dry-runs; mandatory native Retina evidence. |
| Final candidate review | A bounded exploratory agent session on the exact candidate, with native/rendered image review, case results and links to artifacts. Any changed binary invalidates the relevant candidate evidence. |

Exploration is a short charter, not an assertion that an agent's impression proves correctness: discover controls from the UI, repeat actions, navigate away/back, vary supported theme/locale/size, interrupt one flow, then inspect both semantic state and native/rendered images. Record unexpected tool errors, retries and workarounds. A newly found defect gets a ticket and replayable case, not just a chat note.

Measure first-run completion without workaround, failed/blocked cases, fallback/retry counts, duration and cleanup success per build. Use these to find weak workflows; do not use total test count as the release criterion.

## Evidence and release decision

Every run records commit and candidate hashes, AvaScope/.NET/Avalonia versions, provider mode, OS/architecture/backend, input/capture route, DIP/pixel sizes, scale, fonts/theme/locale, scene seed, request/response timeline, app journal, screenshots/diffs and cleanup. Preserve failure artifacts under existing redaction/retention rules.

Report `passed`, `failed`, `blocked` and `skipped` separately. Required missing native 2× coverage is blocked, not success. Generic MCP invocation errors fail the relevant journey. Child process failures, missing images, timeouts and cleanup errors must propagate to the gate.

Close the five defects only after their criteria pass. Move the release to candidate only after all in-scope acceptance and existing [validation](VALIDATION.md) gates pass. This planning task runs no application tests and supplies no native macOS evidence.
