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
            "Find me");

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
            var match = Assert.Single(payload.Value.Matches);
            Assert.Equal("logical:match", match.Node.NodeId);
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
            return BridgeIpcResponse.Ok(
                request.RequestId,
                new InputResponse(sessionId, request.TopLevelId!, InputActions.KeyText, true, DateTimeOffset.UtcNow, "visual:textbox"));
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
                "typed text");
            var request = await serverTask;

            Assert.Equal(0, result.ExitCode);
            Assert.Equal("typed text", request.InputText);

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

    private static async Task<CliResult> RunCliAsyncFromDirectory(
        string workingDirectory,
        string cliAssembly,
        params string[] arguments)
    {
        using var process = new Process
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
