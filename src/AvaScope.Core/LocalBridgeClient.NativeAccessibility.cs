using AvaScope.Protocol;

namespace AvaScope.Core;

public sealed partial class LocalBridgeClient
{
    public async Task<CoreResult<NativeAccessibilityAuditResponse>> AuditNativeAccessibilityAsync(NativeAccessibilityAuditRequest request, CancellationToken cancellationToken = default)
    {
        if (AuthorizeInspection(request.Target.SessionId, request.Policy) is { } error) return CoreResult<NativeAccessibilityAuditResponse>.Fail(error);
        var manifest = FindSingleManifest(null, request.Target.SessionId);
        if (!manifest.Success) return CoreResult<NativeAccessibilityAuditResponse>.Fail(manifest.Error!);
        var result = await SendAsync<NativeAccessibilityAuditResponse>(manifest.Value!, new(NewRequestId(), BridgeIpcMethods.AuditNativeAccessibility, nativeAccessibility: request), cancellationToken,
            _operationTimeout + TimeSpan.FromMilliseconds(request.TimeoutMs));
        return result.Success && request.Policy is { } policy ? new RuntimeEvidencePolicyEnforcer(policy).Sanitize(result.Value!) : result;
    }
}
