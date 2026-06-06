using AvaScope.Protocol;

namespace AvaScope.Core;

public sealed record SessionSnapshot
{
    public SessionSnapshot(
        SessionId id,
        string kind,
        SessionLifecycleState state,
        DateTimeOffset createdAt,
        string? displayName = null,
        CoreError? lastError = null)
    {
        Id = id ?? throw new ArgumentNullException(nameof(id));

        if (string.IsNullOrWhiteSpace(kind))
        {
            throw new ArgumentException("Session kind cannot be empty.", nameof(kind));
        }

        Kind = kind;
        State = state;
        CreatedAt = createdAt;
        DisplayName = displayName;
        LastError = lastError;
    }

    public SessionId Id { get; }

    public string Kind { get; }

    public SessionLifecycleState State { get; }

    public DateTimeOffset CreatedAt { get; }

    public string? DisplayName { get; }

    public CoreError? LastError { get; }
}
