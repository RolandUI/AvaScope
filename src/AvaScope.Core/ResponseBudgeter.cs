using System.Security.Cryptography;
using System.Text.Json;
using AvaScope.Protocol;

namespace AvaScope.Core;

public static class ResponseBudgeter
{
    internal const string ArtifactDirectoryEnvironmentVariable = "AVASCOPE_RESPONSE_ARTIFACT_DIR";

    public const int DefaultMaxInlineBytes = 128 * 1024;
    public const int DefaultMaxItems = 200;
    public const int DefaultMaxDepth = 8;

    /// <summary>Reads verified, bounded full tree evidence for internal audit consumers.
    /// Unavailable or mismatched evidence leaves the original partial response intact.</summary>
    public static TreeResponse ReadTreeEvidence(TreeResponse response)
    {
        ArgumentNullException.ThrowIfNull(response);
        var budget = response.ResponseBudget;
        if (budget is not { Truncated: true, ArtifactPath: not null, EstimatedBytes: > 0 and <= 16 * 1024 * 1024,
                TotalItems: > 0 and <= 8192, OriginalDepth: <= 64 }) return response;
        try
        {
            using var stream = File.OpenRead(budget.ArtifactPath);
            if (stream.Length != budget.EstimatedBytes) return response;
            var payload = new byte[budget.EstimatedBytes];
            stream.ReadExactly(payload);
            if (stream.ReadByte() != -1) return response;
            var hash = Convert.ToHexString(SHA256.HashData(payload)).ToLowerInvariant()[..16];
            if (!string.Equals(Path.GetFileName(budget.ArtifactPath), $"tree-{hash}.json", StringComparison.Ordinal)) return response;
            var full = JsonSerializer.Deserialize<TreeResponse>(payload, JsonOptions);
            if (full is null || full.ResponseBudget is not null || full.SessionId != response.SessionId
                || full.TopLevelId != response.TopLevelId || full.TreeKind != response.TreeKind
                || full.DepthLimit != response.DepthLimit || full.Target != response.Target
                || full.Root.NodeId != response.Root.NodeId || full.Root.Target != response.Root.Target
                || CountNodes(full.Root) != budget.TotalItems || GetDepth(full.Root) != budget.OriginalDepth)
                return response;
            var pending = new Stack<TreeNodeSummary>();
            pending.Push(full.Root);
            while (pending.TryPop(out var node))
            {
                if (node.Target is { } target && (target.SessionId != full.SessionId
                    || target.TopLevelId != full.TopLevelId || target.TreeKind != full.TreeKind
                    || target.NodeId != node.NodeId || target.TopLevelGeneration != full.Target.TopLevelGeneration))
                    return response;
                foreach (var child in node.Children) pending.Push(child);
            }
            return full;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException
            or ArgumentException or NotSupportedException)
        {
            return response;
        }
    }

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    public static T Apply<T>(
        T value,
        int maxInlineBytes = DefaultMaxInlineBytes,
        int maxItems = DefaultMaxItems,
        int maxDepth = DefaultMaxDepth)
    {
        ArgumentNullException.ThrowIfNull(value);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxInlineBytes, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxItems, 1);
        ArgumentOutOfRangeException.ThrowIfNegative(maxDepth);

        return value switch
        {
            TreeResponse response when response.ResponseBudget is null
                => (T)(object)ApplyTree(response, maxInlineBytes, maxItems, maxDepth),
            FindNodesResponse response when response.Coverage is not null
                => (T)(object)ApplyQuery(response, maxInlineBytes, maxItems),
            FindNodesResponse response when response.ResponseBudget is null
                => (T)(object)ApplyFindNodes(response, maxInlineBytes, maxItems, maxDepth),
            DiagnosticsResponse response when response.ResponseBudget is null
                => (T)(object)ApplyDiagnostics(response, maxInlineBytes, maxItems, maxDepth),
            SemanticWorkflowResponse response when response.ResponseBudget is null
                => (T)(object)ApplyWorkflow(response, maxInlineBytes, maxItems, maxDepth),
            RuntimeScenarioResponse response when response.ResponseBudget is null
                => (T)(object)ApplyScenario(response, maxInlineBytes, maxItems, maxDepth),
            RuntimeObservationResponse response when response.ResponseBudget is null
                => (T)(object)ApplyObservation(response, maxInlineBytes, maxItems, maxDepth),
            RuntimeObservationChangesResponse response when response.ResponseBudget is null
                => (T)(object)ApplyObservationChanges(response, maxInlineBytes, maxItems),
            _ => value
        };
    }

    private static FindNodesResponse ApplyQuery(FindNodesResponse response, int maxInlineBytes, int maxItems)
    {
        var matches = response.Matches.Take(maxItems).ToList();
        var projections = response.Projections.Take(maxItems).ToList();
        var candidates = response.Candidates.Take(maxItems).ToList();
        var reasons = response.Coverage!.Reasons.ToHashSet(StringComparer.Ordinal);
        if (matches.Count != response.Matches.Count || projections.Count != response.Projections.Count || candidates.Count != response.Candidates.Count) reasons.Add("response_item_limit");
        FindNodesResponse Result() => new(response.SessionId, response.TopLevelId, response.TreeKind, response.DepthLimit,
            matches.ToArray(), response.Target, projections: projections.ToArray(),
            coverage: response.Coverage with { Complete = response.Coverage.Complete && reasons.Count == 0, Reasons = reasons.Order(StringComparer.Ordinal).ToArray() }, candidates: candidates.ToArray());
        var result = Result();
        while (Serialize(result).Length > maxInlineBytes && (matches.Count > 0 || projections.Count > 0 || candidates.Count > 0))
        {
            reasons.Add("response_byte_limit");
            if (matches.Count > 0) matches.RemoveAt(matches.Count - 1);
            if (projections.Count > 0) projections.RemoveAt(projections.Count - 1);
            if (candidates.Count > 0) candidates.RemoveAt(candidates.Count - 1);
            result = Result();
        }
        return result;
    }

    public static RuntimeObservationChangesResponse ApplyObservationChanges(RuntimeObservationChangesResponse response,
        int maxInlineBytes, int maxItems, string? artifactDirectory = null)
    {
        var payload = Serialize(response);
        if (payload.Length <= maxInlineBytes) return response;
        var artifact = WriteArtifact("observation-changes", payload, artifactDirectory);
        return response with
        {
            Events = [], Baseline = null, RequiresArtifactRead = true,
            ResponseBudget = CreateInfo(maxInlineBytes, payload.Length, maxItems, response.Events.Count, 0, 8, 8, 0,
                artifact, ["byte_budget", "read_artifact_before_advancing_cursor"])
        };
    }

    public static RuntimeObservationResponse ApplyObservation(RuntimeObservationResponse response,
        int maxInlineBytes, int maxItems, int maxDepth, string? artifactDirectory = null)
    {
        var payload = Serialize(response);
        if (payload.Length <= maxInlineBytes) return response;
        var totalItems = response.Windows.Sum(window => window.Nodes.Count);
        var artifactPath = WriteArtifact("observation", payload, artifactDirectory);
        var windows = response.Windows.ToArray();
        var returned = totalItems;
        while (Serialize(response with { Windows = windows }).Length > Math.Max(1024, maxInlineBytes - 1024) && returned > 0)
        {
            var largest = Array.FindIndex(windows, window => window.Nodes.Count == windows.Max(candidate => candidate.Nodes.Count));
            var window = windows[largest];
            var keep = window.Nodes.Count / 2;
            returned -= window.Nodes.Count - keep;
            windows[largest] = window with { Nodes = window.Nodes.Take(keep).ToArray(), Truncated = true };
        }
        var result = response with
        {
            Windows = windows, Truncated = true,
            ResponseBudget = CreateInfo(maxInlineBytes, payload.Length, maxItems, totalItems, returned, maxDepth, maxDepth, maxDepth,
                artifactPath, ["byte_budget"])
        };
        if (Serialize(result).Length > maxInlineBytes)
            result = result with { Windows = [], Diagnostics = [], ResponseBudget = CreateInfo(maxInlineBytes, payload.Length,
                maxItems, totalItems, 0, maxDepth, maxDepth, 0, artifactPath, ["byte_budget"]) };
        return result;
    }

    private static TreeResponse ApplyTree(TreeResponse response, int maxInlineBytes, int maxItems, int maxDepth)
    {
        var payload = Serialize(response);
        var totalItems = CountNodes(response.Root);
        var originalDepth = GetDepth(response.Root);
        var reasons = GetReasons(payload.Length, totalItems, originalDepth, maxInlineBytes, maxItems, maxDepth);
        if (reasons.Count == 0)
        {
            return response;
        }

        var byteLimited = reasons.Contains("byte_budget", StringComparer.Ordinal);
        var remaining = byteLimited ? Math.Min(maxItems, 32) : maxItems;
        var root = ProjectNode(response.Root, 0, maxDepth, byteLimited, ref remaining);
        var artifactPath = WriteArtifact("tree", payload);
        var budget = CreateInfo(
            maxInlineBytes, payload.Length, maxItems, totalItems, CountNodes(root), maxDepth,
            originalDepth, GetDepth(root), artifactPath, reasons);
        return new TreeResponse(
            response.SessionId, response.TopLevelId, response.TreeKind, response.DepthLimit,
            root, response.Target, budget);
    }

    private static FindNodesResponse ApplyFindNodes(
        FindNodesResponse response,
        int maxInlineBytes,
        int maxItems,
        int maxDepth)
    {
        var payload = Serialize(response);
        var totalItems = response.Matches.Sum(static match => CountNodes(match.Node));
        var originalDepth = response.Matches.Count == 0
            ? 0
            : response.Matches.Max(static match => GetDepth(match.Node));
        var reasons = GetReasons(payload.Length, totalItems, originalDepth, maxInlineBytes, maxItems, maxDepth);
        if (reasons.Count == 0)
        {
            return response;
        }

        var byteLimited = reasons.Contains("byte_budget", StringComparer.Ordinal);
        var remaining = byteLimited ? Math.Min(maxItems, 32) : maxItems;
        var matches = new List<FindNodeMatch>();
        foreach (var match in response.Matches)
        {
            if (remaining <= 0)
            {
                break;
            }

            matches.Add(new FindNodeMatch(
                ProjectNode(match.Node, 0, maxDepth, byteLimited, ref remaining),
                match.Path,
                match.Target));
        }

        var returnedItems = matches.Sum(static match => CountNodes(match.Node));
        var returnedDepth = matches.Count == 0 ? 0 : matches.Max(static match => GetDepth(match.Node));
        var artifactPath = WriteArtifact("find-nodes", payload);
        var budget = CreateInfo(
            maxInlineBytes, payload.Length, maxItems, totalItems, returnedItems, maxDepth,
            originalDepth, returnedDepth, artifactPath, reasons);
        return new FindNodesResponse(
            response.SessionId, response.TopLevelId, response.TreeKind, response.DepthLimit,
            matches, response.Target, budget);
    }

    private static DiagnosticsResponse ApplyDiagnostics(
        DiagnosticsResponse response,
        int maxInlineBytes,
        int maxItems,
        int maxDepth)
    {
        var payload = Serialize(response);
        var totalItems = response.BridgeSessions.Count
            + response.PreviewSessions.Count
            + response.Issues.Count
            + response.DiagnosticIssues.Count
            + response.ComponentOrigins.Count;
        var reasons = GetReasons(payload.Length, totalItems, 1, maxInlineBytes, maxItems, maxDepth);
        if (reasons.Count == 0)
        {
            return response;
        }

        var byteLimited = reasons.Contains("byte_budget", StringComparer.Ordinal);
        var remaining = byteLimited ? 0 : maxItems;
        var bridgeSessions = Take(response.BridgeSessions, ref remaining);
        var previewSessions = Take(response.PreviewSessions, ref remaining);
        var issues = Take(response.Issues, ref remaining);
        var diagnosticIssues = Take(response.DiagnosticIssues, ref remaining);
        var componentOrigins = Take(response.ComponentOrigins, ref remaining);
        var artifactPath = WriteArtifact("diagnostics", payload);
        var returnedItems = bridgeSessions.Count + previewSessions.Count + issues.Count
            + diagnosticIssues.Count + componentOrigins.Count;
        var budget = CreateInfo(
            maxInlineBytes, payload.Length, maxItems, totalItems, returnedItems, maxDepth,
            1, 1, artifactPath, reasons);
        return new DiagnosticsResponse(
            response.Service, response.GeneratedAt, response.ManifestDirectory,
            bridgeSessions, issues, response.PreviewHost, previewSessions, diagnosticIssues,
            response.Summary, componentOrigins, budget);
    }

    private static SemanticWorkflowResponse ApplyWorkflow(
        SemanticWorkflowResponse response,
        int maxInlineBytes,
        int maxItems,
        int maxDepth)
    {
        var payload = Serialize(response);
        var totalItems = response.Steps.Count
            + response.Diagnostics.Count
            + (response.Plan?.Steps.Count ?? 0)
            + (response.Plan?.Diagnostics.Count ?? 0)
            + response.Steps.Sum(static step =>
                (step.Verification?.Diagnostics.Count ?? 0)
                + (step.FailureEvidence?.Diagnostics.Count ?? 0)
                + (step.FailureEvidence?.UnavailableEvidence.Count ?? 0))
            + (response.ReportPack?.Assets.Count ?? 0);
        var reasons = GetReasons(payload.Length, totalItems, 1, maxInlineBytes, maxItems, maxDepth);
        if (reasons.Count == 0)
        {
            return response;
        }

        var byteLimited = reasons.Contains("byte_budget", StringComparer.Ordinal);
        var remaining = byteLimited ? 0 : maxItems;
        var steps = Take(response.Steps, ref remaining);
        var diagnostics = Take(response.Diagnostics, ref remaining);
        SemanticWorkflowPlan? plan = null;
        if (response.Plan is not null)
        {
            var planSteps = Take(response.Plan.Steps, ref remaining);
            var planDiagnostics = Take(response.Plan.Diagnostics, ref remaining);
            plan = new SemanticWorkflowPlan(
                response.Plan.Valid,
                response.Plan.ExpandedStepCount,
                response.Plan.EstimatedMaximumExecutions,
                response.Plan.MaximumNestingDepth,
                response.Plan.MaximumArtifactCount,
                planSteps,
                planDiagnostics);
        }
        var artifactPath = WriteArtifact("workflow", payload);
        var returnedItems = steps.Count
            + diagnostics.Count
            + (plan?.Steps.Count ?? 0)
            + (plan?.Diagnostics.Count ?? 0);
        var budget = CreateInfo(
            maxInlineBytes, payload.Length, maxItems, totalItems, returnedItems, maxDepth,
            1, 1, artifactPath, reasons);
        return new SemanticWorkflowResponse(
            response.RequestId, response.SessionId, response.TopLevelId, response.Status,
            response.StartedAt, response.CompletedAt, steps, response.IsolatedStateStatus,
            diagnostics, response.Metadata, budget, plan, response.ReportPack);
    }

    private static RuntimeScenarioResponse ApplyScenario(
        RuntimeScenarioResponse response,
        int maxInlineBytes,
        int maxItems,
        int maxDepth)
    {
        var payload = Serialize(response);
        var totalItems = (response.Workflow?.Steps.Count ?? 0)
            + (response.Workflow?.Diagnostics.Count ?? 0)
            + (response.Workflow?.Plan?.Steps.Count ?? 0)
            + (response.Workflow?.Plan?.Diagnostics.Count ?? 0)
            + (response.Workflow?.Steps.Sum(static step =>
                (step.Verification?.Diagnostics.Count ?? 0)
                + (step.FailureEvidence?.Diagnostics.Count ?? 0)
                + (step.FailureEvidence?.UnavailableEvidence.Count ?? 0)) ?? 0)
            + (response.Workflow?.ReportPack?.Assets.Count ?? 0)
            + response.TopLevels.Count
            + response.Diagnostics.Count;
        var reasons = GetReasons(payload.Length, totalItems, 1, maxInlineBytes, maxItems, maxDepth);
        if (reasons.Count == 0)
        {
            return response;
        }

        var byteLimited = reasons.Contains("byte_budget", StringComparer.Ordinal);
        var remaining = byteLimited ? 0 : maxItems;
        SemanticWorkflowResponse? workflow = null;
        if (response.Workflow is not null)
        {
            var steps = Take(response.Workflow.Steps, ref remaining);
            var workflowDiagnostics = Take(response.Workflow.Diagnostics, ref remaining);
            SemanticWorkflowPlan? plan = null;
            if (response.Workflow.Plan is not null)
            {
                var planSteps = Take(response.Workflow.Plan.Steps, ref remaining);
                var planDiagnostics = Take(response.Workflow.Plan.Diagnostics, ref remaining);
                plan = new SemanticWorkflowPlan(
                    response.Workflow.Plan.Valid,
                    response.Workflow.Plan.ExpandedStepCount,
                    response.Workflow.Plan.EstimatedMaximumExecutions,
                    response.Workflow.Plan.MaximumNestingDepth,
                    response.Workflow.Plan.MaximumArtifactCount,
                    planSteps,
                    planDiagnostics);
            }

            workflow = new SemanticWorkflowResponse(
                response.Workflow.RequestId, response.Workflow.SessionId, response.Workflow.TopLevelId,
                response.Workflow.Status, response.Workflow.StartedAt, response.Workflow.CompletedAt,
                steps, response.Workflow.IsolatedStateStatus, workflowDiagnostics,
                response.Workflow.Metadata, plan: plan, reportPack: response.Workflow.ReportPack);
        }

        var diagnostics = Take(response.Diagnostics, ref remaining);
        var topLevels = Take(response.TopLevels, ref remaining);
        var artifactPath = WriteArtifact("scenario", payload);
        var returnedItems = (workflow?.Steps.Count ?? 0)
            + (workflow?.Diagnostics.Count ?? 0)
            + (workflow?.Plan?.Steps.Count ?? 0)
            + (workflow?.Plan?.Diagnostics.Count ?? 0)
            + topLevels.Count
            + diagnostics.Count;
        var budget = CreateInfo(
            maxInlineBytes, payload.Length, maxItems, totalItems, returnedItems, maxDepth,
            1, 1, artifactPath, reasons);
        return new RuntimeScenarioResponse(
            response.RequestId, response.Status, response.StartedAt, response.CompletedAt,
            response.SessionId, response.TopLevelId, response.Launch, response.Attach, workflow,
            response.IsolatedStateStatus, response.IsolatedStateDirectory, response.TimelinePath,
            diagnostics, response.Metadata, response.PreparedPickerResult, budget,
            response.Build, response.Readiness, topLevels, response.Cleanup, response.FailureStage, response.Environment, response.TestFixture, response.RunId);
    }

    private static TreeNodeSummary ProjectNode(
        TreeNodeSummary node,
        int depth,
        int maxDepth,
        bool compact,
        ref int remaining)
    {
        remaining--;
        var children = new List<TreeNodeSummary>();
        if (depth < maxDepth && remaining > 0)
        {
            foreach (var child in node.Children)
            {
                if (remaining <= 0)
                {
                    break;
                }

                children.Add(ProjectNode(child, depth + 1, maxDepth, compact, ref remaining));
            }
        }

        return new TreeNodeSummary(
            LimitRequired(node.NodeId, 256),
            LimitRequired(node.NodeType, 256),
            Limit(node.Name, 256),
            Limit(node.AutomationId, 256),
            Limit(node.Text, compact ? 512 : int.MaxValue),
            node.Bounds,
            compact ? node.Classes.Take(8).Select(static value => LimitRequired(value, 128)).ToArray() : node.Classes,
            children,
            node.Target,
            compact ? null : node.AccessibilityState,
            compact ? null : node.ValidationState,
            compact ? null : node.SourceMap,
            compact ? null : node.BindingSummary,
            node.InteractionState,
            childrenTruncated: node.ChildrenTruncated || children.Count != node.Children.Count);
    }

    private static int CountNodes(TreeNodeSummary node) =>
        1 + node.Children.Sum(CountNodes);

    private static int GetDepth(TreeNodeSummary node) =>
        node.Children.Count == 0 ? 0 : 1 + node.Children.Max(GetDepth);

    private static string LimitRequired(string value, int maximumLength) =>
        value.Length <= maximumLength ? value : value[..maximumLength];

    private static string? Limit(string? value, int maximumLength) =>
        value is null || value.Length <= maximumLength ? value : value[..maximumLength];

    private static IReadOnlyList<T> Take<T>(IReadOnlyList<T> items, ref int remaining)
    {
        if (remaining <= 0 || items.Count == 0)
        {
            return [];
        }

        var count = Math.Min(items.Count, remaining);
        remaining -= count;
        return items.Take(count).ToArray();
    }

    private static List<string> GetReasons(
        int bytes,
        int items,
        int depth,
        int maxInlineBytes,
        int maxItems,
        int maxDepth)
    {
        var reasons = new List<string>(3);
        if (bytes > maxInlineBytes)
        {
            reasons.Add("byte_budget");
        }

        if (items > maxItems)
        {
            reasons.Add("item_budget");
        }

        if (depth > maxDepth)
        {
            reasons.Add("depth_budget");
        }

        return reasons;
    }

    private static ResponseBudgetInfo CreateInfo(
        int maxInlineBytes,
        int estimatedBytes,
        int maxItems,
        int totalItems,
        int returnedItems,
        int maxDepth,
        int originalDepth,
        int returnedDepth,
        string? artifactPath,
        IReadOnlyList<string> reasons) =>
        new(
            maxInlineBytes, estimatedBytes, maxItems, totalItems, returnedItems, maxDepth,
            originalDepth, returnedDepth, truncated: true, artifactPath,
            artifactPath is null ? reasons.Concat(["artifact_unavailable"]).ToArray() : reasons);

    private static byte[] Serialize<T>(T value) =>
        JsonSerializer.SerializeToUtf8Bytes(value, JsonOptions);

    private static string? WriteArtifact(string kind, byte[] payload, string? artifactDirectory = null)
    {
        try
        {
            var hash = Convert.ToHexString(SHA256.HashData(payload)).ToLowerInvariant()[..16];
            var configuredDirectory = Environment.GetEnvironmentVariable(ArtifactDirectoryEnvironmentVariable);
            var directory = artifactDirectory ?? (string.IsNullOrWhiteSpace(configuredDirectory)
                ? Path.Combine(Path.GetTempPath(), "AvaScope", "response-artifacts")
                : Path.GetFullPath(configuredDirectory));
            Directory.CreateDirectory(directory);
            var path = Path.Combine(directory, $"{kind}-{hash}.json");
            if (!File.Exists(path))
            {
                File.WriteAllBytes(path, payload);
            }

            return path;
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }
}
