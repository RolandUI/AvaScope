using AvaScope.Protocol;

namespace AvaScope.Core;

public sealed class RuntimeObserver
{
    public async Task<CoreResult<RuntimeObservationResponse>> ObserveAsync(LocalBridgeClient client,
        RuntimeObservationRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(request);
        var policy = request.Policy is null ? null : new RuntimeEvidencePolicyEnforcer(request.Policy);
        if (PreparePolicy(client, request, policy) is { } policyError) return CoreResult<RuntimeObservationResponse>.Fail(policyError);
        var result = await client.ReadObservationAsync(request, cancellationToken);
        if (!result.Success) return result;
        var response = result.Value!;
        var windows = new List<RuntimeObservedWindow>();
        foreach (var window in response.Windows)
        {
            var current = window with { ScreenshotPng = null };
            if (window.Screenshot is { } capture && window.ScreenshotPng is { } png)
            {
                var path = Path.Combine(request.OutputDirectory!, $"observation-{Guid.NewGuid():N}.png");
                var saved = false;
                try
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var screenshot = new ScreenshotResponse(capture.SessionId, capture.TopLevelId, path, capture.PixelWidth,
                        capture.PixelHeight, capture.CapturedAt, capture.Target, capture.Provenance, capture.Readiness);
                    var parts = new Dictionary<string, string>(current.Parts);
                    if (policy is not null)
                    {
                        var masked = policy.MaskObservationPng(png, screenshot);
                        png = masked.Png;
                        parts["screenshotMasking"] = masked.Masking;
                    }
                    Directory.CreateDirectory(request.OutputDirectory!);
                    await using (var file = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                        await file.WriteAsync(png, cancellationToken);
                    current = current with { Screenshot = screenshot, Parts = parts };
                    saved = true;
                }
                catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException)
                {
                    var parts = new Dictionary<string, string>(current.Parts) { ["screenshot"] = "unavailable" };
                    current = current with { Screenshot = null, Parts = parts,
                        Diagnostics = current.Diagnostics.Append(new ProtocolError("observation_screenshot_save_failed", exception.Message)).ToArray() };
                }
                finally
                {
                    if (!saved && File.Exists(path)) File.Delete(path);
                }
            }
            if (request.Policy?.ExcludedControlAutomationIds is { Count: > 0 } excludedIds)
            {
                var excludedNodes = new HashSet<string>(StringComparer.Ordinal);
                foreach (var node in current.Nodes)
                    if (excludedIds.Contains(node.AutomationId, StringComparer.Ordinal)
                        || node.ParentNodeId is not null && excludedNodes.Contains(node.ParentNodeId)) excludedNodes.Add(node.NodeId);
                current = current with
                {
                    Nodes = current.Nodes.Where(node => !excludedNodes.Contains(node.NodeId)).ToArray(),
                    FocusedNodeId = excludedNodes.Contains(current.FocusedNodeId ?? "") ? null : current.FocusedNodeId
                };
            }
            windows.Add(current);
        }
        response = response with { Windows = windows };
        if (policy is not null)
        {
            var sanitized = policy.Sanitize(response);
            if (!sanitized.Success) return sanitized;
            response = sanitized.Value!;
        }
        return CoreResult<RuntimeObservationResponse>.Ok(ResponseBudgeter.ApplyObservation(response,
            request.MaxInlineBytes, request.MaxNodes, request.MaxDepth, request.OutputDirectory));
    }

    public async Task<CoreResult<RuntimeObservationChangesResponse>> ObserveChangesAsync(LocalBridgeClient client,
        RuntimeObservationChangesRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(request);
        var policy = request.Observation.Policy is null ? null : new RuntimeEvidencePolicyEnforcer(request.Observation.Policy);
        if (PreparePolicy(client, request.Observation, policy) is { } error) return CoreResult<RuntimeObservationChangesResponse>.Fail(error);
        var result = await client.ReadObservationChangesAsync(request, cancellationToken);
        if (!result.Success) return result;
        if (policy is not null) result = policy.Sanitize(result.Value!);
        if (!result.Success) return result;
        return CoreResult<RuntimeObservationChangesResponse>.Ok(ResponseBudgeter.ApplyObservationChanges(result.Value!,
            request.Observation.MaxInlineBytes, request.MaxEvents, request.Observation.OutputDirectory));
    }

    private static CoreError? PreparePolicy(LocalBridgeClient client, RuntimeObservationRequest request, RuntimeEvidencePolicyEnforcer? policy)
    {
        if (policy is null) return null;
        var session = policy.AuthorizeSession(client, request.SessionId);
        if (!session.Success) return session.Error;
        foreach (var action in request.IncludeScreenshot ? new[] { SemanticWorkflowActions.Inspect, SemanticWorkflowActions.Screenshot } : [SemanticWorkflowActions.Inspect])
        {
            var authorization = policy.AuthorizeAction(action, customActionName: null);
            if (!authorization.Success) return authorization.Error;
        }
        return policy.PrepareRun(request.OutputDirectory!, [], request.RequestId).Error;
    }
}
