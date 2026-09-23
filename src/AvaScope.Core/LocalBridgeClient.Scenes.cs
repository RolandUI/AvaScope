using AvaScope.Protocol;

namespace AvaScope.Core;

public sealed partial class LocalBridgeClient
{
    public async Task<CoreResult<RuntimeSceneResponse>> SceneAsync(RuntimeSceneRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var policy = request.Policy is null ? null : new RuntimeEvidencePolicyEnforcer(request.Policy);
        if (policy is not null)
        {
            var session = policy.AuthorizeSession(this, request.Canvas.SessionId);
            if (!session.Success) return CoreResult<RuntimeSceneResponse>.Fail(session.Error!);
            var inspection = policy.AuthorizeAction(SemanticWorkflowActions.Inspect, null);
            if (!inspection.Success) return CoreResult<RuntimeSceneResponse>.Fail(inspection.Error!);
            if (request.Action == "invoke" && policy.AuthorizeAction(SemanticWorkflowActions.CustomAction, request.ActionName) is { Success: false } denied)
                return CoreResult<RuntimeSceneResponse>.Fail(denied.Error!);
        }
        var manifest = FindSingleManifest(null, request.Canvas.SessionId);
        if (!manifest.Success) return CoreResult<RuntimeSceneResponse>.Fail(manifest.Error!);
        var result = await SendAsync<RuntimeSceneResponse>(manifest.Value!, new(NewRequestId(), BridgeIpcMethods.Scene, scene: request), cancellationToken);
        if (request.Action == "invoke" && !result.Success && result.Error!.Code is CoreErrorCodes.BridgeIpcFailed or CoreErrorCodes.BridgeIpcUnavailable)
            result = CoreResult<RuntimeSceneResponse>.Fail(new(result.Error.Code, result.Error.Message,
                new Dictionary<string, string>(result.Error.Details ?? new Dictionary<string, string>())
                { ["dispatched"] = "unknown", ["nextAction"] = "Inspect the scene and application state; this action must not be automatically replayed." }));
        if (policy is null) return result;
        if (result.Success) return policy.Sanitize(result.Value!);
        var error = policy.Sanitize(result.Error!);
        return CoreResult<RuntimeSceneResponse>.Fail(error.Success ? error.Value! : error.Error!);
    }
}
