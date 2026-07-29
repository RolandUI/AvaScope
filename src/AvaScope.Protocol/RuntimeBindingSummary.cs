using System.Text.Json.Serialization;

namespace AvaScope.Protocol;

public sealed record RuntimeBindingSummary
{
    public const int MaximumEntries = 16;

    [JsonConstructor]
    public RuntimeBindingSummary(
        string status,
        string? dataContextType,
        int totalCount,
        IReadOnlyList<RuntimeBindingSummaryEntry>? entries = null,
        bool truncated = false)
    {
        if (string.IsNullOrWhiteSpace(status))
        {
            throw new ArgumentException("Binding summary status cannot be empty.", nameof(status));
        }

        if (totalCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(totalCount), totalCount, "Binding count cannot be negative.");
        }

        Status = status;
        DataContextType = string.IsNullOrWhiteSpace(dataContextType) ? null : dataContextType;
        TotalCount = totalCount;
        Entries = (entries ?? []).Take(MaximumEntries).ToArray();
        Truncated = truncated || totalCount > Entries.Count;
    }

    [JsonPropertyName("status")] public string Status { get; }
    [JsonPropertyName("dataContextType")] public string? DataContextType { get; }
    [JsonPropertyName("totalCount")] public int TotalCount { get; }
    [JsonPropertyName("entries")] public IReadOnlyList<RuntimeBindingSummaryEntry> Entries { get; }
    [JsonPropertyName("truncated")] public bool Truncated { get; }
}

public sealed record RuntimeBindingSummaryEntry
{
    [JsonConstructor]
    public RuntimeBindingSummaryEntry(
        string propertyName,
        string bindingPath,
        string status,
        string resolvedValueStatus,
        string compiledBindingStatus)
    {
        PropertyName = string.IsNullOrWhiteSpace(propertyName) ? "unknown" : propertyName;
        BindingPath = string.IsNullOrWhiteSpace(bindingPath) ? "unknown" : bindingPath;
        Status = string.IsNullOrWhiteSpace(status) ? "unknown" : status;
        ResolvedValueStatus = string.IsNullOrWhiteSpace(resolvedValueStatus) ? "not_available" : resolvedValueStatus;
        CompiledBindingStatus = string.IsNullOrWhiteSpace(compiledBindingStatus) ? "not_available" : compiledBindingStatus;
    }

    [JsonPropertyName("propertyName")] public string PropertyName { get; }
    [JsonPropertyName("bindingPath")] public string BindingPath { get; }
    [JsonPropertyName("status")] public string Status { get; }
    [JsonPropertyName("resolvedValueStatus")] public string ResolvedValueStatus { get; }
    [JsonPropertyName("compiledBindingStatus")] public string CompiledBindingStatus { get; }
}
