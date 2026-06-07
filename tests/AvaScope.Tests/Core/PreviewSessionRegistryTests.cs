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
    public async Task ReloadAsyncRerendersExistingPreviewSession()
    {
        Directory.CreateDirectory(_testRoot);
        var createdAt = new DateTimeOffset(2026, 6, 7, 5, 30, 0, TimeSpan.Zero);
        var timeProvider = new ManualTimeProvider(createdAt);
        var sessionRegistry = new SessionRegistry(timeProvider);
        var previewHost = new PreviewHostClient(Path.Combine(AppContext.BaseDirectory, "AvaScope.PreviewHost.dll"));
        var previewSessions = new PreviewSessionRegistry(sessionRegistry, previewHost, timeProvider);
        var viewPath = Path.Combine(_testRoot, "ReloadView.axaml");
        var outputPath = Path.Combine(_testRoot, "reload.png");

        await File.WriteAllTextAsync(viewPath, """
            <UserControl xmlns="https://github.com/avaloniaui">
              <Border Background="#FFFFFFFF">
                <TextBlock Text="Initial preview session reload smoke" />
              </Border>
            </UserControl>
            """);

        var request = new PreviewRequest(
            outputPath,
            width: 300,
            height: 160,
            dpi: 96,
            viewPath: viewPath);
        var created = await previewSessions.CreateAsync(request);
        timeProvider.UtcNow = createdAt.AddMinutes(1);

        var reloaded = await previewSessions.ReloadAsync(created.Value!.Session.SessionId);

        Assert.True(reloaded.Success, reloaded.Error?.Message);
        Assert.Equal(created.Value.Session.SessionId, reloaded.Value!.Session.SessionId);
        Assert.Equal(SessionStates.Active, reloaded.Value.Session.State);
        Assert.True(reloaded.Value.LastRender.Success, reloaded.Value.LastRender.Error?.Message);
        Assert.Equal(Path.GetFullPath(outputPath), reloaded.Value.LastRender.Value!.FilePath);
        Assert.Equal(timeProvider.UtcNow, reloaded.Value.UpdatedAt);
        Assert.Single(previewSessions.List());
        Assert.Single(sessionRegistry.List());
    }

    [Fact]
    public async Task ReloadAsyncStoresFailedRenderAndKeepsSession()
    {
        var createdAt = new DateTimeOffset(2026, 6, 7, 6, 0, 0, TimeSpan.Zero);
        var timeProvider = new ManualTimeProvider(createdAt);
        var sessionRegistry = new SessionRegistry(timeProvider);
        var missingHost = new PreviewHostClient(Path.Combine(_testRoot, "missing-host.dll"));
        var previewSessions = new PreviewSessionRegistry(sessionRegistry, missingHost, timeProvider);
        var request = new PreviewRequest(
            Path.Combine(_testRoot, "preview.png"),
            width: 100,
            height: 100,
            dpi: 96);
        var created = await previewSessions.CreateAsync(request);
        timeProvider.UtcNow = createdAt.AddMinutes(1);

        var reloaded = await previewSessions.ReloadAsync(created.Value!.Session.SessionId);

        Assert.True(reloaded.Success, reloaded.Error?.Message);
        Assert.Equal(created.Value.Session.SessionId, reloaded.Value!.Session.SessionId);
        Assert.Equal(SessionStates.Failed, reloaded.Value.Session.State);
        Assert.False(reloaded.Value.LastRender.Success);
        Assert.Equal(CoreErrorCodes.PreviewHostUnavailable, reloaded.Value.LastRender.Error!.Code);
        Assert.Equal(timeProvider.UtcNow, reloaded.Value.UpdatedAt);
        Assert.Equal(SessionLifecycleState.Failed, sessionRegistry.Get(created.Value.Session.SessionId).Value!.State);
    }

    [Fact]
    public async Task ReloadAsyncRejectsClosedPreviewSession()
    {
        var sessionRegistry = new SessionRegistry();
        var missingHost = new PreviewHostClient(Path.Combine(_testRoot, "missing-host.dll"));
        var previewSessions = new PreviewSessionRegistry(sessionRegistry, missingHost);
        var created = await previewSessions.CreateAsync(new PreviewRequest(
            Path.Combine(_testRoot, "preview.png"),
            width: 100,
            height: 100,
            dpi: 96));
        previewSessions.Close(created.Value!.Session.SessionId);

        var reloaded = await previewSessions.ReloadAsync(created.Value.Session.SessionId);

        Assert.False(reloaded.Success);
        Assert.Equal(CoreErrorCodes.SessionClosed, reloaded.Error!.Code);
    }

    [Fact]
    public async Task PreviewSessionsPersistAndRestoreFromStore()
    {
        var createdAt = new DateTimeOffset(2026, 6, 7, 7, 0, 0, TimeSpan.Zero);
        var timeProvider = new ManualTimeProvider(createdAt);
        var store = new PreviewSessionStore(Path.Combine(_testRoot, "store"));
        var missingHost = new PreviewHostClient(Path.Combine(_testRoot, "missing-host.dll"));
        var firstRegistry = new PreviewSessionRegistry(
            new SessionRegistry(timeProvider),
            missingHost,
            timeProvider,
            store);
        var request = new PreviewRequest(
            Path.Combine(_testRoot, "preview.png"),
            width: 100,
            height: 100,
            dpi: 96,
            viewPath: Path.Combine(_testRoot, "StoredView.axaml"));

        var created = await firstRegistry.CreateAsync(request, "Stored preview");

        Assert.True(created.Success, created.Error?.Message);
        Assert.Equal(SessionStates.Failed, created.Value!.Session.State);
        Assert.False(created.Value.LastRender.Success);
        Assert.True(Directory.EnumerateFiles(store.Directory, "*.json").Any());

        var restoredSessionRegistry = new SessionRegistry(timeProvider);
        var restoredRegistry = new PreviewSessionRegistry(
            restoredSessionRegistry,
            missingHost,
            timeProvider,
            store);

        var restored = Assert.Single(restoredRegistry.List());
        Assert.Equal(created.Value.Session.SessionId, restored.Session.SessionId);
        Assert.Equal("Stored preview", restored.Session.DisplayName);
        Assert.Equal(SessionStates.Failed, restored.Session.State);
        Assert.Equal(Path.GetFullPath(request.OutputPath), Path.GetFullPath(restored.Request.OutputPath));
        Assert.False(restored.LastRender.Success);
        Assert.Equal(CoreErrorCodes.PreviewHostUnavailable, restored.LastRender.Error!.Code);
        Assert.Equal(SessionLifecycleState.Failed, restoredSessionRegistry.Get(restored.Session.SessionId).Value!.State);
    }

    [Fact]
    public async Task ClosedPreviewSessionStatePersistsAcrossRegistryInstances()
    {
        var timeProvider = new ManualTimeProvider(new DateTimeOffset(2026, 6, 7, 7, 30, 0, TimeSpan.Zero));
        var store = new PreviewSessionStore(Path.Combine(_testRoot, "closed-store"));
        var missingHost = new PreviewHostClient(Path.Combine(_testRoot, "missing-host.dll"));
        var firstSessionRegistry = new SessionRegistry(timeProvider);
        var firstRegistry = new PreviewSessionRegistry(
            firstSessionRegistry,
            missingHost,
            timeProvider,
            store);
        var created = await firstRegistry.CreateAsync(new PreviewRequest(
            Path.Combine(_testRoot, "preview.png"),
            width: 100,
            height: 100,
            dpi: 96));
        timeProvider.UtcNow = timeProvider.UtcNow.AddMinutes(1);

        var closed = firstRegistry.Close(created.Value!.Session.SessionId);

        Assert.True(closed.Success, closed.Error?.Message);
        Assert.Equal(SessionStates.Closed, closed.Value!.Session.State);

        var restoredSessionRegistry = new SessionRegistry(timeProvider);
        var restoredRegistry = new PreviewSessionRegistry(
            restoredSessionRegistry,
            missingHost,
            timeProvider,
            store);

        var restored = Assert.Single(restoredRegistry.List());
        Assert.Equal(SessionStates.Closed, restored.Session.State);
        Assert.Equal(SessionLifecycleState.Closed, restoredSessionRegistry.Get(restored.Session.SessionId).Value!.State);

        var reloaded = await restoredRegistry.ReloadAsync(restored.Session.SessionId);

        Assert.False(reloaded.Success);
        Assert.Equal(CoreErrorCodes.SessionClosed, reloaded.Error!.Code);
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
