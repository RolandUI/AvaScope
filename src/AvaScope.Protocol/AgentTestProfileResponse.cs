using System.Text.Json;
using System.Text.Json.Serialization;

namespace AvaScope.Protocol;

public sealed record AgentTestProfileResponse(
    [property: JsonPropertyName("schemaVersion")] int SchemaVersion,
    [property: JsonPropertyName("profileFile")] string ProfileFile,
    [property: JsonPropertyName("profileName")] string ProfileName,
    [property: JsonPropertyName("platform")] string Platform,
    [property: JsonPropertyName("profileSha256")] string ProfileSha256,
    [property: JsonPropertyName("scenario")] JsonElement Scenario,
    [property: JsonPropertyName("provider")] ProviderVerificationResponse? Provider,
    [property: JsonPropertyName("environmentVariableNames")] IReadOnlyList<string> EnvironmentVariableNames,
    [property: JsonPropertyName("diagnostics")] IReadOnlyList<string> Diagnostics);
