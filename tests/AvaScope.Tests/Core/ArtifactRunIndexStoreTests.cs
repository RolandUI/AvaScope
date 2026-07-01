using System.Text.Json;
using System.Text.Json.Nodes;
using AvaScope.Core;
using AvaScope.Protocol;

namespace AvaScope.Tests.Core;

public sealed class ArtifactRunIndexStoreTests : IDisposable
{
    private readonly string _testRoot = Path.Combine(
        Path.GetTempPath(),
        "AvaScope.Tests",
        $"run-index-{Guid.NewGuid():N}");

    public ArtifactRunIndexStoreTests()
    {
        Directory.CreateDirectory(_testRoot);
    }

    [Fact]
    public void WriteCreatesJsonHtmlAndResolvableLatestPointer()
    {
        var generatedAt = new DateTimeOffset(2026, 7, 1, 12, 0, 0, TimeSpan.Zero);
        var projectPath = Path.Combine(_testRoot, "Sample.csproj");
        var screenshotPath = Path.Combine(_testRoot, "preview.png");
        var reportPath = Path.Combine(_testRoot, "report.json");
        File.WriteAllText(projectPath, "<Project />");
        File.WriteAllText(screenshotPath, "png");
        File.WriteAllText(reportPath, "{}");
        var request = new ArtifactRunIndexRequest(
            "preview",
            "completed_with_warnings",
            projectPath: projectPath,
            viewPath: "Views/MainView.axaml",
            profile: "main",
            variant: "desktop",
            stateVariant: "loading",
            artifacts:
            [
                new ArtifactRunIndexArtifact("preview_screenshot", screenshotPath, "Preview screenshot.", "image/png")
            ],
            diagnostics:
            [
                new ArtifactRunIndexDiagnostic(
                    "warning",
                    "binding",
                    "binding_missing_datacontext",
                    "Binding has no DataContext.")
            ],
            warnings: ["binding_missing_datacontext: Binding has no DataContext."],
            generatedReports:
            [
                new ArtifactRunIndexArtifact("json_report", reportPath, "Machine-readable report.", "application/json")
            ]);
        var store = new ArtifactRunIndexStore(_testRoot, new ManualTimeProvider(generatedAt));

        var result = store.Write(request);

        Assert.True(result.Success, result.Error?.Message);
        Assert.Equal("preview", result.Value!.Command);
        Assert.Equal("completed_with_warnings", result.Value.Status);
        Assert.Equal(Path.GetFullPath(projectPath), result.Value.ProjectPath);
        Assert.Equal("Views/MainView.axaml", result.Value.ViewPath);
        Assert.Equal("loading", result.Value.StateVariant);
        Assert.Equal(Path.GetFullPath(screenshotPath), Assert.Single(result.Value.ScreenshotPaths));
        Assert.True(File.Exists(result.Value.IndexJsonPath), result.Value.IndexJsonPath);
        Assert.True(File.Exists(result.Value.IndexHtmlPath), result.Value.IndexHtmlPath);
        Assert.True(File.Exists(result.Value.LatestPointerPath), result.Value.LatestPointerPath);

        var json = JsonNode.Parse(File.ReadAllText(result.Value.IndexJsonPath))!;
        Assert.Equal(result.Value.RunId, json["runId"]!.GetValue<string>());
        Assert.Equal("preview_screenshot", json["artifacts"]![0]!["kind"]!.GetValue<string>());
        Assert.Equal("json_report", json["generatedReports"]![0]!["kind"]!.GetValue<string>());
        Assert.Contains("AvaScope Run Index", File.ReadAllText(result.Value.IndexHtmlPath), StringComparison.Ordinal);

        var latest = store.ResolveLatest(new ArtifactRunIndexSelector(
            projectPath: projectPath,
            viewPath: "Views/MainView.axaml",
            profile: "main",
            variant: "desktop",
            stateVariant: "loading"));

        Assert.True(latest.Success, latest.Error?.Message);
        Assert.Equal(result.Value.RunId, latest.Value!.RunId);
        Assert.Equal(result.Value.IndexJsonPath, latest.Value.IndexJsonPath);
    }

    [Fact]
    public void ResolveLatestReturnsStructuredErrorWhenPointerIsMissing()
    {
        var store = new ArtifactRunIndexStore(_testRoot);

        var result = store.ResolveLatest(new ArtifactRunIndexSelector(taskName: "missing-task"));

        Assert.False(result.Success);
        Assert.Equal(CoreErrorCodes.ArtifactRunIndexUnavailable, result.Error!.Code);
        Assert.Equal(ArtifactRunIndexStore.CreateTaskKey(new ArtifactRunIndexSelector(taskName: "missing-task")), result.Error.Details!["taskKey"]);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testRoot))
        {
            Directory.Delete(_testRoot, recursive: true);
        }
    }

    private sealed class ManualTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow()
        {
            return utcNow;
        }
    }
}
