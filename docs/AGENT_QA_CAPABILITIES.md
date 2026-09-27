# Agent QA capability map

Current checkpoint: the shared d7562ed full CI is successful; #228/#229/#230 are
closed. Source remains unchanged through documentation commit 731af68. The #231
fresh-intermediate Direct publish completes in 251.372s under the original 300s
deadline, then five actual CLI/MCP calls verify Win32 startup, tree, a reviewed
1120x800 rendered image and owned shutdown. This does not establish the original
publish-timeout cause or an independent native-pixel/Retina pass. The lab now
reuses one binary set and build cache; historical local artifacts are discarded.
`docs/DEVELOPMENT_PLAN.md` and GitHub carry current issue state. The scoped
checkpoints below are historical observations, not still-pending batch statuses.

After that gate, #201's timing-provenance slice changes only PreviewHost's
animation diagnostic and the MCP tool description. Six focused regressions pass;
the two real moving-animation cases fail before the correction. Actual CLI/MCP
requests for 0/500/500ms carry the warning on all six frames, including cached
duplicates. Two strips were reviewed: independent red widths are CLI85/78/78 and
MCP71/67/67 versus the animation's declared20/70/70. This directly confirms that
deterministic timing is still broken; #201 stays open. Both callers exit0 and no
selected tool/PreviewHost process remains; MCP server shutdown exits-1 at its
existing transport bound. Only four shared DLL/PDB outputs were replaced in
place, without a new binary snapshot. Full CI for this source change is pending
the next combined batch; the prior green gate does not validate this new slice.

The reused af6e2f3 standalone/provider set now verifies active-highlight input
through a warm persistent MCP connection: synthetic and Win32 owned-window
message clicks complete their show/inspect/click/inspect sequences in 516ms and
734ms. Each preserves the same active highlight id around input and increments
the independent app toggle count once (false/true/false). Clear, expiry and
screenshot removal pass. Two reviewed 1120x800 images differ only in 37 pixels of
the bottom event counter; the main content is unchanged. There are 23 successful
MCP calls plus successful CLI attach, after the original CLI launch exceeded its
30s readiness deadline. The same late app was attached, never restarted. Public
close is close-only because unsuccessful launch recorded no ownership. Explicit
owned termination initially exceeded the observer's 10s exit wait; later read-only
checks find app/client/server absent. MCP transport shutdown reports server exit
-1 after its existing five-second budget, not a natural server exit. These limits
prevent a blanket clean-startup/cleanup claim. Independent native pixels remain
unavailable; this closes the scoped input-transparency gap through the product's
two declared input routes, not global hardware-input coverage.

## Historical scoped checkpoints

At `af6e2f3`, held-work charters add 42 public calls across native Direct and
standalone: actual cancellation after separate observations, explicit continuation
and reset isolation are verified by operation results, independent journals and
eight reviewed rendered images. Direct also verifies the previously missing
fresh-sample wrong-node pointer assertion. The 43 local fixture regressions pass.
Both apps are terminated, 47 process observations are absent and manifests empty.
Direct uses a successful resumed publish after the original 300-second fresh
publish timeout (#231); that original failure remains unexplained. Standalone
fresh publish and its no-AvaScope-assembly boundary pass. Evidence is retained in
`held-work-direct-230-02` and `held-work-standalone-230-01`. The updated explicit
call index has 2163 records/2159 responses across 112 roots (75 MCP/70 CLI names).
This is not a complete current-source capability pass; the shared CI, remaining
CLI lifecycle/preview boundaries and native OS/Retina gaps remain.

The clean `9c24651` Direct charter adds 51 public calls (49 CLI/two MCP),
510 unchanged binary pins, nine expected negative results, 27 correlated trace
events and five reviewed rendered images. Scene/stale-object behavior, successful
and deliberately failed work, bounded trace export, geometry/picking, sampled
pointer assertions and selection of offscreen `QA-175` agree with the independent
journal. The rendered capture matches the reviewed scene image byte for byte.
The owned app is terminated; 54 process observations are accounted for, including
one subsequent pwsh PID reuse; private manifests are empty.

Two coverage limits remain explicit: cancellation arrived after the short work
completed (zero cancellations), now tracked as the held-work fixture feature #230;
and pointer case43 omitted a move in its request, so its `hit_unverified` result
is not a wrong-node mismatch pass. Native desktop scope was refused and no OS
pixels were exposed. Evidence: `cli-runtime-direct-01/verified-charter.json`.
This charter does not close the shared validation batch or justify a Retina pass.

At `db511f9`, `cli-adapters-standalone-01` adds 29 public calls (24 CLI/five MCP)
with 540 unchanged binary pins, three reviewed rendered images and independent
fixture state. Attach, capabilities, private run listing, bounded/full-artifact
trees, Unicode state/replay/conflict/stale-edit guards, expressions, explanation
and text restoration agree with their scoped oracles. The original 24-level
expression request correctly returns incomplete coverage; a separate 32-level
request passes, while a false assertion fails. All originals are retained.
Owned app 13640 and all 35 recorded process observations are absent; manifests
are empty. Focus remains after text restoration, so no exact image reset is
claimed. Independent OS capture remains unavailable. The updated inventory has
2070 records/2066 responses across 109 roots and names all 75 MCP/63 CLI commands;
this is not a claim that all capabilities passed. Remaining CLI boundaries and
the shared validation batch are still open. Evidence: `public-call-inventory-db511f9.json`
and `cli-adapters-standalone-01/verified-charter.json` under `artifacts/agent-qa`.

The `8968aad` reconciliation preserves the older inventories and indexes 2041
explicit request/argv records across 108 evidence roots; 2037 have retained
responses, four historical health attempts do not. There are no duplicate
response identities. That index covers 74 of 75 MCP names and 55 of 78 CLI names;
forty older raw CLI payloads remain unclassified. It now includes the external
integration-startup charters, native pickers and recorded scenario process argv
that the qa-run-only scan omitted. Missing names are index gaps, not proof that
the feature was never exercised; neither a response nor a success flag is an
independent passing oracle. Artifacts: expanded-campaign/public-call-inventory-
8968aad-supplemented.json and its summary/audit. The explicit attach/CLI comparison
above extends that checkpoint. Existing
Retina/native observation limitations remain unchanged.

`artifact-boundaries-01` on707e891 adds45 actual public calls (18MCP/27CLI),
known64-pixel diff/crop controls, invalid inputs, full75/78 capability catalog
comparison, private cleanup/sentinels, preview session/export/close and actual
run-index resolution. Seven image files were opened; HTML source and embedded
image bytes are verified but browser appearance remains unavailable. All63
recorded caller/server observations are absent; original inputs and pinned
204 tool/runtime plus70 sample/project files remain unchanged. PreviewHost child
PIDs were not independently retained. One missing-selector invocation is
preserved and separately corrected. The charter discovers228: identical PNGs
with TextBlock Margin versus equivalent Canvas placement have different clipping
warnings because DesiredSize includes Margin. Both adapters reproduce it; this
is a campaign finding, not a clean pass. Source/binary provenance is retained.

Inventory: 75 MCP tools from `src/AvaScope.Mcp/AvaScopeMcpTools.cs` on the
#172 expansion branch; CLI commands from `src/AvaScope.Cli/Program.cs`.
Execution tracker: #174, umbrella #166. This is a coverage plan, **not a pass
report**. The expanded campaign must attach an exact source/artifact identity,
request/response, independent oracle and outcome to every applicable journey.
Historical passing tests do not validate newly added scenes.

Source/schema reconciliation on `1335a4c` confirms 75 MCP tools and 78 CLI
commands (plus version flags). The retained `expanded-campaign/public-call-inventory.json`
indexes 1657 explicit calls in 84 native/preview runs, with request/response
hashes, integration/source and available oracle records. It finds 56 MCP tool
names and 32 CLI command names; forty raw CLI payloads lack explicit argv and
remain unclassified. Startup and scripted CI calls are excluded. Invocation
counts and transport success are not coverage passes. Older persistent-client
wrappers are included; the original filename-only scan missed them.

The later retained scan covers 1796 explicit calls in 90 roots (60 MCP tool and
36 CLI command names, forty raw CLI payloads still unclassified). It excludes
startup and newer snapshot-reuse wrappers, so its missing-name list is not an
untested-capability list. Logical-tree, observation changes and highlight
lifecycle now have Direct/standalone evidence; the standalone active-overlay
click remains unproved because actual input followed expiry.

`native-picker-standalone-02` separately adds 24 explicit calls (18 MCP/six CLI),
113 independent checks and twelve viewed native images with owned cleanup.
Open/cancel/one-shot/expiry controls pass; changed Save filename fails and is
tracked in #219. Integration/startup, runtime diagnostics and artifact/viewer CLI
boundaries still need reconciliation or fresh exploration. The matrix below
remains the scope and oracle plan; only recorded journeys support scoped results.

`integration-diagnostics-01` on clean 4ffd1a2 adds 15 real public calls (nine
MCP/six CLI) and 199 independent checks. Provider identity/pin refusal, Windows
and Linux profile previews, selected/missing-target doctor and supported guide
controls pass. Four observations reproduce #221's hidden target review and
#222's declaration-as-activation error. Five calls in `integration-review-after-01`
verify the #221 correction through both adapters and retain #222 as known open.
The subsequent six CLI/MCP calls in `integration-declaration-after-01` verify
#222's correction against the same declaration-only source, preserve both real
standalone invocation locations and keep #221's required-review behavior.
These are static/diagnostic checks with owned platform probes and unchanged
source/provider files; no native app launch or UI journey is claimed. Actual
integration startup, runtime diagnostics and artifact/viewer boundaries still
need their remaining scoped journeys.

Actual integration startup now has two retained eleven-call charters:
`integration-startup-174-01` uses066192b (947 checks,399 hashes) and retains
#182's first-tree timeout and #225's misclassified cleanup failure. The
`integration-startup-225-01` comparison uses995046f verifier tools with those
unchanged pinned hosts/provider/client (944 checks): disabled CLI/MCP cleanup,
early exit and all guards pass; Direct MCP instead times out at screenshot IPC
after successful tree/inspection, retained separately under182. Seven native
and three rendered images are reviewed; all25 per-case owned process
observations are absent. Three enabled routes pass; neither charter is a blanket
pass. Runtime diagnostics and artifact/viewer boundaries remain pending.

`runtime-diagnostics-direct-01` on clean0b2a7cf now adds35 explicit public calls
(18MCP/17CLI),627 independent checks and510 unchanged pinned binary files.
Six native JPEGs and four rendered PNGs were opened and reviewed. Public and
native toggle cycles expose the intentional validation error and800-DIP child
inside a280-DIP clipping parent; targeted inspection/layout/audit agree with
the app journal. Disabling and Reset restore the exact diagnostic image region
(6405 changed pixels while enabled, zero after restoration). Missing-node/scope
guards preserve the journal. One omitted manifest argument and one explicitly
depth-limited empty discovery are retained as agent invocation/coverage limits.
Binding expression/fallback text are observed, but binding-path/error metadata
remain unavailable. Connection-health `diagnostics` and single-node design
rules do not replace targeted validation/clipping checks. Unrelated accessibility
findings and bounded inventory truncation remain explicit. The app expired and
closed during interruption; subsequent owned Stop and independent process/
manifest inspection verify cleanup.

`runtime-diagnostics-standalone-01` completes29 explicit calls (eightMCP/21CLI),
672 checks,540 pinned-file hashes and35 provider file hashes. The pure host and
current provider preserve the same validation/clipping/fallback and exact
diagnostic-region restoration across enable/disable/Reset. Missing-node/scope
guards leave the journal unchanged; all four rendered images were reviewed.
Owned Stop terminates the app; private manifests are empty and30 original
app/caller identities are absent (one PID reused by a later different process
is distinguished by creation time). Independent native capture failed recovery
and remains unavailable, so this is a scoped API/journal/rendered comparison,
not a completed independent native visual pass. #227 artifact retention is next.

Full4b47791 and0b2a7cf gates both pass; #221–#225 close after downloaded
cross-platform verification. The latest gate has Windows1134/eight skips,
macOS1131/ten skips, Linux 33 and nine native/expiry1x runs. #227 records missing
Wayland PNGs/prerequisite guard evidence in the full Linux artifact despite
successful native responses; those missing images have not been reviewed.
Targeted Wayland evidence below remains a separate source, not a substitute.

Targeted managed-Wayland CI36253239362 onf309708 additionally verifies four
positive native CLI/MCP 1x/2x journeys and four negative outcomes with retained
responses, supported rendered-capture provenance,500x460/1000x920 images and
owned cleanup. Seven hosted readiness tests distinguish queued dispatch from
pending composition. The two distinct image contents were viewed and hashes
match the successful run's images. This does not reproduce226's original
1000ms delay or supply independent OS screenshots/native Retina evidence.

| MCP tools | Fixture / positive and negative journey | Independent oracle / constraint |
| --- | --- | --- |
| `health`, `capabilities` | Isolated current stdio server; compare advertised versions and capabilities with built artifacts. | Assembly identity and tool schema inventory. |
| `list_sessions`, `attach_to_app`, `session_capabilities`, `list_top_levels` | Explicit Direct/standalone launch; correct, absent and ambiguous session selection. | Owned PID, journal backend/scale, actual windows. |
| `launch_app`, `close_session`, `cleanup_bridge_sessions` | Start, retain, close, expire and recover only the selected owned fixture. | Process identity/termination; preserve unrelated sessions. |
| `doctor_target`, `resolve_test_profile`, `verify_integration`, `integration_guide`, `verify_provider` | QA profile and external provider distribution; incompatible/missing target/provider cases. | Built deps, provider manifest/hash and actual launch outcome. |
| `visual_tree`, `logical_tree`, `find_nodes`, `inspect_node` | All tabs; exact identities, labels, relationships, replacement, limits and hidden/virtualized content. | UI/journal, independent native accessibility where available; incomplete coverage stays explicit. |
| `observe`, `observe_changes` | Settings/form/table changes, window lifecycle; response limits and invalid bounds. | Journal deltas, unchanged/repeated observations, actual captures. |
| `explain_layout`, `audit_ui`, `design_quality_audit`, `diagnostics` | Rendering/forms; clean and intentionally problematic diagnostic scene (#173). | Visible layout and deliberately introduced finding; no unexplained default-scene findings. |
| `pick_node`, `highlight`, `audit_native_accessibility` | Nested rendering card, input pad, form; stale geometry and highlight cleanup. | Independent OS window pixels/accessibility, after-clear image. Platform support must be observed. |
| `screenshot`, `capture_screen`, `assert_region` | Light/dark, locale, nested opacity/transform, table scroll, privacy masks, dimension limits. | App geometry and independently captured pixels; native screen route requires declared desktop. Real Retina remains #157/#161. |
| `input`, `edit_text`, `ensure_state`, `explain_action` | Settings/form/input: text ranges, Unicode, selection, toggles/range, menu, click/drag/key; disabled/read-only/stale target and exact replay. | Actual values and event counts; rejected requests must not mutate state. |
| `inspect_focus`, `probe_focus`, `action_map` | Form, keyboard editor/next button, menu, modal window. | Visible focus, app key/action counts; refused modal-owner dispatch. |
| `inspect_form`, `fill_form` | Labeled validated profile; redaction, choice realization, partial inventory, invalid plans, repeated fill, submit separately. | Current fields versus saved profile, validation errors, save count; never journal password contents. |
| `query_table`, `table_action`, `virtual_item` | Keyed 200-row DataGrid plus original list; offscreen key, edit/select/sort, stale row/column/revision, read-only column. | Seed data versus actual view order, selection key and edit count. |
| `window`, `navigation` | Tabs, popup, child and modal; discover/activate/resize/close guards. Navigation tab (#212): synthetic document overview/details, context/revision/reset, retained routes/loops and privacy guards. | Window count, observed geometry, selected tab/focus and independent document journal. Direct declares scoped identity; standalone retains explicit uncertainty. Requested size is not observed size. |
| `native_picker` | Existing standalone native-input fixture: open/save/cancel and host-prepared synthetic result. | Selected synthetic path/cancellation in the app; native dialog platform restrictions and ownership are explicit. |
| `scene` | Declared rendered objects and revisions (#173); query/pick/select, stale or absent declaration. | App-owned scene state and custom-rendered pixels. Direct registration only unless standalone explicitly supports it. |
| `custom_actions`, `invoke_custom_action`, `operation`, `trace`, `evaluate_runtime` | Fixture reset plus declared action/progress/cancel/failure and correlated assertions (#173). | Independent app operation state, action counts and terminal result; trace alone is not proof of success. |
| `pointer_diagnostics`, `pseudo_state_matrix`, `record_interaction_animation` | Input pad, template button, form focus/hover/pressed; bounded recording and cleanup. | Native pointer behavior, per-frame images, final focus/pseudo-state reset. |
| `mutate_node`, `mutate_node_evidence`, `mutation_review` | Rendering text/spacing/color/size and finite brush timelines with Style/Local target/reference pairs; unsupported animated writes, history, reset during animation and before/after evidence. | Pixels, actual sampled brushes/priorities, untouched references, review artifact and unchanged source files. |
| `run_workflow`, `run_scenario`, `export_workflow`, `replay_workflow` | Discover a real form/table task, execute, export, validate/replay after reset; deliberate blocker and failure cleanup. | Saved profile/table state, run artifacts, event counts, owned launch cleanup. |
| `session_control`, `list_agent_runs`, `recover_run` | Explicit lease ownership/contention, retained and interrupted run; unsafe recovery refusal. | Journal and exact run/lease identity; no duplicate action or unrelated process termination. |
| `preview_axaml`, `preview_axaml_multi`, `preview_axaml_animation` | GettingStarted views/design data/animation plus isolated synthetic XAML; size/theme/DPI/culture combinations and invalid XAML. | Rendered variants, actual frame timing/content and diagnostics. These need a project, not only a running QA window. |
| `create_preview_session`, `list_preview_sessions`, `reload`, `close_preview_session`, `preview_viewer` | Isolated preview store; edit/reload/fail/repair/close, viewer export. | New pixels and persisted session status; local HTML viewer visual inspection remains blocked by browser file-URL policy. |
| `baseline_check`, `semantic_diff`, `cleanup` | Isolated baseline/artifacts, known equal/different images and semantic tree change; bounded owned cleanup. | Expected pixel/tree delta, retained report and unrelated artifact preservation; no automatic baseline acceptance. |

CLI-only journeys additionally cover `baseline-create`, `diff`, `latest-run`,
`watch-preview-session` and `reload-preview-session`. CLI `preview`,
`preview-animation`, `attach`, `doctor`, `design-audit` and `mcp` have different
names from their MCP counterparts. Help/version/error exits and argument/file
handling also need CLI evidence. For a family exposed on both transports, execute
at least one representative positive and negative case on each; do not infer CLI
correctness solely from the equivalent MCP result.

Each recorded case needs: tool/operation, scene, integration/backend/actual scale,
source/binary identity, initial state, intent, exact calls, expected/actual state,
reviewed image/artifact, passed/failed/blocked/unsupported status, recovery and
linked defect. A transport success with failed postconditions is a failed case.
Broad tool coverage is not exhaustive coverage of every parameter combination;
the campaign should vary boundaries and state transitions as well.

## Recorded expanded journeys

These entries link scoped evidence, not completion of the entire inventory.
Exact calls, journals, images, limitations and cleanup are described in
[the campaign report](AGENT_QA_PHASE_ONE_REPORT.md).

| Family | Native Direct | Native standalone | Remaining boundary |
| --- | --- | --- | --- |
| Logical inspection, observation changes and highlighting | `inspection-direct-01`, 7c0b33c: 42 calls/250 checks, eleven native/two rendered viewed images and two actual Windows clicks. | Historical `inspection-standalone-01`, b377fb5: 46 calls/276 checks, sixteen native/two rendered viewed images and eight actual clicks. Reused af6e2f3 adds 23 persistent MCP calls and two reviewed rendered images. | Full/shallow/missing trees, Unicode and ordered paged deltas, unchanged/resync/expiry/privacy, visible highlights and clear/screenshot cleanup verify in their recorded scopes. Earlier standalone clicks missed the five-second lifetime. The fresh persistent journey proves synthetic/Win32-message input while the same highlight remains active, with one independent toggle per click. Initial launch/termination-observer timeouts remain explicit above. Scoped Win32 1x; no new independent native-pixel or hardware-input claim. |
| Sensitive image coverage (#217) | `deep-mask-direct-after-01`, 69244a1: 16 calls/106 checks; `deep-mask-scenario-direct-04`: CLI profile, one call/59 checks. | `deep-mask-standalone-after-01`: 15 calls/101 checks; `deep-mask-scenario-standalone-03`: stdio MCP scenario, one call/59 checks. | Exact selective child-tree masks and all outside pixels verify; incomplete native main-tree/owned fallback masks every 1120x800 pixel with truthful metadata. Each integration has three viewed native/four rendered images and one actual click across both runs, pinned binaries and owned cleanup. 104 affected local tests pass; new combined gate pending. Win32 1x only. |
| Excluded accessibility-name policy (#216) | `excluded-name-direct-after-01`, clean 2f7c686: ten calls/102 checks, two viewed native images and 31 binaries. | `excluded-name-standalone-after-01`: same ten-call/102-check comparison and identities. | Intentional failed steps and redacted reports survive; optional availability is withheld and ordinary names remain truthful. Outside-run tree fallback correctly omits/deletes screenshots; this is not a native masked-image pass. Passing assertions, real reset and all twenty clients/apps absent verify. All fourteen #215/#216 focused cases pass on Windows/macOS in 36227569598; the full gate fails separately as #218. |
| Effective accessibility names | `names-direct-after-01`, a8ba007: twelve calls/126 checks/four viewed images. | `names-standalone-after-01`, same source and counts; pure host and verified provider. | Nine actual visual/logical peer names agree with independent Windows UIA before/after reset. Raw declarations, genuinely unnamed negative control, real validation and read-only journals remain truthful; 31 binaries and all owned cleanup verify. 166 local tests pass; #215 full gate remains pending. Win32 1x only. |
| UI/design audit coverage | Original `audit-direct-01` retains #214; corrected `audit-direct-after-01`, de733c7: 25 calls/179 checks/four viewed images. | `audit-standalone-after-01`, same source: 25 calls/179 checks/four viewed images, pure host/verified provider. | #214 source/report limits, validation, scope absence/unavailability, reset, 31 binaries and all owned cleanup pass; 41 local tests and all six jobs of full de733c7 CI 36222349163 pass; #214 closed. Separate #215 false TabItem name findings disagree with independent native UIA; whole accessibility family remains incomplete. |
| Forms | `capabilities-direct-01`, 3b82d1d: 23 MCP/nine CLI, 29 passing checks; three classified agent mistakes. | Historical SF001-SF019, 483fedb: 13 MCP/six CLI, 13 passing checks. Reused af6e2f3 adds one CLI launch and 19 persistent MCP calls: 19 successes plus one expected stale-popup refusal, three reviewed rendered images. | Standalone popup discovery exposes Reader/Editor/Reviewer; semantic selection changes the independent journal to Reviewer without saving. Separate submit saves exactly once; reset restores Reader/zero saves. The closed popup returns top_level_not_found. Owned app/client/server and manifests are absent; server shutdown exits -1 at the existing transport bound. Launch takes 23.203s with an explicit 60s request budget, not a product default change. ComboBox form inventory remains partial; Win32 1x only. |
| Tables | `capabilities-direct-table-01`, e3cf363: 29 MCP/11 CLI, 26 passing checks; independent images, source and cleanup. | Same standalone run T001-T051: 37 MCP/12 CLI, 27 passing checks; three classified agent mistakes. | Read/filter/page/edit/sort/privacy/draft recovery/reset/stale/replay are covered at Win32 1x; column replacement, duplicate-key fixtures and other backends are not claimed here. |
| Mutation ordering | `mutation-order-direct-after-01`, 5d0df14: 21 MCP/15 CLI, 13 passing checks and one retained agent mistake. | `mutation-order-standalone-after-01`, 5d0df14: 16 MCP/12 CLI, 11 passing checks. | #194 fix: unsafe order, actual tree aliases, individual/reset_all restoration, class/resource independence, values/counters/pixels and cleanup. Native resource values use response/registry evidence; independent resource-value assertions are headless. Broader mutation-evidence/HTML review remains incomplete. |
| Mutation evidence/precedence | `capabilities-direct-evidence-01`, bfd7978: 16 MCP/two CLI, two passing/three failing checks; two confirmed defects. | Fresh comparison pending fixes. | #196 reset freezes style-derived values; #197 logical target summaries disappear despite captured visual aliases. Independent state, reviewed native/rendered pixels and owned cleanup retained. HTML viewer remains blocked. |
| Mutation precedence correction | `style-reset-direct-after-01`, 5fda3f9: 22 MCP/four CLI, nine passing checks. | `style-reset-standalone-after-01`, 5fda3f9: 22 MCP/four CLI, nine passing checks. | Each verifies reverse reset and reset_all across actual theme changes, metadata, independent journal, four viewed images, source hashes and owned cleanup. Agent setup/bookkeeping errors are retained separately. Animated priority remains under audit; #197 and HTML viewing remain unresolved. |
| Animated mutation correction | `animation-direct-after-01`, 8803271: 29 MCP/four CLI, 16 passing checks. | `animation-standalone-after-01`, 8803271: 29 MCP/four CLI, 17 passing checks. | Real finite brush timelines with StyleTrigger/Local target/reference pairs; refusal before effects, individual/reset_all while animation continues, restored bases after stop and fixture reset. Each has seven viewed images, unchanged sources, clean client exit and owned termination at 1x. Controlled headless coverage additionally includes binding/current-value sources and deactivation; native Retina and #197 remain unresolved. |
| Logical mutation evidence correction | `logical-evidence-direct-after-01`, c6b11db: 14 MCP/three CLI, 16 passing checks. | `logical-evidence-standalone-after-01`, c6b11db: 14 MCP/three CLI, 17 checks including pure-host/provider isolation. | #197 valid scoped visual summaries for logical CLI/MCP, explicit depth-limited diagnostics, missing/invalid zero effects, original value/priority restoration, independent journals, seven viewed images per run, six unchanged source hashes and owned cleanup. HTML source inspected; visual viewer blocked. Full c6b11db CI 36130672354 passes; #197 closed. Native scope is Win32 1x. |
| Native initial connection delay (#199) | Controlled native fixture, not a new Direct app exploration. | Opt-in `--accessibility-fixture --uia-delay-fixture`; two real delayed WM_GETOBJECT root responses per arming. D:/AvaScope-QA-active/uia-timeout-199/burst-before-01 and burst-after-01 select identical host/test binaries and independently verified providers. | Original 250ms connection cap fails during a measured 756.958ms response. Separate capped 2000ms connection budget passes two delayed responses/14 nodes; real CLI/MCP, mapping/privacy/ownership and 250ms query-deadline control pass. Native 1/1 and focused 68/68; candidate full gate pending, original CI machine conditions unproven. Continuous-provider delay still correctly reaches the overall deadline. |
| Native accessibility diagnostics | Controlled real HWND/STA regression and affected checks; new Direct QA exploration is not claimed. | `uia-diagnostic-standalone-after-01`, 4ea4d02: nine MCP/two CLI, 14 checks, two viewed images, unchanged journal/source and owned cleanup. | Positive and wrong-name mapping, privacy/stale/owner/exclusion/partial-tree guards pass. Original integration audits pass but test cleanup fails (#200); corrected cleanup and full 108adc5 CI subsequently pass and #200 closes. The remaining delayed stdout completion condition is now tracked as #220; diagnostic preservation itself works. |
| Workflow/export/replay | `workflow-direct-01`, e91041b: 20 MCP/six CLI, 20 checks, six viewed images, fresh host/source verification and owned cleanup. | `workflow-standalone-01`, d4b376e: 21 MCP/six CLI, 20 checks, six viewed images, restored state/source and owned cleanup. | Positive execution/export/parameterized replay plus truncated/mismatched recording, missing bindings, dry-run, unsupported-plan and stop-on-assertion-failure guards. Exact independent journal and complete report evidence; other platforms and broader composition remain unclaimed. |
| Ownership/recovery | `ownership-direct-01`, 8bebfe6: 24 passing checks. | `ownership-standalone-01`, a099b86: 34 passing checks. | Scoped production-bridge comparison; Unix fixture regression and combined gate tracked separately. |
| Pseudo-state target budgeting | `pointer-focus-direct-01` discovers #206 on ba07220; candidate `pseudo-budget-direct-after-01`: eleven calls, exact state/pixel/identity/cleanup evidence. | `pseudo-budget-standalone-after-01`: eight calls with fresh pure host/verified provider and matching target/guard results. | Deep-target, pressed/disabled and restoration checks pass; both transports/integrations expose separate false hover success (#207). Three viewed images per candidate run, owned cleanup, Win32 1x. This is not a passing whole state matrix; #206 full gate remains pending. |
| Actual hover and cleanup | `hover-direct-after-01`, d32e539 plus #207 candidate: twelve calls/76 independent checks and six viewed images; real hover class and 3859 changed pixels, pressed/disabled/reset, delayed inspection and missing/stale refusal. | Fresh `hover-standalone-after-01`: twelve calls/78 checks and six viewed images, same correct behavior with pure host/verified provider (da3987d8...). | Owned apps 8460/9204 and all clients are gone, both candidate sources and selected binary hashes unchanged. Eight real new regressions plus existing affected tests pass (56/two Unix skips). Full #207 gate remains required; native scope is Win32 1x. |

`focus-recording-direct-01` on clean `703c960` adds 22 public calls and 45
independent checks with five viewed images: observed Tab/Shift+Tab focus round
trip, stale focus refusal, action-map availability/search/partial coverage and
window guards. It confirms #209 deep recording geometry omission on both
transports, with a passing shallow control. Candidate `recording-direct-after-01`
then resolves the deep target and parent in nine calls/116 checks, with six
viewed images and owned cleanup. It separately exposes #210's transformed
origins with unscaled extents; this remains a failed geometry boundary. Neither
those checks nor successful recording responses imply complete geometry or
standalone focus coverage. Fresh `recording-standalone-after-01` verifies nine
calls/118 checks and six viewed images with pure host/verified provider, matching
target-resolution success and the retained #210 extent failure. Both apps and
all clients are gone; source and pinned binaries remain unchanged.

The #210 correction is subsequently verified in `transformed-direct-after-01`
and `transformed-standalone-after-01`, based on `2fbbee7` plus the retained bridge
candidate. Each has 17 calls (12 MCP/five CLI), 153/155 checks respectively,
eight exact pixel-extent comparisons and eight reviewed images. Full transformed
bounds, local layout separation, fixed ancestor scale, picking, missing refusal,
input state, source/binary identity and owned cleanup pass at Win32 1x. The
combined #209/#210 full gate remains pending. Earlier #206/#207 acceptance is
now complete after all six jobs of `59b9b2c` CI `36177716925` pass; their original
failures above remain historical evidence. Standalone focus exploration is
still not implied by these geometry journeys.

Subsequent `focus-standalone-after-01` on clean `2684a38` adds 24 calls
(15 MCP/nine CLI), two actual Windows Tab/Shift+Tab actions, 61 independent
checks and eight viewed images. Focus inspection/probes, stale refusal,
action-map search/realization/partial coverage, window guards and the corrected
deep/shallow/missing recording journey pass. App key/action counters and
independent UIA agree, except for one retained immediate native-Tab UIA lag
that resolves on a separate read without redispatch. Source/25 pinned binaries
and original-process cleanup verify. This supplies standalone focus evidence at
Win32 1x; multi-window/modal focus and other platforms remain separate boundaries.

`windows-direct-01` on clean `53ad53c` subsequently supplies Direct multi-window
evidence: 32 public calls, 124 checks and seven viewed native images. Parent
resize/stale/restore, owned child move with independent native coordinate delta,
modal resize blocking, explicit modal focus/owned HWND Tab, isolated closure and
closed-target refusal pass. Final window focus has no focused control, confirmed
by UIA. The original missing-isolation close request was correctly refused and
is preserved separately from its corrected invocation. Reset, binary/source
identity and original-process absence verify. Subsequent standalone evidence is
recorded below; no cross-platform/Retina claim. The full
19dd8bd CI fails a separate macOS common-state matrix case (#211) even though all
thirteen #209/#210 regressions and the Windows/Linux/native gates pass.

`windows-standalone-01` on clean `b650ca1` (production `16a44ac`) supplies the
comparison: 32 window calls/126 checks/seven viewed images match Direct geometry,
modal, focus and lifecycle outcomes. Fourteen additional navigation calls/58
checks/two images verify real page changes, ordered retrospective routes, stale
tips, bounded queries and clear. Without a declared host identity, all visits
retain distinct state keys and sampled loop candidates stay explicitly uncertain;
the partial sample does not distinguish these pages. The missing declared
context/revision coverage is addressed by the subsequent #212 journey below.
Thirty-one binaries,
pure host/verified provider, reset and all 46 original client/app absences verify.

The #212 scoped document fixture on clean `1ebaaa1` is exercised in
`navigation-direct-01` and `navigation-standalone-01`: 29 corrected public calls
each (17 MCP/12 CLI), 131/116 checks and five viewed images per integration at
Win32 1x. Overview/details/context/edit/reset, ordered routes and stale/privacy/
clear guards agree with the independent journal and pixels. Direct recognizes
declared revisits; standalone retains unique visits and explicitly uncertain
sampled candidates. Both remain partial observations with hidden state unverified.
All 31 selected binaries/source per run and owned cleanup verify. Seven earlier
Direct calls retain an agent depth-limit mistake and invalid caller-reported
transition; that history was cleared and excluded from the corrected evidence.
All 65 original clients and both apps are absent. Twenty-six affected tests pass;
the complete hosted gate remains pending behind #211, so #212 stays review.

Subsequent exact f2ab747 CI 36219846271 passes all six jobs: independently verified
Windows 1021/eight skips, macOS 1019/nine, Linux 33 and six native/three expiry
runs at actual 1x. #209/#210/#211/#212 are closed after their full acceptance;
earlier pending/failing statements above describe retained historical stages.

The separate `preview-campaign-01` journey on clean `0658716` uses Windows
headless Skia / Avalonia 12.1.3: 22 MCP/six CLI calls, 17 positive checks, nine
viewed images and independent pixel analysis. DPI sizing, source reload with
error/recovery, exact CLI/MCP image parity, theme/culture/design data, multiple
sizes, diff/regions and session close guards pass. It also confirms two failures:
#201 animation offsets do not control time, and #202 baseline-create ignores
build options. Baseline comparisons, file watch and diagnostic filters remain
pending in that historical round. The `preview-followup-01` journey on `96c1656`
adds eight MCP/eight CLI calls with verified source/binary hashes (only the
handoff document was dirty). Equal/changed loose-XAML baseline checks work on
both transports; independent pixels agree with the 28,734-pixel reported delta
and passing/failing JUnit reports. CLI watch performs one real file-triggered
reload, skips unchanged input and rejects a closed session. MCP/CLI error-only
filtering retains identical pixels. The journey confirms #205 on both transports:
valid local DataContext bindings render exactly like the literal expected value,
but incorrectly produce a missing-root-context warning. Five images were viewed,
33 verification assertions retain this defect plus one separate agent CLI-argument
mistake. All 16 clients exit and the session metadata is closed. #202 closes after
the full c83e50c gate; project baseline comparison timeout remains #203. #205's
ba07220 correction has twelve real pixel-backed scope cases and actual CLI/MCP
verification; its full gate is pending. This is neither native Retina nor visual
HTML validation.

The final explicit CLI lifecycle/preview boundaries were exercised on the
unchanged d7562ed production source, now on master. The native launch/close/mcp
journey has 17 calls, two reviewed rendered images, an independent toggle journal,
close-only keeping the app alive, later owned termination and an idempotent
already-exited outcome. A short 1500ms missing-executable attempt times out in
helper readiness; it is not executable-resolution-stage coverage.

The reusable D: environment adds ten completed preview calls: session creation,
source reload, malformed-XAML diagnostics, recovery, close/reload refusal,
baseline-create at 500x320/640x360, invalid size, static animation offsets 0/100/100
and invalid offset. Four images were reviewed. The initial malformed-XAML call
was interrupted by the old outer 60s harness deadline before retaining a product
response; the owned tree exited. A separate 90s observer permits the unchanged
60s product deadline and cleanup, and returns the normal XAML failure in 29.675s.
Its later success does not explain the original child delay. Registry success
with `lastRender.success=false` is the existing documented metadata contract,
not a successful render. Moving animation timing remains unresolved in #201;
the static control is not timing coverage. This round does not repeat the
previous public baseline-check journeys.

The shared final d7562ed CI 36335386524 passes all six jobs: Windows 1166/eight
skips, macOS 1163/ten, Linux 33/zero; nine scripted native lifecycle/expiry runs;
managed Wayland CLI/MCP 1x/2x positives, expected failures and cleanup. The six
Wayland PNGs contain three distinct reviewed images. These checks do not close
the Retina or independent local OS-observation gaps. Historical local artifacts
were deleted under the owner's 2026-09-27 retention policy; issue comments,
commits and regressions retain the results. No reconstruction/archive is needed.

All listed native apps and clients were cleaned up. Shared-host lifecycle failures
are retained as #192; hosted desired-state timing failure is #191. Neither is
converted into a passing result by unrelated native journeys.
