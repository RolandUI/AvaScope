using System.Security.Cryptography;
using AvaScope.Protocol;

namespace AvaScope.Core;

/// <summary>Bridge-owned coordination, not authentication against code running as the same OS user.</summary>
public sealed class SessionControlCoordinator(SessionId sessionId, TimeProvider? timeProvider = null)
{
    private readonly object _sync = new();
    private readonly TimeProvider _time = timeProvider ?? TimeProvider.System;
    private string? _owner;
    private string? _token;
    private long _renewed;
    private int _ttl;
    private DateTimeOffset? _expires;
    private bool _busy;
    private bool _leaseRequired;

    public CoreResult<SessionControlResponse> Execute(SessionControlRequest request)
    {
        lock (_sync)
        {
            Expire();
            if (request.Operation is not ("status" or "acquire" or "renew" or "release") || request.TtlMs is < 1000 or > 300000
                || request.Owner?.Length > 128 || request.Token?.Length > 128)
                return CoreResult<SessionControlResponse>.Fail(new("session_control_invalid", "Use status/acquire/renew/release, ttlMs 1000..300000 and owner/token of at most 128 characters."));
            if (request.Operation == "status") return Snapshot();
            if (request.Operation == "acquire")
            {
                if (string.IsNullOrWhiteSpace(request.Owner))
                    return CoreResult<SessionControlResponse>.Fail(new("session_control_invalid", "An explicit nonempty owner/run identity is required."));
                if (_token is not null || _busy) return Conflict<SessionControlResponse>();
                _owner = request.Owner;
                _token = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(32));
                _leaseRequired = true;
            }
            else if (_token is null || !Matches(request.Token)) return Conflict<SessionControlResponse>();
            else if (request.Operation == "release")
            {
                if (_busy) return Conflict<SessionControlResponse>();
                _token = null;
                _owner = null;
                _expires = null;
                _leaseRequired = false;
                return Snapshot();
            }
            _ttl = request.TtlMs;
            _renewed = _time.GetTimestamp();
            _expires = _time.GetUtcNow().AddMilliseconds(_ttl);
            return Snapshot(includeToken: true);
        }
    }

    public CoreResult<IDisposable> Enter(string? token)
    {
        lock (_sync)
        {
            Expire();
            if (_busy || (_token is not null ? !Matches(token) : _leaseRequired || token is not null))
                return Conflict<IDisposable>();
            _busy = true;
            return CoreResult<IDisposable>.Ok(new Permit(this));
        }
    }

    private void Expire()
    {
        // An expired lease cannot transfer an operation that is still executing.
        if (_token is not null && !_busy && _time.GetElapsedTime(_renewed).TotalMilliseconds >= _ttl)
        { _token = null; _owner = null; _expires = null; }
    }

    private bool Matches(string? token) => token is { Length: <= 128 } && _token is not null
        && CryptographicOperations.FixedTimeEquals(System.Text.Encoding.UTF8.GetBytes(token), System.Text.Encoding.UTF8.GetBytes(_token));

    private CoreResult<SessionControlResponse> Snapshot(bool includeToken = false) => CoreResult<SessionControlResponse>.Ok(new(
        sessionId, _token is not null ? "leased" : _leaseRequired ? "expired" : "available", _owner, _expires, _busy, includeToken ? _token : null));

    private CoreResult<T> Conflict<T>() => CoreResult<T>.Fail(new("session_control_conflict",
        "Session control is busy, leased to another owner, or the supplied lease expired. Inspect session_control, then explicitly acquire or renew; no control was dispatched.",
        new Dictionary<string, string> { ["sessionId"] = sessionId.Value, ["owner"] = _owner ?? "none", ["busy"] = _busy.ToString(), ["expiresAt"] = _expires?.ToString("O") ?? "none" }));

    private sealed class Permit(SessionControlCoordinator owner) : IDisposable
    {
        private SessionControlCoordinator? _owner = owner;
        public void Dispose()
        {
            var coordinator = Interlocked.Exchange(ref _owner, null);
            if (coordinator is not null) lock (coordinator._sync) { coordinator._busy = false; coordinator.Expire(); }
        }
    }
}
