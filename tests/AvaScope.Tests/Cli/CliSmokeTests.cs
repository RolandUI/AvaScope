using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using AvaScope.Core;
using AvaScope.Protocol;

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
                "light");

            Assert.Equal(0, result.ExitCode);
            Assert.True(string.IsNullOrWhiteSpace(result.StandardError), result.StandardError);

            var payload = JsonSerializer.Deserialize<ToolResult<PreviewResponse>>(result.StandardOutput, JsonOptions);
            Assert.NotNull(payload);
            Assert.True(payload.Success, payload.Error?.Message);
            Assert.Equal(Path.GetFullPath(outputPath), payload.Value!.FilePath);
            Assert.Equal(220, payload.Value.PixelWidth);
            Assert.Equal(140, payload.Value.PixelHeight);
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
    public void McpServerAssemblyIsCopiedBesideCli()
    {
        var cliAssembly = Path.Combine(AppContext.BaseDirectory, "avascope.dll");
        var mcpAssembly = Path.Combine(AppContext.BaseDirectory, "AvaScope.Mcp.dll");

        Assert.True(File.Exists(cliAssembly), $"Expected CLI assembly at {cliAssembly}.");
        Assert.True(File.Exists(mcpAssembly), $"Expected MCP assembly at {mcpAssembly}.");
    }

    private static async Task<CliResult> RunCliAsync(string cliAssembly, params string[] arguments)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                WorkingDirectory = AppContext.BaseDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            }
        };

        process.StartInfo.ArgumentList.Add(cliAssembly);
        foreach (var argument in arguments)
        {
            process.StartInfo.ArgumentList.Add(argument);
        }

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

    private sealed record CliResult(
        int ExitCode,
        string StandardOutput,
        string StandardError);
}
