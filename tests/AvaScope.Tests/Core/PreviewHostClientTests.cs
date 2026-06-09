using AvaScope.Core;
using AvaScope.Protocol;

namespace AvaScope.Tests.Core;

public sealed class PreviewHostClientTests : IDisposable
{
    private readonly string _testRoot = Path.Combine(
        Path.GetTempPath(),
        "AvaScope.Tests",
        $"preview-client-{Guid.NewGuid():N}");

    public void Dispose()
    {
        DeleteDirectoryWithRetry(_testRoot);
    }

    private static void DeleteDirectoryWithRetry(string path)
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
                Thread.Sleep(100);
            }
            catch (UnauthorizedAccessException)
            {
                Thread.Sleep(100);
            }
        }
    }

    [Fact]
    public async Task RenderAsyncLaunchesPreviewHostChildProcess()
    {
        Directory.CreateDirectory(_testRoot);

        var hostAssembly = Path.Combine(AppContext.BaseDirectory, "AvaScope.PreviewHost.dll");
        var viewPath = Path.Combine(_testRoot, "SmokeView.axaml");
        var outputPath = Path.Combine(_testRoot, "preview.png");

        await File.WriteAllTextAsync(viewPath, """
            <UserControl xmlns="https://github.com/avaloniaui">
              <Border Background="#FFFFFFFF">
                <TextBlock Text="Core preview client smoke" />
              </Border>
            </UserControl>
            """);

        var client = new PreviewHostClient(hostAssembly);
        var result = await client.RenderAsync(new PreviewRequest(
            outputPath,
            width: 300,
            height: 180,
            dpi: 96,
            viewPath: viewPath,
            themeVariant: "light"));

        Assert.True(result.Success, result.Error?.Message);
        Assert.Equal(Path.GetFullPath(outputPath), result.Value!.FilePath);
        Assert.Equal(300, result.Value.PixelWidth);
        Assert.Equal(180, result.Value.PixelHeight);
        Assert.True(File.Exists(result.Value.FilePath));
        Assert.True(new FileInfo(result.Value.FilePath).Length > 0);
    }

    [Fact]
    public async Task RenderAsyncReturnsStructuredErrorWhenPreviewHostIsMissing()
    {
        var client = new PreviewHostClient(Path.Combine(_testRoot, "missing-host.dll"));

        var result = await client.RenderAsync(new PreviewRequest(
            Path.Combine(_testRoot, "preview.png"),
            width: 100,
            height: 100,
            dpi: 96));

        Assert.False(result.Success);
        Assert.Equal(CoreErrorCodes.PreviewHostUnavailable, result.Error!.Code);
        Assert.Equal("host", result.Error.Details!["phase"]);
        Assert.Equal("host_assembly", result.Error.Details["requirement"]);
        Assert.Equal(Path.GetFullPath(Path.Combine(_testRoot, "missing-host.dll")), result.Error.Details["hostAssemblyPath"]);
        Assert.Contains("AvaScope.PreviewHost.dll", result.Error.Details["nextAction"], StringComparison.Ordinal);
    }

    [Fact]
    public async Task RenderAnimationAsyncCreatesOffsetFramesStripAndMotionSummary()
    {
        Directory.CreateDirectory(_testRoot);

        var hostAssembly = Path.Combine(AppContext.BaseDirectory, "AvaScope.PreviewHost.dll");
        var viewPath = Path.Combine(_testRoot, "AnimationView.axaml");
        var outputPath = Path.Combine(_testRoot, "animation.png");
        var stripPath = Path.Combine(_testRoot, "animation-strip.png");
        var viewerPath = Path.Combine(_testRoot, "animation.html");

        await File.WriteAllTextAsync(viewPath, """
            <UserControl xmlns="https://github.com/avaloniaui">
              <Border Background="#FFFFFFFF">
                <TextBlock Text="Animation frame sample" />
              </Border>
            </UserControl>
            """);

        var client = new PreviewHostClient(hostAssembly);
        var result = await client.RenderAnimationAsync(new PreviewAnimationRequest(
            outputPath,
            [0, 33, 33],
            width: 240,
            height: 120,
            dpi: 96,
            viewPath: viewPath,
            themeVariant: "light",
            frameStripPath: stripPath,
            viewerPath: viewerPath));

        Assert.True(result.Success, result.Error?.Message);
        Assert.Equal(3, result.Value!.Frames.Count);
        Assert.All(result.Value.Frames, frame => Assert.True(frame.Render.Success, frame.Render.Error?.Message));
        Assert.Equal(0, result.Value.Frames[0].Render.Value!.AnimationTimeOffsetMs);
        Assert.Equal(33, result.Value.Frames[1].Render.Value!.AnimationTimeOffsetMs);
        Assert.Equal(33, result.Value.Frames[2].Render.Value!.AnimationTimeOffsetMs);
        Assert.Contains(
            result.Value.Frames[2].Render.Value!.Diagnostics,
            static diagnostic => diagnostic.Code == "animation_frame_reused");
        Assert.All(result.Value.Frames, frame => Assert.True(File.Exists(frame.OutputPath)));
        Assert.Equal(Path.GetFullPath(stripPath), result.Value.FrameStripPath);
        Assert.True(File.Exists(stripPath));
        Assert.NotNull(result.Value.Viewer);
        Assert.Equal(Path.GetFullPath(viewerPath), result.Value.Viewer!.ViewerPath);
        Assert.Equal(new Uri(viewerPath).AbsoluteUri, result.Value.Viewer.PreviewUrl);
        Assert.True(File.Exists(viewerPath));
        var viewerHtml = await File.ReadAllTextAsync(viewerPath);
        Assert.Contains("data:image/png;base64,", viewerHtml);
        Assert.Contains("animation-json", viewerHtml);
        Assert.Equal("static", result.Value.Motion.Status);
        Assert.Contains(result.Value.Diagnostics, static diagnostic => diagnostic.Code == "animation_static_frames");
    }

    [Fact]
    public void GetDiagnosticsReportsAvailablePreviewHost()
    {
        var hostAssembly = Path.Combine(AppContext.BaseDirectory, "AvaScope.PreviewHost.dll");
        var client = new PreviewHostClient(hostAssembly);

        var diagnostics = client.GetDiagnostics();

        Assert.Equal(DiagnosticStatuses.Available, diagnostics.Status);
        Assert.Equal(Path.GetFullPath(hostAssembly), diagnostics.HostAssemblyPath);
        Assert.Equal(DiagnosticProcessModes.IsolatedChildProcess, diagnostics.ProcessMode);
        Assert.Equal("avascope", diagnostics.Service!.ServiceName);
        Assert.Null(diagnostics.Error);
    }

    [Fact]
    public void GetDiagnosticsReportsMissingPreviewHostAsUnavailable()
    {
        var hostAssembly = Path.Combine(_testRoot, "missing-host.dll");
        var client = new PreviewHostClient(hostAssembly);

        var diagnostics = client.GetDiagnostics();

        Assert.Equal(DiagnosticStatuses.Unavailable, diagnostics.Status);
        Assert.Equal(Path.GetFullPath(hostAssembly), diagnostics.HostAssemblyPath);
        Assert.Equal(DiagnosticProcessModes.IsolatedChildProcess, diagnostics.ProcessMode);
        Assert.Null(diagnostics.Service);
        Assert.Equal(CoreErrorCodes.PreviewHostUnavailable, diagnostics.Error!.Code);
        Assert.Equal("host", diagnostics.Error.Details!["phase"]);
        Assert.Equal("host_assembly", diagnostics.Error.Details["requirement"]);
        Assert.Contains("co-located", diagnostics.Error.Details["nextAction"], StringComparison.Ordinal);
    }
}
