using System.Security.Cryptography;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using AvaScope.Core;
using AvaScope.Protocol;

namespace AvaScope.Bridge;

public sealed partial class AvaScopeBridgeRuntime
{
    public async Task<CoreResult<RuntimeObservationResponse>> ObserveAsync(RuntimeObservationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.SessionId != SessionId)
            return CoreResult<RuntimeObservationResponse>.Fail(new CoreError("observation_session_mismatch", "The observation must address this bridge session."));
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(request.TimeoutMs);
        RuntimeObservationResponse? observation = null;
        try
        {
            observation = await Dispatcher.UIThread.InvokeAsync(() => CollectObservation(request), DispatcherPriority.Background, deadline.Token);
            var before = ObservationFingerprint(observation);
            if (request.IncludeScreenshot)
            {
                var windows = observation.Windows.ToArray();
                var imageBytesRemaining = 256 * 1024;
                for (var index = 0; index < windows.Length; index++)
                {
                    var window = windows[index];
                    if (window.Window is null) continue;
                    var parts = new Dictionary<string, string>(window.Parts);
                    var ready = await ReadinessAsync(window.TopLevelId,
                        options: new RuntimeReadinessProbeOptions(waitForFrame: true), cancellationToken: deadline.Token);
                    var capture = !ready.Success
                        ? CoreResult<(ScreenshotResponse Screenshot, byte[] Png)>.Fail(ready.Error!)
                        : await Dispatcher.UIThread.InvokeAsync(() => CaptureObservationFrame(window.TopLevelId,
                            Path.Combine(request.OutputDirectory!, $"observation-{observation.ObservationId}-{index}.png"),
                            imageBytesRemaining, ready.Value!), DispatcherPriority.Background, deadline.Token);
                    parts["screenshot"] = capture.Success ? "available" : "unavailable";
                    windows[index] = window with
                    {
                        Screenshot = capture.Success ? capture.Value.Screenshot : null,
                        ScreenshotPng = capture.Success ? capture.Value.Png : null,
                        Parts = parts,
                        Diagnostics = capture.Success ? window.Diagnostics : window.Diagnostics.Append(new ProtocolError(capture.Error!.Code, capture.Error.Message)).ToArray()
                    };
                    if (capture.Success) imageBytesRemaining -= capture.Value.Png.Length;
                    observation = observation with { Windows = windows.ToArray() };
                }
            }
            var after = await Dispatcher.UIThread.InvokeAsync(() => CollectObservation(request), DispatcherPriority.Background, deadline.Token);
            var changed = !before.AsSpan().SequenceEqual(ObservationFingerprint(after));
            return CoreResult<RuntimeObservationResponse>.Ok(observation with
            {
                CompletedAt = DateTimeOffset.UtcNow,
                Consistency = changed ? "changed_during_collection" : "no_sampled_change_detected",
                ChangedDuringCollection = changed
            });
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested && observation is not null)
        {
            return CoreResult<RuntimeObservationResponse>.Ok(observation with
            {
                CompletedAt = DateTimeOffset.UtcNow,
                Consistency = "comparison_unavailable",
                Diagnostics = [new ProtocolError("observation_timeout", "The collection deadline expired; completed parts are retained and pending parts are unavailable.")],
                Windows = observation.Windows.Select(window => window with
                {
                    Parts = window.Parts.ToDictionary(pair => pair.Key, pair => pair.Value == "pending" ? "unavailable" : pair.Value)
                }).ToArray()
            });
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return CoreResult<RuntimeObservationResponse>.Fail(new CoreError("observation_timeout", "The UI dispatcher did not respond before the observation deadline."));
        }
    }

    private CoreResult<(ScreenshotResponse Screenshot, byte[] Png)> CaptureObservationFrame(string topLevelId,
        string path, int maximumBytes, RuntimeReadinessSnapshot readiness)
    {
        Dispatcher.UIThread.VerifyAccess();
        var topLevel = FindTopLevel(topLevelId);
        if (topLevel is null || readiness.Frame.Status != "rendered" || !readiness.LayoutValid)
            return CoreResult<(ScreenshotResponse, byte[])>.Fail(new CoreError("observation_frame_unavailable", "The selected window has no usable rendered frame."));
        var pixels = GetPixelSize(topLevel);
        if (pixels.Width < 1 || pixels.Height < 1 || (long)pixels.Width * pixels.Height > 16777216)
            return CoreResult<(ScreenshotResponse, byte[])>.Fail(new CoreError("observation_screenshot_pixel_limit", "Observation screenshots are limited to 16777216 pixels."));
        try
        {
            using var bitmap = new RenderTargetBitmap(pixels, new Vector(96 * topLevel.RenderScaling, 96 * topLevel.RenderScaling));
            bitmap.Render(topLevel.GetPresentationSource()?.RootVisual ?? topLevel);
            using var stream = new MemoryStream();
            bitmap.Save(stream, PngBitmapEncoderOptions.Default);
            if (stream.Length > maximumBytes)
                return CoreResult<(ScreenshotResponse, byte[])>.Fail(new CoreError("observation_screenshot_byte_limit", "PNG data exceeded the shared 256 KiB observation image budget; use the dedicated screenshot tool."));
            return CoreResult<(ScreenshotResponse, byte[])>.Ok((new ScreenshotResponse(SessionId, topLevelId, path,
                pixels.Width, pixels.Height, DateTimeOffset.UtcNow, CreateTopLevelTarget(topLevelId, topLevel),
                RuntimePlatformEvidence.Operation(topLevel, RuntimeOperationRoutes.RenderTargetBitmap, coordinateSpace: "top_level_pixel"), readiness), stream.ToArray()));
        }
        catch (Exception exception) when (exception is InvalidOperationException or NotSupportedException or IOException)
        {
            return CoreResult<(ScreenshotResponse, byte[])>.Fail(new CoreError("observation_screenshot_failed", ClipObservationText(exception.Message)!));
        }
    }

    private RuntimeObservationResponse CollectObservation(RuntimeObservationRequest request)
    {
        Dispatcher.UIThread.VerifyAccess();
        var started = DateTimeOffset.UtcNow;
        var policy = request.Policy is null ? null : new RuntimeEvidencePolicyEnforcer(request.Policy);
        string? Clip(string? value) => ClipObservationText(value is null ? null : policy?.SanitizeScalar(value) ?? value);
        bool Excluded(object visual) => request.Policy?.ExcludedControlAutomationIds.Contains(GetAutomationId(visual), StringComparer.Ordinal) == true;
        var available = DiscoverTopLevels();
        var selected = available.Where(window => (request.TopLevelIds.Count == 0 || request.TopLevelIds.Contains(window.Id, StringComparer.Ordinal))
            && (request.TopLevelTitle is null || string.Equals(request.TopLevelTitle, window.Title, StringComparison.Ordinal))
            && (!request.ActiveOnly || window.IsActive)).Take(request.MaxTopLevels + 1).ToArray();
        var truncated = selected.Length > request.MaxTopLevels;
        var windows = new List<RuntimeObservedWindow>();
        var remaining = request.MaxNodes;
        var remainingNodeBytes = 384 * 1024;
        foreach (var summary in selected.Take(request.MaxTopLevels))
        {
            var topLevel = FindTopLevel(summary.Id);
            if (topLevel is null) continue;
            var nodes = new List<RuntimeObservedNode>();
            var errors = new List<ProtocolError>();
            var parts = new Dictionary<string, string>
            {
                ["window"] = "available", ["nodes"] = "available", ["focus"] = "available",
                ["diagnostics"] = request.IncludeDiagnostics ? "available_public_validation" : "not_requested",
                ["screenshot"] = request.IncludeScreenshot ? "pending" : "not_requested"
            };
            var root = request.RootNodeId is null ? topLevel : FindNodeById(topLevel, request.RootNodeId) as Visual;
            var cut = false;
            if (root is null)
            {
                parts["nodes"] = "unavailable";
                parts["diagnostics"] = "unavailable";
                errors.Add(new ProtocolError("observation_root_missing", "The requested root visual no longer exists in this window."));
            }
            else
            {
                var queue = new Queue<(Visual Node, string? Parent, int Depth)>();
                queue.Enqueue((root, null, 0));
                while (remaining > 0 && queue.TryDequeue(out var item))
                {
                    remaining--;
                    if (Excluded(item.Node)) continue;
                    var id = CreateNodeId(item.Node, TreeKinds.Visual);
                    var text = GetText(item.Node);
                    var bounds = GetGlobalBounds(item.Node, topLevel);
                    RuntimeValidationState? validation = null;
                    if (request.IncludeDiagnostics && item.Node is Control control)
                    {
                        var validationErrors = (DataValidationErrors.GetErrors(control) ?? []).Take(4)
                            .Select(error => Clip(FormatValidationError(error)) ?? "Unknown validation error").ToArray();
                        var hasErrors = DataValidationErrors.GetHasErrors(control);
                        validation = new RuntimeValidationState(validationErrors.Length > 3 ? "truncated" : hasErrors ? "has_errors" : "clean",
                            "avalonia_public_data_validation_errors", hasErrors, validationErrors.Length > 3 ? null : validationErrors.Length,
                            validationErrors.Take(3).ToArray());
                    }
                    var sampled = new RuntimeObservedNode(id, item.Parent, item.Depth,
                        Clip(item.Node.GetType().FullName)!, Clip(GetName(item.Node)),
                        Clip(GetAutomationId(item.Node)), Clip(text),
                        bounds is null ? null : new NodeBounds(bounds.Value.X, bounds.Value.Y, bounds.Value.Width, bounds.Value.Height),
                        CreateInteractionState(topLevel, item.Node)!, CreateObjectGeneration(item.Node), validation, text?.Length > 384);
                    var nodeBytes = JsonSerializer.SerializeToUtf8Bytes(sampled).Length;
                    if (nodeBytes > remainingNodeBytes)
                    {
                        cut = true;
                        break;
                    }
                    remainingNodeBytes -= nodeBytes;
                    nodes.Add(sampled);
                    foreach (var child in item.Node.GetVisualChildren())
                    {
                        if (item.Depth >= request.MaxDepth || queue.Count >= remaining)
                        {
                            cut = true;
                            break;
                        }
                        queue.Enqueue((child, id, item.Depth + 1));
                    }
                }
                cut |= queue.Count > 0;
            }
            var focused = topLevel.FocusManager?.GetFocusedElement();
            var focusExcluded = focused is Visual focusVisual && (Excluded(focusVisual) || focusVisual.GetVisualAncestors().Any(Excluded));
            if (focusExcluded) focused = null;
            var focusId = focused is null ? null : CreateNodeId(focused, TreeKinds.Visual);
            parts["focus"] = focusExcluded ? "excluded_by_policy" : focused is null ? "none" : nodes.Any(node => node.NodeId == focusId) ? "available" : "outside_sample";
            if (cut) parts["nodes"] = "partial";
            truncated |= cut;
            var boundedSummary = new TopLevelSummary(summary.Id, summary.Kind, Clip(summary.Title),
                summary.Width, summary.Height, summary.RenderScaling, summary.IsActive, summary.Backend);
            windows.Add(new RuntimeObservedWindow(summary.Id, boundedSummary, CreateObjectGeneration(topLevel), DateTimeOffset.UtcNow,
                focusId, nodes, parts, cut, errors));
        }
        foreach (var missing in request.TopLevelIds.Except(available.Select(window => window.Id), StringComparer.Ordinal).Take(request.MaxTopLevels - windows.Count))
            windows.Add(new RuntimeObservedWindow(missing, null, null, DateTimeOffset.UtcNow, null, [],
                new Dictionary<string, string> { ["window"] = "unavailable", ["nodes"] = "unavailable", ["focus"] = "unavailable", ["diagnostics"] = "unavailable", ["screenshot"] = "unavailable" },
                false, [new ProtocolError("observation_top_level_missing", "The selected window is absent or closed.")]));
        return new RuntimeObservationResponse(request.RequestId, Guid.NewGuid().ToString("N"), SessionId, started,
            DateTimeOffset.UtcNow, GetCapabilities().Revision, windows, "comparison_pending", null, truncated, []);
    }

    private static string? ClipObservationText(string? value) => value is { Length: > 384 } ? value[..384] : value;

    private static byte[] ObservationFingerprint(RuntimeObservationResponse response) => SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(
        response.Windows.Select(window => new { window.TopLevelId, window.Window, window.Generation, window.FocusedNodeId, window.Nodes, window.Truncated })));
}
