using System.Text.Json.Serialization;

namespace AvaScope.Protocol;

public sealed record PreviewAnimationResponse
{
    [JsonConstructor]
    public PreviewAnimationResponse(
        IReadOnlyList<PreviewAnimationFrame>? frames,
        string? frameStripPath,
        PreviewAnimationMotionSummary motion,
        IReadOnlyList<PreviewDiagnostic>? diagnostics,
        DateTimeOffset sampledAt,
        PreviewAnimationViewerResponse? viewer = null,
        PreviewDiagnosticSummary? diagnosticSummary = null,
        string? diagnosticsArtifactPath = null)
    {
        ArgumentNullException.ThrowIfNull(motion);

        Frames = frames ?? [];
        FrameStripPath = string.IsNullOrWhiteSpace(frameStripPath) ? null : Path.GetFullPath(frameStripPath);
        Motion = motion;
        Diagnostics = diagnostics ?? [];
        SampledAt = sampledAt;
        Viewer = viewer;
        DiagnosticSummary = diagnosticSummary ?? PreviewResponse.CreateDiagnosticSummary(Diagnostics);
        DiagnosticsArtifactPath = string.IsNullOrWhiteSpace(diagnosticsArtifactPath)
            ? null
            : Path.GetFullPath(diagnosticsArtifactPath);
    }

    [JsonPropertyName("frames")]
    public IReadOnlyList<PreviewAnimationFrame> Frames { get; }

    [JsonPropertyName("frameStripPath")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? FrameStripPath { get; }

    [JsonPropertyName("motion")]
    public PreviewAnimationMotionSummary Motion { get; }

    [JsonPropertyName("diagnostics")]
    public IReadOnlyList<PreviewDiagnostic> Diagnostics { get; }

    [JsonPropertyName("sampledAt")]
    public DateTimeOffset SampledAt { get; }

    [JsonPropertyName("viewer")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public PreviewAnimationViewerResponse? Viewer { get; }

    [JsonPropertyName("diagnosticSummary")]
    public PreviewDiagnosticSummary DiagnosticSummary { get; }

    [JsonPropertyName("diagnosticsArtifactPath")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? DiagnosticsArtifactPath { get; }
}
