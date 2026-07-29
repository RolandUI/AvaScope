using AvaScope.Core;
using AvaScope.Protocol;

namespace AvaScope.Tests.Core;

public sealed class OperationResultMapperTests
{
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
