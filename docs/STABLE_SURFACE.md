# AvaScope Stable Surface

This document freezes the public surfaces intended to remain stable for the `v1.0.0` release. It defines what agents and developers can rely on, what can grow additively, and what remains internal or intentionally non-stable.

## Stability Levels

- Stable: name, JSON property, package identity, command name, tool name, exit-code class, or artifact name remains compatible within the same major version.
- Additive stable: new JSON properties, capability ids, command options, diagnostics, assets, or metadata may be added without a major version bump; clients must ignore unknown JSON properties.
- Operationally stable: behavior is part of the supported workflow, but timing, ordering, diagnostics wording, or local paths may vary.
- Non-stable: implementation detail. Do not bind clients to it unless another document explicitly promotes it to stable.

## Public Packages

The stable public NuGet packages are:

- `AvaScope.Protocol`: transport-neutral DTOs, protocol constants, JSON result shapes, capability discovery, and stable protocol contract types.
- `AvaScope.Core`: reusable local bridge client, preview host client, preview-session, diagnostics, baseline, report, and artifact workflow logic used by CLI and MCP adapters.
- `AvaScope.Bridge`: opt-in local-only runtime inspection bridge for Avalonia applications.

The package ids, package dependency direction, and SemVer major compatibility rules are stable. `AvaScope.Protocol` remains the contract boundary; `AvaScope.Core` and `AvaScope.Bridge` can add public members in minor releases, but removals or incompatible behavior changes require a major release or a compatibility alias.

These executable or test assemblies are intentionally not NuGet package surfaces:

- `AvaScope.Cli`
- `AvaScope.Mcp`
- `AvaScope.PreviewHost`
- `AvaScope.Tests`

They are distributed through executable ZIPs and source, not as package APIs.

## Protocol Contracts

The stable service name is `avascope`. The protocol version uses `AvaScopeProtocol.CurrentVersion`; breaking protocol changes require `protocolVersion.major` to increase.
The MCP initialization handshake reports `serverInfo.name` as `avascope`.

Stable protocol primitives:

- `ToolResult<T>` preserves `success`, `value`, and `error`.
- `ProtocolError` preserves `code`, `message`, and optional `details`.
- `SessionId` serializes as a JSON string.
- `AvaScopeCapabilitiesResponse` exposes `serviceName`, `protocolVersion`, `compatibilityPolicy`, `capabilities`, `tools`, `runtimeMutationCapabilities`, and `diagnostics`.
- `capability_not_supported` is the stable error code for unsupported required capabilities.

Compatibility rules:

- Clients must call `capabilities` when they require a specific newer workflow.
- Clients must key feature checks on `capabilities[].id` and `tools[]`, not package-version guessing.
- Clients must ignore unknown JSON properties and unknown capability ids.
- New optional properties, diagnostics, report paths, agent review fields, capabilities, CLI options, and MCP parameters are minor-version compatible.
- Removed properties, renamed commands/tools, changed required parameters, changed error success/failure semantics, or removed artifact names require a major release or a compatibility alias.

Stable DTO areas covered by protocol contract tests include health, sessions, diagnostics, runtime targets, trees, node inspection, search, input, runtime mutation, runtime mutation evidence/review, preview rendering, preview sessions, preview watch lifecycle, screenshots, diffs, region assertions, baseline manifests, baseline reports, capability discovery, and agent review surfaces.

## CLI Commands

The stable CLI command name set is:

- `capabilities`
- `mcp`
- `doctor`
- `diagnostics`
- `attach`
- `launch-app`
- `list-top-levels`
- `visual-tree`
- `logical-tree`
- `inspect-node`
- `find-nodes`
- `audit-ui`
- `input`
- `mutate-node`
- `mutate-node-evidence`
- `mutation-review`
- `close-session`
- `screenshot`
- `preview`
- `preview-animation`
- `create-preview-session`
- `list-preview-sessions`
- `reload-preview-session`
- `reload`
- `close-preview-session`
- `watch-preview-session`
- `preview-viewer`
- `baseline-create`
- `baseline-check`
- `diff`
- `assert-region`
- `cleanup`
- `cleanup-bridge-sessions`

CLI compatibility rules:

- Existing command names stay valid within major version `1`.
- Existing option names and meanings stay compatible; new optional flags may be added.
- Removing an option, making an optional option required, or changing a command's structured result shape requires a major release or a compatibility alias.
- JSON stdout is the stable machine contract for commands that return a `ToolResult<T>`.
- Human-readable usage text and diagnostic wording may change; stable clients should read JSON fields and error codes.

## MCP Tools

The stable MCP tool name set is:

- `health`
- `capabilities`
- `list_sessions`
- `attach_to_app`
- `launch_app`
- `list_top_levels`
- `screenshot`
- `assert_region`
- `visual_tree`
- `logical_tree`
- `inspect_node`
- `find_nodes`
- `audit_ui`
- `input`
- `mutate_node`
- `mutate_node_evidence`
- `mutation_review`
- `close_session`
- `diagnostics`
- `preview_axaml`
- `baseline_check`
- `preview_axaml_multi`
- `preview_axaml_animation`
- `cleanup`
- `cleanup_bridge_sessions`
- `create_preview_session`
- `list_preview_sessions`
- `preview_viewer`
- `close_preview_session`
- `reload`

MCP compatibility rules:

- MCP stays a thin stdio adapter over reusable Core and Protocol contracts.
- Tool names stay valid within major version `1`.
- Tool schemas may grow optional parameters and optional result fields.
- Required parameter removals, required parameter additions, incompatible result changes, or renamed tools require a major release or compatibility alias.

## Exit Codes

The stable CLI exit-code classes for `v1.0.0` are:

- `0`: command succeeded.
- `1`: command ran but returned a structured workflow failure, validation failure, unavailable dependency, changed visual baseline, failed region assertion, or tool error.
- `2`: command-line arguments were invalid or the command was unknown.

No finer-grained numeric exit-code taxonomy is stable in `v1.0.0`. Stable clients should inspect `ToolResult<T>.error.code` and `error.details` when a command writes JSON.

## Artifact Names

Stable release artifact names:

- `AvaScope.Protocol.<version>.nupkg`
- `AvaScope.Core.<version>.nupkg`
- `AvaScope.Bridge.<version>.nupkg`
- `avascope-win-x64-framework-dependent.zip`
- `avascope-linux-x64-framework-dependent.zip`
- `release-manifest.json`

Stable local executable artifact directory and ZIP pattern:

- `avascope-<rid>-<packageKind>`
- `avascope-<rid>-<packageKind>.zip`

Stable baseline report-pack filenames:

- `baseline-report.json`
- `baseline-report.html`
- `baseline-junit.xml`
- `baseline.sarif.json`

Stable runtime mutation evidence suffixes:

- `-before.png`
- `-after.png`
- `-before-visual-tree.json`
- `-after-visual-tree.json`
- `-diff.png`
- `-review.html`

The caller-controlled request id prefix, absolute output path, image contents, SHA-256 hash, and file timestamp are not stable.

## Release Workflow

Stable release workflow behavior:

- `Directory.Build.props` `<Version>` is the release version source.
- The GitHub Release tag is `v<Version>`.
- The release commit subject is `Release <Version>`.
- `docs/RELEASE_PLAN.md` must declare the same target version in `Release Candidate` state before the release commit can publish.
- `eng/create-local-release.ps1` is the local release gate.
- `eng/verify-artifacts.ps1` verifies package and executable artifact coverage and writes `artifacts/release-manifest.json`.
- `eng/publish-github-release.ps1 -Tag v<Version> -DryRun` verifies the GitHub Release asset set without publishing.
- The development CI workflow is manual-only; release preparation relies on local validation unless a GitHub Actions run is explicitly requested.
- Automatic GitHub Release publishing is scoped to pushes that change `Directory.Build.props` or to manual workflow dispatch.
- The GitHub Release workflow no-ops when the remote tag already exists for the current version.

## Non-Stable Surfaces

The following are intentionally non-stable unless promoted in a future major release:

- Internal classes, private methods, and constructor shapes not documented here.
- `AvaScope.PreviewHost` child-process request temp directories and internal process protocol.
- Generated temp-store layout for bridge manifests and preview sessions.
- Console formatting outside JSON stdout.
- Diagnostic message wording, diagnostic ordering, inventory ordering, and timing-sensitive lifecycle ordering.
- Exact screenshots, generated HTML styling, local absolute paths, timestamps, hashes, and process ids.
- Sample app UI details.
- Avalonia private behavior or implementation details that AvaScope reports as `unknown`, `not_available`, or advisory diagnostics.

## Migration Guidance

Minor releases may add capabilities, tools metadata, optional fields, optional CLI flags, optional MCP parameters, diagnostics, and report assets. Major releases are required for removals, renamed stable commands/tools, changed required parameters, incompatible JSON changes, or changed success/failure semantics.

When practical, deprecate before removal, keep aliases for at least one minor release, and document the replacement in this file plus `docs/USER_GUIDE.md`. Agents should request required capability ids before using optional workflows and fail closed on `capability_not_supported`.

## Accepted Compatibility Risks

- `AvaScope.Core` and `AvaScope.Bridge` expose some public types that exist to support package consumers and tests; not every low-level constructor is an endorsed extension point.
- Preview diagnostics depend on public Avalonia 12 behavior and source metadata, so diagnostics may become more precise without a breaking version change.
- Runtime mutation support is stable as a temporary, local, reversible workflow. Supported properties and diagnostics may grow additively; unsupported private runtime hooks remain out of scope.
