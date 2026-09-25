using AvaScope.Protocol;
using System.Globalization;
using System.Text.Json;

namespace AvaScope.Core;

public sealed class RuntimeMutationEvidenceRunner
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    public async Task<CoreResult<RuntimeMutationEvidenceResponse>> CaptureAsync(
        LocalBridgeClient bridgeClient,
        SessionId sessionId,
        RuntimeMutationRequest request,
        string artifactDirectory,
        int maxDepth = 8,
        bool includeDiff = true,
        double tolerance = 0,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(bridgeClient);
        ArgumentNullException.ThrowIfNull(sessionId);
        ArgumentNullException.ThrowIfNull(request);

        if (request.Target.SessionId != sessionId)
        {
            return CoreResult<RuntimeMutationEvidenceResponse>.Fail(new CoreError(
                CoreErrorCodes.InvalidBridgeRequest,
                "Mutation evidence requests must target the selected bridge session.",
                new Dictionary<string, string>
                {
                    ["selectedSessionId"] = sessionId.Value,
                    ["targetSessionId"] = request.Target.SessionId.Value
                }));
        }

        if (string.IsNullOrWhiteSpace(artifactDirectory))
        {
            return CoreResult<RuntimeMutationEvidenceResponse>.Fail(new CoreError(
                CoreErrorCodes.InvalidBridgeRequest,
                "Mutation evidence artifact directory cannot be empty."));
        }

        if (maxDepth < 0 || maxDepth > 64)
        {
            return CoreResult<RuntimeMutationEvidenceResponse>.Fail(new CoreError(
                CoreErrorCodes.InvalidBridgeRequest,
                "Mutation evidence maxDepth must be between 0 and 64."));
        }

        if (tolerance < 0 || tolerance > 255)
        {
            return CoreResult<RuntimeMutationEvidenceResponse>.Fail(new CoreError(
                CoreErrorCodes.InvalidBridgeRequest,
                "Mutation evidence tolerance must be between 0 and 255."));
        }

        var fullArtifactDirectory = Path.GetFullPath(artifactDirectory);
        var artifactPrefix = SanitizeArtifactPrefix(request.RequestId);
        var beforeScreenshotPath = Path.Combine(fullArtifactDirectory, $"{artifactPrefix}-before.png");
        var afterScreenshotPath = Path.Combine(fullArtifactDirectory, $"{artifactPrefix}-after.png");
        var beforeTreePath = Path.Combine(fullArtifactDirectory, $"{artifactPrefix}-before-visual-tree.json");
        var afterTreePath = Path.Combine(fullArtifactDirectory, $"{artifactPrefix}-after-visual-tree.json");
        var diffPath = includeDiff ? Path.Combine(fullArtifactDirectory, $"{artifactPrefix}-diff.png") : null;

        try
        {
            Directory.CreateDirectory(fullArtifactDirectory);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            return CoreResult<RuntimeMutationEvidenceResponse>.Fail(new CoreError(
                CoreErrorCodes.InvalidBridgeRequest,
                $"Mutation evidence artifact directory could not be created: {exception.Message}",
                new Dictionary<string, string>
                {
                    ["artifactDirectory"] = fullArtifactDirectory
                }));
        }

        var beforeScreenshot = await bridgeClient.CaptureScreenshotAsync(
            sessionId,
            request.Target.TopLevelId,
            beforeScreenshotPath,
            cancellationToken);
        if (!beforeScreenshot.Success)
        {
            return CoreResult<RuntimeMutationEvidenceResponse>.Fail(beforeScreenshot.Error!);
        }

        var beforeTree = await bridgeClient.VisualTreeAsync(
            sessionId,
            request.Target.TopLevelId,
            maxDepth,
            cancellationToken);
        if (!beforeTree.Success)
        {
            return CoreResult<RuntimeMutationEvidenceResponse>.Fail(beforeTree.Error!);
        }

        var beforeTreeWrite = await WriteTreeSnapshotAsync(beforeTree.Value!, beforeTreePath, cancellationToken);
        if (!beforeTreeWrite.Success)
        {
            return CoreResult<RuntimeMutationEvidenceResponse>.Fail(beforeTreeWrite.Error!);
        }

        var mutation = await bridgeClient.MutateNodeAsync(sessionId, request, cancellationToken);
        if (!mutation.Success)
        {
            return CoreResult<RuntimeMutationEvidenceResponse>.Fail(mutation.Error!);
        }

        var afterScreenshot = await bridgeClient.CaptureScreenshotAsync(
            sessionId,
            request.Target.TopLevelId,
            afterScreenshotPath,
            cancellationToken);
        if (!afterScreenshot.Success)
        {
            return CoreResult<RuntimeMutationEvidenceResponse>.Fail(afterScreenshot.Error!);
        }

        var afterTree = await bridgeClient.VisualTreeAsync(
            sessionId,
            request.Target.TopLevelId,
            maxDepth,
            cancellationToken);
        if (!afterTree.Success)
        {
            return CoreResult<RuntimeMutationEvidenceResponse>.Fail(afterTree.Error!);
        }

        var afterTreeWrite = await WriteTreeSnapshotAsync(afterTree.Value!, afterTreePath, cancellationToken);
        if (!afterTreeWrite.Success)
        {
            return CoreResult<RuntimeMutationEvidenceResponse>.Fail(afterTreeWrite.Error!);
        }

        PreviewDiffResponse? diff = null;
        var diagnostics = new List<ProtocolError>(mutation.Value!.Diagnostics);
        var diffStatus = "not_requested";
        if (includeDiff && diffPath is not null)
        {
            var diffResult = new PreviewImageDiffer().Compare(beforeScreenshotPath, afterScreenshotPath, diffPath, tolerance);
            if (diffResult.Success)
            {
                diff = diffResult.Value!;
                diffStatus = diff.Passed ? "unchanged" : "changed";
            }
            else
            {
                diffStatus = "error";
                diagnostics.Add(new ProtocolError(
                    diffResult.Error!.Code,
                    diffResult.Error.Message,
                    diffResult.Error.Details));
            }
        }

        var beforeTarget = FindEvidenceTarget(beforeTree.Value!, request.Target, mutation.Value.Target, "before", diagnostics);
        var afterTarget = FindEvidenceTarget(afterTree.Value!, request.Target, mutation.Value.Target, "after", diagnostics);
        var summary = new RuntimeMutationEvidenceSummary(
            CreateEvidenceStatus(mutation.Value),
            mutation.Value.Status,
            mutation.Value.Applied,
            screenshotsCaptured: true,
            visualTreeSnapshotsCaptured: true,
            diffStatus,
            CountNodes(beforeTree.Value!.Root),
            CountNodes(afterTree.Value!.Root),
            beforeTarget is not null,
            afterTarget is not null,
            diff?.ChangedPixels,
            diff?.ChangedPercent);

        var response = new RuntimeMutationEvidenceResponse(
            request.RequestId,
            sessionId,
            request.Target.TopLevelId,
            request.Target,
            mutation.Value,
            summary,
            fullArtifactDirectory,
            beforeScreenshot.Value!.FilePath,
            afterScreenshot.Value!.FilePath,
            beforeTreePath,
            afterTreePath,
            DateTimeOffset.UtcNow,
            diffPath,
            diff,
            beforeTarget is null ? null : ToEvidenceTargetSummary(beforeTarget),
            afterTarget is null ? null : ToEvidenceTargetSummary(afterTarget),
            diagnostics);
        var reviewArtifact = new RuntimeMutationReviewExporter().ExportEvidence(response);
        if (!reviewArtifact.Success)
        {
            return CoreResult<RuntimeMutationEvidenceResponse>.Fail(reviewArtifact.Error!);
        }

        return CoreResult<RuntimeMutationEvidenceResponse>.Ok(new RuntimeMutationEvidenceResponse(
            response.RequestId,
            response.SessionId,
            response.TopLevelId,
            response.Target,
            response.Mutation,
            response.Summary,
            response.ArtifactDirectory,
            response.BeforeScreenshotPath,
            response.AfterScreenshotPath,
            response.BeforeVisualTreePath,
            response.AfterVisualTreePath,
            response.CapturedAt,
            response.DiffPath,
            response.Diff,
            response.BeforeTarget,
            response.AfterTarget,
            response.Diagnostics,
            reviewArtifact.Value));
    }

    private static async Task<CoreResult<string>> WriteTreeSnapshotAsync(
        TreeResponse tree,
        string outputPath,
        CancellationToken cancellationToken)
    {
        try
        {
            var fullPath = Path.GetFullPath(outputPath);
            var directory = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            await File.WriteAllTextAsync(
                fullPath,
                JsonSerializer.Serialize(tree, JsonOptions),
                cancellationToken);

            return CoreResult<string>.Ok(fullPath);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            return CoreResult<string>.Fail(new CoreError(
                CoreErrorCodes.InvalidBridgeRequest,
                $"Mutation evidence tree snapshot could not be written: {exception.Message}",
                new Dictionary<string, string>
                {
                    ["outputPath"] = outputPath
                }));
        }
    }

    private static string SanitizeArtifactPrefix(string requestId)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var sanitized = new string(requestId
            .Select(character => invalid.Contains(character) || char.IsWhiteSpace(character) ? '-' : character)
            .ToArray())
            .Trim('-', '.');

        return string.IsNullOrWhiteSpace(sanitized) ? "mutation-evidence" : sanitized;
    }

    private static string CreateEvidenceStatus(RuntimeMutationResponse mutation)
    {
        if (mutation.Applied)
        {
            return "captured";
        }

        return mutation.Status == RuntimeMutationStatuses.NoOp
            ? "captured_no_change"
            : "mutation_not_applied";
    }

    private static TreeNodeSummary? FindEvidenceTarget(
        TreeResponse tree,
        RuntimeTargetContext requested,
        RuntimeTargetContext resolved,
        string stage,
        List<ProtocolError> diagnostics)
    {
        if (string.IsNullOrWhiteSpace(requested.NodeId))
        {
            return null;
        }

        var requestedTreeKind = requested.TreeKind ?? TreeKinds.Visual;
        var logical = requestedTreeKind == TreeKinds.Logical;
        var identityAvailable = tree.TreeKind == TreeKinds.Visual
            && tree.SessionId == requested.SessionId && tree.TopLevelId == requested.TopLevelId
            && resolved.SessionId == requested.SessionId && resolved.TopLevelId == requested.TopLevelId
            && resolved.NodeId == requested.NodeId && (resolved.TreeKind ?? TreeKinds.Visual) == requestedTreeKind
            && (requested.NodeGeneration is null || requested.NodeGeneration == resolved.NodeGeneration)
            && (requested.TopLevelGeneration is null || requested.TopLevelGeneration == resolved.TopLevelGeneration)
            && (!logical || (!string.IsNullOrWhiteSpace(resolved.NodeGeneration)
                && !string.IsNullOrWhiteSpace(resolved.TopLevelGeneration)));
        TreeNodeSummary? match = null;
        var ambiguous = false;
        var pending = new Stack<TreeNodeSummary>();
        if (identityAvailable) pending.Push(tree.Root);
        while (pending.TryPop(out var node))
        {
            // Tree-local IDs are opaque. Logical aliases require the generation identity
            // supplied by the bridge, scoped to this same session and top-level.
            var matches = logical || node.NodeId == resolved.NodeId;
            if (node.Target is { } identity)
            {
                matches &= identity.SessionId == requested.SessionId
                    && identity.TopLevelId == requested.TopLevelId
                    && (identity.TreeKind ?? TreeKinds.Visual) == TreeKinds.Visual && identity.NodeId == node.NodeId
                    && (resolved.NodeGeneration is null || identity.NodeGeneration == resolved.NodeGeneration)
                    && (resolved.TopLevelGeneration is null || identity.TopLevelGeneration == resolved.TopLevelGeneration);
            }
            else
            {
                matches &= !logical && resolved.NodeGeneration is null && resolved.TopLevelGeneration is null;
            }
            if (matches)
            {
                if (match is not null) { ambiguous = true; break; }
                match = node;
            }
            foreach (var child in node.Children) pending.Push(child);
        }

        if (match is not null && !ambiguous) return match;
        diagnostics.Add(new ProtocolError(
            RuntimeMutationErrorCodes.RuntimeMutationEvidenceTargetUnavailable,
            "The mutation target could not be uniquely identified in the captured visual tree; this does not establish that it is absent from the application.",
            new Dictionary<string, string>
            {
                ["stage"] = stage,
                ["requestedTreeKind"] = requestedTreeKind,
                ["maxDepth"] = tree.DepthLimit.ToString(CultureInfo.InvariantCulture),
                ["reason"] = !identityAvailable ? "target_identity_unavailable"
                    : ambiguous ? "ambiguous_visual_identity" : "target_not_in_captured_visual_tree",
                ["nextAction"] = "Inspect the current target identity and capture sufficient visual-tree depth; do not infer aliases from node-id text."
            }));
        return null;
    }

    private static int CountNodes(TreeNodeSummary node)
    {
        var count = 1;
        foreach (var child in node.Children)
        {
            count += CountNodes(child);
        }

        return count;
    }

    private static RuntimeMutationEvidenceTargetSummary ToEvidenceTargetSummary(TreeNodeSummary node)
    {
        return new RuntimeMutationEvidenceTargetSummary(
            node.NodeId,
            node.NodeType,
            node.Name,
            node.Text,
            node.Bounds,
            node.Classes);
    }
}
