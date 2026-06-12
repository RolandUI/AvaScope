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

        var beforeTarget = FindNodeById(beforeTree.Value!.Root, request.Target.NodeId);
        var afterTarget = FindNodeById(afterTree.Value!.Root, request.Target.NodeId);
        var summary = new RuntimeMutationEvidenceSummary(
            CreateEvidenceStatus(mutation.Value),
            mutation.Value.Status,
            mutation.Value.Applied,
            screenshotsCaptured: true,
            visualTreeSnapshotsCaptured: true,
            diffStatus,
            CountNodes(beforeTree.Value.Root),
            CountNodes(afterTree.Value.Root),
            beforeTarget is not null,
            afterTarget is not null,
            diff?.ChangedPixels,
            diff?.ChangedPercent);

        return CoreResult<RuntimeMutationEvidenceResponse>.Ok(new RuntimeMutationEvidenceResponse(
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
            diagnostics));
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

    private static TreeNodeSummary? FindNodeById(TreeNodeSummary node, string? nodeId)
    {
        if (string.IsNullOrWhiteSpace(nodeId))
        {
            return null;
        }

        if (string.Equals(node.NodeId, nodeId, StringComparison.Ordinal))
        {
            return node;
        }

        foreach (var child in node.Children)
        {
            var match = FindNodeById(child, nodeId);
            if (match is not null)
            {
                return match;
            }
        }

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
