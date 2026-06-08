using System.Text.Json.Serialization;

namespace AvaScope.Protocol;

public sealed record PreviewBatchEntry
{
    [JsonConstructor]
    public PreviewBatchEntry(PreviewViewport viewport, string outputPath, ToolResult<PreviewResponse> render)
    {
        ArgumentNullException.ThrowIfNull(viewport);
        ArgumentNullException.ThrowIfNull(render);

        if (string.IsNullOrWhiteSpace(outputPath))
        {
            throw new ArgumentException("Batch output path cannot be empty.", nameof(outputPath));
        }

        Viewport = viewport;
        OutputPath = Path.GetFullPath(outputPath);
        Render = render;
    }

    [JsonPropertyName("viewport")]
    public PreviewViewport Viewport { get; }

    [JsonPropertyName("outputPath")]
    public string OutputPath { get; }

    [JsonPropertyName("render")]
    public ToolResult<PreviewResponse> Render { get; }
}
