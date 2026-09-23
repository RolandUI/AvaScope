using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;
using AvaScope.Core;
using AvaScope.Protocol;

namespace AvaScope.Bridge;

public sealed partial class AvaScopeBridgeRuntime
{
    private readonly object _navigationLock = new();
    private readonly Dictionary<string, NavigationRun> _navigationRuns = new(StringComparer.Ordinal);
    private Timer? _navigationExpiry;
    private bool _navigationClosed;

    public async Task<CoreResult<RuntimeNavigationResponse>> NavigationAsync(RuntimeNavigationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(4000);
        try { return await Dispatcher.UIThread.InvokeAsync(() => Navigation(request), DispatcherPriority.Background, deadline.Token); }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        { return CoreResult<RuntimeNavigationResponse>.Fail(new("navigation_timeout", "The observation dispatcher did not respond within four seconds; query the run before recording again.")); }
    }

    private CoreResult<RuntimeNavigationResponse> Navigation(RuntimeNavigationRequest request)
    {
        Dispatcher.UIThread.VerifyAccess();
        if (request.SessionId != SessionId) return Fail("navigation_session_mismatch", "Navigation history belongs to its original bridge session; start a new run after app restart.");
        if (JsonSerializer.SerializeToUtf8Bytes(request).Length > 65536) return Fail("navigation_request_limit", "Navigation requests are limited to 64 KiB.");
        var policy = request.Policy is null ? null : new RuntimeEvidencePolicyEnforcer(request.Policy);
        if (policy?.AuthorizeAction(SemanticWorkflowActions.Inspect, null) is { Success: false } denied)
            return CoreResult<RuntimeNavigationResponse>.Fail(denied.Error!);
        if (request.Policy is { } authorization && (authorization.AuthorizedSessionIds.Count > 0 && !authorization.AuthorizedSessionIds.Contains(SessionId.Value, StringComparer.Ordinal)
            || authorization.AuthorizedProcessIds.Count > 0 && !authorization.AuthorizedProcessIds.Contains(Environment.ProcessId)))
            return Fail("navigation_policy_denied", "The current session/process is outside the evidence policy.");
        var policyKey = NavigationHash(request.Policy);
        NavigationRun run;
        lock (_navigationLock)
        {
            ExpireNavigationRuns();
            if (_navigationClosed) return Fail("navigation_closed", "The bridge navigation journal has been closed.");
            if (request.Action == "start")
            {
                if (_navigationRuns.Count >= 8) return Fail("navigation_run_limit", "Eight navigation runs are retained; clear a finished run or wait for its expiry.");
                run = new(request, policyKey);
            }
            else
            {
                if (!_navigationRuns.TryGetValue(request.RunId!, out run!))
                    return Fail("navigation_run_unavailable", "The run expired, was cleared, or belongs to a prior application session.");
                if (run.PolicyKey != policyKey) return Fail("navigation_policy_changed", "Retained evidence can only be read with the exact original policy. Start a new run to change it.");
                if (request.Action == "clear")
                {
                    _navigationRuns.Remove(run.Id);
                    return CoreResult<RuntimeNavigationResponse>.Ok(new(run.Id, SessionId, run.ExpiresAt, "cleared", null, [], [], null, 0, run.Dropped, 0, false, []));
                }
                if (request.Action == "record" && run.Visits[^1].Visit.VisitId != request.PreviousVisitId)
                    return Fail("navigation_tip_changed", "The previous visit is not the current run tip. Query the latest visit before recording; no route was inferred.");
                if (request.Action == "query") return CoreResult<RuntimeNavigationResponse>.Ok(NavigationResponse(run, request));
            }
        }

        try
        {
            var captured = CaptureNavigationVisit(run, request, policy);
            var bytes = JsonSerializer.SerializeToUtf8Bytes(captured).Length;
            if (bytes > 65536) return Fail("navigation_observation_limit", "The visit exceeds 64 KiB. Start a smaller observation scope; no incomplete evidence was retained.");
            lock (_navigationLock)
            {
                ExpireNavigationRuns();
                if (_navigationClosed || run.Expired) return Fail("navigation_run_unavailable", "The run or session expired while the observation was being collected.");
                if (request.Action == "start")
                {
                    if (_navigationRuns.Count >= 8) return Fail("navigation_run_limit", "The run limit was reached during collection.");
                }
                else if (!_navigationRuns.TryGetValue(run.Id, out var retained) || retained != run || run.Visits[^1].Visit.VisitId != request.PreviousVisitId)
                    return Fail("navigation_tip_changed", "The run was cleared or changed during collection; no visit or action was appended.");
                var previous = run.Visits.LastOrDefault()?.Visit;
                captured = captured with
                {
                    Sequence = run.Sequence + 1,
                    ArrivedVia = previous is not null && request.Transition is { } action ? new(previous.VisitId, captured.VisitId,
                        new(policy?.SanitizeScalar(action.Description) ?? action.Description, action.Outcome,
                            action.RequestId is null ? null : policy?.SanitizeScalar(action.RequestId) ?? action.RequestId),
                        previous.ObservationId, captured.ObservationId) : null
                };
                bytes = JsonSerializer.SerializeToUtf8Bytes(captured).Length;
                if (bytes > 65536) return Fail("navigation_observation_limit", "The visit plus transition exceeds 64 KiB; no evidence was retained.");
                if (request.Action == "start")
                {
                    _navigationRuns.Add(run.Id, run);
                    _navigationExpiry ??= new Timer(_ => { lock (_navigationLock) ExpireNavigationRuns(); }, null, 1000, 1000);
                }
                run.Sequence++;
                run.Visits.Add(new(captured, bytes)); run.Bytes += bytes;
                while (run.Visits.Count > 128 || run.Bytes > 512 * 1024)
                {
                    run.Bytes -= run.Visits[0].Bytes; run.Visits.RemoveAt(0); run.Dropped++;
                }
                return CoreResult<RuntimeNavigationResponse>.Ok(NavigationResponse(run, request));
            }
        }
        catch (NavigationStop exception) { return Fail(exception.Code, exception.Message); }
        catch (Exception exception) when (exception is not OutOfMemoryException and not AccessViolationException)
        { return Fail("navigation_capture_failed", "The host observation/identity provider failed; no private exception text was retained."); }

        static CoreResult<RuntimeNavigationResponse> Fail(string code, string message) => CoreResult<RuntimeNavigationResponse>.Fail(new(code, message));
    }

    private RuntimeNavigationVisit CaptureNavigationVisit(NavigationRun run, RuntimeNavigationRequest request, RuntimeEvidencePolicyEnforcer? policy)
    {
        var started = Stopwatch.GetTimestamp();
        var options = new RuntimeObservationRequest(SessionId, run.TopLevelIds, maxTopLevels: 4, maxNodes: run.MaxNodes, maxDepth: run.MaxDepth,
            includeDiagnostics: true, outputDirectory: request.Policy?.OwnedEvidenceRoot, policy: request.Policy);
        var unavailable = new HashSet<string>(StringComparer.Ordinal);
        var first = Sample();
        var second = Sample();
        if (Stopwatch.GetElapsedTime(started).TotalSeconds > 2) throw new NavigationStop("navigation_budget", "The cooperative two-second observation budget expired.");
        var changed = first.Fingerprint != second.Fingerprint || first.Identity != second.Identity || first.IdentityContext != second.IdentityContext;
        if (changed) unavailable.Add("changed_during_collection");
        var snapshot = second.Snapshot with { Consistency = changed ? "changed_during_collection" : "no_sampled_change_detected", ChangedDuringCollection = changed };
        if (snapshot.Truncated) unavailable.Add("partial_tree_sample");
        if (snapshot.Windows.Any(window => window.Window is null || window.Parts.Values.Any(value => value is "unavailable" or "partial"))) unavailable.Add("unavailable_window_parts");
        if (request.Policy is not null && (request.Policy.RedactedText.Count > 0 || request.Policy.RedactedAutomationIds.Count > 0 || request.Policy.ExcludedControlAutomationIds.Count > 0))
            unavailable.Add("policy_limited_evidence");
        var id = Guid.NewGuid().ToString("N");
        // Even a matching key never merges visits. Without explicit host identity the key is unique.
        var identity = changed ? null : second.Identity;
        var stateKey = NavigationHash(new { SessionId, Identity = identity, Context = second.IdentityContext,
            second.Fingerprint, UniqueVisit = identity is null ? id : null });
        return new(id, 0, stateKey, request.VisitLabel is null ? null : policy?.SanitizeScalar(request.VisitLabel) ?? request.VisitLabel,
            identity, identity is null ? "unique_visit; sampled_matches_are_uncertain" : "host_declared_identity_and_sample; hidden_state_unverified",
            snapshot.CompletedAt, snapshot.ObservationId, second.Fingerprint,
            unavailable.Count == 0 ? "sampled" : "partial", null, snapshot, unavailable.ToArray());

        (RuntimeObservationResponse Snapshot, string Fingerprint, RuntimeNavigationIdentity? Identity, string? IdentityContext) Sample()
        {
            RuntimeNavigationIdentity? identity = null;
            string? context = null;
            if (request.IdentityTarget is { } target)
            {
                var top = FindTopLevel(target.TopLevelId);
                if (top is null || !ResolveInputGenerationTarget(top, target.TopLevelId, target.NodeId, target).Success
                    || FindNodeById(top, target.NodeId!) is not Visual visual)
                    throw new NavigationStop("navigation_identity_stale", "Resolve a fresh identity-provider node in this session.");
                var ancestors = visual.GetSelfAndVisualAncestors().Take(65).ToArray();
                if (ancestors.Length > 64 || request.Policy is { } privacy && ancestors.Any(node => privacy.ExcludedControlAutomationIds.Contains(GetAutomationId(node), StringComparer.Ordinal)
                    || privacy.RedactedAutomationIds.Contains(GetAutomationId(node), StringComparer.Ordinal)))
                    unavailable.Add("identity_excluded_by_policy");
                else
                {
                    context = QueryIdentity(visual, false);
                    var fields = GetDebugState(visual);
                    if (fields.Status == "available" && !fields.Truncated
                        && fields.Fields.TryGetValue("navigation.surface", out var surface)
                        && fields.Fields.TryGetValue("navigation.context", out var document)
                        && fields.Fields.TryGetValue("navigation.revision", out var revision)
                        && new[] { surface, document, revision }.All(value => !string.IsNullOrWhiteSpace(value) && value.Length <= 128 && (policy is null || policy.SanitizeScalar(value) == value)))
                        identity = new(surface, document, revision);
                    else unavailable.Add("host_identity_unavailable_or_redacted");
                    if (TopLevel.GetTopLevel(visual) != top || context != QueryIdentity(visual, false)
                        || !ResolveInputGenerationTarget(top, target.TopLevelId, target.NodeId, target).Success)
                        throw new NavigationStop("navigation_identity_stale", "The identity provider was replaced or recycled during collection.");
                }
            }
            var observation = CollectObservation(options);
            if (observation.Windows.All(window => window.Window is null))
                throw new NavigationStop("navigation_no_windows", "No registered window is available in this observation scope.");
            if (request.IdentityTarget is { } selected && !observation.Windows.Any(window => window.TopLevelId == selected.TopLevelId && window.Generation == selected.TopLevelGeneration))
                throw new NavigationStop("navigation_identity_outside_scope", "The identity provider must belong to a sampled window generation.");
            if (request.IdentityTarget is { } original && context is not null
                && (FindTopLevel(original.TopLevelId) is not { } liveTop || FindNodeById(liveTop, original.NodeId!) is not Visual live
                    || context != QueryIdentity(live, false) || !ResolveInputGenerationTarget(liveTop, original.TopLevelId, original.NodeId, original).Success))
                throw new NavigationStop("navigation_identity_stale", "The provider changed while the surrounding observation was collected.");
            if (policy is not null)
            {
                var sanitized = policy.Sanitize(observation);
                if (!sanitized.Success) throw new NavigationStop("navigation_redaction_failed", "Evidence policy could not sanitize the observation.");
                observation = sanitized.Value!;
            }
            // Ignore focus, geometry and timestamps for candidate equivalence; preserve content, state and generations.
            var fingerprint = NavigationHash(observation.Windows.Select(window => new { window.TopLevelId, window.Generation,
                window.Window?.Title, window.Window?.Kind, window.Truncated, window.Parts,
                Nodes = window.Nodes.Select(node => new { node.NodeId, node.Generation, node.NodeType, node.Name, node.AutomationId, node.Text,
                    node.ParentNodeId, node.State, node.Validation, node.TextTruncated }) }));
            return (observation, fingerprint, identity, context);
        }
    }

    private RuntimeNavigationResponse NavigationResponse(NavigationRun run, RuntimeNavigationRequest request)
    {
        var all = run.Visits.Select(item => item.Visit).ToArray();
        IEnumerable<RuntimeNavigationVisit> selected = all;
        RuntimeNavigationRoute? route = null;
        if (request.VisitId is not null) selected = all.Where(visit => visit.VisitId == request.VisitId);
        else if (request.StateKey is not null) selected = all.Where(visit => visit.StateKey == request.StateKey);
        else if (request.FromVisitId is not null)
        {
            var from = Array.FindIndex(all, visit => visit.VisitId == request.FromVisitId);
            var to = Array.FindIndex(all, visit => visit.VisitId == request.ToVisitId);
            var range = from >= 0 && to >= from ? all[from..(to + 1)] : [];
            var complete = range.Length > 0 && range.Length <= request.MaxVisits;
            var path = range.Take(request.MaxVisits).ToArray();
            var steps = path.Skip(1).Select(visit => visit.ArrivedVia).Where(step => step is not null).Cast<RuntimeNavigationStep>().ToArray();
            complete &= steps.Length == path.Length - 1;
            route = new(range.Length == 0 ? "outside_retention_or_invalid_order" : !complete ? "partial" : steps.All(step => step.Action.Outcome == "succeeded") ? "reported_success" : "includes_failed_or_unknown",
                path.Select(visit => visit.VisitId).ToArray(), steps, complete);
            selected = range;
        }
        else selected = all.Reverse();
        var candidates = selected.ToArray();
        var visits = new List<RuntimeNavigationVisit>();
        var bytes = 0;
        foreach (var visit in candidates.Take(request.MaxVisits))
        {
            var item = request.IncludeEvidence ? visit : visit with { Observation = null };
            var size = JsonSerializer.SerializeToUtf8Bytes(item).Length;
            if (bytes + size > 80 * 1024) break;
            visits.Add(item); bytes += size;
        }
        var loops = new List<RuntimeNavigationLoop>();
        var patterns = new Dictionary<string, int>(StringComparer.Ordinal);
        for (var end = 1; end < all.Length; end++)
        {
            var key = EquivalenceKey(all[end]);
            var begin = Array.FindLastIndex(all, end - 1, visit => EquivalenceKey(visit) == key);
            if (begin < 0) continue;
            var segment = all[begin..(end + 1)];
            if (segment.Skip(1).Any(visit => visit.ArrivedVia is null)) continue;
            var pattern = NavigationHash(segment.Select(EquivalenceKey));
            patterns.TryGetValue(pattern, out var repeats); patterns[pattern] = ++repeats;
            loops.Add(new(all[begin].VisitId, all[end].VisitId, end - begin, repeats,
                segment.All(visit => visit.Identity is not null) ? "host_declared_and_sampled_revisit" : "possible_sampled_revisit; equivalence_uncertain",
                segment.Skip(1).All(visit => visit.ArrivedVia?.Action.Outcome == "succeeded") ? "reported_success" : "failed_unknown_or_unrecorded_actions"));
        }
        var diagnostics = new List<ProtocolError>();
        if (run.Dropped > 0) diagnostics.Add(new("navigation_retention_gap", "Older visits and their evidence were evicted; missing routes or loops are not proof they never occurred."));
        if ((request.VisitId is not null || request.StateKey is not null) && candidates.Length == 0)
            diagnostics.Add(new("navigation_no_retained_match", "No retained visit matches; it may be expired, evicted or outside this run."));
        if (visits.Count < candidates.Length) diagnostics.Add(new("navigation_response_partial", "The response limit omits retained visits; query a specific visit for its evidence."));
        if (loops.Count > 32) diagnostics.Add(new("navigation_loops_partial", "Only the latest 32 retained revisit loops are returned."));
        return new(run.Id, SessionId, run.ExpiresAt, request.Action == "start" ? "started" : request.Action == "record" ? "recorded" : "observed",
            all.LastOrDefault()?.VisitId, visits, loops.TakeLast(32).ToArray(), route, all.Length, run.Dropped, run.Bytes, visits.Count < candidates.Length || loops.Count > 32, diagnostics);

        static string EquivalenceKey(RuntimeNavigationVisit visit) => visit.Identity is null ? "sample:" + visit.ObservationRevision : "host:" + visit.StateKey;
    }

    private void ExpireNavigationRuns()
    {
        foreach (var pair in _navigationRuns.Where(pair => pair.Value.Expired).ToArray()) _navigationRuns.Remove(pair.Key);
    }

    private void CloseNavigation()
    {
        lock (_navigationLock) { _navigationClosed = true; _navigationExpiry?.Dispose(); _navigationExpiry = null; _navigationRuns.Clear(); }
    }

    private static string NavigationHash(object? value) => Convert.ToHexStringLower(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(value)));
    private sealed class NavigationStop(string code, string message) : Exception(message) { public string Code { get; } = code; }
    private sealed record NavigationEntry(RuntimeNavigationVisit Visit, int Bytes);
    private sealed class NavigationRun(RuntimeNavigationRequest request, string policyKey)
    {
        private readonly long _started = Stopwatch.GetTimestamp();
        private readonly int _ttlMs = request.TtlMs;
        public string Id { get; } = Guid.NewGuid().ToString("N");
        public string PolicyKey { get; } = policyKey;
        public DateTimeOffset ExpiresAt { get; } = DateTimeOffset.UtcNow.AddMilliseconds(request.TtlMs);
        public bool Expired => Stopwatch.GetElapsedTime(_started).TotalMilliseconds >= _ttlMs;
        public IReadOnlyList<string> TopLevelIds { get; } = request.TopLevelIds.ToArray();
        public int MaxNodes { get; } = request.MaxNodes;
        public int MaxDepth { get; } = request.MaxDepth;
        public List<NavigationEntry> Visits { get; } = [];
        public long Sequence { get; set; }
        public long Dropped { get; set; }
        public int Bytes { get; set; }
    }
}
