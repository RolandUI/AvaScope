using System.Diagnostics;
using System.IO.Pipes;
using System.Runtime.ExceptionServices;
using System.Text;
using System.Text.Json;
using AvaScope.Core;
using AvaScope.Protocol;
using SkiaSharp;
using Xunit.Abstractions;

namespace AvaScope.Tests.Core;

public sealed class RuntimePointerDiagnosticsRunnerTests : IDisposable
{
    private static readonly TimeSpan BridgePipeTestTimeout = TimeSpan.FromSeconds(30);
    private readonly string _testRoot = Path.Combine(Path.GetTempPath(), "AvaScope.Tests", Guid.NewGuid().ToString("N"));
    private readonly string _manifestDirectory;
    private readonly CancellationTokenSource _fixtureCancellation = new();
    private readonly Stopwatch _fixtureElapsed = Stopwatch.StartNew();
    private readonly List<FixtureEvent> _events = [];
    private readonly ITestOutputHelper _output;

    public RuntimePointerDiagnosticsRunnerTests(ITestOutputHelper output)
    {
        _output = output;
        _manifestDirectory = Path.Combine(_testRoot, "manifests");
        Directory.CreateDirectory(_manifestDirectory);
    }

    [Fact]
    public async Task RunAsyncReportsPopupLayerParentHoverExitAndScreenshotOverlay()
    {
        var sessionId = SessionId.New();
        var pipeName = TestPipeNames.New();
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
            expectedCount: 35,
            (index, request) => index switch
            {
                0 => CreateInputResponse(request, sessionId, "topLevel:main", x: 10, y: 10),
                1 => CreateTopLevelsResponse(request),
                2 => CreateTreeResponse(request, sessionId, "topLevel:main", popup: false),
                3 or 4 or 8 => CreatePickResponse(request, sessionId, "topLevel:main", 10, 10),
                5 => CreateTreeResponse(request, sessionId, "topLevel:popup", popup: true),
                6 or 7 => CreatePickResponse(request, sessionId, "topLevel:popup", -100, 20),
                9 => CreateInputResponse(request, sessionId, "topLevel:main", x: 65, y: 10),
                10 or 18 or 27 => CreateTopLevelsResponse(request),
                11 or 19 or 28 => CreateTreeResponse(request, sessionId, "topLevel:main", popup: false),
                12 or 13 or 17 or 20 or 21 or 25 or 29 or 30 or 34 => CreatePickResponse(request, sessionId, "topLevel:main", 65, 10),
                14 or 22 or 31 => CreateTreeResponse(request, sessionId, "topLevel:popup", popup: true),
                15 or 16 or 23 or 24 or 32 or 33 => CreatePickResponse(request, sessionId, "topLevel:popup", 10, 20),
                26 => CreateScreenshotResponse(request, sessionId, "topLevel:popup"),
                _ => throw new InvalidOperationException("Unexpected pointer diagnostics bridge request.")
            });
        var request = new RuntimePointerDiagnosticsRequest(
            sessionId,
            "topLevel:main",
            [
                new RuntimePointerPathStep(RuntimePointerPathActions.Move, "move-parent", x: 10, y: 10),
                new RuntimePointerPathStep(RuntimePointerPathActions.Move, "move-popup", x: 65, y: 10),
                new RuntimePointerPathStep(RuntimePointerPathActions.Screenshot, "capture-popup"),
                new RuntimePointerPathStep(RuntimePointerPathActions.AssertHit, expectedNodeId: "visual:popupItem")
            ],
            requestId: "pointer-core",
            outputDirectory: outputDirectory,
            parentHoverNodeId: "visual:hover");

        var (result, bridgeRequests) = await RunWithServerAsync(serverTask,
            new LocalBridgeClient(_manifestDirectory, BridgePipeTestTimeout), request);

        Assert.True(result.Success, result.Error?.Message);
        Assert.Equal("failed", result.Value!.Status);
        Assert.Equal(4, result.Value.Steps.Count);
        Assert.Contains(result.Value.Steps[3].Diagnostics, error => error.Code == "runtime_pointer_hit_unverified");
        Assert.Equal(BridgeIpcMethods.Input, bridgeRequests[0].Method);
        Assert.Equal(BridgeIpcMethods.ListTopLevels, bridgeRequests[1].Method);
        Assert.Equal(BridgeIpcMethods.Screenshot, bridgeRequests[26].Method);
        Assert.Equal("topLevel:popup", bridgeRequests[26].TopLevelId);

        var popupStep = result.Value.Steps[1];
        Assert.Equal("popup", popupStep.ActiveLayer!.LayerKind);
        Assert.Equal("visual:popupItem", popupStep.ActiveLayer.HitTestPath.Last().NodeId);
        var hoverExit = Assert.Single(popupStep.Transitions, transition => transition.Code == "pointer_parent_hover_exited_into_popup_layer");
        Assert.True(hoverExit.ParentHoverRegionExited);
        Assert.Equal("avalonia_hit_test_snapshot_inference", hoverExit.Provenance);
        Assert.Equal(new RuntimePointerLocation(10, 20), popupStep.ActiveLayer.Pointer);
        Assert.Equal("ambiguous", popupStep.ActiveLayer.Metadata["layerSelection"]);
        Assert.Contains(popupStep.Diagnostics, error => error.Code == "runtime_pointer_layer_order_unverified");

        var screenshotStep = result.Value.Steps[2];
        Assert.NotNull(screenshotStep.Screenshot);
        Assert.True(File.Exists(screenshotStep.Screenshot!.FilePath), screenshotStep.Screenshot.FilePath);
        Assert.NotNull(screenshotStep.PointerOverlayPath);
        Assert.True(File.Exists(screenshotStep.PointerOverlayPath), screenshotStep.PointerOverlayPath);
        using var overlay = SKBitmap.Decode(screenshotStep.PointerOverlayPath);
        Assert.NotEqual(SKColors.White, overlay.GetPixel(10, 20));
        Assert.Equal(SKColors.White, overlay.GetPixel(65, 20));
        Assert.Contains(result.Value.AgentReview.ArtifactPaths, artifact => artifact.Kind == "pointer_overlay");
    }

    [Fact]
    public async Task RunAsyncPreservesActualInputTargetAndRuntimeHitPathMismatchWithEffectiveCoordinates()
    {
        var sessionId = SessionId.New();
        var topLevelId = "topLevel:main";
        var pipeName = TestPipeNames.New();
        WriteManifest("pointer-mismatch.json", new BridgeSessionManifest(
            sessionId,
            Environment.ProcessId,
            pipeName,
            DateTimeOffset.UtcNow,
            "Pointer diagnostics app",
            processName: Process.GetCurrentProcess().ProcessName));

        var serverTask = RespondToBridgeRequestsAsync(
            pipeName,
            expectedCount: 4,
            (index, request) => index switch
            {
                0 => CreateInputResponse(
                    request,
                    sessionId,
                    topLevelId,
                    x: 88,
                    y: 50,
                    targetNodeId: "visual:button",
                    metadata: new Dictionary<string, string>
                    {
                        ["coordinateSpace"] = "top_level_dip",
                        ["hitTestSource"] = "TopLevel.GetVisualAt",
                        ["effectiveX"] = "88",
                        ["effectiveY"] = "50",
                        ["hitVisualNodeId"] = "visual:button",
                        ["inputTargetNodeType"] = "Avalonia.Controls.Button"
                    }),
                1 => CreateMismatchedTreeResponse(request, sessionId, topLevelId),
                2 or 3 => CreatePickResponse(request, sessionId, topLevelId, 88, 50, "visual:panel"),
                _ => throw new InvalidOperationException("Unexpected pointer diagnostics bridge request.")
            });
        var request = new RuntimePointerDiagnosticsRequest(
            sessionId,
            topLevelId,
            [new RuntimePointerPathStep(RuntimePointerPathActions.Move, "move-nested", x: 88, y: 50)],
            requestId: "pointer-mismatch",
            includeAllTopLevels: false);

        var (result, _) = await RunWithServerAsync(serverTask,
            new LocalBridgeClient(_manifestDirectory, BridgePipeTestTimeout), request);

        Assert.True(result.Success, result.Error?.Message);
        Assert.Equal("passed", result.Value!.Status);
        var diagnostic = Assert.Single(
            result.Value.Diagnostics,
            diagnostic => diagnostic.Code == "runtime_pointer_input_hit_path_mismatch");
        Assert.Equal("visual:button", diagnostic.Details!["inputTargetNodeId"]);
        Assert.Equal("visual:panel", diagnostic.Details["hitPathLeafNodeId"]);
        Assert.Equal("88", diagnostic.Details["effectiveX"]);
        Assert.Equal("50", diagnostic.Details["effectiveY"]);
        Assert.Equal("top_level_dip", diagnostic.Details["coordinateSpace"]);
        Assert.Contains("bounds", diagnostic.Details["nextAction"], StringComparison.OrdinalIgnoreCase);
        Assert.Equal("88", result.Value.Steps[0].Metadata["effectiveX"]);
        Assert.Equal("visual:button", result.Value.Steps[0].Metadata["inputTargetNodeId"]);
        Assert.Equal("input_target_not_in_runtime_hit_path", diagnostic.Details["mismatchKind"]);
    }

    [Fact]
    public async Task PointerOverlayMapsSelectedRootDipToRenderedPixels()
    {
        var sessionId = SessionId.New(); var pipeName = TestPipeNames.New();
        WriteManifest("pointer-scale.json", new(sessionId, Environment.ProcessId, pipeName, DateTimeOffset.UtcNow,
            "Scaled app", processName: Process.GetCurrentProcess().ProcessName));
        var server = RespondToBridgeRequestsAsync(pipeName, 5, (index, request) => index switch
        {
            0 => CreateInputResponse(request, sessionId, "topLevel:main", 10, 10, "visual:hover"),
            1 => CreateTreeResponse(request, sessionId, "topLevel:main", false),
            2 or 3 => CreatePickResponse(request, sessionId, "topLevel:main", 10, 10),
            4 => CreateScreenshotResponse(request, sessionId, "topLevel:main", 200, 200),
            _ => throw new InvalidOperationException()
        });
        var (result, _) = await RunWithServerAsync(server, new(_manifestDirectory, BridgePipeTestTimeout),
            new(sessionId, "topLevel:main", [new("move", x: 10, y: 10)], outputDirectory: _testRoot,
                includeAllTopLevels: false, captureScreenshots: true));
        Assert.True(result.Success, result.Error?.Message); Assert.Equal("passed", result.Value!.Status);
        using var bitmap = SKBitmap.Decode(Assert.Single(result.Value.Steps).PointerOverlayPath);
        Assert.NotEqual(SKColors.White, bitmap.GetPixel(20, 20));
        Assert.Equal(SKColors.White, bitmap.GetPixel(10, 10));
    }

    [Theory]
    [InlineData("partial", 7)]
    [InlineData("unsupported", 5)]
    [InlineData("geometry_changed", 7)]
    [InlineData("missing_generation", 3)]
    public async Task IncompleteRuntimeEvidenceCannotTurnBoundsIntoAPassingHit(string kind, int expectedCount)
    {
        var sessionId = SessionId.New(); var pipeName = TestPipeNames.New();
        WriteManifest("pointer-coverage.json", new(sessionId, Environment.ProcessId, pipeName, DateTimeOffset.UtcNow,
            "Coverage app", processName: Process.GetCurrentProcess().ProcessName));
        var server = RespondToBridgeRequestsAsync(pipeName, expectedCount, (_, request) =>
        {
            if (request.Method == BridgeIpcMethods.Input)
                return CreateInputResponse(request, sessionId, "topLevel:main", 10, 10, "visual:hover");
            if (request.Method == BridgeIpcMethods.VisualTree)
            {
                var tree = CreateTreeResponse(request, sessionId, "topLevel:main", false).GetValue<TreeResponse>()!;
                return kind == "missing_generation"
                    ? BridgeIpcResponse.Ok(request.RequestId, new TreeResponse(sessionId, tree.TopLevelId, tree.TreeKind, tree.DepthLimit, tree.Root))
                    : BridgeIpcResponse.Ok(request.RequestId, tree);
            }
            Assert.Equal(BridgeIpcMethods.PickNode, request.Method);
            if (kind == "unsupported") return BridgeIpcResponse.Fail(request.RequestId, new("bridge_method_not_supported", "Picking unavailable."));
            if (kind == "geometry_changed" && request.Pick!.X is not null)
                return BridgeIpcResponse.Fail(request.RequestId, new("pick_geometry_changed", "Observed geometry changed."));
            return CreatePickResponse(request, sessionId, "topLevel:main", 10, 10, truncated: kind == "partial");
        });
        var (result, _) = await RunWithServerAsync(server, new(_manifestDirectory, BridgePipeTestTimeout),
            new(sessionId, "topLevel:main", [new("move", x: 10, y: 10), new("assert_hit", expectedNodeId: "visual:hover")], includeAllTopLevels: false));
        Assert.True(result.Success, result.Error?.Message);
        Assert.Equal("failed", result.Value!.Status);
        var last = result.Value.Steps.Last();
        Assert.Contains(last.Diagnostics, error => error.Code == "runtime_pointer_hit_unverified");
        Assert.DoesNotContain(result.Value.Diagnostics, error => error.Code == "runtime_pointer_input_hit_path_mismatch");
        Assert.Equal(kind == "partial" ? "partial" : "unavailable", last.ActiveLayer!.Metadata["hitPathCoverage"]);
        if (kind == "partial") Assert.Contains(last.Diagnostics, error => error.Code == "runtime_pointer_hit_path_partial");
        else Assert.Empty(last.ActiveLayer.HitTestPath);
        Assert.NotNull(last.ActiveLayer.NearestNode);
    }

    [Theory]
    [InlineData("missing_mapping", 15, "runtime_pointer_coordinates_unavailable")]
    [InlineData("primary_moved", 17, "runtime_pointer_primary_geometry_changed")]
    public async Task CrossRootAssertionsRefuseMissingOrChangedPrimaryCoordinates(string kind, int expectedCount, string diagnostic)
    {
        var sessionId = SessionId.New(); var pipeName = TestPipeNames.New(); var primaryGeometryReads = 0;
        WriteManifest("pointer-mapping.json", new(sessionId, Environment.ProcessId, pipeName, DateTimeOffset.UtcNow,
            "Mapping app", processName: Process.GetCurrentProcess().ProcessName));
        var server = RespondToBridgeRequestsAsync(pipeName, expectedCount, (_, request) =>
        {
            if (request.Method == BridgeIpcMethods.Input) return CreateInputResponse(request, sessionId, "topLevel:main", 65, 10, "visual:root");
            if (request.Method == BridgeIpcMethods.ListTopLevels) return CreateTopLevelsResponse(request);
            if (request.Method == BridgeIpcMethods.VisualTree) return CreateTreeResponse(request, sessionId, request.TopLevelId!, request.TopLevelId == "topLevel:popup");
            var popup = request.Pick!.Target.TopLevelId == "topLevel:popup";
            var response = CreatePickResponse(request, sessionId, request.Pick.Target.TopLevelId, popup ? 10 : 65, popup ? 20 : 10).GetValue<RuntimePickResponse>()!;
            if (request.Pick.X is null)
            {
                if (popup && kind == "missing_mapping") response = response with { Geometry = response.Geometry with { DesktopBounds = null, DesktopUnits = null } };
                if (!popup && ++primaryGeometryReads % 2 == 0 && kind == "primary_moved")
                    response = response with { Geometry = response.Geometry with { Revision = new string('b', 64) } };
            }
            return BridgeIpcResponse.Ok(request.RequestId, response);
        });
        var (result, _) = await RunWithServerAsync(server, new(_manifestDirectory, BridgePipeTestTimeout),
            new(sessionId, "topLevel:main", [new("move", x: 65, y: 10), new("assert_hit", expectedNodeId: "visual:hover")]));
        Assert.True(result.Success, result.Error?.Message); Assert.Equal("failed", result.Value!.Status);
        Assert.Contains(result.Value.Steps.Last().Diagnostics, error => error.Code == diagnostic);
        Assert.Contains(result.Value.Steps.Last().Diagnostics, error => error.Code == "runtime_pointer_hit_unverified");
        Assert.Equal("topLevel:main", result.Value.Steps.Last().ActiveLayer!.TopLevelId);
        Assert.Equal("incomplete", result.Value.Steps.Last().ActiveLayer!.Metadata["layerSelection"]);
    }

    public void Dispose()
    {
        _fixtureCancellation.Cancel();
        _fixtureCancellation.Dispose();
        if (Directory.Exists(_testRoot))
        {
            Directory.Delete(_testRoot, recursive: true);
        }
    }

    [Fact]
    public async Task GeometryFailureSequenceCancelsTheFixtureAndPreservesItsFirstAssertion()
    {
        var sessionId = SessionId.New();
        var pipeName = TestPipeNames.New();
        WriteManifest("controlled-pointer.json", new(sessionId, Environment.ProcessId, pipeName,
            DateTimeOffset.UtcNow, "Controlled pointer fixture", processName: Process.GetCurrentProcess().ProcessName));
        var methods = new List<string>();
        var server = RespondToBridgeRequestsAsync(pipeName, 5, (index, request) =>
        {
            methods.Add(request.Method);
            return index switch
            {
                0 => CreateInputResponse(request, sessionId, "topLevel:main", 10, 10),
                1 => CreateTopLevelsResponse(request),
                2 => CreateTreeResponse(request, sessionId, "topLevel:main", false),
                3 => BridgeIpcResponse.Fail(request.RequestId, new("bridge_ipc_unavailable", "Controlled geometry failure.")),
                4 => CreatePickResponse(request, sessionId, "topLevel:main", 10, 10),
                _ => throw new InvalidOperationException("Unexpected controlled request.")
            };
        });
        var run = RunWithServerAsync(server, new(_manifestDirectory, BridgePipeTestTimeout),
            new(sessionId, "topLevel:main", [new("move", x: 10, y: 10)]));
        try
        {
            var original = await Record.ExceptionAsync(() => server);
            Assert.NotNull(original);
            Assert.Equal(["input", "list_top_levels", "visual_tree", "pick_node", "visual_tree"], methods);
            Assert.True(_fixtureCancellation.IsCancellationRequested,
                "A failed response assertion must cancel the fixture before subsequent client requests can wait.");
            Assert.Same(original, await Record.ExceptionAsync(() => run));
            Assert.Contains(_events, item => item.Index == 3 && item.Phase == "response" && item.Outcome == "bridge_ipc_unavailable");
            Assert.Contains(_events, item => item.Index == 4 && item.Phase == "request" && item.Method == "visual_tree");
            Assert.Contains(_events, item => item.Index == 4 && item.Phase == "server_failed" && item.ErrorType == original.GetType().Name);
        }
        finally
        {
            _fixtureCancellation.Cancel();
            await Record.ExceptionAsync(() => run);
        }
    }

    [UnixPipeFact]
    public Task LegacyListenerDisposalDropsAnAlreadyQueuedPointerFixtureRequest() =>
        AssertQueuedFixtureConnectionAsync(preserveListener: false);

    [UnixPipeFact]
    public Task PointerFixtureRetainsAnAlreadyQueuedRequestBetweenResponses() =>
        AssertQueuedFixtureConnectionAsync(preserveListener: true);

    private async Task AssertQueuedFixtureConnectionAsync(bool preserveListener)
    {
        var pipeName = TestPipeNames.New();
        var firstFlushed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var continueServer = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var deadline = new CancellationTokenSource(BridgePipeTestTimeout);
        var server = RespondToBridgeRequestsAsync(pipeName, 2, (_, request) => CreateTopLevelsResponse(request),
            async (index, cancellationToken) =>
            {
                if (index != 0) return;
                firstFlushed.SetResult();
                await continueServer.Task.WaitAsync(cancellationToken);
            }, preserveListener);
        try
        {
            await using var first = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
            await first.ConnectAsync(deadline.Token);
            await first.WriteAsync(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(
                new BridgeIpcRequest("first", BridgeIpcMethods.ListTopLevels)) + "\n"), deadline.Token);
            Assert.NotNull(await ReadLineAsync(first, deadline.Token));
            await firstFlushed.Task.WaitAsync(deadline.Token);

            // Connect and write once while the server is explicitly held at its previous reply.
            // On Unix this enters the listening socket's backlog; no sleep or retry is involved.
            await using var queued = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
            await queued.ConnectAsync(deadline.Token);
            await queued.WriteAsync(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(
                new BridgeIpcRequest("queued", BridgeIpcMethods.ListTopLevels)) + "\n"), deadline.Token);
            continueServer.SetResult();
            string? line = null;
            var failure = await Record.ExceptionAsync(async () => line = await ReadLineAsync(queued, deadline.Token));
            Trace("queued_result", failure: failure, outcome: string.IsNullOrWhiteSpace(line) ? "no_response" : "response");
            if (preserveListener)
            {
                Assert.Null(failure);
                Assert.False(string.IsNullOrWhiteSpace(line), "The pointer fixture dropped an already queued request.");
                var response = JsonSerializer.Deserialize<BridgeIpcResponse>(line!);
                Assert.Equal("queued", response!.RequestId);
                Assert.True(response.Success);
                Assert.Equal(2, (await server.WaitAsync(deadline.Token)).Count);
            }
            else
            {
                Assert.True(failure is IOException || failure is null && string.IsNullOrWhiteSpace(line),
                    "The legacy negative control must lose the queued response, without a deadline or a retry.");
                Assert.False(deadline.IsCancellationRequested);
            }
        }
        finally
        {
            continueServer.TrySetResult();
            _fixtureCancellation.Cancel();
            await Record.ExceptionAsync(() => server.WaitAsync(TimeSpan.FromSeconds(3)));
        }
    }

    private async Task<(CoreResult<RuntimePointerDiagnosticsResponse> Result, IReadOnlyList<BridgeIpcRequest> Requests)>
        RunWithServerAsync(Task<IReadOnlyList<BridgeIpcRequest>> server, LocalBridgeClient client, RuntimePointerDiagnosticsRequest request)
    {
        var phase = "runner";
        try
        {
            var result = await new RuntimePointerDiagnosticsRunner().RunAsync(client, request, _fixtureCancellation.Token);
            Trace("runner_completed", outcome: result.Success ? result.Value!.Status : result.Error!.Code);
            foreach (var diagnostic in result.Value?.Diagnostics.Take(64) ?? [])
            {
                Trace("runner_diagnostic", outcome: diagnostic.Code);
            }

            phase = "server";
            var requests = await server;
            return (result, requests);
        }
        catch (Exception failure)
        {
            Trace("coordinator_failed", failure: failure, outcome: phase);
            var serverFailedFirst = phase == "server"
                || failure is OperationCanceledException && _fixtureCancellation.IsCancellationRequested;
            _fixtureCancellation.Cancel();
            try
            {
                await server.WaitAsync(TimeSpan.FromSeconds(3));
            }
            catch (Exception serverFailure)
            {
                Trace("server_observed", failure: serverFailure);
                if (serverFailedFirst && server.IsFaulted)
                {
                    ExceptionDispatchInfo.Capture(serverFailure).Throw();
                }
            }

            throw;
        }
    }

    private void Trace(string phase, int index = -1, BridgeIpcRequest? request = null, Exception? failure = null, string? outcome = null)
    {
        var method = request?.Method is "input" or "list_top_levels" or "visual_tree" or "pick_node" or "screenshot"
            ? request.Method : null;
        var target = request?.TopLevelId ?? request?.Pick?.Target.TopLevelId;
        if (target is not ("topLevel:main" or "topLevel:popup")) target = null;
        // Only fixture symbols and exception identity are retained, never request/response payloads or messages.
        if (outcome is { Length: > 96 } || outcome?.Any(c => !char.IsAsciiLetterOrDigit(c) && c != '_') == true) outcome = "other";
        lock (_events)
        {
            if (_events.Count >= 256) return;
            var item = new FixtureEvent(index, phase, method, target, _fixtureElapsed.ElapsedMilliseconds,
                failure?.GetType().Name, failure?.HResult, outcome);
            _events.Add(item);
            _output.WriteLine("Pointer fixture: " + JsonSerializer.Serialize(item));
        }
    }

    private sealed record FixtureEvent(int Index, string Phase, string? Method, string? TopLevelId,
        long ElapsedMs, string? ErrorType, int? HResult, string? Outcome);

    private string WriteManifest(string fileName, BridgeSessionManifest manifest)
    {
        var path = Path.Combine(_manifestDirectory, fileName);
        File.WriteAllText(path, JsonSerializer.Serialize(manifest), Encoding.UTF8);
        return path;
    }

    private static BridgeIpcResponse CreateInputResponse(
        BridgeIpcRequest request,
        SessionId sessionId,
        string topLevelId,
        double x,
        double y,
        string targetNodeId = "visual:pointerTarget",
        IReadOnlyDictionary<string, string>? metadata = null)
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
                targetNodeId,
                metadata: metadata));
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
                    bounds: new NodeBounds(0, 0, 100, 60),
                    children:
                    [
                        new TreeNodeSummary(
                            "visual:popupItem",
                            "Avalonia.Controls.Button",
                            "PopupItem",
                            text: "Popup action",
                            bounds: new NodeBounds(0, 0, 50, 30))
                    ]), target: new(sessionId, topLevelId, topLevelGeneration: "popup"))
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
                    ]), target: new(sessionId, topLevelId, topLevelGeneration: "main"));

        return BridgeIpcResponse.Ok(request.RequestId, tree);
    }

    private static BridgeIpcResponse CreateMismatchedTreeResponse(
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
                    "MainWindow",
                    bounds: new NodeBounds(0, 0, 140, 100),
                    children:
                    [
                        new TreeNodeSummary(
                            "visual:panel",
                            "Avalonia.Controls.Panel",
                            "Sidebar",
                            bounds: new NodeBounds(80, 40, 40, 40),
                            children:
                            [
                                new TreeNodeSummary(
                                    "visual:button",
                                    "Avalonia.Controls.Button",
                                    "SystemProfileQuickAccessButton",
                                    automationId: "system-profile-quick-access",
                                    text: "Profile",
                                    bounds: new NodeBounds(8, 10, 24, 16))
                            ])
                    ]), target: new(sessionId, topLevelId, topLevelGeneration: "main")));
    }

    private static BridgeIpcResponse CreatePickResponse(BridgeIpcRequest request, SessionId sessionId,
        string topLevelId, double x, double y, string? leaf = null, bool truncated = false)
    {
        Assert.Equal(BridgeIpcMethods.PickNode, request.Method);
        var pick = Assert.IsType<RuntimePickRequest>(request.Pick);
        Assert.Equal(topLevelId, pick.Target.TopLevelId);
        var popup = topLevelId == "topLevel:popup";
        Assert.Equal(popup ? "popup" : "main", pick.Target.TopLevelGeneration);
        var target = new RuntimeTargetContext(sessionId, topLevelId, topLevelGeneration: popup ? "popup" : "main");
        var geometry = new RuntimePickGeometry(new string('a', 64), new(100, popup ? 60 : 100), new(popup ? 100 : 200, popup ? 60 : 200), popup ? 1 : 2,
            popup ? 1 : 2, new(popup ? 220 : 100, 200, popup ? 100 : 200, popup ? 60 : 200), "physical_desktop_pixels", DateTimeOffset.UtcNow);
        if (pick.X is null)
            return BridgeIpcResponse.Ok(request.RequestId, new RuntimePickResponse("geometry", target, geometry, null, [], [], "not_queried", false, []));
        Assert.Equal(x, pick.X); Assert.Equal(y, pick.Y); Assert.Equal(geometry.Revision, pick.ExpectedGeometryRevision);
        Assert.Equal("top_level_dip", pick.CoordinateSpace); Assert.Equal(32, pick.MaxPath);
        var outside = x < 0;
        var root = new RuntimePickedNode(new(sessionId, topLevelId, TreeKinds.Visual, popup ? "visual:popupRoot" : "visual:root"),
            popup ? "Avalonia.Controls.Primitives.PopupRoot" : "Avalonia.Controls.Window", null, null, null, new(0, 0, 100, popup ? 60 : 100), true, true);
        var hitId = leaf ?? (popup ? "visual:popupItem" : x < 50 ? "visual:hover" : "visual:root");
        var hit = root with { Target = new(sessionId, topLevelId, TreeKinds.Visual, hitId), Type = "Avalonia.Controls.Border" };
        return BridgeIpcResponse.Ok(request.RequestId, new RuntimePickResponse(outside ? "outside" : "picked", target, geometry,
            new(x, y), outside ? [] : hitId == root.Target.NodeId ? [root] : [hit, root], [], "native_occlusion_unverified", truncated, []));
    }

    private static BridgeIpcResponse CreateScreenshotResponse(BridgeIpcRequest request, SessionId sessionId, string topLevelId, int width = 100, int height = 60)
    {
        Assert.Equal(BridgeIpcMethods.Screenshot, request.Method);
        Assert.Equal(topLevelId, request.TopLevelId);
        Assert.NotNull(request.OutputPath);
        WriteImage(request.OutputPath!, width, height);

        return BridgeIpcResponse.Ok(
            request.RequestId,
            new ScreenshotResponse(
                sessionId,
                topLevelId,
                request.OutputPath!,
                width,
                height,
                DateTimeOffset.UtcNow));
    }

    private static void WriteImage(string path, int width, int height)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using var bitmap = new SKBitmap(width, height);
        bitmap.Erase(SKColors.White);
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        using var stream = File.Create(path);
        data.SaveTo(stream);
    }

    private async Task<IReadOnlyList<BridgeIpcRequest>> RespondToBridgeRequestsAsync(
        string pipeName,
        int expectedCount,
        Func<int, BridgeIpcRequest, BridgeIpcResponse> responseFactory,
        Func<int, CancellationToken, Task>? afterResponse = null,
        bool preserveListener = true)
    {
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(_fixtureCancellation.Token);
        cancellation.CancelAfter(BridgePipeTestTimeout);
        var requests = new List<BridgeIpcRequest>(expectedCount);
        NamedPipeServerStream? pendingPipe = null;
        try
        {
            while (requests.Count < expectedCount)
            {
                pendingPipe ??= CreatePipe();
                await using var pipe = pendingPipe;
                pendingPipe = null;

                BridgeIpcRequest? request = null;
                var index = requests.Count;
                try
                {
                    Trace("accepting", index);
                    await pipe.WaitForConnectionAsync(cancellation.Token);
                    Trace("connected", index);
                    // Match LocalBridgeServer: keep the Unix listener and its queued connections alive
                    // before replying and disposing this connection. False is only the regression's negative control.
                    if (preserveListener) pendingPipe = CreatePipe();
                    var requestLine = await ReadLineAsync(pipe, cancellation.Token);
                    if (string.IsNullOrWhiteSpace(requestLine)) continue;
                    request = JsonSerializer.Deserialize<BridgeIpcRequest>(requestLine);
                    if (request is null) continue;

                    requests.Add(request);
                    Trace("request", index, request);
                    var response = responseFactory(index, request);
                    Trace("response", index, request, outcome: response.Success ? "success" : response.Error!.Code);
                    var responseBytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(response) + Environment.NewLine);
                    await pipe.WriteAsync(responseBytes, cancellation.Token);
                    await pipe.FlushAsync(cancellation.Token);
                    Trace("flushed", index, request);
                    if (afterResponse is not null) await afterResponse(index, cancellation.Token);
                }
                catch (Exception failure)
                {
                    var timedOut = failure is OperationCanceledException && cancellation.IsCancellationRequested
                        && !_fixtureCancellation.IsCancellationRequested;
                    Trace("server_failed", index, request, failure, timedOut ? "deadline" : "failure");
                    // Cancel while the failing connection is still open so the runner does not enter another connect wait.
                    _fixtureCancellation.Cancel();
                    if (timedOut)
                        throw new TimeoutException($"Timed out waiting for {expectedCount} bridge IPC requests; received {requests.Count}.", failure);
                    throw;
                }
            }

            return requests;
        }
        finally
        {
            if (pendingPipe is not null) await pendingPipe.DisposeAsync();
        }

        NamedPipeServerStream CreatePipe() => new(pipeName, PipeDirection.InOut,
            NamedPipeServerStream.MaxAllowedServerInstances, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
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
