# Native agent QA lab

The lab exercises package-integrated and standalone-provider hosts through the public CLI/MCP surfaces. This document owns fixture operation, independent checks and local retention; current work and results belong in GitHub issues.

## Local testing and retention policy

- Use one reusable binary set and one current case directory. Record revision/binary identity once per rebuild; rebuild only affected components. Keep tools, client, Direct host, standalone host and provider aligned. Reuse the app and reset between related cases; stop owned apps before replacing binaries.
- Keep the absolute QA location and cleanup restrictions in the optional Git-metadata handoff described in [AGENTS.md](../AGENTS.md#development-entry-point). Never publish local paths or raw QA bundles; follow the [publication policy](../AGENTS.md#github-information-policy).
- Keep only minimal failing requests/responses, relevant logs and necessary images/TRX while a defect or batch remains open. Discard checked successful output and delete resolved evidence. Issues, commits and regression tests are the lasting record; do not archive local runs.
- Keep the entire QA area below 8 GiB. Check usage before builds/downloads and clear completed outputs first. Inspect CI artifacts individually, then delete each unpacked artifact. Never copy/hash whole runtime trees per case.
- Before recursive cleanup, verify exact resolved targets remain in the selected QA area and no live process uses them. Preserve local cleanup restrictions; do not touch other projects, global caches, installed AvaScope or user data.
- Select checks through [risk-based validation](VALIDATION.md#choose-validation-by-risk). Verify application postconditions, journal and relevant pixels; transport success is insufficient.

Use both existing hosts: `AvaScope.ComplexWorkflowApp` integrates the package; `AvaScope.StandaloneHost` uses an external provider without an AvaScope reference. Extend their shared scenes instead of adding an executable per defect. Reset/preparation must not perform the action under test. Keep data synthetic and vary relevant theme/locale/size boundaries.

For related MCP calls, reuse `AvaScope.McpScenarioClient --stdio-session --full-result`. Warm the connection before observing short-lived UI, bracket an input with observations of the same target/highlight, and verify independent event counts. Client startup can consume a highlight's 100–5000 ms lifetime; do not extend product lifetimes or repeat a possibly dispatched action to obtain a pass.

The helper below is for bounded isolated/CI checks: it restricts runs to repository `artifacts/agent-qa` and copies binaries even with `-SkipBuild` or `-HostDirectory`. It is not the repeated local exploration path. Stop its owned processes and remove completed output before another isolated run, subject to the same budget and cleanup checks.

## Start and operate

Requirements: .NET 10, PowerShell 7.4+, a selected native desktop and this checkout. Run from the repository root. Windows needs an interactive desktop, macOS a logged-in GUI session, and Linux an explicitly selected X11 `DISPLAY`. Missing native prerequisites fail; there is no implicit headless fallback. Existing managed X11/Wayland restrictions remain documented in [NATIVE_PLATFORM_MATRIX.md](NATIVE_PLATFORM_MATRIX.md). A retained session cannot outlive a managed desktop owner.

```powershell
# Builds the solution, publishes the fixture, pins copies of the binaries,
# runs target doctor, launches and returns the exact session and evidence root.
pwsh -NoProfile -File eng/agent-qa.ps1 -Integration Direct

# External provider path: no AvaScope reference/assemblies in the host.
pwsh -NoProfile -File eng/package-provider.ps1
pwsh -NoProfile -File eng/agent-qa.ps1 -Integration Standalone
```

`-RunDirectory artifacts/agent-qa/my-investigation` selects a fresh directory. `-Backend Headless` explicitly selects the fast regression lane. `-SkipBuild` reuses already built tools/client; `-HostDirectory` reuses an already published QA host of the selected integration. `-ProviderDirectory` chooses a verified standalone distribution. `-LifetimeSeconds` is 30–14400, default 3600, measured from opening the fixture; reset does not extend it. The run record's expiry is a conservative estimate from launch start. Binaries and hashes are kept per run so rebuilding the checkout does not change a running investigation.

`test-agent-qa-lab.ps1` also accepts `-ProviderDirectory` and forwards it to the standalone run. Build the provider from the source being validated before using `-SkipBuild`; a valid manifest proves distribution integrity, not that an older local distribution contains current fixes. Retain the chosen provider's hash with the run.

The shared fixture constrains its content to observed `ClientSize`, independently of requested scene dimensions. Native window managers may reject a repeated oversized request without changing the client area; Reset and tabular content must remain inside that actual viewport. Requested Full HD dimensions remain distinct in the journal. Geometry regressions exercise this content/client mismatch; native backend validation is required to establish platform behavior.

The Rendering tab includes real `Animation.RunAsync` brush timelines. `qa-animation-start`, `qa-animation-stop` and `qa-animation-sample` control a finite 20-minute blue pulse and record independent state. The `qa-animation-style-target`/`qa-animation-style-reference` pair starts from a red Style; the `qa-animation-local-target`/`qa-animation-local-reference` pair starts from red LocalValue. Mutate only the targets, compare against their untouched references, and record after a rendered frame and after stopping. `qa-state.json.animation` reports actual brushes, priorities, `isAnimating`, lifecycle counts and any fixture error. No per-frame disk writes occur. Repeat Start does not stack animations; Reset, cleanup and window closure cancel them before recording final state. Reset/cleanup do not silently clear AvaScope mutations: reset those through the public mutation API when verifying restoration.

Exercise both orders: an already running animation must cause a truthful unsupported mutation with zero effects; a supported mutation followed by animation must reset its hidden base without interrupting the timeline. Stop then exposes the restored red source. Preserve the request/response, sampled journal and viewed native/rendered images for each integration; the headless priority matrix alone does not validate a native timeline.

Copy the returned `root` into `$qa`. All subsequent operations require this exact directory:

```powershell
$qa = '<root returned by start>'
$run = Get-Content (Join-Path $qa 'qa-run.json') -Raw | ConvertFrom-Json
pwsh -File eng/agent-qa.ps1 -Operation Status -RunDirectory $qa
pwsh -File eng/agent-qa.ps1 -Operation Schemas -RunDirectory $qa

# The JSON file contains the exact public MCP arguments, not a workflow recipe.
@{request=@{sessionId=$run.sessionId;topLevelIds=@($run.topLevelId);includeScreenshot=$true;
    outputDirectory=(Join-Path $qa 'observations')}} |
    ConvertTo-Json -Depth 10 | Set-Content (Join-Path $qa 'observe.json')
pwsh -File eng/agent-qa.ps1 -Operation Call -RunDirectory $qa `
    -Tool observe -ArgumentsPath (Join-Path $qa 'observe.json')

pwsh -File eng/agent-qa.ps1 -Operation Reset -RunDirectory $qa
pwsh -File eng/agent-qa.ps1 -Operation Stop -RunDirectory $qa
```

Read the selected tool schema from `schemas.json`. Discovery, action and verification are separate agent decisions. `Call` adds only the run's manifest directory where the tool accepts it; it does not rewrite targets, choose a fallback or retry an action. A failed tool makes the command fail. Use `-AllowFailure` for expected negative cases; the transcript still records failure. Inspect both transport `isError` and structured operation status/postcondition: a successful transport is not proof of a completed task.

For direct MCP registration, `mcp-server.json` identifies the exact copied server; supply the returned manifest directory to public calls. The included real stdio client supports single-tool calls and `--stdio-session --full-result` for a retained server connection. CLI handoff uses `dotnet $run.cliAssembly` with the same session and manifest directory.

Persistent test-client input is BOM-less UTF-8, one complete JSON object per line,
with `tool` and `arguments` fields. Raw Unicode and ordinary JSON Unicode escapes
are both supported regardless of the Windows console code page. Invalid UTF-8,
malformed/truncated JSON or a line over 1048576 characters fails the client before
another tool dispatch, with a bounded diagnostic on stderr. The existing maximum
128 commands and ten-minute session lifetime remain. The client does not alter
the console code page; file-request mode continues reading JSON from its file.

`Stop` uses `close-session --terminate-launched-process true` for a ready session;
the product checks the launch marker and live process identity. A failed or
interrupted startup with a recorded run ID uses `recover-run` against that run's
private `run-store`, including recovery after a previous failed Stop. Recovery
checks active-run ownership, process start identity and session control before
cleanup. A successful cleanup is recorded separately as `cleanupStatus=cleaned`;
the original failed startup stays `status=failed`, with its logs and failure stage
retained. `cleanupOutcome=already_exited` means all recorded children were already
absent before recovery; otherwise the verified recovery outcome is `cleaned`.
Without a run ID, only the existing unique-session/launch-marker path is allowed.
Ambiguous/missing ownership needs [RUN_RECOVERY.md](RUN_RECOVERY.md); do not kill a
process by name. Fixture lease expiry closes its windows even if the orchestration
process disappears. No run directories are automatically deleted.

Startup separately establishes bridge, application and rendered-frame readiness within a shared 10-second readiness budget before capturing the initial tree. Health and window discovery alone do not establish UI readiness. IPC transport failures retain the final attempt's phase, elapsed milliseconds, configured operation timeout and received byte count, without response contents. A timeout with zero received bytes cannot distinguish server scheduling from execution; a partial response shows that bytes arrived but not why the frame stopped. These diagnostics do not change operation deadlines or retry actions.

## Independent observation

Use three observations together: public response, app-owned `qa-state.json`, and OS-native pixels. The journal records seed, counters, editor replacement generation, bounded event history, readiness, theme, locale, font, backend handle, actual scale and client/screen geometry. It is read-only to the agent. Never change it to make a test pass. Reset is preparation, not the tested user action.

Use an available, authorized OS window-capture facility independent of AvaScope. Select the uniquely observed window titled `AvaScope Agent QA`, open the native screenshot, and save the returned image in the run directory. Keep only one interactive QA main window open to avoid ambiguous window selection. On another worker, use its independent OS window-capture facility (for example macOS `screencapture` against an observed window ID, or X11 window capture against the selected owned window). Save the original full-resolution image and source/window identity; never substitute an AvaScope RTB image as the native reference. If the agent cannot view the worker or its images, hands-on visual validation is **blocked**, even if scripted assertions pass.

Open the AvaScope screenshot too. Record capture times, time skew and whether the scene was stable. Align the client area (OS captures can include window borders), inspect full-resolution text/card regions, clipping, focus, popups and layout. Compare within the same OS/font/theme/scale; antialiasing differences alone are not text-size failures. Do not automatically accept new baselines. A machine-readable screenshot path or correct dimensions does not count as visual review.

Product `capture_screen` is an additional route under test. `-AuthorizeScreenCapture` opts the fixture into its declared test-desktop scope and requests the corresponding doctor probe. Use it only on a dedicated test desktop. Requests still need their explicit native capture policy/scope. Default runs do not authorize product desktop capture; independent selected-window observation remains possible.

For Retina scale/geometry requirements, follow [Native Retina Validation](#native-retina-validation).

## Agent task charters

Start with reset seed 42, Light/en-US, notifications off, display name Ada, zero action counters. Give the agent the objective and let it discover the UI and public contracts. Preserve the chosen calls and explain recoveries; do not hand it only a script of selectors.

| Charter | Objective and observable completion |
| --- | --- |
| Profile | Enable notifications, change the display name to `Grace Hopper`, visit another page and return. Both values persist and native pixels agree with the journal. |
| Repetition | Request the same checked state twice, then replay an identical text edit with the same request ID. The toggle and text-change counters prove no duplicate change. |
| Identity | Find the two case-distinct key buttons and activate each independently. Only the intended counter changes each time; a wrong-case exact query does not silently target another control. |
| Appearance | Explore the rendering page, supported language/theme changes and viewport sizes, then return to the simple page. Compare native/rendered text, transforms, clipping and layout at each stable state. |
| Recovery | Try a read-only/disabled editor and an editor reference captured before replacement. Inspect the diagnostic, rediscover the current editor, perform a valid change and prove rejected actions did not mutate state. |
| Free exploration | Spend a bounded session on keyboard focus, loading/reset interruption, virtualized items, secondary/modal windows and popup lifecycle. Choose actions from observations; investigate one unexpected result before continuing. |

Apply charters to both integrations. Vary one relevant condition at a time. Run at least two clean prepare/action/reset/cleanup cycles, one deliberate failure, and interrupted/expired-session recovery. `eng/test-agent-qa-lab.ps1` checks lifecycle/evidence plumbing and retained public MCP regressions for exact case-distinct IDs and consistent query bounds with geometry-pinned picking; its scripted result does not count as these agent tasks.

### Expanded fixture charters

Form (4), Table (5) and Input (6) share the same fixture on both integrations.

| Scene | Public-tool journeys and independent oracle |
| --- | --- |
| `qa-form` | Inspect explicit labels, sensitive redaction, read-only/disabled fields, role choices, consent and priority range. Fill a valid profile; inspect focus and desired-state repetition; save through ordinary input. `form.savedProfile` and `form.saves` prove submission. Empty name/invalid email must expose validation and preserve the last saved profile. Reset clears errors and saves. The synthetic password never enters the app journal. |
| `qa-rows` | The original Identity and items tab (index 2) exposes 200 `QaRecord` items with public `Id` keys `QA-001`–`QA-200`; visible text stays `Record 001 — seeded QA data`. Use `virtual_item` with `keyProperty: Id` to find an unrealized row, reveal without selecting, then select. `selectedRow` preserves the zero-based index; `selectedRowKey` independently records the stable identity. Missing/stale/over-budget requests must leave selection unchanged. The list shares seeded records with the table but keeps its own order and selection; table sorting must not reorder it. Reset reseeds both and clears selection. |
| `qa-table` | Query the real DataGrid with `keyProperty: Id`; select offscreen `QA-175`, edit Status/Score, reject Identifier edits, sort Identifier descending and rediscover fresh row evidence. `table.rows` preserves seed order, `table.viewOrder` exposes actual sort order, `selectedKey` and `edits` prove mutations. Reset restores 200 rows, ascending seed order, no selection/edits. |
| `qa-pointer-pad`, `qa-menu`, `qa-keyboard-editor` | Query action map and pointer diagnostics; click versus drag at least 20 DIP, release capture, right-click/context action, menu action, keyboard text/chords, Tab/Shift-Tab and focus activation. `input` counts menu/context/press/release/drag/key/focus events; an ordinary click must leave `drags=0`. Compare visible text/status and focus with the journal. Reset closes menus, clears counts and restores editor text. |

All data is local and synthetic. Form validation is intentionally triggered by
Save; changing a field does not itself commit the profile. Table status is
free-form, Score is an integer, and Identifier/Name are read-only. Unsupported
providers/operations must be recorded as unsupported, not silently replaced by a
different successful action. Host-declared scenes/operations/diagnostics are
described below; use the [capability scenarios](AGENT_QA_CAPABILITIES.md) to select further checks.

### Runtime fixture charters

Runtime (tab 7) adds a shared pure Avalonia scene, bounded background work and
opt-in diagnostic errors. Direct additionally registers the scene and custom
actions through `QaRuntimeRegistration`; standalone has no host-owned registration
and explicitly says so. An unsupported standalone `scene`/custom-action request
is an expected limitation, never a passing declared-operation journey.

| Surface | Journey and independent oracle |
| --- | --- |
| `qa-scene` | Three drawn records (`blue`, `orange`, `green`) keep stable IDs, with generation changed by reset and revision changed by selection/shift/reset. Inspect declared object bounds; use geometry-pinned `pick_node` to locate the canvas and ordinary pointer input to select a drawn record. Shift by 20 DIP and repeat. Direct `scene` invocation uses `qa.scene.select` and the observed object token; stale tokens must be refused without journal changes. Selection outline, `scene.selectedId` and `scene.selections` are independent oracles. Scene declarations do not claim OS hit testing. |
| `qa-operation-start`, `qa-operation-hold`, `qa-operation-continue` | Direct `qa.work` declares `mode` (`complete`/`fail`/`hold`) and integer `steps` (1–10), each step taking 150 ms. Use `hold` for separate agent observations: it waits at zero progress until **Continue work**, **Cancel work**, operation cancellation, replacement, reset or cleanup. The `operation` journal exposes `state=waiting`, `waitingForRelease=true`, starts/terminal counts and visible status. UI buttons run the same ordinary work on both hosts; only Direct declares AvaScope operations. Continue is disabled outside held work. The fixture lease still bounds the app lifetime. Use retained operation IDs for terminal observation after reset. Background work leaves the UI interactive and does not declare the entire app busy. |
| `qa-diagnostics-toggle` | Default is clean: validation off, no intentional missing binding, width 260 inside the 280 DIP clipping parent. Enable errors to introduce one explicit validation error, an unresolved `MissingQaDiagnosticProperty` binding with a visible fallback and an 800 DIP child inside that parent. Observe diagnostics/audit/layout findings and compare pixels plus `intentionalDiagnostics`; disable/reset must remove the intentional current errors. Historical diagnostic logs may retain earlier events and must be distinguished from current state. |

For an agent cancellation charter, invoke `qa.work` with `mode=hold;steps=1`,
retain the returned operation ID, then use separate CLI/MCP status calls and a
rendered screenshot to confirm it remains active. Cancel that ID and check both
its `cancelled` outcome and the independent journal's cancellation count. Start a
second held operation, invoke `qa-operation-continue`, and verify completion and
the completion count. A late cancellation of already completed short work is not
a cancellation pass. Reset/replacement/close must not allow old continuations to
overwrite newer fixture state. Standalone tests use the visible held/continue/
cancel buttons and the journal; they cannot claim host-declared operation coverage.

Retain trace correlation around declared actions, runtime expression/assertion
results against independent journal values, and reversible mutation before/after/
reset images. A successful operation response is insufficient if the journal or
visible state disagrees. Fresh execution outcomes belong in the relevant GitHub issue;
this charter does not assert a passing test run.

Use targeted inspection for these diagnostics: `inspect_node`/`find_nodes`
expose current validation, and `explain_layout` identifies the clipping ancestor.
`audit_ui` is not a substitute for those checks. Public binding metadata can
observe an active runtime expression and its fallback value while reporting the
binding path/error/fallback details unavailable; do not infer a healthy binding
or full error coverage from that partial metadata. `operation` currently accepts
status/wait/cancel for a retained ID, not an operation-list action.

Table checks require an applied DataGrid template and realized rows/cells; reading the backing collection alone can pass with a blank grid. Register implicit themes before XAML attaches controls, then inspect the rendered table and perform its interaction charter.

### Navigation fixture charter

Navigation (tab 8) contains `qa-navigation-identity`, a scoped synthetic document
view. Overview/Details change its surface; Switch document alternates document-a
and document-b; Edit document increments only that document's revision. Reset
restores both documents and increments the generation included in the revision.
The `navigation` journal object and visible content independently expose these
values. Selecting a page/document does not edit it. This declaration covers only
these synthetic documents, not the rest of the app's form/table/hidden state.

Direct implements `IAvaScopeDebugStateProvider` in the existing host-only source.
Find a fresh `qa-navigation-identity` target and pass it to `navigation` start and
record. Use workflow `maxDepth: 32` for the nested action controls; verify the
workflow passed before recording its outcome. Explore Overview → Details →
Overview, document A → B → A, edits and
reset; compare distinct visit IDs, equivalent state keys where applicable, ordered
routes and loop confidence with the journal and pixels. Check stale-tip refusal,
excluded identity/policy mismatch, clear and unchanged unrelated counters.
Standalone keeps ordinary controls and journal but has no declaration: identity
must remain unavailable, visits distinct and sampled candidates uncertain.
Even declared matches retain the product's `hidden_state_unverified` limitation.

## Assessing Results

Compare the expected state with the journal delta, public result and reviewed images. Distinguish passed, failed, blocked and skipped; a workaround does not turn the original failed operation into a pass. Preserve only unresolved evidence under the retention policy and report the concise result in the relevant issue. Reuse existing defects when behavior matches.

The fixture retains requested scene dimensions separately from observed client
geometry. The Full HD button means a 1920x1080 DIP request even if the desktop
backend clamps the window. The journal's `clientWidth`/`clientHeight` and physical
sizes remain the observed values. A clamped window does not establish Full HD
coverage; record that limitation and use a capable dedicated desktop for that
charter. Toggling again restores compact intent even on a smaller display.

## Diagnosing Probe Waits

Set `AVASCOPE_PROBE_LIFECYCLE_FILE` to a new file in an owned evidence directory. The client records content-free UTC/monotonic phases and request/response/output counts, bounded to 1024 events and 256 KiB. It excludes arguments, user text, environment values and exception messages; existing files are preserved and trace-write failure never retries an action.

Interpret phases with the independent journal:

| Phase | Meaning |
| --- | --- |
| `no_tool_request_started` | No tool call has started in this probe. |
| `request_outcome_unknown` | Application effects remain unknown. |
| `response_received_output_pending` | MCP returned; stdout delivery is pending. |
| `responses_written` | Output was written; parent consumption and shutdown are not established. |
| `closed` | Client disposal completed; unavailable transport exit metadata remains null. |

A missing trace establishes none of these outcomes. Keep primary and cleanup failures separate. Controlled probe tests can use `AVASCOPE_PROBE_HOLD_STAGE` / `AVASCOPE_PROBE_HOLD_RELEASE` with a five-second maximum; they do not prove a historical scheduling cause or native behavior.

## Native Retina Validation

Use a logged-in Mac desktop with observed `RenderScaling=2`, sufficient room for the required logical client size and an authorized independent window-capture route. A hosted runner label, headless scale or rendered-image dimensions do not establish native Retina coverage. Required unavailable geometry or permissions are missing acceptance evidence; use the current issue to track that limitation.
On an explicitly selected test desktop, use the same documented native lab
entry point and freshly verified standalone provider; enable
`-AuthorizeScreenCapture` only when that desktop is authorized. First inspect
`qa-state.json`, doctor output and public window geometry. If the observed scale
is not 2 or the Full HD scene is clamped, record blocked and stop that case.
Do not resize the app smaller or force headless scaling to obtain a pass.

Repeat the Appearance charter in both integrations, opening independent native
and RTB images at full resolution. Scroll to `qa-opacity-transform`: all five
MM rows must retain the same size and spacing. Compare the overlapping
group-opacity rectangles and clipped green square as well as the existing
localized/nested/template references. Run actual CLI and MCP paired capture,
checking unchanged 1920×1080 DIP geometry, 3840×2160 image dimensions, native
provenance, masking and authorization refusal. Retain only minimal unresolved failure evidence in the reusable QA area under the policy above. If the
representative scene does not reproduce the customer's specific symptom,
obtain the exact Avalonia patch and reduced view/font/template combination.
The local mitigation of the confirmed opacity/transform defect does not by
itself establish the original customer root cause or satisfy native acceptance.
