using AvaScope.Core;
using AvaScope.Protocol;

namespace AvaScope.Tests.Core;

public sealed class PreviewSessionRegistryTests : IDisposable
{
    private readonly string _testRoot = Path.Combine(
        Path.GetTempPath(),
        "AvaScope.Tests",
        $"preview-sessions-{Guid.NewGuid():N}");

    public void Dispose()
    {
        if (Directory.Exists(_testRoot))
        {
            Directory.Delete(_testRoot, recursive: true);
        }
    }

    [Fact]
    public async Task CreateAsyncRendersAndRegistersPreviewSession()
    {
        Directory.CreateDirectory(_testRoot);
        var createdAt = new DateTimeOffset(2026, 6, 7, 4, 30, 0, TimeSpan.Zero);
        var timeProvider = new ManualTimeProvider(createdAt);
        var sessionRegistry = new SessionRegistry(timeProvider);
        var previewHost = new PreviewHostClient(Path.Combine(AppContext.BaseDirectory, "AvaScope.PreviewHost.dll"));
        var previewSessions = new PreviewSessionRegistry(sessionRegistry, previewHost, timeProvider);
        var viewPath = Path.Combine(_testRoot, "PreviewSessionView.axaml");
        var outputPath = Path.Combine(_testRoot, "preview.png");

        await File.WriteAllTextAsync(viewPath, """
            <UserControl xmlns="https://github.com/avaloniaui">
              <Border Background="#FFFFFFFF">
                <TextBlock Text="Preview session registry smoke" />
              </Border>
            </UserControl>
            """);

        var request = new PreviewRequest(
            outputPath,
            width: 320,
            height: 180,
            dpi: 96,
            viewPath: viewPath,
            themeVariant: "light");

        var result = await previewSessions.CreateAsync(request);

        Assert.True(result.Success, result.Error?.Message);
        Assert.Equal(SessionKinds.Preview, result.Value!.Session.Kind);
        Assert.Equal(SessionStates.Active, result.Value.Session.State);
        Assert.Equal(Path.GetFullPath(viewPath), Path.GetFullPath(result.Value.Session.DisplayName!));
        Assert.Same(request, result.Value.Request);
        Assert.True(result.Value.LastRender.Success, result.Value.LastRender.Error?.Message);
        Assert.Equal(Path.GetFullPath(outputPath), result.Value.LastRender.Value!.FilePath);
        Assert.Equal(createdAt, result.Value.UpdatedAt);
        Assert.True(File.Exists(outputPath));

        var listed = Assert.Single(previewSessions.List());
        Assert.Equal(result.Value.Session.SessionId, listed.Session.SessionId);
        var coreSession = Assert.Single(sessionRegistry.List());
        Assert.Equal(SessionKinds.Preview, coreSession.Kind);

        timeProvider.UtcNow = createdAt.AddMinutes(1);
        var closed = previewSessions.Close(result.Value.Session.SessionId);

        Assert.True(closed.Success, closed.Error?.Message);
        Assert.Equal(SessionStates.Closed, closed.Value!.Session.State);
        Assert.Equal(timeProvider.UtcNow, closed.Value.UpdatedAt);
        Assert.Equal(SessionLifecycleState.Closed, sessionRegistry.Get(result.Value.Session.SessionId).Value!.State);
    }

    [Fact]
    public async Task CreateAsyncStoresFailedRenderForMissingPreviewHost()
    {
        var timeProvider = new ManualTimeProvider(new DateTimeOffset(2026, 6, 7, 5, 0, 0, TimeSpan.Zero));
        var sessionRegistry = new SessionRegistry(timeProvider);
        var missingHost = new PreviewHostClient(Path.Combine(_testRoot, "missing-host.dll"));
        var previewSessions = new PreviewSessionRegistry(sessionRegistry, missingHost, timeProvider);
        var request = new PreviewRequest(
            Path.Combine(_testRoot, "preview.png"),
            width: 100,
            height: 100,
            dpi: 96);

        var result = await previewSessions.CreateAsync(request, "Broken preview");

        Assert.True(result.Success, result.Error?.Message);
        Assert.Equal(SessionStates.Failed, result.Value!.Session.State);
        Assert.Equal("Broken preview", result.Value.Session.DisplayName);
        Assert.False(result.Value.LastRender.Success);
        Assert.Equal(CoreErrorCodes.PreviewHostUnavailable, result.Value.LastRender.Error!.Code);
        Assert.Equal(SessionLifecycleState.Failed, sessionRegistry.Get(result.Value.Session.SessionId).Value!.State);
    }

    [Fact]
    public void CloseReturnsStructuredErrorForUnknownPreviewSession()
    {
        var previewSessions = new PreviewSessionRegistry(
            new SessionRegistry(),
            new PreviewHostClient(Path.Combine(_testRoot, "missing-host.dll")));

        var result = previewSessions.Close(new SessionId("missing"));

        Assert.False(result.Success);
        Assert.Equal(CoreErrorCodes.SessionNotFound, result.Error!.Code);
    }

    private sealed class ManualTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public DateTimeOffset UtcNow { get; set; } = utcNow;

        public override DateTimeOffset GetUtcNow() => UtcNow;
    }
}
