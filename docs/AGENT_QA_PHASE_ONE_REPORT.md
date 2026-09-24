# First-phase native agent QA campaign

Status: **in progress**, 2026-09-24. Tracking issue [#166](https://github.com/RolandUI/AvaScope/issues/166). This report separates actual agent exploration from scripted checks. The owner expanded the original intake-only phase to include fixes for existing and newly discovered defects, improved regression coverage and repeated comprehensive testing. Original failures remain recorded separately from post-fix verification. Version changes and release remain outside scope.

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

Remaining campaign work: theme/locale/size and repeated captures; loading/reset interruption; virtualized rows; modal/popup lifecycle; capture/policy boundaries; repeat representative and failing journeys through standalone integration; fix and rerun ticketed regressions; broader final evidence/cleanup audit.

## Defect inventory so far

| Ticket | Campaign finding |
| --- | --- |
| [#157](https://github.com/RolandUI/AvaScope/issues/157) | Complex native Windows 1× rendering looks correct; native Retina reproduction remains blocked. |
| [#158](https://github.com/RolandUI/AvaScope/issues/158) | Confirmed beyond macOS. Structured selector context is the relevant difference from the passing legacy path. Reproduction added. |
| [#159](https://github.com/RolandUI/AvaScope/issues/159) | Confirmed opaque constructor-validation errors; concrete missing-offset case and contrasting valid insert/CLI response added. Original macOS payload details remain unverified. |
| [#160](https://github.com/RolandUI/AvaScope/issues/160) | Confirmed native Windows MCP/CLI with exact-case, wrong-case, ambiguity and independent action counters. Reproduction added. |
| [#161](https://github.com/RolandUI/AvaScope/issues/161) | Native 2× Full HD paired-capture acceptance remains blocked. |
| [#167](https://github.com/RolandUI/AvaScope/issues/167) | New: structured find_nodes returns parent-relative visual bounds while the existing argument form returns top-level bounds. |

No product bug had been fixed in the original campaign evidence above. Subsequent fixes and reruns will be identified by commit/session. A workaround never changes the original failed case to passed.

## Stabilization reruns

The fixture correction `936dbed` passed `lifecycle-validation-03` locally and [focused CI 35996074830](https://github.com/RolandUI/AvaScope/actions/runs/35996074830) on native Windows, X11 and macOS. Downloaded lifecycle summaries confirm both integrations, child close/reopen/reset, preserved negative evidence and lease expiry; all observed scales are 1×. The earlier full baseline CI `35991939691` also completed successfully.

**#160 fixed in `173b42b`.** Exact AutomationIDs now compare ordinally in bridge queries, pseudo-state re-resolution and audit uniqueness. The new exact-ID/audit tests failed before the fix; all 14 relationship/query, audit and matrix tests then passed, including actual CLI/MCP stdio and duplicate rejection. Native agent reruns used clean `173b42b` in `campaign-direct-02` (D202–D206) and `campaign-standalone-01` (S01–S08), with a freshly packaged hash-verified provider. Both resolve each case-distinct ID separately, reject `EXAMPLE_BUTTON_KEY_A`, and execute exact-selector actions with journal counters 0/0 → 1/0 → 1/1. Standalone CLI agrees. Native and rendered identity images were opened: both show `a=1; A=1`, correct text/layout and the same list region after excluding window chrome. Native sampling follows rendering and is not atomic. Direct owned termination passed; standalone remains retained for the next investigation.

D201 was an unsuccessful text-filter discovery attempt on tab headers, followed by the observed native AutomationID. The three D203 queries have distinct original `calls/` transcripts; two convenience filenames differed only by case on Windows, so those aliases are not the authoritative record. No request/response in `calls/` was overwritten.
