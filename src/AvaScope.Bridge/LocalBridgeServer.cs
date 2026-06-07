using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using AvaScope.Core;
using AvaScope.Protocol;

namespace AvaScope.Bridge;

internal sealed class LocalBridgeServer : IDisposable
{
    private readonly CancellationTokenSource _cancellation = new();
    private readonly object _pipeSyncRoot = new();
    private readonly AvaScopeBridgeRuntime _runtime;
    private readonly Task _serverTask;
    private NamedPipeServerStream? _activePipe;

    private LocalBridgeServer(AvaScopeBridgeRuntime runtime, string pipeName, string manifestPath)
    {
        _runtime = runtime;
        PipeName = pipeName;
        ManifestPath = manifestPath;
        _serverTask = Task.Run(() => RunAsync(_cancellation.Token));
    }

    public string PipeName { get; }

    public string ManifestPath { get; }

    public static LocalBridgeServer Start(AvaScopeBridgeRuntime runtime)
    {
        ArgumentNullException.ThrowIfNull(runtime);

        var pipeName = $"avascope-{Environment.ProcessId}-{runtime.SessionId.Value}";
        var manifestPath = GetManifestPath(runtime.SessionId);
        var manifest = new BridgeSessionManifest(
            runtime.SessionId,
            Environment.ProcessId,
            pipeName,
            runtime.Session.CreatedAt,
            runtime.Session.DisplayName);

        Directory.CreateDirectory(Path.GetDirectoryName(manifestPath)!);
        File.WriteAllText(manifestPath, JsonSerializer.Serialize(manifest), Encoding.UTF8);

        return new LocalBridgeServer(runtime, pipeName, manifestPath);
    }

    public void Dispose()
    {
        _cancellation.Cancel();

        lock (_pipeSyncRoot)
        {
            _activePipe?.Dispose();
        }

        try
        {
            _serverTask.Wait(TimeSpan.FromSeconds(2));
        }
        catch (AggregateException)
        {
        }
        catch (ObjectDisposedException)
        {
        }

        _cancellation.Dispose();

        if (File.Exists(ManifestPath))
        {
            File.Delete(ManifestPath);
        }
    }

    public static string GetManifestPath(SessionId sessionId)
    {
        ArgumentNullException.ThrowIfNull(sessionId);

        return BridgeSessionManifest.GetDefaultPath(sessionId);
    }

    public static string GetManifestDirectory()
    {
        return BridgeSessionManifest.GetDefaultDirectory();
    }

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            await using var pipe = CreatePipe();

            try
            {
                lock (_pipeSyncRoot)
                {
                    _activePipe = pipe;
                }

                await pipe.WaitForConnectionAsync(cancellationToken);
                await HandleConnectionAsync(pipe, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return;
            }
            catch (ObjectDisposedException) when (cancellationToken.IsCancellationRequested)
            {
                return;
            }
            catch (IOException) when (cancellationToken.IsCancellationRequested)
            {
                return;
            }
            catch (IOException)
            {
                // A client can disconnect mid-request; keep serving later local clients.
            }
            finally
            {
                lock (_pipeSyncRoot)
                {
                    if (ReferenceEquals(_activePipe, pipe))
                    {
                        _activePipe = null;
                    }
                }
            }
        }
    }

    private NamedPipeServerStream CreatePipe()
    {
        return new NamedPipeServerStream(
            PipeName,
            PipeDirection.InOut,
            4,
            PipeTransmissionMode.Byte,
            PipeOptions.Asynchronous);
    }

    private async Task HandleConnectionAsync(NamedPipeServerStream pipe, CancellationToken cancellationToken)
    {
        var requestBytes = new List<byte>();
        var buffer = new byte[1];

        while (true)
        {
            var read = await pipe.ReadAsync(buffer, cancellationToken);
            if (read == 0)
            {
                return;
            }

            if (buffer[0] == (byte)'\n')
            {
                break;
            }

            if (buffer[0] != (byte)'\r')
            {
                requestBytes.Add(buffer[0]);
            }
        }

        var line = Encoding.UTF8.GetString(requestBytes.ToArray());
        if (string.IsNullOrWhiteSpace(line))
        {
            return;
        }

        var result = await HandleRequestAsync(line, cancellationToken);
        var responseBytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(result.Response) + Environment.NewLine);

        await pipe.WriteAsync(responseBytes, cancellationToken);
        await pipe.FlushAsync(cancellationToken);

        if (result.CloseAfterResponse)
        {
            _ = Task.Run(() => AvaScopeBridge.CompleteRemoteClose(_runtime), CancellationToken.None);
        }
    }

    private async Task<BridgeRequestResult> HandleRequestAsync(string line, CancellationToken cancellationToken)
    {
        BridgeIpcRequest request;

        try
        {
            request = JsonSerializer.Deserialize<BridgeIpcRequest>(line)
                ?? throw new JsonException("Request payload was empty.");
        }
        catch (Exception exception) when (exception is JsonException or ArgumentException)
        {
            return Respond(BridgeIpcResponse.Fail(
                "unknown",
                new ProtocolError("invalid_request", exception.Message)));
        }

        return request.Method switch
        {
            BridgeIpcMethods.Health => Respond(BridgeIpcResponse.Ok(request.RequestId, HealthResponse.Current())),
            BridgeIpcMethods.ListTopLevels => Respond(BridgeIpcResponse.Ok(
                request.RequestId,
                await _runtime.ListTopLevelsAsync(cancellationToken))),
            BridgeIpcMethods.Screenshot => Respond(await CaptureScreenshotAsync(request, cancellationToken)),
            BridgeIpcMethods.VisualTree => Respond(await GetTreeAsync(request, TreeKinds.Visual, cancellationToken)),
            BridgeIpcMethods.LogicalTree => Respond(await GetTreeAsync(request, TreeKinds.Logical, cancellationToken)),
            BridgeIpcMethods.FindNodes => Respond(await FindNodesAsync(request, cancellationToken)),
            BridgeIpcMethods.Input => Respond(await InputAsync(request, cancellationToken)),
            BridgeIpcMethods.CloseSession => CloseSession(request),
            _ => Respond(BridgeIpcResponse.Fail(
                request.RequestId,
                new ProtocolError("unknown_method", $"Bridge method '{request.Method}' is not supported.")))
        };
    }

    private BridgeRequestResult CloseSession(BridgeIpcRequest request)
    {
        var result = _runtime.CloseSession();

        return result.Success
            ? Respond(BridgeIpcResponse.Ok(
                    request.RequestId,
                    new CloseSessionResponse(
                        ToSessionSummary(result.Value!),
                        Environment.ProcessId,
                        DateTimeOffset.UtcNow)),
                closeAfterResponse: true)
            : Respond(BridgeIpcResponse.Fail(
                request.RequestId,
                new ProtocolError(result.Error!.Code, result.Error.Message)));
    }

    private async Task<BridgeIpcResponse> CaptureScreenshotAsync(
        BridgeIpcRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.TopLevelId))
        {
            return BridgeIpcResponse.Fail(
                request.RequestId,
                new ProtocolError("missing_top_level_id", "Screenshot requests require a top-level id."));
        }

        if (string.IsNullOrWhiteSpace(request.OutputPath))
        {
            return BridgeIpcResponse.Fail(
                request.RequestId,
                new ProtocolError(BridgeErrorCodes.InvalidScreenshotPath, "Screenshot requests require an output path."));
        }

        var result = await _runtime.CaptureScreenshotAsync(request.TopLevelId, request.OutputPath, cancellationToken);

        return result.Success
            ? BridgeIpcResponse.Ok(request.RequestId, result.Value)
            : BridgeIpcResponse.Fail(
                request.RequestId,
                new ProtocolError(result.Error!.Code, result.Error.Message));
    }

    private async Task<BridgeIpcResponse> GetTreeAsync(
        BridgeIpcRequest request,
        string treeKind,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.TopLevelId))
        {
            return BridgeIpcResponse.Fail(
                request.RequestId,
                new ProtocolError("missing_top_level_id", "Tree requests require a top-level id."));
        }

        var result = treeKind switch
        {
            TreeKinds.Visual => await _runtime.GetVisualTreeAsync(
                request.TopLevelId,
                request.MaxDepth,
                cancellationToken),
            TreeKinds.Logical => await _runtime.GetLogicalTreeAsync(
                request.TopLevelId,
                request.MaxDepth,
                cancellationToken),
            _ => throw new ArgumentOutOfRangeException(nameof(treeKind), treeKind, "Unknown tree kind.")
        };

        return result.Success
            ? BridgeIpcResponse.Ok(request.RequestId, result.Value)
            : BridgeIpcResponse.Fail(
                request.RequestId,
                new ProtocolError(result.Error!.Code, result.Error.Message));
    }

    private async Task<BridgeIpcResponse> FindNodesAsync(
        BridgeIpcRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.TopLevelId))
        {
            return BridgeIpcResponse.Fail(
                request.RequestId,
                new ProtocolError("missing_top_level_id", "Find requests require a top-level id."));
        }

        if (string.IsNullOrWhiteSpace(request.TreeKind))
        {
            return BridgeIpcResponse.Fail(
                request.RequestId,
                new ProtocolError(BridgeErrorCodes.InvalidFindRequest, "Find requests require a tree kind."));
        }

        var result = await _runtime.FindNodesAsync(
            request.TopLevelId,
            request.TreeKind,
            request.NodeType,
            request.Name,
            request.AutomationId,
            request.Text,
            request.MaxDepth,
            request.MaxResults,
            cancellationToken);

        return result.Success
            ? BridgeIpcResponse.Ok(request.RequestId, result.Value)
            : BridgeIpcResponse.Fail(
                request.RequestId,
                new ProtocolError(result.Error!.Code, result.Error.Message));
    }

    private async Task<BridgeIpcResponse> InputAsync(
        BridgeIpcRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.TopLevelId))
        {
            return BridgeIpcResponse.Fail(
                request.RequestId,
                new ProtocolError("missing_top_level_id", "Input requests require a top-level id."));
        }

        if (string.IsNullOrWhiteSpace(request.Action))
        {
            return BridgeIpcResponse.Fail(
                request.RequestId,
                new ProtocolError(BridgeErrorCodes.InvalidInputRequest, "Input requests require an action."));
        }

        var result = await _runtime.InputAsync(
            request.TopLevelId,
            request.Action,
            request.X,
            request.Y,
            request.InputText,
            cancellationToken);

        return result.Success
            ? BridgeIpcResponse.Ok(request.RequestId, result.Value)
            : BridgeIpcResponse.Fail(
                request.RequestId,
                new ProtocolError(result.Error!.Code, result.Error.Message));
    }

    private static BridgeRequestResult Respond(
        BridgeIpcResponse response,
        bool closeAfterResponse = false)
    {
        return new BridgeRequestResult(response, closeAfterResponse);
    }

    private static SessionSummary ToSessionSummary(SessionSnapshot session)
    {
        return new SessionSummary(
            session.Id,
            session.Kind,
            ToProtocolState(session.State),
            session.CreatedAt,
            session.DisplayName);
    }

    private static string ToProtocolState(SessionLifecycleState state)
    {
        return state switch
        {
            SessionLifecycleState.Active => SessionStates.Active,
            SessionLifecycleState.Closing => SessionStates.Closing,
            SessionLifecycleState.Closed => SessionStates.Closed,
            SessionLifecycleState.Failed => SessionStates.Failed,
            _ => throw new ArgumentOutOfRangeException(nameof(state), state, "Unknown session state.")
        };
    }

    private sealed record BridgeRequestResult(
        BridgeIpcResponse Response,
        bool CloseAfterResponse);
}
