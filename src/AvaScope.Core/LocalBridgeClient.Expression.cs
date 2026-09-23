using AvaScope.Protocol;

namespace AvaScope.Core;

public sealed partial class LocalBridgeClient
{
    public async Task<CoreResult<RuntimeExpressionResponse>> EvaluateRuntimeAsync(RuntimeExpressionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var policy = request.Policy is null ? null : new RuntimeEvidencePolicyEnforcer(request.Policy);
        if (policy is not null)
        {
            var authorized = policy.AuthorizeSession(this, request.SessionId);
            if (!authorized.Success) return CoreResult<RuntimeExpressionResponse>.Fail(authorized.Error!);
            var action = policy.AuthorizeAction(SemanticWorkflowActions.Inspect, null);
            if (!action.Success) return CoreResult<RuntimeExpressionResponse>.Fail(action.Error!);
        }
        var manifest = FindSingleManifest(null, request.SessionId);
        if (!manifest.Success) return CoreResult<RuntimeExpressionResponse>.Fail(manifest.Error!);
        var result = await SendAsync<RuntimeExpressionResponse>(manifest.Value!,
            new(NewRequestId(), BridgeIpcMethods.EvaluateRuntime, expression: request), cancellationToken);
        if (policy is null) return result;
        if (result.Success) return policy.Sanitize(result.Value!);
        var error = policy.Sanitize(result.Error!);
        return CoreResult<RuntimeExpressionResponse>.Fail(error.Success ? error.Value! : error.Error!);
    }
}
