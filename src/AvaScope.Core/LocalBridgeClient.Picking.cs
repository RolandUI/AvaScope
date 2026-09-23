using AvaScope.Protocol;

namespace AvaScope.Core;

public sealed partial class LocalBridgeClient
{
    public async Task<CoreResult<RuntimePickResponse>> PickNodeAsync(RuntimePickRequest request, CancellationToken cancellationToken = default)
    {
        if (AuthorizeInspection(request.Target.SessionId, request.Policy) is { } error) return CoreResult<RuntimePickResponse>.Fail(error);
        var manifest = FindSingleManifest(null, request.Target.SessionId);
        if (!manifest.Success) return CoreResult<RuntimePickResponse>.Fail(manifest.Error!);
        var result = await SendAsync<RuntimePickResponse>(manifest.Value!, new(NewRequestId(), BridgeIpcMethods.PickNode, pick: request), cancellationToken);
        return result.Success && request.Policy is { } policy ? new RuntimeEvidencePolicyEnforcer(policy).Sanitize(result.Value!) : result;
    }

    public async Task<CoreResult<RuntimeHighlightResponse>> HighlightAsync(RuntimeHighlightRequest request, CancellationToken cancellationToken = default)
    {
        if (AuthorizeInspection(request.Target.SessionId, request.Policy) is { } error) return CoreResult<RuntimeHighlightResponse>.Fail(error);
        var manifest = FindSingleManifest(null, request.Target.SessionId);
        if (!manifest.Success) return CoreResult<RuntimeHighlightResponse>.Fail(manifest.Error!);
        var result = await SendAsync<RuntimeHighlightResponse>(manifest.Value!, new(NewRequestId(), BridgeIpcMethods.Highlight, highlight: request), cancellationToken);
        return result.Success && request.Policy is { } policy ? new RuntimeEvidencePolicyEnforcer(policy).Sanitize(result.Value!) : result;
    }

    private CoreError? AuthorizeInspection(SessionId sessionId, RuntimeEvidencePolicy? policy)
    {
        if (policy is null) return null;
        var enforcer = new RuntimeEvidencePolicyEnforcer(policy);
        return enforcer.AuthorizeSession(this, sessionId).Error ?? enforcer.AuthorizeAction(SemanticWorkflowActions.Inspect, null).Error;
    }
}
