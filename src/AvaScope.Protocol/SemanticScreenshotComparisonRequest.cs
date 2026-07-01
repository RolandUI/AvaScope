using System.Text.Json.Serialization;

namespace AvaScope.Protocol;

public sealed record SemanticScreenshotComparisonRequest
{
    public const int MaximumFindings = 32;
    public const int MaximumRawRegions = 32;

    [JsonConstructor]
    public SemanticScreenshotComparisonRequest(
        string referencePath,
        string currentPath,
        string? requestId = null,
        string? outputDirectory = null,
        string? diffPath = null,
        string? annotatedPath = null,
        double tolerance = 0,
        int maxFindings = 12,
        int maxRawRegions = 8,
        int minChangedPixels = 4)
    {
        if (string.IsNullOrWhiteSpace(referencePath))
        {
            throw new ArgumentException("Reference image path cannot be empty.", nameof(referencePath));
        }

        if (string.IsNullOrWhiteSpace(currentPath))
        {
            throw new ArgumentException("Current image path cannot be empty.", nameof(currentPath));
        }

        if (tolerance < 0 || tolerance > 255)
        {
            throw new ArgumentOutOfRangeException(nameof(tolerance), tolerance, "Tolerance must be between 0 and 255.");
        }

        if (maxFindings < 1 || maxFindings > MaximumFindings)
        {
            throw new ArgumentOutOfRangeException(nameof(maxFindings), maxFindings, $"Maximum findings must be between 1 and {MaximumFindings}.");
        }

        if (maxRawRegions < 1 || maxRawRegions > MaximumRawRegions)
        {
            throw new ArgumentOutOfRangeException(nameof(maxRawRegions), maxRawRegions, $"Maximum raw regions must be between 1 and {MaximumRawRegions}.");
        }

        if (minChangedPixels < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(minChangedPixels), minChangedPixels, "Minimum changed pixels must be positive.");
        }

        RequestId = string.IsNullOrWhiteSpace(requestId) ? Guid.NewGuid().ToString("n") : requestId.Trim();
        ReferencePath = Path.GetFullPath(referencePath);
        CurrentPath = Path.GetFullPath(currentPath);
        OutputDirectory = string.IsNullOrWhiteSpace(outputDirectory) ? null : Path.GetFullPath(outputDirectory);
        DiffPath = string.IsNullOrWhiteSpace(diffPath) ? null : Path.GetFullPath(diffPath);
        AnnotatedPath = string.IsNullOrWhiteSpace(annotatedPath) ? null : Path.GetFullPath(annotatedPath);
        Tolerance = tolerance;
        MaxFindings = maxFindings;
        MaxRawRegions = maxRawRegions;
        MinChangedPixels = minChangedPixels;
    }

    [JsonPropertyName("requestId")]
    public string RequestId { get; }

    [JsonPropertyName("referencePath")]
    public string ReferencePath { get; }

    [JsonPropertyName("currentPath")]
    public string CurrentPath { get; }

    [JsonPropertyName("outputDirectory")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? OutputDirectory { get; }

    [JsonPropertyName("diffPath")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? DiffPath { get; }

    [JsonPropertyName("annotatedPath")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? AnnotatedPath { get; }

    [JsonPropertyName("tolerance")]
    public double Tolerance { get; }

    [JsonPropertyName("maxFindings")]
    public int MaxFindings { get; }

    [JsonPropertyName("maxRawRegions")]
    public int MaxRawRegions { get; }

    [JsonPropertyName("minChangedPixels")]
    public int MinChangedPixels { get; }
}

public static class SemanticScreenshotFindingKinds
{
    public const string CenterMismatch = "center_mismatch";
    public const string EdgeMismatch = "edge_mismatch";
    public const string PaddingDifference = "padding_difference";
    public const string BorderOrSeamDifference = "border_or_seam_difference";
    public const string WrappingDifference = "wrapping_difference";
}
