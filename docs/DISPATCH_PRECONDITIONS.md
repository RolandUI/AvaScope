# State preconditions for input

Discover `runtime.dispatch_preconditions`. Supply the same closed definition used
by [runtime expressions](RUNTIME_EXPRESSIONS.md) in `execution.preconditions` on
CLI/MCP `input`, or `inputExecution.preconditions` on a workflow input step.
Choose an explicit `semantic`, `synthetic` or `native` strategy.

For example, invoke an observed action only while the document identity still
matches the decision on which the action was based:

```json
{
  "strategy":"semantic",
  "preconditions": {
    "sources":[{"id":"document","selector":{"name":"DocumentIdentity"},"attribute":"text"}],
    "expression":{"kind":"eq","operands":[
      {"kind":"value","source":"document"},
      {"kind":"literal","literal":"document-42"}
    ]}
  }
}
```

Save this as `guard.json` for CLI
`avascope input --session <session> --top-level <window> --action invoke --target-node <node> --execution guard.json`.
For MCP, pass it as the `execution` object. Sources may observe supported values,
selection flags, AutomationIds or an application-published identity control;
arbitrary view-model reflection and code evaluation are unavailable. Use stable
selectors and the existing workflow generation-bearing targets. Conditions on
multiple controls can use `all`, counts, comparisons and the other typed operators.

Checks run on the UI thread after normal preparation, then again immediately
before the first event/provider/property invocation. The final check follows focus
or provider preparation that may call application code. Target attachment and
availability are checked again after expression evaluation; pointer geometry and
keyboard/native ownership are revalidated when applicable. False, missing,
redacted, partial or changing observations reject dispatch.

Successful input includes the full typed check in `preconditions`, along with
`metadata.preconditionPhase`, `metadata.preconditionsStatus`, `metadata.dispatched`
and `metadata.dispatchOutcome`. A guard rejection includes the same check as JSON
in `error.details.preconditions`, with `dispatched: "false"`. Earlier target,
validation or concurrency failures can report `not_checked`; no condition outcome
is fabricated. Validation-only input also checks the expression but dispatches
nothing. A validation result does not reserve or guarantee future application state.

`dispatched: "true"` means AvaScope attempted the selected input/provider/property
invocation. If that invocation throws after changing state, the outcome remains
`unknown_after_dispatch`; this is not a rollback. A lost transport response returns
`dispatched: "unknown"`. Neither case automatically dispatches again. Existing
workflow idempotency stores the original result and precondition evidence; replay
of the same key does not re-evaluate or re-execute a committed operation. A new
decision needs fresh observations and, when appropriate, a new key.

The guard covers the **first dispatch of one requested input operation**. A compound
click/drag/key sequence has subsequent events and paired cleanup; the guard is not
a transaction over them. Input target/focus checks and cleanup still apply during
the sequence. Preparation and public observation callbacks may execute app code.
An asynchronous handler or external service can change state after the check;
this API cannot lock it. Use a separate typed `verify` condition for eventual
completion. Custom actions, mutations, form/table operations and legacy gesture
options do not silently acquire this guard: it applies to supported explicit input.

Optional `preconditionPolicy` must authorize both inspection and the corresponding
workflow action (`type_text` for keyboard text). Its session/process restrictions
are enforced by the client, and both operands and result/error evidence are
sanitized. A workflow's evidence policy overrides a per-step guard policy, so a
step cannot weaken its enclosing policy. Normal expression and input limits,
session-control leases, native ownership rules and generation checks remain active.

Tests cover changed identities/values, target replacement, focus callbacks changing
the decision before dispatch, read-only validation, asynchronous completion,
post-dispatch exceptions, idempotent replay, response loss without replay, policies
and actual CLI/MCP calls. The native input gate verifies rejected and accepted
guards on the validated platform routes.
