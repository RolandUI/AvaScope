using System.Diagnostics;
using System.Text.Json;
using AvaScope.Protocol;
using SkiaSharp;

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
            Assert.NotNull(result.Error.Details);
            Assert.Equal("build", result.Error.Details!["phase"]);
            Assert.Equal(Path.GetFullPath(projectPath), result.Error.Details["projectPath"]);
            Assert.Equal("1", result.Error.Details["exitCode"]);
            Assert.Contains("Build FAILED", result.Error.Details["outputTail"], StringComparison.Ordinal);
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
                <PackageReference Include="Avalonia" Version="12.0.4" />
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
                <PackageReference Include="Avalonia" Version="12.0.4" />
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
                <PackageReference Include="Avalonia" Version="12.0.4" />
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
                <PackageReference Include="Avalonia" Version="12.0.4" />
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
                <PackageReference Include="Avalonia" Version="12.0.4" />
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
                <PackageReference Include="Avalonia" Version="12.0.4" />
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
                <PackageReference Include="Avalonia" Version="12.0.4" />
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
