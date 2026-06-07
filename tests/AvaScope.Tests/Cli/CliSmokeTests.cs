using System.Diagnostics;
using System.Globalization;
using System.IO.Pipes;
using System.Text;
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
            "visual:root",
            "Window",
            "CliWindow",
            children:
            [
                new TreeNodeSummary("visual:child", "TextBlock", text: "CLI tree")
            ]);

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
            Assert.Equal("Window", payload.Value.Root.NodeType);
            Assert.Equal("CLI tree", Assert.Single(payload.Value.Root.Children).Text);
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
        await using var pipe = new NamedPipeServerStream(
            pipeName,
            PipeDirection.InOut,
            maxNumberOfServerInstances: 1,
            PipeTransmissionMode.Byte,
            PipeOptions.Asynchronous);

        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await pipe.WaitForConnectionAsync(cancellation.Token);

        var requestLine = await ReadLineAsync(pipe, cancellation.Token);
        var request = JsonSerializer.Deserialize<BridgeIpcRequest>(requestLine, JsonOptions)
            ?? throw new InvalidOperationException("Bridge IPC request payload was empty.");
        var responseBytes = Encoding.UTF8.GetBytes(
            JsonSerializer.Serialize(responseFactory(request), JsonOptions) + Environment.NewLine);
        await pipe.WriteAsync(responseBytes, cancellation.Token);
        await pipe.FlushAsync(cancellation.Token);
        return request;
    }

    private static async Task<string> ReadLineAsync(Stream stream, CancellationToken cancellationToken)
    {
        var bytes = new List<byte>();
        var buffer = new byte[1];

        while (true)
        {
            var read = await stream.ReadAsync(buffer, cancellationToken);
            if (read == 0)
            {
                break;
            }

            if (buffer[0] == (byte)'\n')
            {
                break;
            }

            if (buffer[0] != (byte)'\r')
            {
                bytes.Add(buffer[0]);
            }
        }

        var line = Encoding.UTF8.GetString(bytes.ToArray());
        Assert.False(string.IsNullOrWhiteSpace(line));
        return line;
    }

    private sealed record CliResult(
        int ExitCode,
        string StandardOutput,
        string StandardError);
}
