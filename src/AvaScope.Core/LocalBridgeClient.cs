using System.Diagnostics;
using System.Globalization;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using AvaScope.Protocol;

namespace AvaScope.Core;

public sealed class LocalBridgeClient
{
    private const int MaxMessageBytes = 1024 * 1024;
    private const int MaxDiagnosticsSessions = 100;
    private const int MaxDiagnosticIssues = 200;
    private readonly TimeSpan _operationTimeout;

    public LocalBridgeClient()
        : this(null)
    {
    }

    public LocalBridgeClient(string? manifestDirectory, TimeSpan? operationTimeout = null)
    {
        ManifestDirectory = string.IsNullOrWhiteSpace(manifestDirectory)
            ? BridgeSessionManifest.GetDefaultDirectory()
            : Path.GetFullPath(manifestDirectory);
        _operationTimeout = operationTimeout ?? TimeSpan.FromSeconds(5);

        if (_operationTimeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(operationTimeout), operationTimeout, "Timeout must be positive.");
        }
    }

    public string ManifestDirectory { get; }

    public TimeSpan OperationTimeout => _operationTimeout;

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
        string? processName = null,
        string? manifestPath = null,
        CancellationToken cancellationToken = default)
    {
        var manifestResult = FindSingleManifest(processId, sessionId, processName, manifestPath);
        if (!manifestResult.Success)
        {
            return CoreResult<AttachToAppResponse>.Fail(manifestResult.Error!);
        }

        var manifest = manifestResult.Value!;
        var healthResult = await SendAsync<HealthResponse>(
            manifest,
            new BridgeIpcRequest(NewRequestId(), BridgeIpcMethods.Health),
            cancellationToken);

        if (!healthResult.Success)
        {
            return CoreResult<AttachToAppResponse>.Fail(healthResult.Error!);
        }

        var compatibility = ValidateProtocolCompatibility(healthResult.Value!, manifest);
        if (!compatibility.Success)
        {
            return CoreResult<AttachToAppResponse>.Fail(compatibility.Error!);
        }

        return CoreResult<AttachToAppResponse>.Ok(new AttachToAppResponse(
            ToSessionSummary(manifest),
            manifest.ProcessId,
            GetProcessName(manifest.ProcessId) ?? manifest.ProcessName,
            ResolveManifestPath(manifest, manifestPath)));
    }

    public async Task<CoreResult<AttachToAppResponse>> AttachLatestToAppAsync(
        int? processId = null,
        string? processName = null,
        CancellationToken cancellationToken = default)
    {
        var manifestResult = FindLatestManifest(processId, processName);
        if (!manifestResult.Success)
        {
            return CoreResult<AttachToAppResponse>.Fail(manifestResult.Error!);
        }

        return await AttachToAppAsync(
            manifestResult.Value!.ProcessId,
            manifestResult.Value.SessionId,
            processName,
            cancellationToken: cancellationToken);
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

    public async Task<CoreResult<LayoutExplainResponse>> ExplainLayoutAsync(
        SessionId sessionId,
        string topLevelId,
        string treeKind,
        string nodeId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sessionId);

        if (string.IsNullOrWhiteSpace(topLevelId))
        {
            return CoreResult<LayoutExplainResponse>.Fail(
                new CoreError(CoreErrorCodes.InvalidBridgeRequest, "Top-level id cannot be empty."));
        }

        if (string.IsNullOrWhiteSpace(treeKind))
        {
            return CoreResult<LayoutExplainResponse>.Fail(
                new CoreError(CoreErrorCodes.InvalidBridgeRequest, "Tree kind cannot be empty."));
        }

        if (string.IsNullOrWhiteSpace(nodeId))
        {
            return CoreResult<LayoutExplainResponse>.Fail(
                new CoreError(CoreErrorCodes.InvalidBridgeRequest, "Node id cannot be empty."));
        }

        var manifestResult = FindSingleManifest(null, sessionId);
        if (!manifestResult.Success)
        {
            return CoreResult<LayoutExplainResponse>.Fail(manifestResult.Error!);
        }

        return await SendAsync<LayoutExplainResponse>(
            manifestResult.Value!,
            new BridgeIpcRequest(
                NewRequestId(),
                BridgeIpcMethods.ExplainLayout,
                topLevelId,
                treeKind: treeKind,
                nodeId: nodeId),
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

    public async Task<CoreResult<RuntimeMutationResponse>> MutateNodeAsync(
        SessionId sessionId,
        RuntimeMutationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sessionId);
        ArgumentNullException.ThrowIfNull(request);

        if (request.Target.SessionId != sessionId)
        {
            return RuntimeMutationUnavailable(
                sessionId,
                request,
                new ProtocolError(
                    RuntimeMutationErrorCodes.RuntimeMutationNonLocalSession,
                    "Runtime mutation requests must target the selected local bridge session.",
                    new Dictionary<string, string>
                    {
                        ["selectedSessionId"] = sessionId.Value,
                        ["targetSessionId"] = request.Target.SessionId.Value,
                        ["nextAction"] = "Use a target context returned by this session's visual-tree, logical-tree, find-nodes, or inspect-node command."
                    }));
        }

        var manifestResult = FindSingleManifest(null, sessionId);
        if (!manifestResult.Success)
        {
            return CoreResult<RuntimeMutationResponse>.Fail(manifestResult.Error!);
        }

        var manifest = manifestResult.Value!;
        if (!string.Equals(manifest.TransportScope, BridgeTransportScopes.LocalOnly, StringComparison.Ordinal))
        {
            return RuntimeMutationUnavailable(
                sessionId,
                request,
                new ProtocolError(
                    RuntimeMutationErrorCodes.RuntimeMutationNonLocalSession,
                    "Runtime mutation is available only for local bridge sessions.",
                    new Dictionary<string, string>
                    {
                        ["sessionId"] = sessionId.Value,
                        ["transportScope"] = manifest.TransportScope,
                        ["nextAction"] = "Attach to an app that exposes a local AvaScope bridge session."
                    }));
        }

        return await SendAsync<RuntimeMutationResponse>(
            manifest,
            new BridgeIpcRequest(
                request.RequestId,
                BridgeIpcMethods.MutateNode,
                mutation: request),
            cancellationToken);
    }

    public async Task<CoreResult<RuntimeMutationReviewResponse>> MutationReviewAsync(
        SessionId sessionId,
        int? maxResults = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sessionId);

        if (maxResults is < 1 or > RuntimeMutationReviewResponse.MaximumEntries)
        {
            return CoreResult<RuntimeMutationReviewResponse>.Fail(new CoreError(
                CoreErrorCodes.InvalidBridgeRequest,
                $"Mutation review maxResults must be between 1 and {RuntimeMutationReviewResponse.MaximumEntries.ToString(CultureInfo.InvariantCulture)}."));
        }

        var manifestResult = FindSingleManifest(null, sessionId);
        if (!manifestResult.Success)
        {
            return CoreResult<RuntimeMutationReviewResponse>.Fail(manifestResult.Error!);
        }

        var manifest = manifestResult.Value!;
        if (!string.Equals(manifest.TransportScope, BridgeTransportScopes.LocalOnly, StringComparison.Ordinal))
        {
            return CoreResult<RuntimeMutationReviewResponse>.Fail(new CoreError(
                RuntimeMutationErrorCodes.RuntimeMutationNonLocalSession,
                "Runtime mutation review is available only for local bridge sessions.",
                new Dictionary<string, string>
                {
                    ["sessionId"] = sessionId.Value,
                    ["transportScope"] = manifest.TransportScope,
                    ["nextAction"] = "Attach to an app that exposes a local AvaScope bridge session."
                }));
        }

        return await SendAsync<RuntimeMutationReviewResponse>(
            manifest,
            new BridgeIpcRequest(
                NewRequestId(),
                BridgeIpcMethods.MutationReview,
                maxResults: maxResults),
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
        CancellationToken cancellationToken = default,
        string? processName = null,
        string? manifestPath = null,
        IReadOnlyList<DiagnosticComponentOrigin>? componentOrigins = null)
    {
        if (maxSessions is < 1 or > MaxDiagnosticsSessions)
        {
            return CoreResult<DiagnosticsResponse>.Fail(new CoreError(
                CoreErrorCodes.InvalidBridgeRequest,
                $"Diagnostics session limit must be between 1 and {MaxDiagnosticsSessions}."));
        }

        var records = EnumerateDiagnosticManifests(manifestPath)
            .Where(record => MatchesDiagnosticFilters(record, processId, sessionId, processName, manifestPath))
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

        foreach (var duplicate in FindDuplicateManifestIssues(selectedRecords))
        {
            issues.Add(duplicate);
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

        var generatedAt = DateTimeOffset.UtcNow;
        var previewSessionDiagnostics = previewSessions ?? Array.Empty<PreviewSessionDiagnostic>();
        var diagnosticOrigins = componentOrigins ?? Array.Empty<DiagnosticComponentOrigin>();
        var originIssue = CreateMixedInstallRootIssue(diagnosticOrigins);
        if (originIssue is not null)
        {
            issues.Add(originIssue);
        }

        var diagnosticIssues = BuildDiagnosticIssues(
            issues,
            bridgeSessions,
            previewHost,
            previewSessionDiagnostics,
            generatedAt);

        return CoreResult<DiagnosticsResponse>.Ok(new DiagnosticsResponse(
            HealthResponse.Current(),
            generatedAt,
            ManifestDirectory,
            bridgeSessions,
            issues,
            previewHost,
            previewSessionDiagnostics,
            diagnosticIssues,
            componentOrigins: diagnosticOrigins));
    }

    public async Task<CoreResult<BridgeCleanupResponse>> CleanupBridgeManifestsAsync(
        CancellationToken cancellationToken = default)
    {
        var diagnostics = await DiagnosticsAsync(maxSessions: MaxDiagnosticsSessions, cancellationToken: cancellationToken);
        if (!diagnostics.Success)
        {
            return CoreResult<BridgeCleanupResponse>.Fail(diagnostics.Error!);
        }

        var candidates = diagnostics.Value!.BridgeSessions
            .Where(static session => session.CleanupCandidate)
            .ToArray();
        var deletedPaths = new List<string>(candidates.Length);
        var issues = new List<ProtocolError>();

        foreach (var candidate in candidates)
        {
            try
            {
                File.Delete(candidate.ManifestPath);
                deletedPaths.Add(candidate.ManifestPath);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                issues.Add(new ProtocolError(
                    CoreErrorCodes.BridgeManifestCleanupFailed,
                    $"Bridge session manifest cleanup failed: {exception.Message}",
                    new Dictionary<string, string>
                    {
                        ["manifestPath"] = candidate.ManifestPath,
                        ["status"] = candidate.Status
                    }));
            }
        }

        return CoreResult<BridgeCleanupResponse>.Ok(new BridgeCleanupResponse(
            ManifestDirectory,
            deletedPaths.Count,
            candidates,
            deletedPaths,
            issues,
            DateTimeOffset.UtcNow));
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

    private CoreResult<BridgeSessionManifest> FindSingleManifest(
        int? processId,
        SessionId? sessionId,
        string? processName = null,
        string? manifestPath = null)
    {
        if (!string.IsNullOrWhiteSpace(manifestPath))
        {
            var record = ReadDiagnosticManifest(manifestPath);
            if (record.Manifest is null)
            {
                return CoreResult<BridgeSessionManifest>.Fail(new CoreError(
                    record.Error!.Code,
                    record.Error.Message,
                    record.Error.Details));
            }

            if (!IsProcessAlive(record.Manifest.ProcessId))
            {
                return CoreResult<BridgeSessionManifest>.Fail(new CoreError(
                    CoreErrorCodes.BridgeSessionNotFound,
                    "The selected AvaScope bridge manifest belongs to a process that is not running.",
                    new Dictionary<string, string>
                    {
                        ["manifestPath"] = Path.GetFullPath(manifestPath),
                        ["processId"] = record.Manifest.ProcessId.ToString(CultureInfo.InvariantCulture),
                        ["status"] = DiagnosticStatuses.Stale,
                        ["nextAction"] = "Run bridge-session cleanup or select a manifest for a running local process."
                    }));
            }

            if (!MatchesManifestFilters(record.Manifest, processId, sessionId, processName))
            {
                return CoreResult<BridgeSessionManifest>.Fail(new CoreError(
                    CoreErrorCodes.BridgeSessionNotFound,
                    "The selected AvaScope bridge manifest did not match the requested filters.",
                    BuildSelectionDetails(processId, sessionId, processName, manifestPath)));
            }

            return CoreResult<BridgeSessionManifest>.Ok(record.Manifest);
        }

        var matches = ListSessionManifests()
            .Where(manifest => MatchesManifestFilters(manifest, processId, sessionId, processName))
            .ToArray();

        return matches.Length switch
        {
            0 => CoreResult<BridgeSessionManifest>.Fail(new CoreError(
                CoreErrorCodes.BridgeSessionNotFound,
                "No active AvaScope bridge session matched the requested filters.",
                BuildSelectionDetails(processId, sessionId, processName, manifestPath))),
            1 => CoreResult<BridgeSessionManifest>.Ok(matches[0]),
            _ => CoreResult<BridgeSessionManifest>.Fail(new CoreError(
                CoreErrorCodes.MultipleBridgeSessions,
                "Multiple active AvaScope bridge sessions matched the requested filters. Specify a session id, process id, process name, or manifest path.",
                BuildMultipleMatchDetails(matches)))
        };
    }

    private CoreResult<BridgeSessionManifest> FindLatestManifest(int? processId, string? processName)
    {
        var matches = ListSessionManifests()
            .Where(manifest => MatchesManifestFilters(manifest, processId, null, processName))
            .OrderByDescending(static manifest => manifest.CreatedAt)
            .ThenByDescending(static manifest => manifest.ProcessId)
            .ToArray();

        if (matches.Length == 0)
        {
            return CoreResult<BridgeSessionManifest>.Fail(new CoreError(
                CoreErrorCodes.BridgeSessionNotFound,
                "No active AvaScope bridge session matched the latest-session filters.",
                BuildLatestSelectionDetails(processId, processName)));
        }

        if (matches.Length > 1 && matches[0].CreatedAt == matches[1].CreatedAt)
        {
            return CoreResult<BridgeSessionManifest>.Fail(new CoreError(
                CoreErrorCodes.MultipleBridgeSessions,
                "Multiple active AvaScope bridge sessions are equally latest for the requested filters.",
                BuildMultipleMatchDetails(matches.Where(match => match.CreatedAt == matches[0].CreatedAt).ToArray())));
        }

        return CoreResult<BridgeSessionManifest>.Ok(matches[0]);
    }

    private async Task<BridgeSessionDiagnostic> CreateDiagnosticAsync(
        DiagnosticManifestRecord record,
        CancellationToken cancellationToken)
    {
        if (record.Manifest is null)
        {
            var status = record.Error?.Code == CoreErrorCodes.BridgeManifestUnauthorized
                ? DiagnosticStatuses.Unauthorized
                : DiagnosticStatuses.Invalid;
            return new BridgeSessionDiagnostic(
                status,
                record.Path,
                error: record.Error,
                cleanupCandidate: status == DiagnosticStatuses.Invalid);
        }

        var manifest = record.Manifest;
        var processName = GetProcessName(manifest.ProcessId) ?? manifest.ProcessName;
        var checkedAt = DateTimeOffset.UtcNow;
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
                    "The process recorded by the AvaScope bridge manifest is not running.",
                    new Dictionary<string, string>
                    {
                        ["nextAction"] = "Run bridge-session cleanup or ignore this stale local manifest."
                    }),
                processName: processName,
                checkedAt: checkedAt,
                cleanupCandidate: true);
        }

        var requestId = NewRequestId();
        var healthResult = await SendAsync<HealthResponse>(
            manifest,
            new BridgeIpcRequest(requestId, BridgeIpcMethods.Health),
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
                error: ToProtocolError(healthResult.Error!),
                processName: processName,
                checkedAt: checkedAt,
                requestId: requestId);
        }

        var compatibility = ValidateProtocolCompatibility(healthResult.Value!, manifest);
        if (!compatibility.Success)
        {
            return new BridgeSessionDiagnostic(
                DiagnosticStatuses.Incompatible,
                record.Path,
                ToSessionSummary(manifest, SessionStates.Failed),
                manifest.ProcessId,
                DiagnosticTransportKinds.NamedPipe,
                manifest.PipeName,
                healthResult.Value,
                ToProtocolError(compatibility.Error!),
                processName,
                checkedAt,
                requestId);
        }

        return new BridgeSessionDiagnostic(
            DiagnosticStatuses.Available,
            record.Path,
            ToSessionSummary(manifest),
            manifest.ProcessId,
            DiagnosticTransportKinds.NamedPipe,
            manifest.PipeName,
            healthResult.Value,
            processName: processName,
            checkedAt: checkedAt,
            requestId: requestId);
    }

    private static IReadOnlyList<DiagnosticIssue> BuildDiagnosticIssues(
        IReadOnlyList<ProtocolError> issues,
        IReadOnlyList<BridgeSessionDiagnostic> bridgeSessions,
        PreviewHostDiagnostic? previewHost,
        IReadOnlyList<PreviewSessionDiagnostic> previewSessions,
        DateTimeOffset observedAt)
    {
        var diagnosticIssues = new List<DiagnosticIssue>();

        foreach (var issue in issues)
        {
            AddDiagnosticIssue(
                diagnosticIssues,
                new DiagnosticIssue(
                    DiagnosticIssueSources.Diagnostics,
                    IssueSeverityForError(issue.Code),
                    IssueStatusForError(issue.Code),
                    issue.Code,
                    issue.Message,
                    "diagnostics_summary",
                    observedAt,
                    details: issue.Details));
        }

        foreach (var bridgeSession in bridgeSessions)
        {
            if (bridgeSession.Error is null)
            {
                continue;
            }

            AddDiagnosticIssue(
                diagnosticIssues,
                new DiagnosticIssue(
                    DiagnosticIssueSources.BridgeSession,
                    IssueSeverityForStatus(bridgeSession.Status),
                    bridgeSession.Status,
                    bridgeSession.Error.Code,
                    bridgeSession.Error.Message,
                    "bridge_session_manifest",
                    observedAt,
                    bridgeSession.Session?.SessionId.Value,
                    bridgeSession.ProcessId,
                    bridgeSession.ManifestPath,
                    bridgeSession.Error.Details));
        }

        if (previewHost?.Error is not null)
        {
            AddDiagnosticIssue(
                diagnosticIssues,
                new DiagnosticIssue(
                    DiagnosticIssueSources.PreviewHost,
                    IssueSeverityForStatus(previewHost.Status),
                    previewHost.Status,
                    previewHost.Error.Code,
                    previewHost.Error.Message,
                    "preview_host_assembly_probe",
                    observedAt,
                    path: previewHost.HostAssemblyPath,
                    details: previewHost.Error.Details));
        }

        foreach (var previewSession in previewSessions)
        {
            if (previewSession.Error is null)
            {
                continue;
            }

            AddDiagnosticIssue(
                diagnosticIssues,
                new DiagnosticIssue(
                    DiagnosticIssueSources.PreviewSession,
                    IssueSeverityForStatus(previewSession.Status),
                    previewSession.Status,
                    previewSession.Error.Code,
                    previewSession.Error.Message,
                    "preview_session_store_record",
                    observedAt,
                    previewSession.Session?.SessionId.Value,
                    path: previewSession.RecordPath,
                    details: previewSession.Error.Details));
        }

        return diagnosticIssues;
    }

    private static ProtocolError? CreateMixedInstallRootIssue(IReadOnlyList<DiagnosticComponentOrigin> componentOrigins)
    {
        var roots = componentOrigins
            .Where(static origin => origin.Exists)
            .Select(static origin => origin.RootDirectory)
            .Where(static root => !string.IsNullOrWhiteSpace(root))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (roots.Length <= 1)
        {
            return null;
        }

        var details = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["rootDirectories"] = string.Join("|", roots),
            ["components"] = string.Join("|", componentOrigins.Select(static origin =>
                $"{origin.Component}:{origin.OriginKind}:{origin.RootDirectory}")),
            ["nextAction"] = "Confirm whether CLI, MCP server, and PreviewHost should come from different roots before comparing diagnostics or versions."
        };

        return new ProtocolError(
            CoreErrorCodes.DiagnosticsMixedInstallRoots,
            "Diagnostics found AvaScope components resolved from multiple roots.",
            details);
    }

    private static void AddDiagnosticIssue(List<DiagnosticIssue> issues, DiagnosticIssue issue)
    {
        if (issues.Count < MaxDiagnosticIssues)
        {
            issues.Add(issue);
        }
    }

    private static string IssueSeverityForError(string code)
    {
        return code is CoreErrorCodes.DiagnosticsTruncated
            or CoreErrorCodes.BridgeSessionNotFound
            or CoreErrorCodes.DiagnosticsMixedInstallRoots
            ? DiagnosticIssueSeverities.Warning
            : DiagnosticIssueSeverities.Error;
    }

    private static string IssueStatusForError(string code)
    {
        return code is CoreErrorCodes.DiagnosticsTruncated
            or CoreErrorCodes.DiagnosticsMixedInstallRoots
            ? DiagnosticStatuses.Available
            : DiagnosticStatuses.Unavailable;
    }

    private static string IssueSeverityForStatus(string status)
    {
        return status switch
        {
            DiagnosticStatuses.Stale => DiagnosticIssueSeverities.Warning,
            DiagnosticStatuses.Available => DiagnosticIssueSeverities.Info,
            _ => DiagnosticIssueSeverities.Error
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
                    "Bridge IPC response request id did not match the request.",
                    CreateRequestDetails(manifest, request)));
            }

            if (!response.Success)
            {
                return CoreResult<T>.Fail(new CoreError(
                    response.Error!.Code,
                    response.Error.Message,
                    CreateRequestDetails(manifest, request, response.Error.Details)));
            }

            var value = response.GetValue<T>();
            return value is null
                ? CoreResult<T>.Fail(new CoreError(
                    CoreErrorCodes.BridgeIpcFailed,
                    "Bridge IPC response value was empty.",
                    CreateRequestDetails(manifest, request)))
                : CoreResult<T>.Ok(value);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return CoreResult<T>.Fail(new CoreError(
                CoreErrorCodes.BridgeIpcUnavailable,
                "Bridge IPC request timed out.",
                CreateRequestDetails(manifest, request)));
        }
        catch (Exception exception) when (exception is IOException or TimeoutException or JsonException or InvalidOperationException or ObjectDisposedException)
        {
            return CoreResult<T>.Fail(new CoreError(
                CoreErrorCodes.BridgeIpcUnavailable,
                exception.Message,
                CreateRequestDetails(manifest, request)));
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

    private IReadOnlyList<DiagnosticManifestRecord> EnumerateDiagnosticManifests(string? manifestPath = null)
    {
        if (!string.IsNullOrWhiteSpace(manifestPath))
        {
            return [ReadDiagnosticManifest(manifestPath)];
        }

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
        catch (UnauthorizedAccessException exception)
        {
            return new DiagnosticManifestRecord(
                fullPath,
                null,
                new ProtocolError(
                    CoreErrorCodes.BridgeManifestUnauthorized,
                    $"Bridge session manifest could not be read due to local access permissions: {exception.Message}",
                    new Dictionary<string, string>
                    {
                        ["manifestPath"] = fullPath
                    }));
        }
        catch (Exception exception) when (exception is IOException or JsonException or ArgumentException)
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
        DiagnosticManifestRecord record,
        int? processId,
        SessionId? sessionId,
        string? processName,
        string? manifestPath)
    {
        if (!string.IsNullOrWhiteSpace(manifestPath)
            && !string.Equals(Path.GetFullPath(manifestPath), record.Path, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var manifest = record.Manifest;
        if (manifest is null)
        {
            return processId is null
                && sessionId is null
                && string.IsNullOrWhiteSpace(processName);
        }

        return MatchesManifestFilters(manifest, processId, sessionId, processName);
    }

    private static bool MatchesManifestFilters(
        BridgeSessionManifest manifest,
        int? processId,
        SessionId? sessionId,
        string? processName)
    {
        return (processId is null || manifest.ProcessId == processId.Value)
            && (sessionId is null || manifest.SessionId == sessionId)
            && MatchesProcessName(manifest, processName);
    }

    private static bool MatchesProcessName(BridgeSessionManifest manifest, string? processName)
    {
        if (string.IsNullOrWhiteSpace(processName))
        {
            return true;
        }

        var normalizedFilter = NormalizeProcessName(processName);
        var actualProcessName = GetProcessName(manifest.ProcessId) ?? manifest.ProcessName;

        return !string.IsNullOrWhiteSpace(actualProcessName)
            && string.Equals(NormalizeProcessName(actualProcessName), normalizedFilter, StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeProcessName(string processName)
    {
        var normalized = processName.Trim();
        return normalized.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
            ? normalized[..^4]
            : normalized;
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

    private static string? GetProcessName(int processId)
    {
        try
        {
            using var process = Process.GetProcessById(processId);
            return process.HasExited ? null : process.ProcessName;
        }
        catch (ArgumentException)
        {
            return null;
        }
        catch (InvalidOperationException)
        {
            return null;
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

    private static CoreResult<HealthResponse> ValidateProtocolCompatibility(
        HealthResponse health,
        BridgeSessionManifest manifest)
    {
        if (health.ProtocolVersion.Major == AvaScopeProtocol.CurrentVersion.Major)
        {
            return CoreResult<HealthResponse>.Ok(health);
        }

        return CoreResult<HealthResponse>.Fail(new CoreError(
            CoreErrorCodes.BridgeProtocolIncompatible,
            "AvaScope bridge protocol major version is not compatible with this client.",
            new Dictionary<string, string>
            {
                ["expectedProtocolMajor"] = AvaScopeProtocol.CurrentVersion.Major.ToString(CultureInfo.InvariantCulture),
                ["actualProtocolMajor"] = health.ProtocolVersion.Major.ToString(CultureInfo.InvariantCulture),
                ["actualProtocolVersion"] = health.ProtocolVersion.ToString(),
                ["sessionId"] = manifest.SessionId.Value,
                ["processId"] = manifest.ProcessId.ToString(CultureInfo.InvariantCulture),
                ["nextAction"] = "Use matching AvaScope package versions in the inspected application and the CLI/MCP client."
            }));
    }

    private static IReadOnlyList<ProtocolError> FindDuplicateManifestIssues(
        IReadOnlyList<DiagnosticManifestRecord> records)
    {
        return records
            .Where(static record => record.Manifest is not null)
            .GroupBy(static record => record.Manifest!.SessionId)
            .Where(static group => group.Count() > 1)
            .Select(static group => new ProtocolError(
                CoreErrorCodes.BridgeManifestDuplicate,
                $"Multiple AvaScope bridge manifests use session id '{group.Key.Value}'.",
                new Dictionary<string, string>
                {
                    ["sessionId"] = group.Key.Value,
                    ["manifestPaths"] = string.Join(Path.PathSeparator, group.Select(static record => record.Path)),
                    ["nextAction"] = "Run bridge-session cleanup and attach using an explicit manifest path if duplicates remain."
                }))
            .ToArray();
    }

    private static IReadOnlyDictionary<string, string> BuildSelectionDetails(
        int? processId,
        SessionId? sessionId,
        string? processName,
        string? manifestPath)
    {
        var details = new Dictionary<string, string>
        {
            ["nextAction"] = "Run diagnostics to list available bridge manifests, then retry with a session id, process id, process name, or manifest path."
        };

        if (processId is not null)
        {
            details["processId"] = processId.Value.ToString(CultureInfo.InvariantCulture);
        }

        if (sessionId is not null)
        {
            details["sessionId"] = sessionId.Value;
        }

        if (!string.IsNullOrWhiteSpace(processName))
        {
            details["processName"] = processName.Trim();
        }

        if (!string.IsNullOrWhiteSpace(manifestPath))
        {
            details["manifestPath"] = Path.GetFullPath(manifestPath);
        }

        return details;
    }

    private static IReadOnlyDictionary<string, string> BuildMultipleMatchDetails(
        IReadOnlyList<BridgeSessionManifest> matches)
    {
        return new Dictionary<string, string>
        {
            ["matchedSessions"] = string.Join(",", matches.Select(static manifest => manifest.SessionId.Value)),
            ["matchedProcesses"] = string.Join(",", matches.Select(static manifest => manifest.ProcessId.ToString(CultureInfo.InvariantCulture))),
            ["nextAction"] = "Retry with --session or --manifest to select one bridge session deterministically."
        };
    }

    private static IReadOnlyDictionary<string, string> BuildLatestSelectionDetails(
        int? processId,
        string? processName)
    {
        var details = new Dictionary<string, string>
        {
            ["selectionMode"] = "latest",
            ["nextAction"] = "Run diagnostics to list active bridge manifests, then retry with a matching process id or process name."
        };

        if (processId is not null)
        {
            details["processId"] = processId.Value.ToString(CultureInfo.InvariantCulture);
        }

        if (!string.IsNullOrWhiteSpace(processName))
        {
            details["processName"] = processName.Trim();
        }

        return details;
    }

    private string ResolveManifestPath(BridgeSessionManifest manifest, string? manifestPath)
    {
        return string.IsNullOrWhiteSpace(manifestPath)
            ? Path.Combine(ManifestDirectory, $"{manifest.SessionId.Value}.json")
            : Path.GetFullPath(manifestPath);
    }

    private static IReadOnlyDictionary<string, string> CreateRequestDetails(
        BridgeSessionManifest manifest,
        BridgeIpcRequest request,
        IReadOnlyDictionary<string, string>? existingDetails = null)
    {
        var details = existingDetails is null
            ? new Dictionary<string, string>(StringComparer.Ordinal)
            : new Dictionary<string, string>(existingDetails, StringComparer.Ordinal);

        details["requestId"] = request.RequestId;
        details["method"] = request.Method;
        details["sessionId"] = manifest.SessionId.Value;
        details["processId"] = manifest.ProcessId.ToString(CultureInfo.InvariantCulture);
        details["pipeName"] = manifest.PipeName;

        if (!string.IsNullOrWhiteSpace(request.TopLevelId))
        {
            details["topLevelId"] = request.TopLevelId;
        }

        if (!string.IsNullOrWhiteSpace(request.TreeKind))
        {
            details["treeKind"] = request.TreeKind;
        }

        if (!string.IsNullOrWhiteSpace(request.NodeId))
        {
            details["nodeId"] = request.NodeId;
        }

        if (!string.IsNullOrWhiteSpace(request.TargetNodeId))
        {
            details["targetNodeId"] = request.TargetNodeId;
        }

        if (request.Mutation is { } mutation)
        {
            details["mutationRequestId"] = mutation.RequestId;
            details["mutationOperation"] = mutation.Operation.Kind;
            details["mutationTargetTopLevelId"] = mutation.Target.TopLevelId;

            if (!string.IsNullOrWhiteSpace(mutation.Target.NodeId))
            {
                details["mutationTargetNodeId"] = mutation.Target.NodeId;
            }

            if (!string.IsNullOrWhiteSpace(mutation.Operation.PropertyName))
            {
                details["mutationPropertyName"] = mutation.Operation.PropertyName;
            }

            if (!string.IsNullOrWhiteSpace(mutation.Operation.ClassName))
            {
                details["mutationClassName"] = mutation.Operation.ClassName;
            }

            if (!string.IsNullOrWhiteSpace(mutation.Operation.ResourceKey))
            {
                details["mutationResourceKey"] = mutation.Operation.ResourceKey;
            }
        }

        return details;
    }

    private static CoreResult<RuntimeMutationResponse> RuntimeMutationUnavailable(
        SessionId sessionId,
        RuntimeMutationRequest request,
        ProtocolError diagnostic)
    {
        return CoreResult<RuntimeMutationResponse>.Ok(new RuntimeMutationResponse(
            request.RequestId,
            $"mutation:unavailable:{request.RequestId}",
            sessionId,
            request.Target.TopLevelId,
            request.Target,
            request.Operation,
            RuntimeMutationStatuses.Unavailable,
            applied: false,
            DateTimeOffset.UtcNow,
            RuntimeMutationCapabilityCatalog.ContractOnly(),
            [diagnostic]));
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
