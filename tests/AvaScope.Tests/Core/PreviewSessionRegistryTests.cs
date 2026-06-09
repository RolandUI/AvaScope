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
    public void PreviewSessionStoreDiagnosticsAndCleanupRemoveOnlyStaleRecords()
    {
        var store = new PreviewSessionStore(Path.Combine(_testRoot, "store"));
        var staleSession = new PreviewSessionSummary(
            new SessionSummary(
                new SessionId("preview-stale"),
                SessionKinds.Preview,
                SessionStates.Failed,
                DateTimeOffset.UnixEpoch,
                "Stale preview"),
            new PreviewRequest(
                Path.Combine(_testRoot, "missing.png"),
                width: 100,
                height: 100,
                dpi: 96),
            ToolResult<PreviewResponse>.Fail(new ProtocolError("preview_render_failed", "Render failed.")),
            DateTimeOffset.UnixEpoch);
        var activeOutput = Path.Combine(_testRoot, "active.png");
        Directory.CreateDirectory(_testRoot);
        File.WriteAllBytes(activeOutput, [1, 2, 3]);
        var activeSession = new PreviewSessionSummary(
            new SessionSummary(
                new SessionId("preview-active"),
                SessionKinds.Preview,
                SessionStates.Active,
                DateTimeOffset.UnixEpoch,
                "Active preview"),
            new PreviewRequest(
                activeOutput,
                width: 100,
                height: 100,
                dpi: 96),
            ToolResult<PreviewResponse>.Ok(new PreviewResponse(
                activeOutput,
                100,
                100,
                96,
                DateTimeOffset.UnixEpoch)),
            DateTimeOffset.UnixEpoch);

        Assert.True(store.Save(staleSession).Success);
        Assert.True(store.Save(activeSession).Success);

        var diagnostics = store.GetDiagnostics();
        Assert.Contains(diagnostics, static diagnostic => diagnostic.Status == DiagnosticStatuses.Stale);
        Assert.Contains(diagnostics, static diagnostic => diagnostic.Status == DiagnosticStatuses.Available);

        var cleanup = store.CleanupStale();

        Assert.True(cleanup.Success, cleanup.Error?.Message);
        Assert.Equal(1, cleanup.Value!.DeletedPreviewSessionRecords);
        var remaining = Assert.Single(store.Load());
        Assert.Equal("preview-active", remaining.Session.SessionId.Value);
    }

    [Fact]
    public void PreviewViewerExporterWritesSelfContainedHtmlWithFileBackedPreviewUrl()
    {
        Directory.CreateDirectory(_testRoot);
        var generatedAt = new DateTimeOffset(2026, 6, 9, 14, 0, 0, TimeSpan.Zero);
        var timeProvider = new ManualTimeProvider(generatedAt);
        var imagePath = Path.Combine(_testRoot, "preview.png");
        var viewerPath = Path.Combine(_testRoot, "viewer.html");
        File.WriteAllBytes(imagePath, [1, 2, 3]);
        var session = new PreviewSessionSummary(
            new SessionSummary(
                new SessionId("preview-viewer"),
                SessionKinds.Preview,
                SessionStates.Active,
                DateTimeOffset.UnixEpoch,
                "Viewer preview"),
            new PreviewRequest(
                imagePath,
                width: 100,
                height: 80,
                dpi: 96,
                viewPath: "Views/MainView.axaml"),
            ToolResult<PreviewResponse>.Ok(new PreviewResponse(
                imagePath,
                100,
                80,
                96,
                DateTimeOffset.UnixEpoch,
                viewPath: "Views/MainView.axaml",
                diagnostics:
                [
                    new PreviewDiagnostic(
                        PreviewDiagnosticSeverities.Warning,
                        PreviewDiagnosticCategories.Binding,
                        "binding_warning",
                        "Binding warning.",
                        nodeId: "visual:text")
                ])),
            DateTimeOffset.UnixEpoch);

        var result = new PreviewViewerExporter(timeProvider).Export(session, viewerPath);

        Assert.True(result.Success, result.Error?.Message);
        Assert.Equal(Path.GetFullPath(viewerPath), result.Value!.ViewerPath);
        Assert.Equal(new Uri(viewerPath).AbsoluteUri, result.Value.PreviewUrl);
        Assert.Equal(generatedAt, result.Value.GeneratedAt);
        Assert.True(File.Exists(viewerPath));

        var html = File.ReadAllText(viewerPath);
        Assert.Contains("data:image/png;base64,AQID", html);
        Assert.Contains("Viewer preview", html);
        Assert.Contains("binding_warning", html);
        Assert.Contains("preview-viewer", html);
    }

    [Fact]
    public void PreviewViewerExporterRejectsFailedPreviewSessionRender()
    {
        var session = new PreviewSessionSummary(
            new SessionSummary(
                new SessionId("preview-failed"),
                SessionKinds.Preview,
                SessionStates.Failed,
                DateTimeOffset.UnixEpoch,
                "Failed preview"),
            new PreviewRequest(
                Path.Combine(_testRoot, "missing.png"),
                width: 100,
                height: 80,
                dpi: 96),
            ToolResult<PreviewResponse>.Fail(new ProtocolError("preview_failed", "Preview failed.")),
            DateTimeOffset.UnixEpoch);

        var result = new PreviewViewerExporter().Export(session, Path.Combine(_testRoot, "viewer.html"));

        Assert.False(result.Success);
        Assert.Equal(CoreErrorCodes.PreviewViewerUnavailable, result.Error!.Code);
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

    [Fact]
    public async Task PreviewSessionWatcherReloadsWhenExplicitWatchFileChanges()
    {
        var watchedPath = Path.Combine(_testRoot, "WatchedView.axaml");
        Directory.CreateDirectory(_testRoot);
        await File.WriteAllTextAsync(watchedPath, "<UserControl />");

        var previewSessions = new PreviewSessionRegistry(
            new SessionRegistry(),
            new PreviewHostClient(Path.Combine(_testRoot, "missing-host.dll")));
        var created = await previewSessions.CreateAsync(new PreviewRequest(
            Path.Combine(_testRoot, "preview.png"),
            width: 100,
            height: 100,
            dpi: 96,
            viewPath: watchedPath));
        Assert.True(created.Success, created.Error?.Message);

        var watcher = new PreviewSessionWatcher(previewSessions);
        var watchTask = watcher.WatchAsync(
            created.Value!.Session.SessionId,
            new PreviewSessionWatchOptions(
                TimeSpan.FromSeconds(5),
                TimeSpan.FromMilliseconds(50),
                maxReloads: 1,
                [watchedPath]));

        await Task.Delay(500);
        await File.AppendAllTextAsync(watchedPath, Environment.NewLine);

        var result = await watchTask;

        Assert.True(result.Success, result.Error?.Message);
        Assert.False(result.Value!.TimedOut);
        Assert.Equal(1, result.Value.ReloadCount);
        Assert.Contains(result.Value.Events, static watchEvent => watchEvent.EventType == PreviewWatchEventTypes.Changed);
        Assert.Contains(result.Value.Events, static watchEvent => watchEvent.EventType == PreviewWatchEventTypes.Reloaded);
        Assert.Equal(created.Value.Session.SessionId, result.Value.SessionId);
    }

    [Fact]
    public async Task PreviewSessionWatcherSkipsReloadWhenWatchedInputsAreUnchanged()
    {
        var watchedDirectory = Path.Combine(_testRoot, "watched");
        var watchedPath = Path.Combine(watchedDirectory, "WatchedView.axaml");
        Directory.CreateDirectory(watchedDirectory);
        await File.WriteAllTextAsync(watchedPath, "<UserControl />");

        var previewSessions = new PreviewSessionRegistry(
            new SessionRegistry(),
            new PreviewHostClient(Path.Combine(_testRoot, "missing-host.dll")));
        var created = await previewSessions.CreateAsync(new PreviewRequest(
            Path.Combine(_testRoot, "preview.png"),
            width: 100,
            height: 100,
            dpi: 96,
            viewPath: watchedPath));
        Assert.True(created.Success, created.Error?.Message);

        var watcher = new PreviewSessionWatcher(previewSessions);
        var watchTask = watcher.WatchAsync(
            created.Value!.Session.SessionId,
            new PreviewSessionWatchOptions(
                TimeSpan.FromSeconds(3),
                TimeSpan.FromMilliseconds(150),
                maxReloads: 1,
                [watchedDirectory]));

        await Task.Delay(500);
        var temporaryPath = Path.Combine(watchedDirectory, "temporary.txt");
        await File.WriteAllTextAsync(temporaryPath, "transient");
        File.Delete(temporaryPath);

        var result = await watchTask;

        Assert.True(result.Success, result.Error?.Message);
        Assert.True(result.Value!.TimedOut);
        Assert.Equal(0, result.Value.ReloadCount);
        Assert.Contains(result.Value.Events, static watchEvent => watchEvent.EventType == PreviewWatchEventTypes.Changed);
        Assert.Contains(result.Value.Events, static watchEvent => watchEvent.EventType == PreviewWatchEventTypes.Skipped);
        Assert.DoesNotContain(result.Value.Events, static watchEvent => watchEvent.EventType == PreviewWatchEventTypes.Reloaded);
        Assert.Equal(created.Value.Session.SessionId, result.Value.SessionId);
    }

    private sealed class ManualTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public DateTimeOffset UtcNow { get; set; } = utcNow;

        public override DateTimeOffset GetUtcNow() => UtcNow;
    }
}
