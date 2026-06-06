using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.LogicalTree;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using AvaScope.Core;
using AvaScope.Protocol;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;

namespace AvaScope.Bridge;

public sealed class AvaScopeBridgeRuntime
{
    private const int DefaultTreeDepth = 10;
    private const int MaximumTreeDepth = 64;
    private readonly ConcurrentDictionary<int, WeakReference<TopLevel>> _registeredTopLevels = new();
    private readonly SessionRegistry _sessionRegistry;
    private LocalBridgeServer? _localServer;

    internal AvaScopeBridgeRuntime(
        SessionRegistry sessionRegistry,
        SessionSnapshot session,
        BridgeTransportScope transportScope)
    {
        _sessionRegistry = sessionRegistry ?? throw new ArgumentNullException(nameof(sessionRegistry));
        Session = session ?? throw new ArgumentNullException(nameof(session));
        TransportScope = transportScope;
    }

    public SessionSnapshot Session { get; }

    public SessionId SessionId => Session.Id;

    public BridgeTransportScope TransportScope { get; }

    public string? LocalPipeName => _localServer?.PipeName;

    public string? SessionManifestPath => _localServer?.ManifestPath;

    public IDisposable RegisterTopLevel(TopLevel topLevel)
    {
        ArgumentNullException.ThrowIfNull(topLevel);
        Dispatcher.UIThread.VerifyAccess();

        var key = InspectableTopLevel.GetRuntimeId(topLevel);
        _registeredTopLevels[key] = new WeakReference<TopLevel>(topLevel);

        return new TopLevelRegistration(_registeredTopLevels, key);
    }

    public Task<IReadOnlyList<TopLevelSummary>> ListTopLevelsAsync(CancellationToken cancellationToken = default)
    {
        if (Dispatcher.UIThread.CheckAccess())
        {
            return Task.FromResult(DiscoverTopLevels());
        }

        return Dispatcher.UIThread
            .InvokeAsync(DiscoverTopLevels, DispatcherPriority.Background, cancellationToken)
            .GetTask();
    }

    public Task<CoreResult<ScreenshotResponse>> CaptureScreenshotAsync(
        string topLevelId,
        string outputPath,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(topLevelId))
        {
            throw new ArgumentException("Top-level id cannot be empty.", nameof(topLevelId));
        }

        if (string.IsNullOrWhiteSpace(outputPath))
        {
            return Task.FromResult(CoreResult<ScreenshotResponse>.Fail(
                new CoreError(BridgeErrorCodes.InvalidScreenshotPath, "Screenshot output path cannot be empty.")));
        }

        if (Dispatcher.UIThread.CheckAccess())
        {
            return Task.FromResult(CaptureScreenshot(topLevelId, outputPath));
        }

        return Dispatcher.UIThread
            .InvokeAsync(() => CaptureScreenshot(topLevelId, outputPath), DispatcherPriority.Background, cancellationToken)
            .GetTask();
    }

    public Task<CoreResult<TreeResponse>> GetVisualTreeAsync(
        string topLevelId,
        int? maxDepth = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(topLevelId))
        {
            throw new ArgumentException("Top-level id cannot be empty.", nameof(topLevelId));
        }

        if (Dispatcher.UIThread.CheckAccess())
        {
            return Task.FromResult(GetVisualTree(topLevelId, maxDepth));
        }

        return Dispatcher.UIThread
            .InvokeAsync(() => GetVisualTree(topLevelId, maxDepth), DispatcherPriority.Background, cancellationToken)
            .GetTask();
    }

    public Task<CoreResult<TreeResponse>> GetLogicalTreeAsync(
        string topLevelId,
        int? maxDepth = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(topLevelId))
        {
            throw new ArgumentException("Top-level id cannot be empty.", nameof(topLevelId));
        }

        if (Dispatcher.UIThread.CheckAccess())
        {
            return Task.FromResult(GetLogicalTree(topLevelId, maxDepth));
        }

        return Dispatcher.UIThread
            .InvokeAsync(() => GetLogicalTree(topLevelId, maxDepth), DispatcherPriority.Background, cancellationToken)
            .GetTask();
    }

    internal CoreResult<SessionSnapshot> Close()
    {
        _localServer?.Dispose();
        _localServer = null;

        return _sessionRegistry.Close(SessionId);
    }

    internal void StartLocalServer()
    {
        _localServer ??= LocalBridgeServer.Start(this);
    }

    private IReadOnlyList<TopLevelSummary> DiscoverTopLevels()
    {
        Dispatcher.UIThread.VerifyAccess();

        var discovered = new List<TopLevelSummary>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var topLevel in DiscoverLifetimeTopLevels())
        {
            if (seen.Add(topLevel.Id))
            {
                discovered.Add(topLevel);
            }
        }

        foreach (var topLevel in DiscoverRegisteredTopLevels())
        {
            if (seen.Add(topLevel.Id))
            {
                discovered.Add(topLevel);
            }
        }

        return discovered;
    }

    private static IReadOnlyList<TopLevelSummary> DiscoverLifetimeTopLevels()
    {
        return Application.Current?.ApplicationLifetime switch
        {
            IClassicDesktopStyleApplicationLifetime desktop => desktop.Windows
                .Select(static window => InspectableTopLevel.FromWindow(window))
                .ToArray(),
            ISingleViewApplicationLifetime { MainView: { } mainView } => DiscoverSingleViewTopLevel(mainView),
            _ => Array.Empty<TopLevelSummary>()
        };
    }

    private IReadOnlyList<TopLevelSummary> DiscoverRegisteredTopLevels()
    {
        var topLevels = new List<TopLevelSummary>();

        foreach (var (key, weakReference) in _registeredTopLevels)
        {
            if (!weakReference.TryGetTarget(out var topLevel))
            {
                _registeredTopLevels.TryRemove(key, out _);
                continue;
            }

            topLevels.Add(topLevel is Window window
                ? InspectableTopLevel.FromWindow(window)
                : InspectableTopLevel.FromTopLevel(topLevel, "topLevel"));
        }

        return topLevels;
    }

    private CoreResult<ScreenshotResponse> CaptureScreenshot(string topLevelId, string outputPath)
    {
        Dispatcher.UIThread.VerifyAccess();

        var topLevel = FindTopLevel(topLevelId);
        if (topLevel is null)
        {
            return CoreResult<ScreenshotResponse>.Fail(
                new CoreError(BridgeErrorCodes.TopLevelNotFound, $"Top-level '{topLevelId}' was not found."));
        }

        var pixelSize = GetPixelSize(topLevel);
        if (pixelSize.Width < 1 || pixelSize.Height < 1)
        {
            return CoreResult<ScreenshotResponse>.Fail(
                new CoreError(BridgeErrorCodes.InvalidTopLevelSize, $"Top-level '{topLevelId}' has no renderable size."));
        }

        try
        {
            var fullPath = Path.GetFullPath(outputPath);
            var directory = Path.GetDirectoryName(fullPath);

            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var dpi = new Vector(96 * GetRenderScaling(topLevel), 96 * GetRenderScaling(topLevel));
            using var bitmap = new RenderTargetBitmap(pixelSize, dpi);
            bitmap.Render(topLevel);

            using (var stream = File.Create(fullPath))
            {
                bitmap.Save(stream);
            }

            return CoreResult<ScreenshotResponse>.Ok(new ScreenshotResponse(
                SessionId,
                topLevelId,
                fullPath,
                pixelSize.Width,
                pixelSize.Height,
                DateTimeOffset.UtcNow));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or NotSupportedException or InvalidOperationException)
        {
            return CoreResult<ScreenshotResponse>.Fail(
                new CoreError(BridgeErrorCodes.ScreenshotCaptureFailed, exception.Message));
        }
    }

    private CoreResult<TreeResponse> GetVisualTree(string topLevelId, int? maxDepth)
    {
        Dispatcher.UIThread.VerifyAccess();

        var topLevel = FindTopLevel(topLevelId);
        if (topLevel is null)
        {
            return TopLevelNotFound<TreeResponse>(topLevelId);
        }

        var depthLimit = NormalizeDepthLimit(maxDepth);
        if (!depthLimit.Success)
        {
            return CoreResult<TreeResponse>.Fail(depthLimit.Error!);
        }

        var normalizedDepth = depthLimit.Value;

        return CoreResult<TreeResponse>.Ok(new TreeResponse(
            SessionId,
            topLevelId,
            TreeKinds.Visual,
            normalizedDepth,
            SerializeVisualNode(topLevel, depth: 0, normalizedDepth)));
    }

    private CoreResult<TreeResponse> GetLogicalTree(string topLevelId, int? maxDepth)
    {
        Dispatcher.UIThread.VerifyAccess();

        var topLevel = FindTopLevel(topLevelId);
        if (topLevel is null)
        {
            return TopLevelNotFound<TreeResponse>(topLevelId);
        }

        var depthLimit = NormalizeDepthLimit(maxDepth);
        if (!depthLimit.Success)
        {
            return CoreResult<TreeResponse>.Fail(depthLimit.Error!);
        }

        var normalizedDepth = depthLimit.Value;

        return CoreResult<TreeResponse>.Ok(new TreeResponse(
            SessionId,
            topLevelId,
            TreeKinds.Logical,
            normalizedDepth,
            SerializeLogicalNode(topLevel, depth: 0, normalizedDepth)));
    }

    private static CoreResult<int> NormalizeDepthLimit(int? maxDepth)
    {
        if (maxDepth is < 0)
        {
            return CoreResult<int>.Fail(new CoreError(
                BridgeErrorCodes.InvalidTreeDepth,
                "Tree depth limit cannot be negative."));
        }

        return CoreResult<int>.Ok(Math.Min(maxDepth ?? DefaultTreeDepth, MaximumTreeDepth));
    }

    private static CoreResult<T> TopLevelNotFound<T>(string topLevelId)
    {
        return CoreResult<T>.Fail(
            new CoreError(BridgeErrorCodes.TopLevelNotFound, $"Top-level '{topLevelId}' was not found."));
    }

    private static TreeNodeSummary SerializeVisualNode(Visual visual, int depth, int maxDepth)
    {
        var children = depth >= maxDepth
            ? Array.Empty<TreeNodeSummary>()
            : visual.GetVisualChildren()
                .Select(child => SerializeVisualNode(child, depth + 1, maxDepth))
                .ToArray();

        return CreateNodeSummary(visual, TreeKinds.Visual, children);
    }

    private static TreeNodeSummary SerializeLogicalNode(ILogical logical, int depth, int maxDepth)
    {
        var children = depth >= maxDepth
            ? Array.Empty<TreeNodeSummary>()
            : logical.GetLogicalChildren()
                .Select(child => SerializeLogicalNode(child, depth + 1, maxDepth))
                .ToArray();

        return CreateNodeSummary(logical, TreeKinds.Logical, children);
    }

    private static TreeNodeSummary CreateNodeSummary(
        object node,
        string treeKind,
        IReadOnlyList<TreeNodeSummary> children)
    {
        return new TreeNodeSummary(
            $"{treeKind}:{RuntimeHelpers.GetHashCode(node):x}",
            node.GetType().FullName ?? node.GetType().Name,
            GetName(node),
            GetText(node),
            GetBounds(node),
            GetClasses(node),
            children);
    }

    private static string? GetName(object node)
    {
        return node is StyledElement styledElement && !string.IsNullOrWhiteSpace(styledElement.Name)
            ? styledElement.Name
            : null;
    }

    private static string? GetText(object node)
    {
        return node switch
        {
            TextBlock { Text: { } text } when !string.IsNullOrEmpty(text) => text,
            TextBox { Text: { } text } when !string.IsNullOrEmpty(text) => text,
            ContentControl { Content: string text } when !string.IsNullOrEmpty(text) => text,
            _ => null
        };
    }

    private static NodeBounds? GetBounds(object node)
    {
        if (node is not Visual visual)
        {
            return null;
        }

        return new NodeBounds(
            visual.Bounds.X,
            visual.Bounds.Y,
            visual.Bounds.Width,
            visual.Bounds.Height);
    }

    private static IReadOnlyList<string> GetClasses(object node)
    {
        return node is StyledElement styledElement
            ? styledElement.Classes.ToArray()
            : Array.Empty<string>();
    }

    private TopLevel? FindTopLevel(string topLevelId)
    {
        return EnumerateLifetimeTopLevels()
            .Concat(EnumerateRegisteredTopLevels())
            .FirstOrDefault(topLevel => InspectableTopLevel.CreateId(topLevel) == topLevelId);
    }

    private static IEnumerable<TopLevel> EnumerateLifetimeTopLevels()
    {
        return Application.Current?.ApplicationLifetime switch
        {
            IClassicDesktopStyleApplicationLifetime desktop => desktop.Windows,
            ISingleViewApplicationLifetime { MainView: { } mainView } => TopLevel.GetTopLevel(mainView) is { } topLevel
                ? [topLevel]
                : [],
            _ => []
        };
    }

    private IEnumerable<TopLevel> EnumerateRegisteredTopLevels()
    {
        foreach (var (key, weakReference) in _registeredTopLevels)
        {
            if (weakReference.TryGetTarget(out var topLevel))
            {
                yield return topLevel;
                continue;
            }

            _registeredTopLevels.TryRemove(key, out _);
        }
    }

    private static PixelSize GetPixelSize(TopLevel topLevel)
    {
        var scaling = GetRenderScaling(topLevel);

        return new PixelSize(
            Math.Max(0, (int)Math.Ceiling(topLevel.ClientSize.Width * scaling)),
            Math.Max(0, (int)Math.Ceiling(topLevel.ClientSize.Height * scaling)));
    }

    private static double GetRenderScaling(TopLevel topLevel)
    {
        return topLevel.RenderScaling > 0 ? topLevel.RenderScaling : 1;
    }

    private static IReadOnlyList<TopLevelSummary> DiscoverSingleViewTopLevel(Control mainView)
    {
        var topLevel = TopLevel.GetTopLevel(mainView);

        return topLevel is null
            ? Array.Empty<TopLevelSummary>()
            : [InspectableTopLevel.FromTopLevel(topLevel, "singleView")];
    }

    private sealed class TopLevelRegistration(
        ConcurrentDictionary<int, WeakReference<TopLevel>> registeredTopLevels,
        int key) : IDisposable
    {
        public void Dispose()
        {
            registeredTopLevels.TryRemove(key, out _);
        }
    }
}
