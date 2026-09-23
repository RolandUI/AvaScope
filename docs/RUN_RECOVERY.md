# Session control and interrupted run recovery

`run-scenario` assigns a random durable `runId`, independently of the workflow's
request/idempotency key. Default output paths are unique. Explicit output, test
state, launch logs, screenshots, reports and timeline paths are reserved against
overlapping active or abandoned runs. Existing filesystem links are resolved for
path comparison; changing paths or links during a run is unsupported.

Scenario builds use `dotnet build --disable-build-servers`, matching preview builds.
Persistent MSBuild nodes must not keep a completed run's captured output streams open.

The private recovery store defaults to the current user's local application-data
directory, `AvaScope/agent-runs`. Set `AVASCOPE_RUN_STORE_DIR` explicitly to relocate
it. Records link the run to its provider version/hash when supplied by a profile,
session, exact app/helper process start times, managed X11 resources and retained
artifact/data paths. Missing provider pins mean unverified/host-referenced identity,
not a claim that a particular external provider was used. Environment values,
application text, Xauthority cookies and command arguments are not copied into the
record. Unix record directories/files use user-only permissions; Windows uses the
user profile's inherited permissions. Protect an explicitly relocated store likewise.

## Control leases

`session_control` (CLI `session-control`) supports `status`, `acquire`, `renew`
and `release`. Acquisition requires an explicit owner such as a run id. TTL defaults
to 30 seconds and is bounded to 1–300 seconds. Expiry uses monotonic elapsed time.
Read-only observation remains available to other clients. Conflicting input,
mutations, custom actions, virtual-item reveal/select, picker control and session
closure return `session_control_conflict` before dispatch.

The MCP server remembers tokens it acquires for that server instance and manifest
directory. After reconnecting, explicitly renew with the prior token. CLI calls
pass `--control-token` to bridge commands. Tokens are returned only by acquisition,
renewal and explicit run resume; status/list/conflict results never expose them.
Do not put tokens in reports or shared logs. This coordinates authorized local
agents; it is not an authentication boundary against the application itself or
other code running as the same OS user.

```text
avascope session-control --session <id> --operation acquire --owner <run>
avascope input --session <id> --top-level <window> --action invoke --target-node <node> --control-token <returned-token>
avascope session-control --session <id> --operation release --control-token <returned-token>
```

Each bridge admits at most one control request at a time, including legacy clients
without a lease. Legacy calls work while no lease is held, but cannot bypass a
lease held by another client. After expiry, a fresh explicit acquisition is required;
the stale token cannot silently become an unleased request. Expiry never transfers
control while a request is still executing. Requests are not automatically replayed.
In-process host picker hooks remain host-authorized operations; external CLI/MCP
picker calls go through the bridge gate.

Scenarios acquire control before picker preparation, fixtures or workflow actions.
They renew every ten seconds and cancel further work if renewal fails. Cleanup
keeps the token until fixture cleanup and owned process/session shutdown finish,
then releases it for retained apps. Older bridges without `session_control` fail
explicitly; no uncoordinated scenario fallback is attempted.

## Recovery commands

`list_agent_runs` / `list-agent-runs` lists the newest 25 records by default,
maximum 100. The store scan is capped at 2048 records; archive completed records
explicitly when necessary. `recover_run` / `recover-run` selects one exact run id:

```text
avascope list-agent-runs
avascope recover-run --run <id> --operation inspect
avascope recover-run --run <id> --operation resume
avascope recover-run --run <id> --operation cleanup
```

The CLI accepts `--store-dir`; MCP accepts `storeDirectory`. `inspect` reports
active versus abandoned ownership, session, processes, retained paths and the
original outcome/failure stage. An OS-held file lock protects active runs and
recovery from another simultaneous owner. After a process is killed, OS handle
teardown may briefly leave the record active; inspect again before recovery.

`resume` only reacquires/renews the original live session. It returns a token for
subsequent CLI control; an MCP reconnect should pass it to `session_control` with
operation `renew`. It never restarts the app, retries an uncertain action, replays
a workflow or repeats a fixture cleanup callback. Observe current state and choose
the next action explicitly. A conflicting owner or still-running operation blocks
recovery. A missing original session requires a new explicit run.

`cleanup` stops only recorded owned processes after comparing their exact process
start identity. Linux uses the exact `/proc/<pid>/stat` start ticks plus boot id,
because converting uptime to wall-clock time can vary between observing processes;
Windows/macOS use process start time. Missing Linux kernel identity fails closed.
Attached apps and externally supplied displays are retained. PID reuse,
changed manifests, machine mismatch and live conflicting leases never authorize
termination. If an owned app's session cannot be closed, its process and helpers
are retained with a partial result. Before-session launch failures can still clean
up their recorded exact app/helper processes.

If scenario shutdown cannot terminate its app because another client holds control,
its managed desktop and helpers are retained as well (`environment.status=retained`).
After that client releases control, recover the recorded run explicitly; shutting
down a shared-live desktop must not bypass the session conflict.

Managed X11 helpers receive graceful shutdown before bounded termination. Private
runtime-directory deletion requires its run marker, fixed temporary parent and a
bounded tree without links. Xvfb lock cleanup additionally checks its recorded PID;
external display sockets are never deleted. Unix bridge-socket recovery requires
the recorded PID-scoped path, dead original process, a `stat`-verified socket and
a refused connection. Missing `stat`, unknown ownership or a live listener retains
the resource and reports the limitation. Windows named pipes disappear when their
owning process closes. Records and evidence/test-data directories are retained.

`cleaned` describes AvaScope-owned resource cleanup, not a passed application test.
The original outcome and failure stage remain visible; host fixture/business data
is never silently retried or deleted. `partial_cleanup` retains diagnostics and
can be inspected/retried explicitly. Process creation and record persistence are
not an atomic OS transaction: if the agent dies between them, an unrecorded resource
cannot be claimed by guessing a PID. Review the retained launch/helper evidence.

The tests kill an actual CLI scenario owner, resume its lease, reject another
client, and clean up the exact app while preserving data. Separate tests cover
overlapping paths, active-run rejection, monotonic expiry while executing, stale
tokens/PIDs, marker mismatch and real CLI/MCP/bridge control conflicts. Native CI
also exercises scenarios and managed display shutdown on each supported platform.
The optional `AVASCOPE_RECOVERY_MANAGED_X11=1` test gate additionally validates
retaining and subsequently recovering Xvfb/Openbox/D-Bus after a control conflict.

Linux identity uses the kernel's documented [process start ticks](https://www.man7.org/linux/man-pages/man5/proc_pid_stat.5.html),
paired with the current boot id, rather than a tolerance around a converted timestamp.
