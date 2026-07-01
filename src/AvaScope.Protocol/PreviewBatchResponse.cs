using System.Text.Json.Serialization;

namespace AvaScope.Protocol;

public sealed record PreviewBatchResponse
{
    [JsonConstructor]
    public PreviewBatchResponse(
        IReadOnlyList<PreviewBatchEntry>? entries,
        string? contactSheetPath,
        DateTimeOffset renderedAt,
        ArtifactRunIndexResponse? runIndex = null)
    {
        Entries = entries ?? [];
        ContactSheetPath = string.IsNullOrWhiteSpace(contactSheetPath)
            ? null
            : Path.GetFullPath(contactSheetPath);
        RenderedAt = renderedAt;
        RunIndex = runIndex;
    }

    [JsonPropertyName("entries")]
    public IReadOnlyList<PreviewBatchEntry> Entries { get; }

    [JsonPropertyName("contactSheetPath")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ContactSheetPath { get; }

    [JsonPropertyName("renderedAt")]
    public DateTimeOffset RenderedAt { get; }

    [JsonPropertyName("runIndex")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ArtifactRunIndexResponse? RunIndex { get; }
}
