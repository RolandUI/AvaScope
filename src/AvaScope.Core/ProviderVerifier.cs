using AvaScope.Protocol;
using OptionalDiagnostics;

namespace AvaScope.Core;

public static class ProviderVerifier
{
    public static CoreResult<ProviderVerificationResponse> Verify(
        string directory, string? expectedVersion = null, string? expectedManifestSha256 = null)
    {
        var result = OptionalProviderLoader.Verify(directory, expectedVersion, expectedManifestSha256);
        return result.Success
            ? CoreResult<ProviderVerificationResponse>.Ok(new(
                result.Directory!, result.ProviderVersion!, result.ManifestSha256!,
                "untrimmed .NET 10; dynamic assembly loading", "[12.1.0,12.2.0)", "checked_by_host_at_activation"))
            : CoreResult<ProviderVerificationResponse>.Fail(new CoreError(result.Code, result.Message));
    }
}
