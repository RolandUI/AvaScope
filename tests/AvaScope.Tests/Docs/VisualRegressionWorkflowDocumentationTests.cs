namespace AvaScope.Tests.Docs;

public sealed class VisualRegressionWorkflowDocumentationTests
{
    [Fact]
    public void VisualRegressionWorkflowExampleUploadsReportPackWithoutPublishingPermissions()
    {
        var root = FindRepositoryRoot();
        var workflowPath = Path.Combine(
            root,
            "docs",
            "examples",
            "github-actions",
            "avascope-visual-regression.yml");
        var workflow = File.ReadAllText(workflowPath);
        var normalized = workflow.Replace("\r\n", "\n", StringComparison.Ordinal);

        Assert.Contains("workflow_dispatch:", normalized, StringComparison.Ordinal);
        Assert.Contains("permissions:\n  contents: read", normalized, StringComparison.Ordinal);
        Assert.Contains("actions/upload-artifact@v4", normalized, StringComparison.Ordinal);
        Assert.Contains("baseline-check", normalized, StringComparison.Ordinal);
        Assert.Contains("--report-pack", normalized, StringComparison.Ordinal);
        Assert.Contains("baseline-report.json", normalized, StringComparison.Ordinal);
        Assert.Contains("eng\\create-local-release.ps1 -SkipTests", normalized, StringComparison.Ordinal);
        Assert.DoesNotContain("NUGET_API_KEY", normalized, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("packages: write", normalized, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("contents: write", normalized, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("publish-nuget.ps1", normalized, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("publish-github-release.ps1", normalized, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void VisualRegressionDocumentationLinksWorkflowExampleAndExplainsReviewSemantics()
    {
        var root = FindRepositoryRoot();
        var documentationPath = Path.Combine(root, "docs", "VISUAL_REGRESSION_CI.md");
        var workflowPath = Path.Combine(
            root,
            "docs",
            "examples",
            "github-actions",
            "avascope-visual-regression.yml");
        var documentation = File.ReadAllText(documentationPath);

        Assert.True(File.Exists(workflowPath), workflowPath);
        Assert.Contains(
            "examples/github-actions/avascope-visual-regression.yml",
            documentation,
            StringComparison.Ordinal);
        Assert.Contains("if: always()", documentation, StringComparison.Ordinal);
        Assert.Contains("changed variants fail the job", documentation, StringComparison.Ordinal);
        Assert.Contains("Release Workflow Separation", documentation, StringComparison.Ordinal);
        Assert.Contains("does not require `NUGET_API_KEY`", documentation, StringComparison.Ordinal);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "AvaScope.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate repository root from the test output directory.");
    }
}
