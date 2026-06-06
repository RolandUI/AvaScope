using System.Diagnostics;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using AvaScope.Protocol;

namespace AvaScope.Core;

public sealed class LocalBridgeClient
{
    private const int MaxMessageBytes = 1024 * 1024;
    private readonly TimeSpan _operationTimeout;

    public LocalBridgeClient()
        : this(null)
    {
    }

    public LocalBridgeClient(string? manifestDirectory, TimeSpan? operationTimeout = null)
    {
        ManifestDirectory = string.IsNullOrWhiteSpace(manifestDirectory)
            ? BridgeSessionManifest.GetDefaultDirectory()
            : manifestDirectory;
        _operationTimeout = operationTimeout ?? TimeSpan.FromSeconds(5);

        if (_operationTimeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(operationTimeout), operationTimeout, "Timeout must be positive.");
        }
    }

    public string ManifestDirectory { get; }

    public IReadOnlyList<BridgeSessionManifest> ListSessionManifests()
    {
        if (!Directory.Exists(ManifestDirectory))
        {
            return Array.Empty<BridgeSessionManifest>();
        }

        return Directory.EnumerateFiles(ManifestDirectory, "*.json")
            .Select(TryReadManifest)
            .Where(static manifest => manifest is not null)
            .Cast<BridgeSessionManifest>()
            .Where(static manifest => IsProcessAlive(manifest.ProcessId))
            .OrderBy(static manifest => manifest.CreatedAt)
            .ToArray();
    }

    public async Task<CoreResult<AttachToAppResponse>> AttachToAppAsync(
        int? processId = null,
        SessionId? sessionId = null,
        CancellationToken cancellationToken = default)
    {
        var manifestResult = FindSingleManifest(processId, sessionId);
        if (!manifestResult.Success)
        {
            return CoreResult<AttachToAppResponse>.Fail(manifestResult.Error!);
        }

        var healthResult = await SendAsync<HealthResponse>(
            manifestResult.Value!,
            new BridgeIpcRequest(NewRequestId(), BridgeIpcMethods.Health),
            cancellationToken);

        if (!healthResult.Success)
        {
            return CoreResult<AttachToAppResponse>.Fail(healthResult.Error!);
        }

        return CoreResult<AttachToAppResponse>.Ok(new AttachToAppResponse(
            ToSessionSummary(manifestResult.Value!),
            manifestResult.Value!.ProcessId));
    }

    public async Task<CoreResult<ListTopLevelsResponse>> ListTopLevelsAsync(
        SessionId sessionId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sessionId);

        var manifestResult = FindSingleManifest(null, sessionId);
        if (!manifestResult.Success)
        {
            return CoreResult<ListTopLevelsResponse>.Fail(manifestResult.Error!);
        }

        var topLevelsResult = await SendAsync<TopLevelSummary[]>(
            manifestResult.Value!,
            new BridgeIpcRequest(NewRequestId(), BridgeIpcMethods.ListTopLevels),
            cancellationToken);

        return topLevelsResult.Success
            ? CoreResult<ListTopLevelsResponse>.Ok(new ListTopLevelsResponse(topLevelsResult.Value))
            : CoreResult<ListTopLevelsResponse>.Fail(topLevelsResult.Error!);
    }

    public async Task<CoreResult<ScreenshotResponse>> CaptureScreenshotAsync(
        SessionId sessionId,
        string topLevelId,
        string outputPath,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sessionId);

        if (string.IsNullOrWhiteSpace(topLevelId))
        {
            return CoreResult<ScreenshotResponse>.Fail(
                new CoreError(CoreErrorCodes.InvalidBridgeRequest, "Top-level id cannot be empty."));
        }

        if (string.IsNullOrWhiteSpace(outputPath))
        {
            return CoreResult<ScreenshotResponse>.Fail(
                new CoreError(CoreErrorCodes.InvalidBridgeRequest, "Screenshot output path cannot be empty."));
        }

        var manifestResult = FindSingleManifest(null, sessionId);
        if (!manifestResult.Success)
        {
            return CoreResult<ScreenshotResponse>.Fail(manifestResult.Error!);
        }

        return await SendAsync<ScreenshotResponse>(
            manifestResult.Value!,
            new BridgeIpcRequest(NewRequestId(), BridgeIpcMethods.Screenshot, topLevelId, outputPath),
            cancellationToken);
    }

    public Task<CoreResult<TreeResponse>> VisualTreeAsync(
        SessionId sessionId,
        string topLevelId,
        int? maxDepth = null,
        CancellationToken cancellationToken = default)
    {
        return TreeAsync(
            sessionId,
            topLevelId,
            BridgeIpcMethods.VisualTree,
            maxDepth,
            cancellationToken);
    }

    public Task<CoreResult<TreeResponse>> LogicalTreeAsync(
        SessionId sessionId,
        string topLevelId,
        int? maxDepth = null,
        CancellationToken cancellationToken = default)
    {
        return TreeAsync(
            sessionId,
            topLevelId,
            BridgeIpcMethods.LogicalTree,
            maxDepth,
            cancellationToken);
    }

    private async Task<CoreResult<TreeResponse>> TreeAsync(
        SessionId sessionId,
        string topLevelId,
        string method,
        int? maxDepth,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(sessionId);

        if (string.IsNullOrWhiteSpace(topLevelId))
        {
            return CoreResult<TreeResponse>.Fail(
                new CoreError(CoreErrorCodes.InvalidBridgeRequest, "Top-level id cannot be empty."));
        }

        var manifestResult = FindSingleManifest(null, sessionId);
        if (!manifestResult.Success)
        {
            return CoreResult<TreeResponse>.Fail(manifestResult.Error!);
        }

        return await SendAsync<TreeResponse>(
            manifestResult.Value!,
            new BridgeIpcRequest(NewRequestId(), method, topLevelId, maxDepth: maxDepth),
            cancellationToken);
    }

    private CoreResult<BridgeSessionManifest> FindSingleManifest(int? processId, SessionId? sessionId)
    {
        var matches = ListSessionManifests()
            .Where(manifest => processId is null || manifest.ProcessId == processId.Value)
            .Where(manifest => sessionId is null || manifest.SessionId == sessionId)
            .ToArray();

        return matches.Length switch
        {
            0 => CoreResult<BridgeSessionManifest>.Fail(new CoreError(
                CoreErrorCodes.BridgeSessionNotFound,
                "No active AvaScope bridge session matched the requested filters.")),
            1 => CoreResult<BridgeSessionManifest>.Ok(matches[0]),
            _ => CoreResult<BridgeSessionManifest>.Fail(new CoreError(
                CoreErrorCodes.MultipleBridgeSessions,
                "Multiple active AvaScope bridge sessions matched the requested filters. Specify a process id or session id."))
        };
    }

    private async Task<CoreResult<T>> SendAsync<T>(
        BridgeSessionManifest manifest,
        BridgeIpcRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var pipe = new NamedPipeClientStream(
                ".",
                manifest.PipeName,
                PipeDirection.InOut,
                PipeOptions.Asynchronous);

            await ConnectAsync(pipe, cancellationToken);

            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(_operationTimeout);

            var requestBytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(request) + Environment.NewLine);
            await pipe.WriteAsync(requestBytes, timeout.Token);
            await pipe.FlushAsync(timeout.Token);

            var responseLine = await ReadLineAsync(pipe, timeout.Token);
            var response = JsonSerializer.Deserialize<BridgeIpcResponse>(responseLine)
                ?? throw new JsonException("Bridge IPC response payload was empty.");

            if (!string.Equals(response.RequestId, request.RequestId, StringComparison.Ordinal))
            {
                return CoreResult<T>.Fail(new CoreError(
                    CoreErrorCodes.BridgeIpcFailed,
                    "Bridge IPC response request id did not match the request."));
            }

            if (!response.Success)
            {
                return CoreResult<T>.Fail(new CoreError(
                    response.Error!.Code,
                    response.Error.Message));
            }

            var value = response.GetValue<T>();
            return value is null
                ? CoreResult<T>.Fail(new CoreError(CoreErrorCodes.BridgeIpcFailed, "Bridge IPC response value was empty."))
                : CoreResult<T>.Ok(value);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return CoreResult<T>.Fail(new CoreError(
                CoreErrorCodes.BridgeIpcUnavailable,
                "Bridge IPC request timed out."));
        }
        catch (Exception exception) when (exception is IOException or TimeoutException or JsonException or InvalidOperationException or ObjectDisposedException)
        {
            return CoreResult<T>.Fail(new CoreError(
                CoreErrorCodes.BridgeIpcUnavailable,
                exception.Message));
        }
    }

    private async Task ConnectAsync(NamedPipeClientStream pipe, CancellationToken cancellationToken)
    {
        var timeoutMilliseconds = (int)Math.Ceiling(_operationTimeout.TotalMilliseconds);
        await Task.Run(() => pipe.Connect(timeoutMilliseconds), cancellationToken);
    }

    private static async Task<string> ReadLineAsync(PipeStream pipe, CancellationToken cancellationToken)
    {
        var responseBytes = new List<byte>();
        var buffer = new byte[1];

        while (true)
        {
            var read = await pipe.ReadAsync(buffer, cancellationToken);
            if (read == 0)
            {
                break;
            }

            if (buffer[0] == (byte)'\n')
            {
                break;
            }

            if (buffer[0] == (byte)'\r')
            {
                continue;
            }

            responseBytes.Add(buffer[0]);
            if (responseBytes.Count > MaxMessageBytes)
            {
                throw new InvalidOperationException("Bridge IPC response exceeded the maximum allowed size.");
            }
        }

        if (responseBytes.Count == 0)
        {
            throw new InvalidOperationException("Bridge IPC response was empty.");
        }

        return Encoding.UTF8.GetString(responseBytes.ToArray());
    }

    private static BridgeSessionManifest? TryReadManifest(string path)
    {
        try
        {
            return JsonSerializer.Deserialize<BridgeSessionManifest>(File.ReadAllText(path, Encoding.UTF8));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException or ArgumentException)
        {
            return null;
        }
    }

    private static bool IsProcessAlive(int processId)
    {
        try
        {
            using var process = Process.GetProcessById(processId);
            return !process.HasExited;
        }
        catch (ArgumentException)
        {
            return false;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    private static SessionSummary ToSessionSummary(BridgeSessionManifest manifest)
    {
        return new SessionSummary(
            manifest.SessionId,
            SessionKinds.Runtime,
            SessionStates.Active,
            manifest.CreatedAt,
            manifest.DisplayName);
    }

    private static string NewRequestId()
    {
        return Guid.NewGuid().ToString("n");
    }
}
