using System.Text.Json.Serialization;

namespace AvaScope.Protocol;

public sealed record RuntimeSourceSuggestion
{
    public const int MaximumLimitations = 8;
    public const int MaximumMetadataEntries = 16;

    [JsonConstructor]
    public RuntimeSourceSuggestion(
        string suggestionId,
        string mutationId,
        long sequence,
        string operationKind,
        RuntimeTargetContext affectedTarget,
        string confidence,
        string provenance,
        string suggestedTargetKind,
        string sourceFileStatus,
        string suggestedAction,
        string? suggestedFilePath = null,
        string? suggestedMember = null,
        string? suggestedProperty = null,
        string? suggestedClass = null,
        string? suggestedResourceKey = null,
        IReadOnlyList<string>? limitations = null,
        IReadOnlyDictionary<string, string>? metadata = null)
    {
        if (string.IsNullOrWhiteSpace(suggestionId))
        {
            throw new ArgumentException("Source suggestion id cannot be empty.", nameof(suggestionId));
        }

        if (string.IsNullOrWhiteSpace(mutationId))
        {
            throw new ArgumentException("Source suggestion mutation id cannot be empty.", nameof(mutationId));
        }

        if (sequence < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(sequence), sequence, "Source suggestion sequence must be positive.");
        }

        if (string.IsNullOrWhiteSpace(operationKind))
        {
            throw new ArgumentException("Source suggestion operation kind cannot be empty.", nameof(operationKind));
        }

        if (string.IsNullOrWhiteSpace(confidence))
        {
            throw new ArgumentException("Source suggestion confidence cannot be empty.", nameof(confidence));
        }

        if (string.IsNullOrWhiteSpace(provenance))
        {
            throw new ArgumentException("Source suggestion provenance cannot be empty.", nameof(provenance));
        }

        if (string.IsNullOrWhiteSpace(suggestedTargetKind))
        {
            throw new ArgumentException("Source suggestion target kind cannot be empty.", nameof(suggestedTargetKind));
        }

        if (string.IsNullOrWhiteSpace(sourceFileStatus))
        {
            throw new ArgumentException("Source suggestion file status cannot be empty.", nameof(sourceFileStatus));
        }

        if (string.IsNullOrWhiteSpace(suggestedAction))
        {
            throw new ArgumentException("Source suggestion action cannot be empty.", nameof(suggestedAction));
        }

        SuggestionId = suggestionId.Trim();
        MutationId = mutationId.Trim();
        Sequence = sequence;
        OperationKind = operationKind.Trim();
        AffectedTarget = affectedTarget ?? throw new ArgumentNullException(nameof(affectedTarget));
        Confidence = confidence.Trim();
        Provenance = provenance.Trim();
        SuggestedTargetKind = suggestedTargetKind.Trim();
        SourceFileStatus = sourceFileStatus.Trim();
        SuggestedFilePath = NormalizeOptionalPath(suggestedFilePath);
        SuggestedMember = NormalizeOptionalText(suggestedMember);
        SuggestedProperty = NormalizeOptionalText(suggestedProperty);
        SuggestedClass = NormalizeOptionalText(suggestedClass);
        SuggestedResourceKey = NormalizeOptionalText(suggestedResourceKey);
        SuggestedAction = suggestedAction.Trim();
        Limitations = (limitations ?? [])
            .Where(static limitation => !string.IsNullOrWhiteSpace(limitation))
            .Select(static limitation => limitation.Trim())
            .Take(MaximumLimitations)
            .ToArray();
        Metadata = metadata is null
            ? new Dictionary<string, string>()
            : metadata
                .Where(static item => !string.IsNullOrWhiteSpace(item.Key))
                .Take(MaximumMetadataEntries)
                .ToDictionary(
                    static item => item.Key,
                    static item => item.Value,
                    StringComparer.Ordinal);
    }

    [JsonPropertyName("suggestionId")]
    public string SuggestionId { get; }

    [JsonPropertyName("mutationId")]
    public string MutationId { get; }

    [JsonPropertyName("sequence")]
    public long Sequence { get; }

    [JsonPropertyName("operationKind")]
    public string OperationKind { get; }

    [JsonPropertyName("affectedTarget")]
    public RuntimeTargetContext AffectedTarget { get; }

    [JsonPropertyName("confidence")]
    public string Confidence { get; }

    [JsonPropertyName("provenance")]
    public string Provenance { get; }

    [JsonPropertyName("suggestedTargetKind")]
    public string SuggestedTargetKind { get; }

    [JsonPropertyName("sourceFileStatus")]
    public string SourceFileStatus { get; }

    [JsonPropertyName("suggestedFilePath")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? SuggestedFilePath { get; }

    [JsonPropertyName("suggestedMember")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? SuggestedMember { get; }

    [JsonPropertyName("suggestedProperty")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? SuggestedProperty { get; }

    [JsonPropertyName("suggestedClass")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? SuggestedClass { get; }

    [JsonPropertyName("suggestedResourceKey")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? SuggestedResourceKey { get; }

    [JsonPropertyName("suggestedAction")]
    public string SuggestedAction { get; }

    [JsonPropertyName("limitations")]
    public IReadOnlyList<string> Limitations { get; }

    [JsonPropertyName("metadata")]
    public IReadOnlyDictionary<string, string> Metadata { get; }

    private static string? NormalizeOptionalText(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static string? NormalizeOptionalPath(string? path)
    {
        return string.IsNullOrWhiteSpace(path) ? null : Path.GetFullPath(path.Trim());
    }
}
