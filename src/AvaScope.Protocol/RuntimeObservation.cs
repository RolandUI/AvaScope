using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace AvaScope.Protocol;

public sealed record RuntimeObservationRequest
{
    [JsonConstructor]
    public RuntimeObservationRequest(SessionId sessionId, IReadOnlyList<string>? topLevelIds = null,
        string? topLevelTitle = null, bool activeOnly = false, string? rootNodeId = null,
        int maxTopLevels = 4, int maxNodes = 40, int maxDepth = 4, bool includeDiagnostics = true,
        bool includeScreenshot = false, string? outputDirectory = null, RuntimeEvidencePolicy? policy = null,
        int maxInlineBytes = 32768, int timeoutMs = 5000, string? requestId = null)
    {
        SessionId = sessionId ?? throw new ArgumentNullException(nameof(sessionId));
        if (maxTopLevels is < 1 or > 8) throw new ArgumentOutOfRangeException(nameof(maxTopLevels));
        if (maxNodes is < 1 or > 256) throw new ArgumentOutOfRangeException(nameof(maxNodes));
        if (maxDepth is < 0 or > 8) throw new ArgumentOutOfRangeException(nameof(maxDepth));
        if (maxInlineBytes is < 4096 or > 131072) throw new ArgumentOutOfRangeException(nameof(maxInlineBytes));
        if (timeoutMs is < 1 or > 5000) throw new ArgumentOutOfRangeException(nameof(timeoutMs));
        if (requestId?.Length > 128 || topLevelTitle?.Length > 384 || rootNodeId?.Length > 256 || outputDirectory?.Length > 1024)
            throw new ArgumentException("Observation identifiers, title and output path exceed their bounded lengths.");
        var ids = (topLevelIds ?? []).Distinct(StringComparer.Ordinal).ToArray();
        if (ids.Length > 8 || ids.Any(id => string.IsNullOrWhiteSpace(id) || id.Length > 256)) throw new ArgumentException("Select at most eight non-empty top-level ids of at most 256 characters.", nameof(topLevelIds));
        if (rootNodeId is not null && ids.Length != 1) throw new ArgumentException("A root node requires exactly one explicit top-level id.", nameof(rootNodeId));
        if ((includeScreenshot || policy is not null) && string.IsNullOrWhiteSpace(outputDirectory))
            throw new ArgumentException("Screenshots and evidence policies require an explicit output directory.", nameof(outputDirectory));
        TopLevelIds = ids;
        TopLevelTitle = topLevelTitle;
        ActiveOnly = activeOnly;
        RootNodeId = rootNodeId;
        MaxTopLevels = maxTopLevels;
        MaxNodes = maxNodes;
        MaxDepth = maxDepth;
        IncludeDiagnostics = includeDiagnostics;
        IncludeScreenshot = includeScreenshot;
        OutputDirectory = outputDirectory is null ? null : Path.GetFullPath(outputDirectory);
        Policy = policy;
        MaxInlineBytes = maxInlineBytes;
        TimeoutMs = timeoutMs;
        RequestId = string.IsNullOrWhiteSpace(requestId) ? Guid.NewGuid().ToString("N") : requestId;
    }

    [JsonPropertyName("sessionId")] public SessionId SessionId { get; }
    [JsonPropertyName("topLevelIds")] public IReadOnlyList<string> TopLevelIds { get; }
    [JsonPropertyName("topLevelTitle")] public string? TopLevelTitle { get; }
    [JsonPropertyName("activeOnly")] public bool ActiveOnly { get; }
    [JsonPropertyName("rootNodeId")] public string? RootNodeId { get; }
    [JsonPropertyName("maxTopLevels"), Range(1, 8)] public int MaxTopLevels { get; }
    [JsonPropertyName("maxNodes"), Range(1, 256)] public int MaxNodes { get; }
    [JsonPropertyName("maxDepth"), Range(0, 8)] public int MaxDepth { get; }
    [JsonPropertyName("includeDiagnostics")] public bool IncludeDiagnostics { get; }
    [JsonPropertyName("includeScreenshot")] public bool IncludeScreenshot { get; }
    [JsonPropertyName("outputDirectory")] public string? OutputDirectory { get; }
    [JsonPropertyName("policy")] public RuntimeEvidencePolicy? Policy { get; }
    [JsonPropertyName("maxInlineBytes"), Range(4096, 131072)] public int MaxInlineBytes { get; }
    [JsonPropertyName("timeoutMs"), Range(1, 5000)] public int TimeoutMs { get; }
    [JsonPropertyName("requestId")] public string RequestId { get; }
}

public sealed record RuntimeObservedNode(
    [property: JsonPropertyName("nodeId")] string NodeId,
    [property: JsonPropertyName("parentNodeId")] string? ParentNodeId,
    [property: JsonPropertyName("depth")] int Depth,
    [property: JsonPropertyName("nodeType")] string NodeType,
    [property: JsonPropertyName("name")] string? Name,
    [property: JsonPropertyName("automationId")] string? AutomationId,
    [property: JsonPropertyName("text")] string? Text,
    [property: JsonPropertyName("bounds")] NodeBounds? Bounds,
    [property: JsonPropertyName("state")] RuntimeNodeInteractionState State,
    [property: JsonPropertyName("generation")] string Generation,
    [property: JsonPropertyName("validation")] RuntimeValidationState? Validation,
    [property: JsonPropertyName("textTruncated")] bool TextTruncated = false);

public sealed record RuntimeObservedWindow(
    [property: JsonPropertyName("topLevelId")] string TopLevelId,
    [property: JsonPropertyName("window")] TopLevelSummary? Window,
    [property: JsonPropertyName("generation")] string? Generation,
    [property: JsonPropertyName("observedAt")] DateTimeOffset ObservedAt,
    [property: JsonPropertyName("focusedNodeId")] string? FocusedNodeId,
    [property: JsonPropertyName("nodes")] IReadOnlyList<RuntimeObservedNode> Nodes,
    [property: JsonPropertyName("parts")] IReadOnlyDictionary<string, string> Parts,
    [property: JsonPropertyName("truncated")] bool Truncated,
    [property: JsonPropertyName("diagnostics")] IReadOnlyList<ProtocolError> Diagnostics,
    [property: JsonPropertyName("screenshot")] ScreenshotResponse? Screenshot = null,
    [property: JsonPropertyName("screenshotPng"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] byte[]? ScreenshotPng = null);

public sealed record RuntimeObservationResponse(
    [property: JsonPropertyName("requestId")] string RequestId,
    [property: JsonPropertyName("observationId")] string ObservationId,
    [property: JsonPropertyName("sessionId")] SessionId SessionId,
    [property: JsonPropertyName("startedAt")] DateTimeOffset StartedAt,
    [property: JsonPropertyName("completedAt")] DateTimeOffset CompletedAt,
    [property: JsonPropertyName("capabilityRevision")] string CapabilityRevision,
    [property: JsonPropertyName("windows")] IReadOnlyList<RuntimeObservedWindow> Windows,
    [property: JsonPropertyName("consistency")] string Consistency,
    [property: JsonPropertyName("changedDuringCollection")] bool? ChangedDuringCollection,
    [property: JsonPropertyName("truncated")] bool Truncated,
    [property: JsonPropertyName("diagnostics")] IReadOnlyList<ProtocolError> Diagnostics,
    [property: JsonPropertyName("responseBudget")] ResponseBudgetInfo? ResponseBudget = null);
