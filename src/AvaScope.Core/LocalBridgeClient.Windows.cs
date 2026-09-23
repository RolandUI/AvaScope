using AvaScope.Protocol;

namespace AvaScope.Core;

public sealed partial class LocalBridgeClient
{
    public async Task<CoreResult<RuntimeWindowResponse>> WindowAsync(RuntimeWindowRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var policy = request.Policy is null ? null : new RuntimeEvidencePolicyEnforcer(request.Policy);
        if (policy is not null)
        {
            var session = policy.AuthorizeSession(this, request.Target.SessionId);
            if (!session.Success) return CoreResult<RuntimeWindowResponse>.Fail(session.Error!);
            var inspection = policy.AuthorizeAction(SemanticWorkflowActions.Inspect, null);
            if (!inspection.Success) return CoreResult<RuntimeWindowResponse>.Fail(inspection.Error!);
            if (request.Action != "inspect" && !request.Policy!.AllowedWindowActions.Contains(request.Action, StringComparer.Ordinal))
                return CoreResult<RuntimeWindowResponse>.Fail(new("window_policy_denied", "Explicitly allow this operation in allowedWindowActions."));
        }
        var manifest = FindSingleManifest(null, request.Target.SessionId);
        if (!manifest.Success) return CoreResult<RuntimeWindowResponse>.Fail(manifest.Error!);
        var result = await SendAsync<RuntimeWindowResponse>(manifest.Value!, new(NewRequestId(), BridgeIpcMethods.Window, window: request), cancellationToken);
        if (request.Action != "inspect" && !result.Success && result.Error!.Code is CoreErrorCodes.BridgeIpcFailed or CoreErrorCodes.BridgeIpcUnavailable)
            result = CoreResult<RuntimeWindowResponse>.Fail(new(result.Error.Code, result.Error.Message,
                new Dictionary<string, string>(result.Error.Details ?? new Dictionary<string, string>())
                { ["dispatched"] = "unknown", ["nextAction"] = "Reinspect the selected window before issuing a new intent; no automatic replay is performed." }));
        if (policy is null) return result;
        if (result.Success) return policy.Sanitize(result.Value!);
        var error = policy.Sanitize(result.Error!);
        return CoreResult<RuntimeWindowResponse>.Fail(error.Success ? error.Value! : error.Error!);
    }
}
