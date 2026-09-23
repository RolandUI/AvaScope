using System.Text.Json.Serialization;

namespace AvaScope.Protocol;

public sealed record RuntimeNavigationRequest
{
    [JsonConstructor]
    public RuntimeNavigationRequest(SessionId sessionId, string action = "query", string? runId = null,
        IReadOnlyList<string>? topLevelIds = null, int maxNodes = 24, int maxDepth = 4, int ttlMs = 120000,
        RuntimeTargetContext? identityTarget = null, string? visitLabel = null, string? previousVisitId = null,
        RuntimeNavigationAction? transition = null, string? visitId = null, string? stateKey = null,
        string? fromVisitId = null, string? toVisitId = null, int maxVisits = 16, bool includeEvidence = false,
        RuntimeEvidencePolicy? policy = null)
    {
        SessionId = sessionId ?? throw new ArgumentNullException(nameof(sessionId));
        if (action is not ("start" or "record" or "query" or "clear")) throw new ArgumentException("Navigation action must be start, record, query or clear.");
        if (action == "start" ? runId is not null : !Guid.TryParseExact(runId, "N", out _))
            throw new ArgumentException("Start creates a new run; other operations require its exact runId.");
        if (maxNodes is < 1 or > 64 || maxDepth is < 0 or > 8 || maxVisits is < 1 or > 32 || ttlMs is < 100 or > 1800000)
            throw new ArgumentOutOfRangeException(nameof(maxNodes), "Limits: 1..64 nodes, depth 0..8, 1..32 returned visits, TTL 100..1800000 ms.");
        var ids = (topLevelIds ?? []).Distinct(StringComparer.Ordinal).ToArray();
        if (ids.Length > 4 || ids.Any(id => string.IsNullOrWhiteSpace(id) || id.Length > 256))
            throw new ArgumentException("Select at most four top-level ids.");
        if (action != "start" && (ids.Length != 0 || maxNodes != 24 || maxDepth != 4 || ttlMs != 120000))
            throw new ArgumentException("Observation scope, limits and expiry are fixed when starting the run.");
        if (action == "record" ? !Guid.TryParseExact(previousVisitId, "N", out _) : previousVisitId is not null || transition is not null)
            throw new ArgumentException("Recording requires the current previousVisitId; only records may declare a transition.");
        if (action is not ("start" or "record") && (identityTarget is not null || visitLabel is not null))
            throw new ArgumentException("Identity targets and labels apply only to a new visit.");
        if (identityTarget is not null && (identityTarget.SessionId != sessionId || identityTarget.TreeKind != TreeKinds.Visual
            || string.IsNullOrEmpty(identityTarget.NodeId) || string.IsNullOrEmpty(identityTarget.NodeGeneration) || string.IsNullOrEmpty(identityTarget.TopLevelGeneration)))
            throw new ArgumentException("Host identity requires a fresh visual node target with both generations.");
        if (visitLabel?.Length > 256) throw new ArgumentException("Visit labels are limited to 256 characters.");
        foreach (var id in new[] { visitId, fromVisitId, toVisitId })
            if (id is not null && !Guid.TryParseExact(id, "N", out _)) throw new ArgumentException("Use exact retained visit ids.");
        if (stateKey is not null && (stateKey.Length != 64 || stateKey.Any(c => !char.IsAsciiHexDigit(c))))
            throw new ArgumentException("Use the stateKey returned by a retained visit.");
        if ((fromVisitId is null) != (toVisitId is null) || visitId is not null && (stateKey is not null || fromVisitId is not null)
            || stateKey is not null && fromVisitId is not null || action != "query" && (visitId is not null || stateKey is not null || fromVisitId is not null))
            throw new ArgumentException("Queries select one visit, a state key, or both route endpoint visits.");
        Action = action; RunId = runId; TopLevelIds = ids; MaxNodes = maxNodes; MaxDepth = maxDepth; TtlMs = ttlMs;
        IdentityTarget = identityTarget; VisitLabel = visitLabel; PreviousVisitId = previousVisitId; Transition = transition;
        VisitId = visitId; StateKey = stateKey; FromVisitId = fromVisitId; ToVisitId = toVisitId;
        MaxVisits = maxVisits; IncludeEvidence = includeEvidence; Policy = policy;
    }

    [JsonPropertyName("sessionId")] public SessionId SessionId { get; }
    [JsonPropertyName("action")] public string Action { get; }
    [JsonPropertyName("runId")] public string? RunId { get; }
    [JsonPropertyName("topLevelIds")] public IReadOnlyList<string> TopLevelIds { get; }
    [JsonPropertyName("maxNodes")] public int MaxNodes { get; }
    [JsonPropertyName("maxDepth")] public int MaxDepth { get; }
    [JsonPropertyName("ttlMs")] public int TtlMs { get; }
    [JsonPropertyName("identityTarget")] public RuntimeTargetContext? IdentityTarget { get; }
    [JsonPropertyName("visitLabel")] public string? VisitLabel { get; }
    [JsonPropertyName("previousVisitId")] public string? PreviousVisitId { get; }
    [JsonPropertyName("transition")] public RuntimeNavigationAction? Transition { get; }
    [JsonPropertyName("visitId")] public string? VisitId { get; }
    [JsonPropertyName("stateKey")] public string? StateKey { get; }
    [JsonPropertyName("fromVisitId")] public string? FromVisitId { get; }
    [JsonPropertyName("toVisitId")] public string? ToVisitId { get; }
    [JsonPropertyName("maxVisits")] public int MaxVisits { get; }
    [JsonPropertyName("includeEvidence")] public bool IncludeEvidence { get; }
    [JsonPropertyName("policy")] public RuntimeEvidencePolicy? Policy { get; }
}

public sealed record RuntimeNavigationAction
{
    [JsonConstructor]
    public RuntimeNavigationAction(string description, string outcome = "unknown", string? requestId = null)
    {
        if (string.IsNullOrWhiteSpace(description) || description.Length > 256 || requestId?.Length > 128)
            throw new ArgumentException("Record a bounded action description (256) and optional correlation id (128), without action arguments.");
        if (outcome is not ("succeeded" or "failed" or "unknown")) throw new ArgumentException("Action outcome must be succeeded, failed or unknown.");
        Description = description; Outcome = outcome; RequestId = requestId;
    }
    [JsonPropertyName("description")] public string Description { get; }
    [JsonPropertyName("outcome")] public string Outcome { get; }
    [JsonPropertyName("requestId")] public string? RequestId { get; }
}

public sealed record RuntimeNavigationIdentity(
    [property: JsonPropertyName("surface")] string Surface,
    [property: JsonPropertyName("context")] string Context,
    [property: JsonPropertyName("revision")] string Revision);

public sealed record RuntimeNavigationStep(
    [property: JsonPropertyName("fromVisitId")] string FromVisitId,
    [property: JsonPropertyName("toVisitId")] string ToVisitId,
    [property: JsonPropertyName("action")] RuntimeNavigationAction Action,
    [property: JsonPropertyName("beforeObservationId")] string BeforeObservationId,
    [property: JsonPropertyName("afterObservationId")] string AfterObservationId,
    [property: JsonPropertyName("provenance")] string Provenance = "caller_reported_action_between_bridge_observations; causality_not_verified");

public sealed record RuntimeNavigationVisit(
    [property: JsonPropertyName("visitId")] string VisitId,
    [property: JsonPropertyName("sequence")] long Sequence,
    [property: JsonPropertyName("stateKey")] string StateKey,
    [property: JsonPropertyName("label")] string? Label,
    [property: JsonPropertyName("identity")] RuntimeNavigationIdentity? Identity,
    [property: JsonPropertyName("equivalence")] string Equivalence,
    [property: JsonPropertyName("observedAt")] DateTimeOffset ObservedAt,
    [property: JsonPropertyName("observationId")] string ObservationId,
    [property: JsonPropertyName("observationRevision")] string ObservationRevision,
    [property: JsonPropertyName("coverage")] string Coverage,
    [property: JsonPropertyName("arrivedVia")] RuntimeNavigationStep? ArrivedVia,
    [property: JsonPropertyName("observation")] RuntimeObservationResponse? Observation,
    [property: JsonPropertyName("unavailable")] IReadOnlyList<string> Unavailable);

public sealed record RuntimeNavigationLoop(
    [property: JsonPropertyName("fromVisitId")] string FromVisitId,
    [property: JsonPropertyName("toVisitId")] string ToVisitId,
    [property: JsonPropertyName("length")] int Length,
    [property: JsonPropertyName("repetitions")] int Repetitions,
    [property: JsonPropertyName("confidence")] string Confidence,
    [property: JsonPropertyName("outcomes")] string Outcomes);

public sealed record RuntimeNavigationRoute(
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("visitIds")] IReadOnlyList<string> VisitIds,
    [property: JsonPropertyName("steps")] IReadOnlyList<RuntimeNavigationStep> Steps,
    [property: JsonPropertyName("complete")] bool Complete,
    [property: JsonPropertyName("provenance")] string Provenance = "retained_observed_path; not_a_future_execution_plan");

public sealed record RuntimeNavigationResponse(
    [property: JsonPropertyName("runId")] string RunId,
    [property: JsonPropertyName("sessionId")] SessionId SessionId,
    [property: JsonPropertyName("expiresAt")] DateTimeOffset ExpiresAt,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("currentVisitId")] string? CurrentVisitId,
    [property: JsonPropertyName("visits")] IReadOnlyList<RuntimeNavigationVisit> Visits,
    [property: JsonPropertyName("loops")] IReadOnlyList<RuntimeNavigationLoop> Loops,
    [property: JsonPropertyName("route")] RuntimeNavigationRoute? Route,
    [property: JsonPropertyName("retainedVisits")] int RetainedVisits,
    [property: JsonPropertyName("droppedVisits")] long DroppedVisits,
    [property: JsonPropertyName("retainedBytes")] int RetainedBytes,
    [property: JsonPropertyName("hasMore")] bool HasMore,
    [property: JsonPropertyName("diagnostics")] IReadOnlyList<ProtocolError> Diagnostics);
