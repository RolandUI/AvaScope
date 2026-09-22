using AvaScope.Core;
using AvaScope.Protocol;

namespace AvaScope.Bridge;

public sealed record BridgeActivationOptions
{
    public static BridgeActivationOptions Default { get; } = new();

    public BridgeActivationOptions(
        string? displayName = null,
        string sessionKind = SessionKinds.Runtime,
        SessionRegistry? sessionRegistry = null,
        bool enableCustomActions = false,
        IReadOnlyList<string>? allowedCustomActions = null,
        bool allowDestructiveCustomActions = false,
        bool enableTestFixtures = false,
        IReadOnlyList<string>? allowedTestResources = null)
    {
        if (string.IsNullOrWhiteSpace(sessionKind))
        {
            throw new ArgumentException("Session kind cannot be empty.", nameof(sessionKind));
        }

        DisplayName = displayName;
        SessionKind = sessionKind;
        SessionRegistry = sessionRegistry;
        EnableCustomActions = enableCustomActions;
        AllowedCustomActions = (allowedCustomActions ?? [])
            .Where(static action => !string.IsNullOrWhiteSpace(action))
            .Select(static action => action.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        AllowDestructiveCustomActions = allowDestructiveCustomActions;
        EnableTestFixtures = enableTestFixtures;
        AllowedTestResources = (allowedTestResources ?? []).Distinct(StringComparer.Ordinal).ToArray();
        foreach (var resource in AllowedTestResources) RuntimeTestFixtureDescriptor.ValidateIdentifier(resource, nameof(allowedTestResources));
    }

    public string? DisplayName { get; }

    public string SessionKind { get; }

    public SessionRegistry? SessionRegistry { get; }

    public bool EnableCustomActions { get; }

    public IReadOnlyList<string> AllowedCustomActions { get; }

    public bool AllowDestructiveCustomActions { get; }
    public bool EnableTestFixtures { get; }
    public IReadOnlyList<string> AllowedTestResources { get; }
}
