using System.Text.Json.Serialization;

namespace AvaScope.Protocol;

public sealed record PreviewLifecycleStatus
{
    public static PreviewLifecycleStatus OneShotIsolated { get; } = new(
        "one_shot_isolated_child_process",
        persistentHostEnabled: false,
        "Each preview reload launches an isolated PreviewHost child process and waits for it to exit; close-preview-session closes metadata only.",
        "No persistent preview host TTL is active; watch timeout bounds file watching, not a long-lived user-code process.",
        "A failed child process is captured as the latest render result and session state; no persistent host restart loop exists.",
        "PreviewHostClient deletes request temp directories; cleanup removes stale AvaScope preview-session metadata only.",
        "Design explicit persistent-host ownership, close, TTL, crash, and cleanup semantics before enabling persistent preview hosts.");

    [JsonConstructor]
    public PreviewLifecycleStatus(
        string hostProcessMode,
        bool persistentHostEnabled,
        string closeSemantics,
        string ttlSemantics,
        string crashSemantics,
        string cleanupSemantics,
        string nextStep)
    {
        if (string.IsNullOrWhiteSpace(hostProcessMode))
        {
            throw new ArgumentException("Host process mode cannot be empty.", nameof(hostProcessMode));
        }

        if (string.IsNullOrWhiteSpace(closeSemantics))
        {
            throw new ArgumentException("Close semantics cannot be empty.", nameof(closeSemantics));
        }

        if (string.IsNullOrWhiteSpace(ttlSemantics))
        {
            throw new ArgumentException("TTL semantics cannot be empty.", nameof(ttlSemantics));
        }

        if (string.IsNullOrWhiteSpace(crashSemantics))
        {
            throw new ArgumentException("Crash semantics cannot be empty.", nameof(crashSemantics));
        }

        if (string.IsNullOrWhiteSpace(cleanupSemantics))
        {
            throw new ArgumentException("Cleanup semantics cannot be empty.", nameof(cleanupSemantics));
        }

        if (string.IsNullOrWhiteSpace(nextStep))
        {
            throw new ArgumentException("Next step cannot be empty.", nameof(nextStep));
        }

        HostProcessMode = hostProcessMode;
        PersistentHostEnabled = persistentHostEnabled;
        CloseSemantics = closeSemantics;
        TtlSemantics = ttlSemantics;
        CrashSemantics = crashSemantics;
        CleanupSemantics = cleanupSemantics;
        NextStep = nextStep;
    }

    [JsonPropertyName("hostProcessMode")]
    public string HostProcessMode { get; }

    [JsonPropertyName("persistentHostEnabled")]
    public bool PersistentHostEnabled { get; }

    [JsonPropertyName("closeSemantics")]
    public string CloseSemantics { get; }

    [JsonPropertyName("ttlSemantics")]
    public string TtlSemantics { get; }

    [JsonPropertyName("crashSemantics")]
    public string CrashSemantics { get; }

    [JsonPropertyName("cleanupSemantics")]
    public string CleanupSemantics { get; }

    [JsonPropertyName("nextStep")]
    public string NextStep { get; }
}
