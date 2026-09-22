using System.Text.Json.Serialization;

namespace AvaScope.Protocol;

public sealed record TargetReadinessRequest(
    [property: JsonPropertyName("projectPath")] string? ProjectPath = null,
    [property: JsonPropertyName("assemblyPath")] string? AssemblyPath = null,
    [property: JsonPropertyName("profileFile")] string? ProfileFile = null,
    [property: JsonPropertyName("profileName")] string? ProfileName = null,
    [property: JsonPropertyName("providerDirectory")] string? ProviderDirectory = null,
    [property: JsonPropertyName("expectedProviderVersion")] string? ExpectedProviderVersion = null,
    [property: JsonPropertyName("expectedManifestSha256")] string? ExpectedManifestSha256 = null,
    [property: JsonPropertyName("backend")] string Backend = "auto",
    [property: JsonPropertyName("nativeInput")] bool NativeInput = false,
    [property: JsonPropertyName("nativeScreenshot")] bool NativeScreenshot = false,
    [property: JsonPropertyName("nativeDialogs")] bool NativeDialogs = false,
    [property: JsonPropertyName("expectedInstallationRoot")] string? ExpectedInstallationRoot = null,
    [property: JsonPropertyName("timeoutMs")] int TimeoutMs = 10000,
    [property: JsonPropertyName("framework")] string? Framework = null);

public sealed record TargetReadinessResponse(
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("backend")] string Backend,
    [property: JsonPropertyName("checks")] IReadOnlyList<DoctorCheck> Checks,
    [property: JsonPropertyName("componentOrigins")] IReadOnlyList<DiagnosticComponentOrigin> ComponentOrigins,
    [property: JsonPropertyName("bridgeActivated")] bool BridgeActivated = false);

public sealed record PlatformReadinessProbeRequest(string Backend, bool NativeInput, bool NativeScreenshot, bool NativeDialogs,
    X11EnvironmentOptions? X11Environment = null);
