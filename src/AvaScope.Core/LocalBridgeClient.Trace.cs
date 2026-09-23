using System.Text.Json;
using AvaScope.Protocol;

namespace AvaScope.Core;

public sealed partial class LocalBridgeClient
{
    public async Task<CoreResult<RuntimeTraceResponse>> TraceAsync(RuntimeTraceRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var policy = request.Policy is null ? null : new RuntimeEvidencePolicyEnforcer(request.Policy);
        if (policy is not null)
        {
            var session = policy.AuthorizeSession(this, request.SessionId);
            if (!session.Success) return CoreResult<RuntimeTraceResponse>.Fail(session.Error!);
            var action = policy.AuthorizeAction(SemanticWorkflowActions.Inspect, null);
            if (!action.Success) return CoreResult<RuntimeTraceResponse>.Fail(action.Error!);
        }
        var manifest = FindSingleManifest(null, request.SessionId);
        if (!manifest.Success) return CoreResult<RuntimeTraceResponse>.Fail(manifest.Error!);
        var result = await SendAsync<RuntimeTraceResponse>(manifest.Value!, new(NewRequestId(), BridgeIpcMethods.Trace, trace: request), cancellationToken);
        if (!result.Success)
        {
            if (policy is null) return result;
            var safe = policy.Sanitize(result.Error!);
            return CoreResult<RuntimeTraceResponse>.Fail(safe.Success ? safe.Value! : safe.Error!);
        }
        if (policy is not null) result = policy.Sanitize(result.Value!);
        if (!result.Success || request.OutputDirectory is null) return result;
        try
        {
            var directory = Path.GetFullPath(request.OutputDirectory);
            var artifact = Path.Combine(directory, "trace.json");
            var prepared = policy!.PrepareRun(directory, [artifact], result.Value!.TraceId);
            if (!prepared.Success) return CoreResult<RuntimeTraceResponse>.Fail(prepared.Error!);
            var response = result.Value with { ArtifactPath = artifact };
            var safe = policy.Sanitize(response);
            if (!safe.Success) return safe;
            var temporary = Path.Combine(directory, ".trace-" + Guid.NewGuid().ToString("N") + ".tmp");
            try
            {
                await File.WriteAllTextAsync(temporary, JsonSerializer.Serialize(safe.Value, new JsonSerializerOptions { WriteIndented = true }), cancellationToken);
                File.Move(temporary, artifact, overwrite: true);
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
            return safe;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        { return CoreResult<RuntimeTraceResponse>.Fail(new("trace_export_failed", "The sanitized trace could not be written under the selected policy-owned directory.")); }
    }
}
