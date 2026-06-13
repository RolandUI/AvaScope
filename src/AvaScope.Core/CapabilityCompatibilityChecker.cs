using AvaScope.Protocol;

namespace AvaScope.Core;

public sealed class CapabilityCompatibilityChecker
{
    public CoreResult<AvaScopeCapabilitiesResponse> CreateResponse(string? requiredCapabilities = null)
    {
        return Validate(
            AvaScopeCapabilityCatalog.Current(),
            ParseRequiredCapabilities(requiredCapabilities));
    }

    public CoreResult<AvaScopeCapabilitiesResponse> Validate(
        AvaScopeCapabilitiesResponse response,
        IEnumerable<string>? requiredCapabilities)
    {
        ArgumentNullException.ThrowIfNull(response);

        var required = NormalizeRequiredCapabilities(requiredCapabilities).ToArray();
        if (required.Length == 0)
        {
            return CoreResult<AvaScopeCapabilitiesResponse>.Ok(response);
        }

        var available = response.Capabilities
            .Where(static capability => string.Equals(
                capability.Status,
                AvaScopeCapabilityStatuses.Available,
                StringComparison.Ordinal))
            .Select(static capability => capability.Id)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var unsupported = required
            .Where(capability => !available.Contains(capability))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (unsupported.Length == 0)
        {
            return CoreResult<AvaScopeCapabilitiesResponse>.Ok(response);
        }

        return CoreResult<AvaScopeCapabilitiesResponse>.Fail(new CoreError(
            AvaScopeCapabilityErrorCodes.CapabilityNotSupported,
            "One or more requested AvaScope capabilities are not supported by this service.",
            new Dictionary<string, string>
            {
                ["requestedCapabilities"] = string.Join(",", required),
                ["unsupportedCapabilities"] = string.Join(",", unsupported),
                ["availableCapabilities"] = string.Join(",", available.Order(StringComparer.OrdinalIgnoreCase).Take(64)),
                ["protocolVersion"] = response.ProtocolVersion.ToString(),
                ["nextAction"] = "Call capabilities without requirements, then select only available capabilities before invoking newer tools."
            }));
    }

    public static IReadOnlyList<string> ParseRequiredCapabilities(string? requiredCapabilities)
    {
        if (string.IsNullOrWhiteSpace(requiredCapabilities))
        {
            return [];
        }

        return requiredCapabilities
            .Split([',', ';', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(static capability => !string.IsNullOrWhiteSpace(capability))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static IEnumerable<string> NormalizeRequiredCapabilities(IEnumerable<string>? requiredCapabilities)
    {
        return (requiredCapabilities ?? [])
            .Where(static capability => !string.IsNullOrWhiteSpace(capability))
            .Select(static capability => capability.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase);
    }
}
