using System.Text.Json.Serialization;

namespace AvaScope.Protocol;

public sealed record DiagnosticsResponse
{
    [JsonConstructor]
    public DiagnosticsResponse(
        HealthResponse service,
        DateTimeOffset generatedAt,
        string manifestDirectory,
        IReadOnlyList<BridgeSessionDiagnostic>? bridgeSessions = null,
        IReadOnlyList<ProtocolError>? issues = null)
    {
        ArgumentNullException.ThrowIfNull(service);

        if (string.IsNullOrWhiteSpace(manifestDirectory))
        {
            throw new ArgumentException("Manifest directory cannot be empty.", nameof(manifestDirectory));
        }

        Service = service;
        GeneratedAt = generatedAt;
        ManifestDirectory = Path.GetFullPath(manifestDirectory);
        BridgeSessions = bridgeSessions ?? [];
        Issues = issues ?? [];
    }

    [JsonPropertyName("service")]
    public HealthResponse Service { get; }

    [JsonPropertyName("generatedAt")]
    public DateTimeOffset GeneratedAt { get; }

    [JsonPropertyName("manifestDirectory")]
    public string ManifestDirectory { get; }

    [JsonPropertyName("bridgeSessions")]
    public IReadOnlyList<BridgeSessionDiagnostic> BridgeSessions { get; }

    [JsonPropertyName("issues")]
    public IReadOnlyList<ProtocolError> Issues { get; }
}
