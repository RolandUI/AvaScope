# Text range editing

`avascope edit-text --request text-edit.json` and MCP `edit_text` share the
`runtime.text_editing` capability. They work on generation-pinned visual targets
from `find_nodes`/`query` in an explicitly activated local bridge session.

Read first: `{ "target": { ... }, "action": "read" }`. The `after` state contains
the complete bounded text, caret, selection endpoints, validation-error flag,
read-only flag, `AcceptsReturn`, `MaxLength`, `NewLine` and an opaque `revision`.
There is no focus change on read. Copy that revision into the next request:

```json
{
  "target": { "sessionId": "session-id", "topLevelId": "top-id", "treeKind": "visual",
    "nodeId": "node-id", "topLevelGeneration": "observed-top-generation", "nodeGeneration": "observed-node-generation" },
  "action": "replace_range",
  "requestId": "replace-config-path-1",
  "expectedRevision": "copy-the-64-character-revision-from-the-read-result",
  "start": 15,
  "end": 22,
  "text": "/new/東京"
}
```

The identity and revision above are placeholders, not reusable target values.
For `first 😀\r\npath=/old/út\nlast é`, offsets `[15,22)` select `/old/út`;
the replacement preserves both surrounding lines and their original endings.

| Action | Arguments | Result |
| --- | --- | --- |
| `read` | target, optional policy | Current state, without focus/editing |
| `select_range` | start, end, expectedRevision, requestId | Focus and exact forward selection; no replacement text |
| `replace_range` | start, end, text, expectedRevision, requestId | Replace just that range |
| `replace_selection` | text, expectedRevision, requestId | Replace the selection captured by that revision |
| `insert` | start, text, expectedRevision, requestId | Insert at an explicit position, collapsing selection |

Offsets are zero-based UTF-16 code units, start-inclusive/end-exclusive. An emoji
outside the BMP occupies two units. A boundary cannot split a surrogate pair or
CRLF. Combining-character/grapheme boundaries are not inferred. Input must be
well-formed UTF-16. Empty replacement deletes the selected range; an empty range
plus empty text is a no-op and never sends Delete to the following character.
Existing line endings are preserved; replacement text uses the control's public
input path without AvaScope newline normalization. Control filtering, bindings,
undo and validation remain active. The returned state is authoritative if the
control rejects, normalizes or transforms input.

The revision fingerprints text, caret, both selection endpoints, editing
constraints/validation state and target generations. It identifies an observed
state, not a monotonic edit history: returning to exactly the same state produces
the same revision. Stale state is rejected before dispatch, including changes
from focus or action-context callbacks. Range preparation uses public current
values to preserve property bindings. Replacements use routed TextInput or a
paired Delete, never direct Text assignment or the system clipboard.

Verification observes the immediate public result in the dispatcher turn. It
checks the full text, collapsed selection, caret and absence of validation errors.
It does not establish completion of later async business validation; use runtime
waits/traces for that. Selection/focus preparation can remain after a rejection;
`preparationPerformed` and `dispatchedOperations` distinguish this from text input.
Exceptions after dispatch return `uncertain` with whatever safe state is readable.
No rollback or automatic action retry occurs.

A cooperative two-second deadline stops preparation before another input can be
dispatched. A synchronous application callback can overrun that deadline. The
bridge still reads its bounded final state, preserving all identity and privacy
checks, and retains the result for replay without dispatching the input again.

Reads and edits accept at most 8192 text units, 4096 replacement units, 16 NewLine
units and 64 KiB requests. Oversized text is refused, never truncated into editable
ranges. Each session retains 128 edit ids and their results; an identical id and
payload retrieves the original result with `replayed: true`, even after reconnect.
A conflicting payload is refused. At capacity, new edits are refused while reads
and existing ids remain available. Session shutdown clears this ledger; a new
session cannot reuse the old generation-pinned target. Lost IPC responses are
explicitly uncertain; recover with the exact original request, then read again.

Password fields are refused even when RevealPassword is enabled. If the supplied
evidence policy protects any text value or the field/ancestor AutomationId, the
whole state (including revision and positions) is withheld. This prevents ranges
over redacted text from referring to different content. Policies require `inspect`
and, for changes including selection, `allowedDesiredStates: ["text"]`. Normal
session/PID policy authorization and the control lease apply. Read-only fields can
be inspected, but selection/edit requests fail. No secret validation messages are
included. Custom/rich editors that do not derive from TextBox return unsupported;
use an explicit app-declared custom action for those editors.

The shared path uses public Avalonia 12 APIs on Windows, Linux X11, macOS and
headless backends; it does not claim native keyboard/IME behavior. API references:
[Avalonia 12.1 TextBox](https://github.com/AvaloniaUI/Avalonia/blob/12.1.0/src/Avalonia.Controls/TextBox.cs)
(`CaretIndex`, `SelectionStart`, `SelectionEnd`, routed text input and CRLF coercion).

Validation covers multiline Unicode and combining text, mixed line endings,
surrogate/CRLF rejection, empty edits, undo, stale/reentrant state, sensitive
ancestors/passwords, unsupported/read-only/oversized controls, application
validation/rejection, uncertain callback failure, replay/capacity/shutdown,
control-lease enforcement and real CLI/MCP parity.
