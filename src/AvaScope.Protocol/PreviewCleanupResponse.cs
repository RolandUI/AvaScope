using System.Text.Json.Serialization;

namespace AvaScope.Protocol;

public sealed record PreviewCleanupResponse
{
    [JsonConstructor]
    public PreviewCleanupResponse(
        string previewSessionDirectory,
        int deletedPreviewSessionRecords,
        IReadOnlyList<PreviewSessionDiagnostic>? stalePreviewSessions,
        IReadOnlyList<string>? deletedPaths,
        DateTimeOffset cleanedAt)
    {
        if (string.IsNullOrWhiteSpace(previewSessionDirectory))
        {
            throw new ArgumentException("Preview session directory cannot be empty.", nameof(previewSessionDirectory));
        }

        if (deletedPreviewSessionRecords < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(deletedPreviewSessionRecords),
                deletedPreviewSessionRecords,
                "Deleted record count cannot be negative.");
        }

        PreviewSessionDirectory = Path.GetFullPath(previewSessionDirectory);
        DeletedPreviewSessionRecords = deletedPreviewSessionRecords;
        StalePreviewSessions = stalePreviewSessions ?? [];
        DeletedPaths = (deletedPaths ?? [])
            .Where(static path => !string.IsNullOrWhiteSpace(path))
            .Select(Path.GetFullPath)
            .ToArray();
        CleanedAt = cleanedAt;
    }

    [JsonPropertyName("previewSessionDirectory")]
    public string PreviewSessionDirectory { get; }

    [JsonPropertyName("deletedPreviewSessionRecords")]
    public int DeletedPreviewSessionRecords { get; }

    [JsonPropertyName("stalePreviewSessions")]
    public IReadOnlyList<PreviewSessionDiagnostic> StalePreviewSessions { get; }

    [JsonPropertyName("deletedPaths")]
    public IReadOnlyList<string> DeletedPaths { get; }

    [JsonPropertyName("cleanedAt")]
    public DateTimeOffset CleanedAt { get; }
}
