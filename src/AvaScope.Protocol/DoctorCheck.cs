using System.Text.Json.Serialization;

namespace AvaScope.Protocol;

public sealed record DoctorCheck
{
    [JsonConstructor]
    public DoctorCheck(
        string name,
        string status,
        string message,
        string? path = null,
        ProtocolError? error = null)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Doctor check name cannot be empty.", nameof(name));
        }

        if (string.IsNullOrWhiteSpace(status))
        {
            throw new ArgumentException("Doctor check status cannot be empty.", nameof(status));
        }

        if (string.IsNullOrWhiteSpace(message))
        {
            throw new ArgumentException("Doctor check message cannot be empty.", nameof(message));
        }

        Name = name;
        Status = status;
        Message = message;
        Path = string.IsNullOrWhiteSpace(path) ? null : System.IO.Path.GetFullPath(path);
        Error = error;
    }

    [JsonPropertyName("name")]
    public string Name { get; }

    [JsonPropertyName("status")]
    public string Status { get; }

    [JsonPropertyName("message")]
    public string Message { get; }

    [JsonPropertyName("path")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Path { get; }

    [JsonPropertyName("error")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ProtocolError? Error { get; }
}
