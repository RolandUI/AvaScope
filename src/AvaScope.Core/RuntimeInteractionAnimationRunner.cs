using System.Globalization;
using AvaScope.Protocol;
using SkiaSharp;

namespace AvaScope.Core;

public sealed class RuntimeInteractionAnimationRunner
{
    private const string Passed = "passed";
    private const string Failed = "failed";
    private const int FrameStripLabelHeight = 34;
    private const int FrameStripPadding = 8;

    public async Task<CoreResult<RuntimeInteractionAnimationResponse>> RunAsync(
        LocalBridgeClient bridgeClient,
        RuntimeInteractionAnimationRequest request,
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
            return CoreResult<RuntimeInteractionAnimationResponse>.Fail(new CoreError(
                CoreErrorCodes.InvalidBridgeRequest,
                $"Interaction animation output directory could not be created: {exception.Message}",
                new Dictionary<string, string> { ["outputDirectory"] = outputDirectory }));
        }

        var stepResults = new List<RuntimeInteractionAnimationStepResult>();
        foreach (var step in request.Steps)
        {
            var result = await ExecuteStepAsync(
                bridgeClient,
                request,
                outputDirectory,
                step,
                stepResults.Count,
                cancellationToken);
            stepResults.Add(result);
            diagnostics.AddRange(result.Diagnostics);

            if (result.Status == Failed)
            {
                break;
            }
        }

        var assertionResults = EvaluateAssertions(request.Assertions, stepResults);
        diagnostics.AddRange(assertionResults
            .Where(static assertion => assertion.Status == Failed)
            .Select(static assertion => new ProtocolError(
                "interaction_geometry_assertion_failed",
                assertion.Message,
                new Dictionary<string, string>
                {
                    ["assertionId"] = assertion.AssertionId,
                    ["targetNodeId"] = assertion.TargetNodeId,
                    ["mode"] = assertion.Mode,
                    ["metric"] = assertion.Metric
                })));

        var frames = stepResults.SelectMany(static step => step.Frames).ToArray();
        var frameStripPath = request.FrameStripPath
            ?? Path.Combine(outputDirectory, $"{SanitizeFileToken(request.RequestId)}-frame-strip.png");
        if (frames.Length > 0)
        {
            var strip = TryCreateFrameStrip(frames, frameStripPath);
            if (!strip.Success)
            {
                diagnostics.Add(new ProtocolError(strip.Error!.Code, strip.Error.Message, strip.Error.Details));
                frameStripPath = null;
            }
        }
        else
        {
            frameStripPath = null;
        }

        var status = stepResults.All(static step => step.Status == Passed)
            && assertionResults.All(static assertion => assertion.Status == Passed)
            && diagnostics.Count == 0
                ? Passed
                : Failed;

        return CoreResult<RuntimeInteractionAnimationResponse>.Ok(new RuntimeInteractionAnimationResponse(
            request.RequestId,
            request.SessionId,
            request.TopLevelId,
            status,
            startedAt,
            DateTimeOffset.UtcNow,
            stepResults,
            assertionResults,
            frameStripPath,
            diagnostics,
            new Dictionary<string, string>
            {
                ["outputDirectory"] = outputDirectory,
                ["requestedSteps"] = request.Steps.Count.ToString(CultureInfo.InvariantCulture),
                ["executedSteps"] = stepResults.Count.ToString(CultureInfo.InvariantCulture),
                ["capturedFrames"] = frames.Length.ToString(CultureInfo.InvariantCulture),
                ["assertions"] = request.Assertions.Count.ToString(CultureInfo.InvariantCulture)
            }));
    }

    private static async Task<RuntimeInteractionAnimationStepResult> ExecuteStepAsync(
        LocalBridgeClient bridgeClient,
        RuntimeInteractionAnimationRequest request,
        string outputDirectory,
        RuntimeInteractionAnimationStep step,
        int stepIndex,
        CancellationToken cancellationToken)
    {
        var diagnostics = new List<ProtocolError>();
        var metadata = new Dictionary<string, string>
        {
            ["captureFrames"] = step.CaptureFrames.ToString(CultureInfo.InvariantCulture)
        };
        InputResponse? input = null;
        var frames = new List<RuntimeInteractionAnimationFrame>();

        try
        {
            if (step.Action == RuntimeInteractionAnimationActions.Wait)
            {
                if (step.WaitMs is > 0)
                {
                    await Task.Delay(step.WaitMs.Value, cancellationToken);
                }
            }
            else
            {
                var inputResult = await bridgeClient.InputAsync(
                    request.SessionId,
                    request.TopLevelId,
                    step.Action,
                    step.X,
                    step.Y,
                    step.Text,
                    step.TargetNodeId,
                    step.InputKey,
                    step.KeyModifiers,
                    cancellationToken: cancellationToken);
                if (!inputResult.Success)
                {
                    var error = ToProtocolError(inputResult.Error!);
                    return FailedStep(step, error.Message, diagnostics.Append(error).ToArray(), metadata, input, frames);
                }

                input = inputResult.Value!;
                if (step.WaitMs is > 0)
                {
                    await Task.Delay(step.WaitMs.Value, cancellationToken);
                }
            }

            if (step.CaptureFrames)
            {
                var captured = await CaptureFramesAsync(
                    bridgeClient,
                    request,
                    outputDirectory,
                    step,
                    stepIndex,
                    cancellationToken);
                frames.AddRange(captured.Frames);
                diagnostics.AddRange(captured.Diagnostics);
            }

            metadata["capturedFrames"] = frames.Count.ToString(CultureInfo.InvariantCulture);
            var status = diagnostics.Count == 0 ? Passed : Failed;
            var message = status == Passed
                ? $"Interaction step '{step.Id}' captured {frames.Count} frame(s)."
                : diagnostics[0].Message;

            return new RuntimeInteractionAnimationStepResult(
                step.Id,
                step.Action,
                status,
                message,
                DateTimeOffset.UtcNow,
                input,
                frames,
                diagnostics,
                metadata);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException)
        {
            diagnostics.Add(new ProtocolError("interaction_animation_step_failed", exception.Message));
            return FailedStep(step, exception.Message, diagnostics, metadata, input, frames);
        }
    }

    private static async Task<FrameCaptureResult> CaptureFramesAsync(
        LocalBridgeClient bridgeClient,
        RuntimeInteractionAnimationRequest request,
        string outputDirectory,
        RuntimeInteractionAnimationStep step,
        int stepIndex,
        CancellationToken cancellationToken)
    {
        var frames = new List<RuntimeInteractionAnimationFrame>();
        var diagnostics = new List<ProtocolError>();
        var offsets = step.FrameOffsetsMs ?? request.DefaultFrameOffsetsMs;
        var targetNodeIds = request.Assertions
            .Where(assertion => assertion.StepId is null || string.Equals(assertion.StepId, step.Id, StringComparison.Ordinal))
            .Select(static assertion => assertion.TargetNodeId)
            .Append(step.TargetNodeId)
            .Where(static nodeId => !string.IsNullOrWhiteSpace(nodeId))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var previousOffset = 0;

        for (var offsetIndex = 0; offsetIndex < offsets.Count; offsetIndex++)
        {
            var offset = offsets[offsetIndex];
            var waitDelta = offsetIndex == 0 ? offset : offset - previousOffset;
            previousOffset = offset;
            if (waitDelta > 0)
            {
                await Task.Delay(waitDelta, cancellationToken);
            }

            var frameId = $"{SanitizeFileToken(step.Id)}-{offsetIndex.ToString("00", CultureInfo.InvariantCulture)}-{offset.ToString(CultureInfo.InvariantCulture)}ms";
            var screenshotPath = Path.Combine(
                outputDirectory,
                $"{(stepIndex + 1).ToString("00", CultureInfo.InvariantCulture)}-{frameId}.png");

            var treeResult = await bridgeClient.VisualTreeAsync(
                request.SessionId,
                request.TopLevelId,
                request.MaxDepth,
                cancellationToken);
            if (!treeResult.Success)
            {
                diagnostics.Add(ToProtocolError(treeResult.Error!));
                continue;
            }

            var screenshotResult = await bridgeClient.CaptureScreenshotAsync(
                request.SessionId,
                request.TopLevelId,
                screenshotPath,
                cancellationToken);
            if (!screenshotResult.Success)
            {
                diagnostics.Add(ToProtocolError(screenshotResult.Error!));
                continue;
            }

            var geometry = targetNodeIds
                .Select(targetNodeId => FindNodeWithParent(treeResult.Value!.Root, targetNodeId!, null))
                .Where(static match => match.Node is not null)
                .Select(static match => ToGeometrySnapshot(match.Node!, match.Parent))
                .ToArray();
            foreach (var missingTarget in targetNodeIds.Where(target => geometry.All(snapshot => snapshot.NodeId != target)))
            {
                diagnostics.Add(new ProtocolError(
                    "interaction_geometry_target_not_found",
                    $"Interaction animation target node '{missingTarget}' was not found in frame '{frameId}'.",
                    new Dictionary<string, string>
                    {
                        ["stepId"] = step.Id,
                        ["frameId"] = frameId,
                        ["targetNodeId"] = missingTarget!
                    }));
            }

            var overlayPath = Path.Combine(
                outputDirectory,
                $"{(stepIndex + 1).ToString("00", CultureInfo.InvariantCulture)}-{frameId}-geometry.png");
            var overlay = TryCreateGeometryOverlay(screenshotResult.Value!, geometry, overlayPath);
            if (!overlay.Success)
            {
                diagnostics.Add(new ProtocolError(overlay.Error!.Code, overlay.Error.Message, overlay.Error.Details));
                overlayPath = string.Empty;
            }

            frames.Add(new RuntimeInteractionAnimationFrame(
                step.Id,
                frameId,
                offsetIndex,
                offset,
                DateTimeOffset.UtcNow,
                screenshotResult.Value!,
                string.IsNullOrWhiteSpace(overlayPath) ? null : overlayPath,
                geometry,
                new Dictionary<string, string>
                {
                    ["treeRoot"] = treeResult.Value!.Root.NodeId,
                    ["geometrySnapshotCount"] = geometry.Length.ToString(CultureInfo.InvariantCulture)
                }));
        }

        return new FrameCaptureResult(frames, diagnostics);
    }

    private static IReadOnlyList<RuntimeInteractionGeometryAssertionResult> EvaluateAssertions(
        IReadOnlyList<RuntimeInteractionGeometryAssertion> assertions,
        IReadOnlyList<RuntimeInteractionAnimationStepResult> steps)
    {
        return assertions
            .Select(assertion => EvaluateAssertion(
                assertion,
                steps
                    .Where(step => assertion.StepId is null || string.Equals(step.StepId, assertion.StepId, StringComparison.Ordinal))
                    .SelectMany(static step => step.Frames)
                    .ToArray()))
            .ToArray();
    }

    private static RuntimeInteractionGeometryAssertionResult EvaluateAssertion(
        RuntimeInteractionGeometryAssertion assertion,
        IReadOnlyList<RuntimeInteractionAnimationFrame> frames)
    {
        var samples = frames
            .Select(frame =>
            {
                var snapshot = frame.Geometry.FirstOrDefault(item => string.Equals(item.NodeId, assertion.TargetNodeId, StringComparison.Ordinal));
                return new RuntimeInteractionGeometrySample(
                    frame.StepId,
                    frame.FrameId,
                    frame.OffsetMs,
                    TryReadMetric(snapshot?.Bounds, assertion.Metric, out var value) ? value : null,
                    snapshot?.Bounds,
                    snapshot?.ParentBounds,
                    snapshot?.IsClippedByParent ?? false,
                    snapshot is null ? "target_not_found" : null);
            })
            .ToArray();

        var values = samples
            .Where(static sample => sample.Value.HasValue)
            .Select(static sample => sample.Value!.Value)
            .ToArray();

        string status;
        string message;
        var metadata = new Dictionary<string, string>
        {
            ["sampleCount"] = samples.Length.ToString(CultureInfo.InvariantCulture),
            ["valueCount"] = values.Length.ToString(CultureInfo.InvariantCulture)
        };

        if (samples.Length == 0)
        {
            status = Failed;
            message = $"Geometry assertion '{assertion.AssertionId}' did not match any captured frames.";
        }
        else
        {
            switch (assertion.Mode)
            {
                case RuntimeInteractionGeometryAssertionModes.Stable:
                    if (values.Length != samples.Length)
                    {
                        status = Failed;
                        message = $"Geometry assertion '{assertion.AssertionId}' could not read metric '{assertion.Metric}' in every frame.";
                    }
                    else
                    {
                        var minimum = values.Min();
                        var maximum = values.Max();
                        metadata["minimum"] = minimum.ToString("0.###", CultureInfo.InvariantCulture);
                        metadata["maximum"] = maximum.ToString("0.###", CultureInfo.InvariantCulture);
                        metadata["delta"] = (maximum - minimum).ToString("0.###", CultureInfo.InvariantCulture);
                        status = maximum - minimum <= assertion.Tolerance ? Passed : Failed;
                        message = status == Passed
                            ? $"Metric '{assertion.Metric}' stayed stable within tolerance {assertion.Tolerance.ToString(CultureInfo.InvariantCulture)}."
                            : $"Metric '{assertion.Metric}' changed by {(maximum - minimum).ToString("0.###", CultureInfo.InvariantCulture)}, exceeding tolerance {assertion.Tolerance.ToString(CultureInfo.InvariantCulture)}.";
                    }

                    break;
                case RuntimeInteractionGeometryAssertionModes.Equal:
                    if (assertion.ExpectedValue is null)
                    {
                        status = Failed;
                        message = $"Geometry assertion '{assertion.AssertionId}' requires expectedValue for equals mode.";
                    }
                    else if (values.Length != samples.Length)
                    {
                        status = Failed;
                        message = $"Geometry assertion '{assertion.AssertionId}' could not read metric '{assertion.Metric}' in every frame.";
                    }
                    else
                    {
                        var maximumDelta = values.Max(value => Math.Abs(value - assertion.ExpectedValue.Value));
                        metadata["maximumDelta"] = maximumDelta.ToString("0.###", CultureInfo.InvariantCulture);
                        status = maximumDelta <= assertion.Tolerance ? Passed : Failed;
                        message = status == Passed
                            ? $"Metric '{assertion.Metric}' matched expected value {assertion.ExpectedValue.Value.ToString(CultureInfo.InvariantCulture)}."
                            : $"Metric '{assertion.Metric}' differed from expected value by up to {maximumDelta.ToString("0.###", CultureInfo.InvariantCulture)}.";
                    }

                    break;
                case RuntimeInteractionGeometryAssertionModes.WithinRange:
                    if (values.Length != samples.Length)
                    {
                        status = Failed;
                        message = $"Geometry assertion '{assertion.AssertionId}' could not read metric '{assertion.Metric}' in every frame.";
                    }
                    else if (assertion.MinValue is null && assertion.MaxValue is null)
                    {
                        status = Failed;
                        message = $"Geometry assertion '{assertion.AssertionId}' requires minValue or maxValue for within_range mode.";
                    }
                    else
                    {
                        var lower = assertion.MinValue - assertion.Tolerance ?? double.NegativeInfinity;
                        var upper = assertion.MaxValue + assertion.Tolerance ?? double.PositiveInfinity;
                        status = values.All(value => value >= lower && value <= upper) ? Passed : Failed;
                        message = status == Passed
                            ? $"Metric '{assertion.Metric}' stayed inside the requested range."
                            : $"Metric '{assertion.Metric}' left the requested range.";
                    }

                    break;
                case RuntimeInteractionGeometryAssertionModes.FinalStable:
                    if (values.Length < 2 || values.Length != samples.Length)
                    {
                        status = Failed;
                        message = $"Geometry assertion '{assertion.AssertionId}' requires at least two readable frames for final_stable mode.";
                    }
                    else
                    {
                        var finalDelta = Math.Abs(values[^1] - values[^2]);
                        metadata["finalDelta"] = finalDelta.ToString("0.###", CultureInfo.InvariantCulture);
                        status = finalDelta <= assertion.Tolerance ? Passed : Failed;
                        message = status == Passed
                            ? $"Final metric '{assertion.Metric}' settled within tolerance."
                            : $"Final metric '{assertion.Metric}' changed by {finalDelta.ToString("0.###", CultureInfo.InvariantCulture)} between the last two frames.";
                    }

                    break;
                case RuntimeInteractionGeometryAssertionModes.NotClipped:
                    status = samples.All(sample => sample.Bounds is not null && !sample.IsClippedByParent) ? Passed : Failed;
                    message = status == Passed
                        ? $"Target '{assertion.TargetNodeId}' was not clipped by its parent in captured frames."
                        : $"Target '{assertion.TargetNodeId}' was clipped or missing in at least one captured frame.";
                    break;
                default:
                    status = Failed;
                    message = $"Geometry assertion mode '{assertion.Mode}' is not supported.";
                    break;
            }
        }

        return new RuntimeInteractionGeometryAssertionResult(
            assertion.AssertionId,
            assertion.TargetNodeId,
            assertion.Metric,
            assertion.Mode,
            status,
            message,
            assertion.Tolerance,
            assertion.StepId,
            assertion.ExpectedValue,
            assertion.MinValue,
            assertion.MaxValue,
            samples,
            metadata);
    }

    private static bool TryReadMetric(NodeBounds? bounds, string metric, out double value)
    {
        value = 0;
        if (bounds is null)
        {
            return false;
        }

        value = metric switch
        {
            RuntimeInteractionGeometryMetrics.X => bounds.X,
            RuntimeInteractionGeometryMetrics.Y => bounds.Y,
            RuntimeInteractionGeometryMetrics.Left => bounds.X,
            RuntimeInteractionGeometryMetrics.Top => bounds.Y,
            RuntimeInteractionGeometryMetrics.Right => bounds.X + bounds.Width,
            RuntimeInteractionGeometryMetrics.Bottom => bounds.Y + bounds.Height,
            RuntimeInteractionGeometryMetrics.Width => bounds.Width,
            RuntimeInteractionGeometryMetrics.Height => bounds.Height,
            RuntimeInteractionGeometryMetrics.CenterX => bounds.X + bounds.Width / 2,
            RuntimeInteractionGeometryMetrics.CenterY => bounds.Y + bounds.Height / 2,
            _ => double.NaN
        };

        return !double.IsNaN(value);
    }

    private static CoreResult<bool> TryCreateGeometryOverlay(
        ScreenshotResponse screenshot,
        IReadOnlyList<RuntimeInteractionGeometrySnapshot> geometry,
        string overlayPath)
    {
        try
        {
            using var bitmap = SKBitmap.Decode(screenshot.FilePath);
            if (bitmap is null)
            {
                return CoreResult<bool>.Fail(new CoreError(
                    CoreErrorCodes.PreviewHostFailed,
                    "Interaction geometry overlay could not decode the screenshot.",
                    new Dictionary<string, string> { ["screenshotPath"] = screenshot.FilePath }));
            }

            using var surface = new SKBitmap(bitmap.Width, bitmap.Height);
            using var canvas = new SKCanvas(surface);
            canvas.Clear(SKColors.Transparent);
            canvas.DrawBitmap(bitmap, 0, 0);

            using var targetPaint = new SKPaint
            {
                Color = new SKColor(0, 120, 215),
                StrokeWidth = 3,
                Style = SKPaintStyle.Stroke,
                IsAntialias = true
            };
            using var parentPaint = new SKPaint
            {
                Color = new SKColor(180, 180, 180),
                StrokeWidth = 1,
                Style = SKPaintStyle.Stroke,
                IsAntialias = true
            };
            using var clippedPaint = new SKPaint
            {
                Color = new SKColor(210, 50, 50),
                StrokeWidth = 3,
                Style = SKPaintStyle.Stroke,
                IsAntialias = true
            };
            using var labelPaint = new SKPaint { Color = SKColors.Black, IsAntialias = true };
            using var labelBackground = new SKPaint { Color = new SKColor(255, 255, 255, 220), Style = SKPaintStyle.Fill };
            using var font = new SKFont { Size = 13 };

            foreach (var snapshot in geometry)
            {
                if (snapshot.ParentBounds is not null)
                {
                    canvas.DrawRect(ToRect(snapshot.ParentBounds), parentPaint);
                }

                if (snapshot.Bounds is null)
                {
                    continue;
                }

                var rect = ToRect(snapshot.Bounds);
                canvas.DrawRect(rect, snapshot.IsClippedByParent ? clippedPaint : targetPaint);
                var label = $"{snapshot.NodeId} {snapshot.Bounds.Width.ToString("0.#", CultureInfo.InvariantCulture)}x{snapshot.Bounds.Height.ToString("0.#", CultureInfo.InvariantCulture)}";
                var labelWidth = Math.Min(bitmap.Width - rect.Left - 4, Math.Max(60, label.Length * 7));
                var labelRect = new SKRect(rect.Left, Math.Max(0, rect.Top - 20), rect.Left + labelWidth, Math.Max(18, rect.Top - 2));
                canvas.DrawRect(labelRect, labelBackground);
                canvas.DrawText(label, labelRect.Left + 3, labelRect.Bottom - 4, SKTextAlign.Left, font, labelPaint);
            }

            Directory.CreateDirectory(Path.GetDirectoryName(overlayPath)!);
            using var image = SKImage.FromBitmap(surface);
            using var data = image.Encode(SKEncodedImageFormat.Png, 100);
            using var stream = File.Create(overlayPath);
            data.SaveTo(stream);
            return CoreResult<bool>.Ok(true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException or ArgumentException)
        {
            return CoreResult<bool>.Fail(new CoreError(
                CoreErrorCodes.PreviewHostFailed,
                $"Interaction geometry overlay could not be created: {exception.Message}",
                new Dictionary<string, string> { ["overlayPath"] = overlayPath }));
        }
    }

    private static CoreResult<bool> TryCreateFrameStrip(
        IReadOnlyList<RuntimeInteractionAnimationFrame> frames,
        string frameStripPath)
    {
        try
        {
            var decoded = frames
                .Select(frame => new StripImage(
                    frame,
                    SKBitmap.Decode(frame.GeometryOverlayPath is not null && File.Exists(frame.GeometryOverlayPath)
                        ? frame.GeometryOverlayPath
                        : frame.Screenshot?.FilePath)))
                .ToArray();

            try
            {
                var successful = decoded.Where(static item => item.Bitmap is not null).ToArray();
                var cellWidth = Math.Max(260, successful.Length == 0 ? 260 : successful.Max(static item => item.Bitmap!.Width));
                var imageHeight = Math.Max(160, successful.Length == 0 ? 160 : successful.Max(static item => item.Bitmap!.Height));
                var columns = Math.Min(4, Math.Max(1, frames.Count));
                var rows = (int)Math.Ceiling(frames.Count / (double)columns);
                using var strip = new SKBitmap(
                    columns * (cellWidth + FrameStripPadding) + FrameStripPadding,
                    rows * (imageHeight + FrameStripLabelHeight + FrameStripPadding) + FrameStripPadding);
                using var canvas = new SKCanvas(strip);
                canvas.Clear(SKColors.White);

                using var labelFont = new SKFont { Size = 15 };
                using var detailFont = new SKFont { Size = 12 };
                using var labelPaint = new SKPaint { Color = SKColors.Black, IsAntialias = true };
                using var detailPaint = new SKPaint { Color = SKColors.DimGray, IsAntialias = true };
                using var borderPaint = new SKPaint { Color = new SKColor(210, 214, 220), StrokeWidth = 1, Style = SKPaintStyle.Stroke };
                using var placeholderPaint = new SKPaint { Color = new SKColor(244, 246, 248), Style = SKPaintStyle.Fill };

                for (var index = 0; index < decoded.Length; index++)
                {
                    var column = index % columns;
                    var row = index / columns;
                    var x = FrameStripPadding + column * (cellWidth + FrameStripPadding);
                    var y = FrameStripPadding + row * (imageHeight + FrameStripLabelHeight + FrameStripPadding);
                    var imageRect = new SKRect(x, y + FrameStripLabelHeight, x + cellWidth, y + FrameStripLabelHeight + imageHeight);

                    canvas.DrawText(decoded[index].Frame.StepId, x, y + 16, SKTextAlign.Left, labelFont, labelPaint);
                    canvas.DrawText(
                        $"{decoded[index].Frame.OffsetMs.ToString(CultureInfo.InvariantCulture)} ms",
                        x,
                        y + 31,
                        SKTextAlign.Left,
                        detailFont,
                        detailPaint);

                    if (decoded[index].Bitmap is null)
                    {
                        canvas.DrawRect(imageRect, placeholderPaint);
                        canvas.DrawText("frame unavailable", x + 10, y + FrameStripLabelHeight + 26, SKTextAlign.Left, detailFont, detailPaint);
                    }
                    else
                    {
                        var bitmap = decoded[index].Bitmap!;
                        var scale = Math.Min(cellWidth / (float)bitmap.Width, imageHeight / (float)bitmap.Height);
                        var width = bitmap.Width * scale;
                        var height = bitmap.Height * scale;
                        var left = x + (cellWidth - width) / 2;
                        var top = y + FrameStripLabelHeight + (imageHeight - height) / 2;
                        canvas.DrawBitmap(bitmap, new SKRect(left, top, left + width, top + height));
                    }

                    canvas.DrawRect(imageRect, borderPaint);
                }

                Directory.CreateDirectory(Path.GetDirectoryName(frameStripPath)!);
                using var image = SKImage.FromBitmap(strip);
                using var data = image.Encode(SKEncodedImageFormat.Png, 100);
                using var stream = File.Create(frameStripPath);
                data.SaveTo(stream);
                return CoreResult<bool>.Ok(true);
            }
            finally
            {
                foreach (var image in decoded)
                {
                    image.Bitmap?.Dispose();
                }
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException or ArgumentException)
        {
            return CoreResult<bool>.Fail(new CoreError(
                CoreErrorCodes.PreviewHostFailed,
                $"Interaction animation frame strip could not be created: {exception.Message}",
                new Dictionary<string, string> { ["frameStripPath"] = frameStripPath }));
        }
    }

    private static RuntimeInteractionAnimationStepResult FailedStep(
        RuntimeInteractionAnimationStep step,
        string message,
        IReadOnlyList<ProtocolError> diagnostics,
        IReadOnlyDictionary<string, string> metadata,
        InputResponse? input,
        IReadOnlyList<RuntimeInteractionAnimationFrame> frames)
    {
        return new RuntimeInteractionAnimationStepResult(
            step.Id,
            step.Action,
            Failed,
            message,
            DateTimeOffset.UtcNow,
            input,
            frames,
            diagnostics,
            metadata);
    }

    private static RuntimeInteractionGeometrySnapshot ToGeometrySnapshot(
        TreeNodeSummary node,
        TreeNodeSummary? parent)
    {
        return new RuntimeInteractionGeometrySnapshot(
            node.NodeId,
            node.NodeType,
            node.Name,
            node.AutomationId,
            node.Text,
            node.Bounds,
            parent?.NodeId,
            parent?.Bounds,
            IsClippedByParent(node.Bounds, parent?.Bounds));
    }

    private static bool IsClippedByParent(NodeBounds? bounds, NodeBounds? parentBounds)
    {
        if (bounds is null || parentBounds is null)
        {
            return false;
        }

        return bounds.X < parentBounds.X
            || bounds.Y < parentBounds.Y
            || bounds.X + bounds.Width > parentBounds.X + parentBounds.Width
            || bounds.Y + bounds.Height > parentBounds.Y + parentBounds.Height;
    }

    private static (TreeNodeSummary? Node, TreeNodeSummary? Parent) FindNodeWithParent(
        TreeNodeSummary node,
        string nodeId,
        TreeNodeSummary? parent)
    {
        if (string.Equals(node.NodeId, nodeId, StringComparison.Ordinal))
        {
            return (node, parent);
        }

        foreach (var child in node.Children)
        {
            var match = FindNodeWithParent(child, nodeId, node);
            if (match.Node is not null)
            {
                return match;
            }
        }

        return (null, null);
    }

    private static SKRect ToRect(NodeBounds bounds)
    {
        return new SKRect(
            (float)bounds.X,
            (float)bounds.Y,
            (float)(bounds.X + bounds.Width),
            (float)(bounds.Y + bounds.Height));
    }

    private static string ResolveOutputDirectory(RuntimeInteractionAnimationRequest request)
    {
        return request.OutputDirectory
            ?? Path.Combine(Path.GetTempPath(), "avascope-interaction-animation", SanitizeFileToken(request.RequestId));
    }

    private static string SanitizeFileToken(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var chars = value.Select(character => invalid.Contains(character) ? '-' : character).ToArray();
        var sanitized = new string(chars).Trim('-', ' ', '.');
        return string.IsNullOrWhiteSpace(sanitized) ? "item" : sanitized;
    }

    private static ProtocolError ToProtocolError(CoreError error)
    {
        return new ProtocolError(error.Code, error.Message, error.Details);
    }

    private sealed record FrameCaptureResult(
        IReadOnlyList<RuntimeInteractionAnimationFrame> Frames,
        IReadOnlyList<ProtocolError> Diagnostics);

    private sealed record StripImage(RuntimeInteractionAnimationFrame Frame, SKBitmap? Bitmap);
}
