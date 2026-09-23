using System.Diagnostics;
using System.Text.Json;
using Avalonia.Threading;
using AvaScope.Core;
using AvaScope.Protocol;

namespace AvaScope.Bridge;

public sealed partial class AvaScopeBridgeRuntime
{
    public async Task<CoreResult<RuntimeExpressionResponse>> EvaluateRuntimeAsync(RuntimeExpressionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return await Dispatcher.UIThread.InvokeAsync(() => EvaluateRuntime(request), DispatcherPriority.Background, cancellationToken);
    }

    private CoreResult<RuntimeExpressionResponse> EvaluateRuntime(RuntimeExpressionRequest request)
    {
        if (request.SessionId != SessionId) return CoreResult<RuntimeExpressionResponse>.Fail(new("expression_session_mismatch", "Select this bridge session."));
        var policy = request.Policy is null ? null : new RuntimeEvidencePolicyEnforcer(request.Policy);
        if (policy?.AuthorizeAction(SemanticWorkflowActions.Inspect, null) is { Success: false } denied)
            return CoreResult<RuntimeExpressionResponse>.Fail(denied.Error!);
        var top = FindTopLevel(request.TopLevelId);
        if (top is null) return TopLevelNotFound<RuntimeExpressionResponse>(request.TopLevelId);
        var started = DateTimeOffset.UtcNow;
        var timer = Stopwatch.StartNew();
        var sources = request.Definition.Sources.Select(Collect).ToArray();
        // Re-read every dependency in the same dispatcher turn. This is sampled
        // consistency, not an atomic application transaction or a global dataset claim.
        var changed = request.Definition.Sources.Select(Collect).Zip(sources).Any(pair => Fingerprint(pair.First) != Fingerprint(pair.Second));
        changed |= FindTopLevel(request.TopLevelId) != top;
        if (changed)
            sources = sources.Select(source => source with { Coverage = source.Coverage with
                { Complete = false, Reasons = source.Coverage.Reasons.Append("observation_changed").Distinct().ToArray() } }).ToArray();
        var result = RuntimeExpressionEvaluator.Evaluate(request.Definition, sources);
        if (request.RequireTrue && result.Status == "available" && result.Type != "boolean")
            result = result with { Status = "indeterminate", Value = null, Reason = "assertion_requires_boolean" };
        var status = result.Status != "available" ? "indeterminate" : !request.RequireTrue ? "observed" : result.Value!.Value.GetBoolean() ? "passed" : "failed";
        var diagnostics = status is "failed" or "indeterminate"
            ? new[] { new ProtocolError("runtime_expression_" + status, "Inspect result operands and source coverage for exact failing or unavailable evidence.") } : [];
        var response = new RuntimeExpressionResponse(SessionId, request.TopLevelId, status, result, sources, started, DateTimeOffset.UtcNow,
            changed ? "changed_between_samples" : "two_samples_one_dispatcher_turn", diagnostics);
        var safe = policy is null ? CoreResult<RuntimeExpressionResponse>.Ok(response) : policy.Sanitize(response);
        if (safe.Success && JsonSerializer.SerializeToUtf8Bytes(safe.Value).Length > 131072)
            return CoreResult<RuntimeExpressionResponse>.Fail(new("expression_response_limit", "Expression evidence exceeded 128 KiB. Narrow selectors or reduce source/result limits; no passing result is available."));
        return safe;

        RuntimeExpressionSourceObservation Collect(RuntimeExpressionSource source)
        {
            if (timer.Elapsed > TimeSpan.FromSeconds(2)) return Unavailable(source, "analysis_budget");
            try
            {
                var query = QueryNodes(new(SessionId, request.TopLevelId, source.Selector, [source.Attribute],
                    request.Definition.MaxResults, request.Definition.MaxNodes, request.Definition.MaxDepth, request.Policy), requireCollectionCoverage: true);
                if (!query.Success) return Unavailable(source, "query_unavailable");
                var value = query.Value!;
                var coverage = value.Coverage ?? new RuntimeQueryCoverage(false, 0, 0, ["coverage_unavailable"]);
                if (value.ResponseBudget?.Truncated == true || value.Projections.Count != coverage.MatchedAtLeast)
                    coverage = coverage with { Complete = false, Reasons = coverage.Reasons.Append("projection_incomplete").Distinct().ToArray() };
                if (timer.Elapsed > TimeSpan.FromSeconds(2)) coverage = coverage with { Complete = false, Reasons = [.. coverage.Reasons, "analysis_budget"] };
                return new(source.Id, source.Selector, source.Attribute, coverage,
                    value.Projections.Select(item => new RuntimeExpressionValue(
                        new(item.Target.SessionId, item.Target.TopLevelId, item.Target.TreeKind, item.Target.NodeId,
                            targetKind: item.Target.TargetKind, topLevelGeneration: item.Target.TopLevelGeneration, nodeGeneration: item.Target.NodeGeneration),
                        item.Attributes.Single())).ToArray());
            }
            catch (Exception exception) when (exception is not OutOfMemoryException and not AccessViolationException)
            { return Unavailable(source, "query_provider_unavailable"); }
        }
        static RuntimeExpressionSourceObservation Unavailable(RuntimeExpressionSource source, string reason) =>
            new(source.Id, source.Selector, source.Attribute, new(false, 0, 0, [reason]), []);
        static string Fingerprint(RuntimeExpressionSourceObservation source) => JsonSerializer.Serialize(new
        { source.Coverage, Values = source.Values.Select(value => new { value.Target.NodeId, value.Target.NodeGeneration, value.Target.TopLevelGeneration, value.Observation }) });
    }
}
