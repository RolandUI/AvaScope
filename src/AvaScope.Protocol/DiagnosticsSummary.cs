using System.Text.Json.Serialization;

namespace AvaScope.Protocol;

public sealed record DiagnosticsSummary
{
    [JsonConstructor]
    public DiagnosticsSummary(
        int bridgeSessionCount,
        int activeBridgeSessionCount,
        int staleBridgeSessionCount,
        int invalidBridgeSessionCount,
        int unavailableBridgeSessionCount,
        int inactiveBridgeSessionCount,
        int previewSessionCount,
        int activePreviewSessionCount,
        int stalePreviewSessionCount,
        int invalidPreviewSessionCount,
        int unavailablePreviewSessionCount,
        int diagnosticIssueCount,
        IReadOnlyList<string>? nextCommands = null)
    {
        if (bridgeSessionCount < 0
            || activeBridgeSessionCount < 0
            || staleBridgeSessionCount < 0
            || invalidBridgeSessionCount < 0
            || unavailableBridgeSessionCount < 0
            || inactiveBridgeSessionCount < 0
            || previewSessionCount < 0
            || activePreviewSessionCount < 0
            || stalePreviewSessionCount < 0
            || invalidPreviewSessionCount < 0
            || unavailablePreviewSessionCount < 0
            || diagnosticIssueCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(bridgeSessionCount), "Diagnostic summary counts cannot be negative.");
        }

        BridgeSessionCount = bridgeSessionCount;
        ActiveBridgeSessionCount = activeBridgeSessionCount;
        StaleBridgeSessionCount = staleBridgeSessionCount;
        InvalidBridgeSessionCount = invalidBridgeSessionCount;
        UnavailableBridgeSessionCount = unavailableBridgeSessionCount;
        InactiveBridgeSessionCount = inactiveBridgeSessionCount;
        PreviewSessionCount = previewSessionCount;
        ActivePreviewSessionCount = activePreviewSessionCount;
        StalePreviewSessionCount = stalePreviewSessionCount;
        InvalidPreviewSessionCount = invalidPreviewSessionCount;
        UnavailablePreviewSessionCount = unavailablePreviewSessionCount;
        DiagnosticIssueCount = diagnosticIssueCount;
        NextCommands = nextCommands ?? [];
    }

    [JsonPropertyName("bridgeSessionCount")]
    public int BridgeSessionCount { get; }

    [JsonPropertyName("activeBridgeSessionCount")]
    public int ActiveBridgeSessionCount { get; }

    [JsonPropertyName("staleBridgeSessionCount")]
    public int StaleBridgeSessionCount { get; }

    [JsonPropertyName("invalidBridgeSessionCount")]
    public int InvalidBridgeSessionCount { get; }

    [JsonPropertyName("unavailableBridgeSessionCount")]
    public int UnavailableBridgeSessionCount { get; }

    [JsonPropertyName("inactiveBridgeSessionCount")]
    public int InactiveBridgeSessionCount { get; }

    [JsonPropertyName("previewSessionCount")]
    public int PreviewSessionCount { get; }

    [JsonPropertyName("activePreviewSessionCount")]
    public int ActivePreviewSessionCount { get; }

    [JsonPropertyName("stalePreviewSessionCount")]
    public int StalePreviewSessionCount { get; }

    [JsonPropertyName("invalidPreviewSessionCount")]
    public int InvalidPreviewSessionCount { get; }

    [JsonPropertyName("unavailablePreviewSessionCount")]
    public int UnavailablePreviewSessionCount { get; }

    [JsonPropertyName("diagnosticIssueCount")]
    public int DiagnosticIssueCount { get; }

    [JsonPropertyName("nextCommands")]
    public IReadOnlyList<string> NextCommands { get; }

    public static DiagnosticsSummary Create(
        IReadOnlyList<BridgeSessionDiagnostic> bridgeSessions,
        IReadOnlyList<PreviewSessionDiagnostic> previewSessions,
        IReadOnlyList<DiagnosticIssue> diagnosticIssues)
    {
        ArgumentNullException.ThrowIfNull(bridgeSessions);
        ArgumentNullException.ThrowIfNull(previewSessions);
        ArgumentNullException.ThrowIfNull(diagnosticIssues);

        var activeBridge = bridgeSessions.Count(static session => session.Status == DiagnosticStatuses.Available);
        var staleBridge = bridgeSessions.Count(static session => session.Status == DiagnosticStatuses.Stale);
        var invalidBridge = bridgeSessions.Count(static session => session.Status is DiagnosticStatuses.Invalid or DiagnosticStatuses.Unauthorized);
        var unavailableBridge = bridgeSessions.Count(static session => session.Status is DiagnosticStatuses.Unavailable or DiagnosticStatuses.Incompatible);
        var activePreview = previewSessions.Count(static session => session.Status == DiagnosticStatuses.Available);
        var stalePreview = previewSessions.Count(static session => session.Status == DiagnosticStatuses.Stale);
        var invalidPreview = previewSessions.Count(static session => session.Status is DiagnosticStatuses.Invalid or DiagnosticStatuses.Unauthorized);
        var unavailablePreview = previewSessions.Count(static session => session.Status is DiagnosticStatuses.Unavailable or DiagnosticStatuses.Incompatible);
        var inactiveBridge = bridgeSessions.Count - activeBridge;

        var nextCommands = new List<string>();
        if (activeBridge == 0)
        {
            nextCommands.Add("avascope launch-app --command <bridge-enabled-app>");
            nextCommands.Add("avascope attach --latest true");
        }

        if (activePreview == 0)
        {
            nextCommands.Add("avascope create-preview-session <project.csproj> --view <view.axaml> --out <preview.png>");
        }

        if (staleBridge + invalidBridge > 0)
        {
            nextCommands.Add("avascope cleanup-bridge-sessions");
        }

        if (stalePreview + invalidPreview > 0)
        {
            nextCommands.Add("avascope cleanup");
        }

        return new DiagnosticsSummary(
            bridgeSessions.Count,
            activeBridge,
            staleBridge,
            invalidBridge,
            unavailableBridge,
            inactiveBridge,
            previewSessions.Count,
            activePreview,
            stalePreview,
            invalidPreview,
            unavailablePreview,
            diagnosticIssues.Count,
            nextCommands);
    }
}
