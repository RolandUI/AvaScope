using System.Diagnostics;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using AvaScope.Core;
using AvaScope.Protocol;
using SkiaSharp;

namespace AvaScope.Tests.Core;

public sealed class RuntimePointerDiagnosticsRunnerTests : IDisposable
{
    private static readonly TimeSpan BridgePipeTestTimeout = TimeSpan.FromSeconds(30);
    private readonly string _testRoot = Path.Combine(Path.GetTempPath(), "AvaScope.Tests", Guid.NewGuid().ToString("N"));
    private readonly string _manifestDirectory;

    public RuntimePointerDiagnosticsRunnerTests()
    {
        _manifestDirectory = Path.Combine(_testRoot, "manifests");
        Directory.CreateDirectory(_manifestDirectory);
    }

    [Fact]
    public async Task RunAsyncReportsPopupLayerParentHoverExitAndScreenshotOverlay()
    {
        var sessionId = SessionId.New();
        var pipeName = $"avascope-pointer-core-{Guid.NewGuid():N}";
        WriteManifest("pointer.json", new BridgeSessionManifest(
            sessionId,
            Environment.ProcessId,
            pipeName,
            DateTimeOffset.UtcNow,
            "Pointer diagnostics app",
            processName: Process.GetCurrentProcess().ProcessName));
        var outputDirectory = Path.Combine(_testRoot, "artifacts");
        var serverTask = RespondToBridgeRequestsAsync(
            pipeName,
            expectedCount: 12,
            (index, request) => index switch
            {
                0 => CreateInputResponse(request, sessionId, "topLevel:main", x: 10, y: 10),
                1 => CreateTopLevelsResponse(request),
                2 => CreateTreeResponse(request, sessionId, "topLevel:main", popup: false),
                3 => CreateTreeResponse(request, sessionId, "topLevel:popup", popup: true),
                4 => CreateInputResponse(request, sessionId, "topLevel:main", x: 65, y: 10),
                5 => CreateTopLevelsResponse(request),
                6 => CreateTreeResponse(request, sessionId, "topLevel:main", popup: false),
                7 => CreateTreeResponse(request, sessionId, "topLevel:popup", popup: true),
                8 => CreateTopLevelsResponse(request),
                9 => CreateTreeResponse(request, sessionId, "topLevel:main", popup: false),
                10 => CreateTreeResponse(request, sessionId, "topLevel:popup", popup: true),
                11 => CreateScreenshotResponse(request, sessionId, "topLevel:popup"),
                _ => throw new InvalidOperationException("Unexpected pointer diagnostics bridge request.")
            });
        var request = new RuntimePointerDiagnosticsRequest(
            sessionId,
            "topLevel:main",
            [
                new RuntimePointerPathStep(RuntimePointerPathActions.Move, "move-parent", x: 10, y: 10),
                new RuntimePointerPathStep(RuntimePointerPathActions.Move, "move-popup", x: 65, y: 10),
                new RuntimePointerPathStep(RuntimePointerPathActions.Screenshot, "capture-popup")
            ],
            requestId: "pointer-core",
            outputDirectory: outputDirectory,
            parentHoverNodeId: "visual:hover");

        var result = await new RuntimePointerDiagnosticsRunner()
            .RunAsync(new LocalBridgeClient(_manifestDirectory, BridgePipeTestTimeout), request);
        var bridgeRequests = await serverTask;

        Assert.True(result.Success, result.Error?.Message);
        Assert.Equal("passed", result.Value!.Status);
        Assert.Equal(3, result.Value.Steps.Count);
        Assert.Equal(BridgeIpcMethods.Input, bridgeRequests[0].Method);
        Assert.Equal(BridgeIpcMethods.ListTopLevels, bridgeRequests[1].Method);
        Assert.Equal(BridgeIpcMethods.Screenshot, bridgeRequests[11].Method);
        Assert.Equal("topLevel:popup", bridgeRequests[11].TopLevelId);

        var popupStep = result.Value.Steps[1];
        Assert.Equal("popup", popupStep.ActiveLayer!.LayerKind);
        Assert.Equal("visual:popupItem", popupStep.ActiveLayer.HitTestPath.Last().NodeId);
        var hoverExit = Assert.Single(popupStep.Transitions, transition => transition.Code == "pointer_parent_hover_exited_into_popup_layer");
        Assert.True(hoverExit.ParentHoverRegionExited);
        Assert.Equal("bounds_snapshot_inference", hoverExit.Provenance);

        var screenshotStep = result.Value.Steps[2];
        Assert.NotNull(screenshotStep.Screenshot);
        Assert.True(File.Exists(screenshotStep.Screenshot!.FilePath), screenshotStep.Screenshot.FilePath);
        Assert.NotNull(screenshotStep.PointerOverlayPath);
        Assert.True(File.Exists(screenshotStep.PointerOverlayPath), screenshotStep.PointerOverlayPath);
        Assert.Contains(result.Value.AgentReview.ArtifactPaths, artifact => artifact.Kind == "pointer_overlay");
    }

    public void Dispose()
    {
        if (Directory.Exists(_testRoot))
        {
            Directory.Delete(_testRoot, recursive: true);
        }
    }

    private string WriteManifest(string fileName, BridgeSessionManifest manifest)
    {
        var path = Path.Combine(_manifestDirectory, fileName);
        File.WriteAllText(path, JsonSerializer.Serialize(manifest), Encoding.UTF8);
        return path;
    }

    private static BridgeIpcResponse CreateInputResponse(BridgeIpcRequest request, SessionId sessionId, string topLevelId, double x, double y)
    {
        Assert.Equal(BridgeIpcMethods.Input, request.Method);
        Assert.Equal(InputActions.PointerMove, request.Action);
        Assert.Equal(x, request.X);
        Assert.Equal(y, request.Y);

        return BridgeIpcResponse.Ok(
            request.RequestId,
            new InputResponse(
                sessionId,
                topLevelId,
                InputActions.PointerMove,
                handled: true,
                DateTimeOffset.UtcNow,
                "visual:pointerTarget"));
    }

    private static BridgeIpcResponse CreateTopLevelsResponse(BridgeIpcRequest request)
    {
        Assert.Equal(BridgeIpcMethods.ListTopLevels, request.Method);
        return BridgeIpcResponse.Ok(
            request.RequestId,
            new[]
            {
                new TopLevelSummary("topLevel:main", "window", "Main", 100, 100, 1, isActive: true),
                new TopLevelSummary("topLevel:popup", "topLevel", "PopupRoot", 100, 60, 1, isActive: false)
            });
    }

    private static BridgeIpcResponse CreateTreeResponse(BridgeIpcRequest request, SessionId sessionId, string topLevelId, bool popup)
    {
        Assert.Equal(BridgeIpcMethods.VisualTree, request.Method);
        Assert.Equal(topLevelId, request.TopLevelId);

        var tree = popup
            ? new TreeResponse(
                sessionId,
                topLevelId,
                TreeKinds.Visual,
                request.MaxDepth ?? 16,
                new TreeNodeSummary(
                    "visual:popupRoot",
                    "Avalonia.Controls.Primitives.PopupRoot",
                    "PopupRoot",
                    bounds: new NodeBounds(60, 0, 50, 50),
                    children:
                    [
                        new TreeNodeSummary(
                            "visual:popupItem",
                            "Avalonia.Controls.Button",
                            "PopupItem",
                            text: "Popup action",
                            bounds: new NodeBounds(60, 0, 50, 30))
                    ]))
            : new TreeResponse(
                sessionId,
                topLevelId,
                TreeKinds.Visual,
                request.MaxDepth ?? 16,
                new TreeNodeSummary(
                    "visual:root",
                    "Avalonia.Controls.Window",
                    "MainWindow",
                    bounds: new NodeBounds(0, 0, 100, 100),
                    children:
                    [
                        new TreeNodeSummary(
                            "visual:hover",
                            "Avalonia.Controls.Border",
                            "HoverPanel",
                            automationId: "hover-panel",
                            bounds: new NodeBounds(0, 0, 50, 50))
                    ]));

        return BridgeIpcResponse.Ok(request.RequestId, tree);
    }

    private static BridgeIpcResponse CreateScreenshotResponse(BridgeIpcRequest request, SessionId sessionId, string topLevelId)
    {
        Assert.Equal(BridgeIpcMethods.Screenshot, request.Method);
        Assert.Equal(topLevelId, request.TopLevelId);
        Assert.NotNull(request.OutputPath);
        WriteImage(request.OutputPath!);

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

    private static void WriteImage(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using var bitmap = new SKBitmap(100, 60);
        bitmap.Erase(SKColors.White);
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        using var stream = File.Create(path);
        data.SaveTo(stream);
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
                var requestLine = await ReadLineAsync(pipe, cancellation.Token);
                if (string.IsNullOrWhiteSpace(requestLine))
                {
                    continue;
                }

                var request = JsonSerializer.Deserialize<BridgeIpcRequest>(requestLine);
                if (request is null)
                {
                    continue;
                }

                var index = requests.Count;
                requests.Add(request);
                var responseBytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(responseFactory(index, request)) + Environment.NewLine);
                await pipe.WriteAsync(responseBytes, cancellation.Token);
                await pipe.FlushAsync(cancellation.Token);
            }

            return requests;
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            throw new TimeoutException($"Timed out waiting for {expectedCount} bridge IPC requests on pipe '{pipeName}'.");
        }
    }

    private static async Task<string?> ReadLineAsync(Stream stream, CancellationToken cancellationToken)
    {
        var buffer = new List<byte>();
        var one = new byte[1];
        while (true)
        {
            var read = await stream.ReadAsync(one, cancellationToken);
            if (read == 0)
            {
                break;
            }

            if (one[0] == (byte)'\n')
            {
                break;
            }

            if (one[0] != (byte)'\r')
            {
                buffer.Add(one[0]);
            }
        }

        return buffer.Count == 0 ? null : Encoding.UTF8.GetString(buffer.ToArray());
    }
}
