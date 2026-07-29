using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AvaScope.Protocol;

namespace AvaScope.Core;

public sealed class PreviewDiagnosticProcessor
{
    public const int MaximumInlineDiagnostics = 16;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<PreviewDiagnosticProcessingResult> ProcessAsync(
        IReadOnlyList<PreviewDiagnostic> diagnostics,
        string artifactPath,
        PreviewDiagnosticOptions? options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(diagnostics);
        ArgumentException.ThrowIfNullOrWhiteSpace(artifactPath);

        options ??= new PreviewDiagnosticOptions();
        var baseline = await LoadBaselineAsync(options, cancellationToken);
        var remaining = baseline.Fingerprints
            .GroupBy(static value => value, StringComparer.Ordinal)
            .ToDictionary(static group => group.Key, static group => group.Count(), StringComparer.Ordinal);
        var comparisonAvailable = baseline.IsConfigured && baseline.Error is null;
        var annotated = diagnostics.Select(diagnostic =>
        {
            var fingerprint = CreateFingerprint(diagnostic);
            string? baselineStatus = null;
            if (comparisonAvailable)
            {
                if (remaining.TryGetValue(fingerprint, out var count) && count > 0)
                {
                    baselineStatus = "existing";
                    remaining[fingerprint] = count - 1;
                }
                else
                {
                    baselineStatus = "new";
                }
            }

            return Copy(diagnostic, fingerprint, baselineStatus);
        }).ToArray();

        var fullArtifactPath = Path.GetFullPath(artifactPath);
        var directory = Path.GetDirectoryName(fullArtifactPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await File.WriteAllTextAsync(
            fullArtifactPath,
            JsonSerializer.Serialize(annotated, JsonOptions),
            cancellationToken);

        var filtered = annotated
            .Where(item => MeetsMinimumSeverity(item.Severity, options.MinimumSeverity))
            .ToArray();
        var inline = filtered
            .OrderBy(static item => SeverityRank(item.Severity) * -1)
            .ThenBy(static item => item.Category, StringComparer.Ordinal)
            .ThenBy(static item => item.Code, StringComparer.Ordinal)
            .Take(MaximumInlineDiagnostics)
            .ToArray();
        var severityCounts = filtered
            .GroupBy(static item => item.Severity, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(static group => group.Key, static group => group.Count(), StringComparer.OrdinalIgnoreCase);
        var categoryCounts = filtered
            .GroupBy(static item => item.Category, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(static group => group.Key, static group => group.Count(), StringComparer.OrdinalIgnoreCase);
        int? newCount = comparisonAvailable
            ? filtered.Count(static item => item.BaselineStatus == "new")
            : null;
        int? existingCount = comparisonAvailable
            ? filtered.Count(static item => item.BaselineStatus == "existing")
            : null;
        int? resolvedCount = comparisonAvailable ? remaining.Values.Sum() : null;
        var errors = severityCounts.GetValueOrDefault(PreviewDiagnosticSeverities.Error);
        var warnings = severityCounts.GetValueOrDefault(PreviewDiagnosticSeverities.Warning);
        var comparisonText = comparisonAvailable
            ? $" {newCount} new, {existingCount} existing, {resolvedCount} resolved."
            : string.Empty;
        var summary = new PreviewDiagnosticSummary(
            filtered.Length,
            severityCounts,
            categoryCounts,
            $"{filtered.Length} diagnostic(s): {errors} error(s), {warnings} warning(s).{comparisonText}",
            baseline.Provenance,
            newCount,
            existingCount,
            resolvedCount,
            filtered.Length > inline.Length,
            inline.Length,
            baseline.IsConfigured ? baseline.Fingerprints.Count : null,
            baseline.Error);
        return new PreviewDiagnosticProcessingResult(inline, summary, fullArtifactPath);
    }

    public static string CreateFingerprint(PreviewDiagnostic diagnostic)
    {
        ArgumentNullException.ThrowIfNull(diagnostic);

        var canonical = string.Join(
            "\u001f",
            Normalize(diagnostic.Severity),
            Normalize(diagnostic.Category),
            Normalize(diagnostic.Code),
            Normalize(diagnostic.NodeId),
            Normalize(diagnostic.NodeType),
            Normalize(diagnostic.PropertyName),
            NormalizePath(diagnostic.SourcePath),
            Normalize(diagnostic.Phase),
            Normalize(diagnostic.Provenance));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant();
    }

    private static async Task<BaselineLoadResult> LoadBaselineAsync(
        PreviewDiagnosticOptions options,
        CancellationToken cancellationToken)
    {
        var fingerprints = new List<string>(options.BaselineFingerprints);
        var hasPath = options.BaselinePath is not null;
        ProtocolError? error = null;
        if (hasPath)
        {
            try
            {
                if (!File.Exists(options.BaselinePath))
                {
                    throw new FileNotFoundException("The preview diagnostics baseline artifact was not found.");
                }

                var json = await File.ReadAllTextAsync(options.BaselinePath!, cancellationToken);
                fingerprints.AddRange(ReadFingerprints(json));
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException or ArgumentException)
            {
                error = new ProtocolError(
                    CoreErrorCodes.PreviewDiagnosticsBaselineInvalid,
                    exception.Message,
                    new Dictionary<string, string>
                    {
                        ["baselinePath"] = options.BaselinePath!,
                        ["nextAction"] = "Provide an existing preview diagnostics artifact, a PreviewDiagnosticBaseline object, or explicit SHA-256 fingerprints."
                    });
            }
        }

        var normalized = fingerprints
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .Select(static value => value.Trim().ToLowerInvariant())
            .Take(PreviewDiagnosticBaseline.MaximumFingerprints)
            .ToArray();
        if (normalized.Any(static value => !PreviewDiagnosticOptions.IsValidFingerprint(value)))
        {
            error = new ProtocolError(
                CoreErrorCodes.PreviewDiagnosticsBaselineInvalid,
                "The preview diagnostics baseline contains a value that is not a SHA-256 fingerprint.",
                new Dictionary<string, string>
                {
                    ["nextAction"] = "Regenerate the baseline from a preview .diagnostics.json artifact or provide 64-character hexadecimal fingerprints."
                });
        }
        var configured = hasPath || options.BaselineFingerprints.Count > 0;
        var provenance = (hasPath, options.BaselineFingerprints.Count > 0, error) switch
        {
            (_, _, not null) => "invalid",
            (true, true, _) => "artifact+fingerprint_set",
            (true, false, _) => "artifact",
            (false, true, _) => "fingerprint_set",
            _ => "unavailable"
        };
        return new BaselineLoadResult(configured, normalized, provenance, error);
    }

    private static IReadOnlyList<string> ReadFingerprints(string json)
    {
        using var document = JsonDocument.Parse(json);
        if (document.RootElement.ValueKind == JsonValueKind.Array)
        {
            if (document.RootElement.GetArrayLength() == 0)
            {
                return [];
            }

            return document.RootElement[0].ValueKind == JsonValueKind.String
                ? JsonSerializer.Deserialize<string[]>(json, JsonOptions) ?? []
                : (JsonSerializer.Deserialize<PreviewDiagnostic[]>(json, JsonOptions) ?? [])
                    .Select(CreateFingerprint)
                    .ToArray();
        }

        if (document.RootElement.ValueKind != JsonValueKind.Object
            || (!document.RootElement.TryGetProperty("fingerprints", out _)
                && !document.RootElement.TryGetProperty("diagnostics", out _)))
        {
            throw new JsonException(
                "The preview diagnostics baseline must be an array of diagnostics, an array of fingerprints, or an object with fingerprints/diagnostics.");
        }

        var baseline = JsonSerializer.Deserialize<PreviewDiagnosticBaseline>(json, JsonOptions)
            ?? throw new JsonException("The preview diagnostics baseline artifact was empty.");
        return baseline.Fingerprints
            .Concat(baseline.Diagnostics.Select(CreateFingerprint))
            .ToArray();
    }

    private static PreviewDiagnostic Copy(
        PreviewDiagnostic diagnostic,
        string fingerprint,
        string? baselineStatus) =>
        new(
            diagnostic.Severity,
            diagnostic.Category,
            diagnostic.Code,
            diagnostic.Message,
            diagnostic.NodeId,
            diagnostic.NodeType,
            diagnostic.PropertyName,
            diagnostic.SourcePath,
            diagnostic.Bounds,
            diagnostic.Details,
            diagnostic.Phase,
            diagnostic.Provenance,
            diagnostic.SuggestedAction,
            diagnostic.SuppressionReason,
            fingerprint,
            baselineStatus);

    private static bool MeetsMinimumSeverity(string severity, string minimumSeverity) =>
        SeverityRank(severity) >= SeverityRank(minimumSeverity);

    private static int SeverityRank(string severity) =>
        severity.ToLowerInvariant() switch
        {
            PreviewDiagnosticSeverities.Error => 2,
            PreviewDiagnosticSeverities.Warning => 1,
            _ => 0
        };

    private static string Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim().ToLowerInvariant();

    private static string NormalizePath(string? value) =>
        Normalize(value).Replace('\\', '/');

    private sealed record BaselineLoadResult(
        bool IsConfigured,
        IReadOnlyList<string> Fingerprints,
        string Provenance,
        ProtocolError? Error);
}

public sealed record PreviewDiagnosticProcessingResult(
    IReadOnlyList<PreviewDiagnostic> Diagnostics,
    PreviewDiagnosticSummary Summary,
    string ArtifactPath);
