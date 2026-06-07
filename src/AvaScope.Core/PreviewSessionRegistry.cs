using System.Collections.Concurrent;
using AvaScope.Protocol;

namespace AvaScope.Core;

public sealed class PreviewSessionRegistry
{
    private readonly ConcurrentDictionary<string, PreviewSessionRecord> _sessions = new(StringComparer.Ordinal);
    private readonly PreviewHostClient _previewHostClient;
    private readonly SessionRegistry _sessionRegistry;
    private readonly TimeProvider _timeProvider;

    public PreviewSessionRegistry(
        SessionRegistry sessionRegistry,
        PreviewHostClient previewHostClient)
        : this(sessionRegistry, previewHostClient, TimeProvider.System)
    {
    }

    public PreviewSessionRegistry(
        SessionRegistry sessionRegistry,
        PreviewHostClient previewHostClient,
        TimeProvider timeProvider)
    {
        _sessionRegistry = sessionRegistry ?? throw new ArgumentNullException(nameof(sessionRegistry));
        _previewHostClient = previewHostClient ?? throw new ArgumentNullException(nameof(previewHostClient));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
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

        return CoreResult<PreviewSessionSummary>.Ok(record.Snapshot(ToProtocolSummary(snapshot)));
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
        return CoreResult<PreviewSessionSummary>.Ok(record.Snapshot(ToProtocolSummary(closed.Value!)));
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

        return CoreResult<PreviewSessionSummary>.Ok(record.Snapshot(ToProtocolSummary(snapshot)));
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
