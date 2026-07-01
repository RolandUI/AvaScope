using System.Text.Json.Serialization;

namespace AvaScope.Protocol;

public sealed record SemanticScreenshotComparisonResponse
{
    public const int MaximumFindings = 32;
    public const int MaximumRawRegions = 32;
    public const int MaximumDiagnostics = 32;

    [JsonConstructor]
    public SemanticScreenshotComparisonResponse(
        string requestId,
        string referencePath,
        string currentPath,
        string status,
        DateTimeOffset comparedAt,
        PreviewDiffResponse rawDiff,
        string? annotatedPath = null,
        IReadOnlyList<SemanticScreenshotRawRegion>? rawRegions = null,
        IReadOnlyList<SemanticScreenshotFinding>? findings = null,
        IReadOnlyList<ProtocolError>? diagnostics = null,
        IReadOnlyDictionary<string, string>? metadata = null)
    {
        if (string.IsNullOrWhiteSpace(requestId))
        {
            throw new ArgumentException("Semantic screenshot comparison request id cannot be empty.", nameof(requestId));
        }

        if (string.IsNullOrWhiteSpace(referencePath))
        {
            throw new ArgumentException("Reference path cannot be empty.", nameof(referencePath));
        }

        if (string.IsNullOrWhiteSpace(currentPath))
        {
            throw new ArgumentException("Current path cannot be empty.", nameof(currentPath));
        }

        if (string.IsNullOrWhiteSpace(status))
        {
            throw new ArgumentException("Semantic screenshot comparison status cannot be empty.", nameof(status));
        }

        RequestId = requestId.Trim();
        ReferencePath = Path.GetFullPath(referencePath);
        CurrentPath = Path.GetFullPath(currentPath);
        Status = status.Trim();
        ComparedAt = comparedAt;
        RawDiff = rawDiff ?? throw new ArgumentNullException(nameof(rawDiff));
        AnnotatedPath = string.IsNullOrWhiteSpace(annotatedPath) ? null : Path.GetFullPath(annotatedPath);
        RawRegions = (rawRegions ?? []).Take(MaximumRawRegions).ToArray();
        Findings = (findings ?? []).Take(MaximumFindings).ToArray();
        Diagnostics = (diagnostics ?? []).Take(MaximumDiagnostics).ToArray();
        Metadata = metadata ?? new Dictionary<string, string>();
    }

    [JsonPropertyName("requestId")]
    public string RequestId { get; }

    [JsonPropertyName("referencePath")]
    public string ReferencePath { get; }

    [JsonPropertyName("currentPath")]
    public string CurrentPath { get; }

    [JsonPropertyName("status")]
    public string Status { get; }

    [JsonPropertyName("comparedAt")]
    public DateTimeOffset ComparedAt { get; }

    [JsonPropertyName("rawDiff")]
    public PreviewDiffResponse RawDiff { get; }

    [JsonPropertyName("annotatedPath")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? AnnotatedPath { get; }

    [JsonPropertyName("rawRegions")]
    public IReadOnlyList<SemanticScreenshotRawRegion> RawRegions { get; }

    [JsonPropertyName("findings")]
    public IReadOnlyList<SemanticScreenshotFinding> Findings { get; }

    [JsonPropertyName("diagnostics")]
    public IReadOnlyList<ProtocolError> Diagnostics { get; }

    [JsonPropertyName("metadata")]
    public IReadOnlyDictionary<string, string> Metadata { get; }

    [JsonPropertyName("agentReview")]
    public AgentReviewSurface AgentReview => CreateAgentReview();

    private AgentReviewSurface CreateAgentReview()
    {
        var failures = Findings
            .Take(AgentReviewSurface.MaximumFailureSummaries)
            .Select(static finding => new AgentReviewFailure("semantic_screenshot_compare", finding.Message, finding.Kind, finding.AnnotatedCropPath ?? finding.CropPath))
            .Concat(Diagnostics.Select(static diagnostic => new AgentReviewFailure("semantic_screenshot_compare", diagnostic.Message, diagnostic.Code)))
            .Take(AgentReviewSurface.MaximumFailureSummaries)
            .ToArray();

        var artifacts = RawRegions
            .SelectMany(static region => region.ArtifactPaths())
            .Concat(Findings.SelectMany(static finding => finding.ArtifactPaths()))
            .Concat(RawDiff.DiffPath is null ? [] : [new AgentReviewPath("raw_diff", RawDiff.DiffPath, description: "Raw pixel diff image.")])
            .Concat(AnnotatedPath is null ? [] : [new AgentReviewPath("semantic_annotation", AnnotatedPath, description: "Annotated semantic screenshot comparison overview.")])
            .Take(AgentReviewSurface.MaximumPaths)
            .ToArray();

        return new AgentReviewSurface(
            Status,
            $"Semantic screenshot comparison '{RequestId}' completed with status '{Status}'.",
            [
                $"rawRegions: {RawRegions.Count}",
                $"semanticFindings: {Findings.Count}",
                $"changedPixels: {RawDiff.ChangedPixels}"
            ],
            failures,
            artifactPaths: artifacts,
            truncated: Findings.Count + Diagnostics.Count > AgentReviewSurface.MaximumFailureSummaries
                || RawRegions.Sum(static region => region.ArtifactPaths().Count()) + Findings.Sum(static finding => finding.ArtifactPaths().Count()) + (RawDiff.DiffPath is null ? 0 : 1) + (AnnotatedPath is null ? 0 : 1) > AgentReviewSurface.MaximumPaths);
    }
}

public sealed record SemanticScreenshotRawRegion
{
    [JsonConstructor]
    public SemanticScreenshotRawRegion(
        string regionId,
        ScreenshotRegion region,
        long changedPixels,
        double changedPercent,
        int maxDelta,
        string? cropPath = null,
        string? annotatedCropPath = null,
        IReadOnlyDictionary<string, string>? metadata = null)
    {
        if (string.IsNullOrWhiteSpace(regionId))
        {
            throw new ArgumentException("Raw region id cannot be empty.", nameof(regionId));
        }

        if (changedPixels < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(changedPixels), changedPixels, "Changed pixels cannot be negative.");
        }

        if (changedPercent < 0 || changedPercent > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(changedPercent), changedPercent, "Changed percent must be between 0 and 100.");
        }

        if (maxDelta < 0 || maxDelta > 255)
        {
            throw new ArgumentOutOfRangeException(nameof(maxDelta), maxDelta, "Max delta must be between 0 and 255.");
        }

        RegionId = regionId.Trim();
        Region = region ?? throw new ArgumentNullException(nameof(region));
        ChangedPixels = changedPixels;
        ChangedPercent = changedPercent;
        MaxDelta = maxDelta;
        CropPath = string.IsNullOrWhiteSpace(cropPath) ? null : Path.GetFullPath(cropPath);
        AnnotatedCropPath = string.IsNullOrWhiteSpace(annotatedCropPath) ? null : Path.GetFullPath(annotatedCropPath);
        Metadata = metadata ?? new Dictionary<string, string>();
    }

    [JsonPropertyName("regionId")]
    public string RegionId { get; }

    [JsonPropertyName("region")]
    public ScreenshotRegion Region { get; }

    [JsonPropertyName("changedPixels")]
    public long ChangedPixels { get; }

    [JsonPropertyName("changedPercent")]
    public double ChangedPercent { get; }

    [JsonPropertyName("maxDelta")]
    public int MaxDelta { get; }

    [JsonPropertyName("cropPath")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? CropPath { get; }

    [JsonPropertyName("annotatedCropPath")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? AnnotatedCropPath { get; }

    [JsonPropertyName("metadata")]
    public IReadOnlyDictionary<string, string> Metadata { get; }

    internal IEnumerable<AgentReviewPath> ArtifactPaths()
    {
        if (CropPath is not null)
        {
            yield return new AgentReviewPath("raw_region_crop", CropPath, description: $"Raw changed-region crop '{RegionId}'.");
        }

        if (AnnotatedCropPath is not null)
        {
            yield return new AgentReviewPath("annotated_region_crop", AnnotatedCropPath, description: $"Annotated raw changed-region crop '{RegionId}'.");
        }
    }
}

public sealed record SemanticScreenshotFinding
{
    [JsonConstructor]
    public SemanticScreenshotFinding(
        string findingId,
        string kind,
        string severity,
        double confidence,
        string provenance,
        string message,
        ScreenshotRegion region,
        string? cropPath = null,
        string? annotatedCropPath = null,
        IReadOnlyDictionary<string, string>? metrics = null)
    {
        if (string.IsNullOrWhiteSpace(findingId))
        {
            throw new ArgumentException("Finding id cannot be empty.", nameof(findingId));
        }

        if (string.IsNullOrWhiteSpace(kind))
        {
            throw new ArgumentException("Finding kind cannot be empty.", nameof(kind));
        }

        if (string.IsNullOrWhiteSpace(severity))
        {
            throw new ArgumentException("Finding severity cannot be empty.", nameof(severity));
        }

        if (confidence < 0 || confidence > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(confidence), confidence, "Finding confidence must be between 0 and 1.");
        }

        if (string.IsNullOrWhiteSpace(provenance))
        {
            throw new ArgumentException("Finding provenance cannot be empty.", nameof(provenance));
        }

        if (string.IsNullOrWhiteSpace(message))
        {
            throw new ArgumentException("Finding message cannot be empty.", nameof(message));
        }

        FindingId = findingId.Trim();
        Kind = kind.Trim();
        Severity = severity.Trim();
        Confidence = confidence;
        Provenance = provenance.Trim();
        Message = message.Trim();
        Region = region ?? throw new ArgumentNullException(nameof(region));
        CropPath = string.IsNullOrWhiteSpace(cropPath) ? null : Path.GetFullPath(cropPath);
        AnnotatedCropPath = string.IsNullOrWhiteSpace(annotatedCropPath) ? null : Path.GetFullPath(annotatedCropPath);
        Metrics = metrics ?? new Dictionary<string, string>();
    }

    [JsonPropertyName("findingId")]
    public string FindingId { get; }

    [JsonPropertyName("kind")]
    public string Kind { get; }

    [JsonPropertyName("severity")]
    public string Severity { get; }

    [JsonPropertyName("confidence")]
    public double Confidence { get; }

    [JsonPropertyName("provenance")]
    public string Provenance { get; }

    [JsonPropertyName("message")]
    public string Message { get; }

    [JsonPropertyName("region")]
    public ScreenshotRegion Region { get; }

    [JsonPropertyName("cropPath")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? CropPath { get; }

    [JsonPropertyName("annotatedCropPath")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? AnnotatedCropPath { get; }

    [JsonPropertyName("metrics")]
    public IReadOnlyDictionary<string, string> Metrics { get; }

    internal IEnumerable<AgentReviewPath> ArtifactPaths()
    {
        if (CropPath is not null)
        {
            yield return new AgentReviewPath("semantic_finding_crop", CropPath, description: $"Semantic finding crop '{FindingId}'.");
        }

        if (AnnotatedCropPath is not null)
        {
            yield return new AgentReviewPath("annotated_semantic_crop", AnnotatedCropPath, description: $"Annotated semantic finding crop '{FindingId}'.");
        }
    }
}
