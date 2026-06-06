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

        var response = await HandleRequestAsync(line, cancellationToken);
        var responseBytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(response) + Environment.NewLine);

        await pipe.WriteAsync(responseBytes, cancellationToken);
        await pipe.FlushAsync(cancellationToken);
    }

    private async Task<BridgeIpcResponse> HandleRequestAsync(string line, CancellationToken cancellationToken)
    {
        BridgeIpcRequest request;

        try
        {
            request = JsonSerializer.Deserialize<BridgeIpcRequest>(line)
                ?? throw new JsonException("Request payload was empty.");
        }
        catch (Exception exception) when (exception is JsonException or ArgumentException)
        {
            return BridgeIpcResponse.Fail(
                "unknown",
                new ProtocolError("invalid_request", exception.Message));
        }

        return request.Method switch
        {
            BridgeIpcMethods.Health => BridgeIpcResponse.Ok(request.RequestId, HealthResponse.Current()),
            BridgeIpcMethods.ListTopLevels => BridgeIpcResponse.Ok(
                request.RequestId,
                await _runtime.ListTopLevelsAsync(cancellationToken)),
            BridgeIpcMethods.Screenshot => await CaptureScreenshotAsync(request, cancellationToken),
            _ => BridgeIpcResponse.Fail(
                request.RequestId,
                new ProtocolError("unknown_method", $"Bridge method '{request.Method}' is not supported."))
        };
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
}
