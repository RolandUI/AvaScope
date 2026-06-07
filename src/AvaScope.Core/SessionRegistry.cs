using System.Collections.Concurrent;
using AvaScope.Protocol;

namespace AvaScope.Core;

public sealed class SessionRegistry
{
    private readonly ConcurrentDictionary<string, SessionRecord> _sessions = new(StringComparer.Ordinal);
    private readonly TimeProvider _timeProvider;

    public SessionRegistry()
        : this(TimeProvider.System)
    {
    }

    public SessionRegistry(TimeProvider timeProvider)
    {
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    public SessionSnapshot Create(string kind, string? displayName = null)
    {
        if (string.IsNullOrWhiteSpace(kind))
        {
            throw new ArgumentException("Session kind cannot be empty.", nameof(kind));
        }

        while (true)
        {
            var id = SessionId.New();
            var record = new SessionRecord(
                id,
                kind,
                displayName,
                _timeProvider.GetUtcNow());

            if (_sessions.TryAdd(id.Value, record))
            {
                return record.Snapshot();
            }
        }
    }

    public IReadOnlyList<SessionSnapshot> List()
    {
        return _sessions.Values
            .Select(static session => session.Snapshot())
            .OrderBy(static session => session.CreatedAt)
            .ToArray();
    }

    public CoreResult<SessionSnapshot> Get(SessionId sessionId)
    {
        ArgumentNullException.ThrowIfNull(sessionId);

        return _sessions.TryGetValue(sessionId.Value, out var session)
            ? CoreResult<SessionSnapshot>.Ok(session.Snapshot())
            : CoreResult<SessionSnapshot>.Fail(SessionNotFound(sessionId));
    }

    public CoreResult<SessionSnapshot> Restore(SessionSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        var record = new SessionRecord(
            snapshot.Id,
            snapshot.Kind,
            snapshot.DisplayName,
            snapshot.CreatedAt,
            snapshot.State,
            snapshot.LastError);

        return _sessions.TryAdd(snapshot.Id.Value, record)
            ? CoreResult<SessionSnapshot>.Ok(record.Snapshot())
            : Get(snapshot.Id);
    }

    public CoreResult<SessionSnapshot> Close(SessionId sessionId)
    {
        ArgumentNullException.ThrowIfNull(sessionId);

        if (!_sessions.TryGetValue(sessionId.Value, out var session))
        {
            return CoreResult<SessionSnapshot>.Fail(SessionNotFound(sessionId));
        }

        return CoreResult<SessionSnapshot>.Ok(session.Close());
    }

    public CoreResult<SessionSnapshot> MarkActive(SessionId sessionId)
    {
        ArgumentNullException.ThrowIfNull(sessionId);

        if (!_sessions.TryGetValue(sessionId.Value, out var session))
        {
            return CoreResult<SessionSnapshot>.Fail(SessionNotFound(sessionId));
        }

        return CoreResult<SessionSnapshot>.Ok(session.MarkActive());
    }

    public CoreResult<SessionSnapshot> MarkFailed(SessionId sessionId, CoreError error)
    {
        ArgumentNullException.ThrowIfNull(sessionId);
        ArgumentNullException.ThrowIfNull(error);

        if (!_sessions.TryGetValue(sessionId.Value, out var session))
        {
            return CoreResult<SessionSnapshot>.Fail(SessionNotFound(sessionId));
        }

        return CoreResult<SessionSnapshot>.Ok(session.MarkFailed(error));
    }

    private static CoreError SessionNotFound(SessionId sessionId)
    {
        return new CoreError(CoreErrorCodes.SessionNotFound, $"Session '{sessionId}' was not found.");
    }

    private sealed class SessionRecord
    {
        private readonly object _syncRoot = new();
        private SessionLifecycleState _state = SessionLifecycleState.Active;
        private CoreError? _lastError;

        public SessionRecord(
            SessionId id,
            string kind,
            string? displayName,
            DateTimeOffset createdAt,
            SessionLifecycleState state = SessionLifecycleState.Active,
            CoreError? lastError = null)
        {
            Id = id;
            Kind = kind;
            DisplayName = displayName;
            CreatedAt = createdAt;
            _state = state;
            _lastError = lastError;
        }

        public SessionId Id { get; }

        public string Kind { get; }

        public string? DisplayName { get; }

        public DateTimeOffset CreatedAt { get; }

        public SessionSnapshot Snapshot()
        {
            lock (_syncRoot)
            {
                return new SessionSnapshot(Id, Kind, _state, CreatedAt, DisplayName, _lastError);
            }
        }

        public SessionSnapshot Close()
        {
            lock (_syncRoot)
            {
                if (_state is not SessionLifecycleState.Closed)
                {
                    _state = SessionLifecycleState.Closing;
                    _state = SessionLifecycleState.Closed;
                }

                return new SessionSnapshot(Id, Kind, _state, CreatedAt, DisplayName, _lastError);
            }
        }

        public SessionSnapshot MarkFailed(CoreError error)
        {
            lock (_syncRoot)
            {
                _state = SessionLifecycleState.Failed;
                _lastError = error;

                return new SessionSnapshot(Id, Kind, _state, CreatedAt, DisplayName, _lastError);
            }
        }

        public SessionSnapshot MarkActive()
        {
            lock (_syncRoot)
            {
                if (_state is not SessionLifecycleState.Closed)
                {
                    _state = SessionLifecycleState.Active;
                    _lastError = null;
                }

                return new SessionSnapshot(Id, Kind, _state, CreatedAt, DisplayName, _lastError);
            }
        }
    }
}
