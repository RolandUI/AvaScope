using System.Diagnostics;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using AvaScope.Core;
using AvaScope.Protocol;

namespace AvaScope.Tests.Core;

public sealed class LocalBridgeClientTests : IDisposable
{
    private readonly string _manifestDirectory = Path.Combine(
        Path.GetTempPath(),
        "AvaScope.Tests",
        $"manifests-{Guid.NewGuid():N}");

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
        var pipeName = $"avascope-core-test-{Guid.NewGuid():N}";
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
            request => BridgeIpcResponse.Ok(request.RequestId, HealthResponse.Current()));
        var client = new LocalBridgeClient(Path.Combine(_manifestDirectory, "unused"));

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
                $"avascope-core-old-{Guid.NewGuid():N}",
                DateTimeOffset.UtcNow.AddMinutes(-5),
                "Old app",
                processName: processName));
        var pipeName = $"avascope-core-new-{Guid.NewGuid():N}";
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
        var client = new LocalBridgeClient(_manifestDirectory);

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
    public async Task CloseSessionReturnsStructuredErrorWhenNoManifestMatches()
    {
        var client = new LocalBridgeClient(_manifestDirectory);

        var result = await client.CloseSessionAsync(new SessionId("missing"));

        Assert.False(result.Success);
        Assert.Equal(CoreErrorCodes.BridgeSessionNotFound, result.Error!.Code);
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
        var pipeName = $"avascope-core-test-{Guid.NewGuid():N}";
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
        var client = new LocalBridgeClient(_manifestDirectory, TimeSpan.FromMilliseconds(100));

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
