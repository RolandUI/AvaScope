using System.Text.Json;
using AvaScope.Core;
using AvaScope.Mcp;

namespace AvaScope.Tests.Core;

public sealed class BridgeIntegrationAdvisorTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "AvaScope.Tests", $"integration-guide-{Guid.NewGuid():N}");
    private string ProjectPath => Path.Combine(_root, "Host.csproj");
    private const string Startup = """
        using Avalonia;
        using Avalonia.Controls.ApplicationLifetimes;
        class App : Application
        {
            public override void OnFrameworkInitializationCompleted()
            {
                if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop) { }
                base.OnFrameworkInitializationCompleted();
            }
        }
        """;

    public BridgeIntegrationAdvisorTests()
    {
        Directory.CreateDirectory(_root);
        File.WriteAllText(ProjectPath, """
            <Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><TargetFramework>net10.0</TargetFramework><AvaloniaVersion>12.1.0</AvaloniaVersion></PropertyGroup>
            <ItemGroup><PackageReference Include="Avalonia.Desktop" Version="$(AvaloniaVersion)" /></ItemGroup></Project>
            """);
        File.WriteAllText(Path.Combine(_root, "App.cs"), Startup);
    }

    [Fact]
    public void GuidanceIdentifiesExactLocationsForBothModesWithoutEditingOrExecutingProject()
    {
        var before = Directory.GetFiles(_root).ToDictionary(path => path, File.ReadAllText);
        var result = BridgeIntegrationAdvisor.Analyze(ProjectPath);
        Assert.True(result.Success, result.Error?.Message);
        var guide = result.Value!;
        Assert.Equal("guidance_available", guide.Status);
        Assert.Equal("12.1.0", guide.AvaloniaVersion);
        Assert.Equal(4, guide.Guidance.Count);
        Assert.All(guide.Guidance, item => Assert.Equal(64, item.Location.Sha256.Length));
        Assert.All(guide.Guidance.Where(item => item.Location.Path.EndsWith("App.cs", StringComparison.Ordinal)), item =>
        {
            Assert.Equal(8, item.Location.Line);
            Assert.StartsWith("#if ENABLE_UI_INSPECTION", item.Snippet);
        });
        Assert.Contains("Compile Remove", guide.Guidance.Single(item => item.Mode == "standalone" && item.Location.Path == ProjectPath).Snippet);
        foreach (var (path, content) in before) Assert.Equal(content, File.ReadAllText(path));
        Assert.Equal(before.Count, Directory.GetFiles(_root).Length);
        Assert.Equal(JsonSerializer.Serialize(guide), JsonSerializer.Serialize(AvaScopeMcpTools.IntegrationGuide(ProjectPath).Value));
    }

    [Theory]
    [InlineData("global::AvaScope.Bridge.Bootstrap.Start();")]
    [InlineData("var bridge = AvaScopeBridge.Activate();")]
    [InlineData("OptionalDiagnostics.OptionalProviderLoader.TryStartFromEnvironment();")]
    public void ExistingIntegrationReturnsNoDuplicateProposals(string call)
    {
        File.WriteAllText(Path.Combine(_root, "App.cs"), Startup.Replace("base.OnFramework", call + "\nbase.OnFramework", StringComparison.Ordinal));
        var guide = BridgeIntegrationAdvisor.Analyze(ProjectPath).Value!;
        Assert.Equal("already_integrated", guide.Status);
        Assert.Single(guide.ExistingIntegration);
        Assert.Empty(guide.Guidance);
    }

    [Fact]
    public void CommentedIntegrationDoesNotSuppressRealGuidanceAndExistingPackageIsNotDuplicated()
    {
        File.AppendAllText(Path.Combine(_root, "App.cs"), "\n// Bootstrap.Start();\n/* AvaScopeBridge.Activate(); */");
        File.WriteAllText(ProjectPath, File.ReadAllText(ProjectPath).Replace("</ItemGroup>", "<PackageReference Include=\"AvaScope.Bridge\" Version=\"1.5.0\" /></ItemGroup>", StringComparison.Ordinal));
        var guide = BridgeIntegrationAdvisor.Analyze(ProjectPath).Value!;
        Assert.Equal("guidance_available", guide.Status);
        Assert.DoesNotContain(guide.Guidance, item => item.Snippet.Contains("PackageReference", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("undeclared_framework")]
    [InlineData("unsupported_framework")]
    [InlineData("multiple_frameworks")]
    [InlineData("conditional_framework")]
    [InlineData("old_avalonia")]
    [InlineData("new_avalonia_line")]
    [InlineData("unresolved_avalonia")]
    [InlineData("aot")]
    [InlineData("trimmed")]
    public void ExistingActivationRetainsRequiredCompatibilityReview(string condition)
    {
        var project = File.ReadAllText(ProjectPath);
        string? framework = null;
        var expectedDiagnostic = "declared .NET 10";
        switch (condition)
        {
            case "undeclared_framework": framework = "net9.0"; break;
            case "unsupported_framework": project = project.Replace("net10.0", "net9.0", StringComparison.Ordinal); break;
            case "multiple_frameworks": project = project.Replace("<TargetFramework>net10.0</TargetFramework>", "<TargetFrameworks>net10.0;net10.0-windows</TargetFrameworks>", StringComparison.Ordinal); break;
            case "conditional_framework": project = project.Replace("<TargetFramework>", "<TargetFramework Condition=\"'$(Configuration)' == 'Diagnostics'\">", StringComparison.Ordinal); break;
            case "old_avalonia": project = project.Replace("12.1.0", "11.3.12", StringComparison.Ordinal); expectedDiagnostic = "requires Avalonia"; break;
            case "new_avalonia_line": project = project.Replace("12.1.0", "12.2.0", StringComparison.Ordinal); expectedDiagnostic = "requires Avalonia"; break;
            case "unresolved_avalonia": project = project.Replace("12.1.0", "$(UnknownVersion)", StringComparison.Ordinal); expectedDiagnostic = "requires Avalonia"; break;
            case "aot": project = project.Replace("</PropertyGroup>", "<PublishAot>true</PublishAot></PropertyGroup>", StringComparison.Ordinal); expectedDiagnostic = "NativeAOT/trimmed"; break;
            case "trimmed": project = project.Replace("</PropertyGroup>", "<PublishTrimmed>true</PublishTrimmed></PropertyGroup>", StringComparison.Ordinal); expectedDiagnostic = "NativeAOT/trimmed"; break;
        }
        File.WriteAllText(ProjectPath, project);
        File.WriteAllText(Path.Combine(_root, "App.cs"), Startup.Replace("base.OnFramework", "global::AvaScope.Bridge.Bootstrap.Start();\nbase.OnFramework", StringComparison.Ordinal));
        var before = Directory.GetFiles(_root).ToDictionary(path => path, File.ReadAllText);
        var result = BridgeIntegrationAdvisor.Analyze(ProjectPath, framework);
        Assert.True(result.Success, result.Error?.Message);
        var guide = result.Value!;
        Assert.Equal("needs_review", guide.Status);
        Assert.Single(guide.ExistingIntegration);
        Assert.Empty(guide.Guidance);
        Assert.True(guide.ReadOnly);
        Assert.Contains(guide.Diagnostics, diagnostic => diagnostic.Contains(expectedDiagnostic, StringComparison.Ordinal));
        Assert.Equal(JsonSerializer.Serialize(guide), JsonSerializer.Serialize(AvaScopeMcpTools.IntegrationGuide(ProjectPath, framework).Value));
        foreach (var (path, content) in before) Assert.Equal(content, File.ReadAllText(path));
        Assert.Equal(before.Count, Directory.GetFiles(_root).Length);
    }

    [Fact]
    public void UnknownStartupAndMultipleFrameworksRequireReview()
    {
        File.WriteAllText(Path.Combine(_root, "App.cs"), "class UnfamiliarStartup { }");
        Assert.Equal("needs_review", BridgeIntegrationAdvisor.Analyze(ProjectPath).Value!.Status);
        File.WriteAllText(Path.Combine(_root, "App.cs"), Startup);
        File.WriteAllText(ProjectPath, File.ReadAllText(ProjectPath).Replace("<TargetFramework>net10.0</TargetFramework>", "<TargetFrameworks>net10.0;net10.0-windows</TargetFrameworks>", StringComparison.Ordinal));
        Assert.Empty(BridgeIntegrationAdvisor.Analyze(ProjectPath).Value!.Guidance);
        Assert.Equal("guidance_available", BridgeIntegrationAdvisor.Analyze(ProjectPath, "net10.0").Value!.Status);
        Assert.Equal("needs_review", BridgeIntegrationAdvisor.Analyze(ProjectPath, "net9.0").Value!.Status);
    }

    [Fact]
    public void CentralVersionsAndInheritedFrameworkAreReadWithoutImportEvaluation()
    {
        File.WriteAllText(Path.Combine(_root, "Directory.Build.props"), "<Project><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>");
        File.WriteAllText(Path.Combine(_root, "Directory.Packages.props"), "<Project><ItemGroup><PackageVersion Include=\"Avalonia.Desktop\" Version=\"12.1.0\" /></ItemGroup></Project>");
        File.WriteAllText(ProjectPath, "<Project Sdk=\"Microsoft.NET.Sdk\"><ItemGroup><PackageReference Include=\"Avalonia.Desktop\" /></ItemGroup></Project>");
        Assert.Equal("guidance_available", BridgeIntegrationAdvisor.Analyze(ProjectPath).Value!.Status);
        File.WriteAllText(Path.Combine(_root, "Directory.Build.props"), "<Project><PropertyGroup><TargetFramework>net9.0</TargetFramework></PropertyGroup></Project>");
        Assert.Equal("needs_review", BridgeIntegrationAdvisor.Analyze(ProjectPath).Value!.Status);
    }

    [Fact]
    public void InvalidOrOversizedInputsFailSafely()
    {
        Assert.False(BridgeIntegrationAdvisor.Analyze("Host.csproj").Success);
        File.WriteAllText(ProjectPath, "<!DOCTYPE Project [<!ENTITY external SYSTEM 'file:///missing'>]><Project>&external;</Project>");
        Assert.False(BridgeIntegrationAdvisor.Analyze(ProjectPath).Success);
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);
}
