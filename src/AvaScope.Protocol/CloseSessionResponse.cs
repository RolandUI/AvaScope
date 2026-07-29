using System.Text.Json.Serialization;

namespace AvaScope.Protocol;

public sealed record CloseSessionResponse
{
    [JsonConstructor]
    public CloseSessionResponse(
        SessionSummary session,
        int processId,
        DateTimeOffset closedAt,
        bool terminateLaunchedProcessRequested = false,
        string outcome = CloseSessionOutcomes.ClosedOnly,
        bool launchedProcessOwned = false,
        bool processTerminated = false,
        string? terminationMessage = null)
    {
        ArgumentNullException.ThrowIfNull(session);

        if (processId < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(processId), processId, "Process id must be positive.");
        }

        Session = session;
        ProcessId = processId;
        ClosedAt = closedAt;
        TerminateLaunchedProcessRequested = terminateLaunchedProcessRequested;
        Outcome = outcome;
        LaunchedProcessOwned = launchedProcessOwned;
        ProcessTerminated = processTerminated;
        TerminationMessage = string.IsNullOrWhiteSpace(terminationMessage) ? null : terminationMessage;
    }

    [JsonPropertyName("session")]
    public SessionSummary Session { get; }

    [JsonPropertyName("processId")]
    public int ProcessId { get; }

    [JsonPropertyName("closedAt")]
    public DateTimeOffset ClosedAt { get; }

    [JsonPropertyName("terminateLaunchedProcessRequested")]
    public bool TerminateLaunchedProcessRequested { get; }

    [JsonPropertyName("outcome")]
    public string Outcome { get; }

    [JsonPropertyName("launchedProcessOwned")]
    public bool LaunchedProcessOwned { get; }

    [JsonPropertyName("processTerminated")]
    public bool ProcessTerminated { get; }

    [JsonPropertyName("terminationMessage")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? TerminationMessage { get; }
}

public static class CloseSessionOutcomes
{
    public const string ClosedOnly = "closed_only";
    public const string Terminated = "terminated";
    public const string AlreadyExited = "already_exited";
    public const string NotOwned = "not_owned";
    public const string TerminationFailed = "termination_failed";
}
