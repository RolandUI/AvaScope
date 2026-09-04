using System.Text.Json;
using System.Text.RegularExpressions;

namespace AvaScope.Tests.Docs;

public sealed partial class ShowcaseSiteTests
{
    [Fact]
    public void ShowcaseContainsAccessibleFeatureNarrativeAndRepositoryLinks()
    {
        var root = FindRepositoryRoot();
        var website = Path.Combine(root, "website");
        var html = File.ReadAllText(Path.Combine(website, "index.html"));
        var css = File.ReadAllText(Path.Combine(website, "styles.css"));
        var script = File.ReadAllText(Path.Combine(website, "script.js"));

        Assert.Contains("<main id=\"main\">", html, StringComparison.Ordinal);
        Assert.Contains("class=\"skip-link\"", html, StringComparison.Ordinal);
        Assert.Contains("aria-label=\"Primary navigation\"", html, StringComparison.Ordinal);
        Assert.Contains("prefers-reduced-motion", css, StringComparison.Ordinal);
        Assert.Contains("@media (max-width: 680px)", css, StringComparison.Ordinal);
        Assert.Contains("fetch(\"release.json\"", script, StringComparison.Ordinal);

        foreach (var capability in new[]
                 {
                     "Design-time preview",
                     "Runtime intelligence",
                     "Interaction",
                     "Reversible mutation",
                     "Diagnostics and evidence",
                     "Workflow orchestration",
                     "Safety boundary"
                 })
        {
            Assert.Contains(capability, html, StringComparison.Ordinal);
        }

        Assert.Contains("https://github.com/RolandUI/AvaScope", html, StringComparison.Ordinal);
        Assert.DoesNotContain("TODO", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("TBD", html, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ShowcaseLocalAssetsAndReleaseMetadataAreComplete()
    {
        var root = FindRepositoryRoot();
        var website = Path.Combine(root, "website");
        var html = File.ReadAllText(Path.Combine(website, "index.html"));
        var localReferences = LocalAssetRegex()
            .Matches(html)
            .Select(match => match.Groups[1].Value)
            .Distinct(StringComparer.Ordinal);

        foreach (var reference in localReferences)
        {
            var relativePath = reference.Split('?', '#')[0].Replace('/', Path.DirectorySeparatorChar);
            Assert.True(File.Exists(Path.Combine(website, relativePath)), $"Missing showcase asset: {reference}");
        }

        using var release = JsonDocument.Parse(File.ReadAllText(Path.Combine(website, "release.json")));
        Assert.StartsWith("v", release.RootElement.GetProperty("tagName").GetString(), StringComparison.Ordinal);
        Assert.StartsWith("https://github.com/RolandUI/AvaScope/releases/tag/", release.RootElement.GetProperty("releaseUrl").GetString(), StringComparison.Ordinal);
        Assert.True(release.RootElement.GetProperty("publishedAt").GetDateTimeOffset() <= DateTimeOffset.UtcNow);
    }

    [Fact]
    public void ShowcaseDeploymentRunsOnlyForPublishedReleasesOrManualRecovery()
    {
        var root = FindRepositoryRoot();
        var workflow = File.ReadAllText(Path.Combine(root, ".github", "workflows", "pages.yml"))
            .Replace("\r\n", "\n", StringComparison.Ordinal);

        Assert.Contains("\n  release:\n    types:\n      - published", workflow, StringComparison.Ordinal);
        Assert.Contains("\n  workflow_dispatch:", workflow, StringComparison.Ordinal);
        Assert.DoesNotContain("\n  push:", workflow, StringComparison.Ordinal);
        Assert.DoesNotContain("\n  pull_request:", workflow, StringComparison.Ordinal);
        Assert.Contains("pages: write", workflow, StringComparison.Ordinal);
        Assert.Contains("id-token: write", workflow, StringComparison.Ordinal);
        Assert.Contains("actions/upload-pages-artifact@v4", workflow, StringComparison.Ordinal);
        Assert.Contains("actions/deploy-pages@v4", workflow, StringComparison.Ordinal);
        Assert.Contains("website/.", workflow, StringComparison.Ordinal);
        Assert.Contains("_site/release.json", workflow, StringComparison.Ordinal);
    }

    [GeneratedRegex("(?:src|href)=\"((?:assets/|styles\\.css|script\\.js)[^\"]*)\"", RegexOptions.CultureInvariant)]
    private static partial Regex LocalAssetRegex();

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
