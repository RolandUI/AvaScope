# AvaScope Troubleshooting

Use this when an agent workflow fails and the next action is not obvious from the structured result. Prefer the exact error code, `nextAction`, artifact path, and session id returned by the CLI or MCP response.

## MCP Clients

- Rider Copilot and other strict MCP clients require structured responses to match each advertised output schema. AvaScope 1.1.4 and later always serialize `success`, `value`, and `error`, using an explicit JSON `null` for the inactive result branch. Upgrade the complete CLI/MCP installation and reconnect the stdio server if an older installation fails tool calls with JSON-RPC `-32602` and a missing `value` or `error` field.

## Attach And Bridge Sessions

- `bridge_session_not_found`: no active local manifest matched the selected process, session, process name, or manifest path. Run `avascope diagnostics --manifest-dir <dir>`, then retry with `--session` or `--manifest`.
- `multiple_bridge_sessions`: the selection was ambiguous. Retry with an explicit `--session`, `--process`, or `--manifest`; do not let an agent guess between active apps.
- `bridge_ipc_unavailable`: the process is stale, the named pipe is gone, or the app stopped responding. Run `cleanup-bridge-sessions`, restart the bridge-enabled app, and attach again.
- `bridge_protocol_incompatible`: the app and CLI/MCP use incompatible AvaScope protocol major versions. Align package versions in the app and the local tool.
- If the sample app does not expose a bridge session, confirm it was launched with `AVASCOPE_SAMPLE_BRIDGE=1`.

## Preview Rendering

- Preview readiness or build failures come back as structured preview errors. Check `details.phase`, `projectPath`, `viewPath`, and the suggested `nextAction`.
- If `AvaScope.PreviewHost.dll` is missing from a packaged tool directory, rerun `eng\package-executables.ps1` or use a release ZIP that contains CLI, MCP, and PreviewHost together.
- If a preview session reload fails after a source edit, inspect `list-preview-sessions` and `diagnostics`; close the failed session and recreate it if the stored request is stale.
- Repeated preview sessions are one-shot isolated child-process renders. `PersistentHostEnabled=false` is expected behavior, not a failure.

## Runtime Mutation

- `runtime_mutation_target_stale`: the node or top-level target no longer matches the current runtime tree. Refresh `visual-tree`, `logical-tree`, or `find-nodes`, then retry with the returned target context.
- `runtime_mutation_non_local_session`: the mutation target came from a different session or non-local manifest. Attach to the intended local bridge session and use its current tree result.
- Unsupported property/value errors mean the current safe mutation set rejected the request. Use `capabilities`, `mutate-node --operation no_op`, or `mutation-review` to inspect supported operations.
- After an experiment, run `mutation-review`, then `reset_mutation` for specific ids or `reset_all` for the selected session before closing the app if you need a clean runtime state.

## Reports And Visual Regression

- `preview_baseline_failed`: at least one baseline render or diff failed. Open `baseline-report.html` first, then inspect `baseline-report.json`, `baseline-junit.xml`, or `baseline.sarif.json`.
- Changed baselines should fail CI but still upload artifacts. The GitHub Actions example uses `if: always()` so reviewers can inspect current and diff images.
- Do not update committed baselines from CI. Refresh baselines locally or through a reviewed workflow.
- If report paths are missing, confirm `--report`, `--report-pack`, `--out-dir`, and `--diff-dir` point to writable local directories.

## Packages And Release Artifacts

- Framework-dependent ZIPs require a compatible local .NET runtime. Run `avascope doctor` from the extracted ZIP before using preview or MCP commands.
- Release ZIPs must include `avascope`, `AvaScope.Mcp`, and `AvaScope.PreviewHost` in one directory. Do not split those binaries across unrelated folders.
- Validate local release artifacts with `eng\create-local-release.ps1`, `eng\verify-artifacts.ps1`, and `release-manifest.json`.
- On macOS, use `osx-arm64` for Apple Silicon and `osx-x64` for Intel. The artifacts are unsigned and unnotarized. Verify SHA-256 first; if Gatekeeper reports quarantine, use `xattr -d com.apple.quarantine <verified-installer>` or Privacy & Security > Open Anyway. Do not remove quarantine broadly. If corporate MDM or administrator policy still blocks execution, report that external policy boundary instead of attempting a bypass.
- The macOS installer never edits shell profiles. If `avascope` is not found after install, invoke `~/.local/bin/avascope` directly or add `~/.local/bin` to the user's `PATH`.
- NuGet publishing and GitHub Release publication are handled only by the release workflow. Normal visual-regression or sample workflows must not require `NUGET_API_KEY`.
