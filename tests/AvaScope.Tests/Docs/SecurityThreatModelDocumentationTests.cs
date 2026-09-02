namespace AvaScope.Tests.Docs;

public sealed class SecurityThreatModelDocumentationTests
{
    [Fact]
    public void SecurityThreatModelCoversRequiredReleaseAuditBoundaries()
    {
        var root = FindRepositoryRoot();
        var threatModelPath = Path.Combine(root, "docs", "SECURITY_THREAT_MODEL.md");
        var document = File.ReadAllText(threatModelPath);

        Assert.Contains("# AvaScope Security Threat Model", document, StringComparison.Ordinal);
        Assert.Contains("## Local-Only Transport", document, StringComparison.Ordinal);
        Assert.Contains("## Opt-In Bridge Activation", document, StringComparison.Ordinal);
        Assert.Contains("## Runtime Mutation Permissions", document, StringComparison.Ordinal);
        Assert.Contains("## Preview Execution", document, StringComparison.Ordinal);
        Assert.Contains("## File Outputs And Logs", document, StringComparison.Ordinal);
        Assert.Contains("## Package, API, CLI, And MCP Compatibility", document, StringComparison.Ordinal);
        Assert.Contains("## Unsafe Defaults Rejected", document, StringComparison.Ordinal);
        Assert.Contains("## Accepted Risks And Deferrals", document, StringComparison.Ordinal);
        Assert.Contains("local_only", document, StringComparison.Ordinal);
        Assert.Contains("AvaScopeBridge.Activate", document, StringComparison.Ordinal);
        Assert.Contains("AVASCOPE_SAMPLE_BRIDGE", document, StringComparison.Ordinal);
        Assert.Contains("runtime_mutation_non_local_session", document, StringComparison.Ordinal);
        Assert.Contains("capability_not_supported", document, StringComparison.Ordinal);
        Assert.Contains("environment values", document, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("PID-reused", document, StringComparison.Ordinal);
        Assert.DoesNotContain("TODO", document, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("TBD", document, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SecurityThreatModelIsLinkedFromPrimaryDocumentation()
    {
        var root = FindRepositoryRoot();
        var readme = File.ReadAllText(Path.Combine(root, "README.md"));
        var userGuide = File.ReadAllText(Path.Combine(root, "docs", "USER_GUIDE.md"));

        Assert.Contains("docs/SECURITY_THREAT_MODEL.md", readme, StringComparison.Ordinal);
        Assert.Contains("SECURITY_THREAT_MODEL.md", userGuide, StringComparison.Ordinal);
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
