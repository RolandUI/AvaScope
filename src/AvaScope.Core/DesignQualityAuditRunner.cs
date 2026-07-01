using AvaScope.Protocol;

namespace AvaScope.Core;

public sealed class DesignQualityAuditRunner
{
    public async Task<CoreResult<DesignQualityAuditResponse>> RunAsync(
        LocalBridgeClient bridgeClient,
        DesignQualityAuditRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(bridgeClient);
        ArgumentNullException.ThrowIfNull(request);

        CoreResult<TreeResponse> tree = request.TreeKind switch
        {
            TreeKinds.Visual => await bridgeClient.VisualTreeAsync(
                request.SessionId,
                request.TopLevelId,
                request.MaxDepth,
                cancellationToken),
            TreeKinds.Logical => await bridgeClient.LogicalTreeAsync(
                request.SessionId,
                request.TopLevelId,
                request.MaxDepth,
                cancellationToken),
            _ => CoreResult<TreeResponse>.Fail(new CoreError(
                CoreErrorCodes.InvalidBridgeRequest,
                $"Tree kind '{request.TreeKind}' is not supported.",
                new Dictionary<string, string>
                {
                    ["supportedTreeKinds"] = $"{TreeKinds.Visual},{TreeKinds.Logical}"
                }))
        };

        if (!tree.Success)
        {
            return CoreResult<DesignQualityAuditResponse>.Fail(tree.Error!);
        }

        return new DesignQualityAuditBuilder().Create(tree.Value!, request);
    }
}
