using System.Text.Json.Serialization;

namespace AvaScope.Protocol;

public sealed record PreviewBaselineCreateResponse
{
    [JsonConstructor]
    public PreviewBaselineCreateResponse(
        string manifestPath,
        PreviewBaselineManifest manifest,
        PreviewBatchResponse render)
    {
        if (string.IsNullOrWhiteSpace(manifestPath))
        {
            throw new ArgumentException("Baseline manifest path cannot be empty.", nameof(manifestPath));
        }

        ManifestPath = Path.GetFullPath(manifestPath);
        Manifest = manifest ?? throw new ArgumentNullException(nameof(manifest));
        Render = render ?? throw new ArgumentNullException(nameof(render));
    }

    [JsonPropertyName("manifestPath")]
    public string ManifestPath { get; }

    [JsonPropertyName("manifest")]
    public PreviewBaselineManifest Manifest { get; }

    [JsonPropertyName("render")]
    public PreviewBatchResponse Render { get; }
}
