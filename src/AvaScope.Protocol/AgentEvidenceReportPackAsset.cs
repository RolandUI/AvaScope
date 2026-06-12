using System.Text.Json.Serialization;

namespace AvaScope.Protocol;

public sealed record AgentEvidenceReportPackAsset
{
    [JsonConstructor]
    public AgentEvidenceReportPackAsset(
        string kind,
        string path,
        string contentType,
        string? description = null)
    {
        if (string.IsNullOrWhiteSpace(kind))
        {
            throw new ArgumentException("Report pack asset kind cannot be empty.", nameof(kind));
        }

        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("Report pack asset path cannot be empty.", nameof(path));
        }

        if (string.IsNullOrWhiteSpace(contentType))
        {
            throw new ArgumentException("Report pack asset content type cannot be empty.", nameof(contentType));
        }

        Kind = kind.Trim();
        Path = System.IO.Path.GetFullPath(path);
        ContentType = contentType.Trim();
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
    }

    [JsonPropertyName("kind")]
    public string Kind { get; }

    [JsonPropertyName("path")]
    public string Path { get; }

    [JsonPropertyName("url")]
    public string Url => new Uri(Path).AbsoluteUri;

    [JsonPropertyName("contentType")]
    public string ContentType { get; }

    [JsonPropertyName("description")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Description { get; }
}
