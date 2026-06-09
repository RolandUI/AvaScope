using System.Text.Json.Serialization;

namespace AvaScope.Protocol;

public sealed record PreviewAnimationFrame
{
    [JsonConstructor]
    public PreviewAnimationFrame(int timeOffsetMs, string outputPath, ToolResult<PreviewResponse> render)
    {
        if (timeOffsetMs < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(timeOffsetMs), timeOffsetMs, "Animation time offset must be zero or greater.");
        }

        if (string.IsNullOrWhiteSpace(outputPath))
        {
            throw new ArgumentException("Animation frame output path cannot be empty.", nameof(outputPath));
        }

        ArgumentNullException.ThrowIfNull(render);

        TimeOffsetMs = timeOffsetMs;
        OutputPath = Path.GetFullPath(outputPath);
        Render = render;
    }

    [JsonPropertyName("timeOffsetMs")]
    public int TimeOffsetMs { get; }

    [JsonPropertyName("outputPath")]
    public string OutputPath { get; }

    [JsonPropertyName("render")]
    public ToolResult<PreviewResponse> Render { get; }
}
