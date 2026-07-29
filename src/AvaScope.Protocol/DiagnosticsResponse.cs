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
        IReadOnlyList<ProtocolError>? issues = null,
        PreviewHostDiagnostic? previewHost = null,
        IReadOnlyList<PreviewSessionDiagnostic>? previewSessions = null,
        IReadOnlyList<DiagnosticIssue>? diagnosticIssues = null,
        DiagnosticsSummary? summary = null,
        IReadOnlyList<DiagnosticComponentOrigin>? componentOrigins = null,
        ResponseBudgetInfo? responseBudget = null)
    {
        ArgumentNullException.ThrowIfNull(service);

        if (string.IsNullOrWhiteSpace(manifestDirectory))
        {
            throw new ArgumentException("Manifest directory cannot be empty.", nameof(manifestDirectory));
        }

        Service = service;
        GeneratedAt = generatedAt;
        ManifestDirectory = Path.GetFullPath(manifestDirectory);
        PreviewHost = previewHost;
        BridgeSessions = bridgeSessions ?? [];
        PreviewSessions = previewSessions ?? [];
        Issues = issues ?? [];
        DiagnosticIssues = diagnosticIssues ?? [];
        Summary = summary ?? DiagnosticsSummary.Create(BridgeSessions, PreviewSessions, DiagnosticIssues);
        ComponentOrigins = componentOrigins ?? [];
        ResponseBudget = responseBudget;
    }

    [JsonPropertyName("service")]
    public HealthResponse Service { get; }

    [JsonPropertyName("generatedAt")]
    public DateTimeOffset GeneratedAt { get; }

    [JsonPropertyName("manifestDirectory")]
    public string ManifestDirectory { get; }

    [JsonPropertyName("previewHost")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public PreviewHostDiagnostic? PreviewHost { get; }

    [JsonPropertyName("bridgeSessions")]
    public IReadOnlyList<BridgeSessionDiagnostic> BridgeSessions { get; }

    [JsonPropertyName("previewSessions")]
    public IReadOnlyList<PreviewSessionDiagnostic> PreviewSessions { get; }

    [JsonPropertyName("issues")]
    public IReadOnlyList<ProtocolError> Issues { get; }

    [JsonPropertyName("diagnosticIssues")]
    public IReadOnlyList<DiagnosticIssue> DiagnosticIssues { get; }

    [JsonPropertyName("summary")]
    public DiagnosticsSummary Summary { get; }

    [JsonPropertyName("componentOrigins")]
    public IReadOnlyList<DiagnosticComponentOrigin> ComponentOrigins { get; }

    [JsonPropertyName("responseBudget")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ResponseBudgetInfo? ResponseBudget { get; }
}

public sealed record DiagnosticComponentOrigin
{
    [JsonConstructor]
    public DiagnosticComponentOrigin(
        string component,
        string assemblyPath,
        string baseDirectory,
        string rootDirectory,
        string originKind,
        bool exists = true)
    {
        if (string.IsNullOrWhiteSpace(component))
        {
            throw new ArgumentException("Diagnostic component cannot be empty.", nameof(component));
        }

        if (string.IsNullOrWhiteSpace(assemblyPath))
        {
            throw new ArgumentException("Diagnostic component assembly path cannot be empty.", nameof(assemblyPath));
        }

        if (string.IsNullOrWhiteSpace(baseDirectory))
        {
            throw new ArgumentException("Diagnostic component base directory cannot be empty.", nameof(baseDirectory));
        }

        if (string.IsNullOrWhiteSpace(rootDirectory))
        {
            throw new ArgumentException("Diagnostic component root directory cannot be empty.", nameof(rootDirectory));
        }

        if (string.IsNullOrWhiteSpace(originKind))
        {
            throw new ArgumentException("Diagnostic component origin kind cannot be empty.", nameof(originKind));
        }

        Component = component.Trim();
        AssemblyPath = Path.GetFullPath(assemblyPath);
        BaseDirectory = Path.GetFullPath(baseDirectory);
        RootDirectory = Path.GetFullPath(rootDirectory);
        OriginKind = originKind.Trim();
        Exists = exists;
    }

    [JsonPropertyName("component")]
    public string Component { get; }

    [JsonPropertyName("assemblyPath")]
    public string AssemblyPath { get; }

    [JsonPropertyName("baseDirectory")]
    public string BaseDirectory { get; }

    [JsonPropertyName("rootDirectory")]
    public string RootDirectory { get; }

    [JsonPropertyName("originKind")]
    public string OriginKind { get; }

    [JsonPropertyName("exists")]
    public bool Exists { get; }
}
