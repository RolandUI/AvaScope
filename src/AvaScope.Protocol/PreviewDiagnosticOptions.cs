using System.Text.Json.Serialization;

namespace AvaScope.Protocol;

public static class PreviewMinimumSeverities
{
    public const string All = "all";
    public const string Info = PreviewDiagnosticSeverities.Info;
    public const string Warning = PreviewDiagnosticSeverities.Warning;
    public const string Error = PreviewDiagnosticSeverities.Error;

    public static readonly IReadOnlyList<string> Values = [All, Info, Warning, Error];
}

public sealed record PreviewDiagnosticOptions
{
    [JsonConstructor]
    public PreviewDiagnosticOptions(
        string minimumSeverity = PreviewMinimumSeverities.All,
        bool errorsOnly = false,
        string? baselinePath = null,
        IReadOnlyList<string>? baselineFingerprints = null)
    {
        if (!PreviewMinimumSeverities.Values.Contains(minimumSeverity, StringComparer.OrdinalIgnoreCase))
        {
            throw new ArgumentException(
                $"Minimum severity must be one of: {string.Join(", ", PreviewMinimumSeverities.Values)}.",
                nameof(minimumSeverity));
        }

        MinimumSeverity = errorsOnly
            ? PreviewMinimumSeverities.Error
            : minimumSeverity.ToLowerInvariant();
        ErrorsOnly = errorsOnly;
        BaselinePath = string.IsNullOrWhiteSpace(baselinePath) ? null : Path.GetFullPath(baselinePath);
        var normalizedFingerprints = (baselineFingerprints ?? [])
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .Select(static value => value.Trim().ToLowerInvariant())
            .Distinct(StringComparer.Ordinal)
            .Take(PreviewDiagnosticBaseline.MaximumFingerprints)
            .ToArray();
        if (normalizedFingerprints.Any(static value => !IsValidFingerprint(value)))
        {
            throw new ArgumentException(
                "Preview diagnostic fingerprints must be 64-character SHA-256 hexadecimal values.",
                nameof(baselineFingerprints));
        }

        BaselineFingerprints = normalizedFingerprints;
    }

    [JsonPropertyName("minimumSeverity")] public string MinimumSeverity { get; }
    [JsonPropertyName("errorsOnly")] public bool ErrorsOnly { get; }

    [JsonPropertyName("baselinePath")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? BaselinePath { get; }

    [JsonPropertyName("baselineFingerprints")]
    public IReadOnlyList<string> BaselineFingerprints { get; }

    public static bool IsValidFingerprint(string value) =>
        value.Length == 64 && value.All(Uri.IsHexDigit);
}

public sealed record PreviewDiagnosticBaseline
{
    public const int MaximumFingerprints = 1000;

    [JsonConstructor]
    public PreviewDiagnosticBaseline(
        IReadOnlyList<string>? fingerprints,
        DateTimeOffset? generatedAt = null,
        IReadOnlyList<PreviewDiagnostic>? diagnostics = null)
    {
        var normalizedFingerprints = (fingerprints ?? [])
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .Select(static value => value.Trim().ToLowerInvariant())
            .Take(MaximumFingerprints)
            .ToArray();
        if (normalizedFingerprints.Any(static value => !PreviewDiagnosticOptions.IsValidFingerprint(value)))
        {
            throw new ArgumentException(
                "Preview diagnostic fingerprints must be 64-character SHA-256 hexadecimal values.",
                nameof(fingerprints));
        }

        Fingerprints = normalizedFingerprints;
        GeneratedAt = generatedAt;
        Diagnostics = (diagnostics ?? []).Take(MaximumFingerprints).ToArray();
    }

    [JsonPropertyName("fingerprints")] public IReadOnlyList<string> Fingerprints { get; }
    [JsonPropertyName("generatedAt")] public DateTimeOffset? GeneratedAt { get; }
    [JsonPropertyName("diagnostics")] public IReadOnlyList<PreviewDiagnostic> Diagnostics { get; }
}
