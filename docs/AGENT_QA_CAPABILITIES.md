# Agent QA capability map

Inventory: 75 MCP tools from `src/AvaScope.Mcp/AvaScopeMcpTools.cs` on the
#172 expansion branch; CLI commands from `src/AvaScope.Cli/Program.cs`.
Execution tracker: #174, umbrella #166. This is a coverage plan, **not a pass
report**. The expanded campaign must attach an exact source/artifact identity,
request/response, independent oracle and outcome to every applicable journey.
Historical passing tests do not validate newly added scenes.

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
| `window`, `navigation` | Tabs, popup, child and modal; discover/activate/resize/close guards, record/revisit route. | Window count, observed geometry, selected tab and focus; requested size is not observed size. |
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
| Forms | `capabilities-direct-01`, 3b82d1d: 23 MCP/nine CLI, 29 passing checks; three classified agent mistakes. | `capabilities-standalone-01` SF001-SF019, 483fedb: 13 MCP/six CLI, 13 passing checks. | ComboBox inventory is partial; separate popup selection passed only in Direct. |
| Tables | `capabilities-direct-table-01`, e3cf363: 29 MCP/11 CLI, 26 passing checks; independent images, source and cleanup. | Same standalone run T001-T051: 37 MCP/12 CLI, 27 passing checks; three classified agent mistakes. | Read/filter/page/edit/sort/privacy/draft recovery/reset/stale/replay are covered at Win32 1x; column replacement, duplicate-key fixtures and other backends are not claimed here. |
| Mutation ordering | `mutation-order-direct-after-01`, 5d0df14: 21 MCP/15 CLI, 13 passing checks and one retained agent mistake. | `mutation-order-standalone-after-01`, 5d0df14: 16 MCP/12 CLI, 11 passing checks. | #194 fix: unsafe order, actual tree aliases, individual/reset_all restoration, class/resource independence, values/counters/pixels and cleanup. Native resource values use response/registry evidence; independent resource-value assertions are headless. Broader mutation-evidence/HTML review remains incomplete. |
| Mutation evidence/precedence | `capabilities-direct-evidence-01`, bfd7978: 16 MCP/two CLI, two passing/three failing checks; two confirmed defects. | Fresh comparison pending fixes. | #196 reset freezes style-derived values; #197 logical target summaries disappear despite captured visual aliases. Independent state, reviewed native/rendered pixels and owned cleanup retained. HTML viewer remains blocked. |
| Mutation precedence correction | `style-reset-direct-after-01`, 5fda3f9: 22 MCP/four CLI, nine passing checks. | `style-reset-standalone-after-01`, 5fda3f9: 22 MCP/four CLI, nine passing checks. | Each verifies reverse reset and reset_all across actual theme changes, metadata, independent journal, four viewed images, source hashes and owned cleanup. Agent setup/bookkeeping errors are retained separately. Animated priority remains under audit; #197 and HTML viewing remain unresolved. |
| Animated mutation correction | `animation-direct-after-01`, 8803271: 29 MCP/four CLI, 16 passing checks. | `animation-standalone-after-01`, 8803271: 29 MCP/four CLI, 17 passing checks. | Real finite brush timelines with StyleTrigger/Local target/reference pairs; refusal before effects, individual/reset_all while animation continues, restored bases after stop and fixture reset. Each has seven viewed images, unchanged sources, clean client exit and owned termination at 1x. Controlled headless coverage additionally includes binding/current-value sources and deactivation; native Retina and #197 remain unresolved. |
| Logical mutation evidence correction | `logical-evidence-direct-after-01`, c6b11db: 14 MCP/three CLI, 16 passing checks. | `logical-evidence-standalone-after-01`, c6b11db: 14 MCP/three CLI, 17 checks including pure-host/provider isolation. | #197 valid scoped visual summaries for logical CLI/MCP, explicit depth-limited diagnostics, missing/invalid zero effects, original value/priority restoration, independent journals, seven viewed images per run, six unchanged source hashes and owned cleanup. HTML source inspected; visual viewer blocked. Full c6b11db CI 36130672354 passes; #197 closed. Native scope is Win32 1x. |
| Native accessibility diagnostics | Controlled real HWND/STA regression and affected checks; new Direct QA exploration is not claimed. | `uia-diagnostic-standalone-after-01`, 4ea4d02: nine MCP/two CLI, 14 checks, two viewed images, unchanged journal/source and owned cleanup. | Positive and wrong-name mapping, privacy/stale/owner/exclusion/partial-tree guards pass. Original integration audits pass but test cleanup fails (#200); corrected cleanup and full 108adc5 CI subsequently pass and #200 closes. Original hosted UIA cause remains #199. |
| Workflow/export/replay | `workflow-direct-01`, e91041b: 20 MCP/six CLI, 20 checks, six viewed images, fresh host/source verification and owned cleanup. | `workflow-standalone-01`, d4b376e: 21 MCP/six CLI, 20 checks, six viewed images, restored state/source and owned cleanup. | Positive execution/export/parameterized replay plus truncated/mismatched recording, missing bindings, dry-run, unsupported-plan and stop-on-assertion-failure guards. Exact independent journal and complete report evidence; other platforms and broader composition remain unclaimed. |
| Ownership/recovery | `ownership-direct-01`, 8bebfe6: 24 passing checks. | `ownership-standalone-01`, a099b86: 34 passing checks. | Scoped production-bridge comparison; Unix fixture regression and combined gate tracked separately. |
| Pseudo-state target budgeting | `pointer-focus-direct-01` discovers #206 on ba07220; candidate `pseudo-budget-direct-after-01`: eleven calls, exact state/pixel/identity/cleanup evidence. | `pseudo-budget-standalone-after-01`: eight calls with fresh pure host/verified provider and matching target/guard results. | Deep-target, pressed/disabled and restoration checks pass; both transports/integrations expose separate false hover success (#207). Three viewed images per candidate run, owned cleanup, Win32 1x. This is not a passing whole state matrix; #206 full gate remains pending. |

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

All listed native apps and clients were cleaned up. Shared-host lifecycle failures
are retained as #192; hosted desired-state timing failure is #191. Neither is
converted into a passing result by unrelated native journeys.
