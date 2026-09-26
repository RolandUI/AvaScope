using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media.Imaging;
using Avalonia.Rendering.Composition;
using Avalonia.Threading;
using Avalonia.VisualTree;
using AvaScope.Core;
using AvaScope.Protocol;

namespace AvaScope.Bridge;

public sealed partial class AvaScopeBridgeRuntime
{
    private const int ReadinessMaximumNodes = 512;
    private const int ReadinessMaximumDepth = 8;
    private const int ReadinessMaximumPixels = 1024 * 1024;
    private RuntimeReadinessStage _applicationReadiness = new("unavailable", "host_declaration", DateTimeOffset.UtcNow,
        "The application has not declared test readiness.");

    public void SetReadiness(string state, string? reason = null)
    {
        Dispatcher.UIThread.VerifyAccess();
        if (state is not ("starting" or "busy" or "ready" or "failed"))
            throw new ArgumentException("Readiness state must be starting, busy, ready or failed.", nameof(state));
        if (_sessionRegistry.Get(SessionId).Value?.State != SessionLifecycleState.Active)
            throw new InvalidOperationException("Readiness cannot be declared on a closed bridge.");
        _applicationReadiness = new RuntimeReadinessStage(state, "host_declaration", DateTimeOffset.UtcNow,
            reason is { Length: > 512 } ? reason[..512] : reason);
    }

    public async Task<CoreResult<RuntimeReadinessSnapshot>> ReadinessAsync(
        string topLevelId,
        string? nodeId = null,
        RuntimeReadinessProbeOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        options ??= new RuntimeReadinessProbeOptions();
        var started = Stopwatch.GetTimestamp();
        var phaseStarted = started;
        var phase = options.WaitForFrame ? "frame_dispatch" : "snapshot_dispatch";
        Task? frameProcessed = null;
        Task? frameRendered = null;
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(options.TimeoutMs);
        try
        {
            var frameStatus = "not_sampled";
            string? frameReason = null;
            if (options.WaitForFrame)
            {
                var frame = Dispatcher.UIThread.CheckAccess()
                    ? BeginReadinessFrame(topLevelId)
                    : await Dispatcher.UIThread.InvokeAsync(() => BeginReadinessFrame(topLevelId), DispatcherPriority.Background, deadline.Token);
                frameStatus = frame.Status;
                frameReason = frame.Reason;
                frameProcessed = frame.Processed;
                frameRendered = frame.Rendered;
                if (frame.Rendered is not null)
                {
                    phase = "composition_render";
                    phaseStarted = Stopwatch.GetTimestamp();
                    await frame.Rendered.WaitAsync(deadline.Token).ConfigureAwait(false);
                    frameStatus = "rendered";
                }
            }

            phase = "snapshot_dispatch";
            phaseStarted = Stopwatch.GetTimestamp();
            var snapshot = Dispatcher.UIThread.CheckAccess()
                ? ObserveReadiness(topLevelId, nodeId, frameStatus, frameReason, options.IncludeFrameHash)
                : await Dispatcher.UIThread.InvokeAsync(
                    () => ObserveReadiness(topLevelId, nodeId, frameStatus, frameReason, options.IncludeFrameHash),
                    DispatcherPriority.Background, deadline.Token);
            return CoreResult<RuntimeReadinessSnapshot>.Ok(snapshot);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            var observedAt = Stopwatch.GetTimestamp();
            var renderedStatus = frameRendered?.Status.ToString() ?? "not_observed";
            var processedStatus = frameProcessed?.Status.ToString() ?? "not_observed";
            return CoreResult<RuntimeReadinessSnapshot>.Fail(new CoreError("runtime_readiness_probe_timeout",
                "The UI dispatcher or composition frame did not complete within the bounded readiness observation.",
                new Dictionary<string, string>
                {
                    ["timeoutMs"] = options.TimeoutMs.ToString(CultureInfo.InvariantCulture),
                    ["readinessPhase"] = phase,
                    ["readinessElapsedMs"] = Stopwatch.GetElapsedTime(started, observedAt).TotalMilliseconds.ToString("0.###", CultureInfo.InvariantCulture),
                    ["phaseElapsedMs"] = Stopwatch.GetElapsedTime(phaseStarted, observedAt).TotalMilliseconds.ToString("0.###", CultureInfo.InvariantCulture),
                    ["frameProcessedStatus"] = processedStatus,
                    ["frameRenderedStatus"] = renderedStatus
                }));
        }
        catch (Exception exception) when (exception is InvalidOperationException or NotSupportedException)
        {
            return CoreResult<RuntimeReadinessSnapshot>.Fail(new CoreError("runtime_readiness_unavailable", exception.Message));
        }
    }

    private (Task? Processed, Task? Rendered, string Status, string? Reason) BeginReadinessFrame(string topLevelId)
    {
        Dispatcher.UIThread.VerifyAccess();
        var topLevel = FindTopLevel(topLevelId);
        if (topLevel?.PlatformImpl is null || !topLevel.IsVisible || topLevel.ClientSize.Width <= 0 || topLevel.ClientSize.Height <= 0)
            return (null, null, "pending", "The target window is not visible with positive client bounds.");
        var root = topLevel.GetPresentationSource()?.RootVisual ?? topLevel;
        var compositor = ElementComposition.GetElementVisual(root)?.Compositor;
        var batch = compositor?.RequestCompositionBatchCommitAsync();
        return batch is null
            ? (null, null, "unavailable", "No public composition visual is available for the target window.")
            : (batch.Processed, batch.Rendered, "pending", null);
    }

    private async Task<CoreResult<ScreenshotResponse>> CaptureAfterRenderAsync(
        string topLevelId, string outputPath, CancellationToken cancellationToken)
    {
        var readiness = await ReadinessAsync(topLevelId, options: new RuntimeReadinessProbeOptions(waitForFrame: true),
            cancellationToken: cancellationToken);
        if (!readiness.Success) return CoreResult<ScreenshotResponse>.Fail(readiness.Error!);
        if (readiness.Value!.Frame.Status != "rendered" || !readiness.Value.LayoutValid)
            return CoreResult<ScreenshotResponse>.Fail(new CoreError("runtime_frame_not_ready",
                readiness.Value.Frame.Reason ?? "The target window does not yet have a usable rendered frame."));
        var result = await CaptureScreenshotAsync(topLevelId, outputPath, cancellationToken);
        if (!result.Success) return result;
        var screenshot = result.Value!;
        return CoreResult<ScreenshotResponse>.Ok(new ScreenshotResponse(screenshot.SessionId, screenshot.TopLevelId,
            screenshot.FilePath, screenshot.PixelWidth, screenshot.PixelHeight, screenshot.CapturedAt,
            screenshot.Target, screenshot.Provenance, readiness.Value));
    }

    private RuntimeReadinessSnapshot ObserveReadiness(
        string topLevelId, string? nodeId, string frameStatus, string? frameReason, bool includeFrameHash)
    {
        Dispatcher.UIThread.VerifyAccess();
        var at = DateTimeOffset.UtcNow;
        var topLevel = FindTopLevel(topLevelId);
        var created = topLevel?.PlatformImpl is not null;
        var bridge = new RuntimeReadinessStage("available", "explicit_bridge_activation", at);
        var window = new RuntimeReadinessStage(created ? "created" : "missing", "registered_top_level", at);
        var frame = new RuntimeReadinessStage(frameStatus, "composition_batch_rendered", at, frameReason);
        if (!created)
            return new RuntimeReadinessSnapshot(SessionId, topLevelId, bridge, window,
                frame with { Status = "pending", Reason = "The target window is absent or closed." }, _applicationReadiness, false);

        var visual = string.IsNullOrWhiteSpace(nodeId) ? topLevel
            : FindNodeById(topLevel!, nodeId) as Visual;
        if (visual is null)
            return new RuntimeReadinessSnapshot(SessionId, topLevelId, bridge, window, frame, _applicationReadiness,
                false, SampleReason: "The selected visual is absent; resolve it again.", Backend: RuntimePlatformEvidence.Observe(topLevel!));

        var layout = new StringBuilder();
        var pending = new Queue<(Visual Visual, int Depth)>();
        pending.Enqueue((visual, 0));
        var count = 0;
        var truncated = false;
        var valid = visual.IsEffectivelyVisible && visual.Bounds.Width > 0 && visual.Bounds.Height > 0;
        while (pending.TryDequeue(out var item))
        {
            count++;
            layout.Append(CreateNodeId(item.Visual, TreeKinds.Visual)).Append('|')
                .Append(FormatRect(GetGlobalBounds(item.Visual, topLevel!) ?? item.Visual.Bounds)).Append('|')
                .Append(item.Visual.IsEffectivelyVisible).Append(';');
            if (item.Visual.IsEffectivelyVisible && item.Visual is Layoutable laidOut)
                valid &= laidOut.IsMeasureValid && laidOut.IsArrangeValid;
            foreach (var child in item.Visual.GetVisualChildren())
            {
                if (count + pending.Count >= ReadinessMaximumNodes || item.Depth >= ReadinessMaximumDepth)
                {
                    truncated = true;
                    break;
                }
                pending.Enqueue((child, item.Depth + 1));
            }
        }
        var fingerprint = truncated ? null : Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(layout.ToString())));
        string? frameFingerprint = null;
        var reason = truncated ? "Layout sampling exceeded 512 nodes or depth 8; select a smaller subtree." : null;
        if (includeFrameHash && frameStatus == "rendered" && valid)
        {
            var scaling = topLevel!.RenderScaling;
            var size = string.IsNullOrWhiteSpace(nodeId) ? topLevel.ClientSize : visual.Bounds.Size;
            var width = Math.Ceiling(size.Width * scaling);
            var height = Math.Ceiling(size.Height * scaling);
            if (!double.IsFinite(width * height) || width < 1 || height < 1 || width * height > ReadinessMaximumPixels)
                reason = "Frame sampling is limited to 1048576 pixels; select a smaller subtree.";
            else
            {
                using var bitmap = new RenderTargetBitmap(new PixelSize((int)width, (int)height), new Vector(96 * scaling, 96 * scaling));
                RenderRuntimeVisual(bitmap, string.IsNullOrWhiteSpace(nodeId) ? topLevel.GetPresentationSource()?.RootVisual ?? topLevel : visual);
                using var stream = new MemoryStream();
                bitmap.Save(stream, PngBitmapEncoderOptions.Default);
                frameFingerprint = Convert.ToHexStringLower(SHA256.HashData(stream.GetBuffer().AsSpan(0, (int)stream.Length)));
            }
        }
        return new RuntimeReadinessSnapshot(SessionId, topLevelId, bridge, window, frame, _applicationReadiness,
            valid, fingerprint, frameFingerprint, count, truncated, reason,
            CreateNodeTarget(topLevelId, TreeKinds.Visual, topLevel!, visual), RuntimePlatformEvidence.Observe(topLevel!),
            frameFingerprint is null ? null : RuntimeOperationRoutes.RenderTargetBitmap);
    }
}
