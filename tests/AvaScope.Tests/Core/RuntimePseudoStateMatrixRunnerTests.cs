using System.Diagnostics;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using AvaScope.Core;
using AvaScope.Protocol;
using SkiaSharp;

namespace AvaScope.Tests.Core;

public sealed class RuntimePseudoStateMatrixRunnerTests : IDisposable
{
    private static readonly TimeSpan BridgePipeTestTimeout = TimeSpan.FromSeconds(30);
    private readonly string _testRoot = Path.Combine(Path.GetTempPath(), "AvaScope.Tests", Guid.NewGuid().ToString("N"));
    private readonly string _manifestDirectory;

    public RuntimePseudoStateMatrixRunnerTests()
    {
        _manifestDirectory = Path.Combine(_testRoot, "manifests");
        Directory.CreateDirectory(_manifestDirectory);
    }

    [Fact]
    public async Task RunAsyncCapturesStatesContactSheetAndResetMutation()
    {
        var sessionId = SessionId.New();
        var topLevelId = "topLevel:matrix";
        var target = new RuntimeTargetContext(sessionId, topLevelId, TreeKinds.Visual, "visual:target");
        var pipeName = $"avascope-state-core-{Guid.NewGuid():N}";
        WriteManifest("matrix.json", new BridgeSessionManifest(
            sessionId,
            Environment.ProcessId,
            pipeName,
            DateTimeOffset.UtcNow,
            "Pseudo-state matrix app",
            processName: Process.GetCurrentProcess().ProcessName));
        var outputDirectory = Path.Combine(_testRoot, "artifacts");

        var serverTask = RespondToBridgeRequestsAsync(
            pipeName,
            expectedCount: 8,
            (index, request) => index switch
            {
                0 => CreateTreeResponse(request, sessionId, topLevelId),
                1 => CreateScreenshotResponse(request, sessionId, topLevelId, SKColors.White),
                2 => CreateTreeResponse(request, sessionId, topLevelId),
                3 => CreateTreeResponse(request, sessionId, topLevelId),
                4 => CreateMutationResponse(request, sessionId, target, RuntimeMutationStatuses.Applied, applied: true),
                5 => CreateScreenshotResponse(request, sessionId, topLevelId, SKColors.LightGray),
                6 => CreateTreeResponse(request, sessionId, topLevelId, enabled: false),
                7 => CreateMutationResponse(request, sessionId, target, RuntimeMutationStatuses.Applied, applied: true),
                _ => throw new InvalidOperationException("Unexpected pseudo-state matrix bridge request.")
            });
        var request = new RuntimePseudoStateMatrixRequest(
            sessionId,
            topLevelId,
            target,
            [RuntimePseudoStates.Normal, RuntimePseudoStates.Disabled],
            requestId: "matrix-core",
            outputDirectory: outputDirectory,
            contactSheetPath: Path.Combine(outputDirectory, "sheet.png"));

        var result = await new RuntimePseudoStateMatrixRunner()
            .RunAsync(new LocalBridgeClient(_manifestDirectory, BridgePipeTestTimeout), request);
        var bridgeRequests = await serverTask;

        Assert.True(result.Success, result.Error?.Message);
        Assert.Equal("passed", result.Value!.Status);
        Assert.Equal(2, result.Value.Entries.Count);
        Assert.Equal("normal", result.Value.Entries[0].State);
        Assert.Equal("disabled", result.Value.Entries[1].State);
        Assert.NotNull(result.Value.ContactSheetPath);
        Assert.True(File.Exists(result.Value.ContactSheetPath), result.Value.ContactSheetPath);
        Assert.All(result.Value.Entries, entry => Assert.True(File.Exists(entry.Screenshot!.FilePath), entry.Screenshot.FilePath));
        Assert.Single(result.Value.Entries[1].AppliedMutations);
        Assert.Single(result.Value.Entries[1].ResetMutations);
        Assert.Equal(RuntimeMutationOperationKinds.SetProperty, bridgeRequests[4].Mutation!.Operation.Kind);
        Assert.Equal("IsEnabled", bridgeRequests[4].Mutation!.Operation.PropertyName);
        Assert.Equal(RuntimeMutationOperationKinds.ResetMutation, bridgeRequests[7].Mutation!.Operation.Kind);
        Assert.Contains(result.Value.AgentReview.ArtifactPaths, artifact => artifact.Kind == "contact_sheet");
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

    private static BridgeIpcResponse CreateTreeResponse(
        BridgeIpcRequest request,
        SessionId sessionId,
        string topLevelId,
        bool enabled = true)
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
                    "MatrixWindow",
                    bounds: new NodeBounds(0, 0, 160, 100),
                    children:
                    [
                        new TreeNodeSummary(
                            "visual:target",
                            "Avalonia.Controls.ListBoxItem",
                            "StateTarget",
                            automationId: "state-target",
                            text: "State target",
                            bounds: new NodeBounds(20, 20, 100, 32),
                            classes: enabled ? [] : ["disabled"],
                            target: new RuntimeTargetContext(sessionId, topLevelId, TreeKinds.Visual, "visual:target"),
                            accessibilityState: new RuntimeAccessibilityState("bridge", automationName: "State target", isEnabled: enabled))
                    ])));
    }

    private static BridgeIpcResponse CreateMutationResponse(
        BridgeIpcRequest request,
        SessionId sessionId,
        RuntimeTargetContext target,
        string status,
        bool applied)
    {
        Assert.Equal(BridgeIpcMethods.MutateNode, request.Method);
        Assert.NotNull(request.Mutation);

        return BridgeIpcResponse.Ok(
            request.RequestId,
            new RuntimeMutationResponse(
                request.Mutation!.RequestId,
                request.Mutation.Operation.Kind == RuntimeMutationOperationKinds.ResetMutation
                    ? "mutation:reset"
                    : "mutation:state",
                sessionId,
                target.TopLevelId,
                target,
                request.Mutation.Operation,
                status,
                applied,
                DateTimeOffset.UtcNow));
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
                120,
                80,
                DateTimeOffset.UtcNow));
    }

    private static void WriteImage(string path, SKColor color)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using var bitmap = new SKBitmap(120, 80);
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
