using System.Text.Json.Serialization;

namespace AvaScope.Protocol;

public sealed record RuntimeReadinessStage(
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("source")] string Source,
    [property: JsonPropertyName("observedAt")] DateTimeOffset ObservedAt,
    [property: JsonPropertyName("reason")] string? Reason = null);

public sealed record RuntimeReadinessSnapshot(
    [property: JsonPropertyName("sessionId")] SessionId SessionId,
    [property: JsonPropertyName("topLevelId")] string TopLevelId,
    [property: JsonPropertyName("bridge")] RuntimeReadinessStage Bridge,
    [property: JsonPropertyName("window")] RuntimeReadinessStage Window,
    [property: JsonPropertyName("frame")] RuntimeReadinessStage Frame,
    [property: JsonPropertyName("application")] RuntimeReadinessStage Application,
    [property: JsonPropertyName("layoutValid")] bool LayoutValid,
    [property: JsonPropertyName("layoutFingerprint")] string? LayoutFingerprint = null,
    [property: JsonPropertyName("frameFingerprint")] string? FrameFingerprint = null,
    [property: JsonPropertyName("sampledNodes")] int SampledNodes = 0,
    [property: JsonPropertyName("truncated")] bool Truncated = false,
    [property: JsonPropertyName("sampleReason")] string? SampleReason = null,
    [property: JsonPropertyName("target")] RuntimeTargetContext? Target = null,
    [property: JsonPropertyName("backend")] RuntimeBackendInfo? Backend = null,
    [property: JsonPropertyName("frameFingerprintSource")] string? FrameFingerprintSource = null);

public sealed record RuntimeReadinessProbeOptions
{
    [JsonConstructor]
    public RuntimeReadinessProbeOptions(bool waitForFrame = false, bool includeFrameHash = false, int timeoutMs = 1000)
    {
        if (timeoutMs is < 1 or > 5000)
            throw new ArgumentOutOfRangeException(nameof(timeoutMs), "Frame observation timeout must be between 1 and 5000 ms.");
        WaitForFrame = waitForFrame || includeFrameHash;
        IncludeFrameHash = includeFrameHash;
        TimeoutMs = timeoutMs;
    }

    [JsonPropertyName("waitForFrame")] public bool WaitForFrame { get; }
    [JsonPropertyName("includeFrameHash")] public bool IncludeFrameHash { get; }
    [JsonPropertyName("timeoutMs")] public int TimeoutMs { get; }
}

public sealed record RuntimeStartupReadinessOptions
{
    [JsonConstructor]
    public RuntimeStartupReadinessOptions(bool waitForFrame = true, bool waitForApplication = false,
        bool waitForStableLayout = false, int timeoutMs = 10000)
    {
        if (timeoutMs is < 1 or > 60000)
            throw new ArgumentOutOfRangeException(nameof(timeoutMs), "Startup readiness timeout must be between 1 and 60000 ms.");
        WaitForFrame = waitForFrame;
        WaitForApplication = waitForApplication;
        WaitForStableLayout = waitForStableLayout;
        TimeoutMs = timeoutMs;
    }

    [JsonPropertyName("waitForFrame")] public bool WaitForFrame { get; }
    [JsonPropertyName("waitForApplication")] public bool WaitForApplication { get; }
    [JsonPropertyName("waitForStableLayout")] public bool WaitForStableLayout { get; }
    [JsonPropertyName("timeoutMs")] public int TimeoutMs { get; }
}
