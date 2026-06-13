using System.Text.Json.Serialization;

namespace AvaScope.Protocol;

public sealed record RuntimeValidationState
{
    public const int MaximumErrors = 8;

    [JsonConstructor]
    public RuntimeValidationState(
        string status,
        string provenance,
        bool? hasErrors = null,
        int? errorCount = null,
        IReadOnlyList<string>? errors = null)
    {
        if (string.IsNullOrWhiteSpace(status))
        {
            throw new ArgumentException("Validation status cannot be empty.", nameof(status));
        }

        if (string.IsNullOrWhiteSpace(provenance))
        {
            throw new ArgumentException("Validation provenance cannot be empty.", nameof(provenance));
        }

        if (errorCount is < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(errorCount), errorCount, "Validation error count cannot be negative.");
        }

        Status = status.Trim();
        Provenance = provenance.Trim();
        HasErrors = hasErrors;
        ErrorCount = errorCount;
        Errors = (errors ?? [])
            .Where(static error => !string.IsNullOrWhiteSpace(error))
            .Select(static error => error.Trim())
            .Take(MaximumErrors)
            .ToArray();
    }

    [JsonPropertyName("status")]
    public string Status { get; }

    [JsonPropertyName("provenance")]
    public string Provenance { get; }

    [JsonPropertyName("hasErrors")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? HasErrors { get; }

    [JsonPropertyName("errorCount")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? ErrorCount { get; }

    [JsonPropertyName("errors")]
    public IReadOnlyList<string> Errors { get; }
}
