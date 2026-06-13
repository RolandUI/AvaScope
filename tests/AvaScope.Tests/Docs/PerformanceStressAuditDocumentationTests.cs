namespace AvaScope.Tests.Docs;

public sealed class PerformanceStressAuditDocumentationTests
{
    [Fact]
    public void PerformanceStressAuditDocumentsBudgetsAndValidationCoverage()
    {
        var root = FindRepositoryRoot();
        var audit = File.ReadAllText(Path.Combine(root, "docs", "PERFORMANCE_STRESS_AUDIT.md"));

        Assert.Contains("# AvaScope Performance And Stress Audit", audit, StringComparison.Ordinal);
        Assert.Contains("## Automation Coverage", audit, StringComparison.Ordinal);
        Assert.Contains("## Bounded Output Budgets", audit, StringComparison.Ordinal);
        Assert.Contains("## Sample Workflow Coverage", audit, StringComparison.Ordinal);
        Assert.Contains("## Validation Commands", audit, StringComparison.Ordinal);
        Assert.Contains("large visual tree", audit, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("large diagnostics payload", audit, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("repeated preview", audit, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("repeated mutation", audit, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("persistent session", audit, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("baseline suite", audit, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("maxDepth", audit, StringComparison.Ordinal);
        Assert.Contains("64", audit, StringComparison.Ordinal);
        Assert.Contains("diagnosticIssues", audit, StringComparison.Ordinal);
        Assert.Contains("200", audit, StringComparison.Ordinal);
        Assert.Contains("RuntimeMutationReviewResponse.MaximumEntries", audit, StringComparison.Ordinal);
        Assert.Contains("PreviewSessionStore", audit, StringComparison.Ordinal);
        Assert.DoesNotContain("TODO", audit, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("TBD", audit, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TroubleshootingDocumentationCoversStableAgentFailureAreas()
    {
        var root = FindRepositoryRoot();
        var troubleshooting = File.ReadAllText(Path.Combine(root, "docs", "TROUBLESHOOTING.md"));
        var readme = File.ReadAllText(Path.Combine(root, "README.md"));
        var userGuide = File.ReadAllText(Path.Combine(root, "docs", "USER_GUIDE.md"));

        Assert.Contains("# AvaScope Troubleshooting", troubleshooting, StringComparison.Ordinal);
        Assert.Contains("## Attach And Bridge Sessions", troubleshooting, StringComparison.Ordinal);
        Assert.Contains("## Preview Rendering", troubleshooting, StringComparison.Ordinal);
        Assert.Contains("## Runtime Mutation", troubleshooting, StringComparison.Ordinal);
        Assert.Contains("## Reports And Visual Regression", troubleshooting, StringComparison.Ordinal);
        Assert.Contains("## Packages And Release Artifacts", troubleshooting, StringComparison.Ordinal);
        Assert.Contains("bridge_session_not_found", troubleshooting, StringComparison.Ordinal);
        Assert.Contains("preview_baseline_failed", troubleshooting, StringComparison.Ordinal);
        Assert.Contains("runtime_mutation_target_stale", troubleshooting, StringComparison.Ordinal);
        Assert.Contains("baseline-report.html", troubleshooting, StringComparison.Ordinal);
        Assert.Contains("release-manifest.json", troubleshooting, StringComparison.Ordinal);
        Assert.DoesNotContain("TODO", troubleshooting, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("TBD", troubleshooting, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("docs/TROUBLESHOOTING.md", readme, StringComparison.Ordinal);
        Assert.Contains("TROUBLESHOOTING.md", userGuide, StringComparison.Ordinal);
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
