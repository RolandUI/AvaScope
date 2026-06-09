using System.Text.Json.Serialization;

namespace AvaScope.Protocol;

public sealed record PreviewViewerResponse
{
    [JsonConstructor]
    public PreviewViewerResponse(
        PreviewSessionSummary session,
        string viewerPath,
        string previewUrl,
        DateTimeOffset generatedAt)
    {
        if (string.IsNullOrWhiteSpace(viewerPath))
        {
            throw new ArgumentException("Viewer path cannot be empty.", nameof(viewerPath));
        }

        if (string.IsNullOrWhiteSpace(previewUrl))
        {
            throw new ArgumentException("Preview URL cannot be empty.", nameof(previewUrl));
        }

        Session = session ?? throw new ArgumentNullException(nameof(session));
        ViewerPath = Path.GetFullPath(viewerPath);
        PreviewUrl = previewUrl;
        GeneratedAt = generatedAt;
    }

    [JsonPropertyName("session")]
    public PreviewSessionSummary Session { get; }

    [JsonPropertyName("viewerPath")]
    public string ViewerPath { get; }

    [JsonPropertyName("previewUrl")]
    public string PreviewUrl { get; }

    [JsonPropertyName("generatedAt")]
    public DateTimeOffset GeneratedAt { get; }
}
