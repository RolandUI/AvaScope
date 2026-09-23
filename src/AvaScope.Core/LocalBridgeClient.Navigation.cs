using AvaScope.Protocol;

namespace AvaScope.Core;

public sealed partial class LocalBridgeClient
{
    public async Task<CoreResult<RuntimeNavigationResponse>> NavigationAsync(RuntimeNavigationRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var policy = request.Policy is null ? null : new RuntimeEvidencePolicyEnforcer(request.Policy);
        if (policy is not null)
        {
            var session = policy.AuthorizeSession(this, request.SessionId);
            if (!session.Success) return CoreResult<RuntimeNavigationResponse>.Fail(session.Error!);
            var inspection = policy.AuthorizeAction(SemanticWorkflowActions.Inspect, null);
            if (!inspection.Success) return CoreResult<RuntimeNavigationResponse>.Fail(inspection.Error!);
        }
        var manifest = FindSingleManifest(null, request.SessionId);
        if (!manifest.Success) return CoreResult<RuntimeNavigationResponse>.Fail(manifest.Error!);
        var result = await SendAsync<RuntimeNavigationResponse>(manifest.Value!, new(NewRequestId(), BridgeIpcMethods.Navigation, navigation: request), cancellationToken);
        if (policy is null) return result;
        if (result.Success) return policy.Sanitize(result.Value!);
        var error = policy.Sanitize(result.Error!);
        return CoreResult<RuntimeNavigationResponse>.Fail(error.Success ? error.Value! : error.Error!);
    }
}
