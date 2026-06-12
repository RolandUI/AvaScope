using System.Text.Json.Serialization;

namespace AvaScope.Protocol;

public sealed record RuntimeMutationReviewArtifact
{
    [JsonConstructor]
    public RuntimeMutationReviewArtifact(
        string artifactPath,
        string reviewUrl,
        string format,
        DateTimeOffset generatedAt)
    {
        if (string.IsNullOrWhiteSpace(artifactPath))
        {
            throw new ArgumentException("Review artifact path cannot be empty.", nameof(artifactPath));
        }

        if (string.IsNullOrWhiteSpace(reviewUrl))
        {
            throw new ArgumentException("Review URL cannot be empty.", nameof(reviewUrl));
        }

        if (string.IsNullOrWhiteSpace(format))
        {
            throw new ArgumentException("Review artifact format cannot be empty.", nameof(format));
        }

        ArtifactPath = Path.GetFullPath(artifactPath);
        ReviewUrl = reviewUrl;
        Format = format.Trim();
        GeneratedAt = generatedAt;
    }

    [JsonPropertyName("artifactPath")]
    public string ArtifactPath { get; }

    [JsonPropertyName("reviewUrl")]
    public string ReviewUrl { get; }

    [JsonPropertyName("format")]
    public string Format { get; }

    [JsonPropertyName("generatedAt")]
    public DateTimeOffset GeneratedAt { get; }
}
