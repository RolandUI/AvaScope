using System.Diagnostics;
using System.Text.Json;
using AvaScope.Protocol;

namespace AvaScope.Tests.PreviewHost;

public sealed class PreviewHostSmokeTests
{
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

        await File.WriteAllTextAsync(requestPath, JsonSerializer.Serialize(request, new JsonSerializerOptions(JsonSerializerDefaults.Web)));

        try
        {
            using var process = StartPreviewHost(hostAssembly, requestPath);
            using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(30));

            var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellation.Token);
            var stderrTask = process.StandardError.ReadToEndAsync(cancellation.Token);

            await process.WaitForExitAsync(cancellation.Token);
            var stdout = await stdoutTask;
            var stderr = await stderrTask;

            Assert.Equal(0, process.ExitCode);
            Assert.True(string.IsNullOrWhiteSpace(stderr), stderr);

            var result = JsonSerializer.Deserialize<ToolResult<PreviewResponse>>(
                stdout,
                new JsonSerializerOptions(JsonSerializerDefaults.Web));

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
            if (Directory.Exists(testRoot))
            {
                Directory.Delete(testRoot, recursive: true);
            }
        }
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
