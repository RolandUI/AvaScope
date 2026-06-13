namespace AvaScope.Tests.Docs;

public sealed class DocumentationCompletionTests
{
    [Fact]
    public void PrimaryDocumentationUsesStableV1Positioning()
    {
        var root = FindRepositoryRoot();
        var primaryDocs = new[]
        {
            Path.Combine(root, "README.md"),
            Path.Combine(root, "docs", "AGENT_WORKFLOW.md"),
            Path.Combine(root, "docs", "SECURITY_THREAT_MODEL.md"),
            Path.Combine(root, "docs", "STABLE_SURFACE.md"),
            Path.Combine(root, "docs", "TROUBLESHOOTING.md"),
            Path.Combine(root, "docs", "UPGRADE.md"),
            Path.Combine(root, "docs", "USER_GUIDE.md"),
            Path.Combine(root, "docs", "VALIDATION.md"),
            Path.Combine(root, "docs", "VISUAL_REGRESSION_CI.md")
        };

        foreach (var path in primaryDocs)
        {
            var document = File.ReadAllText(path);
            Assert.DoesNotContain("public-alpha", document, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("public alpha", document, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("pre-1.0", document, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("pre 1.0", document, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("0.1.0", document, StringComparison.Ordinal);
            Assert.DoesNotContain("v0.2.0", document, StringComparison.Ordinal);
            Assert.DoesNotContain("TODO", document, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("TBD", document, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void UserDocumentationCoversStableInstallUpgradeAndWorkflowEntrypoints()
    {
        var root = FindRepositoryRoot();
        var readme = File.ReadAllText(Path.Combine(root, "README.md"));
        var userGuide = File.ReadAllText(Path.Combine(root, "docs", "USER_GUIDE.md"));
        var upgrade = File.ReadAllText(Path.Combine(root, "docs", "UPGRADE.md"));

        Assert.Contains("docs/UPGRADE.md", readme, StringComparison.Ordinal);
        Assert.Contains("UPGRADE.md", userGuide, StringComparison.Ordinal);

        Assert.Contains("## Install From Release Artifacts", userGuide, StringComparison.Ordinal);
        Assert.Contains("## Getting Started Sample", userGuide, StringComparison.Ordinal);
        Assert.Contains("## CLI", userGuide, StringComparison.Ordinal);
        Assert.Contains("## MCP", userGuide, StringComparison.Ordinal);
        Assert.Contains("## Runtime Bridge", userGuide, StringComparison.Ordinal);
        Assert.Contains("## Preview Host", userGuide, StringComparison.Ordinal);
        Assert.Contains("## Safety Boundaries", userGuide, StringComparison.Ordinal);
        Assert.Contains("mutate-node-evidence", userGuide, StringComparison.Ordinal);
        Assert.Contains("baseline-check", userGuide, StringComparison.Ordinal);
        Assert.Contains("create-preview-session", userGuide, StringComparison.Ordinal);

        Assert.Contains("# AvaScope Upgrade And Compatibility", upgrade, StringComparison.Ordinal);
        Assert.Contains("AvaScope.Protocol", upgrade, StringComparison.Ordinal);
        Assert.Contains("AvaScope.Core", upgrade, StringComparison.Ordinal);
        Assert.Contains("AvaScope.Bridge", upgrade, StringComparison.Ordinal);
        Assert.Contains("same major version", upgrade, StringComparison.Ordinal);
        Assert.Contains("capabilities", upgrade, StringComparison.Ordinal);
        Assert.Contains("capability_not_supported", upgrade, StringComparison.Ordinal);
        Assert.Contains("bridge_protocol_incompatible", upgrade, StringComparison.Ordinal);
        Assert.Contains("avascope doctor", upgrade, StringComparison.Ordinal);
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
