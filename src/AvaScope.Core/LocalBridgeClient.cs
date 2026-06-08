using System.Diagnostics;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using AvaScope.Protocol;

namespace AvaScope.Core;

public sealed class LocalBridgeClient
{
    private const int MaxMessageBytes = 1024 * 1024;
    private const int MaxDiagnosticsSessions = 100;
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

    public async Task<CoreResult<InspectNodeResponse>> InspectNodeAsync(
        SessionId sessionId,
        string topLevelId,
        string treeKind,
        string nodeId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sessionId);

        if (string.IsNullOrWhiteSpace(topLevelId))
        {
            return CoreResult<InspectNodeResponse>.Fail(
                new CoreError(CoreErrorCodes.InvalidBridgeRequest, "Top-level id cannot be empty."));
        }

        if (string.IsNullOrWhiteSpace(treeKind))
        {
            return CoreResult<InspectNodeResponse>.Fail(
                new CoreError(CoreErrorCodes.InvalidBridgeRequest, "Tree kind cannot be empty."));
        }

        if (string.IsNullOrWhiteSpace(nodeId))
        {
            return CoreResult<InspectNodeResponse>.Fail(
                new CoreError(CoreErrorCodes.InvalidBridgeRequest, "Node id cannot be empty."));
        }

        var manifestResult = FindSingleManifest(null, sessionId);
        if (!manifestResult.Success)
        {
            return CoreResult<InspectNodeResponse>.Fail(manifestResult.Error!);
        }

        return await SendAsync<InspectNodeResponse>(
            manifestResult.Value!,
            new BridgeIpcRequest(
                NewRequestId(),
                BridgeIpcMethods.InspectNode,
                topLevelId,
                treeKind: treeKind,
                nodeId: nodeId),
            cancellationToken);
    }

    public async Task<CoreResult<FindNodesResponse>> FindNodesAsync(
        SessionId sessionId,
        string topLevelId,
        string treeKind,
        string? nodeType = null,
        string? name = null,
        string? automationId = null,
        string? text = null,
        int? maxDepth = null,
        int? maxResults = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sessionId);

        if (string.IsNullOrWhiteSpace(topLevelId))
        {
            return CoreResult<FindNodesResponse>.Fail(
                new CoreError(CoreErrorCodes.InvalidBridgeRequest, "Top-level id cannot be empty."));
        }

        if (string.IsNullOrWhiteSpace(treeKind))
        {
            return CoreResult<FindNodesResponse>.Fail(
                new CoreError(CoreErrorCodes.InvalidBridgeRequest, "Tree kind cannot be empty."));
        }

        if (string.IsNullOrWhiteSpace(nodeType)
            && string.IsNullOrWhiteSpace(name)
            && string.IsNullOrWhiteSpace(automationId)
            && string.IsNullOrWhiteSpace(text))
        {
            return CoreResult<FindNodesResponse>.Fail(
                new CoreError(CoreErrorCodes.InvalidBridgeRequest, "At least one find filter is required."));
        }

        var manifestResult = FindSingleManifest(null, sessionId);
        if (!manifestResult.Success)
        {
            return CoreResult<FindNodesResponse>.Fail(manifestResult.Error!);
        }

        return await SendAsync<FindNodesResponse>(
            manifestResult.Value!,
            new BridgeIpcRequest(
                NewRequestId(),
                BridgeIpcMethods.FindNodes,
                topLevelId,
                maxDepth: maxDepth,
                treeKind: treeKind,
                nodeType: nodeType,
                name: name,
                automationId: automationId,
                text: text,
                maxResults: maxResults),
            cancellationToken);
    }

    public async Task<CoreResult<InputResponse>> InputAsync(
        SessionId sessionId,
        string topLevelId,
        string action,
        double? x = null,
        double? y = null,
        string? inputText = null,
        string? targetNodeId = null,
        string? inputKey = null,
        string? keyModifiers = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sessionId);

        if (string.IsNullOrWhiteSpace(topLevelId))
        {
            return CoreResult<InputResponse>.Fail(
                new CoreError(CoreErrorCodes.InvalidBridgeRequest, "Top-level id cannot be empty."));
        }

        if (string.IsNullOrWhiteSpace(action))
        {
            return CoreResult<InputResponse>.Fail(
                new CoreError(CoreErrorCodes.InvalidBridgeRequest, "Input action cannot be empty."));
        }

        var manifestResult = FindSingleManifest(null, sessionId);
        if (!manifestResult.Success)
        {
            return CoreResult<InputResponse>.Fail(manifestResult.Error!);
        }

        return await SendAsync<InputResponse>(
            manifestResult.Value!,
            new BridgeIpcRequest(
                NewRequestId(),
                BridgeIpcMethods.Input,
                topLevelId,
                action: action,
                x: x,
                y: y,
                inputText: inputText,
                targetNodeId: targetNodeId,
                inputKey: inputKey,
                keyModifiers: keyModifiers),
            cancellationToken);
    }

    public async Task<CoreResult<CloseSessionResponse>> CloseSessionAsync(
        SessionId sessionId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sessionId);

        var manifestResult = FindSingleManifest(null, sessionId);
        if (!manifestResult.Success)
        {
            return CoreResult<CloseSessionResponse>.Fail(manifestResult.Error!);
        }

        return await SendAsync<CloseSessionResponse>(
            manifestResult.Value!,
            new BridgeIpcRequest(NewRequestId(), BridgeIpcMethods.CloseSession),
            cancellationToken);
    }

    public async Task<CoreResult<SessionSummary>> ReloadRuntimeAsync(
        SessionId sessionId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sessionId);

        var manifestResult = FindSingleManifest(null, sessionId);
        if (!manifestResult.Success)
        {
            return CoreResult<SessionSummary>.Fail(manifestResult.Error!);
        }

        var healthResult = await SendAsync<HealthResponse>(
            manifestResult.Value!,
            new BridgeIpcRequest(NewRequestId(), BridgeIpcMethods.Health),
            cancellationToken);

        if (!healthResult.Success)
        {
            return CoreResult<SessionSummary>.Fail(healthResult.Error!);
        }

        return CoreResult<SessionSummary>.Fail(new CoreError(
            CoreErrorCodes.RuntimeReloadNotSupported,
            "Runtime bridge reload is not supported yet. AvaScope verified the local bridge session is active, but it will not restart, inject code, or claim hot reload."));
    }

    public async Task<CoreResult<DiagnosticsResponse>> DiagnosticsAsync(
        int? processId = null,
        SessionId? sessionId = null,
        int maxSessions = 50,
        PreviewHostDiagnostic? previewHost = null,
        IReadOnlyList<PreviewSessionDiagnostic>? previewSessions = null,
        CancellationToken cancellationToken = default)
    {
        if (maxSessions is < 1 or > MaxDiagnosticsSessions)
        {
            return CoreResult<DiagnosticsResponse>.Fail(new CoreError(
                CoreErrorCodes.InvalidBridgeRequest,
                $"Diagnostics session limit must be between 1 and {MaxDiagnosticsSessions}."));
        }

        var records = EnumerateDiagnosticManifests()
            .Where(record => MatchesDiagnosticFilters(record.Manifest, processId, sessionId))
            .OrderBy(record => record.Manifest?.CreatedAt ?? DateTimeOffset.MaxValue)
            .ThenBy(record => record.Path, StringComparer.Ordinal)
            .ToArray();

        var issues = new List<ProtocolError>();
        var selectedRecords = records.Take(maxSessions).ToArray();

        if (records.Length > maxSessions)
        {
            issues.Add(new ProtocolError(
                CoreErrorCodes.DiagnosticsTruncated,
                $"Diagnostics were limited to {maxSessions} bridge session manifests."));
        }

        var bridgeSessions = new List<BridgeSessionDiagnostic>(selectedRecords.Length);
        foreach (var record in selectedRecords)
        {
            var diagnostic = await CreateDiagnosticAsync(record, cancellationToken);
            bridgeSessions.Add(diagnostic);
        }

        if (bridgeSessions.Count == 0 && (processId is not null || sessionId is not null))
        {
            issues.Add(new ProtocolError(
                CoreErrorCodes.BridgeSessionNotFound,
                "No AvaScope bridge session manifest matched the requested diagnostics filters."));
        }

        return CoreResult<DiagnosticsResponse>.Ok(new DiagnosticsResponse(
            HealthResponse.Current(),
            DateTimeOffset.UtcNow,
            ManifestDirectory,
            bridgeSessions,
            issues,
            previewHost,
            previewSessions));
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

    private async Task<BridgeSessionDiagnostic> CreateDiagnosticAsync(
        DiagnosticManifestRecord record,
        CancellationToken cancellationToken)
    {
        if (record.Manifest is null)
        {
            return new BridgeSessionDiagnostic(
                DiagnosticStatuses.Invalid,
                record.Path,
                error: record.Error);
        }

        var manifest = record.Manifest;
        if (!IsProcessAlive(manifest.ProcessId))
        {
            return new BridgeSessionDiagnostic(
                DiagnosticStatuses.Stale,
                record.Path,
                ToSessionSummary(manifest, SessionStates.Failed),
                manifest.ProcessId,
                DiagnosticTransportKinds.NamedPipe,
                manifest.PipeName,
                error: new ProtocolError(
                    CoreErrorCodes.BridgeIpcUnavailable,
                    "The process recorded by the AvaScope bridge manifest is not running."));
        }

        var healthResult = await SendAsync<HealthResponse>(
            manifest,
            new BridgeIpcRequest(NewRequestId(), BridgeIpcMethods.Health),
            cancellationToken);

        if (!healthResult.Success)
        {
            return new BridgeSessionDiagnostic(
                DiagnosticStatuses.Unavailable,
                record.Path,
                ToSessionSummary(manifest, SessionStates.Failed),
                manifest.ProcessId,
                DiagnosticTransportKinds.NamedPipe,
                manifest.PipeName,
                error: ToProtocolError(healthResult.Error!));
        }

        return new BridgeSessionDiagnostic(
            DiagnosticStatuses.Available,
            record.Path,
            ToSessionSummary(manifest),
            manifest.ProcessId,
            DiagnosticTransportKinds.NamedPipe,
            manifest.PipeName,
            healthResult.Value);
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

    private IReadOnlyList<DiagnosticManifestRecord> EnumerateDiagnosticManifests()
    {
        if (!Directory.Exists(ManifestDirectory))
        {
            return [];
        }

        return Directory.EnumerateFiles(ManifestDirectory, "*.json")
            .Select(ReadDiagnosticManifest)
            .ToArray();
    }

    private static DiagnosticManifestRecord ReadDiagnosticManifest(string path)
    {
        var fullPath = Path.GetFullPath(path);

        try
        {
            var manifest = JsonSerializer.Deserialize<BridgeSessionManifest>(File.ReadAllText(fullPath, Encoding.UTF8));
            if (manifest is null)
            {
                return new DiagnosticManifestRecord(
                    fullPath,
                    null,
                    new ProtocolError(CoreErrorCodes.BridgeManifestInvalid, "Bridge session manifest payload was empty."));
            }

            return new DiagnosticManifestRecord(fullPath, manifest, null);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException or ArgumentException)
        {
            return new DiagnosticManifestRecord(
                fullPath,
                null,
                new ProtocolError(
                    CoreErrorCodes.BridgeManifestInvalid,
                    $"Bridge session manifest could not be read: {exception.Message}"));
        }
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

    private static bool MatchesDiagnosticFilters(
        BridgeSessionManifest? manifest,
        int? processId,
        SessionId? sessionId)
    {
        if (manifest is null)
        {
            return processId is null && sessionId is null;
        }

        return (processId is null || manifest.ProcessId == processId.Value)
            && (sessionId is null || manifest.SessionId == sessionId);
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
        return ToSessionSummary(manifest, SessionStates.Active);
    }

    private static SessionSummary ToSessionSummary(BridgeSessionManifest manifest, string state)
    {
        return new SessionSummary(
            manifest.SessionId,
            SessionKinds.Runtime,
            state,
            manifest.CreatedAt,
            manifest.DisplayName);
    }

    private static ProtocolError ToProtocolError(CoreError error)
    {
        return new ProtocolError(error.Code, error.Message, error.Details);
    }

    private static string NewRequestId()
    {
        return Guid.NewGuid().ToString("n");
    }

    private sealed record DiagnosticManifestRecord(
        string Path,
        BridgeSessionManifest? Manifest,
        ProtocolError? Error);
}
