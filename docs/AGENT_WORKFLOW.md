# AvaScope Agent Workflow

Use this decision loop when controlling an Avalonia application. The [user guide](USER_GUIDE.md) owns command examples and options; [executable recipes](AGENT_RECIPES.md) cover onboarding and repeatable scenarios. Repository development follows the separate [project workflow](GITHUB_PROJECT_WORKFLOW.md).

## 1. Select Tools And Check Readiness

Use an installed or unpacked release; build only when validating changed source. Follow [installation and discovery](USER_GUIDE.md#install-from-release-artifacts), then:

```powershell
avascope --version
avascope capabilities
avascope doctor
avascope diagnostics --max-sessions 10 --mode active-only
```

Gate features by capability IDs, not descriptions or version guesses. Select the exact project/provider/backend with [target readiness](TARGET_READINESS.md). After attach, query `session-capabilities`: the server catalog does not prove the connected app's support. Use isolated manifest/preview stores for fixture validation and default stores only when diagnosing the current installation.

## 2. Observe The Selected App Or Preview

For a view without a running app, use [preview profiles](USER_GUIDE.md#preview-and-profiles), [animation sampling](USER_GUIDE.md#preview-animation) or [preview sessions](USER_GUIDE.md#preview-sessions). Preview executes project code in an isolated process; it does not run the application's full startup lifecycle.

For runtime work, activate the bridge explicitly and select the returned session. Inspect windows, then request a bounded [observation](RUNTIME_OBSERVATIONS.md) or find a target by stable selector. Carry generation-scoped node IDs only into immediate follow-ups. Use [relationship queries](RELATIONSHIP_QUERIES.md) for repeated controls and [action explanations](ACTION_EXPLANATIONS.md) for blocked targets.

Check availability, truncation, geometry and [runtime provenance](RUNTIME_PROVENANCE.md) before deciding. A partial tree or empty audit is not proof of absence; [audit coverage](USER_GUIDE.md#audits) and [native accessibility](NATIVE_ACCESSIBILITY.md) have distinct limits.

## 3. Act Once And Verify

Prefer [desired-state actions](DESIRED_STATE_ACTIONS.md) or a [semantic workflow](USER_GUIDE.md#semantic-workflows) with stable selectors, typed waits and an explicit `verify` postcondition. Re-resolve targets immediately before dispatch. Use [dispatch preconditions](DISPATCH_PRECONDITIONS.md) when document, row or value identity matters.

Choose [input strategy](INPUT_STRATEGIES.md) explicitly when native behavior is required. Successful dispatch does not establish the application outcome. Do not repeat an uncertain side effect; preserve request IDs/idempotency keys and inspect the result through [run recovery](RUN_RECOVERY.md).

For an owned build/launch/workflow, use [run-scenario](USER_GUIDE.md#scenario-lifecycle) and check `failureStage`, workflow status and cleanup separately. Follow the user guide's [task reference](USER_GUIDE.md#task-reference) for forms, tables, text editing, focus, windows and declared application actions.

## 4. Review Evidence

Inspect operation status and bounded `agentReview` first, then relevant referenced artifacts. Compare expected application state with the actual observation; retain missing evidence as an explicit limitation. Use [screen evidence](SCREEN_EVIDENCE.md) when desktop presentation matters: a rendered screenshot or headless result does not establish native visibility.

For visual changes use [diffs/baselines](USER_GUIDE.md#visual-comparison); never accept a changed baseline automatically. For timing assertions inspect measured intervals and `inconclusive` outcomes in [runtime animation](USER_GUIDE.md#interaction-animation).

Runtime mutations are [reversible experiments](USER_GUIDE.md#runtime-mutations). Review before/after evidence, reset overlapping overrides in reverse order, and inspect advisory source suggestions before editing source.

Apply [evidence policy](USER_GUIDE.md#workflow-evidence-and-privacy) to all referenced artifacts as well as inline output. Incomplete trees cannot prove that selective screenshot masking covered every sensitive control.

## 5. Close Owned State

```powershell
avascope close-session --session <session-id>
avascope close-preview-session --session <preview-session-id>
```

When AvaScope launched and owns the app, explicitly request `--terminate-launched-process true` if termination is intended. Respect `not_owned` and process-start identity checks; never work around them by killing a process by name. Use [cleanup](USER_GUIDE.md#cleanup-and-reload) for stale AvaScope metadata and the [QA retention policy](AGENT_QA_LAB.md#local-testing-and-retention-policy) for repository testing.
