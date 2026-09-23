using AvaScope.Protocol;

namespace AvaScope.Core;

public sealed partial class LocalBridgeClient
{
    private CoreError? AuthorizeInputPreconditions(SessionId sessionId, string action, InputExecutionOptions? execution)
    {
        if (execution?.PreconditionPolicy is not { } settings) return null;
        var policy = new RuntimeEvidencePolicyEnforcer(settings);
        var session = policy.AuthorizeSession(this, sessionId);
        if (!session.Success) return session.Error;
        var authorization = policy.AuthorizeAction(action == InputActions.KeyText ? SemanticWorkflowActions.TypeText : action, null);
        return authorization.Success ? null : authorization.Error;
    }

    private static CoreResult<InputResponse> FinishInputPreconditions(CoreResult<InputResponse> result, InputExecutionOptions? execution, bool mayHaveDispatched)
    {
        if (execution?.Preconditions is null) return result;
        if (!result.Success && result.Error!.Code is CoreErrorCodes.BridgeIpcUnavailable or CoreErrorCodes.BridgeIpcFailed)
            result = CoreResult<InputResponse>.Fail(new(result.Error.Code, result.Error.Message,
                new Dictionary<string, string>(result.Error.Details ?? new Dictionary<string, string>())
                {
                    ["dispatched"] = mayHaveDispatched ? "unknown" : "false", ["preconditionsStatus"] = "response_unavailable",
                    ["dispatchOutcome"] = mayHaveDispatched ? "unknown" : "not_dispatched",
                    ["nextAction"] = "Inspect current application state or use the existing workflow idempotency key; never blindly redispatch after response loss."
                }));
        if (execution.PreconditionPolicy is not { } settings) return result;
        var policy = new RuntimeEvidencePolicyEnforcer(settings);
        if (result.Success) return policy.Sanitize(result.Value!);
        var safe = policy.Sanitize(result.Error!);
        return CoreResult<InputResponse>.Fail(safe.Success ? safe.Value! : safe.Error!);
    }

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
