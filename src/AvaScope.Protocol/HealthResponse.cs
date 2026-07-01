using System.Text.Json.Serialization;

namespace AvaScope.Protocol;

public sealed record HealthResponse
{
    [JsonConstructor]
    public HealthResponse(
        string serviceName,
        ProtocolVersion protocolVersion,
        string? productVersion = null)
    {
        if (string.IsNullOrWhiteSpace(serviceName))
        {
            throw new ArgumentException("Service name cannot be empty.", nameof(serviceName));
        }

        ServiceName = serviceName;
        ProtocolVersion = protocolVersion ?? throw new ArgumentNullException(nameof(protocolVersion));
        ProductVersion = string.IsNullOrWhiteSpace(productVersion)
            ? AvaScopeProduct.Version
            : productVersion.Trim();
    }

    [JsonPropertyName("serviceName")]
    public string ServiceName { get; }

    [JsonPropertyName("protocolVersion")]
    public ProtocolVersion ProtocolVersion { get; }

    [JsonPropertyName("productVersion")]
    public string ProductVersion { get; }

    public static HealthResponse Current() => new(AvaScopeProtocol.ServiceName, AvaScopeProtocol.CurrentVersion);
}
