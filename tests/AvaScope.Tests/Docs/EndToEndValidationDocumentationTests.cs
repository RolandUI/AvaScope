namespace AvaScope.Tests.Docs;

public sealed class EndToEndValidationDocumentationTests
{
    [Fact]
    public void EndToEndValidationDocumentsV1WorkflowMatrix()
    {
        var root = FindRepositoryRoot();
        var validation = File.ReadAllText(Path.Combine(root, "docs", "END_TO_END_VALIDATION.md"));

        Assert.Contains("# AvaScope End-To-End Validation", validation, StringComparison.Ordinal);
        Assert.Contains("## Source Validation", validation, StringComparison.Ordinal);
        Assert.Contains("## Release Artifact Validation", validation, StringComparison.Ordinal);
        Assert.Contains("## Packaged CLI Workflow Validation", validation, StringComparison.Ordinal);
        Assert.Contains("## Packaged Runtime Bridge Validation", validation, StringComparison.Ordinal);
        Assert.Contains("## Packaged MCP Validation", validation, StringComparison.Ordinal);
        Assert.Contains("## Open P0/P1 Audit", validation, StringComparison.Ordinal);
        Assert.Contains("dotnet restore AvaScope.slnx", validation, StringComparison.Ordinal);
        Assert.Contains("dotnet build AvaScope.slnx --no-restore -v:minimal", validation, StringComparison.Ordinal);
        Assert.Contains("dotnet test AvaScope.slnx --no-build", validation, StringComparison.Ordinal);
        Assert.Contains("eng\\create-local-release.ps1", validation, StringComparison.Ordinal);
        Assert.Contains("eng\\publish-github-release.ps1", validation, StringComparison.Ordinal);
        Assert.Contains("capabilities --require", validation, StringComparison.Ordinal);
        Assert.Contains("preview-animation", validation, StringComparison.Ordinal);
        Assert.Contains("baseline-check", validation, StringComparison.Ordinal);
        Assert.Contains("baseline-report.html", validation, StringComparison.Ordinal);
        Assert.Contains("launch-app", validation, StringComparison.Ordinal);
        Assert.Contains("mutate-node-evidence", validation, StringComparison.Ordinal);
        Assert.Contains("mutation-review", validation, StringComparison.Ordinal);
        Assert.Contains("tools/list", validation, StringComparison.Ordinal);
        Assert.Contains("No unexpected P0/P1 blocker", validation, StringComparison.Ordinal);
        Assert.Contains("Residual release risks", validation, StringComparison.Ordinal);
        Assert.DoesNotContain("TODO", validation, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("TBD", validation, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void EndToEndValidationIsLinkedFromPrimaryDocs()
    {
        var root = FindRepositoryRoot();
        var readme = File.ReadAllText(Path.Combine(root, "README.md"));
        var userGuide = File.ReadAllText(Path.Combine(root, "docs", "USER_GUIDE.md"));
        var validation = File.ReadAllText(Path.Combine(root, "docs", "VALIDATION.md"));
        var releasePlan = File.ReadAllText(Path.Combine(root, "docs", "RELEASE_PLAN.md"));

        Assert.Contains("docs/END_TO_END_VALIDATION.md", readme, StringComparison.Ordinal);
        Assert.Contains("END_TO_END_VALIDATION.md", userGuide, StringComparison.Ordinal);
        Assert.Contains("END_TO_END_VALIDATION.md", validation, StringComparison.Ordinal);
        Assert.Contains("END_TO_END_VALIDATION.md", releasePlan, StringComparison.Ordinal);
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
