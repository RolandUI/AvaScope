using System.Diagnostics;
using System.Globalization;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using AvaScope.Core;
using AvaScope.Protocol;
using SkiaSharp;

namespace AvaScope.Tests.Cli;

public sealed class CliSmokeTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly AsyncLocal<string?> CurrentBridgeManifestDirectory = new();

    [Fact]
    public async Task VersionCommandReportsProductVersion()
    {
        var cliAssembly = Path.Combine(AppContext.BaseDirectory, "avascope.dll");
        Assert.True(File.Exists(cliAssembly), $"Expected CLI assembly at {cliAssembly}.");

        var longResult = await RunCliAsync(cliAssembly, "--version");
        var shortResult = await RunCliAsync(cliAssembly, "-v");

        Assert.Equal(0, longResult.ExitCode);
        Assert.Equal(AvaScopeProduct.Version, longResult.StandardOutput.Trim());
        Assert.True(string.IsNullOrWhiteSpace(longResult.StandardError), longResult.StandardError);
        Assert.Equal(0, shortResult.ExitCode);
        Assert.Equal(AvaScopeProduct.Version, shortResult.StandardOutput.Trim());
        Assert.True(string.IsNullOrWhiteSpace(shortResult.StandardError), shortResult.StandardError);
    }

    [Fact]
    public async Task GeneralUsageIncludesSemanticGestureOptions()
    {
        var cliAssembly = Path.Combine(AppContext.BaseDirectory, "avascope.dll");
        var result = await RunCliAsync(cliAssembly);

        Assert.Equal(2, result.ExitCode);
        Assert.Contains("--direction left|right|up|down|start|end", result.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("--destination-target-node", result.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("--duration-ms", result.StandardOutput, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CapabilitiesCommandReportsProtocolAndToolCapabilities()
    {
        var cliAssembly = Path.Combine(AppContext.BaseDirectory, "avascope.dll");
        Assert.True(File.Exists(cliAssembly), $"Expected CLI assembly at {cliAssembly}.");

        var result = await RunCliAsync(cliAssembly, "capabilities");

        Assert.Equal(0, result.ExitCode);
        Assert.True(string.IsNullOrWhiteSpace(result.StandardError), result.StandardError);

        var payload = JsonSerializer.Deserialize<ToolResult<AvaScopeCapabilitiesResponse>>(result.StandardOutput, JsonOptions);
        Assert.NotNull(payload);
        Assert.True(payload.Success, payload.Error?.Message);
        Assert.Equal("avascope", payload.Value!.ServiceName);
        Assert.Equal(AvaScopeProduct.Version, payload.Value.ProductVersion);
        Assert.Contains(payload.Value.Capabilities, capability =>
            capability.Id == AvaScopeCapabilityIds.ProtocolCapabilityDiscovery
            && capability.Status == AvaScopeCapabilityStatuses.Available
            && capability.Metadata["productVersion"] == AvaScopeProduct.Version);
        Assert.Contains(payload.Value.Capabilities, capability =>
            capability.Id == AvaScopeCapabilityIds.RuntimeStyleLayoutMutation
            && capability.Metadata["runtimeMutationCapability"] == RuntimeMutationCapabilityCatalog.StyleLayoutMutation);
        Assert.Contains(payload.Value.Capabilities, capability =>
            capability.Id == AvaScopeCapabilityIds.RuntimeSemanticWorkflow
            && capability.Status == AvaScopeCapabilityStatuses.Available);
        Assert.Contains(payload.Value.Capabilities, capability =>
            capability.Id == AvaScopeCapabilityIds.RuntimeWorkflowEvidence
            && capability.Status == AvaScopeCapabilityStatuses.Available
            && capability.Metadata["reports"] == "json,markdown,junit");
        Assert.Contains(payload.Value.Capabilities, capability =>
            capability.Id == AvaScopeCapabilityIds.RuntimeScenarioRunner
            && capability.Status == AvaScopeCapabilityStatuses.Available
            && capability.Metadata["launchTargets"] == "command,project");
        Assert.Contains(payload.Value.Capabilities, capability =>
            capability.Id == AvaScopeCapabilityIds.RuntimeInteractionAnimation
            && capability.Status == AvaScopeCapabilityStatuses.Available);
        Assert.Contains(payload.Value.Capabilities, capability =>
            capability.Id == AvaScopeCapabilityIds.PreviewSemanticDiff
            && capability.Status == AvaScopeCapabilityStatuses.Available);
        Assert.Contains(payload.Value.Capabilities, capability =>
            capability.Id == AvaScopeCapabilityIds.PreviewStateVariants
            && capability.Status == AvaScopeCapabilityStatuses.Available);
        Assert.Contains(payload.Value.Tools, tool =>
            tool.Adapter == "cli"
            && tool.Name == "capabilities"
            && tool.CapabilityIds.Contains(AvaScopeCapabilityIds.ProtocolCapabilityDiscovery));
        Assert.Contains(payload.Value.Tools, tool =>
            tool.Adapter == "cli"
            && tool.Name == "run-workflow"
            && tool.CapabilityIds.Contains(AvaScopeCapabilityIds.RuntimeSemanticWorkflow)
            && tool.CapabilityIds.Contains(AvaScopeCapabilityIds.RuntimeWorkflowEvidence));
        Assert.Contains(payload.Value.Tools, tool =>
            tool.Adapter == "cli"
            && tool.Name == "run-scenario"
            && tool.CapabilityIds.Contains(AvaScopeCapabilityIds.RuntimeScenarioRunner));
        Assert.Contains(payload.Value.Tools, tool =>
            tool.Adapter == "cli"
            && tool.Name == "record-interaction-animation"
            && tool.CapabilityIds.Contains(AvaScopeCapabilityIds.RuntimeInteractionAnimation));
        Assert.Contains(payload.Value.Tools, tool =>
            tool.Adapter == "cli"
            && tool.Name == "semantic-diff"
            && tool.CapabilityIds.Contains(AvaScopeCapabilityIds.PreviewSemanticDiff));
        Assert.Contains(payload.Value.Tools, tool =>
            tool.Adapter == "cli"
            && tool.Name == "explain-layout"
            && tool.CapabilityIds.Contains(AvaScopeCapabilityIds.RuntimeLayoutExplain));
        var baselineCreate = Assert.Single(payload.Value.Tools, tool =>
            tool.Adapter == "cli"
            && tool.Name == "baseline-create");
        Assert.DoesNotContain(AvaScopeCapabilityIds.ArtifactsRunIndex, baselineCreate.CapabilityIds);
        var baselineCheck = Assert.Single(payload.Value.Tools, tool =>
            tool.Adapter == "cli"
            && tool.Name == "baseline-check");
        Assert.Contains(AvaScopeCapabilityIds.ArtifactsRunIndex, baselineCheck.CapabilityIds);
        var runIndexCapability = Assert.Single(payload.Value.Capabilities, capability =>
            capability.Id == AvaScopeCapabilityIds.ArtifactsRunIndex);
        Assert.DoesNotContain("baseline-create", runIndexCapability.Tools);
        Assert.Contains("baseline-check", runIndexCapability.Tools);
        Assert.Contains(payload.Value.RuntimeMutationCapabilities, capability =>
            capability.Name == RuntimeMutationCapabilityCatalog.RuntimeMutationContract);
    }

    [Fact]
    public async Task CapabilitiesCommandRejectsUnsupportedRequiredCapability()
    {
        var cliAssembly = Path.Combine(AppContext.BaseDirectory, "avascope.dll");
        Assert.True(File.Exists(cliAssembly), $"Expected CLI assembly at {cliAssembly}.");

        var result = await RunCliAsync(cliAssembly, "capabilities", "--require", "post_1_0.magic");

        Assert.Equal(1, result.ExitCode);
        Assert.True(string.IsNullOrWhiteSpace(result.StandardError), result.StandardError);

        var payload = JsonSerializer.Deserialize<ToolResult<AvaScopeCapabilitiesResponse>>(result.StandardOutput, JsonOptions);
        Assert.NotNull(payload);
        Assert.False(payload.Success);
        Assert.Null(payload.Value);
        Assert.Equal(AvaScopeCapabilityErrorCodes.CapabilityNotSupported, payload.Error!.Code);
        Assert.Equal("post_1_0.magic", payload.Error.Details!["unsupportedCapabilities"]);
    }

    [Fact]
    public async Task PreviewCommandRendersAxamlThroughPreviewHostClient()
    {
        var cliAssembly = Path.Combine(AppContext.BaseDirectory, "avascope.dll");
        Assert.True(File.Exists(cliAssembly), $"Expected CLI assembly at {cliAssembly}.");

        var testRoot = Path.Combine(Path.GetTempPath(), "AvaScope.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(testRoot);

        var projectPath = Path.Combine(testRoot, "CliPreviewSample.csproj");
        var viewPath = Path.Combine(testRoot, "MainView.axaml");
        var designDataPath = Path.Combine(testRoot, "PreviewDesignData.cs");
        var outputPath = Path.Combine(testRoot, "preview.png");
        var runIndexDirectory = Path.Combine(testRoot, "run-indexes");

        await File.WriteAllTextAsync(projectPath, """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
              </PropertyGroup>
            </Project>
            """);

        await File.WriteAllTextAsync(viewPath, """
            <UserControl xmlns="https://github.com/avaloniaui">
              <Border Background="#FFFFFFFF">
                <TextBlock Text="CLI preview smoke" />
              </Border>
            </UserControl>
            """);

        await File.WriteAllTextAsync(designDataPath, """
            namespace CliPreviewSample;

            public sealed class PreviewDesignData
            {
                public string Title { get; } = "CLI design data";
            }
            """);

        try
        {
            var result = await RunCliAsync(
                cliAssembly,
                "preview",
                projectPath,
                "--view",
                viewPath,
                "--out",
                outputPath,
                "--width",
                "220",
                "--height",
                "140",
                "--theme",
                "light",
                "--culture",
                "ja-JP",
                "--design-data-type",
                "CliPreviewSample.PreviewDesignData",
                "--state-variant",
                "loading",
                "--run-index",
                runIndexDirectory);

            Assert.Equal(0, result.ExitCode);
            Assert.True(string.IsNullOrWhiteSpace(result.StandardError), result.StandardError);

            var payload = JsonSerializer.Deserialize<ToolResult<PreviewResponse>>(result.StandardOutput, JsonOptions);
            Assert.NotNull(payload);
            Assert.True(payload.Success, payload.Error?.Message);
            Assert.Equal(Path.GetFullPath(outputPath), payload.Value!.FilePath);
            Assert.Equal(220, payload.Value.PixelWidth);
            Assert.Equal(140, payload.Value.PixelHeight);
            Assert.Equal("ja-JP", payload.Value.Culture);
            Assert.Equal("CliPreviewSample.PreviewDesignData", payload.Value.DesignDataType);
            Assert.Equal("loading", payload.Value.StateVariant);
            Assert.True(File.Exists(payload.Value.FilePath));
            Assert.True(new FileInfo(payload.Value.FilePath).Length > 0);
            Assert.NotNull(payload.Value.RunIndex);
            Assert.True(File.Exists(payload.Value.RunIndex!.IndexJsonPath), payload.Value.RunIndex.IndexJsonPath);
            Assert.True(File.Exists(payload.Value.RunIndex.IndexHtmlPath), payload.Value.RunIndex.IndexHtmlPath);
            Assert.True(File.Exists(payload.Value.RunIndex.LatestPointerPath), payload.Value.RunIndex.LatestPointerPath);
            Assert.Equal(Path.GetFullPath(outputPath), Assert.Single(payload.Value.RunIndex.ScreenshotPaths));

            var latest = await RunCliAsync(
                cliAssembly,
                "latest-run",
                "--run-index",
                runIndexDirectory,
                "--project",
                projectPath,
                "--view",
                viewPath,
                "--state-variant",
                "loading");

            Assert.Equal(0, latest.ExitCode);
            var latestPayload = JsonSerializer.Deserialize<ToolResult<ArtifactRunIndexResponse>>(latest.StandardOutput, JsonOptions);
            Assert.NotNull(latestPayload);
            Assert.True(latestPayload.Success, latestPayload.Error?.Message);
            Assert.Equal(payload.Value.RunIndex.RunId, latestPayload.Value!.RunId);
        }
        finally
        {
            await DeleteDirectoryWithRetryAsync(testRoot);
        }
    }

    [Fact]
    public async Task PreviewCommandUsesDesignDimensionsWhenWidthAndHeightAreOmitted()
    {
        var cliAssembly = Path.Combine(AppContext.BaseDirectory, "avascope.dll");
        Assert.True(File.Exists(cliAssembly), $"Expected CLI assembly at {cliAssembly}.");

        var testRoot = Path.Combine(Path.GetTempPath(), "AvaScope.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(testRoot);

        var projectPath = Path.Combine(testRoot, "CliDesignDimensionsPreviewSample.csproj");
        var viewPath = Path.Combine(testRoot, "MainView.axaml");
        var outputPath = Path.Combine(testRoot, "preview.png");

        await File.WriteAllTextAsync(projectPath, """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
              </PropertyGroup>
            </Project>
            """);

        await File.WriteAllTextAsync(viewPath, """
            <UserControl xmlns="https://github.com/avaloniaui"
                         xmlns:d="http://schemas.microsoft.com/expression/blend/2008"
                         xmlns:mc="http://schemas.openxmlformats.org/markup-compatibility/2006"
                         d:DesignWidth="260"
                         d:DesignHeight="150"
                         mc:Ignorable="d">
              <Border Background="#FFFFFFFF" />
            </UserControl>
            """);

        try
        {
            var result = await RunCliAsync(
                cliAssembly,
                "preview",
                projectPath,
                "--view",
                viewPath,
                "--out",
                outputPath);

            Assert.Equal(0, result.ExitCode);
            Assert.True(string.IsNullOrWhiteSpace(result.StandardError), result.StandardError);

            var payload = JsonSerializer.Deserialize<ToolResult<PreviewResponse>>(result.StandardOutput, JsonOptions);
            Assert.NotNull(payload);
            Assert.True(payload.Success, payload.Error?.Message);
            Assert.Equal(260, payload.Value!.PixelWidth);
            Assert.Equal(150, payload.Value.PixelHeight);
            Assert.True(File.Exists(payload.Value.FilePath));
            Assert.True(new FileInfo(payload.Value.FilePath).Length > 0);
        }
        finally
        {
            if (Directory.Exists(testRoot))
            {
                Directory.Delete(testRoot, recursive: true);
            }
        }
    }

    [Fact]
    public async Task PreviewCommandRendersMultipleSizesAndContactSheet()
    {
        var cliAssembly = Path.Combine(AppContext.BaseDirectory, "avascope.dll");
        Assert.True(File.Exists(cliAssembly), $"Expected CLI assembly at {cliAssembly}.");

        var testRoot = Path.Combine(Path.GetTempPath(), "AvaScope.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(testRoot);

        var projectPath = Path.Combine(testRoot, "CliMultiPreviewSample.csproj");
        var viewPath = Path.Combine(testRoot, "MainView.axaml");
        var outputPath = Path.Combine(testRoot, "preview.png");
        var contactSheetPath = Path.Combine(testRoot, "sheet.png");

        await File.WriteAllTextAsync(projectPath, """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
              </PropertyGroup>
            </Project>
            """);

        await File.WriteAllTextAsync(viewPath, """
            <UserControl xmlns="https://github.com/avaloniaui">
              <Border Background="#FFFFFFFF">
                <TextBlock Text="CLI multi preview smoke" />
              </Border>
            </UserControl>
            """);

        try
        {
            var result = await RunCliAsync(
                cliAssembly,
                "preview",
                projectPath,
                "--view",
                viewPath,
                "--out",
                outputPath,
                "--sizes",
                "160x100,120x80",
                "--contact-sheet",
                contactSheetPath);

            Assert.Equal(0, result.ExitCode);
            Assert.True(string.IsNullOrWhiteSpace(result.StandardError), result.StandardError);

            var payload = JsonSerializer.Deserialize<ToolResult<PreviewBatchResponse>>(result.StandardOutput, JsonOptions);
            Assert.NotNull(payload);
            Assert.True(payload.Success, payload.Error?.Message);
            Assert.Equal(2, payload.Value!.Entries.Count);
            Assert.All(payload.Value.Entries, entry =>
            {
                Assert.True(entry.Render.Success, entry.Render.Error?.Message);
                Assert.True(File.Exists(entry.Render.Value!.FilePath));
            });
            Assert.Equal(Path.GetFullPath(contactSheetPath), payload.Value.ContactSheetPath);
            Assert.True(File.Exists(payload.Value.ContactSheetPath));
            Assert.NotEqual(payload.Value.Entries[0].OutputPath, payload.Value.Entries[1].OutputPath);
        }
        finally
        {
            if (Directory.Exists(testRoot))
            {
                Directory.Delete(testRoot, recursive: true);
            }
        }
    }

    [Fact]
    public async Task PreviewCommandAllFailedContactSheetReportsFirstRootCauseAndBuildLog()
    {
        var cliAssembly = Path.Combine(AppContext.BaseDirectory, "avascope.dll");
        Assert.True(File.Exists(cliAssembly), $"Expected CLI assembly at {cliAssembly}.");

        var testRoot = Path.Combine(Path.GetTempPath(), "AvaScope.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(testRoot);

        var projectPath = Path.Combine(testRoot, "CliBrokenMultiPreviewSample.csproj");
        var viewPath = Path.Combine(testRoot, "MainView.axaml");
        var outputPath = Path.Combine(testRoot, "preview.png");
        var contactSheetPath = Path.Combine(testRoot, "sheet.png");

        await File.WriteAllTextAsync(projectPath, """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
              </PropertyGroup>
              <Target Name="FailPreviewBuild" BeforeTargets="Build">
                <Error Text="AVASCOPE_MULTI_ROOT_CAUSE C:\absolute\fixture\MultiFailure.dll" />
              </Target>
            </Project>
            """);

        await File.WriteAllTextAsync(viewPath, """
            <UserControl xmlns="https://github.com/avaloniaui">
              <TextBlock Text="Should not render" />
            </UserControl>
            """);

        try
        {
            var result = await RunCliAsync(
                cliAssembly,
                "preview",
                projectPath,
                "--view",
                viewPath,
                "--out",
                outputPath,
                "--sizes",
                "160x100,120x80",
                "--contact-sheet",
                contactSheetPath);

            Assert.Equal(1, result.ExitCode);
            Assert.True(string.IsNullOrWhiteSpace(result.StandardError), result.StandardError);

            var payload = JsonSerializer.Deserialize<ToolResult<PreviewBatchResponse>>(result.StandardOutput, JsonOptions);
            Assert.NotNull(payload);
            Assert.False(payload.Success);
            Assert.Equal("preview_project_build_failed", payload.Error!.Code);
            Assert.NotNull(payload.Error.Details);
            Assert.Equal("contact_sheet", payload.Error.Details!["phase"]);
            Assert.Equal("preview_project_build_failed", payload.Error.Details["firstRootCauseCode"]);
            Assert.Contains("AVASCOPE_MULTI_ROOT_CAUSE", payload.Error.Details["firstRootCauseMessage"], StringComparison.Ordinal);
            Assert.Contains("160x100", payload.Error.Details["failedViewports"], StringComparison.Ordinal);
            Assert.Contains("120x80", payload.Error.Details["failedViewports"], StringComparison.Ordinal);
            Assert.True(payload.Error.Details.TryGetValue("buildLogPath", out var buildLogPath));
            Assert.True(File.Exists(buildLogPath), buildLogPath);
            var fullLog = await File.ReadAllTextAsync(buildLogPath);
            Assert.Contains("AVASCOPE_MULTI_ROOT_CAUSE C:\\absolute\\fixture\\MultiFailure.dll", fullLog, StringComparison.Ordinal);
            Assert.False(File.Exists(contactSheetPath));
        }
        finally
        {
            if (Directory.Exists(testRoot))
            {
                Directory.Delete(testRoot, recursive: true);
            }
        }
    }

    [Fact]
    public async Task PreviewAnimationCommandRendersOffsetFramesAndStrip()
    {
        var cliAssembly = Path.Combine(AppContext.BaseDirectory, "avascope.dll");
        Assert.True(File.Exists(cliAssembly), $"Expected CLI assembly at {cliAssembly}.");

        var testRoot = Path.Combine(Path.GetTempPath(), "AvaScope.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(testRoot);

        var projectPath = Path.Combine(testRoot, "CliAnimationPreviewSample.csproj");
        var viewPath = Path.Combine(testRoot, "AnimationView.axaml");
        var outputPath = Path.Combine(testRoot, "animation.png");
        var stripPath = Path.Combine(testRoot, "animation-strip.png");
        var viewerPath = Path.Combine(testRoot, "animation.html");

        await File.WriteAllTextAsync(projectPath, """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
              </PropertyGroup>
            </Project>
            """);

        await File.WriteAllTextAsync(viewPath, """
            <UserControl xmlns="https://github.com/avaloniaui">
              <Border Background="#FFFFFFFF">
                <TextBlock Text="CLI animation preview smoke" />
              </Border>
            </UserControl>
            """);

        try
        {
            var result = await RunCliAsync(
                cliAssembly,
                "preview-animation",
                projectPath,
                "--view",
                viewPath,
                "--out",
                outputPath,
                "--time-offsets",
                "0,33",
                "--frame-strip",
                stripPath,
                "--viewer",
                viewerPath,
                "--width",
                "180",
                "--height",
                "100");

            Assert.Equal(0, result.ExitCode);
            Assert.True(string.IsNullOrWhiteSpace(result.StandardError), result.StandardError);

            var payload = JsonSerializer.Deserialize<ToolResult<PreviewAnimationResponse>>(result.StandardOutput, JsonOptions);
            Assert.NotNull(payload);
            Assert.True(payload.Success, payload.Error?.Message);
            Assert.Equal(2, payload.Value!.Frames.Count);
            Assert.Equal(Path.GetFullPath(stripPath), payload.Value.FrameStripPath);
            Assert.True(File.Exists(stripPath));
            Assert.NotNull(payload.Value.Viewer);
            Assert.Equal(Path.GetFullPath(viewerPath), payload.Value.Viewer!.ViewerPath);
            Assert.Equal(new Uri(viewerPath).AbsoluteUri, payload.Value.Viewer.PreviewUrl);
            Assert.True(File.Exists(viewerPath));
            var viewerHtml = await File.ReadAllTextAsync(viewerPath);
            Assert.Contains("data:image/png;base64,", viewerHtml);
            Assert.Contains("animation-json", viewerHtml);
            Assert.All(payload.Value.Frames, frame =>
            {
                Assert.True(frame.Render.Success, frame.Render.Error?.Message);
                Assert.True(File.Exists(frame.OutputPath));
            });
            Assert.Equal(0, payload.Value.Frames[0].Render.Value!.AnimationTimeOffsetMs);
            Assert.Equal(33, payload.Value.Frames[1].Render.Value!.AnimationTimeOffsetMs);
            Assert.Equal("static", payload.Value.Motion.Status);
        }
        finally
        {
            if (Directory.Exists(testRoot))
            {
                Directory.Delete(testRoot, recursive: true);
            }
        }
    }

    [Fact]
    public async Task PreviewCommandUsesProjectPreviewProfileAndAllowsExplicitOverrides()
    {
        var cliAssembly = Path.Combine(AppContext.BaseDirectory, "avascope.dll");
        Assert.True(File.Exists(cliAssembly), $"Expected CLI assembly at {cliAssembly}.");

        var testRoot = Path.Combine(Path.GetTempPath(), "AvaScope.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(testRoot);

        var projectPath = Path.Combine(testRoot, "CliProfilePreviewSample.csproj");
        var viewPath = Path.Combine(testRoot, "MainView.axaml");
        var profileOutputPath = Path.Combine(testRoot, "profile-preview.png");
        var profilePath = Path.Combine(testRoot, "avascope.preview.json");

        await File.WriteAllTextAsync(projectPath, """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
              </PropertyGroup>
            </Project>
            """);

        await File.WriteAllTextAsync(viewPath, """
            <UserControl xmlns="https://github.com/avaloniaui">
              <Border Background="#FFFFFFFF">
                <TextBlock Text="CLI profile preview smoke" />
              </Border>
            </UserControl>
            """);

        await File.WriteAllTextAsync(profilePath, $$"""
            {
              "profiles": {
                "main": {
                  "view": "MainView.axaml",
                  "out": "{{Path.GetFileName(profileOutputPath)}}",
                  "width": 260,
                  "height": 140,
                  "theme": "light"
                }
              }
            }
            """);

        try
        {
            var result = await RunCliAsync(
                cliAssembly,
                "preview",
                projectPath,
                "--profile",
                "main",
                "--width",
                "180");

            Assert.Equal(0, result.ExitCode);
            Assert.True(string.IsNullOrWhiteSpace(result.StandardError), result.StandardError);

            var payload = JsonSerializer.Deserialize<ToolResult<PreviewResponse>>(result.StandardOutput, JsonOptions);
            Assert.NotNull(payload);
            Assert.True(payload.Success, payload.Error?.Message);
            Assert.Equal(Path.GetFullPath(profileOutputPath), payload.Value!.FilePath);
            Assert.Equal(180, payload.Value.PixelWidth);
            Assert.Equal(140, payload.Value.PixelHeight);
            Assert.True(File.Exists(payload.Value.FilePath));
        }
        finally
        {
            if (Directory.Exists(testRoot))
            {
                Directory.Delete(testRoot, recursive: true);
            }
        }
    }

    [Fact]
    public async Task PreviewCommandUsesProjectPreviewProfileVariantAndAllowsExplicitOverrides()
    {
        var cliAssembly = Path.Combine(AppContext.BaseDirectory, "avascope.dll");
        Assert.True(File.Exists(cliAssembly), $"Expected CLI assembly at {cliAssembly}.");

        var testRoot = Path.Combine(Path.GetTempPath(), "AvaScope.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(testRoot);

        var projectPath = Path.Combine(testRoot, "CliProfileVariantPreviewSample.csproj");
        var viewPath = Path.Combine(testRoot, "MainView.axaml");
        var variantOutputPath = Path.Combine(testRoot, "profile-variant-preview.png");
        var profilePath = Path.Combine(testRoot, "avascope.preview.json");

        await File.WriteAllTextAsync(projectPath, """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
              </PropertyGroup>
            </Project>
            """);

        await File.WriteAllTextAsync(viewPath, """
            <UserControl xmlns="https://github.com/avaloniaui">
              <Border Background="#FFFFFFFF">
                <TextBlock Text="CLI profile variant preview smoke" />
              </Border>
            </UserControl>
            """);

        await File.WriteAllTextAsync(profilePath, $$"""
            {
              "profiles": {
                "main": {
                  "view": "MainView.axaml",
                  "out": "base-preview.png",
                  "width": 260,
                  "height": 140,
                  "theme": "light",
                  "variants": {
                    "dark-wide": {
                      "out": "{{Path.GetFileName(variantOutputPath)}}",
                      "width": 300,
                      "theme": "dark",
                      "stateVariant": "loading"
                    }
                  }
                }
              }
            }
            """);

        try
        {
            var result = await RunCliAsync(
                cliAssembly,
                "preview",
                projectPath,
                "--profile",
                "main",
                "--variant",
                "dark-wide",
                "--height",
                "180");

            Assert.Equal(0, result.ExitCode);
            Assert.True(string.IsNullOrWhiteSpace(result.StandardError), result.StandardError);

            var payload = JsonSerializer.Deserialize<ToolResult<PreviewResponse>>(result.StandardOutput, JsonOptions);
            Assert.NotNull(payload);
            Assert.True(payload.Success, payload.Error?.Message);
            Assert.Equal(Path.GetFullPath(variantOutputPath), payload.Value!.FilePath);
            Assert.Equal(300, payload.Value.PixelWidth);
            Assert.Equal(180, payload.Value.PixelHeight);
            Assert.Equal("dark", payload.Value.ThemeVariant);
            Assert.Equal("loading", payload.Value.StateVariant);
            Assert.True(File.Exists(payload.Value.FilePath));
        }
        finally
        {
            if (Directory.Exists(testRoot))
            {
                Directory.Delete(testRoot, recursive: true);
            }
        }
    }

    [Fact]
    public async Task PreviewSessionCommandsCreateListReloadAndClosePersistedSession()
    {
        var cliAssembly = Path.Combine(AppContext.BaseDirectory, "avascope.dll");
        Assert.True(File.Exists(cliAssembly), $"Expected CLI assembly at {cliAssembly}.");

        var testRoot = Path.Combine(Path.GetTempPath(), "AvaScope.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(testRoot);

        var storePath = Path.Combine(testRoot, "preview-sessions");
        var environment = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [PreviewSessionStore.DirectoryEnvironmentVariable] = storePath
        };
        var projectPath = Path.Combine(testRoot, "CliPreviewSessionSample.csproj");
        var viewPath = Path.Combine(testRoot, "MainView.axaml");
        var outputPath = Path.Combine(testRoot, "session-preview.png");

        await File.WriteAllTextAsync(projectPath, """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
              </PropertyGroup>
            </Project>
            """);

        await File.WriteAllTextAsync(viewPath, """
            <UserControl xmlns="https://github.com/avaloniaui">
              <Border Background="#FFFFFFFF">
                <TextBlock Text="CLI preview session smoke" />
              </Border>
            </UserControl>
            """);

        try
        {
            var created = await RunCliAsyncWithEnvironment(
                environment,
                cliAssembly,
                "create-preview-session",
                projectPath,
                "--view",
                viewPath,
                "--out",
                outputPath,
                "--width",
                "220",
                "--height",
                "140",
                "--display-name",
                "CLI persisted preview");

            Assert.Equal(0, created.ExitCode);
            Assert.True(string.IsNullOrWhiteSpace(created.StandardError), created.StandardError);

            var createdPayload = JsonSerializer.Deserialize<ToolResult<PreviewSessionSummary>>(
                created.StandardOutput,
                JsonOptions);
            Assert.NotNull(createdPayload);
            Assert.True(createdPayload.Success, createdPayload.Error?.Message);
            Assert.Equal("CLI persisted preview", createdPayload.Value!.Session.DisplayName);
            Assert.Equal(SessionStates.Active, createdPayload.Value.Session.State);
            Assert.True(createdPayload.Value.LastRender.Success, createdPayload.Value.LastRender.Error?.Message);
            Assert.True(File.Exists(createdPayload.Value.LastRender.Value!.FilePath));

            var sessionId = createdPayload.Value.Session.SessionId.Value;
            var listed = await RunCliAsyncWithEnvironment(
                environment,
                cliAssembly,
                "list-preview-sessions");

            Assert.Equal(0, listed.ExitCode);
            var listedPayload = JsonSerializer.Deserialize<ToolResult<ListPreviewSessionsResponse>>(
                listed.StandardOutput,
                JsonOptions);
            Assert.NotNull(listedPayload);
            Assert.True(listedPayload.Success, listedPayload.Error?.Message);
            var listedSession = Assert.Single(listedPayload.Value!.Sessions);
            Assert.Equal(sessionId, listedSession.Session.SessionId.Value);

            var reloaded = await RunCliAsyncWithEnvironment(
                environment,
                cliAssembly,
                "reload-preview-session",
                "--session",
                sessionId);

            Assert.Equal(0, reloaded.ExitCode);
            var reloadedPayload = JsonSerializer.Deserialize<ToolResult<PreviewSessionSummary>>(
                reloaded.StandardOutput,
                JsonOptions);
            Assert.NotNull(reloadedPayload);
            Assert.True(reloadedPayload.Success, reloadedPayload.Error?.Message);
            Assert.Equal(SessionStates.Active, reloadedPayload.Value!.Session.State);
            Assert.True(reloadedPayload.Value.LastRender.Success, reloadedPayload.Value.LastRender.Error?.Message);

            var viewerPath = Path.Combine(testRoot, "session-viewer.html");
            var viewer = await RunCliAsyncWithEnvironment(
                environment,
                cliAssembly,
                "preview-viewer",
                "--session",
                sessionId,
                "--out",
                viewerPath);

            Assert.Equal(0, viewer.ExitCode);
            var viewerPayload = JsonSerializer.Deserialize<ToolResult<PreviewViewerResponse>>(
                viewer.StandardOutput,
                JsonOptions);
            Assert.NotNull(viewerPayload);
            Assert.True(viewerPayload.Success, viewerPayload.Error?.Message);
            Assert.Equal(Path.GetFullPath(viewerPath), viewerPayload.Value!.ViewerPath);
            Assert.Equal(new Uri(viewerPath).AbsoluteUri, viewerPayload.Value.PreviewUrl);
            Assert.Equal("available", viewerPayload.Value.AgentReview.Status);
            Assert.Contains(new Uri(viewerPath).AbsoluteUri, viewerPayload.Value.AgentReview.PreviewUrls);
            Assert.Equal(sessionId, viewerPayload.Value.Session.Session.SessionId.Value);
            Assert.True(File.Exists(viewerPath));
            var viewerHtml = await File.ReadAllTextAsync(viewerPath);
            Assert.Contains("CLI persisted preview", viewerHtml);
            Assert.Contains("data:image/png;base64,", viewerHtml);

            var closed = await RunCliAsyncWithEnvironment(
                environment,
                cliAssembly,
                "close-preview-session",
                "--session",
                sessionId);

            Assert.Equal(0, closed.ExitCode);
            var closedPayload = JsonSerializer.Deserialize<ToolResult<PreviewSessionSummary>>(
                closed.StandardOutput,
                JsonOptions);
            Assert.NotNull(closedPayload);
            Assert.True(closedPayload.Success, closedPayload.Error?.Message);
            Assert.Equal(SessionStates.Closed, closedPayload.Value!.Session.State);

            var listedAfterClose = await RunCliAsyncWithEnvironment(
                environment,
                cliAssembly,
                "list-preview-sessions");
            var listedAfterClosePayload = JsonSerializer.Deserialize<ToolResult<ListPreviewSessionsResponse>>(
                listedAfterClose.StandardOutput,
                JsonOptions);
            Assert.NotNull(listedAfterClosePayload);
            Assert.Equal(SessionStates.Closed, Assert.Single(listedAfterClosePayload.Value!.Sessions).Session.State);
        }
        finally
        {
            if (Directory.Exists(testRoot))
            {
                Directory.Delete(testRoot, recursive: true);
            }
        }
    }

    [Fact]
    public async Task CreatePreviewSessionCommandUsesProjectPreviewProfile()
    {
        var cliAssembly = Path.Combine(AppContext.BaseDirectory, "avascope.dll");
        Assert.True(File.Exists(cliAssembly), $"Expected CLI assembly at {cliAssembly}.");

        var testRoot = Path.Combine(Path.GetTempPath(), "AvaScope.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(testRoot);

        var storePath = Path.Combine(testRoot, "preview-sessions");
        var environment = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [PreviewSessionStore.DirectoryEnvironmentVariable] = storePath
        };
        var projectPath = Path.Combine(testRoot, "CliProfileSessionSample.csproj");
        var viewPath = Path.Combine(testRoot, "MainView.axaml");
        var outputPath = Path.Combine(testRoot, "profile-session-preview.png");
        var profilePath = Path.Combine(testRoot, "avascope.preview.json");

        await File.WriteAllTextAsync(projectPath, """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
              </PropertyGroup>
            </Project>
            """);

        await File.WriteAllTextAsync(viewPath, """
            <UserControl xmlns="https://github.com/avaloniaui">
              <Border Background="#FFFFFFFF">
                <TextBlock Text="CLI profile session smoke" />
              </Border>
            </UserControl>
            """);

        await File.WriteAllTextAsync(profilePath, $$"""
            {
              "profiles": {
                "main": {
                  "view": "MainView.axaml",
                  "out": "{{Path.GetFileName(outputPath)}}",
                  "width": 200,
                  "height": 120,
                  "displayName": "Profile session"
                }
              }
            }
            """);

        try
        {
            var created = await RunCliAsyncWithEnvironment(
                environment,
                cliAssembly,
                "create-preview-session",
                projectPath,
                "--profile",
                "main");

            Assert.Equal(0, created.ExitCode);
            Assert.True(string.IsNullOrWhiteSpace(created.StandardError), created.StandardError);

            var payload = JsonSerializer.Deserialize<ToolResult<PreviewSessionSummary>>(
                created.StandardOutput,
                JsonOptions);
            Assert.NotNull(payload);
            Assert.True(payload.Success, payload.Error?.Message);
            Assert.Equal("Profile session", payload.Value!.Session.DisplayName);
            Assert.Equal(Path.GetFullPath(outputPath), payload.Value.Request.OutputPath);
            Assert.Equal(200, payload.Value.Request.Width);
            Assert.Equal(120, payload.Value.Request.Height);
            Assert.True(payload.Value.LastRender.Success, payload.Value.LastRender.Error?.Message);
            Assert.True(File.Exists(payload.Value.LastRender.Value!.FilePath));
        }
        finally
        {
            if (Directory.Exists(testRoot))
            {
                Directory.Delete(testRoot, recursive: true);
            }
        }
    }

    [Fact]
    public async Task ReloadPreviewSessionCommandReturnsStructuredErrorWhenNoPreviewSessionMatches()
    {
        var cliAssembly = Path.Combine(AppContext.BaseDirectory, "avascope.dll");
        Assert.True(File.Exists(cliAssembly), $"Expected CLI assembly at {cliAssembly}.");

        var testRoot = Path.Combine(Path.GetTempPath(), "AvaScope.Tests", Guid.NewGuid().ToString("N"));
        var environment = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [PreviewSessionStore.DirectoryEnvironmentVariable] = Path.Combine(testRoot, "preview-sessions")
        };

        try
        {
            var result = await RunCliAsyncWithEnvironment(
                environment,
                cliAssembly,
                "reload-preview-session",
                "--session",
                "missing");

            Assert.Equal(1, result.ExitCode);
            Assert.True(string.IsNullOrWhiteSpace(result.StandardError), result.StandardError);

            var payload = JsonSerializer.Deserialize<ToolResult<PreviewSessionSummary>>(
                result.StandardOutput,
                JsonOptions);
            Assert.NotNull(payload);
            Assert.False(payload.Success);
            Assert.Equal(CoreErrorCodes.SessionNotFound, payload.Error!.Code);
        }
        finally
        {
            if (Directory.Exists(testRoot))
            {
                Directory.Delete(testRoot, recursive: true);
            }
        }
    }

    [Fact]
    public async Task WatchPreviewSessionCommandReloadsWhenWatchedFileChanges()
    {
        var cliAssembly = Path.Combine(AppContext.BaseDirectory, "avascope.dll");
        Assert.True(File.Exists(cliAssembly), $"Expected CLI assembly at {cliAssembly}.");

        var testRoot = Path.Combine(Path.GetTempPath(), "AvaScope.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(testRoot);

        var environment = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [PreviewSessionStore.DirectoryEnvironmentVariable] = Path.Combine(testRoot, "preview-sessions")
        };
        var projectPath = Path.Combine(testRoot, "CliPreviewWatchSample.csproj");
        var viewPath = Path.Combine(testRoot, "MainView.axaml");
        var outputPath = Path.Combine(testRoot, "watch-preview.png");

        await File.WriteAllTextAsync(projectPath, """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
              </PropertyGroup>
            </Project>
            """);

        await File.WriteAllTextAsync(viewPath, """
            <UserControl xmlns="https://github.com/avaloniaui">
              <Border Background="#FFFFFFFF">
                <TextBlock Text="CLI preview watch smoke" />
              </Border>
            </UserControl>
            """);

        try
        {
            var created = await RunCliAsyncWithEnvironment(
                environment,
                cliAssembly,
                "create-preview-session",
                projectPath,
                "--view",
                viewPath,
                "--out",
                outputPath,
                "--width",
                "220",
                "--height",
                "140");
            var createdPayload = JsonSerializer.Deserialize<ToolResult<PreviewSessionSummary>>(
                created.StandardOutput,
                JsonOptions);
            Assert.NotNull(createdPayload);
            Assert.True(createdPayload.Success, createdPayload.Error?.Message);

            var sessionId = createdPayload.Value!.Session.SessionId.Value;
            var watched = await RunCliAsyncWithEnvironmentAfterStart(
                environment,
                cliAssembly,
                async () =>
                {
                    await Task.Delay(2000);
                    for (var attempt = 1; attempt <= 6; attempt++)
                    {
                        await WriteAllTextWithRetryAsync(viewPath, $$"""
                            <UserControl xmlns="https://github.com/avaloniaui">
                              <Border Background="#FFFFFFFF">
                                <TextBlock Text="CLI preview watch changed {{attempt}}" />
                              </Border>
                            </UserControl>
                            """);
                        await Task.Delay(500);
                    }
                },
                "watch-preview-session",
                "--session",
                sessionId,
                "--timeout-ms",
                "30000",
                "--settle-ms",
                "6000",
                "--max-reloads",
                "1",
                "--watch",
                viewPath);

            Assert.True(
                watched.ExitCode == 0,
                $"Expected watch command to exit 0, got {watched.ExitCode}. stdout: {watched.StandardOutput} stderr: {watched.StandardError}");
            Assert.True(string.IsNullOrWhiteSpace(watched.StandardError), watched.StandardError);

            var payload = JsonSerializer.Deserialize<ToolResult<PreviewWatchResponse>>(
                watched.StandardOutput,
                JsonOptions);
            Assert.NotNull(payload);
            Assert.True(payload.Success, payload.Error?.Message);
            Assert.False(payload.Value!.TimedOut);
            Assert.Equal(1, payload.Value.ReloadCount);
            Assert.Contains(payload.Value.Events, static watchEvent => watchEvent.EventType == PreviewWatchEventTypes.Changed);
            Assert.Contains(payload.Value.Events, static watchEvent => watchEvent.EventType == PreviewWatchEventTypes.Reloaded);
            Assert.Equal("one_shot_isolated_child_process", payload.Value.Lifecycle.HostProcessMode);
            Assert.False(payload.Value.Lifecycle.PersistentHostEnabled);
            Assert.Contains("TTL", payload.Value.Lifecycle.TtlSemantics, StringComparison.Ordinal);
            Assert.True(payload.Value.LatestSession!.LastRender.Success, payload.Value.LatestSession.LastRender.Error?.Message);
        }
        finally
        {
            await DeleteDirectoryWithRetryAsync(testRoot);
        }
    }

    [Fact]
    public async Task BaselineCommandsCreateManifestPassCheckAndFailChangedCheck()
    {
        var cliAssembly = Path.Combine(AppContext.BaseDirectory, "avascope.dll");
        Assert.True(File.Exists(cliAssembly), $"Expected CLI assembly at {cliAssembly}.");

        var testRoot = Path.Combine(Path.GetTempPath(), "AvaScope.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(testRoot);

        var projectPath = Path.Combine(testRoot, "CliBaselineSample.csproj");
        var viewPath = Path.Combine(testRoot, "MainView.axaml");
        var manifestPath = Path.Combine(testRoot, "baseline", "baseline.json");
        var baselineDirectory = Path.Combine(testRoot, "baseline", "images");

        await File.WriteAllTextAsync(projectPath, """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
              </PropertyGroup>
            </Project>
            """);

        await File.WriteAllTextAsync(viewPath, """
            <UserControl xmlns="https://github.com/avaloniaui">
              <Border Background="#FFFFFFFF" />
            </UserControl>
            """);

        try
        {
            var created = await RunCliAsync(
                cliAssembly,
                "baseline-create",
                projectPath,
                "--view",
                viewPath,
                "--manifest",
                manifestPath,
                "--sizes",
                "80x60",
                "--out-dir",
                baselineDirectory);

            Assert.Equal(0, created.ExitCode);
            Assert.True(string.IsNullOrWhiteSpace(created.StandardError), created.StandardError);

            var createPayload = JsonSerializer.Deserialize<ToolResult<PreviewBaselineCreateResponse>>(
                created.StandardOutput,
                JsonOptions);
            Assert.NotNull(createPayload);
            Assert.True(createPayload.Success, createPayload.Error?.Message);
            Assert.True(File.Exists(createPayload.Value!.ManifestPath));
            Assert.Single(createPayload.Value.Manifest.Entries);
            Assert.True(File.Exists(createPayload.Value.Manifest.Entries[0].ImagePath));

            var passReportPackDirectory = Path.Combine(testRoot, "reports", "pack-pass");
            var passed = await RunCliAsync(
                cliAssembly,
                "baseline-check",
                "--manifest",
                manifestPath,
                "--out-dir",
                Path.Combine(testRoot, "current-pass"),
                "--diff-dir",
                Path.Combine(testRoot, "diff-pass"),
                "--report-pack",
                passReportPackDirectory);

            Assert.Equal(0, passed.ExitCode);
            var passedPayload = JsonSerializer.Deserialize<ToolResult<PreviewBaselineCheckResponse>>(
                passed.StandardOutput,
                JsonOptions);
            Assert.NotNull(passedPayload);
            Assert.True(passedPayload.Success, passedPayload.Error?.Message);
            Assert.True(passedPayload.Value!.Passed);
            Assert.NotNull(passedPayload.Value.ReportPack);
            Assert.Equal("passed", passedPayload.Value.ReportPack!.Status);

            await Task.Delay(1000);
            await File.WriteAllTextAsync(viewPath, """
                <UserControl xmlns="https://github.com/avaloniaui">
                  <Border Background="#FF000000" />
                </UserControl>
                """);

            var reportPath = Path.Combine(testRoot, "reports", "baseline-check.json");
            var failedReportPackDirectory = Path.Combine(testRoot, "reports", "pack-fail");
            var failed = await RunCliAsync(
                cliAssembly,
                "baseline-check",
                "--manifest",
                manifestPath,
                "--out-dir",
                Path.Combine(testRoot, "current-fail"),
                "--diff-dir",
                Path.Combine(testRoot, "diff-fail"),
                "--report",
                reportPath,
                "--report-pack",
                failedReportPackDirectory);

            Assert.Equal(1, failed.ExitCode);
            var failedPayload = JsonSerializer.Deserialize<ToolResult<PreviewBaselineCheckResponse>>(
                failed.StandardOutput,
                JsonOptions);
            Assert.NotNull(failedPayload);
            Assert.True(failedPayload.Success, failedPayload.Error?.Message);
            Assert.False(failedPayload.Value!.Passed);
            var entry = Assert.Single(failedPayload.Value.Entries);
            Assert.True(entry.Diff.Success, entry.Diff.Error?.Message);
            Assert.False(entry.Diff.Value!.Passed);
            Assert.True(entry.Diff.Value.ChangedPixels > 0);
            Assert.True(File.Exists(entry.DiffPath));
            Assert.Equal(Path.GetFullPath(reportPath), failedPayload.Value.ReportPath);
            Assert.True(File.Exists(reportPath));
            Assert.NotNull(failedPayload.Value.ReportPack);
            Assert.Equal("failed", failedPayload.Value.ReportPack!.Status);
            Assert.Equal(Path.GetFullPath(failedReportPackDirectory), failedPayload.Value.ReportPack.ReportDirectory);
            Assert.Equal(4, failedPayload.Value.ReportPack.Assets.Count);
            Assert.All(failedPayload.Value.ReportPack.Assets, asset => Assert.True(File.Exists(asset.Path), asset.Path));
            Assert.Equal("failed", failedPayload.Value.AgentReview.Status);
            Assert.Contains(failedPayload.Value.AgentReview.Failures, failure => failure.Code == "visual_diff_changed");
            Assert.Contains(failedPayload.Value.AgentReview.ReportPaths, path => path.Path == Path.GetFullPath(reportPath));
            Assert.Contains(failedPayload.Value.AgentReview.ArtifactPaths, path => path.Kind == "diff" && path.Path == Path.GetFullPath(entry.DiffPath));
            Assert.Contains(failedPayload.Value.AgentReview.ReviewUrls, url => url.EndsWith("baseline-report.html", StringComparison.Ordinal));
            Assert.Contains(
                failedPayload.Value.ReportPack.Assets,
                asset => asset.Kind == "html" && File.ReadAllText(asset.Path).Contains("Grouped Failures", StringComparison.Ordinal));
            Assert.Contains(
                failedPayload.Value.ReportPack.Assets,
                asset => asset.Kind == "junit" && File.ReadAllText(asset.Path).Contains("failures=\"1\"", StringComparison.Ordinal));
            var reportPayload = JsonSerializer.Deserialize<PreviewBaselineCheckResponse>(
                await File.ReadAllTextAsync(reportPath),
                JsonOptions);
            Assert.NotNull(reportPayload);
            Assert.False(reportPayload.Passed);
            Assert.Equal(Path.GetFullPath(reportPath), reportPayload.ReportPath);
            Assert.NotNull(reportPayload.ReportPack);
            Assert.Equal(Path.GetFullPath(failedReportPackDirectory), reportPayload.ReportPack!.ReportDirectory);
            Assert.Equal(Path.GetFullPath(entry.CurrentImagePath), reportPayload.Entries[0].CurrentImagePath);
            Assert.Equal(Path.GetFullPath(entry.DiffPath), reportPayload.Entries[0].DiffPath);
            Assert.Equal("failed", reportPayload.AgentReview.Status);
        }
        finally
        {
            if (Directory.Exists(testRoot))
            {
                Directory.Delete(testRoot, recursive: true);
            }
        }
    }

    [Theory]
    [InlineData("--run-index")]
    [InlineData("--task")]
    [InlineData("--run-group")]
    public async Task BaselineCreateRejectsRunIndexFlagsWithBaselineCheckGuidance(string flag)
    {
        var cliAssembly = Path.Combine(AppContext.BaseDirectory, "avascope.dll");
        Assert.True(File.Exists(cliAssembly), $"Expected CLI assembly at {cliAssembly}.");

        var result = await RunCliAsync(cliAssembly, "baseline-create", "sample.csproj", flag, "value");

        Assert.Equal(2, result.ExitCode);
        Assert.True(string.IsNullOrWhiteSpace(result.StandardError), result.StandardError);

        var payload = JsonSerializer.Deserialize<ToolResult<PreviewBaselineCreateResponse>>(
            result.StandardOutput,
            JsonOptions);
        Assert.NotNull(payload);
        Assert.False(payload.Success);
        Assert.Null(payload.Value);
        Assert.Equal("invalid_cli_arguments", payload.Error!.Code);
        Assert.Contains("baseline-create does not support run-index flags", payload.Error.Message, StringComparison.Ordinal);
        Assert.Contains(flag, payload.Error.Message, StringComparison.Ordinal);
        Assert.Contains("baseline-check", payload.Error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task BaselineSuiteCreateRejectsRunIndexFlagsWithBaselineCheckGuidance()
    {
        var cliAssembly = Path.Combine(AppContext.BaseDirectory, "avascope.dll");
        Assert.True(File.Exists(cliAssembly), $"Expected CLI assembly at {cliAssembly}.");

        var result = await RunCliAsync(
            cliAssembly,
            "baseline-create",
            "--suite",
            "suite.json",
            "--manifest",
            "baseline.json",
            "--run-index",
            "runs");

        Assert.Equal(2, result.ExitCode);
        Assert.True(string.IsNullOrWhiteSpace(result.StandardError), result.StandardError);

        var payload = JsonSerializer.Deserialize<ToolResult<PreviewBaselineCreateResponse>>(
            result.StandardOutput,
            JsonOptions);
        Assert.NotNull(payload);
        Assert.False(payload.Success);
        Assert.Null(payload.Value);
        Assert.Equal("invalid_cli_arguments", payload.Error!.Code);
        Assert.Contains("baseline-create does not support run-index flags", payload.Error.Message, StringComparison.Ordinal);
        Assert.Contains("--run-index", payload.Error.Message, StringComparison.Ordinal);
        Assert.Contains("baseline-check", payload.Error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task BaselineSuiteCommandCreatesManifestAndCheckPasses()
    {
        var cliAssembly = Path.Combine(AppContext.BaseDirectory, "avascope.dll");
        Assert.True(File.Exists(cliAssembly), $"Expected CLI assembly at {cliAssembly}.");

        var testRoot = Path.Combine(Path.GetTempPath(), "AvaScope.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(testRoot);

        var projectPath = Path.Combine(testRoot, "CliBaselineSuiteSample.csproj");
        var viewPath = Path.Combine(testRoot, "MainView.axaml");
        var suitePath = Path.Combine(testRoot, "agent-suite.json");
        var manifestPath = Path.Combine(testRoot, "baseline", "suite-baseline.json");
        var baselineDirectory = Path.Combine(testRoot, "baseline", "images");
        var runtimeTarget = new RuntimeTargetContext(
            new SessionId("session-suite"),
            "topLevel:main",
            TreeKinds.Visual,
            "visual:root");

        await File.WriteAllTextAsync(projectPath, """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
              </PropertyGroup>
            </Project>
            """);

        await File.WriteAllTextAsync(viewPath, """
            <UserControl xmlns="https://github.com/avaloniaui">
              <Border Background="#FFFFFFFF">
                <TextBlock Text="suite baseline" />
              </Border>
            </UserControl>
            """);

        var suite = new PreviewBaselineSuiteManifest(
            PreviewBaselineSuiteManifest.CurrentVersion,
            "agent-suite",
            [
                new PreviewBaselineSuiteEntry(
                    "main",
                    projectPath,
                    viewPath,
                    sizes: [new PreviewViewport(80, 60), new PreviewViewport(100, 70)],
                    runtimeTarget: runtimeTarget)
            ],
            new PreviewBaselineSuiteDefaults(
                dpis: [96],
                themes: ["light"],
                cultures: ["en-US"],
                animationFramesMs: [0],
                mutationPresetIds: ["wide"],
                comparisonRules: new PreviewComparisonRules(
                    tolerance: 1,
                    maxChangedPixels: 0,
                    ignoredRegions:
                    [
                        new ScreenshotRegion(0, 0, 1, 1, "volatile-pixel")
                    ],
                    requiredRegions:
                    [
                        new PreviewRequiredRegion(new ScreenshotRegion(0, 0, 10, 10, "top-left"))
                    ])),
            [
                new PreviewBaselineMutationPreset(
                    "wide",
                    "Metadata-only width preset.",
                    [
                        new RuntimeMutationOperation(
                            RuntimeMutationOperationKinds.SetProperty,
                            propertyName: "Width",
                            value: "100",
                            valueType: "double")
                    ])
            ]);
        await File.WriteAllTextAsync(suitePath, JsonSerializer.Serialize(suite, JsonOptions));

        try
        {
            var created = await RunCliAsync(
                cliAssembly,
                "baseline-create",
                "--suite",
                suitePath,
                "--manifest",
                manifestPath,
                "--out-dir",
                baselineDirectory);

            Assert.Equal(0, created.ExitCode);
            Assert.True(string.IsNullOrWhiteSpace(created.StandardError), created.StandardError);

            var createPayload = JsonSerializer.Deserialize<ToolResult<PreviewBaselineCreateResponse>>(
                created.StandardOutput,
                JsonOptions);
            Assert.NotNull(createPayload);
            Assert.True(createPayload.Success, createPayload.Error?.Message);
            Assert.Equal(Path.GetFullPath(manifestPath), createPayload.Value!.ManifestPath);
            Assert.Equal(2, createPayload.Value.Manifest.Entries.Count);
            var first = createPayload.Value.Manifest.Entries[0];
            Assert.Equal("agent-suite", first.SuiteName);
            Assert.Equal("main", first.SuiteEntryId);
            Assert.Equal("light", first.ThemeVariant);
            Assert.Equal("en-US", first.Culture);
            Assert.Equal(0, first.AnimationTimeOffsetMs);
            Assert.Equal("wide", Assert.Single(first.MutationPresetIds));
            Assert.Equal(runtimeTarget, first.RuntimeTarget);
            Assert.NotNull(first.ComparisonRules);
            Assert.Equal(1, first.ComparisonRules!.Tolerance);
            Assert.Equal(0, first.ComparisonRules.MaxChangedPixels);
            Assert.Equal("volatile-pixel", Assert.Single(first.ComparisonRules.IgnoredRegions).Name);
            Assert.Equal("top-left", Assert.Single(first.ComparisonRules.RequiredRegions).Region.Name);
            Assert.All(createPayload.Value.Manifest.Entries, entry =>
            {
                Assert.True(File.Exists(entry.ImagePath), entry.ImagePath);
                Assert.StartsWith(Path.GetFullPath(baselineDirectory), entry.ImagePath, StringComparison.OrdinalIgnoreCase);
            });

            var reportPackDirectory = Path.Combine(testRoot, "suite-report-pack");
            var passed = await RunCliAsync(
                cliAssembly,
                "baseline-check",
                "--manifest",
                manifestPath,
                "--out-dir",
                Path.Combine(testRoot, "current-pass"),
                "--diff-dir",
                Path.Combine(testRoot, "diff-pass"),
                "--report-pack",
                reportPackDirectory);

            Assert.Equal(0, passed.ExitCode);
            var passedPayload = JsonSerializer.Deserialize<ToolResult<PreviewBaselineCheckResponse>>(
                passed.StandardOutput,
                JsonOptions);
            Assert.NotNull(passedPayload);
            Assert.True(passedPayload.Success, passedPayload.Error?.Message);
            Assert.True(passedPayload.Value!.Passed);
            Assert.NotNull(passedPayload.Value.ReportPack);
            Assert.Equal("passed", passedPayload.Value.ReportPack!.Status);
            Assert.Equal(2, passedPayload.Value.ReportPack.TotalEntries);
            Assert.Equal(0, passedPayload.Value.ReportPack.FailedEntries);
            Assert.Equal(Path.GetFullPath(reportPackDirectory), passedPayload.Value.ReportPack.ReportDirectory);
            Assert.All(passedPayload.Value.ReportPack.Assets, asset => Assert.True(File.Exists(asset.Path), asset.Path));
            Assert.Contains(
                passedPayload.Value.ReportPack.Assets,
                asset => asset.Kind == "sarif" && File.ReadAllText(asset.Path).Contains("\"results\": []", StringComparison.Ordinal));
            Assert.Equal(2, passedPayload.Value.Entries.Count);
            Assert.All(passedPayload.Value.Entries, entry =>
            {
                Assert.True(entry.Diff.Success, entry.Diff.Error?.Message);
                Assert.Equal(1, entry.Diff.Value!.Tolerance);
                Assert.Equal(1, entry.Diff.Value.IgnoredPixelCount);
                Assert.Equal(0, entry.Diff.Value.MaxChangedPixels);
                var requiredRegion = Assert.Single(entry.RequiredRegionResults);
                Assert.True(requiredRegion.Result.Success, requiredRegion.Result.Error?.Message);
                Assert.True(requiredRegion.Result.Value!.Passed);
                Assert.True(File.Exists(requiredRegion.Result.Value.CropPath));
            });
        }
        finally
        {
            await DeleteDirectoryWithRetryAsync(testRoot);
        }
    }

    [Fact]
    public async Task PreviewCommandResolvesRelativeProjectAndOutputPathsFromCallerWorkingDirectory()
    {
        var cliAssembly = Path.Combine(AppContext.BaseDirectory, "avascope.dll");
        Assert.True(File.Exists(cliAssembly), $"Expected CLI assembly at {cliAssembly}.");

        var testRoot = Path.Combine(Path.GetTempPath(), "AvaScope.Tests", Guid.NewGuid().ToString("N"));
        var sampleDirectory = Path.Combine(testRoot, "Sample");
        var viewsDirectory = Path.Combine(sampleDirectory, "Views");
        Directory.CreateDirectory(viewsDirectory);

        var projectPath = Path.Combine(sampleDirectory, "RelativePreviewSample.csproj");
        var viewPath = Path.Combine(viewsDirectory, "MainView.axaml");
        var outputPath = Path.Combine(testRoot, "artifacts", "relative-preview.png");

        await File.WriteAllTextAsync(projectPath, """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
              </PropertyGroup>
            </Project>
            """);

        await File.WriteAllTextAsync(viewPath, """
            <UserControl xmlns="https://github.com/avaloniaui">
              <Border Background="#FFFFFFFF">
                <TextBlock Text="Relative CLI preview smoke" />
              </Border>
            </UserControl>
            """);

        try
        {
            var result = await RunCliAsyncFromDirectory(
                testRoot,
                cliAssembly,
                "preview",
                Path.Combine("Sample", "RelativePreviewSample.csproj"),
                "--view",
                Path.Combine("Views", "MainView.axaml"),
                "--out",
                Path.Combine("artifacts", "relative-preview.png"),
                "--width",
                "220",
                "--height",
                "140");

            Assert.Equal(0, result.ExitCode);
            Assert.True(string.IsNullOrWhiteSpace(result.StandardError), result.StandardError);

            var payload = JsonSerializer.Deserialize<ToolResult<PreviewResponse>>(result.StandardOutput, JsonOptions);
            Assert.NotNull(payload);
            Assert.True(payload.Success, payload.Error?.Message);
            AssertSamePath(outputPath, payload.Value!.FilePath);
            AssertSamePath(projectPath, payload.Value.ProjectPath);
            AssertSamePath(viewPath, payload.Value.ViewPath);
            Assert.True(File.Exists(payload.Value.FilePath));
            Assert.True(new FileInfo(payload.Value.FilePath).Length > 0);
        }
        finally
        {
            if (Directory.Exists(testRoot))
            {
                Directory.Delete(testRoot, recursive: true);
            }
        }
    }

    [Fact]
    public async Task PreviewCommandReturnsStructuredErrorForInvalidArguments()
    {
        var cliAssembly = Path.Combine(AppContext.BaseDirectory, "avascope.dll");
        Assert.True(File.Exists(cliAssembly), $"Expected CLI assembly at {cliAssembly}.");

        var result = await RunCliAsync(cliAssembly, "preview");

        Assert.Equal(2, result.ExitCode);
        Assert.True(string.IsNullOrWhiteSpace(result.StandardError), result.StandardError);

        var payload = JsonSerializer.Deserialize<ToolResult<PreviewResponse>>(result.StandardOutput, JsonOptions);
        Assert.NotNull(payload);
        Assert.False(payload.Success);
        Assert.Equal("invalid_cli_arguments", payload.Error!.Code);
    }

    [Fact]
    public async Task PreviewCommandRejectsInvalidMinimumDiagnosticSeverityBeforeRendering()
    {
        var cliAssembly = Path.Combine(AppContext.BaseDirectory, "avascope.dll");
        var result = await RunCliAsync(
            cliAssembly,
            "preview",
            "sample.csproj",
            "--view",
            "MainView.axaml",
            "--out",
            "preview.png",
            "--minimum-severity",
            "critical");

        Assert.Equal(2, result.ExitCode);
        var payload = JsonSerializer.Deserialize<ToolResult<PreviewResponse>>(
            result.StandardOutput,
            JsonOptions);
        Assert.False(payload!.Success);
        Assert.Equal(CoreErrorCodes.InvalidPreviewRequest, payload.Error!.Code);
        Assert.Contains("all, info, warning, error", payload.Error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task PreviewCommandPreservesPreviewReadinessFailureDetails()
    {
        var cliAssembly = Path.Combine(AppContext.BaseDirectory, "avascope.dll");
        Assert.True(File.Exists(cliAssembly), $"Expected CLI assembly at {cliAssembly}.");

        var testRoot = Path.Combine(Path.GetTempPath(), "AvaScope.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(testRoot);

        var projectPath = Path.Combine(testRoot, "CliReadinessPreviewSample.csproj");
        var viewPath = Path.Combine(testRoot, "Views", "MissingView.axaml");
        var outputPath = Path.Combine(testRoot, "preview.png");

        await File.WriteAllTextAsync(projectPath, """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
              </PropertyGroup>
            </Project>
            """);

        try
        {
            var result = await RunCliAsync(
                cliAssembly,
                "preview",
                projectPath,
                "--view",
                Path.Combine("Views", "MissingView.axaml"),
                "--out",
                outputPath,
                "--width",
                "220",
                "--height",
                "140");

            Assert.Equal(1, result.ExitCode);
            Assert.True(string.IsNullOrWhiteSpace(result.StandardError), result.StandardError);

            var payload = JsonSerializer.Deserialize<ToolResult<PreviewResponse>>(result.StandardOutput, JsonOptions);
            Assert.NotNull(payload);
            Assert.False(payload.Success);
            Assert.Equal("preview_readiness_failed", payload.Error!.Code);
            Assert.NotNull(payload.Error.Details);
            Assert.Equal("readiness", payload.Error.Details!["phase"]);
            Assert.Equal("view_file", payload.Error.Details["requirement"]);
            Assert.Equal(Path.GetFullPath(projectPath), payload.Error.Details["projectPath"]);
            Assert.Equal(Path.GetFullPath(viewPath), payload.Error.Details["viewPath"]);
            Assert.Contains("existing .axaml", payload.Error.Details["nextAction"], StringComparison.Ordinal);
            Assert.False(File.Exists(outputPath));
        }
        finally
        {
            if (Directory.Exists(testRoot))
            {
                Directory.Delete(testRoot, recursive: true);
            }
        }
    }

    [Fact]
    public async Task PreviewCommandPreservesPreviewFailureDetails()
    {
        var cliAssembly = Path.Combine(AppContext.BaseDirectory, "avascope.dll");
        Assert.True(File.Exists(cliAssembly), $"Expected CLI assembly at {cliAssembly}.");

        var testRoot = Path.Combine(Path.GetTempPath(), "AvaScope.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(testRoot);

        var projectPath = Path.Combine(testRoot, "BrokenPreviewSample.csproj");
        var viewPath = Path.Combine(testRoot, "MainView.axaml");
        var outputPath = Path.Combine(testRoot, "preview.png");

        await File.WriteAllTextAsync(projectPath, """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
            </Project>
            """);

        await File.WriteAllTextAsync(viewPath, """
            <UserControl xmlns="https://github.com/avaloniaui" />
            """);

        try
        {
            var result = await RunCliAsync(
                cliAssembly,
                "preview",
                projectPath,
                "--view",
                viewPath,
                "--out",
                outputPath,
                "--width",
                "220",
                "--height",
                "140");

            Assert.Equal(1, result.ExitCode);
            Assert.True(string.IsNullOrWhiteSpace(result.StandardError), result.StandardError);

            var payload = JsonSerializer.Deserialize<ToolResult<PreviewResponse>>(result.StandardOutput, JsonOptions);
            Assert.NotNull(payload);
            Assert.False(payload.Success);
            Assert.Equal("preview_project_build_failed", payload.Error!.Code);
            Assert.NotNull(payload.Error.Details);
            Assert.Equal("build", payload.Error.Details!["phase"]);
            Assert.Equal(Path.GetFullPath(projectPath), payload.Error.Details["projectPath"]);
            Assert.Equal("1", payload.Error.Details["exitCode"]);
            Assert.Contains("Build FAILED", payload.Error.Details["outputTail"], StringComparison.Ordinal);
            Assert.False(File.Exists(outputPath));
        }
        finally
        {
            if (Directory.Exists(testRoot))
            {
                Directory.Delete(testRoot, recursive: true);
            }
        }
    }

    [Fact]
    public async Task AttachCommandReturnsStructuredErrorWhenNoBridgeSessionMatches()
    {
        var cliAssembly = Path.Combine(AppContext.BaseDirectory, "avascope.dll");
        Assert.True(File.Exists(cliAssembly), $"Expected CLI assembly at {cliAssembly}.");

        var result = await RunCliAsync(
            cliAssembly,
            "attach",
            "--process",
            int.MaxValue.ToString(CultureInfo.InvariantCulture));

        Assert.Equal(1, result.ExitCode);
        Assert.True(string.IsNullOrWhiteSpace(result.StandardError), result.StandardError);

        var payload = JsonSerializer.Deserialize<ToolResult<AttachToAppResponse>>(result.StandardOutput, JsonOptions);
        Assert.NotNull(payload);
        Assert.False(payload.Success);
        Assert.Equal(CoreErrorCodes.BridgeSessionNotFound, payload.Error!.Code);
    }

    [Fact]
    public async Task AttachCommandRejectsInvalidProcessId()
    {
        var cliAssembly = Path.Combine(AppContext.BaseDirectory, "avascope.dll");
        Assert.True(File.Exists(cliAssembly), $"Expected CLI assembly at {cliAssembly}.");

        var result = await RunCliAsync(cliAssembly, "attach", "--process", "abc");

        Assert.Equal(2, result.ExitCode);
        Assert.True(string.IsNullOrWhiteSpace(result.StandardError), result.StandardError);

        var payload = JsonSerializer.Deserialize<ToolResult<PreviewResponse>>(result.StandardOutput, JsonOptions);
        Assert.NotNull(payload);
        Assert.False(payload.Success);
        Assert.Equal("invalid_cli_arguments", payload.Error!.Code);
    }

    [Fact]
    public async Task AttachCommandSelectsManifestPathAndProcessName()
    {
        var cliAssembly = Path.Combine(AppContext.BaseDirectory, "avascope.dll");
        Assert.True(File.Exists(cliAssembly), $"Expected CLI assembly at {cliAssembly}.");

        var manifestDirectory = Path.Combine(Path.GetTempPath(), "AvaScope.Tests", Guid.NewGuid().ToString("N"));
        var sessionId = SessionId.New();
        var pipeName = TestPipeNames.New();
        var processName = Process.GetCurrentProcess().ProcessName;
        var manifestPath = WriteBridgeManifest(
            sessionId,
            pipeName,
            manifestDirectory,
            processName: processName);
        var serverTask = RespondToBridgeRequestAsync(pipeName, request =>
        {
            Assert.Equal(BridgeIpcMethods.Health, request.Method);
            return BridgeIpcResponse.Ok(request.RequestId, HealthResponse.Current());
        });

        try
        {
            var result = await RunCliAsync(
                cliAssembly,
                "attach",
                "--manifest",
                manifestPath,
                "--process-name",
                processName + ".exe");
            var request = await serverTask;

            Assert.Equal(0, result.ExitCode);
            Assert.Equal(BridgeIpcMethods.Health, request.Method);
            Assert.True(string.IsNullOrWhiteSpace(result.StandardError), result.StandardError);

            var payload = JsonSerializer.Deserialize<ToolResult<AttachToAppResponse>>(result.StandardOutput, JsonOptions);
            Assert.NotNull(payload);
            Assert.True(payload.Success, payload.Error?.Message);
            Assert.Equal(sessionId, payload.Value!.Session.SessionId);
            Assert.Equal(processName, payload.Value.ProcessName);
            Assert.Equal(Path.GetFullPath(manifestPath), payload.Value.ManifestPath);
        }
        finally
        {
            if (Directory.Exists(manifestDirectory))
            {
                Directory.Delete(manifestDirectory, recursive: true);
            }
        }
    }

    [Fact]
    public async Task SessionCapabilitiesCommandReturnsNegotiatedContract()
    {
        var cliAssembly = Path.Combine(AppContext.BaseDirectory, "avascope.dll");
        var manifestDirectory = Path.Combine(
            Path.GetTempPath(),
            "AvaScope.Tests",
            $"cli-capabilities-{Guid.NewGuid():N}");
        var sessionId = SessionId.New();
        var pipeName = TestPipeNames.New();
        WriteBridgeManifest(sessionId, pipeName, manifestDirectory);
        var expected = SessionCapabilitiesResponse.Current(sessionId, Environment.ProcessId);
        var serverTask = RespondToBridgeRequestAsync(
            pipeName,
            request => BridgeIpcResponse.Ok(request.RequestId, expected));

        try
        {
            var result = await RunCliAsync(
                cliAssembly,
                "session-capabilities",
                "--session",
                sessionId.Value,
                "--manifest-dir",
                manifestDirectory);
            var request = await serverTask;

            Assert.Equal(0, result.ExitCode);
            Assert.Equal(BridgeIpcMethods.Capabilities, request.Method);
            var payload = JsonSerializer.Deserialize<ToolResult<SessionCapabilitiesResponse>>(
                result.StandardOutput,
                JsonOptions);
            Assert.NotNull(payload);
            Assert.True(payload.Success, payload.Error?.Message);
            Assert.Equal(expected.Revision, payload.Value!.Revision);
            Assert.Equal(InputActions.All, payload.Value.InputActions);
        }
        finally
        {
            if (Directory.Exists(manifestDirectory))
            {
                Directory.Delete(manifestDirectory, recursive: true);
            }
        }
    }

    [Fact]
    public async Task ListTopLevelsCommandReturnsStructuredErrorWhenNoBridgeSessionMatches()
    {
        var cliAssembly = Path.Combine(AppContext.BaseDirectory, "avascope.dll");
        Assert.True(File.Exists(cliAssembly), $"Expected CLI assembly at {cliAssembly}.");

        var result = await RunCliAsync(cliAssembly, "list-top-levels", "--session", "missing");

        Assert.Equal(1, result.ExitCode);
        Assert.True(string.IsNullOrWhiteSpace(result.StandardError), result.StandardError);

        var payload = JsonSerializer.Deserialize<ToolResult<ListTopLevelsResponse>>(result.StandardOutput, JsonOptions);
        Assert.NotNull(payload);
        Assert.False(payload.Success);
        Assert.Equal(CoreErrorCodes.BridgeSessionNotFound, payload.Error!.Code);
    }

    [Fact]
    public async Task ListTopLevelsCommandReadsTopLevelsThroughBridgePipe()
    {
        var cliAssembly = Path.Combine(AppContext.BaseDirectory, "avascope.dll");
        Assert.True(File.Exists(cliAssembly), $"Expected CLI assembly at {cliAssembly}.");

        var sessionId = SessionId.New();
        var pipeName = TestPipeNames.New();
        var manifestPath = WriteBridgeManifest(sessionId, pipeName);
        var expectedTopLevel = new TopLevelSummary(
            "topLevel:cli",
            "window",
            "CLI Pipe Window",
            320,
            200,
            1,
            true);

        var serverTask = RespondToBridgeRequestAsync(pipeName, request =>
        {
            Assert.Equal(BridgeIpcMethods.ListTopLevels, request.Method);
            return BridgeIpcResponse.Ok(request.RequestId, new[] { expectedTopLevel });
        });

        try
        {
            var result = await RunCliAsync(cliAssembly, "list-top-levels", "--session", sessionId.Value);
            var request = await serverTask;

            Assert.Equal(0, result.ExitCode);
            Assert.Equal(BridgeIpcMethods.ListTopLevels, request.Method);
            Assert.True(string.IsNullOrWhiteSpace(result.StandardError), result.StandardError);

            var payload = JsonSerializer.Deserialize<ToolResult<ListTopLevelsResponse>>(result.StandardOutput, JsonOptions);
            Assert.NotNull(payload);
            Assert.True(payload.Success, payload.Error?.Message);
            var topLevel = Assert.Single(payload.Value!.TopLevels);
            Assert.Equal(expectedTopLevel.Id, topLevel.Id);
            Assert.Equal(expectedTopLevel.Title, topLevel.Title);
            Assert.True(topLevel.IsActive);
        }
        finally
        {
            if (File.Exists(manifestPath))
            {
                File.Delete(manifestPath);
            }
        }
    }

    [Fact]
    public async Task ListTopLevelsCommandUsesCustomManifestDirectory()
    {
        var cliAssembly = Path.Combine(AppContext.BaseDirectory, "avascope.dll");
        Assert.True(File.Exists(cliAssembly), $"Expected CLI assembly at {cliAssembly}.");

        var manifestDirectory = Path.Combine(Path.GetTempPath(), "AvaScope.Tests", Guid.NewGuid().ToString("N"));
        var sessionId = SessionId.New();
        var pipeName = TestPipeNames.New();
        WriteBridgeManifest(sessionId, pipeName, manifestDirectory);
        var expectedTopLevel = new TopLevelSummary(
            "topLevel:custom",
            "window",
            "Custom Manifest Window",
            640,
            360,
            1,
            true);
        var serverTask = RespondToBridgeRequestAsync(pipeName, request =>
        {
            Assert.Equal(BridgeIpcMethods.ListTopLevels, request.Method);
            return BridgeIpcResponse.Ok(request.RequestId, new[] { expectedTopLevel });
        });

        try
        {
            var result = await RunCliAsync(
                cliAssembly,
                "list-top-levels",
                "--session",
                sessionId.Value,
                "--manifest-dir",
                manifestDirectory);
            var request = await serverTask;

            Assert.Equal(0, result.ExitCode);
            Assert.Equal(BridgeIpcMethods.ListTopLevels, request.Method);
            var payload = JsonSerializer.Deserialize<ToolResult<ListTopLevelsResponse>>(result.StandardOutput, JsonOptions);
            Assert.NotNull(payload);
            Assert.True(payload.Success, payload.Error?.Message);
            Assert.Equal("topLevel:custom", Assert.Single(payload.Value!.TopLevels).Id);
        }
        finally
        {
            if (Directory.Exists(manifestDirectory))
            {
                Directory.Delete(manifestDirectory, recursive: true);
            }
        }
    }

    [Fact]
    public async Task ListTopLevelsCommandRejectsMissingSession()
    {
        var cliAssembly = Path.Combine(AppContext.BaseDirectory, "avascope.dll");
        Assert.True(File.Exists(cliAssembly), $"Expected CLI assembly at {cliAssembly}.");

        var result = await RunCliAsync(cliAssembly, "list-top-levels");

        Assert.Equal(2, result.ExitCode);
        Assert.True(string.IsNullOrWhiteSpace(result.StandardError), result.StandardError);

        var payload = JsonSerializer.Deserialize<ToolResult<ListTopLevelsResponse>>(result.StandardOutput, JsonOptions);
        Assert.NotNull(payload);
        Assert.False(payload.Success);
        Assert.Equal("invalid_cli_arguments", payload.Error!.Code);
    }

    [Theory]
    [InlineData("visual-tree", BridgeIpcMethods.VisualTree, TreeKinds.Visual)]
    [InlineData("logical-tree", BridgeIpcMethods.LogicalTree, TreeKinds.Logical)]
    public async Task TreeCommandReadsTreeThroughBridgePipe(
        string command,
        string expectedMethod,
        string expectedTreeKind)
    {
        var cliAssembly = Path.Combine(AppContext.BaseDirectory, "avascope.dll");
        Assert.True(File.Exists(cliAssembly), $"Expected CLI assembly at {cliAssembly}.");

        var sessionId = SessionId.New();
        var pipeName = TestPipeNames.New();
        var manifestPath = WriteBridgeManifest(sessionId, pipeName);
        var root = new TreeNodeSummary(
            $"{expectedTreeKind}:root",
            "Window",
            "CliWindow",
            children:
            [
                new TreeNodeSummary(
                    $"{expectedTreeKind}:child",
                    "TextBlock",
                    text: "CLI tree",
                    target: new RuntimeTargetContext(
                        sessionId,
                        "topLevel:cli",
                        expectedTreeKind,
                        $"{expectedTreeKind}:child"))
            ],
            target: new RuntimeTargetContext(sessionId, "topLevel:cli", expectedTreeKind, $"{expectedTreeKind}:root"));

        var serverTask = RespondToBridgeRequestAsync(pipeName, request =>
        {
            Assert.Equal(expectedMethod, request.Method);
            Assert.Equal("topLevel:cli", request.TopLevelId);
            Assert.Equal(2, request.MaxDepth);
            return BridgeIpcResponse.Ok(
                request.RequestId,
                new TreeResponse(sessionId, request.TopLevelId!, expectedTreeKind, 2, root));
        });

        try
        {
            var result = await RunCliAsync(
                cliAssembly,
                command,
                "--session",
                sessionId.Value,
                "--top-level",
                "topLevel:cli",
                "--max-depth",
                "2");
            var request = await serverTask;

            Assert.Equal(0, result.ExitCode);
            Assert.Equal(expectedMethod, request.Method);
            Assert.True(string.IsNullOrWhiteSpace(result.StandardError), result.StandardError);

            var payload = JsonSerializer.Deserialize<ToolResult<TreeResponse>>(result.StandardOutput, JsonOptions);
            Assert.NotNull(payload);
            Assert.True(payload.Success, payload.Error?.Message);
            Assert.Equal(sessionId, payload.Value!.SessionId);
            Assert.Equal("topLevel:cli", payload.Value.TopLevelId);
            Assert.Equal(expectedTreeKind, payload.Value.TreeKind);
            Assert.Equal(2, payload.Value.DepthLimit);
            Assert.Equal(sessionId, payload.Value.Target.SessionId);
            Assert.Equal("topLevel:cli", payload.Value.Target.TopLevelId);
            Assert.Equal(expectedTreeKind, payload.Value.Target.TreeKind);
            Assert.Equal($"{expectedTreeKind}:root", payload.Value.Root.Target!.NodeId);
            Assert.Equal("Window", payload.Value.Root.NodeType);
            var child = Assert.Single(payload.Value.Root.Children);
            Assert.Equal("CLI tree", child.Text);
            Assert.Equal($"{expectedTreeKind}:child", child.Target!.NodeId);
        }
        finally
        {
            if (File.Exists(manifestPath))
            {
                File.Delete(manifestPath);
            }
        }
    }

    [Fact]
    public async Task VisualTreeCommandReturnsStructuredErrorWhenNoBridgeSessionMatches()
    {
        var cliAssembly = Path.Combine(AppContext.BaseDirectory, "avascope.dll");
        Assert.True(File.Exists(cliAssembly), $"Expected CLI assembly at {cliAssembly}.");

        var result = await RunCliAsync(
            cliAssembly,
            "visual-tree",
            "--session",
            "missing",
            "--top-level",
            "topLevel:missing");

        Assert.Equal(1, result.ExitCode);
        Assert.True(string.IsNullOrWhiteSpace(result.StandardError), result.StandardError);

        var payload = JsonSerializer.Deserialize<ToolResult<TreeResponse>>(result.StandardOutput, JsonOptions);
        Assert.NotNull(payload);
        Assert.False(payload.Success);
        Assert.Equal(CoreErrorCodes.BridgeSessionNotFound, payload.Error!.Code);
    }

    [Theory]
    [InlineData("visual-tree")]
    [InlineData("logical-tree")]
    public async Task TreeCommandRejectsMissingSession(string command)
    {
        var cliAssembly = Path.Combine(AppContext.BaseDirectory, "avascope.dll");
        Assert.True(File.Exists(cliAssembly), $"Expected CLI assembly at {cliAssembly}.");

        var result = await RunCliAsync(cliAssembly, command, "--top-level", "topLevel:missing");

        Assert.Equal(2, result.ExitCode);
        Assert.True(string.IsNullOrWhiteSpace(result.StandardError), result.StandardError);

        var payload = JsonSerializer.Deserialize<ToolResult<TreeResponse>>(result.StandardOutput, JsonOptions);
        Assert.NotNull(payload);
        Assert.False(payload.Success);
        Assert.Equal("invalid_cli_arguments", payload.Error!.Code);
    }

    [Theory]
    [InlineData("visual-tree")]
    [InlineData("logical-tree")]
    public async Task TreeCommandRejectsMissingTopLevel(string command)
    {
        var cliAssembly = Path.Combine(AppContext.BaseDirectory, "avascope.dll");
        Assert.True(File.Exists(cliAssembly), $"Expected CLI assembly at {cliAssembly}.");

        var result = await RunCliAsync(cliAssembly, command, "--session", "missing");

        Assert.Equal(2, result.ExitCode);
        Assert.True(string.IsNullOrWhiteSpace(result.StandardError), result.StandardError);

        var payload = JsonSerializer.Deserialize<ToolResult<TreeResponse>>(result.StandardOutput, JsonOptions);
        Assert.NotNull(payload);
        Assert.False(payload.Success);
        Assert.Equal("invalid_cli_arguments", payload.Error!.Code);
    }

    [Fact]
    public async Task VisualTreeCommandRejectsInvalidMaxDepth()
    {
        var cliAssembly = Path.Combine(AppContext.BaseDirectory, "avascope.dll");
        Assert.True(File.Exists(cliAssembly), $"Expected CLI assembly at {cliAssembly}.");

        var result = await RunCliAsync(
            cliAssembly,
            "visual-tree",
            "--session",
            "missing",
            "--top-level",
            "topLevel:missing",
            "--max-depth",
            "-1");

        Assert.Equal(2, result.ExitCode);
        Assert.True(string.IsNullOrWhiteSpace(result.StandardError), result.StandardError);

        var payload = JsonSerializer.Deserialize<ToolResult<TreeResponse>>(result.StandardOutput, JsonOptions);
        Assert.NotNull(payload);
        Assert.False(payload.Success);
        Assert.Equal("invalid_cli_arguments", payload.Error!.Code);
    }

    [Theory]
    [InlineData(TreeKinds.Visual)]
    [InlineData(TreeKinds.Logical)]
    public async Task InspectNodeCommandReadsNodeThroughBridgePipe(string treeKind)
    {
        var cliAssembly = Path.Combine(AppContext.BaseDirectory, "avascope.dll");
        Assert.True(File.Exists(cliAssembly), $"Expected CLI assembly at {cliAssembly}.");

        var sessionId = SessionId.New();
        var pipeName = TestPipeNames.New();
        var manifestDirectory = Path.Combine(
            Path.GetTempPath(),
            "AvaScope.Tests",
            $"cli-inspect-node-manifests-{Guid.NewGuid():N}");
        var manifestPath = WriteBridgeManifest(sessionId, pipeName, manifestDirectory);
        var nodeId = $"{treeKind}:child";

        var serverTask = RespondToBridgeRequestAsync(pipeName, request =>
        {
            Assert.Equal(BridgeIpcMethods.InspectNode, request.Method);
            Assert.Equal("topLevel:cli", request.TopLevelId);
            Assert.Equal(treeKind, request.TreeKind);
            Assert.Equal(nodeId, request.NodeId);
            return BridgeIpcResponse.Ok(
                request.RequestId,
                new InspectNodeResponse(
                    sessionId,
                    request.TopLevelId!,
                    treeKind,
                    request.NodeId!,
                    "TextBlock",
                    childCount: 0,
                    name: "CliText",
                    text: "CLI node"));
        });

        try
        {
            var result = await RunCliAsync(
                cliAssembly,
                "inspect-node",
                "--session",
                sessionId.Value,
                "--top-level",
                "topLevel:cli",
                "--node",
                nodeId,
                "--tree-kind",
                treeKind,
                "--manifest-dir",
                manifestDirectory);
            var request = await serverTask;

            Assert.Equal(0, result.ExitCode);
            Assert.Equal(BridgeIpcMethods.InspectNode, request.Method);
            Assert.True(string.IsNullOrWhiteSpace(result.StandardError), result.StandardError);

            var payload = JsonSerializer.Deserialize<ToolResult<InspectNodeResponse>>(result.StandardOutput, JsonOptions);
            Assert.NotNull(payload);
            Assert.True(payload.Success, payload.Error?.Message);
            Assert.Equal(sessionId, payload.Value!.SessionId);
            Assert.Equal(treeKind, payload.Value.TreeKind);
            Assert.Equal(nodeId, payload.Value.NodeId);
            Assert.Equal("TextBlock", payload.Value.NodeType);
            Assert.Equal("CLI node", payload.Value.Text);
        }
        finally
        {
            if (File.Exists(manifestPath))
            {
                File.Delete(manifestPath);
            }

            if (Directory.Exists(manifestDirectory))
            {
                Directory.Delete(manifestDirectory, recursive: true);
            }
        }
    }

    [Fact]
    public async Task InspectNodeCommandDefaultsTreeKindToVisual()
    {
        var cliAssembly = Path.Combine(AppContext.BaseDirectory, "avascope.dll");
        Assert.True(File.Exists(cliAssembly), $"Expected CLI assembly at {cliAssembly}.");

        var sessionId = SessionId.New();
        var pipeName = TestPipeNames.New();
        var manifestPath = WriteBridgeManifest(sessionId, pipeName);

        var serverTask = RespondToBridgeRequestAsync(pipeName, request =>
        {
            Assert.Equal(BridgeIpcMethods.InspectNode, request.Method);
            Assert.Equal(TreeKinds.Visual, request.TreeKind);
            return BridgeIpcResponse.Ok(
                request.RequestId,
                new InspectNodeResponse(
                    sessionId,
                    request.TopLevelId!,
                    TreeKinds.Visual,
                    request.NodeId!,
                    "Window",
                    childCount: 1));
        });

        try
        {
            var result = await RunCliAsync(
                cliAssembly,
                "inspect-node",
                "--session",
                sessionId.Value,
                "--top-level",
                "topLevel:cli",
                "--node",
                "visual:root");
            var request = await serverTask;

            Assert.Equal(0, result.ExitCode);
            Assert.Equal(TreeKinds.Visual, request.TreeKind);

            var payload = JsonSerializer.Deserialize<ToolResult<InspectNodeResponse>>(result.StandardOutput, JsonOptions);
            Assert.NotNull(payload);
            Assert.True(payload.Success, payload.Error?.Message);
            Assert.Equal(TreeKinds.Visual, payload.Value!.TreeKind);
        }
        finally
        {
            if (File.Exists(manifestPath))
            {
                File.Delete(manifestPath);
            }
        }
    }

    [Fact]
    public async Task InspectNodeCommandReturnsStructuredErrorWhenNoBridgeSessionMatches()
    {
        var cliAssembly = Path.Combine(AppContext.BaseDirectory, "avascope.dll");
        Assert.True(File.Exists(cliAssembly), $"Expected CLI assembly at {cliAssembly}.");

        var result = await RunCliAsync(
            cliAssembly,
            "inspect-node",
            "--session",
            "missing",
            "--top-level",
            "topLevel:missing",
            "--node",
            "visual:missing");

        Assert.Equal(1, result.ExitCode);
        Assert.True(string.IsNullOrWhiteSpace(result.StandardError), result.StandardError);

        var payload = JsonSerializer.Deserialize<ToolResult<InspectNodeResponse>>(result.StandardOutput, JsonOptions);
        Assert.NotNull(payload);
        Assert.False(payload.Success);
        Assert.Equal(CoreErrorCodes.BridgeSessionNotFound, payload.Error!.Code);
    }

    [Theory]
    [InlineData("--session", "missing", "--top-level", "topLevel:missing")]
    [InlineData("--session", "missing", "--node", "visual:missing")]
    [InlineData("--top-level", "topLevel:missing", "--node", "visual:missing")]
    public async Task InspectNodeCommandRejectsMissingRequiredArguments(params string[] arguments)
    {
        var cliAssembly = Path.Combine(AppContext.BaseDirectory, "avascope.dll");
        Assert.True(File.Exists(cliAssembly), $"Expected CLI assembly at {cliAssembly}.");

        var commandArguments = new[] { "inspect-node" }.Concat(arguments).ToArray();
        var result = await RunCliAsync(cliAssembly, commandArguments);

        Assert.Equal(2, result.ExitCode);
        Assert.True(string.IsNullOrWhiteSpace(result.StandardError), result.StandardError);

        var payload = JsonSerializer.Deserialize<ToolResult<InspectNodeResponse>>(result.StandardOutput, JsonOptions);
        Assert.NotNull(payload);
        Assert.False(payload.Success);
        Assert.Equal("invalid_cli_arguments", payload.Error!.Code);
    }

    [Fact]
    public async Task InspectNodeCommandRejectsInvalidTreeKind()
    {
        var cliAssembly = Path.Combine(AppContext.BaseDirectory, "avascope.dll");
        Assert.True(File.Exists(cliAssembly), $"Expected CLI assembly at {cliAssembly}.");

        var result = await RunCliAsync(
            cliAssembly,
            "inspect-node",
            "--session",
            "missing",
            "--top-level",
            "topLevel:missing",
            "--node",
            "visual:missing",
            "--tree-kind",
            "layout");

        Assert.Equal(2, result.ExitCode);
        Assert.True(string.IsNullOrWhiteSpace(result.StandardError), result.StandardError);

        var payload = JsonSerializer.Deserialize<ToolResult<InspectNodeResponse>>(result.StandardOutput, JsonOptions);
        Assert.NotNull(payload);
        Assert.False(payload.Success);
        Assert.Equal("invalid_cli_arguments", payload.Error!.Code);
    }

    [Fact]
    public async Task FindNodesCommandReadsMatchesThroughBridgePipe()
    {
        var cliAssembly = Path.Combine(AppContext.BaseDirectory, "avascope.dll");
        Assert.True(File.Exists(cliAssembly), $"Expected CLI assembly at {cliAssembly}.");

        var sessionId = SessionId.New();
        var pipeName = TestPipeNames.New();
        var manifestPath = WriteBridgeManifest(sessionId, pipeName);
        var matchNode = new TreeNodeSummary(
            "logical:match",
            "TextBlock",
            "SearchTarget",
            "search-target",
            "Find me",
            target: new RuntimeTargetContext(sessionId, "topLevel:cli", TreeKinds.Logical, "logical:match"));

        var serverTask = RespondToBridgeRequestAsync(pipeName, request =>
        {
            Assert.Equal(BridgeIpcMethods.FindNodes, request.Method);
            Assert.Equal("topLevel:cli", request.TopLevelId);
            Assert.Equal(TreeKinds.Logical, request.TreeKind);
            Assert.Equal("TextBlock", request.NodeType);
            Assert.Equal("SearchTarget", request.Name);
            Assert.Equal("search-target", request.AutomationId);
            Assert.Equal("Find me", request.Text);
            Assert.True(request.Visible);
            Assert.True(request.Enabled);
            Assert.True(request.Rendered);
            Assert.True(request.Actionable);
            Assert.Equal(3, request.MaxDepth);
            Assert.Equal(5, request.MaxResults);
            return BridgeIpcResponse.Ok(
                request.RequestId,
                new FindNodesResponse(
                    sessionId,
                    request.TopLevelId!,
                    TreeKinds.Logical,
                    3,
                    [new FindNodeMatch(matchNode, ["logical:root", "logical:match"])]));
        });

        try
        {
            var result = await RunCliAsync(
                cliAssembly,
                "find-nodes",
                "--session",
                sessionId.Value,
                "--top-level",
                "topLevel:cli",
                "--tree-kind",
                TreeKinds.Logical,
                "--type",
                "TextBlock",
                "--name",
                "SearchTarget",
                "--automation-id",
                "search-target",
                "--text",
                "Find me",
                "--visible",
                "true",
                "--enabled",
                "true",
                "--rendered",
                "true",
                "--actionable",
                "true",
                "--max-depth",
                "3",
                "--max-results",
                "5");
            var request = await serverTask;

            Assert.Equal(0, result.ExitCode);
            Assert.Equal(BridgeIpcMethods.FindNodes, request.Method);
            Assert.True(string.IsNullOrWhiteSpace(result.StandardError), result.StandardError);

            var payload = JsonSerializer.Deserialize<ToolResult<FindNodesResponse>>(result.StandardOutput, JsonOptions);
            Assert.NotNull(payload);
            Assert.True(payload.Success, payload.Error?.Message);
            Assert.Equal(sessionId, payload.Value!.SessionId);
            Assert.Equal(TreeKinds.Logical, payload.Value.TreeKind);
            Assert.Equal(sessionId, payload.Value.Target.SessionId);
            Assert.Equal("topLevel:cli", payload.Value.Target.TopLevelId);
            Assert.Equal(TreeKinds.Logical, payload.Value.Target.TreeKind);
            var match = Assert.Single(payload.Value.Matches);
            Assert.Equal("logical:match", match.Node.NodeId);
            Assert.Equal(sessionId, match.Target!.SessionId);
            Assert.Equal("topLevel:cli", match.Target.TopLevelId);
            Assert.Equal(TreeKinds.Logical, match.Target.TreeKind);
            Assert.Equal("logical:match", match.Target.NodeId);
            Assert.Equal("SearchTarget", match.Node.Name);
            Assert.Equal(new[] { "logical:root", "logical:match" }, match.Path);
        }
        finally
        {
            if (File.Exists(manifestPath))
            {
                File.Delete(manifestPath);
            }
        }
    }

    [Fact]
    public async Task FindNodesCommandReturnsStructuredErrorWhenNoBridgeSessionMatches()
    {
        var cliAssembly = Path.Combine(AppContext.BaseDirectory, "avascope.dll");
        Assert.True(File.Exists(cliAssembly), $"Expected CLI assembly at {cliAssembly}.");

        var result = await RunCliAsync(
            cliAssembly,
            "find-nodes",
            "--session",
            "missing",
            "--top-level",
            "topLevel:missing",
            "--type",
            "TextBlock");

        Assert.Equal(1, result.ExitCode);
        Assert.True(string.IsNullOrWhiteSpace(result.StandardError), result.StandardError);

        var payload = JsonSerializer.Deserialize<ToolResult<FindNodesResponse>>(result.StandardOutput, JsonOptions);
        Assert.NotNull(payload);
        Assert.False(payload.Success);
        Assert.Equal(CoreErrorCodes.BridgeSessionNotFound, payload.Error!.Code);
    }

    [Fact]
    public async Task AuditUiCommandBuildsBoundedReportFromVisualTreeThroughBridgePipe()
    {
        var cliAssembly = Path.Combine(AppContext.BaseDirectory, "avascope.dll");
        Assert.True(File.Exists(cliAssembly), $"Expected CLI assembly at {cliAssembly}.");

        var sessionId = SessionId.New();
        var pipeName = TestPipeNames.New();
        var manifestPath = WriteBridgeManifest(sessionId, pipeName);
        var buttonTarget = new RuntimeTargetContext(sessionId, "topLevel:cli", TreeKinds.Visual, "visual:button");
        var tree = new TreeResponse(
            sessionId,
            "topLevel:cli",
            TreeKinds.Visual,
            4,
            new TreeNodeSummary(
                "visual:root",
                "Avalonia.Controls.Window",
                children:
                [
                    new TreeNodeSummary(
                        "visual:button",
                        "Avalonia.Controls.Button",
                        target: buttonTarget,
                        accessibilityState: new RuntimeAccessibilityState(
                            "test",
                            focusable: true,
                            isTabStop: true)),
                    new TreeNodeSummary(
                        "visual:textbox",
                        "Avalonia.Controls.TextBox",
                        automationId: "EmailInput",
                        target: new RuntimeTargetContext(sessionId, "topLevel:cli", TreeKinds.Visual, "visual:textbox"),
                        accessibilityState: new RuntimeAccessibilityState(
                            "test",
                            automationName: "Email",
                            focusable: true,
                            isTabStop: true),
                        validationState: new RuntimeValidationState(
                            "has_errors",
                            "test",
                            hasErrors: true,
                            errorCount: 1,
                            errors: ["Email is required"]))
                ]),
            new RuntimeTargetContext(sessionId, "topLevel:cli", TreeKinds.Visual));

        var serverTask = RespondToBridgeRequestAsync(pipeName, request =>
        {
            Assert.Equal(BridgeIpcMethods.VisualTree, request.Method);
            Assert.Equal("topLevel:cli", request.TopLevelId);
            Assert.Equal(4, request.MaxDepth);
            return BridgeIpcResponse.Ok(request.RequestId, tree);
        });

        try
        {
            var result = await RunCliAsync(
                cliAssembly,
                "audit-ui",
                "--session",
                sessionId.Value,
                "--top-level",
                "topLevel:cli",
                "--max-depth",
                "4",
                "--max-issues",
                "2",
                "--max-inventory",
                "3");
            var request = await serverTask;

            Assert.Equal(0, result.ExitCode);
            Assert.Equal(BridgeIpcMethods.VisualTree, request.Method);
            Assert.True(string.IsNullOrWhiteSpace(result.StandardError), result.StandardError);

            var payload = JsonSerializer.Deserialize<ToolResult<UiAuditResponse>>(result.StandardOutput, JsonOptions);
            Assert.NotNull(payload);
            Assert.True(payload.Success, payload.Error?.Message);
            Assert.Equal(sessionId, payload.Value!.SessionId);
            Assert.Equal("topLevel:cli", payload.Value.TopLevelId);
            Assert.Equal(TreeKinds.Visual, payload.Value.TreeKind);
            Assert.Equal(3, payload.Value.Summary.TotalNodes);
            Assert.Equal(2, payload.Value.Summary.ActionableNodes);
            Assert.Equal(3, payload.Value.Summary.IssueCount);
            Assert.Equal(2, payload.Value.Issues.Count);
            Assert.True(payload.Value.Summary.Truncated);
            Assert.Contains(payload.Value.Issues, issue => issue.Code == "accessibility.missing_accessible_name");
            Assert.Contains(payload.Value.Issues, issue => issue.Code == "accessibility.missing_automation_id");
            Assert.Contains(payload.Value.Inventory, item => item.Category == "control" && item.Name == "Button");
            Assert.Equal("issues_found", payload.Value.AgentReview.Status);
            Assert.Contains("issues: 3", payload.Value.AgentReview.Summary, StringComparer.Ordinal);
        }
        finally
        {
            if (File.Exists(manifestPath))
            {
                File.Delete(manifestPath);
            }
        }
    }

    [Fact]
    public async Task DesignAuditCommandBuildsScopedReportFromVisualTreeThroughBridgePipe()
    {
        var cliAssembly = Path.Combine(AppContext.BaseDirectory, "avascope.dll");
        Assert.True(File.Exists(cliAssembly), $"Expected CLI assembly at {cliAssembly}.");

        var sessionId = SessionId.New();
        var pipeName = TestPipeNames.New();
        var manifestPath = WriteBridgeManifest(sessionId, pipeName);
        var requestPath = Path.Combine(Path.GetTempPath(), $"avascope-design-audit-{Guid.NewGuid():N}.json");
        var tree = new TreeResponse(
            sessionId,
            "topLevel:cli",
            TreeKinds.Visual,
            6,
            new TreeNodeSummary(
                "visual:root",
                "Avalonia.Controls.Window",
                bounds: new NodeBounds(0, 0, 240, 160),
                sourceMap: TestSourceMap("Background", "#FFFFFFFF"),
                children:
                [
                    new TreeNodeSummary(
                        "visual:toolbar",
                        "Avalonia.Controls.StackPanel",
                        name: "Toolbar",
                        bounds: new NodeBounds(0, 0, 120, 44),
                        sourceMap: TestSourceMap("Background", "#FFFFFFFF"),
                        children:
                        [
                            new TreeNodeSummary(
                                "visual:icon",
                                "Avalonia.Controls.PathIcon",
                                name: "SearchIcon",
                                bounds: new NodeBounds(2, 2, 16, 16),
                                classes: ["icon"],
                                sourceMap: TestSourceMap("Foreground", "#FF202020"))
                        ])
                ]),
            new RuntimeTargetContext(sessionId, "topLevel:cli", TreeKinds.Visual));
        var request = new DesignQualityAuditRequest(
            sessionId,
            "topLevel:cli",
            requestId: "cli-design-audit",
            scopeName: "Toolbar",
            maxDepth: 6);

        File.WriteAllText(requestPath, JsonSerializer.Serialize(request, JsonOptions));
        var serverTask = RespondToBridgeRequestAsync(pipeName, bridgeRequest =>
        {
            Assert.Equal(BridgeIpcMethods.VisualTree, bridgeRequest.Method);
            Assert.Equal("topLevel:cli", bridgeRequest.TopLevelId);
            Assert.Equal(6, bridgeRequest.MaxDepth);
            return BridgeIpcResponse.Ok(bridgeRequest.RequestId, tree);
        });

        try
        {
            var result = await RunCliAsync(
                cliAssembly,
                "design-audit",
                "--request",
                requestPath);
            var bridgeRequest = await serverTask;

            Assert.Equal(1, result.ExitCode);
            Assert.Equal(BridgeIpcMethods.VisualTree, bridgeRequest.Method);
            Assert.True(string.IsNullOrWhiteSpace(result.StandardError), result.StandardError);

            var payload = JsonSerializer.Deserialize<ToolResult<DesignQualityAuditResponse>>(result.StandardOutput, JsonOptions);
            Assert.NotNull(payload);
            Assert.True(payload.Success, payload.Error?.Message);
            Assert.Equal("cli-design-audit", payload.Value!.RequestId);
            Assert.Equal("issues_found", payload.Value.Summary.Status);
            Assert.Contains(payload.Value.Findings, finding => finding.Code == "design.alignment.icon_center_mismatch");
            Assert.Equal("scoped", payload.Value.Summary.ScopeStatus);
        }
        finally
        {
            if (File.Exists(manifestPath))
            {
                File.Delete(manifestPath);
            }

            if (File.Exists(requestPath))
            {
                File.Delete(requestPath);
            }
        }
    }

    [Fact]
    public async Task FindNodesCommandRejectsMissingFilters()
    {
        var cliAssembly = Path.Combine(AppContext.BaseDirectory, "avascope.dll");
        Assert.True(File.Exists(cliAssembly), $"Expected CLI assembly at {cliAssembly}.");

        var result = await RunCliAsync(
            cliAssembly,
            "find-nodes",
            "--session",
            "missing",
            "--top-level",
            "topLevel:missing");

        Assert.Equal(2, result.ExitCode);
        Assert.True(string.IsNullOrWhiteSpace(result.StandardError), result.StandardError);

        var payload = JsonSerializer.Deserialize<ToolResult<FindNodesResponse>>(result.StandardOutput, JsonOptions);
        Assert.NotNull(payload);
        Assert.False(payload.Success);
        Assert.Equal("invalid_cli_arguments", payload.Error!.Code);
    }

    [Theory]
    [InlineData("--max-depth", "-1")]
    [InlineData("--max-results", "0")]
    [InlineData("--tree-kind", "layout")]
    [InlineData("--actionable", "sometimes")]
    public async Task FindNodesCommandRejectsInvalidOptions(string optionName, string optionValue)
    {
        var cliAssembly = Path.Combine(AppContext.BaseDirectory, "avascope.dll");
        Assert.True(File.Exists(cliAssembly), $"Expected CLI assembly at {cliAssembly}.");

        var result = await RunCliAsync(
            cliAssembly,
            "find-nodes",
            "--session",
            "missing",
            "--top-level",
            "topLevel:missing",
            "--type",
            "TextBlock",
            optionName,
            optionValue);

        Assert.Equal(2, result.ExitCode);
        Assert.True(string.IsNullOrWhiteSpace(result.StandardError), result.StandardError);

        var payload = JsonSerializer.Deserialize<ToolResult<FindNodesResponse>>(result.StandardOutput, JsonOptions);
        Assert.NotNull(payload);
        Assert.False(payload.Success);
        Assert.Equal("invalid_cli_arguments", payload.Error!.Code);
    }

    [Fact]
    public async Task InputCommandSendsClickThroughBridgePipe()
    {
        var cliAssembly = Path.Combine(AppContext.BaseDirectory, "avascope.dll");
        Assert.True(File.Exists(cliAssembly), $"Expected CLI assembly at {cliAssembly}.");

        var sessionId = SessionId.New();
        var pipeName = TestPipeNames.New();
        var manifestPath = WriteBridgeManifest(sessionId, pipeName);

        var serverTask = RespondToBridgeRequestAsync(pipeName, request =>
        {
            Assert.Equal(BridgeIpcMethods.Input, request.Method);
            Assert.Equal("topLevel:cli", request.TopLevelId);
            Assert.Equal(InputActions.Click, request.Action);
            Assert.Equal(12.5, request.X);
            Assert.Equal(34.25, request.Y);
            return BridgeIpcResponse.Ok(
                request.RequestId,
                new InputResponse(sessionId, request.TopLevelId!, InputActions.Click, true, DateTimeOffset.UtcNow, "visual:button"));
        });

        try
        {
            var result = await RunCliAsync(
                cliAssembly,
                "input",
                "--session",
                sessionId.Value,
                "--top-level",
                "topLevel:cli",
                "--action",
                "CLICK",
                "--x",
                "12.5",
                "--y",
                "34.25");
            var request = await serverTask;

            Assert.Equal(0, result.ExitCode);
            Assert.Equal(InputActions.Click, request.Action);
            Assert.True(string.IsNullOrWhiteSpace(result.StandardError), result.StandardError);

            var payload = JsonSerializer.Deserialize<ToolResult<InputResponse>>(result.StandardOutput, JsonOptions);
            Assert.NotNull(payload);
            Assert.True(payload.Success, payload.Error?.Message);
            Assert.True(payload.Value!.Handled);
            Assert.Equal("visual:button", payload.Value.TargetNodeId);
            Assert.Equal(sessionId, payload.Value.Target.SessionId);
            Assert.Equal("topLevel:cli", payload.Value.Target.TopLevelId);
            Assert.Equal(TreeKinds.Visual, payload.Value.Target.TreeKind);
            Assert.Equal("visual:button", payload.Value.Target.NodeId);
        }
        finally
        {
            if (File.Exists(manifestPath))
            {
                File.Delete(manifestPath);
            }
        }
    }

    [Fact]
    public async Task InputCommandSendsTargetOnlyClickThroughBridgePipe()
    {
        var cliAssembly = Path.Combine(AppContext.BaseDirectory, "avascope.dll");
        Assert.True(File.Exists(cliAssembly), $"Expected CLI assembly at {cliAssembly}.");

        var sessionId = SessionId.New();
        var pipeName = TestPipeNames.New();
        var manifestPath = WriteBridgeManifest(sessionId, pipeName);

        var serverTask = RespondToBridgeRequestAsync(pipeName, request =>
        {
            Assert.Equal(BridgeIpcMethods.Input, request.Method);
            Assert.Equal(InputActions.Click, request.Action);
            Assert.Null(request.X);
            Assert.Null(request.Y);
            Assert.Equal("visual:deploy", request.TargetNodeId);
            return BridgeIpcResponse.Ok(
                request.RequestId,
                new InputResponse(
                    sessionId,
                    request.TopLevelId!,
                    InputActions.Click,
                    true,
                    DateTimeOffset.UtcNow,
                    request.TargetNodeId,
                    metadata: new Dictionary<string, string>
                    {
                        ["coordinateSource"] = "target_center"
                    }));
        });

        try
        {
            var result = await RunCliAsync(
                cliAssembly,
                "input",
                "--session",
                sessionId.Value,
                "--top-level",
                "topLevel:cli",
                "--action",
                InputActions.Click,
                "--target-node",
                "visual:deploy");
            var request = await serverTask;

            Assert.Equal(0, result.ExitCode);
            Assert.Equal("visual:deploy", request.TargetNodeId);
            var payload = JsonSerializer.Deserialize<ToolResult<InputResponse>>(result.StandardOutput, JsonOptions);
            Assert.NotNull(payload);
            Assert.True(payload.Success, payload.Error?.Message);
            Assert.Equal("target_center", payload.Value!.Metadata["coordinateSource"]);
        }
        finally
        {
            if (File.Exists(manifestPath))
            {
                File.Delete(manifestPath);
            }
        }
    }

    [Fact]
    public async Task InputCommandSendsKeyTextThroughBridgePipe()
    {
        var cliAssembly = Path.Combine(AppContext.BaseDirectory, "avascope.dll");
        Assert.True(File.Exists(cliAssembly), $"Expected CLI assembly at {cliAssembly}.");

        var sessionId = SessionId.New();
        var pipeName = TestPipeNames.New();
        var manifestPath = WriteBridgeManifest(sessionId, pipeName);

        var serverTask = RespondToBridgeRequestAsync(pipeName, request =>
        {
            Assert.Equal(BridgeIpcMethods.Input, request.Method);
            Assert.Equal(InputActions.KeyText, request.Action);
            Assert.Equal("typed text", request.InputText);
            Assert.Equal("visual:textbox", request.TargetNodeId);
            return BridgeIpcResponse.Ok(
                request.RequestId,
                new InputResponse(sessionId, request.TopLevelId!, InputActions.KeyText, true, DateTimeOffset.UtcNow, request.TargetNodeId));
        });

        try
        {
            var result = await RunCliAsync(
                cliAssembly,
                "input",
                "--session",
                sessionId.Value,
                "--top-level",
                "topLevel:cli",
                "--action",
                InputActions.KeyText,
                "--text",
                "typed text",
                "--target-node",
                "visual:textbox");
            var request = await serverTask;

            Assert.Equal(0, result.ExitCode);
            Assert.Equal("typed text", request.InputText);
            Assert.Equal("visual:textbox", request.TargetNodeId);

            var payload = JsonSerializer.Deserialize<ToolResult<InputResponse>>(result.StandardOutput, JsonOptions);
            Assert.NotNull(payload);
            Assert.True(payload.Success, payload.Error?.Message);
            Assert.Equal(InputActions.KeyText, payload.Value!.Action);
        }
        finally
        {
            if (File.Exists(manifestPath))
            {
                File.Delete(manifestPath);
            }
        }
    }

    [Fact]
    public async Task InputCommandSendsClearTextThroughBridgePipe()
    {
        var cliAssembly = Path.Combine(AppContext.BaseDirectory, "avascope.dll");
        Assert.True(File.Exists(cliAssembly), $"Expected CLI assembly at {cliAssembly}.");

        var sessionId = SessionId.New();
        var pipeName = TestPipeNames.New();
        var manifestPath = WriteBridgeManifest(sessionId, pipeName);

        var serverTask = RespondToBridgeRequestAsync(pipeName, request =>
        {
            Assert.Equal(BridgeIpcMethods.Input, request.Method);
            Assert.Equal(InputActions.ClearText, request.Action);
            Assert.Equal("visual:textbox", request.TargetNodeId);
            Assert.Null(request.InputText);
            return BridgeIpcResponse.Ok(
                request.RequestId,
                new InputResponse(sessionId, request.TopLevelId!, InputActions.ClearText, true, DateTimeOffset.UtcNow, request.TargetNodeId));
        });

        try
        {
            var result = await RunCliAsync(
                cliAssembly,
                "input",
                "--session",
                sessionId.Value,
                "--top-level",
                "topLevel:cli",
                "--action",
                InputActions.ClearText,
                "--target-node",
                "visual:textbox");
            var request = await serverTask;

            Assert.Equal(0, result.ExitCode);
            Assert.Equal("visual:textbox", request.TargetNodeId);

            var payload = JsonSerializer.Deserialize<ToolResult<InputResponse>>(result.StandardOutput, JsonOptions);
            Assert.NotNull(payload);
            Assert.True(payload.Success, payload.Error?.Message);
            Assert.Equal(InputActions.ClearText, payload.Value!.Action);
            Assert.Equal("visual:textbox", payload.Value.TargetNodeId);
        }
        finally
        {
            if (File.Exists(manifestPath))
            {
                File.Delete(manifestPath);
            }
        }
    }

    [Fact]
    public async Task InputCommandSendsKeyDownThroughBridgePipe()
    {
        var cliAssembly = Path.Combine(AppContext.BaseDirectory, "avascope.dll");
        Assert.True(File.Exists(cliAssembly), $"Expected CLI assembly at {cliAssembly}.");

        var sessionId = SessionId.New();
        var pipeName = TestPipeNames.New();
        var manifestPath = WriteBridgeManifest(sessionId, pipeName);

        var serverTask = RespondToBridgeRequestAsync(pipeName, request =>
        {
            Assert.Equal(BridgeIpcMethods.Input, request.Method);
            Assert.Equal(InputActions.KeyDown, request.Action);
            Assert.Equal("Enter", request.InputKey);
            Assert.Equal("Control+Shift", request.KeyModifiers);
            Assert.Equal("visual:textbox", request.TargetNodeId);
            return BridgeIpcResponse.Ok(
                request.RequestId,
                new InputResponse(sessionId, request.TopLevelId!, InputActions.KeyDown, true, DateTimeOffset.UtcNow, request.TargetNodeId));
        });

        try
        {
            var result = await RunCliAsync(
                cliAssembly,
                "input",
                "--session",
                sessionId.Value,
                "--top-level",
                "topLevel:cli",
                "--action",
                InputActions.KeyDown,
                "--key",
                "Enter",
                "--modifiers",
                "Control+Shift",
                "--target-node",
                "visual:textbox");
            var request = await serverTask;

            Assert.Equal(0, result.ExitCode);
            Assert.Equal("Enter", request.InputKey);

            var payload = JsonSerializer.Deserialize<ToolResult<InputResponse>>(result.StandardOutput, JsonOptions);
            Assert.NotNull(payload);
            Assert.True(payload.Success, payload.Error?.Message);
            Assert.Equal("visual:textbox", payload.Value!.TargetNodeId);
        }
        finally
        {
            if (File.Exists(manifestPath))
            {
                File.Delete(manifestPath);
            }
        }
    }

    [Fact]
    public async Task InputCommandSendsFocusThroughBridgePipe()
    {
        var cliAssembly = Path.Combine(AppContext.BaseDirectory, "avascope.dll");
        Assert.True(File.Exists(cliAssembly), $"Expected CLI assembly at {cliAssembly}.");

        var sessionId = SessionId.New();
        var pipeName = TestPipeNames.New();
        var manifestPath = WriteBridgeManifest(sessionId, pipeName);

        var serverTask = RespondToBridgeRequestAsync(pipeName, request =>
        {
            Assert.Equal(BridgeIpcMethods.Input, request.Method);
            Assert.Equal(InputActions.Focus, request.Action);
            Assert.Equal("visual:textbox", request.TargetNodeId);
            return BridgeIpcResponse.Ok(
                request.RequestId,
                new InputResponse(sessionId, request.TopLevelId!, InputActions.Focus, true, DateTimeOffset.UtcNow, request.TargetNodeId));
        });

        try
        {
            var result = await RunCliAsync(
                cliAssembly,
                "input",
                "--session",
                sessionId.Value,
                "--top-level",
                "topLevel:cli",
                "--action",
                InputActions.Focus,
                "--target-node",
                "visual:textbox");
            var request = await serverTask;

            Assert.Equal(0, result.ExitCode);
            Assert.Equal("visual:textbox", request.TargetNodeId);

            var payload = JsonSerializer.Deserialize<ToolResult<InputResponse>>(result.StandardOutput, JsonOptions);
            Assert.NotNull(payload);
            Assert.True(payload.Success, payload.Error?.Message);
            Assert.Equal(InputActions.Focus, payload.Value!.Action);
        }
        finally
        {
            if (File.Exists(manifestPath))
            {
                File.Delete(manifestPath);
            }
        }
    }

    [Theory]
    [InlineData("select", "1", null, null)]
    [InlineData("select", null, null, null)]
    [InlineData("invoke", null, null, null)]
    [InlineData("toggle", null, null, null)]
    [InlineData("expand", null, null, null)]
    [InlineData("collapse", null, null, null)]
    [InlineData("scroll", null, "0", "40")]
    public async Task InputCommandSendsExpandedInputThroughBridgePipe(
        string action,
        string? text,
        string? x,
        string? y)
    {
        var cliAssembly = Path.Combine(AppContext.BaseDirectory, "avascope.dll");
        Assert.True(File.Exists(cliAssembly), $"Expected CLI assembly at {cliAssembly}.");

        var sessionId = SessionId.New();
        var pipeName = TestPipeNames.New();
        var manifestPath = WriteBridgeManifest(sessionId, pipeName);

        var serverTask = RespondToBridgeRequestAsync(pipeName, request =>
        {
            Assert.Equal(BridgeIpcMethods.Input, request.Method);
            Assert.Equal(action, request.Action);
            Assert.Equal("visual:target", request.TargetNodeId);
            if (text is not null)
            {
                Assert.Equal(text, request.InputText);
            }

            if (x is not null)
            {
                Assert.Equal(double.Parse(x, CultureInfo.InvariantCulture), request.X);
            }

            if (y is not null)
            {
                Assert.Equal(double.Parse(y, CultureInfo.InvariantCulture), request.Y);
            }

            return BridgeIpcResponse.Ok(
                request.RequestId,
                new InputResponse(
                    sessionId,
                    request.TopLevelId!,
                    action,
                    true,
                    DateTimeOffset.UtcNow,
                    request.TargetNodeId,
                    metadata: new Dictionary<string, string>
                    {
                        ["action"] = action
                    }));
        });

        try
        {
            var arguments = new List<string>
            {
                "input",
                "--session",
                sessionId.Value,
                "--top-level",
                "topLevel:cli",
                "--action",
                action,
                "--target-node",
                "visual:target"
            };
            if (text is not null)
            {
                arguments.AddRange(["--text", text]);
            }

            if (x is not null)
            {
                arguments.AddRange(["--x", x]);
            }

            if (y is not null)
            {
                arguments.AddRange(["--y", y]);
            }

            var result = await RunCliAsync(cliAssembly, arguments.ToArray());
            var request = await serverTask;

            Assert.Equal(0, result.ExitCode);
            Assert.Equal(action, request.Action);
            var payload = JsonSerializer.Deserialize<ToolResult<InputResponse>>(result.StandardOutput, JsonOptions);
            Assert.NotNull(payload);
            Assert.True(payload.Success, payload.Error?.Message);
            Assert.Equal(action, payload.Value!.Metadata["action"]);
        }
        finally
        {
            if (File.Exists(manifestPath))
            {
                File.Delete(manifestPath);
            }
        }
    }

    [Theory]
    [InlineData("drag", "right", "75", "320", null)]
    [InlineData("drag", null, null, "300", "visual:destination")]
    [InlineData("swipe", "left", "50", "180", null)]
    [InlineData("long_press", null, null, "600", null)]
    [InlineData("press_and_hold", null, null, "700", null)]
    public async Task InputCommandSendsSemanticGestureThroughBridgePipe(
        string action,
        string? direction,
        string? distancePercentage,
        string durationMs,
        string? destinationTargetNodeId)
    {
        var cliAssembly = Path.Combine(AppContext.BaseDirectory, "avascope.dll");
        Assert.True(File.Exists(cliAssembly), $"Expected CLI assembly at {cliAssembly}.");

        var sessionId = SessionId.New();
        var pipeName = TestPipeNames.New();
        var manifestPath = WriteBridgeManifest(sessionId, pipeName);
        var serverTask = RespondToBridgeRequestAsync(pipeName, request =>
        {
            Assert.Equal(BridgeIpcMethods.Input, request.Method);
            Assert.Equal(action, request.Action);
            Assert.Equal("visual:source", request.TargetNodeId);
            Assert.NotNull(request.Gesture);
            Assert.Equal(direction, request.Gesture.Direction);
            Assert.Equal(
                distancePercentage is null ? null : double.Parse(distancePercentage, CultureInfo.InvariantCulture),
                request.Gesture.DistancePercentage);
            Assert.Equal(int.Parse(durationMs, CultureInfo.InvariantCulture), request.Gesture.DurationMs);
            Assert.Equal(destinationTargetNodeId, request.Gesture.DestinationTargetNodeId);
            return BridgeIpcResponse.Ok(
                request.RequestId,
                new InputResponse(
                    sessionId,
                    request.TopLevelId!,
                    action,
                    handled: true,
                    DateTimeOffset.UtcNow,
                    request.TargetNodeId));
        });

        try
        {
            var arguments = new List<string>
            {
                "input",
                "--session",
                sessionId.Value,
                "--top-level",
                "topLevel:cli",
                "--action",
                action,
                "--target-node",
                "visual:source",
                "--duration-ms",
                durationMs
            };
            if (direction is not null)
            {
                arguments.AddRange(["--direction", direction]);
            }

            if (distancePercentage is not null)
            {
                arguments.AddRange(["--distance-percent", distancePercentage]);
            }

            if (destinationTargetNodeId is not null)
            {
                arguments.AddRange(["--destination-target-node", destinationTargetNodeId]);
            }

            var result = await RunCliAsync(cliAssembly, arguments.ToArray());
            await serverTask;

            Assert.Equal(0, result.ExitCode);
            var payload = JsonSerializer.Deserialize<ToolResult<InputResponse>>(result.StandardOutput, JsonOptions);
            Assert.True(payload!.Success, payload.Error?.Message);
            Assert.Equal(action, payload.Value!.Action);
        }
        finally
        {
            if (File.Exists(manifestPath))
            {
                File.Delete(manifestPath);
            }
        }
    }

    [Fact]
    public async Task InputCommandReturnsStructuredErrorWhenNoBridgeSessionMatches()
    {
        var cliAssembly = Path.Combine(AppContext.BaseDirectory, "avascope.dll");
        Assert.True(File.Exists(cliAssembly), $"Expected CLI assembly at {cliAssembly}.");

        var result = await RunCliAsync(
            cliAssembly,
            "input",
            "--session",
            "missing",
            "--top-level",
            "topLevel:missing",
            "--action",
            InputActions.Click,
            "--x",
            "1",
            "--y",
            "2");

        Assert.Equal(1, result.ExitCode);
        Assert.True(string.IsNullOrWhiteSpace(result.StandardError), result.StandardError);

        var payload = JsonSerializer.Deserialize<ToolResult<InputResponse>>(result.StandardOutput, JsonOptions);
        Assert.NotNull(payload);
        Assert.False(payload.Success);
        Assert.Equal(CoreErrorCodes.BridgeSessionNotFound, payload.Error!.Code);
    }

    [Theory]
    [InlineData("click", "--x", "1")]
    [InlineData("click", "--target-node", "")]
    [InlineData("key_text", "--target-node", "visual:textbox")]
    [InlineData("key_down", "--target-node", "visual:textbox")]
    [InlineData("focus", "--text", "ignored")]
    public async Task InputCommandRejectsMissingActionSpecificArguments(
        string action,
        string optionName,
        string optionValue)
    {
        var cliAssembly = Path.Combine(AppContext.BaseDirectory, "avascope.dll");
        Assert.True(File.Exists(cliAssembly), $"Expected CLI assembly at {cliAssembly}.");

        var result = await RunCliAsync(
            cliAssembly,
            "input",
            "--session",
            "missing",
            "--top-level",
            "topLevel:missing",
            "--action",
            action,
            optionName,
            optionValue);

        Assert.Equal(2, result.ExitCode);
        Assert.True(string.IsNullOrWhiteSpace(result.StandardError), result.StandardError);

        var payload = JsonSerializer.Deserialize<ToolResult<InputResponse>>(result.StandardOutput, JsonOptions);
        Assert.NotNull(payload);
        Assert.False(payload.Success);
        Assert.Equal("invalid_cli_arguments", payload.Error!.Code);
    }

    [Theory]
    [InlineData("--action", "unknown")]
    [InlineData("--x", "NaN")]
    public async Task InputCommandRejectsInvalidOptions(string optionName, string optionValue)
    {
        var cliAssembly = Path.Combine(AppContext.BaseDirectory, "avascope.dll");
        Assert.True(File.Exists(cliAssembly), $"Expected CLI assembly at {cliAssembly}.");

        var result = await RunCliAsync(
            cliAssembly,
            "input",
            "--session",
            "missing",
            "--top-level",
            "topLevel:missing",
            "--action",
            InputActions.Click,
            "--x",
            "1",
            "--y",
            "2",
            optionName,
            optionValue);

        Assert.Equal(2, result.ExitCode);
        Assert.True(string.IsNullOrWhiteSpace(result.StandardError), result.StandardError);

        var payload = JsonSerializer.Deserialize<ToolResult<InputResponse>>(result.StandardOutput, JsonOptions);
        Assert.NotNull(payload);
        Assert.False(payload.Success);
        Assert.Equal("invalid_cli_arguments", payload.Error!.Code);
    }

    [Theory]
    [InlineData("drag", "--direction", "diagonal")]
    [InlineData("swipe", "--distance-percent", "50")]
    [InlineData("long_press", "--direction", "right")]
    [InlineData("press_and_hold", "--destination-target-node", "visual:destination")]
    public async Task InputCommandRejectsInvalidGestureOptions(
        string action,
        string optionName,
        string optionValue)
    {
        var cliAssembly = Path.Combine(AppContext.BaseDirectory, "avascope.dll");
        Assert.True(File.Exists(cliAssembly), $"Expected CLI assembly at {cliAssembly}.");

        var result = await RunCliAsync(
            cliAssembly,
            "input",
            "--session",
            "missing",
            "--top-level",
            "topLevel:missing",
            "--action",
            action,
            "--target-node",
            "visual:source",
            optionName,
            optionValue);

        Assert.Equal(2, result.ExitCode);
        Assert.True(string.IsNullOrWhiteSpace(result.StandardError), result.StandardError);
        var payload = JsonSerializer.Deserialize<ToolResult<InputResponse>>(result.StandardOutput, JsonOptions);
        Assert.False(payload!.Success);
        Assert.Equal("invalid_cli_arguments", payload.Error!.Code);
    }

    [Fact]
    public async Task RunScenarioCommandRunsAttachedWorkflowAndWritesTimeline()
    {
        var cliAssembly = Path.Combine(AppContext.BaseDirectory, "avascope.dll");
        Assert.True(File.Exists(cliAssembly), $"Expected CLI assembly at {cliAssembly}.");

        var sessionId = SessionId.New();
        var pipeName = TestPipeNames.New();
        var manifestPath = WriteBridgeManifest(sessionId, pipeName);
        var artifactDirectory = Path.Combine(
            Path.GetTempPath(),
            "AvaScope.Tests",
            $"cli-scenario-{Guid.NewGuid():N}");
        var requestPath = Path.Combine(artifactDirectory, "scenario.json");
        var timelinePath = Path.Combine(artifactDirectory, "timeline.md");
        Directory.CreateDirectory(artifactDirectory);

        var target = new RuntimeTargetContext(sessionId, "topLevel:scenario", TreeKinds.Visual, "visual:save");
        var serverTask = RespondToBridgeRequestsAsync(
            pipeName,
            expectedCount: 4,
            (index, request) =>
            {
                return index switch
                {
                    0 => BridgeIpcResponse.Ok(request.RequestId, HealthResponse.Current()),
                    1 => CreateScenarioFindNodesResponse(request, sessionId, target, automationId: "save-button", text: "Save"),
                    2 => CreateScenarioInputResponse(request, sessionId, target, InputActions.Click),
                    3 => CreateCliEvidenceScreenshotResponse(
                        request,
                        sessionId,
                        "topLevel:scenario",
                        "02-click-save.png"),
                    _ => throw new InvalidOperationException("Unexpected scenario bridge request index.")
                };
            });

        var scenario = new RuntimeScenarioRequest(
            [
                new SemanticWorkflowStep(
                    SemanticWorkflowActions.Click,
                    "click-save",
                    new SemanticWorkflowSelector(automationId: "save-button"))
            ],
            requestId: "cli-scenario",
            sessionId: sessionId,
            topLevelId: "topLevel:scenario",
            outputDirectory: artifactDirectory,
            captureAfterEachStep: true,
            timelinePath: timelinePath,
            evidence: new SemanticWorkflowEvidenceOptions(
                reportDirectory: Path.Combine(artifactDirectory, "reports")));
        await File.WriteAllTextAsync(requestPath, JsonSerializer.Serialize(scenario, JsonOptions));

        try
        {
            var result = await RunCliAsync(
                cliAssembly,
                "run-scenario",
                "--request",
                requestPath);
            var requests = await serverTask;

            Assert.Equal(0, result.ExitCode);
            Assert.True(string.IsNullOrWhiteSpace(result.StandardError), result.StandardError);
            Assert.Equal(BridgeIpcMethods.Health, requests[0].Method);
            Assert.Equal(BridgeIpcMethods.FindNodes, requests[1].Method);
            Assert.Equal(BridgeIpcMethods.Input, requests[2].Method);
            Assert.Equal(BridgeIpcMethods.Screenshot, requests[3].Method);

            var payload = JsonSerializer.Deserialize<ToolResult<RuntimeScenarioResponse>>(result.StandardOutput, JsonOptions);
            Assert.NotNull(payload);
            Assert.True(payload.Success, payload.Error?.Message);
            Assert.Equal("passed", payload.Value!.Status);
            Assert.Equal("session", payload.Value.Metadata["scenarioMode"]);
            Assert.Equal("not_applicable_existing_session", payload.Value.IsolatedStateStatus);
            Assert.Equal(Path.GetFullPath(timelinePath), payload.Value.TimelinePath);
            Assert.True(File.Exists(timelinePath), timelinePath);
            var timeline = await File.ReadAllTextAsync(timelinePath);
            Assert.Contains("Execution path", timeline, StringComparison.Ordinal);
            Assert.Contains("click-save", timeline, StringComparison.Ordinal);
            Assert.Contains("passed", timeline, StringComparison.Ordinal);
            Assert.Equal(2, payload.Value.Workflow!.Steps.Count);
            Assert.Equal("passed", payload.Value.Workflow.ReportPack!.Status);
            Assert.All(payload.Value.Workflow.ReportPack.Assets, asset => Assert.True(File.Exists(asset.Path), asset.Path));
        }
        finally
        {
            if (File.Exists(manifestPath))
            {
                File.Delete(manifestPath);
            }
            await DeleteDirectoryWithRetryAsync(artifactDirectory);
        }
    }

    [Fact]
    public async Task RunScenarioCommandRejectsInvalidCompositionBeforeLaunchOrArtifactCreation()
    {
        var cliAssembly = Path.Combine(AppContext.BaseDirectory, "avascope.dll");
        var requestDirectory = Path.Combine(
            Path.GetTempPath(),
            "AvaScope.Tests",
            $"cli-scenario-validation-{Guid.NewGuid():N}");
        var outputDirectory = Path.Combine(requestDirectory, "must-not-be-created");
        var requestPath = Path.Combine(requestDirectory, "scenario.json");
        Directory.CreateDirectory(requestDirectory);
        var scenario = new RuntimeScenarioRequest(
            [new SemanticWorkflowStep(SemanticWorkflowActions.UseFragment, "missing", fragment: "not-declared")],
            requestId: "invalid-composition",
            launch: new RuntimeScenarioLaunchOptions("definitely-not-a-real-command"),
            outputDirectory: outputDirectory);
        await File.WriteAllTextAsync(requestPath, JsonSerializer.Serialize(scenario, JsonOptions));

        try
        {
            var result = await RunCliAsync(cliAssembly, "run-scenario", "--request", requestPath);

            Assert.Equal(1, result.ExitCode);
            var payload = JsonSerializer.Deserialize<ToolResult<RuntimeScenarioResponse>>(result.StandardOutput, JsonOptions);
            Assert.False(payload!.Success);
            Assert.Equal("semantic_workflow_fragment_unresolved", payload.Error!.Code);
            Assert.Equal("false", payload.Value!.Metadata["dispatchPerformed"]);
            Assert.Null(payload.Value.Launch);
            Assert.Equal("validation_failed", payload.Value.Workflow!.Status);
            Assert.False(Directory.Exists(outputDirectory));
        }
        finally
        {
            await DeleteDirectoryWithRetryAsync(requestDirectory);
        }
    }

    [Fact]
    public async Task PointerDiagnosticsCommandReportsHitPathAndScreenshotArtifacts()
    {
        var cliAssembly = Path.Combine(AppContext.BaseDirectory, "avascope.dll");
        Assert.True(File.Exists(cliAssembly), $"Expected CLI assembly at {cliAssembly}.");

        var sessionId = SessionId.New();
        var pipeName = TestPipeNames.New();
        var manifestPath = WriteBridgeManifest(sessionId, pipeName);
        var artifactDirectory = Path.Combine(
            Path.GetTempPath(),
            "AvaScope.Tests",
            $"cli-pointer-{Guid.NewGuid():N}");
        var requestPath = Path.Combine(artifactDirectory, "pointer.json");
        Directory.CreateDirectory(artifactDirectory);

        var serverTask = RespondToBridgeRequestsAsync(
            pipeName,
            expectedCount: 4,
            (index, request) => index switch
            {
                0 => CreatePointerInputResponse(request, sessionId),
                1 => CreatePointerTreeResponse(request, sessionId, "topLevel:pointer"),
                2 => CreatePointerTreeResponse(request, sessionId, "topLevel:pointer"),
                3 => CreatePointerScreenshotResponse(request, sessionId, "topLevel:pointer", "02-capture.png"),
                _ => throw new InvalidOperationException("Unexpected pointer diagnostics bridge request index.")
            });
        var pointerRequest = new RuntimePointerDiagnosticsRequest(
            sessionId,
            "topLevel:pointer",
            [
                new RuntimePointerPathStep(RuntimePointerPathActions.Move, "move-button", x: 12, y: 8),
                new RuntimePointerPathStep(RuntimePointerPathActions.Screenshot, "capture")
            ],
            requestId: "cli-pointer",
            outputDirectory: artifactDirectory,
            includeAllTopLevels: false);
        await File.WriteAllTextAsync(requestPath, JsonSerializer.Serialize(pointerRequest, JsonOptions));

        try
        {
            var result = await RunCliAsync(
                cliAssembly,
                "pointer-diagnostics",
                "--request",
                requestPath);
            var requests = await serverTask;

            Assert.Equal(0, result.ExitCode);
            Assert.True(string.IsNullOrWhiteSpace(result.StandardError), result.StandardError);
            Assert.Equal(BridgeIpcMethods.Input, requests[0].Method);
            Assert.Equal(BridgeIpcMethods.VisualTree, requests[1].Method);
            Assert.Equal(BridgeIpcMethods.Screenshot, requests[3].Method);

            var payload = JsonSerializer.Deserialize<ToolResult<RuntimePointerDiagnosticsResponse>>(result.StandardOutput, JsonOptions);
            Assert.NotNull(payload);
            Assert.True(payload.Success, payload.Error?.Message);
            Assert.Equal("passed", payload.Value!.Status);
            Assert.Equal("visual:pointerButton", payload.Value.Steps[0].ActiveLayer!.HitTestPath.Last().NodeId);
            var screenshotStep = payload.Value.Steps[1];
            Assert.EndsWith("02-capture.png", screenshotStep.Screenshot!.FilePath, StringComparison.Ordinal);
            Assert.EndsWith("02-capture-pointer-overlay.png", screenshotStep.PointerOverlayPath, StringComparison.Ordinal);
            Assert.True(File.Exists(screenshotStep.PointerOverlayPath), screenshotStep.PointerOverlayPath);
            Assert.Contains(payload.Value.AgentReview.ArtifactPaths, artifact => artifact.Kind == "pointer_overlay");
        }
        finally
        {
            if (File.Exists(manifestPath))
            {
                File.Delete(manifestPath);
            }

            await DeleteDirectoryWithRetryAsync(artifactDirectory);
        }
    }

    [Fact]
    public async Task PseudoStateMatrixCommandCapturesContactSheetThroughBridgePipe()
    {
        var cliAssembly = Path.Combine(AppContext.BaseDirectory, "avascope.dll");
        Assert.True(File.Exists(cliAssembly), $"Expected CLI assembly at {cliAssembly}.");

        var sessionId = SessionId.New();
        var pipeName = TestPipeNames.New();
        var manifestPath = WriteBridgeManifest(sessionId, pipeName);
        var artifactDirectory = Path.Combine(
            Path.GetTempPath(),
            "AvaScope.Tests",
            $"cli-state-{Guid.NewGuid():N}");
        var requestPath = Path.Combine(artifactDirectory, "state-matrix.json");
        var contactSheetPath = Path.Combine(artifactDirectory, "sheet.png");
        Directory.CreateDirectory(artifactDirectory);

        var target = new RuntimeTargetContext(sessionId, "topLevel:pointer", TreeKinds.Visual, "visual:pointerButton");
        var serverTask = RespondToBridgeRequestsAsync(
            pipeName,
            expectedCount: 3,
            (index, request) => index switch
            {
                0 => CreatePointerTreeResponse(request, sessionId, "topLevel:pointer"),
                1 => CreatePointerScreenshotResponse(request, sessionId, "topLevel:pointer", "01-normal.png"),
                2 => CreatePointerTreeResponse(request, sessionId, "topLevel:pointer"),
                _ => throw new InvalidOperationException("Unexpected pseudo-state matrix bridge request index.")
            });
        var matrixRequest = new RuntimePseudoStateMatrixRequest(
            sessionId,
            "topLevel:pointer",
            target,
            [RuntimePseudoStates.Normal],
            requestId: "cli-state",
            outputDirectory: artifactDirectory,
            contactSheetPath: contactSheetPath);
        await File.WriteAllTextAsync(requestPath, JsonSerializer.Serialize(matrixRequest, JsonOptions));

        try
        {
            var result = await RunCliAsync(
                cliAssembly,
                "pseudo-state-matrix",
                "--request",
                requestPath);
            var requests = await serverTask;

            Assert.Equal(0, result.ExitCode);
            Assert.True(string.IsNullOrWhiteSpace(result.StandardError), result.StandardError);
            Assert.Equal(BridgeIpcMethods.VisualTree, requests[0].Method);
            Assert.Equal(BridgeIpcMethods.Screenshot, requests[1].Method);

            var payload = JsonSerializer.Deserialize<ToolResult<RuntimePseudoStateMatrixResponse>>(result.StandardOutput, JsonOptions);
            Assert.NotNull(payload);
            Assert.True(payload.Success, payload.Error?.Message);
            Assert.Equal("passed", payload.Value!.Status);
            Assert.Equal("normal", payload.Value.Entries[0].State);
            Assert.Equal(Path.GetFullPath(contactSheetPath), payload.Value.ContactSheetPath);
            Assert.True(File.Exists(payload.Value.ContactSheetPath), payload.Value.ContactSheetPath);
            Assert.Contains(payload.Value.AgentReview.ArtifactPaths, artifact => artifact.Kind == "contact_sheet");
        }
        finally
        {
            if (File.Exists(manifestPath))
            {
                File.Delete(manifestPath);
            }

            await DeleteDirectoryWithRetryAsync(artifactDirectory);
        }
    }

    [Fact]
    public async Task RecordInteractionAnimationCommandCapturesFrameStripAndAssertionsThroughBridgePipe()
    {
        var cliAssembly = Path.Combine(AppContext.BaseDirectory, "avascope.dll");
        Assert.True(File.Exists(cliAssembly), $"Expected CLI assembly at {cliAssembly}.");

        var sessionId = SessionId.New();
        var topLevelId = "topLevel:interaction";
        var pipeName = TestPipeNames.New();
        var manifestPath = WriteBridgeManifest(sessionId, pipeName);
        var artifactDirectory = Path.Combine(
            Path.GetTempPath(),
            "AvaScope.Tests",
            $"cli-interaction-{Guid.NewGuid():N}");
        var requestPath = Path.Combine(artifactDirectory, "interaction-animation.json");
        var frameStripPath = Path.Combine(artifactDirectory, "strip.png");
        Directory.CreateDirectory(artifactDirectory);

        var serverTask = RespondToBridgeRequestsAsync(
            pipeName,
            expectedCount: 5,
            (index, request) => index switch
            {
                0 => CreateInteractionInputResponse(request, sessionId, topLevelId),
                1 => CreateInteractionTreeResponse(request, sessionId, topLevelId),
                2 => CreateInteractionScreenshotResponse(request, sessionId, topLevelId),
                3 => CreateInteractionTreeResponse(request, sessionId, topLevelId),
                4 => CreateInteractionScreenshotResponse(request, sessionId, topLevelId),
                _ => throw new InvalidOperationException("Unexpected interaction animation bridge request index.")
            });
        var interactionRequest = new RuntimeInteractionAnimationRequest(
            sessionId,
            topLevelId,
            [
                new RuntimeInteractionAnimationStep(
                    InputActions.Click,
                    "expand",
                    x: 40,
                    y: 24,
                    frameOffsetsMs: [0, 1])
            ],
            requestId: "cli-interaction",
            outputDirectory: artifactDirectory,
            frameStripPath: frameStripPath,
            assertions:
            [
                new RuntimeInteractionGeometryAssertion(
                    "visual:panel",
                    RuntimeInteractionGeometryMetrics.Width,
                    RuntimeInteractionGeometryAssertionModes.Stable,
                    "panel-width",
                    stepId: "expand",
                    tolerance: 0)
            ]);
        await File.WriteAllTextAsync(requestPath, JsonSerializer.Serialize(interactionRequest, JsonOptions));

        try
        {
            var result = await RunCliAsync(
                cliAssembly,
                "record-interaction-animation",
                "--request",
                requestPath);
            var requests = await serverTask;

            Assert.Equal(0, result.ExitCode);
            Assert.True(string.IsNullOrWhiteSpace(result.StandardError), result.StandardError);
            Assert.Equal(BridgeIpcMethods.Input, requests[0].Method);
            Assert.Equal(BridgeIpcMethods.VisualTree, requests[1].Method);
            Assert.Equal(BridgeIpcMethods.Screenshot, requests[2].Method);

            var payload = JsonSerializer.Deserialize<ToolResult<RuntimeInteractionAnimationResponse>>(result.StandardOutput, JsonOptions);
            Assert.NotNull(payload);
            Assert.True(payload.Success, payload.Error?.Message);
            Assert.Equal("passed", payload.Value!.Status);
            Assert.Equal(2, payload.Value.Steps[0].Frames.Count);
            Assert.Equal("passed", Assert.Single(payload.Value.Assertions).Status);
            Assert.Equal(Path.GetFullPath(frameStripPath), payload.Value.FrameStripPath);
            Assert.True(File.Exists(payload.Value.FrameStripPath), payload.Value.FrameStripPath);
            Assert.All(payload.Value.Steps[0].Frames, frame =>
            {
                Assert.True(File.Exists(frame.Screenshot!.FilePath), frame.Screenshot.FilePath);
                Assert.True(File.Exists(frame.GeometryOverlayPath), frame.GeometryOverlayPath);
            });
            Assert.Contains(payload.Value.AgentReview.ArtifactPaths, artifact => artifact.Kind == "frame_strip");
        }
        finally
        {
            if (File.Exists(manifestPath))
            {
                File.Delete(manifestPath);
            }

            await DeleteDirectoryWithRetryAsync(artifactDirectory);
        }
    }

    [Fact]
    public async Task RunScenarioCommandBlocksDestructiveTargetWithoutIsolation()
    {
        var cliAssembly = Path.Combine(AppContext.BaseDirectory, "avascope.dll");
        Assert.True(File.Exists(cliAssembly), $"Expected CLI assembly at {cliAssembly}.");

        var sessionId = SessionId.New();
        var pipeName = TestPipeNames.New();
        var manifestPath = WriteBridgeManifest(sessionId, pipeName);
        var artifactDirectory = Path.Combine(
            Path.GetTempPath(),
            "AvaScope.Tests",
            $"cli-scenario-{Guid.NewGuid():N}");
        var requestPath = Path.Combine(artifactDirectory, "scenario.json");
        var timelinePath = Path.Combine(artifactDirectory, "timeline.md");
        Directory.CreateDirectory(artifactDirectory);

        var target = new RuntimeTargetContext(sessionId, "topLevel:scenario", TreeKinds.Visual, "visual:delete");
        var serverTask = RespondToBridgeRequestsAsync(
            pipeName,
            expectedCount: 2,
            (index, request) =>
            {
                return index switch
                {
                    0 => BridgeIpcResponse.Ok(request.RequestId, HealthResponse.Current()),
                    1 => CreateScenarioFindNodesResponse(request, sessionId, target, automationId: "delete-button", text: "Delete"),
                    _ => throw new InvalidOperationException("Unexpected scenario bridge request index.")
                };
            });

        var scenario = new RuntimeScenarioRequest(
            [
                new SemanticWorkflowStep(
                    SemanticWorkflowActions.Click,
                    "click-delete",
                    new SemanticWorkflowSelector(automationId: "delete-button", text: "Delete"))
            ],
            requestId: "cli-scenario-safety",
            sessionId: sessionId,
            topLevelId: "topLevel:scenario",
            outputDirectory: artifactDirectory,
            timelinePath: timelinePath);
        await File.WriteAllTextAsync(requestPath, JsonSerializer.Serialize(scenario, JsonOptions));

        try
        {
            var result = await RunCliAsync(
                cliAssembly,
                "run-scenario",
                "--request",
                requestPath);
            var requests = await serverTask;

            Assert.Equal(1, result.ExitCode);
            Assert.Equal(BridgeIpcMethods.Health, requests[0].Method);
            Assert.Equal(BridgeIpcMethods.FindNodes, requests[1].Method);

            var payload = JsonSerializer.Deserialize<ToolResult<RuntimeScenarioResponse>>(result.StandardOutput, JsonOptions);
            Assert.NotNull(payload);
            Assert.False(payload.Success);
            Assert.NotNull(payload.Value);
            Assert.Equal("failed", payload.Value!.Status);
            Assert.NotNull(payload.Value.Workflow);
            Assert.Equal(timelinePath, payload.Value.TimelinePath);
            Assert.Single(payload.Value.Workflow.Steps);
            Assert.Equal("semantic_workflow_destructive_target_requires_isolation", payload.Error!.Code);
            Assert.Equal("true", payload.Error.Details!["partialValueAvailable"]);
            Assert.True(File.Exists(timelinePath), timelinePath);
            var timeline = await File.ReadAllTextAsync(timelinePath);
            Assert.Contains("semantic_workflow_destructive_target_requires_isolation", timeline, StringComparison.Ordinal);
            Assert.Contains("click-delete", timeline, StringComparison.Ordinal);
        }
        finally
        {
            if (File.Exists(manifestPath))
            {
                File.Delete(manifestPath);
            }
            await DeleteDirectoryWithRetryAsync(artifactDirectory);
        }
    }

    [Fact]
    public async Task RunScenarioCommandLaunchModeCreatesIsolatedStateAndTimeline()
    {
        var cliAssembly = Path.Combine(AppContext.BaseDirectory, "avascope.dll");
        Assert.True(File.Exists(cliAssembly), $"Expected CLI assembly at {cliAssembly}.");

        var artifactDirectory = Path.Combine(
            Path.GetTempPath(),
            "AvaScope.Tests",
            $"cli-scenario-launch-{Guid.NewGuid():N}");
        var requestPath = Path.Combine(artifactDirectory, "scenario.json");
        var timelinePath = Path.Combine(artifactDirectory, "timeline.md");
        var isolatedStateDirectory = Path.Combine(artifactDirectory, "isolated-state");
        Directory.CreateDirectory(artifactDirectory);

        var scenario = new RuntimeScenarioRequest(
            [
                new SemanticWorkflowStep(
                    SemanticWorkflowActions.Wait,
                    "wait-after-launch",
                    waitMs: 1)
            ],
            requestId: "cli-scenario-launch",
            launch: new RuntimeScenarioLaunchOptions(
                "dotnet",
                arguments: "--info",
                outputDirectory: Path.Combine(artifactDirectory, "launch"),
                timeoutMs: 250),
            outputDirectory: artifactDirectory,
            isolatedStateDirectory: isolatedStateDirectory,
            timelinePath: timelinePath);
        await File.WriteAllTextAsync(requestPath, JsonSerializer.Serialize(scenario, JsonOptions));

        try
        {
            var result = await RunCliAsync(
                cliAssembly,
                "run-scenario",
                "--request",
                requestPath);

            Assert.Equal(1, result.ExitCode);
            Assert.True(string.IsNullOrWhiteSpace(result.StandardError), result.StandardError);

            var payload = JsonSerializer.Deserialize<ToolResult<RuntimeScenarioResponse>>(result.StandardOutput, JsonOptions);
            Assert.NotNull(payload);
            Assert.False(payload.Success);
            Assert.NotNull(payload.Value);
            Assert.Equal("failed", payload.Value!.Status);
            Assert.Equal(timelinePath, payload.Value.TimelinePath);
            Assert.Equal(CoreErrorCodes.BridgeSessionNotFound, payload.Error!.Code);
            Assert.Equal("true", payload.Error.Details!["partialValueAvailable"]);
            Assert.True(Directory.Exists(Path.Combine(isolatedStateDirectory, "appdata", "local")));
            Assert.True(File.Exists(timelinePath), timelinePath);
            var timeline = await File.ReadAllTextAsync(timelinePath);
            Assert.Contains("applied_environment", timeline, StringComparison.Ordinal);
            Assert.Contains(CoreErrorCodes.BridgeSessionNotFound, timeline, StringComparison.Ordinal);
        }
        finally
        {
            await DeleteDirectoryWithRetryAsync(artifactDirectory);
        }
    }

    [Fact]
    public async Task RunScenarioCommandReturnsStructuredBuildFailureBeforeLaunch()
    {
        var cliAssembly = Path.Combine(AppContext.BaseDirectory, "avascope.dll");
        var artifactDirectory = Path.Combine(
            Path.GetTempPath(),
            "AvaScope.Tests",
            $"cli-scenario-build-{Guid.NewGuid():N}");
        var projectDirectory = Path.Combine(artifactDirectory, "project");
        var projectPath = Path.Combine(projectDirectory, "BuildFailure.csproj");
        var requestPath = Path.Combine(artifactDirectory, "scenario.json");
        Directory.CreateDirectory(projectDirectory);
        await File.WriteAllTextAsync(
            projectPath,
            "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup><Target Name=\"FailExpectedly\" BeforeTargets=\"Build\"><Error Text=\"Expected CLI lifecycle failure\" /></Target></Project>");
        var scenario = new RuntimeScenarioRequest(
            [new SemanticWorkflowStep(SemanticWorkflowActions.Wait, "wait", waitMs: 1)],
            requestId: "cli-scenario-build-failure",
            launch: new RuntimeScenarioLaunchOptions($"missing-command-{Guid.NewGuid():N}"),
            topLevelId: "topLevel:missing",
            outputDirectory: artifactDirectory,
            build: new RuntimeScenarioBuildOptions(projectPath, arguments: ["--nologo"]),
            terminateLaunchedProcess: true);
        await File.WriteAllTextAsync(requestPath, JsonSerializer.Serialize(scenario, JsonOptions));

        try
        {
            var result = await RunCliAsync(cliAssembly, "run-scenario", "--request", requestPath);

            Assert.Equal(1, result.ExitCode);
            Assert.True(string.IsNullOrWhiteSpace(result.StandardError), result.StandardError);
            var payload = JsonSerializer.Deserialize<ToolResult<RuntimeScenarioResponse>>(result.StandardOutput, JsonOptions);
            Assert.False(payload!.Success);
            Assert.Equal("runtime_scenario_build_failed", payload.Error!.Code);
            Assert.Equal(RuntimeScenarioFailureStages.Build, payload.Value!.FailureStage);
            Assert.Equal(RuntimeScenarioLifecycleStatuses.Failed, payload.Value.Build!.Status);
            Assert.True(File.Exists(payload.Value.Build.StdoutPath));
            Assert.Contains("Expected CLI lifecycle failure", await File.ReadAllTextAsync(payload.Value.Build.StdoutPath), StringComparison.Ordinal);
            Assert.Null(payload.Value.Launch);
        }
        finally
        {
            await DeleteDirectoryWithRetryAsync(artifactDirectory);
        }
    }

    [Fact]
    public async Task MutateNodeCommandSendsNoOpThroughBridgePipe()
    {
        var cliAssembly = Path.Combine(AppContext.BaseDirectory, "avascope.dll");
        Assert.True(File.Exists(cliAssembly), $"Expected CLI assembly at {cliAssembly}.");

        var sessionId = SessionId.New();
        var pipeName = TestPipeNames.New();
        var manifestPath = WriteBridgeManifest(sessionId, pipeName);

        var serverTask = RespondToBridgeRequestAsync(pipeName, request =>
        {
            Assert.Equal(BridgeIpcMethods.MutateNode, request.Method);
            Assert.Equal("cli-mutation-request-1", request.RequestId);
            Assert.NotNull(request.Mutation);
            Assert.Equal("cli-mutation-request-1", request.Mutation.RequestId);
            Assert.Equal(sessionId, request.Mutation.Target.SessionId);
            Assert.Equal("topLevel:cli", request.Mutation.Target.TopLevelId);
            Assert.Equal(TreeKinds.Visual, request.Mutation.Target.TreeKind);
            Assert.Equal("visual:button", request.Mutation.Target.NodeId);
            Assert.Equal(RuntimeMutationOperationKinds.NoOp, request.Mutation.Operation.Kind);

            return BridgeIpcResponse.Ok(
                request.RequestId,
                new RuntimeMutationResponse(
                    request.Mutation.RequestId,
                    "mutation:cli:1",
                    sessionId,
                    request.Mutation.Target.TopLevelId,
                    request.Mutation.Target,
                    request.Mutation.Operation,
                    RuntimeMutationStatuses.NoOp,
                    applied: false,
                    DateTimeOffset.UtcNow,
                    RuntimeMutationCapabilityCatalog.ContractOnly()));
        });

        try
        {
            var result = await RunCliAsync(
                cliAssembly,
                "mutate-node",
                "--session",
                sessionId.Value,
                "--top-level",
                "topLevel:cli",
                "--node",
                "visual:button",
                "--operation",
                RuntimeMutationOperationKinds.NoOp,
                "--request-id",
                "cli-mutation-request-1");
            var request = await serverTask;

            Assert.Equal(0, result.ExitCode);
            Assert.Equal(BridgeIpcMethods.MutateNode, request.Method);
            Assert.True(string.IsNullOrWhiteSpace(result.StandardError), result.StandardError);

            var payload = JsonSerializer.Deserialize<ToolResult<RuntimeMutationResponse>>(result.StandardOutput, JsonOptions);
            Assert.NotNull(payload);
            Assert.True(payload.Success, payload.Error?.Message);
            Assert.Equal("mutation:cli:1", payload.Value!.MutationId);
            Assert.Equal(RuntimeMutationStatuses.NoOp, payload.Value.Status);
            Assert.Empty(payload.Value.Diagnostics);
        }
        finally
        {
            if (File.Exists(manifestPath))
            {
                File.Delete(manifestPath);
            }
        }
    }

    [Fact]
    public async Task MutateNodeCommandReturnsUnsupportedDiagnosticsThroughBridgePipe()
    {
        var cliAssembly = Path.Combine(AppContext.BaseDirectory, "avascope.dll");
        Assert.True(File.Exists(cliAssembly), $"Expected CLI assembly at {cliAssembly}.");

        var sessionId = SessionId.New();
        var pipeName = TestPipeNames.New();
        var manifestPath = WriteBridgeManifest(sessionId, pipeName);

        var serverTask = RespondToBridgeRequestAsync(pipeName, request =>
        {
            Assert.Equal(BridgeIpcMethods.MutateNode, request.Method);
            Assert.NotNull(request.Mutation);
            Assert.Equal(RuntimeMutationOperationKinds.SetProperty, request.Mutation.Operation.Kind);
            Assert.Equal("Width", request.Mutation.Operation.PropertyName);
            Assert.Equal("240", request.Mutation.Operation.Value);
            Assert.Equal("double", request.Mutation.Operation.ValueType);

            return BridgeIpcResponse.Ok(
                request.RequestId,
                new RuntimeMutationResponse(
                    request.Mutation.RequestId,
                    "mutation:cli:2",
                    sessionId,
                    request.Mutation.Target.TopLevelId,
                    request.Mutation.Target,
                    request.Mutation.Operation,
                    RuntimeMutationStatuses.Unsupported,
                    applied: false,
                    DateTimeOffset.UtcNow,
                    RuntimeMutationCapabilityCatalog.ContractOnly(),
                    [
                        new ProtocolError(
                            RuntimeMutationErrorCodes.UnsupportedRuntimeMutationProperty,
                            "Width is not supported yet.",
                            new Dictionary<string, string>
                            {
                                ["propertyName"] = "Width"
                            })
                    ]));
        });

        try
        {
            var result = await RunCliAsync(
                cliAssembly,
                "mutate-node",
                "--session",
                sessionId.Value,
                "--top-level",
                "topLevel:cli",
                "--node",
                "visual:button",
                "--operation",
                RuntimeMutationOperationKinds.SetProperty,
                "--property",
                "Width",
                "--value",
                "240",
                "--value-type",
                "double");
            await serverTask;

            Assert.Equal(1, result.ExitCode);

            var payload = JsonSerializer.Deserialize<ToolResult<RuntimeMutationResponse>>(result.StandardOutput, JsonOptions);
            Assert.NotNull(payload);
            Assert.False(payload.Success);
            Assert.NotNull(payload.Value);
            Assert.Equal(RuntimeMutationStatuses.Unsupported, payload.Value!.Status);
            Assert.Equal("visual:button", payload.Value.Target.NodeId);
            Assert.Equal(RuntimeMutationErrorCodes.UnsupportedRuntimeMutationProperty, payload.Error!.Code);
            Assert.Equal("true", payload.Error.Details!["partialValueAvailable"]);
        }
        finally
        {
            if (File.Exists(manifestPath))
            {
                File.Delete(manifestPath);
            }
        }
    }

    [Fact]
    public async Task MutateNodeCommandSendsResetMutationThroughBridgePipe()
    {
        var cliAssembly = Path.Combine(AppContext.BaseDirectory, "avascope.dll");
        Assert.True(File.Exists(cliAssembly), $"Expected CLI assembly at {cliAssembly}.");

        var sessionId = SessionId.New();
        var pipeName = TestPipeNames.New();
        var manifestPath = WriteBridgeManifest(sessionId, pipeName);

        var serverTask = RespondToBridgeRequestAsync(pipeName, request =>
        {
            Assert.Equal(BridgeIpcMethods.MutateNode, request.Method);
            Assert.NotNull(request.Mutation);
            Assert.Equal(RuntimeMutationOperationKinds.ResetMutation, request.Mutation.Operation.Kind);
            Assert.Equal("mutation:cli:2", request.Mutation.Operation.MutationId);

            return BridgeIpcResponse.Ok(
                request.RequestId,
                new RuntimeMutationResponse(
                    request.Mutation.RequestId,
                    "mutation:cli:3",
                    sessionId,
                    request.Mutation.Target.TopLevelId,
                    request.Mutation.Target,
                    request.Mutation.Operation,
                    RuntimeMutationStatuses.Applied,
                    applied: true,
                    DateTimeOffset.UtcNow,
                    RuntimeMutationCapabilityCatalog.CurrentBridgeCapabilities(),
                    metadata: new Dictionary<string, string>
                    {
                        ["resetMutationIds"] = "mutation:cli:2",
                        ["resetCount"] = "1",
                        ["activeMutationCount"] = "0"
                    }));
        });

        try
        {
            var result = await RunCliAsync(
                cliAssembly,
                "mutate-node",
                "--session",
                sessionId.Value,
                "--top-level",
                "topLevel:cli",
                "--node",
                "visual:button",
                "--operation",
                RuntimeMutationOperationKinds.ResetMutation,
                "--mutation-id",
                "mutation:cli:2");
            await serverTask;

            Assert.Equal(0, result.ExitCode);
            var payload = JsonSerializer.Deserialize<ToolResult<RuntimeMutationResponse>>(result.StandardOutput, JsonOptions);
            Assert.NotNull(payload);
            Assert.True(payload.Success, payload.Error?.Message);
            Assert.Equal(RuntimeMutationStatuses.Applied, payload.Value!.Status);
            Assert.Equal("mutation:cli:2", payload.Value.Metadata["resetMutationIds"]);
            Assert.Equal("0", payload.Value.Metadata["activeMutationCount"]);
            var styleCapability = Assert.Single(payload.Value.Capabilities, capability =>
                capability.Name == RuntimeMutationCapabilityCatalog.StyleLayoutMutation);
            Assert.Equal("local_only", styleCapability.Metadata["transport"]);
            Assert.Equal("true", styleCapability.Metadata["temporary"]);
            Assert.Equal("true", styleCapability.Metadata["reversible"]);
        }
        finally
        {
            if (File.Exists(manifestPath))
            {
                File.Delete(manifestPath);
            }
        }
    }

    [Fact]
    public async Task MutateNodeEvidenceCommandCapturesSequencedArtifactsThroughBridgePipe()
    {
        var cliAssembly = Path.Combine(AppContext.BaseDirectory, "avascope.dll");
        Assert.True(File.Exists(cliAssembly), $"Expected CLI assembly at {cliAssembly}.");

        var sessionId = SessionId.New();
        var pipeName = TestPipeNames.New();
        var manifestPath = WriteBridgeManifest(sessionId, pipeName);
        var artifactDirectory = Path.Combine(
            Path.GetTempPath(),
            "AvaScope.Tests",
            $"cli-evidence-{Guid.NewGuid():N}");
        var serverTask = RespondToBridgeRequestsAsync(
            pipeName,
            expectedCount: 5,
            (index, request) =>
            {
                return index switch
                {
                    0 => CreateCliEvidenceScreenshotResponse(
                        request,
                        sessionId,
                        "topLevel:cli",
                        "cli-evidence-before.png"),
                    1 => CreateCliEvidenceTreeResponse(
                        request,
                        sessionId,
                        "topLevel:cli",
                        expectedMaxDepth: 5,
                        "Before"),
                    2 => CreateCliEvidenceMutationResponse(
                        request,
                        sessionId,
                        "topLevel:cli"),
                    3 => CreateCliEvidenceScreenshotResponse(
                        request,
                        sessionId,
                        "topLevel:cli",
                        "cli-evidence-after.png"),
                    4 => CreateCliEvidenceTreeResponse(
                        request,
                        sessionId,
                        "topLevel:cli",
                        expectedMaxDepth: 5,
                        "After"),
                    _ => throw new InvalidOperationException("Unexpected bridge request index.")
                };
            });

        try
        {
            var result = await RunCliAsync(
                cliAssembly,
                "mutate-node-evidence",
                "--session",
                sessionId.Value,
                "--top-level",
                "topLevel:cli",
                "--node",
                "visual:button",
                "--operation",
                RuntimeMutationOperationKinds.SetProperty,
                "--property",
                "Text",
                "--value",
                "After",
                "--value-type",
                "string",
                "--request-id",
                "cli-evidence",
                "--out-dir",
                artifactDirectory,
                "--max-depth",
                "5",
                "--diff",
                "false");
            var bridgeRequests = await serverTask;

            Assert.Equal(0, result.ExitCode);
            Assert.True(string.IsNullOrWhiteSpace(result.StandardError), result.StandardError);
            Assert.Equal(
                [
                    BridgeIpcMethods.Screenshot,
                    BridgeIpcMethods.VisualTree,
                    BridgeIpcMethods.MutateNode,
                    BridgeIpcMethods.Screenshot,
                    BridgeIpcMethods.VisualTree
                ],
                bridgeRequests.Select(static bridgeRequest => bridgeRequest.Method).ToArray());

            var payload = JsonSerializer.Deserialize<ToolResult<RuntimeMutationEvidenceResponse>>(result.StandardOutput, JsonOptions);
            Assert.NotNull(payload);
            Assert.True(payload.Success, payload.Error?.Message);
            Assert.Equal("cli-evidence", payload.Value!.RequestId);
            Assert.Equal("captured", payload.Value.Summary.Status);
            Assert.Equal("not_requested", payload.Value.Summary.DiffStatus);
            Assert.Equal(RuntimeMutationStatuses.Applied, payload.Value.Mutation.Status);
            Assert.True(payload.Value.Mutation.Applied);
            Assert.Equal("Before", payload.Value.BeforeTarget!.Text);
            Assert.Equal("After", payload.Value.AfterTarget!.Text);
            Assert.EndsWith("cli-evidence-before.png", payload.Value.BeforeScreenshotPath, StringComparison.Ordinal);
            Assert.EndsWith("cli-evidence-after.png", payload.Value.AfterScreenshotPath, StringComparison.Ordinal);
            Assert.True(File.Exists(payload.Value.BeforeVisualTreePath));
            Assert.True(File.Exists(payload.Value.AfterVisualTreePath));
            Assert.NotNull(payload.Value.ReviewArtifact);
            Assert.True(File.Exists(payload.Value.ReviewArtifact!.ArtifactPath));
            Assert.Equal("html", payload.Value.ReviewArtifact.Format);
            Assert.Equal("captured", payload.Value.AgentReview.Status);
            Assert.Equal("mutation:cli:evidence:1", Assert.Single(payload.Value.AgentReview.Mutations).MutationId);
            Assert.Contains(payload.Value.AgentReview.ArtifactPaths, path => path.Kind == "after_screenshot");
            Assert.Contains(payload.Value.AgentReview.ReviewUrls, url => url.EndsWith("-review.html", StringComparison.Ordinal));
            var reviewHtml = await File.ReadAllTextAsync(payload.Value.ReviewArtifact.ArtifactPath);
            Assert.Contains("mutation:cli:evidence:1", reviewHtml, StringComparison.Ordinal);
            Assert.Contains("Before", reviewHtml, StringComparison.Ordinal);
            Assert.Contains("After", reviewHtml, StringComparison.Ordinal);
        }
        finally
        {
            if (File.Exists(manifestPath))
            {
                File.Delete(manifestPath);
            }

            if (Directory.Exists(artifactDirectory))
            {
                Directory.Delete(artifactDirectory, recursive: true);
            }
        }
    }

    [Fact]
    public async Task MutationReviewCommandReadsHistoryAndWritesArtifactThroughBridgePipe()
    {
        var cliAssembly = Path.Combine(AppContext.BaseDirectory, "avascope.dll");
        Assert.True(File.Exists(cliAssembly), $"Expected CLI assembly at {cliAssembly}.");

        var sessionId = SessionId.New();
        var pipeName = TestPipeNames.New();
        var manifestDirectory = Path.Combine(
            Path.GetTempPath(),
            "AvaScope.Tests",
            $"cli-review-manifests-{Guid.NewGuid():N}");
        var manifestPath = WriteBridgeManifest(sessionId, pipeName, manifestDirectory);
        var artifactDirectory = Path.Combine(
            Path.GetTempPath(),
            "AvaScope.Tests",
            $"cli-review-{Guid.NewGuid():N}");
        var reviewPath = Path.Combine(artifactDirectory, "review.html");
        var sourceViewPath = Path.Combine(artifactDirectory, "Views", "MainView.axaml");
        var sourceProjectPath = Path.Combine(artifactDirectory, "SampleApp.csproj");
        var target = new RuntimeTargetContext(sessionId, "topLevel:cli", TreeKinds.Visual, "visual:button");
        var operation = new RuntimeMutationOperation(
            RuntimeMutationOperationKinds.SetProperty,
            propertyName: "Text",
            value: "After",
            valueType: "string");
        var entry = new RuntimeMutationReviewEntry(
            1,
            "cli-review-request-1",
            "mutation:cli:review:1",
            sessionId,
            target.TopLevelId,
            target,
            operation,
            RuntimeMutationStatuses.Applied,
            applied: true,
            active: true,
            DateTimeOffset.UtcNow,
            metadata: new Dictionary<string, string>
            {
                ["propertyName"] = "Text"
            });
        var serverTask = RespondToBridgeRequestAsync(pipeName, request =>
        {
            Assert.Equal(BridgeIpcMethods.MutationReview, request.Method);
            Assert.Equal(5, request.MaxResults);

            return BridgeIpcResponse.Ok(
                request.RequestId,
                new RuntimeMutationReviewResponse(
                    sessionId,
                    DateTimeOffset.UtcNow,
                    historyCount: 1,
                    activeMutationCount: 1,
                    history: [entry],
                    activeMutations: [entry],
                    resetHandoff: new RuntimeMutationResetHandoff(
                        sessionId,
                        activeMutationCount: 1,
                        activeMutationIds: [entry.MutationId],
                        suggestedResetAllTarget: target),
                    metadata: new Dictionary<string, string>
                    {
                        ["scope"] = "local_session"
                    }));
        });

        try
        {
            var result = await RunCliAsync(
                cliAssembly,
                "mutation-review",
                "--session",
                sessionId.Value,
                "--max-results",
                "5",
                "--out",
                reviewPath,
                "--manifest-dir",
                manifestDirectory,
                "--source-project",
                sourceProjectPath,
                "--source-view",
                sourceViewPath);
            var bridgeRequest = await serverTask;

            Assert.Equal(0, result.ExitCode);
            Assert.Equal(BridgeIpcMethods.MutationReview, bridgeRequest.Method);
            Assert.True(string.IsNullOrWhiteSpace(result.StandardError), result.StandardError);

            var payload = JsonSerializer.Deserialize<ToolResult<RuntimeMutationReviewResponse>>(result.StandardOutput, JsonOptions);
            Assert.NotNull(payload);
            Assert.True(payload.Success, payload.Error?.Message);
            Assert.Equal(1, payload.Value!.HistoryCount);
            Assert.Equal(1, payload.Value.ActiveMutationCount);
            Assert.Equal("mutation:cli:review:1", Assert.Single(payload.Value.ActiveMutations).MutationId);
            Assert.Equal(RuntimeMutationOperationKinds.ResetMutation, payload.Value.ResetHandoff.ResetMutationOperation);
            Assert.NotNull(payload.Value.SourceContext);
            Assert.Equal(Path.GetFullPath(sourceViewPath), payload.Value.SourceContext!.ViewPath);
            var suggestion = Assert.Single(payload.Value.SourceSuggestions);
            Assert.Equal("mutation:cli:review:1", suggestion.MutationId);
            Assert.Equal("medium", suggestion.Confidence);
            Assert.Equal("provided", suggestion.SourceFileStatus);
            Assert.Equal(Path.GetFullPath(sourceViewPath), suggestion.SuggestedFilePath);
            Assert.Equal("Text", suggestion.SuggestedProperty);
            Assert.NotNull(payload.Value.ReviewArtifact);
            Assert.Equal(Path.GetFullPath(reviewPath), payload.Value.ReviewArtifact!.ArtifactPath);
            Assert.Equal("active_mutations", payload.Value.AgentReview.Status);
            Assert.Contains("sourceSuggestions: 1", payload.Value.AgentReview.Summary, StringComparer.Ordinal);
            Assert.Equal("mutation:cli:review:1", Assert.Single(payload.Value.AgentReview.Mutations).MutationId);
            Assert.Contains(payload.Value.AgentReview.ReviewUrls, url => url == new Uri(reviewPath).AbsoluteUri);
            Assert.True(File.Exists(payload.Value.ReviewArtifact.ArtifactPath));
            var reviewHtml = await File.ReadAllTextAsync(payload.Value.ReviewArtifact.ArtifactPath);
            Assert.Contains("mutation:cli:review:1", reviewHtml, StringComparison.Ordinal);
            Assert.Contains("Source Suggestions", reviewHtml, StringComparison.Ordinal);
            Assert.Contains(Path.GetFileName(sourceViewPath), reviewHtml, StringComparison.Ordinal);
        }
        finally
        {
            if (File.Exists(manifestPath))
            {
                File.Delete(manifestPath);
            }

            if (Directory.Exists(manifestDirectory))
            {
                Directory.Delete(manifestDirectory, recursive: true);
            }

            if (Directory.Exists(artifactDirectory))
            {
                Directory.Delete(artifactDirectory, recursive: true);
            }
        }
    }

    [Fact]
    public async Task MutateNodeEvidenceCommandRejectsMissingRequiredArguments()
    {
        var cliAssembly = Path.Combine(AppContext.BaseDirectory, "avascope.dll");
        Assert.True(File.Exists(cliAssembly), $"Expected CLI assembly at {cliAssembly}.");

        var result = await RunCliAsync(
            cliAssembly,
            "mutate-node-evidence",
            "--session",
            "missing");

        Assert.Equal(2, result.ExitCode);
        var payload = JsonSerializer.Deserialize<ToolResult<RuntimeMutationEvidenceResponse>>(result.StandardOutput, JsonOptions);
        Assert.NotNull(payload);
        Assert.False(payload.Success);
        Assert.Equal("invalid_cli_arguments", payload.Error!.Code);
    }

    [Fact]
    public async Task MutateNodeCommandRejectsMissingRequiredArguments()
    {
        var cliAssembly = Path.Combine(AppContext.BaseDirectory, "avascope.dll");
        Assert.True(File.Exists(cliAssembly), $"Expected CLI assembly at {cliAssembly}.");

        var result = await RunCliAsync(
            cliAssembly,
            "mutate-node",
            "--session",
            "missing");

        Assert.Equal(2, result.ExitCode);
        var payload = JsonSerializer.Deserialize<ToolResult<RuntimeMutationResponse>>(result.StandardOutput, JsonOptions);
        Assert.NotNull(payload);
        Assert.False(payload.Success);
        Assert.Equal("invalid_cli_arguments", payload.Error!.Code);
    }

    [Fact]
    public async Task CloseSessionCommandClosesThroughBridgePipe()
    {
        var cliAssembly = Path.Combine(AppContext.BaseDirectory, "avascope.dll");
        Assert.True(File.Exists(cliAssembly), $"Expected CLI assembly at {cliAssembly}.");

        var sessionId = SessionId.New();
        var pipeName = TestPipeNames.New();
        var manifestDirectory = Path.Combine(
            Path.GetTempPath(),
            "AvaScope.Tests",
            $"cli-close-session-manifests-{Guid.NewGuid():N}");
        var manifestPath = WriteBridgeManifest(sessionId, pipeName, manifestDirectory);
        var closedAt = DateTimeOffset.UtcNow;

        var serverTask = RespondToBridgeRequestAsync(pipeName, request =>
        {
            Assert.Equal(BridgeIpcMethods.CloseSession, request.Method);
            return BridgeIpcResponse.Ok(
                request.RequestId,
                new CloseSessionResponse(
                    new SessionSummary(
                        sessionId,
                        SessionKinds.Runtime,
                        SessionStates.Closed,
                        closedAt,
                        "CLI fake bridge"),
                    Environment.ProcessId,
                    closedAt));
        });

        try
        {
            var result = await RunCliAsync(
                cliAssembly,
                "close-session",
                "--session",
                sessionId.Value,
                "--manifest-dir",
                manifestDirectory);
            var request = await serverTask;

            Assert.Equal(0, result.ExitCode);
            Assert.Equal(BridgeIpcMethods.CloseSession, request.Method);
            Assert.True(string.IsNullOrWhiteSpace(result.StandardError), result.StandardError);

            var payload = JsonSerializer.Deserialize<ToolResult<CloseSessionResponse>>(result.StandardOutput, JsonOptions);
            Assert.NotNull(payload);
            Assert.True(payload.Success, payload.Error?.Message);
            Assert.Equal(sessionId, payload.Value!.Session.SessionId);
            Assert.Equal(SessionStates.Closed, payload.Value.Session.State);
            Assert.Equal(Environment.ProcessId, payload.Value.ProcessId);
        }
        finally
        {
            if (File.Exists(manifestPath))
            {
                File.Delete(manifestPath);
            }

            if (Directory.Exists(manifestDirectory))
            {
                Directory.Delete(manifestDirectory, recursive: true);
            }
        }
    }

    [Fact]
    public async Task CloseSessionCommandReturnsStructuredErrorWhenNoBridgeSessionMatches()
    {
        var cliAssembly = Path.Combine(AppContext.BaseDirectory, "avascope.dll");
        Assert.True(File.Exists(cliAssembly), $"Expected CLI assembly at {cliAssembly}.");

        var result = await RunCliAsync(cliAssembly, "close-session", "--session", "missing");

        Assert.Equal(1, result.ExitCode);
        Assert.True(string.IsNullOrWhiteSpace(result.StandardError), result.StandardError);

        var payload = JsonSerializer.Deserialize<ToolResult<CloseSessionResponse>>(result.StandardOutput, JsonOptions);
        Assert.NotNull(payload);
        Assert.False(payload.Success);
        Assert.Equal(CoreErrorCodes.BridgeSessionNotFound, payload.Error!.Code);
    }

    [Fact]
    public async Task RunWorkflowCommandWaitsForTypedChangeFromFirstObservedBaseline()
    {
        var cliAssembly = Path.Combine(AppContext.BaseDirectory, "avascope.dll");
        Assert.True(File.Exists(cliAssembly), $"Expected CLI assembly at {cliAssembly}.");

        var sessionId = SessionId.New();
        var pipeName = TestPipeNames.New();
        var manifestDirectory = Path.Combine(
            Path.GetTempPath(),
            "AvaScope.Tests",
            $"cli-wait-manifests-{Guid.NewGuid():N}");
        var manifestPath = WriteBridgeManifest(sessionId, pipeName, manifestDirectory);
        var requestPath = Path.Combine(manifestDirectory, "wait-workflow.json");
        var target = new RuntimeTargetContext(
            sessionId,
            "topLevel:wait",
            TreeKinds.Visual,
            "visual:range");
        var serverTask = RespondToBridgeRequestsAsync(
            pipeName,
            expectedCount: 4,
            (index, request) => index switch
            {
                0 or 2 => CreateScenarioFindNodesResponse(
                    request,
                    sessionId,
                    target,
                    automationId: "wait-range",
                    text: "Range"),
                1 => CreateWaitInspectResponse(request, sessionId, target, "42"),
                3 => CreateWaitInspectResponse(request, sessionId, target, "75"),
                _ => throw new InvalidOperationException("Unexpected wait workflow bridge request index.")
            });
        var workflow = new SemanticWorkflowRequest(
            sessionId,
            target.TopLevelId,
            [
                new SemanticWorkflowStep(
                    SemanticWorkflowActions.WaitForState,
                    "wait-change",
                    new SemanticWorkflowSelector(automationId: "wait-range"),
                    timeoutMs: 5000,
                    pollIntervalMs: 25,
                    waitCondition: new SemanticWaitCondition(
                        SemanticWaitConditionKinds.ChangeFromBaseline,
                        valueType: "number",
                        propertyName: "Value"))
            ]);
        await File.WriteAllTextAsync(requestPath, JsonSerializer.Serialize(workflow, JsonOptions));

        try
        {
            var result = await RunCliAsync(
                cliAssembly,
                "run-workflow",
                "--request",
                requestPath,
                "--manifest-dir",
                manifestDirectory);
            var requests = await serverTask;

            Assert.Equal(0, result.ExitCode);
            Assert.Equal(
                [BridgeIpcMethods.FindNodes, BridgeIpcMethods.InspectNode, BridgeIpcMethods.FindNodes, BridgeIpcMethods.InspectNode],
                requests.Select(static request => request.Method));
            var payload = JsonSerializer.Deserialize<ToolResult<SemanticWorkflowResponse>>(result.StandardOutput, JsonOptions);
            Assert.True(payload!.Success, payload.Error?.Message);
            var step = Assert.Single(payload.Value!.Steps);
            Assert.Equal("passed", step.Status);
            Assert.Equal("42", step.WaitObservation!.Baseline);
            Assert.Equal("75", step.WaitObservation.Value);
            Assert.Equal(typeof(double).FullName, step.WaitObservation.ValueType);
        }
        finally
        {
            if (File.Exists(manifestPath))
            {
                File.Delete(manifestPath);
            }

            if (Directory.Exists(manifestDirectory))
            {
                Directory.Delete(manifestDirectory, recursive: true);
            }
        }
    }

    [Fact]
    public async Task RunWorkflowCommandResolvesTopLevelAliasWithoutRootRuntimeId()
    {
        var cliAssembly = Path.Combine(AppContext.BaseDirectory, "avascope.dll");
        var sessionId = SessionId.New();
        var pipeName = TestPipeNames.New();
        var manifestDirectory = Path.Combine(
            Path.GetTempPath(),
            "AvaScope.Tests",
            $"cli-alias-manifests-{Guid.NewGuid():N}");
        var manifestPath = WriteBridgeManifest(sessionId, pipeName, manifestDirectory);
        var requestPath = Path.Combine(manifestDirectory, "alias-workflow.json");
        var target = new RuntimeTargetContext(
            sessionId,
            "topLevel:controls-current",
            TreeKinds.Visual,
            "visual:status");
        var serverTask = RespondToBridgeRequestsAsync(
            pipeName,
            expectedCount: 3,
            (index, request) => index switch
            {
                0 => BridgeIpcResponse.Ok(
                    request.RequestId,
                    new[]
                    {
                        new TopLevelSummary(
                            target.TopLevelId,
                            "Window",
                            "Controls",
                            320,
                            200,
                            1,
                            isActive: true)
                    }),
                1 => CreateScenarioFindNodesResponse(
                    request,
                    sessionId,
                    target,
                    automationId: "alias-status",
                    text: "Ready"),
                2 => BridgeIpcResponse.Ok(
                    request.RequestId,
                    new InspectNodeResponse(
                        sessionId,
                        target.TopLevelId,
                        TreeKinds.Visual,
                        target.NodeId!,
                        "Avalonia.Controls.TextBlock",
                        childCount: 0,
                        text: "Ready",
                        target: target)),
                _ => throw new InvalidOperationException("Unexpected alias workflow bridge request index.")
            });
        var workflow = new SemanticWorkflowRequest(
            sessionId,
            topLevelId: null,
            steps:
            [
                new SemanticWorkflowStep(
                    SemanticWorkflowActions.Inspect,
                    "inspect-controls",
                    new SemanticWorkflowSelector(automationId: "alias-status"),
                    topLevelAlias: "controls")
            ],
            topLevelAliases:
            [
                new SemanticWorkflowTopLevelAlias("controls", new SemanticTopLevelSelector(title: "Controls"))
            ]);
        await File.WriteAllTextAsync(requestPath, JsonSerializer.Serialize(workflow, JsonOptions));

        try
        {
            var result = await RunCliAsync(
                cliAssembly,
                "run-workflow",
                "--request",
                requestPath,
                "--manifest-dir",
                manifestDirectory);
            var requests = await serverTask;

            Assert.Equal(0, result.ExitCode);
            Assert.Equal(
                [BridgeIpcMethods.ListTopLevels, BridgeIpcMethods.FindNodes, BridgeIpcMethods.InspectNode],
                requests.Select(static request => request.Method));
            var payload = JsonSerializer.Deserialize<ToolResult<SemanticWorkflowResponse>>(result.StandardOutput, JsonOptions);
            Assert.True(payload!.Success, payload.Error?.Message);
            Assert.Null(payload.Value!.TopLevelId);
            var step = Assert.Single(payload.Value.Steps);
            Assert.Equal("controls", step.TopLevelAlias);
            Assert.Equal(target.TopLevelId, step.ResolvedTopLevelId);
            Assert.Equal("Ready", step.Inspection!.Text);
        }
        finally
        {
            if (File.Exists(manifestPath))
            {
                File.Delete(manifestPath);
            }

            if (Directory.Exists(manifestDirectory))
            {
                Directory.Delete(manifestDirectory, recursive: true);
            }
        }
    }

    [Fact]
    public async Task RunWorkflowCommandValidatesExpandedCompositionWithoutBridgeDispatch()
    {
        var cliAssembly = Path.Combine(AppContext.BaseDirectory, "avascope.dll");
        var manifestDirectory = Path.Combine(
            Path.GetTempPath(),
            "AvaScope.Tests",
            $"cli-composition-validation-{Guid.NewGuid():N}");
        Directory.CreateDirectory(manifestDirectory);
        var requestPath = Path.Combine(manifestDirectory, "composition-workflow.json");
        var evidenceRoot = Path.Combine(manifestDirectory, "owned-evidence");
        var request = new SemanticWorkflowRequest(
            new SessionId("cli-composition"),
            "topLevel:diagnostic-only",
            [
                new SemanticWorkflowStep(
                    SemanticWorkflowActions.If,
                    "branch",
                    new SemanticWorkflowSelector(automationId: "${statusId}"),
                    waitCondition: new SemanticWaitCondition(SemanticWaitConditionKinds.Text, "ready"),
                    then:
                    [
                        new SemanticWorkflowStep(
                            SemanticWorkflowActions.UseFragment,
                            "verify",
                            fragment: "assert-status",
                            arguments: new Dictionary<string, string> { ["expected"] = "ready" })
                    ]),
                new SemanticWorkflowStep(
                    SemanticWorkflowActions.Invoke,
                    "verified-save",
                    new SemanticWorkflowSelector(automationId: "save"),
                    verify: new SemanticWorkflowVerification(
                        new SemanticWaitCondition(SemanticWaitConditionKinds.Text, "ready"),
                        new SemanticWorkflowSelector(automationId: "status")))
            ],
            variables: new Dictionary<string, string> { ["statusId"] = "status" },
            fragments:
            [
                new SemanticWorkflowFragment(
                    "assert-status",
                    [
                        new SemanticWorkflowStep(
                            SemanticWorkflowActions.AssertState,
                            "assert",
                            new SemanticWorkflowSelector(automationId: "${statusId}"),
                            assertProperty: "Text",
                            expected: "${expected}")
                    ],
                    ["expected"])
            ],
            validateOnly: true,
            evidence: new SemanticWorkflowEvidenceOptions(
                exportReports: false,
                policy: new RuntimeEvidencePolicy(
                    evidenceRoot,
                    allowedActions:
                    [
                        SemanticWorkflowActions.If,
                        SemanticWorkflowActions.UseFragment,
                        SemanticWorkflowActions.Invoke,
                        SemanticWorkflowActions.AssertState
                    ])));
        await File.WriteAllTextAsync(requestPath, JsonSerializer.Serialize(request, JsonOptions));

        try
        {
            var result = await RunCliAsync(
                cliAssembly,
                "run-workflow",
                "--request",
                requestPath,
                "--manifest-dir",
                manifestDirectory);

            Assert.Equal(0, result.ExitCode);
            var payload = JsonSerializer.Deserialize<ToolResult<SemanticWorkflowResponse>>(result.StandardOutput, JsonOptions);
            Assert.True(payload!.Success, payload.Error?.Message);
            Assert.Equal("validated", payload.Value!.Status);
            Assert.Empty(payload.Value.Steps);
            Assert.True(payload.Value.Plan!.Valid);
            Assert.Equal(4, payload.Value.Plan.ExpandedStepCount);
            Assert.Contains(payload.Value.Plan.Steps, step => step.SourceFragment == "assert-status");
            Assert.Equal("explicit_local_opt_in", payload.Value.Metadata["evidencePolicy"]);
            Assert.Equal("disabled", payload.Value.Metadata["networkUpload"]);
            Assert.False(Directory.Exists(evidenceRoot));
        }
        finally
        {
            Directory.Delete(manifestDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task RunWorkflowCommandPreservesActionFailureEvidenceAndReports()
    {
        var cliAssembly = Path.Combine(AppContext.BaseDirectory, "avascope.dll");
        var directory = Path.Combine(
            Path.GetTempPath(),
            "AvaScope.Tests",
            $"cli-workflow-evidence-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        var requestPath = Path.Combine(directory, "workflow.json");
        var outputDirectory = Path.Combine(directory, "output");
        var request = new SemanticWorkflowRequest(
            new SessionId("missing-evidence-session"),
            "topLevel:main",
            [
                new SemanticWorkflowStep(
                    SemanticWorkflowActions.Invoke,
                    "save",
                    new SemanticWorkflowSelector(automationId: "save"),
                    verify: new SemanticWorkflowVerification(
                        new SemanticWaitCondition(SemanticWaitConditionKinds.Text, "saved"),
                        new SemanticWorkflowSelector(automationId: "status"),
                        captureBefore: false,
                        captureAfter: false))
            ],
            outputDirectory: outputDirectory,
            evidence: new SemanticWorkflowEvidenceOptions(
                includeScreenshot: false,
                includeVisualTree: false,
                includeActiveTopLevels: false,
                includeSelectorCandidates: false,
                reportDirectory: Path.Combine(outputDirectory, "reports")));
        await File.WriteAllTextAsync(requestPath, JsonSerializer.Serialize(request, JsonOptions));

        try
        {
            var result = await RunCliAsync(
                cliAssembly,
                "run-workflow",
                "--request",
                requestPath,
                "--manifest-dir",
                directory);

            Assert.Equal(1, result.ExitCode);
            var payload = JsonSerializer.Deserialize<ToolResult<SemanticWorkflowResponse>>(result.StandardOutput, JsonOptions);
            Assert.False(payload!.Success);
            Assert.Equal(CoreErrorCodes.BridgeSessionNotFound, payload.Error!.Code);
            var step = Assert.Single(payload.Value!.Steps);
            Assert.Equal("not_run", step.Verification!.Status);
            Assert.NotNull(step.FailureEvidence);
            Assert.True(File.Exists(step.FailureEvidence.WorkflowContextPath));
            Assert.Equal("failed", payload.Value.ReportPack!.Status);
            Assert.All(payload.Value.ReportPack.Assets, asset => Assert.True(File.Exists(asset.Path), asset.Path));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task MutateNodeCommandRejectsNonCanonicalOperationBeforeDispatch()
    {
        var cliAssembly = Path.Combine(AppContext.BaseDirectory, "avascope.dll");

        var result = await RunCliAsync(
            cliAssembly,
            "mutate-node",
            "--session",
            "schema-parity",
            "--top-level",
            "topLevel:main",
            "--node",
            "visual:1",
            "--operation",
            "setProperty");

        Assert.Equal(2, result.ExitCode);
        var payload = JsonSerializer.Deserialize<ToolResult<RuntimeMutationResponse>>(
            result.StandardOutput,
            JsonOptions);
        Assert.NotNull(payload);
        Assert.False(payload.Success);
        Assert.Equal("invalid_cli_arguments", payload.Error!.Code);
        Assert.Contains(RuntimeMutationOperationKinds.SetProperty, payload.Error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CloseSessionCommandPreservesClosedSessionWhenRequestedProcessIsNotOwned()
    {
        var cliAssembly = Path.Combine(AppContext.BaseDirectory, "avascope.dll");
        var sessionId = SessionId.New();
        var pipeName = TestPipeNames.New();
        var manifestDirectory = Path.Combine(
            Path.GetTempPath(),
            "AvaScope.Tests",
            $"cli-close-partial-{Guid.NewGuid():N}");
        var manifestPath = WriteBridgeManifest(sessionId, pipeName, manifestDirectory);
        var closedAt = DateTimeOffset.UtcNow;
        var serverTask = RespondToBridgeRequestAsync(pipeName, request =>
            BridgeIpcResponse.Ok(
                request.RequestId,
                new CloseSessionResponse(
                    new SessionSummary(
                        sessionId,
                        SessionKinds.Runtime,
                        SessionStates.Closed,
                        closedAt,
                        "CLI fake bridge"),
                    Environment.ProcessId,
                    closedAt)));

        try
        {
            var result = await RunCliAsync(
                cliAssembly,
                "close-session",
                "--session",
                sessionId.Value,
                "--terminate-launched-process",
                "true",
                "--manifest-dir",
                manifestDirectory);
            await serverTask;

            Assert.Equal(1, result.ExitCode);
            var payload = JsonSerializer.Deserialize<ToolResult<CloseSessionResponse>>(
                result.StandardOutput,
                JsonOptions);
            Assert.NotNull(payload);
            Assert.False(payload.Success);
            Assert.True(payload.TransportSuccess);
            Assert.Equal("launched_process_not_owned", payload.Error!.Code);
            Assert.Equal("true", payload.Error.Details!["sessionClosed"]);
            Assert.NotNull(payload.Value);
            Assert.Equal(SessionStates.Closed, payload.Value!.Session.State);
            Assert.Equal(CloseSessionOutcomes.NotOwned, payload.Value.Outcome);
        }
        finally
        {
            if (File.Exists(manifestPath))
            {
                File.Delete(manifestPath);
            }

            if (Directory.Exists(manifestDirectory))
            {
                Directory.Delete(manifestDirectory, recursive: true);
            }
        }
    }

    [Fact]
    public async Task CloseSessionCommandRejectsMissingSession()
    {
        var cliAssembly = Path.Combine(AppContext.BaseDirectory, "avascope.dll");
        Assert.True(File.Exists(cliAssembly), $"Expected CLI assembly at {cliAssembly}.");

        var result = await RunCliAsync(cliAssembly, "close-session");

        Assert.Equal(2, result.ExitCode);
        Assert.True(string.IsNullOrWhiteSpace(result.StandardError), result.StandardError);

        var payload = JsonSerializer.Deserialize<ToolResult<CloseSessionResponse>>(result.StandardOutput, JsonOptions);
        Assert.NotNull(payload);
        Assert.False(payload.Success);
        Assert.Equal("invalid_cli_arguments", payload.Error!.Code);
    }

    [Fact]
    public async Task DoctorCommandReportsLocalReadiness()
    {
        var cliAssembly = Path.Combine(AppContext.BaseDirectory, "avascope.dll");
        Assert.True(File.Exists(cliAssembly), $"Expected CLI assembly at {cliAssembly}.");

        var testRoot = Path.Combine(Path.GetTempPath(), "AvaScope.Tests", Guid.NewGuid().ToString("N"));
        var manifestDirectory = Path.Combine(testRoot, "sessions");
        var previewSessionStoreDirectory = Path.Combine(testRoot, "preview-sessions");
        Directory.CreateDirectory(testRoot);

        try
        {
            var result = await RunCliAsync(
                cliAssembly,
                "doctor",
                "--manifest-dir",
                manifestDirectory,
                "--preview-session-store",
                previewSessionStoreDirectory);

            Assert.Equal(0, result.ExitCode);
            Assert.True(string.IsNullOrWhiteSpace(result.StandardError), result.StandardError);

            var payload = JsonSerializer.Deserialize<ToolResult<DoctorResponse>>(result.StandardOutput, JsonOptions);
            Assert.NotNull(payload);
            Assert.True(payload.Success, payload.Error?.Message);
            Assert.Equal(DiagnosticStatuses.Available, payload.Value!.Status);
            Assert.Equal(AvaScopeProduct.Version, payload.Value.ProductVersion);
            Assert.Equal(AvaScopeProduct.Version, payload.Value.Service.ProductVersion);
            Assert.Equal(Path.GetFullPath(manifestDirectory), payload.Value.ManifestDirectory);
            Assert.Equal(Path.GetFullPath(previewSessionStoreDirectory), payload.Value.PreviewSessionStoreDirectory);
            Assert.Equal(DiagnosticStatuses.Available, payload.Value.PreviewHost!.Status);
            Assert.Empty(payload.Value.BridgeSessions);
            Assert.Empty(payload.Value.PreviewSessions);
            Assert.Empty(payload.Value.Issues);
            Assert.Contains(payload.Value.Checks, static check => check.Name == "cli_assembly" && check.Status == DiagnosticStatuses.Available);
            Assert.Contains(payload.Value.Checks, static check => check.Name == "mcp_assembly" && check.Status == DiagnosticStatuses.Available);
            Assert.Contains(payload.Value.Checks, static check => check.Name == "preview_host" && check.Status == DiagnosticStatuses.Available);
            Assert.Contains(payload.Value.Checks, static check => check.Name == "bridge_sessions" && check.Status == DiagnosticStatuses.Available);
            Assert.Contains(payload.Value.Checks, static check => check.Name == "preview_sessions" && check.Status == DiagnosticStatuses.Available);
        }
        finally
        {
            if (Directory.Exists(testRoot))
            {
                Directory.Delete(testRoot, recursive: true);
            }
        }
    }

    [Fact]
    public async Task DoctorCommandRejectsInvalidArguments()
    {
        var cliAssembly = Path.Combine(AppContext.BaseDirectory, "avascope.dll");
        Assert.True(File.Exists(cliAssembly), $"Expected CLI assembly at {cliAssembly}.");

        var result = await RunCliAsync(cliAssembly, "doctor", "--unknown", "value");

        Assert.Equal(2, result.ExitCode);
        Assert.True(string.IsNullOrWhiteSpace(result.StandardError), result.StandardError);

        var payload = JsonSerializer.Deserialize<ToolResult<DoctorResponse>>(result.StandardOutput, JsonOptions);
        Assert.NotNull(payload);
        Assert.False(payload.Success);
        Assert.Equal("invalid_cli_arguments", payload.Error!.Code);
    }

    [Fact]
    public async Task DiagnosticsCommandReadsBridgeHealthThroughPipe()
    {
        var cliAssembly = Path.Combine(AppContext.BaseDirectory, "avascope.dll");
        Assert.True(File.Exists(cliAssembly), $"Expected CLI assembly at {cliAssembly}.");

        var sessionId = SessionId.New();
        var pipeName = TestPipeNames.New();
        var manifestPath = WriteBridgeManifest(sessionId, pipeName);
        var environment = CreateIsolatedPreviewSessionEnvironment();

        var serverTask = RespondToBridgeRequestAsync(pipeName, request =>
        {
            Assert.Equal(BridgeIpcMethods.Health, request.Method);
            return BridgeIpcResponse.Ok(request.RequestId, HealthResponse.Current());
        });

        try
        {
            var result = await RunCliAsyncWithEnvironment(
                environment,
                cliAssembly,
                "diagnostics",
                "--session",
                sessionId.Value,
                "--max-sessions",
                "1");
            var request = await serverTask;

            Assert.Equal(0, result.ExitCode);
            Assert.Equal(BridgeIpcMethods.Health, request.Method);
            Assert.True(string.IsNullOrWhiteSpace(result.StandardError), result.StandardError);

            var payload = JsonSerializer.Deserialize<ToolResult<DiagnosticsResponse>>(result.StandardOutput, JsonOptions);
            Assert.NotNull(payload);
            Assert.True(payload.Success, payload.Error?.Message);
            Assert.Equal(DiagnosticStatuses.Available, payload.Value!.PreviewHost!.Status);
            var bridge = Assert.Single(payload.Value.BridgeSessions);
            Assert.Equal(DiagnosticStatuses.Available, bridge.Status);
            Assert.Equal(sessionId, bridge.Session!.SessionId);
            Assert.Equal(pipeName, bridge.PipeName);
            Assert.NotNull(bridge.Health);
            Assert.Empty(payload.Value.Issues);
            Assert.Empty(payload.Value.DiagnosticIssues);
            var cliOrigin = Assert.Single(payload.Value.ComponentOrigins, origin => origin.Component == "cli");
            Assert.Equal(Path.GetFullPath(AppContext.BaseDirectory), cliOrigin.BaseDirectory);
            Assert.Equal("repository", cliOrigin.OriginKind);
            Assert.True(cliOrigin.Exists);
            var mcpOrigin = Assert.Single(payload.Value.ComponentOrigins, origin => origin.Component == "mcp");
            Assert.EndsWith("AvaScope.Mcp.dll", mcpOrigin.AssemblyPath, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(cliOrigin.RootDirectory, mcpOrigin.RootDirectory);
            var previewHostOrigin = Assert.Single(payload.Value.ComponentOrigins, origin => origin.Component == "previewHost");
            Assert.Equal(payload.Value.PreviewHost.HostAssemblyPath, previewHostOrigin.AssemblyPath);
            Assert.Equal(cliOrigin.RootDirectory, previewHostOrigin.RootDirectory);
        }
        finally
        {
            if (File.Exists(manifestPath))
            {
                File.Delete(manifestPath);
            }
        }
    }

    [Fact]
    public async Task DiagnosticsCommandReturnsStructuredIssueWhenNoBridgeSessionMatches()
    {
        var cliAssembly = Path.Combine(AppContext.BaseDirectory, "avascope.dll");
        Assert.True(File.Exists(cliAssembly), $"Expected CLI assembly at {cliAssembly}.");
        var environment = CreateIsolatedPreviewSessionEnvironment();

        var result = await RunCliAsyncWithEnvironment(
            environment,
            cliAssembly,
            "diagnostics",
            "--session",
            SessionId.New().Value);

        Assert.Equal(0, result.ExitCode);
        Assert.True(string.IsNullOrWhiteSpace(result.StandardError), result.StandardError);

        var payload = JsonSerializer.Deserialize<ToolResult<DiagnosticsResponse>>(result.StandardOutput, JsonOptions);
        Assert.NotNull(payload);
        Assert.True(payload.Success, payload.Error?.Message);
        Assert.Empty(payload.Value!.BridgeSessions);
        var issue = Assert.Single(payload.Value.Issues);
        Assert.Equal(CoreErrorCodes.BridgeSessionNotFound, issue.Code);
        var diagnosticIssue = Assert.Single(payload.Value.DiagnosticIssues);
        Assert.Equal(DiagnosticIssueSources.Diagnostics, diagnosticIssue.Source);
        Assert.Equal(DiagnosticIssueSeverities.Warning, diagnosticIssue.Severity);
        Assert.Equal(CoreErrorCodes.BridgeSessionNotFound, diagnosticIssue.Code);
        Assert.Equal("diagnostics_summary", diagnosticIssue.Provenance);
        Assert.NotNull(payload.Value.PreviewHost);
    }

    [Fact]
    public async Task DiagnosticsCommandRejectsInvalidProcessId()
    {
        var cliAssembly = Path.Combine(AppContext.BaseDirectory, "avascope.dll");
        Assert.True(File.Exists(cliAssembly), $"Expected CLI assembly at {cliAssembly}.");

        var result = await RunCliAsync(cliAssembly, "diagnostics", "--process", "abc");

        Assert.Equal(2, result.ExitCode);
        Assert.True(string.IsNullOrWhiteSpace(result.StandardError), result.StandardError);

        var payload = JsonSerializer.Deserialize<ToolResult<DiagnosticsResponse>>(result.StandardOutput, JsonOptions);
        Assert.NotNull(payload);
        Assert.False(payload.Success);
        Assert.Equal("invalid_cli_arguments", payload.Error!.Code);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("101")]
    public async Task DiagnosticsCommandRejectsInvalidMaxSessions(string maxSessions)
    {
        var cliAssembly = Path.Combine(AppContext.BaseDirectory, "avascope.dll");
        Assert.True(File.Exists(cliAssembly), $"Expected CLI assembly at {cliAssembly}.");

        var result = await RunCliAsync(cliAssembly, "diagnostics", "--max-sessions", maxSessions);

        Assert.Equal(2, result.ExitCode);
        Assert.True(string.IsNullOrWhiteSpace(result.StandardError), result.StandardError);

        var payload = JsonSerializer.Deserialize<ToolResult<DiagnosticsResponse>>(result.StandardOutput, JsonOptions);
        Assert.NotNull(payload);
        Assert.False(payload.Success);
        Assert.Equal("invalid_cli_arguments", payload.Error!.Code);
    }

    [Fact]
    public async Task ReloadCommandRejectsActiveRuntimeBridgeSessionWithExplicitUnsupportedError()
    {
        var cliAssembly = Path.Combine(AppContext.BaseDirectory, "avascope.dll");
        Assert.True(File.Exists(cliAssembly), $"Expected CLI assembly at {cliAssembly}.");

        var sessionId = SessionId.New();
        var pipeName = TestPipeNames.New();
        var manifestPath = WriteBridgeManifest(sessionId, pipeName);

        var serverTask = RespondToBridgeRequestAsync(pipeName, request =>
        {
            Assert.Equal(BridgeIpcMethods.Health, request.Method);
            return BridgeIpcResponse.Ok(request.RequestId, HealthResponse.Current());
        });

        try
        {
            var result = await RunCliAsync(cliAssembly, "reload", "--session", sessionId.Value);
            var request = await serverTask;

            Assert.Equal(1, result.ExitCode);
            Assert.Equal(BridgeIpcMethods.Health, request.Method);
            Assert.True(string.IsNullOrWhiteSpace(result.StandardError), result.StandardError);

            var payload = JsonSerializer.Deserialize<ToolResult<SessionSummary>>(result.StandardOutput, JsonOptions);
            Assert.NotNull(payload);
            Assert.False(payload.Success);
            Assert.Equal(CoreErrorCodes.RuntimeReloadNotSupported, payload.Error!.Code);
            Assert.Contains("verified the local bridge session is active", payload.Error.Message, StringComparison.Ordinal);
        }
        finally
        {
            if (File.Exists(manifestPath))
            {
                File.Delete(manifestPath);
            }
        }
    }

    [Fact]
    public async Task ReloadCommandReturnsStructuredErrorWhenNoBridgeSessionMatches()
    {
        var cliAssembly = Path.Combine(AppContext.BaseDirectory, "avascope.dll");
        Assert.True(File.Exists(cliAssembly), $"Expected CLI assembly at {cliAssembly}.");

        var result = await RunCliAsync(cliAssembly, "reload", "--session", "missing");

        Assert.Equal(1, result.ExitCode);
        Assert.True(string.IsNullOrWhiteSpace(result.StandardError), result.StandardError);

        var payload = JsonSerializer.Deserialize<ToolResult<SessionSummary>>(result.StandardOutput, JsonOptions);
        Assert.NotNull(payload);
        Assert.False(payload.Success);
        Assert.Equal(CoreErrorCodes.BridgeSessionNotFound, payload.Error!.Code);
    }

    [Fact]
    public async Task ReloadCommandRejectsMissingSession()
    {
        var cliAssembly = Path.Combine(AppContext.BaseDirectory, "avascope.dll");
        Assert.True(File.Exists(cliAssembly), $"Expected CLI assembly at {cliAssembly}.");

        var result = await RunCliAsync(cliAssembly, "reload");

        Assert.Equal(2, result.ExitCode);
        Assert.True(string.IsNullOrWhiteSpace(result.StandardError), result.StandardError);

        var payload = JsonSerializer.Deserialize<ToolResult<SessionSummary>>(result.StandardOutput, JsonOptions);
        Assert.NotNull(payload);
        Assert.False(payload.Success);
        Assert.Equal("invalid_cli_arguments", payload.Error!.Code);
    }

    [Fact]
    public async Task ScreenshotCommandReturnsStructuredErrorWhenNoBridgeSessionMatches()
    {
        var cliAssembly = Path.Combine(AppContext.BaseDirectory, "avascope.dll");
        Assert.True(File.Exists(cliAssembly), $"Expected CLI assembly at {cliAssembly}.");

        var screenshotPath = Path.Combine(
            Path.GetTempPath(),
            "AvaScope.Tests",
            $"{Guid.NewGuid():N}.png");

        var result = await RunCliAsync(
            cliAssembly,
            "screenshot",
            "--session",
            "missing",
            "--top-level",
            "topLevel:missing",
            "--out",
            screenshotPath);

        Assert.Equal(1, result.ExitCode);
        Assert.True(string.IsNullOrWhiteSpace(result.StandardError), result.StandardError);
        Assert.False(File.Exists(screenshotPath));

        var payload = JsonSerializer.Deserialize<ToolResult<ScreenshotResponse>>(result.StandardOutput, JsonOptions);
        Assert.NotNull(payload);
        Assert.False(payload.Success);
        Assert.Equal(CoreErrorCodes.BridgeSessionNotFound, payload.Error!.Code);
    }

    [Fact]
    public async Task ScreenshotCommandCapturesThroughBridgePipe()
    {
        var cliAssembly = Path.Combine(AppContext.BaseDirectory, "avascope.dll");
        Assert.True(File.Exists(cliAssembly), $"Expected CLI assembly at {cliAssembly}.");

        var sessionId = SessionId.New();
        var pipeName = TestPipeNames.New();
        var manifestPath = WriteBridgeManifest(sessionId, pipeName);
        var outputDirectory = Path.Combine(Path.GetTempPath(), "AvaScope.Tests", Guid.NewGuid().ToString("N"));
        var outputPath = Path.Combine(outputDirectory, "cli-screenshot.png");
        Directory.CreateDirectory(outputDirectory);

        var serverTask = RespondToBridgeRequestAsync(pipeName, request =>
        {
            Assert.Equal(BridgeIpcMethods.Screenshot, request.Method);
            Assert.Equal("topLevel:cli", request.TopLevelId);
            Assert.Equal(outputPath, request.OutputPath);

            File.WriteAllBytes(outputPath, [1, 2, 3]);
            return BridgeIpcResponse.Ok(
                request.RequestId,
                new ScreenshotResponse(
                    sessionId,
                    request.TopLevelId!,
                    Path.GetFullPath(request.OutputPath!),
                    320,
                    200,
                    DateTimeOffset.UtcNow));
        });

        try
        {
            var result = await RunCliAsync(
                cliAssembly,
                "screenshot",
                "--session",
                sessionId.Value,
                "--top-level",
                "topLevel:cli",
                "--out",
                outputPath);
            var request = await serverTask;

            Assert.Equal(0, result.ExitCode);
            Assert.Equal(BridgeIpcMethods.Screenshot, request.Method);
            Assert.True(string.IsNullOrWhiteSpace(result.StandardError), result.StandardError);

            var payload = JsonSerializer.Deserialize<ToolResult<ScreenshotResponse>>(result.StandardOutput, JsonOptions);
            Assert.NotNull(payload);
            Assert.True(payload.Success, payload.Error?.Message);
            Assert.Equal(sessionId, payload.Value!.SessionId);
            Assert.Equal("topLevel:cli", payload.Value.TopLevelId);
            Assert.Equal(Path.GetFullPath(outputPath), payload.Value.FilePath);
            Assert.True(File.Exists(payload.Value.FilePath));
            Assert.True(new FileInfo(payload.Value.FilePath).Length > 0);
        }
        finally
        {
            if (File.Exists(manifestPath))
            {
                File.Delete(manifestPath);
            }

            if (Directory.Exists(outputDirectory))
            {
                Directory.Delete(outputDirectory, recursive: true);
            }
        }
    }

    [Fact]
    public async Task ScreenshotCommandRejectsMissingOutputPath()
    {
        var cliAssembly = Path.Combine(AppContext.BaseDirectory, "avascope.dll");
        Assert.True(File.Exists(cliAssembly), $"Expected CLI assembly at {cliAssembly}.");

        var result = await RunCliAsync(
            cliAssembly,
            "screenshot",
            "--session",
            "missing",
            "--top-level",
            "topLevel:missing");

        Assert.Equal(2, result.ExitCode);
        Assert.True(string.IsNullOrWhiteSpace(result.StandardError), result.StandardError);

        var payload = JsonSerializer.Deserialize<ToolResult<ScreenshotResponse>>(result.StandardOutput, JsonOptions);
        Assert.NotNull(payload);
        Assert.False(payload.Success);
        Assert.Equal("invalid_cli_arguments", payload.Error!.Code);
    }

    [Fact]
    public async Task CleanupBridgeSessionsCommandDeletesStaleAndInvalidCustomManifestRecords()
    {
        var cliAssembly = Path.Combine(AppContext.BaseDirectory, "avascope.dll");
        Assert.True(File.Exists(cliAssembly), $"Expected CLI assembly at {cliAssembly}.");

        var manifestDirectory = Path.Combine(Path.GetTempPath(), "AvaScope.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(manifestDirectory);
        var staleManifestPath = WriteBridgeManifest(
            new SessionId("session-stale"),
            "avascope-stale",
            manifestDirectory,
            processId: int.MaxValue);
        var invalidManifestPath = Path.Combine(manifestDirectory, "invalid.json");
        await File.WriteAllTextAsync(invalidManifestPath, "{");

        try
        {
            var result = await RunCliAsync(
                cliAssembly,
                "cleanup-bridge-sessions",
                "--manifest-dir",
                manifestDirectory);

            Assert.Equal(0, result.ExitCode);
            Assert.True(string.IsNullOrWhiteSpace(result.StandardError), result.StandardError);
            Assert.False(File.Exists(staleManifestPath));
            Assert.False(File.Exists(invalidManifestPath));

            var payload = JsonSerializer.Deserialize<ToolResult<BridgeCleanupResponse>>(result.StandardOutput, JsonOptions);
            Assert.NotNull(payload);
            Assert.True(payload.Success, payload.Error?.Message);
            Assert.Equal(2, payload.Value!.DeletedBridgeManifestRecords);
            Assert.Contains(payload.Value.CleanupCandidates, candidate => candidate.Status == DiagnosticStatuses.Stale);
            Assert.Contains(payload.Value.CleanupCandidates, candidate => candidate.Status == DiagnosticStatuses.Invalid);
        }
        finally
        {
            if (Directory.Exists(manifestDirectory))
            {
                Directory.Delete(manifestDirectory, recursive: true);
            }
        }
    }

    [Fact]
    public async Task DiffCommandWritesDiffAndReturnsChangedSummary()
    {
        var cliAssembly = Path.Combine(AppContext.BaseDirectory, "avascope.dll");
        Assert.True(File.Exists(cliAssembly), $"Expected CLI assembly at {cliAssembly}.");

        var testRoot = Path.Combine(Path.GetTempPath(), "AvaScope.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(testRoot);

        var baselinePath = Path.Combine(testRoot, "baseline.png");
        var currentPath = Path.Combine(testRoot, "current.png");
        var diffPath = Path.Combine(testRoot, "diff.png");
        WriteSolidImage(baselinePath, SKColors.White);
        WriteSolidImage(currentPath, SKColors.White, changedPixel: SKColors.Black);

        try
        {
            var result = await RunCliAsync(
                cliAssembly,
                "diff",
                "--baseline",
                baselinePath,
                "--current",
                currentPath,
                "--out",
                diffPath,
                "--tolerance",
                "0");

            Assert.Equal(1, result.ExitCode);
            Assert.True(string.IsNullOrWhiteSpace(result.StandardError), result.StandardError);

            var payload = JsonSerializer.Deserialize<ToolResult<PreviewDiffResponse>>(result.StandardOutput, JsonOptions);
            Assert.NotNull(payload);
            Assert.True(payload.Success, payload.Error?.Message);
            Assert.False(payload.Value!.Passed);
            Assert.Equal(1, payload.Value.ChangedPixels);
            Assert.Equal(16, payload.Value.TotalPixels);
            Assert.Equal(Path.GetFullPath(diffPath), payload.Value.DiffPath);
            Assert.True(File.Exists(diffPath));
        }
        finally
        {
            if (Directory.Exists(testRoot))
            {
                Directory.Delete(testRoot, recursive: true);
            }
        }
    }

    [Fact]
    public async Task SemanticDiffCommandWritesAnnotatedArtifactsAndBoundedFindings()
    {
        var cliAssembly = Path.Combine(AppContext.BaseDirectory, "avascope.dll");
        Assert.True(File.Exists(cliAssembly), $"Expected CLI assembly at {cliAssembly}.");

        var testRoot = Path.Combine(Path.GetTempPath(), "AvaScope.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(testRoot);

        var referencePath = Path.Combine(testRoot, "reference.png");
        var currentPath = Path.Combine(testRoot, "current.png");
        var outputDirectory = Path.Combine(testRoot, "semantic");
        WriteSemanticFixture(referencePath, shifted: false, border: false);
        WriteSemanticFixture(currentPath, shifted: true, border: true);

        try
        {
            var result = await RunCliAsync(
                cliAssembly,
                "semantic-diff",
                "--reference",
                referencePath,
                "--current",
                currentPath,
                "--out-dir",
                outputDirectory,
                "--request-id",
                "cli-semantic",
                "--max-findings",
                "8",
                "--max-raw-regions",
                "8");

            Assert.Equal(1, result.ExitCode);
            Assert.True(string.IsNullOrWhiteSpace(result.StandardError), result.StandardError);

            var payload = JsonSerializer.Deserialize<ToolResult<SemanticScreenshotComparisonResponse>>(result.StandardOutput, JsonOptions);
            Assert.NotNull(payload);
            Assert.True(payload.Success, payload.Error?.Message);
            Assert.Equal("differences_found", payload.Value!.Status);
            Assert.Contains(payload.Value.Findings, finding => finding.Kind == SemanticScreenshotFindingKinds.PaddingDifference);
            Assert.Contains(payload.Value.Findings, finding => finding.Kind == SemanticScreenshotFindingKinds.BorderOrSeamDifference);
            Assert.True(File.Exists(payload.Value.AnnotatedPath), payload.Value.AnnotatedPath);
            Assert.True(File.Exists(payload.Value.RawDiff.DiffPath), payload.Value.RawDiff.DiffPath);
            Assert.All(payload.Value.RawRegions, region =>
            {
                Assert.True(File.Exists(region.CropPath), region.CropPath);
                Assert.True(File.Exists(region.AnnotatedCropPath), region.AnnotatedCropPath);
            });
            Assert.Contains(payload.Value.AgentReview.ArtifactPaths, artifact => artifact.Kind == "semantic_annotation");
        }
        finally
        {
            if (Directory.Exists(testRoot))
            {
                Directory.Delete(testRoot, recursive: true);
            }
        }
    }

    [Fact]
    public async Task AssertRegionCommandChecksNonEmptyRegionAndWritesCrop()
    {
        var cliAssembly = Path.Combine(AppContext.BaseDirectory, "avascope.dll");
        Assert.True(File.Exists(cliAssembly), $"Expected CLI assembly at {cliAssembly}.");

        var testRoot = Path.Combine(Path.GetTempPath(), "AvaScope.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(testRoot);
        var imagePath = Path.Combine(testRoot, "current.png");
        var cropPath = Path.Combine(testRoot, "crop.png");
        WriteSolidPng(imagePath, 10, 10, SKColors.White, (2, 2, SKColors.Black));

        try
        {
            var result = await RunCliAsync(
                cliAssembly,
                "assert-region",
                "--image",
                imagePath,
                "--assert",
                "non_empty",
                "--x",
                "0",
                "--y",
                "0",
                "--width",
                "4",
                "--height",
                "4",
                "--crop-out",
                cropPath);

            Assert.Equal(0, result.ExitCode);
            var payload = JsonSerializer.Deserialize<ToolResult<ScreenshotRegionAssertionResponse>>(result.StandardOutput, JsonOptions);
            Assert.NotNull(payload);
            Assert.True(payload.Success, payload.Error?.Message);
            Assert.True(payload.Value!.Passed);
            Assert.Equal(1, payload.Value.NonBlankPixels);
            Assert.True(File.Exists(cropPath));
        }
        finally
        {
            if (Directory.Exists(testRoot))
            {
                Directory.Delete(testRoot, recursive: true);
            }
        }
    }

    [Fact]
    public async Task LaunchAppCommandReturnsStructuredErrorWhenNoBridgeSessionAppears()
    {
        var cliAssembly = Path.Combine(AppContext.BaseDirectory, "avascope.dll");
        Assert.True(File.Exists(cliAssembly), $"Expected CLI assembly at {cliAssembly}.");

        var testRoot = Path.Combine(Path.GetTempPath(), "AvaScope.Tests", Guid.NewGuid().ToString("N"));
        var manifestDirectory = Path.Combine(testRoot, "manifests");
        var outputDirectory = Path.Combine(testRoot, "launch");
        Directory.CreateDirectory(testRoot);

        try
        {
            var result = await RunCliAsync(
                cliAssembly,
                "launch-app",
                "--command",
                "dotnet",
                "--args",
                "--info",
                "--manifest-dir",
                manifestDirectory,
                "--out-dir",
                outputDirectory,
                "--timeout-ms",
                "3000");

            Assert.Equal(1, result.ExitCode);
            var payload = JsonSerializer.Deserialize<ToolResult<LaunchAppResponse>>(result.StandardOutput, JsonOptions);
            Assert.NotNull(payload);
            Assert.False(payload.Success);
            Assert.Equal(CoreErrorCodes.BridgeSessionNotFound, payload.Error!.Code);
            Assert.True(File.Exists(payload.Error.Details!["stdoutPath"]));
            Assert.True(File.Exists(payload.Error.Details["stderrPath"]));
            Assert.Equal(Path.GetFullPath(manifestDirectory), payload.Error.Details["manifestDirectory"]);
        }
        finally
        {
            if (Directory.Exists(testRoot))
            {
                Directory.Delete(testRoot, recursive: true);
            }
        }
    }

    [Fact]
    public async Task LaunchAppCommandReturnsTimeoutAndExitsWhenProcessKeepsRunningWithoutBridge()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var cliAssembly = Path.Combine(AppContext.BaseDirectory, "avascope.dll");
        Assert.True(File.Exists(cliAssembly), $"Expected CLI assembly at {cliAssembly}.");

        var testRoot = Path.Combine(Path.GetTempPath(), "AvaScope.Tests", Guid.NewGuid().ToString("N"));
        var manifestDirectory = Path.Combine(testRoot, "manifests");
        var outputDirectory = Path.Combine(testRoot, "launch");
        Directory.CreateDirectory(testRoot);

        try
        {
            var startedAt = Stopwatch.StartNew();
            var result = await RunCliAsync(
                cliAssembly,
                "launch-app",
                "--command",
                "powershell.exe",
                "--args",
                "-NoProfile -Command \"Start-Sleep -Seconds 5\"",
                "--manifest-dir",
                manifestDirectory,
                "--out-dir",
                outputDirectory,
                "--timeout-ms",
                "500");
            startedAt.Stop();

            Assert.Equal(1, result.ExitCode);
            Assert.True(
                startedAt.Elapsed < TimeSpan.FromSeconds(4),
                $"launch-app should not wait for the child process to exit. Elapsed: {startedAt.Elapsed}");
            var payload = JsonSerializer.Deserialize<ToolResult<LaunchAppResponse>>(result.StandardOutput, JsonOptions);
            Assert.NotNull(payload);
            Assert.False(payload.Success);
            Assert.Equal(CoreErrorCodes.BridgeSessionNotFound, payload.Error!.Code);
            Assert.Contains("Timed out waiting", payload.Error.Message, StringComparison.Ordinal);
            Assert.True(payload.Error.Details!.TryGetValue("processId", out var processIdText));
            Assert.True(int.Parse(processIdText, CultureInfo.InvariantCulture) > 0);
            Assert.True(File.Exists(payload.Error.Details["stdoutPath"]));
            Assert.True(File.Exists(payload.Error.Details["stderrPath"]));
        }
        finally
        {
            await DeleteDirectoryWithRetryAsync(testRoot);
        }
    }

    [Fact]
    public void McpServerAssemblyIsCopiedBesideCli()
    {
        var cliAssembly = Path.Combine(AppContext.BaseDirectory, "avascope.dll");
        var mcpAssembly = Path.Combine(AppContext.BaseDirectory, "AvaScope.Mcp.dll");

        Assert.True(File.Exists(cliAssembly), $"Expected CLI assembly at {cliAssembly}.");
        Assert.True(File.Exists(mcpAssembly), $"Expected MCP assembly at {mcpAssembly}.");
    }

    private static async Task<CliResult> RunCliAsync(string cliAssembly, params string[] arguments)
    {
        return await RunCliAsyncFromDirectory(AppContext.BaseDirectory, cliAssembly, arguments);
    }

    private static IReadOnlyDictionary<string, string> CreateIsolatedPreviewSessionEnvironment()
    {
        return new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [PreviewSessionStore.DirectoryEnvironmentVariable] = Path.Combine(
                Path.GetTempPath(),
                "AvaScope.Tests",
                $"preview-sessions-{Guid.NewGuid():N}")
        };
    }

    private static async Task<CliResult> RunCliAsyncWithEnvironment(
        IReadOnlyDictionary<string, string> environment,
        string cliAssembly,
        params string[] arguments)
    {
        return await RunCliAsyncFromDirectory(AppContext.BaseDirectory, cliAssembly, environment, arguments);
    }

    private static async Task<CliResult> RunCliAsyncWithEnvironmentAfterStart(
        IReadOnlyDictionary<string, string> environment,
        string cliAssembly,
        Func<Task> afterStart,
        params string[] arguments)
    {
        using var process = CreateCliProcess(
            AppContext.BaseDirectory,
            cliAssembly,
            environment,
            arguments);
        Assert.True(process.Start());
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(60));

        var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellation.Token);
        var stderrTask = process.StandardError.ReadToEndAsync(cancellation.Token);
        await afterStart();
        await process.WaitForExitAsync(cancellation.Token);

        return new CliResult(
            process.ExitCode,
            await stdoutTask,
            await stderrTask);
    }

    private static async Task WriteAllTextWithRetryAsync(string path, string contents)
    {
        Exception? lastException = null;
        for (var attempt = 1; attempt <= 20; attempt++)
        {
            try
            {
                await File.WriteAllTextAsync(path, contents);
                return;
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                lastException = exception;
                await Task.Delay(TimeSpan.FromMilliseconds(100 * attempt));
            }
        }

        throw new IOException($"Timed out writing test file '{path}'.", lastException);
    }

    private static async Task DeleteDirectoryWithRetryAsync(string path)
    {
        Exception? lastException = null;
        for (var attempt = 1; attempt <= 20; attempt++)
        {
            try
            {
                if (Directory.Exists(path))
                {
                    Directory.Delete(path, recursive: true);
                }

                return;
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                lastException = exception;
                GC.Collect();
                GC.WaitForPendingFinalizers();
                await Task.Delay(TimeSpan.FromMilliseconds(100 * attempt));
            }
        }

        throw new IOException($"Timed out deleting test directory '{path}'.", lastException);
    }

    private static void WriteSolidImage(string path, SKColor color, SKColor? changedPixel = null)
    {
        using var bitmap = new SKBitmap(4, 4);
        bitmap.Erase(color);
        if (changedPixel is { } pixel)
        {
            bitmap.SetPixel(0, 0, pixel);
        }

        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        using var stream = File.Create(path);
        data.SaveTo(stream);
    }

    private static void WriteSemanticFixture(string path, bool shifted, bool border)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using var bitmap = new SKBitmap(120, 80);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.White);
        using var contentPaint = new SKPaint { Color = SKColors.Black, Style = SKPaintStyle.Fill };
        canvas.DrawRect(new SKRect(shifted ? 36 : 30, 25, shifted ? 76 : 70, 45), contentPaint);

        if (border)
        {
            using var borderPaint = new SKPaint { Color = SKColors.Red, StrokeWidth = 1, Style = SKPaintStyle.Stroke };
            canvas.DrawLine(100, 10, 100, 70, borderPaint);
        }

        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        using var stream = File.Create(path);
        data.SaveTo(stream);
    }

    private static async Task<CliResult> RunCliAsyncFromDirectory(
        string workingDirectory,
        string cliAssembly,
        params string[] arguments)
    {
        return await RunCliAsyncFromDirectory(workingDirectory, cliAssembly, environment: null, arguments);
    }

    private static async Task<CliResult> RunCliAsyncFromDirectory(
        string workingDirectory,
        string cliAssembly,
        IReadOnlyDictionary<string, string>? environment,
        params string[] arguments)
    {
        using var process = CreateCliProcess(workingDirectory, cliAssembly, environment, arguments);
        Assert.True(process.Start());
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(60));

        var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellation.Token);
        var stderrTask = process.StandardError.ReadToEndAsync(cancellation.Token);
        await process.WaitForExitAsync(cancellation.Token);

        return new CliResult(
            process.ExitCode,
            await stdoutTask,
            await stderrTask);
    }

    private static Process CreateCliProcess(
        string workingDirectory,
        string cliAssembly,
        IReadOnlyDictionary<string, string>? environment,
        IReadOnlyList<string> arguments)
    {
        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                WorkingDirectory = workingDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            }
        };

        if (environment is null || !environment.ContainsKey(BridgeSessionManifest.DirectoryEnvironmentVariable))
        {
            process.StartInfo.Environment[BridgeSessionManifest.DirectoryEnvironmentVariable] =
                CurrentBridgeManifestDirectory.Value ?? CreateBridgeManifestDirectory();
        }

        if (environment is not null)
        {
            foreach (var item in environment)
            {
                process.StartInfo.Environment[item.Key] = item.Value;
            }
        }

        process.StartInfo.ArgumentList.Add(cliAssembly);
        foreach (var argument in arguments)
        {
            process.StartInfo.ArgumentList.Add(argument);
        }

        return process;
    }

    private static string WriteBridgeManifest(
        SessionId sessionId,
        string pipeName,
        string? manifestDirectory = null,
        string? fileName = null,
        int? processId = null,
        string? processName = null)
    {
        var directory = string.IsNullOrWhiteSpace(manifestDirectory)
            ? CreateBridgeManifestDirectory()
            : manifestDirectory;
        Directory.CreateDirectory(directory);
        if (string.IsNullOrWhiteSpace(manifestDirectory))
        {
            CurrentBridgeManifestDirectory.Value = directory;
        }

        var manifest = new BridgeSessionManifest(
            sessionId,
            processId ?? Environment.ProcessId,
            pipeName,
            DateTimeOffset.UtcNow,
            "CLI fake bridge",
            processName: processName);
        var manifestPath = Path.Combine(directory, fileName ?? $"{sessionId.Value}.json");
        File.WriteAllText(manifestPath, JsonSerializer.Serialize(manifest), Encoding.UTF8);
        return manifestPath;
    }

    private static string CreateBridgeManifestDirectory()
    {
        return Path.Combine(
            Path.GetTempPath(),
            "AvaScope.Tests",
            $"cli-bridge-manifests-{Guid.NewGuid():N}");
    }

    private static void WriteSolidPng(
        string path,
        int width,
        int height,
        SKColor background,
        params (int X, int Y, SKColor Color)[] pixels)
    {
        using var bitmap = new SKBitmap(width, height);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(background);
        foreach (var pixel in pixels)
        {
            bitmap.SetPixel(pixel.X, pixel.Y, pixel.Color);
        }

        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        using var stream = File.Create(path);
        data.SaveTo(stream);
    }

    private static async Task<BridgeIpcRequest> RespondToBridgeRequestAsync(
        string pipeName,
        Func<BridgeIpcRequest, BridgeIpcResponse> responseFactory)
    {
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        try
        {
            while (true)
            {
                await using var pipe = new NamedPipeServerStream(
                    pipeName,
                    PipeDirection.InOut,
                    maxNumberOfServerInstances: 1,
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous);

                await pipe.WaitForConnectionAsync(cancellation.Token);

                var requestLine = await ReadOptionalLineAsync(pipe, cancellation.Token);
                if (string.IsNullOrWhiteSpace(requestLine))
                {
                    continue;
                }

                BridgeIpcRequest? request;
                try
                {
                    request = JsonSerializer.Deserialize<BridgeIpcRequest>(requestLine, JsonOptions);
                }
                catch (JsonException)
                {
                    continue;
                }

                if (request is null)
                {
                    continue;
                }

                var responseBytes = Encoding.UTF8.GetBytes(
                    JsonSerializer.Serialize(responseFactory(request), JsonOptions) + Environment.NewLine);
                try
                {
                    await pipe.WriteAsync(responseBytes, cancellation.Token);
                    await pipe.FlushAsync(cancellation.Token);
                }
                catch (IOException)
                {
                    return request;
                }

                return request;
            }
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            throw new TimeoutException($"Timed out waiting for a bridge IPC request on pipe '{pipeName}'.");
        }
    }

    private static async Task<IReadOnlyList<BridgeIpcRequest>> RespondToBridgeRequestsAsync(
        string pipeName,
        int expectedCount,
        Func<int, BridgeIpcRequest, BridgeIpcResponse> responseFactory)
    {
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var requests = new List<BridgeIpcRequest>(expectedCount);
        try
        {
            while (requests.Count < expectedCount)
            {
                await using var pipe = new NamedPipeServerStream(
                    pipeName,
                    PipeDirection.InOut,
                    maxNumberOfServerInstances: 1,
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous);

                await pipe.WaitForConnectionAsync(cancellation.Token);

                var requestLine = await ReadOptionalLineAsync(pipe, cancellation.Token);
                if (string.IsNullOrWhiteSpace(requestLine))
                {
                    continue;
                }

                BridgeIpcRequest? request;
                try
                {
                    request = JsonSerializer.Deserialize<BridgeIpcRequest>(requestLine, JsonOptions);
                }
                catch (JsonException)
                {
                    continue;
                }

                if (request is null)
                {
                    continue;
                }

                var index = requests.Count;
                requests.Add(request);
                var responseBytes = Encoding.UTF8.GetBytes(
                    JsonSerializer.Serialize(responseFactory(index, request), JsonOptions) + Environment.NewLine);
                try
                {
                    await pipe.WriteAsync(responseBytes, cancellation.Token);
                    await pipe.FlushAsync(cancellation.Token);
                }
                catch (IOException) when (requests.Count == expectedCount)
                {
                    return requests;
                }
            }

            return requests;
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            throw new TimeoutException($"Timed out waiting for {expectedCount} bridge IPC requests on pipe '{pipeName}'.");
        }
    }

    private static BridgeIpcResponse CreateScenarioFindNodesResponse(
        BridgeIpcRequest request,
        SessionId sessionId,
        RuntimeTargetContext target,
        string automationId,
        string text)
    {
        Assert.Equal(BridgeIpcMethods.FindNodes, request.Method);
        Assert.Equal(target.TopLevelId, request.TopLevelId);
        Assert.Equal(automationId, request.AutomationId);

        var node = new TreeNodeSummary(
            target.NodeId!,
            "Avalonia.Controls.Button",
            automationId,
            automationId,
            text,
            target: target);

        return BridgeIpcResponse.Ok(
            request.RequestId,
            new FindNodesResponse(
                sessionId,
                target.TopLevelId,
                target.TreeKind ?? TreeKinds.Visual,
                request.MaxDepth ?? 16,
                [new FindNodeMatch(node)]));
    }

    private static BridgeIpcResponse CreateWaitInspectResponse(
        BridgeIpcRequest request,
        SessionId sessionId,
        RuntimeTargetContext target,
        string value)
    {
        Assert.Equal(BridgeIpcMethods.InspectNode, request.Method);
        Assert.Equal(target.TopLevelId, request.TopLevelId);
        Assert.Equal(target.NodeId, request.NodeId);
        return BridgeIpcResponse.Ok(
            request.RequestId,
            new InspectNodeResponse(
                sessionId,
                target.TopLevelId,
                target.TreeKind ?? TreeKinds.Visual,
                target.NodeId!,
                "Avalonia.Controls.Slider",
                childCount: 0,
                computedProperties:
                [
                    new ComputedPropertyValue(
                        "Value",
                        value,
                        typeof(double).FullName!,
                        source: "test")
                ],
                target: target));
    }

    private static BridgeIpcResponse CreateScenarioInputResponse(
        BridgeIpcRequest request,
        SessionId sessionId,
        RuntimeTargetContext target,
        string expectedAction)
    {
        Assert.Equal(BridgeIpcMethods.Input, request.Method);
        Assert.Equal(target.TopLevelId, request.TopLevelId);
        Assert.Equal(expectedAction, request.Action);
        Assert.Equal(target.NodeId, request.TargetNodeId);

        return BridgeIpcResponse.Ok(
            request.RequestId,
            new InputResponse(
                sessionId,
                target.TopLevelId,
                expectedAction,
                handled: true,
                DateTimeOffset.UtcNow,
                targetNodeId: target.NodeId,
                target: target));
    }

    private static BridgeIpcResponse CreatePointerInputResponse(BridgeIpcRequest request, SessionId sessionId)
    {
        Assert.Equal(BridgeIpcMethods.Input, request.Method);
        Assert.Equal("topLevel:pointer", request.TopLevelId);
        Assert.Equal(InputActions.PointerMove, request.Action);
        Assert.Equal(12, request.X);
        Assert.Equal(8, request.Y);

        return BridgeIpcResponse.Ok(
            request.RequestId,
            new InputResponse(
                sessionId,
                "topLevel:pointer",
                InputActions.PointerMove,
                handled: true,
                DateTimeOffset.UtcNow,
                "visual:pointerButton"));
    }

    private static BridgeIpcResponse CreatePointerTreeResponse(BridgeIpcRequest request, SessionId sessionId, string topLevelId)
    {
        Assert.Equal(BridgeIpcMethods.VisualTree, request.Method);
        Assert.Equal(topLevelId, request.TopLevelId);

        return BridgeIpcResponse.Ok(
            request.RequestId,
            new TreeResponse(
                sessionId,
                topLevelId,
                TreeKinds.Visual,
                request.MaxDepth ?? 16,
                new TreeNodeSummary(
                    "visual:pointerRoot",
                    "Avalonia.Controls.Window",
                    "PointerWindow",
                    bounds: new NodeBounds(0, 0, 100, 60),
                    children:
                    [
                        new TreeNodeSummary(
                            "visual:pointerButton",
                            "Avalonia.Controls.Button",
                            "PointerButton",
                            automationId: "pointer-button",
                            text: "Hover",
                            bounds: new NodeBounds(0, 0, 50, 24))
                    ])));
    }

    private static BridgeIpcResponse CreatePointerScreenshotResponse(
        BridgeIpcRequest request,
        SessionId sessionId,
        string topLevelId,
        string expectedFileName)
    {
        Assert.Equal(BridgeIpcMethods.Screenshot, request.Method);
        Assert.Equal(topLevelId, request.TopLevelId);
        Assert.NotNull(request.OutputPath);
        Assert.EndsWith(expectedFileName, request.OutputPath, StringComparison.Ordinal);
        WriteSolidPng(request.OutputPath!, 100, 60, SKColors.White);

        return BridgeIpcResponse.Ok(
            request.RequestId,
            new ScreenshotResponse(
                sessionId,
                topLevelId,
                request.OutputPath!,
                100,
                60,
                DateTimeOffset.UtcNow));
    }

    private static BridgeIpcResponse CreateInteractionInputResponse(
        BridgeIpcRequest request,
        SessionId sessionId,
        string topLevelId)
    {
        Assert.Equal(BridgeIpcMethods.Input, request.Method);
        Assert.Equal(topLevelId, request.TopLevelId);
        Assert.Equal(InputActions.Click, request.Action);
        Assert.Equal(40, request.X);
        Assert.Equal(24, request.Y);

        return BridgeIpcResponse.Ok(
            request.RequestId,
            new InputResponse(
                sessionId,
                topLevelId,
                InputActions.Click,
                handled: true,
                DateTimeOffset.UtcNow,
                "visual:button"));
    }

    private static BridgeIpcResponse CreateInteractionTreeResponse(
        BridgeIpcRequest request,
        SessionId sessionId,
        string topLevelId)
    {
        Assert.Equal(BridgeIpcMethods.VisualTree, request.Method);
        Assert.Equal(topLevelId, request.TopLevelId);

        return BridgeIpcResponse.Ok(
            request.RequestId,
            new TreeResponse(
                sessionId,
                topLevelId,
                TreeKinds.Visual,
                request.MaxDepth ?? 16,
                new TreeNodeSummary(
                    "visual:root",
                    "Avalonia.Controls.Window",
                    "InteractionWindow",
                    bounds: new NodeBounds(0, 0, 180, 100),
                    children:
                    [
                        new TreeNodeSummary(
                            "visual:panel",
                            "Avalonia.Controls.Border",
                            "AnimatedPanel",
                            automationId: "animated-panel",
                            bounds: new NodeBounds(16, 10, 120, 48))
                    ])));
    }

    private static BridgeIpcResponse CreateInteractionScreenshotResponse(
        BridgeIpcRequest request,
        SessionId sessionId,
        string topLevelId)
    {
        Assert.Equal(BridgeIpcMethods.Screenshot, request.Method);
        Assert.Equal(topLevelId, request.TopLevelId);
        Assert.NotNull(request.OutputPath);
        WriteSolidPng(request.OutputPath!, 180, 100, SKColors.White);

        return BridgeIpcResponse.Ok(
            request.RequestId,
            new ScreenshotResponse(
                sessionId,
                topLevelId,
                request.OutputPath!,
                180,
                100,
                DateTimeOffset.UtcNow));
    }

    private static BridgeIpcResponse CreateCliEvidenceScreenshotResponse(
        BridgeIpcRequest request,
        SessionId sessionId,
        string topLevelId,
        string expectedFileName)
    {
        Assert.Equal(BridgeIpcMethods.Screenshot, request.Method);
        Assert.Equal(topLevelId, request.TopLevelId);
        Assert.NotNull(request.OutputPath);
        Assert.EndsWith(expectedFileName, request.OutputPath, StringComparison.Ordinal);

        return BridgeIpcResponse.Ok(
            request.RequestId,
            new ScreenshotResponse(
                sessionId,
                topLevelId,
                request.OutputPath!,
                10,
                10,
                DateTimeOffset.UtcNow));
    }

    private static BridgeIpcResponse CreateCliEvidenceTreeResponse(
        BridgeIpcRequest request,
        SessionId sessionId,
        string topLevelId,
        int expectedMaxDepth,
        string targetText)
    {
        Assert.Equal(BridgeIpcMethods.VisualTree, request.Method);
        Assert.Equal(topLevelId, request.TopLevelId);
        Assert.Equal(expectedMaxDepth, request.MaxDepth);

        return BridgeIpcResponse.Ok(
            request.RequestId,
            CreateCliEvidenceTree(sessionId, topLevelId, expectedMaxDepth, targetText));
    }

    private static BridgeIpcResponse CreateCliEvidenceMutationResponse(
        BridgeIpcRequest request,
        SessionId sessionId,
        string topLevelId)
    {
        Assert.Equal(BridgeIpcMethods.MutateNode, request.Method);
        Assert.NotNull(request.Mutation);
        Assert.Equal("cli-evidence", request.Mutation.RequestId);
        Assert.Equal(topLevelId, request.Mutation.Target.TopLevelId);
        Assert.Equal("visual:button", request.Mutation.Target.NodeId);
        Assert.Equal(RuntimeMutationOperationKinds.SetProperty, request.Mutation.Operation.Kind);
        Assert.Equal("Text", request.Mutation.Operation.PropertyName);
        Assert.Equal("After", request.Mutation.Operation.Value);

        return BridgeIpcResponse.Ok(
            request.RequestId,
            new RuntimeMutationResponse(
                request.Mutation.RequestId,
                "mutation:cli:evidence:1",
                sessionId,
                topLevelId,
                request.Mutation.Target,
                request.Mutation.Operation,
                RuntimeMutationStatuses.Applied,
                applied: true,
                DateTimeOffset.UtcNow,
                RuntimeMutationCapabilityCatalog.CurrentBridgeCapabilities()));
    }

    private static TreeResponse CreateCliEvidenceTree(
        SessionId sessionId,
        string topLevelId,
        int depthLimit,
        string targetText)
    {
        return new TreeResponse(
            sessionId,
            topLevelId,
            TreeKinds.Visual,
            depthLimit,
            new TreeNodeSummary(
                "visual:root",
                "Avalonia.Controls.Window",
                "CliEvidenceWindow",
                children:
                [
                    new TreeNodeSummary(
                        "visual:button",
                        "Avalonia.Controls.Button",
                        "CliEvidenceButton",
                        text: targetText,
                        bounds: new NodeBounds(1, 2, 100, 32),
                        classes: ["evidence-target"],
                        target: new RuntimeTargetContext(
                            sessionId,
                            topLevelId,
                            TreeKinds.Visual,
                            "visual:button"))
                ]));
    }

    private static RuntimeNodeSourceMap TestSourceMap(string propertyName, string value)
    {
        return new RuntimeNodeSourceMap(
            "partial",
            "test_property_origins",
            propertyOrigins:
            [
                new RuntimeSourcePropertyOrigin(
                    propertyName,
                    value,
                    "test",
                    "local",
                    "LocalValue")
            ]);
    }

    private static async Task<string?> ReadOptionalLineAsync(Stream stream, CancellationToken cancellationToken)
    {
        var bytes = new List<byte>();
        var buffer = new byte[256];

        while (true)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken);
            if (read == 0)
            {
                return bytes.Count == 0
                    ? null
                    : Encoding.UTF8.GetString(bytes.ToArray());
            }

            for (var index = 0; index < read; index++)
            {
                if (buffer[index] == (byte)'\n')
                {
                    return Encoding.UTF8.GetString(bytes.ToArray());
                }

                if (buffer[index] != (byte)'\r')
                {
                    bytes.Add(buffer[index]);
                }
            }
        }
    }

    private static void AssertSamePath(string expected, string? actual)
    {
        Assert.NotNull(actual);
        Assert.Equal(NormalizePath(expected), NormalizePath(actual));
    }

    private static string NormalizePath(string path)
    {
        var fullPath = Path.GetFullPath(path);
        return OperatingSystem.IsMacOS() && fullPath.StartsWith("/var/", StringComparison.Ordinal)
            ? "/private" + fullPath
            : fullPath;
    }

    private sealed record CliResult(
        int ExitCode,
        string StandardOutput,
        string StandardError);
}
