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
| `mutate_node`, `mutate_node_evidence`, `mutation_review` | Rendering text/spacing/color/size; unsupported property, history, reset and before/after evidence. | Pixels, original property values, review artifact and unchanged source files. |
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
| Ownership/recovery | `ownership-direct-01`, 8bebfe6: 24 passing checks. | `ownership-standalone-01`, a099b86: 34 passing checks. | Scoped production-bridge comparison; Unix fixture regression and combined gate tracked separately. |

All listed native apps and clients were cleaned up. Shared-host lifecycle failures
are retained as #192; hosted desired-state timing failure is #191. Neither is
converted into a passing result by unrelated native journeys.
