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
        File.WriteAllText(staleManifestPath, JsonSerializer.Serialize(staleManifest), Encoding.UTF8);
        File.WriteAllText(invalidManifestPath, "{", Encoding.UTF8);

        var client = new LocalBridgeClient(_manifestDirectory, TimeSpan.FromMilliseconds(100));

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
            });
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
    }
}
