using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Presenters;
using Avalonia.Data;
using Avalonia.Diagnostics;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.LogicalTree;
using Avalonia.Markup.Xaml.Diagnostics;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using AvaScope.Core;
using AvaScope.Protocol;
using System.Collections.Concurrent;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

namespace AvaScope.Bridge;

public sealed class AvaScopeBridgeRuntime
{
    private const int DefaultTreeDepth = 10;
    private const int DefaultFindResultLimit = 100;
    private const int MaximumTreeDepth = 64;
    private const int MaximumFindResultLimit = 1000;
    private const int MaximumDebugFieldCount = 32;
    private const int MaximumDebugValueLength = 500;
    private const int MaximumRuntimeSourceBindings = 32;
    private const int MaximumRuntimeLayoutAncestors = 16;
    private const int MaximumMutationHistoryEntries = 128;
    private const int DefaultMutationReviewLimit = 50;
    private const string MutationValueKindBool = "bool";
    private const string MutationValueKindBrush = "brush";
    private const string MutationValueKindDouble = "double";
    private const string MutationValueKindLayoutDouble = "layout_double";
    private const string MutationValueKindMaxLayoutDouble = "max_layout_double";
    private const string MutationValueKindOpacity = "opacity";
    private const string MutationValueKindString = "string";
    private const string MutationValueKindThickness = "thickness";
    private readonly ConcurrentDictionary<int, WeakReference<TopLevel>> _registeredTopLevels = new();
    private readonly ConcurrentDictionary<string, Pointer> _activePointers = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, AppliedRuntimeMutation> _activeMutations = new(StringComparer.Ordinal);
    private readonly ConcurrentQueue<RuntimeMutationReviewEntry> _mutationHistory = new();
    private readonly SessionRegistry _sessionRegistry;
    private long _mutationSequence;
    private long _mutationHistorySequence;
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
        var topLevelId = InspectableTopLevel.CreateId(topLevel);
        _registeredTopLevels[key] = new WeakReference<TopLevel>(topLevel);

        return new TopLevelRegistration(() => UnregisterTopLevel(key, topLevelId));
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
        CancellationToken cancellationToken = default,
        bool includeChildren = false,
        bool includeBounds = true,
        bool includeAccessibility = false,
        bool includeBindings = false,
        int? maxResponseDepth = null)
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
                maxResults,
                includeChildren,
                includeBounds,
                includeAccessibility,
                includeBindings,
                maxResponseDepth));
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
                    maxResults,
                    includeChildren,
                    includeBounds,
                    includeAccessibility,
                    includeBindings,
                    maxResponseDepth),
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

    public Task<CoreResult<LayoutExplainResponse>> ExplainLayoutAsync(
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
            return Task.FromResult(ExplainLayout(topLevelId, treeKind, nodeId));
        }

        return Dispatcher.UIThread
            .InvokeAsync(
                () => ExplainLayout(topLevelId, treeKind, nodeId),
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

    public Task<CoreResult<InputResponse>> ValidateInputAsync(
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
            return Task.FromResult(ValidateInput(
                topLevelId, action, x, y, inputText, targetNodeId, inputKey, keyModifiers));
        }

        return Dispatcher.UIThread
            .InvokeAsync(
                () => ValidateInput(
                    topLevelId, action, x, y, inputText, targetNodeId, inputKey, keyModifiers),
                DispatcherPriority.Background,
                cancellationToken)
            .GetTask();
    }

    public Task<CoreResult<RuntimeMutationResponse>> MutateNodeAsync(
        RuntimeMutationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (Dispatcher.UIThread.CheckAccess())
        {
            return Task.FromResult(MutateNode(request));
        }

        return Dispatcher.UIThread
            .InvokeAsync(() => MutateNode(request), DispatcherPriority.Background, cancellationToken)
            .GetTask();
    }

    public Task<CoreResult<RuntimeMutationResponse>> ValidateMutationAsync(
        RuntimeMutationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (Dispatcher.UIThread.CheckAccess())
        {
            return Task.FromResult(EvaluateMutation(request, validateOnly: true));
        }

        return Dispatcher.UIThread
            .InvokeAsync(
                () => EvaluateMutation(request, validateOnly: true),
                DispatcherPriority.Background,
                cancellationToken)
            .GetTask();
    }

    public Task<CoreResult<RuntimeMutationReviewResponse>> MutationReviewAsync(
        int? maxResults = null,
        CancellationToken cancellationToken = default)
    {
        if (Dispatcher.UIThread.CheckAccess())
        {
            return Task.FromResult(CreateMutationReview(maxResults));
        }

        return Dispatcher.UIThread
            .InvokeAsync(() => CreateMutationReview(maxResults), DispatcherPriority.Background, cancellationToken)
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
        ResetActiveMutationsOnUiThread(static _ => true);
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
                bitmap.Save(stream, PngBitmapEncoderOptions.Default);
            }

            return CoreResult<ScreenshotResponse>.Ok(new ScreenshotResponse(
                SessionId,
                topLevelId,
                fullPath,
                pixelSize.Width,
                pixelSize.Height,
                DateTimeOffset.UtcNow,
                CreateTopLevelTarget(topLevelId, topLevel)));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or NotSupportedException or InvalidOperationException)
        {
            return CoreResult<ScreenshotResponse>.Fail(
                new CoreError(BridgeErrorCodes.ScreenshotCaptureFailed, exception.Message));
        }
    }

    private CoreResult<TreeResponse> GetVisualTree(
        string topLevelId,
        int? maxDepth,
        bool applyResponseBudget = true)
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

        var response = new TreeResponse(
            SessionId,
            topLevelId,
            TreeKinds.Visual,
            normalizedDepth,
            SerializeVisualNode(topLevel, topLevelId, topLevel, depth: 0, normalizedDepth),
            CreateTreeTarget(topLevelId, TreeKinds.Visual, topLevel));
        return CoreResult<TreeResponse>.Ok(
            applyResponseBudget ? ResponseBudgeter.Apply(response) : response);
    }

    private CoreResult<TreeResponse> GetLogicalTree(
        string topLevelId,
        int? maxDepth,
        bool applyResponseBudget = true)
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

        var response = new TreeResponse(
            SessionId,
            topLevelId,
            TreeKinds.Logical,
            normalizedDepth,
            SerializeLogicalNode(topLevel, topLevelId, topLevel, depth: 0, normalizedDepth),
            CreateTreeTarget(topLevelId, TreeKinds.Logical, topLevel));
        return CoreResult<TreeResponse>.Ok(
            applyResponseBudget ? ResponseBudgeter.Apply(response) : response);
    }

    private CoreResult<FindNodesResponse> FindNodes(
        string topLevelId,
        string treeKind,
        string? nodeType,
        string? name,
        string? automationId,
        string? text,
        int? maxDepth,
        int? maxResults,
        bool includeChildren,
        bool includeBounds,
        bool includeAccessibility,
        bool includeBindings,
        int? maxResponseDepth)
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
            TreeKinds.Visual => GetVisualTree(topLevelId, maxDepth, applyResponseBudget: false),
            TreeKinds.Logical => GetLogicalTree(topLevelId, maxDepth, applyResponseBudget: false),
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
            resultLimit.Value,
            includeChildren,
            includeBounds,
            includeAccessibility,
            includeBindings,
            Math.Clamp(maxResponseDepth ?? 1, 0, 8));

        return CoreResult<FindNodesResponse>.Ok(ResponseBudgeter.Apply(new FindNodesResponse(
            SessionId,
            topLevelId,
            treeKind,
            treeResult.Value.DepthLimit,
            matches,
            treeResult.Value.Target)));
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
            return InvalidInspectRequest<InspectNodeResponse>("Node id cannot be empty.");
        }

        return treeKind switch
        {
            TreeKinds.Visual => InspectVisualNode(topLevel, topLevelId, nodeId),
            TreeKinds.Logical => topLevel is ILogical logical
                ? InspectLogicalNode(logical, topLevel, topLevelId, nodeId)
                : NodeNotFound(topLevelId, treeKind, nodeId),
            _ => InvalidInspectRequest($"Tree kind '{treeKind}' is not supported for top-level '{topLevelId}'.")
        };
    }

    private CoreResult<InspectNodeResponse> InspectVisualNode(TopLevel topLevel, string topLevelId, string nodeId)
    {
        var node = FindVisualNodeById(topLevel, nodeId);
        if (node is null)
        {
            return NodeNotFound(topLevelId, TreeKinds.Visual, nodeId);
        }

        var computedProperties = GetComputedProperties(node);
        var sourceMap = CreateRuntimeSourceMap(node, computedProperties);
        var target = CreateNodeTarget(topLevelId, TreeKinds.Visual, topLevel, node);
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
            GetTreeNodeBounds(topLevel, node),
            GetClasses(node),
            computedProperties,
            target,
            GetScrollState(topLevel, topLevelId, node),
            GetBindingState(node, sourceMap, computedProperties),
            GetDebugState(node),
            GetAccessibilityState(node),
            GetValidationState(node),
            sourceMap,
            CreateLayoutExplanation(topLevel, topLevelId, node, target)));
    }

    private CoreResult<InspectNodeResponse> InspectLogicalNode(ILogical root, TopLevel topLevel, string topLevelId, string nodeId)
    {
        var node = FindLogicalNodeById(root, nodeId);
        if (node is null)
        {
            return NodeNotFound(topLevelId, TreeKinds.Logical, nodeId);
        }

        var computedProperties = GetComputedProperties(node);
        var sourceMap = CreateRuntimeSourceMap(node, computedProperties);
        var target = CreateNodeTarget(topLevelId, TreeKinds.Logical, topLevel, node);
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
            computedProperties,
            target,
            GetScrollState(topLevel, topLevelId, node),
            GetBindingState(node, sourceMap, computedProperties),
            GetDebugState(node),
            GetAccessibilityState(node),
            GetValidationState(node),
            sourceMap,
            CreateLayoutExplanation(topLevel, topLevelId, node, target)));
    }

    private CoreResult<LayoutExplainResponse> ExplainLayout(string topLevelId, string treeKind, string nodeId)
    {
        Dispatcher.UIThread.VerifyAccess();

        var topLevel = FindTopLevel(topLevelId);
        if (topLevel is null)
        {
            return TopLevelNotFound<LayoutExplainResponse>(topLevelId);
        }

        if (string.IsNullOrWhiteSpace(nodeId))
        {
            return InvalidInspectRequest<LayoutExplainResponse>("Node id cannot be empty.");
        }

        object? node = treeKind switch
        {
            TreeKinds.Visual => FindVisualNodeById(topLevel, nodeId),
            TreeKinds.Logical => topLevel is ILogical logical ? FindLogicalNodeById(logical, nodeId) : null,
            _ => null
        };

        if (node is null)
        {
            return treeKind is TreeKinds.Visual or TreeKinds.Logical
                ? NodeNotFound<LayoutExplainResponse>(topLevelId, treeKind, nodeId)
                : InvalidInspectRequest<LayoutExplainResponse>($"Tree kind '{treeKind}' is not supported for top-level '{topLevelId}'.");
        }

        var target = CreateNodeTarget(topLevelId, treeKind, topLevel, node);
        return CoreResult<LayoutExplainResponse>.Ok(new LayoutExplainResponse(
            SessionId,
            topLevelId,
            treeKind,
            CreateNodeId(node, treeKind),
            CreateLayoutExplanation(topLevel, topLevelId, node, target),
            target));
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
            InputActions.Click => Click(topLevel, topLevelId, x, y, targetNodeId),
            InputActions.KeyText => KeyText(topLevel, topLevelId, targetNodeId, inputText),
            InputActions.ClearText => ClearText(topLevel, topLevelId, targetNodeId),
            InputActions.Focus => FocusTarget(topLevel, topLevelId, targetNodeId, x, y),
            InputActions.KeyDown => KeyInput(topLevel, topLevelId, InputActions.KeyDown, targetNodeId, inputKey, keyModifiers),
            InputActions.KeyUp => KeyInput(topLevel, topLevelId, InputActions.KeyUp, targetNodeId, inputKey, keyModifiers),
            InputActions.Invoke => SemanticAutomationAction(topLevel, topLevelId, InputActions.Invoke, targetNodeId),
            InputActions.Select when string.IsNullOrWhiteSpace(inputText)
                => SemanticAutomationAction(topLevel, topLevelId, InputActions.Select, targetNodeId),
            InputActions.Select => SelectTarget(topLevel, topLevelId, targetNodeId, inputText),
            InputActions.Toggle => SemanticAutomationAction(topLevel, topLevelId, InputActions.Toggle, targetNodeId),
            InputActions.Expand => SemanticAutomationAction(topLevel, topLevelId, InputActions.Expand, targetNodeId),
            InputActions.Collapse => SemanticAutomationAction(topLevel, topLevelId, InputActions.Collapse, targetNodeId),
            InputActions.Scroll => ScrollTarget(topLevel, topLevelId, targetNodeId, x, y),
            _ => CoreResult<InputResponse>.Fail(new CoreError(
                BridgeErrorCodes.UnsupportedInputAction,
                $"Input action '{action}' is not supported.",
                new Dictionary<string, string>
                {
                    ["supportedActions"] = string.Join(",", InputActions.All),
                    ["nextAction"] = "Read runtime.input capability metadata for parameter requirements and examples."
                }))
        };
    }

    private CoreResult<InputResponse> ValidateInput(
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

        if (!InputActions.All.Contains(action, StringComparer.Ordinal))
        {
            return CoreResult<InputResponse>.Fail(new CoreError(
                BridgeErrorCodes.UnsupportedInputAction,
                $"Input action '{action}' is not supported.",
                new Dictionary<string, string>
                {
                    ["supportedActions"] = string.Join(",", InputActions.All)
                }));
        }

        if (action == InputActions.Click)
        {
            return Click(topLevel, topLevelId, x, y, targetNodeId, validateOnly: true);
        }

        if (action is InputActions.PointerMove or InputActions.PointerDown or InputActions.PointerUp)
        {
            var point = GetInputPoint(x, y);
            if (!point.Success)
            {
                return CoreResult<InputResponse>.Fail(point.Error!);
            }

            var pointerTarget = ResolveInputTarget(
                topLevel,
                targetNodeId: null,
                x,
                y,
                $"Input action '{action}' requires x/y coordinates.");
            return pointerTarget.Success
                ? ValidatedInput(topLevelId, action, pointerTarget.Value!)
                : CoreResult<InputResponse>.Fail(pointerTarget.Error!);
        }

        if (action is InputActions.Invoke or InputActions.Toggle
            or InputActions.Expand or InputActions.Collapse
            || action == InputActions.Select && string.IsNullOrWhiteSpace(inputText))
        {
            return SemanticAutomationAction(
                topLevel,
                topLevelId,
                action,
                targetNodeId,
                validateOnly: true);
        }

        if (action == InputActions.KeyText && string.IsNullOrEmpty(inputText))
        {
            return Invalid("Text input requires non-empty input text.");
        }

        if (action is InputActions.KeyDown or InputActions.KeyUp)
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
        }

        var target = ResolveInputTarget(
            topLevel,
            targetNodeId,
            x,
            y,
            $"Input action '{action}' requires a target node id or x/y coordinates.");
        if (!target.Success)
        {
            return CoreResult<InputResponse>.Fail(target.Error!);
        }

        var inputTarget = target.Value!;
        if (action == InputActions.Focus && !inputTarget.Focusable)
        {
            return Unsupported("Focus target is not focusable.");
        }

        if ((action is InputActions.KeyDown or InputActions.KeyUp)
            && (!inputTarget.Focusable || !inputTarget.IsEnabled || !inputTarget.IsVisible))
        {
            return Unsupported("Key input target cannot accept focus.");
        }

        if (action is InputActions.KeyText or InputActions.ClearText)
        {
            var textBox = inputTarget as TextBox
                ?? (inputTarget as Visual)?.FindAncestorOfType<TextBox>();
            if (textBox is null)
            {
                return Unsupported("Text input target is not a TextBox.");
            }

            if (textBox.IsReadOnly)
            {
                return Unsupported("Text input target is read-only.");
            }

            if (!textBox.Focusable || !textBox.IsEnabled || !textBox.IsVisible)
            {
                return Unsupported("Text input target cannot accept focus.");
            }

            inputTarget = textBox;
        }

        if (action == InputActions.Select && !string.IsNullOrWhiteSpace(inputText))
        {
            var selector = inputTarget as SelectingItemsControl
                ?? (inputTarget as Visual)?.FindAncestorOfType<SelectingItemsControl>();
            if (selector is null)
            {
                return Unsupported("Select target is not a SelectingItemsControl.");
            }

            if (TryResolveSelectionIndex(selector, inputText.Trim()) is null)
            {
                return Unsupported($"Select target does not contain item '{inputText.Trim()}'.");
            }

            inputTarget = selector;
        }

        if (action == InputActions.Scroll)
        {
            if (x is null && y is null)
            {
                return Invalid("Scroll input requires x or y delta.");
            }

            var viewer = inputTarget as ScrollViewer
                ?? (inputTarget as Visual)?.FindAncestorOfType<ScrollViewer>();
            if (viewer is null)
            {
                return Unsupported("Scroll input requires a ScrollViewer target.");
            }

            inputTarget = viewer;
        }

        return ValidatedInput(topLevelId, action, inputTarget);

        static CoreResult<InputResponse> Invalid(string message) =>
            CoreResult<InputResponse>.Fail(new CoreError(BridgeErrorCodes.InvalidInputRequest, message));

        static CoreResult<InputResponse> Unsupported(string message) =>
            CoreResult<InputResponse>.Fail(new CoreError(BridgeErrorCodes.UnsupportedInputAction, message));
    }

    private CoreResult<InputResponse> ValidatedInput(
        string topLevelId,
        string action,
        InputElement target) =>
        CoreResult<InputResponse>.Ok(new InputResponse(
            SessionId,
            topLevelId,
            action,
            handled: false,
            DateTimeOffset.UtcNow,
            CreateNodeId(target, TreeKinds.Visual),
            new RuntimeTargetContext(
                SessionId,
                topLevelId,
                TreeKinds.Visual,
                CreateNodeId(target, TreeKinds.Visual)),
            metadata: new Dictionary<string, string>
            {
                ["dryRun"] = "true",
                ["validationStatus"] = "validated",
                ["targetType"] = target.GetType().FullName ?? target.GetType().Name
            }));

    private CoreResult<RuntimeMutationResponse> MutateNode(RuntimeMutationRequest request)
    {
        Dispatcher.UIThread.VerifyAccess();

        var result = EvaluateMutation(request);
        if (result.Success && result.Value is not null)
        {
            RecordMutationResponse(result.Value);
        }

        return result;
    }

    private CoreResult<RuntimeMutationResponse> EvaluateMutation(
        RuntimeMutationRequest request,
        bool validateOnly = false)
    {
        Dispatcher.UIThread.VerifyAccess();

        var session = _sessionRegistry.Get(SessionId);
        if (!session.Success || session.Value!.State is not SessionLifecycleState.Active)
        {
            return MutationResponse(
                request,
                RuntimeMutationStatuses.Unavailable,
                applied: false,
                request.Target,
                new ProtocolError(
                    CoreErrorCodes.SessionClosed,
                    "Runtime mutation session is closed.",
                    new Dictionary<string, string>
                    {
                        ["sessionId"] = SessionId.Value,
                        ["nextAction"] = "Attach to an active local bridge session before mutating runtime UI."
                    }));
        }

        if (request.Target.SessionId != SessionId)
        {
            return MutationResponse(
                request,
                RuntimeMutationStatuses.Unavailable,
                applied: false,
                request.Target,
                new ProtocolError(
                    RuntimeMutationErrorCodes.RuntimeMutationNonLocalSession,
                    "Runtime mutation target belongs to a different session.",
                    new Dictionary<string, string>
                    {
                        ["currentSessionId"] = SessionId.Value,
                        ["targetSessionId"] = request.Target.SessionId.Value,
                        ["nextAction"] = "Refresh the target context from this bridge session before retrying."
                    }));
        }

        var topLevel = FindTopLevel(request.Target.TopLevelId);
        if (topLevel is null)
        {
            return MutationResponse(
                request,
                RuntimeMutationStatuses.StaleTarget,
                applied: false,
                request.Target,
                CreateStaleMutationDiagnostic(
                    "Top-level target is no longer available.",
                    request.Target,
                    "Call list-top-levels and refresh the visual or logical tree before retrying."));
        }

        var targetResult = ResolveMutationTarget(topLevel, request.Target);
        if (!targetResult.Success)
        {
            return MutationResponse(
                request,
                RuntimeMutationStatuses.StaleTarget,
                applied: false,
                request.Target,
                ToProtocolError(targetResult.Error!));
        }

        var currentTarget = targetResult.Value!.Target;
        var validation = ValidateMutationOperation(request.Operation);
        if (validation is not null)
        {
            return MutationResponse(
                request,
                MutationStatusForDiagnostic(validation.Code),
                applied: false,
                currentTarget,
                validation);
        }

        return validateOnly
            ? ValidateMutationTarget(request, targetResult.Value!)
            : ApplyMutation(request, targetResult.Value!);
    }

    private CoreResult<RuntimeMutationResponse> ApplyMutation(
        RuntimeMutationRequest request,
        ResolvedMutationTarget resolvedTarget)
    {
        return request.Operation.Kind switch
        {
            RuntimeMutationOperationKinds.NoOp => MutationResponse(
                request,
                RuntimeMutationStatuses.NoOp,
                applied: false,
                resolvedTarget.Target),
            RuntimeMutationOperationKinds.SetProperty => ApplyPropertyMutation(request, resolvedTarget),
            RuntimeMutationOperationKinds.AddClass
                or RuntimeMutationOperationKinds.RemoveClass
                or RuntimeMutationOperationKinds.ToggleClass => ApplyClassMutation(request, resolvedTarget),
            RuntimeMutationOperationKinds.SetResource
                or RuntimeMutationOperationKinds.RemoveResource => ApplyResourceMutation(request, resolvedTarget),
            RuntimeMutationOperationKinds.ResetMutation => ResetMutation(request, resolvedTarget.Target),
            RuntimeMutationOperationKinds.ResetAll => ResetAllMutations(request, resolvedTarget.Target),
            _ => MutationResponse(
                request,
                RuntimeMutationStatuses.Unsupported,
                applied: false,
                resolvedTarget.Target,
                new ProtocolError(
                    RuntimeMutationErrorCodes.UnsupportedRuntimeMutationOperation,
                    $"Runtime mutation operation '{request.Operation.Kind}' is not supported."))
        };
    }

    private CoreResult<RuntimeMutationResponse> ValidateMutationTarget(
        RuntimeMutationRequest request,
        ResolvedMutationTarget resolvedTarget)
    {
        CoreError? error = request.Operation.Kind switch
        {
            RuntimeMutationOperationKinds.SetProperty => ValidatePropertyMutation(
                resolvedTarget.Node,
                request.Operation),
            RuntimeMutationOperationKinds.AddClass
                or RuntimeMutationOperationKinds.RemoveClass
                or RuntimeMutationOperationKinds.ToggleClass
                when resolvedTarget.Node is not StyledElement => new CoreError(
                    RuntimeMutationErrorCodes.UnsupportedRuntimeMutationProperty,
                    "Class mutations require a StyledElement target."),
            RuntimeMutationOperationKinds.SetResource
                or RuntimeMutationOperationKinds.RemoveResource
                when resolvedTarget.Node is not StyledElement => new CoreError(
                    RuntimeMutationErrorCodes.UnsupportedRuntimeMutationProperty,
                    "Resource mutations require a StyledElement target."),
            RuntimeMutationOperationKinds.SetResource => ValidateResourceMutation(request.Operation),
            RuntimeMutationOperationKinds.ResetMutation
                when !_activeMutations.ContainsKey(request.Operation.MutationId!) => new CoreError(
                    RuntimeMutationErrorCodes.RuntimeMutationResetTargetNotFound,
                    $"Runtime mutation '{request.Operation.MutationId}' is not active and cannot be reset."),
            _ => null
        };

        if (error is not null)
        {
            return MutationResponse(
                request,
                MutationStatusForDiagnostic(error.Code),
                applied: false,
                resolvedTarget.Target,
                ToProtocolError(error));
        }

        return MutationResponseWithMetadata(
            request,
            RuntimeMutationStatuses.Validated,
            applied: false,
            resolvedTarget.Target,
            metadata: new Dictionary<string, string>
            {
                ["dryRun"] = "true",
                ["validationStatus"] = RuntimeMutationStatuses.Validated,
                ["targetType"] = resolvedTarget.Node.GetType().FullName
                    ?? resolvedTarget.Node.GetType().Name
            });
    }

    private static CoreError? ValidatePropertyMutation(
        object node,
        RuntimeMutationOperation operation)
    {
        var property = ResolveMutableProperty(node, operation.PropertyName!);
        if (!property.Success)
        {
            return property.Error;
        }

        var value = ConvertMutationValue(operation, property.Value!.ValueKind);
        return value.Success ? null : value.Error;
    }

    private static CoreError? ValidateResourceMutation(RuntimeMutationOperation operation)
    {
        var value = ConvertResourceMutationValue(operation);
        return value.Success ? null : value.Error;
    }

    private CoreResult<RuntimeMutationResponse> ApplyPropertyMutation(
        RuntimeMutationRequest request,
        ResolvedMutationTarget resolvedTarget)
    {
        var propertyResult = ResolveMutableProperty(resolvedTarget.Node, request.Operation.PropertyName!);
        if (!propertyResult.Success)
        {
            return MutationResponse(
                request,
                MutationStatusForDiagnostic(propertyResult.Error!.Code),
                applied: false,
                resolvedTarget.Target,
                ToProtocolError(propertyResult.Error));
        }

        var property = propertyResult.Value!;
        var valueResult = ConvertMutationValue(request.Operation, property.ValueKind);
        if (!valueResult.Success)
        {
            return MutationResponse(
                request,
                RuntimeMutationStatuses.Rejected,
                applied: false,
                resolvedTarget.Target,
                ToProtocolError(valueResult.Error!));
        }

        var original = CapturePropertySnapshot(property.Owner, property.Property);
        var identity = NextMutationIdentity();
        var appliedAt = DateTimeOffset.UtcNow;

        try
        {
            property.Owner.SetValue(property.Property, valueResult.Value!.Value!, BindingPriority.LocalValue);
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidCastException or InvalidOperationException or NotSupportedException)
        {
            return MutationResponse(
                request,
                RuntimeMutationStatuses.Rejected,
                applied: false,
                resolvedTarget.Target,
                CreateInvalidMutationValueDiagnostic(
                    request.Operation,
                    $"Value '{request.Operation.Value}' could not be assigned to property '{property.PropertyName}': {exception.Message}"));
        }

        var effective = CapturePropertySnapshot(property.Owner, property.Property);
        var metadata = CreateAppliedMutationMetadata(
            identity.Id,
            request.Operation,
            resolvedTarget,
            property.PropertyName,
            original,
            effective,
            appliedAt);
        var shouldRestoreLocalValue = original.Priority == BindingPriority.LocalValue || original.HasLocalValue;
        var activeMutation = new AppliedRuntimeMutation(
            identity.Id,
            identity.Sequence,
            request.RequestId,
            request.Operation,
            request.Operation.Kind,
            resolvedTarget.Target.TopLevelId,
            resolvedTarget.Target.TreeKind,
            resolvedTarget.Target.NodeId,
            resolvedTarget.Node.GetType().FullName ?? resolvedTarget.Node.GetType().Name,
            appliedAt,
            metadata,
            () => ResetAvaloniaProperty(property.Owner, property.Property, original.Value, shouldRestoreLocalValue));

        _activeMutations[identity.Id] = activeMutation;

        return MutationResponseWithMetadata(
            request,
            RuntimeMutationStatuses.Applied,
            applied: true,
            resolvedTarget.Target,
            identity.Id,
            metadata);
    }

    private CoreResult<RuntimeMutationResponse> ApplyClassMutation(
        RuntimeMutationRequest request,
        ResolvedMutationTarget resolvedTarget)
    {
        if (resolvedTarget.Node is not StyledElement styledElement)
        {
            return MutationResponse(
                request,
                RuntimeMutationStatuses.Unsupported,
                applied: false,
                resolvedTarget.Target,
                new ProtocolError(
                    RuntimeMutationErrorCodes.UnsupportedRuntimeMutationProperty,
                    "Class mutations require a StyledElement target.",
                    new Dictionary<string, string>
                    {
                        ["operation"] = request.Operation.Kind,
                        ["nodeType"] = resolvedTarget.Node.GetType().FullName ?? resolvedTarget.Node.GetType().Name,
                        ["nextAction"] = "Retry with a StyledElement node from the visual or logical tree."
                    }));
        }

        var className = request.Operation.ClassName!;
        var wasPresent = styledElement.Classes.Contains(className);
        var shouldBePresent = request.Operation.Kind switch
        {
            RuntimeMutationOperationKinds.AddClass => true,
            RuntimeMutationOperationKinds.RemoveClass => false,
            RuntimeMutationOperationKinds.ToggleClass => !wasPresent,
            _ => wasPresent
        };

        if (wasPresent == shouldBePresent)
        {
            var noChangeMetadata = CreateClassMutationMetadata(
                null,
                request.Operation,
                resolvedTarget,
                className,
                wasPresent,
                shouldBePresent,
                DateTimeOffset.UtcNow,
                resetSupported: false);

            return MutationResponseWithMetadata(
                request,
                RuntimeMutationStatuses.NoOp,
                applied: false,
                resolvedTarget.Target,
                metadata: noChangeMetadata);
        }

        var identity = NextMutationIdentity();
        var appliedAt = DateTimeOffset.UtcNow;
        SetClassPresence(styledElement, className, shouldBePresent);

        var metadata = CreateClassMutationMetadata(
            identity.Id,
            request.Operation,
            resolvedTarget,
            className,
            wasPresent,
            shouldBePresent,
            appliedAt,
            resetSupported: true);
        var activeMutation = new AppliedRuntimeMutation(
            identity.Id,
            identity.Sequence,
            request.RequestId,
            request.Operation,
            request.Operation.Kind,
            resolvedTarget.Target.TopLevelId,
            resolvedTarget.Target.TreeKind,
            resolvedTarget.Target.NodeId,
            resolvedTarget.Node.GetType().FullName ?? resolvedTarget.Node.GetType().Name,
            appliedAt,
            metadata,
            () => SetClassPresence(styledElement, className, wasPresent));

        _activeMutations[identity.Id] = activeMutation;

        return MutationResponseWithMetadata(
            request,
            RuntimeMutationStatuses.Applied,
            applied: true,
            resolvedTarget.Target,
            identity.Id,
            metadata);
    }

    private CoreResult<RuntimeMutationResponse> ApplyResourceMutation(
        RuntimeMutationRequest request,
        ResolvedMutationTarget resolvedTarget)
    {
        if (resolvedTarget.Node is not StyledElement styledElement)
        {
            return MutationResponse(
                request,
                RuntimeMutationStatuses.Unsupported,
                applied: false,
                resolvedTarget.Target,
                new ProtocolError(
                    RuntimeMutationErrorCodes.UnsupportedRuntimeMutationProperty,
                    "Resource mutations require a StyledElement target.",
                    new Dictionary<string, string>
                    {
                        ["operation"] = request.Operation.Kind,
                        ["nodeType"] = resolvedTarget.Node.GetType().FullName ?? resolvedTarget.Node.GetType().Name,
                        ["nextAction"] = "Retry with a StyledElement node from the visual or logical tree."
                    }));
        }

        var resourceKey = request.Operation.ResourceKey!;
        var hadLocalResource = styledElement.Resources.TryGetValue(resourceKey, out var originalValue);
        var originalSnapshot = CreateValueSnapshot(originalValue, hadLocalResource ? "local_resource" : "missing", hadLocalResource);

        if (request.Operation.Kind == RuntimeMutationOperationKinds.RemoveResource && !hadLocalResource)
        {
            var noChangeMetadata = CreateResourceMutationMetadata(
                null,
                request.Operation,
                resolvedTarget,
                resourceKey,
                originalSnapshot,
                originalSnapshot,
                DateTimeOffset.UtcNow,
                resetSupported: false);

            return MutationResponseWithMetadata(
                request,
                RuntimeMutationStatuses.NoOp,
                applied: false,
                resolvedTarget.Target,
                metadata: noChangeMetadata);
        }

        ConvertedMutationValue? converted = null;
        if (request.Operation.Kind == RuntimeMutationOperationKinds.SetResource)
        {
            var conversionResult = ConvertResourceMutationValue(request.Operation);
            if (!conversionResult.Success)
            {
                return MutationResponse(
                    request,
                    RuntimeMutationStatuses.Rejected,
                    applied: false,
                    resolvedTarget.Target,
                    ToProtocolError(conversionResult.Error!));
            }

            converted = conversionResult.Value!;
        }

        var identity = NextMutationIdentity();
        var appliedAt = DateTimeOffset.UtcNow;

        try
        {
            if (request.Operation.Kind == RuntimeMutationOperationKinds.RemoveResource)
            {
                styledElement.Resources.Remove(resourceKey);
            }
            else
            {
                styledElement.Resources[resourceKey] = converted!.Value;
            }
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or NotSupportedException)
        {
            return MutationResponse(
                request,
                RuntimeMutationStatuses.Rejected,
                applied: false,
                resolvedTarget.Target,
                CreateInvalidMutationValueDiagnostic(
                    request.Operation,
                    $"Resource key '{resourceKey}' could not be changed: {exception.Message}"));
        }

        styledElement.Resources.TryGetValue(resourceKey, out var effectiveResource);
        var effectiveSnapshot = CreateValueSnapshot(
            effectiveResource,
            request.Operation.Kind == RuntimeMutationOperationKinds.RemoveResource ? "missing" : "local_resource",
            request.Operation.Kind != RuntimeMutationOperationKinds.RemoveResource);
        var metadata = CreateResourceMutationMetadata(
            identity.Id,
            request.Operation,
            resolvedTarget,
            resourceKey,
            originalSnapshot,
            effectiveSnapshot,
            appliedAt,
            resetSupported: true);
        var activeMutation = new AppliedRuntimeMutation(
            identity.Id,
            identity.Sequence,
            request.RequestId,
            request.Operation,
            request.Operation.Kind,
            resolvedTarget.Target.TopLevelId,
            resolvedTarget.Target.TreeKind,
            resolvedTarget.Target.NodeId,
            resolvedTarget.Node.GetType().FullName ?? resolvedTarget.Node.GetType().Name,
            appliedAt,
            metadata,
            () => ResetResource(styledElement, resourceKey, hadLocalResource, originalValue));

        _activeMutations[identity.Id] = activeMutation;

        return MutationResponseWithMetadata(
            request,
            RuntimeMutationStatuses.Applied,
            applied: true,
            resolvedTarget.Target,
            identity.Id,
            metadata);
    }

    private CoreResult<RuntimeMutationResponse> ResetMutation(
        RuntimeMutationRequest request,
        RuntimeTargetContext currentTarget)
    {
        var targetMutationId = request.Operation.MutationId!;
        if (!_activeMutations.TryGetValue(targetMutationId, out var mutation))
        {
            return MutationResponse(
                request,
                RuntimeMutationStatuses.Rejected,
                applied: false,
                currentTarget,
                new ProtocolError(
                    RuntimeMutationErrorCodes.RuntimeMutationResetTargetNotFound,
                    $"Runtime mutation '{targetMutationId}' is not active and cannot be reset.",
                    new Dictionary<string, string>
                    {
                        ["mutationId"] = targetMutationId,
                        ["activeMutationCount"] = _activeMutations.Count.ToString(CultureInfo.InvariantCulture),
                        ["nextAction"] = "Reset only mutation ids returned by applied mutation responses, or call reset_all for the current session."
                    }));
        }

        return ResetAppliedMutations(request, currentTarget, [mutation], resetAll: false);
    }

    private CoreResult<RuntimeMutationResponse> ResetAllMutations(
        RuntimeMutationRequest request,
        RuntimeTargetContext currentTarget)
    {
        var mutations = _activeMutations.Values
            .OrderByDescending(static mutation => mutation.Sequence)
            .ToArray();

        return ResetAppliedMutations(request, currentTarget, mutations, resetAll: true);
    }

    private CoreResult<RuntimeMutationResponse> ResetAppliedMutations(
        RuntimeMutationRequest request,
        RuntimeTargetContext currentTarget,
        IReadOnlyList<AppliedRuntimeMutation> mutations,
        bool resetAll)
    {
        if (mutations.Count == 0)
        {
            var noChangeMetadata = CreateResetMetadata(null, [], resetAll, DateTimeOffset.UtcNow);
            return MutationResponseWithMetadata(
                request,
                RuntimeMutationStatuses.NoOp,
                applied: false,
                currentTarget,
                metadata: noChangeMetadata);
        }

        var resetMutations = new List<AppliedRuntimeMutation>(mutations.Count);
        foreach (var mutation in mutations)
        {
            try
            {
                mutation.Reset();
                resetMutations.Add(mutation);
                _activeMutations.TryRemove(mutation.MutationId, out _);
            }
            catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or NotSupportedException or ObjectDisposedException)
            {
                return MutationResponse(
                    request,
                    RuntimeMutationStatuses.Unavailable,
                    applied: false,
                    currentTarget,
                    new ProtocolError(
                        RuntimeMutationErrorCodes.InvalidRuntimeMutationRequest,
                        $"Runtime mutation reset failed: {exception.Message}",
                        new Dictionary<string, string>
                        {
                            ["failedMutationId"] = mutation.MutationId,
                            ["resetAll"] = resetAll.ToString(CultureInfo.InvariantCulture),
                            ["requestedMutationCount"] = mutations.Count.ToString(CultureInfo.InvariantCulture),
                            ["resetCount"] = resetMutations.Count.ToString(CultureInfo.InvariantCulture),
                            ["activeMutationCount"] = _activeMutations.Count.ToString(CultureInfo.InvariantCulture),
                            ["nextAction"] = "Retry reset_mutation for the failed mutation id or close the local bridge session to force cleanup."
                        }));
            }
        }

        var identity = NextMutationIdentity();
        var metadata = CreateResetMetadata(identity.Id, resetMutations, resetAll, DateTimeOffset.UtcNow);

        return MutationResponseWithMetadata(
            request,
            RuntimeMutationStatuses.Applied,
            applied: true,
            currentTarget,
            identity.Id,
            metadata);
    }

    private void UnregisterTopLevel(int key, string topLevelId)
    {
        _registeredTopLevels.TryRemove(key, out _);
        ResetActiveMutationsOnUiThread(mutation => string.Equals(mutation.TopLevelId, topLevelId, StringComparison.Ordinal));
    }

    private void ResetActiveMutationsOnUiThread(Func<AppliedRuntimeMutation, bool> predicate)
    {
        if (_activeMutations.IsEmpty)
        {
            return;
        }

        if (Dispatcher.UIThread.CheckAccess())
        {
            ResetActiveMutations(predicate);
            return;
        }

        Dispatcher.UIThread
            .InvokeAsync(() => ResetActiveMutations(predicate), DispatcherPriority.Send)
            .GetTask()
            .GetAwaiter()
            .GetResult();
    }

    private void ResetActiveMutations(Func<AppliedRuntimeMutation, bool> predicate)
    {
        Dispatcher.UIThread.VerifyAccess();

        var mutations = _activeMutations.Values
            .Where(predicate)
            .OrderByDescending(static mutation => mutation.Sequence)
            .ToArray();

        foreach (var mutation in mutations)
        {
            if (!_activeMutations.TryRemove(mutation.MutationId, out var activeMutation))
            {
                continue;
            }

            try
            {
                activeMutation.Reset();
            }
            catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or NotSupportedException or ObjectDisposedException)
            {
            }
        }
    }

    private CoreResult<RuntimeMutationReviewResponse> CreateMutationReview(int? maxResults)
    {
        Dispatcher.UIThread.VerifyAccess();

        if (maxResults is < 1 or > RuntimeMutationReviewResponse.MaximumEntries)
        {
            return CoreResult<RuntimeMutationReviewResponse>.Fail(new CoreError(
                CoreErrorCodes.InvalidBridgeRequest,
                $"Mutation review maxResults must be between 1 and {RuntimeMutationReviewResponse.MaximumEntries.ToString(CultureInfo.InvariantCulture)}."));
        }

        var limit = maxResults ?? DefaultMutationReviewLimit;
        var activeMutations = _activeMutations.Values
            .OrderByDescending(static mutation => mutation.Sequence)
            .ToArray();
        var activeIds = activeMutations
            .Select(static mutation => mutation.MutationId)
            .ToHashSet(StringComparer.Ordinal);
        var rawHistory = _mutationHistory.ToArray();
        var historyByMutationId = rawHistory
            .GroupBy(static entry => entry.MutationId, StringComparer.Ordinal)
            .ToDictionary(
                static group => group.Key,
                static group => group.OrderByDescending(static entry => entry.Sequence).First(),
                StringComparer.Ordinal);

        var history = rawHistory
            .OrderByDescending(static entry => entry.Sequence)
            .Take(limit)
            .Select(entry => WithActiveState(entry, activeIds.Contains(entry.MutationId)))
            .ToArray();
        var active = activeMutations
            .Take(limit)
            .Select(mutation => historyByMutationId.TryGetValue(mutation.MutationId, out var historyEntry)
                ? WithActiveState(historyEntry, active: true)
                : ToReviewEntry(mutation, active: true))
            .ToArray();
        var metadata = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["scope"] = "local_session",
            ["transport"] = "local_only",
            ["temporary"] = "true",
            ["maxResults"] = limit.ToString(CultureInfo.InvariantCulture),
            ["historyTruncated"] = (rawHistory.Length > history.Length).ToString(CultureInfo.InvariantCulture),
            ["activeMutationsTruncated"] = (activeMutations.Length > active.Length).ToString(CultureInfo.InvariantCulture)
        };
        var resetHandoff = new RuntimeMutationResetHandoff(
            SessionId,
            activeMutations.Length,
            activeMutations.Select(static mutation => mutation.MutationId).ToArray(),
            suggestedResetAllTarget: active.FirstOrDefault()?.Target,
            nextAction: activeMutations.Length == 0
                ? "No active runtime mutations are currently registered for this local bridge session."
                : "Use reset_mutation with a listed mutation id, or reset_all with the suggested target to clear active runtime overrides.");

        var response = new RuntimeMutationReviewResponse(
            SessionId,
            DateTimeOffset.UtcNow,
            rawHistory.Length,
            activeMutations.Length,
            history,
            active,
            resetHandoff,
            metadata);

        return CoreResult<RuntimeMutationReviewResponse>.Ok(
            RuntimeSourceSuggestionBuilder.WithSourceContext(response, sourceContext: null));
    }

    private void RecordMutationResponse(RuntimeMutationResponse response)
    {
        var sequence = Interlocked.Increment(ref _mutationHistorySequence);
        _mutationHistory.Enqueue(ToReviewEntry(
            response,
            sequence,
            response.Applied && _activeMutations.ContainsKey(response.MutationId)));

        while (_mutationHistory.Count > MaximumMutationHistoryEntries
            && _mutationHistory.TryDequeue(out _))
        {
        }
    }

    private RuntimeMutationReviewEntry ToReviewEntry(
        AppliedRuntimeMutation mutation,
        bool active)
    {
        return new RuntimeMutationReviewEntry(
            mutation.Sequence,
            mutation.RequestId,
            mutation.MutationId,
            SessionId,
            mutation.TopLevelId,
            new RuntimeTargetContext(SessionId, mutation.TopLevelId, mutation.TreeKind, mutation.NodeId),
            mutation.Operation,
            RuntimeMutationStatuses.Applied,
            applied: true,
            active,
            mutation.AppliedAt,
            metadata: mutation.Metadata);
    }

    private static RuntimeMutationReviewEntry ToReviewEntry(
        RuntimeMutationResponse response,
        long sequence,
        bool active)
    {
        return new RuntimeMutationReviewEntry(
            sequence,
            response.RequestId,
            response.MutationId,
            response.SessionId,
            response.TopLevelId,
            response.Target,
            response.Operation,
            response.Status,
            response.Applied,
            active,
            response.EvaluatedAt,
            response.Diagnostics,
            response.Metadata);
    }

    private static RuntimeMutationReviewEntry WithActiveState(
        RuntimeMutationReviewEntry entry,
        bool active)
    {
        return new RuntimeMutationReviewEntry(
            entry.Sequence,
            entry.RequestId,
            entry.MutationId,
            entry.SessionId,
            entry.TopLevelId,
            entry.Target,
            entry.Operation,
            entry.Status,
            entry.Applied,
            active,
            entry.EvaluatedAt,
            entry.Diagnostics,
            entry.Metadata);
    }

    private CoreResult<ResolvedMutationTarget> ResolveMutationTarget(TopLevel topLevel, RuntimeTargetContext target)
    {
        var currentTopLevelGeneration = CreateObjectGeneration(topLevel);
        if (!string.IsNullOrWhiteSpace(target.TopLevelGeneration)
            && !string.Equals(target.TopLevelGeneration, currentTopLevelGeneration, StringComparison.Ordinal))
        {
            return CoreResult<ResolvedMutationTarget>.Fail(new CoreError(
                RuntimeMutationErrorCodes.RuntimeMutationTargetStale,
                "Top-level target generation no longer matches the current runtime object.",
                new Dictionary<string, string>
                {
                    ["topLevelId"] = target.TopLevelId,
                    ["expectedTopLevelGeneration"] = target.TopLevelGeneration,
                    ["actualTopLevelGeneration"] = currentTopLevelGeneration,
                    ["nextAction"] = "Refresh the target context from visual-tree, logical-tree, find-nodes, or inspect-node before retrying."
                }));
        }

        if (string.IsNullOrWhiteSpace(target.NodeId))
        {
            return CoreResult<ResolvedMutationTarget>.Ok(new ResolvedMutationTarget(
                topLevel,
                CreateTopLevelTarget(target.TopLevelId, topLevel)));
        }

        var node = FindNodeById(topLevel, target.NodeId);
        if (node is null)
        {
            return CoreResult<ResolvedMutationTarget>.Fail(new CoreError(
                RuntimeMutationErrorCodes.RuntimeMutationTargetStale,
                $"Node '{target.NodeId}' was not found in top-level '{target.TopLevelId}'.",
                CreateTargetErrorDetails(target.TopLevelId, target.TreeKind, target.NodeId)));
        }

        var currentTarget = CreateNodeTarget(target.TopLevelId, target.TreeKind!, topLevel, node);
        if (!string.IsNullOrWhiteSpace(target.NodeGeneration)
            && !string.Equals(target.NodeGeneration, currentTarget.NodeGeneration, StringComparison.Ordinal))
        {
            return CoreResult<ResolvedMutationTarget>.Fail(new CoreError(
                RuntimeMutationErrorCodes.RuntimeMutationTargetStale,
                "Node target generation no longer matches the current runtime object.",
                new Dictionary<string, string>
                {
                    ["topLevelId"] = target.TopLevelId,
                    ["treeKind"] = target.TreeKind!,
                    ["nodeId"] = target.NodeId,
                    ["expectedNodeGeneration"] = target.NodeGeneration,
                    ["actualNodeGeneration"] = currentTarget.NodeGeneration ?? "not_available",
                    ["nextAction"] = "Refresh the target context from visual-tree, logical-tree, find-nodes, or inspect-node before retrying."
                }));
        }

        return CoreResult<ResolvedMutationTarget>.Ok(new ResolvedMutationTarget(node, currentTarget));
    }

    private static ProtocolError? ValidateMutationOperation(RuntimeMutationOperation operation)
    {
        return operation.Kind switch
        {
            RuntimeMutationOperationKinds.NoOp => null,
            RuntimeMutationOperationKinds.SetProperty => ValidateSetPropertyOperation(operation),
            RuntimeMutationOperationKinds.AddClass
                or RuntimeMutationOperationKinds.RemoveClass
                or RuntimeMutationOperationKinds.ToggleClass => ValidateClassOperation(operation),
            RuntimeMutationOperationKinds.SetResource => ValidateSetResourceOperation(operation),
            RuntimeMutationOperationKinds.RemoveResource => ValidateRemoveResourceOperation(operation),
            RuntimeMutationOperationKinds.ResetMutation => ValidateResetMutationOperation(operation),
            RuntimeMutationOperationKinds.ResetAll => null,
            _ => new ProtocolError(
                RuntimeMutationErrorCodes.UnsupportedRuntimeMutationOperation,
                $"Runtime mutation operation '{operation.Kind}' is not supported.",
                new Dictionary<string, string>
                {
                    ["operation"] = operation.Kind,
                    ["supportedOperations"] = string.Join(",", GetSupportedMutationOperations()),
                    ["nextAction"] = "Use one of the supported runtime mutation operations reported by capabilities."
                })
        };
    }

    private static ProtocolError? ValidateSetPropertyOperation(RuntimeMutationOperation operation)
    {
        if (string.IsNullOrWhiteSpace(operation.PropertyName))
        {
            return new ProtocolError(
                RuntimeMutationErrorCodes.InvalidRuntimeMutationRequest,
                "set_property requires propertyName.",
                new Dictionary<string, string>
                {
                    ["operation"] = operation.Kind,
                    ["nextAction"] = "Provide a concrete Avalonia property name."
                });
        }

        if (operation.Value is null)
        {
            return new ProtocolError(
                RuntimeMutationErrorCodes.InvalidRuntimeMutationValue,
                "set_property requires value.",
                new Dictionary<string, string>
                {
                    ["operation"] = operation.Kind,
                    ["propertyName"] = operation.PropertyName,
                    ["nextAction"] = "Provide a string value and optional valueType for the requested property."
                });
        }

        return null;
    }

    private static ProtocolError? ValidateClassOperation(RuntimeMutationOperation operation)
    {
        if (string.IsNullOrWhiteSpace(operation.ClassName))
        {
            return new ProtocolError(
                RuntimeMutationErrorCodes.InvalidRuntimeMutationRequest,
                $"{operation.Kind} requires className.",
                new Dictionary<string, string>
                {
                    ["operation"] = operation.Kind,
                    ["nextAction"] = "Provide a style class name."
                });
        }

        return null;
    }

    private static ProtocolError? ValidateSetResourceOperation(RuntimeMutationOperation operation)
    {
        if (string.IsNullOrWhiteSpace(operation.ResourceKey))
        {
            return new ProtocolError(
                RuntimeMutationErrorCodes.InvalidRuntimeMutationRequest,
                "set_resource requires resourceKey.",
                new Dictionary<string, string>
                {
                    ["operation"] = operation.Kind,
                    ["nextAction"] = "Provide a resource key."
                });
        }

        if (operation.Value is null)
        {
            return new ProtocolError(
                RuntimeMutationErrorCodes.InvalidRuntimeMutationValue,
                "set_resource requires value.",
                new Dictionary<string, string>
                {
                    ["operation"] = operation.Kind,
                    ["resourceKey"] = operation.ResourceKey,
                    ["nextAction"] = "Provide a resource value and optional valueType."
                });
        }

        return null;
    }

    private static ProtocolError? ValidateRemoveResourceOperation(RuntimeMutationOperation operation)
    {
        return string.IsNullOrWhiteSpace(operation.ResourceKey)
            ? new ProtocolError(
                RuntimeMutationErrorCodes.InvalidRuntimeMutationRequest,
                "remove_resource requires resourceKey.",
                new Dictionary<string, string>
                {
                    ["operation"] = operation.Kind,
                    ["nextAction"] = "Provide a resource key."
                })
            : null;
    }

    private static ProtocolError? ValidateResetMutationOperation(RuntimeMutationOperation operation)
    {
        return string.IsNullOrWhiteSpace(operation.MutationId)
            ? new ProtocolError(
                RuntimeMutationErrorCodes.InvalidRuntimeMutationRequest,
                "reset_mutation requires mutationId.",
                new Dictionary<string, string>
                {
                    ["operation"] = operation.Kind,
                    ["nextAction"] = "Provide the mutationId returned by an applied mutation response."
                })
            : null;
    }

    private static IReadOnlyList<string> GetSupportedMutationOperations()
    {
        return
        [
            RuntimeMutationOperationKinds.NoOp,
            RuntimeMutationOperationKinds.SetProperty,
            RuntimeMutationOperationKinds.AddClass,
            RuntimeMutationOperationKinds.RemoveClass,
            RuntimeMutationOperationKinds.ToggleClass,
            RuntimeMutationOperationKinds.SetResource,
            RuntimeMutationOperationKinds.RemoveResource,
            RuntimeMutationOperationKinds.ResetMutation,
            RuntimeMutationOperationKinds.ResetAll
        ];
    }

    private static ProtocolError CreateUnsupportedPropertyDiagnostic(
        string operation,
        string propertyName,
        object node)
    {
        return new ProtocolError(
            RuntimeMutationErrorCodes.UnsupportedRuntimeMutationProperty,
            $"Runtime property mutation for '{propertyName}' is not supported on node type '{node.GetType().FullName ?? node.GetType().Name}'.",
            new Dictionary<string, string>
            {
                ["operation"] = operation,
                ["propertyName"] = propertyName,
                ["nodeType"] = node.GetType().FullName ?? node.GetType().Name,
                ["supportedProperties"] = string.Join(",", RuntimeMutationPropertyNames.All),
                ["nextAction"] = "Retry with a supported property for the selected node type."
            });
    }

    private static string MutationStatusForDiagnostic(string code)
    {
        return code switch
        {
            RuntimeMutationErrorCodes.InvalidRuntimeMutationRequest
                or RuntimeMutationErrorCodes.InvalidRuntimeMutationValue
                or RuntimeMutationErrorCodes.RuntimeMutationResetTargetNotFound => RuntimeMutationStatuses.Rejected,
            RuntimeMutationErrorCodes.RuntimeMutationCapabilityUnavailable
                or RuntimeMutationErrorCodes.UnsupportedRuntimeMutationOperation
                or RuntimeMutationErrorCodes.UnsupportedRuntimeMutationProperty => RuntimeMutationStatuses.Unsupported,
            RuntimeMutationErrorCodes.RuntimeMutationTargetStale => RuntimeMutationStatuses.StaleTarget,
            _ => RuntimeMutationStatuses.Unavailable
        };
    }

    private CoreResult<RuntimeMutationResponse> MutationResponse(
        RuntimeMutationRequest request,
        string status,
        bool applied,
        RuntimeTargetContext target,
        params ProtocolError[] diagnostics)
    {
        return CoreResult<RuntimeMutationResponse>.Ok(new RuntimeMutationResponse(
            request.RequestId,
            NextMutationId(),
            SessionId,
            target.TopLevelId,
            target,
            request.Operation,
            status,
            applied,
            DateTimeOffset.UtcNow,
            RuntimeMutationCapabilityCatalog.CurrentBridgeCapabilities(),
            diagnostics));
    }

    private CoreResult<RuntimeMutationResponse> MutationResponseWithMetadata(
        RuntimeMutationRequest request,
        string status,
        bool applied,
        RuntimeTargetContext target,
        string? mutationId = null,
        IReadOnlyDictionary<string, string>? metadata = null,
        IReadOnlyList<ProtocolError>? diagnostics = null)
    {
        var responseMetadata = metadata is null
            ? null
            : new Dictionary<string, string>(metadata, StringComparer.Ordinal)
            {
                ["activeMutationCount"] = _activeMutations.Count.ToString(CultureInfo.InvariantCulture)
            };

        return CoreResult<RuntimeMutationResponse>.Ok(new RuntimeMutationResponse(
            request.RequestId,
            mutationId ?? NextMutationId(),
            SessionId,
            target.TopLevelId,
            target,
            request.Operation,
            status,
            applied,
            DateTimeOffset.UtcNow,
            RuntimeMutationCapabilityCatalog.CurrentBridgeCapabilities(),
            diagnostics,
            responseMetadata));
    }

    private static CoreResult<MutableAvaloniaProperty> ResolveMutableProperty(object node, string propertyName)
    {
        var normalized = NormalizeMutationName(propertyName);

        if (node is Layoutable layoutable)
        {
            var layoutProperty = normalized switch
            {
                "width" => new MutableAvaloniaProperty(layoutable, Layoutable.WidthProperty, "Width", MutationValueKindLayoutDouble),
                "height" => new MutableAvaloniaProperty(layoutable, Layoutable.HeightProperty, "Height", MutationValueKindLayoutDouble),
                "minwidth" => new MutableAvaloniaProperty(layoutable, Layoutable.MinWidthProperty, "MinWidth", MutationValueKindDouble),
                "minheight" => new MutableAvaloniaProperty(layoutable, Layoutable.MinHeightProperty, "MinHeight", MutationValueKindDouble),
                "maxwidth" => new MutableAvaloniaProperty(layoutable, Layoutable.MaxWidthProperty, "MaxWidth", MutationValueKindMaxLayoutDouble),
                "maxheight" => new MutableAvaloniaProperty(layoutable, Layoutable.MaxHeightProperty, "MaxHeight", MutationValueKindMaxLayoutDouble),
                "margin" => new MutableAvaloniaProperty(layoutable, Layoutable.MarginProperty, "Margin", MutationValueKindThickness),
                _ => null
            };

            if (layoutProperty is not null)
            {
                return CoreResult<MutableAvaloniaProperty>.Ok(layoutProperty);
            }
        }

        if (node is Visual visual && normalized == "opacity")
        {
            return CoreResult<MutableAvaloniaProperty>.Ok(new MutableAvaloniaProperty(
                visual,
                Visual.OpacityProperty,
                "Opacity",
                MutationValueKindOpacity));
        }

        if (node is InputElement inputElement && normalized == "isenabled")
        {
            return CoreResult<MutableAvaloniaProperty>.Ok(new MutableAvaloniaProperty(
                inputElement,
                InputElement.IsEnabledProperty,
                "IsEnabled",
                MutationValueKindBool));
        }

        if (normalized == "isselected")
        {
            var selectedProperty = node switch
            {
                ListBoxItem listBoxItem => new MutableAvaloniaProperty(listBoxItem, ListBoxItem.IsSelectedProperty, "IsSelected", MutationValueKindBool),
                TabItem tabItem => new MutableAvaloniaProperty(tabItem, TabItem.IsSelectedProperty, "IsSelected", MutationValueKindBool),
                TreeViewItem treeViewItem => new MutableAvaloniaProperty(treeViewItem, TreeViewItem.IsSelectedProperty, "IsSelected", MutationValueKindBool),
                Control control => new MutableAvaloniaProperty(control, SelectingItemsControl.IsSelectedProperty, "IsSelected", MutationValueKindBool),
                _ => null
            };

            if (selectedProperty is not null)
            {
                return CoreResult<MutableAvaloniaProperty>.Ok(selectedProperty);
            }
        }

        if (normalized == "isexpanded")
        {
            var expandedProperty = node switch
            {
                Expander expander => new MutableAvaloniaProperty(expander, Expander.IsExpandedProperty, "IsExpanded", MutationValueKindBool),
                TreeViewItem treeViewItem => new MutableAvaloniaProperty(treeViewItem, TreeViewItem.IsExpandedProperty, "IsExpanded", MutationValueKindBool),
                _ => null
            };

            if (expandedProperty is not null)
            {
                return CoreResult<MutableAvaloniaProperty>.Ok(expandedProperty);
            }
        }

        if (normalized == "padding")
        {
            var paddingProperty = node switch
            {
                TextBlock textBlock => new MutableAvaloniaProperty(textBlock, TextBlock.PaddingProperty, "Padding", MutationValueKindThickness),
                ContentPresenter presenter => new MutableAvaloniaProperty(presenter, ContentPresenter.PaddingProperty, "Padding", MutationValueKindThickness),
                Decorator decorator => new MutableAvaloniaProperty(decorator, Decorator.PaddingProperty, "Padding", MutationValueKindThickness),
                TemplatedControl templatedControl => new MutableAvaloniaProperty(templatedControl, TemplatedControl.PaddingProperty, "Padding", MutationValueKindThickness),
                _ => null
            };

            if (paddingProperty is not null)
            {
                return CoreResult<MutableAvaloniaProperty>.Ok(paddingProperty);
            }
        }

        if (normalized == "text")
        {
            var textProperty = node switch
            {
                TextBlock textBlock => new MutableAvaloniaProperty(textBlock, TextBlock.TextProperty, "Text", MutationValueKindString),
                TextBox textBox => new MutableAvaloniaProperty(textBox, TextBox.TextProperty, "Text", MutationValueKindString),
                ContentControl contentControl => new MutableAvaloniaProperty(contentControl, ContentControl.ContentProperty, "Content", MutationValueKindString),
                _ => null
            };

            if (textProperty is not null)
            {
                return CoreResult<MutableAvaloniaProperty>.Ok(textProperty);
            }
        }

        if (normalized == "content" && node is ContentControl content)
        {
            return CoreResult<MutableAvaloniaProperty>.Ok(new MutableAvaloniaProperty(
                content,
                ContentControl.ContentProperty,
                "Content",
                MutationValueKindString));
        }

        if (normalized == "background")
        {
            var backgroundProperty = node switch
            {
                TextBlock textBlock => new MutableAvaloniaProperty(textBlock, TextBlock.BackgroundProperty, "Background", MutationValueKindBrush),
                Border border => new MutableAvaloniaProperty(border, Border.BackgroundProperty, "Background", MutationValueKindBrush),
                Panel panel => new MutableAvaloniaProperty(panel, Panel.BackgroundProperty, "Background", MutationValueKindBrush),
                ContentPresenter presenter => new MutableAvaloniaProperty(presenter, ContentPresenter.BackgroundProperty, "Background", MutationValueKindBrush),
                TemplatedControl templatedControl => new MutableAvaloniaProperty(templatedControl, TemplatedControl.BackgroundProperty, "Background", MutationValueKindBrush),
                _ => null
            };

            if (backgroundProperty is not null)
            {
                return CoreResult<MutableAvaloniaProperty>.Ok(backgroundProperty);
            }
        }

        if (normalized == "foreground")
        {
            var foregroundProperty = node switch
            {
                TextBlock textBlock => new MutableAvaloniaProperty(textBlock, TextBlock.ForegroundProperty, "Foreground", MutationValueKindBrush),
                ContentPresenter presenter => new MutableAvaloniaProperty(presenter, ContentPresenter.ForegroundProperty, "Foreground", MutationValueKindBrush),
                TemplatedControl templatedControl => new MutableAvaloniaProperty(templatedControl, TemplatedControl.ForegroundProperty, "Foreground", MutationValueKindBrush),
                _ => null
            };

            if (foregroundProperty is not null)
            {
                return CoreResult<MutableAvaloniaProperty>.Ok(foregroundProperty);
            }
        }

        return CoreResult<MutableAvaloniaProperty>.Fail(new CoreError(
            RuntimeMutationErrorCodes.UnsupportedRuntimeMutationProperty,
            $"Runtime property mutation for '{propertyName}' is not supported on this node.",
            CreateUnsupportedPropertyDiagnostic(RuntimeMutationOperationKinds.SetProperty, propertyName, node).Details));
    }

    private static CoreResult<ConvertedMutationValue> ConvertMutationValue(
        RuntimeMutationOperation operation,
        string valueKind)
    {
        var value = operation.Value;
        if (value is null)
        {
            return CoreResult<ConvertedMutationValue>.Fail(new CoreError(
                RuntimeMutationErrorCodes.InvalidRuntimeMutationValue,
                $"{operation.Kind} requires value."));
        }

        try
        {
            return valueKind switch
            {
                MutationValueKindBool => CoreResult<ConvertedMutationValue>.Ok(new ConvertedMutationValue(bool.Parse(value))),
                MutationValueKindBrush => CoreResult<ConvertedMutationValue>.Ok(new ConvertedMutationValue(ParseBrushValue(value))),
                MutationValueKindDouble => CoreResult<ConvertedMutationValue>.Ok(new ConvertedMutationValue(ParseFiniteNonNegativeDouble(value, operation.PropertyName))),
                MutationValueKindLayoutDouble => CoreResult<ConvertedMutationValue>.Ok(new ConvertedMutationValue(ParseLayoutDouble(value, allowAuto: true, allowInfinity: false, operation.PropertyName))),
                MutationValueKindMaxLayoutDouble => CoreResult<ConvertedMutationValue>.Ok(new ConvertedMutationValue(ParseLayoutDouble(value, allowAuto: false, allowInfinity: true, operation.PropertyName))),
                MutationValueKindOpacity => CoreResult<ConvertedMutationValue>.Ok(new ConvertedMutationValue(ParseOpacity(value))),
                MutationValueKindString => CoreResult<ConvertedMutationValue>.Ok(new ConvertedMutationValue(value)),
                MutationValueKindThickness => CoreResult<ConvertedMutationValue>.Ok(new ConvertedMutationValue(Thickness.Parse(value))),
                _ => CoreResult<ConvertedMutationValue>.Fail(new CoreError(
                    RuntimeMutationErrorCodes.InvalidRuntimeMutationValue,
                    $"Runtime mutation value kind '{valueKind}' is not supported."))
            };
        }
        catch (Exception exception) when (exception is ArgumentException or FormatException or OverflowException)
        {
            return CoreResult<ConvertedMutationValue>.Fail(new CoreError(
                RuntimeMutationErrorCodes.InvalidRuntimeMutationValue,
                exception.Message,
                new Dictionary<string, string>
                {
                    ["operation"] = operation.Kind,
                    ["propertyName"] = operation.PropertyName ?? "not_available",
                    ["value"] = value,
                    ["valueType"] = operation.ValueType ?? valueKind
                }));
        }
    }

    private static CoreResult<ConvertedMutationValue> ConvertResourceMutationValue(RuntimeMutationOperation operation)
    {
        var value = operation.Value;
        if (value is null)
        {
            return CoreResult<ConvertedMutationValue>.Fail(new CoreError(
                RuntimeMutationErrorCodes.InvalidRuntimeMutationValue,
                "set_resource requires value."));
        }

        var valueType = NormalizeMutationName(operation.ValueType ?? "string");
        try
        {
            return valueType switch
            {
                "brush" => CoreResult<ConvertedMutationValue>.Ok(new ConvertedMutationValue(ParseBrushValue(value))),
                "color" => CoreResult<ConvertedMutationValue>.Ok(new ConvertedMutationValue(new SolidColorBrush(Color.Parse(value)))),
                "double" or "number" => CoreResult<ConvertedMutationValue>.Ok(new ConvertedMutationValue(ParseInvariantDouble(value, "resource"))),
                "bool" or "boolean" => CoreResult<ConvertedMutationValue>.Ok(new ConvertedMutationValue(bool.Parse(value))),
                "thickness" => CoreResult<ConvertedMutationValue>.Ok(new ConvertedMutationValue(Thickness.Parse(value))),
                "null" => CoreResult<ConvertedMutationValue>.Ok(new ConvertedMutationValue(null)),
                "string" => CoreResult<ConvertedMutationValue>.Ok(new ConvertedMutationValue(value)),
                _ => CoreResult<ConvertedMutationValue>.Fail(new CoreError(
                    RuntimeMutationErrorCodes.InvalidRuntimeMutationValue,
                    $"Resource mutation valueType '{operation.ValueType}' is not supported.",
                    new Dictionary<string, string>
                    {
                        ["resourceKey"] = operation.ResourceKey ?? "not_available",
                        ["supportedValueTypes"] = "string,double,bool,brush,color,thickness,null"
                    }))
            };
        }
        catch (Exception exception) when (exception is ArgumentException or FormatException or OverflowException)
        {
            return CoreResult<ConvertedMutationValue>.Fail(new CoreError(
                RuntimeMutationErrorCodes.InvalidRuntimeMutationValue,
                exception.Message,
                new Dictionary<string, string>
                {
                    ["operation"] = operation.Kind,
                    ["resourceKey"] = operation.ResourceKey ?? "not_available",
                    ["value"] = value,
                    ["valueType"] = operation.ValueType ?? "string"
                }));
        }
    }

    private static object? ParseBrushValue(string value)
    {
        return string.Equals(value, "null", StringComparison.OrdinalIgnoreCase)
            ? null
            : Brush.Parse(value);
    }

    private static double ParseOpacity(string value)
    {
        var parsed = ParseInvariantDouble(value, "Opacity");
        if (parsed < 0 || parsed > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(value), "Opacity must be between 0 and 1.");
        }

        return parsed;
    }

    private static double ParseFiniteNonNegativeDouble(string value, string? propertyName)
    {
        var parsed = ParseInvariantDouble(value, propertyName ?? "value");
        if (!double.IsFinite(parsed) || parsed < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(value), $"{propertyName ?? "Value"} must be a finite non-negative number.");
        }

        return parsed;
    }

    private static double ParseLayoutDouble(
        string value,
        bool allowAuto,
        bool allowInfinity,
        string? propertyName)
    {
        if (allowAuto && string.Equals(value, "auto", StringComparison.OrdinalIgnoreCase))
        {
            return double.NaN;
        }

        if (allowInfinity && string.Equals(value, "infinity", StringComparison.OrdinalIgnoreCase))
        {
            return double.PositiveInfinity;
        }

        var parsed = ParseInvariantDouble(value, propertyName ?? "value");
        if (double.IsNaN(parsed) || parsed < 0 || (!allowInfinity && double.IsInfinity(parsed)))
        {
            throw new ArgumentOutOfRangeException(nameof(value), $"{propertyName ?? "Value"} must be a non-negative layout number.");
        }

        return parsed;
    }

    private static double ParseInvariantDouble(string value, string propertyName)
    {
        if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
        {
            throw new FormatException($"{propertyName} value '{value}' is not a valid invariant-culture number.");
        }

        return parsed;
    }

    private static PropertyValueSnapshot CapturePropertySnapshot(AvaloniaObject owner, AvaloniaProperty property)
    {
        try
        {
            var diagnostic = owner.GetDiagnostic(property);
            return new PropertyValueSnapshot(
                diagnostic.Value,
                diagnostic.Value?.GetType().FullName ?? "null",
                FormatComputedValue(diagnostic.Value),
                diagnostic.Priority,
                MapComputedSource(diagnostic.Priority),
                owner.IsSet(property));
        }
        catch (InvalidOperationException)
        {
            var value = owner.GetValue(property);
            return new PropertyValueSnapshot(
                value,
                value?.GetType().FullName ?? "null",
                FormatComputedValue(value),
                BindingPriority.Unset,
                "not_available",
                owner.IsSet(property));
        }
    }

    private static PropertyValueSnapshot CreateValueSnapshot(object? value, string source, bool hasLocalValue)
    {
        return new PropertyValueSnapshot(
            value,
            value?.GetType().FullName ?? "null",
            FormatComputedValue(value),
            BindingPriority.Unset,
            source,
            hasLocalValue);
    }

    private static IReadOnlyDictionary<string, string> CreateAppliedMutationMetadata(
        string mutationId,
        RuntimeMutationOperation operation,
        ResolvedMutationTarget resolvedTarget,
        string propertyName,
        PropertyValueSnapshot original,
        PropertyValueSnapshot effective,
        DateTimeOffset appliedAt)
    {
        var metadata = CreateBaseMutationMetadata(mutationId, operation, resolvedTarget, appliedAt);
        metadata["propertyName"] = propertyName;
        AddValueMetadata(metadata, "original", original);
        AddValueMetadata(metadata, "effective", effective);
        metadata["resetSupported"] = "true";
        return metadata;
    }

    private static IReadOnlyDictionary<string, string> CreateClassMutationMetadata(
        string? mutationId,
        RuntimeMutationOperation operation,
        ResolvedMutationTarget resolvedTarget,
        string className,
        bool wasPresent,
        bool isPresent,
        DateTimeOffset appliedAt,
        bool resetSupported)
    {
        var metadata = CreateBaseMutationMetadata(mutationId, operation, resolvedTarget, appliedAt);
        metadata["className"] = className;
        metadata["originalValue"] = wasPresent ? "present" : "absent";
        metadata["originalValueType"] = "class_presence";
        metadata["originalValueSource"] = "classes";
        metadata["effectiveValue"] = isPresent ? "present" : "absent";
        metadata["effectiveValueType"] = "class_presence";
        metadata["effectiveValueSource"] = "classes";
        metadata["resetSupported"] = resetSupported.ToString(CultureInfo.InvariantCulture);
        return metadata;
    }

    private static IReadOnlyDictionary<string, string> CreateResourceMutationMetadata(
        string? mutationId,
        RuntimeMutationOperation operation,
        ResolvedMutationTarget resolvedTarget,
        string resourceKey,
        PropertyValueSnapshot original,
        PropertyValueSnapshot effective,
        DateTimeOffset appliedAt,
        bool resetSupported)
    {
        var metadata = CreateBaseMutationMetadata(mutationId, operation, resolvedTarget, appliedAt);
        metadata["resourceKey"] = resourceKey;
        AddValueMetadata(metadata, "original", original);
        AddValueMetadata(metadata, "effective", effective);
        metadata["resetSupported"] = resetSupported.ToString(CultureInfo.InvariantCulture);
        return metadata;
    }

    private static Dictionary<string, string> CreateBaseMutationMetadata(
        string? mutationId,
        RuntimeMutationOperation operation,
        ResolvedMutationTarget resolvedTarget,
        DateTimeOffset appliedAt)
    {
        var metadata = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["operation"] = operation.Kind,
            ["sessionId"] = resolvedTarget.Target.SessionId.Value,
            ["topLevelId"] = resolvedTarget.Target.TopLevelId,
            ["targetKind"] = resolvedTarget.Target.TargetKind ?? "node",
            ["nodeType"] = resolvedTarget.Node.GetType().FullName ?? resolvedTarget.Node.GetType().Name,
            ["appliedAt"] = appliedAt.ToString("O", CultureInfo.InvariantCulture)
        };

        if (!string.IsNullOrWhiteSpace(mutationId))
        {
            metadata["mutationId"] = mutationId;
        }

        if (!string.IsNullOrWhiteSpace(resolvedTarget.Target.TreeKind))
        {
            metadata["treeKind"] = resolvedTarget.Target.TreeKind!;
        }

        if (!string.IsNullOrWhiteSpace(resolvedTarget.Target.NodeId))
        {
            metadata["nodeId"] = resolvedTarget.Target.NodeId!;
        }

        return metadata;
    }

    private static void AddValueMetadata(
        IDictionary<string, string> metadata,
        string prefix,
        PropertyValueSnapshot snapshot)
    {
        metadata[$"{prefix}Value"] = snapshot.ValueText;
        metadata[$"{prefix}ValueType"] = snapshot.ValueType;
        metadata[$"{prefix}ValueSource"] = snapshot.Source;
        metadata[$"{prefix}HadLocalValue"] = snapshot.HasLocalValue.ToString(CultureInfo.InvariantCulture);
    }

    private static IReadOnlyDictionary<string, string> CreateResetMetadata(
        string? mutationId,
        IReadOnlyList<AppliedRuntimeMutation> mutations,
        bool resetAll,
        DateTimeOffset resetAt)
    {
        var metadata = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["operation"] = resetAll ? RuntimeMutationOperationKinds.ResetAll : RuntimeMutationOperationKinds.ResetMutation,
            ["resetAll"] = resetAll.ToString(CultureInfo.InvariantCulture),
            ["resetCount"] = mutations.Count.ToString(CultureInfo.InvariantCulture),
            ["resetAt"] = resetAt.ToString("O", CultureInfo.InvariantCulture),
            ["resetOrder"] = resetAll ? "reverse_application_order" : "single"
        };

        if (!string.IsNullOrWhiteSpace(mutationId))
        {
            metadata["mutationId"] = mutationId;
        }

        if (mutations.Count > 0)
        {
            metadata["resetMutationIds"] = string.Join(",", mutations.Select(static mutation => mutation.MutationId));
            metadata["resetOperationKinds"] = string.Join(",", mutations.Select(static mutation => mutation.OperationKind));
            metadata["resetTargets"] = string.Join(",", mutations.Select(static mutation => mutation.NodeId ?? mutation.TopLevelId));
        }

        return metadata;
    }

    private static void ResetAvaloniaProperty(
        AvaloniaObject owner,
        AvaloniaProperty property,
        object? originalValue,
        bool restoreLocalValue)
    {
        if (restoreLocalValue)
        {
            owner.SetValue(property, originalValue!, BindingPriority.LocalValue);
            return;
        }

        owner.ClearValue(property);
    }

    private static void SetClassPresence(StyledElement styledElement, string className, bool isPresent)
    {
        if (isPresent)
        {
            if (!styledElement.Classes.Contains(className))
            {
                styledElement.Classes.Add(className);
            }

            return;
        }

        styledElement.Classes.Remove(className);
    }

    private static void ResetResource(
        StyledElement styledElement,
        string resourceKey,
        bool hadLocalResource,
        object? originalValue)
    {
        if (hadLocalResource)
        {
            styledElement.Resources[resourceKey] = originalValue;
            return;
        }

        styledElement.Resources.Remove(resourceKey);
    }

    private static string NormalizeMutationName(string value)
    {
        return value
            .Replace("-", string.Empty, StringComparison.Ordinal)
            .Replace("_", string.Empty, StringComparison.Ordinal)
            .Trim()
            .ToLowerInvariant();
    }

    private static ProtocolError CreateInvalidMutationValueDiagnostic(
        RuntimeMutationOperation operation,
        string message)
    {
        var details = new Dictionary<string, string>
        {
            ["operation"] = operation.Kind,
            ["value"] = operation.Value ?? "null"
        };

        if (!string.IsNullOrWhiteSpace(operation.PropertyName))
        {
            details["propertyName"] = operation.PropertyName!;
        }

        if (!string.IsNullOrWhiteSpace(operation.ResourceKey))
        {
            details["resourceKey"] = operation.ResourceKey!;
        }

        if (!string.IsNullOrWhiteSpace(operation.ValueType))
        {
            details["valueType"] = operation.ValueType!;
        }

        return new ProtocolError(RuntimeMutationErrorCodes.InvalidRuntimeMutationValue, message, details);
    }

    private static ProtocolError CreateStaleMutationDiagnostic(
        string message,
        RuntimeTargetContext target,
        string nextAction)
    {
        var details = new Dictionary<string, string>
        {
            ["topLevelId"] = target.TopLevelId,
            ["nextAction"] = nextAction
        };

        if (!string.IsNullOrWhiteSpace(target.TreeKind))
        {
            details["treeKind"] = target.TreeKind;
        }

        if (!string.IsNullOrWhiteSpace(target.NodeId))
        {
            details["nodeId"] = target.NodeId;
        }

        return new ProtocolError(
            RuntimeMutationErrorCodes.RuntimeMutationTargetStale,
            message,
            details);
    }

    private string NextMutationId()
    {
        return NextMutationIdentity().Id;
    }

    private RuntimeMutationIdentity NextMutationIdentity()
    {
        var next = Interlocked.Increment(ref _mutationSequence);
        return new RuntimeMutationIdentity(
            $"mutation:{SessionId.Value}:{next.ToString(CultureInfo.InvariantCulture)}",
            next);
    }

    private static ProtocolError ToProtocolError(CoreError error)
    {
        return new ProtocolError(error.Code, error.Message, error.Details);
    }

    private CoreResult<InputResponse> PointerMove(TopLevel topLevel, string topLevelId, double? x, double? y)
    {
        var point = GetInputPoint(x, y);
        if (!point.Success)
        {
            return CoreResult<InputResponse>.Fail(point.Error!);
        }

        var target = topLevel.GetVisualAt(point.Value);
        var metadata = CreatePointerInputMetadata(topLevel, point.Value, target, null);
        if (target is null)
        {
            return CoreResult<InputResponse>.Ok(new InputResponse(
                SessionId,
                topLevelId,
                InputActions.PointerMove,
                handled: false,
                DateTimeOffset.UtcNow,
                metadata: metadata));
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
            CreateNodeId(inputTarget, TreeKinds.Visual),
            CreateNodeTarget(topLevelId, TreeKinds.Visual, topLevel, inputTarget),
            metadata: CreatePointerInputMetadata(topLevel, point.Value, target, inputTarget)));
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
        var metadata = CreatePointerInputMetadata(topLevel, point.Value, target, null);
        if (target is null)
        {
            return CoreResult<InputResponse>.Ok(new InputResponse(
                SessionId,
                topLevelId,
                action,
                handled: false,
                DateTimeOffset.UtcNow,
                metadata: metadata));
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
            CreateNodeId(inputTarget, TreeKinds.Visual),
            CreateNodeTarget(topLevelId, TreeKinds.Visual, topLevel, inputTarget),
            pointerButton: "left",
            metadata: CreatePointerInputMetadata(topLevel, point.Value, target, inputTarget)));
    }

    private CoreResult<InputResponse> Click(
        TopLevel topLevel,
        string topLevelId,
        double? x,
        double? y,
        string? targetNodeId,
        bool validateOnly = false)
    {
        if ((x is null) != (y is null))
        {
            return CoreResult<InputResponse>.Fail(new CoreError(
                BridgeErrorCodes.InvalidInputRequest,
                "Click input requires both x and y when either explicit coordinate is provided."));
        }

        var normalizedTargetNodeId = string.IsNullOrWhiteSpace(targetNodeId) ? null : targetNodeId.Trim();
        object? requestedNode = null;
        Visual? requestedVisual = null;
        Button? requestedButton = null;
        Rect? requestedBounds = null;
        if (normalizedTargetNodeId is not null)
        {
            requestedNode = FindNodeById(topLevel, normalizedTargetNodeId);
            if (requestedNode is null)
            {
                return CoreResult<InputResponse>.Fail(new CoreError(
                    BridgeErrorCodes.NodeNotFound,
                    $"Click target node '{normalizedTargetNodeId}' was not found.",
                    CreateTargetErrorDetails(null, null, normalizedTargetNodeId)));
            }

            requestedVisual = requestedNode as Visual;
            if (requestedVisual is null)
            {
                return InvalidClickTarget(
                    normalizedTargetNodeId,
                    requestedNode.GetType().FullName ?? requestedNode.GetType().Name,
                    "Click target is not a visual element.");
            }

            requestedButton = requestedVisual as Button ?? requestedVisual.FindAncestorOfType<Button>();
            requestedBounds = GetGlobalBounds(requestedVisual, topLevel);
        }

        var coordinateSource = x is not null ? "explicit" : "target_center";
        Point point;
        if (x is not null && y is not null)
        {
            point = new Point(x.Value, y.Value);
        }
        else
        {
            if (normalizedTargetNodeId is null)
            {
                return CoreResult<InputResponse>.Fail(new CoreError(
                    BridgeErrorCodes.InvalidInputRequest,
                    "Click input requires x and y coordinates or a target node id."));
            }

            if (!requestedVisual!.IsEffectivelyVisible)
            {
                return InvalidClickTarget(
                    normalizedTargetNodeId,
                    requestedVisual.GetType().FullName ?? requestedVisual.GetType().Name,
                    "Click target is not effectively visible.");
            }

            if (requestedBounds is null
                || requestedBounds.Value.Width <= 0
                || requestedBounds.Value.Height <= 0
                || !double.IsFinite(requestedBounds.Value.X)
                || !double.IsFinite(requestedBounds.Value.Y)
                || !double.IsFinite(requestedBounds.Value.Width)
                || !double.IsFinite(requestedBounds.Value.Height))
            {
                return InvalidClickTarget(
                    normalizedTargetNodeId,
                    requestedVisual.GetType().FullName ?? requestedVisual.GetType().Name,
                    "Click target does not have finite, positive arranged bounds.",
                    requestedBounds);
            }

            point = requestedBounds.Value.Center;
            var topLevelBounds = new Rect(topLevel.Bounds.Size);
            if (!topLevelBounds.Contains(point))
            {
                return InvalidClickTarget(
                    normalizedTargetNodeId,
                    requestedVisual.GetType().FullName ?? requestedVisual.GetType().Name,
                    "Click target center is clipped outside the top-level bounds.",
                    requestedBounds);
            }
        }

        var hitTarget = topLevel.GetVisualAt(point);
        var button = hitTarget as Button ?? hitTarget?.FindAncestorOfType<Button>();
        if (button is null)
        {
            return InvalidClickTarget(
                normalizedTargetNodeId,
                requestedNode?.GetType().FullName ?? requestedNode?.GetType().Name ?? "not_available",
                "Click currently supports Button targets only.",
                requestedBounds,
                hitTarget);
        }

        if (requestedButton is not null && !ReferenceEquals(requestedButton, button))
        {
            return InvalidClickTarget(
                normalizedTargetNodeId!,
                requestedVisual!.GetType().FullName ?? requestedVisual.GetType().Name,
                "Click coordinates do not hit the requested target Button.",
                requestedBounds,
                hitTarget);
        }

        if (normalizedTargetNodeId is not null && requestedButton is null)
        {
            return InvalidClickTarget(
                normalizedTargetNodeId,
                requestedVisual!.GetType().FullName ?? requestedVisual.GetType().Name,
                "Requested click target is not a Button or inside a Button.",
                requestedBounds,
                hitTarget);
        }

        if (!validateOnly)
        {
            button.Focus(NavigationMethod.Pointer);
            button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent, button));
        }

        var metadata = new Dictionary<string, string>(
            CreatePointerInputMetadata(topLevel, point, hitTarget, button),
            StringComparer.Ordinal)
        {
            ["coordinateSource"] = coordinateSource,
            ["requestedX"] = x?.ToString("0.###", CultureInfo.InvariantCulture) ?? "not_provided",
            ["requestedY"] = y?.ToString("0.###", CultureInfo.InvariantCulture) ?? "not_provided",
            ["dryRun"] = validateOnly.ToString().ToLowerInvariant(),
            ["validationStatus"] = validateOnly ? "validated" : "executed"
        };
        if (normalizedTargetNodeId is not null)
        {
            metadata["requestedTargetNodeId"] = normalizedTargetNodeId;
        }

        if (requestedBounds is not null)
        {
            metadata["requestedTargetBounds"] = FormatRect(requestedBounds.Value);
        }

        return CoreResult<InputResponse>.Ok(new InputResponse(
            SessionId,
            topLevelId,
            InputActions.Click,
            handled: true,
            DateTimeOffset.UtcNow,
            CreateNodeId(button, TreeKinds.Visual),
            CreateNodeTarget(topLevelId, TreeKinds.Visual, topLevel, button),
            pointerButton: "left",
            metadata: metadata));
    }

    private CoreResult<InputResponse> InvalidClickTarget(
        string? targetNodeId,
        string targetType,
        string message,
        Rect? targetBounds = null,
        Visual? hitTarget = null)
    {
        var details = new Dictionary<string, string>
        {
            ["targetNodeId"] = targetNodeId ?? "not_provided",
            ["targetType"] = targetType,
            ["coordinateSpace"] = "top_level_dip",
            ["nextAction"] = "Refresh the visual tree and use a visible Button target, or provide explicit x and y coordinates that hit it."
        };
        if (targetBounds is not null)
        {
            details["targetBounds"] = FormatRect(targetBounds.Value);
        }

        if (hitTarget is not null)
        {
            details["hitNodeId"] = CreateNodeId(hitTarget, TreeKinds.Visual);
            details["hitNodeType"] = hitTarget.GetType().FullName ?? hitTarget.GetType().Name;
        }

        return CoreResult<InputResponse>.Fail(new CoreError(
            BridgeErrorCodes.UnsupportedInputAction,
            message,
            details));
    }

    private static IReadOnlyDictionary<string, string> CreatePointerInputMetadata(
        TopLevel topLevel,
        Point point,
        Visual? hitVisual,
        InputElement? inputTarget)
    {
        var metadata = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["coordinateSpace"] = "top_level_dip",
            ["hitTestSource"] = "TopLevel.GetVisualAt",
            ["requestedX"] = point.X.ToString("0.###", CultureInfo.InvariantCulture),
            ["requestedY"] = point.Y.ToString("0.###", CultureInfo.InvariantCulture),
            ["effectiveX"] = point.X.ToString("0.###", CultureInfo.InvariantCulture),
            ["effectiveY"] = point.Y.ToString("0.###", CultureInfo.InvariantCulture),
            ["hitVisualNodeId"] = hitVisual is null ? "not_available" : CreateNodeId(hitVisual, TreeKinds.Visual),
            ["hitVisualNodeType"] = hitVisual?.GetType().FullName ?? "not_available",
            ["inputTargetNodeId"] = inputTarget is null ? "not_available" : CreateNodeId(inputTarget, TreeKinds.Visual),
            ["inputTargetNodeType"] = inputTarget?.GetType().FullName ?? "not_available"
        };

        if (hitVisual is not null)
        {
            var hitBounds = GetGlobalBounds(hitVisual, topLevel);
            if (hitBounds is not null)
            {
                metadata["hitVisualBounds"] = FormatRect(hitBounds.Value);
            }
        }

        if (inputTarget is Visual inputVisual)
        {
            var targetBounds = GetGlobalBounds(inputVisual, topLevel);
            if (targetBounds is not null)
            {
                metadata["inputTargetBounds"] = FormatRect(targetBounds.Value);
            }
        }

        return metadata;
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
            CreateNodeId(textBox, TreeKinds.Visual),
            CreateNodeTarget(topLevelId, TreeKinds.Visual, topLevel, textBox)));
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
            CreateNodeId(textBox, TreeKinds.Visual),
            CreateNodeTarget(topLevelId, TreeKinds.Visual, topLevel, textBox)));
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
            CreateNodeId(target.Value, TreeKinds.Visual),
            CreateNodeTarget(topLevelId, TreeKinds.Visual, topLevel, target.Value)));
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
            CreateNodeId(target, TreeKinds.Visual),
            CreateNodeTarget(topLevelId, TreeKinds.Visual, topLevel, target),
            key.Value.ToString(),
            modifiers.Value.ToString()));
    }

    private CoreResult<InputResponse> SelectTarget(
        TopLevel topLevel,
        string topLevelId,
        string? targetNodeId,
        string? selectionText)
    {
        if (string.IsNullOrWhiteSpace(targetNodeId))
        {
            return CoreResult<InputResponse>.Fail(new CoreError(
                BridgeErrorCodes.InvalidInputRequest,
                "Select input requires target node id."));
        }

        if (string.IsNullOrWhiteSpace(selectionText))
        {
            return CoreResult<InputResponse>.Fail(new CoreError(
                BridgeErrorCodes.InvalidInputRequest,
                "Select input requires text containing a selected index or item text."));
        }

        var node = FindNodeById(topLevel, targetNodeId.Trim());
        if (node is null)
        {
            return CoreResult<InputResponse>.Fail(new CoreError(
                BridgeErrorCodes.NodeNotFound,
                $"Select target node '{targetNodeId}' was not found.",
                CreateTargetErrorDetails(null, null, targetNodeId.Trim())));
        }

        var selector = node as SelectingItemsControl
            ?? (node as Visual)?.FindAncestorOfType<SelectingItemsControl>();
        if (selector is null)
        {
            return CoreResult<InputResponse>.Fail(new CoreError(
                BridgeErrorCodes.UnsupportedInputAction,
                "Select target is not a SelectingItemsControl."));
        }

        var requested = selectionText.Trim();
        var previousIndex = selector.SelectedIndex;
        var index = TryResolveSelectionIndex(selector, requested);
        if (index is null)
        {
            return CoreResult<InputResponse>.Fail(new CoreError(
                BridgeErrorCodes.UnsupportedInputAction,
                $"Select target does not contain item '{requested}'.",
                new Dictionary<string, string>
                {
                    ["targetNodeId"] = targetNodeId.Trim(),
                    ["requestedSelection"] = requested,
                    ["itemCount"] = selector.ItemCount.ToString(CultureInfo.InvariantCulture)
                }));
        }

        selector.SelectedIndex = index.Value;
        selector.Focus(NavigationMethod.Unspecified);

        var metadata = new Dictionary<string, string>
        {
            ["selectionKind"] = int.TryParse(requested, NumberStyles.Integer, CultureInfo.InvariantCulture, out _)
                ? "index"
                : "text",
            ["requestedSelection"] = requested,
            ["previousSelectedIndex"] = previousIndex.ToString(CultureInfo.InvariantCulture),
            ["selectedIndex"] = selector.SelectedIndex.ToString(CultureInfo.InvariantCulture),
            ["selectedItem"] = FormatComputedValue(selector.SelectedItem),
            ["itemCount"] = selector.ItemCount.ToString(CultureInfo.InvariantCulture)
        };

        return CoreResult<InputResponse>.Ok(new InputResponse(
            SessionId,
            topLevelId,
            InputActions.Select,
            handled: true,
            DateTimeOffset.UtcNow,
            CreateNodeId(selector, TreeKinds.Visual),
            CreateNodeTarget(topLevelId, TreeKinds.Visual, topLevel, selector),
            metadata: metadata));
    }

    private CoreResult<InputResponse> SemanticAutomationAction(
        TopLevel topLevel,
        string topLevelId,
        string action,
        string? targetNodeId,
        bool validateOnly = false)
    {
        if (string.IsNullOrWhiteSpace(targetNodeId))
        {
            return CoreResult<InputResponse>.Fail(new CoreError(
                BridgeErrorCodes.InvalidInputRequest,
                $"Input action '{action}' requires target node id."));
        }

        var normalizedTargetNodeId = targetNodeId.Trim();
        var node = FindNodeById(topLevel, normalizedTargetNodeId);
        if (node is null)
        {
            return CoreResult<InputResponse>.Fail(new CoreError(
                BridgeErrorCodes.NodeNotFound,
                $"Semantic automation target node '{normalizedTargetNodeId}' was not found.",
                CreateTargetErrorDetails(null, null, normalizedTargetNodeId)));
        }

        if (node is not Control control)
        {
            return UnsupportedAutomationPattern(
                action,
                normalizedTargetNodeId,
                node.GetType().FullName ?? node.GetType().Name,
                requiredPattern: GetAutomationPatternName(action));
        }

        if (!control.IsEffectivelyEnabled)
        {
            return CoreResult<InputResponse>.Fail(new CoreError(
                BridgeErrorCodes.UnsupportedInputAction,
                $"Semantic automation target '{normalizedTargetNodeId}' is disabled.",
                new Dictionary<string, string>
                {
                    ["action"] = action,
                    ["targetNodeId"] = normalizedTargetNodeId,
                    ["targetType"] = control.GetType().FullName ?? control.GetType().Name,
                    ["requiredPattern"] = GetAutomationPatternName(action),
                    ["enabled"] = bool.FalseString
                }));
        }

        var peer = ControlAutomationPeer.CreatePeerForElement(control);
        if (peer is null)
        {
            return UnsupportedAutomationPattern(
                action,
                normalizedTargetNodeId,
                control.GetType().FullName ?? control.GetType().Name,
                requiredPattern: GetAutomationPatternName(action));
        }

        var supportedActions = GetSupportedSemanticAutomationActions(peer);
        if (!supportedActions.Contains(action, StringComparer.Ordinal))
        {
            return UnsupportedAutomationPattern(
                action,
                normalizedTargetNodeId,
                control.GetType().FullName ?? control.GetType().Name,
                GetAutomationPatternName(action),
                supportedActions);
        }

        var previousState = GetAutomationState(peer, action);
        try
        {
            var handled = validateOnly || action switch
            {
                InputActions.Invoke => Invoke(peer),
                InputActions.Select => Select(peer),
                InputActions.Toggle => Toggle(peer),
                InputActions.Expand => Expand(peer),
                InputActions.Collapse => Collapse(peer),
                _ => false
            };

            if (!handled)
            {
                return UnsupportedAutomationPattern(
                    action,
                    normalizedTargetNodeId,
                    control.GetType().FullName ?? control.GetType().Name,
                    GetAutomationPatternName(action),
                    GetSupportedSemanticAutomationActions(peer));
            }
        }
        catch (InvalidOperationException exception)
        {
            return CoreResult<InputResponse>.Fail(new CoreError(
                BridgeErrorCodes.UnsupportedInputAction,
                $"Automation pattern '{GetAutomationPatternName(action)}' could not execute action '{action}'.",
                new Dictionary<string, string>
                {
                    ["action"] = action,
                    ["targetNodeId"] = normalizedTargetNodeId,
                    ["targetType"] = control.GetType().FullName ?? control.GetType().Name,
                    ["requiredPattern"] = GetAutomationPatternName(action),
                    ["reason"] = exception.Message
                }));
        }

        var metadata = new Dictionary<string, string>
        {
            ["automationPattern"] = GetAutomationPatternName(action),
            ["automationPeer"] = peer.GetType().FullName ?? peer.GetType().Name,
            ["targetType"] = control.GetType().FullName ?? control.GetType().Name,
            ["supportedSemanticActions"] = string.Join(",", supportedActions),
            ["dryRun"] = validateOnly.ToString().ToLowerInvariant(),
            ["validationStatus"] = validateOnly ? "validated" : "executed"
        };
        if (previousState is not null)
        {
            metadata["previousState"] = previousState;
        }

        var currentState = GetAutomationState(peer, action);
        if (currentState is not null)
        {
            metadata["currentState"] = currentState;
        }

        return CoreResult<InputResponse>.Ok(new InputResponse(
            SessionId,
            topLevelId,
            action,
            handled: !validateOnly,
            DateTimeOffset.UtcNow,
            CreateNodeId(control, TreeKinds.Visual),
            CreateNodeTarget(topLevelId, TreeKinds.Visual, topLevel, control),
            metadata: metadata));
    }

    private static bool Invoke(AutomationPeer peer)
    {
        var provider = peer.GetProvider<IInvokeProvider>();
        if (provider is null)
        {
            return false;
        }

        provider.Invoke();
        return true;
    }

    private static bool Select(AutomationPeer peer)
    {
        var provider = peer.GetProvider<ISelectionItemProvider>();
        if (provider is null)
        {
            return false;
        }

        provider.Select();
        return true;
    }

    private static bool Toggle(AutomationPeer peer)
    {
        var provider = peer.GetProvider<IToggleProvider>();
        if (provider is null)
        {
            return false;
        }

        provider.Toggle();
        return true;
    }

    private static bool Expand(AutomationPeer peer)
    {
        var provider = peer.GetProvider<IExpandCollapseProvider>();
        if (provider is null)
        {
            return false;
        }

        provider.Expand();
        return true;
    }

    private static bool Collapse(AutomationPeer peer)
    {
        var provider = peer.GetProvider<IExpandCollapseProvider>();
        if (provider is null)
        {
            return false;
        }

        provider.Collapse();
        return true;
    }

    private static string? GetAutomationState(AutomationPeer peer, string action)
    {
        return action switch
        {
            InputActions.Select => peer.GetProvider<ISelectionItemProvider>()?.IsSelected.ToString(),
            InputActions.Toggle => peer.GetProvider<IToggleProvider>()?.ToggleState.ToString(),
            InputActions.Expand or InputActions.Collapse
                => peer.GetProvider<IExpandCollapseProvider>()?.ExpandCollapseState.ToString(),
            _ => null
        };
    }

    private static IReadOnlyList<string> GetSupportedSemanticAutomationActions(AutomationPeer peer)
    {
        var actions = new List<string>(5);
        if (peer.GetProvider<IInvokeProvider>() is not null)
        {
            actions.Add(InputActions.Invoke);
        }

        if (peer.GetProvider<ISelectionItemProvider>() is not null)
        {
            actions.Add(InputActions.Select);
        }

        if (peer.GetProvider<IToggleProvider>() is not null)
        {
            actions.Add(InputActions.Toggle);
        }

        if (peer.GetProvider<IExpandCollapseProvider>() is not null)
        {
            actions.Add(InputActions.Expand);
            actions.Add(InputActions.Collapse);
        }

        return actions;
    }

    private CoreResult<InputResponse> UnsupportedAutomationPattern(
        string action,
        string targetNodeId,
        string targetType,
        string requiredPattern,
        IReadOnlyList<string>? supportedActions = null)
    {
        return CoreResult<InputResponse>.Fail(new CoreError(
            BridgeErrorCodes.UnsupportedInputAction,
            $"Target '{targetNodeId}' does not support automation pattern '{requiredPattern}' for action '{action}'.",
            new Dictionary<string, string>
            {
                ["action"] = action,
                ["targetNodeId"] = targetNodeId,
                ["targetType"] = targetType,
                ["requiredPattern"] = requiredPattern,
                ["supportedSemanticActions"] = supportedActions is { Count: > 0 }
                    ? string.Join(",", supportedActions)
                    : "none"
            }));
    }

    private static string GetAutomationPatternName(string action)
    {
        return action switch
        {
            InputActions.Invoke => nameof(IInvokeProvider),
            InputActions.Select => nameof(ISelectionItemProvider),
            InputActions.Toggle => nameof(IToggleProvider),
            InputActions.Expand or InputActions.Collapse => nameof(IExpandCollapseProvider),
            _ => "unknown"
        };
    }

    private CoreResult<InputResponse> ScrollTarget(
        TopLevel topLevel,
        string topLevelId,
        string? targetNodeId,
        double? deltaX,
        double? deltaY)
    {
        if (deltaX is null && deltaY is null)
        {
            return CoreResult<InputResponse>.Fail(new CoreError(
                BridgeErrorCodes.InvalidInputRequest,
                "Scroll input requires x or y delta."));
        }

        ScrollViewer? viewer;
        if (!string.IsNullOrWhiteSpace(targetNodeId))
        {
            var node = FindNodeById(topLevel, targetNodeId.Trim());
            if (node is null)
            {
                return CoreResult<InputResponse>.Fail(new CoreError(
                    BridgeErrorCodes.NodeNotFound,
                    $"Scroll target node '{targetNodeId}' was not found.",
                    CreateTargetErrorDetails(null, null, targetNodeId.Trim())));
            }

            viewer = node as ScrollViewer ?? (node as Visual)?.FindAncestorOfType<ScrollViewer>();
        }
        else
        {
            var focused = topLevel.FocusManager?.GetFocusedElement();
            viewer = focused as ScrollViewer ?? (focused as Visual)?.FindAncestorOfType<ScrollViewer>();
        }

        if (viewer is null)
        {
            return CoreResult<InputResponse>.Fail(new CoreError(
                BridgeErrorCodes.UnsupportedInputAction,
                "Scroll input requires a ScrollViewer target node or a focused element inside one."));
        }

        var previous = viewer.Offset;
        var maximum = viewer.ScrollBarMaximum;
        var next = new Vector(
            Math.Clamp(previous.X + (deltaX ?? 0), 0, Math.Max(0, maximum.X)),
            Math.Clamp(previous.Y + (deltaY ?? 0), 0, Math.Max(0, maximum.Y)));
        viewer.Offset = next;

        var metadata = new Dictionary<string, string>
        {
            ["previousOffsetX"] = previous.X.ToString("0.###", CultureInfo.InvariantCulture),
            ["previousOffsetY"] = previous.Y.ToString("0.###", CultureInfo.InvariantCulture),
            ["offsetX"] = viewer.Offset.X.ToString("0.###", CultureInfo.InvariantCulture),
            ["offsetY"] = viewer.Offset.Y.ToString("0.###", CultureInfo.InvariantCulture),
            ["maximumX"] = maximum.X.ToString("0.###", CultureInfo.InvariantCulture),
            ["maximumY"] = maximum.Y.ToString("0.###", CultureInfo.InvariantCulture)
        };

        return CoreResult<InputResponse>.Ok(new InputResponse(
            SessionId,
            topLevelId,
            InputActions.Scroll,
            handled: true,
            DateTimeOffset.UtcNow,
            CreateNodeId(viewer, TreeKinds.Visual),
            CreateNodeTarget(topLevelId, TreeKinds.Visual, topLevel, viewer),
            wheelDeltaX: deltaX,
            wheelDeltaY: deltaY,
            metadata: metadata));
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

    private static int? TryResolveSelectionIndex(SelectingItemsControl selector, string requested)
    {
        if (int.TryParse(requested, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedIndex))
        {
            return parsedIndex >= 0 && parsedIndex < selector.ItemCount
                ? parsedIndex
                : null;
        }

        for (var index = 0; index < selector.ItemCount; index++)
        {
            var item = selector.ItemsView[index];
            var itemText = item switch
            {
                null => string.Empty,
                ContentControl { Content: { } content } => content.ToString() ?? string.Empty,
                _ => item.ToString() ?? string.Empty
            };

            if (string.Equals(itemText, requested, StringComparison.OrdinalIgnoreCase))
            {
                return index;
            }
        }

        return null;
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

    private static CoreResult<T> InvalidInspectRequest<T>(string message)
    {
        return CoreResult<T>.Fail(new CoreError(BridgeErrorCodes.InvalidInspectRequest, message));
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

    private static CoreResult<T> NodeNotFound<T>(
        string topLevelId,
        string treeKind,
        string nodeId)
    {
        return CoreResult<T>.Fail(new CoreError(
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

    private void CollectMatches(
        TreeNodeSummary node,
        string? nodeType,
        string? name,
        string? automationId,
        string? text,
        List<string> path,
        List<FindNodeMatch> matches,
        int maxResults,
        bool includeChildren,
        bool includeBounds,
        bool includeAccessibility,
        bool includeBindings,
        int maxResponseDepth)
    {
        if (matches.Count >= maxResults)
        {
            return;
        }

        path.Add(node.NodeId);

        if (Matches(node, nodeType, name, automationId, text))
        {
            matches.Add(new FindNodeMatch(
                ProjectFindNode(node, includeChildren, includeBounds, includeAccessibility, includeBindings, maxResponseDepth),
                path.ToArray()));
        }

        foreach (var child in node.Children)
        {
            CollectMatches(
                child, nodeType, name, automationId, text, path, matches, maxResults,
                includeChildren, includeBounds, includeAccessibility, includeBindings, maxResponseDepth);
            if (matches.Count >= maxResults)
            {
                break;
            }
        }

        path.RemoveAt(path.Count - 1);
    }

    private TreeNodeSummary ProjectFindNode(
        TreeNodeSummary node,
        bool includeChildren,
        bool includeBounds,
        bool includeAccessibility,
        bool includeBindings,
        int remainingDepth)
    {
        var children = includeChildren && remainingDepth > 0
            ? node.Children.Select(child => ProjectFindNode(
                child,
                true,
                includeBounds,
                includeAccessibility,
                includeBindings,
                remainingDepth - 1)).ToArray()
            : [];

        return new TreeNodeSummary(
            node.NodeId,
            node.NodeType,
            node.Name,
            node.AutomationId,
            node.Text,
            includeBounds ? node.Bounds : null,
            node.Classes,
            children,
            node.Target,
            includeAccessibility ? node.AccessibilityState : null,
            node.ValidationState,
            sourceMap: null,
            bindingSummary: includeBindings ? CreateBindingSummary(node) : null);
    }

    private RuntimeBindingSummary CreateBindingSummary(TreeNodeSummary summary)
    {
        var target = summary.Target;
        if (target is null)
        {
            return new RuntimeBindingSummary("not_available", null, 0);
        }

        var topLevel = FindTopLevel(target.TopLevelId);
        object? node = target.TreeKind switch
        {
            TreeKinds.Visual when topLevel is not null => FindVisualNodeById(topLevel, target.NodeId!),
            TreeKinds.Logical when topLevel is ILogical logical => FindLogicalNodeById(logical, target.NodeId!),
            _ => null
        };
        if (node is null)
        {
            return new RuntimeBindingSummary("stale_target", null, 0);
        }

        var computedProperties = GetComputedProperties(node);
        var sourceMap = CreateRuntimeSourceMap(node, computedProperties);
        var state = GetBindingState(node, sourceMap, computedProperties);
        var entries = state.BoundProperties
            .Take(RuntimeBindingSummary.MaximumEntries)
            .Select(static property => new RuntimeBindingSummaryEntry(
                property.PropertyName,
                property.BindingPath,
                property.Status,
                property.ResolvedValueStatus,
                property.CompiledBindingStatus))
            .ToArray();
        return new RuntimeBindingSummary(
            state.BindingMetadataStatus,
            state.DataContextType,
            state.BoundProperties.Count,
            entries,
            state.BoundProperties.Count > entries.Length);
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

    private TreeNodeSummary SerializeVisualNode(Visual visual, string topLevelId, TopLevel topLevel, int depth, int maxDepth)
    {
        var children = depth >= maxDepth
            ? Array.Empty<TreeNodeSummary>()
            : visual.GetVisualChildren()
                .Select(child => SerializeVisualNode(child, topLevelId, topLevel, depth + 1, maxDepth))
                .ToArray();

        return CreateNodeSummary(topLevelId, visual, TreeKinds.Visual, topLevel, children);
    }

    private TreeNodeSummary SerializeLogicalNode(ILogical logical, string topLevelId, TopLevel topLevel, int depth, int maxDepth)
    {
        var children = depth >= maxDepth
            ? Array.Empty<TreeNodeSummary>()
            : logical.GetLogicalChildren()
                .Select(child => SerializeLogicalNode(child, topLevelId, topLevel, depth + 1, maxDepth))
                .ToArray();

        return CreateNodeSummary(topLevelId, logical, TreeKinds.Logical, topLevel, children);
    }

    private TreeNodeSummary CreateNodeSummary(
        string topLevelId,
        object node,
        string treeKind,
        TopLevel topLevel,
        IReadOnlyList<TreeNodeSummary> children)
    {
        var computedProperties = GetComputedProperties(node);
        return new TreeNodeSummary(
            CreateNodeId(node, treeKind),
            node.GetType().FullName ?? node.GetType().Name,
            GetName(node),
            GetAutomationId(node),
            GetText(node),
            GetBounds(node),
            GetClasses(node),
            children,
            CreateNodeTarget(topLevelId, treeKind, topLevel, node),
            GetAccessibilityState(node),
            GetValidationState(node),
            CreateRuntimeSourceMap(node, computedProperties));
    }

    private RuntimeTargetContext CreateTopLevelTarget(string topLevelId, TopLevel topLevel)
    {
        return new RuntimeTargetContext(
            SessionId,
            topLevelId,
            capturedAt: DateTimeOffset.UtcNow,
            topLevelGeneration: CreateObjectGeneration(topLevel));
    }

    private RuntimeTargetContext CreateTreeTarget(string topLevelId, string treeKind, TopLevel topLevel)
    {
        return new RuntimeTargetContext(
            SessionId,
            topLevelId,
            treeKind,
            capturedAt: DateTimeOffset.UtcNow,
            targetKind: "tree",
            topLevelGeneration: CreateObjectGeneration(topLevel));
    }

    private RuntimeTargetContext CreateNodeTarget(string topLevelId, string treeKind, TopLevel topLevel, object node)
    {
        return new RuntimeTargetContext(
            SessionId,
            topLevelId,
            treeKind,
            CreateNodeId(node, treeKind),
            DateTimeOffset.UtcNow,
            topLevelGeneration: CreateObjectGeneration(topLevel),
            nodeGeneration: CreateObjectGeneration(node));
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

    private static string CreateObjectGeneration(object value)
    {
        return RuntimeHelpers.GetHashCode(value).ToString("x", CultureInfo.InvariantCulture);
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

    private static NodeBounds? GetTreeNodeBounds(TopLevel topLevel, object node)
    {
        if (node is not Visual visual)
        {
            return null;
        }

        var origin = visual.TranslatePoint(new Point(0, 0), topLevel) ?? new Point(visual.Bounds.X, visual.Bounds.Y);
        return new NodeBounds(
            origin.X,
            origin.Y,
            visual.Bounds.Width,
            visual.Bounds.Height);
    }

    private static IReadOnlyList<string> GetClasses(object node)
    {
        return node is StyledElement styledElement
            ? styledElement.Classes.ToArray()
            : Array.Empty<string>();
    }

    private static RuntimeAccessibilityState? GetAccessibilityState(object node)
    {
        if (node is not StyledElement styledElement)
        {
            return null;
        }

        var labeledBy = AutomationProperties.GetLabeledBy(styledElement);
        var inputElement = node as InputElement;
        return new RuntimeAccessibilityState(
            "avalonia_public_automation_properties",
            AutomationProperties.GetName(styledElement),
            AutomationProperties.GetHelpText(styledElement),
            AutomationProperties.GetAccessKey(styledElement),
            labeledBy is null ? null : FirstNonEmpty(GetName(labeledBy), GetText(labeledBy), labeledBy.GetType().Name),
            AutomationProperties.GetControlTypeOverride(styledElement)?.ToString(),
            inputElement?.Focusable,
            inputElement is null ? null : inputElement.GetValue(KeyboardNavigation.IsTabStopProperty),
            inputElement is null ? null : inputElement.GetValue(KeyboardNavigation.TabIndexProperty),
            inputElement?.IsEnabled);
    }

    private static RuntimeValidationState? GetValidationState(object node)
    {
        if (node is not Control control)
        {
            return null;
        }

        var hasErrors = DataValidationErrors.GetHasErrors(control);
        var errors = (DataValidationErrors.GetErrors(control) ?? [])
            .Select(FormatValidationError)
            .Where(static error => !string.IsNullOrWhiteSpace(error))
            .ToArray();
        return new RuntimeValidationState(
            hasErrors ? "has_errors" : "clean",
            "avalonia_public_data_validation_errors",
            hasErrors,
            errors.Length,
            errors);
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

    private RuntimeScrollState? GetScrollState(TopLevel topLevel, string topLevelId, object node)
    {
        if (node is not ScrollViewer viewer)
        {
            return null;
        }

        var maximum = viewer.ScrollBarMaximum;
        return new RuntimeScrollState(
            "available",
            new RuntimeVector(viewer.Offset.X, viewer.Offset.Y),
            new RuntimeSize(viewer.Extent.Width, viewer.Extent.Height),
            new RuntimeSize(viewer.Viewport.Width, viewer.Viewport.Height),
            new RuntimeVector(maximum.X, maximum.Y),
            viewer.HorizontalScrollBarVisibility.ToString(),
            viewer.VerticalScrollBarVisibility.ToString(),
            CreateContentLayoutMetrics(topLevel, topLevelId, viewer));
    }

    private RuntimeLayoutMetrics CreateContentLayoutMetrics(TopLevel topLevel, string topLevelId, ScrollViewer viewer)
    {
        if (viewer.Content is not object content)
        {
            return new RuntimeLayoutMetrics("not_available");
        }

        if (content is not Layoutable layoutable)
        {
            return new RuntimeLayoutMetrics(
                "not_available",
                nodeType: content.GetType().FullName ?? content.GetType().Name);
        }

        return new RuntimeLayoutMetrics(
            "available",
            CreateNodeId(layoutable, TreeKinds.Visual),
            layoutable.GetType().FullName ?? layoutable.GetType().Name,
            GetBounds(layoutable),
            new RuntimeSize(layoutable.DesiredSize.Width, layoutable.DesiredSize.Height),
            new RuntimeSize(layoutable.Bounds.Width, layoutable.Bounds.Height),
            CreateNodeTarget(topLevelId, TreeKinds.Visual, topLevel, layoutable));
    }

    private static RuntimeBindingState GetBindingState(
        object node,
        RuntimeNodeSourceMap? sourceMap,
        IReadOnlyList<ComputedPropertyValue> computedProperties)
    {
        if (node is not StyledElement styledElement)
        {
            return new RuntimeBindingState(
                "not_available",
                bindingMetadataStatus: "not_available",
                diagnostics:
                [
                    new ProtocolError(
                        "runtime_binding_metadata_not_available",
                        "Runtime binding state requires a StyledElement target.")
                ],
                sourceMap: sourceMap);
        }

        var dataContext = styledElement.DataContext;
        var dataContextStatus = dataContext is null ? "not_available" : "available";
        var diagnostics = new List<ProtocolError>();
        var boundProperties = GetRuntimeBoundProperties(styledElement, sourceMap, computedProperties);
        if (boundProperties.Count == 0)
        {
            diagnostics.Add(new ProtocolError(
                "runtime_binding_path_metadata_not_available",
                "Avalonia public runtime APIs did not expose active binding expression metadata for this selected node; sourceMap.bindings may still contain XAML-declared binding paths when XAML source info is available."));
        }

        return new RuntimeBindingState(
            dataContextStatus,
            dataContext?.GetType().FullName ?? dataContext?.GetType().Name,
            boundProperties.Count == 0 ? "not_available" : "available",
            boundProperties,
            diagnostics,
            sourceMap);
    }

    private static IReadOnlyList<RuntimeBoundProperty> GetRuntimeBoundProperties(
        StyledElement styledElement,
        RuntimeNodeSourceMap? sourceMap,
        IReadOnlyList<ComputedPropertyValue> computedProperties)
    {
        if (styledElement is not AvaloniaObject avaloniaObject)
        {
            return Array.Empty<RuntimeBoundProperty>();
        }

        var sourceBindings = sourceMap?.Bindings ?? Array.Empty<RuntimeSourceBinding>();
        var sourceBindingsByProperty = sourceBindings
            .GroupBy(static binding => binding.TargetProperty, StringComparer.Ordinal)
            .ToDictionary(static group => group.Key, static group => group.First(), StringComparer.Ordinal);
        var properties = GetInspectableProperties(styledElement).ToArray();
        var results = new List<RuntimeBoundProperty>();

        foreach (var property in properties)
        {
            var expression = TryGetBindingExpression(avaloniaObject, property);
            sourceBindingsByProperty.TryGetValue(property.Name, out var sourceBinding);
            if (expression is null && sourceBinding is null)
            {
                continue;
            }

            var computed = computedProperties.FirstOrDefault(value => string.Equals(value.Name, property.Name, StringComparison.Ordinal));
            var value = computed?.Value ?? TryGetPropertyValue(avaloniaObject, property);
            var valueType = computed?.ValueType ?? "not_available";
            var expressionText = expression?.ToString();
            var expressionType = expression?.GetType().FullName ?? expression?.GetType().Name;
            var compiledStatus = sourceBinding?.BindingKind == "compiled"
                || expressionType?.Contains("Compiled", StringComparison.OrdinalIgnoreCase) == true
                ? "compiled"
                : expression is null
                    ? "source_declared"
                    : "runtime";

            results.Add(new RuntimeBoundProperty(
                property.Name,
                sourceBinding?.BindingPath ?? "not_available",
                value,
                valueType,
                computed?.Source ?? "binding",
                expression is null ? "source_declared" : "active",
                expressionText,
                expressionType,
                value == "not_available" ? "not_available" : "available",
                sourceBinding?.ConverterResourceKey is null ? "not_available" : "declared",
                "not_available",
                string.Equals(value, "null", StringComparison.Ordinal) ? "null" : "not_null",
                compiledStatus,
                sourceBinding));
        }

        foreach (var binding in sourceBindings)
        {
            if (results.Any(result => string.Equals(result.PropertyName, binding.TargetProperty, StringComparison.Ordinal)))
            {
                continue;
            }

            results.Add(new RuntimeBoundProperty(
                binding.TargetProperty,
                binding.BindingPath,
                "not_available",
                "not_available",
                "source_map",
                "source_declared",
                binding.Expression,
                binding.BindingKind,
                "not_available",
                binding.ConverterResourceKey is null ? "not_available" : "declared",
                "not_available",
                "not_available",
                binding.BindingKind,
                binding));
        }

        return results.Take(MaximumRuntimeSourceBindings).ToArray();
    }

    private static object? TryGetBindingExpression(AvaloniaObject avaloniaObject, AvaloniaProperty property)
    {
        try
        {
            return BindingOperations.GetBindingExpressionBase(avaloniaObject, property);
        }
        catch (InvalidOperationException)
        {
            return null;
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    private static string TryGetPropertyValue(AvaloniaObject avaloniaObject, AvaloniaProperty property)
    {
        try
        {
            return FormatComputedValue(avaloniaObject.GetValue(property));
        }
        catch (InvalidOperationException)
        {
            return "not_available";
        }
        catch (ArgumentException)
        {
            return "not_available";
        }
    }

    private static RuntimeNodeSourceMap CreateRuntimeSourceMap(
        object node,
        IReadOnlyList<ComputedPropertyValue>? computedProperties)
    {
        var sourceInfo = TryGetXamlSourceInfo(node);
        var filePath = GetSourceFilePath(sourceInfo);
        var line = sourceInfo?.LineNumber > 0 ? sourceInfo.LineNumber : (int?)null;
        var column = sourceInfo?.LinePosition > 0 ? sourceInfo.LinePosition : (int?)null;
        var sourceElement = TryReadSourceElement(filePath, line, GetName(node), node.GetType().Name);
        var propertyOrigins = CreatePropertyOrigins(computedProperties, filePath, line);
        var bindings = sourceElement?.Bindings ?? Array.Empty<RuntimeSourceBinding>();
        var diagnostics = new List<ProtocolError>();

        if (sourceInfo is null)
        {
            diagnostics.Add(new ProtocolError(
                "runtime_xaml_source_info_not_available",
                "Avalonia did not expose XAML source info for this runtime object."));
        }
        else if (filePath is null)
        {
            diagnostics.Add(new ProtocolError(
                "runtime_xaml_source_file_not_available",
                "Avalonia exposed source line metadata without a readable source file path."));
        }
        else if (sourceElement is null)
        {
            diagnostics.Add(new ProtocolError(
                "runtime_xaml_source_element_not_resolved",
                "AvaScope could not map the runtime source line to a readable XAML element start tag."));
        }

        var status = sourceInfo is not null
            ? "available"
            : propertyOrigins.Count > 0
                ? "partial"
                : "not_available";
        var provenance = sourceInfo is not null
            ? "avalonia_xaml_source_info"
            : propertyOrigins.Count > 0
                ? "avalonia_property_diagnostics"
                : "not_available";

        return new RuntimeNodeSourceMap(
            status,
            provenance,
            filePath,
            line,
            column,
            sourceElement?.XName ?? GetName(node),
            sourceElement?.ElementType ?? node.GetType().Name,
            sourceElement?.ElementPath,
            propertyOrigins,
            bindings,
            diagnostics);
    }

    private static XamlSourceInfo? TryGetXamlSourceInfo(object node)
    {
        try
        {
            return XamlSourceInfo.GetXamlSourceInfo(node);
        }
        catch (ArgumentException)
        {
            return null;
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }

    private static string? GetSourceFilePath(XamlSourceInfo? sourceInfo)
    {
        if (sourceInfo?.SourceUri is null)
        {
            return null;
        }

        var uri = sourceInfo.SourceUri;
        var path = uri.IsFile ? uri.LocalPath : uri.ToString();
        return string.IsNullOrWhiteSpace(path) ? null : path;
    }

    private static SourceElementInfo? TryReadSourceElement(
        string? filePath,
        int? line,
        string? xName,
        string fallbackElementType)
    {
        if (string.IsNullOrWhiteSpace(filePath)
            || line is null
            || !File.Exists(filePath))
        {
            return null;
        }

        try
        {
            var lines = File.ReadAllLines(filePath);
            if (line.Value < 1 || line.Value > lines.Length)
            {
                return null;
            }

            var lineIndex = line.Value - 1;
            var start = FindElementStartLine(lines, lineIndex, xName, fallbackElementType);
            if (start is null)
            {
                return null;
            }

            var end = start.Value;
            while (end + 1 < lines.Length
                && !lines[end].Contains('>')
                && end - start.Value < 32)
            {
                end++;
            }

            var snippet = string.Join(" ", lines[start.Value..(end + 1)]).Trim();
            var elementType = ExtractElementType(snippet) ?? fallbackElementType;
            var resolvedName = ExtractXName(snippet) ?? xName;
            var bindings = ExtractRuntimeSourceBindings(snippet, filePath, start.Value + 1);
            var elementPath = string.IsNullOrWhiteSpace(resolvedName)
                ? $"{elementType}@{(start.Value + 1).ToString(CultureInfo.InvariantCulture)}"
                : $"{elementType}#{resolvedName}";

            return new SourceElementInfo(
                elementType,
                elementPath,
                resolvedName,
                snippet,
                bindings);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or RegexMatchTimeoutException)
        {
            return null;
        }
    }

    private static int? FindElementStartLine(
        IReadOnlyList<string> lines,
        int lineIndex,
        string? xName,
        string fallbackElementType)
    {
        var min = Math.Max(0, lineIndex - 24);
        for (var i = lineIndex; i >= min; i--)
        {
            var text = lines[i].TrimStart();
            if (!text.StartsWith('<') || text.StartsWith("</", StringComparison.Ordinal) || text.StartsWith("<!--", StringComparison.Ordinal))
            {
                continue;
            }

            if (!string.IsNullOrWhiteSpace(xName))
            {
                var windowEnd = Math.Min(lines.Count - 1, i + 16);
                var window = string.Join(" ", lines.Skip(i).Take(windowEnd - i + 1));
                if (!window.Contains($"Name=\"{xName}\"", StringComparison.Ordinal)
                    && !window.Contains($"x:Name=\"{xName}\"", StringComparison.Ordinal)
                    && !window.Contains($"Name='{xName}'", StringComparison.Ordinal)
                    && !window.Contains($"x:Name='{xName}'", StringComparison.Ordinal))
                {
                    continue;
                }
            }

            if (text.Contains(fallbackElementType, StringComparison.Ordinal)
                || string.IsNullOrWhiteSpace(fallbackElementType)
                || Regex.IsMatch(text, @"^<\s*[\w:.]+", RegexOptions.CultureInvariant))
            {
                return i;
            }
        }

        return null;
    }

    private static string? ExtractElementType(string snippet)
    {
        var match = Regex.Match(
            snippet,
            @"<\s*(?<name>[\w:.]+)",
            RegexOptions.CultureInvariant);
        return match.Success ? match.Groups["name"].Value : null;
    }

    private static string? ExtractXName(string snippet)
    {
        var match = Regex.Match(
            snippet,
            @"(?:^|\s)(?:x:)?Name\s*=\s*[""'](?<name>[^""']+)[""']",
            RegexOptions.CultureInvariant);
        return match.Success ? match.Groups["name"].Value : null;
    }

    private static IReadOnlyList<RuntimeSourceBinding> ExtractRuntimeSourceBindings(
        string snippet,
        string filePath,
        int startLine)
    {
        var bindings = new List<RuntimeSourceBinding>();
        foreach (Match match in Regex.Matches(
            snippet,
            @"(?<property>[\w:.]+)\s*=\s*[""'](?<expression>[^""']*\{(?:Binding|CompiledBinding)[^""']*)[""']",
            RegexOptions.CultureInvariant))
        {
            var expression = match.Groups["expression"].Value;
            var bindingPath = ExtractRuntimeBindingPath(expression) ?? "not_available";
            bindings.Add(new RuntimeSourceBinding(
                NormalizeXamlPropertyName(match.Groups["property"].Value),
                bindingPath,
                expression,
                expression.Contains("{CompiledBinding", StringComparison.Ordinal) ? "compiled" : "runtime",
                sourcePath: filePath,
                line: startLine,
                converterResourceKey: ExtractRuntimeConverterResourceKey(expression),
                dataTypeName: ExtractRuntimeDataTypeName(snippet)));

            if (bindings.Count >= MaximumRuntimeSourceBindings)
            {
                break;
            }
        }

        return bindings;
    }

    private static string NormalizeXamlPropertyName(string value)
    {
        var trimmed = value.Trim();
        var propertySeparator = trimmed.LastIndexOf('.');
        if (propertySeparator >= 0 && propertySeparator < trimmed.Length - 1)
        {
            trimmed = trimmed[(propertySeparator + 1)..];
        }

        var namespaceSeparator = trimmed.LastIndexOf(':');
        return namespaceSeparator >= 0 && namespaceSeparator < trimmed.Length - 1
            ? trimmed[(namespaceSeparator + 1)..]
            : trimmed;
    }

    private static string? ExtractRuntimeBindingPath(string expression)
    {
        var pathMatch = Regex.Match(
            expression,
            @"(?:^|[,{]\s*)Path\s*=\s*(?<path>[^,}]+)",
            RegexOptions.CultureInvariant);
        if (pathMatch.Success)
        {
            return CleanRuntimeBindingToken(pathMatch.Groups["path"].Value);
        }

        var positionalMatch = Regex.Match(
            expression,
            @"\{(?:Binding|CompiledBinding)\s+(?<path>[^,}]+)",
            RegexOptions.CultureInvariant);
        if (!positionalMatch.Success)
        {
            return null;
        }

        var path = CleanRuntimeBindingToken(positionalMatch.Groups["path"].Value);
        return string.Equals(path, "}", StringComparison.Ordinal) ? null : path;
    }

    private static string CleanRuntimeBindingToken(string value)
    {
        return value.Trim().Trim('"', '\'');
    }

    private static string? ExtractRuntimeConverterResourceKey(string expression)
    {
        var match = Regex.Match(
            expression,
            @"Converter\s*=\s*\{(?:StaticResource|DynamicResource)\s+(?<key>[^},\s]+)",
            RegexOptions.CultureInvariant);
        return match.Success ? match.Groups["key"].Value : null;
    }

    private static string? ExtractRuntimeDataTypeName(string snippet)
    {
        var match = Regex.Match(
            snippet,
            @"x:DataType\s*=\s*[""'](?<type>[^""']+)[""']",
            RegexOptions.CultureInvariant);
        return match.Success ? match.Groups["type"].Value : null;
    }

    private static IReadOnlyList<RuntimeSourcePropertyOrigin> CreatePropertyOrigins(
        IReadOnlyList<ComputedPropertyValue>? computedProperties,
        string? filePath,
        int? line)
    {
        if (computedProperties is null || computedProperties.Count == 0)
        {
            return Array.Empty<RuntimeSourcePropertyOrigin>();
        }

        return computedProperties
            .Select(property => new RuntimeSourcePropertyOrigin(
                property.Name,
                property.Value,
                property.ValueType,
                property.Source,
                property.Priority,
                property.Diagnostic,
                ExtractResourceKeyHint(property.Diagnostic),
                styleSelector: property.Source == "style" ? "not_available" : null,
                templateOrigin: property.Source == "template" ? "template_or_template_binding" : null,
                filePath,
                line))
            .ToArray();
    }

    private static string? ExtractResourceKeyHint(string? diagnostic)
    {
        if (string.IsNullOrWhiteSpace(diagnostic))
        {
            return null;
        }

        var match = Regex.Match(
            diagnostic,
            @"(?:StaticResource|DynamicResource|resource)\s+['""]?(?<key>[\w.-]+)",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        return match.Success ? match.Groups["key"].Value : null;
    }

    private RuntimeLayoutExplanation CreateLayoutExplanation(
        TopLevel topLevel,
        string topLevelId,
        object node,
        RuntimeTargetContext target)
    {
        if (node is not Layoutable layoutable)
        {
            return new RuntimeLayoutExplanation(
                "not_available",
                "Layout explain requires an Avalonia Layoutable target.",
                new RuntimeLayoutMetrics(
                    "not_available",
                    target.NodeId,
                    node.GetType().FullName ?? node.GetType().Name,
                    GetBounds(node),
                    target: target),
                "not_available",
                reasons:
                [
                    new RuntimeLayoutReason(
                        "layoutable_required",
                        "The selected node does not expose DesiredSize, measure validity, or arrange validity through Avalonia Layoutable.",
                        "warning")
                ]);
        }

        var nodeMetrics = CreateLayoutMetrics(topLevelId, topLevel, layoutable, target);
        var ancestors = CreateLayoutAncestors(topLevel, topLevelId, layoutable);
        var reasons = CreateLayoutReasons(topLevel, layoutable, ancestors);
        var parentConstraint = ancestors.FirstOrDefault()?.Bounds is { } parentBounds
            ? new RuntimeSize(parentBounds.Width, parentBounds.Height)
            : null;
        var status = reasons.Any(static reason => reason.Severity is "warning" or "error")
            ? "issues_found"
            : "available";
        var summary = status == "issues_found"
            ? reasons.First(static reason => reason.Severity is "warning" or "error").Message
            : "The node is arranged within its current parent bounds.";

        return new RuntimeLayoutExplanation(
            status,
            summary,
            nodeMetrics,
            "inferred_from_parent_bounds",
            parentConstraint,
            ancestors,
            reasons);
    }

    private RuntimeLayoutMetrics CreateLayoutMetrics(
        string topLevelId,
        TopLevel topLevel,
        Layoutable layoutable,
        RuntimeTargetContext? target = null)
    {
        return new RuntimeLayoutMetrics(
            "available",
            target?.NodeId ?? CreateNodeId(layoutable, TreeKinds.Visual),
            layoutable.GetType().FullName ?? layoutable.GetType().Name,
            GetBounds(layoutable),
            new RuntimeSize(layoutable.DesiredSize.Width, layoutable.DesiredSize.Height),
            new RuntimeSize(layoutable.Bounds.Width, layoutable.Bounds.Height),
            target ?? CreateNodeTarget(topLevelId, TreeKinds.Visual, topLevel, layoutable));
    }

    private IReadOnlyList<RuntimeLayoutAncestor> CreateLayoutAncestors(
        TopLevel topLevel,
        string topLevelId,
        Layoutable layoutable)
    {
        if (layoutable is not Visual visual)
        {
            return Array.Empty<RuntimeLayoutAncestor>();
        }

        return visual.GetVisualAncestors()
            .Take(MaximumRuntimeLayoutAncestors)
            .Select(ancestor => CreateLayoutAncestor(topLevel, topLevelId, ancestor))
            .ToArray();
    }

    private RuntimeLayoutAncestor CreateLayoutAncestor(TopLevel topLevel, string topLevelId, Visual ancestor)
    {
        var layoutable = ancestor as Layoutable;
        var control = ancestor as Control;
        var grid = ancestor as Grid;
        return new RuntimeLayoutAncestor(
            CreateNodeId(ancestor, TreeKinds.Visual),
            ancestor.GetType().FullName ?? ancestor.GetType().Name,
            GetName(ancestor),
            GetBounds(ancestor),
            layoutable is null ? null : new RuntimeSize(layoutable.DesiredSize.Width, layoutable.DesiredSize.Height),
            layoutable is null ? null : new RuntimeSize(layoutable.Bounds.Width, layoutable.Bounds.Height),
            ancestor.ClipToBounds,
            ancestor is Panel ? ancestor.GetType().Name : null,
            control is null ? null : Grid.GetRow(control).ToString(CultureInfo.InvariantCulture),
            control is null ? null : Grid.GetColumn(control).ToString(CultureInfo.InvariantCulture),
            control is null ? null : Grid.GetRowSpan(control).ToString(CultureInfo.InvariantCulture),
            control is null ? null : Grid.GetColumnSpan(control).ToString(CultureInfo.InvariantCulture),
            grid is null ? null : FormatGridDefinitionActuals(grid.RowDefinitions, "ActualHeight"),
            grid is null ? null : FormatGridDefinitionActuals(grid.ColumnDefinitions, "ActualWidth"),
            GetScrollState(topLevel, topLevelId, ancestor));
    }

    private static IReadOnlyList<RuntimeLayoutReason> CreateLayoutReasons(
        TopLevel topLevel,
        Layoutable layoutable,
        IReadOnlyList<RuntimeLayoutAncestor> ancestors)
    {
        var reasons = new List<RuntimeLayoutReason>();
        var nodeId = CreateNodeId(layoutable, TreeKinds.Visual);
        var nodeType = layoutable.GetType().FullName ?? layoutable.GetType().Name;
        var bounds = layoutable.Bounds;
        var desired = layoutable.DesiredSize;

        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            reasons.Add(new RuntimeLayoutReason(
                "arranged_zero_size",
                "The node was arranged to zero width or height.",
                "warning",
                nodeId,
                nodeType,
                new Dictionary<string, string>
                {
                    ["boundsWidth"] = bounds.Width.ToString(CultureInfo.InvariantCulture),
                    ["boundsHeight"] = bounds.Height.ToString(CultureInfo.InvariantCulture),
                    ["desiredWidth"] = desired.Width.ToString(CultureInfo.InvariantCulture),
                    ["desiredHeight"] = desired.Height.ToString(CultureInfo.InvariantCulture),
                    ["isMeasureValid"] = layoutable.IsMeasureValid.ToString(CultureInfo.InvariantCulture),
                    ["isArrangeValid"] = layoutable.IsArrangeValid.ToString(CultureInfo.InvariantCulture)
                }));
        }

        if (desired.Width > bounds.Width + 0.5 || desired.Height > bounds.Height + 0.5)
        {
            reasons.Add(new RuntimeLayoutReason(
                "desired_size_exceeds_bounds",
                "The node desired more space than it received during arrange.",
                "warning",
                nodeId,
                nodeType,
                new Dictionary<string, string>
                {
                    ["desiredWidth"] = desired.Width.ToString(CultureInfo.InvariantCulture),
                    ["desiredHeight"] = desired.Height.ToString(CultureInfo.InvariantCulture),
                    ["boundsWidth"] = bounds.Width.ToString(CultureInfo.InvariantCulture),
                    ["boundsHeight"] = bounds.Height.ToString(CultureInfo.InvariantCulture)
                }));
        }

        if (layoutable is Visual visual)
        {
            AddClipReasons(topLevel, visual, reasons);
            AddGridReasons(visual, reasons);
            AddScrollViewerReasons(topLevel, visual, ancestors, reasons);
        }

        if (ancestors.FirstOrDefault() is { Bounds: { Width: <= 0 } or { Height: <= 0 } } parent)
        {
            reasons.Add(new RuntimeLayoutReason(
                "parent_arranged_zero_size",
                "The immediate visual parent has zero arranged size.",
                "warning",
                parent.NodeId,
                parent.NodeType));
        }

        if (reasons.Count == 0)
        {
            reasons.Add(new RuntimeLayoutReason(
                "layout_within_bounds",
                "No zero-size, clipping, Grid, or ScrollViewer constraint issue was detected from public runtime layout state.",
                "info",
                nodeId,
                nodeType));
        }

        return reasons;
    }

    private static void AddClipReasons(TopLevel topLevel, Visual visual, ICollection<RuntimeLayoutReason> reasons)
    {
        var nodeBounds = GetGlobalBounds(visual, topLevel);
        if (nodeBounds is null)
        {
            return;
        }

        foreach (var ancestor in visual.GetVisualAncestors())
        {
            if (!ancestor.ClipToBounds)
            {
                continue;
            }

            var ancestorBounds = GetGlobalBounds(ancestor, topLevel);
            if (ancestorBounds is null || Contains(ancestorBounds.Value, nodeBounds.Value))
            {
                continue;
            }

            reasons.Add(new RuntimeLayoutReason(
                "clipped_by_ancestor",
                "A clipping ancestor does not fully contain the node bounds.",
                "warning",
                CreateNodeId(ancestor, TreeKinds.Visual),
                ancestor.GetType().FullName ?? ancestor.GetType().Name,
                new Dictionary<string, string>
                {
                    ["clippingAncestorClipToBounds"] = "true",
                    ["nodeGlobalBounds"] = FormatRect(nodeBounds.Value),
                    ["ancestorGlobalBounds"] = FormatRect(ancestorBounds.Value)
                }));
            return;
        }
    }

    private static void AddGridReasons(Visual visual, ICollection<RuntimeLayoutReason> reasons)
    {
        if (visual.GetVisualParent() is not Grid grid || visual is not Control control)
        {
            return;
        }

        reasons.Add(new RuntimeLayoutReason(
            "grid_cell_constraint",
            "The immediate parent Grid controls this node through row and column sizing.",
            "info",
            CreateNodeId(grid, TreeKinds.Visual),
            grid.GetType().FullName ?? grid.GetType().Name,
            new Dictionary<string, string>
            {
                ["row"] = Grid.GetRow(control).ToString(CultureInfo.InvariantCulture),
                ["column"] = Grid.GetColumn(control).ToString(CultureInfo.InvariantCulture),
                ["rowSpan"] = Grid.GetRowSpan(control).ToString(CultureInfo.InvariantCulture),
                ["columnSpan"] = Grid.GetColumnSpan(control).ToString(CultureInfo.InvariantCulture),
                ["rowHeights"] = FormatGridDefinitionActuals(grid.RowDefinitions, "ActualHeight"),
                ["columnWidths"] = FormatGridDefinitionActuals(grid.ColumnDefinitions, "ActualWidth")
            }));
    }

    private static void AddScrollViewerReasons(
        TopLevel topLevel,
        Visual visual,
        IReadOnlyList<RuntimeLayoutAncestor> ancestors,
        ICollection<RuntimeLayoutReason> reasons)
    {
        var scrollAncestor = visual.GetVisualAncestors().OfType<ScrollViewer>().FirstOrDefault();
        if (scrollAncestor is null)
        {
            return;
        }

        var viewport = scrollAncestor.Viewport;
        var extent = scrollAncestor.Extent;
        var nodeBounds = GetGlobalBounds(visual, topLevel);
        var scrollBounds = GetGlobalBounds(scrollAncestor, topLevel);
        var outsideViewport = nodeBounds is not null
            && scrollBounds is not null
            && !Contains(scrollBounds.Value, nodeBounds.Value);

        if (extent.Width <= viewport.Width && extent.Height <= viewport.Height && !outsideViewport)
        {
            return;
        }

        reasons.Add(new RuntimeLayoutReason(
            "scrollviewer_viewport_constraint",
            outsideViewport
                ? "The node is outside or partially outside a ScrollViewer viewport."
                : "A ScrollViewer ancestor constrains content to a smaller viewport than its extent.",
            outsideViewport ? "warning" : "info",
            CreateNodeId(scrollAncestor, TreeKinds.Visual),
            scrollAncestor.GetType().FullName ?? scrollAncestor.GetType().Name,
            new Dictionary<string, string>
            {
                ["offset"] = $"{scrollAncestor.Offset.X.ToString(CultureInfo.InvariantCulture)},{scrollAncestor.Offset.Y.ToString(CultureInfo.InvariantCulture)}",
                ["extent"] = $"{extent.Width.ToString(CultureInfo.InvariantCulture)}x{extent.Height.ToString(CultureInfo.InvariantCulture)}",
                ["viewport"] = $"{viewport.Width.ToString(CultureInfo.InvariantCulture)}x{viewport.Height.ToString(CultureInfo.InvariantCulture)}",
                ["ancestorChainContainsScrollViewer"] = ancestors.Any(static ancestor => ancestor.ScrollState is not null).ToString(CultureInfo.InvariantCulture)
            }));
    }

    private static Rect? GetGlobalBounds(Visual visual, Visual root)
    {
        var point = visual.TranslatePoint(new Point(0, 0), root);
        return point is null ? null : new Rect(point.Value, visual.Bounds.Size);
    }

    private static bool Contains(Rect outer, Rect inner)
    {
        return inner.Left >= outer.Left
            && inner.Top >= outer.Top
            && inner.Right <= outer.Right
            && inner.Bottom <= outer.Bottom;
    }

    private static string FormatRect(Rect rect)
    {
        return string.Create(
            CultureInfo.InvariantCulture,
            $"{rect.X:0.###},{rect.Y:0.###},{rect.Width:0.###},{rect.Height:0.###}");
    }

    private static string FormatGridDefinitionActuals<T>(IEnumerable<T> definitions, string propertyName)
    {
        return string.Join(
            ";",
            definitions.Select((definition, index) =>
                $"{index.ToString(CultureInfo.InvariantCulture)}:{GetReflectedDouble(definition, propertyName)}"));
    }

    private static string GetReflectedDouble(object? instance, string propertyName)
    {
        if (instance is null)
        {
            return "not_available";
        }

        var property = instance.GetType().GetProperty(propertyName);
        if (property?.GetValue(instance) is IFormattable formattable)
        {
            return formattable.ToString("0.###", CultureInfo.InvariantCulture);
        }

        return "not_available";
    }

    private static RuntimeDebugState GetDebugState(object node)
    {
        if (node is not IAvaScopeDebugStateProvider provider)
        {
            return new RuntimeDebugState(
                "not_enabled",
                fieldCount: 0,
                maximumFieldCount: MaximumDebugFieldCount,
                maximumValueLength: MaximumDebugValueLength);
        }

        try
        {
            var fields = new Dictionary<string, string>(StringComparer.Ordinal);
            var truncated = false;
            foreach (var field in provider.GetAvaScopeDebugState())
            {
                if (fields.Count >= MaximumDebugFieldCount)
                {
                    truncated = true;
                    break;
                }

                var key = string.IsNullOrWhiteSpace(field.Key)
                    ? $"field_{fields.Count + 1}"
                    : field.Key.Trim();
                var value = field.Value ?? "null";
                if (value.Length > MaximumDebugValueLength)
                {
                    value = value[..MaximumDebugValueLength];
                    truncated = true;
                }

                fields[key] = value;
            }

            return new RuntimeDebugState(
                "available",
                fields,
                node.GetType().FullName ?? node.GetType().Name,
                truncated,
                fields.Count,
                MaximumDebugFieldCount,
                MaximumDebugValueLength);
        }
        catch (Exception exception) when (exception is InvalidOperationException or NotSupportedException or ArgumentException)
        {
            return new RuntimeDebugState(
                "error",
                new Dictionary<string, string>
                {
                    ["error"] = exception.Message
                },
                node.GetType().FullName ?? node.GetType().Name,
                fieldCount: 1,
                maximumFieldCount: MaximumDebugFieldCount,
                maximumValueLength: MaximumDebugValueLength);
        }
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
            yield return Border.BorderThicknessProperty;
            yield return Border.CornerRadiusProperty;
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

    private static string FormatValidationError(object? value)
    {
        return value switch
        {
            null => "null",
            Exception exception => exception.Message,
            _ => FormatComputedValue(value)
        };
    }

    private static string? FirstNonEmpty(params string?[] values)
    {
        return values.FirstOrDefault(static value => !string.IsNullOrWhiteSpace(value))?.Trim();
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

    private sealed record RuntimeMutationIdentity(string Id, long Sequence);

    private sealed record ConvertedMutationValue(object? Value);

    private sealed record MutableAvaloniaProperty(
        AvaloniaObject Owner,
        AvaloniaProperty Property,
        string PropertyName,
        string ValueKind);

    private sealed record PropertyValueSnapshot(
        object? Value,
        string ValueType,
        string ValueText,
        BindingPriority Priority,
        string Source,
        bool HasLocalValue);

    private sealed record AppliedRuntimeMutation(
        string MutationId,
        long Sequence,
        string RequestId,
        RuntimeMutationOperation Operation,
        string OperationKind,
        string TopLevelId,
        string? TreeKind,
        string? NodeId,
        string NodeType,
        DateTimeOffset AppliedAt,
        IReadOnlyDictionary<string, string> Metadata,
        Action Reset);

    private sealed record ResolvedMutationTarget(object Node, RuntimeTargetContext Target);

    private sealed record SourceElementInfo(
        string ElementType,
        string ElementPath,
        string? XName,
        string Snippet,
        IReadOnlyList<RuntimeSourceBinding> Bindings);

    private sealed class TopLevelRegistration(Action unregister) : IDisposable
    {
        private int _disposed;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
            {
                unregister();
            }
        }
    }
}
