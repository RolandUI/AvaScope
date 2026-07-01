using System.Text.Json.Serialization;

namespace AvaScope.Protocol;

public sealed record DoctorResponse
{
    [JsonConstructor]
    public DoctorResponse(
        HealthResponse service,
        DateTimeOffset generatedAt,
        string status,
        string cliAssemblyPath,
        string baseDirectory,
        string manifestDirectory,
        string previewSessionStoreDirectory,
        IReadOnlyList<DoctorCheck>? checks = null,
        IReadOnlyList<ProtocolError>? issues = null,
        PreviewHostDiagnostic? previewHost = null,
        IReadOnlyList<BridgeSessionDiagnostic>? bridgeSessions = null,
        IReadOnlyList<PreviewSessionDiagnostic>? previewSessions = null,
        string? productVersion = null)
    {
        ArgumentNullException.ThrowIfNull(service);

        if (string.IsNullOrWhiteSpace(status))
        {
            throw new ArgumentException("Doctor status cannot be empty.", nameof(status));
        }

        if (string.IsNullOrWhiteSpace(cliAssemblyPath))
        {
            throw new ArgumentException("CLI assembly path cannot be empty.", nameof(cliAssemblyPath));
        }

        if (string.IsNullOrWhiteSpace(baseDirectory))
        {
            throw new ArgumentException("Base directory cannot be empty.", nameof(baseDirectory));
        }

        if (string.IsNullOrWhiteSpace(manifestDirectory))
        {
            throw new ArgumentException("Manifest directory cannot be empty.", nameof(manifestDirectory));
        }

        if (string.IsNullOrWhiteSpace(previewSessionStoreDirectory))
        {
            throw new ArgumentException("Preview session store directory cannot be empty.", nameof(previewSessionStoreDirectory));
        }

        Service = service;
        GeneratedAt = generatedAt;
        Status = status;
        CliAssemblyPath = Path.GetFullPath(cliAssemblyPath);
        BaseDirectory = Path.GetFullPath(baseDirectory);
        ManifestDirectory = Path.GetFullPath(manifestDirectory);
        PreviewSessionStoreDirectory = Path.GetFullPath(previewSessionStoreDirectory);
        PreviewHost = previewHost;
        Checks = checks ?? [];
        Issues = issues ?? [];
        BridgeSessions = bridgeSessions ?? [];
        PreviewSessions = previewSessions ?? [];
        ProductVersion = string.IsNullOrWhiteSpace(productVersion)
            ? service.ProductVersion
            : productVersion.Trim();
    }

    [JsonPropertyName("service")]
    public HealthResponse Service { get; }

    [JsonPropertyName("generatedAt")]
    public DateTimeOffset GeneratedAt { get; }

    [JsonPropertyName("status")]
    public string Status { get; }

    [JsonPropertyName("productVersion")]
    public string ProductVersion { get; }

    [JsonPropertyName("cliAssemblyPath")]
    public string CliAssemblyPath { get; }

    [JsonPropertyName("baseDirectory")]
    public string BaseDirectory { get; }

    [JsonPropertyName("manifestDirectory")]
    public string ManifestDirectory { get; }

    [JsonPropertyName("previewSessionStoreDirectory")]
    public string PreviewSessionStoreDirectory { get; }

    [JsonPropertyName("previewHost")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public PreviewHostDiagnostic? PreviewHost { get; }

    [JsonPropertyName("bridgeSessions")]
    public IReadOnlyList<BridgeSessionDiagnostic> BridgeSessions { get; }

    [JsonPropertyName("previewSessions")]
    public IReadOnlyList<PreviewSessionDiagnostic> PreviewSessions { get; }

    [JsonPropertyName("checks")]
    public IReadOnlyList<DoctorCheck> Checks { get; }

    [JsonPropertyName("issues")]
    public IReadOnlyList<ProtocolError> Issues { get; }
}
