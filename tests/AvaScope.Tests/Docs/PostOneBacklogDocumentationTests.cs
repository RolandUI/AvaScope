namespace AvaScope.Tests.Docs;

public sealed class PostOneBacklogDocumentationTests
{
    [Fact]
    public void PostOneBacklogDocumentsNonBlockingDeferrals()
    {
        var root = FindRepositoryRoot();
        var documentation = File.ReadAllText(Path.Combine(root, "docs", "POST_1_0_BACKLOG.md"));

        Assert.Contains("# AvaScope Post-1.0 Backlog And Deferral Audit", documentation, StringComparison.Ordinal);
        Assert.Contains("Open `priority:p1` issues: none.", documentation, StringComparison.Ordinal);
        Assert.Contains("#33 `Release v1.0.0`", documentation, StringComparison.Ordinal);
        Assert.Contains("#39 `R1.0.0-M6 Stable Release Commit And Publication`", documentation, StringComparison.Ordinal);
        Assert.Contains("Remote inspection/control", documentation, StringComparison.Ordinal);
        Assert.Contains("No-code attach", documentation, StringComparison.Ordinal);
        Assert.Contains("Process injection and CLR profiling", documentation, StringComparison.Ordinal);
        Assert.Contains("Private runtime hooks and private designer APIs", documentation, StringComparison.Ordinal);
        Assert.Contains("Cloud dashboards", documentation, StringComparison.Ordinal);
        Assert.Contains("Native IDE extensions", documentation, StringComparison.Ordinal);
        Assert.Contains("Destructive runtime actions", documentation, StringComparison.Ordinal);
        Assert.Contains("Automatic source editing", documentation, StringComparison.Ordinal);
        Assert.Contains("Release-blocking", documentation, StringComparison.Ordinal);
        Assert.Contains("No |", documentation, StringComparison.Ordinal);
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
