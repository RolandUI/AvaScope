using System.Text.Json.Serialization;

namespace AvaScope.Protocol;

public sealed record HealthResponse
{
    [JsonConstructor]
    public HealthResponse(string serviceName, ProtocolVersion protocolVersion)
    {
        if (string.IsNullOrWhiteSpace(serviceName))
        {
            throw new ArgumentException("Service name cannot be empty.", nameof(serviceName));
        }

        ServiceName = serviceName;
        ProtocolVersion = protocolVersion ?? throw new ArgumentNullException(nameof(protocolVersion));
    }

    [JsonPropertyName("serviceName")]
    public string ServiceName { get; }

    [JsonPropertyName("protocolVersion")]
    public ProtocolVersion ProtocolVersion { get; }

    public static HealthResponse Current() => new(AvaScopeProtocol.ServiceName, AvaScopeProtocol.CurrentVersion);
}
