using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using Avalonia.Interactivity;
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
    private const int DefaultFindResultLimit = 100;
    private const int MaximumTreeDepth = 64;
    private const int MaximumFindResultLimit = 1000;
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

    public Task<CoreResult<FindNodesResponse>> FindNodesAsync(
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
        if (string.IsNullOrWhiteSpace(topLevelId))
        {
            throw new ArgumentException("Top-level id cannot be empty.", nameof(topLevelId));
        }

        if (string.IsNullOrWhiteSpace(treeKind))
        {
            throw new ArgumentException("Tree kind cannot be empty.", nameof(treeKind));
        }

        if (Dispatcher.UIThread.CheckAccess())
        {
            return Task.FromResult(FindNodes(
                topLevelId,
                treeKind,
                nodeType,
                name,
                automationId,
                text,
                maxDepth,
                maxResults));
        }

        return Dispatcher.UIThread
            .InvokeAsync(
                () => FindNodes(
                    topLevelId,
                    treeKind,
                    nodeType,
                    name,
                    automationId,
                    text,
                    maxDepth,
                    maxResults),
                DispatcherPriority.Background,
                cancellationToken)
            .GetTask();
    }

    public Task<CoreResult<InputResponse>> InputAsync(
        string topLevelId,
        string action,
        double? x = null,
        double? y = null,
        string? inputText = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(topLevelId))
        {
            throw new ArgumentException("Top-level id cannot be empty.", nameof(topLevelId));
        }

        if (string.IsNullOrWhiteSpace(action))
        {
            throw new ArgumentException("Input action cannot be empty.", nameof(action));
        }

        if (Dispatcher.UIThread.CheckAccess())
        {
            return Task.FromResult(Input(topLevelId, action, x, y, inputText));
        }

        return Dispatcher.UIThread
            .InvokeAsync(
                () => Input(topLevelId, action, x, y, inputText),
                DispatcherPriority.Background,
                cancellationToken)
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

    private CoreResult<FindNodesResponse> FindNodes(
        string topLevelId,
        string treeKind,
        string? nodeType,
        string? name,
        string? automationId,
        string? text,
        int? maxDepth,
        int? maxResults)
    {
        Dispatcher.UIThread.VerifyAccess();

        if (string.IsNullOrWhiteSpace(nodeType)
            && string.IsNullOrWhiteSpace(name)
            && string.IsNullOrWhiteSpace(automationId)
            && string.IsNullOrWhiteSpace(text))
        {
            return InvalidFindRequest("At least one find filter is required.");
        }

        var resultLimit = NormalizeResultLimit(maxResults);
        if (!resultLimit.Success)
        {
            return CoreResult<FindNodesResponse>.Fail(resultLimit.Error!);
        }

        var treeResult = treeKind switch
        {
            TreeKinds.Visual => GetVisualTree(topLevelId, maxDepth),
            TreeKinds.Logical => GetLogicalTree(topLevelId, maxDepth),
            _ => InvalidTreeKind(topLevelId, treeKind)
        };

        if (!treeResult.Success)
        {
            return CoreResult<FindNodesResponse>.Fail(treeResult.Error!);
        }

        var matches = new List<FindNodeMatch>();
        CollectMatches(
            treeResult.Value!.Root,
            nodeType,
            name,
            automationId,
            text,
            new List<string>(),
            matches,
            resultLimit.Value);

        return CoreResult<FindNodesResponse>.Ok(new FindNodesResponse(
            SessionId,
            topLevelId,
            treeKind,
            treeResult.Value.DepthLimit,
            matches));
    }

    private CoreResult<InputResponse> Input(
        string topLevelId,
        string action,
        double? x,
        double? y,
        string? inputText)
    {
        Dispatcher.UIThread.VerifyAccess();

        var topLevel = FindTopLevel(topLevelId);
        if (topLevel is null)
        {
            return TopLevelNotFound<InputResponse>(topLevelId);
        }

        return action switch
        {
            InputActions.PointerMove => PointerMove(topLevel, topLevelId, x, y),
            InputActions.Click => Click(topLevel, topLevelId, x, y),
            InputActions.KeyText => KeyText(topLevel, topLevelId, inputText),
            _ => CoreResult<InputResponse>.Fail(new CoreError(
                BridgeErrorCodes.UnsupportedInputAction,
                $"Input action '{action}' is not supported."))
        };
    }

    private CoreResult<InputResponse> PointerMove(TopLevel topLevel, string topLevelId, double? x, double? y)
    {
        var point = GetInputPoint(x, y);
        if (!point.Success)
        {
            return CoreResult<InputResponse>.Fail(point.Error!);
        }

        var target = topLevel.GetVisualAt(point.Value);
        if (target is null)
        {
            return CoreResult<InputResponse>.Ok(new InputResponse(
                SessionId,
                topLevelId,
                InputActions.PointerMove,
                handled: false,
                DateTimeOffset.UtcNow));
        }

        var inputTarget = target as InputElement ?? target.FindAncestorOfType<InputElement>();
        if (inputTarget is null)
        {
            return CoreResult<InputResponse>.Fail(new CoreError(
                BridgeErrorCodes.UnsupportedInputAction,
                "Pointer move target is not an input element."));
        }

        inputTarget.RaiseEvent(new PointerEventArgs(
            InputElement.PointerMovedEvent,
            inputTarget,
            new Pointer(Pointer.GetNextFreeId(), PointerType.Mouse, isPrimary: true),
            topLevel,
            point.Value,
            (ulong)Environment.TickCount64,
            PointerPointProperties.None,
            KeyModifiers.None));

        return CoreResult<InputResponse>.Ok(new InputResponse(
            SessionId,
            topLevelId,
            InputActions.PointerMove,
            handled: true,
            DateTimeOffset.UtcNow,
            CreateNodeId(inputTarget, TreeKinds.Visual)));
    }

    private CoreResult<InputResponse> Click(TopLevel topLevel, string topLevelId, double? x, double? y)
    {
        var point = GetInputPoint(x, y);
        if (!point.Success)
        {
            return CoreResult<InputResponse>.Fail(point.Error!);
        }

        var target = topLevel.GetVisualAt(point.Value);
        var button = target as Button ?? target?.FindAncestorOfType<Button>();
        if (button is null)
        {
            return CoreResult<InputResponse>.Fail(new CoreError(
                BridgeErrorCodes.UnsupportedInputAction,
                "Click MVP currently supports Button targets only."));
        }

        button.Focus(NavigationMethod.Pointer);
        button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent, button));

        return CoreResult<InputResponse>.Ok(new InputResponse(
            SessionId,
            topLevelId,
            InputActions.Click,
            handled: true,
            DateTimeOffset.UtcNow,
            CreateNodeId(button, TreeKinds.Visual)));
    }

    private CoreResult<InputResponse> KeyText(TopLevel topLevel, string topLevelId, string? inputText)
    {
        if (string.IsNullOrEmpty(inputText))
        {
            return CoreResult<InputResponse>.Fail(new CoreError(
                BridgeErrorCodes.InvalidInputRequest,
                "Text input requires non-empty input text."));
        }

        if (topLevel.FocusManager?.GetFocusedElement() is not TextBox textBox)
        {
            return CoreResult<InputResponse>.Fail(new CoreError(
                BridgeErrorCodes.UnsupportedInputAction,
                "Text input MVP currently requires a focused TextBox target."));
        }

        var currentText = textBox.Text ?? string.Empty;
        var caretIndex = Math.Clamp(textBox.CaretIndex, 0, currentText.Length);
        textBox.Text = currentText.Insert(caretIndex, inputText);
        textBox.CaretIndex = caretIndex + inputText.Length;

        return CoreResult<InputResponse>.Ok(new InputResponse(
            SessionId,
            topLevelId,
            InputActions.KeyText,
            handled: true,
            DateTimeOffset.UtcNow,
            CreateNodeId(textBox, TreeKinds.Visual)));
    }

    private static CoreResult<Point> GetInputPoint(double? x, double? y)
    {
        if (x is null || y is null)
        {
            return CoreResult<Point>.Fail(new CoreError(
                BridgeErrorCodes.InvalidInputRequest,
                "Pointer input requires x and y coordinates."));
        }

        return CoreResult<Point>.Ok(new Point(x.Value, y.Value));
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

    private static CoreResult<int> NormalizeResultLimit(int? maxResults)
    {
        if (maxResults is < 1)
        {
            return CoreResult<int>.Fail(new CoreError(
                BridgeErrorCodes.InvalidFindRequest,
                "Find result limit must be positive."));
        }

        return CoreResult<int>.Ok(Math.Min(maxResults ?? DefaultFindResultLimit, MaximumFindResultLimit));
    }

    private static CoreResult<FindNodesResponse> InvalidFindRequest(string message)
    {
        return CoreResult<FindNodesResponse>.Fail(new CoreError(BridgeErrorCodes.InvalidFindRequest, message));
    }

    private static CoreResult<TreeResponse> InvalidTreeKind(string topLevelId, string treeKind)
    {
        return CoreResult<TreeResponse>.Fail(new CoreError(
            BridgeErrorCodes.InvalidFindRequest,
            $"Tree kind '{treeKind}' is not supported for top-level '{topLevelId}'."));
    }

    private static void CollectMatches(
        TreeNodeSummary node,
        string? nodeType,
        string? name,
        string? automationId,
        string? text,
        List<string> path,
        List<FindNodeMatch> matches,
        int maxResults)
    {
        if (matches.Count >= maxResults)
        {
            return;
        }

        path.Add(node.NodeId);

        if (Matches(node, nodeType, name, automationId, text))
        {
            matches.Add(new FindNodeMatch(node, path.ToArray()));
        }

        foreach (var child in node.Children)
        {
            CollectMatches(child, nodeType, name, automationId, text, path, matches, maxResults);
            if (matches.Count >= maxResults)
            {
                break;
            }
        }

        path.RemoveAt(path.Count - 1);
    }

    private static bool Matches(
        TreeNodeSummary node,
        string? nodeType,
        string? name,
        string? automationId,
        string? text)
    {
        return MatchesContains(node.NodeType, nodeType)
            && MatchesEquals(node.Name, name)
            && MatchesEquals(node.AutomationId, automationId)
            && MatchesContains(node.Text, text);
    }

    private static bool MatchesContains(string? value, string? filter)
    {
        return string.IsNullOrWhiteSpace(filter)
            || (!string.IsNullOrWhiteSpace(value)
                && value.Contains(filter, StringComparison.OrdinalIgnoreCase));
    }

    private static bool MatchesEquals(string? value, string? filter)
    {
        return string.IsNullOrWhiteSpace(filter)
            || string.Equals(value, filter, StringComparison.OrdinalIgnoreCase);
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
            CreateNodeId(node, treeKind),
            node.GetType().FullName ?? node.GetType().Name,
            GetName(node),
            GetAutomationId(node),
            GetText(node),
            GetBounds(node),
            GetClasses(node),
            children);
    }

    private static string CreateNodeId(object node, string treeKind)
    {
        return $"{treeKind}:{RuntimeHelpers.GetHashCode(node):x}";
    }

    private static string? GetName(object node)
    {
        return node is StyledElement styledElement && !string.IsNullOrWhiteSpace(styledElement.Name)
            ? styledElement.Name
            : null;
    }

    private static string? GetAutomationId(object node)
    {
        return node is StyledElement styledElement
            ? AutomationProperties.GetAutomationId(styledElement)
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
