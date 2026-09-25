using AvaScope.Core;
using AvaScope.Protocol;

namespace AvaScope.Tests.Core;

public sealed class OperationResultMapperTests
{
    [Theory]
    [InlineData("failed", "pseudo_state_not_observed")]
    [InlineData("unsupported", "input_strategy_unsupported")]
    [InlineData("failed", null)]
    [InlineData("passed", null)]
    public void PseudoStateOutcomePreservesCausalFailureInsteadOfTargetAdvisories(string status, string? failureCode)
    {
        var sessionId = new SessionId("matrix-diagnostics");
        var advisory = new ProtocolError("pseudo_state_raw_node_id_generation_scoped", "Target uses a generation-scoped node id.");
        var resolved = new ProtocolError("pseudo_state_target_reresolved", "Target was re-resolved.");
        var failure = failureCode is null ? null : new ProtocolError(failureCode, "Requested state could not be observed.",
            new Dictionary<string, string> { ["state"] = "pointerover", ["expectedClass"] = ":pointerover" });
        var diagnostics = failure is null ? new[] { advisory, resolved } : new[] { advisory, resolved, failure };
        var response = new RuntimePseudoStateMatrixResponse(
            "matrix-diagnostics", sessionId, "topLevel:main",
            new RuntimeTargetContext(sessionId, "topLevel:main", TreeKinds.Visual, "visual:target"),
            status, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow,
            [
                new RuntimePseudoStateMatrixEntry("normal", "Normal", "passed", "Captured.", DateTimeOffset.UtcNow, diagnostics: [advisory]),
                new RuntimePseudoStateMatrixEntry("pointerover", "Hover", status, failure?.Message ?? status, DateTimeOffset.UtcNow, diagnostics: diagnostics)
            ], diagnostics: diagnostics);

        var result = OperationResultMapper.ToToolResult(CoreResult<RuntimePseudoStateMatrixResponse>.Ok(response));

        Assert.True(result.TransportSuccess);
        Assert.Same(response, result.Value);
        Assert.Equal(diagnostics, result.Value!.Diagnostics);
        Assert.Equal(status == "passed", result.Success);
        if (status == "passed")
        {
            Assert.Null(result.Error);
        }
        else
        {
            Assert.Equal(failureCode ?? "pseudo_state_matrix_failed", result.Error!.Code);
            Assert.Equal(status, result.Error.Details!["status"]);
            Assert.Equal("true", result.Error.Details["partialValueAvailable"]);
            if (failure is not null)
            {
                Assert.Equal(failure.Message, result.Error.Message);
                Assert.Equal(":pointerover", result.Error.Details["expectedClass"]);
            }
        }
    }

    [Fact]
    public void ValidatedWorkflowIsSuccessfulWithoutRuntimeDispatch()
    {
        var response = new SemanticWorkflowResponse(
            "validate-only",
            new SessionId("validate-only"),
            "topLevel:diagnostic",
            "validated",
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow,
            [],
            plan: new SemanticWorkflowPlan(true, 1, 1, 0, 0));

        var result = OperationResultMapper.ToToolResult(CoreResult<SemanticWorkflowResponse>.Ok(response));

        Assert.True(result.Success);
        Assert.Equal("validated", result.Value!.Status);
    }

    [Theory]
    [InlineData(CloseSessionOutcomes.NotOwned, "launched_process_not_owned")]
    [InlineData(CloseSessionOutcomes.TerminationFailed, "launched_process_termination_failed")]
    public void RequestedTerminationPartialFailurePreservesClosedSession(
        string outcome,
        string expectedErrorCode)
    {
        var response = CreateCloseResponse(outcome);

        var result = OperationResultMapper.ToToolResult(CoreResult<CloseSessionResponse>.Ok(response));

        Assert.False(result.Success);
        Assert.True(result.TransportSuccess);
        Assert.Same(response, result.Value);
        Assert.Equal(SessionStates.Closed, result.Value!.Session.State);
        Assert.Equal(expectedErrorCode, result.Error!.Code);
        Assert.Equal("true", result.Error.Details!["sessionClosed"]);
        Assert.Equal("true", result.Error.Details["partialValueAvailable"]);
    }

    [Theory]
    [InlineData(CloseSessionOutcomes.ClosedOnly)]
    [InlineData(CloseSessionOutcomes.Terminated)]
    [InlineData(CloseSessionOutcomes.AlreadyExited)]
    public void SuccessfulCloseOutcomeRemainsSuccessful(string outcome)
    {
        var response = CreateCloseResponse(outcome);

        var result = OperationResultMapper.ToToolResult(CoreResult<CloseSessionResponse>.Ok(response));

        Assert.True(result.Success);
        Assert.Same(response, result.Value);
        Assert.Null(result.Error);
    }

    [Fact]
    public void FailedWorkflowRetainsTimelineStepsAndArtifacts()
    {
        var sessionId = new SessionId("partial-workflow");
        var screenshot = new ScreenshotResponse(
            sessionId,
            "topLevel:main",
            "C:\\artifacts\\step.png",
            100,
            50,
            DateTimeOffset.UtcNow);
        var diagnostic = new ProtocolError("assertion_failed", "Expected state was not reached.");
        var workflow = new SemanticWorkflowResponse(
            "workflow-1",
            sessionId,
            "topLevel:main",
            "failed",
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow,
            [
                new SemanticWorkflowStepResult(
                    "capture",
                    SemanticWorkflowActions.Screenshot,
                    "passed",
                    "Captured.",
                    DateTimeOffset.UtcNow,
                    screenshot: screenshot),
                new SemanticWorkflowStepResult(
                    "assert",
                    SemanticWorkflowActions.AssertState,
                    "failed",
                    diagnostic.Message,
                    DateTimeOffset.UtcNow,
                    diagnostics: [diagnostic])
            ],
            diagnostics: [diagnostic]);

        var result = OperationResultMapper.ToToolResult(
            CoreResult<SemanticWorkflowResponse>.Ok(workflow));

        Assert.False(result.Success);
        Assert.Same(workflow, result.Value);
        Assert.Equal("C:\\artifacts\\step.png", result.Value!.Steps[0].Screenshot!.FilePath);
        Assert.Equal("assertion_failed", result.Error!.Code);
        Assert.Equal("true", result.Error.Details!["partialValueAvailable"]);
    }

    private static CloseSessionResponse CreateCloseResponse(string outcome)
    {
        var sessionId = new SessionId("close-partial");
        return new CloseSessionResponse(
            new SessionSummary(
                sessionId,
                SessionKinds.Runtime,
                SessionStates.Closed,
                DateTimeOffset.UtcNow,
                "App"),
            42,
            DateTimeOffset.UtcNow,
            terminateLaunchedProcessRequested: true,
            outcome,
            launchedProcessOwned: outcome != CloseSessionOutcomes.NotOwned,
            processTerminated: outcome == CloseSessionOutcomes.Terminated,
            terminationMessage: outcome is CloseSessionOutcomes.NotOwned or CloseSessionOutcomes.TerminationFailed
                ? "Termination did not complete."
                : null);
    }
}
