using System.Text.Json.Serialization;

namespace AvaScope.Protocol;

public sealed record BridgeCleanupResponse
{
    [JsonConstructor]
    public BridgeCleanupResponse(
        string manifestDirectory,
        int deletedBridgeManifestRecords,
        IReadOnlyList<BridgeSessionDiagnostic>? cleanupCandidates,
        IReadOnlyList<string>? deletedPaths,
        IReadOnlyList<ProtocolError>? issues,
        DateTimeOffset cleanedAt)
    {
        if (string.IsNullOrWhiteSpace(manifestDirectory))
        {
            throw new ArgumentException("Manifest directory cannot be empty.", nameof(manifestDirectory));
        }

        if (deletedBridgeManifestRecords < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(deletedBridgeManifestRecords),
                deletedBridgeManifestRecords,
                "Deleted record count cannot be negative.");
        }

        ManifestDirectory = Path.GetFullPath(manifestDirectory);
        DeletedBridgeManifestRecords = deletedBridgeManifestRecords;
        CleanupCandidates = cleanupCandidates ?? [];
        DeletedPaths = (deletedPaths ?? [])
            .Where(static path => !string.IsNullOrWhiteSpace(path))
            .Select(Path.GetFullPath)
            .ToArray();
        Issues = issues ?? [];
        CleanedAt = cleanedAt;
    }

    [JsonPropertyName("manifestDirectory")]
    public string ManifestDirectory { get; }

    [JsonPropertyName("deletedBridgeManifestRecords")]
    public int DeletedBridgeManifestRecords { get; }

    [JsonPropertyName("cleanupCandidates")]
    public IReadOnlyList<BridgeSessionDiagnostic> CleanupCandidates { get; }

    [JsonPropertyName("deletedPaths")]
    public IReadOnlyList<string> DeletedPaths { get; }

    [JsonPropertyName("issues")]
    public IReadOnlyList<ProtocolError> Issues { get; }

    [JsonPropertyName("cleanedAt")]
    public DateTimeOffset CleanedAt { get; }
}
