# Native agent QA campaign

Status: **in progress**, 2026-09-25. Tracking issue [#166](https://github.com/RolandUI/AvaScope/issues/166); expanded fixture #172, declared surfaces #173 and capability campaign #174. The full goal is not achieved and current applicable checks are not all passing. The owner expanded the original intake-only phase to include fixes, meaningful regressions and repeated comprehensive testing. Original failures remain distinct from post-fix verification. Version changes and release remain outside scope.

## Current validation checkpoint

Native #212 validation on exact clean `1ebaaa1` is complete in
`navigation-direct-01` and `navigation-standalone-01`: 29 corrected calls each
(17 MCP/12 CLI), 131/116 independently verified checks and five reviewed native
images each at Win32 1x. Eight visits preserve actual page/context/revision/reset
changes. Direct returns equivalent keys for unchanged document A and revised
document B; standalone honestly retains unique visits and uncertain sampled
candidates. Retrospective routes, stale-tip refusal, excluded identity, exact
privacy-policy matching and clear guards pass. Coverage remains partial and
hidden domain state unverified. Both runs preserve 31 selected binary/source
identities; standalone remains a pure host with the verified provider. Reset and
owned termination pass, apps 9836/6428 and all 65 original clients are absent,
independently confirmed by process audits and the native window inventory.

The corrected 58-call charter is separate from seven retained initial Direct
calls. A depth-16 workflow missed the nested button, and an agent helper then
incorrectly reported that failed action as successful to the navigation journal.
The unchanged app journal and invalid transition are retained; that history was
explicitly cleared. Corrected requests use depth 32 and validate each outcome
before continuing. No original failure is overwritten or treated as a product
success. #212 moves to review pending the full hosted gate; #211 resumes as sole
active for the first-hit-test/frame boundary. #213 remains ready with its original
deadline cause unknown. Inventory verified from GitHub: 48 defects/32 closed
(44 `type:bug` tickets plus CI defects #171/#192/#193/#200). No release.

#212 adds a scoped synthetic-document navigation view: Overview/Details, two
independent document revisions, reset generation, ordinary controls/journal in
shared source and Direct-only declared identity. Both viewport regressions and
all 24 existing affected cases pass after an explicit rebuild (26 total, 19s).
The first two runs remain retained: 24 passed/two stale agent-expectation failures;
then 23 passed/those two failures plus one existing keyed-list selection deadline.
Editing during compilation made the first output newer than the corrected source,
so incremental compilation reused the stale assertion; forced rebuild corrects
that verification error. #213 retains the separate list deadline with unknown
cause, and its assertions now preserve full responses without relaxing deadlines.
Fresh native Direct/standalone validation follows. Current inventory: 48 defects,
32 closed; #212 sole active. No full passing gate or release is claimed.

The full `16a44ac` gate `36193600154` subsequently finishes **failed**: Windows
1018 passed/eight skipped, macOS 1015 passed/one failed/nine skipped, Linux 33
passed and all nine native/expiry runs verified on exact clean source at 1x.
All eight #211 diagnostic controls and thirteen #209/#210 regressions pass on
both full-test platforms. The repeated common-state matrix failure now retains
its real primary error: initial pointerover at (180,42) has no hit visual and
`handled=false`; later pressed/selected+pointerover at those coordinates hit a
TextBlock and pass. This narrows the unresolved first-frame/hit-test boundary
without proving its cause. Full JSON/TRX/logs are retained in
`matrix-diagnostics-01`; downstream macOS runtime/package gates were skipped.
#209/#210/#211 stay review. #212 is now sole active for the scoped navigation
fixture gap, and #174 is review. Inventory remains 47 defects/32 closed.

Fresh `windows-standalone-01` on clean `b650ca1` (production `16a44ac`)
completes 46 public calls, 30 MCP/16 CLI, with nine reviewed native images at
Win32 1x. Its 32 window calls pass 126 checks: resize/stale/restore, independently
measured child movement, modal owner blocking/focus, guarded close and closed
identity refusal match Direct behavior. W013 intentionally omits isolation as
a negative control; it leaves the child open until the explicit isolated close.
Final main-window activity has no focused control, independently confirmed by UIA.

The additional 14 navigation calls pass 58 checks: actual Windows → Settings →
Windows changes, retained ordered visits/actions, stale-tip refusal, bounded
queries, retrospective route provenance, explicit clear and expired-identity
refusal. The fixture has no declared navigation identity. Its bounded partial
sample stays unchanged across these visibly different pages, so all three visits
remain distinct and the two sampled revisit candidates are explicitly uncertain.
An initial verifier wrongly expected no candidates; its failure is retained and
corrected against the documented contract without replaying requests. This is a
fixture coverage gap, not proof of equivalent domain state or a product defect.

Reset, owned termination and independent absence of app 19560/all 46 original
clients verify. Thirty-one pinned binaries, unchanged production source, pure
standalone host and updated verified provider agree. Exact `16a44ac` full CI
`36193600154` has passed Windows and all three native labs; Linux/macOS full jobs
still run. No complete gate or Retina coverage is claimed. #174 remains sole
active; #209/#210/#211 remain review. Inventory: 47 defects/32 closed.

The #211 diagnostic correction prioritizes failed pseudo-state entry diagnostics
over three known target-lifetime advisories, preserving the full partial response
and all advisories. Eight new controls include actual stdio MCP/CLI over real
Avalonia occluded and unobstructed controls, with rendered pixels and hover cleanup.
Before correction five fail/three pass. The first affected run retains 59 passes
and five failures: two existing deep-state matrices, two structured geometry
queries and a 30-second MCP cancellation. A host snapshot records 100% CPU; this
does not establish causation. Additional assertions retain the entire matrix and
query response, without changing deadlines, replaying uncertain input or relaxing
state checks. All 19 targeted cases then pass, including those boundaries and the
unchanged common-state journey. Original macOS and transient local causes remain
unknown; #211 stays open and the complete hosted gate is still required. Artifacts:
`matrix-diagnostics-01` (all four TRX stages, logs, independent verification and
resource observation). The MCP timing recurrence is also linked to existing #192.

Earlier #174 exploration: `windows-direct-01` on clean `53ad53c` retains
32 calls (22 MCP/ten CLI), 124 checks and seven reviewed native images. Parent
resize 1120x800 to 1000x760 and restoration agree with independent image sizes;
stale revision is refused without dispatch. An owned 460x260 child moves exactly
24 pixels in both axes, independently confirmed by native screenshot origins.
Modal ownership blocks parent resize without dispatch. Explicit modal focus and
paired native-window-message Tab agree with UIA, with no held input or fallback.
This route is owned HWND event dispatch, not hardware/IME injection.

Original W013 omitted `isolatedStateDirectory`; its correct guard refusal and
still-open W014/W015 observations are preserved as an agent request mistake.
Corrected W026b closes the child once; W026c and W030 reject actually closed
windows. Final main-window activity, no modal blocker, no focused control and
unchanged unrelated input/form/table journals agree with native UIA. Reset and
owned termination pass; 25 immutable binaries/source verify. All 32 original
clients and app 9388 are absent. PID 19144 now identifies an unrelated svchost
with unavailable start time; the original dotnet client's recorded exit and
different process identity prove reuse, and that process was never touched.
An optional-null verifier error and incorrect cleanup parameter/stale exit-code
checks remain retained; corrected verification does not replay the journey.
The subsequent standalone comparison and navigation baseline are recorded above.

Full `19dd8bd` CI `36186815465` is terminal **failed**. Downloaded TRX verifies
Windows 1010 passed/eight skipped, macOS 1007 passed/one failed/nine skipped and
Linux 33 passed. All thirteen #209/#210 cases pass on both full-test platforms;
six native integration runs and three expiry runs pass on exact clean source
at actual 1x. macOS fails the unchanged common-state pseudo-state matrix test;
its only reported error is a generation-scoped-node advisory, and its output
directory is deleted. #211 retains this failure and investigates the missing
causal evidence; the original cause is unknown. Subsequent macOS gates were
skipped. #209/#210 stay review, #174 moves to review while #211 is selected.
Current inventory: 47 defects, 32 closed. No release.

Earlier #174 exploration: `focus-standalone-after-01` on clean `2684a38`
(production unchanged since `19dd8bd`) executes 24 public calls, 15 MCP/nine CLI,
plus two real Windows keyboard actions. All 61 evidence checks pass; eight
images were reviewed. Initial no-focus, editor focus, synthetic Tab/Shift+Tab,
native Tab/Shift+Tab, stale-target refusal, wrong-window/invalid-map guards,
action search and explicit partial/unrealized menu coverage agree with the app
journal and independently observed Windows UIA focus. Deep recording now passes
on both transports, shallow recording remains valid and missing geometry fails.

One immediate native-Tab UIA snapshot still named the editor while the image,
journal and subsequent runtime query showed the button. A separate fresh UIA
read agreed, without repeating input; both snapshots remain retained and no
zero-latency cross-provider synchronization is claimed. An initial PID-only
cleanup audit briefly found an already exited client's numeric PID, with no
identity available at the next read. The original audit is retained; final
start-time-aware checks confirm owned app 5680 and all original clients absent.
No unrelated process was terminated. Twenty-five immutable binaries and source
identity verify. Full combined #209/#210 CI `36186815465` continues on exact
`19dd8bd`; #174 remains active for multi-window/modal boundaries.

#210 candidate validation now succeeds locally and in both actual native
integrations. `transformed-bounds-01` retains the old five failures/two passing
controls and the corrected 73 affected tests/two Unix skips. Seven cases compare
actual solid pixels against manually specified normal, translated, scaled,
rotated, ancestor-transformed and clipping-control rectangles. Two additional
bounds/gesture consumers pass after updating a legacy assertion that expected
the original mismatch; its initial failure remains retained.

`transformed-direct-after-01` and `transformed-standalone-after-01` each execute
17 public calls (12 MCP/five CLI), with 153/155 independent checks plus eight
exact pixel-extent comparisons and eight reviewed images per integration.
Normal/hover/pressed/release frames now report the full rectangle (pressed
122.5x31.36 DIP), and static 1.15x ancestor scaling agrees across find, inspect,
recording and geometry-pinned picking. Local layout metrics remain local;
transformed bounds are unclipped layout AABBs, not ink extents or ancestor-clip
intersections. Missing targets still fail and input journals remain unchanged.
Source and selected binary hashes, pure standalone/provider isolation and owned
app/all-client absence are independently verified. Provider manifest is
`82844218f109028ad1440218fe16255d26fa77e3c05a725972d66cb671d38611`.
The initial Direct pixel parser's wrong `step.id` lookup is retained as an agent
error, corrected without replaying application actions. Sampling uses explicit
200 ms settling delays and does not claim a deterministic animation clock.
Native scope remains Win32 1x; the combined #209/#210 full gate is next.

Latest gate: full `36177716925` on exact `59b9b2c` succeeds all six jobs.
Independent artifact verification confirms Windows 997 passed/eight skips,
macOS 995/nine, Linux 33 and six native/three expiry checks at 1x. All five #206
and eight #207 cases plus both provenance cases pass on both full platforms;
#206/#207 are closed. #208's three Windows process controls pass (explicit skips
on macOS), while its original installer stall cause remains unknown/open. #209
is pushed as `2fbbee7` and in review; active #210 now tests transformed extents.
The next full gate will combine those changes after native verification. Current
accounting: 46 defects/32 closed. No local owned app/client remains. Entries
below retain earlier intermediate failures and narrower validation.

Latest discovery: `focus-recording-direct-01` on clean `703c960` executes 22
public calls (14 MCP/eight CLI), with 45 independent checks and five viewed
images. Focus inspection, observed Tab/Shift+Tab round trip, stale refusal,
action-map search/realization boundaries and missing-window guards pass.
Recording a deep visible button fails through both transports with
`interaction_geometry_target_not_found`, despite correct captured pixels and
successful subsequent targeted inspection. A shallow recording control passes;
genuinely missing geometry remains refused. New #209 tracks this separate
recording response-budget defect. App 9580/all clients are gone, 25 pinned
binaries match the verified #207 production candidate, and production source is
unchanged since `8d10546`. Native scope is Win32 1x. Two agent helper mistakes
(window argument shape and null-result presentation) were corrected without
replaying product actions. #209 is active; 45 defects/30 closed.

Combined full CI `36177716925` targets `59b9b2c` including #206/#207/#208 and is
still running. Its predecessor `36174208805` is terminal failed at the old
provenance test expectation (993 pass/one fail/eight skips); all eight new hover
cases and six native/three expiry runs are independently verified. The test-only
follow-up passes all 32 local cases. Neither run includes the new #209 work.
Historical checkpoints below retain their exact scope/source.

#209's bounded lookup candidate passes 37 affected tests (39s), including six
new real geometry/coordinate/clipping/missing/replacement and retained-pipe
disappearance cases. Before: two failures/two passes. First candidate: 33 passes
and two failures due to incorrectly passing a tree context as a node context;
corrected to retain the actual inspected node identity across frames. Every
stage is preserved. Direct `recording-direct-after-01` then verifies nine calls
(six MCP/three CLI), 116 checks and six viewed images. Deep target/parent lookup,
missing refusal and explicit hover/down/release/clear work; app 21968/all clients
are gone. Source/binary identity verifies. This is not whole-geometry success:
new #210 records transformed origins combined with unscaled width/height under
Fluent's pressed scale. The original verifier incorrectly assumed a stationary
origin; that failure is retained and the separate extent defect remains explicit.
Fresh `recording-standalone-after-01` completes the same nine calls with 118
checks/six viewed images, pure host and verified provider manifest
`a45b0e1b083478f59f0f39899c5261acfc770b267a1d36910da46be9ca73d9a5`.
Deep geometry/parent resolution, missing guards and explicit hover/down/release
pass with source/binary hashes unchanged; the separate #210 extent failure
reproduces. Both native apps/all clients are gone. #209 is ready for commit and
review pending a full gate. Current accounting: 46 defects/30 closed.
Earlier 59b9b2c CI has Windows 997/eight skips, Linux 33 and six native/three
expiry checks verified at 1x; macOS full validation still runs.

Full combined CI `36141416097` on `108adc5` succeeds across all six jobs.
Downloaded individual TRX results verify Windows 953 passed/six skipped, macOS
952/seven and Linux 33 focused cases. All seven #200 cleanup regressions pass
on both full platforms. Windows native input now has twelve passing cases;
actual accessibility cleanup records complete logs, all phases and disposed
owned host 8292. Six native lab runs plus three expiry checks verify clean
identity and ownership at actual 1x. #200 is closed; #199 original cause remains
open. These are scripted checks, not exploratory Retina coverage.

#202 fix `8bfcec0` is closed after the later complete `c83e50c` gate
`36152235816` passes all six jobs: Windows 968 passed/six skipped, macOS
967/seven and Linux 33. All eleven #203 cases pass on both full platforms; all
six native lab runs/three expiry checks verify clean identity and owned cleanup
at 1x. The original `8bfcec0` macOS pointer failure remains retained as #204;
the later green run is not proof of that original cause. #204's candidate
`96c1656` is now review, and new full CI `36157963606` runs exact `30d176a`,
including its two controlled Unix cases. #201 remains blocked on supported
animation clock control, and #203's original project timeout cause is still
unknown despite validated diagnostic improvements. Full `30d176a` CI
`36157963606` subsequently succeeds: Windows 969/eight skips, macOS 970/seven,
Linux 33, all twelve pointer cases on macOS including both Unix controls, and
six native/three expiry checks at 1x are independently verified. #205 closes after
the complete `ba07220` gate `36163086325`: Windows 981/eight skips, macOS 982/seven,
Linux 33, all twelve new binding-scope cases on both full platforms, and six
native/three expiry checks at 1x independently verified. #207 is the sole active
issue; current GitHub accounting is 44 unique defects/30 closed.

Full #206 `d32e539` CI `36168286222` is terminal failed: Windows 985 passed/one
installer process timeout/eight skips, with all five new #206 cases passing.
Downstream full macOS/Linux jobs skip. All six native lab runs/three expiry
checks pass and verify clean exact source/ownership at 1x. New #208 preserves
the installer's 60-second process wait and the fixture's missing partial-output
and owned-cleanup evidence; the original cause and surviving-child outcome are
unknown. #206 remains review until a complete gate passes.

## Installer process evidence and cleanup (#208)

The controlled Windows child writes partial stdout/stderr and waits beyond the
unchanged 60-second deadline. Before correction it remains alive after the helper
returns cancellation: two exit-code controls pass, one ownership case fails in
62s. The test's fallback kills that observed child (9144); no live child remains.
This proves the helper gap, not the historical CI installer's preceding cause.

The test helper now retains the original exception, captures bounded output tails
after owned termination, reports process phase/timing and cleanup outcomes, and
closes redirected readers. Installer failures additionally report boolean file
milestones; arguments and environment values are omitted. The default deadline
stays 60 seconds, cleanup/drain waits are each bounded to three seconds, and
neither installer behavior nor retries change. New controlled cases are explicitly
Windows-only instead of silently passing elsewhere.

Both initial and final affected runs pass all six discovered cases in 90s. Five
exercise real processes, including the original install/discovery and MCP tests;
the packaged-installer test is an environment guard because no local installer
artifact was selected. Independent TRX verification confirms actual child exit,
original cancellation, retained stdout/stderr and bounded tails. Evidence is in
`artifacts/agent-qa/installer-process-01`. The original install timeout cause
remains unknown and this ticket stays open pending further evidence/full CI.

## Actual hover state and cleanup (#207)

The first complete `8d10546` gate `36174208805` fails one Windows provenance
expectation: after moving inside, the first outside move now actually dispatches
owned exit events, but the old test still expects `not_dispatched`. Windows has
993 passed/one failed/eight skips; all eight new hover cases pass. The original
#208 installer invocation also passes (1.599s), without establishing its earlier
cause. Full macOS/Linux jobs skip and native labs continue. #207 needs the
provenance regression updated to distinguish actual exit from a repeated no-op.
The follow-up now observes real entered/exited events and window hover state,
requires synthetic dispatch for the first exit, and retains `not_dispatched` for
the second outside move without another event. All 32 provenance/picking cases
pass locally (2m45s); the original CI failure and follow-up TRX remain retained
in `ci-8d10546-windows` and `hover-state-01/provenance-followup`. No production
change was needed for that obsolete assertion. Combined full validation remains
required after the current native jobs complete.

Five real Avalonia 12.1.3 tests fail before the candidate: target/ancestor hover
is absent and an occluded target's matrix still reports success. The candidate
uses public entered/exited events, tracks only newly entered elements and clears
them in leaf-to-root order after movement, detach, registration disposal or bridge
deactivation. A held pointer retains identity/capture; preexisting native hover
is preserved. It never writes the private input root or moves the OS cursor;
concurrent native input can interfere with this synthetic approximation.

The matrix checks the requested `:pointerover`/`:pressed` after capture and
retains a failed entry, screenshot and `pseudo_state_not_observed` when absent.
Cleanup releases/moves outside the top-level. Real pixel tests cover a full-window
blue-to-green hover and an occluding sibling; a full-window button proves pressed/
disabled restoration with zero clicks. The final affected run passes 56 cases
with two Unix skips (68 seconds), including all eight new tests. One intermediate
new test used an incorrect fixed coordinate; its 35-pass/one-failure result is
retained separately, and translated actual geometry corrects it. Original
five failures, all candidate stages and independent TRX verification live in
`artifacts/agent-qa/hover-state-01`.

Native `hover-direct-after-01` on d32e539 plus the candidate completes twelve
public calls (nine MCP/three CLI) and 76 independent checks. Both matrices show
`:pointerover` and 3859 changed pixels, visible pressed/disabled states and
restoration. Separate delayed inspection observes hover before explicit outside
movement and no hover afterward. Missing/stale targets are refused with unchanged
input journals. Six images are viewed, including independent native captures;
both source hashes and selected binary hashes remain unchanged. Reset/Stop
terminates owned app 8460, and all twelve clients are absent.

Fresh `hover-standalone-after-01` completes the same twelve public calls with 78
independent checks and six viewed images. Its host contains no AvaScope DLLs;
the verified provider manifest is `da3987d82874b61fc951a18396d400414e3c5a42290efa9879218013cc4d0d5a`.
Both transport matrices show the same 3859-pixel hover change, correct pseudo-classes,
visibly distinct pressed/disabled states and restoration. Delayed hover/clear
and all negative guards agree. The candidate source/binary hashes and independent
input journals remain unchanged; Reset/Stop terminates owned app 9204, and all
twelve clients are absent. Both journeys validate the requested four-state matrix
on Win32 1x; no complete hosted gate or native Retina pass is claimed yet.

## Native pseudo-state response-budget discovery (#206)

`pointer-focus-direct-01` starts from clean `ba07220` with freshly published
and pinned Direct binaries on Win32 / Avalonia 12.1.3 / actual 1x. Ten public
calls (nine MCP/one CLI), two viewed independent native images and 39 verification
assertions retain the discovery. A visible enabled `KeyboardNext` control is
found uniquely through depth-24 lookup and focus inspection. Both MCP's four
state matrix and CLI's normal state then falsely report it missing. A shallow
ThemeButton normal-state control succeeds; failed matrices dispatch no inputs
or mutations and the independent input journal is unchanged.

The visual tree correctly reports its response budget: requested/original depth
24, returned depth eight, 124 original nodes/27 inline nodes. The complete
artifact contains the target, while its inline root omits it. The matrix uses
that inline root as though it establishes absence, and its post-capture fallback
can reuse earlier node data. #206 tracks the bounded-current-target correction.
The first tab invocation used unsupported `invoke` and was corrected to `select`;
an extra ignored argument on the separate tree read is also an agent mistake,
not a product failure. Neither is in the failing matrix requests.

`verify-discovery.py` / `verified-discovery.json` retain exact identities, original
responses, budget/full-artifact comparison, unchanged selected binary hashes and
owned cleanup. Reset followed by public Stop terminates app 7048; all ten clients
are absent. This interrupted the planned pointer/recording/focus journey; those
remaining operations are not claimed tested. The two real Avalonia regressions
for a selector and pinned deep target both fail on the original source (three
seconds), before the candidate correction. #205's separate full gate continues.

The #206 candidate inspects the exact generation-bearing target when an inline
tree omits it, preserving bounded responses. Stable selector recovery remains
available before applying a state. After capture, missing current evidence fails
with a null target summary instead of silently reusing the earlier node or
substituting a replacement; mutation reset still runs. Twenty-seven affected
picking/matrix cases pass (31 seconds), followed by all three final deep-target
variants (eight seconds), including actual replacement and stale-generation
refusal. Original before-fix failures remain retained in `pseudo-budget-01`.

`pseudo-budget-direct-after-01` retains eleven public calls and 73 independent
identity/state/pixel/cleanup assertions against the exact candidate patch. Deep
target resolution, real pressed/disabled state, enabled restoration, missing and
stale guards now agree through CLI/MCP and the journal. Three images were viewed,
including the independent native restored window. App 2972 and all clients are
gone after Reset/Stop. This is **not** a complete passing state matrix: both
transports claim successful `pointerover` while its pixels equal normal and its
classes lack `:pointerover`. Separate pointer move then delayed inspection still
shows no hover. New #207 preserves this independent synthetic-input/state-verification
defect; no hover fix is claimed by #206.

`pseudo-budget-standalone-after-01` repeats eight public calls with the freshly
built pure host and verified external provider. The same target/pressed/disabled/
restoration and negative guards agree, and the independent hover defect reproduces
on both transports. Three images were viewed, including the native restored
window. Original binary/source/patch identities, response-reported versus independently
counted pixel differences and owned cleanup are retained. App 10996 and every
client are absent after Reset/Stop. This scoped native comparison is Win32 1x;
full combined hosted validation of #206 remains required.

## Public preview follow-up and binding-scope discovery (#205)

`preview-followup-01` executes sixteen public calls (eight MCP/eight CLI) on
`96c1656`, Windows headless Skia / Avalonia 12.1.3. Source and five binary hashes
remain unchanged; only the handoff document was dirty. Five images were viewed.
Thirty-three independent verification assertions retain equal/changed baseline
results on both transports, the exact 28,734 changed pixels, passing/failing
JUnit artifacts, one file-triggered reload, zero unchanged-input reloads, closed
session refusal and error-only filtering with unchanged pixels. A malformed
baseline CLI invocation is separately retained as an agent argument error;
its corrected invocation produces the expected comparison failure.

The deliberately problematic diagnostic scene reveals a binding-scope mismatch.
A separate valid fixture then confirms #205: element-local `DataContext="sample"`
and `Text="{Binding Length}"` render the correct value `6`, pixel-identical to
a literal `Text="6"` reference, while both MCP and CLI report
`binding_missing_datacontext`. The literal reference does not produce that
warning. The original `AddSourceDiagnostics` applies root `content.DataContext`
to ordinary binding references, ignoring the nearer effective source. The
original images/responses remain preserved separately from the candidate.

`verify.py` / `verified-summary.json` retain comparisons, checks and identities.
All sixteen public clients exited; the owned preview session is closed.
Loose-XAML comparison does not validate #203's project-build timeout or #201's
animation timing. #204 remains review with its two new Unix controls awaiting
hosted execution. The separate `c83e50c` gate completed successfully with the
platform results recorded above.

The #205 candidate enables public Avalonia 12.1.3 XAML source information in
loose views and reads diagnostics after visual realization. URI/line/column
mapping identifies the rendered control and its effective DataContext. Local
nulls and missing properties still warn; bindings on DataContext use the visual
parent, matching Avalonia's public-source implementation. Compiled views without
source metadata report informational `binding_datacontext_unverified` instead
of guessing a child's root context, including code-behind context assignments.
Known root elements and declared typed-source diagnostics retain provenance.

Evidence in `binding-scope-01` retains five original regression failures/three
passes, followed by a candidate realization failure (six/two) and correction.
Final twelve scope cases pass (2m01s), each comparing actual rendered pixels
against a literal reference before asserting diagnostics. They cover element,
ancestor, different root, null, invalid path, DataContext binding, compiled
metadata/no metadata/code-behind and a template with different logical/visual
parent contexts. The visual-parent regression fails before its correction.
Four existing typed/resource/source cases passed with the preceding candidate;
the remaining 31 preview smoke cases passed before the final unknown-child and
visual-parent refinements. Full validation of the exact final candidate remains
required; those earlier runs are not represented as a final full gate.

Actual public after-fix exploration includes six CLI/MCP calls with 23 independent
checks after the unknown-child refinement: correct pixels, real missing-path/null
warnings and severity filtering. A seventh MCP call after the visual-parent fix
renders the reviewed correct `6`, pixel-identical to the original reference with
zero diagnostics. Separate identity/patch hashes, client exit and process-absence
checks are retained for these stages. All seven clients exited; no owned preview
process remains. No native/Retina or deterministic-animation result is implied.

## Pointer fixture request sequence and failure propagation (#204)

The original macOS failure remains retained from `8bfcec0` / CI `36146649046`:
expected `pick_node`, received `visual_tree`, followed by a total three-minute
test duration. Its exact preceding transport failure is unknown. A controlled
geometry failure reproduces the method transition: the runner correctly skips
the point query after failed geometry and advances to the next visual tree.
The strict fixture then fails its assertion; previously the caller awaited the
runner before observing that failure, allowing further request deadlines.

The controlled regression initially fails (nine existing cases pass). The
test-only coordinator now cancels before the failing connection closes, retains
the identical primary exception and observes both tasks. Bounded TRX output
records request index, method, fixture target, phase, elapsed time, error type
and result codes without payloads. The original 30-second fixture/client bounds
and all semantic assertions remain. The regression initially used a one-second
client bound to contain the before-fix experiment; its final version uses the
same existing 30-second bound and asserts cancellation directly.

The responder also now creates the next pending pipe before disposing the
accepted connection, matching production `LocalBridgeServer` and the earlier
#184 fixture correction. [Official .NET 10 Unix pipe source](https://github.com/dotnet/runtime/blob/v10.0.7/src/libraries/System.IO.Pipes/src/System/IO/Pipes/NamedPipeServerStream.Unix.cs) disposes the shared
listening socket when its last server instance is disposed; queued connections
can therefore be lost by the old per-request fixture lifetime. Two controlled
Unix tests hold the first reply, connect/write the second request exactly once,
then release the server: the legacy negative control must lose that reply and
the corrected fixture must return it. These tests require Linux/macOS; local
Windows skips are not Unix proof. No production pointer, retry or deadline
behavior is changed, and a passing later run alone cannot establish the
original hosted failure's cause.

Evidence: `artifacts/agent-qa/pointer-sequence-01` retains before/after TRX,
bounded traces and independent `verify.py` checks. The candidate local group
passes ten cases with two Unix skips (final rebuilt run: seven seconds); nineteen real Avalonia picking checks
also pass (41 seconds). Subsequent full `30d176a` CI passes all six jobs; its
macOS TRX individually verifies all twelve pointer cases including both Unix
controls. #204 retains the unknown original preceding cause, as distinguished
from these demonstrated fixture corrections.

## Preview timeout evidence and owned cleanup (#203)

PreviewHost now writes a small atomic progress snapshot with explicit phases,
owned process ids, elapsed times and build-log location. During builds it saves
bounded safe counters and recognized output markers at most four times/second;
arbitrary in-progress output is withheld. Completed build logs retain existing
behavior. Output markers identify observed text, not authoritative MSBuild task
state or a root cause. The client accepts only bounded, matching-process,
allowlisted progress and reports unavailable evidence explicitly.

Timeout handling confirms owned process exit, bounds stream draining separately
and records each stream's completion/count without copying its content. The
same owned cleanup runs for caller cancellation. The existing operation deadline
also covers output draining; default production timeout remains 60 seconds.
An internal standard .NET TimeProvider seam lets a real blocked-build test
expire that deadline only after the controlled marker; it does not control
Avalonia animation time.

The original controlled design-data timeout fails before the change (missing
phase), then passes. Final affected validation passes 29 cases/zero skips in
58 seconds, including real design-data/build children, cancellation, delayed
capture after an actual process exit, invalid progress, baseline options and
normal rendering. Two agent fixture corrections are retained separately: an
inline MSBuild task used an unavailable reference API, and its initial fixed
15-second deadline sometimes preceded the controlled marker. The latter is
replaced by explicit deadline triggering, not a production budget increase.

Actual `preview-timeout-01/journey` has successful CLI no-build baseline creation
at 128x96 and 240x120; both images were viewed. MCP T002 then times out both
fresh builds, honestly returning transport success but baseline failure. The
final CLI T003 returns exit 1 and baseline failure, also in `project_build`:
recognized restore output appears at 43.740/40.403 seconds before each unchanged
60-second deadline. All four owned host/build pairs are absent after completion,
both output captures complete and safe build markers remain available. These
recurrences narrow the observed boundary but do not prove the original B004
cause or a timeout fix. A separate aggregate snapshot afterwards records 100%
CPU, 5221 MiB available memory and 262 page inputs/s; it is evidence of current
host contention, not a causal conclusion. No unrelated processes were changed.

Before/after/fixture corrections, TRX, exact requests/results, immutable tool
snapshots, source hashes and build markers are retained under
`artifacts/agent-qa/preview-timeout-01`. The separate #204 original macOS TRX,
three-minute method mismatch and job log remain under `expanded-campaign`.

## Isolated preview exploration (#174)

### Animation clock API boundary (#201)

The supported timing audit pins Avalonia's official `12.1.3` source to
`8eeda4f6f546165b3f72e63c9f42247abb306905`. `IClock`, `Clock`,
`Animatable.ClockProperty` and `Animatable.Clock` are internal, as is the
`Animation.RunAsync` overload taking a clock. An isolated compile probe against
the actual 12.1.3 package confirms four access errors (CS0122/CS1061/CS0117),
not a restore failure. `MediaContext` drives its UI animation clock from a
private stopwatch; replacing the render timer alone does not control that
clock. `IRenderTimer` is also explicitly marked `PrivateApi`.

Primary sources: [animation clock](https://github.com/AvaloniaUI/Avalonia/blob/12.1.3/src/Avalonia.Base/Animation/Animatable.cs),
[animation execution](https://github.com/AvaloniaUI/Avalonia/blob/12.1.3/src/Avalonia.Base/Animation/Animation.cs),
[UI animation clock](https://github.com/AvaloniaUI/Avalonia/blob/12.1.3/src/Avalonia.Base/Media/MediaContext.cs),
[headless timer](https://github.com/AvaloniaUI/Avalonia/blob/12.1.3/src/Headless/Avalonia.Headless/HeadlessRenderTimer.cs).
The source checkout, exact probe and failed build log are retained under
`artifacts/agent-qa/animation-timing-01`. No supported deterministic clock
control was found in this pinned API. #201 therefore remains open/blocked on
a supported Avalonia timing API or an explicit change to its timing contract.
No private hook, substitute interpolator, sleep-based approximation or passing
animation claim is introduced. Original wrong pixels remain retained. Continue
with #203's independent preview timeout evidence; the overall goal stays active.

`preview-campaign-01` on clean `0658716` (production `4ea4d02`) uses Windows
headless Skia / Avalonia 12.1.3. The 28 public calls (22 MCP/six CLI), 17 positive
checks and nine viewed images include 96/192 DPI, a live XAML change, malformed
XAML failure and exact-pixel recovery, CLI session-store access and identical
CLI/MCP project pixels. Real application resources, compiled design data,
light/dark and en-US/hu-HU appear correctly. Multiple sizes and a contact sheet
render; clipping in the smaller sample is recorded, not claimed responsive.

Independent Pillow analysis agrees with CLI diff and MCP region assertions on
28,760 changed pixels out of 28,800; the deliberate unchanged assertion fails.
Semantic findings retain heuristic provenance. Close rejects subsequent reload,
closed metadata remains listed as designed. Five original sample source hashes
match, client/launcher 19976 exits 0 and no owned preview app remains. The
viewer HTML is generated; visual HTML review is still blocked. Four failed
agent launcher attempts (command PATH/PATHEXT and PowerShell Task output)
precede the successful health probe and are retained separately; they dispatched
no product tool request.

Two product defects prevent an overall pass: #201 samples real project
animation at 0/225/600/600 ms with identical pixels on both CLI and MCP. A
separate red rectangle animating width 20 to 120 over one second measures
66/68/68 at requested 0/500/1000 ms. The timer API uses actual stopwatch time,
not a fixed timestep. #202 accepts build flags in baseline-create but drops
them before rendering, launching a build despite explicit no-build. Its
subsequent NuGet error reflects the limited agent launcher environment and is
not asserted to be a separate product bug. Baseline checks, file watching and
diagnostic filters remain pending. Full requests, tool identity, original
failures, images, independent-pixels.json and preview-summary.json are retained.

## Baseline build selection correction (#202)

The CLI now forwards its already accepted buildOutputRoot, assemblyPath and
noBuild properties and validates no-build using the existing boolean parser.
Four new regressions fail on original source (18 s). The first after run has
23 passing cases and one agent fixture mistake: the existing assembly must sit
under Debug/net10.0 inside buildOutputRoot. After correcting that fixture, all
24 affected baseline/CLI/preview cases pass (101 s), including exact pixels and
a project that fails deliberately if an unwanted build starts.

The actual corrected CLI creates both baseline images using assembly_path and
no build; both are viewed. A separate standard-shell MCP baseline check has one
60-second host timeout followed by an exact zero-pixel comparison. Missing
phase/child evidence for the first viewport is #203, not a claimed successful
round or an inferred root cause. An earlier restricted-launcher run reports
explicit NuGet path1 errors and remains separate. Evidence and candidate hashes
are in preview-campaign-01/baseline-after-01/verified-summary.json, B001–B004 and
the before/after/corrected TRX directories. Full candidate CI `36146649046`
fails the separate macOS #204 fixture; all four #202 regressions pass on both
Windows and macOS. #202 remains review pending a complete passing gate.
Baseline-check still builds fresh project outputs; no-build creation does not
silently pin future comparisons to an old assembly.

## Native workflow execution and replay (#174)

`workflow-standalone-01` uses clean `d4b376e`, Avalonia 12.1.3.0 and Win32 at
actual 1x. Its 27 public calls (21 MCP/six CLI), 20 checks and six viewed images
cover actual Unicode edits and notification changes, explicit postconditions,
complete recording export and two parameterized replay cycles. Validation-only,
missing parameter/owner, changed source, truncated recording and unsupported-plan
guards leave the independent journal unchanged. An intentional failed assertion
stops before the next control step. CLI and MCP both execute/export/replay.

The bounded inline response identifies a complete separate artifact; that artifact
was copied, inspected and used for export. Stable aliases replace transient IDs,
literal values become required parameters and process/evidence bindings must be
fresh. Success and failure JSON/Markdown/JUnit reports match actual outcomes.
Final reset restores the seed, client 3100 exits 0 and owned app 11044 terminates.
Six source hashes and pure-host/provider identity remain verified. The summary,
exact requests/results, journals, reports and images are retained under
`workflow-standalone-01/workflow-summary.json`.

An initial out-of-range evidence depth was rejected before dispatch; a separate
local JSON reader needed UTF-8 BOM handling. Both agent mistakes are recorded;
no application action was repeated because of them.

Fresh `workflow-direct-01` on clean `e91041b` repeats the same positive/negative
CLI/MCP journeys with 26 calls (20 MCP/six CLI), 20 checks, six viewed images and
no agent request errors. A freshly published Direct host and its bridge/core/
protocol snapshots are verified. Client 396 exits 0, owned app 3220 terminates,
final state and six source hashes agree. Both integration summaries retain exact
source identity, requests, independent state, exports and complete reports. No
new product bug in these scoped workflow rounds. Preview/reload/baseline and other
capability boundaries remain incomplete.

## Native test cleanup correction (#200)

Implementation `7f8e46a` is closed after the combined `108adc5` CI above.
The original delayed-EOF cause remains distinct from verified failure-preserving
cleanup behavior.

The controlled before baseline retains two failures after actual owned processes
exit but an injected capture completion remains pending. The correction keeps
termination, bounded exit, each stream and disposal independent, persists safe
phase metadata and preserves primary plus cleanup failures. Missing capture is
explicitly unavailable; it is not a successful empty log. Existing three-second
stream bounds remain unchanged; process exit gains its own three-second bound.
Production runtime behavior is unchanged.

Corrected validation passes 65 affected checks/one separately gated native skip
(25 s). Seven initial after failures are retained as an agent fixture error:
Process.Disposed was an invalid disposal oracle; tests now check public disposed
getter behavior. The actual native integration then passes in 32 s with three
successful audits (13 native nodes). Owned host 22000 exits/is disposed; both logs
and cleanup.json persist. Exit wait takes 1.22 ms; stdout completion follows
2438.88 ms later. This measures separated phases, not the original delayed-EOF
cause. Evidence: expanded-campaign/native-cleanup-{before,after,corrected}-results,
native-uia-cleanup-after-results, uia-owned-cleanup-after-01/accessibility and
native-uia-cleanup-after-verified.json. Combined hosted validation now passes;
ci-108adc5-{windows,macos,linux,native}/verified*.json retains the independent
cross-checks.

## Native accessibility diagnostic checkpoint (#199)

Commit `4ea4d02` retains bounded Windows stage, actual HRESULT, elapsed time,
cancellation and query/provider budgets without exception text. A controlled
owned HWND on an STA thread proves the previously missing diagnostic; it does
not reproduce the original hosted provider failure. All 58 affected checks pass
with one separately gated native skip. The initial test-fixture HRESULT mapping
error is retained separately.

`uia-diagnostic-standalone-after-01` uses clean 4ea4d02, a verified standalone
provider and pure host, Avalonia 12.1.3.0, Win32 at actual 1x. Nine MCP/two CLI calls
pass 14 checks. Before external OS inspection, the first audit observes 36 native
nodes and correctly maps Reset. CLI mapping, intentional name mismatch, redacted
label, stale/unauthorized targets, excluded-subtree refusal and bounded partial
tree semantics pass. Two native/rendered images agree; the application journal
and six source hashes remain unchanged. Client 20824 exits 0 and owned app 12712
terminates. Full evidence and cleanup are retained in `native-uia-summary.json`.

The original native integration case then produces three successful audits
(initial/CLI/MCP, 13 native nodes), but the overall test fails at the three-second
stdout-drain cleanup after 38 seconds. Original TRX and artifacts remain under
`expanded-campaign/native-uia-owned-after-results` and
`uia-owned-integration-after-01`. The already-exited host is PID 9776; no matching
host/test process remains. Missing cleanup logs and lost failure context are now
#200, the sole active issue. #199 remains review, with its original cause unknown;
full CI `36136730284` is running. No budget extension or repeated app action.
Current count: 36 unique defects, 27 closed.

## Logical mutation evidence correction (#197)

Commit `c6b11db` passes all 100 affected mutation/pseudo-state regressions
(32 s, zero failures/skips). The prepared real-Avalonia baseline retains one
visual pass/one logical failure. Thirteen protocol identity cases cover scoped,
foreign, stale, missing, ambiguous and depth-limited captures plus legacy visual
requests. An initial invalid null tree-kind in the new legacy fixture is retained
as an agent test-input error, separately from product failures.

Fresh `logical-evidence-{direct,standalone}-after-01` runs use clean `c6b11db`,
Avalonia 12.1.3.0, Win32, 1120x800 and actual 1x. Each performs 14 MCP/three CLI
calls and reviews seven native/rendered images. Direct passes 16 retained checks;
standalone passes 17 including pure-host/provider isolation. Visual and logical
requests identify the same public generation identity without rewriting opaque
node IDs. The original CLI Unicode Content and MCP Background cases now include
valid before/after visual summaries and changed pixels (2259/4652). A depth-0
capture keeps one-node visual snapshots with precise before/after coverage
diagnostics. Invalid depth creates no artifact directory; a missing logical
target remains stale, applies nothing and changes zero pixels. Four actual
mutations restore original Content/Background/Padding values and priorities,
with no active overrides. Independent journals and six source hashes are unchanged.

Exact requests/results, independent tree proofs, `artifact-audit.json` and
`logical-evidence-summary.json` are retained in each directory. HTML source has
both identities and correct target-found flags; visual browser review remains
blocked by the existing tool policy and was not bypassed. The standalone
verifier initially referenced a variable outside its persistent scope; it read
the saved provider verification record to complete bookkeeping without repeating
application requests. That agent error is retained separately. Clients 6148/5104
exit 0 and owned apps 20308/15968 terminate. This is not native Retina coverage.

#197 is completed after full CI `36130672354` succeeds. Downloaded TRX verifies
Windows 937 passed/six skipped, macOS 938/five and Linux 33 focused cases. All six
native lifecycle runs and three expiry checks verify clean identity and owned
cleanup at actual 1x. These scripted hosted runs are not exploratory Retina
coverage. #181/#199 original causes remain unproven. No release.

## Animated-priority and native UIA discoveries (#198, #199)

The fixture expansion also reopens #181: a fresh local run hits the same image
lock boundary at the explicit exclusive-open assertion for `01-normal.png`,
before teardown. Its owner is still unknown. The original failed result is
retained; no retry or weaker assertion replaces it. At that checkpoint the unique-defect counts
were 35 tickets/24 closed after reopening. The two new fixture-test expectation
mistakes (StyleTrigger priority and style detachment on window close) are recorded
separately from product defects in `animation-fixture-*-error.json` and
`animation-fixture-close-expectation.json`.

The extended #196 audit finds an additional reset boundary, now #198. On
unchanged `5fda3f9` production, all nine initial real-Avalonia cases retain the
temporary green base after successful reset while Blue animation remains active.
The expanded 24-case baseline has 18 failures/six passes. It compares local,
Style, local binding and current-value-over-Style origins against an independently
animated untouched control. Animations beginning after mutation expose both a
hidden Style override and a restored current value replacing ongoing animation.
Before TRX/logs remain in `expanded-campaign/animation-priority*-before*`.
The public-API correction passes all 87 affected mutation/pseudo-state cases
(zero failures/skips, 1m15s). Validation and apply refuse already animated writes
before effects; reset preserves an animation that starts after a supported
mutation while restoring the known base. The shared QA fixture now provides
finite brush timelines, Style/Local target/reference pairs and independent
sampled value/priority evidence. All 16 affected QA fixture/runtime cases pass
after cancellation moves to Closing, before the window detaches its styles and
animations. Earlier evidence includes a 102-pass/one-failure run exposing that
new fixture lifecycle defect; the corrected fixture-only validation takes 33 s
and reuses unchanged previously built dependencies.

Commit `8803271` passes fresh real native brush-timeline journeys in both
integrations at Avalonia 12.1.3.0 / Windows win32 / 1x. Direct has 33 public calls
(29 MCP/four CLI), 16 passing checks and seven viewed images; standalone has the
same calls/images and 17 checks including pure-host/provider identity. Both
verify already-animated refusal, untouched references, supported edits before
animation, individual/reset_all reset without interrupting the timeline, correct
StyleTrigger/Local bases after stop and fixture reset. Four source hashes remain
unchanged. Clients 18156/22152 exit 0; owned apps 2032/6152 terminate. Evidence is
in `animation-{direct,standalone}-after-01/animation-summary.json`. The initial
native screenshot data URLs were saved as their actual JPEG type; rendered
captures are PNG. This is not native Retina coverage.

Full `8803271` CI 36125594904 is successful. Downloaded TRX verifies Windows
923 passed/six skips, macOS 924 passed/five skips and Linux 33 passed. All six
native integrations and three expiry checks pass with verified clean identity
and owned cleanup at 1x. Acceptance closes #196/#198; the fresh pass does not
resolve #181's lock owner or #199's missing COM-stage/cause diagnostics.

Full `5fda3f9` CI 36118971656 finishes failed in the Windows Verify artifacts
step, specifically the native UIA audit (#199). Main Windows TRX passes 895 with
six skips; provider/onboarding/recipe/readiness lanes also pass. The owned native
group has four passes/one failure. Its retained `initial.json` has 20 valid bridge
nodes but zero native UIA nodes, `unavailable`, `truncated=true` and only a generic
`native_accessibility_partial` diagnostic. Timings do not establish a deadline
or COM/provider root cause. Linux/macOS downstream jobs skip. All three separate
native-lab jobs pass; their retained identities/cleanup are audited separately.
Evidence lives under `expanded-campaign/ci-5fda-windows` and the failed job log.
At that earlier failure checkpoint #198 was sole active, #196 review and
#197/#199 ready; subsequent acceptance is recorded above. No release.

## Native precedence verification and combined acceptance audit

Both `style-reset-{direct,standalone}-after-01` runs use clean `5fda3f9`,
Avalonia 12.1.3.0 and Win32 at 1x. Each completes 22 MCP/four CLI calls,
nine passing checks and four reviewed native/rendered images. Actual property
priority, stacked visual/logical targets, unsafe-order refusal, individual reset,
theme changes while local layers remain active and final reset_all agree with
the independent journal. Final Light uses current `#33000000` Style, no active
mutations, unchanged source hashes and exactly two theme changes. Clients
20164/14692 exit 0; owned apps 9180/2064 are terminated.

Direct client initialization mistakes, standalone check-list closure routing and
a Stop outer-redirection collision are retained as agent errors. No product
failure is inferred and no uncertain application action is repeated. Four
misrouted standalone check entries were transferred unchanged, with the mixed
file retained and the nine original Direct entries restored. Stop failed before
child dispatch because the outer command opened the script's own log; the
corrected invocation terminated the verified owned app. See each run's
`style-reset-summary.json` and separately named agent notes.

Full CI 36113144014 on `c1f11d6` completes successfully: Windows 865 passed/six
skips, macOS 866/five skips, Linux 33, all six native integrations and three
expiry checks. Downloaded TRX, clean identities and owned cleanup are verified
under `expanded-campaign/ci-c1f-*`. Acceptance audit closes #179, #181, #183,
#185, #186, #188, #189, #190, #193, #194 and #195. Original-cause uncertainty
is explicitly retained on test/diagnostic corrections; #182 stays open in review.
CI 36118971656 now validates `5fda3f9`; #196 remains active for animated-priority
coverage, and #197 remains ready. Neither this gate nor the Windows journeys
prove Retina behavior or complete the broader capability campaign. No release.

## Native mutation evidence and precedence defects (#196, #197)

Clean bfd7978 `capabilities-direct-evidence-01` runs the actual Direct app on
Avalonia 12.1.3/Win32 1x with 18 public requests (16 MCP/two CLI). Two checks
pass and three retained failures establish two separate defects. A visual
Background mutation records both target summaries and visible pixel changes.
CLI Content and MCP Background mutations through the same button's logical ID
apply successfully but omit both target summaries, although both visual-tree
artifacts contain its shallow visual alias (#197). Public inspection, changed
pixels and the artifact contents independently establish the mismatch.

After reverse reset_all, activeCount is zero and original values return, but
the button's originally styled Background now has LocalValue precedence (#196).
Switching the actual fixture to Dark leaves this button at #33000000 while the
untouched reference changes to #33ffffff at Style precedence. The app journal,
public property inspection, and viewed native/rendered dark images agree.
This differs from #194 ordering: restoration in the valid reverse order still
loses the original value source. The original failed assertion remains classified
as a product failure, not an agent mistake.

Full calls/state, logical-target-repro.json, logical-target-mcp-repro.json,
style-reset-repro.json, E017 dark images, journey-checks.json and
evidence-summary.json are retained in the run directory. Four source hashes
stay unchanged. Theme returns to Light, client 12144 exits 0 with empty stderr,
and owned Stop terminates app 21428; the residual local override persists until
termination and is not claimed repaired by cleanup. HTML visual review remains
blocked by the previously observed local-file policy; no alternate serving
route is attempted. #196 is selected first; #197 and remaining #174 are ready.

The prepared pre-fix regression confirms 15 failures/15 passes in 30 cases:
style, theme, style binding and current-value overrides lose precedence across
individual, reset_all and deactivation paths. Local values/null, local bindings,
inheritance and default values pass the original behavior. The first 24-case
attempt retains nine product failures plus three agent fixture errors from
checking new styles before rendering; the prepared rerun explicitly renders
before the future-update assertion. Both TRX files and classifications are in
expanded-campaign/style-reset-before[-prepared]-results.

The implementation uses Avalonia 12.1.3's public property diagnostics for local
precedence and current-value overrides, clears temporary locals when the original
source was lower priority, and restores existing current-value overrides through
SetCurrentValue. The previous IsSet/unknown-source fallback is removed: mutation
snapshot reads run on the UI thread with already resolved public properties and
must have actual source diagnostics before applying an override. Relevant source:
[IsSet/GetDiagnostic](https://raw.githubusercontent.com/AvaloniaUI/Avalonia/12.1.3/src/Avalonia.Base/AvaloniaObject.cs),
[value-store priorities](https://raw.githubusercontent.com/AvaloniaUI/Avalonia/12.1.3/src/Avalonia.Base/PropertyStore/ValueStore.cs),
[current-value diagnostics](https://raw.githubusercontent.com/AvaloniaUI/Avalonia/12.1.3/src/Avalonia.Base/Diagnostics/AvaloniaPropertyValue.cs).
All 63 affected mutation/pseudo-state cases pass after the fix (36 s, clean
build), including all 30 precedence cases; TRX is retained in
expanded-campaign/style-reset-after-results. Native verification remains pending.

## Native mutation reset-order defect (#194)

Fix 5d0df14 passes fresh native Win32/Avalonia 12.1.3/1x comparison:
`mutation-order-direct-after-01` retains 36 public calls (21 MCP/15 CLI),
13 passing checks and one explicitly classified agent mistake;
`mutation-order-standalone-after-01` retains 28 calls (16 MCP/12 CLI) and
11 passing checks, with no failure. CLI/MCP unsafe resets return rejected,
the corresponding blocking mutation ID and zero extra Text events. Actual
logical/visual aliases share the guard; same-name classes/resources remain
independent. Individual reverse resets and reset_all restore Ada, Width=360,
the original class list and a truthful empty active registry. Journal counts,
public inspection and viewed native/rendered images agree; source hashes stay
unchanged. Native resource-value coverage is response/registry only, with the
independent value oracle provided by the real-Avalonia regressions.

The Direct agent binding error occurred after a successful set A; the agent
incorrectly repeated it, then asserted two active entries instead of the actual
three. Original calls and failed assertion remain; explicit reset_all recovers
Ada and a separate clean sequence verifies two layers. The first MD030 native
image includes the newly opened standalone window's occlusion; a fresh foreground
image and rendered image were both inspected. Clients 11480/21020 exit 0 with
empty stderr; owned Stop terminates apps 12228/4476. Detailed evidence is in each
run's mutation-summary.json, journey-checks.json, full calls/state and images.
#194 is in review pending a fresh combined gate; no new product failure is
claimed from these scoped native checks.

Clean afd4ad0 `capabilities-direct-mutation-01` records 20 public calls
(15 MCP/five CLI) on Avalonia 12.1.3/Win32 1x. Six checks pass, one agent
assertion contract mistake is retained separately, and one product defect is
confirmed. Unsupported/invalid values do not change state; Unicode Text mutation,
bounded history/source advice and its single reset agree with app counters and
viewed pixels. Runtime/source files are not changed by these experiments.

M012 sets the original Ada to Mutation A; M013 sets Mutation B. Resetting the
earlier A in M014 unexpectedly sets Ada while M015 still lists B active. M016
reset_all then leaves Mutation A, although M017 reports zero active mutations.
M018 native/rendered images and the independent journal confirm the incorrect
final value. Both mutation resets return applied/success. The reset closures
capture the immediately previous value; removing an earlier closure loses the
original baseline for later cleanup. This is #194, not a passing campaign.
M019 explicit fixture Reset recovers Ada; M020 inspection/journal agree. Client
20728 exits 0 with empty stderr and owned Stop terminates app 21148. Reproduction,
full calls/state, five reviewed images, source hashes and cleanup are retained
in reset-order-repro.json, mutation-summary.json and journey-checks.json.

The evidence tree has explicit byte/depth budgets: requested depth 32 is copied
as a 27-node inline tree, while a 193-node/full-depth artifact is also returned
and contains the target. The mutation evidence reports missing target summaries;
full target evidence and HTML visual review are not claimed. Class/resource and
standalone mutation coverage remain pending.

#194's six before-fix real-Avalonia property/class/resource cases all fail at the
unsafe-reset assertion, retaining complete responses and actual values. The
guard now compares actual owner/value identity, including visual/logical aliases,
and refuses an older overlapping reset with an actionable blocking mutation ID.
Validation and execution share the guard; independent targets/values remain
resettable. All 27 affected mutation/evidence/review cases pass (20s, clean build),
including nine ordering cases with individual reverse reset, reset_all and
deactivation. Original values, event counts and active registry are checked.
TRX and verified summaries are in `expanded-campaign/mutation-order-{before,after}-results`.
Native comparison is recorded above; the fresh combined gate is still pending.

## Hosted dispatch-precondition fixture readiness (#195)

Full CI 36107667148 on 511312a fails only macOS
`RuntimeDispatchPreconditionTests.WorkflowReplayPreservesOriginalGuardAndAsyncCompletionRequiresSeparateVerification`:
WithWindow line 178's initial readiness assertion fails in 1.138s before the
test body. The assertion discards the error/phase evidence, so the root cause is
unknown. The window has no explicit initial headless frame preparation; #195
tracks measurement and an evidence-based fixture correction, not an assumed
production dispatch/replay defect. Windows has 856 passes/six skips; Linux has
33 passes; all six native lifecycle combinations and their owned cleanup pass.
macOS has 856 passes/one failure/five skips (9m17s); its downstream workflow and
packaging steps are skipped. All five #193 cases and #185's gesture case pass.
TRX/logs, per-platform verified summaries and native cleanup are retained in
`expanded-campaign/ci-511-*`. All native runners use 1x, not Retina.

The instrumented unchanged local precondition fixture passes all five cases
(18s). All five initially have valid layout but no frame; unchanged readiness
completes in 7.52-354.15ms. This measures a preparation gap without reproducing
or proving the original hosted timeout cause. `dispatch-readiness-before-results`
retains TRX and the phase summary. The correction prepares/requires a real
500x300 frame before the unchanged readiness probe, then asserts rendered status,
valid layout and observed pixels; bounded initial/preparation/probe evidence is
retained. No production code, deadline, replay or dispatch assertion changes.

The startup-readiness audit identifies 15 matching initial fixture sites across
12 test files. Each now captures/requires its declared pixel dimensions before
the initial probe (or before the test body that adds controls and first probes):

| Fixture | Initial frame dimensions |
| --- | --- |
| Dispatch preconditions | 500x300, with before/after phase timings |
| Explicit input; picking | 300x220 each; picking now checks the initial readiness result |
| Focus | 400x240 |
| Expressions; desired state; relationship queries | 500x700 each |
| Provenance | 320x220 |
| Navigation | 400x220 |
| Observation after initial focus | 300x180 |
| Rendering regression | 300x300 DIP at the case's actual 1/1.5/2 scale |
| Bridge input; nested pointer; durable selector; targeted click | 360x240; 260x180; 420x320; 320x180 |

These sites shared Show/RunJobs/layout followed by first-frame-dependent checks.
Mid-test geometry/content changes continue to exercise readiness as before.
`RuntimeReadinessTests` intentionally probes unprepared, hidden and animated
states and is unchanged; #193's action and #185's gesture preparation already
require a frame. Official Avalonia 12.1.3 CaptureRenderedFrame pumps bounded
dispatcher/render work, while GetLastRenderedFrame only samples. The affected
local group passes all 138 cases (4m18s, clean build); the separately named
`McpMutateNodeEvidenceCapturesScreenshotsTreesAndDiffThroughLocalBridgePipe`
also passes (4s). All five measured precondition fixtures initially lack a frame;
preparation takes 2.72-4.16ms and the unchanged readiness probe 7.95-34.43ms.
TRX and phase summaries are retained in `dispatch-readiness-after-results` and
`mutation-mcp-evidence-after-results`. Fresh combined hosted confirmation remains
pending; this does not prove the discarded original error's root cause.

## Action-explanation fixture readiness (#193)

The original macOS failure on 483fedb remains retained. Local instrumentation
of the unchanged preparation sequence passes all five action-explanation cases
(18s), but three begin with valid layout and no rendered frame, including
DisabledSave. Readiness subsequently completes in 56.7-123.5ms. This establishes
a preparation gap; it does not reproduce or prove the original hosted cause.

The fixture now uses [Avalonia 12.1.3's public headless frame capture](https://raw.githubusercontent.com/AvaloniaUI/Avalonia/12.1.3/src/Headless/Avalonia.Headless/HeadlessWindowExtensions.cs)
to prepare and require a real 320x240 image before its unchanged 1000ms readiness
probe. It retains bounded initial/frame/layout/error and preparation/probe timing,
and additionally requires rendered readiness, valid layout and observed pixels.
Action assertions, production code, deadlines and retries remain unchanged.
The affected action/readiness/picking/explicit-input/gesture group passes all 39
cases (1m13s) with a clean build. All five action fixtures initially lack a frame;
preparation takes 1.31-2.03ms and the subsequent probes 4.71-20.64ms. Evidence:
`action-readiness-before-results` and `action-readiness-after-results`, full
TRX and verified summaries. Hosted full-gate confirmation remains pending.

#184 is complete at 8bf3c4b: the full affected Linux Installer gate now passes
and both the original recovery case and queued Unix regression pass on Linux
and macOS. The original before-fix failures remain retained. This does not
convert the unrelated #193 failure or the unfinished #174 campaign into a pass.

## Expanded Direct table comparison and hosted gate (#174)

Clean e3cf363 run `capabilities-direct-table-01` completes 40 public
requests (29 MCP/11 CLI) and 26 independent passing checks on native Win32,
Avalonia 12.1.3, 1120x800 at 1x. Complete 200-row reads, last-page projection,
AND filtering, explicit incomplete scans, invalid bounds, offscreen selection,
read-only/type refusal, Unicode/numeric editor changes, replay/conflict, sort,
privacy, invalid draft recovery and reset/stale/historical replay match the
independent journal. An incorrect column generation refuses with zero dispatch;
physical column replacement is not claimed. The invalid 3.5 draft leaves source
37 unchanged; Escape plus observed valid source and explicit Enter finish allow
a new score 38 exactly once. All four native/rendered positive and invalid
images were viewed. Initial native accessibility lagged the pixels; a fresh
observation agrees with Unicode/37/edits=2, and both observations are retained.
No new product defect was confirmed. Final reset restores all seed records;
client 15468 exits 0 with empty stderr and owned Stop terminates app 360.
Evidence: table-summary.json, journey-checks.json, DT001-DT040 and client-exit.json.

CI 36102961992 on clean 483fedb is terminal **failed** only on macOS Test:
Windows 856 passed/six explicit skips (12m04s), Linux 33 passed (1m02s),
macOS 856 passed/one failed/five skips (11m16s). #184 queued Unix cases pass
on Linux/macOS (269/299 ms); #185 macOS gesture passes with frame/hit evidence
(1.583s). All six native lab integrations pass two cycles and expiry; downloaded
identities and owned terminations are verified in ci-483-native/verified-summary.json.
All observed scales remain 1x, so this is not Retina acceptance. macOS downstream
workflow/package checks are skipped after Test failure.

New #193 retains ActionExplanationTests' WithWindow readiness failure at line
282, before the actual test body. The original assertion captured no phase
evidence; no production cause is inferred. Both hosted Unicode variants and
the controlled cooperative-budget case pass on Windows/macOS. Windows's eight
routed input spans are 0.306-20.449 ms and every expected text/change count agrees;
this does not explain #191's original failure or resolve #192's local contention.
Full TRX, bounded summaries and timing records remain under ci-483-*.
#193 is next, followed by remaining capability families; #174 remains incomplete.

## Expanded standalone table and form journeys (#174)

Clean `483fedb` run `capabilities-standalone-01` uses the immutable standalone
host/provider, Avalonia 12.1.3, Win32 and 1120x800 at 1x. T001-T051 contain 49
public requests (37 MCP/12 CLI; T038/T039 are independent native observations).
Twenty-seven table checks pass. Three retained agent errors are classified:
an old Direct oracle closure, an incorrect expected conflict-code spelling, and
assuming cell cancellation also finished the row. The first was detected before
further edits; requests/snapshots were already in the correct standalone run and
the accidentally appended Direct check was restored with the original failed
evidence retained in `harness-path-error.json`. No new product defect is confirmed.

The table round verifies all 200 available rows, typed values, projected CLI
paging to unrealized QA-200, AND filtering, bounded incomplete scans, invalid
bounds, realization/selection of QA-199 and exact cross-adapter replay. Stale
revision, read-only column and wrong scalar type refuse without mutation. Real
cell editors commit a Unicode status and integer score once each. Exact replay,
already-satisfied values and conflicting payload reuse add no edits. Descending
sort agrees with the independent view order; projection agrees across adapters.
Redacted cells remain unavailable to filters without exposing the protected value;
the CLI policy-denied action does not change selection or records.

A fractional value in the integer Score column produces a visible invalid draft
with original source value 37, `table_editor_not_verified` and `tableEditing:true`.
Query returns the original numeric value with validation errors, and a new action
refuses with zero dispatch. Escape clears the invalid cell but leaves the row
transaction active in this fixture. A second synthetic Escape and a native Escape
do not establish completed recovery; an already-satisfied selection also cannot
prove that no edit is active. After observing the valid original value, explicit
Enter finishes the row and a new edit verifies score 38 exactly once. Reset
restores records, sort, selection and counters. Fresh same-key row generations
differ; old generations refuse, while exact pre-reset replay returns historical
score 37 without touching the new record's 98. The final reset restores the seed.
Native and rendered positive/invalid frames (T020/T027), plus recovery observations
T038/T039, were actually viewed and retained.

SF001-SF019 add 19 form requests (13 MCP/six CLI) and 13 passing checks. Eight-field
inventory, password redaction, labels, read-only mixed-plan refusal, invalid bounds,
Unicode fill, lower range boundary 0, no-submit behavior, exact replay/conflict and
separate CLI submission agree with the app journal and viewed SF010 images. Invalid
email preserves the saved profile and exposes one error in CLI inspection and both
SF015 images. Out-of-range priority 101 refuses before a change. Repairing text
correctly reports partial effects until explicit revalidation; exact partial replay
adds no events, and separate submit saves the repaired profile once more. Native
ComboBox inventory remains explicitly partial; standalone choice selection was not
exercised in this round. Response scans find no synthetic password.

Together the rounds retain 68 public requests and 40 passing checks, plus three
classified agent errors. Both clients exit 0 with empty stderr (PID 19888 after
23 MCP calls, PID 20964 after 27). Final reset clears saved/validation/table state;
owned Stop closes the session and terminates PID 8208. `table-summary.json` and
`form-summary.json` include cleanup. No local test/QA process remains. Direct table
comparison and the remaining capability families are still pending; this is scoped
native evidence, not a comprehensive passing gate.

## Desired-state budget and MCP lifecycle diagnosis (#191)

CI `36100032601` is terminal failure on clean `3b82d1d`. Windows has 853
passed/two failed/six explicit skips; its two failures are post-dispatch
`desired_state_budget` responses in the persistent Unicode regression. All three
native QA jobs pass, including Direct/standalone two-cycle validation and expiry.
Downloaded `ci-3b82-native/verified-summary.json` verifies all six clean commit
identities, stopped sessions and owned process termination. All scales are 1;
only Linux observes 1920x1080 (Windows 1028x749, macOS 1920x649). Downstream
Linux/macOS jobs are skipped, so #184/#185 Unix after-fix checks remain pending.

The unchanged local two-case Unicode round passes (53 seconds). Test-only
instrumentation now records bounded focus/selection/caret/text and routed-input
timestamps, input/change counts and actual expected-versus-seed state even if
the response wait fails. It preserves every existing success/content assertion
and adds once-only event assertions. Initial instrumented validation passes all
six persistent-client cases (1m54s); eight measured input spans are 0.85-131 ms
and all intended texts are present. Evidence: `desired-budget-before-results`
and `desired-budget-diagnostics-results`, including `verified-timings.json`.
These passing measurements do not reproduce or explain the hosted budget failure.

The [Avalonia 12.1.3 TextBox implementation](https://raw.githubusercontent.com/AvaloniaUI/Avalonia/12.1.3/src/Avalonia.Controls/TextBox.cs)
changes text and synchronizes presenter/caret within the routed callback. A new
controlled regression deliberately holds a callback for 2100 ms after the actual
edit. It passes in 2.658 seconds: the unchanged cooperative budget returns
uncertain/unverified, the editor changed exactly once, exact replay retains the
original outcome without another event, and conflicting payload reuse refuses.
This validates the safety boundary, not the original slowdown's cause. Production
code, deadlines, test timeouts and retry behavior are unchanged.

The expanded local group finishes 15 passed/five failed (6m19s). Its five failures
are MCP connect/initial-health/first-response/process-exit waits, not a reproduced
two-second operation failure. One first response times out with zero events and
the seed intact; the other case completes all persistent requests/replay and the
file edit, then times out waiting for process exit. Its independent text and all
four events agree. These failures are separately tracked as #192. The retained
host sample at 06:22:41 UTC shows 100% CPU, 4606 MiB available RAM and unrelated
pre-existing TradeR.Mcp processes dominating resources; those processes are outside
task ownership. Contention is observed, not a proven complete causal explanation.
`desired-budget-after-results` retains full TRX, phase observations and host sample.
The affected group is failed, not passed; independent hosted validation is next.
No active local test/QA process remains after the group's cleanup.

## Expanded Direct form journey (#174, scoped evidence)

Clean `3b82d1d` native Direct run `capabilities-direct-01` uses Avalonia 12.1.3,
Win32, 1120x800 at 1x. F001-F032 retain 23 actual persistent MCP calls and nine
CLI calls, independent app journals, responses and four reviewed native/rendered
images (F014/F026). Twenty-nine checks pass. Three retained failures are agent
request/assertion mistakes: legacy click on a TabItem instead of select, expecting
CLI argument-error exit 1 instead of 2, and expecting success on an invalid form
plan. Corrected requests/assertions verify the intended refusals without effects;
these are not product defects or 32 passing checks.

Eight-field inspection agrees across adapters, redacts the password, and reports
bounded/partial coverage. Invalid bounds and a plan containing a read-only field
refuse before mutation. A four-field Unicode/name/email/consent/range fill verifies
without saving; exact cross-adapter replay and already-satisfied values produce
no new journal events, while changed-payload reuse refuses. Explicit popup input
selects Reviewer and separate submit saves once. The native ComboBox exposes its
choices in a separate popup; form/desired-state selection requires same-window
targets and refuses that combination. This documented limitation is not a full
form-selection pass.

Invalid name/email preserve the previous saved profile and expose both errors
through inspection and reviewed pixels. Because the fixture validates on submit,
repairing text retains old validation errors and correctly reports partial effects
without submitting or proceeding to the next field. Exact partial replay adds no
events. Explicit revalidation, remaining repair and separate submit save the
correct profile exactly once more and clear both errors. Reset restores the seed.
Response scanning finds no fixture password. Client PID 21196 exits 0 after 23
calls with empty stderr; owned Stop terminates app PID 21188 and closes the
session. This scoped form round confirms no new product defect. Standalone forms,
the current table journey and the remaining capability inventory are unfinished.

The concurrent full Windows gate on `3b82d1d` in CI `36100032601` instead fails
two persistent Unicode regression variants: 853 passed/two failed/six explicit
skips (9m41s). Both transport-successful replies report `desired_state_budget`
after one routed text operation, uncertain status and unavailable after-state.
The false/full-result variant had already preserved the preceding Unicode value.
This is tracked separately as [#191](https://github.com/RolandUI/AvaScope/issues/191),
not a demonstrated encoding regression. Logs and full TRX with verified outcomes
remain in `expanded-campaign/ci-3b82-windows*`. Native QA jobs still run; downstream
Linux/macOS jobs are skipped, leaving #184/#185 after-fix validation pending.
Return #174 to ready while diagnosing #191; no comprehensive passing claim.

## Initial gesture frame readiness (#185, validation in progress)

The first provider-backed slider drag fails on macOS in both retained hosted
runs: 19 ms on b90ca39 and 12 ms on 8bebfe6. The latter confirms that #188's
input-transparent hit selection did not resolve this case. A successful visual
tree query and drained dispatcher jobs were the fixture's only prerequisites.

The [Avalonia 12.1.3 compositor source](https://raw.githubusercontent.com/AvaloniaUI/Avalonia/12.1.3/src/Avalonia.Base/Rendering/Composition/CompositingRenderer.cs)
publishes updated hit data after a full render. Its
[headless timer](https://raw.githubusercontent.com/AvaloniaUI/Avalonia/12.1.3/src/Headless/Avalonia.Headless/HeadlessRenderTimer.cs)
uses a scheduled UI dispatcher tick; draining available jobs is not a first-frame
barrier. The test now logs bounded initial frame availability, target bounds,
layout validity and center hit ancestry, then uses the public
[`CaptureRenderedFrame`](https://raw.githubusercontent.com/AvaloniaUI/Avalonia/12.1.3/src/Headless/Avalonia.Headless/HeadlessWindowExtensions.cs)
barrier. It requires an actual 500x430 frame and a center hit belonging to the
slider before sending input, and checks exactly one range-value change.
Production input guards, fallback/capture cleanup, negative cases and operation
timeouts are unchanged. Linux/macOS CI now retain TRX output, including these
diagnostics, alongside the existing Windows evidence.

The initial instrumented local cold run passes (9 seconds,
`gesture-readiness-before-results/gesture-readiness-before.trx`): a frame already
exists, bounds are 20,20,200,40 and the center hit belongs to the slider template.
This is not a local reproduction; the two uninstrumented hosted failures do not
reveal their exact compositor sequence. Post-change focused validation passes all
36 gesture, explicit input, picking and action-explanation cases (one minute,
clean build, `gesture-readiness-after-results/gesture-readiness-after.trx`). The
gesture output records a 500x430 ready frame, valid layout and slider-template
center hit; the once-only range assertion and all existing fallback/cancellation
checks pass. Hosted macOS after-fix and the combined gate remain required.

## Unix recovery connection (#184, fix validation in progress)

The before-fix lifecycle fixture disposes its sole named-pipe server after every
response. In the [official .NET 10 Unix implementation](https://raw.githubusercontent.com/dotnet/runtime/v10.0.0/src/libraries/System.IO.Pipes/src/System/IO/Pipes/NamedPipeServerStream.Unix.cs),
last-instance disposal unlinks the path and closes the listening socket;
`Disconnect` releases the accepted connection while retaining the listener.
This identifies a possible queued-connection race, not the exact cause of the
original hosted failure.

A bounded test-only response gate now holds the real fixture after its first
health reply. The Unix regression connects and writes one outsider acquire while
that response remains held, then releases the fixture and requires a correlated
`session_control_conflict` response with the original lease intact. There is no
acquire retry. Windows explicitly skips this socket-backlog test. The existing
agent-kill/resume/cleanup test now includes bounded redacted message and
allowlisted IPC phase/attempt/timing/byte diagnostics on conflict failure;
neither the saved token nor a successful response's token is serialized.
Local WSL/Linux is unavailable; hosted Unix validation is required.

The fixture builds cleanly; local Windows recovery validation passes ten cases
and explicitly skips the Unix case (1m32s, clean build,
`unix-queue-before-windows-results/unix-queue-before-windows.trx`). This is a
regression/diagnostic checkpoint for hosted reproduction, not a validated Unix
fix. The original b90ca39 hosted failure remains retained.

The before-fix Linux job `107954766546` in CI `36096369354` now reproduces
the controlled race on unchanged `8bebfe6`: `queued_response` fails with
`IOException` / `SocketException: Connection reset by peer` after 206 ms
(245 ms test), fixture PID 6005 still alive, no acquire retry. Aggregate:
32 passed/one failed/zero skips, 55 seconds. Raw/clean logs are retained in
`expanded-campaign/ci-8beb-linux-raw.log` / `ci-8beb-linux.log`. The original
agent-kill/resume case passes this run. This proves the controlled fixture race,
but cannot recover the exact original b90ca39 request sequence.
macOS job `107954766598` independently reproduces the same queued-response loss
after 287 ms (365 ms test): an empty response with fixture PID 15467 still alive.
Its full run has 854 passed/two failed/five explicit skips (9m56s); the second
failure is the existing #185 first slider gesture, again rejected as obscured
after only 12 ms. `ci-8beb-macos-raw.log` / `ci-8beb-macos.log` retain both. The
before-fix workflow is terminal failure, not a passing combined gate.

The fixture now follows the production bridge's existing pattern: create the
next pending pipe before handling/disposal of the accepted connection, retaining
the shared Unix listener. The first listener exists before manifest publication;
processing remains sequential and the pending pipe is disposed on exit. Response
disposal, ownership rules, deadlines and client retry behavior are unchanged.
The explicit fixture build passes in 21 seconds with zero warnings/errors.
Fresh Windows affected validation passes 35 cases/one explicit Unix skip
(2m09s, `unix-listener-after-windows-results/unix-listener-after-windows.trx`),
including recovery, launch ownership, session control, scenarios and integration
verification. `unix-listener-after-build-identity.json` pins the rebuilt fixture
and edited source hashes. Hosted Unix after-fix and combined gates are pending.
An initial post-edit invocation rebuilt only the test project and used the old
separately built fixture; `unix-listener-pre-rebuild-note.json` records that
binary's timestamp/hash. Its 26 pass/one skip result is retained separately as
`unix-listener-pre-rebuild.trx`, not post-fix evidence. The actual affected rerun
above follows the explicit fixture rebuild.

The independent production-bridge comparison on clean `8bebfe6`, retained in
`ownership-direct-01`, passes 24 checks through 25 actual persistent MCP calls
and seven CLI calls. Native Windows/Avalonia 12.1.3/Win32/1x ownership rejects an
outsider's acquisition, input and cleanup; the remembered owner toggles exactly
once. Explicit CLI-to-MCP handoff/renewal, stale-token and invalid-TTL refusals,
observed expiry, refusal after expiry and explicit reacquisition all agree with
the app journal. Viewed Windows/rendered images show the same active state.
Only the two permitted input cases and reset change the journal. Recovery cleans
only recorded app PID 19164, repeated recovery agrees, and Stop confirms
`already_exited`. Client 6468 exits 0 after 25 calls with empty stderr; the native
window is gone. All retained protocol requests/responses, including nested MCP
text, redact lease tokens; the evidence scan finds no known raw lease token.
This is Windows production ownership evidence, not a Unix fixture fix or proof
of the original hosted failure's cause. Full CI
[36096369354](https://github.com/RolandUI/AvaScope/actions/runs/36096369354)
is running the unchanged before-fix checkpoint. Windows Build/Test/Pack is now
complete: 855 passed/six explicit skips (including the Unix case), zero failures,
11m34s, plus the packaged-installer case. Raw/clean job logs are retained as
`expanded-campaign/ci-8beb-windows-raw.log` / `ci-8beb-windows.log`. The macOS
native QA job also passed. All three native QA jobs have now passed; their
downloaded `ci-8beb-native` lifecycle summaries verify both integrations, two
cycles and expiry. All observed scales are 1. Full HD is actually observed only
on X11 (Windows observes 1028x749, macOS 1920x649); requested size is not evidence
of observed size. The complete workflow fails on the controlled Unix
reproduction on both hosts and the existing macOS #185 gesture case.

Fresh clean `a099b86` standalone comparison `ownership-standalone-01` passes all
34 retained checks, through 25 MCP/seven CLI calls, on native Win32/Avalonia
12.1.3/1120x800/1x. Provider manifest SHA-256 is
`aefea294a734bf699fcacde04420a66b1373c73ed8580dc37833490c840ae074`.
The same contention, handoff, old/invalid/expired-token, reacquire, reset and
owned cleanup journeys pass. Viewed Windows/rendered images show notifications
enabled and exactly one toggle; only the two permitted toggles and reset change
the independent journal. Token-redacted protocol evidence has no known raw
lease token. App PID 1760 is gone, client 15016 exits 0 after 25 calls with empty
stderr, repeated recovery agrees and Stop confirms `already_exited`/`stopped`.
These are independent integration checks; neither asserts a Unix fixture fix.

## Failed startup cleanup (#190, combined gate pending)

The controlled lifecycle app now supports an explicit 60-second top-level
response stall and timestamps entry into that probe. A real CLI scenario with
a 15-second launch budget reaches `top_levels`, returns the retained partial
failure and terminates its directly owned process. The initial new test wrongly
expected the CLI error envelope's `success` to be true; that setup error is
retained in `qa-stop-before-results`, not counted as a product reproduction.
After correcting it, `qa-stop-before-valid-results` records the actual lab Stop
failure: the already-exited app has no live session. The changed-identity case
also cannot reach run recovery with the old script. Both cases fail (57 seconds).

The lab now routes failed/interrupted startup cleanup through the existing
private `recover-run` contract, including a previous failed Stop that copied a
stale session ID. Process/start identity, active-run and control-lease checks stay
in the existing Core implementation. Cleanup status is separate from startup
status; the lifecycle script attempts it whenever a run ID was recorded. No
launcher, bridge deadline or operation retry change is made.

The original native `legacy-hit-standalone-01` was recovered through the actual
updated Stop command: exit 0, `cleaned`, `already_exited`, with original
`outcome=failed` / `failureStage=top_levels`. Its QA status is restored to `failed`
from the old Stop's `cleanup_failed`; the guessed stale session ID is cleared.
Pre-recovery QA/private records are preserved. SHA-256 checks prove original
startup and failed Stop responses are unchanged (`recovery-190-audit.json`).
The bootstrap latency cause remains unknown. All 25 affected launch, scenario,
ownership and recovery guards pass (2m16s, no skips, clean build,
`qa-stop-after-results/qa-stop-after.trx`). This includes original/previously
attempted failed Stop, repeated cleanup and a mismatched live PID's refusal.
Fresh native `cleanup-standalone-01` on clean pushed `b70e35c` passes ten retained
checks: actual Win32/Avalonia 12.1.3/1x/1120x800, three readiness observations,
unchanged seeded journal after capture, viewed Windows/rendered images, exactly
one toggle, reset to generation two and owned app PID 9956 termination. Client
1828 exits 0 after three MCP calls, empty stderr; independent Windows inventory
confirms the QA window is gone. The existing verified provider manifest remains
`aefea294a734bf699fcacde04420a66b1373c73ed8580dc37833490c840ae074`.
Both failed-start recovery and ordinary ready-session cleanup are now exercised.
The successful fresh start does not explain the original bootstrap latency.
#190 is ready for the combined gate; #184 follows. No release.

## CLI timeout lifecycle (#189, combined gate pending)

The unchanged launcher was measured with a controlled bridge-less child under
`expanded-campaign/launch-lifecycle-probe-01`: CLI JSON at 3918 ms, process exit
at 3995 ms and redirected stream closure at 5306 ms. The actual child was
independently observed alive afterward and released at 32755 ms; both child and
detached launcher subsequently exited. The timeout payload reports launcher PID
13824, whereas the child-owned ready record identifies PID 17580 and its exact
start time. No production wait defect is reproduced, and these measurements do
not establish the original full-suite 7.767-second failure's cause.

The Windows CLI regression now holds its child until explicit release and
separately observes the CLI's `Exited` event, output completion and the child's
identity/liveness. The actual bridge attach deadline remains 500 ms. A 60-second
outer test bound and the child's 120-second fallback prevent indefinite waits;
the obsolete less-than-four-second wall-clock assertion is removed. Failure
evidence retains phase/timing, bounded output, process identities and logs.
Owned cleanup checks both PID and start time; late children see the retained
stop marker. No release or launcher implementation change is made for #189.

All 13 affected launch/scenario lifecycle tests pass (2m53s, no skips,
`launch-lifecycle-results/launch-lifecycle.trx`). After removing diagnostic
analyzer warnings and making the helper's script policy explicit, the final
exact CLI regression passes again (11 seconds, clean build,
`launch-lifecycle-final-results/launch-lifecycle-final.trx`). No helper process
remains. The original full-suite failure is retained; a fresh combined gate
remains required. #190 cleanup recovery is next.

## Legacy input hit selection (#188, combined gate pending)

All ten real-Avalonia child/sibling-overlay cases fail before the correction
(`legacy-hit-before-results`, seven seconds). Independent handlers show events
delivered to an input-transparent visual for move/down/up and child drag;
sibling-overlay drag is incorrectly blocked, and coordinate focus targets the
wrong element. A separate valid before-case proves an actual click succeeds
while the same transparent overlay makes its tree report `actionable=false`
(`legacy-actionability-before-valid-results`, three seconds). The first attempt
at that test reparented an attached control and failed during setup; that
retained attempt is an authoring error, not a product reproduction.

The seven-line correction uses public `InputHitTest` for six legacy input,
gesture, coordinate-focus and actionability selections and fixes shared
`hitTestSource` metadata. Existing click dispatch already uses this API.
`enabledElementsOnly:false` preserves the existing picking/click behavior and
separate input guards; capture/release ownership is unchanged. This choice is
checked against [Avalonia 12.1.3 InputExtensions](https://raw.githubusercontent.com/AvaloniaUI/Avalonia/12.1.3/src/Avalonia.Base/Input/InputExtensions.cs).
All 59 affected Release input/picking/focus/provenance/actionability and pointer
diagnostic cases now pass (1m41s), including all eleven new reproductions,
paired capture/release, genuine input overlays and disabled-target refusal.
Full Release build passes with zero warnings/errors. Fresh Direct host and
standalone provider packaging pass; the provider's verified manifest SHA-256
is `aefea294a734bf699fcacde04420a66b1373c73ed8580dc37833490c840ae074`.

Fresh native `legacy-hit-direct-01` N001-N014 passes 22 retained checks including
reset. Public picking, MCP center diagnostics and actual CLI center movement
all select the pad through its input-transparent text, with no mismatch.
Coordinate focus and independent focus inspection agree. MCP down/CLI up make
one click; a legacy synthetic half-width gesture yields two total presses and
releases, one 259x0 DIP drag. An extra release and move-only probes leave the
independent journal unchanged. Wrong-node assertion still refuses. Windows
accessibility/pixels and the public marker image were reviewed. Reset/owned
PID 18512 termination succeed; client 2116 exits 0 after 11 calls, empty stderr.
This is real native Win32 1x app validation of legacy synthetic event routes,
not native Retina evidence.

`legacy-hit-standalone-01` fails startup at `top_levels`: the provider reports
activation and writes a manifest near the 30-second launch deadline, then the
launcher explicitly terminates its owned PID 3168 on cancellation. Only reset
was journaled, with no opened window. Read-only recovery finds no live session
or QA window; this is not evidence of an app crash. The documented lab Stop
also fails with `bridge_session_not_found` instead of recognizing completed
owned cleanup (#190). The original failed run and all logs stay unchanged; client
9272 exits 0 after one read. The late bootstrap cause is unknown and is not
conflated with #182's post-attach inspection or #189's elapsed guard. A second
fresh standalone attempt uses unchanged binaries; no deadline is increased.

That second run, `legacy-hit-standalone-02`, passes the same 22 N001-N014/reset
checks as Direct, including actual focus, CLI/MCP center agreement, no mismatch,
independent 2/2/1 counters at 259x0 DIP and reviewed native/marker pixels. Reset
and owned PID 8472 termination pass; client 16412 exits 0 after 11 calls with
empty stderr. The successful unchanged attempt does not explain or erase
standalone-01's startup/cleanup failure. #188 implementation is pushed as
`92c5f10` and remains in review pending the combined gate. #189 is next.

## Pointer diagnostics checkpoint (#186, combined gate pending)

The actual-Avalonia transformed-pad regression fails with the old bounds-order
path. Generation/geometry-pinned public picking now supplies the root-to-leaf
path independently of the nearest-node tree depth. Partial/unavailable evidence
and ambiguous cross-root order refuse assertions. Layer-local DIP coordinates
and render scale place screenshot markers correctly. All 19 affected Release
picking/Core/CLI/protocol cases pass (32 seconds), including the final two
missing-map/changed-primary regressions; the full Release build is clean.
The complete Release test run finishes with 838 passed, one failed and five
explicit native skips (35m05s). The failure is the unrelated launch timeout
guard (7.767 seconds versus less than four seconds, tracked as #189); no
launcher cause is established. The two final mapping cases were added after
that full run started and are validated in the separate 19-case focused run.

Fresh native `pointer-direct-01` uses fixture `5e5f159` and changed Core/CLI/MCP
with retained binary identities (source base `91819d1`, dirty). Win32 1x P004
now reports the correct complete pad path, replacing the window background.
It also exposes a genuine separate legacy input mismatch: `pointer_move`
dispatches to the centered `IsHitTestVisible=False` TextBlock. This is tracked
in #188, not removed from diagnostics. P006/P007 clear-area CLI/MCP positives
pass, including `maxDepth=1`; P008/P009 wrong-node/outside-pad assertions fail
as expected without changing the journal. P012/P013 native click/drag produce
two presses/releases and exactly one 80x30 DIP drag. P014 stale geometry refuses
with zero events. Public marker images and native pixels were reviewed.

P010 incorrectly supplied a pick revision to input's distinct activation guard;
it was safely refused, then corrected using `explain_action`. P015's default
depth did not find the menu; P017's expand provider was unsupported; an observed
native click opened it. P021-P023 observed a real popup and geometry, but it
closed before P024 input (`top_level_not_found`). That popup journey is not
counted passed and its cause is not inferred. Repeat with no simultaneous full
test run. Final reset/owned PID 12868 termination pass; persistent client 2964
exits 0 after 23 calls. The original popup failure remains retained.

Isolated native `pointer-standalone-01` S001-S021 passes 18 retained checks:
correct center path (with the separate #188 mismatch still present), depth-one
CLI clear-pad positive, MCP wrong-node negative, actual native click/CLI drag,
zero-event stale refusal and unchanged move-only journals. Independent app
counters and reviewed Windows pixels show two presses/releases and one 80x30
DIP drag. An explicit popup point (81,20) hits `qa-menu-apply`; the rendered
popup marker was reviewed. Mapping owner (125,244) to that popup yields (81,20)
and refuses ambiguous cross-root assertions. Owner-only cannot assert the popup
node. The outer failure includes the existing `pick_native_popup_present`
diagnostic; per-step diagnostics retain `runtime_pointer_hit_unverified` or
`runtime_pointer_expected_node_not_hit` respectively. Escape closes the popup
with zero menu actions. Final reset/owned PID 11868 termination pass; persistent
client 4444 exits 0 after 19 calls with empty stderr.

Fresh isolated `pointer-direct-02` D001-D013 independently passes eight checks:
CLI popup-local assertion/marker, MCP correctly mapped ambiguous-root refusal,
owner-only negative, unchanged journals and Escape without a menu action.
The actual Windows window and popup were reviewed alongside the marker image.
Final reset and owned PID 11992 termination pass; client 21184 exits 0 after
13 calls with empty stderr. Both integrations are Win32 1x, not native Retina
evidence. #186 is ready for combined validation; #188/#189 remain separately
open, and a fresh passing full gate is still required.

## Persistent UTF-8 client checkpoint (#187)

The test client now reads its stdin pipe as strict, BOM-less UTF-8 without
changing the console code page. Production MCP transport is unchanged. Invalid
bytes, malformed/truncated JSON and oversized lines exit with bounded errors;
existing command/session limits and file-request behavior remain.

The unchanged client (`9a1a8c6d55bed39a946342222d95c36767adf56beba754afe91002f43208c9a9`)
failed both compact/full-result raw-Unicode subprocess cases. Original native
K004/K005 corruption stays retained. Initial regression development also exposed
two test-authoring mistakes: failed-dispatch cleanup tried to join its own
headless worker, and an explicitly multiline string was sent to a single-line
TextBox. Cleanup now disposes off-worker and the fixture accepts returns. The
aborted baseline/stack and intermediate 6-pass/2-fail run are retained, not
classified as additional product defects. Final focused Release validation:
**8/8 passed, 2m28s**, including existing AgentRecipe cases, compact/full-result
raw accents/CJK/emoji, escaped multiline JSON, exact replay, file mode and four
invalid-input cases. Evidence: `expanded-campaign/persistent-utf8-final-results`.

Fresh native `artifacts/agent-qa/utf8-direct-01` uses unchanged fixture `5e5f159`
and rebuilt client `0b08f82ae5806b4b6cf9e47179881f8463a28a1a6b7911d7bb18b7dea53a3116`.
Node's console code page is **852 before and after**. U003/U004 use unmodified
raw UTF-8 pipe writes: each produces the exact desired accented/CJK/emoji text
and one independent journal increment. U005 exact replay leaves the entire
journal unchanged. U006 file mode preserves the combined Unicode string.
Reviewed native images and Windows accessibility agree; no Unicode-escape
workaround is installed. Five retained checks pass. Final reset restores seed
state, owned app PID 9664 terminates, and client PID 1032 exits 0 after six
persistent calls with empty stderr. This is Windows Win32 1x evidence only.

## Expanded fixture checkpoint

The capability map in [AGENT_QA_CAPABILITIES.md](AGENT_QA_CAPABILITIES.md) remains
a coverage plan. The shared form/table/input fixture, stable list keys and reset
guards compile for both host paths on Avalonia 12.1.3. At `8e36e2a`, all 12
fixture/virtual-item Release tests pass (30 seconds). Those tests do not replace
the native evidence below.

Actual Windows Direct and standalone exploration at `8e36e2a`, standalone
provider unchanged from `47034aa`, observed Win32 / 1120x800 DIP / 1x:

| Journey | Outcome and independent evidence |
| --- | --- |
| V001–V011: original virtual list | Passed: unrealized `QA-175` lookup, CLI reveal without selection, MCP select, missing/stale/incomplete-search refusal and CLI reset. Journal index 174/key `QA-175`, table selection unchanged; reviewed native/public images agree. |
| K001–K017: action map, text, focus, keyboard | Standalone passed. Direct persistent-client raw UTF-8 K004–K006 **failed, #187**; subsequent R004–R006 passed using standard JSON Unicode escapes. Other cases passed in both hosts: visible action discovery; desired text once/exact replay/CLI already-satisfied state; native Tab/Shift-Tab, CLI Enter once, CLI/MCP changed-focus refusal, Ctrl+A and `Árvíztűrő 😀 日本語`. Windows accessibility independently identifies the expected focused button/editor and final Unicode text; native images were reviewed. |
| D001–D005: pointer input | Passed: synthetic click has no drag; CLI native drag has two total presses/releases, one 80x30 DIP drag, and no held input. Stale geometry refuses with zero events and unchanged journal. Native image and counters agree. |
| D006/D007: pointer diagnostics | **Failed, #186**: both MCP center and CLI clear-pad point reconstruct a hit path ending at the full-window `PART_TransparencyFallback` instead of the actual pad. The CLI move metadata itself identifies the pad. Preserve both failures; no successful screenshot step followed the failed assertion. |
| Direct G001–G003: independent public pick | Passed: current public `pick_node` returns `qa-pointer-pad` as the actual input hit at 150,390 with a complete 16-node ancestry, whereas pointer diagnostics chooses the fallback border at the same point. Stale geometry refuses. This narrows #186 without changing its failed status. |

Evidence roots: `artifacts/agent-qa/keyed-standalone-01` and `keyed-direct-01`,
including exact V/K/D/G requests/results, CLI exits, independent state snapshots, `V006-native-selected.png`,
`K009-native-focus.json`/PNG, `K017-native-focus.json`/Unicode PNG and
`D006-native-state.png`. Native input here is the declared
`win32_owned_window_message` route, not global hardware injection or IME coverage.
Final resets restored seed text, counters, page and selection in both hosts;
owned PIDs 20960 and 21724 terminated. The Direct persistent MCP client completed
31 calls and exited 0. Raw UTF-8 stdin was decoded using Windows cp852 by this
test client; the file-request path preserved Unicode. `persistent-encoding-workaround.json`
and the subsequent ASCII JSON wire log explain the temporary workaround.
This is an infrastructure defect (#187), not a product desired-state replay bug.

Earlier CI `36072375163` on `b90ca39` passed Windows and all three native labs,
but failed Linux recovery (#184) and the macOS slider gesture (#185). It does not
validate subsequent #182/#183 implementation or the new keyed fixture.
#157/#161 still require real native Retina 2x evidence. The historical checkpoint
below describes earlier source-specific validation only.

## Runtime fixture checkpoint (#173)

`5e5f159` adds Runtime tab 7, shared custom drawing, bounded background work and
opt-in diagnostics. Direct additionally owns scene/action registrations. All 25
affected Release fixture/scene/operation regressions pass (67 seconds); both host
publishes have no warnings. The first regression caught premature registration
of detached tab content; registration now waits for actual visual attachment.

Fresh native Windows standalone evidence is in
`artifacts/agent-qa/runtime-standalone-01` (Win32, 1120x800 DIP, 1x). The run
started from the uncommitted fixture later committed as `5e5f159`; its binary
hashes are retained. The external provider remains the verified `47034aa` build.

| Journey | Outcome |
| --- | --- |
| S001–S015: scene discovery/pick/input | Ordinary drawn scene passed: exact canvas pick, shift, native orange/green selection, two selections in the independent journal, selected outline in reviewed native and public images. Scene declaration and custom actions are explicitly unsupported/disabled in this standalone host. S007/S009 lacked the target required by a geometry guard and S010 used the wrong argument shape; these agent-authored requests were rejected without events, then corrected using `explain_action` and the observed target. They are not product failures or successful dispatches. |
| W001–W009: ordinary background work | Passed: running to completed, deliberate failure, explicit cancellation, reset while active. Terminal counts agree with visible state; later observations still show idle/zero counts after reset, with no late completion. |
| D001–D011: intentional diagnostics | Passed current-state validation/clipping/removal: MCP and CLI inspect show the one expected validation error; `explain_layout` identifies the clipping ancestor. Native pixels show the error and clipped text. Binding metadata observes the runtime binding and fallback value; path/error/fallback-detail availability is explicitly unavailable through these public metadata APIs, so this is not full binding-error diagnostics coverage. Toggle-off removes the binding and current errors. `audit_ui` returns no issues in either state; it does not substitute for these targeted checks. D008 used an unsupported CLI request-file form and was corrected to the documented flags. |
| E001/E002: runtime assertions | CLI idle assertion passed; MCP assertion expecting Completed failed as expected, without changing the journal. |
| M001–M005: reversible mutation | MCP changes scene opacity 1 to 0.35; CLI resets it. Reviewed native/public pixels agree and each comparison reports 78000 changed pixels. Unsupported property mutation is refused and history reports zero active mutations. Inline trees are explicitly depth/byte truncated (27 nodes, target absent); their retained full-tree artifacts contain all 162 nodes including `qa-scene`. Do not report inline-tree coverage as complete. |

Final reset passed, owned PID 20092 terminated, and the persistent MCP client
21488 exited 0 after 39 calls. Standard JSON Unicode escapes remain an explicit
workaround for open test-client #187. No additional confirmed product defect was
found in this scoped standalone journey.

Fresh Direct evidence at clean `5e5f159` is in
`artifacts/agent-qa/runtime-direct-01`, also Win32 / 1120x800 DIP / 1x:

| Journey | Outcome |
| --- | --- |
| S001–S015 | Passed: declared discovery/three objects, MCP green selection, CLI orange selection after a 20 DIP shift, stale shift/reset token refusal, fresh generation after reset and native blue selection. Reviewed native outlines and journal counts agree. |
| W001–W013 | Passed: CLI action schema, MCP start, actual CLI running/progress=0.5 with journal progress=50, successful completion, CLI deliberate failure, CLI cancel and MCP terminal wait. Bad enum and out-of-range integer refuse without starting work. W013 exercises a wrong-session operation identity (`runtime_operation_session_mismatch`), not an unknown retained ID in the current session. |
| T001–T005 | Passed bridge/operation correlation: completed work is explicitly correlated to its request/operation, the deliberate failure is marked different correlation, CLI exports a sanitized trace and MCP stops it. Unknown trace is refused. Validation sampling reports partial coverage and misses this deeply nested fixture; binding/app-event/log adapters are unavailable. Those sources are not counted as validated. |
| F001–F009 | Passed declared cleanup and CLI prepare while work is active: retained operations finish cancelled, and the exact cleanup/prepared journals remain unchanged after those terminal observations. Prepare clears scene/diagnostics and restores idle/zero counters. |
| E001/E002, D001/D002 | MCP idle assertion passes, incorrect CLI assertion fails without changing state. Native intentional errors are visible; prepare removes them and public validation becomes clean. |
| M001–M003 | Passed MCP opacity change plus CLI reset, reviewed native/public images and 78000 changed pixels in each direction; zero active mutations afterward. The same explicit inline-tree budget applies as in standalone. |

The retained-evidence checks pass 13 assertions for standalone and 19 for Direct.
The Direct helper's first summary counter used an old JavaScript closure; its
zero-count artifact is retained and all checks were rerun with an explicit
session-owned counter. This is a reporting-helper correction, not a product
failure. Final Direct reset passed, owned PID 15012 terminated, and client 20920
exited 0 after 37 calls. No new product defect was confirmed in these scoped
Runtime journeys. #173 fixture implementation/available-surface validation is
complete; #174 comprehensive coverage, #184–#187 fixes, review gates and native
Retina acceptance remain open. `operation` supports status/wait/cancel by retained
ID; no list action exists and none is claimed tested.

## Earlier first-phase checkpoint (2026-09-24)

Latest checkpoint: infrastructure #162/#163 and defects #158/#159/#160/#167/#168/
#169/#170 are completed. #170 `678ac0b` passes 17 focused regressions in both
configurations, native V001–V004, and full Release (802 passed, five explicit
native skips). #157 `c8b7dc3` passes native opacity/text review and full Debug/Release
(801 passed each, five explicit native skips); every job of CI `36019901130`
passes, including all six hosted native lab combinations. Public preview/session/
reload/error-recovery exploration P001–P015 and fresh native direct F000–F036
pass. Combined CI `36025169889` attempt 1 on `2f3894f` found one text-edit IPC
timeout (#171); all six native lab combinations pass. The unchanged hosted
Windows rerun passes 802 tests plus five skips, as does the second complete
local Release run. Every attempt-2 downstream gate also passes. The
instrumented-source CI `36031948678` on `a6783cc` now passes every job. Windows
and macOS each pass 803 tests plus five explicit native skips; downloaded native
and package evidence also passes. #171 remains open/blocked because the original
failure's cause is not established and needs a reproducible failing environment
or captured recurrence. #157/#161 native 2× acceptance still needs an accessible
Retina environment. The ledger retains
historical intermediate results; its older pending statements are superseded by
the later dated/source-specific validations.

## Tested environment and infrastructure

### Requirement audit at `a6783cc` (2026-09-24)

This is an incomplete completion audit, not a declaration that AvaScope is
defect-free. Product code matches the explored `2f3894f`; subsequent changes
add test diagnostics, the deadline-recovery regression and documentation.

| Required outcome | Current evidence | Result / outstanding work |
| --- | --- | --- |
| Real agent environment, both integrations, reset/recovery and owned cleanup (#162/#163) | `eng/agent-qa.ps1`, shared QA scenes/journal, six documented charters; native Direct/Standalone transcripts and all three hosted lab backends | Implemented and validated; hosted checks remain identified as scripted. |
| Hands-on public CLI/MCP exploration with independent state and native pixels | Direct F000–F036, Standalone M100–M134/O002–O017/V001–V004 and earlier task charters; `campaign-final-direct/verified-summary.json` contains 19 passing evidence checks | Available Windows scope passed. This does not claim agent-operated Mac/Linux desktops or native Retina coverage. |
| Preview, variants, reload, failure recovery and session lifecycle | P001–P015: actual CLI/MCP, opened PNGs, repeated scale/theme/culture and XAML updates, failed-build recovery, one closed owned session | Passed; HTML viewer browser inspection remains blocked by the tool's local-file policy. |
| Original five reported runtime defects (#157–#161) | Completed #158/#159/#160; #157 mitigation `c8b7dc3` and #161 bounds correction `295939d` pass the available render/capture/native gates | Native macOS 2× evidence required by #157/#161 is still missing; both tickets remain open/blocked. Original missing customer payload/view details are not invented. |
| Newly found defects ticketed, corrected and regression-tested | #167 bounds, #168 duplicate logical identities, #169 mask overflow, #170 observe diagnostics; before/after tests and native reruns retained above | Completed. #171 remains open/blocked: the original hosted IPC deadline failure is retained and its cause is unknown. Controlled timeout recovery passes 12 focused checks and full hosted suites. |
| Fresh combined regression round on the final test source | Every job of unchanged-source CI `36025169889` attempt 2 and instrumented-source CI `36031948678` on `a6783cc` passes; full Windows/macOS each pass 803 plus five explicit native skips | Applicable round passes; all downloaded platform evidence is checked. Passing reruns do not explain the original failure or supply missing native Retina coverage. |
| Committed handoff, evidence and product boundaries | Changes pushed to `codex/agent-qa-phase-one`; F036 reports owned process termination; P015 reports the closed preview session; issue states retain external gaps | Product stays 1.5.0; no version bump, tag, release or publication. The campaign is not marked complete. |

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
| [#157](https://github.com/RolandUI/AvaScope/issues/157) | Repeated opacity/transform inflation reproduced on Avalonia 12.1.0 and 12.1.3, including bridge screenshots. | Blocked only on native macOS 2× acceptance: `c8b7dc3` mitigation passes focused/full local, native Win32 and every CI `36019901130` job. |
| [#158](https://github.com/RolandUI/AvaScope/issues/158) | Incomplete selector coverage was mislabeled as stale. Customer coverage metadata was absent. | Fixed `e32139f`, completed; complete targets, precise refusal and dispatch counts verified. |
| [#159](https://github.com/RolandUI/AvaScope/issues/159) | Expected constructor/domain failures became opaque MCP invocation errors. Original customer payload was absent. | Fixed `74c07e6`, completed; structured invalid arguments and supported insert/replay verified. |
| [#160](https://github.com/RolandUI/AvaScope/issues/160) | Exact AutomationID matching incorrectly ignored case. | Fixed `173b42b`, completed; independent counters and CLI/MCP parity verified. |
| [#161](https://github.com/RolandUI/AvaScope/issues/161) | 4 Mi-pixel cap rejected Full HD DIP at 2×. | Implemented `295939d`; headless 2× and native X11 4K pass. Native Retina acceptance remains blocked. |
| [#167](https://github.com/RolandUI/AvaScope/issues/167) | Structured visual queries returned bounds in the wrong coordinate space. | Fixed `cfa0d3d`, completed; nested/scroll/transform and geometry-pinned picking verified. |
| [#168](https://github.com/RolandUI/AvaScope/issues/168) | Repeated logical object identity made popup queries fail over IPC. | Fixed `d1028c3`, completed; both integrations on all three native backends verified. |
| [#169](https://github.com/RolandUI/AvaScope/issues/169) | Oversized privacy rectangle overflow skipped masking while reporting success. | Completed `5bbaad1`; native MCP/workflow/CLI, full Debug/Release and all jobs of CI `36014239916` pass. |
| [#170](https://github.com/RolandUI/AvaScope/issues/170) | observe omits numeric limits from its public schema and returns only a generic request error when exceeding them. | Completed `678ac0b`; 17 focused tests and native V001–V004 verify schema, actionable refusal, corrected/default success and owned cleanup. |
| [#171](https://github.com/RolandUI/AvaScope/issues/171) | One full hosted Windows run times out on the first Unicode range-edit IPC request; cause not established. | Active investigation; original failure retained, 15 fresh-process repetitions and unchanged full local/hosted Windows reruns pass. Instrumented-source gate pending. |

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

Native standalone follow-up `campaign-opacity-fixed` ran clean `c8b7dc3`,
Avalonia 12.1.3.0/Segoe UI, Win32 1×/1120×800, provider manifest
`451f3b08adad5b83e77a22a430376a3834bee7046764a942065420742a986c44`:

- O002–O007: selected Rendering through public MCP, scrolled with native input,
  and independently opened native/RTB frames. All five text rows agree in size,
  placement and spacing; overlap alpha, green clipping and partial viewport
  clipping also agree after accounting for native window chrome.
- O008b–O011: observation, rendered capture_screen and packaged CLI produce
  byte-identical PNGs to screenshot (`ec71ea28b2be429421111d6204395b73c73dfb9548ad39a94ee1c7e909fdccc8`).
  Observation reports its expected byte/depth truncation explicitly.
- O012–O014: theme/locale change to Dark/hu-HU and Settings → Rendering
  navigation preserve text sizes and opacity/clipping. Native and RTB images
  were both viewed.
- O015–O017: reset restores Settings, Light/en-US, Ready/Ada, zero counters,
  no child/popup, and unchanged geometry. Owned termination of PID 11680 is
  confirmed. An initial occluded-window capture did not show the selected QA
  content and was discarded; only visibly confirmed QA frames are retained.

O008 also exposed the separate #170 contract defect: a maxDepth=20 request
passes the published integer-only schema but is rejected without a field/range
diagnostic. Source inspection established 0..8; the corrected O008b succeeds.
This negative result is preserved separately from the passing screenshot cases.
Full local Release→Debug and CI `36019901130` now validate `c8b7dc3`; both are
still running at this checkpoint. No native Retina success is inferred.

### Observation contract and recovery (#170)

Standard data-annotation Range metadata publishes minimum/maximum for
maxTopLevels (1..8), maxNodes (1..256), maxDepth (0..8), maxInlineBytes
(4096..131072) and timeoutMs (1..5000). The MCP pre-binding filter maps only
these trusted DTO annotations to field/minimum/maximum diagnostics. It does
not echo raw exception messages, JSON paths or caller values. Constructor
range violations, oversized integers, wrong types and nulls receive the same
bounded recovery information; unrelated validation behavior stays intact.

Actual MCP stdio tools/list failed its minimum-bound assertion before the fix.
Afterward, 17 observation/text-edit/MCP tests pass. The new case checks every
numeric bound, 25 invalid requests through an absent manifest directory,
boundary-valid/default calls against a real bridge, corrected retries,
unchanged application text and absence of a sensitive canary from replies and
stderr. A valid 1 ms timeout may legitimately expire at runtime; the test
distinguishes that from argument rejection.

Clean `campaign-observe-bounds` at `678ac0b` uses the unchanged verified
`c8b7dc3` standalone host/provider and updated MCP/CLI. Actual schema includes
minimum=0/maximum=8. V001 rejects maxDepth=20 with the field and exact range;
V002 at 8 returns a screenshot and explicitly partial node coverage; V003
defaults succeed. The journal remains Ada, zero edits/toggles, native Win32
1×, Avalonia 12.1.3.0. V004 confirms owned PID 47256 termination. #170 is closed.

### Actual preview lifecycle and recovery exploration

`preview-exploration-01` uses the `678ac0b` MCP/CLI snapshot with a synthetic
net10.0/Avalonia 12.1.3 project and a separate preview metadata store. The test
client now forwards `AVASCOPE_PREVIEW_SESSION_STORE` to its sanitized MCP child;
the persisted record was verified in that exact owned directory. No default
or unrelated preview sessions are involved. This one-line infrastructure change
is validated by the public calls below, rather than a test mirroring an env list.

- P001–P003: create a durable preview record, render Light/en-US at 1× and 2×,
  then Dark/hu-HU at 2×. Viewed PNGs have the requested 680×640 / 1360×1280
  dimensions, correct number formatting, Unicode text and nested template.
  Five identical transformed/full-opacity text rows remain equally sized in
  both themes: independent pixel measurements find five 26-pixel ink bands at
  2×, 90 pixels apart. This is headless preview evidence, not native Retina.
- P004: edit the owned XAML from version A to B and reload the same session;
  the new content is visible. The initial image is preserved separately.
- P005–P007: mismatched closing XAML tag returns `preview_project_build_failed`
  in `lastRender`, marks the durable session failed and survives a new MCP
  process/list call. Repairing it to version C returns the same session to
  active and renders the corrected content. The outer reload result describes
  a successfully updated session record; its nested render result correctly
  reports the deliberate build failure.
- P008: viewer HTML export succeeds. Browser review is **blocked** by the
  browser tool's local `file://` URL policy; no alternate surface or URL
  workaround was attempted. PNG review is completed independently, and does
  not establish the HTML viewer's browser appearance.
- P009–P010: packaged CLI preview is byte-identical to the MCP version-C PNG;
  CLI listing sees exactly the owned active record.
- P011: a bounded CLI watch observes the actual C → D file edit, coalesces the
  two filesystem events into one successful reload and exits at maxReloads=1
  without timing out. The version-D PNG and complete watch result are retained.
- P012–P015: close the owned record, reject reload with `session_closed`,
  repeat close successfully, and verify only the closed record remains.

The selected operations, raw responses, expected failure and recovered images
are retained under `artifacts/agent-qa/preview-exploration-01`. No new product
defect was found in this charter. Local full Release at `678ac0b` passes 802
tests with five explicit native-only skips; focused observation/text/MCP Debug
also passes 17/17. The broader native/packaging CI is tracked separately.

### Fresh native direct regression round

Clean `2f3894f` was built/published by the lab for `campaign-final-direct`:
direct integration, Avalonia 12.1.3.0, Segoe UI, native Win32 1×/1120×800.
Session `0ce36ae935e24b32b8259facc689bc6b` owns PID 10780. Public MCP calls,
before/after application journals and independently viewed native images are
retained separately. F000–F036 found no new product defect:

- F001–F008: complete selectors report correct top-level visual coordinates;
  desired checked state dispatches once and repetition dispatches zero times.
  Malformed insert is rejected before dispatch; valid Unicode insertion and
  exact replay leave `Ada Á😀` and exactly one actual edit in the app journal.
- F009–F014: native and rendered Dark/hu-HU nested templates, local transforms,
  reference fonts, five equal opacity rows, group alpha and clipping agree
  after excluding window chrome and the native cursor overlay. Screenshot and
  observe PNGs are byte-identical. Invalid depth identifies 0..8; corrected
  observation succeeds with explicit partial coverage. The freshly published
  schema includes all five numeric limits.
- F015–F017: all 894,081 intended pixels of the int.MaxValue privacy mask are
  black. Removing masking restores exactly the preceding PNG bytes. Case-
  distinct buttons each increment their own counter once.
- F018–F027: replacing the editor rejects its old target; fresh selection reads
  the preserved Unicode name. Logical popup lookup returns one complete match,
  and native popup text is readable. The first close omitted allowDestructive:
  its guard rejected the operation with unchanged popup/child state. The
  explicitly authorized retry closes the owned popup and opens one child;
  list_top_levels reports that child as active and its owner inactive. This
  retained correction is an expected guard outcome, not an additional defect.
- F028–F035: reset closes the child; load immediately followed by reset settles
  to Ready. A real modal blocks owner input with zero dispatched events; its
  owned close succeeds. Final reset yields Ada, Light/en-US, all counters zero,
  no popup/child, one top-level, unchanged geometry and independently viewed
  native/rendered Settings images.
- F036: owned termination confirms PID 10780 exited. Nineteen cross-checks of
  the retained responses, independent before/after journals, image hashes,
  mask pixel count and process ownership all pass in `verified-summary.json`.

The rendering baseline `c8b7dc3` completed every job of CI `36019901130`.
Downloaded `opacity-hosted-{windows,linux,macos}` evidence confirms native
input/dialog/capture checks. X11 paired CLI/MCP capture compares all 8,294,400
pixels at actual 3840×2160; hosted macOS remains actual 1×. The later observation
contract/client changes are covered locally and by ongoing CI `36025169889`.
Native Retina and the browser-blocked HTML viewer remain explicit coverage gaps.

### Intermittent hosted text-edit timeout investigation (#171)

CI `36025169889` attempt 1 failed
`MultilineUnicodeRangeSelectionInsertionAndUndoPreserveSurroundingText` on its
first `replace_range` IPC request (line 108 on `2f3894f`). The response is
`bridge_ipc_unavailable`, `dispatched=unknown`, with exact request-id recovery
guidance; the test took 12 seconds. The full suite recorded 801 passed, one
failed and five explicit native skips. The original completed-job log is
retained as `artifacts/agent-qa/final-ci-windows-test.log`. All six native lab
combinations and their independent journal checks passed on the same source;
downloaded artifacts are under `final-hosted-lab/{windows,linux,macos}`.

Fifteen unchanged fresh-process focused repetitions pass (1.66–1.92 seconds
for the whole test). A second unchanged complete local Release run also passes
802 tests plus five skips in 11m14s; that text-edit case takes 0.208 seconds in
the full-suite context. The unchanged hosted attempt 2 passes the full Windows
suite (802 plus five skips, 9m26s) and Windows packaging; downstream Linux/macOS
gates continue. These results do not
establish why the original operation exceeded its five-second IPC deadline.
No production deadline/dispatcher change or automatic uncertain-edit retry is
supported by this evidence.

The failed test now retains elapsed time, configured timeout and a bounded
snapshot of focus/caret/selection plus whether initial/expected text was
observed. Windows CI also emits and always uploads full TRX results, retaining
the test timeline that was missing from attempt 1. A temporary, deliberately
one-tick test-client timeout verified the negative diagnostic: 2.96 ms elapsed,
initial text unchanged, expected replacement absent, focus false and the
uncertain IPC error retained. That injected timeout was restored immediately;
it is neither the committed test nor a reproduction of the hosted cause.
Evidence is in `timeout-diagnostic-negative.log` and its TRX. After restoring
the five-second path, all 17 text/observation/MCP regressions pass with no build
warnings/errors (`timeout-diagnostics-positive.log`). The issue remains open;
the original timeout is not claimed fixed by a passing rerun.

An isolated diagnostic probe also measures actual IPC reads and edits on
Avalonia 12.1.3 with the unchanged five-second deadline. Forty operations cover
30, 542, 4,126 and 8,030 UTF-16 text units (ten each); every edit is verified,
matches the independent control text and dispatches once. The largest measured
read/edit times are 188.6/86.2 ms. Evidence is in
`artifacts/agent-qa/text-ipc-probe/evidence-cleanup-fixed`. A first probe had
completed its measurements but hung in the console harness's headless-session
disposal; its exact owned process was stopped. Moving that one-off probe's
disposal off the completing UI thread yields normal exit and zero remaining
owned processes. This is diagnostic harness evidence, not a product change or
proof of the original hosted cause. The successful measurements do not support
a speculative product timeout increase.

The timeout boundary now has a deterministic regression:
`IpcTimeoutDuringDispatchedCallbackPreservesOneEditAndItsRecoverableOutcome`
holds the actual application's TextInput callback until the unchanged default
five-second IPC deadline. It verifies the uncertain response's original request
id, releases the callback, independently reads the actual changed text and
retrieves the retained outcome with the same payload/id. The edit and input
callback occur exactly once. All 12 text-edit tests pass in Release, including
this case (`timeout-boundary.log`, `timeout-boundary-results/timeout-boundary.trx`).
This demonstrates safe recovery under a controlled timeout, not the cause of
the earlier hosted timeout; production behavior is unchanged.

CI `36025169889` attempt 2 then completes successfully on unchanged `2f3894f`.
Windows and macOS each pass 802 tests plus five explicit native skips; all six
native lab combinations and the Linux/macOS package gates pass. Downloaded
`final-hosted-linux` and `final-hosted-macos` evidence confirms 20 input/dialog
checks per platform and five native test cases each. Both X11 full-resolution
CLI/MCP samples capture 3840×2160 and compare all 8,294,400 pixels. Hosted
macOS captures remain actual 1×. Full CI `36031948678` was subsequently started
on `a6783cc` to validate the new diagnostic retention and timeout-boundary test;
the earlier passing run does not validate those test-only changes.

On `a6783cc`, CI `36031948678` has now passed the full Windows Test step,
source and packaged CLI/MCP workflows, the Windows installer test, and all
three native lab jobs. Downloaded `instrumented-hosted-lab/{windows,linux,macos}`
artifacts independently match the selected clean source. All six Direct/
Standalone journals contain one-toggle and one-edit states, isolated
case-distinct action counters and successful owned termination; both lifecycle
cycles and lease expiry pass. The compact cross-check is retained as
`instrumented-hosted-lab/verified-all-platforms.json`. All observed scales are
1; Mac's requested Full HD window is clamped to 1920×649 DIP. The entire Windows
job now passes, including provider/artifact verification. Its downloaded TRX
contains 803 passed and five explicit native `NotExecuted` results (808 total),
matching the console log; the adapter's summary `notExecuted` counter is zero,
so skip counts are derived from the individual outcomes. The formerly failing
Unicode case passes in 0.636 seconds; the deterministic deadline-recovery case
passes in 5.054 seconds. This verifies diagnostic artifact retention and the new
test in the full hosted context. Evidence is in `instrumented-hosted-windows`,
including `verified-trx-summary.json`, and `instrumented-ci-windows.log`.
Linux/macOS downstream gates subsequently pass as well: the combined run is
successful. Full macOS reports 803 passed plus five native skips, zero build
warnings/errors. Downloaded `instrumented-hosted-linux` and
`instrumented-hosted-macos` each confirm 20 input/dialog checks and five native
test cases; both X11 3840×2160 CLI/MCP pairs compare all 8,294,400 pixels.
Mac's native captures remain actual 1×. Compact checks are retained in each
artifact directory's `verified-summary.json`; the macOS job log is
`instrumented-ci-macos.log`. The original timeout cause is still unknown.

### Blocked handoff after completed gates

The named defect acceptances prevent claiming the entire goal complete.
#157/#161 require an accessible logged-in Mac with actual 2× rendering, room for
the 1920×1080 DIP client and independent native capture. That same missing
environment has persisted across multiple consecutive goal turns; the request
for a suitable host remains unanswered. Continue with the exact procedure in
[AGENT_QA_LAB.md](AGENT_QA_LAB.md) when access exists.

#171 retains its original failure separately from the passing reruns. The
original log lacks per-operation timing and observed control state, and the
unchanged failure does not reproduce in the completed focused/full checks.
Further causal work needs a reproducible failing environment or a captured
recurrence with the now-retained diagnostics. The diagnostic/recovery changes
are validated; no unsupported product correction or timeout increase is made.
The ticket remains open/blocked rather than claiming its cause fixed.

#166, #157, #161 and #171 remain open/blocked. No test process or CI job remains
running; all owned QA/preview cleanup is recorded. Commits are pushed, the
working tree is clean at handoff, and only documentation follows tested
`a6783cc`. HTML viewer browser review remains a separate tool-policy coverage
gap; PNGs were viewed independently. Further repetition of unchanged passing
suites does not provide the missing acceptance evidence. No release is made.
