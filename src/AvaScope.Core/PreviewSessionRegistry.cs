using System.Collections.Concurrent;
using AvaScope.Protocol;

namespace AvaScope.Core;

public sealed class PreviewSessionRegistry
{
    private readonly ConcurrentDictionary<string, PreviewSessionRecord> _sessions = new(StringComparer.Ordinal);
    private readonly PreviewHostClient _previewHostClient;
    private readonly SessionRegistry _sessionRegistry;
    private readonly PreviewSessionStore? _store;
    private readonly TimeProvider _timeProvider;

    public PreviewSessionRegistry(
        SessionRegistry sessionRegistry,
        PreviewHostClient previewHostClient)
        : this(sessionRegistry, previewHostClient, TimeProvider.System, store: null)
    {
    }

    public PreviewSessionRegistry(
        SessionRegistry sessionRegistry,
        PreviewHostClient previewHostClient,
        TimeProvider timeProvider)
        : this(sessionRegistry, previewHostClient, timeProvider, store: null)
    {
    }

    public PreviewSessionRegistry(
        SessionRegistry sessionRegistry,
        PreviewHostClient previewHostClient,
        TimeProvider timeProvider,
        PreviewSessionStore? store)
    {
        _sessionRegistry = sessionRegistry ?? throw new ArgumentNullException(nameof(sessionRegistry));
        _previewHostClient = previewHostClient ?? throw new ArgumentNullException(nameof(previewHostClient));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _store = store;

        RestoreFromStore();
    }

    public async Task<CoreResult<PreviewSessionSummary>> CreateAsync(
        PreviewRequest request,
        string? displayName = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var session = _sessionRegistry.Create(
            SessionKinds.Preview,
            string.IsNullOrWhiteSpace(displayName) ? CreateDisplayName(request) : displayName);

        var renderResult = await _previewHostClient.RenderAsync(request, cancellationToken);
        var lastRender = ToToolResult(renderResult);
        var snapshot = renderResult.Success
            ? _sessionRegistry.Get(session.Id).Value!
            : _sessionRegistry.MarkFailed(session.Id, renderResult.Error!).Value!;
        var updatedAt = _timeProvider.GetUtcNow();
        var record = new PreviewSessionRecord(session.Id, request, lastRender, updatedAt);

        _sessions[session.Id.Value] = record;

        var summary = record.Snapshot(ToProtocolSummary(snapshot));
        var stored = Store(summary);
        return stored.Success
            ? CoreResult<PreviewSessionSummary>.Ok(summary)
            : CoreResult<PreviewSessionSummary>.Fail(stored.Error!);
    }

    public IReadOnlyList<PreviewSessionSummary> List()
    {
        return _sessions.Values
            .Select(TrySnapshot)
            .Where(static session => session is not null)
            .Cast<PreviewSessionSummary>()
            .OrderBy(static session => session.Session.CreatedAt)
            .ToArray();
    }

    public CoreResult<PreviewSessionSummary> Close(SessionId sessionId)
    {
        ArgumentNullException.ThrowIfNull(sessionId);

        if (!_sessions.TryGetValue(sessionId.Value, out var record))
        {
            return CoreResult<PreviewSessionSummary>.Fail(SessionNotFound(sessionId));
        }

        var closed = _sessionRegistry.Close(sessionId);
        if (!closed.Success)
        {
            return CoreResult<PreviewSessionSummary>.Fail(closed.Error!);
        }

        record.Touch(_timeProvider.GetUtcNow());
        var summary = record.Snapshot(ToProtocolSummary(closed.Value!));
        var stored = Store(summary);
        return stored.Success
            ? CoreResult<PreviewSessionSummary>.Ok(summary)
            : CoreResult<PreviewSessionSummary>.Fail(stored.Error!);
    }

    public async Task<CoreResult<PreviewSessionSummary>> ReloadAsync(
        SessionId sessionId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sessionId);

        if (!_sessions.TryGetValue(sessionId.Value, out var record))
        {
            return CoreResult<PreviewSessionSummary>.Fail(SessionNotFound(sessionId));
        }

        var current = _sessionRegistry.Get(sessionId);
        if (!current.Success)
        {
            return CoreResult<PreviewSessionSummary>.Fail(current.Error!);
        }

        if (current.Value!.State is SessionLifecycleState.Closed)
        {
            return CoreResult<PreviewSessionSummary>.Fail(new CoreError(
                CoreErrorCodes.SessionClosed,
                $"Preview session '{sessionId}' is closed and cannot be reloaded."));
        }

        var renderResult = await _previewHostClient.RenderAsync(record.Request, cancellationToken);
        var lastRender = ToToolResult(renderResult);
        var snapshot = renderResult.Success
            ? _sessionRegistry.MarkActive(sessionId).Value!
            : _sessionRegistry.MarkFailed(sessionId, renderResult.Error!).Value!;

        record.Update(lastRender, _timeProvider.GetUtcNow());

        var summary = record.Snapshot(ToProtocolSummary(snapshot));
        var stored = Store(summary);
        return stored.Success
            ? CoreResult<PreviewSessionSummary>.Ok(summary)
            : CoreResult<PreviewSessionSummary>.Fail(stored.Error!);
    }

    private void RestoreFromStore()
    {
        if (_store is null)
        {
            return;
        }

        foreach (var summary in _store.Load())
        {
            if (!TryCreateSnapshot(summary, out var snapshot))
            {
                continue;
            }

            var restored = _sessionRegistry.Restore(snapshot!);
            if (!restored.Success)
            {
                continue;
            }

            _sessions[summary.Session.SessionId.Value] = new PreviewSessionRecord(
                summary.Session.SessionId,
                summary.Request,
                summary.LastRender,
                summary.UpdatedAt);
        }
    }

    private CoreResult<bool> Store(PreviewSessionSummary summary)
    {
        return _store?.Save(summary) ?? CoreResult<bool>.Ok(true);
    }

    private PreviewSessionSummary? TrySnapshot(PreviewSessionRecord record)
    {
        var session = _sessionRegistry.Get(record.SessionId);
        return session.Success
            ? record.Snapshot(ToProtocolSummary(session.Value!))
            : null;
    }

    private static ToolResult<PreviewResponse> ToToolResult(CoreResult<PreviewResponse> result)
    {
        return result.Success
            ? ToolResult<PreviewResponse>.Ok(result.Value!)
            : ToolResult<PreviewResponse>.Fail(new ProtocolError(result.Error!.Code, result.Error.Message));
    }

    private static SessionSummary ToProtocolSummary(SessionSnapshot session)
    {
        return new SessionSummary(
            session.Id,
            session.Kind,
            ToProtocolState(session.State),
            session.CreatedAt,
            session.DisplayName);
    }

    private static string ToProtocolState(SessionLifecycleState state)
    {
        return state switch
        {
            SessionLifecycleState.Active => SessionStates.Active,
            SessionLifecycleState.Closing => SessionStates.Closing,
            SessionLifecycleState.Closed => SessionStates.Closed,
            SessionLifecycleState.Failed => SessionStates.Failed,
            _ => throw new ArgumentOutOfRangeException(nameof(state), state, "Unknown session state.")
        };
    }

    private static bool TryCreateSnapshot(
        PreviewSessionSummary summary,
        out SessionSnapshot? snapshot)
    {
        snapshot = null;
        if (!string.Equals(summary.Session.Kind, SessionKinds.Preview, StringComparison.Ordinal))
        {
            return false;
        }

        if (!TryParseState(summary.Session.State, out var state))
        {
            return false;
        }

        var lastError = summary.LastRender.Success || summary.LastRender.Error is null
            ? null
            : new CoreError(summary.LastRender.Error.Code, summary.LastRender.Error.Message);
        snapshot = new SessionSnapshot(
            summary.Session.SessionId,
            summary.Session.Kind,
            state,
            summary.Session.CreatedAt,
            summary.Session.DisplayName,
            lastError);
        return true;
    }

    private static bool TryParseState(string state, out SessionLifecycleState lifecycleState)
    {
        lifecycleState = state switch
        {
            SessionStates.Active => SessionLifecycleState.Active,
            SessionStates.Closing => SessionLifecycleState.Closing,
            SessionStates.Closed => SessionLifecycleState.Closed,
            SessionStates.Failed => SessionLifecycleState.Failed,
            _ => SessionLifecycleState.Active
        };

        return state is SessionStates.Active
            or SessionStates.Closing
            or SessionStates.Closed
            or SessionStates.Failed;
    }

    private static CoreError SessionNotFound(SessionId sessionId)
    {
        return new CoreError(CoreErrorCodes.SessionNotFound, $"Preview session '{sessionId}' was not found.");
    }

    private static string CreateDisplayName(PreviewRequest request)
    {
        return request.ViewPath
            ?? request.ProjectPath
            ?? "Preview session";
    }

    private sealed class PreviewSessionRecord
    {
        private readonly object _syncRoot = new();
        private ToolResult<PreviewResponse> _lastRender;
        private DateTimeOffset _updatedAt;

        public PreviewSessionRecord(
            SessionId sessionId,
            PreviewRequest request,
            ToolResult<PreviewResponse> lastRender,
            DateTimeOffset updatedAt)
        {
            SessionId = sessionId;
            Request = request;
            _lastRender = lastRender;
            _updatedAt = updatedAt;
        }

        public SessionId SessionId { get; }

        public PreviewRequest Request { get; }

        public void Touch(DateTimeOffset updatedAt)
        {
            lock (_syncRoot)
            {
                _updatedAt = updatedAt;
            }
        }

        public void Update(
            ToolResult<PreviewResponse> lastRender,
            DateTimeOffset updatedAt)
        {
            lock (_syncRoot)
            {
                _lastRender = lastRender;
                _updatedAt = updatedAt;
            }
        }

        public PreviewSessionSummary Snapshot(SessionSummary session)
        {
            lock (_syncRoot)
            {
                return new PreviewSessionSummary(session, Request, _lastRender, _updatedAt);
            }
        }
    }
}
