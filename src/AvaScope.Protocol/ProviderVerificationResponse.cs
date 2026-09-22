using System.Text.Json.Serialization;

namespace AvaScope.Protocol;

public sealed record ProviderVerificationResponse(
    [property: JsonPropertyName("directory")] string Directory,
    [property: JsonPropertyName("providerVersion")] string ProviderVersion,
    [property: JsonPropertyName("manifestSha256")] string ManifestSha256,
    [property: JsonPropertyName("runtimeRequirement")] string RuntimeRequirement,
    [property: JsonPropertyName("avaloniaRequirement")] string AvaloniaRequirement,
    [property: JsonPropertyName("hostCompatibility")] string HostCompatibility,
    [property: JsonPropertyName("activated")] bool Activated = false);
