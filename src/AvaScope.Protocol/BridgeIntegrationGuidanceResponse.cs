using System.Text.Json.Serialization;

namespace AvaScope.Protocol;

public sealed record BridgeIntegrationGuidanceResponse(
    [property: JsonPropertyName("projectPath")] string ProjectPath,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("targetFrameworks")] IReadOnlyList<string> TargetFrameworks,
    [property: JsonPropertyName("selectedFramework")] string? SelectedFramework,
    [property: JsonPropertyName("avaloniaVersion")] string? AvaloniaVersion,
    [property: JsonPropertyName("existingIntegration")] IReadOnlyList<IntegrationSourceLocation> ExistingIntegration,
    [property: JsonPropertyName("guidance")] IReadOnlyList<IntegrationGuidance> Guidance,
    [property: JsonPropertyName("diagnostics")] IReadOnlyList<string> Diagnostics,
    [property: JsonPropertyName("readOnly")] bool ReadOnly = true);

public sealed record IntegrationSourceLocation(
    [property: JsonPropertyName("path")] string Path,
    [property: JsonPropertyName("line")] int Line,
    [property: JsonPropertyName("sha256")] string Sha256);

public sealed record IntegrationGuidance(
    [property: JsonPropertyName("mode")] string Mode,
    [property: JsonPropertyName("location")] IntegrationSourceLocation Location,
    [property: JsonPropertyName("instruction")] string Instruction,
    [property: JsonPropertyName("snippet")] string Snippet);
