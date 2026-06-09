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
                "CliPreviewSample.PreviewDesignData");

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
                    await Task.Delay(1000);
                    await WriteAllTextWithRetryAsync(viewPath, """
                        <UserControl xmlns="https://github.com/avaloniaui">
                          <Border Background="#FFFFFFFF">
                            <TextBlock Text="CLI preview watch changed" />
                          </Border>
                        </UserControl>
                        """);
                },
                "watch-preview-session",
                "--session",
                sessionId,
                "--timeout-ms",
                "15000",
                "--settle-ms",
                "1000",
                "--max-reloads",
                "1",
                "--watch",
                viewPath);

            Assert.Equal(0, watched.ExitCode);
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
            if (Directory.Exists(testRoot))
            {
                Directory.Delete(testRoot, recursive: true);
            }
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

            var passed = await RunCliAsync(
                cliAssembly,
                "baseline-check",
                "--manifest",
                manifestPath,
                "--out-dir",
                Path.Combine(testRoot, "current-pass"),
                "--diff-dir",
                Path.Combine(testRoot, "diff-pass"));

            Assert.Equal(0, passed.ExitCode);
            var passedPayload = JsonSerializer.Deserialize<ToolResult<PreviewBaselineCheckResponse>>(
                passed.StandardOutput,
                JsonOptions);
            Assert.NotNull(passedPayload);
            Assert.True(passedPayload.Success, passedPayload.Error?.Message);
            Assert.True(passedPayload.Value!.Passed);

            await Task.Delay(1000);
            await File.WriteAllTextAsync(viewPath, """
                <UserControl xmlns="https://github.com/avaloniaui">
                  <Border Background="#FF000000" />
                </UserControl>
                """);

            var reportPath = Path.Combine(testRoot, "reports", "baseline-check.json");
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
                reportPath);

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
            var reportPayload = JsonSerializer.Deserialize<PreviewBaselineCheckResponse>(
                await File.ReadAllTextAsync(reportPath),
                JsonOptions);
            Assert.NotNull(reportPayload);
            Assert.False(reportPayload.Passed);
            Assert.Equal(Path.GetFullPath(reportPath), reportPayload.ReportPath);
            Assert.Equal(Path.GetFullPath(entry.CurrentImagePath), reportPayload.Entries[0].CurrentImagePath);
            Assert.Equal(Path.GetFullPath(entry.DiffPath), reportPayload.Entries[0].DiffPath);
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
            Assert.Equal(Path.GetFullPath(outputPath), payload.Value!.FilePath);
            Assert.Equal(Path.GetFullPath(projectPath), payload.Value.ProjectPath);
            Assert.Equal(Path.GetFullPath(viewPath), payload.Value.ViewPath);
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
        var pipeName = $"avascope-cli-test-{Guid.NewGuid():N}";
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
        var pipeName = $"avascope-cli-test-{Guid.NewGuid():N}";
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
        var pipeName = $"avascope-cli-test-{Guid.NewGuid():N}";
        var manifestPath = WriteBridgeManifest(sessionId, pipeName);
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
                treeKind);
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
        }
    }

    [Fact]
    public async Task InspectNodeCommandDefaultsTreeKindToVisual()
    {
        var cliAssembly = Path.Combine(AppContext.BaseDirectory, "avascope.dll");
        Assert.True(File.Exists(cliAssembly), $"Expected CLI assembly at {cliAssembly}.");

        var sessionId = SessionId.New();
        var pipeName = $"avascope-cli-test-{Guid.NewGuid():N}";
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
        var pipeName = $"avascope-cli-test-{Guid.NewGuid():N}";
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
        var pipeName = $"avascope-cli-test-{Guid.NewGuid():N}";
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
    public async Task InputCommandSendsKeyTextThroughBridgePipe()
    {
        var cliAssembly = Path.Combine(AppContext.BaseDirectory, "avascope.dll");
        Assert.True(File.Exists(cliAssembly), $"Expected CLI assembly at {cliAssembly}.");

        var sessionId = SessionId.New();
        var pipeName = $"avascope-cli-test-{Guid.NewGuid():N}";
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
        var pipeName = $"avascope-cli-test-{Guid.NewGuid():N}";
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
        var pipeName = $"avascope-cli-test-{Guid.NewGuid():N}";
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
        var pipeName = $"avascope-cli-test-{Guid.NewGuid():N}";
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
    [InlineData("--action", "drag")]
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

    [Fact]
    public async Task CloseSessionCommandClosesThroughBridgePipe()
    {
        var cliAssembly = Path.Combine(AppContext.BaseDirectory, "avascope.dll");
        Assert.True(File.Exists(cliAssembly), $"Expected CLI assembly at {cliAssembly}.");

        var sessionId = SessionId.New();
        var pipeName = $"avascope-cli-test-{Guid.NewGuid():N}";
        var manifestPath = WriteBridgeManifest(sessionId, pipeName);
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
            var result = await RunCliAsync(cliAssembly, "close-session", "--session", sessionId.Value);
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
        var pipeName = $"avascope-cli-test-{Guid.NewGuid():N}";
        var manifestPath = WriteBridgeManifest(sessionId, pipeName);

        var serverTask = RespondToBridgeRequestAsync(pipeName, request =>
        {
            Assert.Equal(BridgeIpcMethods.Health, request.Method);
            return BridgeIpcResponse.Ok(request.RequestId, HealthResponse.Current());
        });

        try
        {
            var result = await RunCliAsync(
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

        var result = await RunCliAsync(cliAssembly, "diagnostics", "--session", SessionId.New().Value);

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
        var pipeName = $"avascope-cli-test-{Guid.NewGuid():N}";
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
        var pipeName = $"avascope-cli-test-{Guid.NewGuid():N}";
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

    private static string WriteBridgeManifest(SessionId sessionId, string pipeName)
    {
        Directory.CreateDirectory(BridgeSessionManifest.GetDefaultDirectory());

        var manifest = new BridgeSessionManifest(
            sessionId,
            Environment.ProcessId,
            pipeName,
            DateTimeOffset.UtcNow,
            "CLI fake bridge");
        var manifestPath = BridgeSessionManifest.GetDefaultPath(sessionId);
        File.WriteAllText(manifestPath, JsonSerializer.Serialize(manifest), Encoding.UTF8);
        return manifestPath;
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

                var request = JsonSerializer.Deserialize<BridgeIpcRequest>(requestLine, JsonOptions)
                    ?? throw new InvalidOperationException("Bridge IPC request payload was empty.");
                var responseBytes = Encoding.UTF8.GetBytes(
                    JsonSerializer.Serialize(responseFactory(request), JsonOptions) + Environment.NewLine);
                await pipe.WriteAsync(responseBytes, cancellation.Token);
                await pipe.FlushAsync(cancellation.Token);
                return request;
            }
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            throw new TimeoutException($"Timed out waiting for a bridge IPC request on pipe '{pipeName}'.");
        }
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

    private sealed record CliResult(
        int ExitCode,
        string StandardOutput,
        string StandardError);
}
