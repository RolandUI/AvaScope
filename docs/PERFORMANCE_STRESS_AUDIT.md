# AvaScope Performance And Stress Audit

This document records the `v0.9.0` beta stress audit for agent-facing AvaScope workflows. The goal is bounded, repeatable local validation rather than benchmark-grade timing numbers.

## Automation Coverage

- Large visual tree: `PerformanceStressAuditTests.UiAuditBuilderCapsLargeTreeIssuesInventoryAndAgentReview` builds a 161-node runtime tree with 160 actionable controls and verifies bounded UI audit output.
- Large diagnostics payload: `PerformanceStressAuditTests.DiagnosticsCapsLargeManifestAndPreviewSessionIssuePayloads` feeds 120 invalid bridge manifests plus 250 preview-session diagnostics and verifies `diagnosticIssues` is capped.
- Repeated preview: `PerformanceStressAuditTests.PreviewSessionRegistryHandlesRepeatedOneShotReloadsAndPersistentRestore` creates one preview session and reloads it repeatedly through the isolated one-shot PreviewHost model.
- Repeated mutation/reset: `BridgeHeadlessSmokeTests.RuntimeMutationRepeatedSetPropertyAndResetAllKeepsReviewBounded` applies 24 runtime width mutations, verifies review history, then resets all mutations.
- Persistent session store: `PerformanceStressAuditTests.PreviewSessionStoreCapsLargePersistentSessionDiagnosticsAndCleanup` persists 130 preview records and verifies diagnostics/cleanup only process the documented budget.
- Baseline suite: `PerformanceStressAuditTests.BaselineSuiteExpansionHandlesLargeVariantMatrixWithDeterministicArtifactPaths` expands a 288-variant suite matrix and verifies deterministic artifact paths and inherited comparison rules.

## Bounded Output Budgets

- Runtime visual/logical tree requests default to `maxDepth=10` and reject depth above `64`.
- Runtime find defaults to `maxResults=100` and rejects result limits above `1000`.
- Diagnostics defaults to `maxSessions=50`, rejects values above `100`, and caps normalized `diagnosticIssues` at `200`.
- `PreviewSessionStore.GetDiagnostics()` reports at most `100` persisted preview-session records per call; cleanup follows the same budget.
- UI audit returns at most `100` issues and `100` inventory items. Its `agentReview` returns at most 8 summary/failure items through `AgentReviewSurface`.
- Runtime mutation review returns at most `RuntimeMutationReviewResponse.MaximumEntries` (`100`) history and active mutation entries per response. The bridge keeps an internal rolling mutation history of `128`.
- Runtime mutation and evidence responses cap diagnostics at 16 and avoid inlining screenshots or visual tree snapshots.
- Report and artifact surfaces return file paths/URLs instead of image bytes. Baseline report packs write JSON, HTML, JUnit XML, and SARIF JSON files.
- Capability discovery and safety docs are the compatibility gate for clients that need to know whether a bounded surface exists before invoking it.

## Sample Workflow Coverage

The getting-started sample remains the stable sample for agent workflows:

- Preview: `avascope preview .\samples\AvaScope.GettingStartedApp\AvaScope.GettingStartedApp.csproj --profile main --out .\artifacts\samples\main-preview.png`
- Runtime attach/control: launch the sample with `AVASCOPE_SAMPLE_BRIDGE=1`, then use `attach`, `list-top-levels`, `visual-tree`, `find-nodes`, `mutate-node`, `mutate-node-evidence`, `mutation-review`, `reset_all`, and `close-session`.
- Visual regression: use `baseline-create --suite` and `baseline-check --report --report-pack` with explicit artifact directories.
- Diagnostics: run `doctor`, `diagnostics`, and `cleanup` with explicit manifest/session-store directories for repeatable local runs.

The sample does not require project-specific source edits beyond the documented bridge opt-in environment variable.

## Operational Notes

- Repeated previews are intentionally one-shot isolated child-process renders. `PersistentHostEnabled=false` is expected until a separate long-lived host model is designed and validated.
- Large responses should be consumed by `agentReview`, summary counts, and artifact paths first. Agents should request deeper trees or reports only when the summary indicates they need them.
- Runtime mutation is temporary and local-only. Use `mutation-review` before cleanup and `reset_mutation` or `reset_all` before closing a session when evidence is still needed.
- Baseline report packs are safe to upload as CI artifacts, but baseline approval remains a manual reviewed workflow.

## Validation Commands

Run the focused stress audit first:

```powershell
dotnet test AvaScope.slnx --no-build --filter "FullyQualifiedName~PerformanceStressAuditTests|FullyQualifiedName~BridgeHeadlessSmokeTests.RuntimeMutationRepeatedSetPropertyAndResetAllKeepsReviewBounded|FullyQualifiedName~PerformanceStressAuditDocumentationTests"
```

Then run the normal slice validation:

```powershell
dotnet build AvaScope.slnx --no-restore -v:minimal
dotnet test AvaScope.slnx --no-build
git diff --check
```
