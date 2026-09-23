using AvaScope.Core;
using AvaScope.Protocol;

namespace AvaScope.Bridge;

/// <summary>Thread-safe, app-reported work. Reporting does not access Avalonia controls.</summary>
public sealed class RuntimeOperationHandle
{
    private readonly object _sync = new();
    private readonly CancellationTokenSource _cancellation = new();
    private readonly TaskCompletionSource _terminal = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private Task _cancellationTask = Task.CompletedTask;
    private RuntimeOperationSnapshot _snapshot;
    private bool _closed;
    internal string SafetyClassification { get; }
    internal Task Terminal => _terminal.Task;
    public string OperationId => _snapshot.OperationId;
    public CancellationToken CancellationToken { get; }

    internal RuntimeOperationHandle(SessionId sessionId, string requestId, string actionName, RuntimeTargetContext target,
        string? owner, bool canCancel, string safetyClassification)
    {
        var now = DateTimeOffset.UtcNow;
        _snapshot = new(sessionId, sessionId.Value + ":" + Guid.NewGuid().ToString("N"), requestId, actionName, target,
            owner, "accepted", null, null, canCancel, false, now, now, null, new Dictionary<string, string>(), null);
        SafetyClassification = safetyClassification;
        CancellationToken = _cancellation.Token;
    }

    internal RuntimeOperationSnapshot Snapshot { get { lock (_sync) return _snapshot; } }

    public bool ReportProgress(double? progress = null, string? message = null)
    {
        if (progress is { } value && (!double.IsFinite(value) || value is < 0 or > 1) || message?.Length > 512)
            throw new ArgumentException("Progress must be finite 0..1; messages allow at most 512 characters.");
        lock (_sync)
        {
            if (_closed || _snapshot.IsTerminal) return false;
            _snapshot = _snapshot with { Status = "running", Progress = progress, Message = message, UpdatedAt = DateTimeOffset.UtcNow };
            return true;
        }
    }

    public bool Complete(IReadOnlyDictionary<string, string>? result = null) => Finish("completed", result, null);
    public bool Fail(string code, string message)
    {
        if (string.IsNullOrWhiteSpace(code) || code.Length > 128 || string.IsNullOrWhiteSpace(message) || message.Length > 512)
            throw new ArgumentException("Failure requires a code of 1..128 and message of 1..512 characters.");
        return Finish("failed", null, new(code, message));
    }
    public bool ConfirmCancelled() => Finish("cancelled", null, null);

    private bool Finish(string status, IReadOnlyDictionary<string, string>? result, ProtocolError? error)
    {
        if (result is not null && (result.Count > 16 || result.Any(pair => string.IsNullOrWhiteSpace(pair.Key)
            || pair.Key.Length > 64 || pair.Value is null || pair.Value.Length > 512)))
            throw new ArgumentException("Operation results allow up to 16 fields, keys of 1..64 and values of at most 512 characters.");
        var values = new Dictionary<string, string>(result ?? new Dictionary<string, string>(), StringComparer.Ordinal);
        lock (_sync)
        {
            if (_closed || _snapshot.IsTerminal) return false;
            var now = DateTimeOffset.UtcNow;
            _snapshot = _snapshot with { Status = status, Result = values, Error = error, UpdatedAt = now, RetainUntil = now.AddMinutes(10) };
            _terminal.TrySetResult();
            return true;
        }
    }

    internal CoreError? RequestCancellation(string? owner, bool allowDestructive, RuntimeEvidencePolicyEnforcer? policy)
    {
        lock (_sync)
        {
            if (_snapshot.Owner is not null && !string.Equals(_snapshot.Owner, owner, StringComparison.Ordinal))
                return new("runtime_operation_owner_mismatch", "Cancellation requires a current control lease for the run that started this operation.");
            if (policy?.AuthorizeAction(SemanticWorkflowActions.CustomAction, _snapshot.ActionName) is { Success: false } denied)
                return denied.Error;
            if (SafetyClassification == RuntimeCustomActionSafetyClassifications.Destructive
                && (!allowDestructive || policy is not null && !policy.AllowsDestructiveAction(true, false)))
                return new("runtime_operation_cancel_disallowed", "Cancelling this action requires explicit destructive-action authorization.");
            if (!_snapshot.CanCancel) return new("runtime_operation_cancel_unsupported", "The host did not declare cancellation for this action.");
            if (_closed) return new("runtime_operation_session_closed", "The operation's bridge session is closed.");
            if (_snapshot.IsTerminal || _snapshot.CancellationRequested) return null;
            _snapshot = _snapshot with { CancellationRequested = true, UpdatedAt = DateTimeOffset.UtcNow };
            // CancelAsync schedules app callbacks asynchronously; the lock protects its lifetime, not their execution.
            _cancellationTask = SignalCancellationAsync();
        }
        return null;
    }

    private async Task SignalCancellationAsync()
    {
        try { await _cancellation.CancelAsync().ConfigureAwait(false); }
        catch (Exception exception) when (exception is not OutOfMemoryException and not AccessViolationException)
        {
            // A throwing cancellation callback does not prove that app work stopped.
            lock (_sync)
                if (!_closed && !_snapshot.IsTerminal)
                    _snapshot = _snapshot with { Error = new("runtime_operation_cancel_callback_failed", "An application cancellation callback failed; completion remains unknown."), UpdatedAt = DateTimeOffset.UtcNow };
        }
    }

    internal void Close()
    {
        lock (_sync)
        {
            if (_closed) return;
            _closed = true; _terminal.TrySetResult();
            _ = DisposeCancellationAsync();
        }
        // Closing the observer is not authorization to cancel host work.
    }

    private async Task DisposeCancellationAsync()
    {
        await _cancellationTask.ConfigureAwait(false);
        _cancellation.Dispose();
    }
}
