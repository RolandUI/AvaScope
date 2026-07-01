using System.Globalization;
using AvaScope.Protocol;
using SkiaSharp;

namespace AvaScope.Core;

public sealed class RuntimePointerDiagnosticsRunner
{
    private const string Passed = "passed";
    private const string Failed = "failed";
    private const int MaximumTopLevelLayers = 8;
    private const string TransitionProvenance = "bounds_snapshot_inference";

    public async Task<CoreResult<RuntimePointerDiagnosticsResponse>> RunAsync(
        LocalBridgeClient bridgeClient,
        RuntimePointerDiagnosticsRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(bridgeClient);
        ArgumentNullException.ThrowIfNull(request);

        var startedAt = DateTimeOffset.UtcNow;
        var results = new List<RuntimePointerStepResult>();
        var diagnostics = new List<ProtocolError>();
        RuntimePointerLocation? pointer = null;
        RuntimePointerLayerSnapshot? previousLayer = null;

        if (!string.IsNullOrWhiteSpace(request.OutputDirectory))
        {
            Directory.CreateDirectory(request.OutputDirectory);
        }

        for (var index = 0; index < request.Steps.Count; index++)
        {
            var result = await ExecuteStepAsync(
                bridgeClient,
                request,
                request.Steps[index],
                index,
                pointer,
                previousLayer,
                cancellationToken);
            results.Add(result.Step);
            diagnostics.AddRange(result.Step.Diagnostics);
            diagnostics.AddRange(result.Step.Transitions.Select(ToProtocolError));

            pointer = result.Pointer ?? pointer;
            previousLayer = result.Step.ActiveLayer ?? previousLayer;

            if (result.Step.Status == Failed)
            {
                break;
            }
        }

        var status = results.All(static step => step.Status == Passed) ? Passed : Failed;
        return CoreResult<RuntimePointerDiagnosticsResponse>.Ok(new RuntimePointerDiagnosticsResponse(
            request.RequestId,
            request.SessionId,
            request.TopLevelId,
            status,
            startedAt,
            DateTimeOffset.UtcNow,
            results,
            diagnostics,
            new Dictionary<string, string>
            {
                ["requestedSteps"] = request.Steps.Count.ToString(CultureInfo.InvariantCulture),
                ["executedSteps"] = results.Count.ToString(CultureInfo.InvariantCulture),
                ["transitionProvenance"] = TransitionProvenance,
                ["includeAllTopLevels"] = request.IncludeAllTopLevels.ToString(CultureInfo.InvariantCulture),
                ["captureScreenshots"] = request.CaptureScreenshots.ToString(CultureInfo.InvariantCulture)
            }));
    }

    private static async Task<StepExecution> ExecuteStepAsync(
        LocalBridgeClient bridgeClient,
        RuntimePointerDiagnosticsRequest request,
        RuntimePointerPathStep step,
        int stepIndex,
        RuntimePointerLocation? previousPointer,
        RuntimePointerLayerSnapshot? previousLayer,
        CancellationToken cancellationToken)
    {
        var diagnostics = new List<ProtocolError>();
        InputResponse? input = null;
        ScreenshotResponse? screenshot = null;
        string? overlayPath = null;
        RuntimePointerLocation? pointer = previousPointer;

        try
        {
            switch (step.Action)
            {
                case RuntimePointerPathActions.Move:
                    if (step.X is null || step.Y is null)
                    {
                        return Fail(step, "runtime_pointer_move_coordinates_required", "Pointer move steps require x and y coordinates.", pointer);
                    }

                    pointer = new RuntimePointerLocation(step.X.Value, step.Y.Value);
                    var inputResult = await bridgeClient.InputAsync(
                        request.SessionId,
                        request.TopLevelId,
                        InputActions.PointerMove,
                        x: pointer.X,
                        y: pointer.Y,
                        cancellationToken: cancellationToken);
                    if (!inputResult.Success)
                    {
                        return Fail(step, inputResult.Error!, pointer);
                    }

                    input = inputResult.Value;
                    break;
                case RuntimePointerPathActions.Wait:
                    var waitMs = step.WaitMs ?? 0;
                    if (waitMs > 0)
                    {
                        await Task.Delay(waitMs, cancellationToken);
                    }

                    break;
                case RuntimePointerPathActions.Screenshot:
                case RuntimePointerPathActions.AssertHit:
                    break;
                default:
                    return Fail(
                        step,
                        "runtime_pointer_action_not_supported",
                        $"Pointer diagnostics action '{step.Action}' is not supported.",
                        pointer);
            }

            var activeLayer = pointer is null
                ? null
                : await CaptureActiveLayerAsync(bridgeClient, request, pointer, diagnostics, cancellationToken);
            var transitions = CreateTransitions(request, previousLayer, activeLayer);
            var status = Passed;
            var message = CreatePassMessage(step, activeLayer, transitions);

            if (step.Action == RuntimePointerPathActions.AssertHit)
            {
                var assertion = EvaluateHitAssertion(step, activeLayer);
                if (assertion is not null)
                {
                    diagnostics.Add(assertion);
                    status = Failed;
                    message = assertion.Message;
                }
            }

            if (step.Action == RuntimePointerPathActions.Screenshot || request.CaptureScreenshots)
            {
                var screenshotPath = ResolveScreenshotPath(request, step, stepIndex);
                if (string.IsNullOrWhiteSpace(screenshotPath))
                {
                    diagnostics.Add(new ProtocolError(
                        "runtime_pointer_screenshot_path_required",
                        "Pointer diagnostics screenshot capture requires step screenshotPath or request outputDirectory."));
                    status = Failed;
                    message = diagnostics[^1].Message;
                }
                else
                {
                    var screenshotTopLevelId = activeLayer?.TopLevelId ?? request.TopLevelId;
                    var screenshotResult = await bridgeClient.CaptureScreenshotAsync(
                        request.SessionId,
                        screenshotTopLevelId,
                        screenshotPath,
                        cancellationToken);
                    if (!screenshotResult.Success)
                    {
                        diagnostics.Add(ToProtocolError(screenshotResult.Error!));
                        status = Failed;
                        message = screenshotResult.Error!.Message;
                    }
                    else
                    {
                        screenshot = screenshotResult.Value;
                        overlayPath = TryCreatePointerOverlay(screenshot, pointer, activeLayer, diagnostics);
                    }
                }
            }

            return new StepExecution(
                pointer,
                new RuntimePointerStepResult(
                    step.Id,
                    step.Action,
                    status,
                    message,
                    DateTimeOffset.UtcNow,
                    pointer,
                    input,
                    screenshot,
                    overlayPath,
                    activeLayer,
                    transitions,
                    diagnostics,
                    CreateStepMetadata(step, activeLayer)));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException)
        {
            return Fail(step, "runtime_pointer_step_failed", exception.Message, pointer);
        }
    }

    private static async Task<RuntimePointerLayerSnapshot?> CaptureActiveLayerAsync(
        LocalBridgeClient bridgeClient,
        RuntimePointerDiagnosticsRequest request,
        RuntimePointerLocation pointer,
        List<ProtocolError> diagnostics,
        CancellationToken cancellationToken)
    {
        var topLevels = await ResolveTopLevelsAsync(bridgeClient, request, diagnostics, cancellationToken);
        var layers = new List<RuntimePointerLayerSnapshot>();

        foreach (var topLevel in topLevels.Take(MaximumTopLevelLayers))
        {
            var tree = await bridgeClient.VisualTreeAsync(
                request.SessionId,
                topLevel.Id,
                request.MaxDepth,
                cancellationToken);
            if (!tree.Success)
            {
                diagnostics.Add(ToProtocolError(tree.Error!));
                continue;
            }

            layers.Add(CreateLayerSnapshot(request, topLevel, tree.Value!.Root, pointer));
        }

        return layers
            .OrderByDescending(static layer => layer.HitTestPath.Count > 0)
            .ThenByDescending(static layer => LayerPriority(layer.LayerKind))
            .ThenByDescending(static layer => layer.HitTestPath.Count)
            .ThenBy(static layer => layer.NearestNode?.Distance ?? double.MaxValue)
            .FirstOrDefault();
    }

    private static async Task<IReadOnlyList<TopLevelSummary>> ResolveTopLevelsAsync(
        LocalBridgeClient bridgeClient,
        RuntimePointerDiagnosticsRequest request,
        List<ProtocolError> diagnostics,
        CancellationToken cancellationToken)
    {
        if (!request.IncludeAllTopLevels)
        {
            return [new TopLevelSummary(request.TopLevelId, "primary", null, 0, 0, 1, isActive: true)];
        }

        var topLevels = await bridgeClient.ListTopLevelsAsync(request.SessionId, cancellationToken);
        if (!topLevels.Success)
        {
            diagnostics.Add(ToProtocolError(topLevels.Error!));
            return [new TopLevelSummary(request.TopLevelId, "primary", null, 0, 0, 1, isActive: true)];
        }

        if (topLevels.Value!.TopLevels.Any(topLevel => string.Equals(topLevel.Id, request.TopLevelId, StringComparison.Ordinal)))
        {
            return topLevels.Value.TopLevels;
        }

        return topLevels.Value.TopLevels
            .Prepend(new TopLevelSummary(request.TopLevelId, "primary", null, 0, 0, 1, isActive: true))
            .ToArray();
    }

    private static RuntimePointerLayerSnapshot CreateLayerSnapshot(
        RuntimePointerDiagnosticsRequest request,
        TopLevelSummary topLevel,
        TreeNodeSummary root,
        RuntimePointerLocation pointer)
    {
        var hitPath = CreateHitPath(root, pointer).ToArray();
        var nearest = FindNearestNode(root, pointer);
        var layerKind = InferLayerKind(topLevel, root, hitPath);
        return new RuntimePointerLayerSnapshot(
            topLevel.Id,
            topLevel.Kind,
            layerKind,
            string.Equals(topLevel.Id, request.TopLevelId, StringComparison.Ordinal),
            hitPath,
            nearest);
    }

    private static IReadOnlyList<RuntimePointerHitNode> CreateHitPath(TreeNodeSummary root, RuntimePointerLocation pointer)
    {
        var path = new List<RuntimePointerHitNode>();
        AddHitPath(root, pointer, path);
        return path;
    }

    private static bool AddHitPath(TreeNodeSummary node, RuntimePointerLocation pointer, List<RuntimePointerHitNode> path)
    {
        if (!Contains(node.Bounds, pointer))
        {
            return false;
        }

        path.Add(ToHitNode(node, pointer, contains: true));
        foreach (var child in node.Children
            .Where(child => Contains(child.Bounds, pointer))
            .OrderBy(static child => Area(child.Bounds)))
        {
            AddHitPath(child, pointer, path);
            break;
        }

        return true;
    }

    private static RuntimePointerHitNode? FindNearestNode(TreeNodeSummary root, RuntimePointerLocation pointer)
    {
        return EnumerateNodes(root)
            .Where(static node => node.Bounds is not null)
            .Select(node => ToHitNode(node, pointer, Contains(node.Bounds, pointer)))
            .OrderBy(static node => node.Distance)
            .ThenBy(static node => Area(node.Bounds))
            .FirstOrDefault();
    }

    private static IEnumerable<TreeNodeSummary> EnumerateNodes(TreeNodeSummary node)
    {
        yield return node;
        foreach (var child in node.Children)
        {
            foreach (var descendant in EnumerateNodes(child))
            {
                yield return descendant;
            }
        }
    }

    private static RuntimePointerHitNode ToHitNode(TreeNodeSummary node, RuntimePointerLocation pointer, bool contains)
    {
        return new RuntimePointerHitNode(
            node.NodeId,
            node.NodeType,
            node.Name,
            node.AutomationId,
            node.Text,
            node.Bounds,
            contains,
            Distance(node.Bounds, pointer));
    }

    private static IReadOnlyList<RuntimePointerTransitionDiagnostic> CreateTransitions(
        RuntimePointerDiagnosticsRequest request,
        RuntimePointerLayerSnapshot? previous,
        RuntimePointerLayerSnapshot? current)
    {
        var transitions = new List<RuntimePointerTransitionDiagnostic>();
        var previousLeaf = previous?.HitTestPath.LastOrDefault();
        var currentLeaf = current?.HitTestPath.LastOrDefault();
        if (previous is not null
            && current is not null
            && previousLeaf is not null
            && currentLeaf is not null
            && (!string.Equals(previousLeaf.NodeId, currentLeaf.NodeId, StringComparison.Ordinal)
                || !string.Equals(previous.TopLevelId, current.TopLevelId, StringComparison.Ordinal)))
        {
            transitions.Add(new RuntimePointerTransitionDiagnostic(
                "info",
                "pointer_hit_path_changed",
                "Pointer hit path changed between diagnostics steps.",
                TransitionProvenance,
                previous.TopLevelId,
                previousLeaf.NodeId,
                current.TopLevelId,
                currentLeaf.NodeId,
                metadata: CreateTransitionMetadata(previous, current)));
        }

        if (!string.IsNullOrWhiteSpace(request.ParentHoverNodeId)
            && previous?.HitTestPath.Any(node => string.Equals(node.NodeId, request.ParentHoverNodeId, StringComparison.Ordinal)) == true
            && current?.HitTestPath.Any(node => string.Equals(node.NodeId, request.ParentHoverNodeId, StringComparison.Ordinal)) != true)
        {
            var popupLike = current is not null && IsPopupLike(current.LayerKind);
            transitions.Add(new RuntimePointerTransitionDiagnostic(
                "warning",
                popupLike ? "pointer_parent_hover_exited_into_popup_layer" : "pointer_parent_hover_region_exited",
                popupLike
                    ? "Pointer moved into a popup-like layer outside the parent hover region; parent PointerExited behavior may run."
                    : "Pointer moved outside the configured parent hover region; parent PointerExited behavior may run.",
                TransitionProvenance,
                previous?.TopLevelId,
                request.ParentHoverNodeId,
                current?.TopLevelId,
                currentLeaf?.NodeId,
                parentHoverRegionExited: true,
                metadata: CreateTransitionMetadata(previous, current)));
        }

        return transitions;
    }

    private static ProtocolError? EvaluateHitAssertion(RuntimePointerPathStep step, RuntimePointerLayerSnapshot? activeLayer)
    {
        if (!string.IsNullOrWhiteSpace(step.ExpectedNodeId)
            && activeLayer?.HitTestPath.Any(node => string.Equals(node.NodeId, step.ExpectedNodeId, StringComparison.Ordinal)) != true)
        {
            return new ProtocolError(
                "runtime_pointer_expected_node_not_hit",
                $"Expected pointer hit path to include node '{step.ExpectedNodeId}'.",
                new Dictionary<string, string>
                {
                    ["expectedNodeId"] = step.ExpectedNodeId,
                    ["actualNodeId"] = activeLayer?.HitTestPath.LastOrDefault()?.NodeId ?? "not_available"
                });
        }

        if (!string.IsNullOrWhiteSpace(step.ExpectedLayerKind)
            && !string.Equals(activeLayer?.LayerKind, step.ExpectedLayerKind, StringComparison.OrdinalIgnoreCase))
        {
            return new ProtocolError(
                "runtime_pointer_expected_layer_not_active",
                $"Expected active pointer layer kind '{step.ExpectedLayerKind}'.",
                new Dictionary<string, string>
                {
                    ["expectedLayerKind"] = step.ExpectedLayerKind,
                    ["actualLayerKind"] = activeLayer?.LayerKind ?? "not_available"
                });
        }

        return null;
    }

    private static string? TryCreatePointerOverlay(
        ScreenshotResponse? screenshot,
        RuntimePointerLocation? pointer,
        RuntimePointerLayerSnapshot? activeLayer,
        List<ProtocolError> diagnostics)
    {
        if (screenshot is null || pointer is null || !File.Exists(screenshot.FilePath))
        {
            return null;
        }

        var overlayPath = Path.Combine(
            Path.GetDirectoryName(screenshot.FilePath) ?? string.Empty,
            $"{Path.GetFileNameWithoutExtension(screenshot.FilePath)}-pointer-overlay.png");

        try
        {
            using var bitmap = SKBitmap.Decode(screenshot.FilePath);
            if (bitmap is null)
            {
                return null;
            }

            using var canvas = new SKCanvas(bitmap);
            using var markerPaint = new SKPaint
            {
                Color = SKColors.DeepSkyBlue,
                IsAntialias = true,
                StrokeWidth = 2,
                Style = SKPaintStyle.Stroke
            };
            using var fillPaint = new SKPaint
            {
                Color = new SKColor(0, 191, 255, 64),
                IsAntialias = true,
                Style = SKPaintStyle.Fill
            };
            using var font = new SKFont
            {
                Size = 14
            };
            using var textPaint = new SKPaint
            {
                Color = SKColors.DeepSkyBlue,
                IsAntialias = true
            };

            var x = (float)Math.Clamp(pointer.X, 0, Math.Max(0, bitmap.Width - 1));
            var y = (float)Math.Clamp(pointer.Y, 0, Math.Max(0, bitmap.Height - 1));
            canvas.DrawCircle(x, y, 8, fillPaint);
            canvas.DrawCircle(x, y, 8, markerPaint);
            canvas.DrawLine(x - 14, y, x + 14, y, markerPaint);
            canvas.DrawLine(x, y - 14, x, y + 14, markerPaint);
            canvas.DrawText(
                activeLayer?.LayerKind ?? "pointer",
                Math.Min(x + 12, bitmap.Width - 80),
                Math.Max(16, y - 12),
                SKTextAlign.Left,
                font,
                textPaint);

            using var image = SKImage.FromBitmap(bitmap);
            using var data = image.Encode(SKEncodedImageFormat.Png, 100);
            using var stream = File.Create(overlayPath);
            data.SaveTo(stream);
            return overlayPath;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            diagnostics.Add(new ProtocolError(
                "runtime_pointer_overlay_unavailable",
                $"Pointer overlay image could not be written: {exception.Message}",
                new Dictionary<string, string> { ["screenshotPath"] = screenshot.FilePath }));
            return null;
        }
    }

    private static string? ResolveScreenshotPath(RuntimePointerDiagnosticsRequest request, RuntimePointerPathStep step, int stepIndex)
    {
        if (!string.IsNullOrWhiteSpace(step.ScreenshotPath))
        {
            return step.ScreenshotPath;
        }

        if (string.IsNullOrWhiteSpace(request.OutputDirectory))
        {
            return null;
        }

        var safeId = string.Join("-", step.Id.Split(Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries));
        if (string.IsNullOrWhiteSpace(safeId))
        {
            safeId = step.Action;
        }

        return Path.Combine(
            request.OutputDirectory,
            $"{(stepIndex + 1).ToString("00", CultureInfo.InvariantCulture)}-{safeId}.png");
    }

    private static IReadOnlyDictionary<string, string> CreateStepMetadata(
        RuntimePointerPathStep step,
        RuntimePointerLayerSnapshot? activeLayer)
    {
        var metadata = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["transitionProvenance"] = TransitionProvenance
        };

        if (step.X is not null && step.Y is not null)
        {
            metadata["x"] = step.X.Value.ToString("0.###", CultureInfo.InvariantCulture);
            metadata["y"] = step.Y.Value.ToString("0.###", CultureInfo.InvariantCulture);
        }

        if (activeLayer is not null)
        {
            metadata["activeTopLevelId"] = activeLayer.TopLevelId;
            metadata["activeLayerKind"] = activeLayer.LayerKind;
            metadata["hitPathLength"] = activeLayer.HitTestPath.Count.ToString(CultureInfo.InvariantCulture);
        }

        return metadata;
    }

    private static IReadOnlyDictionary<string, string> CreateTransitionMetadata(
        RuntimePointerLayerSnapshot? previous,
        RuntimePointerLayerSnapshot? current)
    {
        return new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["fromLayerKind"] = previous?.LayerKind ?? "not_available",
            ["toLayerKind"] = current?.LayerKind ?? "not_available",
            ["fromHitPath"] = previous is null ? string.Empty : string.Join(",", previous.HitTestPath.Select(static node => node.NodeId)),
            ["toHitPath"] = current is null ? string.Empty : string.Join(",", current.HitTestPath.Select(static node => node.NodeId))
        };
    }

    private static string CreatePassMessage(
        RuntimePointerPathStep step,
        RuntimePointerLayerSnapshot? activeLayer,
        IReadOnlyList<RuntimePointerTransitionDiagnostic> transitions)
    {
        var leaf = activeLayer?.HitTestPath.LastOrDefault()?.NodeId ?? activeLayer?.NearestNode?.NodeId ?? "not_available";
        return $"{step.Action} completed; active layer '{activeLayer?.LayerKind ?? "not_available"}', hit node '{leaf}', transitions {transitions.Count.ToString(CultureInfo.InvariantCulture)}.";
    }

    private static string InferLayerKind(TopLevelSummary topLevel, TreeNodeSummary root, IReadOnlyList<RuntimePointerHitNode> hitPath)
    {
        var haystack = string.Join(
            " ",
            topLevel.Kind,
            topLevel.Title,
            root.NodeType,
            root.Name,
            string.Join(" ", hitPath.Select(static node => $"{node.NodeType} {node.Name} {node.AutomationId} {node.Text}")));

        if (haystack.Contains("ToolTip", StringComparison.OrdinalIgnoreCase))
        {
            return "tooltip";
        }

        if (haystack.Contains("Flyout", StringComparison.OrdinalIgnoreCase))
        {
            return "flyout";
        }

        if (haystack.Contains("ContextMenu", StringComparison.OrdinalIgnoreCase) || haystack.Contains("MenuFlyout", StringComparison.OrdinalIgnoreCase))
        {
            return "context_menu";
        }

        if (haystack.Contains("Popup", StringComparison.OrdinalIgnoreCase))
        {
            return "popup";
        }

        return string.Equals(topLevel.Kind, "window", StringComparison.OrdinalIgnoreCase)
            || string.Equals(topLevel.Kind, "singleView", StringComparison.OrdinalIgnoreCase)
            || string.Equals(topLevel.Kind, "primary", StringComparison.OrdinalIgnoreCase)
            ? "root"
            : "top_level";
    }

    private static bool IsPopupLike(string layerKind)
    {
        return layerKind is "popup" or "flyout" or "tooltip" or "context_menu" or "top_level";
    }

    private static int LayerPriority(string layerKind)
    {
        return layerKind switch
        {
            "tooltip" => 50,
            "flyout" => 40,
            "context_menu" => 35,
            "popup" => 30,
            "top_level" => 20,
            "root" => 10,
            _ => 0
        };
    }

    private static bool Contains(NodeBounds? bounds, RuntimePointerLocation pointer)
    {
        if (bounds is null)
        {
            return false;
        }

        var minX = Math.Min(bounds.X, bounds.X + bounds.Width);
        var maxX = Math.Max(bounds.X, bounds.X + bounds.Width);
        var minY = Math.Min(bounds.Y, bounds.Y + bounds.Height);
        var maxY = Math.Max(bounds.Y, bounds.Y + bounds.Height);
        return pointer.X >= minX && pointer.X <= maxX && pointer.Y >= minY && pointer.Y <= maxY;
    }

    private static double Distance(NodeBounds? bounds, RuntimePointerLocation pointer)
    {
        if (bounds is null)
        {
            return double.MaxValue;
        }

        var minX = Math.Min(bounds.X, bounds.X + bounds.Width);
        var maxX = Math.Max(bounds.X, bounds.X + bounds.Width);
        var minY = Math.Min(bounds.Y, bounds.Y + bounds.Height);
        var maxY = Math.Max(bounds.Y, bounds.Y + bounds.Height);
        var dx = pointer.X < minX ? minX - pointer.X : pointer.X > maxX ? pointer.X - maxX : 0;
        var dy = pointer.Y < minY ? minY - pointer.Y : pointer.Y > maxY ? pointer.Y - maxY : 0;
        return Math.Sqrt(dx * dx + dy * dy);
    }

    private static double Area(NodeBounds? bounds)
    {
        return bounds is null ? double.MaxValue : Math.Max(Math.Abs(bounds.Width), 1) * Math.Max(Math.Abs(bounds.Height), 1);
    }

    private static StepExecution Fail(RuntimePointerPathStep step, CoreError error, RuntimePointerLocation? pointer)
    {
        return Fail(step, error.Code, error.Message, pointer, error.Details);
    }

    private static StepExecution Fail(
        RuntimePointerPathStep step,
        string code,
        string message,
        RuntimePointerLocation? pointer,
        IReadOnlyDictionary<string, string>? details = null)
    {
        return new StepExecution(
            pointer,
            new RuntimePointerStepResult(
                step.Id,
                step.Action,
                Failed,
                message,
                DateTimeOffset.UtcNow,
                pointer,
                diagnostics: [new ProtocolError(code, message, details)]));
    }

    private static ProtocolError ToProtocolError(CoreError error)
    {
        return new ProtocolError(error.Code, error.Message, error.Details);
    }

    private static ProtocolError ToProtocolError(RuntimePointerTransitionDiagnostic diagnostic)
    {
        return new ProtocolError(diagnostic.Code, diagnostic.Message, diagnostic.Metadata);
    }

    private sealed record StepExecution(RuntimePointerLocation? Pointer, RuntimePointerStepResult Step);
}
