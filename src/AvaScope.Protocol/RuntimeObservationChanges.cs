using System.Text.Json.Serialization;

namespace AvaScope.Protocol;

public sealed record RuntimeObservationChangesRequest
{
    [JsonConstructor]
    public RuntimeObservationChangesRequest(RuntimeObservationRequest observation, string? cursor = null,
        int waitMs = 0, int pollIntervalMs = 100, int maxEvents = 32, int cursorTtlMs = 120000)
    {
        Observation = observation ?? throw new ArgumentNullException(nameof(observation));
        if (observation.IncludeScreenshot) throw new ArgumentException("Change observations do not retain images; use observe for an explicit screenshot.", nameof(observation));
        if (cursor?.Length > 128) throw new ArgumentException("The observation cursor is too long.", nameof(cursor));
        if (waitMs is < 0 or > 3000) throw new ArgumentOutOfRangeException(nameof(waitMs));
        if (pollIntervalMs is < 25 or > 1000) throw new ArgumentOutOfRangeException(nameof(pollIntervalMs));
        if (maxEvents is < 1 or > 128) throw new ArgumentOutOfRangeException(nameof(maxEvents));
        if (cursorTtlMs is < 100 or > 300000) throw new ArgumentOutOfRangeException(nameof(cursorTtlMs));
        Cursor = string.IsNullOrWhiteSpace(cursor) ? null : cursor;
        WaitMs = waitMs;
        PollIntervalMs = pollIntervalMs;
        MaxEvents = maxEvents;
        CursorTtlMs = cursorTtlMs;
    }

    [JsonPropertyName("observation")] public RuntimeObservationRequest Observation { get; }
    [JsonPropertyName("cursor")] public string? Cursor { get; }
    [JsonPropertyName("waitMs")] public int WaitMs { get; }
    [JsonPropertyName("pollIntervalMs")] public int PollIntervalMs { get; }
    [JsonPropertyName("maxEvents")] public int MaxEvents { get; }
    [JsonPropertyName("cursorTtlMs")] public int CursorTtlMs { get; }
}

public sealed record RuntimeObservationChange(
    [property: JsonPropertyName("sequence")] long Sequence,
    [property: JsonPropertyName("kind")] string Kind,
    [property: JsonPropertyName("topLevelId")] string TopLevelId,
    [property: JsonPropertyName("observedAt")] DateTimeOffset ObservedAt,
    [property: JsonPropertyName("nodeId")] string? NodeId = null,
    [property: JsonPropertyName("node")] RuntimeObservedNode? Node = null,
    [property: JsonPropertyName("window")] RuntimeObservedWindow? Window = null,
    [property: JsonPropertyName("changedFields")] IReadOnlyList<string>? ChangedFields = null);

public sealed record RuntimeObservationChangesResponse(
    [property: JsonPropertyName("requestId")] string RequestId,
    [property: JsonPropertyName("sessionId")] SessionId SessionId,
    [property: JsonPropertyName("cursor")] string Cursor,
    [property: JsonPropertyName("cursorExpiresAt")] DateTimeOffset CursorExpiresAt,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("resyncRequired")] bool ResyncRequired,
    [property: JsonPropertyName("resyncReason")] string? ResyncReason,
    [property: JsonPropertyName("events")] IReadOnlyList<RuntimeObservationChange> Events,
    [property: JsonPropertyName("baseline")] RuntimeObservationResponse? Baseline,
    [property: JsonPropertyName("hasMore")] bool HasMore,
    [property: JsonPropertyName("latestSequence")] long LatestSequence,
    [property: JsonPropertyName("oldestAvailableSequence")] long OldestAvailableSequence,
    [property: JsonPropertyName("droppedEvents")] long DroppedEvents,
    [property: JsonPropertyName("retainedEvents")] int RetainedEvents,
    [property: JsonPropertyName("retainedBytes")] int RetainedBytes,
    [property: JsonPropertyName("samplesTaken")] int SamplesTaken,
    [property: JsonPropertyName("coverage")] string Coverage = "sampled_state_changes_intermediate_states_may_be_coalesced",
    [property: JsonPropertyName("requiresArtifactRead")] bool RequiresArtifactRead = false,
    [property: JsonPropertyName("responseBudget")] ResponseBudgetInfo? ResponseBudget = null);
