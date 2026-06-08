using System.Threading.Channels;
using AvaScope.Protocol;

namespace AvaScope.Core;

public sealed class PreviewSessionWatcher
{
    private const int MaximumWatchEvents = 100;
    private readonly PreviewSessionRegistry _previewSessions;
    private readonly TimeProvider _timeProvider;

    public PreviewSessionWatcher(PreviewSessionRegistry previewSessions)
        : this(previewSessions, TimeProvider.System)
    {
    }

    public PreviewSessionWatcher(PreviewSessionRegistry previewSessions, TimeProvider timeProvider)
    {
        _previewSessions = previewSessions ?? throw new ArgumentNullException(nameof(previewSessions));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    public async Task<CoreResult<PreviewWatchResponse>> WatchAsync(
        SessionId sessionId,
        PreviewSessionWatchOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sessionId);
        ArgumentNullException.ThrowIfNull(options);

        var startedAt = _timeProvider.GetUtcNow();
        var initialSession = FindSession(sessionId);
        if (initialSession is null)
        {
            return CoreResult<PreviewWatchResponse>.Fail(new CoreError(
                CoreErrorCodes.SessionNotFound,
                $"Preview session '{sessionId}' was not found."));
        }

        if (initialSession.Session.State is SessionStates.Closed)
        {
            return CoreResult<PreviewWatchResponse>.Fail(new CoreError(
                CoreErrorCodes.SessionClosed,
                $"Preview session '{sessionId}' is closed and cannot be watched."));
        }

        var watchPaths = ResolveWatchPaths(initialSession, options.WatchPaths);
        if (watchPaths.Count == 0)
        {
            return CoreResult<PreviewWatchResponse>.Fail(new CoreError(
                CoreErrorCodes.InvalidPreviewRequest,
                "At least one watch path is required, or the preview session must contain a project or view path."));
        }

        var channel = Channel.CreateUnbounded<WatchFileEvent>();
        var watcherSetResult = CreateWatcherSet(watchPaths, channel);
        if (!watcherSetResult.Success)
        {
            return CoreResult<PreviewWatchResponse>.Fail(watcherSetResult.Error!);
        }

        using var watcherSet = watcherSetResult.Value!;
        var events = new List<PreviewWatchEvent>();
        var reloadCount = 0;
        PreviewSessionSummary? latestSession = initialSession;

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(options.Timeout);

        try
        {
            while (reloadCount < options.MaxReloads)
            {
                var firstEvent = await channel.Reader.ReadAsync(timeout.Token);
                AddChangedEvent(events, firstEvent);

                if (options.SettleDelay > TimeSpan.Zero)
                {
                    await Task.Delay(options.SettleDelay, timeout.Token);
                }

                while (channel.Reader.TryRead(out var extraEvent))
                {
                    AddChangedEvent(events, extraEvent);
                }

                var reloaded = await _previewSessions.ReloadAsync(sessionId, cancellationToken);
                reloadCount++;
                latestSession = reloaded.Success ? reloaded.Value : latestSession;
                AddEvent(events, new PreviewWatchEvent(
                    PreviewWatchEventTypes.Reloaded,
                    _timeProvider.GetUtcNow(),
                    reload: reloaded.Success
                        ? ToolResult<PreviewSessionSummary>.Ok(reloaded.Value!)
                        : ToolResult<PreviewSessionSummary>.Fail(new ProtocolError(
                            reloaded.Error!.Code,
                            reloaded.Error.Message,
                            reloaded.Error.Details))));

                if (!reloaded.Success)
                {
                    return CoreResult<PreviewWatchResponse>.Fail(reloaded.Error!);
                }
            }

            return CoreResult<PreviewWatchResponse>.Ok(new PreviewWatchResponse(
                sessionId,
                watchPaths,
                events,
                timedOut: false,
                reloadCount,
                startedAt,
                _timeProvider.GetUtcNow(),
                latestSession));
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return CoreResult<PreviewWatchResponse>.Ok(new PreviewWatchResponse(
                sessionId,
                watchPaths,
                events,
                timedOut: true,
                reloadCount,
                startedAt,
                _timeProvider.GetUtcNow(),
                latestSession));
        }
    }

    private PreviewSessionSummary? FindSession(SessionId sessionId)
    {
        return _previewSessions
            .List()
            .FirstOrDefault(session => string.Equals(
                session.Session.SessionId.Value,
                sessionId.Value,
                StringComparison.Ordinal));
    }

    private static IReadOnlyList<string> ResolveWatchPaths(
        PreviewSessionSummary session,
        IReadOnlyList<string> explicitWatchPaths)
    {
        if (explicitWatchPaths.Count > 0)
        {
            return explicitWatchPaths
                .Select(Path.GetFullPath)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        var paths = new List<string>();
        if (!string.IsNullOrWhiteSpace(session.Request.ProjectPath))
        {
            paths.Add(Path.GetFullPath(session.Request.ProjectPath));
        }

        if (!string.IsNullOrWhiteSpace(session.Request.ViewPath))
        {
            var viewPath = session.Request.ViewPath;
            if (!Path.IsPathRooted(viewPath) && !string.IsNullOrWhiteSpace(session.Request.ProjectPath))
            {
                var projectDirectory = Path.GetDirectoryName(Path.GetFullPath(session.Request.ProjectPath));
                if (!string.IsNullOrWhiteSpace(projectDirectory))
                {
                    viewPath = Path.Combine(projectDirectory, viewPath);
                }
            }

            paths.Add(Path.GetFullPath(viewPath));
        }

        return paths
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static CoreResult<WatcherSet> CreateWatcherSet(
        IReadOnlyList<string> watchPaths,
        ChannelWriter<WatchFileEvent> writer)
    {
        var watchers = new List<FileSystemWatcher>();
        try
        {
            foreach (var path in watchPaths)
            {
                var target = CreateWatchTarget(path);
                if (!Directory.Exists(target.Directory))
                {
                    foreach (var existingWatcher in watchers)
                    {
                        existingWatcher.Dispose();
                    }

                    return CoreResult<WatcherSet>.Fail(new CoreError(
                        CoreErrorCodes.InvalidPreviewRequest,
                        $"Watch directory '{target.Directory}' was not found."));
                }

                var watcher = new FileSystemWatcher(target.Directory, target.Filter)
                {
                    IncludeSubdirectories = false,
                    NotifyFilter = NotifyFilters.FileName
                        | NotifyFilters.LastWrite
                        | NotifyFilters.Size
                        | NotifyFilters.CreationTime
                };
                watcher.Changed += (_, args) => TryWriteEvent(writer, args);
                watcher.Created += (_, args) => TryWriteEvent(writer, args);
                watcher.Deleted += (_, args) => TryWriteEvent(writer, args);
                watcher.Renamed += (_, args) => TryWriteEvent(writer, args);
                watcher.EnableRaisingEvents = true;
                watchers.Add(watcher);
            }

            return CoreResult<WatcherSet>.Ok(new WatcherSet(watchers));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            foreach (var existingWatcher in watchers)
            {
                existingWatcher.Dispose();
            }

            return CoreResult<WatcherSet>.Fail(new CoreError(
                CoreErrorCodes.InvalidPreviewRequest,
                $"Preview watch setup failed: {exception.Message}"));
        }
    }

    private static WatchTarget CreateWatchTarget(string path)
    {
        var fullPath = Path.GetFullPath(path);
        if (Directory.Exists(fullPath))
        {
            return new WatchTarget(fullPath, "*");
        }

        return new WatchTarget(
            Path.GetDirectoryName(fullPath) ?? Environment.CurrentDirectory,
            Path.GetFileName(fullPath));
    }

    private static void TryWriteEvent(ChannelWriter<WatchFileEvent> writer, FileSystemEventArgs args)
    {
        writer.TryWrite(new WatchFileEvent(args.FullPath, args.ChangeType.ToString()));
    }

    private void AddChangedEvent(List<PreviewWatchEvent> events, WatchFileEvent fileEvent)
    {
        AddEvent(events, new PreviewWatchEvent(
            PreviewWatchEventTypes.Changed,
            _timeProvider.GetUtcNow(),
            fileEvent.Path,
            fileEvent.ChangeKind));
    }

    private static void AddEvent(List<PreviewWatchEvent> events, PreviewWatchEvent watchEvent)
    {
        if (events.Count < MaximumWatchEvents)
        {
            events.Add(watchEvent);
        }
    }

    private sealed record WatchTarget(string Directory, string Filter);

    private sealed record WatchFileEvent(string Path, string ChangeKind);

    private sealed class WatcherSet(IReadOnlyList<FileSystemWatcher> watchers) : IDisposable
    {
        public void Dispose()
        {
            foreach (var watcher in watchers)
            {
                watcher.Dispose();
            }
        }
    }
}
