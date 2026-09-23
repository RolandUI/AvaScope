using Avalonia.Threading;
using Avalonia.Automation;
using Avalonia.VisualTree;
using AvaScope.Core;
using AvaScope.Protocol;

namespace AvaScope.Bridge;

public sealed partial class AvaScopeBridgeRuntime
{
    private readonly object _operationsLock = new();
    private readonly Dictionary<string, RuntimeOperationHandle> _operations = new(StringComparer.Ordinal);
    private bool _operationsClosed;

    private RuntimeOperationHandle CreateOperation(RuntimeCustomActionRequest request, RuntimeTargetContext target,
        CustomActionRegistration registration, string? owner, Avalonia.Visual visual)
    {
        Dispatcher.UIThread.VerifyAccess();
        lock (_operationsLock)
        {
            if (_operationsClosed) throw new InvalidOperationException("Operations cannot begin on a closed bridge.");
            PruneOperations();
            if (_operations.Values.Count(operation => !operation.Snapshot.IsTerminal) >= 32)
                throw new InvalidOperationException("The bridge already tracks 32 active operations. Complete existing work before starting more.");
            if (_operations.Count >= 128)
            {
                var oldest = _operations.Values.Where(operation => operation.Snapshot.IsTerminal).MinBy(operation => operation.Snapshot.UpdatedAt)!;
                _operations.Remove(oldest.OperationId); oldest.Close();
            }
            var runtime = new WeakReference<AvaScopeBridgeRuntime>(this);
            var ancestry = visual.GetVisualAncestors().Prepend(visual).Take(129).ToArray();
            var scopeComplete = ancestry.Length is > 0 and <= 128;
            var automationIds = ancestry.Select(AutomationProperties.GetAutomationId).Where(id => id is not null).Cast<string>().ToArray();
            var handle = new RuntimeOperationHandle(SessionId, request.RequestId, request.ActionName, target, owner,
                registration.SupportsCancellation, registration.SafetyClassification, snapshot =>
                { if (runtime.TryGetTarget(out var current)) current.TraceOperation(snapshot, automationIds, scopeComplete); });
            _operations.Add(handle.OperationId, handle);
            TraceOperation(handle.Snapshot, automationIds, scopeComplete);
            return handle;
        }
    }

    private void PruneOperations()
    {
        foreach (var entry in _operations.Where(entry => entry.Value.Snapshot.RetainUntil <= DateTimeOffset.UtcNow).ToArray())
        { _operations.Remove(entry.Key); entry.Value.Close(); }
    }

    private void CloseOperations()
    {
        lock (_operationsLock)
        {
            _operationsClosed = true;
            foreach (var handle in _operations.Values) handle.Close();
            _operations.Clear();
        }
    }

    public Task<CoreResult<RuntimeOperationResponse>> OperationAsync(RuntimeOperationRequest request, CancellationToken cancellationToken = default)
        => OperationAsync(request, owner: null, cancellationToken);

    internal async Task<CoreResult<RuntimeOperationResponse>> OperationAsync(RuntimeOperationRequest request, string? owner, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (request.SessionId != SessionId) return Fail("runtime_operation_session_mismatch", "Operations belong to one exact bridge session; restart invalidates prior ids.");
        if (!request.OperationId.StartsWith(SessionId.Value + ":", StringComparison.Ordinal))
            return Fail("runtime_operation_session_mismatch", "The operation id belongs to another bridge instance or is invalid. Discover the current session; do not replay the original action.");
        RuntimeOperationHandle handle;
        lock (_operationsLock)
        {
            if (_operationsClosed) return Fail("runtime_operation_session_closed", "The bridge session is closed.");
            PruneOperations();
            if (!_operations.TryGetValue(request.OperationId, out handle!))
                return Fail("runtime_operation_unknown", "No retained operation has this id in this session. It may be unknown, expired, evicted or from an earlier app instance; do not automatically repeat its action.");
        }
        var policy = request.Policy is null ? null : new RuntimeEvidencePolicyEnforcer(request.Policy);
        if (request.Action == "cancel")
        {
            if (handle.RequestCancellation(owner, request.AllowDestructive, policy) is { } error) return CoreResult<RuntimeOperationResponse>.Fail(error);
        }
        else if (policy?.AuthorizeAction(SemanticWorkflowActions.Inspect, null) is { Success: false } denied)
            return CoreResult<RuntimeOperationResponse>.Fail(denied.Error!);
        var status = "observed";
        if (request.Action == "wait")
        {
            try { await handle.Terminal.WaitAsync(TimeSpan.FromMilliseconds(request.TimeoutMs), cancellationToken).ConfigureAwait(false); }
            catch (TimeoutException) { status = "timed_out"; }
        }
        lock (_operationsLock)
            if (_operationsClosed) return Fail("runtime_operation_session_closed", "The bridge closed while observing app work; no application outcome is implied.");
        var response = new RuntimeOperationResponse(status, handle.Snapshot, DateTimeOffset.UtcNow);
        return policy is null ? CoreResult<RuntimeOperationResponse>.Ok(response) : policy.Sanitize(response);

        static CoreResult<RuntimeOperationResponse> Fail(string code, string message) => CoreResult<RuntimeOperationResponse>.Fail(new(code, message));
    }
}
