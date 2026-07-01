using System.Diagnostics;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using AvaScope.Core;
using AvaScope.Protocol;
using SkiaSharp;

namespace AvaScope.Tests.Core;

public sealed class RuntimeInteractionAnimationRunnerTests : IDisposable
{
    private static readonly TimeSpan BridgePipeTestTimeout = TimeSpan.FromSeconds(30);
    private readonly string _testRoot = Path.Combine(Path.GetTempPath(), "AvaScope.Tests", Guid.NewGuid().ToString("N"));
    private readonly string _manifestDirectory;

    public RuntimeInteractionAnimationRunnerTests()
    {
        _manifestDirectory = Path.Combine(_testRoot, "manifests");
        Directory.CreateDirectory(_manifestDirectory);
    }

    [Fact]
    public async Task RunAsyncRecordsFramesFrameStripGeometryOverlaysAndAssertionsAfterInput()
    {
        var sessionId = SessionId.New();
        var topLevelId = "topLevel:animation";
        var pipeName = $"avascope-animation-core-{Guid.NewGuid():N}";
        WriteManifest("animation.json", new BridgeSessionManifest(
            sessionId,
            Environment.ProcessId,
            pipeName,
            DateTimeOffset.UtcNow,
            "Interaction animation app",
            processName: Process.GetCurrentProcess().ProcessName));
        var outputDirectory = Path.Combine(_testRoot, "artifacts");
        var frameStripPath = Path.Combine(outputDirectory, "strip.png");

        var serverTask = RespondToBridgeRequestsAsync(
            pipeName,
            expectedCount: 7,
            (index, request) => index switch
            {
                0 => CreateInputResponse(request, sessionId, topLevelId),
                1 => CreateTreeResponse(request, sessionId, topLevelId),
                2 => CreateScreenshotResponse(request, sessionId, topLevelId, SKColors.White),
                3 => CreateTreeResponse(request, sessionId, topLevelId),
                4 => CreateScreenshotResponse(request, sessionId, topLevelId, SKColors.LightGray),
                5 => CreateTreeResponse(request, sessionId, topLevelId),
                6 => CreateScreenshotResponse(request, sessionId, topLevelId, SKColors.White),
                _ => throw new InvalidOperationException("Unexpected interaction animation bridge request.")
            });
        var request = new RuntimeInteractionAnimationRequest(
            sessionId,
            topLevelId,
            [
                new RuntimeInteractionAnimationStep(
                    InputActions.Click,
                    "expand",
                    x: 60,
                    y: 35,
                    frameOffsetsMs: [0, 1, 2])
            ],
            requestId: "animation-core",
            outputDirectory: outputDirectory,
            frameStripPath: frameStripPath,
            assertions:
            [
                new RuntimeInteractionGeometryAssertion(
                    "visual:panel",
                    RuntimeInteractionGeometryMetrics.Width,
                    RuntimeInteractionGeometryAssertionModes.Stable,
                    "panel-width",
                    stepId: "expand",
                    tolerance: 0),
                new RuntimeInteractionGeometryAssertion(
                    "visual:panel",
                    RuntimeInteractionGeometryMetrics.X,
                    RuntimeInteractionGeometryAssertionModes.Equal,
                    "panel-x",
                    stepId: "expand",
                    expectedValue: 20,
                    tolerance: 0)
            ]);

        var result = await new RuntimeInteractionAnimationRunner()
            .RunAsync(new LocalBridgeClient(_manifestDirectory, BridgePipeTestTimeout), request);
        var bridgeRequests = await serverTask;

        Assert.True(result.Success, result.Error?.Message);
        Assert.Equal("passed", result.Value!.Status);
        Assert.Equal(3, result.Value.Steps[0].Frames.Count);
        Assert.All(result.Value.Steps[0].Frames, frame =>
        {
            Assert.Equal("expand", frame.StepId);
            Assert.True(File.Exists(frame.Screenshot!.FilePath), frame.Screenshot.FilePath);
            Assert.True(File.Exists(frame.GeometryOverlayPath), frame.GeometryOverlayPath);
            Assert.Equal("visual:panel", Assert.Single(frame.Geometry).NodeId);
        });
        Assert.All(result.Value.Assertions, assertion => Assert.Equal("passed", assertion.Status));
        Assert.Equal(Path.GetFullPath(frameStripPath), result.Value.FrameStripPath);
        Assert.True(File.Exists(result.Value.FrameStripPath), result.Value.FrameStripPath);
        Assert.Contains(result.Value.AgentReview.ArtifactPaths, artifact => artifact.Kind == "frame_strip");
        Assert.Equal(BridgeIpcMethods.Input, bridgeRequests[0].Method);
        Assert.Equal(InputActions.Click, bridgeRequests[0].Action);
        Assert.Equal(BridgeIpcMethods.VisualTree, bridgeRequests[1].Method);
        Assert.Equal(BridgeIpcMethods.Screenshot, bridgeRequests[2].Method);
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

    private static BridgeIpcResponse CreateInputResponse(
        BridgeIpcRequest request,
        SessionId sessionId,
        string topLevelId)
    {
        Assert.Equal(BridgeIpcMethods.Input, request.Method);
        Assert.Equal(topLevelId, request.TopLevelId);
        Assert.Equal(InputActions.Click, request.Action);
        Assert.Equal(60, request.X);
        Assert.Equal(35, request.Y);

        return BridgeIpcResponse.Ok(
            request.RequestId,
            new InputResponse(
                sessionId,
                topLevelId,
                InputActions.Click,
                handled: true,
                DateTimeOffset.UtcNow,
                "visual:button"));
    }

    private static BridgeIpcResponse CreateTreeResponse(
        BridgeIpcRequest request,
        SessionId sessionId,
        string topLevelId)
    {
        Assert.Equal(BridgeIpcMethods.VisualTree, request.Method);
        Assert.Equal(topLevelId, request.TopLevelId);

        return BridgeIpcResponse.Ok(
            request.RequestId,
            new TreeResponse(
                sessionId,
                topLevelId,
                TreeKinds.Visual,
                request.MaxDepth ?? 16,
                new TreeNodeSummary(
                    "visual:root",
                    "Avalonia.Controls.Window",
                    "AnimationWindow",
                    bounds: new NodeBounds(0, 0, 200, 120),
                    children:
                    [
                        new TreeNodeSummary(
                            "visual:panel",
                            "Avalonia.Controls.Border",
                            "AnimatedPanel",
                            automationId: "animated-panel",
                            bounds: new NodeBounds(20, 10, 120, 50))
                    ])));
    }

    private static BridgeIpcResponse CreateScreenshotResponse(
        BridgeIpcRequest request,
        SessionId sessionId,
        string topLevelId,
        SKColor color)
    {
        Assert.Equal(BridgeIpcMethods.Screenshot, request.Method);
        Assert.Equal(topLevelId, request.TopLevelId);
        Assert.NotNull(request.OutputPath);
        WriteImage(request.OutputPath!, color);

        return BridgeIpcResponse.Ok(
            request.RequestId,
            new ScreenshotResponse(
                sessionId,
                topLevelId,
                request.OutputPath!,
                160,
                90,
                DateTimeOffset.UtcNow));
    }

    private static void WriteImage(string path, SKColor color)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using var bitmap = new SKBitmap(160, 90);
        bitmap.Erase(color);
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
