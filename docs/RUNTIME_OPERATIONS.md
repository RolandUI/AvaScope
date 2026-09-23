# App-reported operations

Discover `runtime.operations`, then inspect an allowlisted custom action's
`supportsOperations` and `supportsCancellation`. These declarations are opt-in.
An ordinary click, spinner, successful input or synchronous custom action does
not imply an operation contract or application completion.

The host can extend its existing custom action:

```csharp
runtime.RegisterCustomAction(importButton, new CustomActionRegistration(
    "import", context =>
    {
        var operation = context.BeginOperation();
        _ = ImportAsync(operation);
        return CustomActionOutcome.Succeeded("Import accepted; observe the operation.");
    }, supportsOperations: true, supportsCancellation: true));

async Task ImportAsync(RuntimeOperationHandle operation)
{
    try
    {
        operation.ReportProgress(0, "Starting import");
        await ImportRecordsAsync(operation.CancellationToken);
        operation.Complete(new Dictionary<string, string> { ["records"] = "10" });
    }
    catch (OperationCanceledException) { operation.ConfirmCancelled(); }
    catch (Exception) { operation.Fail("import_failed", "The import failed."); }
}
```

`BeginOperation` is available only during the synchronous, authorized handler
of a declared operation action. Repeated calls in that handler return the same
handle. Start application work only after obtaining it: capacity rejection must
not start untracked work. Reporting methods are thread-safe and never access
Avalonia controls. Host code still dispatches its own UI updates to the UI thread.
See `demo_import` in the opt-in Getting Started sample for a cancellable in-memory
import with progress.

The custom-action response (also retained in workflow step/idempotency evidence)
contains `operation`: its id, originating request/action/target, owner, state,
progress, result, error and timestamps. It may already be terminal when returned.
If the handler throws after starting work, the response preserves that operation;
the handler error does not prove the asynchronous work stopped.

Use CLI `avascope operation --request operation.json` or MCP `operation` with a
`request` object:

```json
{"sessionId":"observed-session","operationId":"observed-operation-id","action":"wait","timeoutMs":3000}
```

`status` returns immediately. `wait` waits for **any terminal outcome**, including
failure or cancellation; inspect `operation.status` and `operation.error`.
A timeout returns `runtime_operation_wait_timeout` plus the last snapshot.
Neither timeout nor a disconnected observer cancels or repeats application work.
Reconnect to the same session and query the known id. Loss of the starting action's
response leaves dispatch uncertain; use existing workflow idempotency where
appropriate. Never resubmit a new action merely because its id/result is missing.

States are `accepted`, `running`, `completed`, `failed` and `cancelled`.
Only the host declares these transitions; the first terminal report wins.
Progress is optional and finite in 0..1. Messages allow 512 characters; results
allow 16 string fields with keys up to 64 and values up to 512 characters. Error
codes allow 128 characters and error messages 512. No result contains an inferred
claim about the underlying business transaction.

`cancel` requires declared support and existing session-control authorization.
For work started under a lease, it also requires a current lease with the same
originating owner/run identity; a different run cannot cancel it after acquiring
the session. This uses AvaScope's coordination identity, not authentication
against arbitrary code under the same OS user. A renewed/reacquired lease for
the original run can continue managing its operation. Destructive actions require
explicit `allowDestructive: true` for cancellation too. An evidence policy must
authorize the original custom action; observation requires `inspect`. Session/PID
restrictions and output redaction apply as for other runtime requests.

Accepted cancellation sets `cancellationRequested`, signals the host token once,
and returns without waiting for app callbacks. It does not set `cancelled`.
The host must confirm cancellation; completion may win a race. A throwing callback
is reported separately and leaves completion unknown. Callback scheduling uses
the public [.NET CancelAsync contract](https://learn.microsoft.com/en-us/dotnet/api/system.threading.cancellationtokensource.cancelasync?view=net-10.0).

Limits are 32 active operations and 128 total entries per bridge. Terminal entries
expire after ten minutes and the oldest terminal entry may be evicted earlier at
capacity. Status reads/new operations prune expired entries; storage remains
bounded between calls. The bridge retains no target-control reference for the
operation. Closing/restarting the bridge removes its entries and wakes observers;
old ids cannot address new work. Closing the observer does not authorize cancelling
host work, which remains the application's responsibility. Results are not durable
across an app restart and no production/background attach mechanism is added.

Tests cover immediate/delayed completion, progress, structured failure, handler
failure after starting, repeated and raced cancellation, unsupported/foreign-run
cancellation, wait cancellation, reconnect, restart, capacity eviction, shutdown,
policy redaction and actual CLI/MCP transport. This contract is platform-neutral;
it requires app integration and makes no native input or desktop capture claim.
