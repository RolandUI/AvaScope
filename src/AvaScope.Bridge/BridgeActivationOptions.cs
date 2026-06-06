using AvaScope.Core;
using AvaScope.Protocol;

namespace AvaScope.Bridge;

public sealed record BridgeActivationOptions
{
    public static BridgeActivationOptions Default { get; } = new();

    public BridgeActivationOptions(
        string? displayName = null,
        string sessionKind = SessionKinds.Runtime,
        SessionRegistry? sessionRegistry = null)
    {
        if (string.IsNullOrWhiteSpace(sessionKind))
        {
            throw new ArgumentException("Session kind cannot be empty.", nameof(sessionKind));
        }

        DisplayName = displayName;
        SessionKind = sessionKind;
        SessionRegistry = sessionRegistry;
    }

    public string? DisplayName { get; }

    public string SessionKind { get; }

    public SessionRegistry? SessionRegistry { get; }
}
