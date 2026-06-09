using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls.Primitives;
using Avalonia.Diagnostics;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using AvaScope.Core;
using AvaScope.Protocol;
using System.Collections.Concurrent;
using System.Globalization;
using System.Runtime.CompilerServices;

namespace AvaScope.Bridge;

public sealed class AvaScopeBridgeRuntime
{
    private const int DefaultTreeDepth = 10;
    private const int DefaultFindResultLimit = 100;
    private const int MaximumTreeDepth = 64;
    private const int MaximumFindResultLimit = 1000;
    private readonly ConcurrentDictionary<int, WeakReference<TopLevel>> _registeredTopLevels = new();
    private readonly ConcurrentDictionary<string, Pointer> _activePointers = new(StringComparer.Ordinal);
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

    public Task<CoreResult<InspectNodeResponse>> InspectNodeAsync(
        string topLevelId,
        string treeKind,
        string nodeId,
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

        if (string.IsNullOrWhiteSpace(nodeId))
        {
            throw new ArgumentException("Node id cannot be empty.", nameof(nodeId));
        }

        if (Dispatcher.UIThread.CheckAccess())
        {
            return Task.FromResult(InspectNode(topLevelId, treeKind, nodeId));
        }

        return Dispatcher.UIThread
            .InvokeAsync(
                () => InspectNode(topLevelId, treeKind, nodeId),
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
        string? targetNodeId = null,
        string? inputKey = null,
        string? keyModifiers = null,
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
            return Task.FromResult(Input(topLevelId, action, x, y, inputText, targetNodeId, inputKey, keyModifiers));
        }

        return Dispatcher.UIThread
            .InvokeAsync(
                () => Input(topLevelId, action, x, y, inputText, targetNodeId, inputKey, keyModifiers),
                DispatcherPriority.Background,
                cancellationToken)
            .GetTask();
    }

    internal CoreResult<SessionSnapshot> Close()
    {
        var result = CloseSession();
        StopLocalServer();

        return result;
    }

    internal CoreResult<SessionSnapshot> CloseSession()
    {
        return _sessionRegistry.Close(SessionId);
    }

    internal void StopLocalServer()
    {
        _localServer?.Dispose();
        _localServer = null;
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
            return TopLevelNotFound<ScreenshotResponse>(topLevelId);
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
                DateTimeOffset.UtcNow,
                CreateTopLevelTarget(topLevelId)));
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
            SerializeVisualNode(topLevel, topLevelId, depth: 0, normalizedDepth)));
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
            SerializeLogicalNode(topLevel, topLevelId, depth: 0, normalizedDepth)));
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
            matches,
            treeResult.Value.Target));
    }

    private CoreResult<InspectNodeResponse> InspectNode(string topLevelId, string treeKind, string nodeId)
    {
        Dispatcher.UIThread.VerifyAccess();

        var topLevel = FindTopLevel(topLevelId);
        if (topLevel is null)
        {
            return TopLevelNotFound<InspectNodeResponse>(topLevelId);
        }

        if (string.IsNullOrWhiteSpace(nodeId))
        {
            return InvalidInspectRequest("Node id cannot be empty.");
        }

        return treeKind switch
        {
            TreeKinds.Visual => InspectVisualNode(topLevel, topLevelId, nodeId),
            TreeKinds.Logical => topLevel is ILogical logical
                ? InspectLogicalNode(logical, topLevelId, nodeId)
                : NodeNotFound(topLevelId, treeKind, nodeId),
            _ => InvalidInspectRequest($"Tree kind '{treeKind}' is not supported for top-level '{topLevelId}'.")
        };
    }

    private CoreResult<InspectNodeResponse> InspectVisualNode(Visual root, string topLevelId, string nodeId)
    {
        var node = FindVisualNodeById(root, nodeId);
        if (node is null)
        {
            return NodeNotFound(topLevelId, TreeKinds.Visual, nodeId);
        }

        return CoreResult<InspectNodeResponse>.Ok(new InspectNodeResponse(
            SessionId,
            topLevelId,
            TreeKinds.Visual,
            CreateNodeId(node, TreeKinds.Visual),
            node.GetType().FullName ?? node.GetType().Name,
            node.GetVisualChildren().Count(),
            GetName(node),
            GetAutomationId(node),
            GetText(node),
            GetBounds(node),
            GetClasses(node),
            GetComputedProperties(node),
            CreateNodeTarget(topLevelId, TreeKinds.Visual, node)));
    }

    private CoreResult<InspectNodeResponse> InspectLogicalNode(ILogical root, string topLevelId, string nodeId)
    {
        var node = FindLogicalNodeById(root, nodeId);
        if (node is null)
        {
            return NodeNotFound(topLevelId, TreeKinds.Logical, nodeId);
        }

        return CoreResult<InspectNodeResponse>.Ok(new InspectNodeResponse(
            SessionId,
            topLevelId,
            TreeKinds.Logical,
            CreateNodeId(node, TreeKinds.Logical),
            node.GetType().FullName ?? node.GetType().Name,
            node.GetLogicalChildren().Count(),
            GetName(node),
            GetAutomationId(node),
            GetText(node),
            GetBounds(node),
            GetClasses(node),
            GetComputedProperties(node),
            CreateNodeTarget(topLevelId, TreeKinds.Logical, node)));
    }

    private CoreResult<InputResponse> Input(
        string topLevelId,
        string action,
        double? x,
        double? y,
        string? inputText,
        string? targetNodeId,
        string? inputKey,
        string? keyModifiers)
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
            InputActions.PointerDown => PointerButton(topLevel, topLevelId, x, y, InputActions.PointerDown, isPressed: true),
            InputActions.PointerUp => PointerButton(topLevel, topLevelId, x, y, InputActions.PointerUp, isPressed: false),
            InputActions.Click => Click(topLevel, topLevelId, x, y),
            InputActions.KeyText => KeyText(topLevel, topLevelId, targetNodeId, inputText),
            InputActions.ClearText => ClearText(topLevel, topLevelId, targetNodeId),
            InputActions.Focus => FocusTarget(topLevel, topLevelId, targetNodeId, x, y),
            InputActions.KeyDown => KeyInput(topLevel, topLevelId, InputActions.KeyDown, targetNodeId, inputKey, keyModifiers),
            InputActions.KeyUp => KeyInput(topLevel, topLevelId, InputActions.KeyUp, targetNodeId, inputKey, keyModifiers),
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

    private CoreResult<InputResponse> PointerButton(
        TopLevel topLevel,
        string topLevelId,
        double? x,
        double? y,
        string action,
        bool isPressed)
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
                action,
                handled: false,
                DateTimeOffset.UtcNow));
        }

        var inputTarget = target as InputElement ?? target.FindAncestorOfType<InputElement>();
        if (inputTarget is null)
        {
            return CoreResult<InputResponse>.Fail(new CoreError(
                BridgeErrorCodes.UnsupportedInputAction,
                "Pointer button target is not an input element."));
        }

        if (isPressed)
        {
            var pointer = new Pointer(Pointer.GetNextFreeId(), PointerType.Mouse, isPrimary: true);
            _activePointers[topLevelId] = pointer;

            inputTarget.RaiseEvent(new PointerPressedEventArgs(
                inputTarget,
                pointer,
                topLevel,
                point.Value,
                (ulong)Environment.TickCount64,
                new PointerPointProperties(RawInputModifiers.LeftMouseButton, PointerUpdateKind.LeftButtonPressed),
                KeyModifiers.None));
        }
        else
        {
            if (!_activePointers.TryRemove(topLevelId, out var pointer))
            {
                pointer = new Pointer(Pointer.GetNextFreeId(), PointerType.Mouse, isPrimary: true);
            }

            inputTarget.RaiseEvent(new PointerReleasedEventArgs(
                inputTarget,
                pointer,
                topLevel,
                point.Value,
                (ulong)Environment.TickCount64,
                new PointerPointProperties(RawInputModifiers.None, PointerUpdateKind.LeftButtonReleased),
                KeyModifiers.None,
                MouseButton.Left));
        }

        return CoreResult<InputResponse>.Ok(new InputResponse(
            SessionId,
            topLevelId,
            action,
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

    private CoreResult<InputResponse> KeyText(
        TopLevel topLevel,
        string topLevelId,
        string? targetNodeId,
        string? inputText)
    {
        if (string.IsNullOrEmpty(inputText))
        {
            return CoreResult<InputResponse>.Fail(new CoreError(
                BridgeErrorCodes.InvalidInputRequest,
                "Text input requires non-empty input text."));
        }

        TextBox? textBox;
        if (string.IsNullOrWhiteSpace(targetNodeId))
        {
            textBox = topLevel.FocusManager?.GetFocusedElement() as TextBox;
            if (textBox is null)
            {
                return CoreResult<InputResponse>.Fail(new CoreError(
                    BridgeErrorCodes.UnsupportedInputAction,
                    "Text input requires a focused TextBox target or target node id."));
            }
        }
        else
        {
            var resolved = ResolveInputTarget(topLevel, targetNodeId, null, null, "Text input target node id is required.");
            if (!resolved.Success)
            {
                return CoreResult<InputResponse>.Fail(resolved.Error!);
            }

            textBox = resolved.Value as TextBox ?? (resolved.Value as Visual)?.FindAncestorOfType<TextBox>();
            if (textBox is null)
            {
                return CoreResult<InputResponse>.Fail(new CoreError(
                    BridgeErrorCodes.UnsupportedInputAction,
                    "Text input target is not a TextBox."));
            }

            if (!textBox.IsFocused && !textBox.Focus(NavigationMethod.Unspecified))
            {
                return CoreResult<InputResponse>.Fail(new CoreError(
                    BridgeErrorCodes.UnsupportedInputAction,
                    "Text input target did not accept focus."));
            }
        }

        if (textBox.IsReadOnly)
        {
            return CoreResult<InputResponse>.Fail(new CoreError(
                BridgeErrorCodes.UnsupportedInputAction,
                "Text input target is read-only."));
        }

        var currentText = textBox.Text ?? string.Empty;
        var selectionStart = Math.Clamp(Math.Min(textBox.SelectionStart, textBox.SelectionEnd), 0, currentText.Length);
        var selectionEnd = Math.Clamp(Math.Max(textBox.SelectionStart, textBox.SelectionEnd), 0, currentText.Length);
        var caretIndex = Math.Clamp(textBox.CaretIndex, 0, currentText.Length);
        var insertIndex = selectionStart == selectionEnd ? caretIndex : selectionStart;
        var removeLength = selectionStart == selectionEnd ? 0 : selectionEnd - selectionStart;
        var updatedText = currentText.Remove(insertIndex, removeLength).Insert(insertIndex, inputText);
        var updatedCaretIndex = insertIndex + inputText.Length;

        textBox.Text = updatedText;
        textBox.CaretIndex = updatedCaretIndex;
        textBox.SelectionStart = updatedCaretIndex;
        textBox.SelectionEnd = updatedCaretIndex;

        return CoreResult<InputResponse>.Ok(new InputResponse(
            SessionId,
            topLevelId,
            InputActions.KeyText,
            handled: true,
            DateTimeOffset.UtcNow,
            CreateNodeId(textBox, TreeKinds.Visual)));
    }

    private CoreResult<InputResponse> ClearText(
        TopLevel topLevel,
        string topLevelId,
        string? targetNodeId)
    {
        TextBox? textBox;
        if (string.IsNullOrWhiteSpace(targetNodeId))
        {
            textBox = topLevel.FocusManager?.GetFocusedElement() as TextBox;
            if (textBox is null)
            {
                return CoreResult<InputResponse>.Fail(new CoreError(
                    BridgeErrorCodes.UnsupportedInputAction,
                    "Clear text input requires a focused TextBox target or target node id."));
            }
        }
        else
        {
            var resolved = ResolveInputTarget(topLevel, targetNodeId, null, null, "Clear text target node id is required.");
            if (!resolved.Success)
            {
                return CoreResult<InputResponse>.Fail(resolved.Error!);
            }

            textBox = resolved.Value as TextBox ?? (resolved.Value as Visual)?.FindAncestorOfType<TextBox>();
            if (textBox is null)
            {
                return CoreResult<InputResponse>.Fail(new CoreError(
                    BridgeErrorCodes.UnsupportedInputAction,
                    "Clear text target is not a TextBox."));
            }

            if (!textBox.IsFocused && !textBox.Focus(NavigationMethod.Unspecified))
            {
                return CoreResult<InputResponse>.Fail(new CoreError(
                    BridgeErrorCodes.UnsupportedInputAction,
                    "Clear text target did not accept focus."));
            }
        }

        if (textBox.IsReadOnly)
        {
            return CoreResult<InputResponse>.Fail(new CoreError(
                BridgeErrorCodes.UnsupportedInputAction,
                "Clear text target is read-only."));
        }

        textBox.Text = string.Empty;
        textBox.CaretIndex = 0;
        textBox.SelectionStart = 0;
        textBox.SelectionEnd = 0;

        return CoreResult<InputResponse>.Ok(new InputResponse(
            SessionId,
            topLevelId,
            InputActions.ClearText,
            handled: true,
            DateTimeOffset.UtcNow,
            CreateNodeId(textBox, TreeKinds.Visual)));
    }

    private CoreResult<InputResponse> FocusTarget(
        TopLevel topLevel,
        string topLevelId,
        string? targetNodeId,
        double? x,
        double? y)
    {
        var target = ResolveInputTarget(topLevel, targetNodeId, x, y, "Focus input requires targetNodeId or x/y coordinates.");
        if (!target.Success)
        {
            return CoreResult<InputResponse>.Fail(target.Error!);
        }

        var navigationMethod = string.IsNullOrWhiteSpace(targetNodeId)
            ? NavigationMethod.Pointer
            : NavigationMethod.Unspecified;
        if (!target.Value!.Focus(navigationMethod))
        {
            return CoreResult<InputResponse>.Fail(new CoreError(
                BridgeErrorCodes.UnsupportedInputAction,
                "Focus target did not accept focus."));
        }

        return CoreResult<InputResponse>.Ok(new InputResponse(
            SessionId,
            topLevelId,
            InputActions.Focus,
            handled: true,
            DateTimeOffset.UtcNow,
            CreateNodeId(target.Value, TreeKinds.Visual)));
    }

    private CoreResult<InputResponse> KeyInput(
        TopLevel topLevel,
        string topLevelId,
        string action,
        string? targetNodeId,
        string? inputKey,
        string? keyModifiers)
    {
        var key = ParseInputKey(inputKey);
        if (!key.Success)
        {
            return CoreResult<InputResponse>.Fail(key.Error!);
        }

        var modifiers = ParseKeyModifiers(keyModifiers);
        if (!modifiers.Success)
        {
            return CoreResult<InputResponse>.Fail(modifiers.Error!);
        }

        InputElement? target;
        if (string.IsNullOrWhiteSpace(targetNodeId))
        {
            target = topLevel.FocusManager?.GetFocusedElement() as InputElement;
            if (target is null)
            {
                return CoreResult<InputResponse>.Fail(new CoreError(
                    BridgeErrorCodes.UnsupportedInputAction,
                    "Key input requires a focused input element or target node id."));
            }
        }
        else
        {
            var resolved = ResolveInputTarget(topLevel, targetNodeId, null, null, "Key input target node id is required.");
            if (!resolved.Success)
            {
                return CoreResult<InputResponse>.Fail(resolved.Error!);
            }

            target = resolved.Value!;
            if (!target.IsFocused && !target.Focus(NavigationMethod.Unspecified))
            {
                return CoreResult<InputResponse>.Fail(new CoreError(
                    BridgeErrorCodes.UnsupportedInputAction,
                    "Key input target did not accept focus."));
            }
        }

        var routedEvent = action == InputActions.KeyDown
            ? InputElement.KeyDownEvent
            : InputElement.KeyUpEvent;

        target.RaiseEvent(new KeyEventArgs
        {
            RoutedEvent = routedEvent,
            Source = target,
            Key = key.Value,
            KeyModifiers = modifiers.Value,
            PhysicalKey = default,
            KeyDeviceType = KeyDeviceType.Keyboard
        });

        return CoreResult<InputResponse>.Ok(new InputResponse(
            SessionId,
            topLevelId,
            action,
            handled: true,
            DateTimeOffset.UtcNow,
            CreateNodeId(target, TreeKinds.Visual)));
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

    private static CoreResult<InputElement> ResolveInputTarget(
        TopLevel topLevel,
        string? targetNodeId,
        double? x,
        double? y,
        string missingTargetMessage)
    {
        if (!string.IsNullOrWhiteSpace(targetNodeId))
        {
            var node = FindNodeById(topLevel, targetNodeId.Trim());
            if (node is null)
            {
                return CoreResult<InputElement>.Fail(new CoreError(
                    BridgeErrorCodes.NodeNotFound,
                    $"Input target node '{targetNodeId}' was not found.",
                    CreateTargetErrorDetails(null, null, targetNodeId.Trim())));
            }

            var inputTarget = node as InputElement ?? (node as Visual)?.FindAncestorOfType<InputElement>();
            return inputTarget is null
                ? CoreResult<InputElement>.Fail(new CoreError(
                    BridgeErrorCodes.UnsupportedInputAction,
                    $"Input target node '{targetNodeId}' is not an input element."))
                : CoreResult<InputElement>.Ok(inputTarget);
        }

        if (x is null && y is null)
        {
            return CoreResult<InputElement>.Fail(new CoreError(
                BridgeErrorCodes.InvalidInputRequest,
                missingTargetMessage));
        }

        var point = GetInputPoint(x, y);
        if (!point.Success)
        {
            return CoreResult<InputElement>.Fail(point.Error!);
        }

        var visual = topLevel.GetVisualAt(point.Value);
        if (visual is null)
        {
            return CoreResult<InputElement>.Fail(new CoreError(
                BridgeErrorCodes.UnsupportedInputAction,
                "Input coordinates did not hit a visual target."));
        }

        var target = visual as InputElement ?? visual.FindAncestorOfType<InputElement>();
        return target is null
            ? CoreResult<InputElement>.Fail(new CoreError(
                BridgeErrorCodes.UnsupportedInputAction,
                "Input coordinates did not hit an input element."))
            : CoreResult<InputElement>.Ok(target);
    }

    private static CoreResult<Key> ParseInputKey(string? inputKey)
    {
        if (string.IsNullOrWhiteSpace(inputKey))
        {
            return CoreResult<Key>.Fail(new CoreError(
                BridgeErrorCodes.InvalidInputRequest,
                "Key input requires a non-empty input key."));
        }

        var normalizedKey = inputKey.Trim();
        if (!Enum.TryParse<Key>(normalizedKey, ignoreCase: true, out var key)
            || !Enum.IsDefined(key))
        {
            return CoreResult<Key>.Fail(new CoreError(
                BridgeErrorCodes.InvalidInputRequest,
                $"Input key '{inputKey}' is not a supported Avalonia key name."));
        }

        return CoreResult<Key>.Ok(key);
    }

    private static CoreResult<KeyModifiers> ParseKeyModifiers(string? keyModifiers)
    {
        if (string.IsNullOrWhiteSpace(keyModifiers))
        {
            return CoreResult<KeyModifiers>.Ok(KeyModifiers.None);
        }

        var modifiers = KeyModifiers.None;
        var tokens = keyModifiers.Split(
            [',', '+', '|', ' '],
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        foreach (var token in tokens)
        {
            var normalized = string.Equals(token, "ctrl", StringComparison.OrdinalIgnoreCase)
                ? "Control"
                : token;
            if (!Enum.TryParse<KeyModifiers>(normalized, ignoreCase: true, out var parsed))
            {
                return CoreResult<KeyModifiers>.Fail(new CoreError(
                    BridgeErrorCodes.InvalidInputRequest,
                    $"Key modifier '{token}' is not supported."));
            }

            modifiers |= parsed;
        }

        return CoreResult<KeyModifiers>.Ok(modifiers);
    }

    private static object? FindNodeById(TopLevel topLevel, string targetNodeId)
    {
        if (targetNodeId.StartsWith($"{TreeKinds.Visual}:", StringComparison.Ordinal))
        {
            return FindVisualNodeById(topLevel, targetNodeId);
        }

        return targetNodeId.StartsWith($"{TreeKinds.Logical}:", StringComparison.Ordinal)
            && topLevel is ILogical logical
            ? FindLogicalNodeById(logical, targetNodeId)
            : null;
    }

    private static Visual? FindVisualNodeById(Visual visual, string targetNodeId)
    {
        if (string.Equals(CreateNodeId(visual, TreeKinds.Visual), targetNodeId, StringComparison.Ordinal))
        {
            return visual;
        }

        foreach (var child in visual.GetVisualChildren())
        {
            var match = FindVisualNodeById(child, targetNodeId);
            if (match is not null)
            {
                return match;
            }
        }

        return null;
    }

    private static ILogical? FindLogicalNodeById(ILogical logical, string targetNodeId)
    {
        if (string.Equals(CreateNodeId(logical, TreeKinds.Logical), targetNodeId, StringComparison.Ordinal))
        {
            return logical;
        }

        foreach (var child in logical.GetLogicalChildren())
        {
            var match = FindLogicalNodeById(child, targetNodeId);
            if (match is not null)
            {
                return match;
            }
        }

        return null;
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

    private static CoreResult<InspectNodeResponse> InvalidInspectRequest(string message)
    {
        return CoreResult<InspectNodeResponse>.Fail(new CoreError(BridgeErrorCodes.InvalidInspectRequest, message));
    }

    private static CoreResult<InspectNodeResponse> NodeNotFound(
        string topLevelId,
        string treeKind,
        string nodeId)
    {
        return CoreResult<InspectNodeResponse>.Fail(new CoreError(
            BridgeErrorCodes.NodeNotFound,
            $"Node '{nodeId}' was not found in the {treeKind} tree for top-level '{topLevelId}'.",
            CreateTargetErrorDetails(topLevelId, treeKind, nodeId)));
    }

    private static CoreResult<TreeResponse> InvalidTreeKind(string topLevelId, string treeKind)
    {
        return CoreResult<TreeResponse>.Fail(new CoreError(
            BridgeErrorCodes.InvalidFindRequest,
            $"Tree kind '{treeKind}' is not supported for top-level '{topLevelId}'.",
            new Dictionary<string, string>
            {
                ["topLevelId"] = topLevelId,
                ["treeKind"] = treeKind,
                ["supportedTreeKinds"] = $"{TreeKinds.Visual},{TreeKinds.Logical}",
                ["nextAction"] = "Use treeKind 'visual' or 'logical' from the target context returned by visual-tree, logical-tree, find-nodes, or inspect-node."
            }));
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
            new CoreError(
                BridgeErrorCodes.TopLevelNotFound,
                $"Top-level '{topLevelId}' was not found.",
                new Dictionary<string, string>
                {
                    ["topLevelId"] = topLevelId,
                    ["nextAction"] = "Call list-top-levels again and retry with a current topLevelId from the returned runtime target context."
                }));
    }

    private TreeNodeSummary SerializeVisualNode(Visual visual, string topLevelId, int depth, int maxDepth)
    {
        var children = depth >= maxDepth
            ? Array.Empty<TreeNodeSummary>()
            : visual.GetVisualChildren()
                .Select(child => SerializeVisualNode(child, topLevelId, depth + 1, maxDepth))
                .ToArray();

        return CreateNodeSummary(topLevelId, visual, TreeKinds.Visual, children);
    }

    private TreeNodeSummary SerializeLogicalNode(ILogical logical, string topLevelId, int depth, int maxDepth)
    {
        var children = depth >= maxDepth
            ? Array.Empty<TreeNodeSummary>()
            : logical.GetLogicalChildren()
                .Select(child => SerializeLogicalNode(child, topLevelId, depth + 1, maxDepth))
                .ToArray();

        return CreateNodeSummary(topLevelId, logical, TreeKinds.Logical, children);
    }

    private TreeNodeSummary CreateNodeSummary(
        string topLevelId,
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
            children,
            CreateNodeTarget(topLevelId, treeKind, node));
    }

    private RuntimeTargetContext CreateTopLevelTarget(string topLevelId)
    {
        return new RuntimeTargetContext(SessionId, topLevelId);
    }

    private RuntimeTargetContext CreateNodeTarget(string topLevelId, string treeKind, object node)
    {
        return new RuntimeTargetContext(SessionId, topLevelId, treeKind, CreateNodeId(node, treeKind));
    }

    private static IReadOnlyDictionary<string, string> CreateTargetErrorDetails(
        string? topLevelId,
        string? treeKind,
        string nodeId)
    {
        var details = new Dictionary<string, string>
        {
            ["nodeId"] = nodeId,
            ["nextAction"] = "Refresh the relevant visual-tree, logical-tree, or find-nodes result and retry with the current target object."
        };

        if (!string.IsNullOrWhiteSpace(topLevelId))
        {
            details["topLevelId"] = topLevelId;
        }

        if (!string.IsNullOrWhiteSpace(treeKind))
        {
            details["treeKind"] = treeKind;
        }

        return details;
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

    private static IReadOnlyList<ComputedPropertyValue> GetComputedProperties(object node)
    {
        if (node is not AvaloniaObject avaloniaObject)
        {
            return Array.Empty<ComputedPropertyValue>();
        }

        var properties = GetInspectableProperties(node).ToArray();
        var values = new List<ComputedPropertyValue>(properties.Length);
        foreach (var property in properties)
        {
            try
            {
                var diagnostic = avaloniaObject.GetDiagnostic(property);
                values.Add(new ComputedPropertyValue(
                    property.Name,
                    FormatComputedValue(diagnostic.Value),
                    diagnostic.Value?.GetType().FullName ?? "null",
                    diagnostic.Priority.ToString(),
                    MapComputedSource(diagnostic.Priority),
                    diagnostic.Diagnostic));
            }
            catch (InvalidOperationException)
            {
                values.Add(new ComputedPropertyValue(
                    property.Name,
                    "not_available",
                    "not_available",
                    source: "not_available"));
            }
        }

        return values;
    }

    private static IEnumerable<AvaloniaProperty> GetInspectableProperties(object node)
    {
        if (node is Visual)
        {
            yield return Visual.BoundsProperty;
            yield return Visual.ClipToBoundsProperty;
        }

        if (node is Layoutable)
        {
            yield return Layoutable.MarginProperty;
            yield return Layoutable.DesiredSizeProperty;
        }

        if (node is Border)
        {
            yield return Border.BackgroundProperty;
            yield return Border.BorderBrushProperty;
            yield return Border.PaddingProperty;
        }

        if (node is Panel)
        {
            yield return Panel.BackgroundProperty;
        }

        if (node is TemplatedControl)
        {
            yield return TemplatedControl.BackgroundProperty;
            yield return TemplatedControl.BorderBrushProperty;
            yield return TemplatedControl.ForegroundProperty;
            yield return TemplatedControl.FontFamilyProperty;
            yield return TemplatedControl.FontSizeProperty;
            yield return TemplatedControl.FontStyleProperty;
            yield return TemplatedControl.FontWeightProperty;
            yield return TemplatedControl.PaddingProperty;
        }

        if (node is TextBlock)
        {
            yield return TextBlock.ForegroundProperty;
            yield return TextBlock.FontFamilyProperty;
            yield return TextBlock.FontSizeProperty;
            yield return TextBlock.FontStyleProperty;
            yield return TextBlock.FontWeightProperty;
        }
    }

    private static string FormatComputedValue(object? value)
    {
        return value switch
        {
            null => "null",
            IBrush brush => brush.ToString() ?? brush.GetType().FullName ?? "brush",
            IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
            _ => value.ToString() ?? value.GetType().FullName ?? "value"
        };
    }

    private static string MapComputedSource(Avalonia.Data.BindingPriority priority)
    {
        return priority switch
        {
            Avalonia.Data.BindingPriority.Animation => "animation",
            Avalonia.Data.BindingPriority.LocalValue => "local",
            Avalonia.Data.BindingPriority.StyleTrigger => "style",
            Avalonia.Data.BindingPriority.Template => "template",
            Avalonia.Data.BindingPriority.Style => "style",
            Avalonia.Data.BindingPriority.Inherited => "inherited",
            Avalonia.Data.BindingPriority.Unset => "default",
            _ => "unknown"
        };
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
