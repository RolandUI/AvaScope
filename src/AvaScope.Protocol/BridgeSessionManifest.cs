using System.Text.Json.Serialization;

namespace AvaScope.Protocol;

public sealed record BridgeSessionManifest
{
    private const string RootDirectoryName = "AvaScope";
    private const string SessionDirectoryName = "sessions";
    public const string DirectoryEnvironmentVariable = "AVASCOPE_BRIDGE_MANIFEST_DIR";

    [JsonConstructor]
    public BridgeSessionManifest(
        SessionId sessionId,
        int processId,
        string pipeName,
        DateTimeOffset createdAt,
        string? displayName = null,
        string? transportScope = null,
        string? processName = null)
    {
        SessionId = sessionId ?? throw new ArgumentNullException(nameof(sessionId));

        if (processId < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(processId), processId, "Process id must be positive.");
        }

        if (string.IsNullOrWhiteSpace(pipeName))
        {
            throw new ArgumentException("Pipe name cannot be empty.", nameof(pipeName));
        }

        ProcessId = processId;
        PipeName = pipeName;
        CreatedAt = createdAt;
        DisplayName = displayName;
        ProcessName = string.IsNullOrWhiteSpace(processName) ? null : processName.Trim();
        TransportScope = string.IsNullOrWhiteSpace(transportScope)
            ? BridgeTransportScopes.LocalOnly
            : transportScope;

        if (!string.Equals(TransportScope, BridgeTransportScopes.LocalOnly, StringComparison.Ordinal))
        {
            throw new ArgumentException(
                $"Bridge transport scope '{TransportScope}' is not supported.",
                nameof(transportScope));
        }
    }

    [JsonPropertyName("sessionId")]
    public SessionId SessionId { get; }

    [JsonPropertyName("processId")]
    public int ProcessId { get; }

    [JsonPropertyName("pipeName")]
    public string PipeName { get; }

    [JsonPropertyName("createdAt")]
    public DateTimeOffset CreatedAt { get; }

    [JsonPropertyName("displayName")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? DisplayName { get; }

    [JsonPropertyName("processName")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ProcessName { get; }

    [JsonPropertyName("transportScope")]
    public string TransportScope { get; }

    public static string GetDefaultDirectory()
    {
        var configuredDirectory = Environment.GetEnvironmentVariable(DirectoryEnvironmentVariable);
        return string.IsNullOrWhiteSpace(configuredDirectory)
            ? Path.Combine(Path.GetTempPath(), RootDirectoryName, SessionDirectoryName)
            : configuredDirectory;
    }

    public static string GetDefaultPath(SessionId sessionId)
    {
        ArgumentNullException.ThrowIfNull(sessionId);

        return Path.Combine(GetDefaultDirectory(), $"{sessionId.Value}.json");
    }
}
