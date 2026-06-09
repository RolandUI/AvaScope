# AvaScope Release Plan

AvaScope development is release-based from `2026-06-09` onward. Each new version must have an explicit release target before implementation starts, and the version bump is the final release commit after the target scope is complete.

## Release Workflow

1. Define the next release target in this file and mirror the active slice in `docs/DEVELOPMENT_PLAN.md`.
2. Lock the intended release scope before implementation. Scope changes are allowed only when they are recorded here before the release commit.
3. Complete each release milestone as a vertical slice with tests or an explicit validation note.
4. Move the release target to `Release Candidate` only after every in-scope milestone is `Done` and the release gate passes.
5. Make the release commit by increasing `Directory.Build.props` `<Version>` to the target version and committing with subject `Release <version>`.
6. Push the release commit to `master`. The GitHub `Release` workflow validates, publishes packages/assets when credentials are available, and creates the matching `v<version>` tag.

The release commit must not include unfinished feature work. It should contain only the version bump and release-readiness metadata required to publish the already validated scope.

## Release States

- `Planned`: scope is defined, implementation has not started.
- `In Progress`: at least one release milestone is actively being implemented.
- `Release Candidate`: all release milestones are complete and the local release gate passed; version bump may be committed.
- `Released`: the matching GitHub tag and release assets exist.
- `Deferred`: target was intentionally stopped or moved to a later version.

## Release Gate

Before a target can move to `Release Candidate`, run the release validation commands from `docs/VALIDATION.md`, including:

```powershell
dotnet build AvaScope.slnx
dotnet test AvaScope.slnx --no-build
powershell -NoProfile -ExecutionPolicy Bypass -File .\eng\create-local-release.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File .\eng\publish-github-release.ps1 -Tag v<version> -DryRun
git diff --check
```

If the release includes public workflow or packaging changes, also validate the packaged CLI paths documented in `docs/AGENT_WORKFLOW.md`.

## Current Release Target

- Release: `v0.2.0`
- Target Version: `0.2.0`
- Release State: `In Progress`
- Scope Lock: `2026-06-09`
- Release Commit: pending until every `v0.2.0` milestone is `Done` and the release gate passes.
- Previous Release: `v0.1.0`

### v0.2.0 Release Goals

The `v0.2.0` release target is focused on making AvaScope more reliable for repeated agent workflows against local Avalonia projects while preserving the current local-only safety boundary.

1. `RG-0.2.0-1 Runtime Target Handoff`: a node found through tree or search output can be carried into follow-up runtime commands without guessing which id, tree kind, or top-level context is required.
   Success signal: `find-nodes`, tree, `inspect-node`, `screenshot`, and `input` workflows expose or accept consistent target context, and stale or mismatched references return structured actionable errors.
2. `RG-0.2.0-2 Preview Failure Triage`: preview failures distinguish local environment readiness, project build failures, and render/runtime failures before agents retry commands.
   Success signal: CLI/MCP diagnostics and preview responses report bounded readiness issues for missing SDK/build/host prerequisites where reliable signals are available.
3. `RG-0.2.0-3 Live Preview Lifecycle`: live preview behavior has a concrete next step after unchanged-input skipping.
   Success signal: either a small validated lifecycle improvement ships, or persistent preview host processes are explicitly deferred with close, TTL, crash, and cleanup semantics documented.
4. `RG-0.2.0-4 Visual Regression CI Handoff`: visual baseline checks are ready to be consumed by CI without changing local baseline command behavior.
   Success signal: a documented workflow or helper shows how to upload the JSON report, current image, and diff image artifacts produced by `baseline-check --report`.
5. `RG-0.2.0-5 Guarded Release`: `v0.2.0` ships only after the declared goals are complete or explicitly deferred.
   Success signal: audits are refreshed, the full release gate passes, `Directory.Build.props` is bumped to `0.2.0` in a `Release 0.2.0` commit, and the guarded release workflow publishes the matching version.

### v0.2.0 Milestone Map

- `R0.2.0-M1 Runtime Workflow Hardening` delivers `RG-0.2.0-1`.
- `R0.2.0-M2 Preview Diagnostics Readiness` delivers `RG-0.2.0-2`.
- `R0.2.0-M3 Live Preview Lifecycle Decision` delivers `RG-0.2.0-3`.
- `R0.2.0-M4 Visual Regression CI Integration` delivers `RG-0.2.0-4`.
- `R0.2.0-M5 Release Candidate And Version Bump` delivers `RG-0.2.0-5`.

### Explicit Deferrals

- macOS release assets, signing, notarization, and installers remain deferred until a validation surface exists.
- Remote runtime inspection remains out of scope; bridge transport stays opt-in and local-only.
- Private Avalonia runtime hooks, CLR injection, and production remote control remain out of scope.
- Persistent preview host processes are not guaranteed for `v0.2.0` unless their lifecycle and safety semantics are validated first.
