using System.Text.Json.Serialization;

namespace AvaScope.Protocol;

public sealed record PreviewBaselineManifest
{
    public const int CurrentVersion = 1;

    [JsonConstructor]
    public PreviewBaselineManifest(
        int version,
        DateTimeOffset createdAt,
        IReadOnlyList<PreviewBaselineEntry>? entries = null)
    {
        if (version < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(version), version, "Baseline manifest version must be positive.");
        }

        Version = version;
        CreatedAt = createdAt;
        Entries = entries ?? [];
    }

    [JsonPropertyName("version")]
    public int Version { get; }

    [JsonPropertyName("createdAt")]
    public DateTimeOffset CreatedAt { get; }

    [JsonPropertyName("entries")]
    public IReadOnlyList<PreviewBaselineEntry> Entries { get; }
}
