using System.Text.Json.Serialization;

namespace AvaScope.Protocol;

/// <summary>A measured observation interval, not a virtual animation-clock position.</summary>
public sealed record AnimationSampleTiming(
    [property: JsonPropertyName("requestedOffsetMs")] int RequestedOffsetMs,
    [property: JsonPropertyName("earliestElapsedMs")] double EarliestElapsedMs,
    [property: JsonPropertyName("latestElapsedMs")] double LatestElapsedMs,
    [property: JsonPropertyName("toleranceMs")] int ToleranceMs,
    [property: JsonPropertyName("origin")] string Origin)
{
    [JsonPropertyName("withinTolerance")]
    public bool WithinTolerance => double.IsFinite(EarliestElapsedMs) && double.IsFinite(LatestElapsedMs)
        && EarliestElapsedMs >= 0 && LatestElapsedMs >= EarliestElapsedMs && ToleranceMs >= 0
        && EarliestElapsedMs >= RequestedOffsetMs - ToleranceMs
        && LatestElapsedMs <= RequestedOffsetMs + ToleranceMs;
}

/// <summary>Both endpoints come from the same process's monotonic clock.</summary>
public sealed record RuntimeOperationTiming(
    [property: JsonPropertyName("startedTimestamp")] long StartedTimestamp,
    [property: JsonPropertyName("completedTimestamp")] long CompletedTimestamp,
    [property: JsonPropertyName("timestampFrequency")] long TimestampFrequency,
    [property: JsonPropertyName("processId")] int ProcessId);
