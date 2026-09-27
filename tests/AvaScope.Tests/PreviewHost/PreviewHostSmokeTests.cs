using System.Diagnostics;
using System.Text.Json;
using AvaScope.Protocol;
using SkiaSharp;

namespace AvaScope.Tests.PreviewHost;

public sealed class PreviewHostSmokeTests(Xunit.Abstractions.ITestOutputHelper output)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task PreviewHostTimeoutRetainsEvidenceAndStopsOwnedProcess()
    {
        Process? owned = null;
        try
        {
            var failure = await Record.ExceptionAsync(() => RunPreviewHostAsync(
                "controlled-capture", "controlled-request.json", expectedExitCode: 0, startProcess: () =>
                {
                    var process = StartPreviewCaptureProbe(new string('x', 12000) + "\npreview-fixture-ready", "preview-fixture-stderr", block: true);
                    owned = Process.GetProcessById(process.Id);
                    _ = owned.SafeHandle;
                    return process;
                }));
            Assert.NotNull(failure);
            Assert.IsAssignableFrom<OperationCanceledException>(failure);
            Assert.NotNull(owned);
            output.WriteLine(JsonSerializer.Serialize(new { failureType = failure.GetType().Name, processId = owned.Id, ownedExited = owned.HasExited, evidenceEntries = failure.Data.Count }));
            Assert.True(owned.HasExited, "The timed-out preview test child is still running after the helper returned.");
            Assert.Equal("process_wait", failure.Data["processPhase"]);
            Assert.Equal(owned.Id, failure.Data["processId"]);
            Assert.Equal(true, failure.Data["cleanupProcessExited"]);
            Assert.Equal(60000, failure.Data["timeoutMs"]);
            var stdout = Assert.IsType<string>(failure.Data["stdoutTail"]);
            var stderr = Assert.IsType<string>(failure.Data["stderrTail"]);
            Assert.Contains("preview-fixture-ready", stdout);
            Assert.Contains("preview-fixture-stderr", stderr);
            Assert.True(stdout.Length <= 4096);
            Assert.True(stderr.Length <= 4096);
        }
        finally
        {
            // The unchanged-helper regression must also clean up its deliberately blocked child.
            if (owned is not null)
            {
                if (!owned.HasExited) owned.Kill(entireProcessTree: true);
                await owned.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5));
                owned.Dispose();
            }
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(23)]
    public async Task PreviewHostCapturePreservesCompletedResponseAndExitCode(int exitCode)
    {
        var payload = new string('x', 12000);
        var json = JsonSerializer.Serialize(ToolResult<PreviewResponse>.Fail(
            new ProtocolError("controlled_preview_response", "Completed capture fixture", new Dictionary<string, string> { ["payload"] = payload })), JsonOptions);
        var result = await RunPreviewHostAsync("controlled-capture", "controlled-request.json", exitCode,
            () => StartPreviewCaptureProbe(json, string.Empty, block: false, exitCode));
        Assert.NotNull(result);
        Assert.False(result.Success);
        Assert.Equal("controlled_preview_response", result.Error!.Code);
        Assert.NotNull(result.Error.Details);
        Assert.Equal(payload, result.Error.Details["payload"]);
    }

    private static Process StartPreviewCaptureProbe(string stdout, string stderr, bool block, int exitCode = 0)
    {
        var info = new ProcessStartInfo
        {
            FileName = OperatingSystem.IsWindows() ? "powershell" : "/bin/sh",
            WorkingDirectory = AppContext.BaseDirectory,
            UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardOutput = true, RedirectStandardError = true
        };
        if (OperatingSystem.IsWindows())
        {
            info.ArgumentList.Add("-NoProfile");
            info.ArgumentList.Add("-NonInteractive");
            info.ArgumentList.Add("-Command");
            info.ArgumentList.Add($"[Console]::Out.WriteLine('{stdout.Replace("'", "''")}'); [Console]::Error.Write('{stderr.Replace("'", "''")}'); "
                + (block ? "Start-Sleep -Seconds 120" : $"exit {exitCode}"));
        }
        else
        {
            info.ArgumentList.Add("-c");
            info.ArgumentList.Add($"printf '%s\\n' '{stdout.Replace("'", "'\"'\"'")}'; printf '%s' '{stderr.Replace("'", "'\"'\"'")}' >&2; "
                + (block ? "exec sleep 120" : $"exit {exitCode}"));
        }
        var process = new Process { StartInfo = info };
        Assert.True(process.Start());
        return process;
    }

    [Theory]
    [InlineData("12,40,7,9", true, 96)]
    [InlineData("0.4", true, 144)]
    [InlineData("-0.4", true, 96)]
    [InlineData("-2,-3,-4,-5", false, 96)]
    [InlineData("4.2,9.3,3.4,2.1", false, 120)]
    [InlineData("0", true, 96)]
    public async Task PreviewTextMarginsDoNotProduceClippingWarnings(string margin, bool useLayoutRounding, double dpi)
    {
        var testRoot = Path.Combine(Path.GetTempPath(), "AvaScope.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(testRoot);
        var viewPath = Path.Combine(testRoot, "MarginView.axaml");
        var requestPath = Path.Combine(testRoot, "request.json");
        await File.WriteAllTextAsync(viewPath, $$"""
            <Border xmlns="https://github.com/avaloniaui" Background="White" Padding="20">
              <TextBlock Text="Artifact QA" FontSize="14" Foreground="Black"
                         Margin="{{margin}}" UseLayoutRounding="{{useLayoutRounding}}"
                         HorizontalAlignment="Left" VerticalAlignment="Top" />
            </Border>
            """);
        await File.WriteAllTextAsync(requestPath, JsonSerializer.Serialize(
            new PreviewRequest(Path.Combine(testRoot, "preview.png"), width: 240, height: 160, dpi: dpi, viewPath: viewPath), JsonOptions));
        try
        {
            var result = await RunPreviewHostAsync(Path.Combine(AppContext.BaseDirectory, "AvaScope.PreviewHost.dll"), requestPath, expectedExitCode: 0);
            Assert.NotNull(result);
            Assert.True(result.Success, result.Error?.Message);
            Assert.Equal((int)(240 * dpi / 96), result.Value!.PixelWidth);
            Assert.Equal((int)(160 * dpi / 96), result.Value.PixelHeight);
            using var bitmap = SKBitmap.Decode(result.Value.FilePath);
            Assert.NotNull(bitmap);
            Assert.Contains(bitmap.Pixels, pixel => pixel.Red < 128 && pixel.Green < 128 && pixel.Blue < 128);
            Assert.DoesNotContain(result.Value.Diagnostics, diagnostic => diagnostic.Code is "text_clipped" or "text_truncated");
        }
        finally
        {
            await DeleteDirectoryWithRetryAsync(testRoot);
        }
    }

    [Theory]
    [InlineData("-100,0,-100,0")]
    [InlineData("0,-30,0,-30")]
    [InlineData("-100")]
    public async Task PreviewTextClampedDesiredSizeDoesNotImplyClipping(string margin)
    {
        var testRoot = Path.Combine(Path.GetTempPath(), "AvaScope.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(testRoot);
        var viewPath = Path.Combine(testRoot, "ClampedMarginView.axaml");
        var requestPath = Path.Combine(testRoot, "request.json");
        await File.WriteAllTextAsync(viewPath, $$"""
            <Border xmlns="https://github.com/avaloniaui" Background="White" Padding="150">
              <TextBlock Text="Artifact QA" FontSize="14" Foreground="Black" Width="120" Height="40"
                         Margin="{{margin}}" HorizontalAlignment="Left" VerticalAlignment="Top" />
            </Border>
            """);
        await File.WriteAllTextAsync(requestPath, JsonSerializer.Serialize(
            new PreviewRequest(Path.Combine(testRoot, "preview.png"), width: 500, height: 400, viewPath: viewPath), JsonOptions));
        try
        {
            var result = await RunPreviewHostAsync(Path.Combine(AppContext.BaseDirectory, "AvaScope.PreviewHost.dll"), requestPath, expectedExitCode: 0);
            Assert.NotNull(result);
            Assert.True(result.Success, result.Error?.Message);
            using var bitmap = SKBitmap.Decode(result.Value!.FilePath);
            Assert.NotNull(bitmap);
            Assert.Contains(bitmap.Pixels, pixel => pixel.Red < 128 && pixel.Green < 128 && pixel.Blue < 128);
            Assert.DoesNotContain(result.Value.Diagnostics, diagnostic => diagnostic.Code is "text_clipped" or "text_truncated");
        }
        finally
        {
            await DeleteDirectoryWithRetryAsync(testRoot);
        }
    }

    [Fact]
    public async Task PreviewHostRendersStandaloneAxamlViewInChildProcess()
    {
        var hostAssembly = Path.Combine(AppContext.BaseDirectory, "AvaScope.PreviewHost.dll");
        Assert.True(File.Exists(hostAssembly), $"Expected preview host assembly at {hostAssembly}.");

        var testRoot = Path.Combine(Path.GetTempPath(), "AvaScope.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(testRoot);

        var viewPath = Path.Combine(testRoot, "SmokeView.axaml");
        var requestPath = Path.Combine(testRoot, "request.json");
        var outputPath = Path.Combine(testRoot, "preview.png");

        await File.WriteAllTextAsync(viewPath, """
            <UserControl xmlns="https://github.com/avaloniaui">
              <Border Background="#FFFFFFFF" Padding="12">
                <TextBlock Text="AvaScope preview smoke" />
              </Border>
            </UserControl>
            """);

        var request = new PreviewRequest(
            outputPath,
            width: 320,
            height: 200,
            dpi: 96,
            viewPath: viewPath,
            themeVariant: "light");

        await File.WriteAllTextAsync(requestPath, JsonSerializer.Serialize(request, JsonOptions));

        try
        {
            var result = await RunPreviewHostAsync(hostAssembly, requestPath, expectedExitCode: 0);

            Assert.NotNull(result);
            Assert.True(result.Success, result.Error?.Message);
            Assert.Equal(Path.GetFullPath(outputPath), result.Value!.FilePath);
            Assert.Equal(320, result.Value.PixelWidth);
            Assert.Equal(200, result.Value.PixelHeight);
            Assert.Equal(96, result.Value.Dpi);
            Assert.Equal(Path.GetFullPath(viewPath), Path.GetFullPath(result.Value.ViewPath!));
            Assert.True(File.Exists(result.Value.FilePath));
            Assert.True(new FileInfo(result.Value.FilePath).Length > 0);
        }
        finally
        {
            await DeleteDirectoryWithRetryAsync(testRoot);
        }
    }

    [Fact]
    public async Task PreviewHostReturnsProjectInfoAndProjectGraphDiagnostics()
    {
        var hostAssembly = Path.Combine(AppContext.BaseDirectory, "AvaScope.PreviewHost.dll");
        Assert.True(File.Exists(hostAssembly), $"Expected preview host assembly at {hostAssembly}.");

        var testRoot = Path.Combine(Path.GetTempPath(), "AvaScope.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(testRoot);

        var projectPath = Path.Combine(testRoot, "ProjectInfoSample.csproj");
        var viewPath = Path.Combine(testRoot, "ProjectInfoView.axaml");
        var requestPath = Path.Combine(testRoot, "request.json");
        var outputPath = Path.Combine(testRoot, "preview.png");

        await File.WriteAllTextAsync(projectPath, """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFrameworks>net10.0</TargetFrameworks>
                <AssemblyName>CustomPreviewAssembly</AssemblyName>
              </PropertyGroup>
            </Project>
            """);

        await File.WriteAllTextAsync(viewPath, """
            <UserControl xmlns="https://github.com/avaloniaui">
              <Border Background="#FFFFFFFF">
                <TextBlock Text="Project info preview" />
              </Border>
            </UserControl>
            """);

        var request = new PreviewRequest(
            outputPath,
            width: 320,
            height: 200,
            dpi: 96,
            projectPath: projectPath,
            viewPath: "ProjectInfoView.axaml",
            themeVariant: "light");

        await File.WriteAllTextAsync(requestPath, JsonSerializer.Serialize(request, JsonOptions));

        try
        {
            var result = await RunPreviewHostAsync(hostAssembly, requestPath, expectedExitCode: 0);

            Assert.NotNull(result);
            Assert.True(result.Success, result.Error?.Message);
            Assert.NotNull(result.Value!.ProjectInfo);
            Assert.Equal("CustomPreviewAssembly", result.Value.ProjectInfo!.AssemblyName);
            Assert.Equal("net10.0", Assert.Single(result.Value.ProjectInfo.TargetFrameworks));
            Assert.Equal("net10.0", result.Value.ProjectInfo.SelectedTargetFramework);
            Assert.True(File.Exists(result.Value.ProjectInfo.OutputAssemblyPath), result.Value.ProjectInfo.OutputAssemblyPath);
            var diagnostic = Assert.Single(result.Value.Diagnostics, static item => item.Code == "project_graph_resolved");
            Assert.Equal("project", diagnostic.Category);
            Assert.Equal("project_graph", diagnostic.Phase);
            Assert.Equal("msbuild_project_file", diagnostic.Provenance);
            Assert.False(string.IsNullOrWhiteSpace(diagnostic.SuggestedAction));
        }
        finally
        {
            await DeleteDirectoryWithRetryAsync(testRoot);
        }
    }

    [Fact]
    public async Task PreviewHostReturnsBindingResourceAndLayoutDiagnostics()
    {
        var hostAssembly = Path.Combine(AppContext.BaseDirectory, "AvaScope.PreviewHost.dll");
        Assert.True(File.Exists(hostAssembly), $"Expected preview host assembly at {hostAssembly}.");

        var testRoot = Path.Combine(Path.GetTempPath(), "AvaScope.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(testRoot);

        var viewPath = Path.Combine(testRoot, "DiagnosticView.axaml");
        var requestPath = Path.Combine(testRoot, "request.json");
        var outputPath = Path.Combine(testRoot, "preview.png");

        await File.WriteAllTextAsync(viewPath, """
            <UserControl xmlns="https://github.com/avaloniaui">
              <Grid Width="120" Height="80" ClipToBounds="True">
                <TextBlock Width="16"
                           Height="12"
                           Text="{Binding MissingTitle}"
                           TextTrimming="CharacterEllipsis" />
                <Border Width="20"
                        Height="20"
                        Margin="160,0,0,0"
                        Background="{DynamicResource MissingBrush}" />
                <Button Width="10"
                        Height="10"
                        HorizontalAlignment="Left"
                        VerticalAlignment="Bottom"
                        Content="!" />
              </Grid>
            </UserControl>
            """);

        var request = new PreviewRequest(
            outputPath,
            width: 120,
            height: 80,
            dpi: 96,
            viewPath: viewPath,
            themeVariant: "light");

        await File.WriteAllTextAsync(requestPath, JsonSerializer.Serialize(request, JsonOptions));

        try
        {
            var result = await RunPreviewHostAsync(hostAssembly, requestPath, expectedExitCode: 0);

            Assert.NotNull(result);
            Assert.True(result.Success, result.Error?.Message);
            Assert.True(File.Exists(result.Value!.FilePath));
            Assert.Contains(result.Value.Diagnostics, static diagnostic => diagnostic.Code == "binding_missing_datacontext");
            Assert.Contains(result.Value.Diagnostics, static diagnostic => diagnostic.Code == "resource_not_found");
            Assert.Contains(result.Value.Diagnostics, static diagnostic => diagnostic.Code == "hit_target_too_small");
        }
        finally
        {
            await DeleteDirectoryWithRetryAsync(testRoot);
        }
    }

    [Fact]
    public async Task PreviewHostSuppressesFluentTemplateLayoutNoise()
    {
        var hostAssembly = Path.Combine(AppContext.BaseDirectory, "AvaScope.PreviewHost.dll");
        Assert.True(File.Exists(hostAssembly), $"Expected preview host assembly at {hostAssembly}.");

        var testRoot = Path.Combine(Path.GetTempPath(), "AvaScope.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(testRoot);

        var projectPath = Path.Combine(testRoot, "FluentTemplateDiagnosticsSample.csproj");
        var appPath = Path.Combine(testRoot, "App.axaml");
        var appCodeBehindPath = Path.Combine(testRoot, "App.axaml.cs");
        var viewPath = Path.Combine(testRoot, "SettingsView.axaml");
        var codeBehindPath = Path.Combine(testRoot, "SettingsView.axaml.cs");
        var requestPath = Path.Combine(testRoot, "request.json");
        var outputPath = Path.Combine(testRoot, "preview.png");

        await File.WriteAllTextAsync(projectPath, """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
                <Nullable>enable</Nullable>
              </PropertyGroup>
              <ItemGroup>
                <PackageReference Include="Avalonia" Version="12.1.0" />
                <PackageReference Include="Avalonia.Themes.Fluent" Version="12.1.0" />
              </ItemGroup>
            </Project>
            """);

        await File.WriteAllTextAsync(appPath, """
            <Application xmlns="https://github.com/avaloniaui"
                         xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                         x:Class="FluentTemplateDiagnosticsSample.App">
              <Application.Styles>
                <FluentTheme />
              </Application.Styles>
            </Application>
            """);

        await File.WriteAllTextAsync(appCodeBehindPath, """
            using Avalonia;
            using Avalonia.Markup.Xaml;

            namespace FluentTemplateDiagnosticsSample;

            public partial class App : Application
            {
                public override void Initialize()
                {
                    AvaloniaXamlLoader.Load(this);
                }
            }
            """);

        await File.WriteAllTextAsync(viewPath, """
            <UserControl xmlns="https://github.com/avaloniaui"
                         xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                         x:Class="FluentTemplateDiagnosticsSample.SettingsView">
              <Border Padding="24">
                <TabControl>
                  <TabItem Header="General">
                    <StackPanel Spacing="16">
                      <CheckBox Content="Auto-connect on workspace open" />
                      <Slider Minimum="1"
                              Maximum="20"
                              Value="4"
                              Width="520"
                              HorizontalAlignment="Left" />
                    </StackPanel>
                  </TabItem>
                  <TabItem Header="System">
                    <TextBlock Text="System" />
                  </TabItem>
                  <TabItem Header="Monitoring">
                    <TextBlock Text="Monitoring" />
                  </TabItem>
                </TabControl>
              </Border>
            </UserControl>
            """);

        await File.WriteAllTextAsync(codeBehindPath, """
            using Avalonia.Controls;

            namespace FluentTemplateDiagnosticsSample;

            public partial class SettingsView : UserControl
            {
                public SettingsView()
                {
                    InitializeComponent();
                }
            }
            """);

        var request = new PreviewRequest(
            outputPath,
            width: 900,
            height: 620,
            dpi: 96,
            projectPath: projectPath,
            viewPath: "SettingsView.axaml",
            themeVariant: "dark");

        await File.WriteAllTextAsync(requestPath, JsonSerializer.Serialize(request, JsonOptions));

        try
        {
            var result = await RunPreviewHostAsync(hostAssembly, requestPath, expectedExitCode: 0);

            Assert.NotNull(result);
            Assert.True(result.Success, result.Error?.Message);
            Assert.True(File.Exists(result.Value!.FilePath));
            Assert.DoesNotContain(result.Value.Diagnostics, static item => item.Code == "elements_overlap");
            Assert.DoesNotContain(result.Value.Diagnostics, static item => item.Code == "text_clipped");
            Assert.DoesNotContain(result.Value.Diagnostics, static item => item.Code == "text_truncated");
            Assert.DoesNotContain(result.Value.Diagnostics, static item => item.Code == "hit_target_too_small");
        }
        finally
        {
            await DeleteDirectoryWithRetryAsync(testRoot);
        }
    }

    [Fact]
    public async Task PreviewHostSuppressesIntentionalOverlayChildOverlapDiagnostics()
    {
        var hostAssembly = Path.Combine(AppContext.BaseDirectory, "AvaScope.PreviewHost.dll");
        Assert.True(File.Exists(hostAssembly), $"Expected preview host assembly at {hostAssembly}.");

        var testRoot = Path.Combine(Path.GetTempPath(), "AvaScope.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(testRoot);

        var projectPath = Path.Combine(testRoot, "OverlayDiagnosticsSample.csproj");
        var viewPath = Path.Combine(testRoot, "OverlayView.axaml");
        var codeBehindPath = Path.Combine(testRoot, "OverlayView.axaml.cs");
        var overlayPath = Path.Combine(testRoot, "PreviewOverlay.cs");
        var requestPath = Path.Combine(testRoot, "request.json");
        var outputPath = Path.Combine(testRoot, "preview.png");

        await File.WriteAllTextAsync(projectPath, """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
                <Nullable>enable</Nullable>
              </PropertyGroup>
              <ItemGroup>
                <PackageReference Include="Avalonia" Version="12.1.0" />
              </ItemGroup>
            </Project>
            """);

        await File.WriteAllTextAsync(viewPath, """
            <UserControl xmlns="https://github.com/avaloniaui"
                         xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                         xmlns:local="using:OverlayDiagnosticsSample"
                         x:Class="OverlayDiagnosticsSample.OverlayView">
              <Grid Width="220" Height="140">
                <Border Background="#FF102030" />
                <local:PreviewOverlay Background="#6620A060" />
              </Grid>
            </UserControl>
            """);

        await File.WriteAllTextAsync(codeBehindPath, """
            using Avalonia.Controls;

            namespace OverlayDiagnosticsSample;

            public partial class OverlayView : UserControl
            {
                public OverlayView()
                {
                    InitializeComponent();
                }
            }
            """);

        await File.WriteAllTextAsync(overlayPath, """
            using Avalonia.Controls;

            namespace OverlayDiagnosticsSample;

            public sealed class PreviewOverlay : Border
            {
            }
            """);

        var request = new PreviewRequest(
            outputPath,
            width: 220,
            height: 140,
            dpi: 96,
            projectPath: projectPath,
            viewPath: "OverlayView.axaml",
            themeVariant: "light");

        await File.WriteAllTextAsync(requestPath, JsonSerializer.Serialize(request, JsonOptions));

        try
        {
            var result = await RunPreviewHostAsync(hostAssembly, requestPath, expectedExitCode: 0);

            Assert.NotNull(result);
            Assert.True(result.Success, result.Error?.Message);
            Assert.True(File.Exists(result.Value!.FilePath));
            Assert.DoesNotContain(result.Value.Diagnostics, static item => item.Code == "elements_overlap");
        }
        finally
        {
            await DeleteDirectoryWithRetryAsync(testRoot);
        }
    }

    [Fact]
    public async Task PreviewHostReturnsDataTypeBindingPathDiagnostics()
    {
        var hostAssembly = Path.Combine(AppContext.BaseDirectory, "AvaScope.PreviewHost.dll");
        Assert.True(File.Exists(hostAssembly), $"Expected preview host assembly at {hostAssembly}.");

        var testRoot = Path.Combine(Path.GetTempPath(), "AvaScope.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(testRoot);

        var projectPath = Path.Combine(testRoot, "TypedBindingDiagnosticsSample.csproj");
        var viewPath = Path.Combine(testRoot, "TypedBindingDiagnosticsView.axaml");
        var codeBehindPath = Path.Combine(testRoot, "TypedBindingDiagnosticsView.axaml.cs");
        var designDataPath = Path.Combine(testRoot, "PreviewDesignData.cs");
        var requestPath = Path.Combine(testRoot, "request.json");
        var outputPath = Path.Combine(testRoot, "preview.png");

        await File.WriteAllTextAsync(projectPath, """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
                <Nullable>enable</Nullable>
              </PropertyGroup>
              <ItemGroup>
                <PackageReference Include="Avalonia" Version="12.1.0" />
              </ItemGroup>
            </Project>
            """);

        await File.WriteAllTextAsync(viewPath, """
            <UserControl xmlns="https://github.com/avaloniaui"
                         xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                         xmlns:local="using:TypedBindingDiagnosticsSample"
                         x:Class="TypedBindingDiagnosticsSample.TypedBindingDiagnosticsView"
                         x:DataType="local:PreviewDesignData"
                         x:CompileBindings="False">
              <StackPanel>
                <TextBlock Text="{Binding MissingTitle}" />
                <TextBlock Text="{Binding Title}" />
              </StackPanel>
            </UserControl>
            """);

        await File.WriteAllTextAsync(codeBehindPath, """
            using Avalonia.Controls;

            namespace TypedBindingDiagnosticsSample;

            public partial class TypedBindingDiagnosticsView : UserControl
            {
                public TypedBindingDiagnosticsView()
                {
                    InitializeComponent();
                }
            }
            """);

        await File.WriteAllTextAsync(designDataPath, """
            namespace TypedBindingDiagnosticsSample;

            public sealed class PreviewDesignData
            {
                public string Title { get; } = "Known title";
            }
            """);

        var request = new PreviewRequest(
            outputPath,
            width: 260,
            height: 180,
            dpi: 96,
            projectPath: projectPath,
            viewPath: "TypedBindingDiagnosticsView.axaml",
            themeVariant: "light");

        await File.WriteAllTextAsync(requestPath, JsonSerializer.Serialize(request, JsonOptions));

        try
        {
            var result = await RunPreviewHostAsync(hostAssembly, requestPath, expectedExitCode: 0);

            Assert.NotNull(result);
            Assert.True(result.Success, result.Error?.Message);
            Assert.True(File.Exists(result.Value!.FilePath));

            var diagnostic = Assert.Single(
                result.Value.Diagnostics,
                static item => item.Code == "binding_datatype_path_not_found");
            Assert.Equal("Text", diagnostic.PropertyName);
            Assert.NotNull(diagnostic.Details);
            Assert.Equal("local:PreviewDesignData", diagnostic.Details!["dataTypeName"]);
            Assert.Equal("TypedBindingDiagnosticsSample.PreviewDesignData", diagnostic.Details["dataType"]);
            Assert.Equal("MissingTitle", diagnostic.Details["bindingPath"]);
        }
        finally
        {
            await DeleteDirectoryWithRetryAsync(testRoot);
        }
    }

    [Fact]
    public async Task PreviewHostTreatsHashElementNameBindingsAsExplicitSources()
    {
        var hostAssembly = Path.Combine(AppContext.BaseDirectory, "AvaScope.PreviewHost.dll");
        Assert.True(File.Exists(hostAssembly), $"Expected preview host assembly at {hostAssembly}.");

        var testRoot = Path.Combine(Path.GetTempPath(), "AvaScope.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(testRoot);

        var projectPath = Path.Combine(testRoot, "ElementNameBindingDiagnosticsSample.csproj");
        var viewPath = Path.Combine(testRoot, "ElementNameBindingView.axaml");
        var codeBehindPath = Path.Combine(testRoot, "ElementNameBindingView.axaml.cs");
        var viewModelPath = Path.Combine(testRoot, "ElementNameBindingViewModel.cs");
        var requestPath = Path.Combine(testRoot, "request.json");
        var outputPath = Path.Combine(testRoot, "preview.png");

        await File.WriteAllTextAsync(projectPath, """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
                <Nullable>enable</Nullable>
              </PropertyGroup>
              <ItemGroup>
                <PackageReference Include="Avalonia" Version="12.1.0" />
              </ItemGroup>
            </Project>
            """);

        await File.WriteAllTextAsync(viewPath, """
            <UserControl xmlns="https://github.com/avaloniaui"
                         xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                         xmlns:local="using:ElementNameBindingDiagnosticsSample"
                         x:Class="ElementNameBindingDiagnosticsSample.ElementNameBindingView"
                         x:DataType="local:ElementNameBindingViewModel"
                         x:CompileBindings="False">
              <StackPanel>
                <Button x:Name="TargetButton" Content="Target" />
                <Border Width="160"
                        Height="90"
                        Background="#FF356859"
                        Tag="{Binding #TargetButton}" />
              </StackPanel>
            </UserControl>
            """);

        await File.WriteAllTextAsync(codeBehindPath, """
            using Avalonia.Controls;

            namespace ElementNameBindingDiagnosticsSample;

            public partial class ElementNameBindingView : UserControl
            {
                public ElementNameBindingView()
                {
                    InitializeComponent();
                }
            }
            """);

        await File.WriteAllTextAsync(viewModelPath, """
            namespace ElementNameBindingDiagnosticsSample;

            public sealed class ElementNameBindingViewModel
            {
            }
            """);

        var request = new PreviewRequest(
            outputPath,
            width: 240,
            height: 160,
            dpi: 96,
            projectPath: projectPath,
            viewPath: "ElementNameBindingView.axaml",
            themeVariant: "light",
            designDataType: "ElementNameBindingDiagnosticsSample.ElementNameBindingViewModel");

        await File.WriteAllTextAsync(requestPath, JsonSerializer.Serialize(request, JsonOptions));

        try
        {
            var result = await RunPreviewHostAsync(hostAssembly, requestPath, expectedExitCode: 0);

            Assert.NotNull(result);
            Assert.True(result.Success, result.Error?.Message);
            Assert.True(File.Exists(result.Value!.FilePath));
            Assert.DoesNotContain(result.Value.Diagnostics, static item => item.Code == "binding_path_not_found");
            Assert.DoesNotContain(result.Value.Diagnostics, static item => item.Code == "binding_datatype_path_not_found");
        }
        finally
        {
            await DeleteDirectoryWithRetryAsync(testRoot);
        }
    }

    [Fact]
    public async Task PreviewHostUsesDataTemplateDataTypeForBindingDiagnostics()
    {
        var hostAssembly = Path.Combine(AppContext.BaseDirectory, "AvaScope.PreviewHost.dll");
        Assert.True(File.Exists(hostAssembly), $"Expected preview host assembly at {hostAssembly}.");

        var testRoot = Path.Combine(Path.GetTempPath(), "AvaScope.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(testRoot);

        var projectPath = Path.Combine(testRoot, "DataTemplateBindingDiagnosticsSample.csproj");
        var viewPath = Path.Combine(testRoot, "SettingsView.axaml");
        var codeBehindPath = Path.Combine(testRoot, "SettingsView.axaml.cs");
        var viewModelPath = Path.Combine(testRoot, "SettingsViewModel.cs");
        var requestPath = Path.Combine(testRoot, "request.json");
        var outputPath = Path.Combine(testRoot, "preview.png");

        await File.WriteAllTextAsync(projectPath, """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
                <Nullable>enable</Nullable>
              </PropertyGroup>
              <ItemGroup>
                <PackageReference Include="Avalonia" Version="12.1.0" />
              </ItemGroup>
            </Project>
            """);

        await File.WriteAllTextAsync(viewPath, """
            <UserControl xmlns="https://github.com/avaloniaui"
                         xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                         xmlns:local="using:DataTemplateBindingDiagnosticsSample"
                         x:Class="DataTemplateBindingDiagnosticsSample.SettingsView"
                         x:DataType="local:SettingsViewModel"
                         x:CompileBindings="False">
              <ItemsControl ItemsSource="{Binding SystemProfileOptions}">
                <ItemsControl.ItemTemplate>
                  <DataTemplate x:DataType="local:SystemProfileOptionViewModel">
                    <StackPanel>
                      <TextBlock Text="{Binding DisplayName}" />
                      <CheckBox IsChecked="{Binding IsServerProfile}" />
                    </StackPanel>
                  </DataTemplate>
                </ItemsControl.ItemTemplate>
              </ItemsControl>
            </UserControl>
            """);

        await File.WriteAllTextAsync(codeBehindPath, """
            using Avalonia.Controls;

            namespace DataTemplateBindingDiagnosticsSample;

            public partial class SettingsView : UserControl
            {
                public SettingsView()
                {
                    InitializeComponent();
                }
            }
            """);

        await File.WriteAllTextAsync(viewModelPath, """
            using System.Collections.Generic;

            namespace DataTemplateBindingDiagnosticsSample;

            public sealed class SettingsViewModel
            {
                public IReadOnlyList<SystemProfileOptionViewModel> SystemProfileOptions { get; } =
                    new[] { new SystemProfileOptionViewModel() };
            }

            public sealed class SystemProfileOptionViewModel
            {
                public string DisplayName { get; } = "Server profile";

                public bool IsServerProfile { get; } = true;
            }
            """);

        var request = new PreviewRequest(
            outputPath,
            width: 420,
            height: 240,
            dpi: 96,
            projectPath: projectPath,
            viewPath: "SettingsView.axaml",
            themeVariant: "light",
            designDataType: "DataTemplateBindingDiagnosticsSample.SettingsViewModel");

        await File.WriteAllTextAsync(requestPath, JsonSerializer.Serialize(request, JsonOptions));

        try
        {
            var result = await RunPreviewHostAsync(hostAssembly, requestPath, expectedExitCode: 0);

            Assert.NotNull(result);
            Assert.True(result.Success, result.Error?.Message);
            Assert.True(File.Exists(result.Value!.FilePath));
            Assert.DoesNotContain(result.Value.Diagnostics, static item => item.Code == "binding_path_not_found");
            Assert.DoesNotContain(result.Value.Diagnostics, static item => item.Code == "binding_datatype_path_not_found");
            Assert.DoesNotContain(result.Value.Diagnostics, static item => item.Code == "binding_missing_datacontext");
        }
        finally
        {
            await DeleteDirectoryWithRetryAsync(testRoot);
        }
    }

    [Fact]
    public async Task PreviewHostResolvesRelativeViewPathAgainstProjectDirectory()
    {
        var hostAssembly = Path.Combine(AppContext.BaseDirectory, "AvaScope.PreviewHost.dll");
        Assert.True(File.Exists(hostAssembly), $"Expected preview host assembly at {hostAssembly}.");

        var testRoot = Path.Combine(Path.GetTempPath(), "AvaScope.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(testRoot);

        var projectPath = Path.Combine(testRoot, "Sample.csproj");
        var viewsDirectory = Path.Combine(testRoot, "Views");
        Directory.CreateDirectory(viewsDirectory);

        var viewPath = Path.Combine(viewsDirectory, "MainView.axaml");
        var requestPath = Path.Combine(testRoot, "request.json");
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
              <Grid Background="#FFFFFFFF">
                <TextBlock Text="Project relative preview" />
              </Grid>
            </UserControl>
            """);

        var request = new PreviewRequest(
            outputPath,
            width: 240,
            height: 160,
            dpi: 96,
            projectPath: projectPath,
            viewPath: Path.Combine("Views", "MainView.axaml"),
            themeVariant: "dark");

        await File.WriteAllTextAsync(requestPath, JsonSerializer.Serialize(request, JsonOptions));

        try
        {
            var result = await RunPreviewHostAsync(hostAssembly, requestPath, expectedExitCode: 0);

            Assert.NotNull(result);
            Assert.True(result.Success, result.Error?.Message);
            Assert.Equal(Path.GetFullPath(projectPath), result.Value!.ProjectPath);
            Assert.Equal(Path.GetFullPath(viewPath), result.Value.ViewPath);
            Assert.Equal(Path.GetFullPath(outputPath), result.Value.FilePath);
            Assert.Equal(240, result.Value.PixelWidth);
            Assert.Equal(160, result.Value.PixelHeight);
            Assert.Equal("dark", result.Value.ThemeVariant);
            Assert.True(File.Exists(result.Value.FilePath));
            Assert.True(new FileInfo(result.Value.FilePath).Length > 0);
        }
        finally
        {
            await DeleteDirectoryWithRetryAsync(testRoot);
        }
    }

    [Fact]
    public async Task PreviewHostReturnsReadinessErrorWhenProjectFileIsMissing()
    {
        var hostAssembly = Path.Combine(AppContext.BaseDirectory, "AvaScope.PreviewHost.dll");
        Assert.True(File.Exists(hostAssembly), $"Expected preview host assembly at {hostAssembly}.");

        var testRoot = Path.Combine(Path.GetTempPath(), "AvaScope.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(testRoot);

        var projectPath = Path.Combine(testRoot, "Missing.csproj");
        var viewPath = Path.Combine(testRoot, "MainView.axaml");
        var requestPath = Path.Combine(testRoot, "request.json");
        var outputPath = Path.Combine(testRoot, "preview.png");

        await File.WriteAllTextAsync(viewPath, """
            <UserControl xmlns="https://github.com/avaloniaui">
              <TextBlock Text="Missing project should not render" />
            </UserControl>
            """);

        var request = new PreviewRequest(
            outputPath,
            width: 240,
            height: 160,
            dpi: 96,
            projectPath: projectPath,
            viewPath: viewPath);

        await File.WriteAllTextAsync(requestPath, JsonSerializer.Serialize(request, JsonOptions));

        try
        {
            var result = await RunPreviewHostAsync(hostAssembly, requestPath, expectedExitCode: 1);

            Assert.NotNull(result);
            Assert.False(result.Success);
            Assert.Equal("preview_readiness_failed", result.Error!.Code);
            Assert.Equal("readiness", result.Error.Details!["phase"]);
            Assert.Equal("project_file", result.Error.Details["requirement"]);
            Assert.Equal(Path.GetFullPath(projectPath), result.Error.Details["projectPath"]);
            Assert.Equal(Path.GetFullPath(viewPath), result.Error.Details["viewPath"]);
            Assert.Contains("existing .csproj", result.Error.Details["nextAction"], StringComparison.Ordinal);
            Assert.False(File.Exists(outputPath));
        }
        finally
        {
            await DeleteDirectoryWithRetryAsync(testRoot);
        }
    }

    [Fact]
    public async Task PreviewHostReturnsReadinessErrorWhenViewFileIsMissing()
    {
        var hostAssembly = Path.Combine(AppContext.BaseDirectory, "AvaScope.PreviewHost.dll");
        Assert.True(File.Exists(hostAssembly), $"Expected preview host assembly at {hostAssembly}.");

        var testRoot = Path.Combine(Path.GetTempPath(), "AvaScope.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(testRoot);

        var projectPath = Path.Combine(testRoot, "Sample.csproj");
        var viewPath = Path.Combine(testRoot, "Views", "MissingView.axaml");
        var requestPath = Path.Combine(testRoot, "request.json");
        var outputPath = Path.Combine(testRoot, "preview.png");

        await File.WriteAllTextAsync(projectPath, """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
              </PropertyGroup>
            </Project>
            """);

        var request = new PreviewRequest(
            outputPath,
            width: 240,
            height: 160,
            dpi: 96,
            projectPath: projectPath,
            viewPath: Path.Combine("Views", "MissingView.axaml"));

        await File.WriteAllTextAsync(requestPath, JsonSerializer.Serialize(request, JsonOptions));

        try
        {
            var result = await RunPreviewHostAsync(hostAssembly, requestPath, expectedExitCode: 1);

            Assert.NotNull(result);
            Assert.False(result.Success);
            Assert.Equal("preview_readiness_failed", result.Error!.Code);
            Assert.Equal("readiness", result.Error.Details!["phase"]);
            Assert.Equal("view_file", result.Error.Details["requirement"]);
            Assert.Equal(Path.GetFullPath(projectPath), result.Error.Details["projectPath"]);
            Assert.Equal(Path.GetFullPath(viewPath), result.Error.Details["viewPath"]);
            Assert.Contains("existing .axaml", result.Error.Details["nextAction"], StringComparison.Ordinal);
            Assert.False(File.Exists(outputPath));
        }
        finally
        {
            await DeleteDirectoryWithRetryAsync(testRoot);
        }
    }

    [Fact]
    public async Task PreviewHostReturnsStructuredErrorWhenProjectBuildFails()
    {
        var hostAssembly = Path.Combine(AppContext.BaseDirectory, "AvaScope.PreviewHost.dll");
        Assert.True(File.Exists(hostAssembly), $"Expected preview host assembly at {hostAssembly}.");

        var testRoot = Path.Combine(Path.GetTempPath(), "AvaScope.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(testRoot);

        var projectPath = Path.Combine(testRoot, "Broken.csproj");
        var viewPath = Path.Combine(testRoot, "MainView.axaml");
        var requestPath = Path.Combine(testRoot, "request.json");
        var outputPath = Path.Combine(testRoot, "preview.png");

        await File.WriteAllTextAsync(projectPath, """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
              </PropertyGroup>
              <Target Name="FailPreviewBuild" BeforeTargets="Build">
                <Error Text="AVASCOPE_FULL_LOG_ROOT_CAUSE C:\absolute\fixture\LockedOutput.dll" />
              </Target>
            </Project>
            """);

        await File.WriteAllTextAsync(viewPath, """
            <UserControl xmlns="https://github.com/avaloniaui">
              <TextBlock Text="Should not render" />
            </UserControl>
            """);

        var request = new PreviewRequest(
            outputPath,
            width: 240,
            height: 160,
            dpi: 96,
            projectPath: projectPath,
            viewPath: viewPath);

        await File.WriteAllTextAsync(requestPath, JsonSerializer.Serialize(request, JsonOptions));

        try
        {
            var result = await RunPreviewHostAsync(hostAssembly, requestPath, expectedExitCode: 1);

            Assert.NotNull(result);
            Assert.False(result.Success);
            Assert.Equal("preview_project_build_failed", result.Error!.Code);
            Assert.NotNull(result.Error.Details);
            Assert.Equal("build", result.Error.Details!["phase"]);
            Assert.Equal(Path.GetFullPath(projectPath), result.Error.Details["projectPath"]);
            Assert.Equal("1", result.Error.Details["exitCode"]);
            Assert.Equal("isolated_default_build", result.Error.Details["buildMode"]);
            Assert.Contains("Build FAILED", result.Error.Details["outputTail"], StringComparison.Ordinal);
            Assert.Contains("AVASCOPE_FULL_LOG_ROOT_CAUSE", result.Error.Details["outputTail"], StringComparison.Ordinal);
            Assert.True(result.Error.Details.TryGetValue("buildLogPath", out var buildLogPath));
            Assert.True(File.Exists(buildLogPath), buildLogPath);
            var fullLog = await File.ReadAllTextAsync(buildLogPath);
            Assert.Contains("AVASCOPE_FULL_LOG_ROOT_CAUSE C:\\absolute\\fixture\\LockedOutput.dll", fullLog, StringComparison.Ordinal);
            Assert.Contains(
                fullLog.Split(Environment.NewLine).Where(line => line.StartsWith(Path.GetFullPath(projectPath), StringComparison.OrdinalIgnoreCase)),
                line => line.Contains("AVASCOPE_FULL_LOG_ROOT_CAUSE", StringComparison.Ordinal));
            Assert.Contains("Fix the project build output", result.Error.Details["nextAction"], StringComparison.Ordinal);
            Assert.False(File.Exists(outputPath));
        }
        finally
        {
            await DeleteDirectoryWithRetryAsync(testRoot);
        }
    }

    [Fact]
    public async Task PreviewHostLoadsCompiledAvaloniaProjectResourceView()
    {
        var hostAssembly = Path.Combine(AppContext.BaseDirectory, "AvaScope.PreviewHost.dll");
        Assert.True(File.Exists(hostAssembly), $"Expected preview host assembly at {hostAssembly}.");

        var testRoot = Path.Combine(Path.GetTempPath(), "AvaScope.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(testRoot);

        var projectPath = Path.Combine(testRoot, "CompiledPreviewSample.csproj");
        var viewsDirectory = Path.Combine(testRoot, "Views");
        Directory.CreateDirectory(viewsDirectory);

        var viewPath = Path.Combine(viewsDirectory, "MainView.axaml");
        var codeBehindPath = Path.Combine(viewsDirectory, "MainView.axaml.cs");
        var requestPath = Path.Combine(testRoot, "request.json");
        var outputPath = Path.Combine(testRoot, "preview.png");
        var normalOutputAssemblyPath = Path.Combine(testRoot, "bin", "Debug", "net10.0", "CompiledPreviewSample.dll");

        await File.WriteAllTextAsync(projectPath, """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
                <Nullable>enable</Nullable>
              </PropertyGroup>
              <ItemGroup>
                <PackageReference Include="Avalonia" Version="12.1.0" />
              </ItemGroup>
            </Project>
            """);

        await File.WriteAllTextAsync(viewPath, """
            <UserControl xmlns="https://github.com/avaloniaui"
                         xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                         x:Class="CompiledPreviewSample.Views.MainView">
              <Border Background="#FFFFFFFF" Padding="8">
                <TextBlock Text="Compiled project resource preview" />
              </Border>
            </UserControl>
            """);

        await File.WriteAllTextAsync(codeBehindPath, """
            using Avalonia.Controls;

            namespace CompiledPreviewSample.Views;

            public partial class MainView : UserControl
            {
                public MainView()
                {
                    InitializeComponent();
                    ConstructedByCodeBehind = true;
                }

                public bool ConstructedByCodeBehind { get; }
            }
            """);

        Directory.CreateDirectory(Path.GetDirectoryName(normalOutputAssemblyPath)!);
        await File.WriteAllTextAsync(normalOutputAssemblyPath, "locked normal output");

        var request = new PreviewRequest(
            outputPath,
            width: 260,
            height: 180,
            dpi: 96,
            projectPath: projectPath,
            viewPath: Path.Combine("Views", "MainView.axaml"),
            themeVariant: "light");

        await File.WriteAllTextAsync(requestPath, JsonSerializer.Serialize(request, JsonOptions));

        try
        {
            using var lockedNormalOutput = new FileStream(
                normalOutputAssemblyPath,
                FileMode.Open,
                FileAccess.ReadWrite,
                FileShare.None);
            var result = await RunPreviewHostAsync(hostAssembly, requestPath, expectedExitCode: 0);

            Assert.NotNull(result);
            Assert.True(result.Success, result.Error?.Message);
            Assert.Equal(Path.GetFullPath(projectPath), result.Value!.ProjectPath);
            Assert.Equal(Path.GetFullPath(viewPath), result.Value.ViewPath);
            Assert.Equal(260, result.Value.PixelWidth);
            Assert.Equal(180, result.Value.PixelHeight);
            Assert.NotNull(result.Value.ProjectInfo);
            Assert.Equal("isolated_default_build", result.Value.ProjectInfo!.BuildMode);
            Assert.NotNull(result.Value.ProjectInfo.BuildOutputRoot);
            Assert.NotNull(result.Value.ProjectInfo.OutputAssemblyPath);
            Assert.StartsWith(
                Path.Combine(testRoot, ".avascope", "build"),
                result.Value.ProjectInfo.BuildOutputRoot,
                StringComparison.OrdinalIgnoreCase);
            Assert.StartsWith(
                result.Value.ProjectInfo.BuildOutputRoot,
                result.Value.ProjectInfo.OutputAssemblyPath,
                StringComparison.OrdinalIgnoreCase);
            Assert.True(File.Exists(result.Value.FilePath));
            Assert.True(new FileInfo(result.Value.FilePath).Length > 0);
        }
        finally
        {
            await DeleteDirectoryWithRetryAsync(testRoot);
        }
    }

    [Fact]
    public async Task PreviewHostSerializesParallelBuildsForSameProject()
    {
        var hostAssembly = Path.Combine(AppContext.BaseDirectory, "AvaScope.PreviewHost.dll");
        Assert.True(File.Exists(hostAssembly), $"Expected preview host assembly at {hostAssembly}.");

        var testRoot = Path.Combine(Path.GetTempPath(), "AvaScope.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(testRoot);

        var projectPath = Path.Combine(testRoot, "ParallelPreviewSample.csproj");
        var viewsDirectory = Path.Combine(testRoot, "Views");
        Directory.CreateDirectory(viewsDirectory);

        var viewPath = Path.Combine(viewsDirectory, "MainView.axaml");
        var codeBehindPath = Path.Combine(viewsDirectory, "MainView.axaml.cs");
        var firstRequestPath = Path.Combine(testRoot, "first-request.json");
        var secondRequestPath = Path.Combine(testRoot, "second-request.json");
        var firstOutputPath = Path.Combine(testRoot, "first-preview.png");
        var secondOutputPath = Path.Combine(testRoot, "second-preview.png");

        await File.WriteAllTextAsync(projectPath, """
            <Project Sdk="Microsoft.NET.Sdk">
              <UsingTask TaskName="HoldIntermediateOutputLock"
                         TaskFactory="RoslynCodeTaskFactory"
                         AssemblyFile="$(MSBuildToolsPath)\Microsoft.Build.Tasks.Core.dll">
                <ParameterGroup>
                  <FilePath ParameterType="System.String" Required="true" />
                </ParameterGroup>
                <Task>
                  <Code Type="Fragment" Language="cs"><![CDATA[
                    var directory = System.IO.Path.GetDirectoryName(FilePath);
                    if (!string.IsNullOrWhiteSpace(directory))
                    {
                        System.IO.Directory.CreateDirectory(directory);
                    }

                    using (var stream = new System.IO.FileStream(
                        FilePath,
                        System.IO.FileMode.OpenOrCreate,
                        System.IO.FileAccess.ReadWrite,
                        System.IO.FileShare.None))
                    {
                        System.Threading.Thread.Sleep(1500);
                    }
                  ]]></Code>
                </Task>
              </UsingTask>

              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
                <Nullable>enable</Nullable>
              </PropertyGroup>

              <ItemGroup>
                <PackageReference Include="Avalonia" Version="12.1.0" />
              </ItemGroup>

              <Target Name="AvaScopeHoldIntermediateOutputLock" BeforeTargets="CoreCompile">
                <HoldIntermediateOutputLock FilePath="$(BaseIntermediateOutputPath)avascope-build.lock" />
              </Target>
            </Project>
            """);

        await File.WriteAllTextAsync(viewPath, """
            <UserControl xmlns="https://github.com/avaloniaui"
                         xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                         x:Class="ParallelPreviewSample.Views.MainView">
              <Border Background="#FFFFFFFF" Padding="8">
                <TextBlock Text="Parallel preview sample" />
              </Border>
            </UserControl>
            """);

        await File.WriteAllTextAsync(codeBehindPath, """
            using Avalonia.Controls;

            namespace ParallelPreviewSample.Views;

            public partial class MainView : UserControl
            {
                public MainView()
                {
                    InitializeComponent();
                }
            }
            """);

        var firstRequest = new PreviewRequest(
            firstOutputPath,
            width: 260,
            height: 180,
            dpi: 96,
            projectPath: projectPath,
            viewPath: Path.Combine("Views", "MainView.axaml"),
            themeVariant: "light");
        var secondRequest = new PreviewRequest(
            secondOutputPath,
            width: 280,
            height: 180,
            dpi: 96,
            projectPath: projectPath,
            viewPath: Path.Combine("Views", "MainView.axaml"),
            themeVariant: "light");

        await File.WriteAllTextAsync(firstRequestPath, JsonSerializer.Serialize(firstRequest, JsonOptions));
        await File.WriteAllTextAsync(secondRequestPath, JsonSerializer.Serialize(secondRequest, JsonOptions));

        try
        {
            var firstTask = RunPreviewHostAsync(hostAssembly, firstRequestPath, expectedExitCode: 0);
            var secondTask = RunPreviewHostAsync(hostAssembly, secondRequestPath, expectedExitCode: 0);
            var results = await Task.WhenAll(firstTask, secondTask);

            Assert.All(results, result =>
            {
                Assert.NotNull(result);
                Assert.True(result!.Success, result.Error?.Message);
                Assert.NotNull(result.Value!.ProjectInfo);
                Assert.Equal("isolated_default_build", result.Value.ProjectInfo!.BuildMode);
                Assert.NotNull(result.Value.ProjectInfo.BuildOutputRoot);
                Assert.True(File.Exists(result.Value.FilePath), result.Value.FilePath);
            });
            Assert.NotEqual(
                results[0]!.Value!.ProjectInfo!.BuildOutputRoot,
                results[1]!.Value!.ProjectInfo!.BuildOutputRoot);
        }
        finally
        {
            await DeleteDirectoryWithRetryAsync(testRoot);
        }
    }

    [Fact]
    public async Task PreviewHostCanRenderFromExplicitAssemblyPathWithoutBuild()
    {
        var hostAssembly = Path.Combine(AppContext.BaseDirectory, "AvaScope.PreviewHost.dll");
        Assert.True(File.Exists(hostAssembly), $"Expected preview host assembly at {hostAssembly}.");

        var testRoot = Path.Combine(Path.GetTempPath(), "AvaScope.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(testRoot);

        var projectPath = Path.Combine(testRoot, "AssemblyPathPreviewSample.csproj");
        var viewsDirectory = Path.Combine(testRoot, "Views");
        Directory.CreateDirectory(viewsDirectory);

        var viewPath = Path.Combine(viewsDirectory, "MainView.axaml");
        var codeBehindPath = Path.Combine(viewsDirectory, "MainView.axaml.cs");
        var firstRequestPath = Path.Combine(testRoot, "request-build.json");
        var secondRequestPath = Path.Combine(testRoot, "request-assembly.json");
        var firstOutputPath = Path.Combine(testRoot, "first-preview.png");
        var secondOutputPath = Path.Combine(testRoot, "second-preview.png");

        await File.WriteAllTextAsync(projectPath, """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
                <Nullable>enable</Nullable>
              </PropertyGroup>
              <ItemGroup>
                <PackageReference Include="Avalonia" Version="12.1.0" />
              </ItemGroup>
            </Project>
            """);

        await File.WriteAllTextAsync(viewPath, """
            <UserControl xmlns="https://github.com/avaloniaui"
                         xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                         x:Class="AssemblyPathPreviewSample.Views.MainView">
              <Border Background="#FFFFFFFF">
                <TextBlock Text="Assembly path preview" />
              </Border>
            </UserControl>
            """);

        await File.WriteAllTextAsync(codeBehindPath, """
            using Avalonia.Controls;

            namespace AssemblyPathPreviewSample.Views;

            public partial class MainView : UserControl
            {
                public MainView()
                {
                    InitializeComponent();
                }
            }
            """);

        var firstRequest = new PreviewRequest(
            firstOutputPath,
            width: 240,
            height: 160,
            dpi: 96,
            projectPath: projectPath,
            viewPath: Path.Combine("Views", "MainView.axaml"));
        await File.WriteAllTextAsync(firstRequestPath, JsonSerializer.Serialize(firstRequest, JsonOptions));

        try
        {
            var first = await RunPreviewHostAsync(hostAssembly, firstRequestPath, expectedExitCode: 0);

            Assert.NotNull(first);
            Assert.True(first.Success, first.Error?.Message);
            var assemblyPath = first.Value!.ProjectInfo?.OutputAssemblyPath;
            Assert.False(string.IsNullOrWhiteSpace(assemblyPath));
            Assert.True(File.Exists(assemblyPath), assemblyPath);

            var secondRequest = new PreviewRequest(
                secondOutputPath,
                width: 240,
                height: 160,
                dpi: 96,
                projectPath: projectPath,
                viewPath: Path.Combine("Views", "MainView.axaml"),
                assemblyPath: assemblyPath,
                noBuild: true);
            await File.WriteAllTextAsync(secondRequestPath, JsonSerializer.Serialize(secondRequest, JsonOptions));

            var second = await RunPreviewHostAsync(hostAssembly, secondRequestPath, expectedExitCode: 0);

            Assert.NotNull(second);
            Assert.True(second.Success, second.Error?.Message);
            Assert.Equal("assembly_path", second.Value!.ProjectInfo!.BuildMode);
            Assert.Equal(Path.GetFullPath(assemblyPath!), second.Value.ProjectInfo.OutputAssemblyPath);
            Assert.True(File.Exists(second.Value.FilePath));
        }
        finally
        {
            await DeleteDirectoryWithRetryAsync(testRoot);
        }
    }

    [Fact]
    public async Task PreviewHostRendersCompiledWindowRootViewDirectly()
    {
        var hostAssembly = Path.Combine(AppContext.BaseDirectory, "AvaScope.PreviewHost.dll");
        Assert.True(File.Exists(hostAssembly), $"Expected preview host assembly at {hostAssembly}.");

        var testRoot = Path.Combine(Path.GetTempPath(), "AvaScope.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(testRoot);

        var projectPath = Path.Combine(testRoot, "WindowRootPreviewSample.csproj");
        var viewsDirectory = Path.Combine(testRoot, "Views");
        Directory.CreateDirectory(viewsDirectory);

        var viewPath = Path.Combine(viewsDirectory, "MainWindow.axaml");
        var codeBehindPath = Path.Combine(viewsDirectory, "MainWindow.axaml.cs");
        var requestPath = Path.Combine(testRoot, "request.json");
        var outputPath = Path.Combine(testRoot, "window-preview.png");

        await File.WriteAllTextAsync(projectPath, """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
                <Nullable>enable</Nullable>
              </PropertyGroup>
              <ItemGroup>
                <PackageReference Include="Avalonia" Version="12.1.0" />
              </ItemGroup>
            </Project>
            """);

        await File.WriteAllTextAsync(viewPath, """
            <Window xmlns="https://github.com/avaloniaui"
                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                    x:Class="WindowRootPreviewSample.Views.MainWindow"
                    Background="#FF1B5E20">
              <Grid />
            </Window>
            """);

        await File.WriteAllTextAsync(codeBehindPath, """
            using Avalonia.Controls;

            namespace WindowRootPreviewSample.Views;

            public partial class MainWindow : Window
            {
                public MainWindow()
                {
                    InitializeComponent();
                }
            }
            """);

        var request = new PreviewRequest(
            outputPath,
            width: 240,
            height: 160,
            dpi: 96,
            projectPath: projectPath,
            viewPath: Path.Combine("Views", "MainWindow.axaml"),
            themeVariant: "light");

        await File.WriteAllTextAsync(requestPath, JsonSerializer.Serialize(request, JsonOptions));

        try
        {
            var result = await RunPreviewHostAsync(hostAssembly, requestPath, expectedExitCode: 0);

            Assert.NotNull(result);
            Assert.True(result.Success, result.Error?.Message);
            Assert.Equal(240, result.Value!.PixelWidth);
            Assert.Equal(160, result.Value.PixelHeight);
            AssertCenterPixel(outputPath, red: 0x1B, green: 0x5E, blue: 0x20);
        }
        finally
        {
            await DeleteDirectoryWithRetryAsync(testRoot);
        }
    }

    [Fact]
    public async Task PreviewHostLoadsCompiledAppResourcesBeforeProjectView()
    {
        var hostAssembly = Path.Combine(AppContext.BaseDirectory, "AvaScope.PreviewHost.dll");
        Assert.True(File.Exists(hostAssembly), $"Expected preview host assembly at {hostAssembly}.");

        var testRoot = Path.Combine(Path.GetTempPath(), "AvaScope.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(testRoot);

        var projectPath = Path.Combine(testRoot, "AppResourcePreviewSample.csproj");
        var appPath = Path.Combine(testRoot, "App.axaml");
        var appCodeBehindPath = Path.Combine(testRoot, "App.axaml.cs");
        var viewsDirectory = Path.Combine(testRoot, "Views");
        Directory.CreateDirectory(viewsDirectory);

        var viewPath = Path.Combine(viewsDirectory, "ResourceView.axaml");
        var codeBehindPath = Path.Combine(viewsDirectory, "ResourceView.axaml.cs");
        var requestPath = Path.Combine(testRoot, "request.json");
        var outputPath = Path.Combine(testRoot, "preview.png");

        await File.WriteAllTextAsync(projectPath, """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
                <Nullable>enable</Nullable>
              </PropertyGroup>
              <ItemGroup>
                <PackageReference Include="Avalonia" Version="12.1.0" />
              </ItemGroup>
            </Project>
            """);

        await File.WriteAllTextAsync(appPath, """
            <Application xmlns="https://github.com/avaloniaui"
                         xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                         x:Class="AppResourcePreviewSample.App">
              <Application.Resources>
                <SolidColorBrush x:Key="PreviewAccentBrush" Color="#FF225588" />
              </Application.Resources>
            </Application>
            """);

        await File.WriteAllTextAsync(appCodeBehindPath, """
            using Avalonia;
            using Avalonia.Markup.Xaml;

            namespace AppResourcePreviewSample;

            public partial class App : Application
            {
                public override void Initialize()
                {
                    AvaloniaXamlLoader.Load(this);
                }
            }
            """);

        await File.WriteAllTextAsync(viewPath, """
            <UserControl xmlns="https://github.com/avaloniaui"
                         xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                         x:Class="AppResourcePreviewSample.Views.ResourceView">
              <Border Background="{StaticResource PreviewAccentBrush}" Padding="8">
                <TextBlock Text="App resource preview" />
              </Border>
            </UserControl>
            """);

        await File.WriteAllTextAsync(codeBehindPath, """
            using Avalonia.Controls;

            namespace AppResourcePreviewSample.Views;

            public partial class ResourceView : UserControl
            {
                public ResourceView()
                {
                    InitializeComponent();
                }
            }
            """);

        var request = new PreviewRequest(
            outputPath,
            width: 280,
            height: 180,
            dpi: 96,
            projectPath: projectPath,
            viewPath: Path.Combine("Views", "ResourceView.axaml"),
            themeVariant: "light");

        await File.WriteAllTextAsync(requestPath, JsonSerializer.Serialize(request, JsonOptions));

        try
        {
            var result = await RunPreviewHostAsync(hostAssembly, requestPath, expectedExitCode: 0);

            Assert.NotNull(result);
            Assert.True(result.Success, result.Error?.Message);
            Assert.Equal(Path.GetFullPath(projectPath), result.Value!.ProjectPath);
            Assert.Equal(Path.GetFullPath(viewPath), result.Value.ViewPath);
            Assert.Equal(Path.GetFullPath(outputPath), result.Value.FilePath);
            Assert.Equal(280, result.Value.PixelWidth);
            Assert.Equal(180, result.Value.PixelHeight);
            Assert.True(File.Exists(result.Value.FilePath));
            Assert.True(new FileInfo(result.Value.FilePath).Length > 0);
        }
        finally
        {
            await DeleteDirectoryWithRetryAsync(testRoot);
        }
    }

    [Fact]
    public async Task PreviewHostAppliesCompiledAppStylesBeforeProjectView()
    {
        var hostAssembly = Path.Combine(AppContext.BaseDirectory, "AvaScope.PreviewHost.dll");
        Assert.True(File.Exists(hostAssembly), $"Expected preview host assembly at {hostAssembly}.");

        var testRoot = Path.Combine(Path.GetTempPath(), "AvaScope.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(testRoot);

        var projectPath = Path.Combine(testRoot, "AppStylePreviewSample.csproj");
        var appPath = Path.Combine(testRoot, "App.axaml");
        var appCodeBehindPath = Path.Combine(testRoot, "App.axaml.cs");
        var viewsDirectory = Path.Combine(testRoot, "Views");
        Directory.CreateDirectory(viewsDirectory);

        var viewPath = Path.Combine(viewsDirectory, "StyleView.axaml");
        var codeBehindPath = Path.Combine(viewsDirectory, "StyleView.axaml.cs");
        var requestPath = Path.Combine(testRoot, "request.json");
        var outputPath = Path.Combine(testRoot, "preview.png");

        await File.WriteAllTextAsync(projectPath, """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
                <Nullable>enable</Nullable>
              </PropertyGroup>
              <ItemGroup>
                <PackageReference Include="Avalonia" Version="12.1.0" />
              </ItemGroup>
            </Project>
            """);

        await File.WriteAllTextAsync(appPath, """
            <Application xmlns="https://github.com/avaloniaui"
                         xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                         x:Class="AppStylePreviewSample.App">
              <Application.Styles>
                <Style Selector="Border">
                  <Setter Property="Background" Value="#FF2B6CB0" />
                </Style>
              </Application.Styles>
            </Application>
            """);

        await File.WriteAllTextAsync(appCodeBehindPath, """
            using Avalonia;
            using Avalonia.Markup.Xaml;

            namespace AppStylePreviewSample;

            public partial class App : Application
            {
                public override void Initialize()
                {
                    AvaloniaXamlLoader.Load(this);
                }
            }
            """);

        await File.WriteAllTextAsync(viewPath, """
            <UserControl xmlns="https://github.com/avaloniaui"
                         xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                         x:Class="AppStylePreviewSample.Views.StyleView">
              <Border Width="220" Height="140" />
            </UserControl>
            """);

        await File.WriteAllTextAsync(codeBehindPath, """
            using Avalonia.Controls;

            namespace AppStylePreviewSample.Views;

            public partial class StyleView : UserControl
            {
                public StyleView()
                {
                    InitializeComponent();
                }
            }
            """);

        var request = new PreviewRequest(
            outputPath,
            width: 220,
            height: 140,
            dpi: 96,
            projectPath: projectPath,
            viewPath: Path.Combine("Views", "StyleView.axaml"),
            themeVariant: "light");

        await File.WriteAllTextAsync(requestPath, JsonSerializer.Serialize(request, JsonOptions));

        try
        {
            var result = await RunPreviewHostAsync(hostAssembly, requestPath, expectedExitCode: 0);

            Assert.NotNull(result);
            Assert.True(result.Success, result.Error?.Message);
            Assert.Equal(Path.GetFullPath(outputPath), result.Value!.FilePath);
            AssertCenterPixel(outputPath, red: 0x2B, green: 0x6C, blue: 0xB0);
        }
        finally
        {
            await DeleteDirectoryWithRetryAsync(testRoot);
        }
    }

    [Fact]
    public async Task PreviewHostAppliesCompiledAppImplicitControlThemesBeforeProjectView()
    {
        var hostAssembly = Path.Combine(AppContext.BaseDirectory, "AvaScope.PreviewHost.dll");
        Assert.True(File.Exists(hostAssembly), $"Expected preview host assembly at {hostAssembly}.");

        var testRoot = Path.Combine(Path.GetTempPath(), "AvaScope.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(testRoot);

        var projectPath = Path.Combine(testRoot, "ImplicitControlThemePreviewSample.csproj");
        var appPath = Path.Combine(testRoot, "App.axaml");
        var appCodeBehindPath = Path.Combine(testRoot, "App.axaml.cs");
        var viewsDirectory = Path.Combine(testRoot, "Views");
        Directory.CreateDirectory(viewsDirectory);

        var viewPath = Path.Combine(viewsDirectory, "ImplicitThemeView.axaml");
        var codeBehindPath = Path.Combine(viewsDirectory, "ImplicitThemeView.axaml.cs");
        var requestPath = Path.Combine(testRoot, "request.json");
        var outputPath = Path.Combine(testRoot, "preview.png");

        await File.WriteAllTextAsync(projectPath, """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
                <Nullable>enable</Nullable>
              </PropertyGroup>
              <ItemGroup>
                <PackageReference Include="Avalonia" Version="12.1.0" />
                <PackageReference Include="Avalonia.Themes.Fluent" Version="12.1.0" />
              </ItemGroup>
            </Project>
            """);

        await File.WriteAllTextAsync(appPath, """
            <Application xmlns="https://github.com/avaloniaui"
                         xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                         x:Class="ImplicitControlThemePreviewSample.App">
              <Application.Resources>
                <ControlTheme x:Key="{x:Type Button}" TargetType="Button">
                  <Setter Property="Background" Value="#FF2B6CB0" />
                  <Setter Property="Template">
                    <ControlTemplate>
                      <Border Background="{TemplateBinding Background}" />
                    </ControlTemplate>
                  </Setter>
                </ControlTheme>
              </Application.Resources>
              <Application.Styles>
                <FluentTheme />
              </Application.Styles>
            </Application>
            """);

        await File.WriteAllTextAsync(appCodeBehindPath, """
            using Avalonia;
            using Avalonia.Markup.Xaml;

            namespace ImplicitControlThemePreviewSample;

            public partial class App : Application
            {
                public override void Initialize()
                {
                    AvaloniaXamlLoader.Load(this);
                }
            }
            """);

        await File.WriteAllTextAsync(viewPath, """
            <UserControl xmlns="https://github.com/avaloniaui"
                         xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                         x:Class="ImplicitControlThemePreviewSample.Views.ImplicitThemeView">
              <Grid Width="220" Height="140" Background="#FFFFFFFF">
                <Button Width="160"
                        Height="80"
                        HorizontalAlignment="Center"
                        VerticalAlignment="Center" />
              </Grid>
            </UserControl>
            """);

        await File.WriteAllTextAsync(codeBehindPath, """
            using Avalonia.Controls;

            namespace ImplicitControlThemePreviewSample.Views;

            public partial class ImplicitThemeView : UserControl
            {
                public ImplicitThemeView()
                {
                    InitializeComponent();
                }
            }
            """);

        var request = new PreviewRequest(
            outputPath,
            width: 220,
            height: 140,
            dpi: 96,
            projectPath: projectPath,
            viewPath: Path.Combine("Views", "ImplicitThemeView.axaml"),
            themeVariant: "light");

        await File.WriteAllTextAsync(requestPath, JsonSerializer.Serialize(request, JsonOptions));

        try
        {
            var result = await RunPreviewHostAsync(hostAssembly, requestPath, expectedExitCode: 0);

            Assert.NotNull(result);
            Assert.True(result.Success, result.Error?.Message);
            Assert.Equal(Path.GetFullPath(outputPath), result.Value!.FilePath);
            AssertCenterPixel(outputPath, red: 0x2B, green: 0x6C, blue: 0xB0);
        }
        finally
        {
            await DeleteDirectoryWithRetryAsync(testRoot);
        }
    }

    [Fact]
    public async Task PreviewHostLoadsCompiledAppStyleIncludesBeforeProjectView()
    {
        var hostAssembly = Path.Combine(AppContext.BaseDirectory, "AvaScope.PreviewHost.dll");
        Assert.True(File.Exists(hostAssembly), $"Expected preview host assembly at {hostAssembly}.");

        var testRoot = Path.Combine(Path.GetTempPath(), "AvaScope.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(testRoot);

        var projectPath = Path.Combine(testRoot, "AppStyleIncludePreviewSample.csproj");
        var appPath = Path.Combine(testRoot, "App.axaml");
        var appCodeBehindPath = Path.Combine(testRoot, "App.axaml.cs");
        var stylesDirectory = Path.Combine(testRoot, "Styles");
        var viewsDirectory = Path.Combine(testRoot, "Views");
        Directory.CreateDirectory(stylesDirectory);
        Directory.CreateDirectory(viewsDirectory);

        var stylesPath = Path.Combine(stylesDirectory, "AppStyles.axaml");
        var viewPath = Path.Combine(viewsDirectory, "IncludedStyleView.axaml");
        var codeBehindPath = Path.Combine(viewsDirectory, "IncludedStyleView.axaml.cs");
        var requestPath = Path.Combine(testRoot, "request.json");
        var outputPath = Path.Combine(testRoot, "preview.png");

        await File.WriteAllTextAsync(projectPath, """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
                <Nullable>enable</Nullable>
              </PropertyGroup>
              <ItemGroup>
                <PackageReference Include="Avalonia" Version="12.1.0" />
              </ItemGroup>
            </Project>
            """);

        await File.WriteAllTextAsync(appPath, """
            <Application xmlns="https://github.com/avaloniaui"
                         xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                         x:Class="AppStyleIncludePreviewSample.App">
              <Application.Styles>
                <StyleInclude Source="avares://AppStyleIncludePreviewSample/Styles/AppStyles.axaml" />
              </Application.Styles>
            </Application>
            """);

        await File.WriteAllTextAsync(appCodeBehindPath, """
            using Avalonia;
            using Avalonia.Markup.Xaml;

            namespace AppStyleIncludePreviewSample;

            public partial class App : Application
            {
                public override void Initialize()
                {
                    AvaloniaXamlLoader.Load(this);
                }
            }
            """);

        await File.WriteAllTextAsync(stylesPath, """
            <Styles xmlns="https://github.com/avaloniaui"
                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
              <Style Selector="Border.includedStyleTarget">
                <Setter Property="Background" Value="#FF4C956C" />
              </Style>
            </Styles>
            """);

        await File.WriteAllTextAsync(viewPath, """
            <UserControl xmlns="https://github.com/avaloniaui"
                         xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                         x:Class="AppStyleIncludePreviewSample.Views.IncludedStyleView">
              <Border Classes="includedStyleTarget" Width="220" Height="140" />
            </UserControl>
            """);

        await File.WriteAllTextAsync(codeBehindPath, """
            using Avalonia.Controls;

            namespace AppStyleIncludePreviewSample.Views;

            public partial class IncludedStyleView : UserControl
            {
                public IncludedStyleView()
                {
                    InitializeComponent();
                }
            }
            """);

        var request = new PreviewRequest(
            outputPath,
            width: 220,
            height: 140,
            dpi: 96,
            projectPath: projectPath,
            viewPath: Path.Combine("Views", "IncludedStyleView.axaml"),
            themeVariant: "light");

        await File.WriteAllTextAsync(requestPath, JsonSerializer.Serialize(request, JsonOptions));

        try
        {
            var result = await RunPreviewHostAsync(hostAssembly, requestPath, expectedExitCode: 0);

            Assert.NotNull(result);
            Assert.True(result.Success, result.Error?.Message);
            Assert.Equal(Path.GetFullPath(outputPath), result.Value!.FilePath);
            AssertCenterPixel(outputPath, red: 0x4C, green: 0x95, blue: 0x6C);
        }
        finally
        {
            await DeleteDirectoryWithRetryAsync(testRoot);
        }
    }

    [Fact]
    public async Task PreviewHostLoadsCompiledAppResourceIncludesBeforeProjectView()
    {
        var hostAssembly = Path.Combine(AppContext.BaseDirectory, "AvaScope.PreviewHost.dll");
        Assert.True(File.Exists(hostAssembly), $"Expected preview host assembly at {hostAssembly}.");

        var testRoot = Path.Combine(Path.GetTempPath(), "AvaScope.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(testRoot);

        var projectPath = Path.Combine(testRoot, "AppResourceIncludePreviewSample.csproj");
        var appPath = Path.Combine(testRoot, "App.axaml");
        var appCodeBehindPath = Path.Combine(testRoot, "App.axaml.cs");
        var stylesDirectory = Path.Combine(testRoot, "Styles");
        var viewsDirectory = Path.Combine(testRoot, "Views");
        Directory.CreateDirectory(stylesDirectory);
        Directory.CreateDirectory(viewsDirectory);

        var palettePath = Path.Combine(stylesDirectory, "Palette.axaml");
        var viewPath = Path.Combine(viewsDirectory, "IncludedResourceView.axaml");
        var codeBehindPath = Path.Combine(viewsDirectory, "IncludedResourceView.axaml.cs");
        var requestPath = Path.Combine(testRoot, "request.json");
        var outputPath = Path.Combine(testRoot, "preview.png");

        await File.WriteAllTextAsync(projectPath, """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
                <Nullable>enable</Nullable>
              </PropertyGroup>
              <ItemGroup>
                <PackageReference Include="Avalonia" Version="12.1.0" />
              </ItemGroup>
            </Project>
            """);

        await File.WriteAllTextAsync(appPath, """
            <Application xmlns="https://github.com/avaloniaui"
                         xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                         x:Class="AppResourceIncludePreviewSample.App">
              <Application.Resources>
                <ResourceDictionary>
                  <ResourceDictionary.MergedDictionaries>
                    <ResourceInclude Source="avares://AppResourceIncludePreviewSample/Styles/Palette.axaml" />
                  </ResourceDictionary.MergedDictionaries>
                </ResourceDictionary>
              </Application.Resources>
            </Application>
            """);

        await File.WriteAllTextAsync(appCodeBehindPath, """
            using Avalonia;
            using Avalonia.Markup.Xaml;

            namespace AppResourceIncludePreviewSample;

            public partial class App : Application
            {
                public override void Initialize()
                {
                    AvaloniaXamlLoader.Load(this);
                }
            }
            """);

        await File.WriteAllTextAsync(palettePath, """
            <ResourceDictionary xmlns="https://github.com/avaloniaui"
                                xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
              <SolidColorBrush x:Key="IncludedPreviewBrush" Color="#FF7A3E9D" />
            </ResourceDictionary>
            """);

        await File.WriteAllTextAsync(viewPath, """
            <UserControl xmlns="https://github.com/avaloniaui"
                         xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                         x:Class="AppResourceIncludePreviewSample.Views.IncludedResourceView">
              <Border Width="220" Height="140" Background="{StaticResource IncludedPreviewBrush}" />
            </UserControl>
            """);

        await File.WriteAllTextAsync(codeBehindPath, """
            using Avalonia.Controls;

            namespace AppResourceIncludePreviewSample.Views;

            public partial class IncludedResourceView : UserControl
            {
                public IncludedResourceView()
                {
                    InitializeComponent();
                }
            }
            """);

        var request = new PreviewRequest(
            outputPath,
            width: 220,
            height: 140,
            dpi: 96,
            projectPath: projectPath,
            viewPath: Path.Combine("Views", "IncludedResourceView.axaml"),
            themeVariant: "light");

        await File.WriteAllTextAsync(requestPath, JsonSerializer.Serialize(request, JsonOptions));

        try
        {
            var result = await RunPreviewHostAsync(hostAssembly, requestPath, expectedExitCode: 0);

            Assert.NotNull(result);
            Assert.True(result.Success, result.Error?.Message);
            Assert.Equal(Path.GetFullPath(outputPath), result.Value!.FilePath);
            AssertCenterPixel(outputPath, red: 0x7A, green: 0x3E, blue: 0x9D);
        }
        finally
        {
            await DeleteDirectoryWithRetryAsync(testRoot);
        }
    }

    [Fact]
    public async Task PreviewHostResolvesCompiledAppThemeDictionariesForRequestedVariant()
    {
        var hostAssembly = Path.Combine(AppContext.BaseDirectory, "AvaScope.PreviewHost.dll");
        Assert.True(File.Exists(hostAssembly), $"Expected preview host assembly at {hostAssembly}.");

        var testRoot = Path.Combine(Path.GetTempPath(), "AvaScope.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(testRoot);

        var projectPath = Path.Combine(testRoot, "ThemeDictionaryPreviewSample.csproj");
        var appPath = Path.Combine(testRoot, "App.axaml");
        var appCodeBehindPath = Path.Combine(testRoot, "App.axaml.cs");
        var viewsDirectory = Path.Combine(testRoot, "Views");
        Directory.CreateDirectory(viewsDirectory);

        var viewPath = Path.Combine(viewsDirectory, "ThemeView.axaml");
        var codeBehindPath = Path.Combine(viewsDirectory, "ThemeView.axaml.cs");
        var requestPath = Path.Combine(testRoot, "request.json");
        var lightOutputPath = Path.Combine(testRoot, "preview-light.png");
        var darkOutputPath = Path.Combine(testRoot, "preview-dark.png");

        await File.WriteAllTextAsync(projectPath, """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
                <Nullable>enable</Nullable>
              </PropertyGroup>
              <ItemGroup>
                <PackageReference Include="Avalonia" Version="12.1.0" />
              </ItemGroup>
            </Project>
            """);

        await File.WriteAllTextAsync(appPath, """
            <Application xmlns="https://github.com/avaloniaui"
                         xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                         x:Class="ThemeDictionaryPreviewSample.App">
              <Application.Resources>
                <ResourceDictionary>
                  <ResourceDictionary.ThemeDictionaries>
                    <ResourceDictionary x:Key="Light">
                      <SolidColorBrush x:Key="VariantPreviewBrush" Color="#FF1B998B" />
                    </ResourceDictionary>
                    <ResourceDictionary x:Key="Dark">
                      <SolidColorBrush x:Key="VariantPreviewBrush" Color="#FFD7263D" />
                    </ResourceDictionary>
                  </ResourceDictionary.ThemeDictionaries>
                </ResourceDictionary>
              </Application.Resources>
            </Application>
            """);

        await File.WriteAllTextAsync(appCodeBehindPath, """
            using Avalonia;
            using Avalonia.Markup.Xaml;

            namespace ThemeDictionaryPreviewSample;

            public partial class App : Application
            {
                public override void Initialize()
                {
                    AvaloniaXamlLoader.Load(this);
                }
            }
            """);

        await File.WriteAllTextAsync(viewPath, """
            <UserControl xmlns="https://github.com/avaloniaui"
                         xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                         x:Class="ThemeDictionaryPreviewSample.Views.ThemeView">
              <Border Width="220" Height="140" Background="{DynamicResource VariantPreviewBrush}" />
            </UserControl>
            """);

        await File.WriteAllTextAsync(codeBehindPath, """
            using Avalonia.Controls;

            namespace ThemeDictionaryPreviewSample.Views;

            public partial class ThemeView : UserControl
            {
                public ThemeView()
                {
                    InitializeComponent();
                }
            }
            """);

        try
        {
            await File.WriteAllTextAsync(
                requestPath,
                JsonSerializer.Serialize(
                    new PreviewRequest(
                        lightOutputPath,
                        width: 220,
                        height: 140,
                        dpi: 96,
                        projectPath: projectPath,
                        viewPath: Path.Combine("Views", "ThemeView.axaml"),
                        themeVariant: "light"),
                    JsonOptions));
            var light = await RunPreviewHostAsync(hostAssembly, requestPath, expectedExitCode: 0);

            Assert.NotNull(light);
            Assert.True(light.Success, light.Error?.Message);
            AssertCenterPixel(lightOutputPath, red: 0x1B, green: 0x99, blue: 0x8B);

            await File.WriteAllTextAsync(
                requestPath,
                JsonSerializer.Serialize(
                    new PreviewRequest(
                        darkOutputPath,
                        width: 220,
                        height: 140,
                        dpi: 96,
                        projectPath: projectPath,
                        viewPath: Path.Combine("Views", "ThemeView.axaml"),
                        themeVariant: "dark"),
                    JsonOptions));
            var dark = await RunPreviewHostAsync(hostAssembly, requestPath, expectedExitCode: 0);

            Assert.NotNull(dark);
            Assert.True(dark.Success, dark.Error?.Message);
            AssertCenterPixel(darkOutputPath, red: 0xD7, green: 0x26, blue: 0x3D);
        }
        finally
        {
            await DeleteDirectoryWithRetryAsync(testRoot);
        }
    }

    [Fact]
    public async Task PreviewHostUsesDarkFluentWindowBackgroundForTransparentRootControl()
    {
        var hostAssembly = Path.Combine(AppContext.BaseDirectory, "AvaScope.PreviewHost.dll");
        Assert.True(File.Exists(hostAssembly), $"Expected preview host assembly at {hostAssembly}.");

        var testRoot = Path.Combine(Path.GetTempPath(), "AvaScope.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(testRoot);

        var projectPath = Path.Combine(testRoot, "DarkWindowBackgroundPreviewSample.csproj");
        var appPath = Path.Combine(testRoot, "App.axaml");
        var appCodeBehindPath = Path.Combine(testRoot, "App.axaml.cs");
        var viewsDirectory = Path.Combine(testRoot, "Views");
        Directory.CreateDirectory(viewsDirectory);

        var viewPath = Path.Combine(viewsDirectory, "TransparentRootView.axaml");
        var codeBehindPath = Path.Combine(viewsDirectory, "TransparentRootView.axaml.cs");
        var requestPath = Path.Combine(testRoot, "request.json");
        var outputPath = Path.Combine(testRoot, "preview.png");

        await File.WriteAllTextAsync(projectPath, """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
                <Nullable>enable</Nullable>
              </PropertyGroup>
              <ItemGroup>
                <PackageReference Include="Avalonia" Version="12.1.0" />
                <PackageReference Include="Avalonia.Themes.Fluent" Version="12.1.0" />
              </ItemGroup>
            </Project>
            """);

        await File.WriteAllTextAsync(appPath, """
            <Application xmlns="https://github.com/avaloniaui"
                         xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                         x:Class="DarkWindowBackgroundPreviewSample.App">
              <Application.Styles>
                <FluentTheme />
              </Application.Styles>
            </Application>
            """);

        await File.WriteAllTextAsync(appCodeBehindPath, """
            using Avalonia;
            using Avalonia.Markup.Xaml;

            namespace DarkWindowBackgroundPreviewSample;

            public partial class App : Application
            {
                public override void Initialize()
                {
                    AvaloniaXamlLoader.Load(this);
                }
            }
            """);

        await File.WriteAllTextAsync(viewPath, """
            <UserControl xmlns="https://github.com/avaloniaui"
                         xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                         x:Class="DarkWindowBackgroundPreviewSample.Views.TransparentRootView">
              <Grid />
            </UserControl>
            """);

        await File.WriteAllTextAsync(codeBehindPath, """
            using Avalonia.Controls;

            namespace DarkWindowBackgroundPreviewSample.Views;

            public partial class TransparentRootView : UserControl
            {
                public TransparentRootView()
                {
                    InitializeComponent();
                }
            }
            """);

        await File.WriteAllTextAsync(
            requestPath,
            JsonSerializer.Serialize(
                new PreviewRequest(
                    outputPath,
                    width: 180,
                    height: 120,
                    dpi: 96,
                    projectPath: projectPath,
                    viewPath: Path.Combine("Views", "TransparentRootView.axaml"),
                    themeVariant: "dark"),
                JsonOptions));

        try
        {
            var result = await RunPreviewHostAsync(hostAssembly, requestPath, expectedExitCode: 0);

            Assert.NotNull(result);
            Assert.True(result.Success, result.Error?.Message);
            Assert.Equal(Path.GetFullPath(outputPath), result.Value!.FilePath);
            AssertCenterPixel(outputPath, red: 0x00, green: 0x00, blue: 0x00);
        }
        finally
        {
            await DeleteDirectoryWithRetryAsync(testRoot);
        }
    }

    [Fact]
    public async Task PreviewHostKeepsAppWindowBackgroundStyleForTransparentRootControl()
    {
        var hostAssembly = Path.Combine(AppContext.BaseDirectory, "AvaScope.PreviewHost.dll");
        Assert.True(File.Exists(hostAssembly), $"Expected preview host assembly at {hostAssembly}.");

        var testRoot = Path.Combine(Path.GetTempPath(), "AvaScope.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(testRoot);

        var projectPath = Path.Combine(testRoot, "StyledWindowBackgroundPreviewSample.csproj");
        var appPath = Path.Combine(testRoot, "App.axaml");
        var appCodeBehindPath = Path.Combine(testRoot, "App.axaml.cs");
        var viewsDirectory = Path.Combine(testRoot, "Views");
        Directory.CreateDirectory(viewsDirectory);

        var viewPath = Path.Combine(viewsDirectory, "TransparentRootView.axaml");
        var codeBehindPath = Path.Combine(viewsDirectory, "TransparentRootView.axaml.cs");
        var requestPath = Path.Combine(testRoot, "request.json");
        var outputPath = Path.Combine(testRoot, "preview.png");

        await File.WriteAllTextAsync(projectPath, """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
                <Nullable>enable</Nullable>
              </PropertyGroup>
              <ItemGroup>
                <PackageReference Include="Avalonia" Version="12.1.0" />
                <PackageReference Include="Avalonia.Themes.Fluent" Version="12.1.0" />
              </ItemGroup>
            </Project>
            """);

        await File.WriteAllTextAsync(appPath, """
            <Application xmlns="https://github.com/avaloniaui"
                         xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                         x:Class="StyledWindowBackgroundPreviewSample.App">
              <Application.Resources>
                <ResourceDictionary>
                  <ResourceDictionary.ThemeDictionaries>
                    <ResourceDictionary x:Key="Light">
                      <SolidColorBrush x:Key="PreviewWindowBackgroundBrush" Color="#FFE9ECEF" />
                    </ResourceDictionary>
                    <ResourceDictionary x:Key="Dark">
                      <SolidColorBrush x:Key="PreviewWindowBackgroundBrush" Color="#FF123456" />
                    </ResourceDictionary>
                  </ResourceDictionary.ThemeDictionaries>
                </ResourceDictionary>
              </Application.Resources>
              <Application.Styles>
                <FluentTheme />
                <Style Selector="Window">
                  <Setter Property="Background" Value="{DynamicResource PreviewWindowBackgroundBrush}" />
                </Style>
              </Application.Styles>
            </Application>
            """);

        await File.WriteAllTextAsync(appCodeBehindPath, """
            using Avalonia;
            using Avalonia.Markup.Xaml;

            namespace StyledWindowBackgroundPreviewSample;

            public partial class App : Application
            {
                public override void Initialize()
                {
                    AvaloniaXamlLoader.Load(this);
                }
            }
            """);

        await File.WriteAllTextAsync(viewPath, """
            <UserControl xmlns="https://github.com/avaloniaui"
                         xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                         x:Class="StyledWindowBackgroundPreviewSample.Views.TransparentRootView">
              <Grid />
            </UserControl>
            """);

        await File.WriteAllTextAsync(codeBehindPath, """
            using Avalonia.Controls;

            namespace StyledWindowBackgroundPreviewSample.Views;

            public partial class TransparentRootView : UserControl
            {
                public TransparentRootView()
                {
                    InitializeComponent();
                }
            }
            """);

        await File.WriteAllTextAsync(
            requestPath,
            JsonSerializer.Serialize(
                new PreviewRequest(
                    outputPath,
                    width: 180,
                    height: 120,
                    dpi: 96,
                    projectPath: projectPath,
                    viewPath: Path.Combine("Views", "TransparentRootView.axaml"),
                    themeVariant: "dark"),
                JsonOptions));

        try
        {
            var result = await RunPreviewHostAsync(hostAssembly, requestPath, expectedExitCode: 0);

            Assert.NotNull(result);
            Assert.True(result.Success, result.Error?.Message);
            Assert.Equal(Path.GetFullPath(outputPath), result.Value!.FilePath);
            AssertCenterPixel(outputPath, red: 0x12, green: 0x34, blue: 0x56);
        }
        finally
        {
            await DeleteDirectoryWithRetryAsync(testRoot);
        }
    }

    [Fact]
    public async Task PreviewHostAppliesCompiledAppDataTemplatesBeforeProjectView()
    {
        var hostAssembly = Path.Combine(AppContext.BaseDirectory, "AvaScope.PreviewHost.dll");
        Assert.True(File.Exists(hostAssembly), $"Expected preview host assembly at {hostAssembly}.");

        var testRoot = Path.Combine(Path.GetTempPath(), "AvaScope.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(testRoot);

        var projectPath = Path.Combine(testRoot, "DataTemplatePreviewSample.csproj");
        var appPath = Path.Combine(testRoot, "App.axaml");
        var appCodeBehindPath = Path.Combine(testRoot, "App.axaml.cs");
        var viewPath = Path.Combine(testRoot, "DataTemplateView.axaml");
        var codeBehindPath = Path.Combine(testRoot, "DataTemplateView.axaml.cs");
        var designDataPath = Path.Combine(testRoot, "PreviewItem.cs");
        var requestPath = Path.Combine(testRoot, "request.json");
        var outputPath = Path.Combine(testRoot, "preview.png");

        await File.WriteAllTextAsync(projectPath, """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
                <Nullable>enable</Nullable>
              </PropertyGroup>
              <ItemGroup>
                <PackageReference Include="Avalonia" Version="12.1.0" />
              </ItemGroup>
            </Project>
            """);

        await File.WriteAllTextAsync(appPath, """
            <Application xmlns="https://github.com/avaloniaui"
                         xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                         xmlns:local="using:DataTemplatePreviewSample"
                         x:Class="DataTemplatePreviewSample.App">
              <Application.DataTemplates>
                <DataTemplate DataType="local:PreviewItem">
                  <Border Width="220" Height="140" Background="#FF2A9D8F" />
                </DataTemplate>
              </Application.DataTemplates>
            </Application>
            """);

        await File.WriteAllTextAsync(appCodeBehindPath, """
            using System;
            using Avalonia;
            using Avalonia.Markup.Xaml;

            namespace DataTemplatePreviewSample;

            public partial class App : Application
            {
                public override void Initialize()
                {
                    AvaloniaXamlLoader.Load(this);
                }

                public override void OnFrameworkInitializationCompleted()
                {
                    throw new InvalidOperationException("PreviewHost must not run app startup hooks for data-template previews.");
                }
            }
            """);

        await File.WriteAllTextAsync(viewPath, """
            <UserControl xmlns="https://github.com/avaloniaui"
                         xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                         xmlns:local="using:DataTemplatePreviewSample"
                         x:Class="DataTemplatePreviewSample.DataTemplateView"
                         x:DataType="local:PreviewItem">
              <ContentControl Width="220" Height="140" Content="{Binding}" />
            </UserControl>
            """);

        await File.WriteAllTextAsync(codeBehindPath, """
            using Avalonia.Controls;

            namespace DataTemplatePreviewSample;

            public partial class DataTemplateView : UserControl
            {
                public DataTemplateView()
                {
                    InitializeComponent();
                }
            }
            """);

        await File.WriteAllTextAsync(designDataPath, """
            namespace DataTemplatePreviewSample;

            public sealed class PreviewItem
            {
            }
            """);

        var request = new PreviewRequest(
            outputPath,
            width: 220,
            height: 140,
            dpi: 96,
            projectPath: projectPath,
            viewPath: "DataTemplateView.axaml",
            themeVariant: "light",
            designDataType: "DataTemplatePreviewSample.PreviewItem");

        await File.WriteAllTextAsync(requestPath, JsonSerializer.Serialize(request, JsonOptions));

        try
        {
            var result = await RunPreviewHostAsync(hostAssembly, requestPath, expectedExitCode: 0);

            Assert.NotNull(result);
            Assert.True(result.Success, result.Error?.Message);
            Assert.Equal(Path.GetFullPath(outputPath), result.Value!.FilePath);
            AssertCenterPixel(outputPath, red: 0x2A, green: 0x9D, blue: 0x8F);
        }
        finally
        {
            await DeleteDirectoryWithRetryAsync(testRoot);
        }
    }

    [Fact]
    public async Task PreviewHostAppliesApplicationDataContextAsFallbackRootDataContext()
    {
        var hostAssembly = Path.Combine(AppContext.BaseDirectory, "AvaScope.PreviewHost.dll");
        Assert.True(File.Exists(hostAssembly), $"Expected preview host assembly at {hostAssembly}.");

        var testRoot = Path.Combine(Path.GetTempPath(), "AvaScope.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(testRoot);

        var projectPath = Path.Combine(testRoot, "AppDataContextPreviewSample.csproj");
        var appPath = Path.Combine(testRoot, "App.axaml");
        var appCodeBehindPath = Path.Combine(testRoot, "App.axaml.cs");
        var viewPath = Path.Combine(testRoot, "AppDataContextView.axaml");
        var codeBehindPath = Path.Combine(testRoot, "AppDataContextView.axaml.cs");
        var shellDataPath = Path.Combine(testRoot, "PreviewShellData.cs");
        var requestPath = Path.Combine(testRoot, "request.json");
        var outputPath = Path.Combine(testRoot, "preview.png");

        await File.WriteAllTextAsync(projectPath, """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
                <Nullable>enable</Nullable>
              </PropertyGroup>
              <ItemGroup>
                <PackageReference Include="Avalonia" Version="12.1.0" />
              </ItemGroup>
            </Project>
            """);

        await File.WriteAllTextAsync(appPath, """
            <Application xmlns="https://github.com/avaloniaui"
                         xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                         x:Class="AppDataContextPreviewSample.App" />
            """);

        await File.WriteAllTextAsync(appCodeBehindPath, """
            using System;
            using Avalonia;
            using Avalonia.Markup.Xaml;

            namespace AppDataContextPreviewSample;

            public partial class App : Application
            {
                public override void Initialize()
                {
                    AvaloniaXamlLoader.Load(this);
                    DataContext = new PreviewShellData();
                }

                public override void OnFrameworkInitializationCompleted()
                {
                    throw new InvalidOperationException("PreviewHost must not run app startup hooks for App.DataContext previews.");
                }
            }
            """);

        await File.WriteAllTextAsync(viewPath, """
            <UserControl xmlns="https://github.com/avaloniaui"
                         xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                         xmlns:local="using:AppDataContextPreviewSample"
                         x:Class="AppDataContextPreviewSample.AppDataContextView"
                         x:DataType="local:PreviewShellData">
              <Border Width="220" Height="140" Background="{CompiledBinding PreviewBrush}" />
            </UserControl>
            """);

        await File.WriteAllTextAsync(codeBehindPath, """
            using Avalonia.Controls;

            namespace AppDataContextPreviewSample;

            public partial class AppDataContextView : UserControl
            {
                public AppDataContextView()
                {
                    InitializeComponent();
                }
            }
            """);

        await File.WriteAllTextAsync(shellDataPath, """
            using Avalonia.Media;

            namespace AppDataContextPreviewSample;

            public sealed class PreviewShellData
            {
                public IBrush PreviewBrush { get; } = new SolidColorBrush(Color.FromArgb(0xFF, 0x46, 0x7A, 0xA7));
            }
            """);

        var request = new PreviewRequest(
            outputPath,
            width: 220,
            height: 140,
            dpi: 96,
            projectPath: projectPath,
            viewPath: "AppDataContextView.axaml",
            themeVariant: "light");

        await File.WriteAllTextAsync(requestPath, JsonSerializer.Serialize(request, JsonOptions));

        try
        {
            var result = await RunPreviewHostAsync(hostAssembly, requestPath, expectedExitCode: 0);

            Assert.NotNull(result);
            Assert.True(result.Success, result.Error?.Message);
            Assert.Equal(Path.GetFullPath(outputPath), result.Value!.FilePath);
            AssertCenterPixel(outputPath, red: 0x46, green: 0x7A, blue: 0xA7);
        }
        finally
        {
            await DeleteDirectoryWithRetryAsync(testRoot);
        }
    }

    [Fact]
    public async Task PreviewHostAppliesRequestedCultureBeforeProjectViewLoading()
    {
        var hostAssembly = Path.Combine(AppContext.BaseDirectory, "AvaScope.PreviewHost.dll");
        Assert.True(File.Exists(hostAssembly), $"Expected preview host assembly at {hostAssembly}.");

        var testRoot = Path.Combine(Path.GetTempPath(), "AvaScope.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(testRoot);

        var projectPath = Path.Combine(testRoot, "CulturePreviewSample.csproj");
        var viewPath = Path.Combine(testRoot, "CultureView.axaml");
        var codeBehindPath = Path.Combine(testRoot, "CultureView.axaml.cs");
        var requestPath = Path.Combine(testRoot, "request.json");
        var outputPath = Path.Combine(testRoot, "preview.png");

        await File.WriteAllTextAsync(projectPath, """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
                <Nullable>enable</Nullable>
              </PropertyGroup>
              <ItemGroup>
                <PackageReference Include="Avalonia" Version="12.1.0" />
              </ItemGroup>
            </Project>
            """);

        await File.WriteAllTextAsync(viewPath, """
            <UserControl xmlns="https://github.com/avaloniaui"
                         xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                         x:Class="CulturePreviewSample.CultureView" />
            """);

        await File.WriteAllTextAsync(codeBehindPath, """
            using System.Globalization;
            using Avalonia.Controls;
            using Avalonia.Media;

            namespace CulturePreviewSample;

            public partial class CultureView : UserControl
            {
                public CultureView()
                {
                    InitializeComponent();
                    var color = CultureInfo.CurrentCulture.Name == "ja-JP"
                        ? Color.FromArgb(0xFF, 0x0E, 0x7C, 0x7B)
                        : Color.FromArgb(0xFF, 0xE4, 0x57, 0x2E);
                    Content = new Border
                    {
                        Width = 220,
                        Height = 140,
                        Background = new SolidColorBrush(color)
                    };
                }
            }
            """);

        var request = new PreviewRequest(
            outputPath,
            width: 220,
            height: 140,
            dpi: 96,
            projectPath: projectPath,
            viewPath: "CultureView.axaml",
            themeVariant: "light",
            culture: "ja-JP");

        await File.WriteAllTextAsync(requestPath, JsonSerializer.Serialize(request, JsonOptions));

        try
        {
            var result = await RunPreviewHostAsync(hostAssembly, requestPath, expectedExitCode: 0);

            Assert.NotNull(result);
            Assert.True(result.Success, result.Error?.Message);
            Assert.Equal("ja-JP", result.Value!.Culture);
            AssertCenterPixel(outputPath, red: 0x0E, green: 0x7C, blue: 0x7B);
        }
        finally
        {
            await DeleteDirectoryWithRetryAsync(testRoot);
        }
    }

    [Fact]
    public async Task PreviewHostAppliesProjectDesignDataTypeAsRootDataContext()
    {
        var hostAssembly = Path.Combine(AppContext.BaseDirectory, "AvaScope.PreviewHost.dll");
        Assert.True(File.Exists(hostAssembly), $"Expected preview host assembly at {hostAssembly}.");

        var testRoot = Path.Combine(Path.GetTempPath(), "AvaScope.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(testRoot);

        var projectPath = Path.Combine(testRoot, "DesignDataPreviewSample.csproj");
        var viewPath = Path.Combine(testRoot, "DesignDataView.axaml");
        var codeBehindPath = Path.Combine(testRoot, "DesignDataView.axaml.cs");
        var designDataPath = Path.Combine(testRoot, "PreviewDesignData.cs");
        var requestPath = Path.Combine(testRoot, "request.json");
        var outputPath = Path.Combine(testRoot, "preview.png");

        await File.WriteAllTextAsync(projectPath, """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
                <Nullable>enable</Nullable>
              </PropertyGroup>
              <ItemGroup>
                <PackageReference Include="Avalonia" Version="12.1.0" />
              </ItemGroup>
            </Project>
            """);

        await File.WriteAllTextAsync(viewPath, """
            <UserControl xmlns="https://github.com/avaloniaui"
                         xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                         xmlns:local="using:DesignDataPreviewSample"
                         x:Class="DesignDataPreviewSample.DesignDataView"
                         x:DataType="local:PreviewDesignData">
              <Border Width="220" Height="140" Background="{CompiledBinding PreviewBrush}" />
            </UserControl>
            """);

        await File.WriteAllTextAsync(codeBehindPath, """
            using Avalonia.Controls;

            namespace DesignDataPreviewSample;

            public partial class DesignDataView : UserControl
            {
                public DesignDataView()
                {
                    InitializeComponent();
                }
            }
            """);

        await File.WriteAllTextAsync(designDataPath, """
            using Avalonia.Media;

            namespace DesignDataPreviewSample;

            public sealed class PreviewDesignData
            {
                public IBrush PreviewBrush { get; } = new SolidColorBrush(Color.FromArgb(0xFF, 0x5C, 0x2A, 0x9D));
            }
            """);

        var request = new PreviewRequest(
            outputPath,
            width: 220,
            height: 140,
            dpi: 96,
            projectPath: projectPath,
            viewPath: "DesignDataView.axaml",
            themeVariant: "light",
            culture: "ja-JP",
            designDataType: "DesignDataPreviewSample.PreviewDesignData");

        await File.WriteAllTextAsync(requestPath, JsonSerializer.Serialize(request, JsonOptions));

        try
        {
            var result = await RunPreviewHostAsync(hostAssembly, requestPath, expectedExitCode: 0);

            Assert.NotNull(result);
            Assert.True(result.Success, result.Error?.Message);
            Assert.Equal("DesignDataPreviewSample.PreviewDesignData", result.Value!.DesignDataType);
            AssertCenterPixel(outputPath, red: 0x5C, green: 0x2A, blue: 0x9D);
        }
        finally
        {
            await DeleteDirectoryWithRetryAsync(testRoot);
        }
    }

    [Fact]
    public async Task PreviewHostAppliesExplicitStateVariantThroughDesignDataFactory()
    {
        var hostAssembly = Path.Combine(AppContext.BaseDirectory, "AvaScope.PreviewHost.dll");
        Assert.True(File.Exists(hostAssembly), $"Expected preview host assembly at {hostAssembly}.");

        var testRoot = Path.Combine(Path.GetTempPath(), "AvaScope.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(testRoot);

        var projectPath = Path.Combine(testRoot, "StateVariantPreviewSample.csproj");
        var viewPath = Path.Combine(testRoot, "StateVariantView.axaml");
        var codeBehindPath = Path.Combine(testRoot, "StateVariantView.axaml.cs");
        var designDataPath = Path.Combine(testRoot, "PreviewDesignData.cs");
        var requestPath = Path.Combine(testRoot, "request.json");
        var outputPath = Path.Combine(testRoot, "preview.png");

        await File.WriteAllTextAsync(projectPath, """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
                <Nullable>enable</Nullable>
              </PropertyGroup>
              <ItemGroup>
                <PackageReference Include="Avalonia" Version="12.1.0" />
              </ItemGroup>
            </Project>
            """);

        await File.WriteAllTextAsync(viewPath, """
            <UserControl xmlns="https://github.com/avaloniaui"
                         xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                         xmlns:local="using:StateVariantPreviewSample"
                         x:Class="StateVariantPreviewSample.StateVariantView"
                         x:DataType="local:PreviewDesignData">
              <Border Width="220" Height="140" Background="{Binding PreviewBrush}" />
            </UserControl>
            """);

        await File.WriteAllTextAsync(codeBehindPath, """
            using Avalonia.Controls;

            namespace StateVariantPreviewSample;

            public partial class StateVariantView : UserControl
            {
                public StateVariantView()
                {
                    InitializeComponent();
                }
            }
            """);

        await File.WriteAllTextAsync(designDataPath, """
            using Avalonia.Media;

            namespace StateVariantPreviewSample;

            public sealed class PreviewDesignData
            {
                public PreviewDesignData()
                    : this("default")
                {
                }

                private PreviewDesignData(string state)
                {
                    PreviewBrush = state == "loading"
                        ? new SolidColorBrush(Color.FromArgb(0xFF, 0x1B, 0x99, 0x8B))
                        : new SolidColorBrush(Color.FromArgb(0xFF, 0x5C, 0x2A, 0x9D));
                }

                public static PreviewDesignData ForState(string state) => new(state);

                public IBrush PreviewBrush { get; }
            }
            """);

        var request = new PreviewRequest(
            outputPath,
            width: 220,
            height: 140,
            dpi: 96,
            projectPath: projectPath,
            viewPath: "StateVariantView.axaml",
            themeVariant: "light",
            designDataType: "StateVariantPreviewSample.PreviewDesignData",
            stateVariant: "loading");

        await File.WriteAllTextAsync(requestPath, JsonSerializer.Serialize(request, JsonOptions));

        try
        {
            var result = await RunPreviewHostAsync(hostAssembly, requestPath, expectedExitCode: 0);

            Assert.NotNull(result);
            Assert.True(result.Success, result.Error?.Message);
            Assert.Equal("loading", result.Value!.StateVariant);
            var diagnostic = Assert.Single(result.Value.Diagnostics, diagnostic => diagnostic.Code == "state_variant_applied");
            Assert.Equal("loading", diagnostic.Details["stateVariant"]);
            Assert.Equal("static_ForState", diagnostic.Details["activationKind"]);
            AssertCenterPixel(outputPath, red: 0x1B, green: 0x99, blue: 0x8B);
        }
        finally
        {
            await DeleteDirectoryWithRetryAsync(testRoot);
        }
    }

    [Fact]
    public async Task PreviewHostAppliesDesignTimeStaticDataContextAsRootDataContext()
    {
        var hostAssembly = Path.Combine(AppContext.BaseDirectory, "AvaScope.PreviewHost.dll");
        Assert.True(File.Exists(hostAssembly), $"Expected preview host assembly at {hostAssembly}.");

        var testRoot = Path.Combine(Path.GetTempPath(), "AvaScope.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(testRoot);

        var projectPath = Path.Combine(testRoot, "StaticDesignDataPreviewSample.csproj");
        var viewPath = Path.Combine(testRoot, "StaticDesignDataView.axaml");
        var codeBehindPath = Path.Combine(testRoot, "StaticDesignDataView.axaml.cs");
        var designDataPath = Path.Combine(testRoot, "TargetDesignData.cs");
        var requestPath = Path.Combine(testRoot, "request.json");
        var outputPath = Path.Combine(testRoot, "preview.png");

        await File.WriteAllTextAsync(projectPath, """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
                <Nullable>enable</Nullable>
              </PropertyGroup>
              <ItemGroup>
                <PackageReference Include="Avalonia" Version="12.1.0" />
              </ItemGroup>
            </Project>
            """);

        await File.WriteAllTextAsync(viewPath, """
            <UserControl xmlns="https://github.com/avaloniaui"
                         xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                         xmlns:d="http://schemas.microsoft.com/expression/blend/2008"
                         xmlns:design="clr-namespace:StaticDesignDataPreviewSample"
                         xmlns:mc="http://schemas.openxmlformats.org/markup-compatibility/2006"
                         x:Class="StaticDesignDataPreviewSample.StaticDesignDataView"
                         x:DataType="design:PreviewDesignData"
                         d:DataContext="{x:Static design:TargetDesignData.LiveTrade}"
                         mc:Ignorable="d">
              <Border Width="220" Height="140" Background="{CompiledBinding PreviewBrush}" />
            </UserControl>
            """);

        await File.WriteAllTextAsync(codeBehindPath, """
            using Avalonia.Controls;

            namespace StaticDesignDataPreviewSample;

            public partial class StaticDesignDataView : UserControl
            {
                public StaticDesignDataView()
                {
                    InitializeComponent();
                }
            }
            """);

        await File.WriteAllTextAsync(designDataPath, """
            using Avalonia.Media;

            namespace StaticDesignDataPreviewSample;

            public static class TargetDesignData
            {
                public static PreviewDesignData LiveTrade { get; } = new();
            }

            public sealed class PreviewDesignData
            {
                public IBrush PreviewBrush { get; } = new SolidColorBrush(Color.FromArgb(0xFF, 0xC1, 0x12, 0x1F));
            }
            """);

        var request = new PreviewRequest(
            outputPath,
            width: 220,
            height: 140,
            dpi: 96,
            projectPath: projectPath,
            viewPath: "StaticDesignDataView.axaml",
            themeVariant: "light");

        await File.WriteAllTextAsync(requestPath, JsonSerializer.Serialize(request, JsonOptions));

        try
        {
            var result = await RunPreviewHostAsync(hostAssembly, requestPath, expectedExitCode: 0);

            Assert.NotNull(result);
            Assert.True(result.Success, result.Error?.Message);
            Assert.Null(result.Value!.DesignDataType);
            AssertCenterPixel(outputPath, red: 0xC1, green: 0x12, blue: 0x1F);
        }
        finally
        {
            await DeleteDirectoryWithRetryAsync(testRoot);
        }
    }

    [Fact]
    public async Task PreviewHostAppliesAttachedDesignDataContextAsRootDataContext()
    {
        var hostAssembly = Path.Combine(AppContext.BaseDirectory, "AvaScope.PreviewHost.dll");
        Assert.True(File.Exists(hostAssembly), $"Expected preview host assembly at {hostAssembly}.");

        var testRoot = Path.Combine(Path.GetTempPath(), "AvaScope.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(testRoot);

        var projectPath = Path.Combine(testRoot, "AttachedDesignDataPreviewSample.csproj");
        var viewPath = Path.Combine(testRoot, "AttachedDesignDataView.axaml");
        var codeBehindPath = Path.Combine(testRoot, "AttachedDesignDataView.axaml.cs");
        var designDataPath = Path.Combine(testRoot, "PreviewDesignData.cs");
        var requestPath = Path.Combine(testRoot, "request.json");
        var outputPath = Path.Combine(testRoot, "preview.png");

        await File.WriteAllTextAsync(projectPath, """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
                <Nullable>enable</Nullable>
              </PropertyGroup>
              <ItemGroup>
                <PackageReference Include="Avalonia" Version="12.1.0" />
              </ItemGroup>
            </Project>
            """);

        await File.WriteAllTextAsync(viewPath, """
            <UserControl xmlns="https://github.com/avaloniaui"
                         xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                         xmlns:design="using:AttachedDesignDataPreviewSample"
                         x:Class="AttachedDesignDataPreviewSample.AttachedDesignDataView"
                         x:DataType="design:PreviewDesignData">
              <Design.DataContext>
                <design:PreviewDesignData />
              </Design.DataContext>
              <Border Width="220" Height="140" Background="{CompiledBinding PreviewBrush}" />
            </UserControl>
            """);

        await File.WriteAllTextAsync(codeBehindPath, """
            using Avalonia.Controls;

            namespace AttachedDesignDataPreviewSample;

            public partial class AttachedDesignDataView : UserControl
            {
                public AttachedDesignDataView()
                {
                    InitializeComponent();
                }
            }
            """);

        await File.WriteAllTextAsync(designDataPath, """
            using Avalonia.Media;

            namespace AttachedDesignDataPreviewSample;

            public sealed class PreviewDesignData
            {
                public IBrush PreviewBrush { get; } = new SolidColorBrush(Color.FromArgb(0xFF, 0x21, 0x77, 0x3F));
            }
            """);

        var request = new PreviewRequest(
            outputPath,
            width: 220,
            height: 140,
            dpi: 96,
            projectPath: projectPath,
            viewPath: "AttachedDesignDataView.axaml",
            themeVariant: "light");

        await File.WriteAllTextAsync(requestPath, JsonSerializer.Serialize(request, JsonOptions));

        try
        {
            var result = await RunPreviewHostAsync(hostAssembly, requestPath, expectedExitCode: 0);

            Assert.NotNull(result);
            Assert.True(result.Success, result.Error?.Message);
            AssertCenterPixel(outputPath, red: 0x21, green: 0x77, blue: 0x3F);
        }
        finally
        {
            await DeleteDirectoryWithRetryAsync(testRoot);
        }
    }

    [Fact]
    public async Task PreviewHostUsesDesignDimensionsWhenRequestOmitsDimensions()
    {
        var hostAssembly = Path.Combine(AppContext.BaseDirectory, "AvaScope.PreviewHost.dll");
        Assert.True(File.Exists(hostAssembly), $"Expected preview host assembly at {hostAssembly}.");

        var testRoot = Path.Combine(Path.GetTempPath(), "AvaScope.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(testRoot);

        var projectPath = Path.Combine(testRoot, "DesignDimensionsPreviewSample.csproj");
        var viewPath = Path.Combine(testRoot, "DesignDimensionsView.axaml");
        var requestPath = Path.Combine(testRoot, "request.json");
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
                         d:DesignWidth="310"
                         d:DesignHeight="170"
                         mc:Ignorable="d">
              <Border Background="#FF005A9C" />
            </UserControl>
            """);

        var request = new PreviewRequest(
            outputPath,
            dpi: 96,
            projectPath: projectPath,
            viewPath: "DesignDimensionsView.axaml",
            themeVariant: "light");

        await File.WriteAllTextAsync(requestPath, JsonSerializer.Serialize(request, JsonOptions));

        try
        {
            var result = await RunPreviewHostAsync(hostAssembly, requestPath, expectedExitCode: 0);

            Assert.NotNull(result);
            Assert.True(result.Success, result.Error?.Message);
            Assert.Equal(310, result.Value!.PixelWidth);
            Assert.Equal(170, result.Value.PixelHeight);
            AssertCenterPixel(outputPath, red: 0x00, green: 0x5A, blue: 0x9C);
        }
        finally
        {
            await DeleteDirectoryWithRetryAsync(testRoot);
        }
    }

    [Fact]
    public async Task PreviewHostReturnsStructuredErrorForUnsupportedDesignDataContext()
    {
        var hostAssembly = Path.Combine(AppContext.BaseDirectory, "AvaScope.PreviewHost.dll");
        Assert.True(File.Exists(hostAssembly), $"Expected preview host assembly at {hostAssembly}.");

        var testRoot = Path.Combine(Path.GetTempPath(), "AvaScope.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(testRoot);

        var projectPath = Path.Combine(testRoot, "UnsupportedDesignDataPreviewSample.csproj");
        var viewPath = Path.Combine(testRoot, "UnsupportedDesignDataView.axaml");
        var requestPath = Path.Combine(testRoot, "request.json");
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
                         d:DataContext="{Binding LiveTrade}"
                         mc:Ignorable="d">
              <Border />
            </UserControl>
            """);

        var request = new PreviewRequest(
            outputPath,
            width: 220,
            height: 140,
            dpi: 96,
            projectPath: projectPath,
            viewPath: "UnsupportedDesignDataView.axaml");

        await File.WriteAllTextAsync(requestPath, JsonSerializer.Serialize(request, JsonOptions));

        try
        {
            var result = await RunPreviewHostAsync(hostAssembly, requestPath, expectedExitCode: 1);

            Assert.NotNull(result);
            Assert.False(result.Success);
            Assert.Equal("preview_render_failed", result.Error!.Code);
            Assert.NotNull(result.Error.Details);
            Assert.Equal("render", result.Error.Details!["phase"]);
            Assert.Equal("{Binding LiveTrade}", result.Error.Details["designDataContext"]);
            Assert.False(File.Exists(outputPath));
        }
        finally
        {
            await DeleteDirectoryWithRetryAsync(testRoot);
        }
    }

    [Fact]
    public async Task PreviewHostReturnsStructuredErrorWhenDesignDataTypeIsMissing()
    {
        var hostAssembly = Path.Combine(AppContext.BaseDirectory, "AvaScope.PreviewHost.dll");
        Assert.True(File.Exists(hostAssembly), $"Expected preview host assembly at {hostAssembly}.");

        var testRoot = Path.Combine(Path.GetTempPath(), "AvaScope.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(testRoot);

        var projectPath = Path.Combine(testRoot, "MissingDesignDataPreviewSample.csproj");
        var viewPath = Path.Combine(testRoot, "MainView.axaml");
        var requestPath = Path.Combine(testRoot, "request.json");
        var outputPath = Path.Combine(testRoot, "preview.png");

        await File.WriteAllTextAsync(projectPath, """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
              </PropertyGroup>
            </Project>
            """);

        await File.WriteAllTextAsync(viewPath, """
            <UserControl xmlns="https://github.com/avaloniaui" />
            """);

        var request = new PreviewRequest(
            outputPath,
            width: 220,
            height: 140,
            dpi: 96,
            projectPath: projectPath,
            viewPath: "MainView.axaml",
            designDataType: "MissingDesignDataPreviewSample.MissingData");

        await File.WriteAllTextAsync(requestPath, JsonSerializer.Serialize(request, JsonOptions));

        try
        {
            var result = await RunPreviewHostAsync(hostAssembly, requestPath, expectedExitCode: 1);

            Assert.NotNull(result);
            Assert.False(result.Success);
            Assert.Equal("invalid_preview_request", result.Error!.Code);
            Assert.Contains("Design data type", result.Error.Message, StringComparison.Ordinal);
            Assert.False(File.Exists(outputPath));
        }
        finally
        {
            await DeleteDirectoryWithRetryAsync(testRoot);
        }
    }

    [Fact]
    public async Task PreviewHostReturnsStructuredErrorWhenAppResourceRootIsNotApplication()
    {
        var hostAssembly = Path.Combine(AppContext.BaseDirectory, "AvaScope.PreviewHost.dll");
        Assert.True(File.Exists(hostAssembly), $"Expected preview host assembly at {hostAssembly}.");

        var testRoot = Path.Combine(Path.GetTempPath(), "AvaScope.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(testRoot);

        var projectPath = Path.Combine(testRoot, "BrokenAppResourceSample.csproj");
        var appPath = Path.Combine(testRoot, "App.axaml");
        var viewPath = Path.Combine(testRoot, "MainView.axaml");
        var requestPath = Path.Combine(testRoot, "request.json");
        var outputPath = Path.Combine(testRoot, "preview.png");

        await File.WriteAllTextAsync(projectPath, """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
              </PropertyGroup>
              <ItemGroup>
                <PackageReference Include="Avalonia" Version="12.1.0" />
              </ItemGroup>
            </Project>
            """);

        await File.WriteAllTextAsync(appPath, """
            <UserControl xmlns="https://github.com/avaloniaui" />
            """);

        await File.WriteAllTextAsync(viewPath, """
            <UserControl xmlns="https://github.com/avaloniaui">
              <TextBlock Text="Should not render" />
            </UserControl>
            """);

        var request = new PreviewRequest(
            outputPath,
            width: 240,
            height: 160,
            dpi: 96,
            projectPath: projectPath,
            viewPath: "MainView.axaml");

        await File.WriteAllTextAsync(requestPath, JsonSerializer.Serialize(request, JsonOptions));

        try
        {
            var result = await RunPreviewHostAsync(hostAssembly, requestPath, expectedExitCode: 1);

            Assert.NotNull(result);
            Assert.False(result.Success);
            Assert.Equal("preview_render_failed", result.Error!.Code);
            Assert.Contains("App.axaml", result.Error.Message, StringComparison.Ordinal);
            Assert.False(File.Exists(outputPath));
        }
        finally
        {
            await DeleteDirectoryWithRetryAsync(testRoot);
        }
    }

    private static async Task DeleteDirectoryWithRetryAsync(string path)
    {
        for (var attempt = 0; attempt < 30; attempt++)
        {
            if (!Directory.Exists(path))
            {
                return;
            }

            try
            {
                Directory.Delete(path, recursive: true);
                return;
            }
            catch (IOException)
            {
                await Task.Delay(100);
            }
            catch (UnauthorizedAccessException)
            {
                await Task.Delay(100);
            }
        }
    }

    private async Task<ToolResult<PreviewResponse>?> RunPreviewHostAsync(
        string hostAssembly,
        string requestPath,
        int expectedExitCode,
        Func<Process>? startProcess = null)
    {
        using var process = startProcess is null ? StartPreviewHost(hostAssembly, requestPath) : startProcess();
        var timer = Stopwatch.StartNew();
        var phase = "process_wait";
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        using var captureCancellation = new CancellationTokenSource();
        var stdoutReader = process.StandardOutput;
        var stderrReader = process.StandardError;
        var streamsClosed = false;
        // Keep readers alive through owned termination; the operation deadline must not erase output.
        var stdoutTask = ReadCapturedStream(stdoutReader);
        var stderrTask = ReadCapturedStream(stderrReader);
        try
        {
            await process.WaitForExitAsync(cancellation.Token);
            phase = "stdout_drain";
            var stdout = await stdoutTask.WaitAsync(cancellation.Token);
            phase = "stderr_drain";
            var stderr = await stderrTask.WaitAsync(cancellation.Token);
            phase = "exit_assertion";
            Assert.True(
                process.ExitCode == expectedExitCode,
                $"Expected exit code {expectedExitCode}, got {process.ExitCode}.{Environment.NewLine}stdout: {Tail(stdout)}{Environment.NewLine}stderr: {Tail(stderr)}");
            phase = "stderr_assertion";
            Assert.True(string.IsNullOrWhiteSpace(stderr), Tail(stderr));
            phase = "response_parse";
            return JsonSerializer.Deserialize<ToolResult<PreviewResponse>>(stdout, JsonOptions);
        }
        catch (Exception primary)
        {
            var failedAfterMs = timer.Elapsed.TotalMilliseconds;
            var cleanupFailures = new List<string>();
            var exited = false;
            var exitedBeforeCleanup = false;
            int? exitCode = null;
            try
            {
                exitedBeforeCleanup = process.HasExited;
                if (!exitedBeforeCleanup) process.Kill(entireProcessTree: true);
            }
            catch (Exception failure) { cleanupFailures.Add("terminate:" + failure.GetType().Name); }
            try
            {
                await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(3));
                exited = true;
                exitCode = process.ExitCode;
            }
            catch (Exception failure) { cleanupFailures.Add("exit:" + failure.GetType().Name); }
            var stdout = await CaptureTailAsync(stdoutTask, "stdout");
            var stderr = await CaptureTailAsync(stderrTask, "stderr");
            captureCancellation.Cancel();
            CloseReader(stdoutReader, "stdout");
            CloseReader(stderrReader, "stderr");
            streamsClosed = true;
            var evidence = new Dictionary<string, object?>
            {
                ["operation"] = "preview_host_test_process", ["processId"] = process.Id,
                ["requestFile"] = Path.GetFileName(requestPath), ["processPhase"] = phase,
                ["failureType"] = primary.GetType().Name, ["elapsedMs"] = failedAfterMs,
                ["timeoutMs"] = 60000, ["cleanupWaitMs"] = 3000,
                ["exitedBeforeCleanup"] = exitedBeforeCleanup, ["cleanupProcessExited"] = exited,
                ["exitCode"] = exitCode, ["cleanupFailures"] = cleanupFailures,
                ["stdoutState"] = stdoutTask.Status.ToString(), ["stderrState"] = stderrTask.Status.ToString(),
                ["stdoutTail"] = stdout, ["stderrTail"] = stderr
            };
            foreach (var entry in evidence) primary.Data[entry.Key] = entry.Value;
            try { output.WriteLine(JsonSerializer.Serialize(evidence)); }
            catch (Exception failure) { primary.Data["evidenceWriteFailureType"] = failure.GetType().Name; }
            throw;

            async Task<string> CaptureTailAsync(Task<string> read, string stream)
            {
                try { return Tail(await read.WaitAsync(TimeSpan.FromSeconds(3))); }
                catch (Exception failure)
                {
                    cleanupFailures.Add(stream + "_drain:" + failure.GetType().Name);
                    _ = read.ContinueWith(task => { _ = task.Exception; }, CancellationToken.None,
                        TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
                    return "[capture unavailable]";
                }
            }

            void CloseReader(StreamReader reader, string stream)
            {
                try { reader.Dispose(); }
                catch (Exception failure) { cleanupFailures.Add(stream + "_close:" + failure.GetType().Name); }
            }
        }
        finally
        {
            captureCancellation.Cancel();
            if (!streamsClosed) { stdoutReader.Dispose(); stderrReader.Dispose(); }
        }

        Task<string> ReadCapturedStream(StreamReader reader) => OperatingSystem.IsWindows()
            // Redirected Windows pipes use synchronous IO; dedicated readers also observe real EOF under worker contention.
            ? Task.Factory.StartNew(reader.ReadToEnd, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default)
            : reader.ReadToEndAsync(captureCancellation.Token);

        static string Tail(string text) => text.Length <= 4096 ? text : text[^4096..];
    }

    private static Process StartPreviewHost(string hostAssembly, string requestPath)
    {
        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = $"\"{hostAssembly}\" --request \"{requestPath}\"",
                WorkingDirectory = AppContext.BaseDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            }
        };

        Assert.True(process.Start());
        return process;
    }

    private static void AssertCenterPixel(
        string filePath,
        byte red,
        byte green,
        byte blue)
    {
        using var bitmap = SKBitmap.Decode(filePath);
        Assert.NotNull(bitmap);

        var color = bitmap.GetPixel(bitmap.Width / 2, bitmap.Height / 2);
        Assert.InRange(Math.Abs(color.Red - red), 0, 3);
        Assert.InRange(Math.Abs(color.Green - green), 0, 3);
        Assert.InRange(Math.Abs(color.Blue - blue), 0, 3);
        Assert.Equal(byte.MaxValue, color.Alpha);
    }
}
