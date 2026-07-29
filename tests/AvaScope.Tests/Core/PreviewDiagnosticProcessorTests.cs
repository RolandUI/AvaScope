using System.Text.Json;
using AvaScope.Core;
using AvaScope.Protocol;

namespace AvaScope.Tests.Core;

public sealed class PreviewDiagnosticProcessorTests : IDisposable
{
    private readonly string _testRoot = Path.Combine(
        Path.GetTempPath(),
        "AvaScope.Tests",
        $"preview-diagnostics-{Guid.NewGuid():N}");

    public void Dispose()
    {
        if (Directory.Exists(_testRoot))
        {
            Directory.Delete(_testRoot, recursive: true);
        }
    }

    [Fact]
    public void FingerprintIsStableAcrossMessageAndBoundsChanges()
    {
        var first = Diagnostic(
            PreviewDiagnosticSeverities.Warning,
            "binding_warning",
            "First runtime value.",
            new NodeBounds(1, 2, 3, 4));
        var second = Diagnostic(
            PreviewDiagnosticSeverities.Warning,
            "binding_warning",
            "Different runtime value.",
            new NodeBounds(10, 20, 30, 40));

        var firstFingerprint = PreviewDiagnosticProcessor.CreateFingerprint(first);
        var secondFingerprint = PreviewDiagnosticProcessor.CreateFingerprint(second);

        Assert.Equal(firstFingerprint, secondFingerprint);
        Assert.Equal(64, firstFingerprint.Length);
        Assert.NotEqual(
            firstFingerprint,
            PreviewDiagnosticProcessor.CreateFingerprint(
                Diagnostic(PreviewDiagnosticSeverities.Warning, "different_code", "Message.")));
        Assert.Throws<ArgumentException>(
            () => new PreviewDiagnosticOptions(baselineFingerprints: ["not-a-sha256"]));
    }

    [Fact]
    public async Task BaselineComparisonClassifiesNewExistingAndResolvedBeforeFiltering()
    {
        Directory.CreateDirectory(_testRoot);
        var baselinePath = Path.Combine(_testRoot, "baseline.json");
        var artifactPath = Path.Combine(_testRoot, "current.json");
        var existing = Diagnostic(
            PreviewDiagnosticSeverities.Warning,
            "binding_warning",
            "Baseline warning.");
        var resolved = Diagnostic(
            PreviewDiagnosticSeverities.Error,
            "xaml_error",
            "Baseline error.");
        await File.WriteAllTextAsync(
            baselinePath,
            JsonSerializer.Serialize(new[] { existing, resolved }));

        var result = await new PreviewDiagnosticProcessor().ProcessAsync(
            [
                Diagnostic(
                    PreviewDiagnosticSeverities.Warning,
                    "binding_warning",
                    "Message changed but identity is stable."),
                Diagnostic(PreviewDiagnosticSeverities.Info, "project_info", "New info.")
            ],
            artifactPath,
            new PreviewDiagnosticOptions(
                minimumSeverity: PreviewMinimumSeverities.Warning,
                baselinePath: baselinePath));

        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal("existing", diagnostic.BaselineStatus);
        Assert.Equal("artifact", result.Summary.ComparisonProvenance);
        Assert.Equal(0, result.Summary.NewCount);
        Assert.Equal(1, result.Summary.ExistingCount);
        Assert.Equal(1, result.Summary.ResolvedCount);
        Assert.Equal(2, result.Summary.BaselineCount);
        Assert.Null(result.Summary.ComparisonError);
        Assert.True(File.Exists(result.ArtifactPath));

        var complete = JsonSerializer.Deserialize<PreviewDiagnostic[]>(
            await File.ReadAllTextAsync(result.ArtifactPath));
        Assert.Equal(2, complete!.Length);
        Assert.Contains(complete, item => item.Code == "project_info" && item.BaselineStatus == "new");
    }

    [Fact]
    public async Task MissingBaselineReturnsActionableComparisonErrorWithoutDroppingDiagnostics()
    {
        Directory.CreateDirectory(_testRoot);
        var result = await new PreviewDiagnosticProcessor().ProcessAsync(
            [Diagnostic(PreviewDiagnosticSeverities.Error, "xaml_error", "Render diagnostic.")],
            Path.Combine(_testRoot, "current.json"),
            new PreviewDiagnosticOptions(
                baselinePath: Path.Combine(_testRoot, "missing.json")));

        Assert.Single(result.Diagnostics);
        Assert.Equal("invalid", result.Summary.ComparisonProvenance);
        Assert.Equal(CoreErrorCodes.PreviewDiagnosticsBaselineInvalid, result.Summary.ComparisonError!.Code);
        Assert.Contains("existing", result.Summary.ComparisonError.Details!["nextAction"], StringComparison.Ordinal);
        Assert.Null(result.Summary.NewCount);

        var malformedPath = Path.Combine(_testRoot, "malformed.json");
        await File.WriteAllTextAsync(malformedPath, "{}");
        var malformed = await new PreviewDiagnosticProcessor().ProcessAsync(
            [Diagnostic(PreviewDiagnosticSeverities.Warning, "binding_warning", "Warning.")],
            Path.Combine(_testRoot, "malformed-current.json"),
            new PreviewDiagnosticOptions(baselinePath: malformedPath));
        Assert.Equal(
            CoreErrorCodes.PreviewDiagnosticsBaselineInvalid,
            malformed.Summary.ComparisonError!.Code);
        Assert.Single(malformed.Diagnostics);
    }

    private static PreviewDiagnostic Diagnostic(
        string severity,
        string code,
        string message,
        NodeBounds? bounds = null) =>
        new(
            severity,
            PreviewDiagnosticCategories.Binding,
            code,
            message,
            nodeId: "visual:title",
            nodeType: "TextBlock",
            propertyName: "Text",
            sourcePath: "Views/MainView.axaml",
            bounds: bounds,
            phase: "render",
            provenance: "avalonia");
}
