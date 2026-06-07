using System.Text.Json.Serialization;

namespace AvaScope.Protocol;

public sealed record PreviewHostDiagnostic
{
    [JsonConstructor]
    public PreviewHostDiagnostic(
        string status,
        string hostAssemblyPath,
        string processMode,
        HealthResponse? service = null,
        ProtocolError? error = null)
    {
        if (string.IsNullOrWhiteSpace(status))
        {
            throw new ArgumentException("Diagnostic status cannot be empty.", nameof(status));
        }

        if (string.IsNullOrWhiteSpace(hostAssemblyPath))
        {
            throw new ArgumentException("Preview host assembly path cannot be empty.", nameof(hostAssemblyPath));
        }

        if (string.IsNullOrWhiteSpace(processMode))
        {
            throw new ArgumentException("Process mode cannot be empty.", nameof(processMode));
        }

        Status = status;
        HostAssemblyPath = Path.GetFullPath(hostAssemblyPath);
        ProcessMode = processMode;
        Service = service;
        Error = error;
    }

    [JsonPropertyName("status")]
    public string Status { get; }

    [JsonPropertyName("hostAssemblyPath")]
    public string HostAssemblyPath { get; }

    [JsonPropertyName("processMode")]
    public string ProcessMode { get; }

    [JsonPropertyName("service")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public HealthResponse? Service { get; }

    [JsonPropertyName("error")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ProtocolError? Error { get; }
}
