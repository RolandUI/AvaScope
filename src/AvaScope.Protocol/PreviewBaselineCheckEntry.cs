using System.Text.Json.Serialization;

namespace AvaScope.Protocol;

public sealed record PreviewBaselineCheckEntry
{
    [JsonConstructor]
    public PreviewBaselineCheckEntry(
        PreviewBaselineEntry baseline,
        string currentImagePath,
        string diffPath,
        ToolResult<PreviewResponse> render,
        ToolResult<PreviewDiffResponse> diff)
    {
        if (string.IsNullOrWhiteSpace(currentImagePath))
        {
            throw new ArgumentException("Current image path cannot be empty.", nameof(currentImagePath));
        }

        if (string.IsNullOrWhiteSpace(diffPath))
        {
            throw new ArgumentException("Diff path cannot be empty.", nameof(diffPath));
        }

        Baseline = baseline ?? throw new ArgumentNullException(nameof(baseline));
        CurrentImagePath = Path.GetFullPath(currentImagePath);
        DiffPath = Path.GetFullPath(diffPath);
        Render = render ?? throw new ArgumentNullException(nameof(render));
        Diff = diff ?? throw new ArgumentNullException(nameof(diff));
    }

    [JsonPropertyName("baseline")]
    public PreviewBaselineEntry Baseline { get; }

    [JsonPropertyName("currentImagePath")]
    public string CurrentImagePath { get; }

    [JsonPropertyName("diffPath")]
    public string DiffPath { get; }

    [JsonPropertyName("render")]
    public ToolResult<PreviewResponse> Render { get; }

    [JsonPropertyName("diff")]
    public ToolResult<PreviewDiffResponse> Diff { get; }
}
