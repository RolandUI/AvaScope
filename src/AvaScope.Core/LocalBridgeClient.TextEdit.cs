using AvaScope.Protocol;

namespace AvaScope.Core;

public sealed partial class LocalBridgeClient
{
    public async Task<CoreResult<RuntimeTextEditResponse>> EditTextAsync(RuntimeTextEditRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var policy = request.Policy is null ? null : new RuntimeEvidencePolicyEnforcer(request.Policy);
        if (policy is not null)
        {
            var session = policy.AuthorizeSession(this, request.Target.SessionId);
            if (!session.Success) return CoreResult<RuntimeTextEditResponse>.Fail(session.Error!);
            var action = policy.AuthorizeAction(SemanticWorkflowActions.Inspect, null);
            if (!action.Success) return CoreResult<RuntimeTextEditResponse>.Fail(action.Error!);
            if (request.Action != "read" && !request.Policy!.AllowedDesiredStates.Contains("text", StringComparer.Ordinal))
                return CoreResult<RuntimeTextEditResponse>.Fail(new("text_edit_policy_denied", "Selection and editing require the policy's explicit allowedDesiredStates: text permission."));
        }
        var manifest = FindSingleManifest(null, request.Target.SessionId);
        if (!manifest.Success) return CoreResult<RuntimeTextEditResponse>.Fail(manifest.Error!);
        var result = await SendAsync<RuntimeTextEditResponse>(manifest.Value!, new(NewRequestId(), BridgeIpcMethods.EditText, textEdit: request), cancellationToken);
        if (request.Action != "read" && !result.Success && result.Error!.Code is CoreErrorCodes.BridgeIpcUnavailable or CoreErrorCodes.BridgeIpcFailed)
            result = CoreResult<RuntimeTextEditResponse>.Fail(new(result.Error.Code, result.Error.Message,
                new Dictionary<string, string>(result.Error.Details ?? new Dictionary<string, string>())
                {
                    ["dispatched"] = "unknown", ["textEditRequestId"] = request.RequestId!,
                    ["nextAction"] = "Retrieve the retained outcome using the exact original edit payload and requestId in this session. Do not repeat the edit with a new id without observing current state."
                }));
        if (policy is null) return result;
        if (result.Success) return policy.Sanitize(result.Value!);
        var error = policy.Sanitize(result.Error!);
        return CoreResult<RuntimeTextEditResponse>.Fail(error.Success ? error.Value! : error.Error!);
    }
}
