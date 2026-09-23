using AvaScope.Protocol;

namespace AvaScope.Core;

public sealed partial class LocalBridgeClient
{
    public async Task<CoreResult<RuntimeOperationResponse>> OperationAsync(RuntimeOperationRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var policy = request.Policy is null ? null : new RuntimeEvidencePolicyEnforcer(request.Policy);
        if (policy is not null)
        {
            var session = policy.AuthorizeSession(this, request.SessionId);
            if (!session.Success) return CoreResult<RuntimeOperationResponse>.Fail(session.Error!);
            var action = policy.AuthorizeAction(request.Action == "cancel" ? SemanticWorkflowActions.CustomAction : SemanticWorkflowActions.Inspect, null);
            if (!action.Success) return CoreResult<RuntimeOperationResponse>.Fail(action.Error!);
        }
        var manifest = FindSingleManifest(null, request.SessionId);
        if (!manifest.Success) return CoreResult<RuntimeOperationResponse>.Fail(manifest.Error!);
        var result = await SendAsync<RuntimeOperationResponse>(manifest.Value!, new(NewRequestId(), BridgeIpcMethods.Operation, operation: request),
            cancellationToken, TimeSpan.FromMilliseconds(request.TimeoutMs + 5000));
        if (policy is null) return result;
        if (result.Success) return policy.Sanitize(result.Value!);
        var error = policy.Sanitize(result.Error!);
        return CoreResult<RuntimeOperationResponse>.Fail(error.Success ? error.Value! : error.Error!);
    }
}
