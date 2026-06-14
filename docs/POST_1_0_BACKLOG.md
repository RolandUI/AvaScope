# AvaScope Post-1.0 Backlog And Deferral Audit

This document records the `R1.0.0-M5 Post-1.0 Backlog And Deferral Audit` slice for GitHub issue #38.

## Audit Result

- Audit date: `2026-06-13`.
- GitHub milestone audited: `v1.0.0`.
- Open `priority:p1` issues: none.
- Open `priority:p0` issues: #33 `Release v1.0.0` and #39 `R1.0.0-M6 Stable Release Commit And Publication`.
- Release-blocking conclusion: no hidden product or implementation blocker remains. The only open P0 items are the release tracker and the final release/publish slice.
- Feature-request intake audit: all `docs/feature-requests/FEAT-*.md` tickets are marked `Status: Implemented`; this directory is backlog intake, not the active release queue.

Deferred means non-blocking for `v1.0.0`. A deferred item can only move back into a release after it has a new GitHub issue, acceptance criteria, validation plan, and an explicit release target.

## Accepted v1.0.0 Scope

The stable `v1.0.0` release accepts these boundaries as final for the release:

- AvaScope is a local-first agent control plane exposed through CLI and MCP.
- Runtime inspection/control requires an opt-in bridge in the target app.
- Runtime bridge transport stays local-only through manifests and local named pipes.
- Runtime mutation is temporary, bounded, reversible, and local-only.
- Preview rendering runs user project code only inside isolated `AvaScope.PreviewHost` child processes.
- Source-aware mutation review is advisory and never edits project files automatically.
- Release assets are NuGet packages plus framework-dependent executable ZIPs by default.

## Deferred Or Rejected For v1.0.0

| Area | Item | Decision | Reason | Priority | Release-blocking |
| --- | --- | --- | --- | --- | --- |
| Runtime security | Remote inspection/control | Deferred | Needs authentication, authorization, transport hardening, audit logging, and a separate threat model. | P2 | No |
| Runtime attach | No-code attach | Deferred | The stable foundation is explicit opt-in bridge activation; attaching without app participation would push toward injection/private runtime hooks. | P2 | No |
| Runtime attach | Process injection and CLR profiling | Rejected for v1 defaults | Too invasive for the local-first safety boundary and not needed for the stable bridge workflow. | P3 | No |
| Avalonia internals | Private runtime hooks and private designer APIs | Rejected for v1 defaults | Stable behavior must rely on public Avalonia APIs and explicit `unknown`/`not_available` provenance when private data is unavailable. | P3 | No |
| Runtime actions | Destructive runtime actions | Rejected for v1 defaults | The stable tool set supports narrow input and reversible mutations, not destructive app operations. | P2 | No |
| Source changes | Automatic source editing | Deferred | Current source suggestions are advisory; automatic patching needs explicit review, rollback, and ownership semantics. | P2 | No |
| Runtime updates | Runtime hot reload | Deferred | Runtime `reload` currently returns an explicit unsupported result; real hot reload needs app cooperation and lifecycle semantics. | P2 | No |
| Runtime input | Drag/drop, IME-level text input, hardware-like key repeat, richer pointer buttons | Deferred | These need deterministic coverage through public Avalonia APIs before becoming stable automation surfaces. | P2 | No |
| Preview lifecycle | Persistent preview host processes | Deferred | One-shot isolated child processes are safer; persistent hosts need close, TTL, crash recovery, cancellation, and cleanup semantics. | P2 | No |
| Preview startup | Full app startup/lifetime execution | Deferred | Running `OnFrameworkInitializationCompleted`, app services, and project windows needs a separate execution and security model. | P2 | No |
| Preview design data | JSON object injection, dependency injection, remote design data, long-lived design-data state | Deferred | Current stable design data uses a project-owned public parameterless type; broader inputs need lifecycle and trust boundaries. | P3 | No |
| Diagnostics | Deeper private binding-engine, resource-chain, and style telemetry | Deferred | AvaScope reports what public APIs and source metadata can support; private telemetry is not stable. | P3 | No |
| Distribution | macOS release assets, signing, notarization, and native signed installers | Deferred | v1 ships validated NuGet packages, win/linux framework-dependent ZIPs, and a Windows per-user install workflow; native installer signing policy needs separate validation. | P3 | No |
| Distribution | Self-contained ZIPs as default release assets | Deferred | Self-contained artifacts are validated as an opt-in lane; framework-dependent ZIPs remain the default release set. | P3 | No |
| Hosted workflows | Cloud dashboards and hosted visual regression review services | Deferred | v1 provides local report packs and CI-friendly artifacts; hosted services need account, retention, privacy, and UX decisions. | P3 | No |
| Integrations | Native IDE extensions | Deferred | Stable CLI/MCP contracts are the integration substrate; native IDE work can build on them after v1. | P3 | No |

## Open Issue Audit

As of this audit, the open `v1.0.0` milestone items are:

- #33 `Release v1.0.0`: release tracker. Expected to stay open until #39 publishes the release.
- #38 `R1.0.0-M5 Post-1.0 Backlog And Deferral Audit`: this audit slice.
- #39 `R1.0.0-M6 Stable Release Commit And Publication`: final release commit and publish slice.

There are no open P1 issues. There are no open product P0 issues outside the release tracker and final publish slice.

## Release Candidate Gate

Before moving `v1.0.0` to `Release Candidate`, #38 must be closed and #39 must verify:

- all milestone implementation issues except #33 and #39 are closed;
- the release plan marks the target as `Release Candidate`;
- the local release gate passes;
- the final `Release 1.0.0` commit contains only the version bump and release-readiness metadata needed for publish.
