using System.Text.Json.Serialization;

namespace AvaScope.Protocol;

public sealed record RuntimeMutationResetHandoff
{
    [JsonConstructor]
    public RuntimeMutationResetHandoff(
        SessionId sessionId,
        int activeMutationCount,
        IReadOnlyList<string>? activeMutationIds = null,
        string resetMutationOperation = RuntimeMutationOperationKinds.ResetMutation,
        string resetAllOperation = RuntimeMutationOperationKinds.ResetAll,
        RuntimeTargetContext? suggestedResetAllTarget = null,
        string? nextAction = null)
    {
        if (activeMutationCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(activeMutationCount), activeMutationCount, "Active mutation count cannot be negative.");
        }

        if (string.IsNullOrWhiteSpace(resetMutationOperation))
        {
            throw new ArgumentException("Reset mutation operation cannot be empty.", nameof(resetMutationOperation));
        }

        if (string.IsNullOrWhiteSpace(resetAllOperation))
        {
            throw new ArgumentException("Reset all operation cannot be empty.", nameof(resetAllOperation));
        }

        SessionId = sessionId ?? throw new ArgumentNullException(nameof(sessionId));
        ActiveMutationCount = activeMutationCount;
        ActiveMutationIds = (activeMutationIds ?? [])
            .Where(static id => !string.IsNullOrWhiteSpace(id))
            .Select(static id => id.Trim())
            .ToArray();
        ResetMutationOperation = resetMutationOperation.Trim();
        ResetAllOperation = resetAllOperation.Trim();
        SuggestedResetAllTarget = suggestedResetAllTarget;
        NextAction = string.IsNullOrWhiteSpace(nextAction) ? null : nextAction.Trim();
    }

    [JsonPropertyName("sessionId")]
    public SessionId SessionId { get; }

    [JsonPropertyName("activeMutationCount")]
    public int ActiveMutationCount { get; }

    [JsonPropertyName("activeMutationIds")]
    public IReadOnlyList<string> ActiveMutationIds { get; }

    [JsonPropertyName("resetMutationOperation")]
    public string ResetMutationOperation { get; }

    [JsonPropertyName("resetAllOperation")]
    public string ResetAllOperation { get; }

    [JsonPropertyName("suggestedResetAllTarget")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public RuntimeTargetContext? SuggestedResetAllTarget { get; }

    [JsonPropertyName("nextAction")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? NextAction { get; }
}
