using System.Text.Json.Serialization;

namespace AvaScope.Protocol;

public sealed record AgentReviewPath
{
    [JsonConstructor]
    public AgentReviewPath(
        string kind,
        string path,
        string? url = null,
        string? description = null)
    {
        if (string.IsNullOrWhiteSpace(kind))
        {
            throw new ArgumentException("Agent review path kind cannot be empty.", nameof(kind));
        }

        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("Agent review path cannot be empty.", nameof(path));
        }

        Kind = kind.Trim();
        Path = System.IO.Path.GetFullPath(path);
        Url = string.IsNullOrWhiteSpace(url) ? new Uri(Path).AbsoluteUri : url.Trim();
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
    }

    [JsonPropertyName("kind")]
    public string Kind { get; }

    [JsonPropertyName("path")]
    public string Path { get; }

    [JsonPropertyName("url")]
    public string Url { get; }

    [JsonPropertyName("description")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Description { get; }
}
