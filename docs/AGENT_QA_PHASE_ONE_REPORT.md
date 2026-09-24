# First-phase native agent QA campaign

Status: **in progress**, 2026-09-24. Tracking issue [#166](https://github.com/RolandUI/AvaScope/issues/166). This report separates actual agent exploration from scripted checks. The owner expanded the original intake-only phase to include fixes for existing and newly discovered defects, improved regression coverage and repeated comprehensive testing. Original failures remain recorded separately from post-fix verification. Version changes and release remain outside scope.

Latest checkpoint: infrastructure #162/#163 and defects #158/#159/#160/#167/#168
are completed. #169 is implemented at `5bbaad1`, passed a fresh native agent
round M100–M134 and full Debug/Release (792 passed each, five native-only skips);
updated CI `36014239916` is running. The preceding full CI `36009001380`
passed every job, including native tests 5/5 on each platform and actual X11
3840×2160 paired CLI/MCP capture. #157 and #161's remaining native 2× acceptance
are blocked on an accessible Retina environment. The following ledger retains
historical intermediate results; its older pending statements are superseded by
the later dated/source-specific validations.

## Tested environment and infrastructure

Shared fixtures: `0957350`; retained lab: `f18f8c8fb74cce2c024c3fc3d509226bee033469`. Product assemblies remain AvaScope 1.5.0. The lab copies and hashes the CLI/MCP/client/host/provider selected for each session. See [lab instructions and task charters](AGENT_QA_LAB.md).

| Environment | Observed backend/scale | Evidence and interpretation |
| --- | --- | --- |
| Local Windows 10.0.26200 x64, .NET 10.0.7, Avalonia 12.1.3, Segoe UI | Win32, 1×, 1120×800 DIP client, 1920×1080 screen | Actual agent-operated native app, independent Computer Use screenshots, individual real MCP calls and CLI, app-owned journal. |
| Hosted Windows | Win32, 1× | Scripted two-host lifecycle/capture/reset/failure/lease checks passed. |
| Hosted Ubuntu 24.04 | X11, 1× | Same scripted lifecycle checks passed on explicit Xvfb desktop. |
| Hosted macOS | macOS, 1× | Same scripted lifecycle checks passed; **does not establish Retina coverage**. |
| Native Retina at 2× / Full HD DIP | Unavailable in the selected environments | Blocked for #157/#161 native visual acceptance; no simulated-scale substitution. |

Hosted evidence: [CI run 35991939691](https://github.com/RolandUI/AvaScope/actions/runs/35991939691), artifacts `agent-qa-lab-Windows`, `agent-qa-lab-Linux`, `agent-qa-lab-macOS`. These artifacts explicitly contain `agentExploration: false`. The broader pre-existing CI jobs are still being checked.

Local validation: Release suite **745 passed, 0 failed, 5 explicitly gated native-only skips**; the two new fixture tests are included. Existing complex CLI workflow passed two success runs and the deliberate redacted failure/retention/cleanup path. `artifacts/agent-qa/lifecycle-validation-02/lifecycle-summary.json` records both integrations' two public MCP toggle/screenshot/reset cycles, preserved invalid-target errors, owned termination and an independent 30-second lease-expiry/recovery run. All passed. Earlier lab-development failures (output containment, pipe capture, expected doctor origin, an incorrect test-client result field) were corrected in infrastructure and were not filed as product defects.

## Agent exploration ledger

The local direct session is `artifacts/agent-qa/campaign-direct`, clean fixture/lab commit `f18f8c8`. `Dnn-*.arguments.json` and `Dnn-*.result.json` identify each agent-selected operation. `calls/` retains full original MCP envelopes and before/after journal snapshots, including errors; `calls.jsonl` records timing/outcome. A tool call is not itself a test case, and expected rejection is not a test failure when the diagnostic/state contract passes.

| Case | Evidence | Result / independent observation |
| --- | --- | --- |
| Discover profile, enable notifications, rename Ada to Grace Hopper, navigate away/back | D01–D04, D06, D10–D14 | Passed using legacy query targets. Journal: notifications=true, toggleCount=1, name=Grace Hopper, textChanges=1, persisted after navigation. |
| Desired state repeated and exact text-edit replay | D05, D07 | Passed: already_satisfied/zero new dispatch; replay=true and textChanges remains 1. |
| Old text revision and conflicting request-ID payload | D08–D09 | Passed negative checks: structured text_edit_stale / text_edit_id_conflict; no extra modification. |
| Complex XAML/template/local-transform text against native pixels | D12 rendering PNG and independent D12-native-rendering.png | Passed at Windows 1×: 12/18/30 DIP reference text, nested card text and 1.15× local transform align after excluding native chrome. No inflated text or unexpected clipping observed. Retina acceptance remains blocked. |
| Case-distinct button IDs and CLI parity | D15–D19 | **Failed, #160**: both exact and wrong-case IDs match both buttons; semantic invoke fails ambiguity with no dispatch. CLI agrees. |
| Recovery from ambiguous identity using observed node IDs | D20–D21 | Workaround passed: independent counters 0/0 → 1/0 → 1/1. Original exact-ID case remains failed. |
| Read-only editor: read, revision-safe insert rejection, legacy clear rejection, reread | D23–D26 | Passed: text_edit_read_only / unsupported_input_action; same text and revision after attempts. |
| Structured selector target passed intact to desired-state/edit tools | D28–D35 | **Failed, #158**: stable, unique target with selectionRevision rejected as runtime_input_target_stale. Fresh re-resolution, with/without policy and explicit selector depth all fail; toggle count stays 1. Even edit_text(read) fails for the similarly selected editor. Legacy target still works. |
| Invalid insert diagnostic and exact replay, then corrected insert | D36–D40 | **Failed diagnostic, #159**: missing start yields only opaque MCP invocation error, including replay. CLI explains required offset. Valid start=12 succeeds, name becomes Grace HopperX once. This does not prove valid-offset inserts fail on Windows. |
| Equivalent selector forms return incompatible visual coordinates | D41–D42, D45–D47 | **Failed, new #167**: legacy toggle bounds (44,236,150,32) versus selector (0,44,150,32). Geometry-pinned read-only pick at legacy center hits the toggle; selector center hits a root panel. |
| Disabled editor read and rejected clear/desired-text operations | D48–D50 | Passed: reading available; mutation rejected with zero dispatch and bounded blocker/next-action diagnostics. |

Exploration corrections are retained: D27 requested unsupported projection attributes and received a structured validation error; D43/D44 omitted required picking generation/geometry context and received opaque MCP errors, then D45–D47 used the documented geometry handshake. These corrections are not hidden successes or additional distinct bugs; the lost typed-request diagnostics are noted under #159.

Additional completed exploration: D51–D59 replaced the editor, rejected its genuine stale target before input, inserted `Á😀 ` in the fresh editor exactly once, preserved replay semantics, and rejected an offset inside the emoji surrogate pair. D60–D64 checked focus inspection, synthetic forward/back traversal, stale focus rejection and real native Tab traversal. D65–D74 inspected window geometry, opened/closed/reopened child windows and read their rendered/native images. The journal preserved the Unicode profile and notification state.

The latter exposed a fixture error: direct integration manually retained child registrations after native close. The fixture now relies on the desktop lifetime for child discovery/removal, with a new two-integration native close/reopen/reset lifecycle check. This is a test-environment correction, not a new product defect. The original direct campaign session was stopped through verified owned termination; its evidence is retained.

This initial ledger is historical. Later sections record the remaining appearance, loading, virtualization, modal/popup and standalone journeys; unresolved work is the Retina/pixel-budget issues and final combined validation.

## Defect inventory and current validation

| Ticket | Confirmed finding / uncertainty | Current state |
| --- | --- | --- |
| [#157](https://github.com/RolandUI/AvaScope/issues/157) | Repeated opacity/transform inflation reproduced on Avalonia 12.1.0 and 12.1.3, including bridge screenshots. | Active: recorded-visual mitigation and regressions; actual native macOS 2× acceptance remains outstanding. |
| [#158](https://github.com/RolandUI/AvaScope/issues/158) | Incomplete selector coverage was mislabeled as stale. Customer coverage metadata was absent. | Fixed `e32139f`, completed; complete targets, precise refusal and dispatch counts verified. |
| [#159](https://github.com/RolandUI/AvaScope/issues/159) | Expected constructor/domain failures became opaque MCP invocation errors. Original customer payload was absent. | Fixed `74c07e6`, completed; structured invalid arguments and supported insert/replay verified. |
| [#160](https://github.com/RolandUI/AvaScope/issues/160) | Exact AutomationID matching incorrectly ignored case. | Fixed `173b42b`, completed; independent counters and CLI/MCP parity verified. |
| [#161](https://github.com/RolandUI/AvaScope/issues/161) | 4 Mi-pixel cap rejected Full HD DIP at 2×. | Implemented `295939d`; headless 2× and native X11 4K pass. Native Retina acceptance remains blocked. |
| [#167](https://github.com/RolandUI/AvaScope/issues/167) | Structured visual queries returned bounds in the wrong coordinate space. | Fixed `cfa0d3d`, completed; nested/scroll/transform and geometry-pinned picking verified. |
| [#168](https://github.com/RolandUI/AvaScope/issues/168) | Repeated logical object identity made popup queries fail over IPC. | Fixed `d1028c3`, completed; both integrations on all three native backends verified. |
| [#169](https://github.com/RolandUI/AvaScope/issues/169) | Oversized privacy rectangle overflow skipped masking while reporting success. | Completed `5bbaad1`; native MCP/workflow/CLI, full Debug/Release and all jobs of CI `36014239916` pass. |

No product bug had been fixed in the original campaign evidence above. Subsequent fixes and reruns will be identified by commit/session. A workaround never changes the original failed case to passed.

## Stabilization reruns

The fixture correction `936dbed` passed `lifecycle-validation-03` locally and [focused CI 35996074830](https://github.com/RolandUI/AvaScope/actions/runs/35996074830) on native Windows, X11 and macOS. Downloaded lifecycle summaries confirm both integrations, child close/reopen/reset, preserved negative evidence and lease expiry; all observed scales are 1×. The earlier full baseline CI `35991939691` also completed successfully.

**#160 fixed in `173b42b`.** Exact AutomationIDs now compare ordinally in bridge queries, pseudo-state re-resolution and audit uniqueness. The new exact-ID/audit tests failed before the fix; all 14 relationship/query, audit and matrix tests then passed, including actual CLI/MCP stdio and duplicate rejection. Native agent reruns used clean `173b42b` in `campaign-direct-02` (D202–D206) and `campaign-standalone-01` (S01–S08), with a freshly packaged hash-verified provider. Both resolve each case-distinct ID separately, reject `EXAMPLE_BUTTON_KEY_A`, and execute exact-selector actions with journal counters 0/0 → 1/0 → 1/1. Standalone CLI agrees. Native and rendered identity images were opened: both show `a=1; A=1`, correct text/layout and the same list region after excluding window chrome. Native sampling follows rendering and is not atomic. Direct owned termination passed; standalone remains retained for the next investigation.

D201 was an unsuccessful text-filter discovery attempt on tab headers, followed by the observed native AutomationID. The three D203 queries have distinct original `calls/` transcripts; two convenience filenames differed only by case on Windows, so those aliases are not the authoritative record. No request/response in `calls/` was overwritten.

**#167 implemented in `cfa0d3d`, native matrix pending.** The new nested/scroll/transform test failed before the fix; 15 query/picking tests pass. `campaign-direct-03` D301–D305 verifies public MCP/CLI bounds agree at (44,236,150,32); a geometry-pinned pick at the returned center contains the expected toggle. Native initial pixels were opened. Standalone S10/S11 preserved the original mismatch with complete query coverage before the fix. CI `35998419321` runs the fixed coordinate and exact-ID cases on both native integration modes across three backends.

**#158 diagnosis refined by controlled native contrast.** D306–D308 at `cfa0d3d` found that depth 24 returns partial coverage (`depth_limit`, 152 visited nodes), whose safe refusal was incorrectly labeled stale; depth 32 returns complete coverage (168 nodes), toggles once and returns already_satisfied with zero dispatch on repetition. D309–D311 similarly reads and edits the name with the entire complete selector target, producing Grace Hopper exactly once. This proves the locally reproduced failure is missing coverage classification/recovery guidance, not a changed toggle identity. The original macOS report's coverage was not supplied. New regressions require a distinct incomplete-selection diagnostic and preserve genuine stale/ambiguity protections; partial coverage never becomes authorization to act.

Appearance exploration on the same pinned `cfa0d3d` app: D312–D314 selected Rendering, Dark and hu-HU. Native and RTB images were opened; the Hungarian heading, multilingual nested 14/16/18-DIP text, 1.15× local transform and 12/18/30 reference sizes align at actual Windows 1× after excluding chrome. Two consecutive rendered PNGs are byte-identical. The independent journal retains Grace Hopper, one text change and one toggle. This does not satisfy native Retina acceptance.

### MCP validation fix and additional geometry finding

- #159: actual stdio regression failed before the fix because a missing insert `start` produced no structured content. The adapter now prevalidates Protocol DTO arguments using the SDK serializer before invocation, returns `invalid_mcp_arguments` with zero-dispatch metadata, and never reclassifies post-invocation uncertainty. Only the exact static insert-offset explanation is allowed through; converter/nested-constructor values are omitted. Eleven malformed cases, valid read/insert/replay, missing pick generations and stderr privacy are covered. The text-edit/MCP group passed 13/13. Hosted two-host native insertion regressions are added; execution is pending.
- #158 native run `35999831206` passed all six Direct/Standalone × Win32/X11/macOS combinations at observed 1×, including independent counters for partial-coverage refusal, complete-target dispatch and already-satisfied zero dispatch. Downloaded summaries are under `artifacts/agent-qa/hosted-regressions-158`. This does not infer the absent customer coverage metadata or establish 2× Retina validation.
- Direct D315–D317: the Full HD button requested 1920×1080 DIP, but Windows supplied a 1920×1061 client on the local 1920×1080 display. The fixture journal incorrectly overwrote the requested height with the observed 1061. This fixture defect is recorded in #162; the application was restored to compact size. Full HD native coverage is not claimed.

- #162 geometry regression failed before the correction (requested width became observed 1300 in the simulated clamp) and all three fixture tests pass afterward. The journal now retains scene intent independently of `Width`/`Height`; reset and the next toggle restore compact intent. The native lab summary records requested and observed Full HD geometry separately, without counting clamped geometry as Full HD coverage.

### Direct native campaign continuation (2542edc)

- D401–D402 preserved Full HD intent despite OS clamping, restored compact geometry and completed the delayed load. D403–D407 verified the #159 error explanation and one insert/exact replay via actual MCP and app textChanges=1.
- D408 invoked load then reset within 56 ms; the independent journal recorded load_started then reset with no later load_completed, and Ready/Ada remained stable. D409–D413 used real native click + End: Record 200 was visibly selected, journal selectedRow=199, complete structured query found that one realized item, and Record 001 had no realized match. D410 initially used an unsupported controlType field; D412 corrected this to the published nodeType field. That agent request correction is not a product defect.
- D414–D419 opened a real modal, confirmed active child/inactive owner, and verified blocked owner input with zero dispatched events. The first close workflow omitted its required destructive-action permission and was safely rejected; the explicitly authorized fixture close succeeded.
- D420–D430 opened and visually inspected a native popup. Its separate unregistered visual root was correctly absent from owner visual lookup and explicitly identified by geometry-pinned pick_native_popup_present. Logical lookup exposed new #168: repeated identical node IDs and repeatable empty IPC on structured queries. Native button closure succeeded and journal popupOpen became false. The dark popup card's close-button contrast remains a fixture presentation follow-up in #162.
- Native screenshots are retained as D402-native-loaded.jpg, D411-native-last-row.jpg, D415-native-modal.jpg, D420-native-popup.jpg and D430-native-popup-closed.jpg. A stale JavaScript binding initially reused the previous app window; fresh uniquely named window bindings corrected this without interacting with unrelated windows.
- Hosted `36001930848` passed #159 and size-intent regressions on Direct/Standalone × Win32/X11/macOS. Downloaded summaries are under `artifacts/agent-qa/hosted-regressions-159`. Only X11 provided the requested 1920×1080 client at 1×; hosted Windows supplied 1028×749 and macOS 1920×649 at 1×. These are explicit native Full HD/Retina gaps, not passing Full HD evidence.
- #168: the real TabControl + Popup regression failed before the correction with two identical logical node IDs. Snapshots, structured queries and logical ID lookup now deduplicate by object reference while preserving first traversal paths. The 52-test query/desired-state/bridge group passes, including actual CLI/MCP and distinct duplicated AutomationIDs. Both-host native regressions were added; execution is pending.

### Standalone native campaign (d1028c3)

`campaign-standalone-02` used fresh CLI/MCP and the separately packaged provider (manifest SHA-256 `1e8f0826094199da572fd1425709cbf3d1d6e77911a8ad1321c239d027550fb9`), native Win32 at observed 1×. The source-linked application has no AvaScope project/package reference. Session `99a17d0633a7476cb05b43652311e658` was stopped through verified owned termination after the following agent-selected checks:

- S201–S210: complete structured targets toggle once, return already-satisfied with zero new dispatch, reject an insert missing `start` before dispatch, insert `Á😀` once (AdaÁ😀; UTF-16 caret 6), replay exactly, and reject conflicting request IDs and stale revisions. Independent counters remain one toggle/one edit.
- S211–S216: read-only/disabled text can be read but mutations are refused; replacing the editor rejects the old target and preserves text under a fresh identity.
- S217–S224: independently viewed native and rendered light/en-US and dark/hu-HU images agree for complex text, templates, nested layout and local transforms at 1×. Repeated dark rendered PNGs are byte-identical. Full HD intent remains 1920×1080 despite observed 1920×1061; compact restoration works. Case-distinct IDs invoke separately; wrong case has no match.
- S225–S230: native click/End visibly selects Record 200, journal selectedRow=199, and complete structured lookup finds one realized row. A real modal preserves the Unicode profile, blocks owner input with zero events, and closes through the explicitly permitted fixture action.
- S231–S244: popup logical flat/MCP/CLI queries each return one identical object identity with complete coverage; the same holds after native closure. Focus inspection agrees with native UIA; synthetic Tab and owned-window native Shift+Tab move to Reset and back. Load followed immediately by reset remains Ready/Ada with zero counters and no subsequent load_completed. The final native image was viewed and retained.
- Observation returns an image and truthfully reports the requested depth-8 tree as partial. Application readiness is `not_declared` in this standalone integration, not inferred from the independent journal. Paired capture on this shared desktop preserves rendered evidence while refusing native capture with `native_screen_scope_denied`; no host-wide capture grant was added.

The #168 hosted run `36004045571` verified unique open-popup flat/structured identities on all three direct native backends, then failed because the new script tried the owner's actionable Reset while a native popup was open. The script now queries the closed popup before opening it and verifies both journal states; this preserves the existing popup input boundary. This was a test orchestration correction, not a new product query failure. The first run never reached standalone. Corrected run `36006539457` passed all six Direct/Standalone × Win32/X11/macOS combinations plus expiry; downloaded summaries explicitly report logicalPopupIdentityVerified=true. #168 is completed. All actual scales remain 1×.

### Bounded 2× capture correction (#161)

The added 1920×1080 DIP/2× and exact 8 Mi-pixel headless cases both failed on the
previous 4 Mi-pixel guard. Shared bridge/native/client limits now admit 8 Mi pixels
with a 16,384 dimension bound; the 256 KiB masked PNG and 1 MiB IPC response bounds
remain unchanged. macOS native requests preflight the observed display scale and
recheck callback dimensions before copying. Paired capture and comparison retain
their sequential buffer lifetimes, native callback slot and explicit processing
deadlines; resource accounting and OS/codec limitations are in SCREEN_EVIDENCE.md.

The focused capture/client/capability group passed 67 tests, including full-size
rendered evidence, unchanged DIP geometry, truthful authorization refusal,
oversize rejection/recovery and controlled-IPC 4K comparison plus invalid-half
preservation. These are headless/client regressions, not native Retina evidence.
The final 20-test capture group also passed the dense-image masking/byte-budget
case: unmasked high-entropy PNGs are rejected, while the fully masked result is
accepted without exposing the original. A real isolated X11 4K CLI/MCP pair is
added and pending hosted execution. True macOS 2× capture remains unvalidated.

### Final fixture corrections (#162)

The source-linked standalone QA host now forwards its existing readiness event
through the provider's public reflection bootstrap, matching direct integration
without an AvaScope package/type dependency. The native lab requires declared
application readiness in both screenshot cycles and waits for app-ready after
loading, then checks the independent Loaded 200 records journal state. The popup's
fixed dark card now has a matching local dark theme, preserving readable button
states under either parent theme. Release solution build passed without warnings
or errors; all three fixture tests passed. Fresh native inspection and combined
CI are pending; the visual correction does not need an implementation-mirroring
unit test.

Fresh native standalone `campaign-standalone-03`, clean `7d86ab3`, used the newly
verified provider manifest `abfa258a46f331f0f69a7cdde2feb4bea6b9ee4a91ed5041ac7e523832076cf8`
on Win32 1×. S301–S306 verified host-declared readiness, delayed load completion,
load/reset cancellation and ready recovery. Independently viewed native popup
images in both light and dark parent themes show readable button text; native
closure works. S307–S314 rechecked complete-target desired state, zero-repeat
dispatch, safe missing-offset diagnostics and one Unicode insert/exact replay.
S315–S318 preserved state through appearance/navigation and case-distinct actions;
viewed native/RTB dark Hungarian complex text, clipping, 1.15× transforms and
12/18/30-DIP references agree at 1×, and repeated PNGs are byte-identical.
S319–S322 rejected the replaced editor's stale target and recovered the preserved
text through a fresh identity. S323–S324 preserved rendered paired evidence while
explicitly refusing native desktop pixels without the host grant. S325 restored
seed state (Ready/Ada, zero counters, no children/popup), then owned termination of
PID 52112 succeeded. No new defect was found in this native rerun; it is not a
Retina pass. Full Release validation at this source passed 771 tests with five
explicit native-only skips; full Debug and combined hosted gates are pending.
## Screenshot privacy-mask overflow (#169)

The resource-boundary audit found another defect after the S301–S325 round.
Native standalone `campaign-mask-overflow`, clean `0745a3b` (product code
`7d86ab3`), Win32 1×, called public MCP `capture_screen` with a valid rectangle
`{x:1,y:1,width:2147483647,height:2147483647}`. M002 returned `captured` and
`masking: applied`, but the independently opened 1120×800 PNG remained readable.
M003's ordinary equivalent rectangle (1,1,1119,799) correctly blacked the image
except its first row/column. Both original transcripts/images are retained;
owned termination of PID 11836 succeeded.

The shared masking code added Int32 rectangle endpoints before clamping, so
horizontal or vertical overflow could silently skip the mask. Eight of twenty
new pixel-by-pixel memory/file cases failed before correction; ordinary, huge
non-overflowing and off-image controls passed. The correction widens endpoint
addition before clamping. A separate bridge/client regression checks masking
before IPC and in the final saved PNG. The evidence/capture/client group passed
102 tests; the final evidence/capture group, including that additional IPC
regression, passed 64 tests. Release build passed without warnings/errors. Fresh
native verification is pending. This finding supersedes any interpretation of the preceding passing
round as a completed clean campaign.

The preceding code baseline's full Debug and Release each passed 771 tests with
five explicit native skips and zero build warnings/errors. CI `36009001380`
passed all six native QA combinations and its Windows build/test/package gate;
macOS/Linux final gates remain running. All six lab summaries explicitly verify
host-declared readiness and unique popup logical identities. All observed scales
remain 1×.

### Native verification after #169 (5bbaad1)

Clean `campaign-mask-fixed` uses the verified standalone provider manifest
`fee8d6a012e4f379c8509f6b0fc0d33fa30c150ac8cf997695c151ce6b1f2e81`,
Win32 1×, session `6e57c2982afb46ec9abb7f392172ab82`.

- M100–M108: independently viewed the actual initial window and fixed masked
  PNG. The previously failing oversized rectangle now produces exactly the
  expected black intersection, with all 896,000 pixels checked and zero
  incorrect pixels. Ordinary-mask control, public workflow file masking and
  CLI capture produce byte-identical masked PNGs.
- M109–M111: removing the policy restores normal readable capture; a rectangle
  wholly outside the image changes zero of 896,000 pixels. PNG encoding bytes
  differ after re-encoding, so decoded pixels, not hashes, establish this case.
- M112–M119: complete-target desired state changes once and repeats with zero
  dispatch. Invalid insert arguments fail structurally before dispatch; valid
  insertion produces `Ada Á😀`, exact replay is true and app textChanges=1.
- M120–M127: native and rendered dark/hu-HU complex text were opened and agree
  at 1×, including nested templates, 1.15× local transform and 12/18/30-DIP
  references. Case-distinct actions increment separate counters once. Editor
  replacement rejects the old identity and a fresh query recovers the text.
- M128–M134: open popup logical query has one identity with complete coverage;
  native popup text is readable and native closure works. Load followed by
  reset remains Ready/Ada, all counters zero, no late load completion, no child
  windows/popup, declared readiness ready. Final native image was opened.

No new failure was found in this post-fix native round. Owned termination of
PID 3488 succeeded. Full Debug and Release at `5bbaad1` each passed 792 tests with
five explicit native-only skips and zero build warnings/errors. Updated full CI
`36014239916` completed successfully in every job. Downloaded Windows/Linux/macOS
foundation evidence and all six native lab summaries were inspected. Windows and
macOS each passed 792 full-suite tests plus five explicit native skips. Native
provider/onboarding/platform gates passed, including paired X11 CLI/MCP
3840×2160 comparison of all 8,294,400 pixels. #169 is completed.

The preceding combined CI `36009001380` completed successfully at `7d86ab3`.
Downloaded native logs confirm 5/5 on Windows, Linux and macOS. Linux's isolated
4096×2400 X11 desktop supplies actual 3840×2160 rendered/native frames through
both packaged CLI and MCP, each comparing all 8,294,400 pixels while preserving
requested client geometry. Its observed scale is 1×. Native macOS paired capture
also passes at 1×; neither substitutes for the remaining #157/#161 Retina case.

### Avalonia 12.1 opacity/transform reproduction (#157)

The supported runtime line is 12.1.x: normal hosts/tests use 12.1.3; the bridge
compiles against the explicitly declared 12.1.0 minimum. An isolated probe ran
against both actual loaded assembly versions, 12.1.0.0 and 12.1.3.0. This is not
an Avalonia 11 compatibility test. The earlier 11.3.12 version appears only in
the historical upstream report [Avalonia#20693](https://github.com/AvaloniaUI/Avalonia/issues/20693).

With full opacity handling and repeated opacity/transform scopes, a 2× RTB
draws the first Segoe UI glyph band 26 pixels high and later bands 50 pixels
high, shifting later rows outside the image. Disabling full opacity handling
is only a diagnostic control: all five bands are 26 pixels high. Both 12.1
patches reproduce the same defect. Images were independently opened. Evidence:
`artifacts/agent-qa/opacity-probe/evidence/measurements.json` and
`evidence-12.1.0/measurements.json`, plus `visual-*-bridge.png` and experimental
`visual-*-visualbrush-native-dpi.png` comparisons in those directories.

The redistributable `QaOpacityTransformControl` now appears on the lab's
Rendering tab. It uses only public Avalonia APIs, five identical 18-DIP text
rows (12 DIP × 1.5 transform), overlapping group-opacity rectangles and a
clipped rectangle. The first bridge regression run failed all six 1.5×/2×
cases while all three 1× controls passed. It exercises screenshot, observe
and capture_screen through local IPC, using the independently captured
headless compositor frame as the text-region reference. It also checks
simple → complex → complex → simple navigation, unchanged live frames/layout,
repeated bytes and readiness-frame hashes. This is diagnostic compositor
evidence, not native Retina evidence.

The mitigation records the presentation visual through public VisualBrush
before rasterizing into the original full-resolution/DPI RenderTargetBitmap.
It shares this path across screenshot, observation, paired rendered capture
and readiness hashing. It keeps group opacity and clipping; no hidden DPI
transform is restored during live visual traversal. Avalonia 12.1.3's
[VisualBrush source](https://github.com/AvaloniaUI/Avalonia/blob/12.1.3/src/Avalonia.Base/Media/VisualBrush.cs)
uses the same immediate visual traversal with a recording context. The
original issue's exact customer root cause and macOS native 2× visual
acceptance remain unproven; #157 must stay open pending that comparison.

The final focused rendering/capture/observation/readiness/evidence group passes
82/82 after the mitigation, without build warnings/errors. The minimum 12.1.0
bridge probe also produces the same five correctly sized rows as its isolated
recorded-visual control (identical SHA-256). New full-suite/native validation
is pending; the previous successful CI covers the earlier `5bbaad1` baseline.
