# AvaScope Validation

For documentation-only policy or prose changes, review the diff for correctness and consistency, check affected relative links, scan outgoing content for machine-specific information, and run `git diff --check`. Record that validation without rebuilding the product or dispatching a full CI gate. Executable examples, generated contracts, scripts or product behavior changes still require their relevant tests. The [GitHub information policy](../AGENTS.md#github-information-policy) applies to all published validation summaries and evidence.

## Choose Validation By Risk

Development commits and pushes go directly to `master`, without task branches, worktrees or pull requests. Choose the smallest set of checks that establishes the changed behavior and protects its affected dependents. The commands later in this document are a scope-specific reference, not a checklist to run in full for every change.

| Level | When required | Scope |
| --- | --- | --- |
| Local | Default for changes that can be adequately verified locally | Build affected components; run the relevant regression/contract tests and affected public CLI/MCP or native journey. Documentation-only edits use the checks above. |
| Targeted CI | Required acceptance evidence depends on a platform or environment unavailable locally | Run only the relevant platform and test/job group. A Windows-only result does not establish macOS/Linux or native display coverage. |
| Full CI | Before release, or when cross-cutting risk cannot be covered by narrower checks | Validate the required platform matrix and integration boundaries; for release, verify the exact candidate's packaged CLI/MCP/provider/installers and publication inputs. |

Assess shared protocol/Core behavior, transport and process lifecycle, security boundaries, Avalonia/.NET/dependency upgrades, and build or packaging changes for wider impact. These are reasons to examine dependencies and expand coverage where needed, not automatic full-CI triggers for every edit in those areas. Account for the accumulated relevant changes since the last applicable validation, not just the latest commit. If the impact cannot be bounded confidently, broaden validation before claiming acceptance.

A completed commit, issue or batch, elapsed time, or a desire for a fresh green badge does not justify a full gate. Full solution build/test, all-platform QA, installers and release packaging are not mandatory for each development slice. Specific issue acceptance criteria remain binding; keep missing evidence pending instead of calling it passed.

## Local Development Loop

Reproduce a defect through the affected public behavior where applicable, add a focused regression, implement the smallest correction, then build and test the affected components and dependents. Verify the actual application journey when unit tests cannot establish the behavior, especially for native or visual changes. Inspect the diff and run `git diff --check`, then commit and push the coherent, locally validated change to `master`. Resolve failures attributable to the change before push; report unrelated failures accurately.

Reuse the current QA binaries and build outputs. Restore when dependencies or restore inputs change or assets are missing; rebuild before using `--no-build` after relevant source/test/configuration changes. Do not reuse an old binary merely because a filtered test passed. Reuse a successful result only while its relevant source, tests, dependencies, configuration and environment remain applicable. Keep provenance compact and local under the existing QA policy.

For a required full local solution check, run from the repository root:

```powershell
dotnet restore AvaScope.slnx
dotnet build AvaScope.slnx
dotnet test AvaScope.slnx
git status --short
```

## CI And Release Decisions

CI is deliberately dispatched from `master` to answer an identified validation gap. Briefly identify the selected commit, required platforms/tests and reason in the existing issue or chat context; no extra start/status comment is required. Verify the run's source revision. Independent local work can continue while it runs, but its result does not cover later commits automatically.

Prefer the existing targeted native-input workflow when it covers the required case. Selective platform/job controls for the main CI remain pending; do not claim an unavailable narrow mode was run. If only the current broad workflow can supply required evidence, its use needs the same concrete risk justification. Diagnose failures and rerun the affected checks/jobs first; rerun the complete gate only when the fix invalidates broader coverage or the remaining risk requires it.

Commit/push, issue closure and release publication are separate decisions. Close an issue after its specific acceptance criteria pass; full CI is not a universal closure requirement. Keep an issue in review when required validation is pending, or blocked for an external dependency. Release publication requires complete applicable validation for the exact candidate and its artifacts, plus publication authorization. A Windows Release workflow alone does not establish the full native platform matrix.

The target workflow design uses explicit dispatch, selectable platform/job groups and build reuse within each compatible platform/configuration. Coordinate CI and Release validation so identical checks are not repeated without cause; verify the actual release artifacts even when source-level results can be reused. Parallelize independent jobs to reduce waiting, and omit unnecessary work to reduce cost. Workflow optimization is tracked in [#234](https://github.com/RolandUI/AvaScope/issues/234).

## Validation Reference

Use the affected test class or method as a filter, after building the relevant configuration:

```powershell
dotnet test tests/AvaScope.Tests/AvaScope.Tests.csproj -c Release --no-build --filter "FullyQualifiedName~<affected-test>"
```

The table locates coverage; select applicable cases, not every row. Include affected CLI/MCP adapters when changing shared behavior. Run builds and tests sequentially when they share `bin`/`obj` outputs.

| Change | Test entry points |
| --- | --- |
| Protocol, versions and capability discovery | `ProtocolContractTests`, `CapabilityCompatibilityCheckerTests`, relevant `CliSmokeTests` / `AvaScopeMcpToolsTests`, `McpStdioSmokeTests` |
| Bridge transport, discovery and ownership | `AvaScopeBridgeTests`, `LocalBridgeClientTests`, `BridgeHeadlessSmokeTests`; attach/launch/close/cleanup adapter cases |
| Input, selectors, workflows and state | Affected Core runner and bridge cases; corresponding CLI/MCP cases and the real application postcondition |
| Preview build, diagnostics and animation | `PreviewHostClientTests`, `PreviewHostSmokeTests`, matching preview adapter cases |
| Preview profiles, sessions, viewer and watch | `PreviewSessionRegistryTests` and matching `CliSmokeTests` / `AvaScopeMcpToolsTests` |
| Mutation apply, reset, evidence and source suggestions | `RuntimeMutation` / `MutationReview` cases across Protocol/Core/Bridge/adapters; `RuntimeSourceSuggestionBuilderTests` |
| Pseudo-states and interaction animation | `RuntimePseudoStateMatrixRunnerTests`, `RuntimeInteractionAnimationRunnerTests` and bridge/adapter cases |
| UI/design audit | `UiAuditBuilderTests`, `DesignQualityAuditBuilderTests` and bridge/adapter cases, including incomplete evidence |
| Image comparison and baselines | `PreviewImageDifferTests`, `SemanticScreenshotComparerTests`, `PreviewBaselineManagerTests`, `PreviewBaselineReportPackExporterTests` and adapter cases |
| Evidence privacy and action policy | `RuntimeEvidencePolicyEnforcerTests`, `RuntimeEvidencePolicyRedactsAndMasksWorkflowEvidenceEndToEnd` |
| Performance and response limits | `PerformanceStressAuditTests`; [budgets and stress cases](PERFORMANCE_STRESS_AUDIT.md) |
| Installers | `InstallerWorkflowTests`; artifact-backed `PackagedInstallerSupportsInstallRepairDoctorMcpAndUninstall` |
| Documentation and public contract | Affected `Documentation` tests and `StableSurfaceContractTests`; links, anchors and publication-policy review |
| Release guard | `eng/test-release-commit.ps1 -OutputDirectory <qa-root>`, using mocked GitHub responses |

For bridge onboarding, profiles and native behavior, select the relevant integration gate:

| Gate | Purpose / prerequisites |
| --- | --- |
| `eng/test-standalone-provider.ps1` | Enabled/disabled/incompatible hosts, provider pins/tampering, real CLI/MCP loading and owned shutdown. `-Native -SkipBuild` uses a prepared native environment and current binaries/provider. See [provider integration](STANDALONE_PROVIDER.md). |
| `eng/test-integration-onboarding.ps1` | Disposable clean projects, both integration modes, enabled/disabled builds, returned snippets, named profiles and log redaction. `-Native -SkipBuild` selects native validation after preparation. |
| `eng/test-managed-wayland.ps1` | Built Release tools/provider and Linux Weston dependencies; real 1x/2x capture/text, refusal and cleanup. [Wayland limits](MANAGED_WAYLAND.md) differ from X11. |
| `eng/test-agent-qa-lab.ps1 -SkipBuild -TestExpiry` | Two-host lifecycle, lease expiry and failure-evidence plumbing on the selected desktop; does not substitute for [agent-operated tasks](AGENT_QA_LAB.md). |
| `eng/test-packaged-lifecycle.ps1 -CliAssembly <package>/avascope.dll -Configuration Release` | Packaged build/launch, readiness, attach, workflow, evidence and exact process cleanup on each required OS. |
| `eng/test-complex-workflow.ps1` | Source and packaged CLI/MCP multi-window workflows; invocation and required assertions below. |
| `eng/test-linux-installer.sh <installer> <version>` | Linux install, repair, diagnostics and uninstall. |
| `eng/test-macos-packaged-workflow.sh` | Native macOS installed CLI/PreviewHost, runtime tree/screenshot, preview and uninstall. Execution of each architecture requires a compatible runner. |

The [native platform matrix](NATIVE_PLATFORM_MATRIX.md) owns backend-specific commands and coverage limits. For an artifact-backed installer test, package only the required runtime, set `AVASCOPE_INSTALLER_ARTIFACT` to that exact installer and run the installer test above. Windows packaging needs Inno Setup 6 or 7. Packaging all platforms is not a prerequisite for editing documentation.

### Test Execution Invariants

- Await headless asynchronous bodies through `BridgeHeadlessSmokeTests.DispatchAsync` or `HeadlessUnitTestSession.Dispatch<T>(Func<Task<T>>, CancellationToken)`. A void-returning async lambda can select the synchronous overload and leave a nested task unobserved. `TestSessionPropagatesAnExceptionAfterAnAsynchronousBoundary` protects this boundary.
- Tests and CLI/MCP children use isolated run/bridge registries. Preserve explicit `AVASCOPE_RUN_STORE_DIR` and `AVASCOPE_BRIDGE_MANIFEST_DIR` overrides; never depend on a developer's normal run store.
- A transport success is insufficient: verify the requested state, coverage and owned cleanup. Native claims need observed backend/scale and independently reviewed images under the [lab policy](AGENT_QA_LAB.md).
- Privacy checks cover inline output and referenced JSON, screenshots, Markdown, JUnit, audit, timeline and lifecycle logs. Test masks, fail-closed removal, owned retention, traversal and authorization refusals. A clean inline result does not excuse a secret in a fallback artifact.
- For visual reports, confirm stdout and JSON agree on `passed`/`entries`, and that JSON/HTML/JUnit/SARIF outputs report the same result. Collection/upload examples live in [visual regression CI](VISUAL_REGRESSION_CI.md).

### Complex Workflow Gate

```powershell
pwsh -NoProfile -File eng/test-complex-workflow.ps1 -CliAssembly <cli>/avascope.dll -Surface Cli -Configuration Release
pwsh -NoProfile -File eng/test-complex-workflow.ps1 -CliAssembly <cli>/avascope.dll -Surface Mcp -McpAssembly <mcp>/AvaScope.Mcp.dll -McpScenarioClientAssembly <client>/AvaScope.McpScenarioClient.dll -Configuration Release
```

Each invocation runs two successful cases with alternating optional UI and one intentional failure. Required evidence covers fresh window aliases; provider and bounds-derived gestures without stored coordinates; custom actions, bounded retry, branches, fragments, typed waits and postconditions; redacted reports and fallback artifacts; owned retention and process termination with unrelated resources preserved. Run the required source/packaged surfaces. These headless cases do not establish native Retina coverage.

## Stable Release Validation

Release scope and acceptance live in the GitHub milestone and its `type:release` tracker. Validate the exact candidate across the required platform matrix and packaged CLI/MCP/provider/installers. After required checks and publication authorization, set the tracker to `status:review`. Stable publication requires all other milestone issues closed or explicitly moved. An authorized prerelease may retain documented gaps in that numeric milestone; it must not promote Latest or close unresolved stable acceptance.

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\eng\create-local-release.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File .\eng\validate-release-commit.ps1 -Version <version> -CommitSubject "Release <version>"
```

`create-local-release.ps1` restores, builds and tests Release, packs the three public libraries, packages executable ZIPs/installers, writes and verifies `artifacts/release-manifest.json`, and smoke-tests packaged sample workflows. Default executable runtimes are `win-x64`, `linux-x64`, `osx-arm64` and `osx-x64`. The provider has its own packaging/verification gate in [STANDALONE_PROVIDER.md](STANDALONE_PROVIDER.md). The local release script alone does not prove every native platform.

The version source is `Directory.Build.props`; the commit subject must be `Release <version>`. The guard requires GitHub issue read access, exactly one open release tracker in review and completed stable acceptance. It fails closed when GitHub cannot be verified. Version-specific release notes supply publication text. The [stable surface](STABLE_SURFACE.md) owns package, artifact and compatibility contracts.

For narrower packaging validation, select `-RuntimeIdentifiers <rid>`. Opt-in self-contained ZIPs use `-ExecutablePackageKind self-contained` on `create-local-release.ps1`, `verify-artifacts.ps1` and `publish-github-release.ps1` (or `-PackageKind` on `package-executables.ps1`). `-SkipTests` / `-SkipSampleSmoke` deliberately omit checks; do not report them as complete release validation.

### Current Publication Triggers

Current YAML has not yet been aligned with explicit dispatch: `CI` declares pull-request and manual triggers; `Release` reacts to `Directory.Build.props` pushes on `master`/`main` and manual dispatch. Ordinary `master` source pushes do not start `CI`. A version change without an existing remote `v<Version>` tag can start release validation and publication. Inspect triggers before touching release inputs and make version changes only for authorized releases. The pending change is tracked in [#234](https://github.com/RolandUI/AvaScope/issues/234).

Manual workflow runs validate by default; select `publish=true` only for authorized publication. Hosted NuGet publishing uses trusted publishing. The release workflow publishes libraries to nuget.org and GitHub Packages in dependency order, creates the version tag and uploads the verified release assets. An existing tag prevents automatic duplicate publication.

Before manual publication, dry-run the exact asset set:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\eng\publish-nuget.ps1 -DryRun
powershell -NoProfile -ExecutionPolicy Bypass -File .\eng\publish-github-release.ps1 -Tag v<version> -DryRun
```

Manual NuGet publication uses `AVASCOPE_NUGET_API_KEY`, `NUGET_API_KEY` or `-ApiKey`. Never store credentials in source. Omit `-DryRun` only after publication is authorized. Verify generated artifacts remain ignored, and apply the [publication policy](../AGENTS.md#github-information-policy) before uploading evidence.

macOS assets are framework-dependent and unsigned/unnotarized; the [installation guide](USER_GUIDE.md#install-from-release-artifacts) owns checksum, Gatekeeper and execution-permission instructions. Hosted Apple Silicon execution does not establish Intel execution. Keep missing platform evidence explicit.
