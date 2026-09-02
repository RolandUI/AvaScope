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
            Path.Combine(root, "CONTRIBUTING.md"),
            Path.Combine(root, "SECURITY.md"),
            Path.Combine(root, "TRADEMARKS.md"),
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
    public void RepositoryDocumentsPublicInstallContributionSecurityAndBrandingPolicies()
    {
        var root = FindRepositoryRoot();
        var readme = File.ReadAllText(Path.Combine(root, "README.md"));
        var contributing = File.ReadAllText(Path.Combine(root, "CONTRIBUTING.md"));
        var security = File.ReadAllText(Path.Combine(root, "SECURITY.md"));
        var trademarks = File.ReadAllText(Path.Combine(root, "TRADEMARKS.md"));

        Assert.Contains("releases/latest/download/AvaScopeSetup.exe", readme, StringComparison.Ordinal);
        Assert.Contains("releases/latest/download/avascope-linux-x64-installer", readme, StringComparison.Ordinal);
        Assert.Contains("not Authenticode-signed", readme, StringComparison.Ordinal);
        Assert.Contains("SECURITY.md", readme, StringComparison.Ordinal);
        Assert.Contains("CONTRIBUTING.md", readme, StringComparison.Ordinal);
        Assert.Contains("TRADEMARKS.md", readme, StringComparison.Ordinal);

        Assert.Contains("dotnet test AvaScope.slnx", contributing, StringComparison.Ordinal);
        Assert.Contains("Apache License 2.0", contributing, StringComparison.Ordinal);
        Assert.Contains("autonomous", contributing, StringComparison.OrdinalIgnoreCase);

        Assert.Contains("security/advisories/new", security, StringComparison.Ordinal);
        Assert.Contains("docs/SECURITY_THREAT_MODEL.md", security, StringComparison.Ordinal);
        Assert.Contains("Do not open a public issue", security, StringComparison.Ordinal);

        Assert.Contains("does not grant", trademarks, StringComparison.Ordinal);
        Assert.Contains("AvaScope name", trademarks, StringComparison.Ordinal);
        Assert.Contains("modified distributions", trademarks, StringComparison.Ordinal);
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
        Assert.Contains("install-avascope.ps1", userGuide, StringComparison.Ordinal);
        Assert.Contains("avascope.discovery.json", userGuide, StringComparison.Ordinal);
        Assert.Contains("Agent discovery order", userGuide, StringComparison.Ordinal);
        Assert.Contains("## Getting Started Sample", userGuide, StringComparison.Ordinal);
        Assert.Contains("## CLI", userGuide, StringComparison.Ordinal);
        Assert.Contains("## MCP", userGuide, StringComparison.Ordinal);
        Assert.Contains("## Runtime Bridge", userGuide, StringComparison.Ordinal);
        Assert.Contains("## Preview Host", userGuide, StringComparison.Ordinal);
        Assert.Contains("## Safety Boundaries", userGuide, StringComparison.Ordinal);
        Assert.Contains("mutate-node-evidence", userGuide, StringComparison.Ordinal);
        Assert.Contains("baseline-check", userGuide, StringComparison.Ordinal);
        Assert.Contains("create-preview-session", userGuide, StringComparison.Ordinal);
        Assert.Contains("press_and_hold", userGuide, StringComparison.Ordinal);
        Assert.Contains("IRangeValueProvider", userGuide, StringComparison.Ordinal);
        Assert.Contains("destinationSelector", userGuide, StringComparison.Ordinal);

        var agentWorkflow = File.ReadAllText(Path.Combine(root, "docs", "AGENT_WORKFLOW.md"));
        Assert.Contains("--distance-percent", agentWorkflow, StringComparison.Ordinal);
        Assert.Contains("cancellation releases a pressed pointer", agentWorkflow, StringComparison.Ordinal);

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

    [Fact]
    public void PrimaryDocumentationCoversUnsignedMacOsAgentWorkflow()
    {
        var root = FindRepositoryRoot();
        var readme = File.ReadAllText(Path.Combine(root, "README.md"));
        var agentWorkflow = File.ReadAllText(Path.Combine(root, "docs", "AGENT_WORKFLOW.md"));
        var stableSurface = File.ReadAllText(Path.Combine(root, "docs", "STABLE_SURFACE.md"));
        var troubleshooting = File.ReadAllText(Path.Combine(root, "docs", "TROUBLESHOOTING.md"));
        var validation = File.ReadAllText(Path.Combine(root, "docs", "VALIDATION.md"));

        foreach (var document in new[] { readme, agentWorkflow, troubleshooting })
        {
            Assert.Contains("unsigned and unnotarized", document, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("release-manifest.json", document, StringComparison.Ordinal);
            Assert.Contains("xattr -d com.apple.quarantine", document, StringComparison.Ordinal);
        }

        Assert.Contains("Open Anyway", readme, StringComparison.Ordinal);
        Assert.Contains("MDM", readme, StringComparison.Ordinal);
        Assert.Contains("does not use `sudo`", readme, StringComparison.Ordinal);
        Assert.Contains("does not edit shell profiles", agentWorkflow, StringComparison.Ordinal);
        Assert.Contains("avascope-osx-arm64-framework-dependent.zip", stableSurface, StringComparison.Ordinal);
        Assert.Contains("avascope-osx-x64-framework-dependent.zip", stableSurface, StringComparison.Ordinal);
        Assert.Contains("avascope-osx-arm64-installer", stableSurface, StringComparison.Ordinal);
        Assert.Contains("avascope-osx-x64-installer", stableSurface, StringComparison.Ordinal);
        Assert.Contains("test-macos-packaged-workflow.sh", validation, StringComparison.Ordinal);
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
