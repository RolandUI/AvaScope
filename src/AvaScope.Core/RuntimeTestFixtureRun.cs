using System.Diagnostics;
using AvaScope.Protocol;

namespace AvaScope.Core;

internal sealed class RuntimeTestFixtureRun(LocalBridgeClient client, RuntimeScenarioRequest scenario,
    SessionId sessionId, string topLevelId, string outputDirectory, RuntimeEvidencePolicyEnforcer? policy)
{
    private readonly RuntimeScenarioFixtureOptions _options = scenario.TestFixture!;
    private RuntimeCustomActionDescriptor? _prepare;
    private RuntimeCustomActionDescriptor? _cleanup;
    private bool _cleanupNeeded;
    private CoreResult<bool>? _cleanupResult;
    public RuntimeTestFixtureEvidence Evidence { get; private set; } = new(scenario.TestFixture!.Name,
        scenario.TestFixture.ResourceId, scenario.TestFixture.Parameters.Keys.Order(StringComparer.Ordinal).ToArray(), DateTimeOffset.UtcNow);

    public async Task<CoreResult<bool>> PrepareAsync(CancellationToken cancellationToken)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(_options.TimeoutMs);
        var elapsed = Stopwatch.StartNew();
        try
        {
            if (policy?.AuthorizeSession(client, sessionId) is { Success: false } authorization)
                return Fail("authorization", authorization.Error!.Message);
            var nodes = await client.FindNodesAsync(sessionId, topLevelId, TreeKinds.Visual,
                automationId: _options.TargetAutomationId, maxDepth: Math.Min(scenario.MaxDepth, 32), maxResults: 2, cancellationToken: deadline.Token);
            if (!nodes.Success || nodes.Value!.Matches.Count != 1)
                return Fail("target", "The fixture target must resolve uniquely in the selected window and bounded search depth.");
            var target = nodes.Value.Matches[0].Node.Target!;
            var discovery = await client.CustomActionsAsync(sessionId, target, deadline.Token);
            if (!discovery.Success) return Fail("discovery", discovery.Error!.Message);
            var matches = discovery.Value!.Actions.Where(action => action.TestFixture?.Name == _options.Name
                && action.Name == action.TestFixture.PrepareAction).ToArray();
            if (matches.Length != 1) return Fail("unknown", "The selected target does not declare one matching fixture preparation action.");
            _prepare = matches[0];
            var fixture = _prepare.TestFixture!;
            Evidence = Evidence with { Version = fixture.Version, CleanupStatus = fixture.HasCleanup ? "not_started" : "not_required" };
            if (!fixture.ResourceIds.Contains(_options.ResourceId, StringComparer.Ordinal))
                return Fail("resource_disallowed", "The selected resource identity is not declared by this host fixture.");
            if (!_prepare.Executable) return Fail("unavailable", "The host fixture is not currently executable.");
            if (fixture.HasCleanup)
            {
                _cleanup = discovery.Value.Actions.SingleOrDefault(action => action.Name == fixture.CleanupAction
                    && action.TestFixture?.Name == fixture.Name && action.TestFixture.Version == fixture.Version
                    && action.TestFixture.ResourceIds.Contains(_options.ResourceId, StringComparer.Ordinal));
                if (_cleanup is null || !_cleanup.Executable) return Fail("cleanup_unavailable", "The required fixture cleanup action is not available; preparation was not dispatched.");
            }
            foreach (var action in new[] { _prepare, _cleanup }.Where(action => action is not null))
            {
                if (policy?.AuthorizeAction(SemanticWorkflowActions.CustomAction, action!.Name) is { Success: false } denied)
                    return Fail("authorization", denied.Error!.Message);
                if (action!.SafetyClassification == RuntimeCustomActionSafetyClassifications.Destructive
                    && (!scenario.AllowDestructive || policy?.AllowsDestructiveAction(scenario.AllowDestructive, false) == false))
                    return Fail("authorization", "Fixture actions marked destructive require explicit scenario authorization.");
            }
            // Validate host-supplied readiness before dispatching any fixture handler.
            var readiness = new SemanticWorkflowRequest(sessionId, topLevelId,
                [new SemanticWorkflowStep(SemanticWorkflowActions.WaitForState, "fixture-readiness", selector: fixture.ReadinessSelector,
                    waitCondition: fixture.Readiness, timeoutMs: _options.TimeoutMs)], scenario.RequestId,
                outputDirectory: outputDirectory, timeoutMs: _options.TimeoutMs,
                evidence: new SemanticWorkflowEvidenceOptions(captureOnFailure: false, exportReports: false, policy: scenario.Evidence?.Policy));
            if (!SemanticWorkflowCompiler.Compile(readiness).Plan.Valid)
                return Fail("readiness_invalid", "The host fixture readiness condition is invalid; preparation was not dispatched.");
            var parameters = new Dictionary<string, string>(_options.Parameters, StringComparer.Ordinal) { ["testResource"] = _options.ResourceId };
            if (Audit(_prepare, "dispatch_requested") is { Success: false } beforeAudit)
                return Fail("audit_failed", beforeAudit.Error!.Message);
            _cleanupNeeded = _cleanup is not null;
            Evidence = Evidence with { PreparationStatus = "dispatching" };
            var result = await client.InvokeCustomActionAsync(sessionId, new RuntimeCustomActionRequest(
                scenario.RequestId + ":fixture-prepare", _prepare.Target, _prepare.Name, parameters, scenario.AllowDestructive, fixture.Version), deadline.Token);
            if (!result.Success) return Fail("preparation_failed", "The fixture preparation response is unavailable; its outcome may be unknown.");
            if (!result.Value!.Executed)
            {
                _cleanupNeeded = false;
                Evidence = Evidence with { PreparationStatus = "rejected" };
            }
            else Evidence = Evidence with { PreparationStatus = result.Value.Status == RuntimeCustomActionStatuses.Executed ? "prepared" : "failed" };
            if (Audit(_prepare, result.Value.Status, result.Value) is { Success: false } afterAudit)
                return Fail("audit_failed", afterAudit.Error!.Message);
            if (result.Value.Status != RuntimeCustomActionStatuses.Executed)
            {
                if (result.Value.Executed) Evidence = Evidence with { PreparationStatus = "failed" };
                return Fail("preparation_failed", "The host rejected or failed fixture preparation.");
            }
            Evidence = Evidence with { PreparationStatus = "prepared", ReadinessStatus = "waiting" };
            var remaining = Math.Max(1, _options.TimeoutMs - (int)elapsed.ElapsedMilliseconds);
            var probe = await new SemanticWorkflowRunner().RunAsync(client, new SemanticWorkflowRequest(sessionId, topLevelId,
                [new SemanticWorkflowStep(SemanticWorkflowActions.WaitForState, "fixture-readiness", selector: fixture.ReadinessSelector,
                    waitCondition: fixture.Readiness, timeoutMs: remaining)], scenario.RequestId, outputDirectory: outputDirectory,
                timeoutMs: remaining, evidence: readiness.Evidence), deadline.Token);
            var observation = probe.Value?.Steps.LastOrDefault()?.WaitObservation;
            Evidence = Evidence with { ReadinessObservation = observation, ReadinessStatus = probe.Success && probe.Value!.Status == "passed" ? "ready" : "failed" };
            return Evidence.ReadinessStatus == "ready" ? CoreResult<bool>.Ok(true) : Fail("readiness_failed", "The fixture did not reach its declared readiness/postcondition.");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            Evidence = Evidence with { ReadinessStatus = Evidence.PreparationStatus == "prepared" ? "timed_out" : Evidence.ReadinessStatus };
            return Fail("timeout", "Fixture preparation/readiness exceeded the shared deadline; any dispatched handler may still require cleanup.");
        }
        catch (OperationCanceledException)
        {
            Evidence = Evidence with { PreparationStatus = Evidence.PreparationStatus == "prepared" ? "prepared" : "cancelled_or_unknown", ReadinessStatus = "cancelled" };
            throw;
        }
    }

    public async Task<CoreResult<bool>> CleanupAsync()
    {
        if (_cleanupResult is not null) return _cleanupResult;
        if (!_cleanupNeeded || _cleanup is null)
        {
            Evidence = Evidence with { CleanupStatus = Evidence.CleanupStatus == "not_required" ? "not_required" : "not_needed", CompletedAt = DateTimeOffset.UtcNow };
            return _cleanupResult = CoreResult<bool>.Ok(true);
        }
        _cleanupNeeded = false;
        using var deadline = new CancellationTokenSource(_options.CleanupTimeoutMs);
        try
        {
            var result = await client.InvokeCustomActionAsync(sessionId, new RuntimeCustomActionRequest(
                scenario.RequestId + ":fixture-cleanup", _cleanup.Target, _cleanup.Name,
                new Dictionary<string, string> { ["testResource"] = _options.ResourceId }, scenario.AllowDestructive, _cleanup.TestFixture!.Version), deadline.Token);
            var passed = result.Success && result.Value!.Status == RuntimeCustomActionStatuses.Executed;
            Evidence = Evidence with { CleanupStatus = passed ? "cleaned" : "failed", CompletedAt = DateTimeOffset.UtcNow };
            if (Audit(_cleanup, result.Value?.Status ?? "unknown", result.Value) is { Success: false } audit)
                return _cleanupResult = Fail("audit_failed", audit.Error!.Message);
            return _cleanupResult = passed ? CoreResult<bool>.Ok(true) : Fail("cleanup_failed", "Required host fixture cleanup failed or its result is unavailable.");
        }
        catch (OperationCanceledException)
        {
            Evidence = Evidence with { CleanupStatus = "timed_out_or_unknown", CompletedAt = DateTimeOffset.UtcNow };
            return _cleanupResult = Fail("cleanup_timeout", "Required host fixture cleanup exceeded its independent deadline; no successful cleanup is claimed.");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException or ArgumentException)
        {
            Evidence = Evidence with { CleanupStatus = "failed_or_unknown", CompletedAt = DateTimeOffset.UtcNow };
            return _cleanupResult = Fail("cleanup_failed", "The fixture cleanup transport failed; owned process/environment cleanup will still be attempted.");
        }
    }

    private CoreResult<bool> Fail(string code, string message)
    {
        if (Evidence.PreparationStatus is "not_started" or "dispatching")
            Evidence = Evidence with { PreparationStatus = Evidence.PreparationStatus == "dispatching" ? "failed_or_unknown" : "rejected" };
        return CoreResult<bool>.Fail(new CoreError("runtime_fixture_" + code, message));
    }

    private CoreResult<bool> Audit(RuntimeCustomActionDescriptor action, string status, RuntimeCustomActionResponse? response = null)
    {
        if (policy is null) return CoreResult<bool>.Ok(true);
        var request = new SemanticWorkflowRequest(sessionId, topLevelId,
            [new SemanticWorkflowStep(SemanticWorkflowActions.CustomAction, action.Name)], scenario.RequestId, outputDirectory: outputDirectory);
        return policy.AppendActionAudit(request, new SemanticWorkflowStepResult(action.Name, SemanticWorkflowActions.CustomAction,
            status, "Host-owned test fixture operation.", DateTimeOffset.UtcNow, action.Target, customAction: response));
    }
}
