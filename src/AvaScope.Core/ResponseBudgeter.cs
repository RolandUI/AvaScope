using System.Security.Cryptography;
using System.Text.Json;
using AvaScope.Protocol;

namespace AvaScope.Core;

public static class ResponseBudgeter
{
    public const int DefaultMaxInlineBytes = 128 * 1024;
    public const int DefaultMaxItems = 200;
    public const int DefaultMaxDepth = 8;

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
            FindNodesResponse response when response.ResponseBudget is null
                => (T)(object)ApplyFindNodes(response, maxInlineBytes, maxItems, maxDepth),
            DiagnosticsResponse response when response.ResponseBudget is null
                => (T)(object)ApplyDiagnostics(response, maxInlineBytes, maxItems, maxDepth),
            SemanticWorkflowResponse response when response.ResponseBudget is null
                => (T)(object)ApplyWorkflow(response, maxInlineBytes, maxItems, maxDepth),
            RuntimeScenarioResponse response when response.ResponseBudget is null
                => (T)(object)ApplyScenario(response, maxInlineBytes, maxItems, maxDepth),
            _ => value
        };
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
        var artifactPath = WriteArtifact("scenario", payload);
        var returnedItems = (workflow?.Steps.Count ?? 0)
            + (workflow?.Diagnostics.Count ?? 0)
            + (workflow?.Plan?.Steps.Count ?? 0)
            + (workflow?.Plan?.Diagnostics.Count ?? 0)
            + diagnostics.Count;
        var budget = CreateInfo(
            maxInlineBytes, payload.Length, maxItems, totalItems, returnedItems, maxDepth,
            1, 1, artifactPath, reasons);
        return new RuntimeScenarioResponse(
            response.RequestId, response.Status, response.StartedAt, response.CompletedAt,
            response.SessionId, response.TopLevelId, response.Launch, response.Attach, workflow,
            response.IsolatedStateStatus, response.IsolatedStateDirectory, response.TimelinePath,
            diagnostics, response.Metadata, response.PreparedPickerResult, budget);
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
            node.InteractionState);
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

    private static string? WriteArtifact(string kind, byte[] payload)
    {
        try
        {
            var hash = Convert.ToHexString(SHA256.HashData(payload)).ToLowerInvariant()[..16];
            var directory = Path.Combine(Path.GetTempPath(), "AvaScope", "response-artifacts");
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
