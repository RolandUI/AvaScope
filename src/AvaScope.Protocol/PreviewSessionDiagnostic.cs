using System.Text.Json.Serialization;

namespace AvaScope.Protocol;

public sealed record PreviewSessionDiagnostic
{
    [JsonConstructor]
    public PreviewSessionDiagnostic(
        string status,
        string recordPath,
        SessionSummary? session = null,
        string? outputPath = null,
        ProtocolError? error = null)
    {
        if (string.IsNullOrWhiteSpace(status))
        {
            throw new ArgumentException("Preview session diagnostic status cannot be empty.", nameof(status));
        }

        if (string.IsNullOrWhiteSpace(recordPath))
        {
            throw new ArgumentException("Preview session record path cannot be empty.", nameof(recordPath));
        }

        Status = status;
        RecordPath = Path.GetFullPath(recordPath);
        Session = session;
        OutputPath = string.IsNullOrWhiteSpace(outputPath) ? null : Path.GetFullPath(outputPath);
        Error = error;
    }

    [JsonPropertyName("status")]
    public string Status { get; }

    [JsonPropertyName("recordPath")]
    public string RecordPath { get; }

    [JsonPropertyName("session")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public SessionSummary? Session { get; }

    [JsonPropertyName("outputPath")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? OutputPath { get; }

    [JsonPropertyName("error")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ProtocolError? Error { get; }
}
