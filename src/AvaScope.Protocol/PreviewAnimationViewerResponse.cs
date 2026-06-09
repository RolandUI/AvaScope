using System.Text.Json.Serialization;

namespace AvaScope.Protocol;

public sealed record PreviewAnimationViewerResponse
{
    [JsonConstructor]
    public PreviewAnimationViewerResponse(
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

        ViewerPath = Path.GetFullPath(viewerPath);
        PreviewUrl = previewUrl;
        GeneratedAt = generatedAt;
    }

    [JsonPropertyName("viewerPath")]
    public string ViewerPath { get; }

    [JsonPropertyName("previewUrl")]
    public string PreviewUrl { get; }

    [JsonPropertyName("generatedAt")]
    public DateTimeOffset GeneratedAt { get; }
}
