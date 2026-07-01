using System.Text.Json.Serialization;

namespace AvaScope.Protocol;

public sealed record ArtifactRunIndexArtifact
{
    [JsonConstructor]
    public ArtifactRunIndexArtifact(
        string kind,
        string path,
        string? description = null,
        string? contentType = null)
    {
        if (string.IsNullOrWhiteSpace(kind))
        {
            throw new ArgumentException("Run index artifact kind cannot be empty.", nameof(kind));
        }

        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("Run index artifact path cannot be empty.", nameof(path));
        }

        Kind = kind.Trim();
        Path = System.IO.Path.GetFullPath(path);
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        ContentType = string.IsNullOrWhiteSpace(contentType) ? null : contentType.Trim();
    }

    [JsonPropertyName("kind")]
    public string Kind { get; }

    [JsonPropertyName("path")]
    public string Path { get; }

    [JsonPropertyName("url")]
    public string Url => new Uri(Path).AbsoluteUri;

    [JsonPropertyName("description")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Description { get; }

    [JsonPropertyName("contentType")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ContentType { get; }
}
