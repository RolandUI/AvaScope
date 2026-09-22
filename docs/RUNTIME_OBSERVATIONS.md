# Coordinated runtime observations

`avascope observe --request observation.json` and MCP `observe(request)` collect context from one explicitly selected local session. Existing tree, inspection, diagnostics and screenshot tools remain available.

```json
{
  "sessionId": "session-id-from-attach",
  "topLevelIds": ["topLevel:id-from-list"],
  "maxDepth": 4,
  "maxNodes": 40,
  "includeDiagnostics": true,
  "includeScreenshot": false,
  "maxInlineBytes": 32768
}
```

An observation includes a correlation `requestId`, unique `observationId`, session capability revision, start/completion timestamps and selected windows. Each window contains active state and actual backend, object generation, focused node identity, a flat visual fragment with parent/depth information, bounds in top-level DIPs, visible/enabled/rendered/actionable state, available actions and optional public Avalonia validation diagnostics. Generations identify object lifetimes; they are not state-change counters.

`parts` distinguishes available, partial, outside-sample, missing, excluded and unrequested data. Public validation diagnostics do not claim comprehensive application logs or all binding diagnostics. A closed target or failed screenshot retains the other completed parts with structured diagnostics. CLI/MCP transport success means an observation was returned; inspect per-part availability before acting.

## Collection and scope

The bridge coordinates tree, window and focus reads in one UI-dispatcher pass. Optional bounded render fences/screenshots follow; a second dispatcher pass compares the sampled projection. `changedDuringCollection` and `consistency` report detected changes or an unavailable comparison. `no_sampled_change_detected` is limited to the sampled properties and targets, including their redacted projection. It is never a promise of atomic screenshot/tree consistency, global idleness or a native desktop screenshot. The screenshot's provenance and readiness record describe its actual route.

With no `topLevelIds`, the request samples up to four windows in the selected session. Use exact `topLevelTitle`, `activeOnly`, or explicit ids to narrow it. `rootNodeId` requires one explicit top-level id and selects a visual subtree. Missing focus is distinguished from focus outside that subtree. Policy exclusions remove matching nodes and their descendants, including excluded focus identities.

One observation uses one bridge IPC request, with no per-node round trips. A typical separate top-level/tree/focus inspection/capability flow requires at least four calls. The real CLI/MCP regression compares matching identities and verifies that the compact projection is smaller than that separate payload. Screenshot encoding and redaction happen inside the same public request.

## Budgets and local evidence

Defaults are 4 windows, 40 nodes total, depth 4, 32768 inline bytes and a 5000 ms collection timeout. Bounds are 1–8 windows, 1–256 nodes, depth 0–8, 4096–131072 inline bytes and 1–5000 ms. Text fields are limited to 384 characters after configured redaction. Node JSON has a shared 384 KiB transport ceiling. All cuts are reported; omitted unsampled subtrees are not claimed to be present in an artifact.

The Core applies the inline byte budget after redaction/exclusion. If necessary, it returns a smaller projection and `responseBudget.artifactPath` pointing to the complete **bounded, sanitized observation**. The budget covers the response value, excluding the adapter envelope. An explicit `outputDirectory` keeps these artifacts together; otherwise the existing response-artifact location is used.

Set `includeScreenshot: true` with an explicit `outputDirectory` to save screenshots. The bridge only renders to memory for this operation, with a 16-megapixel per-image limit and a shared 256 KiB encoded PNG limit. Larger images report an unavailable part and recommend the dedicated screenshot tool. Raw PNG bytes are used only for local IPC; Core responses and JSON artifacts contain paths, not those bytes. Cancellation/transport failures do not leave an unredacted bridge-written image.

The optional existing `policy` requires a directory below its `ownedEvidenceRoot`. Session/process authorization and the `inspect`/`screenshot` action allowlist run before collection. Core masks PNG data in memory before saving. Explicit screenshot regions are honored. If text redactions or control exclusions are configured, the image is conservatively fully masked (`screenshotMasking: full_sensitive_mask`), since bounded sampling cannot prove it found every sensitive visual. Use individual policy-aware screenshot workflows when more selective masking is appropriate. Redacted text is removed before string truncation, then every returned part and artifact is sanitized again by Core.

Tests cover stable/focused UI, available actions, a rendering-triggered concurrent text change, hidden/missing windows, screenshot failure, small byte budgets, excluded subtrees/focus, long redacted strings, masked image pixels, rejected session authorization and actual CLI/MCP process parity.
