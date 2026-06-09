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

### v0.2.0 Scope

The `v0.2.0` release target is focused on making AvaScope easier for agents to use repeatedly against real Avalonia projects while preserving the current local-only safety boundary.

1. Runtime workflow hardening: make runtime node targeting easier to carry from `find-nodes`/tree output into `inspect-node`, `screenshot`, and `input`, with clearer CLI/MCP errors for stale or mismatched nodes.
2. Preview diagnostics readiness: improve project/environment diagnostics so preview failures identify missing SDK/build/host prerequisites before agents retry commands blindly.
3. Live preview lifecycle decision: decide and implement the smallest safe improvement after unchanged-input skipping, or explicitly defer persistent preview host processes with documented close/TTL/crash semantics.
4. Visual regression CI integration: add a repository-ready workflow example that uploads baseline report/current/diff artifacts without changing local baseline command behavior.
5. Release candidate validation: refresh audits, run the full release gate, bump version to `0.2.0`, and publish through the existing release workflow.

### Explicit Deferrals

- macOS release assets, signing, notarization, and installers remain deferred until a validation surface exists.
- Remote runtime inspection remains out of scope; bridge transport stays opt-in and local-only.
- Private Avalonia runtime hooks, CLR injection, and production remote control remain out of scope.
- Persistent preview host processes are not guaranteed for `v0.2.0` unless their lifecycle and safety semantics are validated first.
