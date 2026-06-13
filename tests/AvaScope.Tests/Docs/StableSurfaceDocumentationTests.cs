using AvaScope.Protocol;

namespace AvaScope.Tests.Docs;

public sealed class StableSurfaceDocumentationTests
{
    [Fact]
    public void StableSurfaceDocumentationCoversFrozenContracts()
    {
        var root = FindRepositoryRoot();
        var stableSurface = File.ReadAllText(Path.Combine(root, "docs", "STABLE_SURFACE.md"));

        Assert.Contains("# AvaScope Stable Surface", stableSurface, StringComparison.Ordinal);
        Assert.Contains("## Public Packages", stableSurface, StringComparison.Ordinal);
        Assert.Contains("## Protocol Contracts", stableSurface, StringComparison.Ordinal);
        Assert.Contains("## CLI Commands", stableSurface, StringComparison.Ordinal);
        Assert.Contains("## MCP Tools", stableSurface, StringComparison.Ordinal);
        Assert.Contains("## Exit Codes", stableSurface, StringComparison.Ordinal);
        Assert.Contains("## Artifact Names", stableSurface, StringComparison.Ordinal);
        Assert.Contains("## Release Workflow", stableSurface, StringComparison.Ordinal);
        Assert.Contains("## Non-Stable Surfaces", stableSurface, StringComparison.Ordinal);
        Assert.Contains("## Migration Guidance", stableSurface, StringComparison.Ordinal);
        Assert.Contains("## Accepted Compatibility Risks", stableSurface, StringComparison.Ordinal);
        Assert.Contains("AvaScope.Protocol", stableSurface, StringComparison.Ordinal);
        Assert.Contains("AvaScope.Core", stableSurface, StringComparison.Ordinal);
        Assert.Contains("AvaScope.Bridge", stableSurface, StringComparison.Ordinal);
        Assert.Contains("ToolResult<T>", stableSurface, StringComparison.Ordinal);
        Assert.Contains("ProtocolError", stableSurface, StringComparison.Ordinal);
        Assert.Contains("SessionId", stableSurface, StringComparison.Ordinal);
        Assert.Contains("capability_not_supported", stableSurface, StringComparison.Ordinal);
        Assert.Contains("Release <Version>", stableSurface, StringComparison.Ordinal);
        Assert.Contains("v<Version>", stableSurface, StringComparison.Ordinal);
        Assert.Contains("release-manifest.json", stableSurface, StringComparison.Ordinal);
        Assert.Contains("baseline-report.json", stableSurface, StringComparison.Ordinal);
        Assert.Contains("baseline-report.html", stableSurface, StringComparison.Ordinal);
        Assert.Contains("baseline-junit.xml", stableSurface, StringComparison.Ordinal);
        Assert.Contains("baseline.sarif.json", stableSurface, StringComparison.Ordinal);
        Assert.Contains("-before.png", stableSurface, StringComparison.Ordinal);
        Assert.Contains("-after.png", stableSurface, StringComparison.Ordinal);
        Assert.Contains("-review.html", stableSurface, StringComparison.Ordinal);

        foreach (var tool in AvaScopeCapabilityCatalog.Current().Tools)
        {
            Assert.Contains($"`{tool.Name}`", stableSurface, StringComparison.Ordinal);
        }

        Assert.DoesNotContain("TODO", stableSurface, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("TBD", stableSurface, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void StableSurfaceDocumentationIsLinkedFromPrimaryDocs()
    {
        var root = FindRepositoryRoot();
        var readme = File.ReadAllText(Path.Combine(root, "README.md"));
        var userGuide = File.ReadAllText(Path.Combine(root, "docs", "USER_GUIDE.md"));
        var releasePlan = File.ReadAllText(Path.Combine(root, "docs", "RELEASE_PLAN.md"));

        Assert.Contains("docs/STABLE_SURFACE.md", readme, StringComparison.Ordinal);
        Assert.Contains("STABLE_SURFACE.md", userGuide, StringComparison.Ordinal);
        Assert.Contains("STABLE_SURFACE.md", releasePlan, StringComparison.Ordinal);
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
