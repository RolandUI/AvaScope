using System.Text.Json.Serialization;

namespace AvaScope.Protocol;

public sealed record PreviewResponse
{
    [JsonConstructor]
    public PreviewResponse(
        string filePath,
        int pixelWidth,
        int pixelHeight,
        double dpi,
        DateTimeOffset renderedAt,
        string? projectPath = null,
        string? viewPath = null,
        string? themeVariant = null,
        string? culture = null,
        string? designDataType = null,
        IReadOnlyList<PreviewDiagnostic>? diagnostics = null,
        int? animationTimeOffsetMs = null,
        PreviewProjectInfo? projectInfo = null,
        string? stateVariant = null,
        ArtifactRunIndexResponse? runIndex = null,
        PreviewDiagnosticSummary? diagnosticSummary = null,
        string? diagnosticsArtifactPath = null)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentException("File path cannot be empty.", nameof(filePath));
        }

        if (pixelWidth < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(pixelWidth), pixelWidth, "Pixel width must be positive.");
        }

        if (pixelHeight < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(pixelHeight), pixelHeight, "Pixel height must be positive.");
        }

        if (dpi <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(dpi), dpi, "DPI must be positive.");
        }

        if (animationTimeOffsetMs is < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(animationTimeOffsetMs), animationTimeOffsetMs, "Animation time offset must be zero or greater.");
        }

        FilePath = filePath;
        PixelWidth = pixelWidth;
        PixelHeight = pixelHeight;
        Dpi = dpi;
        RenderedAt = renderedAt;
        ProjectPath = string.IsNullOrWhiteSpace(projectPath) ? null : projectPath;
        ViewPath = string.IsNullOrWhiteSpace(viewPath) ? null : viewPath;
        ThemeVariant = string.IsNullOrWhiteSpace(themeVariant) ? null : themeVariant;
        Culture = string.IsNullOrWhiteSpace(culture) ? null : culture;
        DesignDataType = string.IsNullOrWhiteSpace(designDataType) ? null : designDataType;
        Diagnostics = diagnostics ?? [];
        AnimationTimeOffsetMs = animationTimeOffsetMs;
        ProjectInfo = projectInfo;
        StateVariant = string.IsNullOrWhiteSpace(stateVariant) ? null : stateVariant;
        RunIndex = runIndex;
        DiagnosticSummary = diagnosticSummary ?? CreateDiagnosticSummary(Diagnostics);
        DiagnosticsArtifactPath = string.IsNullOrWhiteSpace(diagnosticsArtifactPath)
            ? null
            : Path.GetFullPath(diagnosticsArtifactPath);
    }

    [JsonPropertyName("filePath")]
    public string FilePath { get; }

    [JsonPropertyName("pixelWidth")]
    public int PixelWidth { get; }

    [JsonPropertyName("pixelHeight")]
    public int PixelHeight { get; }

    [JsonPropertyName("dpi")]
    public double Dpi { get; }

    [JsonPropertyName("renderedAt")]
    public DateTimeOffset RenderedAt { get; }

    [JsonPropertyName("projectPath")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ProjectPath { get; }

    [JsonPropertyName("viewPath")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ViewPath { get; }

    [JsonPropertyName("themeVariant")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ThemeVariant { get; }

    [JsonPropertyName("culture")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Culture { get; }

    [JsonPropertyName("designDataType")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? DesignDataType { get; }

    [JsonPropertyName("diagnostics")]
    public IReadOnlyList<PreviewDiagnostic> Diagnostics { get; }

    [JsonPropertyName("animationTimeOffsetMs")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? AnimationTimeOffsetMs { get; }

    [JsonPropertyName("projectInfo")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public PreviewProjectInfo? ProjectInfo { get; }

    [JsonPropertyName("stateVariant")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? StateVariant { get; }

    [JsonPropertyName("runIndex")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ArtifactRunIndexResponse? RunIndex { get; }

    [JsonPropertyName("diagnosticSummary")]
    public PreviewDiagnosticSummary DiagnosticSummary { get; }

    [JsonPropertyName("diagnosticsArtifactPath")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? DiagnosticsArtifactPath { get; }

    public static PreviewDiagnosticSummary CreateDiagnosticSummary(
        IReadOnlyList<PreviewDiagnostic> diagnostics,
        bool truncated = false,
        int? totalCount = null)
    {
        var severityCounts = diagnostics
            .GroupBy(static item => item.Severity, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(static group => group.Key, static group => group.Count(), StringComparer.OrdinalIgnoreCase);
        var categoryCounts = diagnostics
            .GroupBy(static item => item.Category, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(static group => group.Key, static group => group.Count(), StringComparer.OrdinalIgnoreCase);
        var total = totalCount ?? diagnostics.Count;
        var errors = severityCounts.GetValueOrDefault(PreviewDiagnosticSeverities.Error);
        var warnings = severityCounts.GetValueOrDefault(PreviewDiagnosticSeverities.Warning);
        return new PreviewDiagnosticSummary(
            total,
            severityCounts,
            categoryCounts,
            $"{total} diagnostic(s): {errors} error(s), {warnings} warning(s).",
            truncated: truncated,
            inlineCount: diagnostics.Count);
    }
}
