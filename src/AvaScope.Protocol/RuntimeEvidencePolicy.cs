using System.Text.Json.Serialization;

namespace AvaScope.Protocol;

public sealed record RuntimeEvidencePolicy
{
    public const int MaximumRedactionEntries = 128;
    public const int MaximumMaskRegions = 64;
    public const int MaximumAuthorizedProcesses = 32;
    public const int MaximumOwnedRuns = 1000;

    private static readonly string[] SafeDefaultActions =
    [
        SemanticWorkflowActions.AssertState,
        SemanticWorkflowActions.Screenshot,
        SemanticWorkflowActions.Inspect,
        SemanticWorkflowActions.Wait,
        SemanticWorkflowActions.WaitForNode,
        SemanticWorkflowActions.WaitForState,
        SemanticWorkflowActions.WaitForDialog,
        SemanticWorkflowActions.ValidateAction,
        SemanticWorkflowActions.ValidateMutation,
        SemanticWorkflowActions.CustomActions,
        SemanticWorkflowActions.If,
        SemanticWorkflowActions.RetryUntil,
        SemanticWorkflowActions.UseFragment
    ];

    [JsonConstructor]
    public RuntimeEvidencePolicy(
        string ownedEvidenceRoot,
        IReadOnlyList<string>? redactedText = null,
        IReadOnlyList<string>? redactedAutomationIds = null,
        IReadOnlyList<string>? excludedControlAutomationIds = null,
        IReadOnlyList<ScreenshotRegion>? screenshotMaskRegions = null,
        IReadOnlyList<string>? allowedActions = null,
        IReadOnlyList<string>? allowedCustomActions = null,
        bool allowGestures = false,
        bool allowDestructiveActions = false,
        IReadOnlyList<string>? authorizedSessionIds = null,
        IReadOnlyList<int>? authorizedProcessIds = null,
        int? retentionMaxAgeMinutes = null,
        int? retentionMaxOwnedRuns = null,
        bool writeActionAudit = true,
        bool networkUpload = false)
    {
        if (string.IsNullOrWhiteSpace(ownedEvidenceRoot))
        {
            throw new ArgumentException("Owned evidence root cannot be empty.", nameof(ownedEvidenceRoot));
        }

        if (retentionMaxAgeMinutes is < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(retentionMaxAgeMinutes), retentionMaxAgeMinutes, "Retention age must be positive.");
        }

        if (retentionMaxOwnedRuns is < 1 or > MaximumOwnedRuns)
        {
            throw new ArgumentOutOfRangeException(nameof(retentionMaxOwnedRuns), retentionMaxOwnedRuns, $"Retention run count must be between 1 and {MaximumOwnedRuns}.");
        }

        if (networkUpload)
        {
            throw new ArgumentException("Runtime evidence network upload is unavailable. Evidence remains local-only.", nameof(networkUpload));
        }

        OwnedEvidenceRoot = Path.GetFullPath(ownedEvidenceRoot);
        RedactedText = Normalize(redactedText, nameof(redactedText));
        RedactedAutomationIds = Normalize(redactedAutomationIds, nameof(redactedAutomationIds));
        ExcludedControlAutomationIds = Normalize(excludedControlAutomationIds, nameof(excludedControlAutomationIds));
        ScreenshotMaskRegions = (screenshotMaskRegions ?? [])
            .Take(MaximumMaskRegions + 1)
            .ToArray();
        if (ScreenshotMaskRegions.Count > MaximumMaskRegions)
        {
            throw new ArgumentException($"At most {MaximumMaskRegions} screenshot mask regions are supported.", nameof(screenshotMaskRegions));
        }

        var normalizedActions = Normalize(allowedActions ?? SafeDefaultActions, nameof(allowedActions));
        var unsupportedAction = normalizedActions.FirstOrDefault(action => !SemanticWorkflowActions.All.Contains(action, StringComparer.Ordinal));
        if (unsupportedAction is not null)
        {
            throw new ArgumentException($"Workflow action '{unsupportedAction}' is not supported.", nameof(allowedActions));
        }

        AllowedActions = normalizedActions;
        AllowedCustomActions = Normalize(allowedCustomActions, nameof(allowedCustomActions));
        AllowGestures = allowGestures;
        AllowDestructiveActions = allowDestructiveActions;
        AuthorizedSessionIds = Normalize(authorizedSessionIds, nameof(authorizedSessionIds));
        AuthorizedProcessIds = (authorizedProcessIds ?? [])
            .Distinct()
            .Take(MaximumAuthorizedProcesses + 1)
            .ToArray();
        if (AuthorizedProcessIds.Count > MaximumAuthorizedProcesses || AuthorizedProcessIds.Any(static processId => processId < 1))
        {
            throw new ArgumentException($"Authorized process ids must contain at most {MaximumAuthorizedProcesses} positive values.", nameof(authorizedProcessIds));
        }

        RetentionMaxAgeMinutes = retentionMaxAgeMinutes;
        RetentionMaxOwnedRuns = retentionMaxOwnedRuns;
        WriteActionAudit = writeActionAudit;
        NetworkUpload = false;
    }

    [JsonPropertyName("ownedEvidenceRoot")]
    public string OwnedEvidenceRoot { get; }

    [JsonPropertyName("redactedText")]
    public IReadOnlyList<string> RedactedText { get; }

    [JsonPropertyName("redactedAutomationIds")]
    public IReadOnlyList<string> RedactedAutomationIds { get; }

    [JsonPropertyName("excludedControlAutomationIds")]
    public IReadOnlyList<string> ExcludedControlAutomationIds { get; }

    [JsonPropertyName("screenshotMaskRegions")]
    public IReadOnlyList<ScreenshotRegion> ScreenshotMaskRegions { get; }

    [JsonPropertyName("allowedActions")]
    public IReadOnlyList<string> AllowedActions { get; }

    [JsonPropertyName("allowedCustomActions")]
    public IReadOnlyList<string> AllowedCustomActions { get; }

    [JsonPropertyName("allowGestures")]
    public bool AllowGestures { get; }

    [JsonPropertyName("allowDestructiveActions")]
    public bool AllowDestructiveActions { get; }

    [JsonPropertyName("authorizedSessionIds")]
    public IReadOnlyList<string> AuthorizedSessionIds { get; }

    [JsonPropertyName("authorizedProcessIds")]
    public IReadOnlyList<int> AuthorizedProcessIds { get; }

    [JsonPropertyName("retentionMaxAgeMinutes")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? RetentionMaxAgeMinutes { get; }

    [JsonPropertyName("retentionMaxOwnedRuns")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? RetentionMaxOwnedRuns { get; }

    [JsonPropertyName("writeActionAudit")]
    public bool WriteActionAudit { get; }

    [JsonPropertyName("networkUpload")]
    public bool NetworkUpload { get; }

    private static IReadOnlyList<string> Normalize(IReadOnlyList<string>? values, string parameterName)
    {
        var normalized = (values ?? [])
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .Select(static value => value.Trim())
            .Distinct(StringComparer.Ordinal)
            .Take(MaximumRedactionEntries + 1)
            .ToArray();
        if (normalized.Length > MaximumRedactionEntries)
        {
            throw new ArgumentException($"At most {MaximumRedactionEntries} entries are supported.", parameterName);
        }

        return normalized;
    }
}
