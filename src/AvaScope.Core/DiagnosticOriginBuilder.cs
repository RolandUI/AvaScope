using AvaScope.Protocol;

namespace AvaScope.Core;

public static class DiagnosticOriginBuilder
{
    public static DiagnosticComponentOrigin Create(
        string component,
        string assemblyPath,
        string? baseDirectory = null)
    {
        var fullAssemblyPath = Path.GetFullPath(assemblyPath);
        var fullBaseDirectory = Path.GetFullPath(
            string.IsNullOrWhiteSpace(baseDirectory)
                ? Path.GetDirectoryName(fullAssemblyPath) ?? AppContext.BaseDirectory
                : baseDirectory);
        var (rootDirectory, originKind) = ResolveRoot(fullAssemblyPath, fullBaseDirectory);

        return new DiagnosticComponentOrigin(
            component,
            fullAssemblyPath,
            fullBaseDirectory,
            rootDirectory,
            originKind,
            File.Exists(fullAssemblyPath));
    }

    private static (string RootDirectory, string OriginKind) ResolveRoot(string assemblyPath, string baseDirectory)
    {
        var startDirectory = Directory.Exists(baseDirectory)
            ? new DirectoryInfo(baseDirectory)
            : new DirectoryInfo(Path.GetDirectoryName(assemblyPath) ?? AppContext.BaseDirectory);

        var packageRoot = FindPackageArtifactRoot(startDirectory);
        if (packageRoot is not null)
        {
            return (packageRoot.FullName, "package_artifact");
        }

        var repositoryRoot = FindRepositoryRoot(startDirectory);
        if (repositoryRoot is not null)
        {
            return (repositoryRoot.FullName, "repository");
        }

        if (string.Equals(startDirectory.Name, "current", StringComparison.OrdinalIgnoreCase)
            && startDirectory.Parent is { Name: "AvaScope" } installRoot)
        {
            return (installRoot.FullName, "per_user_install");
        }

        return (startDirectory.FullName, "directory");
    }

    private static DirectoryInfo? FindPackageArtifactRoot(DirectoryInfo startDirectory)
    {
        for (var current = startDirectory; current is not null; current = current.Parent)
        {
            if (string.Equals(current.Parent?.Name, "executables", StringComparison.OrdinalIgnoreCase)
                && string.Equals(current.Parent?.Parent?.Name, "artifacts", StringComparison.OrdinalIgnoreCase))
            {
                return current;
            }
        }

        return null;
    }

    private static DirectoryInfo? FindRepositoryRoot(DirectoryInfo startDirectory)
    {
        for (var current = startDirectory; current is not null; current = current.Parent)
        {
            if (File.Exists(Path.Combine(current.FullName, "AvaScope.slnx")))
            {
                return current;
            }
        }

        return null;
    }
}
