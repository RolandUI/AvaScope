using AvaScope.Protocol;

namespace AvaScope.Core;

public static class OperationResultMapper
{
    public static ToolResult<T> ToToolResult<T>(CoreResult<T> result)
    {
        ArgumentNullException.ThrowIfNull(result);

        if (!result.Success)
        {
            return ToolResult<T>.Fail(ToProtocolError(result.Error!));
        }

        var outcomeError = GetOutcomeError(result.Value);
        return outcomeError is null
            ? ToolResult<T>.Ok(result.Value!)
            : ToolResult<T>.Fail(outcomeError, result.Value);
    }

    public static bool IsSuccessful<T>(CoreResult<T> result) =>
        ToToolResult(result).Success;

    public static ProtocolError? GetOutcomeError<T>(T? value)
    {
        return value switch
        {
            SemanticWorkflowResponse response when response.Status != "passed"
                => OutcomeError("workflow_failed", response.Status, response.Diagnostics),
            RuntimeScenarioResponse response when response.Status != "passed"
                => OutcomeError("scenario_failed", response.Status, response.Diagnostics),
            RuntimeMutationResponse response when response.Status is
                RuntimeMutationStatuses.Rejected or
                RuntimeMutationStatuses.Unsupported or
                RuntimeMutationStatuses.StaleTarget or
                RuntimeMutationStatuses.Unavailable
                => OutcomeError("mutation_failed", response.Status, response.Diagnostics),
            RuntimeMutationEvidenceResponse response when response.Mutation.Status is
                RuntimeMutationStatuses.Rejected or
                RuntimeMutationStatuses.Unsupported or
                RuntimeMutationStatuses.StaleTarget or
                RuntimeMutationStatuses.Unavailable
                => OutcomeError(
                    "mutation_failed",
                    response.Mutation.Status,
                    response.Diagnostics.Concat(response.Mutation.Diagnostics)),
            RuntimePointerDiagnosticsResponse response when response.Status == "failed"
                => OutcomeError("pointer_diagnostics_failed", response.Status, response.Diagnostics),
            RuntimePseudoStateMatrixResponse response when response.Status is "failed" or "unsupported"
                => OutcomeError("pseudo_state_matrix_failed", response.Status, response.Diagnostics),
            RuntimeInteractionAnimationResponse response when response.Status == "failed"
                => OutcomeError("interaction_animation_failed", response.Status, response.Diagnostics),
            CloseSessionResponse response when response.TerminateLaunchedProcessRequested
                && response.Outcome is CloseSessionOutcomes.NotOwned or CloseSessionOutcomes.TerminationFailed
                => CloseSessionOutcomeError(response),
            _ => null
        };
    }

    private static ProtocolError CloseSessionOutcomeError(CloseSessionResponse response)
    {
        var code = response.Outcome == CloseSessionOutcomes.NotOwned
            ? "launched_process_not_owned"
            : "launched_process_termination_failed";
        return new ProtocolError(
            code,
            response.TerminationMessage
                ?? $"The bridge session closed, but process termination completed with outcome '{response.Outcome}'.",
            new Dictionary<string, string>
            {
                ["outcome"] = response.Outcome,
                ["sessionClosed"] = "true",
                ["sessionId"] = response.Session.SessionId.Value,
                ["processId"] = response.ProcessId.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["launchedProcessOwned"] = response.LaunchedProcessOwned.ToString().ToLowerInvariant(),
                ["processTerminated"] = response.ProcessTerminated.ToString().ToLowerInvariant(),
                ["partialValueAvailable"] = "true"
            });
    }

    private static ProtocolError OutcomeError(
        string fallbackCode,
        string status,
        IEnumerable<ProtocolError> diagnostics)
    {
        var diagnostic = diagnostics.FirstOrDefault();
        var details = diagnostic?.Details is null
            ? new Dictionary<string, string>(StringComparer.Ordinal)
            : new Dictionary<string, string>(diagnostic.Details, StringComparer.Ordinal);
        details["status"] = status;
        details["partialValueAvailable"] = "true";
        return new ProtocolError(
            diagnostic?.Code ?? fallbackCode,
            diagnostic?.Message ?? $"The requested operation completed with status '{status}'.",
            details);
    }

    private static ProtocolError ToProtocolError(CoreError error) =>
        new(error.Code, error.Message, error.Details);
}
