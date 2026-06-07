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
}
