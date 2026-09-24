# Complex workflow and agent QA sample

The existing no-argument workflow remains Headless + Skia for compatibility with
`eng/test-complex-workflow.ps1`. Select `--native` for actual desktop windows or
`--headless` explicitly for deterministic tests. The flags are mutually exclusive.

`--qa` selects the shared agent QA window instead of the original two-window
workflow. It contains settings, precise text editing, case-distinct identities,
200 seeded rows, template-rich localized rendering, a local transform, navigation,
window/modal/popup lifecycle and cancellable delayed loading. Additional Form,
Table and Input tabs provide validated profile submission, sensitive/read-only/
disabled fields, role/consent/priority controls, 200 keyed editable DataGrid rows,
column sorting, menus, context menus, pointer capture/drag and keyboard routing.
Both hosts use the
same compiled XAML and code; the fixture itself has no AvaScope dependency.

```powershell
dotnet run --project samples/AvaScope.ComplexWorkflowApp -c Release -- --native --qa
dotnet run --project samples/AvaScope.StandaloneHost -c Release -p:EnableUiInspection=true -p:EnableQaFixture=true -- --qa
```

The standalone command additionally requires the normal opt-in external provider
environment documented in [STANDALONE_PROVIDER.md](../../docs/STANDALONE_PROVIDER.md).
Its project continues to have no AvaScope package or project reference. The QA
fixture is opt-in at compile time there and does not change existing host scenes.

Set `AVASCOPE_QA_OUTPUT` to an owned evidence directory to write `qa-state.json`.
The application writes this independently of the bridge: actual checked/text state,
per-reset action counts, editor generation, child windows, selected page/row,
readiness, actual scale/client size and the last 100 event records. Text fields
are bounded to 2048 characters; use synthetic test data only. The snapshot is
replaced atomically. Password values are excluded from the journal. The `form`
section separates current fields, validation and last saved profile; `table`
records seed-order data and actual view order; `input` records independent event
counts and pointer displacement. It is an observation of app state, not an alternative action API.

`Reset test state` restores seed 42, settings, data, page, theme, locale and viewport,
cancels delayed work and closes owned child windows/popups. The direct bridge host
also exposes the existing `agent-qa` fixture (`qa-memory`) via custom actions;
the standalone host exposes the same reset through its ordinary UI without adding
a reflection registration API. Replacing the editor intentionally changes its
instance while preserving its AutomationID, for genuine stale-target tests.

The fixture expires after one hour by default. Set
`AVASCOPE_QA_LIFETIME_SECONDS` to 30–14400 for an explicit run lease; reset does
not extend it. Closing the main QA window cancels pending work and closes children.

Native screen capture requires the explicit `--authorize-screen-capture` host
flag on a declared test desktop. Native OS permissions still apply. Always record
the observed scale and geometry: the Full HD button requests 1920×1080 DIP, but
an OS/display may constrain it. An unavailable 2× native desktop cannot be replaced
by a simulated headless scale when reporting coverage.

The initial checks for this fixture are `AgentQaFixtureTests` (reset/cancellation,
owned children, independent state, instance replacement and case-distinct actions),
the existing complex CLI/MCP workflow, and native visual inspection. The broader
agent workspace and task charters are tracked in #163; the exploratory audit in #166.
