using AvaScope.Protocol;

namespace AvaScope.Core;

public sealed partial class LocalBridgeClient
{
    public async Task<CoreResult<RuntimeFocusSnapshot>> InspectFocusAsync(RuntimeFocusInspectionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var policy = request.Policy is null ? null : new RuntimeEvidencePolicyEnforcer(request.Policy);
        if (policy is not null)
        {
            var session = policy.AuthorizeSession(this, request.SessionId);
            if (!session.Success) return CoreResult<RuntimeFocusSnapshot>.Fail(session.Error!);
            var action = policy.AuthorizeAction(SemanticWorkflowActions.Inspect, null);
            if (!action.Success) return CoreResult<RuntimeFocusSnapshot>.Fail(action.Error!);
        }
        var manifest = FindSingleManifest(null, request.SessionId);
        if (!manifest.Success) return CoreResult<RuntimeFocusSnapshot>.Fail(manifest.Error!);
        var result = await SendAsync<RuntimeFocusSnapshot>(manifest.Value!,
            new BridgeIpcRequest(NewRequestId(), BridgeIpcMethods.InspectFocus, focusInspection: request), cancellationToken);
        if (policy is null) return result;
        if (result.Success) return policy.Sanitize(result.Value!);
        var error = policy.Sanitize(result.Error!);
        return CoreResult<RuntimeFocusSnapshot>.Fail(error.Success ? error.Value! : error.Error!);
    }

    public async Task<CoreResult<RuntimeFocusProbeResponse>> ProbeFocusAsync(RuntimeFocusProbeRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var policy = request.Policy is null ? null : new RuntimeEvidencePolicyEnforcer(request.Policy);
        if (policy is not null)
        {
            var session = policy.AuthorizeSession(this, request.Target.SessionId);
            if (!session.Success) return CoreResult<RuntimeFocusProbeResponse>.Fail(session.Error!);
            var action = policy.AuthorizeAction(InputActions.KeySequence, null);
            if (!action.Success) return CoreResult<RuntimeFocusProbeResponse>.Fail(action.Error!);
        }
        var manifest = FindSingleManifest(null, request.Target.SessionId);
        if (!manifest.Success) return CoreResult<RuntimeFocusProbeResponse>.Fail(manifest.Error!);
        var result = await SendAsync<RuntimeFocusProbeResponse>(manifest.Value!,
            new BridgeIpcRequest(NewRequestId(), BridgeIpcMethods.ProbeFocus, focusProbe: request), cancellationToken);
        if (!result.Success && result.Error?.Code is CoreErrorCodes.BridgeIpcUnavailable or CoreErrorCodes.BridgeIpcFailed)
            result = CoreResult<RuntimeFocusProbeResponse>.Fail(new(result.Error.Code, result.Error.Message,
                new Dictionary<string, string>(result.Error.Details ?? new Dictionary<string, string>())
                { ["dispatchOutcome"] = "unknown", ["nextAction"] = "Inspect focus and application state before choosing another action; this probe must not be automatically replayed." }));
        if (policy is null) return result;
        if (result.Success) return policy.Sanitize(result.Value!);
        var error = policy.Sanitize(result.Error!);
        return CoreResult<RuntimeFocusProbeResponse>.Fail(error.Success ? error.Value! : error.Error!);
    }
}
