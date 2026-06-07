using System.Diagnostics;
using System.Text.Json;
using AvaScope.Protocol;

namespace AvaScope.Tests.PreviewHost;

public sealed class PreviewHostSmokeTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

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

        await File.WriteAllTextAsync(projectPath, """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
                <Nullable>enable</Nullable>
              </PropertyGroup>
              <ItemGroup>
                <PackageReference Include="Avalonia" Version="12.0.4" />
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
            var result = await RunPreviewHostAsync(hostAssembly, requestPath, expectedExitCode: 0);

            Assert.NotNull(result);
            Assert.True(result.Success, result.Error?.Message);
            Assert.Equal(Path.GetFullPath(projectPath), result.Value!.ProjectPath);
            Assert.Equal(Path.GetFullPath(viewPath), result.Value.ViewPath);
            Assert.Equal(260, result.Value.PixelWidth);
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
                <PackageReference Include="Avalonia" Version="12.0.4" />
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
                <PackageReference Include="Avalonia" Version="12.0.4" />
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
        for (var attempt = 0; attempt < 10; attempt++)
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
            catch (IOException) when (attempt < 9)
            {
                await Task.Delay(100);
            }
            catch (UnauthorizedAccessException) when (attempt < 9)
            {
                await Task.Delay(100);
            }
        }

        if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
        }
    }

    private static async Task<ToolResult<PreviewResponse>?> RunPreviewHostAsync(
        string hostAssembly,
        string requestPath,
        int expectedExitCode)
    {
        using var process = StartPreviewHost(hostAssembly, requestPath);
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(60));

        var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellation.Token);
        var stderrTask = process.StandardError.ReadToEndAsync(cancellation.Token);

        await process.WaitForExitAsync(cancellation.Token);
        var stdout = await stdoutTask;
        var stderr = await stderrTask;

        Assert.True(
            process.ExitCode == expectedExitCode,
            $"Expected exit code {expectedExitCode}, got {process.ExitCode}.{Environment.NewLine}stdout: {stdout}{Environment.NewLine}stderr: {stderr}");
        Assert.True(string.IsNullOrWhiteSpace(stderr), stderr);

        return JsonSerializer.Deserialize<ToolResult<PreviewResponse>>(stdout, JsonOptions);
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
}
