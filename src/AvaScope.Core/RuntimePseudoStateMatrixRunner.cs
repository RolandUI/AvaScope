using System.Globalization;
using AvaScope.Protocol;
using SkiaSharp;

namespace AvaScope.Core;

public sealed class RuntimePseudoStateMatrixRunner
{
    private const string Passed = "passed";
    private const string Failed = "failed";
    private const string Unsupported = "unsupported";
    private const int LabelHeight = 30;
    private const int CellPadding = 8;

    public async Task<CoreResult<RuntimePseudoStateMatrixResponse>> RunAsync(
        LocalBridgeClient bridgeClient,
        RuntimePseudoStateMatrixRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(bridgeClient);
        ArgumentNullException.ThrowIfNull(request);

        var startedAt = DateTimeOffset.UtcNow;
        var diagnostics = new List<ProtocolError>();
        var outputDirectory = ResolveOutputDirectory(request);

        try
        {
            Directory.CreateDirectory(outputDirectory);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            return CoreResult<RuntimePseudoStateMatrixResponse>.Fail(new CoreError(
                CoreErrorCodes.InvalidBridgeRequest,
                $"Pseudo-state matrix output directory could not be created: {exception.Message}",
                new Dictionary<string, string> { ["outputDirectory"] = outputDirectory }));
        }

        var targetResult = await ResolveTargetAsync(bridgeClient, request, diagnostics, cancellationToken);
        if (!targetResult.Success)
        {
            return CoreResult<RuntimePseudoStateMatrixResponse>.Fail(targetResult.Error!);
        }

        var target = targetResult.Value!;
        var entries = new List<RuntimePseudoStateMatrixEntry>();
        RuntimePseudoStateMatrixEntry? baselineEntry = null;

        foreach (var state in request.States)
        {
            var entry = await CaptureStateAsync(
                bridgeClient,
                request,
                outputDirectory,
                target,
                state,
                entries.Count,
                baselineEntry,
                cancellationToken);
            entries.Add(entry);
            diagnostics.AddRange(entry.Diagnostics);

            if (baselineEntry is null
                && entry.Screenshot is not null
                && string.Equals(state, RuntimePseudoStates.Normal, StringComparison.Ordinal))
            {
                baselineEntry = entry;
            }
        }

        var contactSheetPath = request.ContactSheetPath
            ?? Path.Combine(outputDirectory, $"{SanitizeFileToken(request.RequestId)}-contact-sheet.png");
        var sheet = TryCreateContactSheet(entries, contactSheetPath);
        if (!sheet.Success)
        {
            diagnostics.Add(new ProtocolError(sheet.Error!.Code, sheet.Error.Message, sheet.Error.Details));
            contactSheetPath = null;
        }

        var status = entries.Any(static entry => entry.Status == Failed) ? Failed : Passed;
        return CoreResult<RuntimePseudoStateMatrixResponse>.Ok(new RuntimePseudoStateMatrixResponse(
            request.RequestId,
            request.SessionId,
            request.TopLevelId,
            target,
            status,
            startedAt,
            DateTimeOffset.UtcNow,
            entries,
            contactSheetPath,
            diagnostics,
            new Dictionary<string, string>
            {
                ["outputDirectory"] = outputDirectory,
                ["requestedStates"] = request.States.Count.ToString(CultureInfo.InvariantCulture),
                ["capturedStates"] = entries.Count(static entry => entry.Screenshot is not null).ToString(CultureInfo.InvariantCulture),
                ["unsupportedStates"] = entries.Count(static entry => entry.Status == Unsupported).ToString(CultureInfo.InvariantCulture),
                ["resetSemantics"] = "per_state_runtime_reset"
            }));
    }

    private static async Task<RuntimePseudoStateMatrixEntry> CaptureStateAsync(
        LocalBridgeClient bridgeClient,
        RuntimePseudoStateMatrixRequest request,
        string outputDirectory,
        RuntimeTargetContext target,
        string state,
        int stateIndex,
        RuntimePseudoStateMatrixEntry? baselineEntry,
        CancellationToken cancellationToken)
    {
        var diagnostics = new List<ProtocolError>();
        var inputs = new List<InputResponse>();
        var appliedMutations = new List<RuntimeMutationResponse>();
        var resetMutations = new List<RuntimeMutationResponse>();
        var pressed = false;
        var pointerMoved = false;
        var status = Passed;
        var message = $"Pseudo-state '{state}' captured.";
        ScreenshotResponse? screenshot = null;
        RuntimePseudoStateTargetSummary? targetSummary = null;
        Dictionary<string, string>? metadata = null;
        var screenshotPath = Path.Combine(
            outputDirectory,
            $"{(stateIndex + 1).ToString("00", CultureInfo.InvariantCulture)}-{SanitizeFileToken(state)}.png");

        {
            var currentTree = await bridgeClient.VisualTreeAsync(request.SessionId, request.TopLevelId, request.MaxDepth, cancellationToken);
            if (!currentTree.Success)
            {
                diagnostics.Add(ToProtocolError(currentTree.Error!));
                status = Failed;
                message = currentTree.Error!.Message;
                return await CompleteAsync();
            }

            var node = FindNodeById(currentTree.Value!.Root, target.NodeId);
            if (node is null)
            {
                var diagnostic = new ProtocolError(
                    "pseudo_state_target_not_found",
                    $"Pseudo-state target node '{target.NodeId}' was not found in the visual tree.",
                    new Dictionary<string, string> { ["nodeId"] = target.NodeId ?? "not_available" });
                diagnostics.Add(diagnostic);
                status = Failed;
                message = diagnostic.Message;
                return await CompleteAsync();
            }

            targetSummary = ToTargetSummary(node);
            if (!TryCreateOperations(state, out var operations))
            {
                var diagnostic = UnsupportedState(state);
                diagnostics.Add(diagnostic);
                status = Unsupported;
                message = diagnostic.Message;
                return await CompleteAsync();
            }

            foreach (var operation in operations)
            {
                switch (operation.Kind)
                {
                    case MatrixOperationKind.PointerOver:
                        var move = await MovePointerToTargetAsync(bridgeClient, request, node, cancellationToken);
                        if (!move.Success)
                        {
                            diagnostics.Add(ToProtocolError(move.Error!));
                            status = Unsupported;
                            message = move.Error!.Message;
                            return await CompleteAsync();
                        }

                        inputs.Add(move.Value!);
                        pointerMoved = true;
                        break;
                    case MatrixOperationKind.Pressed:
                        var pressMove = await MovePointerToTargetAsync(bridgeClient, request, node, cancellationToken);
                        if (!pressMove.Success)
                        {
                            diagnostics.Add(ToProtocolError(pressMove.Error!));
                            status = Unsupported;
                            message = pressMove.Error!.Message;
                            return await CompleteAsync();
                        }

                        inputs.Add(pressMove.Value!);
                        pointerMoved = true;

                        var down = await PointerButtonAsync(bridgeClient, request, node, InputActions.PointerDown, cancellationToken);
                        if (!down.Success)
                        {
                            diagnostics.Add(ToProtocolError(down.Error!));
                            status = Unsupported;
                            message = down.Error!.Message;
                            return await CompleteAsync();
                        }

                        inputs.Add(down.Value!);
                        pressed = true;
                        break;
                    case MatrixOperationKind.SetBoolProperty:
                        var mutation = await ApplyBoolMutationAsync(
                            bridgeClient,
                            request,
                            target,
                            state,
                            operation.PropertyName!,
                            operation.Value,
                            cancellationToken);
                        if (!mutation.Success)
                        {
                            diagnostics.Add(ToProtocolError(mutation.Error!));
                            status = Failed;
                            message = mutation.Error!.Message;
                            return await CompleteAsync();
                        }

                        var mutationResponse = mutation.Value!;
                        appliedMutations.Add(mutationResponse);
                        diagnostics.AddRange(mutationResponse.Diagnostics);
                        if (!mutationResponse.Applied && mutationResponse.Status is RuntimeMutationStatuses.Unsupported or RuntimeMutationStatuses.Rejected)
                        {
                            status = Unsupported;
                            message = mutationResponse.Diagnostics.FirstOrDefault()?.Message ?? $"Pseudo-state '{state}' is unsupported on this target.";
                            return await CompleteAsync();
                        }

                        break;
                }
            }

            var screenshotResult = await bridgeClient.CaptureScreenshotAsync(
                request.SessionId,
                request.TopLevelId,
                screenshotPath,
                cancellationToken);
            if (!screenshotResult.Success)
            {
                diagnostics.Add(ToProtocolError(screenshotResult.Error!));
                status = Failed;
                message = screenshotResult.Error!.Message;
                return await CompleteAsync();
            }

            screenshot = screenshotResult.Value!;
            var afterTree = await bridgeClient.VisualTreeAsync(request.SessionId, request.TopLevelId, request.MaxDepth, cancellationToken);
            var afterNode = afterTree.Success
                ? FindNodeById(afterTree.Value!.Root, target.NodeId) ?? node
                : node;
            if (!afterTree.Success)
            {
                diagnostics.Add(ToProtocolError(afterTree.Error!));
            }

            targetSummary = ToTargetSummary(afterNode);
            metadata = CreateEntryMetadata(state, appliedMutations, inputs);
            AddDiffMetadata(request, state, baselineEntry, screenshot, outputDirectory, metadata, diagnostics);

            return await CompleteAsync();
        }

        async Task<RuntimePseudoStateMatrixEntry> CompleteAsync()
        {
            if (pressed)
            {
                var reset = await PointerButtonAsync(bridgeClient, request, null, InputActions.PointerUp, cancellationToken);
                if (reset.Success)
                {
                    inputs.Add(reset.Value!);
                }
                else
                {
                    diagnostics.Add(ToProtocolError(reset.Error!));
                }
            }

            if (pointerMoved)
            {
                var resetPointer = await MovePointerAwayAsync(bridgeClient, request, cancellationToken);
                if (resetPointer.Success)
                {
                    inputs.Add(resetPointer.Value!);
                }
                else
                {
                    diagnostics.Add(ToProtocolError(resetPointer.Error!));
                }
            }

            foreach (var mutation in appliedMutations.Where(static mutation => mutation.Applied).Reverse<RuntimeMutationResponse>())
            {
                var reset = await ResetMutationAsync(bridgeClient, request, target, mutation.MutationId, cancellationToken);
                if (reset.Success)
                {
                    resetMutations.Add(reset.Value!);
                }
                else
                {
                    diagnostics.Add(ToProtocolError(reset.Error!));
                }
            }

            metadata ??= CreateEntryMetadata(state, appliedMutations, inputs);
            metadata["resetMutationCount"] = resetMutations.Count.ToString(CultureInfo.InvariantCulture);

            return new RuntimePseudoStateMatrixEntry(
                state,
                CreateLabel(state),
                status,
                message,
                DateTimeOffset.UtcNow,
                screenshot,
                targetSummary,
                appliedMutations,
                resetMutations,
                inputs,
                diagnostics,
                metadata);
        }
    }

    private static async Task<CoreResult<RuntimeTargetContext>> ResolveTargetAsync(
        LocalBridgeClient bridgeClient,
        RuntimePseudoStateMatrixRequest request,
        List<ProtocolError> diagnostics,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(request.Selector) || !string.IsNullOrWhiteSpace(request.Path))
        {
            diagnostics.Add(new ProtocolError(
                "pseudo_state_selector_path_targeting_unsupported",
                "Pseudo-state matrix selector/path targeting is not available yet; use nodeId, target, automationId, name, nodeType, or text.",
                new Dictionary<string, string>
                {
                    ["selector"] = request.Selector ?? "not_requested",
                    ["path"] = request.Path ?? "not_requested"
                }));
        }

        if (request.Target?.NodeId is not null)
        {
            return CoreResult<RuntimeTargetContext>.Ok(request.Target);
        }

        if (!string.IsNullOrWhiteSpace(request.NodeId))
        {
            return CoreResult<RuntimeTargetContext>.Ok(new RuntimeTargetContext(
                request.SessionId,
                request.TopLevelId,
                request.TreeKind,
                request.NodeId,
                targetKind: "node"));
        }

        if (string.IsNullOrWhiteSpace(request.NodeType)
            && string.IsNullOrWhiteSpace(request.Name)
            && string.IsNullOrWhiteSpace(request.AutomationId)
            && string.IsNullOrWhiteSpace(request.Text))
        {
            return CoreResult<RuntimeTargetContext>.Fail(new CoreError(
                CoreErrorCodes.InvalidBridgeRequest,
                "Pseudo-state matrix requires a node target or at least one node filter."));
        }

        var matches = await bridgeClient.FindNodesAsync(
            request.SessionId,
            request.TopLevelId,
            request.TreeKind,
            request.NodeType,
            request.Name,
            request.AutomationId,
            request.Text,
            request.MaxDepth,
            maxResults: 2,
            cancellationToken);
        if (!matches.Success)
        {
            return CoreResult<RuntimeTargetContext>.Fail(matches.Error!);
        }

        if (matches.Value!.Matches.Count == 0)
        {
            return CoreResult<RuntimeTargetContext>.Fail(new CoreError(
                CoreErrorCodes.InvalidBridgeRequest,
                "Pseudo-state matrix target filters did not match any nodes.",
                new Dictionary<string, string>
                {
                    ["automationId"] = request.AutomationId ?? "not_requested",
                    ["name"] = request.Name ?? "not_requested",
                    ["nodeType"] = request.NodeType ?? "not_requested",
                    ["text"] = request.Text ?? "not_requested"
                }));
        }

        if (matches.Value.Matches.Count > 1)
        {
            diagnostics.Add(new ProtocolError(
                "pseudo_state_target_filters_ambiguous",
                "Pseudo-state matrix target filters matched multiple nodes; the first match was used.",
                new Dictionary<string, string>
                {
                    ["matchedNodeIds"] = string.Join(",", matches.Value.Matches.Select(static match => match.Node.NodeId))
                }));
        }

        return CoreResult<RuntimeTargetContext>.Ok(matches.Value.Matches[0].Target ?? new RuntimeTargetContext(
            request.SessionId,
            request.TopLevelId,
            request.TreeKind,
            matches.Value.Matches[0].Node.NodeId,
            targetKind: "node"));
    }

    private static async Task<CoreResult<InputResponse>> MovePointerToTargetAsync(
        LocalBridgeClient bridgeClient,
        RuntimePseudoStateMatrixRequest request,
        TreeNodeSummary node,
        CancellationToken cancellationToken)
    {
        var center = Center(node.Bounds);
        if (center is null)
        {
            return CoreResult<InputResponse>.Fail(new CoreError(
                "pseudo_state_target_bounds_unavailable",
                $"Pseudo-state target '{node.NodeId}' does not expose bounds required for pointer state forcing.",
                new Dictionary<string, string> { ["nodeId"] = node.NodeId }));
        }

        var result = await bridgeClient.InputAsync(
            request.SessionId,
            request.TopLevelId,
            InputActions.PointerMove,
            center.Value.X,
            center.Value.Y,
            cancellationToken: cancellationToken);
        return result.Success
            ? result
            : CoreResult<InputResponse>.Fail(result.Error!);
    }

    private static async Task<CoreResult<InputResponse>> PointerButtonAsync(
        LocalBridgeClient bridgeClient,
        RuntimePseudoStateMatrixRequest request,
        TreeNodeSummary? node,
        string action,
        CancellationToken cancellationToken)
    {
        var point = Center(node?.Bounds) ?? new PointerPoint(0, 0);
        var result = await bridgeClient.InputAsync(
            request.SessionId,
            request.TopLevelId,
            action,
            point.X,
            point.Y,
            cancellationToken: cancellationToken);
        return result.Success
            ? result
            : CoreResult<InputResponse>.Fail(result.Error!);
    }

    private static async Task<CoreResult<InputResponse>> MovePointerAwayAsync(
        LocalBridgeClient bridgeClient,
        RuntimePseudoStateMatrixRequest request,
        CancellationToken cancellationToken)
    {
        var result = await bridgeClient.InputAsync(
            request.SessionId,
            request.TopLevelId,
            InputActions.PointerMove,
            0,
            0,
            cancellationToken: cancellationToken);
        return result.Success
            ? result
            : CoreResult<InputResponse>.Fail(result.Error!);
    }

    private static async Task<CoreResult<RuntimeMutationResponse>> ApplyBoolMutationAsync(
        LocalBridgeClient bridgeClient,
        RuntimePseudoStateMatrixRequest request,
        RuntimeTargetContext target,
        string state,
        string propertyName,
        bool value,
        CancellationToken cancellationToken)
    {
        var mutationRequest = new RuntimeMutationRequest(
            $"{request.RequestId}:{state}:{propertyName}:{value.ToString(CultureInfo.InvariantCulture)}",
            target,
            new RuntimeMutationOperation(
                RuntimeMutationOperationKinds.SetProperty,
                propertyName: propertyName,
                value: value.ToString(CultureInfo.InvariantCulture).ToLowerInvariant(),
                valueType: "bool"));
        return await bridgeClient.MutateNodeAsync(request.SessionId, mutationRequest, cancellationToken);
    }

    private static async Task<CoreResult<RuntimeMutationResponse>> ResetMutationAsync(
        LocalBridgeClient bridgeClient,
        RuntimePseudoStateMatrixRequest request,
        RuntimeTargetContext target,
        string mutationId,
        CancellationToken cancellationToken)
    {
        var resetRequest = new RuntimeMutationRequest(
            $"{request.RequestId}:reset:{SanitizeFileToken(mutationId)}",
            target,
            new RuntimeMutationOperation(
                RuntimeMutationOperationKinds.ResetMutation,
                mutationId: mutationId));
        return await bridgeClient.MutateNodeAsync(request.SessionId, resetRequest, cancellationToken);
    }

    private static bool TryCreateOperations(string state, out IReadOnlyList<MatrixOperation> operations)
    {
        var parsed = new List<MatrixOperation>();
        if (string.Equals(state, RuntimePseudoStates.Normal, StringComparison.Ordinal))
        {
            operations = parsed;
            return true;
        }

        foreach (var token in state.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            switch (token)
            {
                case "pointerover":
                case "hover":
                    parsed.Add(new MatrixOperation(MatrixOperationKind.PointerOver));
                    break;
                case "pressed":
                case "press":
                    parsed.Add(new MatrixOperation(MatrixOperationKind.Pressed));
                    break;
                case "disabled":
                    parsed.Add(new MatrixOperation(MatrixOperationKind.SetBoolProperty, "IsEnabled", false));
                    break;
                case "enabled":
                    parsed.Add(new MatrixOperation(MatrixOperationKind.SetBoolProperty, "IsEnabled", true));
                    break;
                case "selected":
                    parsed.Add(new MatrixOperation(MatrixOperationKind.SetBoolProperty, "IsSelected", true));
                    break;
                case "unselected":
                    parsed.Add(new MatrixOperation(MatrixOperationKind.SetBoolProperty, "IsSelected", false));
                    break;
                case "expanded":
                    parsed.Add(new MatrixOperation(MatrixOperationKind.SetBoolProperty, "IsExpanded", true));
                    break;
                case "collapsed":
                    parsed.Add(new MatrixOperation(MatrixOperationKind.SetBoolProperty, "IsExpanded", false));
                    break;
                default:
                    operations = [];
                    return false;
            }
        }

        operations = parsed;
        return parsed.Count > 0;
    }

    private static void AddDiffMetadata(
        RuntimePseudoStateMatrixRequest request,
        string state,
        RuntimePseudoStateMatrixEntry? baselineEntry,
        ScreenshotResponse screenshot,
        string outputDirectory,
        Dictionary<string, string> metadata,
        List<ProtocolError> diagnostics)
    {
        if (baselineEntry?.Screenshot is null
            || string.Equals(state, RuntimePseudoStates.Normal, StringComparison.Ordinal))
        {
            metadata["diffStatus"] = "baseline";
            return;
        }

        var diffPath = Path.Combine(
            outputDirectory,
            $"{SanitizeFileToken(state)}-vs-{SanitizeFileToken(baselineEntry.State)}-diff.png");
        var diff = new PreviewImageDiffer().Compare(
            baselineEntry.Screenshot.FilePath,
            screenshot.FilePath,
            diffPath,
            request.DiffTolerance);
        if (!diff.Success)
        {
            diagnostics.Add(new ProtocolError(diff.Error!.Code, diff.Error.Message, diff.Error.Details));
            metadata["diffStatus"] = "unavailable";
            return;
        }

        metadata["diffStatus"] = diff.Value!.Passed ? "unchanged" : "changed";
        metadata["diffPath"] = diff.Value.DiffPath ?? diffPath;
        metadata["changedPixels"] = diff.Value.ChangedPixels.ToString(CultureInfo.InvariantCulture);
        metadata["changedPercent"] = diff.Value.ChangedPercent.ToString("0.####", CultureInfo.InvariantCulture);
    }

    private static CoreResult<bool> TryCreateContactSheet(
        IReadOnlyList<RuntimePseudoStateMatrixEntry> entries,
        string contactSheetPath)
    {
        try
        {
            var decoded = entries
                .Select(entry => new SheetImage(entry, entry.Screenshot is null || !File.Exists(entry.Screenshot.FilePath) ? null : SKBitmap.Decode(entry.Screenshot.FilePath)))
                .ToArray();

            try
            {
                var successful = decoded.Where(static item => item.Bitmap is not null).ToArray();
                var cellWidth = Math.Max(320, successful.Length == 0 ? 320 : successful.Max(static item => item.Bitmap!.Width));
                var imageHeight = Math.Max(180, successful.Length == 0 ? 180 : successful.Max(static item => item.Bitmap!.Height));
                var columns = Math.Min(3, Math.Max(1, entries.Count));
                var rows = (int)Math.Ceiling(entries.Count / (double)columns);
                using var sheet = new SKBitmap(
                    columns * (cellWidth + CellPadding) + CellPadding,
                    rows * (imageHeight + LabelHeight + CellPadding) + CellPadding);
                using var canvas = new SKCanvas(sheet);
                canvas.Clear(SKColors.White);

                using var labelFont = new SKFont { Size = 16 };
                using var detailFont = new SKFont { Size = 12 };
                using var labelPaint = new SKPaint { Color = SKColors.Black, IsAntialias = true };
                using var detailPaint = new SKPaint { Color = SKColors.DimGray, IsAntialias = true };
                using var borderPaint = new SKPaint { Color = new SKColor(210, 214, 220), StrokeWidth = 1, Style = SKPaintStyle.Stroke };
                using var placeholderPaint = new SKPaint { Color = new SKColor(244, 246, 248), Style = SKPaintStyle.Fill };

                for (var index = 0; index < decoded.Length; index++)
                {
                    var column = index % columns;
                    var row = index / columns;
                    var x = CellPadding + column * (cellWidth + CellPadding);
                    var y = CellPadding + row * (imageHeight + LabelHeight + CellPadding);
                    var imageRect = new SKRect(x, y + LabelHeight, x + cellWidth, y + LabelHeight + imageHeight);

                    canvas.DrawText(decoded[index].Entry.Label, x, y + 18, SKTextAlign.Left, labelFont, labelPaint);
                    canvas.DrawText(decoded[index].Entry.Status, x + Math.Min(220, cellWidth - 80), y + 18, SKTextAlign.Left, detailFont, detailPaint);

                    if (decoded[index].Bitmap is null)
                    {
                        canvas.DrawRect(imageRect, placeholderPaint);
                        canvas.DrawText(decoded[index].Entry.Message, x + 10, y + LabelHeight + 28, SKTextAlign.Left, detailFont, detailPaint);
                    }
                    else
                    {
                        var bitmap = decoded[index].Bitmap!;
                        var scale = Math.Min(cellWidth / (float)bitmap.Width, imageHeight / (float)bitmap.Height);
                        var width = bitmap.Width * scale;
                        var height = bitmap.Height * scale;
                        var left = x + (cellWidth - width) / 2;
                        var top = y + LabelHeight + (imageHeight - height) / 2;
                        canvas.DrawBitmap(bitmap, new SKRect(left, top, left + width, top + height));
                    }

                    canvas.DrawRect(imageRect, borderPaint);
                }

                var directory = Path.GetDirectoryName(contactSheetPath);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                using var image = SKImage.FromBitmap(sheet);
                using var data = image.Encode(SKEncodedImageFormat.Png, 100);
                using var stream = File.Create(contactSheetPath);
                data.SaveTo(stream);
                return CoreResult<bool>.Ok(true);
            }
            finally
            {
                foreach (var item in decoded)
                {
                    item.Bitmap?.Dispose();
                }
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException or ArgumentException)
        {
            return CoreResult<bool>.Fail(new CoreError(
                CoreErrorCodes.PreviewHostFailed,
                $"Pseudo-state contact sheet could not be created: {exception.Message}",
                new Dictionary<string, string> { ["contactSheetPath"] = contactSheetPath }));
        }
    }

    private static Dictionary<string, string> CreateEntryMetadata(
        string state,
        IReadOnlyList<RuntimeMutationResponse> mutations,
        IReadOnlyList<InputResponse> inputs)
    {
        return new Dictionary<string, string>
        {
            ["state"] = state,
            ["stateTokens"] = string.Join(",", state.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)),
            ["appliedMutationCount"] = mutations.Count(static mutation => mutation.Applied).ToString(CultureInfo.InvariantCulture),
            ["inputCount"] = inputs.Count.ToString(CultureInfo.InvariantCulture),
            ["resetRequired"] = (mutations.Any(static mutation => mutation.Applied)
                || inputs.Any(static input => input.Action is InputActions.PointerDown or InputActions.PointerMove)).ToString(CultureInfo.InvariantCulture)
        };
    }

    private static ProtocolError UnsupportedState(string state)
    {
        return new ProtocolError(
            "pseudo_state_not_supported",
            $"Pseudo-state '{state}' is not supported by the runtime matrix runner.",
            new Dictionary<string, string>
            {
                ["state"] = state,
                ["supportedStates"] = string.Join(",", RuntimePseudoStates.DefaultMatrix.Concat([RuntimePseudoStates.Expanded, RuntimePseudoStates.Collapsed]))
            });
    }

    private static RuntimePseudoStateTargetSummary ToTargetSummary(TreeNodeSummary node)
    {
        return new RuntimePseudoStateTargetSummary(
            node.NodeId,
            node.NodeType,
            node.Name,
            node.AutomationId,
            node.Text,
            node.Bounds,
            node.Classes,
            node.AccessibilityState);
    }

    private static TreeNodeSummary? FindNodeById(TreeNodeSummary node, string? nodeId)
    {
        if (string.IsNullOrWhiteSpace(nodeId))
        {
            return null;
        }

        if (string.Equals(node.NodeId, nodeId, StringComparison.Ordinal))
        {
            return node;
        }

        foreach (var child in node.Children)
        {
            var match = FindNodeById(child, nodeId);
            if (match is not null)
            {
                return match;
            }
        }

        return null;
    }

    private static PointerPoint? Center(NodeBounds? bounds)
    {
        return bounds is null
            ? null
            : new PointerPoint(bounds.X + bounds.Width / 2, bounds.Y + bounds.Height / 2);
    }

    private static string ResolveOutputDirectory(RuntimePseudoStateMatrixRequest request)
    {
        return request.OutputDirectory
            ?? Path.Combine(Path.GetTempPath(), "AvaScope", "pseudo-state-matrix", request.RequestId);
    }

    private static string CreateLabel(string state)
    {
        return string.Join(" + ", state.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
    }

    private static string SanitizeFileToken(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var sanitized = new string(value
            .Select(character => invalid.Contains(character) || char.IsWhiteSpace(character) || character == '+' ? '-' : character)
            .ToArray())
            .Trim('-', '.');
        return string.IsNullOrWhiteSpace(sanitized) ? "state" : sanitized;
    }

    private static ProtocolError ToProtocolError(CoreError error)
    {
        return new ProtocolError(error.Code, error.Message, error.Details);
    }

    private readonly record struct PointerPoint(double X, double Y);

    private sealed record MatrixOperation(MatrixOperationKind Kind, string? PropertyName = null, bool Value = false);

    private enum MatrixOperationKind
    {
        PointerOver,
        Pressed,
        SetBoolProperty
    }

    private sealed record SheetImage(RuntimePseudoStateMatrixEntry Entry, SKBitmap? Bitmap);
}
