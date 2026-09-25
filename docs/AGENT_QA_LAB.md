# Native agent QA lab

The first-phase lab implements #162/#163. It uses the existing profile/scenario engine and launch ownership, a shared QA screen in two existing sample hosts, and individual public MCP calls. The detailed defect campaign is #166; this is not a release command.

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

The shared fixture constrains its content to observed `ClientSize`, independently of requested scene dimensions. Native window managers may reject a repeated oversized request without changing the client area; Reset and tabular content must remain inside that actual viewport (#177). Requested Full HD dimensions remain distinct in the journal. Geometry regressions exercise this content/client mismatch; native CI is still required to validate platform behavior.

Copy the returned `root` into `$qa`. All subsequent operations require this exact directory:

```powershell
$qa = '/absolute/path/returned/by/start'
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

`Stop` uses `close-session --terminate-launched-process true`; the product checks the launch marker and live process identity. If startup was interrupted after launch, Stop can recover the unique manifest in that run. Ambiguous/missing ownership needs [RUN_RECOVERY.md](RUN_RECOVERY.md); do not kill a process by name. Fixture lease expiry closes its windows even if the orchestration process disappears. A failed startup/command retains evidence. No run directories are automatically deleted.

Startup separately establishes bridge, application and rendered-frame readiness within a shared 10-second readiness budget before capturing the initial tree. Health and window discovery alone do not establish UI readiness. IPC transport failures retain the final attempt's phase, elapsed milliseconds, configured operation timeout and received byte count, without response contents. A timeout with zero received bytes cannot distinguish server scheduling from execution; a partial response shows that bytes arrived but not why the frame stopped. These diagnostics do not change operation deadlines or retry actions.

## Independent observation

Use three observations together: public response, app-owned `qa-state.json`, and OS-native pixels. The journal records seed, counters, editor replacement generation, bounded event history, readiness, theme, locale, font, backend handle, actual scale and client/screen geometry. It is read-only to the agent. Never change it to make a test pass. Reset is preparation, not the tested user action.

On Windows, the agent's Computer Use `sky.list_windows` / `get_window_state` captures the selected QA window independently of AvaScope. Select the uniquely observed window titled `AvaScope Agent QA`, open the native screenshot, and save the returned image in the run directory. Keep only one interactive QA main window open to avoid ambiguous window selection. On another worker, use its independent OS window-capture facility (for example macOS `screencapture` against an observed window ID, or X11 window capture against the selected owned window). Save the original full-resolution image and source/window identity; never substitute an AvaScope RTB image as the native reference. If the agent cannot view the worker or its images, hands-on visual validation is **blocked**, even if scripted assertions pass.

Open the AvaScope screenshot too. Record capture times, time skew and whether the scene was stable. Align the client area (OS captures can include window borders), inspect full-resolution text/card regions, clipping, focus, popups and layout. Compare within the same OS/font/theme/scale; antialiasing differences alone are not text-size failures. Do not automatically accept new baselines. A machine-readable screenshot path or correct dimensions does not count as visual review.

Product `capture_screen` is an additional route under test. `-AuthorizeScreenCapture` opts the fixture into its declared test-desktop scope and requests the corresponding doctor probe. Use it only on a dedicated test desktop. Requests still need their explicit native capture policy/scope. Default runs do not authorize product desktop capture; independent selected-window observation remains possible.

Mac Retina cases require **observed** `renderScaling=2` and sufficient actual client geometry for 1920×1080 DIP. A worker label, forced headless scale or downsized window does not meet that requirement. Missing display capacity or capture/input permissions is a recorded blocked case.

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

### Expanded fixture charters (#172)

The original four tabs retain their indices and IDs. Form (4), Table (5) and
Input (6) extend the shared Direct/standalone fixture. Their existence is not
evidence that a tool journey passed; retain fresh calls, state and reviewed images.

| Scene | Public-tool journeys and independent oracle |
| --- | --- |
| `qa-form` | Inspect explicit labels, sensitive redaction, read-only/disabled fields, role choices, consent and priority range. Fill a valid profile; inspect focus and desired-state repetition; save through ordinary input. `form.savedProfile` and `form.saves` prove submission. Empty name/invalid email must expose validation and preserve the last saved profile. Reset clears errors and saves. The synthetic password never enters the app journal. |
| `qa-rows` | The original Identity and items tab (index 2) exposes 200 `QaRecord` items with public `Id` keys `QA-001`–`QA-200`; visible text stays `Record 001 — seeded QA data`. Use `virtual_item` with `keyProperty: Id` to find an unrealized row, reveal without selecting, then select. `selectedRow` preserves the zero-based index; `selectedRowKey` independently records the stable identity. Missing/stale/over-budget requests must leave selection unchanged. The list shares seeded records with the table but keeps its own order and selection; table sorting must not reorder it. Reset reseeds both and clears selection. |
| `qa-table` | Query the real DataGrid with `keyProperty: Id`; select offscreen `QA-175`, edit Status/Score, reject Identifier edits, sort Identifier descending and rediscover fresh row evidence. `table.rows` preserves seed order, `table.viewOrder` exposes actual sort order, `selectedKey` and `edits` prove mutations. Reset restores 200 rows, ascending seed order, no selection/edits. DataGrid has its own 12.1.2 package version alongside Avalonia 12.1.3, matching the existing native table fixture. |
| `qa-pointer-pad`, `qa-menu`, `qa-keyboard-editor` | Query action map and pointer diagnostics; click versus drag at least 20 DIP, release capture, right-click/context action, menu action, keyboard text/chords, Tab/Shift-Tab and focus activation. `input` counts menu/context/press/release/drag/key/focus events; an ordinary click must leave `drags=0`. Compare visible text/status and focus with the journal. Reset closes menus, clears counts and restores editor text. |

All data is local and synthetic. Form validation is intentionally triggered by
Save; changing a field does not itself commit the profile. Table status is
free-form, Score is an integer, and Identifier/Name are read-only. Unsupported
providers/operations must be recorded as unsupported, not silently replaced by a
different successful action. Host-declared scenes/operations/diagnostics are
described below; [the complete source-derived capability inventory](AGENT_QA_CAPABILITIES.md)
and campaign are #174.

### Runtime fixture charters (#173)

Runtime (tab 7) adds a shared pure Avalonia scene, bounded background work and
opt-in diagnostic errors. Direct additionally registers the scene and custom
actions through `QaRuntimeRegistration`; standalone has no host-owned registration
and explicitly says so. An unsupported standalone `scene`/custom-action request
is an expected limitation, never a passing declared-operation journey.

| Surface | Journey and independent oracle |
| --- | --- |
| `qa-scene` | Three drawn records (`blue`, `orange`, `green`) keep stable IDs, with generation changed by reset and revision changed by selection/shift/reset. Inspect declared object bounds; use geometry-pinned `pick_node` to locate the canvas and ordinary pointer input to select a drawn record. Shift by 20 DIP and repeat. Direct `scene` invocation uses `qa.scene.select` and the observed object token; stale tokens must be refused without journal changes. Selection outline, `scene.selectedId` and `scene.selections` are independent oracles. Scene declarations do not claim OS hit testing. |
| `qa-operation-start` | Direct `qa.work` declares `mode` (`complete`/`fail`) and integer `steps` (1–10), each step taking 150 ms. Discover parameters, invoke once, observe progress, wait, fail deliberately and cancel. UI buttons run the same work on both hosts. `operation` journals starts/terminal counts, progress and visible status; reset or cleanup cancels old work before it can alter prepared state. Use retained operation IDs for terminal observation after reset. Background work leaves the UI interactive and does not declare the entire app busy. |
| `qa-diagnostics-toggle` | Default is clean: validation off, no intentional missing binding, width 260 inside the 280 DIP clipping parent. Enable errors to introduce one explicit validation error, an unresolved `MissingQaDiagnosticProperty` binding with a visible fallback and an 800 DIP child inside that parent. Observe diagnostics/audit/layout findings and compare pixels plus `intentionalDiagnostics`; disable/reset must remove the intentional current errors. Historical diagnostic logs may retain earlier events and must be distinguished from current state. |

Retain trace correlation around declared actions, runtime expression/assertion
results against independent journal values, and reversible mutation before/after/
reset images. A successful operation response is insufficient if the journal or
visible state disagrees. Fresh execution outcomes belong in the campaign report;
this charter does not assert a passing test run.

The #175 regression requires an applied DataGrid template and realized rows;
reading its backing collection alone can succeed even when the entire grid is
visually blank. Register implicit control themes before XAML attaches controls.
The lifecycle check now visits Table in both reset cycles on both host paths,
requires rendered row nodes plus realized query cells, and retains a table image.
These checks establish a usable surface; the agent must still inspect the images
and perform the table interaction charter.

## Report and cadence

For each case record: ID/charter, goal, integration/backend/scale, exact request/response files, expected state, actual journal delta, native/rendered image observations, passed/failed/blocked/skipped, elapsed time, retries, workaround and ticket. Original `edit_text`/`ensure_state` failures remain failed even when a different tool finishes the user's broader task. Reuse existing defect tickets when the failure matches; add a new ticket only with distinct reproducible behavior.

Evidence includes `qa-run.json`, `artifact-identity.json`, `doctor*.log`, `dotnet-info*.log`, session capabilities, startup workflow reports, exact MCP requests/full responses/stderr, before/after journal snapshots and `calls.jsonl`. Synthetic fixture content may be retained; do not introduce customer data or secrets. The ledger counts task outcomes separately from expected negative responses and tool-call totals. Preserve cleanup evidence.

During development, reproduce the affected task, implement a focused regression, then repeat the relevant native task after the change. At a coherent checkpoint, run the lab lifecycle check and affected deterministic suite. The later #164 work owns broader packaged/candidate gates; it must consume the exact hashes and platform gaps rather than converting missing coverage into success.

The fixture retains requested scene dimensions separately from observed client
geometry. The Full HD button means a 1920x1080 DIP request even if the desktop
backend clamps the window. The journal's `clientWidth`/`clientHeight` and physical
sizes remain the observed values. A clamped window does not establish Full HD
coverage; record that limitation and use a capable dedicated desktop for that
charter. Toggling again restores compact intent even on a smaller display.

## Continuing the blocked Retina cases

The 2026-09-24 campaign observed 1× on local Windows and all three hosted lab
backends. #157's native Retina case therefore remains unverified, although
the repeated-opacity/transform defect is now reproduced on headless Skia at
1.5×/2× with Avalonia 12.1.0 and 12.1.3. #161's implementation has
headless 2× and actual X11 4K coverage, but its native Retina acceptance is open.
The agent needs access to a logged-in Mac desktop that actually provides a
1920×1080 DIP client at 2×, plus an independent window-capture route. No such
access is implied by the current CI runner labels.

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
provenance, masking and authorization refusal. Preserve the complete transcripts,
provider hashes, journal and cleanup in a fresh campaign directory. If the
representative scene does not reproduce the customer's specific symptom,
obtain the exact Avalonia patch and reduced view/font/template combination.
The local mitigation of the confirmed opacity/transform defect does not by
itself establish the original customer root cause or satisfy native acceptance.
