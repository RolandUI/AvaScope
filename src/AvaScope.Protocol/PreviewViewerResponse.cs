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

    [JsonPropertyName("agentReview")]
    public AgentReviewSurface AgentReview => new(
        Session.LastRender.Success ? "available" : "render_failed",
        Session.LastRender.Success
            ? "Preview viewer is available for local review."
            : "Preview viewer was generated for a failed preview session.",
        [
            $"session: {Session.Session.SessionId.Value}",
            $"state: {Session.Session.State}"
        ],
        Session.LastRender.Success || Session.LastRender.Error is null
            ? []
            : [new AgentReviewFailure("preview", Session.LastRender.Error.Message, Session.LastRender.Error.Code)],
        reportPaths: [new AgentReviewPath("html", ViewerPath, PreviewUrl, "Preview viewer HTML.")],
        reviewUrls: [PreviewUrl],
        previewUrls: [PreviewUrl]);
}
