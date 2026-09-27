# AvaScope Development Plan

GitHub milestone v1.5.1 and issue #174 are the current backlog and acceptance source. #174 is the sole in-progress issue. This file contains the latest handoff; older per-run history is intentionally omitted.

## Working policy (owner instruction, 2026-09-27)

- Work directly on `master`. It was fast-forwarded and pushed to d7562ed24b70ba026b271b608ccbff5ac2c21781; all prior stabilization commits are included, without rewritten history.
- One reusable local QA environment: `D:/AvaScope-QA-active/current`, below 8 GiB. Reuse tools/client/Direct/standalone/provider binaries across cases. One native app at a time; stop owned processes before replacing binaries.
- No per-case binary snapshots, local CI history or secondary archive. Discard successful raw case outputs after verifying response, independent state and relevant pixels. Retain only minimal evidence for unresolved issues or the current pending batch; delete it when the issue/batch closes. GitHub, commits and regressions retain the lasting result.
- Focused local checks per fix; one full CI gate per coherent batch. Continue independent local work during CI. No version bump, release or publication.
- Send a concise Hungarian Gmail report to the user's own account after each completed batch. Include defect counts, changes, validation and remaining gaps; deduplicate against sent mail. Last sent report: 1a0e365c662c7929. The current repair batch is still open.

## Current validation and next work

- Shared full CI 36335386524 runs on exact d7562ed: https://github.com/RolandUI/AvaScope/actions/runs/36335386524 . Build/Test/Pack and all three native QA labs passed at the latest check; macOS Runtime and Linux Installer are still running. Do not dispatch a replacement. Inspect completed artifacts one at a time, record results, then remove unpacked data.
- #228 (margin diagnostics) and #229 (test-helper failure evidence/cleanup) are locally fixed in review pending this shared gate. Feature #230 adds held operations to test actual cancellation; 43 local fixture regressions and 42 native public calls across Direct/standalone passed, owned apps stopped. #230 also awaits the shared gate.
- #231 records an original Direct publish timeout at the unchanged 300-second deadline. A separate resumed publish succeeded; the original cause remains unknown. Do not close it on that basis.
- The CLI lifecycle exploration additionally observed 17 public calls: launch, native Win32 discovery/rendered images, toggle with independent journal, runtime reload refusal, invalid close arguments, CLI `mcp` initialization/75-tool catalog/attachment, close-only preserving the app, later owned termination and idempotent already-exited outcome. Two rendered images were reviewed. The app and recorded callers exited. A 1500ms missing-executable probe expired in helper readiness and does not prove executable-resolution-stage handling.
- Remaining explicit CLI boundaries: create-preview-session, reload-preview-session, preview-animation and baseline-create. The earlier prepared preview directory was discarded during the storage redesign before executing those cases. Continue using the shared binaries and a small replaceable case directory, not a new runtime snapshot.
- Inventory remains 65 defects: 49 closed, 16 open. Two fixes await the batch gate; eleven original timing/IPC/build/lock causes remain unresolved; three platform/API limitations remain. #230 is a feature, not a defect.
- No independent local OS capture/input or Retina pass: native computer-use remains unavailable after failed recovery; do not retry through a substitute OS automation route. AvaScope-rendered images plus independent app state remain useful but are not independent native pixels. #157/#161 and #201 animation-clock limitations remain explicit.

## Storage transition

Historical `artifacts/agent-qa`, `D:/AvaScope-QA-active` run copies and `D:/AvaScope-QA-archive` contents are being removed under the owner's instruction. Only `D:/AvaScope-QA-active/current` remains selected. Its `binaries` are the existing current set moved and checked against retained hashes; this is not a fresh build. Compact pending-batch records live under `pending-batch`; cleanup result is `reclaimed.json`.

Older issue/report links to local artifacts may intentionally no longer resolve. Do not restore old snapshots or repeat validation merely to recreate an archive. The same historical results remain in issue comments and commits. The existing snapshot-oriented `eng/agent-qa.ps1` is for bounded isolated/CI runs; it still copies binaries and restricts paths to repository artifacts. Repeated local exploration uses actual CLI/MCP calls against the shared D: binaries instead.
