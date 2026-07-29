using System.Text.RegularExpressions;
using System.Xml.Linq;
using AvaScope.Protocol;

namespace AvaScope.Tests.Core;

public sealed class StableSurfaceContractTests
{
    private static readonly string[] FrozenCliTools =
    [
        "capabilities",
        "mcp",
        "doctor",
        "diagnostics",
        "attach",
        "launch-app",
        "list-top-levels",
        "visual-tree",
        "logical-tree",
        "inspect-node",
        "explain-layout",
        "find-nodes",
        "audit-ui",
        "design-audit",
        "input",
        "run-workflow",
        "run-scenario",
        "native-picker",
        "pointer-diagnostics",
        "pseudo-state-matrix",
        "record-interaction-animation",
        "mutate-node",
        "mutate-node-evidence",
        "mutation-review",
        "close-session",
        "screenshot",
        "preview",
        "preview-animation",
        "create-preview-session",
        "list-preview-sessions",
        "reload-preview-session",
        "reload",
        "close-preview-session",
        "watch-preview-session",
        "preview-viewer",
        "baseline-create",
        "baseline-check",
        "latest-run",
        "diff",
        "semantic-diff",
        "assert-region",
        "cleanup",
        "cleanup-bridge-sessions"
    ];

    private static readonly string[] FrozenMcpTools =
    [
        "capabilities",
        "health",
        "diagnostics",
        "attach_to_app",
        "launch_app",
        "list_top_levels",
        "visual_tree",
        "logical_tree",
        "inspect_node",
        "explain_layout",
        "find_nodes",
        "audit_ui",
        "design_quality_audit",
        "input",
        "run_workflow",
        "run_scenario",
        "native_picker",
        "pointer_diagnostics",
        "pseudo_state_matrix",
        "record_interaction_animation",
        "mutate_node",
        "mutate_node_evidence",
        "mutation_review",
        "close_session",
        "screenshot",
        "preview_axaml",
        "preview_axaml_multi",
        "preview_axaml_animation",
        "create_preview_session",
        "list_preview_sessions",
        "reload",
        "close_preview_session",
        "preview_viewer",
        "baseline_check",
        "semantic_diff",
        "assert_region",
        "cleanup",
        "cleanup_bridge_sessions",
        "list_sessions"
    ];

    [Fact]
    public void CapabilityCatalogDeclaresFrozenCliAndMcpSurface()
    {
        var tools = AvaScopeCapabilityCatalog.Current(new DateTimeOffset(2026, 6, 13, 4, 30, 0, TimeSpan.Zero)).Tools;

        Assert.Equal(FrozenCliTools, tools.Where(static tool => tool.Adapter == "cli").Select(static tool => tool.Name));
        Assert.Equal(FrozenMcpTools, tools.Where(static tool => tool.Adapter == "mcp").Select(static tool => tool.Name));
        Assert.Equal(tools.Count, tools.Select(static tool => $"{tool.Adapter}:{tool.Name}").Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void CapabilityCatalogMatchesImplementedCliAndMcpToolNames()
    {
        var root = FindRepositoryRoot();
        var catalog = AvaScopeCapabilityCatalog.Current(new DateTimeOffset(2026, 6, 13, 4, 30, 0, TimeSpan.Zero)).Tools;
        var catalogCli = catalog
            .Where(static tool => tool.Adapter == "cli")
            .Select(static tool => tool.Name)
            .OrderBy(static name => name, StringComparer.Ordinal)
            .ToArray();
        var catalogMcp = catalog
            .Where(static tool => tool.Adapter == "mcp")
            .Select(static tool => tool.Name)
            .OrderBy(static name => name, StringComparer.Ordinal)
            .ToArray();

        var cliSwitchNames = ReadCliCommandSwitchNames(Path.Combine(root, "src", "AvaScope.Cli", "Program.cs"))
            .OrderBy(static name => name, StringComparer.Ordinal)
            .ToArray();
        var mcpAttributeNames = ReadRegexGroupValues(
                Path.Combine(root, "src", "AvaScope.Mcp", "AvaScopeMcpTools.cs"),
                "Name\\s*=\\s*\"(?<name>[a-z0-9_]+)\"")
            .OrderBy(static name => name, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(cliSwitchNames, catalogCli);
        Assert.Equal(mcpAttributeNames, catalogMcp);
    }

    [Fact]
    public void PackageMetadataAndReleaseArtifactNamesAreFrozen()
    {
        var root = FindRepositoryRoot();

        AssertProjectPackage(root, "src/AvaScope.Protocol/AvaScope.Protocol.csproj", "true", "AvaScope.Protocol");
        AssertProjectPackage(root, "src/AvaScope.Core/AvaScope.Core.csproj", "true", "AvaScope.Core");
        AssertProjectPackage(root, "src/AvaScope.Bridge/AvaScope.Bridge.csproj", "true", "AvaScope.Bridge");
        AssertProjectPackage(root, "src/AvaScope.Cli/AvaScope.Cli.csproj", "false", null);
        AssertProjectPackage(root, "src/AvaScope.Installer/AvaScope.Installer.csproj", "false", null);
        AssertProjectPackage(root, "src/AvaScope.Mcp/AvaScope.Mcp.csproj", "false", null);
        AssertProjectPackage(root, "src/AvaScope.PreviewHost/AvaScope.PreviewHost.csproj", "false", null);

        var buildProps = XDocument.Load(Path.Combine(root, "Directory.Build.props"));
        Assert.Equal("net10.0", buildProps.Descendants("TargetFramework").Single().Value);
        Assert.Equal("Apache-2.0", buildProps.Descendants("PackageLicenseExpression").Single().Value);
        Assert.Equal("https://github.com/RolandUI/AvaScope", buildProps.Descendants("PackageProjectUrl").Single().Value);
        Assert.Equal("https://github.com/RolandUI/AvaScope.git", buildProps.Descendants("RepositoryUrl").Single().Value);

        foreach (var legalFileName in new[] { "LICENSE", "NOTICE", "LICENSE-SCOPE.md", "THIRD-PARTY-NOTICES.md" })
        {
            Assert.True(File.Exists(Path.Combine(root, legalFileName)), $"Missing required legal file: {legalFileName}");
        }

        var licenseScope = File.ReadAllText(Path.Combine(root, "LICENSE-SCOPE.md"));
        Assert.Contains("versions 0.1.0 and later", licenseScope, StringComparison.Ordinal);

        var verifyArtifacts = File.ReadAllText(Path.Combine(root, "eng", "verify-artifacts.ps1"));
        Assert.Contains("AvaScope.Protocol.$version.nupkg", verifyArtifacts, StringComparison.Ordinal);
        Assert.Contains("AvaScope.Core.$version.nupkg", verifyArtifacts, StringComparison.Ordinal);
        Assert.Contains("AvaScope.Bridge.$version.nupkg", verifyArtifacts, StringComparison.Ordinal);
        Assert.Contains("avascope-$runtimeIdentifier-$ExecutablePackageKind.zip", verifyArtifacts, StringComparison.Ordinal);
        Assert.Contains("AvaScopeSetup.exe", verifyArtifacts, StringComparison.Ordinal);
        Assert.Contains("avascope-$runtimeIdentifier-installer", verifyArtifacts, StringComparison.Ordinal);
        Assert.Contains("artifacts/release-manifest.json", verifyArtifacts, StringComparison.Ordinal);

        var publishGitHubRelease = File.ReadAllText(Path.Combine(root, "eng", "publish-github-release.ps1"));
        Assert.Contains("$expectedTag = \"v$version\"", publishGitHubRelease, StringComparison.Ordinal);
        Assert.Contains("AvaScope.Protocol.$version.nupkg", publishGitHubRelease, StringComparison.Ordinal);
        Assert.Contains("AvaScopeSetup.exe", publishGitHubRelease, StringComparison.Ordinal);
        Assert.Contains("avascope-$runtimeIdentifier-installer", publishGitHubRelease, StringComparison.Ordinal);
        Assert.Contains("release-manifest.json", publishGitHubRelease, StringComparison.Ordinal);

        var releaseCommitGuard = File.ReadAllText(Path.Combine(root, "eng", "validate-release-commit.ps1"));
        Assert.Contains("$expectedSubject = \"Release $Version\"", releaseCommitGuard, StringComparison.Ordinal);
        Assert.Contains("Release Candidate", releaseCommitGuard, StringComparison.Ordinal);

        var installer = File.ReadAllText(Path.Combine(root, "eng", "install-avascope.ps1"));
        Assert.Contains("avascope.discovery.json", installer, StringComparison.Ordinal);
        Assert.Contains("avascope.cmd", installer, StringComparison.Ordinal);
        Assert.Contains("AvaScope.Mcp.dll", installer, StringComparison.Ordinal);

        var packageExecutables = File.ReadAllText(Path.Combine(root, "eng", "package-executables.ps1"));
        var packageInstallers = File.ReadAllText(Path.Combine(root, "eng", "package-installers.ps1"));
        var windowsInstaller = File.ReadAllText(Path.Combine(root, "eng", "installer", "AvaScope.iss"));
        var verifyArtifactsScript = File.ReadAllText(Path.Combine(root, "eng", "verify-artifacts.ps1"));
        var readme = File.ReadAllText(Path.Combine(root, "README.md"));
        Assert.Contains("THIRD-PARTY-NOTICES.md", packageExecutables, StringComparison.Ordinal);
        Assert.Contains("AvaScopeInstallerPayload", packageInstallers, StringComparison.Ordinal);
        Assert.Contains("AvaScope.iss", packageInstallers, StringComparison.Ordinal);
        Assert.Contains("Resolve-InnoSetupCompiler", packageInstallers, StringComparison.Ordinal);
        Assert.Contains("WindowsSignToolPath", packageInstallers, StringComparison.Ordinal);
        Assert.Contains("WizardStyle=modern dynamic", windowsInstaller, StringComparison.Ordinal);
        Assert.Contains(@"SetupIconFile={#RepoRoot}\assets\brand\avascope.ico", windowsInstaller, StringComparison.Ordinal);
        Assert.Contains(@"WizardSmallImageFile={#RepoRoot}\assets\brand\avascope-icon.png", windowsInstaller, StringComparison.Ordinal);
        Assert.Contains(@"WizardSmallImageFileDynamicDark={#RepoRoot}\assets\brand\avascope-icon-dark.png", windowsInstaller, StringComparison.Ordinal);
        Assert.Contains("PrivilegesRequired=lowest", windowsInstaller, StringComparison.Ordinal);
        Assert.Contains(@"UninstallDisplayIcon={app}\AvaScope.ico", windowsInstaller, StringComparison.Ordinal);
        Assert.Contains("avascope.discovery.json", windowsInstaller, StringComparison.Ordinal);
        Assert.Contains("verify-avascope.cmd", windowsInstaller, StringComparison.Ordinal);
        Assert.Contains("Verify the AvaScope installation", windowsInstaller, StringComparison.Ordinal);
        Assert.Contains("postinstall skipifsilent nowait", windowsInstaller, StringComparison.Ordinal);
        Assert.DoesNotContain("ExecAndCaptureOutput(", windowsInstaller, StringComparison.Ordinal);
        Assert.Contains("assets/brand/avascope-logo.png", readme, StringComparison.Ordinal);
        Assert.Contains("assets/brand/avascope-logo-dark.png", readme, StringComparison.Ordinal);
        foreach (var brandFileName in new[]
                 {
                     "avascope-logo.png",
                     "avascope-logo-dark.png",
                     "avascope-icon.png",
                     "avascope-icon-dark.png",
                     "avascope.ico",
                     "brand-assets.json"
                 })
        {
            Assert.True(
                File.Exists(Path.Combine(root, "assets", "brand", brandFileName)),
                $"Missing required brand asset: {brandFileName}");
        }
        Assert.Contains("Assert-NuGetPackageMetadata", verifyArtifactsScript, StringComparison.Ordinal);
        Assert.Contains("Assert-ExecutableLegalFiles", verifyArtifactsScript, StringComparison.Ordinal);
    }

    [Fact]
    public void WorkflowTriggersKeepPullRequestValidationReadOnlyAndReleaseVersionScoped()
    {
        var root = FindRepositoryRoot();
        var ciWorkflow = NormalizeLineEndings(File.ReadAllText(Path.Combine(root, ".github", "workflows", "ci.yml")));
        var releaseWorkflow = NormalizeLineEndings(File.ReadAllText(Path.Combine(root, ".github", "workflows", "publish-nuget.yml")));

        Assert.Contains("\n  workflow_dispatch:", ciWorkflow, StringComparison.Ordinal);
        Assert.Contains("\n  pull_request:", ciWorkflow, StringComparison.Ordinal);
        Assert.Contains("\n    branches:\n      - master", ciWorkflow, StringComparison.Ordinal);
        Assert.DoesNotContain("\n  push:", ciWorkflow, StringComparison.Ordinal);
        Assert.DoesNotContain("pull_request_target", ciWorkflow, StringComparison.Ordinal);
        Assert.Contains("\npermissions:\n  contents: read", ciWorkflow, StringComparison.Ordinal);
        Assert.Contains(
            "AVASCOPE_INSTALLER_ARTIFACT: ${{ github.workspace }}/artifacts/executables/AvaScopeSetup.exe",
            ciWorkflow,
            StringComparison.Ordinal);
        Assert.Contains(
            "AVASCOPE_INSTALLER_ARTIFACT: ${{ github.workspace }}/artifacts/executables/avascope-linux-x64-installer",
            ciWorkflow,
            StringComparison.Ordinal);

        Assert.Contains("\n  push:", releaseWorkflow, StringComparison.Ordinal);
        Assert.Contains("\n    paths:", releaseWorkflow, StringComparison.Ordinal);
        Assert.Contains("\n      - Directory.Build.props", releaseWorkflow, StringComparison.Ordinal);
        Assert.Contains("\n  workflow_dispatch:", releaseWorkflow, StringComparison.Ordinal);
        Assert.DoesNotContain("\n  pull_request:", releaseWorkflow, StringComparison.Ordinal);
    }

    private static IReadOnlyList<string> ReadRegexGroupValues(string path, string pattern)
    {
        var source = File.ReadAllText(path);
        return Regex.Matches(source, pattern)
            .Select(static match => match.Groups["name"].Value)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    private static string NormalizeLineEndings(string source)
    {
        return source.Replace("\r\n", "\n", StringComparison.Ordinal);
    }

    private static IReadOnlyList<string> ReadCliCommandSwitchNames(string path)
    {
        var source = File.ReadAllText(path);
        var switchStart = source.IndexOf("return args[0] switch", StringComparison.Ordinal);
        var switchEnd = source.IndexOf("_ => UnknownCommand(args[0])", switchStart, StringComparison.Ordinal);
        Assert.True(switchStart >= 0, "Could not find the CLI command switch.");
        Assert.True(switchEnd > switchStart, "Could not find the end of the CLI command switch.");

        var commandSwitch = source[switchStart..switchEnd];
        return Regex.Matches(commandSwitch, "\"(?<name>[a-z0-9-]+)\"\\s*=>")
            .Select(static match => match.Groups["name"].Value)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    private static void AssertProjectPackage(string root, string projectPath, string isPackable, string? packageId)
    {
        var project = XDocument.Load(Path.Combine(root, projectPath));
        Assert.Equal(isPackable, project.Descendants("IsPackable").Single().Value);
        if (packageId is not null)
        {
            Assert.Equal(packageId, project.Descendants("PackageId").Single().Value);
        }
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
