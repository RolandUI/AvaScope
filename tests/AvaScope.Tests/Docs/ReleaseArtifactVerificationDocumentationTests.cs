namespace AvaScope.Tests.Docs;

public sealed class ReleaseArtifactVerificationDocumentationTests
{
    [Fact]
    public void ReleaseArtifactVerificationDocumentsV1ArtifactGate()
    {
        var root = FindRepositoryRoot();
        var documentation = File.ReadAllText(Path.Combine(root, "docs", "RELEASE_ARTIFACT_VERIFICATION.md"));

        Assert.Contains("# AvaScope v1.0.0 Release Artifact Verification", documentation, StringComparison.Ordinal);
        Assert.Contains("R1.0.0-M4 Release Artifact And Package Verification", documentation, StringComparison.Ordinal);
        Assert.Contains("create-local-release.ps1", documentation, StringComparison.Ordinal);
        Assert.Contains("publish-nuget.ps1 -DryRun", documentation, StringComparison.Ordinal);
        Assert.Contains("publish-github-release.ps1 -Tag v1.0.0 -DryRun", documentation, StringComparison.Ordinal);
        Assert.Contains("AvaScope.Protocol.1.0.0.nupkg", documentation, StringComparison.Ordinal);
        Assert.Contains("avascope-win-x64-framework-dependent.zip", documentation, StringComparison.Ordinal);
        Assert.Contains("avascope-linux-x64-framework-dependent.zip", documentation, StringComparison.Ordinal);
        Assert.Contains("release-manifest.json", documentation, StringComparison.Ordinal);
        Assert.Contains("serverInfo.name", documentation, StringComparison.Ordinal);
        Assert.Contains("avascope-win-x64-self-contained.zip", documentation, StringComparison.Ordinal);
        Assert.Contains("normal development CI was intentionally manual-only", documentation, StringComparison.Ordinal);
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
