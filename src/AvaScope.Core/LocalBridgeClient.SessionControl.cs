using System.Collections.Concurrent;
using AvaScope.Protocol;

namespace AvaScope.Core;

public sealed partial class LocalBridgeClient
{
    private ConcurrentDictionary<string, string> _controlTokens = new(StringComparer.Ordinal);
    private readonly string? _defaultControlToken;

    public LocalBridgeClient WithManifestDirectory(string? directory)
    {
        var client = new LocalBridgeClient(directory ?? ManifestDirectory, OperationTimeout, _defaultControlToken);
        client._controlTokens = _controlTokens;
        return client;
    }

    private string ControlKey(SessionId sessionId) => ManifestDirectory + "|" + sessionId.Value;
    private string? GetControlToken(SessionId sessionId) => _controlTokens.GetValueOrDefault(ControlKey(sessionId)) ?? _defaultControlToken;

    public async Task<CoreResult<SessionControlResponse>> SessionControlAsync(SessionId sessionId, SessionControlRequest request,
        CancellationToken cancellationToken = default)
    {
        var manifest = FindSingleManifest(null, sessionId);
        if (!manifest.Success) return CoreResult<SessionControlResponse>.Fail(manifest.Error!);
        var result = await SendAsync<SessionControlResponse>(manifest.Value!, new BridgeIpcRequest(NewRequestId(), BridgeIpcMethods.SessionControl,
            sessionControl: request with { Token = request.Token ?? GetControlToken(sessionId) }), cancellationToken);
        if (result.Success)
        {
            if (result.Value!.Token is { } token) _controlTokens[ControlKey(sessionId)] = token;
            if (request.Operation == "release") _controlTokens.TryRemove(ControlKey(sessionId), out _);
        }
        return result;
    }
}
