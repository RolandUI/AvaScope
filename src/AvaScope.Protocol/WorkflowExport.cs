using System.Text.Json;
using System.Text.Json.Serialization;

namespace AvaScope.Protocol;

public sealed record WorkflowExportRequest(
    [property: JsonPropertyName("source")] SemanticWorkflowRequest Source,
    [property: JsonPropertyName("recording")] SemanticWorkflowResponse Recording,
    [property: JsonPropertyName("outputDirectory")] string OutputDirectory,
    [property: JsonPropertyName("parameters")] IReadOnlyDictionary<string, string>? Parameters = null,
    [property: JsonPropertyName("defaultTopLevelAlias")] SemanticWorkflowTopLevelAlias? DefaultTopLevelAlias = null);

public sealed record WorkflowExportParameter(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("purpose")] string Purpose);

public sealed record WorkflowExportReview(
    [property: JsonPropertyName("code")] string Code,
    [property: JsonPropertyName("stepId")] string? StepId,
    [property: JsonPropertyName("message")] string Message,
    [property: JsonPropertyName("blocksReplay")] bool BlocksReplay = false);

public sealed record WorkflowExportDocument(
    [property: JsonPropertyName("schemaVersion")] int SchemaVersion,
    [property: JsonPropertyName("workflow")] JsonElement Workflow,
    [property: JsonPropertyName("parameters")] IReadOnlyList<WorkflowExportParameter> Parameters,
    [property: JsonPropertyName("reviewItems")] IReadOnlyList<WorkflowExportReview> ReviewItems,
    [property: JsonPropertyName("verifiedStepIds")] IReadOnlyList<string> VerifiedStepIds,
    [property: JsonPropertyName("requiresIsolatedState")] bool RequiresIsolatedState,
    [property: JsonPropertyName("requiresEvidenceRoot")] bool RequiresEvidenceRoot,
    [property: JsonPropertyName("requiresProcessId")] bool RequiresProcessId);

public sealed record WorkflowExportResponse(
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("exportPath")] string ExportPath,
    [property: JsonPropertyName("workflowPath")] string WorkflowPath,
    [property: JsonPropertyName("parameters")] IReadOnlyList<WorkflowExportParameter> Parameters,
    [property: JsonPropertyName("reviewItems")] IReadOnlyList<WorkflowExportReview> ReviewItems,
    [property: JsonPropertyName("verifiedStepIds")] IReadOnlyList<string> VerifiedStepIds,
    [property: JsonPropertyName("validated")] bool Validated);

public sealed record WorkflowReplayRequest(
    [property: JsonPropertyName("exportPath")] string ExportPath,
    [property: JsonPropertyName("sessionId")] SessionId SessionId,
    [property: JsonPropertyName("outputDirectory")] string OutputDirectory,
    [property: JsonPropertyName("parameters")] IReadOnlyDictionary<string, string>? Parameters = null,
    [property: JsonPropertyName("validateOnly")] bool ValidateOnly = true,
    [property: JsonPropertyName("acknowledgeReview")] bool AcknowledgeReview = false,
    [property: JsonPropertyName("isolatedStateDirectory")] string? IsolatedStateDirectory = null,
    [property: JsonPropertyName("evidenceRoot")] string? EvidenceRoot = null,
    [property: JsonPropertyName("authorizedProcessId")] int? AuthorizedProcessId = null,
    [property: JsonPropertyName("allowDestructive")] bool AllowDestructive = false);
