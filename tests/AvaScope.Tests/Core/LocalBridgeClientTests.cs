using System.Diagnostics;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using AvaScope.Core;
using AvaScope.Protocol;
using SkiaSharp;

namespace AvaScope.Tests.Core;

public sealed class LocalBridgeClientTests : IDisposable
{
    private static readonly TimeSpan BridgePipeTestTimeout = TimeSpan.FromSeconds(30);

    private readonly string _manifestDirectory = Path.Combine(
        Path.GetTempPath(),
        "AvaScope.Tests",
        $"manifests-{Guid.NewGuid():N}");

    [Theory]
    [InlineData("valid")]
    [InlineData("last-pixel")]
    [InlineData("tolerance")]
    [InlineData("over-tolerance")]
    [InlineData("transparent")]
    [InlineData("overflow")]
    [InlineData("dimensions")]
    [InlineData("bytes")]
    public async Task FullResolutionScreenPairIsComparedOrRejectsInvalidHalfBeforeDecode(string nativeCase)
    {
        Directory.CreateDirectory(_manifestDirectory);
        var sessionId = SessionId.New(); var pipeName = TestPipeNames.New();
        WriteManifest("screen.json", new(sessionId, Environment.ProcessId, pipeName, DateTimeOffset.UtcNow));
        var target = new RuntimeTargetContext(sessionId, "top", topLevelGeneration: "current");
        var now = DateTimeOffset.UtcNow;
        byte[] png; byte[] nativePng;
        using (var bitmap = new SKBitmap(3840, 2160, SKColorType.Bgra8888, SKAlphaType.Premul))
        {
            bitmap.Erase(SKColors.Blue);
            using var image = SKImage.FromBitmap(bitmap); using var encoded = image.Encode(SKEncodedImageFormat.Png, 100);
            png = encoded.ToArray();
            nativePng = png;
            if (nativeCase is "last-pixel" or "tolerance" or "over-tolerance" or "transparent")
            {
                bitmap.SetPixel(3839, 2159, nativeCase switch
                {
                    "tolerance" => new SKColor(0, 0, 239),
                    "over-tolerance" => new SKColor(0, 0, 238),
                    "transparent" => SKColors.Transparent,
                    _ => SKColors.Red
                });
                using var changedImage = SKImage.FromBitmap(bitmap);
                using var changedPng = changedImage.Encode(SKEncodedImageFormat.Png, 100);
                nativePng = changedPng.ToArray();
            }
        }
        var rendered = new RuntimeScreenFrame("rendered-test", "captured", 3840, 2160, now, now,
            null, null, null, [], "not_required", null, [], png);
        var native = rendered with { Source = "native-test", Png = nativePng };
        native = nativeCase switch
        {
            "overflow" => native with { PixelWidth = int.MaxValue, PixelHeight = int.MaxValue },
            "dimensions" => native with { PixelWidth = 1920, PixelHeight = 1080 },
            "bytes" => native with { Png = new byte[RuntimeScreenCaptureLimits.MaximumPngBytes + 1] },
            _ => native
        };
        var server = RespondToBridgeRequestAsync(pipeName, request =>
        {
            Assert.Equal(BridgeIpcMethods.CaptureScreen, request.Method);
            return BridgeIpcResponse.Ok(request.RequestId, new RuntimeScreenCaptureResponse("captured", target,
                new("test", "test", "controlled_ipc_fixture", null, null, "test", [], [], []), 2, 2,
                "no_sampled_geometry_change", rendered, native, null, []));
        });
        var captureElapsed = Stopwatch.StartNew();
        var result = await new LocalBridgeClient(_manifestDirectory, BridgePipeTestTimeout).CaptureScreenAsync(
            new(target, Path.Combine(_manifestDirectory, "evidence"), timeoutMs: 5000));
        captureElapsed.Stop();
        await server;
        Assert.True(result.Success, JsonSerializer.Serialize(result.Error));
        var value = result.Value!;
        Assert.True(value.Rendered!.Status == "captured", JsonSerializer.Serialize(new
        {
            nativeCase,
            elapsedMs = captureElapsed.ElapsedMilliseconds,
            response = value
        }));
        Assert.Null(value.Rendered.Png);
        Assert.True(File.Exists(value.Rendered.FilePath));
        if (nativeCase is "valid" or "last-pixel" or "tolerance" or "over-tolerance" or "transparent")
        {
            Assert.Equal("captured", value.Status);
            Assert.True(File.Exists(value.Native!.FilePath)); Assert.Null(value.Native.Png);
            Assert.True(value.Comparison!.Status == "compared", JsonSerializer.Serialize(new
            {
                nativeCase,
                elapsedMs = captureElapsed.ElapsedMilliseconds,
                response = value
            }));
            Assert.Equal(3840L * 2160 - (nativeCase == "transparent" ? 1 : 0), value.Comparison.ComparedPixels);
            Assert.Equal(nativeCase is "last-pixel" or "over-tolerance" ? 1 : 0, value.Comparison.DifferentPixels);
        }
        else
        {
            Assert.Equal("partial", value.Status); Assert.Equal("unaligned", value.Comparison!.Status);
            Assert.Equal("unavailable", value.Native!.Status); Assert.Null(value.Native.FilePath); Assert.Null(value.Native.Png);
            var diagnostic = Assert.Single(value.Native.Diagnostics);
            Assert.Equal("screen_capture_save_failed", diagnostic.Code);
            Assert.Equal(nativeCase == "dimensions" ? "decode_header" : "validate_frame", diagnostic.Details!["stage"]);
            Assert.Equal("none", diagnostic.Details["cancellationSource"]);
        }
    }

    public void Dispose()
    {
        if (Directory.Exists(_manifestDirectory))
        {
            Directory.Delete(_manifestDirectory, recursive: true);
        }
    }

    [Fact]
    public void ListSessionManifestsReturnsOnlyReadableLiveProcesses()
    {
        Directory.CreateDirectory(_manifestDirectory);
        var createdAt = new DateTimeOffset(2026, 6, 6, 23, 30, 0, TimeSpan.Zero);
        var liveManifest = new BridgeSessionManifest(
            new SessionId("session-live"),
            Environment.ProcessId,
            "avascope-live",
            createdAt,
            "Live app");
        var staleManifest = new BridgeSessionManifest(
            new SessionId("session-stale"),
            int.MaxValue,
            "avascope-stale",
            createdAt.AddMinutes(1),
            "Stale app");

        File.WriteAllText(
            Path.Combine(_manifestDirectory, "live.json"),
            JsonSerializer.Serialize(liveManifest),
            Encoding.UTF8);
        File.WriteAllText(
            Path.Combine(_manifestDirectory, "stale.json"),
            JsonSerializer.Serialize(staleManifest),
            Encoding.UTF8);
        File.WriteAllText(
            Path.Combine(_manifestDirectory, "invalid.json"),
            "{",
            Encoding.UTF8);

        if (OperatingSystem.IsWindows())
        {
            try
            {
                using var system = Process.GetProcessById(4);
                _ = system.HasExited;
            }
            catch (System.ComponentModel.Win32Exception)
            {
                // Exercise the actual protected-process failure when Windows denies the query.
                File.WriteAllText(Path.Combine(_manifestDirectory, "inaccessible.json"),
                    JsonSerializer.Serialize(new BridgeSessionManifest(new("inaccessible"), 4, "unavailable", createdAt)));
            }
        }

        var client = new LocalBridgeClient(_manifestDirectory);

        var manifests = client.ListSessionManifests();

        var manifest = Assert.Single(manifests);
        Assert.Equal(liveManifest.SessionId, manifest.SessionId);
        Assert.Equal(Environment.ProcessId, manifest.ProcessId);
        Assert.Equal("Live app", manifest.DisplayName);
    }

    [Fact]
    public async Task AttachToAppCanSelectManifestPathAndProcessName()
    {
        Directory.CreateDirectory(_manifestDirectory);
        var sessionId = SessionId.New();
        var pipeName = TestPipeNames.New();
        var processName = Process.GetCurrentProcess().ProcessName;
        var manifestPath = WriteManifest(
            "selected.json",
            new BridgeSessionManifest(
                sessionId,
                Environment.ProcessId,
                pipeName,
                DateTimeOffset.UtcNow,
                "Selected app",
                processName: processName));
        var serverTask = RespondToBridgeRequestAsync(
            pipeName,
            request => BridgeIpcResponse.Ok(
                request.RequestId,
                HealthResponse.Current(SessionCapabilitiesResponse.Current(sessionId, Environment.ProcessId))));
        var client = new LocalBridgeClient(Path.Combine(_manifestDirectory, "unused"), BridgePipeTestTimeout);

        var result = await client.AttachToAppAsync(
            processName: processName + ".exe",
            manifestPath: manifestPath);
        var request = await serverTask;

        Assert.True(result.Success, result.Error?.Message);
        Assert.Equal(BridgeIpcMethods.Health, request.Method);
        Assert.Equal(sessionId, result.Value!.Session.SessionId);
        Assert.Equal(Environment.ProcessId, result.Value.ProcessId);
        Assert.Equal(processName, result.Value.ProcessName);
        Assert.Equal(Path.GetFullPath(manifestPath), result.Value.ManifestPath);
        Assert.NotNull(result.Value.EffectiveCapabilities);
        Assert.Equal(sessionId, result.Value.EffectiveCapabilities!.SessionId);
        Assert.Contains(InputActions.Select, result.Value.EffectiveCapabilities.InputActions);
        Assert.Equal(64, result.Value.EffectiveCapabilities.Revision.Length);
    }

    [Fact]
    public async Task SessionCapabilitiesReturnsEffectiveBridgeHandshake()
    {
        Directory.CreateDirectory(_manifestDirectory);
        var sessionId = SessionId.New();
        var pipeName = TestPipeNames.New();
        WriteManifest(
            "capabilities.json",
            new BridgeSessionManifest(
                sessionId,
                Environment.ProcessId,
                pipeName,
                DateTimeOffset.UtcNow,
                "Capabilities"));
        var expected = SessionCapabilitiesResponse.Current(sessionId, Environment.ProcessId);
        var serverTask = RespondToBridgeRequestAsync(
            pipeName,
            request => BridgeIpcResponse.Ok(request.RequestId, expected));
        var client = new LocalBridgeClient(_manifestDirectory, BridgePipeTestTimeout);

        var result = await client.SessionCapabilitiesAsync(sessionId);
        var request = await serverTask;

        Assert.True(result.Success, result.Error?.Message);
        Assert.Equal(BridgeIpcMethods.Capabilities, request.Method);
        Assert.Equal(expected.Revision, result.Value!.Revision);
        Assert.Equal(BridgeIpcMethods.All, result.Value.SupportedMethods);
        Assert.Equal(InputActions.All, result.Value.InputActions);
        Assert.Equal(RuntimeMutationOperationKinds.All, result.Value.MutationCapabilities
            .SelectMany(static capability => capability.SupportedOperations)
            .Distinct(StringComparer.Ordinal)
            .ToArray());
    }

    [Fact]
    public async Task AttachLatestToAppSelectsNewestActiveMatchingManifest()
    {
        Directory.CreateDirectory(_manifestDirectory);
        var oldSessionId = SessionId.New();
        var newSessionId = SessionId.New();
        var processName = Process.GetCurrentProcess().ProcessName;
        WriteManifest(
            "old.json",
            new BridgeSessionManifest(
                oldSessionId,
                Environment.ProcessId,
                TestPipeNames.New(),
                DateTimeOffset.UtcNow.AddMinutes(-5),
                "Old app",
                processName: processName));
        var pipeName = TestPipeNames.New();
        WriteManifest(
            "new.json",
            new BridgeSessionManifest(
                newSessionId,
                Environment.ProcessId,
                pipeName,
                DateTimeOffset.UtcNow,
                "New app",
                processName: processName));
        var serverTask = RespondToBridgeRequestAsync(
            pipeName,
            request => BridgeIpcResponse.Ok(request.RequestId, HealthResponse.Current()));
        var client = new LocalBridgeClient(_manifestDirectory, BridgePipeTestTimeout);

        var result = await client.AttachLatestToAppAsync(processName: processName);
        var request = await serverTask;

        Assert.True(result.Success, result.Error?.Message);
        Assert.Equal(BridgeIpcMethods.Health, request.Method);
        Assert.Equal(newSessionId, result.Value!.Session.SessionId);
    }

    [Fact]
    public async Task AttachToAppReturnsStructuredErrorWhenNoManifestMatches()
    {
        var client = new LocalBridgeClient(_manifestDirectory);

        var result = await client.AttachToAppAsync(processId: Environment.ProcessId);

        Assert.False(result.Success);
        Assert.Null(result.Value);
        Assert.Equal(CoreErrorCodes.BridgeSessionNotFound, result.Error!.Code);
    }

    [Fact]
    public async Task CaptureScreenshotRejectsEmptyTopLevelIdBeforeIpc()
    {
        var client = new LocalBridgeClient(_manifestDirectory);

        var result = await client.CaptureScreenshotAsync(
            new SessionId("session-1"),
            " ",
            "capture.png");

        Assert.False(result.Success);
        Assert.Equal(CoreErrorCodes.InvalidBridgeRequest, result.Error!.Code);
    }

    [Fact]
    public async Task VisualTreeRejectsEmptyTopLevelIdBeforeIpc()
    {
        var client = new LocalBridgeClient(_manifestDirectory);

        var result = await client.VisualTreeAsync(
            new SessionId("session-1"),
            " ");

        Assert.False(result.Success);
        Assert.Equal(CoreErrorCodes.InvalidBridgeRequest, result.Error!.Code);
    }

    [Fact]
    public async Task FindNodesRejectsMissingFiltersBeforeIpc()
    {
        var client = new LocalBridgeClient(_manifestDirectory);

        var result = await client.FindNodesAsync(
            new SessionId("session-1"),
            "topLevel:abc",
            TreeKinds.Visual);

        Assert.False(result.Success);
        Assert.Equal(CoreErrorCodes.InvalidBridgeRequest, result.Error!.Code);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task TreeTimeoutReportsReadProgressAndAllowsAnExplicitFreshRequest(bool partialResponse)
    {
        Directory.CreateDirectory(_manifestDirectory);
        var sessionId = SessionId.New();
        var pipeName = TestPipeNames.New();
        WriteManifest("tree-timeout.json", new BridgeSessionManifest(
            sessionId, Environment.ProcessId, pipeName, DateTimeOffset.UtcNow, "Tree timeout fixture"));
        using var deadline = new CancellationTokenSource(BridgePipeTestTimeout);
        var releaseFirst = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var nextReady = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var prefix = Encoding.UTF8.GetBytes("{\"fixture-secret\":");
        var requests = new List<BridgeIpcRequest>();
        await using var firstPipe = new NamedPipeServerStream(pipeName, PipeDirection.InOut, 1,
            PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
        var server = Task.Run(async () =>
        {
            await firstPipe.WaitForConnectionAsync(deadline.Token);
            requests.Add(JsonSerializer.Deserialize<BridgeIpcRequest>(await ReadLineAsync(firstPipe, deadline.Token))!);
            if (partialResponse)
            {
                await firstPipe.WriteAsync(prefix, deadline.Token);
                await firstPipe.FlushAsync(deadline.Token);
            }
            await releaseFirst.Task.WaitAsync(deadline.Token);
            await firstPipe.DisposeAsync();
            await using var next = new NamedPipeServerStream(pipeName, PipeDirection.InOut, 1,
                PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
            nextReady.SetResult();
            await next.WaitForConnectionAsync(deadline.Token);
            var request = JsonSerializer.Deserialize<BridgeIpcRequest>(await ReadLineAsync(next, deadline.Token))!;
            requests.Add(request);
            var tree = new TreeResponse(sessionId, "topLevel:timeout", TreeKinds.Visual, 4,
                new TreeNodeSummary("visual:recovered", "Window"));
            await next.WriteAsync(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(
                BridgeIpcResponse.Ok(request.RequestId, tree)) + "\n"), deadline.Token);
        }, deadline.Token);
        try
        {
            var client = new LocalBridgeClient(_manifestDirectory, TimeSpan.FromSeconds(2));
            var timedOut = await client.VisualTreeAsync(sessionId, "topLevel:timeout", 4, deadline.Token);
            Assert.False(timedOut.Success);
            Assert.Equal(CoreErrorCodes.BridgeIpcUnavailable, timedOut.Error!.Code);
            Assert.Equal("read", timedOut.Error.Details!["ipcPhase"]);
            Assert.Equal("1", timedOut.Error.Details["ipcAttempt"]);
            Assert.Equal("2000", timedOut.Error.Details["ipcOperationTimeoutMs"]);
            Assert.Equal(partialResponse ? prefix.Length : 0, int.Parse(timedOut.Error.Details["ipcReceivedBytes"]));
            Assert.True(long.Parse(timedOut.Error.Details["ipcElapsedMs"]) >= 1900);
            Assert.Equal("4", timedOut.Error.Details["maxDepth"]);
            Assert.DoesNotContain("fixture-secret", JsonSerializer.Serialize(timedOut));

            releaseFirst.SetResult();
            await nextReady.Task.WaitAsync(deadline.Token);
            var recovered = await client.VisualTreeAsync(sessionId, "topLevel:timeout", 4, deadline.Token);
            Assert.True(recovered.Success, recovered.Error?.Message);
            Assert.Equal("visual:recovered", recovered.Value!.Root.NodeId);
            await server;
            Assert.Equal(2, requests.Count);
            Assert.All(requests, request => Assert.Equal(BridgeIpcMethods.VisualTree, request.Method));
            Assert.NotEqual(requests[0].RequestId, requests[1].RequestId);
        }
        finally
        {
            releaseFirst.TrySetResult();
            await deadline.CancelAsync();
            try { await server; } catch (OperationCanceledException) when (deadline.IsCancellationRequested) { }
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task BufferedResponsesPreserveFragmentedUnicodeFramingAndTheMessageLimit(bool oversized)
    {
        Directory.CreateDirectory(_manifestDirectory);
        var sessionId = SessionId.New();
        var pipeName = TestPipeNames.New();
        WriteManifest("buffered.json", new BridgeSessionManifest(sessionId, Environment.ProcessId, pipeName, DateTimeOffset.UtcNow, "Buffered fixture"));
        var message = "😀東京\r\n" + new string('x', oversized ? 1024 * 1024 : 128 * 1024);
        using var deadline = new CancellationTokenSource(BridgePipeTestTimeout);
        var server = Task.Run(async () =>
        {
            await using var pipe = new NamedPipeServerStream(pipeName, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
            await pipe.WaitForConnectionAsync(deadline.Token);
            var request = JsonSerializer.Deserialize<BridgeIpcRequest>(await ReadLineAsync(pipe, deadline.Token))!;
            // An error payload is sufficient to verify byte preservation without introducing an unbounded DTO.
            var response = JsonSerializer.Serialize(BridgeIpcResponse.Fail(request.RequestId, new("fixture", message)),
                new JsonSerializerOptions { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
            var bytes = Encoding.UTF8.GetBytes(response + "\r\nignored second frame\n");
            var split = Array.IndexOf(bytes, (byte)0xE6) + 1; // Split the UTF-8 sequence for the first CJK character.
            Assert.True(split > 0);
            try
            {
                await pipe.WriteAsync(bytes.AsMemory(0, split), deadline.Token);
                await pipe.FlushAsync(deadline.Token);
                await Task.Delay(10, deadline.Token);
                await pipe.WriteAsync(bytes.AsMemory(split), deadline.Token);
                await pipe.FlushAsync(deadline.Token);
            }
            catch (IOException) when (oversized) { } // Client closes immediately after enforcing its 1 MiB limit.
        }, deadline.Token);
        var result = await new LocalBridgeClient(_manifestDirectory).TraceAsync(new(sessionId, "read", "fixture-trace"), deadline.Token);
        Assert.False(result.Success);
        if (oversized)
        {
            Assert.Equal(CoreErrorCodes.BridgeIpcUnavailable, result.Error!.Code);
            Assert.Contains("maximum allowed size", result.Error.Message);
        }
        else { Assert.Equal("fixture", result.Error!.Code); Assert.Equal(message, result.Error.Message); }
        await server;
    }

    [Fact]
    public async Task GuardedInputResponseLossRemainsUnknownAndDoesNotRedispatch()
    {
        Directory.CreateDirectory(_manifestDirectory);
        var sessionId = SessionId.New();
        var pipeName = TestPipeNames.New();
        WriteManifest("guard-drop.json", new BridgeSessionManifest(sessionId, Environment.ProcessId, pipeName, DateTimeOffset.UtcNow, "Guard drop app"));
        var server = Task.Run(async () =>
        {
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            var dispatched = 0;
            await using (var pipe = new NamedPipeServerStream(pipeName, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous))
            {
                await pipe.WaitForConnectionAsync(deadline.Token);
                var request = JsonSerializer.Deserialize<BridgeIpcRequest>(await ReadLineAsync(pipe, deadline.Token))!;
                Assert.Equal(BridgeIpcMethods.Input, request.Method); Assert.NotNull(request.InputExecution!.Preconditions);
                dispatched++; // Simulate executing and losing the response before the caller receives it.
            }
            await using var repeated = new NamedPipeServerStream(pipeName, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
            using var retryWindow = new CancellationTokenSource(TimeSpan.FromMilliseconds(800));
            try
            {
                await repeated.WaitForConnectionAsync(retryWindow.Token);
                var request = JsonSerializer.Deserialize<BridgeIpcRequest>(await ReadLineAsync(repeated, deadline.Token))!;
                dispatched++;
                await repeated.WriteAsync(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(BridgeIpcResponse.Ok(request.RequestId,
                    new InputResponse(sessionId, "top", InputActions.Invoke, true, DateTimeOffset.UtcNow))) + "\n"), deadline.Token);
            }
            catch (OperationCanceledException) when (retryWindow.IsCancellationRequested) { }
            return dispatched;
        });
        var client = new LocalBridgeClient(_manifestDirectory, BridgePipeTestTimeout);
        var result = await client.InputAsync(sessionId, "top", InputActions.Invoke, execution: new()
        { Strategy = "semantic", Preconditions = new(new("literal", literal: JsonSerializer.SerializeToElement(true))) });
        Assert.False(result.Success);
        Assert.Equal("unknown", result.Error!.Details!["dispatchOutcome"]);
        Assert.Equal("unknown", result.Error.Details["dispatched"]);
        Assert.Equal(1, await server);
    }

    [Fact]
    public async Task FindNodesRetriesOneTransientTransportDisconnect()
    {
        Directory.CreateDirectory(_manifestDirectory);
        var sessionId = SessionId.New();
        var pipeName = TestPipeNames.New();
        var topLevelId = "topLevel:retry";
        WriteManifest(
            "retry.json",
            new BridgeSessionManifest(
                sessionId,
                Environment.ProcessId,
                pipeName,
                DateTimeOffset.UtcNow,
                "Retry app"));
        var serverTask = DisconnectFirstThenRespondAsync(
            pipeName,
            request => BridgeIpcResponse.Ok(
                request.RequestId,
                new FindNodesResponse(
                    sessionId,
                    topLevelId,
                    TreeKinds.Visual,
                    8,
                    [])));
        var client = new LocalBridgeClient(_manifestDirectory, BridgePipeTestTimeout);

        var result = await client.FindNodesAsync(
            sessionId,
            topLevelId,
            TreeKinds.Visual,
            automationId: "retry-target");
        var requests = await serverTask;

        Assert.True(result.Success, result.Error?.Message);
        Assert.Equal(2, requests.Count);
        Assert.All(requests, request => Assert.Equal(BridgeIpcMethods.FindNodes, request.Method));
        Assert.Equal(requests[0].RequestId, requests[1].RequestId);
    }

    [Fact]
    public async Task InspectNodeRejectsEmptyNodeIdBeforeIpc()
    {
        var client = new LocalBridgeClient(_manifestDirectory);

        var result = await client.InspectNodeAsync(
            new SessionId("session-1"),
            "topLevel:abc",
            TreeKinds.Visual,
            " ");

        Assert.False(result.Success);
        Assert.Equal(CoreErrorCodes.InvalidBridgeRequest, result.Error!.Code);
    }

    [Fact]
    public async Task InputRejectsEmptyActionBeforeIpc()
    {
        var client = new LocalBridgeClient(_manifestDirectory);

        var result = await client.InputAsync(
            new SessionId("session-1"),
            "topLevel:abc",
            " ");

        Assert.False(result.Success);
        Assert.Equal(CoreErrorCodes.InvalidBridgeRequest, result.Error!.Code);
    }

    [Fact]
    public async Task MutateNodeRejectsSessionMismatchWithoutIpc()
    {
        var client = new LocalBridgeClient(_manifestDirectory);
        var selectedSessionId = new SessionId("selected-session");
        var request = new RuntimeMutationRequest(
            "mutation-request-1",
            new RuntimeTargetContext(
                new SessionId("target-session"),
                "topLevel:abc",
                TreeKinds.Visual,
                "visual:node"),
            new RuntimeMutationOperation(RuntimeMutationOperationKinds.NoOp));

        var result = await client.MutateNodeAsync(selectedSessionId, request);

        Assert.True(result.Success, result.Error?.Message);
        Assert.False(result.Value!.Applied);
        Assert.Equal(RuntimeMutationStatuses.Unavailable, result.Value.Status);
        Assert.Equal(selectedSessionId, result.Value.SessionId);
        Assert.Equal(RuntimeMutationErrorCodes.RuntimeMutationNonLocalSession, Assert.Single(result.Value.Diagnostics).Code);
        Assert.Equal("target-session", result.Value.Diagnostics[0].Details!["targetSessionId"]);
    }

    [Fact]
    public async Task MutateNodeSendsStructuredMutationThroughBridgePipe()
    {
        Directory.CreateDirectory(_manifestDirectory);
        var sessionId = SessionId.New();
        var pipeName = TestPipeNames.New();
        WriteManifest(
            "mutation.json",
            new BridgeSessionManifest(
                sessionId,
                Environment.ProcessId,
                pipeName,
                DateTimeOffset.UtcNow,
                "Mutation app"));
        var request = new RuntimeMutationRequest(
            "mutation-request-2",
            new RuntimeTargetContext(sessionId, "topLevel:abc", TreeKinds.Visual, "visual:node"),
            new RuntimeMutationOperation(
                RuntimeMutationOperationKinds.ResetMutation,
                mutationId: "mutation:session:existing"));
        var serverTask = RespondToBridgeRequestAsync(
            pipeName,
            bridgeRequest =>
            {
                Assert.Equal(BridgeIpcMethods.MutateNode, bridgeRequest.Method);
                Assert.Equal("mutation-request-2", bridgeRequest.RequestId);
                Assert.NotNull(bridgeRequest.Mutation);
                Assert.Equal("topLevel:abc", bridgeRequest.Mutation.Target.TopLevelId);
                Assert.Equal("visual:node", bridgeRequest.Mutation.Target.NodeId);
                Assert.Equal(RuntimeMutationOperationKinds.ResetMutation, bridgeRequest.Mutation.Operation.Kind);
                Assert.Equal("mutation:session:existing", bridgeRequest.Mutation.Operation.MutationId);

                return BridgeIpcResponse.Ok(
                    bridgeRequest.RequestId,
                    new RuntimeMutationResponse(
                        bridgeRequest.Mutation.RequestId,
                        "mutation:session:1",
                        sessionId,
                        bridgeRequest.Mutation.Target.TopLevelId,
                        bridgeRequest.Mutation.Target,
                        bridgeRequest.Mutation.Operation,
                        RuntimeMutationStatuses.Applied,
                        applied: true,
                        DateTimeOffset.UtcNow,
                        RuntimeMutationCapabilityCatalog.CurrentBridgeCapabilities(),
                        metadata: new Dictionary<string, string>
                        {
                            ["resetMutationIds"] = "mutation:session:existing",
                            ["resetCount"] = "1",
                            ["activeMutationCount"] = "0"
                        }));
            });
        var client = new LocalBridgeClient(_manifestDirectory, BridgePipeTestTimeout);

        var result = await client.MutateNodeAsync(sessionId, request);
        var bridgeRequest = await serverTask;

        Assert.True(result.Success, result.Error?.Message);
        Assert.Equal(BridgeIpcMethods.MutateNode, bridgeRequest.Method);
        Assert.Equal("mutation:session:1", result.Value!.MutationId);
        Assert.Equal(RuntimeMutationStatuses.Applied, result.Value.Status);
        Assert.True(result.Value.Applied);
        Assert.Equal("mutation:session:existing", result.Value.Metadata["resetMutationIds"]);
        Assert.Equal("0", result.Value.Metadata["activeMutationCount"]);
        var styleCapability = Assert.Single(result.Value.Capabilities, capability =>
            capability.Name == RuntimeMutationCapabilityCatalog.StyleLayoutMutation);
        Assert.Equal("local_only", styleCapability.Metadata["transport"]);
        Assert.Equal("true", styleCapability.Metadata["temporary"]);
        Assert.Equal("true", styleCapability.Metadata["reversible"]);
        Assert.Empty(result.Value.Diagnostics);
    }

    [Fact]
    public async Task MutationReviewReadsBoundedHistoryThroughBridgePipe()
    {
        Directory.CreateDirectory(_manifestDirectory);
        var sessionId = SessionId.New();
        var pipeName = TestPipeNames.New();
        WriteManifest(
            "mutation-review.json",
            new BridgeSessionManifest(
                sessionId,
                Environment.ProcessId,
                pipeName,
                DateTimeOffset.UtcNow,
                "Mutation review app"));
        var target = new RuntimeTargetContext(sessionId, "topLevel:abc", TreeKinds.Visual, "visual:node");
        var operation = new RuntimeMutationOperation(
            RuntimeMutationOperationKinds.SetProperty,
            propertyName: "Width",
            value: "240",
            valueType: "double");
        var entry = new RuntimeMutationReviewEntry(
            1,
            "mutation-review-request-1",
            "mutation:session:1",
            sessionId,
            target.TopLevelId,
            target,
            operation,
            RuntimeMutationStatuses.Applied,
            applied: true,
            active: true,
            DateTimeOffset.UtcNow,
            metadata: new Dictionary<string, string>
            {
                ["propertyName"] = "Width"
            });
        var serverTask = RespondToBridgeRequestAsync(
            pipeName,
            bridgeRequest =>
            {
                Assert.Equal(BridgeIpcMethods.MutationReview, bridgeRequest.Method);
                Assert.Equal(7, bridgeRequest.MaxResults);

                return BridgeIpcResponse.Ok(
                    bridgeRequest.RequestId,
                    new RuntimeMutationReviewResponse(
                        sessionId,
                        DateTimeOffset.UtcNow,
                        historyCount: 1,
                        activeMutationCount: 1,
                        history: [entry],
                        activeMutations: [entry],
                        resetHandoff: new RuntimeMutationResetHandoff(
                            sessionId,
                            activeMutationCount: 1,
                            activeMutationIds: [entry.MutationId],
                            suggestedResetAllTarget: target)));
            });
        var client = new LocalBridgeClient(_manifestDirectory, BridgePipeTestTimeout);

        var result = await client.MutationReviewAsync(sessionId, maxResults: 7);
        var bridgeRequest = await serverTask;

        Assert.True(result.Success, result.Error?.Message);
        Assert.Equal(BridgeIpcMethods.MutationReview, bridgeRequest.Method);
        Assert.Equal(1, result.Value!.HistoryCount);
        Assert.Equal(1, result.Value.ActiveMutationCount);
        Assert.Equal("mutation:session:1", Assert.Single(result.Value.ActiveMutations).MutationId);
        Assert.Equal(RuntimeMutationOperationKinds.ResetMutation, result.Value.ResetHandoff.ResetMutationOperation);
        Assert.Equal(RuntimeMutationOperationKinds.ResetAll, result.Value.ResetHandoff.ResetAllOperation);
        Assert.Equal(target.NodeId, result.Value.ResetHandoff.SuggestedResetAllTarget!.NodeId);
    }

    [Fact]
    public async Task RuntimeMutationEvidenceRunnerCapturesSequencedArtifactsThroughBridgePipe()
    {
        Directory.CreateDirectory(_manifestDirectory);
        var sessionId = SessionId.New();
        var pipeName = TestPipeNames.New();
        var artifactDirectory = Path.Combine(
            Path.GetTempPath(),
            "AvaScope.Tests",
            $"core-evidence-{Guid.NewGuid():N}");
        WriteManifest(
            "mutation-evidence.json",
            new BridgeSessionManifest(
                sessionId,
                Environment.ProcessId,
                pipeName,
                DateTimeOffset.UtcNow,
                "Mutation evidence app"));
        var request = new RuntimeMutationRequest(
            "core-evidence",
            new RuntimeTargetContext(sessionId, "topLevel:core", TreeKinds.Visual, "visual:target"),
            new RuntimeMutationOperation(
                RuntimeMutationOperationKinds.SetProperty,
                propertyName: "Text",
                value: "After",
                valueType: "string"));
        var serverTask = RespondToBridgeRequestsAsync(
            pipeName,
            expectedCount: 5,
            (index, bridgeRequest) =>
            {
                return index switch
                {
                    0 => CreateEvidenceScreenshotResponse(
                        bridgeRequest,
                        sessionId,
                        "topLevel:core",
                        "core-evidence-before.png"),
                    1 => CreateEvidenceTreeResponse(
                        bridgeRequest,
                        sessionId,
                        "topLevel:core",
                        expectedMaxDepth: 4,
                        "Before"),
                    2 => CreateEvidenceMutationResponse(
                        bridgeRequest,
                        sessionId,
                        "topLevel:core"),
                    3 => CreateEvidenceScreenshotResponse(
                        bridgeRequest,
                        sessionId,
                        "topLevel:core",
                        "core-evidence-after.png"),
                    4 => CreateEvidenceTreeResponse(
                        bridgeRequest,
                        sessionId,
                        "topLevel:core",
                        expectedMaxDepth: 4,
                        "After"),
                    _ => throw new InvalidOperationException("Unexpected bridge request index.")
                };
            });
        var client = new LocalBridgeClient(_manifestDirectory, BridgePipeTestTimeout);

        try
        {
            var result = await new RuntimeMutationEvidenceRunner().CaptureAsync(
                client,
                sessionId,
                request,
                artifactDirectory,
                maxDepth: 4,
                includeDiff: false);
            var bridgeRequests = await serverTask;

            Assert.True(result.Success, result.Error?.Message);
            Assert.Equal(
                [
                    BridgeIpcMethods.Screenshot,
                    BridgeIpcMethods.VisualTree,
                    BridgeIpcMethods.MutateNode,
                    BridgeIpcMethods.Screenshot,
                    BridgeIpcMethods.VisualTree
                ],
                bridgeRequests.Select(static bridgeRequest => bridgeRequest.Method).ToArray());
            Assert.Equal(Path.GetFullPath(artifactDirectory), result.Value!.ArtifactDirectory);
            Assert.EndsWith("core-evidence-before.png", result.Value.BeforeScreenshotPath, StringComparison.Ordinal);
            Assert.EndsWith("core-evidence-after.png", result.Value.AfterScreenshotPath, StringComparison.Ordinal);
            Assert.EndsWith("core-evidence-before-visual-tree.json", result.Value.BeforeVisualTreePath, StringComparison.Ordinal);
            Assert.EndsWith("core-evidence-after-visual-tree.json", result.Value.AfterVisualTreePath, StringComparison.Ordinal);
            Assert.Null(result.Value.Diff);
            Assert.Equal("not_requested", result.Value.Summary.DiffStatus);
            Assert.Equal("captured", result.Value.Summary.Status);
            Assert.Equal(RuntimeMutationStatuses.Applied, result.Value.Summary.MutationStatus);
            Assert.True(result.Value.Summary.MutationApplied);
            Assert.Equal(2, result.Value.Summary.BeforeVisualTreeNodeCount);
            Assert.Equal(2, result.Value.Summary.AfterVisualTreeNodeCount);
            Assert.True(result.Value.Summary.BeforeTargetFound);
            Assert.True(result.Value.Summary.AfterTargetFound);
            Assert.Equal("Before", result.Value.BeforeTarget!.Text);
            Assert.Equal("After", result.Value.AfterTarget!.Text);
            Assert.True(File.Exists(result.Value.BeforeVisualTreePath));
            Assert.True(File.Exists(result.Value.AfterVisualTreePath));
            Assert.NotNull(result.Value.ReviewArtifact);
            Assert.True(File.Exists(result.Value.ReviewArtifact!.ArtifactPath));
            Assert.Equal("html", result.Value.ReviewArtifact.Format);
            Assert.Contains("Before", await File.ReadAllTextAsync(result.Value.BeforeVisualTreePath));
            Assert.Contains("After", await File.ReadAllTextAsync(result.Value.AfterVisualTreePath));
            var reviewHtml = await File.ReadAllTextAsync(result.Value.ReviewArtifact.ArtifactPath);
            Assert.Contains("mutation:core:1", reviewHtml, StringComparison.Ordinal);
            Assert.Contains("Before", reviewHtml, StringComparison.Ordinal);
            Assert.Contains("After", reviewHtml, StringComparison.Ordinal);
        }
        finally
        {
            if (Directory.Exists(artifactDirectory))
            {
                Directory.Delete(artifactDirectory, recursive: true);
            }
        }
    }

    [Fact]
    public async Task CloseSessionReturnsStructuredErrorWhenNoManifestMatches()
    {
        var client = new LocalBridgeClient(_manifestDirectory);

        var result = await client.CloseSessionAsync(new SessionId("missing"));

        Assert.False(result.Success);
        Assert.Equal(CoreErrorCodes.BridgeSessionNotFound, result.Error!.Code);
    }

    [Fact]
    public void NativePickerPredefinedDeletedPathIsDeterministicAndProcessScoped()
    {
        Directory.CreateDirectory(_manifestDirectory);
        var sessionId = new SessionId("picker-session");
        WriteManifest(
            "picker.json",
            new BridgeSessionManifest(
                sessionId,
                Environment.ProcessId,
                "picker-test-pipe",
                DateTimeOffset.UtcNow,
                processName: Process.GetCurrentProcess().ProcessName));
        var client = new LocalBridgeClient(_manifestDirectory);

        var result = client.NativePicker(
            sessionId,
            NativePickerOperations.PredefineResult,
            @"C:\deleted",
            NativePickerResultStates.DeletedPath);

        Assert.True(result.Success, result.Error?.Message);
        Assert.Equal(NativePickerResultStates.DeletedPath, result.Value!.Status);
        Assert.Equal(Environment.ProcessId, result.Value.ProcessId);
        Assert.False(result.Value.DialogDetected);
    }

    [Fact]
    public void NativePickerPredefinedResultIsCorrelatedRedactedAndConsumedOnce()
    {
        Directory.CreateDirectory(_manifestDirectory);
        var sessionId = new SessionId("picker-one-shot");
        WriteManifest(
            "picker-one-shot.json",
            new BridgeSessionManifest(
                sessionId,
                Environment.ProcessId,
                "picker-one-shot-pipe",
                DateTimeOffset.UtcNow,
                processName: Process.GetCurrentProcess().ProcessName));
        var client = new LocalBridgeClient(_manifestDirectory);

        var prepared = client.NativePicker(
            sessionId,
            NativePickerOperations.PredefineResult,
            @"C:\private\exports\logs",
            NativePickerResultStates.Success,
            correlationId: "scenario-42",
            ttlMs: 5000);
        var wrongCorrelation = client.NativePicker(
            sessionId,
            NativePickerOperations.ConsumePredefinedResult,
            correlationId: "scenario-other");
        var consumed = client.NativePicker(
            sessionId,
            NativePickerOperations.ConsumePredefinedResult,
            correlationId: "scenario-42");
        var replay = client.NativePicker(
            sessionId,
            NativePickerOperations.ConsumePredefinedResult,
            correlationId: "scenario-42");

        Assert.True(prepared.Success, prepared.Error?.Message);
        Assert.Equal("scenario-42", prepared.Value!.CorrelationId);
        Assert.True(prepared.Value.PathRedacted);
        Assert.DoesNotContain("private", prepared.Value.SelectedPath!, StringComparison.OrdinalIgnoreCase);
        Assert.NotNull(prepared.Value.ExpiresAt);
        Assert.Equal(NativePickerResultStates.NotPrepared, wrongCorrelation.Value!.Status);
        Assert.Equal(NativePickerResultStates.Success, consumed.Value!.Status);
        Assert.NotNull(consumed.Value.ConsumedAt);
        Assert.Equal(NativePickerResultStates.NotPrepared, replay.Value!.Status);
    }

    [Fact]
    public void NativePickerPredefinedResultExpiresBeforeConsumption()
    {
        Directory.CreateDirectory(_manifestDirectory);
        var sessionId = new SessionId("picker-expiry");
        WriteManifest(
            "picker-expiry.json",
            new BridgeSessionManifest(
                sessionId,
                Environment.ProcessId,
                "picker-expiry-pipe",
                DateTimeOffset.UtcNow,
                processName: Process.GetCurrentProcess().ProcessName));
        var client = new LocalBridgeClient(_manifestDirectory);

        var prepared = client.NativePicker(
            sessionId,
            NativePickerOperations.PredefineResult,
            predefinedResult: NativePickerResultStates.Cancelled,
            correlationId: "expires",
            ttlMs: 100);
        Thread.Sleep(250);
        var consumed = client.NativePicker(
            sessionId,
            NativePickerOperations.ConsumePredefinedResult,
            correlationId: "expires");

        Assert.True(prepared.Success, prepared.Error?.Message);
        Assert.Equal(NativePickerResultStates.Expired, consumed.Value!.Status);
        Assert.NotNull(consumed.Value.ConsumedAt);
    }

    [Fact]
    public async Task SemanticWorkflowConsumesPreparedPickerResultByRequestId()
    {
        Directory.CreateDirectory(_manifestDirectory);
        var sessionId = new SessionId("picker-workflow");
        WriteManifest(
            "picker-workflow.json",
            new BridgeSessionManifest(
                sessionId,
                Environment.ProcessId,
                "picker-workflow-pipe",
                DateTimeOffset.UtcNow,
                processName: Process.GetCurrentProcess().ProcessName));
        var client = new LocalBridgeClient(_manifestDirectory);
        var prepared = client.NativePicker(
            sessionId,
            NativePickerOperations.PredefineResult,
            predefinedResult: NativePickerResultStates.Cancelled,
            correlationId: "workflow-picker");
        var request = new SemanticWorkflowRequest(
            sessionId,
            "topLevel:test",
            [new SemanticWorkflowStep(SemanticWorkflowActions.PickerResult, "consume")],
            requestId: "workflow-picker");

        var result = await new SemanticWorkflowRunner().RunAsync(client, request);

        Assert.True(prepared.Success, prepared.Error?.Message);
        Assert.True(result.Success, result.Error?.Message);
        Assert.Equal("passed", result.Value!.Status);
        var step = Assert.Single(result.Value.Steps);
        Assert.Equal(NativePickerResultStates.Cancelled, step.Picker!.Status);
        Assert.Equal("true", step.Metadata["oneShot"]);
    }

    [Fact]
    public async Task SemanticWorkflowRetriesOnlyGenerationStaleInputThatWasNotDispatched()
    {
        Directory.CreateDirectory(_manifestDirectory);
        var sessionId = new SessionId("selector-recovery");
        var topLevelId = "topLevel:selector-recovery";
        var pipeName = TestPipeNames.New();
        WriteManifest(
            "selector-recovery.json",
            new BridgeSessionManifest(
                sessionId,
                Environment.ProcessId,
                pipeName,
                DateTimeOffset.UtcNow,
                "Selector recovery app"));

        RuntimeTargetContext Target(string nodeId, string generation) =>
            new(
                sessionId,
                topLevelId,
                TreeKinds.Visual,
                nodeId,
                topLevelGeneration: "top-generation",
                nodeGeneration: generation);

        BridgeIpcResponse FindResponse(BridgeIpcRequest request, RuntimeTargetContext target) =>
            BridgeIpcResponse.Ok(
                request.RequestId,
                new FindNodesResponse(
                    sessionId,
                    topLevelId,
                    TreeKinds.Visual,
                    8,
                    [
                        new FindNodeMatch(
                            new TreeNodeSummary(
                                target.NodeId!,
                                "Avalonia.Controls.Button",
                                automationId: "save-action",
                                bounds: new NodeBounds(10, 10, 100, 32),
                                target: target,
                                interactionState: new RuntimeNodeInteractionState(
                                    visible: true,
                                    enabled: true,
                                    rendered: true,
                                    actionable: true,
                                    availableActions: [InputActions.Invoke])),
                            ["visual:root", target.NodeId!])
                    ]));

        var oldTarget = Target("visual:old", "old-generation");
        var currentTarget = Target("visual:current", "current-generation");
        var serverTask = RespondToBridgeRequestsAsync(
            pipeName,
            expectedCount: 6,
            (index, request) => index switch
            {
                0 => FindResponse(request, oldTarget),
                1 => BridgeIpcResponse.Fail(
                    request.RequestId,
                    new ProtocolError(
                        RuntimeInputErrorCodes.TargetStale,
                        "Node was recreated before dispatch.",
                        new Dictionary<string, string> { ["dispatched"] = "false" })),
                2 => FindResponse(request, currentTarget),
                3 => BridgeIpcResponse.Ok(
                    request.RequestId,
                    new InputResponse(
                        sessionId,
                        topLevelId,
                        InputActions.Invoke,
                        handled: true,
                        DateTimeOffset.UtcNow,
                        currentTarget.NodeId,
                        currentTarget)),
                4 => FindResponse(request, currentTarget),
                5 => BridgeIpcResponse.Fail(
                    request.RequestId,
                    new ProtocolError(
                        RuntimeInputErrorCodes.TargetStale,
                        "Generation changed after dispatch.",
                        new Dictionary<string, string> { ["dispatched"] = "true" })),
                _ => throw new InvalidOperationException("Unexpected bridge request index.")
            });
        var client = new LocalBridgeClient(_manifestDirectory, BridgePipeTestTimeout);
        SemanticWorkflowRequest Request(string requestId) => new(
            sessionId,
            topLevelId,
            [
                new SemanticWorkflowStep(
                    SemanticWorkflowActions.Invoke,
                    "invoke-save",
                    new SemanticWorkflowSelector(automationId: "save-action", actionable: true))
            ],
            requestId: requestId,
            maxDepth: 8);

        var recovered = await new SemanticWorkflowRunner().RunAsync(client, Request("recover-before-dispatch"));
        var postDispatch = await new SemanticWorkflowRunner().RunAsync(client, Request("do-not-retry-after-dispatch"));
        var requests = await serverTask;

        Assert.Equal("passed", recovered.Value!.Status);
        Assert.Equal(currentTarget.NodeId, Assert.Single(recovered.Value.Steps).Target!.NodeId);
        Assert.Equal("failed", postDispatch.Value!.Status);
        Assert.Equal(RuntimeInputErrorCodes.TargetStale, Assert.Single(Assert.Single(postDispatch.Value.Steps).Diagnostics).Code);
        Assert.Equal(
            [
                BridgeIpcMethods.FindNodes,
                BridgeIpcMethods.Input,
                BridgeIpcMethods.FindNodes,
                BridgeIpcMethods.Input,
                BridgeIpcMethods.FindNodes,
                BridgeIpcMethods.Input
            ],
            requests.Select(static request => request.Method).ToArray());
        Assert.Equal(oldTarget.NodeGeneration, requests[1].InputTarget!.NodeGeneration);
        Assert.Equal(currentTarget.NodeGeneration, requests[3].InputTarget!.NodeGeneration);
    }

    [Fact]
    public async Task ReloadRuntimeReturnsStructuredErrorWhenNoManifestMatches()
    {
        var client = new LocalBridgeClient(_manifestDirectory);

        var result = await client.ReloadRuntimeAsync(new SessionId("missing"));

        Assert.False(result.Success);
        Assert.Null(result.Value);
        Assert.Equal(CoreErrorCodes.BridgeSessionNotFound, result.Error!.Code);
    }

    [Fact]
    public async Task DiagnosticsReturnsStructuredIssueWhenNoManifestMatches()
    {
        var client = new LocalBridgeClient(_manifestDirectory);

        var result = await client.DiagnosticsAsync(sessionId: new SessionId("missing"));

        Assert.True(result.Success, result.Error?.Message);
        Assert.Equal(Path.GetFullPath(_manifestDirectory), result.Value!.ManifestDirectory);
        Assert.Empty(result.Value.BridgeSessions);
        var issue = Assert.Single(result.Value.Issues);
        Assert.Equal(CoreErrorCodes.BridgeSessionNotFound, issue.Code);
        var diagnosticIssue = Assert.Single(result.Value.DiagnosticIssues);
        Assert.Equal(DiagnosticIssueSources.Diagnostics, diagnosticIssue.Source);
        Assert.Equal(DiagnosticIssueSeverities.Warning, diagnosticIssue.Severity);
        Assert.Equal(DiagnosticStatuses.Unavailable, diagnosticIssue.Status);
        Assert.Equal(CoreErrorCodes.BridgeSessionNotFound, diagnosticIssue.Code);
        Assert.Equal("diagnostics_summary", diagnosticIssue.Provenance);
    }

    [Fact]
    public async Task DiagnosticsReportsInvalidAndStaleManifestsWithoutThrowing()
    {
        Directory.CreateDirectory(_manifestDirectory);
        var createdAt = new DateTimeOffset(2026, 6, 7, 3, 30, 0, TimeSpan.Zero);
        var staleManifest = new BridgeSessionManifest(
            new SessionId("session-stale"),
            int.MaxValue,
            "avascope-stale",
            createdAt,
            "Stale app");

        var staleManifestPath = Path.Combine(_manifestDirectory, "stale.json");
        var invalidManifestPath = Path.Combine(_manifestDirectory, "invalid.json");
        var unsupportedTransportManifestPath = Path.Combine(_manifestDirectory, "unsupported-transport.json");
        File.WriteAllText(staleManifestPath, JsonSerializer.Serialize(staleManifest), Encoding.UTF8);
        File.WriteAllText(invalidManifestPath, "{", Encoding.UTF8);
        File.WriteAllText(
            unsupportedTransportManifestPath,
            $$"""
            {
              "sessionId": "session-unsupported-transport",
              "processId": {{Environment.ProcessId}},
              "pipeName": "avascope-unsupported-transport",
              "createdAt": "2026-06-07T03:30:00+00:00",
              "transportScope": "remote"
            }
            """,
            Encoding.UTF8);

        var client = new LocalBridgeClient(_manifestDirectory, TimeSpan.FromSeconds(5));

        var result = await client.DiagnosticsAsync();

        Assert.True(result.Success, result.Error?.Message);
        Assert.Empty(result.Value!.Issues);
        Assert.Collection(
            result.Value.BridgeSessions,
            stale =>
            {
                Assert.Equal(DiagnosticStatuses.Stale, stale.Status);
                Assert.Equal(Path.GetFullPath(staleManifestPath), stale.ManifestPath);
                Assert.Equal(staleManifest.SessionId, stale.Session!.SessionId);
                Assert.Equal(SessionStates.Failed, stale.Session.State);
                Assert.Equal(int.MaxValue, stale.ProcessId);
                Assert.Equal(DiagnosticTransportKinds.NamedPipe, stale.Transport);
                Assert.Equal(CoreErrorCodes.BridgeIpcUnavailable, stale.Error!.Code);
            },
            invalid =>
            {
                Assert.Equal(DiagnosticStatuses.Invalid, invalid.Status);
                Assert.Equal(Path.GetFullPath(invalidManifestPath), invalid.ManifestPath);
                Assert.Null(invalid.Session);
                Assert.Equal(CoreErrorCodes.BridgeManifestInvalid, invalid.Error!.Code);
            },
            unsupportedTransport =>
            {
                Assert.Equal(DiagnosticStatuses.Invalid, unsupportedTransport.Status);
                Assert.Equal(Path.GetFullPath(unsupportedTransportManifestPath), unsupportedTransport.ManifestPath);
                Assert.Null(unsupportedTransport.Session);
                Assert.Equal(CoreErrorCodes.BridgeManifestInvalid, unsupportedTransport.Error!.Code);
                Assert.Contains("transport scope", unsupportedTransport.Error.Message, StringComparison.OrdinalIgnoreCase);
            });
        Assert.Equal(3, result.Value.Summary.BridgeSessionCount);
        Assert.Equal(0, result.Value.Summary.ActiveBridgeSessionCount);
        Assert.Equal(1, result.Value.Summary.StaleBridgeSessionCount);
        Assert.Equal(2, result.Value.Summary.InvalidBridgeSessionCount);
        Assert.Equal(3, result.Value.Summary.InactiveBridgeSessionCount);
        Assert.Contains("avascope cleanup-bridge-sessions", result.Value.Summary.NextCommands);
        Assert.Collection(
            result.Value.DiagnosticIssues,
            stale =>
            {
                Assert.Equal(DiagnosticIssueSources.BridgeSession, stale.Source);
                Assert.Equal(DiagnosticIssueSeverities.Warning, stale.Severity);
                Assert.Equal(DiagnosticStatuses.Stale, stale.Status);
                Assert.Equal(CoreErrorCodes.BridgeIpcUnavailable, stale.Code);
                Assert.Equal(staleManifest.SessionId.Value, stale.SessionId);
                Assert.Equal(int.MaxValue, stale.ProcessId);
                Assert.Equal(Path.GetFullPath(staleManifestPath), stale.Path);
                Assert.Equal("bridge_session_manifest", stale.Provenance);
            },
            invalid =>
            {
                Assert.Equal(DiagnosticIssueSources.BridgeSession, invalid.Source);
                Assert.Equal(DiagnosticIssueSeverities.Error, invalid.Severity);
                Assert.Equal(DiagnosticStatuses.Invalid, invalid.Status);
                Assert.Equal(CoreErrorCodes.BridgeManifestInvalid, invalid.Code);
                Assert.Equal(Path.GetFullPath(invalidManifestPath), invalid.Path);
            },
            unsupportedTransport =>
            {
                Assert.Equal(DiagnosticIssueSources.BridgeSession, unsupportedTransport.Source);
                Assert.Equal(DiagnosticIssueSeverities.Error, unsupportedTransport.Severity);
                Assert.Equal(DiagnosticStatuses.Invalid, unsupportedTransport.Status);
                Assert.Equal(CoreErrorCodes.BridgeManifestInvalid, unsupportedTransport.Code);
                Assert.Equal(Path.GetFullPath(unsupportedTransportManifestPath), unsupportedTransport.Path);
            });
    }

    [Fact]
    public async Task DiagnosticsReportsDuplicateAndIncompatibleBridgeManifests()
    {
        Directory.CreateDirectory(_manifestDirectory);
        var duplicateSessionId = new SessionId("session-duplicate");
        var incompatibleSessionId = new SessionId("session-incompatible");
        var pipeName = TestPipeNames.New();
        var createdAt = new DateTimeOffset(2026, 6, 10, 8, 0, 0, TimeSpan.Zero);
        WriteManifest(
            "duplicate-a.json",
            new BridgeSessionManifest(duplicateSessionId, Environment.ProcessId, "avascope-duplicate-a", createdAt));
        WriteManifest(
            "duplicate-b.json",
            new BridgeSessionManifest(duplicateSessionId, Environment.ProcessId, "avascope-duplicate-b", createdAt.AddSeconds(1)));
        WriteManifest(
            "incompatible.json",
            new BridgeSessionManifest(
                incompatibleSessionId,
                Environment.ProcessId,
                pipeName,
                createdAt.AddSeconds(2),
                processName: Process.GetCurrentProcess().ProcessName));
        var serverTask = RespondToBridgeRequestAsync(
            pipeName,
            request => BridgeIpcResponse.Ok(
                request.RequestId,
                new HealthResponse(AvaScopeProtocol.ServiceName, new ProtocolVersion(2, 0))));
        var client = new LocalBridgeClient(_manifestDirectory, BridgePipeTestTimeout);

        var result = await client.DiagnosticsAsync(sessionId: incompatibleSessionId);
        var request = await serverTask;

        Assert.True(result.Success, result.Error?.Message);
        Assert.Equal(BridgeIpcMethods.Health, request.Method);
        var incompatible = Assert.Single(result.Value!.BridgeSessions);
        Assert.Equal(DiagnosticStatuses.Incompatible, incompatible.Status);
        Assert.Equal(CoreErrorCodes.BridgeProtocolIncompatible, incompatible.Error!.Code);
        Assert.Equal(pipeName, incompatible.PipeName);
        Assert.False(string.IsNullOrWhiteSpace(incompatible.RequestId));

        var duplicateResult = await client.DiagnosticsAsync(maxSessions: 10);

        Assert.True(duplicateResult.Success, duplicateResult.Error?.Message);
        Assert.Contains(
            duplicateResult.Value!.Issues,
            issue => issue.Code == CoreErrorCodes.BridgeManifestDuplicate
                && issue.Details is not null
                && issue.Details["sessionId"] == duplicateSessionId.Value);
    }

    [Fact]
    public async Task CleanupBridgeManifestsDeletesStaleAndInvalidRecordsOnly()
    {
        Directory.CreateDirectory(_manifestDirectory);
        var staleManifestPath = WriteManifest(
            "stale.json",
            new BridgeSessionManifest(
                new SessionId("session-stale"),
                int.MaxValue,
                "avascope-stale",
                DateTimeOffset.UtcNow));
        var invalidManifestPath = Path.Combine(_manifestDirectory, "invalid.json");
        File.WriteAllText(invalidManifestPath, "{", Encoding.UTF8);
        var liveManifestPath = WriteManifest(
            "live.json",
            new BridgeSessionManifest(
                new SessionId("session-live"),
                Environment.ProcessId,
                "avascope-live",
                DateTimeOffset.UtcNow));
        var client = new LocalBridgeClient(_manifestDirectory, TimeSpan.FromMilliseconds(50));

        var result = await client.CleanupBridgeManifestsAsync();

        Assert.True(result.Success, result.Error?.Message);
        Assert.Equal(Path.GetFullPath(_manifestDirectory), result.Value!.ManifestDirectory);
        Assert.Equal(2, result.Value.DeletedBridgeManifestRecords);
        Assert.False(File.Exists(staleManifestPath));
        Assert.False(File.Exists(invalidManifestPath));
        Assert.True(File.Exists(liveManifestPath));
        Assert.Contains(result.Value.CleanupCandidates, candidate => candidate.Status == DiagnosticStatuses.Stale);
        Assert.Contains(result.Value.CleanupCandidates, candidate => candidate.Status == DiagnosticStatuses.Invalid);
        Assert.Empty(result.Value.Issues);
    }

    [Fact]
    public async Task DiagnosticsRejectsInvalidSessionLimit()
    {
        var client = new LocalBridgeClient(_manifestDirectory);

        var result = await client.DiagnosticsAsync(maxSessions: 0);

        Assert.False(result.Success);
        Assert.Equal(CoreErrorCodes.InvalidBridgeRequest, result.Error!.Code);
    }

    [Fact]
    public async Task DiagnosticsIncludesPreviewHostDiagnosticWhenProvided()
    {
        var client = new LocalBridgeClient(_manifestDirectory);
        var previewHost = new PreviewHostDiagnostic(
            DiagnosticStatuses.Available,
            Path.Combine(AppContext.BaseDirectory, "AvaScope.PreviewHost.dll"),
            DiagnosticProcessModes.IsolatedChildProcess,
            HealthResponse.Current());

        var result = await client.DiagnosticsAsync(previewHost: previewHost);

        Assert.True(result.Success, result.Error?.Message);
        Assert.Same(previewHost, result.Value!.PreviewHost);
        Assert.Empty(result.Value.BridgeSessions);
        Assert.Empty(result.Value.Issues);
        Assert.Empty(result.Value.DiagnosticIssues);
    }

    [Fact]
    public async Task DiagnosticsReportsMixedComponentRootsAsWarning()
    {
        var client = new LocalBridgeClient(_manifestDirectory);
        var origins = new[]
        {
            new DiagnosticComponentOrigin(
                "cli",
                "C:\\repo\\.codex\\tools\\avascope\\avascope.dll",
                "C:\\repo\\.codex\\tools\\avascope",
                "C:\\repo",
                "repository"),
            new DiagnosticComponentOrigin(
                "previewHost",
                "C:\\repo\\artifacts\\executables\\avascope-win-x64-framework-dependent\\AvaScope.PreviewHost.dll",
                "C:\\repo\\artifacts\\executables\\avascope-win-x64-framework-dependent",
                "C:\\repo\\artifacts\\executables\\avascope-win-x64-framework-dependent",
                "package_artifact")
        };

        var result = await client.DiagnosticsAsync(componentOrigins: origins);

        Assert.True(result.Success, result.Error?.Message);
        Assert.Equal(origins, result.Value!.ComponentOrigins);
        var issue = Assert.Single(result.Value.Issues);
        Assert.Equal(CoreErrorCodes.DiagnosticsMixedInstallRoots, issue.Code);
        Assert.Contains("C:\\repo", issue.Details!["rootDirectories"], StringComparison.OrdinalIgnoreCase);
        Assert.Contains("previewHost:package_artifact", issue.Details["components"], StringComparison.Ordinal);
        var diagnosticIssue = Assert.Single(result.Value.DiagnosticIssues);
        Assert.Equal(DiagnosticIssueSources.Diagnostics, diagnosticIssue.Source);
        Assert.Equal(DiagnosticIssueSeverities.Warning, diagnosticIssue.Severity);
        Assert.Equal(DiagnosticStatuses.Available, diagnosticIssue.Status);
        Assert.Equal(CoreErrorCodes.DiagnosticsMixedInstallRoots, diagnosticIssue.Code);
    }

    [Fact]
    public async Task DiagnosticsTreatsTrailingDirectorySeparatorsAsTheSameComponentRoot()
    {
        var client = new LocalBridgeClient(_manifestDirectory);
        var rootDirectory = Path.Combine(_manifestDirectory, "install-root");
        var origins = new[]
        {
            new DiagnosticComponentOrigin(
                "cli",
                Path.Combine(rootDirectory, "avascope.dll"),
                rootDirectory,
                rootDirectory,
                "directory"),
            new DiagnosticComponentOrigin(
                "mcp",
                Path.Combine(rootDirectory, "AvaScope.Mcp.dll"),
                rootDirectory,
                rootDirectory + Path.DirectorySeparatorChar,
                "directory"),
            new DiagnosticComponentOrigin(
                "previewHost",
                Path.Combine(rootDirectory, "AvaScope.PreviewHost.dll"),
                rootDirectory,
                rootDirectory + Path.AltDirectorySeparatorChar,
                "directory")
        };

        var result = await client.DiagnosticsAsync(componentOrigins: origins);

        Assert.True(result.Success, result.Error?.Message);
        Assert.Equal(origins, result.Value!.ComponentOrigins);
        Assert.DoesNotContain(result.Value.Issues, issue => issue.Code == CoreErrorCodes.DiagnosticsMixedInstallRoots);
        Assert.DoesNotContain(result.Value.DiagnosticIssues, issue => issue.Code == CoreErrorCodes.DiagnosticsMixedInstallRoots);
    }

    [Fact]
    public void DiagnosticOriginBuilderClassifiesPackageArtifactRootBeforeRepositoryRoot()
    {
        var repositoryRoot = Path.Combine(_manifestDirectory, "repo");
        var packageRoot = Path.Combine(repositoryRoot, "artifacts", "executables", "avascope-win-x64-framework-dependent");
        var assemblyPath = Path.Combine(packageRoot, "AvaScope.PreviewHost.dll");
        Directory.CreateDirectory(packageRoot);
        File.WriteAllText(Path.Combine(repositoryRoot, "AvaScope.slnx"), string.Empty);
        File.WriteAllText(assemblyPath, string.Empty);

        var origin = DiagnosticOriginBuilder.Create("previewHost", assemblyPath);

        Assert.Equal("previewHost", origin.Component);
        Assert.Equal(Path.GetFullPath(packageRoot), origin.RootDirectory);
        Assert.Equal("package_artifact", origin.OriginKind);
        Assert.True(origin.Exists);
    }

    [Fact]
    public async Task DiagnosticsBuildsDiagnosticIssuesForPreviewHostAndPreviewSessions()
    {
        var client = new LocalBridgeClient(_manifestDirectory);
        var previewHostPath = Path.Combine(AppContext.BaseDirectory, "missing-preview-host.dll");
        var previewHost = new PreviewHostDiagnostic(
            DiagnosticStatuses.Unavailable,
            previewHostPath,
            DiagnosticProcessModes.IsolatedChildProcess,
            error: new ProtocolError(CoreErrorCodes.PreviewHostUnavailable, "Preview host is missing."));
        var previewRecordPath = Path.Combine(_manifestDirectory, "preview-session.json");
        var previewSessionId = new SessionId("preview-session-1");
        var previewSession = new PreviewSessionDiagnostic(
            DiagnosticStatuses.Stale,
            previewRecordPath,
            new SessionSummary(
                previewSessionId,
                SessionKinds.Preview,
                SessionStates.Failed,
                new DateTimeOffset(2026, 6, 9, 10, 0, 0, TimeSpan.Zero),
                "Stale preview"),
            error: new ProtocolError(CoreErrorCodes.PreviewSessionStoreFailed, "Preview session is stale."));

        var result = await client.DiagnosticsAsync(
            previewHost: previewHost,
            previewSessions: [previewSession]);

        Assert.True(result.Success, result.Error?.Message);
        Assert.Collection(
            result.Value!.DiagnosticIssues,
            host =>
            {
                Assert.Equal(DiagnosticIssueSources.PreviewHost, host.Source);
                Assert.Equal(DiagnosticIssueSeverities.Error, host.Severity);
                Assert.Equal(DiagnosticStatuses.Unavailable, host.Status);
                Assert.Equal(CoreErrorCodes.PreviewHostUnavailable, host.Code);
                Assert.Equal(Path.GetFullPath(previewHostPath), host.Path);
                Assert.Equal("preview_host_assembly_probe", host.Provenance);
            },
            preview =>
            {
                Assert.Equal(DiagnosticIssueSources.PreviewSession, preview.Source);
                Assert.Equal(DiagnosticIssueSeverities.Warning, preview.Severity);
                Assert.Equal(DiagnosticStatuses.Stale, preview.Status);
                Assert.Equal(CoreErrorCodes.PreviewSessionStoreFailed, preview.Code);
                Assert.Equal(previewSessionId.Value, preview.SessionId);
                Assert.Equal(Path.GetFullPath(previewRecordPath), preview.Path);
                Assert.Equal("preview_session_store_record", preview.Provenance);
            });
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task ExpressionWaitDeadlinePreservesCompletedOperandsAndReportsUnavailablePolls(
        bool observeFirst, bool failNext)
    {
        Directory.CreateDirectory(_manifestDirectory);
        var sessionId = SessionId.New(); var pipeName = TestPipeNames.New();
        WriteManifest("wait.json", new(sessionId, Environment.ProcessId, pipeName, DateTimeOffset.UtcNow));
        var definition = new RuntimeExpressionDefinition(RuntimeExpressionEvaluatorTests.Op("eq",
            RuntimeExpressionEvaluatorTests.Literal("busy"), RuntimeExpressionEvaluatorTests.Literal("saved")));
        var sampleAt = DateTimeOffset.UtcNow;
        var expression = new RuntimeExpressionResponse(sessionId, "top", "failed",
            RuntimeExpressionEvaluator.Evaluate(definition, []), [], sampleAt, sampleAt, "stable", []);
        using var serverDeadline = new CancellationTokenSource(BridgePipeTestTimeout);
        var listening = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var received = 0;
        var server = Task.Run(async () =>
        {
            try
            {
                while (true)
                {
                    await using var pipe = new NamedPipeServerStream(pipeName, PipeDirection.InOut, 1,
                        PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
                    listening.TrySetResult();
                    await pipe.WaitForConnectionAsync(serverDeadline.Token);
                    var request = JsonSerializer.Deserialize<BridgeIpcRequest>(await ReadLineAsync(pipe, serverDeadline.Token))!;
                    Assert.Equal(BridgeIpcMethods.EvaluateRuntime, request.Method);
                    received++;
                    BridgeIpcResponse? response = received == 1 && observeFirst
                        ? BridgeIpcResponse.Ok(request.RequestId, expression)
                        : received == 2 && failNext
                            ? BridgeIpcResponse.Fail(request.RequestId, new("fixture_poll_unavailable", "The next sample is unavailable."))
                            : null;
                    if (response is null)
                    {
                        // Keep the pipe open: only the workflow's own deadline can finish this poll.
                        await Task.Delay(Timeout.Infinite, serverDeadline.Token);
                    }
                    else
                    {
                        await pipe.WriteAsync(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(response) + "\n"), serverDeadline.Token);
                        await pipe.FlushAsync(serverDeadline.Token);
                    }
                }
            }
            catch (OperationCanceledException) when (serverDeadline.IsCancellationRequested) { }
        });
        await listening.Task.WaitAsync(serverDeadline.Token);
        CoreResult<SemanticWorkflowResponse> result;
        try
        {
            result = await new SemanticWorkflowRunner().RunAsync(new(_manifestDirectory, BridgePipeTestTimeout),
                new(sessionId, "top", [new(SemanticWorkflowActions.WaitForState,
                    waitCondition: new("expression", expression: definition), timeoutMs: 3000, pollIntervalMs: 25)]));
        }
        finally { await serverDeadline.CancelAsync(); await server; }
        var diagnostic = JsonSerializer.Serialize(result);
        Assert.False(OperationResultMapper.IsSuccessful(result), diagnostic);
        Assert.True(result.Value?.Steps.Count == 1, diagnostic);
        var step = result.Value!.Steps[0];
        Assert.Equal(observeFirst ? "semantic_workflow_wait_timeout" : "semantic_workflow_wait_state_unavailable", step.Diagnostics[0].Code);
        Assert.True(step.WaitObservation is not null, diagnostic);
        Assert.False(step.WaitObservation!.Matched);
        Assert.Equal(observeFirst && !failNext ? "available" : "unavailable", step.WaitObservation.Availability);
        Assert.InRange(long.Parse(step.Metadata!["elapsedMs"]), 2900, 10000);
        Assert.Equal((observeFirst ? 1 : 0) + (failNext ? 1 : 0) + 1, received);
        if (observeFirst)
        {
            Assert.True(step.WaitObservation.Expression is not null, diagnostic);
            Assert.Equal(sampleAt, step.WaitObservation.Expression!.CompletedAt);
            Assert.Equal("busy", step.WaitObservation.Expression.Result.Operands[0].Value!.Value.GetString());
            Assert.Equal("saved", step.WaitObservation.Expression.Result.Operands[1].Value!.Value.GetString());
            Assert.Contains("false", step.Metadata["expressionFindings"]);
        }
        else Assert.Null(step.WaitObservation.Expression);
        if (failNext) Assert.Equal("fixture_poll_unavailable", step.Metadata["lastErrorCode"]);
    }

    private string WriteManifest(string fileName, BridgeSessionManifest manifest)
    {
        var manifestPath = Path.Combine(_manifestDirectory, fileName);
        File.WriteAllText(manifestPath, JsonSerializer.Serialize(manifest), Encoding.UTF8);
        return manifestPath;
    }

    private static async Task<BridgeIpcRequest> RespondToBridgeRequestAsync(
        string pipeName,
        Func<BridgeIpcRequest, BridgeIpcResponse> responseFactory)
    {
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        try
        {
            while (true)
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

                BridgeIpcRequest? request;
                try
                {
                    request = JsonSerializer.Deserialize<BridgeIpcRequest>(requestLine);
                }
                catch (JsonException)
                {
                    continue;
                }

                if (request is null)
                {
                    continue;
                }

                var responseBytes = Encoding.UTF8.GetBytes(
                    JsonSerializer.Serialize(responseFactory(request)) + Environment.NewLine);
                try
                {
                    await pipe.WriteAsync(responseBytes, cancellation.Token);
                    await pipe.FlushAsync(cancellation.Token);
                }
                catch (IOException)
                {
                    return request;
                }

                return request;
            }
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            throw new TimeoutException($"Timed out waiting for a bridge IPC request on pipe '{pipeName}'.");
        }
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

                BridgeIpcRequest? request;
                try
                {
                    request = JsonSerializer.Deserialize<BridgeIpcRequest>(requestLine);
                }
                catch (JsonException)
                {
                    continue;
                }

                if (request is null)
                {
                    continue;
                }

                var index = requests.Count;
                requests.Add(request);
                var responseBytes = Encoding.UTF8.GetBytes(
                    JsonSerializer.Serialize(responseFactory(index, request)) + Environment.NewLine);
                try
                {
                    await pipe.WriteAsync(responseBytes, cancellation.Token);
                    await pipe.FlushAsync(cancellation.Token);
                }
                catch (IOException) when (requests.Count == expectedCount)
                {
                    return requests;
                }
            }

            return requests;
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            throw new TimeoutException($"Timed out waiting for {expectedCount} bridge IPC requests on pipe '{pipeName}'.");
        }
    }

    private static async Task<IReadOnlyList<BridgeIpcRequest>> DisconnectFirstThenRespondAsync(
        string pipeName,
        Func<BridgeIpcRequest, BridgeIpcResponse> responseFactory)
    {
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var requests = new List<BridgeIpcRequest>(2);

        for (var index = 0; index < 2; index++)
        {
            await using var pipe = new NamedPipeServerStream(
                pipeName,
                PipeDirection.InOut,
                maxNumberOfServerInstances: 1,
                PipeTransmissionMode.Byte,
                PipeOptions.Asynchronous);

            await pipe.WaitForConnectionAsync(cancellation.Token);
            var requestLine = await ReadLineAsync(pipe, cancellation.Token);
            var request = JsonSerializer.Deserialize<BridgeIpcRequest>(requestLine)
                ?? throw new InvalidOperationException("Bridge IPC request payload was empty.");
            requests.Add(request);

            if (index == 0)
            {
                continue;
            }

            var responseBytes = Encoding.UTF8.GetBytes(
                JsonSerializer.Serialize(responseFactory(request)) + Environment.NewLine);
            await pipe.WriteAsync(responseBytes, cancellation.Token);
            await pipe.FlushAsync(cancellation.Token);
        }

        return requests;
    }

    private static BridgeIpcResponse CreateEvidenceScreenshotResponse(
        BridgeIpcRequest request,
        SessionId sessionId,
        string topLevelId,
        string expectedFileName)
    {
        Assert.Equal(BridgeIpcMethods.Screenshot, request.Method);
        Assert.Equal(topLevelId, request.TopLevelId);
        Assert.NotNull(request.OutputPath);
        Assert.EndsWith(expectedFileName, request.OutputPath, StringComparison.Ordinal);

        return BridgeIpcResponse.Ok(
            request.RequestId,
            new ScreenshotResponse(
                sessionId,
                topLevelId,
                request.OutputPath!,
                10,
                10,
                DateTimeOffset.UtcNow));
    }

    private static BridgeIpcResponse CreateEvidenceTreeResponse(
        BridgeIpcRequest request,
        SessionId sessionId,
        string topLevelId,
        int expectedMaxDepth,
        string targetText)
    {
        Assert.Equal(BridgeIpcMethods.VisualTree, request.Method);
        Assert.Equal(topLevelId, request.TopLevelId);
        Assert.Equal(expectedMaxDepth, request.MaxDepth);

        return BridgeIpcResponse.Ok(
            request.RequestId,
            CreateEvidenceTree(sessionId, topLevelId, expectedMaxDepth, targetText));
    }

    private static BridgeIpcResponse CreateEvidenceMutationResponse(
        BridgeIpcRequest request,
        SessionId sessionId,
        string topLevelId)
    {
        Assert.Equal(BridgeIpcMethods.MutateNode, request.Method);
        Assert.NotNull(request.Mutation);
        Assert.Equal("core-evidence", request.Mutation.RequestId);
        Assert.Equal(topLevelId, request.Mutation.Target.TopLevelId);
        Assert.Equal("visual:target", request.Mutation.Target.NodeId);
        Assert.Equal(RuntimeMutationOperationKinds.SetProperty, request.Mutation.Operation.Kind);
        Assert.Equal("Text", request.Mutation.Operation.PropertyName);
        Assert.Equal("After", request.Mutation.Operation.Value);

        return BridgeIpcResponse.Ok(
            request.RequestId,
            new RuntimeMutationResponse(
                request.Mutation.RequestId,
                "mutation:core:1",
                sessionId,
                topLevelId,
                request.Mutation.Target,
                request.Mutation.Operation,
                RuntimeMutationStatuses.Applied,
                applied: true,
                DateTimeOffset.UtcNow,
                RuntimeMutationCapabilityCatalog.CurrentBridgeCapabilities()));
    }

    private static TreeResponse CreateEvidenceTree(
        SessionId sessionId,
        string topLevelId,
        int depthLimit,
        string targetText)
    {
        return new TreeResponse(
            sessionId,
            topLevelId,
            TreeKinds.Visual,
            depthLimit,
            new TreeNodeSummary(
                "visual:root",
                "Avalonia.Controls.Window",
                "EvidenceWindow",
                children:
                [
                    new TreeNodeSummary(
                        "visual:target",
                        "Avalonia.Controls.TextBlock",
                        "EvidenceTarget",
                        text: targetText,
                        bounds: new NodeBounds(1, 2, 100, 32),
                        classes: ["evidence-target"],
                        target: new RuntimeTargetContext(
                            sessionId,
                            topLevelId,
                            TreeKinds.Visual,
                            "visual:target"))
                ]));
    }

    private static async Task<string> ReadLineAsync(Stream stream, CancellationToken cancellationToken)
    {
        var bytes = new List<byte>();
        var buffer = new byte[128];

        while (true)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken);
            if (read == 0)
            {
                break;
            }

            for (var index = 0; index < read; index++)
            {
                if (buffer[index] == (byte)'\n')
                {
                    return Encoding.UTF8.GetString(bytes.ToArray());
                }

                if (buffer[index] != (byte)'\r')
                {
                    bytes.Add(buffer[index]);
                }
            }
        }

        return Encoding.UTF8.GetString(bytes.ToArray());
    }
}
