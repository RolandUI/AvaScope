using System.Text.Json.Serialization;

namespace AvaScope.Protocol;

public sealed record PreviewBaselineCheckResponse
{
    [JsonConstructor]
    public PreviewBaselineCheckResponse(
        string manifestPath,
        bool passed,
        IReadOnlyList<PreviewBaselineCheckEntry>? entries,
        DateTimeOffset checkedAt,
        string? reportPath = null,
        AgentEvidenceReportPackResponse? reportPack = null)
    {
        if (string.IsNullOrWhiteSpace(manifestPath))
        {
            throw new ArgumentException("Baseline manifest path cannot be empty.", nameof(manifestPath));
        }

        ManifestPath = Path.GetFullPath(manifestPath);
        Passed = passed;
        Entries = entries ?? [];
        CheckedAt = checkedAt;
        ReportPath = string.IsNullOrWhiteSpace(reportPath) ? null : Path.GetFullPath(reportPath);
        ReportPack = reportPack;
    }

    [JsonPropertyName("manifestPath")]
    public string ManifestPath { get; }

    [JsonPropertyName("passed")]
    public bool Passed { get; }

    [JsonPropertyName("entries")]
    public IReadOnlyList<PreviewBaselineCheckEntry> Entries { get; }

    [JsonPropertyName("checkedAt")]
    public DateTimeOffset CheckedAt { get; }

    [JsonPropertyName("reportPath")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ReportPath { get; }

    [JsonPropertyName("reportPack")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public AgentEvidenceReportPackResponse? ReportPack { get; }

    [JsonPropertyName("agentReview")]
    public AgentReviewSurface AgentReview => CreateAgentReview();

    private AgentReviewSurface CreateAgentReview()
    {
        var failedEntries = Entries.Where(static entry => IsFailedEntry(entry)).ToArray();
        var failures = failedEntries
            .Take(AgentReviewSurface.MaximumFailureSummaries)
            .Select(CreateFailure)
            .ToArray();
        var reportPaths = CreateReportPaths().ToArray();
        var artifactPaths = failedEntries
            .Take(AgentReviewSurface.MaximumPaths / 3)
            .SelectMany(static entry => new[]
            {
                new AgentReviewPath("baseline", entry.Baseline.ImagePath, description: "Baseline image."),
                new AgentReviewPath("current", entry.CurrentImagePath, description: "Current render image."),
                new AgentReviewPath("diff", entry.DiffPath, description: "Visual diff image.")
            })
            .Take(AgentReviewSurface.MaximumPaths)
            .ToArray();
        var reviewUrls = reportPaths
            .Where(static path => string.Equals(path.Kind, "html", StringComparison.OrdinalIgnoreCase))
            .Select(static path => path.Url)
            .ToArray();
        var status = Passed ? "passed" : "failed";
        var headline = Passed
            ? $"Baseline check passed for {Entries.Count.ToString(System.Globalization.CultureInfo.InvariantCulture)} entries."
            : $"Baseline check failed for {failedEntries.Length.ToString(System.Globalization.CultureInfo.InvariantCulture)} of {Entries.Count.ToString(System.Globalization.CultureInfo.InvariantCulture)} entries.";

        return new AgentReviewSurface(
            status,
            headline,
            [
                $"manifest: {ManifestPath}",
                $"entries: {Entries.Count.ToString(System.Globalization.CultureInfo.InvariantCulture)}",
                $"failed: {failedEntries.Length.ToString(System.Globalization.CultureInfo.InvariantCulture)}"
            ],
            failures,
            reportPaths: reportPaths,
            artifactPaths: artifactPaths,
            reviewUrls: reviewUrls,
            truncated: failedEntries.Length > AgentReviewSurface.MaximumFailureSummaries);
    }

    private IEnumerable<AgentReviewPath> CreateReportPaths()
    {
        if (ReportPath is not null)
        {
            yield return new AgentReviewPath("json", ReportPath, description: "Baseline check JSON report.");
        }

        if (ReportPack is null)
        {
            yield break;
        }

        yield return new AgentReviewPath("directory", ReportPack.ReportDirectory, description: "Agent evidence report pack directory.");
        foreach (var asset in ReportPack.Assets)
        {
            yield return new AgentReviewPath(asset.Kind, asset.Path, asset.Url, asset.Description);
        }
    }

    private static bool IsFailedEntry(PreviewBaselineCheckEntry entry)
    {
        return !entry.Render.Success
            || !entry.Diff.Success
            || entry.Diff.Value is { Passed: false }
            || entry.RequiredRegionResults.Any(static result =>
                !result.Result.Success || result.Result.Value is { Passed: false });
    }

    private static AgentReviewFailure CreateFailure(PreviewBaselineCheckEntry entry)
    {
        var scope = $"baseline:{entry.Baseline.Index.ToString(System.Globalization.CultureInfo.InvariantCulture)}";
        if (!entry.Render.Success)
        {
            return new AgentReviewFailure(
                scope,
                entry.Render.Error!.Message,
                entry.Render.Error.Code,
                entry.CurrentImagePath);
        }

        if (!entry.Diff.Success)
        {
            return new AgentReviewFailure(
                scope,
                entry.Diff.Error!.Message,
                entry.Diff.Error.Code,
                entry.DiffPath);
        }

        if (entry.Diff.Value is { Passed: false } diff)
        {
            return new AgentReviewFailure(
                scope,
                $"Visual diff changed {diff.ChangedPixels.ToString(System.Globalization.CultureInfo.InvariantCulture)} pixels ({diff.ChangedPercent.ToString("0.####", System.Globalization.CultureInfo.InvariantCulture)}%).",
                "visual_diff_changed",
                entry.DiffPath);
        }

        var regionFailure = entry.RequiredRegionResults.First(static result =>
            !result.Result.Success || result.Result.Value is { Passed: false });
        return new AgentReviewFailure(
            scope,
            regionFailure.Result.Success
                ? $"Required region assertion '{regionFailure.Assertion}' failed."
                : regionFailure.Result.Error!.Message,
            regionFailure.Result.Success ? "required_region_failed" : regionFailure.Result.Error!.Code,
            regionFailure.Result.Value?.CropPath ?? entry.CurrentImagePath);
    }
}
