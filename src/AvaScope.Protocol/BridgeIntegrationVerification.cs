using System.Text.Json.Serialization;

namespace AvaScope.Protocol;

public sealed record BridgeIntegrationVerificationRequest(
    [property: JsonPropertyName("launch")] RuntimeScenarioLaunchOptions Launch,
    [property: JsonPropertyName("outputDirectory")] string OutputDirectory,
    [property: JsonPropertyName("build")] RuntimeScenarioBuildOptions? Build = null,
    [property: JsonPropertyName("providerDirectory")] string? ProviderDirectory = null,
    [property: JsonPropertyName("expectedProviderVersion")] string? ExpectedProviderVersion = null,
    [property: JsonPropertyName("expectedManifestSha256")] string? ExpectedManifestSha256 = null,
    [property: JsonPropertyName("bootstrapDisabled")] bool BootstrapDisabled = false,
    [property: JsonPropertyName("productionOutputDirectory")] string? ProductionOutputDirectory = null,
    [property: JsonPropertyName("observationMs")] int ObservationMs = 1500,
    [property: JsonPropertyName("safeInputTarget")] SemanticWorkflowSelector? SafeInputTarget = null,
    [property: JsonPropertyName("safeInputTargetDeclared")] bool SafeInputTargetDeclared = false,
    [property: JsonPropertyName("inspectionTarget")] SemanticWorkflowSelector? InspectionTarget = null);

public sealed record BridgeIntegrationVerificationResponse(
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("failureStage")] string? FailureStage,
    [property: JsonPropertyName("mode")] string Mode,
    [property: JsonPropertyName("reportPath")] string ReportPath,
    [property: JsonPropertyName("stages")] IReadOnlyList<IntegrationVerificationStage> Stages,
    [property: JsonPropertyName("scenario")] RuntimeScenarioResponse? Scenario = null);

public sealed record IntegrationVerificationStage(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("message")] string Message,
    [property: JsonPropertyName("remediation")] string? Remediation = null,
    [property: JsonPropertyName("evidencePaths")] IReadOnlyList<string>? EvidencePaths = null);
