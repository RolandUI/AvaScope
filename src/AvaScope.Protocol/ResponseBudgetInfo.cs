using System.Text.Json.Serialization;

namespace AvaScope.Protocol;

public sealed record ResponseBudgetInfo
{
    [JsonConstructor]
    public ResponseBudgetInfo(
        int maxInlineBytes,
        int estimatedBytes,
        int maxItems,
        int totalItems,
        int returnedItems,
        int maxDepth,
        int originalDepth,
        int returnedDepth,
        bool truncated,
        string? artifactPath = null,
        IReadOnlyList<string>? reasons = null)
    {
        MaxInlineBytes = maxInlineBytes;
        EstimatedBytes = estimatedBytes;
        MaxItems = maxItems;
        TotalItems = totalItems;
        ReturnedItems = returnedItems;
        MaxDepth = maxDepth;
        OriginalDepth = originalDepth;
        ReturnedDepth = returnedDepth;
        Truncated = truncated;
        ArtifactPath = string.IsNullOrWhiteSpace(artifactPath) ? null : Path.GetFullPath(artifactPath);
        Reasons = reasons ?? [];
    }

    [JsonPropertyName("maxInlineBytes")] public int MaxInlineBytes { get; }
    [JsonPropertyName("estimatedBytes")] public int EstimatedBytes { get; }
    [JsonPropertyName("maxItems")] public int MaxItems { get; }
    [JsonPropertyName("totalItems")] public int TotalItems { get; }
    [JsonPropertyName("returnedItems")] public int ReturnedItems { get; }
    [JsonPropertyName("maxDepth")] public int MaxDepth { get; }
    [JsonPropertyName("originalDepth")] public int OriginalDepth { get; }
    [JsonPropertyName("returnedDepth")] public int ReturnedDepth { get; }
    [JsonPropertyName("truncated")] public bool Truncated { get; }

    [JsonPropertyName("artifactPath")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ArtifactPath { get; }

    [JsonPropertyName("reasons")]
    public IReadOnlyList<string> Reasons { get; }
}
